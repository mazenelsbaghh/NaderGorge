package main

import (
	"fmt"
	"path/filepath"
	"testing"
)

func TestQuestionBankTwentyShowsTenAndKeepsSavedSelection(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	cfg := demoExam()
	cfg["questionCount"] = float64(10)
	cfg["shuffle"] = false
	bank := []any{}
	for i := 0; i < 20; i++ {
		bank = append(bank, M{"kind": "mcq", "text": fmt.Sprintf("السؤال %d", i+1), "points": float64(2), "options": []any{"صحيح", "خطأ"}, "correct": float64(0)})
	}
	cfg["questions"] = bank
	created, err := d.createExam(cfg)
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = d.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	_, tok, err := d.join(M{"code": "BANK1", "name": "طالب اختبار", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	view, err := d.session(tok)
	if err != nil {
		t.Fatal(err)
	}
	session := view["session"].(M)
	qs := session["questions"].([]M)
	if len(qs) != 10 {
		t.Fatal("wrong displayed count")
	}
	x, err := exam(d.sql, eid)
	if err != nil {
		t.Fatal(err)
	}
	lookup := map[string]bool{}
	for _, q := range x.Config["questions"].([]any) {
		lookup[str(q.(map[string]any)["id"])] = true
	}
	seen := map[string]bool{}
	answers := M{}
	for _, q := range qs {
		qid := str(q["id"])
		if seen[qid] || !lookup[qid] {
			t.Fatal("duplicate or foreign question")
		}
		seen[qid] = true
		answers[qid] = float64(0)
	}
	if _, err = d.saveAnswers(tok, M{"revision": float64(1), "submit": false, "answers": answers}); err != nil {
		t.Fatal(err)
	}
	before, err := attempt(d.sql, str(session["id"]))
	if err != nil {
		t.Fatal(err)
	}
	_, newTok, err := d.join(M{"code": "BANK1", "name": "طالب اختبار", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	after, err := attempt(d.sql, before.ID)
	if err != nil {
		t.Fatal(err)
	}
	if after.QuestionIDs != before.QuestionIDs || after.Answers != before.Answers || after.Deadline != before.Deadline {
		t.Fatal("changed bank selection on return")
	}
	if _, err = d.saveAnswers(newTok, M{"revision": float64(2), "submit": true, "answers": answers}); err != nil {
		t.Fatal(err)
	}
	result, err := d.report(before.ID)
	if err != nil {
		t.Fatal(err)
	}
	summary := result["attempt"].(M)
	if summary["score"] != float64(20) || summary["maximum"] != float64(20) || len(result["questions"].([]M)) != 10 {
		t.Fatal("graded full bank instead of selected questions")
	}
	bank[0].(M)["points"] = float64(3)
	if _, err = d.createExam(cfg); err == nil {
		t.Fatal("unequal maximum allowed for sampled bank")
	}
}
