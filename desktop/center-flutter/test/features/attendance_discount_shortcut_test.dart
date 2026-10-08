import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';
import '../helpers/attendance_ui_helpers.dart';
import '../helpers/ui_wait_helpers.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Map<int, String> months;
  late Student student;
  late LessonSession session;

  setUp(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-attendance-discount-shortcut-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('discount-manager', 'discount-manager-pass');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة الاختبار',
          subjectId: store.catalogs[0].id,
          centerId: store.catalogs[1].id,
          gradeId: store.catalogs[2].id,
          sessionPrice: 12345,
          twoSessionPrice: 21000,
          threeSessionPrice: 30000,
          packagePrice: 41000,
          monthPlans: const [
            GroupMonthPlan(
              id: 'four',
              name: 'الشهر الكامل',
              sessions: 4,
              price: 41000,
            ),
            GroupMonthPlan(
              id: 'two',
              name: 'شهر حصتين',
              sessions: 2,
              price: 21000,
            ),
            GroupMonthPlan(
              id: 'three',
              name: 'شهر ثلاث حصص',
              sessions: 3,
              price: 30000,
            ),
          ],
        ),
      );
      months = await seedAttendanceMonths(
        store,
        fourPrice: 41000,
        twoPrice: 21000,
        threePrice: 30000,
      );
      group = store.groups.single;
      student = await store.registerStudent(
        Student(
          name: 'طالب الخصم',
          phone: '01012345678',
          guardianPhone: '01112345678',
          groupIds: [group.id],
          discountPercent: 10,
          notes: 'ملاحظة محفوظة',
          createdAt: DateTime.now().subtract(const Duration(days: 1)),
        ),
      );
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 1,
          startsAt: DateTime.now().add(const Duration(hours: 1)),
          createdAt: DateTime.now(),
        ),
      );
      session = store.sessions.single;
      await store.startSession(session.id);
      await (FontLoader('Tajawal')
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
          .load();
      await (FontLoader(
        'MaterialIcons',
      )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  final search = find.byKey(const Key('student-search'));
  final discountDialog = find.byKey(const Key('student-discount-dialog'));
  final paymentDialog = find.byKey(const Key('entry-confirmation-dialog'));

  TextField codeField(WidgetTester tester) => tester.widget<TextField>(search);

  void expectNoFinance() {
    expect(store.payments, isEmpty);
    expect(store.packages, isEmpty);
    expect(store.attendances, isEmpty);
    expect(store.cardPayments, isEmpty);
    expect(store.cardReceipts, isEmpty);
  }

  void expectCodeFocus(WidgetTester tester) {
    expect(codeField(tester).focusNode!.hasFocus, isTrue);
    expect(codeField(tester).controller!.text, isEmpty);
  }

  Future<void> open(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(1440, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        builder: (context, child) =>
            Directionality(textDirection: TextDirection.rtl, child: child!),
        home: AttendanceWorkspace(
          store: store,
          initialSessionId: session.id,
          onExit: () {},
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> scan(WidgetTester tester, String code) =>
      previewAttendanceStudent(tester, code);

  Future<void> openDiscount(WidgetTester tester) async {
    await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
    await tester.pumpAndSettle();
    expect(discountDialog, findsOneWidget);
    expect(paymentDialog, findsNothing);
    expectNoFinance();
  }

  Future<void> saveDraftDiscount(WidgetTester tester) async {
    await tester.runAsync(() async {
      final saved = Completer<void>();
      void observe() {
        if (store.students.single.discountPercent != student.discountPercent &&
            !saved.isCompleted) {
          saved.complete();
        }
      }

      store.addListener(observe);
      try {
        await tester.tap(find.byKey(const Key('save-student-discount')));
        await saved.future.timeout(const Duration(seconds: 5));
        expectNoFinance();
        await waitForUiCondition(
          tester,
          () => discountDialog.evaluate().isEmpty,
          reason: 'Saved discount editor closes',
        );
      } finally {
        store.removeListener(observe);
      }
    });
    await tester.pumpAndSettle();
    expect(discountDialog, findsNothing);
    expectCodeFocus(tester);
  }

  Future<void> saveDiscount(WidgetTester tester, String percent) async {
    await tester.enterText(find.byKey(const Key('discount-percent')), percent);
    await saveDraftDiscount(tester);
  }

  testWidgets(
    'S needs a resolved student and never replaces an unfinished search',
    (tester) async {
      await open(tester);
      final audits = store.audit.length;
      await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
      await tester.pumpAndSettle();
      expect(discountDialog, findsNothing);
      await scan(tester, student.code);
      await tester.enterText(search, 'بحث لم يكتمل');
      await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
      await tester.pumpAndSettle();
      expect(discountDialog, findsNothing);
      expect(codeField(tester).controller!.text, 'بحث لم يكتمل');
      expect(store.students.single.toJson(), student.toJson());
      expect(store.audit, hasLength(audits));
      expectNoFinance();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'three-session context opens its price reference and a two-session amount target preserves full precision without changing purchase quantity',
    (tester) async {
      await open(tester);
      await scan(tester, student.code);
      final audits = store.audit.length;
      await selectAttendanceMonth(tester, 'شهر ثلاث حصص · 3 حصص');
      await openDiscount(tester);
      final reference = find.descendant(
        of: discountDialog,
        matching: find.byType(DropdownButtonFormField<String>),
      );
      expect(
        tester.state<FormFieldState<String>>(reference).value,
        'month-${months[3]}',
      );
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('discount-amount')))
            .controller!
            .text,
        '270.00',
      );
      await tester.tap(reference);
      await tester.pumpAndSettle();
      await tester.tap(find.text('شهر حصتين · 2 حصص').last);
      await tester.pumpAndSettle();
      expect(
        tester.state<FormFieldState<String>>(reference).value,
        'month-${months[2]}',
      );
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('discount-amount')))
            .controller!
            .text,
        '189.00',
      );
      await tester.enterText(
        find.byKey(const Key('discount-amount')),
        '120.01',
      );
      await saveDraftDiscount(tester);
      final exact = (21000 - 12001) * 100 / 21000;
      expect(store.students.single.discountPercent, exact);
      expectNoFinance();
      expect(store.audit, hasLength(audits + 1));
      expect(
        store
            .entryConfirmationFor(
              EntryRequest(
                studentId: student.id,
                sessionId: session.id,
                mode: EntryMode.package,
                packageSessions: 2,
                monthPlanId: months[2],
              ),
            )
            .netAmount,
        12001,
      );
      await tester.runAsync(
        () => requestAttendanceConfirmation(tester, LogicalKeyboardKey.keyN),
      );
      await tester.pumpAndSettle();
      expect(paymentDialog, findsOneWidget);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(17144),
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expectNoFinance();
      expect(store.audit, hasLength(audits + 1));
      expect(tester.takeException(), isNull);
    },
  );

  for (final kind in [SessionKind.extra, SessionKind.free]) {
    testWidgets(
      '$kind discount reference follows class context while payment confirmation uses actual charge policy',
      (tester) async {
        await tester.runAsync(() async {
          await store.saveSession(
            session.copyWith(kind: kind, extraPrice: 8765),
          );
          session = store.sessions.single;
        });
        await open(tester);
        await scan(tester, student.code);
        final audits = store.audit.length;
        await openDiscount(tester);
        final reference = find.descendant(
          of: discountDialog,
          matching: find.byType(DropdownButtonFormField<String>),
        );
        expect(
          tester.state<FormFieldState<String>>(reference).value,
          kind == SessionKind.extra ? 'extra' : 'single',
        );
        expect(
          tester
              .widget<TextField>(find.byKey(const Key('discount-amount')))
              .controller!
              .text,
          kind == SessionKind.extra ? '78.89' : '111.11',
        );
        await saveDiscount(tester, '25.5');
        expect(store.students.single.discountPercent, 25.5);
        expectNoFinance();
        await tester.runAsync(
          () => requestAttendanceConfirmation(tester, LogicalKeyboardKey.keyL),
        );
        await tester.pumpAndSettle();
        expect(paymentDialog, findsOneWidget);
        expect(
          tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
          money(kind == SessionKind.extra ? 6530 : 0),
        );
        await tester.sendKeyEvent(LogicalKeyboardKey.escape);
        await tester.pumpAndSettle();
        expectCodeFocus(tester);
        expectNoFinance();
        expect(store.audit, hasLength(audits + 1));
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'held S and pending Enter create one editor without registering or charging the student',
    (tester) async {
      await open(tester);
      await scan(tester, student.code);
      final audits = store.audit.length;
      await tester.sendKeyDownEvent(LogicalKeyboardKey.keyS);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyS);
      await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.keyS);
      await tester.pumpAndSettle();
      expect(discountDialog, findsOneWidget);
      expectNoFinance();
      expect(store.students.single.toJson(), student.toJson());
      expect(store.audit, hasLength(audits));
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();
      expect(discountDialog, findsNothing);
      expectCodeFocus(tester);
      expectNoFinance();
      expect(store.audit, hasLength(audits));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'fractional discount save refreshes the L quote and only explicit payment confirmation charges exact piastres',
    (tester) async {
      await open(tester);
      await scan(tester, student.code);
      final audits = store.audit.length;
      await openDiscount(tester);
      await saveDiscount(tester, '25.5');
      final updated = store.students.single;
      expect(updated.discountPercent, 25.5);
      expect(
        updated.toJson(),
        student.copyWith(discountPercent: 25.5).toJson(),
      );
      expect(store.audit, hasLength(audits + 1));
      expectNoFinance();
      await tester.runAsync(
        () => requestAttendanceConfirmation(tester, LogicalKeyboardKey.keyL),
      );
      await tester.pumpAndSettle();
      expect(paymentDialog, findsOneWidget);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(9197),
      );
      await tester.runAsync(() async {
        final paid = Completer<void>();
        void observe() {
          if (store.payments.isNotEmpty && !paid.isCompleted) paid.complete();
        }

        store.addListener(observe);
        try {
          await tester.sendKeyEvent(LogicalKeyboardKey.enter);
          await paid.future.timeout(const Duration(seconds: 5));
          expect(store.payments.single.netAmount, 9197);
          expect(store.payments.single.discountPercent, 25.5);
          expect(store.attendances.single.fixedDiscountPercent, 25.5);
          await waitForUiCondition(
            tester,
            () => paymentDialog.evaluate().isEmpty,
            reason: 'Payment confirmation closes after durable write',
          );
        } finally {
          store.removeListener(observe);
        }
      });
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(store.payments.single.studentId, student.id);
      expect(store.payments.single.baseAmount, 12345);
      expect(store.packages, isEmpty);
      expect(store.audit, hasLength(audits + 3));
      await tester.pumpWidget(const SizedBox.shrink());
      await tester.runAsync(() async {
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.signIn('discount-manager', 'discount-manager-pass');
        expect(store.students.single.discountPercent, 25.5);
        expect(store.payments.single.netAmount, 9197);
        expect(store.payments.single.discountPercent, 25.5);
        expect(store.attendances.single.fixedDiscountPercent, 25.5);
      });
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'fresh Enter in amount editor saves a fractional fixed discount without attendance or collection',
    (tester) async {
      await open(tester);
      await scan(tester, student.code);
      final audits = store.audit.length;
      await openDiscount(tester);
      await tester.runAsync(() async {
        final saved = Completer<void>();
        void observe() {
          if (store.students.single.discountPercent != 10 &&
              !saved.isCompleted) {
            saved.complete();
          }
        }

        store.addListener(observe);
        try {
          await tester.enterText(
            find.byKey(const Key('discount-amount')),
            '30.00',
          );
          await tester.sendKeyEvent(LogicalKeyboardKey.enter);
          await saved.future.timeout(const Duration(seconds: 5));
          expectNoFinance();
          expect(
            store
                .entryConfirmationFor(
                  EntryRequest(
                    studentId: student.id,
                    sessionId: session.id,
                    mode: EntryMode.single,
                  ),
                )
                .netAmount,
            3000,
          );
          await waitForUiCondition(
            tester,
            () => discountDialog.evaluate().isEmpty,
            reason: 'Saved discount editor closes',
          );
        } finally {
          store.removeListener(observe);
        }
      });
      await tester.pumpAndSettle();
      expect(discountDialog, findsNothing);
      expectCodeFocus(tester);
      expectNoFinance();
      expect(store.students.single.code, student.code);
      expect(store.students.single.notes, student.notes);
      expect(store.audit, hasLength(audits + 1));
      expect(tester.takeException(), isNull);
    },
  );

  for (final code in ['S', 's12']) {
    testWidgets(
      'known legacy code $code has priority over S and scanner Enter cannot edit or charge the previously selected student',
      (tester) async {
        await tester.runAsync(
          () => store.saveStudent(
            Student(
              name: 'طالب الكود القديم',
              code: code,
              groupIds: [group.id],
              createdAt: DateTime.now().subtract(const Duration(days: 1)),
            ),
          ),
        );
        final legacy = store.students.last;
        await open(tester);
        await scan(tester, student.code);
        final audits = store.audit.length;
        await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
        await tester.pumpAndSettle();
        expect(discountDialog, findsNothing);
        await scan(tester, code);
        expect(find.text('كود الطالب: ${legacy.code}'), findsOneWidget);
        expectNoFinance();
        expect(store.audit, hasLength(audits));
        expect(store.students.first.toJson(), student.toJson());
        await tester.ensureVisible(
          find.byKey(const Key('attendance-discount')),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const Key('attendance-discount')));
        await tester.pumpAndSettle();
        expect(discountDialog, findsOneWidget);
        expect(
          find.descendant(of: discountDialog, matching: find.text(legacy.name)),
          findsOneWidget,
        );
        await tester.sendKeyEvent(LogicalKeyboardKey.escape);
        await tester.pumpAndSettle();
        expectCodeFocus(tester);
        expectNoFinance();
        expect(store.audit, hasLength(audits));
        expect(tester.takeException(), isNull);
      },
    );
  }

  for (final role in [StaffRole.cashier, StaffRole.assistant]) {
    testWidgets('$role can review a discount while cancel preserves finance', (
      tester,
    ) async {
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'restricted-staff',
          password: 'restricted-staff-pass',
          role: role,
        );
        await store.signIn('restricted-staff', 'restricted-staff-pass');
      });
      await open(tester);
      await scan(tester, student.code);
      final audits = store.audit.length;
      await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
      await tester.pumpAndSettle();
      expect(discountDialog, findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();
      final button = find.byKey(const Key('attendance-discount'));
      await tester.ensureVisible(button);
      expect(tester.widget<ButtonStyleButton>(button).onPressed, isNotNull);
      await tester.tap(button);
      await tester.pumpAndSettle();
      expect(discountDialog, findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();
      expectNoFinance();
      expect(store.students.single.toJson(), student.toJson());
      expect(store.audit, hasLength(audits));
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets(
    'note editing and another confirmation keep S from opening a discount route or changing financial state',
    (tester) async {
      await open(tester);
      await scan(tester, student.code);
      final audits = store.audit.length;
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
      await tester.pumpAndSettle();
      expect(discountDialog, findsNothing);
      expect(find.byKey(const Key('student-note-editor')), findsOneWidget);
      expect(store.students.single.toJson(), student.toJson());
      expectNoFinance();
      await tester.tap(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      await requestAttendanceConfirmation(tester, LogicalKeyboardKey.keyL);
      await tester.pumpAndSettle();
      expect(paymentDialog, findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
      await tester.pumpAndSettle();
      expect(discountDialog, findsNothing);
      expect(paymentDialog, findsOneWidget);
      expectNoFinance();
      expect(store.students.single.toJson(), student.toJson());
      expect(store.audit, hasLength(audits));
      await tester.runAsync(() async {
        await tester.tap(find.byKey(const Key('cancel-entry-confirmation')));
        await acknowledgeNotice(
          tester,
          message:
              'تم إلغاء التأكيد لحماية حساب الطالب. امسح كود الطالب من جديد.',
        );
      });
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expectNoFinance();
      expect(tester.takeException(), isNull);
    },
  );
}
