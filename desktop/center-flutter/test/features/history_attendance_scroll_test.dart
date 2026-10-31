import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/features/attendance/student_history_panel.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/reception_ui_fixture.dart';

void main() {
  testWidgets('reception attendance scrolls through all lessons newest first', (
    tester,
  ) async {
    late Directory directory;
    late CenterStore store;
    await tester.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-history-scroll-',
      );
      store = await CenterStore.open(directory: directory.path);
      addTearDown(() async {
        await store.close();
        await directory.delete(recursive: true);
      });
      await seedReceptionUi(
        store,
        directory,
        studentCount: 1,
        sessionCount: 12,
      );
    });
    await tester.binding.setSurfaceSize(const Size(1600, 1200));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: Scaffold(
            body: StudentHistoryPanel(
              store: store,
              student: store.students.single,
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    final newest = find.text('شهر 1 · حصة 12');
    final oldest = find.text('شهر 1 · حصة 1');
    expect(newest.hitTestable(), findsOneWidget);
    expect(oldest.hitTestable(), findsNothing);
    for (var number = 1; number < 12; number++) {
      expect(
        tester.getTopLeft(find.text('شهر 1 · حصة ${number + 1}')).dy,
        lessThan(tester.getTopLeft(find.text('شهر 1 · حصة $number')).dy),
      );
    }
    final attendanceViewport = find.byKey(
      const ValueKey('student-attendance-student-0'),
    );
    final verticalScroll = find.descendant(
      of: attendanceViewport,
      matching: find.byWidgetPredicate(
        (widget) =>
            widget is SingleChildScrollView &&
            widget.scrollDirection == Axis.vertical,
      ),
    );
    final examsY = tester.getTopLeft(find.text('الامتحانات السابقة')).dy;
    await tester.drag(verticalScroll, const Offset(0, -800));
    await tester.pumpAndSettle();
    expect(oldest.hitTestable(), findsOneWidget);
    expect(newest.hitTestable(), findsNothing);
    expect(tester.getTopLeft(find.text('الامتحانات السابقة')).dy, examsY);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox.shrink());
  });
}
