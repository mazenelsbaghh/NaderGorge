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
  late StudyGroup firstGroup, secondGroup;
  late Student ahmed, mina, absent, inactive;
  late LessonSession firstSession, secondSession;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-reports-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('الإدارة', 'test-password-2026');
    for (final catalog in [
      const CatalogEntry(name: 'فيزياء', kind: CatalogKind.subject),
      const CatalogEntry(name: 'النور', kind: CatalogKind.center),
      const CatalogEntry(name: 'الجنوب', kind: CatalogKind.center),
      const CatalogEntry(name: 'الثالث الثانوي', kind: CatalogKind.grade),
    ]) {
      await store.saveCatalog(catalog);
    }
    final subject = store.catalogs
        .firstWhere((entry) => entry.kind == CatalogKind.subject)
        .id;
    final grade = store.catalogs
        .firstWhere((entry) => entry.kind == CatalogKind.grade)
        .id;
    for (final center in store.catalogs.where(
      (entry) => entry.kind == CatalogKind.center,
    )) {
      await store.saveGroup(
        StudyGroup(
          name: center.name == 'النور' ? 'الأحد' : 'الثلاثاء',
          subjectId: subject,
          centerId: center.id,
          gradeId: grade,
          schedule: '٥ مساءً',
          sessionPrice: 10025,
          packagePrice: 40050,
        ),
      );
    }
    firstGroup = store.groups.first;
    secondGroup = store.groups.last;
    for (final (code, name, group, discount) in [
      ('001', 'أحمد', firstGroup, 25),
      ('002', 'مينا', firstGroup, 0),
      ('003', 'يوسف', firstGroup, 20),
      ('004', 'بدون نشاط', secondGroup, 0),
    ]) {
      await store.saveStudent(
        Student(
          code: code,
          name: name,
          groupIds: [group.id],
          discountPercent: discount,
          notes: code == '001' ? '=SUM(1,2)\n"ملاحظة"' : '',
          createdAt: DateTime.now().subtract(const Duration(days: 20)),
        ),
      );
    }
    ahmed = store.students[0];
    mina = store.students[1];
    absent = store.students[2];
    inactive = store.students[3];
    for (final (group, days) in [(firstGroup, 1), (secondGroup, 2)]) {
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 1,
          startsAt: DateTime.now().add(Duration(days: days)),
          createdAt: DateTime.now(),
        ),
      );
    }
    firstSession = store.sessions.first;
    secondSession = store.sessions.last;
    await store.collectAndAttend(
      EntryRequest(
        studentId: ahmed.id,
        sessionId: firstSession.id,
        mode: EntryMode.package,
      ),
    );
    await store.collectAndAttend(
      EntryRequest(
        studentId: mina.id,
        sessionId: firstSession.id,
        mode: EntryMode.single,
        method: 'تحويل',
      ),
    );
    await store.renewPackage(
      PackageRequest(
        studentId: absent.id,
        groupId: firstGroup.id,
        sessionId: firstSession.id,
      ),
    );
    await store.closeSession(firstSession.id);
    final original = store.attendances.firstWhere(
      (record) =>
          record.studentId == absent.id &&
          record.status == AttendanceStatus.absent,
    );
    await store.collectAndAttend(
      EntryRequest(
        studentId: absent.id,
        sessionId: secondSession.id,
        mode: EntryMode.makeup,
        originalAttendanceId: original.id,
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: ahmed.id,
        sessionId: firstSession.id,
        score: 0,
        maxScore: 20,
        homework: HomeworkStatus.complete,
        updatedAt: DateTime.now(),
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: mina.id,
        sessionId: firstSession.id,
        examAbsent: true,
        homework: HomeworkStatus.missing,
        updatedAt: DateTime.now(),
      ),
    );
    await store.saveAcademic(
      AcademicRecord(
        studentId: absent.id,
        sessionId: secondSession.id,
        score: 5,
        maxScore: 10,
        homework: HomeworkStatus.incomplete,
        updatedAt: DateTime.now(),
      ),
    );
    final payment = store.payments.firstWhere(
      (payment) => payment.studentId == ahmed.id,
    );
    await store.savePaymentReview(
      ReviewRequest(
        studentId: ahmed.id,
        paymentId: payment.id,
        sessionId: firstSession.id,
        paperAmount: payment.netAmount - 100,
      ),
    );
    await store.savePaymentReview(
      ReviewRequest(
        studentId: absent.id,
        sessionId: secondSession.id,
        paperAmount: 5000,
      ),
    );
    await store.finalizeSession(
      sessionId: firstSession.id,
      actualCash: 62000,
      notes: 'فرق مسجل للمراجعة',
    );
    await store.renewPackage(
      PackageRequest(studentId: absent.id, groupId: firstGroup.id),
    );
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  CenterReportData report(
    CenterReportKind kind, [
    CenterReportFilter filter = const CenterReportFilter(),
  ]) => CenterReports.build(store, kind, filter);
  Map<String, Object?> studentRow(CenterReportData report, String code) {
    final index = report.columns.indexOf('الكود');
    final row = report.rows.firstWhere((row) => row[index] == code);
    return Map.fromIterables(report.columns, row);
  }

  test(
    'all report types preserve inactive roster and actual financial and academic distinctions',
    () {
      for (final kind in CenterReportKind.values) {
        final table = report(kind);
        expect(
          table.rows.every((row) => row.length == table.columns.length),
          isTrue,
          reason:
              '${kind.name} rows must align with visible and exported columns',
        );
      }
      final roster = report(CenterReportKind.students);
      expect(roster.rows, hasLength(4));
      expect(studentRow(roster, inactive.code)['نسبة الحضور'], '—');
      expect(studentRow(roster, absent.code)['معوّض'], 1);
      expect(studentRow(roster, absent.code)['نسبة الحضور'], '50.00٪');
      final groupReport = report(CenterReportKind.groups);
      expect(groupReport.rows, hasLength(2));
      expect(groupReport.columns, isNot(contains('السعة')));
      final firstGroupRow = Map.fromIterables(
        groupReport.columns,
        groupReport.rows.firstWhere((row) => row.first == firstGroup.name),
      );
      expect(
        firstGroupRow['المسجلون حاليًا'],
        store.students
            .where((student) => student.groupIds.contains(firstGroup.id))
            .length,
      );
      expect(report(CenterReportKind.sessions).rows, hasLength(2));
      final attendance = report(CenterReportKind.attendance);
      expect(attendance.summary, containsPair('معوّض', '1'));
      expect(
        attendance.rows.any((row) => row.contains('غائب — تم التعويض')),
        isTrue,
      );
      final packages = report(CenterReportKind.packages);
      expect(packages.rows, hasLength(3));
      expect(packages.summary['الرصيد الحالي'], '10 حصة');
      final payments = report(CenterReportKind.payments);
      expect(payments.rows, hasLength(4));
      expect(payments.summary['التحصيل'], '1041.43 ج');
      expect(payments.summary['عمليات غير مرتبطة بحصة'], '1');
      final exams = report(
        CenterReportKind.exams,
        CenterReportFilter(sessionId: firstSession.id),
      );
      expect(studentRow(exams, ahmed.code)['الدرجة'], 0);
      expect(studentRow(exams, mina.code)['حالة الامتحان'], 'غائب عن الامتحان');
      expect(studentRow(exams, absent.code)['حالة الامتحان'], 'لم تُرصد');
      expect(
        studentRow(report(CenterReportKind.homework), mina.code)['حالة الواجب'],
        'لم يعمل',
      );
      final reviews = report(
        CenterReportKind.reviews,
        const CenterReportFilter(reviewMode: ReviewReportMode.amounts),
      );
      expect(reviews.summary['غير محسومة'], '2');
      expect(
        reviews.rows.any((row) => row.contains('ورق دون دفع مسجل')),
        isTrue,
      );
      final closings = report(CenterReportKind.closings);
      expect(closings.rows, hasLength(1));
      expect(closings.summary['فرق النقدي'], '-0.78 ج');
      expect(closings.summary['التحصيل المثبت'], '721.03 ج');
    },
  );

  test(
    'filters combine exact student, academic scope, session and their honest calendar date basis',
    () {
      final oneStudent = CenterReportFilter(
        studentId: ahmed.id,
        subjectId: firstGroup.subjectId,
        centerId: firstGroup.centerId,
        gradeId: firstGroup.gradeId,
        groupId: firstGroup.id,
        sessionId: firstSession.id,
      );
      expect(
        report(CenterReportKind.attendance, oneStudent).rows,
        hasLength(1),
      );
      expect(report(CenterReportKind.exams, oneStudent).rows, hasLength(1));
      expect(report(CenterReportKind.payments, oneStudent).rows, hasLength(1));
      expect(
        report(
          CenterReportKind.attendance,
          CenterReportFilter(
            studentId: ahmed.id,
            centerId: secondGroup.centerId,
          ),
        ).rows,
        isEmpty,
      );
      final lessonDay = CenterReportFilter(
        from: firstSession.startsAt,
        until: firstSession.startsAt,
      );
      expect(report(CenterReportKind.attendance, lessonDay).rows, hasLength(3));
      expect(report(CenterReportKind.sessions, lessonDay).rows, hasLength(1));
      expect(report(CenterReportKind.payments, lessonDay).rows, isEmpty);
      expect(report(CenterReportKind.packages, lessonDay).rows, isEmpty);
      expect(report(CenterReportKind.reviews, lessonDay).rows, isEmpty);
      expect(report(CenterReportKind.closings, lessonDay).rows, isEmpty);
      expect(report(CenterReportKind.students, lessonDay).rows, hasLength(4));
      final unassigned = report(
        CenterReportKind.payments,
        CenterReportFilter(
          studentId: absent.id,
          groupId: firstGroup.id,
          unassignedPaymentsOnly: true,
        ),
      );
      expect(unassigned.rows, hasLength(1));
      expect(unassigned.title, 'مدفوعات غير مرتبطة بحصة');
      expect(
        report(
          CenterReportKind.payments,
          CenterReportFilter(
            sessionId: firstSession.id,
            unassignedPaymentsOnly: true,
          ),
        ).rows,
        isEmpty,
      );
    },
  );

  test(
    'later enrollment does not invent old exam or homework rows, while actual history remains',
    () async {
      await store.saveSession(
        LessonSession(
          groupId: secondGroup.id,
          number: 2,
          kind: SessionKind.free,
          startsAt: DateTime.now().subtract(const Duration(days: 1)),
          createdAt: DateTime.now(),
        ),
      );
      final old = store.sessions.last;
      await store.saveStudent(
        Student(
          name: 'طالب جديد',
          code: '005',
          groupIds: [secondGroup.id],
          createdAt: DateTime.now(),
        ),
      );
      final exam = report(
        CenterReportKind.exams,
        CenterReportFilter(sessionId: old.id),
      );
      final homework = report(
        CenterReportKind.homework,
        CenterReportFilter(sessionId: old.id),
      );
      expect(exam.rows.any((row) => row.contains('005')), isFalse);
      expect(homework.rows.any((row) => row.contains('005')), isFalse);
      await store.saveStudent(ahmed.copyWith(groupIds: [secondGroup.id]));
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(studentId: ahmed.id, sessionId: old.id),
        ).rows,
        isEmpty,
      );
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(studentId: ahmed.id, sessionId: firstSession.id),
        ).rows,
        hasLength(1),
      );
    },
  );

  test(
    'code review reports export only current receipts and preserve amount reviews separately',
    () async {
      for (final student in [ahmed, mina]) {
        final payment = store.payments.singleWhere(
          (p) => p.studentId == student.id && p.sessionId == firstSession.id,
        );
        await store.checkPayment(
          studentId: student.id,
          sessionId: firstSession.id,
          expectedAmount: payment.collectedAmount,
        );
      }
      for (final student in [absent, inactive]) {
        await expectLater(
          store.checkPayment(
            studentId: student.id,
            sessionId: secondSession.id,
            expectedAmount: 10025,
          ),
          throwsA(isA<CenterException>()),
        );
      }
      var checks = report(CenterReportKind.reviews);
      expect(checks.rows, hasLength(2));
      expect(checks.columns, isNot(contains('الورق (جنيه مصري)')));
      final payment = store.payments.singleWhere(
        (p) => p.studentId == ahmed.id,
      );
      expect(studentRow(checks, ahmed.code)['رقم عملية الدفع'], payment.id);
      expect(studentRow(checks, ahmed.code)['رقم الباقة'], payment.packageId);
      expect(checks.rows.any((row) => row.contains('مطابق')), isFalse);
      final all = report(
        CenterReportKind.reviews,
        const CenterReportFilter(reviewMode: ReviewReportMode.all),
      );
      expect(all.rows, hasLength(4));
      for (final row in all.rows.where(
        (row) => row[all.columns.indexOf('نوع المراجعة')] == 'مراجعة كود',
      )) {
        for (final column in [
          'المقبوض الأصلي وقت المراجعة (جنيه مصري)',
          'الورق (جنيه مصري)',
          'الفرق (جنيه مصري)',
        ]) {
          expect(row[all.columns.indexOf(column)], isNull);
        }
      }
      await store.collectAndAttend(
        EntryRequest(
          studentId: inactive.id,
          sessionId: secondSession.id,
          mode: EntryMode.single,
        ),
      );
      expect(report(CenterReportKind.reviews).rows, hasLength(2));
      await store.checkPayment(
        studentId: inactive.id,
        sessionId: secondSession.id,
        expectedAmount: 10025,
      );
      checks = report(CenterReportKind.reviews);
      expect(checks.rows, hasLength(3));
      final exact = CenterReportFilter(
        studentId: inactive.id,
        sessionId: secondSession.id,
        centerId: secondGroup.centerId,
        from: DateTime.now(),
        until: DateTime.now(),
      );
      final path = '${directory.path}/code-check.csv';
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.reviews,
        filter: exact,
        destination: path,
      );
      final csv = await File(path).readAsString();
      expect(csv, contains(inactive.code));
      expect(csv, contains('100.25'));
      expect(csv, isNot(contains('أحمد')));
      expect(csv, isNot(contains('الورق (جنيه مصري)')));
      expect(
        report(
          CenterReportKind.reviews,
          CenterReportFilter(
            studentId: inactive.id,
            centerId: firstGroup.centerId,
          ),
        ).rows,
        isEmpty,
      );
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('الإدارة', 'test-password-2026');
      expect(report(CenterReportKind.reviews).rows, hasLength(3));
      expect(
        report(
          CenterReportKind.reviews,
          const CenterReportFilter(reviewMode: ReviewReportMode.amounts),
        ).rows,
        hasLength(2),
      );
    },
  );

  test(
    'CSV exports the filtered table with BOM and formula escaping without changing historical amounts',
    () async {
      await store.saveStudent(ahmed.copyWith(discountPercent: 0));
      final filter = CenterReportFilter(
        studentId: ahmed.id,
        sessionId: firstSession.id,
      );
      final file = File('${directory.path}/filtered.csv');
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.payments,
        filter: filter,
        destination: file.path,
      );
      final bytes = await file.readAsBytes();
      expect(bytes.take(3).toList(), [0xef, 0xbb, 0xbf]);
      final content = utf8.decode(bytes.skip(3).toList());
      expect(content, contains('"300.38"'));
      expect(content, contains('"25"'));
      expect(content, contains('"001"'));
      expect(content, isNot(contains('"مينا"')));
      expect(
        content.split('\r\n').where((line) => line.isNotEmpty),
        hasLength(2),
      );
      final rosterFile = '${directory.path}/roster.csv';
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.students,
        filter: CenterReportFilter(studentId: ahmed.id),
        destination: rosterFile,
      );
      final roster = await File(rosterFile).readAsString();
      expect(roster, contains("'=SUM(1,2)"));
      expect(roster, contains('""ملاحظة""'));
      expect(roster, isNot(contains('password')));
      final closingCsv = '${directory.path}/closing.csv';
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.closings,
        filter: const CenterReportFilter(),
        destination: closingCsv,
      );
      final closingContent = await File(closingCsv).readAsString();
      expect(closingContent, contains('"-0.78"'));
      expect(closingContent, isNot(contains("'-0.78")));

      final original = await file.readAsBytes();
      await expectLater(
        CenterReports.exportCsv(
          store: store,
          kind: CenterReportKind.payments,
          filter: filter,
          destination: file.path,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(await file.readAsBytes(), original);
      await expectLater(
        CenterReports.exportCsv(
          store: store,
          kind: CenterReportKind.students,
          filter: const CenterReportFilter(),
          destination: store.databasePath,
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        CenterReports.exportCsv(
          store: store,
          kind: CenterReportKind.students,
          filter: const CenterReportFilter(),
          destination: '${directory.path}/backups/report.csv',
        ),
        throwsA(isA<CenterException>()),
      );
    },
  );

  test(
    'closing category reports preserve snapshot discounts, distinct students and overlapping attendance categories',
    () async {
      await store.saveStudent(ahmed.copyWith(discountPercent: 0));
      await store.saveStudent(mina.copyWith(discountPercent: 100));
      await store.saveGroup(
        firstGroup.copyWith(sessionPrice: 90000, packagePrice: 80000),
      );
      await store.saveSession(
        LessonSession(
          groupId: firstGroup.id,
          number: 2,
          startsAt: firstSession.startsAt.add(const Duration(days: 7)),
          createdAt: DateTime.now(),
        ),
      );
      final next = store.sessions.last;
      for (var purchase = 0; purchase < 2; purchase++) {
        await store.renewPackage(
          PackageRequest(
            studentId: ahmed.id,
            groupId: firstGroup.id,
            sessionId: next.id,
          ),
        );
      }
      await store.collectAndAttend(
        EntryRequest(
          studentId: ahmed.id,
          sessionId: next.id,
          mode: EntryMode.package,
        ),
      );
      await store.collectAndAttend(
        EntryRequest(
          studentId: mina.id,
          sessionId: next.id,
          mode: EntryMode.single,
        ),
      );
      await store.closeSession(next.id);
      await store.finalizeSession(sessionId: next.id, actualCash: 160000);
      final detail = report(
        CenterReportKind.closings,
        CenterReportFilter(
          sessionId: next.id,
          closingMode: ClosingReportMode.categories,
        ),
      );
      final labelIndex = detail.columns.indexOf('فئة الطلبة');
      final fullPackage = detail.rows.firstWhere(
        (row) => row[labelIndex] == 'باقة بالسعر الكامل',
      );
      expect(fullPackage[detail.columns.indexOf('عدد الطلبة داخل الفئة')], 1);
      expect(fullPackage[detail.columns.indexOf('عدد عمليات الدفع')], 2);
      expect(
        fullPackage[detail.columns.indexOf('المحصل للوحدة (جنيه مصري)')],
        '800.00',
      );
      final prepaid = detail.rows.firstWhere(
        (row) => row[labelIndex] == 'حضور بباقة سابقة بخصم 25٪',
      );
      expect(prepaid[detail.columns.indexOf('نسبة الخصم ٪')], 25);
      expect(
        prepaid[detail.columns.indexOf('المحصل للوحدة (جنيه مصري)')],
        '0.00',
      );
      expect(prepaid[detail.columns.indexOf('عدد عمليات الدفع')], 0);
      final exempt = detail.rows.firstWhere(
        (row) => row[labelIndex] == 'حضور مجاني أو بإعفاء من رسوم المدرس',
      );
      expect(exempt[detail.columns.indexOf('نسبة الخصم ٪')], isNull);
      expect(exempt[detail.columns.indexOf('عدد عمليات الدفع')], 0);
      expect(
        exempt[detail.columns.indexOf('المحصل للوحدة (جنيه مصري)')],
        '0.00',
      );
      expect(
        detail.rows.any((row) => row[labelIndex] == 'حضور حصة مجانية'),
        isFalse,
      );
      final original = report(
        CenterReportKind.closings,
        CenterReportFilter(
          sessionId: firstSession.id,
          closingMode: ClosingReportMode.categories,
        ),
      );
      expect(
        original.rows.any(
          (row) => row.contains('باقة بخصم 25٪') && row.contains('300.38'),
        ),
        isTrue,
      );
      expect(
        original.rows.any(
          (row) => row.contains('باقة بخصم 20٪') && row.contains('320.40'),
        ),
        isTrue,
      );
      final summary = report(
        CenterReportKind.closings,
        CenterReportFilter(sessionId: next.id),
      );
      final closingRow = Map.fromIterables(
        summary.columns,
        summary.rows.single,
      );
      expect(closingRow['إعفاء 100٪'], '0 طالب');
      expect(closingRow['توزيع الخصم الثابت للحاضرين'], contains('100'));
      expect(closingRow['كل الحضور المجاني والإعفاء'], 1);
      expect(closingRow['دفع بلا خصم حسب الفئة'], contains('1 طالب، 2 عملية'));
      expect(closingRow['حضور من رصيد سابق'], contains('1 طالب، 0 عملية'));
      expect(closingRow['تفصيل نسب الخصم'], '0 طالب');
      expect(detail.caption, contains('قد تتداخل'));
      final path = '${directory.path}/saved-categories.csv';
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.closings,
        filter: CenterReportFilter(
          sessionId: firstSession.id,
          closingMode: ClosingReportMode.categories,
        ),
        destination: path,
      );
      final csv = await File(path).readAsString();
      expect(csv, contains('باقة بخصم 25٪'));
      expect(csv, contains('"300.38"'));
      expect(csv, isNot(contains('"800.00"')));
      final reopened = store.closings.firstWhere(
        (closing) => closing.sessionId == next.id,
      );
      await store.reopenFinancialClosing(
        closingId: reopened.id,
        reason: 'مراجعة التقفيلة',
      );
      final historical = report(
        CenterReportKind.closings,
        CenterReportFilter(
          sessionId: next.id,
          closingMode: ClosingReportMode.categories,
        ),
      );
      expect(
        historical.rows.every((row) => row.contains('أُعيد فتحها — تاريخية')),
        isTrue,
      );
      expect(historical.summary['التحصيل المثبت'], '0.00 ج');
      expect(
        historical.rows
            .firstWhere((row) => row[labelIndex] == 'باقة بالسعر الكامل')
            .sublist(labelIndex),
        fullPackage.sublist(labelIndex),
      );
    },
  );

  test(
    'free-session and makeup categories carry zero operations without claiming a 100 percent payment exemption',
    () async {
      await store.closeSession(secondSession.id);
      await store.finalizeSession(sessionId: secondSession.id, actualCash: 0);
      final makeup = report(
        CenterReportKind.closings,
        CenterReportFilter(
          sessionId: secondSession.id,
          closingMode: ClosingReportMode.categories,
        ),
      );
      final makeupRow = Map.fromIterables(
        makeup.columns,
        makeup.rows.firstWhere(
          (row) => row[makeup.columns.indexOf('فئة الطلبة')] == 'تعويض',
        ),
      );
      expect(makeupRow['فئة الطلبة'], 'تعويض');
      expect(makeupRow['عدد الطلبة داخل الفئة'], 1);
      expect(makeupRow['عدد عمليات الدفع'], 0);
      expect(makeupRow['المحصل للوحدة (جنيه مصري)'], '0.00');
      await store.saveStudent(inactive.copyWith(discountPercent: 100));
      await store.saveSession(
        LessonSession(
          groupId: secondGroup.id,
          number: 2,
          kind: SessionKind.free,
          startsAt: secondSession.startsAt.add(const Duration(days: 7)),
          createdAt: DateTime.now(),
        ),
      );
      final free = store.sessions.last;
      await store.collectAndAttend(
        EntryRequest(
          studentId: inactive.id,
          sessionId: free.id,
          mode: EntryMode.single,
        ),
      );
      await store.closeSession(free.id);
      await store.finalizeSession(sessionId: free.id, actualCash: 0);
      final detail = report(
        CenterReportKind.closings,
        CenterReportFilter(
          sessionId: free.id,
          closingMode: ClosingReportMode.categories,
        ),
      );
      final freeRow = Map.fromIterables(detail.columns, detail.rows.single);
      expect(freeRow['فئة الطلبة'], 'حضور مجاني أو بإعفاء من رسوم المدرس');
      expect(freeRow['نسبة الخصم ٪'], isNull);
      expect(freeRow['عدد عمليات الدفع'], 0);
      expect(freeRow['عدد الطلبة داخل الفئة'], 1);
      final summary = report(
        CenterReportKind.closings,
        CenterReportFilter(sessionId: free.id),
      );
      expect(
        Map.fromIterables(summary.columns, summary.rows.single)['إعفاء 100٪'],
        '0 طالب',
      );
      expect(summary.summary['التحصيل المثبت'], '0.00 ج');
    },
  );

  test(
    'legacy closing categories remain unavailable instead of inferred from current student discounts',
    () async {
      final backup = await store.createBackup();
      final json =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final state = json['data'] as Map<String, dynamic>;
      final closing =
          (state['closings'] as List).single as Map<String, dynamic>;
      final savedSummary = closing['summary'] as Map<String, dynamic>;
      for (final key in [
        'studentCategories',
        'attendanceDiscountCategories',
        'allFreeCount',
        'packageBuyerCount',
        'paymentAmountCategories',
      ]) {
        savedSummary.remove(key);
      }
      final legacy = File('${directory.path}/legacy-no-categories.json');
      await legacy.writeAsString(jsonEncode(json));
      await store.restoreBackup(legacy.path);
      await store.signIn('الإدارة', 'test-password-2026');
      await store.saveStudent(ahmed.copyWith(discountPercent: 100));
      final summary = report(CenterReportKind.closings);
      final row = Map.fromIterables(summary.columns, summary.rows.single);
      expect(row['تفاصيل الفئات'], 'غير محفوظة — تقفيلة قديمة');
      expect(row['تفصيل نسب الخصم'], 'غير متوفرة في هذه التقفيلة');
      expect(row['إعفاء 100٪'], 'غير متوفرة في هذه التقفيلة');
      expect(summary.summary['التحصيل المثبت'], '721.03 ج');
      final sessions = report(
        CenterReportKind.sessions,
        CenterReportFilter(sessionId: firstSession.id),
      );
      final sessionRow = Map.fromIterables(
        sessions.columns,
        sessions.rows.single,
      );
      for (final field in [
        'كل الحضور المجاني والإعفاء',
        'توزيع الخصم الثابت للحاضرين',
        'عدد مشتري الباقة في الحصة',
        'التحصيل حسب المبلغ المقبوض',
        'تفصيل الدفع والرصيد السابق',
      ]) {
        expect(sessionRow[field], 'غير متوفر في هذه النسخة', reason: field);
      }
      final categories = report(
        CenterReportKind.closings,
        const CenterReportFilter(closingMode: ClosingReportMode.categories),
      );
      expect(categories.rows, hasLength(1));
      final placeholder = Map.fromIterables(
        categories.columns,
        categories.rows.single,
      );
      expect(placeholder['عدد الطلبة داخل الفئة'], isNull);
      expect(placeholder['عدد عمليات الدفع'], isNull);
      expect(placeholder['نسبة الخصم ٪'], isNull);
      expect(
        placeholder['فئة الطلبة'],
        'تفاصيل الفئات غير محفوظة لهذه التقفيلة',
      );
      final path = '${directory.path}/legacy-categories.csv';
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.closings,
        filter: const CenterReportFilter(
          closingMode: ClosingReportMode.categories,
        ),
        destination: path,
      );
      final csv = await File(path).readAsString();
      expect(csv, contains('تفاصيل الفئات غير محفوظة لهذه التقفيلة'));
      expect(csv, isNot(contains('إعفاء 100٪')));
    },
  );

  test(
    'exam status and inclusive score filters preserve absent, unrecorded and real zero distinctions in export',
    () async {
      final notTaken = report(
        CenterReportKind.exams,
        CenterReportFilter(
          sessionId: firstSession.id,
          examStatus: ExamReportStatus.notTaken,
        ),
      );
      expect(notTaken.rows, hasLength(2));
      expect(notTaken.summary['غائب عن الامتحان'], '1');
      expect(notTaken.summary['لم تُرصد'], '1');
      expect(notTaken.rows.any((row) => row.contains(ahmed.code)), isFalse);
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(
            sessionId: firstSession.id,
            examStatus: ExamReportStatus.unrecorded,
          ),
        ).rows.single,
        contains(absent.code),
      );
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(
            sessionId: firstSession.id,
            examStatus: ExamReportStatus.absent,
          ),
        ).rows.single,
        contains(mina.code),
      );
      final zeroFilter = CenterReportFilter(
        sessionId: firstSession.id,
        exactScore: 0,
      );
      expect(
        report(CenterReportKind.exams, zeroFilter).rows.single,
        contains(ahmed.code),
      );
      expect(
        report(
          CenterReportKind.exams,
          const CenterReportFilter(minScore: 0, maxScore: 5),
        ).rows,
        hasLength(2),
      );
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(
            sessionId: firstSession.id,
            examStatus: ExamReportStatus.absent,
            exactScore: 0,
          ),
        ).rows,
        isEmpty,
      );
      for (final invalid in [
        const CenterReportFilter(minScore: -1),
        const CenterReportFilter(minScore: 5, maxScore: 0),
        const CenterReportFilter(exactScore: 0, minScore: 1),
      ]) {
        expect(
          () => report(CenterReportKind.exams, invalid),
          throwsA(isA<CenterException>()),
        );
      }
      final homework = report(
        CenterReportKind.homework,
        CenterReportFilter(
          sessionId: firstSession.id,
          homeworkStatus: HomeworkStatus.missing,
        ),
      );
      expect(homework.rows.single, contains(mina.code));
      expect(homework.summary['لم يعمل'], '1');
      expect(homework.summary['كامل'], '0');
      final path = '${directory.path}/zero-only.csv';
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.exams,
        filter: zeroFilter,
        destination: path,
      );
      final csv = await File(path).readAsString();
      expect(csv, contains('"001"'));
      expect(csv, isNot(contains('"002"')));
      expect(csv, isNot(contains('"003"')));
      expect(csv.split('\r\n').where((row) => row.isNotEmpty), hasLength(2));
    },
  );

  test(
    'session report exposes saved attendance discounts and amount buckets after current profiles change',
    () async {
      await store.saveStudent(ahmed.copyWith(discountPercent: 100));
      final sessions = report(
        CenterReportKind.sessions,
        CenterReportFilter(sessionId: firstSession.id),
      );
      final row = Map.fromIterables(sessions.columns, sessions.rows.single);
      expect(row['توزيع الخصم الثابت للحاضرين'], contains('25'));
      expect(row['توزيع الخصم الثابت للحاضرين'], isNot(contains('100')));
      expect(row['عدد مشتري الباقة في الحصة'], 2);
      expect(row['التحصيل حسب المبلغ المقبوض'], contains('300.38'));
      expect(row['التحصيل حسب المبلغ المقبوض'], contains('320.40'));
      expect(row['كل الحضور المجاني والإعفاء'], 0);
      await store.closeSession(secondSession.id);
      final makeup = report(
        CenterReportKind.sessions,
        CenterReportFilter(sessionId: secondSession.id),
      );
      final second = Map.fromIterables(makeup.columns, makeup.rows.single);
      expect(second['كل الحضور المجاني والإعفاء'], 0);
      expect(second['معوّض'], 1);
      expect(second['التحصيل حسب المبلغ المقبوض'], '0 عملية');
      expect(second['توزيع الخصم الثابت للحاضرين'], contains('20'));
      await store.saveStaff(
        name: 'مساعد التقارير',
        password: 'test-password-2026',
        role: StaffRole.assistant,
      );
      store.signOut();
      await store.signIn('مساعد التقارير', 'test-password-2026');
      final readonly = report(CenterReportKind.sessions);
      expect(readonly.columns, isNot(contains('التحصيل حسب المبلغ المقبوض')));
      expect(readonly.columns, contains('توزيع الخصم الثابت للحاضرين'));
    },
  );

  test(
    'financial report and export permissions agree for assistant, cashier and signed out staff',
    () async {
      await store.saveStaff(
        name: 'مساعد',
        password: 'test-password-2026',
        role: StaffRole.assistant,
      );
      await store.saveStaff(
        name: 'استقبال',
        password: 'test-password-2026',
        role: StaffRole.cashier,
      );
      store.signOut();
      await store.signIn('مساعد', 'test-password-2026');
      expect(report(CenterReportKind.attendance).rows, isNotEmpty);
      for (final kind in [
        CenterReportKind.payments,
        CenterReportKind.reviews,
        CenterReportKind.closings,
      ]) {
        expect(() => report(kind), throwsA(isA<CenterException>()));
        final destination = '${directory.path}/${kind.name}.csv';
        await expectLater(
          CenterReports.exportCsv(
            store: store,
            kind: kind,
            filter: const CenterReportFilter(),
            destination: destination,
          ),
          throwsA(isA<CenterException>()),
        );
        expect(await File(destination).exists(), isFalse);
      }
      store.signOut();
      await store.signIn('استقبال', 'test-password-2026');
      expect(report(CenterReportKind.payments).rows, hasLength(4));
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.payments,
        filter: const CenterReportFilter(),
        destination: '${directory.path}/cashier.csv',
      );
      store.signOut();
      expect(
        () => report(CenterReportKind.students),
        throwsA(isA<CenterException>()),
      );
    },
  );

  for (final (count, configuredPrice) in [(2, 17025), (3, 26050)]) {
    test(
      '$count-session package report and CSV preserve purchased quantity and independent price after group prices change',
      () async {
        await store.saveGroup(
          secondGroup.copyWith(
            twoSessionPrice: 17025,
            threeSessionPrice: 26050,
          ),
        );
        await store.collectAndAttend(
          EntryRequest(
            studentId: inactive.id,
            sessionId: secondSession.id,
            mode: EntryMode.package,
            packageSessions: count,
          ),
        );
        await store.saveGroup(
          store.groups.last.copyWith(
            twoSessionPrice: 99000,
            threeSessionPrice: 98000,
          ),
        );
        final packages = report(
          CenterReportKind.packages,
          CenterReportFilter(studentId: inactive.id),
        );
        final row = studentRow(packages, inactive.code);
        expect(row['الحصص الأصلية'], count);
        expect(row['المتبقي حاليًا'], count - 1);
        expect(row['المستهلك'], 1);
        expect(
          row['المحصل حتى الآن (جنيه مصري)'],
          reportAmount(configuredPrice),
        );
        final groups = report(CenterReportKind.groups);
        final groupRow = Map.fromIterables(
          groups.columns,
          groups.rows.firstWhere((row) => row.first == secondGroup.name),
        );
        final plans = groupRow['الأشهر المتاحة حاليًا — الاسم / الحصص / السعر'];
        for (final plan
            in store.groups
                .singleWhere((g) => g.id == secondGroup.id)
                .effectiveMonthPlans) {
          expect(
            plans,
            contains(
              '${plan.name} — ${plan.sessions} حصص — ${reportAmount(plan.price)} ج',
            ),
          );
        }
        final destination = '${directory.path}/package-$count.csv';
        await CenterReports.exportCsv(
          store: store,
          kind: CenterReportKind.packages,
          filter: CenterReportFilter(studentId: inactive.id),
          destination: destination,
        );
        final exported = await File(destination).readAsString();
        expect(
          exported,
          contains('"الحصص الأصلية","المتبقي حاليًا","المستهلك"'),
        );
        expect(exported, contains(reportAmount(configuredPrice)));
        expect(exported, contains('"$count","${count - 1}","1"'));
      },
    );
  }
}
