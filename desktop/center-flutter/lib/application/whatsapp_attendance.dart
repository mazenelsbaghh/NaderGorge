import '../domain/models.dart';
import 'center_store.dart';

/// Resolves active makeup attendance without changing the original register.
class AttendanceMakeups {
  AttendanceMakeups(CenterStore store, Iterable<AttendanceRecord> attendance) {
    for (final record in attendance) {
      if (record.status != AttendanceStatus.makeup) continue;
      final target = store.sessionById(record.sessionId)!;
      final originalId = record.originalAttendanceId;
      if (originalId != null) {
        _byOriginalId[originalId] = target;
      } else if (record.makeupSourceGroupId case final String sourceGroupId) {
        _bySourceLesson[_lessonKey(record.studentId, sourceGroupId, target)] =
            target;
      }
    }
  }

  final _byOriginalId = <String, LessonSession>{};
  final _bySourceLesson = <(String, String, int, int), LessonSession>{};

  // A group's month and lesson number identify one session, including
  // legacy sessions created without a prepared lesson ID.
  static (String, String, int, int) _lessonKey(
    String studentId,
    String groupId,
    LessonSession session,
  ) => (studentId, groupId, session.monthNumber, session.number);

  AttendanceStatus? statusFor(
    String studentId,
    LessonSession session,
    AttendanceRecord? record,
  ) => coveringSessionFor(studentId, session, record) == null
      ? record?.status
      : AttendanceStatus.makeup;

  LessonSession? coveringSessionFor(
    String studentId,
    LessonSession session,
    AttendanceRecord? record,
  ) {
    if (record != null && record.status != AttendanceStatus.absent) return null;
    return _byOriginalId[record?.id] ??
        _bySourceLesson[_lessonKey(studentId, session.groupId, session)];
  }
}
