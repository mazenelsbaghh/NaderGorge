import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../application/center_store.dart';
import '../../cloud/app_update_controller.dart';
import '../../cloud/cloud_support_controller.dart';
import '../../shared/app_build_metadata.dart';
import '../../shared/problem_log.dart';
import '../../shared/problem_reporting.dart';
import 'management_widgets.dart';

class CloudSettingsPage extends StatefulWidget {
  const CloudSettingsPage({
    super.key,
    required this.store,
    required this.cloud,
    required this.updates,
  });

  final CenterStore store;
  final CloudSupportController cloud;
  final AppUpdateController updates;

  @override
  State<CloudSettingsPage> createState() => _CloudSettingsPageState();
}

class _CloudSettingsPageState extends State<CloudSettingsPage> {
  late final Map<AppPackageRole, AppUpdateController> _packages;
  bool _busy = false;
  String? _message, _error;
  List<Map<String, dynamic>> _events = const [];

  @override
  void initState() {
    super.initState();
    _packages = {
      for (final role in AppPackageRole.values)
        role: widget.updates.createPackageDownloader(role),
    };
    unawaited(_loadEvents());
  }

  @override
  void dispose() {
    for (final download in _packages.values) {
      unawaited(download.close().whenComplete(download.dispose));
    }
    super.dispose();
  }

  Future<void> _downloadPackages() => _run(
    () async =>
        Future.wait(_packages.values.map((download) => download.checkNow())),
  );

  Future<void> _savePackage(AppPackageRole role) => _run(() async {
    final download = _packages[role]!;
    final location = await getSaveLocation(
      suggestedName: download.suggestedFilename,
      acceptedTypeGroups: const [
        XTypeGroup(label: 'ZIP', extensions: ['zip']),
      ],
    );
    if (location == null) return;
    await download.saveDownloadedTo(location.path);
    if (mounted) {
      setState(
        () => _message =
            'تم حفظ نسخة ${_packageLabel(role)} في: ${location.path}',
      );
    }
  });

  String _packageLabel(AppPackageRole role) => switch (role) {
    AppPackageRole.host => 'الهوست — الجهاز الرئيسي',
    AppPackageRole.client => 'الساكند — الجهاز الفرعي',
  };

  Widget _packageDownload(AppPackageRole role) {
    final download = _packages[role]!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          _packageLabel(role),
          style: const TextStyle(fontWeight: FontWeight.bold),
        ),
        Text(download.statusLabel),
        if (download.availableVersion != null)
          Text('الإصدار: ${download.availableVersion}'),
        if (download.checking)
          LinearProgressIndicator(value: download.progress),
        if (download.lastError != null)
          Text(
            download.lastError!,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
        Wrap(
          spacing: 12,
          runSpacing: 8,
          children: [
            OutlinedButton.icon(
              onPressed: _busy || download.checking
                  ? null
                  : () => _run(download.checkNow),
              icon: const Icon(Icons.download_outlined),
              label: Text(
                role == AppPackageRole.host ? 'تنزيل الهوست' : 'تنزيل الساكند',
              ),
            ),
            if (download.downloadedPath != null)
              FilledButton.tonalIcon(
                onPressed: _busy || download.checking
                    ? null
                    : () => _savePackage(role),
                icon: const Icon(Icons.save_alt),
                label: Text(
                  role == AppPackageRole.host
                      ? 'حفظ ملف الهوست'
                      : 'حفظ ملف الساكند لإرساله',
                ),
              ),
          ],
        ),
      ],
    );
  }

  Future<void> _run(Future<void> Function() action) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _message = null;
      _error = null;
    });
    try {
      await action();
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'ui.cloud_settings');
      if (mounted) {
        setState(
          () => _error = error is CenterException
              ? error.message
              : 'تعذرت العملية. راجع حالة الربط وسجل المشاكل.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _configure() async {
    if (_busy || !widget.store.canManage) return;
    setState(() => _busy = true);
    try {
      await _configureDialog();
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'ui.cloud_settings');
      if (mounted) {
        setState(() => _error = 'تعذر فتح إعداد الربط. حاول مجددًا.');
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _configureDialog() async {
    final origin = TextEditingController(text: widget.cloud.origin?.toString());
    final center = TextEditingController(text: widget.cloud.centerId);
    final token = TextEditingController();
    final saved = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => ManagementEditor(
        title: 'ربط هذا الجهاز بمسار',
        controllers: [origin, center, token],
        saveLabel: 'حفظ الربط',
        fields: [
          const Text(
            'استخدم عنوان خدمة الدعم الخاصة ورمز الربط المخصص للسنتر من إدارة مسار. بعد الربط، يفحص هذا الجهاز التحديثات وينزّل نسخته تلقائيًا عند توفر الإنترنت.',
          ),
          TextFormField(
            controller: origin,
            textDirection: TextDirection.ltr,
            decoration: const InputDecoration(
              labelText: 'عنوان خدمة مسار — HTTPS',
            ),
            validator: requiredText,
          ),
          TextFormField(
            controller: center,
            textDirection: TextDirection.ltr,
            decoration: const InputDecoration(labelText: 'معرّف السنتر'),
            validator: requiredText,
          ),
          TextFormField(
            controller: token,
            obscureText: true,
            enableSuggestions: false,
            autocorrect: false,
            textDirection: TextDirection.ltr,
            decoration: const InputDecoration(
              labelText: 'رمز الربط الخاص',
              helperText: 'رمز خدمة الدعم، وليس كلمة مرور الموظف.',
            ),
            validator: requiredText,
          ),
          const Text(
            'حفظ الربط لا يرفع بيانات الطلبة. الرفع يبدأ من زر «مزامنة للدعم». النسخة المعلّقة تستكمل تلقائيًا بعد عودة الإنترنت.',
          ),
        ],
        onSave: () async {
          final uri = Uri.tryParse(origin.text.trim());
          if (uri == null) {
            throw const CenterException('عنوان الخدمة غير صالح.');
          }
          if (!widget.store.canManage) {
            throw const CenterException('سجّل دخول المدير لإعداد وجهة مسار.');
          }
          await widget.cloud.configure(uri, center.text, token.text);
        },
      ),
    );
    if (mounted && saved == true) unawaited(widget.updates.checkNow());
  }

  Future<void> _sync() => _run(() async {
    final messages = <String>[];
    final errors = <String>[];
    try {
      await widget.store.requestSupportUpload();
      messages.add('نسخة البيانات في قائمة إرسال الجهاز الرئيسي.');
    } catch (error) {
      errors.add(
        error is CenterException ? error.message : 'تعذر طلب نسخة الرئيسي.',
      );
    }
    if (widget.cloud.clientOnly) {
      try {
        await widget.cloud.queueUpload();
        messages.add('سجل مشاكل هذا الجهاز في قائمة الإرسال.');
      } catch (error) {
        errors.add(
          error is CenterException
              ? error.message
              : 'تعذر تجهيز سجل هذا الجهاز.',
        );
      }
    }
    if (mounted) {
      setState(() {
        _message = messages.isEmpty ? null : messages.join('\n');
        _error = errors.isEmpty ? null : errors.join('\n');
      });
    }
  });

  Future<void> _loadEvents() async {
    try {
      final log = ProblemLog.current;
      if (log == null) return;
      final text = await log.exportText();
      final events = const LineSplitter()
          .convert(text)
          .map((line) => jsonDecode(line) as Map<String, dynamic>)
          .where((event) => event['kind'] == 'error')
          .toList();
      if (mounted) setState(() => _events = events.reversed.take(100).toList());
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'ui.cloud_settings');
      if (mounted) setState(() => _error = 'تعذر قراءة سجل المشاكل الآن.');
    }
  }

  Future<void> _exportLogs() => _run(() async {
    final log = ProblemLog.current;
    if (log == null) throw const CenterException('سجل المشاكل غير متاح الآن.');
    final location = await getSaveLocation(
      suggestedName: 'massar-problems.txt',
    );
    if (location == null) return;
    await log.exportTo(location.path);
    if (mounted) {
      setState(() => _message = 'تم حفظ سجل المشاكل في المكان المختار.');
    }
  });

  String _date(Object? value) {
    final parsed = DateTime.tryParse('$value')?.toLocal();
    if (parsed == null) return 'لم يتم بعد';
    String two(int n) => '$n'.padLeft(2, '0');
    return '${parsed.year}/${two(parsed.month)}/${two(parsed.day)} ${two(parsed.hour)}:${two(parsed.minute)}';
  }

  Widget _box(String title, List<Widget> children, {IconData? icon}) => Card(
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              if (icon != null) ...[Icon(icon), const SizedBox(width: 8)],
              Expanded(
                child: Text(
                  title,
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          for (final child in children)
            Padding(padding: const EdgeInsets.only(bottom: 10), child: child),
        ],
      ),
    ),
  );

  Widget _uploadStatus(Map<String, dynamic> status) {
    final label = status['busy'] == true
        ? 'جارٍ تجهيز أو إرسال النسخة'
        : status['pending'] == true
        ? 'نسخة معلّقة — تُستكمل تلقائيًا'
        : status['configured'] == true
        ? 'جاهز للرفع عند الطلب'
        : 'لم يتم ربط خدمة مسار بعد';
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontWeight: FontWeight.bold)),
        Text('آخر رفع مؤكّد: ${_date(status['lastSuccess'])}'),
        if (status['pendingCreatedAt'] != null)
          Text('النسخة المعلّقة التُقطت: ${_date(status['pendingCreatedAt'])}'),
        if (status['lastError'] is String)
          Text(
            status['lastError'] as String,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: Listenable.merge([
      widget.store,
      widget.cloud,
      widget.updates,
      ..._packages.values,
    ]),
    builder: (context, _) {
      final updates = widget.updates;
      return ManagementPanel(
        title: 'المزامنة والتحديثات',
        subtitle:
            'الشغل أوفلاين كما هو. ارفع نسخة للدعم وتابع تحديث كل جهاز من هنا.',
        actions: [
          if (widget.store.canManage)
            OutlinedButton.icon(
              onPressed: _busy || widget.cloud.busy ? null : _configure,
              icon: const Icon(Icons.link),
              label: const Text('إعداد الربط'),
            ),
        ],
        child: ListView(
          children: [
            if (_busy) const LinearProgressIndicator(),
            if (_message != null)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Text(
                  _message!,
                  style: TextStyle(
                    color: Theme.of(context).colorScheme.primary,
                  ),
                ),
              ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.all(12),
                child: Text(
                  _error!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            _box('نسخة هذا الجهاز', [
              Wrap(
                spacing: 24,
                runSpacing: 8,
                children: [
                  Text('الإصدار المثبّت: ${AppBuildMetadata.version}'),
                  Text(
                    widget.cloud.clientOnly
                        ? 'الساكند — الجهاز الفرعي'
                        : 'الهوست — الجهاز الرئيسي',
                  ),
                  Text(
                    Platform.isWindows
                        ? 'ويندوز'
                        : Platform.isMacOS
                        ? 'ماك'
                        : Platform.operatingSystem,
                  ),
                ],
              ),
              SelectableText(
                'بصمة النسخة: ${AppBuildMetadata.buildIdentifier}',
                textDirection: TextDirection.ltr,
              ),
            ], icon: Icons.computer_outlined),
            _box('مزامنة للدعم', [
              const Text(
                'الرفع تلقائي عند فتح البرنامج، ثم تُفحص التغييرات كل دقيقة. الرئيسي يرفع البيانات كاملة مع المشاكل والإصدار، والفرعي يرفع مشاكله فقط. عند انقطاع النت تظل النسخة محفوظة وتُعاد المحاولة تلقائيًا.',
              ),
              if (widget.store.isRemote)
                const Text(
                  'قاعدة البيانات تُجهّز وتُرفع من الرئيسي فقط. هذا الجهاز يرفع سجل مشاكله؛ لا ينزّل قاعدة البيانات.',
                ),
              if (widget.cloud.origin != null)
                SelectableText('وجهة هذا الجهاز: ${widget.cloud.origin}'),
              _uploadStatus(widget.store.supportStatus),
              if (widget.cloud.clientOnly) ...[
                const Divider(),
                const Text('سجل مشاكل الجهاز الفرعي'),
                _uploadStatus(widget.cloud.publicStatus),
              ],
              Align(
                alignment: AlignmentDirectional.centerStart,
                child: FilledButton.icon(
                  onPressed: _busy || widget.cloud.busy ? null : _sync,
                  icon: const Icon(Icons.cloud_upload_outlined),
                  label: const Text('مزامنة للدعم'),
                ),
              ),
            ], icon: Icons.cloud_sync_outlined),
            _box('تحديث البرنامج', [
              const Text(
                'كل جهاز ينزّل النسخة المناسبة لنظامه ودوره عند توفر الإنترنت. التنزيل لا يثبّت البرنامج ولا يستبدل بيانات السنتر.',
              ),
              Text(
                updates.statusLabel,
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              if (updates.progress != null)
                LinearProgressIndicator(value: updates.progress),
              if (updates.availableVersion != null)
                Text('الإصدار المتاح: ${updates.availableVersion}'),
              if (updates.releaseNotes?.isNotEmpty == true)
                Text(updates.releaseNotes!),
              if (updates.lastError != null)
                Text(
                  updates.lastError!,
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              Wrap(
                spacing: 12,
                runSpacing: 8,
                children: [
                  FilledButton.tonalIcon(
                    onPressed: _busy || updates.checking
                        ? null
                        : () => _run(updates.checkNow),
                    icon: const Icon(Icons.system_update_alt),
                    label: const Text('البحث وتنزيل التحديث'),
                  ),
                  if (updates.downloadedPath != null)
                    OutlinedButton.icon(
                      onPressed: () => _run(() async {
                        await Clipboard.setData(
                          ClipboardData(text: updates.downloadedPath!),
                        );
                        if (mounted) {
                          setState(() => _message = 'تم نسخ مكان ملف التحديث.');
                        }
                      }),
                      icon: const Icon(Icons.copy),
                      label: const Text('نسخ مكان ملف التحديث'),
                    ),
                ],
              ),
              if (updates.downloadedPath != null)
                SelectableText(
                  updates.downloadedPath!,
                  textDirection: TextDirection.ltr,
                ),
            ], icon: Icons.download_outlined),
            _box('نسخ الهوست والساكند', [
              const Text(
                'نزّل النسختين لنفس نظام هذا الجهاز، حتى لو البرنامج عندك محدّث. احفظ ملف الساكند ثم ابعته للجهاز الفرعي الذي يعمل بنفس النظام. كل ملف منفصل ولا يثبّت تلقائيًا.',
              ),
              Align(
                alignment: AlignmentDirectional.centerStart,
                child: FilledButton.icon(
                  onPressed:
                      _busy ||
                          _packages.values.any((download) => download.checking)
                      ? null
                      : _downloadPackages,
                  icon: const Icon(Icons.download),
                  label: const Text('تنزيل الهوست والساكند'),
                ),
              ),
              _packageDownload(AppPackageRole.host),
              const Divider(),
              _packageDownload(AppPackageRole.client),
            ], icon: Icons.devices_outlined),
            _box('سجل المشاكل', [
              const Text(
                'آخر ١٠٠ خطأ مسجّل على هذا الجهاز. التصدير والرفع يشملان السجل المتاح مع الإصدار؛ رسائل الأخطاء والبيانات الشخصية لا تظهر في سجل التشخيص.',
              ),
              Wrap(
                spacing: 12,
                children: [
                  OutlinedButton.icon(
                    onPressed: _busy ? null : () => _run(_loadEvents),
                    icon: const Icon(Icons.refresh),
                    label: const Text('تحديث السجل'),
                  ),
                  OutlinedButton.icon(
                    onPressed: _busy ? null : _exportLogs,
                    icon: const Icon(Icons.download),
                    label: const Text('حفظ سجل المشاكل'),
                  ),
                ],
              ),
              if (_events.isEmpty)
                const Text('لا توجد أخطاء مسجّلة في السجل المتاح.'),
              for (final event in _events)
                ListTile(
                  dense: true,
                  leading: Icon(
                    Icons.error_outline,
                    color: Theme.of(context).colorScheme.error,
                  ),
                  title: Text(
                    '${event['operation'] ?? 'غير محدد'} · ${(event['errors'] as List?)?.firstOrNull?['type'] ?? ''}',
                  ),
                  subtitle: Text(
                    '${_date(event['time'])} · الإصدار ${event['version'] ?? ''}',
                  ),
                  onTap: () => showDialog<void>(
                    context: context,
                    builder: (_) => AlertDialog(
                      title: const Text('تفاصيل الخطأ المسجّل'),
                      content: SingleChildScrollView(
                        child: SelectableText(
                          const JsonEncoder.withIndent('  ').convert(event),
                          textDirection: TextDirection.ltr,
                        ),
                      ),
                      actions: [
                        TextButton(
                          onPressed: () => Navigator.pop(context),
                          child: const Text('إغلاق'),
                        ),
                      ],
                    ),
                  ),
                ),
            ], icon: Icons.bug_report_outlined),
          ],
        ),
      );
    },
  );
}
