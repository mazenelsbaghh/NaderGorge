package main

import (
	"database/sql"
	"errors"
)

// Presentation metadata follows the saved room destination, never the latest
// catalog or student-supplied identity. Do not expose a prepared roster here.
func dashboardCenterTarget(q Q, eid string) (M, error) {
	target, err := standaloneRoomTarget(q, eid)
	if err != nil || target != nil {
		return target, err
	}
	binding, err := readCenterBinding(q, eid)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	target = M{"groupId": binding.GroupID, "sessionId": binding.SessionID,
		"activityId": binding.ActivityID, "maxScore": binding.MaxScore}
	contextID := hash(binding.GroupID + ":" + binding.SessionID + ":" + binding.ActivityID)
	var raw string
	err = q.QueryRow(`SELECT payload FROM center_contexts WHERE context_id=?`, contextID).Scan(&raw)
	if errors.Is(err, sql.ErrNoRows) {
		return target, nil
	}
	if err != nil {
		return nil, err
	}
	saved := decode(raw)
	for _, key := range []string{"groupLabel", "sessionLabel", "activityName"} {
		if value := saved[key]; value != nil {
			target[key] = value
		}
	}
	return target, nil
}

// The private desktop endpoint prepares the roster before the cable is removed.
// Browser room creation references this saved context; it cannot supply a roster
// or change the authoritative group/session/activity destination.
func (d *DB) prepareCenterContext(p M) (M, error) {
	request := M{}
	for key, value := range p {
		request[key] = value
	}
	request["examId"] = "prepared-context"
	binding, _, err := parseCenterBinding(request)
	if err != nil {
		return nil, err
	}
	for _, field := range []string{"groupLabel", "sessionLabel", "activityName"} {
		if _, err := textField(request, field, 1, 250); err != nil {
			return nil, err
		}
	}
	contextID := hash(binding.GroupID + ":" + binding.SessionID + ":" + binding.ActivityID)
	_, err = d.sql.Exec(`INSERT INTO center_contexts(context_id,payload,prepared_at) VALUES(?,?,?) ON CONFLICT(context_id) DO UPDATE SET payload=excluded.payload,prepared_at=excluded.prepared_at`, contextID, encode(request), now())
	return M{"contextId": contextID, "prepared": err == nil}, err
}

func (d *DB) preparedCenterContext(contextID string) (M, error) {
	var raw string
	err := d.sql.QueryRow(`SELECT payload FROM center_contexts WHERE context_id=?`, contextID).Scan(&raw)
	if err == sql.ErrNoRows {
		return nil, fail("كشف الحصة غير مجهّز. وصّل الكابل وجهّز الساكند من برنامج السنتر", 409)
	}
	if err != nil {
		return nil, err
	}
	return decode(raw), nil
}

func (d *DB) centerRoomChoices() (M, error) {
	rows, err := d.sql.Query(`SELECT context_id,payload FROM center_contexts ORDER BY prepared_at DESC LIMIT 1000`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	choices := []M{}
	for rows.Next() {
		var contextID, raw string
		if err := rows.Scan(&contextID, &raw); err != nil {
			return nil, err
		}
		saved := decode(raw)
		choice := M{"id": contextID}
		for _, key := range []string{"groupId", "sessionId", "activityId", "groupLabel", "sessionLabel", "activityName", "maxScore"} {
			choice[key] = saved[key]
		}
		choices = append(choices, choice)
	}
	return M{"contexts": choices}, rows.Err()
}

func (d *DB) openPreparedCenterRoom(p M) (M, error) {
	contextID, err := textField(p, "contextId", 1, 100)
	if err != nil {
		return nil, err
	}
	saved, err := d.preparedCenterContext(contextID)
	if err != nil {
		return nil, err
	}
	saved["templateId"] = p["templateId"]
	return d.openCenterRoom(saved)
}
