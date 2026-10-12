package main

import (
	"crypto/tls"
	"crypto/x509"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"regexp"
	"strings"
	"time"
)

type capportConfig struct {
	hostname string
	certFile string
	keyFile  string
	listen   string
}

var capportHostname = regexp.MustCompile(`^[a-zA-Z0-9](?:[a-zA-Z0-9.-]*[a-zA-Z0-9])?$`)

func (config capportConfig) configured() bool {
	return config.hostname != "" || config.certFile != "" || config.keyFile != ""
}

func (config capportConfig) tlsConfig() (*tls.Config, error) {
	if config.hostname == "" || config.certFile == "" || config.keyFile == "" {
		return nil, fmt.Errorf("CAPPORT requires --capport-hostname, --capport-cert and --capport-key together")
	}
	if err := validateCapportHostname(config.hostname); err != nil {
		return nil, err
	}
	certificate, err := tls.LoadX509KeyPair(config.certFile, config.keyFile)
	if err != nil {
		return nil, fmt.Errorf("CAPPORT certificate/key: %w", err)
	}
	if err = validateCapportCertificate(config.hostname, certificate, nil); err != nil {
		return nil, err
	}
	certificate.Leaf, err = x509.ParseCertificate(certificate.Certificate[0])
	if err != nil {
		return nil, err
	}
	return &tls.Config{MinVersion: tls.VersionTLS12, Certificates: []tls.Certificate{certificate}}, nil
}

func validateCapportHostname(hostname string) error {
	if !capportHostname.MatchString(hostname) || !strings.Contains(hostname, ".") || net.ParseIP(hostname) != nil {
		return fmt.Errorf("CAPPORT requires a DNS hostname covered by a publicly trusted certificate")
	}
	return nil
}

type capportMetadata struct {
	Hostname      string `json:"hostname"`
	ExpiresAt     string `json:"expiresAt"`
	DaysRemaining int    `json:"daysRemaining"`
}

func writeCapportMetadata(config capportConfig, output io.Writer) error {
	verified, err := config.tlsConfig()
	if err != nil {
		return err
	}
	return json.NewEncoder(output).Encode(certificateMetadata(config.hostname, verified.Certificates[0].Leaf, time.Now()))
}

func certificateMetadata(hostname string, certificate *x509.Certificate, current time.Time) capportMetadata {
	return capportMetadata{Hostname: strings.ToLower(hostname), ExpiresAt: certificate.NotAfter.UTC().Format(time.RFC3339),
		DaysRemaining: int(certificate.NotAfter.Sub(current) / (24 * time.Hour))}
}

func validateCapportCertificate(hostname string, certificate tls.Certificate, roots *x509.CertPool) error {
	leaf, err := x509.ParseCertificate(certificate.Certificate[0])
	if err != nil {
		return fmt.Errorf("CAPPORT leaf certificate: %w", err)
	}
	intermediates := x509.NewCertPool()
	for _, encoded := range certificate.Certificate[1:] {
		intermediate, err := x509.ParseCertificate(encoded)
		if err != nil {
			return fmt.Errorf("CAPPORT intermediate certificate: %w", err)
		}
		intermediates.AddCert(intermediate)
	}
	_, err = leaf.Verify(x509.VerifyOptions{DNSName: hostname, Roots: roots, Intermediates: intermediates})
	if err != nil {
		return fmt.Errorf("CAPPORT certificate must be currently valid and trusted for %s: %w", hostname, err)
	}
	return nil
}

func capportHandler(hostname string, certificate *x509.Certificate, student http.Handler) http.Handler {
	portalURL := "https://" + strings.ToLower(hostname) + "/"
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Cache-Control", "no-store")
		if r.TLS == nil || !strings.EqualFold(strings.TrimSuffix(r.Host, ":443"), hostname) {
			http.Error(w, "Invalid CAPPORT host or transport", http.StatusMisdirectedRequest)
			return
		}
		if origin := r.Header.Get("Origin"); origin != "" && !sameCapportOrigin(origin, hostname) {
			http.Error(w, "Invalid portal request origin", http.StatusForbidden)
			return
		}
		if r.URL.Path != "/capport" {
			student.ServeHTTP(w, r)
			return
		}
		if current := time.Now(); current.Before(certificate.NotBefore) || !current.Before(certificate.NotAfter) {
			http.Error(w, "Portal certificate expired or not yet valid; contact the exam supervisor", http.StatusServiceUnavailable)
			return
		}
		if r.Method != http.MethodGet && r.Method != http.MethodHead {
			w.Header().Set("Allow", "GET, HEAD")
			http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
			return
		}
		w.Header().Set("Content-Type", "application/captive+json")
		// This dedicated LAN remains captive; joining an exam does not grant Internet access.
		if r.Method == http.MethodGet {
			json.NewEncoder(w).Encode(map[string]any{"captive": true, "user-portal-url": portalURL})
		}
	})
}

func sameCapportOrigin(origin, hostname string) bool {
	parsed, err := url.Parse(origin)
	return err == nil && parsed.Scheme == "https" && parsed.User == nil && parsed.Path == "" &&
		parsed.RawQuery == "" && parsed.Fragment == "" &&
		strings.EqualFold(strings.TrimSuffix(parsed.Host, ":443"), hostname)
}

func secureStudentProxy(target *url.URL, hostname string) http.Handler {
	proxy := studentProxy(target)
	proxy.ModifyResponse = func(response *http.Response) error {
		cookies := response.Cookies()
		response.Header.Del("Set-Cookie")
		for _, cookie := range cookies {
			if cookie.Name != "massar_student" {
				continue
			}
			cookie.Secure = true
			cookie.Domain = ""
			response.Header.Add("Set-Cookie", cookie.String())
		}
		if location := response.Header.Get("Location"); location != "" {
			redirect, err := url.Parse(location)
			if err != nil {
				return fmt.Errorf("invalid student redirect: %w", err)
			}
			if redirect.User != nil {
				return fmt.Errorf("student redirect must not contain credentials")
			}
			if redirect.IsAbs() || redirect.Host != "" {
				if redirect.Host != target.Host && redirect.Host != "10.77.0.1" && !strings.EqualFold(redirect.Host, hostname) {
					return fmt.Errorf("student redirect outside the configured portal is forbidden")
				}
				redirect.Scheme, redirect.Host = "https", hostname
				response.Header.Set("Location", redirect.String())
			}
		}
		return nil
	}
	return proxy
}

func capportServer(config capportConfig, sink io.Writer) (*http.Server, error) {
	tlsConfig, err := config.tlsConfig()
	if err != nil {
		return nil, err
	}
	address := config.listen
	if address == "" {
		address = ":443"
	}
	server := portalServer(address, diagnosticHandler(capportHandler(config.hostname, tlsConfig.Certificates[0].Leaf,
		secureStudentProxy(studentUpstream(), config.hostname)), sink))
	server.TLSConfig = tlsConfig
	return server, nil
}
