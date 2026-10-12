//go:build windows

package main

import (
	"encoding/json"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"golang.org/x/sys/windows"
)

func TestPowerShellEnvironmentRemovesInheritedModulePaths(t *testing.T) {
	executable := `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`
	inherited := []string{"PATH=unchanged", "PSModulePath=PowerShell7", "psmodulepath=duplicate", "USERPROFILE=Arabic profile", "=C:=C:\\"}
	want := []string{"PATH=unchanged", "USERPROFILE=Arabic profile", "=C:=C:\\", "PSModulePath=" + filepath.Join(filepath.Dir(executable), "Modules")}
	if got := powershellEnvironment(inherited, executable); !reflect.DeepEqual(got, want) {
		t.Fatalf("environment isolation changed unrelated entries or retained inherited module paths: %v", got)
	}
}

func TestPowerShellLoadsBuiltInModulesWithPoisonedParentPath(t *testing.T) {
	t.Setenv("PSModulePath", filepath.Join(t.TempDir(), "incompatible-parent-modules"))
	content, err := powershell(`@{ major=$PSVersionTable.PSVersion.Major; edition=$PSVersionTable.PSEdition; security=(Get-Command Get-Acl -ErrorAction Stop).ModuleName; network=(Get-Command Get-NetAdapter -ErrorAction Stop).ModuleName; modulePath=$env:PSModulePath } | ConvertTo-Json -Compress`)
	if err != nil {
		t.Fatalf("built-in PowerShell module discovery failed: %v", err)
	}
	var actual struct {
		Major      int    `json:"major"`
		Edition    string `json:"edition"`
		Security   string `json:"security"`
		Network    string `json:"network"`
		ModulePath string `json:"modulePath"`
	}
	if err := json.Unmarshal(content, &actual); err != nil {
		t.Fatalf("PowerShell module discovery returned invalid JSON: %v", err)
	}
	systemDirectory, err := windows.GetSystemDirectory()
	if err != nil {
		t.Fatal(err)
	}
	if actual.Major != 5 || actual.Edition != "Desktop" || actual.Security != "Microsoft.PowerShell.Security" || actual.Network != "NetAdapter" || !strings.EqualFold(actual.ModulePath, filepath.Join(systemDirectory, "WindowsPowerShell", "v1.0", "Modules")) {
		t.Fatalf("unexpected PowerShell runtime or built-in module discovery: %+v", actual)
	}
}
