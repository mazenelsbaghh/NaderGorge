param(
    [string]$OutputDirectory = "dist",
    [switch]$ClientOnly,
    [switch]$SkipTests,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')]
    [string]$OutputTag = "release"
)
$ErrorActionPreference = "Stop"
$AppRoot = Split-Path $PSScriptRoot -Parent

function Assert-RunnerIdentity {
    $ResourcePath = Join-Path $AppRoot "windows/runner/Runner.rc"
    $Resource = Get-Content -LiteralPath $ResourcePath -Raw
    foreach ($Entry in @(@("CompanyName", "com.massar"), @("ProductName", "Massar Center"))) {
        $Key = [regex]::Escape($Entry[0])
        $Value = [regex]::Escape($Entry[1])
        $Pattern = '(?m)^\s*VALUE\s+"' + $Key + '"\s*,\s*"' + $Value + '"\s*"\\0"\s*$'
        $Definitions = [regex]::Matches($Resource, '(?m)^\s*VALUE\s+"' + $Key + '"\s*,')
        if ($Definitions.Count -ne 1 -or -not [regex]::IsMatch($Resource, $Pattern)) {
            throw "Runner.rc $($Entry[0]) must remain $($Entry[1]); changing it changes the existing data profile."
        }
    }
}

function Copy-VisualCppRuntime([string]$Directory) {
    $VsWhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio/Installer/vswhere.exe"
    if (-not (Test-Path -LiteralPath $VsWhere)) { throw "Visual Studio locator is missing." }
    $Installation = & $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if ($LASTEXITCODE -ne 0 -or -not $Installation) { throw "Visual Studio C++ tools are missing." }
    $Runtime = Get-ChildItem (Join-Path $Installation "VC/Redist/MSVC") -Directory |
        Sort-Object { if ($_.Name -match '^\d+(\.\d+){1,3}$') { [version]$_.Name } else { [version]'0.0' } } -Descending |
        ForEach-Object { Join-Path $_.FullName "x64/Microsoft.VC143.CRT" } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
    if (-not $Runtime) { throw "Visual C++ x64 redistributable files are missing." }
    foreach ($Name in @("msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll")) {
        $Source = Join-Path $Runtime $Name
        if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) { throw "Missing Visual C++ runtime: $Name" }
        Copy-Item -LiteralPath $Source -Destination $Directory -Force
    }
}

function Assert-NoRuntimeData([string]$Directory) {
    $PrivateNames = @("identity.json", "devices.json", "lan-settings.json", "lan-pending-command.json", "last-opened-build.json", "center.sqlite.lock", "data-repair-status.json")
    foreach ($File in Get-ChildItem -LiteralPath $Directory -File -Recurse) {
        foreach ($PrivateName in $PrivateNames) {
            if ($File.Name -eq $PrivateName -or $File.Name.StartsWith($PrivateName + ".", [StringComparison]::OrdinalIgnoreCase)) {
                throw "Release contains private runtime metadata; refusing to package."
            }
        }
        if ($File.Name -match '(?i)\.(sqlite3?|db)(-wal|-shm|-journal)?$') {
            throw "Release contains a database; refusing to package."
        }
        $Stream = [System.IO.File]::OpenRead($File.FullName)
        try {
            $Header = New-Object byte[] 16
            $Read = $Stream.Read($Header, 0, 16)
            if ($Read -eq 16 -and [System.Text.Encoding]::ASCII.GetString($Header) -eq "SQLite format 3`0") {
                throw "Release contains a database; refusing to package."
            }
        }
        finally { $Stream.Dispose() }
        if ($File.Extension -eq ".json") {
            $Content = Get-Content -LiteralPath $File.FullName -Raw
            if ($Content -match '"format"\s*:\s*"massar-center-backup"') {
                throw "Release contains a private runtime backup; refusing to package."
            }
        }
    }
}

Push-Location $AppRoot
try {
    if (-not $IsWindows -and $env:OS -ne "Windows_NT") {
        throw "Build this package on Windows with Flutter 3.41.0 and Visual Studio C++ desktop tools."
    }
    Assert-RunnerIdentity
    $PackageMode = if ($ClientOnly) { "client" } else { "host" }
    $ClientDefine = if ($ClientOnly) { "true" } else { "false" }
    $SeedSource = Join-Path $AppRoot "assets/installation_seed.json"
    if (-not $ClientOnly -and -not (Test-Path -LiteralPath $SeedSource -PathType Leaf)) {
        throw "The host package requires assets/installation_seed.json."
    }
    & flutter pub get
    if ($LASTEXITCODE -ne 0) { throw "Dependency installation failed." }
    & flutter analyze
    if ($LASTEXITCODE -ne 0) { throw "Analysis failed." }
    if (-not $SkipTests) {
        & flutter test
        if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
    }
    $LanRoot = Join-Path (Split-Path $AppRoot -Parent) "center-lan"
    Push-Location $LanRoot
    try {
        if (-not $SkipTests) {
            & go test ./...
            if ($LASTEXITCODE -ne 0) { throw "LAN gateway tests failed." }
        }
    }
    finally { Pop-Location }
    $SourceFingerprints = Get-ChildItem (Join-Path $AppRoot "lib") -Filter *.dart -Recurse |
        Sort-Object FullName | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    $LanFingerprints = Get-ChildItem $LanRoot -Filter *.go -Recurse | Sort-Object FullName |
        ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
    $AssetFingerprints = Get-ChildItem (Join-Path $AppRoot "assets") -File -Recurse |
        Sort-Object FullName | ForEach-Object {
            $_.FullName.Substring($AppRoot.Length) + (Get-FileHash $_.FullName -Algorithm SHA256).Hash
        }
    $SourceFingerprintText = ($SourceFingerprints -join "") + ($LanFingerprints -join "") +
        ($AssetFingerprints -join "") + $PackageMode +
        (Get-FileHash (Join-Path $LanRoot "go.mod") -Algorithm SHA256).Hash +
        (Get-FileHash "pubspec.yaml" -Algorithm SHA256).Hash +
        (Get-FileHash "pubspec.lock" -Algorithm SHA256).Hash
    $Digest = [System.Security.Cryptography.SHA256]::Create()
    try {
        $DigestBytes = $Digest.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($SourceFingerprintText))
    }
    finally { $Digest.Dispose() }
    $BuildId = [BitConverter]::ToString($DigestBytes).Replace("-", "").ToLowerInvariant().Substring(0, 16)
    $VersionLines = [regex]::Matches((Get-Content -LiteralPath "pubspec.yaml" -Raw), '(?m)^version:\s*([^\s#]+)\s*(?:#.*)?$')
    if ($VersionLines.Count -ne 1) { throw "pubspec.yaml must declare one application version." }
    $AppVersion = $VersionLines[0].Groups[1].Value.Trim([char[]]@('"', "'"))
    if ($AppVersion -notmatch '^\d{1,6}\.\d{1,6}\.\d{1,6}(?:-[0-9A-Za-z][0-9A-Za-z.-]{0,31})?\+\d{1,10}$') {
        throw "Application version must include a bounded numeric build number."
    }
    & flutter build windows --release "--dart-define=MASSAR_BUILD_ID=$BuildId" "--dart-define=MASSAR_CLIENT_ONLY=$ClientDefine" "--dart-define=MASSAR_APP_VERSION=$AppVersion"
    if ($LASTEXITCODE -ne 0) { throw "Windows compilation failed." }
    $ReleaseDirectory = Join-Path $AppRoot "build/windows/x64/runner/Release"
    if (-not (Test-Path (Join-Path $ReleaseDirectory "massar_center.exe"))) {
        throw "Release executable was not found."
    }
    $Version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $ReleaseDirectory "massar_center.exe"))
    if ($Version.CompanyName -ne "com.massar" -or $Version.ProductName -ne "Massar Center") {
        throw "Compiled application identity differs from the existing Windows data profile."
    }
    $PackagedAssets = Join-Path $ReleaseDirectory "data/flutter_assets/assets"
    if ($ClientOnly) {
        foreach ($PrivateAsset in @("admin_account.json", "installation_seed.json", "data_repair_20261002.json", "data_repair_20261004.json", "academic_import_20261004.json", "cairo_academic_import_20261004.json", "cairo_codes_20261004.json", "gec_codes_20261004.json", "gec_duplicate_codes_20261004.json")) {
            $PackagedFile = Join-Path $PackagedAssets $PrivateAsset
            if (Test-Path -LiteralPath $PackagedFile) {
                Remove-Item -LiteralPath $PackagedFile -Force
            }
        }
    }
    elseif (-not (Test-Path -LiteralPath (Join-Path $PackagedAssets "installation_seed.json") -PathType Leaf)) {
        throw "The compiled host package is missing installation_seed.json."
    }
    Push-Location $LanRoot
    try {
        & go build -trimpath -ldflags="-s -w" -o (Join-Path $ReleaseDirectory "massar-lan-host.exe") .
        if ($LASTEXITCODE -ne 0) { throw "LAN gateway compilation failed." }
    }
    finally { Pop-Location }
    Copy-VisualCppRuntime $ReleaseDirectory
    $Seed = Get-Content -LiteralPath $SeedSource -Raw | ConvertFrom-Json
    $HistoryKeys = @("sessions", "packages", "attendances", "payments", "academics", "academicActivities", "audit", "staff", "reviews", "closings", "paymentChecks", "corrections", "refunds", "cardPayments", "cardReceipts", "debtSettlements", "centerFees")
    foreach ($Key in $HistoryKeys) {
        if ($null -ne $Seed.state.$Key -and @($Seed.state.$Key).Count -ne 0) { throw "First-install seed contains operational history: $Key" }
    }
    $Manifest = [ordered]@{
        role = $PackageMode
        appVersion = $AppVersion
        buildId = $BuildId
        sourceCommit = $env:GITHUB_SHA
        builtAt = [DateTime]::UtcNow.ToString("o")
        testsSkipped = [bool]$SkipTests
        seedId = if ($ClientOnly) { $null } else { $Seed.state.installationSeedId }
        students = if ($ClientOnly) { 0 } else { @($Seed.state.students).Count }
        groups = if ($ClientOnly) { 0 } else { @($Seed.state.groups).Count }
        seedSha256 = if ($ClientOnly) { $null } else { (Get-FileHash $SeedSource -Algorithm SHA256).Hash.ToLowerInvariant() }
    }
    $Manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ReleaseDirectory "build-manifest.json") -Encoding utf8
    Assert-RunnerIdentity
    Assert-NoRuntimeData $ReleaseDirectory
    $Destination = Join-Path $AppRoot $OutputDirectory
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $Archive = Join-Path $Destination "massar-center-windows-$PackageMode-$OutputTag-$BuildId.zip"
    if (Test-Path -LiteralPath $Archive) { throw "Output archive already exists; choose a new OutputTag." }
    Compress-Archive -Path "$ReleaseDirectory/*" -DestinationPath $Archive
    (Get-FileHash $Archive -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content "$Archive.sha256" -Encoding ascii
    Write-Output "Windows bundle: $Archive"
    Write-Output "Extract the whole archive; keep DLLs and data next to massar_center.exe."
    Write-Output "For updates, replace the same host/client application files only; preserve the existing user data and LAN settings folders."
    Write-Output "Bundled students/groups seed only a genuinely new database. Updates never reseed existing data and do not require device pairing again."
}
finally {
    Pop-Location
}
