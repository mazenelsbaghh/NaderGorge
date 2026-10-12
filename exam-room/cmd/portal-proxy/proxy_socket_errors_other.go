//go:build !windows

package main

import "syscall"

const (
	nativeConnectionRefused = syscall.ECONNREFUSED
	nativeConnectionReset   = syscall.ECONNRESET
	nativeSocketLimit       = syscall.EMFILE
)
