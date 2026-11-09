import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  testWidgets('preparation lists qualifying grades in the original group', (
    tester,
  ) async {
    final (home, visiting, store, directory) = (await tester.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      final directory = await Directory.systemTemp.createTemp('honor-roll-');
      final store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('مدير', 'honor-roll-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      String catalog(CatalogKind kind) =>
          store.catalogs.firstWhere((entry) => entry.kind == kind).id;
      for (final name in ['السبت', 'الثلاثاء']) {
        await store.saveGroup(
          StudyGroup(
            name: name,
            subjectId: catalog(CatalogKind.subject),
            centerId: catalog(CatalogKind.center),
            gradeId: catalog(CatalogKind.grade),
            sessionPrice: 6000,
          ),
        );
      }
      final [home, visiting] = store.groups;
      final at = DateTime.now().subtract(const Duration(days: 1));
      for (final (name, code, group) in [
        ('تعويض مرتبط', '00011', home),
        ('تعويض بالمجموعة', '00012', home),
        ('أقل من الحد', '00013', home),
        ('طالب الثلاثاء', '00014', visiting),
      ]) {
        await store.saveStudent(
          Student(name: name, code: code, groupIds: [group.id], createdAt: at),
        );
      }
      final [linked, paidVisit, below, local] = store.students;
      final lesson = store.studyMonths.first.lessons.first;
      final homeSession = await store.startPreparedLesson(
        groupId: home.id,
        preparedLessonId: lesson.id,
      );
      final exam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: lesson.id,
          name: 'امتحان الشهر',
          kind: AcademicActivityKind.exam,
          maxScore: 100,
          createdAt: at,
        ),
      );
      Future<void> grade(Student student, LessonSession session, num score) =>
          store.saveAcademic(
            AcademicRecord(
              studentId: student.id,
              sessionId: session.id,
              activityId: exam.id,
              score: score,
              maxScore: 100,
              updatedAt: at,
            ),
          );

      await store.renewPackage(
        PackageRequest(
          studentId: linked.id,
          groupId: home.id,
          sessionId: homeSession.id,
        ),
      );
      await store.recordAttendance(
        EntryRequest(
          studentId: below.id,
          sessionId: homeSession.id,
          mode: EntryMode.single,
        ),
      );
      await grade(below, homeSession, 54.5);
      await store.closeSession(homeSession.id);
      final absence = store.attendances.singleWhere(
        (a) => a.studentId == linked.id,
      );
      final visitingSession = await store.startPreparedLesson(
        groupId: visiting.id,
        preparedLessonId: lesson.id,
      );
      await store.recordAttendance(
        EntryRequest(
          studentId: linked.id,
          sessionId: visitingSession.id,
          mode: EntryMode.makeup,
          originalAttendanceId: absence.id,
        ),
      );
      await store.recordAttendance(
        EntryRequest(
          studentId: paidVisit.id,
          sessionId: visitingSession.id,
          mode: EntryMode.makeup,
          makeupSourceGroupId: home.id,
        ),
      );
      await store.recordAttendance(
        EntryRequest(
          studentId: local.id,
          sessionId: visitingSession.id,
          mode: EntryMode.single,
        ),
      );
      await grade(linked, visitingSession, 55);
      await grade(paidVisit, visitingSession, 75);
      await grade(local, visitingSession, 91);
      return (homeSession, visitingSession, store, directory);
    }))!;
    addTearDown(
      () => tester.runAsync(() async {
        await store.close();
        await directory.delete(recursive: true);
      }),
    );
    await tester.binding.setSurfaceSize(const Size(1280, 820));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    Future<void> openRoll(LessonSession session) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: AttendanceWorkspace(
              key: ValueKey(session.id),
              initialSessionId: session.id,
              store: store,
              onExit: () {},
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('session-exam-honor-roll')));
      await tester.pumpAndSettle();
    }

    await openRoll(home);
    expect(find.text('تعويض مرتبط'), findsOneWidget);
    expect(find.text('00011'), findsOneWidget);
    expect(find.text('تعويض بالمجموعة'), findsOneWidget);
    expect(find.text('أقل من الحد'), findsNothing);
    expect(find.text('طالب الثلاثاء'), findsNothing);

    await tester.enterText(
      find.widgetWithText(TextFormField, 'من درجة'),
      '٥٤٫٥',
    );
    await tester.pumpAndSettle();
    expect(find.text('أقل من الحد'), findsOneWidget);
    await tester.tap(find.byTooltip('العودة للتحضير'));
    await tester.pumpAndSettle();

    await openRoll(visiting);
    expect(find.text('طالب الثلاثاء'), findsOneWidget);
    expect(find.text('تعويض مرتبط'), findsNothing);
    expect(find.text('تعويض بالمجموعة'), findsNothing);
    expect(tester.takeException(), isNull);
  });
}
