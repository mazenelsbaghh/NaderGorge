import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'cancellation-test-password';
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  late LessonSession session;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-cancellations-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير الاختبار', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة اختبار',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 12345,
        packagePrice: 41000,
        twoSessionPrice: 17777,
        threeSessionPrice: 26666,
      ),
    );
    group = store.groups.single;
    student = await store.registerStudent(
      Student(
        name: 'طالب الاختبار',
        groupIds: [group.id],
        discountPercent: 25,
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
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

  Future<void> enter({EntryMode mode = EntryMode.single, int sessions = 2}) =>
      store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: mode,
          packageSessions: sessions,
        ),
      );

  String evidence() => jsonEncode({
    'payments': store.allPayments.map((e) => e.toJson()).toList(),
    'attendance': store.allAttendances.map((e) => e.toJson()).toList(),
    'packages': store.allPackages.map((e) => e.toJson()).toList(),
    'refunds': store.refunds.map((e) => e.toJson()).toList(),
    'corrections': store.corrections.map((e) => e.toJson()).toList(),
    'audit': store.audit.map((e) => e.toJson()).toList(),
  });

  Future<void> restart() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير الاختبار', password);
  }

  for (final makeup in [false, true]) {
    test(
      'used month payment cancellation keeps ${makeup ? 'makeup' : 'normal'} attendance unpaid and survives restart',
      () async {
        var target = session;
        if (makeup) {
          await store.saveGroup(
            StudyGroup(
              name: 'مجموعة الاستقبال',
              subjectId: group.subjectId,
              centerId: group.centerId,
              gradeId: group.gradeId,
              sessionPrice: group.sessionPrice,
              packagePrice: group.packagePrice,
            ),
          );
          final other = store.groups.last;
          await store.saveSession(
            LessonSession(
              groupId: other.id,
              number: 1,
              startsAt: session.startsAt,
              createdAt: DateTime.now(),
            ),
          );
          target = store.sessions.last;
        }
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: target.id,
            mode: EntryMode.package,
            makeupSourceGroupId: makeup ? group.id : null,
          ),
        );
        final payment = store.payments.single;
        final attendanceId = store.attendances.single.id;
        final paid = payment.collectedAmount;
        await store.cancelPayment(
          paymentId: payment.id,
          reason: 'دفع أضيف بالخطأ',
        );
        expect(store.payments, isEmpty);
        expect(store.packages, isEmpty);
        expect(store.attendances, hasLength(1));
        expect(store.attendances.single.paymentPending, isTrue);
        expect(store.attendances.single.packageId, isNull);
        expect(store.attendances.single.sessionId, target.id);
        expect(
          store.attendances.single.status,
          makeup ? AttendanceStatus.makeup : AttendanceStatus.present,
        );
        expect(store.allAttendances.any((e) => e.id == attendanceId), isTrue);
        expect(store.refunds.single.amount, paid);
        await restart();
        final summary = store.sessionFinancialSummary(target.id);
        expect(summary.presentCount + summary.makeupCount, 1);
        expect(store.attendanceNeedsPayment(student.id, target.id), isTrue);
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: target.id,
            mode: EntryMode.single,
            makeupSourceGroupId: makeup ? group.id : null,
          ),
        );
        expect(store.attendances, hasLength(1));
        expect(store.payments, hasLength(1));
      },
    );
  }

  for (final percent in [25, 100]) {
    test(
      'payment-only cancellation with $percent percent discount keeps attendance unpaid and recharge does not duplicate entry',
      () async {
        if (percent != 25) {
          await store.saveStudentDiscount(
            studentId: student.id,
            percent: percent,
          );
        }
        await enter();
        final originalEntry = store.attendances.single.toJson();
        final originalPayment = store.payments.single.toJson();
        final amount = store.payments.single.netAmount;
        await store.cancelPayment(
          paymentId: store.payments.single.id,
          reason: 'إيصال خاطئ والحضور صحيح',
        );
        expect(store.attendances.single.toJson(), originalEntry);
        expect(store.payments, isEmpty);
        expect(store.allPayments.single.toJson(), originalPayment);
        expect(store.refunds.single.amount, amount);
        expect(
          store.paymentStatusFor(student.id, session.id).status,
          StudentPaymentStatus.notPaid,
        );
        final unpaidSummary = store.sessionFinancialSummary(session.id);
        expect(unpaidSummary.totalCollected, 0);
        expect(unpaidSummary.refundAmount, amount);
        expect(unpaidSummary.expectedCash, 0);
        expect(unpaidSummary.presentCount, 1);
        expect(unpaidSummary.freeCount, 0);
        expect(unpaidSummary.allFreeCount, 0);
        final unpaidCategory = unpaidSummary.studentCategories!.single;
        expect(unpaidCategory.kind, SessionStudentCategoryKind.unpaid);
        expect(unpaidCategory.studentCount, 1);
        expect(unpaidCategory.operationCount, 0);
        expect(unpaidCategory.unitAmount, 0);
        expect(unpaidSummary.paymentAmountCategories, isEmpty);
        await store.closeSession(session.id);
        await store.finalizeSession(sessionId: session.id, actualCash: 0);
        final unpaidClosing = jsonEncode(store.closings.single.toJson());
        await restart();
        expect(store.closings.single.summary.freeCount, 0);
        expect(
          store.closings.single.summary.studentCategories!.single.kind,
          SessionStudentCategoryKind.unpaid,
        );
        await store.reopenSession(session.id);
        final quote = store.entryConfirmationFor(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        expect(quote.netAmount, amount);
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
            confirmation: quote,
          ),
        );
        expect(store.attendances, hasLength(1));
        expect(store.attendances.single.id, isNot(originalEntry['id']));
        expect(store.allAttendances, hasLength(2));
        expect(store.allAttendances.first.toJson(), originalEntry);
        expect(store.corrections.last.action, CorrectionAction.entryCorrected);
        expect(store.corrections.last.attendanceId, originalEntry['id']);
        expect(
          store.corrections.last.replacementAttendanceId,
          store.attendances.single.id,
        );
        expect(store.payments.single.netAmount, amount);
        expect(store.allPayments, hasLength(2));
        expect(store.refunds, hasLength(1));
        expect(
          store.sessionFinancialSummary(session.id).totalCollected,
          amount,
        );
        expect(
          store
              .sessionFinancialSummary(session.id)
              .studentCategories!
              .any((e) => e.kind == SessionStudentCategoryKind.unpaid),
          isFalse,
        );
        expect(
          store.sessionFinancialSummary(session.id).allFreeCount,
          percent == 100 ? 1 : 0,
        );
        final beforeDuplicate = evidence();
        await expectLater(enter(), throwsA(isA<CenterException>()));
        expect(evidence(), beforeDuplicate);
        await store.closeSession(session.id);
        await store.finalizeSession(sessionId: session.id, actualCash: amount);
        expect(jsonEncode(store.allClosings.first.toJson()), unpaidClosing);
        expect(store.closings.single.summary.totalCollected, amount);
        await restart();
        expect(jsonEncode(store.allClosings.first.toJson()), unpaidClosing);
        expect(store.closings.single.summary.totalCollected, amount);
      },
    );
  }

  test(
    'attendance-only cancellation retains receipt and re-entry reuses its immutable amount',
    () async {
      await enter();
      final original = store.attendances.single.toJson();
      final payment = store.payments.single.toJson();
      await store.cancelAttendance(
        attendanceId: store.attendances.single.id,
        reason: 'الحضور على الكود الخطأ لكن الدفع صحيح',
      );
      expect(store.attendances, isEmpty);
      expect(store.allAttendances.single.toJson(), original);
      expect(store.payments.single.toJson(), payment);
      expect(store.refunds, isEmpty);
      await store.saveStudentDiscount(studentId: student.id, percent: 50);
      final request = EntryRequest(
        studentId: student.id,
        sessionId: session.id,
        mode: EntryMode.single,
      );
      final quote = store.entryConfirmationFor(request);
      expect(quote.baseAmount, 0);
      expect(quote.netAmount, 0);
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
          confirmation: quote,
        ),
      );
      expect(store.attendances, hasLength(1));
      expect(store.allAttendances, hasLength(2));
      expect(store.payments.single.toJson(), payment);
      expect(store.allPayments, hasLength(1));
      expect(store.refunds, isEmpty);
      expect(
        store.sessionFinancialSummary(session.id).totalCollected,
        payment['netAmount'],
      );
    },
  );

  for (final source in ['payment', 'attendance']) {
    test(
      '$source with related cancellation refunds once and removes open single entry',
      () async {
        await enter();
        final entryId = store.attendances.single.id;
        final paymentId = store.payments.single.id;
        final amount = store.payments.single.netAmount;
        if (source == 'payment') {
          await store.cancelPayment(
            paymentId: paymentId,
            reason: 'إلغاء العملية كلها',
            mode: RecordCancellationMode.recordAndRelated,
          );
        } else {
          await store.cancelAttendance(
            attendanceId: entryId,
            reason: 'إلغاء العملية كلها',
            mode: RecordCancellationMode.recordAndRelated,
          );
        }
        expect(store.attendances, isEmpty);
        expect(store.payments, isEmpty);
        expect(store.allPayments.single.id, paymentId);
        expect(store.allAttendances.single.id, entryId);
        expect(store.refunds.single.amount, amount);
        expect(store.sessionFinancialSummary(session.id).expectedCash, 0);
        final snapshot = evidence();
        await expectLater(
          store.cancelPayment(paymentId: paymentId, reason: 'رد مكرر'),
          throwsA(isA<CenterException>()),
        );
        await expectLater(
          store.cancelAttendance(attendanceId: entryId, reason: 'إلغاء مكرر'),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), snapshot);
        await enter();
        expect(store.payments, hasLength(1));
        expect(store.attendances, hasLength(1));
        expect(
          store.sessionFinancialSummary(session.id).totalCollected,
          amount,
        );
      },
    );
  }

  test(
    'closed single related cancellation keeps an absence; financially finalized records stay immutable',
    () async {
      await enter();
      final entryId = store.attendances.single.id;
      await store.closeSession(session.id);
      final amount = store.payments.single.netAmount;
      await store.finalizeSession(sessionId: session.id, actualCash: amount);
      final closedEvidence = evidence();
      await expectLater(
        store.cancelAttendance(
          attendanceId: entryId,
          reason: 'قبل فتح التقفيلة',
          mode: RecordCancellationMode.recordAndRelated,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), closedEvidence);
      await store.reopenFinancialClosing(
        closingId: store.closings.single.id,
        reason: 'مراجعة الإلغاء',
      );
      await store.cancelAttendance(
        attendanceId: entryId,
        reason: 'لم يحضر بالفعل',
        mode: RecordCancellationMode.recordAndRelated,
      );
      expect(store.attendances.single.status, AttendanceStatus.absent);
      expect(store.allAttendances.first.id, entryId);
      expect(store.payments, isEmpty);
      expect(store.refunds.single.amount, amount);
      expect(store.sessions.single.status, SessionStatus.closed);
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      expect(store.closings.single.summary.totalCollected, 0);
      expect(store.allClosings, hasLength(2));
      expect(store.allClosings.first.summary.totalCollected, amount);
      await restart();
      expect(store.attendances.single.status, AttendanceStatus.absent);
      expect(store.refunds.single.amount, amount);
    },
  );

  test(
    'related package cancellation restores credit and refunds full purchase atomically',
    () async {
      await enter(mode: EntryMode.package, sessions: 3);
      final entryId = store.attendances.single.id;
      final paymentId = store.payments.single.id;
      final packageId = store.packages.single.id;
      final amount = store.payments.single.netAmount;
      expect(store.packages.single.remaining, 2);
      await store.cancelPayment(
        paymentId: paymentId,
        reason: 'رد الباقة مع إلغاء دخولها',
        mode: RecordCancellationMode.recordAndRelated,
      );
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.allAttendances.single.id, entryId);
      expect(store.allPackages.single.id, packageId);
      expect(store.allPackages.single.remaining, 3);
      expect(store.refunds.single.amount, amount);
      expect(store.sessionFinancialSummary(session.id).totalCollected, 0);
      final canceled = evidence();
      await expectLater(
        store.cancelPayment(paymentId: paymentId, reason: 'تكرار'),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), canceled);
    },
  );

  test(
    'closed attendance-only cancellation allows absence correction using retained receipt without another charge',
    () async {
      await enter();
      final originalPayment = store.payments.single.toJson();
      await store.closeSession(session.id);
      await store.cancelAttendance(
        attendanceId: store.attendances.single.id,
        reason: 'مراجعة حضور دون رد الدفع',
      );
      final absent = store.attendances.single;
      expect(absent.status, AttendanceStatus.absent);
      expect(store.payments.single.toJson(), originalPayment);
      expect(store.refunds, isEmpty);
      await store.markAbsentPresent(
        attendanceId: absent.id,
        reason: 'تأكدنا أنه حضر بالفعل',
      );
      expect(store.attendances.single.status, AttendanceStatus.present);
      expect(store.allPayments, hasLength(1));
      expect(store.payments.single.toJson(), originalPayment);
      expect(store.refunds, isEmpty);
      expect(
        store.sessionFinancialSummary(session.id).totalCollected,
        originalPayment['netAmount'],
      );
    },
  );

  test(
    'attendance-only returns open package credit without refund; related cancellation refunds an otherwise unused package',
    () async {
      await enter(mode: EntryMode.package);
      final payment = store.payments.single.toJson();
      await store.cancelAttendance(
        attendanceId: store.attendances.single.id,
        reason: 'إلغاء الحضور فقط',
      );
      expect(store.attendances, isEmpty);
      expect(store.packages.single.remaining, 2);
      expect(store.payments.single.toJson(), payment);
      expect(store.refunds, isEmpty);
      final afterCancel = store.sessionFinancialSummary(session.id);
      expect(afterCancel.prepaidCount, 0);
      expect(afterCancel.presentCount, 0);
      expect(afterCancel.totalCollected, payment['netAmount']);
      expect(afterCancel.refundAmount, 0);
      final quote = store.entryConfirmationFor(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.package,
        ),
      );
      expect(quote.eligibleRemaining, 2);
      expect(quote.netAmount, 0);
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.package,
          confirmation: quote,
        ),
      );
      expect(store.packages.single.remaining, 1);
      expect(store.allPayments, hasLength(1));
      final afterReentry = store.sessionFinancialSummary(session.id);
      expect(afterReentry.prepaidCount, 1);
      expect(afterReentry.totalCollected, payment['netAmount']);
      expect(afterReentry.packageBuyerCount, 1);
      expect(afterReentry.refundAmount, 0);
      await store.cancelAttendance(
        attendanceId: store.attendances.single.id,
        reason: 'إلغاء الباقة والدخول',
        mode: RecordCancellationMode.recordAndRelated,
      );
      expect(store.attendances, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.allPackages.single.remaining, 2);
      expect(store.refunds.single.amount, payment['netAmount']);
    },
  );

  test(
    'prior closed package usage prevents both cancellation paths from partially returning later credit',
    () async {
      await enter(mode: EntryMode.package, sessions: 3);
      await store.closeSession(session.id);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: DateTime.now().add(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      session = store.sessions.last;
      await enter(mode: EntryMode.package);
      expect(store.packages.single.remaining, 1);
      final before = evidence();
      await expectLater(
        store.cancelPayment(
          paymentId: store.payments.single.id,
          reason: 'رد باقة مستخدمة',
          mode: RecordCancellationMode.recordAndRelated,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), before);
      await expectLater(
        store.cancelAttendance(
          attendanceId: store.attendances.last.id,
          reason: 'رد بعد حصة سابقة',
          mode: RecordCancellationMode.recordAndRelated,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), before);
    },
  );

  test(
    'unused package cancellation and payment-only unpaid evidence survive restart and validated backup restore',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: student.id, groupId: group.id, sessions: 2),
      );
      final packagePayment = store.payments.single.id;
      await store.cancelPayment(
        paymentId: packagePayment,
        reason: 'لم يستخدم الباقة',
      );
      expect(store.packages, isEmpty);
      expect(store.allPackages.single.remaining, 2);
      await enter();
      final attendanceId = store.attendances.single.id;
      await store.cancelPayment(
        paymentId: store.payments.single.id,
        reason: 'إيصال الحصة خطأ',
      );
      final backup = await store.createBackup();
      await restart();
      expect(store.attendances.single.id, attendanceId);
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
      expect(store.refunds, hasLength(2));
      await store.restoreBackup(backup);
      await store.signIn('مدير الاختبار', password);
      expect(store.attendances.single.id, attendanceId);
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      final content =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final correction =
          ((content['data'] as Map)['corrections'] as List).last as Map;
      correction['studentId'] = 'missing-student';
      final forged = File('${directory.path}/forged.json');
      await forged.writeAsString(jsonEncode(content));
      final before = evidence();
      await expectLater(
        store.restoreBackup(forged.path),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), before);
      await enter();
      expect(store.attendances, hasLength(1));
      expect(store.attendances.single.id, isNot(attendanceId));
      expect(store.allAttendances.first.id, attendanceId);
      expect(store.payments, hasLength(1));
    },
  );

  test(
    'reason and role checks precede mutations; two concurrent cashier refunds pay only once',
    () async {
      await enter();
      final id = store.payments.single.id;
      await store.saveStaff(
        name: 'استقبال',
        password: 'cashier-password',
        role: StaffRole.cashier,
      );
      await store.saveStaff(
        name: 'مساعد',
        password: 'assistant-password',
        role: StaffRole.assistant,
      );
      final untouched = evidence();
      for (final denied in ['signedOut', 'assistant']) {
        store.signOut();
        if (denied == 'assistant') {
          await store.signIn('مساعد', 'assistant-password');
        }
        await expectLater(
          store.cancelPayment(paymentId: id, reason: 'تصحيح'),
          throwsA(isA<CenterException>()),
        );
        await expectLater(
          store.cancelAttendance(
            attendanceId: store.attendances.single.id,
            reason: 'تصحيح',
          ),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), untouched);
      }
      store.signOut();
      await store.signIn('استقبال', 'cashier-password');
      await expectLater(
        store.cancelPayment(paymentId: id, reason: '  '),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), untouched);
      Future<bool> cancel() async {
        try {
          await store.cancelPayment(paymentId: id, reason: 'مراجعة إيصال');
          return true;
        } on CenterException {
          return false;
        }
      }

      final results = await Future.wait([cancel(), cancel()]);
      expect(results.where((e) => e), hasLength(1));
      expect(store.refunds, hasLength(1));
      expect(store.refunds.single.staffId, store.currentUser!.id);
      expect(store.corrections.single.staffId, store.currentUser!.id);
      expect(store.audit.last.description, contains('مراجعة إيصال'));
      expect(store.attendances, hasLength(1));
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
    },
  );

  test(
    'SQLite failure rolls back package refund, returned credit, attendance cancellation and audit; retry succeeds',
    () async {
      await enter(mode: EntryMode.package);
      final paymentId = store.payments.single.id;
      final before = evidence();
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      try {
        await db.execute(
          "CREATE TRIGGER reject_cancel BEFORE UPDATE ON state BEGIN SELECT RAISE(ABORT, 'temporary test failure'); END",
        );
        await expectLater(
          store.cancelPayment(
            paymentId: paymentId,
            reason: 'رد مع الدخول',
            mode: RecordCancellationMode.recordAndRelated,
          ),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), before);
        expect(store.packages.single.remaining, 1);
        await db.execute('DROP TRIGGER reject_cancel');
      } finally {
        await db.close();
      }
      await restart();
      expect(evidence(), before);
      await store.cancelPayment(
        paymentId: paymentId,
        reason: 'رد مع الدخول',
        mode: RecordCancellationMode.recordAndRelated,
      );
      expect(store.packages, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.refunds, hasLength(1));
      expect(store.allPackages.single.remaining, 2);
    },
  );
}
