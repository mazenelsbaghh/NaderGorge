package main

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"sync/atomic"
	"testing"
	"time"
)

func TestGradeBatchFailureLeavesOtherFourRequestsRunning(t *testing.T) {
	cloud, body := reportCloudFixture(t)
	original, err := attempt(cloud.app.db.sql, str(body["attemptId"]))
	if err != nil {
		t.Fatal(err)
	}
	tasks := []gradingTask{{attemptID: "missing-attempt", questionID: "bad"}}
	for i := 0; i < 4; i++ {
		aid := id()
		cloneReportAttempt(t, cloud, original.ID, aid, original.ExamID)
		tasks = append(tasks, gradingTask{attemptID: aid, questionID: "essay", answer: "صحيح", points: 2})
	}
	var count atomic.Int32
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		count.Add(1)
		time.Sleep(50 * time.Millisecond)
		json.NewEncoder(w).Encode(M{"candidates": []M{{"content": M{"parts": []M{{"text": encode(M{"correct": true, "feedback": "صحيح"})}}}}}})
	}))
	defer provider.Close()
	old := geminiEndpoint
	geminiEndpoint = provider.URL
	defer func() { geminiEndpoint = old }()
	g := &grader{db: cloud.app.db, envPath: filepath.Join(t.TempDir(), ".env")}
	if err := g.gradeBatch(context.Background(), "isolation", tasks, "test-key"); err == nil {
		t.Fatal("database failure disappeared")
	}
	if count.Load() != 4 {
		t.Fatalf("only %d requests completed", count.Load())
	}
	for _, task := range tasks[1:] {
		saved, err := attempt(g.db.sql, task.attemptID)
		if err != nil || decode(saved.Grades)["essay"] == nil {
			t.Fatal("sibling grade lost", err)
		}
	}
}

func TestLongFeedbackIsValidAndThoughtPartsAreIgnored(t *testing.T) {
	provider := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		json.NewEncoder(w).Encode(M{"candidates": []M{{"content": M{"parts": []M{{"thought": true, "text": "internal reasoning"}, {"text": encode(M{"correct": true, "feedback": strings.Repeat("أ", 1000)})}}}}}})
	}))
	defer provider.Close()
	old := geminiEndpoint
	geminiEndpoint = provider.URL
	defer func() { geminiEndpoint = old }()
	result, err := geminiGrade(context.Background(), "test-key", gradingTask{points: 2})
	if err != nil || result["correct"] != true || len([]rune(str(result["feedback"]))) != 1000 {
		t.Fatal("valid result discarded", result, err)
	}
}
