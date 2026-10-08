import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/domain/models.dart';

final validationTime = DateTime.utc(2026, 10, 1, 12);

CenterState financialValidationFixture() {
  final state = CenterState();
  state.catalogs = [
    for (final kind in CatalogKind.values)
      CatalogEntry(id: kind.name, name: kind.name, kind: kind),
  ];
  state.groups = [
    for (var group = 1; group <= 2; group++)
      StudyGroup(
        id: 'group-$group',
        name: 'Group $group',
        subjectId: 'subject',
        centerId: 'center',
        gradeId: 'grade',
      ),
  ];
  state.students = [
    for (var student = 1; student <= 2; student++)
      Student(
        id: 'student-$student',
        name: 'Student $student',
        code: 'V$student',
        groupIds: ['group-$student'],
        createdAt: validationTime,
      ),
  ];
  state.enrollments = {
    for (var student = 1; student <= 2; student++)
      'student-$student:group-$student': validationTime.toIso8601String(),
  };
  state.sessions = [
    for (var session = 1; session <= 2; session++)
      LessonSession(
        id: 'session-$session',
        groupId: 'group-$session',
        number: 1,
        startsAt: validationTime,
        createdAt: validationTime,
      ),
  ];
  state.staff = const [
    StaffUser(id: 'admin', name: 'Admin', role: StaffRole.admin),
    StaffUser(id: 'assistant', name: 'Assistant', role: StaffRole.assistant),
  ];
  state.credentials = {
    for (final staff in state.staff)
      staff.id: {
        'algorithm': 'pbkdf2-sha256-120000',
        'hash': base64Encode(List.filled(32, 1)),
        'salt': base64Encode(List.filled(16, 2)),
      },
  };
  state.centerFees = [
    CenterFeeRecord(
      id: 'fee-original',
      studentId: 'student-1',
      sessionId: 'session-1',
      amount: 1500,
      paidAmount: 500,
      recordedAt: validationTime,
    ),
    CenterFeeRecord(
      id: 'fee-top-up',
      originalFeeId: 'fee-original',
      studentId: 'student-1',
      sessionId: 'session-1',
      amount: 1000,
      paidAmount: 1000,
      recordedAt: validationTime,
      staffId: 'admin',
    ),
  ];
  state.attendances = [
    AttendanceRecord(
      id: 'center-only',
      studentId: 'student-1',
      sessionId: 'session-1',
      status: AttendanceStatus.present,
      recordedAt: validationTime,
      centerFeeOnly: true,
      centerFeeAmount: 1500,
      fixedDiscountPercent: 100,
    ),
    AttendanceRecord(
      id: 'makeup',
      studentId: 'student-2',
      sessionId: 'session-1',
      status: AttendanceStatus.makeup,
      makeupSourceGroupId: 'group-2',
      recordedAt: validationTime,
    ),
  ];
  state.payments = [
    PaymentRecord(
      id: 'makeup-payment',
      studentId: 'student-2',
      groupId: 'group-2',
      sessionId: 'session-1',
      description: 'Makeup lesson',
      baseAmount: 6000,
      discountPercent: 0,
      netAmount: 6000,
      paidAmount: 3000,
      createdAt: validationTime,
      staffId: 'admin',
    ),
  ];
  return state;
}

void _changeFee(CenterState state, int index, Map<String, dynamic> changes) {
  state.centerFees[index] = CenterFeeRecord.fromJson({
    ...state.centerFees[index].toJson(),
    ...changes,
  });
}

void _cancelMakeupPayment(CenterState state, DateTime settledAt) {
  final canceledAt = validationTime.add(const Duration(minutes: 2));
  state.debtSettlements = [
    DebtSettlement(
      id: 'settlement',
      studentId: 'student-2',
      paymentId: 'makeup-payment',
      kind: DebtKind.lesson,
      amount: 1500,
      createdAt: settledAt,
      staffId: 'admin',
    ),
  ];
  state.corrections = [
    CorrectionRecord(
      id: 'payment-cancellation',
      action: CorrectionAction.paymentCanceled,
      studentId: 'student-2',
      sessionId: 'session-1',
      paymentId: 'makeup-payment',
      voidsPayment: true,
      reason: 'Correction',
      staffId: 'admin',
      createdAt: canceledAt,
    ),
  ];
  state.refunds = [
    RefundRecord(
      id: 'refund',
      correctionId: 'payment-cancellation',
      paymentId: 'makeup-payment',
      studentId: 'student-2',
      groupId: 'group-2',
      sessionId: 'session-1',
      amount: 4500,
      method: 'نقدي',
      reason: 'Correction',
      staffId: 'admin',
      createdAt: canceledAt,
    ),
  ];
}

typedef ValidationScenario = ({
  String name,
  void Function(CenterState) change,
  String? error,
});

List<ValidationScenario> validationScenarios() => [
  (
    name: 'partial fee and top-up may precede their original in storage',
    change: (state) => state.centerFees = state.centerFees.reversed.toList(),
    error: null,
  ),
  (
    name: 'legacy zero-paid invoice remains evidence of center-only attendance',
    change: (state) {
      state.centerFees.removeLast();
      _changeFee(state, 0, {'paidAmount': 0});
    },
    error: null,
  ),
  (
    name: 'overcollection is rejected before a later malformed top-up',
    change: (state) => _changeFee(state, 1, {
      'amount': 1100,
      'paidAmount': 1100,
      'studentId': 'student-2',
    }),
    error: 'تحصيل رسوم السنتر أكبر من المبلغ المستحق.',
  ),
  (
    name: 'top-up identity is checked first when its receipt occurs first',
    change: (state) {
      _changeFee(state, 1, {'studentId': 'student-2'});
      state.centerFees = state.centerFees.reversed.toList();
    },
    error: 'إيصال سداد رسوم السنتر غير مرتبط بأصل الرسوم الصحيح.',
  ),
  for (final staffId in ['assistant', 'unknown-staff'])
    (
      name: 'fee collection rejects $staffId',
      change: (state) => _changeFee(state, 1, {'staffId': staffId}),
      error: 'رسوم السنتر مرتبطة بطالب أو حصة غير موجودة، أو مبلغ غير صالح.',
    ),
  for (final changed in [
    {'studentId': 'student-2'},
    {'sessionId': 'session-2'},
  ])
    (
      name: 'fees for another ${changed.keys.single} cannot cover attendance',
      change: (state) {
        for (var index = 0; index < state.centerFees.length; index++) {
          _changeFee(state, index, changed);
        }
      },
      error:
          'حضور السنتر فقط يجب أن يحتفظ بإعفاء المدرس ورسوم منفصلة دون استهلاك باقة.',
    ),
  (
    name: 'makeup requires matching student in source-group payment evidence',
    change: (state) =>
        state.payments[0] = state.payments[0].copyWith(studentId: 'student-1'),
    error: 'الدفع مرتبط بحصة مختلفة.',
  ),
  (
    name: 'makeup cannot borrow a payment from another session',
    change: (state) =>
        state.payments[0] = state.payments[0].copyWith(sessionId: 'session-2'),
    error: 'التعويض لا يحمل دفع حصة أو باقة صالحة من المجموعة الأصلية.',
  ),
  for (final minutes in [1, 2, 3])
    (
      name: 'settlement at minute $minutes respects cancellation at minute 2',
      change: (state) => _cancelMakeupPayment(
        state,
        validationTime.add(Duration(minutes: minutes)),
      ),
      error: minutes <= 2
          ? null
          : 'لا يمكن تسجيل تسديد بعد إلغاء أصل المديونية.',
    ),
  (
    name: 'voided makeup remains evidence for its historical receipt',
    change: (state) => state.corrections.add(
      CorrectionRecord(
        id: 'entry-reversal',
        action: CorrectionAction.entryReversed,
        studentId: 'student-2',
        sessionId: 'session-1',
        attendanceId: 'makeup',
        reason: 'Correction',
        staffId: 'admin',
        createdAt: validationTime.add(const Duration(minutes: 1)),
      ),
    ),
    error: null,
  ),
  for (final amount in [1500, 1400])
    (
      name: 'historical fee review validates its saved amount $amount',
      change: (state) => state.paymentChecks.add(
        PaymentCheck(
          id: 'fee-review',
          studentId: 'student-1',
          sessionId: 'session-1',
          status: StudentPaymentStatus.free,
          staffId: 'admin',
          checkedAt: validationTime,
          amount: amount,
        ),
      ),
      error: amount == 1500
          ? null
          : 'مبلغ مراجعة الإيصال لا يطابق التحصيل التاريخي للحصة.',
    ),
  (
    name: 'later cancellation preserves payment amount reviewed before it',
    change: (state) {
      _cancelMakeupPayment(
        state,
        validationTime.add(const Duration(minutes: 1)),
      );
      state.paymentChecks.add(
        PaymentCheck(
          id: 'makeup-review',
          studentId: 'student-2',
          sessionId: 'session-1',
          status: StudentPaymentStatus.paidSingle,
          paymentId: 'makeup-payment',
          staffId: 'admin',
          checkedAt: validationTime,
          amount: 3000,
        ),
      );
    },
    error: null,
  ),
];

void main() {
  test(
    'valid fee and cross-group makeup history is not mutated by validation',
    () {
      final state = financialValidationFixture();
      final snapshot = jsonEncode(state.toJson());
      validateState(state);
      expect(jsonEncode(state.toJson()), snapshot);
    },
  );

  for (final scenario in validationScenarios()) {
    test(scenario.name, () {
      final state = financialValidationFixture();
      scenario.change(state);
      if (scenario.error == null) {
        expect(() => validateState(state), returnsNormally);
      } else {
        expect(
          () => validateState(state),
          throwsA(
            isA<CenterException>().having(
              (error) => error.message,
              'message',
              scenario.error,
            ),
          ),
        );
      }
    });
  }
}
