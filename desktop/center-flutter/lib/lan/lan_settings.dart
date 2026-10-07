import 'dart:convert';
import 'dart:io';

import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/lan_transport.dart';
import 'package:path/path.dart' as path;
import 'package:uuid/uuid.dart';

enum LanMode { standalone, host, client }

class LanConfiguration {
  const LanConfiguration({
    this.mode = LanMode.standalone,
    required this.deviceId,
    required this.deviceName,
    this.endpoint,
    this.deviceToken,
  });

  final LanMode mode;
  final String deviceId;
  final String deviceName;
  final LanEndpoint? endpoint;
  final String? deviceToken;

  LanConfiguration copyWith({
    LanMode? mode,
    String? deviceName,
    LanEndpoint? endpoint,
    String? deviceToken,
    bool clearEndpoint = false,
    bool clearDeviceToken = false,
  }) => LanConfiguration(
    mode: mode ?? this.mode,
    deviceId: deviceId,
    deviceName: deviceName ?? this.deviceName,
    endpoint: clearEndpoint ? null : endpoint ?? this.endpoint,
    deviceToken: clearDeviceToken ? null : deviceToken ?? this.deviceToken,
  );

  Map<String, dynamic> toJson() => {
    'version': 1,
    'mode': mode.name,
    'deviceId': deviceId,
    'deviceName': deviceName,
    'endpoint': endpoint?.toJson(),
    'deviceToken': deviceToken,
  };

  factory LanConfiguration.fromJson(Map<String, dynamic> json) {
    if (json['version'] != 1) {
      throw const FormatException('Unsupported LAN settings');
    }
    final config = LanConfiguration(
      mode: LanMode.values.byName(json['mode'] as String),
      deviceId: json['deviceId'] as String,
      deviceName: json['deviceName'] as String,
      endpoint: json['endpoint'] == null
          ? null
          : LanEndpoint.fromJson(json['endpoint'] as Map<String, dynamic>),
      deviceToken: json['deviceToken'] as String?,
    );
    if (config.deviceId.isEmpty ||
        config.deviceName.isEmpty ||
        (config.mode == LanMode.client &&
            (config.endpoint == null ||
                config.deviceToken == null ||
                config.deviceToken!.isEmpty))) {
      throw const FormatException('Incomplete LAN settings');
    }
    return config;
  }
}

class LanSettings {
  LanSettings(this.directory);
  final Directory directory;
  Future<void> _pending = Future<void>.value();
  String get filePath => path.join(directory.path, 'lan-settings.json');

  Future<T> _serial<T>(Future<T> Function() task) {
    final operation = _pending.then((_) => task());
    _pending = operation.then<void>(
      (_) {},
      onError: (Object _, StackTrace _) {},
    );
    return operation;
  }

  Future<LanConfiguration> read() => _serial(() async {
    try {
      final file = File(filePath);
      if (await FileSystemEntity.type(filePath, followLinks: false) ==
          FileSystemEntityType.link) {
        throw const CenterException('ملف إعدادات الشبكة غير صالح.');
      }
      if (!await file.exists()) {
        final initial = LanConfiguration(
          deviceId: const Uuid().v4(),
          deviceName: Platform.localHostname,
        );
        await _write(initial);
        return initial;
      }
      if (await file.length() > 64 * 1024) {
        throw const FormatException('Oversized settings');
      }
      return LanConfiguration.fromJson(
        jsonDecode(await file.readAsString()) as Map<String, dynamic>,
      );
    } on CenterException {
      rethrow;
    } on Object catch (error, stackTrace) {
      throw CenterException(
        'تعذر قراءة إعدادات الربط المحلي. لا تغيّر وضع الجهاز قبل مراجعتها.',
        cause: error,
        stackTrace: stackTrace,
      );
    }
  });

  Future<void> save(LanConfiguration config) => _serial(() async {
    try {
      LanConfiguration.fromJson(config.toJson());
      await _write(config);
    } on Object catch (error, stackTrace) {
      throw CenterException(
        'تعذر حفظ إعدادات الربط المحلي.',
        cause: error,
        stackTrace: stackTrace,
      );
    }
  });

  Future<void> _write(LanConfiguration config) async {
    await directory.create(recursive: true);
    if (await FileSystemEntity.type(filePath, followLinks: false) ==
        FileSystemEntityType.link) {
      throw const CenterException('ملف إعدادات الشبكة غير صالح.');
    }
    final staged = File('$filePath.${const Uuid().v4()}.tmp');
    try {
      await staged.writeAsString(jsonEncode(config.toJson()), flush: true);
      await staged.rename(filePath);
    } finally {
      if (await staged.exists()) await staged.delete();
    }
  }
}
