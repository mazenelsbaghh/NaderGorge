package main

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

func parentTemplate() M {
	return M{"id": "parent", "name": "parent_follow_center", "status": "APPROVED", "language": "ar", "category": "UTILITY", "components": []any{M{"type": "HEADER", "format": "TEXT", "text": "مساء الخير يا ولي أمر {{1}}"}, M{"type": "BODY", "text": "الطالب {{1}} حضر {{2}} في {{3}} بسنتر {{4}} الساعة {{5}} جاب {{6}} من {{7}} و{{8}} الواجب"}}}
}
func centerCloudBatch(t *testing.T) (*whatsappCloud, M) {
	t.Helper()
	c, _ := reportCloudFixture(t)
	template := parentTemplate()
	_, err := c.app.db.sql.Exec(`UPDATE app_settings SET value=? WHERE key='whatsapp_cloud_templates'`, encode(M{"templates": []any{template}, "syncedAt": now(), "phoneNumberId": "123456", "businessAccountId": "654321"}))
	if err != nil {
		t.Fatal(err)
	}
	records := []any{}
	for i, maximum := range []float64{10, 12} {
		records = append(records, M{"id": []string{"paper", "online"}[i], "name": "طالب تجريبي", "code": []string{"001", "002"}[i], "phone": "01012345678", "guardianPhone": "01112345678", "lesson": "الأولى", "month": "الثاني", "center": "سنتر تجريبي", "time": "18:00", "homework": "عمل", "attendance": "حاضر", "score": float64(8), "maximum": maximum})
	}
	result, err := c.saveCenterBatch(M{"title": "تقرير الامتحان", "records": records})
	if err != nil {
		t.Fatal(err)
	}
	fields, reason := templateBindings(template)
	if reason != "" {
		t.Fatal(reason)
	}
	mappings := []any{}
	for _, field := range fields {
		mappings = append(mappings, M{"source": field["suggestedSource"]})
	}
	return c, M{"centerBatchId": result["batchId"], "templateId": "parent", "mappings": mappings, "recipientKind": "guardian"}
}
func TestCenterWhatsAppUsesEachRecordedMaximumAndSeparateHeaderVariables(t *testing.T) {
	c, base := centerCloudBatch(t)
	preview, err := c.preview(M{"centerBatchId": base["centerBatchId"], "templateId": base["templateId"], "mappings": base["mappings"], "recipientKind": "guardian", "attemptIds": []any{"paper", "online"}})
	if err != nil {
		t.Fatal(err)
	}
	items := preview["items"].([]M)
	if items[0]["parameters"].([]string)[7] != "10" || items[1]["parameters"].([]string)[7] != "12" {
		t.Fatal("mixed maxima were lost", preview)
	}
	var sent atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if !strings.HasSuffix(r.URL.Path, "/messages") {
			t.Error("text template must not render/upload PDF")
		}
		var payload M
		if json.NewDecoder(r.Body).Decode(&payload) != nil {
			t.Error("invalid payload")
		}
		if payload["to"] != "201112345678" {
			t.Error("guardian recipient was not selected")
		}
		components := payload["template"].(map[string]any)["components"].([]any)
		header := components[0].(map[string]any)
		body := components[1].(map[string]any)
		if header["type"] != "header" || len(header["parameters"].([]any)) != 1 || body["type"] != "body" || len(body["parameters"].([]any)) != 8 {
			t.Error("component-local parameter scopes lost")
		}
		sent.Add(1)
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "accepted"}}})
	}))
	defer provider.Close()
	c.baseURL = provider.URL
	c.client = provider.Client()
	request := M{}
	for key, value := range base {
		request[key] = value
	}
	request["attemptId"] = "paper"
	request["fingerprint"] = items[0]["fingerprint"]
	for range 2 {
		reply, err := c.sendReport(context.Background(), request)
		if err != nil || reply["record"].(M)["state"] != "accepted" {
			t.Fatal(reply, err)
		}
	}
	if sent.Load() != 1 {
		t.Fatal("duplicate center message")
	}
	var attempts int
	c.app.db.sql.QueryRow(`SELECT COUNT(*) FROM attempts`).Scan(&attempts)
	if attempts != 1 {
		t.Fatal("center snapshot became an exam attempt")
	}
}
func TestWhatsAppRateLimitRetriesRejectedMessagesWithoutRepeatingAcceptedOnes(t *testing.T) {
	c, base := centerCloudBatch(t)
	c.batchGap = time.Millisecond
	var calls atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if calls.Add(1) == 1 {
			w.WriteHeader(http.StatusTooManyRequests)
			json.NewEncoder(w).Encode(M{"error": M{"code": 130429}})
			return
		}
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "accepted"}}})
	}))
	defer provider.Close()
	c.baseURL = provider.URL
	c.client = provider.Client()
	prepared, err := c.prepare(M{"centerBatchId": base["centerBatchId"], "templateId": base["templateId"], "mappings": base["mappings"], "attemptId": "paper", "recipientKind": "guardian"})
	if err != nil {
		t.Fatal(err)
	}
	base["items"] = []any{M{"attemptId": "paper", "fingerprint": prepared.fingerprint}}
	if _, err = c.enqueueReports(context.Background(), base); err != nil {
		t.Fatal(err)
	}
	waitReportQueue(t, c)
	if calls.Load() != 2 {
		t.Fatal("rate limit should retry the definitively rejected request", calls.Load())
	}
	jobs, err := c.jobs()
	if err != nil || jobs["items"].([]M)[0]["accepted"] != 1 {
		t.Fatal(jobs, err)
	}
}

func TestWhatsAppRetiredImportedVersionIsRejectedBeforeSavingOrContactingMeta(t *testing.T) {
	c, _ := reportCloudFixture(t)
	var requests atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		requests.Add(1)
		t.Error("retired API version reached Meta")
	}))
	defer provider.Close()
	c.baseURL = provider.URL
	c.client = provider.Client()
	_, err := c.saveConfig(M{"phoneNumberId": "123456", "businessAccountId": "654321", "apiVersion": "v20.0"})
	if err == nil || !strings.Contains(safeSendError(err), "v20.0") {
		t.Fatal("retired imported version needs a clear correction", err)
	}
	config, err := c.config()
	if err != nil || config.APIVersion != "v23.0" {
		t.Fatal("invalid import overwrote working account", err)
	}
	config.APIVersion = "v20.0"
	_, err = c.metaRequest(context.Background(), config, metaCall{method: "GET", path: "654321/message_templates"})
	if err == nil || requests.Load() != 0 {
		t.Fatal("stored retired configuration must require an explicit upgrade", err)
	}
}
