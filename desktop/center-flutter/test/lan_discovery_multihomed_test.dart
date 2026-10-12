import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/lan_discovery.dart';

void main() {
  test('one failed adapter does not discard a reachable host', () async {
    final listener = await ServerSocket.bind(InternetAddress.loopbackIPv4, 0);
    final connections = listener.listen((socket) => socket.destroy());
    final responder = await RawDatagramSocket.bind(
      InternetAddress.loopbackIPv4,
      0,
    );
    final queries = <String>[];
    final replies = responder.listen((event) {
      if (event != RawSocketEvent.read) return;
      final query = responder.receive();
      if (query == null) return;
      queries.add(query.address.address);
      responder.send(_host(listener.port), query.address, query.port);
    });
    final failedSource = InternetAddress('192.0.2.1');
    try {
      final result = await LanDiscovery.search(
        timeout: const Duration(milliseconds: 500),
        discoveryPort: responder.port,
        targets: [InternetAddress.loopbackIPv4],
        listSourceAddresses: () async => [
          failedSource,
          InternetAddress.loopbackIPv4,
        ],
        bindSourceSocket: (source) {
          if (source.address == failedSource.address) {
            throw const SocketException('Adapter disconnected');
          }
          return RawDatagramSocket.bind(source, 0);
        },
      );
      expect(result, hasLength(1));
      expect(result.single.hostId, 'center-host');
      expect(result.single.port, listener.port);
      expect(queries, ['127.0.0.1']);
    } finally {
      await replies.cancel();
      responder.close();
      await connections.cancel();
      await listener.close();
    }
  });

  test(
    'duplicate host prefers a reachable endpoint regardless of order',
    () async {
      final listener = await ServerSocket.bind(InternetAddress.loopbackIPv4, 0);
      final connections = listener.listen((socket) => socket.destroy());
      final unreachable = await ServerSocket.bind(
        InternetAddress.loopbackIPv4,
        0,
      );
      final closedPort = unreachable.port;
      await unreachable.close();
      final responder = await RawDatagramSocket.bind(
        InternetAddress.loopbackIPv4,
        0,
      );
      final replies = responder.listen((event) {
        if (event != RawSocketEvent.read) return;
        final query = responder.receive();
        if (query == null) return;
        // Repeating the unavailable endpoint last must not replace the live one.
        for (final port in [closedPort, listener.port, closedPort]) {
          responder.send(_host(port), query.address, query.port);
        }
      });
      try {
        final result = await LanDiscovery.search(
          timeout: const Duration(milliseconds: 500),
          discoveryPort: responder.port,
          targets: [InternetAddress.loopbackIPv4],
          listSourceAddresses: () async => [InternetAddress.loopbackIPv4],
        );
        expect(result, hasLength(1));
        expect(result.single.port, listener.port);
      } finally {
        await replies.cancel();
        responder.close();
        await connections.cancel();
        await listener.close();
      }
    },
  );

  test('failure of all adapters preserves the individual OS causes', () async {
    const first = SocketException('First adapter unavailable');
    const second = SocketException('Second adapter unavailable');
    await expectLater(
      LanDiscovery.search(
        timeout: const Duration(milliseconds: 500),
        listSourceAddresses: () async => [
          InternetAddress('192.0.2.1'),
          InternetAddress('192.0.2.2'),
        ],
        bindSourceSocket: (source) async {
          throw source.address.endsWith('.1') ? first : second;
        },
      ),
      throwsA(
        isA<CenterException>().having(
          (error) => error.cause,
          'original network failures',
          unorderedEquals([same(first), same(second)]),
        ),
      ),
    );
  });

  test('socket binding that completes after timeout is closed', () async {
    final pending = Completer<RawDatagramSocket>();
    await expectLater(
      LanDiscovery.search(
        timeout: const Duration(milliseconds: 30),
        targets: [InternetAddress.loopbackIPv4],
        bindSocket: () => pending.future,
      ),
      throwsA(isA<CenterException>()),
    );
    final lateSocket = await RawDatagramSocket.bind(
      InternetAddress.loopbackIPv4,
      0,
      reuseAddress: false,
    );
    final port = lateSocket.port;
    final closed = Completer<void>();
    lateSocket.listen((_) {}, onDone: closed.complete);
    pending.complete(lateSocket);
    await closed.future.timeout(const Duration(seconds: 1));
    // Exclusive rebinding proves the late socket no longer owns its port.
    final replacement = await RawDatagramSocket.bind(
      InternetAddress.loopbackIPv4,
      port,
      reuseAddress: false,
    );
    replacement.close();
  });
}

List<int> _host(int port) => utf8.encode(
  jsonEncode({
    'kind': 'massar-host',
    'protocol': 1,
    'hostId': 'center-host',
    'name': 'السنتر',
    'port': port,
    'certificateSha256': 'b' * 64,
  }),
);
