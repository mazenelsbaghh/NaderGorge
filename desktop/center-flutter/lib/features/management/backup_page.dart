import '../../shared/workspace_draft_guard.dart';
import 'dart:io';

import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:path/path.dart' as path;
import 'package:massar_center/shared/problem_log.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/shared/formatters.dart';

import 'management_widgets.dart';

class BackupPage extends StatefulWidget {
  const BackupPage({super.key, required this.store});
  final CenterStore store;

  @override
  State<BackupPage> createState() => _BackupPageState();
}

class _BackupPageState extends State<BackupPage> {
  bool _busy = false;
  String? _savedPath;

  Future<void> _backup() async {
    if (_busy || !widget.store.canManage) return;
    setState(() => _busy = true);
    try {
      final location = await getSaveLocation(
        suggestedName:
            'massar-backup-${DateTime.now().toIso8601String().substring(0, 19).replaceAll(':', '-')}.json',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'نسخة مسار', extensions: ['json']),
        ],
      );
      if (location == null) return;
      final saved = await widget.store.createBackup(destination: location.path);
      if (mounted) setState(() => _savedPath = saved);
      if (mounted) {
        await showManagementMessage(context, 'حُفظت النسخة الاحتياطية بنجاح.');
      }
    } catch (error, stackTrace) {
      ProblemLog.current?.record(
        error,
        stackTrace,
        operation: 'ui.backup_page',
      );
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _restore() async {
    if (_busy || !widget.store.canManage) return;
    setState(() => _busy = true);
    try {
      final file = await openFile(
        acceptedTypeGroups: const [
          XTypeGroup(label: 'نسخة مسار', extensions: ['json']),
        ],
      );
      if (file == null || !mounted) return;
      final accepted = await confirmManagement(
        context,
        title: 'استعادة نسخة احتياطية',
        description:
            'سيُستبدل محتوى الجهاز ببيانات النسخة المختارة بعد التحقق منها. تُحفظ نسخة من الوضع الحالي قبل الاستعادة، وستحتاج لتسجيل الدخول. حساب مدير التثبيت يظل متاحًا.\n\n${file.name}',
        confirmLabel: 'استعادة البيانات',
        destructive: true,
      );
      if (!accepted || !mounted) return;
      await widget.store.restoreBackup(file.path);
    } catch (error, stackTrace) {
      ProblemLog.current?.record(
        error,
        stackTrace,
        operation: 'ui.backup_page',
      );
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _copyProblemLogPath() async {
    final log = ProblemLog.current;
    if (_busy || !widget.store.canManage || log == null) return;
    setState(() => _busy = true);
    try {
      await Clipboard.setData(ClipboardData(text: log.directoryPath));
      if (!mounted) return;
      await showManagementMessage(
        context,
        log.writeFailure == null
            ? 'تم نسخ مسار سجل المشاكل.'
            : 'تم نسخ المسار، لكن حفظ سجل المشاكل متعذر حاليًا. راجع صلاحية الكتابة أو مساحة الجهاز.',
        kind: log.writeFailure == null
            ? NoticeKind.success
            : NoticeKind.warning,
      );
    } catch (error, stackTrace) {
      log.record(error, stackTrace, operation: 'diagnostics.copy_path');
      if (mounted) {
        await showManagementMessage(
          context,
          'تعذر نسخ المسار. يمكنك تحديده ونسخه يدويًا.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<bool> _validProblemLogDestination(
    String destination,
    ProblemLog log,
  ) async {
    final normalized = path.normalize(path.absolute(destination));
    final source = path.normalize(path.absolute(log.directoryPath));
    final unsafe =
        path.extension(normalized).toLowerCase() != '.txt' ||
        path.equals(source, normalized) ||
        path.isWithin(source, normalized) ||
        await FileSystemEntity.type(normalized, followLinks: false) !=
            FileSystemEntityType.notFound;
    if (!unsafe) return true;
    if (mounted) {
      await showManagementMessage(
        context,
        'اختار ملفًا جديدًا بامتداد .txt خارج مجلد السجل. لا يمكن استبدال ملف موجود.',
        kind: NoticeKind.warning,
      );
    }
    return false;
  }

  Future<void> _exportProblemLog() async {
    final log = ProblemLog.current;
    if (_busy || !widget.store.canManage || log == null) return;
    setState(() => _busy = true);
    try {
      if (log.writeFailure != null) {
        if (mounted) {
          await showManagementMessage(
            context,
            'تعذر حفظ سجل المشاكل محليًا. راجع صلاحية الكتابة أو مساحة الجهاز قبل التصدير.',
            kind: NoticeKind.warning,
          );
        }
        return;
      }
      final location = await getSaveLocation(
        suggestedName:
            'massar-problems-${DateTime.now().millisecondsSinceEpoch}.txt',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'سجل المشاكل', extensions: ['txt']),
        ],
      );
      if (location == null || !mounted || !widget.store.canManage) return;
      if (!await _validProblemLogDestination(location.path, log)) return;
      await log.exportTo(location.path);
      if (mounted) {
        await showManagementMessage(
          context,
          'تم تصدير سجل المشاكل للدعم بنجاح.',
        );
      }
    } catch (error, stackTrace) {
      log.record(error, stackTrace, operation: 'diagnostics.export');
      if (mounted) {
        await showManagementMessage(
          context,
          'تعذر تصدير سجل المشاكل. اختار مكانًا آخر وراجع صلاحية الكتابة أو مساحة الجهاز.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _problemLogSection() {
    final log = ProblemLog.current;
    final colors = MassarPalette.of(context);
    return Container(
      key: const Key('problem-log-section'),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: colors.surface,
        border: Border.all(color: colors.line),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Wrap(
            spacing: 18,
            runSpacing: 8,
            alignment: WrapAlignment.spaceBetween,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'سجل المشاكل',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const Text(
                    'سجل محلي للدعم، مستقل عن النسخ الاحتياطية وسجل التغييرات.',
                  ),
                ],
              ),
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  OutlinedButton.icon(
                    key: const Key('copy-problem-log-path'),
                    onPressed: _busy || log == null
                        ? null
                        : _copyProblemLogPath,
                    icon: const Icon(Icons.copy_outlined),
                    label: const Text('نسخ مسار السجل'),
                  ),
                  OutlinedButton.icon(
                    key: const Key('export-problem-log'),
                    onPressed: _busy || log == null ? null : _exportProblemLog,
                    icon: const Icon(Icons.file_download_outlined),
                    label: const Text('تصدير سجل المشاكل'),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 8),
          if (log == null)
            const Text('سجل المشاكل غير متاح في هذه الجلسة.')
          else
            SelectableText(
              log.directoryPath,
              key: const Key('problem-log-path'),
              textDirection: TextDirection.ltr,
              maxLines: 2,
              style: TextStyle(color: colors.muted, fontSize: 13),
            ),
          if (log?.writeFailure != null)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(
                'تعذر حفظ بعض المشاكل محليًا. راجع مكان السجل.',
                style: TextStyle(color: colors.warning),
              ),
            ),
        ],
      ),
    );
  }

  String _actionLabel(String action) => switch (action) {
    'setup_admin' => 'إعداد المدير',
    'installation_admin' => 'تجهيز مدير التثبيت',
    'payment_check' => 'مراجعة مبلغ الإيصال',
    'payment_uncheck' => 'إزالة مراجعة طالب',
    'payment_checks_clear' => 'مسح مراجعات حصة',
    'staff_create' => 'إضافة موظف',
    'catalog_save' => 'تعديل أساس النظام',
    'group_save' => 'تعديل مجموعة',
    'student_save' => 'تسجيل أو تعديل طالب',
    'student_transfer' => 'نقل طالب بين المجموعات',
    'student_discount' => 'تعديل الخصم الثابت',
    'session_save' => 'إنشاء أو تعديل حصة',
    'sessions_create' => 'إنشاء حصص للمجموعات',
    'entry' => 'تحضير وتحصيل',
    'package_renew' => 'تجديد باقة',
    'session_close' => 'إغلاق حصة',
    'attendance_record' => 'تسجيل حضور مستقل عن الدفع',
    'session_reopen' => 'إعادة فتح حصة للتحضير',
    'session_cancel' => 'إلغاء حصة',
    'academic_save' => 'رصد أكاديمي',
    'payment_review_save' => 'مراجعة الورق',
    'session_finalize' => 'تقفيلة حسابات الحصة',
    'payment_cancel' => 'إلغاء دفعة',
    'attendance_cancel' => 'إلغاء حضور',
    'entry_reverse' => 'إلغاء دخول وتصحيح الحساب',
    'entry_correct' => 'تصحيح دفع ودخول',
    'absence_present' => 'تصحيح غياب إلى حضور',
    'payment_method_correct' => 'تصحيح وسيلة الدفع',
    'package_refund' => 'استرداد باقة',
    'closing_reopen' => 'إعادة فتح تقفيلة',
    'backup_restore' => 'استعادة نسخة',
    _ => 'عملية مسجلة',
  };

  @override
  Widget build(BuildContext context) => !widget.store.canManage
      ? const EmptySection(message: 'النسخ والسجل متاحان للإدارة فقط.')
      : WorkspaceDraftRegistration(
          dirty: false,
          busy: _busy,
          child: ManagementPanel(
            title: 'النسخ الاحتياطي والسجل',
            subtitle: widget.store.isRemote
                ? 'البيانات محفوظة على الرئيسي. النسخ والاستعادة تتم من هناك؛ سجل المشاكل هنا يخص هذا الجهاز.'
                : 'البيانات محلية على هذا الجهاز. احفظ نسخة خارج الجهاز بصورة منتظمة.',
            child: ManagementBody(
              header: [
                Text(
                  widget.store.isRemote
                      ? 'النسخ التلقائية تُحفظ على الجهاز الرئيسي فقط؛ هذا الجهاز لا يحتفظ بقاعدة بيانات.'
                      : 'نسخ تلقائي كل ١٠ دقائق أثناء تشغيل البرنامج، مع الاحتفاظ بأحدث ٥٠ نسخة تلقائية. لا تُحذف نسخة قديمة إلا بعد نجاح حفظ النسخة الجديدة. النسخ اليدوية تبقى محفوظة.',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                if (widget.store.automaticBackupDirectory != null) ...[
                  const SizedBox(height: 12),
                  SelectableText(
                    'مجلد النسخ التلقائية: ${widget.store.automaticBackupDirectory}',
                  ),
                  const SizedBox(height: 8),
                  Text(
                    widget.store.lastAutomaticBackupAt == null
                        ? 'النسخ التلقائي يعمل؛ في انتظار أول نسخة ناجحة.'
                        : 'آخر نسخة تلقائية ناجحة: ${shortDate(widget.store.lastAutomaticBackupAt!.toLocal())} · ${TimeOfDay.fromDateTime(widget.store.lastAutomaticBackupAt!.toLocal()).format(context)}',
                  ),
                  if (widget.store.automaticBackupError != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      'تعذر إكمال النسخ أو تنظيف النسخ القديمة. سجل المشاكل يحتوي التفاصيل؛ ستُعاد المحاولة تلقائيًا. ${widget.store.automaticBackupError}',
                      style: TextStyle(
                        color: MassarPalette.of(context).warning,
                      ),
                    ),
                  ],
                ],
                const SizedBox(height: 18),
                if (!widget.store.isRemote)
                  SelectableText(
                    'ملف البيانات المحلي: ${widget.store.databasePath}',
                  ),
                const SizedBox(height: 18),
                if (!widget.store.isRemote)
                  Wrap(
                    spacing: 12,
                    runSpacing: 12,
                    children: [
                      FilledButton.icon(
                        onPressed: _busy ? null : _backup,
                        icon: const Icon(Icons.save_alt),
                        label: Text(
                          _busy
                              ? 'جارٍ تنفيذ العملية…'
                              : 'حفظ نسخة على الجهاز أو فلاشة',
                        ),
                      ),
                      OutlinedButton.icon(
                        onPressed: _busy ? null : _restore,
                        icon: const Icon(Icons.restore),
                        label: const Text('استعادة نسخة'),
                      ),
                    ],
                  ),
                if (_savedPath != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 16),
                    child: SelectableText('آخر نسخة يدوية: $_savedPath'),
                  ),
                const SizedBox(height: 18),
                _problemLogSection(),
                const SizedBox(height: 18),
                Text(
                  'سجل التغييرات',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 12),
              ],
              child: widget.store.audit.isEmpty
                  ? const EmptySection(
                      message:
                          'سيظهر هنا تاريخ العمليات واسم الموظف الذي نفّذها.',
                    )
                  : ManagementTable(
                      columns: const [
                        'التاريخ',
                        'العملية',
                        'التفاصيل',
                        'الموظف',
                      ],
                      rows: widget.store.audit.reversed
                          .map(
                            (record) => DataRow(
                              cells: [
                                DataCell(
                                  Text(
                                    '${shortDate(record.createdAt)}\n${TimeOfDay.fromDateTime(record.createdAt).format(context)}',
                                  ),
                                ),
                                DataCell(Text(_actionLabel(record.action))),
                                DataCell(
                                  SizedBox(
                                    width: 420,
                                    child: Text(
                                      record.description,
                                      maxLines: 3,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                ),
                                DataCell(
                                  Text(
                                    widget.store.staff
                                            .where(
                                              (staff) =>
                                                  staff.id == record.staffId,
                                            )
                                            .firstOrNull
                                            ?.name ??
                                        '—',
                                  ),
                                ),
                              ],
                            ),
                          )
                          .toList(),
                    ),
            ),
          ),
        );
}
