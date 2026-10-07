import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';

import '../application/center_store.dart';
import '../shared/problem_reporting.dart';
import 'center_store_host_bridge.dart';
import 'lan_discovery.dart';
import 'lan_host_process.dart';
import 'lan_settings.dart';
import 'lan_transport.dart';

enum LanConnectionStatus {
  standalone,
  hosting,
  connecting,
  connected,
  disconnected,
}

/// Selects one authoritative store; never merges the two devices' databases.
class LanController extends ChangeNotifier {
  LanController({
    required this.localStore,
    LanSettings? settings,
    LanHostProcess? hostProcess,
    Future<List<LanEndpoint>> Function()? searchHosts,
    this.clientOnly = false,
  }) : _settings =
           settings ?? LanSettings(File(localStore.databasePath).parent),
       _hostProcess = hostProcess ?? LanHostProcess(),
       _searchHosts = searchHosts ?? (() => LanDiscovery.search()),
       _activeStore = localStore;

  final CenterStore localStore;
  final bool clientOnly;
  bool get isClientOnly => clientOnly;
  final LanSettings _settings;
  final LanHostProcess _hostProcess;
  final Future<List<LanEndpoint>> Function() _searchHosts;
  CenterStore _activeStore;
  LanConfiguration? _configuration;
  LanTransport? _transport;
  CenterStoreHostBridge? _bridge;
  LanHostReady? _hostReady;
  StreamSubscription<int>? _exitSubscription;
  Timer? _pollTimer;
  Future<void> _queue = Future.value();
  bool _busy = false, _closed = false, _disposed = false, _polling = false;
  List<LanEndpoint> _endpoints = [];
  List<Map<String, dynamic>> _devices = [];
  String? _pairingCode;
  DateTime? _pairingExpiresAt;
  LanConnectionStatus _status = LanConnectionStatus.standalone;

  CenterStore get activeStore => _activeStore;
  LanConfiguration? get configuration => _configuration;
  LanConnectionStatus get status => _status;
  bool get isBusy => _busy;
  bool get isHost => _configuration?.mode == LanMode.host;
  bool get canConfigureHost =>
      !clientOnly && identical(activeStore, localStore) && localStore.canManage;
  LanHostReady? get hostReady => _hostReady;
  String? get pairingCode => _pairingCode;
  DateTime? get pairingExpiresAt => _pairingExpiresAt;
  List<LanEndpoint> get endpoints => List.unmodifiable(_endpoints);
  List<Map<String, dynamic>> get pairedDevices => List.unmodifiable(
    _devices.map((device) => Map<String, dynamic>.unmodifiable(device)),
  );
  String get statusLabel => switch (_status) {
    LanConnectionStatus.standalone =>
      clientOnly
          ? 'هذا جهاز فرعي؛ اربطه بالجهاز الرئيسي'
          : 'هذا الجهاز يعمل مستقلًا',
    LanConnectionStatus.hosting => 'السيرفر يعمل على هذا الجهاز',
    LanConnectionStatus.connecting => 'جارٍ الاتصال بالجهاز الرئيسي…',
    LanConnectionStatus.connected =>
      activeStore.currentUser == null
          ? 'الجهاز الرئيسي متاح؛ سجّل دخول الموظف'
          : 'متصل بالجهاز الرئيسي',
    LanConnectionStatus.disconnected => 'الاتصال بالجهاز الرئيسي متوقف',
  };

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  Future<T> _exclusive<T>(Future<T> Function() operation) {
    final result = _queue.then((_) async {
      if (_closed) throw const CenterException('تم إغلاق ربط الأجهزة.');
      _busy = true;
      _notify();
      try {
        return await operation();
      } finally {
        _busy = false;
        _notify();
      }
    });
    _queue = result.then<void>((_) {}, onError: (Object _, StackTrace _) {});
    return result;
  }

  Future<void> initialize() => _exclusive(() async {
    if (_configuration != null) return;
    _configuration = await _settings.read();
    if (clientOnly) {
      final paired =
          _configuration!.endpoint != null &&
          (_configuration!.deviceToken?.isNotEmpty ?? false);
      final mode = paired ? LanMode.client : LanMode.standalone;
      if (_configuration!.mode != mode) {
        final candidate = _configuration!.copyWith(mode: mode);
        await _settings.save(candidate);
        _configuration = candidate;
      }
    }
    if (_configuration!.mode == LanMode.host) {
      // A saved host was enabled by an administrator in an earlier run.
      try {
        await _startHost(_configuration!);
      } catch (error, stackTrace) {
        _status = LanConnectionStatus.disconnected;
        reportProblem(error, stackTrace, operation: 'lan.host_start');
      }
    } else if (_configuration!.mode == LanMode.client) {
      await _activateClient(_configuration!);
      await _checkConnection(recoverAddress: true);
    }
    _pollTimer = Timer.periodic(const Duration(seconds: 5), (_) => _poll());
  });

  void _poll() {
    if (_closed ||
        _busy ||
        _polling ||
        _configuration?.mode != LanMode.client) {
      return;
    }
    _polling = true;
    _exclusive(() => _checkConnection(recoverAddress: true))
        .whenComplete(() {
          _polling = false;
        })
        .catchError((Object error, StackTrace stackTrace) {
          reportProblem(error, stackTrace, operation: 'lan.connection');
        });
  }

  Future<void> discoverHosts() => _exclusive(() async {
    _endpoints = await _searchHosts();
  });

  Future<void> pairHost(
    LanEndpoint endpoint,
    String code,
  ) => _exclusive(() async {
    final config = _configuration;
    if (config == null) throw const CenterException('انتظر تهيئة ربط الأجهزة.');
    if (isHost) {
      throw const CenterException(
        'أوقف السيرفر المحلي قبل ربط هذا الجهاز كعميل.',
      );
    }
    if (!RegExp(r'^\d{6}$').hasMatch(code)) {
      throw const CenterException('اكتب رمز الربط المكوّن من ستة أرقام.');
    }
    final saved = config.endpoint;
    if (saved == null ||
        saved.hostId != endpoint.hostId ||
        saved.certificateSha256 != endpoint.certificateSha256) {
      await activeStore.prepareLanSwitch();
    }
    final pairing = LanTransport(endpoint, '');
    try {
      final response = await pairing.pair(
        code,
        config.deviceId,
        config.deviceName,
      );
      final token = response['token'];
      if (token is! String || token.isEmpty) {
        throw const CenterException('لم يُكمل الجهاز الرئيسي ربط هذا الجهاز.');
      }
      final candidate = config.copyWith(
        mode: LanMode.client,
        endpoint: endpoint,
        deviceToken: token,
      );
      await _activateClient(candidate, persist: true);
      await _checkConnection(recoverAddress: false);
    } finally {
      pairing.close();
    }
  });

  Future<void> _activateClient(
    LanConfiguration candidate, {
    bool persist = false,
  }) async {
    final transport = LanTransport(candidate.endpoint!, candidate.deviceToken!);
    CenterStore? remote;
    try {
      remote = CenterStore.remote(
        transport,
        localDirectory: File(localStore.databasePath).parent.path,
      );
      if (persist) await _settings.save(candidate);
    } catch (_) {
      if (remote != null) await remote.close();
      transport.close();
      rethrow;
    }
    final previous = _activeStore;
    final previousTransport = _transport;
    previous.signOut();
    localStore.signOut();
    _activeStore = remote;
    _transport = transport;
    _configuration = candidate;
    _status = LanConnectionStatus.connecting;
    _devices = [];
    _notify();
    if (!identical(previous, localStore)) await previous.close();
    previousTransport?.close();
  }

  Future<void> reconnect() => _exclusive(() async {
    final config = _configuration;
    if (config?.endpoint == null || config?.deviceToken == null) {
      throw const CenterException(
        'اختار الجهاز الرئيسي وأدخل رمز الربط أول مرة.',
      );
    }
    if (isHost) throw const CenterException('أوقف السيرفر المحلي أولًا.');
    if (!activeStore.isRemote) {
      await _activateClient(
        config!.copyWith(mode: LanMode.client),
        persist: true,
      );
    }
    await _checkConnection(recoverAddress: true);
    if (_status != LanConnectionStatus.connected) {
      throw const CenterException(
        'الجهاز الرئيسي غير متاح. شغّله وتأكد أن الجهازين على نفس الشبكة.',
      );
    }
  });

  Future<void> testConnection() => _exclusive(() async {
    if (isHost) {
      if (!_hostProcess.isRunning || _hostReady == null) {
        throw const CenterException('السيرفر المحلي متوقف.');
      }
      final probe = LanTransport(_hostReady!.endpoint, '');
      try {
        await probe.health();
      } finally {
        probe.close();
      }
      return;
    }
    if (!activeStore.isRemote) {
      throw const CenterException(
        'هذا الجهاز يعمل مستقلًا؛ لا يوجد ربط لاختباره.',
      );
    }
    await _checkConnection(recoverAddress: true);
    if (_status != LanConnectionStatus.connected) {
      throw const CenterException(
        'تعذر الوصول للجهاز الرئيسي. لم تُحفظ عمليات دون اتصال.',
      );
    }
  });

  Future<void> _checkConnection({required bool recoverAddress}) async {
    try {
      await _verifyConnection();
      _status = LanConnectionStatus.connected;
      return;
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'lan.connection');
      _status = LanConnectionStatus.disconnected;
    }
    if (!recoverAddress) return;
    final saved = _configuration!.endpoint!;
    List<LanEndpoint> discovered;
    try {
      discovered = await _searchHosts();
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'lan.connection');
      return;
    }
    final matching = discovered
        .where(
          (endpoint) =>
              endpoint.hostId == saved.hostId &&
              endpoint.certificateSha256 == saved.certificateSha256,
        )
        .firstOrNull;
    if (matching == null ||
        (matching.address == saved.address && matching.port == saved.port)) {
      return;
    }
    try {
      await _activateClient(
        _configuration!.copyWith(endpoint: matching),
        persist: true,
      );
      await _verifyConnection();
      _status = LanConnectionStatus.connected;
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'lan.connection');
      _status = LanConnectionStatus.disconnected;
    }
  }

  Future<void> _verifyConnection() async {
    await activeStore.refreshRemote();
  }

  void _requireHostAdmin() {
    if (clientOnly) {
      throw const CenterException(
        'نسخة الجهاز الفرعي لا تعمل كسيرفر أو جهاز مستقل.',
      );
    }
    if (!canConfigureHost) {
      throw const CenterException(
        'تشغيل السيرفر وإدارة الأجهزة متاحان لمدير الجهاز الرئيسي فقط.',
      );
    }
  }

  Future<void> startHost({String? name}) => _exclusive(() async {
    _requireHostAdmin();
    if (_hostProcess.isRunning) {
      throw const CenterException('السيرفر المحلي يعمل بالفعل.');
    }
    final config = _configuration;
    if (config == null) throw const CenterException('انتظر تهيئة ربط الأجهزة.');
    final hostName = (name ?? config.deviceName).trim();
    if (hostName.isEmpty || hostName.length > 120) {
      throw const CenterException('اكتب اسمًا للجهاز من حرف إلى ١٢٠ حرفًا.');
    }
    final candidate = config.copyWith(mode: LanMode.host, deviceName: hostName);
    try {
      await _startHost(candidate);
    } catch (_) {
      _status = isHost
          ? LanConnectionStatus.disconnected
          : LanConnectionStatus.standalone;
      rethrow;
    }
    try {
      await _settings.save(candidate);
      _configuration = candidate;
    } catch (_) {
      await _stopHostResources();
      _status = LanConnectionStatus.standalone;
      rethrow;
    }
    await _loadDevices();
  });

  Future<void> _startHost(LanConfiguration candidate) async {
    await _stopHostResources();
    final bridge = await CenterStoreHostBridge.start(localStore);
    try {
      final ready = await _hostProcess.start(
        dataDirectory: File(localStore.databasePath).parent.path,
        upstreamUrl: bridge.uri.toString(),
        upstreamSecret: bridge.secret,
        name: candidate.deviceName,
      );
      _bridge = bridge;
      _hostReady = ready;
      _pairingCode = ready.pairingCode;
      _pairingExpiresAt = ready.pairingExpiresAt;
      _status = LanConnectionStatus.hosting;
      _exitSubscription = _hostProcess.exitCodes.listen((code) {
        if (_configuration?.mode == LanMode.host && !_closed) {
          reportProblem(
            CenterException('توقفت خدمة الربط المحلي (رمز الخروج: $code).'),
            StackTrace.current,
            operation: 'lan.host_exit',
          );
          _status = LanConnectionStatus.disconnected;
          _hostReady = null;
          _pairingCode = null;
          _notify();
        }
      });
    } catch (_) {
      await bridge.close();
      rethrow;
    }
  }

  Future<void> _stopHostResources() async {
    await _exitSubscription?.cancel();
    _exitSubscription = null;
    await _hostProcess.stop();
    await _bridge?.close();
    _bridge = null;
    _hostReady = null;
    _pairingCode = null;
    _pairingExpiresAt = null;
    _devices = [];
  }

  Future<void> stopHost() => _exclusive(() async {
    _requireHostAdmin();
    final candidate = _configuration!.copyWith(mode: LanMode.standalone);
    await _settings.save(candidate);
    await _stopHostResources();
    _configuration = candidate;
    _status = LanConnectionStatus.standalone;
  });

  Future<void> useStandalone() => _exclusive(() async {
    if (clientOnly) {
      throw const CenterException(
        'نسخة الجهاز الفرعي تعمل ببيانات الجهاز الرئيسي فقط؛ لا يوجد وضع محلي مستقل.',
      );
    }
    if (isHost) _requireHostAdmin();
    await activeStore.prepareLanSwitch();
    final candidate = _configuration!.copyWith(mode: LanMode.standalone);
    await _settings.save(candidate);
    await _stopHostResources();
    final previous = _activeStore;
    previous.signOut();
    localStore.signOut();
    _activeStore = localStore;
    _transport?.close();
    _transport = null;
    _configuration = candidate;
    _status = LanConnectionStatus.standalone;
    _notify();
    if (!identical(previous, localStore)) await previous.close();
  });

  Future<void> _loadDevices() async {
    _devices = await _hostProcess.devices();
  }

  Future<void> refreshDevices() => _exclusive(() async {
    _requireHostAdmin();
    if (!isHost || !_hostProcess.isRunning) {
      throw const CenterException('شغّل السيرفر المحلي أولًا.');
    }
    await _loadDevices();
  });
  Future<void> renewPairingCode() => _exclusive(() async {
    _requireHostAdmin();
    if (!isHost || !_hostProcess.isRunning) {
      throw const CenterException('شغّل السيرفر المحلي أولًا.');
    }
    final result = await _hostProcess.controlPairing();
    final code = result['pairingCode'];
    final expires = result['expiresAt'];
    if (code is! String ||
        !RegExp(r'^\d{6}$').hasMatch(code) ||
        expires is! String ||
        DateTime.tryParse(expires) == null) {
      throw const CenterException('تعذر إصدار رمز ربط جديد.');
    }
    _pairingCode = code;
    _pairingExpiresAt = DateTime.parse(expires);
  });
  Future<void> revokeDevice(String id) => _exclusive(() async {
    _requireHostAdmin();
    if (!isHost || !_hostProcess.isRunning) {
      throw const CenterException('شغّل السيرفر المحلي أولًا.');
    }
    await _hostProcess.revokeDevice(id);
    await _loadDevices();
  });

  Future<void> close() async {
    if (_closed) return;
    _pollTimer?.cancel();
    await _queue;
    if (_closed) return;
    _closed = true;
    await _stopHostResources();
    await _hostProcess.dispose();
    _transport?.close();
    _transport = null;
    if (!identical(_activeStore, localStore)) await _activeStore.close();
  }

  @override
  void dispose() {
    _disposed = true;
    _pollTimer?.cancel();
    super.dispose();
  }
}
