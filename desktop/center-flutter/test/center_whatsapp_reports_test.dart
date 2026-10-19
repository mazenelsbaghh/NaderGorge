import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/application/center_whatsapp_reports.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/whatsapp_page.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';

import 'helpers/academic_fixture.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late LessonSession session;
  late AcademicActivity exam, homework;
  late Student first, second, unrecorded;
  final classDate = DateTime(2026, 9, 1, 18);

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-whatsapp-page-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('الإدارة', 'workspace-test-password');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة الأحد',
        subjectId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.subject)
            .id,
        centerId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.center)
            .id,
        gradeId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.grade)
            .id,
        sessionPrice: 6000,
        packagePrice: 21000,
      ),
    );
    group = store.groups.single;
    for (final (code, name, joined) in [
      ('00101', 'أحمد', classDate.subtract(const Duration(days: 2))),
      ('00102', 'مينا', classDate.subtract(const Duration(days: 2))),
      ('00103', 'لم يسجل حضوره', classDate.subtract(const Duration(days: 2))),
      ('00104', 'التحق بعد الحصة', classDate.add(const Duration(days: 2))),
    ]) {
      await store.saveStudent(
        Student(
          code: code,
          name: name,
          phone: '01012345678',
          guardianPhone: '01234567890',
          groupIds: [group.id],
          createdAt: joined,
        ),
      );
    }
    first = store.students[0];
    second = store.students[1];
    unrecorded = store.students[2];
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: classDate,
        createdAt: classDate,
      ),
    );
    session = await prepareAcademicFixture(store, store.sessions.single, [
      first,
      second,
    ]);
    exam = await store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: session.preparedLessonId,
        kind: AcademicActivityKind.exam,
        name: 'امتحان الحركة',
        maxScore: 10,
        allowedMaxScores: [10, 12],
        createdAt: classDate,
      ),
    );
    homework = await store.saveAcademicActivity(
      AcademicActivity(
        preparedLessonId: session.preparedLessonId,
        kind: AcademicActivityKind.homework,
        name: 'واجب الحركة',
        createdAt: classDate,
      ),
    );
    for (final (student, score, maximum) in [
      (first, 0, 10),
      (second, 11, 12),
    ]) {
      await store.saveAcademic(
        AcademicRecord(
          studentId: student.id,
          sessionId: session.id,
          activityId: exam.id,
          score: score,
          maxScore: maximum,
          updatedAt: classDate,
        ),
      );
    }
    await store.saveAcademic(
      AcademicRecord(
        studentId: first.id,
        sessionId: session.id,
        activityId: homework.id,
        homework: HomeworkStatus.complete,
        updatedAt: classDate,
      ),
    );
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  CenterReportData report({String? examId, String? homeworkId}) =>
      CenterWhatsAppReports.build(
        store,
        CenterWhatsAppSelection(
          groupId: group.id,
          sessionId: session.id,
          examId: examId ?? exam.id,
          homeworkId: homeworkId ?? homework.id,
        ),
      );

  test(
    'class followup defaults attendees homework to complete while preserving zero and unknown attendance',
    () {
      final snapshot = report();
      final messages = {
        for (final row in snapshot.whatsAppRecords) row['studentId']: row,
      };
      expect(
        messages.keys,
        unorderedEquals([first.id, second.id, unrecorded.id]),
      );
      expect(messages[first.id]!['score'], 0);
      expect(messages[first.id]!['maximum'], 10);
      expect(messages[second.id]!['maximum'], 12);
      expect(
        messages[first.id]!['homework'],
        homeworkLabel(HomeworkStatus.complete),
      );
      expect(
        messages[second.id]!['homework'],
        homeworkLabel(HomeworkStatus.complete),
      );
      expect(messages[unrecorded.id]!['attendance'], 'لم يُسجل حضور');
      expect(messages[unrecorded.id]!['homework'], 'لم يُراجع');
      expect(messages[unrecorded.id]!['score'], isNull);
      expect(messages[unrecorded.id]!['maximum'], isNull);
      expect(store.payments, isEmpty);
    },
  );

  test(
    'named and historical homework keep their saved states in followup messages',
    () async {
      for (final (student, status) in [
        (first, HomeworkStatus.missing),
        (second, HomeworkStatus.exempt),
      ]) {
        await store.saveAcademic(
          AcademicRecord(
            studentId: student.id,
            sessionId: session.id,
            homework: status,
            updatedAt: classDate,
          ),
        );
      }
      Map<String, dynamic> studentMessage(
        CenterReportData report,
        Student student,
      ) => report.whatsAppRecords.singleWhere(
        (row) => row['studentId'] == student.id,
      );

      expect(studentMessage(report(), first)['homework'], 'كامل');
      expect(studentMessage(report(), second)['homework'], 'كامل');
      final historical = CenterWhatsAppReports.build(
        store,
        CenterWhatsAppSelection(
          groupId: group.id,
          sessionId: session.id,
          homeworkId: CenterWhatsAppSelection.legacyHomeworkId,
        ),
      );
      expect(studentMessage(historical, first)['homework'], 'لم يعمل');
      expect(studentMessage(historical, second)['homework'], 'معفى');
      expect(studentMessage(historical, unrecorded)['homework'], 'لم يُراجع');
      final attendance = CenterReports.build(
        store,
        CenterReportKind.attendance,
        CenterReportFilter(sessionId: session.id),
      );
      expect(studentMessage(attendance, first)['homework'], 'كامل');
      expect(studentMessage(attendance, first)['score'], 0);
      expect(studentMessage(attendance, second)['maximum'], 12);

      final otherHomework = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          kind: AcademicActivityKind.homework,
          name: 'واجب آخر',
          createdAt: classDate,
        ),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: first.id,
          sessionId: session.id,
          activityId: otherHomework.id,
          homework: HomeworkStatus.missing,
          updatedAt: classDate,
        ),
      );
      expect(studentMessage(report(), first)['homework'], 'كامل');
      expect(
        studentMessage(report(homeworkId: otherHomework.id), first)['homework'],
        'لم يعمل',
      );
      final ambiguous = CenterReports.build(
        store,
        CenterReportKind.attendance,
        CenterReportFilter(sessionId: session.id),
      );
      expect(studentMessage(ambiguous, first)['homework'], 'لم يُراجع');
    },
  );

  test(
    'followup defaults never overwrite saved homework exceptions or require opening the homework page',
    () async {
      for (final status in HomeworkStatus.values) {
        await store.saveAcademic(
          AcademicRecord(
            studentId: second.id,
            sessionId: session.id,
            activityId: homework.id,
            homework: status,
            updatedAt: classDate,
          ),
        );
        final before = store
            .academicRecordsForSession(session.id)
            .map((record) => record.toJson())
            .toList();
        final snapshot = report();
        final expected = status == HomeworkStatus.notReviewed
            ? HomeworkStatus.complete
            : status;
        expect(
          snapshot.whatsAppRecords.singleWhere(
            (row) => row['studentId'] == second.id,
          )['homework'],
          homeworkLabel(expected),
        );
        expect(
          store
              .academicRecordsForSession(session.id)
              .map((record) => record.toJson())
              .toList(),
          before,
        );
      }
      final noSelection = CenterWhatsAppReports.build(
        store,
        CenterWhatsAppSelection(groupId: group.id, sessionId: session.id),
      );
      expect(
        noSelection.whatsAppRecords.singleWhere(
          (row) => row['studentId'] == first.id,
        )['homework'],
        'كامل',
      );
    },
  );

  test(
    'only the store closing can turn unrecorded presence into recorded absence',
    () async {
      expect(
        report().whatsAppRecords.firstWhere(
          (row) => row['studentId'] == unrecorded.id,
        )['attendance'],
        'لم يُسجل حضور',
      );
      await store.closeSession(session.id);
      expect(
        report().whatsAppRecords.firstWhere(
          (row) => row['studentId'] == unrecorded.id,
        )['attendance'],
        attendanceLabel(AttendanceStatus.absent),
      );
      expect(
        report().whatsAppRecords.firstWhere(
          (row) => row['studentId'] == unrecorded.id,
        )['homework'],
        'لم يُراجع',
      );
      expect(report().whatsAppRecords, hasLength(3));
    },
  );

  test(
    'another named assessment cannot leak into the selected followup and stale class scopes reject',
    () async {
      final otherExam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          kind: AcademicActivityKind.exam,
          name: 'امتحان آخر',
          maxScore: 20,
          createdAt: classDate,
        ),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: first.id,
          sessionId: session.id,
          activityId: otherExam.id,
          score: 18,
          maxScore: 20,
          updatedAt: classDate,
        ),
      );
      expect(
        report().whatsAppRecords.firstWhere(
          (row) => row['studentId'] == first.id,
        )['score'],
        0,
      );
      expect(
        () => CenterWhatsAppReports.build(
          store,
          CenterWhatsAppSelection(
            groupId: 'removed-group',
            sessionId: session.id,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(
        () => report(examId: homework.id),
        throwsA(isA<CenterException>()),
      );
      store.signOut();
      expect(() => report(), throwsA(isA<CenterException>()));
    },
  );

  testWidgets(
    'dedicated page previews only the chosen class and selected recipients before any send',
    (tester) async {
      tester.view.physicalSize = const Size(1280, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.runAsync(() async {
        await store.saveAcademic(
          AcademicRecord(
            studentId: first.id,
            sessionId: session.id,
            homework: HomeworkStatus.missing,
            updatedAt: classDate,
          ),
        );
        await (FontLoader(
          'Tajawal',
        )..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))).load();
      });
      CenterReportData? preview;
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: RepaintBoundary(
            key: const Key('whatsapp-preview-screenshot'),
            child: Directionality(
              textDirection: TextDirection.rtl,
              child: Scaffold(
                body: WhatsAppPage(
                  store: store,
                  onWhatsApp: (report) async {
                    preview = report;
                  },
                ),
              ),
            ),
          ),
        ),
      );
      Future<void> choose(String label, String text) async {
        final field = find.widgetWithText(
          DropdownButtonFormField<String>,
          label,
        );
        await tester.ensureVisible(field);
        await tester.tap(field);
        await tester.pumpAndSettle();
        await tester.tap(find.text(text).last);
        await tester.pumpAndSettle();
      }

      expect(preview, isNull);
      await choose('المجموعة', store.groupLabel(group.id));
      await choose(
        'الحصة',
        '${sessionLabel(session)} · ${sessionDateLabel(session)}',
      );
      expect(find.text('0 من 10'), findsOneWidget);
      expect(find.text('11 من 12'), findsOneWidget);
      final previewButton = find.byKey(const Key('whatsapp-open-preview'));
      expect(tester.widget<FilledButton>(previewButton).onPressed, isNull);
      await choose('الواجب', 'رصد سابق بدون اسم');
      await tester.ensureVisible(previewButton);
      await tester.tap(previewButton);
      await tester.pumpAndSettle();
      expect(
        preview!.whatsAppRecords.singleWhere(
          (row) => row['studentId'] == first.id,
        )['homework'],
        'لم يعمل',
      );
      await choose('الواجب', homework.name);
      await tester.ensureVisible(previewButton);
      await tester.tap(previewButton);
      await tester.pumpAndSettle();
      expect(
        preview!.whatsAppRecords.singleWhere(
          (row) => row['studentId'] == first.id,
        )['homework'],
        'كامل',
      );
      final screenshotPath = Platform.environment['MASSAR_WHATSAPP_SCREENSHOT'];
      if (screenshotPath != null) {
        await tester.runAsync(() async {
          final boundary = tester.renderObject<RenderRepaintBoundary>(
            find.byKey(const Key('whatsapp-preview-screenshot')),
          );
          final image = await boundary.toImage(pixelRatio: 1);
          final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
          final screenshot = File(screenshotPath);
          await screenshot.parent.create(recursive: true);
          await screenshot.writeAsBytes(bytes!.buffer.asUint8List());
          image.dispose();
        });
      }

      await tester.tap(find.byType(Checkbox).first);
      await tester.pump();
      await tester.ensureVisible(
        find.byKey(const Key('whatsapp-open-preview')),
      );
      await tester.tap(find.byKey(const Key('whatsapp-open-preview')));
      await tester.pumpAndSettle();
      expect(preview!.whatsAppRecords, hasLength(2));
      expect(preview!.whatsAppMessageKind, 'followup');
      expect(tester.takeException(), isNull);
      await choose('نوع الرسالة', 'درجة الامتحان');
      await tester.ensureVisible(
        find.byKey(const Key('whatsapp-open-preview')),
      );
      await tester.tap(find.byKey(const Key('whatsapp-open-preview')));
      await tester.pumpAndSettle();
      expect(preview!.whatsAppMessageKind, 'grade');
      expect(preview!.whatsAppRecords, hasLength(2));
      expect(
        preview!.whatsAppRecords.every((row) => row['score'] != null),
        isTrue,
      );
      tester.view.physicalSize = const Size(760, 900);
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await choose('السنتر', 'center');
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('whatsapp-open-preview')),
            )
            .onPressed,
        isNull,
      );
      expect(
        find.text('اختر المجموعة والحصة لعرض الطلاب ورسائلهم.'),
        findsOneWidget,
      );
    },
  );
}
