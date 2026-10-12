package main

import (
	"errors"
	"net/url"
	"strings"
)

// SQLite expects Windows drive paths as /C:/..., not a URI authority named C:.
// The caller resolves the native path to absolute before invoking this helper.
func readOnlyBackupURI(absolutePath, goos string) (string, error) {
	path := absolutePath
	if goos == "windows" {
		path = strings.ReplaceAll(path, `\`, "/")
		if strings.HasPrefix(path, "//?/") && len(path) >= 7 && path[5] == ':' && path[6] == '/' {
			path = path[4:]
		}
		if strings.HasPrefix(path, "//") {
			return "", errors.New("احفظ بيانات الامتحان على قرص محلي؛ مسارات الشبكة والأجهزة غير مدعومة للنسخ الاحتياطي")
		}
		if len(path) < 3 || !((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) || path[1] != ':' || path[2] != '/' {
			return "", errors.New("مسار بيانات الامتحان يجب أن يكون مسارًا كاملًا على قرص ويندوز محلي")
		}
		path = "/" + path
	} else if !strings.HasPrefix(path, "/") {
		return "", errors.New("مسار بيانات الامتحان غير كامل")
	}
	query := url.Values{"mode": {"ro"}, "_pragma": {"busy_timeout=15000", "synchronous=FULL"}}
	return (&url.URL{Scheme: "file", Path: path, RawQuery: query.Encode()}).String(), nil
}
