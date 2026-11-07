import 'package:flutter/material.dart';

import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../shared/formatters.dart';
import '../../shared/scrollable_dialog.dart';
import '../management/management_widgets.dart' show piastresFromText, priceText;

class PaymentEditDialog extends StatefulWidget {
  const PaymentEditDialog({
    super.key,
    required this.store,
    required this.payment,
  });

  final CenterStore store;
  final PaymentRecord payment;

  @override
  State<PaymentEditDialog> createState() => _PaymentEditDialogState();
}

class _PaymentEditDialogState extends State<PaymentEditDialog> {
  final _form = GlobalKey<FormState>();
  late final _amount = TextEditingController(
    text: priceText(widget.payment.collectedAmount),
  );
  final _reason = TextEditingController();
  late String _method = widget.payment.method;
  bool _saving = false;
  String? _error;
  bool get _canEditAmount =>
      widget.store.canEditPaymentAmount(widget.payment.id);

  @override
  void dispose() {
    _amount.dispose();
    _reason.dispose();
    super.dispose();
  }

  String? _amountError(String? value) {
    final amount = piastresFromText(value ?? '');
    if (amount == null) {
      return 'اكتب مبلغًا صحيحًا بالجنيه حتى رقمين بعد الفاصلة.';
    }
    if (amount <= widget.payment.netAmount) return null;
    return 'اكتب مبلغًا من صفر إلى قيمة المستحق.';
  }

  Future<void> _save() async {
    if (_saving || !_form.currentState!.validate()) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await widget.store.editPayment(
        reviewed: widget.payment,
        paidAmount: piastresFromText(_amount.text)!,
        method: _method,
        reason: _reason.text,
      );
      if (mounted) Navigator.pop(context, true);
    } on CenterException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) => Directionality(
    textDirection: TextDirection.rtl,
    child: ScrollableMassarDialog(
      title: const Text('تعديل الدفع'),
      content: Form(
        key: _form,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(widget.store.studentById(widget.payment.studentId)!.name),
            Text(widget.payment.description),
            Text('المستحق: ${money(widget.payment.netAmount)}'),
            const SizedBox(height: 12),
            TextFormField(
              controller: _amount,
              enabled: !_saving && _canEditAmount,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              textDirection: TextDirection.ltr,
              decoration: const InputDecoration(
                labelText: 'المبلغ المحصّل بالجنيه',
              ),
              validator: _amountError,
            ),
            const SizedBox(height: 12),
            if (!_canEditAmount)
              const Text(
                'المبلغ مرتبط بسداد مديونية أو تحويل للأونلاين. يمكن تعديل طريقة الدفع فقط.',
              ),
            DropdownButtonFormField<String>(
              initialValue: _method,
              decoration: const InputDecoration(labelText: 'طريقة الدفع'),
              items:
                  {
                        'نقدي',
                        'إنستاباي',
                        'تحويل بنكي',
                        'بطاقة',
                        widget.payment.method,
                      }
                      .map(
                        (method) => DropdownMenuItem(
                          value: method,
                          child: Text(method),
                        ),
                      )
                      .toList(),
              onChanged: _saving
                  ? null
                  : (method) => setState(() => _method = method!),
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: _reason,
              enabled: !_saving,
              maxLength: 2000,
              decoration: const InputDecoration(labelText: 'سبب التعديل'),
              validator: (value) =>
                  value!.trim().isEmpty ? 'اكتب سبب التعديل.' : null,
            ),
            if (_error != null)
              Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _saving ? null : () => Navigator.pop(context),
          child: const Text('رجوع'),
        ),
        FilledButton(
          onPressed: _saving ? null : _save,
          child: const Text('حفظ التعديل'),
        ),
      ],
    ),
  );
}
