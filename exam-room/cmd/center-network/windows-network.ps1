$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Root = Join-Path $env:ProgramData 'Massar\ExamNetwork'
$ConfigPath = Join-Path $Root 'network.json'
$BackupPath = Join-Path $Root 'adapter-backup.json'
$TaskName = 'MassarExamDirectNetwork'
$FirewallGroup = 'Massar Exam Direct Network'
$SetupLock=Join-Path $Root 'setup.lock'
$LockOwned=$false
$RollbackConfirmed=$false

function Reply($Content) {
    if($Action -in @('status','list-adapters')) {
        $Version=WebView-Runtime
        $Content.webView2Installed=($null -ne $Version)
        $Content.webView2Version=$Version
    }
    $Serialized=$Content | ConvertTo-Json -Depth 8 -Compress
    if($Action -in @('configure','portal-configure','restore') -and (Test-Path -LiteralPath $Root)) {
        try { @{ at=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds(); result=$Content } | ConvertTo-Json -Depth 8 -Compress | Set-Content -LiteralPath (Join-Path $Root 'last-setup-result.json') -Encoding UTF8 } catch { }
    }
    Write-Output $Serialized
    exit 0
}
function WebView-Runtime {
    foreach($Path in @('HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'HKCU:\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')) {
        $Version=(Get-ItemProperty -LiteralPath $Path -Name pv -ErrorAction SilentlyContinue).pv
        if($Version -match '^\d+\.\d+\.\d+\.\d+$' -and [version]$Version -gt [version]'0.0.0.0') { return $Version }
    }
    return $null
}
function Adapter-Summary($Adapter) {
    $Addresses = @(Get-NetIPAddress -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | ForEach-Object { $_.IPAddress })
    return @{ id = ([string]$Adapter.InterfaceGuid).Trim('{}'); name = [string]$Adapter.Name; index = [int]$Adapter.ifIndex; status = [string]$Adapter.Status; physical = [bool]$Adapter.HardwareInterface; wireless = ($Adapter.NdisPhysicalMedium -eq 9); ipv4 = $Addresses }
}
function Selected-Adapter($Id) {
    $Adapters = @(Get-NetAdapter -IncludeHidden | Where-Object { ([string]$_.InterfaceGuid).Trim('{}') -eq $Id })
    if ($Adapters.Count -ne 1) { throw 'The selected adapter is not available.' }
    return $Adapters[0]
}
function Check-OwnedPaths {
    foreach($Path in @((Join-Path $env:ProgramData 'Massar'),$Root,$ConfigPath,$BackupPath,(Join-Path $Root 'capport'))) {
        if(Test-Path -LiteralPath $Path) {
            $Entry=Get-Item -LiteralPath $Path -Force
            if(($Entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'A network state path is a reparse point; setup was refused.' }
        }
    }
}
function Check-Deadline {
    if ($MaintenanceDeadline -lt [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()+120) { throw 'The exam maintenance reservation expired; retry from the application.' }
}
function Take-SetupLock {
    Check-Deadline
    if (Test-Path -LiteralPath $SetupLock) { throw 'A prior network setup needs recovery; inspect the saved network before opening exams.' }
    New-Item -ItemType Directory -Path $SetupLock -ErrorAction Stop | Out-Null
    $script:LockOwned=$true
}
function Read-Configuration {
    if (-not (Test-Path -LiteralPath $ConfigPath)) { return $null }
    return Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
}
function Restore-Adapter($Backup) {
    $Adapter = Selected-Adapter $Backup.adapterId
    $Unexpected = @(Get-NetIPAddress -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.PrefixOrigin -eq 'Manual' -and $_.IPAddress -ne '10.77.0.1' })
    if ($Unexpected.Count -gt 0) { throw 'The adapter has new manual addresses. Review them before restoring; no address was removed.' }
    Get-NetIPAddress -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -eq '10.77.0.1' -and $_.PrefixLength -eq 22 } | Remove-NetIPAddress -Confirm:$false
    if ($Backup.dhcp -notin @('Enabled','Disabled')) { throw 'The saved DHCP setting is invalid; restoration was refused.' }
    Set-NetIPInterface -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -Dhcp $Backup.dhcp
    if ($null -ne $Backup.networkCategory) { Get-NetConnectionProfile -InterfaceIndex $Adapter.ifIndex -ErrorAction SilentlyContinue | Set-NetConnectionProfile -NetworkCategory $Backup.networkCategory }
}
function Stop-Owned-Service {
    $Task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($null -ne $Task) { Stop-ScheduledTask -TaskName $TaskName -ErrorAction Stop; Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false }
    $Expected = @(Join-Path $Root 'massar-exam-network.exe'; Join-Path $Root 'massar-exam-portal-proxy.exe')
    foreach ($Process in Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -in $Expected }) {
        Stop-Process -Id $Process.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Start-ConfiguredNetwork($Adapter,$Config) {
    Get-NetFirewallRule -Group $FirewallGroup -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    $Common = @{ Group=$FirewallGroup; Direction='Inbound'; Action='Allow'; Profile='Private'; InterfaceAlias=$Adapter.Name; Enabled='True' }
    New-NetFirewallRule @Common -DisplayName 'Massar exam DHCP' -Protocol UDP -LocalPort 67 -Program (Join-Path $Root 'massar-exam-network.exe') -RemoteAddress Any | Out-Null
    foreach ($Protocol in @('UDP','TCP')) { New-NetFirewallRule @Common -DisplayName "Massar exam DNS $Protocol" -Protocol $Protocol -LocalAddress '10.77.0.1' -LocalPort 53 -RemoteAddress '10.77.0.0/22' -Program (Join-Path $Root 'massar-exam-network.exe') | Out-Null }
    New-NetFirewallRule @Common -DisplayName 'Massar exam portal' -Protocol TCP -LocalAddress '10.77.0.1' -LocalPort 80 -RemoteAddress '10.77.0.0/22' -Program (Join-Path $Root 'massar-exam-portal-proxy.exe') | Out-Null
    if ($Config.portalMode -eq 'https') { New-NetFirewallRule @Common -DisplayName 'Massar exam HTTPS portal' -Protocol TCP -LocalAddress '10.77.0.1' -LocalPort 443 -RemoteAddress '10.77.0.0/22' -Program (Join-Path $Root 'massar-exam-portal-proxy.exe') | Out-Null }
    $TaskAction = New-ScheduledTaskAction -Execute (Join-Path $Root 'massar-exam-network.exe') -Argument 'run' -WorkingDirectory $Root
    $Trigger = New-ScheduledTaskTrigger -AtStartup
    $Settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $TaskName -Action $TaskAction -Trigger $Trigger -Settings $Settings -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
    Remove-Item -LiteralPath (Join-Path $Root 'heartbeat.json') -ErrorAction SilentlyContinue
    Start-ScheduledTask -TaskName $TaskName
}

try {
    if ($Action -eq 'list-adapters') {
        $Adapters = @(Get-NetAdapter -Physical | ForEach-Object { Adapter-Summary $_ })
        Reply @{ ok = $true; supported = $true; adapters = $Adapters }
    }
    if ($Action -eq 'status') {
        $Config = Read-Configuration
        if ($null -eq $Config) { Reply @{ ok=$true; supported=$true; configured=$false; ready=$false; supportedModes=@('legacy','https'); message='اختَر كابل Omada ثم جهّز شبكة الامتحان.' } }
        $Adapter = Selected-Adapter $Config.adapterId
        $Addresses = @(Get-NetIPAddress -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -eq '10.77.0.1' -and $_.PrefixLength -eq 22 })
        Reply @{ ok=$true; supported=$true; configured=$true; ready=$false; addressReady=($Adapter.Status -eq 'Up' -and $Addresses.Count -eq 1); adapterId=$Config.adapterId; adapter=(Adapter-Summary $Adapter); examAddress='10.77.0.1'; studentUrl='http://10.77.0.1/'; portalMode=$Config.portalMode; hostname=$Config.hostname; supportedModes=@('legacy','https'); requiresElevation=$false; message='إعداد شبكة الامتحان محفوظ؛ افحص الوصول من موبايل قبل فتح القاعة.' }
    }
    Check-OwnedPaths
    $Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not ([Security.Principal.WindowsPrincipal]$Identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Windows administrator authorization is required.' }
    if ($Action -eq 'restore') {
        if (-not (Test-Path -LiteralPath $BackupPath)) { throw 'No owned network backup exists.' }
        $Backup = Get-Content -LiteralPath $BackupPath -Raw | ConvertFrom-Json
        $Adapter = Selected-Adapter $Backup.adapterId
        # Validate drift before stopping a network that may have been changed manually.
        $Drift = @(Get-NetIPAddress -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.PrefixOrigin -eq 'Manual' -and ($_.IPAddress -ne '10.77.0.1' -or $_.PrefixLength -ne 22) })
        if ($Drift.Count -gt 0) { throw 'The selected adapter was changed after setup; review it before restoration.' }
        Take-SetupLock
        Stop-Owned-Service
        Restore-Adapter $Backup
        Get-NetFirewallRule -Group $FirewallGroup -ErrorAction SilentlyContinue | Remove-NetFirewallRule
        Remove-Item -LiteralPath $ConfigPath -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $BackupPath -ErrorAction SilentlyContinue
        $RollbackConfirmed=$true
        Reply @{ ok=$true; configured=$false; ready=$false; message='تم إرجاع إعداد كابل الامتحان السابق.' }
    }
    if ($Action -ne 'configure' -and $Action -ne 'portal-configure') { throw 'Unknown network action.' }
    $Adapter = Selected-Adapter $AdapterId
    if (-not $Adapter.HardwareInterface -or $Adapter.NdisPhysicalMedium -eq 9 -or $Adapter.Status -ne 'Up' -or $Adapter.InterfaceType -ne 6) { throw 'Choose the active physical Ethernet cable connected to Omada.' }
    $Existing = Read-Configuration
    if ($null -ne $Existing -and $Existing.adapterId -ne $AdapterId) { throw 'Restore the previously configured adapter before selecting another one.' }
    $Defaults = @(Get-NetRoute -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue)
    if ($Defaults.Count -gt 0) { throw 'This cable supplies a default route. Choose the dedicated Omada cable.' }
    $Conflicts = @(Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceIndex -ne $Adapter.ifIndex -and $_.IPAddress -match '^10\.77\.[0-3]\.' })
    if ($Conflicts.Count -gt 0) { throw 'The exam subnet is already used by another adapter.' }
    if ($PortalMode -eq '') { $PortalMode = if ($null -ne $Existing) { $Existing.portalMode } else { 'legacy' } }
    if ($PortalMode -eq 'https' -and $CertificatePath -eq '') { $CertificatePath=Join-Path $Root 'capport\fullchain.pem'; $PrivateKeyPath=Join-Path $Root 'capport\privkey.pem'; $Hostname=$Existing.hostname }
    $Manual = @(Get-NetIPAddress -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.PrefixOrigin -eq 'Manual' })
    if ($null -eq $Existing -and $Manual.Count -gt 0) { throw 'The selected cable has existing manual settings; they were not changed.' }
    if (@($Manual | Where-Object { $_.IPAddress -ne '10.77.0.1' -or $_.PrefixLength -ne 22 }).Count -gt 0) { throw 'The selected cable settings have changed; review before setup.' }
    foreach ($Name in @('massar-exam-network.exe','massar-exam-portal-proxy.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $SourceDirectory $Name) -PathType Leaf)) { throw "Required bundled executable is missing: $Name" }
    }
    if ($PortalMode -eq 'https') {
        if ($Hostname.Length -gt 200 -or $Hostname -notmatch '^[a-zA-Z0-9][a-zA-Z0-9.-]*\.[a-zA-Z0-9]+$') { throw 'A valid DNS hostname is required.' }
        foreach ($Path in @($CertificatePath,$PrivateKeyPath)) { if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-Item -LiteralPath $Path).Length -gt 131072) { throw 'Certificate files are missing or too large.' } }
        $Metadata = & (Join-Path $SourceDirectory 'massar-exam-portal-proxy.exe') --check-capport --capport-hostname $Hostname --capport-cert $CertificatePath --capport-key $PrivateKeyPath
        if ($LASTEXITCODE -ne 0) { throw 'The certificate/key, hostname, trust or expiry check failed. Existing network settings were not changed.' }
    }
    # Reject foreign DHCP/DNS/HTTP services before mutating addresses or stopping ours.
    $Owned = @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -in @((Join-Path $Root 'massar-exam-network.exe'),(Join-Path $Root 'massar-exam-portal-proxy.exe')) } | ForEach-Object { $_.ProcessId })
    $Udp = @(Get-NetUDPEndpoint -ErrorAction Stop | Where-Object { $_.LocalPort -in @(53,67) -and $_.OwningProcess -notin $Owned })
    $Tcp = @(Get-NetTCPConnection -State Listen -ErrorAction Stop | Where-Object { $_.LocalPort -in $(if ($PortalMode -eq 'https') { @(53,80,443) } else { @(53,80) }) -and $_.OwningProcess -notin $Owned })
    if ($Udp.Count -gt 0 -or $Tcp.Count -gt 0) { throw 'A foreign DHCP, DNS or HTTP service is using the required ports. No network settings were changed.' }
    Check-Deadline
    New-Item -ItemType Directory -Path $Root -Force | Out-Null
    & icacls.exe $Root /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not protect the network service directory.' }
    if (-not (Test-Path -LiteralPath $BackupPath)) {
        $Category = Get-NetConnectionProfile -InterfaceIndex $Adapter.ifIndex -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty NetworkCategory
        $PriorDhcp=[string](Get-NetIPInterface -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -ErrorAction Stop).Dhcp
        if($PriorDhcp -notin @('Enabled','Disabled')) { throw 'Could not read the original DHCP setting.' }
        @{ adapterId=$AdapterId; dhcp=$PriorDhcp; networkCategory=$Category } | ConvertTo-Json | Set-Content -LiteralPath $BackupPath -Encoding UTF8
    }
    $Backup = Get-Content -LiteralPath $BackupPath -Raw | ConvertFrom-Json
    $Previous = Join-Path $Root ('rollback-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $Previous | Out-Null
    & icacls.exe $Previous /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not protect the rollback snapshot.' }
    if ($null -ne $Existing) {
        foreach ($Name in @('network.json','massar-exam-network.exe','massar-exam-portal-proxy.exe','capport')) { $Path=Join-Path $Root $Name; if(Test-Path -LiteralPath $Path) { Copy-Item -LiteralPath $Path -Destination $Previous -Recurse -Force } }
    }
    # Read approved material before replacing a previous installation that may contain it.
    $CertificateBytes=$null; $PrivateKeyBytes=$null
    if ($PortalMode -eq 'https') { $CertificateBytes=[IO.File]::ReadAllBytes($CertificatePath); $PrivateKeyBytes=[IO.File]::ReadAllBytes($PrivateKeyPath) }
    Take-SetupLock
    try {
        Stop-Owned-Service
        foreach ($Name in @('massar-exam-network.exe','massar-exam-portal-proxy.exe')) { Copy-Item -LiteralPath (Join-Path $SourceDirectory $Name) -Destination (Join-Path $Root $Name) -Force }
        Set-NetIPInterface -InterfaceIndex $Adapter.ifIndex -AddressFamily IPv4 -Dhcp Disabled
        if ($Manual.Count -eq 0) { New-NetIPAddress -InterfaceIndex $Adapter.ifIndex -IPAddress '10.77.0.1' -PrefixLength 22 -AddressFamily IPv4 | Out-Null }
        Get-NetConnectionProfile -InterfaceIndex $Adapter.ifIndex -ErrorAction SilentlyContinue | Set-NetConnectionProfile -NetworkCategory Private
        if ($PortalMode -eq 'https') {
            $CertificateDirectory=Join-Path $Root 'capport'
            New-Item -ItemType Directory -Path $CertificateDirectory -Force | Out-Null
            & icacls.exe $CertificateDirectory /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Could not protect the portal private key.' }
            [IO.File]::WriteAllBytes((Join-Path $CertificateDirectory 'fullchain.pem'),$CertificateBytes)
            [IO.File]::WriteAllBytes((Join-Path $CertificateDirectory 'privkey.pem'),$PrivateKeyBytes)
        }
        $Config=@{ schemaVersion=1; adapterId=$AdapterId; adapterIndex=[int]$Adapter.ifIndex; apMac='20e15dd36f52'; portalMode=$PortalMode; hostname=$(if($PortalMode -eq 'https'){$Hostname}else{''}) }
        $Config | ConvertTo-Json | Set-Content -LiteralPath $ConfigPath -Encoding UTF8
        Start-ConfiguredNetwork $Adapter $Config
        $Ready = $false
        for ($Attempt=0; $Attempt -lt 15; $Attempt++) {
            Start-Sleep -Seconds 1
            $Heartbeat = Join-Path $Root 'heartbeat.json'
            if (Test-Path -LiteralPath $Heartbeat) { $State = Get-Content -LiteralPath $Heartbeat -Raw | ConvertFrom-Json; if ($State.adapterId -eq $AdapterId -and $State.at -ge [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()-5 -and $State.dhcp) { $Ready=$true; break } }
        }
        if (-not $Ready) { throw 'The DHCP service did not confirm readiness.' }
        Remove-Item -LiteralPath $Previous -Recurse -Force
        $RollbackConfirmed=$true
        Reply @{ ok=$true; configured=$true; adapterId=$AdapterId; examAddress='10.77.0.1'; studentUrl='http://10.77.0.1/'; portalMode=$PortalMode; message='تم تجهيز كابل Omada؛ اختبر الدخول من الموبايل قبل فتح القاعة.' }
    }
    catch {
        $Failure = $_.Exception.Message
        try {
            Stop-Owned-Service
            if ($null -ne $Existing) {
                foreach($Name in @('network.json','massar-exam-network.exe','massar-exam-portal-proxy.exe','capport')) {
                    $Saved=Join-Path $Previous $Name
                    if(Test-Path -LiteralPath $Saved){ Copy-Item -LiteralPath $Saved -Destination $Root -Recurse -Force }
                }
                Start-ConfiguredNetwork $Adapter $Existing
            } else {
                Restore-Adapter $Backup
                Get-NetFirewallRule -Group $FirewallGroup -ErrorAction SilentlyContinue | Remove-NetFirewallRule
                Remove-Item -LiteralPath $ConfigPath -ErrorAction SilentlyContinue
                Remove-Item -LiteralPath $BackupPath -ErrorAction SilentlyContinue
            }
            Remove-Item -LiteralPath $Previous -Recurse -Force
            $RollbackConfirmed=$true
        }
        catch { throw "Setup failed and rollback needs review: $Failure" }
        throw "Setup failed; previous owned network configuration was restored: $Failure"
    }
}
catch { Reply @{ ok=$false; ready=$false; message=$_.Exception.Message } }

finally { if ($LockOwned -and $RollbackConfirmed) { Remove-Item -LiteralPath $SetupLock -Force -ErrorAction Stop } }
