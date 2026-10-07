import 'package:flutter/material.dart';

import 'notice_dialog.dart';
import 'scrollable_dialog.dart';

/// Used by both page navigation and a page's own context selectors.
Future<bool> confirmDiscardDraft(
  BuildContext context, {
  required bool dirty,
  bool busy = false,
}) async {
  if (busy) {
    await showMassarNotice(
      context,
      'انتظر انتهاء العملية الحالية قبل الانتقال.',
      kind: NoticeKind.warning,
    );
    return false;
  }
  if (!dirty) return true;
  return await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (context) => ScrollableMassarDialog(
          width: 460,
          title: const Text('تعديلات لم تُحفظ'),
          content: const Text(
            'كتبت بيانات ولم تحفظها بعد. تكمّل تعديلها، أم تخرج بدون حفظ؟',
          ),
          actions: [
            TextButton(
              autofocus: true,
              onPressed: () => Navigator.pop(context, false),
              child: const Text('أكمّل التعديل'),
            ),
            FilledButton(
              style: FilledButton.styleFrom(
                backgroundColor: Theme.of(context).colorScheme.error,
                foregroundColor: Theme.of(context).colorScheme.onError,
              ),
              onPressed: () => Navigator.pop(context, true),
              child: const Text('خروج بدون حفظ'),
            ),
          ],
        ),
      ) ??
      false;
}

class WorkspaceDraftController {
  final _registrations = <_WorkspaceDraftRegistrationState>{};
  bool _checking = false;

  Future<bool> requestLeave(BuildContext context) async {
    if (_checking) return false;
    _checking = true;
    try {
      return await confirmDiscardDraft(
        context,
        dirty: _registrations.any((entry) => entry.widget.dirty),
        busy: _registrations.any((entry) => entry.widget.busy),
      );
    } finally {
      _checking = false;
    }
  }
}

class WorkspaceDraftScope extends InheritedWidget {
  const WorkspaceDraftScope({
    super.key,
    required this.controller,
    required super.child,
  });
  final WorkspaceDraftController controller;

  static WorkspaceDraftController? maybeOf(BuildContext context) => context
      .dependOnInheritedWidgetOfExactType<WorkspaceDraftScope>()
      ?.controller;

  @override
  bool updateShouldNotify(WorkspaceDraftScope oldWidget) =>
      controller != oldWidget.controller;
}

/// Registers page drafts with the shell, and protects the same page when it is
/// opened as a standalone route (for example a closing from attendance).
class WorkspaceDraftRegistration extends StatefulWidget {
  const WorkspaceDraftRegistration({
    super.key,
    required this.dirty,
    required this.busy,
    required this.child,
  });
  final bool dirty;
  final bool busy;
  final Widget child;

  @override
  State<WorkspaceDraftRegistration> createState() =>
      _WorkspaceDraftRegistrationState();
}

class _WorkspaceDraftRegistrationState
    extends State<WorkspaceDraftRegistration> {
  WorkspaceDraftController? _controller;
  bool _allowPop = false;
  bool _checking = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final controller = WorkspaceDraftScope.maybeOf(context);
    if (controller == _controller) return;
    _controller?._registrations.remove(this);
    _controller = controller;
    _controller?._registrations.add(this);
  }

  @override
  void dispose() {
    _controller?._registrations.remove(this);
    super.dispose();
  }

  Future<void> _requestPop(Object? result) async {
    if (_checking || !mounted || ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    _checking = true;
    final leave = await confirmDiscardDraft(
      context,
      dirty: widget.dirty,
      busy: widget.busy,
    );
    _checking = false;
    if (!mounted || !leave || widget.busy) return;
    setState(() => _allowPop = true);
    await WidgetsBinding.instance.endOfFrame;
    if (mounted && ModalRoute.of(context)?.isCurrent == true) {
      Navigator.of(context).pop(result);
    }
  }

  @override
  Widget build(BuildContext context) => PopScope<Object?>(
    canPop: _allowPop || (!widget.dirty && !widget.busy),
    onPopInvokedWithResult: (didPop, result) {
      if (!didPop) _requestPop(result);
    },
    child: widget.child,
  );
}
