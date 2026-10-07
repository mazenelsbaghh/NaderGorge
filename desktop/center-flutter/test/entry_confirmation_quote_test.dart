import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'entry-quote-password';
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  late LessonSession session;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-entry-quote-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', password);
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
        packagePrice: 50000,
        twoSessionPrice: 27777,
        threeSessionPrice: 36666,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        name: 'طالب',
        code: 'Q1',
        discountPercent: 25,
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
  EntryRequest request({
    EntryMode mode = EntryMode.single,
    int sessions = 4,
    EntryConfirmation? confirmation,
    String? originalId,
    String? sessionId,
  }) => EntryRequest(
    studentId: student.id,
    sessionId: sessionId ?? session.id,
    mode: mode,
    packageSessions: sessions,
    confirmation: confirmation,
    originalAttendanceId: originalId,
  );
  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير', password);
  }

  test(
    'unchanged independently recreated confirmation values succeed without persisting the ephemeral quote',
    () async {
      final first = store.entryConfirmationFor(request());
      final second = store.entryConfirmationFor(request());
      expect(first, second);
      expect(first.hashCode, second.hashCode);
      expect(first.staffId, store.currentUser!.id);
      expect(first.groupId, group.id);
      expect(first.sessionKind, SessionKind.counted);
      expect(first.baseAmount, 10000);
      expect(first.discountPercent, 25);
      expect(first.netAmount, 7500);
      expect(first.eligibleRemaining, 0);
      await store.collectAndAttend(request(confirmation: first));
      expect(store.payments.single.netAmount, 7500);
      expect(store.attendances.single.studentId, student.id);
      final backup = await store.createBackup(
        destination: '${directory.path}/quote.json',
      );
      final data =
          (jsonDecode(await File(backup).readAsString()) as Map)['data'] as Map;
      expect(jsonEncode(data).contains('confirmation'), isFalse);
      await reopen();
      expect(store.payments.single.netAmount, 7500);
      expect(store.attendances, hasLength(1));
    },
  );

  for (final count in [2, 3, 4]) {
    test(
      '$count-session package confirmation uses independent price and commits the exact quoted amount',
      () async {
        final quote = store.entryConfirmationFor(
          request(mode: EntryMode.package, sessions: count),
        );
        final base = group.packageAmountFor(count)!;
        expect(quote.baseAmount, base);
        expect(quote.netAmount, (base * 75 + 50) ~/ 100);
        await store.collectAndAttend(
          request(
            mode: EntryMode.package,
            sessions: count,
            confirmation: quote,
          ),
        );
        expect(store.payments.single.netAmount, quote.netAmount);
        expect(store.packages.single.totalSessions, count);
        expect(store.packages.single.remaining, count - 1);
      },
    );
  }

  test(
    'existing prepaid balance quotes no new collection, consumes one eligible credit, and preserves the original payment',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: student.id, groupId: group.id, sessions: 3),
      );
      final originalPayment = store.payments.single.toJson();
      final quote = store.entryConfirmationFor(
        request(mode: EntryMode.package),
      );
      expect(quote.eligibleRemaining, 3);
      expect(quote.baseAmount, 0);
      expect(quote.netAmount, 0);
      expect(quote.discountPercent, 25);
      await store.collectAndAttend(
        request(mode: EntryMode.package, confirmation: quote),
      );
      expect(store.payments.single.toJson(), originalPayment);
      expect(store.packages.single.remaining, 2);
      expect(store.attendances.single.packageId, store.packages.single.id);
    },
  );

  for (final change in [
    'price',
    'equivalent-net-discount',
    'balance',
    'group',
    'kind',
    'staff',
  ]) {
    test(
      'stale $change confirmation rejects all entry effects and audit while a refreshed quote succeeds',
      () async {
        var mode = EntryMode.single;
        if (change == 'balance') mode = EntryMode.package;
        if (change == 'equivalent-net-discount') {
          await store.saveGroup(group.copyWith(sessionPrice: 1));
        } else if (change == 'kind') {
          await store.saveGroup(group.copyWith(sessionPrice: 0));
        }
        final quote = store.entryConfirmationFor(request(mode: mode));
        switch (change) {
          case 'price':
            await store.saveGroup(group.copyWith(sessionPrice: 20000));
          case 'equivalent-net-discount':
            await store.saveStudent(student.copyWith(discountPercent: 26));
            expect(
              store.entryConfirmationFor(request()).netAmount,
              quote.netAmount,
            );
          case 'balance':
            await store.renewPackage(
              PackageRequest(studentId: student.id, groupId: group.id),
            );
          case 'group':
            await store.saveGroup(group.copyWith(id: '', name: 'مجموعة ثانية'));
            final other = store.groups.last;
            await store.saveStudent(
              student.copyWith(groupIds: [group.id, other.id]),
            );
            await store.saveSession(session.copyWith(groupId: other.id));
          case 'kind':
            await store.saveSession(session.copyWith(kind: SessionKind.free));
            expect(
              store.entryConfirmationFor(request()).netAmount,
              quote.netAmount,
            );
          case 'staff':
            await store.saveStaff(
              name: 'cashier',
              password: password,
              role: StaffRole.cashier,
            );
            await store.signIn('cashier', password);
        }
        final paymentsBefore = store.payments
            .map((payment) => payment.toJson())
            .toList();
        final packagesBefore = store.packages
            .map((package) => package.toJson())
            .toList();
        final auditCount = store.audit.length;
        await expectLater(
          store.collectAndAttend(request(mode: mode, confirmation: quote)),
          throwsA(
            isA<CenterException>().having(
              (error) => error.message,
              'review prompt',
              contains('راجع الدفع'),
            ),
          ),
        );
        expect(store.attendances, isEmpty);
        expect(
          store.payments.map((payment) => payment.toJson()).toList(),
          paymentsBefore,
        );
        expect(
          store.packages.map((package) => package.toJson()).toList(),
          packagesBefore,
        );
        expect(store.audit.length, auditCount);
        final refreshed = store.entryConfirmationFor(request(mode: mode));
        expect(refreshed, isNot(quote));
        await store.collectAndAttend(
          request(mode: mode, confirmation: refreshed),
        );
        expect(store.attendances, hasLength(1));
        expect(store.audit.length, auditCount + 1);
      },
    );
  }

  test(
    'free and extra session quotes follow actual kind while paid-absence makeup quotes and commits zero charge',
    () async {
      await store.saveSession(session.copyWith(kind: SessionKind.free));
      var quote = store.entryConfirmationFor(request(mode: EntryMode.package));
      expect(quote.baseAmount, 0);
      expect(quote.netAmount, 0);
      expect(quote.discountPercent, 25);
      await store.collectAndAttend(
        request(mode: EntryMode.package, confirmation: quote),
      );
      expect(store.payments, isEmpty);
      await store.closeSession(session.id);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          kind: SessionKind.extra,
          extraPrice: 14000,
          startsAt: DateTime.now().add(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      final extra = store.sessions.last;
      quote = store.entryConfirmationFor(
        request(mode: EntryMode.package, sessionId: extra.id),
      );
      expect(quote.baseAmount, 14000);
      expect(quote.netAmount, 10500);
      await store.collectAndAttend(
        request(
          mode: EntryMode.package,
          sessionId: extra.id,
          confirmation: quote,
        ),
      );
      expect(store.payments.single.packageId, isNull);
      expect(store.payments.single.netAmount, 10500);
      await store.closeSession(extra.id);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 3,
          startsAt: DateTime.now().add(const Duration(hours: 3)),
          createdAt: DateTime.now(),
        ),
      );
      final missed = store.sessions.last;
      await store.renewPackage(
        PackageRequest(
          studentId: student.id,
          groupId: group.id,
          sessions: 2,
          sessionId: missed.id,
        ),
      );
      await store.closeSession(missed.id);
      final absence = store.attendances.singleWhere(
        (attendance) => attendance.sessionId == missed.id,
      );
      expect(absence.status, AttendanceStatus.absent);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 4,
          startsAt: DateTime.now().add(const Duration(hours: 4)),
          createdAt: DateTime.now(),
        ),
      );
      final target = store.sessions.last;
      quote = store.entryConfirmationFor(
        request(
          mode: EntryMode.makeup,
          sessionId: target.id,
          originalId: absence.id,
        ),
      );
      expect(quote.baseAmount, 0);
      expect(quote.netAmount, 0);
      expect(quote.eligibleRemaining, 1);
      final paymentCount = store.payments.length;
      await store.collectAndAttend(
        request(
          mode: EntryMode.makeup,
          sessionId: target.id,
          originalId: absence.id,
          confirmation: quote,
        ),
      );
      expect(store.payments.length, paymentCount);
      expect(store.packages.single.remaining, 1);
      expect(store.attendances.last.status, AttendanceStatus.makeup);
    },
  );

  test(
    'snapshot permissions and failed SQLite commit preserve the quote; retry and legacy unquoted requests still work',
    () async {
      final quote = store.entryConfirmationFor(request());
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      final auditCount = store.audit.length;
      try {
        await db.execute(
          "CREATE TRIGGER reject_entry_quote BEFORE UPDATE ON state BEGIN SELECT RAISE(ABORT, 'blocked'); END",
        );
        await expectLater(
          store.collectAndAttend(request(confirmation: quote)),
          throwsA(
            isA<CenterException>().having(
              (error) => error.cause,
              'SQLite cause',
              isNotNull,
            ),
          ),
        );
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.audit.length, auditCount);
        expect(store.entryConfirmationFor(request()), quote);
        await db.execute('DROP TRIGGER reject_entry_quote');
      } finally {
        await db.close();
      }
      await store.collectAndAttend(request(confirmation: quote));
      await store.closeSession(session.id);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: DateTime.now().add(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      await store.collectAndAttend(request(sessionId: store.sessions.last.id));
      expect(store.payments, hasLength(2));
      await store.saveStaff(
        name: 'assistant',
        password: password,
        role: StaffRole.assistant,
      );
      await store.signIn('assistant', password);
      expect(
        () => store.entryConfirmationFor(request()),
        throwsA(isA<CenterException>()),
      );
      store.signOut();
      expect(
        () => store.entryConfirmationFor(request()),
        throwsA(isA<CenterException>()),
      );
    },
  );
}
