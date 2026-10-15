package main

import (
	"database/sql"
	"errors"
	"math"
	"regexp"
	"strings"
)

const centerSchema = `
CREATE TABLE IF NOT EXISTS center_standalone_targets(exam_id TEXT PRIMARY KEY REFERENCES exams(id),payload TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS center_standalone_rooms(exam_id TEXT PRIMARY KEY REFERENCES exams(id),request_id TEXT NOT NULL UNIQUE);
CREATE TABLE IF NOT EXISTS center_standalone_versions(attempt_id TEXT PRIMARY KEY REFERENCES attempts(id),version INTEGER NOT NULL,fingerprint TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS center_monitor_versions(exam_id TEXT PRIMARY KEY REFERENCES exams(id),version INTEGER NOT NULL,fingerprint TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS center_contexts(context_id TEXT PRIMARY KEY,payload TEXT NOT NULL,prepared_at REAL NOT NULL);
CREATE TABLE IF NOT EXISTS center_bindings(exam_id TEXT PRIMARY KEY REFERENCES exams(id),group_id TEXT NOT NULL,session_id TEXT NOT NULL,activity_id TEXT NOT NULL,max_score REAL NOT NULL);
CREATE TABLE IF NOT EXISTS center_roster(exam_id TEXT NOT NULL REFERENCES exams(id),student_id TEXT NOT NULL,code TEXT NOT NULL,name TEXT NOT NULL,phone TEXT NOT NULL,present INTEGER NOT NULL,PRIMARY KEY(exam_id,student_id),UNIQUE(exam_id,code));
CREATE INDEX IF NOT EXISTS center_roster_phone ON center_roster(exam_id,phone);
CREATE TABLE IF NOT EXISTS center_attempts(attempt_id TEXT PRIMARY KEY REFERENCES attempts(id),exam_id TEXT NOT NULL REFERENCES exams(id),student_id TEXT NOT NULL,UNIQUE(exam_id,student_id));
CREATE TABLE IF NOT EXISTS center_results(attempt_id TEXT PRIMARY KEY REFERENCES attempts(id),version INTEGER NOT NULL,acked_version INTEGER NOT NULL DEFAULT 0,fingerprint TEXT NOT NULL,payload TEXT NOT NULL);
`

// Windows integration reserves this NIC for student traffic. The other Wi-Fi
// connection belongs to reception and must never be advertised to students.
func centerStudentAddresses(addresses []string) []string {
	for _, address := range addresses {
		if address == "10.77.0.1" {
			return []string{address}
		}
	}
	return []string{}
}

func initCenterSchema(q Q) error {
	if _, err := q.Exec(centerSchema); err != nil {
		return err
	}
	_, err := q.Exec(`INSERT OR IGNORE INTO app_settings(key,value) VALUES('center_source_id',?)`, id())
	return err
}

type centerBinding struct {
	ExamID     string  `json:"examId"`
	GroupID    string  `json:"groupId"`
	SessionID  string  `json:"sessionId"`
	ActivityID string  `json:"activityId"`
	MaxScore   float64 `json:"maxScore"`
}

func readCenterBinding(q Q, eid string) (centerBinding, error) {
	b := centerBinding{ExamID: eid}
	err := q.QueryRow(`SELECT group_id,session_id,activity_id,max_score FROM center_bindings WHERE exam_id=?`, eid).Scan(&b.GroupID, &b.SessionID, &b.ActivityID, &b.MaxScore)
	return b, err
}

var centerPhonePattern = regexp.MustCompile(`^01[0125][0-9]{8}$`)

func centerPhone(raw string) string {
	s := normalizeDigits(strings.TrimSpace(raw))
	s = strings.Map(func(r rune) rune {
		if strings.ContainsRune(" ()-\u200e\u200f", r) {
			return -1
		}
		return r
	}, s)
	s = strings.TrimPrefix(s, "+")
	if strings.HasPrefix(s, "0020") {
		s = s[4:]
	} else if strings.HasPrefix(s, "20") {
		s = s[2:]
	}
	if len(s) == 10 && strings.HasPrefix(s, "1") {
		s = "0" + s
	}
	if !centerPhonePattern.MatchString(s) {
		return ""
	}
	return s
}

type centerStudent struct {
	ID, Code, Name, Phone string
	Present               bool
}

func centerRoster(p M) ([]centerStudent, error) {
	raw, ok := p["roster"].([]any)
	if !ok || len(raw) > 10000 {
		return nil, fail("كشف الطلاب غير صالح أو أكبر من الحد المسموح", 400)
	}
	list := make([]centerStudent, 0, len(raw))
	ids, codes := map[string]bool{}, map[string]bool{}
	for _, v := range raw {
		m, ok := v.(map[string]any)
		if !ok {
			return nil, fail("بيانات طالب غير صالحة", 400)
		}
		sid, e := textField(m, "studentId", 1, 100)
		if e != nil {
			return nil, e
		}
		code, e := normalizeCode(m["code"])
		if e != nil {
			return nil, e
		}
		name, e := textField(m, "name", 2, 100)
		if e != nil {
			return nil, e
		}
		present, e := boolField(m, "present")
		if e != nil {
			return nil, e
		}
		if ids[sid] || codes[code] {
			return nil, fail("طالب أو كود مكرر في كشف الامتحان", 409)
		}
		ids[sid] = true
		codes[code] = true
		list = append(list, centerStudent{sid, code, name, centerPhone(str(m["phone"])), present})
	}
	return list, nil
}
func parseCenterBinding(p M) (centerBinding, []centerStudent, error) {
	fields := []string{"examId", "groupId", "sessionId", "activityId"}
	for _, f := range fields {
		if _, e := textField(p, f, 1, 100); e != nil {
			return centerBinding{}, nil, e
		}
	}
	max, ok := p["maxScore"].(float64)
	if !ok || math.IsNaN(max) || math.IsInf(max, 0) || max <= 0 {
		return centerBinding{}, nil, fail("الدرجة النهائية غير صالحة", 400)
	}
	roster, e := centerRoster(p)
	if e != nil {
		return centerBinding{}, nil, e
	}
	b := centerBinding{str(p["examId"]), str(p["groupId"]), str(p["sessionId"]), str(p["activityId"]), max}
	return b, roster, nil
}

func (d *DB) bindCenter(p M) (M, error) {
	b, roster, e := parseCenterBinding(p)
	if e != nil {
		return nil, e
	}
	e = d.tx(func(q *sql.Tx) error { return writeCenterBinding(q, b, roster) })
	return M{"bound": e == nil, "examId": b.ExamID}, e
}

func writeCenterBinding(q Q, b centerBinding, roster []centerStudent) error {
	x, e := exam(q, b.ExamID)
	if e != nil {
		return e
	}
	if e = validateCenterMaximum(x, b.MaxScore); e != nil {
		return e
	}
	old, err := readCenterBinding(q, b.ExamID)
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return err
	}
	var count int
	if err = q.QueryRow(`SELECT COUNT(*) FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL`, b.ExamID).Scan(&count); err != nil {
		return err
	}
	if count > 0 && old != b {
		return fail("لا يمكن تغيير ربط امتحان بدأ الطلاب الدخول إليه", 409)
	}
	if _, e = q.Exec(`INSERT INTO center_bindings VALUES(?,?,?,?,?) ON CONFLICT(exam_id) DO UPDATE SET group_id=excluded.group_id,session_id=excluded.session_id,activity_id=excluded.activity_id,max_score=excluded.max_score`, b.ExamID, b.GroupID, b.SessionID, b.ActivityID, b.MaxScore); e != nil {
		return e
	}
	if e = syncCenterRoster(q, b.ExamID, roster); e != nil {
		return e
	}
	audit(q, "center-bound", b.ExamID)
	return nil
}

func validateCenterMaximum(x Exam, maximum float64) error {
	chosen, err := selectQuestionIDs(x.Config)
	if err != nil {
		return err
	}
	actual := adminAttempt(x, Attempt{QuestionIDs: encode(chosen)})["maximum"].(float64)
	if actual != maximum {
		return fail("مجموع امتحان القاعة لا يطابق مجموع امتحان السنتر؛ حدّث الربط قبل فتحها", 409)
	}
	return nil
}

func syncCenterRoster(q Q, eid string, roster []centerStudent) error {
	if _, e := q.Exec(`DELETE FROM center_roster WHERE exam_id=? AND student_id NOT IN(SELECT student_id FROM center_attempts WHERE exam_id=?)`, eid, eid); e != nil {
		return e
	}
	if _, e := q.Exec(`UPDATE center_roster SET present=0 WHERE exam_id=?`, eid); e != nil {
		return e
	}
	for _, student := range roster {
		var oldCode string
		e := q.QueryRow(`SELECT r.code FROM center_roster r JOIN center_attempts a ON a.exam_id=r.exam_id AND a.student_id=r.student_id WHERE r.exam_id=? AND r.student_id=?`, eid, student.ID).Scan(&oldCode)
		if e != nil && !errors.Is(e, sql.ErrNoRows) {
			return e
		}
		if e == nil && oldCode != student.Code {
			return fail("كود طالب بدأ الامتحان تغيّر؛ راجع الربط قبل تحديث الكشف", 409)
		}
		if _, e = q.Exec(`INSERT INTO center_roster VALUES(?,?,?,?,?,?) ON CONFLICT(exam_id,student_id) DO UPDATE SET code=excluded.code,name=excluded.name,phone=excluded.phone,present=excluded.present`, eid, student.ID, student.Code, student.Name, student.Phone, student.Present); e != nil {
			return e
		}
	}
	return nil
}

func lookupCenterStudent(q Q, eid string, p M) (centerStudent, error) {
	phone := centerPhone(str(p["phone"]))
	if phone == "" {
		return centerStudent{}, fail("اكتب رقم الموبايل المسجل في السنتر صحيحًا", 400)
	}
	code := ""
	if str(p["code"]) != "" {
		var e error
		code, e = normalizeCode(p["code"])
		if e != nil {
			return centerStudent{}, e
		}
	}
	rows, e := q.Query(`SELECT student_id,code,name,phone,present FROM center_roster WHERE exam_id=? AND phone=? AND (?='' OR code=?)`, eid, phone, code, code)
	if e != nil {
		return centerStudent{}, e
	}
	defer rows.Close()
	found := []centerStudent{}
	for rows.Next() {
		var s centerStudent
		if e = rows.Scan(&s.ID, &s.Code, &s.Name, &s.Phone, &s.Present); e != nil {
			return s, e
		}
		found = append(found, s)
	}
	if e = rows.Err(); e != nil {
		return centerStudent{}, e
	}
	if len(found) == 0 {
		return centerStudent{}, fail("الرقم غير موجود في كشف الامتحان. راجع الاستقبال لتسجيله أو تصحيحه ثم حاول مرة أخرى", 404)
	}
	if len(found) > 1 {
		return centerStudent{}, fail("الرقم مشترك بين أكثر من طالب. اكتب كودك في السنتر لتحديد بياناتك", 409)
	}
	return found[0], nil
}

func lookupCenterStudentByCode(q Q, eid string, p M) (centerStudent, error) {
	code, err := normalizeCode(p["code"])
	if err != nil {
		return centerStudent{}, err
	}
	name, err := textField(p, "name", 2, 100)
	if err != nil {
		return centerStudent{}, err
	}
	phone, err := normalizePhone(p["phone"])
	if err != nil {
		return centerStudent{}, err
	}
	student := centerStudent{Name: name, Phone: phone}
	lookup := func(candidate string) error {
		return q.QueryRow(`SELECT student_id,code,present FROM center_roster WHERE exam_id=? AND code=?`, eid, candidate).
			Scan(&student.ID, &student.Code, &student.Present)
	}
	err = lookup(code)
	if err == sql.ErrNoRows && len(code) == 4 && regexp.MustCompile(`^[0-9]+$`).MatchString(code) {
		err = lookup("0" + code)
	}
	if err == sql.ErrNoRows {
		return centerStudent{}, fail("كود الطالب غير موجود في كشف القاعة. راجع الكود مع المشرف أو حدّث كشف الطلاب أثناء اتصال الساكند بالهوست", 404)
	}
	return student, err
}
func centerExamID(q Q, p M) (string, error) {
	eid := str(p["examId"])
	if eid != "" {
		return eid, nil
	}
	rooms, e := activeRooms(q)
	if e != nil {
		return "", e
	}
	if len(rooms) != 1 {
		return "", fail("اختر قاعة الامتحان أولًا", 409)
	}
	return str(rooms[0]["id"]), nil
}
func (d *DB) lookupCenter(p M) (M, error) {
	eid, e := centerExamID(d.sql, p)
	if e != nil {
		return nil, e
	}
	if _, e = readCenterBinding(d.sql, eid); e != nil {
		return nil, fail("القاعة لم ترتبط بكشف السنتر بعد. راجع المشرف", 409)
	}
	x, e := exam(d.sql, eid)
	if e != nil {
		return nil, e
	}
	if x.State != "waiting" && x.State != "running" {
		return nil, fail("القاعة ليست مفتوحة للدخول", 409)
	}
	s, e := lookupCenterStudent(d.sql, eid, p)
	if e != nil {
		return nil, e
	}
	return M{"examId": eid, "name": s.Name, "code": s.Code, "phone": s.Phone, "present": s.Present}, nil
}
func bindCenterAttempt(q Q, eid, aid, sid string) error {
	if sid == "" {
		return nil
	}
	var existing string
	e := q.QueryRow(`SELECT student_id FROM center_attempts WHERE attempt_id=?`, aid).Scan(&existing)
	if e == nil && existing != sid {
		return fail("المحاولة مرتبطة بطالب آخر؛ راجع المشرف", 409)
	}
	if e != nil && !errors.Is(e, sql.ErrNoRows) {
		return e
	}
	_, e = q.Exec(`INSERT INTO center_attempts VALUES(?,?,?) ON CONFLICT(attempt_id) DO NOTHING`, aid, eid, sid)
	return e
}

func (d *DB) centerState() (M, error) {
	var source string
	if e := d.sql.QueryRow(`SELECT value FROM app_settings WHERE key='center_source_id'`).Scan(&source); e != nil {
		return nil, e
	}
	exams, e := d.listExams()
	if e != nil {
		return nil, e
	}
	templates, e := d.centerTemplates()
	if e != nil {
		return nil, e
	}
	bindings := []centerBinding{}
	rows, e := d.sql.Query(`SELECT exam_id,group_id,session_id,activity_id,max_score FROM center_bindings`)
	if e != nil {
		return nil, e
	}
	for rows.Next() {
		var b centerBinding
		if e = rows.Scan(&b.ExamID, &b.GroupID, &b.SessionID, &b.ActivityID, &b.MaxScore); e != nil {
			rows.Close()
			return nil, e
		}
		bindings = append(bindings, b)
	}
	e = rows.Err()
	rows.Close()
	if e != nil {
		return nil, e
	}
	attempts, results, monitors := []M{}, []M{}, []M{}
	// Release the single SQLite connection between rooms so live answer saves do
	// not wait for every historical room. Each result/version remains atomic.
	for _, b := range bindings {
		e = d.tx(func(q *sql.Tx) error {
			summaries, err := captureCenterResults(q, b, source)
			if err != nil {
				return err
			}
			monitor, err := captureCenterMonitor(q, b, source, summaries)
			if err != nil {
				return err
			}
			attempts = append(attempts, summaries...)
			if monitor["roomState"] != "draft" {
				monitors = append(monitors, monitor)
			}
			return nil
		})
		if e != nil {
			return nil, e
		}
	}
	rs, e := d.sql.Query(`SELECT payload FROM center_results WHERE acked_version<version ORDER BY attempt_id`)
	if e != nil {
		return nil, e
	}
	defer rs.Close()
	for rs.Next() {
		var raw string
		if e = rs.Scan(&raw); e != nil {
			return nil, e
		}
		results = append(results, decode(raw))
	}
	if err := rs.Err(); err != nil {
		return nil, err
	}
	rs.Close()
	offline, err := d.centerOfflineAccess()
	if err != nil {
		return nil, err
	}
	return M{"protocol": 1, "standaloneExportProtocol": 1, "academicCatalogProtocol": 1, "sourceId": source, "exams": exams["exams"], "templates": templates, "bindings": bindings, "results": results, "attempts": attempts, "monitors": monitors, "offlineAccess": offline}, nil
}

type centerAttemptState struct {
	Attempt
	studentID, fingerprint string
	version, acked         int
}

func captureCenterResults(q Q, b centerBinding, source string) ([]M, error) {
	x, e := exam(q, b.ExamID)
	if e != nil {
		return nil, e
	}
	// Essay answers are intentionally absent. Join ownership/version once instead
	// of issuing two extra queries and decoding full answers for every student.
	rows, e := q.Query(`SELECT a.id,a.question_ids,a.grades,a.submitted_at,c.created_at,ca.student_id,COALESCE(r.version,0),COALESCE(r.acked_version,0),COALESCE(r.fingerprint,'') FROM attempts a JOIN center_attempts ca ON ca.attempt_id=a.id LEFT JOIN attempt_cancellations c ON c.attempt_id=a.id LEFT JOIN center_results r ON r.attempt_id=a.id WHERE a.exam_id=?`, b.ExamID)
	if e != nil {
		return nil, e
	}
	attempts := []centerAttemptState{}
	for rows.Next() {
		var a centerAttemptState
		if e = rows.Scan(&a.ID, &a.QuestionIDs, &a.Grades, &a.Submitted, &a.Cancelled, &a.studentID, &a.version, &a.acked, &a.fingerprint); e != nil {
			rows.Close()
			return nil, e
		}
		attempts = append(attempts, a)
	}
	e = rows.Err()
	rows.Close()
	if e != nil {
		return nil, e
	}
	summaries := []M{}
	points := map[string]float64{}
	for _, raw := range x.Config["questions"].([]any) {
		question := raw.(M)
		points[str(question["id"])] = num(question["points"])
	}
	for _, a := range attempts {
		grades := decode(a.Grades)
		score, maximum, missing := 0.0, 0.0, 0
		for _, raw := range grades {
			score += num(raw.(M)["score"])
		}
		for _, raw := range array(a.QuestionIDs) {
			questionID := str(raw)
			if point, exists := points[questionID]; exists {
				maximum += point
				if _, graded := grades[questionID]; !graded {
					missing++
				}
			}
		}
		status := "not_submitted"
		var pending any
		if a.Submitted.Valid {
			pending = missing
			status = "pending_grading"
			if missing == 0 {
				status = "awaiting_close"
				if x.State == "closed" {
					status = "final"
				}
			}
		}
		if a.Cancelled.Valid {
			status = "cancelled"
		}
		version := a.version
		if status == "final" || version > 0 {
			resultStatus := "final"
			var finalScore any = score
			if status != "final" {
				resultStatus = "revoked"
				finalScore = nil
			}
			result := M{"sourceId": source, "examId": b.ExamID, "attemptId": a.ID, "studentId": a.studentID, "groupId": b.GroupID, "sessionId": b.SessionID, "activityId": b.ActivityID, "score": finalScore, "maxScore": b.MaxScore, "status": resultStatus}
			digest := hash(encode(result))
			if digest != a.fingerprint {
				version++
				result["version"] = version
				if _, e = q.Exec(`INSERT INTO center_results(attempt_id,version,fingerprint,payload) VALUES(?,?,?,?) ON CONFLICT(attempt_id) DO UPDATE SET version=excluded.version,fingerprint=excluded.fingerprint,payload=excluded.payload`, a.ID, version, digest, encode(result)); e != nil {
					return nil, e
				}
			}
		}
		summaries = append(summaries, M{"examId": b.ExamID, "attemptId": a.ID, "studentId": a.studentID, "sessionId": b.SessionID, "activityId": b.ActivityID, "status": status, "pending": pending, "score": score, "maxScore": maximum, "resultVersion": version, "ackedVersion": a.acked})
	}
	return summaries, nil
}
func (d *DB) ackCenter(p M) (M, error) {
	aid, e := textField(p, "attemptId", 1, 100)
	if e != nil {
		return nil, e
	}
	version, e := integerField(p, "version", 1, 2147483647)
	if e != nil {
		return nil, e
	}
	result, e := d.sql.Exec(`UPDATE center_results SET acked_version=MAX(acked_version,?) WHERE attempt_id=? AND version>=?`, version, aid, version)
	if e != nil {
		return nil, e
	}
	n, e := result.RowsAffected()
	if e != nil {
		return nil, e
	}
	if n != 1 {
		return nil, fail("نسخة النتيجة غير موجودة", 409)
	}
	return M{"acknowledged": true, "attemptId": aid, "version": version}, nil
}
