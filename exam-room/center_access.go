package main

import (
	"crypto/subtle"
	"net/http"
	"sync"
	"time"
)

type centerBrowserAccess struct {
	mu                sync.Mutex
	token, capability string
	expires           time.Time
	leaseExpires      time.Time
}

const centerBrowserLease = 90 * time.Second

func sameSecret(left, right string) bool {
	return left != "" && right != "" && subtle.ConstantTimeCompare([]byte(left), []byte(right)) == 1
}
func (a *app) browserTokenValid(value string) bool {
	a.centerAccess.mu.Lock()
	defer a.centerAccess.mu.Unlock()
	return time.Now().Before(a.centerAccess.leaseExpires) && sameSecret(value, a.centerAccess.token)
}

// Only the private desktop controller may keep an existing browser session alive.
// A reconnect must explicitly open a new session; polling cannot revive a stale one.
func (a *app) renewCenterBrowserLease() {
	a.centerAccess.mu.Lock()
	defer a.centerAccess.mu.Unlock()
	if a.centerAccess.token != "" && time.Now().Before(a.centerAccess.leaseExpires) {
		a.centerAccess.leaseExpires = time.Now().Add(centerBrowserLease)
	} else {
		a.centerAccess.token = ""
		a.centerAccess.capability = ""
	}
}
func (a *app) centerAdminAccess(p M) (M, error) {
	enabled, e := boolField(p, "enabled")
	if e != nil {
		return nil, e
	}
	a.centerAccess.mu.Lock()
	defer a.centerAccess.mu.Unlock()
	a.centerAccess.token = ""
	a.centerAccess.capability = ""
	a.centerAccess.leaseExpires = time.Time{}
	result := M{"enabled": enabled}
	if enabled {
		a.centerAccess.token = token()
		a.centerAccess.capability = token()
		a.centerAccess.expires = time.Now().Add(time.Minute)
		a.centerAccess.leaseExpires = time.Now().Add(centerBrowserLease)
		result["adminUrl"] = a.adminOrigin() + "/#center-access=" + a.centerAccess.capability
		result["expiresInSeconds"] = 60
	}
	return result, nil
}
func (a *app) centerBrowserLogin(w http.ResponseWriter, r *http.Request) error {
	p, e := payload(r)
	if e != nil {
		return e
	}
	a.centerAccess.mu.Lock()
	defer a.centerAccess.mu.Unlock()
	if !time.Now().Before(a.centerAccess.expires) || !time.Now().Before(a.centerAccess.leaseExpires) || !sameSecret(str(p["capability"]), a.centerAccess.capability) {
		return fail("افتح إدارة الامتحان من برنامج السنتر على هذا الجهاز", 401)
	}
	a.centerAccess.capability = ""
	http.SetCookie(w, &http.Cookie{Name: "massar_admin", Value: a.centerAccess.token, Path: "/", HttpOnly: true, SameSite: http.SameSiteStrictMode})
	jsonOut(w, M{"authorized": true})
	return nil
}
