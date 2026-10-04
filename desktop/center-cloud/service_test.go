package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"sync"
	"testing"
)

const testDeviceToken = "synthetic-device-token-for-tests-only"
const testOtherToken = "synthetic-other-center-token-for-tests-only"
const testAdminToken = "synthetic-admin-token-for-tests-only"
const testUploadID = "11111111-1111-4111-8111-111111111111"

// Real TLS and disk storage exercise the public protocol, including restarts.
type serviceFixture struct {
	config config
	server *httptest.Server
}

func newFixture(t *testing.T) *serviceFixture {
	t.Helper()
	root := t.TempDir()
	c := config{storageDir: filepath.Join(root, "private"), updatesDir: filepath.Join(root, "releases"), maxUploadBytes: 128 * 1024 * 1024,
		devices:   map[string][32]byte{"test-center": sha256.Sum256([]byte(testDeviceToken)), "other-center": sha256.Sum256([]byte(testOtherToken))},
		adminHash: sha256.Sum256([]byte(testAdminToken))}
	if err := os.Mkdir(c.updatesDir, 0700); err != nil {
		t.Fatal(err)
	}
	fixture := &serviceFixture{config: c}
	fixture.start(t)
	t.Cleanup(func() { fixture.server.Close() })
	return fixture
}
func (f *serviceFixture) start(t *testing.T) {
	t.Helper()
	service, err := newService(f.config)
	if err != nil {
		t.Fatal(err)
	}
	f.server = httptest.NewTLSServer(service)
}
func (f *serviceFixture) request(t *testing.T, method, path, token string, body []byte) (int, []byte, http.Header) {
	t.Helper()
	request, err := http.NewRequest(method, f.server.URL+path, bytes.NewReader(body))
	if err != nil {
		t.Fatal(err)
	}
	if token != "" {
		request.Header.Set("Authorization", "Bearer "+token)
	}
	if body != nil {
		request.Header.Set("Content-Type", "application/json")
	}
	response, err := f.server.Client().Do(request)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	data, err := io.ReadAll(response.Body)
	if err != nil {
		t.Fatal(err)
	}
	return response.StatusCode, data, response.Header
}
func uploadBody(t *testing.T, mutate func(map[string]any)) []byte {
	t.Helper()
	envelope := map[string]any{"format": "massar-support-upload-v1", "kind": "database", "uploadId": testUploadID, "centerId": "test-center", "createdAt": "2026-10-03T12:00:00Z",
		"app":  map[string]any{"version": "1.0.0+1", "build": "0123456789abcdef", "role": "host", "os": "windows"},
		"data": map[string]any{"schemaVersion": 8, "ledgerInteger": json.Number("9007199254740993"), "studentNote": "synthetic private logical backup"}, "diagnostics": ""}
	if mutate != nil {
		mutate(envelope)
	}
	body, err := json.Marshal(envelope)
	if err != nil {
		t.Fatal(err)
	}
	return body
}
func decodeReceipt(t *testing.T, data []byte) uploadReceipt {
	t.Helper()
	var receipt uploadReceipt
	if err := json.Unmarshal(data, &receipt); err != nil {
		t.Fatal(err)
	}
	if !uuidPattern.MatchString(receipt.ReceiptID) || receipt.UploadID != testUploadID || !validUTC(receipt.ReceivedAt) || !hashPattern.MatchString(receipt.SHA256) {
		t.Fatalf("invalid receipt: %s", data)
	}
	return receipt
}
func requireStatus(t *testing.T, actual, expected int, body []byte) {
	t.Helper()
	if actual != expected {
		t.Fatalf("HTTP%d, want%d: %s", actual, expected, body)
	}
}

func TestUploadSurvivesRestartAndLogicalRetryWithoutReplacingEvidence(t *testing.T) {
	f := newFixture(t)
	body := uploadBody(t, nil)
	status, response, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
	requireStatus(t, status, 201, response)
	receipt := decodeReceipt(t, response)
	if receipt.SHA256 != sumHex(body) || receipt.CenterID != "test-center" {
		t.Fatal("receipt not bound to exact logical envelope and center")
	}
	f.server.Close()
	f.start(t)
	var reformatted bytes.Buffer
	if err := json.Indent(&reformatted, body, "", "  "); err != nil {
		t.Fatal(err)
	}
	status, response, _ = f.request(t, "POST", "/v1/uploads", testDeviceToken, reformatted.Bytes())
	requireStatus(t, status, 200, response)
	if repeated := decodeReceipt(t, response); repeated != receipt {
		t.Fatal("restart/retry replaced original durable receipt")
	}
	changed := uploadBody(t, func(e map[string]any) { e["data"].(map[string]any)["studentNote"] = "changed" })
	status, response, _ = f.request(t, "POST", "/v1/uploads", testDeviceToken, changed)
	requireStatus(t, status, 409, response)
	other := uploadBody(t, func(e map[string]any) { e["centerId"] = "other-center" })
	status, response, _ = f.request(t, "POST", "/v1/uploads", testOtherToken, other)
	requireStatus(t, status, 409, response)
	status, bundle, _ := f.request(t, "GET", "/v1/uploads/"+testUploadID, testAdminToken, nil)
	requireStatus(t, status, 200, bundle)
	if sumHex(bundle) != receipt.BundleSHA256 || int64(len(bundle)) != receipt.Size || !bytes.Contains(bundle, []byte("9007199254740993")) || bytes.Contains(bundle, []byte("changed")) {
		t.Fatal("saved logical data or original evidence changed")
	}
	status, list, _ := f.request(t, "GET", "/v1/uploads?limit=1", testAdminToken, nil)
	requireStatus(t, status, 200, list)
	var listing struct {
		Uploads []uploadReceipt `json:"uploads"`
	}
	if json.Unmarshal(list, &listing) != nil || len(listing.Uploads) != 1 || listing.Uploads[0] != receipt {
		t.Fatal("metadata differs from durable receipt")
	}
	if runtime.GOOS != "windows" {
		for _, name := range []string{"bundle.json", "receipt.json"} {
			info, err := os.Stat(filepath.Join(f.config.storageDir, testUploadID, name))
			if err != nil || info.Mode().Perm() != 0600 {
				t.Fatal("private file permissions lost")
			}
		}
		info, err := os.Stat(f.config.storageDir)
		if err != nil || info.Mode().Perm() != 0700 {
			t.Fatal("private directory permissions lost")
		}
	}
}

func TestDeviceAndAdminCredentialsHaveSeparateCapabilities(t *testing.T) {
	f := newFixture(t)
	for _, test := range []struct {
		name, method, path, token string
		expected                  int
	}{
		{"anonymous upload", "POST", "/v1/uploads", "", 401},
		{"admin cannot act as device", "POST", "/v1/uploads", testAdminToken, 401},
		{"device cannot list private uploads", "GET", "/v1/uploads", testDeviceToken, 401},
		{"device cannot read private upload", "GET", "/v1/uploads/" + testUploadID, testDeviceToken, 401},
		{"anonymous cannot discover update", "GET", "/v1/updates/windows-x64/host", "", 401},
		{"admin cannot use update device endpoint", "GET", "/v1/updates/windows-x64/host", testAdminToken, 401},
		{"anonymous cannot fetch zip", "GET", "/v1/releases/test.zip", "", 401},
		{"device no manifest", "GET", "/v1/updates/windows-x64/host", testDeviceToken, 204},
	} {
		t.Run(test.name, func(t *testing.T) {
			status, data, _ := f.request(t, test.method, test.path, test.token, uploadBody(t, nil))
			requireStatus(t, status, test.expected, data)
		})
	}
	status, data, _ := f.request(t, "POST", "/v1/uploads", testOtherToken, uploadBody(t, nil))
	requireStatus(t, status, 400, data)
	entries, err := os.ReadDir(f.config.storageDir)
	if err != nil {
		t.Fatal(err)
	}
	for _, entry := range entries {
		if entry.Name() != ".staging" {
			t.Fatal("rejected unauthorized request persisted data")
		}
	}
}

func TestClientUploadsDiagnosticsButNeverLogicalDatabase(t *testing.T) {
	for _, test := range []struct {
		name, kind string
		data       any
		expected   int
	}{
		{"client cannot claim database", "database", map[string]any{"credentials": "synthetic"}, 400},
		{"diagnostic kind cannot hide data", "diagnostics", map[string]any{}, 400},
		{"implicit diagnostic kind cannot hide data", "", map[string]any{}, 400},
		{"explicit diagnostics", "diagnostics", nil, 201},
		{"inferred diagnostics", "", nil, 201},
	} {
		t.Run(test.name, func(t *testing.T) {
			f := newFixture(t)
			body := uploadBody(t, func(e map[string]any) {
				e["kind"] = test.kind
				e["app"].(map[string]any)["role"] = "client"
				e["data"] = test.data
			})
			status, response, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
			requireStatus(t, status, test.expected, response)
			status, bundle, _ := f.request(t, "GET", "/v1/uploads/"+testUploadID, testAdminToken, nil)
			if test.expected == 400 {
				requireStatus(t, status, 404, bundle)
				return
			}
			requireStatus(t, status, 200, bundle)
			var saved uploadEnvelope
			if json.Unmarshal(bundle, &saved) != nil || saved.Kind != "diagnostics" || string(saved.Data) != "null" || saved.App.Role != "client" {
				t.Fatal("secondary persisted database instead of diagnostics")
			}
		})
	}
}

func TestConcurrentUploadPublishersReturnOneReceipt(t *testing.T) {
	f := newFixture(t)
	body := uploadBody(t, nil)
	start := make(chan struct{})
	type outcome struct {
		status  int
		receipt uploadReceipt
		err     error
	}
	results := make(chan outcome, 16)
	var wait sync.WaitGroup
	for index := 0; index < 16; index++ {
		wait.Add(1)
		go func() {
			defer wait.Done()
			<-start
			request, err := http.NewRequest("POST", f.server.URL+"/v1/uploads", bytes.NewReader(body))
			if err != nil {
				results <- outcome{err: err}
				return
			}
			request.Header.Set("Authorization", "Bearer "+testDeviceToken)
			request.Header.Set("Content-Type", "application/json")
			response, err := f.server.Client().Do(request)
			if err != nil {
				results <- outcome{err: err}
				return
			}
			defer response.Body.Close()
			data, err := io.ReadAll(response.Body)
			result := outcome{status: response.StatusCode, err: err}
			if result.status == 200 || result.status == 201 {
				result.err = json.Unmarshal(data, &result.receipt)
			}
			results <- result
		}()
	}
	close(start)
	wait.Wait()
	close(results)
	created := 0
	receiptID := ""
	for result := range results {
		if result.err != nil {
			t.Fatal(result.err)
		}
		switch result.status {
		case 201:
			created++
			fallthrough
		case 200:
			if receiptID == "" {
				receiptID = result.receipt.ReceiptID
			}
			if receiptID != result.receipt.ReceiptID {
				t.Fatal("concurrent writes acknowledged different receipts")
			}
		case 503: // Admission is deliberately bounded; retry exactly the same envelope.
		default:
			t.Fatalf("unexpected status%d", result.status)
		}
	}
	if created != 1 {
		t.Fatalf("created%d uploads, want1", created)
	}
	status, data, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
	requireStatus(t, status, 200, data)
	if decodeReceipt(t, data).ReceiptID != receiptID {
		t.Fatal("retry changed winner receipt")
	}
}

func TestStorageFailureCanRetryButCorruptEvidenceIsNeverReplaced(t *testing.T) {
	f := newFixture(t)
	stage := filepath.Join(f.config.storageDir, ".staging")
	preserved := stage + "-preserved"
	if err := os.Rename(stage, preserved); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(stage, []byte("synthetic disk fault"), 0600); err != nil {
		t.Fatal(err)
	}
	body := uploadBody(t, nil)
	status, data, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
	requireStatus(t, status, 503, data)
	if _, err := os.Stat(filepath.Join(f.config.storageDir, testUploadID)); !os.IsNotExist(err) {
		t.Fatal("failed storage exposed partial upload")
	}
	if err := os.Remove(stage); err != nil {
		t.Fatal(err)
	}
	if err := os.Rename(preserved, stage); err != nil {
		t.Fatal(err)
	}
	status, data, _ = f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
	requireStatus(t, status, 201, data)
	receipt := decodeReceipt(t, data)
	name := filepath.Join(f.config.storageDir, testUploadID, "bundle.json")
	corrupt := []byte("synthetic corrupted evidence")
	if err := os.WriteFile(name, corrupt, 0600); err != nil {
		t.Fatal(err)
	}
	status, data, _ = f.request(t, "GET", "/v1/uploads/"+testUploadID, testAdminToken, nil)
	requireStatus(t, status, 503, data)
	status, data, _ = f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
	requireStatus(t, status, 503, data)
	retained, err := os.ReadFile(name)
	if err != nil || !bytes.Equal(retained, corrupt) {
		t.Fatal("retry overwrote corrupt evidence")
	}
	metadata, err := os.ReadFile(filepath.Join(f.config.storageDir, testUploadID, "receipt.json"))
	if err != nil {
		t.Fatal(err)
	}
	if decodeReceipt(t, metadata) != receipt {
		t.Fatal("error replaced original receipt")
	}
}

func TestUploadRejectsBodyAndDiagnosticBoundsBeforePublishing(t *testing.T) {
	for _, test := range []struct {
		name     string
		limit    int64
		body     func(*testing.T) []byte
		expected int
	}{
		{"known oversized body", 1024, func(t *testing.T) []byte {
			return uploadBody(t, func(e map[string]any) { e["data"] = map[string]any{"blob": strings.Repeat("x", 2048)} })
		}, 413},
		{"oversized diagnostics", 128 * 1024 * 1024, func(t *testing.T) []byte {
			return uploadBody(t, func(e map[string]any) { e["diagnostics"] = strings.Repeat("x", maximumDiagnosticsBytes+1) })
		}, 400},
		{"trailing JSON", 1024, func(t *testing.T) []byte { return append(uploadBody(t, nil), []byte(" {}")...) }, 400},
	} {
		t.Run(test.name, func(t *testing.T) {
			f := newFixture(t)
			f.server.Close()
			f.config.maxUploadBytes = test.limit
			f.start(t)
			status, data, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, test.body(t))
			requireStatus(t, status, test.expected, data)
			status, data, _ = f.request(t, "GET", "/v1/uploads/"+testUploadID, testAdminToken, nil)
			requireStatus(t, status, 404, data)
		})
	}
	// Unknown-length/chunked bodies must not bypass Content-Length validation.
	f := newFixture(t)
	f.server.Close()
	f.config.maxUploadBytes = 1024
	f.start(t)
	request, err := http.NewRequest("POST", f.server.URL+"/v1/uploads", io.NopCloser(strings.NewReader(strings.Repeat("x", 2048))))
	if err != nil {
		t.Fatal(err)
	}
	request.Header.Set("Authorization", "Bearer "+testDeviceToken)
	request.Header.Set("Content-Type", "application/json")
	response, err := f.server.Client().Do(request)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	if response.StatusCode != 413 {
		t.Fatalf("chunked body bypassed bound: HTTP%d", response.StatusCode)
	}
}

func TestPlainHTTPAndUntrustedForwardedHTTPSCannotUpload(t *testing.T) {
	f := newFixture(t)
	service, err := newService(f.config)
	if err != nil {
		t.Fatal(err)
	}
	for _, test := range []struct {
		name, remote, forwarded string
		expected                int
	}{
		{"plain loopback", "127.0.0.1:1234", "", 403},
		{"external spoofed proxy", "198.51.100.23:1234", "https", 403},
		{"trusted local TLS proxy", "127.0.0.1:1234", "https", 201},
	} {
		t.Run(test.name, func(t *testing.T) {
			request := httptest.NewRequest("POST", "http://local/v1/uploads", bytes.NewReader(uploadBody(t, nil)))
			request.RemoteAddr = test.remote
			request.Header.Set("X-Forwarded-Proto", test.forwarded)
			request.Header.Set("Authorization", "Bearer "+testDeviceToken)
			request.Header.Set("Content-Type", "application/json")
			response := httptest.NewRecorder()
			service.ServeHTTP(response, request)
			requireStatus(t, response.Code, test.expected, response.Body.Bytes())
		})
	}
}
