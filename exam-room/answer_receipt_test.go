package main

import (
	"path/filepath"
	"testing"
)

func TestFinalizedAnswerReceiptRequiresExactSavedPayload(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	id := str(created["id"])
	if _, err = d.changeState(id, "publish"); err != nil {
		t.Fatal(err)
	}
	_, token, err := d.join(M{"code": "RECEIPT", "name": "طالب تجربة", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(id, "start"); err != nil {
		t.Fatal(err)
	}
	session, err := d.session(token)
	if err != nil {
		t.Fatal(err)
	}
	answers := M{}
	for _, question := range session["session"].(M)["questions"].([]M) {
		if question["kind"] == "mcq" {
			answers[str(question["id"])] = float64(0)
		} else {
			answers[str(question["id"])] = "إجابة وصلت"
		}
	}
	result, err := d.saveAnswers(token, M{"revision": float64(1), "submit": true, "answers": answers})
	if err != nil || result["answersReceived"] != true {
		t.Fatalf("initial receipt: %v %v", result, err)
	}
	for _, test := range []struct {
		name     string
		revision float64
		payload  M
		received bool
	}{
		{"lost acknowledgement replay", 1, answers, true},
		{"other tab different answers", 1, M{}, false},
		{"higher revision", 2, answers, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			response, err := d.saveAnswers(token, M{"revision": test.revision, "submit": true, "answers": test.payload})
			if err != nil || response["answersReceived"] != test.received {
				t.Fatalf("receipt: %v %v", response, err)
			}
			var stored string
			err = d.sql.QueryRow(`SELECT answers FROM attempts WHERE id=?`, session["session"].(M)["id"]).Scan(&stored)
			if err != nil {
				t.Fatal(err)
			}
			if stored != encode(answers) {
				t.Fatal("finalized answers changed")
			}
		})
	}
}
