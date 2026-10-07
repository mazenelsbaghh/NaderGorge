import 'package:massar_center/shared/scrollable_dialog.dart';
import 'problem_reporting.dart';
import '../domain/models.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'theme.dart';

enum NoticeKind { success, error, warning, info }

class _NoticeQueue {
  Future<void> tail = Future<void>.value();
  int pending = 0;
}

final _noticeQueues = Expando<_NoticeQueue>('massar-notice-queue');

bool hasPendingMassarNotice(BuildContext context) {
  if (!context.mounted) return false;
  final navigator = Navigator.maybeOf(context, rootNavigator: true);
  return navigator != null && (_noticeQueues[navigator]?.pending ?? 0) > 0;
}

/// Acknowledges an event without exposing keyboard input to the page beneath it.
Future<void> showMassarNotice(
  BuildContext context,
  String message, {
  NoticeKind kind = NoticeKind.info,
  String? title,
}) {
  if (!context.mounted || kind == NoticeKind.success) {
    return Future<void>.value();
  }
  if (kind == NoticeKind.warning) {
    reportProblem(
      CenterException('User operation warning'),
      StackTrace.current,
      operation: 'ui.warning',
    );
  }
  final navigator = Navigator.of(context, rootNavigator: true);
  final queue = _noticeQueues[navigator] ??= _NoticeQueue();
  queue.pending++;
  final previous = queue.tail;
  final focus = FocusManager.instance.primaryFocus;
  final next = previous.then((_) async {
    try {
      if (!context.mounted || !navigator.mounted) return;
      final reduced = MediaQuery.maybeOf(context)?.disableAnimations ?? false;
      final route = RawDialogRoute<void>(
        barrierDismissible: false,
        barrierLabel: 'تنبيه',
        transitionDuration: reduced
            ? Duration.zero
            : const Duration(milliseconds: 150),
        pageBuilder: (context, _, _) => Directionality(
          textDirection: TextDirection.rtl,
          child: _NoticeDialog(message: message, kind: kind, title: title),
        ),
        transitionBuilder: (context, animation, _, child) {
          final curve = CurvedAnimation(
            parent: animation,
            curve: Curves.easeOutCubic,
            reverseCurve: Curves.easeInCubic,
          );
          return FadeTransition(
            opacity: curve,
            child: ScaleTransition(
              scale: Tween<double>(begin: .97, end: 1).animate(curve),
              child: child,
            ),
          );
        },
      );
      await navigator.push<void>(route);
      await route.completed;
      if (focus?.context?.mounted == true && focus!.canRequestFocus) {
        focus.requestFocus();
      }
    } finally {
      queue.pending--;
    }
  });
  queue.tail = next.catchError((Object error, StackTrace stackTrace) {
    reportProblem(error, stackTrace, operation: 'ui.notice');
  });
  return next;
}

class _NoticeDialog extends StatefulWidget {
  const _NoticeDialog({required this.message, required this.kind, this.title});
  final String message;
  final NoticeKind kind;
  final String? title;
  @override
  State<_NoticeDialog> createState() => _NoticeDialogState();
}

class _NoticeDialogState extends State<_NoticeDialog> {
  LogicalKeyboardKey? _closingKey;
  bool _closed = false;
  final _scroll = ScrollController();
  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  void _close() {
    if (_closed) return;
    _closed = true;
    Navigator.of(context).pop();
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    final key = event.logicalKey;
    if (key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter ||
        key == LogicalKeyboardKey.escape) {
      if (event is KeyDownEvent &&
          !HardwareKeyboard.instance.isControlPressed &&
          !HardwareKeyboard.instance.isAltPressed &&
          !HardwareKeyboard.instance.isMetaPressed &&
          !HardwareKeyboard.instance.isShiftPressed) {
        _closingKey = key;
      } else if (event is KeyUpEvent && _closingKey == key) {
        _close();
      }
    }
    if (event is KeyDownEvent && _scroll.hasClients) {
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
    // Held keys, scanner characters, and shortcuts stay inside this notice.
    return KeyEventResult.handled;
  }

  @override
  Widget build(BuildContext context) {
    final colors = MassarPalette.of(context);
    final (color, surface, icon, label) = switch (widget.kind) {
      NoticeKind.success => (
        colors.success,
        colors.successSurface,
        Icons.check_circle_outline,
        'تم بنجاح',
      ),
      NoticeKind.error => (
        colors.error,
        colors.errorSurface,
        Icons.error_outline,
        'تعذر إتمام العملية',
      ),
      NoticeKind.warning => (
        colors.warning,
        colors.warningSurface,
        Icons.warning_amber_outlined,
        'انتبه',
      ),
      NoticeKind.info => (
        colors.accent,
        colors.subtle,
        Icons.info_outline,
        'تنبيه',
      ),
    };
    return Focus(
      autofocus: true,
      descendantsAreFocusable: false,
      onKeyEvent: _key,
      child: ScrollableMassarDialog(
        key: const Key('massar-notice-dialog'),
        scrollController: _scroll,
        title: Row(
          children: [
            Icon(icon, color: color, semanticLabel: label),
            const SizedBox(width: 12),
            Expanded(child: Text(widget.title ?? label)),
          ],
        ),
        content: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 500),
          child: Container(
            key: const Key('massar-notice-message'),
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: surface,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Semantics(
              liveRegion: true,
              child: Text(
                widget.message,
                style: TextStyle(color: color, fontSize: 17, height: 1.5),
              ),
            ),
          ),
        ),
        actions: [
          FilledButton(
            key: const Key('dismiss-massar-notice'),
            onPressed: _close,
            child: const Text('تمام · Enter / Esc'),
          ),
        ],
      ),
    );
  }
}
