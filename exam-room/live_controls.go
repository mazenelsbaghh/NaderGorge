package main

import (
	"crypto/sha256"
	"database/sql"
	"fmt"
	"sort"
)

// Choices keep their original answer IDs so saved answers and grading never change.
func choiceOrder(attemptID, questionID string, count int) []int {
	order := make([]int, count)
	for i := range order {
		order[i] = i
	}
	sort.Slice(order, func(i, j int) bool {
		a := sha256.Sum256([]byte(fmt.Sprintf("%s:%s:%d", attemptID, questionID, order[i])))
		b := sha256.Sum256([]byte(fmt.Sprintf("%s:%s:%d", attemptID, questionID, order[j])))
		return string(a[:]) < string(b[:])
	})
	return order
}
func examDuration(x Exam) float64 { return num(x.Config["minutes"])*60 + num(x.Config["extraSeconds"]) }
func (d *DB) extendTime(eid string, p M) (M, error) {
	minutes, err := integerField(p, "minutes", 1, 120)
	if err != nil {
		return nil, err
	}
	err = d.tx(func(q *sql.Tx) error {
		x, err := exam(q, eid)
		if err != nil {
			return err
		}
		if x.State != "running" {
			return fail("يمكن زيادة الوقت أثناء تشغيل الامتحان فقط", 409)
		}
		if raw, targeted := p["attemptIds"]; targeted {
			ids, ok := raw.([]any)
			if !ok || len(ids) == 0 || len(ids) > maxExamStudents {
				return fail("حدد طالبًا واحدًا على الأقل", 400)
			}
			seen := map[string]bool{}
			selected := []Attempt{}
			for _, value := range ids {
				aid, ok := value.(string)
				if !ok || aid == "" || seen[aid] {
					return fail("اختيار الطلاب غير صالح", 400)
				}
				seen[aid] = true
				a, err := attempt(q, aid)
				if err != nil {
					return err
				}
				if a.ExamID != eid || !a.Joined.Valid || a.Cancelled.Valid || a.Submitted.Valid || !a.Deadline.Valid || a.Deadline.Float64 <= now() {
					return fail("أحد الطلاب المحددين انتهى وقته أو سلّم أو أُلغيت محاولته. حدّث القائمة وحدد الطلاب من جديد", 409)
				}
				selected = append(selected, a)
			}
			for _, a := range selected {
				if _, err := q.Exec(`UPDATE attempts SET deadline=deadline+? WHERE id=?`, float64(minutes)*60, a.ID); err != nil {
					return err
				}
				audit(q, fmt.Sprintf("student:extend-time:%d-minutes", minutes), a.ID)
			}
			return nil
		}
		if x.Config["timerMode"] == "shared" && now() >= x.Started.Float64+examDuration(x) {
			return fail("انتهى وقت الامتحان بالفعل", 409)
		}
		seconds := float64(minutes) * 60
		x.Config["extraSeconds"] = num(x.Config["extraSeconds"]) + seconds
		if _, err = q.Exec(`UPDATE exams SET config=? WHERE id=?`, encode(x.Config), eid); err != nil {
			return err
		}
		if _, err = q.Exec(`UPDATE attempts SET deadline=deadline+? WHERE exam_id=? AND submitted_at IS NULL AND deadline>? AND NOT EXISTS (SELECT 1 FROM attempt_cancellations WHERE attempt_id=attempts.id)`, seconds, eid, now()); err != nil {
			return err
		}
		audit(q, "exam:extend-time", eid)
		return nil
	})
	if err != nil {
		return nil, err
	}
	_, _ = d.backup()
	return d.dashboard(eid)
}
