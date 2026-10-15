//go:build windows

package main

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"golang.org/x/sys/windows"
)

func TestSetupLockLifecycleThroughPowerShellStdinLoader(t *testing.T) {
	// Regression: successful setup on 2026-10-13 left a lock that blocked every room.
	// Use the embedded installer's AST and real temporary directories, without running network commands.
	parts := setupLockLifecycleParts(t)
	for _, scenario := range []struct {
		name              string
		priorLock         bool
		rollbackConfirmed bool
		replyOK           bool
		wantLock          bool
	}{
		{name: "successful_setup_removes_owned_lock", rollbackConfirmed: true, replyOK: true},
		{name: "confirmed_rollback_removes_owned_lock", rollbackConfirmed: true},
		{name: "unconfirmed_rollback_preserves_owned_lock", wantLock: true},
		{name: "existing_lock_is_preserved", priorLock: true, rollbackConfirmed: true, wantLock: true},
	} {
		t.Run(scenario.name, func(t *testing.T) {
			root := t.TempDir()
			lock := filepath.Join(root, "setup.lock")
			if scenario.priorLock {
				if err := os.Mkdir(lock, 0700); err != nil {
					t.Fatal(err)
				}
			}
			script := fmt.Sprintf("$Action='configure'\n$Root=%s\n$SetupLock=%s\n$MaintenanceDeadline=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+600\n%s\n%s\n$Acquired=$false\ntry { Take-SetupLock; $Acquired=$true; $RollbackConfirmed=$%t; Reply @{ok=$%t; acquired=$Acquired} } catch { Reply @{ok=$false; acquired=$Acquired; message=$_.Exception.Message} } finally %s",
				psLiteral(root), psLiteral(lock), parts.Initialization, parts.Functions, scenario.rollbackConfirmed, scenario.replyOK, parts.Finally)
			content, err := powershell(script)
			if err != nil {
				t.Fatalf("temporary setup lifecycle failed: %v; output: %s", err, content)
			}
			var reply struct{ OK, Acquired bool }
			if err := json.Unmarshal(content, &reply); err != nil || reply.OK != scenario.replyOK || reply.Acquired == scenario.priorLock {
				t.Fatalf("unexpected setup reply: %s; decode error: %v", content, err)
			}
			_, err = os.Stat(lock)
			if scenario.wantLock && err != nil || !scenario.wantLock && !os.IsNotExist(err) {
				t.Fatalf("setup lock exists=%v, want=%v; stat error=%v", err == nil, scenario.wantLock, err)
			}
		})
	}
}

type setupLockScriptParts struct {
	Initialization string
	Functions      string
	Finally        string
}

func setupLockLifecycleParts(t *testing.T) setupLockScriptParts {
	t.Helper()
	content, err := powershell("$Source=" + psLiteral(networkScript) + `
$Tokens=$null; $ParseErrors=$null
$Ast=[System.Management.Automation.Language.Parser]::ParseInput($Source,[ref]$Tokens,[ref]$ParseErrors)
if($ParseErrors.Count -ne 0){throw 'The embedded network script did not parse.'}
$Initialization=@($Ast.EndBlock.Statements | Where-Object {
    $_ -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $_.Left -is [System.Management.Automation.Language.VariableExpressionAst] -and
    $_.Left.VariablePath.UserPath -in @('LockOwned','script:LockOwned','RollbackConfirmed')
})
$Functions=@($Ast.EndBlock.Statements | Where-Object {
    $_ -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
    $_.Name -in @('Reply','Check-Deadline','Take-SetupLock')
})
$Installer=@($Ast.EndBlock.Statements | Where-Object {
    $_ -is [System.Management.Automation.Language.TryStatementAst] -and $null -ne $_.Finally
})
if($Initialization.Count -ne 2 -or $Functions.Count -ne 3 -or $Installer.Count -ne 1){throw 'The installer lock lifecycle could not be located.'}
@{
    Initialization=($Initialization | ForEach-Object {$_.Extent.Text}) -join [Environment]::NewLine
    Functions=($Functions | ForEach-Object {$_.Extent.Text}) -join [Environment]::NewLine
    Finally=$Installer[0].Finally.Extent.Text
} | ConvertTo-Json -Compress
`)
	if err != nil {
		t.Fatalf("extract installer lifecycle: %v; output: %s", err, content)
	}
	var parts setupLockScriptParts
	if err := json.Unmarshal(content, &parts); err != nil {
		t.Fatalf("decode installer lifecycle: %v; output: %s", err, content)
	}
	return parts
}

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
