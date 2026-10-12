package main

import (
	"database/sql"
	"fmt"
	"path/filepath"
	"sort"
	"strings"
	"testing"
	"time"
)

// Large answers are deliberately present: the center status protocol needs
// grades and identities, never essay text. Fixtures remain private and synthetic.
func centerLoadFixture(tb testing.TB) (*DB, M) {
	tb.Helper()
	d, err := openDB(filepath.Join(tb.TempDir(), "synthetic.sqlite3"))
	if err != nil {
		tb.Fatal(err)
	}
	tb.Cleanup(func() { d.sql.Close() })
	config := demoExam()
	bank := []any{}
	for i := range 20 {
		bank = append(bank, M{"kind": "essay", "text": fmt.Sprintf("سؤال %d", i), "modelAnswer": "إجابة نموذجية", "points": float64(1)})
	}
	config["questions"], config["questionCount"] = bank, float64(20)
	clean, err := validateExam(config)
	if err != nil {
		tb.Fatal(err)
	}
	config = decode(encode(clean))
	selected, err := selectQuestionIDs(config)
	if err != nil {
		tb.Fatal(err)
	}
	answers, grades := M{}, M{}
	for _, questionID := range selected {
		answers[questionID] = strings.Repeat("إجابة الطالب التجريبية ", 25)
		grades[questionID] = M{"score": float64(1), "feedback": "تم التصحيح"}
	}
	err = d.tx(func(q *sql.Tx) error {
		for room := range 10 {
			eid := fmt.Sprintf("history-%d", room)
			if _, err := q.Exec(`INSERT INTO exams(id,config,state,created_at) VALUES(?,?,'closed',?)`, eid, encode(config), now()-100); err != nil {
				return err
			}
			if _, err := q.Exec(`INSERT INTO center_bindings VALUES(?,?,?,?,?)`, eid, "group", eid, "activity", 20); err != nil {
				return err
			}
			for student := range 100 {
				aid := fmt.Sprintf("%s-attempt-%d", eid, student)
				sid := fmt.Sprintf("student-%d", student)
				if _, err := q.Exec(`INSERT INTO attempts(id,exam_id,code,joined_at,question_ids,answers,grades,submitted_at) VALUES(?,?,?,?,?,?,?,?)`, aid, eid, sid, now()-90, encode(selected), encode(answers), encode(grades), now()-60); err != nil {
					return err
				}
				if _, err := q.Exec(`INSERT INTO center_attempts VALUES(?,?,?)`, aid, eid, sid); err != nil {
					return err
				}
			}
		}
		return nil
	})
	if err != nil {
		tb.Fatal(err)
	}
	state, err := d.centerState()
	if err != nil || len(state["results"].([]M)) != 1000 {
		tb.Fatalf("synthetic result preparation: %v", err)
	}
	if _, err = d.sql.Exec(`UPDATE center_results SET acked_version=version`); err != nil {
		tb.Fatal(err)
	}
	return d, config
}

func BenchmarkCenterState1000AcknowledgedAttempts(b *testing.B) {
	d, _ := centerLoadFixture(b)
	b.ReportAllocs()
	b.ResetTimer()
	for range b.N {
		state, err := d.centerState()
		if err != nil || len(state["attempts"].([]M)) != 1000 || len(state["results"].([]M)) != 0 {
			b.Fatalf("historical state changed: %v", err)
		}
	}
}

func TestCenterStateWith1000HistoryDoesNotLoseConcurrentAnswerSaves(t *testing.T) {
	d, config := centerLoadFixture(t)
	created, err := d.createExam(config)
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	payload := centerOpenPayload("unused")
	payload["examId"], payload["maxScore"] = eid, float64(20)
	if _, err = d.bindCenter(payload); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	_, sessionToken, err := d.join(M{"examId": eid, "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	active, err := exam(d.sql, eid)
	if err != nil {
		t.Fatal(err)
	}
	questionID := str(active.Config["questions"].([]any)[0].(M)["id"])
	type result struct {
		elapsed time.Duration
		err     error
	}
	polled := make(chan result, 1)
	go func() {
		start := time.Now()
		for range 5 {
			if _, err := d.centerState(); err != nil {
				polled <- result{time.Since(start), err}
				return
			}
		}
		polled <- result{elapsed: time.Since(start)}
	}()
	latencies := []time.Duration{}
	for revision := 1; revision <= 15; revision++ {
		start := time.Now()
		_, err = d.saveAnswers(sessionToken, M{"revision": float64(revision), "submit": false, "answers": M{questionID: fmt.Sprintf("إجابة محفوظة %d", revision)}})
		if err != nil {
			t.Fatal(err)
		}
		latencies = append(latencies, time.Since(start))
	}
	poll := <-polled
	if poll.err != nil {
		t.Fatal(poll.err)
	}
	view, err := d.session(sessionToken)
	if err != nil {
		t.Fatal(err)
	}
	session := view["session"].(M)
	if session["revision"] != 15 || session["answers"].(M)[questionID] != "إجابة محفوظة 15" {
		t.Fatal("concurrent center polling lost the latest answer")
	}
	sort.Slice(latencies, func(i, j int) bool { return latencies[i] < latencies[j] })
	t.Logf("1000 acknowledged attempts / 10 old rooms; 5 concurrent polls=%s, 15 answer saves median=%s max=%s", poll.elapsed, latencies[len(latencies)/2], latencies[len(latencies)-1])
}
