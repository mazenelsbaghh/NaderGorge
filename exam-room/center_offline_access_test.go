package main

import (
	"testing"
	"time"
)

func TestCenterManualEntryRetainsAllThreeFieldsAndBindsOnlyByKnownCode(t *testing.T) {
	d, eid, _ := centerTestRoom(t)
	for _, entry := range []M{
		{"code": "00123", "phone": "01012345678"},
		{"code": "00123", "name": "اسم الطالب"},
		{"code": "00999", "name": "اسم الطالب", "phone": "01012345678"},
	} {
		entry["examId"] = eid
		if _, _, err := d.joinManual(entry, ""); err == nil {
			t.Fatalf("invalid entry accepted: %v", entry)
		}
	}
	joined, _, err := d.joinManual(M{"examId": eid, "code": "٠١٢٣", "name": "اسم كتبه الطالب", "phone": "01112345678", "studentId": "forged-id"}, "")
	if err != nil {
		t.Fatal(err)
	}
	aid := str(joined["session"].(M)["id"])
	saved, err := attempt(d.sql, aid)
	if err != nil {
		t.Fatal(err)
	}
	if saved.Code != "00123" || saved.Name.String != "اسم كتبه الطالب" || saved.Phone.String != "01112345678" {
		t.Fatalf("student details lost: %+v", saved)
	}
	var owner string
	if err := d.sql.QueryRow(`SELECT student_id FROM center_attempts WHERE attempt_id=?`, aid).Scan(&owner); err != nil || owner != "student-1" {
		t.Fatalf("incorrect center identity: %s %v", owner, err)
	}
}

func TestPreparedHallAccessSurvivesEngineRestartAndExpiresWithoutHostSession(t *testing.T) {
	d, eid, _ := centerTestRoom(t)
	grant, err := d.prepareCenterOfflineAccess(M{"staffId": "staff-1", "examId": eid})
	if err != nil || grant["allowed"] != true {
		t.Fatalf("prepare: %v %v", grant, err)
	}
	var original string
	if err := d.sql.QueryRow(`SELECT file FROM pragma_database_list WHERE name='main'`).Scan(&original); err != nil {
		t.Fatal(err)
	}
	if err := d.sql.Close(); err != nil {
		t.Fatal(err)
	}
	reopened, err := openDB(original)
	if err != nil {
		t.Fatal(err)
	}
	defer reopened.sql.Close()
	grant, err = reopened.centerOfflineAccess()
	if err != nil || grant["allowed"] != true || grant["staffId"] != "staff-1" {
		t.Fatalf("restart lost lease: %v %v", grant, err)
	}
	expired := M{"staffId": "staff-1", "expiresAt": time.Now().Add(-time.Second).UTC().Format(time.RFC3339)}
	if _, err := reopened.sql.Exec(`UPDATE app_settings SET value=? WHERE key='center_offline_access'`, encode(expired)); err != nil {
		t.Fatal(err)
	}
	grant, err = reopened.centerOfflineAccess()
	if err != nil || grant["allowed"] != false {
		t.Fatalf("expired lease accepted: %v %v", grant, err)
	}
}

func TestHallPreparationRequiresBoundOpenRoomAndRevocationIsDurable(t *testing.T) {
	d, eid, _ := centerTestRoom(t)
	if _, err := d.prepareCenterOfflineAccess(M{"staffId": "staff-1", "examId": "unknown-room"}); err == nil {
		t.Fatal("unknown room prepared")
	}
	if _, err := d.prepareCenterOfflineAccess(M{"staffId": "staff-1", "examId": eid}); err != nil {
		t.Fatal(err)
	}
	if _, err := d.prepareCenterOfflineAccess(M{"revoke": true}); err != nil {
		t.Fatal(err)
	}
	snapshot, err := d.centerState()
	if err != nil || snapshot["offlineAccess"].(M)["allowed"] != false {
		t.Fatalf("revocation: %v %v", snapshot, err)
	}
}
