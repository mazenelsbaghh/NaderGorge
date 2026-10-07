import 'package:massar_center/domain/discount_calculation.dart';
import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';

/// Card status and explicit actions. The caller owns persistence and busy state.
class StudentCardActions extends StatelessWidget {
  const StudentCardActions({
    super.key,
    required this.store,
    required this.student,
    this.onPay,
    this.onReceive,
  });

  final CenterStore store;
  final Student student;
  final VoidCallback? onPay;
  final VoidCallback? onReceive;

  @override
  Widget build(BuildContext context) {
    final colors = MassarPalette.of(context);
    final settings = store.cardSettings;
    final payment = store.cardPaymentFor(student.id);
    final receipt = store.cardReceiptFor(student.id);
    final base = settings.price;
    final quote = base == null
        ? null
        : discountedAmount(base, student.discountPercent);
    final paid = payment != null;
    final debt = payment == null ? 0 : store.cardDebtFor(payment.id);
    final collected = payment == null ? 0 : store.cardCollectedFor(payment.id);
    return Container(
      key: const Key('student-card-actions'),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: colors.subtle,
        border: Border.all(color: colors.line),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(Icons.badge_outlined, size: 20, color: colors.accent),
              const SizedBox(width: 8),
              const Expanded(
                child: Text(
                  'كارت الطالب',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
              Text(
                receipt == null ? 'لم يستلم' : 'الكارت مستلم',
                key: const Key('student-card-receipt-status'),
                style: TextStyle(
                  color: receipt == null ? colors.muted : colors.success,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            paid
                ? 'المطلوب ${money(payment.netAmount)} · المدفوع ${money(collected)} · المديونية ${money(debt)} · ${payment.method} · ${shortDate(payment.createdAt)}'
                : receipt != null
                ? 'لا يوجد تحصيل مسجل للكارت.'
                : quote == null
                ? 'رسوم الكارت غير محددة في إعدادات السنتر.'
                : 'رسوم الكارت: ${money(quote)} بعد الخصم الثابت ${percentText(student.discountPercent)}٪',
            key: const Key('student-card-payment-status'),
          ),
          if (receipt != null)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                '${receipt.paymentBypassed ? 'استلم بدون تحصيل مسجل' : 'استلم الكارت'} · ${shortDate(receipt.receivedAt)}',
                key: const Key('student-card-received-detail'),
              ),
            ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              OutlinedButton.icon(
                key: const Key('pay-student-card'),
                onPressed:
                    store.canCollect &&
                        !student.isSuspended &&
                        !paid &&
                        receipt == null &&
                        quote != null
                    ? onPay
                    : null,
                icon: const Icon(Icons.payments_outlined, size: 18),
                label: Text(
                  paid
                      ? debt > 0
                            ? 'دفع الكارت مسجل — عليه مديونية'
                            : 'رسوم الكارت مدفوعة'
                      : receipt != null
                      ? 'استلام سابق بدون تحصيل'
                      : quote == null
                      ? 'دفع الكارت · السعر غير محدد'
                      : 'دفع الكارت · ${money(quote)}',
                ),
              ),
              FilledButton.tonalIcon(
                key: const Key('receive-student-card'),
                onPressed:
                    store.canCollect &&
                        !student.isSuspended &&
                        receipt == null &&
                        store.canReceiveStudentCard(student.id)
                    ? onReceive
                    : null,
                icon: const Icon(Icons.check_circle_outline, size: 18),
                label: Text(receipt == null ? 'استلم الكارت' : 'الاستلام مسجل'),
              ),
            ],
          ),
          if (receipt == null && !paid)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                settings.requirePaymentBeforeReceipt
                    ? 'تسجيل دفع الكارت مطلوب قبل الاستلام؛ المبلغ المتبقي يُحفظ كمديونية.'
                    : 'يسمح إعداد السنتر بالاستلام بدون تحصيل مسجل.',
                style: TextStyle(fontSize: 12, color: colors.muted),
              ),
            ),
        ],
      ),
    );
  }
}
