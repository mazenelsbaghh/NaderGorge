import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group, otherGroup;
  late LessonSession session;
  late Student full, discounted, exempt, prepaid;
  const password = 'comprehensive-fixture-password';
  const ownerName = 'comprehensive-owner';

  Future<Student> addStudent(
    String code,
    String name, {
    int discount = 0,
    String? groupId,
    DateTime? joined,
  }) async {
    await store.saveStudent(
      Student(
        code: code,
        name: name,
        discountPercent: discount,
        groupIds: [groupId ?? group.id],
        createdAt: joined ?? session.startsAt.subtract(const Duration(days: 7)),
      ),
    );
    return store.students.last;
  }

  setUp(() async {
    await initializeDateFormatting('ar_EG');
    directory = await Directory.systemTemp.createTemp(
      'massar-comprehensive-management-',
    );
    store = await CenterStore.open(directory: directory.path);
    final salt = List<int>.generate(24, (index) => index + 1);
    final key = await Pbkdf2(
      macAlgorithm: Hmac.sha256(),
      iterations: 120000,
      bits: 256,
    ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
    await store.ensureInstallationAdmin(
      InstallationAdmin(
        id: 'management-suite-owner',
        name: ownerName,
        credential: {
          'algorithm': 'pbkdf2-sha256-120000',
          'salt': base64Encode(salt),
          'hash': base64Encode(await key.extractBytes()),
        },
      ),
    );
    await store.signIn(ownerName, password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(
        CatalogEntry(name: 'أساسي ${kind.name}', kind: kind),
      );
      await store.saveCatalog(
        CatalogEntry(name: 'آخر ${kind.name}', kind: kind),
      );
    }
    Future<void> addGroup(String name, bool other) => store.saveGroup(
      StudyGroup(
        name: name,
        subjectId: store.catalogs
            .where((entry) => entry.kind == CatalogKind.subject)
            .elementAt(other ? 1 : 0)
            .id,
        centerId: store.catalogs
            .where((entry) => entry.kind == CatalogKind.center)
            .elementAt(other ? 1 : 0)
            .id,
        gradeId: store.catalogs
            .where((entry) => entry.kind == CatalogKind.grade)
            .elementAt(other ? 1 : 0)
            .id,
        sessionPrice: 10103,
        packagePrice: 41307,
        twoSessionPrice: 17103,
        threeSessionPrice: 26705,
      ),
    );
    await addGroup('المجموعة الأصلية', false);
    await addGroup('المجموعة الأخرى', true);
    group = store.groups.first;
    otherGroup = store.groups.last;
    final starts = DateTime.now().add(const Duration(days: 1));
    final month = store.studyMonths.first;
    await store.saveSession(
      LessonSession(
        preparedLessonId: month.lessons.first.id,
        monthNumber: month.number,
        groupId: group.id,
        number: 1,
        startsAt: starts,
        createdAt: DateTime.now(),
      ),
    );
    session = store.sessions.single;
    full = await addStudent('001', '=اسم,"طالب"\nسطر');
    discounted = await addStudent('002', 'طالب بخصم', discount: 25);
    exempt = await addStudent('003', 'طالب معفى', discount: 100);
    prepaid = await addStudent('004', 'طالب برصيد سابق', discount: 50);
    await store.saveCardSettings(const CenterCardSettings(price: 3051));
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  CenterReportData report(
    CenterReportKind kind, [
    CenterReportFilter filter = const CenterReportFilter(),
  ]) => CenterReports.build(store, kind, filter);
  Map<String, Object?> rowMap(CenterReportData table, List<Object?> row) =>
      Map.fromIterables(table.columns, row);
  Map<String, Object?> financialEvidence() => {
    'payments': store.allPayments.map((entry) => entry.toJson()).toList(),
    'packages': store.allPackages.map((entry) => entry.toJson()).toList(),
    'attendance': store.allAttendances.map((entry) => entry.toJson()).toList(),
    'refunds': store.refunds.map((entry) => entry.toJson()).toList(),
    'closings': store.allClosings.map((entry) => entry.toJson()).toList(),
    'cards': store.cardPayments.map((entry) => entry.toJson()).toList(),
    'receipts': store.cardReceipts.map((entry) => entry.toJson()).toList(),
  };
  Future<void> reload() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn(ownerName, password);
  }

  for (final scenario in [
    (
      sessions: 2,
      discount: 25,
      packageNet: 12827,
      cardNet: 2288,
      collectMethod: 'نقدي',
      refundMethod: 'تحويل',
      initial: 25218,
      net: 15115,
      cash: 25218,
    ),
    (
      sessions: 3,
      discount: 50,
      packageNet: 13353,
      cardNet: 1526,
      collectMethod: 'تحويل',
      refundMethod: 'نقدي',
      initial: 24982,
      net: 14879,
      cash: 4776,
    ),
  ]) {
    test(
      'mixed ${scenario.sessions}-class package, card and exemption reconcile after attendance reopen and cross-method refund',
      () async {
        await store.saveStudent(
          discounted.copyWith(discountPercent: scenario.discount),
        );
        await store.renewPackage(
          PackageRequest(studentId: prepaid.id, groupId: group.id, sessions: 3),
        );
        await store.saveStudent(prepaid.copyWith(discountPercent: 0));
        for (final student in [full, exempt]) {
          await store.collectAndAttend(
            EntryRequest(
              studentId: student.id,
              sessionId: session.id,
              mode: EntryMode.single,
              method: student.id == full.id ? scenario.collectMethod : 'نقدي',
            ),
          );
        }
        await store.collectAndAttend(
          EntryRequest(
            studentId: discounted.id,
            sessionId: session.id,
            mode: EntryMode.package,
            packageSessions: scenario.sessions,
          ),
        );
        await store.collectAndAttend(
          EntryRequest(
            studentId: prepaid.id,
            sessionId: session.id,
            mode: EntryMode.package,
          ),
        );
        await store.collectStudentCard(
          studentId: discounted.id,
          sessionId: session.id,
        );
        await store.receiveStudentCard(discounted.id);
        final paidFull = store.payments.singleWhere(
          (payment) => payment.studentId == full.id,
        );
        await store.checkPayment(
          studentId: full.id,
          sessionId: session.id,
          expectedAmount: paidFull.collectedAmount,
        );
        await store.savePaymentReview(
          ReviewRequest(
            studentId: full.id,
            paymentId: paidFull.id,
            paperAmount: 10103,
          ),
        );
        final fullEntry = store.attendances.singleWhere(
          (attendance) => attendance.studentId == full.id,
        );
        final checkSnapshot = store.paymentChecks.single.toJson();
        final reviewSnapshot = store.reviews.single.toJson();
        await store.closeSession(session.id);
        final originalSummary = store.sessionFinancialSummary(session.id);
        expect(originalSummary.totalCollected, scenario.initial);
        expect(originalSummary.cardCollectedAmount, scenario.cardNet);
        expect(originalSummary.allFreeCount, 1);
        expect(originalSummary.packageBuyerCount, 1);
        expect(
          originalSummary.studentCategories!
              .singleWhere(
                (category) =>
                    category.kind == SessionStudentCategoryKind.prepaid,
              )
              .discountPercent,
          50,
        );
        await store.finalizeSession(
          sessionId: session.id,
          actualCash: originalSummary.expectedCash + 13,
        );
        final historicalClosing = store.closings.single.toJson();
        await store.reopenSession(session.id);
        await store.reverseEntry(
          attendanceId: fullEntry.id,
          reason: 'تصحيح حضور خاطئ',
          refundMethod: scenario.refundMethod,
        );
        await store.closeSession(session.id);
        final replacement = store.sessionFinancialSummary(session.id);
        expect(replacement.totalCollected, scenario.net);
        expect(replacement.expectedCash, scenario.cash);
        expect(replacement.refundAmount, 10103);
        expect(replacement.presentCount, 3);
        expect(replacement.absentCount, 1);
        expect(replacement.allFreeCount, 1);
        await store.finalizeSession(
          sessionId: session.id,
          actualCash: scenario.cash + 7,
        );
        await store.saveGroup(
          group.copyWith(
            sessionPrice: 90000,
            twoSessionPrice: 99000,
            threeSessionPrice: 98000,
          ),
        );
        await store.saveStudent(discounted.copyWith(discountPercent: 0));
        await store.saveCardSettings(const CenterCardSettings(price: 9900));
        await reload();
        expect(store.allClosings.first.toJson(), historicalClosing);
        expect(store.paymentChecks.single.toJson(), checkSnapshot);
        expect(store.reviews.single.toJson(), reviewSnapshot);
        expect(
          store.paymentStatusFor(full.id, session.id).status,
          StudentPaymentStatus.notPaid,
        );
        expect(store.cardPayments.single.netAmount, scenario.cardNet);
        final movements = report(
          CenterReportKind.payments,
          CenterReportFilter(sessionId: session.id),
        );
        expect(
          movements.summary['التحصيل'],
          '${reportAmount(scenario.initial)} ج',
        );
        expect(movements.summary['الاستردادات'], '101.03 ج');
        expect(
          movements.summary['الصافي بعد الاسترداد'],
          '${reportAmount(scenario.net)} ج',
        );
        expect(
          movements.summary['صافي النقدي'],
          '${reportAmount(scenario.cash)} ج',
        );
        final categories = report(
          CenterReportKind.closings,
          CenterReportFilter(
            sessionId: session.id,
            closingMode: ClosingReportMode.categories,
          ),
        );
        expect(categories.summary['التقفيلات السارية'], '1');
        expect(
          categories.summary['التحصيل المثبت'],
          '${reportAmount(scenario.net)} ج',
        );
        expect(categories.summary['فرق النقدي'], '0.07 ج');
        final activeRows = categories.rows.where(
          (row) => rowMap(categories, row)['حالة التقفيلة'] == 'سارية',
        );
        final prepaidCategory = rowMap(
          categories,
          activeRows.singleWhere(
            (row) => rowMap(
              categories,
              row,
            )['فئة الطلبة'].toString().startsWith('حضور بباقة سابقة'),
          ),
        );
        expect(prepaidCategory['نسبة الخصم ٪'], 50);
        expect(prepaidCategory['عدد عمليات الدفع'], 0);
        final packageRows = report(
          CenterReportKind.packages,
          CenterReportFilter(sessionId: session.id),
        );
        final purchased = rowMap(
          packageRows,
          packageRows.rows.singleWhere((row) => row.first == discounted.code),
        );
        expect(purchased['الحصص الأصلية'], scenario.sessions);
        expect(purchased['المتبقي حاليًا'], scenario.sessions - 1);
        expect(
          purchased['المحصل حتى الآن (جنيه مصري)'],
          reportAmount(scenario.packageNet),
        );
        final attendance = report(
          CenterReportKind.attendance,
          CenterReportFilter(sessionId: session.id),
        );
        expect(attendance.summary['حضور'], '3');
        expect(attendance.summary['غياب'], '1');
        final refundsFilter = CenterReportFilter(
          sessionId: session.id,
          studentId: full.id,
          paymentMode: PaymentReportMode.refunds,
        );
        final destination = '${directory.path}/refund-${scenario.sessions}.csv';
        await CenterReports.exportCsv(
          store: store,
          kind: CenterReportKind.payments,
          filter: refundsFilter,
          destination: destination,
        );
        final csv = await File(destination).readAsString();
        expect(csv, contains('"-101.03"'));
        expect(csv, contains(scenario.refundMethod));
        expect(csv, isNot(contains('تحصيل كارت')));
        expect(store.cardReceipts.single.paymentBypassed, isFalse);
      },
    );
  }

  test(
    'academic filters and CSV combine historical enrollment, catalog, date and independent activity after a group move',
    () async {
      final absent = await addStudent('005', 'غائب عن الامتحان');
      final late = await addStudent(
        '006',
        'التحق بعد الحصة',
        joined: session.startsAt.add(const Duration(hours: 1)),
      );
      final foreign = await addStudent(
        '007',
        'طالب مجموعة أخرى',
        groupId: otherGroup.id,
      );
      for (final student in [full, discounted, absent, exempt, prepaid]) {
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
      }
      final exam = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          kind: AcademicActivityKind.exam,
          name: 'امتحان "الحركة"\nالنهائي',
          maxScore: 25,
          createdAt: DateTime.now(),
        ),
      );
      final homework = await store.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: session.preparedLessonId,
          kind: AcademicActivityKind.homework,
          name: 'واجب الحركة',
          createdAt: DateTime.now(),
        ),
      );
      for (final (student, score, absentExam) in [
        (full, 0, false),
        (discounted, 25, false),
        (absent, null, true),
      ]) {
        await store.saveAcademic(
          AcademicRecord(
            studentId: student.id,
            sessionId: session.id,
            activityId: exam.id,
            score: score,
            examAbsent: absentExam,
            maxScore: 25,
            updatedAt: DateTime.now(),
          ),
        );
      }
      await store.saveAcademic(
        AcademicRecord(
          studentId: discounted.id,
          sessionId: session.id,
          activityId: homework.id,
          homework: HomeworkStatus.missing,
          updatedAt: DateTime.now(),
        ),
      );
      await store.saveStudent(full.copyWith(groupIds: [otherGroup.id]));
      for (final scenario in [
        (
          status: ExamReportStatus.all,
          exact: 0,
          minimum: null,
          maximum: null,
          codes: ['001'],
        ),
        (
          status: ExamReportStatus.recorded,
          exact: null,
          minimum: 25,
          maximum: 25,
          codes: ['002'],
        ),
        (
          status: ExamReportStatus.absent,
          exact: null,
          minimum: null,
          maximum: null,
          codes: ['005'],
        ),
        (
          status: ExamReportStatus.unrecorded,
          exact: null,
          minimum: null,
          maximum: null,
          codes: ['003', '004'],
        ),
        (
          status: ExamReportStatus.notTaken,
          exact: null,
          minimum: null,
          maximum: null,
          codes: ['003', '004', '005'],
        ),
      ]) {
        final filter = CenterReportFilter(
          groupId: group.id,
          subjectId: group.subjectId,
          centerId: group.centerId,
          gradeId: group.gradeId,
          sessionId: session.id,
          activityId: exam.id,
          from: session.startsAt,
          until: session.startsAt,
          examStatus: scenario.status,
          exactScore: scenario.exact,
          minScore: scenario.minimum,
          maxScore: scenario.maximum,
        );
        final table = report(CenterReportKind.exams, filter);
        expect(
          table.rows.map((row) => row.first).toList()..sort(),
          scenario.codes,
        );
        expect(
          table.rows.any(
            (row) => row.first == late.code || row.first == foreign.code,
          ),
          isFalse,
        );
        expect(
          table.summary.values
              .map(int.parse)
              .reduce((first, second) => first + second),
          scenario.codes.length,
        );
        final destination =
            '${directory.path}/exam-${scenario.status.name}-${scenario.exact}.csv';
        await CenterReports.exportCsv(
          store: store,
          kind: CenterReportKind.exams,
          filter: filter,
          destination: destination,
        );
        final exported = _readCsv(await File(destination).readAsString());
        final codeIndex = exported.first.indexOf('الكود');
        expect(
          exported.skip(1).map((row) => row[codeIndex]).toList()..sort(),
          scenario.codes,
        );
        expect(exported.first.last, 'اسم الامتحان');
        expect(exported.skip(1).every((row) => row.last == exam.name), isTrue);
        if (scenario.exact == 0) {
          expect(exported[1][exported.first.indexOf('الدرجة')], '0');
          expect(
            exported[1][exported.first.indexOf('الطالب')],
            "'${full.name}",
          );
        }
      }
      final missing = report(
        CenterReportKind.homework,
        CenterReportFilter(
          activityId: homework.id,
          groupId: group.id,
          sessionId: session.id,
          homeworkStatus: HomeworkStatus.missing,
        ),
      );
      expect(missing.rows.single.first, discounted.code);
      expect(
        report(
          CenterReportKind.exams,
          CenterReportFilter(
            from: session.startsAt.add(const Duration(days: 1)),
          ),
        ).rows,
        isEmpty,
      );
      expect(parseAcademicInteger(' ٠ '), 0);
      expect(parseAcademicInteger('۲۵'), 25);
      final before = financialEvidence();
      for (final invalid in [
        const CenterReportFilter(minScore: 26, maxScore: 25),
        const CenterReportFilter(exactScore: 0, minScore: 1),
        const CenterReportFilter(exactScore: -1),
        CenterReportFilter(activityId: homework.id),
        CenterReportFilter(
          activityId: exam.id,
          from: session.startsAt.add(const Duration(days: 1)),
        ),
      ]) {
        final destination = '${directory.path}/invalid-${invalid.hashCode}.csv';
        await expectLater(
          CenterReports.exportCsv(
            store: store,
            kind: CenterReportKind.exams,
            filter: invalid,
            destination: destination,
          ),
          throwsA(isA<CenterException>()),
        );
        expect(await File(destination).exists(), isFalse);
      }
      expect(financialEvidence(), before);
    },
  );

  for (final invalid in [
    (score: -1, max: 25, absent: false),
    (score: 26, max: 25, absent: false),
    (score: 0, max: 25, absent: true),
    (score: 25, max: 24, absent: false),
  ]) {
    test(
      'invalid academic result $invalid after cash finalization cannot mutate grades or financial evidence',
      () async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: full.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.collectStudentCard(
          studentId: full.id,
          sessionId: session.id,
        );
        await store.closeSession(session.id);
        await store.finalizeSession(sessionId: session.id, actualCash: 13154);
        final exam = await store.saveAcademicActivity(
          AcademicActivity(
            preparedLessonId: session.preparedLessonId,
            kind: AcademicActivityKind.exam,
            name: 'رصد بعد التقفيل',
            maxScore: 25,
            createdAt: DateTime.now(),
          ),
        );
        await store.saveAcademic(
          AcademicRecord(
            studentId: full.id,
            sessionId: session.id,
            activityId: exam.id,
            score: 0,
            maxScore: 25,
            updatedAt: DateTime.now(),
          ),
        );
        final originalGrade = store.academics.single.toJson();
        final evidence = financialEvidence();
        final auditCount = store.audit.length;
        await expectLater(
          store.saveAcademic(
            AcademicRecord(
              studentId: full.id,
              sessionId: session.id,
              activityId: exam.id,
              score: invalid.score,
              maxScore: invalid.max,
              examAbsent: invalid.absent,
              updatedAt: DateTime.now(),
            ),
          ),
          throwsA(isA<CenterException>()),
        );
        expect(store.academics.single.toJson(), originalGrade);
        expect(financialEvidence(), evidence);
        expect(store.audit.length, auditCount);
        await reload();
        expect(store.academics.single.toJson(), originalGrade);
        expect(financialEvidence(), evidence);
      },
    );
  }

  for (final switchCase in [
    (kind: CenterReportKind.packages, nextRole: StaffRole.assistant),
    (kind: CenterReportKind.sessions, nextRole: StaffRole.cashier),
    (kind: CenterReportKind.groups, nextRole: StaffRole.admin),
  ]) {
    test(
      'switching employee during ${switchCase.kind.name} CSV export rejects stale content and removes both output and temporary file',
      () async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: discounted.id,
            sessionId: session.id,
            mode: EntryMode.package,
            packageSessions: 2,
          ),
        );
        await store.saveStaff(
          name: 'الموظف التالي',
          password: password,
          role: switchCase.nextRole,
        );
        final evidence = financialEvidence();
        final destination =
            '${directory.path}/employee-switch-${switchCase.kind.name}.csv';
        final written = Completer<void>();
        final resume = Completer<void>();
        final outerZone = Zone.current;
        await IOOverrides.runZoned(
          () async {
            final exporting = CenterReports.exportCsv(
              store: store,
              kind: switchCase.kind,
              filter: const CenterReportFilter(),
              destination: destination,
            );
            final denied = expectLater(
              exporting,
              throwsA(isA<CenterException>()),
            );
            try {
              await written.future.timeout(const Duration(seconds: 5));
              await store.signIn('الموظف التالي', password);
            } finally {
              resume.complete();
            }
            await denied;
          },
          createFile: (filename) {
            final actual = outerZone.run(() => File(filename));
            return filename.startsWith('$destination.') &&
                    filename.endsWith('.tmp')
                ? _PausedCsvFile(actual, written, resume.future)
                : actual;
          },
        );
        expect(await File(destination).exists(), isFalse);
        expect(
          directory.listSync().where(
            (entry) => entry.path.startsWith('$destination.'),
          ),
          isEmpty,
        );
        expect(financialEvidence(), evidence);
      },
    );
  }

  for (final role in [
    StaffRole.admin,
    StaffRole.cashier,
    StaffRole.assistant,
  ]) {
    test(
      '${role.name} package and card financial exports enforce current permissions without hiding public tariffs or balances',
      () async {
        await store.collectAndAttend(
          EntryRequest(
            studentId: discounted.id,
            sessionId: session.id,
            mode: EntryMode.package,
            packageSessions: 2,
          ),
        );
        await store.collectStudentCard(
          studentId: discounted.id,
          sessionId: session.id,
        );
        await store.saveStaff(
          name: 'موظف ${role.name}',
          password: password,
          role: role,
        );
        await store.signIn('موظف ${role.name}', password);
        final financial = role != StaffRole.assistant;
        final packages = report(CenterReportKind.packages);
        expect(packages.rows, hasLength(1));
        final row = rowMap(packages, packages.rows.single);
        expect(row['الحصص الأصلية'], 2);
        expect(row['المتبقي حاليًا'], 1);
        expect(
          packages.columns.contains('المحصل حتى الآن (جنيه مصري)'),
          financial,
        );
        if (financial) expect(row['المحصل حتى الآن (جنيه مصري)'], '128.27');
        final path = '${directory.path}/package-${role.name}.csv';
        await CenterReports.exportCsv(
          store: store,
          kind: CenterReportKind.packages,
          filter: const CenterReportFilter(),
          destination: path,
        );
        final csv = await File(path).readAsString();
        expect(csv.contains('المحصل حتى الآن (جنيه مصري)'), financial);
        expect(csv.contains('128.27'), financial);
        final publicPrices = report(CenterReportKind.groups);
        expect(
          publicPrices.columns,
          contains('الأشهر المتاحة حاليًا — الاسم / الحصص / السعر'),
        );
        expect(publicPrices.rows.first.last, contains('210.00'));
        expect(publicPrices.rows.first, contains('101.03'));
        for (final kind in [
          CenterReportKind.payments,
          CenterReportKind.reviews,
          CenterReportKind.closings,
          CenterReportKind.cards,
        ]) {
          final destination = '${directory.path}/${role.name}-${kind.name}.csv';
          if (financial) {
            await CenterReports.exportCsv(
              store: store,
              kind: kind,
              filter: const CenterReportFilter(),
              destination: destination,
            );
            expect(await File(destination).exists(), isTrue);
          } else {
            expect(() => report(kind), throwsA(isA<CenterException>()));
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
        }
        expect(store.cardPayments.single.netAmount, 2288);
        expect(store.packages.single.remaining, 1);
      },
    );
  }
}

List<List<String>> _readCsv(String text) {
  final source = text.startsWith('\ufeff') ? text.substring(1) : text;
  final rows = <List<String>>[];
  var fields = <String>[];
  var field = StringBuffer();
  var quoted = false;
  for (var index = 0; index < source.length; index++) {
    final character = source[index];
    if (character == '"') {
      if (quoted && index + 1 < source.length && source[index + 1] == '"') {
        field.write('"');
        index++;
      } else {
        quoted = !quoted;
      }
    } else if (!quoted && (character == ',' || character == '\n')) {
      fields.add(field.toString());
      field = StringBuffer();
      if (character == '\n') {
        rows.add(fields);
        fields = <String>[];
      }
    } else if (quoted || character != '\r') {
      field.write(character);
    }
  }
  return rows;
}

// Pause the external file write while retaining real disk contents and cleanup.
class _PausedCsvFile extends Fake implements File {
  _PausedCsvFile(this.actual, this.written, this.resume);
  final File actual;
  final Completer<void> written;
  final Future<void> resume;
  @override
  String get path => actual.path;
  @override
  Future<File> create({bool recursive = false, bool exclusive = false}) async {
    await actual.create(recursive: recursive, exclusive: exclusive);
    written.complete();
    await resume;
    return this;
  }

  @override
  Future<bool> exists() => actual.exists();
  @override
  Future<FileSystemEntity> delete({bool recursive = false}) =>
      actual.delete(recursive: recursive);
  @override
  Future<File> rename(String newPath) => actual.rename(newPath);
}
