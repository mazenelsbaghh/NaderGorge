package main

import "testing"

func TestPreparedContextOpensExistingTemplateWithSavedRosterAndDestination(t *testing.T) {
	a, office, studentURL := centerHTTPServers(t)
	a.centerManualEntry = true
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	context := centerOpenPayload(templateID)
	context["groupLabel"], context["sessionLabel"], context["activityName"] = "المجموعة", "الحصة الأولى", "الامتحان الثالث"
	prepared := office.must(t, "/api/center/context", context, 200)
	office.must(t, "/api/center/offline-access", M{"staffId": "staff-1", "contextId": prepared["contextId"]}, 200)
	assertCenterRoomCount(t, a.db, 0)
	access, err := a.centerAdminAccess(M{"enabled": true})
	if err != nil {
		t.Fatal(err)
	}
	browser := newCenterHTTPClient(office.base, a.centerAccess.token)
	if str(access["adminUrl"]) == "" {
		t.Fatal("missing browser access")
	}
	choices := browser.must(t, "/api/room/center-choices", nil, 200)["contexts"].([]any)
	choice := choices[0].(map[string]any)
	if len(choices) != 1 || choice["groupLabel"] != "المجموعة" || choice["roster"] != nil {
		t.Fatalf("invalid choices: %v", choices)
	}
	browser.must(t, "/api/center/context", context, 403)
	forged := M{"contextId": prepared["contextId"], "templateId": templateID, "groupId": "forged", "sessionId": "forged", "activityId": "forged", "roster": []any{}}
	room := browser.must(t, "/api/room/open", forged, 200)
	eid := str(room["examId"])
	dashboard := browser.must(t, "/api/exams/"+eid, nil, 200)
	target := dashboard["centerTarget"].(map[string]any)
	if target["groupLabel"] != "المجموعة" || target["sessionLabel"] != "الحصة الأولى" || target["activityName"] != "الامتحان الثالث" || target["roster"] != nil {
		t.Fatalf("prepared room dashboard lost academic labels or exposed its roster: %v", target)
	}
	binding, err := readCenterBinding(a.db.sql, eid)
	if err != nil || binding.GroupID != "center-group" || binding.SessionID != "center-session" || binding.ActivityID != "center-exam" {
		t.Fatalf("destination changed: %+v %v", binding, err)
	}
	student := newCenterHTTPClient(studentURL, "")
	joined := student.must(t, "/api/join", M{"examId": eid, "code": "00123", "phone": "01112345678", "name": "اسم كتبه الطالب"}, 200)
	if joined["session"].(map[string]any)["name"] != "اسم كتبه الطالب" {
		t.Fatal("old three-field entry lost")
	}
	student.must(t, "/api/room/center-choices", nil, 404)
	replay := browser.must(t, "/api/room/open", forged, 200)
	if replay["examId"] != eid {
		t.Fatal("opening twice duplicated the room")
	}
	assertCenterRoomCount(t, a.db, 1)
}
