import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/academics_page.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';
import '../helpers/academic_fixture.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late Student student;
  late LessonSession session;
  late List<Map<String, dynamic>> originalAttendance;
  late AcademicActivity exam;
  late AcademicActivity homework;
  late AcademicRecord legacy;
  final captureKey = GlobalKey();

  setUp(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-academic-keyboard-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('academic-manager', 'academic-manager-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'المجموعة',
          subjectId: store.catalogs[0].id,
          centerId: store.catalogs[1].id,
          gradeId: store.catalogs[2].id,
          sessionPrice: 10000,
          packagePrice: 35000,
        ),
      );
      for (final name in ['أحمد الأول', 'أحمد الثاني']) {
        await store.registerStudent(
          Student(
            name: name,
            groupIds: [store.groups.single.id],
            createdAt: DateTime.now().subtract(const Duration(days: 10)),
          ),
        );
      }
      student = store.students.first;
      await store.saveSession(
        LessonSession(
          groupId: store.groups.single.id,
          number: 1,
          startsAt: DateTime.now().subtract(const Duration(days: 2)),
          createdAt: DateTime.now().subtract(const Duration(days: 3)),
        ),
      );
      session = await prepareAcademicFixture(
        store,
        store.sessions.single,
        store.students,
      );
      originalAttendance = store.attendances
          .map((row) => row.toJson())
          .toList();
      exam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          kind: AcademicActivityKind.exam,
          name: 'امتحان الحركة',
          maxScore: 20,
          createdAt: DateTime.now(),
        ),
      );
      homework = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          kind: AcademicActivityKind.homework,
          name: 'واجب الحركة',
          createdAt: DateTime.now(),
        ),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: student.id,
          sessionId: session.id,
          score: 8,
          notes: 'رصد سابق محفوظ',
          updatedAt: DateTime.now(),
        ),
      );
      legacy = store.academics.single;
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

  final code = find.byKey(const Key('academic-code-search'));
  final quick = find.byKey(const Key('academic-quick-entry'));
  final score = find.byKey(const Key('academic-quick-score'));

  EditableText editor(WidgetTester tester, Finder field) =>
      tester.widget<EditableText>(
        find.descendant(of: field, matching: find.byType(EditableText)),
      );

  Finder selector(String label) =>
      find.widgetWithText(DropdownButtonFormField<String>, label);

  AcademicRecord named(String activityId) => store.academics.singleWhere(
    (record) =>
        record.studentId == student.id && record.activityId == activityId,
  );

  void expectNoFinanceAndLegacyPreserved() {
    expect(store.payments, isEmpty);
    expect(store.packages, isEmpty);
    expect(
      store.attendances.map((row) => row.toJson()).toList(),
      originalAttendance,
    );
    expect(store.cardPayments, isEmpty);
    expect(
      store.academics
          .singleWhere((record) => record.activityId == null)
          .toJson(),
      legacy.toJson(),
    );
  }

  void expectCodeFocus(WidgetTester tester) =>
      expect(editor(tester, code).focusNode.hasFocus, isTrue);

  Future<void> choose(WidgetTester tester, String label, String option) async {
    await tester.runAsync(() async {
      final picker = selector(label);
      await tester.ensureVisible(picker);
      await tester.pumpAndSettle();
      await tester.tap(picker);
      await tester.pumpAndSettle();
      await tester.tap(find.text(option).last);
      await tester.pump(const Duration(milliseconds: 200));
      if (label == 'اختر الامتحان أو الواجب' && option.startsWith('واجب:')) {
        for (var attempt = 0; attempt < 100; attempt++) {
          await Future<void>.delayed(const Duration(milliseconds: 10));
          await tester.pump(const Duration(milliseconds: 20));
          if (store.academics
                      .where((record) => record.activityId == homework.id)
                      .length ==
                  2 &&
              tester.widget<TextField>(code).enabled == true) {
            break;
          }
        }
        expect(
          store.academics.where((record) => record.activityId == homework.id),
          hasLength(2),
        );
        expect(tester.widget<TextField>(code).enabled, isTrue);
      }
      await tester.pumpAndSettle();
    });
  }

  Future<void> open(
    WidgetTester tester, {
    bool dark = false,
    Size size = const Size(1440, 1000),
    double textScale = 1,
  }) async {
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: dark ? MassarTheme.dark : MassarTheme.light,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: TextScaler.linear(textScale)),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: child!,
          ),
        ),
        home: RepaintBoundary(
          key: captureKey,
          child: Scaffold(body: AcademicsPage(store: store)),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await selectAcademicFixtureSession(tester, store, session);
    expect(
      tester
          .widget<DropdownButtonFormField<String>>(
            selector('اختر الامتحان أو الواجب'),
          )
          .initialValue,
      exam.id,
    );
    expectCodeFocus(tester);
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (Platform.environment['CAPTURE_UI'] != 'true' &&
        !const bool.fromEnvironment('CAPTURE_UI')) {
      return;
    }
    await tester.pump();
    await tester.runAsync(() async {
      final boundary =
          captureKey.currentContext!.findRenderObject()!
              as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 1);
      try {
        final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
        final output = File('build/verification/$name.png');
        await output.parent.create(recursive: true);
        await output.writeAsBytes(bytes!.buffer.asUint8List(), flush: true);
      } finally {
        image.dispose();
      }
    });
  }

  Future<void> submitCode(WidgetTester tester, String query) async {
    await tester.enterText(code, query);
    await tester.runAsync(() => tester.sendKeyEvent(LogicalKeyboardKey.enter));
    await tester.pumpAndSettle();
  }

  Future<void> openRow(WidgetTester tester) async {
    await tester.enterText(code, student.code);
    await tester.pumpAndSettle();
    final action = find.widgetWithText(TextButton, 'رصد').first;
    await tester.ensureVisible(action);
    await tester.tap(action);
    await tester.pumpAndSettle();
  }

  Future<void> persist(
    WidgetTester tester,
    Future<void> Function() gesture,
  ) async {
    await tester.runAsync(() async {
      final committed = Completer<void>();
      void observe() {
        if (!committed.isCompleted) committed.complete();
      }

      store.addListener(observe);
      try {
        await gesture();
        await committed.future.timeout(const Duration(seconds: 5));
        await Future<void>.delayed(const Duration(milliseconds: 20));
        await tester.pump(const Duration(milliseconds: 200));
        await tester.pumpAndSettle();
      } finally {
        store.removeListener(observe);
      }
    });
    expect(quick, findsNothing);
    expectCodeFocus(tester);
    expectNoFinanceAndLegacyPreserved();
  }

  for (final (grade, text) in [(0, '٠'), (17, '١٧')]) {
    testWidgets(
      'code Enter then Arabic grade $text Enter writes named exam and restores code focus',
      (tester) async {
        await open(tester, size: const Size(1280, 900));
        final audits = store.audit.length;
        await submitCode(tester, student.code);
        expect(quick, findsOneWidget);
        expect(find.byType(AlertDialog), findsNothing);
        expect(editor(tester, score).focusNode.hasFocus, isTrue);
        expect(store.academics, hasLength(1));
        if (grade == 0) {
          await capture(tester, 'academics-page-quick-exam-1280-light');
        }
        await tester.enterText(score, text);
        await persist(
          tester,
          () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
        );
        expect(named(exam.id).score, grade);
        expect(named(exam.id).maxScore, 20);
        expect(named(exam.id).homework, HomeworkStatus.notReviewed);
        expect(store.academics, hasLength(2));
        expect(store.audit, hasLength(audits + 1));
        await tester.pumpWidget(const SizedBox.shrink());
        await tester.runAsync(() async {
          await store.close();
          store = await CenterStore.open(directory: directory.path);
          await store.signIn('academic-manager', 'academic-manager-password');
          expect(named(exam.id).score, grade);
          expectNoFinanceAndLegacyPreserved();
        });
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'empty and out-of-range grades keep old result unchanged until a valid retry',
    (tester) async {
      await tester.runAsync(
        () => store.saveAcademic(
          AcademicRecord(
            studentId: student.id,
            sessionId: session.id,
            activityId: exam.id,
            score: 11,
            maxScore: 20,
            updatedAt: DateTime.now(),
          ),
        ),
      );
      final original = named(exam.id);
      await open(tester);
      final audits = store.audit.length;
      await submitCode(tester, student.code);
      for (final invalid in ['', '21']) {
        await tester.enterText(score, invalid);
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await tester.pumpAndSettle();
        expect(quick, findsOneWidget);
        expect(find.text('أدخل درجة من صفر إلى 20'), findsOneWidget);
        expect(named(exam.id).toJson(), original.toJson());
        expect(store.audit, hasLength(audits));
        expectNoFinanceAndLegacyPreserved();
      }
      await tester.enterText(score, '١٩');
      await persist(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(named(exam.id).id, original.id);
      expect(named(exam.id).score, 19);
      expect(store.audit, hasLength(audits + 1));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'homework three quick choices update only homework while a named exam grade remains intact',
    (tester) async {
      await tester.runAsync(
        () => store.saveAcademic(
          AcademicRecord(
            studentId: student.id,
            sessionId: session.id,
            activityId: exam.id,
            score: 13,
            maxScore: 20,
            updatedAt: DateTime.now(),
          ),
        ),
      );
      final savedExam = named(exam.id);
      await open(tester, dark: true, size: const Size(1280, 900));
      await choose(tester, 'اختر الامتحان أو الواجب', 'واجب: ${homework.name}');
      final audits = store.audit.length;
      for (final (choice, status) in [
        ('complete', HomeworkStatus.complete),
        ('missing', HomeworkStatus.missing),
        ('incomplete', HomeworkStatus.incomplete),
      ]) {
        await openRow(tester);
        expect(quick, findsOneWidget);
        expect(score, findsNothing);
        expect(find.byKey(const Key('academic-quick-save')), findsNothing);
        final buttons = find.descendant(
          of: quick,
          matching: find.byWidgetPredicate(
            (widget) =>
                widget is ButtonStyleButton &&
                widget.key.toString().contains('academic-quick-homework-'),
          ),
        );
        expect(buttons, findsNWidgets(3));
        if (choice == 'complete') {
          await capture(tester, 'academics-page-quick-homework-1280-dark');
        }
        await persist(
          tester,
          () => tester.tap(find.byKey(Key('academic-quick-homework-$choice'))),
        );
        expect(named(homework.id).homework, status);
        expect(named(homework.id).score, isNull);
        expect(named(homework.id).examAbsent, isFalse);
        expect(named(exam.id).toJson(), savedExam.toJson());
      }
      expect(store.academics, hasLength(4));
      expect(store.audit, hasLength(audits + 3));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'modifier Enter and held opening Enter cannot accidentally open or save an exam',
    (tester) async {
      await open(tester);
      await tester.enterText(code, student.code);
      final audits = store.audit.length;
      await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
      await tester.pumpAndSettle();
      expect(quick, findsNothing);
      await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(quick, findsOneWidget);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(store.academics, hasLength(1));
      expect(store.audit, hasLength(audits));
      await tester.enterText(score, '5');
      await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
      await tester.pumpAndSettle();
      expect(quick, findsOneWidget);
      expect(store.academics, hasLength(1));
      expect(store.audit, hasLength(audits));
      await tester.tap(find.byKey(const Key('academic-quick-cancel')));
      await tester.pumpAndSettle();
      expect(find.text('تعديلات لم تُحفظ'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'خروج بدون حفظ'));
      await tester.pumpAndSettle();
      expect(quick, findsNothing);
      expectCodeFocus(tester);
      expectNoFinanceAndLegacyPreserved();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'row action uses the same inline panel and locks context until cancellation',
    (tester) async {
      await open(tester);
      final row = find.widgetWithText(TextButton, 'رصد').first;
      await tester.ensureVisible(row);
      await tester.tap(row);
      await tester.pumpAndSettle();
      expect(quick, findsOneWidget);
      expect(find.byType(AlertDialog), findsNothing);
      await tester.binding.setSurfaceSize(const Size(960, 900));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await capture(tester, 'academics-page-quick-exam-960-light');
      expect(
        find.descendant(of: quick, matching: find.text(student.name)),
        findsOneWidget,
      );
      for (final label in [
        'المجموعة التي بدأت الحصة',
        'الحصة المجهزة لكل المجموعات',
        'اختر الامتحان أو الواجب',
      ]) {
        expect(
          tester
              .widget<DropdownButtonFormField<String>>(selector(label))
              .onChanged,
          isNull,
        );
      }
      expect(tester.widget<TextField>(code).enabled, isFalse);
      final audits = store.audit.length;
      await tester.tap(find.byKey(const Key('academic-quick-cancel')));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(store.audit, hasLength(audits));
      for (final label in [
        'المجموعة التي بدأت الحصة',
        'الحصة المجهزة لكل المجموعات',
        'اختر الامتحان أو الواجب',
      ]) {
        expect(
          tester
              .widget<DropdownButtonFormField<String>>(selector(label))
              .onChanged,
          isNotNull,
        );
      }
      await choose(tester, 'اختر الامتحان أو الواجب', 'واجب: ${homework.name}');
      await openRow(tester);
      expect(score, findsNothing);
      expectNoFinanceAndLegacyPreserved();
      await tester.tap(find.byKey(const Key('academic-quick-cancel')));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(store.audit, hasLength(audits + 1));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'student moved out of the roster while inline entry is open keeps the draft and cancel reachable without an academic write',
    (tester) async {
      await open(tester);
      final target = store.students.last;
      await submitCode(tester, target.code);
      expect(quick, findsOneWidget);
      await tester.enterText(score, '14');
      final before = store.academics.map((record) => record.toJson()).toList();
      final audits = store.audit.length;
      await tester.runAsync(() async {
        await store.saveGroup(
          StudyGroup(
            name: 'المجموعة الجديدة',
            subjectId: store.groups.first.subjectId,
            centerId: store.groups.first.centerId,
            gradeId: store.groups.first.gradeId,
          ),
        );
        await store.saveStudent(
          target.copyWith(groupIds: [store.groups.last.id]),
        );
      });
      await tester.pumpAndSettle();
      expect(quick, findsOneWidget);
      expect(editor(tester, score).controller.text, '14');
      expect(
        store.academics.where((record) => record.studentId == target.id),
        isEmpty,
      );
      expect(
        find.descendant(of: quick, matching: find.text(target.name)),
        findsOneWidget,
      );
      final cancel = find.byKey(const Key('academic-quick-cancel'));
      await tester.ensureVisible(cancel);
      await tester.tap(cancel);
      await tester.pumpAndSettle();
      expect(find.text('تعديلات لم تُحفظ'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'خروج بدون حفظ'));
      await tester.pumpAndSettle();
      expect(quick, findsNothing);
      expectCodeFocus(tester);
      expect(store.academics.map((record) => record.toJson()).toList(), before);
      expectNoFinanceAndLegacyPreserved();
      expect(store.audit, hasLength(audits + 2));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'short enlarged viewport scrolls grading form and reaches table pagination without financial changes',
    (tester) async {
      await tester.runAsync(() async {
        for (var index = 0; index < 49; index++) {
          await store.registerStudent(
            Student(
              name: 'طالب الجدول $index',
              groupIds: [store.groups.single.id],
              createdAt: DateTime.now().subtract(const Duration(days: 10)),
            ),
          );
          await store.recordAttendance(
            EntryRequest(
              studentId: store.students.last.id,
              sessionId: session.id,
              mode: EntryMode.single,
            ),
          );
        }
      });
      originalAttendance = store.attendances
          .map((row) => row.toJson())
          .toList();
      await open(tester, size: const Size(960, 400), textScale: 2);
      expect(tester.takeException(), isNull);
      final audits = store.audit.length;
      await tester.ensureVisible(code);
      await tester.pumpAndSettle();
      expect(code.hitTestable(), findsOneWidget);
      await submitCode(tester, student.code);
      await tester.ensureVisible(score);
      await tester.pumpAndSettle();
      expect(score.hitTestable(), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.enterText(score, '٠');
      await persist(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(named(exam.id).score, 0);
      expect(store.audit, hasLength(audits + 1));
      final page = tester.widget<CustomScrollView>(
        find.byType(CustomScrollView),
      );
      page.controller!.jumpTo(page.controller!.position.maxScrollExtent);
      await tester.pumpAndSettle();
      expect(find.text('1 / 2').hitTestable(), findsOneWidget);
      await tester.tap(find.byTooltip('الصفحة التالية'));
      await tester.pumpAndSettle();
      expect(find.text('2 / 2').hitTestable(), findsOneWidget);
      expect(store.audit, hasLength(audits + 1));
      expectNoFinanceAndLegacyPreserved();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'unknown and ambiguous lookup warn without selecting a student or writing a result',
    (tester) async {
      await open(tester);
      final audits = store.audit.length;
      for (final (query, message) in [
        (
          'unknown-code',
          'لا يوجد طالب بهذا الكود أو الباركود أو الاسم حاضر أو معوّض فعليًا في هذه الحصة.',
        ),
        ('أحمد', 'الاسم يطابق 2 طلبة. اختر الطالب من الجدول أو اكتب كوده.'),
      ]) {
        await tester.runAsync(() async {
          await tester.enterText(code, query);
          await tester.sendKeyEvent(LogicalKeyboardKey.enter);
          if (query == 'أحمد') {
            await tester.pumpAndSettle();
            await cancelAcademicStudentChoice(tester);
          } else {
            await acknowledgeNotice(tester, message: message);
          }
        });
        await tester.pumpAndSettle();
        expect(quick, findsNothing);
        expectCodeFocus(tester);
        expect(store.academics, hasLength(1));
        expect(store.audit, hasLength(audits));
        expectNoFinanceAndLegacyPreserved();
      }
      await submitCode(tester, student.code);
      expect(quick, findsOneWidget);
      expect(
        find.descendant(of: quick, matching: find.text(student.name)),
        findsOneWidget,
      );
      await tester.tap(find.byKey(const Key('academic-quick-cancel')));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );
}
