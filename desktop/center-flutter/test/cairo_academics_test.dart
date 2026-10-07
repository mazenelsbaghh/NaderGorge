import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/features/management/academics_page.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup cairo, alex;
  late Student student;
  late LessonSession session;
  late AcademicActivity exam;

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp('massar-cairo-');
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('اختبار', 'test-password-2026');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveCatalog(
        const CatalogEntry(name: 'سنتر القاهرة', kind: CatalogKind.center),
      );
      for (final center in store.catalogs.where(
        (c) => c.kind == CatalogKind.center,
      )) {
        await store.saveGroup(
          StudyGroup(
            name: center.name,
            centerId: center.id,
            subjectId: store.catalogs
                .firstWhere((c) => c.kind == CatalogKind.subject)
                .id,
            gradeId: store.catalogs
                .firstWhere((c) => c.kind == CatalogKind.grade)
                .id,
            sessionPrice: 6000,
          ),
        );
      }
      cairo = store.groupsForRegion(cairo: true).single;
      alex = store.groupsForRegion().single;
      await store.saveStudent(
        Student(
          name: 'طالب القاهرة',
          code: '101',
          groupIds: [cairo.id],
          createdAt: DateTime.now(),
        ),
      );
      student = store.students.single;
      final month = await store.saveStudyMonth(
        StudyMonth(
          name: 'شهر الاختبار',
          lessons: [const PreparedLesson(number: 1)],
        ),
      );
      session = await store.startPreparedLesson(
        groupId: cairo.id,
        preparedLessonId: month.lessons.single.id,
      );
      exam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: month.lessons.single.id,
          name: 'امتحان الحصة الأولى',
          kind: AcademicActivityKind.exam,
          maxScore: 10,
          createdAt: DateTime.now(),
        ),
      );
    }),
  );
  tearDown(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  AcademicRecord grade(num score) => AcademicRecord(
    studentId: student.id,
    sessionId: session.id,
    activityId: exam.id,
    score: score,
    maxScore: 10,
    updatedAt: DateTime.now(),
  );

  Future<void> openAcademicPage(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(1440, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Scaffold(body: AcademicsPage(store: store, cairo: true)),
      ),
    );
    await tester.pumpAndSettle();
    await tester.tap(
      find.byKey(ValueKey('academic-month-${store.studyMonths.first.id}')),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.text('شهر الاختبار').last);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('academic-group-null')));
    await tester.pumpAndSettle();
    await tester.tap(find.text(store.groupLabel(cairo.id)).last);
    await tester.pumpAndSettle();
  }

  testWidgets(
    'Cairo roster refreshes grades, activities, membership and permissions while searching',
    (tester) async {
      await openAcademicPage(tester);
      final search = find.byKey(const Key('academic-code-search'));
      await tester.enterText(search, student.code);
      await tester.pumpAndSettle();
      expect(find.text(student.name), findsOneWidget);

      await tester.runAsync(() => store.saveAcademic(grade(0)));
      await tester.pumpAndSettle();
      expect(find.text('0 / 10'), findsOneWidget);
      expect(find.text('حاضر'), findsOneWidget);

      late AcademicActivity secondExam;
      await tester.runAsync(() async {
        secondExam = await store.saveAcademicActivity(
          AcademicActivity(
            preparedLessonId: exam.preparedLessonId,
            name: 'الامتحان الثاني',
            kind: AcademicActivityKind.exam,
            maxScore: 10,
            createdAt: DateTime.now(),
          ),
        );
        await store.saveAcademic(
          AcademicRecord(
            studentId: student.id,
            sessionId: session.id,
            activityId: secondExam.id,
            score: 8,
            maxScore: 10,
            updatedAt: DateTime.now(),
          ),
        );
      });
      await tester.pumpAndSettle();
      await tester.tap(
        find.byKey(
          ValueKey(
            'academic-activity-${exam.preparedLessonId}-${session.id}-${exam.id}',
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('امتحان: الامتحان الثاني').last);
      await tester.pumpAndSettle();
      expect(find.text('8 / 10'), findsOneWidget);
      expect(find.text('0 / 10'), findsNothing);

      await tester.runAsync(
        () => store.saveStudent(student.copyWith(name: 'طالب بعد التعديل')),
      );
      await tester.pumpAndSettle();
      expect(find.text('طالب بعد التعديل'), findsOneWidget);
      await tester.runAsync(
        () => store.saveStudent(student.copyWith(groupIds: [alex.id])),
      );
      await tester.pumpAndSettle();
      expect(find.text(student.name), findsNothing);
      expect(find.text('طالب بعد التعديل'), findsNothing);
      await tester.runAsync(() => store.saveStudent(student));
      await tester.pumpAndSettle();
      expect(find.text(student.name), findsOneWidget);
      expect(find.text('8 / 10'), findsOneWidget);

      store.signOut();
      await tester.pumpAndSettle();
      final record = find.widgetWithText(TextButton, 'رصد');
      expect(tester.widget<TextButton>(record).onPressed, isNull);
      await tester.runAsync(() => store.signIn('اختبار', 'test-password-2026'));
      await tester.pumpAndSettle();
      expect(tester.widget<TextButton>(record).onPressed, isNotNull);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets('Cairo roster follows a replacement store with the same IDs', (
    tester,
  ) async {
    await openAcademicPage(tester);
    late Directory replacementDirectory;
    late CenterStore replacement;
    await tester.runAsync(() async {
      replacementDirectory = await Directory.systemTemp.createTemp(
        'massar-cairo-swap-',
      );
      replacement = await CenterStore.open(
        directory: replacementDirectory.path,
      );
      await replacement.setupAdmin('بديل', 'test-password-2026');
      await replacement.restoreBackup(await store.createBackup());
      await replacement.signIn('اختبار', 'test-password-2026');
      await replacement.saveStudent(
        student.copyWith(name: 'طالب النسخة الجديدة'),
      );
    });
    addTearDown(
      () => tester.runAsync(() async {
        await replacement.close();
        await replacementDirectory.delete(recursive: true);
      }),
    );
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Scaffold(body: AcademicsPage(store: replacement, cairo: true)),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('طالب النسخة الجديدة'), findsOneWidget);
    expect(find.text(student.name), findsNothing);
    await tester.runAsync(() => replacement.saveAcademic(grade(4)));
    await tester.pumpAndSettle();
    expect(find.text('4 / 10'), findsOneWidget);
    await tester.runAsync(() => store.saveAcademic(grade(9)));
    await tester.pumpAndSettle();
    expect(find.text('4 / 10'), findsOneWidget);
    expect(find.text('9 / 10'), findsNothing);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  test(
    'Cairo score including zero creates one attendance atomically without money and survives reopen',
    () async {
      expect(store.studentsForRegion(), isEmpty);
      expect(store.studentsForRegion(cairo: true).single.id, student.id);
      expect(session.kind, SessionKind.free);
      expect(store.attendances, isEmpty);
      await store.saveAcademic(grade(0));
      expect(store.attendances.single.status, AttendanceStatus.present);
      await store.saveAcademic(grade(8));
      expect(store.attendances.length, 1);
      expect(store.academics.single.score, 8);
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      expect(store.attendances.single.studentId, student.id);
      expect(store.academics.single.score, 8);
      expect(store.groupsForRegion(cairo: true).single.id, cairo.id);
    },
  );

  for (final kind in [SessionKind.counted, SessionKind.extra]) {
    test(
      'legacy Cairo ${kind.name} lesson accepts exam attendance without payment or debt',
      () async {
        // Regression: imported Cairo lessons can retain a financial session kind.
        await store.saveSession(session.copyWith(kind: kind, extraPrice: 6000));
        session = await store.startPreparedLesson(
          groupId: cairo.id,
          preparedLessonId: session.preparedLessonId!,
        );
        expect(session.kind, kind);
        await expectLater(
          store.saveAcademic(grade(11)),
          throwsA(isA<CenterException>()),
        );
        expect(store.attendances, isEmpty);
        expect(store.academics, isEmpty);
        await store.saveAcademic(grade(0));
        expect(store.academics.single.score, 0);
        final attendanceId = store.attendances.single.id;
        await store.saveAcademic(grade(8));
        expect(store.attendances.single.id, attendanceId);
        expect(store.attendances.single.status, AttendanceStatus.present);
        expect(store.attendances.single.paymentPending, isFalse);
        expect(store.payments, isEmpty);
        expect(store.packages, isEmpty);
        expect(store.centerFees, isEmpty);
        expect(store.studentDebtFor(student.id), 0);
        expect(store.sessionFinancialSummary(session.id).totalCollected, 0);
        expect(
          store.paymentStatusFor(student.id, session.id).status,
          StudentPaymentStatus.free,
        );
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.signIn('اختبار', 'test-password-2026');
        expect(store.attendances.single.id, attendanceId);
        expect(store.academics.single.score, 8);
        expect(store.studentDebtFor(student.id), 0);
      },
    );
  }

  test('non-Cairo paid attendance still requires payment evidence', () async {
    final other = await store.startPreparedLesson(
      groupId: alex.id,
      preparedLessonId: session.preparedLessonId!,
    );
    final backup = await store.createBackup();
    final data = jsonDecode(await File(backup).readAsString()) as Map;
    final state = CenterState.fromJson(data['data'] as Map<String, dynamic>);
    state.attendances.add(
      AttendanceRecord(
        id: 'unpaid-presence',
        studentId: student.id,
        sessionId: other.id,
        status: AttendanceStatus.present,
        recordedAt: DateTime.now(),
      ),
    );
    expect(
      () => validateState(state),
      throwsA(
        isA<CenterException>().having(
          (e) => e.message,
          'payment evidence error',
          contains('حضور الحصة المدفوعة يجب أن يحتفظ بدفع فعال'),
        ),
      ),
    );
  });

  test(
    'invalid grade does not leave presence and Cairo cannot use normal reception',
    () async {
      await expectLater(
        store.saveAcademic(grade(11)),
        throwsA(isA<CenterException>()),
      );
      expect(store.attendances, isEmpty);
      expect(store.academics, isEmpty);
      await expectLater(
        store.recordAttendance(
          EntryRequest(
            mode: EntryMode.single,
            studentId: student.id,
            sessionId: session.id,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.attendances, isEmpty);
    },
  );

  test(
    'Cairo student cannot be graded for another group and absence is not presence',
    () async {
      final other = await store.startPreparedLesson(
        groupId: alex.id,
        preparedLessonId: session.preparedLessonId!,
      );
      await expectLater(
        store.saveAcademic(
          AcademicRecord(
            studentId: student.id,
            sessionId: other.id,
            activityId: exam.id,
            score: 8,
            maxScore: 10,
            updatedAt: DateTime.now(),
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: student.id,
          sessionId: session.id,
          activityId: exam.id,
          examAbsent: true,
          maxScore: 10,
          updatedAt: DateTime.now(),
        ),
      );
      expect(store.attendances, isEmpty);
      expect(store.academics.single.examAbsent, isTrue);
    },
  );

  test('legacy Cairo classification survives center rename', () async {
    final center = store.catalogs.firstWhere((c) => c.id == cairo.centerId);
    await store.saveCatalog(center.copyWith(name: 'اسم جديد'));
    expect(store.isCairoGroup(cairo.id), isTrue);
  });
  test('academic staff can open a Cairo lesson and record its exam', () async {
    await store.saveStaff(
      name: 'موظف الرصد',
      password: 'test-password-2026',
      role: StaffRole.assistant,
    );
    final month = await store.saveStudyMonth(
      StudyMonth(name: 'شهر جديد', lessons: [const PreparedLesson(number: 1)]),
    );
    await store.signIn('موظف الرصد', 'test-password-2026');
    expect(store.canCollect, isFalse);
    final next = await store.startPreparedLesson(
      groupId: cairo.id,
      preparedLessonId: month.lessons.single.id,
    );
    final activity = await store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: month.lessons.single.id,
        name: 'امتحان',
        kind: AcademicActivityKind.exam,
        maxScore: 10,
        createdAt: DateTime.now(),
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: student.id,
        sessionId: next.id,
        activityId: activity.id,
        score: 5,
        maxScore: 10,
        updatedAt: DateTime.now(),
      ),
    );
    expect(store.attendances.single.studentId, student.id);
    expect(store.payments, isEmpty);
  });
  testWidgets(
    'separate top Cairo entry lists ungraded students and Enter records score and presence',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1440, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: Scaffold(
            body: ManagementWorkspace(store: store, onOpenAttendance: () {}),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('open-cairo-attendance')));
      await tester.pumpAndSettle();
      expect(find.text('حضور مجموعات القاهرة'), findsWidgets);
      final firstMonth = store.studyMonths.first.id;
      await tester.tap(find.byKey(ValueKey('academic-month-$firstMonth')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('شهر الاختبار').last);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('academic-group-null')));
      await tester.pumpAndSettle();
      expect(find.text(store.groupLabel(alex.id)), findsNothing);
      await tester.tap(find.text(store.groupLabel(cairo.id)).last);
      await tester.pumpAndSettle();
      expect(find.text(student.name), findsOneWidget);
      expect(store.attendances, isEmpty);
      await tester.tap(find.text('رصد').last);
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('academic-quick-score')),
        '7',
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      for (var i = 0; i < 100 && store.academics.isEmpty; i++) {
        await tester.runAsync(
          () => Future<void>.delayed(const Duration(milliseconds: 20)),
        );
        await tester.pump(const Duration(milliseconds: 20));
      }
      await tester.pumpAndSettle();
      expect(store.academics.single.score, 7);
      expect(store.attendances.single.studentId, student.id);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
      var closed = false;
      final closing = store.close().then((_) => closed = true);
      for (var i = 0; i < 100 && !closed; i++) {
        await tester.runAsync(
          () => Future<void>.delayed(const Duration(milliseconds: 20)),
        );
        await tester.pump(const Duration(milliseconds: 20));
      }
      expect(closed, isTrue);
      await closing;
    },
  );
}
