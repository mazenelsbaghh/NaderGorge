import 'dart:async';
import 'dart:io';
import 'dart:convert';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:webview_windows/webview_windows.dart';

import '../../exams/exam_room_controller.dart';
import '../../domain/models.dart';
import '../../shared/problem_reporting.dart';
import '../../shared/theme.dart';
import '../../shared/workspace_draft_guard.dart' show confirmDiscardDraft;
import 'exam_download_bridge.dart';
import 'exam_admin_theme.dart';
import 'exam_admin_navigation.dart';

class ExamAdminView extends StatefulWidget {
  const ExamAdminView({
    super.key,
    required this.controller,
    this.whatsAppBatchId,
    this.navigation,
  });
  final ExamRoomController controller;
  final String? whatsAppBatchId;
  final ExamAdminNavigation? navigation;
  @override
  State<ExamAdminView> createState() => _ExamAdminViewState();
}

class _ExamAdminViewState extends State<ExamAdminView> {
  static Future<void>? _environment;
  final _web = WebviewController();
  bool _ready = false, _opening = false;
  String? _error;
  StreamSubscription<String>? _urls;
  StreamSubscription<WebErrorStatus>? _errors;
  StreamSubscription<dynamic>? _messages;
  StreamSubscription<LoadingState>? _loading;
  ThemeData? _theme;
  bool _started = false;
  String? _themeScript, _themeScriptId;
  Future<void> _themeUpdates = Future.value();
  late final ExamDownloadBridge _downloads;
  final _navigationNonce = List.generate(
    24,
    (_) => Random.secure().nextInt(256),
  ).map((byte) => byte.toRadixString(16).padLeft(2, '0')).join();
  String? _navigationDocument;
  int? _navigationGeneration;
  String? _navigationActor;
  int? _observedGeneration;
  int _sentNavigation = 0, _leaveSequence = 0;
  Completer<bool>? _pendingLeave;
  int? _pendingLeaveId;

  @override
  void initState() {
    super.initState();
    _downloads = ExamDownloadBridge(
      controller: widget.controller,
      reply: _web.postWebMessage,
    );
    _bindNavigation();
    widget.controller.addListener(_controllerChanged);
  }

  void _controllerChanged() {
    final generation = widget.controller.authorizationGeneration;
    if (_observedGeneration == generation) return;
    _observedGeneration = generation;
    if (!widget.controller.canAdmin ||
        _navigationActor != widget.controller.store.currentUser?.id) {
      _pendingLeave?.complete(false);
      _pendingLeave = null;
    }
    if (mounted) setState(() {});
  }

  void _bindNavigation() {
    widget.navigation?.addListener(_navigationChanged);
    widget.navigation?.bindLeaveGuard(this, _confirmLeave);
  }

  @override
  void didUpdateWidget(covariant ExamAdminView oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!identical(oldWidget.navigation, widget.navigation)) {
      oldWidget.navigation?.removeListener(_navigationChanged);
      oldWidget.navigation?.unbindLeaveGuard(this);
      _sentNavigation = 0;
      _bindNavigation();
      _navigationChanged();
    }
  }

  bool get _navigationAuthorized =>
      mounted &&
      examAdminNavigationSessionValid(
        widget.controller,
        generation: _navigationGeneration,
        actor: _navigationActor,
      );

  void _navigationChanged() {
    final navigation = widget.navigation;
    if (navigation == null ||
        _navigationDocument == null ||
        !_navigationAuthorized ||
        navigation.requestSerial <= _sentNavigation) {
      return;
    }
    _sentNavigation = navigation.requestSerial;
    unawaited(
      _postNavigation({
        'action': 'navigate',
        'requestId': _sentNavigation,
        'destination': navigation.requestedDestination,
      }),
    );
  }

  Future<void> _postNavigation(Map<String, dynamic> command) async {
    try {
      await _web.postWebMessage(
        jsonEncode({
          'kind': 'massar-center-navigation',
          'nonce': _navigationNonce,
          'documentId': _navigationDocument,
          ...command,
        }),
      );
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'exam.admin_navigation');
      _pendingLeave?.complete(false);
      _pendingLeave = null;
    }
  }

  Future<bool> _confirmLeave() async {
    if (!_ready) return !_opening;
    // Leaving a revoked employee session never renews its engine access.
    if (!widget.controller.canAdmin ||
        _navigationActor != widget.controller.store.currentUser?.id) {
      return true;
    }
    if (_navigationDocument == null && _error != null) {
      return confirmDiscardDraft(context, dirty: true);
    }
    if (_navigationDocument == null || _pendingLeave != null) return false;
    final pending = Completer<bool>();
    _pendingLeave = pending;
    final id = ++_leaveSequence;
    _pendingLeaveId = id;
    await _postNavigation({'action': 'leave', 'requestId': id});
    try {
      return await pending.future.timeout(
        const Duration(minutes: 2),
        onTimeout: () => false,
      );
    } finally {
      if (identical(_pendingLeave, pending)) {
        _pendingLeave = null;
        _pendingLeaveId = null;
      }
    }
  }

  void _handleWebMessage(dynamic message) {
    unawaited(_downloads.handle(message));
    if (!mounted || widget.navigation == null) return;
    final parsed = examAdminNavigationMessage(
      message,
      nonce: _navigationNonce,
      documentId: _navigationDocument,
    );
    if (parsed == null) return;
    if (parsed['action'] == 'leave-result') {
      if (_navigationActor == widget.controller.store.currentUser?.id &&
          widget.controller.canAdmin &&
          parsed['requestId'] == _pendingLeaveId) {
        _pendingLeave?.complete(parsed['allowed'] == true);
        _pendingLeave = null;
      }
      return;
    }
    if (!_navigationAuthorized) return;
    if (parsed['action'] == 'ready') {
      if (_navigationDocument != parsed['documentId']) {
        _pendingLeave?.complete(false);
        _pendingLeave = null;
        _navigationDocument = parsed['documentId'] as String;
        _sentNavigation = 0;
      }
      widget.navigation?.accept(parsed['destination'] as String);
      _navigationChanged();
    } else if (parsed['action'] == 'state') {
      widget.navigation?.accept(parsed['destination'] as String);
    }
  }

  Future<void> _reopen() async {
    if (_opening) return;
    if (await (widget.navigation?.confirmLeave() ?? Future.value(true)) &&
        mounted) {
      await _open();
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final next = Theme.of(context);
    final changed = _theme != next;
    _theme = next;
    if (!_started) {
      _started = true;
      unawaited(_open());
    } else if (changed && _web.value.isInitialized) {
      unawaited(_synchronizeTheme());
    }
  }

  Future<void> _synchronizeTheme() {
    _themeUpdates = _themeUpdates.then((_) => _writeTheme());
    return _themeUpdates;
  }

  Future<void> _writeTheme() async {
    if (!mounted || !_web.value.isInitialized || _theme == null) return;
    final script = examAdminThemeScript(_theme!);
    try {
      if (_themeScript != script) {
        final nextId = await _web.addScriptToExecuteOnDocumentCreated(script);
        if (nextId != null) {
          final previous = _themeScriptId;
          _themeScriptId = nextId;
          _themeScript = script;
          if (previous != null) {
            await _web.removeScriptToExecuteOnDocumentCreated(previous);
          }
        }
      }
      if (mounted) await _web.executeScript(script);
    } on PlatformException catch (error, stack) {
      reportProblem(error, stack, operation: 'exam.admin_theme');
    }
  }

  Future<void> _open() async {
    if (_opening) return;
    _opening = true;
    if (mounted) setState(() => _error = null);
    try {
      if (!Platform.isWindows) {
        throw const CenterException('عرض الامتحانات المدمج مخصص لويندوز.');
      }
      if (!_web.value.isInitialized) {
        if (await WebviewController.getWebViewVersion() == null) {
          throw const CenterException(
            'يلزم تثبيت Microsoft Edge WebView2 لعرض الامتحان داخل البرنامج. رابط تنزيله الرسمي موجود في ملف Windows-exam-setup.txt بجوار البرنامج؛ ثبّته ثم افتح الشاشة مجددًا.',
          );
        }
        _environment ??= () async {
          final support = await getApplicationSupportDirectory();
          await WebviewController.initializeEnvironment(
            userDataPath: p.join(support.path, 'massar-center', 'exam-webview'),
          );
        }();
        await _environment;
        await _web.initialize();
        if (!mounted) {
          await _web.dispose();
          return;
        }
        await _web.addScriptToExecuteOnDocumentCreated(_downloads.script);
        await _web.addScriptToExecuteOnDocumentCreated(
          examAdminNavigationScript(_navigationNonce),
        );
        _loading = _web.loadingState.listen((state) {
          if (state == LoadingState.loading) {
            _navigationDocument = null;
            _pendingLeave?.complete(false);
            _pendingLeave = null;
          }
          if (state == LoadingState.navigationCompleted) {
            unawaited(_synchronizeTheme());
          }
        });
        _messages = _web.webMessage.listen(_handleWebMessage);
        await _web.setPopupWindowPolicy(WebviewPopupWindowPolicy.deny);
        _urls = _web.url.listen((url) {
          final uri = Uri.tryParse(url);
          if (url != 'about:blank' &&
              (uri == null ||
                  uri.scheme != 'http' ||
                  uri.host != '127.0.0.1' ||
                  uri.userInfo.isNotEmpty ||
                  uri.port != 8766)) {
            unawaited(_web.stop());
            if (mounted) {
              setState(() => _error = 'تعذر فتح عنوان خارج إدارة الامتحان.');
            }
          }
        });
        _errors = _web.onLoadError.listen((_) {
          if (mounted) {
            setState(
              () => _error =
                  'تعذر عرض شاشة الامتحان. الإجابات تظل محفوظة؛ أعد فتح الشاشة.',
            );
          }
        });
      }
      final address = await widget.controller.openAdmin();
      if (!mounted) return;
      _navigationGeneration = widget.controller.authorizationGeneration;
      _navigationActor = widget.controller.store.currentUser?.id;
      await _synchronizeTheme();
      final target = examAdminEmbeddedAddress(
        address,
        batchId: widget.whatsAppBatchId,
        brightness: _theme!.brightness,
        nativeShell: widget.navigation != null,
      );
      await _web.loadUrl(target.toString());
      if (mounted) setState(() => _ready = true);
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'exam.admin_view');
      if (mounted) {
        setState(
          () => _error = error is CenterException
              ? error.message
              : 'تعذر فتح شاشة الامتحان. أعد المحاولة أو راجع سجل المشاكل.',
        );
      }
    } finally {
      _opening = false;
    }
  }

  @override
  void dispose() {
    widget.controller.removeListener(_controllerChanged);
    widget.navigation?.removeListener(_navigationChanged);
    widget.navigation?.unbindLeaveGuard(this);
    _pendingLeave?.complete(false);
    _pendingLeave = null;
    unawaited(_urls?.cancel());
    unawaited(_errors?.cancel());
    unawaited(_messages?.cancel());
    unawaited(_loading?.cancel());
    _downloads.dispose();
    if (_web.value.isInitialized) unawaited(_web.dispose());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_error != null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.info_outline, size: 32),
              const SizedBox(height: 12),
              Text(_error!, textAlign: TextAlign.center),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: _opening ? null : _reopen,
                child: const Text('إعادة فتح الإدارة'),
              ),
            ],
          ),
        ),
      );
    }
    if (!_ready) {
      return const Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(),
            SizedBox(height: 12),
            Text('جارٍ فتح إدارة الامتحانات…'),
          ],
        ),
      );
    }
    final webview = Webview(
      _web,
      permissionRequested: (_, _, _) => WebviewPermissionDecision.deny,
    );
    if (widget.navigation == null || _navigationAuthorized) return webview;
    final colors = MassarPalette.of(context);
    return Column(
      children: [
        Material(
          color: colors.warningSurface,
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    'تغيّر اتصال الموظف. أعد فتح الإدارة لمتابعة العمل؛ إجابات الطلاب محفوظة.',
                    style: TextStyle(color: colors.warning),
                  ),
                ),
                const SizedBox(width: 12),
                OutlinedButton(
                  onPressed: _opening ? null : _reopen,
                  child: const Text('إعادة فتح الإدارة'),
                ),
              ],
            ),
          ),
        ),
        Expanded(
          child: _navigationActor == widget.controller.store.currentUser?.id
              ? webview
              : const Center(child: Text('افتح الإدارة بجلسة الموظف الحالية.')),
        ),
      ],
    );
  }
}
