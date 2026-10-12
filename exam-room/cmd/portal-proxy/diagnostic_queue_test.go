package main

import (
	"io"
	"net/http"
	"net/http/httptest"
	"sync"
	"testing"
	"time"
)

type blockedDiagnosticStorage struct {
	entered chan struct{}
	release chan struct{}
	once    sync.Once
}

func (storage *blockedDiagnosticStorage) Write(record []byte) (int, error) {
	storage.once.Do(func() { close(storage.entered) })
	<-storage.release
	return len(record), nil
}

func TestStalledDiagnosticStorageDoesNotBlockCaptiveRedirectOrAcknowledgements(t *testing.T) {
	storage := &blockedDiagnosticStorage{entered: make(chan struct{}), release: make(chan struct{})}
	sink := queueDiagnostics(storage)
	defer close(sink.records)
	next := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path == "/generate_204" {
			http.Redirect(w, r, "http://10.77.0.1/", http.StatusFound)
			return
		}
		w.Header().Set("Content-Type", "application/json")
		io.WriteString(w, `{"saved":true}`)
	})
	server := httptest.NewServer(diagnosticHandler(next, sink))
	defer server.Close()
	defer close(storage.release)
	client := &http.Client{Timeout: time.Second, CheckRedirect: func(*http.Request, []*http.Request) error { return http.ErrUseLastResponse }}
	defer client.CloseIdleConnections()
	for _, route := range []string{"/generate_204", "/api/answers"} {
		response, err := client.Get(server.URL + route)
		if err != nil {
			t.Fatalf("diagnostic disk stalled the student response for %s: %v", route, err)
		}
		body, err := io.ReadAll(response.Body)
		response.Body.Close()
		if err != nil {
			t.Fatal(err)
		}
		if route == "/generate_204" && (response.StatusCode != 302 || response.Header.Get("Location") != "http://10.77.0.1/") {
			t.Fatal("captive redirect was changed")
		}
		if route == "/generate_204" {
			select {
			case <-storage.entered:
			case <-time.After(time.Second):
				t.Fatal("diagnostic sink was never used")
			}
			// Also fill the queue while the disk is stuck. The next student
			// acknowledgement must survive diagnostic overflow.
			for index := 0; index <= diagnosticQueueLimit; index++ {
				sink.Write([]byte("{}\n"))
			}
		}
		if route == "/api/answers" && (response.StatusCode != 200 || string(body) != `{"saved":true}`) {
			t.Fatal("student acknowledgement was changed")
		}
	}
	if sink.dropped.Load() == 0 {
		t.Fatal("test did not reach diagnostic overflow")
	}
}
