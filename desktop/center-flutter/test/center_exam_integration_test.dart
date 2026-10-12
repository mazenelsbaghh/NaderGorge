import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/center_exam_integration.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group, other, cairo;
  late LessonSession session;
  late AcademicActivity exam;
  late List<Student> students;
  const password = 'synthetic-exam-integration';

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('center-exam-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('manager', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveCatalog(
      const CatalogEntry(name: 'القاهرة', kind: CatalogKind.center),
    );
    for (final name in ['regular', 'other', 'cairo']) {
      await store.saveGroup(
        StudyGroup(
          name: name,
          centerId: store.catalogs
              .firstWhere(
                (entry) =>
                    entry.kind == CatalogKind.center &&
                    entry.isCairo == (name == 'cairo'),
              )
              .id,
          subjectId: store.catalogs
              .firstWhere((entry) => entry.kind == CatalogKind.subject)
              .id,
          gradeId: store.catalogs
              .firstWhere((entry) => entry.kind == CatalogKind.grade)
              .id,
          sessionPrice: 6000,
        ),
      );
    }
    group = store.groups.firstWhere((entry) => entry.name == 'regular');
    other = store.groups.firstWhere((entry) => entry.name == 'other');
    cairo = store.groups.firstWhere((entry) => entry.name == 'cairo');
    for (var index = 0; index < 3; index++) {
      await store.saveStudent(
        Student(
          code: '0010$index',
          name: 'Synthetic student $index',
          phone: '0100000000$index',
          guardianPhone: '0110000000$index',
          groupIds: index == 2 ? [other.id] : [group.id, cairo.id],
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
      groupId: group.id,
      preparedLessonId: month.lessons.single.id,
    );
    exam = await store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: session.preparedLessonId,
        name: 'Synthetic exam',
        kind: AcademicActivityKind.exam,
        maxScore: 10,
        createdAt: DateTime.now(),
      ),
    );
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  IntegratedExamResult result({
    int student = 0,
    num score = 0,
    int version = 1,
    String? attemptId,
    String? sessionId,
    String? activityId,
    String examId = 'engine-exam',
    num maxScore = 10,
    String sourceId = 'engine-installation',
    String status = 'final',
  }) => IntegratedExamResult(
    sourceId: sourceId,
    examId: examId,
    attemptId: attemptId ?? 'attempt-$student',
    studentId: students[student].id,
    sessionId: sessionId ?? session.id,
    activityId: activityId ?? exam.id,
    version: version,
    score: score,
    maxScore: maxScore,
    status: status,
  );
  Future<void> attend(int index, {bool makeup = false}) =>
      store.recordAttendance(
        EntryRequest(
          studentId: students[index].id,
          sessionId: session.id,
          mode: EntryMode.single,
          makeupSourceGroupId: makeup ? other.id : null,
        ),
      );
  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('manager', password);
  }

  IntegratedExamProgress progress({String? activityId}) =>
      store.integratedExamProgress(
        groupId: session.groupId,
        sessionId: session.id,
        activityId: activityId ?? exam.id,
      );

  test(
    'zero result and ownership survive reopen, replay, and regrading',
    () async {
      await attend(0);
      final receipt = await store.recordIntegratedExamResult(result());
      expect(receipt.alreadyRecorded, isFalse);
      await reopen();
      expect(store.academics.single.score, 0);
      expect(store.academics.single.integration!.result.version, 1);
      final repeated = await store.recordIntegratedExamResult(result());
      expect(repeated.recordId, receipt.recordId);
      expect(repeated.alreadyRecorded, isTrue);
      await store.recordIntegratedExamResult(result(score: 8.5, version: 2));
      await reopen();
      expect(store.academics, hasLength(1));
      expect(store.academics.single.id, receipt.recordId);
      expect(store.academics.single.score, 8.5);
      expect(store.academics.single.integration!.result.version, 2);
      expect(store.payments, isEmpty);
    },
  );

  test(
    'rejects stale, divergent, nonfinal and invalid results without overwrite',
    () async {
      await attend(0);
      await store.recordIntegratedExamResult(result(score: 4, version: 2));
      final saved = store.academics.single.toJson();
      for (final request in [
        result(version: 1),
        result(score: 5, version: 2),
        result(score: 11, version: 3),
        result(score: double.nan, version: 3),
        result(maxScore: 20, version: 3),
        result(maxScore: 10.5, version: 3),
        result(status: 'revoked', version: 3),
        result(status: 'pending', version: 3),
      ]) {
        await expectLater(
          store.recordIntegratedExamResult(request),
          throwsA(isA<CenterException>()),
        );
        expect(store.academics.single.toJson(), saved);
      }
    },
  );

  test(
    'manual corrections retain ownership and block engine replay or regrade',
    () async {
      await attend(0);
      await store.recordIntegratedExamResult(result(score: 3));
      await store.saveAcademic(store.academics.single.copyWith(score: 7));
      await reopen();
      expect(store.academics.single.integration!.manuallyEdited, isTrue);
      for (final request in [result(score: 3), result(score: 8, version: 2)]) {
        await expectLater(
          store.recordIntegratedExamResult(request),
          throwsA(isA<CenterException>()),
        );
      }
      expect(store.academics.single.score, 7);
    },
  );

  test(
    'manual score cannot be claimed by supplying integration metadata',
    () async {
      await attend(0);
      await store.saveAcademic(
        AcademicRecord(
          studentId: students[0].id,
          sessionId: session.id,
          activityId: exam.id,
          score: 0,
          integration: IntegratedExamOwnership(result()),
          updatedAt: DateTime.now(),
        ),
      );
      expect(store.academics.single.integration, isNull);
      await expectLater(
        store.recordIntegratedExamResult(result()),
        throwsA(isA<CenterException>()),
      );
      expect(progress().recorded, 1);
    },
  );

  test(
    'source attempt and exam destination remain bound across students',
    () async {
      await attend(0);
      await attend(1);
      await store.recordIntegratedExamResult(result(score: 2));
      await expectLater(
        store.recordIntegratedExamResult(
          result(student: 1, attemptId: 'attempt-0'),
        ),
        throwsA(isA<CenterException>()),
      );
      final secondExam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          name: 'Different exam',
          kind: AcademicActivityKind.exam,
          createdAt: DateTime.now(),
        ),
      );
      await expectLater(
        store.recordIntegratedExamResult(
          result(student: 1, activityId: secondExam.id),
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.recordIntegratedExamResult(
          result(sourceId: 'another-installation'),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.academics, hasLength(1));
    },
  );

  test(
    'ordinary result requires active attendance, including out-of-group makeup',
    () async {
      await expectLater(
        store.recordIntegratedExamResult(result()),
        throwsA(isA<CenterException>()),
      );
      await attend(2, makeup: true);
      final roster = store.integratedExamRoster(
        groupId: group.id,
        sessionId: session.id,
        activityId: exam.id,
      );
      expect(roster, hasLength(3));
      final makeup = roster.singleWhere((entry) => entry.present);
      expect(makeup.studentId, students[2].id);
      expect(makeup.phone, students[2].phone);
      expect(makeup.toJson(), isNot(contains('guardianPhone')));
      await store.recordIntegratedExamResult(result(student: 2, score: 6));
      expect(progress().recorded, 1);
      await store.cancelAttendance(
        attendanceId: store.attendances.single.id,
        reason: 'Synthetic canceled presence',
      );
      expect(progress().attending, 0);
      await expectLater(
        store.recordIntegratedExamResult(
          result(student: 2, score: 7, version: 2),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.academics.single.score, 6);
    },
  );

  test(
    'missing list counts exact activity and manual zero, excludes nonattendees',
    () async {
      await attend(0);
      await attend(2, makeup: true);
      final secondExam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          name: 'Second exam',
          kind: AcademicActivityKind.exam,
          createdAt: DateTime.now(),
        ),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: students[0].id,
          sessionId: session.id,
          activityId: exam.id,
          score: 0,
          updatedAt: DateTime.now(),
        ),
      );
      expect(progress().attending, 2);
      expect(progress().recorded, 1);
      expect(progress().missing.single.studentId, students[2].id);
      expect(progress(activityId: secondExam.id).missing, hasLength(2));
    },
  );

  test(
    'Cairo score records presence without finance, and suspension blocks new presence',
    () async {
      session = await store.startPreparedLesson(
        groupId: cairo.id,
        preparedLessonId: session.preparedLessonId!,
      );
      await store.recordIntegratedExamResult(result());
      expect(store.attendances.single.status, AttendanceStatus.present);
      expect(store.payments, isEmpty);
      expect(store.centerFees, isEmpty);
      await store.suspendStudent(
        studentId: students[1].id,
        reason: 'Synthetic suspension',
      );
      await expectLater(
        store.recordIntegratedExamResult(result(student: 1)),
        throwsA(isA<CenterException>()),
      );
      expect(store.attendances, hasLength(1));
    },
  );

  test(
    'durable failure returns no receipt and rolls grade and Cairo presence back',
    () async {
      session = await store.startPreparedLesson(
        groupId: cairo.id,
        preparedLessonId: session.preparedLessonId!,
      );
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      try {
        await db.execute(
          "CREATE TRIGGER fail_exam_commit BEFORE INSERT ON state_records WHEN NEW.section='academics' BEGIN SELECT RAISE(ABORT, 'synthetic disk failure'); END",
        );
        await expectLater(
          store.recordIntegratedExamResult(result()),
          throwsA(isA<CenterException>()),
        );
        expect(store.academics, isEmpty);
        expect(store.attendances, isEmpty);
        await db.execute('DROP TRIGGER fail_exam_commit');
      } finally {
        await db.close();
      }
      await reopen();
      expect(store.academics, isEmpty);
      expect(store.attendances, isEmpty);
      final receipt = await store.recordIntegratedExamResult(result());
      expect(receipt.alreadyRecorded, isFalse);
    },
  );

  test('signed-out caller cannot query students or record grades', () async {
    await attend(0);
    store.signOut();
    expect(() => progress(), throwsA(isA<CenterException>()));
    await expectLater(
      store.recordIntegratedExamResult(result()),
      throwsA(isA<CenterException>()),
    );
    expect(store.academics, isEmpty);
  });
}
