part of 'center_store.dart';

extension _CenterExamMonitoring on CenterStore {
  Future<void> _publishExamMonitor(
    ExamMonitorSnapshot snapshot,
    String deviceId,
  ) async {
    final actor = currentUser?.id;
    final authentication = _authenticationRevision;
    var changed = false;
    await _change('exam_entry_monitor', 'تحديث متابعة دخول الامتحان', () async {
      _require(
        canAssess &&
            currentUser?.id == actor &&
            authentication == _authenticationRevision,
      );
      _lanUser(actor!);
      final previous = _state.examMonitors
          .where((row) => row.id == snapshot.id)
          .firstOrNull;
      _validateExamMonitor(snapshot, deviceId, previous);
      if (previous != null && snapshot.version <= previous.snapshot.version) {
        if (snapshot.version != previous.snapshot.version ||
            jsonEncode(snapshot.toJson()) !=
                jsonEncode(previous.snapshot.toJson())) {
          throw const CenterException(
            'كشف دخول الامتحان أقدم من المحفوظ أو مختلف عنه؛ حدّث جهاز الامتحان.',
          );
        }
        return;
      }
      if (_state.examMonitors.isEmpty) {
        await _writeBackup(
          p.join(
            p.dirname(databasePath),
            'backups',
            'before-exam-monitor-v12-${CenterStore._uuid.v4()}.json',
          ),
          _state.copyForBackup(),
        );
        _require(
          currentUser?.id == actor && authentication == _authenticationRevision,
        );
      }
      final record = ExamMonitorRecord(
        snapshot: snapshot,
        deviceId: deviceId,
        staffId: actor,
        receivedAt: DateTime.now(),
        identities: Map.unmodifiable({
          ...?previous?.identities,
          for (final row in snapshot.attempts) row.attemptId: row.studentId,
        }),
        importRequestId: previous?.importRequestId,
        importRequestedAt: previous?.importRequestedAt,
        importCompletedAt: previous?.importCompletedAt,
        importProblems: previous?.importProblems ?? const {},
      );
      if (previous != null) _state.examMonitors.remove(previous);
      _state.examMonitors.add(record);
      changed = true;
    }, commitWhen: () => changed);
    _examMonitorHeartbeats[snapshot.id] = DateTime.now();
    if (!_closed) _notifyLanCommit();
  }

  void _validateExamMonitor(
    ExamMonitorSnapshot snapshot,
    String deviceId,
    ExamMonitorRecord? previous,
  ) {
    _integratedExamContext(
      snapshot.groupId,
      snapshot.sessionId,
      snapshot.activityId,
    );
    final known = <String, String>{};
    for (final record in _state.examMonitors) {
      if (record.snapshot.sourceId != snapshot.sourceId) continue;
      if (record.deviceId != deviceId) {
        throw const CenterException(
          'مصدر الامتحان مرتبط بجهاز آخر؛ لا يمكن نقل متابعته أثناء التشغيل.',
        );
      }
      for (final entry in record.identities.entries) {
        known[entry.key] = '${record.snapshot.examId}:${entry.value}';
      }
    }
    if (previous != null &&
        (previous.snapshot.groupId != snapshot.groupId ||
            previous.snapshot.sessionId != snapshot.sessionId ||
            previous.snapshot.activityId != snapshot.activityId)) {
      throw const CenterException(
        'لا يمكن تغيير حصة أو امتحان كشف دخول محفوظ.',
      );
    }
    final allowed = _integratedExamRoster(
      snapshot.groupId,
      snapshot.sessionId,
      snapshot.activityId,
    ).map((row) => row.studentId).toSet();
    final studentIds = _state.students.map((student) => student.id).toSet();
    for (final attempt in snapshot.attempts) {
      if (!studentIds.contains(attempt.studentId)) {
        throw const CenterException(
          'طالب في كشف الامتحان غير موجود في السنتر.',
        );
      }
      final identity = known[attempt.attemptId];
      if (identity != null &&
              identity != '${snapshot.examId}:${attempt.studentId}' ||
          identity == null && !allowed.contains(attempt.studentId)) {
        throw const CenterException(
          'طالب أو محاولة لا تطابق كشف الامتحان المرتبط.',
        );
      }
    }
    final incoming = {
      for (final row in snapshot.attempts) row.attemptId: row.studentId,
    };
    for (final record in _state.academics) {
      final result = record.integration?.result;
      if (result == null || result.sourceId != snapshot.sourceId) continue;
      if (result.examId == snapshot.examId &&
              (result.sessionId != snapshot.sessionId ||
                  result.activityId != snapshot.activityId) ||
          incoming.containsKey(result.attemptId) &&
              (incoming[result.attemptId] != result.studentId ||
                  snapshot.examId != result.examId)) {
        throw const CenterException(
          'كشف الدخول لا يطابق مصدر درجات الامتحان المحفوظة.',
        );
      }
    }
  }

  IntegratedExamMonitorStatus _examMonitorStatus(ExamMonitorRecord record) {
    final room = record.snapshot;
    final heartbeat = _examMonitorHeartbeats[record.id];
    final activity = _state.academicActivities
        .where((row) => row.id == room.activityId)
        .firstOrNull;
    final session = _state.sessions
        .where((row) => row.id == room.sessionId)
        .firstOrNull;
    return IntegratedExamMonitorStatus(
      sourceId: room.sourceId,
      examId: room.examId,
      groupLabel: groupLabel(room.groupId),
      sessionLabel: session == null
          ? 'حصة غير متاحة'
          : '${studyMonthForLesson(session.preparedLessonId ?? '')?.name ?? 'شهر ${session.monthNumber}'} • حصة ${session.number}',

      activityName: activity?.name ?? 'امتحان غير متاح',
      lastSeenAt: heartbeat ?? record.receivedAt,
      stale:
          heartbeat == null ||
          !remoteConnected && isRemote ||
          DateTime.now().difference(heartbeat) > const Duration(seconds: 30),
    );
  }

  List<IntegratedExamMonitorStatus> _examMonitorStatuses() {
    if (!canAssess) return const [];
    final unresolved = _examAttendanceAlerts()
        .map((row) => '${row.sourceId}:${row.examId}')
        .toSet();
    return List.unmodifiable(
      _state.examMonitors
          .where(
            (record) =>
                record.snapshot.roomState != 'closed' ||
                unresolved.contains(record.id),
          )
          .map(_examMonitorStatus),
    );
  }

  List<IntegratedExamAttendanceAlert> _examAttendanceAlerts() {
    if (!canAssess) return const [];
    final present = {
      for (final row in _activeAttendances)
        if (row.status != AttendanceStatus.absent)
          '${row.sessionId}:${row.studentId}',
    };
    final students = {for (final row in _state.students) row.id: row};
    final alerts = <IntegratedExamAttendanceAlert>[];
    for (final record in _state.examMonitors) {
      final status = _examMonitorStatus(record);
      for (final attempt in record.snapshot.attempts) {
        final student = students[attempt.studentId];
        if (student == null ||
            present.contains(
              '${record.snapshot.sessionId}:${attempt.studentId}',
            )) {
          continue;
        }
        alerts.add(
          IntegratedExamAttendanceAlert(
            sourceId: status.sourceId,
            examId: status.examId,
            groupLabel: status.groupLabel,
            sessionLabel: status.sessionLabel,
            activityName: status.activityName,
            lastSeenAt: status.lastSeenAt,
            stale: status.stale,
            studentId: student.id,
            code: student.code,
            name: student.name,
            status: attempt.status,
          ),
        );
      }
    }
    return List.unmodifiable(alerts);
  }
}

void _validateExamMonitorRecords(CenterState state) {
  if (state.examMonitors.isEmpty) return;
  final students = state.students.map((row) => row.id).toSet();
  final sessions = {for (final row in state.sessions) row.id: row};
  final activities = {for (final row in state.academicActivities) row.id: row};
  final staff = state.staff.map((row) => row.id).toSet();
  final rooms = <String>{},
      owners = <String, String>{},
      identities = <String, String>{};
  for (final record in state.examMonitors) {
    final room = record.snapshot;
    final session = sessions[room.sessionId];
    final activity = activities[room.activityId];
    final owner = owners[room.sourceId];
    if (!rooms.add(record.id) ||
        session == null ||
        session.groupId != room.groupId ||
        activity == null ||
        activity.kind != AcademicActivityKind.exam ||
        !activity.appliesToSession(session) ||
        !staff.contains(record.staffId) ||
        record.deviceId.isEmpty ||
        record.deviceId.length > 160 ||
        record.importRequestId != null &&
            (!RegExp(r'^[0-9a-f-]{36}$').hasMatch(record.importRequestId!) ||
                record.importRequestedAt == null) ||
        record.importRequestId == null &&
            (record.importRequestedAt != null ||
                record.importCompletedAt != null ||
                record.importProblems.isNotEmpty) ||
        record.importCompletedAt != null &&
            record.importCompletedAt!.isBefore(record.importRequestedAt!) ||
        record.importProblems.length > 1000 ||
        record.importProblems.entries.any(
          (entry) =>
              !students.contains(entry.key) ||
              entry.value.isEmpty ||
              entry.value.length > 400,
        ) ||
        owner != null && owner != record.deviceId ||
        record.identities.values.any((id) => !students.contains(id)) ||
        room.attempts.any(
          (row) => record.identities[row.attemptId] != row.studentId,
        )) {
      throw const CenterException(
        'سجل متابعة دخول الامتحان غير متوافق مع بيانات السنتر.',
      );
    }
    owners[room.sourceId] = record.deviceId;
    for (final entry in record.identities.entries) {
      final key = '${room.sourceId}:${entry.key}';
      if (identities.containsKey(key)) {
        throw const CenterException('محاولة امتحان مرتبطة بأكثر من قاعة.');
      }
      identities[key] = entry.value;
    }
  }
}
