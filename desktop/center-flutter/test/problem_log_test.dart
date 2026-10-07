import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/problem_log.dart';
import 'package:massar_center/shared/app_build_metadata.dart';
import 'package:path/path.dart' as path;
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

class _SecretException implements Exception {
  @override
  String toString() => throw StateError('never inspect raw errors');
}

void main() {
  late Directory sandbox;
  late Directory diagnostics;
  late ProblemLog log;
  const secret = 'Ahmed01012345678_password_SQL_SELECT_private_body';
  final trace = StackTrace.fromString(
    '#0 $secret (package:massar_center/application/center_store.dart:42:7)\n'
    '#1 $secret (file:///Users/$secret/private/lib/main.dart:18:4)\n'
    '#2 arbitrary (/Users/$secret/unknown.dart:1:2)\n'
    '#3 $secret (package:massar_center/$secret.dart:1:2)\n'
    '<asynchronous suspension>',
  );

  setUp(() async {
    sandbox = await Directory.systemTemp.createTemp('massar-problem-log-');
    diagnostics = Directory(path.join(sandbox.path, 'diagnostics'));
    log = ProblemLog(diagnostics);
  });

  tearDown(() async {
    await log.flush();
    await sandbox.delete(recursive: true);
  });

  Future<List<Map<String, dynamic>>> exported({
    String name = 'report.txt',
  }) async {
    final destination = path.join(sandbox.path, name);
    expect(await log.exportTo(destination), destination);
    return const LineSplitter()
        .convert(await File(destination).readAsString())
        .map((line) => jsonDecode(line) as Map<String, dynamic>)
        .toList();
  }

  test(
    'numeric domain reason survives export without exposing its message',
    () async {
      await log.record(
        const CenterException(secret, diagnosticCode: 1102),
        StackTrace.empty,
        operation: 'auth.sign_in',
      );
      await log.record(
        const CenterException(secret, diagnosticCode: -1),
        StackTrace.empty,
        operation: 'entry',
      );
      final rows = await exported();
      expect(rows[1]['errors'], [
        {'type': 'CenterException', 'code': 1102},
      ]);
      expect(rows[2]['errors'], [
        {'type': 'CenterException'},
      ]);
      expect(jsonEncode(rows), isNot(contains(secret)));
    },
  );

  test(
    'session metadata survives restart and concurrent events export in queue order',
    () async {
      await log.startSession();
      await log.startSession();
      final writes = [
        for (var i = 0; i < 100; i++)
          log.record(StateError('$secret $i'), trace, operation: 'entry'),
      ];
      final export = exported();
      await Future.wait(writes);
      final first = await export;
      final events = first.skip(1).toList();
      expect(first.first['eventCount'], 101);
      expect(first.first['build'], 'development');
      expect(events.every((event) => event['build'] == 'development'), isTrue);
      expect(events.where((event) => event['kind'] == 'session'), hasLength(1));
      expect(events.map((event) => event['id']).toSet(), hasLength(101));
      expect(events.map((event) => event['session']).toSet(), hasLength(1));
      expect(
        events.every((event) => event['version'] == AppBuildMetadata.version),
        isTrue,
      );
      expect(
        events.every((event) => DateTime.parse(event['time'] as String).isUtc),
        isTrue,
      );
      expect(jsonEncode(first), isNot(contains(secret)));
      final previousSession = events.first['session'];
      log = ProblemLog(diagnostics);
      await log.startSession();
      await log.record(
        const FormatException(secret),
        trace,
        operation: 'auth.sign_in',
      );
      final second = await exported(name: 'restarted.txt');
      expect(second.first['eventCount'], 103);
      expect(second.last['operation'], 'auth.sign_in');
      expect(second.last['session'], isNot(previousSession));
      expect(
        second.skip(1).where((event) => event['kind'] == 'session'),
        hasLength(2),
      );
    },
  );

  test(
    'redacts actual SQLite statement arguments plus causes, OS paths and function names',
    () async {
      sqfliteFfiInit();
      final db = await databaseFactoryFfiNoIsolate.openDatabase(
        path.join(sandbox.path, 'private.sqlite'),
      );
      try {
        await db.execute(
          'CREATE TABLE students (id INTEGER PRIMARY KEY, private_body TEXT)',
        );
        await db.insert('students', {'id': 1, 'private_body': secret});
        Object? failure;
        try {
          await db.insert('students', {'id': 1, 'private_body': secret});
        } on DatabaseException catch (error) {
          failure = error;
        }
        expect(failure, isA<DatabaseException>());
        await log.record(
          CenterException(secret, cause: failure, stackTrace: trace),
          trace,
          operation: 'entry',
        );
        await log.record(
          FileSystemException(
            secret,
            '/Users/$secret',
            const OSError(secret, 13),
          ),
          trace,
          operation: 'ui.backup_page',
        );
        await log.record(
          FormatException(secret, secret, 10),
          trace,
          operation: secret,
        );
        await log.record(
          _SecretException(),
          trace,
          operation: 'flutter.framework',
        );
        final report = await exported();
        final content = jsonEncode(report);
        expect(content, isNot(contains(secret)));
        expect(content, isNot(contains('/Users/')));
        expect(content, isNot(contains('private.sqlite')));
        expect(content, isNot(contains('CREATE TABLE')));
        expect(content, isNot(contains('function')));
        final errors = report[1]['errors'] as List;
        expect(errors.map((error) => error['type']), [
          'CenterException',
          'DatabaseException',
        ]);
        expect(errors[1]['code'], isA<int>());
        expect((report[2]['errors'] as List).single['code'], 13);
        expect(report[3]['operation'], 'unknown_operation');
        expect((report.last['errors'] as List).single['type'], 'OtherError');
        expect((report[1]['frames'] as List).map((frame) => frame['file']), [
          'application/center_store.dart',
          'main.dart',
        ]);
      } finally {
        await db.close();
      }
    },
  );

  test(
    'Flutter and plugin types remain useful without plugin message, code or details',
    () async {
      await log.record(
        PlatformException(
          code: secret,
          message: secret,
          details: {'body': secret},
        ),
        trace,
        operation: 'flutter.platform',
      );
      await log.record(
        MissingPluginException(secret),
        trace,
        operation: 'ui.backup_page',
      );
      await log.record(
        FlutterError(secret),
        trace,
        operation: 'flutter.framework',
      );
      final report = await exported();
      expect(jsonEncode(report), isNot(contains(secret)));
      expect(
        report.skip(1).map((event) => (event['errors'] as List).single['type']),
        ['PlatformException', 'MissingPluginException', 'FlutterError'],
      );
      expect(
        report
            .skip(1)
            .every(
              (event) => !(event['errors'] as List).single.containsKey('code'),
            ),
        isTrue,
      );
    },
  );

  test(
    'identity dedup only merges immediate duplicate reports, not later independent incidents',
    () async {
      const failure = CenterException(secret);
      await Future.wait([
        log.record(failure, trace, operation: 'entry'),
        log.record(failure, trace, operation: 'ui.attendance_workspace'),
      ]);
      await log.record(CenterException(secret), trace, operation: 'entry');
      await log.record(secret, trace, operation: 'entry');
      await log.record(secret, trace, operation: 'entry');
      await Future<void>.delayed(const Duration(milliseconds: 1100));
      await log.record(failure, trace, operation: 'entry');
      final report = await exported();
      expect(report.first['eventCount'], 5);
      expect(report[1]['operation'], 'entry');
      expect(report.last['operation'], 'entry');
    },
  );

  test(
    'cause stack and line bounds reject huge text without leaking or stopping subsequent writes',
    () async {
      Object failure = FormatException(secret, secret);
      for (var i = 0; i < 20; i++) {
        failure = CenterException(secret, cause: failure);
      }
      final hugeTrace = StackTrace.fromString(
        List.filled(
          10000,
          '#0 $secret (package:massar_center/application/center_store.dart:42:7)',
        ).join('\n'),
      );
      await log.record(failure, hugeTrace, operation: 'entry');
      await log.record(StateError(secret), trace, operation: 'database.open');
      final report = await exported();
      expect(report.first['eventCount'], 2);
      expect(report[1]['errors'], hasLength(4));
      expect(report[1]['frames'], hasLength(16));
      expect(jsonEncode(report), isNot(contains(secret)));
      expect(log.writeFailure, isNull);
      final saved = await File(
        path.join(diagnostics.path, 'problems.jsonl'),
      ).readAsLines();
      expect(
        saved.every((line) => utf8.encode(line).length < 16 * 1024),
        isTrue,
      );
    },
  );

  test(
    'five one MiB managed files rotate and evict oldest while unrelated files remain',
    () async {
      await log.record(StateError(secret), trace, operation: 'entry');
      final active = File(path.join(diagnostics.path, 'problems.jsonl'));
      final row = await active.readAsString();
      final chunk = List.filled(
        (1024 * 1024) ~/ utf8.encode(row).length,
        row,
      ).join();
      final unrelated = File(path.join(diagnostics.path, 'keep-user-file.txt'));
      await unrelated.writeAsString(secret);
      for (var generation = 0; generation < 7; generation++) {
        await active.writeAsString(chunk);
        await log.record(
          StateError('$secret $generation'),
          trace,
          operation: 'entry',
        );
      }
      final managed = await diagnostics
          .list()
          .where((entry) => entry.path.endsWith('.jsonl'))
          .toList();
      expect(managed, hasLength(5));
      for (final file in managed.cast<File>()) {
        expect(await file.length(), lessThanOrEqualTo(1024 * 1024));
      }
      expect(await unrelated.readAsString(), secret);
      final report = await exported();
      expect(jsonEncode(report), isNot(contains(secret)));
      expect(report.first['eventCount'], greaterThan(1));
    },
  );

  test(
    'export sanitizes tampered managed rows, corrupt UTF8 and unknown extra fields',
    () async {
      await log.record(StateError(secret), trace, operation: 'entry');
      final active = File(path.join(diagnostics.path, 'problems.jsonl'));
      final saved =
          jsonDecode((await active.readAsLines()).single)
              as Map<String, dynamic>;
      final tampered = {
        ...saved,
        'message': secret,
        'build': '0123456789abcdef',
        'operation': secret,
        'errors': [
          {'type': secret, 'message': secret},
          {'type': 'FormatException', 'source': secret},
        ],
        'frames': [
          {
            'file': '/Users/$secret/private.dart',
            'frame': 0,
            'line': 1,
            'column': 2,
          },
          {
            'file': 'main.dart',
            'frame': 0,
            'line': 1,
            'column': 2,
            'function': secret,
          },
        ],
      };
      await active.writeAsBytes([
        ...utf8.encode(
          '${jsonEncode(tampered)}\n${jsonEncode({...tampered, 'build': secret})}\n$secret\n',
        ),
        0xff,
        0xfe,
        10,
      ], mode: FileMode.append);
      await File(
        path.join(diagnostics.path, 'foreign.txt'),
      ).writeAsString(secret);
      final report = await exported();
      expect(report.first['eventCount'], 3);
      expect(report.last.containsKey('build'), isFalse);
      expect(jsonEncode(report), isNot(contains(secret)));
      expect(report.last['operation'], 'unknown_operation');
      expect(report[2]['build'], '0123456789abcdef');
      expect(report.first['build'], 'development');
      expect((report.last['errors'] as List).first['type'], 'OtherError');
      expect((report.last['frames'] as List).single['file'], 'main.dart');
    },
  );

  test(
    'destination validation never overwrites existing files or symlink targets',
    () async {
      await log.startSession();
      final protected = File(path.join(sandbox.path, 'center.sqlite'));
      await protected.writeAsString(secret);
      final existing = File(path.join(sandbox.path, 'existing.txt'));
      await existing.writeAsString(secret);
      for (final target in [
        protected.path,
        existing.path,
        path.join(diagnostics.path, 'inside.txt'),
      ]) {
        await expectLater(log.exportTo(target), throwsStateError);
      }
      final linked = Link(path.join(sandbox.path, 'linked.txt'));
      await linked.create(protected.path);
      await expectLater(log.exportTo(linked.path), throwsStateError);
      final missingTarget = path.join(sandbox.path, 'never-create.sqlite');
      final dangling = Link(path.join(sandbox.path, 'dangling.txt'));
      await dangling.create(missingTarget);
      await expectLater(log.exportTo(dangling.path), throwsStateError);
      expect(await File(missingTarget).exists(), isFalse);
      expect(await protected.readAsString(), secret);
      expect(await existing.readAsString(), secret);
      expect(await linked.target(), protected.path);
      await exported();
    },
  );

  test(
    'empty diagnostic export retains metadata and invalid destination leaves no partial file',
    () async {
      final emptyTarget = path.join(sandbox.path, 'empty.txt');
      await log.exportTo(emptyTarget);
      final emptyRows = await File(emptyTarget).readAsLines();
      expect(emptyRows, hasLength(1));
      final header = jsonDecode(emptyRows.single) as Map<String, dynamic>;
      expect(header['kind'], 'export');
      expect(header['eventCount'], 0);
      expect(header['version'], AppBuildMetadata.version);
      await log.startSession();
      await expectLater(
        log.exportTo(path.join(sandbox.path, 'missing', 'report.txt')),
        throwsStateError,
      );
      expect(log.writeFailure, isNull);
      await exported();
      expect(
        (await sandbox.list().toList()).any(
          (entry) =>
              path.basename(entry.path).startsWith('.massar-diagnostics-'),
        ),
        isFalse,
      );
    },
  );

  test(
    'write failures stay nonthrowing, notify once and can recover after filesystem repair',
    () async {
      await File(diagnostics.path).writeAsString(secret);
      var notifications = 0;
      log.failure.addListener(() => notifications++);
      await log.startSession();
      await log.record(StateError(secret), trace, operation: 'entry');
      await log.flush();
      expect(log.writeFailure, isNotNull);
      expect(notifications, 1);
      await expectLater(
        log.exportTo(path.join(sandbox.path, 'failed.txt')),
        throwsStateError,
      );
      expect(await File(diagnostics.path).readAsString(), secret);
      await File(diagnostics.path).delete();
      await log.startSession();
      await log.record(StateError(secret), trace, operation: 'entry');
      expect(log.writeFailure, isNull);
      expect(notifications, 2);
      final report = await exported();
      expect(report.first['eventCount'], 2);
    },
  );

  test(
    'managed file symlinks cannot write or export another application file',
    () async {
      await diagnostics.create();
      final protected = File(path.join(sandbox.path, 'center.sqlite'));
      await protected.writeAsString(secret);
      await Link(
        path.join(diagnostics.path, 'problems.jsonl'),
      ).create(protected.path);
      await log.record(StateError(secret), trace, operation: 'entry');
      expect(log.writeFailure, isNotNull);
      expect(await protected.readAsString(), secret);
      await expectLater(
        log.exportTo(path.join(sandbox.path, 'report.txt')),
        throwsStateError,
      );
    },
  );
}
