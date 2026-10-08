package main

import (
	"bytes"
	"compress/gzip"
	"crypto/sha256"
	"crypto/subtle"
	"encoding/json"
	"errors"
	"io"
	"mime"
	"net"
	"net/http"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
)

type supportService struct {
	config      config
	uploadSlots chan struct{}
}

func newService(c config) (*supportService, error) {
	if err := os.MkdirAll(c.storageDir, 0700); err != nil {
		return nil, err
	}
	storage, err := filepath.EvalSymlinks(c.storageDir)
	if err != nil {
		return nil, err
	}
	updates, err := filepath.EvalSymlinks(c.updatesDir)
	if err != nil {
		return nil, err
	}
	info, err := os.Stat(updates)
	if err != nil || !info.IsDir() {
		return nil, errors.New("invalid update directory")
	}
	if inside(storage, updates) || inside(updates, storage) {
		return nil, errors.New("directories overlap")
	}
	c.storageDir, c.updatesDir = storage, updates
	if err := os.Chmod(storage, 0700); err != nil {
		return nil, err
	}
	stage := filepath.Join(storage, ".staging")
	if err := os.MkdirAll(stage, 0700); err != nil {
		return nil, err
	}
	info, err = os.Lstat(stage)
	if err != nil || !info.IsDir() || info.Mode()&os.ModeSymlink != 0 {
		return nil, errors.New("invalid staging directory")
	}
	if err := os.Chmod(stage, 0700); err != nil {
		return nil, err
	}
	return &supportService{config: c, uploadSlots: make(chan struct{}, 2)}, nil
}

func writeJSON(w http.ResponseWriter, status int, data any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}

func fail(w http.ResponseWriter, status int, code string) {
	writeJSON(w, status, map[string]string{"error": code})
}

func (s *supportService) bearerHash(r *http.Request) ([32]byte, bool) {
	value := r.Header.Get("Authorization")
	if len(value) < 8 || len(value) > 1024 || !strings.HasPrefix(value, "Bearer ") {
		return [32]byte{}, false
	}
	token := strings.TrimPrefix(value, "Bearer ")
	for _, character := range token {
		if character < 33 || character > 126 {
			return [32]byte{}, false
		}
	}
	return sha256.Sum256([]byte(token)), true
}

func (s *supportService) device(r *http.Request) (string, bool) {
	hash, ok := s.bearerHash(r)
	if !ok {
		return "", false
	}
	center := ""
	for id, expected := range s.config.devices {
		if subtle.ConstantTimeCompare(hash[:], expected[:]) == 1 {
			center = id
		}
	}
	return center, center != ""
}

func (s *supportService) admin(r *http.Request) bool {
	hash, ok := s.bearerHash(r)
	return ok && subtle.ConstantTimeCompare(hash[:], s.config.adminHash[:]) == 1
}

func secureRequest(r *http.Request) bool {
	if r.TLS != nil {
		return true
	}
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		return false
	}
	ip := net.ParseIP(host)
	return ip != nil && ip.IsLoopback() && r.Header.Get("X-Forwarded-Proto") == "https"
}

func (s *supportService) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Cache-Control", "no-store")
	w.Header().Set("X-Content-Type-Options", "nosniff")
	if !secureRequest(r) {
		fail(w, http.StatusForbidden, "https_required")
		return
	}
	path := r.URL.Path
	switch {
	case path == "/v1/uploads" && r.Method == http.MethodPost:
		center, ok := s.device(r)
		if !ok {
			fail(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		s.upload(w, r, center)
	case path == "/v1/uploads" && r.Method == http.MethodGet:
		if !s.admin(r) {
			fail(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		s.listUploads(w, r)
	case path == "/v1/admin/releases" && r.Method == http.MethodGet:
		if !s.admin(r) {
			fail(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		s.adminReleases(w)
	case strings.HasPrefix(path, "/v1/uploads/") && strings.HasSuffix(path, "/diagnostics") && r.Method == http.MethodGet:
		if !s.admin(r) {
			fail(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		s.uploadDiagnostics(w, r, strings.TrimSuffix(strings.TrimPrefix(path, "/v1/uploads/"), "/diagnostics"))
	case strings.HasPrefix(path, "/v1/uploads/") && r.Method == http.MethodGet:
		if !s.admin(r) {
			fail(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		s.downloadUpload(w, r, strings.TrimPrefix(path, "/v1/uploads/"))
	case strings.HasPrefix(path, "/v1/updates/") && r.Method == http.MethodGet:
		if _, ok := s.device(r); !ok {
			fail(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		s.updateManifest(w, r)
	case strings.HasPrefix(path, "/v1/releases/") && r.Method == http.MethodGet:
		if _, ok := s.device(r); !ok {
			fail(w, http.StatusUnauthorized, "unauthorized")
			return
		}
		s.downloadRelease(w, r)
	default:
		fail(w, http.StatusNotFound, "not_found")
	}
}

func (s *supportService) upload(w http.ResponseWriter, r *http.Request, center string) {
	select {
	case s.uploadSlots <- struct{}{}:
		defer func() { <-s.uploadSlots }()
	default:
		w.Header().Set("Retry-After", "5")
		fail(w, http.StatusServiceUnavailable, "upload_busy")
		return
	}
	mediaType, _, err := mime.ParseMediaType(r.Header.Get("Content-Type"))
	if err != nil || mediaType != "application/json" {
		fail(w, http.StatusUnsupportedMediaType, "json_required")
		return
	}
	if r.ContentLength > s.config.maxUploadBytes {
		fail(w, http.StatusRequestEntityTooLarge, "upload_too_large")
		return
	}
	encoding := strings.ToLower(strings.TrimSpace(strings.Join(r.Header.Values("Content-Encoding"), ",")))
	if encoding != "" && encoding != "identity" && encoding != "gzip" {
		fail(w, http.StatusUnsupportedMediaType, "unsupported_content_encoding")
		return
	}
	r.Body = http.MaxBytesReader(w, r.Body, s.config.maxUploadBytes)
	defer r.Body.Close()
	body, err := readSupportBody(r.Body, encoding, s.config.maxUploadBytes)
	if err != nil {
		var tooLarge *http.MaxBytesError
		if errors.As(err, &tooLarge) || errors.Is(err, errTooLarge) {
			fail(w, http.StatusRequestEntityTooLarge, "upload_too_large")
		} else {
			fail(w, http.StatusBadRequest, "invalid_upload")
		}
		return
	}
	// UseNumber retains exact ledger integers rather than converting them to float64.
	decoder := json.NewDecoder(bytes.NewReader(body))
	decoder.UseNumber()
	var logical map[string]any
	if err := decoder.Decode(&logical); err != nil || logical == nil {
		fail(w, http.StatusBadRequest, "invalid_upload")
		return
	}
	var extra any
	if err := decoder.Decode(&extra); err != io.EOF {
		fail(w, http.StatusBadRequest, "invalid_upload")
		return
	}
	canonical, err := json.Marshal(logical)
	if err != nil {
		fail(w, http.StatusBadRequest, "invalid_upload")
		return
	}
	requestHash := sumHex(canonical)
	var envelope uploadEnvelope
	if err := json.Unmarshal(canonical, &envelope); err != nil || envelope.Format != "massar-support-upload-v1" ||
		!uuidPattern.MatchString(envelope.UploadID) || envelope.CenterID != center || !validUTC(envelope.CreatedAt) ||
		!validMetadata(envelope.App) || len(envelope.Diagnostics) > maximumDiagnosticsBytes {
		fail(w, http.StatusBadRequest, "invalid_upload")
		return
	}
	if envelope.Kind == "" {
		if envelope.App.Role == "client" {
			envelope.Kind = "diagnostics"
		} else {
			envelope.Kind = "database"
		}
	}
	if envelope.Kind == "database" {
		if envelope.App.Role == "client" || !isObject(envelope.Data) {
			fail(w, http.StatusBadRequest, "database_upload_not_allowed")
			return
		}
	} else if envelope.Kind != "diagnostics" || !bytes.Equal(bytes.TrimSpace(envelope.Data), []byte("null")) {
		fail(w, http.StatusBadRequest, "invalid_upload_kind")
		return
	}
	// Complete database contents are intentionally private support data. Only the
	// diagnostics field is redacted again; no names/messages/paths are log metadata.
	envelope.Diagnostics = sanitizedDiagnostics(envelope.Diagnostics)
	receipt, created, err := s.saveUpload(envelope, requestHash)
	if errors.Is(err, errConflict) {
		fail(w, http.StatusConflict, "upload_id_conflict")
		return
	}
	if errors.Is(err, errTooLarge) {
		fail(w, http.StatusRequestEntityTooLarge, "upload_too_large")
		return
	}
	if err != nil {
		fail(w, http.StatusServiceUnavailable, "upload_storage_unavailable")
		return
	}
	status := http.StatusOK
	if created {
		status = http.StatusCreated
	}
	writeJSON(w, status, receipt)
}

// Bound both transfer and expanded JSON; compression never changes the logical
// request hash, receipt identity, or private snapshot stored on disk.
func readSupportBody(body io.Reader, encoding string, limit int64) ([]byte, error) {
	if encoding == "gzip" {
		reader, err := gzip.NewReader(body)
		if err != nil {
			return nil, err
		}
		defer reader.Close()
		body = reader
	}
	data, err := io.ReadAll(io.LimitReader(body, limit+1))
	if int64(len(data)) > limit {
		return nil, errTooLarge
	}
	return data, err
}

func isObject(data json.RawMessage) bool {
	trimmed := bytes.TrimSpace(data)
	return len(trimmed) >= 2 && trimmed[0] == '{' && trimmed[len(trimmed)-1] == '}'
}

func (s *supportService) listUploads(w http.ResponseWriter, r *http.Request) {
	limit := 100
	if value := r.URL.Query().Get("limit"); value != "" {
		parsed, err := strconv.Atoi(value)
		if err != nil || parsed < 1 || parsed > 500 {
			fail(w, http.StatusBadRequest, "invalid_limit")
			return
		}
		limit = parsed
	}
	after := r.URL.Query().Get("after")
	if after != "" && !uuidPattern.MatchString(after) {
		fail(w, http.StatusBadRequest, "invalid_cursor")
		return
	}
	entries, err := os.ReadDir(s.config.storageDir)
	if err != nil {
		fail(w, http.StatusServiceUnavailable, "upload_storage_unavailable")
		return
	}
	ids := []string{}
	for _, entry := range entries {
		if entry.IsDir() && uuidPattern.MatchString(entry.Name()) && entry.Name() > after {
			ids = append(ids, entry.Name())
		}
	}
	sort.Strings(ids)
	result := []uploadReceipt{}
	next := ""
	for _, id := range ids {
		receipt, err := s.readReceipt(id)
		if err != nil {
			fail(w, http.StatusServiceUnavailable, "upload_storage_unavailable")
			return
		}
		if len(result) == limit {
			next = result[len(result)-1].UploadID
			break
		}
		result = append(result, receipt)
	}
	writeJSON(w, http.StatusOK, map[string]any{"uploads": result, "nextCursor": next})
}

func (s *supportService) downloadUpload(w http.ResponseWriter, r *http.Request, id string) {
	if !uuidPattern.MatchString(id) {
		fail(w, http.StatusNotFound, "not_found")
		return
	}
	receipt, err := s.readReceipt(id)
	if os.IsNotExist(err) {
		fail(w, http.StatusNotFound, "not_found")
		return
	}
	if err != nil || s.verifyBundle(receipt) != nil {
		fail(w, http.StatusServiceUnavailable, "upload_storage_unavailable")
		return
	}
	file, err := s.openStoredFile(id, "bundle.json")
	if err != nil {
		fail(w, http.StatusServiceUnavailable, "upload_storage_unavailable")
		return
	}
	defer file.Close()
	info, err := file.Stat()
	if err != nil {
		fail(w, http.StatusServiceUnavailable, "upload_storage_unavailable")
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Header().Set("Content-Disposition", `attachment; filename="`+id+`.json"`)
	w.Header().Set("ETag", `"`+receipt.BundleSHA256+`"`)
	http.ServeContent(w, r, id+".json", info.ModTime(), file)
}
