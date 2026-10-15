import 'dart:io';
import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/center_exam_integration.dart';
import 'package:massar_center/domain/center_exam_monitor.dart';
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

  ExamMonitorSnapshot observation({
    int version = 1,
    List<int> entered = const [0, 1],
    String? activityId,
    String? sessionId,
    String source = 'monitor-engine',
    String? firstAttempt,
  }) => ExamMonitorSnapshot(
    sourceId: source,
    examId: 'monitor-room',
    groupId: group.id,
    sessionId: sessionId ?? session.id,
    activityId: activityId ?? exam.id,
    version: version,
    roomState: 'running',
    attempts: [
      for (final index in entered)
        ExamMonitorAttempt(
          attemptId: index == entered.first
              ? firstAttempt ?? 'entry-$index'
              : 'entry-$index',
          studentId: students[index].id,
          status: 'not_submitted',
        ),
    ],
  );

  test(
    'exam entry alert survives restart and clears only for actual attendance without payment',
    () async {
      await store.publishIntegratedExamSnapshot(observation());
      expect(store.integratedExamAttendanceAlerts.map((row) => row.code), [
        '00100',
        '00101',
      ]);
      expect(store.integratedExamMonitorStatuses.single.stale, isFalse);
      final auditCount = store.audit.length;
      await store.publishIntegratedExamSnapshot(observation());
      expect(
        store.audit.length,
        auditCount,
        reason: 'heartbeat must not write another audit',
      );
      await reopen();
      expect(store.integratedExamAttendanceAlerts, hasLength(2));
      expect(store.integratedExamMonitorStatuses.single.stale, isTrue);
      await attend(0);
      expect(
        store.integratedExamAttendanceAlerts.single.studentId,
        students[1].id,
      );
      expect(store.payments, isEmpty);
      expect(store.academics, isEmpty);
      await store.publishIntegratedExamSnapshot(observation());
      expect(store.integratedExamMonitorStatuses.single.stale, isFalse);
      store.signOut();
      expect(store.integratedExamAttendanceAlerts, isEmpty);
      await expectLater(
        store.publishIntegratedExamSnapshot(observation()),
        throwsA(isA<CenterException>()),
      );
    },
  );

  test(
    'secondary results require a host download request and survive a lost reply without duplicate grades',
    () async {
      await attend(0);
      final staffId = store.currentUser!.id;
      final originalPayments = store.payments.length;
      await store.publishExamMonitorLan(
        deviceId: 'secondary',
        staffId: staffId,
        snapshot: observation(),
        authorize: () => true,
      );
      final grade = result(
        sourceId: 'monitor-engine',
        examId: 'monitor-room',
        attemptId: 'entry-0',
      );
      const commandId = '11111111-1111-4111-8111-111111111111';
      Future<Map<String, dynamic>> sendGrade(String device) => store.commandLan(
        deviceId: device,
        staffId: staffId,
        request: {
          'requestId': commandId,
          'operation': 'recordIntegratedExamResult',
          'arguments': grade.toJson(),
        },
      );
      await expectLater(
        sendGrade('secondary'),
        throwsA(isA<CenterException>()),
      );
      expect(store.academics, isEmpty);
      final roomId = store.integratedExamRooms.single.id;
      await store.requestExamResultImport(roomId);
      final requestId = store.integratedExamRooms.single.importRequestId!;
      await reopen();
      expect(store.integratedExamRooms.single.importPending, isTrue);
      await expectLater(
        sendGrade('another-device'),
        throwsA(isA<CenterException>()),
      );
      final accepted = await sendGrade('secondary');
      expect(accepted['status'], 'committed');
      expect(store.academics.single.score, 0);
      final completion = {
        'requestId': '22222222-2222-4222-8222-222222222222',
        'operation': 'completeExamResultImport',
        'arguments': {
          'roomId': roomId,
          'requestId': requestId,
          'problems': <String, String>{},
        },
      };
      await store.commandLan(
        deviceId: 'secondary',
        staffId: staffId,
        request: completion,
      );
      expect(store.integratedExamRooms.single.importPending, isFalse);
      expect(await sendGrade('secondary'), containsPair('status', 'committed'));
      expect(store.academics, hasLength(1));
      expect(progress().recorded, 1);
      expect(
        store.payments,
        hasLength(originalPayments),
        reason: 'grade import must not collect money',
      );
    },
  );

  test(
    'monitor refresh preserves a requested import and pre-import backup excludes new schema',
    () async {
      await store.publishExamMonitorLan(
        deviceId: 'secondary',
        staffId: store.currentUser!.id,
        snapshot: observation(),
        authorize: () => true,
      );
      await store.requestExamResultImport(store.integratedExamRooms.single.id);
      final request = store.integratedExamRooms.single.importRequestId;
      await store.publishExamMonitorLan(
        deviceId: 'secondary',
        staffId: store.currentUser!.id,
        snapshot: observation(version: 2, entered: [0]),
        authorize: () => true,
      );
      expect(store.integratedExamRooms.single.importRequestId, request);
      final backups = await Directory('${directory.path}/backups')
          .list()
          .where((file) => file.path.contains('before-exam-import-v13-'))
          .toList();
      expect(backups, hasLength(1));
      final preserved =
          (jsonDecode(await File(backups.single.path).readAsString())
                  as Map)['data']
              as Map;
      expect(preserved['schemaVersion'], 12);
      expect(
        (preserved['examMonitors'] as List).single['importRequestId'],
        isNull,
      );
    },
  );

  test(
    'update backup precedes the first durable exam monitor and remains legacy-readable',
    () async {
      await store.close();
      final originalBackups = await Directory(
        store.automaticBackupDirectory!,
      ).list().map((entry) => entry.path).toSet();
      await File('${directory.path}/last-opened-build.json').writeAsString(
        jsonEncode({
          'version': 1,
          'buildIdentifier': 'synthetic-previous-build',
        }),
      );
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('manager', password);
      final files = await Directory(store.automaticBackupDirectory!)
          .list()
          .where(
            (entry) => entry is File && !originalBackups.contains(entry.path),
          )
          .toList();
      expect(files, hasLength(1));
      final preserved =
          (jsonDecode(await File(files.single.path).readAsString())
                  as Map)['data']
              as Map;
      expect(preserved['schemaVersion'] as int, lessThan(12));
      expect(preserved['examMonitors'], isNull);
      await store.publishIntegratedExamSnapshot(observation());
      expect(store.integratedExamAttendanceAlerts, hasLength(2));
      final migrationBackups = await Directory('${directory.path}/backups')
          .list()
          .where(
            (entry) =>
                entry is File &&
                entry.path.contains('before-exam-monitor-v12-'),
          )
          .toList();
      expect(migrationBackups, hasLength(1));
      final legacyState =
          (jsonDecode(await File(migrationBackups.single.path).readAsString())
                  as Map)['data']
              as Map;
      expect(legacyState['schemaVersion'] as int, lessThan(12));
      expect(legacyState['examMonitors'], isNull);
      expect((legacyState['students'] as List).length, students.length);
      final unchanged =
          jsonDecode(await File(files.single.path).readAsString()) as Map;
      expect((unchanged['data'] as Map)['examMonitors'], isNull);
    },
  );

  test(
    'one thousand exam entrants do not lose a simultaneous reception attendance',
    () async {
      final backup = await store.createBackup(
        destination: '${directory.path}/large-fixture.json',
      );
      final contents =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final rows = (contents['data'] as Map)['students'] as List;
      final enrollments = (contents['data'] as Map)['enrollments'] as Map;
      for (var index = 3; index < 1001; index++) {
        enrollments['load-student-$index:${group.id}'] = DateTime.now()
            .toIso8601String();
        rows.add(
          Student(
            id: 'load-student-$index',
            code: '${10000 + index}',
            name: 'Synthetic entry $index',
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ).toJson(),
        );
      }
      await File(backup).writeAsString(jsonEncode(contents));
      await store.restoreBackup(backup);
      await store.signIn('manager', password);
      final entrants = store.students
          .where((student) => student.groupIds.contains(group.id))
          .toList();
      expect(entrants, hasLength(1000));
      final snapshot = ExamMonitorSnapshot(
        sourceId: 'load-engine',
        examId: 'load-room',
        groupId: group.id,
        sessionId: session.id,
        activityId: exam.id,
        version: 1,
        roomState: 'running',
        attempts: [
          for (final student in entrants)
            ExamMonitorAttempt(
              attemptId: 'entry-${student.id}',
              studentId: student.id,
              status: 'not_submitted',
            ),
        ],
      );
      await Future.wait([
        store.publishIntegratedExamSnapshot(snapshot),
        attend(0),
      ]);
      expect(store.integratedExamAttendanceAlerts, hasLength(999));
      expect(store.attendances.single.studentId, students[0].id);
      final revision = store.readRevision;
      await store.publishIntegratedExamSnapshot(snapshot);
      expect(
        identical(store.readRevision, revision),
        isTrue,
        reason: '1000-entry heartbeat must not mutate or persist center state',
      );
      expect(store.payments, isEmpty);
    },
  );

  test(
    'restoring a monitor backup clears live freshness without losing entered students',
    () async {
      await store.publishIntegratedExamSnapshot(observation());
      final backup = await store.createBackup(
        destination: '${directory.path}/monitor-backup.json',
      );
      await store.publishIntegratedExamSnapshot(
        observation(version: 2, entered: []),
      );
      expect(store.integratedExamAttendanceAlerts, isEmpty);
      await store.restoreBackup(backup);
      await store.signIn('manager', password);
      expect(store.integratedExamAttendanceAlerts, hasLength(2));
      expect(store.integratedExamMonitorStatuses.single.stale, isTrue);
    },
  );

  test(
    'monitor rejects stale versions, changed binding and stolen attempt identity atomically',
    () async {
      await store.publishIntegratedExamSnapshot(observation(version: 2));
      final anotherExam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          name: 'Different named exam',
          kind: AcademicActivityKind.exam,
          maxScore: 10,
          createdAt: DateTime.now(),
        ),
      );
      for (final snapshot in [
        observation(),
        observation(version: 2, entered: []),
        observation(version: 3, activityId: anotherExam.id),
        observation(version: 3, entered: [1], firstAttempt: 'entry-0'),
        observation(version: 3, entered: [2]),
      ]) {
        await expectLater(
          store.publishIntegratedExamSnapshot(snapshot),
          throwsA(isA<CenterException>()),
        );
        expect(store.integratedExamAttendanceAlerts, hasLength(2));
      }
      await store.publishIntegratedExamSnapshot(
        observation(version: 3, entered: []),
      );
      expect(
        store.integratedExamAttendanceAlerts,
        isEmpty,
        reason:
            'only a complete newer successful observation can clear entrants',
      );
      await expectLater(
        store.publishIntegratedExamSnapshot(
          observation(version: 4, entered: [1], firstAttempt: 'entry-0'),
        ),
        throwsA(isA<CenterException>()),
      );
    },
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

  for (final role in [StaffRole.cashier, StaffRole.assistant]) {
    test('${role.name} operates exams without management privileges', () async {
      await attend(0);
      await store.saveStaff(name: role.name, password: password, role: role);
      store.signOut();
      await store.signIn(role.name, password);
      expect(store.canManage, isFalse);
      final roster = store.integratedExamRoster(
        groupId: group.id,
        sessionId: session.id,
        activityId: exam.id,
      );
      expect(
        roster.singleWhere((entry) => entry.present).phone,
        students[0].phone,
      );
      final receipt = await store.recordIntegratedExamResult(result(score: 0));
      expect(receipt.alreadyRecorded, isFalse);
      expect(progress().recorded, 1);
      expect(store.audit.last.staffId, store.currentUser!.id);
      await expectLater(
        store.saveStaff(
          name: 'not-authorized',
          password: password,
          role: StaffRole.admin,
        ),
        throwsA(isA<CenterException>()),
      );
      if (role == StaffRole.assistant) {
        await expectLater(
          store.collectStudentCard(studentId: students[0].id),
          throwsA(isA<CenterException>()),
        );
        expect(store.canCollect, isFalse);
      }
      expect(store.cardPayments, isEmpty);
      expect(store.payments, isEmpty);
      expect(store.academics.single.score, 0);
    });
  }
}
