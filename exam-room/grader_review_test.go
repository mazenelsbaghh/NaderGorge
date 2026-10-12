package main

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"sync"
	"testing"
	"time"
)

func TestAmbiguousAnswersReceiveFinalGradesWithoutReviewQueue(t *testing.T) {
	db, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer db.sql.Close()
	created, err := db.createExam(M{"title": "إعادة المراجعة", "instructions": "", "minutes": float64(5), "timerMode": "shared", "allowLate": true, "shuffle": false, "questionCount": float64(2), "questions": []any{
		M{"kind": "essay", "text": "سؤال العينة الأولى", "modelAnswer": "صحيح", "points": float64(2)},
		M{"kind": "essay", "text": "إجابة غير مفهومة", "modelAnswer": "صحيح", "points": float64(2)},
	}})
	if err != nil {
		t.Fatal(err)
	}
	examID := str(created["id"])
	if _, err = db.changeState(examID, "publish"); err != nil {
		t.Fatal(err)
	}
	joined, token, err := db.join(M{"code": "REVIEW", "name": "طالب مراجعة", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	attemptID := str(joined["session"].(M)["id"])
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
	var mu sync.Mutex
	seen := map[string]int{}
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		var body struct {
			Contents []struct{ Parts []struct{ Text string } }
		}
		if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
			t.Error(err)
			w.WriteHeader(400)
			return
		}
		question := "persistent"
		if strings.Contains(body.Contents[0].Parts[0].Text, "سؤال العينة الأولى") {
			question = "partial"
		}
		mu.Lock()
		seen[question]++
		attemptNo := seen[question]
		mu.Unlock()
		if question == "partial" && attemptNo == 1 {
			json.NewEncoder(w).Encode(M{"candidates": []M{{"content": M{"parts": []M{{"text": "not JSON"}}}}}})
			return
		}
		score := 0.0
		verdict := M{"correct": false, "feedback": "لا توجد أجزاء صحيحة مفهومة"}
		if question == "partial" {
			score = 0.5
			verdict["feedback"] = "درجة الجزء الصحيح المفهوم"
			verdict["needsReview"] = true
		}
		verdict["score"] = score
		json.NewEncoder(w).Encode(M{"candidates": []M{{"content": M{"parts": []M{{"text": encode(verdict)}}}}}})
	}))
	defer server.Close()
	original := geminiEndpoint
	geminiEndpoint = server.URL
	defer func() { geminiEndpoint = original }()
	grading := &grader{db: db, envPath: filepath.Join(filepath.Dir(db.path), ".env")}
	if _, err = grading.start(examID, M{"apiKey": "test-key-123456", "partialCredit": true}); err != nil {
		t.Fatal(err)
	}
	defer func() {
		grading.mu.Lock()
		if grading.cancel != nil {
			grading.cancel()
		}
		grading.mu.Unlock()
		grading.wg.Wait()
	}()
	done := make(chan struct{})
	go func() { grading.wg.Wait(); close(done) }()
	select {
	case <-done:
	case <-time.After(15 * time.Second):
		t.Fatal("final grading did not finish")
	}
	status, err := grading.status(examID)
	if err != nil {
		t.Fatal(err)
	}
	job := status["job"].(M)
	if job["state"] != "completed" || job["total"] != 2 || job["processed"] != 2 || job["graded"] != 2 || job["needs_review"] != 0 || job["failed"] != 0 {
		t.Fatalf("incorrect final counters: %#v", job)
	}
	mu.Lock()
	defer mu.Unlock()
	if seen["partial"] != 2 || seen["persistent"] != 1 {
		t.Fatalf("answers were unexpectedly requeued: %#v", seen)
	}
	saved, err := attempt(db.sql, attemptID)
	if err != nil {
		t.Fatal(err)
	}
	grades := decode(saved.Grades)
	if len(grades) != 2 {
		t.Fatalf("missing final grades: %s", saved.Grades)
	}
	var total float64
	for _, grade := range grades {
		total += num(grade.(map[string]any)["score"])
	}
	if total != 0.5 {
		t.Fatalf("model grades were changed: %s", saved.Grades)
	}
}
