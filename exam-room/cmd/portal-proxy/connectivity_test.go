package main

import (
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestDirectNetworkAnswersChecksWithoutOpeningLogin(t *testing.T) {
	for _, check := range []struct {
		url, body string
		status    int
	}{
		{"http://connectivitycheck.gstatic.com/generate_204", "", 204},
		{"http://www.google.com/generate_204", "", 204},
		{"http://captive.apple.com/hotspot-detect.html", "Success", 200},
		{"http://www.apple.com/library/test/success.html", "Success", 200},
		{"http://www.msftconnecttest.com/connecttest.txt", "Microsoft Connect Test", 200},
		{"http://www.msftncsi.com/ncsi.txt", "Microsoft NCSI", 200},
	} {
		for _, method := range []string{http.MethodGet, http.MethodHead} {
			t.Run(method+" "+check.url, func(t *testing.T) {
				response := httptest.NewRecorder()
				networkHandler(true).ServeHTTP(response, httptest.NewRequest(method, check.url, nil))
				if response.Code != check.status || response.Header().Get("Location") != "" {
					t.Fatalf("unexpected login/check response: %d %v", response.Code, response.Header())
				}
				body := response.Body.String()
				if method == http.MethodHead && body != "" || method == http.MethodGet && !strings.Contains(body, check.body) {
					t.Fatalf("unexpected probe body: %q", body)
				}
			})
		}
	}
}

func TestDirectNetworkNeverExposesStudentAPIUnderForeignHosts(t *testing.T) {
	for _, check := range []struct{ method, url, origin string }{
		{http.MethodPost, "http://connectivitycheck.gstatic.com/api/answers", ""},
		{http.MethodGet, "http://connectivitycheck.gstatic.com/api/session", ""},
		{http.MethodGet, "http://captive.apple.com/api/lounge", ""},
		{http.MethodGet, "http://connectivitycheck.gstatic.com/generate_204", "http://other.test"},
		{http.MethodGet, "http://other.test/", ""},
	} {
		request := httptest.NewRequest(check.method, check.url, nil)
		request.Header.Set("Origin", check.origin)
		response := httptest.NewRecorder()
		networkHandler(true).ServeHTTP(response, request)
		if response.Code != http.StatusNotFound || response.Header().Get("Location") != "" {
			t.Fatalf("foreign request unexpectedly accepted or redirected: %s %d", check.url, response.Code)
		}
	}
}
