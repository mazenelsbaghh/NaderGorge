import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  late LessonSession session;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp(
      'massar-online-allocation-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', 'test-pass-123');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 6000,
        packagePrice: 21000,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        name: 'طالبة',
        code: 'P1',
        groupIds: [group.id],
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    student = store.students.single;
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: DateTime.now().add(const Duration(hours: 1)),
        createdAt: DateTime.now(),
      ),
    );
    session = store.sessions.single;
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<PaymentRecord> purchase({int? paidAmount}) async {
    await store.collectAndAttend(
      EntryRequest(
        studentId: student.id,
        sessionId: session.id,
        mode: EntryMode.package,
        paidAmount: paidAmount,
      ),
    );
    return store.payments.single;
  }

  Future<void> allocate(
    String paymentId, {
    int amount = 6000,
    int expected = 0,
    String reason = 'تعويض الحصة الثانية أونلاين',
  }) => store.allocatePackageToOnline(
    paymentId: paymentId,
    amount: amount,
    expectedOnlineAmount: expected,
    reason: reason,
  );

  String evidence() => jsonEncode({
    'payments': store.allPayments.map((e) => e.toJson()).toList(),
    'packages': store.allPackages.map((e) => e.toJson()).toList(),
    'attendance': store.allAttendances.map((e) => e.toJson()).toList(),
    'corrections': store.corrections.map((e) => e.toJson()).toList(),
    'refunds': store.refunds.map((e) => e.toJson()).toList(),
    'closings': store.allClosings.map((e) => e.toJson()).toList(),
    'audit': store.audit.map((e) => e.toJson()).toList(),
  });

  test(
    'online compensation splits a closed 210 receipt without changing cash, review or package coverage',
    () async {
      // Regression: a later 210 cancellation and 150 collection distorts closing.
      final payment = await purchase();
      await store.checkPayment(
        studentId: student.id,
        sessionId: session.id,
        expectedAmount: 21000,
      );
      await store.savePaymentReview(
        ReviewRequest(
          studentId: student.id,
          paymentId: payment.id,
          paperAmount: 21000,
        ),
      );
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 21000);
      final receipt = jsonEncode(payment.toJson());
      final closing = jsonEncode(store.closings.single.toJson());
      final attendance = jsonEncode(store.allAttendances.single.toJson());
      final package = jsonEncode(store.packages.single.toJson());
      final review = jsonEncode(store.reviews.single.toJson());
      final paymentCheck = jsonEncode(store.paymentChecks.single.toJson());

      await allocate(payment.id);

      expect(store.centerAllocatedAmount(payment.id), 15000);
      expect(store.onlineAllocatedAmount(payment.id), 6000);
      expect(jsonEncode(store.allPayments.single.toJson()), receipt);
      expect(jsonEncode(store.closings.single.toJson()), closing);
      expect(jsonEncode(store.allAttendances.single.toJson()), attendance);
      expect(jsonEncode(store.packages.single.toJson()), package);
      expect(jsonEncode(store.reviews.single.toJson()), review);
      expect(jsonEncode(store.paymentChecks.single.toJson()), paymentCheck);
      expect(store.sessionFinancialSummary(session.id).totalCollected, 21000);
      expect(store.sessionFinancialSummary(session.id).expectedCash, 21000);
      expect(store.refunds, isEmpty);
      final correction = store.corrections.single;
      expect(correction.action, CorrectionAction.packageOnlineAllocation);
      expect(correction.onlineAmount, 6000);
      expect(correction.paymentId, payment.id);
      expect(correction.packageId, payment.packageId);
      expect(correction.studentId, student.id);
      expect(correction.staffId, store.currentUser!.id);
      expect(correction.reason, 'تعويض الحصة الثانية أونلاين');
      expect(store.audit.last.description, contains(correction.reason));
      final packages = CenterReports.build(
        store,
        CenterReportKind.packages,
        const CenterReportFilter(),
      );
      expect(
        packages.rows.single[packages.columns.indexOf(
          'المخصص حاليًا للسنتر (جنيه مصري)',
        )],
        '150.00',
      );
      expect(
        packages.rows.single[packages.columns.indexOf(
          'المحوّل للأونلاين (جنيه مصري)',
        )],
        '60.00',
      );
      final payments = CenterReports.build(
        store,
        CenterReportKind.payments,
        const CenterReportFilter(),
      );
      expect(payments.summary['التحصيل'], '210.00 ج');
      expect(payments.summary['الاستردادات'], '0.00 ج');
      expect(
        payments.rows.single[payments.columns.indexOf('الحالة الحالية')],
        contains('أونلاين 60'),
      );
    },
  );

  test(
    'additional allocations require the current total and cannot exceed the original receipt',
    () async {
      final payment = await purchase();
      await allocate(payment.id);
      final saved = evidence();
      for (final request in [
        (amount: 6000, expected: 0), // Double-click or a stale second device.
        (amount: 1, expected: 5999),
        (amount: 0, expected: 6000),
        (amount: -1, expected: 6000),
        (amount: 15001, expected: 6000),
      ]) {
        await expectLater(
          allocate(
            payment.id,
            amount: request.amount,
            expected: request.expected,
          ),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), saved);
      }
      await allocate(payment.id, amount: 15000, expected: 6000);
      expect(store.onlineAllocatedAmount(payment.id), 21000);
      expect(store.centerAllocatedAmount(payment.id), 0);
      expect(store.allPayments, hasLength(1));
      expect(store.refunds, isEmpty);
      expect(store.corrections.map((e) => e.onlineAmount), [6000, 15000]);
    },
  );

  for (final paid in [0, 15000]) {
    test(
      'a package paid $paid of 21000 cannot allocate unpaid money online',
      () async {
        final payment = await purchase(paidAmount: paid);
        final saved = evidence();
        await expectLater(
          allocate(payment.id),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), saved);
        expect(store.onlineAllocatedAmount(payment.id), 0);
      },
    );
  }

  test(
    'single payments and already refunded package payments cannot become online allocations',
    () async {
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      final single = store.payments.single;
      await expectLater(
        allocate(single.id, amount: 1000),
        throwsA(isA<CenterException>()),
      );
      await store.renewPackage(
        PackageRequest(studentId: student.id, groupId: group.id),
      );
      final package = store.packages.single;
      await store.refundPackage(packageId: package.id, reason: 'شراء خطأ');
      final saved = evidence();
      await expectLater(
        allocate(package.paymentId),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), saved);
    },
  );

  test(
    'allocated receipts cannot be refunded or voided through entry and payment correction paths',
    () async {
      final payment = await purchase();
      await allocate(payment.id);
      final entry = store.attendances.single;
      final saved = evidence();
      for (final mode in RecordCancellationMode.values) {
        await expectLater(
          store.cancelPayment(
            paymentId: payment.id,
            reason: 'إلغاء',
            mode: mode,
          ),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), saved);
      }
      await expectLater(
        store.correctEntry(
          attendanceId: entry.id,
          mode: EntryMode.single,
          reason: 'تحويل لحصة',
        ),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), saved);
      await expectLater(
        store.cancelAttendance(
          attendanceId: entry.id,
          reason: 'إلغاء شامل',
          mode: RecordCancellationMode.recordAndRelated,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), saved);

      // Correcting attendance alone must not become a refund of online money.
      await store.cancelAttendance(attendanceId: entry.id, reason: 'لم تحضر');
      expect(store.attendances, isEmpty);
      expect(store.packages.single.remaining, 4);
      expect(store.onlineAllocatedAmount(payment.id), 6000);
      expect(store.payments.single.collectedAmount, 21000);
      expect(store.refunds, isEmpty);
      final afterAttendance = evidence();
      await expectLater(
        store.refundPackage(
          packageId: payment.packageId!,
          reason: 'استرداد بعد تصحيح الحضور',
        ),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), afterAttendance);
    },
  );

  test(
    'cashiers can allocate with a reason while assistants and signed-out sessions cannot write',
    () async {
      final payment = await purchase();
      await store.saveStaff(
        name: 'استقبال',
        password: 'cashier-pass',
        role: StaffRole.cashier,
      );
      await store.saveStaff(
        name: 'مساعد',
        password: 'assistant-pass',
        role: StaffRole.assistant,
      );
      final saved = evidence();
      await expectLater(
        allocate(payment.id, reason: '  '),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), saved);
      store.signOut();
      await expectLater(allocate(payment.id), throwsA(isA<CenterException>()));
      expect(evidence(), saved);
      await store.signIn('مساعد', 'assistant-pass');
      await expectLater(allocate(payment.id), throwsA(isA<CenterException>()));
      expect(evidence(), saved);
      store.signOut();
      await store.signIn('استقبال', 'cashier-pass');
      await allocate(payment.id);
      expect(store.corrections.single.staffId, store.currentUser!.id);
      expect(store.onlineAllocatedAmount(payment.id), 6000);
    },
  );

  test(
    'allocation survives SQLite reopen and backup restore while malformed allocation evidence is rejected atomically',
    () async {
      final payment = await purchase();
      await allocate(payment.id);
      await store.saveStudent(
        Student(
          name: 'طالبة أخرى',
          code: 'P2',
          groupIds: [group.id],
          createdAt: DateTime.now().subtract(const Duration(days: 1)),
        ),
      );
      final otherStudent = store.students.firstWhere((e) => e.code == 'P2');
      await store.renewPackage(
        PackageRequest(studentId: otherStudent.id, groupId: group.id),
      );
      final otherPackage = store.packages.firstWhere(
        (e) => e.studentId == otherStudent.id,
      );
      final backup = await store.createBackup();
      final data =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.onlineAllocatedAmount(payment.id), 6000);
      expect(store.centerAllocatedAmount(payment.id), 15000);
      await allocate(payment.id, amount: 1000, expected: 6000);
      await store.restoreBackup(backup);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.onlineAllocatedAmount(payment.id), 6000);
      expect(store.corrections, hasLength(1));
      final saved = evidence();

      for (final mutation in [
        'over-cap',
        'student',
        'package',
        'action',
        'void-payment',
        'void-package',
        'attendance',
        'schema-downgrade',
      ]) {
        final forged = jsonDecode(jsonEncode(data)) as Map<String, dynamic>;
        final correction =
            ((forged['data'] as Map)['corrections'] as List).single as Map;
        switch (mutation) {
          case 'over-cap':
            correction['onlineAmount'] = 21001;
          case 'student':
            correction['studentId'] = otherStudent.id;
          case 'package':
            correction['packageId'] = otherPackage.id;
          case 'action':
            correction['action'] = CorrectionAction.paymentMethod.name;
            correction['packageId'] = null;
            correction['oldMethod'] = 'نقدي';
            correction['newMethod'] = 'تحويل';
          case 'void-payment':
            correction['voidsPayment'] = true;
          case 'void-package':
            correction['voidsPackage'] = true;
          case 'attendance':
            correction['attendanceId'] = store.attendances.single.id;
          case 'schema-downgrade':
            (forged['data'] as Map)['schemaVersion'] = 15;
        }
        final file = File('${directory.path}/forged-$mutation.json');
        await file.writeAsString(jsonEncode(forged));
        await expectLater(
          store.restoreBackup(file.path),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), saved);
        expect(store.onlineAllocatedAmount(payment.id), 6000);
      }
    },
  );

  test(
    'a SQLite commit failure rolls back the allocation and audit together',
    () async {
      final payment = await purchase();
      final saved = evidence();
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      try {
        await db.execute(
          "CREATE TRIGGER reject_online_allocation BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'blocked'); END",
        );
        await expectLater(
          allocate(payment.id),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), saved);
        expect(store.onlineAllocatedAmount(payment.id), 0);
        await db.execute('DROP TRIGGER reject_online_allocation');
      } finally {
        await db.close();
      }
      await allocate(payment.id);
      expect(store.centerAllocatedAmount(payment.id), 15000);
    },
  );
}
