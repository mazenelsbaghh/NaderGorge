import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/student_history_panel.dart';
import 'package:massar_center/features/management/review_page.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/reception_ui_fixture.dart';

void main() {
  Future<void> withStore(
    WidgetTester tester,
    Future<void> Function(CenterStore, Directory) body,
  ) async {
    late Directory directory;
    late CenterStore store;
    await tester.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-history-review-',
      );
      store = await CenterStore.open(directory: directory.path);
      await seedReceptionUi(
        store,
        directory,
        studentCount: 55,
        sessionCount: 3,
      );
    });
    await tester.binding.setSurfaceSize(const Size(1600, 1200));
    try {
      await body(store, directory);
    } finally {
      await tester.pumpWidget(const SizedBox.shrink());
      var closed = false;
      final closing = store.close().whenComplete(() => closed = true);
      for (var attempt = 0; !closed && attempt < 200; attempt++) {
        await tester.runAsync(
          () => Future<void>.delayed(const Duration(milliseconds: 10)),
        );
        await tester.pump();
      }
      expect(closed, isTrue, reason: 'Temporary SQLite store must close.');
      await closing;
      await tester.runAsync(() => directory.delete(recursive: true));
      await tester.binding.setSurfaceSize(null);
    }
  }

  Future<void> mount(WidgetTester tester, Widget child) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: Scaffold(body: child),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  }

  testWidgets(
    'reviewed last-page rows survive search and refresh after removal',
    (tester) async {
      await withStore(tester, (store, _) async {
        await mount(tester, ReviewPage(store: store, sessionId: 'session-0'));
        final reviewed = find.byKey(const Key('reviewed-students-table'));
        Finder inside(Finder child) =>
            find.descendant(of: reviewed, matching: child);
        expect(inside(find.text('1–50 من 55')), findsOneWidget);
        expect(
          inside(find.byKey(const ValueKey('undo-reviewed-student-54'))),
          findsOneWidget,
        );
        expect(
          inside(find.byKey(const ValueKey('undo-reviewed-student-0'))),
          findsNothing,
        );
        await tester.tap(inside(find.byTooltip('الصفحة التالية')));
        await tester.pumpAndSettle();
        expect(inside(find.text('51–55 من 55')), findsOneWidget);
        expect(
          inside(find.byKey(const ValueKey('undo-reviewed-student-0'))),
          findsOneWidget,
        );
        await tester.enterText(
          find.byKey(const Key('payment-check-code')),
          'no matching student',
        );
        await tester.pump(const Duration(milliseconds: 150));
        expect(
          inside(find.byKey(const ValueKey('undo-reviewed-student-0'))),
          findsOneWidget,
        );
        final attendanceCount = store.allAttendances.length;
        final paymentCount = store.allPayments.length;
        final removeLast = inside(
          find.byKey(const ValueKey('undo-reviewed-student-0')),
        );
        await tester.ensureVisible(removeLast);
        await tester.tap(removeLast);
        await tester.pump(const Duration(milliseconds: 300));
        expect(find.text('إزالة علامة مراجعة الطالب؟'), findsOneWidget);
        await tester.runAsync(
          () => tester.tap(find.text('إزالة علامات المراجعة')),
        );
        var completed = false;
        for (var attempt = 0; attempt < 100 && !completed; attempt++) {
          await tester.runAsync(
            () => Future<void>.delayed(const Duration(milliseconds: 10)),
          );
          await tester.pump();
          final nextRemove = inside(
            find.byKey(const ValueKey('undo-reviewed-student-1')),
          );
          completed =
              store.paymentChecks.length == 54 &&
              nextRemove.evaluate().isNotEmpty &&
              tester.widget<TextButton>(nextRemove).onPressed != null;
        }
        expect(
          completed,
          isTrue,
          reason:
              'Review removal must finish durable persistence and re-enable the actions.',
        );
        await tester.pumpAndSettle();
        expect(find.text('اللي راجعتهم · 54'), findsOneWidget);
        expect(inside(find.text('51–54 من 54')), findsOneWidget);
        expect(
          inside(find.byKey(const ValueKey('undo-reviewed-student-0'))),
          findsNothing,
        );
        expect(
          inside(find.byKey(const ValueKey('undo-reviewed-student-1'))),
          findsOneWidget,
        );
        expect(store.allAttendances.length, attendanceCount);
        expect(store.allPayments.length, paymentCount);
        await tester.runAsync(
          () => store.checkPayment(
            studentId: 'student-0',
            sessionId: 'session-0',
            expectedAmount: 10000,
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(inside(find.byTooltip('الصفحة السابقة')));
        await tester.pumpAndSettle();
        final rows = tester
            .widget<DataTable>(inside(find.byType(DataTable)))
            .rows;
        expect(rows.first.key, const ValueKey('reviewed-student-student-0'));
        expect(find.text('اللي راجعتهم · 55'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'history preserves package makeup and canceled payments across refresh and roles',
    (tester) async {
      await withStore(tester, (store, directory) async {
        await tester.runAsync(
          () => _makeHistoryRelationships(store, directory),
        );
        final student = store.studentById('student-0')!;
        await mount(
          tester,
          SingleChildScrollView(
            child: StudentHistoryPanel(
              store: store,
              student: student,
              expanded: true,
            ),
          ),
        );
        expect(find.text('تم التعويض'), findsOneWidget);
        expect(find.textContaining('عن شهر 1 · حصة 1'), findsOneWidget);
        expect(find.text('محسوب من الشهر · دفع جزئي'), findsNWidgets(2));
        expect(find.text('دفع الحصة · دفع جزئي'), findsOneWidget);
        expect(find.text('حصة اختبار 3'), findsOneWidget);
        await tester.runAsync(() async {
          await store.settleDebt(
            paymentId: 'payment-0-2',
            kind: DebtKind.lesson,
            amount: 1000,
          );
          await store.cancelAttendance(
            attendanceId: 'attendance-0-1',
            reason: 'إلغاء تعويض اختبار',
          );
          await store.cancelPayment(
            paymentId: 'payment-0-2',
            reason: 'رد تحصيل اختبار',
          );
        });
        await tester.pumpAndSettle();
        expect(find.text('تم التعويض'), findsNothing);
        expect(find.text('غياب محسوب من الشهر'), findsOneWidget);
        expect(find.text('محسوب من الشهر · دفع جزئي'), findsOneWidget);
        expect(find.text('غير مدفوع — لا يوجد دفع ساري'), findsOneWidget);
        expect(find.text('ملغاة'), findsNWidgets(2));
        expect(find.text('الاستردادات'), findsOneWidget);
        expect(find.text('سجل التصحيحات'), findsOneWidget);
        expect(find.text('رد تحصيل اختبار'), findsNWidgets(2));
        expect(store.refunds.single.amount, 3500);
        expect(find.text('تسديدات المديونية'), findsOneWidget);
        expect(find.text('حصة اختبار 3'), findsNWidgets(2));
        await tester.runAsync(
          () => store.signIn('ui-assistant', 'test-password-2026'),
        );
        await tester.pumpAndSettle();
        expect(find.text('المدفوعات الأصلية'), findsNothing);
        expect(find.text('الاستردادات'), findsNothing);
        expect(find.text('دفع جزئي'), findsNothing);
        expect(find.text('غياب محسوب من الشهر'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    },
  );
}

Future<void> _makeHistoryRelationships(
  CenterStore store,
  Directory directory,
) async {
  await store.saveStaff(
    name: 'ui-assistant',
    password: 'test-password-2026',
    role: StaffRole.assistant,
  );
  final backup =
      jsonDecode(await File(await store.createBackup()).readAsString())
          as Map<String, dynamic>;
  final snapshot = backup['data'] as Map<String, dynamic>;
  final at = DateTime(2026, 9, 1, 10);
  snapshot['paymentChecks'] = <dynamic>[];
  final attendance = snapshot['attendances'] as List<dynamic>;
  for (final record in attendance.cast<Map<String, dynamic>>()) {
    if (record['id'] == 'attendance-0-0') {
      record['status'] = AttendanceStatus.absent.name;
      record['packageId'] = 'package-0';
    }
    if (record['id'] == 'attendance-0-1') {
      record['status'] = AttendanceStatus.makeup.name;
      record['originalAttendanceId'] = 'attendance-0-0';
    }
  }
  final payments = snapshot['payments'] as List<dynamic>;
  payments.removeWhere((payment) => payment['id'] == 'payment-0-1');
  for (final payment in payments.cast<Map<String, dynamic>>()) {
    if (payment['id'] == 'payment-0-0') {
      payment['packageId'] = 'package-0';
      payment['description'] = 'شراء شهر اختبار';
      payment['baseAmount'] = 40000;
      payment['netAmount'] = 40000;
      payment['paidAmount'] = 10000;
    }
    if (payment['id'] == 'payment-0-2') payment['paidAmount'] = 2500;
  }
  snapshot['packages'] = [
    PrepaidPackage(
      id: 'package-0',
      studentId: 'student-0',
      groupId: 'group',
      purchasedAt: at,
      remaining: 3,
      paymentId: 'payment-0-0',
    ).toJson(),
  ];
  final source = File('${directory.path}/history-relations.json');
  await source.writeAsString(jsonEncode(backup));
  await store.restoreBackup(source.path);
  await store.signIn('ui-admin', 'test-password-2026');
}
