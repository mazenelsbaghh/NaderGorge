import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/formatters.dart';
import '../helpers/notice_helpers.dart';

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

  Future<void> commitAction(
    WidgetTester tester,
    Finder action, {
    String message = 'حُفظت البيانات بنجاح.',
  }) async {
    await tester.ensureVisible(action);
    await tester.tap(action);
    await acknowledgeNotice(tester, message: message);
    for (var attempt = 0; attempt < 40; attempt++) {
      await Future<void>.delayed(const Duration(milliseconds: 50));
      await tester.pumpAndSettle(
        const Duration(milliseconds: 50),
        EnginePhase.sendSemanticsUpdate,
        const Duration(seconds: 5),
      );
      if (find.byType(AlertDialog).evaluate().isEmpty) return;
    }
    fail(
      'The saved dialog did not close: ${find.byType(Text).evaluate().map((element) => (element.widget as Text).data).join(' | ')}',
    );
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
          await commitAction(tester, find.widgetWithText(FilledButton, 'حفظ'));
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
        await tester.enterText(
          find.widgetWithText(TextFormField, 'سعر باقة حصتين بالجنيه'),
          '١٧٥٫٢٥',
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'سعر باقة ٣ حصص بالجنيه'),
          '280.10',
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'سعر باقة ٤ حصص بالجنيه'),
          '390.50',
        );
        await commitAction(tester, find.widgetWithText(FilledButton, 'حفظ'));
        expect(store.groups.single.sessionPrice, 10025);
        expect(store.groups.single.packagePrice, 39050);
        expect(store.groups.single.twoSessionPrice, 17525);
        expect(store.groups.single.threeSessionPrice, 28010);
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
        expect(store.groups.single.twoSessionPrice, 17525);
        expect(store.groups.single.threeSessionPrice, 28010);
        expect(store.groups.single.packagePrice, 39050);
      });
    },
  );

  testWidgets(
    'renewal chooses independent two and three session prices and adds to current balance',
    (tester) async {
      await tester.runAsync(() async {
        final group = await seedGroup();
        await store.saveGroup(
          group.copyWith(twoSessionPrice: 15725, threeSessionPrice: 23999),
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
        await tester.pumpAndSettle();
        for (final (count, label, amount, quote, balance) in [
          (2, 'حصتان', 15725, 11794, 2),
          (3, '٣ حصص', 23999, 17999, 5),
        ]) {
          await tester.ensureVisible(find.byTooltip('تجديد الباقة'));
          await tester.tap(find.byTooltip('تجديد الباقة'));
          await tester.pumpAndSettle();
          expect(
            tester
                .widget<DropdownButtonFormField<int>>(
                  find.byKey(const Key('renew-package-sessions')),
                )
                .initialValue,
            4,
          );
          await tester.tap(find.byKey(const Key('renew-package-sessions')));
          await tester.pumpAndSettle();
          await tester.tap(find.text(label).last);
          await tester.pumpAndSettle();
          expect(
            find.text('المطلوب بعد الخصم: ${money(quote)}'),
            findsOneWidget,
          );
          expect(
            find.text('الرصيد بعد التجديد: $balance حصص.'),
            findsOneWidget,
          );
          await commitAction(tester, find.widgetWithText(FilledButton, 'حفظ'));
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
    'unconfigured short package is blocked while explicit zero price is available',
    (tester) async {
      await tester.runAsync(() async {
        final group = await seedGroup();
        await store.saveStudent(
          Student(
            name: 'مينا',
            code: '2',
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
        await openWorkspace(tester);
        await tester.tap(find.widgetWithText(ListTile, 'الطلبة'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.byTooltip('تجديد الباقة'));
        await tester.tap(find.byTooltip('تجديد الباقة'));
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const Key('renew-package-sessions')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('حصتان').last);
        await tester.pumpAndSettle();
        expect(
          find.text(
            'سعر الباقة المختارة غير محدد لهذه المجموعة. اختر باقة لها سعر أو حدد سعرها من المجموعات.',
          ),
          findsOneWidget,
        );
        expect(find.textContaining('المطلوب بعد الخصم:'), findsNothing);
        await tester.ensureVisible(find.widgetWithText(FilledButton, 'حفظ'));
        await tester.tap(find.widgetWithText(FilledButton, 'حفظ'));
        await tester.pumpAndSettle();
        expect(
          find.text('حدد سعر هذه الباقة في المجموعة قبل التجديد.'),
          findsOneWidget,
        );
        expect(find.byType(AlertDialog), findsOneWidget);
        expect(store.packages, isEmpty);
        expect(store.payments, isEmpty);
        await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
        await tester.pumpAndSettle();
        await tester.tap(find.widgetWithText(ListTile, 'المجموعات'));
        await tester.pumpAndSettle();
        expect(find.text('—'), findsNWidgets(2));
        await tester.ensureVisible(find.byTooltip('تعديل المجموعة'));
        await tester.tap(find.byTooltip('تعديل المجموعة'));
        await tester.pumpAndSettle();
        expect(
          find.widgetWithText(TextFormField, 'سعة المجموعة'),
          findsNothing,
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'المواعيد'),
          'الأحد ٥ مساءً',
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'سعر باقة حصتين بالجنيه'),
          '٠',
        );
        await commitAction(tester, find.widgetWithText(FilledButton, 'حفظ'));
        expect(store.groups.single.twoSessionPrice, 0);
        expect(store.groups.single.threeSessionPrice, isNull);
        await tester.tap(find.widgetWithText(ListTile, 'الطلبة'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.byTooltip('تجديد الباقة'));
        await tester.tap(find.byTooltip('تجديد الباقة'));
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const Key('renew-package-sessions')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('حصتان').last);
        await tester.pumpAndSettle();
        expect(find.text('المطلوب بعد الخصم: ${money(0)}'), findsOneWidget);
        await commitAction(tester, find.widgetWithText(FilledButton, 'حفظ'));
        expect(store.packages.single.totalSessions, 2);
        expect(store.payments.single.baseAmount, 0);
        expect(store.remainingFor(store.students.single.id, group.id), 2);
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
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: 1,
            startsAt: DateTime.now().add(const Duration(minutes: 1)),
            createdAt: DateTime.now(),
          ),
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
          message: 'أُغلقت الحصة وسُجل الغياب.',
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
        await store.saveAcademic(
          AcademicRecord(
            studentId: store.students.single.id,
            sessionId: store.sessions.single.id,
            updatedAt: DateTime.now(),
          ),
        );
        await openWorkspace(tester);
        await tester.tap(find.widgetWithText(ListTile, 'الامتحانات والواجب'));
        await tester.pumpAndSettle();
        final sessionField = find.widgetWithText(
          DropdownButtonFormField<String>,
          'اختر الحصة للرصد',
        );
        await tester.tap(sessionField);
        await tester.pumpAndSettle();
        await tester.tap(find.textContaining('حصة 1 —').last);
        await tester.pumpAndSettle();
        expect(find.text('لم تُرصد'), findsOneWidget);
        await tester.tap(find.widgetWithText(TextButton, 'رصد'));
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
        await commitAction(tester, find.widgetWithText(FilledButton, 'حفظ'));
        expect(store.academics.single.score, 0);
        expect(store.academics.single.homework, HomeworkStatus.complete);
        expect(find.text('0 / 10'), findsOneWidget);
        await tester.tap(find.widgetWithText(TextButton, 'رصد'));
        await tester.pumpAndSettle();
        await tester.tap(
          find.widgetWithText(CheckboxListTile, 'غائب عن الامتحان'),
        );
        await tester.pumpAndSettle();
        await commitAction(tester, find.widgetWithText(FilledButton, 'حفظ'));
        expect(store.academics.single.score, isNull);
        expect(store.academics.single.homework, HomeworkStatus.complete);
        expect(find.text('غائب عن الامتحان'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    },
  );
}
