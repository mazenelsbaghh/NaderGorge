package main

import (
	"bytes"
	"context"
	"encoding/json"
	"net"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"testing"
	"time"
)

func requireProxyDiagnostic(t *testing.T, output *bytes.Buffer, category string) {
	t.Helper()
	var event portalEvent
	if err := json.Unmarshal(output.Bytes(), &event); err != nil {
		t.Fatal(err)
	}
	if event.Status != http.StatusBadGateway || event.ProxyError != category || event.Kind != "asset_error" {
		t.Fatalf("unexpected proxy failure event: %+v", event)
	}
	if strings.Contains(output.String(), "SECRET") {
		t.Fatal("proxy error recorded a URL parameter or cookie")
	}
}

func TestBackendRefusalIsDistinguishedFromBrowserCancellation(t *testing.T) {
	for _, scenario := range []string{"connection_refused", "request_canceled"} {
		t.Run(scenario, func(t *testing.T) {
			started := make(chan struct{})
			backend := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				close(started)
				<-r.Context().Done()
			}))
			defer backend.Close()
			target, _ := url.Parse(backend.URL)
			if scenario == "connection_refused" {
				backend.Close()
			}
			var output bytes.Buffer
			handler := diagnosticHandler(studentProxy(target), &output)
			ctx, cancel := context.WithCancel(context.Background())
			defer cancel()
			request := httptest.NewRequest(http.MethodGet, "http://localhost/assets/student.js?code=SECRET", nil).WithContext(ctx)
			request.Header.Set("Cookie", "massar_student=SECRET")
			response := httptest.NewRecorder()
			finished := make(chan struct{})
			go func() {
				handler.ServeHTTP(response, request)
				close(finished)
			}()
			if scenario == "request_canceled" {
				select {
				case <-started:
					cancel()
				case <-time.After(time.Second):
					t.Fatal("request never reached backend before cancellation")
				}
			}
			select {
			case <-finished:
			case <-time.After(time.Second):
				t.Fatal("failed proxy request did not finish")
			}
			if response.Code != http.StatusBadGateway {
				t.Fatalf("proxy status changed: %d", response.Code)
			}
			requireProxyDiagnostic(t, &output, scenario)
		})
	}
}

func TestTimeoutDiagnosticDoesNotExposeRawTransportError(t *testing.T) {
	var output bytes.Buffer
	handler := diagnosticHandler(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		studentProxyError(w, r, &net.DNSError{Err: "SECRET backend address", IsTimeout: true})
	}), &output)
	handler.ServeHTTP(httptest.NewRecorder(), httptest.NewRequest("GET", "http://localhost/assets/student.js", nil))
	requireProxyDiagnostic(t, &output, "timeout")
}
