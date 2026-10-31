import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/academics_page.dart';
import 'package:massar_center/features/management/academic_quick_entry.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  for (final cairo in [false, true]) {
    testWidgets(
      '${cairo ? 'Cairo' : 'Alexandria'} homework code and Enter records missing and completes other attendees with code focus restored',
      (tester) async {
        late Directory directory;
        late CenterStore store;
        late AcademicActivity homework;
        AcademicActivity? exam;
        await tester.runAsync(() async {
          await initializeDateFormatting('ar_EG');
          directory = await Directory.systemTemp.createTemp(
            'massar-homework-ui-',
          );
          store = await CenterStore.open(directory: directory.path);
          await store.setupAdmin('مدير', 'test-password');
          for (final kind in CatalogKind.values) {
            await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
          }
          if (cairo) {
            await store.saveCatalog(
              const CatalogEntry(
                name: 'سنتر القاهرة',
                kind: CatalogKind.center,
              ),
            );
          }
          await store.saveGroup(
            StudyGroup(
              name: 'الجمعة',
              subjectId: store.catalogs[0].id,
              centerId: cairo ? store.catalogs.last.id : store.catalogs[1].id,
              gradeId: store.catalogs[2].id,
            ),
          );
          final month = await store.saveStudyMonth(
            StudyMonth(
              name: 'الشهر الأول',
              lessons: [const PreparedLesson(number: 1)],
            ),
          );
          final session = await store.startPreparedLesson(
            groupId: store.groups.single.id,
            preparedLessonId: month.lessons.single.id,
          );
          homework = await store.saveAcademicActivity(
            AcademicActivity(
              preparedLessonId: month.lessons.single.id,
              name: 'واجب التاريخ',
              kind: AcademicActivityKind.homework,
              createdAt: DateTime.now(),
            ),
          );
          if (cairo) {
            exam = await store.saveAcademicActivity(
              AcademicActivity(
                preparedLessonId: month.lessons.single.id,
                name: 'امتحان منفصل',
                kind: AcademicActivityKind.exam,
                createdAt: DateTime.now(),
              ),
            );
          }
          for (final code in ['10001', '10002', '10003']) {
            await store.saveStudent(
              Student(
                name: 'طالب $code',
                code: code,
                groupIds: [store.groups.single.id],
                createdAt: DateTime.now(),
              ),
            );
            if (code != '10003') {
              if (cairo) {
                await store.saveAcademic(
                  AcademicRecord(
                    studentId: store.students.last.id,
                    sessionId: session.id,
                    activityId: exam!.id,
                    score: 5,
                    updatedAt: DateTime.now(),
                  ),
                );
              } else {
                await store.recordAttendance(
                  EntryRequest(
                    studentId: store.students.last.id,
                    sessionId: session.id,
                    mode: EntryMode.single,
                  ),
                );
              }
            }
          }
        });
        List<AcademicRecord> homeworkRows() => store.academics
            .where((record) => record.activityId == homework.id)
            .toList();
        final originalAttendance = store.attendances
            .map((a) => a.toJson())
            .toList();
        final originalExams = store.academics
            .where((record) => record.activityId != homework.id)
            .map((record) => record.toJson())
            .toList();
        final attendanceReceipts = store.payments
            .map((payment) => payment.toJson())
            .toList();
        addTearDown(() async {
          await tester.binding.setSurfaceSize(null);
          await store.close();
          await directory.delete(recursive: true);
        });
        await tester.binding.setSurfaceSize(const Size(1440, 1000));
        await tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.light,
            home: Directionality(
              textDirection: TextDirection.rtl,
              child: Scaffold(
                body: AcademicsPage(store: store, cairo: cairo),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(
          find.byKey(ValueKey('academic-month-${store.studyMonths.first.id}')),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('الشهر الأول').last);
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const ValueKey('academic-group-null')));
        await tester.pumpAndSettle();
        await tester.tap(
          find.text(store.groupLabel(store.groups.single.id)).last,
        );
        await tester.pumpAndSettle();
        await tester.runAsync(() async {
          for (var i = 0; i < 100; i++) {
            await Future<void>.delayed(const Duration(milliseconds: 20));
            await tester.pump();
            if (homeworkRows().length == 2 &&
                tester
                    .widget<TextField>(
                      find.byKey(const Key('academic-code-search')),
                    )
                    .enabled!) {
              break;
            }
          }
        });
        await tester.pumpAndSettle();
        expect(homeworkRows(), hasLength(2));
        expect(
          homeworkRows().every((r) => r.homework == HomeworkStatus.complete),
          isTrue,
        );
        expect(
          find.byKey(const Key('start-homework-exceptions')),
          findsNothing,
        );
        expect(find.byKey(const Key('add-academic-homework')), findsOneWidget);
        expect(find.text('طالب 10003'), findsNothing);
        final search = find.byKey(const Key('academic-code-search'));
        await tester.enterText(search, '10001');
        await tester.runAsync(() async {
          await tester.testTextInput.receiveAction(TextInputAction.search);
          for (
            var i = 0;
            i < 100 &&
                tester.widget<TextField>(search).controller!.text.isNotEmpty;
            i++
          ) {
            await Future<void>.delayed(const Duration(milliseconds: 20));
          }
        });
        await tester.pumpAndSettle();
        final first = store.students.firstWhere((s) => s.code == '10001');
        final second = store.students.firstWhere((s) => s.code == '10002');
        expect(
          homeworkRows().firstWhere((r) => r.studentId == first.id).homework,
          HomeworkStatus.missing,
        );
        expect(
          homeworkRows().firstWhere((r) => r.studentId == second.id).homework,
          HomeworkStatus.complete,
        );
        expect(homeworkRows(), hasLength(2));
        expect(
          homeworkRows().every((r) => r.score == null && !r.examAbsent),
          isTrue,
        );
        expect(
          store.attendances.map((a) => a.toJson()).toList(),
          originalAttendance,
        );
        expect(
          store.academics
              .where((r) => r.activityId != homework.id)
              .map((r) => r.toJson())
              .toList(),
          originalExams,
        );
        expect(find.byType(AcademicQuickEntry), findsNothing);
        expect(tester.widget<TextField>(search).controller!.text, isEmpty);
        expect(tester.widget<TextField>(search).focusNode!.hasFocus, isTrue);
        expect(
          store.payments.map((payment) => payment.toJson()).toList(),
          attendanceReceipts,
        );
        expect(tester.takeException(), isNull);
      },
    );
  }
}
