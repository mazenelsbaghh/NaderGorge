import '../domain/models.dart';

/// Builds reconciliation from payment snapshots, never from today's prices.
/// Unassigned payments and paper reviews are deliberately excluded.
SessionFinancialSummary buildSessionFinancialSummary({
  required LessonSession session,
  required Iterable<AttendanceRecord> attendances,
  required Iterable<PaymentRecord> payments,
  Iterable<StudentCardPayment> cardPayments = const [],
  Iterable<PrepaidPackage> packages = const [],
  Iterable<PaymentRecord>? packagePurchasePayments,
  Iterable<RefundRecord> refunds = const [],
  Iterable<DebtSettlement> debtSettlements = const [],
  Iterable<CenterFeeRecord> centerFees = const [],
  int coverageClassificationVersion = 2,
}) {
  final entries = attendances.where((e) => e.sessionId == session.id).toList();
  final operations = payments.where((e) => e.sessionId == session.id).toList();
  final cardCollections = cardPayments
      .where((payment) => payment.sessionId == session.id)
      .toList();
  final payouts = refunds.where((e) => e.sessionId == session.id).toList();
  final settlements = debtSettlements
      .where((e) => e.sessionId == session.id)
      .toList();
  final centerCollections = centerFees
      .where((fee) => fee.sessionId == session.id)
      .toList();
  final centerFeeCollected = centerCollections.fold<int>(
    0,
    (sum, fee) => sum + fee.paidAmount,
  );
  final refundedIds = payouts.map((e) => e.paymentId).toSet();
  final activeOperations = operations
      .where((e) => !refundedIds.contains(e.id))
      .toList();
  final purchases = <String, PaymentRecord>{
    for (final payment in packagePurchasePayments ?? payments)
      if (payment.packageId != null) payment.packageId!: payment,
  };
  final singles = operations.where((e) => e.packageId == null).toList();
  final directStudents = activeOperations
      .where((e) => e.packageId == null)
      .map((e) => e.studentId)
      .toSet();
  final present = entries
      .where((e) => e.status == AttendanceStatus.present)
      .toList();
  final prepaid = present.where((e) => e.packageId != null).length;
  final legacyFree = present
      .where(
        (e) =>
            session.kind == SessionKind.free &&
            e.packageId == null &&
            !directStudents.contains(e.studentId),
      )
      .length;
  final unpaid = entries
      .where(
        (e) =>
            (e.status == AttendanceStatus.present &&
                    session.kind != SessionKind.free ||
                e.status == AttendanceStatus.makeup &&
                    e.makeupSourceGroupId != null) &&
            !e.packageMember &&
            e.packageId == null &&
            !directStudents.contains(e.studentId) &&
            !(coverageClassificationVersion > 0 && e.centerFeeOnly),
      )
      .toList();
  final unpaidLabel = unpaid.any((entry) => entry.importSource.isNotEmpty)
      ? 'حضور — الدفع غير موثق'
      : 'حضور محفوظ — غير مدفوع';
  final actualAttendees = entries
      .where((e) => e.status != AttendanceStatus.absent)
      .toList();
  final exemptSingleStudents = activeOperations
      .where(
        (e) =>
            e.packageId == null &&
            (coverageClassificationVersion > 0
                ? e.netAmount == 0
                : e.discountPercent == 100),
      )
      .map((e) => e.studentId)
      .toSet();
  final freeStudents = <String>{};
  for (final entry in actualAttendees) {
    if (entry.packageMember) continue;
    if (coverageClassificationVersion > 0 && entry.centerFeeOnly ||
        !unpaid.any((e) => e.id == entry.id) &&
            (session.kind == SessionKind.free &&
                    entry.makeupSourceGroupId == null ||
                entry.fixedDiscountPercent == 100 ||
                exemptSingleStudents.contains(entry.studentId) ||
                (entry.packageId != null &&
                    (purchases[entry.packageId]?.discountPercent == 100 ||
                        coverageClassificationVersion > 0 &&
                            purchases[entry.packageId]?.netAmount == 0)))) {
      freeStudents.add(entry.studentId);
    }
  }
  final free = coverageClassificationVersion > 0
      ? freeStudents.length
      : legacyFree;
  final makeup = entries
      .where((e) => e.status == AttendanceStatus.makeup)
      .length;
  final sourceMakeup = entries.where(
    (e) => e.status == AttendanceStatus.makeup && e.makeupSourceGroupId != null,
  );
  final sourcePackageMakeup = sourceMakeup
      .where((e) => e.packageId != null)
      .length;
  final linkedMakeup = makeup - sourceMakeup.length;
  final absent = entries
      .where((e) => e.status == AttendanceStatus.absent)
      .length;
  final grouped = <(bool, int, num, int, int, String?), List<PaymentRecord>>{};
  final packageSizes = {
    for (final package in packages) package.id: package.totalSessions,
  };
  final packageMonthNames = {
    for (final package in packages) package.id: package.monthPlanName,
  };
  for (final operation in operations) {
    final key = (
      operation.packageId != null,
      operation.baseAmount,
      operation.discountPercent,
      operation.collectedAmount,
      packageSizes[operation.packageId] ?? 4,
      packageMonthNames[operation.packageId],
    );
    grouped.putIfAbsent(key, () => []).add(operation);
  }
  String amount(int value) =>
      '${value ~/ 100}.${(value % 100).toString().padLeft(2, '0')}';
  final lines = grouped.values.map((rows) {
    final first = rows.first;
    final count = packageSizes[first.packageId] ?? 4;
    final monthName = packageMonthNames[first.packageId];
    final kind = first.packageId != null
        ? monthName != null
              ? '$monthName — $count حصص'
              : switch (count) {
                  2 => 'بيع باقة حصتين',
                  3 => 'بيع باقة ٣ حصص',
                  _ => 'بيع باقة ٤ حصص',
                }
        : session.kind == SessionKind.extra
        ? 'حصة بسعر منفصل'
        : 'دفع بالحصة';
    return SessionPriceLine(
      label:
          '$kind — أصل ${amount(first.baseAmount)} ج.م، خصم ${first.discountPercent}٪',
      unitAmount: first.collectedAmount,
      count: rows.length,
    );
  }).toList();
  final cardGroups = <(int, num, int), List<StudentCardPayment>>{};
  for (final payment in cardCollections) {
    final key = (
      payment.baseAmount,
      payment.discountPercent,
      payment.collectedAmount,
    );
    cardGroups.putIfAbsent(key, () => []).add(payment);
  }
  for (final rows in cardGroups.values) {
    final first = rows.first;
    lines.add(
      SessionPriceLine(
        label:
            'دفع كارت — أصل ${amount(first.baseAmount)} ج.م، خصم ${first.discountPercent}٪',
        unitAmount: first.collectedAmount,
        count: rows.length,
      ),
    );
  }
  final centerFeeGroups = <int, int>{};
  for (final fee in centerCollections.where((fee) => fee.paidAmount > 0)) {
    centerFeeGroups.update(
      fee.paidAmount,
      (count) => count + 1,
      ifAbsent: () => 1,
    );
  }
  for (final entry in centerFeeGroups.entries) {
    lines.add(
      SessionPriceLine(
        label: coverageClassificationVersion > 0
            ? 'رسوم السنتر — تحصيل مستقل عن إيراد المدرس'
            : 'رسوم السنتر — تحصيل نقدي مستقل عن إيراد المدرس',
        unitAmount: entry.key,
        count: entry.value,
      ),
    );
  }
  for (final refund in payouts) {
    lines.add(
      SessionPriceLine(
        label: 'استرداد فعلي — ${refund.method}',
        unitAmount: -refund.amount,
        count: 1,
      ),
    );
  }
  for (final settlement in settlements) {
    lines.add(
      SessionPriceLine(
        label:
            'تسديد مديونية ${settlement.kind == DebtKind.card ? 'كارت' : 'حصة أو باقة'} — ${settlement.method}',
        unitAmount: settlement.amount,
        count: 1,
      ),
    );
  }
  if (prepaid > 0) {
    lines.add(
      SessionPriceLine(
        label: 'حضور من رصيد باقة، دون تحصيل إضافي',
        unitAmount: 0,
        count: prepaid,
      ),
    );
  }
  if (unpaid.isNotEmpty) {
    lines.add(
      SessionPriceLine(label: unpaidLabel, unitAmount: 0, count: unpaid.length),
    );
  }
  if (free > 0) {
    lines.add(
      SessionPriceLine(
        label: coverageClassificationVersion > 0
            ? 'حضور مجاني — إعفاء من رسوم المدرس'
            : 'حضور مجاني',
        unitAmount: 0,
        count: free,
      ),
    );
  }
  if (sourcePackageMakeup > 0) {
    lines.add(
      SessionPriceLine(
        label: 'تعويض من رصيد المجموعة الأصلية، دون تحصيل إضافي',
        unitAmount: 0,
        count: sourcePackageMakeup,
      ),
    );
  }
  if (linkedMakeup > 0) {
    lines.add(
      SessionPriceLine(
        label: 'تعويض عن غياب محسوب سابقًا',
        unitAmount: 0,
        count: linkedMakeup,
      ),
    );
  }
  if (absent > 0) {
    lines.add(
      SessionPriceLine(
        label: 'غياب — دون تحصيل جديد',
        unitAmount: 0,
        count: absent,
      ),
    );
  }
  final cardGross = cardCollections.fold<int>(
    0,
    (sum, e) => sum + e.baseAmount,
  );
  final cardNet = cardCollections.fold<int>(0, (sum, e) => sum + e.netAmount);
  final cardCollected = cardCollections.fold<int>(
    0,
    (sum, e) => sum + e.collectedAmount,
  );
  final gross =
      operations.fold<int>(0, (sum, e) => sum + e.baseAmount) +
      cardGross +
      centerFeeCollected;
  final total =
      operations.fold<int>(0, (sum, e) => sum + e.netAmount) +
      cardNet +
      centerFeeCollected;
  final initiallyCollected =
      operations.fold<int>(0, (sum, e) => sum + e.collectedAmount) +
      cardCollected +
      centerFeeCollected;
  final settled = settlements.fold<int>(0, (sum, e) => sum + e.amount);
  final returned = payouts.fold<int>(0, (sum, e) => sum + e.amount);
  final categories = <SessionStudentCategory>[];
  final paidGroups = <(bool, num, int), List<PaymentRecord>>{};
  for (final payment in activeOperations.where(
    (payment) => coverageClassificationVersion == 0 || payment.netAmount > 0,
  )) {
    final key = (
      payment.packageId != null,
      payment.discountPercent,
      payment.collectedAmount,
    );
    paidGroups.putIfAbsent(key, () => []).add(payment);
  }
  for (final rows in paidGroups.values) {
    final first = rows.first;
    final kind = first.packageId == null
        ? SessionStudentCategoryKind.single
        : SessionStudentCategoryKind.package;
    final noun = kind == SessionStudentCategoryKind.single ? 'حصة' : 'باقة';
    categories.add(
      SessionStudentCategory(
        kind: kind,
        label: first.discountPercent == 100
            ? '$noun بإعفاء 100٪'
            : first.discountPercent == 0
            ? '$noun بالسعر الكامل'
            : '$noun بخصم ${first.discountPercent}٪',
        discountPercent: first.discountPercent,
        unitAmount: first.collectedAmount,
        studentCount: rows.map((e) => e.studentId).toSet().length,
        operationCount: rows.length,
      ),
    );
  }
  final newlyPurchased = activeOperations
      .map((e) => e.packageId)
      .whereType<String>()
      .toSet();
  void attendanceCategory(
    SessionStudentCategoryKind kind,
    String label,
    Iterable<AttendanceRecord> rows,
  ) {
    final count = rows.map((e) => e.studentId).toSet().length;
    if (count > 0) {
      categories.add(
        SessionStudentCategory(
          kind: kind,
          label: label,
          unitAmount: 0,
          studentCount: count,
          operationCount: 0,
        ),
      );
    }
  }

  attendanceCategory(
    SessionStudentCategoryKind.packageMember,
    'باكدج — دون تحصيل',
    actualAttendees.where((e) => e.packageMember),
  );

  final priorPackageStudents = <num, Set<String>>{};
  for (final attendance
      in (coverageClassificationVersion >= 2 ? actualAttendees : present).where(
        (e) => e.packageId != null && !newlyPurchased.contains(e.packageId),
      )) {
    final purchase = purchases[attendance.packageId];
    if (purchase == null || purchase.studentId != attendance.studentId) {
      throw const CenterException(
        'تعذر العثور على عملية شراء الباقة الأصلية لتفاصيل التقفيلة.',
      );
    }
    priorPackageStudents
        .putIfAbsent(purchase.discountPercent, () => <String>{})
        .add(attendance.studentId);
  }
  for (final entry in priorPackageStudents.entries) {
    categories.add(
      SessionStudentCategory(
        kind: SessionStudentCategoryKind.prepaid,
        label: entry.key == 100
            ? 'حضور بباقة سابقة بإعفاء 100٪'
            : entry.key == 0
            ? 'حضور بباقة سابقة بالسعر الكامل'
            : 'حضور بباقة سابقة بخصم ${entry.key}٪',
        discountPercent: entry.key,
        unitAmount: 0,
        studentCount: entry.value.length,
        operationCount: 0,
      ),
    );
  }
  attendanceCategory(SessionStudentCategoryKind.unpaid, unpaidLabel, unpaid);
  attendanceCategory(
    SessionStudentCategoryKind.free,
    coverageClassificationVersion > 0
        ? 'حضور مجاني أو بإعفاء من رسوم المدرس'
        : 'حضور حصة مجانية',
    coverageClassificationVersion > 0
        ? actualAttendees.where((e) => freeStudents.contains(e.studentId))
        : present.where(
            (e) =>
                session.kind == SessionKind.free &&
                e.packageId == null &&
                !directStudents.contains(e.studentId),
          ),
  );
  attendanceCategory(
    SessionStudentCategoryKind.makeup,
    'تعويض',
    entries.where((e) => e.status == AttendanceStatus.makeup),
  );
  attendanceCategory(
    SessionStudentCategoryKind.absent,
    'غياب',
    entries.where((e) => e.status == AttendanceStatus.absent),
  );
  categories.sort((a, b) {
    final kind = a.kind.index.compareTo(b.kind.index);
    if (kind != 0) return kind;
    final discount = (a.discountPercent ?? 0).compareTo(b.discountPercent ?? 0);
    return discount != 0 ? discount : a.unitAmount.compareTo(b.unitAmount);
  });
  // Attendance discounts describe the student at registration, independently
  // of what an earlier package purchase cost. Absences have snapshots but do
  // not count as actual attendees in these sections.
  final fixedGroups = <num?, Set<String>>{};
  for (final entry in actualAttendees) {
    fixedGroups
        .putIfAbsent(entry.fixedDiscountPercent, () => <String>{})
        .add(entry.studentId);
  }
  final fixedCategories =
      fixedGroups.entries
          .map(
            (e) => AttendanceDiscountCategory(
              discountPercent: e.key,
              label: e.key == null
                  ? 'الخصم الثابت وقت الحضور غير معروف'
                  : e.key == 0
                  ? 'حضور بدون خصم ثابت'
                  : e.key == 100
                  ? 'حضور بإعفاء ثابت 100٪'
                  : 'حضور بخصم ثابت ${e.key}٪',
              studentCount: e.value.length,
            ),
          )
          .toList()
        ..sort(
          (a, b) =>
              (a.discountPercent ?? -1).compareTo(b.discountPercent ?? -1),
        );
  final amountGroups = <String, List<PaymentRecord>>{};
  for (final payment in activeOperations.where(
    (payment) => coverageClassificationVersion == 0 || payment.netAmount > 0,
  )) {
    final key =
        '${payment.packageId == null ? 'single' : 'package'}:${payment.collectedAmount}';
    amountGroups.putIfAbsent(key, () => []).add(payment);
  }
  final amountCategories =
      amountGroups.values
          .map(
            (rows) => SessionPaymentAmountCategory(
              kind: rows.first.packageId == null
                  ? SessionStudentCategoryKind.single
                  : SessionStudentCategoryKind.package,
              unitAmount: rows.first.collectedAmount,
              studentCount: rows.map((e) => e.studentId).toSet().length,
              operationCount: rows.length,
            ),
          )
          .toList()
        ..sort((a, b) {
          final kind = a.kind.index.compareTo(b.kind.index);
          return kind == 0 ? a.unitAmount.compareTo(b.unitAmount) : kind;
        });
  final settlementGroups = <int, List<DebtSettlement>>{};
  for (final settlement in settlements) {
    settlementGroups.putIfAbsent(settlement.amount, () => []).add(settlement);
  }
  final settlementAmounts = settlementGroups.keys.toList()..sort();
  for (final value in settlementAmounts) {
    final rows = settlementGroups[value]!;
    amountCategories.add(
      SessionPaymentAmountCategory(
        kind: SessionStudentCategoryKind.debtSettlement,
        unitAmount: value,
        studentCount: rows.map((e) => e.studentId).toSet().length,
        operationCount: rows.length,
      ),
    );
  }
  return SessionFinancialSummary(
    coverageClassificationVersion: coverageClassificationVersion > 0
        ? coverageClassificationVersion
        : null,
    sessionId: session.id,
    presentCount: present.length,
    makeupCount: makeup,
    absentCount: absent,
    prepaidCount:
        prepaid +
        (coverageClassificationVersion >= 2 ? sourcePackageMakeup : 0),
    singlePaymentCount: singles.length,
    packageSalesCount: operations.length - singles.length,
    freeCount: free,
    grossAmount: gross,
    discountAmount: gross - total,
    totalCollected: initiallyCollected + settled - returned,
    debtAmount: total - initiallyCollected,
    debtSettlementAmount: settled,
    refundAmount: returned,
    centerFeeCollected: centerFeeCollected,
    cardPaymentCount: cardCollections.length,
    cardCollectedAmount: cardCollected,
    expectedCash:
        operations
            .where((e) => e.method == 'نقدي')
            .fold<int>(0, (sum, e) => sum + e.collectedAmount) -
        payouts
            .where((e) => e.method == 'نقدي')
            .fold<int>(0, (sum, e) => sum + e.amount) +
        cardCollections
            .where((e) => e.method == 'نقدي')
            .fold<int>(0, (sum, e) => sum + e.collectedAmount) +
        settlements
            .where((e) => e.method == 'نقدي')
            .fold<int>(0, (sum, e) => sum + e.amount) +
        centerCollections
            .where((fee) => fee.method == 'نقدي')
            .fold<int>(0, (sum, fee) => sum + fee.paidAmount),
    lines: lines,
    studentCategories: categories,
    attendanceDiscountCategories: fixedCategories,
    allFreeCount: freeStudents.length,
    packageBuyerCount: activeOperations
        .where((e) => e.packageId != null)
        .map((e) => e.studentId)
        .toSet()
        .length,
    paymentAmountCategories: amountCategories,
    centerFeePaymentCategories: _centerFeePaymentCategories(
      centerCollections,
      actualAttendees,
    ),
  );
}

List<SessionCenterFeePaymentCategory> _centerFeePaymentCategories(
  List<CenterFeeRecord> receipts,
  List<AttendanceRecord> actualAttendees,
) {
  final centerOnlyStudents = actualAttendees
      .where((attendance) => attendance.centerFeeOnly)
      .map((attendance) => attendance.studentId)
      .toSet();
  final receiptGroups = <(bool, int), List<CenterFeeRecord>>{};
  for (final receipt in receipts.where((receipt) => receipt.paidAmount > 0)) {
    final categoryKey = (
      centerOnlyStudents.contains(receipt.studentId),
      receipt.paidAmount,
    );
    receiptGroups.putIfAbsent(categoryKey, () => []).add(receipt);
  }
  return receiptGroups.entries
      .map(
        (group) => SessionCenterFeePaymentCategory(
          centerOnly: group.key.$1,
          unitAmount: group.key.$2,
          studentCount: group.value
              .map((receipt) => receipt.studentId)
              .toSet()
              .length,
          operationCount: group.value.length,
        ),
      )
      .toList()
    ..sort((first, second) {
      if (first.centerOnly != second.centerOnly) {
        return first.centerOnly ? -1 : 1;
      }
      return first.unitAmount.compareTo(second.unitAmount);
    });
}
