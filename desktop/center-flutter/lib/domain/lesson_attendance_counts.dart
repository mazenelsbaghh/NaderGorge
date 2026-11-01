import 'models.dart';

class LessonAttendanceCounts {
  LessonAttendanceCounts(Iterable<AttendanceRecord> records) {
    for (final record in records) {
      if (record.status == AttendanceStatus.absent) continue;
      _students.add(record.studentId);
      if (record.status == AttendanceStatus.makeup) {
        _makeupStudents.add(record.studentId);
      }
    }
  }

  final _students = <String>{};
  final _makeupStudents = <String>{};
  int get total => _students.length;
  int get makeup => _makeupStudents.length;
}
