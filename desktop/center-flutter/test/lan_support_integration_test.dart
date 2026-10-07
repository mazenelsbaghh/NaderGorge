import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/center_store_host_bridge.dart';
import 'package:massar_center/lan/lan_host_process.dart';
import 'package:massar_center/lan/lan_transport.dart';
import 'package:path/path.dart' as path;
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

// Observes the real transport response, not a replacement server or store.
class _ObservedTransport extends LanTransport {
  _ObservedTransport(super.endpoint, super.deviceToken);
  Map<String, dynamic>? lastSupportResponse;
  String? staffSession;
  @override
  Future<Map<String, dynamic>> post(
    String route,
    Map<String, dynamic> body, {
    String? staffSession,
    String? stateVersion,
    bool statePatches = false,
  }) async {
    final response = await super.post(
      route,
      body,
      staffSession: staffSession,
      stateVersion: stateVersion,
      statePatches: statePatches,
    );
    if (route == '/api/login') {
      this.staffSession = response['staffSession'] as String;
    }
    if (route == '/api/support-upload') lastSupportResponse = response;
    return response;
  }
}

class _SupportHarness {
  _SupportHarness(this.executable);
  final String executable;
  Directory? _sandbox;
  CenterStore? _host, _remote;
  CenterStoreHostBridge? _bridge;
  LanHostProcess? _gateway;
  _ObservedTransport? _wire;
  Directory get sandbox => _sandbox!;
  CenterStore get host => _host!;
  CenterStore get remote => _remote!;
  _ObservedTransport get wire => _wire!;
  late String baselineBackup, cashierId;
  String get studentId => host.students.single.id;
  String get clientDirectory => path.join(sandbox.path, 'client');

  Future<void> start() async {
    _sandbox = await Directory.systemTemp.createTemp('massar-support-lan-');
    await Directory(clientDirectory).create();
    _host = await CenterStore.open(directory: path.join(sandbox.path, 'host'));
    await host.setupAdmin('synthetic-owner', 'synthetic-owner-123');
    baselineBackup = await host.createBackup(
      destination: path.join(sandbox.path, 'baseline.json'),
    );
    await host.saveStaff(
      name: 'synthetic-cashier',
      password: 'synthetic-cashier-123',
      role: StaffRole.cashier,
    );
    cashierId = host.staff
        .firstWhere((user) => user.role == StaffRole.cashier)
        .id;
    for (final kind in CatalogKind.values) {
      await host.saveCatalog(
        CatalogEntry(name: 'Synthetic ${kind.name}', kind: kind),
      );
    }
    await host.saveGroup(
      StudyGroup(
        name: 'Synthetic group',
        subjectId: host.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.subject)
            .id,
        centerId: host.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.center)
            .id,
        gradeId: host.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.grade)
            .id,
      ),
    );
    await host.saveStudent(
      Student(
        name: 'Synthetic student',
        code: '1001',
        groupIds: [host.groups.single.id],
        createdAt: DateTime.now(),
      ),
    );
    _bridge = await CenterStoreHostBridge.start(host);
    _gateway = LanHostProcess(executablePath: executable);
    final ready = await _gateway!.start(
      dataDirectory: path.join(sandbox.path, 'gateway'),
      upstreamUrl: _bridge!.uri.toString(),
      upstreamSecret: _bridge!.secret,
      name: 'Synthetic center',
      port: 0,
      discoveryPort: 0,
    );
    final pairing = LanTransport(ready.endpoint, '');
    late Map<String, dynamic> paired;
    try {
      paired = await pairing.pair(
        ready.pairingCode,
        'synthetic-client',
        'Synthetic secondary',
      );
    } finally {
      pairing.close();
    }
    _wire = _ObservedTransport(ready.endpoint, paired['token'] as String);
    _remote = CenterStore.remote(wire, localDirectory: clientDirectory);
    await remote.signIn('synthetic-cashier', 'synthetic-cashier-123');
  }

  Future<void> disconnect() => _gateway!.stop();

  Future<void> close() async {
    _host?.supportUploadQueue = null;
    _host?.supportStatusReader = null;
    await _remote?.close();
    _wire?.close();
    await _gateway?.dispose();
    await _bridge?.close();
    await _host?.close();
    if (_sandbox != null && await sandbox.exists()) {
      await sandbox.delete(recursive: true);
    }
  }
}

void main() {
  late Directory buildDirectory;
  late String executable;
  setUpAll(() async {
    buildDirectory = await Directory.systemTemp.createTemp(
      'massar-support-gateway-build-',
    );
    executable =
        Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'] ??
        path.join(
          buildDirectory.path,
          'massar-lan-host${Platform.isWindows ? '.exe' : ''}',
        );
    if (Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'] == null) {
      final built = await Process.run('go', [
        'build',
        '-o',
        executable,
        '.',
      ], workingDirectory: '../center-lan').timeout(const Duration(minutes: 2));
      expect(
        built.exitCode,
        0,
        reason: 'Real native Go gateway build: ${built.stderr}',
      );
    }
  });
  tearDownAll(() async => buildDirectory.delete(recursive: true));
  late _SupportHarness harness;
  setUp(() async {
    harness = _SupportHarness(executable);
    addTearDown(harness.close);
    await harness.start().timeout(const Duration(seconds: 45));
  });

  test(
    'cashier secondary queues a consistent host backup with durable LAN receipts and no data response',
    () async {
      await harness.remote.saveStudentNote(
        studentId: harness.studentId,
        notes: 'Synthetic committed note',
      );
      var queues = 0;
      Map<String, dynamic>? captured;
      harness.host.supportStatusReader = () => {
        'configured': true,
        'pending': queues > 0,
        'kind': 'database',
      };
      harness.host.supportUploadQueue = () async {
        captured = await harness.host.captureSupportSnapshot();
        queues++;
      };
      await harness.remote.requestSupportUpload().timeout(
        const Duration(seconds: 15),
      );
      expect(queues, 1);
      expect(harness.host.currentUser!.role, StaffRole.admin);
      expect(harness.remote.currentUser!.role, StaffRole.cashier);
      expect(harness.remote.supportStatus['pending'], isTrue);
      expect(harness.remote.remoteConnected, isTrue);
      expect(captured!['format'], 'massar-center-backup');
      expect(DateTime.parse(captured!['exportedAt'] as String).isUtc, isTrue);
      final snapshotState = captured!['data'] as Map<String, dynamic>;
      expect(snapshotState['credentials'], isNotEmpty);
      final students = snapshotState['students'] as List;
      expect(students.single['notes'], 'Synthetic committed note');
      final receipts = captured!['lanCommandReceipts'] as List;
      expect(receipts, hasLength(1));
      expect(receipts.single['operation'], 'saveStudentNote');
      expect(receipts.single['user_id'], harness.cashierId);
      expect(
        jsonDecode(receipts.single['result_json'] as String)['status'],
        'committed',
      );
      final connection = await databaseFactoryFfiNoIsolate.openDatabase(
        harness.host.databasePath,
        options: OpenDatabaseOptions(readOnly: true, singleInstance: false),
      );
      try {
        final durable =
            (await connection.query('state')).single['payload'] as String;
        expect(snapshotState, jsonDecode(durable));
        expect(receipts, await connection.query('lan_commands'));
      } finally {
        await connection.close();
      }
      final response = harness.wire.lastSupportResponse!;
      expect(response.keys.toSet(), {'queued', 'supportStatus'});
      expect(response['queued'], isTrue);
      expect(jsonEncode(response), isNot(contains('Synthetic committed note')));
      expect(response.containsKey('data'), isFalse);
      expect(response.containsKey('state'), isFalse);
      expect(await Directory(harness.clientDirectory).exists(), isTrue);
      final clientFiles = await Directory(
        harness.clientDirectory,
      ).list(recursive: true).toList();
      expect(
        clientFiles.where((file) => file.path.endsWith('.sqlite')),
        isEmpty,
      );
      expect(
        clientFiles.where(
          (file) => file.path.contains('cloud-support-pending'),
        ),
        isEmpty,
      );
      students.single['notes'] = 'tampered returned snapshot';
      (snapshotState['credentials'] as Map).clear();
      receipts.single['result_json'] = 'tampered returned receipt';
      final independent = await harness.host.captureSupportSnapshot();
      expect(harness.host.students.single.notes, 'Synthetic committed note');
      expect(independent['data']['credentials'], isNotEmpty);
      expect(
        independent['data']['students'].single['notes'],
        'Synthetic committed note',
      );
      expect(
        jsonDecode(
          independent['lanCommandReceipts'].single['result_json'] as String,
        )['status'],
        'committed',
      );
    },
    timeout: const Timeout(Duration(seconds: 45)),
  );

  test(
    'captured state and receipts stay on one committed revision during later LAN edits',
    () async {
      await harness.remote.saveStudentNote(
        studentId: harness.studentId,
        notes: 'Synthetic first commit',
      );
      final capture = harness.host.captureSupportSnapshot();
      final laterEdit = harness.remote.saveStudentNote(
        studentId: harness.studentId,
        notes: 'Synthetic second commit',
      );
      final before = await capture;
      await laterEdit;
      final after = await harness.host.captureSupportSnapshot();
      expect(
        before['data']['students'].single['notes'],
        'Synthetic first commit',
      );
      expect(before['lanCommandReceipts'], hasLength(1));
      expect(
        after['data']['students'].single['notes'],
        'Synthetic second commit',
      );
      expect(after['lanCommandReceipts'], hasLength(2));
      expect(before['data']['credentials'], isNotEmpty);
    },
    timeout: const Timeout(Duration(seconds: 45)),
  );

  test(
    'revoked staff session cannot finish an already queued snapshot capture',
    () async {
      final entered = Completer<void>(), release = Completer<void>();
      var captures = 0;
      harness.host.supportUploadQueue = () async {
        entered.complete();
        await release.future;
        await harness.host.captureSupportSnapshot();
        captures++;
      };
      final rejected = expectLater(
        harness.remote.requestSupportUpload(),
        throwsA(isA<LanAuthorizationException>()),
      );
      try {
        await entered.future.timeout(const Duration(seconds: 10));
        await harness.wire.post(
          '/api/logout',
          {},
          staffSession: harness.wire.staffSession,
        );
      } finally {
        if (!release.isCompleted) release.complete();
      }
      await rejected.timeout(const Duration(seconds: 15));
      expect(captures, 0);
      expect(harness.remote.currentUser, isNull);
      expect(harness.remote.remoteConnected, isFalse);
      expect(harness.host.currentUser!.role, StaffRole.admin);
    },
    timeout: const Timeout(Duration(seconds: 45)),
  );

  test(
    'disconnected support request updates connection state without queuing local data',
    () async {
      var queues = 0;
      harness.host.supportUploadQueue = () async {
        queues++;
      };
      await harness.disconnect();
      await expectLater(
        harness.remote.requestSupportUpload(),
        throwsA(isA<LanConnectionException>()),
      );
      expect(harness.remote.remoteConnected, isFalse);
      expect(harness.remote.currentUser!.role, StaffRole.cashier);
      expect(queues, 0);
      final directory = Directory(harness.clientDirectory);
      if (await directory.exists()) {
        expect(await directory.list(recursive: true).toList(), isEmpty);
      }
    },
    timeout: const Timeout(Duration(seconds: 45)),
  );

  test(
    'actor removed by an earlier queued restore is denied before snapshot materialization',
    () async {
      Map<String, dynamic>? captured;
      harness.host.supportUploadQueue = () async {
        captured = await harness.host.captureSupportSnapshot();
      };
      // Restore is queued first, while the caller still resolves to the old
      // cashier. Authorization must re-read the committed actor inside capture.
      final restored = harness.host.restoreBackup(harness.baselineBackup);
      final denied = expectLater(
        harness.host.queueSupportUploadLan(
          harness.cashierId,
          authorize: () => true,
        ),
        throwsA(isA<CenterException>()),
      );
      await Future.wait([
        restored,
        denied,
      ]).timeout(const Duration(seconds: 15));
      expect(
        harness.host.staff.any((user) => user.id == harness.cashierId),
        isFalse,
      );
      expect(captured, isNull);
      expect(harness.host.currentUser, isNull);
    },
    timeout: const Timeout(Duration(seconds: 45)),
  );
}
