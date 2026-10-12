package main

import (
	"bytes"
	"database/sql"
	"errors"
	"os"
	"path/filepath"
	"runtime"
	"testing"
	"testing/synctest"
	"time"
)

func TestBackupReadsCommittedSnapshotWithoutWaitingForLiveTransaction(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "قاعة #1's exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	live, err := d.sql.Begin()
	if err != nil {
		t.Fatal(err)
	}
	defer live.Rollback()
	if _, err = live.Exec(`UPDATE exams SET state='waiting' WHERE id=?`, created["id"]); err != nil {
		t.Fatal(err)
	}
	type backupResult struct {
		path string
		err  error
	}
	finished := make(chan backupResult, 1)
	go func() {
		path, err := d.backup()
		finished <- backupResult{path, err}
	}()
	var snapshot backupResult
	select {
	case snapshot = <-finished:
	case <-time.After(2 * time.Second):
		live.Rollback()
		<-finished
		t.Fatal("backup and student writes share the sole SQLite connection; a full snapshot stalls every poll/save")
	}
	if snapshot.err != nil {
		t.Fatal(snapshot.err)
	}
	saved, err := sql.Open("sqlite", snapshot.path)
	if err != nil {
		t.Fatal(err)
	}
	defer saved.Close()
	var state string
	if err = saved.QueryRow(`SELECT state FROM exams WHERE id=?`, created["id"]).Scan(&state); err != nil {
		t.Fatal(err)
	}
	if state != "draft" {
		t.Fatalf("backup included an uncommitted live transaction: %s", state)
	}
	var integrity string
	if err = saved.QueryRow(`PRAGMA integrity_check`).Scan(&integrity); err != nil || integrity != "ok" {
		t.Fatalf("inconsistent snapshot: %s, %v", integrity, err)
	}
	if err = live.Commit(); err != nil {
		t.Fatal("live transaction could not commit after concurrent backup", err)
	}
	if err = d.sql.QueryRow(`SELECT state FROM exams WHERE id=?`, created["id"]).Scan(&state); err != nil || state != "waiting" {
		t.Fatalf("live transaction was not retained: state %s, error %v", state, err)
	}
	if err = saved.QueryRow(`SELECT state FROM exams WHERE id=?`, created["id"]).Scan(&state); err != nil || state != "draft" {
		t.Fatalf("finished snapshot changed with later commits: state %s, error %v", state, err)
	}
	backups := d.backupList()
	if len(backups) != 1 || backups[0]["name"] != filepath.Base(snapshot.path) {
		t.Fatalf("completed backup missing from downloads: %+v", backups)
	}
	info, err := os.Stat(snapshot.path)
	if err != nil || (runtime.GOOS != "windows" && info.Mode().Perm() != 0600) {
		t.Fatal("snapshot is not private", err)
	}
}

func TestMissingBackupSourceFailsWithoutPublishingOrRecreatingDatabase(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "room.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	if err = d.sql.Close(); err != nil {
		t.Fatal(err)
	}
	if err = os.Remove(d.path); err != nil {
		t.Fatal(err)
	}
	if _, err = d.backup(); err == nil {
		t.Fatal("missing live database produced an empty successful backup")
	}
	if _, err = os.Stat(d.path); !os.IsNotExist(err) {
		t.Fatalf("backup recreated the missing source: %v", err)
	}
	entries, err := os.ReadDir(filepath.Join(filepath.Dir(d.path), "backups"))
	if err != nil || len(entries) != 0 || len(d.backupList()) != 0 {
		t.Fatalf("failed backup left a partial or downloadable snapshot: %v, %v", entries, err)
	}
}

func TestBackupFilenameCollisionNeverOverwritesCompletedSnapshot(t *testing.T) {
	// A frozen wall clock reproduces the same timestamp on separate backups
	// while exercising real SQLite snapshots and filesystem publication.
	synctest.Test(t, func(t *testing.T) {
		d, err := openDB(filepath.Join(t.TempDir(), "room.sqlite3"))
		if err != nil {
			t.Fatal(err)
		}
		defer d.sql.Close()
		created, err := d.createExam(demoExam())
		if err != nil {
			t.Fatal(err)
		}
		original, err := d.backup()
		if err != nil {
			t.Fatal(err)
		}
		before, err := os.ReadFile(original)
		if err != nil {
			t.Fatal(err)
		}
		if _, err = d.sql.Exec(`UPDATE exams SET state='waiting' WHERE id=?`, created["id"]); err != nil {
			t.Fatal(err)
		}
		if _, err = d.backup(); !errors.Is(err, os.ErrExist) {
			t.Fatalf("timestamp collision did not fail safely: %v", err)
		}
		after, err := os.ReadFile(original)
		if err != nil || !bytes.Equal(after, before) {
			t.Fatalf("completed backup was replaced: %v", err)
		}
		snapshot, err := sql.Open("sqlite", original)
		if err != nil {
			t.Fatal(err)
		}
		defer snapshot.Close()
		var state string
		if err = snapshot.QueryRow(`SELECT state FROM exams WHERE id=?`, created["id"]).Scan(&state); err != nil || state != "draft" {
			t.Fatalf("completed snapshot changed state: %s, %v", state, err)
		}
		entries, err := os.ReadDir(filepath.Dir(original))
		if err != nil || len(entries) != 1 || entries[0].Name() != filepath.Base(original) {
			t.Fatalf("colliding backup left a staging file or changed backup visibility: %v, %v", entries, err)
		}
	})
}
