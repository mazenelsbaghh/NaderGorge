import 'dart:io';
import 'dart:async';
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
import 'package:massar_center/features/management/academics_page.dart';
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
        notes: 'سجل قديم محفوظ',
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
    bool direct = false,
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
        builder: (context, child) =>
            Directionality(textDirection: TextDirection.rtl, child: child!),
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: RepaintBoundary(
            key: captureKey,
            child: Scaffold(
              body: direct
                  ? AcademicsPage(store: store)
                  : ManagementWorkspace(store: store, onOpenAttendance: () {}),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    if (!direct) {
      await tester.ensureVisible(
        find.widgetWithText(ListTile, 'الامتحانات والواجب'),
      );
      await tester.tap(find.widgetWithText(ListTile, 'الامتحانات والواجب'));
      await tester.pumpAndSettle();
    }
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

  Future<void> submit(
    WidgetTester tester,
    String query, {
    bool detailed = true,
  }) async {
    await tester.enterText(
      find.byKey(const Key('academic-code-search')),
      query,
    );
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
    final details = find.byKey(const Key('academic-quick-details'));
    if (detailed && details.evaluate().isNotEmpty) {
      await tester.ensureVisible(details);
      await tester.tap(details);
      await tester.pumpAndSettle();
    }
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

  Future<void> saveEditor(WidgetTester tester, {bool twice = false}) async {
    final button = find.widgetWithText(FilledButton, 'حفظ');
    await tester.ensureVisible(button);
    final callback = tester.widget<FilledButton>(button).onPressed!;
    final persisted = Completer<void>();
    void changed() {
      if (!persisted.isCompleted) persisted.complete();
    }

    store.addListener(changed);
    try {
      await tester.tap(button);
      if (twice) callback();
      await persisted.future.timeout(const Duration(seconds: 5));
      await Future<void>(() {});
      await acknowledgeNotice(tester, message: 'حُفظت البيانات بنجاح.');
      await tester.pumpAndSettle();
    } finally {
      store.removeListener(changed);
    }
    expect(find.byType(AlertDialog), findsNothing);
  }

  void expectCodeFocus(WidgetTester tester) {
    final code = tester.widget<TextField>(
      find.byKey(const Key('academic-code-search')),
    );
    expect(code.focusNode!.hasFocus, isTrue);
  }

  Future<AcademicActivity> create(
    WidgetTester tester,
    AcademicActivityKind kind,
    String name, {
    String maximum = '20',
    bool twice = false,
  }) async {
    final action = find.byKey(
      Key(
        kind == AcademicActivityKind.exam
            ? 'add-academic-exam'
            : 'add-academic-homework',
      ),
    );
    await tester.ensureVisible(action);
    await tester.tap(action);
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('academic-activity-name')),
      name,
    );
    if (kind == AcademicActivityKind.exam) {
      await tester.enterText(
        find.byKey(const Key('academic-activity-max')),
        maximum,
      );
    } else {
      expect(find.byKey(const Key('academic-activity-max')), findsNothing);
    }
    await saveEditor(tester, twice: twice);
    final activity = store.academicActivities.singleWhere(
      (item) => item.name == name && item.sessionId == session.id,
    );
    expect(
      tester
          .widget<DropdownButtonFormField<String>>(
            find.widgetWithText(
              DropdownButtonFormField<String>,
              'اختر الامتحان أو الواجب',
            ),
          )
          .initialValue,
      activity.id,
    );
    expectCodeFocus(tester);
    return activity;
  }

  Future<void> chooseActivity(WidgetTester tester, String label) async {
    final picker = find.widgetWithText(
      DropdownButtonFormField<String>,
      'اختر الامتحان أو الواجب',
    );
    await tester.ensureVisible(picker);
    await tester.tap(picker);
    await tester.pumpAndSettle();
    await tester.tap(find.text(label).last);
    await tester.pumpAndSettle();
    expectCodeFocus(tester);
  }

  testWidgets(
    'named exams and homework in one session persist independent results and preserve legacy evidence',
    (tester) async {
      await tester.runAsync(() async {
        await openPage(tester, dark: false, size: const Size(1280, 900));
        final legacy = store.academics.single;
        final firstExam = await create(
          tester,
          AcademicActivityKind.exam,
          'اختبار الحركة',
          maximum: '٢٠',
        );
        await submit(tester, '101');
        expect(find.textContaining(firstExam.name), findsWidgets);
        expect(
          find.widgetWithText(
            DropdownButtonFormField<HomeworkStatus>,
            'الواجب',
          ),
          findsNothing,
        );
        final maximum = find.descendant(
          of: find.byKey(const Key('academic-result-max')),
          matching: find.byType(EditableText),
        );
        expect(tester.widget<EditableText>(maximum).readOnly, isTrue);
        expect(
          tester
              .widget<TextFormField>(
                find.byKey(const Key('academic-result-max')),
              )
              .controller!
              .text,
          '20',
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'درجة الطالب'),
          '٠',
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'ملاحظات الرصد'),
          'رصد الامتحان الأول',
        );
        await saveEditor(tester);
        expectCodeFocus(tester);
        final secondExam = await create(
          tester,
          AcademicActivityKind.exam,
          'اختبار الطاقة',
          maximum: '30',
        );
        await submit(tester, '101');
        expect(
          tester
              .widget<TextFormField>(
                find.widgetWithText(TextFormField, 'درجة الطالب'),
              )
              .controller!
              .text,
          isEmpty,
        );
        expect(
          tester
              .widget<TextFormField>(
                find.widgetWithText(TextFormField, 'ملاحظات الرصد'),
              )
              .controller!
              .text,
          isEmpty,
        );
        await tester.tap(
          find.widgetWithText(CheckboxListTile, 'غائب عن الامتحان'),
        );
        await tester.pumpAndSettle();
        await saveEditor(tester);
        final firstHomework = await create(
          tester,
          AcademicActivityKind.homework,
          'واجب الفصل الأول',
        );
        await submit(tester, '101');
        expect(find.widgetWithText(TextFormField, 'درجة الطالب'), findsNothing);
        expect(find.byKey(const Key('academic-result-max')), findsNothing);
        expect(
          find.widgetWithText(CheckboxListTile, 'غائب عن الامتحان'),
          findsNothing,
        );
        await tester.enterText(
          find.widgetWithText(TextFormField, 'ملاحظات الرصد'),
          'المراجعة لاحقًا',
        );
        await saveEditor(tester);
        final secondHomework = await create(
          tester,
          AcademicActivityKind.homework,
          'تطبيق الطاقة',
        );
        await submit(tester, '101');
        await tester.tap(
          find.widgetWithText(
            DropdownButtonFormField<HomeworkStatus>,
            'الواجب',
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('ناقص').last);
        await tester.pumpAndSettle();
        await saveEditor(tester);
        AcademicRecord record(AcademicActivity item) =>
            store.academics.singleWhere(
              (row) =>
                  row.activityId == item.id &&
                  row.studentId == store.students.first.id,
            );
        expect(record(firstExam).score, 0);
        expect(record(firstExam).maxScore, 20);
        expect(record(firstExam).examAbsent, isFalse);
        expect(record(firstExam).homework, HomeworkStatus.notReviewed);
        expect(record(secondExam).score, isNull);
        expect(record(secondExam).examAbsent, isTrue);
        expect(record(firstHomework).homework, HomeworkStatus.notReviewed);
        expect(record(firstHomework).score, isNull);
        expect(record(secondHomework).homework, HomeworkStatus.incomplete);
        expect(
          store.academics.singleWhere((row) => row.activityId == null).toJson(),
          legacy.toJson(),
        );
        await chooseActivity(tester, 'امتحان: اختبار الحركة');
        expect(find.text('0 / 20'), findsOneWidget);
        expect(find.text('غائب عن الامتحان'), findsNothing);
        await capture(tester, 'named-academics-exam-light-1280');
        await submit(tester, '101');
        expect(
          tester
              .widget<TextFormField>(
                find.widgetWithText(TextFormField, 'ملاحظات الرصد'),
              )
              .controller!
              .text,
          'رصد الامتحان الأول',
        );
        final firstRecordId = record(firstExam).id;
        await tester.enterText(
          find.widgetWithText(TextFormField, 'ملاحظات الرصد'),
          'تمت مراجعة الصفر',
        );
        await saveEditor(tester, twice: true);
        expect(record(firstExam).id, firstRecordId);
        expect(record(secondExam).examAbsent, isTrue);
        await chooseActivity(tester, 'الرصد السابق للحصة');
        expect(find.text('7 / 10'), findsOneWidget);
        expect(find.text('سجل قديم محفوظ'), findsOneWidget);
        expect(find.text('0 / 20'), findsNothing);
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.signIn('الرصد', 'test-password-2026');
        expect(store.academicActivities, hasLength(4));
        expect(store.academics, hasLength(5));
        expect(record(firstExam).score, 0);
        expect(record(firstExam).notes, 'تمت مراجعة الصفر');
        expect(record(secondExam).examAbsent, isTrue);
        expect(record(firstHomework).homework, HomeworkStatus.notReviewed);
        expect(record(secondHomework).homework, HomeworkStatus.incomplete);
        expect(
          store.academics.singleWhere((row) => row.activityId == null).toJson(),
          legacy.toJson(),
        );
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'fresh session requires a named activity; cancel consumes none and assistant creates and saves once',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveSession(
          LessonSession(
            groupId: session.groupId,
            number: 9,
            startsAt: DateTime.now().subtract(const Duration(days: 1)),
            createdAt: DateTime.now().subtract(const Duration(days: 2)),
          ),
        );
        final fresh = store.sessions.last;
        await store.saveStaff(
          name: 'المساعد',
          password: 'assistant-academic-password',
          role: StaffRole.assistant,
        );
        store.signOut();
        await store.signIn('المساعد', 'assistant-academic-password');
        await openPage(tester, dark: true, size: const Size(1280, 900));
        final sessionPicker = find.widgetWithText(
          DropdownButtonFormField<String>,
          'اختر الحصة للرصد',
        );
        await tester.tap(sessionPicker);
        await tester.pumpAndSettle();
        await tester.tap(find.textContaining('حصة 9 —').last);
        await tester.pumpAndSettle();
        session = fresh;
        expect(
          tester
              .widget<TextField>(find.byKey(const Key('academic-code-search')))
              .enabled,
          isFalse,
        );
        expect(find.text('الرصد السابق للحصة'), findsNothing);
        expect(
          find.text('أضف امتحانًا أو واجبًا أولًا، ثم اختر اسمه للرصد.'),
          findsOneWidget,
        );
        await tester.tap(find.byKey(const Key('add-academic-exam')));
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byKey(const Key('academic-activity-name')),
          'مسودة ملغاة',
        );
        await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
        await tester.pumpAndSettle();
        expect(store.academicActivities, isEmpty);
        final exam = await create(
          tester,
          AcademicActivityKind.exam,
          'اختبار الحصة الجديدة',
          maximum: '15',
          twice: true,
        );
        expect(store.academicActivities, hasLength(1));
        expect(exam.sessionId, fresh.id);
        expect(find.text('الرصد السابق للحصة'), findsNothing);
        expect(
          tester
              .widget<TextField>(find.byKey(const Key('academic-code-search')))
              .enabled,
          isTrue,
        );
        await submit(tester, 'أحمد');
        expect(store.academicActivities, hasLength(1));
        expect(
          store.academics.where((row) => row.activityId == exam.id),
          isEmpty,
        );
        await acknowledgeNotice(
          tester,
          message: 'الاسم يطابق 2 طلبة. اختر الطالب من الجدول أو اكتب كوده.',
        );
        expect(find.byType(AlertDialog), findsNothing);
        await submit(tester, '102');
        await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
        await tester.pumpAndSettle();
        expectCodeFocus(tester);
        expect(
          store.academics.where((record) => record.activityId == exam.id),
          isEmpty,
        );
        await submit(tester, '102');
        await saveEditor(tester, twice: true);
        final row = store.academics.singleWhere(
          (row) => row.activityId == exam.id,
        );
        expect(row.score, isNull);
        expect(row.examAbsent, isFalse);
        expect(row.maxScore, 15);
        expectCodeFocus(tester);
        expect(
          tester
              .widget<DropdownButtonFormField<String>>(sessionPicker)
              .initialValue,
          fresh.id,
        );
        expect(tester.takeException(), isNull);
        await capture(tester, 'named-academics-exam-dark-1280');
      });
    },
  );

  testWidgets(
    'cashier can inspect a named result but cannot create or edit academic activities',
    (tester) async {
      await tester.runAsync(() async {
        final exam = await store.saveAcademicActivity(
          AcademicActivity(
            sessionId: session.id,
            kind: AcademicActivityKind.exam,
            name: 'نتيجة للعرض',
            maxScore: 12,
            createdAt: DateTime.now(),
          ),
        );
        await store.saveAcademic(
          AcademicRecord(
            studentId: store.students.first.id,
            sessionId: session.id,
            activityId: exam.id,
            score: 8,
            maxScore: 12,
            updatedAt: DateTime.now(),
          ),
        );
        await store.saveStaff(
          name: 'المحصل',
          password: 'cashier-academic-password',
          role: StaffRole.cashier,
        );
        store.signOut();
        await store.signIn('المحصل', 'cashier-academic-password');
        await openPage(
          tester,
          dark: false,
          size: const Size(1280, 900),
          direct: true,
        );
        expect(
          tester
              .widget<FilledButton>(find.byKey(const Key('add-academic-exam')))
              .onPressed,
          isNull,
        );
        expect(
          tester
              .widget<OutlinedButton>(
                find.byKey(const Key('add-academic-homework')),
              )
              .onPressed,
          isNull,
        );
        await submit(tester, '101');
        expect(find.byType(AlertDialog), findsNothing);
        expect(find.text('8 / 12'), findsOneWidget);
        expect(
          tester
              .widget<TextButton>(find.widgetWithText(TextButton, 'رصد'))
              .onPressed,
          isNull,
        );
        expect(store.academicActivities, hasLength(1));
        expect(store.academics, hasLength(2));
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'long named homework stays scoped to its class and fits dark desktop without score fields',
    (tester) async {
      await tester.runAsync(() async {
        await openPage(tester, dark: true, size: const Size(1280, 900));
        final name =
            'واجب مراجعة قوانين الحركة وتطبيقات الطاقة والتدريب الشامل على مسائل الفصل الأول قبل الامتحان';
        final homework = await create(
          tester,
          AcademicActivityKind.homework,
          name,
        );
        await submit(tester, '101');
        expect(find.widgetWithText(TextFormField, 'درجة الطالب'), findsNothing);
        expect(find.byKey(const Key('academic-result-max')), findsNothing);
        await tester.enterText(
          find.widgetWithText(TextFormField, 'ملاحظات الرصد'),
          'واجب مستقل',
        );
        await saveEditor(tester);
        expect(
          store.academics
              .singleWhere((row) => row.activityId == homework.id)
              .homework,
          HomeworkStatus.notReviewed,
        );
        await capture(tester, 'named-academics-homework-dark-1280');
        expect(tester.takeException(), isNull);
        await tester.binding.setSurfaceSize(const Size(960, 900));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        await store.saveSession(
          LessonSession(
            groupId: session.groupId,
            number: 9,
            startsAt: DateTime.now().subtract(const Duration(days: 1)),
            createdAt: DateTime.now().subtract(const Duration(days: 2)),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(
          find.widgetWithText(
            DropdownButtonFormField<String>,
            'اختر الحصة للرصد',
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.textContaining('حصة 9 —').last);
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('selected-academic-activity')),
          findsNothing,
        );
        expect(find.text('الرصد السابق للحصة'), findsNothing);
        expect(
          tester
              .widget<TextField>(find.byKey(const Key('academic-code-search')))
              .enabled,
          isFalse,
        );
        await tester.tap(
          find.widgetWithText(
            DropdownButtonFormField<String>,
            'اختر الحصة للرصد',
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.textContaining('حصة 8 —').last);
        await tester.pumpAndSettle();
        expect(
          tester
              .widget<DropdownButtonFormField<String>>(
                find.widgetWithText(
                  DropdownButtonFormField<String>,
                  'اختر الامتحان أو الواجب',
                ),
              )
              .initialValue,
          homework.id,
        );
        expect(find.text('واجب مستقل'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    },
  );
}
