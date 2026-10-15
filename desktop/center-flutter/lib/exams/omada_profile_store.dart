import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:path/path.dart' as p;

import '../domain/models.dart';

abstract interface class OmadaProfileProtector {
  Future<Uint8List> protect(Uint8List plaintext);
  Future<Uint8List> unprotect(Uint8List ciphertext);
}

/// Connection credentials belong to this Windows user, never the center DB,
/// a support snapshot, or the source/bundle. Only DPAPI ciphertext reaches disk.
class OmadaProfileStore {
  OmadaProfileStore({required this.file, OmadaProfileProtector? protector})
    : _protector = protector ?? WindowsOmadaProfileProtector();

  final File file;
  final OmadaProfileProtector _protector;
  static const _maxBytes = 65536;

  Future<Map<String, dynamic>?> load() async {
    if (!await file.exists()) return null;
    try {
      if (await file.length() > _maxBytes) throw const FormatException();
      final envelope = jsonDecode(await file.readAsString());
      if (envelope is! Map ||
          envelope['version'] != 1 ||
          envelope['protected'] is! String) {
        throw const FormatException();
      }
      final plaintext = await _protector.unprotect(
        base64Decode(envelope['protected'] as String),
      );
      final profile = jsonDecode(utf8.decode(plaintext));
      if (profile is! Map<String, dynamic>) throw const FormatException();
      return profile;
    } on Object {
      throw const CenterException(
        'تعذر فتح بيانات Omada المحفوظة لهذا المستخدم. أدخل بيانات الربط مجددًا.',
      );
    }
  }

  Future<void> save(Map<String, dynamic> profile) async {
    final plaintext = utf8.encode(jsonEncode(profile));
    if (plaintext.length > 16384) {
      throw const CenterException('بيانات ربط Omada أكبر من الحد المسموح.');
    }
    final protected = await _protector.protect(Uint8List.fromList(plaintext));
    final envelope = jsonEncode({
      'version': 1,
      'protected': base64Encode(protected),
    });
    if (envelope.length > _maxBytes) {
      throw const CenterException('تعذر حفظ بيانات ربط Omada.');
    }
    await file.parent.create(recursive: true);
    final staged = File('${file.path}.tmp');
    try {
      await staged.writeAsString(envelope, flush: true);
      await staged.rename(file.path);
    } on Object {
      throw const CenterException(
        'لم تُحفظ بيانات الربط. راجع مساحة القرص وصلاحية مجلد البرنامج.',
      );
    } finally {
      if (await staged.exists()) await staged.delete();
    }
  }

  Future<void> forget() async {
    if (await file.exists()) await file.delete();
  }
}

class WindowsOmadaProfileProtector implements OmadaProfileProtector {
  WindowsOmadaProfileProtector({Map<String, String>? environment})
    : _environment = environment ?? Platform.environment;
  final Map<String, String> _environment;
  @override
  Future<Uint8List> protect(Uint8List plaintext) =>
      _transform('protect', plaintext);

  @override
  Future<Uint8List> unprotect(Uint8List ciphertext) =>
      _transform('unprotect', ciphertext);

  Future<Uint8List> _transform(String operation, Uint8List bytes) async {
    if (!Platform.isWindows) {
      throw const CenterException('حفظ ربط Omada متاح في نسخة ويندوز.');
    }
    const script = r'''
$env:PSModulePath = [IO.Path]::Combine($PSHOME, 'Modules')
$ErrorActionPreference = 'Stop'
try {
  Add-Type -AssemblyName System.Security
  $inputJson = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([Console]::In.ReadToEnd()))
  $request = $inputJson | ConvertFrom-Json
  $bytes = [Convert]::FromBase64String($request.payload)
  if ($bytes.Length -gt 49152) { throw 'size' }
  $entropy = [Text.Encoding]::UTF8.GetBytes('Massar.Omada.Profile.v1')
  if ($request.operation -eq 'protect') {
    $output = [Security.Cryptography.ProtectedData]::Protect($bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
  } elseif ($request.operation -eq 'unprotect') {
    $output = [Security.Cryptography.ProtectedData]::Unprotect($bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
  } else { throw 'operation' }
  [Console]::Out.Write([Convert]::ToBase64String($output))
} catch { exit 1 }
''';
    final encodedScript = <int>[];
    for (final unit in script.codeUnits) {
      encodedScript.addAll([unit & 255, unit >> 8]);
    }
    final executable = p.join(
      Platform.environment['SystemRoot'] ?? r'C:\Windows',
      'System32',
      'WindowsPowerShell',
      'v1.0',
      'powershell.exe',
    );
    Process? child;
    try {
      child = await Process.start(
        executable,
        [
          '-NoProfile',
          '-NonInteractive',
          '-EncodedCommand',
          base64Encode(encodedScript),
        ],
        runInShell: false,
        environment: {
          for (final entry in _environment.entries)
            if (entry.key.toLowerCase() != 'psmodulepath')
              entry.key: entry.value,
          'PSModulePath': p.join(p.dirname(executable), 'Modules'),
        },
        includeParentEnvironment: false,
      );
      final output = _boundedOutput(child.stdout);
      unawaited(child.stderr.drain<void>());
      child.stdin.write(
        base64Encode(
          utf8.encode(
            jsonEncode({
              'operation': operation,
              'payload': base64Encode(bytes),
            }),
          ),
        ),
      );
      await child.stdin.close();
      final code = await child.exitCode.timeout(const Duration(seconds: 15));
      final text = await output.timeout(const Duration(seconds: 2));
      if (code != 0 || text == null) throw const FormatException();
      return base64Decode(text);
    } on Object {
      child?.kill();
      throw const CenterException(
        'تعذر حماية بيانات Omada بواسطة ويندوز. لم تُحفظ كلمة السر.',
      );
    }
  }

  Future<String?> _boundedOutput(Stream<List<int>> stream) async {
    final bytes = <int>[];
    var oversized = false;
    await for (final chunk in stream) {
      if (bytes.length + chunk.length > 65536) {
        oversized = true;
      } else if (!oversized) {
        bytes.addAll(chunk);
      }
    }
    return oversized ? null : utf8.decode(bytes, allowMalformed: true).trim();
  }
}
