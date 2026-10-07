import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';
import 'entry_confirmation_dialog.dart';

class ClosedSessionChoice {
  const ClosedSessionChoice({
    this.sessionId,
    this.lookupOnly = false,
    this.reopenCurrent = false,
  });
  final String? sessionId;
  final bool lookupOnly;
  final bool reopenCurrent;
}

/// An explicit navigation choice. Dismissing this warning never registers entry.
class ClosedSessionDialog extends StatefulWidget {
  const ClosedSessionDialog({
    super.key,
    required this.session,
    required this.groupLabel,
    required this.openSessions,
    required this.canReopen,
    required this.hasFinancialClosing,
  });
  final LessonSession session;
  final String groupLabel;
  final List<LessonSession> openSessions;
  final bool canReopen;
  final bool hasFinancialClosing;

  @override
  State<ClosedSessionDialog> createState() => _ClosedSessionDialogState();
}

class _ClosedSessionDialogState extends State<ClosedSessionDialog> {
  bool _choosingSession = false;
  bool _dismissed = false;
  bool _confirmingReopen = false;
  bool _scannerBlocked = false;
  String? _selectedSessionId;

  void _finish([ClosedSessionChoice? choice]) {
    if (_dismissed || (choice?.reopenCurrent == true && _scannerBlocked)) {
      return;
    }
    _dismissed = true;
    Navigator.pop(context, choice);
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (_dismissed) return KeyEventResult.handled;
    final key = event.logicalKey;
    if (key == LogicalKeyboardKey.escape) {
      if (event is KeyDownEvent && !event.synthesized) _finish();
      return KeyEventResult.handled;
    }
    if (key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter) {
      final keyboard = HardwareKeyboard.instance;
      if (event is KeyDownEvent &&
          !event.synthesized &&
          !keyboard.isControlPressed &&
          !keyboard.isAltPressed &&
          !keyboard.isMetaPressed &&
          !keyboard.isShiftPressed) {
        _finish(
          _confirmingReopen
              ? const ClosedSessionChoice(reopenCurrent: true)
              : null,
        );
      }
      return KeyEventResult.handled;
    }
    if (_confirmingReopen && EntryConfirmationDialog.isScannerText(event)) {
      setState(() => _scannerBlocked = true);
      return KeyEventResult.handled;
    }
    if (key == LogicalKeyboardKey.keyL || key == LogicalKeyboardKey.keyN) {
      return KeyEventResult.handled;
    }
    return KeyEventResult.ignored;
  }

  @override
  Widget build(BuildContext context) {
    final palette = MassarPalette.of(context);
    return Focus(
      autofocus: true,
      onKeyEvent: _key,
      child: ScrollableMassarDialog(
        key: Key(
          _confirmingReopen
              ? 'reopen-session-confirmation'
              : 'closed-session-dialog',
        ),
        title: Row(
          children: [
            Icon(Icons.lock_outline, color: palette.warning),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                _confirmingReopen ? 'إعادة فتح هذه الحصة؟' : 'الحصة مغلقة',
              ),
            ),
          ],
        ),
        content: SizedBox(
          width: 480,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                '${sessionLabel(widget.session)} · ${sessionDateLabel(widget.session)}',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Text(widget.groupLabel),
              const SizedBox(height: 16),
              Text(
                _confirmingReopen
                    ? 'الفتح وحده لا يسجل حضورًا أو دفعًا. ستبقى سجلات الحضور والدفع والباقات محفوظة.'
                    : 'لا يمكن تسجيل حضور جديد أو دفع هذه الحصة بعد إغلاقها. يمكنك عرض السجل فقط، أو اختيار حصة مفتوحة من نفس المجموعة.${widget.canReopen ? ' ويمكنك إعادة فتح هذه الحصة بعد التأكيد.' : ''}',
              ),
              if (_confirmingReopen && widget.hasFinancialClosing) ...[
                const SizedBox(height: 12),
                const Text(
                  'التقفيلة السابقة ستبقى في السجل وتحتاج تقفيلة جديدة.',
                ),
              ],
              if (!_confirmingReopen && widget.openSessions.isEmpty) ...[
                const SizedBox(height: 12),
                Text(
                  widget.canReopen
                      ? 'لا توجد حصة مفتوحة لهذه المجموعة. يمكنك إعادة فتح هذه الحصة أو إضافة حصة جديدة.'
                      : 'لا توجد حصة مفتوحة لهذه المجموعة. أضف حصة من صفحة الحصص أولًا.',
                ),
              ],
              if (_choosingSession && !_confirmingReopen) ...[
                const SizedBox(height: 20),
                DropdownButtonFormField<String>(
                  key: const Key('closed-session-open-choice'),
                  isExpanded: true,
                  initialValue: _selectedSessionId,
                  decoration: const InputDecoration(
                    labelText: 'اختر الحصة المفتوحة',
                  ),
                  items: widget.openSessions
                      .map(
                        (session) => DropdownMenuItem(
                          value: session.id,
                          child: Text(
                            '${sessionLabel(session)} · ${sessionDateLabel(session)}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  onChanged: (id) => setState(() => _selectedSessionId = id),
                ),
                const SizedBox(height: 10),
                const Text('اختيار الحصة لا يسجل حضورًا أو دفعًا.'),
              ],
              const SizedBox(height: 16),
              if (_scannerBlocked) ...[
                Text(
                  'دخل كود أثناء التأكيد. اضغط Esc وأعد مراجعة فتح الحصة من جديد.',
                  style: TextStyle(color: palette.error),
                ),
                const SizedBox(height: 10),
              ],
              Text(
                _confirmingReopen
                    ? 'Enter لتأكيد الفتح مرة واحدة · Esc للإلغاء.'
                    : 'Enter أو Esc لإغلاق التنبيه والعودة للكود.',
                style: TextStyle(color: palette.muted, fontSize: 13),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(onPressed: _finish, child: const Text('إلغاء')),
          if (_confirmingReopen)
            FilledButton(
              key: const Key('confirm-reopen-session'),
              onPressed: _scannerBlocked
                  ? null
                  : () =>
                        _finish(const ClosedSessionChoice(reopenCurrent: true)),
              child: const Text('تأكيد فتح الحصة'),
            ),
          if (!_confirmingReopen && widget.canReopen)
            FilledButton.icon(
              key: const Key('closed-session-reopen'),
              onPressed: () => setState(() => _confirmingReopen = true),
              icon: const Icon(Icons.lock_open_outlined),
              label: const Text('إعادة فتح هذه الحصة'),
            ),
          if (!_confirmingReopen)
            OutlinedButton(
              key: const Key('closed-session-view-only'),
              onPressed: () =>
                  _finish(const ClosedSessionChoice(lookupOnly: true)),
              child: const Text('عرض السجل فقط'),
            ),
          if (!_confirmingReopen)
            FilledButton(
              key: const Key('closed-session-choose-open'),
              onPressed:
                  widget.openSessions.isEmpty ||
                      (_choosingSession && _selectedSessionId == null)
                  ? null
                  : () {
                      if (_choosingSession) {
                        _finish(
                          ClosedSessionChoice(sessionId: _selectedSessionId),
                        );
                      } else {
                        setState(() => _choosingSession = true);
                      }
                    },
              child: Text(
                _choosingSession ? 'فتح الحصة المختارة' : 'اختيار حصة مفتوحة',
              ),
            ),
        ],
      ),
    );
  }
}
