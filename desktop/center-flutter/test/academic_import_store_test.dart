import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/academic_import_command.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup cairo, regular;
  late LessonSession session;
  late AcademicActivity exam;
  late List<Student> students;
  const password = 'synthetic-academic-import';

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('academic-import-');
    store = await CenterStore.open(directory: '${directory.path}/host');
    await store.setupAdmin('manager', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveCatalog(
      const CatalogEntry(name: 'القاهرة', kind: CatalogKind.center),
    );
    for (final center in store.catalogs.where(
      (c) => c.kind == CatalogKind.center,
    )) {
      await store.saveGroup(
        StudyGroup(
          name: center.name,
          centerId: center.id,
          subjectId: store.catalogs
              .firstWhere((c) => c.kind == CatalogKind.subject)
              .id,
          gradeId: store.catalogs
              .firstWhere((c) => c.kind == CatalogKind.grade)
              .id,
          sessionPrice: 6000,
        ),
      );
    }
    cairo = store.groupsForRegion(cairo: true).single;
    regular = store.groupsForRegion().single;
    for (var index = 0; index < 3; index++) {
      await store.saveStudent(
        Student(
          code: '00$index',
          name: 'Synthetic student $index',
          groupIds: index == 2 ? [regular.id] : [cairo.id, regular.id],
          createdAt: DateTime.now(),
        ),
      );
    }
    students = store.students;
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'Synthetic month',
        lessons: [const PreparedLesson(number: 1)],
      ),
    );
    session = await store.startPreparedLesson(
      groupId: cairo.id,
      preparedLessonId: month.lessons.single.id,
    );
    exam = await store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: month.lessons.single.id,
        kind: AcademicActivityKind.exam,
        name: 'Synthetic exam',
        maxScore: 10,
        createdAt: DateTime.now(),
      ),
    );
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  AcademicImportRow row(
    int index, {
    num score = 8.5,
    AcademicRecord? expected,
    AcademicImportSource source = const AcademicImportSource(),
  }) => AcademicImportRow(
    studentId: students[index].id,
    score: score,
    maxScore: 10,
    expected: expected,
    source: source,
  );
  AcademicImportCommand command(
    List<AcademicImportRow> rows, {
    String? groupId,
    String? sessionId,
    String? activityId,
    int maxScore = 10,
  }) => AcademicImportCommand(
    groupId: groupId ?? cairo.id,
    sessionId: sessionId ?? session.id,
    activityId: activityId ?? exam.id,
    maxScore: maxScore,
    rows: rows,
  );
  Future<String> durablePayload() async {
    final db = await databaseFactoryFfi.openDatabase(
      store.databasePath,
      options: OpenDatabaseOptions(readOnly: true, singleInstance: false),
    );
    try {
      return (await db.query('state')).single['payload'] as String;
    } finally {
      await db.close();
    }
  }

  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: '${directory.path}/host');
    await store.signIn('manager', password);
  }

  AcademicRecord grade({num score = 3, String notes = ''}) => AcademicRecord(
    studentId: students[0].id,
    sessionId: session.id,
    activityId: exam.id,
    score: score,
    maxScore: 10,
    notes: notes,
    updatedAt: DateTime.now(),
  );

  test(
    'Cairo batch commits once, preserves notes and fractional grades, and creates no money',
    () async {
      await store.saveAcademic(grade(notes: 'Existing reviewed note'));
      final expected = store.academics.single;
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      try {
        await db.execute(
          'CREATE TABLE import_write_count (count INTEGER NOT NULL)',
        );
        await db.insert('import_write_count', {'count': 0});
        await db.execute(
          'CREATE TRIGGER count_import_writes AFTER UPDATE ON state BEGIN UPDATE import_write_count SET count = count + 1; END',
        );
        final input = [
          row(
            0,
            expected: expected,
            source: const AcademicImportSource(
              studentName: 'Source\nStudent\u202e',
              examName: 'Source exam',
              sessionId: 'source-session',
              attemptId: 'attempt-1',
              version: 'v2',
              cairoDate: '2026-10-08 14:20 القاهرة',
            ),
          ),
          row(1, score: 0),
        ];
        final request = command(input);
        input.clear();
        await store.importAcademicGrades(
          AcademicImportCommand.fromJson(request.toJson()),
        );
        expect((await db.query('import_write_count')).single['count'], 1);
      } finally {
        await db.close();
      }
      await reopen();
      final saved = store.academics.firstWhere(
        (r) => r.studentId == students[0].id,
      );
      expect(saved.id, expected.id);
      expect(saved.score, 8.5);
      expect(saved.notes, startsWith('Existing reviewed note\n'));
      expect(saved.notes, contains('source-session'));
      expect(saved.notes, contains('attempt-1'));
      expect(saved.notes, contains('Source Student'));
      expect(saved.notes, isNot(contains('\u202e')));
      expect(
        store.academics.firstWhere((r) => r.studentId == students[1].id).score,
        0,
      );
      expect(store.attendances, hasLength(2));
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.centerFees, isEmpty);
      expect(
        store.audit.where((a) => a.action == 'academic_import'),
        hasLength(1),
      );
    },
  );

  // A valid first row must roll back even when a later row fails.
  for (final failure in [
    'range',
    'nonfinite',
    'duplicate',
    'membership',
    'max',
    'source',
  ]) {
    test(
      'invalid batch $failure leaves grades, attendance, audit and SQLite unchanged',
      () async {
        final invalid = switch (failure) {
          'range' => row(1, score: 10.5),
          'nonfinite' => row(1, score: double.nan),
          'duplicate' => row(0),
          'membership' => row(2),
          'max' => AcademicImportRow(
            studentId: students[1].id,
            score: 8,
            maxScore: 20,
            expected: null,
          ),
          _ => row(1, source: AcademicImportSource(attemptId: 'x' * 201)),
        };
        final before = await durablePayload();
        final auditCount = store.audit.length;
        await expectLater(
          store.importAcademicGrades(command([row(0), invalid])),
          throwsA(isA<CenterException>()),
        );
        expect(await durablePayload(), before);
        expect(store.academics, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.audit.length, auditCount);
      },
    );
  }

  for (final concurrent in ['new record', 'changed score', 'changed notes']) {
    test(
      'preview conflicts with $concurrent reject the entire batch',
      () async {
        AcademicRecord? expected;
        if (concurrent != 'new record') {
          await store.saveAcademic(grade());
          expected = store.academics.single;
        }
        await store.saveAcademic(
          grade(
            score: concurrent == 'changed score' ? 5 : 3,
            notes: concurrent == 'changed notes' ? 'Changed after preview' : '',
          ),
        );
        final before = await durablePayload();
        await expectLater(
          store.importAcademicGrades(
            command([row(1), row(0, expected: expected)]),
          ),
          throwsA(isA<CenterException>()),
        );
        expect(await durablePayload(), before);
        expect(store.academics, hasLength(1));
        expect(store.attendances, hasLength(1));
      },
    );
  }

  test(
    'signed-out import is denied and academic assistant may import without collection permission',
    () async {
      await store.saveStaff(
        name: 'assistant',
        password: password,
        role: StaffRole.assistant,
      );
      store.signOut();
      final before = await durablePayload();
      await expectLater(
        store.importAcademicGrades(command([row(0)])),
        throwsA(isA<CenterException>()),
      );
      expect(await durablePayload(), before);
      await store.signIn('assistant', password);
      expect(store.canCollect, isFalse);
      await store.importAcademicGrades(command([row(0)]));
      expect(store.academics.single.score, 8.5);
      expect(store.audit.last.staffId, store.currentUser!.id);
    },
  );

  test('the same source attempt cannot be assigned to two students', () async {
    const source = AcademicImportSource(
      sessionId: 'source-session',
      attemptId: 'attempt-1',
      version: 'v1',
    );
    final before = await durablePayload();
    await expectLater(
      store.importAcademicGrades(
        command([row(0, source: source), row(1, source: source)]),
      ),
      throwsA(isA<CenterException>()),
    );
    expect(await durablePayload(), before);
    expect(store.academics, isEmpty);
    expect(store.attendances, isEmpty);
  });

  test(
    'unknown source placeholders do not become duplicate attempt identifiers',
    () async {
      const unknown = AcademicImportSource(
        sessionId: '—',
        attemptId: '-',
        version: '–',
        cairoDate: '-',
      );
      await store.importAcademicGrades(
        command([row(0, source: unknown), row(1, source: unknown)]),
      );
      expect(store.academics, hasLength(2));
      expect(
        store.academics.map((record) => record.notes),
        everyElement(contains('بيانات المصدر غير متوفرة')),
      );
    },
  );

  test(
    'suspended Cairo students cannot gain attendance but existing presence may be graded',
    () async {
      await store.suspendStudent(
        studentId: students[0].id,
        reason: 'Synthetic suspension',
      );
      final before = await durablePayload();
      await expectLater(
        store.importAcademicGrades(command([row(0)])),
        throwsA(isA<CenterException>()),
      );
      expect(await durablePayload(), before);
      expect(store.attendances, isEmpty);

      await store.importAcademicGrades(command([row(1, score: 4)]));
      final existing = store.academics.single;
      final attendance = store.attendances.single.toJson();
      await store.suspendStudent(
        studentId: students[1].id,
        reason: 'Synthetic later suspension',
      );
      await store.importAcademicGrades(
        command([row(1, score: 6.5, expected: existing)]),
      );
      expect(store.academics.single.score, 6.5);
      expect(store.attendances.single.toJson(), attendance);
    },
  );

  test(
    'selected group, named exam kind, and changed maximum are authoritative',
    () async {
      final homework = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          kind: AcademicActivityKind.homework,
          name: 'Synthetic homework',
          createdAt: DateTime.now(),
        ),
      );
      final before = await durablePayload();
      for (final request in [
        command([row(0)], groupId: regular.id),
        command([row(0)], activityId: homework.id),
        command([row(0)], maxScore: 20),
      ]) {
        await expectLater(
          store.importAcademicGrades(request),
          throwsA(isA<CenterException>()),
        );
        expect(await durablePayload(), before);
      }
    },
  );

  test('unstarted and canceled sessions cannot accept imports', () async {
    await store.saveSession(
      LessonSession(
        preparedLessonId: session.preparedLessonId,
        groupId: regular.id,
        number: 1,
        monthNumber: session.monthNumber,
        startsAt: DateTime.now(),
        createdAt: DateTime.now(),
      ),
    );
    final unstarted = store.sessions.firstWhere((s) => s.groupId == regular.id);
    expect(store.sessionHasStarted(unstarted.id), isFalse);
    await expectLater(
      store.importAcademicGrades(
        command([row(0)], groupId: regular.id, sessionId: unstarted.id),
      ),
      throwsA(isA<CenterException>()),
    );
    await store.cancelSession(unstarted.id);
    await expectLater(
      store.importAcademicGrades(
        command([row(0)], groupId: regular.id, sessionId: unstarted.id),
      ),
      throwsA(isA<CenterException>()),
    );
    expect(store.academics, isEmpty);
  });

  test(
    'an imported exam with unknown maximum must be resolved before batch grading',
    () async {
      final backupPath = await store.createBackup(
        destination: '${directory.path}/synthetic-backup.json',
      );
      final backup =
          jsonDecode(await File(backupPath).readAsString())
              as Map<String, dynamic>;
      final state = backup['data'] as Map<String, dynamic>;
      state['installationSeedId'] = 'synthetic-historical-source';
      for (final activity in state['academicActivities'] as List) {
        if (activity['id'] == exam.id) activity['maxScoreKnown'] = false;
      }
      final imported = File('${directory.path}/synthetic-unknown-maximum.json');
      await imported.writeAsString(jsonEncode(backup));
      await store.restoreBackup(imported.path);
      final before = await durablePayload();
      await expectLater(
        store.importAcademicGrades(command([row(0)])),
        throwsA(isA<CenterException>()),
      );
      expect(await durablePayload(), before);
      expect(store.academics, isEmpty);
      expect(store.attendances, isEmpty);
    },
  );

  test(
    'regular group requires actual presence and import adds no attendance or money',
    () async {
      final normalSession = await store.startPreparedLesson(
        groupId: regular.id,
        preparedLessonId: session.preparedLessonId!,
      );
      await store.recordAttendance(
        EntryRequest(
          studentId: students[0].id,
          sessionId: normalSession.id,
          mode: EntryMode.single,
        ),
      );
      final before = await durablePayload();
      await expectLater(
        store.importAcademicGrades(
          command(
            [row(0), row(1)],
            groupId: regular.id,
            sessionId: normalSession.id,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(await durablePayload(), before);
      final presenceCount = store.attendances.length;
      final payments = store.payments.map((p) => p.toJson()).toList();
      await store.importAcademicGrades(
        command([row(0)], groupId: regular.id, sessionId: normalSession.id),
      );
      expect(store.academics.single.score, 8.5);
      expect(store.attendances.length, presenceCount);
      expect(store.payments.map((p) => p.toJson()), payments);
    },
  );
}
