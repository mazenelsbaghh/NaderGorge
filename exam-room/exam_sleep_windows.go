//go:build windows

package main

import (
	"fmt"
	"sync"
	"time"

	"golang.org/x/sys/windows"
)

var setThreadExecutionState = windows.NewLazySystemDLL("kernel32.dll").NewProc("SetThreadExecutionState")

func setExamSleepProtection(active bool) error {
	const continuous = 0x80000000
	const systemRequired = 0x00000001
	flags := uintptr(continuous)
	if active {
		flags |= systemRequired
	}
	previous, _, err := setThreadExecutionState.Call(flags)
	if previous == 0 {
		return fmt.Errorf("exam sleep protection failed: %v", err)
	}
	return nil
}

func startExamSleepProtection(d *DB, report func(error)) func() {
	stop, done := make(chan struct{}), make(chan struct{})
	ticker := time.NewTicker(time.Second)
	go func() {
		defer close(done)
		defer ticker.Stop()
		monitorExamSleep(d, ticker.C, stop, setExamSleepProtection, report)
	}()
	var once sync.Once
	return func() { once.Do(func() { close(stop); <-done }) }
}
