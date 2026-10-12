//go:build !windows

package main

func nativeDiagnosticCategory(error) string { return "" }
