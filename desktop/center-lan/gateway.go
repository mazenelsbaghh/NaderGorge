package main

import (
	"bytes"
	"context"
	"crypto/subtle"
	"crypto/tls"
	"encoding/json"
	"errors"
	"io"
	"log"
	"net"
	"net/http"
	"net/http/httputil"
	"os"
	"strings"
	"sync"
	"time"
)

type HostDescription struct {
	Kind              string `json:"kind"`
	Protocol          int    `json:"protocol"`
	HostID            string `json:"hostId"`
	Name              string `json:"name"`
	Port              int    `json:"port"`
	CertificateSHA256 string `json:"certificateSha256"`
}

type Ready struct {
	Ready             bool      `json:"ready"`
	Protocol          int       `json:"protocol"`
	HostID            string    `json:"hostId"`
	Name              string    `json:"name"`
	Port              int       `json:"port"`
	CertificateSHA256 string    `json:"certificateSha256"`
	PairingCode       string    `json:"pairingCode"`
	PairingExpiresAt  time.Time `json:"pairingExpiresAt"`
}

type Gateway struct {
	config    Config
	identity  Identity
	pairing   *Pairing
	server    *http.Server
	listener  net.Listener
	discovery *net.UDPConn
	proxy     *httputil.ReverseProxy
	transport *http.Transport
	mobileMu  sync.Mutex
	mobile    *http.Server
}

type pairedDeviceKey struct{}

func newGateway(config Config) (*Gateway, error) {
	if err := config.validate(); err != nil {
		return nil, err
	}
	identity, err := loadIdentity(config.DataDir)
	if err != nil {
		return nil, err
	}
	pairing, err := loadPairing(config.DataDir)
	if err != nil {
		return nil, err
	}
	gateway := &Gateway{config: config, identity: identity, pairing: pairing}
	gateway.configureProxy()
	gateway.configureServer()
	return gateway, nil
}

func (g *Gateway) configureServer() {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", func(w http.ResponseWriter, r *http.Request) { writeJSON(w, http.StatusOK, g.description()) })
	mux.HandleFunc("POST /pair", g.pairDevice)
	mux.HandleFunc("/api/", g.proxyAPI)
	mux.HandleFunc("POST /control/pairing", g.rotatePairing)
	mux.HandleFunc("GET /control/devices", g.listDevices)
	mux.HandleFunc("POST /control/devices/revoke", g.revokeDevice)
	mux.HandleFunc("POST /control/mobile/start", g.startMobile)
	mux.HandleFunc("POST /control/mobile/stop", g.stopMobile)
	g.server = &http.Server{Handler: mux, ReadHeaderTimeout: 5 * time.Second, ReadTimeout: 30 * time.Second, WriteTimeout: 60 * time.Second, IdleTimeout: 60 * time.Second, MaxHeaderBytes: 16 << 10,
		TLSConfig: &tls.Config{MinVersion: tls.VersionTLS12, Certificates: []tls.Certificate{g.identity.Certificate}}, ErrorLog: log.New(io.Discard, "", 0)}
}

func (g *Gateway) configureProxy() {
	upstream, _ := g.config.upstreamURL()
	g.transport = &http.Transport{Proxy: nil, DialContext: (&net.Dialer{Timeout: 5 * time.Second}).DialContext, ResponseHeaderTimeout: 30 * time.Second, IdleConnTimeout: 60 * time.Second, MaxIdleConns: 16, MaxConnsPerHost: 16}
	g.proxy = &httputil.ReverseProxy{Transport: g.transport,
		Rewrite: func(request *httputil.ProxyRequest) {
			request.SetURL(upstream)
			request.Out.Host = upstream.Host
			// Device identity stays authoritative; session and snapshot version are opaque hints.
			for header := range request.Out.Header {
				if strings.HasPrefix(strings.ToLower(header), "x-massar-") && !strings.EqualFold(header, "X-Massar-Session") && !strings.EqualFold(header, "X-Massar-State-Version") && !strings.EqualFold(header, "X-Massar-State-Patch") && !strings.EqualFold(header, "X-Massar-State-Wait") && !strings.EqualFold(header, "X-Massar-State-Chunks") {
					request.Out.Header.Del(header)
				}
			}
			request.Out.Header.Del("Authorization")
			request.Out.Header.Set("X-Massar-Bridge-Secret", g.config.UpstreamSecret)
			device := request.In.Context().Value(pairedDeviceKey{}).(Device)
			request.Out.Header.Set("X-Massar-Device-ID", device.DeviceID)
		},
		ModifyResponse: func(response *http.Response) error {
			response.Header.Del("X-Massar-Bridge-Secret")
			if response.StatusCode >= 300 && response.StatusCode < 400 {
				return errors.New("API redirects are not permitted")
			}
			return nil
		},
		ErrorHandler: func(w http.ResponseWriter, r *http.Request, err error) {
			writeError(w, http.StatusBadGateway, "host_unavailable")
		},
		ErrorLog: log.New(io.Discard, "", 0),
	}
}

func (g *Gateway) start() (Ready, error) {
	listener, err := net.Listen("tcp4", net.JoinHostPort("0.0.0.0", portText(configuredPort(g.config.Port, defaultPort))))
	if err != nil {
		return Ready{}, err
	}
	g.listener = listener
	discovery, err := net.ListenUDP("udp4", &net.UDPAddr{IP: net.IPv4zero, Port: configuredPort(g.config.DiscoveryPort, defaultDiscoveryPort)})
	if err != nil {
		listener.Close()
		return Ready{}, err
	}
	g.discovery = discovery
	go g.serveDiscovery()
	go g.server.ServeTLS(listener, "", "")
	code := g.pairing.currentCode()
	return Ready{true, protocol, g.identity.HostID, g.config.Name, g.description().Port, g.identity.Fingerprint, code.Code, code.ExpiresAt}, nil
}

func (g *Gateway) close() error {
	g.closeMobile()
	if g.discovery != nil {
		g.discovery.Close()
	}
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	err := g.server.Shutdown(ctx)
	if g.listener != nil {
		g.listener.Close()
	}
	g.transport.CloseIdleConnections()
	return err
}

func (g *Gateway) description() HostDescription {
	port := configuredPort(g.config.Port, defaultPort)
	if g.listener != nil {
		port = g.listener.Addr().(*net.TCPAddr).Port
	}
	return HostDescription{"massar-host", protocol, g.identity.HostID, g.config.Name, port, g.identity.Fingerprint}
}

func writeJSON(w http.ResponseWriter, status int, body any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Header().Set("Cache-Control", "no-store")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(body)
}

func writeError(w http.ResponseWriter, status int, code string) {
	writeJSON(w, status, map[string]string{"error": code})
}

func decodeRequest(w http.ResponseWriter, r *http.Request, body any) bool {
	decoder := json.NewDecoder(http.MaxBytesReader(w, r.Body, 4096))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(body); err != nil {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return false
	}
	if err := decoder.Decode(new(any)); err != io.EOF {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return false
	}
	return true
}

func bearer(r *http.Request) string {
	authorization := r.Header.Values("Authorization")
	if len(authorization) != 1 {
		return ""
	}
	token, ok := strings.CutPrefix(authorization[0], "Bearer ")
	if !ok {
		return ""
	}
	return token
}

func remoteIP(r *http.Request) string {
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		return ""
	}
	return host
}

func (g *Gateway) pairDevice(w http.ResponseWriter, r *http.Request) {
	var request PairRequest
	if !decodeRequest(w, r, &request) {
		return
	}
	token, err := g.pairing.pair(request, remoteIP(r))
	switch {
	case errors.Is(err, errPairRate):
		w.Header().Set("Retry-After", "600")
		writeError(w, http.StatusTooManyRequests, "pairing_rate_limited")
	case errors.Is(err, errPairRejected):
		writeError(w, http.StatusForbidden, "pairing_rejected")
	case err != nil:
		writeError(w, http.StatusInternalServerError, "pairing_save_failed")
	default:
		writeJSON(w, http.StatusOK, map[string]any{"token": token, "deviceId": request.DeviceID, "hostId": g.identity.HostID, "protocol": protocol})
	}
}

func (g *Gateway) proxyAPI(w http.ResponseWriter, r *http.Request) {
	device, authenticated := g.pairing.authenticatedDevice(bearer(r))
	if !authenticated {
		writeError(w, http.StatusUnauthorized, "device_not_paired")
		return
	}
	if r.Header.Get("Upgrade") != "" ||
		!boundedHeader(r.Header, "X-Massar-Session", 4096) ||
		!boundedHeader(r.Header, "X-Massar-State-Version", 128) ||
		!boundedHeader(r.Header, "X-Massar-State-Patch", 16) ||
		!boundedHeader(r.Header, "X-Massar-State-Wait", 1) ||
		!boundedHeader(r.Header, "X-Massar-State-Chunks", 1) {
		writeError(w, http.StatusBadRequest, "invalid_request")
		return
	}
	contents, err := io.ReadAll(http.MaxBytesReader(w, r.Body, maxAPIBody))
	if err != nil {
		writeError(w, http.StatusRequestEntityTooLarge, "request_too_large")
		return
	}
	r.Body = io.NopCloser(bytes.NewReader(contents))
	r.ContentLength = int64(len(contents))
	g.proxy.ServeHTTP(w, r.WithContext(context.WithValue(r.Context(), pairedDeviceKey{}, device)))
}

func boundedHeader(headers http.Header, name string, limit int) bool {
	return len(headers.Values(name)) <= 1 && len(headers.Get(name)) <= limit
}

func (g *Gateway) owner(w http.ResponseWriter, r *http.Request) bool {
	ip := net.ParseIP(remoteIP(r))
	if ip == nil || !ip.IsLoopback() || subtle.ConstantTimeCompare([]byte(bearer(r)), []byte(g.config.UpstreamSecret)) != 1 {
		writeError(w, http.StatusForbidden, "owner_required")
		return false
	}
	return true
}

func (g *Gateway) rotatePairing(w http.ResponseWriter, r *http.Request) {
	if !g.owner(w, r) {
		return
	}
	code, err := g.pairing.rotate()
	if err != nil {
		writeError(w, http.StatusInternalServerError, "pairing_unavailable")
		return
	}
	writeJSON(w, http.StatusOK, code)
}

func (g *Gateway) listDevices(w http.ResponseWriter, r *http.Request) {
	if g.owner(w, r) {
		writeJSON(w, http.StatusOK, map[string]any{"devices": g.pairing.list()})
	}
}

func (g *Gateway) revokeDevice(w http.ResponseWriter, r *http.Request) {
	if !g.owner(w, r) {
		return
	}
	var request struct {
		DeviceID string `json:"deviceId"`
	}
	if !decodeRequest(w, r, &request) {
		return
	}
	err := g.pairing.revoke(request.DeviceID)
	if errors.Is(err, os.ErrNotExist) {
		writeError(w, http.StatusNotFound, "device_not_found")
		return
	}
	if err != nil {
		writeError(w, http.StatusInternalServerError, "revoke_failed")
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"revoked": true, "deviceId": request.DeviceID})
}
