import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/reports_page.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  testWidgets(
    'homework tab exports missing attendees with contacts across chosen groups and lesson',
    (tester) async {
      late Directory directory;
      late CenterStore store;
      late StudyMonth month;
      await tester.runAsync(() async {
        await initializeDateFormatting('ar_EG');
        directory = await Directory.systemTemp.createTemp(
          'massar-homework-report-',
        );
        store = await CenterStore.open(directory: directory.path);
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
      });
      addTearDown(() async {
        await tester.binding.setSurfaceSize(null);
        await store.close();
        await directory.delete(recursive: true);
      });
      final filter = CenterReportFilter(
        groupIds: store.groups.take(2).map((g) => g.id).toSet(),
        studyMonthId: month.id,
        preparedLessonId: month.lessons.first.id,
        homeworkStatus: HomeworkStatus.missing,
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
      await tester.runAsync(() async {
        final path = '${directory.path}/homework.csv';
        await CenterReports.exportCsv(
          store: store,
          kind: CenterReportKind.homework,
          filter: filter,
          destination: path,
        );
        final csv = utf8.decode(await File(path).readAsBytes());
        expect(csv, contains('رقم ولي الأمر'));
        expect(csv, contains('01200000000'));
        expect(csv, isNot(contains('طالب 2')));
        expect(
          csv.split('\r\n').where((line) => line.isNotEmpty),
          hasLength(3),
        );
      });
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
      expect(tester.takeException(), isNull);
    },
  );
}
