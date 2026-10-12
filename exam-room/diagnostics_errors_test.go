package main

import (
	"context"
	"fmt"
	"net"
	"os"
	"syscall"
	"testing"
)

func TestDiagnosticCategoriesPreserveWrappedPortableErrors(t *testing.T) {
	for _, scenario := range []struct {
		err      error
		category string
	}{
		{context.Canceled, "canceled"},
		{context.DeadlineExceeded, "timeout"},
		{syscall.EADDRINUSE, "address_in_use"},
		{syscall.EADDRNOTAVAIL, "address_unavailable"},
		{syscall.ENOSPC, "storage_full"},
		{syscall.EMFILE, "descriptor_limit"},
		{syscall.EACCES, "permission_denied"},
	} {
		err := &net.OpError{Op: "listen", Net: "tcp", Err: &os.SyscallError{Syscall: "bind", Err: fmt.Errorf("wrapped: %w", scenario.err)}}
		if got := diagnosticErrorCategory(err); got != scenario.category {
			t.Fatalf("%v classified as %s, wanted %s", scenario.err, got, scenario.category)
		}
	}
}
