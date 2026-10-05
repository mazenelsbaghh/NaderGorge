package main

import (
	"archive/zip"
	"bytes"
	"io"
	"path/filepath"
	"strings"
	"testing"
)

func TestCancellationPreservesAnswersBlocksReturnAndExportsReason(t *testing.T) {
	for _, submitted := range []bool{false, true} {
		t.Run(map[bool]string{false: "running", true: "submitted"}[submitted], func(t *testing.T) {
			path := filepath.Join(t.TempDir(), "exams.sqlite3")
			d, err := openDB(path)
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
			joined, tok, err := d.join(M{"code": "CANCEL1", "name": "طالب اختبار", "phone": "01012345678"}, "")
			if err != nil {
				t.Fatal(err)
			}
			aid := str(joined["session"].(M)["id"])
			if _, err = d.changeState(eid, "start"); err != nil {
				t.Fatal(err)
			}
			current, err := d.session(tok)
			if err != nil {
				t.Fatal(err)
			}
			qs := current["session"].(M)["questions"].([]M)
			answers := M{str(qs[0]["id"]): float64(1)}
			if _, err = d.saveAnswers(tok, M{"revision": float64(1), "submit": submitted, "answers": answers}); err != nil {
				t.Fatal(err)
			}
			before, err := attempt(d.sql, aid)
			if err != nil {
				t.Fatal(err)
			}
			if _, err = d.cancelAttempt(aid, M{"reason": "  "}); err == nil {
				t.Fatal("accepted blank reason")
			}
			reason := "مخالفة تعليمات القاعة <ملاحظة>"
			if _, err = d.cancelAttempt(aid, M{"reason": reason}); err != nil {
				t.Fatal(err)
			}
			if _, err = d.cancelAttempt(aid, M{"reason": "سبب آخر"}); err == nil {
				t.Fatal("overwrote cancellation")
			}
			view, err := d.session(tok)
			if err != nil {
				t.Fatal(err)
			}
			session := view["session"].(M)
			if session["state"] != "cancelled" || session["cancelReason"] != reason || len(session["questions"].([]M)) != 0 {
				t.Fatal(session)
			}
			if _, err = d.saveAnswers(tok, M{"revision": float64(2), "submit": true, "answers": M{}}); err == nil {
				t.Fatal("saved cancelled attempt")
			}
			if _, _, err = d.join(M{"code": "CANCEL1", "name": "طالب اختبار", "phone": "01012345678"}, tok); err == nil {
				t.Fatal("reopened cancellation")
			}
			if _, err = d.resetLogin(aid); err == nil {
				t.Fatal("reset cancelled login")
			}
			if _, err = d.setPause(aid, false); err == nil {
				t.Fatal("resumed cancelled attempt")
			}
			if _, err = d.sql.Exec(`UPDATE attempts SET deadline=? WHERE id=?`, now()-1, aid); err != nil {
				t.Fatal(err)
			}
			if err = d.expire(); err != nil {
				t.Fatal(err)
			}
			after, err := attempt(d.sql, aid)
			if err != nil {
				t.Fatal(err)
			}
			if after.Answers != before.Answers || after.Grades != before.Grades || after.Revision != before.Revision || after.Submitted != before.Submitted {
				t.Fatal("cancellation mutated saved work")
			}
			if _, err = d.report(aid); err == nil {
				t.Fatal("exported cancelled result PDF")
			}
			dashboard, err := d.dashboard(eid)
			if err != nil {
				t.Fatal(err)
			}
			data, err := excelExport(dashboard, []string{"صف", "سنتر", "مجموعة", "حصة"})
			if err != nil {
				t.Fatal(err)
			}
			archive, err := zip.NewReader(bytes.NewReader(data), int64(len(data)))
			if err != nil {
				t.Fatal(err)
			}
			for _, file := range archive.File {
				if file.Name == "xl/worksheets/sheet1.xml" {
					r, err := file.Open()
					if err != nil {
						t.Fatal(err)
					}
					b, err := io.ReadAll(r)
					r.Close()
					if err != nil {
						t.Fatal(err)
					}
					sheet := string(b)
					if !strings.Contains(sheet, "سبب الإلغاء") || !strings.Contains(sheet, "ملغي") || !strings.Contains(sheet, "مخالفة تعليمات القاعة &lt;ملاحظة&gt;") || strings.Contains(sheet, `r="J5"`) || strings.Contains(sheet, `r="O5"`) {
						t.Fatal(sheet)
					}
				}
			}
			if !strings.Contains(string(csvExport(dashboard, true)), reason) {
				t.Fatal("CSV missing reason")
			}
			if err = d.sql.Close(); err != nil {
				t.Fatal(err)
			}
			reopened, err := openDB(path)
			if err != nil {
				t.Fatal(err)
			}
			defer reopened.sql.Close()
			persisted, err := attempt(reopened.sql, aid)
			if err != nil || persisted.CancelReason != reason || !persisted.Cancelled.Valid {
				t.Fatal("cancellation lost on restart", err)
			}
		})
	}
}
