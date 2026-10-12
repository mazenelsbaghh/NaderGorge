package main

import (
	"fmt"
	"path/filepath"
	"testing"
)

func mixedBankConfig() M {
	config := demoExam()
	bank := []any{}
	for i := 0; i < 20; i++ {
		bank = append(bank, M{"kind": "mcq", "text": fmt.Sprintf("اختياري %d", i), "points": float64(2), "options": []any{"صحيح", "خطأ"}, "correct": float64(0)})
	}
	for i := 0; i < 2; i++ {
		bank = append(bank, M{"kind": "essay", "text": fmt.Sprintf("مقالي %d", i), "points": float64(5), "modelAnswer": "إجابة نموذجية"})
	}
	config["questions"], config["mcqCount"], config["essayCount"] = bank, float64(5), float64(1)
	return config
}

func TestTypedDrawRejectsInvalidQuotasAndUnequalSampledPoints(t *testing.T) {
	cases := []struct {
		name   string
		change func(M)
		valid  bool
	}{
		{"different points between types", func(M) {}, true},
		{"mcq only", func(c M) { c["essayCount"] = float64(0) }, true},
		{"essay only", func(c M) { c["mcqCount"] = float64(0) }, true},
		{"empty exam", func(c M) { c["mcqCount"], c["essayCount"] = float64(0), float64(0) }, false},
		{"too many mcq", func(c M) { c["mcqCount"] = float64(21) }, false},
		{"too many essay", func(c M) { c["essayCount"] = float64(3) }, false},
		{"negative", func(c M) { c["essayCount"] = float64(-1) }, false},
		{"fractional", func(c M) { c["mcqCount"] = 1.5 }, false},
		{"missing mcq", func(c M) { delete(c, "mcqCount") }, false},
		{"missing essay", func(c M) { delete(c, "essayCount") }, false},
		{"null", func(c M) { c["essayCount"] = nil }, false},
		{"unequal sampled mcq", func(c M) { c["questions"].([]any)[0].(M)["points"] = float64(3) }, false},
		{"unequal sampled essay", func(c M) { c["questions"].([]any)[20].(M)["points"] = float64(7) }, false},
		{"all essays can differ", func(c M) { c["essayCount"] = float64(2); c["questions"].([]any)[20].(M)["points"] = float64(7) }, true},
		{"excluded type can differ", func(c M) { c["essayCount"] = float64(0); c["questions"].([]any)[20].(M)["points"] = float64(7) }, true},
		{"absent type zero", func(c M) { c["questions"] = c["questions"].([]any)[:20]; c["essayCount"] = float64(0) }, true},
		{"absent type requested", func(c M) { c["questions"] = c["questions"].([]any)[:20] }, false},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			config := mixedBankConfig()
			tc.change(config)
			clean, err := validateExam(config)
			if (err == nil) != tc.valid {
				t.Fatalf("valid=%v, error=%v", tc.valid, err)
			}
			if err == nil && clean["questionCount"] != int(config["mcqCount"].(float64)+config["essayCount"].(float64)) {
				t.Fatal("total not derived from quotas", clean["questionCount"])
			}
		})
	}
}

func TestTypedDrawPersistsSelectionAndGradesOnlySelectedQuestions(t *testing.T) {
	for _, counts := range [][2]int{{5, 1}, {0, 1}, {5, 0}, {20, 2}} {
		t.Run(fmt.Sprint(counts), func(t *testing.T) {
			dbPath := filepath.Join(t.TempDir(), "exams.sqlite3")
			db, err := openDB(dbPath)
			if err != nil {
				t.Fatal(err)
			}
			defer func() { db.sql.Close() }()
			config := mixedBankConfig()
			config["mcqCount"], config["essayCount"] = float64(counts[0]), float64(counts[1])
			created, err := db.createExam(config)
			if err != nil {
				t.Fatal(err)
			}
			eid := str(created["id"])
			if _, err = db.changeState(eid, "publish"); err != nil {
				t.Fatal(err)
			}
			if _, err = db.changeState(eid, "start"); err != nil {
				t.Fatal(err)
			}
			identity := M{"code": "TYPED1", "name": "طالب تجربة", "phone": "01012345678"}
			joined, tok, err := db.join(identity, "")
			if err != nil {
				t.Fatal(err)
			}
			session := joined["session"].(M)
			qs := session["questions"].([]M)
			actual := map[string]int{}
			seen := map[string]bool{}
			answers := M{}
			for _, question := range qs {
				qid := str(question["id"])
				if seen[qid] {
					t.Fatal("duplicate question")
				}
				seen[qid] = true
				actual[str(question["kind"])]++
				if question["correct"] != nil || question["modelAnswer"] != nil {
					t.Fatal("answer leaked")
				}
				if question["kind"] == "mcq" {
					answers[qid] = float64(0)
				} else {
					answers[qid] = "إجابة نموذجية"
				}
			}
			if actual["mcq"] != counts[0] || actual["essay"] != counts[1] || intv(session["questionCount"]) != len(qs) {
				t.Fatal("wrong quotas", actual)
			}
			if _, err = db.saveAnswers(tok, M{"revision": float64(1), "submit": false, "answers": answers}); err != nil {
				t.Fatal(err)
			}
			before, err := attempt(db.sql, str(session["id"]))
			if err != nil {
				t.Fatal(err)
			}
			if err = db.sql.Close(); err != nil {
				t.Fatal(err)
			}
			db, err = openDB(dbPath)
			if err != nil {
				t.Fatal(err)
			}
			_, tok, err = db.join(identity, "")
			if err != nil {
				t.Fatal(err)
			}
			after, err := attempt(db.sql, before.ID)
			if err != nil {
				t.Fatal(err)
			}
			if before.QuestionIDs != after.QuestionIDs || before.Answers != after.Answers || before.Deadline != after.Deadline {
				t.Fatal("selection or progress changed after restart")
			}
			if _, err = db.saveAnswers(tok, M{"revision": float64(2), "submit": true, "answers": answers}); err != nil {
				t.Fatal(err)
			}
			for _, question := range qs {
				if question["kind"] == "essay" {
					if _, err = db.gradeEssay(before.ID, M{"questionId": question["id"], "score": float64(5), "feedback": "صحيح"}); err != nil {
						t.Fatal(err)
					}
				}
			}
			report, err := db.report(before.ID)
			if err != nil {
				t.Fatal(err)
			}
			summary := report["attempt"].(M)
			maximum := float64(counts[0]*2 + counts[1]*5)
			if summary["score"] != maximum || summary["maximum"] != maximum || len(report["questions"].([]M)) != len(qs) {
				t.Fatal("report used full bank", summary)
			}
		})
	}
}
