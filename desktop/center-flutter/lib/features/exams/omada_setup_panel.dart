import 'dart:io';

import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import '../../domain/models.dart';
import '../../exams/exam_room_controller.dart';
import '../../exams/omada_exam_network_guard.dart';
import '../../exams/omada_controller_service.dart';
import '../../exams/omada_profile_store.dart';
import '../../exams/omada_setup_links.dart';
import '../../shared/theme.dart';
import 'omada_setup_widgets.dart';

class OmadaSetupPanel extends StatefulWidget {
  const OmadaSetupPanel({
    super.key,
    required this.controller,
    required this.networkReady,
    this.disabled = false,
    this.serviceFactory = OmadaOpenApiService.new,
    this.profileStore,
  });
  final ExamRoomController controller;
  final bool networkReady;
  final bool disabled;
  final OmadaControllerServiceFactory serviceFactory;
  final OmadaProfileStore? profileStore;

  @override
  State<OmadaSetupPanel> createState() => _OmadaSetupPanelState();
}

class _OmadaSetupPanelState extends State<OmadaSetupPanel> {
  final _address = TextEditingController(text: 'https://127.0.0.1:8043');
  final _omadaId = TextEditingController();
  final _clientId = TextEditingController();
  final _secret = TextEditingController();
  final _staffName = TextEditingController(text: 'MASSAR-STAFF');
  final _staffPassword = TextEditingController();
  String? _certificatePin, _site, _wlan, _student, _accessPoint;
  String? _message;
  bool _busy = true, _success = false;
  OmadaControllerService? _service;
  OmadaProfileStore? _profiles;
  List<OmadaSite> _sites = [];
  List<OmadaAccessPoint> _accessPoints = [];
  List<OmadaWlanGroup> _wlans = [];
  List<OmadaSsid> _ssids = [];
  OmadaStaffWifiPreview? _preview;
  Map<String, dynamic>? _pending;
  Map<String, dynamic>? _lastConfirmed;
  int _generation = 0;

  bool get _disabled => _busy || widget.disabled;
  bool get _canCreate =>
      !_disabled &&
      widget.networkReady &&
      !widget.controller.hasLiveExam &&
      _preview != null &&
      _pending == null;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final support = widget.profileStore == null
          ? await getApplicationSupportDirectory()
          : null;
      _profiles =
          widget.profileStore ??
          OmadaProfileStore(
            file: File(
              p.join(support!.path, 'massar-center', 'omada-controller.enc'),
            ),
          );
      final saved = await _profiles!.load();
      if (!mounted) return;
      if (saved != null) {
        _address.text = saved['address'] as String? ?? _address.text;
        _omadaId.text = saved['omadaId'] as String? ?? '';
        _clientId.text = saved['clientId'] as String? ?? '';
        _secret.text = saved['clientSecret'] as String? ?? '';
        _certificatePin = saved['certificateSha256'] as String?;
        _staffName.text = saved['staffName'] as String? ?? 'MASSAR-STAFF';
        _pending = saved['pending'] is Map
            ? Map<String, dynamic>.from(saved['pending'] as Map)
            : null;
        _lastConfirmed = saved['confirmed'] is Map
            ? Map<String, dynamic>.from(saved['confirmed'] as Map)
            : null;
      }
    } on Object {
      if (mounted) {
        _message = 'تعذر قراءة الربط المحفوظ. أدخل بيانات إدارة Omada مجددًا.';
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Map<String, dynamic> _profile() => {
    'address': _address.text.trim(),
    'omadaId': _omadaId.text.trim(),
    'clientId': _clientId.text.trim(),
    'clientSecret': _secret.text,
    'certificateSha256': _certificatePin,
    'staffName': _staffName.text.trim(),
    if (_pending != null) 'pending': _pending,
    if (_lastConfirmed != null) 'confirmed': _lastConfirmed,
  };

  OmadaControllerConfig _config() {
    final uri = Uri.tryParse(_address.text.trim());
    if (uri == null) throw const CenterException('اكتب عنوان إدارة Omada.');
    validateOmadaControllerUri(uri);
    return OmadaControllerConfig(
      baseUri: uri,
      omadaId: _omadaId.text.trim(),
      clientId: _clientId.text.trim(),
      clientSecret: _secret.text,
      certificateSha256: _certificatePin,
    );
  }

  Future<void> _run(Future<void> Function() action) async {
    if (_disabled) return;
    setState(() {
      _busy = true;
      _message = null;
      _success = false;
    });
    try {
      await action();
    } catch (error) {
      if (mounted) {
        setState(
          () => _message = error is CenterException
              ? error.message
              : 'تعذر إكمال تجهيز Omada. افحص اتصال الإدارة ثم حاول مجددًا.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _connectionChanged(String _) {
    _generation++;
    _service?.close();
    _service = null;
    setState(() {
      _sites = [];
      _accessPoints = [];
      _wlans = [];
      _ssids = [];
      _site = _wlan = _student = _accessPoint = null;
      _preview = null;
      _certificatePin = null;
      _message = null;
    });
  }

  Future<void> _connect() => _run(() async {
    _service?.close();
    _service = widget.serviceFactory(_config());
    final sites = await _service!.listSites();
    if (!mounted) return;
    setState(() {
      _sites = sites;
      _accessPoints = [];
      _wlans = [];
      _ssids = [];
      _site = _wlan = _student = _accessPoint = null;
      _preview = null;
      _message = sites.isEmpty
          ? 'الإدارة متصلة، لكن لا يوجد موقع متاح لهذا الربط. راجع صلاحيات الربط في Omada.'
          : 'تم الاتصال. اختَر الموقع الذي يحتوي على جهاز Omada وشبكة الطلاب الحالية.';
    });
    await _profiles!.save(_profile());
  });

  Future<void> _trustCertificate() => _run(() async {
    final config = _config();
    final probe = widget.serviceFactory(config);
    try {
      final certificate = await probe.probeCertificate(config.baseUri);
      if (!mounted) return;
      final accepted = await showDialog<bool>(
        context: context,
        builder: (context) => OmadaCertificateDialog(
          address: config.baseUri.toString(),
          certificate: certificate,
        ),
      );
      if (!mounted || accepted != true) return;
      setState(() {
        _certificatePin = certificate.sha256;
        _message = 'تم اختيار شهادة الإدارة. اضغط «قراءة الشبكات» للاتصال.';
      });
    } finally {
      probe.close();
    }
  });

  Future<void> _chooseSite(String? id) => _run(() async {
    setState(() {
      _site = id;
      _wlan = _student = _accessPoint = null;
      _preview = null;
      _wlans = [];
      _ssids = [];
      _accessPoints = [];
    });
    if (id == null) return;
    final points = await _service!.listAccessPoints(id);
    final wlans = await _service!.listWlanGroups(id);
    if (!mounted) return;
    setState(() {
      _accessPoints = points;
      _wlans = wlans;
    });
  });

  Future<void> _chooseWlan(String? id) => _run(() async {
    setState(() {
      _wlan = id;
      _student = null;
      _preview = null;
      _ssids = [];
    });
    if (id == null || _site == null) return;
    final networks = await _service!.listSsids(_site!, id);
    if (!mounted) return;
    setState(() {
      _ssids = networks;
      if (networks.isEmpty) {
        _message =
            'لا توجد شبكة طلاب محفوظة في هذه المجموعة. اختَر المجموعة الحالية؛ لن ننشئ بديلًا لشبكة الطلاب.';
      }
    });
  });

  Future<void> _prepare() => _run(() async {
    if (_site == null ||
        _wlan == null ||
        _student == null ||
        _accessPoint == null) {
      return;
    }
    final preview = await _service!.previewStaffWifi(
      siteId: _site!,
      wlanId: _wlan!,
      studentSsidId: _student!,
      accessPointMac: _accessPoint!,
      staffName: _staffName.text.trim(),
      staffPassword: _staffPassword.text,
    );
    if (!mounted) return;
    setState(() => _preview = preview);
  });

  Future<void> _create() => _run(() async {
    final preview = _preview;
    if (preview == null || _pending != null || !widget.networkReady) return;
    final generation = _generation;
    final guard = OmadaExamNetworkGuard(widget.controller);
    final service = _service!;
    final result = await guard.run(
      (checkLease) => service.applyStaffWifi(
        preview,
        beforeWrite: () async {
          await checkLease();
          if (!mounted || generation != _generation) {
            throw const CenterException(
              'تغيّرت شاشة الربط؛ لم يُرسل طلب إنشاء الشبكة.',
            );
          }
          _pending = {
            'address': _address.text.trim(),
            'omadaId': _omadaId.text.trim(),
            'siteId': preview.siteId,
            'wlanId': preview.wlanId,
            'accessPointMac': preview.accessPointMac,
            'staffName': preview.staffName,
            'studentSsidId': _student,
          };
          try {
            await _profiles!.save(_profile());
          } on Object {
            // No vendor request can be sent until its recovery marker is durable.
            _pending = null;
            rethrow;
          }
          await checkLease();
        },
      ),
    );
    _lastConfirmed = {
      'address': _address.text.trim(),
      'omadaId': _omadaId.text.trim(),
      'siteId': preview.siteId,
      'wlanId': preview.wlanId,
      'accessPointMac': preview.accessPointMac,
      'staffName': result.ssid.name,
    };
    final durablePending = _pending;
    _pending = null;
    try {
      await _profiles!.save(_profile());
    } on Object {
      // The controller may have committed the SSID; preserve the retry barrier.
      _pending = durablePending;
      rethrow;
    }
    if (!mounted) return;
    setState(() {
      _success = true;
      _preview = null;
      _staffPassword.clear();
      _message =
          'حُفظت شبكة ${result.ssid.name} في إدارة Omada، وتأكد بقاء إعدادات الشبكات السابقة كما هي. '
          'وصّل الهوست بها واختبر التسجيل من الجهازين، ثم جرّب دخول طالب.'
          '${guard.releaseWarning == null ? '' : '\n${guard.releaseWarning}'}';
    });
  });

  Future<void> _checkPending() => _run(() async {
    final pending = _pending;
    if (pending == null) return;
    if (_address.text.trim() != pending['address'] ||
        _omadaId.text.trim() != pending['omadaId']) {
      throw const CenterException(
        'ارجع إلى عنوان ومعرّف الإدارة المستخدمين وقت الإرسال لمراجعة النتيجة.',
      );
    }
    _service ??= widget.serviceFactory(_config());
    final networks = await _service!.listSsids(
      pending['siteId'] as String,
      pending['wlanId'] as String,
    );
    final found = networks.where((row) => row.name == pending['staffName']);
    if (!mounted) return;
    setState(
      () => _message = found.length == 1
          ? 'شبكة ${pending['staffName']} موجودة الآن. اختبر اتصال الهوست بكلمة السر التي أدخلتها، وراجع إعداداتها في إدارة Omada. لم يُرسل طلب إنشاء آخر.'
          : 'لم تتأكد نتيجة الطلب السابق. افتح إدارة Omada وراجع الشبكة؛ إعادة الإرسال ممنوعة حتى لا تنشأ شبكة مكررة.',
    );
  });

  @override
  void dispose() {
    _generation++;
    _service?.close();
    for (final field in [
      _address,
      _omadaId,
      _clientId,
      _secret,
      _staffName,
      _staffPassword,
    ]) {
      field.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final colors = MassarPalette.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('شبكتا Omada', style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 8),
        const Text(
          'شبكة الطلاب تفضل باسمها وكلمة سرها الحالية. نضيف شبكة الموظفين على نفس الأكسس بوينت ليتصل بها الهوست، والساكند يظل موصلًا بالكابل.',
        ),
        const SizedBox(height: 12),
        OmadaFirstSetupHelp(
          disabled: _disabled,
          onOpen: (uri) => _run(() => OmadaSetupLinks.open(uri)),
          controllerAddress: _address.text.trim(),
        ),
        const SizedBox(height: 12),
        ExpansionTile(
          initiallyExpanded: _sites.isEmpty,
          tilePadding: EdgeInsets.zero,
          title: const Text('ربط إدارة Omada لأول مرة'),
          subtitle: Text(
            _certificatePin == null
                ? 'بيانات الربط تُحفظ مشفّرة لهذا المستخدم على ويندوز'
                : 'شهادة إدارة محددة لهذا العنوان',
          ),
          children: [
            OmadaConnectionForm(
              address: _address,
              omadaId: _omadaId,
              clientId: _clientId,
              secret: _secret,
              disabled: _disabled,
              onChanged: _connectionChanged,
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 10,
              runSpacing: 8,
              children: [
                FilledButton.icon(
                  onPressed: _disabled ? null : _connect,
                  icon: const Icon(Icons.router_outlined),
                  label: const Text('قراءة الشبكات'),
                ),
                OutlinedButton(
                  onPressed: _disabled ? null : _trustCertificate,
                  child: const Text('فحص شهادة الإدارة المحلية'),
                ),
              ],
            ),
            const SizedBox(height: 12),
          ],
        ),
        if (_busy) const LinearProgressIndicator(),
        if (_message != null)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 12),
            child: Semantics(
              liveRegion: true,
              child: Text(
                _message!,
                style: TextStyle(color: _success ? colors.success : colors.ink),
              ),
            ),
          ),
        if (_pending != null) ...[
          Text(
            'طلب سابق يحتاج مراجعة: ${_pending!['staffName']}',
            style: TextStyle(
              color: colors.warning,
              fontWeight: FontWeight.bold,
            ),
          ),
          const Text(
            'قد يكون الطلب وصل للإدارة رغم انقطاع الرد. نقرأ النتيجة فقط لحماية الشبكة من التكرار.',
          ),
          OutlinedButton(
            onPressed: _disabled ? null : _checkPending,
            child: const Text('فحص نتيجة الطلب السابق'),
          ),
        ],
        if (_sites.isNotEmpty) ...[
          const SizedBox(height: 12),
          OmadaNetworkChoices(
            disabled: _disabled,
            sites: _sites,
            wlans: _wlans,
            networks: _ssids,
            accessPoints: _accessPoints,
            site: _site,
            wlan: _wlan,
            student: _student,
            accessPoint: _accessPoint,
            onAccessPoint: (mac) => setState(() {
              _accessPoint = mac;
              _preview = null;
            }),
            onSite: _chooseSite,
            onWlan: _chooseWlan,
            onStudent: (id) => setState(() {
              _student = id;
              _preview = null;
            }),
          ),
        ],
        if (_student != null && _accessPoint != null && _pending == null) ...[
          const SizedBox(height: 18),
          TextField(
            controller: _staffName,
            enabled: !_disabled,
            textDirection: TextDirection.ltr,
            onChanged: (_) => setState(() => _preview = null),
            decoration: const InputDecoration(
              labelText: 'اسم شبكة الموظفين الجديدة',
            ),
          ),
          const SizedBox(height: 10),
          TextField(
            controller: _staffPassword,
            enabled: !_disabled,
            obscureText: true,
            enableSuggestions: false,
            autocorrect: false,
            textDirection: TextDirection.ltr,
            onChanged: (_) => setState(() => _preview = null),
            decoration: const InputDecoration(
              labelText: 'كلمة سر شبكة الموظفين',
              helperText: 'من ٨ إلى ٦٣ حرفًا إنجليزيًا أو رقمًا أو رمزًا',
            ),
          ),
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: _disabled ? null : _prepare,
            child: const Text('مراجعة التجهيز قبل الإضافة'),
          ),
        ],
        if (_preview != null) ...[
          const SizedBox(height: 12),
          Text(
            'ستُضاف: ${_preview!.staffName}',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          Text(
            'ستبقى الشبكات الحالية كما هي: ${_preview!.preservedSsids.map((row) => row.name).join('، ')}',
          ),
          const Text(
            'الاسمان على نفس الشبكة المحلية. اسم منفصل وكلمة سر مختلفة لا يعزلان أجهزة الطلاب وحدهما.',
          ),
          const SizedBox(height: 10),
          if (!widget.networkReady)
            const Text('جهّز وصلة كابل الامتحان وافحصها أولًا.'),
          if (widget.controller.hasLiveExam)
            const Text(
              'أغلق قاعات الامتحان قبل إضافة شبكة؛ التجهيز قد يعيد اتصال الواي فاي.',
            ),
          FilledButton.icon(
            onPressed: _canCreate ? _create : null,
            icon: const Icon(Icons.add_to_photos_outlined),
            label: const Text('إضافة شبكة الموظفين وحفظ شبكة الطلاب'),
          ),
        ],
        if (_lastConfirmed != null && _message == null)
          Padding(
            padding: const EdgeInsets.only(top: 12),
            child: Text(
              'آخر شبكة حُفظت في الإدارة: ${_lastConfirmed!['staffName']}. فحص الاتصال الفعلي من الهوست مطلوب.',
            ),
          ),
      ],
    );
  }
}
