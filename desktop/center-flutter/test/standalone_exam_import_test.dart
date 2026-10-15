import 'dart:convert';
import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/domain/standalone_exam_results.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup cairo, regular;
  late LessonSession session;
  late AcademicActivity exam;
  late List<Student> students;
  const password = 'standalone-store-test';
  String revision(int number) => number.toRadixString(16).padLeft(64, '0');
  setUp(() async {
    directory = await Directory.systemTemp.createTemp('standalone-grades-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('manager', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveCatalog(
      const CatalogEntry(name: 'القاهرة', kind: CatalogKind.center),
    );
    for (final center in store.catalogs.where(
      (row) => row.kind == CatalogKind.center,
    )) {
      await store.saveGroup(
        StudyGroup(
          name: center.name,
          centerId: center.id,
          subjectId: store.catalogs
              .firstWhere((row) => row.kind == CatalogKind.subject)
              .id,
          gradeId: store.catalogs
              .firstWhere((row) => row.kind == CatalogKind.grade)
              .id,
        ),
      );
    }
    cairo = store.groupsForRegion(cairo: true).single;
    regular = store.groupsForRegion().single;
    for (var number = 0; number < 4; number++) {
      await store.saveStudent(
        Student(
          name: 'طالب $number',
          code: '0100$number',
          phone: '0100000000$number',
          groupIds: [cairo.id, regular.id],
          createdAt: DateTime.now(),
        ),
      );
    }
    students = store.students;
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'شهر الاختبار',
        lessons: [const PreparedLesson(number: 1)],
      ),
    );
    session = await store.startPreparedLesson(
      groupId: cairo.id,
      preparedLessonId: month.lessons.single.id,
    );
    exam = await store.saveAcademicActivity(
      AcademicActivity(
        name: 'امتحان السنتر',
        preparedLessonId: month.lessons.single.id,
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
  StandaloneExamAttempt attempt(
    int index, {
    String? id,
    num? score = 7.5,
    int version = 1,
    String status = 'final',
    String? code,
    String? phone,
    num maximum = 10,
  }) => StandaloneExamAttempt(
    attemptId: id ?? 'attempt-$index',
    name: students[index].name,
    code: code ?? students[index].code,
    phone: phone ?? students[index].phone,
    status: status,
    score: status == 'final' ? score : null,
    maxScore: maximum,
    pending: status == 'pending_grading' ? 1 : 0,
    resultVersion: version,
  );
  StandaloneExamPage page(
    List<StandaloneExamAttempt> rows, {
    int version = 1,
    int? total,
    String? after,
    String? next,
    num maximum = 10,
    StandaloneExamTarget? target,
  }) => StandaloneExamPage(
    sourceId: 'secondary-source',
    examId: 'offline-room',
    title: 'امتحان مستقل',
    roomState: 'closed',
    maxScore: maximum,
    target: target,
    totalAttempts: total ?? rows.length,
    exportRevision: revision(version),
    attempts: rows,
    afterAttemptId: after,
    nextCursor: next,
  );
  StandaloneExamImportPlan preview() => store.previewStandaloneExamImport(
    roomId: 'secondary-source:offline-room',
    groupId: session.groupId,
    sessionId: session.id,
    activityId: exam.id,
  );
  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('manager', password);
  }

  Future<String> durablePayload() async {
    final database = await databaseFactoryFfi.openDatabase(
      store.databasePath,
      options: OpenDatabaseOptions(readOnly: true, singleInstance: false),
    );
    try {
      return (await database.query('state')).single['payload'] as String;
    } finally {
      await database.close();
    }
  }

  test(
    'offline raw results persist without roster prep and one confirmation records zero without money',
    () async {
      await store.publishStandaloneExamPage(
        page([attempt(0, score: 0), attempt(1)]),
      );
      expect(store.academics, isEmpty);
      expect(store.attendances, isEmpty);
      await reopen();
      final plan = preview();
      expect(plan.readyCount, 2);
      expect(plan.replacementCount, 0);
      final result = await store.importStandaloneExamGrades(plan);
      expect(result.recorded, 2);
      expect(store.academics.map((row) => row.score), containsAll([0, 7.5]));
      expect(store.attendances, hasLength(2));
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(
        store.academics.first.integration!.result.sourceId,
        'secondary-source',
      );
      expect(jsonDecode(await durablePayload())['schemaVersion'], 14);
      await reopen();
      expect(preview().alreadyRecordedCount, 2);
      expect((await store.importStandaloneExamGrades(preview())).recorded, 0);
      expect(store.academics, hasLength(2));
    },
  );

  test(
    'cached target survives import and electronic results keep twelve beside a paper ten',
    () async {
      exam = await store.saveAcademicActivity(
        exam.copyWith(allowedMaxScores: [10, 12]),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: students[0].id,
          sessionId: session.id,
          activityId: exam.id,
          score: 9,
          maxScore: 10,
          updatedAt: DateTime.now(),
        ),
      );
      final target = StandaloneExamTarget(
        hostId: store.academicCatalogHostId,
        groupId: session.groupId,
        sessionId: session.id,
        activityId: exam.id,
        maxScore: 12,
      );
      await store.publishStandaloneExamPage(
        page(
          [attempt(1, score: 11.5, maximum: 12)],
          maximum: 12,
          target: target,
        ),
      );
      expect((await store.importStandaloneExamGrades(preview())).recorded, 1);
      expect(
        store.academics.map((row) => (row.score, row.maxScore)),
        containsAll([(9, 10), (11.5, 12)]),
      );
      expect(
        store.standaloneExamRooms.single.target!.toJson(),
        target.toJson(),
      );
      await reopen();
      expect(
        store.standaloneExamRooms.single.target!.toJson(),
        target.toJson(),
      );
      expect(preview().alreadyRecordedCount, 1);
      final electronic = store.academics.firstWhere(
        (row) => row.studentId == students[1].id,
      );
      await store.saveAcademic(electronic.copyWith(score: 9, maxScore: 10));
      expect(
        store.academics
            .firstWhere((row) => row.id == electronic.id)
            .integration!
            .manuallyEdited,
        isTrue,
      );
      await store.importStandaloneExamGrades(preview());
      await reopen();
      final manuallyCorrected = store.academics.firstWhere(
        (row) => row.id == electronic.id,
      );
      expect(manuallyCorrected.maxScore, 10);
      expect(manuallyCorrected.integration!.result.maxScore, 12);
      expect(manuallyCorrected.score, 9);
    },
  );

  test(
    '201 row transfer rejects partial, mixed revisions and duplicate pages before completion',
    () async {
      final rows = [
        for (var index = 0; index < 200; index++)
          attempt(
            0,
            id: 'attempt-${index.toString().padLeft(4, '0')}',
            code: 'unknown-$index',
            phone: '',
          ),
      ];
      await store.publishStandaloneExamPage(
        page(rows, total: 201, next: rows.last.attemptId),
      );
      expect(store.standaloneExamRooms.single.complete, isFalse);
      expect(preview, throwsA(isA<CenterException>()));
      await expectLater(
        store.publishStandaloneExamPage(
          page(
            [attempt(1)],
            version: 2,
            total: 201,
            after: rows.last.attemptId,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.publishStandaloneExamPage(
          page([rows.last], total: 201, after: rows.last.attemptId),
        ),
        throwsA(isA<CenterException>()),
      );
      await store.publishStandaloneExamPage(
        page([attempt(1)], total: 201, after: rows.last.attemptId),
      );
      expect(store.standaloneExamRooms.single.complete, isTrue);
      expect(preview().readyCount, 1);
      final before = await durablePayload();
      await store.publishStandaloneExamPage(
        page(rows, total: 201, next: rows.last.attemptId),
      );
      expect(await durablePayload(), before);
      await store.publishStandaloneExamPage(
        page(rows, version: 2, total: 201, next: rows.last.attemptId),
      );
      await store.publishStandaloneExamPage(
        page(
          [attempt(1, version: 2, status: 'cancelled')],
          version: 2,
          total: 201,
          after: rows.last.attemptId,
        ),
      );
      await store.publishStandaloneExamPage(
        page(rows, version: 3, total: 201, next: rows.last.attemptId),
      );
      await expectLater(
        store.publishStandaloneExamPage(
          page(
            [attempt(1)],
            version: 3,
            total: 201,
            after: rows.last.attemptId,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.standaloneExamRooms.single.complete, isFalse);
    },
  );

  test(
    'reconnect preserves manual edits, a newer correction requires fresh approval and old versions cannot return',
    () async {
      await store.publishStandaloneExamPage(page([attempt(0, score: 0)]));
      await store.importStandaloneExamGrades(preview());
      final original = store.academics.single;
      await store.saveAcademic(
        AcademicRecord(
          id: original.id,
          studentId: original.studentId,
          sessionId: session.id,
          activityId: exam.id,
          score: 9,
          maxScore: 10,
          notes: 'مراجعة يدوية',
          updatedAt: DateTime.now(),
        ),
      );
      await reopen();
      expect(preview().alreadyRecordedCount, 1);
      await store.importStandaloneExamGrades(preview());
      expect(store.academics.single.score, 9);
      await store.publishStandaloneExamPage(
        page([attempt(0, score: 2, version: 2)], version: 2),
      );
      expect(preview().replacementCount, 1);
      await store.importStandaloneExamGrades(preview());
      expect(store.academics.single.score, 2);
      expect(store.academics.single.notes, contains('مراجعة يدوية'));
      await expectLater(
        store.publishStandaloneExamPage(page([attempt(0, score: 0)])),
        throwsA(isA<CenterException>()),
      );
      expect(store.academics.single.score, 2);
    },
  );

  test(
    'fresh data changes reject the whole approved batch and an attempt cannot be moved to another destination',
    () async {
      await store.publishStandaloneExamPage(page([attempt(0), attempt(1)]));
      final approved = preview();
      await store.saveStudent(students[1].copyWith(name: 'اسم محدث'));
      final before = await durablePayload();
      await expectLater(
        store.importStandaloneExamGrades(approved),
        throwsA(isA<CenterException>()),
      );
      expect(await durablePayload(), before);
      expect(store.academics, isEmpty);
      await store.importStandaloneExamGrades(preview());
      final other = await store.startPreparedLesson(
        groupId: regular.id,
        preparedLessonId: session.preparedLessonId!,
      );
      expect(
        () => store.previewStandaloneExamImport(
          roomId: 'secondary-source:offline-room',
          groupId: regular.id,
          sessionId: other.id,
          activityId: exam.id,
        ),
        throwsA(isA<CenterException>()),
      );
    },
  );

  test(
    'actual attendees without an attempt are separated from essay, identity conflicts and absent students',
    () async {
      session = await store.startPreparedLesson(
        groupId: regular.id,
        preparedLessonId: session.preparedLessonId!,
      );
      for (final student in students.take(3)) {
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
      }
      final attendanceCount = store.attendances.length;
      final paymentsBefore = store.payments.map((row) => row.toJson()).toList();
      await store.publishStandaloneExamPage(
        page([
          attempt(0, status: 'pending_grading'),
          attempt(1, phone: students[0].phone),
          attempt(3),
        ]),
      );
      final plan = preview();
      expect(plan.attending, 3);
      expect(plan.examined, 1);
      expect(plan.didNotEnter.map((row) => row.id), [students[2].id]);
      expect(
        plan.attendanceIssues.map((row) => row.student.id),
        containsAll([students[0].id, students[1].id]),
      );
      expect(plan.readyCount, 0);
      final result = await store.importStandaloneExamGrades(plan);
      expect(result.skipped, 3);
      expect(store.academics, isEmpty);
      expect(store.attendances, hasLength(attendanceCount));
      expect(
        store.payments.map((row) => row.toJson()).toList(),
        paymentsBefore,
      );
    },
  );

  test(
    'duplicate exact attempts and cancelled or unfinished attempts are never bulk recorded',
    () async {
      await store.publishStandaloneExamPage(
        page([
          attempt(0),
          attempt(0, id: 'second-attempt'),
          attempt(1, status: 'cancelled'),
          attempt(2, status: 'waiting'),
        ]),
      );
      final plan = preview();
      expect(plan.readyCount, 0);
      expect((await store.importStandaloneExamGrades(plan)).skipped, 4);
      expect(store.academics, isEmpty);
      store.signOut();
      expect(store.standaloneExamRooms, isEmpty);
      await expectLater(
        store.publishStandaloneExamPage(page([attempt(0)])),
        throwsA(isA<CenterException>()),
      );
    },
  );
}
