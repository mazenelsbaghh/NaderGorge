import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/theme.dart';

import 'entry_confirmation_dialog.dart' show EntryConfirmationDialog;

enum StudentSuspensionAction { suspend, reactivate }

/// A status change only; student history and financial balances stay intact.
class StudentSuspensionDialog extends StatefulWidget {
  const StudentSuspensionDialog({
    super.key,
    required this.store,
    required this.student,
    required this.action,
    this.scannerBlocked,
  });
  final CenterStore store;
  final Student student;
  final StudentSuspensionAction action;
  final ValueNotifier<bool>? scannerBlocked;

  @override
  State<StudentSuspensionDialog> createState() =>
      _StudentSuspensionDialogState();
}

class _StudentSuspensionDialogState extends State<StudentSuspensionDialog> {
  final _reason = TextEditingController();
  final _reasonFocus = FocusNode();
  final _cancelFocus = FocusNode();
  LogicalKeyboardKey? _armed;
  bool _busy = false, _finished = false, _scannerText = false;
  String? _reasonError;
  bool get _reactivate => widget.action == StudentSuspensionAction.reactivate;
  bool get _blocked => _scannerText || widget.scannerBlocked?.value == true;

  @override
  void initState() {
    super.initState();
    widget.scannerBlocked?.addListener(_refresh);
    widget.store.addListener(_refresh);
  }

  void _refresh() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    widget.scannerBlocked?.removeListener(_refresh);
    widget.store.removeListener(_refresh);
    _reason.dispose();
    _reasonFocus.dispose();
    _cancelFocus.dispose();
    super.dispose();
  }

  void _cancel() {
    if (_busy || _finished) return;
    _finished = true;
    Navigator.pop(context, false);
  }

  Future<void> _save() async {
    if (_busy ||
        _finished ||
        !widget.store.canCollect ||
        (_reactivate && _blocked) ||
        hasPendingMassarNotice(context)) {
      return;
    }
    final reason = _reason.text.trim();
    if (!_reactivate && (reason.isEmpty || reason.length > 2000)) {
      setState(() => _reasonError = 'اكتب سبب التصفية، حتى ٢٠٠٠ حرف.');
      _reasonFocus.requestFocus();
      return;
    }
    setState(() => _busy = true);
    try {
      if (_reactivate) {
        await widget.store.reactivateStudent(widget.student.id);
      } else {
        await widget.store.suspendStudent(
          studentId: widget.student.id,
          reason: reason,
        );
      }
      if (!mounted) return;
      await showMassarNotice(
        context,
        _reactivate
            ? 'تمت إعادة تفعيل الطالب. لم يُسجل حضور أو دفع.'
            : 'تمت تصفية الطالب مع الاحتفاظ بسجله ورصيده.',
      );
      if (!mounted) return;
      _finished = true;
      Navigator.pop(context, true);
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.student_editor_dialog');
      if (mounted) {
        await showMassarNotice(
          context,
          error is CenterException
              ? error.message
              : 'تعذر تغيير حالة الطالب. حاول مرة أخرى.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (_busy || _finished || hasPendingMassarNotice(context)) {
      _armed = null;
      return KeyEventResult.handled;
    }
    final key = event.logicalKey;
    final enter =
        key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter;
    if (!_reactivate && enter && _reasonFocus.hasFocus) {
      return KeyEventResult.ignored;
    }
    if (enter || key == LogicalKeyboardKey.escape) {
      final keyboard = HardwareKeyboard.instance;
      if (event.synthesized ||
          keyboard.isControlPressed ||
          keyboard.isShiftPressed ||
          keyboard.isAltPressed ||
          keyboard.isMetaPressed ||
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
            _save();
          }
        }
      }
      return KeyEventResult.handled;
    }
    if (_reactivate && EntryConfirmationDialog.isScannerText(event)) {
      setState(() => _scannerText = true);
      return KeyEventResult.handled;
    }
    return KeyEventResult.ignored;
  }

  @override
  Widget build(BuildContext context) => PopScope(
    canPop: !_busy,
    child: Directionality(
      textDirection: TextDirection.rtl,
      child: Focus(
        autofocus: _reactivate,
        onKeyEvent: _key,
        child: ScrollableMassarDialog(
          key: const Key('student-suspension-dialog'),
          width: 560,
          title: Text(
            _reactivate ? 'الطالب مُصفّى — إعادة التفعيل؟' : 'تصفية الطالب',
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                '${widget.student.name} · كود ${widget.student.code}',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 12),
              if (_reactivate) ...[
                Text(
                  'سبب التصفية: ${widget.student.suspensionReason}',
                  style: TextStyle(color: MassarPalette.of(context).error),
                ),
                const SizedBox(height: 12),
                const Text(
                  'هل تريد إعادة تفعيل الطالب؟ إعادة التفعيل وحدها لا تسجل حضورًا أو دفعًا.',
                ),
                if (_blocked)
                  const Text(
                    'وصل كود آخر أثناء التأكيد. ألغِ النافذة ثم امسح كود الطالب من جديد.',
                  ),
              ] else ...[
                const Text(
                  'ستتوقف عمليات الحضور والتحصيل الجديدة بكل المجموعات حتى إعادة تفعيله. يبقى السجل والرصيد والمديونية محفوظًا؛ التصفية لا تسترد أموالًا.',
                ),
                const SizedBox(height: 12),
                TextField(
                  key: const Key('student-suspension-reason'),
                  controller: _reason,
                  focusNode: _reasonFocus,
                  autofocus: true,
                  enabled: !_busy && widget.store.canCollect,
                  maxLines: 3,
                  maxLength: 2000,
                  decoration: InputDecoration(
                    labelText: 'سبب التصفية',
                    errorText: _reasonError,
                  ),
                  onChanged: (_) {
                    if (_reasonError != null) {
                      setState(() => _reasonError = null);
                    }
                  },
                ),
              ],
            ],
          ),
          actions: [
            TextButton(
              focusNode: _cancelFocus,
              onPressed: _busy ? null : _cancel,
              child: const Text('إلغاء · Esc'),
            ),
            FilledButton(
              key: const Key('confirm-student-suspension'),
              onPressed:
                  _busy || (_reactivate && _blocked) || !widget.store.canCollect
                  ? null
                  : _save,
              child: Text(
                _busy
                    ? 'جارٍ الحفظ…'
                    : _reactivate
                    ? 'إعادة تفعيل · Enter'
                    : 'تصفية الطالب',
              ),
            ),
          ],
        ),
      ),
    ),
  );
}
