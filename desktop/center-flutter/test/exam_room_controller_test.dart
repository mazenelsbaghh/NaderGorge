import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/domain/center_exam_monitor.dart';
import 'package:massar_center/domain/standalone_exam_results.dart';
import 'package:massar_center/exams/exam_engine.dart';
import 'package:massar_center/exams/exam_room_controller.dart';
import 'package:massar_center/features/exams/exam_admin_navigation.dart';
import 'package:massar_center/lan/lan_transport.dart';
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
  final standalonePages = <Map<String, dynamic>>[];
  final academicCatalogs = <Map<String, dynamic>>[];
  bool rejectStandaloneRevision = false;
  final rosters = <List<Map<String, dynamic>>>[];
  final adminAccess = <bool>[];
  final openedRooms = <Map<String, dynamic>>[];
  Completer<void>? stateRequested, releaseState, ackRequested, releaseAck;
  Completer<void>? openRequested, releaseOpen;
  Completer<void>? contextRequested, releaseContext;
  Future<void> Function()? beforeAck;
  Future<void> Function(bool enabled)? beforeAdminAccess;
  Future<void> Function(Map<String, dynamic>)? beforeOfflineAccess;
  bool adapterClosed = false;
  bool failState = false;
  bool refuseOfflineAccess = false;
  String? grantedStaffId;
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
        if (failState) throw const CenterException('Synthetic connection loss');
        if (stateRequested?.isCompleted == false) stateRequested!.complete();
        await releaseState?.future;
        return jsonDecode(jsonEncode(snapshot)) as Map<String, dynamic>;
      case '/api/center/catalog':
        academicCatalogs.add(
          jsonDecode(jsonEncode(body)) as Map<String, dynamic>,
        );
        return {
          'saved': true,
          'contextCount': (body!['contexts'] as List).length,
        };
      case '/api/center/standalone-rooms':
        return {
          'protocol': 1,
          'sourceId': snapshot['sourceId'],
          'rooms': standalonePages.isEmpty
              ? []
              : [
                  {'examId': standalonePages.first['examId']},
                ],
          'nextCursor': null,
          'hasMore': false,
        };
      case '/api/center/standalone-results':
        if (rejectStandaloneRevision && body!['exportRevision'] != null) {
          throw const CenterException('The source changed during paging');
        }
        final page = body!['afterAttemptId'] == null
            ? standalonePages.first
            : standalonePages.last;
        return jsonDecode(jsonEncode(page)) as Map<String, dynamic>;
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
      case '/api/center/context':
        if (contextRequested?.isCompleted == false) {
          contextRequested!.complete();
        }
        await releaseContext?.future;
        return {'contextId': 'prepared-context', 'prepared': true};
      case '/api/center/offline-access':
        await beforeOfflineAccess?.call(body!);
        final access = body!['revoke'] == true || refuseOfflineAccess
            ? <String, dynamic>{'allowed': false}
            : <String, dynamic>{
                'allowed': true,
                'remainingSeconds': 3600,
                'staffId': grantedStaffId ?? body['staffId'],
              };
        snapshot['offlineAccess'] = access;
        return access;
      default:
        throw StateError('Unexpected engine route $route');
    }
  }
}

/// The transport boundary is substituted; host authorization, SQLite receipts,
/// client snapshots and offline command rejection are production code.
class _HostTransport extends LanTransport {
  _HostTransport(this.host)
    : super(
        const LanEndpoint(
          hostId: 'test-host',
          name: 'Test host',
          address: '127.0.0.1',
          port: 1,
          certificateSha256:
              '0000000000000000000000000000000000000000000000000000000000000000',
        ),
        'test-pairing',
      );
  final CenterStore host;
  String? actor;
  bool available = true;
  void _requireConnection() {
    if (!available) {
      throw const LanConnectionException(
        'Cable removed',
        outcomeUnknown: false,
      );
    }
  }

  @override
  Future<Map<String, dynamic>> get(
    String path, {
    String? staffSession,
    String? stateVersion,
    bool statePatches = false,
    bool waitForChanges = false,
  }) async {
    _requireConnection();
    return host.snapshotLan(actor!);
  }

  @override
  Future<Map<String, dynamic>> post(
    String path,
    Map<String, dynamic> body, {
    String? staffSession,
    String? stateVersion,
    bool statePatches = false,
  }) async {
    _requireConnection();
    if (path == '/api/login') {
      actor = (await host.authenticateLan(
        body['name'] as String,
        body['password'] as String,
      )).id;
      return {
        ...await host.snapshotLan(actor!),
        'staffSession': 'test-session',
      };
    }
    if (path == '/api/exam-monitor') {
      final snapshot = ExamMonitorSnapshot.fromJson(body);
      await host.publishExamMonitorLan(
        deviceId: 'test-secondary',
        staffId: actor!,
        snapshot: snapshot,
        authorize: () => true,
      );
      return {
        'accepted': true,
        'sourceId': snapshot.sourceId,
        'examId': snapshot.examId,
        'version': snapshot.version,
      };
    }
    if (path == '/api/command') {
      return host.commandLan(
        deviceId: 'test-secondary',
        staffId: actor!,
        request: body,
      );
    }
    throw StateError('Unexpected transfer route $path');
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

  test(
    'academic catalog follows committed changes after unchanged polls',
    () async {
      exam = await store.saveAcademicActivity(exam.copyWith(maxScore: 10));
      engine.snapshot['academicCatalogProtocol'] = 1;
      await controller.refresh();
      await controller.refresh();
      expect(engine.academicCatalogs, hasLength(1));

      exam = await store.saveAcademicActivity(
        exam.copyWith(name: 'Updated exam'),
      );
      await controller.refresh();
      final contexts = engine.academicCatalogs.last['contexts'] as List;
      expect((contexts.single as Map)['activityName'], 'Updated exam');
      expect(engine.academicCatalogs, hasLength(2));
      await controller.refresh();
      expect(engine.academicCatalogs, hasLength(2));
    },
  );

  test('replacement exam source receives unchanged academic catalog', () async {
    exam = await store.saveAcademicActivity(exam.copyWith(maxScore: 10));
    engine.snapshot['academicCatalogProtocol'] = 1;
    engine.snapshot['bindings'] = <Map<String, dynamic>>[];
    await controller.refresh();
    engine.snapshot['sourceId'] = 'replacement-controller-engine';
    await controller.refresh();
    expect(engine.academicCatalogs, hasLength(2));
    expect(engine.academicCatalogs.last, engine.academicCatalogs.first);
  });

  test(
    'standalone paging publishes raw results without grades and caches academic labels without a roster',
    () async {
      exam = await store.saveAcademicActivity(
        exam.copyWith(allowedMaxScores: [10, 12]),
      );
      engine.snapshot['standaloneExportProtocol'] = 1;
      engine.snapshot['academicCatalogProtocol'] = 1;
      final rows = [
        for (var index = 0; index < 201; index++)
          StandaloneExamAttempt(
            attemptId: 'raw-${index.toString().padLeft(4, '0')}',
            code: 'unregistered-$index',
            name: 'Offline student',
            phone: '',
            status: 'final',
            score: 0,
            maxScore: 12,
            pending: 0,
            resultVersion: 1,
          ),
      ];
      for (final (chunk, after, next) in [
        (rows.take(200).toList(), null, rows[199].attemptId),
        (rows.skip(200).toList(), rows[199].attemptId, null),
      ]) {
        engine.standalonePages.add(
          StandaloneExamPage(
            sourceId: engine.snapshot['sourceId'] as String,
            examId: 'raw-room',
            title: 'Offline exam',
            roomState: 'closed',
            maxScore: 12,
            totalAttempts: rows.length,
            exportRevision: '1'.padLeft(64, '0'),
            attempts: chunk,
            afterAttemptId: after,
            nextCursor: next,
          ).toJson(),
        );
      }
      final beforeMoney = store.payments.map((row) => row.toJson()).toList();
      await controller.refresh();
      expect(controller.error, isNull);
      expect(store.standaloneExamRooms.single.attempts, hasLength(201));
      expect(store.standaloneExamRooms.single.complete, isTrue);
      expect(store.academics, isEmpty);
      expect(store.payments.map((row) => row.toJson()).toList(), beforeMoney);
      final context =
          (engine.academicCatalogs.single['contexts'] as List).single as Map;
      expect(context['groupId'], group.id);
      expect(context['sessionId'], session.id);
      expect(context['allowedMaxScores'], [10, 12]);
      expect(context.keys, isNot(contains('roster')));
      expect(
        jsonEncode(engine.academicCatalogs),
        isNot(contains(students.first.phone)),
      );
      final publishedAt = store.standaloneExamRooms.single.receivedAt;
      await controller.refresh();
      expect(store.standaloneExamRooms.single.receivedAt, publishedAt);
      expect(engine.academicCatalogs, hasLength(1));
    },
  );

  test(
    'a changed standalone revision cannot publish a partial raw room',
    () async {
      engine.snapshot['standaloneExportProtocol'] = 1;
      engine.rejectStandaloneRevision = true;
      engine.standalonePages.add(
        StandaloneExamPage(
          sourceId: engine.snapshot['sourceId'] as String,
          examId: 'changing-room',
          title: 'Unstable exam',
          roomState: 'closed',
          maxScore: 10,
          totalAttempts: 0,
          exportRevision: '2'.padLeft(64, '0'),
          attempts: [],
        ).toJson(),
      );
      await controller.refresh();
      expect(controller.error, contains('changed during paging'));
      expect(store.standaloneExamRooms, isEmpty);
      expect(store.academics, isEmpty);
    },
  );

  test(
    'cable removal keeps exam administration, blocks reception writes, and imports only after the host requests grades',
    () async {
      final transport = _HostTransport(store);
      final client = CenterStore.remote(
        transport,
        localDirectory: '${directory.path}/client',
      );
      await Directory('${directory.path}/client').create();
      addTearDown(() async {
        await client.close();
        transport.close();
      });
      await client.signIn('manager', password);
      controller.dispose();
      controller = ExamRoomController(
        engine: engine,
        store: client,
        clientOnly: true,
        pollInterval: const Duration(days: 1),
      );
      await controller.initialize();
      await controller.prepareHallContext(
        groupId: group.id,
        sessionId: session.id,
        activityId: exam.id,
      );
      expect(controller.offlinePrepared, isTrue);
      transport.available = false;
      await expectLater(
        client.refreshRemote(),
        throwsA(isA<LanConnectionException>()),
      );
      expect(controller.canAdmin, isTrue);
      expect(controller.canSync, isFalse);
      expect(await controller.openAdmin(), 'http://127.0.0.1:8766/');
      await expectLater(
        client.recordAttendance(
          EntryRequest(
            studentId: students[1].id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      engine.snapshot['results'] = [result()];
      engine.snapshot['monitors'] = [
        ExamMonitorSnapshot(
          sourceId: 'synthetic-controller-engine',
          examId: 'engine-exam',
          groupId: group.id,
          sessionId: session.id,
          activityId: exam.id,
          version: 1,
          roomState: 'closed',
          attempts: [
            ExamMonitorAttempt(
              attemptId: 'attempt-zero',
              studentId: students[0].id,
              status: 'final',
            ),
          ],
        ).toJson(),
      ];
      await controller.refresh();
      expect(engine.acknowledgments, isEmpty);
      expect(store.academics, isEmpty);
      transport.available = true;
      await client.refreshRemote();
      await controller.refresh();
      expect(store.integratedExamRooms, hasLength(1));
      expect(
        store.academics,
        isEmpty,
        reason: 'reconnection must not auto-import grades',
      );
      await store.requestExamResultImport(store.integratedExamRooms.single.id);
      await client.refreshRemote();
      await controller.refresh();
      expect(store.academics.single.score, 0);
      expect(store.integratedExamRooms.single.importPending, isFalse);
      expect(engine.acknowledgments, hasLength(1));
      await controller.refresh();
      expect(store.academics, hasLength(1));
    },
  );

  Future<({CenterStore client, _HostTransport transport})>
  connectedSecondary() async {
    final transport = _HostTransport(store);
    final client = CenterStore.remote(
      transport,
      localDirectory: '${directory.path}/client',
    );
    await Directory('${directory.path}/client').create();
    addTearDown(() async {
      await client.close();
      transport.close();
    });
    await client.signIn('manager', password);
    controller.dispose();
    controller = ExamRoomController(
      engine: engine,
      store: client,
      clientOnly: true,
      pollInterval: const Duration(days: 1),
    );
    await controller.initialize();
    return (client: client, transport: transport);
  }

  test(
    'native navigation retains the same offline employee but rejects a revoked lease',
    () async {
      final secondary = await connectedSecondary();
      await controller.enterLocalExamMode();
      await controller.openAdmin();
      var generation = controller.authorizationGeneration;
      final actor = secondary.client.currentUser!.id;
      secondary.transport.available = false;
      await expectLater(
        secondary.client.refreshRemote(),
        throwsA(isA<LanConnectionException>()),
      );
      expect(controller.authorizationGeneration, isNot(generation));
      expect(
        examAdminNavigationSessionValid(
          controller,
          generation: generation,
          actor: actor,
        ),
        isTrue,
      );

      secondary.transport.available = true;
      await secondary.client.refreshRemote();
      expect(
        examAdminNavigationSessionValid(
          controller,
          generation: generation,
          actor: actor,
        ),
        isFalse,
      );
      await controller.openAdmin();
      generation = controller.authorizationGeneration;
      expect(
        examAdminNavigationSessionValid(
          controller,
          generation: generation,
          actor: actor,
        ),
        isTrue,
      );
      secondary.client.signOut();
      expect(
        examAdminNavigationSessionValid(
          controller,
          generation: generation,
          actor: actor,
        ),
        isFalse,
      );
    },
  );

  for (final foreignStaff in [false, true]) {
    test(
      '${foreignStaff ? 'another employee grant' : 'refused offline grant'} cannot report the secondary ready',
      () async {
        await connectedSecondary();
        engine.refuseOfflineAccess = !foreignStaff;
        engine.grantedStaffId = foreignStaff ? 'different-staff' : null;
        await expectLater(
          controller.prepareHallContext(
            groupId: group.id,
            sessionId: session.id,
            activityId: exam.id,
          ),
          throwsA(
            isA<CenterException>().having(
              (failure) => failure.message,
              'message',
              contains('لم يتأكد حفظ تجهيز الساكند'),
            ),
          ),
        );
        expect(controller.offlinePrepared, isFalse);
      },
    );
  }

  for (final duringGrant in [false, true]) {
    test(
      'unplug during ${duringGrant ? 'offline grant' : 'context preparation'} reports connection loss without ready confirmation',
      () async {
        final secondary = await connectedSecondary();
        final requested = Completer<void>(), release = Completer<void>();
        if (duringGrant) {
          engine.beforeOfflineAccess = (_) async {
            requested.complete();
            await release.future;
          };
        } else {
          engine.contextRequested = requested;
          engine.releaseContext = release;
        }
        final preparation = controller.prepareHallContext(
          groupId: group.id,
          sessionId: session.id,
          activityId: exam.id,
        );
        final rejected = expectLater(
          preparation,
          throwsA(
            isA<CenterException>().having(
              (failure) => failure.message,
              'message',
              contains('انقطع الاتصال بالهوست أثناء التجهيز'),
            ),
          ),
        );
        await requested.future;
        secondary.transport.available = false;
        await expectLater(
          secondary.client.refreshRemote(),
          throwsA(isA<LanConnectionException>()),
        );
        release.complete();
        await rejected;
        expect(controller.offlinePrepared, isFalse);
        expect(controller.canAdmin, isFalse);
      },
    );
  }

  test(
    'delayed logout cannot revoke a newly prepared hall after another login',
    () async {
      final transport = _HostTransport(store);
      final client = CenterStore.remote(
        transport,
        localDirectory: '${directory.path}/client',
      );
      await Directory('${directory.path}/client').create();
      addTearDown(() async {
        await client.close();
        transport.close();
      });
      await client.signIn('manager', password);
      controller.dispose();
      controller = ExamRoomController(
        engine: engine,
        store: client,
        clientOnly: true,
        pollInterval: const Duration(days: 1),
      );
      await controller.initialize();
      await controller.prepareHallContext(
        groupId: group.id,
        sessionId: session.id,
        activityId: exam.id,
      );
      final logoutStarted = Completer<void>(),
          releaseLogout = Completer<void>();
      engine.beforeOfflineAccess = (command) async {
        if (command['revoke'] == true) {
          logoutStarted.complete();
          await releaseLogout.future;
        }
      };
      client.signOut();
      await logoutStarted.future;
      await client.signIn('manager', password);
      engine.contextRequested = Completer<void>();
      final prepared = controller.prepareHallContext(
        groupId: group.id,
        sessionId: session.id,
        activityId: exam.id,
      );
      await engine.contextRequested!.future;
      await Future<void>.delayed(Duration.zero);
      releaseLogout.complete();
      await prepared;
      expect(controller.offlinePrepared, isTrue);
      expect(engine.snapshot['offlineAccess']['allowed'], isTrue);
      await controller.refresh();
      expect(controller.offlinePrepared, isTrue);
    },
  );
  test(
    'restarted prepared secondary opens only the local exam without a center login',
    () async {
      controller.dispose();
      final client = CenterStore.clientWorkspace(
        directory: '${directory.path}/client',
      );
      addTearDown(client.close);
      engine.snapshot['offlineAccess'] = {
        'allowed': true,
        'remainingSeconds': 3600,
        'staffId': 'prepared-staff',
      };
      controller = ExamRoomController(
        engine: engine,
        store: client,
        clientOnly: true,
        pollInterval: const Duration(days: 1),
      );
      await controller.initialize();
      expect(client.currentUser, isNull);
      expect(client.canAssess, isFalse);
      expect(controller.canSync, isFalse);
      expect(await controller.openAdmin(), 'http://127.0.0.1:8766/');
    },
  );

  Future<void> prepareRoomCreation() async {
    engine.snapshot['exams'] = <Map<String, dynamic>>[];
    engine.snapshot['bindings'] = <Map<String, dynamic>>[];
    engine.snapshot['templates'] = [
      {'id': 'question-set', 'title': 'أسئلة جاهزة', 'maxScore': 10},
      {'id': 'other-total', 'title': 'أسئلة بدرجة مختلفة', 'maxScore': 20},
    ];
    await controller.refresh();
  }

  for (final role in [StaffRole.cashier, StaffRole.assistant]) {
    test(
      '${role.name} publishes entrants and failed reads never clear host alerts',
      () async {
        await store.saveStaff(name: role.name, password: password, role: role);
        store.signOut();
        await store.signIn(role.name, password);
        expect(store.canManage, isFalse);
        engine.snapshot['monitors'] = [
          {
            'sourceId': engine.snapshot['sourceId'],
            'examId': 'engine-exam',
            'groupId': group.id,
            'sessionId': session.id,
            'activityId': exam.id,
            'version': 1,
            'roomState': 'running',
            'attempts': [
              {
                'attemptId': 'entry-1',
                'studentId': students[1].id,
                'status': 'not_submitted',
              },
            ],
          },
        ];
        await controller.refresh();
        expect(
          store.integratedExamAttendanceAlerts.single.studentId,
          students[1].id,
        );
        engine.failState = true;
        await controller.refresh();
        expect(controller.error, isNotNull);
        expect(store.integratedExamAttendanceAlerts, hasLength(1));
        engine.failState = false;
        store.signOut();
        await store.signIn('manager', password);
        await store.recordAttendance(
          EntryRequest(
            studentId: students[1].id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        expect(store.integratedExamAttendanceAlerts, isEmpty);
        expect(store.payments, isEmpty);
      },
    );

    test(
      '${role.name} opens exams and synchronizes grades without management',
      () async {
        await store.saveStaff(name: role.name, password: password, role: role);
        store.signOut();
        await store.signIn(role.name, password);
        expect(store.canManage, isFalse);
        expect(controller.canSync, isTrue);
        expect(await controller.openAdmin(), 'http://127.0.0.1:8766/');
        engine.snapshot['results'] = [result()];
        await controller.refresh();
        expect(store.academics.single.score, 0);
        expect(engine.acknowledgments.single['attemptId'], 'attempt-zero');
        store.signOut();
        expect(controller.canSync, isFalse);
        await expectLater(
          controller.openAdmin(),
          throwsA(isA<CenterException>()),
        );
      },
    );
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
