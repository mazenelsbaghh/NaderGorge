import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/student_history_panel.dart';
import 'package:massar_center/features/management/reports_page.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late LessonSession session;
  late Student first, second, lateStudent;
  late AcademicActivity examA, examB, homeworkA, homeworkB;
  final classDate = DateTime(2026, 9, 1, 10);

  setUp(() async {
    await initializeDateFormatting('ar_EG');
    directory = await Directory.systemTemp.createTemp('massar-named-reports-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('الإدارة', 'named-report-password');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'الأحد',
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
    group = store.groups.single;
    for (final (code, name, date) in [
      ('101', 'أحمد', classDate.subtract(const Duration(days: 3))),
      ('202', 'مينا', classDate.subtract(const Duration(days: 3))),
      ('303', 'طالب انضم لاحقًا', classDate.add(const Duration(days: 1))),
    ]) {
      await store.saveStudent(
        Student(code: code, name: name, groupIds: [group.id], createdAt: date),
      );
    }
    first = store.students[0];
    second = store.students[1];
    lateStudent = store.students[2];
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: classDate,
        createdAt: classDate,
      ),
    );
    final month = await store.saveStudyMonth(
      store.studyMonths.first.copyWith(
        name: 'شهر التقارير',
        lessons: [for (var n = 1; n <= 3; n++) PreparedLesson(number: n)],
      ),
    );
    session = await store.startPreparedLesson(
      groupId: group.id,
      preparedLessonId: month.lessons.first.id,
    );
    for (final student in [first, second]) {
      await store.recordAttendance(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
    }
    Future<AcademicActivity> activity(
      String name,
      AcademicActivityKind kind,
      int max,
    ) => store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: session.preparedLessonId,
        kind: kind,
        name: name,
        maxScore: max,
        createdAt: classDate,
      ),
    );
    examA = await activity('امتحان الحركة', AcademicActivityKind.exam, 20);
    examB = await activity('امتحان القوى', AcademicActivityKind.exam, 40);
    homeworkA = await activity(
      'واجب الحركة',
      AcademicActivityKind.homework,
      10,
    );
    homeworkB = await activity('واجب القوى', AcademicActivityKind.homework, 10);
    await store.saveAcademic(
      AcademicRecord(
        studentId: first.id,
        sessionId: session.id,
        activityId: examA.id,
        score: 0,
        maxScore: 20,
        updatedAt: classDate,
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: second.id,
        sessionId: session.id,
        activityId: examA.id,
        examAbsent: true,
        maxScore: 20,
        updatedAt: classDate,
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: first.id,
        sessionId: session.id,
        activityId: examB.id,
        score: 30,
        maxScore: 40,
        updatedAt: classDate,
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: first.id,
        sessionId: session.id,
        activityId: homeworkA.id,
        homework: HomeworkStatus.complete,
        updatedAt: classDate,
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: first.id,
        sessionId: session.id,
        activityId: homeworkB.id,
        homework: HomeworkStatus.missing,
        updatedAt: classDate,
      ),
    );
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });
  CenterReportData report(
    CenterReportKind kind, [
    CenterReportFilter filter = const CenterReportFilter(),
  ]) => CenterReports.build(store, kind, filter);

  test(
    'named exams keep zero, absence, maximum and unrecorded results independent',
    () {
      final all = report(CenterReportKind.exams);
      expect(all.rows, hasLength(4));
      expect(all.columns.last, 'اسم الامتحان');
      expect(
        all.rows.where((row) => row.last == examA.name).map((row) => row[6]),
        [0, null],
      );
      expect(
        all.rows.where((row) => row.last == examB.name).map((row) => row[6]),
        [30, null],
      );
      expect(all.rows.any((row) => row[0] == lateStudent.code), isFalse);
      final zero = report(
        CenterReportKind.exams,
        CenterReportFilter(activityId: examA.id, exactScore: 0),
      );
      expect(zero.rows.single[0], first.code);
      expect(zero.rows.single[7], 20);
      expect(zero.summary['درجات مرصودة'], '1');
      final missing = report(
        CenterReportKind.exams,
        CenterReportFilter(
          activityId: examB.id,
          examStatus: ExamReportStatus.notTaken,
        ),
      );
      expect(missing.rows.single[0], second.code);
      expect(missing.rows.single[5], 'لم تُرصد');
      expect(missing.rows.single[7], 40);
      final absent = report(
        CenterReportKind.exams,
        CenterReportFilter(
          activityId: examA.id,
          examStatus: ExamReportStatus.absent,
        ),
      );
      expect(absent.rows.single[5], 'غائب عن الامتحان');
      final range = report(
        CenterReportKind.exams,
        CenterReportFilter(activityId: examB.id, minScore: 30, maxScore: 30),
      );
      expect(range.rows.single[6], 30);
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(activityId: examB.id, exactScore: 0),
        ).rows,
        isEmpty,
      );
    },
  );

  test(
    'named homework does not copy results from another homework or create legacy empty rows',
    () {
      final all = report(CenterReportKind.homework);
      expect(all.rows, hasLength(4));
      expect(all.columns.last, 'اسم الواجب');
      expect(
        all.rows
            .where((row) => row.last == homeworkA.name)
            .map((row) => row[all.columns.indexOf('حالة الواجب')]),
        ['كامل', 'لم يُراجع'],
      );
      final done = report(
        CenterReportKind.homework,
        CenterReportFilter(
          activityId: homeworkA.id,
          homeworkStatus: HomeworkStatus.complete,
        ),
      );
      expect(done.rows.single[0], first.code);
      expect(
        report(
          CenterReportKind.homework,
          CenterReportFilter(
            activityId: homeworkB.id,
            homeworkStatus: HomeworkStatus.complete,
          ),
        ).rows,
        isEmpty,
      );
      final missing = report(
        CenterReportKind.homework,
        CenterReportFilter(
          activityId: homeworkB.id,
          homeworkStatus: HomeworkStatus.missing,
        ),
      );
      expect(missing.rows.single.last, homeworkB.name);
    },
  );

  test(
    'legacy records remain separate alongside names; historical academic membership survives group removal',
    () async {
      await store.saveAcademic(
        AcademicRecord(
          studentId: first.id,
          sessionId: session.id,
          score: 7,
          homework: HomeworkStatus.incomplete,
          updatedAt: classDate,
        ),
      );
      final exams = report(CenterReportKind.exams);
      expect(exams.rows, hasLength(5));
      expect(
        exams.rows.where((row) => row.last == 'رصد سابق بدون اسم').single[6],
        7,
      );
      expect(
        exams.rows
            .where((row) => row.last == examA.name && row[0] == first.code)
            .single[6],
        0,
      );
      await store.saveGroup(group.copyWith(id: '', name: 'مجموعة لاحقة'));
      await store.saveStudent(first.copyWith(groupIds: [store.groups.last.id]));
      final historical = report(
        CenterReportKind.exams,
        CenterReportFilter(activityId: examA.id),
      );
      expect(historical.rows.map((row) => row[0]), contains(first.code));
      await store.recordAttendance(
        EntryRequest(
          studentId: lateStudent.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: lateStudent.id,
          sessionId: session.id,
          activityId: examB.id,
          score: 10,
          maxScore: 40,
          updatedAt: classDate,
        ),
      );
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(activityId: examB.id),
        ).rows.map((row) => row[0]),
        contains(lateStudent.code),
      );
    },
  );

  test(
    'wrong activity kind or session fails clearly and CSV uses the identical named filter',
    () async {
      expect(
        () => report(
          CenterReportKind.exams,
          CenterReportFilter(activityId: homeworkA.id),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(
        () => report(
          CenterReportKind.students,
          CenterReportFilter(activityId: examA.id),
        ),
        throwsA(isA<CenterException>()),
      );
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: classDate.add(const Duration(days: 1)),
          createdAt: classDate,
        ),
      );
      expect(
        () => report(
          CenterReportKind.exams,
          CenterReportFilter(
            activityId: examA.id,
            sessionId: store.sessions.last.id,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      const invalid = CenterReportFilter(activityId: 'missing');
      expect(
        () => report(CenterReportKind.exams, invalid),
        throwsA(isA<CenterException>()),
      );
      final path = '${directory.path}/named.csv';
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.exams,
        filter: CenterReportFilter(activityId: examA.id, exactScore: 0),
        destination: path,
      );
      final csv = await File(path).readAsString();
      expect(csv, contains(examA.name));
      expect(csv, contains(first.name));
      expect(csv, isNot(contains(examB.name)));
      expect(csv, isNot(contains(second.name)));
      expect(csv, contains('اسم الامتحان'));
    },
  );

  test(
    'a named-only class never invents the opposite activity while legacy empty classes keep their roster',
    () async {
      Future<LessonSession> addClass(int number) async {
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: number,
            startsAt: classDate.add(Duration(days: number)),
            createdAt: classDate,
          ),
        );
        if (number <= 3) {
          return store.startPreparedLesson(
            groupId: group.id,
            preparedLessonId: store.studyMonths.first.lessons
                .firstWhere((lesson) => lesson.number == number)
                .id,
          );
        }
        return store.sessions.last;
      }

      final examOnly = await addClass(2);
      await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: examOnly.preparedLessonId,
          kind: AcademicActivityKind.exam,
          name: 'امتحان مستقل',
          createdAt: classDate,
        ),
      );
      for (final student in [first, second, lateStudent]) {
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: examOnly.id,
            mode: EntryMode.single,
          ),
        );
      }
      expect(
        report(
          CenterReportKind.homework,
          CenterReportFilter(sessionId: examOnly.id),
        ).rows,
        isEmpty,
      );
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(sessionId: examOnly.id),
        ).rows,
        hasLength(3),
      );
      final homeworkOnly = await addClass(3);
      await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: homeworkOnly.preparedLessonId,
          kind: AcademicActivityKind.homework,
          name: 'واجب مستقل',
          createdAt: classDate,
        ),
      );
      for (final student in [first, second, lateStudent]) {
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: homeworkOnly.id,
            mode: EntryMode.single,
          ),
        );
      }
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(sessionId: homeworkOnly.id),
        ).rows,
        isEmpty,
      );
      expect(
        report(
          CenterReportKind.homework,
          CenterReportFilter(sessionId: homeworkOnly.id),
        ).rows,
        hasLength(3),
      );
      final legacyEmpty = await addClass(4);
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(sessionId: legacyEmpty.id),
        ).rows,
        hasLength(3),
      );
      expect(
        report(
          CenterReportKind.homework,
          CenterReportFilter(sessionId: legacyEmpty.id),
        ).rows,
        hasLength(3),
      );
      await store.recordAttendance(
        EntryRequest(
          studentId: first.id,
          sessionId: examOnly.id,
          mode: EntryMode.single,
        ),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: first.id,
          sessionId: examOnly.id,
          homework: HomeworkStatus.complete,
          updatedAt: classDate,
        ),
      );
      final actualLegacy = report(
        CenterReportKind.homework,
        CenterReportFilter(sessionId: examOnly.id),
      );
      expect(actualLegacy.rows, hasLength(1));
      expect(actualLegacy.rows.single.last, 'رصد سابق بدون اسم');
      expect(
        actualLegacy.rows.single[actualLegacy.columns.indexOf('حالة الواجب')],
        'كامل',
      );
    },
  );

  testWidgets(
    'student history separates named exam and homework tables and preserves names and dates',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1280, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.dark,
          home: Scaffold(
            body: Directionality(
              textDirection: TextDirection.rtl,
              child: StudentHistoryPanel(
                store: store,
                student: first,
                expanded: true,
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      final tables = tester
          .widgetList<DataTable>(find.byType(DataTable))
          .toList();
      final exams = tables.firstWhere(
        (table) => (table.columns.first.label as Text).data == 'الامتحان',
      );
      final homework = tables.firstWhere(
        (table) => (table.columns.first.label as Text).data == 'الواجب والحصة',
      );
      List<String> labels(DataTable table) => table.rows
          .map((row) => (row.cells.first.child as Tooltip).message!)
          .toList();
      expect(labels(exams), hasLength(2));
      expect(labels(homework), hasLength(2));
      expect(labels(exams).join(' '), contains(examA.name));
      expect(labels(exams).join(' '), contains(examB.name));
      expect(labels(exams).join(' '), isNot(contains(homeworkA.name)));
      expect(labels(homework).join(' '), contains(homeworkA.name));
      expect(labels(homework).join(' '), isNot(contains(examB.name)));
      expect(labels(exams).first, contains('حصة 1'));
      final latestExam = find.byWidgetPredicate(
        (widget) =>
            widget is Tooltip && widget.message == '${examB.name}: 30 / 40',
      );
      final latestHomework = find.byWidgetPredicate(
        (widget) =>
            widget is Tooltip && widget.message == '${homeworkB.name}: لم يعمل',
      );
      expect(latestExam, findsOneWidget);
      expect(latestHomework, findsOneWidget);
      expect(
        find.descendant(of: latestExam, matching: find.text('30 / 40')),
        findsOneWidget,
      );
      expect(
        find.descendant(of: latestHomework, matching: find.text('لم يعمل')),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'report activity picker filters actual rows and clears when kind or session changes',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1280, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: Scaffold(
            body: Directionality(
              textDirection: TextDirection.rtl,
              child: Padding(
                padding: const EdgeInsets.all(28),
                child: Row(
                  children: [
                    Expanded(child: ReportsPage(store: store)),
                    const SizedBox(width: 220),
                  ],
                ),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      Future<void> choose(Finder field, Finder option) async {
        await tester.ensureVisible(field);
        await tester.tap(field);
        await tester.pumpAndSettle();
        await tester.ensureVisible(option.last);
        await tester.tap(option.last);
        await tester.pumpAndSettle();
      }

      Finder activityField() => find.byWidgetPredicate(
        (widget) =>
            widget is DropdownButtonFormField<String> &&
            widget.key is ValueKey &&
            (widget.key as ValueKey).value.toString().startsWith(
              'report-activity-',
            ),
      );
      await choose(
        find.byKey(const Key('report-kind')),
        find.text('الامتحانات'),
      );
      await choose(activityField(), find.textContaining(examA.name));
      expect(find.text(examB.name), findsNothing);
      expect(find.text(examA.name), findsNWidgets(2));
      await choose(find.byKey(const Key('report-kind')), find.text('الواجبات'));
      expect(find.text(examA.name), findsNothing);
      // Homework opens on missing submissions; explicitly choose all statuses
      // before exercising independent activity/session filtering.
      await choose(
        find.byWidgetPredicate(
          (widget) => widget is DropdownButtonFormField<HomeworkStatus>,
        ),
        find.text('كل الحالات'),
      );
      expect(find.text(homeworkA.name), findsNWidgets(2));
      expect(find.text(homeworkB.name), findsNWidgets(2));
      await choose(activityField(), find.textContaining(homeworkB.name));
      expect(find.text(homeworkA.name), findsNothing);
      Finder homeworkScope(String prefix) => find.byWidgetPredicate(
        (widget) =>
            widget is DropdownButtonFormField<String> &&
            widget.key is ValueKey &&
            (widget.key as ValueKey).value.toString().startsWith(prefix),
      );
      await choose(
        homeworkScope('homework-report-month-'),
        find.text('شهر التقارير'),
      );
      await choose(
        homeworkScope('homework-report-lesson-'),
        find.textContaining('حصة 1 ·'),
      );
      expect(find.text(homeworkA.name), findsNWidgets(2));
      expect(find.text(homeworkB.name), findsNWidgets(2));
      expect(tester.takeException(), isNull);
    },
  );
}
