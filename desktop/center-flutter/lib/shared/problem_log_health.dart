import 'package:flutter/material.dart';
import 'notice_dialog.dart';
import 'problem_log.dart';

/// Reporting must stay optional to business operations, but a disk failure is visible.
class ProblemLogHealth extends StatefulWidget {
  const ProblemLogHealth({super.key, required this.child});
  final Widget child;

  @override
  State<ProblemLogHealth> createState() => _ProblemLogHealthState();
}

class _ProblemLogHealthState extends State<ProblemLogHealth> {
  final ProblemLog? _log = ProblemLog.current;
  bool _reported = false;

  @override
  void initState() {
    super.initState();
    _log?.failure.addListener(_checkFailure);
    WidgetsBinding.instance.addPostFrameCallback((_) => _checkFailure());
  }

  void _checkFailure() {
    if (!mounted || _reported || _log?.writeFailure == null) return;
    _reported = true;
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        showMassarNotice(
          context,
          'تعذر حفظ سجل المشاكل على الجهاز. الشغل مستمر؛ راجع المساحة وصلاحية الحفظ.',
          kind: NoticeKind.warning,
        );
      }
    });
    WidgetsBinding.instance.scheduleFrame();
  }

  @override
  void dispose() {
    _log?.failure.removeListener(_checkFailure);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
