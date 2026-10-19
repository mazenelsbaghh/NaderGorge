import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:path/path.dart' as p;

import '../domain/models.dart';

abstract final class LanFirewallRepair {
  static const _launcher = r"""
$env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
$ErrorActionPreference = 'Stop'
try {
  $pathBase64 = [Console]::In.ReadToEnd()
  $scriptPath = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($pathBase64))
  if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { exit 10 }
  $command = '$env:PSModulePath = [IO.Path]::Combine($PSHOME, ''Modules''); $ErrorActionPreference = ''Stop''; try { & ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(''' + $pathBase64 + '''))); if ($null -eq $LASTEXITCODE) { exit 21 }; exit $LASTEXITCODE } catch { exit 21 }'
  $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
  $start = [Diagnostics.ProcessStartInfo]::new()
  $start.FileName = [IO.Path]::Combine($PSHOME, 'powershell.exe')
  $start.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand ' + $encoded
  $start.UseShellExecute = $true
  $start.Verb = 'runas'
  $child = [Diagnostics.Process]::Start($start)
  if (-not $child.WaitForExit(120000)) { exit 22 }
  exit $child.ExitCode
} catch {
  $failure = $_.Exception
  while ($failure.InnerException) { $failure = $failure.InnerException }
  if ($failure -is [ComponentModel.Win32Exception] -and $failure.NativeErrorCode -eq 1223) { exit 20 }
  exit 21
}
""";

  static Future<void> repair({String? applicationDirectory}) async {
    if (!Platform.isWindows) {
      throw const CenterException('إصلاح صلاحيات الشبكة متاح على ويندوز.');
    }
    final repairScript = File(
      p.join(
        applicationDirectory ?? p.dirname(Platform.resolvedExecutable),
        'repair-lan-firewall.ps1',
      ),
    );
    if (!await repairScript.exists()) throw failure(10);
    final child = await _startLauncher();
    unawaited(child.stdout.drain<void>());
    unawaited(child.stderr.drain<void>());
    child.stdin.write(base64Encode(utf8.encode(repairScript.absolute.path)));
    await child.stdin.close();
    final exitCode = await child.exitCode;
    if (exitCode != 0) throw failure(exitCode);
  }

  static Future<Process> _startLauncher() async {
    final windowsRoot = Platform.environment['SystemRoot'] ?? r'C:\Windows';
    final utf16 = [
      for (final unit in _launcher.codeUnits) ...[unit & 255, unit >> 8],
    ];
    return Process.start(
      p.join(
        windowsRoot,
        'System32',
        'WindowsPowerShell',
        'v1.0',
        'powershell.exe',
      ),
      [
        '-NoProfile',
        '-NonInteractive',
        '-WindowStyle',
        'Hidden',
        '-EncodedCommand',
        base64Encode(utf16),
      ],
      runInShell: false,
      environment: {
        for (final entry in Platform.environment.entries)
          if (entry.key.toLowerCase() != 'psmodulepath') entry.key: entry.value,
        'PSModulePath': p.join(
          windowsRoot,
          'System32',
          'WindowsPowerShell',
          'v1.0',
          'Modules',
        ),
      },
      includeParentEnvironment: false,
    );
  }

  static CenterException failure(
    int exitCode,
  ) => CenterException(switch (exitCode) {
    10 || 11 =>
      'ملفات إصلاح الشبكة ناقصة أو تخص نسخة أخرى. فك ضغط حزمة الهوست كاملة ثم حاول مجددًا.',
    12 =>
      'جدار الحماية يمنع هذه النسخة صراحةً. اسمح لمسار في إعدادات جدار حماية ويندوز، ثم أعد المحاولة.',
    13 =>
      'قواعد الشبكة الموجودة اتعدلت يدويًا. صدّر سجل مشكلة الربط لمراجعتها.',
    14 =>
      'لم يكتمل الإصلاح أو التراجع عن التغييرات. صدّر سجل مشكلة الربط لمراجعة الإعدادات.',
    20 =>
      'اتلغى طلب صلاحية المسؤول. اضغط إصلاح صلاحيات الشبكة مجددًا ووافق على طلب ويندوز لإتمامه.',
    22 =>
      'لم يصل تأكيد انتهاء إصلاح الشبكة. انتظر قليلًا ثم اختبر الاتصال، وصدّر سجل الربط لو استمرت المشكلة.',
    _ =>
      'تعذر إصلاح صلاحيات الشبكة. حاول مجددًا ووافق على طلب المسؤول، أو صدّر سجل مشكلة الربط.',
  }, diagnosticCode: exitCode);
}
