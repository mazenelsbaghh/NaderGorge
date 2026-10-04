package main

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"os"
	"strconv"
	"strings"
)

type releaseSlot struct {
	Platform string           `json:"platform"`
	Role     string           `json:"role"`
	Status   string           `json:"status"`
	Manifest *releaseManifest `json:"manifest,omitempty"`
}

func (s *supportService) adminReleases(w http.ResponseWriter) {
	slots := make([]releaseSlot, 0, 4)
	for _, platform := range []string{"windows-x64", "macos-arm64"} {
		for _, role := range []string{"host", "client"} {
			slot := releaseSlot{Platform: platform, Role: role, Status: "invalid"}
			manifest, err := s.releaseManifestFor(platform, role)
			if os.IsNotExist(err) {
				slot.Status = "missing"
			} else if err == nil {
				slot.Status = "available"
				slot.Manifest = &manifest
			}
			slots = append(slots, slot)
		}
	}
	writeJSON(w, http.StatusOK, map[string]any{"releases": slots})
}

type diagnosticProjection struct {
	Receipt   uploadReceipt    `json:"receipt"`
	Kind      string           `json:"kind"`
	Events    []map[string]any `json:"events"`
	Total     int              `json:"total"`
	Truncated bool             `json:"truncated"`
}

func (s *supportService) uploadDiagnostics(w http.ResponseWriter, r *http.Request, id string) {
	if !uuidPattern.MatchString(id) {
		fail(w, http.StatusNotFound, "not_found")
		return
	}
	limit := 200
	if input := r.URL.Query().Get("limit"); input != "" {
		parsed, err := strconv.Atoi(input)
		if err != nil || parsed < 1 || parsed > 500 {
			fail(w, http.StatusBadRequest, "invalid_limit")
			return
		}
		limit = parsed
	}
	receipt, err := s.readReceipt(id)
	if os.IsNotExist(err) {
		fail(w, http.StatusNotFound, "not_found")
		return
	}
	if err != nil {
		fail(w, http.StatusServiceUnavailable, "upload_storage_unavailable")
		return
	}
	envelope, err := s.diagnosticEnvelope(receipt)
	if err != nil {
		fail(w, http.StatusServiceUnavailable, "upload_storage_unavailable")
		return
	}
	events, total := diagnosticEvents(envelope.Diagnostics, limit)
	writeJSON(w, http.StatusOK, diagnosticProjection{Receipt: receipt, Kind: envelope.Kind, Events: events, Total: total, Truncated: total > len(events)})
}

// Verify and parse the same open file so pathname replacement cannot switch the
// evidence between integrity verification and projection. Database values are
// consumed as tokens, never retained in the diagnostic response or a backup map.
func (s *supportService) diagnosticEnvelope(receipt uploadReceipt) (uploadEnvelope, error) {
	var envelope uploadEnvelope
	if receipt.Size > s.config.maxUploadBytes {
		return envelope, errors.New("bundle exceeds limit")
	}
	file, err := s.openStoredFile(receipt.UploadID, "bundle.json")
	if err != nil {
		return envelope, err
	}
	defer file.Close()
	stat, err := file.Stat()
	if err != nil || stat.Size() != receipt.Size {
		return envelope, errors.New("incomplete bundle")
	}
	digest := sha256.New()
	if _, err := io.Copy(digest, io.LimitReader(file, receipt.Size+1)); err != nil {
		return envelope, err
	}
	if hex.EncodeToString(digest.Sum(nil)) != receipt.BundleSHA256 {
		return envelope, errors.New("incomplete bundle")
	}
	if _, err := file.Seek(0, io.SeekStart); err != nil {
		return envelope, err
	}
	envelope, err = extractDiagnosticEnvelope(io.LimitReader(file, receipt.Size))
	if err != nil {
		return envelope, err
	}
	if envelope.Kind == "" {
		envelope.Kind = "database"
		if envelope.App.Role == "client" {
			envelope.Kind = "diagnostics"
		}
	}
	if envelope.Format != "massar-support-upload-v1" || envelope.UploadID != receipt.UploadID || envelope.CenterID != receipt.CenterID ||
		envelope.App != receipt.App || envelope.CreatedAt != receipt.CreatedAt || len(envelope.Diagnostics) > maximumDiagnosticsBytes ||
		(envelope.Kind != "database" && envelope.Kind != "diagnostics") || (envelope.App.Role == "client" && envelope.Kind != "diagnostics") {
		return envelope, errors.New("invalid bundle metadata")
	}
	return envelope, nil
}

func extractDiagnosticEnvelope(reader io.Reader) (uploadEnvelope, error) {
	var envelope uploadEnvelope
	decoder := json.NewDecoder(reader)
	decoder.UseNumber()
	opening, err := decoder.Token()
	if err != nil || opening != json.Delim('{') {
		return envelope, errors.New("invalid bundle")
	}
	seen := map[string]bool{}
	for decoder.More() {
		token, err := decoder.Token()
		if err != nil {
			return envelope, err
		}
		key, ok := token.(string)
		if !ok || seen[key] {
			return envelope, errors.New("invalid bundle field")
		}
		seen[key] = true
		err = decodeDiagnosticField(decoder, key, &envelope)
		if err != nil {
			return envelope, err
		}
	}
	if _, err := decoder.Token(); err != nil {
		return envelope, err
	}
	if _, err := decoder.Token(); err != io.EOF {
		return envelope, errors.New("trailing bundle content")
	}
	return envelope, nil
}

func decodeDiagnosticField(decoder *json.Decoder, key string, envelope *uploadEnvelope) error {
	switch key {
	case "format":
		return decoder.Decode(&envelope.Format)
	case "kind":
		return decoder.Decode(&envelope.Kind)
	case "uploadId":
		return decoder.Decode(&envelope.UploadID)
	case "centerId":
		return decoder.Decode(&envelope.CenterID)
	case "createdAt":
		return decoder.Decode(&envelope.CreatedAt)
	case "app":
		return decoder.Decode(&envelope.App)
	case "diagnostics":
		return decoder.Decode(&envelope.Diagnostics)
	default:
		return skipJSONValue(decoder)
	}
}

func skipJSONValue(decoder *json.Decoder) error {
	token, err := decoder.Token()
	if err != nil {
		return err
	}
	delimiter, container := token.(json.Delim)
	if !container {
		return nil
	}
	if delimiter != '{' && delimiter != '[' {
		return errors.New("invalid bundle value")
	}
	depth := 1
	for depth > 0 {
		token, err = decoder.Token()
		if err != nil {
			return err
		}
		if delimiter, ok := token.(json.Delim); ok {
			switch delimiter {
			case '{', '[':
				depth++
			case '}', ']':
				depth--
			}
		}
	}
	return nil
}

func diagnosticEvents(text string, limit int) ([]map[string]any, int) {
	// Reconstruct the whitelist even for formerly stored rows; raw messages and
	// arbitrary fields must never become website diagnostics after an upgrade.
	safe := sanitizedDiagnostics(text)
	events := make([]map[string]any, 0, limit)
	total := 0
	for _, line := range strings.Split(safe, "\n") {
		if line == "" {
			continue
		}
		var event map[string]any
		if json.Unmarshal([]byte(line), &event) != nil {
			continue
		}
		total++
		if len(events) == limit {
			copy(events, events[1:])
			events = events[:limit-1]
		}
		events = append(events, event)
	}
	return events, total
}
