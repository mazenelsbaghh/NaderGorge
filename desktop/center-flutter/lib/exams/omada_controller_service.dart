import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart' as crypto;

import 'omada_models.dart';
export 'omada_models.dart';

/// Omada's documented OpenAPI, never its private browser/session endpoints.
/// Schema: https://use1-omada-northbound.tplinkcloud.com/v3/api-docs
/// Authentication: https://support.omadanetworks.com/us/document/109315/
class OmadaOpenApiService implements OmadaControllerService {
  OmadaOpenApiService(
    this.config, {
    this.timeout = const Duration(seconds: 10),
  }) {
    validateOmadaControllerUri(config.baseUri);
    for (final value in [
      config.omadaId,
      config.clientId,
      config.clientSecret,
    ]) {
      if (value.trim().isEmpty || value.length > 4096) {
        throw const OmadaException(
          'أكمل بيانات ربط OpenAPI المحلية.',
          code: 'invalid_credentials',
        );
      }
    }
    final pin = config.certificateSha256;
    if (pin != null && !RegExp(r'^[a-fA-F0-9]{64}$').hasMatch(pin)) {
      throw const OmadaException(
        'بصمة شهادة الكنترولر غير صحيحة.',
        code: 'invalid_certificate_pin',
      );
    }
    _client = HttpClient(
      context: pin == null ? null : SecurityContext(withTrustedRoots: false),
    );
    _client.connectionTimeout = timeout;
    _client.findProxy = (_) => 'DIRECT';
    _client.badCertificateCallback = (certificate, host, port) =>
        pin != null &&
        host == config.baseUri.host &&
        port == config.baseUri.port &&
        _fingerprint(certificate) == pin.toLowerCase();
  }

  final OmadaControllerConfig config;
  final Duration timeout;
  late final HttpClient _client;
  String? _accessToken;
  DateTime _tokenExpires = DateTime.fromMillisecondsSinceEpoch(0);
  bool _closed = false;
  bool _applying = false;
  final _plans = Map<OmadaStaffWifiPreview, _StaffPlan>.identity();
  final _attempts = <String>{};

  @override
  Future<OmadaCertificateInfo> probeCertificate(Uri baseUri) async {
    validateOmadaControllerUri(baseUri);
    SecureSocket? socket;
    try {
      // This connection sends no HTTP request or credentials. Trust is still
      // required separately before any authenticated API call can be made.
      socket = await SecureSocket.connect(
        baseUri.host,
        baseUri.port,
        onBadCertificate: (_) => true,
        timeout: timeout,
      );
      final certificate = socket.peerCertificate;
      if (certificate == null) throw const FormatException();
      return OmadaCertificateInfo(
        sha256: _fingerprint(certificate),
        subject: certificate.subject,
        issuer: certificate.issuer,
        validFrom: certificate.startValidity,
        validUntil: certificate.endValidity,
      );
    } on Object catch (error) {
      if (error is! IOException &&
          error is! TimeoutException &&
          error is! FormatException) {
        rethrow;
      }
      throw const OmadaException(
        'تعذر قراءة شهادة الكنترولر. تأكد أنه يعمل وأن العنوان والمنفذ صحيحان.',
        code: 'certificate_probe_failed',
      );
    } finally {
      socket?.destroy();
    }
  }

  @override
  Future<List<OmadaSite>> listSites() async => [
    for (final row in await _pages(_path(['sites'])))
      OmadaSite(id: _text(row, 'siteId'), name: _text(row, 'name')),
  ];

  @override
  Future<List<OmadaAccessPoint>> listAccessPoints(String siteId) async => [
    for (final row in await _pages(_path(['sites', siteId, 'devices'])))
      if (['ap', 'eap'].contains(row['type']?.toString().toLowerCase()))
        OmadaAccessPoint(
          mac: _text(row, 'mac'),
          name: _text(row, 'name'),
          model: (row['model'] ?? row['modelName'] ?? '').toString(),
          status: _integer(row, 'status'),
          detailStatus: _integer(row, 'detailStatus'),
        ),
  ];

  @override
  Future<List<OmadaWlanGroup>> listWlanGroups(String siteId) async {
    final result = await _get(
      _path(['sites', siteId, 'wireless-network', 'wlans']),
    );
    if (result is! List || result.length > 32) throw _invalidResponse();
    return [
      for (final value in result)
        OmadaWlanGroup(
          id: _id(_object(value), 'wlanId'),
          name: _text(_object(value), 'name'),
        ),
    ];
  }

  @override
  Future<List<OmadaSsid>> listSsids(String siteId, String wlanId) async => [
    for (final row in await _pages(_ssidPath(siteId, wlanId), maxRows: 64))
      _ssid(row),
  ];

  @override
  Future<OmadaStaffWifiPreview> previewStaffWifi({
    required String siteId,
    required String wlanId,
    required String accessPointMac,
    required String studentSsidId,
    required String staffName,
    required String staffPassword,
  }) async {
    _validateStaffWifi(staffName, staffPassword);
    if (_attempts.contains(_attemptKey(siteId, wlanId, staffName))) {
      throw const OmadaException(
        'هناك محاولة إنشاء سابقة لهذه الشبكة. أعد التحقق من نتيجة المعاينة السابقة قبل إنشاء محاولة أخرى.',
        code: 'previous_attempt_pending',
      );
    }
    final sites = await listSites();
    if (!sites.any((site) => site.id == siteId)) throw _selectionChanged();
    final groups = await listWlanGroups(siteId);
    if (!groups.any((group) => group.id == wlanId)) throw _selectionChanged();
    await _requireApMembership(siteId, wlanId, accessPointMac);
    await _checkUniqueName(siteId, groups, staffName);
    final snapshot = await _snapshot(siteId, wlanId);
    if (!snapshot.ssids.any((ssid) => ssid.id == studentSsidId)) {
      throw _selectionChanged();
    }
    final preview = OmadaStaffWifiPreview(
      siteId: siteId,
      wlanId: wlanId,
      accessPointMac: accessPointMac,
      staffName: staffName,
      preservedSsids: List.unmodifiable(snapshot.ssids),
    );
    _plans[preview] = _StaffPlan(preview, staffPassword, snapshot.digests);
    return preview;
  }

  @override
  Future<OmadaStaffWifiResult> applyStaffWifi(
    OmadaStaffWifiPreview preview, {
    Future<void> Function()? beforeWrite,
  }) async {
    final plan = _plans[preview];
    if (plan == null || _closed) throw _selectionChanged();
    if (_applying) {
      throw const OmadaException(
        'جارٍ التحقق من إنشاء شبكة الموظفين؛ انتظر انتهاء المحاولة الحالية.',
        code: 'operation_in_progress',
      );
    }
    _applying = true;
    try {
      if (plan.attempted) return await _verifyCreated(plan, recovered: true);
      if (DateTime.now().difference(plan.createdAt) >
          const Duration(minutes: 5)) {
        throw const OmadaException(
          'انتهت صلاحية المعاينة. اعرض المعاينة مجددًا قبل الإنشاء.',
          code: 'preview_expired',
        );
      }
      final current = await _snapshot(preview.siteId, preview.wlanId);
      _requireUnchanged(plan, current);
      await _checkUniqueName(
        preview.siteId,
        await listWlanGroups(preview.siteId),
        preview.staffName,
      );
      await _requireApMembership(
        preview.siteId,
        preview.wlanId,
        preview.accessPointMac,
      );
      if (_accessToken == null || !DateTime.now().isBefore(_tokenExpires)) {
        await _authorize();
      }
      // The caller checks its current view/maintenance lease here. A
      // callback failure means no write and no attempted-operation receipt.
      if (beforeWrite != null) await beforeWrite();
      _ensureOpen();
      plan.attempted = true;
      _attempts.add(
        _attemptKey(preview.siteId, preview.wlanId, preview.staffName),
      );
      try {
        await _request(
          'POST',
          _ssidPath(preview.siteId, preview.wlanId),
          body: _createBody(plan),
          token: _accessToken,
        );
      } on OmadaException {
        // A response can be lost after creation. Never send a second POST.
        return await _verifyCreated(plan, recovered: true);
      }
      return await _verifyCreated(plan, recovered: false);
    } finally {
      _applying = false;
    }
  }

  Future<OmadaStaffWifiResult> _verifyCreated(
    _StaffPlan plan, {
    required bool recovered,
  }) async {
    final preview = plan.preview;
    try {
      final snapshot = await _snapshot(preview.siteId, preview.wlanId);
      _requireUnchanged(plan, snapshot, allowCreatedName: preview.staffName);
      final matches = snapshot.ssids
          .where((ssid) => ssid.name == preview.staffName)
          .toList();
      if (matches.length != 1) throw _uncertain();
      final ssid = matches.single;
      final detail = await _detail(preview.siteId, preview.wlanId, ssid.id);
      final psk = _object(detail['pskSetting']);
      if (detail['security'] != 3 ||
          detail['band'] != 3 ||
          detail['broadcast'] != true ||
          detail['guestNetEnable'] != false ||
          detail['vlanEnable'] != false ||
          detail['deviceType'] != 1 ||
          detail['ssidEnable'] == false ||
          psk['versionPsk'] != 2 ||
          psk['encryptionPsk'] != 3) {
        throw _uncertain();
      }
      final readKey = psk['securityKey'];
      if (readKey is String &&
          readKey.isNotEmpty &&
          !RegExp(r'^\*+$').hasMatch(readKey) &&
          readKey != plan.password) {
        throw _uncertain();
      }
      return OmadaStaffWifiResult(
        ssid: ssid,
        recoveredAfterUncertainCreate: recovered,
      );
    } on OmadaException catch (error) {
      if (error.code == 'existing_ssid_changed') rethrow;
      throw _uncertain();
    }
  }

  Future<void> _checkUniqueName(
    String siteId,
    List<OmadaWlanGroup> groups,
    String staffName,
  ) async {
    for (final group in groups) {
      final ssids = await listSsids(siteId, group.id);
      if (ssids.any(
        (ssid) => ssid.name.toLowerCase() == staffName.toLowerCase(),
      )) {
        throw const OmadaException(
          'اسم شبكة الموظفين موجود بالفعل. اختر اسمًا آخر؛ لن نغيّر أي شبكة موجودة.',
          code: 'ssid_name_exists',
        );
      }
    }
  }

  Future<void> _requireApMembership(
    String siteId,
    String wlanId,
    String mac,
  ) async {
    final aps = await listAccessPoints(siteId);
    if (!aps.any(
      (ap) => ap.mac.toLowerCase() == mac.toLowerCase() && ap.isConnected,
    )) {
      throw const OmadaException(
        'اختر الأكسس بوينت المتصل والمضاف للكنترولر. لن نضيف أو نعيد ضبط أي جهاز تلقائيًا.',
        code: 'ap_not_connected',
      );
    }
    final members = await _pages(
      _path(['sites', siteId, 'ap-groups', wlanId, 'aps']),
    );
    if (!members.any(
      (row) =>
          row['mac']?.toString().toLowerCase() == mac.toLowerCase() &&
          row['apGroupId'] == wlanId,
    )) {
      throw const OmadaException(
        'مجموعة الشبكات المختارة ليست مرتبطة بالأكسس بوينت المحدد. اختر مجموعته الحالية؛ لن ننقله أو نغيّر شبكة الطلاب.',
        code: 'ap_group_mismatch',
      );
    }
  }

  Future<_SsidSnapshot> _snapshot(String siteId, String wlanId) async {
    final ssids = await listSsids(siteId, wlanId);
    final digests = <String, String>{};
    for (final ssid in ssids) {
      if (digests.containsKey(ssid.id)) throw _invalidResponse();
      digests[ssid.id] = _digest(await _detail(siteId, wlanId, ssid.id));
    }
    return _SsidSnapshot(ssids, digests);
  }

  void _requireUnchanged(
    _StaffPlan plan,
    _SsidSnapshot snapshot, {
    String? allowCreatedName,
  }) {
    for (final entry in plan.digests.entries) {
      if (snapshot.digests[entry.key] != entry.value) throw _existingChanged();
    }
    for (final ssid in snapshot.ssids) {
      if (!plan.digests.containsKey(ssid.id) && ssid.name != allowCreatedName) {
        throw _existingChanged();
      }
    }
  }

  Map<String, Object?> _createBody(_StaffPlan plan) => {
    'name': plan.preview.staffName,
    'deviceType': 1,
    'ssidEnable': true,
    'chooseDevices': 1,
    'apGroupIds': [plan.preview.wlanId],
    'band': 3,
    'guestNetEnable': false,
    'security': 3,
    'broadcast': true,
    'vlanEnable': false,
    'mloEnable': false,
    'pmfMode': 2,
    'enable11r': false,
    'hidePwd': true,
    'pskSetting': {
      'securityKey': plan.password,
      'versionPsk': 2,
      'encryptionPsk': 3,
      'gikRekeyPskEnable': false,
    },
  };

  Future<Map<String, dynamic>> _detail(
    String siteId,
    String wlanId,
    String ssidId,
  ) async =>
      _object(await _get('${_ssidPath(siteId, wlanId)}/${_segment(ssidId)}'));

  Future<List<Map<String, dynamic>>> _pages(
    String path, {
    int maxRows = 2000,
  }) async {
    final rows = <Map<String, dynamic>>[];
    for (var page = 1; page <= 20; page++) {
      final result = _object(
        await _get(path, query: {'page': '$page', 'pageSize': '100'}),
      );
      final data = result['data'];
      final total = result['totalRows'];
      if (data is! List ||
          total is! int ||
          total < 0 ||
          total > maxRows ||
          data.length > 100 ||
          rows.length + data.length > maxRows ||
          (result['currentPage'] != null && result['currentPage'] != page)) {
        throw _invalidResponse();
      }
      rows.addAll(data.map(_object));
      if (rows.length == total) return rows;
      if (data.isEmpty || rows.length > total) throw _invalidResponse();
    }
    throw _invalidResponse();
  }

  Future<Object?> _get(String path, {Map<String, String>? query}) =>
      _authenticated('GET', path, query: query);

  Future<Object?> _authenticated(
    String method,
    String path, {
    Map<String, String>? query,
    Map<String, Object?>? body,
  }) async {
    _ensureOpen();
    if (_accessToken == null || !DateTime.now().isBefore(_tokenExpires)) {
      await _authorize();
    }
    try {
      return await _request(
        method,
        path,
        query: query,
        body: body,
        token: _accessToken,
      );
    } on OmadaException catch (error) {
      // Safe reads may renew an expired token. Mutations are never replayed.
      if (error.code != 'authentication_failed' || method != 'GET') rethrow;
      await _authorize();
      return _request(method, path, query: query, token: _accessToken);
    }
  }

  Future<void> _authorize() async {
    final result = _object(
      await _request(
        'POST',
        '/openapi/authorize/token',
        query: {'grant_type': 'client_credentials'},
        body: {
          'omadacId': config.omadaId,
          'client_id': config.clientId,
          'client_secret': config.clientSecret,
        },
      ),
    );
    final token = result['accessToken'];
    final expires = result['expiresIn'];
    if (token is! String ||
        token.isEmpty ||
        token.length > 8192 ||
        expires is! num ||
        expires <= 0) {
      throw _invalidResponse();
    }
    _accessToken = token;
    _tokenExpires = DateTime.now().add(
      Duration(seconds: (expires.toInt() - 30).clamp(1, 7200)),
    );
  }

  Future<Object?> _request(
    String method,
    String path, {
    Map<String, String>? query,
    Map<String, Object?>? body,
    String? token,
  }) async {
    _ensureOpen();
    HttpClientRequest? request;
    try {
      final uri = config.baseUri.replace(path: path, queryParameters: query);
      request = await _client.openUrl(method, uri).timeout(timeout);
      _ensureOpen();
      request.followRedirects = false;
      request.headers.set(HttpHeaders.acceptHeader, 'application/json');
      if (token != null) {
        request.headers.set(
          HttpHeaders.authorizationHeader,
          'AccessToken=$token',
        );
      }
      if (body != null) {
        request.headers.contentType = ContentType.json;
        request.write(jsonEncode(body));
      }
      final response = await request.close().timeout(timeout);
      if (response.statusCode == 401 || response.statusCode == 403) {
        throw const OmadaException(
          'بيانات OpenAPI غير صحيحة أو التطبيق لا يملك صلاحية الموقع المختار.',
          code: 'authentication_failed',
        );
      }
      if (response.statusCode == 404 || response.statusCode == 405) {
        throw const OmadaException(
          'إصدار الكنترولر لا يوفر واجهة OpenAPI المطلوبة. راجع تفعيل OpenAPI وإصدار Omada.',
          code: 'unsupported_controller',
        );
      }
      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw const OmadaException(
          'لم يؤكد الكنترولر تنفيذ الطلب. لن نكرر إنشاء الشبكة تلقائيًا.',
          code: 'controller_http_error',
        );
      }
      final bytes = await _readResponse(response).timeout(timeout);
      final envelope = _object(jsonDecode(utf8.decode(bytes)));
      if (envelope['errorCode'] is! int) throw _invalidResponse();
      if (envelope['errorCode'] != 0) {
        throw const OmadaException(
          'رفض Omada الطلب. راجع صلاحيات تطبيق OpenAPI والموقع المختار وإعداداته من الكنترولر.',
          code: 'controller_rejected',
        );
      }
      return envelope['result'];
    } on OmadaException {
      request?.abort();
      rethrow;
    } on HandshakeException {
      request?.abort();
      throw const OmadaException(
        'شهادة Omada غير موثوقة أو تغيرت. افحص بصمتها وقارنها بالكنترولر قبل الموافقة عليها.',
        code: 'certificate_not_trusted',
      );
    } on Object catch (error) {
      request?.abort();
      if (error is FormatException) throw _invalidResponse();
      if (error is! IOException && error is! TimeoutException) rethrow;
      throw const OmadaException(
        'تعذر الوصول إلى Omada أو انتهت مهلة الرد. تأكد أن الكنترولر المحلي يعمل.',
        code: 'connection_failed',
      );
    }
  }

  Future<List<int>> _readResponse(HttpClientResponse response) async {
    final bytes = <int>[];
    await for (final chunk in response) {
      if (bytes.length + chunk.length > 2 * 1024 * 1024) {
        throw _invalidResponse();
      }
      bytes.addAll(chunk);
    }
    return bytes;
  }

  String _path(List<String> segments) =>
      '/openapi/v1/${_segment(config.omadaId)}/${segments.map(_segment).join('/')}';
  String _ssidPath(String siteId, String wlanId) =>
      _path(['sites', siteId, 'wireless-network', 'wlans', wlanId, 'ssids']);

  void _ensureOpen() {
    if (_closed) {
      throw const OmadaException(
        'انتهت جلسة الاتصال بالكنترولر. اتصل مجددًا.',
        code: 'connection_closed',
      );
    }
  }

  @override
  void close() {
    _closed = true;
    _accessToken = null;
    _plans.clear();
    _attempts.clear();
    _client.close(force: true);
  }
}

class _StaffPlan {
  _StaffPlan(this.preview, this.password, this.digests);
  final OmadaStaffWifiPreview preview;
  final String password;
  final Map<String, String> digests;
  final DateTime createdAt = DateTime.now();
  bool attempted = false;
}

class _SsidSnapshot {
  _SsidSnapshot(this.ssids, this.digests);
  final List<OmadaSsid> ssids;
  final Map<String, String> digests;
}

String _fingerprint(X509Certificate certificate) =>
    crypto.sha256.convert(certificate.der).toString();
String _attemptKey(String site, String wlan, String name) =>
    jsonEncode([site, wlan, name.toLowerCase()]);
String _segment(String value) {
  if (value.isEmpty ||
      value.length > 256 ||
      value == '.' ||
      value == '..' ||
      RegExp(r'[/\\\x00-\x1f]').hasMatch(value)) {
    throw _selectionChanged();
  }
  return Uri.encodeComponent(value);
}

Map<String, dynamic> _object(Object? value) {
  if (value is! Map<String, dynamic>) throw _invalidResponse();
  return value;
}

String _text(Map<String, dynamic> row, String key) {
  final value = row[key];
  if (value is! String || value.isEmpty || value.length > 4096) {
    throw _invalidResponse();
  }
  return value;
}

int _integer(Map<String, dynamic> row, String key) {
  final value = row[key];
  if (value is! int) throw _invalidResponse();
  return value;
}

bool _boolean(Map<String, dynamic> row, String key) {
  final value = row[key];
  if (value is! bool) throw _invalidResponse();
  return value;
}

String _id(Map<String, dynamic> row, String legacyKey) =>
    row['id'] is String && (row['id'] as String).isNotEmpty
    ? _text(row, 'id')
    : _text(row, legacyKey);
OmadaSsid _ssid(Map<String, dynamic> row) => OmadaSsid(
  id: _id(row, 'ssidId'),
  name: _text(row, 'name'),
  band: _integer(row, 'band'),
  security: _integer(row, 'security'),
  broadcast: _boolean(row, 'broadcast'),
  guestNetwork: _boolean(row, 'guestNetEnable'),
  vlanEnabled: _boolean(row, 'vlanEnable'),
);

void _validateStaffWifi(String name, String password) {
  if (name.trim() != name ||
      name.isEmpty ||
      utf8.encode(name).length > 32 ||
      RegExp(r'[\x00-\x1f\x7f]').hasMatch(name)) {
    throw const OmadaException(
      'اكتب اسمًا لشبكة الموظفين من ١ إلى ٣٢ بايت، بلا مسافات في البداية أو النهاية.',
      code: 'invalid_ssid_name',
    );
  }
  if (!RegExp(r'^[\x20-\x7e]{8,63}$').hasMatch(password)) {
    throw const OmadaException(
      'كلمة سر شبكة الموظفين يجب أن تكون من ٨ إلى ٦٣ حرفًا إنجليزيًا أو رقمًا أو رمزًا.',
      code: 'invalid_wifi_password',
    );
  }
}

// Compare full returned configuration, including fields this connector does not
// understand. Keep only hashes, so student Wi-Fi credentials never leave RAM.
String _digest(Map<String, dynamic> value) => crypto.sha256
    .convert(utf8.encode(jsonEncode(_canonical(value))))
    .toString();
Object? _canonical(Object? value) {
  if (value is Map<String, dynamic>) {
    final keys = value.keys.toList()..sort();
    return {for (final key in keys) key: _canonical(value[key])};
  }
  if (value is List) return value.map(_canonical).toList();
  return value;
}

OmadaException _invalidResponse() => const OmadaException(
  'رد Omada غير متوافق أو أكبر من الحد المسموح. لم نغيّر إعدادات أي شبكة موجودة.',
  code: 'invalid_response',
);
OmadaException _selectionChanged() => const OmadaException(
  'اختر الموقع ومجموعة الشبكات وشبكة الطلاب الموجودة ثم اعرض المعاينة مجددًا.',
  code: 'selection_changed',
);
OmadaException _existingChanged() => const OmadaException(
  'إعدادات شبكة موجودة تغيرت منذ المعاينة. توقفنا عن الإنشاء أو التكرار؛ راجع الكنترولر قبل المتابعة.',
  code: 'existing_ssid_changed',
);
OmadaException _uncertain() => const OmadaException(
  'لم نتأكد من نتيجة إنشاء شبكة الموظفين بعد. أعد التحقق من نفس المعاينة؛ لن نرسل طلب إنشاء مكررًا. راجع الكنترولر إذا استمرت الرسالة.',
  code: 'create_outcome_unknown',
);
