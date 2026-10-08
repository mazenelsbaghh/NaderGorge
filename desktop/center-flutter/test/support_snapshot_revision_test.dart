import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/cloud/cloud_support_controller.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'helpers/cloud_http_fake.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  CloudSupportController? support;
  const password = 'synthetic-revision-password';

  setUp(() async {
    directory = await Directory.systemTemp.createTemp(
      'massar-support-revision-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('owner', password);
  });
  tearDown(() async {
    await support?.close();
    support?.dispose();
    support = null;
    await store.close();
    await directory.delete(recursive: true);
  });

  test(
    'automatic support detects receipt-only aborts and external commits without a state mutation',
    () async {
      var captures = 0;
      final network = ScriptedCloudHttp(
        (request) => CloudHttpReply.json(201, {
          'uploadId': jsonDecode(request.body)['uploadId'],
          'receiptId': '12345678-1234-4234-8234-123456789abc',
          'sha256': List.filled(64, 'a').join(),
          'receivedAt': '2026-10-08T12:00:00Z',
        }),
      );
      final controller = support = CloudSupportController(
        directory: Directory('${directory.path}/support'),
        clientOnly: false,
        snapshot: () {
          captures++;
          return store.captureSupportSnapshot(automatic: true);
        },
        snapshotRevision: () => store.supportSnapshotRevision(),
        diagnostics: () async => '{"kind":"session"}\n',
        httpClientFactory: network.createClient,
      );
      await controller.configure(
        Uri.parse('https://support.example.invalid'),
        'synthetic-center',
        'synthetic-device-token-1234567890',
      );
      await controller.syncNow(automatic: true);
      await controller.syncNow(automatic: true);
      expect(captures, 1);
      final initial = jsonDecode(network.requests.single.body)['data'] as Map;
      const requestId = '10000000-0000-4000-8000-000000000001';
      final request = {
        'requestId': requestId,
        'operation': 'saveCatalog',
        'requestHash': List.filled(64, 'a').join(),
      };
      Future<Map<String, dynamic>> abort() => store.commandLan(
        deviceId: 'synthetic-device',
        staffId: store.currentUser!.id,
        request: request,
        cancelIfUnseen: true,
      );
      expect((await abort())['status'], 'aborted');
      await controller.syncNow(automatic: true);
      expect(captures, 2);
      final aborted = jsonDecode(network.requests.last.body)['data'] as Map;
      expect(aborted['data'], initial['data']);
      expect(aborted['lanCommandReceipts'], hasLength(1));
      final receipt = (aborted['lanCommandReceipts'] as List).single as Map;
      expect(receipt['request_id'], requestId);
      expect(jsonDecode(receipt['result_json'] as String)['status'], 'aborted');
      await abort();
      await controller.syncNow(automatic: true);
      expect(captures, 2);

      // A second connection doesn't advance SQLite total_changes() on the store
      // connection. Its commit must still invalidate the automatic observation.
      final database = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      try {
        await database.insert('lan_commands', {
          ...Map<String, Object?>.from(receipt),
          'request_id': '10000000-0000-4000-8000-000000000002',
        });
      } finally {
        await database.close();
      }
      await controller.syncNow(automatic: true);
      expect(captures, 3);
      expect(network.requests, hasLength(3));
      final external = jsonDecode(network.requests.last.body)['data'] as Map;
      expect(external['data'], initial['data']);
      expect(external['lanCommandReceipts'], hasLength(2));
    },
  );

  test(
    'state writes, restore and reopening invalidate the token without retaining the previous database lifetime',
    () async {
      final backup = await store.createBackup();
      final initial = await store.supportSnapshotRevision();
      expect(await store.supportSnapshotRevision(), initial);
      await store.saveCatalog(
        const CatalogEntry(name: 'Synthetic change', kind: CatalogKind.subject),
      );
      final changed = await store.supportSnapshotRevision();
      expect(changed, isNot(initial));
      await store.restoreBackup(backup);
      final restored = await store.supportSnapshotRevision();
      expect(restored, isNot(changed));
      final snapshot = await store.captureSupportSnapshot(automatic: true);
      expect((snapshot['data'] as Map)['catalogs'], isEmpty);
      expect((snapshot['data'] as Map)['credentials'], isNotEmpty);
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      expect(await store.supportSnapshotRevision(), isNot(restored));
      await store.close();
      await expectLater(
        store.supportSnapshotRevision(),
        throwsA(isA<CenterException>()),
      );
    },
  );
}
