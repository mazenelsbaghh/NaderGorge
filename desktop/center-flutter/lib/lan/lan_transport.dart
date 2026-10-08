import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';
import 'package:flutter/foundation.dart';
import '../shared/performance_trace.dart';
import 'lan_snapshot_transfer.dart';
import 'dart:io';

import 'package:crypto/crypto.dart' as crypto;
import 'package:massar_center/domain/models.dart';

class LanEndpoint {
  const LanEndpoint({
    required this.hostId,
    required this.name,
    required this.address,
    required this.port,
    required this.certificateSha256,
  });

  final String hostId;
  final String name;
  final String address;
  final int port;
  final String certificateSha256;

  Uri uri(String path) =>
      Uri(scheme: 'https', host: address, port: port, path: path);

  bool verify(X509Certificate certificate) =>
      RegExp(r'^[a-f0-9]{64}$').hasMatch(certificateSha256) &&
      crypto.sha256.convert(certificate.der).toString() == certificateSha256;

  Map<String, dynamic> toJson() => {
    'hostId': hostId,
    'name': name,
    'address': address,
    'port': port,
    'certificateSha256': certificateSha256,
  };

  factory LanEndpoint.fromJson(Map<String, dynamic> json) {
    final endpoint = LanEndpoint(
      hostId: json['hostId'] as String,
      name: json['name'] as String,
      address: json['address'] as String,
      port: json['port'] as int,
      certificateSha256: json['certificateSha256'] as String,
    );
    if (endpoint.hostId.isEmpty ||
        InternetAddress.tryParse(endpoint.address) == null ||
        endpoint.port < 1 ||
        endpoint.port > 65535 ||
        !RegExp(r'^[a-f0-9]{64}$').hasMatch(endpoint.certificateSha256)) {
      throw const FormatException('Invalid LAN endpoint');
    }
    return endpoint;
  }
}

class LanAuthorizationException extends CenterException {
  const LanAuthorizationException(
    super.message, {
    this.requiresPairing = false,
  });
  final bool requiresPairing;
}

class LanConnectionException extends CenterException {
  const LanConnectionException(
    super.message, {
    required this.outcomeUnknown,
    super.cause,
    super.stackTrace,
  });
  final bool outcomeUnknown;
}

/// No automatic mutation retries: a lost response may already have committed.
class LanTransport {
  LanTransport(this.endpoint, this.deviceToken) {
    _client = HttpClient(context: SecurityContext(withTrustedRoots: false));
    _client.connectionTimeout = const Duration(seconds: 8);
    _client.badCertificateCallback = (certificate, host, port) =>
        host == endpoint.address &&
        port == endpoint.port &&
        endpoint.verify(certificate);
  }

  final LanEndpoint endpoint;
  final String deviceToken;
  late final HttpClient _client;
  HttpClientRequest? _stateWait;
  void cancelStateWait() => _stateWait?.abort();

  static const maxRequestBytes = 256 * 1024;
  static const maxResponseBytes = 16 * 1024 * 1024;

  Future<Map<String, dynamic>> health() async {
    final response = await get('/health');
    _verifyIdentity(response);
    return response;
  }

  void _verifyIdentity(Map<String, dynamic> response) {
    if (response['protocol'] != 1 || response['hostId'] != endpoint.hostId) {
      throw const CenterException(
        'هوية جهاز السنتر لا تطابق الجهاز المحفوظ. أعد الربط بعد مراجعة الجهاز.',
      );
    }
  }

  Future<Map<String, dynamic>> pair(
    String code,
    String deviceId,
    String name,
  ) async {
    final response = await post('/pair', {
      'code': code,
      'deviceId': deviceId,
      'name': name,
    });
    _verifyIdentity(response);
    if (response['deviceId'] != deviceId ||
        response['token'] is! String ||
        (response['token'] as String).isEmpty) {
      throw const CenterException(
        'استجابة ربط الجهاز غير صالحة. أعد الربط من جهاز السنتر.',
      );
    }
    return response;
  }

  Future<Map<String, dynamic>> get(
    String path, {
    String? staffSession,
    String? stateVersion,
    bool statePatches = false,
    bool waitForChanges = false,
  }) => _request('GET', path, null, (
    staffSession: staffSession,
    stateVersion: stateVersion,
    statePatches: statePatches,
    waitForChanges: waitForChanges,
  ));
  Future<Map<String, dynamic>> post(
    String path,
    Map<String, dynamic> body, {
    String? staffSession,
    String? stateVersion,
    bool statePatches = false,
  }) => _request('POST', path, body, (
    staffSession: staffSession,
    stateVersion: stateVersion,
    statePatches: statePatches,
    waitForChanges: false,
  ));

  Future<Map<String, dynamic>> _request(
    String method,
    String path,
    Map<String, dynamic>? body,
    ({
      String? staffSession,
      String? stateVersion,
      bool statePatches,
      bool waitForChanges,
    })
    metadata,
  ) async {
    if (!path.startsWith('/') ||
        path.startsWith('//') ||
        path.contains('?') ||
        path.contains('#')) {
      throw const CenterException('مسار اتصال السنتر غير صالح.');
    }
    final encoded = body == null ? null : utf8.encode(jsonEncode(body));
    if (encoded != null && encoded.length > maxRequestBytes) {
      throw const CenterException('الطلب أكبر من الحد المسموح للاتصال المحلي.');
    }
    final trace = PerformanceTrace(
      path == '/api/command' ? 'lan.command' : 'lan.refresh',
      budgetMs: metadata.waitForChanges ? 15000 : 300,
    );
    HttpClientRequest? waitingRequest;
    try {
      final request = await _client.openUrl(method, endpoint.uri(path));
      trace.stage('connect');
      if (metadata.waitForChanges) {
        waitingRequest = request;
        _stateWait = request;
        request.headers.set('X-Massar-State-Wait', '1');
      }
      request.headers.set('X-Massar-State-Chunks', '1');
      request.followRedirects = false;
      request.headers.contentType = ContentType.json;
      if (deviceToken.isNotEmpty &&
          (path.startsWith('/api/') || path.startsWith('/control/'))) {
        request.headers.set(
          HttpHeaders.authorizationHeader,
          'Bearer $deviceToken',
        );
      }
      if (metadata.staffSession != null) {
        request.headers.set('X-Massar-Session', metadata.staffSession!);
      }
      if (metadata.stateVersion != null) {
        request.headers.set('X-Massar-State-Version', metadata.stateVersion!);
      }
      if (metadata.statePatches) {
        request.headers.set('X-Massar-State-Patch', '1');
      }
      if (encoded != null) request.add(encoded);
      final response = await request.close().timeout(
        const Duration(seconds: 20),
      );
      trace.stage('response');
      if (response.statusCode >= 300 && response.statusCode < 400) {
        await response.drain<void>();
        if (path == '/api/command') {
          throw const LanConnectionException(
            'تم رفض تحويل الاتصال؛ لم يمكن التأكد من نتيجة العملية. راجع السجل قبل المحاولة مجددًا.',
            outcomeUnknown: true,
          );
        }
        throw const CenterException('تم رفض تحويل الاتصال إلى جهاز آخر.');
      }
      final bytes = BytesBuilder(copy: false);
      await for (final chunk in response.timeout(const Duration(seconds: 20))) {
        if (bytes.length + chunk.length > maxResponseBytes) {
          throw LanConnectionException(
            'استجابة جهاز السنتر أكبر من الحد المسموح. راجع نتيجة العملية قبل المحاولة مجددًا.',
            outcomeUnknown: path == '/api/command',
          );
        }
        bytes.add(chunk);
      }
      if (response.statusCode >= 500) {
        throw LanConnectionException(
          path == '/api/command'
              ? 'تعذر التأكد من نتيجة العملية على جهاز السنتر. راجع سجل الطالب قبل محاولة التسجيل أو الدفع من جديد.'
              : 'تعذر الاتصال بخدمة السنتر على الجهاز الرئيسي.',
          outcomeUnknown: path == '/api/command',
        );
      }
      trace.stage('receive');
      trace.counts['bytes'] = bytes.length;
      final received = bytes.takeBytes();
      final decoded = received.length > 256 * 1024
          ? await compute(_decodeLanResponse, received)
          : _decodeLanResponse(received);
      trace.stage('decode');
      if (decoded is! Map<String, dynamic>) {
        throw const FormatException('Invalid LAN response');
      }
      if (response.statusCode == 401) {
        final requiresPairing = decoded['error'] == 'device_not_paired';
        throw LanAuthorizationException(
          requiresPairing
              ? 'هذا الجهاز غير مربوط أو تم إلغاء ربطه. أعد الربط من جهاز السنتر.'
              : 'انتهت جلسة الموظف. سجّل الدخول مرة أخرى.',
          requiresPairing: requiresPairing,
        );
      }
      if (response.statusCode < 200 || response.statusCode >= 300) {
        const messages = {
          'pairing_rejected': 'كود الربط غير صحيح أو انتهت صلاحيته.',
          'pairing_rate_limited':
              'محاولات الربط كثيرة. انتظر قليلًا قبل المحاولة مجددًا.',
          'owner_required': 'هذا الإجراء متاح على جهاز السنتر الرئيسي فقط.',
          'invalid_request': 'طلب الاتصال غير صالح.',
          'request_too_large': 'الطلب أكبر من الحد المسموح.',
        };
        final message =
            decoded['message'] ??
            messages[decoded['error']] ??
            'رفض جهاز السنتر الطلب.';
        throw CenterException(
          message is String && message.length <= 2000
              ? message
              : 'رفض جهاز السنتر الطلب.',
        );
      }
      if (decoded.containsKey('snapshotTransfer')) {
        if (path.startsWith('/api/state-chunk/')) {
          throw const FormatException('Nested snapshot transfer');
        }
        try {
          return await _readSnapshotTransfer(decoded, metadata.staffSession);
        } on LanAuthorizationException {
          rethrow;
        } catch (error, stack) {
          throw LanConnectionException(
            'تعذر إكمال بيانات الجهاز الرئيسي. أعد الاتصال لمراجعة نتيجة العملية.',
            outcomeUnknown: path == '/api/command',
            cause: error,
            stackTrace: stack,
          );
        }
      }
      return decoded;
    } on CenterException {
      rethrow;
    } on Object catch (error, stackTrace) {
      throw LanConnectionException(
        path == '/api/command'
            ? 'انقطع الاتصال؛ لم يمكن التأكد من نتيجة العملية. راجع سجل الطالب قبل محاولة التسجيل أو الدفع من جديد.'
            : 'تعذر الاتصال بجهاز السنتر. تأكد أن الجهاز والشبكة يعملان.',
        outcomeUnknown: path == '/api/command',
        cause: error,
        stackTrace: stackTrace,
      );
    } finally {
      if (identical(_stateWait, waitingRequest)) _stateWait = null;
      trace.finish();
    }
  }

  Future<Map<String, dynamic>> _readSnapshotTransfer(
    Map<String, dynamic> envelope,
    String? existingSession,
  ) async {
    final descriptor = envelope['snapshotTransfer'];
    final staffSession = envelope['staffSession'] ?? existingSession;
    if (descriptor is! Map ||
        staffSession is! String ||
        descriptor['token'] is! String ||
        !RegExp(r'^[0-9a-f-]{36}$').hasMatch(descriptor['token'] as String) ||
        descriptor['chunks'] is! int ||
        descriptor['bytes'] is! int ||
        descriptor['sha256'] is! String ||
        !RegExp(r'^[0-9a-f]{64}$').hasMatch(descriptor['sha256'] as String)) {
      throw const FormatException('Invalid snapshot transfer');
    }
    final count = descriptor['chunks'] as int;
    final expectedBytes = descriptor['bytes'] as int;
    if (expectedBytes <= 0 ||
        expectedBytes > LanSnapshotTransferCache.maximumBytes ||
        count !=
            (expectedBytes + LanSnapshotTransferCache.chunkBytes - 1) ~/
                LanSnapshotTransferCache.chunkBytes) {
      throw const FormatException('Invalid snapshot transfer size');
    }
    final bytes = BytesBuilder(copy: false);
    for (var index = 0; index < count; index++) {
      final chunk = await get(
        '/api/state-chunk/${descriptor['token']}/$index',
        staffSession: staffSession,
      );
      if (chunk['token'] != descriptor['token'] ||
          chunk['index'] != index ||
          chunk['chunk'] is! String) {
        throw const FormatException('Invalid snapshot chunk');
      }
      final part = base64Decode(chunk['chunk'] as String);
      final expectedPart =
          (expectedBytes - index * LanSnapshotTransferCache.chunkBytes).clamp(
            0,
            LanSnapshotTransferCache.chunkBytes,
          );
      if (part.length != expectedPart) {
        throw const FormatException('Invalid snapshot chunk size');
      }
      bytes.add(part);
    }
    return compute(verifyTransferredSnapshot, (
      Uint8List.fromList(bytes.takeBytes()),
      descriptor['sha256'] as String,
    ));
  }

  void close() => _client.close(force: true);
}

Object? _decodeLanResponse(List<int> bytes) => jsonDecode(utf8.decode(bytes));
