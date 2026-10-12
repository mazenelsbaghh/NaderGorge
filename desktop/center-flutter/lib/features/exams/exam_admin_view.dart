import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:webview_windows/webview_windows.dart';

import '../../exams/exam_room_controller.dart';
import '../../domain/models.dart';
import '../../shared/problem_reporting.dart';
import 'exam_download_bridge.dart';

class ExamAdminView extends StatefulWidget {
  const ExamAdminView({super.key, required this.controller});
  final ExamRoomController controller;
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
  late final ExamDownloadBridge _downloads;

  @override
  void initState() {
    super.initState();
    _downloads = ExamDownloadBridge(
      controller: widget.controller,
      reply: _web.postWebMessage,
    );
    _open();
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
        _messages = _web.webMessage.listen(
          (message) => unawaited(_downloads.handle(message)),
        );
        await _web.setPopupWindowPolicy(WebviewPopupWindowPolicy.deny);
        _urls = _web.url.listen((url) {
          final uri = Uri.tryParse(url);
          if (url != 'about:blank' &&
              (uri == null ||
                  uri.scheme != 'http' ||
                  uri.host != '127.0.0.1' ||
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
      await _web.loadUrl(address);
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
    unawaited(_urls?.cancel());
    unawaited(_errors?.cancel());
    unawaited(_messages?.cancel());
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
                onPressed: _opening ? null : _open,
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
    return Webview(
      _web,
      permissionRequested: (_, _, _) => WebviewPermissionDecision.deny,
    );
  }
}
