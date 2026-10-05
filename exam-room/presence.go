package main

import (
	"database/sql"
	"errors"
)

const presenceSchema = `CREATE TABLE IF NOT EXISTS attempt_presence(attempt_id TEXT PRIMARY KEY REFERENCES attempts(id),last_visible REAL NOT NULL,hidden_since REAL,paused INTEGER NOT NULL DEFAULT 0,departures INTEGER NOT NULL DEFAULT 0)`

func attachPresence(q Q, a *Attempt) error {
	var paused int
	err := q.QueryRow(`SELECT paused FROM attempt_presence WHERE attempt_id=?`, a.ID).Scan(&paused)
	if errors.Is(err, sql.ErrNoRows) {
		return nil
	}
	a.Paused = paused != 0
	return err
}
func updatePresence(q Q, a Attempt, x Exam, visible bool) error {
	if a.Cancelled.Valid || !a.Joined.Valid || a.Submitted.Valid || x.State != "running" {
		return nil
	}
	t := now()
	if _, err := q.Exec(`INSERT OR IGNORE INTO attempt_presence(attempt_id,last_visible) VALUES(?,?)`, a.ID, t); err != nil {
		return err
	}
	if err := pauseIfAbsent(q, a, x, t); err != nil {
		return err
	}
	if visible {
		_, err := q.Exec(`UPDATE attempt_presence SET last_visible=?,hidden_since=NULL WHERE attempt_id=?`, t, a.ID)
		return err
	}
	_, err := q.Exec(`UPDATE attempt_presence SET hidden_since=COALESCE(hidden_since,?),departures=departures+CASE WHEN hidden_since IS NULL THEN 1 ELSE 0 END WHERE attempt_id=?`, t, a.ID)
	return err
}
func pauseIfAbsent(q Q, a Attempt, x Exam, t float64) error {
	if err := pauseScreenIssues(q, x, t, a.ID); err != nil {
		return err
	}
	seconds := num(x.Config["absenceSeconds"])
	if seconds <= 0 || x.State != "running" || a.Submitted.Valid {
		return nil
	}
	_, err := q.Exec(`UPDATE attempt_presence SET paused=1 WHERE attempt_id=? AND paused=0 AND COALESCE(hidden_since,last_visible)<=?`, a.ID, t-seconds)
	return err
}
func pauseAbsent(q Q, x Exam, t float64) error {
	if err := pauseScreenIssues(q, x, t, ""); err != nil {
		return err
	}
	seconds := num(x.Config["absenceSeconds"])
	if seconds <= 0 || x.State != "running" {
		return nil
	}
	_, err := q.Exec(`UPDATE attempt_presence SET paused=1 WHERE paused=0 AND COALESCE(hidden_since,last_visible)<=? AND attempt_id IN (SELECT id FROM attempts WHERE exam_id=? AND submitted_at IS NULL AND deadline>?)`, t-seconds, x.ID, t)
	return err
}
func (d *DB) studentPresence(tok string, visible bool, screen ...M) (M, error) {
	if tok == "" {
		return M{"session": nil}, nil
	}
	var response M
	err := d.tx(func(q *sql.Tx) error {
		a, x, err := d.auth(q, tok)
		if err != nil {
			return err
		}
		if err = updatePresence(q, a, x, visible); err != nil {
			return err
		}
		if len(screen) > 0 && screen[0] != nil {
			if err = updateScreenGuard(q, a, x, screen[0]); err != nil {
				return err
			}
		}
		if err = attachPresence(q, &a); err != nil {
			return err
		}
		if !a.LastSeen.Valid || now()-a.LastSeen.Float64 >= 5 {
			if _, err = q.Exec(`UPDATE attempts SET last_seen=? WHERE id=?`, now(), a.ID); err != nil {
				return err
			}
		}
		response = M{"session": studentView(x, a)}
		return nil
	})
	return response, err
}

func (d *DB) setPause(aid string, paused bool) (M, error) {
	err := d.tx(func(q *sql.Tx) error {
		a, err := attempt(q, aid)
		if err != nil {
			return err
		}
		x, err := exam(q, a.ExamID)
		if err != nil {
			return err
		}
		if a.Cancelled.Valid || x.State != "running" || a.Submitted.Valid || !a.Deadline.Valid || a.Deadline.Float64 <= now() {
			return fail("الطالب لا يملك محاولة جارٍ حلها", 409)
		}
		flag := 0
		if paused {
			flag = 1
		}
		_, err = q.Exec(`INSERT INTO attempt_presence(attempt_id,last_visible,paused) VALUES(?,?,?) ON CONFLICT(attempt_id) DO UPDATE SET paused=excluded.paused,last_visible=excluded.last_visible,hidden_since=NULL`, a.ID, now(), flag)
		if err != nil {
			return err
		}
		if !paused {
			if _, err = q.Exec(`UPDATE attempt_screen_guard SET issue='',issue_since=NULL WHERE attempt_id=?`, a.ID); err != nil {
				return err
			}
		}
		action := "student-resumed"
		if paused {
			action = "student-paused"
		}
		audit(q, action, aid)
		return err
	})
	if err != nil {
		return nil, err
	}
	return M{"id": aid, "paused": paused}, nil
}
func (d *DB) absenceRule(eid string, p M) (M, error) {
	seconds, err := integerField(p, "seconds", 0, 3600)
	if err != nil {
		return nil, err
	}
	if seconds > 0 && seconds < 5 {
		return nil, fail("اختر ٥ ثوانٍ على الأقل، أو صفر لإلغاء الإيقاف التلقائي", 400)
	}
	err = d.tx(func(q *sql.Tx) error {
		x, err := exam(q, eid)
		if err != nil {
			return err
		}
		if x.State != "waiting" && x.State != "running" {
			return fail("افتح القاعة أولًا", 409)
		}
		x.Config["absenceSeconds"] = float64(seconds)
		if _, err = q.Exec(`UPDATE exams SET config=? WHERE id=?`, encode(x.Config), eid); err != nil {
			return err
		}
		// Give active students a full newly chosen grace period.
		if _, err = q.Exec(`INSERT OR IGNORE INTO attempt_presence(attempt_id,last_visible) SELECT id,? FROM attempts WHERE exam_id=? AND joined_at IS NOT NULL AND submitted_at IS NULL`, now(), eid); err != nil {
			return err
		}
		if _, err = q.Exec(`UPDATE attempt_presence SET last_visible=?,hidden_since=CASE WHEN hidden_since IS NULL THEN NULL ELSE ? END WHERE paused=0 AND attempt_id IN (SELECT id FROM attempts WHERE exam_id=?)`, now(), now(), eid); err != nil {
			return err
		}
		audit(q, "absence-rule-updated", eid)
		return nil
	})
	if err != nil {
		return nil, err
	}
	return d.dashboard(eid)
}
