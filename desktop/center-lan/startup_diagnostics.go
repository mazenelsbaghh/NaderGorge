package main

import (
	"errors"
	"syscall"
)

type startupError struct {
	stage string
	cause error
}

func (e *startupError) Error() string { return "gateway_start_failed" }
func (e *startupError) Unwrap() error { return e.cause }

// Only fixed stages and errno cross the process boundary, never paths or config.
func startupDiagnostic(err error) map[string]any {
	diagnostic := map[string]any{"ready": false, "error": "gateway_start_failed", "stage": "ready"}
	var failure *startupError
	if errors.As(err, &failure) {
		diagnostic["stage"] = failure.stage
	}
	var code syscall.Errno
	if errors.As(err, &code) {
		diagnostic["osCode"] = uint64(code)
	}
	return diagnostic
}
