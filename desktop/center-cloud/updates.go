package main

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"unicode/utf8"
)

const maximumReleaseBytes int64 = 2 * 1024 * 1024 * 1024

type releaseManifest struct {
	ReleaseID    string `json:"releaseId"`
	Version      string `json:"version"`
	Build        string `json:"build"`
	Platform     string `json:"platform"`
	Role         string `json:"role"`
	Size         int64  `json:"size"`
	SHA256       string `json:"sha256"`
	DownloadPath string `json:"downloadPath"`
	Notes        string `json:"notes,omitempty"`
}

// Update content is operator-authored, separate from all private upload storage.
func (s *supportService) openUpdateFile(relative string) (*os.File, error) {
	candidate := filepath.Join(s.config.updatesDir, relative)
	if !inside(s.config.updatesDir, candidate) {
		return nil, os.ErrNotExist
	}
	resolved, err := filepath.EvalSymlinks(candidate)
	if err != nil {
		return nil, err
	}
	if !inside(s.config.updatesDir, resolved) {
		return nil, errors.New("invalid release path")
	}
	info, err := os.Lstat(candidate)
	if err != nil {
		return nil, err
	}
	if !info.Mode().IsRegular() {
		return nil, errors.New("invalid release file")
	}
	return os.Open(resolved)
}

func releaseFilename(downloadPath string) string {
	const prefix = "/v1/releases/"
	if !strings.HasPrefix(downloadPath, prefix) {
		return ""
	}
	filename := strings.TrimPrefix(downloadPath, prefix)
	if !releaseFilePattern.MatchString(filename) {
		return ""
	}
	return filename
}

func validManifest(manifest releaseManifest, platform, role string) bool {
	return identifierPattern.MatchString(manifest.ReleaseID) && versionPattern.MatchString(manifest.Version) &&
		buildPattern.MatchString(manifest.Build) && manifest.Platform == platform && manifest.Role == role &&
		manifest.Size > 0 && manifest.Size <= maximumReleaseBytes && hashPattern.MatchString(manifest.SHA256) &&
		releaseFilename(manifest.DownloadPath) != "" && utf8.ValidString(manifest.Notes) && utf8.RuneCountInString(manifest.Notes) <= 2048
}

func (s *supportService) verifyRelease(manifest releaseManifest) error {
	file, err := s.openUpdateFile(releaseFilename(manifest.DownloadPath))
	if err != nil {
		return err
	}
	defer file.Close()
	info, err := file.Stat()
	if err != nil || info.Size() != manifest.Size {
		return errors.New("release unavailable")
	}
	digest := sha256.New()
	if _, err := io.Copy(digest, file); err != nil {
		return err
	}
	if hex.EncodeToString(digest.Sum(nil)) != manifest.SHA256 {
		return errors.New("release unavailable")
	}
	return nil
}

func (s *supportService) releaseManifestFor(platform, role string) (releaseManifest, error) {
	var manifest releaseManifest
	file, err := s.openUpdateFile(filepath.Join("manifests", platform, role+".json"))
	if err != nil {
		return manifest, err
	}
	defer file.Close()
	encoded, err := io.ReadAll(io.LimitReader(file, 64*1024+1))
	if err != nil || len(encoded) > 64*1024 {
		return manifest, errors.New("invalid manifest")
	}
	decoder := json.NewDecoder(bytes.NewReader(encoded))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&manifest); err != nil {
		return manifest, err
	}
	var trailing any
	if decoder.Decode(&trailing) != io.EOF || !validManifest(manifest, platform, role) {
		return manifest, errors.New("invalid manifest")
	}
	// Missing archives are an invalid publication, distinct from an absent manifest.
	if s.verifyRelease(manifest) != nil {
		return manifest, errors.New("release unavailable")
	}
	return manifest, nil
}

func (s *supportService) updateManifest(w http.ResponseWriter, r *http.Request) {
	parts := strings.Split(strings.TrimPrefix(r.URL.Path, "/v1/updates/"), "/")
	if len(parts) != 2 || (parts[0] != "windows-x64" && parts[0] != "macos-arm64") ||
		(parts[1] != "host" && parts[1] != "client") {
		fail(w, http.StatusNotFound, "not_found")
		return
	}
	platform, role := parts[0], parts[1]
	manifest, err := s.releaseManifestFor(platform, role)
	if os.IsNotExist(err) {
		w.WriteHeader(http.StatusNoContent)
		return
	}
	if err != nil {
		fail(w, http.StatusServiceUnavailable, "update_unavailable")
		return
	}
	writeJSON(w, http.StatusOK, manifest)
}

func (s *supportService) downloadRelease(w http.ResponseWriter, r *http.Request) {
	filename := releaseFilename(r.URL.Path)
	if filename == "" {
		fail(w, http.StatusNotFound, "not_found")
		return
	}
	file, err := s.openUpdateFile(filename)
	if os.IsNotExist(err) {
		fail(w, http.StatusNotFound, "not_found")
		return
	}
	if err != nil {
		fail(w, http.StatusServiceUnavailable, "update_unavailable")
		return
	}
	defer file.Close()
	info, err := file.Stat()
	if err != nil || info.Size() <= 0 || info.Size() > maximumReleaseBytes {
		fail(w, http.StatusServiceUnavailable, "update_unavailable")
		return
	}
	w.Header().Set("Content-Type", "application/zip")
	w.Header().Set("Content-Disposition", `attachment; filename="`+filename+`"`)
	http.ServeContent(w, r, filename, info.ModTime(), file)
}
