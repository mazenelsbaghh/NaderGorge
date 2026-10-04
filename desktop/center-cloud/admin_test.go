package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func diagnosticFixtureLine(t *testing.T, index int, secret string) string {
	t.Helper()
	event := map[string]any{"schema": 1, "kind": "error", "id": fmt.Sprintf("%08x-1111-4111-8111-111111111111", index), "session": testUploadID, "time": "2026-10-03T12:00:00Z", "version": "1.0.0+1", "build": "0123456789abcdef", "platform": "windows", "operation": "entry", "message": secret, "path": "/private/" + secret,
		"errors": []any{map[string]any{"type": "DatabaseException", "code": 19, "message": secret}},
		"frames": []any{map[string]any{"file": "application/center_store.dart", "frame": 0, "line": 42, "column": 1, "function": secret}, map[string]any{"file": "/private/" + secret, "frame": 1, "line": 1, "column": 1}}}
	encoded, err := json.Marshal(event)
	if err != nil {
		t.Fatal(err)
	}
	return string(encoded) + "\n"
}

// Simulates formerly stored evidence whose diagnostics were not yet redacted.
// The bundle remains integrity-valid; projection must sanitize on every read.
func replaceTestBundle(t *testing.T, f *serviceFixture, receipt uploadReceipt, change func(*uploadEnvelope)) {
	t.Helper()
	path := filepath.Join(f.config.storageDir, receipt.UploadID, "bundle.json")
	encoded, err := os.ReadFile(path)
	if err != nil {
		t.Fatal(err)
	}
	var envelope uploadEnvelope
	if err := json.Unmarshal(encoded, &envelope); err != nil {
		t.Fatal(err)
	}
	change(&envelope)
	encoded, err = json.Marshal(envelope)
	if err != nil {
		t.Fatal(err)
	}
	receipt.Size = int64(len(encoded))
	receipt.BundleSHA256 = sumHex(encoded)
	if err := os.WriteFile(path, encoded, 0600); err != nil {
		t.Fatal(err)
	}
	metadata, err := json.Marshal(receipt)
	if err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(filepath.Dir(path), "receipt.json"), metadata, 0600); err != nil {
		t.Fatal(err)
	}
}

func TestAdminDiagnosticsReSanitizesEvidenceWithoutExposingLogicalBackup(t *testing.T) {
	f := newFixture(t)
	secret := "SYNTHETIC_STUDENT_PASSWORD_PHONE_SQL_PATH"
	body := uploadBody(t, func(envelope map[string]any) {
		envelope["data"] = map[string]any{"students": []any{map[string]any{"name": secret, "phone": secret, "password": secret}}, "largeIgnoredLedger": strings.Repeat("x", 3*1024*1024)}
	})
	status, response, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
	requireStatus(t, status, 201, response)
	receipt := decodeReceipt(t, response)
	var diagnostics strings.Builder
	for index := 1; index <= 205; index++ {
		diagnostics.WriteString(diagnosticFixtureLine(t, index, secret))
	}
	diagnostics.WriteString("not JSON " + secret + "\n")
	replaceTestBundle(t, f, receipt, func(envelope *uploadEnvelope) { envelope.Diagnostics = diagnostics.String() })
	for _, limit := range []struct {
		query        string
		count, first int
		truncated    bool
	}{
		{"", 200, 6, true}, {"?limit=2", 2, 204, true}, {"?limit=500", 205, 1, false},
	} {
		t.Run(fmt.Sprintf("limit%d", limit.count), func(t *testing.T) {
			status, response, headers := f.request(t, "GET", "/v1/uploads/"+testUploadID+"/diagnostics"+limit.query, testAdminToken, nil)
			requireStatus(t, status, 200, response)
			var projection diagnosticProjection
			if err := json.Unmarshal(response, &projection); err != nil {
				t.Fatal(err)
			}
			if projection.Receipt.ReceiptID != receipt.ReceiptID || projection.Kind != "database" || projection.Total != 205 || len(projection.Events) != limit.count || projection.Truncated != limit.truncated {
				t.Fatalf("incorrect projection: %s", response)
			}
			if projection.Events[0]["id"] != fmt.Sprintf("%08x-1111-4111-8111-111111111111", limit.first) {
				t.Fatal("latest events lost source order")
			}
			if headers.Get("Cache-Control") != "no-store" || headers.Get("Content-Type") != "application/json; charset=utf-8" {
				t.Fatal("private projection may be cached")
			}
			for _, forbidden := range []string{secret, testDeviceToken, testAdminToken, "students", "password", "/private/", "largeIgnoredLedger", "\"data\"", "\"diagnostics\"", "\"message\"", "\"function\""} {
				if bytes.Contains(response, []byte(forbidden)) {
					t.Fatalf("private projection leaked %s", forbidden)
				}
			}
			first := projection.Events[0]
			if first["operation"] != "entry" || len(first["frames"].([]any)) != 1 || first["errors"].([]any)[0].(map[string]any)["code"] != float64(19) {
				t.Fatal("safe failure metadata lost")
			}
		})
	}
}

func TestAdminProjectionsRejectDeviceCredentialsAndInvalidDiagnosticRequests(t *testing.T) {
	f := newFixture(t)
	status, response, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, uploadBody(t, nil))
	requireStatus(t, status, 201, response)
	for _, path := range []string{"/v1/uploads/" + testUploadID + "/diagnostics", "/v1/admin/releases"} {
		for _, token := range []string{"", testDeviceToken, testOtherToken, "wrong-synthetic-token"} {
			status, response, headers := f.request(t, "GET", path, token, nil)
			requireStatus(t, status, 401, response)
			if headers.Get("Cache-Control") != "no-store" {
				t.Fatal("authorization failure can be cached")
			}
		}
	}
	for _, test := range []struct {
		suffix string
		status int
	}{
		{"/diagnostics?limit=0", 400}, {"/diagnostics?limit=501", 400}, {"/diagnostics?limit=abc", 400}, {"/extra/diagnostics", 404},
	} {
		status, response, _ := f.request(t, "GET", "/v1/uploads/"+testUploadID+test.suffix, testAdminToken, nil)
		requireStatus(t, status, test.status, response)
	}
	status, response, _ = f.request(t, "GET", "/v1/uploads/22222222-2222-4222-8222-222222222222/diagnostics", testAdminToken, nil)
	requireStatus(t, status, 404, response)
}

func TestDiagnosticProjectionSupportsEmptyClientLogsAndRejectsTamperedBundles(t *testing.T) {
	for _, scenario := range []string{"empty client", "modified bundle", "modified metadata", "size exceeds configured limit"} {
		t.Run(scenario, func(t *testing.T) {
			f := newFixture(t)
			body := uploadBody(t, func(envelope map[string]any) {
				envelope["kind"] = "diagnostics"
				envelope["data"] = nil
				envelope["app"].(map[string]any)["role"] = "client"
			})
			status, response, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
			requireStatus(t, status, 201, response)
			receipt := decodeReceipt(t, response)
			switch scenario {
			case "modified bundle":
				path := filepath.Join(f.config.storageDir, testUploadID, "bundle.json")
				encoded, err := os.ReadFile(path)
				if err != nil {
					t.Fatal(err)
				}
				encoded = bytes.Replace(encoded, []byte("diagnostics"), []byte("diagnosticX"), 1)
				if err := os.WriteFile(path, encoded, 0600); err != nil {
					t.Fatal(err)
				}
			case "modified metadata":
				replaceTestBundle(t, f, receipt, func(envelope *uploadEnvelope) { envelope.CenterID = "other-center" })
			case "size exceeds configured limit":
				f.server.Close()
				f.config.maxUploadBytes = receipt.Size - 1
				f.start(t)
			}
			status, response, _ = f.request(t, "GET", "/v1/uploads/"+testUploadID+"/diagnostics", testAdminToken, nil)
			if scenario != "empty client" {
				requireStatus(t, status, 503, response)
				if bytes.Contains(response, []byte("receiptId")) {
					t.Fatal("unverified evidence projected")
				}
				return
			}
			requireStatus(t, status, 200, response)
			var projection diagnosticProjection
			if err := json.Unmarshal(response, &projection); err != nil {
				t.Fatal(err)
			}
			if projection.Kind != "diagnostics" || projection.Total != 0 || projection.Truncated || projection.Events == nil || len(projection.Events) != 0 {
				t.Fatalf("incorrect empty client projection %s", response)
			}
		})
	}
}

func TestAdminReleaseInventoryReportsOnlyVerifiedSlots(t *testing.T) {
	f := newFixture(t)
	archive, available, _ := publishTestRelease(t, f)
	invalidPath := filepath.Join(f.config.updatesDir, "manifests", "windows-x64", "client.json")
	secret := "SYNTHETIC_RELEASE_SECRET_INVALID_MANIFEST"
	if err := os.WriteFile(invalidPath, []byte(`{"token":"`+secret+`"}`), 0600); err != nil {
		t.Fatal(err)
	}
	absentArchive := available
	absentArchive.Role = "client"
	absentArchive.Platform = "macos-arm64"
	absentArchive.DownloadPath = "/v1/releases/absent.zip"
	macClientPath := filepath.Join(f.config.updatesDir, "manifests", "macos-arm64", "client.json")
	if err := os.MkdirAll(filepath.Dir(macClientPath), 0700); err != nil {
		t.Fatal(err)
	}
	writeTestManifest(t, macClientPath, absentArchive)
	status, response, headers := f.request(t, "GET", "/v1/admin/releases", testAdminToken, nil)
	requireStatus(t, status, 200, response)
	var inventory struct {
		Releases []releaseSlot `json:"releases"`
	}
	if err := json.Unmarshal(response, &inventory); err != nil {
		t.Fatal(err)
	}
	if len(inventory.Releases) != 4 || headers.Get("Cache-Control") != "no-store" {
		t.Fatal("inventory incomplete or cacheable")
	}
	expected := []string{"available", "invalid", "missing", "invalid"}
	for index, slot := range inventory.Releases {
		if slot.Status != expected[index] {
			t.Fatalf("incorrect slot: %+v", slot)
		}
		if index == 0 {
			if slot.Manifest == nil || *slot.Manifest != available {
				t.Fatal("verified manifest lost")
			}
		} else if slot.Manifest != nil {
			t.Fatal("unverified manifest exposed")
		}
	}
	for _, forbidden := range []string{secret, testAdminToken, testDeviceToken, "token", "updatesDir", "storageDir"} {
		if bytes.Contains(response, []byte(forbidden)) {
			t.Fatal("inventory leaked private metadata")
		}
	}
	archive[len(archive)/2] ^= 1
	if err := os.WriteFile(filepath.Join(f.config.updatesDir, "test-host.zip"), archive, 0600); err != nil {
		t.Fatal(err)
	}
	status, response, _ = f.request(t, "GET", "/v1/admin/releases", testAdminToken, nil)
	requireStatus(t, status, 200, response)
	inventory.Releases = nil
	if err := json.Unmarshal(response, &inventory); err != nil {
		t.Fatal(err)
	}
	if inventory.Releases[0].Status != "invalid" || inventory.Releases[0].Manifest != nil {
		t.Fatal("tampered archive still presented as available")
	}
}
