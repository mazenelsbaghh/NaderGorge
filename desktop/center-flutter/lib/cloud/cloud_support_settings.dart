import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:path/path.dart' as path;
import 'package:uuid/uuid.dart';

import '../domain/models.dart';

class CloudSupportConfiguration {
  CloudSupportConfiguration({
    required Uri origin,
    required String centerId,
    required String deviceToken,
  }) : origin = _validatedOrigin(origin),
       centerId = centerId.trim(),
       deviceToken = deviceToken.trim() {
    if (!RegExp(r'^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$').hasMatch(this.centerId) ||
        this.deviceToken.length < 16 ||
        this.deviceToken.length > 1017 ||
        !RegExp(r'^[\x21-\x7e]+$').hasMatch(this.deviceToken)) {
      throw const CenterException('بيانات ربط الدعم غير صالحة.');
    }
  }

  final Uri origin;
  final String centerId, deviceToken;

  Map<String, dynamic> toJson() => {
    'version': 1,
    'origin': origin.toString(),
    'centerId': centerId,
    'deviceToken': deviceToken,
  };

  factory CloudSupportConfiguration.fromJson(Map<String, dynamic> json) {
    if (json['version'] != 1) {
      throw const FormatException('Unsupported support settings');
    }
    return CloudSupportConfiguration(
      origin: Uri.parse(json['origin'] as String),
      centerId: json['centerId'] as String,
      deviceToken: json['deviceToken'] as String,
    );
  }
}

Uri _validatedOrigin(Uri origin) {
  if (origin.scheme != 'https' ||
      origin.host.isEmpty ||
      origin.userInfo.isNotEmpty ||
      origin.hasQuery ||
      origin.hasFragment ||
      (origin.path.isNotEmpty && origin.path != '/') ||
      origin.port < 1 ||
      origin.port > 65535) {
    throw const CenterException(
      'استخدم عنوان HTTPS للخادم دون مسار أو بيانات دخول.',
    );
  }
  return Uri(
    scheme: 'https',
    host: origin.host,
    port: origin.hasPort && origin.port != 443 ? origin.port : null,
  );
}

class PendingSupportUpload {
  PendingSupportUpload({
    required this.uploadId,
    required this.origin,
    required this.centerId,
    required this.createdAt,
    required this.body,
  });

  static const maxBytes = 128 * 1024 * 1024;
  final String uploadId, centerId, body;
  final Uri origin;
  final DateTime createdAt;

  Map<String, dynamic> toJson() => {
    'version': 1,
    'uploadId': uploadId,
    'origin': origin.toString(),
    'centerId': centerId,
    'createdAt': createdAt.toUtc().toIso8601String(),
    'body': body,
  };

  factory PendingSupportUpload.fromJson(Map<String, dynamic> json) {
    if (json['version'] != 1) {
      throw const FormatException('Unsupported pending upload');
    }
    final pending = PendingSupportUpload(
      uploadId: json['uploadId'] as String,
      origin: _validatedOrigin(Uri.parse(json['origin'] as String)),
      centerId: json['centerId'] as String,
      createdAt: DateTime.parse(json['createdAt'] as String),
      body: json['body'] as String,
    );
    final envelope = jsonDecode(pending.body) as Map<String, dynamic>;
    if (!_uuidPattern.hasMatch(pending.uploadId) ||
        !pending.createdAt.isUtc ||
        pending.centerId.isEmpty ||
        pending.centerId.length > 128 ||
        utf8.encode(pending.body).length > maxBytes ||
        envelope['format'] != 'massar-support-upload-v1' ||
        envelope['uploadId'] != pending.uploadId ||
        envelope['centerId'] != pending.centerId ||
        envelope['createdAt'] != pending.createdAt.toIso8601String() ||
        !_validPayload(envelope) ||
        envelope['diagnostics'] is! String ||
        utf8.encode(envelope['diagnostics'] as String).length >
            6 * 1024 * 1024) {
      throw const FormatException('Invalid pending support upload');
    }
    return pending;
  }

  static bool _validPayload(Map<String, dynamic> envelope) {
    final app = envelope['app'];
    if (app is! Map) return false;
    return switch (envelope['kind']) {
      'database' => app['role'] == 'host' && envelope['data'] is Map,
      'diagnostics' => app['role'] == 'client' && envelope['data'] == null,
      _ => false,
    };
  }
}

final _uuidPattern = RegExp(
  r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
);

class SupportUploadReceipt {
  const SupportUploadReceipt({
    required this.uploadId,
    required this.receiptId,
    required this.sha256,
    required this.receivedAt,
  });
  final String uploadId, receiptId, sha256;
  final DateTime receivedAt;

  Map<String, dynamic> toJson() => {
    'uploadId': uploadId,
    'receiptId': receiptId,
    'sha256': sha256,
    'receivedAt': receivedAt.toUtc().toIso8601String(),
  };

  factory SupportUploadReceipt.fromJson(Map<String, dynamic> json) {
    final receipt = SupportUploadReceipt(
      uploadId: json['uploadId'] as String,
      receiptId: json['receiptId'] as String,
      sha256: json['sha256'] as String,
      receivedAt: _receiptTime(json['receivedAt'] as String),
    );
    if (!_uuidPattern.hasMatch(receipt.uploadId) ||
        !_uuidPattern.hasMatch(receipt.receiptId) ||
        !receipt.receivedAt.isUtc ||
        !RegExp(r'^[0-9a-f]{64}$').hasMatch(receipt.sha256)) {
      throw const FormatException('Invalid support receipt');
    }
    return receipt;
  }
}

DateTime _receiptTime(String timestamp) {
  final parts = RegExp(
    r'^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d{1,9})?(?:Z|\+00:00)$',
  ).firstMatch(timestamp);
  if (parts == null) throw const FormatException('Invalid receipt UTC time');
  final parsed = DateTime.parse(timestamp);
  final components = [
    parsed.year,
    parsed.month,
    parsed.day,
    parsed.hour,
    parsed.minute,
    parsed.second,
  ];
  for (var index = 0; index < components.length; index++) {
    if (components[index] != int.parse(parts.group(index + 1)!)) {
      throw const FormatException('Invalid receipt date components');
    }
  }
  return parsed;
}

class CloudSupportSettings {
  CloudSupportSettings(this.directory, {this.diagnosticsOnly = false});
  final Directory directory;
  final bool diagnosticsOnly;
  static const _configurationFile = 'cloud-support-settings.json';
  // Dedicated clients never open a host's pending database, even if their
  // caller accidentally supplies the same directory.
  String get _pendingFile => diagnosticsOnly
      ? 'cloud-support-diagnostics-pending.json'
      : 'cloud-support-pending.json';
  String get _statusFile => diagnosticsOnly
      ? 'cloud-support-diagnostics-status.json'
      : 'cloud-support-status.json';
  static final _pendingLocks = <String, Future<void>>{};

  Future<CloudSupportConfiguration?> readConfiguration() async {
    final json = await _read(_configurationFile, 64 * 1024);
    return json == null ? null : CloudSupportConfiguration.fromJson(json);
  }

  Future<void> saveConfiguration(CloudSupportConfiguration configuration) =>
      _write(_configurationFile, configuration.toJson());

  Future<PendingSupportUpload?> readPending() async {
    final json = await _read(
      _pendingFile,
      PendingSupportUpload.maxBytes * 2 + 65536,
    );
    return json == null ? null : PendingSupportUpload.fromJson(json);
  }

  Future<void> savePending(PendingSupportUpload pending) => _withPendingLock(
    () async {
      if (await FileSystemEntity.type(
            _file(_pendingFile).path,
            followLinks: false,
          ) !=
          FileSystemEntityType.notFound) {
        throw const CenterException('توجد نسخة دعم معلّقة؛ لا يمكن استبدالها.');
      }
      await _write(_pendingFile, pending.toJson());
    },
  );

  Future<void> removePending(String uploadId) => _withPendingLock(() async {
    final pending = await readPending();
    if (pending == null) return;
    if (pending.uploadId != uploadId) {
      throw const CenterException('تغيّرت النسخة المعلّقة؛ لم تُحذف.');
    }
    await _file(_pendingFile).delete();
  });

  Future<void> _withPendingLock(Future<void> Function() operation) async {
    final lockPath = path.normalize(path.absolute(directory.path));
    final previous = _pendingLocks[lockPath] ?? Future<void>.value();
    final completed = Completer<void>();
    _pendingLocks[lockPath] = completed.future;
    await previous;
    try {
      await _lockedPendingMutation(operation);
    } finally {
      completed.complete();
      if (identical(_pendingLocks[lockPath], completed.future)) {
        _pendingLocks.remove(lockPath);
      }
    }
  }

  Future<void> _lockedPendingMutation(Future<void> Function() operation) async {
    await _verifyDirectory();
    await directory.create(recursive: true);
    final lockFile = _file('$_pendingFile.lock');
    final kind = await FileSystemEntity.type(lockFile.path, followLinks: false);
    if (kind != FileSystemEntityType.notFound &&
        kind != FileSystemEntityType.file) {
      throw const CenterException('ملف قفل نسخة الدعم غير صالح.');
    }
    final lock = await lockFile.open(mode: FileMode.append);
    try {
      // The OS lock also prevents a second app process replacing queued evidence.
      await lock.lock(FileLock.blockingExclusive);
      await operation();
    } finally {
      await lock.close();
    }
  }

  Future<Map<String, dynamic>?> readStatus() => _read(_statusFile, 64 * 1024);
  Future<void> saveStatus(Map<String, dynamic> status) =>
      _write(_statusFile, status);

  File _file(String name) => File(path.join(directory.path, name));

  Future<void> _verifyDirectory() async {
    final kind = await FileSystemEntity.type(
      directory.path,
      followLinks: false,
    );
    if (kind != FileSystemEntityType.notFound &&
        kind != FileSystemEntityType.directory) {
      throw const CenterException('مجلد إعدادات الدعم غير صالح.');
    }
  }

  Future<Map<String, dynamic>?> _read(String name, int limit) async {
    await _verifyDirectory();
    final file = _file(name);
    final kind = await FileSystemEntity.type(file.path, followLinks: false);
    if (kind == FileSystemEntityType.notFound) return null;
    if (kind != FileSystemEntityType.file || await file.length() > limit) {
      throw const CenterException(
        'ملف إعدادات الدعم غير صالح؛ لم تُستبدل محتوياته.',
      );
    }
    return jsonDecode(await file.readAsString()) as Map<String, dynamic>;
  }

  Future<void> _write(String name, Map<String, dynamic> json) async {
    await _verifyDirectory();
    await directory.create(recursive: true);
    final target = _file(name);
    final kind = await FileSystemEntity.type(target.path, followLinks: false);
    if (kind != FileSystemEntityType.notFound &&
        kind != FileSystemEntityType.file) {
      throw const CenterException('ملف إعدادات الدعم غير صالح؛ لم يُكتب.');
    }
    final staged = _file('$name.${const Uuid().v4()}.tmp');
    try {
      await staged.create(exclusive: true);
      await staged.writeAsString(jsonEncode(json), flush: true);
      await staged.rename(target.path);
    } finally {
      if (await staged.exists()) await staged.delete();
    }
  }
}
