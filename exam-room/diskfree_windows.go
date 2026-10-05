//go:build windows

package main

import "golang.org/x/sys/windows"

func diskFree(path string) int64 {
	directory, err := windows.UTF16PtrFromString(path)
	if err != nil {
		return 0
	}
	var available uint64
	if windows.GetDiskFreeSpaceEx(directory, &available, nil, nil) != nil {
		return 0
	}
	return int64(available)
}
