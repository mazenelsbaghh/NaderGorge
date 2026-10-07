part of 'center_store.dart';

/// Read acceleration belongs to one snapshot, never to an in-flight command.
class _CenterReadIndex {
  _CenterReadIndex(this.state) {
    final voidAttendances = <String>{};
    final voidPayments = <String>{};
    final voidPackages = <String>{};
    final methods = <String, String>{};
    for (final correction in state.corrections) {
      if (correction.attendanceId != null) {
        voidAttendances.add(correction.attendanceId!);
      }
      if (correction.voidsPayment) voidPayments.add(correction.paymentId!);
      if (correction.voidsPackage) voidPackages.add(correction.packageId!);
      if (correction.action == CorrectionAction.paymentMethod) {
        methods[correction.paymentId!] = correction.newMethod!;
      }
    }
    attendances = List.unmodifiable(
      state.attendances.where((entry) => !voidAttendances.contains(entry.id)),
    );
    payments = List.unmodifiable(
      state.payments
          .where((payment) => !voidPayments.contains(payment.id))
          .map(
            (payment) => methods.containsKey(payment.id)
                ? payment.copyWith(method: methods[payment.id]!)
                : payment,
          ),
    );
    packages = List.unmodifiable(
      state.packages.where((package) => !voidPackages.contains(package.id)),
    );
    attendancesByStudent = _recordsBy(attendances, (entry) => entry.studentId);
    paymentsByStudent = _recordsBy(payments, (payment) => payment.studentId);
  }

  final CenterState state;
  late final List<AttendanceRecord> attendances;
  late final List<PaymentRecord> payments;
  late final List<PrepaidPackage> packages;
  late final Map<String, List<AttendanceRecord>> attendancesByStudent;
  late final Map<String, List<PaymentRecord>> paymentsByStudent;
  late final studentsById = {
    for (final student in state.students) student.id: student,
  };
  late final sessionsById = {
    for (final session in state.sessions) session.id: session,
  };
  late final paymentsById = {
    for (final payment in state.payments) payment.id: payment,
  };
  late final groupsById = {for (final group in state.groups) group.id: group};
  late final allAttendancesByStudent = _recordsBy(
    state.attendances,
    (entry) => entry.studentId,
  );
  late final allPaymentsByStudent = _recordsBy(
    state.payments,
    (entry) => entry.studentId,
  );
  late final academicsByStudent = _recordsBy(
    state.academics,
    (entry) => entry.studentId,
  );
  late final cardPaymentsByStudent = _recordsBy(
    state.cardPayments,
    (entry) => entry.studentId,
  );
  late final centerFeesByStudent = _recordsBy(
    state.centerFees,
    (entry) => entry.studentId,
  );
  late final settlementsByStudent = _recordsBy(
    state.debtSettlements,
    (entry) => entry.studentId,
  );
  late final refundsByStudent = _recordsBy(
    state.refunds,
    (entry) => entry.studentId,
  );
  late final correctionsByStudent = _recordsBy(
    state.corrections,
    (entry) => entry.studentId,
  );
  late final activitiesById = Map<String, AcademicActivity>.unmodifiable({
    for (final activity in state.academicActivities) activity.id: activity,
  });
}

Map<K, List<T>> _recordsBy<T, K>(Iterable<T> records, K Function(T) key) {
  final grouped = <K, List<T>>{};
  for (final record in records) {
    grouped.putIfAbsent(key(record), () => []).add(record);
  }
  return grouped;
}
