import '../data/center_state.dart';
import '../domain/models.dart';

/// Owner-reviewed historical facts, without money movements or profile edits.
class HistoricalAcademicImport {
  HistoricalAcademicImport(this.state);
  final CenterState state;
  int attendanceAdded = 0, gradesAdded = 0, preserved = 0, conflicts = 0;
  static final _unknownDate = DateTime(1970);
  late String _repairId;
  late Map<String, Student> _students;
  late Set<String> _groups, _attendancePairs, _academicPairs;
  late Map<String, LessonSession> _sessions;
  final _activities = <String, AcademicActivity>{};
  final _roster = <String, Set<String>>{};

  void apply(Map<String, dynamic> payload) {
    _repairId = payload['id'] as String;
    _students = {for (final s in state.students) s.id: s};
    _groups = state.groups.map((g) => g.id).toSet();
    _sessions = {
      for (final s in state.sessions)
        '${s.groupId}:${s.monthNumber}:${s.number}': s,
    };
    // Include reversed records: importing must not resurrect deliberate removals.
    _attendancePairs = {
      for (final a in state.attendances) '${a.studentId}:${a.sessionId}',
    };
    _academicPairs = {
      for (final a in state.academics) '${a.studentId}:${a.sessionId}',
    };
    for (final raw in payload['records'] as List) {
      _mergeRecord(Map<String, dynamic>.from(raw as Map));
    }
    for (var i = 0; i < state.sessions.length; i++) {
      final ids = _roster[state.sessions[i].id];
      if (ids != null) {
        state.sessions[i] = state.sessions[i].copyWith(
          importRoster: ids.toList(),
        );
      }
    }
  }

  void _mergeRecord(Map<String, dynamic> row) {
    final student = _students[row['studentId']];
    final month = row['month'] as int;
    final lesson = row['lesson'] as int;
    if (student == null ||
        student.barcode != row['barcode'] ||
        row['studentName'] != null && student.name != row['studentName'] ||
        !_groups.contains(row['groupId']) ||
        !(month == 1 && lesson >= 1 && lesson <= 4 ||
            month == 2 && lesson == 1)) {
      conflicts++;
      return;
    }
    final session = _sessionFor(row);
    if (session == null) {
      conflicts++;
      return;
    }
    _roster[session.id]?.add(student.id);
    switch (row['kind']) {
      case 'attendance':
        _addAttendance(student, session, row);
      case 'grade':
        _addGrade(student, session, row);
      default:
        throw const FormatException('Historical record kind');
    }
  }

  LessonSession? _sessionFor(Map<String, dynamic> row) {
    final month = row['month'] as int;
    final number = row['lesson'] as int;
    final groupId = row['groupId'] as String;
    final key = '$groupId:$month:$number';
    final existing = _sessions[key];
    if (existing != null) {
      if (existing.status == SessionStatus.canceled ||
          month == 1 && existing.kind != SessionKind.free) {
        return null;
      }
      return existing;
    }
    // Month two must reuse the group's existing class, never create a substitute.
    if (month == 2) return null;
    final definition = _monthOne();
    final lesson = definition.lessons
        .where((l) => l.number == number)
        .firstOrNull;
    if (lesson == null || lesson.kind != SessionKind.free) return null;
    final session = LessonSession(
      id: '$_repairId:session:$key',
      preparedLessonId: lesson.id,
      groupId: groupId,
      number: number,
      monthNumber: 1,
      startsAt: _unknownDate,
      startsAtKnown: false,
      kind: SessionKind.free,
      status: SessionStatus.closed,
      createdAt: _unknownDate,
    );
    state.sessions.add(session);
    _sessions[key] = session;
    _roster[session.id] = {};
    return session;
  }

  StudyMonth _monthOne() {
    final existing = state.studyMonths.where((m) => m.number == 1).firstOrNull;
    if (existing == null) {
      final month = StudyMonth(
        id: '$_repairId:month1',
        name: 'الشهر الأول',
        number: 1,
        price: 0,
        lessons: List.generate(
          4,
          (i) => PreparedLesson(
            id: '$_repairId:lesson${i + 1}',
            number: i + 1,
            name: 'الحصة ${i + 1}',
            kind: SessionKind.free,
          ),
        ),
      );
      state.studyMonths.add(month);
      return month;
    }
    if (state.sessions.any((s) => s.monthNumber == 1)) return existing;
    final updated = existing.copyWith(
      price: 0,
      lessons: existing.lessons
          .map((l) => l.copyWith(kind: SessionKind.free))
          .toList(),
    );
    state.studyMonths[state.studyMonths.indexOf(existing)] = updated;
    return updated;
  }

  void _addAttendance(
    Student student,
    LessonSession session,
    Map<String, dynamic> row,
  ) {
    final pair = '${student.id}:${session.id}';
    if (!_attendancePairs.add(pair)) {
      preserved++;
      return;
    }
    state.attendances.add(
      AttendanceRecord(
        id: '$_repairId:attendance:$pair',
        studentId: student.id,
        sessionId: session.id,
        status: AttendanceStatus.present,
        recordedAt: _unknownDate,
        recordedAtKnown: false,
        importSource: row['source'] as String,
      ),
    );
    attendanceAdded++;
  }

  void _addGrade(
    Student student,
    LessonSession session,
    Map<String, dynamic> row,
  ) {
    final score = row['score'];
    final knownMax = row['maxScoreKnown'] == true;
    final absent = row['examAbsent'] == true;
    if (absent
        ? score != null
        : score is! num ||
              !score.isFinite ||
              score < 0 ||
              knownMax && score > 10) {
      conflicts++;
      return;
    }
    final pair = '${student.id}:${session.id}';
    if (!_academicPairs.add(pair)) {
      preserved++;
      return;
    }
    final activity = _activities.putIfAbsent(session.id, () {
      final exam = AcademicActivity(
        id: '$_repairId:exam:${session.id}',
        sessionId: session.id,
        kind: AcademicActivityKind.exam,
        name:
            row['examName'] as String? ??
            'امتحان الحصة ${session.number} — السجل المستورد',
        maxScoreKnown: knownMax,
        createdAt: _unknownDate,
      );
      state.academicActivities.add(exam);
      return exam;
    });
    state.academics.add(
      AcademicRecord(
        id: '$_repairId:grade:$pair',
        studentId: student.id,
        sessionId: session.id,
        activityId: activity.id,
        score: absent ? null : score as num,
        examAbsent: absent,
        maxScoreKnown: knownMax,
        notes:
            'درجة تاريخية مستوردة؛ التاريخ غير متوفر. المصدر: ${row['source']}',
        updatedAt: _unknownDate,
      ),
    );
    gradesAdded++;
  }
}
