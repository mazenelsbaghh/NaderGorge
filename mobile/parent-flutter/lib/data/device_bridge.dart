import 'package:flutter/services.dart';
import 'models.dart';

class DeviceBridge {
  static const channel = MethodChannel('net.massaracademy.parent/device');
  Future<Json> legacyProfiles() async => Map<String, dynamic>.from(
    await channel.invokeMapMethod<String, dynamic>('legacyProfiles') ?? {},
  );
  Future<String?> deviceToken() => channel.invokeMethod<String>('deviceToken');
  Future<bool> requestNotifications() async =>
      await channel.invokeMethod<bool>('requestNotifications') ?? false;
  Future<void> openSettings() => channel.invokeMethod('openSettings');
  void listen(Future<void> Function() refresh) {
    channel.setMethodCallHandler((call) async {
      if (call.method == 'refresh' || call.method == 'tokenChanged') {
        await refresh();
      }
    });
  }
}
