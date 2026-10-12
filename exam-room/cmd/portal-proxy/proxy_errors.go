package main

import (
	"context"
	"errors"
	"io"
	"net"
	"net/http"
	"syscall"
)

type proxyFailureKey struct{}
type proxyFailure struct{ category string }

func studentProxyError(writer http.ResponseWriter, request *http.Request, err error) {
	if failure, ok := request.Context().Value(proxyFailureKey{}).(*proxyFailure); ok {
		failure.category = proxyErrorCategory(err)
	}
	// A canceled browser request can reach here too; 502 alone does not establish
	// that the backend stopped. Log only a safe category, never URLs or payloads.
	writer.Header().Set("Cache-Control", "no-store")
	if request.URL.Path == "/" && (request.Method == http.MethodGet || request.Method == http.MethodHead) {
		writePortalUnavailable(writer, request)
		return
	}
	writer.WriteHeader(http.StatusBadGateway)
}

func proxyErrorCategory(err error) string {
	switch {
	case errors.Is(err, context.Canceled):
		return "request_canceled"
	case errors.Is(err, syscall.ECONNREFUSED):
		return "connection_refused"
	case errors.Is(err, syscall.EMFILE), errors.Is(err, syscall.ENFILE):
		return "descriptor_limit"
	case errors.Is(err, syscall.ECONNRESET):
		return "connection_reset"
	case errors.Is(err, io.EOF), errors.Is(err, io.ErrUnexpectedEOF):
		return "connection_closed"
	}
	var networkError net.Error
	if errors.As(err, &networkError) && networkError.Timeout() {
		return "timeout"
	}
	return "transport_error"
}
