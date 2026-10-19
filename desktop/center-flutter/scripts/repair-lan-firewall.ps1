#Requires -Version 5.1
#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Program = Join-Path $PSScriptRoot 'massar-lan-host.exe'
$Group = 'Massar Center LAN folder repair'
$CreatedRules = [Collections.Generic.List[string]]::new()

function Assert-HostPackage {
    if (-not (Test-Path -LiteralPath $Program -PathType Leaf)) {
        throw 'PLACE_FILES_IN_NEW_HOST_FOLDER'
    }
    $ManifestPath = Join-Path $PSScriptRoot 'build-manifest.json'
    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw 'HOST_BUILD_MANIFEST_NOT_FOUND'
    }
    $Manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    if ($Manifest.role -ne 'host') { throw 'USE_ON_HOST_NOT_SECONDARY' }
}

function Add-LanAllowance([string]$Protocol, [int]$Port, [string]$Name) {
    $Existing = @(Get-NetFirewallRule -PolicyStore PersistentStore |
        Where-Object { $_.Name -eq $Name })
    if ($Existing.Count -gt 0) {
        $Filters = @($Existing | Get-NetFirewallApplicationFilter)
        if ($Existing.Count -ne 1 -or $Existing[0].Group -ne $Group -or
            $Filters.Count -ne 1 -or $Filters[0].Program -ne $Program) {
            throw 'EXISTING_RULE_IDENTITY_MISMATCH'
        }
        # Do not broaden a rule that an administrator changed after a prior repair.
        $Ports = @($Existing | Get-NetFirewallPortFilter)
        $Addresses = @($Existing | Get-NetFirewallAddressFilter)
        if ($Existing[0].Enabled -ne 'True' -or $Existing[0].Action -ne 'Allow' -or
            $Existing[0].Direction -ne 'Inbound' -or $Existing[0].Profile -ne 'Any' -or
            $Ports.Count -ne 1 -or [string]$Ports[0].LocalPort -ne [string]$Port -or
            $Ports[0].Protocol -ne $Protocol -or $Addresses.Count -ne 1 -or
            [string]$Addresses[0].RemoteAddress -ne 'LocalSubnet') {
            throw 'EXISTING_REPAIR_RULE_CHANGED'
        }
        return
    }
    New-NetFirewallRule -PolicyStore PersistentStore -Name $Name -DisplayName "Massar LAN $Protocol $Port" `
        -Group $Group -Direction Inbound -Action Allow -Enabled True -Profile Any `
        -Program $Program -Protocol $Protocol -LocalPort $Port -RemoteAddress LocalSubnet `
        -EdgeTraversalPolicy Block | Out-Null
    $CreatedRules.Add($Name)
}

try {
    # Firewall permissions belong to this package path even before its server starts.
    Assert-HostPackage
    $Filters = @(Get-NetFirewallApplicationFilter -PolicyStore ActiveStore |
        Where-Object { $_.Program -eq $Program })
    $Blocks = @()
    if ($Filters.Count -gt 0) {
        $Blocks = @($Filters | Get-NetFirewallRule |
            Where-Object { $_.Direction -eq 'Inbound' -and $_.Action -eq 'Block' -and $_.Enabled -eq 'True' })
    }
    if ($Blocks.Count -gt 0) {
        Write-Host 'Windows has an explicit BLOCK for this new host application.'
        Write-Host 'Allow massar-lan-host.exe from this folder in Windows Firewall, then retry.'
        throw 'EXPLICIT_HOST_BLOCK_REQUIRES_REVIEW'
    }
    $Hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $Digest = $Hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($Program.ToLowerInvariant()))
        $PathId = ([BitConverter]::ToString($Digest)).Replace('-', '').Substring(0, 16)
    } finally { $Hasher.Dispose() }
    Add-LanAllowance 'TCP' 43873 "MassarCenterLAN-$PathId-TCP"
    Add-LanAllowance 'UDP' 43874 "MassarCenterLAN-$PathId-UDP"
    Write-Host 'LAN allowances applied to THIS host folder for local subnets only.'
    Write-Host 'Close older Massar copies. Open massar_center.exe from THIS folder.'
    Write-Host 'Start the host server in Link devices, then retry from the secondary.'
    Write-Host 'Remote connection still needs to be confirmed from the secondary.'
    Write-Host ('Rules added: ' + $CreatedRules.Count)
    exit 0
} catch {
    $Failure = $_.Exception
    $RollbackFailed = $false
    foreach ($RuleName in $CreatedRules) {
        try { Remove-NetFirewallRule -PolicyStore PersistentStore -Name $RuleName }
        catch { $RollbackFailed = $true }
    }
    $Reason = $Failure.Message
    $KnownReasons = @('PLACE_FILES_IN_NEW_HOST_FOLDER', 'HOST_BUILD_MANIFEST_NOT_FOUND',
        'USE_ON_HOST_NOT_SECONDARY',
        'EXISTING_RULE_IDENTITY_MISMATCH', 'EXISTING_REPAIR_RULE_CHANGED',
        'EXPLICIT_HOST_BLOCK_REQUIRES_REVIEW')
    if ($Reason -in $KnownReasons) { Write-Host ('Reason: ' + $Reason) }
    else { Write-Host ('Windows operation failed. Error code: ' + $Failure.HResult) }
    if ($RollbackFailed) { Write-Host 'ROLLBACK_INCOMPLETE: review the Massar Center LAN folder repair firewall group.' }
    exit 1
}
