package main

import (
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

func TestFiveConcurrentGradesWaitBeforeNextBatchAndPreserveAllAnswers(t *testing.T) {
	db, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer db.sql.Close()
	questions := []any{}
	for i := 0; i < 6; i++ {
		questions = append(questions, M{"kind": "essay", "text": fmt.Sprintf("سؤال %d", i), "modelAnswer": "صحيح", "points": float64(2)})
	}
	created, err := db.createExam(M{"title": "تصحيح متزامن", "instructions": "", "minutes": float64(5), "timerMode": "shared", "allowLate": true, "shuffle": false, "questionCount": float64(6), "questions": questions})
	if err != nil {
		t.Fatal(err)
	}
	examID := str(created["id"])
	if _, err = db.changeState(examID, "publish"); err != nil {
		t.Fatal(err)
	}
	_, token, err := db.join(M{"code": "BATCH", "name": "طالب تجربة", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	if _, err = db.changeState(examID, "start"); err != nil {
		t.Fatal(err)
	}
	session, err := db.session(token)
	if err != nil {
		t.Fatal(err)
	}
	answers := M{}
	for _, question := range session["session"].(M)["questions"].([]M) {
		answers[str(question["id"])] = "صحيح"
	}
	if _, err = db.saveAnswers(token, M{"revision": float64(1), "submit": true, "answers": answers}); err != nil {
		t.Fatal(err)
	}
	if _, err = db.changeState(examID, "close"); err != nil {
		t.Fatal(err)
	}

	arrivals := make(chan time.Time, 6)
	release := make(chan struct{})
	var releaseOnce sync.Once
	unblock := func() { releaseOnce.Do(func() { close(release) }) }
	var count atomic.Int32
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		position := count.Add(1)
		arrivals <- time.Now()
		if position <= 5 {
			select {
			case <-release:
			case <-r.Context().Done():
				return
			}
		}
		json.NewEncoder(w).Encode(M{"candidates": []M{{"content": M{"parts": []M{{"text": encode(M{"correct": true, "needsReview": false, "feedback": "إجابة صحيحة"})}}}}}})
	}))
	defer server.Close()
	originalEndpoint := geminiEndpoint
	geminiEndpoint = server.URL
	defer func() { geminiEndpoint = originalEndpoint }()
	grading := &grader{db: db, envPath: filepath.Join(filepath.Dir(db.path), ".env")}
	if _, err = grading.start(examID, M{"apiKey": "test-key-123456"}); err != nil {
		unblock()
		t.Fatal(err)
	}
	defer func() {
		grading.mu.Lock()
		if grading.cancel != nil {
			grading.cancel()
		}
		grading.mu.Unlock()
		unblock()
		grading.wg.Wait()
	}()
	for i := 0; i < 5; i++ {
		select {
		case <-arrivals:
		case <-time.After(3 * time.Second):
			t.Fatal("first five requests did not run concurrently")
		}
	}
	releasedAt := time.Now()
	unblock()
	select {
	case sixth := <-arrivals:
		if sixth.Sub(releasedAt) < 5*time.Second {
			t.Fatal("next batch started before the five-second interval")
		}
	case <-time.After(10 * time.Second):
		t.Fatal("next batch did not start")
	}
	grading.wg.Wait()
	status, err := grading.status(examID)
	if err != nil {
		t.Fatal(err)
	}
	job := status["job"].(M)
	if job["state"] != "completed" || job["graded"] != 6 {
		t.Fatalf("job: %#v", job)
	}
	// Concurrent grades for the same student must not overwrite each other.
	student, err := db.sql.Query(`SELECT grades FROM attempts WHERE exam_id=?`, examID)
	if err != nil {
		t.Fatal(err)
	}
	defer student.Close()
	if !student.Next() {
		t.Fatal("missing student")
	}
	var grades string
	if err = student.Scan(&grades); err != nil {
		t.Fatal(err)
	}
	if len(decode(grades)) != 6 {
		t.Fatalf("lost grades: %s", grades)
	}
}
