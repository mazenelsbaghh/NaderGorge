package main

import "database/sql"

const cancellationSchema = `CREATE TABLE IF NOT EXISTS attempt_cancellations(attempt_id TEXT PRIMARY KEY REFERENCES attempts(id),reason TEXT NOT NULL,created_at REAL NOT NULL)`

func (d *DB) cancelAttempt(aid string, p M) (M, error) {
	reason, err := textField(p, "reason", 1, 1000)
	if err != nil {
		return nil, fail("اكتب سبب الإلغاء من حرف إلى ١٠٠٠ حرف", 400)
	}
	err = d.tx(func(q *sql.Tx) error {
		a, err := attempt(q, aid)
		if err != nil {
			return err
		}
		if !a.Joined.Valid {
			return fail("الطالب لم يدخل الامتحان", 409)
		}
		if a.Cancelled.Valid {
			return fail("محاولة الطالب ملغاة بالفعل", 409)
		}
		_, err = q.Exec(`INSERT INTO attempt_cancellations(attempt_id,reason,created_at) VALUES(?,?,?)`, aid, reason, now())
		if err != nil {
			return err
		}
		audit(q, "attempt-cancelled", aid)
		return nil
	})
	return M{"id": aid, "cancelReason": reason}, err
}
