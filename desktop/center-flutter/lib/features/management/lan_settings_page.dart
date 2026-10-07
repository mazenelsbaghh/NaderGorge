import 'dart:convert';
import 'dart:io';

import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../domain/models.dart';
import '../../lan/lan_controller.dart';
import '../../lan/lan_settings.dart';
import '../../lan/lan_transport.dart';
import '../../shared/problem_reporting.dart';
import '../../shared/problem_log.dart';
import '../../shared/scrollable_dialog.dart';
import '../../shared/theme.dart';
import '../../shared/workspace_draft_guard.dart';
import 'management_widgets.dart';

class LanSettingsPage extends StatefulWidget {
  const LanSettingsPage({super.key, required this.controller});
  final LanController controller;

  @override
  State<LanSettingsPage> createState() => _LanSettingsPageState();
}

class _LanSettingsPageState extends State<LanSettingsPage> {
  late final _name = TextEditingController(
    text: controller.configuration?.deviceName ?? '',
  );
  final _directCode = TextEditingController();
  final _codeFocus = FocusNode();
  final _codeForm = GlobalKey<FormState>();
  LanEndpoint? _selectedEndpoint;
  bool _identityVisible = false;
  bool _autoDiscoveryRequested = false, _discovering = false;
  bool _busy = false, _changingContext = false;
  int _hostSelectionRevision = 0;
  String? _operationError;
  late String _savedName = controller.configuration?.deviceName ?? '';
  bool get _dirty =>
      (!controller.isHost && _name.text != _savedName) ||
      (controller.configuration?.deviceToken == null &&
          _directCode.text.isNotEmpty);
  bool _discoveryFailed = false, _discoverySocketFailure = false;
  LanMode? _choice;
  LanController get controller => widget.controller;

  @override
  void initState() {
    super.initState();
    _codeFocus.onKeyEvent = (_, event) {
      if (event.logicalKey != LogicalKeyboardKey.enter &&
          event.logicalKey != LogicalKeyboardKey.numpadEnter) {
        return KeyEventResult.ignored;
      }
      final keyboard = HardwareKeyboard.instance;
      if (event is KeyDownEvent &&
          !keyboard.isControlPressed &&
          !keyboard.isAltPressed &&
          !keyboard.isMetaPressed &&
          !keyboard.isShiftPressed) {
        _connectByCode();
      }
      return KeyEventResult.handled;
    };
    controller.addListener(_configurationChanged);
    _configurationChanged();
  }

  void _configurationChanged() {
    if (!mounted) return;
    final config = controller.configuration;
    if (config == null || controller.isBusy) return;
    if (_name.text == _savedName) {
      _savedName = config.deviceName;
      _name.text = _savedName;
    }
    if (config.deviceToken != null &&
        controller.status == LanConnectionStatus.connected) {
      _directCode.clear();
    }
    if (config.deviceToken != null) {
      if (_selectedEndpoint == null && config.endpoint != null) {
        _selectEndpoint(config.endpoint!);
      }
      return;
    }
    if (_autoDiscoveryRequested ||
        !(controller.isClientOnly ||
            _choice == LanMode.client ||
            config.mode == LanMode.client)) {
      return;
    }
    _autoDiscoveryRequested = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted &&
          !_busy &&
          !controller.isBusy &&
          ModalRoute.of(context)?.isCurrent == true) {
        _discover();
      }
    });
  }

  @override
  void dispose() {
    controller.removeListener(_configurationChanged);
    _directCode.dispose();
    _codeFocus.dispose();
    _name.dispose();
    super.dispose();
  }

  Future<void> _run(
    Future<void> Function() action, {
    String? success,
    String? failureMessage,
  }) async {
    if (_busy || controller.isBusy) return;
    setState(() {
      _busy = true;
      _operationError = null;
    });
    try {
      await action();
      if (mounted && success != null) {
        await showManagementMessage(context, success);
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.lan_settings_page');
      if (mounted) {
        setState(
          () => _operationError = error is CenterException
              ? error.message
              : failureMessage ??
                    'تعذر إتمام ربط الأجهزة. راجع الاتصال وحاول مرة أخرى.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _standalone() async {
    if (controller.configuration?.mode == LanMode.standalone) return;
    final confirm = await confirmManagement(
      context,
      title: 'العودة لبيانات هذا الجهاز؟',
      description:
          'ستخرج من حساب الموظف وتفتح بيانات هذا الجهاز المحلية. بيانات الجهاز الرئيسي وبيانات هذا الجهاز تظل منفصلة؛ لا يحدث دمج أو نقل تلقائي.',
      confirmLabel: 'العودة للوضع المستقل',
    );
    if (confirm && mounted) await _run(controller.useStandalone);
  }

  Future<void> _pair(LanEndpoint endpoint) async {
    if (_busy || controller.isBusy) return;
    final code = TextEditingController();
    final form = GlobalKey<FormState>();
    var accepting = false, submitted = false, accepted = false;
    String? errorMessage;
    setState(() => _busy = true);
    try {
      final route = DialogRoute<bool>(
        context: context,
        barrierDismissible: false,
        builder: (context) => StatefulBuilder(
          builder: (context, update) => WorkspaceDraftRegistration(
            dirty: !accepted && code.text.isNotEmpty,
            busy: accepting,
            child: ScrollableMassarDialog(
              key: const Key('lan-pair-dialog'),
              title: const Text('الخطوة ٢: كود الربط'),
              width: 560,
              content: Form(
                key: form,
                autovalidateMode: submitted
                    ? AutovalidateMode.onUserInteraction
                    : AutovalidateMode.disabled,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (errorMessage != null)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: Semantics(
                          liveRegion: true,
                          child: Text(
                            'لم يتم الربط. $errorMessage\nالكود موجود؛ صحح السبب وحاول مجددًا.',
                            style: TextStyle(
                              color: Theme.of(context).colorScheme.error,
                            ),
                          ),
                        ),
                      ),
                    Text(
                      endpoint.name,
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    _fingerprint(endpoint.certificateSha256),
                    const SizedBox(height: 14),
                    const Text(
                      'تم تحديد الجهاز الرئيسي. قارن البصمة المعروضة عليه، ثم أدخل كود الربط المكوّن من ستة أرقام. الربط لا يسجل دخول الموظف؛ ستسجل دخولك بحساب من الجهاز الرئيسي.',
                    ),
                    const SizedBox(height: 12),
                    Text(
                      controller.isClientOnly
                          ? 'هذا الجهاز يستخدم بيانات الرئيسي فقط. عند انقطاع الاتصال لا يتم تسجيل دفعات أو عمليات محلية.'
                          : 'أي بيانات موجودة على هذا الجهاز تظل محفوظة ومنفصلة، ولن تُدمج ببيانات الجهاز الرئيسي. عند انقطاع الاتصال لن تُسجل عمليات انتظار أو دفعات محلية.',
                    ),
                    const SizedBox(height: 20),
                    TextFormField(
                      key: const Key('lan-pair-code'),
                      controller: code,
                      enabled: !accepting,
                      onChanged: (_) => update(() => errorMessage = null),
                      autofocus: true,
                      keyboardType: TextInputType.number,
                      maxLength: 6,
                      decoration: const InputDecoration(
                        labelText: 'رمز الربط — ستة أرقام',
                      ),
                      validator: (value) =>
                          RegExp(r'^\d{6}$').hasMatch(_digits(value ?? ''))
                          ? null
                          : 'اكتب الستة أرقام الظاهرة على الجهاز الرئيسي.',
                    ),
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: accepting
                      ? null
                      : () => Navigator.maybePop(context),
                  child: const Text('رجوع'),
                ),
                FilledButton(
                  key: const Key('confirm-lan-pair'),
                  onPressed: accepting
                      ? null
                      : () async {
                          if (accepting || accepted || controller.isBusy) {
                            return;
                          }
                          update(() => submitted = true);
                          if (!form.currentState!.validate()) return;
                          final submittedCode = _digits(code.text);
                          update(() {
                            accepting = true;
                            errorMessage = null;
                          });
                          try {
                            await controller.pairHost(endpoint, submittedCode);
                            if (!context.mounted) return;
                            update(() {
                              accepting = false;
                              accepted = true;
                            });
                            await WidgetsBinding.instance.endOfFrame;
                            if (context.mounted) Navigator.pop(context, true);
                          } catch (error, stackTrace) {
                            reportProblem(
                              error,
                              stackTrace,
                              operation: 'ui.lan_settings_page',
                            );
                            if (context.mounted) {
                              update(() {
                                accepting = false;
                                errorMessage = error is CenterException
                                    ? error.message
                                    : 'تعذر الاتصال بالجهاز الرئيسي. راجع الشبكة ثم حاول مرة أخرى.';
                              });
                            }
                          }
                        },
                  child: Text(accepting ? 'جارٍ الربط…' : 'تأكيد الربط'),
                ),
              ],
            ),
          ),
        ),
      );
      await Navigator.of(context).push(route);
      await route.completed;
    } finally {
      code.dispose();
      if (mounted) setState(() => _busy = false);
    }
  }

  String _digits(String value) => value.trim().split('').map((character) {
    const arabic = '٠١٢٣٤٥٦٧٨٩', persian = '۰۱۲۳۴۵۶۷۸۹';
    final a = arabic.indexOf(character), p = persian.indexOf(character);
    return a >= 0
        ? '$a'
        : p >= 0
        ? '$p'
        : character;
  }).join();

  Future<void> _manual() async {
    if (_busy || controller.isBusy) return;
    final data = TextEditingController();
    final form = GlobalKey<FormState>();
    LanEndpoint? selected;
    var advancing = false;
    try {
      final route = DialogRoute<bool>(
        context: context,
        barrierDismissible: false,
        builder: (context) => StatefulBuilder(
          builder: (context, update) => WorkspaceDraftRegistration(
            dirty: !advancing && data.text.isNotEmpty,
            busy: false,
            child: ScrollableMassarDialog(
              key: const Key('lan-manual-dialog'),
              title: const Text('الخطوة ١: بيانات الجهاز الرئيسي'),
              content: Form(
                key: form,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text(
                      'على الجهاز الرئيسي افتح «ربط الأجهزة» واضغط «نسخ بيانات الربط». انقل النص المنسوخ كاملًا إلى هذا الجهاز والصقه هنا. البيانات تشمل عنوان الجهاز وبصمته؛ لا تكتب الستة أرقام هنا.',
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'بعد مراجعة البيانات ستفتح الخطوة ٢ لإدخال كود الربط المكوّن من ستة أرقام. لا تحتاج كلمة مرور الموظف أو بيانات الطلاب هنا.',
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      key: const Key('lan-manual-data'),
                      controller: data,
                      onChanged: (_) => update(() {}),
                      autofocus: true,
                      minLines: 3,
                      maxLines: 6,
                      textDirection: TextDirection.ltr,
                      decoration: const InputDecoration(
                        labelText: 'الصق بيانات الربط المنسوخة كاملة',
                        helperText:
                            'بيانات الجهاز هنا؛ كود الستة أرقام في الخطوة التالية.',
                        helperMaxLines: 2,
                        errorMaxLines: 4,
                      ),
                      validator: (value) {
                        selected = null;
                        if (RegExp(r'^\d{6}$').hasMatch(_digits(value ?? ''))) {
                          return 'هذه الستة أرقام هي كود الخطوة ٢. انسخ «بيانات الربط» من الجهاز الرئيسي والصق النص كاملًا هنا أولًا.';
                        }
                        try {
                          selected = LanEndpoint.fromJson(
                            jsonDecode(value ?? '') as Map<String, dynamic>,
                          );
                          return null;
                        } catch (_) {
                          return 'الصق بيانات الربط كاملة، بما فيها بصمة الجهاز.';
                        }
                      },
                    ),
                  ],
                ),
              ),
              actions: [
                TextButton(
                  onPressed: advancing
                      ? null
                      : () => Navigator.maybePop(context),
                  child: const Text('رجوع'),
                ),
                FilledButton(
                  onPressed: advancing
                      ? null
                      : () async {
                          if (advancing) return;
                          if (!form.currentState!.validate()) return;
                          update(() => advancing = true);
                          await WidgetsBinding.instance.endOfFrame;
                          if (context.mounted) Navigator.pop(context, true);
                        },
                  key: const Key('lan-manual-next'),
                  child: const Text('التالي: إدخال كود الربط'),
                ),
              ],
            ),
          ),
        ),
      );
      final accepted = await Navigator.of(context).push(route);
      await route.completed;
      if (accepted == true && selected != null && mounted) {
        await _pair(selected!);
      }
    } finally {
      data.dispose();
    }
  }

  Future<void> _copyHost() => _run(() async {
    final ready = controller.hostReady;
    if (!controller.canConfigureHost || ready == null) {
      throw const CenterException(
        'بيانات الربط متاحة لمدير السيرفر أثناء تشغيله.',
      );
    }
    final interfaces = await NetworkInterface.list(
      type: InternetAddressType.IPv4,
      includeLoopback: false,
    );
    final addresses = [
      for (final network in interfaces)
        for (final address in network.addresses)
          if (!address.isLoopback)
            (name: network.name, address: address.address),
    ];
    if (addresses.isEmpty) {
      throw const CenterException(
        'لم يظهر اتصال شبكة لهذا الجهاز. اتصل بالشبكة ثم حاول مرة أخرى.',
      );
    }
    if (!mounted) return;
    var index = 0;
    if (addresses.length > 1) {
      final selected = await showDialog<int>(
        context: context,
        builder: (context) => StatefulBuilder(
          builder: (context, update) => ScrollableMassarDialog(
            title: const Text('اختار شبكة الراوتر'),
            content: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'هذا الجهاز متصل بأكثر من شبكة. اختار اتصال الواي فاي أو الكابل المستخدم في السنتر، لتنسخ عنوانًا يصل إليه الجهاز الآخر.',
                ),
                const SizedBox(height: 16),
                DropdownButtonFormField<int>(
                  key: const Key('lan-host-interface'),
                  initialValue: index,
                  isExpanded: true,
                  decoration: const InputDecoration(
                    labelText: 'اتصال شبكة السنتر',
                  ),
                  items: [
                    for (var value = 0; value < addresses.length; value++)
                      DropdownMenuItem(
                        value: value,
                        child: Text(
                          '${addresses[value].name} · ${addresses[value].address}',
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                  ],
                  onChanged: (value) => update(() => index = value!),
                ),
              ],
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('إلغاء'),
              ),
              FilledButton(
                onPressed: () => Navigator.pop(context, index),
                child: const Text('نسخ بيانات الربط'),
              ),
            ],
          ),
        ),
      );
      if (selected == null || !mounted) return;
      index = selected;
    }
    final endpoint = LanEndpoint(
      hostId: ready.hostId,
      name: ready.name,
      address: addresses[index].address,
      port: ready.port,
      certificateSha256: ready.certificateSha256,
    );
    await Clipboard.setData(ClipboardData(text: jsonEncode(endpoint.toJson())));
    if (mounted) {
      await showManagementMessage(
        context,
        'نُسخت بيانات الربط. الصقها في الربط اليدوي على الجهاز الآخر.',
      );
    }
  });

  Widget _fingerprint(String value) => Wrap(
    spacing: 8,
    crossAxisAlignment: WrapCrossAlignment.center,
    children: [
      const Text('بصمة الجهاز:'),
      SelectableText(value.substring(0, 12), textDirection: TextDirection.ltr),
    ],
  );

  Widget _host(bool locked) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const Text(
        'الجهاز الرئيسي يحتفظ بقاعدة بيانات السنتر ويستقبل أجهزة نفس الشبكة أثناء تشغيل البرنامج.',
      ),
      const SizedBox(height: 16),
      TextField(
        key: const Key('lan-device-name'),
        controller: _name,
        onChanged: (_) => setState(() => _operationError = null),
        enabled: !locked && controller.canConfigureHost && !controller.isHost,
        maxLength: 120,
        decoration: const InputDecoration(labelText: 'اسم الجهاز الرئيسي'),
      ),
      const SizedBox(height: 12),
      if (!controller.canConfigureHost)
        const Text(
          'سجّل دخول مدير هذا الجهاز لتشغيل السيرفر أو إدارة الأجهزة المرتبطة.',
        ),
      Wrap(
        spacing: 12,
        runSpacing: 12,
        children: [
          FilledButton.icon(
            key: const Key('lan-start-host'),
            onPressed:
                locked || !controller.canConfigureHost || controller.isHost
                ? null
                : () => _run(
                    () => controller.startHost(name: _name.text),
                    success: 'تم تشغيل السيرفر المحلي.',
                  ),
            icon: const Icon(Icons.router_outlined),
            label: const Text('تشغيل السيرفر'),
          ),
          if (controller.isHost)
            OutlinedButton(
              key: const Key('lan-stop-host'),
              onPressed: locked || !controller.canConfigureHost
                  ? null
                  : () async {
                      final accepted = await confirmManagement(
                        context,
                        title: 'إيقاف ربط الأجهزة؟',
                        description:
                            'ستفقد الأجهزة المرتبطة الاتصال. بيانات السنتر على الجهاز الرئيسي تظل محفوظة.',
                        confirmLabel: 'إيقاف السيرفر',
                      );
                      if (accepted && mounted) {
                        await _run(
                          controller.stopHost,
                          success: 'توقف السيرفر المحلي.',
                        );
                      }
                    },
              child: const Text('إيقاف السيرفر'),
            ),
        ],
      ),
      if (controller.hostReady != null) ...[
        const SizedBox(height: 20),
        SelectableText('الجهاز الرئيسي: ${controller.hostReady!.name}'),
        SelectableText('معرف الجهاز الرئيسي: ${controller.hostReady!.hostId}'),
        _fingerprint(controller.hostReady!.certificateSha256),
        if (controller.canConfigureHost) ...[
          const SizedBox(height: 16),
          Text(
            'رمز الربط: ${controller.pairingCode ?? 'غير متاح'}',
            key: const Key('lan-pairing-code'),
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          if (controller.pairingExpiresAt != null)
            Text(
              'ينتهي الرمز الساعة ${TimeOfDay.fromDateTime(controller.pairingExpiresAt!.toLocal()).format(context)}. اطلب رمزًا جديدًا لو انتهت صلاحيته.',
            ),
          const Text(
            'على الجهاز الآخر: الصق بيانات الجهاز في الخطوة ١، ثم أدخل هذا الرمز في الخطوة ٢. الرمز لأول ربط فقط، والأجهزة المحفوظة تعود تلقائيًا.',
          ),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              TextButton(
                onPressed: locked
                    ? null
                    : () => _run(controller.renewPairingCode),
                child: const Text('رمز جديد'),
              ),
              TextButton(
                key: const Key('copy-lan-host-data'),
                onPressed: locked ? null : _copyHost,
                child: const Text('نسخ بيانات الربط'),
              ),
              TextButton(
                onPressed: locked
                    ? null
                    : () => _run(controller.refreshDevices),
                child: const Text('تحديث الأجهزة'),
              ),
            ],
          ),
          for (final device in controller.pairedDevices)
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.computer),
              title: Text(
                device['name'] is String
                    ? device['name'] as String
                    : 'جهاز مرتبط',
              ),
              subtitle: Text(
                device['revoked'] != true ? 'مسموح بالاتصال' : 'أُلغي الربط',
              ),
              trailing: device['revoked'] == true
                  ? null
                  : TextButton(
                      onPressed: locked
                          ? null
                          : () async {
                              final id = device['deviceId'];
                              if (id is! String) return;
                              final accepted = await confirmManagement(
                                context,
                                title: 'إلغاء ربط هذا الجهاز؟',
                                description:
                                    'يحتاج الجهاز رمز ربط جديدًا قبل العودة. بيانات السنتر تظل محفوظة.',
                                confirmLabel: 'إلغاء الربط',
                                destructive: true,
                              );
                              if (accepted && mounted) {
                                await _run(
                                  () => controller.revokeDevice(id),
                                  success: 'أُلغي ربط الجهاز.',
                                );
                              }
                            },
                      child: const Text('إلغاء الربط'),
                    ),
            ),
        ],
      ],
    ],
  );

  bool _hasSocketCause(Object error) {
    Object? cause = error;
    for (var depth = 0; depth < 4; depth++) {
      if (cause is SocketException) return true;
      cause = cause is CenterException ? cause.cause : null;
    }
    return false;
  }

  Future<void> _discover() async {
    if (_busy || controller.isBusy || controller.isHost) return;
    _autoDiscoveryRequested = true;
    setState(() {
      _discovering = true;
      _selectedEndpoint = null;
      _identityVisible = false;
    });
    try {
      await _run(() async {
        setState(() {
          _discoveryFailed = false;
          _discoverySocketFailure = false;
        });
        try {
          await controller.discoverHosts();
          if (mounted && controller.endpoints.length == 1) {
            _selectEndpoint(controller.endpoints.single);
          }
        } catch (error) {
          if (mounted) {
            setState(() {
              _discoveryFailed = true;
              _discoverySocketFailure = _hasSocketCause(error);
            });
          }
          rethrow;
        }
      });
    } finally {
      if (mounted) {
        setState(() => _discovering = false);
        if (ModalRoute.of(context)?.isCurrent == true) {
          _codeFocus.requestFocus();
        }
      }
    }
  }

  void _selectEndpoint(LanEndpoint endpoint) {
    setState(() {
      _selectedEndpoint = endpoint;
      _identityVisible = false;
    });
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && identical(_selectedEndpoint, endpoint)) {
        setState(() => _identityVisible = true);
      }
    });
  }

  Future<void> _changeMode(LanMode mode) async {
    final current =
        _choice ?? controller.configuration?.mode ?? LanMode.standalone;
    if (_busy || _changingContext || controller.isBusy || current == mode) {
      return;
    }
    setState(() => _changingContext = true);
    try {
      final accepted = await confirmDiscardDraft(context, dirty: _dirty);
      if (!mounted || !accepted) return;
      setState(() {
        _name.text = _savedName;
        _directCode.clear();
        _choice = mode;
      });
      _configurationChanged();
    } finally {
      if (mounted) setState(() => _changingContext = false);
    }
  }

  Future<void> _changeHost(LanEndpoint endpoint) async {
    if (_busy ||
        _changingContext ||
        controller.isBusy ||
        endpoint == _selectedEndpoint) {
      return;
    }
    setState(() => _changingContext = true);
    try {
      final accepted = await confirmDiscardDraft(
        context,
        dirty: _directCode.text.isNotEmpty,
      );
      if (!mounted || !accepted) return;
      _directCode.clear();
      _selectEndpoint(endpoint);
    } finally {
      if (mounted) {
        setState(() {
          _changingContext = false;
          _hostSelectionRevision++;
        });
      }
    }
  }

  Future<void> _connectByCode() async {
    if (_busy ||
        controller.isBusy ||
        _discovering ||
        !_identityVisible ||
        controller.isHost ||
        controller.configuration == null ||
        (controller.status == LanConnectionStatus.connected &&
            controller.configuration?.deviceToken != null)) {
      return;
    }
    if (!_codeForm.currentState!.validate()) return;
    final endpoint = _selectedEndpoint;
    if (endpoint == null) {
      await _run(
        () => throw const CenterException(
          'لم يتحدد الجهاز الرئيسي. انتظر البحث أو اختر جهازًا من النتائج؛ ويمكن استخدام الربط اليدوي إذا لم يظهر.',
        ),
      );
      return;
    }
    final code = _digits(_directCode.text);
    await _run(() => controller.pairHost(endpoint, code));
    if (mounted &&
        (controller.configuration?.deviceToken == null ||
            controller.status != LanConnectionStatus.connected) &&
        ModalRoute.of(context)?.isCurrent == true) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || ModalRoute.of(context)?.isCurrent != true || _busy) {
          return;
        }
        _codeFocus.requestFocus();
        _directCode.selection = TextSelection(
          baseOffset: 0,
          extentOffset: _directCode.text.length,
        );
      });
    }
  }

  Widget _discoveryHelp(bool locked) => Container(
    key: const Key('lan-discovery-help'),
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: MassarPalette.of(context).warningSurface,
      borderRadius: BorderRadius.circular(8),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'تعذر البحث التلقائي — يمكنك الربط يدويًا',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 8),
        const Text(
          'تأكد من تشغيل السيرفر في مسار على الجهاز الرئيسي، ومن اتصال الجهازين بنفس شبكة السنتر. انسخ «بيانات الربط» من الرئيسي والصقها هنا أولًا؛ كود الستة أرقام يأتي في الخطوة التالية.',
        ),
        if (_discoverySocketFailure && Platform.isMacOS) ...[
          const SizedBox(height: 8),
          const Text(
            'قد يكون إذن الشبكة المحلية أو إعداد الشبكة هو السبب. على الماك افتح إعدادات النظام ← الخصوصية والأمان ← الشبكة المحلية، واسمح لمسار إن ظهر في القائمة. هذه إعدادات macOS، وليست إعدادات داخل مسار. إذا لم يظهر البرنامج، حاول البحث مرة أخرى ووافق على طلب الإذن إن ظهر.',
          ),
        ],
        const SizedBox(height: 12),
        Wrap(
          spacing: 12,
          runSpacing: 8,
          children: [
            FilledButton.icon(
              key: const Key('lan-discovery-manual'),
              onPressed: locked || controller.isHost ? null : _manual,
              icon: const Icon(Icons.link),
              label: const Text('الخطوة ١: لصق بيانات الجهاز'),
            ),
            TextButton(
              key: const Key('lan-discovery-retry'),
              onPressed: locked || controller.isHost ? null : _discover,
              child: const Text('إعادة البحث'),
            ),
          ],
        ),
      ],
    ),
  );

  Future<void> _exportProblemLog() => _run(
    () async {
      final log = ProblemLog.current;
      if (log == null) return;
      final location = await getSaveLocation(
        suggestedName:
            'massar-network-problems-${DateTime.now().millisecondsSinceEpoch}.txt',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'سجل المشاكل', extensions: ['txt']),
        ],
      );
      if (location == null || !mounted) return;
      await log.exportTo(location.path);
      if (mounted) {
        await showManagementMessage(
          context,
          'تم تصدير سجل المشاكل للدعم بنجاح.',
        );
      }
    },
    failureMessage:
        'تعذر تصدير سجل المشاكل. اختر ملفًا جديدًا بامتداد .txt خارج مجلد السجل، وراجع صلاحية الكتابة أو مساحة الجهاز. لا يمكن استبدال ملف موجود.',
  );

  Widget _client(bool locked) {
    final config = controller.configuration;
    final paired = config?.deviceToken != null;
    final endpoint = _selectedEndpoint;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'شغّل السيرفر على الجهاز الرئيسي، واجعل الجهازين على نفس شبكة الواي فاي أو الكابل. اكتب كود الستة أرقام الظاهر عليه، ثم اتصل بعد مراجعة اسم الجهاز وبصمته.',
        ),
        if (!controller.isClientOnly)
          const Text(
            'أي بيانات موجودة على هذا الجهاز تظل محفوظة ومنفصلة، ولن تُدمج ببيانات الجهاز الرئيسي.',
          ),
        const SizedBox(height: 16),
        if (controller.endpoints.length > 1) ...[
          KeyedSubtree(
            key: ValueKey(_hostSelectionRevision),
            child: DropdownButtonFormField<LanEndpoint>(
              key: const Key('lan-direct-host-choice'),
              initialValue: controller.endpoints.contains(endpoint)
                  ? endpoint
                  : null,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'ظهر أكثر من جهاز — اختر الجهاز الرئيسي',
              ),
              items: [
                for (final host in controller.endpoints)
                  DropdownMenuItem(
                    value: host,
                    child: Text(
                      '${host.name} · ${host.certificateSha256.substring(0, 12)}',
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
              ],
              onChanged: locked
                  ? null
                  : (value) {
                      if (value != null) _changeHost(value);
                    },
            ),
          ),
          const SizedBox(height: 12),
        ],
        if (endpoint != null) ...[
          Container(
            key: const Key('lan-direct-host-identity'),
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: MassarPalette.of(context).subtle,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  '${paired ? 'الجهاز الرئيسي المحفوظ' : 'الجهاز الرئيسي'}: ${endpoint.name}',
                  key: const Key('lan-saved-host'),
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                _fingerprint(endpoint.certificateSha256),
                const Text(
                  'قارن هذه البصمة بالبصمة الظاهرة على الجهاز الرئيسي قبل الاتصال.',
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
        ],
        if (!paired || !controller.activeStore.remoteConnected)
          Form(
            key: _codeForm,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextFormField(
                  key: const Key('lan-direct-code'),
                  controller: _directCode,
                  onChanged: (_) => setState(() => _operationError = null),
                  focusNode: _codeFocus,
                  autofocus: controller.isClientOnly,
                  enabled:
                      config != null &&
                      (!locked || _discovering) &&
                      !controller.isHost,
                  keyboardType: TextInputType.number,
                  textInputAction: TextInputAction.done,
                  maxLength: 6,
                  decoration: const InputDecoration(
                    labelText: 'كود الربط — ستة أرقام',
                    helperText:
                        'اكتب الكود الظاهر على الجهاز الرئيسي. لا تحتاج نسخ بيانات الربط إذا ظهر الجهاز هنا.',
                    helperMaxLines: 3,
                    errorMaxLines: 2,
                  ),
                  validator: (value) =>
                      RegExp(r'^\d{6}$').hasMatch(_digits(value ?? ''))
                      ? null
                      : 'اكتب الستة أرقام الظاهرة على الجهاز الرئيسي.',
                  onFieldSubmitted: (_) => _connectByCode(),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 12,
                  runSpacing: 8,
                  children: [
                    FilledButton.icon(
                      key: const Key('lan-direct-connect'),
                      onPressed:
                          locked ||
                              endpoint == null ||
                              !_identityVisible ||
                              controller.isHost
                          ? null
                          : _connectByCode,
                      icon: const Icon(Icons.link),
                      label: const Text('اتصال بالجهاز الرئيسي'),
                    ),
                    if (_discovering)
                      const Padding(
                        padding: EdgeInsets.all(8),
                        child: Text(
                          'جارٍ البحث؛ يمكنك كتابة الكود الآن، ثم الاتصال بعد ظهور الجهاز.',
                        ),
                      ),
                    if (!_discovering &&
                        endpoint == null &&
                        controller.endpoints.length > 1)
                      const Text('اختر الجهاز الرئيسي قبل إرسال الكود.'),
                    if (!_discovering &&
                        endpoint == null &&
                        controller.endpoints.isEmpty &&
                        !_discoveryFailed)
                      const Text(
                        'لم يظهر الجهاز الرئيسي بعد. أعد البحث أو افتح خيارات الربط اليدوي.',
                      ),
                  ],
                ),
                const SizedBox(height: 16),
                const Text(
                  'الربط لا يسجل دخول الموظف؛ بعد الاتصال سجّل دخولك بحساب من الجهاز الرئيسي. لا يتم تسجيل عمليات أو دفعات محلية عند انقطاع الاتصال.',
                ),
              ],
            ),
          )
        else
          const Text(
            'الاتصال محفوظ. يعود هذا الجهاز للرئيسي تلقائيًا، ثم يمكنك تسجيل دخول الموظف.',
          ),
        const SizedBox(height: 16),
        Wrap(
          spacing: 12,
          runSpacing: 8,
          children: [
            OutlinedButton.icon(
              key: const Key('lan-discover'),
              onPressed: locked || controller.isHost ? null : _discover,
              icon: const Icon(Icons.search),
              label: const Text('إعادة البحث عن الجهاز الرئيسي'),
            ),
            if (paired)
              OutlinedButton(
                key: const Key('lan-reconnect'),
                onPressed: locked || controller.isHost
                    ? null
                    : () => _run(controller.reconnect),
                child: const Text('إعادة الاتصال بالجهاز المحفوظ'),
              ),
          ],
        ),
        const SizedBox(height: 12),
        if (_discoveryFailed) _discoveryHelp(locked),
        ExpansionTile(
          key: const Key('lan-advanced-link'),
          tilePadding: EdgeInsets.zero,
          title: const Text('خيارات إضافية إذا لم يظهر الجهاز'),
          children: [
            Align(
              alignment: AlignmentDirectional.centerStart,
              child: TextButton(
                key: const Key('lan-manual-link'),
                onPressed: locked || controller.isHost ? null : _manual,
                child: const Text('ربط يدوي ببيانات الجهاز'),
              ),
            ),
          ],
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: Listenable.merge([
      controller,
      controller.localStore,
      controller.activeStore,
    ]),
    builder: (context, _) {
      final config = controller.configuration;
      final mode = controller.isClientOnly
          ? LanMode.client
          : _choice ?? config?.mode ?? LanMode.standalone;
      final locked =
          _busy || _changingContext || controller.isBusy || config == null;
      final colors = MassarPalette.of(context);
      return WorkspaceDraftRegistration(
        dirty: _dirty,
        busy: locked || _discovering,
        child: ManagementPanel(
          title: controller.isClientOnly
              ? 'جهاز متصل — البيانات على الرئيسي'
              : 'ربط الأجهزة',
          subtitle: controller.isClientOnly
              ? 'اكتب كود الربط من الجهاز الرئيسي؛ يُحفظ الاتصال بعد أول ربط.'
              : 'جهاز رئيسي واحد وبيانات موحدة داخل شبكة السنتر.',
          child: MassarScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (_operationError != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: Semantics(
                      liveRegion: true,
                      child: Text(
                        'تعذر إتمام العملية. $_operationError\nالمدخلات موجودة؛ راجع السبب وحاول مجددًا.',
                        style: TextStyle(color: colors.error),
                      ),
                    ),
                  ),
                Container(
                  key: const Key('lan-connection-status'),
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: colors.subtle,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    controller.statusLabel,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                const SizedBox(height: 12),
                if (config != null)
                  SelectableText(
                    'هوية هذا الجهاز: ${config.deviceName} · ${config.deviceId}',
                  ),
                const SizedBox(height: 20),
                if (!controller.isClientOnly)
                  Wrap(
                    spacing: 12,
                    runSpacing: 12,
                    children: [
                      for (final item in [
                        (LanMode.standalone, 'مستقل'),
                        (LanMode.host, 'جهاز رئيسي'),
                        (LanMode.client, 'جهاز مرتبط'),
                      ])
                        ChoiceChip(
                          key: ValueKey('lan-mode-${item.$1.name}'),
                          selected: mode == item.$1,
                          label: Text(item.$2),
                          onSelected: locked
                              ? null
                              : (_) => _changeMode(item.$1),
                        ),
                    ],
                  ),
                const SizedBox(height: 20),
                if (mode == LanMode.host)
                  _host(locked)
                else if (mode == LanMode.client)
                  _client(locked)
                else ...[
                  const Text(
                    'تستخدم بيانات هذا الجهاز فقط، ويعمل البرنامج دون إنترنت.',
                  ),
                  const SizedBox(height: 16),
                  if (config?.mode != LanMode.standalone)
                    OutlinedButton(
                      key: const Key('lan-use-standalone'),
                      onPressed: locked ? null : _standalone,
                      child: const Text('العودة للوضع المستقل'),
                    ),
                ],
                if (config?.mode != LanMode.standalone) ...[
                  const SizedBox(height: 20),
                  OutlinedButton.icon(
                    key: const Key('lan-test-connection'),
                    onPressed: locked
                        ? null
                        : () => _run(
                            controller.testConnection,
                            success: 'الاتصال بالجهاز الرئيسي يعمل.',
                          ),
                    icon: const Icon(Icons.network_check),
                    label: const Text('اختبار الاتصال'),
                  ),
                ],
                const SizedBox(height: 20),
                const Text(
                  'سجل المشاكل المحلي للدعم منقّح؛ لا يتضمن بيانات الطلاب أو كلمات المرور أو كود الربط.',
                ),
                const SizedBox(height: 8),
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: OutlinedButton.icon(
                    key: const Key('lan-export-problem-log'),
                    onPressed: locked || ProblemLog.current == null
                        ? null
                        : _exportProblemLog,
                    icon: const Icon(Icons.file_download_outlined),
                    label: const Text('تصدير سجل مشكلة الربط'),
                  ),
                ),
                if (locked)
                  const Padding(
                    padding: EdgeInsets.only(top: 16),
                    child: LinearProgressIndicator(),
                  ),
              ],
            ),
          ),
        ),
      );
    },
  );
}
