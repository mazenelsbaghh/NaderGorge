import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/formatters.dart';
import '../helpers/ui_wait_helpers.dart';

void main() {
  late Directory directory;
  late CenterStore store;

  setUp(() async {
    await initializeDateFormatting('ar_EG');
    directory = await Directory.systemTemp.createTemp(
      'massar-management-test-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير الاختبار', 'test-password-2026');
  });

  tearDown(() async {
    await TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    });
  });

  Future<void> openWorkspace(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(1440, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: Scaffold(
            body: ManagementWorkspace(store: store, onOpenAttendance: () {}),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> commitAction(WidgetTester tester, Finder action) async {
    final auditCount = store.audit.length;
    await tester.ensureVisible(action);
    await tester.tap(action);
    await waitForUiCondition(
      tester,
      () =>
          store.audit.length > auditCount &&
          find.byType(Dialog).evaluate().isEmpty &&
          find.byType(LinearProgressIndicator).evaluate().isEmpty &&
          find.byType(CircularProgressIndicator).evaluate().isEmpty,
      reason: 'The UI operation must publish its durable store mutation.',
    );
  }

  Future<void> settleDialogFrames(WidgetTester tester) async {
    // Renewing remains busy while the confirmation route is open.
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
  }

  Future<void> select(WidgetTester tester, String label, String option) async {
    final field = find.widgetWithText(DropdownButtonFormField<String>, label);
    await tester.ensureVisible(field);
    await tester.tap(field);
    await tester.pumpAndSettle();
    await tester.tap(find.text(option).last);
    await tester.pumpAndSettle();
  }

  Future<StudyGroup> seedGroup() async {
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(
        CatalogEntry(name: 'اختبار ${kind.name}', kind: kind),
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
    return store.groups.single;
  }

  testWidgets(
    'first run catalog and group forms persist linked prices without demo records',
    (tester) async {
      await tester.runAsync(() async {
        await openWorkspace(tester);
        expect(store.students, isEmpty);
        expect(store.catalogs, isEmpty);
        for (final entry in [
          ('المواد', 'فيزياء'),
          ('السناتر', 'النور'),
          ('الصفوف الدراسية', 'الثالث الثانوي'),
        ]) {
          await tester.tap(find.widgetWithText(ChoiceChip, entry.$1));
          await tester.pumpAndSettle();
          await tester.tap(find.widgetWithText(FilledButton, 'إضافة'));
          await tester.pumpAndSettle();
          await tester.enterText(
            find.widgetWithText(TextFormField, 'الاسم'),
            entry.$2,
          );
          await commitAction(
            tester,
            find.widgetWithText(FilledButton, switch (entry.$1) {
              'المواد' => 'إضافة مادة',
              'السناتر' => 'إضافة سنتر',
              _ => 'إضافة صف دراسي',
            }),
          );
        }
        final months = <StudyMonth>[];
        for (final count in [2, 3, 4]) {
          months.add(
            await store.saveStudyMonth(
              StudyMonth(
                name: 'شهر $count حصص',
                price: 21000,
                lessons: [
                  for (var number = 1; number <= count; number++)
                    PreparedLesson(number: number),
                ],
              ),
            ),
          );
        }
        await tester.tap(find.widgetWithText(ListTile, 'المجموعات'));
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(FilledButton, 'مجموعة جديدة'));
        await tester.pumpAndSettle();
        expect(
          find.widgetWithText(TextFormField, 'سعة المجموعة'),
          findsNothing,
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'اسم أو رقم المجموعة'),
          'الأحد ٥',
        );
        await select(tester, 'المادة', 'فيزياء');
        await select(tester, 'السنتر', 'النور');
        await select(tester, 'الصف الدراسي', 'الثالث الثانوي');
        await tester.enterText(
          find.widgetWithText(TextFormField, 'المواعيد'),
          'الأحد ٥ مساءً',
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'سعر الحصة بالجنيه'),
          '100.25',
        );
        for (final (index, price) in [
          (0, '١٧٥٫٢٥'),
          (1, '280.10'),
          (2, '390.50'),
        ]) {
          await tester.enterText(
            find.byKey(ValueKey('group-month-price-${months[index].id}')),
            price,
          );
        }
        await commitAction(
          tester,
          find.widgetWithText(FilledButton, 'إضافة مجموعة'),
        );
        expect(store.groups.single.sessionPrice, 10025);
        expect(store.groups.single.monthPlans.map((p) => p.price), [
          21000,
          17525,
          28010,
          39050,
        ]);
        expect(store.groups.single.monthPlans.map((p) => p.sessions), [
          4,
          2,
          3,
          4,
        ]);
        expect(
          store.catalogName(store.groups.single.gradeId),
          'الثالث الثانوي',
        );
        expect(find.text('الأحد ٥'), findsOneWidget);
        expect(find.byType(AlertDialog), findsNothing);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.signIn('مدير الاختبار', 'test-password-2026');
        expect(store.groups.single.monthPlans.map((p) => p.price), [
          21000,
          17525,
          28010,
          39050,
        ]);
        expect(store.groups.single.monthPlans.map((p) => p.sessions), [
          4,
          2,
          3,
          4,
        ]);
      });
    },
  );

  testWidgets(
    'renewal chooses independent two and three session prices and adds to current balance',
    (tester) async {
      await tester.runAsync(() async {
        final group = await seedGroup();
        await store.saveGroup(
          group.copyWith(
            monthPlans: [
              const GroupMonthPlan(
                id: 'two',
                name: 'شهر حصتين',
                sessions: 2,
                price: 15725,
              ),
              const GroupMonthPlan(
                id: 'three',
                name: 'شهر ثلاث حصص',
                sessions: 3,
                price: 23999,
              ),
            ],
          ),
        );
        await store.saveStudent(
          Student(
            name: 'أحمد',
            code: '1',
            groupIds: [group.id],
            discountPercent: 25,
            createdAt: DateTime.now(),
          ),
        );
        await openWorkspace(tester);
        await tester.tap(find.widgetWithText(ListTile, 'الطلبة'));
        await settleDialogFrames(tester);
        for (final (count, label, amount, quote, balance) in [
          (2, 'شهر حصتين · 2 حصص', 15725, 11794, 2),
          (3, 'شهر ثلاث حصص · 3 حصص', 23999, 17999, 5),
        ]) {
          await tester.ensureVisible(find.byTooltip('تجديد الشهر'));
          await tester.ensureVisible(find.byTooltip('تجديد الشهر'));
          await settleDialogFrames(tester);
          await tester.tap(find.byTooltip('تجديد الشهر'));
          await settleDialogFrames(tester);
          final monthPicker = find.widgetWithText(
            DropdownButtonFormField<int>,
            'الشهر',
          );
          await tester.tap(monthPicker);
          await settleDialogFrames(tester);
          await tester.tap(find.text(label).last);
          await settleDialogFrames(tester);
          expect(find.text('المطلوب: ${money(quote)}'), findsOneWidget);
          expect(find.text('المدفوع الآن: ${money(quote)}'), findsOneWidget);
          await commitAction(
            tester,
            find.byKey(const Key('confirm-paid-amount')),
          );
          final package = store.packages.last;
          final payment = store.payments.last;
          expect(package.totalSessions, count);
          expect(package.remaining, count);
          expect(payment.baseAmount, amount);
          expect(payment.discountPercent, 25);
          expect(
            store.remainingFor(store.students.single.id, group.id),
            balance,
          );
        }
        expect(store.payments.map((p) => p.netAmount), [11794, 17999]);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'renewal lists only configured months and permits an explicit zero-price month',
    (tester) async {
      await tester.runAsync(() async {
        final group = await seedGroup();
        await store.saveGroup(
          group.copyWith(
            monthPlans: [
              const GroupMonthPlan(
                id: 'four',
                name: 'شهر أربع حصص',
                sessions: 4,
                price: 40000,
              ),
            ],
          ),
        );
        await store.saveStudent(
          Student(
            name: 'مينا',
            code: '2',
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
        await openWorkspace(tester);
        await navigateManagementPage(tester, 'الطلبة');
        await tester.ensureVisible(find.byTooltip('تجديد الشهر'));
        await settleDialogFrames(tester);
        await tester.tap(find.byTooltip('تجديد الشهر'));
        await settleDialogFrames(tester);
        final monthPicker = find.widgetWithText(
          DropdownButtonFormField<int>,
          'الشهر',
        );
        expect(
          tester
              .widget<DropdownButton<int>>(
                find.descendant(
                  of: monthPicker,
                  matching: find.byType(DropdownButton<int>),
                ),
              )
              .items,
          hasLength(2),
        );
        await tester.tap(monthPicker);
        await settleDialogFrames(tester);
        await tester.tap(find.text('شهر أربع حصص · 4 حصص').last);
        await settleDialogFrames(tester);
        expect(find.text('المطلوب: ${money(40000)}'), findsOneWidget);
        await tester.tap(find.text('إلغاء · Esc'));
        await settleDialogFrames(tester);
        expect(store.packages, isEmpty);
        expect(store.payments, isEmpty);
        await store.saveGroup(
          store.groups.single.copyWith(
            monthPlans: [
              ...store.groups.single.monthPlans,
              const GroupMonthPlan(
                id: 'free-two',
                name: 'شهر مجاني',
                sessions: 2,
                price: 0,
              ),
            ],
          ),
        );
        await tester.ensureVisible(find.byTooltip('تجديد الشهر'));
        await settleDialogFrames(tester);
        await tester.tap(find.byTooltip('تجديد الشهر'));
        await settleDialogFrames(tester);
        await tester.tap(
          find.widgetWithText(DropdownButtonFormField<int>, 'الشهر'),
        );
        await settleDialogFrames(tester);
        await tester.tap(find.text('شهر مجاني · 2 حصص').last);
        await settleDialogFrames(tester);
        expect(find.text('المطلوب: ${money(0)}'), findsOneWidget);
        await commitAction(
          tester,
          find.byKey(const Key('confirm-paid-amount')),
        );
        expect(store.packages.single.totalSessions, 2);
        expect(store.payments.single.baseAmount, 0);
        expect(store.payments.single.netAmount, 0);
        expect(store.remainingFor(store.students.single.id, group.id), 2);
        expect(store.attendances, isEmpty);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'closing class requires confirmation and records paid absence through the page',
    (tester) async {
      await tester.runAsync(() async {
        final group = await seedGroup();
        await store.saveStudent(
          Student(
            name: 'أحمد',
            code: '1',
            groupIds: [group.id],
            createdAt: DateTime.now().subtract(const Duration(days: 1)),
          ),
        );
        await store.renewPackage(
          PackageRequest(
            studentId: store.students.single.id,
            groupId: group.id,
          ),
        );
        final month = await store.saveStudyMonth(
          StudyMonth(
            name: 'شهر الإغلاق',
            price: 40000,
            lessons: [for (var n = 1; n <= 4; n++) PreparedLesson(number: n)],
          ),
        );
        await store.startPreparedLesson(
          groupId: group.id,
          preparedLessonId: month.lessons.first.id,
        );
        await openWorkspace(tester);
        await tester.ensureVisible(find.byTooltip('إغلاق وتسجيل الغياب'));
        await tester.tap(find.byTooltip('إغلاق وتسجيل الغياب'));
        await tester.pumpAndSettle();
        expect(store.sessions.single.status, SessionStatus.open);
        await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
        await tester.pumpAndSettle();
        expect(store.remainingFor(store.students.single.id, group.id), 4);
        await tester.ensureVisible(find.byTooltip('إغلاق وتسجيل الغياب'));
        await tester.tap(find.byTooltip('إغلاق وتسجيل الغياب'));
        await tester.pumpAndSettle();
        await commitAction(
          tester,
          find.widgetWithText(FilledButton, 'إغلاق وتسجيل الغياب'),
        );
        expect(store.attendances.single.status, AttendanceStatus.absent);
        expect(store.remainingFor(store.students.single.id, group.id), 3);
        expect(store.payments, hasLength(1));
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'legacy academic recording preserves zero then distinguishes exam absence and homework',
    (tester) async {
      await tester.runAsync(() async {
        final group = await seedGroup();
        await store.saveStudent(
          Student(
            name: 'أحمد',
            code: '1',
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: 1,
            startsAt: DateTime.now(),
            createdAt: DateTime.now(),
          ),
        );
        await store.collectAndAttend(
          EntryRequest(
            studentId: store.students.single.id,
            sessionId: store.sessions.single.id,
            mode: EntryMode.single,
          ),
        );
        await store.saveAcademic(
          AcademicRecord(
            studentId: store.students.single.id,
            sessionId: store.sessions.single.id,
            updatedAt: DateTime.now(),
          ),
        );
        await openWorkspace(tester);
        await navigateManagementPage(tester, 'رصد الامتحانات والواجبات');
        await select(tester, 'الشهر المشترك', 'سجل الحصص السابق');
        await select(
          tester,
          'المجموعة التي بدأت الحصة',
          store.groupLabel(group.id),
        );
        final sessionField = find.widgetWithText(
          DropdownButtonFormField<String>,
          'الحصة القديمة للرصد',
        );
        await tester.tap(sessionField);
        await tester.pumpAndSettle();
        await tester.tap(find.textContaining('حصة 1 —').last);
        await tester.pumpAndSettle();
        expect(find.text('لم تُرصد'), findsOneWidget);
        await tester.tap(find.text('رصد'));
        await tester.pumpAndSettle();
        await tester.enterText(
          find.widgetWithText(TextFormField, 'درجة الطالب'),
          '0',
        );
        final homework = find.widgetWithText(
          DropdownButtonFormField<HomeworkStatus>,
          'الواجب',
        );
        await tester.tap(homework);
        await tester.pumpAndSettle();
        await tester.tap(find.text('كامل').last);
        await tester.pumpAndSettle();
        await commitAction(
          tester,
          find.widgetWithText(FilledButton, 'حفظ الرصد'),
        );
        expect(store.academics.single.score, 0);
        expect(store.academics.single.homework, HomeworkStatus.complete);
        expect(find.text('0 / 10'), findsOneWidget);
        await tester.tap(find.text('رصد'));
        await tester.pumpAndSettle();
        await tester.tap(
          find.widgetWithText(CheckboxListTile, 'غائب عن الامتحان'),
        );
        await tester.pumpAndSettle();
        await commitAction(
          tester,
          find.widgetWithText(FilledButton, 'حفظ الرصد'),
        );
        expect(store.academics.single.score, isNull);
        expect(store.academics.single.homework, HomeworkStatus.complete);
        expect(find.text('غائب عن الامتحان'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    },
  );
}
