part of 'center_store.dart';

extension _AcademicImportStore on CenterStore {
  Future<void> _importAcademicGrades(AcademicImportCommand command) => _change(
    'academic_import',
    'استيراد ${command.rows.length} درجة امتحان بعد المراجعة',
    () async {
      _require(canAssess);
      final session = _session(command.sessionId);
      final activity = _find<AcademicActivity>(
        _state.academicActivities,
        (exam) => exam.id == command.activityId,
        'الامتحان غير موجود.',
      );
      if (session.groupId != command.groupId ||
          session.status == SessionStatus.canceled ||
          !sessionHasStarted(session.id) ||
          activity.kind != AcademicActivityKind.exam ||
          !activity.appliesToSession(session)) {
        throw const CenterException(
          'اختر المجموعة وحصتها التي بدأت وامتحانًا تابعًا لها قبل الاستيراد.',
        );
      }
      if (!activity.maxScoreKnown ||
          command.maxScore <= 0 ||
          command.maxScore != activity.maxScore) {
        throw const CenterException(
          'تغيّرت الدرجة النهائية أو لم تُحدد؛ راجع الامتحان ومعاينة الاستيراد.',
        );
      }
      if (command.rows.isEmpty || command.rows.length > 1000) {
        throw const CenterException('اختر من طالب واحد إلى ١٠٠٠ طالب للحفظ.');
      }
      final existing = {
        for (final record in _state.academics)
          if (record.sessionId == command.sessionId &&
              record.activityId == command.activityId)
            record.studentId: record,
      };
      final selected = <String>{};
      final sourceAttempts = <(String?, String, String?)>{};
      for (final row in command.rows) {
        _validateAcademicImportRow(command, row, existing[row.studentId]);
        if (!selected.add(row.studentId)) {
          throw const CenterException('الطالب مكرر في الدرجات المختارة.');
        }
        final source = _academicImportFacts(row.source);
        final attempt = source['attemptId'];
        if (attempt != null &&
            !sourceAttempts.add((
              source['sessionId'],
              attempt,
              source['version'],
            ))) {
          throw const CenterException(
            'محاولة الامتحان بالمصدر مكررة لأكثر من طالب؛ راجع المطابقة.',
          );
        }
        _saveAcademicRecord(
          AcademicRecord(
            id: existing[row.studentId]?.id ?? '',
            studentId: row.studentId,
            sessionId: command.sessionId,
            activityId: command.activityId,
            score: row.score,
            maxScore: command.maxScore,
            notes: _academicImportNotes(existing[row.studentId]?.notes, source),
            updatedAt: DateTime.now(),
          ),
        );
      }
    },
  );

  void _validateAcademicImportRow(
    AcademicImportCommand command,
    AcademicImportRow row,
    AcademicRecord? current,
  ) {
    if (!row.score.isFinite ||
        row.score < 0 ||
        row.maxScore != command.maxScore ||
        row.score > command.maxScore) {
      throw const CenterException(
        'كل درجة يجب أن تكون بين صفر والدرجة النهائية المحددة للامتحان.',
      );
    }
    final student = _student(row.studentId);
    if (!student.groupIds.contains(command.groupId)) {
      throw const CenterException('أحد الطلاب غير مسجل في المجموعة المختارة.');
    }
    if (student.isSuspended &&
        isCairoGroup(command.groupId) &&
        !_activeAttendances.any(
          (entry) =>
              entry.studentId == student.id &&
              entry.sessionId == command.sessionId &&
              entry.status != AttendanceStatus.absent,
        )) {
      throw const CenterException(
        'أحد طلاب القاهرة موقوف؛ أعد تفعيله قبل تسجيل حضور جديد بالاستيراد.',
      );
    }
    if (!mapEquals(current?.toJson(), row.expected?.toJson())) {
      throw const CenterException(
        'تغيّر رصد أحد الطلاب منذ المعاينة؛ أعد مراجعة الملف قبل الحفظ.',
      );
    }
  }

  Map<String, String> _academicImportFacts(AcademicImportSource source) {
    final facts = <String, String>{};
    for (final field in source.toJson().entries) {
      final supplied = field.value as String;
      if (supplied.length > 200) {
        throw const CenterException('بيانات مصدر الاستيراد أطول من المسموح.');
      }
      final cleaned = supplied
          .replaceAll(
            RegExp(r'[\x00-\x1f\x7f-\x9f\u202a-\u202e\u2066-\u2069]'),
            ' ',
          )
          .replaceAll(RegExp(r'\s+'), ' ')
          .trim();
      if (cleaned.isNotEmpty && !RegExp(r'^[-–—]+$').hasMatch(cleaned)) {
        facts[field.key] = cleaned;
      }
    }
    return facts;
  }

  String _academicImportNotes(String? previous, Map<String, String> facts) {
    const labels = {
      'studentName': 'اسم الطالب بالمصدر',
      'examName': 'اسم الامتحان بالمصدر',
      'sessionId': 'معرّف الجلسة بالمصدر',
      'attemptId': 'رقم المحاولة',
      'version': 'الإصدار',
      'cairoDate': 'تاريخ القاهرة بالمصدر',
    };
    final description = facts.entries
        .map((entry) => '${labels[entry.key]}: ${entry.value}')
        .join('؛ ');
    final provenance = facts.isEmpty
        ? 'استيراد درجات من ملف؛ بيانات المصدر غير متوفرة.'
        : 'استيراد درجات من ملف؛ $description';
    return previous == null || previous.isEmpty
        ? provenance
        : '$previous\n$provenance';
  }
}
