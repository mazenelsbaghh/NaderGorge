//go:build windows

package main

import (
	"errors"

	"golang.org/x/sys/windows"
)

func nativeDiagnosticCategory(err error) string {
	switch {
	case errors.Is(err, windows.WSAEADDRINUSE):
		return "address_in_use"
	case errors.Is(err, windows.WSAEADDRNOTAVAIL):
		return "address_unavailable"
	case errors.Is(err, windows.ERROR_DISK_FULL), errors.Is(err, windows.ERROR_HANDLE_DISK_FULL):
		return "storage_full"
	case errors.Is(err, windows.ERROR_TOO_MANY_OPEN_FILES), errors.Is(err, windows.WSAEMFILE):
		return "descriptor_limit"
	case errors.Is(err, windows.ERROR_ACCESS_DENIED), errors.Is(err, windows.ERROR_PRIVILEGE_NOT_HELD), errors.Is(err, windows.WSAEACCES):
		return "permission_denied"
	}
	return ""
}
