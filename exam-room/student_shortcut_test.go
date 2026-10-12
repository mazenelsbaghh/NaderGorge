package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"image/png"
	"io"
	"net"
	"net/http/httptest"
	"net/url"
	"strings"
	"testing"
)

func TestStudentShortcutManifestAndIconsAreServedOnStudentOrigin(t *testing.T) {
	a := &app{addresses: func() []string { return nil }}
	server := httptest.NewUnstartedServer(a.handler(false))
	a.studentPort = server.Listener.Addr().(*net.TCPAddr).Port
	server.Start()
	t.Cleanup(server.Close)
	get := func(path string, status int, contentType string) []byte {
		t.Helper()
		response, err := server.Client().Get(server.URL + path)
		if err != nil {
			t.Fatal(err)
		}
		defer response.Body.Close()
		body, err := io.ReadAll(response.Body)
		if err != nil {
			t.Fatal(err)
		}
		if response.StatusCode != status {
			t.Fatalf("GET %s: status %d: %s", path, response.StatusCode, body)
		}
		if contentType != "" && response.Header.Get("Content-Type") != contentType {
			t.Fatalf("GET %s: content type %q", path, response.Header.Get("Content-Type"))
		}
		return body
	}
	page := string(get("/", 200, "text/html; charset=utf-8"))
	for _, link := range []string{`rel="manifest" href="/assets/student.webmanifest"`, `rel="apple-touch-icon" sizes="256x256" href="/assets/student-icon-256.png"`} {
		if !strings.Contains(page, link) {
			t.Fatalf("student page missing home-screen metadata %s", link)
		}
	}
	var manifest struct {
		Name            string `json:"name"`
		ShortName       string `json:"short_name"`
		ID              string `json:"id"`
		StartURL        string `json:"start_url"`
		Scope           string `json:"scope"`
		Display         string `json:"display"`
		Lang            string `json:"lang"`
		Dir             string `json:"dir"`
		ThemeColor      string `json:"theme_color"`
		BackgroundColor string `json:"background_color"`
		Icons           []struct {
			Src   string `json:"src"`
			Sizes string `json:"sizes"`
			Type  string `json:"type"`
		} `json:"icons"`
	}
	if err := json.Unmarshal(get("/assets/student.webmanifest", 200, "application/manifest+json; charset=utf-8"), &manifest); err != nil {
		t.Fatal(err)
	}
	if manifest.Name != "امتحانات مسار" || manifest.ShortName != "مسار" || manifest.Display != "standalone" || manifest.Lang != "ar" || manifest.Dir != "rtl" {
		t.Fatalf("incorrect home-screen identity: %+v", manifest)
	}
	if manifest.ID != "/" || manifest.StartURL != "/" || manifest.Scope != "/" {
		t.Fatal("shortcut identity or launch target includes an origin, route, or student-specific data")
	}
	if manifest.ThemeColor == "" || manifest.BackgroundColor == "" || len(manifest.Icons) == 0 {
		t.Fatal("shortcut appearance is incomplete")
	}
	for _, icon := range manifest.Icons {
		parsed, err := url.Parse(icon.Src)
		if err != nil || !strings.HasPrefix(icon.Src, "/assets/") || parsed.IsAbs() || parsed.Host != "" || parsed.RawQuery != "" || parsed.Fragment != "" {
			t.Fatalf("icon is not a public current-origin asset: %q", icon.Src)
		}
		content := get(icon.Src, 200, "image/png")
		decoded, err := png.DecodeConfig(bytes.NewReader(content))
		if err != nil {
			t.Fatalf("invalid PNG %s: %v", icon.Src, err)
		}
		if icon.Type != "image/png" || icon.Sizes != fmt.Sprintf("%dx%d", decoded.Width, decoded.Height) {
			t.Fatalf("icon dimensions/type do not match manifest: %+v", icon)
		}
	}
	for _, private := range []string{"/assets/admin.js", "/assets/editor.js", "/assets/storage.js", "/assets/report.js", "/assets/report.css", "/assets/../admin.html"} {
		get(private, 404, "")
	}
}
