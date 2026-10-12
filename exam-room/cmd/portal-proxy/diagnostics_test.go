package main

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"testing"
)

func TestDiagnosticRedirectPreservesResponseAndExcludesSecrets(t *testing.T) {
	var output bytes.Buffer
	request := httptest.NewRequest("GET", "http://connectivitycheck.gstatic.com/generate_204?code=SECRET", nil)
	request.RemoteAddr = "10.77.0.25:45123"
	request.Header.Set("Cookie", "session=SECRET")
	request.Header.Set("User-Agent", "SamsungBrowser/20")
	response := httptest.NewRecorder()
	diagnosticHandler(handler(), &output).ServeHTTP(response, request)
	if response.Code != 302 || response.Header().Get("Location") != "http://10.77.0.1/" {
		t.Fatal("redirect changed")
	}
	var event portalEvent
	if err := json.Unmarshal(output.Bytes(), &event); err != nil {
		t.Fatal(err)
	}
	if event.IP != "10.77.0.25" || event.Kind != "probe" || event.Status != 302 {
		t.Fatalf("unexpected event: %+v", event)
	}
	if strings.Contains(output.String(), "SECRET") {
		t.Fatal("student secret leaked")
	}
}

func TestDiagnosticJoinDoesNotRecordAnswersOrCode(t *testing.T) {
	var output bytes.Buffer
	next := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.WriteHeader(503) })
	request := httptest.NewRequest("POST", "http://localhost/api/join?code=SECRET", strings.NewReader(`{"code":"SECRET","answer":"PRIVATE"}`))
	response := httptest.NewRecorder()
	diagnosticHandler(next, &output).ServeHTTP(response, request)
	if response.Code != 503 || strings.Contains(output.String(), "SECRET") || strings.Contains(output.String(), "PRIVATE") {
		t.Fatal("response changed or data leaked")
	}
	var event portalEvent
	json.Unmarshal(output.Bytes(), &event)
	if event.Kind != "student_join" || event.Status != 503 {
		t.Fatalf("unexpected event: %+v", event)
	}
}

func TestDiagnosticRotationAndConcurrentRecords(t *testing.T) {
	path := filepath.Join(t.TempDir(), "portal-events.jsonl")
	writer, err := openDiagnostics(path)
	if err != nil {
		t.Fatal(err)
	}
	defer func() { writer.file.Close() }()
	writer.size = diagnosticLimit
	var workers sync.WaitGroup
	for i := 0; i < 20; i++ {
		workers.Add(1)
		go func() {
			defer workers.Done()
			if _, err := writer.Write([]byte("{}\n")); err != nil {
				t.Error(err)
			}
		}()
	}
	workers.Wait()
	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatal(err)
	}
	if bytes.Count(data, []byte("\n")) != 20 {
		t.Fatal("missing concurrent records")
	}
	if _, err := os.Stat(path + ".1"); err != nil {
		t.Fatal("no rotated log")
	}
}

func TestAnswerDiagnosticsExcludePayloadAndSessionCookie(t *testing.T) {
	for _, endpoint := range []string{"http://localhost/api/answers", "https://portal.example.test/api/answers"} {
		t.Run(endpoint, func(t *testing.T) {
			var output bytes.Buffer
			next := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.WriteHeader(http.StatusOK) })
			request := httptest.NewRequest("POST", endpoint, strings.NewReader(`{"answers":{"id":"PRIVATE"}}`))
			request.Header.Set("Cookie", "massar_student=SECRET")
			response := httptest.NewRecorder()
			diagnosticHandler(next, &output).ServeHTTP(response, request)
			var event portalEvent
			if err := json.Unmarshal(output.Bytes(), &event); err != nil {
				t.Fatal(err)
			}
			if event.Kind != "answer_save" || event.Status != 200 || strings.Contains(output.String(), "PRIVATE") || strings.Contains(output.String(), "SECRET") {
				t.Fatal("unsafe or missing save diagnostic", output.String())
			}
		})
	}
}
