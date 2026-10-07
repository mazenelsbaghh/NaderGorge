import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:crypto/crypto.dart';
import 'package:uuid/uuid.dart';

import '../domain/models.dart';
import '../shared/app_build_metadata.dart';
import '../shared/problem_log.dart';
import 'cloud_support_settings.dart';

/// A queued snapshot stays immutable until the server acknowledges its own ID.
class CloudSupportController extends ChangeNotifier {
  CloudSupportController({
    required Future<Map<String, dynamic>> Function() snapshot,
    required Future<String> Function() diagnostics,
    required Directory directory,
    required this.clientOnly,
    HttpClient Function()? httpClientFactory,
  }) : _snapshot = snapshot,
       _diagnostics = diagnostics,
       _httpClientFactory = httpClientFactory ?? HttpClient.new,
       _settings = CloudSupportSettings(directory, diagnosticsOnly: clientOnly);

  final Future<Map<String, dynamic>> Function() _snapshot;
  final Future<String> Function() _diagnostics;
  final CloudSupportSettings _settings;
  final HttpClient Function() _httpClientFactory;
  final bool clientOnly;
  CloudSupportConfiguration? _configuration;
  PendingSupportUpload? _pending;
  SupportUploadReceipt? _receipt;
  Future<void>? _initialization, _queueOperation, _operation;
  Timer? _retryTimer, _automaticTimer;
  String? _lastFingerprint, _pendingFingerprint;
  HttpClient? _httpClient;
  bool _busy = false, _closed = false, _storageBlocked = false;
  int _attempts = 0;
  int? _lastHttpStatus;
  String? _lastError;
  DateTime? _retryAt;

  CloudSupportConfiguration? get configuration => _configuration;
  bool get configured => _configuration != null;
  Uri? get origin => _configuration?.origin;
  String? get centerId => _configuration?.centerId;
  bool get busy => _busy;
  bool get pending => _pending != null;
  DateTime? get lastSuccess => _receipt?.receivedAt;
  String? get lastError => _lastError;

  Map<String, dynamic> get publicStatus => {
    'configured': configured,
    'busy': busy,
    'pending': pending,
    'kind': clientOnly ? 'diagnostics' : 'database',
    'storageBlocked': _storageBlocked,
    'origin': origin?.toString(),
    'centerId': centerId,
    'pendingCreatedAt': _pending?.createdAt.toIso8601String(),
    'lastSuccess': lastSuccess?.toIso8601String(),
    'receipt': _receipt?.toJson(),
    'lastError': lastError,
    'httpStatus': _lastHttpStatus,
    'retryAt': _retryAt?.toIso8601String(),
  };

  Future<void> initialize() => _initialization ??= _initialize();

  Future<void> _initialize() async {
    _ensureOpen();
    try {
      _configuration = await _settings.readConfiguration();
      _restoreStatus(await _settings.readStatus());
      _pending = await _settings.readPending();
      _validatePendingRole();
      if (_pending != null && _pending!.uploadId == _receipt?.uploadId) {
        await _settings.removePending(_pending!.uploadId);
        _pending = null;
      }
      _scheduleRetry();
    } catch (error, stack) {
      _log(error, stack, 'cloud.initialize');
      _storageBlocked = true;
      _lastError =
          'تعذر قراءة ملفات ربط الدعم. الملفات محفوظة ولم تُستبدل؛ راجعها قبل إعادة المحاولة.';
    }
    _publish();
  }

  void _validatePendingRole() {
    if (_pending == null) return;
    final envelope = jsonDecode(_pending!.body) as Map<String, dynamic>;
    final expectedKind = clientOnly ? 'diagnostics' : 'database';
    if (envelope['kind'] != expectedKind) {
      throw const FormatException('Queued support role differs from this app');
    }
  }

  void _restoreStatus(Map<String, dynamic>? status) {
    if (status == null) return;
    if (status['version'] != 1) {
      throw const FormatException('Unsupported support status');
    }
    _lastFingerprint = status['fingerprint'] as String?;
    _receipt = status['receipt'] == null
        ? null
        : SupportUploadReceipt.fromJson(
            status['receipt'] as Map<String, dynamic>,
          );
    _lastError = status['lastError'] as String?;
    _lastHttpStatus = status['httpStatus'] as int?;
    _attempts = status['attempts'] as int? ?? 0;
    _retryAt = status['retryAt'] == null
        ? null
        : DateTime.parse(status['retryAt'] as String);
    if (_attempts < 0 ||
        _attempts > 1000000 ||
        (_lastError?.length ?? 0) > 1000 ||
        (_retryAt != null && !_retryAt!.isUtc) ||
        (_lastHttpStatus != null &&
            (_lastHttpStatus! < 100 || _lastHttpStatus! > 599))) {
      throw const FormatException('Invalid support status');
    }
  }

  Future<void> configure(Uri origin, String centerId, String token) async {
    await initialize();
    _ensureReadyForConfiguration();
    final candidate = CloudSupportConfiguration(
      origin: origin,
      centerId: centerId,
      deviceToken: token,
    );
    _validatePendingTarget(candidate);
    _busy = true;
    _publish();
    var configurationSaved = false;
    try {
      await _settings.saveConfiguration(candidate);
      configurationSaved = true;
      if (_configuration?.origin != candidate.origin ||
          _configuration?.centerId != candidate.centerId) {
        _lastFingerprint = null;
        _receipt = null;
      }
      _configuration = candidate;
      _lastError = null;
      _lastHttpStatus = null;
      _attempts = 0;
      _retryAt = null;
      await _saveStatus();
    } catch (error, stack) {
      _log(error, stack, 'cloud.settings');
      _lastError = configurationSaved
          ? 'حُفظ ربط الدعم، لكن تعذر حفظ حالة العملية؛ راجع التخزين المحلي.'
          : 'تعذر حفظ ربط الدعم. لم يتغيّر الربط المستخدم.';
      throw CenterException(_lastError!);
    } finally {
      _busy = false;
      _scheduleRetry(immediate: true);
      _publish();
    }
  }

  void _ensureReadyForConfiguration() {
    _ensureOpen();
    if (_busy || _storageBlocked) {
      throw const CenterException(
        'انتظر العملية الحالية أو راجع ملفات إعدادات الدعم أولًا.',
      );
    }
  }

  void _validatePendingTarget(CloudSupportConfiguration candidate) {
    if (_pending != null &&
        (_pending!.origin != candidate.origin ||
            _pending!.centerId != candidate.centerId)) {
      throw const CenterException(
        'ارفع النسخة المعلّقة إلى خادمها والسنتر الأصلي قبل تغيير الربط.',
      );
    }
  }

  /// Persists the request before scheduling networking, so LAN callers need not
  /// keep their command open while the Internet connection is unavailable.
  Future<void> queueUpload() async {
    await _ensureQueued();
    _scheduleRetry(immediate: true);
  }

  Future<void> _ensureQueued({bool automatic = false}) {
    if (_queueOperation != null) return _queueOperation!;
    final queued = _prepareUpload(
      automatic: automatic,
    ).whenComplete(() => _queueOperation = null);
    _queueOperation = queued;
    return queued;
  }

  Future<void> _prepareUpload({bool automatic = false}) async {
    await initialize();
    _ensureUploadReady();
    if (_pending != null) return;
    if (_busy) {
      throw const CenterException('انتظر انتهاء حفظ إعدادات الدعم.');
    }
    _busy = true;
    _publish();
    try {
      _pending = await _capture(skipUnchanged: automatic);
      _retryAt = null;
    } catch (error, stack) {
      _log(error, stack, 'cloud.snapshot');
      final message = error is _UploadFailure
          ? error.message
          : 'تعذر تجهيز أو حفظ نسخة الدعم؛ لم تُرسل نسخة ناقصة. راجع سجل المشاكل.';
      if (!_closed) await _recordFailure(message);
      throw CenterException(message);
    } finally {
      _busy = false;
      _publish();
    }
  }

  void _ensureUploadReady() {
    _ensureOpen();
    if (_storageBlocked) {
      throw const CenterException(
        'راجع ملفات الدعم المحلية قبل إنشاء نسخة جديدة.',
      );
    }
    if (!configured) throw const CenterException('اضبط ربط الدعم قبل الرفع.');
  }

  Future<void> syncNow({bool automatic = false}) {
    if (_operation != null) return _operation!;
    final sending = _sync(
      automatic: automatic,
    ).whenComplete(() => _operation = null);
    _operation = sending;
    return sending;
  }

  Future<void> _sync({bool automatic = false}) async {
    await _ensureQueued(automatic: automatic);
    if (_pending == null) return;
    _ensureUploadReady();
    _retryTimer?.cancel();
    _busy = true;
    _publish();
    try {
      final receipt = await _send(_pending!);
      if (_closed) return;
      await _acknowledge(receipt);
    } catch (error, stack) {
      if (!_closed) {
        _log(error, stack, 'cloud.upload');
        final failure = _publicUploadFailure(error);
        await _recordFailure(failure.message, failure.httpStatus);
        throw CenterException(failure.message);
      }
    } finally {
      _httpClient?.close(force: true);
      _httpClient = null;
      _busy = false;
      _scheduleRetry();
      _publish();
    }
  }

  Future<void> _acknowledge(SupportUploadReceipt receipt) async {
    _lastFingerprint =
        _pendingFingerprint ??
        await compute(_fingerprintSupportBody, _pending!.body);
    _ensureOpen();
    _receipt = receipt;
    _lastError = null;
    _lastHttpStatus = null;
    _attempts = 0;
    _retryAt = null;
    // Persist acknowledgment first: a crash before queue deletion can recover
    // locally without recapturing or inventing a second upload.
    await _saveStatus();
    await _settings.removePending(receipt.uploadId);
    _pending = null;
    _pendingFingerprint = null;
  }

  Future<PendingSupportUpload?> _capture({bool skipUnchanged = false}) async {
    final configuration = _configuration!;
    final snapshot = clientOnly ? null : await _snapshot();
    final diagnostics = await _diagnostics();
    _ensureOpen();
    final createdAt = DateTime.now().toUtc();
    final uploadId = const Uuid().v4();
    final prepared = await compute(_prepareSupportUpload, (
      envelope: _envelope(configuration, snapshot, diagnostics, (
        uploadId: uploadId,
        createdAt: createdAt,
      )),
      previousFingerprint: skipUnchanged ? _lastFingerprint : null,
    ));
    _ensureOpen();
    final body = prepared.body;
    if (body == null) return null;
    final pending = PendingSupportUpload(
      uploadId: uploadId,
      origin: configuration.origin,
      centerId: configuration.centerId,
      createdAt: createdAt,
      body: body,
    );
    await _settings.savePending(pending);
    _pendingFingerprint = prepared.fingerprint;
    return pending;
  }

  Map<String, dynamic> _envelope(
    CloudSupportConfiguration configuration,
    Map<String, dynamic>? snapshot,
    String diagnostics,
    ({String uploadId, DateTime createdAt}) identity,
  ) => {
    'format': 'massar-support-upload-v1',
    'kind': clientOnly ? 'diagnostics' : 'database',
    'uploadId': identity.uploadId,
    'centerId': configuration.centerId,
    'createdAt': identity.createdAt.toIso8601String(),
    'app': {
      'version': AppBuildMetadata.version,
      'build': AppBuildMetadata.buildIdentifier,
      'role': clientOnly ? 'client' : 'host',
      'os': Platform.operatingSystem,
    },
    'data': snapshot,
    'diagnostics': diagnostics,
  };

  Future<SupportUploadReceipt> _send(PendingSupportUpload pending) async {
    final configuration = _configuration!;
    _validatePendingTarget(configuration);
    final client = _httpClientFactory()
      ..connectionTimeout = const Duration(seconds: 10);
    _httpClient = client;
    return _sendRequest(
      client,
      pending,
      configuration.deviceToken,
    ).timeout(const Duration(minutes: 2));
  }

  Future<SupportUploadReceipt> _sendRequest(
    HttpClient client,
    PendingSupportUpload pending,
    String token,
  ) async {
    final request = await client.postUrl(pending.origin.resolve('/v1/uploads'));
    request.followRedirects = false;
    request.headers.set(
      HttpHeaders.userAgentHeader,
      'Massar-Center/${AppBuildMetadata.version}',
    );
    request.maxRedirects = 0;
    request.headers.contentType = ContentType.json;
    request.headers.set(HttpHeaders.authorizationHeader, 'Bearer $token');
    final bytes = utf8.encode(pending.body);
    request.contentLength = bytes.length;
    request.add(bytes);
    final response = await request.close();
    if (response.statusCode != 200 && response.statusCode != 201) {
      throw _UploadFailure(
        _httpError(response.statusCode),
        response.statusCode,
      );
    }
    return _readReceipt(response, pending.uploadId);
  }

  Future<SupportUploadReceipt> _readReceipt(
    HttpClientResponse response,
    String uploadId,
  ) async {
    final receiptBytes = <int>[];
    await for (final chunk in response) {
      if (receiptBytes.length + chunk.length > 64 * 1024) {
        throw const _UploadFailure(
          'تأكيد الخادم أكبر من الحد المسموح؛ النسخة المعلّقة لم تُحذف.',
        );
      }
      receiptBytes.addAll(chunk);
    }
    try {
      final receipt = SupportUploadReceipt.fromJson(
        jsonDecode(utf8.decode(receiptBytes)) as Map<String, dynamic>,
      );
      if (receipt.uploadId != uploadId) {
        throw const FormatException('Wrong receipt upload');
      }
      return receipt;
    } on FormatException {
      throw const _UploadFailure(
        'تأكيد الخادم لا يطابق النسخة المرسلة؛ النسخة المعلّقة محفوظة.',
      );
    } on TypeError {
      throw const _UploadFailure(
        'تأكيد الخادم غير مكتمل؛ النسخة المعلّقة محفوظة.',
      );
    }
  }

  _UploadFailure _publicUploadFailure(Object error) => switch (error) {
    _UploadFailure() => error,
    TimeoutException() => const _UploadFailure(
      'الاتصال استغرق وقتًا طويلًا. النسخة المعلّقة محفوظة للمحاولة التالية.',
    ),
    SocketException() => const _UploadFailure(
      'لا يوجد اتصال بالخادم الآن. النسخة المعلّقة محفوظة وستُعاد المحاولة.',
    ),
    HandshakeException() => const _UploadFailure(
      'لم يكتمل الاتصال الآمن بالخادم. لم يُستخدم اتصال غير مشفّر.',
    ),
    _ => const _UploadFailure(
      'تعذر تأكيد أو حفظ رفع الدعم. النسخة المعلّقة محفوظة؛ راجع سجل المشاكل.',
    ),
  };

  String _httpError(int status) => switch (status) {
    401 ||
    403 => 'الخادم رفض صلاحية الربط. راجع رمز الجهاز؛ النسخة المعلّقة محفوظة.',
    409 => 'الخادم رفض تطابق النسخة. راجع الدعم قبل تغيير النسخة المعلّقة.',
    413 => 'الخادم رفض حجم النسخة. النسخة المعلّقة محفوظة ولم تُختصر.',
    >= 300 && < 400 =>
      'الخادم حاول تحويل الاتصال لعنوان آخر. لم تتبعه نسخة الدعم.',
    _ =>
      'تعذر تأكيد رفع الدعم من الخادم (HTTP $status). النسخة المعلّقة محفوظة.',
  };

  Future<void> _recordFailure(String message, [int? status]) async {
    _lastError = message;
    _lastHttpStatus = status;
    _attempts = min(_attempts + 1, 1000000);
    _retryAt = DateTime.now().toUtc().add(_retryDelay);
    try {
      await _saveStatus();
    } catch (error, stack) {
      _log(error, stack, 'cloud.settings');
      _lastError =
          'تعذر حفظ حالة رفع الدعم. النسخة المعلّقة لم تُحذف؛ راجع التخزين المحلي.';
    }
  }

  Future<void> _saveStatus() => _settings.saveStatus({
    'version': 1,
    'receipt': _receipt?.toJson(),
    'fingerprint': _lastFingerprint,
    'lastError': _lastError,
    'httpStatus': _lastHttpStatus,
    'attempts': _attempts,
    'retryAt': _retryAt?.toIso8601String(),
  });

  Duration get _retryDelay =>
      Duration(seconds: min(60, 15 * (1 << min(_attempts, 7))));

  void _scheduleRetry({bool immediate = false}) {
    _retryTimer?.cancel();
    if (_closed || _storageBlocked || !configured || _pending == null) return;
    final remaining =
        _retryAt?.difference(DateTime.now().toUtc()) ?? _retryDelay;
    final delay = immediate || remaining.isNegative ? Duration.zero : remaining;
    _retryTimer = Timer(delay, _retry);
  }

  void _retry() {
    if (_closed) return;
    if (_busy) {
      _retryTimer = Timer(const Duration(seconds: 1), _retry);
      return;
    }
    unawaited(
      syncNow().catchError((Object error, StackTrace stack) {
        // The failed attempt is reported and retained by syncNow; timer errors
        // must not escape into Flutter's event loop.
        if (error is! CenterException) _log(error, stack, 'cloud.upload');
      }),
    );
  }

  /// Start once per application lifetime. Pending requests retain their identity;
  /// newer changes are captured after acknowledgment on the next tick.
  void startAutomaticUploads({Duration interval = const Duration(minutes: 1)}) {
    if (_closed || _automaticTimer != null) return;
    _automaticTimer = Timer.periodic(interval, (_) => _automaticUpload());
    _automaticUpload();
  }

  void _automaticUpload() {
    if (_closed ||
        !configured ||
        _storageBlocked ||
        _busy ||
        _operation != null ||
        _queueOperation != null) {
      return;
    }
    if (_pending != null &&
        _retryAt != null &&
        _retryAt!.isAfter(DateTime.now().toUtc())) {
      return;
    }
    unawaited(
      syncNow(automatic: true).catchError((Object error, StackTrace stack) {
        if (error is! CenterException) _log(error, stack, 'cloud.upload');
      }),
    );
  }

  void _log(Object error, StackTrace stack, String operation) {
    final log = ProblemLog.current;
    if (log != null) unawaited(log.record(error, stack, operation: operation));
  }

  void _ensureOpen() {
    if (_closed) throw const CenterException('ربط الدعم متوقف على هذا الجهاز.');
  }

  void _publish() {
    if (!_closed) notifyListeners();
  }

  Future<void> close() async {
    _stop();
    try {
      await Future.wait([?_queueOperation, ?_operation]);
    } on CenterException {
      // Operation failures already have public status and sanitized diagnostics;
      // stopping the controller must not report the same failure twice.
    }
  }

  void _stop() {
    _closed = true;
    _retryTimer?.cancel();
    _automaticTimer?.cancel();
    _httpClient?.close(force: true);
  }

  @override
  void dispose() {
    _stop();
    super.dispose();
  }
}

// Work with captured values only, without the live controller or store.
// Unchanged captures need no second envelope serialization.
({String? body, String fingerprint}) _prepareSupportUpload(
  ({Map<String, dynamic> envelope, String? previousFingerprint}) capture,
) {
  final envelope = capture.envelope;
  if (utf8.encode(envelope['diagnostics'] as String).length > 6 * 1024 * 1024) {
    throw const _UploadFailure(
      'سجل المشاكل أكبر من الحد المسموح. لم تُحذف أو تُختصر سجلاتك تلقائيًا.',
    );
  }
  final fingerprint = _fingerprintSupportEnvelope(envelope);
  if (fingerprint == capture.previousFingerprint) {
    return (body: null, fingerprint: fingerprint);
  }
  final body = jsonEncode(envelope);
  if (utf8.encode(body).length > PendingSupportUpload.maxBytes) {
    throw const _UploadFailure(
      'نسخة الدعم أكبر من الحد المسموح. لم تُنشأ نسخة ناقصة أو تُرسل.',
    );
  }
  return (body: body, fingerprint: fingerprint);
}

String _fingerprintSupportBody(String body) =>
    _fingerprintSupportEnvelope(jsonDecode(body) as Map<String, dynamic>);

String _fingerprintSupportEnvelope(Map<String, dynamic> envelope) => sha256
    .convert(
      utf8.encode(
        jsonEncode({
          'data': (envelope['data'] as Map?)?['data'],
          'commands': (envelope['data'] as Map?)?['lanCommandReceipts'],
          'diagnostics': _diagnosticContent(envelope['diagnostics'] as String),
          'app': envelope['app'],
        }),
      ),
    )
    .toString();

List<String> _diagnosticContent(String text) =>
    const LineSplitter().convert(text).where((line) {
      try {
        final event = jsonDecode(line);
        return event is! Map || event['kind'] != 'export';
      } on FormatException {
        return true;
      }
    }).toList();

class _UploadFailure implements Exception {
  const _UploadFailure(this.message, [this.httpStatus]);
  final String message;
  final int? httpStatus;
}
