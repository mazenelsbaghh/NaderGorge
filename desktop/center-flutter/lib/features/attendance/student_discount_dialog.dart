import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/discount_calculation.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:massar_center/shared/theme.dart';

import '../management/management_widgets.dart' show piastresFromText, priceText;

class DiscountPriceOption {
  const DiscountPriceOption({
    required this.id,
    required this.label,
    required this.baseAmount,
  });
  final String id, label;
  final int baseAmount;
}

class StudentDiscountDialog extends StatefulWidget {
  const StudentDiscountDialog({
    super.key,
    required this.store,
    required this.studentId,
    required this.baseAmount,
    required this.priceLabel,
    this.priceOptions = const [],
    this.initialPriceId,
  });
  final CenterStore store;
  final String studentId, priceLabel;
  final int baseAmount;
  final List<DiscountPriceOption> priceOptions;
  final String? initialPriceId;

  @override
  State<StudentDiscountDialog> createState() => _StudentDiscountDialogState();
}

class _ArmDiscountSave extends Intent {
  const _ArmDiscountSave(this.key);
  final LogicalKeyboardKey key;
}

class _StudentDiscountDialogState extends State<StudentDiscountDialog> {
  static const _allPricesId = 'fixed-all';
  final _percent = TextEditingController();
  final _amount = TextEditingController();
  final _centerFee = TextEditingController();
  bool _centerOnly = false, _centerConfigEdited = false;
  final _percentFocus = FocusNode();
  final _amountFocus = FocusNode();
  final _cancelFocus = FocusNode();
  DiscountPriceOption? _price;
  num? _exactPercent;
  String? _percentError, _amountError;
  bool _approximate = false, _busy = false, _finished = false;
  bool _fixedForAll = false;
  LogicalKeyboardKey? _armedEnter, _armedEscape;
  bool _enterCancels = false;

  Student? get _student => widget.store.students
      .where((student) => student.id == widget.studentId)
      .firstOrNull;
  int get _baseAmount => _price?.baseAmount ?? widget.baseAmount;
  String get _priceLabel => _price?.label ?? widget.priceLabel;
  bool get _validDraft =>
      !_busy &&
      !_finished &&
      _student != null &&
      _exactPercent != null &&
      _percentError == null &&
      _amountError == null &&
      (!_centerOnly || (piastresFromText(_centerFee.text) ?? 0) > 0);
  bool get _canSave => _validDraft && widget.store.canEditDiscount;

  @override
  void initState() {
    super.initState();
    widget.store.addListener(_refreshStudent);
    _price = widget.priceOptions
        .where((option) => option.id == widget.initialPriceId)
        .firstOrNull;
    if (_price == null && widget.priceOptions.isNotEmpty) {
      _price = widget.priceOptions.first;
    }
    _centerOnly = _student?.centerOnly ?? false;
    _centerFee.text = priceText(_student?.centerFeeAmount ?? 1500);
    _exactPercent = _student?.discountPercent ?? 0;
    _showPercent();
    _showAmount();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _amount.selection = TextSelection(
        baseOffset: 0,
        extentOffset: _amount.text.length,
      );
    });
  }

  @override
  void dispose() {
    widget.store.removeListener(_refreshStudent);
    _percent.dispose();
    _amount.dispose();
    _centerFee.dispose();
    _percentFocus.dispose();
    _amountFocus.dispose();
    _cancelFocus.dispose();
    super.dispose();
  }

  void _refreshStudent() {
    if (mounted && !_busy) setState(() {});
  }

  void _showPercent() {
    final percent = _exactPercent!;
    _percent.text = percent
        .toStringAsFixed(6)
        .replaceFirst(RegExp(r'0+$'), '')
        .replaceFirst(RegExp(r'\.$'), '');
    _approximate = num.tryParse(_percent.text) != percent;
  }

  void _showAmount() {
    _amount.text = priceText(discountedAmount(_baseAmount, _exactPercent!));
  }

  void _percentChanged(String text) => setState(() {
    _exactPercent = percentFromText(text);
    _percentError = _exactPercent == null ? 'أدخل نسبة من ٠ إلى ١٠٠' : null;
    _amountError = null;
    _approximate = false;
    if (_exactPercent != null) _showAmount();
  });

  void _amountChanged(String text) => setState(() {
    int? target;
    try {
      target = piastresFromText(text);
    } on FormatException {
      target = null;
    }
    if (target == null || target > _baseAmount) {
      _amountError =
          'أدخل مبلغًا من صفر إلى ${money(_baseAmount)}، حتى رقمين عشريين';
      _exactPercent = null;
      return;
    }
    try {
      _exactPercent = discountPercentForAmount(_baseAmount, target);
    } on CenterException catch (error) {
      _amountError = error.message;
      _exactPercent = null;
      return;
    }
    _amountError = null;
    _percentError = null;
    _showPercent();
  });

  void _choosePrice(String? id) => setState(() {
    _fixedForAll = id == _allPricesId;
    if (!_fixedForAll) {
      _price = widget.priceOptions.firstWhere((option) => option.id == id);
    }
    _amountError = null;
    if (_exactPercent != null) _showAmount();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || _busy) return;
      if (_fixedForAll || _baseAmount == 0) {
        _percentFocus.requestFocus();
      } else {
        _amountFocus.requestFocus();
        _amount.selection = TextSelection(
          baseOffset: 0,
          extentOffset: _amount.text.length,
        );
      }
    });
  });

  void _cancel() {
    if (_busy || _finished || hasPendingMassarNotice(context)) return;
    _finished = true;
    Navigator.of(context).pop(false);
  }

  void _armEnter(LogicalKeyboardKey key) {
    if (_busy || _finished || hasPendingMassarNotice(context)) return;
    _armedEnter = key;
    _enterCancels = _cancelFocus.hasFocus;
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (_busy || _finished || hasPendingMassarNotice(context)) {
      _armedEnter = null;
      _armedEscape = null;
      return KeyEventResult.handled;
    }
    final key = event.logicalKey;
    final keyboard = HardwareKeyboard.instance;
    final plain =
        !event.synthesized &&
        !keyboard.isControlPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        !keyboard.isShiftPressed;
    if (key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter) {
      if (!plain || event is KeyRepeatEvent) _armedEnter = null;
      if (event is KeyUpEvent && _armedEnter == key) {
        _armedEnter = null;
        if (_enterCancels) {
          _cancel();
        } else {
          _save();
        }
      }
      return KeyEventResult.handled;
    }
    if (key == LogicalKeyboardKey.escape) {
      if (!plain || event is KeyRepeatEvent) {
        _armedEscape = null;
      } else if (event is KeyDownEvent) {
        _armedEscape = key;
      } else if (event is KeyUpEvent && _armedEscape == key) {
        _armedEscape = null;
        _cancel();
      }
      return KeyEventResult.handled;
    }
    return KeyEventResult.ignored;
  }

  Future<void> _save() async {
    if (_finished || !_canSave || hasPendingMassarNotice(context)) return;
    final percent = _exactPercent!;
    setState(() => _busy = true);
    try {
      await widget.store.saveStudentDiscount(
        studentId: widget.studentId,
        percent: percent,
        centerOnly: _centerConfigEdited ? _centerOnly : null,
        centerFeeAmount: _centerConfigEdited
            ? piastresFromText(_centerFee.text) ??
                  _student?.centerFeeAmount ??
                  1500
            : null,
      );
      if (mounted) {
        _finished = true;
        Navigator.of(context).pop(true);
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.student_editor_dialog');
      if (mounted) {
        await showMassarNotice(
          context,
          error is CenterException
              ? error.message
              : 'تعذر حفظ الخصم. حاول مرة أخرى.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _percentField() => TextField(
    key: const Key('discount-percent'),
    controller: _percent,
    focusNode: _percentFocus,
    autofocus: _fixedForAll || _baseAmount == 0,
    enabled: !_busy && widget.store.canEditDiscount && !_centerOnly,
    keyboardType: const TextInputType.numberWithOptions(decimal: true),
    textDirection: TextDirection.ltr,
    maxLength: 32,
    decoration: InputDecoration(
      labelText: 'نسبة الخصم الثابتة',
      suffixText: '٪',
      counterText: '',
      prefixText: _approximate ? '≈ ' : null,
      errorText: _percentError,
      helperText: _approximate
          ? 'المعروض تقريبي؛ يُحفظ الحساب الدقيق.'
          : '٠ بدون خصم، ١٠٠ إعفاء كامل',
      helperMaxLines: 2,
    ),
    onChanged: _percentChanged,
  );

  Widget _amountField() => TextField(
    key: const Key('discount-amount'),
    controller: _amount,
    focusNode: _amountFocus,
    autofocus: _baseAmount > 0,
    enabled:
        !_busy &&
        widget.store.canEditDiscount &&
        _baseAmount > 0 &&
        !_centerOnly,
    keyboardType: const TextInputType.numberWithOptions(decimal: true),
    textDirection: TextDirection.ltr,
    maxLength: 24,
    decoration: InputDecoration(
      labelText: 'المبلغ بعد الخصم',
      suffixText: 'ج',
      counterText: '',
      errorText: _amountError,
      errorMaxLines: 3,
      helperText:
          'هذا المبلغ يحسب النسبة؛ النسبة نفسها ثابتة لكل الحصص والشهور والكارت.',
      helperMaxLines: 3,
    ),
    onChanged: _amountChanged,
  );

  Widget _title() {
    final student = _student;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('الخصم الثابت للطالب'),
        const SizedBox(height: 12),
        Tooltip(
          message: student?.name ?? 'الطالب غير موجود',
          child: Text(
            student?.name ?? 'الطالب غير موجود',
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(fontSize: 14, fontWeight: FontWeight.bold),
          ),
        ),
        Text(
          'كود الطالب: ${student?.code ?? '—'}',
          style: TextStyle(
            fontSize: 13,
            color: MassarPalette.of(context).muted,
          ),
        ),
        Tooltip(
          message: _priceLabel,
          child: Text(
            _fixedForAll
                ? 'نسبة واحدة لكل المدفوعات الجديدة للطالب'
                : 'مرجع حساب النسبة: $_priceLabel',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 13,
              color: MassarPalette.of(context).muted,
            ),
          ),
        ),
      ],
    );
  }

  Widget _content() {
    final colors = MassarPalette.of(context);
    return SizedBox(
      width: 560,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('السنتر فقط — إعفاء المدرس ١٠٠٪'),
            subtitle: const Text(
              'يسجل حضورًا بدون شراء أو استهلاك شهر للمدرس، وتحصّل رسوم السنتر منفصلة.',
            ),
            value: _centerOnly,
            onChanged: _busy || !widget.store.canEditDiscount
                ? null
                : (enabled) => setState(() {
                    _centerOnly = enabled;
                    _centerConfigEdited = true;
                    if (enabled) {
                      _exactPercent = 100;
                      _percentError = null;
                      _amountError = null;
                      _showPercent();
                      _showAmount();
                    }
                  }),
          ),
          if (_centerOnly) ...[
            TextField(
              key: const Key('discount-center-fee'),
              controller: _centerFee,
              enabled: !_busy && widget.store.canEditDiscount,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: InputDecoration(
                labelText: 'رسوم السنتر لكل حصة بالجنيه',
                suffixText: 'ج',
                errorText: (piastresFromText(_centerFee.text) ?? 0) <= 0
                    ? 'أدخل رسومًا أكبر من صفر'
                    : null,
              ),
              onChanged: (_) => setState(() {
                _centerConfigEdited = true;
              }),
            ),
            const SizedBox(height: 12),
          ],
          if (widget.priceOptions.isNotEmpty)
            DropdownButtonFormField<String>(
              initialValue: _fixedForAll ? _allPricesId : _price?.id,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'مرجع حساب الخصم — النسبة ثابتة لكل الدفع',
              ),
              items: [
                const DropdownMenuItem(
                  value: _allPricesId,
                  child: Text('خصم ثابت لكل المدفوعات'),
                ),
                ...widget.priceOptions.map(
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
                ),
              ],
              onChanged: _busy || !widget.store.canEditDiscount
                  ? null
                  : _choosePrice,
            )
          else
            Text(_priceLabel),
          const SizedBox(height: 12),
          if (!_fixedForAll) Text('السعر قبل الخصم: ${money(_baseAmount)}'),
          const SizedBox(height: 16),
          if (_fixedForAll) ...[
            _percentField(),
            const SizedBox(height: 12),
            const Text(
              'نفس النسبة على الحصص وكل الباقات والكارت، لحد ما تعدّلها أو تلغيها.',
            ),
            if (_exactPercent != null && _percentError == null)
              ...widget.priceOptions.map(
                (option) => Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(
                    '${option.label}: ${money(option.baseAmount)} ← ${money(discountedAmount(option.baseAmount, _exactPercent!))}',
                  ),
                ),
              ),
          ] else
            LayoutBuilder(
              builder: (context, constraints) {
                if (constraints.maxWidth < 500 ||
                    MediaQuery.textScalerOf(context).scale(14) > 21) {
                  return Column(
                    children: [
                      _amountField(),
                      const SizedBox(height: 16),
                      _percentField(),
                    ],
                  );
                }
                return Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(child: _amountField()),
                    const SizedBox(width: 16),
                    Expanded(child: _percentField()),
                  ],
                );
              },
            ),
          if (!_fixedForAll && _baseAmount == 0) ...[
            const SizedBox(height: 12),
            const Text(
              'السعر صفر؛ لا يمكن حساب نسبة من مبلغ. يمكنك تعديل النسبة مباشرة.',
            ),
          ],
          const SizedBox(height: 16),
          Text(
            'اختيار الحصة أو الباقة مرجع لحساب النسبة فقط؛ الخصم المحفوظ ثابت لكل المدفوعات الجديدة. المدفوعات السابقة تحتفظ بأسعارها وخصوماتها.',
            style: TextStyle(color: colors.muted),
          ),
          if (!widget.store.canEditDiscount) ...[
            const SizedBox(height: 12),
            Text(
              'سجل دخولك لتعديل الخصم.',
              style: TextStyle(color: colors.error),
            ),
          ],
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !_busy,
    child: Focus(
      onKeyEvent: _key,
      child: Shortcuts(
        shortcuts: const {
          SingleActivator(LogicalKeyboardKey.enter, includeRepeats: false):
              _ArmDiscountSave(LogicalKeyboardKey.enter),
          SingleActivator(
            LogicalKeyboardKey.numpadEnter,
            includeRepeats: false,
          ): _ArmDiscountSave(
            LogicalKeyboardKey.numpadEnter,
          ),
        },
        child: Actions(
          actions: {
            _ArmDiscountSave: CallbackAction<_ArmDiscountSave>(
              onInvoke: (intent) {
                _armEnter(intent.key);
                return null;
              },
            ),
          },
          child: ScrollableMassarDialog(
            key: const Key('student-discount-dialog'),
            title: _title(),
            content: _content(),
            actions: [
              TextButton(
                key: const Key('cancel-student-discount'),
                focusNode: _cancelFocus,
                onPressed: _busy ? null : _cancel,
                child: const Text('إلغاء دون حفظ · Esc'),
              ),
              FilledButton(
                key: const Key('save-student-discount'),
                onPressed: _canSave ? _save : null,
                child: Text(_busy ? 'جارٍ الحفظ…' : 'حفظ الخصم · Enter'),
              ),
            ],
          ),
        ),
      ),
    ),
  );
}
