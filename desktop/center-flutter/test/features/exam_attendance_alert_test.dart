import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/center_exam_monitor.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/exams/exam_attendance_alert.dart';
import 'package:massar_center/shared/theme.dart';

const _password = 'synthetic-monitor-password';

class _Fixture {
  _Fixture(this.directory, this.store);

  final Directory directory;
  CenterStore store;
  late StudyGroup group;
  late LessonSession session;
  late AcademicActivity exam;
  late List<Student> students;

  static Future<_Fixture> create() async {
    final directory = await Directory.systemTemp.createTemp('exam-alert-ui-');
    final fixture = _Fixture(
      directory,
      await CenterStore.open(directory: directory.path),
    );
    await fixture._seed();
    return fixture;
  }

  Future<void> _seed() async {
    await initializeDateFormatting('ar_EG');
    await store.setupAdmin('monitor-admin', _password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة المتابعة',
        centerId: store.catalogs
            .firstWhere((c) => c.kind == CatalogKind.center)
            .id,
        subjectId: store.catalogs
            .firstWhere((c) => c.kind == CatalogKind.subject)
            .id,
        gradeId: store.catalogs
            .firstWhere((c) => c.kind == CatalogKind.grade)
            .id,
        sessionPrice: 6000,
      ),
    );
    group = store.groups.single;
    for (var index = 0; index < 3; index++) {
      await store.saveStudent(
        Student(
          code: '0010$index',
          name: 'طالب متابعة $index',
          groupIds: [group.id],
          createdAt: DateTime.now(),
        ),
      );
    }
    students = store.students;
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'شهر المتابعة',
        lessons: [const PreparedLesson(number: 1)],
      ),
    );
    session = await store.startPreparedLesson(
      groupId: group.id,
      preparedLessonId: month.lessons.single.id,
    );
    exam = await store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: session.preparedLessonId,
        kind: AcademicActivityKind.exam,
        name: 'امتحان المتابعة',
        maxScore: 10,
        createdAt: DateTime.now(),
      ),
    );
  }

  Future<void> publish(List<ExamMonitorAttempt> attempts) =>
      store.publishIntegratedExamSnapshot(
        ExamMonitorSnapshot(
          sourceId: 'engine',
          examId: 'room',
          groupId: group.id,
          sessionId: session.id,
          activityId: exam.id,
          version: 1,
          roomState: 'running',
          attempts: attempts,
        ),
      );

  Future<void> attend(Student student) => store.recordAttendance(
    EntryRequest(
      studentId: student.id,
      sessionId: session.id,
      mode: EntryMode.single,
    ),
  );

  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('monitor-admin', _password);
  }

  Future<void> dispose() async {
    await store.close();
    await directory.delete(recursive: true);
  }
}

Future<void> _mount(WidgetTester tester, CenterStore store) async {
  await tester.pumpWidget(
    MaterialApp(
      theme: MassarTheme.light,
      home: Directionality(
        textDirection: TextDirection.rtl,
        child: Scaffold(
          body: Column(children: [ExamAttendanceAlert(store: store)]),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'reception shows official entrants and resolves an open alert after attendance',
    (tester) async {
      final fixture = (await tester.runAsync(_Fixture.create))!;
      try {
        await _mount(tester, fixture.store);
        expect(
          find.byKey(const Key('open-exam-attendance-alerts')),
          findsNothing,
        );
        await tester.runAsync(() async {
          await fixture.attend(fixture.students[1]);
          await fixture.publish([
            ExamMonitorAttempt(
              attemptId: 'pending',
              studentId: fixture.students[0].id,
              status: 'pending_grading',
            ),
            ExamMonitorAttempt(
              attemptId: 'attended',
              studentId: fixture.students[1].id,
              status: 'not_submitted',
            ),
            ExamMonitorAttempt(
              attemptId: 'cancelled',
              studentId: fixture.students[2].id,
              status: 'cancelled',
            ),
          ]);
        });
        await tester.pumpAndSettle();
        expect(find.text('دخول امتحان بدون حضور: 2'), findsOneWidget);
        await tester.tap(find.byKey(const Key('open-exam-attendance-alerts')));
        await tester.pumpAndSettle();
        expect(find.text(fixture.students[0].name), findsOneWidget);
        expect(find.text('الكود: 00100'), findsOneWidget);
        expect(find.text(fixture.students[1].name), findsNothing);
        expect(find.text(fixture.students[2].name), findsOneWidget);
        expect(find.text('المحاولة ملغاة'), findsOneWidget);
        expect(find.text('امتحان المتابعة'), findsOneWidget);
        expect(find.textContaining('مجموعة المتابعة'), findsOneWidget);
        expect(find.textContaining('حصة 1'), findsOneWidget);
        expect(find.text('سلّم الامتحان وينتظر التصحيح'), findsOneWidget);

        await tester.runAsync(() => fixture.attend(fixture.students[0]));
        await tester.pumpAndSettle();
        expect(find.text(fixture.students[0].name), findsNothing);
        expect(find.text('دخول امتحان بدون حضور: 1'), findsNWidgets(2));
        expect(fixture.store.payments, isEmpty);

        fixture.store.signOut();
        await tester.pumpAndSettle();
        expect(find.text('امتحان المتابعة'), findsNothing);
        expect(
          find.text('سجّل دخول الموظف لعرض متابعة الحضور.'),
          findsOneWidget,
        );
        expect(
          find.byKey(const Key('open-exam-attendance-alerts')),
          findsNothing,
        );
        expect(tester.takeException(), isNull);
      } finally {
        await tester.pumpWidget(const SizedBox.shrink());
        await tester.runAsync(fixture.dispose);
      }
    },
  );

  testWidgets(
    'stale empty room stays distinct from a fresh room in a narrow window',
    (tester) async {
      final fixture = (await tester.runAsync(_Fixture.create))!;
      await tester.binding.setSurfaceSize(const Size(420, 700));
      try {
        await tester.runAsync(() async {
          await fixture.publish([]);
          await fixture.reopen();
          final freshExam = await fixture.store.saveAcademicActivity(
            AcademicActivity(
              preparedLessonId: fixture.session.preparedLessonId,
              kind: AcademicActivityKind.exam,
              name: 'امتحان متصل',
              maxScore: 10,
              createdAt: DateTime.now(),
            ),
          );
          await fixture.store.publishIntegratedExamSnapshot(
            ExamMonitorSnapshot(
              sourceId: 'engine',
              examId: 'fresh-room',
              groupId: fixture.group.id,
              sessionId: fixture.session.id,
              activityId: freshExam.id,
              version: 1,
              roomState: 'running',
              attempts: [],
            ),
          );
        });
        await _mount(tester, fixture.store);
        expect(
          find.text('لا توجد حالات في آخر تحديث • تحديث متأخر: 1 امتحان'),
          findsOneWidget,
        );
        expect(find.text('لا يوجد دخول امتحان بدون حضور مسجّل'), findsNothing);
        await tester.tap(find.byKey(const Key('open-exam-attendance-alerts')));
        await tester.pumpAndSettle();
        expect(
          find.text('التحديث متأخر؛ قد توجد حالات لم تصل بعد.'),
          findsOneWidget,
        );
        expect(find.textContaining('آخر تحديث:'), findsNWidgets(2));
        expect(find.text('امتحان المتابعة'), findsOneWidget);
        expect(find.text('امتحان متصل'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.ensureVisible(find.text('إغلاق'));
        await tester.tap(find.text('إغلاق'));
        await tester.pumpAndSettle();
        expect(find.text('دخول الامتحان ومراجعة الحضور'), findsNothing);
      } finally {
        await tester.pumpWidget(const SizedBox.shrink());
        await tester.runAsync(fixture.dispose);
        await tester.binding.setSurfaceSize(null);
      }
    },
  );
}
