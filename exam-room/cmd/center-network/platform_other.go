//go:build !windows

package main

import (
	"errors"
	"os"
)

func replaceFile(source, destination string) error { return os.Rename(source, destination) }
func networkInspection(action string) output {
	return output{"ok": true, "supported": false, "configured": false, "ready": false, "adapters": []any{}, "message": "تجهيز شبكة الامتحانات المباشرة متاح في نسخة ويندوز."}
}
func changeNetwork(request input) output {
	return output{"ok": false, "supported": false, "message": "يلزم ويندوز لتطبيق تجهيز الشبكة."}
}
func hotspot(request input) output {
	return output{"ok": false, "supported": false, "active": false, "message": "شبكة الهوست اللاسلكية تحتاج ويندوز ومحولًا يدعم Wi-Fi Direct."}
}
func runNetwork() error { return errors.New("Windows network service required") }
func applyElevated()    { os.Exit(1) }
