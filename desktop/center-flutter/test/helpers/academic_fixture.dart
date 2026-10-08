import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

/// Links the fixture's historical session without changing its date or roster.
/// Each caller explicitly chooses the actual attendees needed by its scenario.
Future<LessonSession> prepareAcademicFixture(
  CenterStore store,
  LessonSession session,
  Iterable<Student> attendees,
) async {
  var month = store.studyMonths.firstWhere(
    (m) => m.number == session.monthNumber,
  );
  if (!month.lessons.any((lesson) => lesson.number == session.number)) {
    month = await store.saveStudyMonth(
      month.copyWith(
        lessons: [
          ...month.lessons,
          PreparedLesson(number: session.number),
        ],
      ),
    );
  }
  final started = await store.startPreparedLesson(
    groupId: session.groupId,
    preparedLessonId: month.lessons
        .firstWhere((lesson) => lesson.number == session.number)
        .id,
  );
  for (final student in attendees) {
    await store.recordAttendance(
      EntryRequest(
        studentId: student.id,
        sessionId: started.id,
        mode: EntryMode.single,
      ),
    );
  }
  return started;
}

Future<void> selectAcademicFixtureSession(
  WidgetTester tester,
  CenterStore store,
  LessonSession session,
) async {
  final month = store.studyMonthForLesson(session.preparedLessonId!)!;
  final lesson = month.lessons.firstWhere(
    (entry) => entry.id == session.preparedLessonId,
  );
  for (final (label, option) in [
    ('الشهر المشترك', month.name),
    ('الحصة المجهزة لكل المجموعات', '${lesson.number} · ${lesson.name}'),
    ('المجموعة التي بدأت الحصة', store.groupLabel(session.groupId)),
  ]) {
    final field = find.widgetWithText(DropdownButtonFormField<String>, label);
    await tester.ensureVisible(field);
    await tester.tap(field);
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text(option).last);
    await tester.tap(find.text(option).last);
    await tester.pumpAndSettle();
  }
}

Future<void> cancelAcademicStudentChoice(WidgetTester tester) async {
  expect(find.byKey(const Key('student-lookup-choice')), findsOneWidget);
  await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
  await tester.pumpAndSettle();
  expect(find.byKey(const Key('student-lookup-choice')), findsNothing);
}
