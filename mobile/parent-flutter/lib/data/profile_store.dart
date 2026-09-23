import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'device_bridge.dart';
import 'models.dart';

class ProfileStore {
  final FlutterSecureStorage storage;
  final DeviceBridge bridge;
  ProfileStore({FlutterSecureStorage? storage, DeviceBridge? bridge})
    : storage = storage ?? const FlutterSecureStorage(),
      bridge = bridge ?? DeviceBridge();
  static const key = 'massar.parent.profiles.v1';

  Future<Json> load() async {
    final saved = await storage.read(key: key);
    if (saved != null) return _validate(jsonDecode(saved));
    final legacy = await bridge.legacyProfiles();
    final profiles = legacy['profiles'] as String?;
    final migrated = _validate({
      'profiles': profiles == null ? [] : jsonDecode(profiles),
      'activeId': legacy['activeId'],
    });
    // Keep native originals untouched; the new envelope is the migration marker.
    await storage.write(key: key, value: jsonEncode(migrated));
    return migrated;
  }

  Json _validate(dynamic value) {
    if (value is! Json ||
        value['profiles'] is! List ||
        (value['activeId'] != null && value['activeId'] is! String)) {
      throw const FormatException('Invalid saved profiles');
    }
    for (final row in value['profiles'] as List) {
      if (row is! Json) throw const FormatException('Invalid saved profile');
      LinkedStudent.fromJson(row);
    }
    return value;
  }

  Future<void> save(List<LinkedStudent> profiles, String? activeId) =>
      storage.write(
        key: key,
        value: jsonEncode({
          'profiles': profiles.map((p) => p.toJson()).toList(),
          'activeId': activeId,
        }),
      );
  Future<String?> theme() => storage.read(key: 'massar.parent.theme');
  Future<void> saveTheme(String mode) =>
      storage.write(key: 'massar.parent.theme', value: mode);
}
