import 'problem_reporting.dart';
import 'dart:convert';
import 'dart:io';
import 'package:flutter/material.dart';
import 'notice_dialog.dart';

/// Device-local appearance; independent of student data and financial backups.
class AppearanceSettings extends ChangeNotifier {
  AppearanceSettings(this.file);
  final File file;
  ThemeMode _mode = ThemeMode.dark;
  bool _busy = true;
  bool _disposed = false;
  String? _error;
  ThemeMode get mode => _mode;
  bool get busy => _busy;
  String? get error => _error;

  Future<void> load() async {
    try {
      if (await file.exists()) {
        final value = jsonDecode(await file.readAsString());
        if (value is! Map || !['dark', 'light'].contains(value['mode'])) {
          throw const FormatException('تفضيل المظهر غير صالح');
        }
        _mode = value['mode'] == 'light' ? ThemeMode.light : ThemeMode.dark;
      }
      _error = null;
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'appearance.storage');
      _error = 'تعذر قراءة المظهر المحفوظ. يمكنك اختيار المظهر وحفظه مجددًا.';
    } finally {
      _busy = false;
      if (!_disposed) notifyListeners();
    }
  }

  Future<void> toggle() async {
    if (_busy) return;
    _busy = true;
    _error = null;
    notifyListeners();
    final next = _mode == ThemeMode.dark ? ThemeMode.light : ThemeMode.dark;
    final temporary = File('${file.path}.tmp');
    try {
      await file.parent.create(recursive: true);
      await temporary.writeAsString(
        jsonEncode({'mode': next.name}),
        flush: true,
      );
      await temporary.rename(file.path);
      _mode = next;
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'appearance.storage');
      _error = 'تعذر حفظ المظهر على الجهاز. حاول مرة أخرى.';
      rethrow;
    } finally {
      _busy = false;
      if (!_disposed) notifyListeners();
    }
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}

class AppearanceScope extends InheritedNotifier<AppearanceSettings> {
  const AppearanceScope({
    super.key,
    required AppearanceSettings settings,
    required super.child,
  }) : super(notifier: settings);
  static AppearanceSettings? maybeOf(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<AppearanceScope>()?.notifier;
}

class AppearanceToggle extends StatelessWidget {
  const AppearanceToggle({super.key, this.onDarkSurface = false});
  final bool onDarkSurface;

  @override
  Widget build(BuildContext context) {
    final settings = AppearanceScope.maybeOf(context);
    if (settings == null) return const SizedBox.shrink();
    final dark = settings.mode == ThemeMode.dark;
    return Focus(
      canRequestFocus: false,
      descendantsAreFocusable: false,
      child: IconButton(
        key: const Key('appearance-toggle'),
        tooltip: settings.error ?? (dark ? 'المظهر الفاتح' : 'المظهر الداكن'),
        color: onDarkSurface ? const Color(0xFFE6EDF5) : null,
        icon: Icon(dark ? Icons.light_mode_outlined : Icons.dark_mode_outlined),
        onPressed: settings.busy
            ? null
            : () async {
                try {
                  await settings.toggle();
                } catch (error, stackTrace) {
                  reportProblem(
                    error,
                    stackTrace,
                    operation: 'appearance.storage',
                  );
                  if (context.mounted) {
                    await showMassarNotice(
                      context,
                      settings.error ?? 'تعذر حفظ المظهر',
                      kind: NoticeKind.error,
                    );
                  }
                }
              },
      ),
    );
  }
}
