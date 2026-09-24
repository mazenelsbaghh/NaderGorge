import 'dart:convert';

typedef Json = Map<String, dynamic>;

class LinkedStudent {
  final String studentId, name, token;
  const LinkedStudent(this.studentId, this.name, this.token);
  factory LinkedStudent.fromJson(Json json) {
    for (final key in ['studentId', 'name', 'token']) {
      if (json[key] is! String || (json[key] as String).isEmpty) {
        throw const FormatException('Invalid saved student');
      }
    }
    return LinkedStudent(json['studentId'], json['name'], json['token']);
  }
  Json toJson() => {'studentId': studentId, 'name': name, 'token': token};
  factory LinkedStudent.verified(Json json) {
    final token = json['token'];
    if (token is! String || token.split('.').length != 3) {
      throw const FormatException('Invalid student token');
    }
    final claims = jsonDecode(
      utf8.decode(base64Url.decode(base64Url.normalize(token.split('.')[1]))),
    );
    if (claims is! Json) throw const FormatException('Invalid token claims');
    return LinkedStudent.fromJson({
      'studentId': json['studentId'] ?? claims['StudentId'],
      'name': json['studentName'],
      'token': token,
    });
  }
}

// The API includes several kinds of academic rows. This read-only projection
// preserves all review fields while centralizing nullable/legacy payload rules.
class AcademicRow {
  final Json json;
  const AcademicRow(this.json);
  String text(String key) => json[key]?.toString() ?? '';
  double number(String key) => (json[key] as num?)?.toDouble() ?? 0;
  int count(String key) => number(key).toInt();
  bool flag(String key) => json[key] == true;
  List<AcademicRow> rows(String key) => ((json[key] as List?) ?? [])
      .map((row) => AcademicRow(Map<String, dynamic>.from(row as Map)))
      .toList();
  String get scope => [
    text('packageName'),
    text('termTitle'),
  ].where((s) => s.isNotEmpty).join(' • ');
  bool get hasExamGrade => const ['Passed', 'Failed'].contains(text('status'));
  bool get hasHomeworkGrade => text('submissionState') == 'Graded';
  String get examStatus => switch (text('status')) {
    'Passed' => 'ناجح',
    'Failed' => 'لم يجتز',
    'NotStarted' => 'لم يبدأ',
    'ManualReconciliationRequired' => 'قيد مراجعة النتيجة',
    _ => 'النتيجة غير متاحة',
  };
  String get homeworkStatus => hasHomeworkGrade
      ? 'تم التصحيح'
      : flag('isSubmitted')
      ? 'بانتظار التصحيح'
      : 'لم يُسلّم';
  int get completedVideos =>
      (json['completedVideos'] as num?)?.toInt() ?? count('watchedVideos');
  int? get startedVideos => (json['startedVideos'] as num?)?.toInt();
  double get videoProgress => count('totalVideos') == 0
      ? 0
      : (completedVideos / count('totalVideos')).clamp(0, 1);
  String get watchStatus => flag('isCompleted')
      ? 'مكتملة'
      : (startedVideos ?? completedVideos) > 0
      ? 'قيد المشاهدة'
      : 'لم يبدأ';
}

class StudentDetails extends AcademicRow {
  const StudentDetails(super.json);
  AcademicRow get attendance => AcademicRow(this.json['attendance'] as Json);
  AcademicRow get balance => AcademicRow((this.json['balance'] as Json?) ?? {});
  List<AcademicRow> get lessons => rows('watchLessons');
  List<AcademicRow> get exams => rows('exams');
  List<AcademicRow> get homeworks => rows('homeworks');
  List<AcademicRow> get warnings => rows('warnings');
  List<AcademicRow> get courses => rows('courses');
  double? get watchProgress =>
      (attendance.json['watchProgressPercentage'] as num?)?.toDouble();
  double get progress => ((watchProgress ?? 0) / 100).clamp(0, 1);
}
