import 'dart:convert';
import 'package:intl/date_symbol_data_local.dart';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/testing.dart';
import 'package:timezone/data/latest.dart' as tz;
import 'package:massar_parent/data/device_bridge.dart';
import 'package:massar_parent/data/models.dart';
import 'package:massar_parent/data/parent_api.dart';
import 'package:massar_parent/ui/academic_screens.dart';
import 'package:massar_parent/ui/dashboard.dart';
import 'package:massar_parent/ui/design_system.dart';
import 'package:massar_parent/ui/link_flow.dart';
import 'widget_test.dart' show fixture, envelope, controllerWith;

void main() {
  testWidgets('all parent routes render with shared texture, cards and motion', (
    tester,
  ) async {
    tz.initializeTimeZones();
    await initializeDateFormatting('ar');
    FlutterSecureStorage.setMockInitialValues({});
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(
          DeviceBridge.channel,
          (call) async =>
              call.method == 'legacyProfiles' ? <String, dynamic>{} : null,
        );
    tester.view.physicalSize = const Size(390, 844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    for (final entry in {
      'Tajawal': 'assets/fonts/Tajawal-Regular.ttf',
      'MaterialIcons': 'fonts/MaterialIcons-Regular.otf',
    }.entries) {
      await (FontLoader(
        entry.key,
      )..addFont(rootBundle.load(entry.value))).load();
    }
    final brandFonts = FontLoader('Tajawal');
    for (final weight in ['Medium', 'Bold', 'Black']) {
      brandFonts.addFont(rootBundle.load('assets/fonts/Tajawal-$weight.ttf'));
    }
    await brandFonts.load();
    final data = jsonDecode(jsonEncode(fixture('أحمد محمد'))) as Json;
    final lessons = data['watchLessons'] as List;
    lessons[0]['watchedSeconds'] = 1920;
    lessons[0]['lastWatchedAt'] = '2026-09-23T17:30:00Z';
    lessons.add({
      ...lessons[0] as Json,
      'lessonId': 'l2',
      'lessonTitle': 'الحركة',
      'startedVideos': 4,
      'completedVideos': 4,
      'isCompleted': true,
    });
    lessons.add({
      ...lessons[0] as Json,
      'lessonId': 'l3',
      'lessonTitle': 'الشغل والطاقة',
      'startedVideos': 0,
      'completedVideos': 0,
    });
    final exams = data['exams'] as List;
    exams[0].addAll({
      'packageName': 'الفيزياء',
      'packageId': 'p1',
      'termId': 't1',
      'termTitle': 'الترم الأول',
      'teacherName': 'أ. محمد علي',
      'teacherId': 'teacher',
      'mistakes': [
        {
          'questionText': 'وحدة قياس القوة هي؟',
          'studentAnswer': 'الجول',
          'correctAnswer': 'النيوتن',
          'writtenCorrection': 'تقاس القوة بالنيوتن، والطاقة بالجول.',
          'points': 5,
          'pointsAwarded': 1,
        },
      ],
    });
    exams.add({
      ...exams[0] as Json,
      'examId': 'e2',
      'examTitle': 'اختبار قوانين نيوتن',
      'status': 'NotStarted',
    });
    data['homeworks'] = [
      {
        'title': 'واجب الحركة',
        'isSubmitted': true,
        'submissionState': 'Graded',
        'grade': '8 من 10',
        'packageId': 'p1',
        'packageName': 'الفيزياء',
        'termId': 't1',
        'termTitle': 'الترم الأول',
        'mistakes': [],
      },
      {
        'title': 'واجب قوانين نيوتن',
        'isSubmitted': true,
        'submissionState': 'Submitted',
      },
      {'title': 'واجب الطاقة', 'isSubmitted': false},
    ];
    data['courses'] = [
      {
        'packageId': 'p1',
        'packageName': 'الفيزياء',
        'teacherId': 'teacher',
        'teacherName': 'أ. محمد علي',
        'terms': [
          {
            'termId': 't1',
            'termTitle': 'الترم الأول',
            'lessonCount': 12,
            'examCount': 4,
          },
        ],
      },
    ];
    data['balance'] = {
      'currentBalance': 250,
      'transactions': [
        {
          'description': 'تفعيل محتوى',
          'amount': -50,
          'balanceAfter': 250,
          'createdAt': '2026-09-24T08:00:00Z',
        },
        {
          'description': 'إضافة رصيد',
          'amount': 300,
          'balanceAfter': 300,
          'createdAt': '2026-09-23T08:00:00Z',
        },
      ],
    };
    data['warnings'] = [
      {
        'reason': 'يرجى مراجعة نتيجة اختبار الحركة',
        'severity': 'Medium',
        'createdAt': '2026-09-23T08:00:00Z',
      },
    ];
    final token =
        'header.${base64Url.encode(utf8.encode(jsonEncode({'StudentId': 'a'})))}.signature';
    final controller = controllerWith(
      MockClient((request) async {
        if (request.url.path.endsWith('verify-code')) {
          return envelope({'token': token, 'studentName': 'أحمد محمد'});
        }
        if (request.url.path.endsWith('app-config')) {
          return envelope({'updateRequired': false});
        }
        if (request.url.path.endsWith('notifications')) return envelope([]);
        return envelope(data);
      }),
    );
    controller.profiles = [
      LinkedStudent('a', 'أحمد محمد', token),
      const LinkedStudent('b', 'سارة محمد', 'other'),
    ];
    controller.active = controller.profiles.first;
    controller.details = StudentDetails(data);
    controller.lastUpdated = DateTime.utc(2026, 9, 24, 9);
    controller.notifications = [
      const AcademicRow({
        'id': 'n1',
        'title': 'واجب جديد متاح',
        'body': 'يمكنك متابعة تسليم الواجب من صفحة النتائج.',
        'isRead': false,
        'createdAt': '2026-09-24T09:00:00Z',
      }),
    ];
    final boundary = GlobalKey();
    Future<void> mount(Widget home) async {
      await tester.pumpWidget(
        RepaintBoundary(
          key: boundary,
          child: MaterialApp(
            debugShowCheckedModeBanner: false,
            locale: const Locale('ar'),
            supportedLocales: const [Locale('ar')],
            localizationsDelegates: GlobalMaterialLocalizations.delegates,
            theme: MassarTokens.theme(Brightness.light),
            builder: (context, child) => MassarBackdrop(child: child!),
            home: home,
          ),
        ),
      );
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 1200));
      await tester.runAsync(() async {
        await precacheImage(
          const AssetImage('assets/onboarding.png'),
          boundary.currentContext!,
        );
      });
      await tester.pump(const Duration(milliseconds: 1200));
    }

    Future<void> save(String name) async {
      expect(tester.takeException(), isNull, reason: name);
      await tester.runAsync(() async {
        final image =
            await (boundary.currentContext!.findRenderObject()!
                    as RenderRepaintBoundary)
                .toImage(pixelRatio: 2);
        final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
        final file = File('test-output/$name.png');
        await file.parent.create(recursive: true);
        await file.writeAsBytes(bytes!.buffer.asUint8List());
        image.dispose();
      });
    }

    final screens = <String, Widget>{
      '01-splash': const SplashScreen(),
      '02-welcome': LinkFlow(controller: controller),
      '03-link-code': LinkFlow(
        key: const ValueKey('code'),
        controller: controller,
        welcome: false,
      ),
      '05-dashboard': Dashboard(controller: controller),
      '07-lesson-detail': MassarPage(
        title: 'تفاصيل الحصة',
        body: ScreenBody(
          children: lessonDetailChildren(AcademicRow(lessons.first as Json)),
        ),
      ),
      '09-exam-detail': MassarPage(
        title: 'تفاصيل الاختبار',
        body: ScreenBody(
          children: assessmentDetailChildren(
            AcademicRow(exams.first as Json),
            false,
          ),
        ),
      ),
      '11-courses': MassarPage(
        title: 'الكورسات المسجلة',
        body: CourseCatalog(details: controller.details!),
      ),
    };
    for (final entry in screens.entries) {
      await mount(entry.value);
      await save(entry.key);
      if (entry.key == '03-link-code') {
        await tester.enterText(find.byType(TextFormField), 'A1B2C3');
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('التحقق من الرمز'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('التحقق من الرمز'));
        await tester.pumpAndSettle();
        await save('04-confirmation');
      }
      await tester.pumpWidget(const SizedBox());
    }
    for (final entry in {
      '06-lessons': 'الحصص',
      '08-results': 'النتائج',
      '10-homeworks': 'النتائج',
      '14-more': 'المزيد',
    }.entries) {
      await mount(Dashboard(controller: controller));
      await tester.tap(
        find.descendant(
          of: find.byType(MassarBottomNavigation),
          matching: find.text(entry.value),
        ),
      );
      await tester.pumpAndSettle();
      if (entry.key != '14-more') {
        await tester.tap(find.text('اختر المدرس').last);
        await tester.pumpAndSettle();
        await tester.tap(find.text('أ. محمد علي').last);
        await tester.pumpAndSettle();
      }
      if (entry.key == '10-homeworks') {
        await tester.tap(find.text('الواجبات'));
        await tester.pumpAndSettle();
      }
      await save(entry.key);
      await tester.pumpWidget(const SizedBox());
    }
    for (final entry in <String, void Function(BuildContext)>{
      '12-balance': (c) => openBalance(c, controller.details!),
      '13-notifications': (c) => openNotifications(c, controller),
      '15-students': (c) => openStudents(c, controller),
    }.entries) {
      await mount(
        Scaffold(
          body: Builder(
            builder: (context) => TextButton(
              onPressed: () => entry.value(context),
              child: const Text('open'),
            ),
          ),
        ),
      );
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      await save(entry.key);
      await tester.pumpWidget(const SizedBox());
    }
    controller.failure = const ParentFailure(
      'تعذر الاتصال. تحقق من الإنترنت وحاول مرة أخرى.',
    );
    await mount(Dashboard(controller: controller));
    await save('16-offline');
    await tester.pumpWidget(const SizedBox());
    controller.dispose();
  });
}
