import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart' as crypto;
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/exams/omada_controller_service.dart';

void main() {
  late Directory tlsDirectory;
  late _ControllerFixture controller;
  late OmadaOpenApiService service;

  setUpAll(() async {
    tlsDirectory = await Directory.systemTemp.createTemp('omada-test-tls-');
    final generated = await Process.run('openssl', [
      'req',
      '-x509',
      '-newkey',
      'rsa:2048',
      '-nodes',
      '-keyout',
      '${tlsDirectory.path}/localhost-key.pem',
      '-out',
      '${tlsDirectory.path}/localhost-cert.pem',
      '-days',
      '1',
      '-subj',
      '/CN=Massar Omada Ephemeral Test Only',
    ]);
    if (generated.exitCode != 0) {
      await tlsDirectory.delete(recursive: true);
      fail(
        'The TLS contract tests require openssl to generate an ephemeral test certificate.',
      );
    }
  });
  tearDownAll(() async {
    if (await tlsDirectory.exists()) await tlsDirectory.delete(recursive: true);
  });
  setUp(() async {
    controller = await _ControllerFixture.start(tlsDirectory);
    service = OmadaOpenApiService(controller.config());
  });
  tearDown(() async {
    service.close();
    await controller.close();
  });

  test(
    'certificate probe sends no credentials and only the accepted pin authenticates',
    () async {
      final untrusted = OmadaOpenApiService(controller.config(pin: ''));
      addTearDown(untrusted.close);
      final certificate = await untrusted.probeCertificate(controller.origin);
      expect(certificate.sha256, controller.pin);
      expect(controller.requests, isEmpty);
      await expectLater(
        untrusted.listSites(),
        throwsOmada('certificate_not_trusted'),
      );
      expect(controller.requests, isEmpty);

      final wrongPin = OmadaOpenApiService(controller.config(pin: 'a' * 64));
      addTearDown(wrongPin.close);
      await expectLater(
        wrongPin.listSites(),
        throwsOmada('certificate_not_trusted'),
      );
      expect(controller.requests, isEmpty);

      expect((await service.listSites()).single.name, 'قاعة الامتحان');
      final tokenRequest = controller.requests.first;
      expect(tokenRequest.path, '/openapi/authorize/token');
      expect(tokenRequest.query, {'grant_type': 'client_credentials'});
      expect(tokenRequest.authorization, isNull);
      expect(tokenRequest.body, {
        'omadacId': 'local-controller',
        'client_id': 'fixture-client',
        'client_secret': 'fixture-client-secret',
      });
      expect(
        controller.requests.last.authorization,
        'AccessToken=fixture-access-token',
      );
    },
  );

  test(
    'creates only selected staff SSID and verifies full student settings unchanged',
    () async {
      final studentBefore = jsonEncode(controller.ssids['students']);
      final preview = await _preview(service);
      expect(preview.preservedSsids.single.name, 'شبكة الطلاب الأصلية');
      expect(preview.accessPointMac, _apMac);
      var callbackCount = 0;
      final result = await service.applyStaffWifi(
        preview,
        beforeWrite: () async {
          callbackCount++;
          expect(controller.createRequests, isEmpty);
        },
      );
      expect(callbackCount, 1);
      expect(result.ssid.name, 'Massar Staff');
      expect(result.existingSsidsUnchanged, isTrue);
      expect(result.physicalBroadcastVerified, isFalse);
      expect(result.recoveredAfterUncertainCreate, isFalse);
      expect(jsonEncode(controller.ssids['students']), studentBefore);
      expect(controller.createRequests, hasLength(1));
      final created = controller.createRequests.single.body!;
      expect(created['apGroupIds'], ['existing-group']);
      expect(created['guestNetEnable'], isFalse);
      expect(created['vlanEnable'], isFalse);
      expect(created['pskSetting'], {
        'securityKey': 'Staff-Password-42',
        'versionPsk': 2,
        'encryptionPsk': 3,
        'gikRekeyPskEnable': false,
      });
      expect(
        controller.requests.where(
          (request) => !['GET', 'POST'].contains(request.method),
        ),
        isEmpty,
      );
      expect(
        controller.requests
            .where((request) => request.method == 'POST')
            .map((request) => request.path)
            .toSet(),
        {'/openapi/authorize/token', _ssidCollection},
      );
      await service.applyStaffWifi(preview);
      expect(
        controller.createRequests,
        hasLength(1),
        reason: 'A repeated confirmation must be read-only.',
      );
    },
  );

  test(
    'lost creation response is reconciled by GET without a second POST',
    () async {
      controller.dropCreateResponse = true;
      final preview = await _preview(service);
      final result = await service.applyStaffWifi(preview);
      expect(result.recoveredAfterUncertainCreate, isTrue);
      expect(result.ssid.name, 'Massar Staff');
      await service.applyStaffWifi(preview);
      expect(controller.createRequests, hasLength(1));
    },
  );

  test(
    'unknown outcome stays blocked even when a delayed readback finds no new SSID',
    () async {
      controller.dropCreateResponse = true;
      controller.commitCreation = false;
      final preview = await _preview(service);
      await expectLater(
        service.applyStaffWifi(preview),
        throwsOmada('create_outcome_unknown'),
      );
      await expectLater(
        service.applyStaffWifi(preview),
        throwsOmada('create_outcome_unknown'),
      );
      await expectLater(
        _preview(service),
        throwsOmada('previous_attempt_pending'),
      );
      expect(controller.createRequests, hasLength(1));
    },
  );

  test('a student setting changed since preview prevents creation', () async {
    final preview = await _preview(service);
    controller.ssids['students']!['macFilter'] = {
      'enabled': true,
      'mode': 'changed-externally',
    };
    await expectLater(
      service.applyStaffWifi(preview),
      throwsOmada('existing_ssid_changed'),
    );
    expect(controller.createRequests, isEmpty);
  });

  test(
    'concurrent controller change after POST is reported without rollback or further mutation',
    () async {
      controller.changeStudentAfterCreation = true;
      final preview = await _preview(service);
      await expectLater(
        service.applyStaffWifi(preview),
        throwsOmada('existing_ssid_changed'),
      );
      expect(controller.createRequests, hasLength(1));
      expect(
        controller.requests.where(
          (r) => ['PUT', 'PATCH', 'DELETE'].contains(r.method),
        ),
        isEmpty,
      );
    },
  );

  test(
    'same name in another WLAN refuses creation instead of modifying or adopting it',
    () async {
      controller.otherGroupStaffName = true;
      await expectLater(_preview(service), throwsOmada('ssid_name_exists'));
      expect(controller.createRequests, isEmpty);
    },
  );

  test(
    'AP must already belong to the explicitly selected WLAN and still be connected',
    () async {
      controller.apGroup = 'different-group';
      await expectLater(_preview(service), throwsOmada('ap_group_mismatch'));
      controller.apGroup = 'existing-group';
      final preview = await _preview(service);
      controller.apConnected = false;
      await expectLater(
        service.applyStaffWifi(preview),
        throwsOmada('ap_not_connected'),
      );
      expect(controller.createRequests, isEmpty);
    },
  );

  test(
    'revoked permission or closing the view before write sends no SSID mutation',
    () async {
      final preview = await _preview(service);
      await expectLater(
        service.applyStaffWifi(
          preview,
          beforeWrite: () async {
            throw const OmadaException(
              'تغيرت جلسة التشغيل.',
              code: 'view_changed',
            );
          },
        ),
        throwsOmada('view_changed'),
      );
      expect(controller.createRequests, isEmpty);
      await expectLater(
        service.applyStaffWifi(
          preview,
          beforeWrite: () async {
            service.close();
          },
        ),
        throwsOmada('connection_closed'),
      );
      expect(controller.createRequests, isEmpty);
    },
  );

  test(
    'untrusted origins and invalid passwords fail without sending credentials',
    () async {
      for (final address in [
        'http://127.0.0.1:8043',
        'https://example.com',
        'https://user:secret@127.0.0.1',
        'https://127.0.0.1/login',
        'https://127.0.0.1?secret=value',
      ]) {
        expect(
          () => OmadaOpenApiService(
            OmadaControllerConfig(
              baseUri: Uri.parse(address),
              omadaId: 'controller',
              clientId: 'client',
              clientSecret: 'secret',
            ),
          ),
          throwsOmada('invalid_controller_address'),
        );
      }
      await expectLater(
        _preview(service, password: 'short'),
        throwsOmada('invalid_wifi_password'),
      );
      expect(controller.requests, isEmpty);
    },
  );

  test(
    'controller error messages cannot disclose credentials or raw bodies',
    () async {
      controller.rejectReads = true;
      await expectLater(
        service.listSites(),
        throwsA(
          isA<OmadaException>()
              .having(
                (error) => error.message,
                'redacted message',
                isNot(contains('fixture-client-secret')),
              )
              .having(
                (error) => error.code,
                'stable safe code',
                'controller_rejected',
              ),
        ),
      );
      expect(controller.createRequests, isEmpty);
    },
  );

  test('redirects do not forward controller tokens to a new URL', () async {
    controller.redirectReads = true;
    await expectLater(
      service.listSites(),
      throwsOmada('controller_http_error'),
    );
    expect(
      controller.requests.where(
        (request) => request.path == '/redirect-target',
      ),
      isEmpty,
    );
    expect(
      controller.requests.where((request) => request.method == 'GET'),
      hasLength(1),
    );
  });

  test('a stalled local controller reaches the configured timeout', () async {
    controller.stallReads = true;
    final bounded = OmadaOpenApiService(
      controller.config(),
      timeout: const Duration(milliseconds: 500),
    );
    addTearDown(bounded.close);
    await expectLater(bounded.listSites(), throwsOmada('connection_failed'));
    expect(controller.createRequests, isEmpty);
  });

  test(
    'oversized paginated listings stop without fetching unbounded pages',
    () async {
      controller.oversizedListing = true;
      await expectLater(service.listSites(), throwsOmada('invalid_response'));
      expect(
        controller.requests.where((request) => request.method == 'GET'),
        hasLength(1),
      );
    },
  );
}

Matcher throwsOmada(String code) =>
    throwsA(isA<OmadaException>().having((error) => error.code, 'code', code));
Future<OmadaStaffWifiPreview> _preview(
  OmadaControllerService service, {
  String password = 'Staff-Password-42',
}) => service.previewStaffWifi(
  siteId: 'exam-site',
  wlanId: 'existing-group',
  accessPointMac: _apMac,
  studentSsidId: 'students',
  staffName: 'Massar Staff',
  staffPassword: password,
);

const _apMac = 'AA-BB-CC-DD-EE-01';
const _api = '/openapi/v1/local-controller/sites/exam-site';
const _ssidCollection = '$_api/wireless-network/wlans/existing-group/ssids';

class _ObservedRequest {
  _ObservedRequest(
    this.method,
    this.path,
    this.query,
    this.authorization,
    this.body,
  );
  final String method;
  final String path;
  final Map<String, String> query;
  final String? authorization;
  final Map<String, dynamic>? body;
}

/// Synthetic controller responses follow the official v1 OpenAPI schemas. The
/// TLS listener tests real serialization/authentication/pinning, not mocked I/O.
class _ControllerFixture {
  _ControllerFixture(this.server, this.pin);
  final HttpServer server;
  final String pin;
  final requests = <_ObservedRequest>[];
  final ssids = <String, Map<String, dynamic>>{
    'students': {
      'id': 'students',
      'name': 'شبكة الطلاب الأصلية',
      'band': 3,
      'security': 3,
      'broadcast': true,
      'guestNetEnable': false,
      'vlanEnable': false,
      'deviceType': 1,
      'ssidEnable': true,
      'apGroupIds': ['existing-group'],
      'pskSetting': {
        'securityKey': 'Student-Fixture-Secret',
        'versionPsk': 2,
        'encryptionPsk': 3,
      },
      'macFilter': {'enabled': false, 'mode': 'allow'},
    },
  };
  bool dropCreateResponse = false;
  bool commitCreation = true;
  bool changeStudentAfterCreation = false;
  bool otherGroupStaffName = false;
  bool apConnected = true;
  bool rejectReads = false;
  bool redirectReads = false;
  bool stallReads = false;
  bool oversizedListing = false;
  String apGroup = 'existing-group';
  List<_ObservedRequest> get createRequests => requests
      .where((r) => r.method == 'POST' && r.path == _ssidCollection)
      .toList();
  Uri get origin => Uri.parse('https://127.0.0.1:${server.port}');

  static Future<_ControllerFixture> start(Directory tlsDirectory) async {
    final certFile = File('${tlsDirectory.path}/localhost-cert.pem');
    final keyFile = File('${tlsDirectory.path}/localhost-key.pem');
    final context = SecurityContext()
      ..useCertificateChain(certFile.path)
      ..usePrivateKey(keyFile.path);
    final pem = await certFile.readAsString();
    final der = base64Decode(
      pem.replaceAll(RegExp(r'-----[^\n]+-----|\s'), ''),
    );
    final server = await HttpServer.bindSecure(
      InternetAddress.loopbackIPv4,
      0,
      context,
    );
    final fixture = _ControllerFixture(
      server,
      crypto.sha256.convert(der).toString(),
    );
    server.listen(
      (request) => unawaited(fixture.handle(request)),
      onError: (Object error) {
        // Expected handshake failures happen before an HTTP request reaches us.
        if (error is! HandshakeException) throw error;
      },
    );
    return fixture;
  }

  OmadaControllerConfig config({String? pin}) => OmadaControllerConfig(
    baseUri: origin,
    omadaId: 'local-controller',
    clientId: 'fixture-client',
    clientSecret: 'fixture-client-secret',
    certificateSha256: pin == '' ? null : pin ?? this.pin,
  );

  Future<void> handle(HttpRequest request) async {
    final rawBody = await utf8.decoder.bind(request).join();
    final body = rawBody.isEmpty
        ? null
        : jsonDecode(rawBody) as Map<String, dynamic>;
    requests.add(
      _ObservedRequest(
        request.method,
        request.uri.path,
        request.uri.queryParameters,
        request.headers.value(HttpHeaders.authorizationHeader),
        body,
      ),
    );
    if (request.uri.path == '/openapi/authorize/token') {
      await reply(request, {
        'accessToken': 'fixture-access-token',
        'tokenType': 'bearer',
        'expiresIn': 7200,
      });
      return;
    }
    if (request.headers.value(HttpHeaders.authorizationHeader) !=
        'AccessToken=fixture-access-token') {
      request.response.statusCode = 401;
      await request.response.close();
      return;
    }
    if (stallReads) return;
    if (redirectReads) {
      request.response.statusCode = 302;
      request.response.headers.set(
        HttpHeaders.locationHeader,
        '/redirect-target',
      );
      await request.response.close();
      return;
    }
    if (oversizedListing) {
      await reply(request, {
        'currentPage': 1,
        'totalRows': 1000000,
        'data': [],
      });
      return;
    }
    if (rejectReads) {
      request.response.write(
        jsonEncode({
          'errorCode': -1,
          'msg': 'fixture-client-secret raw diagnostic',
          'result': null,
        }),
      );
      await request.response.close();
      return;
    }
    final path = request.uri.path;
    if (path == '/openapi/v1/local-controller/sites') {
      await grid(request, [
        {'siteId': 'exam-site', 'name': 'قاعة الامتحان'},
      ]);
    } else if (path == '$_api/devices') {
      await grid(request, [
        {
          'mac': _apMac,
          'type': 'ap',
          'name': 'Hall AP',
          'model': 'EAP-Fixture',
          'status': apConnected ? 1 : 0,
          'detailStatus': apConnected ? 14 : 0,
        },
      ]);
    } else if (path == '$_api/ap-groups/existing-group/aps') {
      await grid(request, [
        {'mac': _apMac, 'apGroupId': apGroup, 'statusCategory': 1},
      ]);
    } else if (path == '$_api/wireless-network/wlans') {
      await reply(request, [
        {'id': 'existing-group', 'name': 'قاعة الطلاب'},
        if (otherGroupStaffName) {'id': 'another-group', 'name': 'قاعة أخرى'},
      ]);
    } else if (path == '$_api/wireless-network/wlans/another-group/ssids') {
      await grid(request, [
        {...ssids['students']!, 'id': 'other-staff', 'name': 'Massar Staff'},
      ]);
    } else if (path == _ssidCollection && request.method == 'POST') {
      if (commitCreation) ssids['new-staff'] = {'id': 'new-staff', ...body!};
      if (changeStudentAfterCreation) ssids['students']!['broadcast'] = false;
      if (dropCreateResponse) {
        final socket = await request.response.detachSocket(writeHeaders: false);
        socket.destroy();
      } else {
        await reply(request, {'id': 'new-staff'});
      }
    } else if (path == _ssidCollection) {
      await grid(request, ssids.values.toList());
    } else if (path.startsWith('$_ssidCollection/') &&
        ssids.containsKey(path.split('/').last)) {
      await reply(request, ssids[path.split('/').last]);
    } else {
      request.response.statusCode = 404;
      await request.response.close();
    }
  }

  Future<void> reply(HttpRequest request, Object? result) async {
    request.response.headers.contentType = ContentType.json;
    request.response.write(
      jsonEncode({'errorCode': 0, 'msg': 'Success', 'result': result}),
    );
    await request.response.close();
  }

  Future<void> grid(HttpRequest request, List<Object?> rows) => reply(request, {
    'currentPage': int.parse(request.uri.queryParameters['page']!),
    'totalRows': rows.length,
    'currentSize': rows.length,
    'data': rows,
  });
  Future<void> close() async => server.close(force: true);
}
