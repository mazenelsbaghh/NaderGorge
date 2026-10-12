package main

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

func cloneReportAttempt(t *testing.T, c *whatsappCloud, source, id, examID string) {
	t.Helper()
	_, err := c.app.db.sql.Exec(`INSERT INTO attempts(id,exam_id,code,name,phone,joined_at,last_seen,question_ids,answers,revision,submitted_at,grades,deadline) SELECT ?,?, ?,name,phone,joined_at,last_seen,question_ids,answers,revision,submitted_at,grades,deadline FROM attempts WHERE id=?`, id, examID, id, source)
	if err != nil {
		t.Fatal(err)
	}
}
func queuePayload(t *testing.T, c *whatsappCloud, body M, ids []string) M {
	t.Helper()
	items := []any{}
	for _, id := range ids {
		prepared, err := c.prepare(M{"attemptId": id, "templateId": body["templateId"], "mappings": body["mappings"]})
		if err != nil {
			t.Fatal(err)
		}
		items = append(items, M{"attemptId": id, "fingerprint": prepared.fingerprint})
	}
	return M{"items": items, "templateId": body["templateId"], "mappings": body["mappings"]}
}
func waitReportQueue(t *testing.T, c *whatsappCloud) {
	t.Helper()
	done := make(chan struct{})
	go func() { c.jobsWG.Wait(); close(done) }()
	select {
	case <-done:
	case <-time.After(20 * time.Second):
		t.Fatal("background queue did not finish")
	}
}

func TestBackgroundReportsContinueAcrossExamsWithFiveSecondBatchGap(t *testing.T) {
	c, body := reportCloudFixture(t)
	original, err := attempt(c.app.db.sql, str(body["attemptId"]))
	if err != nil {
		t.Fatal(err)
	}
	ids := []string{original.ID}
	for i := range 5 {
		id := fmt.Sprint("clone-", i)
		cloneReportAttempt(t, c, original.ID, id, original.ExamID)
		ids = append(ids, id)
	}
	var config string
	if err = c.app.db.sql.QueryRow(`SELECT config FROM exams WHERE id=?`, original.ExamID).Scan(&config); err != nil {
		t.Fatal(err)
	}
	secondConfig := decode(config)
	secondConfig["title"] = "امتحان آخر"
	if _, err = c.app.db.sql.Exec(`INSERT INTO exams(id,config,state,created_at) VALUES('second-exam',?,'closed',?)`, encode(secondConfig), now()); err != nil {
		t.Fatal(err)
	}
	cloneReportAttempt(t, c, original.ID, "second-student", "second-exam")
	firstPayload := queuePayload(t, c, body, ids)
	secondPayload := queuePayload(t, c, body, []string{"second-student"})
	arrived := make(chan struct{}, 10)
	release := make(chan struct{})
	var uploads, sends atomic.Int32
	sixthAt := make(chan time.Time, 1)
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/media") {
			n := uploads.Add(1)
			if n <= 5 {
				arrived <- struct{}{}
				<-release
			} else if n == 6 {
				sixthAt <- time.Now()
			}
			json.NewEncoder(w).Encode(M{"id": "media"})
			return
		}
		sends.Add(1)
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "message"}}})
	}))
	defer provider.Close()
	c.baseURL = provider.URL
	c.client = provider.Client()
	first, err := c.enqueueReports(context.Background(), firstPayload)
	if err != nil {
		t.Fatal(err)
	}
	for range 5 {
		select {
		case <-arrived:
		case <-time.After(10 * time.Second):
			close(release)
			waitReportQueue(t, c)
			t.Fatal("background batch was serialized")
		}
	}
	second, enqueueErr := c.enqueueReports(context.Background(), secondPayload)
	_, duplicateErr := c.enqueueReports(context.Background(), firstPayload)
	if enqueueErr == nil {
		jobs, _ := c.jobs()
		found := false
		for _, j := range jobs["items"].([]M) {
			if j["id"] == second["jobId"] {
				found = j["state"] == "queued"
			}
		}
		if !found {
			enqueueErr = fmt.Errorf("second exam not queued")
		}
	}
	releasedAt := time.Now()
	close(release)
	waitReportQueue(t, c)
	if enqueueErr != nil || duplicateErr == nil {
		t.Fatal("queue acceptance or duplicate guard failed", enqueueErr, duplicateErr)
	}
	select {
	case stamp := <-sixthAt:
		if stamp.Sub(releasedAt) < 5*time.Second {
			t.Fatal("batch gap shorter than five seconds")
		}
	default:
		t.Fatal("second batch not sent")
	}
	if sends.Load() != 7 {
		t.Fatal("reports lost", sends.Load())
	}
	jobs, err := c.jobs()
	if err != nil {
		t.Fatal(err)
	}
	for _, j := range jobs["items"].([]M) {
		if j["state"] != "completed" || j["processed"] != j["total"] || j["failed"] != 0 {
			t.Fatal("incorrect job state", j)
		}
	}
	if first["jobId"] == second["jobId"] {
		t.Fatal("jobs lost identity")
	}
}

func TestBackgroundCancellationStopsNextBatchAndQueueRecoveryRequiresReview(t *testing.T) {
	c, body := reportCloudFixture(t)
	c.batchGap = 0
	a, err := attempt(c.app.db.sql, str(body["attemptId"]))
	if err != nil {
		t.Fatal(err)
	}
	ids := []string{a.ID}
	for i := range 5 {
		id := fmt.Sprint("cancel-", i)
		cloneReportAttempt(t, c, a.ID, id, a.ExamID)
		ids = append(ids, id)
	}
	payload := queuePayload(t, c, body, ids)
	arrived := make(chan struct{}, 5)
	release := make(chan struct{})
	var sends atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/media") {
			arrived <- struct{}{}
			<-release
			json.NewEncoder(w).Encode(M{"id": "media"})
			return
		}
		sends.Add(1)
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "message"}}})
	}))
	defer provider.Close()
	c.baseURL = provider.URL
	c.client = provider.Client()
	job, err := c.enqueueReports(context.Background(), payload)
	if err != nil {
		t.Fatal(err)
	}
	for range 5 {
		select {
		case <-arrived:
		case <-time.After(10 * time.Second):
			close(release)
			waitReportQueue(t, c)
			t.Fatal("first batch never started")
		}
	}
	_, stopErr := c.stopJob(str(job["jobId"]))
	close(release)
	waitReportQueue(t, c)
	if stopErr != nil || sends.Load() != 5 {
		t.Fatal("cancel did not stop sixth report", stopErr, sends.Load())
	}
	jobs, _ := c.jobs()
	if jobs["items"].([]M)[0]["state"] != "stopped" {
		t.Fatal(jobs)
	}
	_, err = c.app.db.sql.Exec(`INSERT INTO whatsapp_report_jobs(id,title,state,payload,total,created_at,updated_at) VALUES('recovery','old','queued','{}',1,?,?)`, now(), now())
	if err != nil {
		t.Fatal(err)
	}
	if err = c.recoverSends(); err != nil {
		t.Fatal(err)
	}
	var state string
	if err = c.app.db.sql.QueryRow(`SELECT state FROM whatsapp_report_jobs WHERE id='recovery'`).Scan(&state); err != nil || state != "interrupted" {
		t.Fatal("restart resumed an unreviewed send", state, err)
	}
}

func TestBackgroundRetriesUploadOutageWithoutResendingAcceptedStudents(t *testing.T) {
	c, body := reportCloudFixture(t)
	c.batchGap = 30 * time.Millisecond
	a, err := attempt(c.app.db.sql, str(body["attemptId"]))
	if err != nil {
		t.Fatal(err)
	}
	ids := []string{a.ID}
	for i := 0; i < 5; i++ {
		aid := fmt.Sprint("resume-", i)
		cloneReportAttempt(t, c, a.ID, aid, a.ExamID)
		ids = append(ids, aid)
	}
	body["mappings"] = []any{M{"source": "code"}, M{"source": "result"}}
	payload := queuePayload(t, c, body, ids)
	var uploads atomic.Int32
	var mu sync.Mutex
	sends := map[string]int{}
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/media") {
			if uploads.Add(1) == 1 {
				w.WriteHeader(503)
				json.NewEncoder(w).Encode(M{"error": M{"code": 2}})
				return
			}
			json.NewEncoder(w).Encode(M{"id": "media"})
			return
		}
		var request struct {
			Template struct {
				Components []struct {
					Type       string
					Parameters []struct{ Text string }
				}
			}
		}
		if err := json.NewDecoder(r.Body).Decode(&request); err != nil {
			t.Error(err)
			w.WriteHeader(400)
			return
		}
		code := request.Template.Components[1].Parameters[0].Text
		mu.Lock()
		sends[code]++
		mu.Unlock()
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "message"}}})
	}))
	defer provider.Close()
	c.baseURL = provider.URL
	c.client = provider.Client()
	if _, err := c.enqueueReports(context.Background(), payload); err != nil {
		t.Fatal(err)
	}
	waitReportQueue(t, c)
	if uploads.Load() != 7 {
		t.Fatal("failed upload was not retried exactly once", uploads.Load())
	}
	mu.Lock()
	defer mu.Unlock()
	if len(sends) != 6 {
		t.Fatal("missing students", sends)
	}
	for _, count := range sends {
		if count != 1 {
			t.Fatal("accepted student sent twice", sends)
		}
	}
	jobs, _ := c.jobs()
	job := jobs["items"].([]M)[0]
	if job["state"] != "completed" || job["processed"] != 6 || job["accepted"] != 6 || job["failed"] != 0 {
		t.Fatal("retry counters wrong", job)
	}
}

func TestBackgroundUnknownMessageDoesNotStopOrRepeatOtherStudents(t *testing.T) {
	c, body := reportCloudFixture(t)
	c.batchGap = 0
	a, err := attempt(c.app.db.sql, str(body["attemptId"]))
	if err != nil {
		t.Fatal(err)
	}
	ids := []string{a.ID}
	for i := 0; i < 5; i++ {
		aid := fmt.Sprint("unknown-", i)
		cloneReportAttempt(t, c, a.ID, aid, a.ExamID)
		ids = append(ids, aid)
	}
	var sends atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if strings.HasSuffix(r.URL.Path, "/media") {
			json.NewEncoder(w).Encode(M{"id": "media"})
			return
		}
		if sends.Add(1) == 1 {
			w.WriteHeader(503)
			json.NewEncoder(w).Encode(M{"error": M{"code": 2}})
			return
		}
		json.NewEncoder(w).Encode(M{"messages": []M{{"id": "message"}}})
	}))
	defer provider.Close()
	c.baseURL = provider.URL
	c.client = provider.Client()
	if _, err := c.enqueueReports(context.Background(), queuePayload(t, c, body, ids)); err != nil {
		t.Fatal(err)
	}
	waitReportQueue(t, c)
	if sends.Load() != 6 {
		t.Fatal("unknown retried or later students blocked", sends.Load())
	}
	jobs, _ := c.jobs()
	job := jobs["items"].([]M)[0]
	if job["processed"] != 6 || job["accepted"] != 5 || job["failed"] != 1 {
		t.Fatal("wrong uncertain counters", job)
	}
}

func TestResumeStoppedJobSkipsAlreadyAcceptedReports(t *testing.T) {
	c, body := reportCloudFixture(t)
	c.batchGap = 0
	payload := queuePayload(t, c, body, []string{str(body["attemptId"])})
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
	result, err := c.enqueueReports(context.Background(), payload)
	if err != nil {
		t.Fatal(err)
	}
	waitReportQueue(t, c)
	_, err = c.app.db.sql.Exec(`UPDATE whatsapp_report_jobs SET state='interrupted' WHERE id=?`, result["jobId"])
	if err != nil {
		t.Fatal(err)
	}
	if _, err = c.resumeJob(str(result["jobId"])); err != nil {
		t.Fatal(err)
	}
	waitReportQueue(t, c)
	if sends.Load() != 1 {
		t.Fatal("resume duplicated an accepted message", sends.Load())
	}
	jobs, _ := c.jobs()
	job := jobs["items"].([]M)[0]
	if job["state"] != "completed" || job["accepted"] != 1 || job["processed"] != 1 {
		t.Fatal("resume lost progress", job)
	}
}
