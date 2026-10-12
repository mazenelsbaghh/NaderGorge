package main

import (
	"bytes"
	"compress/gzip"
	"io"
	"net/http/httptest"
	"strconv"
	"testing"
)

func TestStudentPageCompressionPreservesCompleteDocument(t *testing.T) {
	plain := httptest.NewRecorder()
	if err := serveWeb(plain, httptest.NewRequest("GET", "/", nil), false); err != nil {
		t.Fatal(err)
	}
	for _, encoding := range []string{"gzip", "gzip;q=0", "identity", "br, gzip;q=0.5"} {
		t.Run(encoding, func(t *testing.T) {
			request := httptest.NewRequest("GET", "/", nil)
			request.Header.Set("Accept-Encoding", encoding)
			response := httptest.NewRecorder()
			if err := serveWeb(response, request, false); err != nil {
				t.Fatal(err)
			}
			if response.Header().Get("Content-Length") != strconv.Itoa(response.Body.Len()) {
				t.Fatal("document length does not match transmitted bytes")
			}
			body := response.Body.Bytes()
			if response.Header().Get("Content-Encoding") == "gzip" {
				reader, err := gzip.NewReader(bytes.NewReader(body))
				if err != nil {
					t.Fatal(err)
				}
				body, err = io.ReadAll(reader)
				reader.Close()
				if err != nil {
					t.Fatal(err)
				}
			} else if encoding == "gzip" {
				t.Fatal("student document compression was not applied")
			}
			if encoding == "gzip;q=0" && response.Header().Get("Content-Encoding") != "" {
				t.Fatal("compressed response despite the client rejecting gzip")
			}
			if !bytes.Equal(body, plain.Body.Bytes()) {
				t.Fatal("compressed document differs from the original")
			}
			if response.Header().Get("Vary") != "Accept-Encoding" || response.Header().Get("Cache-Control") != "no-store" {
				t.Fatal("student page must not reuse a different encoding or stale release")
			}
		})
	}
}
