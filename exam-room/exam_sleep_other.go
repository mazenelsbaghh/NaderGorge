//go:build !windows

package main

func startExamSleepProtection(_ *DB, _ func(error)) func() { return func() {} }
