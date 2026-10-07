import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import '../helpers/notice_helpers.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late LessonSession session;
  final captureKey = GlobalKey();

  setUp(() async {
    await initializeDateFormatting('ar_EG');
    directory = await Directory.systemTemp.createTemp('massar-academic-quick-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('الرصد', 'test-password-2026');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(
        CatalogEntry(kind: kind, name: 'اختبار ${kind.name}'),
      );
    }
    for (final name in ['الأحد', 'الثلاثاء']) {
      await store.saveGroup(
        StudyGroup(
          name: name,
          subjectId: store.catalogs
              .firstWhere((e) => e.kind == CatalogKind.subject)
              .id,
          centerId: store.catalogs
              .firstWhere((e) => e.kind == CatalogKind.center)
              .id,
          gradeId: store.catalogs
              .firstWhere((e) => e.kind == CatalogKind.grade)
              .id,
          sessionPrice: 10000,
          packagePrice: 40000,
        ),
      );
    }
    for (final (code, name) in [
      ('101', 'أحمد محمد'),
      ('102', 'أحمد سامح'),
      ('103', 'مينا منتقل'),
    ]) {
      await store.saveStudent(
        Student(
          code: code,
          name: name,
          groupIds: [store.groups.first.id],
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
    }
    await store.saveSession(
      LessonSession(
        groupId: store.groups.first.id,
        number: 8,
        startsAt: DateTime.now().subtract(const Duration(days: 2)),
        createdAt: DateTime.now().subtract(const Duration(days: 3)),
      ),
    );
    session = store.sessions.single;
    await store.saveAcademic(
      AcademicRecord(
        studentId: store.students.last.id,
        sessionId: session.id,
        score: 7,
        updatedAt: DateTime.now(),
      ),
    );
    await store.saveStudent(
      store.students.last.copyWith(groupIds: [store.groups.last.id]),
    );
    await store.saveStudent(
      Student(
        code: '999',
        name: 'طالب انضم بعد الحصة',
        groupIds: [store.groups.first.id],
        createdAt: DateTime.now(),
      ),
    );
  });

  tearDown(() async {
    await TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    });
  });

  Future<void> openPage(
    WidgetTester tester, {
    required bool dark,
    required Size size,
  }) async {
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await (FontLoader('Tajawal')
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
        .load();
    await (FontLoader(
      'MaterialIcons',
    )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    await tester.pumpWidget(
      MaterialApp(
        debugShowCheckedModeBanner: false,
        theme: dark ? MassarTheme.dark : MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: RepaintBoundary(
            key: captureKey,
            child: Scaffold(
              body: ManagementWorkspace(store: store, onOpenAttendance: () {}),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ListTile, 'الامتحانات والواجب'));
    await tester.pumpAndSettle();
    final groupPicker = find.widgetWithText(
      DropdownButtonFormField<String>,
      'المجموعة',
    );
    await tester.tap(groupPicker);
    await tester.pumpAndSettle();
    await tester.tap(find.text(store.groupLabel(store.groups.first.id)).last);
    await tester.pumpAndSettle();
    await tester.tap(
      find.widgetWithText(DropdownButtonFormField<String>, 'اختر الحصة للرصد'),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('حصة 8 —').last);
    await tester.pumpAndSettle();
  }

  Future<void> submit(WidgetTester tester, String query) async {
    await tester.enterText(
      find.byKey(const Key('academic-code-search')),
      query,
    );
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    final boundary =
        captureKey.currentContext!.findRenderObject() as RenderRepaintBoundary;
    final image = await boundary.toImage(pixelRatio: 1);
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    final file = File('build/verification/$name.png');
    await file.parent.create(recursive: true);
    await file.writeAsBytes(bytes!.buffer.asUint8List());
    image.dispose();
  }

  for (final dark in [false, true]) {
    testWidgets(
      'quick academic entry resolves code, disambiguates names and restores focus (${dark ? 'dark' : 'light'})',
      (tester) async {
        await tester.runAsync(() async {
          await openPage(tester, dark: dark, size: const Size(1280, 900));
          expect(
            find.text(
              'النتائج: 3 من 3 طالب — القائمة تخص المسجلين قبل الحصة وأصحاب السجلات الفعلية.',
            ),
            findsOneWidget,
          );
          expect(find.text('طالب انضم بعد الحصة'), findsNothing);
          expect(find.text('مينا منتقل'), findsOneWidget);
          await submit(tester, 'أحمد');
          expect(store.academics, hasLength(1));
          await acknowledgeNotice(
            tester,
            message: 'الاسم يطابق 2 طلبة. اختر الطالب من الجدول أو اكتب كوده.',
          );
          expect(find.byType(AlertDialog), findsNothing);
          expect(find.text('أحمد محمد'), findsOneWidget);
          expect(find.text('أحمد سامح'), findsOneWidget);
          await capture(
            tester,
            'academics-search-${dark ? 'dark' : 'light'}-1280',
          );
          await submit(tester, '101');
          expect(find.text('رصد أحمد محمد — حصة 8'), findsOneWidget);
          await tester.enterText(
            find.widgetWithText(TextFormField, 'الدرجة النهائية'),
            '٢٠',
          );
          await tester.enterText(
            find.widgetWithText(TextFormField, 'درجة الطالب'),
            '٠',
          );
          await tester.ensureVisible(find.widgetWithText(FilledButton, 'حفظ'));
          await tester.tap(find.widgetWithText(FilledButton, 'حفظ'));
          await acknowledgeNotice(tester, message: 'حُفظت البيانات بنجاح.');
          for (var attempt = 0; attempt < 40; attempt++) {
            await Future<void>.delayed(const Duration(milliseconds: 25));
            await tester.pumpAndSettle();
            if (find.byType(AlertDialog).evaluate().isEmpty) break;
          }
          expect(find.byType(AlertDialog), findsNothing);
          final saved = store.academics.singleWhere(
            (e) => e.studentId == store.students.first.id,
          );
          expect(saved.score, 0);
          expect(saved.maxScore, 20);
          expect(saved.examAbsent, isFalse);
          expect(saved.sessionId, session.id);
          expect(find.text('0 / 20'), findsOneWidget);
          final search = tester.widget<TextField>(
            find.byKey(const Key('academic-code-search')),
          );
          expect(search.controller!.text, isEmpty);
          expect(search.focusNode!.hasFocus, isTrue);
          final selected = tester.widget<DropdownButtonFormField<String>>(
            find.widgetWithText(
              DropdownButtonFormField<String>,
              'اختر الحصة للرصد',
            ),
          );
          expect(selected.initialValue, session.id);
          await submit(tester, '999');
          expect(store.academics, hasLength(2));
          await acknowledgeNotice(
            tester,
            message:
                'لا يوجد طالب بهذا الكود أو الاسم مسجل لهذه الحصة وقت إقامتها.',
          );
          expect(find.byType(AlertDialog), findsNothing);
          await submit(tester, '103');
          expect(find.text('رصد مينا منتقل — حصة 8'), findsOneWidget);
          await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
          await tester.pumpAndSettle();
          await tester.tap(find.byTooltip('كل طلبة الحصة'));
          await tester.pumpAndSettle();
          await tester.binding.setSurfaceSize(const Size(1440, 1000));
          await tester.pumpAndSettle();
          await capture(
            tester,
            'academics-recorded-${dark ? 'dark' : 'light'}-1440',
          );
          expect(tester.takeException(), isNull);
        });
      },
    );
  }
}
