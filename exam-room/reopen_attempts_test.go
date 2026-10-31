package main

import (
	"path/filepath"
	"testing"
)

func TestReopenSelectedAttemptsPreservesAnswersAndRejectsStaleWrites(t *testing.T) {
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
	joined, token, err := d.join(M{"code": "REOPEN", "name": "طالب اختبار", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	aid := str(joined["session"].(M)["id"])
	joinedSecond, secondToken, err := d.join(M{"code": "REOPEN-2", "name": "طالب ثان", "phone": "01012345679"}, "")
	if err != nil {
		t.Fatal(err)
	}
	secondID := str(joinedSecond["session"].(M)["id"])
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	current, err := d.session(token)
	if err != nil {
		t.Fatal(err)
	}
	answers := M{}
	for _, question := range current["session"].(M)["questions"].([]M) {
		if question["kind"] == "essay" {
			answers[str(question["id"])] = "إجابة الطالب الأصلية"
		}
	}
	if _, err = d.saveAnswers(token, M{"revision": float64(1), "answers": answers, "submit": true}); err != nil {
		t.Fatal(err)
	}
	if _, err = d.saveAnswers(secondToken, M{"revision": float64(1), "answers": M{}, "submit": true}); err != nil {
		t.Fatal(err)
	}
	before, err := attempt(d.sql, aid)
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.reopenAttempts(eid, M{"minutes": float64(10), "attemptIds": []any{aid, "missing"}}); err == nil {
		t.Fatal("invalid group accepted")
	}
	unchanged, _ := attempt(d.sql, aid)
	if !unchanged.Submitted.Valid || unchanged.Revision != before.Revision {
		t.Fatal("group validation changed attempt")
	}
	if _, err = d.reopenAttempts(eid, M{"minutes": float64(10), "attemptIds": []any{aid, secondID}}); err != nil {
		t.Fatal(err)
	}
	second, err := attempt(d.sql, secondID)
	if err != nil || second.Submitted.Valid || second.Answers != "{}" {
		t.Fatalf("second selected student was not reopened safely: %v", err)
	}
	after, err := attempt(d.sql, aid)
	if err != nil || after.Submitted.Valid || after.Answers != before.Answers || after.Revision != before.Revision+1 || after.Deadline.Float64 < now()+590 {
		t.Fatalf("bad reopened attempt: %v", err)
	}
	var originalAnswers, originalGrades string
	if err = d.sql.QueryRow(`SELECT previous_answers,previous_grades FROM attempt_reopenings WHERE attempt_id=?`, aid).Scan(&originalAnswers, &originalGrades); err != nil || originalAnswers != before.Answers || originalGrades != before.Grades {
		t.Fatalf("submission evidence not preserved: %v", err)
	}
	if _, err = d.saveAnswers(token, M{"revision": float64(1), "answers": answers, "submit": true}); err == nil {
		t.Fatal("old submission replay accepted")
	}
	current, err = d.session(token)
	if err != nil || current["session"].(M)["state"] != "running" || encode(current["session"].(M)["answers"]) != encode(answers) {
		t.Fatalf("student cannot resume: %v", err)
	}
	if _, err = d.saveAnswers(token, M{"revision": float64(after.Revision + 1), "answers": answers, "submit": true}); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(eid, "close"); err != nil {
		t.Fatal(err)
	}
	if _, err = d.reopenAttempts(eid, M{"minutes": float64(10), "attemptIds": []any{aid}}); err == nil {
		t.Fatal("closed room accepted reopening")
	}
}
