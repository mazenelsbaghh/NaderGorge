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
  late List<Student> students;
  late LessonSession current;
  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-center-finance-');
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
    for (var i = 0; i < 6; i++) {
      await store.saveStudent(
        Student(
          name: 'طالب $i',
          code: 'S$i',
          groupIds: [group.id],
          discountPercent: i == 0
              ? 25
              : i == 3
              ? 10
              : 0,
          createdAt: DateTime.now().subtract(const Duration(days: 2)),
        ),
      );
    }
    students = store.students;
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: DateTime.now().add(const Duration(hours: 1)),
        createdAt: DateTime.now(),
      ),
    );
    final source = store.sessions.single;
    await store.renewPackage(
      PackageRequest(studentId: students[5].id, groupId: group.id),
    );
    await store.closeSession(source.id);
    final absence = store.attendances.firstWhere(
      (e) => e.studentId == students[5].id,
    );
    await store.renewPackage(
      PackageRequest(studentId: students[2].id, groupId: group.id),
    );
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 2,
        startsAt: DateTime.now().add(const Duration(hours: 2)),
        createdAt: DateTime.now(),
      ),
    );
    current = store.sessions.last;
    Future<void> enter(
      int i,
      EntryMode mode, {
      String method = 'نقدي',
      String? original,
    }) => store.collectAndAttend(
      EntryRequest(
        studentId: students[i].id,
        sessionId: current.id,
        mode: mode,
        method: method,
        originalAttendanceId: original,
      ),
    );
    await enter(0, EntryMode.single);
    await enter(1, EntryMode.single, method: 'تحويل');
    await enter(2, EntryMode.package);
    await enter(3, EntryMode.package);
    await enter(5, EntryMode.makeup, original: absence.id);
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  test(
    'code checks require actual receipt and attendance without inventing income',
    () async {
      final cash = store.sessionFinancialSummary(current.id).totalCollected;
      final paymentCount = store.payments.length;
      expect(
        store.paymentStatusFor(students[0].id, current.id).status,
        StudentPaymentStatus.paidSingle,
      );
      expect(
        store.paymentStatusFor(students[2].id, current.id).status,
        StudentPaymentStatus.paidPackage,
      );
      expect(
        store.paymentStatusFor(students[5].id, current.id).status,
        StudentPaymentStatus.makeup,
      );
      for (final index in [2, 4, 5]) {
        await expectLater(
          store.checkPayment(
            studentId: students[index].id,
            sessionId: current.id,
            expectedAmount: 7500,
          ),
          throwsA(isA<CenterException>()),
        );
      }
      expect(store.paymentChecks, isEmpty);
      await store.checkPayment(
        studentId: students[0].id,
        sessionId: current.id,
        expectedAmount: 7500,
      );
      final old = store.paymentChecks.single;
      await expectLater(
        store.checkPayment(
          studentId: students[0].id,
          sessionId: current.id,
          expectedAmount: 7500,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.paymentChecks.single.id, old.id);
      expect(store.paymentChecks.single.amount, 7500);
      expect(store.isPaymentCheckCurrent(students[0].id, current.id), isTrue);
      await store.renewPackage(
        PackageRequest(studentId: students[4].id, groupId: group.id),
      );
      expect(
        store.paymentStatusFor(students[4].id, current.id).status,
        StudentPaymentStatus.paidPackage,
      );
      await expectLater(
        store.checkPayment(
          studentId: students[4].id,
          sessionId: current.id,
          expectedAmount: 40000,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.paymentChecks, hasLength(1));
      expect(store.reviews, isEmpty);
      expect(store.payments, hasLength(paymentCount + 1));
      expect(store.sessionFinancialSummary(current.id).totalCollected, cash);
      expect(
        store.attendances.where(
          (e) => e.studentId == students[4].id && e.sessionId == current.id,
        ),
        isEmpty,
      );
      await store.closeSession(current.id);
      await store.finalizeSession(sessionId: current.id, actualCash: 43500);
      await store.clearPaymentChecks(sessionId: current.id);
      await store.checkPayment(
        studentId: students[0].id,
        sessionId: current.id,
        expectedAmount: 7500,
      );
      expect(store.paymentChecks, hasLength(1));
      expect(store.closings.single.summary.totalCollected, cash);
    },
  );

  test(
    'free and separately priced sessions preserve credit and only real receipts can be checked',
    () async {
      await store.closeSession(current.id);
      final credit = store.remainingFor(students[2].id, group.id);
      for (final kind in [SessionKind.free, SessionKind.extra]) {
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: store.sessions.length + 1,
            startsAt: DateTime.now().add(const Duration(hours: 5)),
            createdAt: DateTime.now(),
            kind: kind,
            extraPrice: 5000,
          ),
        );
        final session = store.sessions.last;
        final status = store.paymentStatusFor(students[2].id, session.id);
        expect(
          status.status,
          kind == SessionKind.free
              ? StudentPaymentStatus.free
              : StudentPaymentStatus.notPaid,
        );
        expect(status.packageId, isNull);
        await expectLater(
          store.checkPayment(
            studentId: students[2].id,
            sessionId: session.id,
            expectedAmount: 5000,
          ),
          throwsA(isA<CenterException>()),
        );
        await store.collectAndAttend(
          EntryRequest(
            studentId: students[2].id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        expect(store.remainingFor(students[2].id, group.id), credit);
        if (kind == SessionKind.free) {
          await expectLater(
            store.checkPayment(
              studentId: students[2].id,
              sessionId: session.id,
              expectedAmount: 5000,
            ),
            throwsA(isA<CenterException>()),
          );
          expect(store.paymentChecks, isEmpty);
        } else {
          await store.checkPayment(
            studentId: students[2].id,
            sessionId: session.id,
            expectedAmount: 5000,
          );
          final check = store.paymentChecks.single;
          final audited = store.audit.length;
          for (final edit in [
            () => store.saveSession(session.copyWith(kind: SessionKind.free)),
            () => store.cancelSession(session.id),
          ]) {
            await expectLater(
              edit(),
              throwsA(
                isA<CenterException>().having(
                  (e) => e.message,
                  'reviewed class',
                  contains('مراجعة الدفع بالكود'),
                ),
              ),
            );
          }
          expect(store.sessions.last.kind, SessionKind.extra);
          expect(store.sessions.last.status, SessionStatus.open);
          expect(store.paymentChecks.single.id, check.id);
          expect(check.status, StudentPaymentStatus.paidSingle);
          expect(store.audit.length, audited);
        }
      }
    },
  );

  test(
    'expired package still covers its historical paid absence and attendance',
    () async {
      await store.closeSession(current.id);
      for (var number = 3; number <= 5; number++) {
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: number,
            startsAt: DateTime.now().add(Duration(hours: number)),
            createdAt: DateTime.now(),
          ),
        );
        await store.closeSession(store.sessions.last.id);
      }
      expect(store.remainingFor(students[2].id, group.id), 0);
      expect(store.remainingFor(students[5].id, group.id), 0);
      expect(
        store.paymentStatusFor(students[2].id, current.id).status,
        StudentPaymentStatus.paidPackage,
      );
      expect(
        store.paymentStatusFor(students[5].id, store.sessions.first.id).status,
        StudentPaymentStatus.paidPackage,
      );
      await expectLater(
        store.checkPayment(
          studentId: students[5].id,
          sessionId: store.sessions.first.id,
          expectedAmount: 40000,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.paymentChecks, isEmpty);
    },
  );

  test(
    'payment checks persist, reject forged identities and load older schema2 backups',
    () async {
      await store.checkPayment(
        studentId: students[0].id,
        sessionId: current.id,
        expectedAmount: 7500,
      );
      final id = store.paymentChecks.single.id;
      final backup = await store.createBackup();
      final original =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final forged = jsonDecode(jsonEncode(original)) as Map<String, dynamic>;
      final checks = (forged['data'] as Map)['paymentChecks'] as List;
      checks.first['studentId'] = students[1].id;
      final file = File('${directory.path}/wrong-code-check.json');
      await file.writeAsString(jsonEncode(forged));
      await expectLater(
        store.restoreBackup(file.path),
        throwsA(isA<CenterException>()),
      );
      expect(store.paymentChecks.single.studentId, students[0].id);
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.paymentChecks.single.id, id);
      expect(
        store.paymentChecks.single.status,
        StudentPaymentStatus.paidSingle,
      );
      final older = jsonDecode(jsonEncode(original)) as Map<String, dynamic>;
      (older['data'] as Map).remove('paymentChecks');
      final legacy = File('${directory.path}/older-schema2.json');
      await legacy.writeAsString(jsonEncode(older));
      await store.restoreBackup(legacy.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.paymentChecks, isEmpty);
      expect(store.payments, hasLength(5));
      await store.restoreBackup(backup);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.paymentChecks.single.id, id);
    },
  );

  test(
    'code checking requires financial role and rejects canceled or unknown sessions',
    () async {
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 3,
          startsAt: DateTime.now().add(const Duration(hours: 5)),
          createdAt: DateTime.now(),
          kind: SessionKind.free,
        ),
      );
      final canceled = store.sessions.last;
      await store.cancelSession(canceled.id);
      await expectLater(
        store.checkPayment(studentId: students[0].id, sessionId: canceled.id),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.checkPayment(studentId: students[0].id, sessionId: 'missing'),
        throwsA(isA<CenterException>()),
      );
      await store.saveStaff(
        name: 'مساعد مراجعة',
        password: 'assistant-123',
        role: StaffRole.assistant,
      );
      store.signOut();
      await store.signIn('مساعد مراجعة', 'assistant-123');
      expect(
        () => store.paymentStatusFor(students[0].id, current.id),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.checkPayment(studentId: students[0].id, sessionId: current.id),
        throwsA(isA<CenterException>()),
      );
      expect(store.paymentChecks, isEmpty);
    },
  );

  test(
    'summary uses real discounted operations and keeps noncash and earlier packages separate',
    () async {
      await store.closeSession(current.id);
      final summary = store.sessionFinancialSummary(current.id);
      expect(summary.presentCount, 4);
      expect(summary.makeupCount, 1);
      expect(summary.absentCount, 1);
      expect(summary.prepaidCount, 2);
      expect(summary.singlePaymentCount, 2);
      expect(summary.packageSalesCount, 1);
      expect(summary.freeCount, 0);
      expect(summary.grossAmount, 60000);
      expect(summary.discountAmount, 6500);
      expect(summary.totalCollected, 53500);
      expect(summary.expectedCash, 43500);
      expect(summary.lines.fold(0, (sum, line) => sum + line.total), 53500);
      expect(
        summary.lines
            .where((line) => line.unitAmount == 0)
            .fold(0, (sum, line) => sum + line.count),
        4,
      );
      await store.saveGroup(
        group.copyWith(sessionPrice: 90000, packagePrice: 360000),
      );
      expect(store.sessionFinancialSummary(current.id).totalCollected, 53500);
    },
  );

  test(
    'paper review matches one selected payment, rejects duplicates and never creates revenue',
    () async {
      final payment = store.payments.firstWhere(
        (e) => e.studentId == students[0].id && e.sessionId == current.id,
      );
      await store.savePaymentReview(
        ReviewRequest(
          studentId: students[0].id,
          sessionId: current.id,
          paymentId: payment.id,
          paperAmount: 7000,
        ),
      );
      final review = store.reviews.single;
      expect(review.expectedAmount, 7500);
      expect(review.difference, -500);
      expect(review.matched, false);
      await expectLater(
        store.savePaymentReview(
          ReviewRequest(
            studentId: students[0].id,
            paymentId: payment.id,
            paperAmount: 7500,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      await store.savePaymentReview(
        ReviewRequest(
          id: review.id,
          studentId: students[0].id,
          paymentId: payment.id,
          paperAmount: 7500,
          notes: 'تمت المطابقة',
        ),
      );
      expect(store.reviews.single.matched, true);
      expect(store.reviews.single.sessionId, current.id);
      await store.savePaymentReview(
        ReviewRequest(
          studentId: students[4].id,
          sessionId: current.id,
          paperAmount: 10000,
        ),
      );
      expect(store.reviews.last.paymentId, isNull);
      expect(store.reviews.last.expectedAmount, 0);
      expect(store.reviews.last.difference, 10000);
      expect(store.reviews.last.matched, false);
      expect(store.payments, hasLength(5));
      expect(store.sessionFinancialSummary(current.id).totalCollected, 53500);
      expect(
        store.audit.where((e) => e.action == 'payment_review_save'),
        hasLength(3),
      );
    },
  );

  test(
    'review rejects wrong student session amount and unassigned payment attribution atomically',
    () async {
      final linked = store.payments.firstWhere(
        (e) => e.studentId == students[0].id && e.sessionId == current.id,
      );
      final unassigned = store.payments.firstWhere(
        (e) => e.studentId == students[2].id && e.sessionId == null,
      );
      final requests = [
        ReviewRequest(
          studentId: students[1].id,
          sessionId: current.id,
          paymentId: linked.id,
          paperAmount: 7500,
        ),
        ReviewRequest(
          studentId: students[0].id,
          sessionId: store.sessions.first.id,
          paymentId: linked.id,
          paperAmount: 7500,
        ),
        ReviewRequest(
          studentId: students[2].id,
          sessionId: current.id,
          paymentId: unassigned.id,
          paperAmount: 40000,
        ),
        ReviewRequest(studentId: students[0].id, paperAmount: -1),
      ];
      for (final request in requests) {
        await expectLater(
          store.savePaymentReview(request),
          throwsA(isA<CenterException>()),
        );
      }
      expect(store.reviews, isEmpty);
      expect(store.payments, hasLength(5));
      await store.savePaymentReview(
        ReviewRequest(
          studentId: students[2].id,
          paymentId: unassigned.id,
          paperAmount: 40000,
        ),
      );
      expect(store.reviews.single.sessionId, isNull);
      expect(store.reviews.single.matched, true);
    },
  );

  test(
    'financial finalize requires explicit attendance close and freezes snapshot and review rows',
    () async {
      await expectLater(
        store.finalizeSession(sessionId: current.id, actualCash: 43500),
        throwsA(isA<CenterException>()),
      );
      expect(store.sessions.last.status, SessionStatus.open);
      expect(store.closings, isEmpty);
      final payment = store.payments.firstWhere(
        (e) => e.studentId == students[0].id && e.sessionId == current.id,
      );
      await store.savePaymentReview(
        ReviewRequest(
          studentId: students[0].id,
          paymentId: payment.id,
          paperAmount: 7000,
        ),
      );
      await store.closeSession(current.id);
      await store.finalizeSession(
        sessionId: current.id,
        actualCash: 42500,
        notes: 'فرق ألف قرش',
      );
      final closing = store.closings.single;
      expect(closing.difference, -1000);
      expect(closing.summary.expectedCash, 43500);
      expect(closing.summary.totalCollected, 53500);
      await store.saveGroup(group.copyWith(sessionPrice: 99000));
      expect(closing.summary.totalCollected, 53500);
      await expectLater(
        store.finalizeSession(sessionId: current.id, actualCash: 43500),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.savePaymentReview(
          ReviewRequest(
            id: store.reviews.single.id,
            studentId: students[0].id,
            paperAmount: 0,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.savePaymentReview(
          ReviewRequest(
            studentId: students[0].id,
            paymentId: payment.id,
            paperAmount: 7500,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.savePaymentReview(
          ReviewRequest(
            studentId: students[4].id,
            sessionId: current.id,
            paperAmount: 10000,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.reviews.single.paperAmount, 7000);
      expect(store.closings.single.actualCash, 42500);
    },
  );

  test(
    'free attendance and zero-valued discounted payment are counts without invented revenue',
    () async {
      await store.closeSession(current.id);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 3,
          kind: SessionKind.free,
          startsAt: DateTime.now().add(const Duration(hours: 3)),
          createdAt: DateTime.now(),
        ),
      );
      final free = store.sessions.last;
      await store.collectAndAttend(
        EntryRequest(
          studentId: students[0].id,
          sessionId: free.id,
          mode: EntryMode.single,
        ),
      );
      final summary = store.sessionFinancialSummary(free.id);
      expect(summary.freeCount, 1);
      expect(summary.totalCollected, 0);
      expect(summary.expectedCash, 0);
      await store.saveStudent(students[0].copyWith(discountPercent: 100));
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 4,
          startsAt: DateTime.now().add(const Duration(hours: 4)),
          createdAt: DateTime.now(),
        ),
      );
      final zero = store.sessions.last;
      await store.collectAndAttend(
        EntryRequest(
          studentId: students[0].id,
          sessionId: zero.id,
          mode: EntryMode.single,
        ),
      );
      final zeroSummary = store.sessionFinancialSummary(zero.id);
      expect(zeroSummary.singlePaymentCount, 1);
      expect(zeroSummary.freeCount, 1);
      expect(zeroSummary.grossAmount, 10000);
      expect(zeroSummary.discountAmount, 10000);
      expect(zeroSummary.totalCollected, 0);
    },
  );

  test(
    'current backup restores finance and rejects forged expected and settlement totals',
    () async {
      final payment = store.payments.firstWhere(
        (e) => e.studentId == students[0].id && e.sessionId == current.id,
      );
      await store.savePaymentReview(
        ReviewRequest(
          studentId: students[0].id,
          paymentId: payment.id,
          paperAmount: 7500,
        ),
      );
      await store.closeSession(current.id);
      await store.finalizeSession(sessionId: current.id, actualCash: 43500);
      final backup = await store.createBackup();
      final original =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      expect((original['data'] as Map)['schemaVersion'], 10);
      for (final field in ['review', 'closing']) {
        final forged = jsonDecode(jsonEncode(original)) as Map<String, dynamic>;
        final data = forged['data'] as Map;
        if (field == 'review') {
          (data['reviews'] as List).first['expectedAmount'] = 7501;
        } else {
          (data['closings'] as List).first['summary']['expectedCash'] = 99999;
        }
        final file = File('${directory.path}/forged-$field.json');
        await file.writeAsString(jsonEncode(forged));
        await expectLater(
          store.restoreBackup(file.path),
          throwsA(isA<CenterException>()),
        );
        expect(store.closings.single.summary.expectedCash, 43500);
        expect(store.reviews.single.expectedAmount, 7500);
        expect(store.currentUser, isNotNull);
      }
      await store.restoreBackup(backup);
      expect(store.currentUser, isNull);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.reviews.single.matched, true);
      expect(store.closings.single.difference, 0);
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.closings, hasLength(1));
      expect(store.reviews, hasLength(1));
    },
  );

  test(
    'schema1 SQLite and backup migrate with empty finance collections without data loss',
    () async {
      final backup = await store.createBackup();
      final old =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final data = old['data'] as Map<String, dynamic>;
      data['schemaVersion'] = 1;
      data.remove('reviews');
      data.remove('closings');
      final legacy = File('${directory.path}/legacy.json');
      await legacy.writeAsString(jsonEncode(old));
      final database = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      await database.update('state', {
        'payload': jsonEncode(data),
      }, where: 'id = 1');
      await database.close();
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.reviews, isEmpty);
      expect(store.closings, isEmpty);
      expect(store.students, hasLength(6));
      expect(store.payments, hasLength(5));
      final payment = store.payments.firstWhere(
        (e) => e.studentId == students[0].id && e.sessionId == current.id,
      );
      await store.savePaymentReview(
        ReviewRequest(
          studentId: students[0].id,
          paymentId: payment.id,
          paperAmount: 7500,
        ),
      );
      expect(store.reviews, hasLength(1));
      await store.restoreBackup(legacy.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.reviews, isEmpty);
      expect(store.payments, hasLength(5));
    },
  );

  test('assistant cannot review or finalize financial data', () async {
    await store.saveStaff(
      name: 'مساعد',
      password: 'assistant-123',
      role: StaffRole.assistant,
    );
    store.signOut();
    await store.signIn('مساعد', 'assistant-123');
    await expectLater(
      store.savePaymentReview(
        ReviewRequest(studentId: students[0].id, paperAmount: 10),
      ),
      throwsA(isA<CenterException>()),
    );
    await expectLater(
      store.finalizeSession(sessionId: current.id, actualCash: 0),
      throwsA(isA<CenterException>()),
    );
    expect(store.reviews, isEmpty);
    expect(store.closings, isEmpty);
  });
}
