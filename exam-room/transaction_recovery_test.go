package main

import (
	"context"
	"database/sql"
	"path/filepath"
	"testing"
	"time"
)

func TestRecoveredTransactionPanicDoesNotBlockLaterStudentQueries(t *testing.T) {
	db, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer db.sql.Close()
	created, err := db.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	examID := str(created["id"])
	var interrupted *sql.Tx
	// Retain cleanup so a regression cannot leave the test database locked.
	defer func() {
		if interrupted != nil {
			_ = interrupted.Rollback()
		}
	}()
	func() {
		defer func() {
			if recovered := recover(); recovered != "interrupted request" {
				t.Fatalf("panic must reach the HTTP recovery boundary: %v", recovered)
			}
		}()
		_ = db.tx(func(q *sql.Tx) error {
			interrupted = q
			if _, err := q.Exec(`UPDATE exams SET state='waiting' WHERE id=?`, examID); err != nil {
				t.Fatal(err)
			}
			panic("interrupted request")
		})
	}()
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	var state string
	if err := db.sql.QueryRowContext(ctx, `SELECT state FROM exams WHERE id=?`, examID).Scan(&state); err != nil {
		t.Fatalf("recovered request retained the only database connection: %v", err)
	}
	if state != "draft" {
		t.Fatalf("interrupted request committed partial changes: %s", state)
	}
	if _, err := db.changeState(examID, "publish"); err != nil {
		t.Fatal(err)
	}
	if _, _, err := db.join(M{"code": "AFTER-PANIC", "name": "طالب اختبار", "phone": "01012345678"}, ""); err != nil {
		t.Fatalf("next student could not join: %v", err)
	}
}
