import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../data/models.dart';
import '../data/parent_api.dart';
import '../data/profile_store.dart';
import '../data/device_bridge.dart';

class ParentController extends ChangeNotifier {
  final ParentApi api;
  final ProfileStore store;
  final DeviceBridge bridge;
  ParentController({
    required this.api,
    required this.store,
    required this.bridge,
  });
  List<LinkedStudent> profiles = [];
  LinkedStudent? active;
  StudentDetails? details;
  List<AcademicRow> notifications = [];
  AcademicRow? config;
  ParentFailure? failure;
  String? notificationError;
  DateTime? lastUpdated;
  bool initialized = false, refreshing = false;
  ThemeMode themeMode = ThemeMode.system;
  int _generation = 0;
  bool _disposed = false;

  Future<void> initialize() async {
    failure = null;
    try {
      final saved = await store.load();
      profiles = (saved['profiles'] as List)
          .map((p) => LinkedStudent.fromJson(p as Json))
          .toList();
      active =
          profiles.where((p) => p.studentId == saved['activeId']).firstOrNull ??
          profiles.firstOrNull;
      final theme = await store.theme();
      themeMode =
          ThemeMode.values.where((m) => m.name == theme).firstOrNull ??
          ThemeMode.system;
      await refresh();
      if (!_disposed) await registerPush();
    } on PlatformException {
      failure = const ParentFailure(
        'تعذر فتح التخزين الآمن. أعد المحاولة بعد فتح قفل الجهاز.',
      );
    } on FormatException {
      failure = const ParentFailure(
        'تعذر قراءة الطلاب المحفوظين. لم يتم حذف بياناتك.',
      );
    } finally {
      initialized = true;
      notifyListeners();
    }
  }

  Future<void> refresh() async {
    final generation = ++_generation;
    final student = active;
    refreshing = true;
    failure = null;
    notifyListeners();
    try {
      final nextConfig = AcademicRow(await api.request('app-config') as Json);
      if (generation != _generation) return;
      config = nextConfig;
      if (nextConfig.flag('updateRequired') || student == null) return;
      final nextDetails = await api.details(student);
      if (generation != _generation) return;
      details = nextDetails;
      lastUpdated = DateTime.now();
      await refreshNotifications(student, generation);
    } on ParentFailure catch (error) {
      if (generation != _generation) return;
      failure = error;
      if (error.unauthorized) {
        details = null;
        notifications = [];
        lastUpdated = null;
      }
    } finally {
      if (generation == _generation) {
        refreshing = false;
        notifyListeners();
      }
    }
  }

  Future<void> refreshNotifications(
    LinkedStudent student,
    int generation,
  ) async {
    try {
      final nextNotifications = await api.notifications(student);
      if (generation != _generation) return;
      notifications = nextNotifications;
      notificationError = null;
    } on ParentFailure catch (error) {
      if (generation == _generation) notificationError = error.message;
    }
  }

  Future<void> select(LinkedStudent student) async {
    await store.save(profiles, student.studentId);
    active = student;
    details = null;
    notifications = [];
    notificationError = null;
    lastUpdated = null;
    await refresh();
  }

  Future<void> confirm(LinkedStudent student) async {
    final next = [
      ...profiles.where((p) => p.studentId != student.studentId),
      student,
    ];
    await store.save(next, student.studentId);
    profiles = next;
    await select(student);
    await registerPush();
  }

  Future<void> remove(LinkedStudent student) async {
    final next = profiles
        .where((p) => p.studentId != student.studentId)
        .toList();
    final nextActive = active?.studentId == student.studentId
        ? next.firstOrNull
        : active;
    await store.save(next, nextActive?.studentId);
    profiles = next;
    active = nextActive;
    details = null;
    notifications = [];
    notificationError = null;
    lastUpdated = null;
    await refresh();
  }

  Future<void> markRead(AcademicRow notification) async {
    final student = active;
    if (student == null) return;
    await api.markRead(student, notification.text('id'));
    if (active?.studentId != student.studentId) return;
    await refreshNotifications(student, _generation);
    notifyListeners();
  }

  Future<void> registerPush() async {
    try {
      final token = await bridge.deviceToken();
      if (token == null || token.isEmpty) return;
      for (final student in List<LinkedStudent>.of(profiles)) {
        await api.register(student, token);
      }
    } on ParentFailure catch (error) {
      notificationError = error.message;
    } on PlatformException {
      notificationError = 'تعذر تفعيل إشعارات الجهاز. حاول من الإعدادات.';
    }
    notifyListeners();
  }

  Future<void> setTheme(ThemeMode theme) async {
    await store.saveTheme(theme.name);
    themeMode = theme;
    notifyListeners();
  }

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _generation++;
    api.dispose();
    super.dispose();
  }
}
