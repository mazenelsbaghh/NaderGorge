package main

import (
	"encoding/json"
	"io"
	"net"
	"net/http"
	"sync"
	"time"
)

const clientDiagnosticBodyLimit = 8192

type clientDiagnosticEvent struct {
	Event      string `json:"event"`
	At         string `json:"at"`
	Route      string `json:"route,omitempty"`
	Status     *int   `json:"status,omitempty"`
	DurationMS *int64 `json:"durationMs,omitempty"`
}

func validClientDiagnosticKind(kind string) bool {
	switch kind {
	case "startup_ready", "connection_lost", "connection_restored", "request_failed", "js_error", "promise_rejection":
		return true
	}
	return false
}
func validClientDiagnosticTime(value string) bool {
	if len(value) > 35 {
		return false
	}
	parsed, err := time.Parse(time.RFC3339Nano, value)
	return err == nil && parsed.Year() >= 2000 && parsed.Year() <= 2100
}
func validClientDiagnostic(event clientDiagnosticEvent) bool {
	if !validClientDiagnosticKind(event.Event) || !validClientDiagnosticTime(event.At) {
		return false
	}
	if event.Event != "request_failed" {
		return event.Route == "" && event.Status == nil && event.DurationMS == nil
	}
	switch event.Route {
	case "join", "answers", "session", "presence", "lounge":
	default:
		return false
	}
	return event.Status != nil && *event.Status >= 0 && *event.Status <= 599 && event.DurationMS != nil && *event.DurationMS >= 0 && *event.DurationMS <= 60000
}

type clientDiagnosticBucket struct {
	start  time.Time
	events int
}
type clientDiagnosticLimits struct {
	mu        sync.Mutex
	global    clientDiagnosticBucket
	addresses map[string]clientDiagnosticBucket
}

func (limits *clientDiagnosticLimits) allow(address string, count int, now time.Time) bool {
	limits.mu.Lock()
	defer limits.mu.Unlock()
	if now.Sub(limits.global.start) >= time.Minute {
		limits.global = clientDiagnosticBucket{start: now}
	}
	if limits.global.events+count > 2000 {
		return false
	}
	if limits.addresses == nil {
		limits.addresses = map[string]clientDiagnosticBucket{}
	}
	bucket, exists := limits.addresses[address]
	if !exists && len(limits.addresses) >= 1024 {
		for key, value := range limits.addresses {
			if now.Sub(value.start) >= time.Minute {
				delete(limits.addresses, key)
			}
		}
		if len(limits.addresses) >= 1024 {
			return false
		}
	}
	if now.Sub(bucket.start) >= time.Minute {
		bucket = clientDiagnosticBucket{start: now}
	}
	if bucket.events+count > 60 {
		return false
	}
	bucket.events += count
	limits.addresses[address] = bucket
	limits.global.events += count
	return true
}

func diagnosticClientAddress(request *http.Request) string {
	address, _, err := net.SplitHostPort(request.RemoteAddr)
	if err != nil {
		return "unknown"
	}
	peer := net.ParseIP(address)
	// Only the local portal may supply this header; it replaces client input
	// with the actual socket peer. The address is rate-limit memory only.
	if peer != nil && peer.IsLoopback() {
		if forwarded := net.ParseIP(request.Header.Get("X-Massar-Client-IP")); forwarded != nil {
			return forwarded.String()
		}
	}
	return address
}

func (a *app) clientDiagnostics(writer http.ResponseWriter, request *http.Request) error {
	if a.diagnostics == nil {
		return fail("التشخيص غير متاح", 503)
	}
	address := diagnosticClientAddress(request)
	// Charge malformed batches too, before reading their body. Valid batches
	// consume their remaining event budget only after strict validation.
	if !a.diagnostics.clients.allow(address, 1, time.Now()) {
		a.diagnostics.mu.Lock()
		a.diagnostics.clientRejectedBatches++
		a.diagnostics.mu.Unlock()
		writer.Header().Set("Retry-After", "60")
		return fail("انتظر قبل إرسال سجل تشخيص آخر", 429)
	}
	if request.ContentLength <= 0 || request.ContentLength > clientDiagnosticBodyLimit || len(request.TransferEncoding) > 0 {
		return fail("حجم سجل التشخيص غير مسموح", 413)
	}
	var batch struct {
		Events []clientDiagnosticEvent `json:"events"`
	}
	decoder := json.NewDecoder(io.LimitReader(request.Body, clientDiagnosticBodyLimit+1))
	decoder.DisallowUnknownFields()
	if decoder.Decode(&batch) != nil || len(batch.Events) == 0 || len(batch.Events) > 10 {
		return fail("سجل التشخيص غير صالح", 400)
	}
	var extra any
	if decoder.Decode(&extra) != io.EOF {
		return fail("سجل التشخيص غير صالح", 400)
	}
	for _, event := range batch.Events {
		if !validClientDiagnostic(event) {
			return fail("سجل التشخيص غير صالح", 400)
		}
	}
	if len(batch.Events) > 1 && !a.diagnostics.clients.allow(address, len(batch.Events)-1, time.Now()) {
		a.diagnostics.mu.Lock()
		a.diagnostics.clientRejectedBatches++
		a.diagnostics.mu.Unlock()
		writer.Header().Set("Retry-After", "60")
		return fail("انتظر قبل إرسال سجل تشخيص آخر", 429)
	}
	for _, event := range batch.Events {
		stored := diagnosticEvent{Source: "client", Kind: event.Event, Class: event.Route, ClientTime: event.At}
		if event.Status != nil {
			stored.Status = *event.Status
		}
		if event.DurationMS != nil {
			stored.DurationMS = *event.DurationMS
		}
		a.diagnostics.record(stored)
	}
	jsonOut(writer, M{"accepted": len(batch.Events)})
	return nil
}
