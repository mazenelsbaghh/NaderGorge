part of 'center_store.dart';

extension _CairoAttendanceStore on CenterStore {
  Future<int> _importCairoAttendance(
    String groupId,
    String preparedLessonId,
    List<String> studentIds,
  ) async {
    final reviewed = Set<String>.of(studentIds);
    var imported = 0;
    late String destination;
    await _change(
      'cairo_attendance_import',
      'استيراد حضور القاهرة من Excel',
      () async {
        _require(canAssess);
        if (!isCairoGroup(groupId) ||
            reviewed.isEmpty ||
            reviewed.length > 1000) {
          throw const CenterException(
            'اختر مجموعة القاهرة ومن طالب إلى ١٠٠٠ طالب للحفظ.',
          );
        }
        final month = studyMonthForLesson(preparedLessonId);
        if (month == null) {
          throw const CenterException('الحصة المختارة لم تعد موجودة في الشهر.');
        }
        final lesson = month.lessons.firstWhere(
          (row) => row.id == preparedLessonId,
        );
        destination =
            '${groupLabel(groupId)} — ${month.name} — حصة ${lesson.number}';
        final existing = _preparedLessonInstance(groupId, month, lesson);
        if (existing?.status == SessionStatus.canceled) {
          throw const CenterException('الحصة ملغاة؛ اختر حصة أخرى.');
        }
        _requireFinancialOpen(existing?.id);
        final current = {
          for (final record in _activeAttendances)
            if (record.sessionId == existing?.id)
              record.studentId: record.status,
        };
        final additions = _cairoImportAdditions(groupId, reviewed, current);
        if (additions.isEmpty) return;

        final now = DateTime.now();
        final session = _cairoImportSession(
          groupId,
          month,
          lesson,
          existing,
          now,
        );
        _replace(_state.sessions, session.id, session, (row) => row.id);
        _state.attendances.addAll([
          for (final studentId in additions)
            AttendanceRecord(
              id: CenterStore._uuid.v4(),
              studentId: studentId,
              sessionId: session.id,
              status: AttendanceStatus.present,
              recordedAt: now,
              recordedAtKnown: false,
            ),
        ]);
        imported = additions.length;
      },
      commitWhen: () => imported > 0,
      auditDescription: () => 'استيراد $imported حضور من Excel — $destination',
    );
    return imported;
  }

  List<String> _cairoImportAdditions(
    String groupId,
    Set<String> reviewed,
    Map<String, AttendanceStatus> current,
  ) {
    final additions = <String>[];
    for (final id in reviewed) {
      final student = _student(id);
      if (!student.groupIds.contains(groupId) ||
          student.isSuspended ||
          current[id] == AttendanceStatus.absent) {
        throw const CenterException(
          'تغيّر تسجيل أحد الطلاب أو حالته منذ المعاينة؛ أعد مراجعة الحضور.',
        );
      }
      if (!current.containsKey(id)) additions.add(id);
    }
    return additions;
  }

  LessonSession _cairoImportSession(
    String groupId,
    StudyMonth month,
    PreparedLesson lesson,
    LessonSession? existing,
    DateTime now,
  ) =>
      existing?.copyWith(
        preparedLessonId: lesson.id,
        startedAt: existing.startedAt ?? now,
        startedBy: existing.startedBy ?? currentUser!.id,
      ) ??
      LessonSession(
        id: CenterStore._uuid.v4(),
        preparedLessonId: lesson.id,
        groupId: groupId,
        monthNumber: month.number,
        number: lesson.number,
        name: lesson.name,
        startsAt: now,
        startsAtKnown: false,
        createdAt: now,
        startedAt: now,
        startedBy: currentUser!.id,
        kind: SessionKind.free,
      );
}
