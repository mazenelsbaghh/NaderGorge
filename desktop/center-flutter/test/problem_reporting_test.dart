import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/problem_log.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const privatePassword = 'PRIVATE-password-log-fixture-2026';
  const privateName = 'PRIVATE-student-name-fixture';
  const privateNotes = 'PRIVATE-student-note-phone-01098765432';
  const privateError = 'PRIVATE-sql-error-state-payload-fixture';
  late Directory directory;
  late CenterStore store;
  late ProblemLog log;
  ProblemLog? previousLog;

  setUp(() async {
    previousLog = ProblemLog.current;
    directory = await Directory.systemTemp.createTemp('massar-problem-bridge-');
    log = ProblemLog(Directory('${directory.path}/diagnostics'));
    ProblemLog.current = log;
    await log.startSession();
    store = await CenterStore.open(directory: '${directory.path}/center');
    await store.setupAdmin('fixture-manager', privatePassword);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'fixture-group',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 10000,
        packagePrice: 40000,
      ),
    );
    await store.saveStudent(
      Student(
        code: 'PRIVATE-card-code-fixture',
        name: privateName,
        notes: privateNotes,
        discountPercent: 25,
        groupIds: [store.groups.single.id],
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    await store.saveSession(
      LessonSession(
        groupId: store.groups.single.id,
        monthNumber: store.studyMonths.first.number,
        preparedLessonId: store.studyMonths.first.lessons.first.id,
        number: 1,
        startsAt: DateTime.now().add(const Duration(minutes: 1)),
        createdAt: DateTime.now(),
      ),
    );
  });

  tearDown(() async {
    await store.close();
    await log.flush();
    ProblemLog.current = previousLog;
    await directory.delete(recursive: true);
  });

  EntryRequest packageEntry() => EntryRequest(
    studentId: store.students.single.id,
    sessionId: store.sessions.single.id,
    mode: EntryMode.package,
  );

  Future<void> rejectWrites() async {
    final connection = await databaseFactoryFfi.openDatabase(
      store.databasePath,
      options: OpenDatabaseOptions(singleInstance: false),
    );
    try {
      await connection.execute(
        "CREATE TRIGGER reject_problem_entry BEFORE INSERT ON state_records "
        "BEGIN SELECT RAISE(ABORT, '$privateError'); END",
      );
    } finally {
      await connection.close();
    }
  }

  Future<CenterException> rejectedEntry() async {
    try {
      await store.collectAndAttend(packageEntry());
    } on CenterException catch (error) {
      return error;
    }
    fail('SQLite rejection must still be returned to the caller.');
  }

  Future<String> exportedDiagnostics() async {
    // Export itself must await records queued by the unawaited reporting bridge.
    final path = await log.exportTo('${directory.path}/diagnostics-export.txt');
    return File(path).readAsString();
  }

  void expectPrivateDataAbsent(String exported) {
    for (final secret in [
      privatePassword,
      privateName,
      privateNotes,
      privateError,
      'PRIVATE-card-code-fixture',
      directory.path,
      'credentials',
      'pbkdf2-sha256-120000',
    ]) {
      expect(exported, isNot(contains(secret)));
    }
  }

  test(
    'real failed SQLite entry and authentication produce safe diagnostic events without changing money',
    () async {
      await rejectWrites();
      final auditCount = store.audit.length;
      final actor = store.currentUser!.id;
      final failure = await rejectedEntry();
      expect(failure.cause, isA<DatabaseException>());
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.audit, hasLength(auditCount));
      await expectLater(
        store.signIn('PRIVATE-unknown-user-fixture', privatePassword),
        throwsA(isA<CenterException>()),
      );
      expect(store.currentUser!.id, actor);
      final exported = await exportedDiagnostics();
      expect(exported, contains('"entry"'));
      expect(exported, contains('"auth.sign_in"'));
      expect(exported, isNot(contains('PRIVATE-unknown-user-fixture')));
      expectPrivateDataAbsent(exported);
      // Remove the injected disk-write fault before testing a healthy reopen.
      final recovered = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      await recovered.execute('DROP TRIGGER reject_problem_entry');
      await recovered.close();
      await store.close();
      store = await CenterStore.open(directory: '${directory.path}/center');
      await store.signIn('fixture-manager', privatePassword);
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.audit, hasLength(auditCount));
    },
  );

  test(
    'failed logger storage never replaces the SQLite exception or financial rollback',
    () async {
      final blocker = File('${directory.path}/not-a-directory');
      await blocker.writeAsString('fixture');
      final unavailable = ProblemLog(Directory('${blocker.path}/diagnostics'));
      ProblemLog.current = unavailable;
      await unavailable.startSession();
      await rejectWrites();
      final auditCount = store.audit.length;
      final failure = await rejectedEntry();
      await unavailable.flush();
      expect(failure.cause, isA<DatabaseException>());
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.audit, hasLength(auditCount));
      ProblemLog.current = log;
    },
  );

  test(
    'database startup failure is reported without exposing the unreadable file payload',
    () async {
      final corrupt = Directory('${directory.path}/unreadable-center');
      await corrupt.create();
      await File('${corrupt.path}/center.sqlite').writeAsString(privateNotes);
      await expectLater(
        CenterStore.open(directory: corrupt.path),
        throwsA(isA<CenterException>()),
      );
      final exported = await exportedDiagnostics();
      expect(exported, contains('"database.open"'));
      expectPrivateDataAbsent(exported);
      expect(store.payments, isEmpty);
    },
  );

  test(
    'Flutter and platform hooks retain existing handlers and safely record their real error types',
    () async {
      final originalFlutter = FlutterError.onError;
      final originalPlatform = PlatformDispatcher.instance.onError;
      FlutterErrorDetails? presented;
      Object? platformError;
      FlutterError.onError = (details) => presented = details;
      PlatformDispatcher.instance.onError = (error, _) {
        platformError = error;
        return true;
      };
      final restore = installProblemHandlers();
      try {
        final details = FlutterErrorDetails(
          exception: FlutterError(privateNotes),
          stack: StackTrace.current,
          informationCollector: () sync* {
            yield DiagnosticsProperty<String>('private', privatePassword);
          },
        );
        FlutterError.onError!(details);
        expect(presented, same(details));
        final error = StateError(privateError);
        expect(
          PlatformDispatcher.instance.onError!(error, StackTrace.current),
          isTrue,
        );
        expect(platformError, same(error));
        final exported = await exportedDiagnostics();
        expect(exported, contains('"flutter.framework"'));
        expect(exported, contains('"flutter.platform"'));
        expectPrivateDataAbsent(exported);
      } finally {
        restore();
        FlutterError.onError = originalFlutter;
        PlatformDispatcher.instance.onError = originalPlatform;
      }
    },
  );
}
