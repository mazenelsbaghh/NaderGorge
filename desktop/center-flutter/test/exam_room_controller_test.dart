import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/exam_engine.dart';
import 'package:massar_center/exams/exam_room_controller.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

/// Only the exam-process boundary is fake; academic and reception writes use SQLite.
class _ExamEngine implements ExamEngine {
  Map<String, dynamic> snapshot = {
    'protocol': 1,
    'sourceId': 'synthetic-controller-engine',
    'exams': <Map<String, dynamic>>[],
    'bindings': <Map<String, dynamic>>[],
    'attempts': <Map<String, dynamic>>[],
    'results': <Map<String, dynamic>>[],
  };
  final acknowledgments = <Map<String, dynamic>>[];
  final rosters = <List<Map<String, dynamic>>>[];
  final adminAccess = <bool>[];
  final openedRooms = <Map<String, dynamic>>[];
  Completer<void>? stateRequested, releaseState, ackRequested, releaseAck;
  Completer<void>? openRequested, releaseOpen;
  Future<void> Function()? beforeAck;
  Future<void> Function(bool enabled)? beforeAdminAccess;
  bool adapterClosed = false;
  @override
  bool running = true;

  @override
  Future<void> start() async => running = true;
  @override
  Future<bool> reconnect() async => running;
  @override
  Future<void> close() async => adapterClosed = true;
  @override
  Future<Map<String, dynamic>> network(Map<String, dynamic> command) async =>
      throw StateError('No network mutation expected in controller sync tests');

  @override
  Future<Map<String, dynamic>> request(
    String route, [
    Map<String, dynamic>? body,
  ]) async {
    switch (route) {
      case '/api/center/state':
        if (stateRequested?.isCompleted == false) stateRequested!.complete();
        await releaseState?.future;
        return jsonDecode(jsonEncode(snapshot)) as Map<String, dynamic>;
      case '/api/center/bind':
        rosters.add(
          (body!['roster'] as List)
              .map((row) => Map<String, dynamic>.from(row as Map))
              .toList(),
        );
        return {'ok': true};
      case '/api/center/open':
        openedRooms.add(jsonDecode(jsonEncode(body)) as Map<String, dynamic>);
        if (openRequested?.isCompleted == false) openRequested!.complete();
        await releaseOpen?.future;
        final id = 'opened-room-${openedRooms.length}';
        (snapshot['exams'] as List).add({'id': id, 'state': 'waiting'});
        (snapshot['bindings'] as List).add({'examId': id, ...body!});
        return {'examId': id};
      case '/api/center/ack':
        if (ackRequested?.isCompleted == false) ackRequested!.complete();
        await releaseAck?.future;
        await beforeAck?.call();
        acknowledgments.add(Map<String, dynamic>.from(body!));
        (snapshot['results'] as List).removeWhere(
          (row) =>
              row['attemptId'] == body['attemptId'] &&
              row['version'] == body['version'],
        );
        return {'ok': true};
      case '/api/center/admin-access':
        adminAccess.add(body!['enabled'] as bool);
        await beforeAdminAccess?.call(body['enabled'] as bool);
        return {'adminUrl': 'http://127.0.0.1:8766/'};
      default:
        throw StateError('Unexpected engine route $route');
    }
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'synthetic-controller-password';
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late LessonSession session;
  late AcademicActivity exam;
  late List<Student> students;
  late _ExamEngine engine;
  late ExamRoomController controller;
  var disposed = false;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('exam-controller-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('manager', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'Synthetic group',
        centerId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.center)
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
    group = store.groups.single;
    for (var index = 0; index < 2; index++) {
      await store.saveStudent(
        Student(
          code: '0100$index',
          name: 'Synthetic student $index',
          phone: '0100000000$index',
          groupIds: [group.id],
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
        name: 'Synthetic exam',
        kind: AcademicActivityKind.exam,
        preparedLessonId: session.preparedLessonId,
        createdAt: DateTime.now(),
      ),
    );
    await store.recordAttendance(
      EntryRequest(
        studentId: students[0].id,
        sessionId: session.id,
        mode: EntryMode.single,
      ),
    );
    engine = _ExamEngine();
    engine.snapshot['exams'] = [
      {'id': 'engine-exam', 'state': 'closed'},
    ];
    engine.snapshot['bindings'] = [
      {
        'examId': 'engine-exam',
        'groupId': group.id,
        'sessionId': session.id,
        'activityId': exam.id,
        'maxScore': 10,
      },
    ];
    controller = ExamRoomController(
      engine: engine,
      store: store,
      pollInterval: const Duration(days: 1),
    );
    disposed = false;
  });
  tearDown(() async {
    if (!disposed) controller.dispose();
    await store.close();
    await directory.delete(recursive: true);
  });

  Map<String, dynamic> result({
    String attemptId = 'attempt-zero',
    String? studentId,
    String status = 'final',
    num? score = 0,
  }) => {
    'sourceId': 'synthetic-controller-engine',
    'examId': 'engine-exam',
    'attemptId': attemptId,
    'studentId': studentId ?? students[0].id,
    'sessionId': session.id,
    'activityId': exam.id,
    'version': 1,
    'score': score,
    'maxScore': 10,
    'status': status,
  };

  Future<void> prepareRoomCreation() async {
    engine.snapshot['exams'] = <Map<String, dynamic>>[];
    engine.snapshot['bindings'] = <Map<String, dynamic>>[];
    engine.snapshot['templates'] = [
      {'id': 'question-set', 'title': 'أسئلة جاهزة', 'maxScore': 10},
      {'id': 'other-total', 'title': 'أسئلة بدرجة مختلفة', 'maxScore': 20},
    ];
    await controller.refresh();
  }

  test(
    'room opens already bound to the official center roster without a second catalog',
    () async {
      await prepareRoomCreation();
      final paymentsBefore = store.payments
          .map((payment) => payment.toJson())
          .toList();
      final id = await controller.openRoom(
        templateId: 'question-set',
        groupId: group.id,
        sessionId: session.id,
        activityId: exam.id,
      );
      expect(engine.openedRooms, [
        {
          'templateId': 'question-set',
          'groupId': group.id,
          'sessionId': session.id,
          'activityId': exam.id,
          'maxScore': 10,
          'roster': [
            for (final student in students)
              {
                'studentId': student.id,
                'code': student.code,
                'name': student.name,
                'phone': student.phone,
                'present': student.id == students[0].id,
              },
          ],
        },
      ]);
      expect(controller.exams.single['id'], id);
      expect(controller.bindings.single['activityId'], exam.id);
      expect(controller.bindings.single['examId'], id);
      expect(engine.rosters, isEmpty);
      expect(store.academics, isEmpty);
      expect(
        store.payments.map((payment) => payment.toJson()).toList(),
        paymentsBefore,
      );
    },
  );

  test(
    'room creation rejects mismatched totals and invalid center mapping before any engine write',
    () async {
      await prepareRoomCreation();
      for (final (templateId, groupId, activityId) in [
        ('other-total', group.id, exam.id),
        ('question-set', 'another-group', exam.id),
        ('question-set', group.id, 'missing-activity'),
      ]) {
        await expectLater(
          controller.openRoom(
            templateId: templateId,
            groupId: groupId,
            sessionId: session.id,
            activityId: activityId,
          ),
          throwsA(isA<CenterException>()),
        );
      }
      expect(engine.openedRooms, isEmpty);
      expect(controller.exams, isEmpty);
    },
  );

  test(
    'logout rejects new room requests and discards a delayed prior employee completion',
    () async {
      await prepareRoomCreation();
      engine.openRequested = Completer<void>();
      engine.releaseOpen = Completer<void>();
      final opening = controller.openRoom(
        templateId: 'question-set',
        groupId: group.id,
        sessionId: session.id,
        activityId: exam.id,
      );
      final rejected = expectLater(opening, throwsA(isA<CenterException>()));
      await engine.openRequested!.future;
      store.signOut();
      engine.releaseOpen!.complete();
      await rejected;
      expect(controller.exams, isEmpty);
      await expectLater(
        controller.openRoom(
          templateId: 'question-set',
          groupId: group.id,
          sessionId: session.id,
          activityId: exam.id,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(engine.openedRooms, hasLength(1));
      expect(store.academics, isEmpty);
    },
  );

  test(
    'finalized zero is acknowledged only after the host stores its receipt',
    () async {
      engine.snapshot['results'] = [result()];
      engine.beforeAck = () async {
        final db = await databaseFactoryFfi.openDatabase(
          store.databasePath,
          options: OpenDatabaseOptions(readOnly: true, singleInstance: false),
        );
        try {
          final stored = await db.query(
            'state_records',
            where: 'section = ?',
            whereArgs: ['academics'],
          );
          final record = jsonDecode(stored.single['payload'] as String) as Map;
          expect(record['score'], 0);
          expect((record['integration'] as Map)['attemptId'], 'attempt-zero');
        } finally {
          await db.close();
        }
      };
      await controller.refresh();
      expect(engine.acknowledgments, [
        {'attemptId': 'attempt-zero', 'version': 1},
      ]);
      expect(controller.resultProblems, isEmpty);
      expect(store.academics.single.score, 0);
    },
  );

  test(
    'failed host commit remains pending and retries after storage recovers',
    () async {
      engine.snapshot['results'] = [result()];
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      try {
        await db.execute(
          "CREATE TRIGGER fail_controller_grade BEFORE INSERT ON state_records WHEN NEW.section='academics' BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END",
        );
        await controller.refresh();
        expect(engine.acknowledgments, isEmpty);
        expect(store.academics, isEmpty);
        expect(engine.snapshot['results'], hasLength(1));
        expect(controller.resultProblems, isNotEmpty);
        await db.execute('DROP TRIGGER fail_controller_grade');
      } finally {
        await db.close();
      }
      await controller.refresh();
      expect(store.academics.single.score, 0);
      expect(engine.acknowledgments, [
        {'attemptId': 'attempt-zero', 'version': 1},
      ]);
      expect(controller.resultProblems, isEmpty);
    },
  );

  test(
    'revoked and cancelled results require review instead of zero grade or ACK',
    () async {
      engine.snapshot['results'] = [
        result(attemptId: 'revoked', status: 'revoked', score: null),
        result(attemptId: 'cancelled', status: 'cancelled'),
      ];
      await controller.refresh();
      expect(store.academics, isEmpty);
      expect(engine.acknowledgments, isEmpty);
      expect(controller.resultProblems, hasLength(2));
    },
  );

  for (final signBackIn in [false, true]) {
    test(
      'employee ${signBackIn ? 'logout and same-account relogin' : 'logout'} invalidates an in-flight poll',
      () async {
        engine.snapshot['results'] = [result()];
        engine.stateRequested = Completer<void>();
        engine.releaseState = Completer<void>();
        final polling = controller.refresh();
        await engine.stateRequested!.future;
        store.signOut();
        if (signBackIn) await store.signIn('manager', password);
        engine.releaseState!.complete();
        await polling;
        expect(store.academics, isEmpty);
        expect(engine.acknowledgments, isEmpty);
        expect(engine.adminAccess, contains(false));
        expect(engine.running, isTrue);
      },
    );
  }

  test(
    'reception can record attendance while exam delivery awaits an ACK',
    () async {
      engine.snapshot['results'] = [result()];
      engine.ackRequested = Completer<void>();
      engine.releaseAck = Completer<void>();
      final polling = controller.refresh();
      await engine.ackRequested!.future;
      await store.recordAttendance(
        EntryRequest(
          studentId: students[1].id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      expect(
        store.attendances.map((entry) => entry.studentId),
        contains(students[1].id),
      );
      engine.releaseAck!.complete();
      await polling;
      expect(store.academics.single.score, 0);
      expect(engine.acknowledgments, hasLength(1));
      expect(store.payments, isEmpty);
    },
  );

  test(
    'newly registered and present late arrival reaches the engine roster',
    () async {
      await controller.refresh();
      await store.saveStudent(
        Student(
          code: '01009',
          name: 'Synthetic late arrival',
          phone: '01000000009',
          guardianPhone: '01100000009',
          groupIds: [group.id],
          createdAt: DateTime.now(),
        ),
      );
      final lateStudent = store.students.firstWhere(
        (entry) => entry.code == '01009',
      );
      await store.recordAttendance(
        EntryRequest(
          studentId: lateStudent.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      await controller.refresh();
      final row = engine.rosters.last.singleWhere(
        (entry) => entry['code'] == '01009',
      );
      expect(row, {
        'studentId': lateStudent.id,
        'code': '01009',
        'name': 'Synthetic late arrival',
        'phone': '01000000009',
        'present': true,
      });
    },
  );

  for (final blockedStatus in ['revoked', 'missing-student']) {
    test(
      'twenty $blockedStatus results do not starve a later valid grade',
      () async {
        engine.snapshot['results'] = [
          for (var index = 0; index < 20; index++)
            result(
              attemptId: 'blocked-$index',
              studentId: blockedStatus == 'missing-student'
                  ? 'missing-$index'
                  : null,
              status: blockedStatus == 'revoked' ? 'revoked' : 'final',
              score: blockedStatus == 'revoked' ? null : 0,
            ),
          result(),
        ];
        await controller.refresh();
        await controller.refresh();
        expect(engine.acknowledgments, [
          {'attemptId': 'attempt-zero', 'version': 1},
        ]);
        expect(store.academics.single.score, 0);
        expect(engine.snapshot['results'], hasLength(20));
      },
    );
  }

  test(
    'disposing the controller ignores pending work and leaves student service running',
    () async {
      engine.snapshot['results'] = [result()];
      engine.stateRequested = Completer<void>();
      engine.releaseState = Completer<void>();
      final polling = controller.refresh();
      await engine.stateRequested!.future;
      controller.dispose();
      disposed = true;
      engine.releaseState!.complete();
      await polling;
      expect(store.academics, isEmpty);
      expect(engine.acknowledgments, isEmpty);
      expect(engine.adapterClosed, isTrue);
      expect(engine.running, isTrue);
    },
  );

  test(
    'superseded result conflicts disappear after a newer result is recorded',
    () async {
      engine.snapshot['results'] = [result(studentId: 'missing-student')];
      await controller.refresh();
      expect(controller.resultProblems.keys, ['attempt-zero:1']);
      engine.snapshot['results'] = [
        {...result(), 'version': 2},
      ];
      await controller.refresh();
      expect(controller.resultProblems, isEmpty);
      expect(store.academics.single.score, 0);
      expect(engine.acknowledgments, [
        {'attemptId': 'attempt-zero', 'version': 2},
      ]);
    },
  );

  test(
    'a delayed old administrator cannot revoke the new signed-in session',
    () async {
      final entered = Completer<void>();
      final release = Completer<void>();
      var firstEnable = true;
      engine.beforeAdminAccess = (enabled) async {
        if (enabled && firstEnable) {
          firstEnable = false;
          entered.complete();
          await release.future;
        }
      };
      final oldOpening = controller.openAdmin();
      final oldRejected = expectLater(
        oldOpening,
        throwsA(isA<CenterException>()),
      );
      await entered.future;
      store.signOut();
      await store.signIn('manager', password);
      final newOpening = controller.openAdmin();
      // Let the current employee's opening queue behind the blocked old request.
      await Future<void>.delayed(Duration.zero);
      release.complete();
      await oldRejected;
      expect(await newOpening, 'http://127.0.0.1:8766/');
      await Future<void>.delayed(Duration.zero);
      expect(
        engine.adminAccess.last,
        isTrue,
        reason: 'The last authorization belongs to the current employee.',
      );
      expect(engine.running, isTrue);
    },
  );
}
