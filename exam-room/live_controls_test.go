package main

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"reflect"
	"testing"
)

func TestTimeExtensionUpdatesActiveAndLateStudents(t *testing.T) {
	for _, mode := range []string{"shared", "individual"} {
		t.Run(mode, func(t *testing.T) {
			d, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
			if err != nil {
				t.Fatal(err)
			}
			defer d.sql.Close()
			config := demoExam()
			config["timerMode"] = mode
			created, err := d.createExam(config)
			if err != nil {
				t.Fatal(err)
			}
			eid := str(created["id"])
			if _, err = d.changeState(eid, "publish"); err != nil {
				t.Fatal(err)
			}
			if _, err = d.extendTime(eid, M{"minutes": float64(5)}); err == nil {
				t.Fatal("extended waiting room")
			}
			_, studentToken, err := d.join(M{"name": "طالب اختبار", "phone": "01012345678", "code": "001"}, "")
			if err != nil {
				t.Fatal(err)
			}
			if _, err = d.changeState(eid, "start"); err != nil {
				t.Fatal(err)
			}
			before, err := d.session(studentToken)
			if err != nil {
				t.Fatal(err)
			}
			if _, err = d.extendTime(eid, M{"minutes": float64(2)}); err != nil {
				t.Fatal(err)
			}
			after, err := d.session(studentToken)
			if err != nil {
				t.Fatal(err)
			}
			if num(after["session"].(M)["deadline"])-num(before["session"].(M)["deadline"]) != 120 {
				t.Fatal("active deadline did not extend")
			}
			late, _, err := d.join(M{"name": "طالب متأخر", "phone": "01012345679", "code": "002"}, "")
			if err != nil {
				t.Fatal(err)
			}
			x, err := exam(d.sql, eid)
			if err != nil {
				t.Fatal(err)
			}
			lateDeadline := num(late["session"].(M)["deadline"])
			if mode == "shared" && lateDeadline != x.Started.Float64+examDuration(x) {
				t.Fatal("late shared deadline")
			}
			if mode == "individual" && lateDeadline < now()+examDuration(x)-2 {
				t.Fatal("late individual duration")
			}
			if _, err = d.saveAnswers(studentToken, M{"revision": float64(1), "submit": true, "answers": M{}}); err != nil {
				t.Fatal(err)
			}
			submitted, _ := d.session(studentToken)
			deadline := num(submitted["session"].(M)["deadline"])
			if _, err = d.extendTime(eid, M{"minutes": float64(5)}); err != nil {
				t.Fatal(err)
			}
			submitted, _ = d.session(studentToken)
			if num(submitted["session"].(M)["deadline"]) != deadline || submitted["session"].(M)["state"] != "submitted" {
				t.Fatal("reopened submitted attempt")
			}
		})
	}
}
func TestChoiceOrderStableAndAnswersKeepOriginalIDs(t *testing.T) {
	orders := map[string]bool{}
	for i := 0; i < 20; i++ {
		a := choiceOrder(fmt.Sprint(i), "q1", 4)
		b := choiceOrder(fmt.Sprint(i), "q1", 4)
		if !reflect.DeepEqual(a, b) {
			t.Fatal("unstable order")
		}
		seen := map[int]bool{}
		for _, v := range a {
			seen[v] = true
		}
		if len(seen) != 4 {
			t.Fatal("lost choice")
		}
		orders[fmt.Sprint(a)] = true
	}
	if len(orders) < 2 {
		t.Fatal("identical choice order for all students")
	}
}
func TestPartialEssayVerdictKeepsFractionAndRejectsOutOfRange(t *testing.T) {
	endpoint := geminiEndpoint
	defer func() { geminiEndpoint = endpoint }()
	for _, score := range []float64{2.5, -1, 9} {
		t.Run(fmt.Sprint(score), func(t *testing.T) {
			server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				verdict := encode(M{"correct": false, "needsReview": false, "feedback": "جزء صحيح من الإجابة", "score": score})
				json.NewEncoder(w).Encode(M{"candidates": []M{{"content": M{"parts": []M{{"text": verdict}}}}}})
			}))
			defer server.Close()
			geminiEndpoint = server.URL
			verdict, err := geminiGrade(context.Background(), "test-key", gradingTask{text: "اذكر جزأين", points: 4, partialCredit: true})
			if score == 2.5 {
				if err != nil || verdict["score"] != 2.5 {
					t.Fatalf("fraction rejected %v %v", verdict, err)
				}
			} else if err == nil {
				t.Fatal("invalid score accepted")
			}
		})
	}
}
