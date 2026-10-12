package main

import (
	"database/sql"
	"net/url"
	"path/filepath"
	"reflect"
	"runtime"
	"strings"
	"testing"
)

func TestBackupURIEncodesWindowsDriveAndNativePathsWithoutAuthority(t *testing.T) {
	for _, scenario := range []struct{ name, goos, path, expected string }{
		{"Windows drive", "windows", `C:\Users\RUNNER~1\قاعة #1's 50% & exam.sqlite3`, `/C:/Users/RUNNER~1/قاعة #1's 50% & exam.sqlite3`},
		{"Windows forward slashes", "windows", `D:/Data/exam.sqlite3`, `/D:/Data/exam.sqlite3`},
		{"Windows extended local drive", "windows", `\\?\C:\Long folder\exam.sqlite3`, `/C:/Long folder/exam.sqlite3`},
		{"Mac", "darwin", `/Users/example/قاعة #1's 50% & exam.sqlite3`, `/Users/example/قاعة #1's 50% & exam.sqlite3`},
		{"Unix backslash is filename", "linux", `/data/room\1.sqlite3`, `/data/room\1.sqlite3`},
	} {
		t.Run(scenario.name, func(t *testing.T) {
			value, err := readOnlyBackupURI(scenario.path, scenario.goos)
			if err != nil {
				t.Fatal(err)
			}
			uri, err := url.Parse(value)
			if err != nil || uri.Scheme != "file" || uri.Host != "" || uri.Opaque != "" || uri.Fragment != "" || uri.Path != scenario.expected {
				t.Fatalf("source filename became authority/fragment or changed: %s (%v)", value, err)
			}
			if uri.Query().Get("mode") != "ro" || !reflect.DeepEqual(uri.Query()["_pragma"], []string{"busy_timeout=15000", "synchronous=FULL"}) || len(uri.Query()) != 2 {
				t.Fatalf("backup read-only/snapshot settings changed: %s", value)
			}
			if scenario.goos == "windows" && !strings.HasPrefix(value, "file:///") {
				t.Fatalf("Windows drive lacks absolute URI prefix: %s", value)
			}
		})
	}
}

func TestBackupURIRejectsWindowsNetworkAndAmbiguousPaths(t *testing.T) {
	for _, path := range []string{`\\server\share\exam.sqlite3`, `\\?\UNC\server\share\exam.sqlite3`, `\\.\device\exam.sqlite3`, `C:exam.sqlite3`, `exam.sqlite3`, `/exam.sqlite3`} {
		if uri, err := readOnlyBackupURI(path, "windows"); err == nil || uri != "" {
			t.Fatalf("unsupported path silently redirected to %q: %q, %v", path, uri, err)
		}
	}
}

func TestBackupURIReadsExactNativeDatabaseAndRejectsWrites(t *testing.T) {
	path := filepath.Join(t.TempDir(), "قاعة #1's 50% & exam.sqlite3")
	db, err := sql.Open("sqlite", path)
	if err != nil {
		t.Fatal(err)
	}
	defer db.Close()
	if _, err = db.Exec(`CREATE TABLE identity(value TEXT); INSERT INTO identity VALUES('correct-source')`); err != nil {
		t.Fatal(err)
	}
	uri, err := readOnlyBackupURI(path, runtime.GOOS)
	if err != nil {
		t.Fatal(err)
	}
	source, err := sql.Open("sqlite", uri)
	if err != nil {
		t.Fatal(err)
	}
	defer source.Close()
	var value string
	if err = source.QueryRow(`SELECT value FROM identity`).Scan(&value); err != nil || value != "correct-source" {
		t.Fatalf("read wrong source: %s, %v", value, err)
	}
	if _, err = source.Exec(`UPDATE identity SET value='incorrect'`); err == nil {
		t.Fatal("backup URI allowed writing the live source")
	}
	if err = db.QueryRow(`SELECT value FROM identity`).Scan(&value); err != nil || value != "correct-source" {
		t.Fatalf("read-only source changed: %s, %v", value, err)
	}
}
