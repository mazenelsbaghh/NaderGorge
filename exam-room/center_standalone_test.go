package main

import (
	"database/sql"
	"fmt"
	"testing"
)

func TestStandaloneRoomOpensWithoutHostRosterAndTransfersOnlyCorrectedClosedGrades(t *testing.T) {
	a, office, studentURL := centerHTTPServers(t)
	a.centerManualEntry = true
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	office.must(t, "/api/center/admin-access", M{"enabled": true}, 200)
	browser := newCenterHTTPClient(office.base, a.centerAccess.token)
	payload := M{"standalone": true, "templateId": templateID, "requestId": "open-request", "roster": []any{}, "sessionId": "forged"}
	room := browser.must(t, "/api/room/open", payload, 200)
	eid := str(room["examId"])
	replay := browser.must(t, "/api/room/open", payload, 200)
	if replay["examId"] != eid {
		t.Fatal("uncertain opening created another room")
	}
	if a.db.count(`SELECT COUNT(*) FROM center_roster`) != 0 || a.db.count(`SELECT COUNT(*) FROM center_bindings`) != 0 {
		t.Fatal("standalone room required or trusted host records")
	}
	browser.must(t, "/api/center/standalone-results", M{"examId": eid}, 403)
	newCenterHTTPClient(office.base, "").must(t, "/api/center/standalone-rooms", nil, 401)
	student := newCenterHTTPClient(studentURL, "")
	available := student.must(t, "/api/lounge", nil, 200)["rooms"].([]any)
	if len(available) != 1 || available[0].(map[string]any)["id"] != eid {
		t.Fatal("standalone room was hidden from the student entry page")
	}
	student.must(t, "/api/join", M{"examId": eid, "code": "00555", "name": "طالب جديد"}, 400)
	joined := student.must(t, "/api/join", M{"examId": eid, "code": "٠٠٥٥٥", "name": "طالب جديد", "phone": "01012345678"}, 200)["session"].(map[string]any)
	aid := str(joined["id"])
	catalog := office.must(t, "/api/center/standalone-rooms", nil, 200)
	rooms := catalog["rooms"].([]any)
	if len(rooms) != 1 || rooms[0].(map[string]any)["examId"] != eid || num(rooms[0].(map[string]any)["attemptCount"]) != 1 {
		t.Fatal("standalone catalog lost local room or entrant")
	}
	if joined["code"] != "00555" || joined["name"] != "طالب جديد" {
		t.Fatal("manual identity changed")
	}
	office.must(t, "/api/exams/"+eid+"/start", M{}, 200)
	current := student.must(t, "/api/session", nil, 200)["session"].(map[string]any)
	var essay string
	for _, raw := range current["questions"].([]any) {
		q := raw.(map[string]any)
		if q["kind"] == "essay" {
			essay = str(q["id"])
		}
	}
	if essay == "" {
		t.Fatal("essay missing")
	}
	student.must(t, "/api/answers", M{"revision": float64(1), "submit": true, "answers": M{essay: "إجابة الطالب"}}, 200)
	exported := office.must(t, "/api/center/standalone-results", M{"examId": eid}, 200)
	first := exported["attempts"].([]any)[0].(map[string]any)
	if first["status"] != "pending_grading" || first["score"] != nil {
		t.Fatal("uncorrected grade exported")
	}
	office.must(t, "/api/attempts/"+aid+"/grade", M{"questionId": essay, "score": float64(0), "feedback": ""}, 200)
	awaiting := office.must(t, "/api/center/standalone-results", M{"examId": eid}, 200)["attempts"].([]any)[0].(map[string]any)
	if awaiting["status"] != "awaiting_close" || awaiting["score"] != nil {
		t.Fatal("open room exposed importable grades")
	}
	office.must(t, "/api/exams/"+eid+"/close", M{}, 200)
	exported = office.must(t, "/api/center/standalone-results", M{"examId": eid}, 200)
	final := exported["attempts"].([]any)[0].(map[string]any)
	if final["status"] != "final" || final["score"] != float64(0) || final["maxScore"] != float64(10) || final["phone"] != "01012345678" || final["answers"] != nil {
		t.Fatalf("incorrect final export: %v", final)
	}
	stable := office.must(t, "/api/center/standalone-results", M{"examId": eid}, 200)
	if stable["exportRevision"] != exported["exportRevision"] || stable["attempts"].([]any)[0].(map[string]any)["resultVersion"] != final["resultVersion"] {
		t.Fatal("unchanged export changed identity")
	}
	source := str(exported["sourceId"])
	reopened, err := openDB(a.db.path)
	if err != nil {
		t.Fatal(err)
	}
	defer reopened.sql.Close()
	persisted, err := reopened.standaloneCenterResults(M{"examId": eid})
	if err != nil {
		t.Fatal(err)
	}
	if persisted["sourceId"] != source || persisted["attempts"].([]M)[0]["resultVersion"] != int(num(final["resultVersion"])) {
		t.Fatal("restart lost result identity")
	}
	office.must(t, "/api/attempts/"+aid+"/cancel", M{"reason": "اختبار إلغاء"}, 200)
	office.must(t, "/api/center/standalone-results", M{"examId": eid, "exportRevision": exported["exportRevision"]}, 409)
	cancelled := office.must(t, "/api/center/standalone-results", M{"examId": eid}, 200)["attempts"].([]any)[0].(map[string]any)
	if cancelled["score"] != nil || cancelled["status"] != "cancelled" || num(cancelled["resultVersion"]) <= num(final["resultVersion"]) {
		t.Fatal("cancellation left an importable final result")
	}
}

func TestStandaloneRoomHonorsMaintenanceAndRejectsUnmarkedUnboundRooms(t *testing.T) {
	a, office, studentURL := centerHTTPServers(t)
	a.centerManualEntry = true
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	lease := office.must(t, "/api/network/maintenance/acquire", M{}, 200)
	request := M{"standalone": true, "templateId": templateID, "requestId": "guarded-open"}
	office.must(t, "/api/room/open", request, 409)
	office.must(t, "/api/network/maintenance/release", M{"leaseToken": lease["leaseToken"]}, 200)
	eid := str(office.must(t, "/api/room/open", request, 200)["examId"])
	office.must(t, "/api/network/maintenance/acquire", M{}, 409)
	other := M{"standalone": true, "templateId": templateID, "requestId": "second-open"}
	office.must(t, "/api/room/open", other, 409)
	if _, err := a.db.sql.Exec(`DELETE FROM center_standalone_rooms WHERE exam_id=?`, eid); err != nil {
		t.Fatal(err)
	}
	student := newCenterHTTPClient(studentURL, "")
	if len(student.must(t, "/api/lounge", nil, 200)["rooms"].([]any)) != 0 {
		t.Fatal("unmarked unbound room appeared on the student entry page")
	}
	student.must(t, "/api/join", M{"examId": eid, "code": "00555", "name": "طالب", "phone": "01012345678"}, 409)
	office.must(t, "/api/exams/"+eid+"/start", M{}, 409)
}

func TestStandaloneExportPagesAreBoundedCompleteAndRejectChangedSnapshots(t *testing.T) {
	a, office, _ := centerHTTPServers(t)
	a.centerManualEntry = true
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	eid := str(office.must(t, "/api/room/open", M{"standalone": true, "templateId": templateID, "requestId": "paged-open"}, 200)["examId"])
	x, err := exam(a.db.sql, eid)
	if err != nil {
		t.Fatal(err)
	}
	chosen, err := selectQuestionIDs(x.Config)
	if err != nil {
		t.Fatal(err)
	}
	err = a.db.tx(func(q *sql.Tx) error {
		for i := 0; i < standalonePageSize+1; i++ {
			if _, err := q.Exec(`INSERT INTO attempts(id,exam_id,code,name,phone,joined_at,question_ids) VALUES(?,?,?,?,?,?,?)`, fmt.Sprintf("attempt-%04d", i), eid, fmt.Sprintf("%05d", i), "طالب", "01012345678", now(), encode(chosen)); err != nil {
				return err
			}
		}
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
	page := office.must(t, "/api/center/standalone-results", M{"examId": eid}, 200)
	if len(page["attempts"].([]any)) != standalonePageSize || page["hasMore"] != true || num(page["totalAttempts"]) != standalonePageSize+1 {
		t.Fatal("first page unbounded or incomplete")
	}
	next := M{"examId": eid, "afterAttemptId": page["nextCursor"], "exportRevision": page["exportRevision"]}
	last := office.must(t, "/api/center/standalone-results", next, 200)
	if len(last["attempts"].([]any)) != 1 || last["hasMore"] != false || last["nextCursor"] != nil {
		t.Fatal("last page lost rows")
	}
	if _, err = a.db.sql.Exec(`UPDATE attempts SET name='تصحيح الاسم' WHERE id='attempt-0000'`); err != nil {
		t.Fatal(err)
	}
	office.must(t, "/api/center/standalone-results", next, 409)
	office.must(t, "/api/center/standalone-results", M{"examId": eid, "afterAttemptId": page["nextCursor"]}, 409)
}
