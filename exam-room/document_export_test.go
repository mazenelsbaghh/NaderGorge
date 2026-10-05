package main

import (
	"archive/zip"
	"bytes"
	"io"
	"path/filepath"
	"strings"
	"testing"
)

func TestExcelPreservesStudentIdentifiersAndIncompleteResults(t *testing.T) {
	d, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	defer d.sql.Close()
	created, err := d.createExam(demoExam())
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = d.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	if _, _, err = d.join(M{"code": "00123", "name": "=طالب & تجربة", "phone": "01012345678"}, ""); err != nil {
		t.Fatal(err)
	}
	dashboard, err := d.dashboard(eid)
	if err != nil {
		t.Fatal(err)
	}
	contents, err := excelExport(dashboard, []string{"الأول", "سنتر", "السبت", "حصة"})
	if err != nil {
		t.Fatal(err)
	}
	archive, err := zip.NewReader(bytes.NewReader(contents), int64(len(contents)))
	if err != nil {
		t.Fatal(err)
	}
	foundSheet := false
	for _, file := range archive.File {
		if file.Name != "xl/worksheets/sheet1.xml" {
			continue
		}
		foundSheet = true
		reader, err := file.Open()
		if err != nil {
			t.Fatal(err)
		}
		sheet, err := io.ReadAll(reader)
		reader.Close()
		if err != nil {
			t.Fatal(err)
		}
		for _, expected := range []string{"01012345678", "00123", "=طالب &amp; تجربة", "لم يسلّم"} {
			if !strings.Contains(string(sheet), expected) {
				t.Fatalf("missing %s", expected)
			}
		}
		if strings.Contains(string(sheet), "<f>") || strings.Contains(string(sheet), `r="J5"`) {
			t.Fatal("unfinished grade or formula exported")
		}
	}
	if !foundSheet {
		t.Fatal("missing results sheet")
	}
}
