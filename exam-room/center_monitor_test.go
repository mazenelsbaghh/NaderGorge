package main

import "testing"

func TestCenterMonitorRevisionRetainsIdentityAndDoesNotExposeAnswers(t *testing.T) {
	d, eid, _ := centerTestRoom(t)
	read := func() M {
		t.Helper()
		state, err := d.centerState()
		if err != nil {
			t.Fatal(err)
		}
		return state["monitors"].([]M)[0]
	}
	empty := read()
	if len(empty["attempts"].([]M)) != 0 {
		t.Fatal("unjoined student appeared as an entrant")
	}
	joined, _, err := d.join(M{"examId": eid, "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	entered := read()
	if entered["version"].(int64) <= empty["version"].(int64) {
		t.Fatal("join did not advance observation")
	}
	rows := entered["attempts"].([]M)
	if len(rows) != 1 || len(rows[0]) != 3 || rows[0]["studentId"] != "student-1" || rows[0]["attemptId"] != joined["session"].(M)["id"] || rows[0]["status"] != "not_submitted" {
		t.Fatalf("unexpected entry projection: %v", rows)
	}
	if read()["version"] != entered["version"] {
		t.Fatal("unchanged polling advanced observation")
	}
	path := d.path
	if err = d.sql.Close(); err != nil {
		t.Fatal(err)
	}
	d, err = openDB(path)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { d.sql.Close() })
	if read()["version"] != entered["version"] {
		t.Fatal("schema reopening reset observation")
	}
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	if read()["roomState"] != "running" || read()["version"].(int64) <= entered["version"].(int64) {
		t.Fatal("room transition was not observed")
	}
}
