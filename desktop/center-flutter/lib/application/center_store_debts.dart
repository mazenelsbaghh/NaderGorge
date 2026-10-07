part of 'center_store.dart';

extension CenterStoreDebts on CenterStore {
  List<DebtSettlement> get debtSettlements =>
      List.unmodifiable(_state.debtSettlements);

  int _settledAmount(String paymentId, DebtKind kind) => _state.debtSettlements
      .where((e) => e.paymentId == paymentId && e.kind == kind)
      .fold<int>(0, (sum, e) => sum + e.amount);

  int paymentCollectedFor(String paymentId) {
    final payment =
        _readIndex?.paymentsById[paymentId] ??
        _find<PaymentRecord>(
          _state.payments,
          (e) => e.id == paymentId,
          'عملية الدفع غير موجودة.',
        );
    return payment.collectedAmount + _settledAmount(paymentId, DebtKind.lesson);
  }

  int cardCollectedFor(String paymentId) {
    final payment = _find<StudentCardPayment>(
      _state.cardPayments,
      (e) => e.id == paymentId,
      'عملية دفع الكارت غير موجودة.',
    );
    return payment.collectedAmount + _settledAmount(paymentId, DebtKind.card);
  }

  int paymentDebtFor(String paymentId) {
    final payment =
        _readIndex?.paymentsById[paymentId] ??
        _find<PaymentRecord>(
          _state.payments,
          (e) => e.id == paymentId,
          'عملية الدفع غير موجودة.',
        );
    return _voidPaymentIds.contains(paymentId)
        ? 0
        : payment.netAmount - paymentCollectedFor(paymentId);
  }

  int cardDebtFor(String paymentId) {
    final payment = _find<StudentCardPayment>(
      _state.cardPayments,
      (e) => e.id == paymentId,
      'عملية دفع الكارت غير موجودة.',
    );
    return payment.netAmount - cardCollectedFor(paymentId);
  }

  List<StudentDebt> debtsFor(String studentId) {
    _require(canCollect);
    _student(studentId);
    final rows =
        <StudentDebt>[
            for (final payment in _paymentsForStudent(studentId))
              StudentDebt(
                paymentId: payment.id,
                kind: DebtKind.lesson,
                studentId: studentId,
                groupId: payment.groupId,
                sessionId: payment.sessionId,
                description: payment.description,
                dueAmount: payment.netAmount,
                collectedAmount: paymentCollectedFor(payment.id),
                createdAt: payment.createdAt,
              ),
            for (final payment in _state.cardPayments.where(
              (e) => e.studentId == studentId,
            ))
              StudentDebt(
                paymentId: payment.id,
                kind: DebtKind.card,
                studentId: studentId,
                groupId: payment.groupId,
                sessionId: payment.sessionId,
                description: 'رسوم الكارت',
                dueAmount: payment.netAmount,
                collectedAmount: cardCollectedFor(payment.id),
                createdAt: payment.createdAt,
              ),
          ].where((e) => e.remainingAmount > 0).toList()
          ..sort((a, b) => a.createdAt.compareTo(b.createdAt));
    return List.unmodifiable(rows);
  }

  int studentDebtFor(String studentId) =>
      debtsFor(studentId).fold<int>(0, (sum, e) => sum + e.remainingAmount);

  String _debtCoverageLabel(String paymentId, String label) {
    final remaining = paymentDebtFor(paymentId);
    return remaining == 0
        ? label
        : '$label — المتحصل ${_auditAmount(paymentCollectedFor(paymentId))}، مديونية متبقية ${_auditAmount(remaining)}';
  }

  String _collectionAudit(int due, int collected, String paymentId) =>
      'عملية $paymentId — مستحق ${_auditAmount(due)}، متحصل ${_auditAmount(collected)}، مديونية جديدة ${_auditAmount(due - collected)}';

  int _validatedPaidAmount(int due, int? requested) {
    if (requested == null) return due;
    if (due == 0) {
      throw const CenterException(
        'لا يوجد مبلغ مستحق لهذه العملية؛ اترك الدفع المخصص غير محدد.',
      );
    }
    if (requested < 0 || requested > due) {
      throw const CenterException(
        'المبلغ المدفوع من صفر إلى إجمالي المستحق فقط.',
      );
    }
    return requested;
  }

  void _validateExpectedNet(int due, int? expected) {
    if (expected != null && due != expected) {
      throw const CenterException(
        'تغيّر السعر أو الخصم؛ راجع المبلغ قبل الدفع.',
      );
    }
  }

  void _requireDebtRefundOpen(String paymentId) {
    for (final settlement in _state.debtSettlements.where(
      (e) => e.kind == DebtKind.lesson && e.paymentId == paymentId,
    )) {
      _requireFinancialOpen(settlement.sessionId);
    }
  }
}
