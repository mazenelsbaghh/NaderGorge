import 'models.dart';

bool _validStandaloneId(String value) =>
    RegExp(r'^[A-Za-z0-9_-]{1,100}$').hasMatch(value);
const standaloneAttemptStatuses = {
  'final',
  'pending_grading',
  'awaiting_close',
  'waiting',
  'cancelled',
};

class StandaloneExamAttempt {
  StandaloneExamAttempt({
    required this.attemptId,
    required this.code,
    required this.name,
    required this.phone,
    required this.status,
    required this.score,
    required this.maxScore,
    required this.pending,
    required this.resultVersion,
  }) {
    if (!_validStandaloneId(attemptId) ||
        code.length > 128 ||
        name.length > 400 ||
        phone.length > 80 ||
        !standaloneAttemptStatuses.contains(status) ||
        !maxScore.isFinite ||
        maxScore <= 0 ||
        maxScore > 1000000 ||
        maxScore != maxScore.truncateToDouble() ||
        pending < 0 ||
        pending > 10000 ||
        resultVersion < 1 ||
        resultVersion > 9007199254740991 ||
        (status == 'final'
            ? score == null ||
                  !score!.isFinite ||
                  score! < 0 ||
                  score! > maxScore ||
                  pending != 0
            : score != null)) {
      throw const CenterException('بيانات محاولة الامتحان غير صالحة.');
    }
  }
  final String attemptId, code, name, phone, status;
  final num? score;
  final num maxScore;
  final int pending, resultVersion;
  Map<String, dynamic> toJson() => {
    'attemptId': attemptId,
    'code': code,
    'name': name,
    'phone': phone,
    'status': status,
    'score': score,
    'maxScore': maxScore,
    'pending': pending,
    'resultVersion': resultVersion,
  };
  factory StandaloneExamAttempt.fromJson(Map<String, dynamic> json) =>
      StandaloneExamAttempt(
        attemptId: json['attemptId'] as String,
        code: json['code'] as String,
        name: json['name'] as String,
        phone: json['phone'] as String,
        status: json['status'] as String,
        score: json['score'] as num?,
        maxScore: json['maxScore'] as num,
        pending: json['pending'] as int,
        resultVersion: json['resultVersion'] as int,
      );
}

class StandaloneExamTarget {
  StandaloneExamTarget({
    required this.hostId,
    required this.groupId,
    required this.sessionId,
    required this.activityId,
    required this.maxScore,
  }) {
    if (![hostId, groupId, sessionId, activityId].every(_validStandaloneId) ||
        maxScore <= 0 ||
        maxScore > 1000000) {
      throw const CenterException('وجهة الامتحان المحفوظة غير صالحة.');
    }
  }
  final String hostId, groupId, sessionId, activityId;
  final int maxScore;
  Map<String, dynamic> toJson() => {
    'hostId': hostId,
    'groupId': groupId,
    'sessionId': sessionId,
    'activityId': activityId,
    'maxScore': maxScore,
  };
  factory StandaloneExamTarget.fromJson(Map<String, dynamic> json) {
    final maximum = json['maxScore'] as num;
    if (!maximum.isFinite || maximum != maximum.truncateToDouble()) {
      throw const CenterException('نهاية الامتحان المحفوظة غير صالحة.');
    }
    return StandaloneExamTarget(
      hostId: json['hostId'] as String,
      groupId: json['groupId'] as String,
      sessionId: json['sessionId'] as String,
      activityId: json['activityId'] as String,
      maxScore: maximum.toInt(),
    );
  }
}

class StandaloneExamPage {
  StandaloneExamPage({
    required this.sourceId,
    required this.examId,
    required this.title,
    required this.roomState,
    required this.maxScore,
    required this.totalAttempts,
    required this.exportRevision,
    required List<StandaloneExamAttempt> attempts,
    this.afterAttemptId,
    this.nextCursor,
    this.target,
  }) : attempts = List.unmodifiable(attempts) {
    if (!_validStandaloneId(sourceId) ||
        !_validStandaloneId(examId) ||
        title.isEmpty ||
        title.length > 400 ||
        !{'waiting', 'running', 'closed'}.contains(roomState) ||
        !maxScore.isFinite ||
        maxScore <= 0 ||
        maxScore > 1000000 ||
        maxScore != maxScore.truncateToDouble() ||
        totalAttempts < 0 ||
        totalAttempts > 10000 ||
        !RegExp(r'^[a-f0-9]{64}$').hasMatch(exportRevision) ||
        attempts.length > 200 ||
        attempts.length > totalAttempts ||
        target != null && target!.maxScore != maxScore ||
        attempts.map((row) => row.attemptId).toSet().length !=
            attempts.length ||
        (afterAttemptId != null && !_validStandaloneId(afterAttemptId!)) ||
        (nextCursor != null &&
            (attempts.isEmpty || nextCursor != attempts.last.attemptId)) ||
        attempts.any(
          (row) =>
              row.maxScore != maxScore ||
              row.status == 'final' && roomState != 'closed',
        )) {
      throw const CenterException(
        'دفعة نتائج الامتحان غير صالحة أو أكبر من الحد المسموح.',
      );
    }
  }
  final String sourceId, examId, title, roomState, exportRevision;
  final num maxScore;
  final int totalAttempts;
  final String? afterAttemptId, nextCursor;
  final StandaloneExamTarget? target;
  final List<StandaloneExamAttempt> attempts;
  String get id => '$sourceId:$examId';
  Map<String, dynamic> toJson() => {
    'protocol': 1,
    'sourceId': sourceId,
    'examId': examId,
    'title': title,
    'state': roomState,
    'maxScore': maxScore,
    'totalAttempts': totalAttempts,
    'exportRevision': exportRevision,
    'afterAttemptId': afterAttemptId,
    'nextCursor': nextCursor,
    'hasMore': nextCursor != null,
    if (target != null) 'target': target!.toJson(),
    'attempts': attempts.map((row) => row.toJson()).toList(),
  };
  factory StandaloneExamPage.fromJson(Map<String, dynamic> json) {
    if (json['protocol'] != 1 ||
        json['hasMore'] != (json['nextCursor'] != null)) {
      throw const CenterException('إصدار دفعة الامتحان غير متوافق.');
    }
    return StandaloneExamPage(
      sourceId: json['sourceId'] as String,
      examId: json['examId'] as String,
      title: json['title'] as String,
      roomState: json['state'] as String,
      maxScore: json['maxScore'] as num,
      totalAttempts: json['totalAttempts'] as int,
      exportRevision: json['exportRevision'] as String,
      target: json['target'] == null
          ? null
          : StandaloneExamTarget.fromJson(
              Map<String, dynamic>.from(json['target'] as Map),
            ),
      afterAttemptId: json['afterAttemptId'] as String?,
      nextCursor: json['nextCursor'] as String?,
      attempts: (json['attempts'] as List)
          .map(
            (row) => StandaloneExamAttempt.fromJson(
              Map<String, dynamic>.from(row as Map),
            ),
          )
          .toList(),
    );
  }
}

class StandaloneResultReceipt {
  const StandaloneResultReceipt({
    required this.studentId,
    required this.version,
    required this.recordId,
  });
  final String studentId, recordId;
  final int version;
  Map<String, dynamic> toJson() => {
    'studentId': studentId,
    'version': version,
    'recordId': recordId,
  };
  factory StandaloneResultReceipt.fromJson(Map<String, dynamic> json) =>
      StandaloneResultReceipt(
        studentId: json['studentId'] as String,
        version: json['version'] as int,
        recordId: json['recordId'] as String,
      );
}

/// Persisted only on the authoritative host; partial downloads cannot be imported.
class StandaloneExamRoom {
  StandaloneExamRoom({
    required this.sourceId,
    required this.examId,
    required this.title,
    required this.roomState,
    required this.maxScore,
    required this.totalAttempts,
    required this.exportRevision,
    required this.deviceId,
    required this.receivedAt,
    required this.complete,
    required List<StandaloneExamAttempt> attempts,
    this.nextCursor,
    this.target,
    this.groupId,
    this.sessionId,
    this.activityId,
    Map<String, StandaloneResultReceipt> receipts = const {},
    Map<String, int> knownVersions = const {},
  }) : attempts = List.unmodifiable(attempts),
       receipts = Map.unmodifiable(receipts),
       knownVersions = Map.unmodifiable({
         for (final row in attempts) row.attemptId: row.resultVersion,
         ...knownVersions,
       });
  final String sourceId, examId, title, roomState, exportRevision, deviceId;
  final num maxScore;
  final int totalAttempts;
  final bool complete;
  final DateTime receivedAt;
  final String? nextCursor, groupId, sessionId, activityId;
  final StandaloneExamTarget? target;
  final List<StandaloneExamAttempt> attempts;
  final Map<String, StandaloneResultReceipt> receipts;
  final Map<String, int> knownVersions;
  String get id => '$sourceId:$examId';
  Map<String, dynamic> toJson() => {
    'sourceId': sourceId,
    'examId': examId,
    'title': title,
    'roomState': roomState,
    'maxScore': maxScore,
    'totalAttempts': totalAttempts,
    'exportRevision': exportRevision,
    'deviceId': deviceId,
    'receivedAt': receivedAt.toIso8601String(),
    'complete': complete,
    'nextCursor': nextCursor,
    if (target != null) 'target': target!.toJson(),
    'groupId': groupId,
    'sessionId': sessionId,
    'activityId': activityId,
    'attempts': attempts.map((row) => row.toJson()).toList(),
    'knownVersions': knownVersions,
    'receipts': receipts.map((id, receipt) => MapEntry(id, receipt.toJson())),
  };
  factory StandaloneExamRoom.fromJson(Map<String, dynamic> json) =>
      StandaloneExamRoom(
        sourceId: json['sourceId'] as String,
        examId: json['examId'] as String,
        title: json['title'] as String,
        roomState: json['roomState'] as String,
        maxScore: json['maxScore'] as num,
        totalAttempts: json['totalAttempts'] as int,
        exportRevision: json['exportRevision'] as String,
        deviceId: json['deviceId'] as String,
        receivedAt: DateTime.parse(json['receivedAt'] as String),
        complete: json['complete'] as bool,
        target: json['target'] == null
            ? null
            : StandaloneExamTarget.fromJson(
                Map<String, dynamic>.from(json['target'] as Map),
              ),
        nextCursor: json['nextCursor'] as String?,
        groupId: json['groupId'] as String?,
        sessionId: json['sessionId'] as String?,
        activityId: json['activityId'] as String?,
        attempts: (json['attempts'] as List)
            .map(
              (row) => StandaloneExamAttempt.fromJson(
                Map<String, dynamic>.from(row as Map),
              ),
            )
            .toList(),
        knownVersions: Map<String, int>.from(
          json['knownVersions'] as Map? ?? const {},
        ),
        receipts: (json['receipts'] as Map).map(
          (id, receipt) => MapEntry(
            id as String,
            StandaloneResultReceipt.fromJson(
              Map<String, dynamic>.from(receipt as Map),
            ),
          ),
        ),
      );
}

enum StandaloneImportRowKind { ready, alreadyRecorded, skipped }

class StandaloneExamImportRow {
  const StandaloneExamImportRow({
    required this.attempt,
    required this.student,
    required this.current,
    required this.kind,
    required this.message,
  });
  final StandaloneExamAttempt attempt;
  final Student? student;
  final AcademicRecord? current;
  final StandaloneImportRowKind kind;
  final String message;
  bool get changesPrevious =>
      kind == StandaloneImportRowKind.ready && current != null;
}

class StandaloneAttendanceIssue {
  const StandaloneAttendanceIssue(this.student, this.reason);
  final Student student;
  final String reason;
}

class StandaloneExamImportPlan {
  StandaloneExamImportPlan({
    required this.roomId,
    required this.groupId,
    required this.sessionId,
    required this.activityId,
    required this.fingerprint,
    required List<StandaloneExamImportRow> rows,
    required this.attending,
    required this.examined,
    required this.recorded,
    required List<Student> didNotEnter,
    required List<StandaloneAttendanceIssue> attendanceIssues,
  }) : rows = List.unmodifiable(rows),
       didNotEnter = List.unmodifiable(didNotEnter),
       attendanceIssues = List.unmodifiable(attendanceIssues);
  final String roomId, groupId, sessionId, activityId, fingerprint;
  final List<StandaloneExamImportRow> rows;
  final int attending, examined, recorded;
  final List<Student> didNotEnter;
  final List<StandaloneAttendanceIssue> attendanceIssues;
  int get readyCount =>
      rows.where((row) => row.kind == StandaloneImportRowKind.ready).length;
  int get alreadyRecordedCount => rows
      .where((row) => row.kind == StandaloneImportRowKind.alreadyRecorded)
      .length;
  int get replacementCount => rows.where((row) => row.changesPrevious).length;
  Map<String, int> get skippedReasons {
    final reasons = <String, int>{};
    for (final row in rows.where(
      (row) => row.kind == StandaloneImportRowKind.skipped,
    )) {
      reasons.update(row.message, (count) => count + 1, ifAbsent: () => 1);
    }
    return Map.unmodifiable(reasons);
  }
}

class StandaloneExamImportResult {
  const StandaloneExamImportResult({
    required this.recorded,
    required this.alreadyRecorded,
    required this.skipped,
    required this.skippedReasons,
  });
  final int recorded, alreadyRecorded, skipped;
  final Map<String, int> skippedReasons;
}
