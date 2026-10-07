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
  var sessionNumber = 0;

  setUp(() async {
    sessionNumber = 0;
    directory = await Directory.systemTemp.createTemp(
      'massar-variable-packages-',
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
        sessionPrice: 10000,
        packagePrice: 41001,
        twoSessionPrice: 17777,
        threeSessionPrice: 23333,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        name: 'طالب',
        code: 'P1',
        groupIds: [group.id],
        discountPercent: 25,
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    student = store.students.single;
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<LessonSession> nextSession({
    SessionKind kind = SessionKind.counted,
  }) async {
    sessionNumber++;
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: sessionNumber,
        kind: kind,
        extraPrice: kind == SessionKind.extra ? 8000 : 0,
        startsAt: DateTime.now().add(Duration(hours: sessionNumber)),
        createdAt: DateTime.now(),
      ),
    );
    return store.sessions.last;
  }

  Future<void> enter(
    LessonSession session, {
    EntryMode mode = EntryMode.package,
    int sessions = 4,
    String? original,
  }) => store.collectAndAttend(
    EntryRequest(
      studentId: student.id,
      sessionId: session.id,
      mode: mode,
      packageSessions: sessions,
      originalAttendanceId: original,
    ),
  );

  Future<void> reopenStore() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير', 'test-pass-123');
  }

  for (final count in [2, 3]) {
    test(
      '$count-session purchase uses independent price, consumes consecutive counted absences and excludes free/extra classes',
      () async {
        final first = await nextSession();
        await enter(first, sessions: count);
        final purchased = store.packages.single;
        final payment = store.payments.single;
        final expectedBase = count == 2 ? 17777 : 23333;
        final expectedNet = count == 2 ? 13333 : 17500;
        expect(purchased.totalSessions, count);
        expect(purchased.remaining, count - 1);
        expect(payment.baseAmount, expectedBase);
        expect(payment.netAmount, expectedNet);
        expect(
          payment.description,
          count == 2 ? contains('حصتين') : contains('٣'),
        );
        await store.closeSession(first.id);
        await store.finalizeSession(
          sessionId: first.id,
          actualCash: expectedNet,
        );
        final saved = store.closings.single.summary.toJson();
        await store.saveGroup(
          store.groups.single.copyWith(
            packagePrice: 1,
            twoSessionPrice: 99999,
            threeSessionPrice: 88888,
          ),
        );
        await store.saveStudent(
          store.students.single.copyWith(discountPercent: 100),
        );
        for (final kind in [SessionKind.free, SessionKind.extra]) {
          final skipped = await nextSession(kind: kind);
          await store.closeSession(skipped.id);
          expect(store.packages.single.remaining, count - 1);
          expect(store.attendances.last.packageId, isNull);
        }
        AttendanceRecord? paidAbsence;
        for (var used = 1; used < count; used++) {
          final counted = await nextSession();
          await store.closeSession(counted.id);
          paidAbsence = store.attendances.last;
          expect(paidAbsence.packageId, purchased.id);
          expect(store.packages.single.remaining, count - used - 1);
        }
        final uncovered = await nextSession();
        await store.closeSession(uncovered.id);
        expect(store.attendances.last.packageId, isNull);
        expect(store.payments, hasLength(1));
        expect(store.payments.single.netAmount, expectedNet);
        // Makeup coverage survives exhaustion of a shorter package, without a new charge.
        final makeup = await nextSession();
        await enter(makeup, mode: EntryMode.makeup, original: paidAbsence!.id);
        expect(store.attendances.last.status, AttendanceStatus.makeup);
        expect(store.packages.single.remaining, 0);
        expect(store.payments, hasLength(1));
        await reopenStore();
        expect(store.packages.single.totalSessions, count);
        expect(store.packages.single.remaining, 0);
        expect(store.closings.single.summary.toJson(), saved);
      },
    );

    test(
      '$count-session unused refund and entry corrections preserve original money and full quantity',
      () async {
        final first = await nextSession();
        await enter(first, mode: EntryMode.single);
        final originalSingle = store.payments.single;
        await store.correctEntry(
          attendanceId: store.attendances.single.id,
          mode: EntryMode.package,
          packageSessions: count,
          reason: 'اختار الباقة',
        );
        final package = store.packages.single;
        final purchase = store.payments.single;
        expect(package.totalSessions, count);
        expect(package.remaining, count - 1);
        expect(store.refunds.single.amount, originalSingle.netAmount);
        expect(
          store.sessionFinancialSummary(first.id).totalCollected,
          purchase.netAmount,
        );
        // Request size applies only if a new purchase is needed. Existing balance stays intact.
        await store.correctEntry(
          attendanceId: store.attendances.single.id,
          mode: EntryMode.package,
          packageSessions: count == 2 ? 3 : 2,
          reason: 'إعادة ربط الحضور بالرصيد الموجود',
        );
        expect(store.packages.single.id, package.id);
        expect(store.packages.single.totalSessions, count);
        expect(store.packages.single.remaining, count - 1);
        expect(store.allPayments, hasLength(2));
        await store.saveGroup(
          store.groups.single.copyWith(
            twoSessionPrice: 10,
            threeSessionPrice: 10,
          ),
        );
        await store.correctEntry(
          attendanceId: store.attendances.single.id,
          mode: EntryMode.single,
          reason: 'دفع الحصة فقط',
        );
        expect(store.packages, isEmpty);
        expect(store.allPackages.single.totalSessions, count);
        expect(store.allPackages.single.remaining, count);
        expect(store.refunds.last.amount, purchase.netAmount);
        expect(store.sessionFinancialSummary(first.id).totalCollected, 7500);
        await store.closeSession(first.id);
        await store.finalizeSession(sessionId: first.id, actualCash: 7500);
        final snapshot = store.closings.single.summary.toJson();
        await store.reopenFinancialClosing(
          closingId: store.closings.single.id,
          reason: 'تصحيح حضور',
        );
        await store.reverseEntry(
          attendanceId: store.attendances.single.id,
          reason: 'لم يحضر الطالب',
        );
        await store.finalizeSession(sessionId: first.id, actualCash: 0);
        expect(store.allClosings.first.summary.toJson(), snapshot);
        final backup = await store.createBackup(
          destination: '${directory.path}/corrected-$count.json',
        );
        await reopenStore();
        expect(store.allClosings.first.summary.toJson(), snapshot);
        await store.restoreBackup(backup);
        await store.signIn('مدير', 'test-pass-123');
        expect(store.allPackages.single.totalSessions, count);
        expect(store.allPackages.single.remaining, count);
        expect(store.closings.single.summary.totalCollected, 0);
      },
    );
  }

  test(
    'mixed 2/3 purchases consume FIFO and unused shorter package refunds use their purchase snapshots',
    () async {
      final first = await nextSession();
      await store.renewPackage(
        PackageRequest(
          studentId: student.id,
          groupId: group.id,
          sessions: 2,
          sessionId: first.id,
        ),
      );
      final two = store.packages.single;
      await store.renewPackage(
        PackageRequest(
          studentId: student.id,
          groupId: group.id,
          sessions: 3,
          sessionId: first.id,
        ),
      );
      final three = store.packages.last;
      final threeAmount = store.payments.last.netAmount;
      await enter(first, sessions: 3);
      expect(store.attendances.single.packageId, two.id);
      expect(store.remainingFor(student.id, group.id), 4);
      await store.closeSession(first.id);
      final second = await nextSession();
      await store.closeSession(second.id);
      expect(store.attendances.last.packageId, two.id);
      expect(store.packages.first.remaining, 0);
      await store.saveGroup(store.groups.single.copyWith(threeSessionPrice: 1));
      await store.saveStudent(
        store.students.single.copyWith(discountPercent: 100),
      );
      await store.refundPackage(
        packageId: three.id,
        reason: 'إلغاء الباقة الإضافية',
      );
      expect(store.refunds.single.amount, threeAmount);
      expect(store.packages.single.id, two.id);
      expect(store.allPackages.last.remaining, 3);
      await expectLater(
        store.refundPackage(packageId: two.id, reason: 'باقة مستخدمة'),
        throwsA(isA<CenterException>()),
      );
      await reopenStore();
      expect(store.packages.single.remaining, 0);
      expect(store.allPackages.last.totalSessions, 3);
      expect(store.refunds.single.amount, threeAmount);
    },
  );

  test(
    'unconfigured shorter prices and invalid counts reject atomic commands; explicit zero price permits a real package',
    () async {
      // Existing groups have no inferred 2/3 price even if the four-session price is set.
      final missing = StudyGroup.fromJson(
        Map<String, dynamic>.from(group.toJson())
          ..remove('twoSessionPrice')
          ..remove('threeSessionPrice'),
      );
      await store.saveGroup(missing);
      final first = await nextSession();
      final auditCount = store.audit.length;
      for (final count in [2, 3]) {
        await expectLater(
          store.renewPackage(
            PackageRequest(
              studentId: student.id,
              groupId: group.id,
              sessions: count,
            ),
          ),
          throwsA(
            isA<CenterException>().having(
              (e) => e.message,
              'configuration error',
              contains('حدد سعر'),
            ),
          ),
        );
        await expectLater(
          enter(first, sessions: count),
          throwsA(isA<CenterException>()),
        );
      }
      for (final count in [0, 1, 5, -2]) {
        await expectLater(
          store.renewPackage(
            PackageRequest(
              studentId: student.id,
              groupId: group.id,
              sessions: count,
            ),
          ),
          throwsA(isA<CenterException>()),
        );
        await expectLater(
          enter(first, sessions: count),
          throwsA(isA<CenterException>()),
        );
      }
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.audit.length, auditCount);
      await expectLater(
        store.saveGroup(store.groups.single.copyWith(twoSessionPrice: -1)),
        throwsA(isA<CenterException>()),
      );
      await store.saveGroup(store.groups.single.copyWith(twoSessionPrice: 0));
      await enter(first, sessions: 2);
      expect(store.packages.single.totalSessions, 2);
      expect(store.payments.single.baseAmount, 0);
      expect(store.payments.single.netAmount, 0);
      expect(store.packages.single.remaining, 1);
      final originalAttendance = store.attendances.single.id;
      final auditAfterEntry = store.audit.length;
      await expectLater(
        store.correctEntry(
          attendanceId: originalAttendance,
          mode: EntryMode.package,
          packageSessions: 5,
          reason: 'عدد غير صالح',
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.attendances.single.id, originalAttendance);
      expect(store.packages.single.remaining, 1);
      expect(store.refunds, isEmpty);
      expect(store.corrections, isEmpty);
      expect(store.audit.length, auditAfterEntry);
    },
  );

  test(
    'legacy four-session SQLite and backups default total4 and preserve exact old closing replay',
    () async {
      final first = await nextSession();
      await enter(first);
      await store.closeSession(first.id);
      await store.finalizeSession(sessionId: first.id, actualCash: 30751);
      final snapshot = store.closings.single.summary.toJson();
      final dbPath = store.databasePath;
      await store.close();
      final db = await databaseFactoryFfi.openDatabase(dbPath);
      final rows = await db.query('state');
      final state =
          jsonDecode(rows.single['payload'] as String) as Map<String, dynamic>;
      (state['packages'] as List).single.remove('totalSessions');
      (state['groups'] as List).single.remove('twoSessionPrice');
      (state['groups'] as List).single.remove('threeSessionPrice');
      await db.update(
        'state',
        {'payload': jsonEncode(state)},
        where: 'id = ?',
        whereArgs: [1],
      );
      await db.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.packages.single.totalSessions, 4);
      expect(store.packages.single.remaining, 3);
      expect(store.groups.single.packageAmountFor(2), isNull);
      expect(store.groups.single.packageAmountFor(3), isNull);
      expect(store.closings.single.summary.toJson(), snapshot);
      final backup = await store.createBackup(
        destination: '${directory.path}/legacy4.json',
      );
      final envelope =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      ((envelope['data'] as Map)['packages'] as List).single.remove(
        'totalSessions',
      );
      final legacyFile = File('${directory.path}/legacy4-missing-total.json');
      await legacyFile.writeAsString(jsonEncode(envelope));
      await store.restoreBackup(legacyFile.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.packages.single.totalSessions, 4);
      await store.reopenFinancialClosing(
        closingId: store.closings.single.id,
        reason: 'مراجعة قديمة',
      );
      await store.finalizeSession(sessionId: first.id, actualCash: 30751);
      expect(store.allClosings.first.summary.toJson(), snapshot);
      await reopenStore();
      expect(store.allClosings.first.summary.toJson(), snapshot);
      expect(store.packages.single.remaining, 3);
    },
  );

  test(
    'forged package totals and failed SQLite package correction retain original evidence and balance',
    () async {
      final first = await nextSession();
      await enter(first, sessions: 2);
      final original = store.attendances.single.id;
      final payment = store.payments.single;
      final backup = await store.createBackup(
        destination: '${directory.path}/valid2.json',
      );
      final envelope =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      for (final mutation in ['invalidTotal', 'remaining', 'missingTotal']) {
        final forged = jsonDecode(jsonEncode(envelope)) as Map<String, dynamic>;
        final package = ((forged['data'] as Map)['packages'] as List).single;
        if (mutation == 'invalidTotal') {
          package['totalSessions'] = 5;
        } else if (mutation == 'remaining') {
          package['remaining'] = 3;
        } else {
          package.remove('totalSessions');
        }
        final file = File('${directory.path}/forged-$mutation.json');
        await file.writeAsString(jsonEncode(forged));
        await expectLater(
          store.restoreBackup(file.path),
          throwsA(isA<CenterException>()),
        );
        expect(store.packages.single.totalSessions, 2);
        expect(store.packages.single.remaining, 1);
      }
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      await db.execute(
        "CREATE TRIGGER reject_package BEFORE UPDATE ON state BEGIN SELECT RAISE(ABORT, 'blocked'); END",
      );
      await expectLater(
        store.correctEntry(
          attendanceId: original,
          mode: EntryMode.single,
          reason: 'تصحيح مالي',
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.attendances.single.id, original);
      expect(store.packages.single.remaining, 1);
      expect(store.payments.single.id, payment.id);
      expect(store.refunds, isEmpty);
      expect(store.corrections, isEmpty);
      await db.execute('DROP TRIGGER reject_package');
      await db.close();
      await reopenStore();
      expect(store.packages.single.totalSessions, 2);
      expect(store.packages.single.remaining, 1);
      expect(store.payments.single.id, payment.id);
    },
  );
}
