package main

import (
	"bytes"
	"encoding/csv"
	"fmt"
	"html"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"sort"
	"strings"
)

var backupPattern = regexp.MustCompile(`^exam-room-[0-9]+\.sqlite3$`)

func (d *DB) backupList() []M {
	dir := filepath.Join(filepath.Dir(d.path), "backups")
	entries, _ := os.ReadDir(dir)
	out := []M{}
	for _, entry := range entries {
		if !backupPattern.MatchString(entry.Name()) || entry.Type()&os.ModeSymlink != 0 || !entry.Type().IsRegular() {
			continue
		}
		info, e := entry.Info()
		if e != nil {
			continue
		}
		out = append(out, M{"name": entry.Name(), "bytes": info.Size(), "createdAt": float64(info.ModTime().UnixNano()) / 1e9})
	}
	sort.Slice(out, func(i, j int) bool { return num(out[i]["createdAt"]) > num(out[j]["createdAt"]) })
	return out
}
func (d *DB) backupFile(name string) (string, error) {
	if !backupPattern.MatchString(name) {
		return "", fail("النسخة المطلوبة غير موجودة", 404)
	}
	p := filepath.Join(filepath.Dir(d.path), "backups", name)
	info, e := os.Lstat(p)
	if e != nil || !info.Mode().IsRegular() {
		return "", fail("النسخة المطلوبة غير موجودة", 404)
	}
	return p, nil
}
func (d *DB) storage() (M, error) {
	path, e := filepath.Abs(d.path)
	if e != nil {
		return nil, e
	}
	stat, e := os.Stat(path)
	if e != nil {
		return nil, e
	}
	pending := int64(0)
	if s, e := os.Stat(path + "-wal"); e == nil {
		pending = s.Size()
	}
	free := diskFree(filepath.Dir(path))
	backups := d.backupList()
	var total int64
	for _, b := range backups {
		total += b["bytes"].(int64)
	}
	visible := backups
	if len(visible) > 30 {
		visible = visible[:30]
	}
	return M{"databasePath": path, "folderPath": filepath.Dir(path), "backupFolder": filepath.Join(filepath.Dir(path), "backups"), "databaseBytes": stat.Size(), "pendingBytes": pending, "freeBytes": free, "counts": M{"exams": d.count(`SELECT COUNT(*) FROM exams`), "students": d.count(`SELECT COUNT(*) FROM attempts WHERE joined_at IS NOT NULL`), "submissions": d.count(`SELECT COUNT(*) FROM attempts WHERE submitted_at IS NOT NULL`)}, "backupCount": len(backups), "backupBytes": total, "backups": visible}, nil
}
func (d *DB) check() (M, error) {
	rows, e := d.sql.Query(`PRAGMA quick_check`)
	if e != nil {
		return nil, e
	}
	defer rows.Close()
	messages := []string{}
	for rows.Next() {
		var s string
		if e = rows.Scan(&s); e != nil {
			return nil, e
		}
		messages = append(messages, s)
	}
	return M{"healthy": len(messages) == 1 && messages[0] == "ok", "messages": messages}, rows.Err()
}
func (d *DB) openFolder() (M, error) {
	command := []string{"open", filepath.Dir(d.path)}
	if runtime.GOOS == "windows" {
		command = []string{"explorer", filepath.Dir(d.path)}
	} else if runtime.GOOS == "linux" {
		command[0] = "xdg-open"
	}
	if e := exec.Command(command[0], command[1:]...).Start(); e != nil {
		return nil, fail("تعذر فتح المجلد. انسخ المسار وافتحه من مدير الملفات", 503)
	}
	return M{"opened": true}, nil
}
func csvSafe(v any) string {
	s := fmt.Sprint(v)
	if v == nil {
		s = ""
	}
	if strings.HasPrefix(s, "=") || strings.HasPrefix(s, "+") || strings.HasPrefix(s, "-") || strings.HasPrefix(s, "@") || strings.HasPrefix(s, "\t") || strings.HasPrefix(s, "\r") {
		s = "'" + s
	}
	return s
}
func csvExport(dashboard M, results bool) []byte {
	var b bytes.Buffer
	b.Write([]byte{0xef, 0xbb, 0xbf})
	w := csv.NewWriter(&b)
	attempts := dashboard["attempts"].([]M)
	if results {
		_ = w.Write([]string{"الاسم", "الموبايل", "الكود", "الدرجة المصححة", "المجموع", "متبقي للتصحيح", "الحالة", "سبب الإلغاء"})
		for _, a := range attempts {
			status := "لم يسلم"
			if a["submitted_at"] != nil {
				status = "تم التسليم"
			}
			score, pending := csvSafe(a["score"]), csvSafe(a["pending"])
			if a["cancelled_at"] != nil {
				status = "ملغي"
				score = ""
				pending = ""
			}
			_ = w.Write([]string{csvSafe(a["name"]), csvSafe(a["phone"]), csvSafe(a["code"]), score, csvSafe(a["maximum"]), pending, status, csvSafe(a["cancelReason"])})
		}
	} else {
		_ = w.Write([]string{"الكود", "الاسم", "الموبايل"})
		for _, a := range attempts {
			_ = w.Write([]string{csvSafe(a["code"]), csvSafe(a["name"]), csvSafe(a["phone"])})
		}
	}
	w.Flush()
	return b.Bytes()
}
func reportHTML(report M) []byte {
	x := report["exam"].(M)
	a := report["attempt"].(M)
	qs := report["questions"].([]M)
	esc := html.EscapeString
	answers := a["answers"].(M)
	grades := a["grades"].(M)
	var sections strings.Builder
	for i, q := range qs {
		qid := str(q["id"])
		answer := str(answers[qid])
		model := str(q["modelAnswer"])
		if q["kind"] == "mcq" {
			opts := q["options"].([]any)
			if v, ok := answers[qid].(float64); ok && int(v) >= 0 && int(v) < len(opts) {
				answer = str(opts[int(v)])
			}
			model = str(opts[intv(q["correct"])])
		}
		if answer == "" {
			answer = "لم يجب الطالب"
		}
		grade, ok := grades[qid].(map[string]any)
		if !ok {
			grade = M{"score": 0, "feedback": ""}
		}
		score := num(grade["score"])
		points := num(q["points"])
		status := "إجابة صحيحة"
		class := "correct"
		if score == 0 {
			status = "إجابة خاطئة"
			class = "wrong"
		} else if score < points {
			status = "إجابة جزئية"
			class = "partial"
		}
		feedback := strings.TrimSpace(strings.TrimPrefix(str(grade["feedback"]), "Gemini:"))
		if feedback == "" && score < points {
			feedback = "راجع الإجابة النموذجية والتصحيح الموضح أعلاه."
		}
		fmt.Fprintf(&sections, `<section class="question-report"><div class="question-top"><div><span class="question-number">السؤال %d</span><h2>%s</h2></div><div class="question-mark"><span class="result-chip %s">%s</span><strong>%s / %s</strong></div></div><div class="answer-grid"><div class="answer-box"><h3>إجابة الطالب</h3><p>%s</p></div><div class="answer-box model"><h3>الإجابة الصحيحة / النموذجية</h3><p>%s</p></div></div>%s</section>`, i+1, esc(str(q["text"])), class, status, esc(fmt.Sprint(grade["score"])), esc(fmt.Sprint(q["points"])), esc(answer), esc(model), func() string {
			if feedback == "" {
				return ""
			}
			return `<div class="grade-note"><strong>ملاحظة التصحيح</strong><p>` + esc(feedback) + `</p></div>`
		}())
	}
	title := esc(str(x["config"].(M)["title"]))
	name := esc(str(a["name"]))
	filename := strings.NewReplacer("/", "-", "\\", "-", "\"", "", "<", "", ">", "").Replace(str(a["name"]))
	return []byte(fmt.Sprintf(`<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>تقرير تصحيح %s</title><link rel="stylesheet" href="/assets/report.css"><script defer src="/assets/report.js"></script></head><body><div class="print-tools"><a href="/">← العودة للإدارة</a><div><button id="download-report" data-url="/api/attempts/%s/report.pdf" data-filename="تقرير-%s.pdf">تنزيل PDF</button><button id="print-report" class="secondary">طباعة</button><button id="send-report" class="secondary">إرسال PDF عبر واتساب</button></div><span id="download-status" role="status"></span></div><main id="report-paper"><header><div class="report-brand"><img src="/assets/logo-mark.svg" alt=""><span>مسار<small>امتحانات السنتر</small></span></div><div class="report-heading"><span>تقرير فردي</span><h1>نتيجة وتصحيح الامتحان</h1><p>%s</p></div></header><div class="identity"><div><span>اسم الطالب</span><strong>%s</strong></div><div><span>رقم الموبايل</span><strong dir="ltr">%s</strong></div><div><span>كود الطالب</span><strong dir="ltr">%s</strong></div><div class="total"><span>الدرجة النهائية</span><strong>%s <em>من</em> %s</strong></div></div><div class="report-intro"><h2>تفاصيل الإجابات</h2><p>درجة كل سؤال وإجابة الطالب مع الإجابة الصحيحة وملاحظة التصحيح.</p></div>%s<footer><strong>مسار · امتحانات السنتر</strong><span>نسخة الطالب · تم إعداد التقرير من بيانات الامتحان المحفوظة</span></footer></main></body></html>`, name, esc(str(a["id"])), esc(filename), title, name, esc(str(a["phone"])), esc(str(a["code"])), esc(fmt.Sprint(a["score"])), esc(fmt.Sprint(a["maximum"])), sections.String()))
}
