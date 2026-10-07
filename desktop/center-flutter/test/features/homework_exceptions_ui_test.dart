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
  testWidgets(
    'homework code and Enter records missing and completes other attendees with code focus restored',
    (tester) async {
      late Directory directory;
      late CenterStore store;
      late AcademicActivity homework;
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
        await store.saveGroup(
          StudyGroup(
            name: 'الجمعة',
            subjectId: store.catalogs[0].id,
            centerId: store.catalogs[1].id,
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
            await store.recordAttendance(
              EntryRequest(
                studentId: store.students.last.id,
                sessionId: session.id,
                mode: EntryMode.single,
              ),
            );
          }
        }
      });
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
            child: Scaffold(body: AcademicsPage(store: store)),
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
          if (store.academics.length == 2 &&
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
      expect(store.academics, hasLength(2));
      expect(
        store.academics.every((r) => r.homework == HomeworkStatus.complete),
        isTrue,
      );
      expect(find.byKey(const Key('start-homework-exceptions')), findsNothing);
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
        store.academics.firstWhere((r) => r.studentId == first.id).homework,
        HomeworkStatus.missing,
      );
      expect(
        store.academics.firstWhere((r) => r.studentId == second.id).homework,
        HomeworkStatus.complete,
      );
      expect(store.academics.every((r) => r.activityId == homework.id), isTrue);
      expect(store.academics, hasLength(2));
      expect(find.byType(AcademicQuickEntry), findsNothing);
      expect(tester.widget<TextField>(search).controller!.text, isEmpty);
      expect(tester.widget<TextField>(search).focusNode!.hasFocus, isTrue);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );
}
