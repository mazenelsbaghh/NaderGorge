package main

import (
	"context"
	"encoding/json"
	"io"
	"log"
	"net"
	"net/http"
	"os"
	"strings"
	"sync"
	"time"
)

// A current file plus one previous file keeps diagnostics bounded during exams.
const diagnosticLimit = 5 * 1024 * 1024

type diagnosticWriter struct {
	mu   sync.Mutex
	path string
	file *os.File
	size int64
}

func openDiagnostics(path string) (*diagnosticWriter, error) {
	file, err := os.OpenFile(path, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0644)
	if err != nil {
		return nil, err
	}
	info, err := file.Stat()
	if err != nil {
		file.Close()
		return nil, err
	}
	return &diagnosticWriter{path: path, file: file, size: info.Size()}, nil
}

func (writer *diagnosticWriter) Write(data []byte) (int, error) {
	writer.mu.Lock()
	defer writer.mu.Unlock()
	if writer.size+int64(len(data)) > diagnosticLimit {
		if err := writer.file.Close(); err != nil {
			return 0, err
		}
		if err := os.Rename(writer.path, writer.path+".1"); err != nil {
			return 0, err
		}
		file, err := os.OpenFile(writer.path, os.O_CREATE|os.O_APPEND|os.O_WRONLY, 0644)
		if err != nil {
			return 0, err
		}
		writer.file = file
		writer.size = 0
	}
	count, err := writer.file.Write(data)
	writer.size += int64(count)
	return count, err
}

type diagnosticResponse struct {
	http.ResponseWriter
	status int
}

func (response *diagnosticResponse) WriteHeader(status int) {
	if response.status != 0 {
		return
	}
	// Informational replies precede a final status; treating 103 or 100 as final
	// would suppress the real error and let net/http implicitly report 200.
	if status >= 200 || status == http.StatusSwitchingProtocols {
		response.status = status
	}
	response.ResponseWriter.WriteHeader(status)
}
func (response *diagnosticResponse) Write(data []byte) (int, error) {
	if response.status == 0 {
		response.WriteHeader(http.StatusOK)
	}
	return response.ResponseWriter.Write(data)
}
func (response *diagnosticResponse) Unwrap() http.ResponseWriter { return response.ResponseWriter }

type portalEvent struct {
	Time       string `json:"time"`
	IP         string `json:"ip"`
	Host       string `json:"host"`
	Kind       string `json:"kind"`
	Method     string `json:"method"`
	Status     int    `json:"status"`
	DurationMS int64  `json:"durationMs"`
	Browser    string `json:"browser"`
	ProxyError string `json:"proxyError,omitempty"`
}

func diagnosticKind(request *http.Request, status int) string {
	if request.URL.Path == "/welcome" || request.URL.Path == "/welcome/ready" {
		return "welcome_page"
	}
	if request.URL.Path == "/welcome/accept" {
		return "welcome_accept"
	}
	if request.URL.Path == "/capport" {
		return "capport"
	}
	host := strings.TrimSuffix(request.Host, ":80")
	if request.TLS == nil && !localHost(host) {
		return "probe"
	}
	if request.URL.Path == "/" {
		return "student_page"
	}
	if request.URL.Path == "/api/lounge" {
		return "room_connection"
	}
	if request.URL.Path == "/api/join" {
		return "student_join"
	}
	if request.URL.Path == "/api/answers" {
		return "answer_save"
	}
	if status >= 400 && request.URL.Path == "/api/session" {
		return "student_session"
	}
	if status >= 400 && strings.HasPrefix(request.URL.Path, "/assets/") {
		return "asset_error"
	}
	if status >= 400 {
		return "request_error"
	}
	return ""
}

func diagnosticHandler(next http.Handler, sink io.Writer) http.Handler {
	return http.HandlerFunc(func(writer http.ResponseWriter, request *http.Request) {
		started := time.Now()
		response := &diagnosticResponse{ResponseWriter: writer}
		failure := &proxyFailure{}
		request = request.WithContext(context.WithValue(request.Context(), proxyFailureKey{}, failure))
		next.ServeHTTP(response, request)
		status := response.status
		if status == 0 {
			status = http.StatusOK
		}
		kind := diagnosticKind(request, status)
		if kind == "" {
			return
		}
		ip, _, err := net.SplitHostPort(request.RemoteAddr)
		if err != nil {
			ip = request.RemoteAddr
		}
		event := portalEvent{Time: started.UTC().Format(time.RFC3339Nano), IP: ip,
			Host: clipDiagnostic(request.Host, 253), Kind: kind, Method: request.Method,
			Status: status, DurationMS: time.Since(started).Milliseconds(), Browser: clipDiagnostic(request.UserAgent(), 200),
			ProxyError: failure.category}
		// Request bodies, cookies, query strings and redirect URLs contain student secrets.
		if err := json.NewEncoder(sink).Encode(event); err != nil {
			log.Printf("portal diagnostic write failed: %v", err)
		}
	})
}

func clipDiagnostic(value string, limit int) string {
	if len(value) > limit {
		return value[:limit]
	}
	return value
}
