package main

import (
	"archive/zip"
	"bytes"
	"encoding/json"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func publishTestRelease(t *testing.T, f *serviceFixture) ([]byte, releaseManifest, string) {
	t.Helper()
	var archive bytes.Buffer
	writer := zip.NewWriter(&archive)
	file, err := writer.Create("synthetic-release.txt")
	if err != nil {
		t.Fatal(err)
	}
	if _, err := file.Write([]byte("synthetic desktop release, no application or private data")); err != nil {
		t.Fatal(err)
	}
	if err := writer.Close(); err != nil {
		t.Fatal(err)
	}
	data := archive.Bytes()
	manifest := releaseManifest{ReleaseID: "release-1-0-1", Version: "1.0.1+2", Build: "0123456789abcdef", Platform: "windows-x64", Role: "host", Size: int64(len(data)), SHA256: sumHex(data), DownloadPath: "/v1/releases/test-host.zip", Notes: "synthetic release"}
	if err := os.WriteFile(filepath.Join(f.config.updatesDir, "test-host.zip"), data, 0600); err != nil {
		t.Fatal(err)
	}
	name := filepath.Join(f.config.updatesDir, "manifests", "windows-x64", "host.json")
	if err := os.MkdirAll(filepath.Dir(name), 0700); err != nil {
		t.Fatal(err)
	}
	writeTestManifest(t, name, manifest)
	return data, manifest, name
}
func writeTestManifest(t *testing.T, name string, manifest releaseManifest) {
	t.Helper()
	data, err := json.Marshal(manifest)
	if err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(name, data, 0600); err != nil {
		t.Fatal(err)
	}
}

func TestUpdateOfferIsBoundToExactPlatformRoleAndArchive(t *testing.T) {
	f := newFixture(t)
	archive, manifest, _ := publishTestRelease(t, f)
	status, data, _ := f.request(t, "GET", "/v1/updates/windows-x64/host", testDeviceToken, nil)
	requireStatus(t, status, 200, data)
	var offered releaseManifest
	if json.Unmarshal(data, &offered) != nil || offered != manifest {
		t.Fatal("offered update differs from configured exact role/platform")
	}
	for _, path := range []string{"/v1/updates/windows-x64/client", "/v1/updates/macos-arm64/host"} {
		status, data, _ := f.request(t, "GET", path, testDeviceToken, nil)
		requireStatus(t, status, 204, data)
	}
	status, download, headers := f.request(t, "GET", manifest.DownloadPath, testDeviceToken, nil)
	requireStatus(t, status, 200, download)
	if !bytes.Equal(download, archive) || sumHex(download) != offered.SHA256 || headers.Get("Content-Type") != "application/zip" {
		t.Fatal("downloaded bytes differ from offered immutable release")
	}
	request, err := http.NewRequest("GET", f.server.URL+manifest.DownloadPath, nil)
	if err != nil {
		t.Fatal(err)
	}
	request.Header.Set("Authorization", "Bearer "+testDeviceToken)
	request.Header.Set("Range", "bytes=0-15")
	response, err := f.server.Client().Do(request)
	if err != nil {
		t.Fatal(err)
	}
	defer response.Body.Close()
	part, err := io.ReadAll(response.Body)
	if err != nil {
		t.Fatal(err)
	}
	if response.StatusCode != 206 || !bytes.Equal(part, archive[:16]) {
		t.Fatal("resumable release transfer returned incorrect range")
	}
}

func TestMalformedOrTamperedReleaseNeverGetsAnUpdateOffer(t *testing.T) {
	for _, test := range []struct {
		name   string
		mutate func(*releaseManifest)
		raw    []byte
		tamper bool
	}{
		{name: "wrong role", mutate: func(m *releaseManifest) { m.Role = "client" }},
		{name: "wrong platform", mutate: func(m *releaseManifest) { m.Platform = "macos-arm64" }},
		{name: "wrong size", mutate: func(m *releaseManifest) { m.Size++ }},
		{name: "wrong hash", mutate: func(m *releaseManifest) { m.SHA256 = strings.Repeat("0", 64) }},
		{name: "no explicit build", mutate: func(m *releaseManifest) { m.Build = "development" }},
		{name: "path escape", mutate: func(m *releaseManifest) { m.DownloadPath = "/v1/releases/../private.zip" }},
		{name: "oversized notes", mutate: func(m *releaseManifest) { m.Notes = strings.Repeat("س", 2049) }},
		{name: "invalid JSON", raw: []byte("{broken}")},
		{name: "oversized manifest", raw: []byte(strings.Repeat("x", 64*1024+1))},
		{name: "same size content tamper", tamper: true},
	} {
		t.Run(test.name, func(t *testing.T) {
			f := newFixture(t)
			archive, manifest, name := publishTestRelease(t, f)
			if test.mutate != nil {
				test.mutate(&manifest)
				writeTestManifest(t, name, manifest)
			}
			if test.raw != nil {
				if err := os.WriteFile(name, test.raw, 0600); err != nil {
					t.Fatal(err)
				}
			}
			if test.tamper {
				archive[len(archive)/2] ^= 1
				if err := os.WriteFile(filepath.Join(f.config.updatesDir, "test-host.zip"), archive, 0600); err != nil {
					t.Fatal(err)
				}
			}
			status, data, _ := f.request(t, "GET", "/v1/updates/windows-x64/host", testDeviceToken, nil)
			requireStatus(t, status, 503, data)
		})
	}
}

func TestPrivateFilesAndSymlinksCannotBeReadAsReleases(t *testing.T) {
	f := newFixture(t)
	publishTestRelease(t, f)
	outside := filepath.Join(t.TempDir(), "private.zip")
	sentinel := []byte("SYNTHETIC_PRIVATE_FILE_SENTINEL")
	if err := os.WriteFile(outside, sentinel, 0600); err != nil {
		t.Fatal(err)
	}
	if err := os.Symlink(outside, filepath.Join(f.config.updatesDir, "escape.zip")); err != nil {
		t.Skipf("symlinks unavailable: %v", err)
	}
	if err := os.Symlink(filepath.Dir(outside), filepath.Join(f.config.updatesDir, "escape-dir")); err != nil {
		t.Fatal(err)
	}
	for _, path := range []string{"/v1/releases/escape.zip", "/v1/releases/escape-dir/private.zip", "/v1/releases/../private.zip", "/v1/releases/%2e%2e/private.zip", "/v1/releases/private.json"} {
		status, data, _ := f.request(t, "GET", path, testDeviceToken, nil)
		if status != 404 && status != 503 {
			t.Fatalf("unsafe release path accepted HTTP%d", status)
		}
		if bytes.Contains(data, sentinel) {
			t.Fatal("release endpoint exposed private file")
		}
	}
}
