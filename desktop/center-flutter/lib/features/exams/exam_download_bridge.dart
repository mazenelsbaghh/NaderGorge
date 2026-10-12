import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:file_selector/file_selector.dart';
import 'package:path/path.dart' as p;

import '../../domain/models.dart';
import '../../exams/exam_downloads.dart';
import '../../exams/exam_engine.dart';
import '../../exams/exam_export_saver.dart';
import '../../exams/exam_room_controller.dart';
import '../../shared/problem_reporting.dart';

class ExamDownloadBridge {
  ExamDownloadBridge({required this.controller, required this.reply});
  final ExamRoomController controller;
  final Future<void> Function(String) reply;
  final String _nonce = List.generate(
    24,
    (_) => Random.secure().nextInt(256),
  ).map((byte) => byte.toRadixString(16).padLeft(2, '0')).join();
  bool _busy = false, _disposed = false;

  String get script =>
      '''
(() => {
  if (location.origin !== 'http://127.0.0.1:8766' || window.top !== window) return;
  const pending = new Map(); let sequence = 0;
  const documentId = Array.from(crypto.getRandomValues(new Uint8Array(16)), byte => byte.toString(16).padStart(2, '0')).join('');
  window.chrome.webview.addEventListener('message', event => {
    const message = event.data;
    if (message?.kind !== 'massar-export-result') return;
    const request = pending.get(message.id); if (!request) return;
    pending.delete(message.id); clearTimeout(request.timer);
    if (message.error) request.reject(new Error(message.error));
    else request.resolve({saved: message.saved === true});
  });
  window.pywebview = {api: {download_exam_file: (path, filename, mimeType) => new Promise((resolve, reject) => {
    const id = documentId + '-' + (++sequence);
    const timer = setTimeout(() => { pending.delete(id); reject(new Error('انتهت مهلة حفظ الملف؛ راجع مجلد الحفظ قبل إعادة المحاولة.')); }, 600000);
    pending.set(id, {resolve, reject, timer});
    window.chrome.webview.postMessage({kind: 'massar-export', nonce: '$_nonce', id, path, filename, mimeType});
  })}};
})();
''';

  Future<void> handle(dynamic message) async {
    if (_disposed ||
        message is! Map ||
        message['kind'] != 'massar-export' ||
        message['nonce'] != _nonce ||
        message['id'] is! String ||
        !RegExp(
          r'^[a-f0-9]{32}-[0-9]{1,8}$',
        ).hasMatch(message['id'] as String)) {
      return;
    }
    final response = <String, dynamic>{
      'kind': 'massar-export-result',
      'id': message['id'],
      'saved': false,
    };
    if (_busy) {
      response['error'] = 'انتظر انتهاء حفظ الملف الحالي.';
    } else {
      _busy = true;
      try {
        response['saved'] = await _save(message);
      } catch (failure, stack) {
        reportProblem(failure, stack, operation: 'exam.export');
        response['error'] = failure is CenterException
            ? failure.message
            : 'تعذر حفظ الملف. اختَر مجلدًا متاحًا واسمًا جديدًا.';
      } finally {
        _busy = false;
      }
    }
    if (!_disposed) {
      try {
        await reply(jsonEncode(response));
      } catch (failure, stack) {
        reportProblem(failure, stack, operation: 'exam.export_reply');
      }
    }
  }

  Future<bool> _save(Map message) async {
    final engine = controller.engine;
    final generation = controller.authorizationGeneration;
    bool authorized() =>
        !_disposed &&
        controller.canSync &&
        generation == controller.authorizationGeneration;
    if (!authorized() || engine is! WindowsExamEngine) {
      throw const CenterException(
        'افتح إدارة الامتحان وسجّل دخول الموظف قبل الحفظ.',
      );
    }
    final route = message['path'],
        mime = message['mimeType'],
        name = message['filename'];
    if (route is! String ||
        mime is! String ||
        name is! String ||
        name.length > 240) {
      throw const CenterException('طلب حفظ الملف غير صالح.');
    }
    final source = await (engine as ExamDownloadSource).receiveExport(
      route,
      mime,
    );
    try {
      if (!authorized()) {
        throw const CenterException('تغيّرت جلسة الموظف؛ لم يُحفظ الملف.');
      }
      final extension = p.extension(source.path).substring(1);
      final selection = await getSaveLocation(
        suggestedName: p
            .basename(name)
            .replaceAll(RegExp(r'[\\/:*?"<>|]'), '-'),
        acceptedTypeGroups: [
          XTypeGroup(label: 'ملف الامتحان', extensions: [extension]),
        ],
      );
      if (selection == null) return false;
      await ExamExportSaver(
        protectedDirectories: [
          Directory(p.dirname(controller.store.databasePath)),
          if (controller.store.automaticBackupDirectory
              case final String folder)
            Directory(folder),
          engine.directory,
          Directory(engine.executableDirectory),
        ],
        isAuthorized: authorized,
      ).save(source, selection.path);
      return true;
    } finally {
      await source.parent.delete(recursive: true);
    }
  }

  void dispose() => _disposed = true;
}
