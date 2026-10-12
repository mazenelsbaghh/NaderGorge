package main

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"sync"
	"testing"
	"time"
)

func diagnosticTestApp(t *testing.T) (*app, *httptest.Server, *httptest.Server, string) {
	t.Helper()
	directory := t.TempDir()
	db, err := openDB(filepath.Join(directory, "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { db.sql.Close() })
	a := &app{db: db, adminToken: "ADMIN_SECRET", addresses: func() []string { return nil }, diagnostics: openDiagnosticRecorder(filepath.Join(directory, "diagnostics"))}
	t.Cleanup(func() {
		ctx, cancel := context.WithTimeout(context.Background(), time.Second)
		defer cancel()
		if !a.diagnostics.close(ctx) {
			t.Error("recorder did not drain")
		}
	})
	student := httptest.NewUnstartedServer(nil)
	a.studentPort = student.Listener.Addr().(*net.TCPAddr).Port
	student.Config.Handler = a.handler(false)
	student.Start()
	t.Cleanup(student.Close)
	admin := httptest.NewUnstartedServer(nil)
	a.adminPort = admin.Listener.Addr().(*net.TCPAddr).Port
	admin.Config.Handler = a.handler(true)
	admin.Start()
	t.Cleanup(admin.Close)
	return a, student, admin, directory
}

func diagnosticRequest(t *testing.T, server *httptest.Server, method, path, body string, authorized bool) (int, []byte) {
	t.Helper()
	request, err := http.NewRequest(method, server.URL+path, strings.NewReader(body))
	if err != nil {
		t.Fatal(err)
	}
	request.Header.Set("Origin", server.URL)
	request.Header.Set("Content-Type", "application/json")
	request.Header.Set("X-Exam-Request", "1")
	request.Header.Set("Cookie", "massar_student=COOKIE_SECRET")
	request.Header.Set("User-Agent", "NAME_SECRET PHONE_SECRET")
	if authorized {
		request.Header.Set("X-Admin-Token", "ADMIN_SECRET")
	}
	response, err := server.Client().Do(request)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	data, err := io.ReadAll(response.Body)
	if err != nil {
		t.Fatal(err)
	}
	return response.StatusCode, data
}

func TestDiagnosticsPersistSafeFailuresAndRequireAdmin(t *testing.T) {
	a, student, admin, directory := diagnosticTestApp(t)
	status, _ := diagnosticRequest(t, student, "POST", "/api/join?code=QUERY_SECRET", `{"name":"NAME_SECRET","phone":"01012345678","code":"CODE_SECRET","answers":"ANSWER_SECRET"}`, false)
	if status != 409 {
		t.Fatalf("join status=%d", status)
	}
	clientBatch := `{"events":[{"event":"request_failed","at":"2026-10-10T01:02:03Z","route":"join","status":0,"durationMs":12000}]}`
	status, body := diagnosticRequest(t, student, "POST", "/api/client-diagnostics", clientBatch, false)
	if status != 200 || !bytes.Contains(body, []byte(`"accepted":1`)) {
		t.Fatalf("client diagnostic rejected: %d %s", status, body)
	}
	for _, test := range []struct {
		server     *httptest.Server
		authorized bool
		want       int
	}{{student, false, 404}, {admin, false, 401}, {admin, true, 200}} {
		status, body = diagnosticRequest(t, test.server, "GET", "/api/diagnostics", "", test.authorized)
		if status != test.want {
			t.Fatalf("snapshot access status=%d expected=%d", status, test.want)
		}
		if test.want != 200 && bytes.Contains(body, []byte("events")) {
			t.Fatal("unauthorized diagnostic contents exposed")
		}
	}
	assertSafeDiagnosticSnapshot(t, body)
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	if !a.diagnostics.close(ctx) {
		t.Fatal("recorder did not drain")
	}
	stored, err := os.ReadFile(filepath.Join(directory, "diagnostics", "backend-events.jsonl"))
	if err != nil {
		t.Fatal(err)
	}
	assertNoDiagnosticSecrets(t, stored)
	reopened := openDiagnosticRecorder(filepath.Join(directory, "diagnostics"))
	defer reopened.close(ctx)
	snapshot := reopened.snapshot()
	events := snapshot["events"].([]diagnosticEvent)
	found := false
	for _, event := range events {
		if event.Source == "client" && event.ClientTime == "2026-10-10T01:02:03Z" {
			found = true
		}
	}
	if !found {
		t.Fatal("client incident did not survive recorder restart")
	}
}

func assertNoDiagnosticSecrets(t *testing.T, body []byte) {
	t.Helper()
	for _, secret := range []string{"NAME_SECRET", "PHONE_SECRET", "CODE_SECRET", "COOKIE_SECRET", "QUERY_SECRET", "ANSWER_SECRET", "ADMIN_SECRET", "01012345678", "127.0.0.1", "/api/join"} {
		if bytes.Contains(body, []byte(secret)) {
			t.Fatalf("private data entered diagnostic snapshot: %s", secret)
		}
	}
}
func assertSafeDiagnosticSnapshot(t *testing.T, body []byte) {
	t.Helper()
	assertNoDiagnosticSecrets(t, body)
	var snapshot struct {
		Events              []diagnosticEvent `json:"events"`
		ClientTimeUntrusted bool              `json:"clientTimeUntrusted"`
	}
	if err := json.Unmarshal(body, &snapshot); err != nil {
		t.Fatal(err)
	}
	failure, client := false, false
	for _, event := range snapshot.Events {
		if event.Source == "student" && event.Class == "join" && event.Status == 409 && event.Category == "conflict" {
			failure = true
		}
		if event.Source == "client" && event.Kind == "request_failed" && event.Class == "join" && event.ClientTime == "2026-10-10T01:02:03Z" && event.ReceivedAt != event.ClientTime {
			client = true
		}
	}
	if !failure || !client || !snapshot.ClientTimeUntrusted {
		t.Fatalf("missing diagnostic event or time provenance: %s", body)
	}
}

func TestClientDiagnosticsRejectInjectedFieldsAndOversizedBatches(t *testing.T) {
	_, student, _, _ := diagnosticTestApp(t)
	valid := `{"event":"js_error","at":"2026-10-10T01:02:03Z"}`
	for _, test := range []struct {
		body string
		want int
	}{
		{`{"events":[{"event":"js_error","at":"2026-10-10T01:02:03Z","message":"NAME_SECRET"}]}`, 400},
		{`{"events":[{"event":"request_failed","at":"2026-10-10T01:02:03Z","route":"/api/join?code=SECRET","status":0,"durationMs":10}]}`, 400},
		{`{"events":[{"event":"NAME_SECRET","at":"2026-10-10T01:02:03Z"}]}`, 400},
		{`{"events":[{"event":"request_failed","at":"2026-10-10T01:02:03Z","route":"join","status":200.5,"durationMs":10}]}`, 400},
		{`{"events":[` + strings.TrimSuffix(strings.Repeat(valid+",", 11), ",") + `]}`, 400},
		{`{"events":[]} {"student":"NAME_SECRET"}`, 400},
		{strings.Repeat("x", 8193), 413},
	} {
		status, _ := diagnosticRequest(t, student, "POST", "/api/client-diagnostics", test.body, false)
		if status != test.want {
			t.Fatalf("invalid diagnostic status=%d expected=%d", status, test.want)
		}
	}
	for _, test := range []struct{ origin, header string }{{"http://other.invalid", "1"}, {student.URL, ""}} {
		request, _ := http.NewRequest("POST", student.URL+"/api/client-diagnostics", strings.NewReader(`{"events":[`+valid+`]}`))
		request.Header.Set("Origin", test.origin)
		request.Header.Set("Content-Type", "application/json")
		request.Header.Set("X-Exam-Request", test.header)
		response, err := student.Client().Do(request)
		if err != nil {
			t.Fatal(err)
		}
		response.Body.Close()
		if response.StatusCode != 403 {
			t.Fatalf("cross-origin/headerless report accepted: %d", response.StatusCode)
		}
	}
}

func TestClientDiagnosticRateLimitDoesNotBlockExamRequests(t *testing.T) {
	a, student, _, _ := diagnosticTestApp(t)
	event := `{"event":"connection_lost","at":"2026-10-10T01:02:03Z"}`
	batch := `{"events":[` + strings.TrimSuffix(strings.Repeat(event+",", 10), ",") + `]}`
	for attempt := 0; attempt < 7; attempt++ {
		status, _ := diagnosticRequest(t, student, "POST", "/api/client-diagnostics", batch, false)
		want := 200
		if attempt == 6 {
			want = 429
		}
		if status != want {
			t.Fatalf("report %d status=%d expected=%d", attempt, status, want)
		}
	}
	status, _ := diagnosticRequest(t, student, "GET", "/api/lounge", "", false)
	if status != 200 {
		t.Fatal("diagnostic rate limit blocked exam request")
	}
	if a.diagnostics.snapshot()["recorder"].(M)["clientRejectedBatches"].(uint64) != 1 {
		t.Fatal("missing rejected report count")
	}
	limits := clientDiagnosticLimits{}
	now := time.Now()
	for i := 0; i < 200; i++ {
		if !limits.allow(fmt.Sprintf("10.77.0.%d", i), 10, now) {
			t.Fatal("global limit rejected early")
		}
	}
	if limits.allow("10.77.3.250", 1, now) {
		t.Fatal("global report budget was bypassed with a new address")
	}
	if !limits.allow("10.77.3.250", 10, now.Add(time.Minute)) {
		t.Fatal("report budget did not recover next minute")
	}
	request := httptest.NewRequest("POST", "http://localhost/api/client-diagnostics", nil)
	request.RemoteAddr = "10.77.0.22:1234"
	request.Header.Set("X-Massar-Client-IP", "10.77.0.33")
	if diagnosticClientAddress(request) != "10.77.0.22" {
		t.Fatal("remote peer spoofed limiter identity")
	}
	request.RemoteAddr = "127.0.0.1:1234"
	if diagnosticClientAddress(request) != "10.77.0.33" {
		t.Fatal("trusted local proxy identity not accepted")
	}
}

func TestDiagnosticMiddlewareRecordsSlowRequestsAndRedactsPanics(t *testing.T) {
	recorder := openDiagnosticRecorder(t.TempDir())
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	defer recorder.close(ctx)
	server := httptest.NewServer(recorder.middleware(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path == "/api/join" {
			panic("NAME_SECRET ANSWER_SECRET")
		}
		time.Sleep(2010 * time.Millisecond)
		w.Write([]byte("ok"))
	}), false))
	defer server.Close()
	status, body := diagnosticRequest(t, server, "GET", "/api/join?code=QUERY_SECRET", "", false)
	if status != 500 {
		t.Fatalf("panic status=%d", status)
	}
	assertNoDiagnosticSecrets(t, body)
	status, _ = diagnosticRequest(t, server, "GET", "/api/lounge", "", false)
	if status != 200 {
		t.Fatal("slow successful response changed")
	}
	events := recorder.snapshot()["events"].([]diagnosticEvent)
	panicSeen, slowSeen := false, false
	for _, event := range events {
		if event.Category == "panic" && event.Status == 500 {
			panicSeen = true
		}
		if event.Class == "lounge" && event.Status == 200 && event.DurationMS >= 2000 {
			slowSeen = true
		}
	}
	if !panicSeen || !slowSeen {
		t.Fatal("missing slow response or panic diagnostic")
	}
}

type blockedDiagnosticSink struct {
	entered chan struct{}
	release chan struct{}
	once    sync.Once
}

func (s *blockedDiagnosticSink) Write(data []byte) (int, error) {
	s.once.Do(func() { close(s.entered) })
	<-s.release
	return len(data), nil
}
func (s *blockedDiagnosticSink) Close() error { return nil }

func TestBlockedDiagnosticDiskDoesNotDelayHTTPAndQueueRemainsBounded(t *testing.T) {
	sink := &blockedDiagnosticSink{entered: make(chan struct{}), release: make(chan struct{})}
	recorder := newDiagnosticRecorder(sink)
	go recorder.run()
	recorder.record(diagnosticEvent{Source: "backend", Kind: "startup"})
	<-sink.entered
	for i := 0; i < diagnosticQueueSize+30; i++ {
		recorder.record(diagnosticEvent{Source: "client", Kind: "connection_lost"})
	}
	server := httptest.NewServer(recorder.middleware(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.Write([]byte("ready")) }), false))
	defer server.Close()
	server.Client().Timeout = time.Second
	status, body := diagnosticRequest(t, server, "GET", "/api/lounge", "", false)
	if status != 200 || string(body) != "ready" {
		t.Fatal("diagnostic disk delayed exam response")
	}
	state := recorder.snapshot()["recorder"].(M)
	if state["queued"].(int) > diagnosticQueueSize || state["dropped"].(uint64) == 0 {
		t.Fatal("unbounded or falsely lossless diagnostic queue")
	}
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Millisecond)
	if recorder.close(ctx) {
		t.Fatal("blocked sink incorrectly reported drained")
	}
	cancel()
	close(sink.release)
	ctx, cancel = context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	if !recorder.close(ctx) {
		t.Fatal("recorder did not drain after disk resumed")
	}
}

func TestDiagnosticRotationPreservesBoundedRestartHistory(t *testing.T) {
	directory := t.TempDir()
	file, err := openDiagnosticFile(directory, 600)
	if err != nil {
		t.Fatal(err)
	}
	for i := 0; i < 40; i++ {
		event := diagnosticEvent{ReceivedAt: time.Now().UTC().Format(time.RFC3339Nano), Source: "backend", Kind: "summary", Class: "answers", Category: "2xx", Count: uint64(i + 1)}
		if err := json.NewEncoder(file).Encode(event); err != nil {
			t.Fatal(err)
		}
	}
	file.Close()
	entries, err := os.ReadDir(directory)
	if err != nil {
		t.Fatal(err)
	}
	if len(entries) != 2 {
		t.Fatalf("rotation kept %d files", len(entries))
	}
	for _, entry := range entries {
		info, _ := entry.Info()
		if info.Size() > 600 || (runtime.GOOS != "windows" && info.Mode().Perm() != 0600) {
			t.Fatal("diagnostics exceeded retention or exposed file permissions")
		}
	}
	// Unknown fields cannot survive a restart as arbitrary diagnostic content.
	appendFile, err := os.OpenFile(filepath.Join(directory, "backend-events.jsonl"), os.O_APPEND|os.O_WRONLY, 0600)
	if err != nil {
		t.Fatal(err)
	}
	appendFile.WriteString(`{"receivedAt":"2026-10-10T01:02:03Z","source":"client","kind":"NAME_SECRET","message":"ANSWER_SECRET"}` + "\n")
	appendFile.Close()
	recorder := openDiagnosticRecorder(directory)
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	defer recorder.close(ctx)
	data, _ := json.Marshal(recorder.snapshot())
	assertNoDiagnosticSecrets(t, data)
	seen := false
	for _, event := range recorder.snapshot()["events"].([]diagnosticEvent) {
		if event.Kind == "summary" && event.Count == 40 {
			seen = true
		}
	}
	if !seen {
		t.Fatal("latest persisted summary missing after rotation/restart")
	}
}

func TestDiagnosticStartupFailureDrainsSafeReasonBeforeExit(t *testing.T) {
	directory := t.TempDir()
	recorder := openDiagnosticRecorder(directory)
	listener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	defer listener.Close()
	duplicate, err := net.Listen("tcp", listener.Addr().String())
	if err == nil {
		duplicate.Close()
		t.Fatal("test did not reproduce occupied listener")
	}
	if category := recorder.startupFailed(err); category != "address_in_use" {
		t.Fatalf("startup category=%s", category)
	}
	data, err := os.ReadFile(filepath.Join(directory, "backend-events.jsonl"))
	if err != nil {
		t.Fatal(err)
	}
	if !bytes.Contains(data, []byte(`"kind":"startup_failed"`)) || !bytes.Contains(data, []byte(`"category":"address_in_use"`)) {
		t.Fatal("startup failure was not flushed before exit")
	}
	if bytes.Contains(data, []byte(listener.Addr().String())) {
		t.Fatal("startup log exposed a raw address/error")
	}
}
