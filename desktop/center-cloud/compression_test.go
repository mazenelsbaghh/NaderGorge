package main

import (
	"bytes"
	"compress/gzip"
	"encoding/json"
	"io"
	"net/http"
	"strings"
	"testing"
)

func compressedBody(t *testing.T, raw []byte) []byte {
	t.Helper()
	var out bytes.Buffer
	writer := gzip.NewWriter(&out)
	if _, err := writer.Write(raw); err != nil {
		t.Fatal(err)
	}
	if err := writer.Close(); err != nil {
		t.Fatal(err)
	}
	return out.Bytes()
}

func encodedUpload(t *testing.T, f *serviceFixture, body []byte, encoding string, chunked bool) (int, []byte) {
	t.Helper()
	request, err := http.NewRequest("POST", f.server.URL+"/v1/uploads", bytes.NewReader(body))
	if err != nil {
		t.Fatal(err)
	}
	request.Header.Set("Authorization", "Bearer "+testDeviceToken)
	request.Header.Set("Content-Type", "application/json")
	request.Header.Set("Content-Encoding", encoding)
	if chunked {
		request.ContentLength = -1
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
	return response.StatusCode, data
}

func TestCompressedUploadPreservesSnapshotAndRetryIdentity(t *testing.T) {
	f := newFixture(t)
	raw := uploadBody(t, func(e map[string]any) {
		e["data"].(map[string]any)["padding"] = strings.Repeat("synthetic support snapshot ", 10000)
	})
	packed := compressedBody(t, raw)
	if len(packed) >= len(raw)/10 {
		t.Fatal("synthetic fixture did not exercise smaller transfer")
	}
	status, data := encodedUpload(t, f, packed, "gzip", false)
	requireStatus(t, status, 201, data)
	first := decodeReceipt(t, data)
	// A pending queue created before the upgrade uses the same logical identity.
	status, data, _ = f.request(t, "POST", "/v1/uploads", testDeviceToken, raw)
	requireStatus(t, status, 200, data)
	if decodeReceipt(t, data) != first {
		t.Fatal("compression changed idempotent receipt")
	}
	status, data, _ = f.request(t, "GET", "/v1/uploads/"+testUploadID, testAdminToken, nil)
	requireStatus(t, status, 200, data)
	var saved uploadEnvelope
	if err := json.Unmarshal(data, &saved); err != nil {
		t.Fatal(err)
	}
	if !bytes.Contains(saved.Data, []byte(`9007199254740993`)) || !bytes.Contains(saved.Data, []byte(strings.Repeat("synthetic support snapshot ", 10000))) {
		t.Fatal("compressed upload lost logical data or exact ledger integer")
	}
}

func TestCompressedUploadBoundsAndCorruption(t *testing.T) {
	for _, name := range []string{"expanded", "wire", "chunked wire", "truncated", "checksum", "unsupported"} {
		t.Run(name, func(t *testing.T) {
			f := newFixture(t)
			f.server.Close()
			f.config.maxUploadBytes = 1024
			f.start(t)
			packed := compressedBody(t, uploadBody(t, nil))
			encoding := "gzip"
			expected := 400
			chunked := false
			switch name {
			case "expanded":
				packed = compressedBody(t, uploadBody(t, func(e map[string]any) { e["data"] = map[string]any{"padding": strings.Repeat("x", 2048)} }))
				expected = 413
			case "wire", "chunked wire":
				var out bytes.Buffer
				writer := gzip.NewWriter(&out)
				writer.Header.Extra = bytes.Repeat([]byte{1}, 2048)
				writer.Write(uploadBody(t, nil))
				writer.Close()
				packed = out.Bytes()
				expected = 413
				chunked = name == "chunked wire"
			case "truncated":
				packed = packed[:len(packed)-4]
			case "checksum":
				packed[len(packed)-5] ^= 1
			case "unsupported":
				encoding = "br"
				expected = 415
			}
			status, data := encodedUpload(t, f, packed, encoding, chunked)
			requireStatus(t, status, expected, data)
			status, data, _ = f.request(t, "GET", "/v1/uploads/"+testUploadID, testAdminToken, nil)
			requireStatus(t, status, 404, data)
		})
	}
}
