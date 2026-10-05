//go:build !windows

package main

import "syscall"

func diskFree(path string) int64 {
	var fs syscall.Statfs_t
	if syscall.Statfs(path, &fs) != nil {
		return 0
	}
	return int64(fs.Bavail) * int64(fs.Bsize)
}
