package main

import (
	"path/filepath"
	"testing"
)

func sampleScreen() M {
	return M{"deviceId": "test-browser-1234", "screenWidth": float64(1200), "screenHeight": float64(800), "width": float64(1200), "height": float64(800), "fullscreen": true, "fullscreenSupported": true, "keyboard": false}
}
func TestScreenSignalsRotationKeyboardAndReduction(t *testing.T) {
	base := sampleScreen()
	cases := []struct {
		name   string
		change M
		issue  bool
	}{
		{"rotation", M{"screenWidth": float64(800), "screenHeight": float64(1200), "width": float64(800), "height": float64(1200)}, false},
		{"keyboard", M{"height": float64(350), "keyboard": true}, false},
		{"split width with keyboard", M{"width": float64(500), "keyboard": true}, true},
		{"small browser chrome change", M{"height": float64(740)}, false},
		{"half screen", M{"width": float64(600)}, true},
		{"fullscreen exit", M{"fullscreen": false}, true},
		{"browser changed", M{"deviceId": "other-browser-5678"}, true},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			r := sampleScreen()
			for k, v := range c.change {
				r[k] = v
			}
			if (screenIssue(base, r) != "") != c.issue {
				t.Fatalf("unexpected signal: %s", screenIssue(base, r))
			}
		})
	}
	unsupported := sampleScreen()
	unsupported["fullscreen"] = false
	unsupported["fullscreenSupported"] = false
	if screenIssue(unsupported, unsupported) != "" {
		t.Fatal("unsupported fullscreen treated as exit")
	}
}
func TestScreenPausePersistsAndProtectsAnswers(t *testing.T) {
	path := filepath.Join(t.TempDir(), "exam.sqlite3")
	d, err := openDB(path)
	if err != nil {
		t.Fatal(err)
	}
	defer func() { d.sql.Close() }()
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = d.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	joined, tok, err := d.join(M{"code": "SCREEN", "name": "طالب اختبار", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	aid := str(joined["session"].(M)["id"])
	if _, err = d.screenRule(eid, M{"enabled": true}); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	initial, err := d.studentPresence(tok, true, sampleScreen())
	if err != nil {
		t.Fatal(err)
	}
	deadline := initial["session"].(M)["deadline"]
	answers := M{str(initial["session"].(M)["questions"].([]M)[0]["id"]): float64(1)}
	if _, err = d.saveAnswers(tok, M{"answers": answers, "revision": float64(1), "submit": false}); err != nil {
		t.Fatal(err)
	}
	reduced := sampleScreen()
	reduced["width"] = float64(600)
	first, err := d.studentPresence(tok, true, reduced)
	if err != nil {
		t.Fatal(err)
	}
	if first["session"].(M)["paused"] != false {
		t.Fatal("paused before grace")
	}
	if _, err = d.sql.Exec(`UPDATE attempt_screen_guard SET issue_since=? WHERE attempt_id=?`, now()-6, aid); err != nil {
		t.Fatal(err)
	}
	if err = d.expire(); err != nil {
		t.Fatal(err)
	}
	paused, err := d.session(tok)
	if err != nil {
		t.Fatal(err)
	}
	if paused["session"].(M)["paused"] != true || paused["session"].(M)["deadline"] != deadline {
		t.Fatal("pause or timer incorrect")
	}
	if _, err = d.saveAnswers(tok, M{"answers": M{}, "revision": float64(2), "submit": false}); err == nil {
		t.Fatal("saved while paused")
	}
	if _, err = d.studentPresence(tok, true, sampleScreen()); err != nil {
		t.Fatal(err)
	}
	current, err := d.session(tok)
	if err != nil || current["session"].(M)["paused"] != true {
		t.Fatal("self resumed")
	}
	d.sql.Close()
	d, err = openDB(path)
	if err != nil {
		t.Fatal(err)
	}
	before, err := attempt(d.sql, aid)
	if err != nil {
		t.Fatal(err)
	}
	if before.Revision != 1 || before.Answers != encode(answers) {
		t.Fatal("answers changed")
	}
	dashboard, err := d.dashboard(eid)
	if err != nil {
		t.Fatal(err)
	}
	s := dashboard["attempts"].([]M)[0]["screen"].(M)
	if s["events"] != 1 || str(s["lastEvent"]) == "" {
		t.Fatal("lost screen evidence")
	}
	if _, err = d.setPause(aid, false); err != nil {
		t.Fatal(err)
	}
	resumed, err := d.studentPresence(tok, true, sampleScreen())
	if err != nil || resumed["session"].(M)["paused"] != false {
		t.Fatal("teacher resume failed")
	}
	if _, err = d.screenRule(eid, M{"enabled": false}); err != nil {
		t.Fatal(err)
	}
	if _, err = d.studentPresence(tok, true, reduced); err != nil {
		t.Fatal(err)
	}
	final, err := d.session(tok)
	if err != nil || final["session"].(M)["paused"] != false {
		t.Fatal("disabled rule paused")
	}
}
