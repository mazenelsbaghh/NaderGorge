import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

Future<Map<int, String>> seedAttendanceMonths(
  CenterStore store, {
  int fourPrice = 40000,
  int twoPrice = 18000,
  int threePrice = 27000,
}) async {
  final four = await store.saveStudyMonth(
    store.studyMonths.first.copyWith(name: 'الشهر الكامل', price: fourPrice),
  );
  final ids = <int, String>{4: four.id};
  for (final count in [2, 3]) {
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: count == 2 ? 'شهر حصتين' : 'شهر ثلاث حصص',
        number: count,
        price: count == 2 ? twoPrice : threePrice,
        lessons: List.generate(count, (i) => PreparedLesson(number: i + 1)),
      ),
    );
    ids[count] = month.id;
  }
  return ids;
}

/// Resolves the student through the public lookup mode without recording entry.
Future<void> previewAttendanceStudent(WidgetTester tester, String code) async {
  await tester.ensureVisible(find.text('بحث عن طالب'));
  await tester.tap(find.text('بحث عن طالب'));
  await tester.pumpAndSettle();
  await tester.enterText(find.byKey(const Key('student-search')), code);
  await tester.testTextInput.receiveAction(TextInputAction.done);
  await tester.pumpAndSettle();
  await tester.ensureVisible(find.text('التحضير').last);
  await tester.tap(find.text('التحضير').last);
  await tester.pumpAndSettle();
  await tester.ensureVisible(find.byKey(const Key('student-search')));
  await tester.tap(find.byKey(const Key('student-search')));
  await tester.pump();
}

Future<void> requestAttendanceConfirmation(
  WidgetTester tester,
  LogicalKeyboardKey key,
) async {
  await tester.sendKeyDownEvent(LogicalKeyboardKey.shiftLeft);
  await tester.sendKeyEvent(key);
  await tester.sendKeyUpEvent(LogicalKeyboardKey.shiftLeft);
}

Future<void> selectAttendanceMonth(
  WidgetTester tester,
  String label, {
  bool confirmation = false,
}) async {
  final selector = find.byWidgetPredicate(
    (widget) =>
        widget is DropdownButtonFormField<String> &&
        widget.key is ValueKey<String> &&
        (widget.key! as ValueKey<String>).value.startsWith(
          confirmation ? 'confirmation-month-' : 'month-plan-',
        ),
  );
  await tester.ensureVisible(selector);
  await tester.tap(selector);
  await tester.pumpAndSettle();
  await tester.tap(find.text(label).last);
  await tester.pumpAndSettle();
}
