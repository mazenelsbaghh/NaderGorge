part of 'center_store.dart';

/// Active attendance and complete financial/correction history stay distinct.
class StudentHistoryRecords {
  StudentHistoryRecords._(_CenterReadIndex index, String studentId)
    : attendances = List.unmodifiable(
        index.attendancesByStudent[studentId] ?? const [],
      ),
      allAttendances = List.unmodifiable(
        index.allAttendancesByStudent[studentId] ?? const [],
      ),
      payments = List.unmodifiable(
        index.allPaymentsByStudent[studentId] ?? const [],
      ),
      activePaymentIds = Set.unmodifiable(
        (index.paymentsByStudent[studentId] ?? const <PaymentRecord>[]).map(
          (payment) => payment.id,
        ),
      ),
      academics = List.unmodifiable(
        index.academicsByStudent[studentId] ?? const [],
      ),
      cardPayments = List.unmodifiable(
        index.cardPaymentsByStudent[studentId] ?? const [],
      ),
      centerFees = List.unmodifiable(
        index.centerFeesByStudent[studentId] ?? const [],
      ),
      settlements = List.unmodifiable(
        index.settlementsByStudent[studentId] ?? const [],
      ),
      refunds = List.unmodifiable(
        index.refundsByStudent[studentId] ?? const [],
      ),
      corrections = List.unmodifiable(
        index.correctionsByStudent[studentId] ?? const [],
      ),
      activitiesById = index.activitiesById;

  final List<AttendanceRecord> attendances, allAttendances;
  final List<PaymentRecord> payments;
  final Set<String> activePaymentIds;
  final List<AcademicRecord> academics;
  final List<StudentCardPayment> cardPayments;
  final List<CenterFeeRecord> centerFees;
  final List<DebtSettlement> settlements;
  final List<RefundRecord> refunds;
  final List<CorrectionRecord> corrections;
  final Map<String, AcademicActivity> activitiesById;
}
