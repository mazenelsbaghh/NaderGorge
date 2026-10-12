package main

import (
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"
)

func recoveryFixture(t *testing.T) (*DB, Attempt, Exam, M) {
	t.Helper()
	d, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { d.sql.Close() })
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = d.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	joined, tok, err := d.join(M{"name": "طالب استرجاع", "phone": "01012345678", "code": "RECOVERY"}, "")
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.saveAnswers(tok, M{"revision": float64(1), "answers": M{}, "submit": true}); err != nil {
		t.Fatal(err)
	}
	a, err := attempt(d.sql, str(joined["session"].(M)["id"]))
	if err != nil {
		t.Fatal(err)
	}
	x, err := exam(d.sql, eid)
	if err != nil {
		t.Fatal(err)
	}
	restored := M{}
	for _, q := range questions(x, a) {
		if q["kind"] == "mcq" {
			restored[str(q["id"])] = q["correct"]
		} else {
			restored[str(q["id"])] = "إجابة لم تصل قبل الإغلاق"
		}
	}
	p := M{"format": "massar-answer-backup-v1", "attemptId": a.ID, "examId": eid, "answers": restored, "reason": "انقطاع اتصال راجعه المشرف", "expectedVersion": recoveryVersion(a), "confirmed": true}
	return d, a, x, p
}

func TestApprovedRecoveryPreservesReceiptEvidenceAndRegradesChangedAnswers(t *testing.T) {
	d, before, x, p := recoveryFixture(t)
	accepted, err := d.recoverAnswers(before.ID, p)
	if err != nil {
		t.Fatal(err)
	}
	after, err := attempt(d.sql, before.ID)
	if err != nil {
		t.Fatal(err)
	}
	if after.Submitted != before.Submitted || after.SubmitReason != before.SubmitReason || after.Revision != before.Revision+1 {
		t.Fatal("recovery changed original receipt or lost version")
	}
	summary := adminAttempt(x, after)
	if summary["score"] != float64(6) || summary["pending"] != 1 {
		t.Fatal("incorrect recovered grading", summary)
	}
	var oldAnswers, oldGrades, reason string
	if err = d.sql.QueryRow(`SELECT previous_answers,previous_grades,reason FROM answer_recoveries WHERE id=?`, accepted["id"]).Scan(&oldAnswers, &oldGrades, &reason); err != nil {
		t.Fatal(err)
	}
	if oldAnswers != before.Answers || oldGrades != before.Grades || reason != p["reason"] {
		t.Fatal("original evidence not retained")
	}
	if _, err = d.recoverAnswers(before.ID, p); err == nil {
		t.Fatal("stale replay accepted")
	}
	p["expectedVersion"] = recoveryVersion(after)
	if _, err = d.recoverAnswers(before.ID, p); err == nil {
		t.Fatal("duplicate created another recovery")
	}
	var records int
	d.sql.QueryRow(`SELECT COUNT(*) FROM answer_recoveries`).Scan(&records)
	if records != 1 {
		t.Fatal("duplicate recovery evidence")
	}
}

func TestRecoveryRejectsUnsafeOrUnreviewedImportsWithoutChangingAttempt(t *testing.T) {
	cases := []struct {
		name  string
		alter func(*testing.T, *DB, Attempt, M)
	}{
		{"unsupported file", func(t *testing.T, d *DB, a Attempt, p M) { p["format"] = "unknown" }},
		{"unfinished attempt", func(t *testing.T, d *DB, a Attempt, p M) {
			if _, err := d.sql.Exec(`UPDATE attempts SET submitted_at=NULL WHERE id=?`, a.ID); err != nil {
				t.Fatal(err)
			}
		}},
		{"wrong attempt", func(t *testing.T, d *DB, a Attempt, p M) { p["attemptId"] = "other" }},
		{"wrong exam", func(t *testing.T, d *DB, a Attempt, p M) { p["examId"] = "other" }},
		{"no confirmation", func(t *testing.T, d *DB, a Attempt, p M) { p["confirmed"] = false }},
		{"no reason", func(t *testing.T, d *DB, a Attempt, p M) { p["reason"] = "" }},
		{"foreign question", func(t *testing.T, d *DB, a Attempt, p M) { p["answers"] = M{"foreign": float64(0)} }},
		{"stale review", func(t *testing.T, d *DB, a Attempt, p M) { p["expectedVersion"] = "old" }},
		{"cancelled", func(t *testing.T, d *DB, a Attempt, p M) {
			if _, err := d.cancelAttempt(a.ID, M{"reason": "إلغاء المحاولة للاختبار"}); err != nil {
				t.Fatal(err)
			}
		}},
		{"active grading", func(t *testing.T, d *DB, a Attempt, p M) {
			_, err := d.sql.Exec(`INSERT INTO essay_grade_jobs(id,exam_id,state,model,total,created_at,updated_at) VALUES('test',?,'running','test',1,?,?)`, a.ExamID, now(), now())
			if err != nil {
				t.Fatal(err)
			}
		}},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			d, a, _, p := recoveryFixture(t)
			tc.alter(t, d, a, p)
			before, err := attempt(d.sql, a.ID)
			if err != nil {
				t.Fatal(err)
			}
			if _, err = d.recoverAnswers(a.ID, p); err == nil {
				t.Fatal("unsafe import accepted")
			}
			after, err := attempt(d.sql, a.ID)
			if err != nil {
				t.Fatal(err)
			}
			if after.Answers != before.Answers || after.Grades != before.Grades || after.Revision != before.Revision {
				t.Fatal("failed recovery changed attempt")
			}
			var count int
			d.sql.QueryRow(`SELECT COUNT(*) FROM answer_recoveries`).Scan(&count)
			if count != 0 {
				t.Fatal("failed recovery inserted evidence")
			}
		})
	}
}

func TestStudentCannotApproveAnswerRecovery(t *testing.T) {
	d, a, _, p := recoveryFixture(t)
	app := &app{db: d, studentPort: 8765, adminPort: 8766, adminToken: "admin-secret", addresses: func() []string { return nil }}
	for _, port := range []string{"8765", "8766"} {
		request := httptest.NewRequest("POST", "http://127.0.0.1:"+port+"/api/attempts/"+a.ID+"/recover-answers", strings.NewReader(encode(p)))
		request.Header.Set("Content-Type", "application/json")
		request.Header.Set("X-Exam-Request", "1")
		response := httptest.NewRecorder()
		app.handler(port == "8766").ServeHTTP(response, request)
		if response.Code < 400 {
			t.Fatal("student approved import")
		}
	}
	after, err := attempt(d.sql, a.ID)
	if err != nil {
		t.Fatal(err)
	}
	if after.Answers != a.Answers {
		t.Fatal("unauthorized request changed answers")
	}
}
