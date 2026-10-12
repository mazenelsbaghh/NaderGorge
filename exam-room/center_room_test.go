package main

import (
	"testing"
)

func centerOpenPayload(templateID string) M {
	return M{"templateId": templateID, "groupId": "center-group", "sessionId": "center-session", "activityId": "center-exam", "maxScore": float64(10), "roster": []any{
		M{"studentId": "center-student", "code": "00123", "name": "طالب من السنتر", "phone": "01012345678", "present": true},
	}}
}

func assertCenterRoomCount(t *testing.T, d *DB, count int) {
	t.Helper()
	for _, table := range []string{"exams", "center_bindings", "center_roster"} {
		var got int
		if err := d.sql.QueryRow(`SELECT COUNT(*) FROM ` + table).Scan(&got); err != nil || got != count {
			t.Fatalf("%s count %d, wanted %d: %v", table, got, count, err)
		}
	}
}

func TestCenterHTTPFirstRoomUsesCenterRosterWithoutDuplicateCatalog(t *testing.T) {
	a, office, studentURL := centerHTTPServers(t)
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	state := office.must(t, "/api/center/state", nil, 200)
	templates := state["templates"].([]any)
	if len(templates) != 1 {
		t.Fatal("saved library template missing from center state")
	}
	brief := templates[0].(map[string]any)
	if len(brief) != 3 || brief["id"] != templateID || brief["title"] != demoExam()["title"] || num(brief["maxScore"]) != 10 {
		t.Fatalf("template summary leaked questions or lost identity/maximum: %v", brief)
	}
	payload := centerOpenPayload(templateID)
	created := office.must(t, "/api/center/open", payload, 200)
	eid := str(created["examId"])
	if eid == "" || created["id"] != eid {
		t.Fatal("missing created room identity")
	}
	state = office.must(t, "/api/center/state", nil, 200)
	room := state["exams"].([]any)[0].(map[string]any)
	binding := state["bindings"].([]any)[0].(map[string]any)
	if room["id"] != eid || room["state"] != "waiting" || room["lessonId"] != nil || binding["sessionId"] != payload["sessionId"] || binding["activityId"] != payload["activityId"] {
		t.Fatal("first-use room did not retain authoritative center destination")
	}
	for _, table := range []string{"school_grades", "centers", "study_groups", "lessons"} {
		var count int
		if err := a.db.sql.QueryRow(`SELECT COUNT(*) FROM ` + table).Scan(&count); err != nil || count != 0 {
			t.Fatalf("room required a duplicate %s catalog: %d %v", table, count, err)
		}
	}
	student := newCenterHTTPClient(studentURL, "")
	joined := student.must(t, "/api/join", M{"examId": eid, "phone": "01012345678", "name": "اسم غير معتمد"}, 200)
	if joined["session"].(map[string]any)["name"] != "طالب من السنتر" {
		t.Fatal("new center room did not use official identity")
	}
	replayed := office.must(t, "/api/center/open", payload, 200)
	if replayed["examId"] != eid {
		t.Fatal("lost response replay created another room")
	}
	assertCenterRoomCount(t, a.db, 1)
	office.must(t, "/api/exams/"+eid+"/start", M{}, 200)
	newCenterHTTPClient(office.base, "").must(t, "/api/center/open", payload, 401)
	if _, err := a.centerAdminAccess(M{"enabled": true}); err != nil {
		t.Fatal(err)
	}
	newCenterHTTPClient(office.base, a.centerAccess.token).must(t, "/api/center/open", payload, 403)
}

func TestCenterHTTPOpenFailureRollsBackRoomAndBinding(t *testing.T) {
	a, office, _ := centerHTTPServers(t)
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	payload := centerOpenPayload(templateID)
	payload["maxScore"] = float64(9)
	office.must(t, "/api/center/open", payload, 409)
	assertCenterRoomCount(t, a.db, 0)
	payload["maxScore"] = float64(10)
	payload["templateId"] = "missing-template"
	office.must(t, "/api/center/open", payload, 404)
	assertCenterRoomCount(t, a.db, 0)
	payload["templateId"] = templateID
	if _, err := a.db.sql.Exec(`CREATE TRIGGER reject_center_roster BEFORE INSERT ON center_roster BEGIN SELECT RAISE(ABORT,'synthetic roster disk failure'); END`); err != nil {
		t.Fatal(err)
	}
	office.must(t, "/api/center/open", payload, 500)
	assertCenterRoomCount(t, a.db, 0)
	if _, err := a.db.sql.Exec(`DROP TRIGGER reject_center_roster`); err != nil {
		t.Fatal(err)
	}
	office.must(t, "/api/center/open", payload, 200)
	assertCenterRoomCount(t, a.db, 1)
}

func TestCenterHTTPOpenHonorsNetworkMaintenanceAndLiveRoomPolicy(t *testing.T) {
	a, office, _ := centerHTTPServers(t)
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	payload := centerOpenPayload(templateID)
	lease := office.must(t, "/api/network/maintenance/acquire", M{}, 200)
	office.must(t, "/api/center/open", payload, 409)
	assertCenterRoomCount(t, a.db, 0)
	office.must(t, "/api/network/maintenance/release", M{"leaseToken": lease["leaseToken"]}, 200)
	created := office.must(t, "/api/center/open", payload, 200)
	payload["sessionId"] = "other-center-session"
	office.must(t, "/api/center/open", payload, 409)
	assertCenterRoomCount(t, a.db, 1)
	office.must(t, "/api/settings/rooms", M{"multipleRooms": true}, 200)
	second := office.must(t, "/api/center/open", payload, 200)
	if created["examId"] == second["examId"] {
		t.Fatal("different center lessons reused a room")
	}
	assertCenterRoomCount(t, a.db, 2)
	payload["templateId"] = str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	office.must(t, "/api/center/open", payload, 409)
	assertCenterRoomCount(t, a.db, 2)
}
