part of 'center_store.dart';

extension _StandaloneExamsStore on CenterStore {
  StandaloneExamRoom _standaloneRoom(String roomId) => _find(
    _state.standaloneExamRooms,
    (room) => room.id == roomId,
    'لم يصل امتحان الساكند بعد. أعد توصيل الجهازين وافتح التطبيق عليهما.',
  );

  Future<void> _publishStandaloneExamPage(
    StandaloneExamPage page,
    String deviceId,
  ) async {
    var changed = false;
    await _change(
      'standalone_exam_download',
      'استلام كشف امتحان الساكند دون رصد',
      () async {
        _require(canAssess);
        final previous = _state.standaloneExamRooms
            .where((room) => room.id == page.id)
            .firstOrNull;
        if (previous != null && previous.deviceId != deviceId) {
          throw const CenterException(
            'مصدر الامتحان مرتبط بجهاز آخر؛ لم تتغير النتائج.',
          );
        }
        if (previous?.complete == true &&
            previous?.exportRevision == page.exportRevision) {
          final observed = {
            for (final row in previous!.attempts) row.attemptId: row,
          };
          if (previous.title != page.title ||
              previous.roomState != page.roomState ||
              previous.maxScore != page.maxScore ||
              previous.totalAttempts != page.totalAttempts ||
              !mapEquals(previous.target?.toJson(), page.target?.toJson()) ||
              page.attempts.any(
                (row) =>
                    !mapEquals(observed[row.attemptId]?.toJson(), row.toJson()),
              )) {
            throw const CenterException(
              'كشف مختلف يحمل نفس إصدار الامتحان؛ لم يُعتمد.',
            );
          }
          return;
        }
        if (page.afterAttemptId != null &&
            (previous == null ||
                previous.complete ||
                previous.nextCursor != page.afterAttemptId ||
                previous.exportRevision != page.exportRevision ||
                previous.totalAttempts != page.totalAttempts ||
                previous.roomState != page.roomState ||
                previous.maxScore != page.maxScore ||
                previous.title != page.title ||
                !mapEquals(previous.target?.toJson(), page.target?.toJson()))) {
          throw const CenterException(
            'تغيّرت دفعات الامتحان. أعد التنزيل من البداية.',
          );
        }
        final attempts = [
          if (page.afterAttemptId != null) ...previous!.attempts,
          ...page.attempts,
        ];
        final prior = {
          for (final row in previous?.attempts ?? <StandaloneExamAttempt>[])
            row.attemptId: row,
        };
        for (final row in page.attempts) {
          final old = prior[row.attemptId];
          final importedVersion = previous?.knownVersions[row.attemptId] ?? 0;
          if (row.resultVersion < importedVersion ||
              old != null &&
                  (row.resultVersion < old.resultVersion ||
                      row.resultVersion == old.resultVersion &&
                          !mapEquals(row.toJson(), old.toJson()))) {
            throw const CenterException(
              'وصل إصدار أقدم أو متعارض لمحاولة امتحان؛ لم تُعتمد الدفعة.',
            );
          }
        }
        if (attempts.length > page.totalAttempts ||
            attempts.length > 10000 ||
            attempts.map((row) => row.attemptId).toSet().length !=
                attempts.length ||
            page.nextCursor == null && attempts.length != page.totalAttempts) {
          throw const CenterException(
            'كشف الامتحان ناقص أو يحتوي محاولات مكررة.',
          );
        }
        if (_state.standaloneExamRooms.isEmpty) {
          await _writeBackup(
            p.join(
              p.dirname(databasePath),
              'backups',
              'before-standalone-exams-v14-${CenterStore._uuid.v4()}.json',
            ),
            _state.copyForBackup(),
          );
        }
        final room = StandaloneExamRoom(
          sourceId: page.sourceId,
          examId: page.examId,
          title: page.title,
          roomState: page.roomState,
          maxScore: page.maxScore,
          totalAttempts: page.totalAttempts,
          exportRevision: page.exportRevision,
          deviceId: deviceId,
          receivedAt: DateTime.now(),
          complete: page.nextCursor == null,
          attempts: attempts,
          nextCursor: page.nextCursor,
          target: page.target,
          groupId: previous?.groupId,
          sessionId: previous?.sessionId,
          activityId: previous?.activityId,
          receipts: previous?.receipts ?? const {},
          knownVersions: {
            ...?previous?.knownVersions,
            for (final row in page.attempts) row.attemptId: row.resultVersion,
          },
        );
        _replace(_state.standaloneExamRooms, room.id, room, (row) => row.id);
        changed = true;
      },
      commitWhen: () => changed,
    );
  }

  String _standaloneFingerprint(
    StandaloneExamRoom room,
    LessonSession session,
    AcademicActivity activity,
    List<Student> roster,
  ) => crypto.sha256
      .convert(
        utf8.encode(
          jsonEncode({
            'actor': currentUser?.toJson(),
            'group': groupById(session.groupId)?.toJson(),
            'cairo': isCairoGroup(session.groupId),
            'room': room.toJson(),
            'session': session.toJson(),
            'activity': activity.toJson(),
            'students': roster.map((row) => row.toJson()).toList(),
            'attendance': academicAttendanceForSession(
              session.id,
            ).map((row) => row.toJson()).toList(),
            'records': academicRecordsForSession(session.id)
                .where((row) => row.activityId == activity.id)
                .map((row) => row.toJson())
                .toList(),
          }),
        ),
      )
      .toString();

  StandaloneExamImportPlan _previewStandaloneImport(
    String roomId,
    String groupId,
    String sessionId,
    String activityId,
  ) {
    _require(canAssess && !isRemote && Zone.current[_lanDeviceKey] == null);
    final room = _standaloneRoom(roomId);
    if (!room.complete) {
      throw const CenterException('تنزيل كشف الساكند لم يكتمل بعد.');
    }
    if (room.groupId != null &&
        (room.groupId != groupId ||
            room.sessionId != sessionId ||
            room.activityId != activityId)) {
      throw const CenterException(
        'سبق رصد هذا الامتحان في وجهة أخرى؛ اختر نفس المجموعة والحصة والامتحان.',
      );
    }
    final target = room.target;
    if (target != null &&
        (target.hostId != academicCatalogHostId ||
            target.groupId != groupId ||
            target.sessionId != sessionId ||
            target.activityId != activityId)) {
      throw const CenterException(
        'هذا الامتحان محدد لوجهة محفوظة مختلفة. اختر المجموعة والحصة والامتحان المحددين له.',
      );
    }
    final (session, activity) = _integratedExamContext(
      groupId,
      sessionId,
      activityId,
    );
    final presentIds = academicAttendanceForSession(sessionId)
        .where((row) => row.status != AttendanceStatus.absent)
        .map((row) => row.studentId)
        .toSet();
    final roster = _state.students
        .where(
          (row) =>
              row.groupIds.contains(groupId) || presentIds.contains(row.id),
        )
        .toList();
    final byCode = <String, Set<Student>>{}, byPhone = <String, Set<Student>>{};
    for (final student in roster) {
      for (final code in studentIdentifiers(
        student,
      ).map(normalizeStudentIdentifier)) {
        if (code.isNotEmpty) byCode.putIfAbsent(code, () => {}).add(student);
      }
      for (final phone in [
        student.phone,
        student.guardianPhone,
      ].map(normalizeStudentPhone)) {
        if (phone.isNotEmpty) byPhone.putIfAbsent(phone, () => {}).add(student);
      }
    }
    final existing = {
      for (final row in academicRecordsForSession(sessionId))
        if (row.activityId == activityId) row.studentId: row,
    };
    final matches =
        <
          String,
          ({Student? student, Set<String> candidates, String? problem})
        >{};
    final chosenCounts = <String, int>{};
    for (final attempt in room.attempts) {
      final codes =
          byCode[normalizeStudentIdentifier(attempt.code)] ?? const <Student>{};
      final phones =
          byPhone[normalizeStudentPhone(attempt.phone)] ?? const <Student>{};
      final candidates = {...codes, ...phones}.map((row) => row.id).toSet();
      Student? student;
      String? problem;
      if (codes.length > 1 || phones.length > 1) {
        problem = 'الكود أو الهاتف يطابق أكثر من طالب؛ يحتاج مراجعة.';
      } else if (codes.length == 1 &&
          phones.length == 1 &&
          codes.single.id != phones.single.id) {
        problem = 'الكود والهاتف يخصان طالبين مختلفين؛ يحتاج مراجعة.';
      } else {
        student = codes.firstOrNull ?? phones.firstOrNull;
        if (student == null) {
          problem = 'لا توجد مطابقة مؤكدة داخل المجموعة المختارة.';
        }
      }
      matches[attempt.attemptId] = (
        student: student,
        candidates: candidates,
        problem: problem,
      );
      if (student != null) {
        chosenCounts.update(
          student.id,
          (count) => count + 1,
          ifAbsent: () => 1,
        );
      }
    }
    final rows = <StandaloneExamImportRow>[];
    final entered = <String>{}, uncertain = <String>{};
    final reasons = <String, String>{};
    for (final attempt in room.attempts) {
      final match = matches[attempt.attemptId]!;
      final student = match.student;
      var problem = match.problem;
      if (student == null) uncertain.addAll(match.candidates);
      if (student != null) {
        entered.add(student.id);
        if (chosenCounts[student.id]! > 1) {
          problem = 'أكثر من محاولة لنفس الطالب؛ راجع المحاولات قبل الرصد.';
        }
        final receipt = room.receipts[attempt.attemptId];
        if (receipt != null && receipt.studentId != student.id) {
          problem = 'سبق ربط المحاولة بطالب مختلف؛ راجع الهوية.';
        }
      }
      final current = existing[student?.id];
      var kind = StandaloneImportRowKind.skipped;
      if (problem == null && student != null) {
        problem = _standaloneResultProblem(room, attempt, activity);
        if (problem == null &&
            isCairoGroup(groupId) &&
            !student.groupIds.contains(groupId)) {
          problem = 'الطالب غير مسجل في مجموعة القاهرة المختارة.';
        }
        if (problem == null && !presentIds.contains(student.id)) {
          if (!isCairoGroup(groupId)) {
            problem = 'الطالب غير حاضر أو معوّض في الحصة المختارة.';
          } else if (student.isSuspended) {
            problem = 'الطالب موقوف؛ لا يمكن إنشاء حضور جديد له.';
          }
        }
        final receipt = room.receipts[attempt.attemptId];
        if (problem == null) {
          kind =
              receipt != null && receipt.version >= attempt.resultVersion ||
                  current?.score == attempt.score &&
                      current?.maxScore == attempt.maxScore &&
                      current?.examAbsent == false
              ? StandaloneImportRowKind.alreadyRecorded
              : StandaloneImportRowKind.ready;
        }
      }
      final message =
          problem ??
          (kind == StandaloneImportRowKind.alreadyRecorded
              ? 'مرصود من قبل؛ لن تتكرر المحاولة.'
              : 'مطابق وجاهز للرصد بالتأكيد الجماعي.');
      if (student != null && kind == StandaloneImportRowKind.skipped) {
        reasons[student.id] = message;
      }
      rows.add(
        StandaloneExamImportRow(
          attempt: attempt,
          student: student,
          current: current,
          kind: kind,
          message: message,
        ),
      );
    }
    final attendees = roster
        .where((row) => presentIds.contains(row.id))
        .toList();
    final didNotEnter = attendees
        .where(
          (row) => !entered.contains(row.id) && !uncertain.contains(row.id),
        )
        .toList();
    final attendanceIssues = [
      for (final student in attendees)
        if (uncertain.contains(student.id))
          StandaloneAttendanceIssue(
            student,
            'مطابقة امتحان متعارضة؛ لم يُحسب غائبًا عن الامتحان.',
          )
        else if (reasons[student.id] case final String reason)
          StandaloneAttendanceIssue(student, reason),
    ];
    return StandaloneExamImportPlan(
      roomId: roomId,
      groupId: groupId,
      sessionId: sessionId,
      activityId: activityId,
      fingerprint: _standaloneFingerprint(room, session, activity, roster),
      rows: rows,
      attending: attendees.length,
      examined: attendees.where((row) => entered.contains(row.id)).length,
      recorded: attendees
          .where(
            (row) =>
                existing[row.id]?.score != null &&
                !existing[row.id]!.examAbsent,
          )
          .length,
      didNotEnter: didNotEnter,
      attendanceIssues: attendanceIssues,
    );
  }

  String? _standaloneResultProblem(
    StandaloneExamRoom room,
    StandaloneExamAttempt attempt,
    AcademicActivity activity,
  ) {
    if (room.roomState != 'closed') return 'بانتظار إغلاق الامتحان.';
    if (attempt.status != 'final' || attempt.pending != 0) {
      return switch (attempt.status) {
        'pending_grading' => 'بانتظار تصحيح المقالي.',
        'cancelled' => 'المحاولة ملغاة؛ راجع الدرجة السابقة إن كانت مرصودة.',
        'awaiting_close' => 'بانتظار إغلاق الامتحان.',
        _ => 'دخل الامتحان ولم يسلّم.',
      };
    }
    if (!activity.acceptsMaxScore(attempt.maxScore)) {
      return 'النهاية لا تطابق الامتحان المختار.';
    }
    return null;
  }

  Future<StandaloneExamImportResult> _importStandaloneExamGrades(
    StandaloneExamImportPlan approved,
  ) async {
    late StandaloneExamImportResult importSummary;
    await _change(
      'standalone_exam_import',
      'تأكيد ورصد درجات الساكند المطابقة',
      () async {
        final fresh = _previewStandaloneImport(
          approved.roomId,
          approved.groupId,
          approved.sessionId,
          approved.activityId,
        );
        if (fresh.fingerprint != approved.fingerprint) {
          throw const CenterException(
            'تغيّرت البيانات أو النتائج منذ المعاينة. حدّث المعاينة قبل التأكيد.',
          );
        }
        final room = _standaloneRoom(approved.roomId);
        final receipts = Map<String, StandaloneResultReceipt>.of(room.receipts);
        for (final row in fresh.rows) {
          if (row.kind == StandaloneImportRowKind.skipped) continue;
          if (row.kind == StandaloneImportRowKind.ready) {
            final source = IntegratedExamResult(
              sourceId: room.sourceId,
              examId: room.examId,
              attemptId: row.attempt.attemptId,
              studentId: row.student!.id,
              sessionId: fresh.sessionId,
              activityId: fresh.activityId,
              version: row.attempt.resultVersion,
              score: row.attempt.score!,
              maxScore: row.attempt.maxScore,
            );
            final provenance =
                'نتيجة امتحان الساكند؛ المصدر: ${room.sourceId}؛ الامتحان: ${room.examId}؛ المحاولة: ${row.attempt.attemptId}؛ الإصدار: ${row.attempt.resultVersion}';
            _saveAcademicRecord(
              AcademicRecord(
                id: row.current?.id ?? '',
                studentId: row.student!.id,
                sessionId: fresh.sessionId,
                activityId: fresh.activityId,
                score: row.attempt.score,
                maxScore: row.attempt.maxScore.toInt(),
                notes: row.current?.notes.isNotEmpty == true
                    ? '${row.current!.notes}\n$provenance'
                    : provenance,
                updatedAt: DateTime.now(),
              ),
              integration: IntegratedExamOwnership(source),
            );
          }
          final record = _state.academics.firstWhere(
            (record) =>
                record.studentId == row.student!.id &&
                record.sessionId == fresh.sessionId &&
                record.activityId == fresh.activityId,
          );
          receipts[row.attempt.attemptId] = StandaloneResultReceipt(
            studentId: row.student!.id,
            version: row.attempt.resultVersion,
            recordId: record.id,
          );
        }
        final imported = StandaloneExamRoom(
          sourceId: room.sourceId,
          examId: room.examId,
          title: room.title,
          roomState: room.roomState,
          maxScore: room.maxScore,
          totalAttempts: room.totalAttempts,
          exportRevision: room.exportRevision,
          deviceId: room.deviceId,
          receivedAt: room.receivedAt,
          complete: room.complete,
          attempts: room.attempts,
          nextCursor: room.nextCursor,
          target: room.target,
          groupId: receipts.isEmpty ? null : fresh.groupId,
          sessionId: receipts.isEmpty ? null : fresh.sessionId,
          activityId: receipts.isEmpty ? null : fresh.activityId,
          receipts: receipts,
          knownVersions: room.knownVersions,
        );
        _replace(
          _state.standaloneExamRooms,
          room.id,
          imported,
          (row) => row.id,
        );
        importSummary = StandaloneExamImportResult(
          recorded: fresh.readyCount,
          alreadyRecorded: fresh.alreadyRecordedCount,
          skipped:
              fresh.rows.length - fresh.readyCount - fresh.alreadyRecordedCount,
          skippedReasons: fresh.skippedReasons,
        );
      },
    );
    return importSummary;
  }
}

void _validateStandaloneExamRooms(CenterState state) {
  final ids = <String>{};
  final studentIds = state.students.map((row) => row.id).toSet();
  final records = {for (final row in state.academics) row.id: row};
  for (final room in state.standaloneExamRooms) {
    if (!ids.add(room.id) ||
        room.attempts.length > 10000 ||
        room.attempts.map((row) => row.attemptId).toSet().length !=
            room.attempts.length ||
        room.complete != (room.nextCursor == null) ||
        room.complete && room.attempts.length != room.totalAttempts ||
        !room.complete &&
            (room.attempts.isEmpty ||
                room.nextCursor != room.attempts.last.attemptId) ||
        room.target != null && room.target!.maxScore != room.maxScore ||
        room.complete &&
            !room.attempts
                .map((row) => row.attemptId)
                .toSet()
                .containsAll(room.receipts.keys) ||
        room.attempts.length > room.totalAttempts ||
        room.receipts.length > 10000 ||
        room.knownVersions.length > 10000 ||
        room.knownVersions.values.any(
          (version) => version < 1 || version > 9007199254740991,
        ) ||
        room.attempts.any(
          (row) => room.knownVersions[row.attemptId] != row.resultVersion,
        ) ||
        room.complete &&
            !room.attempts
                .map((row) => row.attemptId)
                .toSet()
                .containsAll(room.knownVersions.keys) ||
        room.deviceId.isEmpty ||
        room.deviceId.length > 150 ||
        room.receipts.values.any(
          (receipt) =>
              receipt.version < 1 ||
              receipt.version > 9007199254740991 ||
              !studentIds.contains(receipt.studentId) ||
              records[receipt.recordId]?.studentId != receipt.studentId ||
              records[receipt.recordId]?.sessionId != room.sessionId ||
              records[receipt.recordId]?.activityId != room.activityId,
        ) ||
        room.receipts.isNotEmpty &&
            (!state.sessions.any(
                  (row) =>
                      row.id == room.sessionId && row.groupId == room.groupId,
                ) ||
                !state.academicActivities.any(
                  (row) => row.id == room.activityId,
                ))) {
      throw const CenterException('سجل تنزيل أو رصد امتحان الساكند غير صالح.');
    }
    StandaloneExamPage(
      sourceId: room.sourceId,
      examId: room.examId,
      title: room.title,
      roomState: room.roomState,
      maxScore: room.maxScore,
      totalAttempts: room.totalAttempts,
      exportRevision: room.exportRevision,
      attempts: const [],
    );
    if (room.attempts.any(
      (row) =>
          row.maxScore != room.maxScore ||
          row.status == 'final' && room.roomState != 'closed',
    )) {
      throw const CenterException('نتائج غير نهائية ضمن كشف الامتحان.');
    }
  }
}
