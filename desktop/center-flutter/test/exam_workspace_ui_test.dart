import 'dart:io';
import 'dart:async';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/exam_engine.dart';
import 'package:massar_center/exams/exam_room_controller.dart';
import 'package:massar_center/features/auth/auth_screen.dart';
import 'package:massar_center/features/exams/exam_network_page.dart';
import 'package:massar_center/features/exams/exam_admin_view.dart';
import 'package:massar_center/shared/workspace_navigation.dart';
import 'package:massar_center/features/exams/exam_workspace.dart';
import 'package:massar_center/shared/theme.dart';

import 'helpers/ui_wait_helpers.dart';

/// The process/Windows boundary is fake. Authentication, grades and attendance
/// below use the production store backed by a fresh SQLite database.
class _UIEngine implements ExamEngine {
  final commands = <Map<String, dynamic>>[];
  final openedRooms = <Map<String, dynamic>>[];
  Map<String, dynamic> snapshot = {
    'protocol': 1,
    'sourceId': 'synthetic-ui-source',
    'templates': <Map<String, dynamic>>[],
    'exams': <Map<String, dynamic>>[],
    'bindings': <Map<String, dynamic>>[],
    'attempts': <Map<String, dynamic>>[],
    'results': <Map<String, dynamic>>[],
  };
  int starts = 0;
  Completer<void>? startDelay;
  final contexts = <Map<String, dynamic>>[];
  @override
  bool running = true;
  @override
  Future<void> start() async {
    starts++;
    await startDelay?.future;
    running = true;
  }

  @override
  Future<bool> reconnect() async => running;
  @override
  Future<void> close() async {}
  @override
  Future<Map<String, dynamic>> request(
    String route, [
    Map<String, dynamic>? body,
  ]) async {
    if (route == '/api/center/admin-access') {
      return {'adminUrl': 'http://127.0.0.1:8766/'};
    }
    if (route == '/api/center/context') {
      contexts.add(Map.of(body!));
      return {'contextId': 'prepared-ui-context'};
    }
    if (route == '/api/center/offline-access') {
      final access = body!['revoke'] == true
          ? <String, dynamic>{'allowed': false}
          : <String, dynamic>{
              'allowed': true,
              'remainingSeconds': 43200,
              'staffId': body['staffId'],
            };
      snapshot['offlineAccess'] = access;
      return access;
    }
    if (route == '/api/center/open') {
      openedRooms.add(Map.of(body!));
      (snapshot['exams'] as List).add({
        'id': 'new-linked-room',
        'title': 'الامتحان الجديد',
        'state': 'waiting',
      });
      (snapshot['bindings'] as List).add({
        'examId': 'new-linked-room',
        ...body,
      });
      return {'examId': 'new-linked-room'};
    }
    return snapshot;
  }

  @override
  Future<Map<String, dynamic>> network(Map<String, dynamic> command) async {
    commands.add(Map.of(command));
    return switch (command['action']) {
      'status' => {
        'ok': true,
        'configured': true,
        'adapterId': 'ethernet',
        'dhcpReady': true,
        'dnsReady': true,
        'portalReady': true,
        'portalMode': 'legacy',
        'studentUrl': 'http://10.77.0.1/',
      },
      'list-adapters' => {
        'adapters': [
          {
            'id': 'ethernet',
            'name': 'Omada Ethernet',
            'status': 'Up',
            'wireless': false,
          },
          {
            'id': 'wifi',
            'name': 'Host Wi-Fi',
            'status': 'Up',
            'wireless': true,
          },
        ],
      },
      'tethering-status' => {'ok': true, 'active': false},
      'hotspot-status' => {'active': true, 'message': 'شبكة الجهازين متاحة'},
      'hotspot-start' => {'ok': true, 'message': 'تم تشغيل شبكة الجهازين'},
      'configure' || 'portal-configure' => {'ok': true},
      _ => throw StateError('Unexpected network action ${command['action']}'),
    };
  }
}

class _Fixture {
  _Fixture(this.directory, this.store, this.engine, this.controller);
  final Directory directory;
  final CenterStore store;
  final _UIEngine engine;
  final ExamRoomController controller;
  bool _controllerDisposed = false;
  static const password = 'synthetic-ui-password';

  static Future<_Fixture> open({
    bool roster = false,
    StaffRole role = StaffRole.admin,
  }) async {
    final directory = await Directory.systemTemp.createTemp('exam-ui-');
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          const MethodChannel('plugins.flutter.io/path_provider'),
          (call) async {
            if (call.method != 'getApplicationSupportDirectory') {
              throw StateError(
                'Unexpected path provider request ${call.method}',
              );
            }
            return '${directory.path}/support';
          },
        );
    final store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('موظف التجربة', password);
    final engine = _UIEngine();
    final controller = ExamRoomController(
      engine: engine,
      store: store,
      pollInterval: const Duration(days: 1),
    );
    final fixture = _Fixture(directory, store, engine, controller);
    if (roster) await fixture.seedRoster();
    if (role != StaffRole.admin) {
      await store.saveStaff(name: 'موظف عادي', password: password, role: role);
      store.signOut();
      await store.signIn('موظف عادي', password);
    }
    return fixture;
  }

  Future<void> seedRoster() async {
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة التجربة',
        centerId: store.catalogs
            .firstWhere((e) => e.kind == CatalogKind.center)
            .id,
        subjectId: store.catalogs
            .firstWhere((e) => e.kind == CatalogKind.subject)
            .id,
        gradeId: store.catalogs
            .firstWhere((e) => e.kind == CatalogKind.grade)
            .id,
        sessionPrice: 6000,
      ),
    );
    final group = store.groups.single;
    final names = [
      'درجة صفر',
      'درجة يدوية',
      'منتظر المقالي',
      'لم يدخل الامتحان',
      'غائب عن الحصة',
    ];
    for (var i = 0; i < names.length; i++) {
      await store.saveStudent(
        Student(
          code: '0100$i',
          name: names[i],
          phone: '0100000000$i',
          groupIds: [group.id],
          createdAt: DateTime.now(),
        ),
      );
    }
    final students = [
      for (var i = 0; i < names.length; i++)
        store.students.firstWhere((s) => s.code == '0100$i'),
    ];
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'الشهر التجريبي',
        lessons: [const PreparedLesson(number: 1)],
      ),
    );
    final session = await store.startPreparedLesson(
      groupId: group.id,
      preparedLessonId: month.lessons.single.id,
    );
    final exam = await store.saveAcademicActivity(
      AcademicActivity(
        name: 'الامتحان المحدد',
        kind: AcademicActivityKind.exam,
        preparedLessonId: session.preparedLessonId,
        createdAt: DateTime.now(),
      ),
    );
    final other = await store.saveAcademicActivity(
      AcademicActivity(
        name: 'امتحان آخر لنفس الحصة',
        kind: AcademicActivityKind.exam,
        preparedLessonId: session.preparedLessonId,
        createdAt: DateTime.now(),
      ),
    );
    for (final student in students.take(4)) {
      await store.recordAttendance(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
    }
    for (var i = 0; i < 3; i++) {
      await store.saveAcademic(
        AcademicRecord(
          studentId: students[i].id,
          sessionId: session.id,
          activityId: i == 2 ? other.id : exam.id,
          score: i == 0 ? 0 : 5,
          updatedAt: DateTime.now(),
        ),
      );
    }
    controller.state = {
      'exams': [
        {'id': 'room-one', 'title': 'قاعة تجربة', 'state': 'closed'},
      ],
      'bindings': [
        {
          'examId': 'room-one',
          'groupId': group.id,
          'sessionId': session.id,
          'activityId': exam.id,
          'maxScore': 10,
        },
      ],
      'attempts': [
        {
          'examId': 'room-one',
          'attemptId': 'essay',
          'studentId': students[2].id,
          'status': 'pending_grading',
          'pending': 1,
        },
        {
          'examId': 'unrelated-room',
          'attemptId': 'unrelated',
          'studentId': students[3].id,
          'status': 'final',
          'pending': 0,
        },
        {
          'examId': 'room-one',
          'attemptId': 'manual',
          'studentId': students[1].id,
          'status': 'final',
          'resultVersion': 1,
        },
      ],
      'results': [
        {
          'examId': 'room-one',
          'attemptId': 'manual',
          'studentId': students[1].id,
          'version': 1,
          'status': 'final',
          'score': 8,
          'maxScore': 10,
        },
      ],
    };
    controller.resultProblems['manual:1'] =
        'درجة الطالب مرصودة يدويًا؛ راجع التعارض.';
  }

  void disposeController() {
    if (_controllerDisposed) return;
    _controllerDisposed = true;
    controller.dispose();
  }

  Future<void> close() async {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          const MethodChannel('plugins.flutter.io/path_provider'),
          null,
        );
    disposeController();
    await store.close();
    await directory.delete(recursive: true);
  }
}

/// Profile discovery uses actual filesystem I/O. Let that I/O complete before
/// waiting for the spinner animation; advancing fake frames alone cannot do it.
Future<void> _settleLocalProfile(WidgetTester tester) async {
  await tester.runAsync(() async {
    await tester.pump();
    await waitForUiCondition(
      tester,
      () => find
          .byType(LinearProgressIndicator, skipOffstage: false)
          .evaluate()
          .isEmpty,
      reason:
          'Local network inspection and Omada profile loading have finished.',
    );
  });
}

Future<void> _show(WidgetTester tester, Widget page, Size size) async {
  tester.view.physicalSize = size;
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    RepaintBoundary(
      key: const Key('exam-screenshot'),
      child: MaterialApp(
        theme: MassarTheme.dark,
        locale: const Locale('ar', 'EG'),
        supportedLocales: const [Locale('ar', 'EG')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        home: Directionality(textDirection: TextDirection.rtl, child: page),
      ),
    ),
  );
  await _settleLocalProfile(tester);
  expect(tester.takeException(), isNull);
}

Future<void> _capture(WidgetTester tester, String name) async {
  await tester.pumpAndSettle();
  final boundary = tester.renderObject<RenderRepaintBoundary>(
    find.byKey(const Key('exam-screenshot')),
  );
  await tester.runAsync(() async {
    final image = await boundary.toImage(pixelRatio: 1);
    try {
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      final output = Directory(
        '../../artifacts/private/windows-exam-integration',
      );
      await output.create(recursive: true);
      await File(
        '${output.path}/$name.png',
      ).writeAsBytes(bytes!.buffer.asUint8List(), flush: true);
    } finally {
      image.dispose();
    }
  });
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUpAll(() async {
    final font = FontLoader('Tajawal')
      ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
      ..addFont(rootBundle.load('assets/fonts/Tajawal-Medium.ttf'))
      ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf'));
    await font.load();
    await (FontLoader(
      'MaterialIcons',
    )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    for (final path in ['assets/logo.svg', 'assets/logo-light.svg']) {
      final loader = SvgAssetLoader(path);
      await svg.cache.putIfAbsent(
        loader.cacheKey(null),
        () => loader.loadBytes(null),
      );
    }
  });

  for (final role in [StaffRole.cashier, StaffRole.assistant]) {
    testWidgets(
      'host ${role.name} can open all exam tabs and logout closes access',
      (tester) async {
        late _Fixture fixture;
        await tester.runAsync(() async {
          fixture = await _Fixture.open(role: role);
        });
        addTearDown(() => tester.runAsync(fixture.close));
        await _show(
          tester,
          ExamWorkspace(controller: fixture.controller),
          const Size(1280, 800),
        );
        expect(fixture.store.canManage, isFalse);
        expect(find.text('إدارة الامتحان'), findsOneWidget);
        expect(find.text('التهيئة والبورتال'), findsOneWidget);
        expect(fixture.engine.commands, isEmpty);
        await tester.tap(find.text('تجهيز الحصة وكشف الطلاب').first);
        await tester.pumpAndSettle();
        expect(find.text('إنشاء قاعة مرتبطة'), findsOneWidget);
        await tester.tap(find.text('التهيئة والبورتال'));
        await _settleLocalProfile(tester);
        expect(find.text('منفذ كابل الـOmada'), findsOneWidget);
        expect(find.text('شبكة الهوست الخاصة (اختياري)'), findsNothing);
        fixture.store.signOut();
        await tester.pumpAndSettle();
        expect(
          find.text(
            'سجّل دخول الموظف لفتح الإدارة. خدمة الطلاب تستمر في الخلفية.',
          ),
          findsOneWidget,
        );
        expect(find.text('التهيئة والبورتال'), findsNothing);
        expect(find.text('تجهيز وصلة الامتحان'), findsNothing);
      },
    );
  }

  testWidgets(
    'native exam navigation keeps the web editor alive and waits for confirmed leave and destination',
    (tester) async {
      late _Fixture fixture;
      await tester.runAsync(() async {
        fixture = await _Fixture.open();
      });
      addTearDown(() => tester.runAsync(fixture.close));
      await _show(
        tester,
        ExamWorkspace(controller: fixture.controller),
        const Size(1280, 800),
      );
      expect(find.byType(MassarWorkspaceNavigation), findsOneWidget);
      expect(find.byType(SegmentedButton<int>), findsNothing);
      expect(find.byType(ExamAdminView, skipOffstage: false), findsNothing);
      expect(fixture.engine.commands, isEmpty);
      await tester.tap(find.text('إدارة الامتحان'));
      await tester.pumpAndSettle();
      final admin = tester.widget<ExamAdminView>(find.byType(ExamAdminView));
      final adminState = tester.state(find.byType(ExamAdminView));
      final navigation = admin.navigation!;
      bool selected(String id) => tester
          .widget<ListTile>(find.byKey(ValueKey('workspace-navigation-$id')))
          .selected;
      expect(selected('web:room'), isTrue);
      await tester.tap(find.text('مكتبة الامتحانات'));
      await tester.pumpAndSettle();
      expect(selected('web:room'), isTrue);
      expect(selected('web:exams'), isFalse);
      navigation.accept('exams');
      await tester.pumpAndSettle();
      expect(selected('web:exams'), isTrue);
      final owner = Object();
      final leave = Completer<bool>();
      navigation.bindLeaveGuard(owner, () => leave.future);
      await tester.tap(find.text('التهيئة والبورتال'));
      await tester.pump();
      expect(find.byType(ExamNetworkPage), findsNothing);
      expect(find.byType(ExamAdminView), findsOneWidget);
      expect(fixture.engine.commands, isEmpty);
      leave.complete(false);
      await tester.pumpAndSettle();
      expect(selected('web:exams'), isTrue);
      navigation.bindLeaveGuard(owner, () async => true);
      await tester.tap(find.text('التهيئة والبورتال'));
      await _settleLocalProfile(tester);
      expect(find.byType(ExamNetworkPage), findsOneWidget);
      expect(
        tester.state(find.byType(ExamAdminView, skipOffstage: false)),
        same(adminState),
      );
      // A hidden editor must never open an invisible leave confirmation.
      navigation.bindLeaveGuard(owner, () => Completer<bool>().future);
      await tester.tap(find.text('تحميل درجات الساكند').first);
      await tester.pumpAndSettle();
      expect(selected('import'), isTrue);
      await tester.tap(find.text('مكتبة الامتحانات'));
      await tester.pumpAndSettle();
      expect(tester.state(find.byType(ExamAdminView)), same(adminState));
      navigation.unbindLeaveGuard(owner);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'return to registration respects the visible editor leave decision',
    (tester) async {
      late _Fixture fixture;
      await tester.runAsync(() async {
        fixture = await _Fixture.open();
      });
      addTearDown(() => tester.runAsync(fixture.close));
      await _show(
        tester,
        Builder(
          builder: (context) => Scaffold(
            body: FilledButton(
              onPressed: () => Navigator.of(context).push<void>(
                MaterialPageRoute(
                  builder: (_) => ExamWorkspace(controller: fixture.controller),
                ),
              ),
              child: const Text('افتح الامتحانات'),
            ),
          ),
        ),
        const Size(1280, 800),
      );
      await tester.tap(find.text('افتح الامتحانات'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('إدارة الامتحان'));
      await tester.pumpAndSettle();
      final navigation = tester
          .widget<ExamAdminView>(find.byType(ExamAdminView))
          .navigation!;
      final owner = Object();
      navigation.bindLeaveGuard(owner, () async => false);
      await tester.binding.handlePopRoute();
      await tester.pumpAndSettle();
      expect(find.byType(ExamWorkspace), findsOneWidget);
      await tester.tap(find.byKey(const Key('exam-return-registration')));
      await tester.pumpAndSettle();
      expect(find.byType(ExamWorkspace), findsOneWidget);
      navigation.bindLeaveGuard(owner, () async => true);
      await tester.tap(find.byKey(const Key('exam-return-registration')));
      await tester.pumpAndSettle();
      expect(find.byType(ExamWorkspace), findsNothing);
      expect(find.text('افتح الامتحانات'), findsOneWidget);
      expect(fixture.store.currentUser, isNotNull);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'fresh secondary opens local exam without host, staff login or hall preparation',
    (tester) async {
      late _Fixture fixture;
      await tester.runAsync(() async {
        fixture = await _Fixture.open();
      });
      addTearDown(() => tester.runAsync(fixture.close));
      final client = CenterStore.clientWorkspace(
        directory: '${fixture.directory.path}/local-exam',
      );
      final engine = _UIEngine();
      final controller = ExamRoomController(
        engine: engine,
        store: client,
        clientOnly: true,
        pollInterval: const Duration(days: 1),
      );
      bool controllerDisposed = false;
      addTearDown(() async {
        if (!controllerDisposed) controller.dispose();
        await client.close();
      });
      await controller.initialize();
      expect(controller.canAdmin, isFalse);
      await _show(
        tester,
        AuthScreen(
          store: client,
          examWorkspaceAvailable: true,
          onOpenPreparedExams: () async {
            final navigator = Navigator.of(
              tester.element(find.byType(AuthScreen)),
            );
            await controller.enterLocalExamMode();
            await navigator.push<void>(
              MaterialPageRoute(
                builder: (_) => ExamWorkspace(controller: controller),
              ),
            );
          },
        ),
        const Size(720, 800),
      );
      expect(find.byKey(const Key('auth-workspace-choice')), findsNothing);
      final open = find.byKey(const Key('auth-prepared-exam'));
      await tester.ensureVisible(open);
      await tester.tap(open);
      await tester.pumpAndSettle();
      expect(controller.canAdmin, isTrue);
      expect(controller.canSync, isFalse);
      expect(controller.offlinePrepared, isFalse);
      expect(client.currentUser, isNull);
      expect(find.text('القاعة والتصحيح'), findsOneWidget);
      expect(find.text('الشبكة والـOmada'), findsOneWidget);
      expect(find.text('تجهيز الساكند للقاعة'), findsNothing);
      expect(find.byType(DropdownButtonFormField<String>), findsNothing);
      expect(find.byKey(const Key('auth-submit')), findsNothing);
      expect(await controller.openAdmin(), 'http://127.0.0.1:8766/');
      await _capture(tester, 'secondary-local-exam');
      await tester.tap(find.text('الشبكة والـOmada'));
      await _settleLocalProfile(tester);
      expect(
        tester.widget<ExamNetworkPage>(find.byType(ExamNetworkPage)).isHost,
        isFalse,
      );
      expect(find.text('تشغيل شبكة الهوست'), findsNothing);
      expect(
        await tester.runAsync(
          () => File(
            '${fixture.directory.path}/local-exam/center.sqlite',
          ).exists(),
        ),
        isFalse,
      );
      expect(engine.contexts, isEmpty);
      expect(tester.takeException(), isNull);
      controller.dispose();
      controllerDisposed = true;
    },
  );

  testWidgets(
    'secondary local exam entry is explicit and does not request employee sign in',
    (tester) async {
      late _Fixture fixture;
      await tester.runAsync(() async {
        fixture = await _Fixture.open();
      });
      addTearDown(() => tester.runAsync(fixture.close));
      final client = CenterStore.clientWorkspace(
        directory: '${fixture.directory.path}/explicit-exam',
      );
      final controller = ExamRoomController(
        engine: fixture.engine,
        store: client,
        clientOnly: true,
        pollInterval: const Duration(days: 1),
      );
      bool controllerDisposed = false;
      addTearDown(() async {
        if (!controllerDisposed) controller.dispose();
        await client.close();
      });
      fixture.engine.running = false;
      fixture.engine.startDelay = Completer<void>();
      await _show(
        tester,
        ExamWorkspace(controller: controller),
        const Size(720, 650),
      );
      expect(controller.canAdmin, isFalse);
      expect(find.text('افتح الامتحان على الساكند'), findsOneWidget);
      expect(find.text('افتح الامتحانات من حساب الموظف'), findsNothing);
      await tester.tap(find.text('فتح امتحان الساكند'));
      await tester.pump();
      expect(find.text('جارٍ فتح الامتحان…'), findsOneWidget);
      expect(find.text('القاعة والتصحيح'), findsNothing);
      fixture.engine.startDelay!.complete();
      await tester.pumpAndSettle();
      expect(controller.localExamMode, isTrue);
      expect(controller.canAdmin, isTrue);
      expect(controller.canSync, isFalse);
      expect(find.text('القاعة والتصحيح'), findsOneWidget);
      expect(find.text('تجهيز الساكند للقاعة'), findsNothing);
      expect(tester.takeException(), isNull);
      controller.dispose();
      controllerDisposed = true;
    },
  );

  for (final width in [720.0, 1280.0]) {
    testWidgets(
      'opens a room from the question library using center data at width $width',
      (tester) async {
        late _Fixture fixture;
        await tester.runAsync(() async {
          fixture = await _Fixture.open(roster: true);
        });
        addTearDown(() => tester.runAsync(fixture.close));
        fixture.engine.snapshot['templates'] = [
          {'id': 'ready-template', 'title': 'أسئلة الامتحان', 'maxScore': 10},
          {'id': 'wrong-total', 'title': 'أسئلة بدرجة مختلفة', 'maxScore': 20},
        ];
        fixture.controller.state = fixture.engine.snapshot;
        fixture.controller.resultProblems.clear();
        await _show(
          tester,
          Scaffold(body: ExamResultsPage(controller: fixture.controller)),
          Size(width, 800),
        );
        await tester.tap(find.byKey(const Key('exam-create-mode')));
        await tester.pumpAndSettle();
        Future<void> choose(int field, String text) async {
          final dropdown = find
              .byType(DropdownButtonFormField<String>)
              .at(field);
          await tester.ensureVisible(dropdown);
          await tester.tap(dropdown);
          await tester.pumpAndSettle();
          await tester.tap(find.textContaining(text).last);
          await tester.pumpAndSettle();
        }

        await choose(0, 'أسئلة بدرجة مختلفة');
        await choose(1, 'مجموعة التجربة');
        await choose(2, 'حصة 1');
        await choose(3, 'الامتحان المحدد');
        final submit = find.byKey(const Key('exam-link-submit'));
        expect(tester.widget<FilledButton>(submit).onPressed, isNull);
        expect(find.textContaining('بنفس الدرجة النهائية'), findsOneWidget);
        expect(fixture.engine.openedRooms, isEmpty);
        await choose(0, 'أسئلة الامتحان');
        await tester.ensureVisible(submit);
        await tester.pumpAndSettle();
        expect(tester.widget<FilledButton>(submit).onPressed, isNotNull);
        await _capture(tester, 'create-room-${width.toInt()}');
        await tester.tap(submit);
        await tester.pumpAndSettle();
        expect(fixture.engine.openedRooms, hasLength(1));
        final request = fixture.engine.openedRooms.single;
        expect(request['templateId'], 'ready-template');
        expect(request['groupId'], fixture.store.groups.single.id);
        expect(request['sessionId'], fixture.store.sessions.single.id);
        expect(
          request['activityId'],
          fixture.store.academicActivities
              .singleWhere((activity) => activity.name == 'الامتحان المحدد')
              .id,
        );
        expect(request['roster'], hasLength(5));
        expect(find.text('الامتحان الجديد • تجهيز'), findsOneWidget);
        expect(find.text('مرتبط بالحصة وكشف الطلاب'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );

    for (final exams in [false, true]) {
      testWidgets(
        'signed employee chooses ${exams ? 'exams' : 'reception'} at width $width',
        (tester) async {
          late _Fixture fixture;
          await tester.runAsync(() async {
            fixture = await _Fixture.open();
            fixture.store.signOut();
          });
          addTearDown(() => tester.runAsync(fixture.close));
          bool? selected;
          await _show(
            tester,
            AuthScreen(
              store: fixture.store,
              examWorkspaceAvailable: true,
              onSignedIn: (value) => selected = value,
            ),
            Size(width, 800),
          );
          expect(
            find.byKey(const Key('auth-workspace-choice')),
            findsOneWidget,
          );
          if (exams) {
            await tester.tap(find.text('الامتحانات'));
            await tester.pumpAndSettle();
          }
          await _capture(
            tester,
            'login-${exams ? 'exam' : 'center'}-${width.toInt()}',
          );
          await tester.enterText(
            find.byKey(const Key('auth-name')),
            'موظف التجربة',
          );
          await tester.enterText(
            find.byKey(const Key('auth-password')),
            _Fixture.password,
          );
          await tester.ensureVisible(find.byKey(const Key('auth-submit')));
          await tester.runAsync(() async {
            await tester.tap(find.byKey(const Key('auth-submit')));
            await waitForUiCondition(
              tester,
              () => selected != null,
              reason: 'Successful authentication opens the selected workspace.',
            );
          });
          expect(selected, exams);
          expect(fixture.store.currentUser, isNotNull);
          expect(fixture.engine.starts, 0);
          expect(tester.takeException(), isNull);
        },
      );
    }

    testWidgets(
      'actual attendees and exact named exam scores at width $width',
      (tester) async {
        late _Fixture fixture;
        await tester.runAsync(() async {
          fixture = await _Fixture.open(roster: true);
        });
        addTearDown(() => tester.runAsync(fixture.close));
        await _show(
          tester,
          Scaffold(body: ExamResultsPage(controller: fixture.controller)),
          Size(width, 800),
        );
        await tester.tap(find.byType(DropdownButtonFormField<String>).first);
        await tester.pumpAndSettle();
        await tester.tap(find.textContaining('قاعة تجربة').last);
        await tester.pumpAndSettle();
        expect(
          find.text('الحاضرون: 4   •   لهم درجة: 2   •   بدون درجة نهائية: 2'),
          findsOneWidget,
        );
        expect(find.text('منتظر المقالي'), findsOneWidget);
        expect(find.text('بانتظار تصحيح المقالي'), findsOneWidget);
        expect(find.text('لم يدخل الامتحان'), findsWidgets);
        expect(find.text('غائب عن الحصة'), findsNothing);
        expect(find.text('درجة صفر'), findsNothing);
        expect(find.text('مراجعة 1 نتيجة تحتاج تدخلًا'), findsOneWidget);
        await _capture(tester, 'results-${width.toInt()}');
        await tester.tap(find.text('مراجعة 1 نتيجة تحتاج تدخلًا'));
        await tester.pumpAndSettle();
        expect(find.text('نتائج تحتاج مراجعة'), findsOneWidget);
        expect(find.text('01001 • درجة يدوية'), findsOneWidget);
        expect(
          find.text('درجة الطالب مرصودة يدويًا؛ راجع التعارض.'),
          findsOneWidget,
        );
        expect(find.text('8 / 10'), findsOneWidget);
        await _capture(tester, 'conflicts-${width.toInt()}');
        await tester.tap(find.text('إغلاق'));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
      },
    );

    for (final host in [false, true]) {
      testWidgets(
        '${host ? 'host' : 'secondary'} Omada and portal setup at width $width',
        (tester) async {
          late _Fixture fixture;
          await tester.runAsync(() async {
            fixture = await _Fixture.open(
              role: host ? StaffRole.cashier : StaffRole.assistant,
            );
          });
          addTearDown(() => tester.runAsync(fixture.close));
          fixture.engine.running = false;
          final previousHitTestPolicy =
              WidgetController.hitTestWarningShouldBeFatal;
          WidgetController.hitTestWarningShouldBeFatal = true;
          addTearDown(
            () => WidgetController.hitTestWarningShouldBeFatal =
                previousHitTestPolicy,
          );
          Future<void> reveal(Finder finder) async {
            await tester.scrollUntilVisible(
              finder,
              250,
              scrollable: find.byType(Scrollable).first,
            );
            await Scrollable.ensureVisible(
              tester.element(finder),
              alignment: 0.5,
            );
            await _settleLocalProfile(tester);
          }

          try {
            await _show(
              tester,
              Scaffold(
                body: ExamNetworkPage(
                  controller: fixture.controller,
                  isHost: host,
                ),
              ),
              Size(width, 800),
            );
            expect(fixture.engine.starts, 0);
            expect(
              fixture.engine.commands.map((command) => command['action']),
              ['status', 'list-adapters', if (host) 'tethering-status'],
            );
            expect(fixture.store.canManage, isFalse);
            expect(
              find.text('تشغيل شبكة الهوست'),
              host ? findsOneWidget : findsNothing,
            );
            expect(find.text('شبكة الهوست الخاصة (اختياري)'), findsNothing);
            expect(find.text('منفذ كابل الـOmada'), findsOneWidget);
            await tester.tap(
              find.byType(DropdownButtonFormField<String>).first,
            );
            await _settleLocalProfile(tester);
            expect(find.textContaining('Host Wi-Fi'), findsNothing);
            await tester.tap(find.text('Omada Ethernet • Up').last);
            await _settleLocalProfile(tester);
            await reveal(find.text('http://10.77.0.1/'));
            expect(find.text('http://10.77.0.1/'), findsOneWidget);
            await reveal(find.text('تجهيز وصلة الامتحان'));
            if (!host) await tester.tap(find.text('تجهيز وصلة الامتحان'));
            await _settleLocalProfile(tester);
            expect(fixture.engine.starts, host ? 0 : 1);
            expect(
              fixture.engine.commands,
              host
                  ? isNot(
                      contains(
                        equals({
                          'action': 'configure',
                          'adapterId': 'ethernet',
                        }),
                      ),
                    )
                  : contains(
                      equals({'action': 'configure', 'adapterId': 'ethernet'}),
                    ),
            );
            await reveal(find.text('تطبيق إعداد البورتال'));
            await tester.tap(find.text('تطبيق إعداد البورتال'));
            await _settleLocalProfile(tester);
            expect(
              fixture.engine.commands,
              contains(
                equals({'action': 'portal-configure', 'mode': 'legacy'}),
              ),
            );
            await _capture(
              tester,
              'network-${host ? 'host' : 'secondary'}-${width.toInt()}',
            );
            await reveal(find.text('طريقة البوابة'));
            await tester.tap(find.byKey(const ValueKey('portal-legacy')));
            await _settleLocalProfile(tester);
            await tester.tap(find.text('HTTPS بشهادة موثوقة').last);
            await _settleLocalProfile(tester);
            await reveal(find.text('اختيار المفتاح الخاص'));
            expect(find.text('اسم النطاق الموجود في الشهادة'), findsOneWidget);
            expect(find.text('اختيار الشهادة'), findsOneWidget);
            await _capture(
              tester,
              'network-https-${host ? 'host' : 'secondary'}-${width.toInt()}',
            );
            expect(tester.takeException(), isNull);
          } finally {
            await tester.pumpWidget(const SizedBox.shrink());
            fixture.disposeController();
          }
        },
      );
    }
  }
}
