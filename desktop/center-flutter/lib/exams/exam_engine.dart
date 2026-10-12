import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:path/path.dart' as p;

import '../domain/models.dart';
import 'exam_downloads.dart';

abstract interface class ExamEngine {
  bool get running;
  Future<void> start();
  Future<bool> reconnect();
  Future<Map<String, dynamic>> request(
    String route, [
    Map<String, dynamic>? body,
  ]);
  Future<Map<String, dynamic>> network(Map<String, dynamic> command);
  Future<void> close();
}

/// The exam process owns only exam attempts. Center money remains at the host.
/// Closing a view/controller never terminates an active room.
class WindowsExamEngine implements ExamEngine, ExamDownloadSource {
  WindowsExamEngine({required this.directory, String? executableDirectory})
    : executableDirectory =
          executableDirectory ?? p.dirname(Platform.resolvedExecutable);

  final Directory directory;
  final String executableDirectory;
  final HttpClient _http = HttpClient()
    ..connectionTimeout = const Duration(seconds: 5)
    ..findProxy = (_) => 'DIRECT';
  String? _token;
  bool _running = false;
  bool _starting = false;
  bool _closed = false;
  @override
  bool get running => _running;
  File get _connectionFile =>
      File(p.join(directory.path, 'engine-connection.json'));

  Future<void> _protectCredentials() async {
    if (!Platform.isWindows) return;
    const script = r'''
$ErrorActionPreference = 'Stop'
$payload = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([Console]::In.ReadToEnd()))
$root = ($payload | ConvertFrom-Json).path
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
$system = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetOwner($identity)
$acl.SetAccessRuleProtection($true, $false)
foreach ($sid in @($identity, $system)) {
  $rule = [Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
  $acl.AddAccessRule($rule)
}
Set-Acl -LiteralPath $root -AclObject $acl
foreach ($name in @('engine-connection.json', 'engine-connection.json.tmp')) {
  $file = Join-Path $root $name
  if (Test-Path -LiteralPath $file) {
    $fileAcl = [Security.AccessControl.FileSecurity]::new()
    $fileAcl.SetOwner($identity)
    $fileAcl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @($identity, $system)) {
      $fileAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'Allow'))
    }
    Set-Acl -LiteralPath $file -AclObject $fileAcl
  }
}
''';
    final utf16 = <int>[];
    for (final unit in script.codeUnits) {
      utf16.addAll([unit & 255, unit >> 8]);
    }
    final process = await Process.start(
      p.join(
        Platform.environment['SystemRoot'] ?? r'C:\Windows',
        'System32',
        'WindowsPowerShell',
        'v1.0',
        'powershell.exe',
      ),
      ['-NoProfile', '-NonInteractive', '-EncodedCommand', base64Encode(utf16)],
      runInShell: false,
    );
    unawaited(process.stdout.drain<void>());
    unawaited(process.stderr.drain<void>());
    process.stdin.write(
      base64Encode(utf8.encode(jsonEncode({'path': directory.path}))),
    );
    await process.stdin.close();
    if (await process.exitCode.timeout(const Duration(seconds: 15)) != 0) {
      throw const CenterException(
        'تعذر حماية بيانات اتصال الامتحان لهذا المستخدم.',
      );
    }
  }

  @override
  Future<bool> reconnect() async {
    _running = false;
    _token = null;
    if (_closed || !await _connectionFile.exists()) return false;
    try {
      await _protectCredentials();
      if (await _connectionFile.length() > 4096) return false;
      final value = jsonDecode(await _connectionFile.readAsString()) as Map;
      final token = value['adminToken'];
      if (value['protocol'] != 1 ||
          token is! String ||
          token.length < 32 ||
          token.length > 256) {
        return false;
      }
      _token = token;
      final state = await request('/api/center/state');
      _running =
          state['protocol'] == 1 && state['sourceId'] == value['sourceId'];
    } on Object {
      _running = false;
    }
    if (!_running) _token = null;
    return _running;
  }

  @override
  Future<void> start() async {
    if (_closed || _starting) {
      throw const CenterException('يجري تشغيل خدمة الامتحانات.');
    }
    _starting = true;
    Process? child;
    StreamSubscription<String>? output;
    try {
      if (await reconnect()) return;
      if (!Platform.isWindows) {
        throw const CenterException('الامتحانات المدمجة متاحة في نسخة ويندوز.');
      }
      await directory.create(recursive: true);
      await _protectCredentials();
      final ready = Completer<Map<String, dynamic>>();
      child = await Process.start(
        p.join(executableDirectory, 'massar-exam-room.exe'),
        [
          '--center-integrated',
          '--pdf-helper',
          p.join(executableDirectory, 'report-pdf', 'massar-report-pdf.exe'),
          '--data-dir',
          directory.path,
          '--port',
          '8765',
          '--admin-port',
          '8766',
        ],
        mode: ProcessStartMode.detachedWithStdio,
        runInShell: false,
      );
      unawaited(child.stderr.drain<void>());
      output = child.stdout
          .transform(utf8.decoder)
          .transform(const LineSplitter())
          .listen(
            (line) {
              if (ready.isCompleted || line.length > 8192) return;
              try {
                final value = jsonDecode(line);
                if (value is Map &&
                    value['ready'] == true &&
                    value['protocol'] == 1) {
                  ready.complete(Map<String, dynamic>.from(value));
                }
              } on FormatException {
                /* Diagnostic lines are not the ready protocol. */
              }
            },
            onDone: () {
              if (!ready.isCompleted) {
                ready.completeError(
                  const CenterException(
                    'تعذر تشغيل محرك الامتحان. راجع سجل المشاكل.',
                  ),
                );
              }
            },
          );
      final handshake = await ready.future.timeout(const Duration(seconds: 35));
      final token = handshake['adminToken'];
      if (token is! String ||
          token.length < 32 ||
          handshake['adminPort'] != 8766 ||
          handshake['studentPort'] != 8765) {
        throw const CenterException('نسخة محرك الامتحان لا تطابق نسخة السنتر.');
      }
      _token = token;
      final state = await request('/api/center/state');
      if (state['protocol'] != 1 || state['sourceId'] is! String) {
        throw const CenterException('تعذر التحقق من محرك الامتحان.');
      }
      final staged = File('${_connectionFile.path}.tmp');
      await staged.writeAsString(
        jsonEncode({
          'protocol': 1,
          'adminToken': token,
          'sourceId': state['sourceId'],
        }),
        flush: true,
      );
      await staged.rename(_connectionFile.path);
      _running = true;
    } on Object {
      // Only this failed startup child is ours to stop. Never stop a reattached room.
      child?.kill();
      _token = null;
      _running = false;
      rethrow;
    } finally {
      _starting = false;
      await output?.cancel();
    }
  }

  @override
  Future<Map<String, dynamic>> request(
    String route, [
    Map<String, dynamic>? body,
  ]) async {
    if (_closed || _token == null || !route.startsWith('/api/')) {
      throw const CenterException('خدمة الامتحان غير متصلة.');
    }
    final request = await _http.openUrl(
      body == null ? 'GET' : 'POST',
      Uri.http('127.0.0.1:8766', route),
    );
    request.followRedirects = false;
    request.headers.set('X-Admin-Token', _token!);
    if (body != null) {
      request.headers.contentType = ContentType.json;
      request.headers.set('X-Exam-Request', '1');
      final payload = utf8.encode(jsonEncode(body));
      request.contentLength = payload.length;
      request.add(payload);
    }
    final response = await request.close().timeout(const Duration(seconds: 15));
    final bytes = <int>[];
    await for (final chunk in response.timeout(const Duration(seconds: 15))) {
      if (bytes.length + chunk.length > 12 * 1024 * 1024) {
        throw const CenterException('بيانات الامتحان أكبر من الحد المسموح.');
      }
      bytes.addAll(chunk);
    }
    final value = jsonDecode(utf8.decode(bytes));
    if (response.statusCode != 200 || value is! Map) {
      final message = value is Map ? value['error'] : null;
      throw CenterException(
        message is String && message.length < 500
            ? message
            : 'تعذر الاتصال بمحرك الامتحان.',
      );
    }
    return Map<String, dynamic>.from(value);
  }

  @override
  Future<Map<String, dynamic>> network(Map<String, dynamic> command) async {
    if (!Platform.isWindows) {
      throw const CenterException('تهيئة الشبكة متاحة على ويندوز.');
    }
    final process = await Process.start(
      p.join(executableDirectory, 'massar-exam-network.exe'),
      const [],
      runInShell: false,
    );
    final result = process.stdout.transform(utf8.decoder).join();
    unawaited(process.stderr.drain<void>());
    process.stdin.write(
      jsonEncode({
        ...command,
        'adminPort': 8766,
        if (_token != null) 'adminToken': _token,
      }),
    );
    await process.stdin.close();
    final text = await result.timeout(
      const Duration(minutes: 3),
      onTimeout: () {
        process.kill();
        throw const CenterException(
          'لم تكتمل تهيئة الشبكة. افحص الحالة قبل المحاولة مجددًا.',
        );
      },
    );
    if (text.length > 1024 * 1024) {
      throw const CenterException('تعذر قراءة حالة الشبكة.');
    }
    final value = jsonDecode(text);
    if (value is! Map) throw const CenterException('تعذر قراءة حالة الشبكة.');
    return Map<String, dynamic>.from(value);
  }

  @override
  Future<File> receiveExport(String route, String mimeType) {
    if (_closed || !_running || _token == null) {
      throw const CenterException('افتح إدارة الامتحان قبل تنزيل الملفات.');
    }
    return ExamDownloads(
      adminToken: () => _token!,
      runtimeDirectory: directory,
    ).receive(route, mimeType);
  }

  @override
  Future<void> close() async {
    if (_running && !_closed) {
      try {
        await request('/api/center/admin-access', {'enabled': false});
      } on Object {
        /* The engine's short authorization lease also expires. */
      }
    }
    _closed = true;
    _http.close(force: true);
    // The detached engine and answers continue until rooms are deliberately closed.
  }
}
