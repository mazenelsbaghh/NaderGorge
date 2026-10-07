import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../shared/formatters.dart';
import '../../shared/theme.dart';
import '../management/management_widgets.dart' show piastresFromText, priceText;

class PaidAmountDraft extends ChangeNotifier {
  PaidAmountDraft(int? dueAmount) : _dueAmount = dueAmount {
    amount.addListener(notifyListeners);
  }
  final amount = TextEditingController();
  final focus = FocusNode();
  int? _dueAmount;
  bool _custom = false;
  int? get dueAmount => _dueAmount;
  bool get custom => _custom;
  int? get paidAmount {
    if (!_custom) return null;
    try {
      return piastresFromText(amount.text);
    } on FormatException {
      return null;
    }
  }

  int? get collectedAmount => _custom ? paidAmount : _dueAmount;
  int? get remainingAmount => valid ? _dueAmount! - collectedAmount! : null;
  String? get error {
    if (_dueAmount == null) return 'راجع السعر أولًا.';
    if (!_custom) return null;
    final paid = paidAmount;
    if (paid == null) return 'اكتب مبلغًا بالجنيه، حتى رقمين بعد الفاصلة.';
    if (paid < 0) return 'اكتب مبلغًا صحيحًا من صفر إلى المطلوب.';
    if (paid > _dueAmount!) return 'المدفوع لا يمكن أن يزيد عن المطلوب.';
    return null;
  }

  bool get valid => error == null;

  void updateDueAmount(int? dueAmount) {
    _dueAmount = dueAmount;
    if (dueAmount == 0) _custom = false;
    notifyListeners();
  }

  void chooseCustom(bool custom) {
    _custom = custom;
    if (custom) {
      amount.text = priceText(_dueAmount ?? 0);
      amount.selection = TextSelection(
        baseOffset: 0,
        extentOffset: amount.text.length,
      );
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (_custom && focus.context != null) focus.requestFocus();
      });
    } else {
      focus.unfocus();
    }
    notifyListeners();
  }

  bool acceptsAmountKey(KeyEvent event) {
    if (!_custom || !focus.hasFocus) return false;
    final keyboard = HardwareKeyboard.instance;
    if (keyboard.isControlPressed || keyboard.isMetaPressed) return true;
    final key = event.logicalKey;
    if (key == LogicalKeyboardKey.backspace ||
        key == LogicalKeyboardKey.delete ||
        key == LogicalKeyboardKey.arrowLeft ||
        key == LogicalKeyboardKey.arrowRight ||
        key == LogicalKeyboardKey.home ||
        key == LogicalKeyboardKey.end) {
      return true;
    }
    if (key.keyId >= LogicalKeyboardKey.numpad0.keyId &&
        key.keyId <= LogicalKeyboardKey.numpad9.keyId) {
      return true;
    }
    final text = event.character ?? key.keyLabel;
    return RegExp(r'^[0-9٠-٩۰-۹.,٫٬]+$').hasMatch(text);
  }

  @override
  void dispose() {
    amount.removeListener(notifyListeners);
    amount.dispose();
    focus.dispose();
    super.dispose();
  }
}

class PaidAmountFields extends StatelessWidget {
  const PaidAmountFields({super.key, required this.draft, this.enabled = true});
  final PaidAmountDraft draft;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    final palette = MassarPalette.of(context);
    return AnimatedBuilder(
      animation: draft,
      builder: (context, _) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          CheckboxListTile(
            key: const Key('custom-paid-amount'),
            contentPadding: EdgeInsets.zero,
            controlAffinity: ListTileControlAffinity.leading,
            value: draft.custom,
            onChanged: enabled && (draft.dueAmount ?? 0) > 0
                ? (custom) => draft.chooseCustom(custom!)
                : null,
            title: const Text('تحديد مبلغ مدفوع مختلف'),
          ),
          if (draft.custom)
            TextField(
              key: const Key('paid-amount'),
              controller: draft.amount,
              focusNode: draft.focus,
              enabled: enabled,
              textDirection: TextDirection.ltr,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: InputDecoration(
                labelText: 'المدفوع الآن بالجنيه',
                errorText: draft.error,
              ),
            ),
          const SizedBox(height: 8),
          Text(
            'المدفوع الآن: ${draft.collectedAmount == null ? 'غير محدد' : money(draft.collectedAmount!)}',
            key: const Key('payment-collected-preview'),
            style: const TextStyle(fontWeight: FontWeight.bold),
          ),
          Text(
            'المديونية: ${draft.remainingAmount == null ? 'راجع المدفوع' : money(draft.remainingAmount!)}',
            key: const Key('payment-debt-preview'),
            style: TextStyle(
              color: (draft.remainingAmount ?? 0) > 0
                  ? palette.error
                  : palette.muted,
            ),
          ),
          const Text(
            'المبلغ المتبقي يُحفظ كمديونية، ولا يغيّر السعر أو الخصم الثابت.',
          ),
        ],
      ),
    );
  }
}
