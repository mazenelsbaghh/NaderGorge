import '../domain/models.dart';
import 'center_store.dart';

/// Resolves attendance for messages while leaving the original register intact.
class WhatsAppAttendance {
  WhatsAppAttendance(CenterStore store) {
    for (final record in store.attendances) {
      if (record.status != AttendanceStatus.makeup) continue;
      final originalId = record.originalAttendanceId;
      if (originalId != null) {
        _originalIds.add(originalId);
        continue;
      }
      // A paid visit can cover the original group's copy of the same lesson.
      // A lesson number alone cannot identify that lesson across study months.
      final sourceGroupId = record.makeupSourceGroupId;
      final lessonId = store.sessionById(record.sessionId)?.preparedLessonId;
      if (sourceGroupId != null && lessonId != null) {
        _sharedLessons.add((record.studentId, sourceGroupId, lessonId));
      }
    }
  }

  final _originalIds = <String>{};
  final _sharedLessons = <(String, String, String)>{};

  AttendanceStatus statusFor(AttendanceRecord record, LessonSession session) {
    if (record.status != AttendanceStatus.absent) return record.status;
    final lessonId = session.preparedLessonId;
    if (_originalIds.contains(record.id) ||
        (lessonId != null &&
            _sharedLessons.contains((
              record.studentId,
              session.groupId,
              lessonId,
            )))) {
      return AttendanceStatus.makeup;
    }
    return AttendanceStatus.absent;
  }
}
