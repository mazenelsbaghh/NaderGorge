package main

import (
	"encoding/json"
	"net/http/httptest"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"testing"
)

func TestNetworkMaintenanceRejectsActiveAndUnknownRooms(t *testing.T) {
	for _, state := range []string{"draft", "waiting", "running", "closed", "unknown"} {
		t.Run(state, func(t *testing.T) {
			d, err := openDB(filepath.Join(t.TempDir(), "room.sqlite3"))
			if err != nil {
				t.Fatal(err)
			}
			defer d.sql.Close()
			created, err := d.createExam(demoExam())
			if err != nil {
				t.Fatal(err)
			}
			if _, err = d.sql.Exec(`UPDATE exams SET state=? WHERE id=?`, state, created["id"]); err != nil {
				t.Fatal(err)
			}
			_, err = d.acquireNetworkMaintenance()
			allowed := state == "draft" || state == "closed"
			if (err == nil) != allowed {
				t.Fatalf("state %s: acquire error = %v", state, err)
			}
		})
	}
}

func TestNetworkMaintenancePersistsAndOnlyOwnerCanRelease(t *testing.T) {
	path := filepath.Join(t.TempDir(), "room.sqlite3")
	d, err := openDB(path)
	if err != nil {
		t.Fatal(err)
	}
	defer func() { d.sql.Close() }()
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	lease, err := d.acquireNetworkMaintenance()
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.acquireNetworkMaintenance(); err == nil {
		t.Fatal("second installer acquired an active lease")
	}
	if err = d.sql.Close(); err != nil {
		t.Fatal(err)
	}
	d, err = openDB(path)
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.releaseNetworkMaintenance("another installer"); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(str(created["id"]), "publish"); err == nil {
		t.Fatal("restart or non-owner release allowed a room during setup")
	}
	if _, err = d.releaseNetworkMaintenance(str(lease["leaseToken"])); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(str(created["id"]), "publish"); err != nil {
		t.Fatal("owner release did not allow room opening", err)
	}
}

func TestNetworkMaintenanceExpiresWithoutOldReleaseClearingReplacement(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "room.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	old, err := d.acquireNetworkMaintenance()
	if err != nil {
		t.Fatal(err)
	}
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.sql.Exec(`UPDATE network_maintenance SET expires_at=?`, now()-1); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(str(created["id"]), "publish"); err != nil {
		t.Fatal("expired lease still blocked a room", err)
	}
	if _, err = d.changeState(str(created["id"]), "close"); err != nil {
		t.Fatal(err)
	}
	next, err := d.acquireNetworkMaintenance()
	if err != nil {
		t.Fatal(err)
	}
	if next["leaseToken"] == old["leaseToken"] {
		t.Fatal("replacement reused old lease ownership")
	}
	if _, err = d.releaseNetworkMaintenance(str(old["leaseToken"])); err != nil {
		t.Fatal(err)
	}
	if _, err = d.acquireNetworkMaintenance(); err == nil {
		t.Fatal("old delayed release cleared replacement lease")
	}
}

func TestNetworkMaintenanceAndRoomPublishAreMutuallyExclusive(t *testing.T) {
	for iteration := 0; iteration < 12; iteration++ {
		d, err := openDB(filepath.Join(t.TempDir(), "room.sqlite3"))
		if err != nil {
			t.Fatal(err)
		}
		created, err := d.createExam(demoExam())
		if err != nil {
			t.Fatal(err)
		}
		start, results := make(chan struct{}), make(chan error, 2)
		go func() { <-start; _, e := d.acquireNetworkMaintenance(); results <- e }()
		go func() { <-start; _, e := d.changeState(str(created["id"]), "publish"); results <- e }()
		close(start)
		first, second := <-results, <-results
		if (first == nil) == (second == nil) {
			t.Fatalf("wanted exactly one winner, got %v and %v", first, second)
		}
		d.sql.Close()
	}
}

func TestNetworkMaintenanceBlocksCatalogOpenAndStart(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "room.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	parent := ""
	for _, item := range []struct{ kind, key string }{{"grades", ""}, {"centers", "gradeId"}, {"groups", "centerId"}, {"lessons", "groupId"}} {
		created, err := d.createCatalogItem(item.kind, M{"name": item.kind, item.key: parent})
		if err != nil {
			t.Fatal(err)
		}
		parent = str(created["id"])
	}
	template, err := d.createTemplate(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.saveRoomSettings(M{"multipleRooms": true}); err != nil {
		t.Fatal(err)
	}
	lease, err := d.acquireNetworkMaintenance()
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.openRoom(parent, str(template["id"])); err == nil {
		t.Fatal("catalog opening bypassed maintenance when multiple rooms enabled")
	}
	// A waiting row restored externally must not bypass the start guard either.
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.sql.Exec(`UPDATE exams SET state='waiting' WHERE id=?`, created["id"]); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(str(created["id"]), "start"); err == nil {
		t.Fatal("start bypassed maintenance")
	}
	if _, err = d.releaseNetworkMaintenance(str(lease["leaseToken"])); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(str(created["id"]), "start"); err != nil {
		t.Fatal(err)
	}
}

func TestNetworkMaintenanceHTTPUsesAdminGuards(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "room.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	a := &app{db: d, adminToken: "admin-secret", studentPort: 8765, adminPort: 8766, addresses: func() []string { return nil }}
	for _, scenario := range []struct {
		name, host, token, origin string
		admin, marker             bool
		status                    int
	}{
		{"missing token", "127.0.0.1:8766", "", "", true, true, 401},
		{"missing marker", "127.0.0.1:8766", "admin-secret", "", true, false, 403},
		{"foreign origin", "127.0.0.1:8766", "admin-secret", "http://example.com", true, true, 403},
		{"LAN admin", "10.77.0.1:8766", "admin-secret", "", true, true, 403},
		{"student service", "127.0.0.1:8765", "admin-secret", "", false, true, 404},
		{"native admin", "127.0.0.1:8766", "admin-secret", "", true, true, 200},
	} {
		t.Run(scenario.name, func(t *testing.T) {
			r := httptest.NewRequest("POST", "http://"+scenario.host+"/api/network/maintenance/acquire", strings.NewReader("{}"))
			r.Header.Set("Content-Type", "application/json")
			r.Header.Set("X-Admin-Token", scenario.token)
			r.Header.Set("Origin", scenario.origin)
			if scenario.marker {
				r.Header.Set("X-Exam-Request", "1")
			}
			response := httptest.NewRecorder()
			a.handler(scenario.admin).ServeHTTP(response, r)
			if response.Code != scenario.status {
				t.Fatalf("status %d: %s", response.Code, response.Body.String())
			}
			if response.Code == 200 {
				var lease M
				if err := json.Unmarshal(response.Body.Bytes(), &lease); err != nil {
					t.Fatal(err)
				}
				if len(str(lease["leaseToken"])) < 32 || lease["expiresAt"].(float64) <= now()+180 {
					t.Fatal("lease does not cover authorization and installation")
				}
			}
		})
	}
}

func TestNetworkMaintenanceInstallerLockBlocksAfterLeaseExpires(t *testing.T) {
	previousLock := networkInstallerLockPath
	networkInstallerLockPath = filepath.Join(t.TempDir(), "installer.lock")
	t.Cleanup(func() { networkInstallerLockPath = previousLock })
	d, err := openDB(filepath.Join(t.TempDir(), "room.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	lease, err := d.acquireNetworkMaintenance()
	if err != nil {
		t.Fatal(err)
	}
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	if err = os.Mkdir(networkInstallerLockPath, 0700); err != nil {
		t.Fatal(err)
	}
	if _, err = d.sql.Exec(`UPDATE network_maintenance SET expires_at=?`, now()-1); err != nil {
		t.Fatal(err)
	}
	if _, err = d.releaseNetworkMaintenance(str(lease["leaseToken"])); err != nil {
		t.Fatal(err)
	}
	expectedRecovery := "أعد تشغيل الماك"
	if runtime.GOOS == "windows" {
		expectedRecovery = "استعادة الإعداد"
	}
	if _, err = d.changeState(str(created["id"]), "publish"); err == nil || !strings.Contains(err.Error(), expectedRecovery) {
		t.Fatal("orphaned installer allowed room opening", err)
	}
	if _, err = d.acquireNetworkMaintenance(); err == nil {
		t.Fatal("orphaned installer allowed a second installer")
	}
	if err = os.Remove(networkInstallerLockPath); err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(str(created["id"]), "publish"); err != nil {
		t.Fatal("completed installer still blocked room opening", err)
	}
}
