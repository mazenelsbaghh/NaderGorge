part of 'center_reports.dart';

extension _StudentCardReports on _ReportContext {
  bool _cardPaymentMatches(StudentCardPayment payment) =>
      _paymentScope(payment.studentId, payment.groupId, payment.sessionId) &&
      _dateMatches(payment.createdAt);

  StudentDebt _cardDebt(StudentCardPayment payment) => StudentDebt(
    paymentId: payment.id,
    kind: DebtKind.card,
    studentId: payment.studentId,
    groupId: payment.groupId,
    sessionId: payment.sessionId,
    description: 'رسوم الكارت',
    dueAmount: payment.netAmount,
    collectedAmount: store.cardCollectedFor(payment.id),
    createdAt: payment.createdAt,
  );

  String _cardPaymentStatus(StudentCardPayment payment) {
    if (payment.netAmount == 0) return 'إعفاء كامل';
    if (store.cardDebtFor(payment.id) == 0) return 'مسدد بالكامل';
    return store.cardCollectedFor(payment.id) == 0
        ? 'لم يُحصل مبلغ'
        : 'دفع جزئي';
  }

  List<Object?> _cardPaymentRow(StudentCardPayment payment) => [
    _timeLabel(payment.createdAt),
    payment.collectedAmount == 0 && payment.netAmount > 0
        ? 'تسجيل استحقاق كارت دون تحصيل'
        : 'تحصيل كارت',
    students[payment.studentId]!.code,
    students[payment.studentId]!.name,
    _optionalGroupLabel(payment.groupId),
    _sessionLabel(payment.sessionId),
    'رسوم كارت الطالب',
    reportAmount(payment.baseAmount),
    payment.discountPercent,
    reportAmount(payment.baseAmount - payment.netAmount),
    reportAmount(payment.collectedAmount),
    payment.method,
    payment.method,
    _cardPaymentStatus(payment),
    _staffLabel(payment.staffId),
    payment.id,
    payment.id,
    '',
    reportAmount(payment.netAmount),
    reportAmount(store.cardDebtFor(payment.id)),
    _sessionLabel(payment.sessionId),
    reportAmount(store.cardCollectedFor(payment.id)),
    _optionalGroupLabel(payment.groupId),
  ];

  bool _cardStudentMatches(Student student) =>
      _studentMatches(student.id) &&
      (student.groupIds.any(_groupMatches) ||
          (student.groupIds.isEmpty &&
              filter.groupId == null &&
              filter.subjectId == null &&
              filter.centerId == null &&
              filter.gradeId == null &&
              filter.sessionId == null));

  CenterReportData studentCardReport() {
    final roster = students.values.where(_cardStudentMatches).toList()
      ..sort((first, second) => first.code.compareTo(second.code));
    final received = roster
        .where((student) => store.cardReceiptFor(student.id) != null)
        .length;
    final paidNotReceived = roster
        .where(
          (student) =>
              store.cardPaymentFor(student.id) != null &&
              store.cardDebtFor(store.cardPaymentFor(student.id)!.id) == 0 &&
              store.cardReceiptFor(student.id) == null,
        )
        .length;
    return CenterReportData(
      title: 'كروت الطلبة',
      columns: const [
        'الكود',
        'الطالب',
        'المجموعات الحالية',
        'دفع الكارت',
        'المحصل حتى الآن (جنيه مصري)',
        'تاريخ الدفع',
        'طريقة الدفع',
        'حالة الاستلام',
        'تاريخ الاستلام',
        'موظف التسليم',
        'المستحق بعد الخصم (جنيه مصري)',
        'المديونية الحالية (جنيه مصري)',
      ],
      rows: roster.map((student) {
        final payment = store.cardPaymentFor(student.id);
        final receipt = store.cardReceiptFor(student.id);
        return <Object?>[
          student.code,
          student.name,
          student.groupIds.map(_groupLabel).join(' • '),
          payment == null ? 'لا يوجد دفع مسجل' : _cardPaymentStatus(payment),
          payment == null
              ? null
              : reportAmount(store.cardCollectedFor(payment.id)),
          payment == null ? null : _timeLabel(payment.createdAt),
          payment?.method,
          receipt == null
              ? 'لم يستلم'
              : receipt.paymentBypassed
              ? 'استلم بدون تحصيل مسجل'
              : 'استلم',
          receipt == null ? null : _timeLabel(receipt.receivedAt),
          receipt == null ? null : _staffLabel(receipt.staffId),
          payment == null ? null : reportAmount(payment.netAmount),
          payment == null ? null : reportAmount(store.cardDebtFor(payment.id)),
        ];
      }).toList(),
      summary: {
        'عدد الطلبة': '${roster.length}',
        'لم يستلموا': '${roster.length - received}',
        'استلموا': '$received',
        'مسددون ولم يستلموا': '$paidNotReceived',
        'عليهم مديونية كارت':
            '${roster.where((student) {
              final payment = store.cardPaymentFor(student.id);
              return payment != null && store.cardDebtFor(payment.id) > 0;
            }).length}',
        'المديونية الحالية':
            '${reportAmount(roster.fold<int>(0, (sum, student) {
              final payment = store.cardPaymentFor(student.id);
              return sum + (payment == null ? 0 : store.cardDebtFor(payment.id));
            }))} ج',
      },
      caption:
          'حالة الكارت الحالية لكل طالب حسب الكود والمجموعات الحالية؛ لا تتغير بالفترة المختارة. المحصل والمتبقي يشملان السداد اللاحق؛ دفع جزء أو تسجيل استحقاق بصفر لا يعني السداد الكامل. تقرير المدفوعات يعرض كل تحصيل للكارت بتاريخ الحركة الفعلي. الاستلام بدون تحصيل مسجل يثبت التسليم فقط ولا يثبت مبلغًا أو يضيف دخلًا. الطباعة وحدها لا تسجل الاستلام.',
    );
  }
}
