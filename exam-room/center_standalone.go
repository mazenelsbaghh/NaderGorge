package main

import (
	"crypto/sha256"
	"database/sql"
	"encoding/hex"
	"encoding/json"
	"errors"
	"math"
)

const standalonePageSize = 200

func isStandaloneCenterRoom(q Q, eid string) (bool, error) {
	var present int
	err := q.QueryRow(`SELECT 1 FROM center_standalone_rooms WHERE exam_id=?`, eid).Scan(&present)
	if errors.Is(err, sql.ErrNoRows) {
		return false, nil
	}
	return err == nil, err
}

// A room is explicitly marked so an unbound legacy room never gains access by accident.
func (d *DB) openStandaloneCenterRoom(p M) (M, error) {
	templateID, err := textField(p, "templateId", 1, 100)
	if err != nil {
		return nil, err
	}
	requestID, err := textField(p, "requestId", 1, 100)
	if err != nil {
		return nil, err
	}
	eid := id()
	err = d.tx(func(q *sql.Tx) error {
		var previous, previousTemplate string
		err := q.QueryRow(`SELECT s.exam_id,e.template_id FROM center_standalone_rooms s JOIN exams e ON e.id=s.exam_id WHERE request_id=?`, requestID).Scan(&previous, &previousTemplate)
		if err == nil {
			if previousTemplate != templateID {
				return fail("طلب فتح القاعة مستخدم لامتحان آخر", 409)
			}
			target, err := standaloneRoomTarget(q, previous)
			if err != nil {
				return err
			}
			if str(target["cachedContextId"]) != str(p["cachedContextId"]) {
				return fail("طلب فتح القاعة مستخدم لحصة أخرى", 409)
			}
			eid = previous
			return nil
		}
		if !errors.Is(err, sql.ErrNoRows) {
			return err
		}
		if err = allowAnotherRoom(q); err != nil {
			return err
		}
		var raw string
		err = q.QueryRow(`SELECT config FROM exam_templates WHERE id=?`, templateID).Scan(&raw)
		if errors.Is(err, sql.ErrNoRows) {
			return fail("الامتحان غير موجود في المكتبة", 404)
		}
		if err != nil {
			return err
		}
		config, err := validateExam(decode(raw))
		if err != nil {
			return err
		}
		target, err := cachedStandaloneTarget(q, p, decode(encode(config)))
		if err != nil {
			return err
		}
		if _, err = q.Exec(`INSERT INTO exams(id,config,state,created_at,template_id) VALUES(?,?,'waiting',?,?)`, eid, encode(config), now(), templateID); err != nil {
			return err
		}
		if _, err = q.Exec(`INSERT INTO center_standalone_rooms(exam_id,request_id) VALUES(?,?)`, eid, requestID); err != nil {
			return err
		}
		if target != nil {
			if _, err = q.Exec(`INSERT INTO center_standalone_targets(exam_id,payload) VALUES(?,?)`, eid, encode(target)); err != nil {
				return err
			}
		}
		audit(q, "standalone-room-opened", eid)
		return nil
	})
	if err != nil {
		return nil, err
	}
	return M{"id": eid, "examId": eid}, nil
}

func centerSource(q Q) (string, error) {
	var source string
	err := q.QueryRow(`SELECT value FROM app_settings WHERE key='center_source_id'`).Scan(&source)
	return source, err
}

func roomMaximum(x Exam) (float64, error) {
	chosen, err := selectQuestionIDs(x.Config)
	if err != nil {
		return 0, err
	}
	maximum := num(adminAttempt(x, Attempt{QuestionIDs: encode(chosen)})["maximum"])
	if maximum <= 0 || maximum > 1000000 || math.IsNaN(maximum) || math.IsInf(maximum, 0) {
		return 0, fail("مجموع امتحان القاعة غير صالح للرصد", 409)
	}
	return maximum, nil
}

func nullableCursor(cursor string) any {
	if cursor == "" {
		return nil
	}
	return cursor
}

func pageCursor(p M, key string) (string, error) {
	if p[key] == nil {
		return "", nil
	}
	return textField(p, key, 0, 100)
}

func (d *DB) standaloneCenterRooms(p M) (M, error) {
	after, err := pageCursor(p, "afterExamId")
	if err != nil {
		return nil, err
	}
	var response M
	err = d.tx(func(q *sql.Tx) error {
		source, err := centerSource(q)
		if err != nil {
			return err
		}
		rows, err := q.Query(`SELECT e.id FROM exams e JOIN center_standalone_rooms s ON s.exam_id=e.id WHERE e.id>? ORDER BY e.id LIMIT ?`, after, standalonePageSize+1)
		if err != nil {
			return err
		}
		ids := []string{}
		for rows.Next() {
			var eid string
			if err = rows.Scan(&eid); err != nil {
				rows.Close()
				return err
			}
			ids = append(ids, eid)
		}
		err = rows.Err()
		rows.Close()
		if err != nil {
			return err
		}
		more := len(ids) > standalonePageSize
		if more {
			ids = ids[:standalonePageSize]
		}
		rooms := []M{}
		pageBytes := 0
		for _, eid := range ids {
			x, err := exam(q, eid)
			if err != nil {
				return err
			}
			maximum, err := roomMaximum(x)
			if err != nil {
				return err
			}
			var count int
			if err = q.QueryRow(`SELECT COUNT(*) FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL`, eid).Scan(&count); err != nil {
				return err
			}
			target, err := standaloneRoomTarget(q, eid)
			if err != nil {
				return err
			}
			room := M{"target": target, "examId": eid, "title": x.Config["title"], "state": x.State, "maxScore": maximum, "attemptCount": count, "createdAt": x.Created}
			encodedBytes := len(encode(room)) + 1
			if len(rooms) > 0 && pageBytes+encodedBytes > 200000 {
				more = true
				break
			}
			pageBytes += encodedBytes
			rooms = append(rooms, room)
		}
		var cursor any
		if more {
			cursor = rooms[len(rooms)-1]["examId"]
		}
		response = M{"protocol": 1, "sourceId": source, "rooms": rooms, "hasMore": more, "nextCursor": cursor}
		return nil
	})
	return response, err
}

// Hash only result-relevant state; viewing a phone or renewing its token cannot
// invalidate a transfer, but correction, cancellation and late submissions do.
func standaloneExportRevision(q Q, x Exam) (string, int, error) {
	digest := sha256.New()
	encoder := json.NewEncoder(digest)
	if err := encoder.Encode(M{"id": x.ID, "state": x.State, "config": x.ConfigText}); err != nil {
		return "", 0, err
	}
	rows, err := q.Query(`SELECT id,code,COALESCE(name,''),COALESCE(phone,''),question_ids,grades,COALESCE(submitted_at,0),COALESCE((SELECT created_at FROM attempt_cancellations WHERE attempt_id=attempts.id),0) FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL ORDER BY id`, x.ID)
	if err != nil {
		return "", 0, err
	}
	defer rows.Close()
	count := 0
	for rows.Next() {
		var aid, code, name, phone, questions, grades string
		var submitted, cancelled float64
		if err = rows.Scan(&aid, &code, &name, &phone, &questions, &grades, &submitted, &cancelled); err != nil {
			return "", 0, err
		}
		if err = encoder.Encode([]any{aid, code, name, phone, questions, grades, submitted, cancelled}); err != nil {
			return "", 0, err
		}
		count++
	}
	if err = rows.Err(); err != nil {
		return "", 0, err
	}
	return hex.EncodeToString(digest.Sum(nil)), count, nil
}

func (d *DB) standaloneCenterResults(p M) (M, error) {
	eid, err := textField(p, "examId", 1, 100)
	if err != nil {
		return nil, err
	}
	after, err := pageCursor(p, "afterAttemptId")
	if err != nil {
		return nil, err
	}
	var response M
	err = d.tx(func(q *sql.Tx) error {
		standalone, err := isStandaloneCenterRoom(q, eid)
		if err != nil {
			return err
		}
		if !standalone {
			return fail("هذه القاعة ليست امتحانًا مستقلًا", 404)
		}
		x, err := exam(q, eid)
		if err != nil {
			return err
		}
		revision, total, err := standaloneExportRevision(q, x)
		if err != nil {
			return err
		}
		expected := str(p["exportRevision"])
		if (after != "" && expected == "") || (expected != "" && expected != revision) {
			return fail("تغيّرت نتائج القاعة أثناء التحميل؛ أعد تحميل النسخة كاملة", 409)
		}
		maximum, err := roomMaximum(x)
		if err != nil {
			return err
		}
		source, err := centerSource(q)
		if err != nil {
			return err
		}
		attempts, err := standaloneAttemptPage(q, eid, after)
		if err != nil {
			return err
		}
		more := len(attempts) > standalonePageSize
		if more {
			attempts = attempts[:standalonePageSize]
		}
		exported := []M{}
		for _, a := range attempts {
			entry, err := standaloneAttemptExport(q, x, a)
			if err != nil {
				return err
			}
			exported = append(exported, entry)
		}
		var cursor any
		if more {
			cursor = attempts[len(attempts)-1].ID
		}
		target, err := standaloneRoomTarget(q, eid)
		if err != nil {
			return err
		}
		response = M{"target": target, "afterAttemptId": nullableCursor(after), "protocol": 1, "sourceId": source, "examId": eid, "title": x.Config["title"], "state": x.State, "maxScore": maximum, "attempts": exported, "hasMore": more, "nextCursor": cursor, "exportRevision": revision, "totalAttempts": total}
		return nil
	})
	return response, err
}

func standaloneAttemptPage(q Q, eid, after string) ([]Attempt, error) {
	// Answers and device credentials are unrelated to grade transfer.
	rows, err := q.Query(`SELECT id,exam_id,code,name,phone,joined_at,question_ids,grades,submitted_at,(SELECT created_at FROM attempt_cancellations WHERE attempt_id=attempts.id) FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL AND id>? ORDER BY id LIMIT ?`, eid, after, standalonePageSize+1)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	attempts := []Attempt{}
	for rows.Next() {
		var a Attempt
		if err = rows.Scan(&a.ID, &a.ExamID, &a.Code, &a.Name, &a.Phone, &a.Joined, &a.QuestionIDs, &a.Grades, &a.Submitted, &a.Cancelled); err != nil {
			return nil, err
		}
		attempts = append(attempts, a)
	}
	return attempts, rows.Err()
}

func standaloneAttemptExport(q Q, x Exam, a Attempt) (M, error) {
	summary := adminAttempt(x, a)
	status := "waiting"
	pending := 0
	var score any
	if a.Submitted.Valid {
		pending = summary["pending"].(int)
		status = "pending_grading"
		if pending == 0 {
			status = "awaiting_close"
			if x.State == "closed" {
				status = "final"
				score = summary["score"]
			}
		}
	}
	if a.Cancelled.Valid {
		status = "cancelled"
		score = nil
	}
	maximum := num(summary["maximum"])
	if maximum <= 0 || maximum > 1000000 || math.IsNaN(maximum) || math.IsInf(maximum, 0) {
		return nil, fail("مجموع محاولة الطالب غير صالح للرصد", 409)
	}
	if score != nil && (num(score) < 0 || num(score) > maximum || math.IsNaN(num(score)) || math.IsInf(num(score), 0)) {
		return nil, fail("درجة محاولة الطالب غير صالحة للرصد", 409)
	}
	entry := M{"attemptId": a.ID, "code": a.Code, "name": a.Name.String, "phone": a.Phone.String, "status": status, "score": score, "maxScore": maximum, "pending": pending}
	digest := hash(encode(entry))
	var fingerprint string
	var version int
	err := q.QueryRow(`SELECT version,fingerprint FROM center_standalone_versions WHERE attempt_id=?`, a.ID).Scan(&version, &fingerprint)
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return nil, err
	}
	if digest != fingerprint {
		version++
		if _, err = q.Exec(`INSERT INTO center_standalone_versions(attempt_id,version,fingerprint) VALUES(?,?,?) ON CONFLICT(attempt_id) DO UPDATE SET version=excluded.version,fingerprint=excluded.fingerprint`, a.ID, version, digest); err != nil {
			return nil, err
		}
	}
	entry["resultVersion"] = version
	return entry, nil
}
