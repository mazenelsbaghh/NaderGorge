import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:timezone/data/latest.dart' as tz;
import 'package:massar_parent/data/models.dart';
import 'package:massar_parent/ui/academic_screens.dart';
import 'package:massar_parent/ui/formatters.dart';

void main() {
  setUpAll(() async {
    tz.initializeTimeZones();
    await initializeDateFormatting('ar');
  });

  test(
    'watch duration keeps seconds and Cairo dates respect source offsets',
    () {
      expect(duration(45), '45 ثانية');
      expect(duration(1925), '32 دقيقة و5 ثانية');
      expect(duration(3661), '1 ساعة و1 دقيقة و1 ثانية');
      expect(displayDate('2026-09-23T17:30:00Z'), '23 سبتمبر 2026، 8:30 م');
      expect(
        displayDate('2026-09-23T20:30:00+03:00'),
        displayDate('2026-09-23T17:30:00Z'),
      );
      expect(displayDate('2026-01-23T17:30:00'), '23 يناير 2026، 7:30 م');
      expect(displayDate(''), '—');
    },
  );

  testWidgets(
    'homework card exposes numeric grade and percentage before opening',
    (tester) async {
      const graded = AcademicRow({
        'title': 'واجب الفيزياء',
        'submissionState': 'Graded',
        'isSubmitted': true,
        'grade': 'جيد جدًا',
        'score': 8,
        'totalScore': 10,
        'percentage': 80,
      });
      await tester.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: Directionality(
              textDirection: TextDirection.rtl,
              child: AssessmentTile(row: graded, homework: true),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('8 من 10'), findsOneWidget);
      expect(find.text('80%'), findsOneWidget);
      expect(find.text('جيد جدًا'), findsNothing);
      expect(tester.takeException(), isNull);
      expect(
        assessmentGrade(
          const AcademicRow({
            'submissionState': 'PendingReview',
            'score': 0,
            'totalScore': 10,
          }),
          true,
        ),
        '—',
      );
    },
  );

  testWidgets('lesson uses elapsed time instead of completion time', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ListView(
            children: lessonDetailChildren(
              const AcademicRow({
                'lessonTitle': 'قوانين نيوتن',
                'totalVideos': 4,
                'watchedSeconds': 600,
                'actualWatchedSeconds': 45,
                'lastWatchedAt': '2026-09-23T17:30:00Z',
              }),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('45 ثانية'), findsOneWidget);
    expect(find.text('10 دقيقة'), findsNothing);
    expect(find.text('23 سبتمبر 2026، 8:30 م'), findsOneWidget);
  });
}
