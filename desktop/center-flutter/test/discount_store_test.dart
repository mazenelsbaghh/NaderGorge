import 'dart:convert';
import 'dart:io';

import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/application/session_finance.dart';
import 'package:massar_center/domain/discount_calculation.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'fractional-discount-fixture';
  late Directory directory;
  late CenterStore store;
  late Student student;
  late StudyGroup group;
  late LessonSession session;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-discount-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير الاختبار', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'المجموعة',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 12345,
        packagePrice: 35003,
        twoSessionPrice: 23457,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        name: 'الطالب',
        code: 'D1',
        groupIds: [group.id],
        phone: '01011111111',
        guardianPhone: '01022222222',
        notes: 'ملاحظة محفوظة',
        discountPercent: 25,
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
  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير الاختبار', password);
  }

  EntryRequest entry({
    EntryMode mode = EntryMode.single,
    EntryConfirmation? quote,
  }) => EntryRequest(
    studentId: student.id,
    sessionId: session.id,
    mode: mode,
    packageSessions: 2,
    confirmation: quote,
  );

  test(
    'amount-derived percentages retain precision and exact piastres, including integer endpoints',
    () {
      final third = discountPercentForAmount(300, 200);
      expect(third, closeTo(100 / 3, 1e-13));
      expect(third, isNot(33.33));
      expect(discountedAmount(300, third), 200);
      expect(discountedAmount(12345, 25.5), 9197);
      expect(discountPercentForAmount(10000, 7500), allOf(25, isA<int>()));
      expect(discountPercentForAmount(10000, 0), allOf(100, isA<int>()));
      expect(discountPercentForAmount(10000, 10000), allOf(0, isA<int>()));
      // Piastre targets across non-divisible prices must never be approximated in the stored rate.
      for (final base in [1, 101, 300, 12345, 23457]) {
        for (final target in {0, 1, base ~/ 3, base - 1, base}) {
          expect(
            discountedAmount(base, discountPercentForAmount(base, target)),
            target,
          );
        }
      }
      // Historical integer rounding, including a half-piastre, is unchanged.
      expect(discountedAmount(101, 50), 51);
      expect(discountedAmount(12345, 25), 9259);
      expect(discountedAmount(0, 33.333333333333336), 0);
      for (final pair in [(0, 0), (-1, 0), (100, -1), (100, 101)]) {
        expect(
          () => discountPercentForAmount(pair.$1, pair.$2),
          throwsA(isA<CenterException>()),
        );
      }
      for (final invalid in [
        double.nan,
        double.infinity,
        double.negativeInfinity,
        -0.001,
        100.001,
      ]) {
        expect(
          () => discountedAmount(10000, invalid),
          throwsA(isA<CenterException>()),
        );
      }
    },
  );

  test(
    'queued discount update resolves the latest profile and preserves notes, identity and enrollment after restart',
    () async {
      final latestProfile = student.copyWith(
        name: 'الاسم الجديد',
        phone: '01033333333',
        guardianPhone: '01044444444',
      );
      final fractional = discountPercentForAmount(12345, 8000);
      await Future.wait([
        store.saveStudent(latestProfile),
        store.saveStudentNote(studentId: student.id, notes: 'الملاحظة الجديدة'),
        store.saveStudentDiscount(studentId: student.id, percent: fractional),
      ]);
      final updated = store.students.single;
      expect(updated.toJson(), {
        ...latestProfile.toJson(),
        'notes': 'الملاحظة الجديدة',
        'discountPercent': fractional,
      });
      expect(store.audit.last.action, 'student_discount');
      expect(store.audit.last.description, contains('من 25٪'));
      expect(store.audit.last.description, contains('كود D1'));
      await reopen();
      expect(store.students.single.toJson(), updated.toJson());
      expect(store.enrollmentDateFor(student.id, group.id), student.createdAt);
      await store.saveStudentDiscount(studentId: student.id, percent: 25.0);
      expect(store.students.single.discountPercent, isA<int>());
    },
  );

  for (final role in [StaffRole.cashier, StaffRole.assistant]) {
    test(
      '$role cannot change a fixed discount; rejected writes leave data and audit untouched',
      () async {
        await store.saveStaff(
          name: 'موظف الخصم',
          password: password,
          role: role,
        );
        await store.signIn('موظف الخصم', password);
        final before = store.students.single.toJson();
        final audit = store.audit.map((e) => e.toJson()).toList();
        await expectLater(
          store.saveStudentDiscount(
            studentId: student.id,
            percent: 33.333333333333336,
          ),
          throwsA(isA<CenterException>()),
        );
        expect(store.students.single.toJson(), before);
        expect(store.audit.map((e) => e.toJson()).toList(), audit);
        await reopen();
        expect(store.students.single.toJson(), before);
      },
    );
  }

  test(
    'invalid non-finite and out-of-range rates roll back and cannot enter a backup',
    () async {
      final before = store.students.single.toJson();
      final audit = store.audit.map((e) => e.toJson()).toList();
      for (final invalid in [
        double.nan,
        double.infinity,
        double.negativeInfinity,
        -0.01,
        100.01,
      ]) {
        await expectLater(
          store.saveStudentDiscount(studentId: student.id, percent: invalid),
          throwsA(isA<CenterException>()),
        );
        expect(store.students.single.toJson(), before);
        expect(store.audit.map((e) => e.toJson()).toList(), audit);
      }
      // The profile API is another write boundary and must share the same validation.
      await expectLater(
        store.saveStudent(student.copyWith(discountPercent: double.infinity)),
        throwsA(isA<CenterException>()),
      );
      final backup = await store.createBackup(
        destination: '${directory.path}/valid-after-rejections.json',
      );
      expect(
        (jsonDecode(await File(backup).readAsString())
            as Map)['data']['students'][0]['discountPercent'],
        25,
      );
      await reopen();
      expect(store.students.single.toJson(), before);
    },
  );

  test(
    'fractional quoted money, fixed attendance and closing categories survive backup, restore, refund and a new closing',
    () async {
      final fractional = discountPercentForAmount(12345, 8000);
      await store.saveStudentDiscount(
        studentId: student.id,
        percent: fractional,
      );
      final quote = store.entryConfirmationFor(entry());
      expect(quote.discountPercent, fractional);
      expect(quote.netAmount, 8000);
      await store.collectAndAttend(entry(quote: quote));
      expect(store.payments.single.discountPercent, fractional);
      expect(store.payments.single.netAmount, 8000);
      expect(store.attendances.single.fixedDiscountPercent, fractional);
      await store.closeSession(session.id);
      final summary = store.sessionFinancialSummary(session.id);
      expect(summary.totalCollected, 8000);
      expect(summary.discountAmount, 4345);
      expect(summary.studentCategories!.single.discountPercent, fractional);
      expect(
        summary.attendanceDiscountCategories!.single.discountPercent,
        fractional,
      );
      await store.finalizeSession(sessionId: session.id, actualCash: 8000);
      final closedSnapshot = store.closings.single.toJson();
      final backup = await store.createBackup(
        destination: '${directory.path}/fractional.json',
      );
      await store.saveStudentDiscount(studentId: student.id, percent: 50);
      await store.restoreBackup(backup);
      await store.signIn('مدير الاختبار', password);
      expect(store.students.single.discountPercent, fractional);
      expect(store.closings.single.toJson(), closedSnapshot);
      await reopen();
      expect(store.closings.single.toJson(), closedSnapshot);
      final historicalPayment = store.allPayments.single.toJson();
      final attendanceId = store.attendances.single.id;
      await store.reopenSession(session.id);
      await store.saveStudentDiscount(studentId: student.id, percent: 0);
      await store.reverseEntry(
        attendanceId: attendanceId,
        reason: 'تصحيح دخول',
        refundMethod: 'تحويل',
      );
      expect(store.refunds.single.amount, 8000);
      expect(store.allPayments.single.toJson(), historicalPayment);
      expect(store.allClosings.single.toJson(), closedSnapshot);
      await store.closeSession(session.id);
      final replacement = store.sessionFinancialSummary(session.id);
      expect(replacement.refundAmount, 8000);
      expect(replacement.totalCollected, 0);
      expect(replacement.expectedCash, 8000);
      await store.finalizeSession(sessionId: session.id, actualCash: 8000);
      expect(store.allClosings, hasLength(2));
      await reopen();
      expect(store.allClosings.first.toJson(), closedSnapshot);
      expect(store.closings.single.summary.totalCollected, 0);
    },
  );

  test(
    'integer historical closing JSON remains identical after fractional profiles and restart',
    () async {
      await store.collectAndAttend(entry());
      expect(store.payments.single.netAmount, 9259);
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 9259);
      final snapshot = jsonEncode(store.closings.single.toJson());
      expect(
        store.closings.single.summary.studentCategories!.single.discountPercent,
        isA<int>(),
      );
      await store.saveStudentDiscount(studentId: student.id, percent: 25.5);
      await reopen();
      expect(jsonEncode(store.closings.single.toJson()), snapshot);
      final backup = await store.createBackup(
        destination: '${directory.path}/integer-history.json',
      );
      await store.restoreBackup(backup);
      await store.signIn('مدير الاختبار', password);
      expect(jsonEncode(store.closings.single.toJson()), snapshot);
      expect(store.allPayments.single.discountPercent, isA<int>());
    },
  );

  test(
    'fractional change invalidates even an equivalent-money quote before any payment, attendance or audit',
    () async {
      await store.saveGroup(group.copyWith(sessionPrice: 1));
      await store.saveStudentDiscount(studentId: student.id, percent: 25.1);
      final first = store.entryConfirmationFor(entry());
      await store.saveStudentDiscount(studentId: student.id, percent: 25.2);
      final audit = store.audit.map((e) => e.toJson()).toList();
      final fresh = store.entryConfirmationFor(entry());
      expect(fresh.netAmount, first.netAmount);
      expect(fresh, isNot(first));
      await expectLater(
        store.collectAndAttend(entry(quote: first)),
        throwsA(isA<CenterException>()),
      );
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.audit.map((e) => e.toJson()).toList(), audit);
      await store.collectAndAttend(entry(quote: fresh));
      expect(store.payments.single.discountPercent, 25.2);
    },
  );

  test(
    'real SQLite commit failure rolls back discount, audit and confirmation then supports a clean retry',
    () async {
      final before = store.students.single.toJson();
      final quote = store.entryConfirmationFor(entry());
      final audit = store.audit.map((e) => e.toJson()).toList();
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      try {
        await db.execute(
          "CREATE TRIGGER reject_discount BEFORE UPDATE ON state BEGIN SELECT RAISE(ABORT, 'blocked'); END",
        );
        await expectLater(
          store.saveStudentDiscount(studentId: student.id, percent: 25.5),
          throwsA(
            isA<CenterException>().having(
              (e) => e.cause,
              'real SQLite failure',
              isNotNull,
            ),
          ),
        );
        expect(store.students.single.toJson(), before);
        expect(store.audit.map((e) => e.toJson()).toList(), audit);
        expect(store.entryConfirmationFor(entry()), quote);
        await db.execute('DROP TRIGGER reject_discount');
      } finally {
        await db.close();
      }
      await store.saveStudentDiscount(studentId: student.id, percent: 25.5);
      await reopen();
      expect(store.students.single.discountPercent, 25.5);
    },
  );

  test(
    'equal integer and decimal percentage values share price lines and category counts without changing old integer labels',
    () {
      final summary = buildSessionFinancialSummary(
        session: session,
        attendances: [],
        payments: [
          for (final percent in <num>[25, 25.0])
            PaymentRecord(
              id: '$percent',
              studentId: 'student-$percent',
              groupId: group.id,
              sessionId: session.id,
              baseAmount: 12345,
              discountPercent: percent,
              netAmount: 9259,
              method: 'نقدي',
              description: 'دفع بالحصة',
              staffId: store.currentUser!.id,
              createdAt: DateTime.now(),
            ),
        ],
      );
      expect(summary.lines.single.count, 2);
      expect(summary.lines.single.label, contains('خصم 25٪'));
      expect(summary.studentCategories!.single.studentCount, 2);
      expect(summary.studentCategories!.single.discountPercent, isA<int>());
      expect(summary.totalCollected, 18518);
      final card = StudentCardPayment(
        id: 'card',
        studentId: student.id,
        baseAmount: 12345,
        discountPercent: 25.5,
        netAmount: 9197,
        createdAt: DateTime.now(),
        staffId: store.currentUser!.id,
      );
      expect(
        StudentCardPayment.fromJson(card.toJson()).toJson(),
        card.toJson(),
      );
    },
  );

  test(
    'fractional card collection remains an independent rounded snapshot in the financial closing after restart',
    () async {
      final admin = store.currentUser!;
      final salt = List<int>.generate(24, (index) => index + 1);
      final key = await Pbkdf2(
        macAlgorithm: Hmac.sha256(),
        iterations: 120000,
        bits: 256,
      ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
      await store.ensureInstallationAdmin(
        InstallationAdmin(
          id: admin.id,
          name: admin.name,
          credential: {
            'algorithm': 'pbkdf2-sha256-120000',
            'salt': base64Encode(salt),
            'hash': base64Encode(await key.extractBytes()),
          },
        ),
      );
      await store.signIn(admin.name, password);
      await store.saveCardSettings(const CenterCardSettings(price: 10103));
      final third = discountPercentForAmount(300, 200);
      await store.saveStudentDiscount(studentId: student.id, percent: third);
      await store.collectStudentCard(
        studentId: student.id,
        sessionId: session.id,
      );
      expect(store.cardPayments.single.discountPercent, third);
      expect(store.cardPayments.single.netAmount, 6735);
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      await store.collectAndAttend(entry());
      expect(store.payments.single.netAmount, 8230);
      await store.closeSession(session.id);
      final summary = store.sessionFinancialSummary(session.id);
      expect(summary.cardCollectedAmount, 6735);
      expect(summary.totalCollected, 14965);
      await store.finalizeSession(sessionId: session.id, actualCash: 14965);
      final closing = store.closings.single.toJson();
      final card = store.cardPayments.single.toJson();
      await store.saveStudentDiscount(studentId: student.id, percent: 0);
      await reopen();
      expect(store.cardPayments.single.toJson(), card);
      expect(store.closings.single.toJson(), closing);
    },
  );

  test(
    'fractional independent package purchase and report preserve the original rate after current discount changes',
    () async {
      await store.saveStudentDiscount(studentId: student.id, percent: 25.5);
      await store.collectAndAttend(entry(mode: EntryMode.package));
      expect(store.payments.single.netAmount, 17475);
      expect(store.payments.single.discountPercent, 25.5);
      expect(store.packages.single.remaining, 1);
      await store.saveStudentDiscount(studentId: student.id, percent: 100);
      await store.closeSession(session.id);
      final summary = store.sessionFinancialSummary(session.id);
      expect(
        summary.studentCategories!
            .where((e) => e.kind == SessionStudentCategoryKind.package)
            .single
            .discountPercent,
        25.5,
      );
      expect(
        summary.attendanceDiscountCategories!.single.discountPercent,
        25.5,
      );
      final payments = CenterReports.build(
        store,
        CenterReportKind.payments,
        CenterReportFilter(sessionId: session.id),
      );
      final row = Map.fromIterables(payments.columns, payments.rows.single);
      expect(row['الخصم ٪'], 25.5);
      await store.finalizeSession(sessionId: session.id, actualCash: 17475);
      await reopen();
      expect(store.closings.single.summary.totalCollected, 17475);
    },
  );
}
