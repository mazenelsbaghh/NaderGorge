import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/exam_engine.dart';
import 'package:path/path.dart' as p;

// CI supplies the just-built native engine. All state belongs to a fresh
// synthetic directory; these tests never configure adapters or read host data.
void main() {
  final runtimeDirectory = Platform.environment['MASSAR_TEST_EXAM_RUNTIME'];
  final skip = !Platform.isWindows || runtimeDirectory == null;

  test(
    'first startup protects an Arabic directory before launching a child',
    () async {
      final root = await Directory.systemTemp.createTemp('massar-acl-');
      final data = Directory(p.join(root.path, 'اختبار صلاحيات المسار'));
      final engine = WindowsExamEngine(
        directory: data,
        executableDirectory: p.join(root.path, 'missing-native-runtime'),
      );
      addTearDown(() async {
        await engine.close();
        await root.delete(recursive: true);
      });

      await expectLater(engine.start(), throwsA(isA<ProcessException>()));
      expect(engine.running, isFalse);
      await expectPrivateAcl(data.path);
    },
    skip: !Platform.isWindows,
    timeout: const Timeout(Duration(minutes: 1)),
  );

  test(
    'native engine reconnect authenticates and protects persisted credentials',
    () async {
      final root = await Directory.systemTemp.createTemp('massar-reconnect-');
      final data = await Directory(
        p.join(root.path, 'قاعة امتحان تجريبية'),
      ).create();
      final child = await Process.start(
        p.join(runtimeDirectory!, 'massar-exam-room.exe'),
        [
          '--center-integrated',
          '--data-dir',
          data.path,
          '--student-address',
          '127.0.0.1',
          '--port',
          '8765',
          '--admin-port',
          '8766',
        ],
        runInShell: false,
      );
      final ready = Completer<Map<String, dynamic>>();
      final output = child.stdout
          .transform(utf8.decoder)
          .transform(const LineSplitter())
          .listen(
            (line) {
              try {
                final message = jsonDecode(line);
                if (message is Map<String, dynamic> &&
                    message['ready'] == true &&
                    !ready.isCompleted) {
                  ready.complete(message);
                }
              } on FormatException {
                /* Startup diagnostics are not protocol messages. */
              }
            },
            onDone: () {
              if (!ready.isCompleted) {
                ready.completeError(
                  StateError('Native engine exited before readiness.'),
                );
              }
            },
          );
      unawaited(child.stderr.drain<void>());
      final engine = WindowsExamEngine(
        directory: data,
        executableDirectory: runtimeDirectory,
      );
      addTearDown(() async {
        await engine.close();
        child.kill();
        await child.exitCode.timeout(const Duration(seconds: 15));
        await output.cancel();
        await root.delete(recursive: true);
      });

      final handshake = await ready.future.timeout(const Duration(seconds: 30));
      expect(handshake['protocol'], 1);
      final token = handshake['adminToken'] as String;
      final http = HttpClient()..findProxy = (_) => 'DIRECT';
      addTearDown(() => http.close(force: true));
      final request = await http.getUrl(
        Uri.http('127.0.0.1:8766', '/api/center/state'),
      );
      request.headers.set('X-Admin-Token', token);
      final response = await request.close().timeout(
        const Duration(seconds: 10),
      );
      expect(response.statusCode, HttpStatus.ok);
      final state = jsonDecode(await utf8.decoder.bind(response).join()) as Map;
      final connection = File(p.join(data.path, 'engine-connection.json'));
      final staged = File('${connection.path}.tmp');
      final credentials = {
        'protocol': 1,
        'adminToken': token,
        'sourceId': state['sourceId'],
      };
      await connection.writeAsString(jsonEncode(credentials), flush: true);
      await staged.writeAsString(jsonEncode(credentials), flush: true);

      expect(
        await engine.reconnect(),
        isTrue,
        reason: engine.credentialProtectionDiagnostic,
      );
      expect(engine.running, isTrue);
      for (final path in [data.path, connection.path, staged.path]) {
        await expectPrivateAcl(path);
      }
      expect(
        (await engine.request('/api/center/state'))['sourceId'],
        state['sourceId'],
      );

      final firstStart = engine.start();
      await expectLater(engine.start(), throwsA(isA<CenterException>()));
      await firstStart;
      expect(
        engine.running,
        isTrue,
        reason:
            'Concurrent start is rejected while the first reconnect owns startup.',
      );

      await connection.writeAsString(
        jsonEncode({...credentials, 'adminToken': 'x' * 64}),
      );
      expect(
        await engine.reconnect(),
        isFalse,
        reason: 'A different token must not attach to an existing process.',
      );
      expect(engine.running, isFalse);

      await connection.writeAsString(
        jsonEncode({...credentials, 'sourceId': 'different-exam-store'}),
      );
      expect(
        await engine.reconnect(),
        isFalse,
        reason: 'Credentials from a different data directory must not attach.',
      );
      expect(engine.running, isFalse);

      await connection.writeAsString(jsonEncode(credentials));
      expect(await engine.reconnect(), isTrue);
      await connection.writeAsString(
        jsonEncode({...credentials, 'adminToken': ''}),
      );
      expect(await engine.reconnect(), isFalse);
      expect(
        engine.running,
        isFalse,
        reason: 'Rejected credentials clear previous connection state.',
      );
    },
    skip: skip,
    timeout: const Timeout(Duration(minutes: 3)),
  );
}

Future<void> expectPrivateAcl(String path) async {
  const script = r'''
$ErrorActionPreference = 'Stop'
$path = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([Console]::In.ReadToEnd()))
$acl = Get-Acl -LiteralPath $path
$rules = @($acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]) | ForEach-Object {
  @{ sid=$_.IdentityReference.Value; inherited=$_.IsInherited; access=$_.AccessControlType.ToString(); full=(($_.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -eq [Security.AccessControl.FileSystemRights]::FullControl) }
})
@{ user=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value; owner=$acl.GetOwner([Security.Principal.SecurityIdentifier]).Value; protected=$acl.AreAccessRulesProtected; rules=$rules } | ConvertTo-Json -Depth 4 -Compress
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
  final output = process.stdout.transform(utf8.decoder).join();
  unawaited(process.stderr.drain<void>());
  process.stdin.write(base64Encode(utf8.encode(path)));
  await process.stdin.close();
  expect(await process.exitCode.timeout(const Duration(seconds: 15)), 0);
  final acl = jsonDecode((await output).trim()) as Map;
  expect(acl['owner'], acl['user']);
  expect(acl['protected'], isTrue);
  final rules = (acl['rules'] as List).cast<Map>();
  expect(rules.map((rule) => rule['sid']).toSet(), {acl['user'], 'S-1-5-18'});
  expect(rules, hasLength(2));
  for (final rule in rules) {
    expect(rule['inherited'], isFalse);
    expect(rule['access'], 'Allow');
    expect(rule['full'], isTrue);
  }
}
