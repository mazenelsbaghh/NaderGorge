//go:build windows

package main

import "golang.org/x/sys/windows"

// Winsock returns WSA errors; Go's compatibility ECONN* values on Windows are
// application-only values and do not match errors from an actual TCP socket.
const (
	nativeConnectionRefused = windows.WSAECONNREFUSED
	nativeConnectionReset   = windows.WSAECONNRESET
	nativeSocketLimit       = windows.WSAEMFILE
)
