package main

import (
	"net/http/httptest"
	"path/filepath"
	"testing"
)

type syncAttempt struct {
	db                       *DB
	examID, attemptID, token string
	answers                  M
}

func newSyncAttempt(t *testing.T) syncAttempt {
	t.Helper()
	db, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { db.sql.Close() })
	created, err := db.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = db.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	joined, token, err := db.join(M{"code": "SYNC", "name": "طالب اختبار", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	if _, err = db.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	current, err := db.session(token)
	if err != nil {
		t.Fatal(err)
	}
	answers := M{}
	for _, question := range current["session"].(M)["questions"].([]M) {
		if question["kind"] == "essay" {
			answers[str(question["id"])] = "إجابة محفوظة وقت انقطاع الشبكة"
		}
	}
	return syncAttempt{db, eid, str(joined["session"].(M)["id"]), token, answers}
}

func TestOfflineAnswersSyncOnlyWithinFiveMinuteGrace(t *testing.T) {
	for _, elapsed := range []float64{1, 299, 301} {
		t.Run(encode(elapsed), func(t *testing.T) {
			f := newSyncAttempt(t)
			deadline := now() - elapsed
			if _, err := f.db.sql.Exec(`UPDATE attempts SET deadline=? WHERE id=?`, deadline, f.attemptID); err != nil {
				t.Fatal(err)
			}
			if _, err := f.db.sql.Exec(`UPDATE exams SET started_at=? WHERE id=?`, now()-3600, f.examID); err != nil {
				t.Fatal(err)
			}
			if err := f.db.expire(); err != nil {
				t.Fatal(err)
			}
			payload := M{"revision": float64(1), "answers": f.answers, "submit": true}
			receipt, err := f.db.saveAnswers(f.token, payload)
			if err != nil {
				t.Fatal(err)
			}
			if receipt["answersReceived"] != (elapsed < 300) {
				t.Fatalf("wrong receipt: %v", receipt)
			}
			stored, err := attempt(f.db.sql, f.attemptID)
			if err != nil {
				t.Fatal(err)
			}
			if !stored.Submitted.Valid || stored.Deadline.Float64 != deadline {
				t.Fatal("deadline changed or attempt not finalized")
			}
			if elapsed < 300 && stored.Answers != encode(f.answers) || elapsed > 300 && stored.Answers != "{}" {
				t.Fatal("wrong persisted answers")
			}
			if elapsed < 300 {
				replay, err := f.db.saveAnswers(f.token, payload)
				if err != nil || replay["answersReceived"] != true {
					t.Fatalf("lost receipt replay: %v %v", replay, err)
				}
			}
		})
	}
}

func TestAutomaticSubmitUsesCurrentServerDeadline(t *testing.T) {
	f := newSyncAttempt(t)
	response, err := f.db.saveAnswers(f.token, M{"revision": float64(1), "answers": f.answers, "submit": true, "autoSubmit": true})
	if err != nil || response["answersReceived"] != true || response["session"].(M)["state"] != "running" {
		t.Fatalf("stale automatic submission must save without closing: %v %v", response, err)
	}
	if _, err := f.db.sql.Exec(`UPDATE attempts SET deadline=? WHERE id=?`, now()-1, f.attemptID); err != nil {
		t.Fatal(err)
	}
	response, err = f.db.saveAnswers(f.token, M{"revision": float64(1), "answers": f.answers, "submit": true, "autoSubmit": true})
	if err != nil || response["answersReceived"] != true || response["session"].(M)["state"] != "submitted" {
		t.Fatalf("automatic submission after actual deadline failed: %v %v", response, err)
	}
	stored, err := attempt(f.db.sql, f.attemptID)
	if err != nil || stored.SubmitReason.String != "timeout" || stored.Answers != encode(f.answers) {
		t.Fatalf("incorrect final attempt: %v %v", stored.SubmitReason, err)
	}
}

func TestGraceSurvivesRestartAndDeliversPendingThenFinalDraft(t *testing.T) {
	f := newSyncAttempt(t)
	if _, err := f.db.sql.Exec(`UPDATE attempts SET deadline=? WHERE id=?`, now()-30, f.attemptID); err != nil {
		t.Fatal(err)
	}
	path := f.db.path
	if err := f.db.sql.Close(); err != nil {
		t.Fatal(err)
	}
	db, err := openDB(path)
	if err != nil {
		t.Fatal(err)
	}
	defer db.sql.Close()
	for revision := 1; revision <= 2; revision++ {
		answers := M{}
		if revision == 2 {
			answers = f.answers
		}
		receipt, err := db.saveAnswers(f.token, M{"revision": float64(revision), "answers": answers, "submit": revision == 2})
		if err != nil || receipt["answersReceived"] != true {
			t.Fatalf("delivery %d: %v %v", revision, receipt, err)
		}
	}
	stored, err := attempt(db.sql, f.attemptID)
	if err != nil || stored.Answers != encode(f.answers) || stored.Revision != 2 || !stored.Submitted.Valid {
		t.Fatalf("final persisted draft: %v %v", stored, err)
	}
}

func TestNetworkSilenceDoesNotPauseButExplicitDepartureStillDoes(t *testing.T) {
	f := newSyncAttempt(t)
	if _, err := f.db.absenceRule(f.examID, M{"seconds": float64(5)}); err != nil {
		t.Fatal(err)
	}
	if _, err := f.db.sql.Exec(`UPDATE attempt_presence SET last_visible=? WHERE attempt_id=?`, now()-60, f.attemptID); err != nil {
		t.Fatal(err)
	}
	if err := f.db.expire(); err != nil {
		t.Fatal(err)
	}
	receipt, err := f.db.saveAnswers(f.token, M{"revision": float64(1), "answers": f.answers, "submit": false})
	if err != nil || receipt["session"].(M)["paused"] != false {
		t.Fatalf("Wi-Fi loss paused student: %v %v", receipt, err)
	}
	if _, err := f.db.sql.Exec(`UPDATE attempt_presence SET hidden_since=? WHERE attempt_id=?`, now()-60, f.attemptID); err != nil {
		t.Fatal(err)
	}
	if err := f.db.expire(); err != nil {
		t.Fatal(err)
	}
	if _, err := f.db.saveAnswers(f.token, M{"revision": float64(2), "answers": f.answers, "submit": false}); err == nil {
		t.Fatal("explicit departure ignored")
	}
}

func TestCompactPollingOnlyOmitsAlreadyKnownAttemptContent(t *testing.T) {
	f := newSyncAttempt(t)
	for _, query := range []string{"", "?knownAttempt=other&knownRevision=0", "?knownAttempt=" + f.attemptID + "&knownRevision=0"} {
		response, err := f.db.session(f.token)
		if err != nil {
			t.Fatal(err)
		}
		studentSessionJSON(httptest.NewRecorder(), httptest.NewRequest("GET", "/api/session"+query, nil), response)
		session := response["session"].(M)
		compact := query == "?knownAttempt="+f.attemptID+"&knownRevision=0"
		if (session["questions"] == nil) != compact || (session["answers"] == nil) != compact {
			t.Fatal("invalid compact response", query)
		}
	}
}
