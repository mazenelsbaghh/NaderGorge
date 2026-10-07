import 'dart:io';
import 'dart:convert';
import '../helpers/notice_helpers.dart';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/review_page.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group, otherGroup;
  late Student student;
  late LessonSession oldClass, newClass, otherClass;

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-scoped-review-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('الإدارة', 'local-review-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      for (final name in ['الأحد', 'الثلاثاء']) {
        await store.saveGroup(
          StudyGroup(
            name: name,
            subjectId: store.catalogs
                .firstWhere((c) => c.kind == CatalogKind.subject)
                .id,
            centerId: store.catalogs
                .firstWhere((c) => c.kind == CatalogKind.center)
                .id,
            gradeId: store.catalogs
                .firstWhere((c) => c.kind == CatalogKind.grade)
                .id,
            sessionPrice: 10000,
            packagePrice: 40000,
          ),
        );
      }
      group = store.groups.first;
      otherGroup = store.groups.last;
      await store.saveStudent(
        Student(
          name: 'أحمد محمد',
          code: 'MS-101',
          groupIds: [group.id],
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
      student = store.students.single;
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 5,
          startsAt: DateTime.now().subtract(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      oldClass = store.sessions.single;
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: oldClass.id,
          mode: EntryMode.single,
        ),
      );
      await store.closeSession(oldClass.id);
      await store.finalizeSession(sessionId: oldClass.id, actualCash: 10000);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 6,
          startsAt: DateTime.now().subtract(const Duration(hours: 1)),
          createdAt: DateTime.now(),
        ),
      );
      newClass = store.sessions.last;
      await store.saveSession(
        LessonSession(
          groupId: otherGroup.id,
          number: 9,
          kind: SessionKind.free,
          startsAt: DateTime.now(),
          createdAt: DateTime.now(),
        ),
      );
      otherClass = store.sessions.last;
      await store.checkPayment(studentId: student.id, sessionId: newClass.id);
      await store.checkPayment(studentId: student.id, sessionId: otherClass.id);
    }),
  );
  tearDown(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> open(WidgetTester tester, {String? sessionId}) async {
    await tester.binding.setSurfaceSize(const Size(1100, 780));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.runAsync(
      () => tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.dark,
          home: Scaffold(
            body: Directionality(
              textDirection: TextDirection.rtl,
              child: ReviewPage(
                key: const Key('same-review'),
                store: store,
                sessionId: sessionId,
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> scan(WidgetTester tester) async {
    var changed = false;
    void listener() {
      changed = true;
    }

    store.addListener(listener);
    try {
      await tester.enterText(
        find.byKey(const Key('payment-check-code')),
        student.code,
      );
      await tester.runAsync(() async {
        await tester.testTextInput.receiveAction(TextInputAction.search);
        for (var attempt = 0; !changed && attempt < 100; attempt++) {
          await tester.pump(const Duration(milliseconds: 10));
          await Future<void>.delayed(const Duration(milliseconds: 10));
        }
        expect(
          changed,
          isTrue,
          reason: 'The real SQLite review must persist before assertions.',
        );
        await acknowledgeNotice(tester);
      });
      await tester.pumpAndSettle();
    } finally {
      store.removeListener(listener);
    }
  }

  testWidgets(
    'review typing remains editable and fresh marks replace cached display',
    (tester) async {
      await open(tester, sessionId: oldClass.id);
      final field = find.byKey(const Key('payment-check-code'));
      await tester.enterText(field, 'MS');
      await tester.pump(const Duration(milliseconds: 30));
      await tester.enterText(field, student.code);
      await tester.pump(const Duration(milliseconds: 30));
      expect(tester.widget<TextField>(field).controller!.text, student.code);
      await tester.pump(const Duration(milliseconds: 150));
      expect(find.textContaining('تمت مراجعة 0 من 1 حاضر'), findsOneWidget);
      await tester.runAsync(
        () => store.checkPayment(
          studentId: student.id,
          sessionId: oldClass.id,
          expectedAmount: 10000,
        ),
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('تمت مراجعة 1 من 1 حاضر'), findsOneWidget);
      final reviewedTable = find.byKey(const Key('reviewed-students-table'));
      expect(reviewedTable, findsOneWidget);
      await tester.enterText(field, 'another student');
      await tester.pump(const Duration(milliseconds: 150));
      expect(
        find.descendant(
          of: reviewedTable,
          matching: find.textContaining(student.code),
        ),
        findsOneWidget,
      );

      await tester.runAsync(
        () =>
            store.uncheckPayment(studentId: student.id, sessionId: oldClass.id),
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('تمت مراجعة 0 من 1 حاضر'), findsOneWidget);
      expect(reviewedTable, findsNothing);
      await tester.enterText(field, 'pending');
      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 150));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'scoped review stays on the exact finalized old class and never falls back to a newer class or group',
    (tester) async {
      await open(tester, sessionId: oldClass.id);
      expect(find.byType(DropdownButtonFormField<String>), findsNothing);
      expect(find.text('مقارنة مبلغ الورق'), findsNothing);
      expect(find.byKey(const Key('session-review-context')), findsOneWidget);
      expect(find.textContaining('حصة 5 ·'), findsOneWidget);
      expect(find.text('تمت مراجعة 0 كود'), findsOneWidget);
      final payments = store.allPayments.length,
          attendance = store.allAttendances.length;
      await scan(tester);
      expect(find.byKey(const Key('payment-check-result')), findsOneWidget);
      expect(
        tester.widget<Text>(find.byKey(const Key('payment-check-result'))).data,
        'دافع الحصة',
      );
      expect(
        store.paymentChecks
            .where((c) => c.sessionId == oldClass.id)
            .single
            .status,
        StudentPaymentStatus.paidSingle,
      );
      expect(
        store.paymentChecks
            .where((c) => c.sessionId == newClass.id)
            .single
            .status,
        StudentPaymentStatus.notPaid,
      );
      expect(
        store.paymentChecks
            .where((c) => c.sessionId == otherClass.id)
            .single
            .status,
        StudentPaymentStatus.free,
      );
      expect(find.text('تمت مراجعة 1 كود'), findsOneWidget);
      expect(store.allPayments, hasLength(payments));
      expect(store.allAttendances, hasLength(attendance));
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('payment-check-code')))
            .focusNode!
            .hasFocus,
        isTrue,
      );
      await scan(tester);
      expect(
        store.paymentChecks.where((c) => c.sessionId == oldClass.id),
        hasLength(1),
      );
      expect(tester.takeException(), isNull);
    },
  );

  for (final invalid in ['missing', 'canceled']) {
    testWidgets(
      '$invalid fixed class offers no scan and creates no review fallback',
      (tester) async {
        if (invalid == 'canceled') {
          await tester.runAsync(() async {
            await store.saveSession(
              LessonSession(
                groupId: otherGroup.id,
                number: 10,
                kind: SessionKind.free,
                startsAt: DateTime.now(),
                createdAt: DateTime.now(),
              ),
            );
            otherClass = store.sessions.last;
            await store.cancelSession(otherClass.id);
          });
        }
        final count = store.paymentChecks.length;
        await open(
          tester,
          sessionId: invalid == 'missing' ? 'not-found' : otherClass.id,
        );
        expect(
          find.byKey(const Key('session-review-unavailable')),
          findsOneWidget,
        );
        expect(find.byKey(const Key('payment-check-code')), findsNothing);
        expect(find.byType(DropdownButtonFormField<String>), findsNothing);
        expect(store.paymentChecks, hasLength(count));
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'cashier can review the locked class but assistant receives no scanner',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'الاستقبال',
          password: 'cashier-review-password',
          role: StaffRole.cashier,
        );
        await store.saveStaff(
          name: 'المساعد',
          password: 'assistant-review-password',
          role: StaffRole.assistant,
        );
        await store.signIn('الاستقبال', 'cashier-review-password');
      });
      await open(tester, sessionId: oldClass.id);
      await scan(tester);
      final count = store.paymentChecks.length;
      await tester.runAsync(
        () => store.signIn('المساعد', 'assistant-review-password'),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('payment-check-code')), findsNothing);
      expect(
        find.text('مراجعة الدفع متاحة للإدارة والاستقبال فقط.'),
        findsOneWidget,
      );
      expect(store.paymentChecks, hasLength(count));
    },
  );

  for (final ambiguous in [false, true]) {
    testWidgets(
      'legacy whitespace code lookup is safe when ambiguous=$ambiguous',
      (tester) async {
        final originalId = student.id;
        await tester.runAsync(() async {
          if (ambiguous) {
            await store.saveStudent(
              student.copyWith(
                id: '',
                name: 'طالب بكود متشابه',
                code: 'OTHER-CODE',
              ),
            );
          }
          final path = await store.createBackup(
            destination: '${directory.path}/legacy.json',
          );
          final backup =
              jsonDecode(await File(path).readAsString())
                  as Map<String, dynamic>;
          final data = backup['data'] as Map<String, dynamic>;
          final students = data['students'] as List;
          (students.first as Map)['code'] = ' MS-101 ';
          if (ambiguous) (students.last as Map)['code'] = 'MS-101';
          await File(path).writeAsString(jsonEncode(backup));
          await store.restoreBackup(path);
          await store.signIn('الإدارة', 'local-review-password');
          student = store.students.firstWhere(
            (student) => student.id == originalId,
          );
        });
        await open(tester, sessionId: oldClass.id);
        final count = store.paymentChecks.length,
            auditCount = store.audit.length;
        if (ambiguous) {
          await tester.enterText(
            find.byKey(const Key('payment-check-code')),
            'MS-101',
          );
          await tester.runAsync(() async {
            await tester.testTextInput.receiveAction(TextInputAction.search);
            await acknowledgeNotice(tester);
          });
          await tester.pumpAndSettle();
          expect(find.textContaining('الكود يطابق أكثر من طالب'), findsNothing);
          expect(store.paymentChecks, hasLength(count));
          expect(store.audit, hasLength(auditCount));
          expect(find.byKey(const Key('payment-check-result')), findsNothing);
        } else {
          await scan(tester);
          expect(
            store.paymentChecks
                .where((check) => check.sessionId == oldClass.id)
                .single
                .studentId,
            originalId,
          );
          expect(
            tester
                .widget<Text>(find.byKey(const Key('payment-check-result')))
                .data,
            'دافع الحصة',
          );
        }
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'reusing an advanced standalone page for a fixed class resets paper mode and arbitrary filters',
    (tester) async {
      await open(tester);
      await tester.tap(find.text('مقارنة مبلغ الورق'));
      await tester.pumpAndSettle();
      expect(find.text('مقارنة مبلغ الورق'), findsOneWidget);
      await tester.runAsync(
        () => tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.dark,
            home: Scaffold(
              body: Directionality(
                textDirection: TextDirection.rtl,
                child: ReviewPage(
                  key: const Key('same-review'),
                  store: store,
                  sessionId: oldClass.id,
                ),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('review-paper-amount')), findsNothing);
      expect(find.byKey(const Key('session-review-context')), findsOneWidget);
      await scan(tester);
      expect(
        store.paymentChecks
            .where((c) => c.sessionId == oldClass.id)
            .single
            .status,
        StudentPaymentStatus.paidSingle,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
