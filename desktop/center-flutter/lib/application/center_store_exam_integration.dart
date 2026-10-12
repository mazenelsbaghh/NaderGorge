part of 'center_store.dart';

extension _CenterExamIntegration on CenterStore {
  (LessonSession, AcademicActivity) _integratedExamContext(
    String groupId,
    String sessionId,
    String activityId,
  ) {
    _require(canAssess);
    final session = _session(sessionId);
    final activity = _find<AcademicActivity>(
      _state.academicActivities,
      (entry) => entry.id == activityId,
      'الامتحان المرتبط غير موجود.',
    );
    if (session.groupId != groupId ||
        session.status == SessionStatus.canceled ||
        !sessionHasStarted(session.id) ||
        activity.kind != AcademicActivityKind.exam ||
        !activity.appliesToSession(session) ||
        !activity.maxScoreKnown) {
      throw const CenterException(
        'اختر المجموعة وحصة بدأت وامتحانًا محدد الدرجة النهائية للربط.',
      );
    }
    return (session, activity);
  }

  List<IntegratedExamStudent> _integratedExamRoster(
    String groupId,
    String sessionId,
    String activityId,
  ) {
    _integratedExamContext(groupId, sessionId, activityId);
    final present = {
      for (final attendance in _activeAttendances)
        if (attendance.sessionId == sessionId &&
            attendance.status != AttendanceStatus.absent)
          attendance.studentId,
    };
    return List.unmodifiable([
      for (final student in _state.students)
        if (present.contains(student.id) ||
            !student.isSuspended && student.groupIds.contains(groupId))
          IntegratedExamStudent(
            studentId: student.id,
            code: student.code,
            name: student.name,
            phone: student.phone,
            present: present.contains(student.id),
          ),
    ]);
  }

  IntegratedExamProgress _integratedExamProgress(
    String groupId,
    String sessionId,
    String activityId,
  ) {
    final attendees = _integratedExamRoster(
      groupId,
      sessionId,
      activityId,
    ).where((student) => student.present).toList();
    final scored = {
      for (final record in _state.academics)
        if (record.sessionId == sessionId &&
            record.activityId == activityId &&
            record.score != null &&
            !record.examAbsent)
          record.studentId,
    };
    final missing = attendees
        .where((student) => !scored.contains(student.studentId))
        .toList();
    return IntegratedExamProgress(
      attending: attendees.length,
      recorded: attendees.length - missing.length,
      missing: List.unmodifiable(missing),
    );
  }

  Future<IntegratedExamReceipt> _recordIntegratedExamResult(
    IntegratedExamResult examResult,
  ) async {
    late IntegratedExamReceipt receipt;
    await _change(
      'integrated_exam_result',
      'رصد نتيجة امتحان معتمدة',
      () async {
        receipt = _applyIntegratedExamResult(examResult);
      },
    );
    // The caller must not acknowledge the engine until _change has committed.
    return receipt;
  }

  IntegratedExamReceipt _applyIntegratedExamResult(
    IntegratedExamResult examResult,
  ) {
    _validateIntegratedContext(examResult);
    final current = _integratedAcademicRecord(examResult);
    _validateIntegratedOwnership(examResult, current);
    final replay = current?.integration?.result.version == examResult.version;
    if (!replay) {
      _saveAcademicRecord(
        AcademicRecord(
          id: current?.id ?? '',
          studentId: examResult.studentId,
          sessionId: examResult.sessionId,
          activityId: examResult.activityId,
          score: examResult.score,
          maxScore: examResult.maxScore.toInt(),
          notes: current?.notes ?? '',
          updatedAt: DateTime.now(),
        ),
        integration: IntegratedExamOwnership(examResult),
      );
    }
    return IntegratedExamReceipt(
      result: examResult,
      recordId: _integratedAcademicRecord(examResult)!.id,
      alreadyRecorded: replay,
    );
  }

  AcademicRecord? _integratedAcademicRecord(IntegratedExamResult examResult) {
    final matches = _state.academics
        .where(
          (record) =>
              record.studentId == examResult.studentId &&
              record.sessionId == examResult.sessionId &&
              record.activityId == examResult.activityId,
        )
        .toList();
    if (matches.length > 1) {
      throw const CenterException('يوجد أكثر من سجل للطالب؛ راجع الرصد أولًا.');
    }
    return matches.firstOrNull;
  }

  void _validateIntegratedContext(IntegratedExamResult examResult) {
    _require(canAssess);
    if (!examResult.isValid) {
      throw const CenterException('نتيجة الامتحان أو هوية مصدرها غير صالحة.');
    }
    final session = _session(examResult.sessionId);
    final (_, activity) = _integratedExamContext(
      session.groupId,
      session.id,
      examResult.activityId,
    );
    if (activity.maxScore != examResult.maxScore) {
      throw const CenterException('الدرجة النهائية لا تطابق الامتحان المرتبط.');
    }
    _validateIntegratedPresence(_student(examResult.studentId), session);
  }

  void _validateIntegratedPresence(Student student, LessonSession session) {
    final present = _activeAttendances.any(
      (entry) =>
          entry.studentId == student.id &&
          entry.sessionId == session.id &&
          entry.status != AttendanceStatus.absent,
    );
    if (isCairoGroup(session.groupId)) {
      if (student.isSuspended && !present) {
        throw const CenterException(
          'الطالب موقوف؛ أعد تفعيله قبل تحضير القاهرة.',
        );
      }
    } else if (!present) {
      throw const CenterException(
        'سجّل حضور الطالب أو تعويضه قبل رصد النتيجة.',
      );
    }
  }

  void _validateIntegratedOwnership(
    IntegratedExamResult result,
    AcademicRecord? current,
  ) {
    _validateIntegratedBinding(result);
    final ownership = current?.integration;
    if (ownership == null) {
      if (current?.score != null || current?.examAbsent == true) {
        throw const CenterException('درجة الطالب مرصودة يدويًا؛ راجع التعارض.');
      }
      return;
    }
    final saved = ownership.result;
    if (ownership.manuallyEdited ||
        !saved.sameDestination(result) ||
        current!.score != saved.score ||
        current.examAbsent ||
        current.maxScore != saved.maxScore) {
      throw const CenterException(
        'تغيّرت درجة الطالب أو مصدرها؛ راجع التعارض.',
      );
    }
    if (result.version < saved.version ||
        result.version == saved.version && result.score != saved.score) {
      throw const CenterException('إصدار النتيجة قديم أو مستخدم لدرجة مختلفة.');
    }
  }

  void _validateIntegratedBinding(IntegratedExamResult result) {
    for (final record in _state.academics) {
      final saved = record.integration?.result;
      if (saved == null || saved.sourceId != result.sourceId) continue;
      if (saved.examId == result.examId &&
          (saved.sessionId != result.sessionId ||
              saved.activityId != result.activityId ||
              saved.maxScore != result.maxScore)) {
        throw const CenterException(
          'امتحان المصدر مرتبط بحصة أو امتحان مختلف.',
        );
      }
      if (saved.attemptId == result.attemptId &&
          !saved.sameDestination(result)) {
        throw const CenterException('المحاولة مرتبطة بطالب أو امتحان آخر.');
      }
    }
  }
}
