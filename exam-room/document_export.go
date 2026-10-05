package main

import (
	"archive/zip"
	"bytes"
	"context"
	"database/sql"
	"encoding/xml"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"
	_ "time/tzdata"
)

func xmlText(v any) string {
	var out bytes.Buffer
	if v != nil {
		_ = xml.EscapeText(&out, []byte(fmt.Sprint(v)))
	}
	return out.String()
}

func excelExport(dashboard M, labels []string) ([]byte, error) {
	var sheet strings.Builder
	sheet.WriteString(`<?xml version="1.0" encoding="UTF-8"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0" rightToLeft="1"><pane ySplit="4" topLeftCell="A5" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews><cols><col min="1" max="1" width="32" customWidth="1"/><col min="2" max="3" width="18" customWidth="1"/><col min="4" max="8" width="24" customWidth="1"/><col min="9" max="9" width="18" customWidth="1"/><col min="10" max="13" width="18" customWidth="1"/><col min="14" max="16" width="40" customWidth="1"/></cols><sheetData>`)
	cell := func(col, row, style int, value any, numeric bool) {
		ref := fmt.Sprintf("%c%d", 'A'+col, row)
		if numeric {
			fmt.Fprintf(&sheet, `<c r="%s" s="%d"><v>%v</v></c>`, ref, style, value)
		} else {
			fmt.Fprintf(&sheet, `<c r="%s" s="%d" t="inlineStr"><is><t xml:space="preserve">%s</t></is></c>`, ref, style, xmlText(value))
		}
	}
	exam := dashboard["exam"].(M)
	title := str(exam["config"].(M)["title"])
	sheet.WriteString(`<row r="1" ht="32" customHeight="1">`)
	cell(0, 1, 1, "نتائج امتحان: "+title, false)
	sheet.WriteString(`</row>`)
	sheet.WriteString(`<row r="2" ht="28" customHeight="1">`)
	cell(0, 2, 0, strings.Join(labels, " · "), false)
	sheet.WriteString(`</row>`)
	headers := []string{"اسم الطالب", "رقم الموبايل", "كود الطالب", "الصف الدراسي", "السنتر", "المجموعة", "الحصة", "الامتحان", "تاريخ الجلسة", "الدرجة المصححة", "المجموع", "النسبة", "متبقي للتصحيح", "الحالة", "جاب كام من كام", "سبب الإلغاء"}
	sheet.WriteString(`<row r="4" ht="30" customHeight="1">`)
	for col, header := range headers {
		cell(col, 4, 1, header, false)
	}
	sheet.WriteString(`</row>`)
	location, err := time.LoadLocation("Africa/Cairo")
	if err != nil {
		return nil, err
	}
	_, offset := time.Unix(int64(num(exam["created_at"])), 0).In(location).Zone()
	sessionDate := (num(exam["created_at"])+float64(offset))/86400 + 25569
	row := 4
	for _, attempt := range dashboard["attempts"].([]M) {
		if attempt["joined_at"] == nil {
			continue
		}
		row++
		status := "لم يسلّم"
		if attempt["submitted_at"] != nil {
			status = "تم التصحيح"
			if attempt["pending"].(int) > 0 {
				status = "تصحيح غير مكتمل"
			}
		}
		cancelled := attempt["cancelled_at"] != nil
		if cancelled {
			status = "ملغي"
		}
		fmt.Fprintf(&sheet, `<row r="%d">`, row)
		values := []any{attempt["name"], attempt["phone"], attempt["code"], labels[0], labels[1], labels[2], labels[3], title}
		for col, value := range values {
			cell(col, row, 0, value, false)
		}
		cell(8, row, 2, sessionDate, true)
		if !cancelled && attempt["submitted_at"] != nil {
			cell(9, row, 3, attempt["score"], true)
			maximum := attempt["maximum"].(float64)
			if maximum > 0 {
				cell(11, row, 4, attempt["score"].(float64)/maximum, true)
			}
		}
		cell(10, row, 3, attempt["maximum"], true)
		if !cancelled && attempt["pending"] != nil {
			cell(12, row, 3, attempt["pending"], true)
		}
		cell(13, row, 0, status, false)
		if !cancelled && attempt["submitted_at"] != nil {
			cell(14, row, 0, fmt.Sprintf("%v من %v", attempt["score"], attempt["maximum"]), false)
		}
		cell(15, row, 0, attempt["cancelReason"], false)
		sheet.WriteString(`</row>`)
	}
	fmt.Fprintf(&sheet, `</sheetData><autoFilter ref="A4:P%d"/><mergeCells count="2"><mergeCell ref="A1:P1"/><mergeCell ref="A2:P2"/></mergeCells></worksheet>`, row)
	files := map[string]string{
		"[Content_Types].xml":        `<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>`,
		"_rels/.rels":                `<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>`,
		"xl/workbook.xml":            `<?xml version="1.0"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="نتائج الامتحان" sheetId="1" r:id="rId1"/></sheets></workbook>`,
		"xl/_rels/workbook.xml.rels": `<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>`,
		"xl/styles.xml":              `<?xml version="1.0"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="2"><numFmt numFmtId="164" formatCode="yyyy-mm-dd hh:mm"/><numFmt numFmtId="165" formatCode="0.##"/></numFmts><fonts count="2"><font><sz val="12"/><name val="Arial"/></font><font><b/><sz val="12"/><color rgb="FFFFFFFF"/><name val="Arial"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF102A43"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="center" horizontal="right" readingOrder="2" wrapText="1"/></xf><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="center" horizontal="right" readingOrder="2" wrapText="1"/></xf><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="10" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>`,
		"xl/worksheets/sheet1.xml":   sheet.String(),
	}
	var output bytes.Buffer
	archive := zip.NewWriter(&output)
	for name, contents := range files {
		file, err := archive.Create(name)
		if err != nil {
			return nil, err
		}
		if _, err = file.Write([]byte(contents)); err != nil {
			return nil, err
		}
	}
	if err := archive.Close(); err != nil {
		return nil, err
	}
	return output.Bytes(), nil
}

func (d *DB) sheetContext(examID, groupID string) ([]string, error) {
	if groupID == "" {
		return nil, fail("اختر المجموعة", 400)
	}
	labels := make([]string, 4)
	err := d.sql.QueryRow(`SELECT gr.name,c.name,g.name,l.name FROM exams e JOIN lessons l ON l.id=e.lesson_id JOIN study_groups g ON g.id=l.group_id JOIN centers c ON c.id=g.center_id JOIN school_grades gr ON gr.id=c.grade_id WHERE e.id=? AND g.id=?`, examID, groupID).Scan(&labels[0], &labels[1], &labels[2], &labels[3])
	if err == sql.ErrNoRows {
		return nil, fail("الامتحان غير مرتبط بالمجموعة المختارة", 404)
	}
	return labels, err
}

func (a *app) reportPDF(report M) ([]byte, error) {
	if a.pdfHelper == "" {
		return nil, fail("مولّد PDF غير موجود. ثبّت أحدث نسخة من البرنامج", 503)
	}
	dir, err := os.MkdirTemp("", "massar-report-")
	if err != nil {
		return nil, err
	}
	defer os.RemoveAll(dir)
	input, output := filepath.Join(dir, "report.json"), filepath.Join(dir, "report.pdf")
	if err = os.WriteFile(input, []byte(encode(report)), 0600); err != nil {
		return nil, err
	}
	ctx, cancel := context.WithTimeout(context.Background(), 60*time.Second)
	defer cancel()
	command := exec.CommandContext(ctx, a.pdfHelper, input, output)
	if err = command.Run(); err != nil {
		return nil, fmt.Errorf("PDF renderer: %w", err)
	}
	return os.ReadFile(output)
}

func (a *app) reportsZIP(eid string) ([]byte, error) {
	dashboard, err := a.db.dashboard(eid)
	if err != nil {
		return nil, err
	}
	reports := []M{}
	for _, attempt := range dashboard["attempts"].([]M) {
		if attempt["cancelled_at"] != nil || attempt["submitted_at"] == nil || attempt["pending"] != 0 {
			continue
		}
		report, err := a.db.report(str(attempt["id"]))
		if err != nil {
			return nil, err
		}
		reports = append(reports, report)
	}
	if len(reports) == 0 {
		return nil, fail("لا توجد تقارير مكتملة التصحيح للتنزيل", 409)
	}
	if a.pdfHelper == "" {
		return nil, fail("ثبّت أحدث نسخة من البرنامج لتوليد PDF", 503)
	}
	dir, err := os.MkdirTemp("", "massar-reports-")
	if err != nil {
		return nil, err
	}
	defer os.RemoveAll(dir)
	input, output := filepath.Join(dir, "reports.json"), filepath.Join(dir, "reports.zip")
	if err = os.WriteFile(input, []byte(encode(reports)), 0600); err != nil {
		return nil, err
	}
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Minute)
	defer cancel()
	if err = exec.CommandContext(ctx, a.pdfHelper, "--zip", input, output).Run(); err != nil {
		return nil, fmt.Errorf("PDF archive: %w", err)
	}
	return os.ReadFile(output)
}
