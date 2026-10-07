import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';
import 'paid_amount_fields.dart';

class PackageConfirmationPreview {
  const PackageConfirmationPreview({
    required this.request,
    this.quote,
    this.error,
  });
  final EntryRequest request;
  final EntryConfirmation? quote;
  final String? error;
}

/// Keeps the selected month preview and scanner guard across route focus changes.
class PackageConfirmationController
    extends ValueNotifier<PackageConfirmationPreview> {
  PackageConfirmationController({
    required PackageConfirmationPreview preview,
    required this.scannerBlocked,
    required this.studentCodes,
    required this.previewForPlan,
    required this.plans,
  }) : super(preview) {
    validateCodePrefix();
  }
  final ValueNotifier<bool> scannerBlocked;
  final Iterable<String> Function() studentCodes;
  final PackageConfirmationPreview Function(GroupMonthPlan) previewForPlan;
  final List<GroupMonthPlan> plans;

  bool _pendingConfirmation = false;
  bool _sealed = false;

  bool validateCodePrefix() {
    if (studentCodes().any((code) {
      final normalized = code.trim().toLowerCase();
      return normalized == 'n';
    })) {
      scannerBlocked.value = true;
    }
    return !scannerBlocked.value;
  }

  void choosePlan(String id) {
    if (_sealed || scannerBlocked.value) return;
    final matches = plans.where((plan) => plan.id == id);
    if (matches.isEmpty) return;
    _pendingConfirmation = false;
    value = previewForPlan(matches.first);
  }

  bool handleSequenceKey(KeyEvent event) {
    if (!EntryConfirmationDialog.isScannerText(event)) return false;
    scannerBlocked.value = true;
    return true;
  }

  void requestPendingConfirmation() {
    if (_sealed || !validateCodePrefix() || value.quote == null) return;
    _pendingConfirmation = true;
    notifyListeners();
  }

  bool takePendingConfirmation() {
    final pending = _pendingConfirmation;
    _pendingConfirmation = false;
    return pending;
  }

  void seal() {
    _sealed = true;
    _pendingConfirmation = false;
  }
}

/// A frozen shortcut preview. Printable scanner input invalidates this preview.
class EntryConfirmationDialog extends StatefulWidget {
  const EntryConfirmationDialog({
    super.key,
    required this.student,
    required this.session,
    required this.groupLabel,
    required this.request,
    required this.quote,
    required this.scannerBlocked,
    this.originalAbsenceLabel,
    this.packageConfirmation,
    this.retainedSessionPayment = false,
    this.attendanceRecorded = false,
    this.onPaidAmountConfirmed,
  });
  final Student student;
  final LessonSession session;
  final String groupLabel;
  final EntryRequest request;
  final EntryConfirmation? quote;
  final ValueNotifier<bool> scannerBlocked;
  final String? originalAbsenceLabel;
  final PackageConfirmationController? packageConfirmation;
  final bool retainedSessionPayment;
  final bool attendanceRecorded;
  final ValueChanged<int?>? onPaidAmountConfirmed;

  static bool isScannerText(KeyEvent event) {
    final key = event.logicalKey;
    if (event is! KeyDownEvent ||
        key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter ||
        key == LogicalKeyboardKey.escape ||
        key == LogicalKeyboardKey.tab ||
        HardwareKeyboard.instance.isControlPressed ||
        HardwareKeyboard.instance.isAltPressed ||
        HardwareKeyboard.instance.isMetaPressed) {
      return false;
    }
    // Some desktop keypad events have a key label but no printable character.
    final keyId = event.logicalKey.keyId;
    if (keyId >= LogicalKeyboardKey.numpad0.keyId &&
        keyId <= LogicalKeyboardKey.numpad9.keyId) {
      return true;
    }
    final text = event.character ?? event.logicalKey.keyLabel;
    return text.runes.any(
          (code) => code >= 32 && !(code >= 127 && code <= 159),
        ) &&
        (event.character != null || text.runes.length == 1);
  }

  @override
  State<EntryConfirmationDialog> createState() =>
      _EntryConfirmationDialogState();
}

class _EntryConfirmationDialogState extends State<EntryConfirmationDialog> {
  bool _closed = false;
  final _scroll = ScrollController();
  final _cancelFocus = FocusNode();
  bool _pendingScheduled = false;
  PackageConfirmationPreview? _renderedPreview;
  late final PaidAmountDraft _payment;
  EntryRequest get _request =>
      widget.packageConfirmation?.value.request ?? widget.request;
  EntryConfirmation? get _quote => widget.packageConfirmation == null
      ? widget.quote
      : widget.packageConfirmation!.value.quote;

  @override
  void initState() {
    super.initState();
    _payment = PaidAmountDraft(_quote?.netAmount)..addListener(_paymentChanged);
    widget.packageConfirmation?.addListener(_previewChanged);
    _schedulePendingConfirmation();
  }

  void _previewChanged() {
    if (!mounted) return;
    _payment.updateDueAmount(_quote?.netAmount);
    setState(() {});
    _schedulePendingConfirmation();
  }

  void _paymentChanged() {
    if (mounted) setState(() {});
  }

  void _schedulePendingConfirmation() {
    if (_pendingScheduled || widget.packageConfirmation == null) return;
    _pendingScheduled = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _pendingScheduled = false;
      if (mounted && widget.packageConfirmation!.takePendingConfirmation()) {
        _finish(true);
      }
    });
  }

  @override
  void dispose() {
    widget.packageConfirmation?.removeListener(_previewChanged);
    _payment.removeListener(_paymentChanged);
    _payment.dispose();
    _scroll.dispose();
    _cancelFocus.dispose();
    super.dispose();
  }

  void _finish(bool confirmed) {
    if (_closed ||
        (confirmed &&
            (_quote == null ||
                (_hasNewPayment && !_payment.valid) ||
                widget.scannerBlocked.value ||
                widget.packageConfirmation?.validateCodePrefix() == false))) {
      return;
    }
    _closed = true;
    widget.packageConfirmation?.seal();
    if (confirmed) {
      widget.onPaidAmountConfirmed?.call(
        _hasNewPayment ? _payment.paidAmount : null,
      );
    }
    Navigator.of(context).pop(confirmed);
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (_closed) return KeyEventResult.handled;
    final key = event.logicalKey;
    if (event is KeyDownEvent &&
        !event.synthesized &&
        key == LogicalKeyboardKey.escape) {
      _finish(false);
    } else if (key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter) {
      if (event is KeyDownEvent &&
          !event.synthesized &&
          !HardwareKeyboard.instance.isControlPressed &&
          !HardwareKeyboard.instance.isAltPressed &&
          !HardwareKeyboard.instance.isMetaPressed &&
          !HardwareKeyboard.instance.isShiftPressed) {
        if (!_cancelFocus.hasFocus &&
            widget.packageConfirmation != null &&
            !identical(_renderedPreview, widget.packageConfirmation!.value)) {
          widget.packageConfirmation!.requestPendingConfirmation();
        } else {
          _finish(!_cancelFocus.hasFocus);
        }
      }
    } else if (_hasNewPayment && _payment.acceptsAmountKey(event)) {
      return KeyEventResult.ignored;
    } else if (widget.packageConfirmation?.handleSequenceKey(event) == true) {
      // Printable scanner input cancels the frozen preview.
    } else if (EntryConfirmationDialog.isScannerText(event)) {
      widget.scannerBlocked.value = true;
    } else if (key == LogicalKeyboardKey.tab) {
      return KeyEventResult.ignored;
    } else if (event is KeyDownEvent && _scroll.hasClients) {
      final offset = switch (key) {
        LogicalKeyboardKey.arrowDown => 48.0,
        LogicalKeyboardKey.arrowUp => -48.0,
        LogicalKeyboardKey.pageDown => 200.0,
        LogicalKeyboardKey.pageUp => -200.0,
        _ => 0.0,
      };
      if (offset != 0) {
        _scroll.jumpTo(
          (_scroll.offset + offset).clamp(0, _scroll.position.maxScrollExtent),
        );
      }
    }
    // Popup keys never reach attendance actions or the underlying code field.
    return KeyEventResult.handled;
  }

  bool get _hasNewPayment {
    if (_quote?.centerFeeOnly == true) {
      return (_quote?.netAmount ?? 0) > 0;
    }
    return !widget.retainedSessionPayment &&
        !(_request.mode == EntryMode.makeup &&
            _request.makeupSourceGroupId == null) &&
        !(widget.session.kind == SessionKind.free &&
            _request.makeupSourceGroupId == null) &&
        !(_request.makeupSourceGroupId != null &&
            (_quote?.eligibleRemaining ?? 0) > 0) &&
        !(widget.session.kind == SessionKind.counted &&
            (_quote?.eligibleRemaining ?? 0) > 0);
  }

  String get _action {
    if (_quote?.centerFeeOnly == true) {
      return 'تحصيل رسوم السنتر فقط — المدرس معفى ١٠٠٪';
    }
    if (widget.retainedSessionPayment) return 'مسددة مسبقًا — تسجيل حضور';
    if (_request.makeupSourceGroupId != null) {
      if ((_quote?.eligibleRemaining ?? 0) > 0) {
        return 'تعويض — خصم حصة من باقة المجموعة الأصلية';
      }
      return _request.mode == EntryMode.package
          ? 'شراء ${_quote?.monthPlanName ?? "الشهر"} (${_quote?.monthPlanSessions ?? _request.packageSessions} حصص) في المجموعة الأصلية وتحضير التعويض'
          : 'دفع حصة في المجموعة الأصلية وتحضير التعويض';
    }
    if (_request.mode == EntryMode.makeup) {
      return _request.makeupSourceGroupId != null
          ? 'تعويض — خصم حصة من باقة المجموعة الأصلية'
          : 'تعويض عن غياب محسوب سابقًا من الباقة';
    }
    if (widget.session.kind == SessionKind.free) return 'حضور مجاني';
    if (widget.session.kind == SessionKind.extra) return 'دفع حصة إضافية';
    if ((_quote?.eligibleRemaining ?? 0) > 0) {
      return 'دخول من الباقة السارية بدون دفع جديد';
    }
    if (_request.mode == EntryMode.package) {
      return 'شراء ${_quote?.monthPlanName ?? "الشهر"} (${_quote?.monthPlanSessions ?? _request.packageSessions} حصص) وتسجيل الحضور';
    }
    return 'دفع الحصة وتسجيل الحضور';
  }

  int get _remainingAfter {
    if (_quote?.centerFeeOnly == true) {
      return _quote?.eligibleRemaining ?? 0;
    }
    if (widget.retainedSessionPayment) return _quote?.eligibleRemaining ?? 0;
    if (_request.makeupSourceGroupId != null &&
        (_quote?.eligibleRemaining ?? 0) > 0) {
      return _quote!.eligibleRemaining - 1;
    }
    if (_request.makeupSourceGroupId != null) {
      return _request.mode == EntryMode.package
          ? (_quote?.monthPlanSessions ?? _request.packageSessions) - 1
          : 0;
    }
    if (widget.retainedSessionPayment ||
        widget.session.kind != SessionKind.counted ||
        _request.mode == EntryMode.makeup) {
      return _quote?.eligibleRemaining ?? 0;
    }
    if ((_quote?.eligibleRemaining ?? 0) > 0) {
      return _quote!.eligibleRemaining - 1;
    }
    return _request.mode == EntryMode.package
        ? (_quote?.monthPlanSessions ?? _request.packageSessions) - 1
        : 0;
  }

  @override
  Widget build(BuildContext context) {
    _renderedPreview = widget.packageConfirmation?.value;
    final palette = MassarPalette.of(context);
    return Focus(
      autofocus: true,
      onKeyEvent: _key,
      child: ValueListenableBuilder<bool>(
        valueListenable: widget.scannerBlocked,
        builder: (context, blocked, _) => ScrollableMassarDialog(
          key: const Key('entry-confirmation-dialog'),
          scrollController: _scroll,
          title: const Text('تأكيد الدخول والتحصيل'),
          content: SizedBox(
            width: 560,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  widget.student.name,
                  key: const Key('confirmation-student-name'),
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(height: 4),
                Text('كود الطالب: ${widget.student.code}'),
                if (_request.mode == EntryMode.makeup ||
                    _request.makeupSourceGroupId != null)
                  Text(
                    'معوّض',
                    style: TextStyle(
                      color: palette.error,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                Text(widget.groupLabel),
                Text(
                  'حصة ${widget.session.number} · ${sessionDateLabel(widget.session)} · ${switch (widget.session.kind) {
                    SessionKind.counted => 'ضمن الباقة',
                    SessionKind.free => 'مجانية',
                    SessionKind.extra => 'إضافية',
                  }}',
                ),
                const SizedBox(height: 16),
                Text(
                  _action,
                  key: const Key('confirmation-action'),
                  style: TextStyle(
                    color: palette.accent,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                if (widget.originalAbsenceLabel != null)
                  Text(widget.originalAbsenceLabel!),
                if (widget.packageConfirmation != null) ...[
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    key: ValueKey('confirmation-month-${_request.monthPlanId}'),
                    initialValue: _request.monthPlanId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'الشهر'),
                    items: widget.packageConfirmation!.plans
                        .map(
                          (plan) => DropdownMenuItem(
                            value: plan.id,
                            child: Tooltip(
                              message:
                                  '${plan.name} · ${plan.sessions} حصص · ${money(plan.price)}',
                              child: Text(
                                '${plan.name} · ${plan.sessions} حصص',
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          ),
                        )
                        .toList(),
                    onChanged: blocked
                        ? null
                        : (id) {
                            if (id != null) {
                              widget.packageConfirmation!.choosePlan(id);
                            }
                          },
                  ),
                ],
                const SizedBox(height: 12),
                if (widget.packageConfirmation?.value.error != null)
                  Text(
                    widget.packageConfirmation!.value.error!,
                    key: const Key('confirmation-price-error'),
                    style: TextStyle(color: palette.error),
                  ),
                if (_quote?.centerFeeOnly == true)
                  _row('دفع المدرس', 'معفى ١٠٠٪ — لا استهلاك للباقة')
                else ...[
                  _row(
                    'السعر قبل الخصم',
                    _quote == null
                        ? 'السعر غير متاح'
                        : money(_quote!.baseAmount),
                  ),
                  _row(
                    'الخصم الثابت',
                    '${percentText(_quote?.discountPercent ?? widget.student.discountPercent)}٪',
                  ),
                ],
                _row(
                  _quote?.centerFeeOnly == true
                      ? 'رسوم السنتر المطلوبة'
                      : 'المبلغ المطلوب',
                  _quote == null
                      ? 'راجع السعر أولًا'
                      : money(_quote!.netAmount),
                  important: true,
                  key: const Key('confirmation-net'),
                ),
                _row(
                  'طريقة الدفع',
                  _hasNewPayment ? _request.method : 'لا يوجد تحصيل جديد',
                ),
                if (_hasNewPayment)
                  PaidAmountFields(draft: _payment, enabled: !blocked),
                if (_quote?.centerFeeOnly != true &&
                    (_request.makeupSourceGroupId != null ||
                        widget.session.kind == SessionKind.counted &&
                            _request.mode != EntryMode.makeup)) ...[
                  _row(
                    'الرصيد المؤهل قبل الدخول',
                    _quote == null
                        ? 'غير مؤكد'
                        : '${_quote!.eligibleRemaining} حصص',
                  ),
                  _row(
                    'الرصيد المؤهل بعد الدخول',
                    _quote == null ? 'غير مؤكد' : '$_remainingAfter حصص',
                  ),
                ] else
                  _row('الباقة', 'لا يخصم من الباقة'),
                if (widget.retainedSessionPayment)
                  const Text(
                    'السداد السابق محفوظ؛ لا يوجد دفع جديد ولا تُخصم حصة من الباقة.',
                  ),
                const SizedBox(height: 12),
                Text(
                  blocked
                      ? 'وصل إدخال كود أثناء التأكيد. لن يُسجل هذا الطالب. اضغط Esc ثم امسح الكود من جديد.'
                      : _quote?.centerFeeOnly == true
                      ? 'المبلغ المدخل تحصيل فعلي لرسوم السنتر فقط. المتبقي محفوظ في سجل الرسوم؛ رصيد المدرس لا يُخصم.'
                      : _request.makeupSourceGroupId != null
                      ? (_quote?.eligibleRemaining ?? 0) > 0
                            ? 'Enter لتحضير التعويض وخصم حصة من الباقة · Esc للإلغاء.'
                            : 'الرصيد انتهى. Enter يؤكد الدفع وتحضير التعويض · Esc للإلغاء واختيار الشهر باستخدام N.'
                      : _request.mode == EntryMode.makeup
                      ? 'Enter لتحضير التعويض · Esc للإلغاء. الغياب الأصلي محسوب من الباقة سابقًا.'
                      : widget.attendanceRecorded
                      ? 'الحضور محفوظ بالفعل. Enter لتأكيد السداد · Esc للإلغاء بدون مسح الحضور.'
                      : 'Enter للتأكيد · Esc للإلغاء. لم يتم تسجيل حضور أو دفع بعد.',
                  key: const Key('confirmation-guidance'),
                  style: TextStyle(
                    color: blocked ? palette.error : palette.muted,
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              key: const Key('cancel-entry-confirmation'),
              focusNode: _cancelFocus,
              onPressed: () => _finish(false),
              child: const Text('إلغاء · Esc'),
            ),
            FilledButton(
              key: const Key('confirm-entry-confirmation'),
              onPressed:
                  blocked ||
                      _quote == null ||
                      (_hasNewPayment && !_payment.valid)
                  ? null
                  : () => _finish(true),
              child: const Text('تأكيد وتسجيل · Enter'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _row(String label, String value, {bool important = false, Key? key}) =>
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(
          children: [
            Expanded(child: Text(label)),
            const SizedBox(width: 12),
            Text(
              value,
              key: key,
              style: TextStyle(
                fontWeight: important ? FontWeight.bold : FontWeight.normal,
                fontSize: important ? 20 : null,
              ),
            ),
          ],
        ),
      );
}
