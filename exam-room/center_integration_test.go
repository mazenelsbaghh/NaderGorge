package main

import (
	"net/http"
	"net/http/httptest"
	"net/url"
	"path/filepath"
	"strings"
	"testing"
)

func TestIntegratedWindowsAdvertisesOnlyStudentNICAndWaitsForConfiguration(t *testing.T) {
	addresses := []string{"192.168.137.1", "192.168.1.50"}
	a := &app{studentPort: 8765, addresses: func() []string { return centerStudentAddresses(addresses) }}
	if len(a.studentURLs()) != 0 {
		t.Fatal("unconfigured exam advertised reception Wi-Fi")
	}
	addresses = append(addresses, "10.77.0.1")
	urls := a.studentURLs()
	if len(urls) != 2 || urls[0] != "http://10.77.0.1/" || urls[1] != "http://10.77.0.1:8765" {
		t.Fatalf("configured exam advertised incorrect links: %v", urls)
	}
	request := httptest.NewRequest("GET", "http://192.168.137.1:8765/api/lounge", nil)
	if err := a.verify(request, false); err == nil {
		t.Fatal("student listener accepted the reception Wi-Fi host")
	}
	addresses = []string{"192.168.137.1"}
	if len(a.studentURLs()) != 0 {
		t.Fatal("disconnected exam fell back to private reception network")
	}
}

func centerTestRoom(t *testing.T) (*DB, string, M) {
	t.Helper()
	d, e := openDB(filepath.Join(t.TempDir(), "exams.sqlite3"))
	if e != nil {
		t.Fatal(e)
	}
	t.Cleanup(func() { d.sql.Close() })
	created, e := d.createExam(demoExam())
	if e != nil {
		t.Fatal(e)
	}
	eid := str(created["id"])
	binding := M{"examId": eid, "groupId": "group", "sessionId": "lesson", "activityId": "exam-activity", "maxScore": float64(10), "roster": []any{M{"studentId": "student-1", "code": "00123", "name": "اسم الطالب الرسمي", "phone": "01012345678", "present": true}}}
	if _, e = d.bindCenter(binding); e != nil {
		t.Fatal(e)
	}
	if _, e = d.changeState(eid, "publish"); e != nil {
		t.Fatal(e)
	}
	return d, eid, binding
}
func TestCenterPhoneEntryUsesOfficialIdentityAndRejectsSharedPhoneGuess(t *testing.T) {
	d, eid, binding := centerTestRoom(t)
	for _, phone := range []string{"٠١٠١٢٣٤٥٦٧٨", "+20 1012345678", "00201012345678"} {
		found, e := d.lookupCenter(M{"examId": eid, "phone": phone})
		if e != nil || found["code"] != "00123" || found["name"] != "اسم الطالب الرسمي" {
			t.Fatalf("official identity: %v %v", found, e)
		}
	}
	second := M{"studentId": "student-2", "code": "00456", "name": "طالب آخر", "phone": "01012345678", "present": true}
	binding["roster"] = append(binding["roster"].([]any), second)
	if _, e := d.bindCenter(binding); e != nil {
		t.Fatal(e)
	}
	if _, e := d.lookupCenter(M{"examId": eid, "phone": "01012345678"}); e == nil {
		t.Fatal("shared phone selected a student without code")
	}
	if _, _, e := d.join(M{"examId": eid, "phone": "01012345678", "name": "اسم مزور", "studentId": "student-2"}, ""); e == nil {
		t.Fatal("ambiguous join accepted client identity")
	}
	joined, _, e := d.join(M{"examId": eid, "phone": "01012345678", "code": "00123", "name": "اسم مزور", "studentId": "student-2"}, "")
	if e != nil {
		t.Fatal(e)
	}
	attemptID := str(joined["session"].(M)["id"])
	stored, e := attempt(d.sql, attemptID)
	if e != nil || stored.Name.String != "اسم الطالب الرسمي" {
		t.Fatalf("client name used: %v %v", stored, e)
	}
	var owner string
	if e = d.sql.QueryRow(`SELECT student_id FROM center_attempts WHERE attempt_id=?`, attemptID).Scan(&owner); e != nil || owner != "student-1" {
		t.Fatalf("incorrect owner %s %v", owner, e)
	}
	binding["sessionId"] = "different"
	if _, e = d.bindCenter(binding); e == nil {
		t.Fatal("active mapping replaced")
	}
}
func TestCenterResultsWaitForEssayPreserveZeroAndPersistNewerThanAcknowledgement(t *testing.T) {
	d, eid, _ := centerTestRoom(t)
	joined, tok, e := d.join(M{"examId": eid, "phone": "01012345678"}, "")
	if e != nil {
		t.Fatal(e)
	}
	aid := str(joined["session"].(M)["id"])
	if _, e = d.changeState(eid, "start"); e != nil {
		t.Fatal(e)
	}
	x, e := exam(d.sql, eid)
	if e != nil {
		t.Fatal(e)
	}
	var essayID string
	for _, raw := range x.Config["questions"].([]any) {
		q := raw.(map[string]any)
		if q["kind"] == "essay" {
			essayID = str(q["id"])
		}
	}
	if _, e = d.saveAnswers(tok, M{"revision": float64(1), "submit": true, "answers": M{essayID: "إجابة تحتاج تصحيح"}}); e != nil {
		t.Fatal(e)
	}
	if _, e = d.changeState(eid, "close"); e != nil {
		t.Fatal(e)
	}
	state, e := d.centerState()
	if e != nil {
		t.Fatal(e)
	}
	if len(state["results"].([]M)) != 0 {
		t.Fatal("incomplete grading was exported")
	}
	if _, e = d.gradeEssay(aid, M{"questionId": essayID, "score": float64(0), "feedback": ""}); e != nil {
		t.Fatal(e)
	}
	state, e = d.centerState()
	if e != nil {
		t.Fatal(e)
	}
	results := state["results"].([]M)
	if len(results) != 1 || num(results[0]["score"]) != 0 || intv(results[0]["version"]) != 1 {
		t.Fatalf("zero grade lost: %v", results)
	}
	source := state["sourceId"]
	if _, e = d.gradeEssay(aid, M{"questionId": essayID, "score": float64(2.5), "feedback": "تعديل"}); e != nil {
		t.Fatal(e)
	}
	state, e = d.centerState()
	if e != nil {
		t.Fatal(e)
	}
	if intv(state["results"].([]M)[0]["version"]) != 2 {
		t.Fatal("regrade did not advance version")
	}
	if _, e = d.ackCenter(M{"attemptId": aid, "version": float64(1)}); e != nil {
		t.Fatal(e)
	}
	d.sql.Close()
	reopened, e := openDB(d.path)
	if e != nil {
		t.Fatal(e)
	}
	defer reopened.sql.Close()
	state, e = reopened.centerState()
	if e != nil {
		t.Fatal(e)
	}
	results = state["results"].([]M)
	if state["sourceId"] != source || len(results) != 1 || num(results[0]["score"]) != 2.5 || intv(results[0]["version"]) != 2 {
		t.Fatalf("replay after restart: %v", state)
	}
	if _, e = reopened.ackCenter(M{"attemptId": aid, "version": float64(2)}); e != nil {
		t.Fatal(e)
	}
	state, e = reopened.centerState()
	if e != nil || len(state["results"].([]M)) != 0 {
		t.Fatal("acknowledged result repeated")
	}
	if _, e = reopened.cancelAttempt(aid, M{"reason": "إلغاء معتمد"}); e != nil {
		t.Fatal(e)
	}
	state, e = reopened.centerState()
	if e != nil {
		t.Fatal(e)
	}
	results = state["results"].([]M)
	if len(results) != 1 || results[0]["status"] != "revoked" || results[0]["score"] != nil || intv(results[0]["version"]) != 3 {
		t.Fatalf("cancellation not propagated: %v", results)
	}
}
func centerAdminRequest(a *app, path string, body M, secret string, cookie *http.Cookie) *httptest.ResponseRecorder {
	method := "GET"
	content := ""
	if body != nil {
		method = "POST"
		content = encode(body)
	}
	r := httptest.NewRequest(method, a.adminOrigin()+path, strings.NewReader(content))
	r.Header.Set("X-Admin-Token", secret)
	r.Header.Set("X-Exam-Request", "1")
	r.Header.Set("Content-Type", "application/json")
	if cookie != nil {
		r.AddCookie(cookie)
	}
	w := httptest.NewRecorder()
	a.handler(true).ServeHTTP(w, r)
	return w
}
func TestCenterBrowserCapabilityIsSeparateSingleUseAndRevokedWithoutStoppingEngine(t *testing.T) {
	d, _, _ := centerTestRoom(t)
	a := &app{db: d, adminToken: "controller-secret", adminPort: 8766, studentPort: 8765, addresses: func() []string { return nil }, centerIntegrated: true}
	if w := centerAdminRequest(a, "/api/bootstrap", nil, "", nil); w.Code != 401 {
		t.Fatal("bootstrap revealed controller credential")
	}
	w := centerAdminRequest(a, "/api/center/admin-access", M{"enabled": true}, a.adminToken, nil)
	if w.Code != 200 {
		t.Fatal(w.Body.String())
	}
	access := decode(w.Body.String())
	u, e := url.Parse(str(access["adminUrl"]))
	if e != nil {
		t.Fatal(e)
	}
	capability := strings.TrimPrefix(u.Fragment, "center-access=")
	login := centerAdminRequest(a, "/api/center/browser-login", M{"capability": capability}, "", nil)
	if login.Code != 200 {
		t.Fatal(login.Body.String())
	}
	cookies := login.Result().Cookies()
	if len(cookies) != 1 {
		t.Fatal("missing UI session")
	}
	if repeat := centerAdminRequest(a, "/api/center/browser-login", M{"capability": capability}, "", nil); repeat.Code != 401 {
		t.Fatal("capability reused")
	}
	boot := centerAdminRequest(a, "/api/bootstrap", nil, "", cookies[0])
	if boot.Code != 200 {
		t.Fatal(boot.Body.String())
	}
	uiToken := str(decode(boot.Body.String())["token"])
	if uiToken == a.adminToken {
		t.Fatal("browser received controller token")
	}
	if w = centerAdminRequest(a, "/api/center/state", nil, uiToken, nil); w.Code != 403 {
		t.Fatal("browser accessed controller state")
	}
	if w = centerAdminRequest(a, "/api/center/admin-access", M{"enabled": false}, a.adminToken, nil); w.Code != 200 {
		t.Fatal(w.Body.String())
	}
	if w = centerAdminRequest(a, "/api/exams", nil, uiToken, cookies[0]); w.Code != 401 {
		t.Fatal("logged out UI still had access")
	}
	if w = centerAdminRequest(a, "/api/center/state", nil, a.adminToken, nil); w.Code != 200 {
		t.Fatal("controller stopped with UI logout")
	}
}

func TestCenterRosterUpdatesAdmitLateStudentWithoutChangingAttemptOwner(t *testing.T) {
	d, eid, binding := centerTestRoom(t)
	joined, tok, e := d.join(M{"phone": "01012345678"}, "")
	if e != nil {
		t.Fatal(e)
	}
	aid := str(joined["session"].(M)["id"])
	roster := binding["roster"].([]any)
	roster[0].(M)["phone"] = "01112345678"
	roster = append(roster, M{"studentId": "late-student", "code": "00124", "name": "طالب متأخر", "phone": "01212345678", "present": true})
	binding["roster"] = roster
	if _, e = d.changeState(eid, "start"); e != nil {
		t.Fatal(e)
	}
	if _, e = d.bindCenter(binding); e != nil {
		t.Fatal(e)
	}
	recovered, _, e := d.join(M{"phone": "01112345678"}, tok)
	if e != nil || recovered["session"].(M)["id"] != aid {
		t.Fatalf("identity changed after phone correction %v %v", recovered, e)
	}
	if _, _, e = d.join(M{"phone": "01212345678"}, ""); e != nil {
		t.Fatalf("late registered student blocked: %v", e)
	}
	state, e := d.centerState()
	if e != nil {
		t.Fatal(e)
	}
	if len(state["attempts"].([]M)) != 2 {
		t.Fatalf("missing late attempt: %v", state)
	}
}
func TestIntegratedStudentPortRejectsUnboundRoomAndCannotAccessAdministration(t *testing.T) {
	d, eid, _ := centerTestRoom(t)
	if _, e := d.sql.Exec(`DELETE FROM center_bindings WHERE exam_id=?`, eid); e != nil {
		t.Fatal(e)
	}
	a := &app{db: d, adminToken: "private", adminPort: 8766, studentPort: 8765, addresses: func() []string { return nil }, centerIntegrated: true}
	for _, path := range []string{"/api/center/state", "/api/bootstrap"} {
		r := httptest.NewRequest("GET", "http://127.0.0.1:8765"+path, nil)
		w := httptest.NewRecorder()
		a.handler(false).ServeHTTP(w, r)
		if w.Code != 404 {
			t.Fatalf("student administration exposed at %s: %d", path, w.Code)
		}
	}
	r := httptest.NewRequest("GET", "http://127.0.0.1:8765/api/lounge", nil)
	w := httptest.NewRecorder()
	a.handler(false).ServeHTTP(w, r)
	if w.Code != 200 || len(decode(w.Body.String())["rooms"].([]any)) != 0 {
		t.Fatal("unbound room advertised")
	}
	r = httptest.NewRequest("POST", "http://127.0.0.1:8765/api/join", strings.NewReader(encode(M{"examId": eid, "name": "طالب", "code": "guess", "phone": "01012345678"})))
	r.Header.Set("X-Exam-Request", "1")
	r.Header.Set("Content-Type", "application/json")
	w = httptest.NewRecorder()
	a.handler(false).ServeHTTP(w, r)
	if w.Code != 409 || d.count(`SELECT COUNT(*) FROM attempts`) != 0 {
		t.Fatal("unbound direct join bypassed roster")
	}
}
