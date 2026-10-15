import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/attendance_ui_helpers.dart';

void main() {
  late Directory fixture;
  late CenterStore store;
  late Student student;
  late LessonSession current;
  late PaymentRecord receipt;

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await initializeDateFormatting('ar_EG');
      fixture = await Directory.systemTemp.createTemp(
        'massar-attendance-online-',
      );
      store = await CenterStore.open(directory: fixture.path);
      await store.setupAdmin('manager', 'test-manager-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(kind: kind, name: kind.name));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة التجربة',
          subjectId: store.catalogs
              .firstWhere((e) => e.kind == CatalogKind.subject)
              .id,
          centerId: store.catalogs
              .firstWhere((e) => e.kind == CatalogKind.center)
              .id,
          gradeId: store.catalogs
              .firstWhere((e) => e.kind == CatalogKind.grade)
              .id,
          sessionPrice: 6000,
          packagePrice: 21000,
        ),
      );
      await seedAttendanceMonths(store, fourPrice: 21000);
      await store.saveStudent(
        Student(
          name: 'طالبة تجربة',
          code: 'T150',
          groupIds: [store.groups.single.id],
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
      student = store.students.single;
      await store.saveSession(
        LessonSession(
          groupId: store.groups.single.id,
          number: 1,
          startsAt: DateTime.now(),
          createdAt: DateTime.now(),
        ),
      );
      final first = store.sessions.single;
      await store.startSession(first.id);
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: first.id,
          mode: EntryMode.package,
        ),
      );
      receipt = store.payments.single;
      await store.closeSession(first.id);
      await store.finalizeSession(sessionId: first.id, actualCash: 21000);
      await store.saveSession(
        LessonSession(
          groupId: store.groups.single.id,
          number: 2,
          startsAt: DateTime.now(),
          createdAt: DateTime.now(),
        ),
      );
      current = store.sessions.last;
      await store.startSession(current.id);
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await store.close();
      await fixture.delete(recursive: true);
    }),
  );

  Future<void> mount(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(1440, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final selection = AttendanceWorkspaceContext()
      ..groupId = current.groupId
      ..sessionId = current.id
      ..studentId = student.id
      ..studentResolved = true;
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: AttendanceWorkspace(
            store: store,
            initialSessionId: current.id,
            workspaceContext: selection,
            onExit: () {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets(
    'attendance button allocates an old closed monthly receipt without changing cash or attendance',
    (tester) async {
      await tester.runAsync(() async {
        final closing = store.closings.single.toJson();
        final package = store.packages.single.toJson();
        final attendance = store.attendances.single.toJson();
        await mount(tester);
        final action = find.byKey(const Key('attendance-allocate-online'));
        await tester.ensureVisible(action);
        expect(tester.widget<OutlinedButton>(action).onPressed, isNotNull);
        await tester.tap(action);
        await tester.pumpAndSettle();
        final amount = find.byKey(const Key('correction-center-amount'));
        await tester.ensureVisible(amount);
        await tester.enterText(amount, '150');
        final reason = find.byKey(const Key('correction-reason'));
        await tester.ensureVisible(reason);
        await tester.enterText(reason, 'تعويض الحصة الثانية أونلاين');
        final apply =
            tester
                    .widget<FilledButton>(
                      find.byKey(const Key('apply-correction')),
                    )
                    .onPressed!
                as Future<void> Function();
        final saving = apply();
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'تأكيد التصحيح'));
        await saving;
        await tester.pumpAndSettle();
        expect(store.centerAllocatedAmount(receipt.id), 15000);
        expect(store.paymentCollectedFor(receipt.id), 21000);
        expect(store.closings.single.toJson(), closing);
        expect(store.packages.single.toJson(), package);
        expect(store.attendances.single.toJson(), attendance);
        expect(store.payments, hasLength(1));
        expect(store.refunds, isEmpty);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets('assistants never see the attendance online allocation action', (
    tester,
  ) async {
    await tester.runAsync(() async {
      await store.saveStaff(
        name: 'assistant',
        password: 'test-assistant-password',
        role: StaffRole.assistant,
      );
      await store.signIn('assistant', 'test-assistant-password');
      await mount(tester);
      expect(find.byKey(const Key('attendance-allocate-online')), findsNothing);
      expect(store.corrections, isEmpty);
      expect(tester.takeException(), isNull);
    });
  });

  testWidgets(
    'multiple monthly receipts require an explicit choice instead of guessing the latest month',
    (tester) async {
      await tester.runAsync(() async {
        await store.renewPackage(
          PackageRequest(
            studentId: student.id,
            groupId: current.groupId,
            sessionId: current.id,
          ),
        );
        final latest = store.payments.last;
        await mount(tester);
        final action = find.byKey(const Key('attendance-allocate-online'));
        await tester.ensureVisible(action);
        await tester.tap(action);
        await tester.pumpAndSettle();
        expect(find.text('اختار دفعة الشهر للتحويل للأونلاين'), findsOneWidget);
        expect(
          find.byKey(ValueKey('online-allocation-receipt-${receipt.id}')),
          findsOneWidget,
        );
        expect(
          find.byKey(ValueKey('online-allocation-receipt-${latest.id}')),
          findsOneWidget,
        );
        expect(find.byKey(const Key('correction-center-amount')), findsNothing);
        await tester.tap(
          find.byKey(ValueKey('online-allocation-receipt-${receipt.id}')),
        );
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('correction-center-amount')),
          findsOneWidget,
        );
        expect(store.corrections, isEmpty);
        expect(tester.takeException(), isNull);
      });
    },
  );
}
