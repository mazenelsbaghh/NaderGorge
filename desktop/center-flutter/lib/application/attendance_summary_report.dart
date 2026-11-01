part of 'center_reports.dart';

typedef _AttendanceLesson = (String, int, int);

extension _MonthlyAttendanceReport on _ReportContext {
  CenterReportData attendanceSummaryReport() {
    final selected = {
      for (final session in sessions.values)
        if (session.status != SessionStatus.canceled &&
            _attendanceSessionMatches(session))
          session.id: session,
    };
    final lessonSessions = <_AttendanceLesson, List<LessonSession>>{};
    final lessonRecords = <_AttendanceLesson, List<AttendanceRecord>>{};
    for (final session in selected.values) {
      final key = (session.groupId, session.monthNumber, session.number);
      lessonSessions.putIfAbsent(key, () => []).add(session);
    }
    for (final record in attendance) {
      final session = selected[record.sessionId];
      if (session == null || !_studentMatches(record.studentId)) continue;
      final key = (session.groupId, session.monthNumber, session.number);
      lessonRecords.putIfAbsent(key, () => []).add(record);
    }
    final outgoing = _outgoingMakeup(selected);
    final scopes =
        {
          for (final key in {...lessonSessions.keys, ...outgoing.keys})
            (key.$1, key.$2),
        }.toList()..sort((a, b) {
          final monthOrder = a.$2.compareTo(b.$2);
          return monthOrder != 0
              ? monthOrder
              : _groupLabel(a.$1).compareTo(_groupLabel(b.$1));
        });
    final lessonNumbers = {
      for (final month in store.studyMonths)
        if (scopes.any((scope) => scope.$2 == month.number))
          for (final lesson in month.lessons) lesson.number,
      for (final key in {...lessonSessions.keys, ...outgoing.keys}) key.$3,
    }.toList()..sort();
    final recordedPresence = {
      for (final record in attendance)
        if (record.status != AttendanceStatus.absent) record.sessionId,
    };
    final counts = {
      for (final key in lessonSessions.keys)
        key: LessonAttendanceCounts(lessonRecords[key] ?? const []),
    };
    var total = 0, makeup = 0, uncertain = 0;
    final rows = <List<Object?>>[];
    for (final scope in scopes) {
      final cells = <Object?>[];
      final notes = <String>[];
      var rowTotal = 0, rowMakeup = 0, rowOutgoing = 0;
      for (final number in lessonNumbers) {
        final key = (scope.$1, scope.$2, number);
        final count = counts[key];
        final unresolved =
            count != null &&
            lessonSessions[key]!.every(
              (s) =>
                  s.status == SessionStatus.closed &&
                  !recordedPresence.contains(s.id),
            );
        cells.add(
          count == null
              ? 'غير متاح'
              : unresolved
              ? 'غير محسوم'
              : count.total,
        );
        rowTotal += count?.total ?? 0;
        rowMakeup += count?.makeup ?? 0;
        if (unresolved) {
          uncertain++;
          notes.add('حصة $number: لا يوجد حضور مثبت داخل الحصة');
        }
        final outside = outgoing[key]?.length ?? 0;
        rowOutgoing += outside;
        if (outside > 0) {
          notes.add('حصة $number: $outside تعويض موثق خارج المجموعة');
        }
      }
      total += rowTotal;
      makeup += rowMakeup;
      rows.add([
        _groupLabel(scope.$1),
        _attendanceMonthName(scope.$2),
        ...cells,
        rowTotal,
        rowMakeup,
        rowOutgoing,
        notes.isEmpty ? '—' : notes.join('؛ '),
      ]);
    }
    return CenterReportData(
      title: 'حضور الحصص بالشهر',
      columns: [
        'المجموعة',
        'الشهر',
        for (final number in lessonNumbers) 'حصة $number',
        'إجمالي الحضور المسجل',
        'منهم معوّضون داخل الحصص',
        'تعويض خارج المجموعة',
        'ملاحظات',
      ],
      rows: rows,
      summary: {
        'إجمالي الحضور المسجل': '$total',
        'منهم معوّضون داخل الحصص': '$makeup',
        'حصص غير محسومة': '$uncertain',
      },
      caption:
          'الطالب محسوب مرة واحدة داخل كل حصة حضرها، شامل الحضور اليدوي '
          'والتعويض المسجلين في البرنامج. الإجمالي مجموع حضور الحصص، وقد يحضر '
          'الطالب أكثر من حصة. التعويض محسوب في مكان الحضور الفعلي فقط؛ التعويض '
          'خارج المجموعة للتوضيح ولا يضاف للإجمالي. غير متاح تعني أن الحصة غير '
          'مسجلة ضمن الفلاتر، وغير محسوم تعني حصة مغلقة بلا حضور مثبت. '
          'الأعداد حسب البيانات الحالية، بما فيها الحصص المفتوحة، وليست تقفيلًا '
          'نهائيًا. وقت إعداد التقرير: ${_timeLabel(generatedAt)}.',
    );
  }

  bool _attendanceSessionMatches(LessonSession session) {
    final month = store.studyMonths
        .where((month) => month.id == filter.studyMonthId)
        .firstOrNull;
    return _groupMatches(session.groupId) &&
        (filter.sessionId == null || session.id == filter.sessionId) &&
        (month == null || session.monthNumber == month.number) &&
        (filter.preparedLessonId == null ||
            session.preparedLessonId == filter.preparedLessonId) &&
        (session.startsAtKnown
            ? _dateMatches(session.startsAt)
            : filter.from == null && filter.until == null);
  }

  String _attendanceMonthName(int number) =>
      store.studyMonths
          .where((month) => month.number == number)
          .map((month) => month.name)
          .firstOrNull ??
      'الشهر $number';

  Map<_AttendanceLesson, Set<String>> _outgoingMakeup(
    Map<String, LessonSession> selected,
  ) {
    final outgoing = <_AttendanceLesson, Set<String>>{};
    final month = store.studyMonths
        .where((month) => month.id == filter.studyMonthId)
        .firstOrNull;
    for (final record in attendance) {
      if (record.status != AttendanceStatus.makeup ||
          !_studentMatches(record.studentId)) {
        continue;
      }
      final target = sessions[record.sessionId]!;
      if (target.status == SessionStatus.canceled) continue;
      final original = attendanceById[record.originalAttendanceId];
      final origin = original == null ? null : sessions[original.sessionId];
      final sourceGroup = origin?.groupId ?? record.makeupSourceGroupId;
      if (sourceGroup == null ||
          sourceGroup == target.groupId ||
          !_groupMatches(sourceGroup)) {
        continue;
      }
      if (filter.preparedLessonId != null &&
          (origin?.preparedLessonId ?? target.preparedLessonId) !=
              filter.preparedLessonId) {
        continue;
      }
      final sourceMonth = origin?.monthNumber ?? target.monthNumber;
      final sourceNumber = origin?.number ?? target.number;
      if (month != null && sourceMonth != month.number) continue;
      if (filter.sessionId != null &&
          !selected.values.any(
            (s) =>
                s.groupId == sourceGroup &&
                s.monthNumber == sourceMonth &&
                s.number == sourceNumber,
          )) {
        continue;
      }
      if ((filter.from != null || filter.until != null) &&
          (!target.startsAtKnown || !_dateMatches(target.startsAt))) {
        continue;
      }
      outgoing
          .putIfAbsent((sourceGroup, sourceMonth, sourceNumber), () => {})
          .add(record.studentId);
    }
    return outgoing;
  }
}
