package main

import (
	"database/sql"
	"strings"
)

const answerRecoverySchema = `CREATE TABLE IF NOT EXISTS answer_recoveries (
 id TEXT PRIMARY KEY, attempt_id TEXT NOT NULL REFERENCES attempts(id), created_at REAL NOT NULL,
 reason TEXT NOT NULL, previous_answers TEXT NOT NULL, previous_grades TEXT NOT NULL,
 recovered_answers TEXT NOT NULL, previous_revision INTEGER NOT NULL)`

func recoveryVersion(a Attempt) string { return hash(a.Answers + "\n" + a.Grades) }

func (d *DB) recoverAnswers(aid string, p M) (M, error) {
	if p["format"] != "massar-answer-backup-v1" {
		return nil, fail("صيغة نسخة الاسترجاع غير مدعومة", 400)
	}
	reason, err := textField(p, "reason", 5, 1000)
	if err != nil {
		return nil, fail("اكتب سبب اعتماد الإجابات المسترجعة (٥ أحرف على الأقل)", 400)
	}
	if p["confirmed"] != true {
		return nil, fail("راجع الاختلافات وأكد اعتماد الاسترجاع", 400)
	}
	var recoveryID string
	err = d.tx(func(q *sql.Tx) error {
		a, e := attempt(q, aid)
		if e != nil {
			return e
		}
		if !a.Submitted.Valid || a.Cancelled.Valid {
			return fail("الاسترجاع متاح للمحاولة المسلّمة غير الملغاة فقط", 409)
		}
		if str(p["examId"]) != a.ExamID || str(p["attemptId"]) != a.ID {
			return fail("النسخة لا تخص محاولة الطالب والامتحان المحددين", 400)
		}
		if str(p["expectedVersion"]) != recoveryVersion(a) {
			return fail("تغيرت ورقة الطالب؛ حدّثها وراجع الفروق مرة أخرى", 409)
		}
		var activeJobs int
		if e = q.QueryRow(`SELECT COUNT(*) FROM essay_grade_jobs WHERE exam_id=? AND state='running'`, a.ExamID).Scan(&activeJobs); e != nil {
			return e
		}
		if activeJobs > 0 {
			return fail("انتظر انتهاء التصحيح الجماعي قبل استرجاع الإجابات", 409)
		}
		x, e := exam(q, a.ExamID)
		if e != nil {
			return e
		}
		qs := questions(x, a)
		imported, e := validAnswers(p["answers"], qs)
		if e != nil {
			return e
		}
		merged := decode(a.Answers)
		for key, answer := range imported {
			merged[key] = answer
		}
		if encode(merged) == encode(decode(a.Answers)) {
			return fail("لا توجد إجابات مختلفة لاعتمادها", 409)
		}
		grades := recoveredGrades(qs, decode(a.Answers), merged, decode(a.Grades))
		recoveryID = id()
		if _, e = q.Exec(`INSERT INTO answer_recoveries(id,attempt_id,created_at,reason,previous_answers,previous_grades,recovered_answers,previous_revision) VALUES(?,?,?,?,?,?,?,?)`, recoveryID, a.ID, now(), reason, a.Answers, a.Grades, encode(merged), a.Revision); e != nil {
			return e
		}
		if _, e = q.Exec(`UPDATE attempts SET answers=?,grades=?,revision=revision+1 WHERE id=?`, encode(merged), encode(grades), a.ID); e != nil {
			return e
		}
		audit(q, "answers-recovered-by-admin", recoveryID)
		return nil
	})
	return M{"id": recoveryID}, err
}

func recoveredGrades(qs []M, previous, recovered, grades M) M {
	for _, question := range qs {
		qid := str(question["id"])
		if previous[qid] == recovered[qid] {
			continue
		}
		delete(grades, qid)
		if question["kind"] == "mcq" {
			score := 0
			if recovered[qid] == question["correct"] {
				score = intv(question["points"])
			}
			grades[qid] = M{"score": score, "feedback": "تصحيح تلقائي بعد استرجاع معتمد"}
		} else if strings.TrimSpace(str(recovered[qid])) == "" {
			grades[qid] = M{"score": 0, "feedback": "لم تُكتب إجابة"}
		}
	}
	return grades
}
