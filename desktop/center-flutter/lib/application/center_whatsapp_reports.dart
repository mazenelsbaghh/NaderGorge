import '../domain/models.dart';
import '../shared/formatters.dart';
import 'center_reports.dart';
import 'center_store.dart';

/// A single class snapshot, with each named assessment kept independent.
class CenterWhatsAppSelection {
  const CenterWhatsAppSelection({
    required this.groupId,
    required this.sessionId,
    this.examId,
    this.homeworkId,
  });
  final String groupId, sessionId;
  final String? examId, homeworkId;
}

abstract final class CenterWhatsAppReports {
  static CenterReportData build(
    CenterStore store,
    CenterWhatsAppSelection selection,
  ) {
    if (store.currentUser == null) {
      throw const CenterException('سجل الدخول لعرض رسائل الطلاب.');
    }
    final group = store.groupById(selection.groupId);
    final session = store.sessionById(selection.sessionId);
    if (group == null ||
        session == null ||
        session.groupId != group.id ||
        session.status == SessionStatus.canceled) {
      throw const CenterException('الحصة لم تعد تطابق المجموعة. أعد اختيارها.');
    }
    final exam = _activity(
      store,
      session,
      selection.examId,
      AcademicActivityKind.exam,
    );
    final homework = _activity(
      store,
      session,
      selection.homeworkId,
      AcademicActivityKind.homework,
    );
    final attendance = {
      for (final entry in store.academicAttendanceForSession(session.id))
        entry.studentId: entry,
    };
    final academics = store.academicRecordsForSession(session.id);
    final historicalIds = {
      ...attendance.keys,
      ...academics.map((entry) => entry.studentId),
    };
    final roster =
        store.students
            .where(
              (student) =>
                  historicalIds.contains(student.id) ||
                  CenterReports.wasEnrolledForSession(store, student, session),
            )
            .toList()
          ..sort((first, second) => first.name.compareTo(second.name));
    final exams = {
      for (final record in academics.where(
        (record) => exam != null && record.activityId == exam.id,
      ))
        record.studentId: record,
    };
    final homeworkRecords = {
      for (final record in academics.where(
        (record) => homework != null && record.activityId == homework.id,
      ))
        record.studentId: record,
    };
    final center =
        store.catalogs
            .where((entry) => entry.id == group.centerId)
            .firstOrNull
            ?.name ??
        'غير معروف';
    final month = session.preparedLessonId == null
        ? 'الشهر ${session.monthNumber}'
        : store.studyMonthForLesson(session.preparedLessonId!)?.name ??
              'الشهر ${session.monthNumber}';
    final messages = roster.map((student) {
      final examRecord = exams[student.id];
      final presence = attendance[student.id]?.status;
      final localTime = session.startsAt.toLocal();
      return <String, dynamic>{
        'id':
            '${student.id}:${session.id}:${exam?.id ?? "none"}:${homework?.id ?? "none"}',
        'studentId': student.id,
        'sessionId': session.id,
        if (exam != null) 'activityId': exam.id,
        'name': student.name,
        'code': student.code,
        'phone': student.phone,
        'guardianPhone': student.guardianPhone,
        'group': group.name,
        'center': center,
        'lesson': session.name.isEmpty
            ? 'الحصة ${session.number}'
            : session.name,
        'month': month,
        'time': session.startsAtKnown
            ? '${localTime.hour.toString().padLeft(2, '0')}:${localTime.minute.toString().padLeft(2, '0')}'
            : 'غير معروف',
        'exam': exam?.name ?? 'لم يحدد امتحان',
        'score': examRecord?.examAbsent == true ? null : examRecord?.score,
        'maximum': examRecord?.maxScoreKnown == true
            ? examRecord!.maxScore
            : null,
        'homework': homework == null
            ? 'لم يحدد واجب'
            : homeworkLabel(
                homeworkRecords[student.id]?.homework ??
                    HomeworkStatus.notReviewed,
              ),
        'attendance': presence == null
            ? 'لم يُسجل حضور'
            : attendanceLabel(presence),
      };
    }).toList();
    return CenterReportData(
      title: '${group.name} · ${sessionLabel(session)}',
      columns: const [
        'الكود',
        'الطالب',
        'الحضور',
        'الواجب',
        'الدرجة',
        'رقم الطالب',
        'رقم ولي الأمر',
      ],
      rows: messages.map(displayRow).toList(),
      whatsAppRecords: messages,
      summary: {
        'الطلاب': '${messages.length}',
        'حاضر ومعوّض':
            '${attendance.values.where((entry) => entry.status != AttendanceStatus.absent).length}',
        'درجات مرصودة':
            '${messages.where((row) => row['score'] != null).length}',
      },
      caption:
          'حالة الحضور والواجب كما هي مسجلة. عدم تسجيل الحضور لا يعني الغياب، والدرجة الفارغة لا تعني صفرًا.',
    );
  }

  static List<Object?> displayRow(Map<String, dynamic> message) => [
    message['code'],
    message['name'],
    message['attendance'],
    message['homework'],
    message['score'] == null
        ? 'لم تُرصد'
        : '${message['score']} من ${message['maximum'] ?? 'غير معروفة'}',
    message['phone'],
    message['guardianPhone'],
  ];

  static AcademicActivity? _activity(
    CenterStore store,
    LessonSession session,
    String? id,
    AcademicActivityKind kind,
  ) {
    if (id == null) return null;
    final selected = store.academicActivities
        .where((activity) => activity.id == id)
        .firstOrNull;
    if (selected == null ||
        selected.kind != kind ||
        !selected.appliesToSession(session)) {
      throw const CenterException(
        'الامتحان أو الواجب لم يعد يطابق الحصة. أعد الاختيار.',
      );
    }
    return selected;
  }
}
