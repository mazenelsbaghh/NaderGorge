//go:build windows

package main

import (
	"fmt"
	"net"
	"os"
	"testing"

	"golang.org/x/sys/windows"
)

func TestDiagnosticCategoriesRecognizeWrappedWindowsSystemErrors(t *testing.T) {
	for _, scenario := range []struct {
		err      error
		category string
	}{
		{windows.WSAEADDRINUSE, "address_in_use"},
		{windows.WSAEADDRNOTAVAIL, "address_unavailable"},
		{windows.ERROR_DISK_FULL, "storage_full"},
		{windows.ERROR_HANDLE_DISK_FULL, "storage_full"},
		{windows.ERROR_TOO_MANY_OPEN_FILES, "descriptor_limit"},
		{windows.WSAEMFILE, "descriptor_limit"},
		{windows.ERROR_ACCESS_DENIED, "permission_denied"},
		{windows.ERROR_PRIVILEGE_NOT_HELD, "permission_denied"},
		{windows.WSAEACCES, "permission_denied"},
	} {
		err := &net.OpError{Op: "listen", Net: "tcp", Err: &os.SyscallError{Syscall: "bind", Err: fmt.Errorf("wrapped: %w", scenario.err)}}
		if got := diagnosticErrorCategory(err); got != scenario.category {
			t.Fatalf("%v classified as %s, wanted %s", scenario.err, got, scenario.category)
		}
	}
}
