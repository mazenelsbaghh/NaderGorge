import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../exams/exam_room_controller.dart';
import '../../domain/models.dart';
import '../../shared/theme.dart';
import '../management/management_widgets.dart';

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
  final _ssid = TextEditingController(text: 'Massar-Host');
  final _password = TextEditingController();
  final _hostname = TextEditingController();
  String? _certificate, _key;

  @override
  void initState() {
    super.initState();
    _inspect();
  }

  @override
  void dispose() {
    _ssid.dispose();
    _password.dispose();
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
    final hotspot = await widget.controller.network({
      'action': 'hotspot-status',
    });
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
  Future<void> _apply(Map<String, dynamic> command) => _run(() async {
    if (command['action'] == 'configure' ||
        command['action'] == 'restore' ||
        command['action'] == 'portal-configure') {
      await widget.controller.start();
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
        ? 'هذا هو الجهاز الرئيسي. شبكة الربط الخاصة تصل الساكند ببيانات السنتر.'
        : 'وصلة الكابل للـOmada، والواي فاي للاتصال بالهوست.',
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
        const SizedBox(height: 16),
        if (widget.isHost) ...[
          Text(
            'شبكة الهوست الخاصة',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 8),
          const Text(
            'الساكند يتصل بهذه الشبكة من إعدادات الواي فاي في ويندوز، ثم تربطه من «ربط الأجهزة». الطلاب يتصلون بشبكة الـOmada.',
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _ssid,
            enabled: !_busy,
            decoration: const InputDecoration(labelText: 'اسم شبكة الجهازين'),
            textDirection: TextDirection.ltr,
          ),
          const SizedBox(height: 10),
          TextField(
            controller: _password,
            enabled: !_busy,
            obscureText: true,
            decoration: const InputDecoration(
              labelText: 'كلمة مرور الشبكة — ٨ أحرف على الأقل',
            ),
            textDirection: TextDirection.ltr,
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 10,
            runSpacing: 8,
            children: [
              FilledButton.icon(
                onPressed: _busy
                    ? null
                    : () {
                        if (_password.text.length < 8 ||
                            _password.text.length > 63 ||
                            _ssid.text.trim().isEmpty) {
                          setState(
                            () => _message =
                                'اكتب اسم الشبكة وكلمة مرور بين ٨ و٦٣ حرفًا.',
                          );
                          return;
                        }
                        _apply({
                          'action': 'hotspot-start',
                          'ssid': _ssid.text.trim(),
                          'password': _password.text,
                        });
                      },
                icon: const Icon(Icons.wifi),
                label: const Text('تشغيل شبكة الهوست'),
              ),
              OutlinedButton(
                onPressed: _busy || _hotspot['active'] != true
                    ? null
                    : () async {
                        final confirmed = await showDialog<bool>(
                          context: context,
                          builder: (context) => AlertDialog(
                            title: const Text('إيقاف شبكة الجهازين؟'),
                            content: const Text(
                              'سيتوقف التحضير والتحصيل على الساكند حتى رجوع الاتصال. أغلق أي عمل جارٍ عليه أولًا.',
                            ),
                            actions: [
                              TextButton(
                                onPressed: () => Navigator.pop(context, false),
                                child: const Text('رجوع'),
                              ),
                              FilledButton(
                                onPressed: () => Navigator.pop(context, true),
                                child: const Text('إيقاف الشبكة'),
                              ),
                            ],
                          ),
                        );
                        if (confirmed == true) {
                          await _apply({'action': 'hotspot-stop'});
                        }
                      },
                child: const Text('إيقاف الشبكة'),
              ),
            ],
          ),
          _check('شبكة الربط اللاسلكي', _hotspot['active']),
          if (_hotspot['addresses'] is List &&
              (_hotspot['addresses'] as List).isNotEmpty)
            SelectableText(
              'عنوان الهوست للربط اليدوي: ${(_hotspot['addresses'] as List).join('، ')}',
            ),
          if (_hotspot['message'] is String)
            Text(_hotspot['message'] as String),
          const Divider(height: 32),
        ],
        if (!widget.isHost) ...[
          Text(
            'وصلة شبكة الامتحان',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 8),
          const Text(
            'اختَر منفذ الكابل الموصل بالـOmada فقط. لا تختَر وصلة الإنترنت أو شبكة الربط بين الجهازين. تغيير الإعداد متاح بعد إغلاق القاعات.',
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
            onChanged: _busy
                ? null
                : (value) => setState(() => _adapter = value),
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 10,
            runSpacing: 8,
            children: [
              FilledButton.icon(
                onPressed: _busy || _adapter == null
                    ? null
                    : () => _apply({
                        'action': 'configure',
                        'adapterId': _adapter,
                      }),
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
          Text(
            'البورتال — طريقة دخول الموبايلات',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 8),
          const Text(
            'صفحة الطالب تعرض إدخال الموبايل، ثم الاسم والكود، وقاعة الانتظار والامتحان. لو صفحة الدخول التلقائي لم تظهر، افتح رابط الطلاب يدويًا.',
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
      ],
    ),
  );
}
