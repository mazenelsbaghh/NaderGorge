import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import '../application/center_store.dart';
import '../data/center_state_encoder.dart';
import '../shared/problem_reporting.dart';
import 'lan_transport.dart';

class _StaffSession {
  const _StaffSession(this.deviceId, this.staffId, this.expiresAt);
  final String deviceId, staffId;
  final DateTime expiresAt;
}

/// Only the Go gateway can call this ephemeral loopback server. Each staff
/// session is bound to the verified device identity injected by that gateway.
class CenterStoreHostBridge {
  CenterStoreHostBridge._(this.store, this._server, this.secret) {
    _subscription = _server.listen((request) {
      unawaited(_handle(request));
    });
  }
  final CenterStore store;
  final HttpServer _server;
  final String secret;
  final _sessions = <String, _StaffSession>{};
  final _loginAttempts = <String, List<DateTime>>{};
  late final StreamSubscription<HttpRequest> _subscription;
  final _inFlight = <Future<void>>{};
  bool _closed = false;
  Uri get uri => Uri(scheme: 'http', host: '127.0.0.1', port: _server.port);

  static String _token() {
    final random = Random.secure();
    return base64UrlEncode(List.generate(32, (_) => random.nextInt(256)));
  }

  static Future<CenterStoreHostBridge> start(CenterStore store) async {
    if (store.isRemote) {
      throw const CenterException('الجهاز المتصل لا يمكنه استقبال أجهزة أخرى.');
    }
    final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    server.autoCompress = true;
    return CenterStoreHostBridge._(store, server, _token());
  }

  Future<Map<String, dynamic>> _body(HttpRequest request) async {
    if (request.headers.contentType?.mimeType != 'application/json') {
      throw const FormatException('JSON required');
    }
    final bytes = <int>[];
    await for (final chunk in request.timeout(const Duration(seconds: 15))) {
      if (bytes.length + chunk.length > 256 * 1024) {
        throw const FormatException('Request too large');
      }
      bytes.addAll(chunk);
    }
    return jsonDecode(utf8.decode(bytes)) as Map<String, dynamic>;
  }

  _StaffSession? _session(HttpRequest request, String deviceId) {
    final token = request.headers.value('X-Massar-Session');
    final session = _sessions[token];
    if (session == null ||
        session.deviceId != deviceId ||
        DateTime.now().isAfter(session.expiresAt)) {
      if (token != null) _sessions.remove(token);
      return null;
    }
    return session;
  }

  Future<void> _handle(HttpRequest request) async {
    final work = _respond(request);
    _inFlight.add(work);
    try {
      await work;
    } finally {
      _inFlight.remove(work);
    }
  }

  Future<void> _respond(HttpRequest request) async {
    var status = HttpStatus.ok;
    Map<String, dynamic> result;
    try {
      final deviceId = request.headers.value('X-Massar-Device-ID');
      if (_closed ||
          request.connectionInfo?.remoteAddress.isLoopback != true ||
          request.headers.value('X-Massar-Bridge-Secret') != secret ||
          deviceId == null ||
          deviceId.isEmpty ||
          deviceId.length > 128) {
        status = HttpStatus.forbidden;
        result = {'message': 'هذا الجهاز غير مصرح له بالاتصال.'};
      } else if (request.uri.path == '/api/login' && request.method == 'POST') {
        final now = DateTime.now();
        final attempts = _loginAttempts.putIfAbsent(deviceId, () => []);
        attempts.removeWhere(
          (time) => now.difference(time) > const Duration(minutes: 1),
        );
        if (attempts.length >= 10) {
          status = HttpStatus.tooManyRequests;
          result = {'message': 'محاولات دخول كثيرة. انتظر دقيقة.'};
        } else {
          attempts.add(now);
          final body = await _body(request);
          final user = await store.authenticateLan(
            body['name'] as String,
            body['password'] as String,
          );
          final token = _token();
          _sessions.removeWhere(
            (_, value) =>
                value.deviceId == deviceId || now.isAfter(value.expiresAt),
          );
          _sessions[token] = _StaffSession(
            deviceId,
            user.id,
            now.add(const Duration(hours: 12)),
          );
          result = {
            'staffSession': token,
            ...await store.snapshotLan(
              user.id,
              stateEncoding: LanStateEncoding.json,
            ),
          };
          attempts.clear();
        }
      } else {
        final session = _session(request, deviceId);
        if (session == null) {
          status = HttpStatus.unauthorized;
          result = {'message': 'جلسة الموظف انتهت. سجّل الدخول من جديد.'};
        } else if (request.uri.path == '/api/state' &&
            request.method == 'GET') {
          result = await store.snapshotLan(
            session.staffId,
            knownStateVersion: request.headers.value('X-Massar-State-Version'),
            authorize: () => identical(_session(request, deviceId), session),
            stateEncoding: LanStateEncoding.json,
            patchVersion: request.headers.value('X-Massar-State-Patch'),
          );
        } else if (request.uri.path == '/api/support-upload' &&
            request.method == 'POST') {
          await _body(request);
          result = await store.queueSupportUploadLan(
            session.staffId,
            authorize: () => identical(_session(request, deviceId), session),
          );
        } else if (request.uri.path == '/api/logout' &&
            request.method == 'POST') {
          _sessions.remove(request.headers.value('X-Massar-Session'));
          result = {'ok': true};
        } else if ((request.uri.path == '/api/command' ||
                request.uri.path == '/api/cancel-command') &&
            request.method == 'POST') {
          result = await store.commandLan(
            deviceId: deviceId,
            staffId: session.staffId,
            request: await _body(request),
            authorize: () => identical(_session(request, deviceId), session),
            cancelIfUnseen: request.uri.path == '/api/cancel-command',
            stateEncoding: LanStateEncoding.json,
            knownStateVersion: request.headers.value('X-Massar-State-Version'),
            patchVersion: request.headers.value('X-Massar-State-Patch'),
          );
        } else {
          status = HttpStatus.notFound;
          result = {'message': 'الطلب غير متاح.'};
        }
      }
    } on LanAuthorizationException catch (error) {
      status = HttpStatus.unauthorized;
      result = {'message': error.message};
    } on CenterException catch (error, stack) {
      reportProblem(error, stack, operation: 'lan.host_request');
      status = HttpStatus.badRequest;
      result = {'message': error.message};
    } on FormatException {
      status = HttpStatus.badRequest;
      result = {'message': 'صيغة الطلب غير صحيحة.'};
    } on TypeError {
      status = HttpStatus.badRequest;
      result = {'message': 'بيانات الطلب غير مكتملة.'};
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'lan.host_request');
      status = HttpStatus.internalServerError;
      result = {'message': 'تعذر إتمام الطلب. راجع الاتصال وسجل المشاكل.'};
    }
    try {
      request.response.statusCode = status;
      request.response.headers.contentType = ContentType.json;
      request.response.headers.set('Cache-Control', 'no-store');
      request.response.write(_encodeResponse(result));
      await request.response.close();
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'lan.host_response');
    }
  }

  String _encodeResponse(Map<String, dynamic> response) =>
      '{${response.entries.map((entry) {
        final field = entry.value;
        final encoded = field is EncodedPublicCenterState ? field.json : jsonEncode(field);
        return '${jsonEncode(entry.key)}:$encoded';
      }).join(',')}}';

  Future<void> close() async {
    if (_closed) return;
    _closed = true;
    await _server.close(force: true);
    await Future.wait(_inFlight.toList());
    await _subscription.cancel();
    _sessions.clear();
  }
}
