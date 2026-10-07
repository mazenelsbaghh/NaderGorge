import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/lan_discovery.dart';

class _Socket extends Fake implements RawDatagramSocket {
  _Socket(this.blockedSends, {this.failure});
  final SocketException? failure;
  final int blockedSends;
  final events = StreamController<RawSocketEvent>();
  int sends = 0;
  bool closed = false;

  @override
  set broadcastEnabled(bool value) {}

  @override
  StreamSubscription<RawSocketEvent> listen(
    void Function(RawSocketEvent)? onData, {
    Function? onError,
    void Function()? onDone,
    bool? cancelOnError,
  }) => events.stream.listen(
    onData,
    onError: onError,
    onDone: onDone,
    cancelOnError: cancelOnError,
  );

  @override
  int send(List<int> buffer, InternetAddress address, int port) {
    sends++;
    if (sends == 1 && failure != null) events.addError(failure!);
    return sends <= blockedSends ? 0 : Uint8List.fromList(buffer).length;
  }

  @override
  void close() {
    closed = true;
    unawaited(events.close());
  }
}

void main() {
  test('temporarily blocked discovery send retries within deadline', () async {
    final socket = _Socket(2);
    final result = await LanDiscovery.search(
      timeout: const Duration(milliseconds: 110),
      targets: [InternetAddress.loopbackIPv4],
      bindSocket: () async => socket,
    );
    expect(result, isEmpty);
    expect(socket.sends, 3);
    expect(socket.closed, isTrue);
  });

  test(
    'socket failure preserves its OS cause instead of becoming a retry timeout',
    () async {
      const error = SocketException(
        'OS network failure',
        osError: OSError('fixture failure', 13),
      );
      final socket = _Socket(10000, failure: error);
      await expectLater(
        LanDiscovery.search(
          timeout: const Duration(milliseconds: 65),
          targets: [InternetAddress.loopbackIPv4],
          bindSocket: () async => socket,
        ),
        throwsA(
          isA<CenterException>().having(
            (e) => e.cause,
            'original OS error',
            same(error),
          ),
        ),
      );
      expect(socket.sends, 1);
      expect(socket.closed, isTrue);
    },
  );

  test(
    'permanently blocked send fails visibly and closes its socket',
    () async {
      final socket = _Socket(10000);
      await expectLater(
        LanDiscovery.search(
          timeout: const Duration(milliseconds: 65),
          targets: [InternetAddress.loopbackIPv4],
          bindSocket: () async => socket,
        ),
        throwsA(
          isA<CenterException>().having(
            (e) => e.cause,
            'network cause',
            isA<SocketException>(),
          ),
        ),
      );
      expect(socket.sends, inInclusiveRange(2, 5));
      expect(socket.closed, isTrue);
    },
  );
}
