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
		if strings.Contains(string(sheet), "<f>") || strings.Contains(string(sheet), `r="J2"`) {
			t.Fatal("unfinished grade or formula exported")
		}
	}
	if !foundSheet {
		t.Fatal("missing results sheet")
	}
}

func TestExcelImportContractAndPendingGradesStayBlank(t *testing.T) {
	dashboard := M{"exam": M{"id": "exam-id", "created_at": float64(1700000000), "started_at": float64(1700003600), "config": M{"title": "امتحان"}}, "attempts": []M{
		{"id": "partial-id", "name": "طالب جزئي", "phone": "01012345678", "code": "00123", "joined_at": float64(1), "submitted_at": float64(2), "score": float64(2), "maximum": float64(4), "pending": 1},
		{"id": "graded-id", "name": "طالب مكتمل", "phone": "01012345679", "code": "00124", "joined_at": float64(1), "submitted_at": float64(2), "score": float64(2.5), "maximum": float64(4), "pending": 0},
	}}
	data, err := excelExport(dashboard, []string{"صف", "سنتر", "مجموعة", "حصة"})
	if err != nil {
		t.Fatal(err)
	}
	archive, err := zip.NewReader(bytes.NewReader(data), int64(len(data)))
	if err != nil {
		t.Fatal(err)
	}
	for _, file := range archive.File {
		if file.Name != "xl/worksheets/sheet1.xml" {
			continue
		}
		reader, err := file.Open()
		if err != nil {
			t.Fatal(err)
		}
		raw, err := io.ReadAll(reader)
		reader.Close()
		if err != nil {
			t.Fatal(err)
		}
		sheet := string(raw)
		for _, expected := range []string{`topLeftCell="A2"`, `autoFilter ref="A1:S3"`, `r="S1"`, `massar-results-v1`, `partial-id`, `graded-id`, `تصحيح غير مكتمل`, `r="J3"`, `<v>2.5</v>`, `<v>0.625</v>`} {
			if !strings.Contains(sheet, expected) {
				t.Fatal("missing contract value", expected)
			}
		}
		for _, invalid := range []string{`r="J2"`, `r="L2"`, `r="O2"`, `mergeCells`} {
			if strings.Contains(sheet, invalid) {
				t.Fatal("unfinished grade or merged layout exported", invalid)
			}
		}
		return
	}
	t.Fatal("no results worksheet")
}
