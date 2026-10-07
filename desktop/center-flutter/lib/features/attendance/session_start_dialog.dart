import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../domain/models.dart';
import '../../shared/formatters.dart';
import '../../shared/scrollable_dialog.dart';
import '../../shared/theme.dart';

/// Activates the selected class context without registering attendance or money.
class SessionStartDialog extends StatefulWidget {
  const SessionStartDialog({
    super.key,
    required this.session,
    required this.groupLabel,
    this.changedContext = false,
  });

  final LessonSession session;
  final String groupLabel;
  final bool changedContext;

  @override
  State<SessionStartDialog> createState() => _SessionStartDialogState();
}

class _SessionStartDialogState extends State<SessionStartDialog> {
  LogicalKeyboardKey? _armedKey;
  bool _finished = false;
  bool _scannerBlocked = false;
  final _scroll = ScrollController();

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  void _finish(bool start) {
    if (_finished || (start && _scannerBlocked)) {
      return;
    }
    _finished = true;
    Navigator.pop(context, start);
  }

  bool get _plainKey {
    final keyboard = HardwareKeyboard.instance;
    return !keyboard.isControlPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        !keyboard.isShiftPressed;
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    final key = event.logicalKey;
    final enter =
        key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter;
    if (enter || key == LogicalKeyboardKey.escape) {
      if (!_plainKey || event.synthesized) {
        _armedKey = null;
      } else if (event is KeyDownEvent) {
        _armedKey = key;
      } else if (event is KeyRepeatEvent) {
        _armedKey = null;
      } else if (event is KeyUpEvent && _armedKey == key) {
        _armedKey = null;
        _finish(enter);
      }
      return KeyEventResult.handled;
    }
    if (event is KeyDownEvent &&
        ((event.character?.isNotEmpty ?? false) ||
            RegExp(r'^[A-Za-z0-9]$').hasMatch(key.keyLabel))) {
      _armedKey = null;
      if (!_scannerBlocked) {
        setState(() => _scannerBlocked = true);
      }
    }
    if (event is KeyDownEvent && _scroll.hasClients) {
      final delta = switch (key) {
        LogicalKeyboardKey.arrowDown => 48.0,
        LogicalKeyboardKey.arrowUp => -48.0,
        LogicalKeyboardKey.pageDown => 180.0,
        LogicalKeyboardKey.pageUp => -180.0,
        _ => 0.0,
      };
      if (delta != 0) {
        _scroll.jumpTo(
          (_scroll.offset + delta).clamp(0, _scroll.position.maxScrollExtent),
        );
      }
    }
    return KeyEventResult.handled;
  }

  @override
  Widget build(BuildContext context) {
    final palette = MassarPalette.of(context);
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Focus(
        autofocus: true,
        descendantsAreFocusable: false,
        onKeyEvent: _key,
        child: ScrollableMassarDialog(
          key: const Key('session-start-dialog'),
          width: 560,
          scrollController: _scroll,
          title: Row(
            children: [
              Icon(Icons.warning_amber_rounded, color: palette.warning),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  widget.changedContext
                      ? 'خلي بالك: تغيّرت الحصة'
                      : 'ابدأ الحصة؟',
                ),
              ),
            ],
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                widget.groupLabel,
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              Text(
                '${sessionLabel(widget.session)} · ${sessionDateLabel(widget.session)}',
              ),
              const SizedBox(height: 8),
              Text(sessionKindLabel(widget.session.kind)),
              const SizedBox(height: 18),
              const Text(
                'بدء الحصة يفعّل التحضير لهذه الحصة فقط. لا يسجل حضور طالب أو دفع أي مبلغ.',
              ),
              if (_scannerBlocked) ...[
                const SizedBox(height: 12),
                Text(
                  'دخل كود أثناء التأكيد. اضغط Esc ثم راجع الحصة من جديد.',
                  style: TextStyle(color: palette.warning),
                ),
              ],
            ],
          ),
          actions: [
            TextButton(
              key: const Key('session-start-cancel'),
              onPressed: () => _finish(false),
              child: const Text('ليس الآن · Esc'),
            ),
            FilledButton(
              key: const Key('session-start-confirm'),
              onPressed: _scannerBlocked ? null : () => _finish(true),
              child: const Text('ابدأ الحصة · Enter'),
            ),
          ],
        ),
      ),
    );
  }
}
