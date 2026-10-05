package main

import (
	"path/filepath"
	"testing"
)

func TestSelectedExtensionKeepsOtherDeadlinesAndSharedStudentsContinue(t *testing.T) {
	for _, mode := range []string{"shared", "individual"} {
		t.Run(mode, func(t *testing.T) {
			d, err := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
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
			ids := []string{}
			tokens := []string{}
			for _, code := range []string{"ONE", "TWO", "THREE"} {
				joined, tok, err := d.join(M{"name": "طالب اختبار " + code, "phone": "01012345678", "code": code}, "")
				if err != nil {
					t.Fatal(err)
				}
				ids = append(ids, str(joined["session"].(M)["id"]))
				tokens = append(tokens, tok)
			}
			if _, err = d.changeState(eid, "start"); err != nil {
				t.Fatal(err)
			}
			before := []Attempt{}
			for _, aid := range ids {
				a, err := attempt(d.sql, aid)
				if err != nil {
					t.Fatal(err)
				}
				before = append(before, a)
			}
			if _, err = d.extendTime(eid, M{"minutes": float64(2), "attemptIds": []any{ids[0], ids[1]}}); err != nil {
				t.Fatal(err)
			}
			for i, aid := range ids {
				a, err := attempt(d.sql, aid)
				if err != nil {
					t.Fatal(err)
				}
				delta := float64(0)
				if i < 2 {
					delta = 120
				}
				if a.Deadline.Float64-before[i].Deadline.Float64 != delta {
					t.Fatal("wrong target deadline")
				}
			}
			x, err := exam(d.sql, eid)
			if err != nil {
				t.Fatal(err)
			}
			if num(x.Config["extraSeconds"]) != 0 {
				t.Fatal("changed global duration")
			}
			if _, err = d.saveAnswers(tokens[1], M{"revision": float64(1), "submit": true, "answers": M{}}); err != nil {
				t.Fatal(err)
			}
			selected, _ := attempt(d.sql, ids[0])
			if _, err = d.extendTime(eid, M{"minutes": float64(5), "attemptIds": []any{ids[0], ids[1]}}); err == nil {
				t.Fatal("extended submitted attempt")
			}
			unchanged, _ := attempt(d.sql, ids[0])
			if unchanged.Deadline != selected.Deadline {
				t.Fatal("partial extension on rejected selection")
			}
			for _, bad := range []any{[]any{}, []any{ids[0], ids[0]}, []any{float64(1)}, "all"} {
				if _, err = d.extendTime(eid, M{"minutes": float64(2), "attemptIds": bad}); err == nil {
					t.Fatal("accepted invalid selection")
				}
			}
			if mode == "shared" {
				duration := examDuration(x)
				if _, err = d.sql.Exec(`UPDATE exams SET started_at=? WHERE id=?`, now()-duration-1, eid); err != nil {
					t.Fatal(err)
				}
				if _, err = d.sql.Exec(`UPDATE attempts SET deadline=deadline-? WHERE exam_id=?`, duration+1, eid); err != nil {
					t.Fatal(err)
				}
				if err = d.expire(); err != nil {
					t.Fatal(err)
				}
				x, _ = exam(d.sql, eid)
				selected, _ = attempt(d.sql, ids[0])
				other, _ := attempt(d.sql, ids[2])
				if x.State != "running" || selected.Submitted.Valid || !other.Submitted.Valid {
					t.Fatal("shared end ignored personal extension")
				}
				if _, _, err = d.join(M{"name": "طالب اختبار ONE", "phone": "01012345678", "code": "ONE"}, ""); err != nil {
					t.Fatal("blocked extended student return", err)
				}
				if _, _, err = d.join(M{"name": "طالب جديد", "phone": "01012345678", "code": "NEW"}, ""); err == nil {
					t.Fatal("new arrival got personal extension")
				}
				if _, err = d.sql.Exec(`UPDATE attempts SET deadline=? WHERE id=?`, now()-1, ids[0]); err != nil {
					t.Fatal(err)
				}
				if err = d.expire(); err != nil {
					t.Fatal(err)
				}
				x, _ = exam(d.sql, eid)
				selected, _ = attempt(d.sql, ids[0])
				if x.State != "closed" || !selected.Submitted.Valid {
					t.Fatal("did not end final personal deadline")
				}
			} else {
				if _, err = d.cancelAttempt(ids[2], M{"reason": "اختبار"}); err != nil {
					t.Fatal(err)
				}
				if _, err = d.extendTime(eid, M{"minutes": float64(2), "attemptIds": []any{ids[2]}}); err == nil {
					t.Fatal("extended cancelled attempt")
				}
				if _, err = d.sql.Exec(`UPDATE attempts SET deadline=? WHERE id=?`, now()-1, ids[0]); err != nil {
					t.Fatal(err)
				}
				if _, err = d.extendTime(eid, M{"minutes": float64(2), "attemptIds": []any{ids[0]}}); err == nil {
					t.Fatal("reopened elapsed deadline")
				}
			}
		})
	}
}
