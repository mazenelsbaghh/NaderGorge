import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late Student student;
  late StudyGroup group;
  late LessonSession session;
  setUp(() async {
    directory = await Directory.systemTemp.createTemp(
      'massar-correction-reports-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('الإدارة', 'report-password-2026');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'الأحد',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 10000,
        packagePrice: 40000,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        code: '001',
        name: 'أحمد',
        groupIds: [group.id],
        discountPercent: 25,
        createdAt: DateTime.now().subtract(const Duration(days: 20)),
      ),
    );
    student = store.students.single;
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: DateTime.now(),
        createdAt: DateTime.now(),
      ),
    );
    session = store.sessions.single;
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });
  CenterReportData report(
    CenterReportKind kind, [
    CenterReportFilter filter = const CenterReportFilter(),
  ]) => CenterReports.build(store, kind, filter);
  Map<String, Object?> mappedRow(CenterReportData report, int index) =>
      Map.fromIterables(report.columns, report.rows[index]);

  test(
    'entry corrections preserve original collections, refunds and historical review snapshots while removing canceled balances',
    () async {
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      final original = store.payments.single;
      await store.checkPayment(studentId: student.id, sessionId: session.id);
      await store.savePaymentReview(
        ReviewRequest(
          studentId: student.id,
          paymentId: original.id,
          sessionId: session.id,
          paperAmount: 7500,
        ),
      );
      await store.correctEntry(
        attendanceId: store.attendances.single.id,
        mode: EntryMode.package,
        reason: 'تحويل الحصة إلى شهر',
      );
      final package = store.packages.single;
      await store.reverseEntry(
        attendanceId: store.attendances.single.id,
        reason: 'دخول الطالب بالخطأ',
      );
      await store.refundPackage(
        packageId: package.id,
        reason: '=سبب رد الشهر',
        refundMethod: 'تحويل',
      );
      final movements = report(CenterReportKind.payments);
      expect(movements.rows, hasLength(4));
      expect(movements.summary['التحصيل'], '375.00 ج');
      expect(movements.summary['الاستردادات'], '375.00 ج');
      expect(movements.summary['الصافي بعد الاسترداد'], '0.00 ج');
      expect(movements.summary['صافي النقدي'], '300.00 ج');
      expect(movements.summary['تحويل'], '-300.00 ج');
      expect(movements.summary['دفعات سارية ضمن النتائج'], '0');
      final refundedOriginal = movements.rows.firstWhere(
        (row) =>
            row[movements.columns.indexOf('نوع الحركة')] == 'تحصيل' &&
            row[movements.columns.indexOf('رقم العملية')] == original.id,
      );
      expect(
        refundedOriginal[movements.columns.indexOf('الصافي (جنيه مصري)')],
        '75.00',
      );
      expect(
        refundedOriginal[movements.columns.indexOf('الحالة الحالية')],
        'ملغاة — مستردة',
      );
      final packages = report(CenterReportKind.packages);
      expect(packages.rows, hasLength(1));
      expect(mappedRow(packages, 0)['المتبقي حاليًا'], 0);
      expect(mappedRow(packages, 0)['حالة الباقة'], 'ملغاة — مستردة');
      expect(packages.summary['الرصيد الحالي'], '0 حصة');
      expect(packages.summary['باقات مستردة'], '1');
      expect(report(CenterReportKind.attendance).rows, isEmpty);
      expect(
        report(
          CenterReportKind.attendance,
          const CenterReportFilter(attendanceCorrections: true),
        ).rows,
        hasLength(2),
      );
      final correctionHistory = report(
        CenterReportKind.attendance,
        const CenterReportFilter(attendanceCorrections: true),
      );
      final changed = correctionHistory.rows.firstWhere(
        (row) => row.contains('تصحيح طريقة الدخول'),
      );
      expect(
        changed[correctionHistory.columns.indexOf('الحالة قبل التصحيح')],
        'حاضر · دفع بالحصة',
      );
      expect(
        changed[correctionHistory.columns.indexOf('الحالة بعد التصحيح')],
        'حاضر · باقة',
      );

      expect(
        report(CenterReportKind.reviews).rows.single,
        contains('دافع حصة'),
      );
      expect(
        report(
          CenterReportKind.reviews,
          const CenterReportFilter(reviewMode: ReviewReportMode.amounts),
        ).rows.single,
        contains('مطابق'),
      );
      final refundsOnly = CenterReportFilter(
        studentId: student.id,
        sessionId: session.id,
        centerId: group.centerId,
        paymentMode: PaymentReportMode.refunds,
      );
      expect(report(CenterReportKind.payments, refundsOnly).rows, hasLength(2));
      final path = '${directory.path}/refunds.csv';
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.payments,
        filter: refundsOnly,
        destination: path,
      );
      final csv = await File(path).readAsString();
      expect(csv, contains('"-300.00"'));
      expect(csv, contains("'=سبب رد الشهر"));
      expect(csv, isNot(contains('"تحصيل"')));
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('الإدارة', 'report-password-2026');
      expect(
        report(CenterReportKind.payments).summary['الصافي بعد الاسترداد'],
        '0.00 ج',
      );
    },
  );

  test(
    'refund date filtering does not erase yesterday collection or backdate today payout',
    () async {
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      await store.reverseEntry(
        attendanceId: store.attendances.single.id,
        reason: 'رد الحصة',
      );
      final backup = await store.createBackup();
      final json =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final yesterday = DateTime.now().subtract(const Duration(days: 1));
      final state = json['data'] as Map<String, dynamic>;
      (state['payments'] as List).single['createdAt'] = yesterday
          .toIso8601String();
      final dated = File('${directory.path}/dated.json');
      await dated.writeAsString(jsonEncode(json));
      await store.restoreBackup(dated.path);
      await store.signIn('الإدارة', 'report-password-2026');
      final yesterdayReport = report(
        CenterReportKind.payments,
        CenterReportFilter(from: yesterday, until: yesterday),
      );
      expect(yesterdayReport.rows, hasLength(1));
      expect(yesterdayReport.summary['التحصيل'], '75.00 ج');
      expect(yesterdayReport.summary['الاستردادات'], '0.00 ج');
      expect(yesterdayReport.summary['الصافي بعد الاسترداد'], '75.00 ج');
      final today = report(
        CenterReportKind.payments,
        CenterReportFilter(from: DateTime.now(), until: DateTime.now()),
      );
      expect(today.rows, hasLength(1));
      expect(today.summary['التحصيل'], '0.00 ج');
      expect(today.summary['الاستردادات'], '75.00 ج');
      expect(today.summary['الصافي بعد الاسترداد'], '-75.00 ج');
    },
  );

  test(
    'payment method correction changes effective cash totals without inventing a refund',
    () async {
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
          method: 'تحويل',
        ),
      );
      await store.correctPaymentMethod(
        paymentId: store.payments.single.id,
        method: 'نقدي',
        reason: 'الطريقة الأصلية خاطئة',
      );
      final payments = report(CenterReportKind.payments);
      expect(payments.rows, hasLength(1));
      expect(payments.summary['التحصيل'], '75.00 ج');
      expect(payments.summary['الاستردادات'], '0.00 ج');
      expect(payments.summary['صافي النقدي'], '75.00 ج');
      expect(mappedRow(payments, 0)['الطريقة الأصلية'], 'تحويل');
      expect(mappedRow(payments, 0)['طريقة الدفع'], 'نقدي');
    },
  );

  test(
    'reopened closing remains immutable history and only replacement active closing contributes totals',
    () async {
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      final attendanceId = store.attendances.single.id;
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 7500);
      final original = store.closings.single;
      await store.reopenFinancialClosing(
        closingId: original.id,
        reason: 'تصحيح دخول خاطئ',
      );
      await store.reverseEntry(
        attendanceId: attendanceId,
        reason: 'الطالب لم يحضر',
      );
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final closings = report(CenterReportKind.closings);
      expect(closings.rows, hasLength(2));
      expect(closings.summary['التقفيلات السارية'], '1');
      expect(closings.summary['النقدي الفعلي'], '0.00 ج');
      expect(closings.summary['فرق النقدي'], '0.00 ج');
      expect(
        store.allClosings.first.summary.totalCollected,
        original.summary.totalCollected,
      );
      expect(
        closings.rows.any((row) => row.contains('أُعيد فتحها — تاريخية')),
        isTrue,
      );
      final attendance = report(CenterReportKind.attendance);
      expect(attendance.summary['حضور'], '0');
      expect(attendance.summary['غياب'], '1');
    },
  );
}
