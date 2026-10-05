//go:build windows

package main

import (
	"errors"
	"golang.org/x/sys/windows"
	"os"
	"path/filepath"
)

func dataLock(dir string) (func(), error) {
	p := filepath.Join(dir, ".running.lock")
	f, e := os.OpenFile(p, os.O_CREATE|os.O_RDWR, 0600)
	if e != nil {
		return nil, e
	}
	var o windows.Overlapped
	if e = windows.LockFileEx(windows.Handle(f.Fd()), windows.LOCKFILE_EXCLUSIVE_LOCK|windows.LOCKFILE_FAIL_IMMEDIATELY, 0, 1, 0, &o); e != nil {
		f.Close()
		return nil, errors.New("هذه البيانات مفتوحة في نسخة أخرى من البرنامج. أغلق النسخة الأخرى أولًا.")
	}
	return func() { _ = windows.UnlockFileEx(windows.Handle(f.Fd()), 0, 1, 0, &o); _ = f.Close() }, nil
}
