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
import 'package:massar_center/features/management/corrections_page.dart';
import 'package:massar_center/features/management/closings_page.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/formatters.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  late LessonSession session;
  final captureKey = GlobalKey();

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      directory = await Directory.systemTemp.createTemp('massar-corrections-');
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
      await acknowledgeNotice(tester);
    } finally {
      store.removeListener(listener);
    }
    await tester.pumpAndSettle();
  }

  Future<void> lookup(WidgetTester tester, String code) async {
    await tester.enterText(
      find.byKey(const Key('correction-student-code')),
      code,
    );
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
  }

  Future<void> select(WidgetTester tester, String field, String choice) async {
    final picker = find.widgetWithText(DropdownButtonFormField<String>, field);
    await tester.ensureVisible(picker);
    await tester.tap(picker);
    await tester.pumpAndSettle();
    await tester.tap(find.text(choice).last);
    await tester.pumpAndSettle();
  }

  Future<void> chooseAction(
    WidgetTester tester,
    String choice, {
    String field = 'التصحيح المطلوب',
  }) async {
    final picker = find
        .ancestor(of: find.text(field), matching: find.byType(InputDecorator))
        .first;
    await tester.ensureVisible(picker);
    await tester.tap(picker);
    await tester.pumpAndSettle();
    await tester.tap(find.text(choice).last);
    await tester.pumpAndSettle();
  }

  testWidgets(
    'cashier reverses a mistaken single entry and refunds its discounted amount once with a durable reason',
    (tester) async {
      await tester.runAsync(() async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        final oldAttendanceId = store.attendances.single.id;
        final oldPaymentId = store.payments.single.id;
        await store.saveStaff(
          name: 'موظف الاستقبال',
          password: 'cashier-password-2026',
          role: StaffRole.cashier,
        );
        await store.signIn('موظف الاستقبال', 'cashier-password-2026');
        await open(
          tester,
          CorrectionsPage(
            store: store,
            initialStudentId: student.id,
            initialSessionId: session.id,
          ),
        );
        final payment = store.payments.single;
        await select(
          tester,
          'عملية الدفع المحددة',
          '${payment.description} · ${money(payment.netAmount)} · ${payment.method} · ${shortDate(payment.createdAt)}',
        );
        await chooseAction(tester, 'تصحيح وسيلة الدفع');
        final reasonField = find.byKey(const Key('correction-reason'));
        await tester.ensureVisible(reasonField);
        await tester.enterText(reasonField, 'مراجعة وسيلة التحصيل المسجلة');
        // A rejected real command must show its Arabic message and leave the
        // money and history untouched. Await the async UI callback itself.
        final failedAction =
            tester
                    .widget<FilledButton>(
                      find.byKey(const Key('apply-correction')),
                    )
                    .onPressed!
                as Future<void> Function();
        final failure = failedAction();
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'تأكيد التصحيح'));
        await acknowledgeNotice(tester, message: 'اختر وسيلة دفع مختلفة وصحيحة.');
        await failure;
        await tester.pumpAndSettle();
        expect(store.corrections, isEmpty);
        expect(store.refunds, isEmpty);
        await chooseAction(tester, 'إلغاء حضور مسجل بالخطأ');
        await tester.enterText(reasonField, '');
        final apply = find.byKey(const Key('apply-correction'));
        await tester.tap(apply);
        await tester.pumpAndSettle();
        expect(find.text('اكتب سبب التصحيح قبل المتابعة'), findsOneWidget);
        expect(store.payments, hasLength(1));
        final reason = find.byKey(const Key('correction-reason'));
        await tester.ensureVisible(reason);
        await tester.enterText(reason, 'تم تسجيل الطالب في الحصة بالخطأ');
        await select(tester, 'طريقة رد المبلغ', 'إنستاباي');
        await capture(tester, 'corrections-entry-preview');
        final activation = tester.widget<FilledButton>(apply).onPressed!;
        activation();
        activation();
        await tester.pumpAndSettle();
        expect(find.byType(AlertDialog), findsOneWidget);
        expect(
          find.descendant(
            of: find.byType(AlertDialog),
            matching: find.textContaining('رد ${money(7500)} بطريقة إنستاباي'),
          ),
          findsOneWidget,
        );
        expect(find.textContaining('إنستاباي'), findsWidgets);
        expect(store.payments, hasLength(1));
        await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
        await tester.pumpAndSettle();
        expect(store.attendances, hasLength(1));
        await tester.tap(apply);
        await tester.pumpAndSettle();
        await mutate(
          tester,
          () => tester.tap(find.widgetWithText(FilledButton, 'تأكيد التصحيح')),
        );
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.allPayments.single.id, oldPaymentId);
        expect(store.allPayments.single.netAmount, 7500);
        expect(store.allAttendances.single.id, oldAttendanceId);
        expect(store.refunds.single.amount, 7500);
        expect(store.refunds.single.method, 'إنستاباي');
        expect(store.refunds.single.staffId, store.currentUser!.id);
        expect(store.corrections, hasLength(1));
        expect(
          store.audit.any(
            (a) => a.description.contains('تم تسجيل الطالب في الحصة بالخطأ'),
          ),
          isTrue,
        );
        await capture(tester, 'corrections-entry-saved');
        expect(tester.takeException(), isNull);
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.allPayments.single.netAmount, 7500);
        expect(store.allAttendances.single.id, oldAttendanceId);
        expect(store.refunds.single.amount, 7500);
        expect(
          store.corrections.single.reason,
          'تم تسجيل الطالب في الحصة بالخطأ',
        );
      });
    },
  );

  testWidgets(
    'correction workspace denies assistant and fits the real desktop shell',
    (tester) async {
      await tester.runAsync(() async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.correctPaymentMethod(
          paymentId: store.payments.single.id,
          method: 'إنستاباي',
          reason: 'تحويل مسجل كنقدي بالخطأ',
        );
        await store.saveStaff(
          name: 'مساعد الرصد',
          password: 'assistant-password-2026',
          role: StaffRole.assistant,
        );
        await store.signIn('مساعد الرصد', 'assistant-password-2026');
        await open(
          tester,
          CorrectionsPage(
            store: store,
            initialStudentId: student.id,
            initialSessionId: session.id,
          ),
        );
        expect(
          find.text(
            'تصحيح الحضور والدفع والاسترداد متاح للإدارة والاستقبال فقط.',
          ),
          findsOneWidget,
        );
        expect(find.byKey(const Key('apply-correction')), findsNothing);
        expect(find.byKey(const Key('correction-student-code')), findsNothing);
        expect(store.payments, hasLength(1));
        await store.signIn('مدير السنتر', 'local-password-2026');
        for (final width in [1440.0, 1280.0]) {
          await open(
            tester,
            ManagementWorkspace(store: store, onOpenAttendance: () {}),
          );
          await tester.binding.setSurfaceSize(Size(width, 900));
          await tester.pumpAndSettle();
          await tester.ensureVisible(find.text('التصحيح والاسترداد'));
          await tester.tap(find.text('التصحيح والاسترداد'));
          await tester.pumpAndSettle();
          await lookup(tester, student.code);
          await select(
            tester,
            'الحصة المقصودة',
            'حصة ${session.number} · ${shortDate(session.startsAt)} · ${store.groupLabel(session.groupId)}',
          );
          await chooseAction(tester, 'إلغاء حضور مسجل بالخطأ');
          final action = find.byKey(const Key('apply-correction'));
          expect(action.hitTestable(), findsOneWidget);
          expect(tester.getRect(action).bottom, lessThanOrEqualTo(900));
          expect(find.text(student.name), findsOneWidget);
          expect(tester.takeException(), isNull);
          await capture(tester, 'corrections-shell-${width.toInt()}');
        }
      });
    },
  );

  for (final (count, originalAmount) in [(2, 12750), (3, 19500), (4, 30000)]) {
    testWidgets(
      'changing a newly purchased $count-session package to single refunds its full original purchase',
      (tester) async {
        await tester.runAsync(() async {
          await store.saveGroup(
            group.copyWith(twoSessionPrice: 17000, threeSessionPrice: 26000),
          );
          await store.collectAndAttend(
            EntryRequest(
              studentId: student.id,
              sessionId: session.id,
              mode: EntryMode.package,
              packageSessions: count,
            ),
          );
          await open(
            tester,
            CorrectionsPage(
              store: store,
              initialStudentId: student.id,
              initialSessionId: session.id,
            ),
          );
          await chooseAction(tester, 'تغيير طريقة دخول الحصة');
          await chooseAction(
            tester,
            'دفع الحصة',
            field: 'طريقة الدخول الصحيحة',
          );
          final reason = find.byKey(const Key('correction-reason'));
          await tester.ensureVisible(reason);
          await tester.enterText(
            reason,
            'الطالب طلب دفع الحصة وتم اختيار الشهر بالخطأ',
          );
          await tester.tap(find.byKey(const Key('apply-correction')));
          await tester.pumpAndSettle();
          expect(
            find.descendant(
              of: find.byType(AlertDialog),
              matching: find.textContaining(
                'رد دفعة الباقة الجديدة ${money(originalAmount)}',
              ),
            ),
            findsOneWidget,
          );
          expect(
            find.descendant(
              of: find.byType(AlertDialog),
              matching: find.textContaining(
                'سعر الحصة بعد الخصم: ${money(7500)}',
              ),
            ),
            findsOneWidget,
          );
          await mutate(
            tester,
            () =>
                tester.tap(find.widgetWithText(FilledButton, 'تأكيد التصحيح')),
          );
          expect(store.refunds.single.amount, originalAmount);
          expect(store.payments.single.netAmount, 7500);
          expect(store.allPayments.first.netAmount, originalAmount);
          expect(store.allPayments, hasLength(2));
          expect(store.packages, isEmpty);
          expect(store.attendances, hasLength(1));
          expect(store.corrections.single.voidsPackage, isTrue);
          expect(tester.takeException(), isNull);
        });
      },
    );
  }

  testWidgets(
    'saved cash mistake reopens explicitly and keeps its original closing when finalized again',
    (tester) async {
      await tester.runAsync(() async {
        await store.closeSession(session.id);
        await store.finalizeSession(
          sessionId: session.id,
          actualCash: 1000,
          notes: 'قيمة أُدخلت بالخطأ',
        );
        final original = store.closings.single;
        await open(tester, ClosingsPage(store: store));
        expect(
          tester
              .widget<TextFormField>(
                find.byKey(const Key('closing-actual-cash')),
              )
              .enabled,
          isFalse,
        );
        final reopen = find.byKey(const Key('reopen-financial-closing'));
        await tester.ensureVisible(reopen);
        await tester.tap(reopen);
        await tester.pumpAndSettle();
        await tester.tap(
          find.widgetWithText(FilledButton, 'تأكيد إعادة الفتح'),
        );
        await tester.pumpAndSettle();
        expect(find.text('اكتب سبب إعادة الفتح'), findsOneWidget);
        expect(store.closings, hasLength(1));
        await tester.enterText(
          find.byKey(const Key('reopen-closing-reason')),
          'تم إدخال قيمة النقدية بالخطأ',
        );
        await mutate(
          tester,
          () => tester.tap(
            find.widgetWithText(FilledButton, 'تأكيد إعادة الفتح'),
          ),
        );
        expect(store.closings, isEmpty);
        expect(store.allClosings.single.id, original.id);
        expect(store.allClosings.single.actualCash, 1000);
        expect(
          tester
              .widget<TextFormField>(
                find.byKey(const Key('closing-actual-cash')),
              )
              .enabled,
          isTrue,
        );
        await tester.enterText(
          find.byKey(const Key('closing-actual-cash')),
          '٠',
        );
        final finalize = find.byKey(const Key('finalize-session-finance'));
        await tester.ensureVisible(finalize);
        await tester.tap(finalize);
        await tester.pumpAndSettle();
        await mutate(
          tester,
          () => tester.tap(
            find.widgetWithText(FilledButton, 'حفظ التقفيلة النهائية'),
          ),
        );
        expect(store.closings.single.actualCash, 0);
        expect(store.allClosings, hasLength(2));
        expect(store.allClosings.first.actualCash, 1000);
        expect(
          store.corrections.single.action,
          CorrectionAction.closingReopened,
        );
        expect(store.corrections.single.reason, 'تم إدخال قيمة النقدية بالخطأ');
        expect(store.payments, isEmpty);
        expect(store.refunds, isEmpty);
        await capture(tester, 'closing-reopened-and-refinalized');
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'unused two-session package exposes refund and preserves its original quantity and discounted payment',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveGroup(group.copyWith(twoSessionPrice: 17000));
        await store.renewPackage(
          PackageRequest(
            studentId: student.id,
            groupId: group.id,
            sessionId: session.id,
            sessions: 2,
          ),
        );
        final payment = store.payments.single;
        await open(
          tester,
          CorrectionsPage(
            store: store,
            initialStudentId: student.id,
            initialSessionId: session.id,
          ),
        );
        await select(
          tester,
          'عملية الدفع المحددة',
          '${payment.description} · ${money(payment.netAmount)} · ${payment.method} · ${shortDate(payment.createdAt)}',
        );
        await chooseAction(tester, 'استرداد باقة لم تُستخدم');
        final reason = find.byKey(const Key('correction-reason'));
        await tester.ensureVisible(reason);
        await tester.enterText(reason, 'دفع باقة حصتين بالخطأ قبل الاستخدام');
        await tester.tap(find.byKey(const Key('apply-correction')));
        await tester.pumpAndSettle();
        expect(
          find.textContaining('رد مبلغ شراء الباقة ${money(12750)}'),
          findsWidgets,
        );
        await mutate(
          tester,
          () => tester.tap(find.widgetWithText(FilledButton, 'تأكيد التصحيح')),
        );
        expect(store.refunds.single.amount, 12750);
        expect(store.packages, isEmpty);
        expect(store.allPackages.single.totalSessions, 2);
        expect(store.allPackages.single.remaining, 2);
        expect(store.allPayments.single.id, payment.id);
        expect(store.attendances, isEmpty);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'single-entry correction previews and purchases the selected three-session independent price',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveGroup(group.copyWith(threeSessionPrice: 26000));
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await open(
          tester,
          CorrectionsPage(
            store: store,
            initialStudentId: student.id,
            initialSessionId: session.id,
          ),
        );
        await chooseAction(tester, 'تغيير طريقة دخول الحصة');
        await chooseAction(
          tester,
          'دخول بالباقة',
          field: 'طريقة الدخول الصحيحة',
        );
        await chooseAction(
          tester,
          '٣ حصص',
          field: 'عدد حصص الباقة عند شراء رصيد جديد',
        );
        final reason = find.byKey(const Key('correction-reason'));
        await tester.ensureVisible(reason);
        await tester.enterText(reason, 'اختيار الحصة بالخطأ بدل باقة ثلاث حصص');
        await tester.tap(find.byKey(const Key('apply-correction')));
        await tester.pumpAndSettle();
        expect(
          find.textContaining('باقة 3 حصص بقيمة ${money(19500)}'),
          findsWidgets,
        );
        await mutate(
          tester,
          () => tester.tap(find.widgetWithText(FilledButton, 'تأكيد التصحيح')),
        );
        expect(store.refunds.single.amount, 7500);
        expect(store.payments.single.netAmount, 19500);
        expect(store.allPayments, hasLength(2));
        expect(store.packages.single.totalSessions, 3);
        expect(store.packages.single.remaining, 2);
        expect(store.attendances, hasLength(1));
        expect(tester.takeException(), isNull);
      });
    },
  );
}
