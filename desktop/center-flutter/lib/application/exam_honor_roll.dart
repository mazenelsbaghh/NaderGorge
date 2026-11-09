import '../domain/models.dart';
import 'center_store.dart';

typedef HonorRollRow = ({String name, String code, num score});

class HonorRollExam {
  HonorRollExam(this.id, this.name, Iterable<HonorRollRow> scores)
    : scores = List.unmodifiable(scores);

  final String id, name;
  final List<HonorRollRow> scores;

  List<HonorRollRow> qualifying(num minimum) =>
      scores.where((row) => row.score >= minimum).toList();
}

List<HonorRollExam> examsForHonorRoll(CenterStore store, String sessionId) {
  final selected = store.sessionById(sessionId);
  if (!store.canAssess ||
      selected == null ||
      selected.status == SessionStatus.canceled) {
    return const [];
  }

  final exams = {
    for (final activity in store.academicActivities)
      if (activity.kind == AcademicActivityKind.exam &&
          activity.appliesToSession(selected))
        activity.id: activity,
  };
  final sourceGroupByMakeup = _sourceGroups(store);
  final records = <String, Map<String, AcademicRecord>>{};
  for (final record in store.academics) {
    if (record.examAbsent || record.score == null) continue;
    final examId = record.activityId ?? '';
    final actual = store.sessionById(record.sessionId)!;
    final groupId =
        sourceGroupByMakeup[(record.studentId, actual.id)] ?? actual.groupId;
    if (groupId != selected.groupId ||
        !_isSameExam(exams, selected, actual, examId)) {
      continue;
    }
    final byStudent = records.putIfAbsent(examId, () => {});
    if (_preferGrade(record, byStudent[record.studentId], selected.id)) {
      byStudent[record.studentId] = record;
    }
  }

  return [
    for (final exam in exams.values)
      HonorRollExam(exam.id, exam.name, _rows(store, records[exam.id])),
    if (records.containsKey(''))
      HonorRollExam('', 'رصد سابق بدون اسم', _rows(store, records[''])),
  ];
}

Map<(String, String), String> _sourceGroups(CenterStore store) {
  final history = store.allAttendances;
  final absences = {
    for (final record in history)
      if (record.status == AttendanceStatus.absent) record.id: record.sessionId,
  };
  final groups = <(String, String), String>{};
  // Grades survive attendance reversals, so keep the recorded makeup source
  // unless an active attendance supersedes it.
  for (final record in history.followedBy(store.attendances)) {
    final pair = (record.studentId, record.sessionId);
    if (record.status == AttendanceStatus.present) {
      groups.remove(pair);
    } else if (record.status == AttendanceStatus.makeup) {
      final original = store.sessionById(absences[record.originalAttendanceId]);
      final sourceGroup = original?.groupId ?? record.makeupSourceGroupId;
      if (sourceGroup != null) groups[pair] = sourceGroup;
    }
  }
  return groups;
}

bool _isSameExam(
  Map<String, AcademicActivity> exams,
  LessonSession selected,
  LessonSession actual,
  String examId,
) {
  if (actual.status == SessionStatus.canceled) return false;
  if (examId.isNotEmpty) {
    return exams[examId]?.appliesToSession(actual) ?? false;
  }
  // Without a named activity, only the same session or shared lesson can
  // identify the old assessment across groups.
  return actual.id == selected.id ||
      (selected.preparedLessonId != null &&
          actual.preparedLessonId == selected.preparedLessonId);
}

bool _preferGrade(
  AcademicRecord grade,
  AcademicRecord? previous,
  String selectedSessionId,
) {
  if (previous == null) return true;
  final order = grade.updatedAt.compareTo(previous.updatedAt);
  if (order != 0) return order > 0;
  return grade.sessionId == selectedSessionId ||
      (previous.sessionId != selectedSessionId &&
          grade.id.compareTo(previous.id) > 0);
}

List<HonorRollRow> _rows(
  CenterStore store,
  Map<String, AcademicRecord>? records,
) {
  final rows = [
    for (final record in records?.values ?? const <AcademicRecord>[])
      if (store.studentById(record.studentId) case final Student student)
        (name: student.name, code: student.code, score: record.score!),
  ];
  return rows..sort((a, b) {
    final score = b.score.compareTo(a.score);
    return score == 0 ? a.code.compareTo(b.code) : score;
  });
}
