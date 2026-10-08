import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/reports_page.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  testWidgets(
    'homework download button saves only missing students with contacts for selected groups month lesson and activity',
    (tester) async {
      late Directory directory;
      late CenterStore store;
      CenterStore? openedStore;
      late StudyMonth month;
      late StudyMonth otherMonth;
      const chooser = MethodChannel('plugins.flutter.io/file_selector');
      late String destination;
      var chooserCalls = 0;
      await tester.runAsync(() async {
        await initializeDateFormatting('ar_EG');
        directory = await Directory.systemTemp.createTemp(
          'massar-homework-report-',
        );
        addTearDown(() async {
          tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
            chooser,
            null,
          );
          await tester.binding.setSurfaceSize(null);
          await openedStore?.close();
          await directory.delete(recursive: true);
        });
        store = await CenterStore.open(directory: directory.path);
        openedStore = store;
        await store.setupAdmin('مدير', 'test-password');
        for (final kind in CatalogKind.values) {
          await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
        }
        month = await store.saveStudyMonth(
          StudyMonth(
            name: 'شهر التقرير',
            lessons: [
              const PreparedLesson(number: 1),
              const PreparedLesson(number: 2),
            ],
          ),
        );
        for (var index = 0; index < 3; index++) {
          await store.saveGroup(
            StudyGroup(
              name: 'مجموعة $index',
              subjectId: store.catalogs[0].id,
              centerId: store.catalogs[1].id,
              gradeId: store.catalogs[2].id,
            ),
          );
          final group = store.groups.last;
          await store.saveStudent(
            Student(
              name: 'طالب $index',
              code: '0000$index',
              phone: '0100000000$index',
              guardianPhone: '0120000000$index',
              groupIds: [group.id],
              createdAt: DateTime.now(),
            ),
          );
          final student = store.students.last;
          for (final lesson in month.lessons) {
            final session = await store.startPreparedLesson(
              groupId: group.id,
              preparedLessonId: lesson.id,
            );
            final activity =
                store.academicActivities
                    .where((a) => a.preparedLessonId == lesson.id)
                    .firstOrNull ??
                await store.saveAcademicActivity(
                  AcademicActivity(
                    preparedLessonId: lesson.id,
                    name: 'واجب ${lesson.number}',
                    kind: AcademicActivityKind.homework,
                    createdAt: DateTime.now(),
                  ),
                );
            await store.recordAttendance(
              EntryRequest(
                studentId: student.id,
                sessionId: session.id,
                mode: EntryMode.single,
              ),
            );
            await store.recordHomeworkExceptions(
              sessionId: session.id,
              activityId: activity.id,
              missingStudentId: student.id,
            );
          }
        }
        final session = store.sessions.firstWhere(
          (s) =>
              s.groupId == store.groups.first.id &&
              s.preparedLessonId == month.lessons.first.id,
        );
        final activity = store.academicActivities.firstWhere(
          (a) => a.preparedLessonId == month.lessons.first.id,
        );
        final otherActivity = await store.saveAcademicActivity(
          AcademicActivity(
            preparedLessonId: month.lessons.first.id,
            name: 'واجب آخر لنفس الحصة',
            kind: AcademicActivityKind.homework,
            createdAt: DateTime.now(),
          ),
        );
        for (final (code, name, status) in [
          ('00010', 'طالب عمل الواجب', HomeworkStatus.complete),
          ('00011', 'طالب لم يراجع واجبه', HomeworkStatus.notReviewed),
          ('00012', 'طالب معفى من الواجب', HomeworkStatus.exempt),
          ('00013', 'طالب غائب عن الحصة', null),
        ]) {
          await store.saveStudent(
            Student(
              code: code,
              name: name,
              groupIds: [store.groups.first.id],
              createdAt: DateTime.now(),
            ),
          );
          final student = store.students.last;
          if (status == null) continue;
          await store.recordAttendance(
            EntryRequest(
              studentId: student.id,
              sessionId: session.id,
              mode: EntryMode.single,
            ),
          );
          if (status != HomeworkStatus.notReviewed) {
            await store.saveAcademic(
              AcademicRecord(
                studentId: student.id,
                sessionId: session.id,
                activityId: activity.id,
                homework: status,
                updatedAt: DateTime.now(),
              ),
            );
          }
          if (status == HomeworkStatus.complete) {
            // Missing in another homework must not leak into the chosen one.
            await store.saveAcademic(
              AcademicRecord(
                studentId: student.id,
                sessionId: session.id,
                activityId: otherActivity.id,
                homework: HomeworkStatus.missing,
                updatedAt: DateTime.now(),
              ),
            );
          }
        }
        otherMonth = await store.saveStudyMonth(
          StudyMonth(
            name: 'شهر آخر',
            lessons: [const PreparedLesson(number: 1)],
          ),
        );
        final otherHomework = await store.saveAcademicActivity(
          AcademicActivity(
            preparedLessonId: otherMonth.lessons.single.id,
            name: 'واجب الشهر الآخر',
            kind: AcademicActivityKind.homework,
            createdAt: DateTime.now(),
          ),
        );
        for (var index = 0; index < 3; index++) {
          final session = await store.startPreparedLesson(
            groupId: store.groups[index].id,
            preparedLessonId: otherMonth.lessons.single.id,
          );
          final student = store.students.firstWhere(
            (s) => s.code == '0000$index',
          );
          await store.recordAttendance(
            EntryRequest(
              studentId: student.id,
              sessionId: session.id,
              mode: EntryMode.single,
            ),
          );
          await store.recordHomeworkExceptions(
            sessionId: session.id,
            activityId: otherHomework.id,
            missingStudentId: student.id,
          );
        }
      });
      destination = '${directory.path}/homework-selected.csv';
      tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(chooser, (
        call,
      ) async {
        if (call.method != 'getSavePath') {
          throw UnsupportedError('Unexpected chooser method: ${call.method}');
        }
        chooserCalls++;
        return destination;
      });
      final filter = CenterReportFilter(
        groupIds: store.groups.take(2).map((g) => g.id).toSet(),
        studyMonthId: month.id,
        preparedLessonId: month.lessons.first.id,
        homeworkStatus: HomeworkStatus.missing,
        activityId: store.academicActivities
            .firstWhere(
              (a) =>
                  a.preparedLessonId == month.lessons.first.id &&
                  a.name == 'واجب 1',
            )
            .id,
      );
      final report = CenterReports.build(
        store,
        CenterReportKind.homework,
        filter,
      );
      expect(report.rows, hasLength(2));
      for (var index = 0; index < 2; index++) {
        final row = report.rows.firstWhere((r) => r[0] == '0000$index');
        expect(row[report.columns.indexOf('رقم الطالب')], '0100000000$index');
        expect(
          row[report.columns.indexOf('رقم ولي الأمر')],
          '0120000000$index',
        );
        expect(row[report.columns.indexOf('حالة الواجب')], 'لم يعمل');
        expect(row[report.columns.indexOf('الحصة')], 1);
        expect(row[report.columns.indexOf('الشهر')], month.name);
      }
      await tester.binding.setSurfaceSize(const Size(1440, 1000));
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: Scaffold(body: ReportsPage(store: store)),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('homework-report-tab')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('homework-report-groups')), findsOneWidget);
      expect(find.text('تنزيل شيت الواجب · Excel / CSV'), findsOneWidget);
      expect(find.text('رقم ولي الأمر'), findsOneWidget);
      await tester.tap(find.byKey(const Key('homework-report-groups')));
      await tester.pumpAndSettle();
      expect(find.text('مجموعات تقرير الواجب'), findsOneWidget);
      for (final group in store.groups.take(2)) {
        await tester.tap(
          find.widgetWithText(CheckboxListTile, store.groupLabel(group.id)),
        );
      }
      await tester.tap(find.widgetWithText(FilledButton, 'تطبيق'));
      await tester.pumpAndSettle();
      await _choose(tester, 'homework-report-month-', find.text(month.name));
      await _choose(
        tester,
        'homework-report-lesson-',
        find.textContaining('حصة 1 ·'),
      );
      await _choose(
        tester,
        'report-activity-',
        find.textContaining('واجب 1 ·'),
      );
      expect(find.text('طالب 0'), findsOneWidget);
      expect(find.text('طالب 1'), findsOneWidget);
      expect(find.text('طالب 2'), findsNothing);
      final beforeExport = _sourceRecords(store);
      final csv = await _download(tester, destination);
      expect(chooserCalls, 1);
      final lines = csv.split('\r\n').where((line) => line.isNotEmpty).toList();
      expect(lines, hasLength(3));
      expect(lines.first, contains('"رقم الطالب","رقم ولي الأمر"'));
      for (var index = 0; index < 2; index++) {
        final line = lines.singleWhere(
          (line) => line.startsWith('"0000$index",'),
        );
        expect(
          line,
          contains('"طالب $index","0100000000$index","0120000000$index"'),
        );
        expect(line, contains('"${month.name}"'));
        expect(line, contains('"1"'));
        expect(line, contains('"لم يعمل"'));
        expect(line, endsWith('"واجب 1"'));
      }
      for (final excluded in [
        'طالب 2',
        'طالب عمل الواجب',
        'طالب لم يراجع واجبه',
        'طالب معفى من الواجب',
        'طالب غائب عن الحصة',
        'واجب آخر لنفس الحصة',
        'واجب الشهر الآخر',
        'واجب 2',
      ]) {
        expect(csv, isNot(contains(excluded)));
      }
      expect(_sourceRecords(store), beforeExport);

      // A second actual download proves that changed scope replaces the old
      // selection, rather than exporting a cached first report.
      await tester.tap(find.byKey(const Key('homework-report-groups')));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(CheckboxListTile, 'كل المجموعات'));
      await tester.tap(
        find.widgetWithText(
          CheckboxListTile,
          store.groupLabel(store.groups.last.id),
        ),
      );
      await tester.tap(find.widgetWithText(FilledButton, 'تطبيق'));
      await tester.pumpAndSettle();
      await _choose(
        tester,
        'homework-report-month-',
        find.text(otherMonth.name),
      );
      await _choose(
        tester,
        'homework-report-lesson-',
        find.textContaining('حصة 1 ·'),
      );
      await _choose(
        tester,
        'report-activity-',
        find.textContaining('واجب الشهر الآخر ·'),
      );
      destination = '${directory.path}/homework-other-month.csv';
      final changedCsv = await _download(tester, destination);
      expect(chooserCalls, 2);
      final changedLines = changedCsv
          .split('\r\n')
          .where((line) => line.isNotEmpty)
          .toList();
      expect(changedLines, hasLength(2));
      expect(
        changedLines.last,
        startsWith('"00002","طالب 2","01000000002","01200000002"'),
      );
      expect(changedLines.last, contains('"${otherMonth.name}"'));
      expect(changedLines.last, endsWith('"واجب الشهر الآخر"'));
      expect(changedCsv, isNot(contains('طالب 0')));
      expect(changedCsv, isNot(contains('طالب 1')));
      expect(_sourceRecords(store), beforeExport);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );
}

Future<void> _choose(
  WidgetTester tester,
  String keyPrefix,
  Finder option,
) async {
  final dropdown = find.byWidgetPredicate(
    (widget) =>
        widget is DropdownButtonFormField<String> &&
        widget.key is ValueKey<String> &&
        (widget.key! as ValueKey<String>).value.startsWith(keyPrefix),
  );
  expect(dropdown, findsOneWidget);
  await tester.ensureVisible(dropdown);
  await tester.tap(dropdown);
  await tester.pumpAndSettle();
  await tester.tap(option.last);
  await tester.pumpAndSettle();
}

Future<String> _download(WidgetTester tester, String destination) async {
  final button = find.widgetWithText(
    OutlinedButton,
    'تنزيل شيت الواجب · Excel / CSV',
  );
  final csv = await tester.runAsync(() async {
    await tester.tap(button);
    for (var attempt = 0; attempt < 100; attempt++) {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      await tester.pump();
      if (await File(destination).exists() && button.evaluate().isNotEmpty) {
        break;
      }
    }
    expect(await File(destination).exists(), isTrue);
    expect(button, findsOneWidget);
    expect(tester.widget<OutlinedButton>(button).onPressed, isNotNull);
    expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
    final bytes = await File(destination).readAsBytes();
    expect(bytes.take(3).toList(), [0xef, 0xbb, 0xbf]);
    return utf8.decode(bytes.skip(3).toList());
  });
  await tester.pumpAndSettle();
  expect(csv, isNotNull);
  return csv!;
}

String _sourceRecords(CenterStore store) => jsonEncode([
  store.students,
  store.academics,
  store.allAttendances,
  store.allPayments,
  store.allPackages,
  store.audit,
]);
