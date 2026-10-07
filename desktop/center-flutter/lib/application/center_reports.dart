import 'dart:convert';
import 'dart:io';

import 'package:path/path.dart' as p;
import 'package:uuid/uuid.dart';

import '../domain/models.dart';
import 'center_store.dart';

part 'student_card_reports.dart';

enum CenterReportKind {
  students,
  groups,
  sessions,
  attendance,
  packages,
  payments,
  exams,
  homework,
  reviews,
  closings,
  cards,
  debts,
}

enum ReviewReportMode { codes, amounts, all }

enum PaymentReportMode { all, collections, refunds }

enum ClosingReportMode { summary, categories, live }

enum DebtReportStatus { outstanding, settled, all }

enum ExamReportStatus { all, recorded, absent, unrecorded, notTaken }

String _normalizeAcademicNumber(String text) {
  const arabic = '٠١٢٣٤٥٦٧٨٩';
  const persian = '۰۱۲۳۴۵۶۷۸۹';
  var normalized = text.trim();
  for (var digit = 0; digit < 10; digit++) {
    normalized = normalized
        .replaceAll(arabic[digit], '$digit')
        .replaceAll(persian[digit], '$digit');
  }
  return normalized;
}

int? parseAcademicInteger(String text) =>
    int.tryParse(_normalizeAcademicNumber(text));

num? parseAcademicScore(String text) {
  final normalized = _normalizeAcademicNumber(
    text,
  ).replaceAll('٫', '.').replaceAll(',', '.');
  if (!RegExp(r'^[+-]?(?:\d+(?:\.\d+)?|\.\d+)$').hasMatch(normalized)) {
    return null;
  }
  final score = num.tryParse(normalized);
  return score != null && score.isFinite ? score : null;
}

class CenterReportFilter {
  const CenterReportFilter({
    this.studentId,
    this.groupId,
    this.groupIds = const {},
    this.studyMonthId,
    this.preparedLessonId,
    this.sessionId,
    this.subjectId,
    this.centerId,
    this.gradeId,
    this.from,
    this.until,
    this.unassignedPaymentsOnly = false,
    this.reviewMode = ReviewReportMode.codes,
    this.paymentMode = PaymentReportMode.all,
    this.attendanceCorrections = false,
    this.closingMode = ClosingReportMode.summary,
    this.examStatus = ExamReportStatus.all,
    this.exactScore,
    this.minScore,
    this.maxScore,
    this.homeworkStatus,
    this.activityId,
    this.debtStatus = DebtReportStatus.outstanding,
    this.debtKind,
  });
  final String? studentId;
  final String? groupId;
  final Set<String> groupIds;
  final String? studyMonthId, preparedLessonId;
  final String? sessionId;
  final String? subjectId;
  final String? centerId;
  final String? gradeId;
  final DateTime? from;
  final DateTime? until;
  final bool unassignedPaymentsOnly;
  final ReviewReportMode reviewMode;
  final PaymentReportMode paymentMode;
  final bool attendanceCorrections;
  final ClosingReportMode closingMode;
  final ExamReportStatus examStatus;
  final num? exactScore, minScore, maxScore;
  final HomeworkStatus? homeworkStatus;
  final String? activityId;
  final DebtReportStatus debtStatus;
  final DebtKind? debtKind;
}

class CenterReportData {
  CenterReportData({
    required this.title,
    required List<String> columns,
    required List<List<Object?>> rows,
    required Map<String, String> summary,
    this.caption,
  }) : columns = List.unmodifiable(columns),
       rows = List.unmodifiable(
         rows.map((row) => List<Object?>.unmodifiable(row)),
       ),
       summary = Map.unmodifiable(summary);
  final String title;
  final List<String> columns;
  final List<List<Object?>> rows;
  final Map<String, String> summary;
  final String? caption;
}

abstract final class CenterReports {
  static bool isFinancial(CenterReportKind kind) => const {
    CenterReportKind.payments,
    CenterReportKind.reviews,
    CenterReportKind.closings,
    CenterReportKind.cards,
    CenterReportKind.debts,
  }.contains(kind);

  static String label(CenterReportKind kind) => switch (kind) {
    CenterReportKind.students => 'الطلبة',
    CenterReportKind.groups => 'المجموعات',
    CenterReportKind.sessions => 'الحصص',
    CenterReportKind.attendance => 'الحضور والغياب',
    CenterReportKind.packages => 'الأشهر والباقات السابقة',
    CenterReportKind.payments => 'المدفوعات',
    CenterReportKind.exams => 'الامتحانات',
    CenterReportKind.homework => 'الواجبات',
    CenterReportKind.reviews => 'مراجعة الدفع',
    CenterReportKind.closings => 'تقرير الحصة والتقفيلات',
    CenterReportKind.cards => 'الكروت',
    CenterReportKind.debts => 'المديونيات',
  };

  static bool wasEnrolledForSession(
    CenterStore store,
    Student student,
    LessonSession session,
  ) {
    if (!session.startsAtKnown) {
      return session.importRoster?.contains(student.id) ?? false;
    }
    final enrolled = store.enrollmentDateFor(student.id, session.groupId);
    return student.groupIds.contains(session.groupId) &&
        enrolled != null &&
        !enrolled.isAfter(session.startsAt);
  }

  static String correctionLabel(CorrectionAction action) => switch (action) {
    CorrectionAction.entryReversed => 'إلغاء تسجيل الحصة',
    CorrectionAction.entryCorrected => 'تصحيح طريقة الدخول',
    CorrectionAction.absencePresent => 'تصحيح غياب إلى حضور',
    CorrectionAction.paymentMethod => 'تصحيح طريقة الدفع',
    CorrectionAction.paymentCanceled => 'إلغاء الدفع',
    CorrectionAction.packageRefund => 'استرداد باقة',
    CorrectionAction.closingReopened => 'إعادة فتح التقفيلة',
  };

  static CenterReportData build(
    CenterStore store,
    CenterReportKind kind,
    CenterReportFilter filter,
  ) {
    _authorize(store, kind);
    final context = _ReportContext(store, filter);
    context.validateActivity(kind);
    return switch (kind) {
      CenterReportKind.students => context.studentReport(),
      CenterReportKind.groups => context.groupReport(),
      CenterReportKind.sessions => context.sessionReport(),
      CenterReportKind.attendance => context.attendanceReport(),
      CenterReportKind.packages => context.packageReport(),
      CenterReportKind.payments => context.paymentReport(),
      CenterReportKind.exams => context.examReport(),
      CenterReportKind.homework => context.homeworkReport(),
      CenterReportKind.reviews => context.reviewReport(),
      CenterReportKind.closings => context.closingReport(),
      CenterReportKind.cards => context.studentCardReport(),
      CenterReportKind.debts => context.debtReport(),
    };
  }

  static void _authorize(CenterStore store, CenterReportKind kind) {
    if (store.currentUser == null) {
      throw const CenterException('سجل الدخول لعرض التقارير.');
    }
    if (isFinancial(kind) && !store.canCollect) {
      throw const CenterException(
        'ليس لديك صلاحية عرض أو تصدير التقارير المالية.',
      );
    }
  }

  static Future<String> exportCsv({
    required CenterStore store,
    required CenterReportKind kind,
    required CenterReportFilter filter,
    required String destination,
  }) async {
    final report = build(store, kind, filter);
    final exportingUser = store.currentUser!;
    final file = File(p.normalize(p.absolute(destination)));
    _validateDestination(store, file);
    await file.parent.create(recursive: true);
    await file.create(exclusive: true);
    final temporary = File('${file.path}.${const Uuid().v4()}.tmp');
    try {
      final lines = [
        report.columns,
        ...report.rows,
      ].map((row) => row.map(_csvCell).join(',')).join('\r\n');
      await temporary.writeAsBytes([
        0xef,
        0xbb,
        0xbf,
        ...utf8.encode('$lines\r\n'),
      ], flush: true);
      _authorize(store, kind);
      if (store.currentUser!.id != exportingUser.id ||
          store.currentUser!.role != exportingUser.role) {
        throw const CenterException(
          'تغيّر الموظف أثناء التصدير. أعد فتح التقرير وحاول مرة أخرى.',
        );
      }
      await temporary.rename(file.path);
      return file.path;
    } catch (_) {
      if (await temporary.exists()) await temporary.delete();
      if (await file.exists() && await file.length() == 0) await file.delete();
      rethrow;
    }
  }

  static void _validateDestination(CenterStore store, File file) {
    final target = file.path.toLowerCase();
    final database = p.normalize(p.absolute(store.databasePath)).toLowerCase();
    final backups = p.join(p.dirname(database), 'backups');
    if (p.extension(target) != '.csv' ||
        target == database ||
        target.startsWith('$database-') ||
        p.isWithin(backups, target)) {
      throw const CenterException(
        'اختر ملف CSV جديدًا خارج ملفات البيانات والنسخ الاحتياطية.',
      );
    }
    if (FileSystemEntity.typeSync(file.path) != FileSystemEntityType.notFound) {
      throw const CenterException(
        'الملف موجود بالفعل. اختر اسمًا جديدًا للحفاظ عليه.',
      );
    }
  }

  static String _csvCell(Object? cell) {
    var text = cell?.toString() ?? '';
    if ((RegExp(r'^[\s\uFEFF]*[=+\-@]').hasMatch(text) &&
            !RegExp(r'^-?\d+\.\d{2}$').hasMatch(text)) ||
        text.startsWith('\t') ||
        text.startsWith('\r')) {
      text = "'$text";
    }
    return '"${text.replaceAll('"', '""')}"';
  }
}

String reportAmount(int piastres) {
  final amount = piastres.abs();
  return '${piastres < 0 ? '-' : ''}${amount ~/ 100}.${(amount % 100).toString().padLeft(2, '0')}';
}

DateTime _calendarDay(DateTime date) {
  final local = date.toLocal();
  return DateTime(local.year, local.month, local.day);
}

String _dateLabel(DateTime date) {
  final local = date.toLocal();
  return '${local.day.toString().padLeft(2, '0')}/${local.month.toString().padLeft(2, '0')}/${local.year}';
}

String _timeLabel(DateTime date) {
  final local = date.toLocal();
  return '${_dateLabel(local)} ${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
}

String _homeworkLabel(HomeworkStatus status) => switch (status) {
  HomeworkStatus.notReviewed => 'لم يُراجع',
  HomeworkStatus.complete => 'كامل',
  HomeworkStatus.incomplete => 'ناقص',
  HomeworkStatus.missing => 'لم يعمل',
  HomeworkStatus.exempt => 'معفى',
};

String _percentage(num attended, int records) {
  if (records == 0) return '—';
  final hundredths = attended is int
      ? (attended * 10000 + records ~/ 2) ~/ records
      : (attended * 10000 / records).round();
  return '${hundredths ~/ 100}.${(hundredths % 100).toString().padLeft(2, '0')}٪';
}

class _ReportContext {
  _ReportContext(this.store, this.filter) {
    if (filter.from != null &&
        filter.until != null &&
        _calendarDay(filter.from!).isAfter(_calendarDay(filter.until!))) {
      throw const CenterException('بداية الفترة يجب أن تكون قبل نهايتها.');
    }
    students = {for (final student in store.students) student.id: student};
    groups = {for (final group in store.groups) group.id: group};
    sessions = {for (final session in store.sessions) session.id: session};
    catalogs = {for (final entry in store.catalogs) entry.id: entry};
    staff = {for (final user in store.staff) user.id: user};
    attendance = store.attendances;
    payments = store.allPayments;
    activePaymentIds = store.payments.map((payment) => payment.id).toSet();
    activePackageIds = store.packages.map((package) => package.id).toSet();
    activeClosingIds = store.closings.map((closing) => closing.id).toSet();
    activeClosingsBySession = {
      for (final closing in store.closings) closing.sessionId: closing,
    };
    academics = store.academics;
    paymentsById = {for (final payment in payments) payment.id: payment};
    cardPaymentsById = {
      for (final payment in store.cardPayments) payment.id: payment,
    };
    moneyPairs = {
      for (final payment in payments.where(
        (payment) => payment.sessionId != null,
      ))
        (payment.studentId, payment.sessionId!),
      for (final payment in cardPaymentsById.values.where(
        (payment) => payment.sessionId != null,
      ))
        (payment.studentId, payment.sessionId!),
      for (final settlement in store.debtSettlements.where(
        (settlement) => settlement.sessionId != null,
      ))
        (settlement.studentId, settlement.sessionId!),
      for (final refund in store.refunds.where(
        (refund) => refund.sessionId != null,
      ))
        (refund.studentId, refund.sessionId!),
    };
    attendanceById = {
      for (final record in store.allAttendances) record.id: record,
    };
    academicByPair = {
      for (final record in academics.where(
        (record) => record.activityId == null,
      ))
        (record.studentId, record.sessionId): record,
    };
    academicPairs = {
      for (final record in academics) (record.studentId, record.sessionId),
    };
    academicByActivity = {
      for (final record in academics.where(
        (record) => record.activityId != null,
      ))
        (record.studentId, record.sessionId, record.activityId!): record,
    };
    activities = {
      for (final activity in store.academicActivities) activity.id: activity,
    };
    final sessionsByPreparedLesson = <String, List<LessonSession>>{};
    for (final session in sessions.values) {
      if (session.preparedLessonId != null) {
        sessionsByPreparedLesson
            .putIfAbsent(session.preparedLessonId!, () => [])
            .add(session);
      }
    }
    for (final activity in activities.values) {
      final targets = activity.preparedLessonId == null
          ? [
              if (sessions[activity.sessionId] != null)
                sessions[activity.sessionId]!,
            ]
          : sessionsByPreparedLesson[activity.preparedLessonId] ??
                <LessonSession>[];
      for (final session in targets) {
        activitiesBySession
            .putIfAbsent((session.id, activity.kind), () => [])
            .add(activity);
      }
    }
    attendancePairs = {
      for (final record in attendance) (record.studentId, record.sessionId),
    };
    actualAttendancePairs = {
      for (final record in attendance.where(
        (entry) =>
            entry.status == AttendanceStatus.present ||
            entry.status == AttendanceStatus.makeup,
      ))
        (record.studentId, record.sessionId),
    };
    for (final student in students.values) {
      for (final groupId in student.groupIds) {
        studentsByGroup.putIfAbsent(groupId, () => []).add(student.id);
      }
    }
    for (final pair in {...attendancePairs, ...academicPairs}) {
      historicalStudentsBySession.putIfAbsent(pair.$2, () => {}).add(pair.$1);
    }
    _validateFilters();
  }
  final CenterStore store;
  final CenterReportFilter filter;
  final DateTime generatedAt = DateTime.now();
  late final Map<String, Student> students;
  late final Map<String, StudyGroup> groups;
  late final Map<String, LessonSession> sessions;
  late final Map<String, CatalogEntry> catalogs;
  late final Map<String, StaffUser> staff;
  late final List<AttendanceRecord> attendance;
  late final List<PaymentRecord> payments;
  late final Set<String> activePaymentIds, activePackageIds, activeClosingIds;
  late final Map<String, SessionClosing> activeClosingsBySession;
  late final List<AcademicRecord> academics;
  late final Map<String, PaymentRecord> paymentsById;
  late final Map<String, StudentCardPayment> cardPaymentsById;
  late final Map<String, AttendanceRecord> attendanceById;
  late final Map<(String, String), AcademicRecord> academicByPair;
  late final Set<(String, String)> attendancePairs, academicPairs, moneyPairs;
  late final Set<(String, String)> actualAttendancePairs;
  late final Map<(String, String, String), AcademicRecord> academicByActivity;
  late final Map<String, AcademicActivity> activities;
  final Map<(String, AcademicActivityKind), List<AcademicActivity>>
  activitiesBySession = {};
  final Map<String, List<String>> studentsByGroup = {};
  final Map<String, Set<String>> historicalStudentsBySession = {};

  void _validateFilters() {
    if ([
      filter.exactScore,
      filter.minScore,
      filter.maxScore,
    ].whereType<num>().any((score) => !score.isFinite)) {
      throw const CenterException('اكتب درجة رقمية صالحة.');
    }
    if ([
      filter.exactScore,
      filter.minScore,
      filter.maxScore,
    ].whereType<num>().any((score) => score < 0)) {
      throw const CenterException('الدرجة لا تقل عن صفر.');
    }
    if (filter.minScore != null &&
        filter.maxScore != null &&
        filter.minScore! > filter.maxScore!) {
      throw const CenterException('أقل درجة يجب ألا تزيد عن أعلى درجة.');
    }
    if (filter.exactScore != null &&
        ((filter.minScore != null && filter.exactScore! < filter.minScore!) ||
            (filter.maxScore != null &&
                filter.exactScore! > filter.maxScore!))) {
      throw const CenterException('الدرجة المحددة خارج نطاق أقل وأعلى درجة.');
    }

    if ((filter.studentId != null && !students.containsKey(filter.studentId)) ||
        (filter.groupId != null && !groups.containsKey(filter.groupId)) ||
        filter.groupIds.any((id) => !groups.containsKey(id)) ||
        (filter.studyMonthId != null &&
            !store.studyMonths.any((m) => m.id == filter.studyMonthId)) ||
        (filter.preparedLessonId != null &&
            !store.studyMonths.any(
              (m) => m.lessons.any(
                (l) =>
                    l.id == filter.preparedLessonId &&
                    (filter.studyMonthId == null ||
                        m.id == filter.studyMonthId),
              ),
            )) ||
        (filter.sessionId != null && !sessions.containsKey(filter.sessionId))) {
      throw const CenterException(
        'أحد اختيارات التقرير لم يعد موجودًا. امسح الفلاتر وأعد الاختيار.',
      );
    }
    final catalogFilters = {
      CatalogKind.subject: filter.subjectId,
      CatalogKind.center: filter.centerId,
      CatalogKind.grade: filter.gradeId,
    };
    for (final entry in catalogFilters.entries) {
      if (entry.value != null && catalogs[entry.value]?.kind != entry.key) {
        throw const CenterException('اختر مادة وسنترًا وصفًا صالحين للتقرير.');
      }
    }
  }

  bool _dateMatches(DateTime date) {
    final day = _calendarDay(date);
    return (filter.from == null || !day.isBefore(_calendarDay(filter.from!))) &&
        (filter.until == null || !day.isAfter(_calendarDay(filter.until!)));
  }

  bool _studentMatches(String id) =>
      filter.studentId == null || id == filter.studentId;
  bool _groupMatches(String id) {
    final group = groups[id]!;
    return (filter.groupId == null || id == filter.groupId) &&
        (filter.groupIds.isEmpty || filter.groupIds.contains(id)) &&
        (filter.subjectId == null || group.subjectId == filter.subjectId) &&
        (filter.centerId == null || group.centerId == filter.centerId) &&
        (filter.gradeId == null || group.gradeId == filter.gradeId) &&
        (filter.sessionId == null || sessions[filter.sessionId]!.groupId == id);
  }

  bool _sessionMatches(LessonSession session) =>
      _groupMatches(session.groupId) &&
      (filter.sessionId == null || session.id == filter.sessionId) &&
      (filter.preparedLessonId == null ||
          session.preparedLessonId == filter.preparedLessonId) &&
      (filter.studyMonthId == null ||
          (session.preparedLessonId != null &&
              store.studyMonthForLesson(session.preparedLessonId!)?.id ==
                  filter.studyMonthId)) &&
      (session.startsAtKnown
          ? _dateMatches(session.startsAt)
          : filter.from == null && filter.until == null);
  String _sessionTimeLabel(LessonSession session) => session.startsAtKnown
      ? _timeLabel(session.startsAt)
      : 'التاريخ غير متوفر';
  String _groupLabel(String id) =>
      '${groups[id]!.name} · ${catalogs[groups[id]!.centerId]!.name}';
  String _staffLabel(String id) => staff[id]?.name ?? '—';
  String _sessionLabel(String? id) => id == null
      ? 'غير مرتبطة بحصة'
      : 'شهر ${sessions[id]!.monthNumber} · حصة ${sessions[id]!.number}${sessions[id]!.name.isEmpty ? '' : ' · ${sessions[id]!.name}'} — ${_groupLabel(sessions[id]!.groupId)}';
  List<AttendanceRecord> get _filteredAttendance => attendance
      .where(
        (record) =>
            _studentMatches(record.studentId) &&
            _sessionMatches(sessions[record.sessionId]!),
      )
      .toList();

  bool _belongsToSession(Student student, LessonSession session) {
    final wasEnrolled = CenterReports.wasEnrolledForSession(
      store,
      student,
      session,
    );
    final pair = (student.id, session.id);
    return wasEnrolled ||
        attendancePairs.contains(pair) ||
        academicPairs.contains(pair);
  }

  bool _financiallyBelongsToSession(Student student, LessonSession session) =>
      _belongsToSession(student, session) ||
      (store.canCollect && moneyPairs.contains((student.id, session.id)));

  CenterReportData studentReport() {
    final roster = students.values
        .where(
          (student) =>
              _studentMatches(student.id) &&
              (filter.sessionId == null
                  ? student.groupIds.any(_groupMatches)
                  : _groupMatches(sessions[filter.sessionId]!.groupId) &&
                        _belongsToSession(
                          student,
                          sessions[filter.sessionId]!,
                        )),
        )
        .toList();
    final records = _filteredAttendance;
    final historyByStudent = <String, List<AttendanceRecord>>{};
    for (final record in records) {
      historyByStudent.putIfAbsent(record.studentId, () => []).add(record);
    }
    return CenterReportData(
      title: 'الطلبة',
      columns: [
        'الكود',
        'الطالب',
        'هاتف الطالب',
        'هاتف ولي الأمر',
        'المجموعات الحالية',
        'تاريخ التسجيل',
        'الخصم ٪',
        'حضور',
        'غياب',
        'معوّض',
        'نسبة الحضور',
        'ملاحظات',
        'حالة الطالب',
        if (store.canManage || store.canCollect) 'سبب التوقف الحالي',
      ],
      rows: roster.map((student) {
        final history =
            historyByStudent[student.id] ?? const <AttendanceRecord>[];
        final present = history
            .where((record) => record.status == AttendanceStatus.present)
            .length;
        final absent = history
            .where((record) => record.status == AttendanceStatus.absent)
            .length;
        final makeup = history
            .where((record) => record.status == AttendanceStatus.makeup)
            .length;
        return <Object?>[
          student.code,
          student.name,
          student.phone,
          student.guardianPhone,
          student.groupIds.map(_groupLabel).join(' / '),
          student.createdAtKnown
              ? _dateLabel(student.createdAt)
              : 'التاريخ غير متوفر',
          student.discountPercent,
          present,
          absent,
          makeup,
          _percentage(present + makeup, history.length),
          student.notes,
          student.isSuspended ? 'متوقف — سحب الاشتراك' : 'نشط',
          if (store.canManage || store.canCollect)
            student.isSuspended ? student.suspensionReason : '',
        ];
      }).toList(),
      summary: {
        'عدد الطلبة': '${roster.length}',
        'طلبة نشطون':
            '${roster.where((student) => !student.isSuspended).length}',
        'طلبة متوقفون':
            '${roster.where((student) => student.isSuspended).length}',
      },
      caption:
          'القائمة وحالة التوقف حسب التسجيل الحالي ولا تتأثران بالفترة. التوقف لا يمحو الحضور أو الرصد أو الحركات المالية السابقة، ولا يعني استردادًا تلقائيًا. إحصاءات الحضور حسب موعد الحصة في الفترة المختارة؛ النسبة = الحضور والتعويض ÷ سجلات الحضور الفعلية، ولا تنشئ مديونية.',
    );
  }

  CenterReportData groupReport() {
    final matches = groups.values
        .where(
          (group) =>
              _groupMatches(group.id) &&
              (filter.studentId == null ||
                  students[filter.studentId]!.groupIds.contains(group.id)),
        )
        .toList();
    return CenterReportData(
      title: 'المجموعات',
      columns: const [
        'المجموعة',
        'المادة',
        'السنتر',
        'الصف',
        'المواعيد',
        'المسجلون حاليًا',
        'الحصص في الفترة',
        'سعر الحصة الحالي (جنيه مصري)',
        'الأشهر المتاحة حاليًا — الاسم / الحصص / السعر',
      ],
      rows: matches
          .map(
            (group) => <Object?>[
              group.name,
              catalogs[group.subjectId]!.name,
              catalogs[group.centerId]!.name,
              catalogs[group.gradeId]!.name,
              group.schedule,
              students.values
                  .where((student) => student.groupIds.contains(group.id))
                  .length,
              sessions.values
                  .where(
                    (session) =>
                        session.groupId == group.id && _sessionMatches(session),
                  )
                  .length,
              group.priceConfigured
                  ? reportAmount(group.sessionPrice)
                  : 'غير محدد',
              group.effectiveMonthPlans
                  .map(
                    (plan) =>
                        '${plan.name} — ${plan.sessions} حصص — ${group.priceConfigured ? '${reportAmount(plan.price)} ج' : 'السعر غير مؤكد'}',
                  )
                  .join('؛ '),
            ],
          )
          .toList(),
      summary: {'عدد المجموعات': '${matches.length}'},
      caption:
          'المجموعات والأسعار والعلاقات تعكس الوضع الحالي ولا تُفلتر بتاريخ. عدد الحصص فقط حسب موعد الحصة والفترة المختارة؛ أسماء الأشهر وعدد حصصها وأسعارها تعكس إعداد المجموعة الحالي فقط؛ لا تغيّر أسماء الأشهر المشتراة أو مبالغها التاريخية. الأسعار الحالية ليست دخلًا محصلًا.',
    );
  }

  CenterReportData sessionReport({bool liveFinancials = false}) {
    final matches =
        sessions.values
            .where(
              (session) =>
                  (!liveFinancials ||
                      session.status != SessionStatus.canceled) &&
                  _sessionMatches(session) &&
                  (filter.studentId == null ||
                      _financiallyBelongsToSession(
                        students[filter.studentId]!,
                        session,
                      )),
            )
            .toList()
          ..sort((first, second) => second.startsAt.compareTo(first.startsAt));
    final summaries = {
      for (final session in matches)
        session.id:
            (liveFinancials
                ? null
                : activeClosingsBySession[session.id]?.summary) ??
            store.sessionFinancialSummary(session.id),
    };
    return CenterReportData(
      title: liveFinancials ? 'تقرير الحصص اللحظي' : 'الحصص',
      columns: [
        'رقم الحصة',
        'المجموعة',
        'المادة',
        'الصف',
        'الموعد',
        'نوع الحصة',
        'الحالة',
        'حضور',
        'غياب مسجل',
        'معوّض',
        'السعر المنفصل (جنيه مصري)',
        'كل الحضور المجاني والإعفاء',
        'توزيع الخصم الثابت للحاضرين',
        if (store.canCollect) ...[
          'عدد مشتري الباقة في الحصة',
          'عمليات دفع الكارت',
          'تحصيل الكروت (جنيه مصري)',
          'تحصيل رسوم السنتر (جنيه مصري)',
          'التحصيل حسب المبلغ المقبوض',
          'تفصيل الدفع والرصيد السابق',
          'المستحق بعد الخصم (جنيه مصري)',
          'صافي التحصيل الفعلي (جنيه مصري)',
          'المديونية عند التسجيل (جنيه مصري)',
          'سداد المديونيات في الحصة (جنيه مصري)',
        ],
        'وقت إعداد التقرير',
        'مصدر الأرقام',
        'الشهر الدراسي',
        'اسم الحصة',
        if (liveFinancials && store.canCollect) ...[
          'السعر الأصلي للعمليات (جنيه مصري)',
          'الخصومات (جنيه مصري)',
          'الاستردادات (جنيه مصري)',
          'صافي النقدي المسجل (جنيه مصري)',
          'صافي الوسائل الأخرى (جنيه مصري)',
        ],
      ],
      rows: matches.map((session) {
        final summary = summaries[session.id]!;
        final records = attendance
            .where(
              (record) =>
                  record.sessionId == session.id &&
                  _studentMatches(record.studentId),
            )
            .toList();
        return <Object?>[
          session.number,
          _groupLabel(session.groupId),
          catalogs[groups[session.groupId]!.subjectId]!.name,
          catalogs[groups[session.groupId]!.gradeId]!.name,
          _sessionTimeLabel(session),
          switch (session.kind) {
            SessionKind.counted => 'ضمن الباقة',
            SessionKind.free => 'مجانية خارج الباقة',
            SessionKind.extra => 'بسعر منفصل',
          },
          switch (session.status) {
            SessionStatus.open => 'مفتوحة',
            SessionStatus.closed => 'مغلقة',
            SessionStatus.canceled => 'ملغاة',
          },
          records
              .where((record) => record.status == AttendanceStatus.present)
              .length,
          records
              .where((record) => record.status == AttendanceStatus.absent)
              .length,
          records
              .where((record) => record.status == AttendanceStatus.makeup)
              .length,
          session.kind == SessionKind.extra
              ? reportAmount(session.extraPrice)
              : '—',
          summary.allFreeCount ?? 'غير متوفر في هذه النسخة',
          _attendanceDiscountBreakdown(summary),
          if (store.canCollect) ...[
            summary.packageBuyerCount ?? 'غير متوفر في هذه النسخة',
            summary.cardPaymentCount ?? 'غير متوفر في هذه النسخة',
            summary.cardCollectedAmount == null
                ? 'غير متوفر في هذه النسخة'
                : reportAmount(summary.cardCollectedAmount!),
            reportAmount(summary.centerFeeCollected),
            _paymentAmountBreakdown(summary),
            _studentCategoryText(summary.studentCategories),
            reportAmount(summary.grossAmount - summary.discountAmount),
            reportAmount(summary.totalCollected),
            _optionalMoney(summary.debtAmount),
            _optionalMoney(summary.debtSettlementAmount),
          ],
          _timeLabel(generatedAt),
          !liveFinancials && activeClosingsBySession.containsKey(session.id)
              ? 'تقفيلة محفوظة — ${_timeLabel(activeClosingsBySession[session.id]!.createdAt)}'
              : session.status == SessionStatus.open
              ? 'لحظي — الحضور مفتوح'
              : 'الوضع الحالي — الحضور مغلق',
          session.monthNumber,
          session.name,
          if (liveFinancials && store.canCollect) ...[
            reportAmount(summary.grossAmount),
            reportAmount(summary.discountAmount),
            reportAmount(summary.refundAmount),
            reportAmount(summary.expectedCash),
            reportAmount(summary.totalCollected - summary.expectedCash),
          ],
        ];
      }).toList(),
      summary: {
        'عدد الحصص': '${matches.length}',
        'وقت إعداد التقرير': _timeLabel(generatedAt),
        if (liveFinancials)
          'الحصص المفتوحة':
              '${matches.where((session) => session.status == SessionStatus.open).length}',
        if (liveFinancials && store.canCollect) ...{
          'صافي التحصيل الحالي':
              '${reportAmount(summaries.values.fold<int>(0, (sum, summary) => sum + summary.totalCollected))} ج',
          'صافي النقدي المسجل':
              '${reportAmount(summaries.values.fold<int>(0, (sum, summary) => sum + summary.expectedCash))} ج',
          'رسوم السنتر المحصّلة':
              '${reportAmount(summaries.values.fold<int>(0, (sum, summary) => sum + summary.centerFeeCollected))} ج',
        },
      },
      caption:
          '${liveFinancials ? 'تقرير لحظي من الوضع الحالي، وليس تقفيلة نهائية أو إثبات النقدية الموجودة. يمكن عرضه وتصديره قبل إنهاء الحصة. ' : ''}الفترة حسب موعد الحصة. أعداد الغياب تخص الغياب المسجل فقط؛ الطلبة الذين لم يُحضّروا لا يصبحون غائبين أثناء الحصة المفتوحة. عند اختيار طالب، تقتصر أعداد الحضور على الطالب المختار؛ توزيع الخصم والمجاني والتحصيل يخص الحصة كلها. الخصم ثابت من وقت تسجيل الحضور، لا خصم الطالب اليوم. ${liveFinancials ? 'الأرقام الحالية قد تختلف عن التقفيلات السابقة، التي تظل محفوظة في العرض التاريخي. ' : 'الحصة المقفلة ماليًا تعرض تفاصيل التقفيلة المحفوظة. '}المستحق والخصم لا يساويان المقبوض؛ المديونية عند التسجيل لقطة أصلية، والسداد في الحصة يخص حصة التحصيل الفعلي ولو كان الالتزام من حصة أخرى. المجاني والإعفاء يشمل التعويض وإعفاء الدفع أو الباقة 100٪، ولا يشمل الغياب.',
    );
  }

  String _optionalMoney(int? amount) =>
      amount == null ? 'غير متوفر في هذه النسخة' : reportAmount(amount);

  String _attendanceDiscountBreakdown(SessionFinancialSummary summary) {
    final categories = summary.attendanceDiscountCategories;
    if (categories == null) return 'غير متوفر في هذه النسخة';
    return categories.isEmpty
        ? '0 طالب'
        : categories
              .map(
                (category) =>
                    '${category.label}: ${category.studentCount} طالب',
              )
              .join('؛ ');
  }

  String _paymentAmountBreakdown(SessionFinancialSummary summary) {
    final categories = summary.paymentAmountCategories;
    if (categories == null) return 'غير متوفر في هذه النسخة';
    return categories.isEmpty
        ? '0 عملية'
        : categories
              .map(
                (category) =>
                    '${_paymentCategoryLabel(category.kind)} ${reportAmount(category.unitAmount)} ج: ${category.studentCount} طالب، ${category.operationCount} عملية',
              )
              .join('؛ ');
  }

  String _paymentCategoryLabel(SessionStudentCategoryKind kind) =>
      switch (kind) {
        SessionStudentCategoryKind.single => 'حصة',
        SessionStudentCategoryKind.package => 'باقة',
        SessionStudentCategoryKind.debtSettlement => 'تسديد مديونية',
        SessionStudentCategoryKind.prepaid => 'رصيد سابق',
        SessionStudentCategoryKind.free => 'مجانية',
        SessionStudentCategoryKind.makeup => 'معوّض',
        SessionStudentCategoryKind.absent => 'غياب',
        SessionStudentCategoryKind.unpaid => 'دون تحصيل',
        SessionStudentCategoryKind.packageMember => 'باكدج',
      };

  String _studentCategoryText(List<SessionStudentCategory>? categories) {
    if (categories == null) return 'غير متوفر في هذه النسخة';
    return categories.isEmpty
        ? '0 طالب'
        : categories
              .map(
                (category) =>
                    '${category.label}: ${category.studentCount} طالب، ${category.operationCount} عملية',
              )
              .join('؛ ');
  }

  CenterReportData attendanceReport() {
    if (filter.attendanceCorrections) return _attendanceCorrectionReport();
    final records = _filteredAttendance
      ..sort(
        (first, second) => sessions[second.sessionId]!.startsAt.compareTo(
          sessions[first.sessionId]!.startsAt,
        ),
      );
    final madeUp = attendance
        .where((record) => record.originalAttendanceId != null)
        .map((record) => record.originalAttendanceId)
        .toSet();
    final present = records
        .where((record) => record.status == AttendanceStatus.present)
        .length;
    final makeup = records
        .where((record) => record.status == AttendanceStatus.makeup)
        .length;
    return CenterReportData(
      title: 'الحضور والغياب',
      columns: const [
        'الكود',
        'الطالب',
        'المجموعة',
        'الحصة',
        'موعد الحصة',
        'الحالة',
        'محسوبة من الباقة',
        'الحصة المعوضة',
        'وقت تسجيل الحالة',
      ],
      rows: records.map((record) {
        final session = sessions[record.sessionId]!;
        final original = attendanceById[record.originalAttendanceId];
        return <Object?>[
          students[record.studentId]!.code,
          students[record.studentId]!.name,
          _groupLabel(session.groupId),
          session.number,
          _sessionTimeLabel(session),
          record.packageMember
              ? '${_attendanceStatus(record.status)} · باكدج'
              : record.paymentPending
              ? record.status == AttendanceStatus.makeup
                    ? 'معوّض — غير مدفوع'
                    : 'حاضر — غير مدفوع'
              : switch (record.status) {
                  AttendanceStatus.present => 'حاضر',
                  AttendanceStatus.makeup => 'معوّض',
                  AttendanceStatus.absent =>
                    madeUp.contains(record.id) ? 'غائب — تم التعويض' : 'غائب',
                },
          record.packageId == null ? 'لا' : 'نعم',
          record.makeupSourceGroupId != null
              ? 'من ${_groupLabel(record.makeupSourceGroupId!)}'
              : original == null
              ? '—'
              : _sessionLabel(original.sessionId),
          record.importSource.isNotEmpty && !session.startsAtKnown
              ? 'التاريخ غير متوفر'
              : _timeLabel(record.recordedAt),
        ];
      }).toList(),
      summary: {
        'حضور': '$present',
        'غياب':
            '${records.where((record) => record.status == AttendanceStatus.absent).length}',
        'معوّض': '$makeup',
        'نسبة الحضور': _percentage(present + makeup, records.length),
      },
      caption:
          'الفترة حسب موعد الحصة. الحصص المستوردة بلا تاريخ تظهر عند إزالة فلتر الفترة. الحضور المستورد لا يثبت دفعًا. نسبة الحضور تشمل التعويض ومقامها عدد سجلات الحضور الفعلية.',
    );
  }

  CenterReportData _attendanceCorrectionReport() {
    final matches =
        store.corrections
            .where(
              (correction) =>
                  correction.attendanceId != null &&
                  correction.studentId != null &&
                  correction.sessionId != null &&
                  _studentMatches(correction.studentId!) &&
                  _groupMatches(sessions[correction.sessionId]!.groupId) &&
                  (filter.sessionId == null ||
                      correction.sessionId == filter.sessionId) &&
                  _dateMatches(correction.createdAt),
            )
            .toList()
          ..sort(
            (first, second) => second.createdAt.compareTo(first.createdAt),
          );
    return CenterReportData(
      title: 'سجل تصحيح الحضور',
      columns: const [
        'وقت التصحيح',
        'الكود',
        'الطالب',
        'الحصة',
        'التصحيح',
        'الحالة قبل التصحيح',
        'الحالة بعد التصحيح',
        'السبب',
        'الموظف',
        'رقم التصحيح',
      ],
      rows: matches.map((correction) {
        final original = attendanceById[correction.attendanceId]!;
        final replacement = attendanceById[correction.replacementAttendanceId];
        return <Object?>[
          _timeLabel(correction.createdAt),
          students[correction.studentId]!.code,
          students[correction.studentId]!.name,
          _sessionLabel(correction.sessionId),
          CenterReports.correctionLabel(correction.action),
          _historicalEntryLabel(original),
          replacement == null
              ? 'أُلغي التسجيل'
              : _historicalEntryLabel(replacement),
          correction.reason,
          _staffLabel(correction.staffId),
          correction.id,
        ];
      }).toList(),
      summary: {'التصحيحات': '${matches.length}'},
      caption:
          'الفترة حسب وقت التصحيح. هذا سجل العمليات الأصلية والتصحيحية؛ أعداد الحضور والنسب في عرض الحضور الحالي فقط. تفاصيل الاستردادات المالية في تقرير المدفوعات.',
    );
  }

  String _historicalEntryLabel(AttendanceRecord record) {
    if (record.packageMember) {
      return '${_attendanceStatus(record.status)} · باكدج';
    }
    if (record.paymentPending) {
      return '${_attendanceStatus(record.status)} — غير مدفوع';
    }
    final account = record.packageId != null
        ? 'باقة'
        : record.status == AttendanceStatus.makeup
        ? 'معوّض'
        : record.status == AttendanceStatus.absent
        ? 'بدون مديونية'
        : sessions[record.sessionId]!.kind == SessionKind.free
        ? 'حصة مجانية'
        : 'دفع بالحصة';
    return '${_attendanceStatus(record.status)} · $account';
  }

  String _attendanceStatus(AttendanceStatus status) => switch (status) {
    AttendanceStatus.present => 'حاضر',
    AttendanceStatus.absent => 'غائب',
    AttendanceStatus.makeup => 'معوّض',
  };

  CenterReportData packageReport() {
    final matches =
        store.allPackages
            .where(
              (package) =>
                  _studentMatches(package.studentId) &&
                  _groupMatches(package.groupId) &&
                  _dateMatches(package.purchasedAt) &&
                  (filter.sessionId == null ||
                      payments.any(
                        (payment) =>
                            payment.packageId == package.id &&
                            payment.sessionId == filter.sessionId,
                      ) ||
                      attendance.any(
                        (record) =>
                            record.packageId == package.id &&
                            record.sessionId == filter.sessionId,
                      )),
            )
            .toList()
          ..sort(
            (first, second) => second.purchasedAt.compareTo(first.purchasedAt),
          );
    return CenterReportData(
      title: 'الأشهر والباقات السابقة',
      columns: [
        'الكود',
        'الطالب',
        'المجموعة',
        'تاريخ الشراء',
        'الحصص الأصلية',
        'المتبقي حاليًا',
        'المستهلك',
        'حالة الباقة',
        if (store.canCollect) 'المستحق الأصلي (جنيه مصري)',
        'حصة الشراء',
        if (store.canCollect) ...[
          'المحصل حتى الآن (جنيه مصري)',
          'المتبقي حاليًا (جنيه مصري)',
        ],
        'اسم الشهر وقت الشراء',
      ],
      rows: matches.map((package) {
        final payment = paymentsById[package.paymentId]!;
        return <Object?>[
          students[package.studentId]!.code,
          students[package.studentId]!.name,
          _groupLabel(package.groupId),
          _timeLabel(package.purchasedAt),
          package.totalSessions,
          activePackageIds.contains(package.id) ? package.remaining : 0,
          package.totalSessions - package.remaining,
          activePackageIds.contains(package.id) ? 'سارية' : 'ملغاة — مستردة',
          if (store.canCollect) reportAmount(payment.netAmount),
          _sessionLabel(payment.sessionId),
          if (store.canCollect) ...[
            reportAmount(store.paymentCollectedFor(payment.id)),
            reportAmount(store.paymentDebtFor(payment.id)),
          ],
          package.monthPlanName ?? 'باقة قديمة — ${package.totalSessions} حصص',
        ];
      }).toList(),
      summary: {
        'عدد الباقات': '${matches.length}',
        'الرصيد الحالي':
            '${matches.where((package) => activePackageIds.contains(package.id)).fold<int>(0, (sum, package) => sum + package.remaining)} حصة',
        'باقات مستهلكة':
            '${matches.where((package) => activePackageIds.contains(package.id) && package.remaining == 0).length}',
        'باقات مستردة':
            '${matches.where((package) => !activePackageIds.contains(package.id)).length}',
      },
      caption:
          'الفترة حسب تاريخ شراء الشهر أو الباقة. اسم الشهر وعدد الحصص محفوظان وقت الشراء، حتى بعد إعادة تسمية الشهر أو حذفه من المجموعة؛ الباقات السابقة دون اسم لا تُنسب إلى شهر جديد. المستحق هو سعر الشراء بعد الخصم، وليس مبلغًا محصلًا. المحصل والمتبقي يعكسان الوضع الحالي؛ رصيد الحصص لا يثبت سداد ثمن الباقة كاملًا. الباقات الملغاة مستبعدة من الرصيد والمديونية؛ التحصيل والاسترداد الفعليان يظهران كتاريخ حركة في تقرير المدفوعات. فلتر الحصة يعرض الباقات المشتراة أو المستخدمة فيها.',
    );
  }

  bool _paymentScope(String studentId, String? groupId, String? sessionId) =>
      _studentMatches(studentId) &&
      (groupId == null ? _hasNoGroupFilter : _groupMatches(groupId)) &&
      (filter.sessionId == null || sessionId == filter.sessionId) &&
      (!filter.unassignedPaymentsOnly || sessionId == null);

  bool get _hasNoGroupFilter =>
      filter.groupId == null &&
      filter.subjectId == null &&
      filter.centerId == null &&
      filter.gradeId == null &&
      filter.sessionId == null;

  String _optionalGroupLabel(String? groupId) =>
      groupId == null ? 'غير مرتبطة بمجموعة' : _groupLabel(groupId);

  String _paymentCurrentStatus(PaymentRecord payment) {
    if (!activePaymentIds.contains(payment.id)) return 'ملغاة — أُلغي الالتزام';
    if (payment.netAmount == 0) return 'إعفاء كامل';
    if (store.paymentDebtFor(payment.id) == 0) return 'مسددة بالكامل';
    return store.paymentCollectedFor(payment.id) == 0
        ? 'لم يُحصل مبلغ'
        : 'دفع جزئي';
  }

  List<Object?> _paymentBalanceCells(PaymentRecord payment) => [
    reportAmount(payment.netAmount),
    reportAmount(store.paymentDebtFor(payment.id)),
    _sessionLabel(payment.sessionId),
    reportAmount(store.paymentCollectedFor(payment.id)),
    _groupLabel(payment.groupId),
  ];

  List<Object?> _collectionRow(PaymentRecord payment) => [
    _timeLabel(payment.createdAt),
    payment.collectedAmount == 0 && payment.netAmount > 0
        ? 'تسجيل استحقاق دون تحصيل'
        : 'تحصيل',
    students[payment.studentId]!.code,
    students[payment.studentId]!.name,
    _groupLabel(payment.groupId),
    _sessionLabel(payment.sessionId),
    payment.description,
    reportAmount(payment.baseAmount),
    payment.discountPercent,
    reportAmount(payment.baseAmount - payment.netAmount),
    reportAmount(payment.collectedAmount),
    store.effectivePaymentMethod(payment.id),
    payment.method,
    _paymentCurrentStatus(payment),
    _staffLabel(payment.staffId),
    payment.id,
    payment.id,
    '',
    ..._paymentBalanceCells(payment),
  ];

  List<Object?> _refundRow(RefundRecord refund) {
    final original = paymentsById[refund.paymentId]!;
    return [
      _timeLabel(refund.createdAt),
      'استرداد',
      students[refund.studentId]!.code,
      students[refund.studentId]!.name,
      _groupLabel(refund.groupId),
      _sessionLabel(refund.sessionId),
      'رد المبلغ المحصل فعليًا',
      null,
      null,
      null,
      reportAmount(-refund.amount),
      refund.method,
      original.method,
      'مبلغ مُعاد',
      _staffLabel(refund.staffId),
      refund.id,
      refund.paymentId,
      refund.reason,
      ..._paymentBalanceCells(original),
    ];
  }

  StudentDebt _settlementOriginal(DebtSettlement settlement) {
    if (settlement.kind == DebtKind.lesson) {
      return _lessonDebt(paymentsById[settlement.paymentId]!);
    }
    return _cardDebt(cardPaymentsById[settlement.paymentId]!);
  }

  String? _settlementGroup(DebtSettlement settlement, StudentDebt original) =>
      settlement.sessionId == null
      ? original.groupId
      : sessions[settlement.sessionId]!.groupId;

  bool _settlementMatches(DebtSettlement settlement) {
    final original = _settlementOriginal(settlement);
    return _paymentScope(
          settlement.studentId,
          _settlementGroup(settlement, original),
          settlement.sessionId,
        ) &&
        _dateMatches(settlement.createdAt);
  }

  List<Object?> _settlementRow(DebtSettlement settlement) {
    final original = _settlementOriginal(settlement);
    final remaining = settlement.kind == DebtKind.lesson
        ? store.paymentDebtFor(settlement.paymentId)
        : store.cardDebtFor(settlement.paymentId);
    return [
      _timeLabel(settlement.createdAt),
      'سداد مديونية',
      students[settlement.studentId]!.code,
      students[settlement.studentId]!.name,
      _optionalGroupLabel(_settlementGroup(settlement, original)),
      _sessionLabel(settlement.sessionId),
      original.description,
      null,
      null,
      null,
      reportAmount(settlement.amount),
      settlement.method,
      settlement.kind == DebtKind.lesson
          ? paymentsById[settlement.paymentId]!.method
          : cardPaymentsById[settlement.paymentId]!.method,
      settlement.kind == DebtKind.lesson &&
              !activePaymentIds.contains(settlement.paymentId)
          ? 'الالتزام الأصلي ملغى'
          : remaining == 0
          ? 'مسددة حاليًا'
          : 'متبقي مديونية',
      _staffLabel(settlement.staffId),
      settlement.id,
      settlement.paymentId,
      settlement.notes,
      reportAmount(original.dueAmount),
      reportAmount(remaining),
      _sessionLabel(original.sessionId),
      reportAmount(original.collectedAmount),
      _optionalGroupLabel(original.groupId),
    ];
  }

  CenterReportData paymentReport() {
    final collections = filter.paymentMode == PaymentReportMode.refunds
        ? <PaymentRecord>[]
        : payments
              .where(
                (payment) =>
                    _paymentScope(
                      payment.studentId,
                      payment.groupId,
                      payment.sessionId,
                    ) &&
                    _dateMatches(payment.createdAt),
              )
              .toList();
    final cards = filter.paymentMode == PaymentReportMode.refunds
        ? <StudentCardPayment>[]
        : store.cardPayments.where(_cardPaymentMatches).toList();
    final settlements = filter.paymentMode == PaymentReportMode.refunds
        ? <DebtSettlement>[]
        : store.debtSettlements.where(_settlementMatches).toList();
    final refunds = filter.paymentMode == PaymentReportMode.collections
        ? <RefundRecord>[]
        : store.refunds
              .where(
                (refund) =>
                    _paymentScope(
                      refund.studentId,
                      refund.groupId,
                      refund.sessionId,
                    ) &&
                    _dateMatches(refund.createdAt),
              )
              .toList();
    final movements = <_ReportMoneyMovement>[
      for (final payment in collections)
        _ReportMoneyMovement(
          time: payment.createdAt,
          amount: payment.collectedAmount,
          method: store.effectivePaymentMethod(payment.id),
          sessionId: payment.sessionId,
          kind: DebtKind.lesson,
          cells: _collectionRow(payment),
        ),
      for (final payment in cards)
        _ReportMoneyMovement(
          time: payment.createdAt,
          amount: payment.collectedAmount,
          method: payment.method,
          sessionId: payment.sessionId,
          kind: DebtKind.card,
          cells: _cardPaymentRow(payment),
        ),
      for (final settlement in settlements)
        _ReportMoneyMovement(
          time: settlement.createdAt,
          amount: settlement.amount,
          method: settlement.method,
          sessionId: settlement.sessionId,
          kind: settlement.kind,
          cells: _settlementRow(settlement),
        ),
      for (final refund in refunds)
        _ReportMoneyMovement(
          time: refund.createdAt,
          amount: -refund.amount,
          method: refund.method,
          sessionId: refund.sessionId,
          kind: DebtKind.lesson,
          cells: _refundRow(refund),
        ),
    ]..sort((first, second) => second.time.compareTo(first.time));
    int totalWhere(bool Function(_ReportMoneyMovement) include) => movements
        .where(include)
        .fold<int>(0, (sum, movement) => sum + movement.amount);
    final collected = totalWhere((movement) => movement.amount > 0);
    final returned = refunds.fold<int>(0, (sum, refund) => sum + refund.amount);
    final methods = movements.map((movement) => movement.method).toSet();
    return CenterReportData(
      title: filter.unassignedPaymentsOnly
          ? 'مدفوعات غير مرتبطة بحصة'
          : 'المدفوعات والاستردادات',
      columns: const [
        'وقت الحركة',
        'نوع الحركة',
        'الكود',
        'الطالب',
        'المجموعة',
        'حصة التحصيل أو الرد',
        'البيان',
        'الأصلي (جنيه مصري)',
        'الخصم ٪',
        'قيمة الخصم (جنيه مصري)',
        'الحركة الفعلية (جنيه مصري)',
        'طريقة الدفع',
        'الطريقة الأصلية',
        'الحالة الحالية',
        'الموظف',
        'رقم العملية',
        'رقم الدفع الأصلي',
        'سبب الرد / ملاحظات السداد',
        'المستحق الأصلي بعد الخصم (جنيه مصري)',
        'المديونية الحالية (جنيه مصري)',
        'حصة الالتزام الأصلي',
        'إجمالي المحصل للأصل حتى الآن (جنيه مصري)',
        'مجموعة الالتزام الأصلي',
      ],
      rows: movements.map((movement) => movement.cells).toList(),
      summary: {
        'عدد العمليات': '${movements.length}',
        'التحصيل': '${reportAmount(collected)} ج',
        'الاستردادات': '${reportAmount(returned)} ج',
        'الصافي بعد الاسترداد': '${reportAmount(collected - returned)} ج',
        'صافي النقدي':
            '${reportAmount(totalWhere((movement) => movement.method == 'نقدي'))} ج',
        'دفعات سارية ضمن النتائج':
            '${collections.where((payment) => activePaymentIds.contains(payment.id)).length + cards.length}',
        'تحصيل الكروت':
            '${reportAmount(totalWhere((movement) => movement.kind == DebtKind.card))} ج',
        'سداد المديونيات':
            '${reportAmount(settlements.fold<int>(0, (sum, settlement) => sum + settlement.amount))} ج',
        'الخصومات':
            '${reportAmount(collections.fold<int>(0, (sum, payment) => sum + payment.baseAmount - payment.netAmount) + cards.fold<int>(0, (sum, payment) => sum + payment.baseAmount - payment.netAmount))} ج',
        'عمليات غير مرتبطة بحصة':
            '${movements.where((movement) => movement.sessionId == null).length}',
        'تحصيل غير مرتبط بحصة':
            '${reportAmount(totalWhere((movement) => movement.sessionId == null && movement.amount > 0))} ج',
        'صافي غير مرتبط بحصة':
            '${reportAmount(totalWhere((movement) => movement.sessionId == null))} ج',
        for (final method in methods)
          method:
              '${reportAmount(totalWhere((movement) => movement.method == method))} ج',
      },
      caption:
          'الفترة والفلاتر حسب حركة التحصيل أو الرد الفعلية. سداد المديونية يظهر بتاريخ وموظف وحصة السداد، مستقلًا عن حصة الالتزام الأصلي. المبالغ غير المقبوضة ليست دخلًا؛ تسجيل الاستحقاق بصفر لا يضيف تحصيلًا. الخصم والمستحق يخصان الأصل فقط ولا يتكرران في سداد المديونية. المديونية وإجمالي المحصل للأصل يعكسان الوضع الحالي؛ لا تجمعهما من صفوف الحركات المتكررة. إلغاء الأصل لا يمحو التحصيل التاريخي، والرد يعيد المقبوض فعلًا فقط. صافي النقدي = التحصيل النقدي ناقص الرد النقدي. دفع الكارت مستقل عن الحصص؛ المدفوعات دون حصة لا تُنسب لتقفيلة.',
    );
  }

  StudentDebt _lessonDebt(PaymentRecord payment) => StudentDebt(
    paymentId: payment.id,
    kind: DebtKind.lesson,
    studentId: payment.studentId,
    groupId: payment.groupId,
    sessionId: payment.sessionId,
    description: payment.description,
    dueAmount: payment.netAmount,
    collectedAmount: store.paymentCollectedFor(payment.id),
    createdAt: payment.createdAt,
  );

  bool _debtMatches(StudentDebt debt) =>
      _paymentScope(debt.studentId, debt.groupId, debt.sessionId) &&
      _dateMatches(debt.createdAt) &&
      (filter.debtKind == null || debt.kind == filter.debtKind) &&
      debt.dueAmount > 0 &&
      switch (filter.debtStatus) {
        DebtReportStatus.outstanding => debt.remainingAmount > 0,
        DebtReportStatus.settled => debt.remainingAmount == 0,
        DebtReportStatus.all => true,
      };

  CenterReportData debtReport() {
    final balances =
        <StudentDebt>[
          ...store.payments.map(_lessonDebt),
          ...store.cardPayments.map(_cardDebt),
        ].where(_debtMatches).toList()..sort(
          (first, second) => second.createdAt.compareTo(first.createdAt),
        );
    return CenterReportData(
      title: 'المديونيات',
      columns: const [
        'الكود',
        'الطالب',
        'نوع الالتزام',
        'البيان',
        'المجموعة الأصلية',
        'الحصة الأصلية',
        'تاريخ الاستحقاق',
        'المستحق بعد الخصم (جنيه مصري)',
        'المحصل حتى الآن (جنيه مصري)',
        'المديونية الحالية (جنيه مصري)',
        'الحالة الحالية',
        'رقم الدفع الأصلي',
      ],
      rows: balances
          .map(
            (debt) => <Object?>[
              students[debt.studentId]!.code,
              students[debt.studentId]!.name,
              debt.kind == DebtKind.card ? 'كارت' : 'حصة / باقة',
              debt.description,
              _optionalGroupLabel(debt.groupId),
              _sessionLabel(debt.sessionId),
              _timeLabel(debt.createdAt),
              reportAmount(debt.dueAmount),
              reportAmount(debt.collectedAmount),
              reportAmount(debt.remainingAmount),
              debt.remainingAmount == 0
                  ? 'مسددة'
                  : debt.collectedAmount == 0
                  ? 'لم يُحصل مبلغ'
                  : 'دفع جزئي',
              debt.paymentId,
            ],
          )
          .toList(),
      summary: {
        'عدد الالتزامات': '${balances.length}',
        'عدد الطلبة':
            '${balances.map((debt) => debt.studentId).toSet().length}',
        'المستحق':
            '${reportAmount(balances.fold<int>(0, (sum, debt) => sum + debt.dueAmount))} ج',
        'المحصل حتى الآن':
            '${reportAmount(balances.fold<int>(0, (sum, debt) => sum + debt.collectedAmount))} ج',
        'المديونية الحالية':
            '${reportAmount(balances.fold<int>(0, (sum, debt) => sum + debt.remainingAmount))} ج',
      },
      caption:
          'الأرصدة الحالية للحصص والباقات والكروت السارية. الفترة والمجموعة والحصة حسب إنشاء الالتزام الأصلي؛ التحصيل يشمل أصل الدفع وكل سداد لاحق، وليس دخل الفترة المختارة. الالتزامات الملغاة والإعفاءات الكاملة لا تمثل مديونية. تقرير المدفوعات يعرض السداد والرد حسب وقت الحركة؛ التقفيلات السابقة تحتفظ بلقطتها الأصلية ولا تتغير بسداد لاحق.',
    );
  }

  void validateActivity(CenterReportKind kind) {
    final id = filter.activityId;
    if (id == null) return;
    final activity = activities[id];
    final expectedKind = switch (kind) {
      CenterReportKind.exams => AcademicActivityKind.exam,
      CenterReportKind.homework => AcademicActivityKind.homework,
      _ => null,
    };
    final targets = activity == null
        ? <LessonSession>[]
        : sessions.values
              .where(
                (session) =>
                    activity.appliesToSession(session) &&
                    session.status != SessionStatus.canceled,
              )
              .toList();
    final emptyPreparedDefinition =
        activity?.preparedLessonId != null &&
        targets.isEmpty &&
        filter.sessionId == null &&
        filter.groupId == null &&
        filter.centerId == null &&
        filter.gradeId == null &&
        filter.subjectId == null;
    if (activity == null ||
        expectedKind != activity.kind ||
        (!emptyPreparedDefinition && !targets.any(_sessionMatches))) {
      throw const CenterException(
        'النشاط المختار لا يطابق نوع التقرير أو الحصة والفلاتر. أعد اختيار الامتحان أو الواجب.',
      );
    }
  }

  Iterable<
    ({
      Student student,
      LessonSession session,
      AcademicRecord? academic,
      AcademicActivity? activity,
    })
  >
  _academicRows(AcademicActivityKind kind) sync* {
    for (final session in sessions.values.where(
      (session) =>
          _sessionMatches(session) && session.status != SessionStatus.canceled,
    )) {
      final named =
          activitiesBySession[(session.id, kind)] ?? const <AcademicActivity>[];
      final selected = named.where(
        (activity) =>
            filter.activityId == null || activity.id == filter.activityId,
      );
      final studentIds = {
        ...?studentsByGroup[session.groupId],
        ...?historicalStudentsBySession[session.id],
        ...?session.importRoster,
      };
      for (final id in studentIds) {
        final student = students[id]!;
        if (!_studentMatches(id) || !_belongsToSession(student, session)) {
          continue;
        }
        for (final activity in selected) {
          final academic = academicByActivity[(id, session.id, activity.id)];
          if (activity.preparedLessonId != null &&
              academic == null &&
              !actualAttendancePairs.contains((id, session.id))) {
            continue;
          }
          yield (
            student: student,
            session: session,
            academic: academic,
            activity: activity,
          );
        }
        final legacy = academicByPair[(id, session.id)];
        final hasNamedActivities =
            activitiesBySession.containsKey((
              session.id,
              AcademicActivityKind.exam,
            )) ||
            activitiesBySession.containsKey((
              session.id,
              AcademicActivityKind.homework,
            ));
        if (filter.activityId == null &&
            ((!hasNamedActivities && session.preparedLessonId == null) ||
                legacy != null)) {
          yield (
            student: student,
            session: session,
            academic: legacy,
            activity: null,
          );
        }
      }
    }
  }

  CenterReportData examReport() {
    final matches = _academicRows(AcademicActivityKind.exam).where((row) {
      final absent = row.academic?.examAbsent == true;
      final score = row.academic?.score;
      final statusMatches = switch (filter.examStatus) {
        ExamReportStatus.all => true,
        ExamReportStatus.recorded => score != null && !absent,
        ExamReportStatus.absent => absent,
        ExamReportStatus.unrecorded => score == null && !absent,
        ExamReportStatus.notTaken => absent || score == null,
      };
      final hasScoreFilter =
          filter.exactScore != null ||
          filter.minScore != null ||
          filter.maxScore != null;
      return statusMatches &&
          (!hasScoreFilter ||
              (score != null &&
                  !absent &&
                  (filter.exactScore == null || score == filter.exactScore) &&
                  (filter.minScore == null || score >= filter.minScore!) &&
                  (filter.maxScore == null || score <= filter.maxScore!)));
    }).toList();
    final absent = matches
        .where((row) => row.academic?.examAbsent == true)
        .length;
    final recorded = matches.where((row) => row.academic?.score != null).length;
    return CenterReportData(
      title: filter.activityId == null
          ? 'الامتحانات'
          : 'الامتحانات · ${activities[filter.activityId]!.name}',
      columns: const [
        'الكود',
        'الطالب',
        'المجموعة',
        'الحصة',
        'موعد الحصة',
        'حالة الامتحان',
        'الدرجة',
        'الدرجة النهائية',
        'النسبة',
        'ملاحظات',
        'اسم الامتحان',
      ],
      rows: matches.map((row) {
        final record = row.academic;
        final maximumKnown =
            record?.maxScoreKnown ?? row.activity?.maxScoreKnown ?? true;
        return <Object?>[
          row.student.code,
          row.student.name,
          _groupLabel(row.session.groupId),
          row.session.number,
          _sessionTimeLabel(row.session),
          record?.examAbsent == true
              ? 'غائب عن الامتحان'
              : record?.score == null
              ? 'لم تُرصد'
              : 'مرصودة',
          record?.score,
          maximumKnown
              ? record?.maxScore ?? row.activity?.maxScore
              : 'غير معروفة',
          record?.score == null || !maximumKnown
              ? '—'
              : _percentage(record!.score!, record.maxScore),
          record?.notes ?? '',
          row.activity?.name ?? 'رصد سابق بدون اسم',
        ];
      }).toList(),
      summary: {
        'درجات مرصودة': '$recorded',
        'غائب عن الامتحان': '$absent',
        'لم تُرصد': '${matches.length - recorded - absent}',
      },
      caption:
          'الفترة حسب موعد الحصة الفعلية، وكل صف يخص الامتحان المسمى وحده؛ الحصص المشتركة تشمل الحاضرين والمعوّضين فعليًا والنتائج المحفوظة. سجلات الحصص القديمة بدون اسم منفصلة وتحتفظ بقائمتها السابقة. الصفر درجة فعلية؛ الفراغ يعني لم تُرصد. «غائب أو لم تُرصد» يجمع الحالتين مع تمييزهما؛ عدم الرصد لا يثبت غياب الطالب أو إقامة امتحان. البحث بالدرجة يطابق الدرجات المرصودة فقط، والنطاق شامل الطرفين.',
    );
  }

  CenterReportData homeworkReport() {
    final matches = _academicRows(AcademicActivityKind.homework)
        .where(
          (row) =>
              filter.homeworkStatus == null ||
              (row.academic?.homework ?? HomeworkStatus.notReviewed) ==
                  filter.homeworkStatus,
        )
        .toList();
    return CenterReportData(
      title: filter.activityId == null
          ? 'الواجبات'
          : 'الواجبات · ${activities[filter.activityId]!.name}',
      columns: const [
        'الكود',
        'الطالب',
        'رقم الطالب',
        'رقم ولي الأمر',
        'الشهر',
        'المجموعة',
        'الحصة',
        'موعد الحصة',
        'حالة الواجب',
        'ملاحظات',
        'اسم الواجب',
      ],
      rows: matches
          .map(
            (row) => <Object?>[
              row.student.code,
              row.student.name,
              row.student.phone,
              row.student.guardianPhone,
              row.session.preparedLessonId == null
                  ? 'شهر ${row.session.monthNumber}'
                  : store
                            .studyMonthForLesson(row.session.preparedLessonId!)
                            ?.name ??
                        'شهر ${row.session.monthNumber}',
              _groupLabel(row.session.groupId),
              row.session.number,
              _sessionTimeLabel(row.session),
              _homeworkLabel(
                row.academic?.homework ?? HomeworkStatus.notReviewed,
              ),
              row.academic?.notes ?? '',
              row.activity?.name ?? 'رصد سابق بدون اسم',
            ],
          )
          .toList(),
      summary: {
        for (final status in HomeworkStatus.values)
          _homeworkLabel(
            status,
          ): '${matches.where((row) => (row.academic?.homework ?? HomeworkStatus.notReviewed) == status).length}',
      },
      caption:
          'الفترة حسب موعد الحصة الفعلية، وكل صف يخص الواجب المسمى وحده؛ الحصص المشتركة تشمل الحاضرين والمعوّضين فعليًا والنتائج المحفوظة. السجلات القديمة بدون اسم منفصلة. لا تعني «لم يُراجع» أن الطالب لم يعمل الواجب؛ الرصد فقط يحدد حالته.',
    );
  }

  CenterReportData reviewReport() {
    final showCodes = filter.reviewMode != ReviewReportMode.amounts;
    final showAmounts = filter.reviewMode != ReviewReportMode.codes;
    final checks = showCodes
        ? store.paymentChecks
              .where(
                (check) =>
                    _studentMatches(check.studentId) &&
                    _dateMatches(check.checkedAt) &&
                    (filter.sessionId == null ||
                        check.sessionId == filter.sessionId) &&
                    _groupMatches(sessions[check.sessionId]!.groupId) &&
                    sessions[check.sessionId]!.status !=
                        SessionStatus.canceled &&
                    store.isPaymentCheckCurrent(
                      check.studentId,
                      check.sessionId,
                    ),
              )
              .toList()
        : <PaymentCheck>[];
    final amounts = showAmounts
        ? store.reviews
              .where(
                (review) =>
                    _studentMatches(review.studentId) &&
                    _dateMatches(review.createdAt) &&
                    (filter.sessionId == null ||
                        review.sessionId == filter.sessionId) &&
                    (review.sessionId != null
                        ? _groupMatches(sessions[review.sessionId]!.groupId)
                        : review.paymentId != null
                        ? _groupMatches(paymentsById[review.paymentId]!.groupId)
                        : filter.groupId == null &&
                              filter.subjectId == null &&
                              filter.centerId == null &&
                              filter.gradeId == null),
              )
              .toList()
        : <PaymentReview>[];
    final rows = <(DateTime, List<Object?>)>[
      for (final check in checks)
        (
          check.checkedAt,
          [
            _timeLabel(check.checkedAt),
            students[check.studentId]!.code,
            students[check.studentId]!.name,
            _sessionLabel(check.sessionId),
            'مراجعة كود',
            if (showAmounts) ...[null, null, null],
            _paymentCheckLabel(check.status),
            _staffLabel(check.staffId),
            '',
            check.paymentId,
            check.packageId,
            reportAmount(
              store
                  .paymentStatusFor(check.studentId, check.sessionId)
                  .debtAmount,
            ),
            check.paymentId == null
                ? null
                : _paymentCurrentStatus(paymentsById[check.paymentId]!),
            _optionalMoney(check.amount),
          ],
        ),
      for (final review in amounts)
        (
          review.createdAt,
          [
            _timeLabel(review.createdAt),
            students[review.studentId]!.code,
            students[review.studentId]!.name,
            _sessionLabel(review.sessionId),
            'مراجعة مبلغ',
            reportAmount(review.expectedAmount),
            reportAmount(review.paperAmount),
            reportAmount(review.difference),
            review.paymentId == null
                ? 'ورق دون دفع مسجل'
                : review.matched
                ? 'مطابق'
                : review.difference < 0
                ? 'نقص في الورق'
                : 'زيادة في الورق',
            _staffLabel(review.staffId),
            review.notes,
            review.paymentId,
            paymentsById[review.paymentId]?.packageId,
            review.paymentId == null
                ? null
                : reportAmount(store.paymentDebtFor(review.paymentId!)),
            review.paymentId == null
                ? null
                : _paymentCurrentStatus(paymentsById[review.paymentId]!),
            null,
          ],
        ),
    ]..sort((first, second) => second.$1.compareTo(first.$1));
    return CenterReportData(
      title: 'مراجعة الدفع',
      columns: [
        'وقت المراجعة',
        'الكود',
        'الطالب',
        'الحصة',
        'نوع المراجعة',
        if (showAmounts) ...[
          'المقبوض الأصلي وقت المراجعة (جنيه مصري)',
          'الورق (جنيه مصري)',
          'الفرق (جنيه مصري)',
        ],
        'الحالة',
        'الموظف',
        'ملاحظات',
        'رقم عملية الدفع',
        'رقم الباقة',
        'المبلغ المتبقي المرتبط حاليًا (جنيه مصري)',
        'حالة الدفع المرتبط حاليًا',
        'المبلغ المحصّل الذي تمت مراجعته (جنيه مصري)',
      ],
      rows: rows.map((row) => row.$2).toList(),
      summary: {
        'المراجعات': '${rows.length}',
        if (showCodes) ...{
          'مراجعات الأكواد': '${checks.length}',
          for (final status in StudentPaymentStatus.values)
            _paymentCheckLabel(status):
                '${checks.where((check) => check.status == status).length}',
        },
        if (showAmounts) ...{
          'مراجعات المبالغ': '${amounts.length}',
          'مطابقة': '${amounts.where((review) => review.matched).length}',
          'غير محسومة': '${amounts.where((review) => !review.matched).length}',
          'فروق الورق':
              '${reportAmount(amounts.fold<int>(0, (sum, review) => sum + review.difference))} ج',
        },
      },
      caption:
          'الفترة حسب وقت المراجعة. مراجعات الأكواد المعروضة هي العلامات السارية للحاضرين والمعوّضين، ومبلغها المحفوظ يطابق التحصيل الحالي في هذه الحصة. العلامة الملغاة أو القديمة التي تغيّر مبلغها أو دفعها لا تُحتسب ولا تُصدّر. المبلغ قد يكون جزئيًا؛ علامة المراجعة لا تثبت سداد الثمن كاملًا أو تضيف دخلًا. المديونية الحالية منفصلة عن مبلغ الإيصال المراجع. الرصيد السابق أو الإعفاء دون تحصيل في الحصة ليس إيصالًا للمراجعة.'
          '${showAmounts ? ' أعمدة المبالغ تخص مراجعات المبلغ فقط؛ فرق الورق هو مبلغ الورق ناقص المقبوض الأصلي المحفوظ وقت المراجعة، ولا يشمل تسديد المديونية اللاحق.' : ''}',
    );
  }

  String _paymentCheckLabel(StudentPaymentStatus status) => switch (status) {
    StudentPaymentStatus.paidSingle => 'مسجل دفع حصة',
    StudentPaymentStatus.paidPackage => 'مسجل باقة',
    StudentPaymentStatus.free => 'إعفاء رسوم المدرس',
    StudentPaymentStatus.makeup => 'معوّض',
    StudentPaymentStatus.notPaid => 'غير دافع وقت المراجعة',
  };

  CenterReportData closingReport() {
    if (filter.closingMode == ClosingReportMode.live) {
      return sessionReport(liveFinancials: true);
    }
    final matches =
        store.allClosings
            .where(
              (closing) =>
                  _groupMatches(sessions[closing.sessionId]!.groupId) &&
                  _dateMatches(closing.createdAt) &&
                  (filter.sessionId == null ||
                      closing.sessionId == filter.sessionId) &&
                  (filter.studentId == null ||
                      _financiallyBelongsToSession(
                        students[filter.studentId]!,
                        sessions[closing.sessionId]!,
                      )),
            )
            .toList()
          ..sort(
            (first, second) => second.createdAt.compareTo(first.createdAt),
          );
    if (filter.closingMode == ClosingReportMode.categories) {
      return _closingCategoryReport(matches);
    }
    return CenterReportData(
      title: 'تقفيلات الحصص',
      columns: const [
        'وقت التقفيلة',
        'حالة التقفيلة',
        'الحصة',
        'المجموعة',
        'تفاصيل الفئات',
        'كل الحضور المجاني والإعفاء',
        'توزيع الخصم الثابت للحاضرين',
        'عدد مشتري الباقة في الحصة',
        'التحصيل حسب المبلغ المقبوض',
        'دفع بلا خصم حسب الفئة',
        'تفصيل نسب الخصم',
        'إعفاء 100٪',
        'حضور من رصيد سابق',
        'تفصيل المجاني والتعويض والغياب',
        'حضور',
        'غياب',
        'معوّض',
        'حضور محسوب من الباقة',
        'حضور مجاني',
        'عمليات تحصيل الحصص الأصلية',
        'عمليات بيع الباقات الأصلية',
        'عمليات دفع الكارت',
        'تحصيل الكروت (جنيه مصري)',
        'تحصيل رسوم السنتر (جنيه مصري)',
        'الأصلي (جنيه مصري)',
        'الخصومات (جنيه مصري)',
        'صافي التحصيل (جنيه مصري)',
        'الاستردادات (جنيه مصري)',
        'النقدي المتوقع (جنيه مصري)',
        'النقدي الفعلي (جنيه مصري)',
        'الفرق (جنيه مصري)',
        'الموظف',
        'ملاحظات',
        'المديونية عند التسجيل (جنيه مصري)',
        'سداد المديونيات في الحصة (جنيه مصري)',
      ],
      rows: matches
          .map(
            (closing) => <Object?>[
              _timeLabel(closing.createdAt),
              activeClosingIds.contains(closing.id)
                  ? 'سارية'
                  : 'أُعيد فتحها — تاريخية',
              sessions[closing.sessionId]!.number,
              _groupLabel(sessions[closing.sessionId]!.groupId),
              closing.summary.studentCategories == null
                  ? 'غير محفوظة — تقفيلة قديمة'
                  : 'محفوظة وقت التقفيل',
              closing.summary.allFreeCount ?? 'غير متوفر في هذه النسخة',
              _attendanceDiscountBreakdown(closing.summary),
              closing.summary.packageBuyerCount ?? 'غير متوفر في هذه النسخة',
              _paymentAmountBreakdown(closing.summary),
              _categoryBreakdown(
                closing,
                (category) =>
                    _isNewCollectionCategory(category) &&
                    category.discountPercent == 0,
              ),
              _categoryBreakdown(
                closing,
                (category) =>
                    _isNewCollectionCategory(category) &&
                    category.discountPercent != null &&
                    category.discountPercent! > 0 &&
                    category.discountPercent! < 100,
              ),
              _categoryBreakdown(
                closing,
                (category) =>
                    _isNewCollectionCategory(category) &&
                    category.discountPercent == 100,
              ),
              _categoryBreakdown(
                closing,
                (category) =>
                    category.kind == SessionStudentCategoryKind.prepaid,
              ),
              _categoryBreakdown(
                closing,
                (category) => const {
                  SessionStudentCategoryKind.packageMember,
                  SessionStudentCategoryKind.unpaid,
                  SessionStudentCategoryKind.free,
                  SessionStudentCategoryKind.makeup,
                  SessionStudentCategoryKind.absent,
                }.contains(category.kind),
              ),
              closing.summary.presentCount,
              closing.summary.absentCount,
              closing.summary.makeupCount,
              closing.summary.prepaidCount,
              closing.summary.freeCount,
              closing.summary.singlePaymentCount,
              closing.summary.packageSalesCount,
              closing.summary.cardPaymentCount ?? 'غير متوفر في هذه النسخة',
              closing.summary.cardCollectedAmount == null
                  ? 'غير متوفر في هذه النسخة'
                  : reportAmount(closing.summary.cardCollectedAmount!),
              reportAmount(closing.summary.centerFeeCollected),
              reportAmount(closing.summary.grossAmount),
              reportAmount(closing.summary.discountAmount),
              reportAmount(closing.summary.totalCollected),
              reportAmount(closing.summary.refundAmount),
              reportAmount(closing.summary.expectedCash),
              reportAmount(closing.actualCash),
              reportAmount(closing.difference),
              _staffLabel(closing.staffId),
              closing.notes,
              _optionalMoney(closing.summary.debtAmount),
              _optionalMoney(closing.summary.debtSettlementAmount),
            ],
          )
          .toList(),
      summary: _closingTotals(matches),
      caption:
          'الفترة حسب وقت التقفيلة. كل النسخ محفوظة؛ الإجماليات تخص التقفيلات السارية فقط وتستبعد النسخ التي أُعيد فتحها. الأرقام لقطة ثابتة للحصة كلها وليست دخل طالب؛ فلتر الطالب يختار الحصص المرتبطة به. عدد عمليات التحصيل والبيع يخص الدفعات الأصلية؛ صافي التحصيل المقبوض بعد الاستردادات محفوظ في التقفيلة؛ المستحق بعد الخصم قد يكون أكبر من المقبوض. المديونية عند التسجيل لا تمثل المتبقي اليوم، والسداد اللاحق خارج الحصة لا يعدل التقفيلة القديمة. فئة بلا خصم لا تثبت سدادًا كاملًا. الفرق يقارن النقدي الفعلي بالنقدي المسجل بعد رد المبالغ النقدية فقط. فئات الطلبة لقطة محفوظة؛ الإعفاء 100٪ منفصل عن الحصة المجانية. عدد الطلبة داخل كل فئة مميز، وقد يتكرر الطالب بين فئة الدفع والحضور؛ لا تُجمع الفئات للحصول على عدد طلبة فريد. التقفيلات القديمة دون تفاصيل تظهر كغير متوفرة، ولا تُستنتج من بيانات اليوم.',
    );
  }

  bool _isNewCollectionCategory(SessionStudentCategory category) =>
      category.kind == SessionStudentCategoryKind.single ||
      category.kind == SessionStudentCategoryKind.package;

  String _categoryBreakdown(
    SessionClosing closing,
    bool Function(SessionStudentCategory) include,
  ) {
    final categories = closing.summary.studentCategories;
    if (categories == null) return 'غير متوفرة في هذه التقفيلة';
    final matches = categories.where(include);
    if (matches.isEmpty) return '0 طالب';
    return matches
        .map(
          (category) =>
              '${category.label}: ${category.studentCount} طالب، ${category.operationCount} عملية، المحصل للوحدة ${reportAmount(category.unitAmount)} ج',
        )
        .join('؛ ');
  }

  Map<String, String> _closingTotals(List<SessionClosing> matches) {
    final active = matches
        .where((closing) => activeClosingIds.contains(closing.id))
        .toList();
    return {
      'التقفيلات': '${matches.length}',
      'التقفيلات السارية': '${active.length}',
      'تقفيلات دون تفاصيل فئات':
          '${matches.where((closing) => closing.summary.studentCategories == null).length}',
      'الاستردادات المثبتة':
          '${reportAmount(active.fold<int>(0, (sum, closing) => sum + closing.summary.refundAmount))} ج',
      'التحصيل المثبت':
          '${reportAmount(active.fold<int>(0, (sum, closing) => sum + closing.summary.totalCollected))} ج',
      'النقدي الفعلي':
          '${reportAmount(active.fold<int>(0, (sum, closing) => sum + closing.actualCash))} ج',
      'مديونية عند التسجيل — النسخ المتوفرة':
          '${reportAmount(active.fold<int>(0, (sum, closing) => sum + (closing.summary.debtAmount ?? 0)))} ج',
      'سداد المديونيات المثبت — النسخ المتوفرة':
          '${reportAmount(active.fold<int>(0, (sum, closing) => sum + (closing.summary.debtSettlementAmount ?? 0)))} ج',
      'تقفيلات سارية دون تفاصيل المديونية':
          '${active.where((closing) => closing.summary.debtAmount == null || closing.summary.debtSettlementAmount == null).length}',
      'فرق النقدي':
          '${reportAmount(active.fold<int>(0, (sum, closing) => sum + closing.difference))} ج',
    };
  }

  CenterReportData _closingCategoryReport(
    List<SessionClosing> matches,
  ) => CenterReportData(
    title: 'فئات الطلبة وقت التقفيل',
    columns: const [
      'وقت التقفيلة',
      'حالة التقفيلة',
      'الحصة',
      'المجموعة',
      'فئة الطلبة',
      'نسبة الخصم ٪',
      'المحصل للوحدة (جنيه مصري)',
      'عدد الطلبة داخل الفئة',
      'عدد عمليات الدفع',
      'الموظف',
      'رقم التقفيلة',
    ],
    rows: [
      for (final closing in matches)
        if (closing.summary.studentCategories == null)
          <Object?>[
            _timeLabel(closing.createdAt),
            activeClosingIds.contains(closing.id)
                ? 'سارية'
                : 'أُعيد فتحها — تاريخية',
            sessions[closing.sessionId]!.number,
            _groupLabel(sessions[closing.sessionId]!.groupId),
            'تفاصيل الفئات غير محفوظة لهذه التقفيلة',
            null,
            null,
            null,
            null,
            _staffLabel(closing.staffId),
            closing.id,
          ]
        else
          for (final category in closing.summary.studentCategories!)
            <Object?>[
              _timeLabel(closing.createdAt),
              activeClosingIds.contains(closing.id)
                  ? 'سارية'
                  : 'أُعيد فتحها — تاريخية',
              sessions[closing.sessionId]!.number,
              _groupLabel(sessions[closing.sessionId]!.groupId),
              category.label,
              category.discountPercent,
              reportAmount(category.unitAmount),
              category.studentCount,
              category.operationCount,
              _staffLabel(closing.staffId),
              closing.id,
            ],
    ],
    summary: _closingTotals(matches),
    caption:
        'الفترة حسب وقت التقفيلة. فلتر الطالب يختار الحصص المرتبطة به؛ الفئات تخص الحصة كلها وليست الطالب المختار وحده. الفئات من اللقطة المحفوظة وقت التقفيل، والمبالغ هي المقبوض عند التسجيل؛ فئة بلا خصم لا تعني سدادًا كاملًا. المديونية والسداد اللاحق يظلان منفصلين عن نسبة الخصم. الإعفاء 100٪ ليس حصة مجانية؛ نسبة خصم الرصيد السابق محفوظة من عملية شراء الباقة الأصلية؛ لا تعكس خصم الطالب اليوم ولا تضيف تحصيلًا جديدًا. الرصيد السابق والتعويض والمجاني والغياب دون عملية تحصيل جديدة. الطلبة مميزون داخل كل فئة فقط، وقد تتداخل فئات الدفع والحضور؛ لا تجمعها لعدد فريد. عدد العمليات يختلف عن عدد الطلبة. التقفيلات المعاد فتحها تاريخية ومستبعدة من الإجماليات؛ لا تُستنتج تفاصيل التقفيلات القديمة.',
  );
}

class _ReportMoneyMovement {
  const _ReportMoneyMovement({
    required this.time,
    required this.amount,
    required this.method,
    required this.sessionId,
    required this.kind,
    required this.cells,
  });
  final DateTime time;
  final int amount;
  final String method;
  final String? sessionId;
  final DebtKind kind;
  final List<Object?> cells;
}
