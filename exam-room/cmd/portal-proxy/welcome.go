package main

import (
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	_ "embed"
	"encoding/hex"
	"html/template"
	"log"
	"net"
	"net/http"
	"strconv"
	"strings"
	"sync"
	"time"
)

//go:embed welcome.html
var welcomeHTML string

const welcomeURL = "http://10.77.0.1/welcome"
const welcomeCapacity = 4096

var welcomePage = template.Must(template.New("welcome").Parse(welcomeHTML))

type welcomePortal struct {
	mu       sync.Mutex
	accepted map[string]time.Time
	secret   [32]byte
	now      func() time.Time
	student  http.Handler
}

func newWelcomePortal() (*welcomePortal, error) {
	portal := &welcomePortal{accepted: make(map[string]time.Time), now: time.Now, student: networkHandler(true)}
	_, err := rand.Read(portal.secret[:])
	return portal, err
}

func welcomeClient(r *http.Request) string {
	host, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil || net.ParseIP(host) == nil {
		return ""
	}
	return net.ParseIP(host).String()
}

func (portal *welcomePortal) hasAccepted(ip string) bool {
	portal.mu.Lock()
	defer portal.mu.Unlock()
	if portal.now().Before(portal.accepted[ip]) {
		return true
	}
	delete(portal.accepted, ip)
	return false
}

func (portal *welcomePortal) accept(ip string) bool {
	portal.mu.Lock()
	defer portal.mu.Unlock()
	now := portal.now()
	if len(portal.accepted) >= welcomeCapacity {
		for client, expiry := range portal.accepted {
			if !now.Before(expiry) {
				delete(portal.accepted, client)
			}
		}
	}
	if _, exists := portal.accepted[ip]; !exists && len(portal.accepted) >= welcomeCapacity {
		return false
	}
	portal.accepted[ip] = now.Add(4 * time.Hour)
	return true
}

func (portal *welcomePortal) token(ip, expiry string) string {
	signature := hmac.New(sha256.New, portal.secret[:])
	signature.Write([]byte(ip + ":" + expiry))
	return expiry + "." + hex.EncodeToString(signature.Sum(nil))
}

func (portal *welcomePortal) validToken(ip, token string) bool {
	expiry, _, found := strings.Cut(token, ".")
	seconds, err := strconv.ParseInt(expiry, 10, 64)
	if !found || err != nil {
		return false
	}
	remaining := time.Unix(seconds, 0).Sub(portal.now())
	return remaining > 0 && remaining <= 10*time.Minute && hmac.Equal([]byte(token), []byte(portal.token(ip, expiry)))
}

func (portal *welcomePortal) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	host := strings.ToLower(strings.TrimSuffix(r.Host, ":80"))
	ip := welcomeClient(r)
	w.Header().Set("Cache-Control", "no-store")
	if ip == "" {
		http.Error(w, "Unknown client address", http.StatusBadRequest)
		return
	}
	if !localHost(host) {
		portal.probe(w, r, host, ip)
		return
	}
	if r.URL.Path == "/welcome" || r.URL.Path == "/welcome/ready" {
		portal.page(w, r, ip)
		return
	}
	if r.URL.Path == "/welcome/accept" {
		portal.submit(w, r, ip)
		return
	}
	portal.student.ServeHTTP(w, r)
}

func (portal *welcomePortal) probe(w http.ResponseWriter, r *http.Request, host, ip string) {
	if (r.Method != http.MethodGet && r.Method != http.MethodHead) || r.Header.Get("Origin") != "" {
		http.Error(w, "Invalid local request", http.StatusForbidden)
		return
	}
	if portal.hasAccepted(ip) {
		if !connectivityResponse(w, r, host) {
			http.Error(w, "Offline exam network: open http://10.77.0.1/", http.StatusNotFound)
		}
		return
	}
	http.Redirect(w, r, welcomeURL, http.StatusFound)
}

func (portal *welcomePortal) page(w http.ResponseWriter, r *http.Request, ip string) {
	if r.Method != http.MethodGet && r.Method != http.MethodHead {
		w.Header().Set("Allow", "GET, HEAD")
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	w.Header().Set("Content-Security-Policy", "default-src 'none'; style-src 'unsafe-inline'; font-src 'self'; img-src 'self'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'")
	if r.Method == http.MethodHead {
		return
	}
	view := struct {
		Accepted bool
		Token    string
	}{portal.hasAccepted(ip), portal.token(ip, strconv.FormatInt(portal.now().Add(10*time.Minute).Unix(), 10))}
	if err := welcomePage.Execute(w, view); err != nil {
		log.Print("welcome response incomplete; client can retry")
	}
}

func (portal *welcomePortal) submit(w http.ResponseWriter, r *http.Request, ip string) {
	if r.Method != http.MethodPost {
		w.Header().Set("Allow", "POST")
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}
	if origin := r.Header.Get("Origin"); origin != "" && origin != "http://"+r.Host {
		http.Error(w, "Invalid local request", http.StatusForbidden)
		return
	}
	r.Body = http.MaxBytesReader(w, r.Body, 2048)
	if err := r.ParseForm(); err != nil || !portal.validToken(ip, r.PostForm.Get("token")) {
		http.Error(w, "افتح شاشة الترحيب من جديد واضغط ابدأ.", http.StatusForbidden)
		return
	}
	if !portal.accept(ip) {
		w.Header().Set("Retry-After", "10")
		http.Error(w, "الشبكة مشغولة، انتظر قليلًا ثم أعد المحاولة.", http.StatusServiceUnavailable)
		return
	}
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	http.Redirect(w, r, "/welcome/ready", http.StatusSeeOther)
}
