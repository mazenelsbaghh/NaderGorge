import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/theme.dart';
import '../management/management_widgets.dart' show piastresFromText, priceText;

Future<void> showStudentDebtDialog(
  BuildContext context,
  CenterStore store,
  Student student, {
  String? sessionId,
}) async {
  if (!context.mounted || !store.canCollect) return;
  final route = DialogRoute<void>(
    context: context,
    barrierDismissible: false,
    builder: (_) =>
        StudentDebtDialog(store: store, student: student, sessionId: sessionId),
  );
  await Navigator.of(context).push(route);
  await route.completed;
}

class StudentDebtDialog extends StatefulWidget {
  const StudentDebtDialog({
    super.key,
    required this.store,
    required this.student,
    this.sessionId,
  });
  final CenterStore store;
  final Student student;
  final String? sessionId;
  @override
  State<StudentDebtDialog> createState() => _StudentDebtDialogState();
}

class _ArmDebtSave extends Intent {
  const _ArmDebtSave(this.key);
  final LogicalKeyboardKey key;
}

class _StudentDebtDialogState extends State<StudentDebtDialog> {
  final _amount = TextEditingController();
  final _amountFocus = FocusNode();
  final _cancelFocus = FocusNode();
  StudentDebt? _selected;
  String _method = 'نقدي';
  bool _busy = false, _enterCancels = false, _finished = false;
  LogicalKeyboardKey? _armedEnter, _armedEscape;
  List<StudentDebt> get _debts => widget.store.canCollect
      ? widget.store.debtsFor(widget.student.id)
      : const [];
  StudentDebt? get _current => _debts
      .where(
        (debt) =>
            debt.paymentId == _selected?.paymentId &&
            debt.kind == _selected?.kind,
      )
      .firstOrNull;
  int? get _value => piastresFromText(_amount.text);
  bool get _valid =>
      !_busy &&
      !_finished &&
      _current != null &&
      _value != null &&
      _value! > 0 &&
      _value! <= _current!.remainingAmount;

  @override
  void initState() {
    super.initState();
    _choose(_debts.firstOrNull);
    widget.store.addListener(_refresh);
  }

  void _refresh() {
    if (mounted && !_busy) setState(() {});
  }

  void _choose(StudentDebt? debt) {
    _armedEnter = null;
    _selected = debt;
    _amount.text = debt == null ? '' : priceText(debt.remainingAmount);
    _amount.selection = TextSelection(
      baseOffset: 0,
      extentOffset: _amount.text.length,
    );
  }

  @override
  void dispose() {
    widget.store.removeListener(_refresh);
    _amount.dispose();
    _amountFocus.dispose();
    _cancelFocus.dispose();
    super.dispose();
  }

  void _cancel() {
    if (_busy || _finished || hasPendingMassarNotice(context)) return;
    _finished = true;
    Navigator.of(context).pop();
  }

  void _arm(LogicalKeyboardKey key) {
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
    if (_finished || !_valid || hasPendingMassarNotice(context)) return;
    final debt = _current!;
    final amount = _value!;
    setState(() => _busy = true);
    try {
      await widget.store.settleDebt(
        paymentId: debt.paymentId,
        kind: debt.kind,
        amount: amount,
        method: _method,
        sessionId: widget.sessionId,
      );
      if (!mounted) return;
      await showMassarNotice(
        context,
        'تم تسديد ${money(amount)} من المديونية.',
        kind: NoticeKind.success,
      );
      if (mounted) {
        _finished = true;
        Navigator.of(context).pop();
      }
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'ui.student_debt_dialog');
      if (mounted) {
        await showMassarNotice(
          context,
          error is CenterException
              ? error.message
              : 'تعذر تسديد المديونية. حاول مرة أخرى.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final debts = _debts;
    final current = _current;
    final session = widget.store.sessions
        .where((s) => s.id == widget.sessionId)
        .firstOrNull;
    return PopScope(
      canPop: !_busy,
      child: Focus(
        onKeyEvent: _key,
        child: Shortcuts(
          shortcuts: const {
            SingleActivator(LogicalKeyboardKey.enter, includeRepeats: false):
                _ArmDebtSave(LogicalKeyboardKey.enter),
            SingleActivator(
              LogicalKeyboardKey.numpadEnter,
              includeRepeats: false,
            ): _ArmDebtSave(
              LogicalKeyboardKey.numpadEnter,
            ),
          },
          child: Actions(
            actions: {
              _ArmDebtSave: CallbackAction<_ArmDebtSave>(
                onInvoke: (intent) {
                  _arm(intent.key);
                  return null;
                },
              ),
            },
            child: ScrollableMassarDialog(
              key: const Key('student-debt-dialog'),
              title: Text('مديونيات ${widget.student.name}'),
              content: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text('كود الطالب: ${widget.student.code}'),
                  const SizedBox(height: 12),
                  Text(
                    'إجمالي المتبقي: ${money(debts.fold<int>(0, (sum, d) => sum + d.remainingAmount))}',
                    style: TextStyle(
                      color: MassarPalette.of(context).error,
                      fontWeight: FontWeight.bold,
                      fontSize: 18,
                    ),
                  ),
                  const SizedBox(height: 16),
                  if (!widget.store.canCollect)
                    const Text(
                      'لم تعد لديك صلاحية التحصيل؛ أغلِق النافذة وسجّل الدخول بحساب مسموح له.',
                    )
                  else if (debts.isEmpty)
                    const Text('لا توجد مديونيات متبقية.')
                  else ...[
                    DropdownButtonFormField<String>(
                      key: ValueKey(
                        'debt-${current?.kind.name}-${current?.paymentId}',
                      ),
                      initialValue: current == null
                          ? null
                          : '${current.kind.name}:${current.paymentId}',
                      decoration: const InputDecoration(
                        labelText: 'اختر المديونية',
                      ),
                      isExpanded: true,
                      items: debts
                          .map(
                            (d) => DropdownMenuItem(
                              value: '${d.kind.name}:${d.paymentId}',
                              child: Text(
                                '${d.description} · ${shortDate(d.createdAt)} · المتبقي ${money(d.remainingAmount)}',
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          )
                          .toList(),
                      onChanged: _busy
                          ? null
                          : (value) => setState(() {
                              _choose(
                                debts.firstWhere(
                                  (d) =>
                                      '${d.kind.name}:${d.paymentId}' == value,
                                ),
                              );
                              _amountFocus.requestFocus();
                            }),
                    ),
                    const SizedBox(height: 16),
                    if (_selected != null && current == null)
                      Text(
                        'تغيّرت المديونية المختارة أو سُددت من جهاز آخر. اختر مديونية سارية قبل التحصيل.',
                        style: TextStyle(
                          color: MassarPalette.of(context).warning,
                        ),
                      ),
                    if (current != null)
                      Text(
                        'المستحق ${money(current.dueAmount)} · المحصّل حتى الآن ${money(current.collectedAmount)}',
                      ),
                    const SizedBox(height: 16),
                    TextField(
                      controller: _amount,
                      focusNode: _amountFocus,
                      autofocus: true,
                      enabled: !_busy,
                      textDirection: TextDirection.ltr,
                      maxLength: 16,
                      keyboardType: const TextInputType.numberWithOptions(
                        decimal: true,
                      ),
                      decoration: InputDecoration(
                        labelText: 'المبلغ المدفوع الآن',
                        suffixText: 'ج',
                        counterText: '',
                        errorText: _valid || _busy
                            ? null
                            : 'أدخل مبلغًا أكبر من صفر ولا يتجاوز المتبقي',
                      ),
                      onChanged: (_) => setState(() {}),
                    ),
                    const SizedBox(height: 16),
                    DropdownButtonFormField<String>(
                      initialValue: _method,
                      decoration: const InputDecoration(
                        labelText: 'وسيلة الدفع',
                      ),
                      items: ['نقدي', 'تحويل', 'بطاقة']
                          .map(
                            (m) => DropdownMenuItem(value: m, child: Text(m)),
                          )
                          .toList(),
                      onChanged: _busy
                          ? null
                          : (value) => setState(() => _method = value!),
                    ),
                    const SizedBox(height: 12),
                    Text(
                      session == null
                          ? 'تحصيل مستقل؛ يظهر في التقارير ولا يدخل تقفيلة حصة.'
                          : 'التحصيل يُسجل في تقفيلة ${sessionLabel(session)} · ${widget.store.groupLabel(session.groupId)}',
                    ),
                    if (_valid)
                      Text(
                        'المتبقي بعد التسديد: ${money(current!.remainingAmount - _value!)}',
                      ),
                  ],
                ],
              ),
              actions: [
                FilledButton(
                  onPressed: _valid ? _save : null,
                  child: Text(_busy ? 'جارٍ الحفظ…' : 'تسديد · Enter'),
                ),
                TextButton(
                  focusNode: _cancelFocus,
                  onPressed: _busy ? null : _cancel,
                  child: const Text('إغلاق · Esc'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
