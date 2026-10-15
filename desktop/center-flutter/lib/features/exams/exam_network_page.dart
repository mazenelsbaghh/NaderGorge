import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../exams/exam_room_controller.dart';
import '../../domain/models.dart';
import '../../shared/theme.dart';
import '../management/management_widgets.dart';
import 'omada_setup_panel.dart';

class ExamNetworkPage extends StatefulWidget {
  const ExamNetworkPage({
    super.key,
    required this.controller,
    required this.isHost,
  });
  final ExamRoomController controller;
  final bool isHost;
  @override
  State<ExamNetworkPage> createState() => _ExamNetworkPageState();
}

class _ExamNetworkPageState extends State<ExamNetworkPage> {
  bool _busy = false;
  Map<String, dynamic> _status = const {};
  Map<String, dynamic> _hotspot = const {};
  List<Map<String, dynamic>> _adapters = const [];
  String? _adapter;
  String? _message;
  String _portalMode = 'legacy';
  final _hostname = TextEditingController();
  String? _certificate, _key;

  @override
  void initState() {
    super.initState();
    _inspect();
  }

  @override
  void dispose() {
    _hostname.dispose();
    super.dispose();
  }

  Future<void> _run(Future<void> Function() action) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _message = null;
    });
    try {
      await action();
    } catch (error) {
      if (mounted) {
        setState(
          () => _message = error is CenterException
              ? error.message
              : 'تعذر إكمال تهيئة الشبكة. افحص اتصال الكابل ثم أعد الفحص.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _read() async {
    final status = await widget.controller.network({'action': 'status'});
    final list = await widget.controller.network({'action': 'list-adapters'});
    final hotspot = widget.isHost
        ? await widget.controller.network({'action': 'tethering-status'})
        : const <String, dynamic>{};
    if (!mounted) return;
    final adapters = (list['adapters'] as List? ?? [])
        .whereType<Map>()
        .map((row) => Map<String, dynamic>.from(row))
        .toList();
    setState(() {
      _status = status;
      _hotspot = hotspot;
      _adapters = adapters;
      _adapter ??= status['adapterId'] as String?;
      if (!_adapters.any((adapter) => adapter['id'] == _adapter)) {
        _adapter = null;
      }
      _portalMode = status['portalMode'] as String? ?? 'legacy';
    });
  }

  Future<void> _inspect() => _run(_read);
  Future<void> _startHostNetwork() => _run(() async {
    final result = await widget.controller.network({
      'action': 'tethering-start',
    });
    if (!mounted) return;
    setState(() => _hotspot = result);
    if (result['ok'] != true || result['active'] != true) {
      throw CenterException(
        result['message'] as String? ?? 'لم يؤكد ويندوز تشغيل شبكة الهوست.',
      );
    }
    setState(() => _message = result['message'] as String?);
  });
  Future<void> _apply(Map<String, dynamic> command) => _run(() async {
    if (command['action'] == 'configure' ||
        command['action'] == 'restore' ||
        command['action'] == 'portal-configure') {
      await widget.controller.prepareNetworkEngine();
    }
    final result = await widget.controller.network(command);
    if (result['ok'] == false || result['error'] != null) {
      throw CenterException(
        result['message'] as String? ?? 'لم تكتمل التهيئة.',
      );
    }
    await _read();
    if (mounted) {
      setState(
        () => _message =
            result['message'] as String? ??
            'تم تنفيذ الطلب. راجع حالة الفحص أدناه.',
      );
    }
  });

  Future<void> _pickCertificate(bool privateKey) async {
    final file = await openFile(
      acceptedTypeGroups: [
        XTypeGroup(
          label: privateKey ? 'المفتاح الخاص' : 'شهادة البوابة',
          extensions: privateKey ? ['pem', 'key'] : ['pem', 'crt'],
        ),
      ],
    );
    if (file != null && mounted) {
      setState(() {
        if (privateKey) {
          _key = file.path;
        } else {
          _certificate = file.path;
        }
      });
    }
  }

  Widget _check(String title, Object? ready) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Row(
      children: [
        Icon(
          ready == true ? Icons.check_circle_outline : Icons.info_outline,
          size: 20,
          color: ready == true
              ? MassarPalette.of(context).success
              : MassarPalette.of(context).muted,
        ),
        const SizedBox(width: 10),
        Expanded(child: Text(title)),
        Text(ready == true ? 'جاهز' : 'لم يتأكد بعد'),
      ],
    ),
  );

  @override
  Widget build(BuildContext context) => ManagementPanel(
    title: 'تهيئة الشبكة وصفحة دخول الطلاب',
    subtitle: widget.isHost
        ? 'شغّل شبكة الهوست لربط الساكند وواجب الموبايل. تجهيز كابل الـOmada يتم على الساكند.'
        : 'وصّل الواي فاي بالهوست لتحميل دليل الحصص ورفع النتائج، والكابل بالـOmada. الامتحان يعمل محليًا عند فصل الهوست.',
    actions: [
      OutlinedButton.icon(
        onPressed: _busy ? null : _inspect,
        icon: const Icon(Icons.refresh),
        label: const Text('فحص الشبكة'),
      ),
    ],
    child: ListView(
      padding: const EdgeInsets.only(top: 8),
      children: [
        if (_busy) const LinearProgressIndicator(),
        if (_message != null)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 12),
            child: Text(
              _message!,
              style: TextStyle(color: MassarPalette.of(context).accent),
            ),
          ),
        if (_status['message'] is String) Text(_status['message'] as String),
        if (widget.isHost) ...[
          const SizedBox(height: 16),
          Text('شبكة الهوست', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 8),
          const Text(
            'يمكن البدء من اتصال الواي فاي على الهوست دون كابل بين الجهازين، إذا كان ويندوز ومحول الواي فاي يدعمان نقطة الاتصال. الشبكة المشغّلة بالفعل تبقى كما هي.',
          ),
          const SizedBox(height: 12),
          Align(
            alignment: AlignmentDirectional.centerStart,
            child: FilledButton.icon(
              onPressed: _busy ? null : _startHostNetwork,
              icon: const Icon(Icons.wifi_tethering),
              label: const Text('تشغيل شبكة الهوست'),
            ),
          ),
          _check('هوتسبوت الهوست يعمل', _hotspot['active']),
          if (_hotspot['message'] is String)
            Text(_hotspot['message'] as String),
          if (_hotspot['active'] == true && _hotspot['ssid'] is String)
            SelectableText('اسم الشبكة: ${_hotspot['ssid']}'),
          const Text(
            'اسم الشبكة وكلمة سرها في إعدادات ويندوز ← الشبكة والإنترنت ← نقطة اتصال الهواتف المحمولة. وصّل الساكند بها من قائمة الواي فاي، ثم افتح ربط الأجهزة. لا تطفئ الشبكة أثناء التسجيل أو واجب الموبايل.',
          ),
          const Divider(height: 32),
        ],
        const SizedBox(height: 16),
        const Text(
          '١. أول اتصال: اربط الساكند بالهوست بالواي فاي أو الكابل وسجّل دخول السنتر لتحميل المجموعات والحصص والامتحانات تلقائيًا.',
        ),
        const SizedBox(height: 8),
        const Text(
          '٢. افتح امتحان الساكند واختر نموذج الأسئلة والمجموعة والحصة وامتحان الرصد من الدليل المحفوظ. لا يحتاج الامتحان كشف طلاب أو تجهيزًا مسبقًا للحصة.',
        ),
        const SizedBox(height: 8),
        const Text(
          '٣. داخل القاعة: وصّل الـOmada بالساكند على منفذ الكابل المجهّز، والطلاب يتصلون بشبكة Mr Nader Center exam الحالية.',
        ),
        const SizedBox(height: 8),
        const Text(
          '٤. بعد التصحيح أعد الاتصال بالهوست. النتائج تصل إلى شاشة تحميل درجات الساكند؛ تأكيد واحد يرصد المطابقين ويعرض الحاضرين الذين لم يدخلوا الامتحان وشيتًا لتنزيلهم.',
        ),
        const SizedBox(height: 8),
        const Text(
          'واجب الموبايل: من الهوست افتح واجب الموبايل واضغط تشغيل هوتسبوت الواجب، ثم وصّل موبايل الموظف به وجهّز الرابط.',
        ),
        const SizedBox(height: 16),
        Text(
          'وصلة شبكة الامتحان',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 8),
        const Text(
          'على الساكند: اختَر منفذ الكابل الذي سيتصل بالـOmada داخل القاعة. يمكن استخدام نفس المنفذ لكابل الهوست قبل القاعة وبعدها. لا تختَر منفذ الإنترنت، وجهّزه بعد إغلاق القاعات.',
        ),
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          key: ValueKey(_adapter),
          initialValue: _adapter,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'منفذ كابل الـOmada'),
          items: _adapters
              .where((adapter) => adapter['wireless'] != true)
              .map(
                (adapter) => DropdownMenuItem(
                  value: adapter['id'] as String,
                  child: Text(
                    '${adapter['name']} • ${adapter['status']}',
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              )
              .toList(),
          onChanged: _busy ? null : (value) => setState(() => _adapter = value),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 10,
          runSpacing: 8,
          children: [
            FilledButton.icon(
              onPressed: _busy || _adapter == null || widget.isHost
                  ? null
                  : () =>
                        _apply({'action': 'configure', 'adapterId': _adapter}),
              icon: const Icon(Icons.settings_ethernet),
              label: const Text('تجهيز وصلة الامتحان'),
            ),
            OutlinedButton(
              onPressed: _busy || _status['configured'] != true
                  ? null
                  : () => _apply({'action': 'restore'}),
              child: const Text('استعادة إعداد الوصلة السابق'),
            ),
          ],
        ),
        const SizedBox(height: 12),
        if (widget.isHost)
          const Text(
            'تجهيز وصلة الامتحان يتم على الساكند فقط حتى لا يأخذ الجهازان العنوان نفسه.',
          ),
        _check('توزيع عناوين الموبايلات', _status['dhcpReady']),
        _check('توجيه صفحة الدخول', _status['dnsReady']),
        _check('البورتال وخدمة الامتحان', _status['portalReady']),
        if (_status['studentUrl'] is String)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('رابط دخول الطلاب'),
            subtitle: SelectableText(
              _status['studentUrl'] as String,
              textDirection: TextDirection.ltr,
            ),
            trailing: IconButton(
              tooltip: 'نسخ رابط الدخول',
              onPressed: () => Clipboard.setData(
                ClipboardData(text: _status['studentUrl'] as String),
              ),
              icon: const Icon(Icons.copy),
            ),
          ),
        const Divider(height: 32),
        ExpansionTile(
          title: const Text('إعدادات Omada الإضافية (اختياري)'),
          subtitle: const Text(
            'إذا كانت شبكة الطلاب تعمل بالفعل، استخدمها مباشرة مع تجهيز الوصلة. إدارة Omada مطلوبة لتغيير إعدادات الأكسس بوينت.',
          ),
          children: [
            OmadaSetupPanel(
              controller: widget.controller,
              networkReady: _status['ready'] == true,
              disabled: _busy,
            ),
          ],
        ),
        const Divider(height: 32),
        Text(
          'البورتال — طريقة دخول الموبايلات',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 8),
        const Text(
          'الطالب يكتب اسمه ورقم تلفونه وكوده، ثم يدخل الانتظار والامتحان. لو صفحة الدخول التلقائي لم تظهر، افتح رابط الطلاب يدويًا.',
        ),
        const SizedBox(height: 12),
        DropdownButtonFormField<String>(
          key: ValueKey('portal-$_portalMode'),
          initialValue: _portalMode,
          decoration: const InputDecoration(labelText: 'طريقة البوابة'),
          items: const [
            DropdownMenuItem(value: 'legacy', child: Text('دخول محلي HTTP')),
            DropdownMenuItem(
              value: 'https',
              child: Text('HTTPS بشهادة موثوقة'),
            ),
          ],
          onChanged: _busy
              ? null
              : (value) => setState(() => _portalMode = value!),
        ),
        if (_portalMode == 'https') ...[
          const SizedBox(height: 12),
          TextField(
            controller: _hostname,
            onChanged: (_) => setState(() {}),
            enabled: !_busy,
            decoration: const InputDecoration(
              labelText: 'اسم النطاق الموجود في الشهادة',
            ),
            textDirection: TextDirection.ltr,
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 10,
            children: [
              OutlinedButton.icon(
                onPressed: _busy ? null : () => _pickCertificate(false),
                icon: Icon(
                  _certificate == null ? Icons.upload_file : Icons.check,
                ),
                label: const Text('اختيار الشهادة'),
              ),
              OutlinedButton.icon(
                onPressed: _busy ? null : () => _pickCertificate(true),
                icon: Icon(_key == null ? Icons.key : Icons.check),
                label: const Text('اختيار المفتاح الخاص'),
              ),
            ],
          ),
        ],
        const SizedBox(height: 12),
        Align(
          alignment: AlignmentDirectional.centerStart,
          child: FilledButton(
            onPressed:
                _busy ||
                    _status['configured'] != true ||
                    (_portalMode == 'https' &&
                        (_certificate == null ||
                            _key == null ||
                            _hostname.text.trim().isEmpty))
                ? null
                : () => _apply({
                    'action': 'portal-configure',
                    'mode': _portalMode,
                    if (_portalMode == 'https') ...{
                      'hostname': _hostname.text.trim(),
                      'certificatePath': _certificate,
                      'privateKeyPath': _key,
                    },
                  }),
            child: const Text('تطبيق إعداد البورتال'),
          ),
        ),
        const SizedBox(height: 16),
      ],
    ),
  );
}
