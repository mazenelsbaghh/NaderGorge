part of 'center_store.dart';

/// Read acceleration belongs to one snapshot, never to an in-flight command.
class _CenterReadIndex {
  _CenterReadIndex(this.state, {_CenterReadIndex? previous})
    : _cache = {...?previous?._cache};

  final CenterState state;
  final Map<Symbol, _CachedRead> _cache;

  // Carry only realized values, never a chain of old indexes or their closures.
  T _read<T extends Object>(
    Symbol key,
    List<List<Object>> sources,
    T Function() build,
  ) {
    final previous = _cache[key];
    final value = previous != null && previous.matches(sources)
        ? previous.value as T
        : build();
    _cache[key] = _CachedRead(sources, value);
    return value;
  }

  Map<String, T> _byId<T extends Object>(
    Symbol key,
    List<T> records,
    String Function(T) id,
  ) => _read(
    key,
    [records],
    () => Map.unmodifiable({for (final record in records) id(record): record}),
  );

  Map<K, List<T>> _grouped<T extends Object, K>(
    Symbol key,
    List<T> records,
    K Function(T) group,
  ) => _read(
    key,
    [records],
    () => Map.unmodifiable(
      _recordsBy(
        records,
        group,
      ).map((key, rows) => MapEntry(key, List<T>.unmodifiable(rows))),
    ),
  );

  late final _corrections = _read(#corrections, [
    state.corrections,
  ], () => _CorrectionReadIndex(state.corrections));
  late final List<AttendanceRecord> attendances = _read(
    #attendances,
    [state.attendances, state.corrections],
    () => List.unmodifiable(
      state.attendances.where(
        (entry) => !_corrections.voidAttendances.contains(entry.id),
      ),
    ),
  );
  late final List<PaymentRecord> payments = _read(
    #payments,
    [state.payments, state.corrections],
    () => List.unmodifiable(
      state.payments
          .where((payment) => !_corrections.voidPayments.contains(payment.id))
          .map(
            (payment) => _corrections.methods.containsKey(payment.id)
                ? payment.copyWith(method: _corrections.methods[payment.id]!)
                : payment,
          ),
    ),
  );
  late final List<PrepaidPackage> packages = _read(
    #packages,
    [state.packages, state.corrections],
    () => List.unmodifiable(
      state.packages.where(
        (package) => !_corrections.voidPackages.contains(package.id),
      ),
    ),
  );
  late final attendancesByStudent = _grouped(
    #attendancesByStudent,
    attendances,
    (entry) => entry.studentId,
  );
  late final attendancesBySession = _grouped(
    #attendancesBySession,
    attendances,
    (entry) => entry.sessionId,
  );
  late final paymentsByStudent = _grouped(
    #paymentsByStudent,
    payments,
    (payment) => payment.studentId,
  );
  late final packagesByStudent = _grouped(
    #packagesByStudent,
    packages,
    (package) => package.studentId,
  );
  late final packagesByStudentGroup = _grouped(
    #packagesByStudentGroup,
    packages,
    (package) => (package.studentId, package.groupId),
  );
  late final Map<(String, String), int> remainingByStudentGroup = _read(
    #remainingByStudentGroup,
    [packages],
    () {
      final remaining = <(String, String), int>{};
      for (final package in packages) {
        final key = (package.studentId, package.groupId);
        remaining[key] = (remaining[key] ?? 0) + package.remaining;
      }
      return Map.unmodifiable(remaining);
    },
  );
  late final studentsById = _byId(
    #studentsById,
    state.students,
    (student) => student.id,
  );
  late final sessionsById = _byId(
    #sessionsById,
    state.sessions,
    (session) => session.id,
  );
  late final paymentsById = _byId(
    #paymentsById,
    state.payments,
    (payment) => payment.id,
  );
  late final groupsById = _byId(#groupsById, state.groups, (group) => group.id);
  late final allAttendancesByStudent = _grouped(
    #allAttendancesByStudent,
    state.attendances,
    (entry) => entry.studentId,
  );
  late final allPaymentsByStudent = _grouped(
    #allPaymentsByStudent,
    state.payments,
    (entry) => entry.studentId,
  );
  late final academicsByStudent = _grouped(
    #academicsByStudent,
    state.academics,
    (entry) => entry.studentId,
  );
  late final cardPaymentsByStudent = _grouped(
    #cardPaymentsByStudent,
    state.cardPayments,
    (entry) => entry.studentId,
  );
  late final centerFeesByStudent = _grouped(
    #centerFeesByStudent,
    state.centerFees,
    (entry) => entry.studentId,
  );
  late final centerFeesByStudentSession = _grouped(
    #centerFeesByStudentSession,
    state.centerFees,
    (entry) => (entry.studentId, entry.sessionId),
  );
  late final settlementsByStudent = _grouped(
    #settlementsByStudent,
    state.debtSettlements,
    (entry) => entry.studentId,
  );
  late final refundsByStudent = _grouped(
    #refundsByStudent,
    state.refunds,
    (entry) => entry.studentId,
  );
  late final correctionsByStudent = _grouped(
    #correctionsByStudent,
    state.corrections,
    (entry) => entry.studentId,
  );
  late final Map<String, List<Student>> studentsByGroup = _read(
    #studentsByGroup,
    [state.students],
    () {
      final grouped = <String, List<Student>>{};
      for (final student in state.students) {
        for (final group in student.groupIds) {
          grouped.putIfAbsent(group, () => []).add(student);
        }
      }
      return Map.unmodifiable(
        grouped.map(
          (key, rows) => MapEntry(key, List<Student>.unmodifiable(rows)),
        ),
      );
    },
  );
  late final academicsBySession = _grouped(
    #academicsBySession,
    state.academics,
    (entry) => entry.sessionId,
  );
  late final activitiesById = _byId(
    #activitiesById,
    state.academicActivities,
    (activity) => activity.id,
  );
}

class _CachedRead {
  const _CachedRead(this.sources, this.value);
  final List<List<Object>> sources;
  final Object value;

  bool matches(List<List<Object>> next) {
    if (sources.length != next.length) return false;
    for (var section = 0; section < sources.length; section++) {
      final before = sources[section];
      final after = next[section];
      if (identical(before, after)) continue;
      if (before.length != after.length) return false;
      for (var row = 0; row < before.length; row++) {
        if (!identical(before[row], after[row])) return false;
      }
    }
    return true;
  }
}

class _CorrectionReadIndex {
  _CorrectionReadIndex(List<CorrectionRecord> corrections) {
    for (final correction in corrections) {
      if (correction.attendanceId != null) {
        voidAttendances.add(correction.attendanceId!);
      }
      if (correction.voidsPayment) voidPayments.add(correction.paymentId!);
      if (correction.voidsPackage) voidPackages.add(correction.packageId!);
      if (correction.action == CorrectionAction.paymentMethod) {
        methods[correction.paymentId!] = correction.newMethod!;
      }
    }
  }
  final voidAttendances = <String>{};
  final voidPayments = <String>{};
  final voidPackages = <String>{};
  final methods = <String, String>{};
}

Map<K, List<T>> _recordsBy<T, K>(Iterable<T> records, K Function(T) key) {
  final grouped = <K, List<T>>{};
  for (final record in records) {
    grouped.putIfAbsent(key(record), () => []).add(record);
  }
  return grouped;
}
