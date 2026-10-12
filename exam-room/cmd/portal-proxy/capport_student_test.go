package main

import (
	"context"
	"crypto/tls"
	"encoding/json"
	"io"
	"net"
	"net/http"
	"net/http/cookiejar"
	"net/http/httptest"
	"net/http/httputil"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

func startCapportExamBackend(t *testing.T) (string, string) {
	t.Helper()
	directory := t.TempDir()
	binary := filepath.Join(directory, "exam-server")
	if runtime.GOOS == "windows" {
		binary += ".exe"
	}
	build := exec.Command("go", "build", "-o", binary, "../..")
	if output, err := build.CombinedOutput(); err != nil {
		t.Fatalf("build actual student service: %v\n%s", err, output)
	}
	ports := make([]string, 2)
	listeners := make([]net.Listener, 0, len(ports))
	for index := range ports {
		listener, err := net.Listen("tcp", "127.0.0.1:0")
		if err != nil {
			t.Fatal(err)
		}
		ports[index] = strconv.Itoa(listener.Addr().(*net.TCPAddr).Port)
		listeners = append(listeners, listener)
		t.Cleanup(func() { listener.Close() })
	}
	for _, listener := range listeners {
		listener.Close()
	}
	command := exec.Command(binary, "--port", ports[0], "--admin-port", ports[1], "--data-dir", directory,
		"--parent-pid", strconv.Itoa(os.Getpid()))
	if err := command.Start(); err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { command.Process.Kill(); command.Wait() })
	student, admin := "http://127.0.0.1:"+ports[0], "http://127.0.0.1:"+ports[1]
	client := &http.Client{Timeout: time.Second}
	for deadline := time.Now().Add(10 * time.Second); time.Now().Before(deadline); {
		response, err := client.Get(admin + "/api/bootstrap")
		if err == nil {
			response.Body.Close()
			if response.StatusCode == http.StatusOK {
				return student, admin
			}
		}
		time.Sleep(25 * time.Millisecond)
	}
	t.Fatal("actual student backend did not start")
	return "", ""
}

type loseAnswerAcknowledgement struct {
	transport http.RoundTripper
	lost      atomic.Bool
}

func (transport *loseAnswerAcknowledgement) RoundTrip(request *http.Request) (*http.Response, error) {
	response, err := transport.transport.RoundTrip(request)
	if err == nil && request.URL.Path == "/api/answers" && !transport.lost.Swap(true) {
		io.Copy(io.Discard, response.Body)
		response.Body.Close()
		return nil, io.ErrUnexpectedEOF
	}
	return response, err
}

func TestHTTPSPortalActualStudentSessionSurvivesLostSaveAcknowledgement(t *testing.T) {
	backend, admin := startCapportExamBackend(t)
	certificate := issueCapportTestCertificate(t, time.Now().Add(-time.Hour), time.Now().Add(time.Hour))
	target, _ := url.Parse(backend)
	proxy := secureStudentProxy(target, "portal.example.test").(*httputil.ReverseProxy)
	loss := &loseAnswerAcknowledgement{transport: proxy.Transport}
	proxy.Transport = loss
	server := httptest.NewUnstartedServer(capportHandler("portal.example.test", certificate.pair.Leaf, proxy))
	server.TLS = &tls.Config{MinVersion: tls.VersionTLS12, Certificates: []tls.Certificate{certificate.pair}}
	server.StartTLS()
	defer server.Close()
	jar, err := cookiejar.New(nil)
	if err != nil {
		t.Fatal(err)
	}
	client := &http.Client{Jar: jar, Timeout: 5 * time.Second, Transport: &http.Transport{
		TLSClientConfig: &tls.Config{RootCAs: certificate.roots, MinVersion: tls.VersionTLS12},
		DialContext: func(ctx context.Context, network, _ string) (net.Conn, error) {
			return (&net.Dialer{}).DialContext(ctx, network, server.Listener.Addr().String())
		}}}
	defer client.CloseIdleConnections()
	portal := "https://portal.example.test"
	landing, err := client.Get(portal + "/")
	if err != nil {
		t.Fatal(err)
	}
	landingBody, err := io.ReadAll(landing.Body)
	landing.Body.Close()
	if err != nil || landing.StatusCode != 200 || !strings.Contains(landing.Header.Get("Content-Type"), "text/html") ||
		!strings.Contains(string(landingBody), `id="join-form"`) || !strings.Contains(string(landingBody), `name="name"`) || landing.Header.Get("Location") != "" {
		t.Fatalf("HTTPS student landing page failed: status=%d error=%v", landing.StatusCode, err)
	}
	adminToken := ""
	call := func(client *http.Client, endpoint string, payload map[string]any, expected int) (http.Header, map[string]any) {
		t.Helper()
		method, encoded := http.MethodGet, ""
		if payload != nil {
			method = http.MethodPost
			body, err := json.Marshal(payload)
			if err != nil {
				t.Fatal(err)
			}
			encoded = string(body)
		}
		request, err := http.NewRequest(method, endpoint, strings.NewReader(encoded))
		if err != nil {
			t.Fatal(err)
		}
		request.Header.Set("Origin", request.URL.Scheme+"://"+request.URL.Host)
		request.Header.Set("X-Exam-Request", "1")
		request.Header.Set("Content-Type", "application/json")
		request.Header.Set("X-Admin-Token", adminToken)
		response, err := client.Do(request)
		if err != nil {
			t.Fatal(err)
		}
		defer response.Body.Close()
		body, err := io.ReadAll(response.Body)
		if err != nil {
			t.Fatal(err)
		}
		if response.StatusCode != expected {
			t.Fatalf("%s: got %d expected %d: %s", endpoint, response.StatusCode, expected, body)
		}
		decoded := map[string]any{}
		if expected == http.StatusOK {
			if err := json.Unmarshal(body, &decoded); err != nil {
				t.Fatalf("%s: %v: %s", endpoint, err, body)
			}
		}
		return response.Header, decoded
	}
	_, bootstrap := call(http.DefaultClient, admin+"/api/bootstrap", nil, 200)
	adminToken = bootstrap["token"].(string)
	_, created := call(http.DefaultClient, admin+"/api/exams", map[string]any{
		"title": "HTTPS recovery", "instructions": "", "minutes": 15, "timerMode": "shared", "allowLate": true,
		"shuffle": false, "shuffleOptions": false, "questionCount": 1,
		"questions": []any{map[string]any{"kind": "mcq", "text": "Choose first", "points": 2,
			"options": []string{"Right", "Wrong"}, "correct": 0}},
	}, 200)
	examID := created["id"].(string)
	call(http.DefaultClient, admin+"/api/exams/"+examID+"/publish", map[string]any{}, 200)
	call(http.DefaultClient, admin+"/api/exams/"+examID+"/start", map[string]any{}, 200)
	call(client, portal+"/api/bootstrap", nil, 404)
	call(client, portal+"/api/exams", nil, 404)
	call(client, portal+"/api/lounge", nil, 200)
	headers, joined := call(client, portal+"/api/join", map[string]any{
		"name": "طالب اختبار HTTPS", "phone": "01012345678", "code": "TLS-TEST", "examId": examID,
	}, 200)
	if !strings.Contains(headers.Get("Set-Cookie"), "Secure") || !strings.Contains(headers.Get("Set-Cookie"), "HttpOnly") {
		t.Fatal("HTTPS student cookie is not secure and HttpOnly")
	}
	session := joined["session"].(map[string]any)
	question := session["questions"].([]any)[0].(map[string]any)
	answers := map[string]any{question["id"].(string): 0}
	write := map[string]any{"revision": 1, "answers": answers, "submit": false}
	call(client, portal+"/api/answers", write, 502)
	call(client, portal+"/api/answers", write, 200)
	_, resumed := call(client, portal+"/api/session", nil, 200)
	current := resumed["session"].(map[string]any)
	if current["id"] != session["id"] || current["revision"] != float64(1) || len(current["answers"].(map[string]any)) != 1 {
		t.Fatalf("lost acknowledgement changed the session: %+v", current)
	}
	call(client, portal+"/api/answers", map[string]any{"revision": 2, "answers": answers, "submit": true}, 200)
	_, finished := call(client, portal+"/api/session", nil, 200)
	if finished["session"].(map[string]any)["state"] != "submitted" {
		t.Fatal("HTTPS submission was not persisted")
	}
}
