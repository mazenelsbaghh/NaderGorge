part of 'center_store.dart';

extension CenterStoreCenterFees on CenterStore {
  Future<void> _setStudentPackageMember({
    required String studentId,
    required bool enabled,
    String? sessionId,
  }) => _change('student_package_member', 'حفظ علامة باكدج للطالب', () async {
    _require(canCollect);
    final student = _student(studentId);
    if (sessionId != null) {
      final session = _session(sessionId);
      final entry = _activeAttendances
          .where(
            (a) =>
                a.studentId == studentId &&
                a.sessionId == sessionId &&
                a.status != AttendanceStatus.absent,
          )
          .firstOrNull;
      if (entry != null && entry.packageMember != enabled) {
        _requireFinancialOpen(sessionId);
        if (session.status != SessionStatus.open ||
            entry.packageId != null ||
            entry.originalAttendanceId != null ||
            _activePayments.any(
              (p) => p.studentId == studentId && p.sessionId == sessionId,
            ) ||
            _state.centerFees.any(
              (f) => f.studentId == studentId && f.sessionId == sessionId,
            )) {
          throw const CenterException(
            'الحضور له حساب محفوظ أو الحصة مغلقة؛ صحح الحساب أولًا أو غيّر علامة الطالب خارج هذه الحصة.',
          );
        }
        _replace(
          _state.attendances,
          entry.id,
          entry.copyWith(
            packageMember: enabled,
            centerFeeOnly: false,
            centerFeeAmount: 0,
            paymentPending:
                !enabled &&
                (session.kind != SessionKind.free ||
                    entry.makeupSourceGroupId != null),
          ),
          (a) => a.id,
        );
      }
    }
    _replace(
      _state.students,
      studentId,
      student.copyWith(
        packageMember: enabled,
        centerFeeEnabled: enabled ? false : student.centerFeeEnabled,
        centerOnly: enabled ? false : student.centerOnly,
      ),
      (s) => s.id,
    );
  });

  Future<void> _setStudentCenterFee({
    required String studentId,
    required bool enabled,
  }) => _change(
    'student_center_fee_setting',
    'حفظ اختيار رسوم السنتر المستقلة',
    () async {
      _require(canCollect);
      final student = _student(studentId);
      if (enabled && student.packageMember) {
        throw const CenterException('ألغِ علامة باكدج قبل تفعيل رسوم السنتر.');
      }
      _replace(
        _state.students,
        student.id,
        student.copyWith(centerFeeEnabled: enabled, centerOnly: false),
        (e) => e.id,
      );
    },
  );

  Future<void> _collectStudentCenterFee({
    required String studentId,
    required String sessionId,
  }) => _change(
    'center_fee_collect',
    'تحصيل رسوم السنتر مستقلة عن دفع المدرس',
    () async {
      _require(canCollect);
      final student = _student(studentId);
      final session = _session(sessionId);
      if (student.packageMember ||
          !student.centerFeeEnabled ||
          student.isSuspended) {
        throw const CenterException('فعّل رسوم السنتر لطالب غير موقوف أولًا.');
      }
      if (session.status != SessionStatus.open) {
        throw const CenterException('اختر حصة مفتوحة لتحصيل رسوم السنتر.');
      }
      _requireFinancialOpen(sessionId);
      final attendance = _activeAttendances
          .where(
            (a) =>
                a.studentId == studentId &&
                a.sessionId == sessionId &&
                a.status != AttendanceStatus.absent,
          )
          .firstOrNull;
      if (attendance == null) {
        throw const CenterException(
          'سجل حضور الطالب أولًا ثم حصّل رسوم السنتر.',
        );
      }
      final remaining = centerFeeRemainingFor(studentId, sessionId);
      if (remaining <= 0) {
        throw const CenterException('رسوم السنتر مدفوعة بالفعل لهذه الحصة.');
      }
      final invoice = _state.centerFees
          .where(
            (f) =>
                f.studentId == studentId &&
                f.sessionId == sessionId &&
                f.originalFeeId == null,
          )
          .firstOrNull;
      _state.centerFees.add(
        CenterFeeRecord(
          id: CenterStore._uuid.v4(),
          studentId: studentId,
          sessionId: sessionId,
          amount: remaining,
          paidAmount: remaining,
          recordedAt: DateTime.now(),
          method: 'نقدي',
          staffId: currentUser!.id,
          originalFeeId: invoice?.id,
          sourceNote: 'تحصيل يدوي من زر رسوم السنتر؛ مستقل عن خصم ودفع المدرس',
        ),
      );
    },
  );

  Future<void> _saveStudentCenterOnly({
    required String studentId,
    required bool enabled,
    int amount = 1500,
  }) => _change('student_center_only', 'حفظ نظام رسوم السنتر للطالب', () async {
    _require(canEditDiscount);
    if (amount <= 0) {
      throw const CenterException('رسوم السنتر يجب أن تكون أكبر من صفر.');
    }
    final student = _student(studentId);
    if (enabled && student.packageMember) {
      throw const CenterException('ألغِ علامة باكدج قبل تفعيل رسوم السنتر.');
    }
    _replace(
      _state.students,
      student.id,
      student.copyWith(
        centerOnly: enabled,
        centerFeeAmount: amount,
        discountPercent: enabled ? 100 : student.discountPercent,
        discountNeedsReview: enabled ? false : student.discountNeedsReview,
      ),
      (e) => e.id,
    );
  });

  AttendanceRecord? _centerOnlyAttendance(String studentId, String sessionId) =>
      _activeAttendances
          .where(
            (entry) =>
                entry.studentId == studentId &&
                entry.sessionId == sessionId &&
                entry.centerFeeOnly,
          )
          .firstOrNull;

  int _centerFeeTotalDue(String studentId, String sessionId) {
    final invoices = _state.centerFees.where(
      (fee) =>
          fee.studentId == studentId &&
          fee.sessionId == sessionId &&
          fee.originalFeeId == null,
    );
    if (invoices.isNotEmpty) {
      return invoices.fold<int>(0, (sum, fee) => sum + fee.amount);
    }
    final entry = _centerOnlyAttendance(studentId, sessionId);
    if (entry != null) return entry.centerFeeAmount;
    final student = _student(studentId);
    return student.centerOnly || student.centerFeeEnabled
        ? student.centerFeeAmount
        : 0;
  }

  int _centerFeeCollected(String studentId, String sessionId) => _state
      .centerFees
      .where((fee) => fee.studentId == studentId && fee.sessionId == sessionId)
      .fold<int>(0, (sum, fee) => sum + fee.paidAmount);

  int _centerFeeDue(Student student, String sessionId) =>
      centerFeeRemainingFor(student.id, sessionId);

  bool _entryIsCenterOnly(Student student, String sessionId) =>
      !student.centerFeeEnabled &&
      (student.centerOnly ||
          _centerOnlyAttendance(student.id, sessionId) != null);

  void _collectCenterOnlyEntry(
    EntryRequest request,
    Student student,
    LessonSession session,
    AttendanceRecord? previous,
  ) {
    if (previous?.packageId != null ||
        _sessionPayment(student.id, session.id) != null) {
      throw const CenterException(
        'للحضور دفع أو استهلاك سابق؛ صحح السجل أولًا قبل تغيير نظامه.',
      );
    }
    final invoice = _state.centerFees
        .where(
          (fee) => fee.studentId == student.id && fee.sessionId == session.id,
        )
        .where((fee) => fee.originalFeeId == null)
        .firstOrNull;
    if (invoice != null) {
      if (previous == null || previous.status == AttendanceStatus.absent) {
        throw const CenterException(
          'سجل حضور الطالب أولًا قبل سداد باقي رسوم السنتر.',
        );
      }
      _settleCenterFee(request, invoice);
      return;
    }
    final sourceId = _entryMakeupSourceGroupId(request);
    if (sourceId == null &&
            !student.groupIds.contains(session.groupId) &&
            previous == null ||
        sourceId != null &&
            !eligibleMakeupSourceGroups(
              student.id,
              session.id,
            ).any((g) => g.id == sourceId)) {
      throw const CenterException(
        'اختر مجموعة الطالب الأصلية قبل تسجيل التعويض.',
      );
    }
    final due = _centerFeeDue(student, session.id);
    final paid = _validatedPaidAmount(due, request.paidAmount);
    final collectedAt = DateTime.now();
    _state.centerFees.add(
      CenterFeeRecord(
        id: CenterStore._uuid.v4(),
        studentId: student.id,
        sessionId: session.id,
        amount: due,
        paidAmount: paid,
        recordedAt: collectedAt,
        method: request.method,
        staffId: currentUser!.id,
        sourceNote: request.notes,
      ),
    );
    final entry = AttendanceRecord(
      id: CenterStore._uuid.v4(),
      studentId: student.id,
      sessionId: session.id,
      status: sourceId == null
          ? AttendanceStatus.present
          : AttendanceStatus.makeup,
      makeupSourceGroupId: sourceId,
      recordedAt: previous?.recordedAt ?? collectedAt,
      recordedAtKnown: previous?.recordedAtKnown ?? true,
      fixedDiscountPercent: 100,
      centerFeeOnly: true,
      centerFeeAmount: due,
    );
    _state.attendances.add(entry);
    if (previous != null) {
      _state.corrections.add(
        CorrectionRecord(
          id: CenterStore._uuid.v4(),
          action: previous.status == AttendanceStatus.absent
              ? CorrectionAction.absencePresent
              : CorrectionAction.entryCorrected,
          studentId: student.id,
          sessionId: session.id,
          attendanceId: previous.id,
          replacementAttendanceId: entry.id,
          reason: 'تحصيل رسوم السنتر مع إعفاء المدرس',
          staffId: currentUser!.id,
          createdAt: collectedAt,
        ),
      );
    }
  }

  void _settleCenterFee(EntryRequest request, CenterFeeRecord invoice) {
    final remaining = centerFeeRemainingFor(
      invoice.studentId,
      invoice.sessionId,
    );
    final paid = _validatedPaidAmount(remaining, request.paidAmount);
    if (paid <= 0) {
      throw const CenterException(
        'أدخل مبلغًا أكبر من صفر لسداد باقي رسوم السنتر.',
      );
    }
    _state.centerFees.add(
      CenterFeeRecord(
        id: CenterStore._uuid.v4(),
        studentId: invoice.studentId,
        sessionId: invoice.sessionId,
        amount: paid,
        paidAmount: paid,
        recordedAt: DateTime.now(),
        method: request.method,
        staffId: currentUser!.id,
        originalFeeId: invoice.id,
        sourceNote: request.notes,
      ),
    );
  }
}
