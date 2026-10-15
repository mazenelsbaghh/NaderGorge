package main

import (
	"database/sql"
	"errors"
	"fmt"
	"math"
	"strings"
	"unicode"
)

const whatsappCenterSchema = `CREATE TABLE IF NOT EXISTS whatsapp_center_batches(id TEXT PRIMARY KEY,title TEXT NOT NULL,payload TEXT NOT NULL,created_at REAL NOT NULL);
CREATE TABLE IF NOT EXISTS whatsapp_center_sends(fingerprint TEXT PRIMARY KEY,attempt_id TEXT NOT NULL,recipient TEXT NOT NULL,template_name TEXT NOT NULL,filename TEXT NOT NULL,state TEXT NOT NULL,message_id TEXT NOT NULL DEFAULT '',error TEXT NOT NULL DEFAULT '',created_at REAL NOT NULL,updated_at REAL NOT NULL);` + whatsappBindingsSchema

func whatsappSendTable(fingerprint string) string {
	if strings.HasPrefix(fingerprint, "center:") {
		return "whatsapp_center_sends"
	}
	return "whatsapp_report_sends"
}

// The Flutter host supplies an authenticated, filtered report snapshot. These
// rows never become exam attempts and cannot change attendance or money.
func (c *whatsappCloud) saveCenterBatch(body M) (M, error) {
	rows, ok := body["records"].([]any)
	title := strings.TrimSpace(str(body["title"]))
	messageKind := str(body["messageKind"])
	if messageKind == "" {
		messageKind = "followup"
	}
	if !validWhatsAppReportKind(messageKind) {
		return nil, fail("نوع رسالة السنتر غير صالح", 400)
	}
	if !ok || len(rows) == 0 || len(rows) > 1000 || title == "" || len([]rune(title)) > 200 {
		return nil, fail("اختر تقريرًا من ١ إلى ١٠٠٠ طالب", 400)
	}
	clean := []M{}
	seen := map[string]bool{}
	allowed := []string{"id", "studentId", "sessionId", "activityId", "name", "code", "phone", "guardianPhone", "lesson", "month", "center", "group", "time", "homework", "exam", "attendance", "score", "maximum"}
	for _, raw := range rows {
		row, ok := raw.(map[string]any)
		if !ok {
			return nil, fail("بيانات تقرير الطالب غير صالحة", 400)
		}
		item := M{}
		for _, key := range allowed {
			if value, exists := row[key]; exists {
				if key != "score" && key != "maximum" {
					text, ok := value.(string)
					if !ok || len([]rune(text)) > 1000 || strings.IndexFunc(text, unicode.IsControl) >= 0 {
						return nil, fail("بيانات تقرير الطالب غير صالحة", 400)
					}
				}
				item[key] = value
			}
		}
		if str(item["id"]) == "" || str(item["name"]) == "" || str(item["code"]) == "" || seen[str(item["id"])] {
			return nil, fail("تعذر تمييز الطلاب في التقرير؛ أعد فتح الفلاتر", 400)
		}
		seen[str(item["id"])] = true
		for _, key := range []string{"score", "maximum"} {
			if value, exists := item[key]; exists && value != nil {
				number, ok := value.(float64)
				if !ok || (key == "maximum" && number == 0) || math.IsNaN(number) || math.IsInf(number, 0) || number < 0 || number > 1000000 {
					return nil, fail("درجة التقرير غير صالحة", 400)
				}
			}
		}
		if item["score"] != nil && item["maximum"] != nil && num(item["score"]) > num(item["maximum"]) {
			return nil, fail("درجة الطالب أكبر من نهايته العظمى", 400)
		}
		clean = append(clean, item)
	}
	id := token()
	snapshot := M{"id": id, "title": title, "messageKind": messageKind, "records": clean}
	_, err := c.app.db.sql.Exec(`INSERT INTO whatsapp_center_batches(id,title,payload,created_at) VALUES(?,?,?,?)`, id, title, encode(snapshot), now())
	return M{"batchId": id, "count": len(clean)}, err
}
func (c *whatsappCloud) centerBatch(id string) (M, error) {
	var raw string
	err := c.app.db.sql.QueryRow(`SELECT payload FROM whatsapp_center_batches WHERE id=?`, id).Scan(&raw)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, fail("تقرير السنتر غير موجود؛ افتحه من التقارير مرة أخرى", 404)
	}
	if err != nil {
		return nil, err
	}
	return decode(raw), nil
}
func (c *whatsappCloud) reportData(body M) (M, error) {
	id := str(body["centerBatchId"])
	if id == "" {
		return c.app.db.report(str(body["attemptId"]))
	}
	batch, err := c.centerBatch(id)
	if err != nil {
		return nil, err
	}
	rows, _ := batch["records"].([]any)
	for _, raw := range rows {
		row := raw.(map[string]any)
		if str(row["id"]) != str(body["attemptId"]) {
			continue
		}
		fields := M{}
		for key, value := range row {
			fields[key] = value
		}
		if row["score"] == nil {
			fields["score"] = "لم تُرصد"
		}
		if row["maximum"] == nil {
			fields["maximum"] = "غير معروفة"
		}
		fields["result"] = "لم تُرصد"
		if row["score"] != nil {
			fields["result"] = fmt.Sprintf("%v من %v", fields["score"], fields["maximum"])
		}
		return M{"attempt": M{"id": row["id"], "name": row["name"], "code": row["code"], "phone": row["phone"], "score": row["score"], "maximum": row["maximum"]}, "guardianPhone": row["guardianPhone"], "exam": M{"id": id, "config": M{"title": batch["title"]}}, "centerFields": fields}, nil
	}
	return nil, fail("الطالب غير موجود في التقرير المختار", 404)
}
