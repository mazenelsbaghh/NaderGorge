package main

import (
	"database/sql"
	"errors"
	"os"
	"path/filepath"
	"runtime"
)

// The lease survives an app restart. It covers the bounded native authorization
// and installer, but cannot leave room opening disabled after a crashed caller.
const networkMaintenanceSeconds = 600
const networkMaintenanceSchema = `CREATE TABLE IF NOT EXISTS network_maintenance(id INTEGER PRIMARY KEY CHECK(id=1),token_hash TEXT NOT NULL,expires_at REAL NOT NULL)`

var networkInstallerLockPath = defaultNetworkInstallerLockPath()

func defaultNetworkInstallerLockPath() string {
	if runtime.GOOS != "windows" {
		return "/var/run/massar-exam-network-setup.lock"
	}
	// ProgramData is a system-owned location shared with the elevated network helper.
	// An unavailable location is kept inaccessible, rather than inferring no installer exists.
	root := os.Getenv("ProgramData")
	if root == "" || !filepath.IsAbs(root) {
		return ""
	}
	return filepath.Join(root, "Massar", "ExamNetwork", "setup.lock")
}

func allowRoomDuringNetworkMaintenance(q Q) error {
	if networkInstallerLockPath == "" {
		return fail("تعذر تحديد حالة تجهيز شبكة الجهاز؛ راجع التهيئة قبل فتح القاعات", 409)
	}
	// An elevated installer can outlive the desktop process or its lease. A
	// leftover lock is deliberately cleared only by installer completion/reboot.
	if _, err := os.Lstat(networkInstallerLockPath); err == nil || !errors.Is(err, os.ErrNotExist) {
		message := "تهيئة الشبكة لم تنتهِ بأمان. انتظر اكتمالها، أو أعد تشغيل الماك قبل فتح القاعات"
		if runtime.GOOS == "windows" {
			message = "تهيئة الشبكة لم تنتهِ بأمان. افحص سجل التهيئة واستعادة الإعداد قبل فتح القاعات"
		}
		return fail(message, 409)
	}
	var active int
	if err := q.QueryRow(`SELECT COUNT(*) FROM network_maintenance WHERE expires_at>?`, now()).Scan(&active); err != nil {
		return err
	}
	if active != 0 {
		return fail("تهيئة شبكة الامتحان جارية. انتظر اكتمالها قبل فتح أو بدء القاعة", 409)
	}
	return nil
}

func (d *DB) acquireNetworkMaintenance() (M, error) {
	leaseToken, expiresAt := token(), now()+networkMaintenanceSeconds
	err := d.tx(func(q *sql.Tx) error {
		if err := allowRoomDuringNetworkMaintenance(q); err != nil {
			return err
		}
		var active int
		// Unknown states also fail closed: never infer that an unrecognized room
		// is safe to interrupt.
		if err := q.QueryRow(`SELECT COUNT(*) FROM exams WHERE state NOT IN ('draft','closed')`).Scan(&active); err != nil {
			return err
		}
		if active != 0 {
			return fail("اقفل القاعات المفتوحة بعد انتهاء الطلاب قبل تحديث الشبكة؛ التحديث يعيد تشغيل الاتصال", 409)
		}
		_, err := q.Exec(`INSERT INTO network_maintenance(id,token_hash,expires_at) VALUES(1,?,?) ON CONFLICT(id) DO UPDATE SET token_hash=excluded.token_hash,expires_at=excluded.expires_at`, hash(leaseToken), expiresAt)
		return err
	})
	return M{"leaseToken": leaseToken, "expiresAt": expiresAt}, err
}

func (d *DB) releaseNetworkMaintenance(leaseToken string) (M, error) {
	if leaseToken == "" {
		return nil, fail("تصريح تهيئة الشبكة غير موجود", 400)
	}
	// A delayed release from an expired installer must not release a new lease.
	_, err := d.sql.Exec(`DELETE FROM network_maintenance WHERE token_hash=?`, hash(leaseToken))
	return M{"released": true}, err
}
