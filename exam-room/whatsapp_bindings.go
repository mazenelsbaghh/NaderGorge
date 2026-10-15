package main

import (
	"strings"
	"unicode"
)

const whatsappBindingsSchema = `CREATE TABLE IF NOT EXISTS whatsapp_template_bindings(account TEXT NOT NULL,template_id TEXT NOT NULL,recipient TEXT NOT NULL,report_kind TEXT NOT NULL,schema_digest TEXT NOT NULL,mappings TEXT NOT NULL,updated_at REAL NOT NULL,PRIMARY KEY(account,template_id,recipient,report_kind))`

func validWhatsAppReportKind(kind string) bool {
	return kind == "followup" || kind == "attendance" || kind == "homework" || kind == "grade" || kind == "exam"
}

func templateBindingDigest(template M) string {
	return hash(encode(M{"name": template["name"], "language": template["language"], "components": template["components"]}))
}

func cleanTemplateMappings(raw any, count int) ([]M, error) {
	rows, ok := raw.([]any)
	if !ok || len(rows) != count {
		return nil, fail("حدد بيانات كل متغير قبل تثبيت الربط", 400)
	}
	allowed := map[string]bool{"name": true, "code": true, "result": true, "score": true, "maximum": true, "exam": true, "lesson": true, "month": true, "center": true, "group": true, "time": true, "homework": true, "attendance": true, "literal": true}
	clean := []M{}
	for _, value := range rows {
		row, ok := value.(map[string]any)
		if !ok || !allowed[str(row["source"])] {
			return nil, fail("اختر بيانات صالحة لكل متغير", 400)
		}
		mapping := M{"source": str(row["source"])}
		if mapping["source"] == "literal" {
			text := strings.TrimSpace(str(row["text"]))
			if text == "" || len([]rune(text)) > 1000 || strings.IndexFunc(text, unicode.IsControl) >= 0 {
				return nil, fail("النص الثابت فارغ أو غير صالح", 400)
			}
			mapping["text"] = text
		}
		clean = append(clean, mapping)
	}
	return clean, nil
}

func (c *whatsappCloud) saveTemplateBindings(body M) (M, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	config, err := c.config()
	if err != nil {
		return nil, err
	}
	template, positions, err := c.reportTemplate(config, str(body["templateId"]))
	if err != nil {
		return nil, err
	}
	recipient, kind := str(body["recipientKind"]), str(body["reportKind"])
	if (recipient != "student" && recipient != "guardian") || !validWhatsAppReportKind(kind) {
		return nil, fail("حدد نوع الرسالة والمستلم", 400)
	}
	digest := templateBindingDigest(template)
	if str(body["bindingSchema"]) != digest {
		return nil, fail("القالب اتغير؛ حدّث القوالب وراجع المتغيرات قبل تثبيت الربط", 409)
	}
	mappings, err := cleanTemplateMappings(body["mappings"], len(positions))
	if err != nil {
		return nil, err
	}
	account := config.BusinessAccountID + ":" + config.PhoneNumberID
	_, err = c.app.db.sql.Exec(`INSERT INTO whatsapp_template_bindings(account,template_id,recipient,report_kind,schema_digest,mappings,updated_at) VALUES(?,?,?,?,?,?,?) ON CONFLICT(account,template_id,recipient,report_kind) DO UPDATE SET schema_digest=excluded.schema_digest,mappings=excluded.mappings,updated_at=excluded.updated_at`, account, str(template["id"]), recipient, kind, digest, encode(M{"mappings": mappings}), now())
	return M{"bindingSchema": digest, "recipientKind": recipient, "reportKind": kind, "mappings": mappings}, err
}

func (c *whatsappCloud) withTemplateBindings(cache M) (M, error) {
	account := str(cache["businessAccountId"]) + ":" + str(cache["phoneNumberId"])
	rows, err := c.app.db.sql.Query(`SELECT template_id,recipient,report_kind,schema_digest,mappings FROM whatsapp_template_bindings WHERE account=?`, account)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	saved := map[string][]M{}
	for rows.Next() {
		var id, recipient, kind, digest, raw string
		if err = rows.Scan(&id, &recipient, &kind, &digest, &raw); err != nil {
			return nil, err
		}
		saved[id] = append(saved[id], M{"recipientKind": recipient, "reportKind": kind, "bindingSchema": digest, "mappings": decode(raw)["mappings"]})
	}
	if err = rows.Err(); err != nil {
		return nil, err
	}
	templates, _ := cache["templates"].([]any)
	for _, raw := range templates {
		template, ok := raw.(map[string]any)
		if !ok {
			continue
		}
		digest := templateBindingDigest(template)
		template["bindingSchema"] = digest
		template["savedBindings"] = []M{}
		for _, binding := range saved[str(template["id"])] {
			if binding["bindingSchema"] == digest {
				template["savedBindings"] = append(template["savedBindings"].([]M), binding)
			}
		}
	}
	return cache, nil
}
