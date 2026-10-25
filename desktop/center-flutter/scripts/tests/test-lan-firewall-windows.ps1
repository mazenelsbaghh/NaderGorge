param([Parameter(Mandatory)][string]$GatewayExecutable)
$ErrorActionPreference = 'Stop'
$Scripts = Split-Path $PSScriptRoot -Parent
$Fixture = Join-Path ([IO.Path]::GetTempPath()) ('massar-firewall-test-' + [Guid]::NewGuid().ToString('N'))
$Program = Join-Path $Fixture 'massar-lan-host.exe'
$RuleNames = @()

function Get-FixtureRules {
    @(Get-NetFirewallApplicationFilter -PolicyStore PersistentStore |
        Where-Object { $_.Program -eq $Program } | Get-NetFirewallRule)
}

try {
    New-Item -ItemType Directory -Path $Fixture | Out-Null
    Copy-Item -LiteralPath $GatewayExecutable -Destination $Program
    Copy-Item -LiteralPath (Join-Path $Scripts 'repair-lan-firewall.ps1') -Destination $Fixture
    $Manifest = Join-Path $Fixture 'build-manifest.json'
    $Repair = Join-Path $Fixture 'repair-lan-firewall.ps1'
    '{"role":"client"}' | Set-Content -LiteralPath $Manifest
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Repair
    if ($LASTEXITCODE -eq 0 -or @(Get-FixtureRules).Count -ne 0) {
        throw 'Secondary package must not create host firewall rules.'
    }
    '{"role":"host"}' | Set-Content -LiteralPath $Manifest
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Repair -CheckOnly
    if ($LASTEXITCODE -ne 15 -or @(Get-FixtureRules).Count -ne 0) {
        throw 'Read-only preflight must detect missing folder permissions without creating rules.'
    }
    # The reported incident had no running gateway: permissions must work before startup.
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Repair
    if ($LASTEXITCODE -ne 0) { throw 'Host folder firewall repair failed.' }
    $Rules = @(Get-FixtureRules)
    $RuleNames = @($Rules.Name)
    if ($Rules.Count -ne 2) { throw 'Expected exactly two scoped host allowances.' }
    $ActualPorts = @()
    foreach ($Rule in $Rules) {
        $Port = $Rule | Get-NetFirewallPortFilter
        $Address = $Rule | Get-NetFirewallAddressFilter
        if ($Rule.Action -ne 'Allow' -or $Rule.Enabled -ne 'True' -or
            $Rule.Direction -ne 'Inbound' -or $Rule.Profile -ne 'Any' -or
            [string]$Address.RemoteAddress -ne 'LocalSubnet') {
            throw 'Host firewall allowance is not restricted to local subnets.'
        }
        $ActualPorts += ([string]$Port.Protocol + ':' + [string]$Port.LocalPort)
    }
    if (@(Compare-Object @('TCP:43873', 'UDP:43874') $ActualPorts).Count -ne 0) {
        throw 'Unexpected host firewall ports.'
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Repair -CheckOnly
    if ($LASTEXITCODE -ne 0 -or @(Compare-Object $RuleNames @((Get-FixtureRules).Name)).Count -ne 0) {
        throw 'Read-only preflight must accept healthy permissions without modifying rules.'
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Repair
    if ($LASTEXITCODE -ne 0 -or @(Compare-Object $RuleNames @((Get-FixtureRules).Name)).Count -ne 0) {
        throw 'Repeating the repair must retain the same two rules.'
    }
    $Block = 'MassarFirewallTest-' + [Guid]::NewGuid().ToString('N')
    New-NetFirewallRule -Name $Block -DisplayName $Block -Program $Program -Direction Inbound -Action Block -Enabled True | Out-Null
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Repair -CheckOnly
    if ($LASTEXITCODE -ne 12 -or (Get-NetFirewallRule -Name $Block).Action -ne 'Block') {
        throw 'Preflight must preserve and report explicit administrator blocks.'
    }
    Write-Host 'Windows firewall checks passed: host-only, read-only preflight, stopped gateway, local subnet scope, repeat repair, explicit block preserved.'
} finally {
    # This unique temporary executable path belongs only to this test invocation.
    Get-FixtureRules | Remove-NetFirewallRule
    if (Test-Path -LiteralPath $Fixture) { Remove-Item -LiteralPath $Fixture -Recurse -Force }
}
