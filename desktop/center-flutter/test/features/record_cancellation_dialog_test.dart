import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late Student student;
  late LessonSession session;
  late PaymentRecord payment;
  late AttendanceRecord attendance;
  final boundary = GlobalKey();
  final confirm = find.byKey(const Key('cancellation-confirm'));
  final reason = find.byKey(const Key('cancellation-reason'));

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      directory = await Directory.systemTemp.createTemp('massar-cancel-ui-');
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('الإدارة', 'cancel-ui-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(kind: kind, name: kind.name));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة مراجعة الإلغاء',
          subjectId: store.catalogs
              .firstWhere((row) => row.kind == CatalogKind.subject)
              .id,
          centerId: store.catalogs
              .firstWhere((row) => row.kind == CatalogKind.center)
              .id,
          gradeId: store.catalogs
              .firstWhere((row) => row.kind == CatalogKind.grade)
              .id,
          sessionPrice: 10000,
          packagePrice: 40000,
        ),
      );
      await store.saveStudent(
        Student(
          code: '701',
          name: 'مينا صاحب السجل',
          groupIds: [store.groups.single.id],
          discountPercent: 25,
          createdAt: DateTime.now().subtract(const Duration(days: 2)),
        ),
      );
      student = store.students.single;
      await store.saveSession(
        LessonSession(
          groupId: store.groups.single.id,
          number: 7,
          startsAt: DateTime.now().add(const Duration(minutes: 2)),
          createdAt: DateTime.now(),
        ),
      );
      session = store.sessions.single;
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      payment = store.allPayments.single;
      attendance = store.allAttendances.single;
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> open(WidgetTester tester, {bool dark = false}) async {
    await tester.binding.setSurfaceSize(const Size(1280, 800));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.runAsync(() async {
      await (FontLoader('Tajawal')
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
          .load();
      await (FontLoader(
        'MaterialIcons',
      )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    });
    await tester.pumpWidget(
      RepaintBoundary(
        key: boundary,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: AttendanceWorkspace(store: store, onExit: () {}),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('student-search')),
      student.code,
    );
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
  }

  Future<void> openCancellation(WidgetTester tester, bool isPayment) async {
    final action = find.byKey(
      ValueKey(
        isPayment
            ? 'cancel-payment-${payment.id}'
            : 'cancel-attendance-${attendance.id}',
      ),
    );
    await tester.ensureVisible(action);
    await tester.runAsync(() => tester.tap(action));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('record-cancellation-dialog')), findsOneWidget);
    expect(find.text('مينا صاحب السجل · كود 701'), findsOneWidget);
  }

  Future<void> chooseRelated(WidgetTester tester, bool isPayment) async {
    await tester.runAsync(
      () => tester.tap(find.byKey(const Key('cancellation-mode'))),
    );
    await tester.pumpAndSettle();
    await tester.runAsync(
      () => tester.tap(
        find
            .text(isPayment ? 'الدفع والحضور المرتبط' : 'الحضور والدفع المرتبط')
            .last,
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> mutate(
    WidgetTester tester,
    Future<void> Function() gesture,
    String message,
  ) async {
    await tester.runAsync(() async {
      final completed = Completer<void>();
      void changed() {
        if (!completed.isCompleted) completed.complete();
      }

      store.addListener(changed);
      try {
        await gesture();
        await completed.future.timeout(const Duration(seconds: 5));
        await acknowledgeNotice(tester, message: message);
      } finally {
        store.removeListener(changed);
      }
    });
    await tester.pumpAndSettle();
  }

  void expectCodeFocus(WidgetTester tester) {
    expect(
      tester
          .widget<TextField>(find.byKey(const Key('student-search')))
          .focusNode!
          .hasFocus,
      isTrue,
    );
    expect(tester.takeException(), isNull);
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    await tester.runAsync(() async {
      final image =
          await (boundary.currentContext!.findRenderObject()
                  as RenderRepaintBoundary)
              .toImage();
      final png = await image.toByteData(format: ui.ImageByteFormat.png);
      final file = File('build/verification/$name.png');
      await file.parent.create(recursive: true);
      await file.writeAsBytes(png!.buffer.asUint8List());
      image.dispose();
    });
  }

  for (final isPayment in [true, false]) {
    for (final both in [false, true]) {
      testWidgets(
        '${isPayment ? 'payment' : 'attendance'} cancellation ${both ? 'with related' : 'record only'} is explicit and durable from inline history',
        (tester) async {
          await open(tester, dark: both);
          expect(find.text('المدفوعات الأصلية'), findsOneWidget);
          await openCancellation(tester, isPayment);
          if (both) await chooseRelated(tester, isPayment);
          if (isPayment && !both) {
            await tester.ensureVisible(confirm);
            await tester.tap(confirm);
            await tester.pump();
            expect(find.text('اكتب سبب الإلغاء أولًا'), findsOneWidget);
            await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
            await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
            await tester.sendKeyEvent(LogicalKeyboardKey.enter);
            await tester.sendKeyEvent(LogicalKeyboardKey.f4);
            await tester.sendKeyEvent(LogicalKeyboardKey.f6);
            await tester.pumpAndSettle();
            expect(store.refunds, isEmpty);
            expect(store.payments, hasLength(1));
            expect(store.attendances, hasLength(1));
            expect(
              find.byKey(const Key('student-editor-dialog')),
              findsNothing,
            );
            expect(
              find.byKey(const Key('entry-confirmation-dialog')),
              findsNothing,
            );
          }
          await tester.enterText(reason, 'تسجيل خاطئ — مراجعة السجل');
          await tester.ensureVisible(confirm);
          await tester.pumpAndSettle();
          if (isPayment) {
            await capture(
              tester,
              both
                  ? 'cancel-payment-related-dark1280'
                  : 'cancel-payment-only-light1280',
            );
          }
          await mutate(
            tester,
            () async {
              await tester.tap(confirm);
              await tester.tap(confirm);
            },
            isPayment
                ? 'تم إلغاء الدفع وحفظ أثر العملية وسببها في السجل.'
                : 'تم إلغاء الحضور وحفظ أثر العملية وسببها في السجل.',
          );
          expect(
            find.byKey(const Key('record-cancellation-dialog')),
            findsNothing,
          );
          final refundExpected = isPayment || both;
          expect(store.refunds, hasLength(refundExpected ? 1 : 0));
          if (refundExpected) expect(store.refunds.single.amount, 7500);
          expect(store.payments, hasLength(refundExpected ? 0 : 1));
          expect(
            store.attendances.where(
              (record) => record.status != AttendanceStatus.absent,
            ),
            hasLength(isPayment && !both ? 1 : 0),
          );
          expect(store.allPayments.single.id, payment.id);
          expectCodeFocus(tester);
          if (isPayment && !both) {
            expect(
              find.text(
                'حضور الطالب مسجل — لا يوجد دفع ساري. حصّل الحساب دون تكرار الحضور.',
              ),
              findsOneWidget,
            );
            expect(
              tester
                  .widget<FilledButton>(find.byKey(const Key('collect-attend')))
                  .onPressed,
              isNotNull,
            );
          }
          if (!isPayment && !both) {
            expect(find.text('مسددة مسبقًا — تسجيل حضور · L'), findsOneWidget);
          }
          if (!both) {
            final entryButton = find.byKey(const Key('collect-attend'));
            await tester.ensureVisible(entryButton);
            await mutate(
              tester,
              () => tester.tap(entryButton),
              'تم تسجيل الحضور وحفظ الحساب. يمكنك استقبال الطالب التالي.',
            );
            expect(
              store.attendances.where(
                (record) => record.status != AttendanceStatus.absent,
              ),
              hasLength(1),
            );
            expect(store.payments, hasLength(1));
            expect(store.payments.single.netAmount, 7500);
            expect(store.allPayments, hasLength(isPayment ? 2 : 1));
            expect(store.packages, isEmpty);
            expectCodeFocus(tester);
          }
          await tester.pumpWidget(const SizedBox());
          await tester.runAsync(() async {
            await store.close();
            store = await CenterStore.open(directory: directory.path);
          });
          expect(
            store.allPayments.any((record) => record.id == payment.id),
            isTrue,
          );
          expect(store.refunds, hasLength(refundExpected ? 1 : 0));
          expect(
            store.corrections.any(
              (record) => record.reason == 'تسجيل خاطئ — مراجعة السجل',
            ),
            isTrue,
          );
        },
      );
    }
  }

  testWidgets(
    'retained single payment confirmation does not claim or consume separate eligible package balance',
    (tester) async {
      await tester.runAsync(() async {
        await store.cancelAttendance(
          attendanceId: attendance.id,
          reason: 'إلغاء حضور فقط',
        );
        await store.renewPackage(
          PackageRequest(
            studentId: student.id,
            groupId: session.groupId,
            sessionId: session.id,
          ),
        );
      });
      expect(store.eligibleRemainingFor(student.id, session.id), 4);
      final originalPayments = store.payments
          .map((record) => record.id)
          .toSet();
      await open(tester, dark: true);
      expect(find.text('مسددة مسبقًا — تسجيل حضور · L'), findsOneWidget);
      await tester.runAsync(() => tester.sendKeyEvent(LogicalKeyboardKey.keyL));
      await tester.pumpAndSettle();
      final dialog = find.byKey(const Key('entry-confirmation-dialog'));
      expect(dialog, findsOneWidget);
      expect(
        find.descendant(
          of: dialog,
          matching: find.text('مسددة مسبقًا — تسجيل حضور'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(of: dialog, matching: find.text('لا يوجد تحصيل جديد')),
        findsOneWidget,
      );
      expect(
        find.descendant(of: dialog, matching: find.text('4 حصص')),
        findsNWidgets(2),
      );
      expect(
        find.text(
          'السداد السابق محفوظ؛ لا يوجد دفع جديد ولا تُخصم حصة من الباقة.',
        ),
        findsOneWidget,
      );
      expect(find.text('دخول من الباقة السارية بدون دفع جديد'), findsNothing);
      expect(store.attendances, isEmpty);
      await capture(tester, 'retained-payment-confirmation-dark1280');
      await mutate(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
        'تم تسجيل مينا صاحب السجل · 701. مسددة مسبقًا؛ تم تسجيل الحضور دون دفع جديد ودون خصم من الباقة.',
      );
      expect(
        store.payments.map((record) => record.id).toSet(),
        originalPayments,
      );
      expect(store.packages.single.remaining, 4);
      expect(store.attendances, hasLength(1));
      expect(store.attendances.single.packageId, isNull);
      expectCodeFocus(tester);
    },
  );

  testWidgets(
    'financial closing rejection preserves cancellation reason and escape restores scanner focus without writes',
    (tester) async {
      await TestWidgetsFlutterBinding.instance.runAsync(() async {
        await store.closeSession(session.id);
        await store.finalizeSession(sessionId: session.id, actualCash: 7500);
      });
      await open(tester);
      await openCancellation(tester, true);
      await tester.enterText(reason, 'سبب يبقى بعد الرفض');
      await tester.ensureVisible(confirm);
      await tester.runAsync(() async {
        await tester.tap(confirm);
        await acknowledgeNotice(tester);
      });
      await tester.pumpAndSettle();
      expect(
        find.byKey(const Key('record-cancellation-dialog')),
        findsOneWidget,
      );
      expect(
        tester.widget<TextFormField>(reason).controller!.text,
        'سبب يبقى بعد الرفض',
      );
      expect(store.refunds, isEmpty);
      expect(store.payments, hasLength(1));
      expect(store.closings, hasLength(1));
      expect(tester.widget<FilledButton>(confirm).onPressed, isNotNull);
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.escape),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('record-cancellation-dialog')), findsNothing);
      expectCodeFocus(tester);
    },
  );

  testWidgets(
    'expanded history refreshes after cancellation and assistant never sees financial table or actions',
    (tester) async {
      await open(tester);
      await tester.runAsync(() => tester.tap(find.text('عرض السجل الكامل')));
      await tester.pumpAndSettle();
      final full = find.byType(AlertDialog).first;
      final action = find.descendant(
        of: full,
        matching: find.byKey(ValueKey('cancel-payment-${payment.id}')),
      );
      await tester.ensureVisible(action);
      await tester.runAsync(() async {
        await tester.tap(action);
        // The second pointer is intentionally blocked by the new modal barrier.
        await tester.tap(action, warnIfMissed: false);
      });
      await tester.pumpAndSettle();
      expect(
        find.byKey(const Key('record-cancellation-dialog')),
        findsOneWidget,
      );
      await tester.enterText(reason, 'دفعة مكررة');
      await tester.ensureVisible(confirm);
      await mutate(
        tester,
        () => tester.tap(confirm),
        'تم إلغاء الدفع وحفظ أثر العملية وسببها في السجل.',
      );
      expect(find.text('السجل الكامل'), findsOneWidget);
      expect(
        find.descendant(of: full, matching: find.text('ملغاة — مستردة')),
        findsOneWidget,
      );
      final canceledButton = find.descendant(
        of: full,
        matching: find.byKey(ValueKey('cancel-payment-${payment.id}')),
      );
      expect(tester.widget<TextButton>(canceledButton).onPressed, isNull);
      await tester.ensureVisible(find.text('رجوع للتحضير'));
      await tester.tap(find.text('رجوع للتحضير'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'المساعد',
          password: 'assistant-password',
          role: StaffRole.assistant,
        );
        await store.signIn('المساعد', 'assistant-password');
      });
      await tester.pumpAndSettle();
      expect(find.text('المدفوعات الأصلية'), findsNothing);
      expect(
        find.byKey(ValueKey('cancel-payment-${payment.id}')),
        findsNothing,
      );
      expect(
        find.byKey(ValueKey('cancel-attendance-${attendance.id}')),
        findsNothing,
      );
    },
  );
}
