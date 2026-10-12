/// A finalized result addressed to one authoritative student and named activity.
class IntegratedExamResult {
  const IntegratedExamResult({
    required this.sourceId,
    required this.examId,
    required this.attemptId,
    required this.studentId,
    required this.sessionId,
    required this.activityId,
    required this.version,
    required this.score,
    required this.maxScore,
    this.status = 'final',
  });

  final String sourceId, examId, attemptId, studentId, sessionId, activityId;
  final int version;
  final num score, maxScore;
  final String status;

  bool get isValid =>
      [sourceId, examId, attemptId, studentId, sessionId, activityId].every(
        (value) =>
            value.isNotEmpty &&
            value.length <= 200 &&
            !RegExp(r'[\x00-\x20\x7f]').hasMatch(value),
      ) &&
      status == 'final' &&
      version > 0 &&
      score.isFinite &&
      maxScore.isFinite &&
      maxScore > 0 &&
      maxScore == maxScore.truncateToDouble() &&
      score >= 0 &&
      score <= maxScore;

  Map<String, dynamic> toJson() => {
    'sourceId': sourceId,
    'examId': examId,
    'attemptId': attemptId,
    'studentId': studentId,
    'sessionId': sessionId,
    'activityId': activityId,
    'version': version,
    'score': score,
    'maxScore': maxScore,
    'status': status,
  };

  factory IntegratedExamResult.fromJson(Map<String, dynamic> value) =>
      IntegratedExamResult(
        sourceId: value['sourceId'] as String,
        examId: value['examId'] as String,
        attemptId: value['attemptId'] as String,
        studentId: value['studentId'] as String,
        sessionId: value['sessionId'] as String,
        activityId: value['activityId'] as String,
        version: value['version'] as int,
        score: value['score'] as num,
        maxScore: value['maxScore'] as num,
        status: value['status'] as String? ?? 'final',
      );

  bool sameDestination(IntegratedExamResult other) =>
      sourceId == other.sourceId &&
      examId == other.examId &&
      attemptId == other.attemptId &&
      studentId == other.studentId &&
      sessionId == other.sessionId &&
      activityId == other.activityId &&
      maxScore == other.maxScore;
}

/// Retained even after a manual edit, preventing an old attempt being reassigned.
class IntegratedExamOwnership {
  const IntegratedExamOwnership(this.result, {this.manuallyEdited = false});
  final IntegratedExamResult result;
  final bool manuallyEdited;

  Map<String, dynamic> toJson() => {
    ...result.toJson(),
    'manuallyEdited': manuallyEdited,
  };

  factory IntegratedExamOwnership.fromJson(Map<String, dynamic> value) =>
      IntegratedExamOwnership(
        IntegratedExamResult.fromJson(value),
        manuallyEdited: value['manuallyEdited'] as bool? ?? false,
      );
}

class IntegratedExamReceipt {
  const IntegratedExamReceipt({
    required this.result,
    required this.recordId,
    required this.alreadyRecorded,
  });
  final IntegratedExamResult result;
  final String recordId;
  final bool alreadyRecorded;

  Map<String, dynamic> toJson() => {
    ...result.toJson(),
    'recordId': recordId,
    'alreadyRecorded': alreadyRecorded,
  };

  factory IntegratedExamReceipt.fromJson(Map<String, dynamic> value) =>
      IntegratedExamReceipt(
        result: IntegratedExamResult.fromJson(value),
        recordId: value['recordId'] as String,
        alreadyRecorded: value['alreadyRecorded'] as bool,
      );
}

class IntegratedExamStudent {
  const IntegratedExamStudent({
    required this.studentId,
    required this.code,
    required this.name,
    required this.phone,
    required this.present,
  });
  final String studentId, code, name, phone;
  final bool present;
  Map<String, dynamic> toJson() => {
    'studentId': studentId,
    'code': code,
    'name': name,
    'phone': phone,
    'present': present,
  };
}

class IntegratedExamProgress {
  const IntegratedExamProgress({
    required this.attending,
    required this.recorded,
    required this.missing,
  });
  final int attending, recorded;
  final List<IntegratedExamStudent> missing;
}
