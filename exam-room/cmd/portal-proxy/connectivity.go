package main

import (
	"io"
	"net/http"
)

// Only plain HTTP checks are answered. HTTPS validation and handset network
// selection remain under the phone's control; this LAN has no Internet uplink.
func connectivityResponse(w http.ResponseWriter, r *http.Request, host string) bool {
	if (r.Method != http.MethodGet && r.Method != http.MethodHead) || r.Header.Get("Origin") != "" {
		return false
	}
	status, contentType, body := http.StatusOK, "text/plain; charset=utf-8", ""
	switch host + r.URL.Path {
	case "connectivitycheck.gstatic.com/generate_204", "connectivitycheck.android.com/generate_204",
		"clients3.google.com/generate_204", "www.google.com/generate_204", "play.googleapis.com/generate_204":
		status = http.StatusNoContent
	case "captive.apple.com/hotspot-detect.html", "www.apple.com/library/test/success.html":
		contentType = "text/html; charset=utf-8"
		body = "<HTML><HEAD><TITLE>Success</TITLE></HEAD><BODY>Success</BODY></HTML>"
	case "www.msftconnecttest.com/connecttest.txt":
		body = "Microsoft Connect Test"
	case "www.msftncsi.com/ncsi.txt":
		body = "Microsoft NCSI"
	default:
		return false
	}
	w.Header().Set("Cache-Control", "no-store")
	w.Header().Set("Content-Type", contentType)
	w.WriteHeader(status)
	if r.Method != http.MethodHead {
		io.WriteString(w, body)
	}
	return true
}
