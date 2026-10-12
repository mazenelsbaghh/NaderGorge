package main

import (
	"encoding/json"
	"os"
	"testing"
)

// TestMain supplies only the synthetic external-renderer fixture. No production
// engine path uses this flag, and ordinary tests keep their usual entry point.
func TestMain(tests *testing.M) {
	if os.Getenv("MASSAR_TEST_PDF_RENDERER") == "1" {
		if len(os.Args) != 3 {
			os.Exit(2)
		}
		content, err := os.ReadFile(os.Args[1])
		var report struct {
			Attempt struct {
				Name string `json:"name"`
			} `json:"attempt"`
		}
		if err != nil || json.Unmarshal(content, &report) != nil || report.Attempt.Name == "" {
			os.Exit(2)
		}
		if os.WriteFile(os.Args[2], []byte("%PDF-1.7\n"+report.Attempt.Name), 0600) != nil {
			os.Exit(2)
		}
		os.Exit(0)
	}
	os.Exit(tests.Run())
}
