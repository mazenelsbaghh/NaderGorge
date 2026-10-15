package main

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

func reportCloudFixture(t *testing.T) (*whatsappCloud, M) {
	t.Helper()
	d, err := openDB(filepath.Join(t.TempDir(), "exam.sqlite3"))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { d.sql.Close() })
	config := demoExam()
	config["questions"] = config["questions"].([]any)[:1]
	config["questionCount"] = float64(1)
	created, err := d.createExam(config)
	if err != nil {
		t.Fatal(err)
	}
	eid := str(created["id"])
	if _, err = d.changeState(eid, "publish"); err != nil {
		t.Fatal(err)
	}
	_, studentToken, err := d.join(M{"name": "طالب تجريبي", "code": "50000", "phone": "01012345678"}, "")
	if err != nil {
		t.Fatal(err)
	}
	if _, err = d.changeState(eid, "start"); err != nil {
		t.Fatal(err)
	}
	session, err := d.session(studentToken)
	if err != nil {
		t.Fatal(err)
	}
	questionID := str(session["session"].(M)["questions"].([]M)[0]["id"])
	if _, err = d.saveAnswers(studentToken, M{"revision": float64(1), "submit": true, "answers": M{questionID: float64(1)}}); err != nil {
		t.Fatal(err)
	}
	dashboard, err := d.dashboard(eid)
	if err != nil {
		t.Fatal(err)
	}
	aid := str(dashboard["attempts"].([]M)[0]["id"])
	// Execute the native test binary as the external renderer boundary on every OS.
	// The real packaged PDF worker is exercised separately by the Windows build.
	renderer, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	t.Setenv("MASSAR_TEST_PDF_RENDERER", "1")
	a := &app{db: d, pdfHelper: renderer}
	cloud := newWhatsAppCloud(a)
	a.whatsapp = cloud
	_, err = cloud.saveConfig(M{"phoneNumberId": "123456", "businessAccountId": "654321", "apiVersion": "v23.0", "accessToken": "test-token-that-is-never-a-real-token"})
	if err != nil {
		t.Fatal(err)
	}
	template := M{"id": "1", "name": "exam_report", "language": "ar", "status": "APPROVED", "category": "UTILITY", "components": []any{M{"type": "HEADER", "format": "DOCUMENT"}, M{"type": "BODY", "text": "أهلًا {{1}}، نتيجتك {{2}}"}}}
	cache := M{"templates": []any{template}, "syncedAt": now(), "phoneNumberId": "123456", "businessAccountId": "654321"}
	if _, err = d.sql.Exec(`INSERT INTO app_settings(key,value) VALUES('whatsapp_cloud_templates',?)`, encode(cache)); err != nil {
		t.Fatal(err)
	}
	body := M{"attemptId": aid, "templateId": "1", "mappings": []any{M{"source": "name"}, M{"source": "result"}}}
	prepared, err := cloud.prepare(body)
	if err != nil {
		t.Fatal(err)
	}
	body["fingerprint"] = prepared.fingerprint
	return cloud, body
}

func TestWhatsAppReportsUseStudentDataAndAreNotSentTwice(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	var sends atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("Authorization") != "Bearer test-token-that-is-never-a-real-token" {
			t.Error("missing provider authorization")
		}
		if strings.HasSuffix(r.URL.Path, "/media") {
			if err := r.ParseMultipartForm(1 << 20); err != nil {
				t.Error(err)
				w.WriteHeader(400)
				return
			}
			defer r.MultipartForm.RemoveAll()
			file, header, err := r.FormFile("file")
			if err != nil {
				t.Error(err)
				return
			}
			defer file.Close()
			pdf, _ := io.ReadAll(file)
			if !strings.Contains(string(pdf), "طالب تجريبي") || header.Header.Get("Content-Type") != "application/pdf" || !strings.Contains(header.Filename, "2 من 2") {
				t.Error("wrong student PDF")
			}
			json.NewEncoder(w).Encode(M{"id": "media-1"})
			return
		}
		sends.Add(1)
		var payload M
		if err := json.NewDecoder(r.Body).Decode(&payload); err != nil {
			t.Error(err)
		}
		if payload["to"] != "201012345678" || !strings.Contains(encode(payload), "2 من 2") || !strings.Contains(encode(payload), "media-1") {
			t.Error("wrong recipient or report parameters")
		}
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "message-1"}}})
	}))
	defer provider.Close()
	cloud.baseURL = provider.URL
	cloud.client = provider.Client()
	for i := 0; i < 2; i++ {
		reply, err := cloud.sendReport(context.Background(), body)
		if err != nil {
			t.Fatal(err)
		}
		if reply["record"].(M)["state"] != "accepted" {
			t.Fatal(reply)
		}
	}
	if sends.Load() != 1 {
		t.Fatal("duplicate provider message")
	}
	history, err := cloud.history()
	if err != nil || len(history["items"].([]M)) != 1 {
		t.Fatal("missing durable send history", err)
	}
}

func TestWhatsAppUnconfirmedMessageIsNotRetried(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	var sends atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/media") {
			json.NewEncoder(w).Encode(M{"id": "media-1"})
			return
		}
		sends.Add(1)
		conn, _, err := w.(http.Hijacker).Hijack()
		if err != nil {
			t.Error(err)
			return
		}
		conn.Close()
	}))
	defer provider.Close()
	cloud.baseURL = provider.URL
	cloud.client = provider.Client()
	reply, err := cloud.sendReport(context.Background(), body)
	if err != nil || reply["record"].(M)["state"] != "unknown" {
		t.Fatal(reply, err)
	}
	body["retry"] = true
	if _, err = cloud.sendReport(context.Background(), body); err != nil {
		t.Fatal(err)
	}
	if sends.Load() != 1 {
		t.Fatal("retried ambiguous send")
	}
}

func TestWhatsAppChangedReportBlocksApprovedPreview(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	if _, err := cloud.app.db.sql.Exec(`UPDATE attempts SET name='اسم مصحح' WHERE id=?`, body["attemptId"]); err != nil {
		t.Fatal(err)
	}
	if _, err := cloud.sendReport(context.Background(), body); err == nil {
		t.Fatal("stale report was sent")
	}
	if history, err := cloud.history(); err != nil || len(history["items"].([]M)) != 0 {
		t.Fatal("stale preview created send record")
	}
}

func TestWhatsAppCredentialsStayOutsidePublicResponsesAndBackup(t *testing.T) {
	cloud, _ := reportCloudFixture(t)
	settings, err := cloud.settings()
	if err != nil {
		t.Fatal(err)
	}
	if strings.Contains(encode(settings), "test-token-") {
		t.Fatal("token in settings response")
	}
	file, err := os.Stat(cloud.path)
	if err != nil || (runtime.GOOS != "windows" && file.Mode().Perm() != 0600) {
		t.Fatal("private configuration permissions", err)
	}
	if runtime.GOOS == "windows" {
		raw, readErr := os.ReadFile(cloud.path)
		if readErr != nil || strings.Contains(string(raw), "test-token-") {
			t.Fatal("Windows credentials were not protected", readErr)
		}
	}
	var leaked int
	if err = cloud.app.db.sql.QueryRow(`SELECT COUNT(*) FROM app_settings WHERE value LIKE '%test-token-%'`).Scan(&leaked); err != nil || leaked != 0 {
		t.Fatal("token stored in database", err)
	}
}

func TestWhatsAppSyncVerifiesAccountAndDocumentTemplatePolicy(t *testing.T) {
	cloud, _ := reportCloudFixture(t)
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/phone_numbers") {
			json.NewEncoder(w).Encode(M{"data": []M{{"id": "wrong-sender"}}})
			return
		}
		t.Error("fetched templates before verifying account")
	}))
	defer provider.Close()
	cloud.baseURL = provider.URL
	cloud.client = provider.Client()
	if _, err := cloud.syncTemplates(context.Background()); err == nil {
		t.Fatal("accepted mismatched account")
	}
	cases := []struct {
		name, status, header, text string
		allowed                    bool
	}{{"document", "APPROVED", "DOCUMENT", "أهلا {{1}}", true}, {"pending", "PENDING", "DOCUMENT", "أهلا {{1}}", false}, {"image", "APPROVED", "IMAGE", "أهلا {{1}}", false}, {"named", "APPROVED", "DOCUMENT", "أهلا {{name}}", true}, {"gap", "APPROVED", "DOCUMENT", "أهلا {{2}}", false}}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			_, reason := reportTemplateFields(M{"status": tc.status, "category": "UTILITY", "components": []any{M{"type": "HEADER", "format": tc.header}, M{"type": "BODY", "text": tc.text}}})
			if (reason == "") != tc.allowed {
				t.Fatal(reason)
			}
		})
	}
}

func TestWhatsAppRetriesOnlyDefinitivelyUnsentReports(t *testing.T) {
	for _, tc := range []struct {
		name     string
		status   int
		upload   bool
		state    string
		requests int
	}{{"upload rejected", 400, true, "failed", 2}, {"message rejected", 400, false, "rejected", 2}, {"provider unavailable", 503, false, "unknown", 1}} {
		t.Run(tc.name, func(t *testing.T) {
			cloud, body := reportCloudFixture(t)
			var requests atomic.Int32
			provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				if strings.HasSuffix(r.URL.Path, "/media") && !tc.upload {
					json.NewEncoder(w).Encode(M{"id": "media-1"})
					return
				}
				requests.Add(1)
				w.WriteHeader(tc.status)
				json.NewEncoder(w).Encode(M{"error": M{"code": 100}})
			}))
			defer provider.Close()
			cloud.baseURL = provider.URL
			cloud.client = provider.Client()
			reply, err := cloud.sendReport(context.Background(), body)
			if err != nil || reply["record"].(M)["state"] != tc.state {
				t.Fatal(reply, err)
			}
			if _, err = cloud.sendReport(context.Background(), body); err != nil {
				t.Fatal(err)
			}
			body["retry"] = true
			if _, err = cloud.sendReport(context.Background(), body); err != nil {
				t.Fatal(err)
			}
			if requests.Load() != int32(tc.requests) {
				t.Fatal("unsafe or missing retry", requests.Load())
			}
		})
	}
}

func TestWhatsAppRestartPreservesAmbiguousSendProtection(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	_, err := cloud.app.db.sql.Exec(`INSERT INTO whatsapp_report_sends(fingerprint,attempt_id,recipient,template_name,filename,state,created_at,updated_at) VALUES(?,?,?,'exam_report','report.pdf','sending',?,?)`, body["fingerprint"], body["attemptId"], "201012345678", now(), now())
	if err != nil {
		t.Fatal(err)
	}
	if err = cloud.recoverSends(); err != nil {
		t.Fatal(err)
	}
	body["retry"] = true
	reply, err := cloud.sendReport(context.Background(), body)
	if err != nil || reply["record"].(M)["state"] != "unknown" || reply["replayed"] != true {
		t.Fatal("lost restart send protection", err)
	}
}

func TestWhatsAppStaleTemplatesRefreshAutomaticallyOnce(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	cache, _ := cloud.templates()
	cache["syncedAt"] = now() - 700
	if _, err := cloud.app.db.sql.Exec(`UPDATE app_settings SET value=? WHERE key='whatsapp_cloud_templates'`, encode(cache)); err != nil {
		t.Fatal(err)
	}
	var refreshes atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch {
		case strings.Contains(r.URL.Path, "phone_numbers"):
			json.NewEncoder(w).Encode(M{"data": []M{{"id": "123456"}}})
		case strings.Contains(r.URL.Path, "message_templates"):
			refreshes.Add(1)
			json.NewEncoder(w).Encode(M{"data": cache["templates"]})
		default:
			t.Error("unexpected Meta request", r.URL.Path)
			w.WriteHeader(500)
		}
	}))
	defer provider.Close()
	cloud.baseURL = provider.URL
	cloud.client = provider.Client()
	for range 2 {
		result, err := cloud.currentTemplates(context.Background())
		if err != nil || result["syncError"] != nil || now()-num(result["syncedAt"]) > 1 {
			t.Fatal("automatic refresh failed", result, err)
		}
	}
	if refreshes.Load() != 1 {
		t.Fatal("fresh cache fetched repeatedly", refreshes.Load())
	}
	if _, err := cloud.prepare(body); err != nil {
		t.Fatal("fresh template unusable", err)
	}
}

func TestWhatsAppFiveReportsUploadConcurrentlyWithoutDuplicateClaims(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	arrived := make(chan struct{}, 5)
	release := make(chan struct{})
	var sends atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/media") {
			arrived <- struct{}{}
			<-release
			json.NewEncoder(w).Encode(M{"id": "media-1"})
			return
		}
		sends.Add(1)
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "message-1"}}})
	}))
	defer provider.Close()
	cloud.baseURL = provider.URL
	cloud.client = provider.Client()
	outcomes := make(chan error, 5)
	for i := range 5 {
		candidate := decode(encode(body))
		if i > 0 {
			source, err := attempt(cloud.app.db.sql, str(body["attemptId"]))
			if err != nil {
				t.Fatal(err)
			}
			cloneID := fmt.Sprint("parallel-student-", i)
			cloneReportAttempt(t, cloud, source.ID, cloneID, source.ExamID)
			candidate["attemptId"] = cloneID
		}
		candidate["mappings"] = []any{M{"source": "name"}, M{"source": "literal", "text": fmt.Sprint(i)}}
		prepared, err := cloud.prepare(candidate)
		if err != nil {
			t.Fatal(err)
		}
		candidate["fingerprint"] = prepared.fingerprint
		go func() { _, err := cloud.sendReport(context.Background(), candidate); outcomes <- err }()
	}
	timer := time.NewTimer(10 * time.Second)
	defer timer.Stop()
	for range 5 {
		select {
		case <-arrived:
		case <-timer.C:
			close(release)
			for range 5 {
				<-outcomes
			}
			t.Fatal("five uploads did not overlap")
		}
	}
	// Replaying an accepted report must not start another upload.
	duplicate := decode(encode(body))
	duplicate["mappings"] = []any{M{"source": "name"}, M{"source": "literal", "text": "0"}}
	prepared, err := cloud.prepare(duplicate)
	if err != nil {
		t.Fatal(err)
	}
	duplicate["fingerprint"] = prepared.fingerprint
	close(release)
	for range 5 {
		if err := <-outcomes; err != nil {
			t.Fatal(err)
		}
	}
	if _, err = cloud.sendReport(context.Background(), duplicate); err != nil {
		t.Fatal(err)
	}
	if sends.Load() != 5 {
		t.Fatal("duplicate sends", sends.Load())
	}
}

func TestWhatsAppOfflineRefreshRetainsCacheButBlocksSending(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	cache, _ := cloud.templates()
	cache["syncedAt"] = now() - 700
	if _, err := cloud.app.db.sql.Exec(`UPDATE app_settings SET value=? WHERE key='whatsapp_cloud_templates'`, encode(cache)); err != nil {
		t.Fatal(err)
	}
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) { w.WriteHeader(503) }))
	defer provider.Close()
	cloud.baseURL = provider.URL
	cloud.client = provider.Client()
	cached, err := cloud.currentTemplates(context.Background())
	if err != nil || cached["syncError"] == nil || len(cached["templates"].([]any)) != 1 {
		t.Fatal("offline cache lost", cached, err)
	}
	if _, err = cloud.sendReport(context.Background(), body); err == nil {
		t.Fatal("stale offline send allowed")
	}
	history, err := cloud.history()
	if err != nil || len(history["items"].([]M)) != 0 {
		t.Fatal("offline send created a claim", history, err)
	}
}

func TestWhatsAppConcurrentDuplicateReturnsInFlightClaim(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	arrived := make(chan struct{}, 1)
	release := make(chan struct{})
	var sends atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/media") {
			arrived <- struct{}{}
			<-release
			json.NewEncoder(w).Encode(M{"id": "media-1"})
			return
		}
		sends.Add(1)
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "message-1"}}})
	}))
	defer provider.Close()
	cloud.baseURL = provider.URL
	cloud.client = provider.Client()
	first := make(chan error, 1)
	go func() { _, err := cloud.sendReport(context.Background(), body); first <- err }()
	select {
	case <-arrived:
	case <-time.After(10 * time.Second):
		close(release)
		<-first
		t.Fatal("upload never started")
	}
	reply, err := cloud.sendReport(context.Background(), body)
	close(release)
	firstErr := <-first
	if err != nil || firstErr != nil || reply["replayed"] != true || reply["record"].(M)["state"] != "preparing" {
		t.Fatal("concurrent duplicate not blocked", reply, err, firstErr)
	}
	if sends.Load() != 1 {
		t.Fatal("duplicate provider messages", sends.Load())
	}
}

func TestAcceptedReportIsNotResentWhenNameOrTemplateParametersChange(t *testing.T) {
	c, body := reportCloudFixture(t)
	var sends atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/media") {
			json.NewEncoder(w).Encode(M{"id": "media"})
			return
		}
		sends.Add(1)
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "message"}}})
	}))
	defer provider.Close()
	c.baseURL = provider.URL
	c.client = provider.Client()
	if _, err := c.sendReport(context.Background(), body); err != nil {
		t.Fatal(err)
	}
	if _, err := c.app.db.sql.Exec(`UPDATE attempts SET name='اسم مصحح' WHERE id=?`, body["attemptId"]); err != nil {
		t.Fatal(err)
	}
	prepared, err := c.prepare(body)
	if err != nil {
		t.Fatal(err)
	}
	body["fingerprint"] = prepared.fingerprint
	body["retry"] = true
	reply, err := c.sendReport(context.Background(), body)
	if err != nil || reply["replayed"] != true || sends.Load() != 1 {
		t.Fatal("accepted student resent", reply, err, sends.Load())
	}
}
