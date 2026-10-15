import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:path/path.dart' as p;

import '../domain/models.dart';

abstract final class OmadaSetupLinks {
  static final download = Uri.parse(
    'https://support.omadanetworks.com/us/download/software/omada-controller/',
  );
  static final integrationGuide = Uri.parse(
    'https://support.omadanetworks.com/us/document/109315/',
  );

  /// The URL is data over stdin; it is never interpolated into shell source.
  static Future<void> open(Uri uri) async {
    if (uri.scheme != 'https' ||
        uri.host.isEmpty ||
        uri.userInfo.isNotEmpty ||
        RegExp(r'[\x00-\x20\x7f]').hasMatch(uri.toString())) {
      throw const CenterException(
        'اكتب عنوان إدارة Omada الصحيح باستخدام HTTPS.',
      );
    }
    if (!Platform.isWindows) {
      throw const CenterException('فتح إعدادات Omada متاح في نسخة ويندوز.');
    }
    const script = r'''
$env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
$ErrorActionPreference = 'Stop'
try {
  $url = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([Console]::In.ReadToEnd()))
  $uri = [Uri]$url
  if ($uri.Scheme -ne 'https' -or $uri.UserInfo) { exit 1 }
  $start = [Diagnostics.ProcessStartInfo]::new()
  $start.FileName = $uri.AbsoluteUri
  $start.UseShellExecute = $true
  [Diagnostics.Process]::Start($start) | Out-Null
} catch { exit 1 }
''';
    final utf16 = <int>[];
    for (final unit in script.codeUnits) {
      utf16.addAll([unit & 255, unit >> 8]);
    }
    Process? child;
    try {
      child = await Process.start(
        p.join(
          Platform.environment['SystemRoot'] ?? r'C:\Windows',
          'System32',
          'WindowsPowerShell',
          'v1.0',
          'powershell.exe',
        ),
        [
          '-NoProfile',
          '-NonInteractive',
          '-EncodedCommand',
          base64Encode(utf16),
        ],
        runInShell: false,
        environment: {
          for (final entry in Platform.environment.entries)
            if (entry.key.toLowerCase() != 'psmodulepath')
              entry.key: entry.value,
          'PSModulePath': p.join(
            Platform.environment['SystemRoot'] ?? r'C:\Windows',
            'System32',
            'WindowsPowerShell',
            'v1.0',
            'Modules',
          ),
        },
        includeParentEnvironment: false,
      );
      unawaited(child.stdout.drain<void>());
      unawaited(child.stderr.drain<void>());
      child.stdin.write(base64Encode(utf8.encode(uri.toString())));
      await child.stdin.close();
      if (await child.exitCode.timeout(const Duration(seconds: 10)) != 0) {
        throw const FormatException();
      }
    } on Object {
      child?.kill();
      throw const CenterException(
        'تعذر فتح المتصفح. انسخ عنوان الإدارة وافتحه يدويًا.',
      );
    }
  }
}
