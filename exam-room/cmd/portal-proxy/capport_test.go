package main

import (
	"bytes"
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/tls"
	"crypto/x509"
	"encoding/json"
	"encoding/pem"
	"io"
	"math/big"
	"net/http"
	"net/http/httptest"
	"net/url"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"
)

type capportTestCertificate struct {
	pair    tls.Certificate
	roots   *x509.CertPool
	certPEM []byte
	keyPEM  []byte
}

func issueCapportTestCertificate(t *testing.T, notBefore, notAfter time.Time) capportTestCertificate {
	t.Helper()
	key, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		t.Fatal(err)
	}
	root := &x509.Certificate{SerialNumber: big.NewInt(1), IsCA: true, BasicConstraintsValid: true,
		KeyUsage: x509.KeyUsageCertSign, NotBefore: time.Now().Add(-24 * time.Hour), NotAfter: time.Now().Add(24 * time.Hour)}
	rootDER, err := x509.CreateCertificate(rand.Reader, root, root, &key.PublicKey, key)
	if err != nil {
		t.Fatal(err)
	}
	leaf := &x509.Certificate{SerialNumber: big.NewInt(2), DNSNames: []string{"portal.example.test"},
		KeyUsage: x509.KeyUsageDigitalSignature, ExtKeyUsage: []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth},
		NotBefore: notBefore, NotAfter: notAfter}
	leafDER, err := x509.CreateCertificate(rand.Reader, leaf, root, &key.PublicKey, key)
	if err != nil {
		t.Fatal(err)
	}
	keyDER, err := x509.MarshalECPrivateKey(key)
	if err != nil {
		t.Fatal(err)
	}
	certPEM := append(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: leafDER}),
		pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: rootDER})...)
	keyPEM := pem.EncodeToMemory(&pem.Block{Type: "EC PRIVATE KEY", Bytes: keyDER})
	pair, err := tls.X509KeyPair(certPEM, keyPEM)
	if err != nil {
		t.Fatal(err)
	}
	roots := x509.NewCertPool()
	roots.AppendCertsFromPEM(pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: rootDER}))
	return capportTestCertificate{pair: pair, roots: roots, certPEM: certPEM, keyPEM: keyPEM}
}

func TestCapportRequiresTrustedCurrentCertificateForConfiguredHostname(t *testing.T) {
	now := time.Now()
	for _, scenario := range []struct {
		name, hostname string
		start, end     time.Time
		trusted        bool
		valid          bool
	}{
		{"valid", "portal.example.test", now.Add(-time.Hour), now.Add(time.Hour), true, true},
		{"wrong hostname", "other.example.test", now.Add(-time.Hour), now.Add(time.Hour), true, false},
		{"expired", "portal.example.test", now.Add(-2 * time.Hour), now.Add(-time.Hour), true, false},
		{"not yet valid", "portal.example.test", now.Add(time.Hour), now.Add(2 * time.Hour), true, false},
		{"not system trusted", "portal.example.test", now.Add(-time.Hour), now.Add(time.Hour), false, false},
	} {
		t.Run(scenario.name, func(t *testing.T) {
			certificate := issueCapportTestCertificate(t, scenario.start, scenario.end)
			roots := certificate.roots
			if !scenario.trusted {
				roots = nil
			}
			err := validateCapportCertificate(scenario.hostname, certificate.pair, roots)
			if (err == nil) != scenario.valid {
				t.Fatalf("certificate accepted=%v, expected=%v: %v", err == nil, scenario.valid, err)
			}
		})
	}
}

func TestCapportTLSAdvertisesSecurePortalAndRecordsNoSecrets(t *testing.T) {
	certificate := issueCapportTestCertificate(t, time.Now().Add(-time.Hour), time.Now().Add(time.Hour))
	var diagnostics bytes.Buffer
	server := httptest.NewUnstartedServer(diagnosticHandler(capportHandler("portal.example.test", certificate.pair.Leaf, http.NotFoundHandler()), &diagnostics))
	server.TLS = &tls.Config{MinVersion: tls.VersionTLS12, Certificates: []tls.Certificate{certificate.pair}}
	server.StartTLS()
	defer server.Close()
	client := &http.Client{Transport: &http.Transport{TLSClientConfig: &tls.Config{
		RootCAs: certificate.roots, ServerName: "portal.example.test", MinVersion: tls.VersionTLS12}}}
	defer client.CloseIdleConnections()
	for _, scenario := range []struct {
		method, host, path string
		status             int
	}{
		{http.MethodGet, "portal.example.test", "/capport", 200},
		{http.MethodHead, "portal.example.test:443", "/capport", 200},
		{http.MethodPost, "portal.example.test", "/capport", 405},
		{http.MethodGet, "foreign.example.test", "/capport", 421},
		{http.MethodGet, "portal.example.test", "/api/lounge", 404},
	} {
		t.Run(scenario.method+scenario.host+scenario.path, func(t *testing.T) {
			request, err := http.NewRequest(scenario.method, server.URL+scenario.path+"?code=SECRET", nil)
			if err != nil {
				t.Fatal(err)
			}
			request.Host = scenario.host
			request.Header.Set("Cookie", "session=SECRET")
			response, err := client.Do(request)
			if err != nil {
				t.Fatal(err)
			}
			defer response.Body.Close()
			body, err := io.ReadAll(response.Body)
			if err != nil {
				t.Fatal(err)
			}
			if response.StatusCode != scenario.status || response.Header.Get("Cache-Control") != "no-store" {
				t.Fatalf("unexpected CAPPORT response %d: %s", response.StatusCode, body)
			}
			if scenario.status == 200 && response.Header.Get("Content-Type") != "application/captive+json" {
				t.Fatal("missing CAPPORT media type")
			}
			if scenario.method == http.MethodHead && len(body) != 0 {
				t.Fatal("HEAD returned a body")
			}
			if scenario.status == 200 && scenario.method == http.MethodGet {
				var advertised map[string]any
				if err := json.Unmarshal(body, &advertised); err != nil {
					t.Fatal(err)
				}
				if len(advertised) != 2 || advertised["captive"] != true || advertised["user-portal-url"] != "https://portal.example.test/" {
					t.Fatalf("incorrect offline network advertisement: %s", body)
				}
			}
		})
	}
	if strings.Contains(diagnostics.String(), "SECRET") || !strings.Contains(diagnostics.String(), `"kind":"capport"`) {
		t.Fatalf("unsafe or missing CAPPORT diagnostics: %s", diagnostics.String())
	}
}

func TestCapportNeverAdvertisesJSONOverPlainHTTP(t *testing.T) {
	certificate := issueCapportTestCertificate(t, time.Now().Add(-time.Hour), time.Now().Add(time.Hour))
	for _, endpoint := range []http.Handler{handler(), capportHandler("portal.example.test", certificate.pair.Leaf, http.NotFoundHandler())} {
		response := httptest.NewRecorder()
		endpoint.ServeHTTP(response, httptest.NewRequest(http.MethodGet, "http://portal.example.test/capport", nil))
		if strings.Contains(response.Header().Get("Content-Type"), "application/captive+json") || strings.Contains(response.Body.String(), `"captive"`) {
			t.Fatal("CAPPORT JSON exposed without TLS")
		}
	}
}

func TestCapportConfigurationFailsClosedBeforeStartingListener(t *testing.T) {
	certificate := issueCapportTestCertificate(t, time.Now().Add(-time.Hour), time.Now().Add(time.Hour))
	directory := t.TempDir()
	certFile, keyFile := filepath.Join(directory, "certificate.pem"), filepath.Join(directory, "key.pem")
	if err := os.WriteFile(certFile, certificate.certPEM, 0600); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(keyFile, certificate.keyPEM, 0600); err != nil {
		t.Fatal(err)
	}
	for _, config := range []capportConfig{
		{hostname: "portal.example.test"},
		{hostname: "10.77.0.1", certFile: certFile, keyFile: keyFile},
		{hostname: "https://portal.example.test", certFile: certFile, keyFile: keyFile},
		{hostname: "portal.example.test", certFile: certFile + ".missing", keyFile: keyFile},
		{hostname: "portal.example.test", certFile: certFile, keyFile: keyFile},
	} {
		if _, err := capportServer(config, io.Discard); err == nil {
			t.Fatalf("invalid or untrusted configuration accepted: %+v", config)
		}
		var metadata bytes.Buffer
		if err := writeCapportMetadata(config, &metadata); err == nil || metadata.Len() != 0 {
			t.Fatal("validation-only mode advertised invalid configuration")
		}
	}
}

func TestHTTPSPortalRejectsCrossOriginBeforeReachingStudentService(t *testing.T) {
	certificate := issueCapportTestCertificate(t, time.Now().Add(-time.Hour), time.Now().Add(time.Hour))
	for _, scenario := range []struct {
		origin string
		status int
	}{
		{"https://portal.example.test", 204},
		{"https://portal.example.test:443", 204},
		{"https://PORTAL.EXAMPLE.TEST", 204},
		{"http://portal.example.test", 403},
		{"https://foreign.example.test", 403},
		{"https://portal.example.test.attacker.test", 403},
		{"https://portal.example.test/ignored", 403},
		{"null", 403},
	} {
		t.Run(scenario.origin, func(t *testing.T) {
			calls := 0
			endpoint := capportHandler("portal.example.test", certificate.pair.Leaf, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				calls++
				w.WriteHeader(204)
			}))
			request := httptest.NewRequest(http.MethodPost, "https://portal.example.test/api/answers", nil)
			request.Header.Set("Origin", scenario.origin)
			response := httptest.NewRecorder()
			endpoint.ServeHTTP(response, request)
			if response.Code != scenario.status || (scenario.status == 403 && calls != 0) {
				t.Fatalf("status=%d, backend calls=%d", response.Code, calls)
			}
		})
	}
}

func TestExpiredCAPPORTStopsAdvertisingButPreservesEstablishedStudentTraffic(t *testing.T) {
	certificate := issueCapportTestCertificate(t, time.Now().Add(-2*time.Hour), time.Now().Add(-time.Hour))
	endpoint := capportHandler("portal.example.test", certificate.pair.Leaf, http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusNoContent)
	}))
	for _, scenario := range []struct {
		path   string
		status int
	}{{"/capport", 503}, {"/api/answers", 204}} {
		response := httptest.NewRecorder()
		endpoint.ServeHTTP(response, httptest.NewRequest(http.MethodGet, "https://portal.example.test"+scenario.path, nil))
		if response.Code != scenario.status {
			t.Fatalf("%s: unexpected response %d", scenario.path, response.Code)
		}
	}
}

func TestCAPPORTCertificateMetadataUsesUTCAndWholeDays(t *testing.T) {
	expires := time.Date(2026, 12, 1, 14, 0, 0, 0, time.FixedZone("test", 2*60*60))
	certificate := &x509.Certificate{NotAfter: expires}
	metadata := certificateMetadata("PORTAL.EXAMPLE.TEST", certificate, expires.Add(-49*time.Hour))
	encoded, err := json.Marshal(metadata)
	if err != nil {
		t.Fatal(err)
	}
	if metadata.Hostname != "portal.example.test" || metadata.DaysRemaining != 2 || metadata.ExpiresAt != "2026-12-01T12:00:00Z" {
		t.Fatalf("incorrect expiry metadata: %s", encoded)
	}
}

func TestHTTPSProxyKeepsCookiesAndRedirectsOnItsOwnSecureOrigin(t *testing.T) {
	var upstreamOrigin string
	backend := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("Origin") != upstreamOrigin || r.Header.Get("X-Admin-Token") != "" ||
			r.Header.Get("Forwarded") != "" || r.Header.Get("X-Forwarded-Host") != "" ||
			len(r.Cookies()) != 1 || r.Cookies()[0].Name != "massar_student" {
			http.Error(w, "Proxy did not isolate the student request", 403)
			return
		}
		http.SetCookie(w, &http.Cookie{Name: "massar_student", Value: "student", Domain: "127.0.0.1", Path: "/", HttpOnly: true, SameSite: http.SameSiteStrictMode})
		http.SetCookie(w, &http.Cookie{Name: "massar_admin", Value: "private"})
		w.Header().Set("Location", r.URL.Query().Get("destination"))
		w.WriteHeader(http.StatusFound)
	}))
	defer backend.Close()
	upstreamOrigin = backend.URL
	target, _ := url.Parse(backend.URL)
	proxy := secureStudentProxy(target, "portal.example.test")
	for _, scenario := range []struct {
		destination, expected string
		status                int
	}{
		{backend.URL + "/?resume=1", "https://portal.example.test/?resume=1", 302},
		{"http://10.77.0.1/", "https://portal.example.test/", 302},
		{"/", "/", 302},
		{"https://attacker.test/", "", 502},
		{"https://secret@portal.example.test/", "", 502},
	} {
		request := httptest.NewRequest(http.MethodGet, "https://portal.example.test/?destination="+url.QueryEscape(scenario.destination), nil)
		request.Header.Set("Origin", "https://portal.example.test")
		request.Header.Set("X-Admin-Token", "private")
		request.Header.Set("Forwarded", "host=attacker.test")
		request.Header.Set("X-Forwarded-Host", "attacker.test")
		request.AddCookie(&http.Cookie{Name: "massar_student", Value: "student"})
		request.AddCookie(&http.Cookie{Name: "massar_admin", Value: "private"})
		response := httptest.NewRecorder()
		proxy.ServeHTTP(response, request)
		if response.Code != scenario.status || response.Header().Get("Location") != scenario.expected {
			t.Fatalf("%s: response %d, location %s", scenario.destination, response.Code, response.Header().Get("Location"))
		}
		if scenario.status == 302 {
			cookies := response.Result().Cookies()
			if len(cookies) != 1 || cookies[0].Name != "massar_student" || !cookies[0].Secure || !cookies[0].HttpOnly ||
				cookies[0].Domain != "" || cookies[0].SameSite != http.SameSiteStrictMode {
				t.Fatalf("unsafe secure student cookie: %+v", cookies)
			}
		}
	}
}
