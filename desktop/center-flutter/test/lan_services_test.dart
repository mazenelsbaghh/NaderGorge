import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/lan_discovery.dart';
import 'package:massar_center/lan/lan_host_process.dart';
import 'package:massar_center/lan/lan_settings.dart';
import 'package:massar_center/lan/lan_transport.dart';

void main() {
  Directory? buildDirectory;
  late String executable;
  setUpAll(() async {
    final override = Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'];
    if (override != null) {
      executable = override;
      expect(
        File(executable).existsSync(),
        isTrue,
        reason: 'The gateway override must exist.',
      );
      return;
    }
    buildDirectory = await Directory.systemTemp.createTemp(
      'massar-lan-native-build-',
    );
    executable =
        '${buildDirectory!.path}/massar-lan-host${Platform.isWindows ? '.exe' : ''}';
    final compiled = await Process.run('go', [
      'build',
      '-o',
      executable,
      '.',
    ], workingDirectory: '../center-lan');
    expect(
      compiled.exitCode,
      0,
      reason: 'The actual Go gateway must build: ${compiled.stderr}',
    );
  });
  tearDownAll(() async {
    if (buildDirectory != null) await buildDirectory!.delete(recursive: true);
  });
  late Directory directory;
  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-lan-services-');
  });
  tearDown(() async => directory.delete(recursive: true));

  test(
    'settings restart keeps identity and serial saves preserve paired credentials',
    () async {
      final settings = LanSettings(directory);
      final initial = await settings.read();
      final simultaneous = await Future.wait(
        List.generate(6, (_) => settings.read()),
      );
      expect(simultaneous.map((c) => c.deviceId).toSet(), {initial.deviceId});
      final endpoint = LanEndpoint(
        hostId: 'fixture-host',
        name: 'السنتر',
        address: '192.168.1.15',
        port: 43873,
        certificateSha256: 'a' * 64,
      );
      final paired = initial.copyWith(
        mode: LanMode.client,
        endpoint: endpoint,
        deviceToken: 'temporary-device-token',
      );
      await Future.wait([
        settings.save(initial.copyWith(mode: LanMode.host)),
        settings.save(paired),
      ]);
      final reloaded = await LanSettings(directory).read();
      expect(reloaded.toJson(), paired.toJson());
      await settings.save(
        reloaded.copyWith(
          mode: LanMode.standalone,
          clearEndpoint: true,
          clearDeviceToken: true,
        ),
      );
      final standalone = await settings.read();
      expect(standalone.deviceId, initial.deviceId);
      expect(standalone.endpoint, isNull);
      expect(standalone.deviceToken, isNull);
      expect(directory.listSync().whereType<File>(), hasLength(1));
    },
  );

  test(
    'corrupt or unwritable settings fail safely without switching modes',
    () async {
      final settings = LanSettings(directory);
      final original = await settings.read();
      final file = File(settings.filePath);
      const corrupt = '{"deviceToken":"do-not-disclose-secret"';
      await file.writeAsString(corrupt);
      await expectLater(
        settings.read(),
        throwsA(
          isA<CenterException>().having(
            (e) => e.message,
            'safe message',
            isNot(contains('do-not-disclose-secret')),
          ),
        ),
      );
      expect(await file.readAsString(), corrupt);
      await file.delete();
      await Directory(settings.filePath).create();
      await expectLater(
        settings.save(original),
        throwsA(isA<CenterException>()),
      );
      expect(await Directory(settings.filePath).exists(), isTrue);
      expect(directory.listSync(), hasLength(1));
      await Directory(settings.filePath).delete();
      await settings.save(original);
      expect((await settings.read()).deviceId, original.deviceId);
    },
  );

  test(
    'discovery ignores malformed protocol packets and deduplicates valid hosts',
    () async {
      final socket = await RawDatagramSocket.bind(
        InternetAddress.loopbackIPv4,
        0,
      );
      final subscription = socket.listen((event) {
        if (event != RawSocketEvent.read) return;
        final query = socket.receive();
        if (query == null) return;
        final request = jsonDecode(utf8.decode(query.data)) as Map;
        expect(request, {'kind': 'massar-discover', 'protocol': 1});
        for (final response in [
          'not JSON',
          jsonEncode({'kind': 'massar-host', 'protocol': 2}),
          jsonEncode({'kind': 'massar-host', 'protocol': 1, 'hostId': 5}),
          for (var duplicate = 0; duplicate < 2; duplicate++)
            jsonEncode({
              'kind': 'massar-host',
              'protocol': 1,
              'hostId': 'real-host',
              'name': 'سنتر الاختبار',
              'port': 43873,
              'certificateSha256': 'b' * 64,
            }),
        ]) {
          socket.send(utf8.encode(response), query.address, query.port);
        }
      });
      try {
        final endpoints = await LanDiscovery.search(
          timeout: const Duration(milliseconds: 250),
          discoveryPort: socket.port,
          targets: [InternetAddress.loopbackIPv4],
        );
        expect(endpoints, hasLength(1));
        expect(endpoints.single.hostId, 'real-host');
        expect(endpoints.single.address, '127.0.0.1');
        expect(endpoints.single.certificateSha256, 'b' * 64);
      } finally {
        await subscription.cancel();
        socket.close();
      }
    },
  );

  group('actual bundled Go gateway', () {
    late HttpServer upstream;
    late LanHostProcess gateway;
    late LanHostReady ready;
    late int discoveryPort;
    LanTransport? unpaired;
    LanTransport? paired;
    const secret = 'temporary-upstream-secret-long-enough-12345';
    var commandCount = 0;
    var apiCount = 0;
    var redirectVisits = 0;
    String? lastStaff;
    String? lastDevice;
    String? lastAuthorization;
    late StreamSubscription<HttpRequest> subscription;

    setUp(() async {
      unpaired = null;
      paired = null;
      commandCount = 0;
      apiCount = 0;
      redirectVisits = 0;
      upstream = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
      subscription = upstream.listen((request) async {
        if (request.uri.path == '/redirect-target') {
          redirectVisits++;
        } else {
          apiCount++;
          expect(request.headers.value('X-Massar-Bridge-Secret'), secret);
          lastStaff = request.headers.value('X-Massar-Session');
          lastDevice = request.headers.value('X-Massar-Device-ID');
          lastAuthorization = request.headers.value(
            HttpHeaders.authorizationHeader,
          );
        }
        if (request.uri.path == '/api/command') {
          commandCount++;
          final command =
              jsonDecode(await utf8.decoder.bind(request).join()) as Map;
          if (command['loseReply'] == true) {
            final socket = await request.response.detachSocket(
              writeHeaders: false,
            );
            socket.destroy();
            return;
          }
          if (command['knownReject'] == true) {
            request.response.statusCode = 400;
            request.response.write(
              jsonEncode({'message': 'الطالب مسجل بالفعل'}),
            );
            await request.response.close();
            return;
          }
        }
        if (request.uri.path == '/api/redirect') {
          request.response.statusCode = 302;
          request.response.headers.set(
            HttpHeaders.locationHeader,
            'http://127.0.0.1:${upstream.port}/redirect-target',
          );
        } else if (request.uri.path == '/api/large') {
          request.response.write(
            '{"large":"${'x' * (LanTransport.maxResponseBytes + 1)}"}',
          );
        } else {
          request.response.write(jsonEncode({'ok': true}));
        }
        await request.response.close();
      });
      final availablePort = await RawDatagramSocket.bind(
        InternetAddress.loopbackIPv4,
        0,
      );
      discoveryPort = availablePort.port;
      availablePort.close();
      gateway = LanHostProcess(executablePath: executable);
      ready = await gateway.start(
        dataDirectory: directory.path,
        upstreamUrl: 'http://127.0.0.1:${upstream.port}',
        upstreamSecret: secret,
        name: 'سنتر حقيقي',
        port: 0,
        discoveryPort: discoveryPort,
      );
      unpaired = LanTransport(ready.endpoint, '');
      final pairing = await unpaired!.pair(
        ready.pairingCode,
        'test-device',
        'جهاز الاختبار',
      );
      paired = LanTransport(ready.endpoint, pairing['token'] as String);
    });

    tearDown(() async {
      unpaired?.close();
      paired?.close();
      await gateway.dispose();
      await subscription.cancel();
      await upstream.close(force: true);
    });

    test(
      'TLS pin pairing controls revocation and restart use durable host identity',
      () async {
        expect((await paired!.health())['hostId'], ready.hostId);
        final discovered = await LanDiscovery.search(
          timeout: const Duration(milliseconds: 250),
          discoveryPort: discoveryPort,
          targets: [InternetAddress.loopbackIPv4],
        );
        expect(discovered.single.hostId, ready.hostId);
        expect(discovered.single.port, ready.port);
        expect(discovered.single.certificateSha256, ready.certificateSha256);
        await paired!.post('/api/command', {
          'test': true,
        }, staffSession: 'temporary-staff-session');
        expect(commandCount, 1);
        expect(lastStaff, 'temporary-staff-session');
        expect(lastDevice, 'test-device');
        expect(lastAuthorization, isNull);
        final devices = await gateway.devices();
        expect(devices.single['deviceId'], 'test-device');
        expect(devices.single['revoked'], false);
        final code = await gateway.controlPairing();
        expect(code['pairingCode'], isA<String>());
        expect(
          DateTime.parse(code['expiresAt'] as String).isAfter(DateTime.now()),
          isTrue,
        );
        await gateway.revokeDevice('test-device');
        await expectLater(
          paired!.get('/api/state'),
          throwsA(
            isA<LanAuthorizationException>().having(
              (e) => e.requiresPairing,
              'device revoked',
              true,
            ),
          ),
        );
        expect(apiCount, 1);
        final exited = gateway.exitCodes.first;
        await gateway.stop();
        await exited;
        expect(gateway.isRunning, false);
        final restarted = await gateway.start(
          dataDirectory: directory.path,
          upstreamUrl: 'http://127.0.0.1:${upstream.port}',
          upstreamSecret: secret,
          name: 'سنتر حقيقي',
          port: 0,
          discoveryPort: 0,
        );
        expect(restarted.hostId, ready.hostId);
        expect(restarted.certificateSha256, ready.certificateSha256);
      },
    );

    test(
      'wrong pin and redirects cannot send credentials to another host',
      () async {
        final wrong = LanTransport(
          LanEndpoint(
            hostId: ready.hostId,
            name: ready.name,
            address: ready.endpoint.address,
            port: ready.port,
            certificateSha256: '0' * 64,
          ),
          'secret-never-forwarded',
        );
        try {
          await expectLater(
            wrong.post('/api/command', {}),
            throwsA(
              isA<LanConnectionException>().having(
                (e) => e.outcomeUnknown,
                'uncertain mutation',
                true,
              ),
            ),
          );
        } finally {
          wrong.close();
        }
        expect(apiCount, 0);
        await expectLater(
          paired!.get('/api/redirect'),
          throwsA(
            isA<CenterException>().having(
              (e) => e.message,
              'redirect refused',
              contains('تعذر الاتصال'),
            ),
          ),
        );
        expect(redirectVisits, 0);
        expect(commandCount, 0);
      },
    );

    test(
      'lost command reply is explicitly unknown and is never retried',
      () async {
        await expectLater(
          paired!.post('/api/command', {'loseReply': true}),
          throwsA(
            isA<LanConnectionException>().having(
              (e) => e.outcomeUnknown,
              'unknown committed result',
              true,
            ),
          ),
        );
        expect(commandCount, 1);
        expect((await paired!.get('/api/state'))['ok'], true);
        expect(commandCount, 1);
        await expectLater(
          paired!.post('/api/command', {'knownReject': true}),
          throwsA(
            isA<CenterException>().having(
              (e) => e.message,
              'known business rejection',
              'الطالب مسجل بالفعل',
            ),
          ),
        );
        expect(commandCount, 2);
      },
    );

    test('body limits reject before send and bound snapshots', () async {
      await expectLater(
        paired!.post('/api/command', {
          'oversized': 'x' * LanTransport.maxRequestBytes,
        }),
        throwsA(isA<CenterException>()),
      );
      expect(commandCount, 0);
      await expectLater(
        paired!.get('/api/large'),
        throwsA(
          isA<CenterException>().having(
            (e) => e.message,
            'bounded response',
            contains('أكبر'),
          ),
        ),
      );
      expect(commandCount, 0);
    });
  });
}
