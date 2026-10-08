import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
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
      directory = await Directory.systemTemp.createTemp(
        'massar-closing-categories-',
      );
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

  Future<void> open(
    WidgetTester tester,
    Widget page, {
    bool dark = false,
  }) async {
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
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: Scaffold(body: page),
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
    } finally {
      store.removeListener(listener);
    }
    await tester.pumpAndSettle();
  }

  testWidgets(
    'ended session categories separate distinct students from payment operations and preserve discounts after saving',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveSession(
          session.copyWith(
            startsAt: DateTime.now().add(const Duration(minutes: 10)),
          ),
        );
        session = store.sessions.single;
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: 2,
            startsAt: session.startsAt.add(const Duration(hours: 1)),
            kind: SessionKind.free,
            createdAt: DateTime.now(),
          ),
        );
        final newer = store.sessions.last;
        await store.closeSession(newer.id);
        Future<Student> add(String code, String name, int discount) async {
          await store.saveStudent(
            Student(
              code: code,
              name: name,
              discountPercent: discount,
              groupIds: [group.id],
              createdAt: DateTime.now(),
            ),
          );
          return store.students.last;
        }

        final full = await add('124', 'مينا عادل', 0);
        final exempt = await add('125', 'يوسف سامح', 100);
        final buyer = await add('126', 'كريم هاني', 0);
        final prepaid = await add('127', 'مارك فادي', 50);
        final sameAmount = await add('129', 'رامي باسم', 50);
        await add('128', 'عمر أحمد', 0);
        for (final value in [student, full, exempt]) {
          await store.collectAndAttend(
            EntryRequest(
              studentId: value.id,
              sessionId: session.id,
              mode: EntryMode.single,
            ),
          );
        }
        await store.saveGroup(group.copyWith(sessionPrice: 15000));
        await store.collectAndAttend(
          EntryRequest(
            studentId: sameAmount.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.saveGroup(group);
        for (var count = 0; count < 2; count++) {
          await store.renewPackage(
            PackageRequest(
              studentId: buyer.id,
              groupId: group.id,
              sessionId: session.id,
            ),
          );
        }
        await store.collectAndAttend(
          EntryRequest(
            studentId: buyer.id,
            sessionId: session.id,
            mode: EntryMode.package,
          ),
        );
        await store.renewPackage(
          PackageRequest(studentId: prepaid.id, groupId: group.id),
        );
        await store.saveStudent(prepaid.copyWith(discountPercent: 25));
        await store.collectAndAttend(
          EntryRequest(
            studentId: prepaid.id,
            sessionId: session.id,
            mode: EntryMode.package,
          ),
        );
        await store.saveStudent(prepaid.copyWith(discountPercent: 50));
        await store.closeSession(session.id);
        await open(
          tester,
          ClosingsPage(store: store, initialSessionId: session.id),
        );
        expect(
          find.byKey(ValueKey('closing-session-${session.id}')),
          findsOneWidget,
        );
        expect(
          find.text('شهر 1 · حصة 1 · ${store.groupLabel(group.id)}'),
          findsOneWidget,
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-students-package-0-40000')),
              )
              .data,
          '1',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-operations-package-0-40000')),
              )
              .data,
          '2',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-label-single-25-7500')),
              )
              .data,
          'حصة بخصم 25٪',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-label-single-0-10000')),
              )
              .data,
          'حصة بالسعر الكامل',
        );
        expect(
          tester
              .widget<Text>(find.byKey(const Key('category-label-free-none-0')))
              .data,
          'حضور مجاني أو بإعفاء من رسوم المدرس',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-students-prepaid-50-0')),
              )
              .data,
          '1',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-operations-prepaid-50-0')),
              )
              .data,
          '0',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-label-prepaid-50-0')),
              )
              .data,
          'حضور بباقة سابقة بخصم 50٪',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-amount-prepaid-50-0')),
              )
              .data,
          money(0),
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-students-absent-none-0')),
              )
              .data,
          '1',
        );
        expect(store.closings, isEmpty);
        for (final dark in [false, true]) {
          for (final width in [1440.0, 1280.0]) {
            await open(
              tester,
              ManagementWorkspace(store: store, onOpenAttendance: () {}),
              dark: dark,
            );
            await tester.binding.setSurfaceSize(Size(width, 900));
            await tester.pumpAndSettle();
            await tester.ensureVisible(find.text('تقفيلة الحسابات'));
            await tester.tap(find.text('تقفيلة الحسابات'));
            await tester.pumpAndSettle();
            final dropdown = find.widgetWithText(
              DropdownButtonFormField<String>,
              'الحصة',
            );
            await tester.tap(dropdown);
            await tester.pumpAndSettle();
            await tester.tap(
              find
                  .text(
                    'شهر 1 · حصة 1 · ${shortDate(session.startsAt)} · الحضور مغلق',
                  )
                  .last,
            );
            await tester.pumpAndSettle();
            expect(
              find.byKey(const Key('category-students-package-0-40000')),
              findsOneWidget,
            );
            expect(
              find.byKey(const Key('attendance-discount-25')),
              findsOneWidget,
            );
            expect(find.byTooltip('حضور بخصم ثابت 25٪'), findsOneWidget);
            expect(
              tester
                  .widget<Text>(
                    find.byKey(const Key('attendance-discount-students-25')),
                  )
                  .data,
              '2',
            );
            expect(
              tester
                  .widget<Text>(
                    find.byKey(
                      const Key('payment-amount-students-single-7500'),
                    ),
                  )
                  .data,
              '2',
            );
            expect(
              tester
                  .widget<Text>(
                    find.byKey(
                      const Key('payment-amount-operations-package-40000'),
                    ),
                  )
                  .data,
              '2',
            );
            expect(
              find.text('إجمالي إعفاء رسوم المدرس: 1 طالب'),
              findsOneWidget,
            );
            expect(
              find.text('مشترو الباقات لهذه الحصة: 1 طالب'),
              findsOneWidget,
            );
            expect(
              tester
                  .getRect(find.byKey(const Key('finalize-session-finance')))
                  .bottom,
              lessThanOrEqualTo(900),
            );
            expect(tester.takeException(), isNull);
            await capture(
              tester,
              'closing-categories-${dark ? 'dark' : 'light'}-shell-${width.toInt()}',
            );
          }
        }
        await tester.enterText(
          find.byKey(const Key('closing-actual-cash')),
          '١٠٥٠',
        );
        final save = find.byKey(const Key('finalize-session-finance'));
        await tester.ensureVisible(save);
        await tester.tap(save);
        await tester.pumpAndSettle();
        await mutate(
          tester,
          () => tester.tap(
            find.widgetWithText(FilledButton, 'حفظ التقفيلة النهائية'),
          ),
        );
        final saved = store.closings.single;
        expect(saved.sessionId, session.id);
        expect(saved.summary.totalCollected, 105000);
        expect(
          saved.summary.studentCategories!
              .where((c) => c.kind == SessionStudentCategoryKind.package)
              .single
              .operationCount,
          2,
        );
        await store.saveStudent(student.copyWith(discountPercent: 50));
        await store.saveStudent(prepaid.copyWith(discountPercent: 100));
        await tester.pumpAndSettle();
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-label-single-25-7500')),
              )
              .data,
          'حصة بخصم 25٪',
        );
        expect(
          find.byKey(const Key('category-label-single-50-5000')),
          findsNothing,
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-label-prepaid-50-0')),
              )
              .data,
          'حضور بباقة سابقة بخصم 50٪',
        );
        expect(
          find.byKey(const Key('category-label-prepaid-100-0')),
          findsNothing,
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-operations-package-0-40000')),
              )
              .data,
          '2',
        );
        expect(find.byKey(const Key('closing-actual-cash')), findsNothing);
        expect(find.text('النقدية المحفوظة في التقفيلة'), findsOneWidget);
        await capture(tester, 'closing-categories-saved');
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'free class category contains students without invented payment operations',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveSession(session.copyWith(kind: SessionKind.free));
        await store.saveStudent(
          Student(
            code: '124',
            name: 'مينا المعفى',
            discountPercent: 100,
            groupIds: [group.id],
            createdAt: DateTime.now().subtract(const Duration(days: 1)),
          ),
        );
        final exempt = store.students.last;
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.collectAndAttend(
          EntryRequest(
            studentId: exempt.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.closeSession(session.id);
        await open(
          tester,
          ClosingsPage(store: store, initialSessionId: session.id),
        );
        expect(
          tester
              .widget<Text>(find.byKey(const Key('category-label-free-none-0')))
              .data,
          'حضور مجاني أو بإعفاء من رسوم المدرس',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-students-free-none-0')),
              )
              .data,
          '2',
        );
        expect(
          tester
              .widget<Text>(
                find.byKey(const Key('category-operations-free-none-0')),
              )
              .data,
          '0',
        );
        expect(store.payments, isEmpty);
        expect(find.text('إجمالي إعفاء رسوم المدرس: 2 طالب'), findsOneWidget);
        expect(
          find.byKey(const Key('attendance-discount-100')),
          findsOneWidget,
        );
        expect(store.sessionFinancialSummary(session.id).totalCollected, 0);
        await store.finalizeSession(sessionId: session.id, actualCash: 0);
        await store.saveStudent(exempt.copyWith(discountPercent: 0));
        await tester.pumpAndSettle();
        expect(find.text('إجمالي إعفاء رسوم المدرس: 2 طالب'), findsOneWidget);
        expect(
          find.byKey(const Key('attendance-discount-100')),
          findsOneWidget,
        );
        expect(store.closings.single.summary.allFreeCount, 2);
        expect(tester.takeException(), isNull);
      });
    },
  );
}
