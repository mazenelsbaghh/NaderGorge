package main

import (
	"database/sql"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"
)

func TestExamLifecycle(t *testing.T) {
	d, e := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if e != nil {
		t.Fatal(e)
	}
	defer d.sql.Close()
	created, e := d.createExam(demoExam())
	if e != nil {
		t.Fatal(e)
	}
	eid := str(created["id"])
	if _, e = d.changeState(eid, "publish"); e != nil {
		t.Fatal(e)
	}
	joined, tok, e := d.join(M{"code": "١٢٣", "name": "طالب تجربة", "phone": "٠١٠١٢٣٤٥٦٧٨"}, "")
	if e != nil {
		t.Fatal(e)
	}
	s := joined["session"].(M)
	if s["state"] != "waiting" {
		t.Fatalf("state: %v", s["state"])
	}
	if _, e = d.changeState(eid, "start"); e != nil {
		t.Fatal(e)
	}
	session, e := d.session(tok)
	if e != nil {
		t.Fatal(e)
	}
	s = session["session"].(M)
	if s["state"] != "running" {
		t.Fatalf("state: %v", s["state"])
	}
	qs := s["questions"].([]M)
	answers := M{}
	for _, q := range qs {
		if q["kind"] == "mcq" {
			answers[str(q["id"])] = float64(1)
		} else {
			answers[str(q["id"])] = "تنظيم الوقت يقلل التوتر"
		}
	}
	submitted, e := d.saveAnswers(tok, M{"revision": float64(1), "submit": true, "answers": answers})
	if e != nil {
		t.Fatal(e)
	}
	if submitted["session"].(M)["state"] != "submitted" {
		t.Fatal("not submitted")
	}
	dashboard, e := d.dashboard(eid)
	if e != nil {
		t.Fatal(e)
	}
	as := dashboard["attempts"].([]M)
	if len(as) != 1 || as[0]["pending"] != 1 {
		t.Fatalf("attempts: %#v", as)
	}
	if _, e = d.report(str(as[0]["id"])); e == nil {
		t.Fatal("report before grading")
	}
	essayID := ""
	for _, q := range qs {
		if q["kind"] == "essay" {
			essayID = str(q["id"])
		}
	}
	if _, e = d.gradeEssay(str(as[0]["id"]), M{"questionId": essayID, "score": float64(4), "feedback": "صحيح"}); e != nil {
		t.Fatal(e)
	}
	if _, e = d.changeState(eid, "close"); e != nil {
		t.Fatal(e)
	}
	report, e := d.report(str(as[0]["id"]))
	if e != nil {
		t.Fatal(e)
	}
	if !strings.Contains(string(reportHTML(report)), "طالب تجربة") {
		t.Fatal("report missing name")
	}
	if _, e = d.backup(); e != nil {
		t.Fatal(e)
	}
	check, e := d.check()
	if e != nil || check["healthy"] != true {
		t.Fatalf("check: %v %v", check, e)
	}
}
func TestHTTPGuards(t *testing.T) {
	d, e := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if e != nil {
		t.Fatal(e)
	}
	defer d.sql.Close()
	a := &app{db: d, grader: &grader{db: d, envPath: filepath.Join(t.TempDir(), ".env")}, adminToken: "secret", studentPort: 8765, adminPort: 8766, addresses: func() []string { return []string{"192.168.1.2"} }}
	req := httptest.NewRequest("GET", "http://127.0.0.1:8766/api/bootstrap", nil)
	rec := httptest.NewRecorder()
	a.handler(true).ServeHTTP(rec, req)
	if rec.Code != 200 {
		t.Fatal(rec.Body.String())
	}
	var result M
	if e = json.Unmarshal(rec.Body.Bytes(), &result); e != nil || result["token"] != "secret" {
		t.Fatal("bootstrap")
	}
	req = httptest.NewRequest("GET", "http://192.168.1.2:8766/api/bootstrap", nil)
	rec = httptest.NewRecorder()
	a.handler(true).ServeHTTP(rec, req)
	if rec.Code != 403 {
		t.Fatal("admin exposed to LAN")
	}
	req = httptest.NewRequest("GET", "http://192.168.1.2:8765/assets/admin.js", nil)
	rec = httptest.NewRecorder()
	a.handler(false).ServeHTTP(rec, req)
	if rec.Code != 404 {
		t.Fatal("private asset exposed")
	}
}

func TestStudentAddressUpdatesWithoutRestart(t *testing.T) {
	addresses := []string{"192.168.1.2"}
	a := &app{adminToken: "secret", studentPort: 8765, adminPort: 8766, addresses: func() []string { return addresses }}
	student := func(address string) int {
		req := httptest.NewRequest("GET", "http://"+address+":8765/", nil)
		rec := httptest.NewRecorder()
		a.handler(false).ServeHTTP(rec, req)
		return rec.Code
	}
	if student("192.168.1.2") != 200 {
		t.Fatal("first local address was rejected")
	}
	addresses = []string{"192.168.43.10"}
	if student("192.168.43.10") != 200 || student("192.168.1.2") != 403 {
		t.Fatal("student access did not follow the network change")
	}
	req := httptest.NewRequest("GET", "http://127.0.0.1:8766/api/status", nil)
	req.AddCookie(&http.Cookie{Name: "massar_admin", Value: "secret"})
	rec := httptest.NewRecorder()
	a.handler(true).ServeHTTP(rec, req)
	var status struct {
		StudentURLs []string `json:"studentUrls"`
	}
	if rec.Code != 200 || json.Unmarshal(rec.Body.Bytes(), &status) != nil || len(status.StudentURLs) != 1 || status.StudentURLs[0] != "http://192.168.43.10:8765" {
		t.Fatalf("the admin status kept a stale student link: %s", rec.Body.String())
	}
}

func TestBatchAwardsFullAndPartialEssayPoints(t *testing.T) {
	for _, partial := range []bool{false, true} {
		t.Run(map[bool]string{false: "full", true: "partial"}[partial], func(t *testing.T) {
			expectedScore := float64(7)
			if partial {
				expectedScore = 2.5
			}
			d, e := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
			if e != nil {
				t.Fatal(e)
			}
			defer d.sql.Close()
			created, e := d.createExam(M{"title": "مقالي", "instructions": "", "minutes": float64(5), "timerMode": "shared", "allowLate": true, "shuffle": false, "questionCount": float64(1), "questions": []any{M{"kind": "essay", "text": "سؤال تجربة", "modelAnswer": "إجابة صحيحة", "points": float64(7)}}})
			if e != nil {
				t.Fatal(e)
			}
			eid := str(created["id"])
			if _, e = d.changeState(eid, "publish"); e != nil {
				t.Fatal(e)
			}
			joined, tok, e := d.join(M{"code": "TEST", "name": "طالب تجربة", "phone": "01012345678"}, "")
			if e != nil {
				t.Fatal(e)
			}
			if _, e = d.changeState(eid, "start"); e != nil {
				t.Fatal(e)
			}
			session, e := d.session(tok)
			if e != nil {
				t.Fatal(e)
			}
			question := session["session"].(M)["questions"].([]M)[0]
			_, e = d.saveAnswers(tok, M{"revision": float64(1), "submit": true, "answers": M{str(question["id"]): "إجابة صحيحة"}})
			if e != nil {
				t.Fatal(e)
			}
			if _, e = d.changeState(eid, "close"); e != nil {
				t.Fatal(e)
			}
			endpoint := geminiEndpoint
			mock := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				if r.Header.Get("x-goog-api-key") != "test-key-123456" {
					t.Error("missing key")
				}
				w.Header().Set("Content-Type", "application/json")
				json.NewEncoder(w).Encode(M{"candidates": []M{{"content": M{"parts": []M{{"text": encode(M{"correct": !partial, "needsReview": false, "feedback": "تصحيح بالمعنى", "score": expectedScore})}}}}}})
			}))
			defer mock.Close()
			geminiEndpoint = mock.URL
			defer func() { geminiEndpoint = endpoint }()
			g := &grader{db: d, envPath: filepath.Join(filepath.Dir(d.path), ".env")}
			if _, e = g.start(eid, M{"apiKey": "test-key-123456", "partialCredit": partial}); e != nil {
				t.Fatal(e)
			}
			g.wg.Wait()
			status, e := g.status(eid)
			if e != nil {
				t.Fatal(e)
			}
			job := status["job"].(M)
			if job["state"] != "completed" || job["graded"] != 1 {
				t.Fatalf("job: %#v", job)
			}
			dashboard, e := d.dashboard(eid)
			if e != nil {
				t.Fatal(e)
			}
			attempts := dashboard["attempts"].([]M)
			if attempts[0]["score"] != expectedScore || attempts[0]["pending"] != 0 {
				t.Fatalf("score: %#v", attempts[0])
			}
			if joined["session"] == nil {
				t.Fatal("join response")
			}
		})
	}
}

func TestVersionOneMigrationKeepsAttempts(t *testing.T) {
	path := filepath.Join(t.TempDir(), "exams.sqlite3")
	raw, e := sql.Open("sqlite", path)
	if e != nil {
		t.Fatal(e)
	}
	statements := []string{
		`CREATE TABLE exams(id TEXT PRIMARY KEY,config TEXT NOT NULL,state TEXT NOT NULL DEFAULT 'draft',created_at REAL NOT NULL,started_at REAL,ended_at REAL)`,
		`CREATE TABLE attempts(id TEXT PRIMARY KEY,exam_id TEXT NOT NULL REFERENCES exams(id),code TEXT NOT NULL UNIQUE,name TEXT,phone TEXT,token_hash TEXT UNIQUE,joined_at REAL,last_seen REAL,deadline REAL,question_ids TEXT NOT NULL DEFAULT '[]',answers TEXT NOT NULL DEFAULT '{}',revision INTEGER NOT NULL DEFAULT 0,submitted_at REAL,submit_reason TEXT,grades TEXT NOT NULL DEFAULT '{}')`,
		`INSERT INTO exams(id,config,created_at) VALUES('old','{"title":"قديم","questions":[]}',1)`,
		`INSERT INTO attempts(id,exam_id,code,name) VALUES('one','old','ABC','طالب')`,
		`PRAGMA user_version=1`,
	}
	for _, statement := range statements {
		if _, e = raw.Exec(statement); e != nil {
			t.Fatal(e)
		}
	}
	raw.Close()
	d, e := openDB(path)
	if e != nil {
		t.Fatal(e)
	}
	defer d.sql.Close()
	a, e := attempt(d.sql, "one")
	if e != nil || a.Name.String != "طالب" {
		t.Fatalf("lost attempt: %v %#v", e, a)
	}
	var version int
	if e = d.sql.QueryRow(`PRAGMA user_version`).Scan(&version); e != nil || version != 3 {
		t.Fatalf("version %d: %v", version, e)
	}
	if len(d.backupList()) < 1 {
		t.Fatal("missing pre-migration backup")
	}
	var templateID string
	if e = d.sql.QueryRow(`SELECT template_id FROM exams WHERE id='old'`).Scan(&templateID); e != nil || templateID == "" {
		t.Fatalf("legacy exam lost its template: %v", e)
	}
}

func TestCatalogTemplateRoomAndPhoneRules(t *testing.T) {
	d, e := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if e != nil {
		t.Fatal(e)
	}
	defer d.sql.Close()
	grade, e := d.createCatalogItem("grades", M{"name": "الصف الأول الثانوي"})
	if e != nil {
		t.Fatal(e)
	}
	center, e := d.createCatalogItem("centers", M{"gradeId": grade["id"], "name": "سنتر النصر"})
	if e != nil {
		t.Fatal(e)
	}
	group, e := d.createCatalogItem("groups", M{"centerId": center["id"], "name": "مجموعة السبت"})
	if e != nil {
		t.Fatal(e)
	}
	lesson, e := d.createCatalogItem("lessons", M{"groupId": group["id"], "name": "الحصة الأولى"})
	if e != nil {
		t.Fatal(e)
	}
	catalog, e := d.catalog()
	if e != nil {
		t.Fatal(e)
	}
	if len(catalog["grades"].([]M)) != 1 || len(catalog["lessons"].([]M)) != 1 {
		t.Fatalf("catalog: %#v", catalog)
	}
	template, e := d.createTemplate(demoExam())
	if e != nil {
		t.Fatal(e)
	}
	created, e := d.openRoom(str(lesson["id"]), str(template["id"]))
	if e != nil {
		t.Fatal(e)
	}
	exam, e := exam(d.sql, str(created["id"]))
	if e != nil {
		t.Fatal(e)
	}
	labels, err := d.sheetContext(str(created["id"]), str(group["id"]))
	if err != nil || labels[2] != "مجموعة السبت" {
		t.Fatalf("sheet group: %v %v", labels, err)
	}
	if _, err = d.sheetContext(str(created["id"]), "another-group"); err == nil {
		t.Fatal("exported another group")
	}
	if exam.State != "waiting" || exam.TemplateID.String != template["id"] || exam.LessonID.String != lesson["id"] {
		t.Fatalf("room links: %#v", exam)
	}
	if _, e = d.openRoom(str(lesson["id"]), str(template["id"])); e == nil {
		t.Fatal("opened simultaneous room")
	}
	for _, v := range []string{"0101234567", "010123456789", "+201012345678", "010 12345678"} {
		if _, e = normalizePhone(v); e == nil {
			t.Fatalf("accepted invalid phone %s", v)
		}
	}
	for _, v := range []string{"01012345678", "٠١٠١٢٣٤٥٦٧٨", "۰۱۰۱۲۳۴۵۶۷۸"} {
		if got, e := normalizePhone(v); e != nil || got != "01012345678" {
			t.Fatalf("phone %s -> %s, %v", v, got, e)
		}
	}
	if _, e = d.saveWhatsAppSettings(M{"senderPhone": "01012345678", "templateName": "exam_result", "templateBody": "مرحبًا {{1}}"}); e != nil {
		t.Fatal(e)
	}
	settings, e := d.whatsappSettings()
	if e != nil || settings["senderPhone"] != "01012345678" || settings["enabled"] != false || settings["templateName"] != "exam_result" || settings["templateBody"] != "مرحبًا {{1}}" {
		t.Fatalf("settings: %#v %v", settings, e)
	}
}

func TestNamedExamCannotBeTakenTwiceWithSameStudentCode(t *testing.T) {
	d, e := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if e != nil {
		t.Fatal(e)
	}
	defer d.sql.Close()
	grade, e := d.createCatalogItem("grades", M{"name": "الأول الثانوي"})
	if e != nil {
		t.Fatal(e)
	}
	center, e := d.createCatalogItem("centers", M{"gradeId": grade["id"], "name": "سنتر"})
	if e != nil {
		t.Fatal(e)
	}
	group, e := d.createCatalogItem("groups", M{"centerId": center["id"], "name": "مجموعة"})
	if e != nil {
		t.Fatal(e)
	}
	lesson, e := d.createCatalogItem("lessons", M{"groupId": group["id"], "name": "حصة"})
	if e != nil {
		t.Fatal(e)
	}
	template, e := d.createTemplate(demoExam())
	if e != nil {
		t.Fatal(e)
	}
	first, e := d.openRoom(str(lesson["id"]), str(template["id"]))
	if e != nil {
		t.Fatal(e)
	}
	joined, tok, e := d.join(M{"code": "STUDENT-1", "name": "طالب أول", "phone": "01012345678"}, "")
	if e != nil {
		t.Fatal(e)
	}
	if joined["session"].(M)["state"] != "waiting" {
		t.Fatal("not in waiting room")
	}
	if _, e = d.changeState(str(first["id"]), "start"); e != nil {
		t.Fatal(e)
	}
	if _, e = d.saveAnswers(tok, M{"revision": float64(1), "submit": true, "answers": M{}}); e != nil {
		t.Fatal(e)
	}
	resumed, sameToken, e := d.join(M{"code": "STUDENT-1", "name": "طالب أول", "phone": "01012345678"}, tok)
	if e != nil || sameToken != tok || resumed["session"].(M)["state"] != "submitted" {
		t.Fatalf("submitted attempt should only reopen its receipt: %v %#v", e, resumed)
	}
	if _, _, e = d.join(M{"code": "STUDENT-1", "name": "طالب أول", "phone": "01012345678"}, ""); e == nil || !strings.Contains(e.Error(), "محاولة جديدة") {
		t.Fatalf("another browser joined the same code or got unclear error: %v", e)
	}
	if _, e = d.changeState(str(first["id"]), "close"); e != nil {
		t.Fatal(e)
	}
	second, e := d.openRoom(str(lesson["id"]), str(template["id"]))
	if e != nil {
		t.Fatal(e)
	}
	if _, _, e = d.join(M{"code": "STUDENT-1", "name": "طالب أول", "phone": "01111111111"}, ""); e == nil || !strings.Contains(e.Error(), "لا يمكن إعادته") {
		t.Fatalf("retake with prior code accepted or unclear error: %v", e)
	}
	if d.count(`SELECT COUNT(*) FROM attempts WHERE exam_id='`+str(second["id"])+`'`) != 0 {
		t.Fatal("rejected join left a placeholder attempt")
	}
	if _, _, e = d.join(M{"code": "STUDENT-2", "name": "طالب ثان", "phone": "01222222222"}, ""); e != nil {
		t.Fatalf("another student was blocked: %v", e)
	}
}

func TestCanceledWaitingRoomDoesNotConsumeExamAttempt(t *testing.T) {
	d, e := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if e != nil {
		t.Fatal(e)
	}
	defer d.sql.Close()
	grade, _ := d.createCatalogItem("grades", M{"name": "الأول الثانوي"})
	center, _ := d.createCatalogItem("centers", M{"gradeId": grade["id"], "name": "سنتر"})
	group, _ := d.createCatalogItem("groups", M{"centerId": center["id"], "name": "مجموعة"})
	lesson, _ := d.createCatalogItem("lessons", M{"groupId": group["id"], "name": "حصة"})
	template, e := d.createTemplate(demoExam())
	if e != nil {
		t.Fatal(e)
	}
	first, e := d.openRoom(str(lesson["id"]), str(template["id"]))
	if e != nil {
		t.Fatal(e)
	}
	if _, _, e = d.join(M{"code": "STUDENT-1", "name": "طالب أول", "phone": "01012345678"}, ""); e != nil {
		t.Fatal(e)
	}
	if _, e = d.changeState(str(first["id"]), "close"); e != nil {
		t.Fatal(e)
	}
	if _, e = d.openRoom(str(lesson["id"]), str(template["id"])); e != nil {
		t.Fatal(e)
	}
	if _, _, e = d.join(M{"code": "STUDENT-1", "name": "طالب أول", "phone": "01012345678"}, ""); e != nil {
		t.Fatalf("student was blocked after waiting room was canceled: %v", e)
	}
}

func TestExpiredStudentCookieIsClearedWithoutChangingAttempts(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	a := &app{db: d, studentPort: 8765, adminPort: 8766, addresses: func() []string { return nil }}
	req := httptest.NewRequest("GET", "http://127.0.0.1:8765/api/session", nil)
	req.AddCookie(&http.Cookie{Name: "massar_student", Value: "expired-token"})
	response := httptest.NewRecorder()
	a.handler(false).ServeHTTP(response, req)
	if response.Code != 401 {
		t.Fatalf("status %d", response.Code)
	}
	cleared := false
	for _, cookie := range response.Result().Cookies() {
		if cookie.Name == "massar_student" && cookie.MaxAge < 0 && cookie.Path == "/" {
			cleared = true
		}
	}
	if !cleared {
		t.Fatal("expired cookie not cleared")
	}
	req = httptest.NewRequest("GET", "http://127.0.0.1:8765/api/session", nil)
	response = httptest.NewRecorder()
	a.handler(false).ServeHTTP(response, req)
	if response.Code != 200 || !strings.Contains(response.Body.String(), `"session":null`) {
		t.Fatalf("anonymous retry: %d %s", response.Code, response.Body.String())
	}
}
