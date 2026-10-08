import 'dart:convert';

import 'package:crypto/crypto.dart';
import 'package:flutter/foundation.dart';
import 'package:uuid/uuid.dart';

class SnapshotTransferIdentity {
  const SnapshotTransferIdentity(this.deviceId, this.staffSession);
  final String deviceId, staffSession;
}

class _CapturedTransfer {
  _CapturedTransfer(this.identity, this.bytes, this.digest);
  final SnapshotTransferIdentity identity;
  final Uint8List bytes;
  final String digest;
  final DateTime createdAt = DateTime.now();
}

/// Chunks always belong to one immutable envelope and one authenticated session.
class LanSnapshotTransferCache {
  static const chunkBytes = 512 * 1024;
  static const maximumBytes = 64 * 1024 * 1024;
  final _captured = <String, _CapturedTransfer>{};

  Future<String> envelope(
    String encoded,
    SnapshotTransferIdentity identity,
  ) async {
    if (encoded.length < 1024 * 1024) return encoded;
    final prepared = await compute(_prepareTransfer, encoded);
    if (prepared.$1.length < 4 * 1024 * 1024) return encoded;
    if (prepared.$1.length > maximumBytes) {
      throw const FormatException('Snapshot exceeds transfer budget');
    }
    _prune();
    _captured.removeWhere(
      (_, transfer) => transfer.identity.staffSession == identity.staffSession,
    );
    while (_captured.isNotEmpty &&
        (_captured.length >= 4 ||
            _retainedBytes + prepared.$1.length > maximumBytes)) {
      _captured.remove(_captured.keys.first);
    }
    final token = const Uuid().v4();
    _captured[token] = _CapturedTransfer(identity, prepared.$1, prepared.$2);
    return jsonEncode({
      'staffSession': identity.staffSession,
      'snapshotTransfer': {
        'token': token,
        'chunks': (prepared.$1.length + chunkBytes - 1) ~/ chunkBytes,
        'bytes': prepared.$1.length,
        'sha256': prepared.$2,
      },
    });
  }

  int get _retainedBytes =>
      _captured.values.fold(0, (sum, transfer) => sum + transfer.bytes.length);

  Map<String, dynamic> chunk(
    String token,
    int index,
    SnapshotTransferIdentity identity,
  ) {
    _prune();
    final transfer = _captured[token];
    if (transfer == null ||
        transfer.identity.deviceId != identity.deviceId ||
        transfer.identity.staffSession != identity.staffSession ||
        index < 0 ||
        index * chunkBytes >= transfer.bytes.length) {
      throw const FormatException('Snapshot transfer unavailable');
    }
    final start = index * chunkBytes;
    final end = (start + chunkBytes).clamp(0, transfer.bytes.length);
    return {
      'token': token,
      'index': index,
      'chunk': base64Encode(Uint8List.sublistView(transfer.bytes, start, end)),
    };
  }

  void _prune() => _captured.removeWhere(
    (_, transfer) =>
        DateTime.now().difference(transfer.createdAt) >
        const Duration(seconds: 30),
  );

  void clear() => _captured.clear();
}

(Uint8List, String) _prepareTransfer(String encoded) {
  final bytes = Uint8List.fromList(utf8.encode(encoded));
  return (bytes, sha256.convert(bytes).toString());
}

Map<String, dynamic> verifyTransferredSnapshot((Uint8List, String) captured) {
  if (sha256.convert(captured.$1).toString() != captured.$2) {
    throw const FormatException('Snapshot digest mismatch');
  }
  return jsonDecode(utf8.decode(captured.$1)) as Map<String, dynamic>;
}
