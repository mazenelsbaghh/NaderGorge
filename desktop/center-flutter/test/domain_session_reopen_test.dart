import 'dart:convert';
import 'dart:io';

import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'session-reopen-pass';
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  var sessionNumber = 0;

  setUp(() async {
    sessionNumber = 0;
    directory = await Directory.systemTemp.createTemp('massar-session-reopen-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', password);
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
        packagePrice: 36000,
        twoSessionPrice: 17777,
        threeSessionPrice: 26666,
      ),
    );
    group = store.groups.single;
    await store.saveStudent(
      Student(
        name: 'طالب',
        code: 'R1',
        groupIds: [group.id],
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    student = store.students.single;
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });
  Future<LessonSession> addSession({
    SessionKind kind = SessionKind.counted,
    String? groupId,
  }) async {
    sessionNumber++;
    await store.saveSession(
      LessonSession(
        groupId: groupId ?? group.id,
        number: sessionNumber,
        kind: kind,
        extraPrice: kind == SessionKind.extra ? 5000 : 0,
        startsAt: DateTime.now().add(Duration(hours: sessionNumber)),
        createdAt: DateTime.now(),
      ),
    );
    return store.sessions.last;
  }

  EntryRequest entry(
    LessonSession session, {
    EntryMode mode = EntryMode.package,
    int count = 4,
    EntryConfirmation? quote,
    String? originalId,
  }) => EntryRequest(
    studentId: student.id,
    sessionId: session.id,
    mode: mode,
    packageSessions: count,
    confirmation: quote,
    originalAttendanceId: originalId,
  );
  Future<void> reopenStore() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير', password);
  }

  test(
    'reopening paid absence at zero balance preserves original credit; quoted L/M attendance and reclosure never double consume',
    () async {
      final first = await addSession();
      await store.collectAndAttend(entry(first, count: 2));
      await store.closeSession(first.id);
      final second = await addSession();
      await store.closeSession(second.id);
      final absence = store.attendances.singleWhere(
        (row) => row.sessionId == second.id,
      );
      final originalPayment = store.payments.single.toJson();
      expect(store.packages.single.remaining, 0);
      await store.reopenSession(second.id);
      expect(
        store.attendances
            .singleWhere((row) => row.sessionId == second.id)
            .toJson(),
        absence.toJson(),
      );
      expect(store.packages.single.remaining, 0);
      expect(store.eligibleRemainingFor(student.id, second.id), 1);
      for (final mode in [EntryMode.single, EntryMode.package]) {
        final quote = store.entryConfirmationFor(entry(second, mode: mode));
        expect(quote.baseAmount, 0);
        expect(quote.netAmount, 0);
        expect(quote.eligibleRemaining, 1);
      }
      final quote = store.entryConfirmationFor(entry(second));
      await store.collectAndAttend(entry(second, quote: quote));
      expect(store.packages.single.remaining, 0);
      expect(store.payments.single.toJson(), originalPayment);
      expect(
        store.attendances
            .singleWhere((row) => row.sessionId == second.id)
            .status,
        AttendanceStatus.present,
      );
      expect(
        store.allAttendances
            .singleWhere((row) => row.id == absence.id)
            .toJson(),
        absence.toJson(),
      );
      expect(store.corrections.single.action, CorrectionAction.absencePresent);
      await expectLater(
        store.collectAndAttend(entry(second)),
        throwsA(isA<CenterException>()),
      );
      final attendanceCount = store.allAttendances.length;
      await store.closeSession(second.id);
      expect(store.allAttendances, hasLength(attendanceCount));
      expect(store.packages.single.remaining, 0);
      await store.reopenSession(second.id);
      await store.closeSession(second.id);
      expect(store.packages.single.remaining, 0);
      expect(store.payments, hasLength(1));
      await reopenStore();
      expect(store.packages.single.remaining, 0);
      expect(store.allAttendances, hasLength(attendanceCount));
    },
  );

  for (final count in [2, 3]) {
    test(
      'unpaid absence can buy $count-session package after reopen with independent quote and original absence retained',
      () async {
        final session = await addSession();
        await store.closeSession(session.id);
        final original = store.attendances.single;
        await store.reopenSession(session.id);
        final quote = store.entryConfirmationFor(entry(session, count: count));
        expect(quote.baseAmount, group.packageAmountFor(count));
        await store.collectAndAttend(
          entry(session, count: count, quote: quote),
        );
        expect(store.packages.single.remaining, count - 1);
        expect(store.payments.single.netAmount, quote.netAmount);
        expect(store.attendances.single.status, AttendanceStatus.present);
        expect(store.allAttendances.first.id, original.id);
        expect(store.allAttendances.first.packageId, isNull);
        expect(
          store.corrections.single.replacementAttendanceId,
          store.attendances.single.id,
        );
        await store.closeSession(session.id);
        expect(store.packages.single.remaining, count - 1);
        await reopenStore();
        expect(store.attendances.single.packageId, store.packages.single.id);
      },
    );
  }

  test(
    'automatic financial reopen archives unchanged snapshot while class open accepts new card fee and new lesson payment before refinalizing',
    () async {
      final salt = List<int>.generate(24, (index) => index + 1);
      final key = await Pbkdf2(
        macAlgorithm: Hmac.sha256(),
        iterations: 120000,
        bits: 256,
      ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
      await store.ensureInstallationAdmin(
        InstallationAdmin(
          id: 'reopen-owner',
          name: 'مدير',
          credential: {
            'salt': base64Encode(salt),
            'hash': base64Encode(await key.extractBytes()),
            'algorithm': 'pbkdf2-sha256-120000',
          },
        ),
      );
      await store.signIn('مدير', password);
      await store.saveCardSettings(const CenterCardSettings(price: 3000));
      final session = await addSession();
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final originalClosing = store.closings.single;
      await store.reopenSession(session.id);
      expect(store.closings, isEmpty);
      expect(store.allClosings.single.toJson(), originalClosing.toJson());
      expect(store.corrections.single.action, CorrectionAction.closingReopened);
      expect(store.corrections.single.reason, 'إعادة فتح الحصة للتحضير');
      expect(store.payments, isEmpty);
      expect(store.attendanceCount(session.id), 0);
      final backup = await store.createBackup(
        destination: '${directory.path}/open-with-archive.json',
      );
      await store.restoreBackup(backup);
      await store.signIn('مدير', password);
      expect(store.sessions.single.status, SessionStatus.open);
      expect(store.allClosings.single.toJson(), originalClosing.toJson());
      await reopenStore();
      await store.collectStudentCard(
        studentId: student.id,
        sessionId: session.id,
      );
      final quote = store.entryConfirmationFor(
        entry(session, mode: EntryMode.single),
      );
      await store.collectAndAttend(
        entry(session, mode: EntryMode.single, quote: quote),
      );
      expect(store.allClosings.single.toJson(), originalClosing.toJson());
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 13000);
      expect(store.closings.single.summary.cardCollectedAmount, 3000);
      expect(store.closings.single.summary.totalCollected, 13000);
      expect(store.allClosings.first.toJson(), originalClosing.toJson());
      await reopenStore();
      expect(store.allClosings.first.toJson(), originalClosing.toJson());
      expect(store.closings.single.actualCash, 13000);
    },
  );

  for (final usage in ['attended', 'closed', 'payment']) {
    test(
      'later counted $usage prevents reopening original while future empty schedules remain safe',
      () async {
        final first = await addSession();
        await store.startSession(first.id);
        await store.closeSession(first.id);
        final later = await addSession();
        await store.reopenSession(first.id);
        await expectLater(
          store.collectAndAttend(entry(later, mode: EntryMode.single)),
          throwsA(isA<CenterException>()),
        );
        await store.closeSession(first.id);
        switch (usage) {
          case 'attended':
            await store.collectAndAttend(entry(later, mode: EntryMode.single));
          case 'closed':
            await store.closeSession(later.id);
          case 'payment':
            await store.renewPackage(
              PackageRequest(
                studentId: student.id,
                groupId: group.id,
                sessionId: later.id,
              ),
            );
        }
        final audits = store.audit.length;
        await expectLater(
          store.reopenSession(first.id),
          throwsA(
            isA<CenterException>().having(
              (error) => error.message,
              'chronology reason',
              contains('تالية'),
            ),
          ),
        );
        expect(store.sessions.first.status, SessionStatus.closed);
        expect(store.audit.length, audits);
      },
    );
  }

  test(
    'later free/extra makeup does not block reopening but consumed original absence cannot be changed to present',
    () async {
      final originalSession = await addSession();
      await store.renewPackage(
        PackageRequest(studentId: student.id, groupId: group.id, sessions: 2),
      );
      await store.closeSession(originalSession.id);
      final absence = store.attendances.single;
      final target = await addSession(kind: SessionKind.extra);
      await store.collectAndAttend(
        entry(target, mode: EntryMode.makeup, originalId: absence.id),
      );
      await store.reopenSession(originalSession.id);
      final payments = store.payments.length;
      final audits = store.audit.length;
      await expectLater(
        store.collectAndAttend(entry(originalSession)),
        throwsA(
          isA<CenterException>().having(
            (error) => error.message,
            'makeup dependency',
            contains('التعويض'),
          ),
        ),
      );
      expect(store.attendances.first.id, absence.id);
      expect(store.payments.length, payments);
      expect(store.audit.length, audits);
      expect(store.packages.single.remaining, 1);
      await store.closeSession(originalSession.id);
      expect(store.packages.single.remaining, 1);
    },
  );

  for (final role in [
    StaffRole.admin,
    StaffRole.cashier,
    StaffRole.assistant,
  ]) {
    test(
      '${role.name} reopen permission, closed-only and duplicate serialized commands are enforced',
      () async {
        final session = await addSession();
        await store.closeSession(session.id);
        await store.saveStaff(name: role.name, password: password, role: role);
        await store.signIn(role.name, password);
        if (role == StaffRole.assistant) {
          await expectLater(
            store.reopenSession(session.id),
            throwsA(isA<CenterException>()),
          );
          expect(store.sessions.single.status, SessionStatus.closed);
        } else {
          final outcomes = await Future.wait(
            List.generate(2, (_) async {
              try {
                await store.reopenSession(session.id);
                return true;
              } on CenterException {
                return false;
              }
            }),
          );
          expect(outcomes.where((success) => success), hasLength(1));
          expect(
            store.audit.where((row) => row.action == 'session_reopen'),
            hasLength(1),
          );
          await expectLater(
            store.reopenSession(session.id),
            throwsA(isA<CenterException>()),
          );
        }
        store.signOut();
        await expectLater(
          store.reopenSession(session.id),
          throwsA(isA<CenterException>()),
        );
      },
    );
  }

  test(
    'even an empty reopened class retains archived closing identity and cannot be edited or canceled',
    () async {
      await store.saveGroup(group.copyWith(id: '', name: 'مجموعة بلا طلبة'));
      final emptyGroup = store.groups.last;
      final session = await addSession(groupId: emptyGroup.id);
      await store.closeSession(session.id);
      expect(store.attendances, isEmpty);
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final archived = store.closings.single.toJson();
      await store.reopenSession(session.id);
      await expectLater(
        store.saveSession(session.copyWith(number: 9)),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.cancelSession(session.id),
        throwsA(isA<CenterException>()),
      );
      expect(store.sessions.single.number, session.number);
      expect(store.sessions.single.status, SessionStatus.open);
      expect(store.allClosings.single.toJson(), archived);
    },
  );

  test(
    'SQLite failure rolls back status and automatic closing reopen correction as one transaction',
    () async {
      final session = await addSession();
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final original = store.closings.single.toJson();
      final audits = store.audit.length;
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      try {
        await db.execute(
          "CREATE TRIGGER reject_session_reopen BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'blocked'); END",
        );
        await expectLater(
          store.reopenSession(session.id),
          throwsA(
            isA<CenterException>().having(
              (error) => error.cause,
              'SQLite cause',
              isNotNull,
            ),
          ),
        );
        expect(store.sessions.single.status, SessionStatus.closed);
        expect(store.closings.single.toJson(), original);
        expect(store.corrections, isEmpty);
        expect(store.audit.length, audits);
        final persisted =
            jsonDecode((await db.query('state')).single['payload'] as String)
                as Map;
        expect((persisted['corrections'] as List), isEmpty);
        expect(
          ((persisted['sessions'] as List).single as Map)['status'],
          'closed',
        );
        await db.execute('DROP TRIGGER reject_session_reopen');
      } finally {
        await db.close();
      }
      await reopenStore();
      expect(store.closings.single.toJson(), original);
      await store.reopenSession(session.id);
      expect(store.closings, isEmpty);
      expect(store.allClosings.single.toJson(), original);
    },
  );
}
