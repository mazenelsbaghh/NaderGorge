package main

import (
	"bytes"
	"context"
	"crypto/tls"
	"crypto/x509"
	"errors"
	"fmt"
	"net"
	"net/http"
	"net/http/httptest"
	"sync/atomic"
	"testing"
	"time"
)

func portalProbeTestTransport(t *testing.T, handler http.Handler) *http.Transport {
	t.Helper()
	certificate := issueCapportTestCertificate(t, time.Now().Add(-time.Hour), time.Now().Add(time.Hour))
	server := httptest.NewUnstartedServer(handler)
	server.TLS = &tls.Config{MinVersion: tls.VersionTLS12, Certificates: []tls.Certificate{certificate.pair}}
	server.StartTLS()
	t.Cleanup(server.Close)
	transport := &http.Transport{TLSClientConfig: &tls.Config{RootCAs: certificate.roots, MinVersion: tls.VersionTLS12},
		DialContext: func(ctx context.Context, network, _ string) (net.Conn, error) {
			return (&net.Dialer{}).DialContext(ctx, network, server.Listener.Addr().String())
		}}
	t.Cleanup(transport.CloseIdleConnections)
	return transport
}

func TestPortalReadinessRequiresTrustedDiscoveryAndActualStudentService(t *testing.T) {
	const validDiscovery = `{"captive":true,"user-portal-url":"https://portal.example.test/"}`
	const validLounge = `{"rooms":[],"serverTime":123}`
	for _, scenario := range []struct {
		name, contentType, discovery, lounge string
		discoveryStatus, loungeStatus        int
		valid                                bool
	}{
		{"ready", "application/captive+json", validDiscovery, validLounge, 200, 200, true},
		{"wrong media type", "text/html", validDiscovery, validLounge, 200, 200, false},
		{"invalid JSON", "application/captive+json", `<html>Router</html>`, validLounge, 200, 200, false},
		{"HTTP student URL", "application/captive+json", `{"captive":true,"user-portal-url":"http://10.77.0.1/"}`, validLounge, 200, 200, false},
		{"foreign student URL", "application/captive+json", `{"captive":true,"user-portal-url":"https://other.test/"}`, validLounge, 200, 200, false},
		{"not captive", "application/captive+json", `{"captive":false,"user-portal-url":"https://portal.example.test/"}`, validLounge, 200, 200, false},
		{"redirect", "application/captive+json", validDiscovery, validLounge, 302, 200, false},
		{"discovery unavailable", "application/captive+json", validDiscovery, validLounge, 503, 200, false},
		{"student backend down", "application/captive+json", validDiscovery, validLounge, 200, 502, false},
		{"missing room list", "application/captive+json", validDiscovery, `{"rooms":null,"serverTime":123}`, 200, 200, false},
		{"invalid server clock", "application/captive+json", validDiscovery, `{"rooms":[],"serverTime":"123"}`, 200, 200, false},
	} {
		t.Run(scenario.name, func(t *testing.T) {
			var forbiddenRequests atomic.Int32
			transport := portalProbeTestTransport(t, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				if r.Host != "portal.example.test" || r.Method != http.MethodGet {
					http.Error(w, "Unexpected probe request", 400)
					return
				}
				switch r.URL.Path {
				case "/capport":
					w.Header().Set("Content-Type", scenario.contentType)
					w.Header().Set("Location", "/forbidden-redirect")
					w.WriteHeader(scenario.discoveryStatus)
					fmt.Fprint(w, scenario.discovery)
				case "/api/lounge":
					w.Header().Set("Content-Type", "application/json; charset=utf-8")
					w.WriteHeader(scenario.loungeStatus)
					fmt.Fprint(w, scenario.lounge)
				default:
					forbiddenRequests.Add(1)
					http.NotFound(w, r)
				}
			}))
			ctx, cancel := context.WithTimeout(context.Background(), time.Second)
			defer cancel()
			err := probePortal(ctx, "portal.example.test", transport)
			if (err == nil) != scenario.valid || forbiddenRequests.Load() != 0 {
				t.Fatalf("valid=%v, err=%v, followed redirects=%d", scenario.valid, err, forbiddenRequests.Load())
			}
		})
	}
}

func TestPortalReadinessRejectsUntrustedTLSAndCertificateHostnameMismatch(t *testing.T) {
	for _, scenario := range []struct {
		hostname string
		trusted  bool
	}{{"portal.example.test", false}, {"foreign.example.test", true}} {
		var requests atomic.Int32
		transport := portalProbeTestTransport(t, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			requests.Add(1)
			w.WriteHeader(200)
		}))
		if !scenario.trusted {
			transport.TLSClientConfig.RootCAs = x509.NewCertPool()
		}
		ctx, cancel := context.WithTimeout(context.Background(), time.Second)
		err := probePortal(ctx, scenario.hostname, transport)
		cancel()
		if err == nil || requests.Load() != 0 {
			t.Fatalf("untrusted connection reached HTTP: err=%v, requests=%d", err, requests.Load())
		}
	}
}

func TestPortalReadinessSharesDeadlineAcrossDiscoveryAndStudentService(t *testing.T) {
	var loungeRequests atomic.Int32
	transport := portalProbeTestTransport(t, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path == "/capport" {
			w.Header().Set("Content-Type", "application/captive+json")
			fmt.Fprint(w, `{"captive":true,"user-portal-url":"https://portal.example.test/"}`)
			return
		}
		loungeRequests.Add(1)
		<-r.Context().Done()
	}))
	ctx, cancel := context.WithTimeout(context.Background(), 100*time.Millisecond)
	defer cancel()
	err := probePortal(ctx, "portal.example.test", transport)
	if !errors.Is(err, context.DeadlineExceeded) || loungeRequests.Load() != 1 {
		t.Fatalf("deadline not shared through student check: err=%v, requests=%d", err, loungeRequests.Load())
	}
}

func TestPortalReadinessMissingHostnameProducesNoSuccessOutput(t *testing.T) {
	var output bytes.Buffer
	if err := writePortalReadiness("", &output); err == nil || output.Len() != 0 {
		t.Fatal("missing hostname reported ready")
	}
}
