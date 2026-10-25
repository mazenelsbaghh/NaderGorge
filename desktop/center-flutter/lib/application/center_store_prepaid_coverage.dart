part of 'center_store.dart';

extension _PrepaidAttendanceCoverage on CenterStore {
  Future<void> _recordAttendanceWithCredit(EntryRequest request) {
    final reviewed = List<String>.unmodifiable(
      request.acknowledgedAttendanceIds,
    );
    var changed = false;
    return _change(
      'attendance_record',
      'تسجيل حضور وربطه بالرصيد المتاح دون تحصيل',
      () async {
        changed = _recordAttendance(request, reviewed);
        final entry = _attendancesForStudent(
          request.studentId,
        ).firstWhere((entry) => entry.sessionId == request.sessionId);
        final package = _prepaidPackageForAttendance(entry);
        if (package == null) return;
        _requirePreviousClosed(_session(entry.sessionId));
        _correctPrepaidAttendance(entry, package, currentUser!.id);
        changed = true;
      },
      commitWhen: () => changed,
      auditDescription: () =>
          'تسجيل حضور — كود ${_student(request.studentId).code} — حصة ${_session(request.sessionId).number} — ربط الرصيد المتاح دون تحصيل جديد${reviewed.isEmpty ? '' : ' — مراجعة التسجيلات ${reviewed.join(', ')}'}',
    );
  }

  PrepaidPackage? _prepaidPackageForAttendance(AttendanceRecord entry) {
    if (!entry.paymentPending ||
        entry.packageId != null ||
        entry.originalAttendanceId != null ||
        entry.centerFeeOnly ||
        entry.packageMember ||
        entry.importSource.isNotEmpty) {
      return null;
    }
    final session = _session(entry.sessionId);
    if (!session.startsAtKnown ||
        session.status == SessionStatus.canceled ||
        isCairoGroup(session.groupId) ||
        (session.kind != SessionKind.counted &&
            entry.makeupSourceGroupId == null)) {
      return null;
    }
    return _available(
      entry.studentId,
      entry.makeupSourceGroupId ?? session.groupId,
      forClosure: session,
    ).firstOrNull;
  }

  void _correctPrepaidAttendance(
    AttendanceRecord entry,
    PrepaidPackage package,
    String staffId,
  ) {
    final replacement = entry.copyWith(
      id: CenterStore._uuid.v4(),
      packageId: package.id,
      paymentPending: false,
    );
    _consume(package);
    _state.attendances.add(replacement);
    _state.corrections.add(
      CorrectionRecord(
        id: CenterStore._uuid.v4(),
        action: CorrectionAction.entryCorrected,
        studentId: entry.studentId,
        sessionId: entry.sessionId,
        attendanceId: entry.id,
        replacementAttendanceId: replacement.id,
        packageId: package.id,
        reason: 'ربط حضور غير مسدد برصيد شهر سابق مؤهل دون تحصيل جديد',
        staffId: staffId,
        createdAt: DateTime.now(),
      ),
    );
  }

  Future<void> _repairPrepaidAttendance() async {
    if (!_activeAttendances.any((entry) => entry.paymentPending)) return;
    final owner = _state.staff.firstWhere(
      (staff) => staff.role == StaffRole.admin,
    );
    var corrected = 0;
    await _change(
      'attendance_prepaid_repair',
      'تصحيح ربط الحضور القديم برصيد الشهر',
      () async {
        final pending =
            _activeAttendances.where((entry) => entry.paymentPending).toList()
              ..sort((a, b) {
                final first = _session(a.sessionId),
                    second = _session(b.sessionId);
                if (_precedes(first, second)) return -1;
                if (_precedes(second, first)) return 1;
                return a.id.compareTo(b.id);
              });
        for (final entry in pending) {
          final package = _prepaidPackageForAttendance(entry);
          if (package == null) continue;
          if (corrected == 0) {
            await _writeBackup(
              p.join(
                p.dirname(databasePath),
                'backups',
                'before-prepaid-coverage-${CenterStore._uuid.v4()}.json',
              ),
              _state.copyForBackup(),
            );
          }
          _correctPrepaidAttendance(entry, package, owner.id);
          corrected++;
        }
      },
      actorId: owner.id,
      commitWhen: () => corrected > 0,
      auditDescription: () =>
          'تصحيح تلقائي: ربط $corrected حضور سابق برصيد شهر مؤهل دون تحصيل جديد؛ التقفيلات المحفوظة لم تتغير.',
    );
  }
}
