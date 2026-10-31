package main

import (
	"encoding/json"
	"fmt"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"sync"
	"testing"
	"time"
)

// Opt-in sustained integration exercise; all accounts and the SQLite file are synthetic.
func TestAnswerDeliveryLoad(t *testing.T) {
	students, _ := strconv.Atoi(os.Getenv("MASSAR_LOAD_STUDENTS"))
	if students == 0 {
		t.Skip("set MASSAR_LOAD_STUDENTS to run the sustained HTTP load exercise")
	}
	if students < 1 || students > 1000 {
		t.Fatal("use 1–1000 students with the normal production limit")
	}
	db, err := openDB(filepath.Join(t.TempDir(), "load.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer db.sql.Close()
	created, err := db.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = db.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	a := &app{db: db, addresses: func() []string { return nil }}
	server := httptest.NewUnstartedServer(nil)
	a.studentPort = server.Listener.Addr().(*net.TCPAddr).Port
	server.Config = a.server(false)
	server.Start()
	defer server.Close()
	transport := &http.Transport{MaxIdleConns: 256, MaxIdleConnsPerHost: 256}
	defer transport.CloseIdleConnections()
	load := &deliveryLoad{origin: server.URL, client: &http.Client{Transport: transport, Timeout: 45 * time.Second}, slots: make(chan struct{}, 128), durations: map[string][]float64{}}
	clients := make([]deliveryStudent, students)
	started := time.Now()
	load.parallel(t, students, func(index int) error {
		response, cookie, err := load.request("/api/join", M{"code": fmt.Sprintf("LOAD-%04d", index), "name": "طالب ضغط تجريبي", "phone": "01012345678"}, "")
		if err == nil {
			clients[index] = deliveryStudent{cookie: cookie, id: str(response["session"].(map[string]any)["id"])}
		}
		return err
	})
	if _, err = db.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	load.parallel(t, students, func(index int) error {
		student := &clients[index]
		response, _, err := load.request("/api/session", nil, student.cookie)
		if err != nil {
			return err
		}
		questions := response["session"].(map[string]any)["questions"].([]any)
		for round := 1; round <= 6; round++ {
			student.answers = loadAnswers(questions, index, round)
			payload := M{"revision": round, "answers": student.answers, "submit": false}
			if round == 3 && index%4 == 0 {
				if err = load.discardReceipt(payload, student.cookie); err != nil {
					return err
				}
			}
			response, _, err = load.request("/api/answers", payload, student.cookie)
			if err != nil {
				return err
			}
			if response["answersReceived"] != true {
				return fmt.Errorf("missing receipt for student %d", index)
			}
			_, _, err = load.request(fmt.Sprintf("/api/session?visible=1&knownAttempt=%s&knownRevision=%d", student.id, round), nil, student.cookie)
			if err != nil {
				return err
			}
			time.Sleep(time.Duration(700+index%300) * time.Millisecond)
		}
		return nil
	})
	// Every student reconnects after the solving deadline, with an unsent final edit.
	if _, err = db.sql.Exec(`UPDATE attempts SET deadline=? WHERE exam_id=?`, now()-1, eid); err != nil {
		t.Fatal(err)
	}
	if err = db.expire(); err != nil {
		t.Fatal(err)
	}
	load.parallel(t, students, func(index int) error {
		student := &clients[index]
		for key, answer := range student.answers {
			if text, ok := answer.(string); ok {
				student.answers[key] = text + " · آخر تعديل قبل انتهاء الوقت"
			}
		}
		payload := M{"revision": 7, "answers": student.answers, "submit": true}
		if index%5 == 0 {
			if err := load.discardReceipt(payload, student.cookie); err != nil {
				return err
			}
		}
		response, _, err := load.request("/api/answers", payload, student.cookie)
		if err != nil {
			return err
		}
		if response["answersReceived"] != true || response["session"].(map[string]any)["state"] != "submitted" {
			return fmt.Errorf("unconfirmed final submission %d", index)
		}
		return nil
	})
	server.Close()
	if err = db.sql.Close(); err != nil {
		t.Fatal(err)
	}
	reopened, err := openDB(db.path)
	if err != nil {
		t.Fatal(err)
	}
	defer reopened.sql.Close()
	for index, student := range clients {
		stored, err := attempt(reopened.sql, student.id)
		if err != nil || stored.Answers != encode(student.answers) || stored.Revision != 7 || !stored.Submitted.Valid {
			t.Fatalf("student %d lost or changed answers after reopen: %v", index, err)
		}
	}
	if reopened.count(`SELECT COUNT(*) FROM attempts`) != students {
		t.Fatal("duplicate or missing attempts")
	}
	t.Logf("students=%d confirmed=%d missing=0 duplicates=0 lost_receipt_replays=%d elapsed=%s", students, students, (students+3)/4+(students+4)/5, time.Since(started).Round(time.Millisecond))
	for route, durations := range load.durations {
		sort.Float64s(durations)
		t.Logf("%s count=%d p50_ms=%.1f p95_ms=%.1f max_ms=%.1f", route, len(durations), durations[len(durations)/2], durations[len(durations)*95/100], durations[len(durations)-1])
	}
}

type deliveryStudent struct {
	cookie, id string
	answers    M
}
type deliveryLoad struct {
	origin    string
	client    *http.Client
	slots     chan struct{}
	mu        sync.Mutex
	durations map[string][]float64
}

func (load *deliveryLoad) parallel(t *testing.T, count int, action func(int) error) {
	t.Helper()
	var group sync.WaitGroup
	failures := make(chan error, count)
	for index := 0; index < count; index++ {
		group.Add(1)
		go func() {
			defer group.Done()
			if err := action(index); err != nil {
				failures <- err
			}
		}()
	}
	group.Wait()
	close(failures)
	for err := range failures {
		t.Error(err)
	}
	if t.Failed() {
		t.FailNow()
	}
}

func (load *deliveryLoad) request(route string, payload M, cookie string) (M, string, error) {
	load.slots <- struct{}{}
	defer func() { <-load.slots }()
	method, body := "GET", ""
	if payload != nil {
		method, body = "POST", encode(payload)
	}
	req, err := http.NewRequest(method, load.origin+route, strings.NewReader(body))
	if err != nil {
		return nil, "", err
	}
	req.Header.Set("X-Exam-Request", "1")
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("Cookie", cookie)
	started := time.Now()
	response, err := load.client.Do(req)
	if err != nil {
		return nil, "", err
	}
	defer response.Body.Close()
	var reply M
	err = json.NewDecoder(response.Body).Decode(&reply)
	if err != nil {
		return nil, "", err
	}
	if response.StatusCode != 200 {
		return nil, "", fmt.Errorf("%s: HTTP %d %s", route, response.StatusCode, encode(reply))
	}
	load.mu.Lock()
	key := strings.Split(route, "?")[0]
	load.durations[key] = append(load.durations[key], float64(time.Since(started).Microseconds())/1000)
	load.mu.Unlock()
	return reply, strings.Split(response.Header.Get("Set-Cookie"), ";")[0], nil
}

func (load *deliveryLoad) discardReceipt(payload M, cookie string) error {
	load.slots <- struct{}{}
	defer func() { <-load.slots }()
	req, err := http.NewRequest("POST", load.origin+"/api/answers", strings.NewReader(encode(payload)))
	if err != nil {
		return err
	}
	req.Header.Set("X-Exam-Request", "1")
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("Cookie", cookie)
	response, err := load.client.Do(req)
	if err != nil {
		return err
	}
	// Headers arrive only after the transaction commits; the application receipt is lost.
	response.Body.Close()
	if response.StatusCode != 200 {
		return fmt.Errorf("lost-receipt request: HTTP %d", response.StatusCode)
	}
	return nil
}

func loadAnswers(questions []any, student, round int) M {
	answers := M{}
	for _, raw := range questions {
		question := raw.(map[string]any)
		if question["kind"] == "mcq" {
			answers[str(question["id"])] = float64(student % 2)
		} else {
			answers[str(question["id"])] = fmt.Sprintf("طالب %d تعديل %d ", student, round) + strings.Repeat("إجابة عربية طويلة للاختبار ", 180)
		}
	}
	return answers
}
