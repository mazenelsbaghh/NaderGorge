package main

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"strings"
	"unicode"
)

const whatsappSendSchema = `CREATE TABLE IF NOT EXISTS whatsapp_report_sends(fingerprint TEXT PRIMARY KEY,attempt_id TEXT NOT NULL REFERENCES attempts(id),recipient TEXT NOT NULL,template_name TEXT NOT NULL,filename TEXT NOT NULL,state TEXT NOT NULL,message_id TEXT NOT NULL DEFAULT '',error TEXT NOT NULL DEFAULT '',created_at REAL NOT NULL,updated_at REAL NOT NULL)`

type preparedWhatsAppReport struct {
	config                           whatsappConfig
	template                         M
	report                           M
	parameters                       []string
	recipient, filename, fingerprint string
}

func (c *whatsappCloud) prepare(body M) (*preparedWhatsAppReport, error) {
	config, err := c.config()
	if err != nil {
		return nil, err
	}
	if config.AccessToken == "" || config.PhoneNumberID == "" || config.BusinessAccountID == "" {
		return nil, fail("أكمل ربط حساب واتساب أولًا", 409)
	}
	template, positions, err := c.reportTemplate(config, str(body["templateId"]))
	if err != nil {
		return nil, err
	}
	report, err := c.app.db.report(str(body["attemptId"]))
	if err != nil {
		return nil, err
	}
	attempt := report["attempt"].(M)
	phone, err := normalizePhone(str(attempt["phone"]))
	if err != nil {
		return nil, fail("رقم الطالب غير صالح للإرسال", 400)
	}
	recipient := "20" + phone[1:]
	exam := report["exam"].(M)["config"].(M)
	parameters, err := reportParameters(report, body["mappings"], len(positions))
	if err != nil {
		return nil, err
	}
	filename := safeReportFilename(fmt.Sprintf("%s - %v من %v.pdf", str(attempt["name"]), attempt["score"], attempt["maximum"]))
	fingerprint := hash(encode(M{"attemptId": attempt["id"], "recipient": recipient, "sender": config.PhoneNumberID, "template": template, "parameters": parameters, "questions": report["questions"], "answers": attempt["answers"], "grades": attempt["grades"], "name": attempt["name"], "code": attempt["code"], "score": attempt["score"], "maximum": attempt["maximum"], "exam": exam["title"]}))
	return &preparedWhatsAppReport{config, template, report, parameters, recipient, filename, fingerprint}, nil
}

func safeReportFilename(name string) string {
	clean := strings.Map(func(r rune) rune {
		if unicode.IsControl(r) || strings.ContainsRune(`/\:"*?<>|`, r) {
			return '-'
		}
		return r
	}, name)
	if len([]rune(clean)) > 150 {
		clean = string([]rune(clean)[:140]) + ".pdf"
	}
	return clean
}

func (c *whatsappCloud) preview(body M) (M, error) {
	ids, ok := body["attemptIds"].([]any)
	if !ok || len(ids) == 0 || len(ids) > 1000 {
		return nil, fail("اختر طلابًا لإرسال تقاريرهم", 400)
	}
	items := []M{}
	seen := map[string]bool{}
	for _, raw := range ids {
		aid := str(raw)
		if seen[aid] {
			return nil, fail("قائمة الطلاب متكررة", 400)
		}
		seen[aid] = true
		prepared, err := c.prepare(M{"attemptId": aid, "templateId": body["templateId"], "mappings": body["mappings"]})
		if err != nil {
			return nil, err
		}
		attempt := prepared.report["attempt"].(M)
		prior, err := c.sendRecord(prepared.fingerprint)
		if err != nil {
			return nil, err
		}
		items = append(items, M{"attemptId": aid, "name": attempt["name"], "phone": attempt["phone"], "code": attempt["code"], "score": attempt["score"], "maximum": attempt["maximum"], "filename": prepared.filename, "parameters": prepared.parameters, "fingerprint": prepared.fingerprint, "prior": prior})
	}
	return M{"items": items}, nil
}

func (c *whatsappCloud) sendRecord(fingerprint string) (M, error) {
	var state, messageID, errText string
	err := c.app.db.sql.QueryRow(`SELECT state,message_id,error FROM whatsapp_report_sends WHERE fingerprint=?`, fingerprint).Scan(&state, &messageID, &errText)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	return M{"state": state, "messageId": messageID, "error": errText}, nil
}

func (c *whatsappCloud) sendReport(ctx context.Context, body M) (M, error) {
	c.mu.Lock()
	defer c.mu.Unlock()
	if err := ctx.Err(); err != nil {
		return nil, err
	}
	prepared, err := c.prepare(body)
	if err != nil {
		return nil, err
	}
	if str(body["fingerprint"]) != prepared.fingerprint {
		return nil, fail("بيانات التقرير أو القالب اتغيرت؛ افتح المعاينة من جديد", 409)
	}
	previous, err := c.claimSend(prepared, body)
	if err != nil {
		return nil, err
	}
	if previous != nil {
		return previous, nil
	}
	mediaID, err := c.uploadPreparedReport(ctx, prepared)
	if err != nil {
		return c.finishSend(prepared.fingerprint, "failed", "", safeSendError(err))
	}
	fresh, err := c.prepare(body)
	if err == nil && fresh.fingerprint != prepared.fingerprint {
		err = fail("تغيّر التقرير أثناء التجهيز؛ راجع المعاينة الجديدة", 409)
	}
	if err != nil {
		return c.finishSend(prepared.fingerprint, "failed", "", safeSendError(err))
	}
	return c.deliverReport(ctx, prepared, mediaID)
}

func (c *whatsappCloud) claimSend(report *preparedWhatsAppReport, body M) (M, error) {
	prior, err := c.sendRecord(report.fingerprint)
	if err != nil {
		return nil, err
	}
	retryable := prior != nil && (str(prior["state"]) == "failed" || str(prior["state"]) == "rejected")
	if prior != nil && !(retryable && body["retry"] == true) {
		return M{"record": prior, "replayed": true}, nil
	}
	if prior != nil {
		_, err = c.app.db.sql.Exec(`UPDATE whatsapp_report_sends SET state='preparing',error='',updated_at=? WHERE fingerprint=? AND state IN ('failed','rejected')`, now(), report.fingerprint)
	} else {
		aid := str(report.report["attempt"].(M)["id"])
		_, err = c.app.db.sql.Exec(`INSERT INTO whatsapp_report_sends(fingerprint,attempt_id,recipient,template_name,filename,state,created_at,updated_at) VALUES(?,?,?,?,?,'preparing',?,?)`, report.fingerprint, aid, report.recipient, str(report.template["name"]), report.filename, now(), now())
	}
	return nil, err
}

func (c *whatsappCloud) uploadPreparedReport(ctx context.Context, report *preparedWhatsAppReport) (string, error) {
	pdf, err := c.app.reportPDF(report.report)
	if err != nil {
		return "", err
	}
	if !strings.HasPrefix(string(pdf), "%PDF-") || len(pdf) > 10*1024*1024 {
		return "", fail("ملف التقرير غير صالح أو أكبر من 10 ميجا", 400)
	}
	return c.uploadPDF(ctx, report.config, pdf, report.filename)
}

func (c *whatsappCloud) deliverReport(ctx context.Context, report *preparedWhatsAppReport, mediaID string) (M, error) {
	// Persist the uncertain boundary before contacting Meta; a crash cannot authorize a duplicate.
	if _, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_sends SET state='sending',updated_at=? WHERE fingerprint=?`, now(), report.fingerprint); err != nil {
		return nil, err
	}
	messageID, err := c.sendPDF(ctx, report, mediaID)
	if err == nil {
		return c.finishSend(report.fingerprint, "accepted", messageID, "")
	}
	state := "unknown"
	var provider roomError
	if errors.As(err, &provider) && provider.status == 422 {
		state = "rejected"
	}
	return c.finishSend(report.fingerprint, state, "", safeSendError(err))
}

func safeSendError(err error) string {
	var expected roomError
	if errors.As(err, &expected) {
		return expected.msg
	}
	return "تعذر تجهيز التقرير؛ راجع إعدادات البرنامج"
}

func (c *whatsappCloud) finishSend(fingerprint, state, messageID, errText string) (M, error) {
	_, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_sends SET state=?,message_id=?,error=?,updated_at=? WHERE fingerprint=?`, state, messageID, errText, now(), fingerprint)
	if err != nil {
		return nil, err
	}
	record, err := c.sendRecord(fingerprint)
	return M{"record": record, "replayed": false}, err
}

func (c *whatsappCloud) history() (M, error) {
	rows, err := c.app.db.sql.Query(`SELECT s.attempt_id,a.name,a.code,s.recipient,s.template_name,s.filename,s.state,s.message_id,s.error,s.created_at FROM whatsapp_report_sends s JOIN attempts a ON a.id=s.attempt_id ORDER BY s.created_at DESC LIMIT 1000`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	items := []M{}
	for rows.Next() {
		var aid, name, code, phone, template, filename, state, messageID, errText string
		var created float64
		if err = rows.Scan(&aid, &name, &code, &phone, &template, &filename, &state, &messageID, &errText, &created); err != nil {
			return nil, err
		}
		items = append(items, M{"attemptId": aid, "name": name, "code": code, "phone": phone, "template": template, "filename": filename, "state": state, "messageId": messageID, "error": errText, "createdAt": created})
	}
	return M{"items": items}, rows.Err()
}

func (c *whatsappCloud) recoverSends() error {
	_, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_sends SET state=CASE WHEN state='sending' THEN 'unknown' ELSE 'failed' END,error='توقف البرنامج أثناء الإرسال؛ راجع الحالة قبل إعادة المحاولة',updated_at=? WHERE state IN ('preparing','sending')`, now())
	return err
}

func (c *whatsappCloud) reportTemplate(config whatsappConfig, templateID string) (M, []int, error) {
	cache, err := c.templates()
	if err != nil {
		return nil, nil, err
	}
	if str(cache["businessAccountId"]) != config.BusinessAccountID || str(cache["phoneNumberId"]) != config.PhoneNumberID || now()-num(cache["syncedAt"]) > 600 {
		return nil, nil, fail("زامن القوالب قبل الإرسال؛ آخر مزامنة يجب أن تكون خلال 10 دقائق", 409)
	}
	var template M
	rows, _ := cache["templates"].([]any)
	for _, raw := range rows {
		candidate, ok := raw.(map[string]any)
		if ok && str(candidate["id"]) == templateID {
			template = candidate
			break
		}
	}
	if template == nil {
		return nil, nil, fail("اختر قالبًا من الحساب الحالي", 400)
	}
	positions, reason := reportTemplateFields(template)
	if reason != "" {
		return nil, nil, fail(reason, 400)
	}
	return template, positions, nil
}

func reportParameters(report M, rawMappings any, count int) ([]string, error) {
	mappings, ok := rawMappings.([]any)
	if !ok || len(mappings) != count {
		return nil, fail("حدد بيانات كل متغير في القالب", 400)
	}
	attempt := report["attempt"].(M)
	exam := report["exam"].(M)["config"].(M)
	fields := M{"name": attempt["name"], "code": attempt["code"], "score": fmt.Sprint(attempt["score"]), "maximum": fmt.Sprint(attempt["maximum"]), "result": fmt.Sprintf("%v من %v", attempt["score"], attempt["maximum"]), "exam": exam["title"]}
	parameters := []string{}
	for _, raw := range mappings {
		mapping, ok := raw.(map[string]any)
		if !ok {
			return nil, fail("تعيين المتغير غير صالح", 400)
		}
		parameter := str(fields[str(mapping["source"])])
		if str(mapping["source"]) == "literal" {
			parameter = str(mapping["text"])
		}
		if strings.TrimSpace(parameter) == "" || len([]rune(parameter)) > 1000 || strings.IndexFunc(parameter, unicode.IsControl) >= 0 {
			return nil, fail("متغير القالب فارغ أو غير صالح", 400)
		}
		parameters = append(parameters, parameter)
	}
	return parameters, nil
}
