import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late Student student;
  late LessonSession session;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-reception-read-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('manager', 'reception-test-password');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة الاختبار',
        subjectId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.subject)
            .id,
        centerId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.center)
            .id,
        gradeId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.grade)
            .id,
        sessionPrice: 10000,
        packagePrice: 40000,
      ),
    );
    student = await store.registerStudent(
      Student(
        name: 'طالب الاختبار',
        groupIds: [store.groups.single.id],
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    await store.saveSession(
      LessonSession(
        groupId: store.groups.single.id,
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

  Future<void> collect() => store.collectAndAttend(
    EntryRequest(
      studentId: student.id,
      sessionId: session.id,
      mode: EntryMode.single,
      paidAmount: 4000,
    ),
  );

  test(
    'warm reception reads follow settlement, correction, cancellation and restore',
    () async {
      expect(store.studentHistoryFor(student.id).attendances, isEmpty);
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
      expect(store.attendances, isEmpty);
      await collect();
      final payment = store.payments.single;
      final collectedHistory = store.studentHistoryFor(student.id);
      expect(collectedHistory.attendances.single.studentId, student.id);
      expect(collectedHistory.payments.single.id, payment.id);
      expect(collectedHistory.activePaymentIds, {payment.id});
      expect(store.paymentStatusFor(student.id, session.id).debtAmount, 6000);
      expect(store.paymentReviewAmountFor(student.id, session.id), 4000);
      await store.checkPayment(
        studentId: student.id,
        sessionId: session.id,
        expectedAmount: 4000,
      );
      expect(store.isPaymentCheckCurrent(student.id, session.id), isTrue);

      await store.settleDebt(
        paymentId: payment.id,
        kind: DebtKind.lesson,
        amount: 6000,
        sessionId: session.id,
      );
      expect(store.paymentStatusFor(student.id, session.id).debtAmount, 0);
      expect(store.paymentReviewAmountFor(student.id, session.id), 10000);
      expect(store.isPaymentCheckCurrent(student.id, session.id), isFalse);
      await store.correctPaymentMethod(
        paymentId: payment.id,
        method: 'تحويل',
        reason: 'تصحيح وسيلة التحصيل',
      );
      expect(store.payments.single.method, 'تحويل');
      final correctedHistory = store.studentHistoryFor(student.id);
      expect(correctedHistory.settlements.single.amount, 6000);
      expect(correctedHistory.payments.single.method, 'نقدي');
      expect(
        correctedHistory.corrections.single.action,
        CorrectionAction.paymentMethod,
      );
      final backup = await store.createBackup();

      await store.cancelPayment(
        paymentId: payment.id,
        reason: 'إلغاء للتحقق من تحديث المراجعة',
      );
      expect(store.payments, isEmpty);
      final canceledHistory = store.studentHistoryFor(student.id);
      expect(canceledHistory.payments.single.id, payment.id);
      expect(canceledHistory.activePaymentIds, isEmpty);
      expect(canceledHistory.refunds, isNotEmpty);
      expect(collectedHistory.activePaymentIds, {payment.id});
      expect(store.paymentReviewAmountFor(student.id, session.id), isNull);
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
      await store.restoreBackup(backup);
      await store.signIn('manager', 'reception-test-password');
      expect(store.payments.single.method, 'تحويل');
      expect(store.paymentReviewAmountFor(student.id, session.id), 10000);
      expect(store.paymentStatusFor(student.id, session.id).debtAmount, 0);
      expect(store.studentHistoryFor(student.id).activePaymentIds, {
        payment.id,
      });
      store.signOut();
      expect(
        () => store.paymentStatusFor(student.id, session.id),
        throwsA(isA<CenterException>()),
      );
    },
  );

  test(
    'warm identifier lookup follows profile edits, region changes and backup restore',
    () async {
      expect(store.lookupReceptionStudents(student.code).single.id, student.id);
      final backup = await store.createBackup();
      await store.saveStudent(
        student.copyWith(name: 'الاسم الجديد', phone: '01099998888'),
      );
      expect(store.lookupReceptionStudents(student.name), isEmpty);
      expect(
        store.lookupReceptionStudents(student.code).single.name,
        'الاسم الجديد',
      );
      expect(
        store.lookupReceptionStudents('٠١٠٩٩٩٩٨٨٨٨').single.id,
        student.id,
      );
      final center = store.catalogs.firstWhere(
        (entry) => entry.kind == CatalogKind.center,
      );
      await store.saveCatalog(center.copyWith(region: 'cairo'));
      expect(store.lookupReceptionStudents(student.code), isEmpty);
      await store.restoreBackup(backup);
      await store.signIn('manager', 'reception-test-password');
      expect(
        store.lookupReceptionStudents(student.code).single.name,
        student.name,
      );
      expect(store.lookupReceptionStudents('01099998888'), isEmpty);
    },
  );

  test(
    'failed commit leaves warmed attendance and receipts unchanged, then retry refreshes them',
    () async {
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
      expect(store.payments, isEmpty);
      final database = await databaseFactoryFfi.openDatabase(
        store.databasePath,
      );
      await database.execute(
        "CREATE TRIGGER deny_reception_write BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'test write failure'); END",
      );
      try {
        await expectLater(collect(), throwsA(isA<CenterException>()));
        expect(store.attendances, isEmpty);
        expect(store.payments, isEmpty);
        expect(store.studentHistoryFor(student.id).payments, isEmpty);
        expect(store.paymentReviewAmountFor(student.id, session.id), isNull);
        expect(
          store.paymentStatusFor(student.id, session.id).status,
          StudentPaymentStatus.notPaid,
        );
      } finally {
        await database.execute('DROP TRIGGER deny_reception_write');
      }
      await collect();
      expect(store.attendances, hasLength(1));
      expect(store.studentHistoryFor(student.id).attendances, hasLength(1));
      expect(store.payments, hasLength(1));
      expect(store.paymentReviewAmountFor(student.id, session.id), 4000);
      expect(store.paymentStatusFor(student.id, session.id).debtAmount, 6000);
    },
  );
}
