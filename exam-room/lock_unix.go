//go:build !windows

package main

import (
	"errors"
	"os"
	"path/filepath"
	"syscall"
)

func dataLock(dir string) (func(), error) {
	p := filepath.Join(dir, ".running.lock")
	f, e := os.OpenFile(p, os.O_CREATE|os.O_RDWR, 0600)
	if e != nil {
		return nil, e
	}
	if e = syscall.Flock(int(f.Fd()), syscall.LOCK_EX|syscall.LOCK_NB); e != nil {
		f.Close()
		return nil, errors.New("هذه البيانات مفتوحة في نسخة أخرى من البرنامج. أغلق النسخة الأخرى أولًا.")
	}
	return func() { _ = syscall.Flock(int(f.Fd()), syscall.LOCK_UN); _ = f.Close() }, nil
}
