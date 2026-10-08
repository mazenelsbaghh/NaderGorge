import 'dart:convert';

import 'dart:typed_data';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/lan/lan_snapshot_transfer.dart';

void main() {
  test(
    'large Unicode snapshot chunks retain exact content and session isolation',
    () async {
      final cache = LanSnapshotTransferCache();
      const identity = SnapshotTransferIdentity(
        'synthetic-device',
        'synthetic-session',
      );
      final expected = {
        'state': {'notes': List.filled(800000, 'ح📘').join()},
        'stateVersion': 'fixed-version',
      };
      final envelope =
          jsonDecode(await cache.envelope(jsonEncode(expected), identity))
              as Map<String, dynamic>;
      final descriptor = envelope['snapshotTransfer'] as Map<String, dynamic>;
      final token = descriptor['token'] as String;
      final count = descriptor['chunks'] as int;
      final received = BytesBuilder(copy: false);
      for (var index = 0; index < count; index++) {
        final chunk = cache.chunk(token, index, identity);
        received.add(base64Decode(chunk['chunk'] as String));
      }
      final bytes = received.takeBytes();
      expect(bytes.length, descriptor['bytes']);
      expect(
        verifyTransferredSnapshot((bytes, descriptor['sha256'] as String)),
        expected,
      );
      expect(
        () => cache.chunk(
          token,
          0,
          const SnapshotTransferIdentity('other-device', 'synthetic-session'),
        ),
        throwsFormatException,
      );
      expect(
        () => cache.chunk(
          token,
          0,
          const SnapshotTransferIdentity('synthetic-device', 'other-session'),
        ),
        throwsFormatException,
      );
      expect(() => cache.chunk(token, count, identity), throwsFormatException);
      bytes[bytes.length ~/ 2] ^= 1;
      expect(
        () =>
            verifyTransferredSnapshot((bytes, descriptor['sha256'] as String)),
        throwsFormatException,
      );
      cache.clear();
      expect(() => cache.chunk(token, 0, identity), throwsFormatException);
    },
  );
}
