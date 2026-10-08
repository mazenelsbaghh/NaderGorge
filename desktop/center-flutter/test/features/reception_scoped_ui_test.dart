import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/features/management/reports_page.dart';
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
      directory = await Directory.systemTemp.createTemp('massar-scoped-ui-');
      store = await CenterStore.open(directory: directory.path);
      await seedReceptionUi(store, directory, studentCount: 3, sessionCount: 3);
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
      expect(closed, isTrue);
      await closing;
      await tester.runAsync(() => directory.delete(recursive: true));
      await tester.binding.setSurfaceSize(null);
    }
  }

  Future<void> mount(WidgetTester tester, Widget page) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Directionality(textDirection: TextDirection.rtl, child: page),
      ),
    );
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  }

  testWidgets(
    'report suggestions preserve the displayed filter and clear selection immediately',
    (tester) async {
      await withStore(tester, (store, _) async {
        await mount(
          tester,
          Scaffold(
            body: AnimatedBuilder(
              animation: store,
              builder: (_, _) => ReportsPage(store: store),
            ),
          ),
        );
        final search = find.byKey(const Key('report-student-search'));
        final instruction = find.text(
          'اختر الطالب من النتائج، أو اضغط Enter بعد كتابة الكود لتطبيق الفلتر.',
        );
        expect(instruction, findsNothing);
        expect(find.text('عدد الطلبة: 3'), findsOneWidget);
        expect(find.byTooltip('كل الطلبة'), findsNothing);
        await tester.enterText(search, 'no matches');
        await tester.pumpAndSettle();
        expect(instruction, findsOneWidget);
        expect(find.text('عدد الطلبة: 3'), findsOneWidget);
        expect(find.byTooltip('كل الطلبة'), findsOneWidget);
        await tester.tap(find.byTooltip('كل الطلبة'));
        await tester.pumpAndSettle();
        expect(tester.widget<TextField>(search).controller!.text, isEmpty);
        expect(instruction, findsNothing);
        expect(find.byTooltip('كل الطلبة'), findsNothing);

        await tester.enterText(search, '10002');
        await tester.testTextInput.receiveAction(TextInputAction.done);
        await tester.pumpAndSettle();
        expect(find.text('عدد الطلبة: 1'), findsOneWidget);
        expect(find.text('طالب اختبار 0002'), findsOneWidget);
        expect(instruction, findsNothing);
        expect(find.text('طالب اختبار 0000'), findsNothing);
        await tester.enterText(search, 'طالب اختبار 0001');
        await tester.pumpAndSettle();
        expect(find.text('عدد الطلبة: 3'), findsOneWidget);
        await tester.tap(find.widgetWithText(ListTile, 'طالب اختبار 0001'));
        await tester.pumpAndSettle();
        expect(find.text('عدد الطلبة: 1'), findsOneWidget);
        expect(find.text('طالب اختبار 0001'), findsOneWidget);
        await tester.runAsync(
          () => store.saveStudent(
            store
                .studentById('student-1')!
                .copyWith(name: 'اسم محدث من الجهاز الرئيسي'),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('اسم محدث من الجهاز الرئيسي'), findsOneWidget);
        expect(find.text('طالب اختبار 0001'), findsNothing);
        await tester.enterText(search, 'name not found');
        await tester.pumpAndSettle();
        expect(find.text('عدد الطلبة: 3'), findsOneWidget);
        expect(find.text('اسم محدث من الجهاز الرئيسي'), findsOneWidget);
        store.signOut();
        await tester.pumpAndSettle();
        expect(find.text('عدد الطلبة: 3'), findsNothing);
        expect(find.text('سجل الدخول لعرض التقارير.'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'attendance display refreshes canceled makeup and switches student without stale coverage',
    (tester) async {
      await withStore(tester, (store, directory) async {
        await tester.runAsync(() => _seedMakeup(store, directory));
        final saved = AttendanceWorkspaceContext()
          ..groupId = 'group'
          ..sessionId = 'session-1'
          ..studentId = 'student-0'
          ..studentResolved = true
          ..lookupOnly = true;
        await mount(
          tester,
          AttendanceWorkspace(
            store: store,
            initialSessionId: 'session-1',
            workspaceContext: saved,
            onExit: () {},
          ),
        );
        final current = find.byKey(const Key('attendance-current-status'));
        expect(
          find.descendant(of: current, matching: find.text('معوّض')),
          findsOneWidget,
        );
        expect(
          find.byKey(const Key('attendance-makeup-status')),
          findsOneWidget,
        );
        expect(find.textContaining('من: مجموعة الاختبار'), findsOneWidget);
        await tester.runAsync(
          () => store.cancelAttendance(
            attendanceId: 'attendance-0-1',
            reason: 'إلغاء تعويض اختبار تحديث العرض',
          ),
        );
        await tester.pumpAndSettle();
        expect(
          find.descendant(of: current, matching: find.text('لم يُسجل الحضور')),
          findsOneWidget,
        );
        expect(find.byKey(const Key('attendance-makeup-status')), findsNothing);
        final beforeAttendance = store.allAttendances.length;
        final beforePayments = store.allPayments.length;
        await tester.enterText(
          find.byKey(const Key('student-search')),
          '١٠٠٠١',
        );
        // Like a scanner: Enter arrives before the 120ms suggestion timer.
        await tester.testTextInput.receiveAction(TextInputAction.done);
        await tester.pumpAndSettle();
        expect(find.text('كود الطالب: 10001'), findsOneWidget);
        expect(
          find.descendant(of: current, matching: find.text('حاضر')),
          findsOneWidget,
        );
        expect(find.byKey(const Key('attendance-makeup-status')), findsNothing);
        expect(store.allAttendances.length, beforeAttendance);
        expect(store.allPayments.length, beforePayments);
        await tester.runAsync(
          () => store.cancelAttendance(
            attendanceId: 'attendance-1-1',
            reason: 'إلغاء حضور اختبار تحديث العرض',
          ),
        );
        await tester.pumpAndSettle();
        expect(
          find.descendant(of: current, matching: find.text('لم يُسجل الحضور')),
          findsOneWidget,
        );
        expect(tester.takeException(), isNull);
      });
    },
  );
}

Future<void> _seedMakeup(CenterStore store, Directory directory) async {
  final backup =
      jsonDecode(await File(await store.createBackup()).readAsString())
          as Map<String, dynamic>;
  final snapshot = backup['data'] as Map<String, dynamic>;
  snapshot['paymentChecks'] = <dynamic>[];
  for (final attendance
      in (snapshot['attendances'] as List).cast<Map<String, dynamic>>()) {
    if (attendance['id'] == 'attendance-0-0') {
      attendance['status'] = AttendanceStatus.absent.name;
      attendance['packageId'] = 'package-0';
    }
    if (attendance['id'] == 'attendance-0-1') {
      attendance['status'] = AttendanceStatus.makeup.name;
      attendance['originalAttendanceId'] = 'attendance-0-0';
    }
  }
  final payments = snapshot['payments'] as List;
  payments.removeWhere((payment) => payment['id'] == 'payment-0-1');
  for (final payment in payments.cast<Map<String, dynamic>>()) {
    if (payment['id'] == 'payment-0-0') {
      payment['packageId'] = 'package-0';
      payment['baseAmount'] = 40000;
      payment['netAmount'] = 40000;
    }
  }
  snapshot['packages'] = [
    PrepaidPackage(
      id: 'package-0',
      studentId: 'student-0',
      groupId: 'group',
      purchasedAt: DateTime(2026, 9, 1, 10),
      remaining: 3,
      paymentId: 'payment-0-0',
    ).toJson(),
  ];
  final source = File('${directory.path}/makeup.json');
  await source.writeAsString(jsonEncode(backup));
  await store.restoreBackup(source.path);
  await store.signIn('ui-admin', 'test-password-2026');
}
