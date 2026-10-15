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

function Build-ExamRuntime([string]$ExamRoot,[string]$ReleaseDirectory,[System.Collections.IDictionary]$NativeVerification,[string]$AppVersion) {
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
        $SetupGuide = @'
مسار — تجهيز ويندوز __APP_VERSION__

استخدم الهوست والساكند من الإصدار نفسه. هذه الحزمة للتجربة على الجهازين قبل اعتمادها في القاعة.
تحتاج شاشة الامتحان إلى Microsoft Edge WebView2 Runtime. جهّز المثبّت الرسمي Evergreen Standalone Installer x64 إذا لم يكن موجودًا:
https://developer.microsoft.com/microsoft-edge/webview2/

١. من شاشة دخول الهوست افتح «تجهيز الشبكة والـOmada» ثم «تشغيل شبكة الهوست». وصّل الساكند بها بكلمة سر نقطة اتصال ويندوز. الزر يحتفظ بالشبكة الشغالة ويعرض فشل ويندوز إذا لم يدعم الجهاز التشغيل. الكابل المباشر بديل: جهّز Ethernet الساكند بعنوان 10.77.0.1/22، ثم وصّل الهوست به واجعل الوصلة Private. لا تضع عنوان الساكند على الهوست.
٢. شغّل السيرفر من «ربط الأجهزة» على الهوست، وقارن البصمة واربط الساكند وسجّل دخول الموظف عليه. التسجيل يعمل على الجهازين طوال الاتصال. يُحمّل دليل المجموعات والحصص الفعلية والامتحانات تلقائيًا؛ لا تحتاج تجهيز كشف طلاب لكل حصة. جهّز نموذج الأسئلة في مكتبة الساكند، والمجاميع المسموح بها في رصد السنتر، مثل 10 و12.
٣. داخل القاعة وصّل Omada بالساكند مع استمرار الكهرباء أو PoE. شبكة الطلاب تبقى Mr Nader Center exam؛ لا تعمل Reset ولا تحتاج شبكة هوست ثانية من Omada. من البداية اضغط «فتح امتحان الساكند» دون موظف أو اتصال الهوست. من «القاعة والتصحيح» اختر «فتح القاعة»، ثم نموذج الأسئلة والمجموعة والحصة والامتحان من الدليل المحفوظ. انتقل إلى «متابعة الطلاب» ثم «التصحيح والرصد»؛ رابط الدخول وQR والإعدادات الإضافية تفتح عند الحاجة. إذا لم يصل الدليل يمكن فتح قاعة مستقلة واختيار وجهة الرصد لاحقًا على الهوست.
٤. الطالب يفتح http://10.77.0.1/ ويكتب الاسم والموبايل والكود، دون اشتراط كشف مسبق أو تسجيل حضور للدخول. بعد التسليم أغلق القاعة وصحّح المقالي. الإجابات والتصحيح تبقى على الساكند. عند فصل الجهازين يواصل الهوست التسجيل والساكند الامتحان؛ تسجيل السنتر على الساكند يحتاج اتصال الهوست.
٥. بعد التصحيح أعد الربط وسجّل دخول الموظف على الساكند. على الهوست افتح «تحميل درجات الساكند»، انتظر وصول الكشف واختر وجهة الرصد ثم «تأكيد ورصد» مرة واحدة للمطابقين. تظهر الأعداد والأسباب وشيت XLSX للحاضرين الذين لم يدخلوا وورقة المتابعة. لا تحتاج شيت استيراد. الصفر صحيح وتكرار النتيجة لا يضاعفها؛ كل طالب يحتفظ بمجموعه الفعلي دون تحويل /10 إلى /12.
٦. من «واجب الموبايل» على الهوست شغّل الهوتسبوت، وصّل هاتف الموظف بالشبكة، ثم جهّز الرابط. تُقرأ نقطة الاتصال الفعلية قبل عرض الرابط؛ إذا لم يظهر عنوانها أعد المحاولة بعد الاتصال. الكاميرا تحتاج شهادة الاتصال حسب النافذة. الرابط ينتهي بخروج الموظف أو بعد ساعتين؛ لا تشاركه مع الطلاب.
٧. افتح «واتساب» من قائمة السنتر. اختر السنتر والمجموعة والحصة والامتحان والواجب، وراجع الطلاب ثم افتح القالب والمعاينة. اختر القالب الموجود واضبط مصادر متغيراته واضغط «تثبيت ربط المتغيرات» مرة واحدة؛ بعدها تُملأ بيانات كل طالب تلقائيًا. استورد إعداد الحساب داخل البرنامج أول مرة وراجع إصدار API قبل الحفظ والإرسال. القوالب تُجلب من الحساب؛ متغيرات العنوان والنص مستقلة وقابلة للتعديل. الحد العالمي خمسة طلبات متزامنة. لا تعاد الطلبات المقبولة أو غير المؤكدة تلقائيًا. إعداد الحساب وأسراره لا تدخل الحزمة، ولم تُرسل رسائل حقيقية في الاختبارات.

إذا ظهر 169.254 على Ethernet الهوست مع الكابل المباشر إلى الساكند 10.77.0.1، اضبط Ethernet الهوست على 10.77.0.10 وقناع 255.255.252.0 واترك Gateway وDNS فارغين. أعد البحث في ربط الأجهزة. رجّع الهوست إلى IP تلقائي عند توصيله براوتر.
أغلق النسخة القديمة وخدمة الامتحان القديمة؛ إعادة تشغيل ويندوز قبل تجربة الحزمة تمنع استخدام محرك قديم. تنزيل WebView2 أول مرة يحتاج إنترنت، والتشغيل المحلي بعد التجهيز لا يحتاجه. لا تُنزّل مكونات أثناء الامتحان.
تنبيه دخول الامتحان دون حضور يصل بعد رجوع الاتصال ولا يسجل دفعًا أو حضورًا. التغطية والهوتسبوت يحتاجان تجربة فعلية على الجهازين والهواتف.
إذا استمر قفل تهيئة قديم راجع الحالة والسجل مع إغلاق التطبيقات؛ لا تحذفه يدويًا أو تستخدم إعداد جهاز آخر.
الهوست يحتوي تصحيح بيانات خاصًا مُراجعًا؛ يحفظ نسخة استرداد ويرفض تعارض العمل الأحدث، ولا يكرر التصحيح. لا تشارك حزمة الهوست أو النسخة الاحتياطية علنًا. الساكند لا يحتوي بيانات السنتر الخاصة. اختبر الربط ودخول هاتف وتسليم الامتحان والرصد والواجب قبل استخدام القاعة.
'@
        $SetupGuide.Replace('__APP_VERSION__', $AppVersion) | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'Windows-exam-setup.txt') -Encoding UTF8
        $Hashes = [ordered]@{}
        foreach ($Name in @('massar-exam-room.exe','massar-exam-portal-proxy.exe','massar-exam-network.exe','hotspot/Massar.Hotspot.exe','report-pdf/massar-report-pdf.exe')) {
            $Hashes[$Name] = (Get-FileHash -LiteralPath (Join-Path $ReleaseDirectory $Name) -Algorithm SHA256).Hash.ToLowerInvariant()
        }
        return [ordered]@{ schemaVersion=1; executables=$Hashes; networkModes=@('legacy','https'); windowsGoTestsPassed=$NativeVerification.windowsGoTestsPassed; windowsCredentialReconnectTestsPassed=$NativeVerification.windowsCredentialReconnectTestsPassed; windowsCliSmokePassed=$true; syntheticPdfAndZipPassed=$true; physicalWindowsVerified=$false; includesRuntimeDatabase=$false; webView2='installed Windows runtime required' }
    }
    finally { Pop-Location }
}
