import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/academic_quick_entry.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late Student first, second;
  late LessonSession session;
  late AcademicActivity exam, homework;
  late ValueNotifier<Student> selected;
  final callerFocus = FocusNode();
  final captureKey = GlobalKey();
  var saved = 0, cancelled = 0, details = 0;
  final score = find.byKey(const Key('academic-quick-score'));
  final save = find.byKey(const Key('academic-quick-save'));

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      saved = cancelled = details = 0;
      directory = await Directory.systemTemp.createTemp(
        'massar-quick-academic-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('الإدارة', 'quick-academic-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة الرصد',
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
      for (final (code, name) in [
        ('401', 'يوسف الأول'),
        ('1002', 'مينا التالي'),
      ]) {
        await store.saveStudent(
          Student(
            code: code,
            name: name,
            groupIds: [store.groups.single.id],
            createdAt: DateTime.now().subtract(const Duration(days: 1)),
          ),
        );
      }
      first = store.students.first;
      second = store.students.last;
      selected = ValueNotifier(first);
      await store.saveSession(
        LessonSession(
          groupId: store.groups.single.id,
          number: 5,
          startsAt: DateTime.now(),
          createdAt: DateTime.now(),
        ),
      );
      session = store.sessions.single;
      exam = await store.saveAcademicActivity(
        AcademicActivity(
          sessionId: session.id,
          kind: AcademicActivityKind.exam,
          name: 'امتحان الحركة',
          maxScore: 20,
          createdAt: DateTime.now(),
        ),
      );
      homework = await store.saveAcademicActivity(
        AcademicActivity(
          sessionId: session.id,
          kind: AcademicActivityKind.homework,
          name: 'واجب الحركة',
          createdAt: DateTime.now(),
        ),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: first.id,
          sessionId: session.id,
          activityId: exam.id,
          score: 7,
          maxScore: 20,
          notes: 'ملاحظة الامتحان الأصلية',
          updatedAt: DateTime.now(),
        ),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: first.id,
          sessionId: session.id,
          activityId: homework.id,
          homework: HomeworkStatus.exempt,
          notes: 'ملاحظة الواجب الأصلية',
          updatedAt: DateTime.now(),
        ),
      );
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      selected.dispose();
      await store.close();
      await directory.delete(recursive: true);
    }),
  );
  tearDownAll(callerFocus.dispose);

  AcademicRecord record(String studentId, String activityId) =>
      store.academics.singleWhere(
        (row) => row.studentId == studentId && row.activityId == activityId,
      );

  Future<void> open(
    WidgetTester tester,
    AcademicActivity activity, {
    bool dark = true,
    double scale = 1,
    double width = 1280,
  }) async {
    await tester.binding.setSurfaceSize(Size(width, 800));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.runAsync(() async {
      await (FontLoader('Tajawal')
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
          .load();
    });
    await tester.pumpWidget(
      MaterialApp(
        theme: dark ? MassarTheme.dark : MassarTheme.light,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: TextScaler.linear(scale)),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: RepaintBoundary(key: captureKey, child: child!),
          ),
        ),
        home: Scaffold(
          body: SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Column(
              children: [
                TextField(
                  key: const Key('parent-code'),
                  focusNode: callerFocus,
                ),
                const SizedBox(height: 20),
                ValueListenableBuilder<Student>(
                  valueListenable: selected,
                  builder: (context, student, _) => AcademicQuickEntry(
                    store: store,
                    student: student,
                    session: session,
                    activity: activity,
                    onSaved: () {
                      saved++;
                      callerFocus.requestFocus();
                    },
                    onCancel: () {
                      cancelled++;
                      callerFocus.requestFocus();
                    },
                    onDetails: () => details++,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> enterScore(WidgetTester tester, String grade) async {
    await tester.enterText(score, grade);
    await tester.pump();
  }

  Future<void> saveMutation(
    WidgetTester tester,
    Future<void> Function() gesture,
    bool Function() persisted,
  ) async {
    await tester.runAsync(() async {
      final complete = Completer<void>();
      void changed() {
        if (persisted() && !complete.isCompleted) complete.complete();
      }

      store.addListener(changed);
      try {
        await gesture();
        await complete.future.timeout(const Duration(seconds: 5));
        await Future<void>.delayed(const Duration(milliseconds: 10));
      } finally {
        store.removeListener(changed);
      }
    });
    await tester.pumpAndSettle();
  }

  void expectNoFinance() {
    expect(store.payments, isEmpty);
    expect(store.cardPayments, isEmpty);
    expect(store.attendances, isEmpty);
    expect(store.packages, isEmpty);
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    await tester.runAsync(() async {
      final boundary =
          captureKey.currentContext!.findRenderObject()
              as RenderRepaintBoundary;
      final image = await boundary.toImage();
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      await Directory('build/verification').create(recursive: true);
      await File(
        'build/verification/$name.png',
      ).writeAsBytes(bytes!.buffer.asUint8List());
      image.dispose();
    });
  }

  testWidgets(
    'quick grade keeps latest notes and record ID with one key-up save and immediate code return',
    (tester) async {
      await open(tester, exam);
      final original = record(first.id, exam.id);
      expect(tester.widget<TextField>(score).focusNode!.hasFocus, isTrue);
      expect(
        tester.widget<TextField>(score).controller!.selection,
        const TextSelection(baseOffset: 0, extentOffset: 1),
      );
      await storeChange(
        tester,
        () => store.saveAcademic(
          original.copyWith(
            score: 9,
            notes: 'ملاحظة أُضيفت أثناء الرصد',
            updatedAt: DateTime.now(),
          ),
        ),
      );
      expect(tester.widget<TextField>(score).controller!.text, '7');
      await enterScore(tester, '٠');
      await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
      await tester.pump();
      expect(record(first.id, exam.id).score, 9);
      expect(saved, 0);
      await saveMutation(
        tester,
        () => tester.sendKeyUpEvent(LogicalKeyboardKey.enter),
        () => record(first.id, exam.id).score == 0,
      );
      final updated = record(first.id, exam.id);
      expect(updated.id, original.id);
      expect(updated.notes, 'ملاحظة أُضيفت أثناء الرصد');
      expect(updated.maxScore, 20);
      expect(updated.examAbsent, isFalse);
      expect(updated.homework, HomeworkStatus.notReviewed);
      expect(saved, 1);
      expect(callerFocus.hasFocus, isTrue);
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      await tester.tap(save);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pump();
      expect(saved, 1);
      expectNoFinance();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'three homework choices preserve latest notes and focused choice Enter never writes a default',
    (tester) async {
      await open(tester, homework);
      expect(find.byKey(const Key('academic-quick-score')), findsNothing);
      expect(find.text('اتعمل'), findsOneWidget);
      expect(find.text('ما اتعملش'), findsOneWidget);
      expect(find.text('ناقص'), findsOneWidget);
      expect(find.text('الحالة السابقة: معفى'), findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pump();
      expect(record(first.id, homework.id).homework, HomeworkStatus.exempt);
      expect(saved, 0);
      final original = record(first.id, homework.id);
      await storeChange(
        tester,
        () => store.saveAcademic(
          original.copyWith(
            notes: 'ملاحظة الواجب الأحدث',
            updatedAt: DateTime.now(),
          ),
        ),
      );
      final missing = find.byKey(const Key('academic-quick-homework-missing'));
      tester.widget<FilledButton>(missing).focusNode!.requestFocus();
      await tester.pump();
      await saveMutation(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
        () => record(first.id, homework.id).homework == HomeworkStatus.missing,
      );
      final updated = record(first.id, homework.id);
      expect(updated.id, original.id);
      expect(updated.notes, 'ملاحظة الواجب الأحدث');
      expect(updated.score, isNull);
      expect(updated.examAbsent, isFalse);
      expect(saved, 1);
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      expectNoFinance();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'scanner code 1002 and Enter on initial homework cannot save a choice for the previous student',
    (tester) async {
      await open(tester, homework);
      expect(second.code, '1002');
      final original = record(first.id, homework.id);
      final beforeAudit = store.audit
          .where((entry) => entry.action == 'academic_save')
          .length;
      await tester.runAsync(() async {
        for (final scanKey in [
          LogicalKeyboardKey.digit1,
          LogicalKeyboardKey.digit0,
          LogicalKeyboardKey.digit0,
          LogicalKeyboardKey.digit2,
          LogicalKeyboardKey.enter,
        ]) {
          await tester.sendKeyEvent(scanKey);
        }
        // Closing waits for queued SQLite work, so a late wrong-student save cannot hide behind the assertion.
        await store.close();
      });
      await tester.pumpAndSettle();
      final current = record(first.id, homework.id);
      expect(current.id, original.id);
      expect(current.homework, HomeworkStatus.exempt);
      expect(current.notes, original.notes);
      expect(
        store.academics.where((row) => row.studentId == second.id),
        isEmpty,
      );
      expect(
        store.audit.where((entry) => entry.action == 'academic_save').length,
        beforeAudit,
      );
      expect(saved, 0);
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      expectNoFinance();
      expect(tester.takeException(), isNull);
    },
  );

  for (final (key, status, label) in [
    (LogicalKeyboardKey.digit1, HomeworkStatus.complete, '1'),
    (LogicalKeyboardKey.digit2, HomeworkStatus.missing, '2'),
    (LogicalKeyboardKey.digit3, HomeworkStatus.incomplete, '3'),
  ]) {
    testWidgets(
      'initial homework Ctrl+$label shortcut saves its explicit choice only on fresh key-up',
      (tester) async {
        await open(tester, homework);
        final original = record(first.id, homework.id);
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await tester.pump();
        expect(record(first.id, homework.id).homework, HomeworkStatus.exempt);
        expect(saved, 0);
        await tester.sendKeyEvent(key);
        await tester.pump();
        expect(record(first.id, homework.id).homework, HomeworkStatus.exempt);
        await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
        await tester.sendKeyDownEvent(LogicalKeyboardKey.shiftLeft);
        await tester.sendKeyEvent(key);
        await tester.sendKeyUpEvent(LogicalKeyboardKey.shiftLeft);
        await tester.pump();
        expect(record(first.id, homework.id).homework, HomeworkStatus.exempt);
        await tester.sendKeyDownEvent(key);
        await tester.sendKeyRepeatEvent(key);
        await tester.pump();
        expect(record(first.id, homework.id).homework, HomeworkStatus.exempt);
        expect(saved, 0);
        await saveMutation(
          tester,
          () => tester.sendKeyUpEvent(key),
          () => record(first.id, homework.id).homework == status,
        );
        await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
        final updated = record(first.id, homework.id);
        expect(updated.id, original.id);
        expect(updated.notes, original.notes);
        expect(updated.score, isNull);
        expect(saved, 1);
        expect(callerFocus.hasFocus, isTrue);
        expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
        expectNoFinance();
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'opening detailed grading blocks the inline draft before callback and never saves on held Enter',
    (tester) async {
      await open(tester, exam);
      await enterScore(tester, '17');
      final detailButton = find.byKey(const Key('academic-quick-details'));
      tester.widget<TextButton>(detailButton).focusNode!.requestFocus();
      await tester.pump();
      await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
      expect(details, 0);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      await tester.pump();
      expect(details, 1);
      expect(saved, 0);
      expect(record(first.id, exam.id).score, 7);
      expect(tester.widget<FilledButton>(save).onPressed, isNull);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pump();
      expect(details, 1);
      expect(cancelled, 0);
      expect(record(first.id, exam.id).score, 7);
      expectNoFinance();
    },
  );

  testWidgets(
    'permission error keeps quick grade for retry without success notice or financial changes',
    (tester) async {
      await open(tester, exam);
      await enterScore(tester, '١٧');
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'الاستقبال',
          password: 'cashier-password',
          role: StaffRole.cashier,
        );
        store.signOut();
        await store.signIn('الاستقبال', 'cashier-password');
        await tester.tap(save);
        await acknowledgeNotice(
          tester,
          message: 'ليس لديك صلاحية لهذا الإجراء.',
        );
      });
      await tester.pumpAndSettle();
      expect(tester.widget<TextField>(score).controller!.text, '١٧');
      expect(record(first.id, exam.id).score, 7);
      expect(saved, 0);
      await storeChange(tester, () async {
        store.signOut();
        await store.signIn('الإدارة', 'quick-academic-password');
      });
      await saveMutation(
        tester,
        () => tester.tap(save),
        () => record(first.id, exam.id).score == 17,
      );
      expect(saved, 1);
      expect(record(first.id, exam.id).notes, 'ملاحظة الامتحان الأصلية');
      expectNoFinance();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'late completed save belongs to captured student and does not finish a replacement student panel',
    (tester) async {
      await open(tester, exam);
      await enterScore(tester, '19');
      late Future<void> queued;
      await saveMutation(tester, () async {
        queued = store.saveStaff(
          name: 'موظف أثناء الكتابة',
          password: 'queued-actor-password',
          role: StaffRole.assistant,
        );
        await tester.tap(save);
        selected.value = second;
        await tester.pump();
      }, () => record(first.id, exam.id).score == 19);
      await tester.runAsync(() => queued);
      await tester.pumpAndSettle();
      expect(saved, 0);
      expect(record(first.id, exam.id).score, 19);
      expect(
        store.academics.where((row) => row.studentId == second.id),
        isEmpty,
      );
      expect(find.text(second.name), findsOneWidget);
      expect(tester.widget<TextField>(score).controller!.text, isEmpty);
      expect(tester.widget<TextField>(score).focusNode!.hasFocus, isTrue);
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pump();
      expect(cancelled, 1);
      expect(callerFocus.hasFocus, isTrue);
      expectNoFinance();
      expect(tester.takeException(), isNull);
    },
  );

  for (final (dark, kind, name) in [
    (true, AcademicActivityKind.exam, 'academic-quick-exam-dark960-text200'),
    (
      false,
      AcademicActivityKind.homework,
      'academic-quick-homework-light960-text200',
    ),
  ]) {
    testWidgets('inline quick panel remains usable at 960px text200 in $name', (
      tester,
    ) async {
      await storeChange(
        tester,
        () => store.saveStudent(
          first.copyWith(
            name: List.filled(6, 'يوسف صاحب الاسم الطويل').join(' '),
          ),
        ),
      );
      first = store.students.first;
      selected.value = first;
      await open(
        tester,
        kind == AcademicActivityKind.exam ? exam : homework,
        dark: dark,
        width: 960,
        scale: 2,
      );
      final target = kind == AcademicActivityKind.exam
          ? save
          : find.byKey(const Key('academic-quick-homework-incomplete'));
      await tester.ensureVisible(target);
      await tester.pumpAndSettle();
      expect(tester.getRect(target).bottom, lessThanOrEqualTo(800));
      expect(tester.getRect(target).right, lessThanOrEqualTo(960));
      expect(tester.takeException(), isNull);
      await capture(tester, name);
      if (kind == AcademicActivityKind.exam) {
        await enterScore(tester, '۱۸');
        await saveMutation(
          tester,
          () => tester.tap(save),
          () => record(first.id, exam.id).score == 18,
        );
      } else {
        await saveMutation(
          tester,
          () => tester.tap(target),
          () =>
              record(first.id, homework.id).homework ==
              HomeworkStatus.incomplete,
        );
      }
      expect(saved, 1);
      expectNoFinance();
      expect(tester.takeException(), isNull);
    });
  }
}

Future<void> storeChange(
  WidgetTester tester,
  Future<void> Function() mutation,
) async {
  await tester.runAsync(mutation);
  await tester.pump();
}
