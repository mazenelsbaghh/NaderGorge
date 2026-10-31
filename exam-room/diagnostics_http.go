package main

import (
	"context"
	"errors"
	"net"
	"net/http"
	"strings"
	"syscall"
	"time"
)

func diagnosticClass(path string, admin bool) string {
	if admin {
		if path == "/api/diagnostics" {
			return "admin_diagnostics"
		}
		if strings.HasPrefix(path, "/report/") {
			return "report"
		}
		return "admin_api"
	}
	switch path {
	case "/":
		return "home"
	case "/api/join":
		return "join"
	case "/api/answers":
		return "answers"
	case "/api/session":
		return "session"
	case "/api/presence":
		return "presence"
	case "/api/lounge":
		return "lounge"
	case "/api/leave":
		return "leave"
	case "/api/client-diagnostics":
		return "client_diagnostics"
	}
	if strings.HasPrefix(path, "/assets/") {
		return "asset"
	}
	return "other"
}

func diagnosticStatusGroup(status int) string {
	switch {
	case status >= 200 && status < 300:
		return "2xx"
	case status >= 300 && status < 400:
		return "3xx"
	case status >= 400 && status < 500:
		return "4xx"
	case status >= 500 && status < 600:
		return "5xx"
	default:
		return "other"
	}
}

func diagnosticErrorCategory(err error) string {
	switch {
	case errors.Is(err, context.Canceled):
		return "canceled"
	case errors.Is(err, context.DeadlineExceeded):
		return "timeout"
	case errors.Is(err, syscall.ENOSPC):
		return "storage_full"
	case errors.Is(err, syscall.EMFILE), errors.Is(err, syscall.ENFILE):
		return "descriptor_limit"
	case errors.Is(err, syscall.EADDRINUSE):
		return "address_in_use"
	case errors.Is(err, syscall.EADDRNOTAVAIL):
		return "address_unavailable"
	case errors.Is(err, syscall.EACCES), errors.Is(err, syscall.EPERM):
		return "permission_denied"
	}
	if category := nativeDiagnosticCategory(err); category != "" {
		return category
	}
	var sqlite interface{ Code() int }
	if errors.As(err, &sqlite) {
		switch sqlite.Code() & 255 {
		case 5, 6:
			return "database_busy"
		case 8:
			return "storage_readonly"
		case 10:
			return "storage_io"
		case 11, 26:
			return "database_corrupt"
		case 13:
			return "storage_full"
		default:
			return "database_error"
		}
	}
	var netError net.Error
	if errors.As(err, &netError) && netError.Timeout() {
		return "timeout"
	}
	var room roomError
	if errors.As(err, &room) {
		switch room.status {
		case 401, 403:
			return "access_denied"
		case 404:
			return "not_found"
		case 409:
			return "conflict"
		case 429:
			return "rate_limited"
		case 408:
			return "timeout"
		case 400, 413, 415, 422:
			return "invalid_request"
		case 503:
			return "unavailable"
		}
	}
	return "internal_error"
}

type diagnosticHTTPResponse struct {
	http.ResponseWriter
	status   int
	category string
}

func (w *diagnosticHTTPResponse) WriteHeader(status int) {
	if w.status != 0 {
		return
	}
	if status >= 200 || status == http.StatusSwitchingProtocols {
		w.status = status
	}
	w.ResponseWriter.WriteHeader(status)
}
func (w *diagnosticHTTPResponse) Write(data []byte) (int, error) {
	if w.status == 0 {
		w.WriteHeader(http.StatusOK)
	}
	n, err := w.ResponseWriter.Write(data)
	if err != nil && w.category == "" {
		w.category = "write_failed"
	}
	return n, err
}
func (w *diagnosticHTTPResponse) Unwrap() http.ResponseWriter { return w.ResponseWriter }
func (w *diagnosticHTTPResponse) diagnosticError(err error) {
	if w.category == "" {
		w.category = diagnosticErrorCategory(err)
	}
}

func (d *diagnosticRecorder) middleware(next http.Handler, admin bool) http.Handler {
	return http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		started := time.Now()
		response := &diagnosticHTTPResponse{ResponseWriter: writer}
		defer func() {
			if recover() != nil {
				response.category = "panic"
				writeErr(response, errors.New("request panic"))
			}
			if response.status == 0 {
				response.status = http.StatusOK
			}
			class := diagnosticClass(request.URL.Path, admin)
			// Polling the recorder and uploading diagnostics must not recursively
			// manufacture a new request event or drown out actual exam requests.
			if class == "admin_diagnostics" || class == "client_diagnostics" {
				return
			}
			source := "student"
			if admin {
				source = "admin"
			}
			d.request(diagnosticEvent{Source: source, Kind: "request", Class: class, Status: response.status,
				DurationMS: time.Since(started).Milliseconds(), Category: response.category})
		}()
		next.ServeHTTP(response, request)
	})
}

func validDiagnosticClass(value string) bool {
	switch value {
	case "", "home", "asset", "join", "answers", "session", "presence", "lounge", "leave",
		"client_diagnostics", "admin_diagnostics", "admin_api", "report", "other":
		return true
	}
	return false
}
func validDiagnosticCategory(value string) bool {
	switch value {
	case "", "canceled", "timeout", "storage_full", "storage_readonly", "storage_io", "descriptor_limit",
		"address_in_use", "address_unavailable", "permission_denied",
		"database_busy", "database_corrupt", "database_error", "access_denied", "not_found", "conflict",
		"rate_limited", "invalid_request", "unavailable", "internal_error", "write_failed", "panic",
		"2xx", "3xx", "4xx", "5xx", "other":
		return true
	}
	return false
}
func validStoredDiagnostic(event diagnosticEvent) bool {
	if len(event.ReceivedAt) > 35 {
		return false
	}
	if _, err := time.Parse(time.RFC3339Nano, event.ReceivedAt); err != nil {
		return false
	}
	if event.ClientTime != "" && !validClientDiagnosticTime(event.ClientTime) {
		return false
	}
	if !validDiagnosticClass(event.Class) || !validDiagnosticCategory(event.Category) || event.Status < 0 || event.Status > 599 || event.DurationMS < 0 {
		return false
	}
	switch event.Source {
	case "backend", "student", "admin", "client":
	default:
		return false
	}
	switch event.Kind {
	case "request", "summary", "startup", "startup_failed", "shutdown", "maintenance", "backup":
		return true
	default:
		return validClientDiagnosticKind(event.Kind)
	}
}
