package main

import (
	"database/sql"
	"time"
)

// This lease authorizes only this Windows user's local exam console. It is not
// a center employee session and cannot authorize reception or financial writes.
func (d *DB) centerOfflineAccess() (M, error) {
	var raw string
	err := d.sql.QueryRow(`SELECT value FROM app_settings WHERE key='center_offline_access'`).Scan(&raw)
	if err == sql.ErrNoRows {
		return M{"allowed": false}, nil
	}
	if err != nil {
		return nil, err
	}
	access := decode(raw)
	expires, err := time.Parse(time.RFC3339, str(access["expiresAt"]))
	if err != nil || str(access["staffId"]) == "" || !time.Now().Before(expires) {
		return M{"allowed": false}, nil
	}
	access["allowed"] = true
	access["remainingSeconds"] = int(time.Until(expires).Seconds())
	return access, nil
}

func (d *DB) prepareCenterOfflineAccess(p M) (M, error) {
	if p["revoke"] == true {
		_, err := d.sql.Exec(`DELETE FROM app_settings WHERE key='center_offline_access'`)
		return M{"allowed": false}, err
	}
	staff, err := textField(p, "staffId", 1, 100)
	if err != nil {
		return nil, err
	}
	eid := str(p["examId"])
	contextID := str(p["contextId"])
	if contextID != "" {
		if _, err := d.preparedCenterContext(contextID); err != nil {
			return nil, err
		}
		access := M{"staffId": staff, "contextId": contextID, "expiresAt": time.Now().Add(12 * time.Hour).UTC().Format(time.RFC3339)}
		_, err := d.sql.Exec(`INSERT INTO app_settings(key,value) VALUES('center_offline_access',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value`, encode(access))
		if err != nil {
			return nil, err
		}
		return d.centerOfflineAccess()
	}
	if _, err := textField(p, "examId", 1, 100); err != nil {
		return nil, err
	}
	var access M
	err = d.tx(func(q *sql.Tx) error {
		if _, err := readCenterBinding(q, eid); err != nil {
			return err
		}
		room, err := exam(q, eid)
		if err != nil {
			return err
		}
		if room.State != "waiting" && room.State != "running" {
			return fail("افتح قاعة مرتبطة بكشف السنتر قبل تجهيز الساكند للقاعة", 409)
		}
		access = M{"staffId": staff, "examId": eid, "expiresAt": time.Now().Add(12 * time.Hour).UTC().Format(time.RFC3339)}
		_, err = q.Exec(`INSERT INTO app_settings(key,value) VALUES('center_offline_access',?) ON CONFLICT(key) DO UPDATE SET value=excluded.value`, encode(access))
		return err
	})
	if err != nil {
		return nil, err
	}
	return d.centerOfflineAccess()
}
