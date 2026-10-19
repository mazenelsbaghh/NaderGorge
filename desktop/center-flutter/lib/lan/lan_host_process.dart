import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:massar_center/lan/lan_transport.dart';
import 'package:path/path.dart' as path;

class LanHostReady {
  const LanHostReady({
    required this.hostId,
    required this.name,
    required this.port,
    required this.certificateSha256,
    required this.pairingCode,
    required this.pairingExpiresAt,
  });
  final String hostId;
  final String name;
  final int port;
  final String certificateSha256;
  final String pairingCode;
  final DateTime pairingExpiresAt;
  LanEndpoint get endpoint => LanEndpoint(
    hostId: hostId,
    name: name,
    address: InternetAddress.loopbackIPv4.address,
    port: port,
    certificateSha256: certificateSha256,
  );
  factory LanHostReady.fromJson(Map<String, dynamic> json) {
    if (json['ready'] != true || json['protocol'] != 1) {
      if (json['error'] == 'gateway_start_failed') {
        reportLan(
          'lan.host_start',
          json['stage'] is String ? json['stage'] as String : 'ready',
          error: CenterException(
            'LAN gateway startup failed',
            diagnosticCode: json['osCode'] is int
                ? json['osCode'] as int
                : null,
          ),
        );
      }
      throw CenterException(switch (json['stage']) {
        'tcp_bind' || 'udp_bind' =>
          'تعذر فتح منفذ الربط. اقفل نسخ مسار الأخرى على هذا الجهاز ثم شغّل السيرفر مجددًا. لو استمرت المشكلة صدّر سجل الربط.',
        'identity' || 'pairing_storage' =>
          'تعذر قراءة أو حفظ هوية الربط. صدّر سجل الربط لمراجعة صلاحيات ملفات البرنامج.',
        _ => 'تعذر تشغيل خدمة الربط المحلي. صدّر سجل الربط لمعرفة السبب.',
      });
    }
    final ready = LanHostReady(
      hostId: json['hostId'] as String,
      name: json['name'] as String,
      port: json['port'] as int,
      certificateSha256: json['certificateSha256'] as String,
      pairingCode: json['pairingCode'] as String,
      pairingExpiresAt: DateTime.parse(json['pairingExpiresAt'] as String),
    );
    LanEndpoint.fromJson(ready.endpoint.toJson());
    return ready;
  }
}

/// Owns only its child process. Secrets travel over stdin, never command args.
class LanHostProcess {
  LanHostProcess({String? executablePath})
    : executablePath =
          executablePath ??
          path.join(
            path.dirname(Platform.resolvedExecutable),
            Platform.isWindows ? 'massar-lan-host.exe' : 'massar-lan-host',
          );
  final String executablePath;
  final _exitCodes = StreamController<int>.broadcast();
  Process? _process;
  LanTransport? _control;
  bool _starting = false;
  Future<void>? _stopping;
  bool get isRunning => _process != null;
  Stream<int> get exitCodes => _exitCodes.stream;

  Future<LanHostReady> start({
    required String dataDirectory,
    required String upstreamUrl,
    required String upstreamSecret,
    required String name,
    int port = 43873,
    int discoveryPort = 43874,
  }) async {
    if (_starting || isRunning || _stopping != null) {
      throw const CenterException(
        'خدمة الربط المحلي تعمل أو يجري تشغيلها بالفعل.',
      );
    }
    _starting = true;
    final watch = Stopwatch()..start();
    var stage = 'spawn';
    try {
      final child = await Process.start(
        executablePath,
        const [],
        runInShell: false,
      );
      stage = 'ready';
      _process = child;
      final ready = Completer<LanHostReady>();
      _watchExit(child);
      unawaited(child.stderr.drain<void>());
      final output = child.stdout
          .transform(utf8.decoder)
          .transform(const LineSplitter())
          .listen(
            (line) {
              if (ready.isCompleted) return;
              try {
                if (line.length > 65536) {
                  throw const FormatException('Oversized ready');
                }
                ready.complete(
                  LanHostReady.fromJson(
                    jsonDecode(line) as Map<String, dynamic>,
                  ),
                );
              } on Object catch (error, stackTrace) {
                ready.completeError(
                  CenterException(
                    error is CenterException
                        ? error.message
                        : 'تعذر قراءة حالة خدمة الربط المحلي.',
                    cause: error,
                    stackTrace: stackTrace,
                  ),
                );
              }
            },
            onError: (Object error, StackTrace stackTrace) {
              if (!ready.isCompleted) {
                ready.completeError(
                  CenterException(
                    'تعذر قراءة حالة خدمة الربط المحلي.',
                    cause: error,
                    stackTrace: stackTrace,
                  ),
                );
              }
            },
            onDone: () {
              // stdout carries the final startup reason before the child exits.
              if (!ready.isCompleted) {
                ready.completeError(
                  const CenterException(
                    'توقفت خدمة الربط المحلي قبل بدء الاتصال.',
                  ),
                );
              }
            },
          );
      try {
        child.stdin.writeln(
          jsonEncode({
            'upstream': upstreamUrl,
            'upstreamSecret': upstreamSecret,
            'dataDir': dataDirectory,
            'name': name,
            'port': port,
            'discoveryPort': discoveryPort,
          }),
        );
        // The open stdin pipe is the gateway's parent-liveness signal.
        final startup = await Future.wait<Object?>([
          child.stdin.flush(),
          ready.future.timeout(const Duration(seconds: 15)),
        ], eagerError: true);
        final started = startup[1] as LanHostReady;
        if (!identical(_process, child)) {
          throw const CenterException('توقفت خدمة الربط المحلي أثناء تشغيلها.');
        }
        _control = LanTransport(started.endpoint, upstreamSecret);
        await _control!.health();
        reportLan(
          'lan.host_start',
          'ready',
          metrics: {
            'port': started.port,
            'durationMs': watch.elapsedMilliseconds,
          },
        );
        return started;
      } finally {
        await output.cancel();
      }
    } on Object catch (error, stackTrace) {
      reportLan(
        'lan.host_start',
        stage,
        error: error,
        metrics: {'durationMs': watch.elapsedMilliseconds, 'port': port},
      );
      await stop();
      if (error is CenterException) rethrow;
      throw CenterException(
        'تعذر تشغيل خدمة الربط المحلي. تأكد من وجود ملف الخدمة مع التطبيق.',
        cause: error,
        stackTrace: stackTrace,
      );
    } finally {
      _starting = false;
    }
  }

  void _watchExit(Process child) {
    unawaited(
      child.exitCode.then((code) {
        reportLan(
          'lan.host_exit',
          'exit',
          error: _stopping == null
              ? const CenterException('LAN child exited unexpectedly')
              : null,
          metrics: {
            'exitCode': code,
            'expectedExit': _stopping != null ? 1 : 0,
          },
        );
        if (identical(_process, child)) {
          _process = null;
          _control?.close();
          _control = null;
        }
        if (!_exitCodes.isClosed) _exitCodes.add(code);
      }),
    );
  }

  LanTransport get _activeControl =>
      _control ??
      (throw const CenterException('خدمة الربط المحلي لا تعمل حاليًا.'));
  Future<Map<String, dynamic>> startMobileHomework() =>
      _activeControl.post('/control/mobile/start', {});
  Future<void> stopMobileHomework() async {
    await _activeControl.post('/control/mobile/stop', {});
  }

  Future<Map<String, dynamic>> controlPairing() =>
      _activeControl.post('/control/pairing', {});
  Future<List<Map<String, dynamic>>> devices() async {
    final response = await _activeControl.get('/control/devices');
    return (response['devices'] as List).cast<Map<String, dynamic>>();
  }

  Future<void> revokeDevice(String deviceId) async {
    await _activeControl.post('/control/devices/revoke', {
      'deviceId': deviceId,
    });
  }

  Future<void> stop() {
    if (_stopping != null) return _stopping!;
    final stopped = _stop();
    _stopping = stopped;
    return stopped.whenComplete(() => _stopping = null);
  }

  Future<void> _stop() async {
    final child = _process;
    if (child == null) return;
    child.kill();
    try {
      await child.exitCode.timeout(const Duration(seconds: 3));
    } on TimeoutException {
      child.kill(ProcessSignal.sigkill);
      await child.exitCode.timeout(const Duration(seconds: 3));
    }
    await child.stdin.close();
    _control?.close();
    _control = null;
    if (identical(_process, child)) _process = null;
  }

  Future<void> dispose() async {
    await stop();
    await _exitCodes.close();
  }
}
