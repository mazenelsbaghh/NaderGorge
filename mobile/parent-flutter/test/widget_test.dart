import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:timezone/data/latest.dart' as tz;
import 'package:massar_parent/data/device_bridge.dart';
import 'package:massar_parent/data/models.dart';
import 'package:massar_parent/data/parent_api.dart';
import 'package:massar_parent/data/profile_store.dart';
import 'package:massar_parent/state/parent_controller.dart';
import 'package:massar_parent/ui/academic_screens.dart';
import 'package:massar_parent/ui/dashboard.dart';
import 'package:massar_parent/ui/design_system.dart';
import 'package:massar_parent/ui/link_flow.dart';

Json fixture(String name) => {
  'studentName': name,
  'grade': 'الصف الثالث الثانوي',
  'school': null,
  'attendance': {
    'totalLessons': 24,
    'watchedLessons': 18,
    'completionRate': 75,
    'watchProgressPercentage': 95,
  },
  'watchLessons': [
    {
      'lessonId': 'l1',
      'lessonTitle': 'قوانين نيوتن',
      'packageName': 'الفيزياء',
      'packageId': 'p1',
      'termId': 't1',
      'termTitle': 'الترم الأول',
      'teacherId': 'teacher',
      'teacherName': 'أ. محمد علي',
      'totalVideos': 4,
      'startedVideos': 3,
      'completedVideos': 1,
      'isCompleted': false,
    },
  ],
  'exams': [
    {
      'examId': 'e1',
      'examTitle': 'اختبار الحركة',
      'status': 'Passed',
      'score': 17,
      'totalScore': 20,
      'percentage': 85,
      'submittedAt': '2026-09-23T18:30:00Z',
      'mistakes': [],
    },
  ],
  'homeworks': [],
  'courses': [
    {
      'packageId': 'p1',
      'packageName': 'الفيزياء',
      'teacherId': 'teacher',
      'teacherName': 'أ. محمد علي',
      'terms': [],
    },
  ],
  'warnings': [],
  'balance': {'currentBalance': 250, 'transactions': []},
};
http.Response envelope(Object payload) => http.Response(
  jsonEncode({'success': true, 'data': payload}),
  200,
  headers: {'content-type': 'application/json; charset=utf-8'},
);
ParentController controllerWith(MockClient client) => ParentController(
  api: ParentApi(client: client),
  store: ProfileStore(),
  bridge: DeviceBridge(),
);

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  setUp(() {
    tz.initializeTimeZones();
    FlutterSecureStorage.setMockInitialValues({});
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          DeviceBridge.channel,
          (call) async => switch (call.method) {
            'legacyProfiles' => <String, dynamic>{},
            'deviceToken' => null,
            _ => null,
          },
        );
  });
  test(
    'migration preserves linked identity and does not resurrect removed profiles',
    () async {
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(
            DeviceBridge.channel,
            (call) async => {
              'profiles': jsonEncode([
                {'studentId': 'a', 'name': 'أحمد', 'token': 'secret'},
              ]),
              'activeId': 'a',
            },
          );
      final store = ProfileStore();
      final migrated = await store.load();
      expect((migrated['profiles'] as List).single['studentId'], 'a');
      await store.save([], null);
      expect((await store.load())['profiles'], isEmpty);
    },
  );
  test('corrupt legacy profiles never mark migration complete', () async {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          DeviceBridge.channel,
          (call) async => {
            'profiles': '[{"studentId":"a","name":"أحمد"}]',
            'activeId': 'a',
          },
        );
    final store = ProfileStore();
    await expectLater(store.load(), throwsFormatException);
    expect(await store.storage.read(key: ProfileStore.key), isNull);
  });
  test(
    'out-of-order student responses cannot replace the active student',
    () async {
      final lateResponse = Completer<http.Response>();
      final controller = controllerWith(
        MockClient((request) async {
          if (request.url.path.endsWith('app-config')) {
            return envelope({'updateRequired': false});
          }
          if (request.url.path.endsWith('notifications')) return envelope([]);
          if (request.headers['Authorization'] == 'Bearer a') {
            return lateResponse.future;
          }
          return envelope(fixture('سارة'));
        }),
      );
      controller.profiles = [
        const LinkedStudent('a', 'أحمد', 'a'),
        const LinkedStudent('b', 'سارة', 'b'),
      ];
      controller.active = controller.profiles.first;
      final first = controller.refresh();
      await Future<void>.delayed(Duration.zero);
      await controller.select(controller.profiles.last);
      lateResponse.complete(envelope(fixture('أحمد')));
      await first;
      expect(controller.active!.studentId, 'b');
      expect(controller.details!.text('studentName'), 'سارة');
      controller.dispose();
    },
  );
  test(
    'network failure retains same-student data but unauthorized clears it',
    () async {
      var mode = 0;
      final controller = controllerWith(
        MockClient((request) async {
          if (request.url.path.endsWith('app-config')) {
            return envelope({'updateRequired': false});
          }
          if (mode == 1) throw const SocketException('offline');
          if (mode == 2) return http.Response('', 401);
          if (request.url.path.endsWith('notifications')) return envelope([]);
          return envelope(fixture('أحمد'));
        }),
      );
      controller.active = const LinkedStudent('a', 'أحمد', 'a');
      await controller.refresh();
      mode = 1;
      await controller.refresh();
      expect(controller.details!.text('studentName'), 'أحمد');
      expect(controller.failure, isNotNull);
      mode = 2;
      await controller.refresh();
      expect(controller.details, isNull);
      expect(controller.failure!.unauthorized, isTrue);
      controller.dispose();
    },
  );
  test('unstarted and reconciliation exams never display a zero grade', () {
    for (final status in ['NotStarted', 'ManualReconciliationRequired']) {
      final row = AcademicRow({
        'status': status,
        'score': 0,
        'percentage': 0,
        'totalScore': 20,
      });
      expect(assessmentGrade(row, false), '—');
    }
    expect(
      AcademicRow({'totalVideos': 4, 'watchedVideos': 1}).startedVideos,
      isNull,
    );
    expect(
      AcademicRow({'totalVideos': 4, 'watchedVideos': 1}).videoProgress,
      .25,
    );
    expect(
      assessmentGrade(
        const AcademicRow({
          'submissionState': 'Submitted',
          'isSubmitted': true,
          'grade': '0',
        }),
        true,
      ),
      '—',
    );
  });
  testWidgets('linking verifies identity before persisting confirmation', (
    tester,
  ) async {
    final token =
        'header.${base64Url.encode(utf8.encode(jsonEncode({'StudentId': 'a'})))}.signature';
    final controller = controllerWith(
      MockClient((request) async {
        if (request.url.path.endsWith('verify-code')) {
          expect(jsonDecode(request.body)['trackingCode'], 'A1B2C3');
          return envelope({'token': token, 'studentName': 'أحمد'});
        }
        if (request.url.path.endsWith('app-config')) {
          return envelope({'updateRequired': false});
        }
        if (request.url.path.endsWith('notifications')) return envelope([]);
        return envelope(fixture('أحمد'));
      }),
    );
    await tester.pumpWidget(
      MaterialApp(
        locale: const Locale('ar'),
        supportedLocales: const [Locale('ar')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        theme: MassarTokens.theme(Brightness.light),
        home: LinkFlow(controller: controller, welcome: false),
      ),
    );
    await tester.enterText(find.byType(TextFormField), 'a1b2c3');
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('التحقق من الرمز'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('التحقق من الرمز'));
    await tester.pumpAndSettle();
    expect(find.text('تأكد من بيانات الطالب'), findsOneWidget);
    expect(controller.profiles, isEmpty);
    expect(await controller.store.storage.read(key: ProfileStore.key), isNull);
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.text('تأكيد الربط'), 200);
    await tester.pumpAndSettle();
    await tester.tap(find.text('تأكيد الربط'));
    await tester.pumpAndSettle();
    expect(controller.active!.studentId, 'a');
    expect((await controller.store.load())['activeId'], 'a');
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    controller.dispose();
  });
  for (final scale in [1.0, 1.6]) {
    testWidgets(
      'academic screens remain readable at 360px with text scale $scale',
      (tester) async {
        tester.view.physicalSize = const Size(360, 800);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        await tester.pumpWidget(
          MaterialApp(
            locale: const Locale('ar'),
            localizationsDelegates: GlobalMaterialLocalizations.delegates,
            supportedLocales: const [Locale('ar')],
            theme: MassarTokens.theme(Brightness.light),
            builder: (context, child) => MediaQuery(
              data: MediaQuery.of(
                context,
              ).copyWith(textScaler: TextScaler.linear(scale)),
              child: child!,
            ),
            home: Scaffold(
              body: AcademicList(
                details: StudentDetails(fixture('أحمد')),
                lessons: true,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        await tester.tap(find.text('اختر المدرس').last);
        await tester.pumpAndSettle();
        await tester.tap(find.text('أ. محمد علي').last);
        await tester.pumpAndSettle();
        await tester.scrollUntilVisible(find.text('قوانين نيوتن'), 200);
        expect(find.text('قوانين نيوتن'), findsOneWidget);
        await tester.tap(find.text('قوانين نيوتن'));
        await tester.pumpAndSettle();
        expect(find.text('اكتمال الفيديوهات'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
  testWidgets(
    'teacher selection only exposes purchased-course teachers and rows',
    (tester) async {
      final data = fixture('أحمد');
      data['courses'] = [
        {
          'packageId': 'p1',
          'teacherId': 'teacher',
          'teacherName': 'مدرس الفيزياء',
        },
        {
          'packageId': 'p2',
          'teacherId': 'math',
          'teacherName': 'مدرس الرياضيات',
        },
      ];
      data['watchLessons'] = [
        ...(data['watchLessons'] as List),
        {
          'lessonId': 'math1',
          'lessonTitle': 'الجبر',
          'packageId': 'p2',
          'teacherId': 'math',
          'teacherName': 'مدرس الرياضيات',
        },
        {
          'lessonId': 'foreign',
          'lessonTitle': 'محتوى غير مشترى',
          'packageId': 'p3',
          'teacherId': 'foreign',
          'teacherName': 'مدرس غير مشترك',
        },
      ];
      await tester.pumpWidget(
        MaterialApp(
          locale: const Locale('ar'),
          supportedLocales: const [Locale('ar')],
          localizationsDelegates: GlobalMaterialLocalizations.delegates,
          home: Scaffold(
            body: AcademicList(details: StudentDetails(data), lessons: true),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('قوانين نيوتن'), findsNothing);
      expect(find.text('الجبر'), findsNothing);
      await tester.tap(find.text('اختر المدرس').last);
      await tester.pumpAndSettle();
      expect(find.text('مدرس غير مشترك'), findsNothing);
      expect(find.text('مدرس الرياضيات'), findsOneWidget);
      await tester.tap(find.text('مدرس الفيزياء').last);
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('قوانين نيوتن'), 200);
      expect(find.text('الجبر'), findsNothing);
      expect(find.text('محتوى غير مشترى'), findsNothing);
      await tester.scrollUntilVisible(find.text('مدرس الفيزياء'), -200);
      await tester.tap(find.text('مدرس الفيزياء').last);
      await tester.pumpAndSettle();
      await tester.tap(find.text('مدرس الرياضيات').last);
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(find.text('الجبر'), 200);
      expect(find.text('قوانين نيوتن'), findsNothing);
    },
  );

  testWidgets(
    'reduced motion presents progress and content without entrance ticks',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: MediaQuery(
            data: const MediaQueryData(disableAnimations: true),
            child: const Scaffold(
              body: Column(
                children: [
                  Entrance(child: Text('جاهز')),
                  ProgressArc(progress: .25, caption: 'التقدم', detail: ''),
                ],
              ),
            ),
          ),
        ),
      );
      await tester.pump();
      expect(find.text('جاهز'), findsOneWidget);
      expect(find.text('25%'), findsOneWidget);
      expect(tester.binding.transientCallbackCount, 0);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'approved splash and dashboard render from real reusable widgets',
    (tester) async {
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      for (final weight in ['Regular']) {
        final loader = FontLoader('Tajawal')
          ..addFont(rootBundle.load('assets/fonts/Tajawal-$weight.ttf'));
        await loader.load();
      }
      final controller = controllerWith(MockClient((r) async => envelope([])));
      controller.active = const LinkedStudent('a', 'أحمد محمد', 'token');
      controller.details = StudentDetails(fixture('أحمد محمد'));
      final icons = FontLoader('MaterialIcons')
        ..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'));
      await icons.load();
      final boundaryKey = GlobalKey();
      for (final entry in <String, Widget>{
        'splash': const SplashScreen(),
        'dashboard': Dashboard(controller: controller),
      }.entries) {
        await tester.pumpWidget(
          MaterialApp(
            locale: const Locale('ar'),
            supportedLocales: const [Locale('ar')],
            localizationsDelegates: GlobalMaterialLocalizations.delegates,
            theme: MassarTokens.theme(Brightness.light),
            home: RepaintBoundary(key: boundaryKey, child: entry.value),
          ),
        );
        await tester.pump(const Duration(milliseconds: 1200));
        await tester.pump();
        expect(tester.takeException(), isNull);
        if (entry.key == 'dashboard') {
          expect(find.text('95%'), findsOneWidget);
          expect(find.text('18 من 24 حصة مكتملة'), findsOneWidget);
        }
        await tester.runAsync(() async {
          final boundary =
              boundaryKey.currentContext!.findRenderObject()!
                  as RenderRepaintBoundary;
          final image = await boundary.toImage(pixelRatio: 2);
          final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
          final file = File('test-output/${entry.key}.png');
          await file.parent.create(recursive: true);
          await file.writeAsBytes(bytes!.buffer.asUint8List());
          image.dispose();
        });
      }
      await tester.pumpWidget(const SizedBox());
      controller.dispose();
    },
  );
}
