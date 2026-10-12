# Called from build-windows.ps1; the Python desktop shell is not part of the center bundle.
function Build-ExamFrontend([string]$ExamRoot) {
    Push-Location (Join-Path $ExamRoot 'frontend')
    try {
        & npm ci | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Exam frontend dependencies failed.' }
        & npm run build | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Exam frontend compilation failed.' }
    }
    finally { Pop-Location }
}

function Exam-InputFingerprints([string]$ExamRoot) {
    $Inputs = @()
    $Inputs += Get-ChildItem -LiteralPath $ExamRoot -File | Where-Object { $_.Extension -in @('.go','.mod','.sum') -or $_.Name -in @('pdf_report.py','requirements-desktop.txt') }
    foreach ($Folder in @('cmd','web','frontend/src','windows-hotspot')) {
        $Inputs += Get-ChildItem -LiteralPath (Join-Path $ExamRoot $Folder) -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
    }
    $Inputs += Get-Item -LiteralPath (Join-Path $ExamRoot 'frontend/package.json'),(Join-Path $ExamRoot 'frontend/package-lock.json')
    foreach ($File in $Inputs | Sort-Object FullName -Unique) {
        $File.FullName.Substring($ExamRoot.Length) + (Get-FileHash -LiteralPath $File.FullName -Algorithm SHA256).Hash
    }
}

function Test-ExamNativeRuntime([string]$ExamRoot,[string]$NativeDirectory) {
    New-Item -ItemType Directory -Force -Path $NativeDirectory | Out-Null
    # Only our three generated files may be replaced; no application/user data.
    foreach ($Name in @('massar-exam-room.exe','massar-exam-portal-proxy.exe','massar-exam-network.exe')) {
        $Generated = Join-Path $NativeDirectory $Name
        if (Test-Path -LiteralPath $Generated) { Remove-Item -LiteralPath $Generated -Force }
    }
    Push-Location $ExamRoot
    try {
        # Native Windows behavior must be tested even when preverified Flutter tests are skipped.
        & go test -count=1 -timeout=5m ./... | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Windows exam engine HTTP, data or network protocol tests failed.' }
        foreach ($Target in @(@('massar-exam-room.exe','.'),@('massar-exam-portal-proxy.exe','./cmd/portal-proxy'),@('massar-exam-network.exe','./cmd/center-network'))) {
            & go build -trimpath -ldflags='-s -w' -o (Join-Path $NativeDirectory $Target[0]) $Target[1] | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "Exam native compilation failed: $($Target[0])" }
        }
        $PreviousRuntime = $env:MASSAR_TEST_EXAM_RUNTIME
        $env:MASSAR_TEST_EXAM_RUNTIME = $NativeDirectory
        Push-Location (Join-Path $PSScriptRoot '..')
        try {
            & flutter test test/windows_exam_engine_runtime_test.dart --reporter expanded | Out-Host
            if ($LASTEXITCODE -ne 0) { throw 'Windows engine reconnect or PowerShell credential permissions failed.' }
        }
        finally {
            Pop-Location
            $env:MASSAR_TEST_EXAM_RUNTIME = $PreviousRuntime
        }
        $Hashes = [ordered]@{}
        foreach ($Name in @('massar-exam-room.exe','massar-exam-portal-proxy.exe','massar-exam-network.exe')) {
            $Hashes[$Name] = (Get-FileHash -LiteralPath (Join-Path $NativeDirectory $Name) -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        return [ordered]@{ directory=$NativeDirectory; executables=$Hashes; windowsGoTestsPassed=$true; windowsCredentialReconnectTestsPassed=$true }
    }
    finally { Pop-Location }
}

function Build-ExamRuntime([string]$ExamRoot,[string]$ReleaseDirectory,[System.Collections.IDictionary]$NativeVerification) {
    if ($NativeVerification.windowsGoTestsPassed -ne $true -or $NativeVerification.windowsCredentialReconnectTestsPassed -ne $true) {
        throw 'The native Windows runtime must pass before packaging.'
    }
    Push-Location $ExamRoot
    try {
        foreach ($Name in @('massar-exam-room.exe','massar-exam-portal-proxy.exe','massar-exam-network.exe')) {
            $Native = Join-Path $NativeVerification.directory $Name
            if ((Get-FileHash -LiteralPath $Native -Algorithm SHA256).Hash.ToLowerInvariant() -ne $NativeVerification.executables[$Name]) {
                throw 'Tested native executable changed before packaging.'
            }
            $Packaged = Join-Path $ReleaseDirectory $Name
            Copy-Item -LiteralPath $Native -Destination $Packaged -Force
            if ((Get-FileHash -LiteralPath $Packaged -Algorithm SHA256).Hash.ToLowerInvariant() -ne $NativeVerification.executables[$Name]) {
                throw 'Packaged native executable differs from the tested runtime.'
            }
        }
        $HotspotDestination = Join-Path $ReleaseDirectory 'hotspot'
        if (Test-Path -LiteralPath $HotspotDestination) { Remove-Item -LiteralPath $HotspotDestination -Recurse -Force }
        & dotnet publish windows-hotspot/Massar.Hotspot.csproj -c Release -r win-x64 --self-contained true -o $HotspotDestination --nologo | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Windows Wi-Fi Direct helper compilation failed.' }
        if (-not (Test-Path -LiteralPath (Join-Path $HotspotDestination 'Massar.Hotspot.exe'))) { throw 'Wi-Fi Direct helper is missing.' }
        # Build only the report worker, with its pinned libraries and public fonts.
        & python -m pip install 'pyinstaller==6.22.3' 'reportlab==4.4.10' 'arabic-reshaper==3.0.0' 'python-bidi==0.6.7' | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'PDF worker build dependencies failed.' }
        $PdfOutput = Join-Path $ExamRoot 'build/center-pdf-dist'
        & python -m PyInstaller --noconfirm --clean --onedir --name massar-report-pdf --distpath $PdfOutput --workpath (Join-Path $ExamRoot 'build/center-pdf-work') --specpath (Join-Path $ExamRoot 'build') --add-data "$(Join-Path $ExamRoot 'web/assets/Tajawal-Regular.ttf');fonts" --add-data "$(Join-Path $ExamRoot 'web/assets/Tajawal-Bold.ttf');fonts" (Join-Path $ExamRoot 'pdf_report.py') | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'PDF worker compilation failed.' }
        $PdfDestination = Join-Path $ReleaseDirectory 'report-pdf'
        if (Test-Path -LiteralPath $PdfDestination) { Remove-Item -LiteralPath $PdfDestination -Recurse -Force }
        Copy-Item -LiteralPath (Join-Path $PdfOutput 'massar-report-pdf') -Destination $PdfDestination -Recurse
        if (-not (Test-Path -LiteralPath (Join-Path $PdfDestination 'massar-report-pdf.exe'))) { throw 'PDF worker is missing.' }
        & python (Join-Path $PSScriptRoot 'verify-exam-runtime-windows.py') $ReleaseDirectory | Out-Host
        if ($LASTEXITCODE -ne 0) { throw 'Packaged Windows native runtime or Arabic PDF/ZIP verification failed.' }
        @'
تحتاج شاشة الامتحانات داخل مسار إلى Microsoft Edge WebView2 Runtime.
إذا لم يكن موجودًا على جهاز ويندوز، جهّز المثبّت الرسمي Evergreen Standalone Installer x64 قبل يوم الامتحان:
https://developer.microsoft.com/microsoft-edge/webview2/
لا يحتاج تشغيل الامتحان إنترنت بعد اكتمال التجهيز. لا يتم تنزيل أو تثبيت مكونات تلقائيًا وقت الامتحان.
بيانات الامتحانات تُنشأ محليًا عند التشغيل؛ حزمة التحديث لا تحتوي قاعدة طلاب أو إجابات.
تشغيل Wi-Fi Direct يعتمد على محوّل الجهاز وتعريفه. يجب تجربة الهوست والساكند والموبايلات فعليًا قبل الاعتماد.
'@ | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'Windows-exam-setup.txt') -Encoding UTF8
        $Hashes = [ordered]@{}
        foreach ($Name in @('massar-exam-room.exe','massar-exam-portal-proxy.exe','massar-exam-network.exe','hotspot/Massar.Hotspot.exe','report-pdf/massar-report-pdf.exe')) {
            $Hashes[$Name] = (Get-FileHash -LiteralPath (Join-Path $ReleaseDirectory $Name) -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        return [ordered]@{ schemaVersion=1; executables=$Hashes; networkModes=@('legacy','https'); windowsGoTestsPassed=$NativeVerification.windowsGoTestsPassed; windowsCredentialReconnectTestsPassed=$NativeVerification.windowsCredentialReconnectTestsPassed; windowsCliSmokePassed=$true; syntheticPdfAndZipPassed=$true; physicalWindowsVerified=$false; includesRuntimeDatabase=$false; webView2='installed Windows runtime required' }
    }
    finally { Pop-Location }
}
