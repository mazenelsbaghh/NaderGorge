import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/theme.dart';

class StudentDiscountWarningDialog extends StatefulWidget {
  const StudentDiscountWarningDialog({
    super.key,
    required this.student,
    required this.canReview,
  });

  final Student student;
  final bool canReview;

  @override
  State<StudentDiscountWarningDialog> createState() =>
      _StudentDiscountWarningDialogState();
}

class _StudentDiscountWarningDialogState
    extends State<StudentDiscountWarningDialog> {
  LogicalKeyboardKey? _armedKey;
  bool _closed = false;

  void _finish(bool review) {
    if (_closed || (review && !widget.canReview)) return;
    _closed = true;
    Navigator.pop(context, review);
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (_closed) return KeyEventResult.handled;
    final key = event.logicalKey;
    final keyboard = HardwareKeyboard.instance;
    final plain =
        !keyboard.isControlPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        !keyboard.isShiftPressed;
    final accepted =
        key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter ||
        key == LogicalKeyboardKey.escape ||
        (key == LogicalKeyboardKey.keyS && widget.canReview);
    // A scanner's trailing Enter must not register attendance under the popup.
    if (!plain || event.synthesized || event is KeyRepeatEvent) {
      _armedKey = null;
    } else if (accepted && event is KeyDownEvent) {
      _armedKey = key;
    }
    if (event is KeyUpEvent && key == _armedKey) {
      _armedKey = null;
      _finish(key == LogicalKeyboardKey.keyS);
    }
    return KeyEventResult.handled;
  }

  @override
  Widget build(BuildContext context) {
    final colors = MassarPalette.of(context);
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Focus(
        autofocus: true,
        descendantsAreFocusable: false,
        onKeyEvent: _key,
        child: ScrollableMassarDialog(
          key: const Key('student-discount-warning-dialog'),
          width: 540,
          title: Row(
            children: [
              Icon(Icons.warning_amber_outlined, color: colors.warning),
              const SizedBox(width: 10),
              const Expanded(child: Text('خصم الطالب يحتاج مراجعة')),
            ],
          ),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                '${widget.student.name} · كود ${widget.student.code}',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 16),
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: colors.warningSurface,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: const Text(
                  'الخصم الموجود في البيانات القديمة غير واضح، ولم يُطبق على حساب الطالب. راجع نسبة الخصم أو المبلغ المطلوب قبل التحصيل.',
                ),
              ),
              const SizedBox(height: 12),
              Text(
                widget.canReview
                    ? 'اضغط S لمراجعة الخصم الآن، ثم Enter لحفظه.'
                    : 'اطلب من المدير مراجعة الخصم من «خصم سريع · S» في وضع التحضير.',
              ),
            ],
          ),
          actions: [
            if (widget.canReview)
              FilledButton.icon(
                key: const Key('review-student-discount'),
                onPressed: () => _finish(true),
                icon: const Icon(Icons.percent),
                label: const Text('مراجعة الخصم · S'),
              ),
            TextButton(
              onPressed: () => _finish(false),
              child: const Text('فهمت · Enter / Esc'),
            ),
          ],
        ),
      ),
    );
  }
}
