import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart'
    show PlatformException, MissingPluginException;
import 'package:path/path.dart' as path;
import 'package:sqflite_common_ffi/sqflite_ffi.dart' show DatabaseException;
import 'package:uuid/uuid.dart';

import '../domain/models.dart';
import 'app_build_metadata.dart';

/// Local diagnostics deliberately exclude exception messages and application data.
class ProblemLog {
  ProblemLog(Directory directory) : _directory = directory.absolute;

  static ProblemLog? current;
  static const _maximumFileBytes = 1024 * 1024;
  static const _fileCount = 5;
  static const _maximumLineBytes = 16 * 1024;
  static const _operations = {
    'startup',
    'flutter_error',
    'platform_error',
    'zone_error',
    'store_command',
    'store_load',
    'store_save',
    'store_import',
    'store_export',
    'auth',
    'appearance',
    'documents',
    'printing',
    'ui_operation',
    'unknown_operation',
    'flutter.framework',
    'flutter.platform',
    'flutter.zone',
    'startup.initialize',
    'startup.open',
    'startup.log_directory',
    'startup.export',
    'database.open',
    'database.repair',
    'database.operation',
    'database.close',
    'auth.sign_in',
    'appearance.storage',
    'ui.notice',
    'ui.warning',
    'ui.auth_screen',
    'ui.attendance_workspace',
    'ui.student_editor_dialog',
    'ui.student_transfer_dialog',
    'ui.management_widgets',
    'ui.academics_page',
    'ui.closings_page',
    'ui.review_page',
    'ui.card_settings_page',
    'ui.reports_page',
    'ui.cards_page',
    'ui.students_page',
    'ui.corrections_page',
    'ui.sessions_page',
    'ui.backup_page',
    'cloud.upload',
    'cloud.snapshot',
    'cloud.settings',
    'cloud.initialize',
    'cloud.updates',
    'ui.cloud_settings',
    'card_settings',
    'card_payment',
    'debt_settle',
    'ui.student_debt_dialog',
    'card_receipt',
    'setup_admin',
    'staff_create',
    'catalog_save',
    'group_save',
    'month_save_groups',
    'student_save',
    'student_transfer',
    'student_suspend',
    'student_reactivate',
    'student_discount',
    'student_note',
    'session_save',
    'session_start',
    'sessions_create',
    'package_renew',
    'entry',
    'attendance_record',
    'session_close',
    'session_reopen',
    'session_cancel',
    'academic_activity_save',
    'academic_save',
    'entry_reverse',
    'payment_cancel',
    'attendance_cancel',
    'entry_correct',
    'absence_present',
    'payment_method_correct',
    'package_refund',
    'closing_reopen',
    'payment_check',
    'payment_uncheck',
    'payment_checks_clear',
    'payment_review_save',
    'session_finalize',
    'installation_admin',
    'backup.create',
    'backup.automatic',
    'backup.before_update',
    'backup.restore',
    'reports.export',
    'diagnostics.copy_path',
    'diagnostics.export',
    'lan.initialize',
    'lan.host_start',
    'lan.host_exit',
    'lan.connection',
    'lan.host_request',
    'lan.host_response',
    'lan.command',
    'lan.switch',
    'lan.saveCatalog',
    'lan.saveGroup',
    'lan.registerStudent',
    'lan.saveStudent',
    'lan.transferStudent',
    'lan.suspendStudent',
    'lan.reactivateStudent',
    'lan.saveSession',
    'lan.startSession',
    'lan.createGroupSessions',
    'lan.saveAcademicActivity',
    'lan.saveAcademic',
    'lan.saveCardSettings',
    'lan.collectStudentCard',
    'lan.settleDebt',
    'lan.saveMonthForGroups',
    'lan.receiveStudentCard',
    'lan.saveStaff',
    'lan.saveStudentDiscount',
    'lan.saveStudentNote',
    'lan.renewPackage',
    'lan.collectAndAttend',
    'lan.closeSession',
    'lan.reopenSession',
    'lan.cancelSession',
    'lan.reverseEntry',
    'lan.cancelPayment',
    'lan.cancelAttendance',
    'lan.correctEntry',
    'lan.markAbsentPresent',
    'lan.correctPaymentMethod',
    'lan.refundPackage',
    'lan.reopenFinancialClosing',
    'lan.checkPayment',
    'lan.uncheckPayment',
    'lan.clearPaymentChecks',
    'lan.savePaymentReview',
    'lan.finalizeSession',
    'lan.login',
    'lan.sign_in',
    'lan.refresh',
    'lan.logout',
    'ui.lan_settings_page',
  };
  static const _types = {
    'CenterException',
    'LanConnectionException',
    'LanAuthorizationException',
    'DatabaseException',
    'FileSystemException',
    'OSError',
    'SocketException',
    'HttpException',
    'TimeoutException',
    'FormatException',
    'StateError',
    'RangeError',
    'ArgumentError',
    'TypeError',
    'AssertionError',
    'UnsupportedError',
    'UnimplementedError',
    'NoSuchMethodError',
    'OtherError',
    'FlutterError',
    'PlatformException',
    'MissingPluginException',
  };
  static const _sourceFiles = {
    'cloud/cloud_support_controller.dart',
    'cloud/cloud_support_settings.dart',
    'cloud/app_update_controller.dart',
    'application/center_store_support.dart',
    'features/management/cloud_settings_page.dart',
    'main.dart',
    'application/admin_configuration.dart',
    'application/center_reports.dart',
    'application/center_store.dart',
    'application/center_store_debts.dart',
    'application/center_store_students.dart',
    'application/center_store_months.dart',
    'application/center_store_backups.dart',
    'application/center_store_lan.dart',
    'application/center_store_remote.dart',
    'lan/center_store_host_bridge.dart',
    'lan/lan_transport.dart',
    'lan/lan_discovery.dart',
    'lan/lan_controller.dart',
    'lan/lan_settings.dart',
    'lan/lan_host_process.dart',
    'features/management/lan_settings_page.dart',
    'application/installation_admin.dart',
    'application/session_finance.dart',
    'application/student_card_reports.dart',
    'data/center_state.dart',
    'domain/models.dart',
    'domain/discount_calculation.dart',
    'features/attendance/attendance_workspace.dart',
    'features/attendance/closed_session_dialog.dart',
    'features/attendance/entry_confirmation_dialog.dart',
    'features/attendance/student_editor_dialog.dart',
    'features/attendance/student_discount_dialog.dart',
    'features/attendance/student_debt_dialog.dart',
    'features/attendance/student_suspension_dialog.dart',
    'features/attendance/paid_amount_dialog.dart',
    'features/attendance/paid_amount_fields.dart',
    'features/attendance/student_history_panel.dart',
    'features/auth/auth_screen.dart',
    'features/cards/student_card_actions.dart',
    'features/management/academics_page.dart',
    'features/management/academic_quick_entry.dart',
    'features/management/backup_page.dart',
    'features/management/card_settings_page.dart',
    'features/management/cards_page.dart',
    'features/management/catalogs_page.dart',
    'features/management/closings_page.dart',
    'features/management/corrections_page.dart',
    'features/management/groups_page.dart',
    'features/management/management_widgets.dart',
    'features/management/management_workspace.dart',
    'features/management/reports_page.dart',
    'features/management/review_page.dart',
    'features/management/sessions_page.dart',
    'features/management/staff_page.dart',
    'features/management/students_page.dart',
    'shared/appearance.dart',
    'shared/app_build_metadata.dart',
    'shared/document_service.dart',
    'shared/formatters.dart',
    'shared/notice_dialog.dart',
    'shared/scrollable_dialog.dart',
    'shared/problem_log.dart',
    'shared/problem_reporting.dart',
    'shared/problem_log_health.dart',
    'shared/theme.dart',
  };
  static final _uuidPattern = RegExp(
    r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
  );
  static final _framePattern = RegExp(
    r'^#([0-9]{1,5})\s+.*\((?:package:massar_center/|.*[/\\]lib[/\\])([a-z_/\\]+\.dart):([0-9]{1,7}):([0-9]{1,7})\)$',
  );

  final Directory _directory;
  final String _sessionId = const Uuid().v4();
  final _seenErrors = Expando<DateTime>('problem-log-identities');
  Future<void> _tail = Future<void>.value();
  final _failure = ValueNotifier<String?>(null);
  bool _sessionStarted = false;

  String get directoryPath => _directory.path;
  String? get writeFailure => _failure.value;
  ValueListenable<String?> get failure => _failure;

  Future<void> startSession() => _enqueue(() async {
    if (_sessionStarted) return;
    await _writeSafely(_event('session', 'startup'));
    _sessionStarted = _failure.value == null;
  });

  /// Logging failure is observable through writeFailure, never a replacement error.
  Future<void> record(
    Object error,
    StackTrace stackTrace, {
    required String operation,
  }) {
    final reportedAt = DateTime.now().toUtc();
    return _enqueue(() async {
      try {
        final previous = _supportsIdentity(error) ? _seenErrors[error] : null;
        if (previous != null &&
            !reportedAt.isBefore(previous) &&
            reportedAt.difference(previous) < const Duration(seconds: 1)) {
          return;
        }
        final entry = _event(
          'error',
          _operations.contains(operation) ? operation : 'unknown_operation',
        );
        entry['time'] = reportedAt.toIso8601String();
        entry['errors'] = _errorChain(error);
        entry['frames'] = _frames(stackTrace);
        await _writeSafely(entry);
        if (_failure.value == null && _supportsIdentity(error)) {
          _seenErrors[error] = reportedAt;
        }
      } catch (_) {
        _failure.value = 'تعذر كتابة سجل المشاكل المحلي.';
      }
    });
  }

  Future<void> flush() => _tail;

  Future<String> exportTo(String destination) => _enqueue(() async {
    if (_failure.value != null) throw StateError(_failure.value!);
    final target = path.normalize(path.absolute(destination));
    if (target == directoryPath || path.isWithin(directoryPath, target)) {
      throw StateError('اختر مكانًا خارج مجلد سجل المشاكل للتصدير.');
    }
    if (path.extension(target).toLowerCase() != '.txt') {
      throw StateError('اختر ملفًا جديدًا بامتداد txt لتصدير سجل المشاكل.');
    }
    try {
      await _requireNewExportFile(target);
      final text = await _exportText();
      await _writeExport(target, text);
      return target;
    } on FileSystemException {
      throw StateError('تعذر تصدير سجل المشاكل. اختر مكانًا قابلًا للكتابة.');
    }
  });

  /// Sanitized diagnostics only; no application records, paths or credentials.
  Future<String> exportText() => _enqueue(_exportText);

  Future<String> _exportText() async {
    if (_failure.value != null) throw StateError(_failure.value!);
    final entries = await _savedEntries();
    final header = {
      'schema': 1,
      'kind': 'export',
      'version': AppBuildMetadata.version,
      'build': AppBuildMetadata.buildIdentifier,
      'role': AppBuildMetadata.role,
      'exportedAt': DateTime.now().toUtc().toIso8601String(),
      'privacy': 'types_codes_app_frames_only',
      'eventCount': entries.length,
    };
    return '${[header, ...entries].map(jsonEncode).join('\n')}\n';
  }

  Future<void> _requireNewExportFile(String destination) async {
    if (await FileSystemEntity.type(destination, followLinks: false) !=
        FileSystemEntityType.notFound) {
      throw StateError('اختر اسم ملف جديد؛ لن نستبدل ملفًا موجودًا.');
    }
  }

  Future<void> _writeExport(String destination, String text) async {
    final staging = await Directory(
      path.dirname(destination),
    ).createTemp('.massar-diagnostics-');
    try {
      final temporary = File(path.join(staging.path, 'export.txt'));
      await temporary.writeAsString(text, flush: true);
      await _requireNewExportFile(destination);
      // Rename replaces a last-minute symlink itself instead of following its target.
      await temporary.rename(destination);
    } finally {
      await staging.delete(recursive: true);
    }
  }

  Future<void> _discardOversizedFiles() async {
    for (var index = 0; index < _fileCount; index++) {
      final file = await _managedFile(index);
      if (await file.exists() && await file.length() > _maximumFileBytes) {
        await file.delete();
      }
    }
  }

  Future<T> _enqueue<T>(Future<T> Function() operation) {
    final completion = Completer<T>();
    _tail = _tail.then((_) async {
      try {
        completion.complete(await operation());
      } catch (error, stackTrace) {
        completion.completeError(error, stackTrace);
      }
    });
    return completion.future;
  }

  Map<String, Object?> _event(String kind, String operation) => {
    'schema': 1,
    'kind': kind,
    'id': const Uuid().v4(),
    'session': _sessionId,
    'time': DateTime.now().toUtc().toIso8601String(),
    'version': AppBuildMetadata.version,
    'build': AppBuildMetadata.buildIdentifier,
    'role': AppBuildMetadata.role,
    'platform': Platform.operatingSystem,
    'operation': operation,
  };

  Future<void> _writeSafely(Map<String, Object?> entry) async {
    try {
      await _directory.create(recursive: true);
      if (await FileSystemEntity.type(directoryPath, followLinks: false) !=
          FileSystemEntityType.directory) {
        throw const FileSystemException('Invalid log directory');
      }
      final line = '${jsonEncode(entry)}\n';
      if (utf8.encode(line).length > _maximumLineBytes) {
        throw const FormatException('Log entry too large');
      }
      await _discardOversizedFiles();
      final currentFile = await _managedFile(0);
      final currentBytes = await currentFile.exists()
          ? await currentFile.length()
          : 0;
      if (currentBytes + utf8.encode(line).length > _maximumFileBytes) {
        await _rotate();
      }
      await currentFile.writeAsString(line, mode: FileMode.append, flush: true);
      _failure.value = null;
    } catch (_) {
      _failure.value = 'تعذر كتابة سجل المشاكل المحلي.';
    }
  }

  Future<File> _managedFile(int index) async {
    final file = File(
      path.join(
        directoryPath,
        index == 0 ? 'problems.jsonl' : 'problems.$index.jsonl',
      ),
    );
    final type = await FileSystemEntity.type(file.path, followLinks: false);
    if (type != FileSystemEntityType.notFound &&
        type != FileSystemEntityType.file) {
      throw const FileSystemException('Invalid managed log file');
    }
    return file;
  }

  Future<void> _rotate() async {
    final oldest = await _managedFile(_fileCount - 1);
    if (await oldest.exists()) await oldest.delete();
    for (var index = _fileCount - 2; index >= 0; index--) {
      final source = await _managedFile(index);
      final destination = await _managedFile(index + 1);
      if (await source.exists()) await source.rename(destination.path);
    }
  }

  Future<List<Map<String, Object?>>> _savedEntries() async {
    final entries = <Map<String, Object?>>[];
    for (var index = _fileCount - 1; index >= 0; index--) {
      final file = await _managedFile(index);
      if (!await file.exists()) continue;
      if (await file.length() > _maximumFileBytes) continue;
      final lines = file
          .openRead()
          .transform(const Utf8Decoder(allowMalformed: true))
          .transform(const LineSplitter());
      await for (final line in lines) {
        if (line.length > _maximumLineBytes) continue;
        try {
          final entry = _sanitizedSavedEntry(jsonDecode(line));
          if (entry != null) entries.add(entry);
        } on FormatException {
          // Corrupt or edited records are not safe diagnostic material to export.
        }
      }
    }
    return entries;
  }

  static bool _supportsIdentity(Object error) =>
      error is! String && error is! num && error is! bool && error is! Record;

  static List<Map<String, Object?>> _errorChain(Object error) {
    final chain = <Map<String, Object?>>[];
    Object? current = error;
    while (current != null && chain.length < 4) {
      final code = switch (current) {
        CenterException() => current.diagnosticCode,
        DatabaseException() => current.getResultCode(),
        FileSystemException() => current.osError?.errorCode,
        SocketException() => current.osError?.errorCode,
        OSError() => current.errorCode,
        _ => null,
      };
      chain.add({
        'type': _errorType(current),
        if (_safeCode(code) != null) 'code': code,
      });
      current = current is CenterException ? current.cause : null;
    }
    return chain;
  }

  static String _errorType(Object error) => switch (error) {
    CenterException() => 'CenterException',
    FlutterError() => 'FlutterError',
    PlatformException() => 'PlatformException',
    MissingPluginException() => 'MissingPluginException',
    DatabaseException() => 'DatabaseException',
    FileSystemException() => 'FileSystemException',
    SocketException() => 'SocketException',
    HttpException() => 'HttpException',
    OSError() => 'OSError',
    TimeoutException() => 'TimeoutException',
    FormatException() => 'FormatException',
    StateError() => 'StateError',
    RangeError() => 'RangeError',
    ArgumentError() => 'ArgumentError',
    TypeError() => 'TypeError',
    AssertionError() => 'AssertionError',
    UnimplementedError() => 'UnimplementedError',
    UnsupportedError() => 'UnsupportedError',
    NoSuchMethodError() => 'NoSuchMethodError',
    _ => 'OtherError',
  };

  static int? _safeCode(Object? code) =>
      code is int && code >= 0 && code <= 0xffffff ? code : null;

  static List<Map<String, Object?>> _frames(StackTrace stackTrace) {
    final raw = stackTrace.toString();
    final bounded = raw.substring(0, raw.length.clamp(0, 32768));
    final frames = <Map<String, Object?>>[];
    for (final line in const LineSplitter().convert(bounded).take(64)) {
      final match = _framePattern.firstMatch(line.trim());
      final sourceFile = match?.group(2)?.replaceAll('\\', '/');
      if (match == null || !_sourceFiles.contains(sourceFile)) continue;
      frames.add({
        'frame': int.parse(match.group(1)!),
        'file': sourceFile,
        'line': int.parse(match.group(3)!),
        'column': int.parse(match.group(4)!),
      });
      if (frames.length == 16) break;
    }
    return frames;
  }

  static Map<String, Object?>? _sanitizedSavedEntry(Object? decoded) {
    if (decoded is! Map ||
        decoded['schema'] != 1 ||
        !['session', 'error'].contains(decoded['kind'])) {
      return null;
    }
    final id = decoded['id'],
        session = decoded['session'],
        time = decoded['time'];
    if (id is! String ||
        session is! String ||
        !_uuidPattern.hasMatch(id) ||
        !_uuidPattern.hasMatch(session)) {
      return null;
    }
    if (time is! String ||
        !RegExp(
          r'^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?Z$',
        ).hasMatch(time) ||
        DateTime.tryParse(time) == null) {
      return null;
    }
    if (!AppBuildMetadata.isValidVersion(decoded['version']) ||
        ![
          'windows',
          'macos',
          'linux',
          'android',
          'ios',
          'fuchsia',
        ].contains(decoded['platform'])) {
      return null;
    }
    final operation = decoded['operation'];
    final entry = <String, Object?>{
      'schema': 1,
      'kind': decoded['kind'],
      'id': id,
      'session': session,
      'time': time,
      'version': decoded['version'],
      if (AppBuildMetadata.isValidBuild(decoded['build']))
        'build': decoded['build'],
      if (['host', 'client'].contains(decoded['role'])) 'role': decoded['role'],
      'platform': decoded['platform'],
      'operation': _operations.contains(operation)
          ? operation
          : 'unknown_operation',
    };
    if (decoded['kind'] == 'error') {
      final errors = decoded['errors'];
      if (errors is! List || errors.isEmpty) return null;
      entry['errors'] = errors
          .take(4)
          .whereType<Map>()
          .map(
            (error) => <String, Object?>{
              'type': _types.contains(error['type'])
                  ? error['type']
                  : 'OtherError',
              if (_safeCode(error['code']) != null)
                'code': _safeCode(error['code']),
            },
          )
          .toList();
      final frames = decoded['frames'];
      entry['frames'] = frames is List
          ? frames
                .take(16)
                .whereType<Map>()
                .where(
                  (frame) =>
                      _sourceFiles.contains(frame['file']) &&
                      _safeCode(frame['frame']) != null &&
                      _safeCode(frame['line']) != null &&
                      _safeCode(frame['column']) != null,
                )
                .map(
                  (frame) => <String, Object?>{
                    'file': frame['file'],
                    'frame': frame['frame'],
                    'line': frame['line'],
                    'column': frame['column'],
                  },
                )
                .toList()
          : <Object>[];
    }
    return entry;
  }
}
