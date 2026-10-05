package main

import (
	"database/sql"
	"path/filepath"
	"testing"
)

func TestReturnWithSameCodeKeepsAnswersAndReplacesOldSession(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = d.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	joined, oldToken, err := d.join(M{"code": "50000", "name": "طالب اختبار", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	aid := str(joined["session"].(M)["id"])
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	current, err := d.session(oldToken)
	if err != nil {
		t.Fatal(err)
	}
	questions := current["session"].(M)["questions"].([]M)
	answers := M{str(questions[0]["id"]): float64(1)}
	if _, err = d.saveAnswers(oldToken, M{"revision": float64(1), "submit": false, "answers": answers}); err != nil {
		t.Fatal(err)
	}
	before, err := attempt(d.sql, aid)
	if err != nil {
		t.Fatal(err)
	}
	if _, _, err = d.join(M{"code": "50000", "name": "طالب اختبار", "phone": "01012345679"}, ""); err == nil {
		t.Fatal("accepted wrong phone")
	}
	restored, newToken, err := d.join(M{"code": "50000", "name": "طالب اختبار", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	after, err := attempt(d.sql, aid)
	if err != nil {
		t.Fatal(err)
	}
	if restored["session"].(M)["id"] != aid || after.Answers != before.Answers || after.QuestionIDs != before.QuestionIDs || after.Deadline != before.Deadline || after.Revision != before.Revision {
		t.Fatal("resume changed saved attempt")
	}
	if _, err = d.session(oldToken); err == nil {
		t.Fatal("old session still active")
	}
	if _, err = d.session(newToken); err != nil {
		t.Fatal(err)
	}
	if _, err = d.saveAnswers(newToken, M{"revision": float64(2), "submit": true, "answers": answers}); err != nil {
		t.Fatal(err)
	}
	if _, _, err = d.join(M{"code": "50000", "name": "طالب اختبار", "phone": "01012345678"}, ""); err == nil {
		t.Fatal("reopened submitted attempt")
	}
}
func TestAbsencePauseKeepsDeadlineAndRequiresTeacherResume(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = d.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	joined, tok, err := d.join(M{"code": "P", "name": "طالب اختبار", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	aid := str(joined["session"].(M)["id"])
	if _, err = d.absenceRule(eid, M{"seconds": float64(30)}); err != nil {
		t.Fatal(err)
	}
	if _, err = d.sql.Exec(`UPDATE attempt_presence SET last_visible=?`, now()-100); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	initial, err := d.studentPresence(tok, true)
	if err != nil {
		t.Fatal(err)
	}
	deadline := initial["session"].(M)["deadline"]
	if initial["session"].(M)["paused"] != false {
		t.Fatal("paused during waiting")
	}
	if _, err = d.studentPresence(tok, false); err != nil {
		t.Fatal(err)
	}
	if _, err = d.sql.Exec(`UPDATE attempt_presence SET hidden_since=?,last_visible=? WHERE attempt_id=?`, now()-31, now()-31, aid); err != nil {
		t.Fatal(err)
	}
	returned, err := d.studentPresence(tok, true)
	if err != nil {
		t.Fatal(err)
	}
	if returned["session"].(M)["paused"] != true || returned["session"].(M)["deadline"] != deadline {
		t.Fatal("absence pause failed")
	}
	if _, err = d.saveAnswers(tok, M{"revision": float64(1), "submit": false, "answers": M{}}); err == nil {
		t.Fatal("saved while paused")
	}
	if _, err = d.setPause(aid, false); err != nil {
		t.Fatal(err)
	}
	restored, err := d.studentPresence(tok, true)
	if err != nil || restored["session"].(M)["paused"] != false {
		t.Fatalf("resume %v %v", restored, err)
	}
	if _, err = d.setPause(aid, true); err != nil {
		t.Fatal(err)
	}
	if _, err = d.setPause(aid, false); err != nil {
		t.Fatal(err)
	}
	if _, err = d.absenceRule(eid, M{"seconds": float64(0)}); err != nil {
		t.Fatal(err)
	}
	if _, err = d.sql.Exec(`UPDATE attempt_presence SET last_visible=?,hidden_since=? WHERE attempt_id=?`, now()-100, now()-100, aid); err != nil {
		t.Fatal(err)
	}
	if err = d.tx(func(q *sql.Tx) error {
		x, e := exam(q, eid)
		if e != nil {
			return e
		}
		return pauseAbsent(q, x, now())
	}); err != nil {
		t.Fatal(err)
	}
	current, err := d.session(tok)
	if err != nil || current["session"].(M)["paused"] != false {
		t.Fatal("manual-only mode auto-paused")
	}
}
