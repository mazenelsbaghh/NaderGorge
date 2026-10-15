package main

import (
	"database/sql"
	"sort"
)

// A durable observation revision makes lost LAN replies and delayed retries
// distinguishable from a newer room state, including after engine restart.
func captureCenterMonitor(q Q, binding centerBinding, source string, attempts []M) (M, error) {
	room, err := exam(q, binding.ExamID)
	if err != nil {
		return nil, err
	}
	entries := make([]M, 0, len(attempts))
	for _, attempt := range attempts {
		entries = append(entries, M{"attemptId": attempt["attemptId"], "studentId": attempt["studentId"], "status": attempt["status"]})
	}
	sort.Slice(entries, func(i, j int) bool { return str(entries[i]["attemptId"]) < str(entries[j]["attemptId"]) })
	snapshot := M{"sourceId": source, "examId": binding.ExamID, "groupId": binding.GroupID, "sessionId": binding.SessionID, "activityId": binding.ActivityID, "roomState": room.State, "attempts": entries}
	fingerprint := hash(encode(snapshot))
	var previous string
	var version int64
	err = q.QueryRow(`SELECT version,fingerprint FROM center_monitor_versions WHERE exam_id=?`, binding.ExamID).Scan(&version, &previous)
	if err != nil && err != sql.ErrNoRows {
		return nil, err
	}
	if previous != fingerprint {
		version++
		if _, err = q.Exec(`INSERT INTO center_monitor_versions(exam_id,version,fingerprint) VALUES(?,?,?) ON CONFLICT(exam_id) DO UPDATE SET version=excluded.version,fingerprint=excluded.fingerprint`, binding.ExamID, version, fingerprint); err != nil {
			return nil, err
		}
	}
	snapshot["version"] = version
	return snapshot, nil
}
