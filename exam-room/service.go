package main

import (
	"crypto/rand"
	"database/sql"
	"errors"
	"math"
	"math/big"
	"strings"
)

func (d *DB) listExams() (M, error) {
	rows, e := d.sql.Query(`SELECT exams.id,config,state,created_at,started_at,template_id,lesson_id,(SELECT COUNT(*) FROM attempts WHERE exam_id=exams.id AND joined_at IS NOT NULL),(SELECT COUNT(*) FROM attempts WHERE exam_id=exams.id AND submitted_at IS NOT NULL) FROM exams ORDER BY created_at DESC`)
	if e != nil {
		return nil, e
	}
	defer rows.Close()
	all := []M{}
	for rows.Next() {
		var id, config, state string
		var created float64
		var started sql.NullFloat64
		var templateID, lessonID sql.NullString
		var count, submitted int
		if e = rows.Scan(&id, &config, &state, &created, &started, &templateID, &lessonID, &count, &submitted); e != nil {
			return nil, e
		}
		all = append(all, M{"id": id, "state": state, "createdAt": created, "startedAt": nullableFloat(started), "templateId": nullableString(templateID), "lessonId": nullableString(lessonID), "students": count, "submitted": submitted, "title": decode(config)["title"]})
	}
	return M{"exams": all}, rows.Err()
}
func (d *DB) updateExam(eid string, p M) (M, error) {
	config, e := validateExam(p)
	if e != nil {
		return nil, e
	}
	e = d.tx(func(q *sql.Tx) error {
		x, e := exam(q, eid)
		if e != nil {
			return e
		}
		if x.State != "draft" {
			return fail("الأسئلة مقفولة بعد فتح قاعة الانتظار. أنشئ نسخة جديدة للتعديل", 409)
		}
		_, e = q.Exec(`UPDATE exams SET config=? WHERE id=?`, encode(config), eid)
		audit(q, "exam-updated", eid)
		return e
	})
	return M{"id": eid}, e
}
func (d *DB) duplicateExam(eid string) (M, error) {
	x, e := exam(d.sql, eid)
	if e != nil {
		return nil, e
	}
	title := []rune(str(x.Config["title"]))
	if len(title) > 135 {
		title = title[:135]
	}
	x.Config["title"] = string(title) + " · نسخة جديدة"
	return d.createExam(x.Config)
}
func (d *DB) changeState(eid, action string) (M, error) {
	e := d.tx(func(q *sql.Tx) error {
		x, e := exam(q, eid)
		if e != nil {
			return e
		}
		switch action {
		case "publish", "start":
			expected, target := "draft", "waiting"
			if action == "start" {
				expected, target = "waiting", "running"
			}
			if action == "publish" {
				if e = allowAnotherRoom(q); e != nil {
					return e
				}
			}
			if x.State != expected {
				return fail("حالة الامتحان تغيرت. حدّث الصفحة", 409)
			}
			if _, e = q.Exec(`UPDATE exams SET state=? WHERE id=?`, target, eid); e != nil {
				return fail("يوجد امتحان آخر مفتوح. أنهِه أولًا", 409)
			}
			if target == "running" {
				t := now()
				_, e = q.Exec(`UPDATE exams SET started_at=? WHERE id=?`, t, eid)
				if e != nil {
					return e
				}
				if _, e = q.Exec(`INSERT OR IGNORE INTO attempt_presence(attempt_id,last_visible) SELECT id,? FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL AND submitted_at IS NULL`, t, eid); e != nil {
					return e
				}
				if _, e = q.Exec(`UPDATE attempt_presence SET last_visible=?,hidden_since=NULL WHERE attempt_id IN (SELECT id FROM attempts WHERE exam_id=?)`, t, eid); e != nil {
					return e
				}
				_, e = q.Exec(`UPDATE attempts SET deadline=? WHERE exam_id=? AND joined_at IS NOT NULL`, t+examDuration(x), eid)
				if e != nil {
					return e
				}
			}
		case "close":
			if x.State != "waiting" && x.State != "running" {
				return fail("الامتحان غير مفتوح", 409)
			}
			if e = closeExam(q, x, "closed"); e != nil {
				return e
			}
		default:
			return fail("إجراء غير معروف", 404)
		}
		audit(q, "exam:"+action, eid)
		return nil
	})
	if e != nil {
		return nil, e
	}
	_, _ = d.backup()
	return M{"id": eid}, nil
}
func closeExam(q Q, x Exam, reason string) error {
	rows, e := q.Query(attemptSQL+` WHERE exam_id=? AND joined_at IS NOT NULL AND submitted_at IS NULL`, x.ID)
	if e != nil {
		return e
	}
	as := []Attempt{}
	for rows.Next() {
		a, err := scanAttempt(rows)
		if err != nil {
			rows.Close()
			return err
		}
		as = append(as, a)
	}
	rows.Close()
	for _, a := range as {
		if e = finalize(q, a, x, reason); e != nil {
			return e
		}
	}
	_, e = q.Exec(`UPDATE exams SET state='closed',ended_at=? WHERE id=?`, now(), x.ID)
	return e
}
func (d *DB) expire() error {
	return d.tx(func(q *sql.Tx) error {
		rows, e := q.Query(`SELECT id FROM exams WHERE state='running'`)
		if e != nil {
			return e
		}
		ids := []string{}
		for rows.Next() {
			var s string
			if e = rows.Scan(&s); e != nil {
				rows.Close()
				return e
			}
			ids = append(ids, s)
		}
		rows.Close()
		for _, eid := range ids {
			x, e := exam(q, eid)
			if e != nil {
				return e
			}
			if e = pauseAbsent(q, x, now()); e != nil {
				return e
			}
			if x.Config["timerMode"] == "shared" && x.Started.Valid && now() >= x.Started.Float64+examDuration(x) {
				var extended int
				if e = q.QueryRow(`SELECT COUNT(*) FROM attempts WHERE exam_id=? AND submitted_at IS NULL AND deadline>? AND NOT EXISTS(SELECT 1 FROM attempt_cancellations WHERE attempt_id=attempts.id)`, eid, now()).Scan(&extended); e != nil {
					return e
				}
				if extended == 0 {
					if e = closeExam(q, x, "timeout"); e != nil {
						return e
					}
					audit(q, "exam:timeout", eid)
					continue
				}
			}
			rs, e := q.Query(attemptSQL+` WHERE exam_id=? AND submitted_at IS NULL AND deadline<=?`, eid, now())
			if e != nil {
				return e
			}
			as := []Attempt{}
			for rs.Next() {
				a, err := scanAttempt(rs)
				if err != nil {
					rs.Close()
					return err
				}
				as = append(as, a)
			}
			rs.Close()
			for _, a := range as {
				if e = finalize(q, a, x, "timeout"); e != nil {
					return e
				}
			}
		}
		return nil
	})
}
func (d *DB) lounge() (M, error) {
	rooms, err := activeRooms(d.sql)
	if err != nil {
		return nil, err
	}
	var single any
	if len(rooms) == 1 {
		single = rooms[0]
	}
	return M{"exam": single, "rooms": rooms, "serverTime": now()}, nil
}
func (d *DB) auth(q Q, tok string) (Attempt, Exam, error) {
	if tok == "" {
		return Attempt{}, Exam{}, fail("سجل الدخول أولًا", 401)
	}
	a, e := scanAttempt(q.QueryRow(attemptSQL+` WHERE token_hash=?`, hash(tok)))
	if errors.Is(e, sql.ErrNoRows) {
		return a, Exam{}, fail("انتهت جلسة الدخول. استخدم الكود أو راجع المشرف", 401)
	}
	if e != nil {
		return a, Exam{}, e
	}
	x, e := exam(q, a.ExamID)
	if e != nil {
		return a, x, e
	}
	if !a.Cancelled.Valid && !a.Submitted.Valid && a.Deadline.Valid && now() >= a.Deadline.Float64 {
		if e = finalize(q, a, x, "timeout"); e != nil {
			return a, x, e
		}
		a, e = attempt(q, a.ID)
	}
	if e == nil {
		e = pauseIfAbsent(q, a, x, now())
	}
	if e == nil {
		e = attachPresence(q, &a)
	}
	return a, x, e
}
func (d *DB) session(tok string) (M, error) {
	if tok == "" {
		return M{"session": nil}, nil
	}
	var out M
	e := d.tx(func(q *sql.Tx) error {
		a, x, e := d.auth(q, tok)
		if e != nil {
			return e
		}
		if !a.LastSeen.Valid || now()-a.LastSeen.Float64 >= 5 {
			_, _ = q.Exec(`UPDATE attempts SET last_seen=? WHERE id=?`, now(), a.ID)
		}
		out = M{"session": studentView(x, a)}
		return nil
	})
	return out, e
}
func (d *DB) join(p M, current string) (M, string, error) {
	code, e := normalizeCode(p["code"])
	if e != nil {
		return nil, "", e
	}
	name, e := textField(p, "name", 2, 100)
	if e != nil {
		return nil, "", e
	}
	phone, e := normalizePhone(p["phone"])
	if e != nil {
		return nil, "", e
	}
	var out M
	newToken := ""
	e = d.tx(func(q *sql.Tx) error {
		rooms, e := activeRooms(q)
		if e != nil {
			return e
		}
		if len(rooms) == 0 {
			return fail("لا يوجد امتحان مفتوح للدخول الآن", 409)
		}
		eid := str(p["examId"])
		if eid == "" {
			if len(rooms) > 1 {
				return fail("اختر القاعة أولًا؛ يوجد أكثر من قاعة مفتوحة", 409)
			}
			eid = str(rooms[0]["id"])
		}
		found := false
		for _, room := range rooms {
			if room["id"] == eid {
				found = true
			}
		}
		if !found {
			return fail("القاعة المختارة لم تعد مفتوحة. اختر القاعة من جديد", 409)
		}
		if current != "" {
			var original string
			err := q.QueryRow(`SELECT a.exam_id FROM attempts a JOIN exams e ON e.id=a.exam_id WHERE a.token_hash=? AND e.state IN ('waiting','running') AND a.submitted_at IS NULL AND NOT EXISTS(SELECT 1 FROM attempt_cancellations WHERE attempt_id=a.id)`, hash(current)).Scan(&original)
			if err != nil && !errors.Is(err, sql.ErrNoRows) {
				return err
			}
			if err == nil && original != eid {
				return fail("أنت داخل قاعة أخرى بالفعل. استكمل محاولتك الحالية", 409)
			}
		}
		var existingRoom string
		err := q.QueryRow(`SELECT a.exam_id FROM attempts a JOIN exams e ON e.id=a.exam_id WHERE a.code=? AND a.exam_id<>? AND a.joined_at IS NOT NULL AND a.submitted_at IS NULL AND e.state IN ('waiting','running') AND NOT EXISTS(SELECT 1 FROM attempt_cancellations WHERE attempt_id=a.id)`, code, eid).Scan(&existingRoom)
		if err != nil && !errors.Is(err, sql.ErrNoRows) {
			return err
		}
		if err == nil {
			return fail("الكود لديه محاولة في قاعة أخرى. اختر قاعته الأصلية للاستكمال", 409)
		}
		x, e := exam(q, eid)
		if e != nil {
			return e
		}
		a, e := scanAttempt(q.QueryRow(attemptSQL+` WHERE exam_id=? AND code=?`, eid, code))
		if errors.Is(e, sql.ErrNoRows) {
			var count int
			if e = q.QueryRow(`SELECT COUNT(*) FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL`, eid).Scan(&count); e != nil {
				return e
			}
			if count >= maxExamStudents {
				return fail("اكتمل عدد الطلاب لهذه الجلسة", 409)
			}
			aid := id()
			if _, e = q.Exec(`INSERT INTO attempts(id,exam_id,code) VALUES(?,?,?)`, aid, eid, code); e != nil {
				return e
			}
			a, e = attempt(q, aid)
		}
		if e != nil {
			return e
		}
		if x.TemplateID.Valid {
			var previousID string
			var previousSubmitted sql.NullFloat64
			e = q.QueryRow(`SELECT a.id,a.submitted_at
				FROM attempts a JOIN exams previous ON previous.id=a.exam_id
				WHERE previous.template_id=? AND previous.started_at IS NOT NULL
				  AND a.joined_at IS NOT NULL
				  AND a.code=? AND a.id<>?
				ORDER BY a.joined_at LIMIT 1`, x.TemplateID.String, code, a.ID).Scan(&previousID, &previousSubmitted)
			if e != nil && !errors.Is(e, sql.ErrNoRows) {
				return e
			}
			if e == nil {
				if previousSubmitted.Valid {
					return fail("هذا الطالب أدّى الامتحان من قبل، ولا يمكن إعادته", 409)
				}
				return fail("للطالب محاولة مفتوحة لهذا الامتحان. استخدم جلسته الأصلية أو راجع المشرف", 409)
			}
		}
		if current != "" {
			var claimed string
			_ = q.QueryRow(`SELECT id FROM attempts WHERE exam_id=? AND token_hash=?`, eid, hash(current)).Scan(&claimed)
			if claimed != "" && claimed != a.ID {
				return fail("هذا المتصفح مسجل بكود آخر في الامتحان. راجع المشرف", 409)
			}
		}
		if e = attachPresence(q, &a); e != nil {
			return e
		}
		if a.Cancelled.Valid {
			return fail("محاولتك ملغاة. راجع المشرف", 409)
		}
		if a.TokenHash.Valid {
			if current != "" && a.TokenHash.String == hash(current) {
				out = M{"session": studentView(x, a)}
				newToken = current
				return nil
			}
			if a.Submitted.Valid {
				return fail("أديت الامتحان ده قبل كده، ولا يمكن تبدأ محاولة جديدة", 409)
			}
			if phone != a.Phone.String {
				return fail("لاستكمال المحاولة، استخدم رقم الموبايل المسجل مع نفس الكود", 400)
			}
		}
		if a.Submitted.Valid {
			return fail("أديت الامتحان ده قبل كده، ولا يمكن تبدأ محاولة جديدة", 409)
		}
		if x.State == "running" {
			if x.Config["allowLate"] == false && !a.Joined.Valid {
				return fail("بدأ الامتحان والدخول المتأخر غير متاح", 409)
			}
			if !a.Joined.Valid && x.Config["timerMode"] == "shared" && now() >= x.Started.Float64+examDuration(x) {
				return fail("انتهى وقت الامتحان", 409)
			}
		}
		newToken = token()
		if a.Joined.Valid {
			if phone != a.Phone.String {
				return fail("للاستعادة، استخدم نفس رقم الموبايل المسجل", 400)
			}
			_, e = q.Exec(`UPDATE attempts SET token_hash=?,last_seen=? WHERE id=?`, hash(newToken), now(), a.ID)
		} else {
			qs := append([]any{}, x.Config["questions"].([]any)...)
			if x.Config["shuffle"] == true || intv(x.Config["questionCount"]) < len(qs) {
				for i := len(qs) - 1; i > 0; i-- {
					choice, err := rand.Int(rand.Reader, big.NewInt(int64(i+1)))
					if err != nil {
						return err
					}
					j := int(choice.Int64())
					qs[i], qs[j] = qs[j], qs[i]
				}
			}
			selected := []string{}
			for _, qv := range qs[:intv(x.Config["questionCount"])] {
				selected = append(selected, str(qv.(map[string]any)["id"]))
			}
			var deadline any
			if x.State == "running" {
				start := now()
				if x.Config["timerMode"] == "shared" {
					start = x.Started.Float64
				}
				deadline = start + examDuration(x)
			}
			t := now()
			_, e = q.Exec(`UPDATE attempts SET name=?,phone=?,token_hash=?,joined_at=?,last_seen=?,deadline=?,question_ids=? WHERE id=?`, name, phone, hash(newToken), t, t, deadline, encode(selected), a.ID)
		}
		if e != nil {
			return e
		}
		audit(q, "student-joined", a.ID)
		a, e = attempt(q, a.ID)
		if e != nil {
			return e
		}
		out = M{"session": studentView(x, a)}
		return nil
	})
	return out, newToken, e
}
func (d *DB) saveAnswers(tok string, p M) (M, error) {
	rev, e := integerField(p, "revision", 1, 2147483647)
	if e != nil {
		return nil, e
	}
	submit, e := boolField(p, "submit")
	if e != nil {
		return nil, e
	}
	var out M
	e = d.tx(func(q *sql.Tx) error {
		a, x, e := d.auth(q, tok)
		if e != nil {
			return e
		}
		if a.Cancelled.Valid {
			return fail("محاولتك ملغاة. راجع المشرف", 423)
		}
		if a.Submitted.Valid {
			out = M{"session": studentView(x, a)}
			return nil
		}
		if x.State != "running" {
			return fail("الامتحان لم يبدأ", 409)
		}
		if a.Paused {
			return fail("الامتحان موقوف لهذا الطالب. انتظر استكماله من المشرف", 423)
		}
		answers, e := validAnswers(p["answers"], questions(x, a))
		if e != nil {
			return e
		}
		if rev == a.Revision && encode(answers) == encode(decode(a.Answers)) {
		} else if rev != a.Revision+1 {
			return fail("الإجابات تغيرت في نافذة أخرى. أعد تحميل الصفحة قبل المتابعة", 409)
		} else {
			_, e = q.Exec(`UPDATE attempts SET answers=?,revision=?,last_seen=? WHERE id=?`, encode(answers), rev, now(), a.ID)
			if e != nil {
				return e
			}
			a, e = attempt(q, a.ID)
			if e != nil {
				return e
			}
		}
		if submit {
			if e = finalize(q, a, x, "student"); e != nil {
				return e
			}
			a, e = attempt(q, a.ID)
			if e != nil {
				return e
			}
		}
		out = M{"session": studentView(x, a)}
		return nil
	})
	return out, e
}
func (d *DB) resetLogin(aid string) (M, error) {
	e := d.tx(func(q *sql.Tx) error {
		a, e := attempt(q, aid)
		if e != nil {
			return e
		}
		if a.Cancelled.Valid {
			return fail("المحاولة ملغاة ولا يمكن إعادة فتحها", 409)
		}
		if a.Submitted.Valid {
			return fail("المحاولة مسلّمة ولا يمكن إعادة فتحها", 409)
		}
		_, e = q.Exec(`UPDATE attempts SET token_hash=NULL WHERE id=?`, aid)
		audit(q, "login-reset", aid)
		return e
	})
	return M{"id": aid}, e
}
func (d *DB) gradeEssay(aid string, p M) (M, error) {
	e := d.tx(func(q *sql.Tx) error {
		a, e := attempt(q, aid)
		if e != nil {
			return e
		}
		x, e := exam(q, a.ExamID)
		if e != nil {
			return e
		}
		if a.Cancelled.Valid {
			return fail("المحاولة ملغاة ولا يمكن تصحيحها", 409)
		}
		if !a.Submitted.Valid {
			return fail("انتظر تسليم الطالب", 409)
		}
		var selected M
		for _, question := range questions(x, a) {
			if question["id"] == p["questionId"] && question["kind"] == "essay" {
				selected = question
			}
		}
		if selected == nil {
			return fail("السؤال المقالي غير موجود", 404)
		}
		score, ok := p["score"].(float64)
		if !ok || math.IsNaN(score) || math.IsInf(score, 0) || score < 0 || score > num(selected["points"]) {
			return fail("الدرجة يجب أن تقع بين صفر ودرجة السؤال", 400)
		}
		feedback, e := textField(p, "feedback", 0, 2000)
		if e != nil {
			return e
		}
		grades := decode(a.Grades)
		grades[str(selected["id"])] = M{"score": score, "feedback": feedback}
		_, e = q.Exec(`UPDATE attempts SET grades=? WHERE id=?`, encode(grades), aid)
		audit(q, "essay-graded", aid)
		return e
	})
	return M{"id": aid}, e
}
func (d *DB) report(aid string) (M, error) {
	a, e := attempt(d.sql, aid)
	if e != nil {
		return nil, e
	}
	x, e := exam(d.sql, a.ExamID)
	if e != nil {
		return nil, e
	}
	if a.Cancelled.Valid {
		return nil, fail("المحاولة ملغاة؛ سبب الإلغاء موجود في شيت النتائج", 409)
	}
	summary := adminAttempt(x, a)
	if !a.Submitted.Valid || summary["pending"] != 0 {
		return nil, fail("أكمل تصحيح المحاولة قبل تصدير التقرير", 409)
	}
	return M{"exam": examMap(x), "attempt": summary, "questions": questions(x, a)}, nil
}
func (d *DB) count(q string) int { var n int; _ = d.sql.QueryRow(q).Scan(&n); return n }
func sqlErr(e error) error {
	if e == nil {
		return nil
	}
	if strings.Contains(e.Error(), "database is locked") {
		return fail("قاعدة البيانات مشغولة؛ حاول مرة أخرى", 503)
	}
	return e
}
