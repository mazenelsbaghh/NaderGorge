import 'dart:io';

import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/exams/exam_engine.dart';
import 'package:massar_center/exams/exam_room_controller.dart';
import 'package:massar_center/lan/lan_transport.dart';

/// Only the native exam process boundary is replaced. This is the real client
/// store, disconnected from its host and with no authenticated employee.
class DisconnectedExamNetworkFixture {
  DisconnectedExamNetworkFixture._(
    this.directory,
    this.transport,
    this.store,
    this.engine,
    this.controller,
  );
  final Directory directory;
  final LanTransport transport;
  final CenterStore store;
  final NetworkEngineFixture engine;
  final ExamRoomController controller;

  static Future<DisconnectedExamNetworkFixture> open() async {
    final directory = await Directory.systemTemp.createTemp(
      'exam-login-network-',
    );
    final transport = LanTransport(
      LanEndpoint(
        hostId: 'unavailable-host',
        name: 'الهوست',
        address: '127.0.0.1',
        port: 1,
        certificateSha256: 'a' * 64,
      ),
      'fixture-pairing-token',
    );
    final store = CenterStore.remote(transport, localDirectory: directory.path);
    final engine = NetworkEngineFixture();
    final controller = ExamRoomController(
      engine: engine,
      store: store,
      pollInterval: const Duration(days: 1),
    );
    return DisconnectedExamNetworkFixture._(
      directory,
      transport,
      store,
      engine,
      controller,
    );
  }

  Future<void> close() async {
    controller.dispose();
    await store.close();
    transport.close();
    await directory.delete(recursive: true);
  }
}

class NetworkEngineFixture implements ExamEngine {
  @override
  bool running = false;
  bool nativeRoomActive = false;
  bool leaseHeld = false;
  bool failHotspotChange = false;
  bool failRelease = false;
  bool tetheringActive = false;
  bool confirmTetheringStart = true;
  final routes = <String>[];
  final adminAccess = <bool>[];
  final commands = <Map<String, dynamic>>[];
  @override
  Future<void> start() async => running = true;
  @override
  Future<bool> reconnect() async => running;
  @override
  Future<void> close() async {}

  @override
  Future<Map<String, dynamic>> request(
    String route, [
    Map<String, dynamic>? body,
  ]) async {
    routes.add(route);
    switch (route) {
      case '/api/center/admin-access':
        adminAccess.add(body!['enabled'] as bool);
        return {'enabled': body['enabled']};
      case '/api/network/maintenance/acquire':
        if (nativeRoomActive || leaseHeld) {
          throw const CenterException('هناك امتحان جارٍ أو تجهيز شبكة قائم.');
        }
        leaseHeld = true;
        return {
          'leaseToken': 'fixture-maintenance-token',
          'expiresAt': DateTime.now().millisecondsSinceEpoch ~/ 1000 + 600,
        };
      case '/api/network/maintenance/release':
        if (failRelease) {
          throw const CenterException('لم يرد محرك الامتحان عند تحرير الحجز.');
        }
        if (body!['leaseToken'] != 'fixture-maintenance-token') {
          throw StateError('Wrong lease token.');
        }
        leaseHeld = false;
        return {'released': true};
      default:
        throw StateError(
          'Pre-login network setup requested an exam/data route: $route',
        );
    }
  }

  @override
  Future<Map<String, dynamic>> network(Map<String, dynamic> command) async {
    commands.add(Map.of(command));
    switch (command['action']) {
      case 'status':
        return {
          'ok': true,
          'configured': true,
          'adapterId': 'ethernet',
          'dhcpReady': true,
          'dnsReady': true,
          'portalReady': true,
          'portalMode': 'legacy',
          'studentUrl': 'http://10.77.0.1/',
        };
      case 'list-adapters':
        return {
          'adapters': [
            {
              'id': 'ethernet',
              'name': 'Omada Ethernet',
              'status': 'Up',
              'wireless': false,
            },
          ],
        };
      case 'tethering-status':
        return {'ok': true, 'active': tetheringActive, 'ssid': 'Host trial'};
      case 'hotspot-status':
        return {'active': false};
      case 'hotspot-start':
      case 'hotspot-stop':
      case 'tethering-start':
      case 'tethering-stop':
        if (!running || !leaseHeld) {
          throw StateError(
            'A hotspot mutation requires the native maintenance lease.',
          );
        }
        if (failHotspotChange) {
          throw const CenterException('فشل تشغيل شبكة الجهازين.');
        }
        if (command['action'] == 'tethering-start') {
          tetheringActive = confirmTetheringStart;
          return {'ok': true, 'active': tetheringActive, 'ssid': 'Host trial'};
        }
        if (command['action'] == 'tethering-stop') tetheringActive = false;
        return {'ok': true};
      case 'configure':
      case 'restore':
      case 'portal-configure':
        if (!running) {
          throw StateError('Native safety service must be started first.');
        }
        if (nativeRoomActive) {
          throw const CenterException('أغلق الامتحان قبل تجهيز الشبكة.');
        }
        return {'ok': true};
      default:
        throw StateError('Unexpected network action ${command['action']}');
    }
  }
}
