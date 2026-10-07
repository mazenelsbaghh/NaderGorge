package main

import (
	"strings"
	"testing"
)

func TestUpstreamCannotEscapeLoopbackOrigin(t *testing.T) {
	for _, address := range []string{"https://127.0.0.1:1234", "http://192.168.1.2:1234", "http://localhost:1234", "http://127.0.0.1", "http://127.0.0.1:0", "http://127.0.0.1:65536", "http://user:password@127.0.0.1:1234", "http://127.0.0.1:1234/api", "http://127.0.0.1:1234?secret=1", "http://127.0.0.1:1234#fragment"} {
		t.Run(address, func(t *testing.T) {
			if _, err := (Config{Upstream: address}).upstreamURL(); err == nil {
				t.Fatal("unsafe upstream accepted")
			}
		})
	}
	for _, address := range []string{"http://127.0.0.1:1234", "http://[::1]:1234"} {
		if _, err := (Config{Upstream: address}).upstreamURL(); err != nil {
			t.Fatal(err)
		}
	}
}

func TestStdinConfigRejectsUnknownOrMalformedConfiguration(t *testing.T) {
	for _, source := range []string{`{"argvSecret":"not-supported"}`, `{`, strings.Repeat(" ", 65537) + `{}`} {
		if _, err := readConfig(strings.NewReader(source)); err == nil {
			t.Fatal("invalid config accepted")
		}
	}
}
