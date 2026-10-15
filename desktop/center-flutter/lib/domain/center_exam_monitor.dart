import 'models.dart' show CenterException;

const examMonitorStatuses = {
  'not_submitted',
  'pending_grading',
  'awaiting_close',
  'final',
  'cancelled',
};

bool _validId(String id) =>
    id.isNotEmpty &&
    id.length <= 100 &&
    !RegExp(r'[\x00-\x20\x7f:]').hasMatch(id);

class ExamMonitorAttempt {
  const ExamMonitorAttempt({
    required this.attemptId,
    required this.studentId,
    required this.status,
  });
  final String attemptId, studentId, status;
  Map<String, dynamic> toJson() => {
    'attemptId': attemptId,
    'studentId': studentId,
    'status': status,
  };
  factory ExamMonitorAttempt.fromJson(Map<String, dynamic> json) =>
      ExamMonitorAttempt(
        attemptId: json['attemptId'] as String,
        studentId: json['studentId'] as String,
        status: json['status'] as String,
      );
}

/// Complete, versioned room observation; it never records attendance or money.
class ExamMonitorSnapshot {
  ExamMonitorSnapshot({
    required this.sourceId,
    required this.examId,
    required this.groupId,
    required this.sessionId,
    required this.activityId,
    required this.version,
    required this.roomState,
    required List<ExamMonitorAttempt> attempts,
  }) : attempts = List.unmodifiable(attempts) {
    if (![sourceId, examId, groupId, sessionId, activityId].every(_validId) ||
        version < 1 ||
        version > 9007199254740991 ||
        !{'waiting', 'running', 'closed'}.contains(roomState) ||
        attempts.length > 1000 ||
        attempts.map((row) => row.attemptId).toSet().length !=
            attempts.length ||
        attempts.map((row) => row.studentId).toSet().length !=
            attempts.length ||
        attempts.any(
          (row) =>
              !_validId(row.attemptId) ||
              !_validId(row.studentId) ||
              !examMonitorStatuses.contains(row.status),
        )) {
      throw const CenterException(
        'كشف متابعة دخول الامتحان غير صالح أو أكبر من الحد المسموح.',
      );
    }
  }
  final String sourceId, examId, groupId, sessionId, activityId, roomState;
  final int version;
  final List<ExamMonitorAttempt> attempts;
  String get id => '$sourceId:$examId';
  Map<String, dynamic> toJson() => {
    'sourceId': sourceId,
    'examId': examId,
    'groupId': groupId,
    'sessionId': sessionId,
    'activityId': activityId,
    'version': version,
    'roomState': roomState,
    'attempts': attempts.map((row) => row.toJson()).toList(),
  };
  factory ExamMonitorSnapshot.fromJson(Map<String, dynamic> json) =>
      ExamMonitorSnapshot(
        sourceId: json['sourceId'] as String,
        examId: json['examId'] as String,
        groupId: json['groupId'] as String,
        sessionId: json['sessionId'] as String,
        activityId: json['activityId'] as String,
        version: json['version'] as int,
        roomState: json['roomState'] as String,
        attempts: (json['attempts'] as List)
            .map(
              (row) => ExamMonitorAttempt.fromJson(
                Map<String, dynamic>.from(row as Map),
              ),
            )
            .toList(),
      );
}

class ExamMonitorRecord {
  const ExamMonitorRecord({
    required this.snapshot,
    required this.deviceId,
    required this.staffId,
    required this.receivedAt,
    required this.identities,
    this.importRequestId,
    this.importRequestedAt,
    this.importCompletedAt,
    this.importProblems = const {},
  });
  final ExamMonitorSnapshot snapshot;
  final String deviceId, staffId;
  final DateTime receivedAt;

  /// Retain earlier attempt identities even after an explicit reset.
  final Map<String, String> identities;
  final String? importRequestId;
  final DateTime? importRequestedAt, importCompletedAt;
  final Map<String, String> importProblems;
  bool get importPending =>
      importRequestId != null && importCompletedAt == null;

  ExamMonitorRecord withImportRequest(String requestId) => ExamMonitorRecord(
    snapshot: snapshot,
    deviceId: deviceId,
    staffId: staffId,
    receivedAt: receivedAt,
    identities: identities,
    importRequestId: requestId,
    importRequestedAt: DateTime.now(),
  );

  ExamMonitorRecord completeImport(Map<String, String> problems) =>
      ExamMonitorRecord(
        snapshot: snapshot,
        deviceId: deviceId,
        staffId: staffId,
        receivedAt: receivedAt,
        identities: identities,
        importRequestId: importRequestId,
        importRequestedAt: importRequestedAt,
        importCompletedAt: DateTime.now(),
        importProblems: Map.unmodifiable(problems),
      );
  String get id => snapshot.id;
  Map<String, dynamic> toJson() => {
    ...snapshot.toJson(),
    'deviceId': deviceId,
    'staffId': staffId,
    'receivedAt': receivedAt.toIso8601String(),
    'identities': identities,
    if (importRequestId != null) 'importRequestId': importRequestId,
    if (importRequestedAt != null)
      'importRequestedAt': importRequestedAt!.toIso8601String(),
    if (importCompletedAt != null)
      'importCompletedAt': importCompletedAt!.toIso8601String(),
    if (importProblems.isNotEmpty) 'importProblems': importProblems,
  };
  factory ExamMonitorRecord.fromJson(Map<String, dynamic> json) =>
      ExamMonitorRecord(
        snapshot: ExamMonitorSnapshot.fromJson(json),
        deviceId: json['deviceId'] as String,
        staffId: json['staffId'] as String,
        receivedAt: DateTime.parse(json['receivedAt'] as String),
        identities: Map.unmodifiable(
          Map<String, String>.from(json['identities'] as Map),
        ),
        importRequestId: json['importRequestId'] as String?,
        importRequestedAt: json['importRequestedAt'] == null
            ? null
            : DateTime.parse(json['importRequestedAt'] as String),
        importCompletedAt: json['importCompletedAt'] == null
            ? null
            : DateTime.parse(json['importCompletedAt'] as String),
        importProblems: Map.unmodifiable(
          Map<String, String>.from(json['importProblems'] as Map? ?? {}),
        ),
      );
}

class IntegratedExamMonitorStatus {
  const IntegratedExamMonitorStatus({
    required this.sourceId,
    required this.examId,
    required this.groupLabel,
    required this.sessionLabel,
    required this.activityName,
    required this.lastSeenAt,
    required this.stale,
  });
  final String sourceId, examId, groupLabel, sessionLabel, activityName;
  final DateTime? lastSeenAt;
  final bool stale;
}

class IntegratedExamAttendanceAlert extends IntegratedExamMonitorStatus {
  const IntegratedExamAttendanceAlert({
    required super.sourceId,
    required super.examId,
    required super.groupLabel,
    required super.sessionLabel,
    required super.activityName,
    required super.lastSeenAt,
    required super.stale,
    required this.studentId,
    required this.code,
    required this.name,
    required this.status,
  });
  final String studentId, code, name, status;
}
