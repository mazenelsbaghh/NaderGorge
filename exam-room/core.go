package main

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"database/sql"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"math"
	"os"
	"path/filepath"
	"regexp"
	"strings"
	"time"
	"unicode"

	_ "modernc.org/sqlite"
)

type M = map[string]any
type roomError struct {
	msg    string
	status int
}

func (e roomError) Error() string     { return e.msg }
func fail(s string, status int) error { return roomError{s, status} }
func now() float64                    { return float64(time.Now().UnixNano()) / 1e9 }
func id() string {
	b := make([]byte, 16)
	if _, err := rand.Read(b); err != nil {
		panic(err)
	}
	return hex.EncodeToString(b)
}
func token() string {
	b := make([]byte, 32)
	if _, err := rand.Read(b); err != nil {
		panic(err)
	}
	return hex.EncodeToString(b)
}
func hash(s string) string { h := sha256.Sum256([]byte(s)); return hex.EncodeToString(h[:]) }
func encode(v any) string  { b, _ := json.Marshal(v); return string(b) }
func decode(s string) M {
	var m M
	_ = json.Unmarshal([]byte(s), &m)
	if m == nil {
		return M{}
	}
	return m
}
func array(s string) []any { var a []any; _ = json.Unmarshal([]byte(s), &a); return a }
func str(v any) string     { s, _ := v.(string); return s }
func num(v any) float64    { n, _ := v.(float64); return n }
func intv(v any) int       { return int(num(v)) }
func textField(m M, k string, min, max int) (string, error) {
	s, ok := m[k].(string)
	s = strings.TrimSpace(s)
	if !ok || len([]rune(s)) < min || len([]rune(s)) > max {
		return "", fail("تحقق من الحقل: "+k, 400)
	}
	return s, nil
}
func integerField(m M, k string, min, max int) (int, error) {
	n, ok := m[k].(float64)
	if !ok || n != math.Trunc(n) || n < float64(min) || n > float64(max) {
		return 0, fail("قيمة غير صالحة: "+k, 400)
	}
	return int(n), nil
}
func boolField(m M, k string) (bool, error) {
	b, ok := m[k].(bool)
	if !ok {
		return false, fail("اختيار غير صالح: "+k, 400)
	}
	return b, nil
}
func validateExam(p M) (M, error) {
	raw, ok := p["questions"].([]any)
	if !ok || len(raw) < 1 || len(raw) > 100 {
		return nil, fail("الامتحان يحتاج من سؤال واحد إلى ١٠٠ سؤال", 400)
	}
	qs := []M{}
	points := map[int]bool{}
	for _, x := range raw {
		q, ok := x.(map[string]any)
		if !ok {
			return nil, fail("صيغة السؤال غير صالحة", 400)
		}
		kind := str(q["kind"])
		if kind != "mcq" && kind != "essay" {
			return nil, fail("نوع السؤال غير صالح", 400)
		}
		txt, e := textField(q, "text", 1, 5000)
		if e != nil {
			return nil, e
		}
		pt, e := integerField(q, "points", 1, 100)
		if e != nil {
			return nil, e
		}
		clean := M{"id": id(), "kind": kind, "text": txt, "points": pt}
		points[pt] = true
		if kind == "essay" {
			a, e := textField(q, "modelAnswer", 1, 10000)
			if e != nil {
				return nil, e
			}
			clean["modelAnswer"] = a
		} else {
			opts, ok := q["options"].([]any)
			if !ok || len(opts) < 2 || len(opts) > 6 {
				return nil, fail("أضف من اختيارين إلى ستة اختيارات", 400)
			}
			cleanOpts := []string{}
			for _, o := range opts {
				v, e := textField(M{"option": o}, "option", 1, 1000)
				if e != nil {
					return nil, e
				}
				cleanOpts = append(cleanOpts, v)
			}
			correct, e := integerField(q, "correct", 0, len(opts)-1)
			if e != nil {
				return nil, e
			}
			clean["options"] = cleanOpts
			clean["correct"] = correct
		}
		qs = append(qs, clean)
	}
	count, e := integerField(p, "questionCount", 1, len(qs))
	if e != nil {
		return nil, e
	}
	if count < len(qs) && len(points) > 1 {
		return nil, fail("للسحب العشوائي، اجعل درجة كل سؤال متساوية لضمان تساوي المجموع", 400)
	}
	mode := str(p["timerMode"])
	if mode != "shared" && mode != "individual" {
		return nil, fail("اختر نظام الوقت", 400)
	}
	title, e := textField(p, "title", 1, 150)
	if e != nil {
		return nil, e
	}
	instructions, e := textField(p, "instructions", 0, 2000)
	if e != nil {
		return nil, e
	}
	mins, e := integerField(p, "minutes", 1, 240)
	if e != nil {
		return nil, e
	}
	late, e := boolField(p, "allowLate")
	if e != nil {
		return nil, e
	}
	shuffle, e := boolField(p, "shuffle")
	if e != nil {
		return nil, e
	}
	shuffleOptions := true
	if _, present := p["shuffleOptions"]; present {
		shuffleOptions, e = boolField(p, "shuffleOptions")
		if e != nil {
			return nil, e
		}
	}
	return M{"title": title, "instructions": instructions, "minutes": mins, "timerMode": mode, "allowLate": late, "shuffle": shuffle, "questionCount": count, "shuffleOptions": shuffleOptions, "questions": qs}, nil
}
func normalizeDigits(s string) string {
	return strings.Map(func(r rune) rune {
		if r >= '٠' && r <= '٩' {
			return '0' + r - '٠'
		}
		if r >= '۰' && r <= '۹' {
			return '0' + r - '۰'
		}
		return r
	}, s)
}
func normalizeCode(v any) (string, error) {
	s, ok := v.(string)
	if !ok {
		return "", fail("اكتب كود الدخول", 400)
	}
	s = strings.ToUpper(normalizeDigits(strings.TrimSpace(s)))
	if len([]rune(s)) < 1 || len([]rune(s)) > 40 {
		return "", fail("كود الطالب من ١ إلى ٤٠ حرفًا أو رقمًا، بدون مسافات", 400)
	}
	for _, r := range s {
		if !unicode.IsLetter(r) && !unicode.IsDigit(r) && !strings.ContainsRune("-_.", r) {
			return "", fail("كود الطالب من ١ إلى ٤٠ حرفًا أو رقمًا، بدون مسافات", 400)
		}
	}
	return s, nil
}

var phonePattern = regexp.MustCompile(`^[0-9]{11}$`)

func normalizePhone(v any) (string, error) {
	s, e := textField(M{"phone": v}, "phone", 1, 25)
	if e != nil {
		return "", e
	}
	s = normalizeDigits(s)
	if !phonePattern.MatchString(s) {
		return "", fail("رقم الموبايل لازم يكون ١١ رقمًا بالضبط", 400)
	}
	return s, nil
}

type Exam struct {
	ID, ConfigText, State string
	Created               float64
	Started, Ended        sql.NullFloat64
	TemplateID, LessonID  sql.NullString
	Config                M
}
type Attempt struct {
	ID, ExamID, Code                      string
	Name, Phone, TokenHash, SubmitReason  sql.NullString
	Joined, LastSeen, Deadline, Submitted sql.NullFloat64
	QuestionIDs, Answers, Grades          string
	Revision                              int
	Paused                                bool
	CancelReason                          string
	Cancelled                             sql.NullFloat64
}
type DB struct {
	sql  *sql.DB
	path string
}

func openDB(path string) (*DB, error) {
	if err := os.MkdirAll(filepath.Dir(path), 0700); err != nil {
		return nil, err
	}
	s, e := sql.Open("sqlite", path)
	if e != nil {
		return nil, e
	}
	s.SetMaxOpenConns(1)
	for _, p := range []string{"PRAGMA busy_timeout=15000", "PRAGMA foreign_keys=ON", "PRAGMA synchronous=FULL", "PRAGMA journal_mode=WAL"} {
		if _, e = s.Exec(p); e != nil {
			s.Close()
			return nil, e
		}
	}
	var version int
	if e = s.QueryRow("PRAGMA user_version").Scan(&version); e != nil {
		s.Close()
		return nil, e
	}
	if version > 3 {
		s.Close()
		return nil, errors.New("This database needs a newer version of Massar Exam Room")
	}
	if version == 1 {
		if _, e = (&DB{s, path}).backup(); e != nil {
			s.Close()
			return nil, e
		}
		migration := []string{
			`CREATE TABLE attempts_v2(id TEXT PRIMARY KEY,exam_id TEXT NOT NULL REFERENCES exams(id),code TEXT NOT NULL,name TEXT,phone TEXT,token_hash TEXT UNIQUE,joined_at REAL,last_seen REAL,deadline REAL,question_ids TEXT NOT NULL DEFAULT '[]',answers TEXT NOT NULL DEFAULT '{}',revision INTEGER NOT NULL DEFAULT 0,submitted_at REAL,submit_reason TEXT,grades TEXT NOT NULL DEFAULT '{}',UNIQUE(exam_id,code))`,
			`INSERT INTO attempts_v2 SELECT * FROM attempts`,
			`DROP TABLE attempts`,
			`ALTER TABLE attempts_v2 RENAME TO attempts`,
			`PRAGMA user_version=2`,
		}
		transaction, err := s.Begin()
		if err != nil {
			s.Close()
			return nil, err
		}
		for _, statement := range migration {
			if _, err = transaction.Exec(statement); err != nil {
				_ = transaction.Rollback()
				s.Close()
				return nil, err
			}
		}
		if err = transaction.Commit(); err != nil {
			s.Close()
			return nil, err
		}
	}
	schema := []string{`CREATE TABLE IF NOT EXISTS exams(id TEXT PRIMARY KEY,config TEXT NOT NULL,state TEXT NOT NULL DEFAULT 'draft',created_at REAL NOT NULL,started_at REAL,ended_at REAL)`, `CREATE TABLE IF NOT EXISTS attempts(id TEXT PRIMARY KEY,exam_id TEXT NOT NULL REFERENCES exams(id),code TEXT NOT NULL,name TEXT,phone TEXT,token_hash TEXT UNIQUE,joined_at REAL,last_seen REAL,deadline REAL,question_ids TEXT NOT NULL DEFAULT '{}',answers TEXT NOT NULL DEFAULT '{}',revision INTEGER NOT NULL DEFAULT 0,submitted_at REAL,submit_reason TEXT,grades TEXT NOT NULL DEFAULT '{}',UNIQUE(exam_id,code))`, `CREATE INDEX IF NOT EXISTS attempts_by_exam ON attempts(exam_id)`, `CREATE INDEX IF NOT EXISTS attempts_by_deadline ON attempts(deadline) WHERE submitted_at IS NULL`, `CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY,created_at REAL NOT NULL,action TEXT NOT NULL,subject_id TEXT NOT NULL)`, `CREATE TABLE IF NOT EXISTS essay_grade_jobs(id TEXT PRIMARY KEY,exam_id TEXT NOT NULL REFERENCES exams(id),state TEXT NOT NULL,model TEXT NOT NULL,total INTEGER NOT NULL,processed INTEGER NOT NULL DEFAULT 0,graded INTEGER NOT NULL DEFAULT 0,skipped INTEGER NOT NULL DEFAULT 0,needs_review INTEGER NOT NULL DEFAULT 0,failed INTEGER NOT NULL DEFAULT 0,message TEXT NOT NULL DEFAULT '',created_at REAL NOT NULL,updated_at REAL NOT NULL)`, `CREATE UNIQUE INDEX IF NOT EXISTS one_running_essay_job ON essay_grade_jobs ((1)) WHERE state='running'`, `CREATE INDEX IF NOT EXISTS essay_jobs_by_exam ON essay_grade_jobs(exam_id,created_at DESC)`}
	schema = append(schema, presenceSchema, cancellationSchema, screenGuardSchema, whatsappSendSchema)
	for _, q := range schema {
		if _, e = s.Exec(q); e != nil {
			s.Close()
			return nil, e
		}
	}
	if version < 3 {
		if version > 0 {
			if _, e = (&DB{s, path}).backup(); e != nil {
				s.Close()
				return nil, e
			}
		}
		transaction, err := s.Begin()
		if err != nil {
			s.Close()
			return nil, err
		}
		migration := []string{
			`CREATE TABLE school_grades(id TEXT PRIMARY KEY,name TEXT NOT NULL UNIQUE,created_at REAL NOT NULL)`,
			`CREATE TABLE centers(id TEXT PRIMARY KEY,grade_id TEXT NOT NULL REFERENCES school_grades(id),name TEXT NOT NULL,created_at REAL NOT NULL,UNIQUE(grade_id,name))`,
			`CREATE TABLE study_groups(id TEXT PRIMARY KEY,center_id TEXT NOT NULL REFERENCES centers(id),name TEXT NOT NULL,created_at REAL NOT NULL,UNIQUE(center_id,name))`,
			`CREATE TABLE lessons(id TEXT PRIMARY KEY,group_id TEXT NOT NULL REFERENCES study_groups(id),name TEXT NOT NULL,scheduled_at REAL,created_at REAL NOT NULL,UNIQUE(group_id,name))`,
			`CREATE TABLE exam_templates(id TEXT PRIMARY KEY,config TEXT NOT NULL,created_at REAL NOT NULL,updated_at REAL NOT NULL,source_exam_id TEXT UNIQUE)`,
			`CREATE TABLE app_settings(key TEXT PRIMARY KEY,value TEXT NOT NULL)`,
			`ALTER TABLE exams ADD COLUMN template_id TEXT REFERENCES exam_templates(id)`,
			`ALTER TABLE exams ADD COLUMN lesson_id TEXT REFERENCES lessons(id)`,
			`CREATE INDEX exams_by_lesson ON exams(lesson_id,created_at DESC)`,
		}
		for _, statement := range migration {
			if _, err = transaction.Exec(statement); err != nil {
				_ = transaction.Rollback()
				s.Close()
				return nil, err
			}
		}
		rows, err := transaction.Query(`SELECT id,config,created_at FROM exams`)
		if err != nil {
			_ = transaction.Rollback()
			s.Close()
			return nil, err
		}
		type priorExam struct {
			id, config string
			created    float64
		}
		prior := []priorExam{}
		for rows.Next() {
			var old priorExam
			if err = rows.Scan(&old.id, &old.config, &old.created); err != nil {
				break
			}
			prior = append(prior, old)
		}
		if err == nil {
			err = rows.Err()
		}
		rows.Close()
		if err == nil {
			for _, old := range prior {
				templateID := id()
				if _, err = transaction.Exec(`INSERT INTO exam_templates(id,config,created_at,updated_at,source_exam_id) VALUES(?,?,?,?,?)`, templateID, old.config, old.created, now(), old.id); err != nil {
					break
				}
				if _, err = transaction.Exec(`UPDATE exams SET template_id=? WHERE id=?`, templateID, old.id); err != nil {
					break
				}
			}
		}
		if err == nil {
			_, err = transaction.Exec(`PRAGMA user_version=3`)
		}
		if err != nil {
			_ = transaction.Rollback()
			s.Close()
			return nil, err
		}
		if err = transaction.Commit(); err != nil {
			s.Close()
			return nil, err
		}
	}
	enabled, err := multipleRooms(s)
	if err == nil {
		err = syncRoomIndex(s, enabled)
	}
	if err != nil {
		s.Close()
		return nil, err
	}
	_ = os.Chmod(path, 0600)
	return &DB{s, path}, nil
}

type Q interface {
	QueryRow(string, ...any) *sql.Row
	Query(string, ...any) (*sql.Rows, error)
	Exec(string, ...any) (sql.Result, error)
}

func (d *DB) tx(fn func(*sql.Tx) error) error {
	t, e := d.sql.BeginTx(context.Background(), nil)
	if e != nil {
		return e
	}
	if e = fn(t); e != nil {
		_ = t.Rollback()
		return e
	}
	return t.Commit()
}
func exam(q Q, eid string) (Exam, error) {
	var x Exam
	e := q.QueryRow(`SELECT id,config,state,created_at,started_at,ended_at,template_id,lesson_id FROM exams WHERE id=?`, eid).Scan(&x.ID, &x.ConfigText, &x.State, &x.Created, &x.Started, &x.Ended, &x.TemplateID, &x.LessonID)
	if errors.Is(e, sql.ErrNoRows) {
		return x, fail("الامتحان غير موجود", 404)
	}
	x.Config = decode(x.ConfigText)
	return x, e
}

const attemptSQL = `SELECT id,exam_id,code,name,phone,token_hash,joined_at,last_seen,deadline,question_ids,answers,revision,submitted_at,submit_reason,grades,COALESCE((SELECT reason FROM attempt_cancellations WHERE attempt_id=attempts.id),''),(SELECT created_at FROM attempt_cancellations WHERE attempt_id=attempts.id) FROM attempts`

func scanAttempt(row interface{ Scan(...any) error }) (Attempt, error) {
	var a Attempt
	e := row.Scan(&a.ID, &a.ExamID, &a.Code, &a.Name, &a.Phone, &a.TokenHash, &a.Joined, &a.LastSeen, &a.Deadline, &a.QuestionIDs, &a.Answers, &a.Revision, &a.Submitted, &a.SubmitReason, &a.Grades, &a.CancelReason, &a.Cancelled)
	return a, e
}
func attempt(q Q, aid string) (Attempt, error) {
	a, e := scanAttempt(q.QueryRow(attemptSQL+` WHERE id=?`, aid))
	if errors.Is(e, sql.ErrNoRows) {
		return a, fail("محاولة الطالب غير موجودة", 404)
	}
	return a, e
}
func audit(q Q, action, subject string) {
	_, _ = q.Exec(`INSERT INTO audit(created_at,action,subject_id) VALUES(?,?,?)`, now(), action, subject)
}
func questions(x Exam, a Attempt) []M {
	lookup := map[string]M{}
	for _, v := range x.Config["questions"].([]any) {
		q := v.(map[string]any)
		lookup[str(q["id"])] = q
	}
	out := []M{}
	for _, v := range array(a.QuestionIDs) {
		if q, ok := lookup[str(v)]; ok {
			out = append(out, q)
		}
	}
	return out
}
func studentView(x Exam, a Attempt) M {
	running := x.State == "running" && !a.Submitted.Valid && !a.Cancelled.Valid
	qs := []M{}
	answers := M{}
	if running {
		answers = decode(a.Answers)
		for _, q := range questions(x, a) {
			v := M{}
			for _, key := range []string{"id", "kind", "text", "points", "options"} {
				if value, ok := q[key]; ok {
					v[key] = value
				}
			}
			if q["kind"] == "mcq" && x.Config["shuffleOptions"] != false {
				v["optionOrder"] = choiceOrder(a.ID, str(q["id"]), len(q["options"].([]any)))
			}
			qs = append(qs, v)
		}
	}
	state := x.State
	if a.Submitted.Valid {
		state = "submitted"
	}
	if a.Cancelled.Valid {
		state = "cancelled"
	}
	return M{"id": a.ID, "name": a.Name.String, "code": a.Code, "examId": x.ID, "title": x.Config["title"], "instructions": x.Config["instructions"], "state": state, "serverTime": now(), "deadline": nullableFloat(a.Deadline), "revision": a.Revision, "submittedAt": nullableFloat(a.Submitted), "questionCount": x.Config["questionCount"], "minutes": x.Config["minutes"], "timerMode": x.Config["timerMode"], "answers": answers, "questions": qs, "paused": a.Paused, "cancelReason": a.CancelReason, "screenGuard": x.Config["screenGuard"] == true}
}
func nullableFloat(n sql.NullFloat64) any {
	if n.Valid {
		return n.Float64
	}
	return nil
}
func nullableString(n sql.NullString) any {
	if n.Valid {
		return n.String
	}
	return nil
}
func adminAttempt(x Exam, a Attempt) M {
	grades := decode(a.Grades)
	score, max, pending := 0.0, 0.0, 0
	for _, g := range grades {
		score += num(g.(map[string]any)["score"])
	}
	for _, q := range questions(x, a) {
		max += num(q["points"])
		if _, ok := grades[str(q["id"])]; !ok {
			pending++
		}
	}
	var p any
	if a.Submitted.Valid {
		p = pending
	}
	return M{"id": a.ID, "code": a.Code, "name": nullableString(a.Name), "phone": nullableString(a.Phone), "joined_at": nullableFloat(a.Joined), "last_seen": nullableFloat(a.LastSeen), "deadline": nullableFloat(a.Deadline), "submitted_at": nullableFloat(a.Submitted), "submit_reason": nullableString(a.SubmitReason), "revision": a.Revision, "answers": decode(a.Answers), "grades": grades, "questionIds": array(a.QuestionIDs), "score": score, "maximum": max, "pending": p, "cancelled_at": nullableFloat(a.Cancelled), "cancelReason": a.CancelReason}
}
func finalize(q Q, a Attempt, x Exam, reason string) error {
	if a.Submitted.Valid || a.Cancelled.Valid {
		return nil
	}
	answers := decode(a.Answers)
	grades := M{}
	for _, question := range questions(x, a) {
		qid := str(question["id"])
		answer := answers[qid]
		if question["kind"] == "mcq" {
			s := 0
			if answer == question["correct"] {
				s = intv(question["points"])
			}
			grades[qid] = M{"score": s, "feedback": "تصحيح تلقائي"}
		} else if strings.TrimSpace(str(answer)) == "" {
			grades[qid] = M{"score": 0, "feedback": "لم تُكتب إجابة"}
		}
	}
	_, e := q.Exec(`UPDATE attempts SET submitted_at=?,submit_reason=?,grades=? WHERE id=?`, now(), reason, encode(grades), a.ID)
	audit(q, "submitted:"+reason, a.ID)
	return e
}
func validAnswers(raw any, qs []M) (M, error) {
	answers, ok := raw.(map[string]any)
	if !ok {
		return nil, fail("الإجابات تحتوي على سؤال غير مخصص لك", 400)
	}
	lookup := map[string]M{}
	for _, q := range qs {
		lookup[str(q["id"])] = q
	}
	for key, value := range answers {
		q, ok := lookup[key]
		if !ok {
			return nil, fail("الإجابات تحتوي على سؤال غير مخصص لك", 400)
		}
		if value == nil {
			continue
		}
		if q["kind"] == "mcq" {
			n, ok := value.(float64)
			if !ok || n != math.Trunc(n) || n < 0 || int(n) >= len(q["options"].([]any)) {
				return nil, fail("اختيار غير صالح", 400)
			}
		} else {
			s, ok := value.(string)
			if !ok || len([]rune(s)) > 10000 {
				return nil, fail("الإجابة المقالية يجب ألا تتجاوز ١٠٠٠٠ حرف", 400)
			}
		}
	}
	return answers, nil
}
func (d *DB) createExam(p M) (M, error) {
	config, e := validateExam(p)
	if e != nil {
		return nil, e
	}
	eid := id()
	e = d.tx(func(q *sql.Tx) error {
		_, e := q.Exec(`INSERT INTO exams(id,config,created_at) VALUES(?,?,?)`, eid, encode(config), now())
		audit(q, "exam-created", eid)
		return e
	})
	return M{"id": eid}, e
}
func (d *DB) dashboard(eid string) (M, error) {
	x, e := exam(d.sql, eid)
	if e != nil {
		return nil, e
	}
	rows, e := d.sql.Query(attemptSQL+` WHERE exam_id=? AND joined_at IS NOT NULL ORDER BY joined_at,rowid`, eid)
	if e != nil {
		return nil, e
	}
	defer rows.Close()
	as := []M{}
	for rows.Next() {
		a, e := scanAttempt(rows)
		if e != nil {
			return nil, e
		}
		as = append(as, adminAttempt(x, a))
	}
	if e = rows.Err(); e != nil {
		return nil, e
	}
	rows.Close()
	presenceRows, err := d.sql.Query(`SELECT attempt_id,paused,departures,hidden_since FROM attempt_presence WHERE attempt_id IN (SELECT id FROM attempts WHERE exam_id=?)`, eid)
	if err != nil {
		return nil, err
	}
	defer presenceRows.Close()
	presenceByID := map[string]M{}
	for presenceRows.Next() {
		var aid string
		var paused, departures int
		var hidden sql.NullFloat64
		if err = presenceRows.Scan(&aid, &paused, &departures, &hidden); err != nil {
			return nil, err
		}
		presenceByID[aid] = M{"paused": paused != 0, "departures": departures, "hiddenSince": nullableFloat(hidden)}
	}
	if err = presenceRows.Err(); err != nil {
		return nil, err
	}
	for _, attempt := range as {
		if presence, ok := presenceByID[str(attempt["id"])]; ok {
			for key, value := range presence {
				attempt[key] = value
			}
		}
	}
	presenceRows.Close()
	screenRows, err := d.sql.Query(`SELECT attempt_id,baseline,last_report,issue,last_event,events,issue_since FROM attempt_screen_guard WHERE attempt_id IN (SELECT id FROM attempts WHERE exam_id=?)`, eid)
	if err != nil {
		return nil, err
	}
	defer screenRows.Close()
	screens := map[string]M{}
	for screenRows.Next() {
		var id, base, last, issue, event string
		var count int
		var since sql.NullFloat64
		if err = screenRows.Scan(&id, &base, &last, &issue, &event, &count, &since); err != nil {
			return nil, err
		}
		screens[id] = M{"baseline": decode(base), "current": decode(last), "issue": issue, "lastEvent": event, "events": count, "since": nullableFloat(since)}
	}
	if err = screenRows.Err(); err != nil {
		return nil, err
	}
	for _, a := range as {
		if v, ok := screens[str(a["id"])]; ok {
			a["screen"] = v
		}
	}
	return M{"exam": examMap(x), "attempts": as, "serverTime": now()}, nil
}
func examMap(x Exam) M {
	return M{"id": x.ID, "config": x.Config, "state": x.State, "created_at": x.Created, "started_at": nullableFloat(x.Started), "ended_at": nullableFloat(x.Ended), "templateId": nullableString(x.TemplateID), "lessonId": nullableString(x.LessonID)}
}
func (d *DB) backup() (string, error) {
	dir := filepath.Join(filepath.Dir(d.path), "backups")
	if e := os.MkdirAll(dir, 0700); e != nil {
		return "", e
	}
	name := fmt.Sprintf("exam-room-%d.sqlite3", time.Now().UnixNano())
	target := filepath.Join(dir, name)
	safe := strings.ReplaceAll(target, "'", "''")
	_, e := d.sql.Exec(`VACUUM INTO '` + safe + `'`)
	if e != nil {
		return "", e
	}
	_ = os.Chmod(target, 0600)
	return target, nil
}
