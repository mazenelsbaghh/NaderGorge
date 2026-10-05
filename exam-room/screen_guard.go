package main

import (
	"database/sql"
	"math"
)

const screenGuardSchema = `CREATE TABLE IF NOT EXISTS attempt_screen_guard(attempt_id TEXT PRIMARY KEY REFERENCES attempts(id),baseline TEXT NOT NULL,last_report TEXT NOT NULL,issue TEXT NOT NULL DEFAULT '',issue_since REAL,last_event TEXT NOT NULL DEFAULT '',events INTEGER NOT NULL DEFAULT 0)`

func screenReport(p M) (M, error) {
	out := M{}
	id := str(p["deviceId"])
	if len(id) < 8 || len(id) > 80 {
		return nil, fail("علامة المتصفح غير صالحة", 400)
	}
	out["deviceId"] = id
	for _, key := range []string{"screenWidth", "screenHeight", "width", "height"} {
		n, err := integerField(p, key, 100, 20000)
		if err != nil {
			return nil, err
		}
		out[key] = float64(n)
	}
	for _, key := range []string{"fullscreen", "fullscreenSupported", "keyboard"} {
		b, err := boolField(p, key)
		if err != nil {
			return nil, err
		}
		out[key] = b
	}
	return out, nil
}
func smallerDimension(a, b M, prefix string) bool {
	x, y := num(a[prefix+"Width"]), num(a[prefix+"Height"])
	u, v := num(b[prefix+"Width"]), num(b[prefix+"Height"])
	return math.Min(u, v) < math.Min(x, y)*0.75 || math.Max(u, v) < math.Max(x, y)*0.75
}
func screenIssue(base, report M) string {
	if base["deviceId"] != report["deviceId"] {
		return "تغيّرت علامة المتصفح"
	}
	if base["fullscreen"] == true && report["fullscreen"] != true {
		return "خرج من ملء الشاشة"
	}
	if smallerDimension(base, report, "screen") {
		return "انخفض مقاس الشاشة المبلّغ عنه"
	}
	if report["keyboard"] != true {
		b := M{"viewWidth": base["width"], "viewHeight": base["height"]}
		r := M{"viewWidth": report["width"], "viewHeight": report["height"]}
		if smallerDimension(b, r, "view") {
			return "انخفضت مساحة العرض بأكثر من ٢٥٪"
		}
	} else if num(report["width"]) < num(base["width"])*0.75 && num(report["width"]) < num(base["height"])*0.75 {
		return "انخفض عرض الصفحة بأكثر من ٢٥٪"
	}
	return ""
}
func updateScreenGuard(q Q, a Attempt, x Exam, p M) error {
	if x.Config["screenGuard"] != true || x.State != "running" || a.Submitted.Valid || a.Cancelled.Valid {
		return nil
	}
	report, err := screenReport(p)
	if err != nil {
		return err
	}
	var raw, previous string
	var since sql.NullFloat64
	err = q.QueryRow(`SELECT baseline,issue,issue_since FROM attempt_screen_guard WHERE attempt_id=?`, a.ID).Scan(&raw, &previous, &since)
	if err == sql.ErrNoRows {
		_, err = q.Exec(`INSERT INTO attempt_screen_guard(attempt_id,baseline,last_report) VALUES(?,?,?)`, a.ID, encode(report), encode(report))
		return err
	}
	if err != nil {
		return err
	}
	issue := screenIssue(decode(raw), report)
	t := now()
	if issue == "" {
		_, err = q.Exec(`UPDATE attempt_screen_guard SET last_report=?,issue='',issue_since=NULL WHERE attempt_id=?`, encode(report), a.ID)
		return err
	}
	if previous != issue || !since.Valid {
		_, err = q.Exec(`UPDATE attempt_screen_guard SET last_report=?,issue=?,issue_since=?,last_event=?,events=events+1 WHERE attempt_id=?`, encode(report), issue, t, issue, a.ID)
		return err
	}
	if _, err = q.Exec(`UPDATE attempt_screen_guard SET last_report=? WHERE attempt_id=?`, encode(report), a.ID); err != nil {
		return err
	}
	if t-since.Float64 >= 5 {
		_, err = q.Exec(`INSERT INTO attempt_presence(attempt_id,last_visible,paused) VALUES(?,?,1) ON CONFLICT(attempt_id) DO UPDATE SET paused=1`, a.ID, t)
	}
	return err
}
func (d *DB) screenRule(eid string, p M) (M, error) {
	enabled, err := boolField(p, "enabled")
	if err != nil {
		return nil, err
	}
	err = d.tx(func(q *sql.Tx) error {
		x, err := exam(q, eid)
		if err != nil {
			return err
		}
		if x.State != "waiting" && x.State != "running" {
			return fail("افتح القاعة أولًا", 409)
		}
		x.Config["screenGuard"] = enabled
		if _, err = q.Exec(`UPDATE exams SET config=? WHERE id=?`, encode(x.Config), eid); err != nil {
			return err
		}
		audit(q, "screen-rule-updated", eid)
		return nil
	})
	if err != nil {
		return nil, err
	}
	return d.dashboard(eid)
}

// Enforce an already reported issue even if the browser stops sending updates.
func pauseScreenIssues(q Q, x Exam, t float64, aid string) error {
	if x.Config["screenGuard"] != true || x.State != "running" {
		return nil
	}
	query := `UPDATE attempt_presence SET paused=1 WHERE paused=0 AND attempt_id IN (SELECT a.id FROM attempts a JOIN attempt_screen_guard g ON g.attempt_id=a.id WHERE a.exam_id=? AND a.submitted_at IS NULL AND a.deadline>? AND g.issue_since<=? AND g.issue!='' AND NOT EXISTS(SELECT 1 FROM attempt_cancellations c WHERE c.attempt_id=a.id))`
	args := []any{x.ID, t, t - 5}
	if aid != "" {
		query += ` AND attempt_id=?`
		args = append(args, aid)
	}
	_, err := q.Exec(query, args...)
	return err
}
