import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/theme.dart';

class AttendanceConflictItem {
  const AttendanceConflictItem({
    required this.attendance,
    required this.session,
    required this.groupLabel,
  });
  final AttendanceRecord attendance;
  final LessonSession session;
  final String groupLabel;
}

String attendanceRecordedAtLabel(DateTime time) {
  final local = time.toLocal();
  return '${shortDate(local)} · ${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
}

class _CancelAttendanceConflict extends Intent {
  const _CancelAttendanceConflict();
}

/// Continuing is explicit. A scanner Enter cancels rather than approving entry.
class AttendanceConflictDialog extends StatefulWidget {
  const AttendanceConflictDialog({
    super.key,
    required this.student,
    required this.session,
    required this.groupLabel,
    required this.conflicts,
    required this.scannerBlocked,
  });
  final Student student;
  final LessonSession session;
  final String groupLabel;
  final List<AttendanceConflictItem> conflicts;
  final ValueNotifier<bool> scannerBlocked;

  @override
  State<AttendanceConflictDialog> createState() =>
      _AttendanceConflictDialogState();
}

class _AttendanceConflictDialogState extends State<AttendanceConflictDialog> {
  bool _finished = false;
  bool get _scannerBlocked => widget.scannerBlocked.value;

  @override
  void initState() {
    super.initState();
    widget.scannerBlocked.addListener(_scannerChanged);
  }

  void _scannerChanged() => setState(() {});

  @override
  void dispose() {
    widget.scannerBlocked.removeListener(_scannerChanged);
    super.dispose();
  }

  void _finish(List<String>? reviewedIds) {
    if (_finished) return;
    _finished = true;
    Navigator.of(context).pop(reviewedIds);
  }

  void _cancel() => _finish(null);
  void _continue() {
    if (_scannerBlocked) return;
    _finish(
      List<String>.unmodifiable(
        widget.conflicts.map((item) => item.attendance.id),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Shortcuts(
    shortcuts: const {
      SingleActivator(LogicalKeyboardKey.enter): _CancelAttendanceConflict(),
      SingleActivator(LogicalKeyboardKey.numpadEnter):
          _CancelAttendanceConflict(),
      SingleActivator(LogicalKeyboardKey.escape): _CancelAttendanceConflict(),
    },
    child: Actions(
      actions: {
        _CancelAttendanceConflict: CallbackAction<_CancelAttendanceConflict>(
          onInvoke: (_) {
            _cancel();
            return null;
          },
        ),
      },
      child: Focus(
        autofocus: true,
        onKeyEvent: (_, event) {
          final key = event.logicalKey;
          if (key == LogicalKeyboardKey.enter ||
              key == LogicalKeyboardKey.numpadEnter ||
              key == LogicalKeyboardKey.escape) {
            if (event is KeyRepeatEvent || event is KeyUpEvent) {
              return KeyEventResult.handled;
            }
            return KeyEventResult.ignored;
          }
          if (event is KeyDownEvent &&
              !HardwareKeyboard.instance.isControlPressed &&
              !HardwareKeyboard.instance.isAltPressed &&
              !HardwareKeyboard.instance.isMetaPressed &&
              (event.character?.isNotEmpty == true ||
                  RegExp(
                    r'^[a-z0-9]$',
                    caseSensitive: false,
                  ).hasMatch(key.keyLabel))) {
            widget.scannerBlocked.value = true;
            return KeyEventResult.handled;
          }
          if (key == LogicalKeyboardKey.f2 ||
              key == LogicalKeyboardKey.f4 ||
              key == LogicalKeyboardKey.f6) {
            return KeyEventResult.handled;
          }
          return KeyEventResult.ignored;
        },
        child: Directionality(
          textDirection: TextDirection.rtl,
          child: ScrollableMassarDialog(
            key: const Key('attendance-conflict-dialog'),
            title: const Text('سبق حضور نفس الحصة'),
            content: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  '${widget.student.name} · كود ${widget.student.code}',
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 8),
                Text(
                  'الحصة المطلوبة: ${widget.session.number} · ${sessionDateLabel(widget.session)} · ${widget.groupLabel}',
                ),
                const SizedBox(height: 16),
                const Text(
                  'هذا الطالب حضر نفس رقم الحصة سابقًا. راجع التسجيلات قبل المتابعة؛ المتابعة ستسمح بتسجيل الدخول لهذه الحصة وفق حسابها المعتاد.',
                ),
                const SizedBox(height: 12),
                ...widget.conflicts.map(
                  (item) => Container(
                    margin: const EdgeInsets.only(bottom: 10),
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: MassarPalette.of(context).subtle,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          'حصة ${item.session.number} · ${sessionDateLabel(item.session)} · ${item.groupLabel}',
                          style: const TextStyle(fontWeight: FontWeight.bold),
                        ),
                        Text(
                          item.attendance.status == AttendanceStatus.makeup
                              ? 'حضور سابق كتعويض'
                              : 'حضور سابق',
                        ),
                        Text(
                          'وقت التسجيل السابق: ${attendanceRecordedAtLabel(item.attendance.recordedAt)}',
                        ),
                      ],
                    ),
                  ),
                ),
                Text(
                  _scannerBlocked
                      ? 'وصل إدخال كود أثناء التنبيه. لا يمكن المتابعة؛ أغلق التنبيه وامسح الطالب من جديد.'
                      : 'Enter أو Esc للرجوع دون تسجيل. المتابعة لا تتم إلا بالزر الواضح أدناه.',
                  style: TextStyle(
                    color: _scannerBlocked
                        ? MassarPalette.of(context).error
                        : MassarPalette.of(context).muted,
                  ),
                ),
              ],
            ),
            actions: [
              TextButton(
                key: const Key('attendance-conflict-cancel'),
                onPressed: _cancel,
                child: const Text('رجوع دون تسجيل'),
              ),
              FilledButton(
                key: const Key('attendance-conflict-continue'),
                onPressed: _scannerBlocked ? null : _continue,
                child: const Text('متابعة رغم الحضور السابق'),
              ),
            ],
          ),
        ),
      ),
    ),
  );
}
