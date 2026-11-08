import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/application/center_whatsapp_reports.dart';
import 'package:massar_center/domain/lesson_attendance_counts.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup home, visiting;
  late Student student;
  late StudyMonth month;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('attendance-summary-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', 'attendance-summary-password');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    for (final name in ['المجموعة الأصلية', 'مجموعة التعويض']) {
      await store.saveGroup(
        StudyGroup(
          name: name,
          subjectId: store.catalogs
              .firstWhere((c) => c.kind == CatalogKind.subject)
              .id,
          centerId: store.catalogs
              .firstWhere((c) => c.kind == CatalogKind.center)
              .id,
          gradeId: store.catalogs
              .firstWhere((c) => c.kind == CatalogKind.grade)
              .id,
          sessionPrice: 6000,
        ),
      );
    }
    home = store.groups.first;
    visiting = store.groups.last;
    student = await store.registerStudent(
      Student(
        name: 'طالب الحضور',
        groupIds: [home.id],
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    month = store.studyMonths.first;
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<LessonSession> start(StudyGroup group, int number) =>
      store.startPreparedLesson(
        groupId: group.id,
        preparedLessonId: month.lessons
            .firstWhere((l) => l.number == number)
            .id,
      );

  CenterReportData report([
    CenterReportFilter filter = const CenterReportFilter(),
  ]) => CenterReports.build(store, CenterReportKind.attendanceSummary, filter);

  Map<String, Object?> row(CenterReportData report, StudyGroup group) =>
      Map.fromIterables(
        report.columns,
        report.rows.singleWhere(
          (r) => (r.first as String).startsWith(group.name),
        ),
      );

  test(
    'repeated imported presence and makeup count each student once within a lesson',
    () {
      final at = DateTime(2026, 10, 1);
      AttendanceRecord record(String student, AttendanceStatus status) =>
          AttendanceRecord(
            studentId: student,
            sessionId: 'lesson',
            status: status,
            recordedAt: at,
            importSource: 'owner-sheet:2',
          );
      final counts = LessonAttendanceCounts([
        record('first', AttendanceStatus.present),
        record('first', AttendanceStatus.present),
        record('second', AttendanceStatus.makeup),
        record('second', AttendanceStatus.makeup),
        record('absent', AttendanceStatus.absent),
      ]);
      expect(counts.total, 2);
      expect(counts.makeup, 1);
    },
  );

  test(
    'month totals count separate lessons and exclude cancelled attendance',
    () async {
      final first = await start(home, 1);
      final second = await start(home, 2);
      for (final session in [first, second]) {
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
      }
      expect(report().summary['إجمالي الحضور المسجل'], '2');
      await store.cancelAttendance(
        attendanceId: store.attendances.first.id,
        reason: 'تصحيح تسجيل حصة غير صحيحة',
      );
      final refreshed = report();
      expect(refreshed.summary['إجمالي الحضور المسجل'], '1');
      expect(row(refreshed, home)['حصة 1'], 0);
      expect(row(refreshed, home)['حصة 2'], 1);
      expect(row(refreshed, home)['حصة 3'], 'غير متاح');
    },
  );

  test(
    'absence-only and missing source lessons disclose outbound makeup without double counting',
    () async {
      final source = await start(home, 3);
      await store.renewPackage(
        PackageRequest(
          studentId: student.id,
          groupId: home.id,
          sessionId: source.id,
        ),
      );
      await store.closeSession(source.id);
      final absent = store.attendances.single;
      final target = await start(visiting, 3);
      await store.recordAttendance(
        EntryRequest(
          studentId: student.id,
          sessionId: target.id,
          mode: EntryMode.makeup,
          originalAttendanceId: absent.id,
        ),
      );
      var table = report();
      expect(table.summary['إجمالي الحضور المسجل'], '1');
      expect(row(table, home)['حصة 3'], 'غير محسوم');
      expect(row(table, home)['تعويض خارج المجموعة'], 1);
      expect(row(table, visiting)['حصة 3'], 1);
      expect(row(table, visiting)['منهم معوّضون داخل الحصص'], 1);

      final otherTarget = await start(visiting, 2);
      await store.recordAttendance(
        EntryRequest(
          studentId: student.id,
          sessionId: otherTarget.id,
          mode: EntryMode.makeup,
          makeupSourceGroupId: home.id,
        ),
      );
      table = report(CenterReportFilter(groupIds: {home.id}));
      expect(row(table, home)['حصة 2'], 'غير متاح');
      expect(row(table, home)['تعويض خارج المجموعة'], 2);
      expect(table.summary['إجمالي الحضور المسجل'], '0');
      expect(row(table, home)['ملاحظات'], contains('حصة 2: 1 تعويض'));
    },
  );

  test(
    'absence messages exclude active makeup for the same shared lesson only',
    () async {
      final source = await start(home, 1);
      await store.closeSession(source.id);
      final nextSource = await start(home, 2);
      await store.closeSession(nextSource.id);

      Object? messageAttendance(LessonSession session) =>
          CenterWhatsAppReports.build(
            store,
            CenterWhatsAppSelection(groupId: home.id, sessionId: session.id),
          ).whatsAppRecords.single['attendance'];

      expect(messageAttendance(source), 'غائب');
      final target = await start(visiting, 1);
      await store.recordAttendance(
        EntryRequest(
          studentId: student.id,
          sessionId: target.id,
          mode: EntryMode.makeup,
          makeupSourceGroupId: home.id,
        ),
      );

      expect(messageAttendance(source), 'حاضر • تعويض');
      expect(messageAttendance(nextSource), 'غائب');
      final attendanceReport = CenterReports.build(
        store,
        CenterReportKind.attendance,
        CenterReportFilter(sessionId: source.id),
      );
      expect(attendanceReport.whatsAppRecords.single['attendance'], 'معوّض');
      expect(
        store.academicAttendanceForSession(source.id).single.status,
        AttendanceStatus.absent,
      );

      await store.cancelAttendance(
        attendanceId: store.academicAttendanceForSession(target.id).single.id,
        reason: 'إلغاء التعويض المسجل بالخطأ',
      );
      expect(messageAttendance(source), 'غائب');
    },
  );

  test(
    'manual historical attendance remains in its study month without a known date or prepared ID',
    () async {
      final session = await start(home, 1);
      await store.recordAttendance(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      final backup =
          jsonDecode(await File(await store.createBackup()).readAsString())
              as Map<String, dynamic>;
      final snapshot = backup['data'] as Map<String, dynamic>;
      snapshot['installationSeedId'] =
          'synthetic-attendance-summary-installation';
      (snapshot['sessions'] as List).single
        ..remove('preparedLessonId')
        ..['startsAtKnown'] = false;
      (snapshot['attendances'] as List).single
        ..['importSource'] = 'owner-attendance-sheet:2'
        ..['paymentPending'] = false
        ..['recordedAtKnown'] = false;
      final import = File('${directory.path}/manual-history.json');
      await import.writeAsString(jsonEncode(backup));
      await store.restoreBackup(import.path);
      await store.signIn('مدير', 'attendance-summary-password');
      final filtered = report(CenterReportFilter(studyMonthId: month.id));
      expect(filtered.summary['إجمالي الحضور المسجل'], '1');
      expect(row(filtered, home)['حصة 1'], 1);
      final csv = await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.attendanceSummary,
        filter: CenterReportFilter(studyMonthId: month.id),
        destination: '${directory.path}/attendance.csv',
      );
      expect(await File(csv).readAsString(), contains('إجمالي الحضور المسجل'));
      expect(report(CenterReportFilter(from: DateTime(2026))).rows, isEmpty);
    },
  );
}
