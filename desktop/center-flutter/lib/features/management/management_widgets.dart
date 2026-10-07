import 'package:massar_center/shared/problem_reporting.dart';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/theme.dart';
import '../../shared/notice_dialog.dart';
import '../../shared/scrollable_dialog.dart';
export '../../shared/notice_dialog.dart' show NoticeKind;

class ManagementPanel extends StatelessWidget {
  const ManagementPanel({
    super.key,
    required this.title,
    required this.subtitle,
    required this.child,
    this.actions = const [],
  });

  final String title;
  final String subtitle;
  final Widget child;
  final List<Widget> actions;

  @override
  Widget build(BuildContext context) {
    final body = child is ManagementBody ? child as ManagementBody : null;
    return Padding(
      padding: const EdgeInsets.all(16),
      child: ManagementBody(
        header: [
          LayoutBuilder(
            builder: (context, constraints) => Wrap(
              alignment: WrapAlignment.spaceBetween,
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 24,
              runSpacing: 12,
              children: [
                ConstrainedBox(
                  constraints: BoxConstraints(maxWidth: constraints.maxWidth),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: Theme.of(context).textTheme.headlineSmall,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        subtitle,
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ),
                if (actions.isNotEmpty)
                  Wrap(spacing: 8, runSpacing: 8, children: actions),
              ],
            ),
          ),
          const SizedBox(height: 12),
          ...?body?.header,
        ],
        child: body?.child ?? child,
      ),
    );
  }
}

/// Filters and forms have their natural height and scroll together with the
/// page heading. Data panes retain bounded space for their own tables/editors.
class ManagementBody extends StatefulWidget {
  const ManagementBody({
    super.key,
    this.header = const [],
    required this.child,
  });
  final List<Widget> header;
  final Widget child;

  @override
  State<ManagementBody> createState() => _ManagementBodyState();
}

class _ManagementBodyState extends State<ManagementBody> {
  final _scroll = ScrollController();

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final minimumPaneHeight = MediaQuery.textScalerOf(context).scale(320);
    return ScrollConfiguration(
      behavior: ScrollConfiguration.of(context).copyWith(scrollbars: false),
      child: Scrollbar(
        controller: _scroll,
        thumbVisibility: true,
        interactive: true,
        child: CustomScrollView(
          controller: _scroll,
          primary: false,
          slivers: [
            SliverToBoxAdapter(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: widget.header,
              ),
            ),
            SliverLayoutBuilder(
              builder: (context, constraints) => SliverToBoxAdapter(
                child: SizedBox(
                  height: constraints.remainingPaintExtent.clamp(
                    minimumPaneHeight,
                    double.infinity,
                  ),
                  child: widget.child,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class EmptySection extends StatelessWidget {
  const EmptySection({super.key, required this.message});
  final String message;

  @override
  Widget build(BuildContext context) => Center(
    child: MassarScrollView(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 440),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.folder_open_outlined,
              size: 42,
              color: MassarPalette.of(context).accent,
            ),
            const SizedBox(height: 18),
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.titleMedium,
            ),
          ],
        ),
      ),
    ),
  );
}

class ManagementTable extends StatefulWidget {
  const ManagementTable({
    super.key,
    required this.columns,
    required this.rows,
    this.pageSize = 50,
  }) : _rowCount = null,
       _rowBuilder = null;
  const ManagementTable.builder({
    super.key,
    required this.columns,
    required int rowCount,
    required DataRow Function(int) rowBuilder,
    this.pageSize = 50,
  }) : rows = const [],
       _rowCount = rowCount,
       _rowBuilder = rowBuilder;

  final List<String> columns;
  final List<DataRow> rows;
  final int pageSize;
  final int? _rowCount;
  final DataRow Function(int)? _rowBuilder;
  int get _length => _rowCount ?? rows.length;
  DataRow _rowAt(int index) => _rowBuilder?.call(index) ?? rows[index];

  @override
  State<ManagementTable> createState() => _ManagementTableState();
}

class _ManagementTableState extends State<ManagementTable> {
  int _page = 0;

  @override
  Widget build(BuildContext context) {
    final pageCount = (widget._length / widget.pageSize).ceil();
    final page = pageCount == 0 ? 0 : _page.clamp(0, pageCount - 1);
    final start = page * widget.pageSize;
    final end = (start + widget.pageSize).clamp(0, widget._length);
    final visibleRows = [
      for (var index = start; index < end; index++) widget._rowAt(index),
    ];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Expanded(
          child: LayoutBuilder(
            builder: (context, constraints) => MassarScrollView(
              child: MassarScrollView(
                scrollDirection: Axis.horizontal,
                child: ConstrainedBox(
                  constraints: BoxConstraints(minWidth: constraints.maxWidth),
                  child: DataTable(
                    headingRowColor: WidgetStateProperty.all(
                      MassarPalette.of(context).tableHeader,
                    ),
                    headingTextStyle: TextStyle(
                      fontFamily: 'Tajawal',
                      fontWeight: FontWeight.w700,
                      color: MassarPalette.of(context).ink,
                    ),
                    dataRowMinHeight: 54,
                    dataRowMaxHeight: 72,
                    columnSpacing: 26,
                    columns: widget.columns
                        .map((label) => DataColumn(label: Text(label)))
                        .toList(),
                    rows: visibleRows,
                  ),
                ),
              ),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.only(top: 12),
          child: Row(
            children: [
              Text(
                widget._length == 0
                    ? 'لا توجد سجلات'
                    : '${start + 1}–${start + visibleRows.length} من ${widget._length}',
              ),
              const Spacer(),
              if (pageCount > 1) ...[
                IconButton(
                  tooltip: 'الصفحة السابقة',
                  onPressed: page == 0
                      ? null
                      : () => setState(() => _page = page - 1),
                  icon: const Icon(Icons.chevron_right),
                ),
                Text('${page + 1} / $pageCount'),
                IconButton(
                  tooltip: 'الصفحة التالية',
                  onPressed: page + 1 >= pageCount
                      ? null
                      : () => setState(() => _page = page + 1),
                  icon: const Icon(Icons.chevron_left),
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

Future<bool> confirmManagement(
  BuildContext context, {
  required String title,
  required String description,
  String confirmLabel = 'تأكيد',
  bool destructive = false,
}) async =>
    await showDialog<bool>(
      context: context,
      barrierDismissible: !destructive,
      builder: (dialogContext) => ScrollableMassarDialog(
        title: Row(
          children: [
            Icon(
              destructive ? Icons.warning_amber_rounded : Icons.help_outline,
              color: destructive
                  ? Theme.of(context).colorScheme.error
                  : MassarPalette.of(context).accent,
            ),
            const SizedBox(width: 12),
            Expanded(child: Text(title)),
          ],
        ),
        content: Text(description),
        actions: [
          TextButton(
            autofocus: destructive,
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('رجوع'),
          ),
          FilledButton(
            autofocus: !destructive,
            style: destructive
                ? FilledButton.styleFrom(
                    backgroundColor: Theme.of(context).colorScheme.error,
                    foregroundColor: Theme.of(context).colorScheme.onError,
                  )
                : null,
            onPressed: () => Navigator.pop(dialogContext, true),
            child: Text(confirmLabel),
          ),
        ],
      ),
    ) ??
    false;

Future<void> showManagementMessage(
  BuildContext context,
  String message, {
  NoticeKind kind = NoticeKind.success,
}) => showMassarNotice(context, message, kind: kind);

String managementError(Object error) => switch (error) {
  CenterException exception => exception.message,
  FileSystemException _ =>
    'تعذر قراءة أو حفظ الملف. راجع المسار وصلاحية الكتابة وحاول مرة أخرى.',
  FormatException _ => 'صيغة الملف غير صحيحة.',
  _ => 'تعذر إتمام العملية: $error',
};

String? requiredText(String? text) =>
    (text?.trim().isEmpty ?? true) ? 'هذا الحقل مطلوب' : null;

String? positiveNumber(String? text) {
  final number = int.tryParse(text ?? '');
  return number == null || number < 1 ? 'اكتب عددًا صحيحًا أكبر من صفر' : null;
}

int? piastresFromText(String text) {
  final normalized = text
      .trim()
      .split('')
      .map((character) {
        const arabicDigits = '٠١٢٣٤٥٦٧٨٩';
        const persianDigits = '۰۱۲۳۴۵۶۷۸۹';
        final arabicIndex = arabicDigits.indexOf(character);
        final persianIndex = persianDigits.indexOf(character);
        if (arabicIndex >= 0) return '$arabicIndex';
        if (persianIndex >= 0) return '$persianIndex';
        return character;
      })
      .join()
      .replaceAll('٬', '')
      .replaceAll('٫', '.')
      .replaceAll(',', '.');
  if (!RegExp(r'^\d+(\.\d{1,2})?$').hasMatch(normalized)) return null;
  final parts = normalized.split('.');
  final value =
      BigInt.parse(parts.first) * BigInt.from(100) +
      (parts.length == 1
          ? BigInt.zero
          : BigInt.parse(parts.last.padRight(2, '0')));
  // Keep amounts exact across desktop and JSON transports.
  if (value > BigInt.from(9007199254740991)) return null;
  return value.toInt();
}

String? validPrice(String? text) => piastresFromText(text ?? '') == null
    ? 'اكتب سعرًا بالجنيه، حتى رقمين بعد الفاصلة'
    : null;
String priceText(int amount) => (amount / 100).toStringAsFixed(2);

class ManagementEditor extends StatefulWidget {
  const ManagementEditor({
    super.key,
    required this.title,
    required this.fields,
    required this.onSave,
    required this.controllers,
    this.saveLabel = 'حفظ',
    this.hasUnsavedChanges = false,
  });
  final String title;
  final List<Widget> fields;
  final Future<void> Function() onSave;
  final List<TextEditingController> controllers;
  final String saveLabel;
  final bool hasUnsavedChanges;

  @override
  State<ManagementEditor> createState() => _ManagementEditorState();
}

class _ManagementEditorState extends State<ManagementEditor> {
  final _form = GlobalKey<FormState>();
  final _scroll = ScrollController();
  bool _busy = false, _formChanged = false, _allowClose = false;
  bool _confirmingDiscard = false, _submitted = false;
  String? _saveError;
  bool get _dirty => _formChanged || widget.hasUnsavedChanges;

  @override
  void dispose() {
    _scroll.dispose();
    for (final controller in widget.controllers) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _finish(bool saved) async {
    if (_allowClose || !mounted) return;
    setState(() => _allowClose = true);
    await WidgetsBinding.instance.endOfFrame;
    if (mounted && ModalRoute.of(context)?.isCurrent == true) {
      Navigator.of(context).pop(saved ? true : null);
    }
  }

  Future<void> _requestClose() async {
    if (_busy ||
        _allowClose ||
        _confirmingDiscard ||
        !mounted ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    if (!_dirty) {
      await _finish(false);
      return;
    }
    _confirmingDiscard = true;
    final discard = await confirmManagement(
      context,
      title: 'الخروج بدون حفظ؟',
      description:
          'فيه تعديلات لم تُحفظ في «${widget.title}». تقدر ترجع تكملها أو تخرج وتتجاهلها.',
      confirmLabel: 'تجاهل التعديلات والخروج',
      destructive: true,
    );
    _confirmingDiscard = false;
    if (mounted && discard) await _finish(false);
  }

  Future<void> _save() async {
    if (_busy ||
        _allowClose ||
        _confirmingDiscard ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    setState(() {
      _submitted = true;
      _saveError = null;
    });
    final invalid = _form.currentState!.validateGranularly();
    if (invalid.isNotEmpty) {
      final field = invalid.first;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || !field.mounted) return;
        Scrollable.ensureVisible(
          field.context,
          alignment: 0.15,
          duration: MediaQuery.disableAnimationsOf(context)
              ? Duration.zero
              : const Duration(milliseconds: 150),
        );
      });
      return;
    }
    setState(() {
      _busy = true;
    });
    try {
      await widget.onSave();
      if (mounted) {
        await showMassarNotice(
          context,
          'حُفظت البيانات بنجاح.',
          kind: NoticeKind.success,
        );
        if (mounted) await _finish(true);
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.management_widgets');
      if (mounted) {
        setState(() {
          _busy = false;
          _saveError = managementError(error);
        });
        if (_scroll.hasClients) {
          await _scroll.animateTo(
            0,
            duration: MediaQuery.disableAnimationsOf(context)
                ? Duration.zero
                : const Duration(milliseconds: 150),
            curve: Curves.easeOut,
          );
        }
      }
    }
  }

  @override
  Widget build(BuildContext context) => PopScope<Object?>(
    canPop: _allowClose || (!_busy && !_dirty && !_confirmingDiscard),
    onPopInvokedWithResult: (didPop, _) {
      if (!didPop) _requestClose();
    },
    child: CallbackShortcuts(
      bindings: {
        const SingleActivator(
          LogicalKeyboardKey.keyS,
          control: true,
          includeRepeats: false,
        ): _save,
        const SingleActivator(
          LogicalKeyboardKey.keyS,
          meta: true,
          includeRepeats: false,
        ): _save,
        const SingleActivator(LogicalKeyboardKey.escape, includeRepeats: false):
            _requestClose,
      },
      child: ScrollableMassarDialog(
        title: Text(widget.title),
        scrollController: _scroll,
        content: ExcludeFocus(
          excluding: _busy,
          child: AbsorbPointer(
            absorbing: _busy,
            child: Form(
              key: _form,
              autovalidateMode: _submitted
                  ? AutovalidateMode.onUserInteraction
                  : AutovalidateMode.disabled,
              onChanged: () {
                if (!_formChanged) setState(() => _formChanged = true);
              },
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (_saveError != null)
                    Semantics(
                      liveRegion: true,
                      child: Container(
                        margin: const EdgeInsets.only(bottom: 16),
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: Theme.of(context).colorScheme.errorContainer,
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          'تعذر إتمام الحفظ أو تأكيده. $_saveError\nالبيانات التي كتبتها موجودة في النافذة؛ راجع الرسالة قبل المحاولة مرة أخرى.',
                          style: TextStyle(
                            color: Theme.of(
                              context,
                            ).colorScheme.onErrorContainer,
                          ),
                        ),
                      ),
                    ),
                  for (final field in widget.fields)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 16),
                      child: field,
                    ),
                ],
              ),
            ),
          ),
        ),
        actions: [
          Text(
            'Ctrl / ⌘ + S للحفظ · Esc للرجوع',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          TextButton(
            onPressed: _busy ? null : _requestClose,
            child: const Text('رجوع'),
          ),
          FilledButton.icon(
            onPressed: _busy ? null : _save,
            icon: _busy
                ? const SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.save_outlined, size: 18),
            label: Text(_busy ? 'جارٍ الحفظ…' : widget.saveLabel),
          ),
        ],
      ),
    ),
  );
}
