import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../shared/formatters.dart';
import '../../shared/scrollable_dialog.dart';
import 'entry_confirmation_dialog.dart' show EntryConfirmationDialog;
import 'paid_amount_fields.dart';

class PaidAmountPriceOption {
  const PaidAmountPriceOption({
    required this.id,
    required this.label,
    required this.dueAmount,
  });
  final int id;
  final String label;
  final int? dueAmount;
}

class PaidAmountSelection {
  const PaidAmountSelection({
    required this.optionId,
    required this.dueAmount,
    required this.method,
    this.paidAmount,
    this.groupId,
  });
  final int optionId;
  final int dueAmount;
  final int? paidAmount;
  final String? groupId;
  final String method;
  int get collectedAmount => paidAmount ?? dueAmount;
}

class PaidAmountGroupOption {
  const PaidAmountGroupOption({
    required this.id,
    required this.label,
    required this.prices,
  });
  final String id, label;
  final List<PaidAmountPriceOption> prices;
}

class PaidAmountDialog extends StatefulWidget {
  const PaidAmountDialog({
    super.key,
    required this.title,
    required this.studentLabel,
    required this.options,
    required this.initialOptionId,
    required this.method,
    required this.description,
    this.groups = const [],
    this.initialGroupId,
    this.paymentMethods = const [],
    this.scannerBlocked,
  });
  final String title, studentLabel, method, description;
  final List<PaidAmountPriceOption> options;
  final int initialOptionId;
  final List<PaidAmountGroupOption> groups;
  final String? initialGroupId;
  final List<String> paymentMethods;
  final ValueNotifier<bool>? scannerBlocked;

  @override
  State<PaidAmountDialog> createState() => _PaidAmountDialogState();
}

class _PaidAmountDialogState extends State<PaidAmountDialog> {
  late int _optionId;
  late final PaidAmountDraft _draft;
  LogicalKeyboardKey? _armed;
  bool _finished = false, _receivedScannerText = false;
  final _cancelFocus = FocusNode();
  bool get _scannerBlocked =>
      _receivedScannerText || widget.scannerBlocked?.value == true;
  String? _groupId;
  late String _method;

  List<PaidAmountPriceOption> get _prices => widget.groups.isEmpty
      ? widget.options
      : widget.groups.firstWhere((group) => group.id == _groupId).prices;
  PaidAmountPriceOption get _option =>
      _prices.firstWhere((option) => option.id == _optionId);
  @override
  void initState() {
    super.initState();
    _optionId = widget.initialOptionId;
    _method = widget.method;
    _groupId =
        widget.initialGroupId ??
        (widget.groups.isEmpty ? null : widget.groups.first.id);
    _draft = PaidAmountDraft(_option.dueAmount)..addListener(_refresh);
    widget.scannerBlocked?.addListener(_refresh);
  }

  void _refresh() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _draft.removeListener(_refresh);
    widget.scannerBlocked?.removeListener(_refresh);
    _draft.dispose();
    _cancelFocus.dispose();
    super.dispose();
  }

  void _cancel() {
    if (_finished) return;
    _finished = true;
    Navigator.pop(context);
  }

  void _confirm() {
    if (_finished || _scannerBlocked || !_draft.valid) return;
    _finished = true;
    Navigator.pop(
      context,
      PaidAmountSelection(
        optionId: _optionId,
        dueAmount: _draft.dueAmount!,
        paidAmount: _draft.paidAmount,
        groupId: _groupId,
        method: _method,
      ),
    );
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (_finished) return KeyEventResult.handled;
    final key = event.logicalKey;
    if (key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter ||
        key == LogicalKeyboardKey.escape) {
      final keyboard = HardwareKeyboard.instance;
      if (event.synthesized ||
          keyboard.isControlPressed ||
          keyboard.isAltPressed ||
          keyboard.isMetaPressed ||
          keyboard.isShiftPressed ||
          event is KeyRepeatEvent) {
        _armed = null;
      } else if (event is KeyDownEvent) {
        _armed = key;
      } else if (event is KeyUpEvent) {
        final armed = _armed == key;
        _armed = null;
        if (armed) {
          if (key == LogicalKeyboardKey.escape || _cancelFocus.hasFocus) {
            _cancel();
          } else {
            _confirm();
          }
        }
      }
      return KeyEventResult.handled;
    }
    if (_draft.acceptsAmountKey(event) || key == LogicalKeyboardKey.tab) {
      return KeyEventResult.ignored;
    }
    if (EntryConfirmationDialog.isScannerText(event)) {
      setState(() => _receivedScannerText = true);
      return KeyEventResult.handled;
    }
    return KeyEventResult.ignored;
  }

  @override
  Widget build(BuildContext context) => Directionality(
    textDirection: TextDirection.rtl,
    child: Focus(
      autofocus: true,
      onKeyEvent: _key,
      child: ScrollableMassarDialog(
        title: Text(widget.title),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              widget.studentLabel,
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            if (widget.groups.isNotEmpty) ...[
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _groupId,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'المجموعة'),
                items: widget.groups
                    .map(
                      (group) => DropdownMenuItem(
                        value: group.id,
                        child: Tooltip(
                          message: group.label,
                          child: Text(
                            group.label,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ),
                    )
                    .toList(),
                onChanged: _scannerBlocked
                    ? null
                    : (id) {
                        _groupId = id!;
                        _optionId = _prices.first.id;
                        _draft.updateDueAmount(_option.dueAmount);
                      },
              ),
            ],
            if (_prices.length > 1) ...[
              const SizedBox(height: 12),
              DropdownButtonFormField<int>(
                key: ValueKey('renew-month-$_groupId-$_optionId'),
                initialValue: _optionId,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'الشهر'),
                items: _prices
                    .map(
                      (option) => DropdownMenuItem(
                        value: option.id,
                        child: Tooltip(
                          message: option.label,
                          child: Text(
                            option.label,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ),
                    )
                    .toList(),
                onChanged: _scannerBlocked
                    ? null
                    : (id) {
                        _optionId = id!;
                        _draft.updateDueAmount(_option.dueAmount);
                      },
              ),
            ],
            const SizedBox(height: 12),
            Text(
              _option.label,
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            Text(
              'المطلوب: ${_draft.dueAmount == null ? 'السعر غير محدد في المجموعة' : money(_draft.dueAmount!)}',
            ),
            if (widget.paymentMethods.isEmpty)
              Text('طريقة الدفع: $_method')
            else
              DropdownButtonFormField<String>(
                initialValue: _method,
                decoration: const InputDecoration(labelText: 'طريقة الدفع'),
                items: widget.paymentMethods
                    .map(
                      (method) =>
                          DropdownMenuItem(value: method, child: Text(method)),
                    )
                    .toList(),
                onChanged: _scannerBlocked
                    ? null
                    : (method) => setState(() => _method = method!),
              ),
            PaidAmountFields(draft: _draft, enabled: !_scannerBlocked),
            const SizedBox(height: 12),
            Text(
              _scannerBlocked
                  ? 'وصل كود أثناء المراجعة. ألغِ الحوار ثم امسح الطالب من جديد.'
                  : widget.description,
            ),
          ],
        ),
        actions: [
          TextButton(
            focusNode: _cancelFocus,
            onPressed: _cancel,
            child: const Text('إلغاء · Esc'),
          ),
          FilledButton(
            key: const Key('confirm-paid-amount'),
            onPressed: _scannerBlocked || !_draft.valid ? null : _confirm,
            child: const Text('تأكيد التحصيل · Enter'),
          ),
        ],
      ),
    ),
  );
}
