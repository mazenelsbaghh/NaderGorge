import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'academic-activity-pass';
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  late LessonSession session;
  var backupNumber = 0;

  setUp(() async {
    backupNumber = 0;
    directory = await Directory.systemTemp.createTemp(
      'massar-academic-activities-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        name: 'طالب',
        code: 'A1',
        groupIds: [group.id],
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    student = store.students.single;
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: DateTime.now().add(const Duration(hours: 1)),
        createdAt: DateTime.now(),
      ),
    );
    session = store.sessions.single;
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });
  AcademicActivity definition(
    String name, {
    AcademicActivityKind kind = AcademicActivityKind.exam,
    int maxScore = 20,
    String? sessionId,
  }) => AcademicActivity(
    sessionId: sessionId ?? session.id,
    kind: kind,
    name: name,
    maxScore: maxScore,
    createdAt: DateTime.now(),
  );
  AcademicRecord result({
    AcademicActivity? activity,
    int? score,
    bool absent = false,
    HomeworkStatus homework = HomeworkStatus.notReviewed,
    int? maxScore,
    String? id,
    String? studentId,
    String? sessionId,
  }) => AcademicRecord(
    id: id ?? '',
    studentId: studentId ?? student.id,
    sessionId: sessionId ?? session.id,
    activityId: activity?.id,
    score: score,
    examAbsent: absent,
    homework: homework,
    maxScore: maxScore ?? activity?.maxScore ?? 10,
    updatedAt: DateTime.now(),
  );
  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير', password);
  }

  Future<File> editedBackup(void Function(Map<String, dynamic>) edit) async {
    final backup = await store.createBackup(
      destination: '${directory.path}/backup-${backupNumber++}.json',
    );
    final json =
        jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
    edit(json['data'] as Map<String, dynamic>);
    final file = File('${directory.path}/edited-${backupNumber++}.json');
    await file.writeAsString(jsonEncode(json));
    return file;
  }

  test(
    'homework exceptions fill actual attendees only and preserve prior states across restart',
    () async {
      await store.closeSession(session.id);
      final month = await store.saveStudyMonth(
        StudyMonth(
          name: 'شهر الرصد',
          lessons: [const PreparedLesson(number: 1)],
        ),
      );
      session = await store.startPreparedLesson(
        groupId: group.id,
        preparedLessonId: month.lessons.single.id,
      );
      final homework = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: month.lessons.single.id,
          name: 'واجب',
          kind: AcademicActivityKind.homework,
          createdAt: DateTime.now(),
        ),
      );
      final exam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: month.lessons.single.id,
          name: 'امتحان',
          kind: AcademicActivityKind.exam,
          maxScore: 20,
          createdAt: DateTime.now(),
        ),
      );
      for (final code in ['B2', 'C3']) {
        await store.saveStudent(
          Student(
            name: code,
            code: code,
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
      }
      final second = store.students.firstWhere((s) => s.code == 'B2');
      final absent = store.students.firstWhere((s) => s.code == 'C3');
      for (final id in [student.id, second.id]) {
        await store.recordAttendance(
          EntryRequest(
            studentId: id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
      }
      await store.saveAcademic(result(activity: exam, score: 9));
      await store.recordHomeworkExceptions(
        sessionId: session.id,
        activityId: homework.id,
        missingStudentId: student.id,
      );
      final homeworkRows = store.academics
          .where((r) => r.activityId == homework.id)
          .toList();
      expect(homeworkRows, hasLength(2));
      expect(
        homeworkRows.firstWhere((r) => r.studentId == student.id).homework,
        HomeworkStatus.missing,
      );
      expect(
        homeworkRows.firstWhere((r) => r.studentId == second.id).homework,
        HomeworkStatus.complete,
      );
      await store.saveStaff(
        name: 'استقبال',
        password: password,
        role: StaffRole.cashier,
      );
      await store.signIn('استقبال', password);
      await store.commandLan(
        deviceId: 'test-client',
        staffId: store.currentUser!.id,
        request: {
          'requestId': 'a4f7d2e1-1182-4e11-9211-872690073ed1',
          'operation': 'recordHomeworkExceptions',
          'arguments': {'sessionId': session.id, 'activityId': homework.id},
        },
      );
      expect(
        store.academics
            .firstWhere(
              (r) => r.activityId == homework.id && r.studentId == student.id,
            )
            .homework,
        HomeworkStatus.missing,
      );
      await expectLater(
        store.recordHomeworkExceptions(
          sessionId: session.id,
          activityId: homework.id,
          missingStudentId: absent.id,
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.recordHomeworkExceptions(
          sessionId: session.id,
          activityId: exam.id,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(
        store.academics.where((r) => r.activityId == exam.id).single.score,
        9,
      );
      expect(store.academics.any((r) => r.studentId == absent.id), isFalse);
      expect(store.payments, isEmpty);
      await reopen();
      expect(
        store.academics.where((r) => r.activityId == homework.id),
        hasLength(2),
      );
      expect(
        store.academics
            .firstWhere(
              (r) => r.activityId == homework.id && r.studentId == student.id,
            )
            .homework,
        HomeworkStatus.missing,
      );
    },
  );

  test(
    'multiple named exams and homework results remain independent and never copy or replace legacy combined results',
    () async {
      await store.saveAcademic(
        result(score: 8, homework: HomeworkStatus.complete),
      );
      final legacy = store.academics.single.toJson();
      final first = await store.saveAcademicActivity(
        definition('امتحان الفصل الأول'),
      );
      final second = await store.saveAcademicActivity(
        definition('امتحان الفصل الثاني'),
      );
      final homeworkOne = await store.saveAcademicActivity(
        definition('واجب الدرس', kind: AcademicActivityKind.homework),
      );
      final homeworkTwo = await store.saveAcademicActivity(
        definition('واجب المراجعة', kind: AcademicActivityKind.homework),
      );
      expect(store.academics.single.toJson(), legacy);
      final definitionsBackup = await store.createBackup(
        destination: '${directory.path}/definition-only.json',
      );
      final definitionData =
          (jsonDecode(await File(definitionsBackup).readAsString())
                  as Map)['data']
              as Map;
      expect(definitionData['schemaVersion'], 3);
      await store.saveAcademic(result(activity: first, score: 0));
      await store.saveAcademic(result(activity: second));
      await store.saveAcademic(
        result(activity: homeworkOne, homework: HomeworkStatus.complete),
      );
      await store.saveAcademic(
        result(activity: homeworkTwo, homework: HomeworkStatus.missing),
      );
      expect(store.academics, hasLength(5));
      final zero = store.academics.singleWhere(
        (row) => row.activityId == first.id,
      );
      expect(zero.score, 0);
      expect(zero.examAbsent, isFalse);
      expect(
        store.academics.singleWhere((row) => row.activityId == second.id).score,
        isNull,
      );
      final untouched = store.academics
          .where((row) => row.activityId != first.id)
          .map((row) => row.toJson())
          .toList();
      await store.saveAcademic(result(activity: first, absent: true));
      final absent = store.academics.singleWhere(
        (row) => row.activityId == first.id,
      );
      expect(absent.id, zero.id);
      expect(absent.score, isNull);
      expect(absent.examAbsent, isTrue);
      expect(
        store.academics
            .where((row) => row.activityId != first.id)
            .map((row) => row.toJson())
            .toList(),
        untouched,
      );
      await store.saveAcademic(result(activity: first));
      expect(
        store.academics
            .singleWhere((row) => row.activityId == first.id)
            .examAbsent,
        isFalse,
      );
      final expected = store.academics.map((row) => row.toJson()).toList();
      await reopen();
      expect(store.academics.map((row) => row.toJson()).toList(), expected);
      expect(store.academics.first.toJson(), legacy);
      expect(store.academicActivities, hasLength(4));
      expect(() => store.academicActivities.clear(), throwsUnsupportedError);
    },
  );

  test(
    'definition name uniqueness is serialized per kind and session, and prevents editing or canceling its lesson',
    () async {
      final attempts = await Future.wait(
        [' Test ', 'test'].map((name) async {
          try {
            return await store.saveAcademicActivity(definition(name));
          } on CenterException {
            return null;
          }
        }),
      );
      expect(attempts.whereType<AcademicActivity>(), hasLength(1));
      expect(store.academicActivities, hasLength(1));
      final homework = await store.saveAcademicActivity(
        definition('TEST', kind: AcademicActivityKind.homework, maxScore: -9),
      );
      expect(homework.maxScore, 10);
      expect(store.academicActivities, hasLength(2));
      await expectLater(
        store.saveSession(session.copyWith(number: 9)),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.cancelSession(session.id),
        throwsA(isA<CenterException>()),
      );
      expect(store.sessions.single.number, 1);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: DateTime.now().add(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      final other = store.sessions.last;
      await store.saveAcademicActivity(definition('test', sessionId: other.id));
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 3,
          startsAt: DateTime.now().add(const Duration(hours: 3)),
          createdAt: DateTime.now(),
        ),
      );
      final canceled = store.sessions.last;
      await store.cancelSession(canceled.id);
      await expectLater(
        store.saveAcademicActivity(definition('ملغاة', sessionId: canceled.id)),
        throwsA(isA<CenterException>()),
      );
    },
  );

  test(
    'exam maximum is editable before results but locked after even an unreviewed result; identity and createdAt remain stable',
    () async {
      final exam = await store.saveAcademicActivity(
        definition(' امتحان ', maxScore: 10),
      );
      expect(exam.name, 'امتحان');
      final changed = await store.saveAcademicActivity(
        exam.copyWith(
          name: 'اسم محدث',
          maxScore: 30,
          createdAt: DateTime(2000),
        ),
      );
      expect(changed.id, exam.id);
      expect(changed.createdAt, exam.createdAt);
      await store.saveAcademic(result(activity: changed));
      final audits = store.audit.length;
      for (final invalid in [
        changed.copyWith(maxScore: 40),
        changed.copyWith(kind: AcademicActivityKind.homework),
        changed.copyWith(name: ' '),
        changed.copyWith(name: 'X' * 121),
      ]) {
        await expectLater(
          store.saveAcademicActivity(invalid),
          throwsA(isA<CenterException>()),
        );
      }
      expect(store.audit.length, audits);
      expect(store.academicActivities.single.toJson(), changed.toJson());
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: DateTime.now().add(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      await expectLater(
        store.saveAcademicActivity(
          changed.copyWith(sessionId: store.sessions.last.id),
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.saveAcademicActivity(definition('درجة غير صالحة', maxScore: 0)),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.saveAcademicActivity(changed.copyWith(id: 'missing')),
        throwsA(isA<CenterException>()),
      );
    },
  );

  test(
    'named result validates activity, session, kind, score range and record identity without partial writes',
    () async {
      final exam = await store.saveAcademicActivity(definition('امتحان'));
      final homework = await store.saveAcademicActivity(
        definition('واجب', kind: AcademicActivityKind.homework),
      );
      final invalid = [
        result(activity: exam, maxScore: 10, score: 5),
        result(activity: exam, score: -1),
        result(activity: exam, score: 21),
        result(activity: exam, score: 0, absent: true),
        result(activity: exam, homework: HomeworkStatus.complete),
        result(activity: homework, score: 0),
        result(activity: homework, absent: true),
        result(activity: exam.copyWith(id: 'missing-activity'), score: 5),
      ];
      final audits = store.audit.length;
      for (final row in invalid) {
        await expectLater(
          store.saveAcademic(row),
          throwsA(isA<CenterException>()),
        );
      }
      expect(store.audit.length, audits);
      expect(store.academics, isEmpty);
      await store.saveAcademic(result(activity: exam, score: 5));
      final examResult = store.academics.single;
      await expectLater(
        store.saveAcademic(
          result(
            activity: homework,
            id: examResult.id,
            homework: HomeworkStatus.complete,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: DateTime.now().add(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      final otherActivity = await store.saveAcademicActivity(
        definition('امتحان آخر', sessionId: store.sessions.last.id),
      );
      await expectLater(
        store.saveAcademic(result(activity: otherActivity)),
        throwsA(isA<CenterException>()),
      );
      expect(store.academics.single.toJson(), examResult.toJson());
    },
  );

  for (final role in [
    StaffRole.admin,
    StaffRole.assistant,
    StaffRole.cashier,
  ]) {
    test(
      '${role.name} academic mutation permission is enforced at command time',
      () async {
        await store.closeSession(session.id);
        final month = await store.saveStudyMonth(
          StudyMonth(
            name: 'شهر الرصد',
            lessons: [const PreparedLesson(number: 1)],
          ),
        );
        session = await store.startPreparedLesson(
          groupId: group.id,
          preparedLessonId: month.lessons.single.id,
        );
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.saveStaff(name: role.name, password: password, role: role);
        await store.signIn(role.name, password);
        final activity = await store.saveAcademicActivity(
          AcademicActivity(
            preparedLessonId: month.lessons.single.id,
            name: 'مسموح',
            kind: AcademicActivityKind.exam,
            maxScore: 20,
            createdAt: DateTime.now(),
          ),
        );
        await store.saveAcademic(result(activity: activity, score: 10));
        expect(store.academics.single.activityId, activity.id);
        final homework = await store.saveAcademicActivity(
          AcademicActivity(
            preparedLessonId: month.lessons.single.id,
            name: 'واجب',
            kind: AcademicActivityKind.homework,
            createdAt: DateTime.now(),
          ),
        );
        await store.saveAcademic(
          result(activity: homework, homework: HomeworkStatus.complete),
        );
        expect(
          store.academics.any(
            (entry) =>
                entry.activityId == homework.id &&
                entry.homework == HomeworkStatus.complete,
          ),
          isTrue,
        );
        store.signOut();
        await expectLater(
          store.saveAcademicActivity(definition('بعد الخروج')),
          throwsA(isA<CenterException>()),
        );
        await expectLater(
          store.saveAcademic(result(score: 1)),
          throwsA(isA<CenterException>()),
        );
      },
    );
  }

  test(
    'actual historical academics allow named results after leaving the group without copying legacy results or widening legacy eligibility',
    () async {
      await store.saveAcademic(
        result(score: 8, homework: HomeworkStatus.complete),
      );
      final legacy = store.academics.single;
      final exam = await store.saveAcademicActivity(definition('تاريخي'));
      await store.saveGroup(group.copyWith(id: '', name: 'مجموعة أخرى'));
      final other = store.groups.last;
      await store.saveStudent(student.copyWith(groupIds: [other.id]));
      await store.saveAcademic(result(activity: exam, score: 10));
      expect(store.academics.first.toJson(), legacy.toJson());
      await store.saveAcademic(result(activity: exam, score: 11));
      expect(store.academics.last.score, 11);
      await expectLater(
        store.saveAcademic(result(score: 9)),
        throwsA(isA<CenterException>()),
      );
      await store.saveStudent(
        Student(
          name: 'بلا تاريخ في الحصة',
          code: 'A2',
          groupIds: [other.id],
          createdAt: DateTime.now(),
        ),
      );
      await expectLater(
        store.saveAcademic(
          result(activity: exam, studentId: store.students.last.id, score: 5),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.academics, hasLength(2));
    },
  );

  test(
    'legacy schema1 and schema2 backups without definitions remain combined and are never cloned into new activities',
    () async {
      await store.saveAcademic(
        result(score: 0, homework: HomeworkStatus.missing),
      );
      final legacy = store.academics.single.toJson();
      for (final schema in [1, 2]) {
        final backup = await editedBackup((data) {
          expect(data['schemaVersion'], 2);
          data['schemaVersion'] = schema;
          data.remove('academicActivities');
          if (schema == 1) {
            for (final key in [
              'reviews',
              'closings',
              'paymentChecks',
              'corrections',
              'refunds',
              'cardPayments',
              'cardReceipts',
            ]) {
              data.remove(key);
            }
          }
          ((data['academics'] as List).single as Map).remove('activityId');
        });
        await store.restoreBackup(backup.path);
        await store.signIn('مدير', password);
        expect(store.academicActivities, isEmpty);
        expect(store.academics.single.toJson(), legacy);
        expect(store.academics.single.activityId, isNull);
      }
      await store.saveAcademicActivity(definition('جديد'));
      expect(store.academics.single.toJson(), legacy);
      await reopen();
      expect(store.academics.single.activityId, isNull);
      expect(store.academicActivities, hasLength(1));
    },
  );

  test(
    'academic definitions and results remain independent from immutable financial closings and survive backup plus reopen',
    () async {
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final originalClosing = store.closings.single.toJson();
      final exam = await store.saveAcademicActivity(definition('بعد الحصة'));
      await store.saveAcademic(result(activity: exam, score: 20));
      expect(store.closings.single.toJson(), originalClosing);
      await store.reopenFinancialClosing(
        closingId: store.closings.single.id,
        reason: 'مراجعة النقدية',
      );
      await store.saveAcademic(result(activity: exam, score: 0));
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final backup = await store.createBackup(
        destination: '${directory.path}/named-activities.json',
      );
      final activityJson = store.academicActivities.single.toJson();
      final resultJson = store.academics.single.toJson();
      await store.restoreBackup(backup);
      await store.signIn('مدير', password);
      await reopen();
      expect(store.academicActivities.single.toJson(), activityJson);
      expect(store.academics.single.toJson(), resultJson);
      expect(store.allClosings.first.toJson(), originalClosing);
    },
  );

  test(
    'forged backups reject duplicate definitions/results and cross-kind or cross-session references before writing SQLite',
    () async {
      final exam = await store.saveAcademicActivity(definition('امتحان'));
      await store.saveAcademic(result(activity: exam, score: 10));
      final originalActivity = store.academicActivities.single.toJson();
      final originalResult = store.academics.single.toJson();
      final mutations = <void Function(Map<String, dynamic>)>[
        (data) => (data['academicActivities'] as List).add({
          ...originalActivity,
          'id': 'duplicate-name',
        }),
        (data) =>
            ((data['academicActivities'] as List).single as Map)['sessionId'] =
                'missing-session',
        (data) =>
            ((data['academicActivities'] as List).single as Map)['maxScore'] =
                0,
        (data) => ((data['academicActivities'] as List).single as Map)['kind'] =
            'homework',
        (data) => ((data['academics'] as List).single as Map)['activityId'] =
            'missing-activity',
        (data) => ((data['academics'] as List).single as Map)['maxScore'] = 30,
        (data) => ((data['academics'] as List).single as Map)['homework'] =
            'complete',
        (data) => (data['academics'] as List).add({
          ...originalResult,
          'id': 'duplicate-result',
        }),
        (data) =>
            ((data['sessions'] as List).single as Map)['status'] = 'canceled',
      ];
      for (final mutation in mutations) {
        final backup = await editedBackup(mutation);
        await expectLater(
          store.restoreBackup(backup.path),
          throwsA(isA<CenterException>()),
        );
        expect(store.academicActivities.single.toJson(), originalActivity);
        expect(store.academics.single.toJson(), originalResult);
      }
      await reopen();
      expect(store.academics.single.toJson(), originalResult);
    },
  );

  test(
    'failed SQLite writes rollback definition creation and results including their audit, and successful retry returns the persisted id',
    () async {
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      Future<void> block() => db.execute(
        "CREATE TRIGGER reject_activity BEFORE UPDATE ON state BEGIN SELECT RAISE(ABORT, 'blocked'); END",
      );
      Future<void> unblock() => db.execute('DROP TRIGGER reject_activity');
      try {
        final initialAudits = store.audit.length;
        await block();
        await expectLater(
          store.saveAcademicActivity(definition('لم يحفظ')),
          throwsA(
            isA<CenterException>().having(
              (error) => error.cause,
              'SQLite cause',
              isNotNull,
            ),
          ),
        );
        expect(store.academicActivities, isEmpty);
        expect(store.audit.length, initialAudits);
        await unblock();
        final activity = await store.saveAcademicActivity(definition('محفوظ'));
        expect(store.academicActivities.single.id, activity.id);
        final audits = store.audit.length;
        await block();
        await expectLater(
          store.saveAcademic(result(activity: activity, score: 10)),
          throwsA(isA<CenterException>()),
        );
        expect(store.academics, isEmpty);
        expect(store.audit.length, audits);
        await unblock();
        await store.saveAcademic(result(activity: activity, score: 10));
      } finally {
        await db.close();
      }
      await reopen();
      expect(store.academicActivities, hasLength(1));
      expect(
        store.academics.single.activityId,
        store.academicActivities.single.id,
      );
      expect(store.academics.single.score, 10);
    },
  );
}
