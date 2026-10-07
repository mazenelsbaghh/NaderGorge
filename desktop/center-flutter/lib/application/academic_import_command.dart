import '../domain/models.dart';

/// A reviewed batch targets one actual group session and one named exam.
class AcademicImportCommand {
  AcademicImportCommand({
    required this.groupId,
    required this.sessionId,
    required this.activityId,
    required this.maxScore,
    required List<AcademicImportRow> rows,
  }) : rows = List.unmodifiable(rows);

  final String groupId, sessionId, activityId;
  final int maxScore;
  final List<AcademicImportRow> rows;

  Map<String, dynamic> toJson() => {
    'groupId': groupId,
    'sessionId': sessionId,
    'activityId': activityId,
    'maxScore': maxScore,
    'rows': rows.map((row) => row.toJson()).toList(),
  };

  factory AcademicImportCommand.fromJson(
    Map<String, dynamic> json,
  ) => AcademicImportCommand(
    groupId: json['groupId'] as String,
    sessionId: json['sessionId'] as String,
    activityId: json['activityId'] as String,
    maxScore: json['maxScore'] as int,
    rows: (json['rows'] as List)
        .map(
          (row) =>
              AcademicImportRow.fromJson(Map<String, dynamic>.from(row as Map)),
        )
        .toList(),
  );
}

class AcademicImportRow {
  const AcademicImportRow({
    required this.studentId,
    required this.score,
    required this.maxScore,
    required this.expected,
    this.source = const AcademicImportSource(),
  });

  final String studentId;
  final num score;
  final int maxScore;
  // Null means the preview found no record, not permission to overwrite one.
  final AcademicRecord? expected;
  final AcademicImportSource source;

  Map<String, dynamic> toJson() => {
    'studentId': studentId,
    'score': score,
    'maxScore': maxScore,
    'expected': expected?.toJson(),
    'source': source.toJson(),
  };

  factory AcademicImportRow.fromJson(Map<String, dynamic> json) {
    if (!json.containsKey('expected')) {
      throw const FormatException('Missing academic preview snapshot');
    }
    return AcademicImportRow(
      studentId: json['studentId'] as String,
      score: json['score'] as num,
      maxScore: json['maxScore'] as int,
      expected: json['expected'] == null
          ? null
          : AcademicRecord.fromJson(
              Map<String, dynamic>.from(json['expected'] as Map),
            ),
      source: AcademicImportSource.fromJson(
        Map<String, dynamic>.from(json['source'] as Map),
      ),
    );
  }
}

/// Source facts are metadata only; unknown dates and identifiers stay absent.
class AcademicImportSource {
  const AcademicImportSource({
    this.studentName,
    this.examName,
    this.sessionId,
    this.attemptId,
    this.version,
    this.cairoDate,
  });

  final String? studentName, examName, sessionId, attemptId, version, cairoDate;

  Map<String, dynamic> toJson() => {
    'studentName': ?studentName,
    'examName': ?examName,
    'sessionId': ?sessionId,
    'attemptId': ?attemptId,
    'version': ?version,
    'cairoDate': ?cairoDate,
  };

  factory AcademicImportSource.fromJson(Map<String, dynamic> json) =>
      AcademicImportSource(
        studentName: json['studentName'] as String?,
        examName: json['examName'] as String?,
        sessionId: json['sessionId'] as String?,
        attemptId: json['attemptId'] as String?,
        version: json['version'] as String?,
        cairoDate: json['cairoDate'] as String?,
      );
}
