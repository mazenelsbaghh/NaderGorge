import 'shared/scrollable_dialog.dart';
import 'dart:async';
import 'dart:io';
import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'application/admin_configuration.dart';
import 'application/center_store.dart';
import 'cloud/cloud_support_controller.dart';
import 'cloud/app_update_controller.dart';
import 'features/attendance/attendance_workspace.dart';
import 'features/auth/auth_screen.dart';
import 'features/management/management_workspace.dart';
import 'features/management/lan_settings_page.dart';
import 'lan/lan_controller.dart';
import 'shared/appearance.dart';
import 'shared/notice_dialog.dart';
import 'shared/problem_log.dart';
import 'shared/problem_log_health.dart';
import 'shared/problem_reporting.dart';
import 'shared/theme.dart';

void main() {
  runZonedGuarded(
    () async {
      WidgetsFlutterBinding.ensureInitialized();
      installProblemHandlers();
      await _initializeProblemLog();
      runApp(const CenterBootstrap());
    },
    (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'flutter.zone');
    },
  );
}

Future<void> _initializeProblemLog() async {
  Object primaryError = const FileSystemException(
    'Primary log folder unavailable',
  );
  StackTrace primaryStack = StackTrace.current;
  try {
    final support = await getApplicationSupportDirectory();
    final log = ProblemLog(
      Directory(p.join(support.path, 'massar-center', 'logs')),
    );
    ProblemLog.current = log;
    await log.startSession();
    if (log.writeFailure == null) return;
  } catch (error, stackTrace) {
    primaryError = error;
    primaryStack = stackTrace;
  }
  final fallback = ProblemLog(
    Directory(p.join(Directory.systemTemp.path, 'massar-center-problem-logs')),
  );
  ProblemLog.current = fallback;
  await fallback.startSession();
  await fallback.record(
    primaryError,
    primaryStack,
    operation: 'startup.log_directory',
  );
}

Future<CenterStore> _openWithDiagnostics({required bool clientOnly}) async {
  try {
    return await openInstalledCenter(clientOnly: clientOnly);
  } catch (error, stackTrace) {
    reportProblem(error, stackTrace, operation: 'startup.open');
    rethrow;
  }
}

class CenterBootstrap extends StatefulWidget {
  const CenterBootstrap({
    super.key,
    this.clientOnly = const bool.fromEnvironment(
      'MASSAR_CLIENT_ONLY',
      defaultValue: false,
    ),
  });
  final bool clientOnly;
  @override
  State<CenterBootstrap> createState() => _CenterBootstrapState();
}

class _CenterBootstrapState extends State<CenterBootstrap> {
  late Future<CenterStore> _opening = _openWithDiagnostics(
    clientOnly: widget.clientOnly,
  );
  bool _exporting = false;

  Future<void> _exportProblems(BuildContext context) async {
    if (_exporting || ProblemLog.current == null) return;
    setState(() => _exporting = true);
    try {
      final location = await getSaveLocation(
        suggestedName: 'massar-startup-problems.txt',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'سجل المشاكل', extensions: ['txt']),
        ],
      );
      if (location == null) return;
      await ProblemLog.current!.exportTo(location.path);
      if (context.mounted) {
        await showMassarNotice(
          context,
          'تم تصدير سجل المشاكل.',
          kind: NoticeKind.success,
        );
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'startup.export');
      if (context.mounted) {
        await showMassarNotice(
          context,
          'تعذر تصدير سجل المشاكل. اختر مكانًا آخر قابلًا للحفظ.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _exporting = false);
    }
  }

  @override
  Widget build(BuildContext context) => FutureBuilder<CenterStore>(
    future: _opening,
    builder: (context, snapshot) {
      if (snapshot.hasData) {
        return CenterApp(
          store: snapshot.requireData,
          clientOnly: widget.clientOnly,
        );
      }
      return MaterialApp(
        theme: MassarTheme.dark,
        home: ProblemLogHealth(
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: Scaffold(
              body: Center(
                child: Padding(
                  padding: const EdgeInsets.all(32),
                  child: snapshot.hasError
                      ? MassarScrollView(
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(Icons.error_outline, size: 40),
                              const SizedBox(height: 16),
                              Text(
                                widget.clientOnly
                                    ? 'تعذر تهيئة ربط الجهاز الفرعي. لم تتغير بيانات الجهاز الرئيسي.'
                                    : 'تعذر فتح البيانات المحلية. البيانات لم تُحذف.',
                              ),
                              const SizedBox(height: 8),
                              const Text(
                                'سجل المشاكل يحتوي تفاصيل العطل للمراجعة.',
                              ),
                              if (ProblemLog.current != null) ...[
                                const SizedBox(height: 8),
                                SelectableText(
                                  ProblemLog.current!.directoryPath,
                                  textDirection: TextDirection.ltr,
                                ),
                                Builder(
                                  builder: (dialogContext) =>
                                      OutlinedButton.icon(
                                        onPressed: _exporting
                                            ? null
                                            : () => _exportProblems(
                                                dialogContext,
                                              ),
                                        icon: const Icon(
                                          Icons.file_download_outlined,
                                        ),
                                        label: const Text('تصدير سجل المشاكل'),
                                      ),
                                ),
                              ],
                              const SizedBox(height: 16),
                              OutlinedButton(
                                onPressed: () => setState(() {
                                  _opening = _openWithDiagnostics(
                                    clientOnly: widget.clientOnly,
                                  );
                                }),
                                child: const Text('إعادة المحاولة'),
                              ),
                            ],
                          ),
                        )
                      : const CircularProgressIndicator(),
                ),
              ),
            ),
          ),
        ),
      );
    },
  );
}

class CenterApp extends StatefulWidget {
  const CenterApp({super.key, required this.store, this.clientOnly = false});
  final CenterStore store;
  final bool clientOnly;
  @override
  State<CenterApp> createState() => _CenterAppState();
}

class _CenterAppState extends State<CenterApp> {
  bool _focusMode = false;
  String? _initialSessionId;
  AttendanceWorkspaceContext _attendanceContext = AttendanceWorkspaceContext();
  String? _attendanceActorId;
  late final AppearanceSettings _appearance;
  late final LanController _lan;
  late final CloudSupportController _cloud;
  late final AppUpdateController _updates;
  CenterStore? _previousStore;
  LanConnectionStatus? _lastConnectionStatus;
  final _navigator = GlobalKey<NavigatorState>();

  @override
  void initState() {
    super.initState();
    _appearance = AppearanceSettings(
      File('${File(widget.store.databasePath).parent.path}/appearance.json'),
    );
    _appearance.load();
    _lan = LanController(
      localStore: widget.store,
      clientOnly: widget.clientOnly || widget.store.isClientWorkspace,
    )..addListener(_lanChanged);
    final dataDirectory = File(widget.store.databasePath).parent;
    _cloud = CloudSupportController(
      directory: Directory(p.join(dataDirectory.path, 'support')),
      clientOnly: widget.clientOnly || widget.store.isClientWorkspace,
      snapshot: () {
        if (!identical(_lan.activeStore, widget.store)) {
          throw const CenterException(
            'ارفع بيانات السنتر من الجهاز الرئيسي المتصل.',
          );
        }
        return widget.store.captureSupportSnapshot(automatic: true);
      },
      diagnostics: () async {
        final log = ProblemLog.current;
        if (log == null) {
          throw const CenterException('سجل المشاكل غير متاح الآن.');
        }
        return log.exportText();
      },
    )..addListener(widget.store.notifySupportChanged);
    widget.store.supportUploadQueue = _cloud.queueUpload;
    widget.store.supportStatusReader = () => _cloud.publicStatus;
    _updates = AppUpdateController(
      directory: Directory(p.join(dataDirectory.path, 'updates')),
      configuration: () async => _cloud.configuration,
    )..addListener(_updateChanged);
    unawaited(_initializeSupport());
    unawaited(
      _lan.initialize().catchError((Object error, StackTrace stack) {
        reportProblem(error, stack, operation: 'lan.initialize');
      }),
    );
  }

  Future<void> _initializeSupport() async {
    try {
      await _cloud.initialize();
      _cloud.startAutomaticUploads();
      if (mounted) await _updates.initialize();
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'cloud.initialize');
    }
  }

  String? _announcedUpdate;
  void _updateChanged() {
    if (!mounted ||
        _updates.status != AppUpdateStatus.downloaded ||
        _updates.downloadedPath == null ||
        _announcedUpdate == _updates.downloadedPath) {
      return;
    }
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final context = _navigator.currentContext;
      if (!mounted ||
          context == null ||
          _updates.status != AppUpdateStatus.downloaded ||
          _announcedUpdate == _updates.downloadedPath) {
        return;
      }
      _announcedUpdate = _updates.downloadedPath;
      unawaited(
        showDialog<void>(
          context: context,
          builder: (context) => ScrollableMassarDialog(
            title: Text('تحديث ${_updates.availableVersion} جاهز'),
            content: const Text(
              'اتحمّل التحديث تلقائيًا. اقفل البرنامج على الجهازين، وفك النسخة الجديدة للهوست ثم الساكند. بياناتك الحالية تفضل محفوظة.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('لاحقًا'),
              ),
              FilledButton(
                onPressed: () async {
                  await Clipboard.setData(
                    ClipboardData(text: _updates.downloadedPath!),
                  );
                  if (context.mounted) Navigator.pop(context);
                },
                child: const Text('نسخ مكان ملف التحديث'),
              ),
            ],
          ),
        ),
      );
    });
  }

  void _lanChanged() {
    if (!identical(_previousStore, _lan.activeStore)) {
      _previousStore = _lan.activeStore;
      _focusMode = false;
      _initialSessionId = null;
      _attendanceContext = AttendanceWorkspaceContext();
      _attendanceActorId = null;
    }
    final before = _lastConnectionStatus;
    _lastConnectionStatus = _lan.status;
    if (_lan.status == LanConnectionStatus.disconnected &&
        (before == LanConnectionStatus.connected ||
            before == LanConnectionStatus.hosting)) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final context = _navigator.currentContext;
        if (!mounted ||
            context == null ||
            _lan.status != LanConnectionStatus.disconnected ||
            hasPendingMassarNotice(context)) {
          return;
        }
        unawaited(
          showMassarNotice(
            context,
            _lan.isHost
                ? 'توقفت خدمة ربط الأجهزة. بياناتك موجودة على هذا الجهاز؛ افتح إعدادات الربط لتشغيلها.'
                : 'انقطع الاتصال بالجهاز الرئيسي. التسجيل متوقف مؤقتًا، والبرنامج سيحاول الاتصال تلقائيًا.',
            title: 'حالة الربط',
            kind: NoticeKind.warning,
          ),
        );
      });
    }
  }

  void _openLan(BuildContext context) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => Scaffold(
          appBar: AppBar(title: const Text('ربط الأجهزة')),
          body: LanSettingsPage(controller: _lan),
        ),
      ),
    );
  }

  @override
  void dispose() {
    _appearance.dispose();
    widget.store.supportUploadQueue = null;
    widget.store.supportStatusReader = null;
    _cloud.removeListener(widget.store.notifySupportChanged);
    unawaited(_cloud.close().whenComplete(_cloud.dispose));
    _updates.removeListener(_updateChanged);
    unawaited(_updates.close().whenComplete(_updates.dispose));
    _lan.removeListener(_lanChanged);
    unawaited(_lan.close().whenComplete(_lan.dispose));
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: Listenable.merge([_appearance, _lan]),
    builder: (context, _) => AppearanceScope(
      settings: _appearance,
      child: MaterialApp(
        navigatorKey: _navigator,
        title: 'مسار | نادر جورج',
        debugShowCheckedModeBanner: false,
        theme: MassarTheme.light,
        darkTheme: MassarTheme.dark,
        themeMode: _appearance.mode,
        locale: const Locale('ar', 'EG'),
        supportedLocales: const [Locale('ar', 'EG')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        home: ProblemLogHealth(
          child: AnimatedBuilder(
            animation: _lan.activeStore,
            builder: (context, _) {
              final store = _lan.activeStore;
              if (_lan.isClientOnly && !store.isRemote) {
                return Scaffold(
                  body: LanSettingsPage(
                    key: const Key('client-first-pairing'),
                    controller: _lan,
                  ),
                );
              }
              Widget withConnection(Widget child) => Column(
                children: [
                  if (store.isRemote)
                    Material(
                      color: store.remoteConnected
                          ? const Color(0xFF076D72)
                          : const Color(0xFF9B3D19),
                      child: SafeArea(
                        bottom: false,
                        child: Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 16,
                            vertical: 4,
                          ),
                          child: Row(
                            children: [
                              Icon(
                                store.remoteConnected
                                    ? Icons.lan
                                    : Icons.wifi_off,
                                color: Colors.white,
                                size: 18,
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Text(
                                  store.remoteConnected
                                      ? 'متصل بالجهاز الرئيسي • نفس البيانات على الجهازين'
                                      : 'الاتصال بالرئيسي متوقف • التسجيل متوقف حتى رجوع الاتصال',
                                  style: const TextStyle(color: Colors.white),
                                ),
                              ),
                              TextButton(
                                onPressed: () => _openLan(context),
                                child: const Text(
                                  'الربط',
                                  style: TextStyle(color: Colors.white),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  Expanded(child: child),
                ],
              );
              if (_attendanceActorId != store.currentUser?.id) {
                _attendanceActorId = store.currentUser?.id;
                _attendanceContext = AttendanceWorkspaceContext();
              }
              if (store.currentUser == null) {
                return withConnection(
                  AuthScreen(
                    key: ValueKey(store),
                    store: store,
                    onOpenLan: () => _openLan(context),
                  ),
                );
              }
              if (_focusMode && store.canCollect) {
                return withConnection(
                  AttendanceWorkspace(
                    key: ValueKey(store),
                    store: store,
                    initialSessionId: _initialSessionId,
                    workspaceContext: _attendanceContext,
                    onExit: () => setState(() {
                      _initialSessionId = null;
                      _focusMode = false;
                    }),
                  ),
                );
              }
              return withConnection(
                Scaffold(
                  body: ManagementWorkspace(
                    key: ValueKey(store),
                    store: store,
                    lanController: _lan,
                    cloudController: _cloud,
                    updateController: _updates,
                    onOpenAttendance: () => setState(() {
                      _initialSessionId = null;
                      _focusMode = true;
                    }),
                    onOpenSession: (sessionId) => setState(() {
                      _initialSessionId = sessionId;
                      _focusMode = true;
                    }),
                  ),
                ),
              );
            },
          ),
        ),
      ),
    ),
  );
}
