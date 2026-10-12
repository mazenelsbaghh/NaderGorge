package main

import (
	"fmt"
	"io"
	"net"
	"net/http"
	"net/http/cookiejar"
	"net/http/httptest"
	"net/url"
	"path/filepath"
	"strings"
	"sync"
	"testing"
	"time"
)

type centerHTTPClient struct {
	base, secret string
	client       *http.Client
}

func newCenterHTTPClient(base, secret string) *centerHTTPClient {
	jar, _ := cookiejar.New(nil)
	return &centerHTTPClient{base, secret, &http.Client{Jar: jar, Timeout: 10 * time.Second}}
}

func (c *centerHTTPClient) request(path string, body M, expected int) (M, error) {
	method, content := "GET", ""
	if body != nil {
		method, content = "POST", encode(body)
	}
	r, err := http.NewRequest(method, c.base+path, strings.NewReader(content))
	if err != nil {
		return nil, err
	}
	r.Header.Set("X-Admin-Token", c.secret)
	r.Header.Set("X-Exam-Request", "1")
	r.Header.Set("Content-Type", "application/json")
	response, err := c.client.Do(r)
	if err != nil {
		return nil, err
	}
	defer response.Body.Close()
	data, err := io.ReadAll(response.Body)
	if err != nil {
		return nil, err
	}
	if response.StatusCode != expected {
		return nil, fmt.Errorf("%s %s: expected %d, got %d: %s", method, path, expected, response.StatusCode, data)
	}
	return decode(string(data)), nil
}

func (c *centerHTTPClient) must(t *testing.T, path string, body M, expected int) M {
	t.Helper()
	v, err := c.request(path, body, expected)
	if err != nil {
		t.Fatal(err)
	}
	return v
}

func centerHTTPServers(t *testing.T) (*app, *centerHTTPClient, string) {
	t.Helper()
	d, err := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { d.sql.Close() })
	a := &app{db: d, adminToken: token(), centerIntegrated: true, addresses: func() []string { return nil }}
	admin := httptest.NewUnstartedServer(a.handler(true))
	students := httptest.NewUnstartedServer(a.handler(false))
	a.adminPort = admin.Listener.Addr().(*net.TCPAddr).Port
	a.studentPort = students.Listener.Addr().(*net.TCPAddr).Port
	admin.Start()
	students.Start()
	t.Cleanup(admin.Close)
	t.Cleanup(students.Close)
	return a, newCenterHTTPClient(admin.URL, a.adminToken), students.URL
}

func runCenterConcurrent(t *testing.T, operations []func() error) {
	t.Helper()
	start := make(chan struct{})
	errors := make(chan error, len(operations))
	var group sync.WaitGroup
	for _, operation := range operations {
		group.Go(func() { <-start; errors <- operation() })
	}
	close(start)
	group.Wait()
	close(errors)
	for err := range errors {
		if err != nil {
			t.Error(err)
		}
	}
	if t.Failed() {
		t.FailNow()
	}
}

func TestCenterHTTPConcurrentReceptionSubmissionAndDurableAcknowledgements(t *testing.T) {
	_, office, studentURL := centerHTTPServers(t)
	eid := str(office.must(t, "/api/exams", demoExam(), 200)["id"])
	office.must(t, "/api/exams/"+eid+"/publish", M{}, 409)
	const students = 12
	roster := []any{}
	for i := range students {
		roster = append(roster, M{"studentId": fmt.Sprintf("student-%d", i), "code": fmt.Sprintf("%05d", 100+i), "name": fmt.Sprintf("طالب رسمي %d", i), "phone": fmt.Sprintf("0100000%04d", i), "present": true})
	}
	for i := range 2 {
		roster = append(roster, M{"studentId": fmt.Sprintf("shared-%d", i), "code": fmt.Sprintf("%05d", 900+i), "name": fmt.Sprintf("طالب مشترك %d", i), "phone": "01199999999", "present": true})
	}
	binding := M{"examId": eid, "groupId": "group", "sessionId": "session", "activityId": "named-exam", "maxScore": float64(10), "roster": roster}
	office.must(t, "/api/center/bind", binding, 200)
	office.must(t, "/api/exams/"+eid+"/publish", M{}, 200)
	office.must(t, "/api/exams/"+eid+"/start", M{}, 200)
	stranger := newCenterHTTPClient(studentURL, "")
	unknown := stranger.must(t, "/api/center/lookup", M{"phone": "01599999999", "examId": eid}, 404)
	if !strings.Contains(str(unknown["error"]), "الاستقبال") {
		t.Fatal("unknown phone lost the reception instruction")
	}
	stranger.must(t, "/api/join", M{"phone": "01599999999", "examId": eid, "code": "00100", "name": "اسم مزور"}, 404)
	stranger.must(t, "/api/center/lookup", M{"phone": "01199999999", "examId": eid}, 409)
	shared := stranger.must(t, "/api/center/lookup", M{"phone": "01199999999", "code": "00901", "examId": eid}, 200)
	if shared["code"] != "00901" {
		t.Fatal("shared phone chose the wrong official code")
	}
	clients := make([]*centerHTTPClient, students)
	attempts, essays := make([]string, students), make([]string, students)
	operations := []func() error{}
	for i := range students {
		clients[i] = newCenterHTTPClient(studentURL, "")
		operations = append(operations, func() error {
			student := roster[i].(M)
			found, err := clients[i].request("/api/center/lookup", M{"phone": student["phone"], "examId": eid}, 200)
			if err != nil {
				return err
			}
			if found["name"] != student["name"] || found["code"] != student["code"] {
				return fmt.Errorf("student %d received another identity", i)
			}
			joined, err := clients[i].request("/api/join", M{"phone": student["phone"], "examId": eid, "name": "اسم مزور", "studentId": "different"}, 200)
			if err != nil {
				return err
			}
			session := joined["session"].(map[string]any)
			if session["name"] != student["name"] || session["code"] != student["code"] {
				return fmt.Errorf("student %d trusted caller identity", i)
			}
			attempts[i] = str(session["id"])
			for _, raw := range session["questions"].([]any) {
				question := raw.(map[string]any)
				if question["kind"] == "essay" {
					essays[i] = str(question["id"])
				}
			}
			if essays[i] == "" {
				return fmt.Errorf("student %d missing essay", i)
			}
			return nil
		})
	}
	runCenterConcurrent(t, operations)
	// Reception updates an existing mobile and adds a late student while phones submit.
	roster[0].(M)["phone"] = "01288888888"
	roster = append(roster, M{"studentId": "late", "code": "00800", "name": "طالب متأخر", "phone": "01588888888", "present": true})
	binding["roster"] = roster
	operations = []func() error{func() error { _, err := office.request("/api/center/bind", binding, 200); return err }}
	for i := range students {
		operations = append(operations, func() error {
			body := M{"revision": float64(1), "submit": true, "answers": M{essays[i]: "إجابة تحتاج تصحيح"}}
			for range 2 { // A lost receipt must not duplicate an attempt or change its answer.
				response, err := clients[i].request("/api/answers", body, 200)
				if err != nil {
					return err
				}
				if response["answersReceived"] != true {
					return fmt.Errorf("student %d lost submission receipt", i)
				}
			}
			return nil
		})
	}
	runCenterConcurrent(t, operations)
	recovered := clients[0].must(t, "/api/join", M{"phone": "01288888888", "examId": eid}, 200)
	if recovered["session"].(map[string]any)["id"] != attempts[0] {
		t.Fatal("reception phone correction changed attempt ownership")
	}
	late := newCenterHTTPClient(studentURL, "")
	late.must(t, "/api/join", M{"phone": "01588888888", "examId": eid}, 200)
	late.must(t, "/api/answers", M{"revision": float64(1), "submit": true, "answers": M{}}, 200)
	state := office.must(t, "/api/center/state", nil, 200)
	if len(state["attempts"].([]any)) != students+1 || len(state["results"].([]any)) != 0 {
		t.Fatal("attempt duplicated or open exam exported prematurely")
	}
	office.must(t, "/api/exams/"+eid+"/close", M{}, 200)
	state = office.must(t, "/api/center/state", nil, 200)
	if len(state["results"].([]any)) != 1 {
		t.Fatal("only the late student's blank zero paper is fully graded")
	}
	operations = nil
	for i := range students {
		operations = append(operations, func() error {
			_, err := office.request("/api/attempts/"+attempts[i]+"/grade", M{"questionId": essays[i], "score": float64(0), "feedback": ""}, 200)
			return err
		})
	}
	runCenterConcurrent(t, operations)
	state = office.must(t, "/api/center/state", nil, 200)
	results := state["results"].([]any)
	if len(results) != students+1 {
		t.Fatalf("missing final grades: %d", len(results))
	}
	operations = nil
	for _, raw := range results {
		result := raw.(map[string]any)
		if num(result["score"]) != 0 || result["status"] != "final" || result["activityId"] != "named-exam" {
			t.Fatal("zero grade or named exam mapping changed")
		}
		for range 2 {
			operations = append(operations, func() error {
				_, err := office.request("/api/center/ack", M{"attemptId": result["attemptId"], "version": result["version"]}, 200)
				return err
			})
		}
	}
	runCenterConcurrent(t, operations)
	if len(office.must(t, "/api/center/state", nil, 200)["results"].([]any)) != 0 {
		t.Fatal("acknowledgements did not drain the outbox")
	}
	office.must(t, "/api/attempts/"+attempts[0]+"/grade", M{"questionId": essays[0], "score": float64(2.5), "feedback": "تصحيح"}, 200)
	office.must(t, "/api/center/state", nil, 200)
	operations = nil
	for range 8 {
		operations = append(operations, func() error {
			_, err := office.request("/api/center/ack", M{"attemptId": attempts[0], "version": float64(1)}, 200)
			return err
		})
	}
	runCenterConcurrent(t, operations)
	state = office.must(t, "/api/center/state", nil, 200)
	results = state["results"].([]any)
	if len(results) != 1 || num(results[0].(map[string]any)["score"]) != 2.5 || num(results[0].(map[string]any)["version"]) != 2 {
		t.Fatal("old concurrent acknowledgements hid the newer grade")
	}
	office.must(t, "/api/center/ack", M{"attemptId": attempts[0], "version": float64(3)}, 409)
}

func TestCenterHTTPExpiredBrowserCannotReopenOrRenewItsOwnAuthority(t *testing.T) {
	a, office, _ := centerHTTPServers(t)
	browser := newCenterHTTPClient(office.base, "")
	browser.must(t, "/api/bootstrap", nil, 401)
	access := office.must(t, "/api/center/admin-access", M{"enabled": true}, 200)
	u, err := url.Parse(str(access["adminUrl"]))
	if err != nil {
		t.Fatal(err)
	}
	capability := strings.TrimPrefix(u.Fragment, "center-access=")
	browser.must(t, "/api/center/browser-login", M{"capability": capability}, 200)
	browser.secret = str(browser.must(t, "/api/bootstrap", nil, 200)["token"])
	browser.must(t, "/api/center/state", nil, 403)
	a.centerAccess.mu.Lock()
	a.centerAccess.leaseExpires = time.Now().Add(-time.Second)
	a.centerAccess.mu.Unlock()
	browser.must(t, "/api/bootstrap", nil, 401)
	browser.must(t, "/api/exams", demoExam(), 401)
	office.must(t, "/api/center/state", nil, 200)
	browser.must(t, "/api/bootstrap", nil, 401)
	browser.must(t, "/api/center/browser-login", M{"capability": capability}, 401)
	if a.db.count(`SELECT COUNT(*) FROM exams`) != 0 {
		t.Fatal("expired administrator created an exam")
	}
	access = office.must(t, "/api/center/admin-access", M{"enabled": true}, 200)
	u, _ = url.Parse(str(access["adminUrl"]))
	capability = strings.TrimPrefix(u.Fragment, "center-access=")
	office.must(t, "/api/center/admin-access", M{"enabled": false}, 200)
	browser.must(t, "/api/center/browser-login", M{"capability": capability}, 401)
}
