package main

import (
	"bytes"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"testing"
)

func TestUnavailableBackendExplainsRootFailureWithoutChangingAPIRetries(t *testing.T) {
	backend := httptest.NewServer(http.NotFoundHandler())
	backend.Close()
	target, _ := url.Parse(backend.URL)
	for _, requestCase := range []struct{ method, path string }{
		{http.MethodGet, "/"}, {http.MethodHead, "/"}, {http.MethodPost, "/api/join"}, {http.MethodPost, "/api/answers"},
	} {
		t.Run(requestCase.method+requestCase.path, func(t *testing.T) {
			var output bytes.Buffer
			proxy := diagnosticHandler(studentProxy(target), &output)
			request := httptest.NewRequest(requestCase.method, "http://localhost"+requestCase.path+"?code=PRIVATE", nil)
			request.Header.Set("Cookie", "massar_student=PRIVATE")
			response := httptest.NewRecorder()
			proxy.ServeHTTP(response, request)
			if response.Code != http.StatusBadGateway || response.Header().Get("Cache-Control") != "no-store" {
				t.Fatal("failure was not returned as a non-cacheable502")
			}
			if requestCase.path == "/" {
				if !strings.Contains(response.Header().Get("Content-Type"), "text/html") || response.Header().Get("Content-Security-Policy") == "" {
					t.Fatal("root failure has no self-contained safe HTML response")
				}
				if requestCase.method == http.MethodGet && (!strings.Contains(response.Body.String(), "إعادة المحاولة") || !strings.Contains(response.Body.String(), `href="/"`)) {
					t.Fatal("root failure is blank or lacks a same-origin retry")
				}
			} else if response.Body.Len() != 0 {
				t.Fatal("API failure was replaced by an HTML page")
			}
			if requestCase.method == http.MethodHead && response.Body.Len() != 0 {
				t.Fatal("HEAD returned a response body")
			}
			if strings.Contains(response.Body.String(), "PRIVATE") || strings.Contains(output.String(), "PRIVATE") || response.Header().Get("Location") != "" {
				t.Fatal("recovery page leaked credentials or redirected the browser")
			}
			var event portalEvent
			if err := json.Unmarshal(output.Bytes(), &event); err != nil || event.ProxyError != "connection_refused" {
				t.Fatalf("lost actual failure diagnostic: %s", output.String())
			}
		})
	}
}

func TestInformationalResponseDoesNotReplaceFinalStudentFailure(t *testing.T) {
	backend := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusEarlyHints)
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusServiceUnavailable)
		io.WriteString(w, `{"error":"exam temporarily unavailable"}`)
	}))
	defer backend.Close()
	target, _ := url.Parse(backend.URL)
	var output bytes.Buffer
	portal := httptest.NewServer(diagnosticHandler(studentProxy(target), &output))
	defer portal.Close()
	response, err := portal.Client().Get(portal.URL + "/api/lounge")
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	body, err := io.ReadAll(response.Body)
	if err != nil || response.StatusCode != http.StatusServiceUnavailable || string(body) != `{"error":"exam temporarily unavailable"}` {
		t.Fatalf("informational response hid final failure: status=%d body=%s err=%v", response.StatusCode, body, err)
	}
}
