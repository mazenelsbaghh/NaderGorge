import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/features/attendance/student_history_panel.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  final captureKey = GlobalKey();
  const chooser = MethodChannel('plugins.flutter.io/file_selector');
  late Directory directory;
  late CenterStore store;
  late String csvDestination;

  setUp(() async {
    await initializeDateFormatting('ar_EG');
    directory = await Directory.systemTemp.createTemp('massar-report-widget-');
    csvDestination = '${directory.path}/selected-report.csv';
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(chooser, (call) async {
          if (call.method == 'getSavePath') return csvDestination;
          throw UnsupportedError(
            'Unexpected file chooser method: ${call.method}',
          );
        });
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('الإدارة', 'test-password-2026');
    for (final catalog in [
      const CatalogEntry(name: 'فيزياء', kind: CatalogKind.subject),
      const CatalogEntry(name: 'النور', kind: CatalogKind.center),
      const CatalogEntry(name: 'الجنوب', kind: CatalogKind.center),
      const CatalogEntry(name: 'الثالث الثانوي', kind: CatalogKind.grade),
    ]) {
      await store.saveCatalog(catalog);
    }
    for (final center in store.catalogs.where(
      (entry) => entry.kind == CatalogKind.center,
    )) {
      await store.saveGroup(
        StudyGroup(
          name: center.name == 'النور' ? 'الأحد' : 'الثلاثاء',
          subjectId: store.catalogs
              .firstWhere((entry) => entry.kind == CatalogKind.subject)
              .id,
          centerId: center.id,
          gradeId: store.catalogs
              .firstWhere((entry) => entry.kind == CatalogKind.grade)
              .id,
          sessionPrice: 10000,
          packagePrice: 40000,
        ),
      );
    }
    for (final (code, name, group, discount) in [
      ('101', 'أحمد محمد', store.groups.first, 25),
      ('202', 'مينا سامح', store.groups.first, 0),
      ('303', 'يوسف بلا نشاط', store.groups.last, 0),
    ]) {
      await store.saveStudent(
        Student(
          code: code,
          name: name,
          groupIds: [group.id],
          discountPercent: discount,
          createdAt: DateTime.now(),
        ),
      );
    }
    await store.saveSession(
      LessonSession(
        groupId: store.groups.first.id,
        number: 7,
        startsAt: DateTime.now().add(const Duration(days: 1)),
        createdAt: DateTime.now(),
      ),
    );
    await store.collectAndAttend(
      EntryRequest(
        studentId: store.students.first.id,
        sessionId: store.sessions.single.id,
        mode: EntryMode.package,
      ),
    );
    await store.collectAndAttend(
      EntryRequest(
        studentId: store.students[1].id,
        sessionId: store.sessions.single.id,
        mode: EntryMode.single,
        method: 'تحويل',
      ),
    );
    await store.renewPackage(
      PackageRequest(
        studentId: store.students.first.id,
        groupId: store.groups.first.id,
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: store.students[1].id,
        sessionId: store.sessions.single.id,
        score: 0,
        homework: HomeworkStatus.complete,
        updatedAt: DateTime.now(),
      ),
    );
  });

  tearDown(() async {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(chooser, null);
    await TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    });
  });

  Future<void> openReports(
    WidgetTester tester,
    Size size, {
    bool dark = false,
  }) async {
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await (FontLoader('Tajawal')
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
        .load();
    await (FontLoader(
      'MaterialIcons',
    )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    await tester.pumpWidget(
      MaterialApp(
        debugShowCheckedModeBanner: false,
        theme: dark ? MassarTheme.dark : MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: RepaintBoundary(
            key: captureKey,
            child: Scaffold(
              body: ManagementWorkspace(store: store, onOpenAttendance: () {}),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, 'التقارير'));
    await tester.pumpAndSettle();
  }

  Future<void> chooseReport(WidgetTester tester, String label) async {
    await tester.tap(find.byKey(const Key('report-kind')));
    await tester.pumpAndSettle();
    await tester.tap(find.text(label).last);
    await tester.pumpAndSettle();
  }

  Future<void> scanCode(WidgetTester tester, String code) async {
    await tester.enterText(
      find.byKey(const Key('report-student-search')),
      code,
    );
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    await tester.pump();
    final boundary =
        captureKey.currentContext!.findRenderObject() as RenderRepaintBoundary;
    final image = await boundary.toImage(pixelRatio: 1);
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    final file = File('build/verification/$name.png');
    await file.parent.create(recursive: true);
    await file.writeAsBytes(bytes!.buffer.asUint8List());
    image.dispose();
  }

  Future<void> waitForExport(WidgetTester tester) async {
    for (var attempt = 0; attempt < 100; attempt++) {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      await tester.pump(const Duration(milliseconds: 50));
      if (await File(csvDestination).exists() &&
          find.text('جارٍ التصدير…').evaluate().isEmpty) {
        break;
      }
    }
    expect(await File(csvDestination).exists(), isTrue);
    expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
  }

  Future<void> acknowledgeExport(WidgetTester tester) async {
    await tester.pumpAndSettle();
    expect(find.text('جارٍ التصدير…'), findsNothing);
    expect(
      tester
          .widget<OutlinedButton>(
            find.widgetWithText(OutlinedButton, 'تصدير النتائج CSV'),
          )
          .onPressed,
      isNotNull,
    );
  }

  testWidgets(
    'student code and unassigned filters update table and export only actual displayed payments at desktop widths',
    (tester) async {
      await tester.runAsync(() async {
        await openReports(tester, const Size(1280, 900));
        expect(find.text('يوسف بلا نشاط'), findsOneWidget);
        expect(find.text('عدد الطلبة: 3'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await chooseReport(tester, 'المدفوعات');
        await scanCode(tester, '101');
        expect(find.text('عدد العمليات: 2'), findsOneWidget);
        expect(find.text('مينا سامح'), findsNothing);
        expect(find.byTooltip('عرض الإيصال'), findsNothing);
        await tester.tap(find.byKey(const Key('report-unassigned')));
        await tester.pumpAndSettle();
        expect(find.text('مدفوعات غير مرتبطة بحصة'), findsOneWidget);
        expect(find.text('عدد العمليات: 1'), findsOneWidget);
        await capture(tester, 'reports-filtered-1280');
        await tester.binding.setSurfaceSize(const Size(1440, 1000));
        await tester.pumpAndSettle();
        await capture(tester, 'reports-filtered');
        await tester.tap(
          find.widgetWithText(OutlinedButton, 'تصدير النتائج CSV'),
        );
        await waitForExport(tester);
        expect(await File(csvDestination).exists(), isTrue);
        final csv = await File(csvDestination).readAsString();
        expect(csv, contains('"أحمد محمد"'));
        expect(csv, contains('"غير مرتبطة بحصة"'));
        expect(csv, contains('"300.00"'));
        expect(csv, isNot(contains('"مينا سامح"')));
        expect(
          csv.split('\r\n').where((line) => line.isNotEmpty),
          hasLength(2),
        );
        await acknowledgeExport(tester);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'academic report uses code selection and displays zero as a recorded score without entry controls',
    (tester) async {
      await tester.runAsync(() async {
        await openReports(tester, const Size(1280, 900));
        await chooseReport(tester, 'الامتحانات');
        await scanCode(tester, '202');
        expect(find.text('درجات مرصودة: 1'), findsOneWidget);
        expect(find.text('مرصودة'), findsOneWidget);
        expect(find.text('0'), findsOneWidget);
        expect(find.text('أحمد محمد'), findsNothing);
        expect(find.byTooltip('عرض الإيصال'), findsNothing);
        expect(find.text('رصد'), findsNothing);
        await capture(tester, 'reports-exams-1280');
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'review report defaults to code checks and exports the chosen amount view',
    (tester) async {
      await tester.runAsync(() async {
        final student = store.students[1];
        final session = store.sessions.single;
        await store.checkPayment(
          studentId: student.id,
          sessionId: session.id,
          expectedAmount: store.paymentReviewAmountFor(student.id, session.id),
        );
        await store.savePaymentReview(
          ReviewRequest(
            studentId: student.id,
            sessionId: session.id,
            paymentId: store.payments
                .firstWhere((p) => p.studentId == student.id)
                .id,
            paperAmount: 9000,
          ),
        );
        await openReports(tester, const Size(1280, 900));
        await chooseReport(tester, 'مراجعة الدفع');
        await scanCode(tester, '202');
        expect(find.text('مراجعات الأكواد: 1'), findsOneWidget);
        expect(find.text('مسجل دفع حصة'), findsOneWidget);
        expect(find.text('الورق (جنيه مصري)'), findsNothing);
        expect(find.text('مطابق'), findsNothing);
        await capture(tester, 'reports-code-check-1280');
        await tester.tap(
          find.byType(DropdownButtonFormField<ReviewReportMode>),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('مبالغ').last);
        await tester.pumpAndSettle();
        expect(find.text('مراجعات المبالغ: 1'), findsOneWidget);
        expect(find.text('مراجعات الأكواد: 1'), findsNothing);
        expect(find.text('نقص في الورق'), findsOneWidget);
        await tester.tap(
          find.widgetWithText(OutlinedButton, 'تصدير النتائج CSV'),
        );
        await waitForExport(tester);
        final csv = await File(csvDestination).readAsString();
        expect(csv, contains('مراجعة مبلغ'));
        expect(csv, contains('"90.00"'));
        expect(csv, contains('"-10.00"'));
        expect(csv, isNot(contains('مراجعة كود')));
        await acknowledgeExport(tester);
        expect(tester.takeException(), isNull);
      });
    },
  );

  for (final dark in [false, true]) {
    testWidgets(
      'exam filters accept Arabic zero and block invalid bounds before export (${dark ? 'dark' : 'light'})',
      (tester) async {
        await tester.runAsync(() async {
          await store.saveAcademic(
            AcademicRecord(
              studentId: store.students.first.id,
              sessionId: store.sessions.single.id,
              examAbsent: true,
              updatedAt: DateTime.now(),
            ),
          );
          await openReports(tester, const Size(1280, 900), dark: dark);
          await chooseReport(tester, 'الامتحانات');
          expect(find.text('غائب عن الامتحان: 1'), findsOneWidget);
          expect(find.text('درجات مرصودة: 1'), findsOneWidget);
          await tester.enterText(
            find.byKey(const Key('report-exact-score')),
            '٠',
          );
          await tester.pumpAndSettle();
          expect(find.text('مينا سامح'), findsOneWidget);
          expect(find.text('أحمد محمد'), findsNothing);
          expect(find.text('درجات مرصودة: 1'), findsOneWidget);
          await capture(
            tester,
            'reports-score-zero-${dark ? 'dark' : 'light'}-1280',
          );
          await tester.tap(
            find.widgetWithText(OutlinedButton, 'تصدير النتائج CSV'),
          );
          await waitForExport(tester);
          final csv = await File(csvDestination).readAsString();
          expect(csv, contains('"مينا سامح"'));
          expect(csv, contains('"0"'));
          expect(csv, isNot(contains('"أحمد محمد"')));
          await acknowledgeExport(tester);
          final validDestination = csvDestination;
          csvDestination = '${directory.path}/invalid-bounds.csv';
          await tester.enterText(
            find.byKey(const Key('report-exact-score')),
            '',
          );
          await tester.enterText(
            find.byKey(const Key('report-min-score')),
            '٥',
          );
          await tester.enterText(
            find.byKey(const Key('report-max-score')),
            '٠',
          );
          await tester.pumpAndSettle();
          expect(
            find.text('أقل درجة يجب ألا تزيد عن أعلى درجة.'),
            findsOneWidget,
          );
          expect(
            tester
                .widget<OutlinedButton>(
                  find.widgetWithText(OutlinedButton, 'تصدير النتائج CSV'),
                )
                .onPressed,
            isNull,
          );
          await tester.enterText(
            find.byKey(const Key('report-min-score')),
            'abc',
          );
          await tester.pumpAndSettle();
          expect(
            find.text(
              'اكتب درجة رقمية في حقل أقل درجة، مثل 8.5. الصفر درجة فعلية.',
            ),
            findsOneWidget,
          );
          expect(await File(csvDestination).exists(), isFalse);
          expect(await File(validDestination).readAsString(), csv);
          await tester.tap(find.widgetWithText(TextButton, 'مسح الفلاتر'));
          await tester.pumpAndSettle();
          await tester.tap(
            find.byType(DropdownButtonFormField<ExamReportStatus>),
          );
          await tester.pumpAndSettle();
          await tester.tap(find.text('غائب أو لم تُرصد').last);
          await tester.pumpAndSettle();
          expect(find.text('أحمد محمد'), findsOneWidget);
          expect(find.text('مينا سامح'), findsNothing);
          expect(find.text('غائب عن الامتحان: 1'), findsOneWidget);
          expect(find.text('لم تُرصد: 0'), findsOneWidget);
          await tester.binding.setSurfaceSize(const Size(1440, 1000));
          await tester.pumpAndSettle();
          await capture(
            tester,
            'reports-not-taken-${dark ? 'dark' : 'light'}-1440',
          );
          expect(tester.takeException(), isNull);
        });
      },
    );
  }

  testWidgets(
    'refund movement filter exports actual payouts and student history preserves correction evidence',
    (tester) async {
      await tester.runAsync(() async {
        final student = store.students[1];
        final attendance = store.attendances.firstWhere(
          (entry) => entry.studentId == student.id,
        );
        await store.reverseEntry(
          attendanceId: attendance.id,
          reason: 'استرداد الحصة للتصحيح',
        );
        await openReports(tester, const Size(1280, 900));
        await chooseReport(tester, 'المدفوعات');
        await scanCode(tester, '202');
        expect(find.text('الصافي بعد الاسترداد: 0.00 ج'), findsOneWidget);
        await tester.tap(
          find.byType(DropdownButtonFormField<PaymentReportMode>),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('الاستردادات').last);
        await tester.pumpAndSettle();
        expect(find.text('عدد العمليات: 1'), findsOneWidget);
        expect(find.text('الصافي بعد الاسترداد: -100.00 ج'), findsOneWidget);
        await capture(tester, 'reports-refunds-1280');
        await tester.tap(
          find.widgetWithText(OutlinedButton, 'تصدير النتائج CSV'),
        );
        await waitForExport(tester);
        final csv = await File(csvDestination).readAsString();
        expect(csv, contains('"استرداد"'));
        expect(csv, contains('"-100.00"'));
        expect(csv, isNot(contains('"تحصيل"')));
        await acknowledgeExport(tester);
        await chooseReport(tester, 'الحضور والغياب');
        await tester.tap(
          find.byKey(const Key('report-attendance-corrections')),
        );
        await tester.pumpAndSettle();
        expect(find.text('سجل تصحيح الحضور'), findsOneWidget);
        expect(find.text('التصحيحات: 1'), findsOneWidget);
        expect(find.text('أُلغي التسجيل'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.light,
            home: Directionality(
              textDirection: TextDirection.rtl,
              child: Scaffold(
                body: StudentHistoryPanel(
                  store: store,
                  student: student,
                  expanded: true,
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('المدفوعات الأصلية'), findsOneWidget);
        expect(find.text('ملغاة'), findsNWidgets(2));
        expect(find.text('الاستردادات'), findsOneWidget);
        expect(find.text('سجل التصحيحات'), findsOneWidget);
        expect(find.text('أُلغي التسجيل'), findsOneWidget);
        expect(find.text('استرداد الحصة للتصحيح'), findsNWidgets(2));
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'closing category selector exports immutable class categories after student discounts change',
    (tester) async {
      await tester.runAsync(() async {
        final session = store.sessions.single;
        await store.closeSession(session.id);
        await store.finalizeSession(sessionId: session.id, actualCash: 30000);
        await store.saveStudent(
          store.students.first.copyWith(discountPercent: 100),
        );
        await openReports(tester, const Size(1280, 900));
        await chooseReport(tester, 'تقرير الحصة والتقفيلات');
        await tester.tap(
          find.byType(DropdownButtonFormField<ClosingReportMode>),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('تقفيلات محفوظة — الفئات').last);
        await tester.pumpAndSettle();
        expect(find.text('فئات الطلبة وقت التقفيل'), findsOneWidget);
        expect(find.text('باقة بخصم 25٪'), findsOneWidget);
        expect(find.text('حصة بالسعر الكامل'), findsOneWidget);
        expect(find.text('باقة بإعفاء 100٪'), findsNothing);
        expect(find.text('300.00'), findsOneWidget);
        await capture(tester, 'reports-closing-categories-1280');
        await tester.tap(
          find.widgetWithText(OutlinedButton, 'تصدير النتائج CSV'),
        );
        await waitForExport(tester);
        final csv = await File(csvDestination).readAsString();
        expect(csv, contains('باقة بخصم 25٪'));
        expect(csv, contains('"300.00"'));
        expect(csv, contains('عدد الطلبة داخل الفئة'));
        expect(csv, contains('عدد عمليات الدفع'));
        expect(csv, isNot(contains('باقة بإعفاء 100٪')));
        await acknowledgeExport(tester);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'assistant report menu excludes financial reports but keeps roster and academic reporting',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'مساعد',
          password: 'test-password-2026',
          role: StaffRole.assistant,
        );
        store.signOut();
        await store.signIn('مساعد', 'test-password-2026');
        await openReports(tester, const Size(1280, 900));
        await tester.tap(find.byKey(const Key('report-kind')));
        await tester.pumpAndSettle();
        final dropdown = find.byType(DropdownMenuItem<CenterReportKind>);
        // Options are checked through the visible popup, separate from navigation labels.
        expect(dropdown, findsWidgets);
        expect(find.text('المدفوعات'), findsNothing);
        expect(find.text('مراجعة الدفع'), findsNothing);
        expect(find.text('تقرير الحصة والتقفيلات'), findsNothing);
        expect(find.text('الامتحانات'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    },
  );
}
