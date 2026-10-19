package main

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"unicode"
)

const whatsappSendSchema = `CREATE TABLE IF NOT EXISTS whatsapp_report_sends(fingerprint TEXT PRIMARY KEY,attempt_id TEXT NOT NULL REFERENCES attempts(id),recipient TEXT NOT NULL,template_name TEXT NOT NULL,filename TEXT NOT NULL,state TEXT NOT NULL,message_id TEXT NOT NULL DEFAULT '',error TEXT NOT NULL DEFAULT '',created_at REAL NOT NULL,updated_at REAL NOT NULL);` + whatsappCenterSchema

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
	if err = validateWhatsAppVersion(config.APIVersion); err != nil {
		return nil, err
	}
	template, positions, err := c.reportTemplate(config, str(body["templateId"]))
	if err != nil {
		return nil, err
	}
	if schema := str(body["bindingSchema"]); schema != "" && schema != templateBindingDigest(template) {
		return nil, fail("القالب اتغير؛ حدّث القوالب وراجع المتغيرات ثم أعد المعاينة", 409)
	}
	if str(body["centerBatchId"]) != "" && templateUsesDocument(template) {
		return nil, fail("اختر قالبًا نصيًا لرسالة تقرير السنتر؛ PDF متاح لتقارير الامتحان الإلكتروني", 400)
	}
	report, err := c.reportData(body)
	if err != nil {
		return nil, err
	}
	if reason := templateAttendanceConflict(template, report); reason != "" {
		return nil, fail(reason, 409)
	}
	attempt := report["attempt"].(M)
	rawPhone := str(attempt["phone"])
	if body["recipientKind"] == "guardian" {
		rawPhone = str(report["guardianPhone"])
	}
	phone, err := normalizeWhatsAppPhone(rawPhone)
	if err != nil {
		return nil, fail("رقم الطالب غير صالح للإرسال", 400)
	}
	recipient := "20" + phone[1:]
	exam := report["exam"].(M)["config"].(M)
	parameters, err := reportParameters(report, body["mappings"], len(positions), template)
	if err != nil {
		return nil, err
	}
	filename := safeReportFilename(fmt.Sprintf("%s - %v من %v.pdf", str(attempt["name"]), attempt["score"], attempt["maximum"]))
	fingerprint := hash(encode(M{"attemptId": attempt["id"], "recipient": recipient, "sender": config.PhoneNumberID, "template": template, "parameters": parameters, "questions": report["questions"], "answers": attempt["answers"], "grades": attempt["grades"], "name": attempt["name"], "code": attempt["code"], "score": attempt["score"], "maximum": attempt["maximum"], "exam": exam["title"]}))
	if str(body["centerBatchId"]) != "" {
		fingerprint = "center:" + fingerprint
		filename = "رسالة · " + str(attempt["name"])
	}
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
	config, err := c.config()
	if err != nil {
		return nil, err
	}
	template, _, err := c.reportTemplate(config, str(body["templateId"]))
	if err != nil {
		return nil, err
	}
	if schema := str(body["bindingSchema"]); schema != "" && schema != templateBindingDigest(template) {
		return nil, fail("القالب اتغير؛ حدّث القوالب وراجع المتغيرات ثم أعد المعاينة", 409)
	}
	for _, raw := range ids {
		aid := str(raw)
		if seen[aid] {
			return nil, fail("قائمة الطلاب متكررة", 400)
		}
		seen[aid] = true
		if str(body["centerBatchId"]) != "" {
			report, err := c.reportData(M{"attemptId": aid, "centerBatchId": body["centerBatchId"]})
			if err != nil {
				return nil, err
			}
			if reason := templateAttendanceConflict(template, report); reason != "" {
				attempt := report["attempt"].(M)
				phone := attempt["phone"]
				if body["recipientKind"] == "guardian" {
					phone = report["guardianPhone"]
				}
				items = append(items, M{"attemptId": aid, "name": attempt["name"], "code": attempt["code"], "phone": phone, "score": attempt["score"], "maximum": attempt["maximum"], "error": reason})
				continue
			}
		}
		prepared, err := c.prepare(M{"attemptId": aid, "templateId": body["templateId"], "bindingSchema": body["bindingSchema"], "mappings": body["mappings"], "centerBatchId": body["centerBatchId"], "recipientKind": body["recipientKind"]})
		if err != nil {
			return nil, err
		}
		attempt := prepared.report["attempt"].(M)
		prior, err := c.guardedSendRecord(prepared)
		if err != nil {
			return nil, err
		}
		items = append(items, M{"attemptId": aid, "name": attempt["name"], "phone": "+" + prepared.recipient, "code": attempt["code"], "score": attempt["score"], "maximum": attempt["maximum"], "filename": prepared.filename, "parameters": prepared.parameters, "fingerprint": prepared.fingerprint, "prior": prior})
	}
	return M{"items": items}, nil
}

func (c *whatsappCloud) sendRecord(fingerprint string) (M, error) {
	var state, messageID, errText string
	err := c.app.db.sql.QueryRow(`SELECT state,message_id,error FROM `+whatsappSendTable(fingerprint)+` WHERE fingerprint=?`, fingerprint).Scan(&state, &messageID, &errText)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	return M{"state": state, "messageId": messageID, "error": errText}, nil
}

func (c *whatsappCloud) sendReport(ctx context.Context, body M) (M, error) {
	select {
	case c.sendSlots <- struct{}{}:
		defer func() { <-c.sendSlots }()
	case <-ctx.Done():
		return nil, ctx.Err()
	}
	if err := ctx.Err(); err != nil {
		return nil, err
	}
	c.mu.Lock()
	_, err := c.ensureTemplatesLocked(ctx)
	var prepared *preparedWhatsAppReport
	var previous M
	if err == nil {
		prepared, err = c.prepare(body)
	}
	if err == nil && str(body["fingerprint"]) != prepared.fingerprint {
		err = fail("بيانات التقرير أو القالب اتغيرت؛ افتح المعاينة من جديد", 409)
	}
	if err == nil {
		previous, err = c.claimSend(prepared, body)
	}
	c.mu.Unlock()
	if err != nil {
		return nil, err
	}
	if previous != nil {
		return previous, nil
	}
	mediaID := ""
	if templateUsesDocument(prepared.template) {
		mediaID, err = c.uploadPreparedReport(ctx, prepared)
	}
	if err != nil {
		reply, saveErr := c.finishSend(prepared.fingerprint, "failed", "", safeSendError(err))
		if reply != nil {
			reply["retryable"] = retryableWhatsAppError(err)
		}
		return reply, saveErr
	}
	c.mu.Lock()
	_, err = c.ensureTemplatesLocked(ctx)
	var fresh *preparedWhatsAppReport
	if err == nil {
		fresh, err = c.prepare(body)
	}
	c.mu.Unlock()
	if err == nil && fresh.fingerprint != prepared.fingerprint {
		err = fail("تغيّر التقرير أثناء التجهيز؛ راجع المعاينة الجديدة", 409)
	}
	if err != nil {
		reply, saveErr := c.finishSend(prepared.fingerprint, "failed", "", safeSendError(err))
		if reply != nil {
			reply["retryable"] = retryableWhatsAppError(err)
		}
		return reply, saveErr
	}
	return c.deliverReport(ctx, prepared, mediaID)
}

func retryableWhatsAppError(err error) bool {
	var expected roomError
	var limited whatsappRateLimit
	if errors.As(err, &limited) {
		return true
	}
	return errors.Is(err, context.DeadlineExceeded) || (errors.As(err, &expected) && expected.status == 502)
}

func (c *whatsappCloud) guardedSendRecord(report *preparedWhatsAppReport) (M, error) {
	var fingerprint string
	err := c.app.db.sql.QueryRow(`SELECT fingerprint FROM `+whatsappSendTable(report.fingerprint)+` WHERE attempt_id=? AND recipient=? AND template_name=? AND state IN ('accepted','unknown','sending','preparing') ORDER BY created_at LIMIT 1`,
		str(report.report["attempt"].(M)["id"]), report.recipient, str(report.template["name"])).Scan(&fingerprint)
	if errors.Is(err, sql.ErrNoRows) {
		return c.sendRecord(report.fingerprint)
	}
	if err != nil {
		return nil, err
	}
	return c.sendRecord(fingerprint)
}

func (c *whatsappCloud) claimSend(report *preparedWhatsAppReport, body M) (M, error) {
	prior, err := c.guardedSendRecord(report)
	if err != nil {
		return nil, err
	}
	retryable := prior != nil && (str(prior["state"]) == "failed" || str(prior["state"]) == "rejected")
	if prior != nil && !(retryable && body["retry"] == true) {
		return M{"record": prior, "replayed": true}, nil
	}
	if prior != nil {
		_, err = c.app.db.sql.Exec(`UPDATE `+whatsappSendTable(report.fingerprint)+` SET state='preparing',error='',updated_at=? WHERE fingerprint=? AND state IN ('failed','rejected')`, now(), report.fingerprint)
	} else {
		aid := str(report.report["attempt"].(M)["id"])
		_, err = c.app.db.sql.Exec(`INSERT INTO `+whatsappSendTable(report.fingerprint)+`(fingerprint,attempt_id,recipient,template_name,filename,state,created_at,updated_at) VALUES(?,?,?,?,?,'preparing',?,?)`, report.fingerprint, aid, report.recipient, str(report.template["name"]), report.filename, now(), now())
	}
	return nil, err
}

func (c *whatsappCloud) uploadPreparedReport(ctx context.Context, report *preparedWhatsAppReport) (string, error) {
	cacheDir := filepath.Join(filepath.Dir(c.app.db.path), "report-cache")
	cachePath := filepath.Join(cacheDir, report.fingerprint+".pdf")
	pdf, err := os.ReadFile(cachePath)
	if errors.Is(err, os.ErrNotExist) {
		pdf, err = c.app.reportPDF(report.report)
		if err == nil {
			err = os.MkdirAll(cacheDir, 0700)
			if err == nil {
				err = os.WriteFile(cachePath, pdf, 0600)
			}
		}
	}
	if err != nil {
		return "", err
	}
	if !strings.HasPrefix(string(pdf), "%PDF-") || len(pdf) > 10*1024*1024 {
		return "", fail("ملف التقرير غير صالح أو أكبر من 10 ميجا", 400)
	}
	return c.uploadPDF(ctx, report.config, pdf, report.filename)
}

func (c *whatsappCloud) deliverReport(ctx context.Context, report *preparedWhatsAppReport, mediaID string) (M, error) {
	if err := ctx.Err(); err != nil {
		reply, saveErr := c.finishSend(report.fingerprint, "failed", "", "الطلب لم يصل إلى مرحلة إرسال الرسالة")
		if reply != nil {
			reply["retryable"] = true
		}
		return reply, saveErr
	}
	// Persist the uncertain boundary before contacting Meta; a crash cannot authorize a duplicate.
	if _, err := c.app.db.sql.Exec(`UPDATE `+whatsappSendTable(report.fingerprint)+` SET state='sending',updated_at=? WHERE fingerprint=?`, now(), report.fingerprint); err != nil {
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
	var limited whatsappRateLimit
	if errors.As(err, &limited) {
		reply, saveErr := c.finishSend(report.fingerprint, "rejected", "", safeSendError(err))
		if reply != nil {
			reply["retryable"] = true
			reply["retryAfterSeconds"] = limited.seconds
		}
		return reply, saveErr
	}
	return c.finishSend(report.fingerprint, state, "", safeSendError(err))
}

func safeSendError(err error) string {
	var limited whatsappRateLimit
	if errors.As(err, &limited) {
		return limited.Error()
	}
	var expected roomError
	if errors.As(err, &expected) {
		return expected.msg
	}
	return "تعذر تجهيز التقرير؛ راجع إعدادات البرنامج"
}

func (c *whatsappCloud) finishSend(fingerprint, state, messageID, errText string) (M, error) {
	_, err := c.app.db.sql.Exec(`UPDATE `+whatsappSendTable(fingerprint)+` SET state=?,message_id=?,error=?,updated_at=? WHERE fingerprint=?`, state, messageID, errText, now(), fingerprint)
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
	if err = rows.Err(); err != nil {
		return nil, err
	}
	rows.Close()
	centerRows, err := c.app.db.sql.Query(`SELECT attempt_id,recipient,template_name,filename,state,message_id,error,created_at FROM whatsapp_center_sends ORDER BY created_at DESC LIMIT 1000`)
	if err != nil {
		return nil, err
	}
	defer centerRows.Close()
	for centerRows.Next() {
		var id, phone, template, filename, state, messageID, errText string
		var created float64
		if err = centerRows.Scan(&id, &phone, &template, &filename, &state, &messageID, &errText, &created); err != nil {
			return nil, err
		}
		items = append(items, M{"attemptId": id, "name": strings.TrimPrefix(filename, "رسالة · "), "phone": phone, "template": template, "filename": filename, "state": state, "messageId": messageID, "error": errText, "createdAt": created})
	}
	return M{"items": items}, centerRows.Err()
}

func (c *whatsappCloud) recoverSends() error {
	if _, err := c.app.db.sql.Exec(`UPDATE whatsapp_center_sends SET state=CASE WHEN state='sending' THEN 'unknown' ELSE 'failed' END,error='توقف البرنامج؛ راجع السجل',updated_at=? WHERE state IN ('preparing','sending')`, now()); err != nil {
		return err
	}
	_, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_sends SET state=CASE WHEN state='sending' THEN 'unknown' ELSE 'failed' END,error='توقف البرنامج أثناء الإرسال؛ راجع الحالة قبل إعادة المحاولة',updated_at=? WHERE state IN ('preparing','sending')`, now())
	if err != nil {
		return err
	}
	_, err = c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET state='interrupted',message='توقف البرنامج؛ راجع سجل الإرسال ثم أعد معاينة الباقي',updated_at=? WHERE state IN ('queued','running')`, now())
	return err
}

func (c *whatsappCloud) reportTemplate(config whatsappConfig, templateID string) (M, []int, error) {
	cache, err := c.templates()
	if err != nil {
		return nil, nil, err
	}
	if str(cache["businessAccountId"]) != config.BusinessAccountID || str(cache["phoneNumberId"]) != config.PhoneNumberID || now()-num(cache["syncedAt"]) > 600 {
		return nil, nil, fail("تعذر تحديث القوالب تلقائيًا؛ راجع الإنترنت وحساب واتساب ثم أعد المحاولة", 409)
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

func reportParameters(report M, rawMappings any, count int, template M) ([]string, error) {
	mappings, ok := rawMappings.([]any)
	if !ok || len(mappings) != count {
		return nil, fail("حدد بيانات كل متغير في القالب", 400)
	}
	bindings, reason := templateBindings(template)
	if reason != "" {
		return nil, fail(reason, 400)
	}
	attempt := report["attempt"].(M)
	exam := report["exam"].(M)["config"].(M)
	fields := M{"name": attempt["name"], "code": attempt["code"], "score": fmt.Sprint(attempt["score"]), "maximum": fmt.Sprint(attempt["maximum"]), "result": fmt.Sprintf("%v من %v", attempt["score"], attempt["maximum"]), "exam": exam["title"]}
	if extras, ok := report["centerFields"].(M); ok {
		for key, value := range extras {
			if value != nil {
				fields[key] = fmt.Sprint(value)
			}
		}
	}
	parameters := []string{}
	for index, raw := range mappings {
		mapping, ok := raw.(map[string]any)
		if !ok {
			return nil, fail("تعيين المتغير غير صالح", 400)
		}
		parameter := str(fields[str(mapping["source"])])
		if mapping["source"] == "homework" {
			parameter = templateHomeworkValue(template, parameter)
		}
		if mapping["source"] == "month" {
			parameter = templateMonthValue(template, bindings[index], parameter)
		}
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

func normalizeWhatsAppPhone(raw string) (string, error) {
	phone := normalizeDigits(strings.TrimSpace(raw))
	phone = strings.NewReplacer(" ", "", "-", "", "(", "", ")", "").Replace(phone)
	if strings.HasPrefix(phone, "0020") {
		phone = "0" + strings.TrimPrefix(phone, "0020")
	} else if strings.HasPrefix(phone, "+20") {
		phone = "0" + strings.TrimPrefix(phone, "+20")
	} else if strings.HasPrefix(phone, "20") && len(phone) == 12 {
		phone = "0" + phone[2:]
	}
	if len(phone) != 11 || !strings.HasPrefix(phone, "01") || !strings.ContainsRune("0125", rune(phone[2])) {
		return "", fail("رقم واتساب المصري غير صالح", 400)
	}
	return normalizePhone(phone)
}
