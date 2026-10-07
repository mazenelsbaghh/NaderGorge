import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/features/management/backup_page.dart';
import 'package:massar_center/features/management/cards_page.dart';
import 'package:massar_center/features/management/closings_page.dart';
import 'package:massar_center/features/management/corrections_page.dart';
import 'package:massar_center/features/management/management_widgets.dart';
import 'package:massar_center/features/management/reports_page.dart';
import 'package:massar_center/features/management/review_page.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;

  setUp(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-scroll-workspace-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('scroll-manager', 'scroll-manager-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة الاختبار',
          subjectId: store.catalogs[0].id,
          centerId: store.catalogs[1].id,
          gradeId: store.catalogs[2].id,
          sessionPrice: 6000,
          packagePrice: 24000,
        ),
      );
      await store.registerStudent(
        Student(
          name: 'طالب الاختبار',
          groupIds: [store.groups.single.id],
          createdAt: DateTime.now().subtract(const Duration(days: 2)),
        ),
      );
      await store.saveSession(
        LessonSession(
          groupId: store.groups.single.id,
          number: 1,
          startsAt: DateTime.now().subtract(const Duration(days: 1)),
          createdAt: DateTime.now().subtract(const Duration(days: 2)),
        ),
      );
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> host(WidgetTester tester, Widget page) async {
    await tester.binding.setSurfaceSize(const Size(960, 400));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.dark,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: const TextScaler.linear(2)),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: child!,
          ),
        ),
        home: Scaffold(body: page),
      ),
    );
    await tester.pumpAndSettle();
  }

  final pages = <String, Widget Function()>{
    'attendance': () => AttendanceWorkspace(
      store: store,
      onExit: () {},
      initialSessionId: store.sessions.single.id,
    ),
    'reports': () => ReportsPage(store: store),
    'backup': () => BackupPage(store: store),
    'cards': () => CardsPage(store: store),
    'closings': () =>
        ClosingsPage(store: store, initialSessionId: store.sessions.single.id),
    'corrections': () => CorrectionsPage(
      store: store,
      initialStudentId: store.students.single.id,
      initialSessionId: store.sessions.single.id,
    ),
    'review': () =>
        ReviewPage(store: store, sessionId: store.sessions.single.id),
  };

  for (final page in pages.entries) {
    testWidgets(
      '${page.key} form remains reachable at 960x400 with doubled text',
      (tester) async {
        final auditCount = store.audit.length;
        await host(tester, page.value());
        expect(tester.takeException(), isNull);
        final pageScroll = find.byType(CustomScrollView).first;
        final scroll = tester.widget<CustomScrollView>(pageScroll).controller!;
        await tester.drag(pageScroll, const Offset(0, -3000));
        await tester.pumpAndSettle();
        expect(scroll.offset, greaterThan(0));
        expect(tester.takeException(), isNull);
        final fields = find.byType(TextFormField);
        if (fields.evaluate().isNotEmpty) {
          await tester.ensureVisible(fields.last);
          await tester.pumpAndSettle();
          expect(fields.last.hitTestable(), findsOneWidget);
        }
        expect(tester.takeException(), isNull);
        expect(store.audit, hasLength(auditCount));
        expect(store.academics, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.payments, isEmpty);
        expect(store.packages, isEmpty);
      },
    );
  }

  testWidgets(
    'saving a long invalid form reveals the first error without saving',
    (tester) async {
      var saveCount = 0;
      final controller = TextEditingController();
      await host(
        tester,
        Builder(
          builder: (context) => Center(
            child: TextButton(
              onPressed: () => showDialog<void>(
                context: context,
                builder: (_) => ManagementEditor(
                  title: 'بيانات المجموعة',
                  fields: [
                    TextFormField(
                      key: const Key('invalid-first'),
                      controller: controller,
                      validator: requiredText,
                      decoration: const InputDecoration(
                        labelText: 'اسم المجموعة',
                      ),
                    ),
                    const SizedBox(height: 1200),
                    const Text('نهاية النموذج'),
                  ],
                  onSave: () async {
                    saveCount++;
                  },
                  controllers: [controller],
                ),
              ),
              child: const Text('فتح'),
            ),
          ),
        ),
      );
      await tester.tap(find.text('فتح'));
      await tester.pumpAndSettle();
      final save = find.widgetWithText(FilledButton, 'حفظ');
      await tester.ensureVisible(save);
      await tester.pumpAndSettle();
      await tester.tap(save);
      await tester.pumpAndSettle();
      expect(find.text('هذا الحقل مطلوب').hitTestable(), findsOneWidget);
      expect(
        find.byKey(const Key('invalid-first')).hitTestable(),
        findsOneWidget,
      );
      expect(saveCount, 0);
      expect(tester.takeException(), isNull);
    },
  );
}
