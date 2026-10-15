import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/notice_dialog.dart';

import '../management/corrections_page.dart';

bool canAllocatePaymentToOnline(CenterStore store, PaymentRecord payment) =>
    store.canCollect &&
    payment.packageId != null &&
    payment.netAmount > 0 &&
    store
        .paymentsForStudent(payment.studentId)
        .any((p) => p.id == payment.id) &&
    store
        .packagesForStudent(payment.studentId, groupId: payment.groupId)
        .any(
          (package) =>
              package.id == payment.packageId &&
              package.paymentId == payment.id,
        ) &&
    store.paymentDebtFor(payment.id) == 0 &&
    store.centerAllocatedAmount(payment.id) > 0;

Future<void> openOnlinePaymentAllocation(
  BuildContext context, {
  required CenterStore store,
  required PaymentRecord payment,
}) async {
  if (!context.mounted || !store.canCollect) return;
  if (!canAllocatePaymentToOnline(store, payment)) {
    await showMassarNotice(
      context,
      'دفعة الشهر لم تعد متاحة للتحويل؛ راجع سجل الدفع وحدّث الاختيار.',
      kind: NoticeKind.warning,
    );
    return;
  }
  await Navigator.of(context).push(
    MaterialPageRoute<void>(
      builder: (_) => Scaffold(
        appBar: AppBar(title: const Text('تسوية دفعة الشهر للأونلاين')),
        body: CorrectionsPage(
          store: store,
          initialStudentId: payment.studentId,
          initialSessionId: payment.sessionId,
          initialPaymentId: payment.id,
        ),
      ),
    ),
  );
}
