package main

import (
	"bytes"
	"fmt"
	"net/http"
	"net/http/httptest"
	"net/url"
	"regexp"
	"strings"
	"sync"
	"testing"
	"time"
)

func welcomeRequest(portal http.Handler, ip, method, address, form string) *httptest.ResponseRecorder {
	r := httptest.NewRequest(method, address, strings.NewReader(form))
	r.RemoteAddr = ip + ":41234"
	if method == http.MethodPost {
		r.Header.Set("Content-Type", "application/x-www-form-urlencoded")
	}
	w := httptest.NewRecorder()
	portal.ServeHTTP(w, r)
	return w
}

func acceptWelcome(portal http.Handler, ip string) *httptest.ResponseRecorder {
	page := welcomeRequest(portal, ip, "GET", "http://localhost/welcome", "")
	match := regexp.MustCompile(`name="token" value="([^"]+)"`).FindStringSubmatch(page.Body.String())
	if len(match) != 2 {
		return page
	}
	return welcomeRequest(portal, ip, "POST", "http://localhost/welcome/accept", url.Values{"token": {match[1]}}.Encode())
}

func TestWelcomeStartChangesOnlyThatClientsHTTPChecksAndLeavesExamServiceAvailable(t *testing.T) {
	portal, err := newWelcomePortal()
	if err != nil {
		t.Fatal(err)
	}
	backend := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Path != "/api/answers" || r.Method != "POST" {
			t.Errorf("welcome traffic reached exam backend: %s %s", r.Method, r.URL.Path)
		}
		w.WriteHeader(http.StatusNoContent)
	}))
	defer backend.Close()
	target, _ := url.Parse(backend.URL)
	portal.student = studentProxy(target)
	for _, address := range []string{"http://connectivitycheck.gstatic.com/generate_204", "http://captive.apple.com/hotspot-detect.html"} {
		before := welcomeRequest(portal, "10.77.0.25", "GET", address, "")
		if before.Code != 302 || before.Header().Get("Location") != welcomeURL {
			t.Fatalf("before Start: %d %v", before.Code, before.Header())
		}
	}
	var log bytes.Buffer
	saved := acceptWelcome(diagnosticHandler(portal, &log), "10.77.0.25")
	if saved.Code != 303 || saved.Header().Get("Location") != "/welcome/ready" {
		t.Fatalf("Start: %d %s", saved.Code, saved.Body.String())
	}
	if !strings.Contains(log.String(), "welcome_accept") || strings.Contains(log.String(), "token") {
		t.Fatalf("acceptance log missing or exposes token: %s", log.String())
	}
	for _, check := range []struct {
		address string
		status  int
		body    string
	}{
		{"http://connectivitycheck.gstatic.com/generate_204", 204, ""},
		{"http://captive.apple.com/hotspot-detect.html", 200, "Success"},
	} {
		after := welcomeRequest(portal, "10.77.0.25", "GET", check.address, "")
		if after.Code != check.status || !strings.Contains(after.Body.String(), check.body) || after.Header().Get("Location") != "" {
			t.Fatalf("after Start: %d %s", after.Code, after.Body.String())
		}
		other := welcomeRequest(portal, "10.77.0.26", "GET", check.address, "")
		if other.Code != 302 {
			t.Fatal("one phone accepted for another")
		}
	}
	for _, ip := range []string{"10.77.0.25", "10.77.0.26"} {
		answer := welcomeRequest(portal, ip, "POST", "http://localhost/api/answers", "answer=fixture")
		if answer.Code != 204 {
			t.Fatalf("exam saving affected by welcome: %d", answer.Code)
		}
	}
	foreign := welcomeRequest(portal, "10.77.0.25", "GET", "http://captive.apple.com/api/session", "")
	if foreign.Code != 404 {
		t.Fatal("foreign host exposes exam service")
	}
	ready := welcomeRequest(portal, "10.77.0.25", "GET", "http://localhost/welcome/ready", "")
	if !strings.Contains(ready.Body.String(), "http://10.77.0.1/") || strings.Contains(ready.Body.String(), "<form") {
		t.Fatal("missing browser handoff instructions")
	}
}

func TestWelcomeRejectsForgedExpiredAndCrossOriginAcceptance(t *testing.T) {
	for _, scenario := range []string{"invalid", "expired", "other_ip", "origin", "foreign_host", "get", "too_large"} {
		t.Run(scenario, func(t *testing.T) {
			portal, err := newWelcomePortal()
			if err != nil {
				t.Fatal(err)
			}
			now := time.Now()
			portal.now = func() time.Time { return now }
			ip, tokenIP := "10.77.0.25", "10.77.0.25"
			if scenario == "other_ip" {
				tokenIP = "10.77.0.26"
			}
			token := portal.token(tokenIP, fmt.Sprint(now.Add(time.Minute).Unix()))
			if scenario == "invalid" {
				token = "invalid"
			}
			if scenario == "expired" {
				now = now.Add(2 * time.Minute)
			}
			address, method := "http://localhost/welcome/accept", "POST"
			if scenario == "foreign_host" {
				address = "http://other.test/welcome/accept"
			}
			if scenario == "get" {
				method = "GET"
			}
			form := url.Values{"token": {token}}.Encode()
			if scenario == "too_large" {
				form += "&overflow=" + strings.Repeat("x", 4096)
			}
			r := httptest.NewRequest(method, address, strings.NewReader(form))
			r.RemoteAddr = ip + ":45454"
			r.Header.Set("Content-Type", "application/x-www-form-urlencoded")
			r.Header.Set("X-Forwarded-For", tokenIP)
			if scenario == "origin" {
				r.Header.Set("Origin", "http://other.test")
			}
			w := httptest.NewRecorder()
			portal.ServeHTTP(w, r)
			if w.Code != 403 && w.Code != 405 {
				t.Fatalf("accepted %s: %d", scenario, w.Code)
			}
			probe := welcomeRequest(portal, ip, "GET", "http://connectivitycheck.gstatic.com/generate_204", "")
			if probe.Code != 302 {
				t.Fatal("failed submission changed connectivity checks")
			}
		})
	}
}

func TestWelcomeThousandConcurrentPhonesAcceptIndependently(t *testing.T) {
	portal, err := newWelcomePortal()
	if err != nil {
		t.Fatal(err)
	}
	var workers sync.WaitGroup
	for index := range 1000 {
		workers.Go(func() {
			ip := fmt.Sprintf("10.77.%d.%d", index/250, index%250+1)
			if accepted := acceptWelcome(portal, ip); accepted.Code != 303 {
				t.Errorf("Start %s: %d", ip, accepted.Code)
				return
			}
			probe := welcomeRequest(portal, ip, "GET", "http://connectivitycheck.gstatic.com/generate_204", "")
			if probe.Code != 204 {
				t.Errorf("check %s: %d", ip, probe.Code)
			}
		})
	}
	workers.Wait()
}

func TestWelcomeCapacityExpiryAndRestartDoNotRetainAnUnboundedClientHistory(t *testing.T) {
	portal, err := newWelcomePortal()
	if err != nil {
		t.Fatal(err)
	}
	now := time.Now()
	portal.now = func() time.Time { return now }
	for index := range welcomeCapacity {
		ip := fmt.Sprintf("10.90.%d.%d", index/250, index%250+1)
		if accepted := acceptWelcome(portal, ip); accepted.Code != 303 {
			t.Fatalf("capacity %d: %d", index, accepted.Code)
		}
	}
	if overflow := acceptWelcome(portal, "10.77.0.99"); overflow.Code != 503 {
		t.Fatalf("capacity exceeded: %d", overflow.Code)
	}
	now = now.Add(4 * time.Hour)
	if expired := welcomeRequest(portal, "10.90.0.1", "GET", "http://connectivitycheck.gstatic.com/generate_204", ""); expired.Code != 302 {
		t.Fatal("expired selection retained")
	}
	if recovered := acceptWelcome(portal, "10.77.0.99"); recovered.Code != 303 {
		t.Fatal("expired slots not reclaimed")
	}
	restarted, err := newWelcomePortal()
	if err != nil {
		t.Fatal(err)
	}
	if probe := welcomeRequest(restarted, "10.77.0.99", "GET", "http://connectivitycheck.gstatic.com/generate_204", ""); probe.Code != 302 {
		t.Fatal("restart retained old acceptance")
	}
}
