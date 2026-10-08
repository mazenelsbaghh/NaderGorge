import '../domain/models.dart';
import 'copy_on_write_list.dart';

part 'center_state_patch.dart';

const centerStateImmutableRecordSections = {
  'catalogs',
  'studyMonths',
  'students',
  'sessions',
  'closings',
  'packages',
  'attendances',
  'payments',
  'centerFees',
  'debtSettlements',
  'academics',
  'academicActivities',
  'audit',
  'staff',
  'reviews',
  'paymentChecks',
  'corrections',
  'refunds',
  'cardPayments',
  'cardReceipts',
};

typedef StateRecordEncoder =
    Object Function<T extends Object>(
      String section,
      List<T> records,
      Map<String, dynamic> Function(T) toJson,
    );

Object _jsonRecords<T extends Object>(
  String section,
  List<T> records,
  Map<String, dynamic> Function(T) toJson,
) => records.map(toJson).toList();

class CenterState {
  String? installationSeedId;
  List<String> appliedDataRepairs = [];
  int defaultMonthPriceVersion = 0;
  List<CatalogEntry> catalogs = [];
  List<StudyGroup> groups = [];
  List<StudyMonth> studyMonths = [];
  List<Student> students = [];
  List<LessonSession> sessions = [];
  List<PrepaidPackage> packages = [];
  List<AttendanceRecord> attendances = [];
  List<PaymentRecord> payments = [];
  List<CenterFeeRecord> centerFees = [];
  List<DebtSettlement> debtSettlements = [];
  List<AcademicRecord> academics = [];
  List<AcademicActivity> academicActivities = [];
  List<AuditRecord> audit = [];
  List<StaffUser> staff = [];
  List<PaymentReview> reviews = [];
  List<SessionClosing> closings = [];
  List<PaymentCheck> paymentChecks = [];
  List<CorrectionRecord> corrections = [];
  List<RefundRecord> refunds = [];
  CenterCardSettings cardSettings = const CenterCardSettings();
  List<StudentCardPayment> cardPayments = [];
  List<StudentCardReceipt> cardReceipts = [];
  Map<String, Map<String, String>> credentials = {};
  Map<String, String> enrollments = {};

  Map<String, dynamic> toJson() => mapJsonFields(_jsonRecords);

  // One schema serves ordinary JSON maps and the incremental snapshot encoder.
  Map<String, dynamic> mapJsonFields(
    StateRecordEncoder encodeRecords, {
    bool includeEmptyFields = false,
  }) => {
    'schemaVersion': paymentChecks.any((check) => check.amount != null)
        ? 11
        : studyMonths.isNotEmpty ||
              sessions.any((e) => e.preparedLessonId != null) ||
              academicActivities.any((e) => e.preparedLessonId != null)
        ? 10
        : students.any((e) => e.centerOnly || e.centerFeeAmount != 1500) ||
              attendances.any((e) => e.centerFeeOnly) ||
              centerFees.any(
                (e) =>
                    e.method != 'نقدي' ||
                    e.staffId != null ||
                    e.originalFeeId != null,
              ) ||
              closings.any(
                (e) => e.summary.coverageClassificationVersion != null,
              )
        ? 9
        : appliedDataRepairs.isNotEmpty || centerFees.isNotEmpty
        ? 8
        : sessions.any((e) => e.monthNumber != 1)
        ? 7
        : defaultMonthPriceVersion > 0 ||
              sessions.any((e) => e.startedAt != null || e.startedBy != null)
        ? 6
        : groups.any((e) => e.monthPlans.isNotEmpty) ||
              students.any((e) => e.isSuspended || e.suspendedAt != null) ||
              packages.any((e) => e.monthPlanId != null)
        ? 5
        : debtSettlements.isNotEmpty ||
              payments.any((e) => e.paidAmount != null) ||
              cardPayments.any((e) => e.paidAmount != null) ||
              students.any((e) => e.twinStudentId != null)
        ? 4
        : academicActivities.isEmpty
        ? 2
        : 3,
    if (includeEmptyFields || installationSeedId != null)
      'installationSeedId': installationSeedId,
    if (includeEmptyFields || appliedDataRepairs.isNotEmpty)
      'appliedDataRepairs': appliedDataRepairs,
    if (includeEmptyFields || defaultMonthPriceVersion > 0)
      'defaultMonthPriceVersion': defaultMonthPriceVersion,
    'catalogs': encodeRecords('catalogs', catalogs, (e) => e.toJson()),
    'groups': encodeRecords('groups', groups, (e) => e.toJson()),
    if (includeEmptyFields || studyMonths.isNotEmpty)
      'studyMonths': encodeRecords(
        'studyMonths',
        studyMonths,
        (e) => e.toJson(),
      ),
    'students': encodeRecords('students', students, (e) => e.toJson()),
    'sessions': encodeRecords('sessions', sessions, (e) => e.toJson()),
    'packages': encodeRecords('packages', packages, (e) => e.toJson()),
    'attendances': encodeRecords('attendances', attendances, (e) => e.toJson()),
    'payments': encodeRecords('payments', payments, (e) => e.toJson()),
    if (includeEmptyFields || centerFees.isNotEmpty)
      'centerFees': encodeRecords('centerFees', centerFees, (e) => e.toJson()),
    if (includeEmptyFields || debtSettlements.isNotEmpty)
      'debtSettlements': encodeRecords(
        'debtSettlements',
        debtSettlements,
        (e) => e.toJson(),
      ),
    'academics': encodeRecords('academics', academics, (e) => e.toJson()),
    'academicActivities': encodeRecords(
      'academicActivities',
      academicActivities,
      (e) => e.toJson(),
    ),
    'audit': encodeRecords('audit', audit, (e) => e.toJson()),
    'staff': encodeRecords('staff', staff, (e) => e.toJson()),
    'reviews': encodeRecords('reviews', reviews, (e) => e.toJson()),
    'closings': encodeRecords('closings', closings, (e) => e.toJson()),
    'paymentChecks': encodeRecords(
      'paymentChecks',
      paymentChecks,
      (e) => e.toJson(),
    ),
    'corrections': encodeRecords('corrections', corrections, (e) => e.toJson()),
    'refunds': encodeRecords('refunds', refunds, (e) => e.toJson()),
    'cardSettings': cardSettings.toJson(),
    'cardPayments': encodeRecords(
      'cardPayments',
      cardPayments,
      (e) => e.toJson(),
    ),
    'cardReceipts': encodeRecords(
      'cardReceipts',
      cardReceipts,
      (e) => e.toJson(),
    ),
    'credentials': credentials,
    'enrollments': enrollments,
  };

  factory CenterState.fromJson(Map<String, dynamic> json) =>
      CenterState._fromFields(json);

  factory CenterState._fromFields(Map<String, dynamic> json) {
    if (json['schemaVersion'] != 1 &&
        json['schemaVersion'] != 2 &&
        json['schemaVersion'] != 3 &&
        json['schemaVersion'] != 4 &&
        json['schemaVersion'] != 5 &&
        json['schemaVersion'] != 6 &&
        json['schemaVersion'] != 7 &&
        json['schemaVersion'] != 8 &&
        json['schemaVersion'] != 9 &&
        json['schemaVersion'] != 10 &&
        json['schemaVersion'] != 11) {
      throw const CenterException(
        'إصدار ملف البيانات غير مدعوم. لم تتغير بياناتك.',
      );
    }
    final defaultMonthPriceVersion =
        json['defaultMonthPriceVersion'] as int? ?? 0;
    final appliedDataRepairs = List<String>.from(
      json['appliedDataRepairs'] as List? ?? const [],
    );
    if (appliedDataRepairs.any((id) => id.trim().isEmpty) ||
        appliedDataRepairs.toSet().length != appliedDataRepairs.length) {
      throw const CenterException(
        'سجل تصحيح البيانات يحتوي معرّفًا فارغًا أو مكررًا.',
      );
    }
    if (defaultMonthPriceVersion < 0 || defaultMonthPriceVersion > 1) {
      throw const CenterException(
        'إصدار إعداد سعر الشهر غير مدعوم. لم تتغير بياناتك.',
      );
    }
    bool hasRows(String key) => switch (json[key]) {
      _RetainedStateRows rows => rows.length > 0,
      List rows => rows.isNotEmpty,
      null => false,
      _ => throw const FormatException('Invalid state section'),
    };
    if (json['schemaVersion'] == 1 &&
        [
          'reviews',
          'closings',
          'paymentChecks',
          'corrections',
          'refunds',
          'cardPayments',
          'cardReceipts',
          'academicActivities',
        ].any(hasRows)) {
      throw const CenterException(
        'إصدار البيانات لا يتوافق مع المراجعات والتقفيلات الموجودة.',
      );
    }
    List<T> rows<T>(String key, T Function(Map<String, dynamic>) parse) {
      final supplied = json[key];
      if (supplied is _RetainedStateRows) return supplied.decode(parse);
      return (supplied as List)
          .map((e) => parse(Map<String, dynamic>.from(e as Map)))
          .toList();
    }

    return CenterState()
      ..installationSeedId = json['installationSeedId'] as String?
      ..appliedDataRepairs = appliedDataRepairs
      ..defaultMonthPriceVersion = defaultMonthPriceVersion
      ..catalogs = rows('catalogs', CatalogEntry.fromJson)
      ..groups = rows('groups', StudyGroup.fromJson)
      ..studyMonths = json['studyMonths'] == null
          ? []
          : rows('studyMonths', StudyMonth.fromJson)
      ..students = rows('students', Student.fromJson)
      ..sessions = rows('sessions', LessonSession.fromJson)
      ..packages = rows('packages', PrepaidPackage.fromJson)
      ..attendances = rows('attendances', AttendanceRecord.fromJson)
      ..payments = rows('payments', PaymentRecord.fromJson)
      ..centerFees = json['centerFees'] == null
          ? []
          : rows('centerFees', CenterFeeRecord.fromJson)
      ..debtSettlements = json['debtSettlements'] == null
          ? []
          : rows('debtSettlements', DebtSettlement.fromJson)
      ..academics = rows('academics', AcademicRecord.fromJson)
      ..academicActivities = json['academicActivities'] == null
          ? []
          : rows('academicActivities', AcademicActivity.fromJson)
      ..audit = rows('audit', AuditRecord.fromJson)
      ..staff = rows('staff', StaffUser.fromJson)
      ..reviews = json['schemaVersion'] == 1
          ? []
          : rows('reviews', PaymentReview.fromJson)
      ..closings = json['schemaVersion'] == 1
          ? []
          : rows('closings', SessionClosing.fromJson)
      ..paymentChecks =
          json['schemaVersion'] == 1 || json['paymentChecks'] == null
          ? []
          : rows('paymentChecks', PaymentCheck.fromJson)
      ..corrections = json['schemaVersion'] == 1 || json['corrections'] == null
          ? []
          : rows('corrections', CorrectionRecord.fromJson)
      ..refunds = json['schemaVersion'] == 1 || json['refunds'] == null
          ? []
          : rows('refunds', RefundRecord.fromJson)
      ..cardSettings = json['cardSettings'] == null
          ? const CenterCardSettings()
          : CenterCardSettings.fromJson(
              Map<String, dynamic>.from(json['cardSettings'] as Map),
            )
      ..cardPayments = json['cardPayments'] == null
          ? []
          : rows('cardPayments', StudentCardPayment.fromJson)
      ..cardReceipts = json['cardReceipts'] == null
          ? []
          : rows('cardReceipts', StudentCardReceipt.fromJson)
      ..credentials = (json['credentials'] as Map).map(
        (key, value) =>
            MapEntry(key as String, Map<String, String>.from(value as Map)),
      )
      ..enrollments = Map<String, String>.from(json['enrollments'] as Map);
  }

  CenterState();

  // Entities are immutable; isolate mutable containers for transaction rollback.
  CenterState copyForBackup() => copyForMutation()
    ..groups = groups
        .map((group) => StudyGroup.fromJson(group.toJson()))
        .toList();

  CenterState copyForMutation() => CenterState()
    ..installationSeedId = installationSeedId
    ..appliedDataRepairs = forkRows(appliedDataRepairs)
    ..defaultMonthPriceVersion = defaultMonthPriceVersion
    ..catalogs = forkRows(catalogs)
    ..groups = List.of(groups)
    ..studyMonths = forkRows(studyMonths)
    ..students = forkRows(students)
    ..sessions = forkRows(sessions)
    ..packages = forkRows(packages)
    ..attendances = forkRows(attendances)
    ..payments = forkRows(payments)
    ..centerFees = forkRows(centerFees)
    ..debtSettlements = forkRows(debtSettlements)
    ..academics = forkRows(academics)
    ..academicActivities = forkRows(academicActivities)
    ..audit = forkRows(audit)
    ..staff = forkRows(staff)
    ..reviews = forkRows(reviews)
    ..closings = forkRows(closings)
    ..paymentChecks = forkRows(paymentChecks)
    ..corrections = forkRows(corrections)
    ..refunds = forkRows(refunds)
    ..cardSettings = cardSettings
    ..cardPayments = forkRows(cardPayments)
    ..cardReceipts = forkRows(cardReceipts)
    ..credentials = {
      for (final credential in credentials.entries)
        credential.key: Map.of(credential.value),
    }
    ..enrollments = Map.of(enrollments);
}
