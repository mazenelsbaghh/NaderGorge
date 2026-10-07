import 'dart:convert';
import 'dart:io';

import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'test-owner-password';
  late Directory directory;
  late CenterStore store;
  late InstallationAdmin owner;
  late StudyGroup group;
  late Student student;
  late LessonSession session;
  var backupNumber = 0;

  setUp(() async {
    backupNumber = 0;
    directory = await Directory.systemTemp.createTemp('massar-student-cards-');
    final salt = List<int>.generate(24, (i) => i + 1);
    final key = await Pbkdf2(
      macAlgorithm: Hmac.sha256(),
      iterations: 120000,
      bits: 256,
    ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
    owner = InstallationAdmin(
      id: 'card-owner',
      name: 'card-owner',
      credential: {
        'salt': base64Encode(salt),
        'hash': base64Encode(await key.extractBytes()),
        'algorithm': 'pbkdf2-sha256-120000',
      },
    );
    store = await CenterStore.open(directory: directory.path);
    await store.ensureInstallationAdmin(owner);
    await store.signIn(owner.name, password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'المجموعة',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 10000,
        packagePrice: 35000,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        name: 'طالب',
        code: 'C1',
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

  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.ensureInstallationAdmin(owner);
    await store.signIn(owner.name, password);
  }

  Future<File> alteredBackup(void Function(Map<String, dynamic>) change) async {
    final backup = await store.createBackup(
      destination: '${directory.path}/case-backup-${backupNumber++}.json',
    );
    final value =
        jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
    change(value['data'] as Map<String, dynamic>);
    final file = File('${directory.path}/altered.json');
    await file.writeAsString(jsonEncode(value));
    return file;
  }

  test(
    'unset fee blocks collection, explicit zero is paid, and unconfigured generic admin cannot configure',
    () async {
      expect(store.cardSettings.price, isNull);
      expect(store.cardSettings.requirePaymentBeforeReceipt, isTrue);
      expect(store.canReceiveStudentCard(student.id), isFalse);
      await expectLater(
        store.collectStudentCard(studentId: student.id),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.receiveStudentCard(student.id),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.saveCardSettings(const CenterCardSettings(price: -1)),
        throwsA(isA<CenterException>()),
      );
      expect(store.cardSettings.price, isNull);
      await store.saveCardSettings(const CenterCardSettings(price: 0));
      await store.collectStudentCard(studentId: student.id);
      expect(store.cardPayments.single.netAmount, 0);
      await store.receiveStudentCard(student.id);
      expect(store.cardReceipts.single.paymentBypassed, isFalse);
      expect(store.cardReceipts.single.paymentId, store.cardPayments.single.id);
      expect(store.payments, isEmpty);
      final generic = await CenterStore.open(
        directory: '${directory.path}/generic',
      );
      try {
        await generic.setupAdmin(owner.name, password);
        expect(generic.canConfigureCards, isFalse);
        await expectLater(
          generic.saveCardSettings(const CenterCardSettings(price: 100)),
          throwsA(isA<CenterException>()),
        );
      } finally {
        await generic.close();
      }
    },
  );

  for (final role in [
    StaffRole.admin,
    StaffRole.cashier,
    StaffRole.assistant,
  ]) {
    test(
      '${role.name}: only configured owner sets policy; collectors pay/receive and assistants cannot',
      () async {
        await store.saveCardSettings(const CenterCardSettings(price: 17777));
        await store.saveStaff(name: role.name, password: password, role: role);
        await store.signIn(role.name, password);
        expect(store.canConfigureCards, isFalse);
        await expectLater(
          store.saveCardSettings(
            const CenterCardSettings(
              price: 5,
              requirePaymentBeforeReceipt: false,
            ),
          ),
          throwsA(isA<CenterException>()),
        );
        if (role == StaffRole.assistant) {
          await expectLater(
            store.collectStudentCard(studentId: student.id),
            throwsA(isA<CenterException>()),
          );
          await expectLater(
            store.receiveStudentCard(student.id),
            throwsA(isA<CenterException>()),
          );
          expect(store.cardPayments, isEmpty);
        } else {
          await store.collectStudentCard(studentId: student.id);
          expect(store.cardPayments.single.netAmount, 13333);
          expect(store.canReceiveStudentCard(student.id), isTrue);
          await store.receiveStudentCard(student.id);
          expect(store.cardReceipts.single.staffId, store.currentUser!.id);
        }
        store.signOut();
        expect(store.canReceiveStudentCard(student.id), isFalse);
        await expectLater(
          store.collectStudentCard(studentId: student.id),
          throwsA(isA<CenterException>()),
        );
        await expectLater(
          store.receiveStudentCard(student.id),
          throwsA(isA<CenterException>()),
        );
      },
    );
  }

  test(
    'serialized duplicate payment and receipt preserve separate financial snapshots across restart',
    () async {
      await store.saveCardSettings(const CenterCardSettings(price: 17777));
      final payments = await Future.wait(
        List.generate(2, (_) async {
          try {
            await store.collectStudentCard(
              studentId: student.id,
              sessionId: session.id,
            );
            return true;
          } on CenterException {
            return false;
          }
        }),
      );
      expect(payments.where((e) => e), hasLength(1));
      final payment = store.cardPayments.single;
      expect(payment.baseAmount, 17777);
      expect(payment.discountPercent, 25);
      expect(payment.netAmount, 13333);
      expect(payment.groupId, group.id);
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.packages, isEmpty);
      expect(
        store.paymentStatusFor(student.id, session.id).status,
        StudentPaymentStatus.notPaid,
      );
      expect(store.attendanceCount(session.id), 0);
      final summary = store.sessionFinancialSummary(session.id);
      expect(summary.cardPaymentCount, 1);
      expect(summary.cardCollectedAmount, 13333);
      expect(summary.totalCollected, 13333);
      expect(summary.expectedCash, 13333);
      expect(summary.singlePaymentCount, 0);
      expect(summary.packageSalesCount, 0);
      expect(summary.studentCategories, isEmpty);
      await store.saveStudent(student.copyWith(discountPercent: 70));
      await store.saveCardSettings(const CenterCardSettings(price: 23000));
      expect(store.cardPayments.single.toJson(), payment.toJson());
      final receipts = await Future.wait(
        List.generate(2, (_) async {
          try {
            await store.receiveStudentCard(student.id);
            return true;
          } on CenterException {
            return false;
          }
        }),
      );
      expect(receipts.where((e) => e), hasLength(1));
      expect(store.cardReceipts.single.paymentBypassed, isFalse);
      expect(
        store.audit.where((e) => e.action == 'card_payment'),
        hasLength(1),
      );
      expect(
        store.audit.where((e) => e.action == 'card_receipt'),
        hasLength(1),
      );
      await reopen();
      expect(store.cardSettings.price, 23000);
      expect(store.cardPayments.single.toJson(), payment.toJson());
      expect(store.cardReceiptFor(student.id)!.paymentId, payment.id);
      expect(store.canReceiveStudentCard(student.id), isFalse);
      expect(() => store.cardPayments.clear(), throwsUnsupportedError);
    },
  );

  test(
    'owner bypass allows cashier historical receipt without assumed payment and survives stricter future policy',
    () async {
      await store.saveCardSettings(
        const CenterCardSettings(requirePaymentBeforeReceipt: false),
      );
      await store.saveStaff(
        name: 'cashier',
        password: password,
        role: StaffRole.cashier,
      );
      await store.signIn('cashier', password);
      expect(store.canReceiveStudentCard(student.id), isTrue);
      await store.receiveStudentCard(student.id);
      final receipt = store.cardReceipts.single;
      expect(receipt.paymentBypassed, isTrue);
      expect(receipt.paymentId, isNull);
      expect(store.cardPayments, isEmpty);
      expect(store.sessionFinancialSummary(session.id).totalCollected, 0);
      await store.signIn(owner.name, password);
      await store.saveCardSettings(const CenterCardSettings(price: 15000));
      await expectLater(
        store.collectStudentCard(studentId: student.id),
        throwsA(isA<CenterException>()),
      );
      await reopen();
      expect(store.cardReceipts.single.toJson(), receipt.toJson());
      expect(store.cardSettings.requirePaymentBeforeReceipt, isTrue);
      expect(store.cardPayments, isEmpty);
    },
  );

  test(
    'card session cash joins closing without lesson coverage, session edits stop, archived close replays unchanged',
    () async {
      await store.saveCardSettings(const CenterCardSettings(price: 10000));
      await store.collectStudentCard(
        studentId: student.id,
        sessionId: session.id,
        method: 'تحويل',
      );
      await expectLater(
        store.saveSession(session.copyWith(number: 7)),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.cancelSession(session.id),
        throwsA(isA<CenterException>()),
      );
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 7500);
      final original = store.closings.single;
      expect(original.summary.totalCollected, 15000);
      expect(original.summary.expectedCash, 7500);
      expect(original.summary.singlePaymentCount, 1);
      expect(original.summary.cardPaymentCount, 1);
      await store.reopenFinancialClosing(
        closingId: original.id,
        reason: 'تصحيح دخول',
      );
      await store.reverseEntry(
        attendanceId: store.attendances.single.id,
        reason: 'إلغاء التسجيل الخاطئ',
      );
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      expect(store.closings.single.summary.totalCollected, 7500);
      expect(store.closings.single.summary.cardCollectedAmount, 7500);
      expect(store.allClosings.first.toJson(), original.toJson());
      await reopen();
      expect(store.allClosings.first.toJson(), original.toJson());
      expect(store.closings.single.summary.cardPaymentCount, 1);
      await store.saveStudent(
        Student(
          name: 'طالب ثاني',
          code: 'C2',
          groupIds: [group.id],
          createdAt: DateTime.now(),
        ),
      );
      await expectLater(
        store.collectStudentCard(
          studentId: store.students.last.id,
          sessionId: session.id,
        ),
        throwsA(isA<CenterException>()),
      );
      await store.collectStudentCard(studentId: store.students.last.id);
      expect(store.cardPayments.last.groupId, isNull);
      expect(store.cardPayments.last.sessionId, isNull);
      expect(store.closings.single.summary.cardPaymentCount, 1);
    },
  );

  test(
    'legacy backup defaults optional card state and only owner may restore altered card policy',
    () async {
      await store.saveStaff(
        name: 'other-admin',
        password: password,
        role: StaffRole.admin,
      );
      final legacy = await alteredBackup((data) {
        data.remove('cardSettings');
        data.remove('cardPayments');
        data.remove('cardReceipts');
      });
      await store.saveCardSettings(
        const CenterCardSettings(
          price: 20000,
          requirePaymentBeforeReceipt: false,
        ),
      );
      await store.signIn('other-admin', password);
      await expectLater(
        store.restoreBackup(legacy.path),
        throwsA(isA<CenterException>()),
      );
      expect(store.cardSettings.price, 20000);
      expect(store.currentUser!.name, 'other-admin');
      await store.signIn(owner.name, password);
      await store.restoreBackup(legacy.path);
      expect(store.currentUser, isNull);
      await store.signIn(owner.name, password);
      expect(store.cardSettings.price, isNull);
      expect(store.cardSettings.requirePaymentBeforeReceipt, isTrue);
      expect(store.cardPayments, isEmpty);
      expect(store.cardReceipts, isEmpty);
      final schemaOne = await alteredBackup((data) {
        data['schemaVersion'] = 1;
        for (final field in [
          'reviews',
          'closings',
          'paymentChecks',
          'corrections',
          'refunds',
          'cardSettings',
          'cardPayments',
          'cardReceipts',
        ]) {
          data.remove(field);
        }
      });
      await store.restoreBackup(schemaOne.path);
      await store.signIn(owner.name, password);
      expect(store.cardSettings.price, isNull);
      expect(store.students.single.code, 'C1');
    },
  );

  test(
    'backup roundtrip validates card amounts, identities, uniqueness and receipt timestamps before writing',
    () async {
      await store.saveCardSettings(const CenterCardSettings(price: 12345));
      await store.collectStudentCard(
        studentId: student.id,
        sessionId: session.id,
      );
      await store.receiveStudentCard(student.id);
      final payment = store.cardPayments.single.toJson();
      final receipt = store.cardReceipts.single.toJson();
      final valid = await store.createBackup();
      final mutations = <void Function(Map<String, dynamic>)>[
        (data) => (data['cardPayments'] as List).add({
          ...payment,
          'id': 'duplicate-student-payment',
        }),
        (data) => (data['cardReceipts'] as List).add({
          ...receipt,
          'id': 'duplicate-student-receipt',
        }),
        (data) =>
            ((data['cardPayments'] as List).single as Map)['netAmount'] = 5,
        (data) =>
            ((data['cardPayments'] as List).single as Map)['groupId'] = null,
        (data) =>
            ((data['cardReceipts'] as List).single as Map)['paymentBypassed'] =
                true,
        (data) => ((data['cardReceipts'] as List).single as Map)['receivedAt'] =
            DateTime.parse(
              payment['createdAt'] as String,
            ).subtract(const Duration(seconds: 1)).toIso8601String(),
        (data) => ((data['cardPayments'] as List).single as Map)['studentId'] =
            'missing-student',
      ];
      for (final mutate in mutations) {
        final broken = await alteredBackup(mutate);
        await expectLater(
          store.restoreBackup(broken.path),
          throwsA(isA<CenterException>()),
        );
        expect(store.cardPayments.single.toJson(), payment);
        expect(store.cardReceipts.single.toJson(), receipt);
      }
      await store.restoreBackup(valid);
      await store.signIn(owner.name, password);
      expect(store.cardPayments.single.toJson(), payment);
      expect(store.cardReceipts.single.toJson(), receipt);
      await reopen();
      expect(store.cardReceipts.single.toJson(), receipt);
    },
  );

  test(
    'legacy closing omits card detail and survives exact replay after restore',
    () async {
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final legacy = await alteredBackup((data) {
        data.remove('cardSettings');
        data.remove('cardPayments');
        data.remove('cardReceipts');
        final summary =
            ((data['closings'] as List).single as Map)['summary'] as Map;
        summary.remove('cardPaymentCount');
        summary.remove('cardCollectedAmount');
      });
      await store.restoreBackup(legacy.path);
      await store.signIn(owner.name, password);
      expect(store.closings.single.summary.cardPaymentCount, isNull);
      expect(store.closings.single.summary.cardCollectedAmount, isNull);
      await reopen();
      expect(store.closings.single.summary.cardPaymentCount, isNull);
      expect(store.sessionFinancialSummary(session.id).cardPaymentCount, 0);
    },
  );

  test(
    'installed owner matched by canonical name retains existing id and can configure cards',
    () async {
      final other = await CenterStore.open(
        directory: '${directory.path}/retained-owner',
      );
      try {
        await other.setupAdmin(owner.name, password);
        final retainedId = other.currentUser!.id;
        expect(other.canConfigureCards, isFalse);
        other.signOut();
        await other.ensureInstallationAdmin(owner);
        await other.signIn(owner.name, password);
        expect(other.currentUser!.id, retainedId);
        expect(retainedId, isNot(owner.id));
        expect(other.canConfigureCards, isTrue);
        await other.saveCardSettings(const CenterCardSettings(price: 4321));
        expect(other.cardSettings.price, 4321);
      } finally {
        await other.close();
      }
    },
  );

  test(
    'SQLite failed writes rollback card payment, receipt and settings with original diagnostic cause',
    () async {
      await store.saveCardSettings(const CenterCardSettings(price: 10000));
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      Future<void> block() => db.execute(
        "CREATE TRIGGER reject_card BEFORE UPDATE ON state BEGIN SELECT RAISE(ABORT, 'blocked'); END",
      );
      Future<void> unblock() => db.execute('DROP TRIGGER reject_card');
      final audits = store.audit.length;
      await block();
      await expectLater(
        store.collectStudentCard(studentId: student.id),
        throwsA(
          isA<CenterException>().having(
            (e) => e.cause,
            'underlying SQLite error',
            isNotNull,
          ),
        ),
      );
      await expectLater(
        store.saveCardSettings(const CenterCardSettings(price: 20000)),
        throwsA(isA<CenterException>()),
      );
      expect(store.cardSettings.price, 10000);
      expect(store.cardPayments, isEmpty);
      expect(store.audit.length, audits);
      await unblock();
      await store.collectStudentCard(studentId: student.id);
      await block();
      await expectLater(
        store.receiveStudentCard(student.id),
        throwsA(isA<CenterException>()),
      );
      expect(store.cardReceipts, isEmpty);
      expect(store.canReceiveStudentCard(student.id), isTrue);
      await unblock();
      await db.close();
      await reopen();
      expect(store.cardPayments, hasLength(1));
      expect(store.cardReceipts, isEmpty);
      expect(store.cardSettings.price, 10000);
    },
  );
}
