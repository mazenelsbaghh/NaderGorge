import 'dart:convert';
import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
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
    directory = await Directory.systemTemp.createTemp('massar-corrections-');
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
        sessionPrice: 10000,
        packagePrice: 40000,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        name: 'طالب',
        code: 'S1',
        groupIds: [group.id],
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
  Future<void> enter(
    EntryMode mode, {
    String method = 'نقدي',
    String? original,
  }) => store.collectAndAttend(
    EntryRequest(
      studentId: student.id,
      sessionId: store.sessions.last.id,
      mode: mode,
      method: method,
      originalAttendanceId: original,
    ),
  );

  test(
    'reverse single preserves evidence, pays full discounted refund and permits re-entry once',
    () async {
      await enter(EntryMode.single);
      final original = store.attendances.single;
      final payment = store.payments.single;
      await store.checkPayment(studentId: student.id, sessionId: session.id);
      await store.savePaymentReview(
        ReviewRequest(
          studentId: student.id,
          paymentId: payment.id,
          paperAmount: 7500,
        ),
      );
      await store.reverseEntry(
        attendanceId: original.id,
        reason: 'الكود الخطأ',
      );
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      expect(store.allAttendances.single.id, original.id);
      expect(store.allPayments.single.id, payment.id);
      expect(store.refunds.single.amount, 7500);
      expect(store.reviews.single.expectedAmount, 7500);
      await expectLater(
        store.savePaymentReview(
          ReviewRequest(
            studentId: student.id,
            paymentId: payment.id,
            paperAmount: 7500,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(
        store.paymentChecks.single.status,
        StudentPaymentStatus.paidSingle,
      );
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
      expect(store.sessionFinancialSummary(session.id).totalCollected, 0);
      expect(store.sessionFinancialSummary(session.id).expectedCash, 0);
      await enter(EntryMode.single);
      expect(store.attendances, hasLength(1));
      expect(store.allAttendances, hasLength(2));
      expect(store.allPayments, hasLength(2));
      expect(store.sessionFinancialSummary(session.id).totalCollected, 7500);
      await expectLater(
        store.reverseEntry(attendanceId: original.id, reason: 'تكرار'),
        throwsA(isA<CenterException>()),
      );
      expect(store.refunds, hasLength(1));
    },
  );
  test(
    'atomic single to package and same-session package to single never double-subtract cash',
    () async {
      await enter(EntryMode.single);
      await store.correctEntry(
        attendanceId: store.attendances.single.id,
        mode: EntryMode.package,
        reason: 'دفع الباقة',
      );
      expect(store.refunds.single.amount, 7500);
      expect(store.packages.single.remaining, 3);
      expect(store.sessionFinancialSummary(session.id).totalCollected, 30000);
      await store.correctEntry(
        attendanceId: store.attendances.single.id,
        mode: EntryMode.single,
        reason: 'دفع الحصة',
      );
      expect(store.packages, isEmpty);
      expect(store.allPackages.single.remaining, 4);
      expect(store.refunds.map((e) => e.amount), [7500, 30000]);
      final summary = store.sessionFinancialSummary(session.id);
      expect(summary.totalCollected, 7500);
      expect(summary.expectedCash, 7500);
      expect(summary.refundAmount, 37500);
      expect(summary.lines.fold(0, (sum, e) => sum + e.total), 7500);
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.paidSingle,
      );
    },
  );
  test(
    'invalid replacement rolls back refund, credit, correction and payment together',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: student.id, groupId: group.id),
      );
      await enter(EntryMode.package);
      final id = store.attendances.single.id;
      final audit = store.audit.length;
      await expectLater(
        store.correctEntry(
          attendanceId: id,
          mode: EntryMode.single,
          reason: 'باقة سابقة',
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.packages.single.remaining, 3);
      expect(store.attendances.single.id, id);
      expect(store.corrections, isEmpty);
      expect(store.refunds, isEmpty);
      expect(store.audit.length, audit);
      await store.reverseEntry(attendanceId: id, reason: 'سجل دخول بالغلط');
      expect(store.packages.single.remaining, 4);
      expect(store.payments, hasLength(1));
      expect(store.refunds, isEmpty);
    },
  );
  test(
    'closed package reversal and absence-present correction preserve exactly one debit',
    () async {
      await enter(EntryMode.package);
      await store.closeSession(session.id);
      final present = store.attendances.single;
      await store.reverseEntry(attendanceId: present.id, reason: 'لم يحضر');
      expect(store.attendances.single.status, AttendanceStatus.absent);
      expect(store.packages.single.remaining, 3);
      expect(store.refunds, isEmpty);
      await store.markAbsentPresent(
        attendanceId: store.attendances.single.id,
        reason: 'راجعنا الورق وكان حاضر',
      );
      expect(store.attendances.single.status, AttendanceStatus.present);
      expect(store.packages.single.remaining, 3);
      expect(store.payments, hasLength(1));
      expect(store.allAttendances, hasLength(3));
    },
  );
  test(
    'unpaid closed absence-present correction collects now and reversal records refund',
    () async {
      await store.closeSession(session.id);
      final absence = store.attendances.single;
      expect(store.payments, isEmpty);
      await store.markAbsentPresent(
        attendanceId: absence.id,
        reason: 'الحضور لم يسجل',
        method: 'تحويل',
      );
      expect(store.payments.single.netAmount, 7500);
      expect(store.sessionFinancialSummary(session.id).expectedCash, 0);
      await store.reverseEntry(
        attendanceId: store.attendances.single.id,
        reason: 'الطالب الآخر',
        refundMethod: 'نقدي',
      );
      expect(store.attendances.single.status, AttendanceStatus.absent);
      expect(store.sessionFinancialSummary(session.id).totalCollected, 0);
      expect(store.sessionFinancialSummary(session.id).expectedCash, -7500);
    },
  );
  test(
    'reopen preserves old closing and supports correction then new immutable closing',
    () async {
      await enter(EntryMode.single);
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 7500);
      final old = store.closings.single;
      final snapshot = jsonEncode(old.toJson());
      final entry = store.attendances.single;
      await expectLater(
        store.reverseEntry(attendanceId: entry.id, reason: 'خطأ'),
        throwsA(isA<CenterException>()),
      );
      await store.reopenFinancialClosing(
        closingId: old.id,
        reason: 'مراجعة الورق',
      );
      expect(store.closings, isEmpty);
      expect(store.sessions.single.status, SessionStatus.closed);
      await store.reverseEntry(attendanceId: entry.id, reason: 'دخول بالغلط');
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      expect(store.allClosings, hasLength(2));
      expect(store.closings.single.summary.totalCollected, 0);
      expect(jsonEncode(store.allClosings.first.toJson()), snapshot);
      await expectLater(
        store.reopenFinancialClosing(closingId: old.id, reason: 'تكرار'),
        throwsA(isA<CenterException>()),
      );
    },
  );
  test(
    'method correction changes cash without fake refund or modifying original receipt',
    () async {
      await enter(EntryMode.single);
      final payment = store.payments.single;
      await store.correctPaymentMethod(
        paymentId: payment.id,
        method: 'تحويل',
        reason: 'طريقة غلط',
      );
      expect(store.allPayments.single.method, 'نقدي');
      expect(store.payments.single.method, 'تحويل');
      expect(store.effectivePaymentMethod(payment.id), 'تحويل');
      expect(store.refunds, isEmpty);
      expect(store.sessionFinancialSummary(session.id).totalCollected, 7500);
      expect(store.sessionFinancialSummary(session.id).expectedCash, 0);
      await store.correctPaymentMethod(
        paymentId: payment.id,
        method: 'نقدي',
        reason: 'راجعنا الإيصال',
      );
      expect(store.sessionFinancialSummary(session.id).expectedCash, 7500);
    },
  );
  test(
    'unused package refund is full and used package or makeup dependencies cannot be corrupted',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: student.id, groupId: group.id),
      );
      final unused = store.packages.single;
      await store.refundPackage(packageId: unused.id, reason: 'دفع بالخطأ');
      expect(store.packages, isEmpty);
      expect(store.refunds.single.amount, 30000);
      expect(store.payments, isEmpty);
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
      await enter(EntryMode.package);
      final package = store.packages.single;
      await store.closeSession(session.id);
      await store.reverseEntry(
        attendanceId: store.attendances.single.id,
        reason: 'غائب',
      );
      final absence = store.attendances.single;
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: DateTime.now().add(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      await enter(EntryMode.makeup, original: absence.id);
      await expectLater(
        store.markAbsentPresent(attendanceId: absence.id, reason: 'كان حاضر'),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.refundPackage(packageId: package.id, reason: 'إرجاع'),
        throwsA(isA<CenterException>()),
      );
      expect(store.packages.single.remaining, 3);
      await store.reverseEntry(
        attendanceId: store.attendances.last.id,
        reason: 'تعويض خطأ',
      );
      await store.markAbsentPresent(
        attendanceId: absence.id,
        reason: 'كان حاضر',
      );
      expect(store.packages.single.remaining, 3);
      expect(store.attendances.single.status, AttendanceStatus.present);
    },
  );
  test(
    'cashier corrections are audited; assistant, signed-out and empty-reason denied',
    () async {
      await enter(EntryMode.single);
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
      final entry = store.attendances.single;
      await expectLater(
        store.reverseEntry(attendanceId: entry.id, reason: ' '),
        throwsA(isA<CenterException>()),
      );
      store.signOut();
      await expectLater(
        store.reverseEntry(attendanceId: entry.id, reason: 'خطأ'),
        throwsA(isA<CenterException>()),
      );
      await store.signIn('مساعد', 'assistant-pass');
      await expectLater(
        store.reverseEntry(attendanceId: entry.id, reason: 'خطأ'),
        throwsA(isA<CenterException>()),
      );
      store.signOut();
      await store.signIn('استقبال', 'cashier-pass');
      await store.reverseEntry(attendanceId: entry.id, reason: 'مراجعة الورق');
      expect(store.corrections.single.staffId, store.currentUser!.id);
      expect(store.refunds.single.staffId, store.currentUser!.id);
      expect(store.audit.last.staffId, store.currentUser!.id);
      expect(store.audit.last.description, contains('مراجعة الورق'));
    },
  );
  test(
    'corrections survive reopen and backup; forged refund or relation rejects without state loss',
    () async {
      await enter(EntryMode.single);
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 7500);
      await store.reopenFinancialClosing(
        closingId: store.closings.single.id,
        reason: 'تدقيق',
      );
      await store.reverseEntry(
        attendanceId: store.attendances.single.id,
        reason: 'خطأ',
      );
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final backup = await store.createBackup();
      final data =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      for (final mutation in [
        'refund',
        'correction',
        'oldClosing',
        'freePaidPresence',
      ]) {
        final forged = jsonDecode(jsonEncode(data)) as Map<String, dynamic>;
        if (mutation == 'refund') {
          ((forged['data'] as Map)['refunds'] as List).first['amount'] = 1;
        } else if (mutation == 'correction') {
          ((forged['data'] as Map)['corrections'] as List).last['studentId'] =
              'missing';
        } else if (mutation == 'oldClosing') {
          ((forged['data'] as Map)['closings'] as List)
                  .first['summary']['presentCount'] =
              9;
        } else {
          ((forged['data'] as Map)['attendances'] as List).last['status'] =
              'present';
        }
        final path = File('${directory.path}/forged-$mutation.json');
        await path.writeAsString(jsonEncode(forged));
        await expectLater(
          store.restoreBackup(path.path),
          throwsA(isA<CenterException>()),
        );
        expect(store.refunds.single.amount, 7500);
      }
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.corrections, hasLength(2));
      expect(store.closings, hasLength(1));
      expect(store.allClosings, hasLength(2));
      await store.restoreBackup(backup);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.refunds.single.amount, 7500);
      expect(store.payments, isEmpty);
      expect(store.allPayments, hasLength(1));
    },
  );
  test(
    'SQLite commit failure rolls back reversal and refund as one operation',
    () async {
      await enter(EntryMode.single);
      final id = store.attendances.single.id;
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      await db.execute(
        "CREATE TRIGGER reject_correction BEFORE UPDATE ON state BEGIN SELECT RAISE(ABORT, 'blocked'); END",
      );
      await expectLater(
        store.reverseEntry(attendanceId: id, reason: 'خطأ'),
        throwsA(isA<CenterException>()),
      );
      expect(store.attendances.single.id, id);
      expect(store.payments, hasLength(1));
      expect(store.refunds, isEmpty);
      expect(store.corrections, isEmpty);
      await db.execute('DROP TRIGGER reject_correction');
      await db.close();
      await store.reverseEntry(attendanceId: id, reason: 'خطأ');
      expect(store.refunds.single.amount, 7500);
    },
  );
}
