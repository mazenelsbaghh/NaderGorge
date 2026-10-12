//go:build windows

package main

import (
	"fmt"
	"net"
	"os"
	"testing"

	"golang.org/x/sys/windows"
)

func TestWrappedWinsockFailuresKeepActionableProxyCategories(t *testing.T) {
	// Native Windows CI 38305179718 exposed the ECONNREFUSED/WSAECONNREFUSED
	// mismatch in the existing real-socket refusal and unavailable-page tests.
	// Cover the other Winsock failures without exhausting the machine's handles.
	for _, scenario := range []struct {
		err      error
		category string
	}{
		{windows.WSAECONNREFUSED, "connection_refused"},
		{windows.WSAECONNRESET, "connection_reset"},
		{windows.WSAEMFILE, "descriptor_limit"},
	} {
		t.Run(scenario.category, func(t *testing.T) {
			err := fmt.Errorf("transport: %w", &net.OpError{Op: "dial", Net: "tcp", Err: &os.SyscallError{
				Syscall: "connectex", Err: scenario.err,
			}})
			if actual := proxyErrorCategory(err); actual != scenario.category {
				t.Fatalf("wrapped Winsock error category=%q, want %q", actual, scenario.category)
			}
		})
	}
}
