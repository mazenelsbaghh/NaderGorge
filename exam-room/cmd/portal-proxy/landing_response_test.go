package main

import (
	"bytes"
	"compress/gzip"
	"io"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strconv"
	"testing"
	"time"
)

func TestSelfContainedLandingPreservesCompressedBodyAndCSPThroughBothPortals(t *testing.T) {
	var compressed bytes.Buffer
	compressor := gzip.NewWriter(&compressed)
	if _, err := compressor.Write([]byte(`<!doctype html><html lang="ar"><style>body{color:navy}</style><body>مسار<script>window.ready=true</script></body></html>`)); err != nil {
		t.Fatal(err)
	}
	if err := compressor.Close(); err != nil {
		t.Fatal(err)
	}
	const policy = "default-src 'self'; script-src 'self' 'sha256-uG6XM/BZGWywLjhzEmreGNnfKsDjhy9Iz6XPUYXagAE='; style-src 'self' 'sha256-tdCAMNH/DJVYRZXGjkwmwIKNH1woSG03xfEgm5yNFj0='"
	backend := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "text/html; charset=utf-8")
		w.Header().Set("Content-Encoding", "gzip")
		w.Header().Set("Content-Length", strconv.Itoa(compressed.Len()))
		w.Header().Set("Content-Security-Policy", policy)
		w.Header().Set("Cache-Control", "no-store")
		w.Write(compressed.Bytes())
	}))
	defer backend.Close()
	target, _ := url.Parse(backend.URL)
	certificate := issueCapportTestCertificate(t, time.Now().Add(-time.Hour), time.Now().Add(time.Hour))
	for _, protocol := range []string{"http", "https"} {
		t.Run(protocol, func(t *testing.T) {
			var server *httptest.Server
			if protocol == "https" {
				server = httptest.NewTLSServer(capportHandler("portal.example.test", certificate.pair.Leaf,
					secureStudentProxy(target, "portal.example.test")))
			} else {
				server = httptest.NewServer(studentProxy(target))
			}
			defer server.Close()
			client := server.Client()
			transport := client.Transport.(*http.Transport).Clone()
			transport.DisableCompression = true
			client.Transport = transport
			defer client.CloseIdleConnections()
			request, _ := http.NewRequest(http.MethodGet, server.URL+"/", nil)
			request.Host = "portal.example.test"
			request.Header.Set("Accept-Encoding", "gzip")
			response, err := client.Do(request)
			if err != nil {
				t.Fatal(err)
			}
			body, err := io.ReadAll(response.Body)
			response.Body.Close()
			if err != nil || response.StatusCode != 200 || !bytes.Equal(body, compressed.Bytes()) {
				t.Fatalf("compressed landing altered: status=%d read=%v", response.StatusCode, err)
			}
			if response.Header.Get("Content-Security-Policy") != policy || response.Header.Get("Content-Encoding") != "gzip" ||
				response.ContentLength != int64(compressed.Len()) || response.Header.Get("Cache-Control") != "no-store" {
				t.Fatalf("landing headers altered: %+v", response.Header)
			}
		})
	}
}
