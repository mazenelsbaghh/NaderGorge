package main

import (
	"database/sql"
	"fmt"
)

const reopenAttemptSchema = `CREATE TABLE IF NOT EXISTS attempt_reopenings (
 id TEXT PRIMARY KEY, attempt_id TEXT NOT NULL REFERENCES attempts(id), created_at REAL NOT NULL,
 previous_submitted_at REAL NOT NULL, previous_submit_reason TEXT, previous_deadline REAL,
 previous_revision INTEGER NOT NULL, previous_answers TEXT NOT NULL, previous_grades TEXT NOT NULL,
 new_deadline REAL NOT NULL)`

// Reopening is a supervised correction to an existing attempt, never a new attempt.
func (d *DB) reopenAttempts(eid string, p M) (M, error) {
	minutes, err := integerField(p, "minutes", 1, 120)
	if err != nil {
		return nil, err
	}
	ids, ok := p["attemptIds"].([]any)
	if !ok || len(ids) == 0 || len(ids) > maxExamStudents {
		return nil, fail("حدد طالبًا واحدًا على الأقل", 400)
	}
	err = d.tx(func(q *sql.Tx) error {
		x, err := exam(q, eid)
		if err != nil {
			return err
		}
		if x.State != "running" {
			return fail("إعادة فتح المحاولات متاحة أثناء تشغيل القاعة فقط", 409)
		}
		var jobs int
		if err = q.QueryRow(`SELECT COUNT(*) FROM essay_grade_jobs WHERE exam_id=? AND state='running'`, eid).Scan(&jobs); err != nil {
			return err
		}
		if jobs > 0 {
			return fail("انتظر انتهاء التصحيح الجماعي قبل إعادة فتح المحاولات", 409)
		}
		seen := map[string]bool{}
		selected := make([]Attempt, 0, len(ids))
		for _, raw := range ids {
			aid, ok := raw.(string)
			if !ok || aid == "" || seen[aid] {
				return fail("اختيار الطلاب غير صالح", 400)
			}
			seen[aid] = true
			a, err := attempt(q, aid)
			if err != nil {
				return err
			}
			if a.ExamID != eid || !a.Joined.Valid || !a.Submitted.Valid || a.Cancelled.Valid {
				return fail("أحد الطلاب غير مسلّم أو لا ينتمي لهذه القاعة أو محاولته ملغاة. حدّث القائمة", 409)
			}
			selected = append(selected, a)
		}
		t := now()
		deadline := t + float64(minutes)*60
		for _, a := range selected {
			rid := id()
			if _, err = q.Exec(`INSERT INTO attempt_reopenings(id,attempt_id,created_at,previous_submitted_at,previous_submit_reason,previous_deadline,previous_revision,previous_answers,previous_grades,new_deadline) VALUES(?,?,?,?,?,?,?,?,?,?)`, rid, a.ID, t, a.Submitted.Float64, nullableString(a.SubmitReason), nullableFloat(a.Deadline), a.Revision, a.Answers, a.Grades, deadline); err != nil {
				return err
			}
			if _, err = q.Exec(`UPDATE attempts SET submitted_at=NULL,submit_reason=NULL,deadline=?,revision=revision+1,grades='{}' WHERE id=?`, deadline, a.ID); err != nil {
				return err
			}
			if _, err = q.Exec(`INSERT INTO attempt_presence(attempt_id,last_visible,paused) VALUES(?,?,0) ON CONFLICT(attempt_id) DO UPDATE SET paused=0,last_visible=excluded.last_visible,hidden_since=NULL`, a.ID, t); err != nil {
				return err
			}
			if _, err = q.Exec(`UPDATE attempt_screen_guard SET issue='',issue_since=NULL WHERE attempt_id=?`, a.ID); err != nil {
				return err
			}
			if _, err = q.Exec(`INSERT INTO audit(created_at,action,subject_id) VALUES(?,?,?)`, t, fmt.Sprintf("attempt:reopened:%d-minutes", minutes), rid); err != nil {
				return err
			}
		}
		return nil
	})
	if err != nil {
		return nil, err
	}
	return d.dashboard(eid)
}
