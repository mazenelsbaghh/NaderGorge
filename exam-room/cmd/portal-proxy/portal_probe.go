package main

import (
	"context"
	"crypto/tls"
	"encoding/json"
	"fmt"
	"io"
	"mime"
	"net"
	"net/http"
	"strings"
	"time"
)

func writePortalReadiness(hostname string, output io.Writer) error {
	if err := validateCapportHostname(hostname); err != nil {
		return err
	}
	hostname = strings.ToLower(hostname)
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	transport := &http.Transport{
		TLSClientConfig: &tls.Config{ServerName: hostname, MinVersion: tls.VersionTLS12},
		DialContext: func(ctx context.Context, _, _ string) (net.Conn, error) {
			return (&net.Dialer{}).DialContext(ctx, "tcp4", "10.77.0.1:443")
		},
	}
	defer transport.CloseIdleConnections()
	if err := probePortal(ctx, hostname, transport); err != nil {
		return err
	}
	return json.NewEncoder(output).Encode(struct {
		Ready    bool   `json:"ready"`
		Hostname string `json:"hostname"`
	}{Ready: true, Hostname: hostname})
}

func probePortal(ctx context.Context, hostname string, transport http.RoundTripper) error {
	client := &http.Client{Transport: transport, CheckRedirect: func(*http.Request, []*http.Request) error {
		return http.ErrUseLastResponse
	}}
	discovery, err := portalProbeJSON(ctx, client, "https://"+hostname+"/capport", "application/captive+json")
	if err != nil {
		return fmt.Errorf("CAPPORT HTTPS check: %w", err)
	}
	if discovery["captive"] != true || discovery["user-portal-url"] != "https://"+hostname+"/" {
		return fmt.Errorf("CAPPORT must advertise the configured HTTPS student portal")
	}
	lounge, err := portalProbeJSON(ctx, client, "https://"+hostname+"/api/lounge", "application/json")
	if err != nil {
		return fmt.Errorf("Student HTTPS service check: %w", err)
	}
	_, roomsValid := lounge["rooms"].([]any)
	_, timeValid := lounge["serverTime"].(float64)
	if !roomsValid || !timeValid {
		return fmt.Errorf("Student HTTPS service returned an invalid lounge response")
	}
	return nil
}

func portalProbeJSON(ctx context.Context, client *http.Client, endpoint, contentType string) (map[string]any, error) {
	request, err := http.NewRequestWithContext(ctx, http.MethodGet, endpoint, nil)
	if err != nil {
		return nil, err
	}
	request.Header.Set("Accept", contentType)
	response, err := client.Do(request)
	if err != nil {
		return nil, err
	}
	defer response.Body.Close()
	mediaType, _, err := mime.ParseMediaType(response.Header.Get("Content-Type"))
	if response.StatusCode != http.StatusOK || err != nil || mediaType != contentType {
		return nil, fmt.Errorf("unexpected HTTP status %d or content type", response.StatusCode)
	}
	const maxResponse = 1024 * 1024
	body, err := io.ReadAll(io.LimitReader(response.Body, maxResponse+1))
	if err != nil {
		return nil, err
	}
	if len(body) > maxResponse {
		return nil, fmt.Errorf("portal response exceeds size limit")
	}
	var document map[string]any
	if err := json.Unmarshal(body, &document); err != nil {
		return nil, fmt.Errorf("invalid portal JSON: %w", err)
	}
	return document, nil
}
