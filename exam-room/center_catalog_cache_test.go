package main

import (
	"database/sql"
	"strings"
	"testing"
)

func testCatalogContext() M {
	return M{"groupId": "group-1", "sessionId": "actual-session-1", "activityId": "named-exam-1", "groupLabel": "مجموعة القاهرة", "sessionLabel": "الشهر الثاني · الحصة الأولى", "activityName": "الامتحان الثالث", "maxScore": float64(10), "allowedMaxScores": []any{float64(10)}, "roster": []any{M{"private": "not cached"}}, "payments": "never cached"}
}

func TestAcademicCatalogCachesOnlyDestinationsAndOpensOfflineWithoutStudentRoster(t *testing.T) {
	a, office, studentURL := centerHTTPServers(t)
	a.centerManualEntry = true
	office.must(t, "/api/center/admin-access", M{"enabled": true}, 200)
	browser := newCenterHTTPClient(office.base, a.centerAccess.token)
	browser.must(t, "/api/center/catalog", M{"hostId": "host", "contexts": []any{testCatalogContext()}}, 403)
	office.must(t, "/api/center/catalog", M{"hostId": "host", "contexts": []any{testCatalogContext()}, "private": "never cached"}, 200)
	catalog := browser.must(t, "/api/room/cached-catalog", nil, 200)
	context := catalog["contexts"].([]any)[0].(map[string]any)
	if context["roster"] != nil || context["payments"] != nil || catalog["private"] != nil || num(catalog["updatedAt"]) <= 0 {
		t.Fatal("academic cache leaked extra fields or lost actual sync time")
	}
	reopened, err := openDB(a.db.path)
	if err != nil {
		t.Fatal(err)
	}
	defer reopened.sql.Close()
	saved, err := cachedCenterCatalog(reopened.sql)
	if err != nil || saved["hostId"] != "host" || len(saved["contexts"].([]any)) != 1 {
		t.Fatal("offline cache did not survive engine restart")
	}
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	request := M{"standalone": true, "templateId": templateID, "requestId": "cached-room-request", "cachedContextId": context["id"], "groupId": "forged"}
	eid := str(browser.must(t, "/api/room/open", request, 200)["examId"])
	newCenterHTTPClient(studentURL, "").must(t, "/api/join", M{"examId": eid, "code": "99888", "name": "طالب بلا كشف مسبق", "phone": "01012345678"}, 200)
	if a.db.count(`SELECT COUNT(*) FROM center_roster`) != 0 || a.db.count(`SELECT COUNT(*) FROM center_bindings`) != 0 {
		t.Fatal("catalog destination accidentally required a student roster")
	}
	// A later catalog refresh cannot move an exam that has already opened.
	changed := testCatalogContext()
	changed["sessionId"] = "different-session"
	office.must(t, "/api/center/catalog", M{"hostId": "other-host", "contexts": []any{changed}}, 200)
	dashboard := browser.must(t, "/api/exams/"+eid, nil, 200)
	roomTarget := dashboard["centerTarget"].(map[string]any)
	if roomTarget["sessionId"] != "actual-session-1" || roomTarget["groupLabel"] != "مجموعة القاهرة" || roomTarget["roster"] != nil || roomTarget["payments"] != nil {
		t.Fatal("dashboard did not preserve the original room destination labels privately")
	}
	if browser.must(t, "/api/room/open", request, 200)["examId"] != eid {
		t.Fatal("idempotent opening depended on latest cache")
	}
	transferred := office.must(t, "/api/center/standalone-results", M{"examId": eid}, 200)
	target := transferred["target"].(map[string]any)
	if target["hostId"] != "host" || target["groupId"] != "group-1" || target["sessionId"] != "actual-session-1" || target["activityId"] != "named-exam-1" || num(target["maxScore"]) != 10 {
		t.Fatalf("room target was changed by caller/cache: %v", target)
	}
	request["cachedContextId"] = "different-reference"
	browser.must(t, "/api/room/open", request, 409)
}

func TestAcademicCatalogRejectsInvalidUpdatesWithoutDestroyingLastGoodCache(t *testing.T) {
	a, office, _ := centerHTTPServers(t)
	a.centerManualEntry = true
	office.must(t, "/api/center/catalog", M{"hostId": "host", "contexts": []any{testCatalogContext()}}, 200)
	for _, change := range []func(M){
		func(context M) { context["maxScore"] = float64(-1) },
		func(context M) { context["allowedMaxScores"] = []any{float64(0)} },
		func(context M) { context["groupLabel"] = strings.Repeat("x", 251) },
	} {
		context := testCatalogContext()
		change(context)
		office.must(t, "/api/center/catalog", M{"hostId": "host", "contexts": []any{context}}, 400)
	}
	office.must(t, "/api/center/catalog", M{"hostId": "host", "contexts": []any{testCatalogContext(), testCatalogContext()}}, 409)
	catalog := office.must(t, "/api/room/cached-catalog", nil, 200)
	context := catalog["contexts"].([]any)[0].(map[string]any)
	if num(context["maxScore"]) != 10 || context["groupLabel"] != "مجموعة القاهرة" {
		t.Fatal("invalid refresh overwrote last valid offline catalog")
	}
	template := demoExam()
	template["questions"].([]any)[0].(map[string]any)["points"] = float64(8)
	templateID := str(office.must(t, "/api/templates", template, 200)["id"])
	office.must(t, "/api/room/open", M{"standalone": true, "templateId": templateID, "requestId": "wrong-maximum", "cachedContextId": context["id"]}, 409)
	if a.db.count(`SELECT COUNT(*) FROM exams`) != 0 {
		t.Fatal("mismatched cached destination left an open room")
	}
	office.must(t, "/api/room/open", M{"standalone": true, "templateId": templateID, "requestId": "unknown-cache", "cachedContextId": "missing"}, 409)
}

func TestCatalogRoomPagesStayBoundedWhenCachedDestinationLabelsAreLong(t *testing.T) {
	a, office, _ := centerHTTPServers(t)
	templateID := str(office.must(t, "/api/templates", demoExam(), 200)["id"])
	var config string
	if err := a.db.sql.QueryRow(`SELECT config FROM exam_templates WHERE id=?`, templateID).Scan(&config); err != nil {
		t.Fatal(err)
	}
	target := M{"hostId": "host", "groupId": "group", "sessionId": "session", "activityId": "activity", "maxScore": 10, "groupLabel": strings.Repeat("&", 250), "sessionLabel": strings.Repeat("&", 250), "activityName": strings.Repeat("&", 250)}
	err := a.db.tx(func(q *sql.Tx) error {
		for i := 0; i < 100; i++ {
			eid := id()
			if _, err := q.Exec(`INSERT INTO exams(id,config,state,created_at,template_id) VALUES(?,?,'closed',?,?)`, eid, config, now(), templateID); err != nil {
				return err
			}
			if _, err := q.Exec(`INSERT INTO center_standalone_rooms(exam_id,request_id) VALUES(?,?)`, eid, eid); err != nil {
				return err
			}
			if _, err := q.Exec(`INSERT INTO center_standalone_targets(exam_id,payload) VALUES(?,?)`, eid, encode(target)); err != nil {
				return err
			}
		}
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
	seen := map[string]bool{}
	request := M{}
	pages := 0
	for {
		page := office.must(t, "/api/center/standalone-rooms", request, 200)
		if len(encode(page)) > 256*1024 {
			t.Fatal("cached targets made the room catalog page exceed transfer bound")
		}
		for _, raw := range page["rooms"].([]any) {
			eid := str(raw.(map[string]any)["examId"])
			if seen[eid] {
				t.Fatal("pagination duplicated a room")
			}
			seen[eid] = true
		}
		pages++
		if page["hasMore"] == false {
			break
		}
		request = M{"afterExamId": page["nextCursor"]}
		if pages > 100 {
			t.Fatal("catalog cursor did not advance")
		}
	}
	if len(seen) != 100 || pages < 2 {
		t.Fatal("bounded catalog paging omitted rooms")
	}
}
