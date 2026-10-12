package main

import (
	"context"
	"time"
)

const whatsappJobsSchema = `CREATE TABLE IF NOT EXISTS whatsapp_report_jobs(id TEXT PRIMARY KEY,title TEXT NOT NULL,state TEXT NOT NULL,payload TEXT NOT NULL,total INTEGER NOT NULL,processed INTEGER NOT NULL DEFAULT 0,accepted INTEGER NOT NULL DEFAULT 0,failed INTEGER NOT NULL DEFAULT 0,stop_requested INTEGER NOT NULL DEFAULT 0,message TEXT NOT NULL DEFAULT '',created_at REAL NOT NULL,updated_at REAL NOT NULL)`

func (c *whatsappCloud) jobs() (M, error) {
	rows, err := c.app.db.sql.Query(`SELECT id,title,state,total,processed,accepted,failed,stop_requested,message FROM whatsapp_report_jobs ORDER BY created_at DESC LIMIT 100`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	items := []M{}
	for rows.Next() {
		var id, title, state, message string
		var total, processed, accepted, failed, stop int
		if err = rows.Scan(&id, &title, &state, &total, &processed, &accepted, &failed, &stop, &message); err != nil {
			return nil, err
		}
		items = append(items, M{"id": id, "title": title, "state": state, "total": total, "processed": processed, "accepted": accepted, "failed": failed, "stopping": stop != 0, "message": message})
	}
	return M{"items": items}, rows.Err()
}

func (c *whatsappCloud) enqueueReports(ctx context.Context, body M) (M, error) {
	items, ok := body["items"].([]any)
	if !ok || len(items) == 0 || len(items) > 1000 {
		return nil, fail("اختر من تقرير واحد إلى ١٠٠٠ تقرير", 400)
	}
	c.jobsMu.Lock()
	defer c.jobsMu.Unlock()
	c.mu.Lock()
	defer c.mu.Unlock()
	if _, err := c.ensureTemplatesLocked(ctx); err != nil {
		return nil, err
	}
	wanted := map[string]bool{}
	title := ""
	examID := ""
	for _, raw := range items {
		item, ok := raw.(map[string]any)
		if !ok {
			return nil, fail("قائمة التقارير غير صالحة", 400)
		}
		report, err := c.prepare(M{"attemptId": item["attemptId"], "templateId": body["templateId"], "mappings": body["mappings"]})
		if err != nil {
			return nil, err
		}
		if str(item["fingerprint"]) != report.fingerprint || wanted[report.fingerprint] {
			return nil, fail("المعاينة تغيرت أو بها تقرير مكرر؛ افتحها من جديد", 409)
		}
		wanted[report.fingerprint] = true
		reportExam := report.report["exam"].(M)
		if examID == "" {
			examID = str(reportExam["id"])
			title = str(reportExam["config"].(M)["title"])
		} else if examID != str(reportExam["id"]) {
			return nil, fail("اختر تقارير امتحان واحد لكل عملية إرسال", 400)
		}
	}
	rows, err := c.app.db.sql.Query(`SELECT payload FROM whatsapp_report_jobs WHERE state IN ('queued','running')`)
	if err != nil {
		return nil, err
	}
	active := 0
	overlap := false
	for rows.Next() {
		var raw string
		if err = rows.Scan(&raw); err != nil {
			break
		}
		active++
		pending, _ := decode(raw)["items"].([]any)
		for _, item := range pending {
			if wanted[str(item.(map[string]any)["fingerprint"])] {
				overlap = true
			}
		}
	}
	if err == nil {
		err = rows.Err()
	}
	rows.Close()
	if err != nil {
		return nil, err
	}
	if overlap {
		return nil, fail("بعض التقارير موجودة بالفعل في دور الإرسال؛ تابع تقدمها أعلى الصفحة", 409)
	}
	if active >= 20 {
		return nil, fail("دور الإرسال ممتلئ؛ انتظر اكتمال إحدى العمليات", 409)
	}
	id := token()
	stamp := now()
	_, err = c.app.db.sql.Exec(`INSERT INTO whatsapp_report_jobs(id,title,state,payload,total,created_at,updated_at) VALUES(?,?,'queued',?,?,?,?)`, id, title, encode(body), len(items), stamp, stamp)
	if err != nil {
		return nil, err
	}
	if !c.jobsRunning {
		c.jobsRunning = true
		c.jobsWG.Add(1)
		go c.runReportQueue()
	}
	return M{"jobId": id, "queued": true}, nil
}

func (c *whatsappCloud) stopJob(id string) (M, error) {
	result, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET stop_requested=1,state=CASE WHEN state='queued' THEN 'stopped' ELSE state END,updated_at=? WHERE id=? AND state IN ('queued','running')`, now(), id)
	if err != nil {
		return nil, err
	}
	n, _ := result.RowsAffected()
	if n == 0 {
		return nil, fail("الإرسال انتهى أو توقف بالفعل", 409)
	}
	return M{"stopping": true}, nil
}

func (c *whatsappCloud) resumeJob(id string) (M, error) {
	c.jobsMu.Lock()
	defer c.jobsMu.Unlock()
	result, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET state='queued',processed=0,accepted=0,failed=0,stop_requested=0,message='',updated_at=? WHERE id=? AND state IN ('partial','interrupted','stopped')`, now(), id)
	if err != nil {
		return nil, err
	}
	count, err := result.RowsAffected()
	if err != nil {
		return nil, err
	}
	if count == 0 {
		return nil, fail("لا توجد عملية متوقفة لاستكمالها", 409)
	}
	if !c.jobsRunning {
		c.jobsRunning = true
		c.jobsWG.Add(1)
		go c.runReportQueue()
	}
	return M{"queued": true, "jobId": id}, nil
}

func (c *whatsappCloud) runReportQueue() {
	defer c.jobsWG.Done()
	for {
		c.jobsMu.Lock()
		var id, raw string
		err := c.app.db.sql.QueryRow(`SELECT id,payload FROM whatsapp_report_jobs WHERE state='queued' ORDER BY created_at,id LIMIT 1`).Scan(&id, &raw)
		if err != nil {
			c.jobsRunning = false
			c.jobsMu.Unlock()
			return
		}
		result, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET state='running',updated_at=? WHERE id=? AND state='queued'`, now(), id)
		if err != nil {
			c.jobsRunning = false
			c.jobsMu.Unlock()
			return
		}
		c.jobsMu.Unlock()
		n, _ := result.RowsAffected()
		if n == 0 {
			continue
		}
		c.runReportJob(id, decode(raw))
	}
}

func (c *whatsappCloud) jobStopped(id string) bool {
	var stop int
	err := c.app.db.sql.QueryRow(`SELECT stop_requested FROM whatsapp_report_jobs WHERE id=?`, id).Scan(&stop)
	return err != nil || stop != 0
}

type reportOutcome struct {
	item      any
	record    M
	retryable bool
	err       error
}

func (c *whatsappCloud) sendJobBatch(body M, items []any) <-chan reportOutcome {
	outcomes := make(chan reportOutcome, len(items))
	for _, raw := range items {
		item := raw.(map[string]any)
		go func() {
			ctx, cancel := context.WithTimeout(context.Background(), 150*time.Second)
			defer cancel()
			reply, err := c.sendReport(ctx, M{"attemptId": item["attemptId"], "fingerprint": item["fingerprint"], "templateId": body["templateId"], "mappings": body["mappings"], "retry": true})
			outcome := reportOutcome{item: item, err: err, retryable: retryableWhatsAppError(err)}
			if reply != nil {
				outcome.record, _ = reply["record"].(M)
				outcome.retryable = reply["retryable"] == true
			}
			outcomes <- outcome
		}()
	}
	return outcomes
}

func (c *whatsappCloud) waitJobGap(id string) bool {
	until := time.Now().Add(c.batchGap)
	for time.Now().Before(until) {
		if c.jobStopped(id) {
			return false
		}
		time.Sleep(min(200*time.Millisecond, time.Until(until)))
	}
	return !c.jobStopped(id)
}

func (c *whatsappCloud) runReportJob(id string, body M) {
	items, _ := body["items"].([]any)
	state, message := "completed", ""
	for offset := 0; offset < len(items); offset += 5 {
		pending := items[offset:min(offset+5, len(items))]
		for len(pending) > 0 {
			if c.jobStopped(id) {
				state = "stopped"
				break
			}
			outcomes := c.sendJobBatch(body, pending)
			retry := []any{}
			for range pending {
				outcome := <-outcomes
				if outcome.retryable {
					retry = append(retry, outcome.item)
					continue
				}
				accepted, failed := 0, 0
				if outcome.err == nil && str(outcome.record["state"]) == "accepted" {
					accepted = 1
				} else {
					failed = 1
				}
				if str(outcome.record["state"]) == "unknown" {
					message = "بعض الرسائل غير مؤكدة ولن تُعاد تلقائيًا لمنع التكرار. باقي التقارير تكمل."
				}
				if outcome.err != nil {
					message = safeSendError(outcome.err)
				}
				if _, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET processed=processed+1,accepted=accepted+?,failed=failed+?,updated_at=? WHERE id=?`, accepted, failed, now(), id); err != nil {
					state = "partial"
					message = "تقدم الإرسال لم يُحفظ؛ راجع سجل الرسائل قبل الاستكمال"
				}
			}
			if state == "partial" {
				break
			}
			pending = retry
			if len(pending) > 0 {
				_, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET message=?,updated_at=? WHERE id=?`, "بانتظار الإنترنت أو استجابة واتساب؛ الاستكمال تلقائي للتقارير التي لم تُرسل.", now(), id)
				if err != nil {
					state = "partial"
					message = "تقدم الإرسال لم يُحفظ"
					break
				}
				if !c.waitJobGap(id) {
					state = "stopped"
					break
				}
			}
		}
		if state != "completed" {
			break
		}
		if c.jobStopped(id) {
			state = "stopped"
			break
		}
		_, err := c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET message=?,updated_at=? WHERE id=?`, message, now(), id)
		if err != nil {
			state = "partial"
			message = "تقدم الإرسال لم يُحفظ"
			break
		}
		if offset+5 < len(items) && !c.waitJobGap(id) {
			state = "stopped"
			break
		}
	}
	var failed int
	err := c.app.db.sql.QueryRow(`SELECT failed FROM whatsapp_report_jobs WHERE id=?`, id).Scan(&failed)
	if state == "completed" && (failed > 0 || err != nil) {
		state = "partial"
	}
	_, _ = c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET state=?,message=?,updated_at=? WHERE id=?`, state, message, now(), id)
}
