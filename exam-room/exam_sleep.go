package main

import (
	"runtime"
	"time"
)

// Execution-state requests belong to a Windows thread, not a process. This
// monitor owns one thread through activation and restoration, including shutdown.
func monitorExamSleep(d *DB, ticks <-chan time.Time, stop <-chan struct{}, setState func(bool) error, report func(error)) {
	runtime.LockOSThread()
	defer runtime.UnlockOSThread()
	protected := false
	defer func() {
		if protected {
			if err := setState(false); err != nil {
				report(err)
			}
		}
	}()
	check := func() {
		var active bool
		if err := d.sql.QueryRow(`SELECT EXISTS(SELECT 1 FROM exams WHERE state IN('waiting','running'))`).Scan(&active); err != nil {
			report(err)
			return // A transient read failure must not release an active room's protection.
		}
		if active != protected {
			if err := setState(active); err != nil {
				report(err)
				return
			}
			protected = active
		}
	}
	check()
	for {
		select {
		case <-stop:
			return
		case <-ticks:
			check()
		}
	}
}
