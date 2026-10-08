import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/data/center_state_encoder.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/lan_transport.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'validate_state_indexing_test.dart'
    show financialValidationFixture, validationTime;

// The HTTP boundary is substituted; snapshot encoding, remote application,
// corrections, and all read projections are the real production code.
class _SnapshotTransport extends LanTransport {
  _SnapshotTransport(this.state)
    : super(
        const LanEndpoint(
          hostId: 'synthetic-host',
          name: 'Synthetic host',
          address: '127.0.0.1',
          port: 1,
          certificateSha256:
              '0000000000000000000000000000000000000000000000000000000000000000',
        ),
        'synthetic-device',
      );

  CenterState state;
  final encoder = CenterStateEncoder();
  var revision = 1;
  bool lastResponseWasDelta = false;

  void publish(CenterState next) {
    validateState(next);
    state = next;
    revision++;
  }

  Map<String, dynamic> _snapshot(String? baseVersion) {
    final fields = encoder.encodePublicChanges(
      state,
      'snapshot-$revision',
      baseVersion: baseVersion,
    );
    lastResponseWasDelta = fields.containsKey('stateDelta');
    return {
      'stateVersion': 'snapshot-$revision',
      for (final entry in fields.entries)
        entry.key: jsonDecode(entry.value.json),
      'currentUser': state.staff.first.toJson(),
      'canConfigureCards': false,
      'supportStatus': {'configured': false},
    };
  }

  @override
  Future<Map<String, dynamic>> get(
    String path, {
    String? staffSession,
    String? stateVersion,
    bool statePatches = false,
    bool waitForChanges = false,
  }) async => _snapshot(statePatches ? stateVersion : null);

  @override
  Future<Map<String, dynamic>> post(
    String path,
    Map<String, dynamic> body, {
    String? staffSession,
    String? stateVersion,
    bool statePatches = false,
  }) async {
    if (path == '/api/login') {
      return {..._snapshot(null), 'staffSession': 'synthetic-employee'};
    }
    if (path == '/api/logout') return {};
    throw StateError('Unexpected mutation in read-only transport fixture');
  }
}

CenterState _withUnusedPackage() {
  final state = financialValidationFixture();
  state.packages.add(
    PrepaidPackage(
      id: 'unused-package',
      studentId: 'student-2',
      groupId: 'group-2',
      purchasedAt: validationTime,
      paymentId: 'package-payment',
    ),
  );
  state.payments.add(
    PaymentRecord(
      id: 'package-payment',
      studentId: 'student-2',
      groupId: 'group-2',
      packageId: 'unused-package',
      description: 'Package',
      baseAmount: 21000,
      discountPercent: 0,
      netAmount: 21000,
      createdAt: validationTime,
      staffId: 'admin',
    ),
  );
  validateState(state);
  return state;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('remote section refresh', () {
    late Directory directory;
    late _SnapshotTransport transport;
    late CenterStore remote;

    setUp(() async {
      directory = await Directory.systemTemp.createTemp('massar-read-remote-');
      transport = _SnapshotTransport(_withUnusedPackage());
      remote = CenterStore.remote(transport, localDirectory: directory.path);
      await remote.signIn('Admin', 'unused-network-fixture-password');
    });
    tearDown(() async {
      await remote.close();
      await directory.delete(recursive: true);
    });

    test(
      'balances sum purchases per student and group without using current prices',
      () async {
        expect(remote.remainingFor('student-2', 'group-2'), 4);
        final expanded = transport.state.copyForMutation();
        final package = expanded.packages.single;
        final payment = expanded.payments.last;
        for (final identity in [
          ('second-purchase', 'student-2', 'group-2'),
          ('other-student', 'student-1', 'group-1'),
        ]) {
          expanded.packages.add(
            package.copyWith(
              id: identity.$1,
              studentId: identity.$2,
              groupId: identity.$3,
              paymentId: '${identity.$1}-payment',
            ),
          );
          expanded.payments.add(
            payment.copyWith(
              id: '${identity.$1}-payment',
              studentId: identity.$2,
              groupId: identity.$3,
              packageId: identity.$1,
            ),
          );
        }
        expanded.groups[1] = expanded.groups[1].copyWith(packagePrice: 99999);
        transport.publish(expanded);
        await remote.refreshRemote();
        expect(remote.remainingFor('student-2', 'group-2'), 8);
        expect(remote.remainingFor('student-1', 'group-1'), 4);
        expect(remote.remainingFor('student-2', 'group-1'), 0);
        expect(
          remote.packagesForStudent('student-2', groupId: 'group-2'),
          hasLength(2),
        );
        expect(remote.paymentsForStudent('student-2').last.netAmount, 21000);
      },
    );

    test(
      'eligible credit preserves purchase dates, reserved absence, and canceled makeup links',
      () async {
        expect(remote.eligibleRemainingFor('student-2', 'session-2'), 4);
        final purchases = transport.state.copyForMutation();
        final package = purchases.packages.single;
        final payment = purchases.payments.last;
        for (final (id, sessionId) in [
          ('future-unassigned', null),
          ('future-for-session', 'session-2'),
        ]) {
          final purchasedAt = validationTime.add(const Duration(hours: 1));
          purchases.packages.add(
            package.copyWith(
              id: id,
              paymentId: '$id-payment',
              purchasedAt: purchasedAt,
            ),
          );
          purchases.payments.add(
            payment.copyWith(
              id: '$id-payment',
              packageId: id,
              sessionId: sessionId,
              createdAt: purchasedAt,
            ),
          );
        }
        transport.publish(purchases);
        await remote.refreshRemote();
        expect(remote.remainingFor('student-2', 'group-2'), 12);
        expect(remote.eligibleRemainingFor('student-2', 'session-2'), 8);
        expect(remote.eligibleRemainingFor('student-2', 'session-1'), 0);

        final reserved = purchases.copyForMutation();
        reserved.packages[0] = reserved.packages[0].copyWith(remaining: 3);
        reserved.attendances.add(
          AttendanceRecord(
            id: 'reserved-absence',
            studentId: 'student-2',
            sessionId: 'session-2',
            status: AttendanceStatus.absent,
            packageId: package.id,
            recordedAt: validationTime,
          ),
        );
        transport.publish(reserved);
        await remote.refreshRemote();
        expect(remote.remainingFor('student-2', 'group-2'), 11);
        expect(remote.eligibleRemainingFor('student-2', 'session-2'), 8);
        expect(
          remote.eligibleMakeups('student-2', 'session-1').single.id,
          'reserved-absence',
        );

        final used = reserved.copyForMutation();
        used.sessions.add(
          used.sessions.first.copyWith(
            id: 'makeup-session',
            number: 2,
            startsAt: validationTime.add(const Duration(days: 1)),
          ),
        );
        used.attendances.add(
          AttendanceRecord(
            id: 'used-makeup',
            studentId: 'student-2',
            sessionId: 'makeup-session',
            status: AttendanceStatus.makeup,
            originalAttendanceId: 'reserved-absence',
            recordedAt: validationTime.add(const Duration(days: 1)),
          ),
        );
        transport.publish(used);
        await remote.refreshRemote();
        expect(remote.eligibleMakeups('student-2', 'session-1'), isEmpty);
        final canceled = used.copyForMutation();
        canceled.corrections.add(
          CorrectionRecord(
            id: 'cancel-makeup',
            action: CorrectionAction.entryReversed,
            studentId: 'student-2',
            sessionId: 'makeup-session',
            attendanceId: 'used-makeup',
            reason: 'Cancel used makeup',
            staffId: 'admin',
            createdAt: validationTime.add(const Duration(days: 1, minutes: 1)),
          ),
        );
        transport.publish(canceled);
        await remote.refreshRemote();
        expect(
          remote.eligibleMakeups('student-2', 'session-1').single.id,
          'reserved-absence',
        );
        expect(remote.eligibleRemainingFor('student-2', 'session-2'), 8);
      },
    );

    test(
      'historical center fee balance and payment cancellation refresh independently',
      () async {
        final partial = transport.state.copyForMutation();
        partial.centerFees.removeLast();
        partial.students[0] = partial.students[0].copyWith(
          centerFeeEnabled: true,
          centerFeeAmount: 9900,
        );
        transport.publish(partial);
        await remote.refreshRemote();
        expect(remote.centerFeeDueFor('student-1', 'session-1'), 1500);
        expect(remote.centerFeeCollectedFor('student-1', 'session-1'), 500);
        expect(remote.centerFeeRemainingFor('student-1', 'session-1'), 1000);
        expect(remote.attendanceNeedsPayment('student-1', 'session-1'), isTrue);
        // The existing partial teacher receipt leaves a debt, not a missing receipt.
        expect(
          remote.attendanceNeedsPayment('student-2', 'session-1'),
          isFalse,
        );

        final changed = partial.copyForMutation();
        changed.centerFees.insert(
          0,
          CenterFeeRecord(
            id: 'new-fee-top-up',
            originalFeeId: 'fee-original',
            studentId: 'student-1',
            sessionId: 'session-1',
            amount: 1000,
            paidAmount: 1000,
            recordedAt: validationTime,
            staffId: 'admin',
          ),
        );
        final canceledAt = validationTime.add(const Duration(minutes: 1));
        changed.corrections.add(
          CorrectionRecord(
            id: 'cancel-payment',
            action: CorrectionAction.paymentCanceled,
            studentId: 'student-2',
            sessionId: 'session-1',
            paymentId: 'makeup-payment',
            voidsPayment: true,
            reason: 'Cancel teacher receipt',
            staffId: 'admin',
            createdAt: canceledAt,
          ),
        );
        changed.refunds.add(
          RefundRecord(
            id: 'payment-refund',
            correctionId: 'cancel-payment',
            paymentId: 'makeup-payment',
            studentId: 'student-2',
            groupId: 'group-2',
            sessionId: 'session-1',
            amount: 3000,
            method: 'نقدي',
            reason: 'Cancel teacher receipt',
            staffId: 'admin',
            createdAt: canceledAt,
          ),
        );
        transport.publish(changed);
        await remote.refreshRemote();
        expect(remote.centerFeeDueFor('student-1', 'session-1'), 1500);
        expect(remote.centerFeeCollectedFor('student-1', 'session-1'), 1500);
        expect(remote.centerFeeRemainingFor('student-1', 'session-1'), 0);
        expect(
          remote.attendanceNeedsPayment('student-1', 'session-1'),
          isFalse,
        );
        expect(remote.attendanceNeedsPayment('student-2', 'session-1'), isTrue);
        expect(
          remote.hasRetainedSessionPayment('student-2', 'session-1'),
          isFalse,
        );

        final repaid = changed.copyForMutation();
        repaid.payments.add(
          repaid.payments.first.copyWith(
            id: 'replacement-payment',
            createdAt: canceledAt.add(const Duration(minutes: 1)),
          ),
        );
        transport.publish(repaid);
        await remote.refreshRemote();
        expect(
          remote.attendanceNeedsPayment('student-2', 'session-1'),
          isFalse,
        );
        expect(remote.centerFeeRemainingFor('student-1', 'session-1'), 0);
      },
    );

    test(
      'note and fee updates preserve immutable student-scoped snapshots',
      () async {
        final attendance = remote.attendancesForStudent('student-1');
        final payments = remote.paymentsForStudent('student-2');
        final packages = remote.packagesForStudent(
          'student-2',
          groupId: 'group-2',
        );
        final fees = remote.centerFeesFor('student-1', 'session-1');
        expect(attendance, hasLength(1));
        expect(payments, hasLength(2));
        expect(packages.single.remaining, 4);
        expect(fees, hasLength(2));
        for (final rows in <List<Object>>[
          attendance,
          payments,
          packages,
          fees,
        ]) {
          expect(rows.clear, throwsUnsupportedError);
        }
        expect(remote.packagesForStudent('student-1'), isEmpty);
        expect(
          remote.packagesForStudent('student-2', groupId: 'group-1'),
          isEmpty,
        );
        expect(remote.centerFeesFor('student-2', 'session-1'), isEmpty);
        expect(remote.remainingFor('student-2', 'group-1'), 0);

        final noted = transport.state.copyForMutation();
        noted.students[0] = noted.students[0].copyWith(notes: 'New note');
        transport.publish(noted);
        await remote.refreshRemote();
        expect(transport.lastResponseWasDelta, isTrue);
        expect(remote.studentById('student-1')!.notes, 'New note');
        expect(
          remote.attendancesForStudent('student-1').single.id,
          attendance.single.id,
        );
        expect(remote.remainingFor('student-2', 'group-2'), 4);

        final collected = noted.copyForMutation();
        collected.centerFees.add(
          CenterFeeRecord(
            id: 'independent-fee',
            studentId: 'student-2',
            sessionId: 'session-1',
            amount: 1000,
            paidAmount: 1000,
            recordedAt: validationTime,
          ),
        );
        transport.publish(collected);
        await remote.refreshRemote();
        expect(
          remote.centerFeesFor('student-2', 'session-1').single.paidAmount,
          1000,
        );
        expect(fees, hasLength(2));
        expect(remote.centerFeesFor('student-1', 'session-1'), hasLength(2));
        expect(File('${directory.path}/center.sqlite').existsSync(), isFalse);
      },
    );

    test(
      'correction deltas refresh methods, active presence, and refunded balances',
      () async {
        final oldPayments = remote.paymentsForStudent('student-2');
        final oldAttendance = remote.attendancesForStudent('student-2');
        final oldPackages = remote.packagesForStudent('student-2');
        expect(remote.attendanceCount('session-1'), 2);
        expect(remote.remainingFor('student-2', 'group-2'), 4);
        final changed = transport.state.copyForMutation();
        changed.corrections.addAll([
          CorrectionRecord(
            id: 'method-correction',
            action: CorrectionAction.paymentMethod,
            studentId: 'student-2',
            sessionId: 'session-1',
            paymentId: 'makeup-payment',
            oldMethod: 'نقدي',
            newMethod: 'تحويل',
            reason: 'Correct method',
            staffId: 'admin',
            createdAt: validationTime,
          ),
          CorrectionRecord(
            id: 'attendance-reversal',
            action: CorrectionAction.entryReversed,
            studentId: 'student-2',
            sessionId: 'session-1',
            attendanceId: 'makeup',
            reason: 'Reverse entry',
            staffId: 'admin',
            createdAt: validationTime,
          ),
          CorrectionRecord(
            id: 'package-refund',
            action: CorrectionAction.packageRefund,
            studentId: 'student-2',
            paymentId: 'package-payment',
            packageId: 'unused-package',
            voidsPayment: true,
            voidsPackage: true,
            reason: 'Refund package',
            staffId: 'admin',
            createdAt: validationTime,
          ),
        ]);
        changed.refunds.add(
          RefundRecord(
            id: 'refund',
            correctionId: 'package-refund',
            paymentId: 'package-payment',
            studentId: 'student-2',
            groupId: 'group-2',
            packageId: 'unused-package',
            amount: 21000,
            method: 'نقدي',
            reason: 'Refund package',
            staffId: 'admin',
            createdAt: validationTime,
          ),
        );
        transport.publish(changed);
        await remote.refreshRemote();
        expect(transport.lastResponseWasDelta, isTrue);
        expect(remote.paymentsForStudent('student-2').single.method, 'تحويل');
        expect(remote.attendancesForStudent('student-2'), isEmpty);
        expect(remote.attendanceCount('session-1'), 1);
        expect(remote.packagesForStudent('student-2'), isEmpty);
        expect(remote.remainingFor('student-2', 'group-2'), 0);
        expect(oldPayments, hasLength(2));
        expect(oldPayments.first.method, 'نقدي');
        expect(oldAttendance.single.id, 'makeup');
        expect(oldPackages.single.remaining, 4);
        expect(
          remote.studentHistoryFor('student-2').allAttendances,
          hasLength(1),
        );
      },
    );

    test(
      'package consumption and full replacement update balances without stale rows',
      () async {
        final prior = remote.packagesForStudent('student-2');
        expect(remote.remainingFor('student-2', 'group-2'), 4);
        final consumed = transport.state.copyForMutation();
        consumed.packages[0] = consumed.packages[0].copyWith(remaining: 3);
        consumed.attendances.add(
          AttendanceRecord(
            id: 'package-attendance',
            studentId: 'student-2',
            sessionId: 'session-2',
            status: AttendanceStatus.present,
            packageId: 'unused-package',
            recordedAt: validationTime,
          ),
        );
        transport.publish(consumed);
        await remote.refreshRemote();
        expect(transport.lastResponseWasDelta, isTrue);
        expect(remote.remainingFor('student-2', 'group-2'), 3);
        expect(remote.packagesForStudent('student-2').single.remaining, 3);
        expect(remote.attendanceCount('session-2'), 1);
        expect(prior.single.remaining, 4);

        transport.encoder.clearPublicHistory();
        transport.publish(_withUnusedPackage());
        await remote.refreshRemote();
        expect(transport.lastResponseWasDelta, isFalse);
        expect(remote.remainingFor('student-2', 'group-2'), 4);
        expect(remote.attendanceCount('session-2'), 0);
        remote.signOut();
        expect(remote.packagesForStudent('student-2'), isEmpty);
        expect(remote.studentById('student-2'), isNull);
      },
    );
  });

  test(
    'failed package commit cannot leak candidate reads; retry and restore refresh balances',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'massar-read-rollback-',
      );
      var store = await CenterStore.open(directory: directory.path);
      addTearDown(() async {
        await store.close();
        await directory.delete(recursive: true);
      });
      const password = 'synthetic-read-password';
      await store.setupAdmin('Admin', password);
      final initialBackup = await store.createBackup();
      final initial =
          jsonDecode(await File(initialBackup).readAsString()) as Map;
      final credential = (initial['data'] as Map)['credentials'] as Map;
      final state = financialValidationFixture();
      state.staff = [state.staff.first];
      state.credentials = {
        'admin': Map<String, String>.from(credential.values.single as Map),
      };
      state.attendances.removeLast();
      state.payments.clear();
      state.sessions = [
        for (final session in state.sessions)
          session.copyWith(startedAt: validationTime, startedBy: 'admin'),
      ];
      validateState(state);
      final path = store.databasePath;
      await store.close();
      final seeded = await databaseFactoryFfi.openDatabase(path);
      await seeded.update('state', {
        'payload': jsonEncode(state.toJson()),
      }, where: 'id = 1');
      await seeded.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('Admin', password);
      final backup = await store.createBackup(
        destination: '${directory.path}/before-package.json',
      );
      final oldPackages = store.packagesForStudent('student-2');
      expect(store.remainingFor('student-2', 'group-2'), 0);
      expect(store.eligibleRemainingFor('student-2', 'session-2'), 0);
      expect(store.attendanceNeedsPayment('student-2', 'session-2'), isFalse);
      expect(store.attendancesForStudent('student-2'), isEmpty);
      expect(store.paymentsForStudent('student-2'), isEmpty);
      final database = await databaseFactoryFfi.openDatabase(
        path,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      Future<void> collect() => store.collectAndAttend(
        const EntryRequest(
          studentId: 'student-2',
          sessionId: 'session-2',
          mode: EntryMode.package,
        ),
      );
      try {
        await database.execute(
          "CREATE TRIGGER deny_read_fixture BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'synthetic save failure'); END",
        );
        await expectLater(
          collect(),
          throwsA(
            isA<CenterException>().having(
              (error) => error.cause.toString(),
              'SQLite failure after the candidate mutation',
              contains('synthetic save failure'),
            ),
          ),
        );
        expect(store.remainingFor('student-2', 'group-2'), 0);
        expect(store.eligibleRemainingFor('student-2', 'session-2'), 0);
        expect(store.attendanceNeedsPayment('student-2', 'session-2'), isFalse);
        expect(store.packagesForStudent('student-2'), isEmpty);
        expect(store.attendancesForStudent('student-2'), isEmpty);
        expect(store.paymentsForStudent('student-2'), isEmpty);
        await database.execute('DROP TRIGGER deny_read_fixture');
        await collect();
        final committedPackages = store.packagesForStudent('student-2');
        expect(committedPackages.single.remaining, 3);
        expect(store.remainingFor('student-2', 'group-2'), 3);
        expect(store.eligibleRemainingFor('student-2', 'session-2'), 3);
        expect(store.attendanceNeedsPayment('student-2', 'session-2'), isFalse);
        expect(
          store.attendancesForStudent('student-2').single.packageId,
          committedPackages.single.id,
        );
        expect(oldPackages, isEmpty);
        await store.restoreBackup(backup);
        await store.signIn('Admin', password);
        expect(store.remainingFor('student-2', 'group-2'), 0);
        expect(store.eligibleRemainingFor('student-2', 'session-2'), 0);
        expect(store.attendanceNeedsPayment('student-2', 'session-2'), isFalse);
        expect(store.packagesForStudent('student-2'), isEmpty);
        expect(committedPackages.single.remaining, 3);
      } finally {
        await database.execute('DROP TRIGGER IF EXISTS deny_read_fixture');
        await database.close();
      }
    },
  );
}
