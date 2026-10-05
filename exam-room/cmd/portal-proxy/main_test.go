package main

import (
	"net/http"
	"net/http/httptest"
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
