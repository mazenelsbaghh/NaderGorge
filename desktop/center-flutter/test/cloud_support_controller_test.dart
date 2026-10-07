import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/cloud/cloud_support_controller.dart';
import 'package:massar_center/cloud/cloud_support_settings.dart';
import 'package:massar_center/domain/models.dart';
import 'package:path/path.dart' as path;

import 'helpers/cloud_http_fake.dart';

const _deviceToken = 'synthetic-device-token-1234567890';
final _origin = Uri.parse('https://support.example.invalid');
final _configuration = CloudSupportConfiguration(
  origin: _origin,
  centerId: 'synthetic-center',
  deviceToken: _deviceToken,
);

Map<String, dynamic> _ack(CloudHttpRequest request) => {
  'uploadId': (jsonDecode(request.body) as Map)['uploadId'],
  'receiptId': '12345678-1234-4234-8234-123456789abc',
  // Canonical Go JSON need not have the same digest as Dart's request bytes.
  'sha256': List.filled(64, 'a').join(),
  'receivedAt': '2026-10-03T12:34:56.123456789Z',
};

void main() {
  late Directory sandbox;
  final controllers = <CloudSupportController>[];
  setUp(() async {
    sandbox = await Directory.systemTemp.createTemp('massar-cloud-test-');
  });
  tearDown(() async {
    for (final controller in controllers) {
      await controller.close();
      controller.dispose();
    }
    controllers.clear();
    await sandbox.delete(recursive: true);
  });

  CloudSupportController create(
    ScriptedCloudHttp network, {
    bool clientOnly = false,
    Future<Map<String, dynamic>> Function()? snapshot,
    Future<String> Function()? diagnostics,
  }) {
    final controller = CloudSupportController(
      directory: sandbox,
      clientOnly: clientOnly,
      snapshot: snapshot ?? () async => {'students': [], 'synthetic': 1},
      diagnostics: diagnostics ?? () async => '{"kind":"session"}\n',
      httpClientFactory: network.createClient,
    );
    controllers.add(controller);
    return controller;
  }

  Future<void> configure(CloudSupportController controller) =>
      controller.configure(_origin, _configuration.centerId, _deviceToken);

  test(
    'automatic upload coalesces unchanged snapshots, persists digest and uploads later edits',
    () async {
      var revision = 1;
      final network = ScriptedCloudHttp(
        (request) => CloudHttpReply.json(201, _ack(request)),
      );
      Future<Map<String, dynamic>> snapshot() async => {
        'exportedAt': DateTime.now().toIso8601String(),
        'data': {'revision': revision},
      };
      final first = create(
        network,
        snapshot: snapshot,
        diagnostics: () async =>
            '{"kind":"export","exportedAt":"${DateTime.now().toIso8601String()}"}\n{"kind":"session"}\n',
      );
      await configure(first);
      await first.syncNow(automatic: true);
      await first.syncNow(automatic: true);
      expect(network.requests, hasLength(1));
      await first.close();
      final restored = create(
        network,
        snapshot: snapshot,
        diagnostics: () async =>
            '{"kind":"export","exportedAt":"${DateTime.now().toIso8601String()}"}\n{"kind":"session"}\n',
      );
      await restored.initialize();
      await restored.syncNow(automatic: true);
      expect(network.requests, hasLength(1));
      revision++;
      await restored.syncNow(automatic: true);
      expect(network.requests, hasLength(2));
      expect(
        jsonDecode(network.requests.last.body)['data']['data']['revision'],
        2,
      );
    },
  );
  test('automatic secondary upload never captures database', () async {
    final network = ScriptedCloudHttp(
      (request) => CloudHttpReply.json(201, _ack(request)),
    );
    final client = create(
      network,
      clientOnly: true,
      snapshot: () async => throw StateError('database accessed'),
    );
    await configure(client);
    await client.syncNow(automatic: true);
    await client.syncNow(automatic: true);
    expect(network.requests, hasLength(1));
    expect(jsonDecode(network.requests.single.body)['data'], isNull);
  });

  test(
    'automatic capture includes receipt-only and diagnostic-only changes',
    () async {
      final receipts = <Map<String, dynamic>>[];
      var diagnostics = '{"kind":"session"}\n';
      final network = ScriptedCloudHttp(
        (request) => CloudHttpReply.json(201, _ack(request)),
      );
      final controller = create(
        network,
        snapshot: () async => {
          'data': {'revision': 1},
          'lanCommandReceipts': receipts,
        },
        diagnostics: () async => diagnostics,
      );
      await configure(controller);
      await controller.syncNow(automatic: true);
      receipts.add({
        'request_id': '12345678-1234-4234-8234-123456789abc',
        'result_json': '{"status":"aborted"}',
      });
      await controller.syncNow(automatic: true);
      diagnostics += '{"kind":"problem","operation":"lan.connect"}\n';
      await controller.syncNow(automatic: true);
      diagnostics += '{"kind":"export","exportedAt":"2026-10-08"}\n';
      await controller.syncNow(automatic: true);
      expect(network.requests, hasLength(3));
      final receiptUpload = jsonDecode(network.requests[1].body) as Map;
      expect(receiptUpload['data']['data'], {'revision': 1});
      expect(receiptUpload['data']['lanCommandReceipts'], receipts);
      expect(
        jsonDecode(network.requests[2].body)['diagnostics'],
        contains('lan.connect'),
      );
    },
  );

  test(
    'secondary sends only its diagnostics and never opens host pending data',
    () async {
      final hostFile = File(
        path.join(sandbox.path, 'cloud-support-pending.json'),
      );
      await hostFile.writeAsString(
        'host-private-contents-not-valid-client-json',
      );
      var snapshotCalls = 0;
      final network = ScriptedCloudHttp(
        (request) => CloudHttpReply.json(201, _ack(request)),
      );
      final client = create(
        network,
        clientOnly: true,
        snapshot: () async {
          snapshotCalls++;
          throw StateError('client must not read host snapshot');
        },
      );
      await configure(client);
      await client.syncNow();
      expect(snapshotCalls, 0);
      expect(network.requests, hasLength(1));
      final envelope = jsonDecode(network.requests.single.body) as Map;
      expect(envelope['kind'], 'diagnostics');
      expect(envelope['data'], isNull);
      expect((envelope['app'] as Map)['role'], 'client');
      expect(client.pending, isFalse);
      expect(client.lastSuccess, isNotNull);
      expect(
        await hostFile.readAsString(),
        'host-private-contents-not-valid-client-json',
      );
      expect(jsonEncode(client.publicStatus), isNot(contains(_deviceToken)));
      expect(
        await sandbox
            .list(recursive: true)
            .any((entry) => entry.path.endsWith('.sqlite')),
        isFalse,
      );
    },
  );

  test(
    'lost reply and restart retry exactly the durable snapshot without recapture',
    () async {
      var captures = 0;
      final snapshot = {'syntheticBalance': 4200};
      final offline = ScriptedCloudHttp(
        (_) => throw const SocketException('synthetic disconnected'),
      );
      final first = create(
        offline,
        snapshot: () async {
          captures++;
          return snapshot;
        },
      );
      await configure(first);
      await expectLater(first.syncNow(), throwsA(isA<CenterException>()));
      final settings = CloudSupportSettings(sandbox);
      final queued = await settings.readPending();
      expect(queued, isNotNull);
      snapshot['syntheticBalance'] = 0;
      expect(jsonDecode(queued!.body)['data']['syntheticBalance'], 4200);
      await expectLater(
        first.configure(
          Uri.parse('https://different.example.invalid'),
          _configuration.centerId,
          _deviceToken,
        ),
        throwsA(isA<CenterException>()),
      );
      await first.close();
      final online = ScriptedCloudHttp(
        (request) => CloudHttpReply.json(200, _ack(request)),
      );
      final restored = create(
        online,
        snapshot: () async {
          captures++;
          throw StateError('durable pending must not recapture');
        },
      );
      await restored.initialize();
      expect(restored.pending, isTrue);
      await restored.syncNow();
      expect(captures, 1);
      expect(online.requests.single.body, offline.requests.single.body);
      expect(await settings.readPending(), isNull);
      final persistedStatus = await settings.readStatus();
      expect(persistedStatus!['receipt']['uploadId'], queued.uploadId);
      expect(restored.lastError, isNull);
    },
  );

  final invalidAcknowledgments =
      <String, Map<String, dynamic> Function(Map<String, dynamic>)>{
        'different upload': (ack) => {
          ...ack,
          'uploadId': '99999999-9999-4999-8999-999999999999',
        },
        'missing receipt': (ack) => {...ack}..remove('receiptId'),
        'malformed hash': (ack) => {...ack, 'sha256': 'bad'},
        'non UTC timestamp': (ack) => {
          ...ack,
          'receivedAt': '2026-10-03T12:00:00+03:00',
        },
        'invalid calendar day': (ack) => {
          ...ack,
          'receivedAt': '2026-02-31T12:00:00Z',
        },
      };
  for (final scenario in invalidAcknowledgments.entries) {
    test(
      'HTTP success with ${scenario.key} retains immutable pending upload',
      () async {
        final network = ScriptedCloudHttp(
          (request) => CloudHttpReply.json(201, scenario.value(_ack(request))),
        );
        final controller = create(network);
        await configure(controller);
        await expectLater(
          controller.syncNow(),
          throwsA(isA<CenterException>()),
        );
        final pending = await CloudSupportSettings(sandbox).readPending();
        expect(pending!.body, network.requests.single.body);
        expect(controller.pending, isTrue);
        expect(controller.lastSuccess, isNull);
        expect(controller.lastError, isNotEmpty);
        expect(
          jsonEncode(controller.publicStatus),
          isNot(contains(_deviceToken)),
        );
      },
    );
  }

  test(
    'concurrent upload requests commit one queue and one acknowledged request',
    () async {
      final sending = Completer<void>();
      final response = Completer<CloudHttpReply>();
      var captures = 0;
      late CloudHttpRequest sent;
      final network = ScriptedCloudHttp((request) {
        sent = request;
        sending.complete();
        return response.future;
      });
      final controller = create(
        network,
        snapshot: () async {
          captures++;
          return {'synthetic': true};
        },
      );
      await configure(controller);
      final first = controller.syncNow();
      final second = controller.syncNow();
      await sending.future;
      expect(controller.busy, isTrue);
      expect(await CloudSupportSettings(sandbox).readPending(), isNotNull);
      expect(sent.followRedirects, isFalse);
      expect(sent.uri.scheme, 'https');
      expect(
        sent.headers.value(HttpHeaders.authorizationHeader),
        'Bearer $_deviceToken',
      );
      response.complete(CloudHttpReply.json(201, _ack(sent)));
      await Future.wait([first, second]);
      expect(captures, 1);
      expect(network.requests, hasLength(1));
      expect(controller.pending, isFalse);
      expect(controller.busy, isFalse);
    },
  );

  test(
    'queueUpload returns after persistence before a delayed network confirmation',
    () async {
      final sending = Completer<CloudHttpRequest>();
      final response = Completer<CloudHttpReply>();
      final network = ScriptedCloudHttp((request) {
        sending.complete(request);
        return response.future;
      });
      final controller = create(network);
      await configure(controller);
      await controller.queueUpload();
      final queued = await CloudSupportSettings(sandbox).readPending();
      expect(queued, isNotNull);
      final request = await sending.future;
      expect(controller.lastSuccess, isNull);
      expect(controller.pending, isTrue);
      final completed = Completer<void>();
      controller.addListener(() {
        if (!controller.pending && !controller.busy && !completed.isCompleted) {
          completed.complete();
        }
      });
      response.complete(CloudHttpReply.json(201, _ack(request)));
      await completed.future;
      expect(controller.lastSuccess, isNotNull);
      expect(await CloudSupportSettings(sandbox).readPending(), isNull);
    },
  );

  test(
    'shutdown during an uncertain reply retains the queue without acknowledging it',
    () async {
      final sending = Completer<CloudHttpRequest>();
      final response = Completer<CloudHttpReply>();
      final network = ScriptedCloudHttp((request) {
        sending.complete(request);
        return response.future;
      });
      final controller = create(network);
      await configure(controller);
      final uploading = controller.syncNow();
      final request = await sending.future;
      final closed = controller.close();
      response.complete(CloudHttpReply.json(201, _ack(request)));
      await Future.wait([closed, uploading]);
      expect(controller.lastSuccess, isNull);
      expect(
        (await CloudSupportSettings(sandbox).readPending())!.body,
        request.body,
      );
      await expectLater(controller.syncNow(), throwsA(isA<CenterException>()));
      expect(network.requests, hasLength(1));
    },
  );

  test(
    'oversized diagnostic export fails before creating or sending a truncated upload',
    () async {
      final network = ScriptedCloudHttp(
        (_) => throw StateError('must not send oversized data'),
      );
      final controller = create(
        network,
        diagnostics: () async => List.filled(6 * 1024 * 1024 + 1, 'x').join(),
      );
      await configure(controller);
      await expectLater(controller.syncNow(), throwsA(isA<CenterException>()));
      expect(network.requests, isEmpty);
      expect(controller.pending, isFalse);
      expect(await CloudSupportSettings(sandbox).readPending(), isNull);
      expect(controller.lastError, contains('أكبر من الحد'));
    },
  );

  test(
    'already acknowledged queue is recovered locally without another upload',
    () async {
      final offline = ScriptedCloudHttp(
        (_) => throw const SocketException('offline'),
      );
      final first = create(offline);
      await configure(first);
      await expectLater(first.syncNow(), throwsA(isA<CenterException>()));
      final settings = CloudSupportSettings(sandbox);
      final queued = (await settings.readPending())!;
      await settings.saveStatus({
        'version': 1,
        'receipt': _ack(offline.requests.single),
        'lastError': null,
        'httpStatus': null,
        'attempts': 0,
        'retryAt': null,
      });
      await first.close();
      final network = ScriptedCloudHttp(
        (_) => throw StateError('acknowledged queue must not send'),
      );
      final restored = create(network);
      await restored.initialize();
      expect(network.requests, isEmpty);
      expect(restored.pending, isFalse);
      expect(restored.lastSuccess, isNotNull);
      expect(await settings.readPending(), isNull);
      expect(restored.publicStatus['receipt']['uploadId'], queued.uploadId);
    },
  );

  test(
    'redirect response is rejected without deleting pending or exposing server text',
    () async {
      final network = ScriptedCloudHttp(
        (_) => CloudHttpReply(302, utf8.encode('private server message')),
      );
      final controller = create(network);
      await configure(controller);
      await expectLater(controller.syncNow(), throwsA(isA<CenterException>()));
      expect(network.requests, hasLength(1));
      expect(network.requests.single.followRedirects, isFalse);
      expect(controller.pending, isTrue);
      expect(controller.publicStatus['httpStatus'], 302);
      expect(controller.lastError, isNot(contains('private server message')));
    },
  );

  test(
    'corrupt existing pending is preserved and blocks snapshot capture and upload',
    () async {
      final file = File(path.join(sandbox.path, 'cloud-support-pending.json'));
      await CloudSupportSettings(sandbox).saveConfiguration(_configuration);
      await file.writeAsString('{corrupt-synthetic-pending');
      var captures = 0;
      final network = ScriptedCloudHttp(
        (_) => throw StateError('must not send'),
      );
      final controller = create(
        network,
        snapshot: () async {
          captures++;
          return {};
        },
      );
      await controller.initialize();
      await expectLater(controller.syncNow(), throwsA(isA<CenterException>()));
      expect(controller.publicStatus['storageBlocked'], isTrue);
      expect(captures, 0);
      expect(network.requests, isEmpty);
      expect(await file.readAsString(), '{corrupt-synthetic-pending');
    },
  );

  test(
    'independent settings instances cannot replace or remove another queued ID',
    () async {
      final offline = ScriptedCloudHttp(
        (_) => throw const SocketException('offline'),
      );
      final controller = create(offline);
      await configure(controller);
      await expectLater(controller.syncNow(), throwsA(isA<CenterException>()));
      final first = CloudSupportSettings(sandbox),
          second = CloudSupportSettings(sandbox);
      final queued = (await first.readPending())!;
      await expectLater(
        second.savePending(queued),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        second.removePending('different-id'),
        throwsA(isA<CenterException>()),
      );
      expect((await first.readPending())!.body, queued.body);
    },
  );

  final invalidConfigurations =
      <String, ({String origin, String center, String token})>{
        'HTTP origin': (
          origin: 'http://support.example.invalid',
          center: 'center',
          token: _deviceToken,
        ),
        'credential URL': (
          origin: 'https://user:secret@support.example.invalid',
          center: 'center',
          token: _deviceToken,
        ),
        'origin path': (
          origin: 'https://support.example.invalid/api',
          center: 'center',
          token: _deviceToken,
        ),
        'origin query': (
          origin: 'https://support.example.invalid?secret=1',
          center: 'center',
          token: _deviceToken,
        ),
        'non ASCII center': (
          origin: _origin.toString(),
          center: 'سنتر',
          token: _deviceToken,
        ),
        'oversized center': (
          origin: _origin.toString(),
          center: List.filled(65, 'a').join(),
          token: _deviceToken,
        ),
        'oversized bearer': (
          origin: _origin.toString(),
          center: 'center',
          token: List.filled(1018, 'a').join(),
        ),
        'non ASCII bearer': (
          origin: _origin.toString(),
          center: 'center',
          token: 'اختبار_رمز_دخول_غير_صالح',
        ),
      };
  for (final scenario in invalidConfigurations.entries) {
    test(
      'invalid ${scenario.key} cannot replace a valid stored configuration',
      () async {
        final settings = CloudSupportSettings(sandbox);
        await settings.saveConfiguration(_configuration);
        final controller = create(
          ScriptedCloudHttp((_) => throw StateError('must not send')),
        );
        final invalid = scenario.value;
        await expectLater(
          controller.configure(
            Uri.parse(invalid.origin),
            invalid.center,
            invalid.token,
          ),
          throwsA(isA<CenterException>()),
        );
        expect(
          (await settings.readConfiguration())!.toJson(),
          _configuration.toJson(),
        );
      },
    );
  }
}
