package main

import (
	"encoding/json"
	"strings"
	"testing"
)

func TestUploadSanitizesUntrustedDiagnosticsAndRetainsCompleteLogicalBackup(t *testing.T) {
	f := newFixture(t)
	secret := "SYNTHETIC_SQL_PASSWORD_NAME_PHONE_PATH_SENTINEL"
	event := map[string]any{"schema": 1, "kind": "error", "id": testUploadID, "session": "22222222-2222-4222-8222-222222222222", "time": "2026-10-03T12:00:00Z", "version": "1.0.0+1", "build": "0123456789abcdef", "role": "host", "platform": "windows", "operation": "cloud.upload",
		"message": secret, "sql": secret, "path": "/private/" + secret, "errors": []any{map[string]any{"type": "DatabaseException", "code": 19, "message": secret, "args": secret}, map[string]any{"type": secret, "code": secret}},
		"frames": []any{map[string]any{"file": "cloud/cloud_support_controller.dart", "frame": 0, "line": 120, "column": 5, "function": secret}, map[string]any{"file": "/private/" + secret, "frame": 1, "line": 1, "column": 1}}}
	encoded, err := json.Marshal(event)
	if err != nil {
		t.Fatal(err)
	}
	unknown := map[string]any{}
	if json.Unmarshal(encoded, &unknown) != nil {
		t.Fatal("fixture decode")
	}
	unknown["operation"] = secret
	unknownJSON, err := json.Marshal(unknown)
	if err != nil {
		t.Fatal(err)
	}
	diagnostics := `{"kind":"export","privacy":"` + secret + `"}` + "\n" + string(encoded) + "\n" + string(unknownJSON) + "\n" + string(encoded) + " {}\n" + secret + "\n"
	body := uploadBody(t, func(e map[string]any) {
		e["diagnostics"] = diagnostics
		e["data"].(map[string]any)["preservedPrivateField"] = secret
	})
	status, response, _ := f.request(t, "POST", "/v1/uploads", testDeviceToken, body)
	requireStatus(t, status, 201, response)
	status, bundle, _ := f.request(t, "GET", "/v1/uploads/"+testUploadID, testAdminToken, nil)
	requireStatus(t, status, 200, bundle)
	var saved uploadEnvelope
	if err := json.Unmarshal(bundle, &saved); err != nil {
		t.Fatal(err)
	}
	if strings.Contains(saved.Diagnostics, secret) || strings.Contains(saved.Diagnostics, "/private/") || strings.Contains(saved.Diagnostics, "message") || strings.Contains(saved.Diagnostics, "function") {
		t.Fatal("diagnostics leaked untrusted payload")
	}
	if !strings.Contains(string(saved.Data), secret) {
		t.Fatal("diagnostic redaction incorrectly destroyed full logical database")
	}
	lines := strings.Split(strings.TrimSpace(saved.Diagnostics), "\n")
	if len(lines) != 2 {
		t.Fatalf("invalid lines or export header survived: %s", saved.Diagnostics)
	}
	for index, line := range lines {
		var clean struct {
			Operation string `json:"operation"`
			Errors    []struct {
				Type string `json:"type"`
				Code *int   `json:"code"`
			} `json:"errors"`
			Frames []struct {
				File string `json:"file"`
			} `json:"frames"`
		}
		if err := json.Unmarshal([]byte(line), &clean); err != nil {
			t.Fatal(err)
		}
		expectedOperation := "cloud.upload"
		if index == 1 {
			expectedOperation = "unknown_operation"
		}
		if clean.Operation != expectedOperation || len(clean.Errors) != 2 || clean.Errors[0].Type != "DatabaseException" || clean.Errors[0].Code == nil || *clean.Errors[0].Code != 19 || clean.Errors[1].Type != "OtherError" || clean.Errors[1].Code != nil || len(clean.Frames) != 1 || clean.Frames[0].File != "cloud/cloud_support_controller.dart" {
			t.Fatalf("safe diagnostics lost actual failure metadata: %s", line)
		}
	}
}
