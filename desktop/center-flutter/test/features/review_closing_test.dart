import 'dart:async';
import '../helpers/notice_helpers.dart';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/review_page.dart';
import 'package:massar_center/features/management/closings_page.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  late LessonSession session;
  final captureKey = GlobalKey();

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      directory = await Directory.systemTemp.createTemp(
        'massar-review-closing-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('مدير السنتر', 'local-password-2026');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(
          CatalogEntry(
            name: switch (kind) {
              CatalogKind.subject => 'الفيزياء',
              CatalogKind.center => 'سنتر النور',
              CatalogKind.grade => 'الثالث الثانوي',
            },
            kind: kind,
          ),
        );
      }
      await store.saveGroup(
        StudyGroup(
          name: 'الأحد',
          subjectId: store.catalogs
              .firstWhere((entry) => entry.kind == CatalogKind.subject)
              .id,
          centerId: store.catalogs
              .firstWhere((entry) => entry.kind == CatalogKind.center)
              .id,
          gradeId: store.catalogs
              .firstWhere((entry) => entry.kind == CatalogKind.grade)
              .id,
          sessionPrice: 10000,
          packagePrice: 40000,
        ),
      );
      group = store.groups.single;
      await store.saveStudent(
        Student(
          code: '123',
          name: 'أحمد محمد',
          discountPercent: 25,
          groupIds: [group.id],
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
      student = store.students.single;
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 1,
          startsAt: DateTime.now().subtract(const Duration(hours: 1)),
          createdAt: DateTime.now(),
        ),
      );
      session = store.sessions.single;
    }),
  );
  tearDown(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> open(WidgetTester tester, Widget page) async {
    await tester.binding.setSurfaceSize(const Size(1440, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await (FontLoader('Tajawal')
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
        .load();
    await (FontLoader(
      'MaterialIcons',
    )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    await tester.pumpWidget(const SizedBox());
    await tester.pumpWidget(
      RepaintBoundary(
        key: captureKey,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: Scaffold(backgroundColor: MassarColors.canvas, body: page),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> openPaper(WidgetTester tester) async {
    await open(tester, ReviewPage(store: store));
    await tester.tap(find.text('مقارنة مبلغ الورق'));
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

  Future<void> mutate(
    WidgetTester tester,
    Future<void> Function() gesture,
  ) async {
    final changed = Completer<void>();
    void listener() {
      if (!changed.isCompleted) changed.complete();
    }

    store.addListener(listener);
    try {
      await gesture();
      await changed.future.timeout(const Duration(seconds: 5));
      await Future<void>(() {});
    } finally {
      store.removeListener(listener);
    }
    await tester.pump(const Duration(milliseconds: 200));
    await tester.pumpAndSettle();
  }

  Future<void> lookup(WidgetTester tester, String code) async {
    await tester.enterText(find.byKey(const Key('review-student-code')), code);
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
    if (find.byKey(const Key('massar-notice-dialog')).evaluate().isNotEmpty) {
      expect(find.textContaining('لم نجد هذا الكود'), findsOneWidget);
      await acknowledgeNotice(tester);
    }
  }

  testWidgets(
    'refunded unassigned payment review retains original context and is read only',
    (tester) async {
      await tester.runAsync(() async {
        await store.renewPackage(
          PackageRequest(studentId: student.id, groupId: group.id),
        );
        final payment = store.payments.single;
        await store.savePaymentReview(
          ReviewRequest(
            studentId: student.id,
            paymentId: payment.id,
            paperAmount: 30000,
          ),
        );
        final originalReview = store.reviews.single;
        await store.refundPackage(
          packageId: payment.packageId!,
          reason: 'شراء باقة بالخطأ',
        );
        await openPaper(tester);
        expect(find.textContaining('دفعة مستردة'), findsOneWidget);
        await tester.tap(find.widgetWithText(TextButton, 'عرض'));
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('review-refunded-history')),
          findsOneWidget,
        );
        expect(find.byKey(const Key('save-payment-review')), findsNothing);
        expect(
          tester
              .widget<TextFormField>(
                find.byKey(const Key('review-paper-amount')),
              )
              .enabled,
          isFalse,
        );
        expect(find.textContaining('دفعة مستردة'), findsWidgets);
        expect(store.reviews.single.toJson(), originalReview.toJson());
        expect(store.reviews.single.paymentId, payment.id);
        expect(store.reviews.single.expectedAmount, 30000);
        expect(store.payments, isEmpty);
        expect(store.refunds.single.amount, 30000);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'receipt review requires an actual matching receipt and never creates revenue',
    (tester) async {
      await tester.runAsync(() async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        for (final (code, name) in [
          ('124', 'مينا عادل'),
          ('125', 'يوسف سامح'),
        ]) {
          await store.saveStudent(
            Student(
              code: code,
              name: name,
              groupIds: [group.id],
              createdAt: DateTime.now(),
            ),
          );
        }
        final packaged = store.students[1], unpaid = store.students[2];
        await store.renewPackage(
          PackageRequest(
            studentId: packaged.id,
            groupId: group.id,
            sessionId: session.id,
          ),
        );
        await store.collectAndAttend(
          EntryRequest(
            studentId: packaged.id,
            sessionId: session.id,
            mode: EntryMode.package,
          ),
        );
        await store.recordAttendance(
          EntryRequest(
            studentId: unpaid.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await open(
          tester,
          ManagementWorkspace(store: store, onOpenAttendance: () {}),
        );
        await tester.tap(find.text('مراجعة'));
        await tester.pumpAndSettle();
        final input = find.byKey(const Key('payment-check-code'));
        final amount = find.byKey(const Key('payment-review-custom-amount'));
        expect(find.byKey(const Key('review-paper-amount')), findsNothing);
        expect(find.byKey(const Key('save-payment-review')), findsNothing);
        expect(find.byType(RadioListTile<String>), findsNothing);
        await tester.enterText(input, 'unknown-code');
        await tester.testTextInput.receiveAction(TextInputAction.search);
        await acknowledgeNotice(
          tester,
          message: 'لم نجد طالبًا بهذا الكود أو الاسم أو الهاتف.',
        );
        expect(store.paymentChecks, isEmpty);
        expect(tester.widget<TextField>(input).focusNode!.hasFocus, isTrue);

        Future<void> check(
          String code,
          String receipt,
          StudentPaymentStatus status,
        ) async {
          await tester.enterText(amount, receipt);
          await tester.enterText(input, code);
          await mutate(
            tester,
            () => tester.testTextInput.receiveAction(TextInputAction.search),
          );
          expect(store.paymentChecks.last.status, status);
          final field = tester.widget<TextField>(input);
          expect(field.focusNode!.hasFocus, isTrue);
          expect(
            field.controller!.selection,
            TextSelection(baseOffset: 0, extentOffset: code.length),
          );
        }

        await check(student.code, '75', StudentPaymentStatus.paidSingle);
        await check(packaged.code, '400', StudentPaymentStatus.paidPackage);
        expect(store.paymentChecks, hasLength(2));
        expect(find.textContaining('تمت مراجعة 2 من 3 حاضر'), findsOneWidget);
        for (final code in [unpaid.code, student.code]) {
          await tester.enterText(amount, '75');
          await tester.enterText(input, code);
          await tester.testTextInput.receiveAction(TextInputAction.search);
          await acknowledgeNotice(tester);
          expect(store.paymentChecks, hasLength(2));
        }
        expect(
          store.paymentChecks.any((row) => row.studentId == unpaid.id),
          isFalse,
        );
        expect(store.reviews, isEmpty);
        expect(store.payments, hasLength(2));
        expect(store.attendances, hasLength(3));
        await capture(tester, 'payment-code-check-shell-1440');
        await tester.binding.setSurfaceSize(const Size(1280, 900));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        await capture(tester, 'payment-code-check-shell-1280');
        await tester.pumpWidget(const SizedBox());
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        expect(store.paymentChecks, hasLength(2));
        expect(
          store.paymentChecks.any((row) => row.studentId == unpaid.id),
          isFalse,
        );
        expect(store.reviews, isEmpty);
        expect(store.payments, hasLength(2));
      });
    },
  );

  testWidgets(
    'paper review finds name or code and reviews a selected operation without creating revenue',
    (tester) async {
      await tester.runAsync(() async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.renewPackage(
          PackageRequest(
            studentId: student.id,
            groupId: group.id,
            sessionId: session.id,
          ),
        );
        final single = store.payments.first;
        final package = store.payments.last;
        await openPaper(tester);
        await lookup(tester, student.name);
        expect(
          find.text(
            'لم نجد هذا الكود. المراجعة تبحث بكود الطالب الكامل، وليس بالاسم.',
          ),
          findsNothing,
        );
        expect(find.byType(RadioListTile<String>), findsNWidgets(3));
        await lookup(tester, student.code);
        final codeField = tester.widget<TextField>(
          find.byKey(const Key('review-student-code')),
        );
        expect(codeField.focusNode!.hasFocus, isTrue);
        expect(
          codeField.controller!.selection,
          const TextSelection(baseOffset: 0, extentOffset: 3),
        );
        expect(find.byType(RadioListTile<String>), findsNWidgets(3));
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('save-payment-review')),
              )
              .onPressed,
          isNull,
        );
        await tester.tap(
          find.byWidgetPredicate(
            (widget) =>
                widget is RadioListTile<String> && widget.value == single.id,
          ),
        );
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byKey(const Key('review-paper-amount')),
          '٧٠٫٥٠',
        );
        await tester.pumpAndSettle();
        expect(
          tester
              .widget<Text>(find.byKey(const Key('review-match-status')))
              .data,
          'نقص في الورق',
        );
        final save = find.byKey(const Key('save-payment-review'));
        await tester.ensureVisible(save);
        await mutate(tester, () => tester.tap(save));
        expect(store.reviews.single.paymentId, single.id);
        expect(store.reviews.single.paperAmount, 7050);
        expect(store.reviews.single.expectedAmount, 7500);
        expect(store.reviews.single.difference, -450);
        await capture(tester, 'payment-review');
        await tester.tap(
          find.byWidgetPredicate(
            (widget) =>
                widget is RadioListTile<String> && widget.value == package.id,
          ),
        );
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byKey(const Key('review-paper-amount')),
          '٣٠٠',
        );
        await tester.ensureVisible(save);
        await mutate(tester, () => tester.tap(save));
        expect(store.reviews.last.paymentId, package.id);
        expect(store.reviews.last.matched, isTrue);
        await tester.tap(
          find.widgetWithText(
            RadioListTile<String>,
            'بند في الورق بدون دفع مسجل',
          ),
        );
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byKey(const Key('review-paper-amount')),
          '١٠',
        );
        await tester.ensureVisible(save);
        // Both activations happen before a frame can disable the button.
        // Unlinked paper rows have no payment identity to deduplicate them.
        final rapidSave = tester.widget<FilledButton>(save).onPressed!;
        await mutate(tester, () async {
          rapidSave();
          rapidSave();
        });
        expect(store.reviews.last.paymentId, isNull);
        expect(store.reviews.last.matched, isFalse);
        expect(store.reviews.last.expectedAmount, 0);
        expect(store.reviews, hasLength(3));
        expect(store.payments, hasLength(2));
        expect(
          store.payments.fold(0, (sum, item) => sum + item.netAmount),
          37500,
        );
        final readyCode = tester.widget<TextField>(
          find.byKey(const Key('review-student-code')),
        );
        expect(readyCode.focusNode!.hasFocus, isTrue);
        expect(
          readyCode.controller!.selection,
          const TextSelection(baseOffset: 0, extentOffset: 3),
        );
        await store.saveStudent(
          Student(
            code: '124',
            name: 'مينا عادل',
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
        // A keyboard scanner replaces the selected code and invalidates the
        // previous student's operation immediately, before its Enter arrives.
        final selectedCode = readyCode.controller!.value;
        tester.testTextInput.updateEditingValue(
          TextEditingValue(
            text: selectedCode.text.replaceRange(
              selectedCode.selection.start,
              selectedCode.selection.end,
              '124',
            ),
            selection: const TextSelection.collapsed(offset: 3),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('save-payment-review')), findsNothing);
        await tester.testTextInput.receiveAction(TextInputAction.search);
        await tester.pumpAndSettle();
        expect(find.text('مينا عادل'), findsOneWidget);
        expect(tester.widget<FilledButton>(save).onPressed, isNull);
        expect(store.reviews, hasLength(3));
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'cash closing confirms attendance separately and keeps cash difference immutable',
    (tester) async {
      await tester.runAsync(() async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.saveStudent(
          Student(
            code: '124',
            name: 'مينا عادل',
            groupIds: [group.id],
            createdAt: DateTime.now().subtract(const Duration(days: 10)),
          ),
        );
        final other = store.students.last;
        await store.collectAndAttend(
          EntryRequest(
            studentId: other.id,
            sessionId: session.id,
            mode: EntryMode.package,
            method: 'إنستاباي',
          ),
        );
        await store.saveStudent(
          Student(
            code: '125',
            name: 'يوسف سامح',
            groupIds: [group.id],
            createdAt: DateTime.now().subtract(const Duration(days: 10)),
          ),
        );
        await store.savePaymentReview(
          ReviewRequest(
            studentId: student.id,
            paymentId: store.payments.first.id,
            paperAmount: 7000,
          ),
        );
        for (final value in [student, other]) {
          await store.checkPayment(
            studentId: value.id,
            sessionId: session.id,
            expectedAmount: store.paymentReviewAmountFor(value.id, session.id),
          );
        }
        await open(tester, ClosingsPage(store: store));
        expect(
          find.text(
            'مراجعة الحضور الحالي: 2 من 2 مُراجع · 0 إعفاء من رسوم المدرس · 0 عليه مبلغ متبقٍ أو بلا دفع',
          ),
          findsNWidgets(2),
        );
        await tester.enterText(
          find.byKey(const Key('closing-actual-cash')),
          '٧٠٫٥٠',
        );
        await tester.pumpAndSettle();
        expect(
          tester
              .widget<Text>(find.byKey(const Key('closing-difference-status')))
              .data,
          'عجز في النقدية',
        );
        expect(find.byKey(const Key('finalize-session-finance')), findsNothing);
        final closeAttendance = find.byKey(
          const Key('close-attendance-before-finance'),
        );
        await tester.ensureVisible(closeAttendance);
        await tester.tap(closeAttendance);
        await tester.pumpAndSettle();
        expect(store.sessions.single.status, SessionStatus.open);
        await mutate(
          tester,
          () => tester.tap(find.widgetWithText(FilledButton, 'إغلاق الحضور')),
        );
        expect(store.sessions.single.status, SessionStatus.closed);
        expect(store.closings, isEmpty);
        expect(
          store.attendances.where(
            (record) => record.status == AttendanceStatus.absent,
          ),
          hasLength(1),
        );
        await capture(tester, 'session-closing');
        final finalize = find.byKey(const Key('finalize-session-finance'));
        await tester.ensureVisible(finalize);
        await tester.tap(finalize);
        await tester.pumpAndSettle();
        expect(
          find.textContaining('بنود مراجعة غير مطابقة: 1'),
          findsOneWidget,
        );
        expect(
          find.textContaining('مراجعة الحضور الحالي: 2 من 2 مُراجع').last,
          findsOneWidget,
        );
        expect(store.closings, isEmpty);
        await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
        await tester.pumpAndSettle();
        expect(store.closings, isEmpty);
        await tester.tap(finalize);
        await tester.pumpAndSettle();
        await mutate(
          tester,
          () => tester.tap(
            find.widgetWithText(FilledButton, 'حفظ التقفيلة النهائية'),
          ),
        );
        final saved = store.closings.single;
        expect(saved.actualCash, 7050);
        expect(saved.summary.expectedCash, 7500);
        expect(saved.summary.totalCollected, 47500);
        expect(saved.difference, -450);
        expect(store.payments, hasLength(2));
        expect(find.byKey(const Key('finalize-session-finance')), findsNothing);
        expect(find.byKey(const Key('closing-actual-cash')), findsNothing);
        expect(find.text('النقدية المحفوظة في التقفيلة'), findsOneWidget);
        await capture(tester, 'session-closing-saved');
        await openPaper(tester);
        await lookup(tester, student.code);
        await tester.tap(
          find.byWidgetPredicate(
            (widget) =>
                widget is RadioListTile<String> &&
                widget.value == store.payments.first.id,
          ),
        );
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('save-payment-review')), findsNothing);
        expect(
          find.text('هذه الحصة مقفلة ماليًا. مراجعتها محفوظة للعرض فقط.'),
          findsOneWidget,
        );
        expect(store.reviews, hasLength(1));
        expect(tester.takeException(), isNull);
      });
    },
  );
  testWidgets(
    'paper review and closing fit the real management shell at desktop widths',
    (tester) async {
      await tester.runAsync(() async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.savePaymentReview(
          ReviewRequest(
            studentId: student.id,
            paymentId: store.payments.single.id,
            paperAmount: 7000,
          ),
        );
        await store.closeSession(session.id);
        await store.checkPayment(
          studentId: student.id,
          sessionId: session.id,
          expectedAmount: 7500,
        );
        for (final width in [1440.0, 1280.0]) {
          await open(
            tester,
            ManagementWorkspace(store: store, onOpenAttendance: () {}),
          );
          await tester.binding.setSurfaceSize(Size(width, 900));
          await tester.pumpAndSettle();
          await tester.tap(find.text('مراجعة'));
          await tester.pumpAndSettle();
          await tester.tap(find.text('مقارنة مبلغ الورق'));
          await tester.pumpAndSettle();
          await lookup(tester, student.code);
          await tester.tap(
            find.byWidgetPredicate(
              (widget) =>
                  widget is RadioListTile<String> &&
                  widget.value == store.payments.single.id,
            ),
          );
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
          final saveReview = find.byKey(const Key('save-payment-review'));
          expect(saveReview.hitTestable(), findsOneWidget);
          final actionRect = tester.getRect(saveReview);
          expect(actionRect.top, greaterThanOrEqualTo(0));
          expect(actionRect.bottom, lessThanOrEqualTo(900));
          await capture(tester, 'payment-review-shell-${width.toInt()}');
          await tester.tap(find.text('تقفيلة الحسابات'));
          await tester.pumpAndSettle();
          await tester.enterText(
            find.byKey(const Key('closing-actual-cash')),
            '٧٠٫٥٠',
          );
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
          await capture(tester, 'session-closing-shell-${width.toInt()}');
        }
      });
    },
  );
}
