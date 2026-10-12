package main

import (
	"net/http"
	"net/http/httptest"
	"net/url"
	"testing"
)

func TestCaptiveProbeRedirectDoesNotProxyForeignHost(t *testing.T) {
	request := httptest.NewRequest(http.MethodGet, "http://connectivitycheck.gstatic.com/generate_204", nil)
	response := httptest.NewRecorder()
	handler().ServeHTTP(response, request)
	if response.Code != http.StatusFound || response.Header().Get("Location") != "http://10.77.0.1/" {
		t.Fatalf("probe response = %d, location = %q", response.Code, response.Header().Get("Location"))
	}
	if response.Header().Get("Cache-Control") != "no-store" {
		t.Fatal("captive redirect should not be cached")
	}

	request = httptest.NewRequest(http.MethodPost, "http://connectivitycheck.gstatic.com/api/student", nil)
	response = httptest.NewRecorder()
	handler().ServeHTTP(response, request)
	if response.Code != http.StatusForbidden {
		t.Fatalf("foreign POST response = %d", response.Code)
	}

	request = httptest.NewRequest(http.MethodGet, "http://connectivitycheck.gstatic.com/generate_204", nil)
	request.Header.Set("Origin", "http://example.com")
	response = httptest.NewRecorder()
	handler().ServeHTTP(response, request)
	if response.Code != http.StatusForbidden {
		t.Fatalf("foreign Origin response = %d", response.Code)
	}
}

func TestProxyReplacesForgedClientAddressForDiagnosticRateLimit(t *testing.T) {
	backend := httptest.NewServer(http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		if request.Header.Get("X-Massar-Client-IP") != "192.0.2.10" {
			http.Error(writer, "wrong client address", http.StatusForbidden)
			return
		}
		writer.WriteHeader(http.StatusNoContent)
	}))
	defer backend.Close()
	target, _ := url.Parse(backend.URL)
	request := httptest.NewRequest(http.MethodPost, "http://127.0.0.1/api/client-diagnostics", nil)
	request.RemoteAddr = "192.0.2.10:4321"
	request.Header.Set("X-Massar-Client-IP", "192.0.2.99")
	response := httptest.NewRecorder()
	studentProxy(target).ServeHTTP(response, request)
	if response.Code != http.StatusNoContent {
		t.Fatalf("proxy forwarded forged limiter identity: %d", response.Code)
	}
}
