package main

import (
	"path/filepath"
	"testing"
)

func TestMultipleRoomsRequiresChoiceAndKeepsExistingAttempt(t *testing.T) {
	path := filepath.Join(t.TempDir(), "exam.sqlite3")
	d, err := openDB(path)
	if err != nil {
		t.Fatal(err)
	}
	defer func() { d.sql.Close() }()
	makeExam := func(title string) string {
		cfg := demoExam()
		cfg["title"] = title
		v, err := d.createExam(cfg)
		if err != nil {
			t.Fatal(err)
		}
		return str(v["id"])
	}
	first := makeExam("قاعة أولى")
	second := makeExam("قاعة ثانية")
	if _, err = d.changeState(first, "publish"); err != nil {
		t.Fatal(err)
	}
	lounge, err := d.lounge()
	if err != nil || lounge["exam"] == nil || len(lounge["rooms"].([]M)) != 1 {
		t.Fatal("single-room entry", err)
	}
	joined, tok, err := d.join(M{"code": "FIRST", "name": "طالب أول", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	aid := str(joined["session"].(M)["id"])
	if _, err = d.changeState(first, "start"); err != nil {
		t.Fatal(err)
	}
	view, err := d.session(tok)
	if err != nil {
		t.Fatal(err)
	}
	qs := view["session"].(M)["questions"].([]M)
	if _, err = d.saveAnswers(tok, M{"revision": float64(1), "submit": false, "answers": M{str(qs[0]["id"]): float64(1)}}); err != nil {
		t.Fatal(err)
	}
	before, err := attempt(d.sql, aid)
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(second, "publish"); err == nil {
		t.Fatal("default permitted second room")
	}
	if _, err = d.saveRoomSettings(M{"multipleRooms": true}); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(second, "publish"); err != nil {
		t.Fatal(err)
	}
	lounge, err = d.lounge()
	if err != nil || lounge["exam"] != nil || len(lounge["rooms"].([]M)) != 2 {
		t.Fatal("multiple room lounge", err)
	}
	if err = d.sql.Close(); err != nil {
		t.Fatal(err)
	}
	d, err = openDB(path)
	if err != nil {
		t.Fatal("restart with two rooms", err)
	}
	setting, err := d.roomSettings()
	if err != nil || setting["multipleRooms"] != true {
		t.Fatal("lost setting", err)
	}
	if _, err = d.saveRoomSettings(M{"multipleRooms": false}); err == nil {
		t.Fatal("disabled multiple live rooms")
	}
	p := M{"code": "SECOND", "name": "طالب ثاني", "phone": "01012345679"}
	if _, _, err = d.join(p, ""); err == nil {
		t.Fatal("auto-selected among two rooms")
	}
	p["examId"] = "missing"
	if _, _, err = d.join(p, ""); err == nil {
		t.Fatal("accepted invalid room")
	}
	p["examId"] = second
	joined, secondTok, err := d.join(p, "")
	if err != nil || joined["session"].(M)["examId"] != second {
		t.Fatal("selected room", err)
	}
	if _, _, err = d.join(M{"examId": second, "code": "OTHER", "name": "طالب آخر", "phone": "01012345670"}, tok); err == nil {
		t.Fatal("moved existing session")
	}
	if _, _, err = d.join(M{"examId": second, "code": "FIRST", "name": "طالب أول", "phone": "01012345678"}, ""); err == nil {
		t.Fatal("parallel code in another room")
	}
	after, err := attempt(d.sql, aid)
	if err != nil || before.Answers != after.Answers || before.Deadline != after.Deadline || before.Revision != after.Revision {
		t.Fatal("changed original answers", err)
	}
	if _, err = d.changeState(second, "start"); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(first, "close"); err != nil {
		t.Fatal(err)
	}
	secondView, err := d.session(secondTok)
	if err != nil || secondView["session"].(M)["state"] != "running" {
		t.Fatal("closing first affected second", err)
	}
	lounge, err = d.lounge()
	if err != nil || lounge["exam"].(M)["id"] != second {
		t.Fatal("did not return to direct entry", err)
	}
	if _, err = d.saveRoomSettings(M{"multipleRooms": false}); err != nil {
		t.Fatal(err)
	}
	third := makeExam("ثالثة")
	if _, err = d.changeState(third, "publish"); err == nil {
		t.Fatal("single room not enforced")
	}
}
