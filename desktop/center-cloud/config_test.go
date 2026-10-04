package main

import (
	"encoding/hex"
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestInvalidProvisioningFailsClosedBeforeServiceStartup(t *testing.T) {
	for _, test := range []struct{ name, key, value string }{
		{"missing devices", "MASSAR_SUPPORT_DEVICES_JSON", ""},
		{"missing admin", "MASSAR_SUPPORT_ADMIN_TOKEN_SHA256", ""},
		{"zero admin hash", "MASSAR_SUPPORT_ADMIN_TOKEN_SHA256", strings.Repeat("0", 64)},
		{"external listener", "MASSAR_SUPPORT_LISTEN", "0.0.0.0:43880"},
		{"relative storage", "MASSAR_SUPPORT_STORAGE_DIR", "relative-private"},
		{"invalid body bound", "MASSAR_SUPPORT_MAX_UPLOAD_BYTES", "0"},
		{"invalid center identifier", "MASSAR_SUPPORT_DEVICES_JSON", `{"../center":"HASH"}`},
		{"device shares admin token", "MASSAR_SUPPORT_DEVICES_JSON", `{"test-center":"ADMIN"}`},
		{"same bearer assigned different centers", "MASSAR_SUPPORT_DEVICES_JSON", `{"test-center":"HASH","other-center":"HASH"}`},
	} {
		t.Run(test.name, func(t *testing.T) {
			f := newFixture(t)
			deviceHash := f.config.devices["test-center"]
			deviceJSON, err := json.Marshal(map[string]string{"test-center": hex.EncodeToString(deviceHash[:])})
			if err != nil {
				t.Fatal(err)
			}
			t.Setenv("MASSAR_SUPPORT_LISTEN", "127.0.0.1:43880")
			t.Setenv("MASSAR_SUPPORT_STORAGE_DIR", f.config.storageDir)
			t.Setenv("MASSAR_SUPPORT_UPDATES_DIR", f.config.updatesDir)
			t.Setenv("MASSAR_SUPPORT_DEVICES_JSON", string(deviceJSON))
			t.Setenv("MASSAR_SUPPORT_ADMIN_TOKEN_SHA256", hex.EncodeToString(f.config.adminHash[:]))
			t.Setenv("MASSAR_SUPPORT_MAX_UPLOAD_BYTES", "134217728")
			if _, err := readConfig(); err != nil {
				t.Fatal("valid synthetic provisioning rejected")
			}
			value := strings.ReplaceAll(test.value, "HASH", hex.EncodeToString(deviceHash[:]))
			value = strings.ReplaceAll(value, "ADMIN", hex.EncodeToString(f.config.adminHash[:]))
			t.Setenv(test.key, value)
			if _, err := readConfig(); err == nil {
				t.Fatal("invalid provisioning activated private service")
			}
		})
	}
}

func TestReleaseAndPrivateStorageCannotOverlapEvenThroughDirectoryAliases(t *testing.T) {
	for _, alias := range []bool{false, true} {
		name := "nested"
		if alias {
			name = "symlink alias"
		}
		t.Run(name, func(t *testing.T) {
			root := t.TempDir()
			storage := filepath.Join(root, "private")
			updates := filepath.Join(storage, "releases")
			if err := os.MkdirAll(updates, 0700); err != nil {
				t.Fatal(err)
			}
			if alias {
				link := filepath.Join(root, "public-alias")
				if err := os.Symlink(updates, link); err != nil {
					t.Skipf("symlinks unavailable: %v", err)
				}
				updates = link
			}
			if _, err := newService(config{storageDir: storage, updatesDir: updates}); err == nil {
				t.Fatal("private upload data could share release directory tree")
			}
		})
	}
}
