enum CatalogKind { subject, center, grade }

enum SessionKind { counted, free, extra }

enum SessionStatus { open, closed, canceled }

enum EntryMode { package, single, makeup }

enum AttendanceStatus { present, absent, makeup }

enum HomeworkStatus { notReviewed, complete, incomplete, missing, exempt }

enum AcademicActivityKind { exam, homework }

enum StaffRole { admin, cashier, assistant }

enum StudentPaymentStatus { paidSingle, paidPackage, free, makeup, notPaid }

enum RecordCancellationMode { recordOnly, recordAndRelated }

enum DebtKind { lesson, card }

/// Historical center fees remain independent of teacher payments and credit.
class CenterFeeRecord {
  const CenterFeeRecord({
    this.id = '',
    required this.studentId,
    required this.sessionId,
    required this.amount,
    required this.paidAmount,
    required this.recordedAt,
    this.recordedAtKnown = true,
    this.sourceNote = '',
    this.method = 'نقدي',
    this.staffId,
    this.originalFeeId,
  });
  final String id, studentId, sessionId, sourceNote, method;
  final String? staffId, originalFeeId;
  final int amount, paidAmount;
  final DateTime recordedAt;
  final bool recordedAtKnown;
  int get remainingAmount => amount - paidAmount;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'sessionId': sessionId,
    'amount': amount,
    'paidAmount': paidAmount,
    'recordedAt': recordedAt.toIso8601String(),
    if (!recordedAtKnown) 'recordedAtKnown': false,
    'sourceNote': sourceNote,
    if (method != 'نقدي') 'method': method,
    if (staffId != null) 'staffId': staffId,
    if (originalFeeId != null) 'originalFeeId': originalFeeId,
  };
  factory CenterFeeRecord.fromJson(Map<String, dynamic> value) =>
      CenterFeeRecord(
        id: value['id'] as String,
        studentId: value['studentId'] as String,
        sessionId: value['sessionId'] as String,
        amount: value['amount'] as int,
        paidAmount: value['paidAmount'] as int,
        recordedAt: DateTime.parse(value['recordedAt'] as String),
        recordedAtKnown: value['recordedAtKnown'] as bool? ?? true,
        sourceNote: value['sourceNote'] as String? ?? '',
        method: value['method'] as String? ?? 'نقدي',
        staffId: value['staffId'] as String?,
        originalFeeId: value['originalFeeId'] as String?,
      );
}

class DebtSettlement {
  const DebtSettlement({
    this.id = '',
    required this.studentId,
    required this.paymentId,
    required this.kind,
    required this.amount,
    this.method = 'نقدي',
    this.sessionId,
    this.notes = '',
    required this.createdAt,
    required this.staffId,
  });
  final String id, studentId, paymentId, method, notes, staffId;
  final DebtKind kind;
  final int amount;
  final String? sessionId;
  final DateTime createdAt;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'paymentId': paymentId,
    'kind': kind.name,
    'amount': amount,
    'method': method,
    'sessionId': sessionId,
    'notes': notes,
    'createdAt': createdAt.toIso8601String(),
    'staffId': staffId,
  };
  factory DebtSettlement.fromJson(Map<String, dynamic> value) => DebtSettlement(
    id: value['id'] as String,
    studentId: value['studentId'] as String,
    paymentId: value['paymentId'] as String,
    kind: DebtKind.values.byName(value['kind'] as String),
    amount: value['amount'] as int,
    method: value['method'] as String,
    sessionId: value['sessionId'] as String?,
    notes: value['notes'] as String? ?? '',
    createdAt: DateTime.parse(value['createdAt'] as String),
    staffId: value['staffId'] as String,
  );
}

/// A live projection of the original receivable and its immutable collections.
class StudentDebt {
  const StudentDebt({
    required this.paymentId,
    required this.kind,
    required this.studentId,
    this.groupId,
    this.sessionId,
    required this.description,
    required this.dueAmount,
    required this.collectedAmount,
    required this.createdAt,
  });
  final String paymentId, studentId, description;
  final DebtKind kind;
  final String? groupId, sessionId;
  final int dueAmount, collectedAmount;
  final DateTime createdAt;
  int get remainingAmount => dueAmount - collectedAmount;
}

enum CorrectionAction {
  entryReversed,
  entryCorrected,
  absencePresent,
  paymentMethod,
  paymentCanceled,
  packageRefund,
  closingReopened,
}

class CorrectionRecord {
  const CorrectionRecord({
    this.id = '',
    required this.action,
    this.studentId,
    this.sessionId,
    this.attendanceId,
    this.paymentId,
    this.packageId,
    this.replacementAttendanceId,
    this.closingId,
    this.oldMethod,
    this.newMethod,
    this.voidsPayment = false,
    this.voidsPackage = false,
    required this.reason,
    required this.staffId,
    required this.createdAt,
  });
  final String id, reason, staffId;
  final CorrectionAction action;
  final String? studentId,
      sessionId,
      attendanceId,
      paymentId,
      packageId,
      replacementAttendanceId,
      closingId,
      oldMethod,
      newMethod;
  final bool voidsPayment, voidsPackage;
  final DateTime createdAt;
  Map<String, dynamic> toJson() => {
    'id': id,
    'action': action.name,
    'studentId': studentId,
    'sessionId': sessionId,
    'attendanceId': attendanceId,
    'paymentId': paymentId,
    'packageId': packageId,
    'replacementAttendanceId': replacementAttendanceId,
    'closingId': closingId,
    'oldMethod': oldMethod,
    'newMethod': newMethod,
    'voidsPayment': voidsPayment,
    'voidsPackage': voidsPackage,
    'reason': reason,
    'staffId': staffId,
    'createdAt': createdAt.toIso8601String(),
  };
  factory CorrectionRecord.fromJson(Map<String, dynamic> j) => CorrectionRecord(
    id: j['id'] as String,
    action: CorrectionAction.values.byName(j['action'] as String),
    studentId: j['studentId'] as String?,
    sessionId: j['sessionId'] as String?,
    attendanceId: j['attendanceId'] as String?,
    paymentId: j['paymentId'] as String?,
    packageId: j['packageId'] as String?,
    replacementAttendanceId: j['replacementAttendanceId'] as String?,
    closingId: j['closingId'] as String?,
    oldMethod: j['oldMethod'] as String?,
    newMethod: j['newMethod'] as String?,
    voidsPayment: j['voidsPayment'] as bool,
    voidsPackage: j['voidsPackage'] as bool,
    reason: j['reason'] as String,
    staffId: j['staffId'] as String,
    createdAt: DateTime.parse(j['createdAt'] as String),
  );
}

class RefundRecord {
  const RefundRecord({
    this.id = '',
    required this.correctionId,
    required this.paymentId,
    required this.studentId,
    required this.groupId,
    this.sessionId,
    this.packageId,
    required this.amount,
    required this.method,
    required this.reason,
    required this.staffId,
    required this.createdAt,
  });
  final String id,
      correctionId,
      paymentId,
      studentId,
      groupId,
      method,
      reason,
      staffId;
  final String? sessionId, packageId;
  final int amount;
  final DateTime createdAt;
  Map<String, dynamic> toJson() => {
    'id': id,
    'correctionId': correctionId,
    'paymentId': paymentId,
    'studentId': studentId,
    'groupId': groupId,
    'sessionId': sessionId,
    'packageId': packageId,
    'amount': amount,
    'method': method,
    'reason': reason,
    'staffId': staffId,
    'createdAt': createdAt.toIso8601String(),
  };
  factory RefundRecord.fromJson(Map<String, dynamic> j) => RefundRecord(
    id: j['id'] as String,
    correctionId: j['correctionId'] as String,
    paymentId: j['paymentId'] as String,
    studentId: j['studentId'] as String,
    groupId: j['groupId'] as String,
    sessionId: j['sessionId'] as String?,
    packageId: j['packageId'] as String?,
    amount: j['amount'] as int,
    method: j['method'] as String,
    reason: j['reason'] as String,
    staffId: j['staffId'] as String,
    createdAt: DateTime.parse(j['createdAt'] as String),
  );
}

class PaymentStatusResult {
  const PaymentStatusResult({
    required this.status,
    this.paymentId,
    this.packageId,
    required this.detail,
    this.debtAmount = 0,
  });
  final StudentPaymentStatus status;
  final String? paymentId, packageId;
  final String detail;
  final int debtAmount;
}

class PaymentCheck {
  const PaymentCheck({
    this.id = '',
    required this.studentId,
    required this.sessionId,
    required this.status,
    this.paymentId,
    this.packageId,
    required this.staffId,
    required this.checkedAt,
    this.amount,
  });
  final String id, studentId, sessionId, staffId;
  final StudentPaymentStatus status;
  final String? paymentId, packageId;
  final DateTime checkedAt;
  final int? amount;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'sessionId': sessionId,
    'status': status.name,
    'paymentId': paymentId,
    'packageId': packageId,
    'staffId': staffId,
    'checkedAt': checkedAt.toIso8601String(),
    if (amount != null) 'amount': amount,
  };
  factory PaymentCheck.fromJson(Map<String, dynamic> json) => PaymentCheck(
    id: json['id'] as String,
    studentId: json['studentId'] as String,
    sessionId: json['sessionId'] as String,
    status: StudentPaymentStatus.values.byName(json['status'] as String),
    paymentId: json['paymentId'] as String?,
    packageId: json['packageId'] as String?,
    staffId: json['staffId'] as String,
    checkedAt: DateTime.parse(json['checkedAt'] as String),
    amount: json['amount'] as int?,
  );
}

class CenterException implements Exception {
  const CenterException(
    this.message, {
    this.cause,
    this.stackTrace,
    this.diagnosticCode,
  });

  /// Reviewed numeric reason only; never include student data in diagnostics.
  final int? diagnosticCode;
  final String message;
  final Object? cause;
  final StackTrace? stackTrace;
  @override
  String toString() => message;
}

class CatalogEntry {
  const CatalogEntry({
    this.id = '',
    required this.name,
    required this.kind,
    this.region,
  });
  final String id;
  final String name;
  final CatalogKind kind;
  final String? region;
  // Older installations identify Cairo in the center name. Persist the resolved
  // region on serialization so a subsequent rename cannot move its students.
  bool get isCairo =>
      kind == CatalogKind.center &&
      (region == 'cairo' ||
          (region == null &&
              (name.contains('القاهرة') || name.contains('القاهره'))));
  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'kind': kind.name,
    if (kind == CatalogKind.center) 'region': isCairo ? 'cairo' : 'alexandria',
  };
  factory CatalogEntry.fromJson(Map<String, dynamic> value) => CatalogEntry(
    id: value['id'] as String,
    name: value['name'] as String,
    kind: CatalogKind.values.byName(value['kind'] as String),
    region: value['region'] as String?,
  );
  CatalogEntry copyWith({
    String? id,
    String? name,
    CatalogKind? kind,
    String? region,
  }) => CatalogEntry(
    id: id ?? this.id,
    name: name ?? this.name,
    kind: kind ?? this.kind,
    region: region ?? this.region ?? (isCairo ? 'cairo' : 'alexandria'),
  );
}

class PreparedLesson {
  const PreparedLesson({
    this.id = '',
    this.name = '',
    required this.number,
    this.kind = SessionKind.counted,
    this.extraPrice = 0,
  });
  final String id, name;
  final int number, extraPrice;
  final SessionKind kind;
  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'number': number,
    'kind': kind.name,
    'extraPrice': extraPrice,
  };
  factory PreparedLesson.fromJson(Map<String, dynamic> value) => PreparedLesson(
    id: value['id'] as String,
    name: value['name'] as String? ?? '',
    number: value['number'] as int,
    kind: SessionKind.values.byName(value['kind'] as String? ?? 'counted'),
    extraPrice: value['extraPrice'] as int? ?? 0,
  );
  PreparedLesson copyWith({
    String? id,
    String? name,
    int? number,
    SessionKind? kind,
    int? extraPrice,
  }) => PreparedLesson(
    id: id ?? this.id,
    name: name ?? this.name,
    number: number ?? this.number,
    kind: kind ?? this.kind,
    extraPrice: extraPrice ?? this.extraPrice,
  );
}

class StudyMonth {
  StudyMonth({
    this.id = '',
    required this.name,
    this.number = 0,
    this.price = 21000,
    required List<PreparedLesson> lessons,
  }) : lessons = List.unmodifiable(lessons);
  final String id, name;
  final int number, price;
  final List<PreparedLesson> lessons;
  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'number': number,
    'price': price,
    'lessons': lessons.map((e) => e.toJson()).toList(),
  };
  factory StudyMonth.fromJson(Map<String, dynamic> value) => StudyMonth(
    id: value['id'] as String,
    name: value['name'] as String,
    number: value['number'] as int,
    price: value['price'] as int,
    lessons: (value['lessons'] as List)
        .map(
          (e) => PreparedLesson.fromJson(Map<String, dynamic>.from(e as Map)),
        )
        .toList(),
  );
  StudyMonth copyWith({
    String? id,
    String? name,
    int? number,
    int? price,
    List<PreparedLesson>? lessons,
  }) => StudyMonth(
    id: id ?? this.id,
    name: name ?? this.name,
    number: number ?? this.number,
    price: price ?? this.price,
    lessons: lessons ?? this.lessons,
  );
}

class GroupMonthPlan {
  const GroupMonthPlan({
    this.id = '',
    required this.name,
    required this.sessions,
    required this.price,
  });
  final String id, name;
  final int sessions, price;
  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'sessions': sessions,
    'price': price,
  };
  factory GroupMonthPlan.fromJson(Map<String, dynamic> json) => GroupMonthPlan(
    id: json['id'] as String,
    name: json['name'] as String,
    sessions: json['sessions'] as int,
    price: json['price'] as int,
  );
  GroupMonthPlan copyWith({
    String? id,
    String? name,
    int? sessions,
    int? price,
  }) => GroupMonthPlan(
    id: id ?? this.id,
    name: name ?? this.name,
    sessions: sessions ?? this.sessions,
    price: price ?? this.price,
  );
  @override
  bool operator ==(Object other) =>
      other is GroupMonthPlan &&
      id == other.id &&
      name == other.name &&
      sessions == other.sessions &&
      price == other.price;
  @override
  int get hashCode => Object.hash(id, name, sessions, price);
}

class StudyGroup {
  const StudyGroup({
    this.id = '',
    required this.name,
    required this.subjectId,
    required this.centerId,
    required this.gradeId,
    this.schedule = '',
    this.sessionPrice = 0,
    this.packagePrice = 21000,
    this.twoSessionPrice,
    this.threeSessionPrice,
    this.priceConfigured = true,
    this.monthPlans = const [],
  });
  final String id;
  final String name;
  final String subjectId;
  final String centerId;
  final String gradeId;
  final String schedule;
  final int sessionPrice;
  final int packagePrice;
  final int? twoSessionPrice, threeSessionPrice;
  final bool priceConfigured;
  final List<GroupMonthPlan> monthPlans;
  List<GroupMonthPlan> get effectiveMonthPlans => List.unmodifiable(
    monthPlans.isEmpty
        ? const [
            GroupMonthPlan(
              id: 'legacy-month4',
              name: 'شهر',
              sessions: 4,
              price: 21000,
            ),
          ]
        : monthPlans,
  );

  /// Missing smaller-package prices are not inferred from a historical 4-pack.
  int? packageAmountFor(int sessions) => switch (sessions) {
    2 => twoSessionPrice,
    3 => threeSessionPrice,
    4 => packagePrice,
    _ => null,
  };
  Map<String, dynamic> toJson() => {
    'id': id,
    'name': name,
    'subjectId': subjectId,
    'centerId': centerId,
    'gradeId': gradeId,
    'schedule': schedule,
    'sessionPrice': sessionPrice,
    'packagePrice': packagePrice,
    if (!priceConfigured) 'priceConfigured': false,
    if (twoSessionPrice != null) 'twoSessionPrice': twoSessionPrice,
    if (threeSessionPrice != null) 'threeSessionPrice': threeSessionPrice,
    if (monthPlans.isNotEmpty)
      'monthPlans': monthPlans.map((e) => e.toJson()).toList(),
  };
  factory StudyGroup.fromJson(Map<String, dynamic> value) => StudyGroup(
    id: value['id'] as String,
    name: value['name'] as String,
    subjectId: value['subjectId'] as String,
    centerId: value['centerId'] as String,
    gradeId: value['gradeId'] as String,
    schedule: value['schedule'] as String,
    sessionPrice: value['sessionPrice'] as int,
    packagePrice: value['packagePrice'] as int,
    twoSessionPrice: value['twoSessionPrice'] as int?,
    threeSessionPrice: value['threeSessionPrice'] as int?,
    priceConfigured: value['priceConfigured'] as bool? ?? true,
    monthPlans: List.unmodifiable(
      (value['monthPlans'] as List? ?? const []).map(
        (e) => GroupMonthPlan.fromJson(Map<String, dynamic>.from(e as Map)),
      ),
    ),
  );
  StudyGroup copyWith({
    String? id,
    String? name,
    String? subjectId,
    String? centerId,
    String? gradeId,
    String? schedule,
    int? sessionPrice,
    int? packagePrice,
    int? twoSessionPrice,
    int? threeSessionPrice,
    bool? priceConfigured,
    List<GroupMonthPlan>? monthPlans,
  }) => StudyGroup(
    id: id ?? this.id,
    name: name ?? this.name,
    subjectId: subjectId ?? this.subjectId,
    centerId: centerId ?? this.centerId,
    gradeId: gradeId ?? this.gradeId,
    schedule: schedule ?? this.schedule,
    sessionPrice: sessionPrice ?? this.sessionPrice,
    packagePrice: packagePrice ?? this.packagePrice,
    twoSessionPrice: twoSessionPrice ?? this.twoSessionPrice,
    threeSessionPrice: threeSessionPrice ?? this.threeSessionPrice,
    priceConfigured: priceConfigured ?? this.priceConfigured,
    monthPlans: List.unmodifiable(monthPlans ?? this.monthPlans),
  );
}

class Student {
  Student({
    this.id = '',
    this.code = '',
    this.barcode = '',
    required this.name,
    this.phone = '',
    this.guardianPhone = '',
    this.twinStudentId,
    this.isSuspended = false,
    this.suspensionReason = '',
    this.suspendedAt,
    this.suspendedBy,
    List<String> groupIds = const [],
    this.discountPercent = 0,
    this.discountNeedsReview = false,
    this.centerOnly = false,
    this.centerFeeEnabled = false,
    this.packageMember = false,
    this.centerFeeAmount = 1500,
    this.notes = '',
    required this.createdAt,
    this.createdAtKnown = true,
  }) : groupIds = List.unmodifiable(groupIds);
  final String id;
  final String code;

  /// Original card identifier; imported profiles display its last five digits.
  final String barcode;
  final String name;
  final String phone;
  final String guardianPhone;
  final String? twinStudentId;
  final bool isSuspended;
  final String suspensionReason;
  final DateTime? suspendedAt;
  final String? suspendedBy;
  final List<String> groupIds;
  final num discountPercent;
  final bool discountNeedsReview;
  final bool centerOnly;
  final bool centerFeeEnabled;
  final bool packageMember;
  final int centerFeeAmount;
  final String notes;
  final DateTime createdAt;
  final bool createdAtKnown;
  Map<String, dynamic> toJson() => {
    'id': id,
    if (packageMember) 'packageMember': true,
    'code': code,
    if (barcode.isNotEmpty) 'barcode': barcode,
    'name': name,
    'phone': phone,
    'guardianPhone': guardianPhone,
    if (twinStudentId != null) 'twinStudentId': twinStudentId,
    if (isSuspended) 'isSuspended': true,
    if (suspensionReason.isNotEmpty) 'suspensionReason': suspensionReason,
    if (suspendedAt != null) 'suspendedAt': suspendedAt!.toIso8601String(),
    if (suspendedBy != null) 'suspendedBy': suspendedBy,
    'groupIds': groupIds,
    'discountPercent': discountPercent,
    if (discountNeedsReview) 'discountNeedsReview': true,
    if (centerOnly) 'centerOnly': true,
    if (centerFeeEnabled) 'centerFeeEnabled': true,
    if (centerOnly || centerFeeAmount != 1500)
      'centerFeeAmount': centerFeeAmount,
    'notes': notes,
    'createdAt': createdAt.toIso8601String(),
    if (!createdAtKnown) 'createdAtKnown': false,
  };
  factory Student.fromJson(Map<String, dynamic> value) => Student(
    id: value['id'] as String,
    code: value['code'] as String,
    barcode: value['barcode'] as String? ?? '',
    name: value['name'] as String,
    phone: value['phone'] as String,
    guardianPhone: value['guardianPhone'] as String,
    twinStudentId: value['twinStudentId'] as String?,
    isSuspended: value['isSuspended'] as bool? ?? false,
    suspensionReason: value['suspensionReason'] as String? ?? '',
    suspendedAt: value['suspendedAt'] == null
        ? null
        : DateTime.parse(value['suspendedAt'] as String),
    suspendedBy: value['suspendedBy'] as String?,
    groupIds: List<String>.from(value['groupIds'] as List),
    discountPercent: value['discountPercent'] as num,
    discountNeedsReview: value['discountNeedsReview'] as bool? ?? false,
    centerOnly: value['centerOnly'] as bool? ?? false,
    packageMember: value['packageMember'] as bool? ?? false,
    centerFeeEnabled: value['centerFeeEnabled'] as bool? ?? false,
    centerFeeAmount: value['centerFeeAmount'] as int? ?? 1500,
    notes: value['notes'] as String,
    createdAt: DateTime.parse(value['createdAt'] as String),
    createdAtKnown: value['createdAtKnown'] as bool? ?? true,
  );
  Student copyWith({
    String? id,
    String? code,
    String? barcode,
    String? name,
    String? phone,
    String? guardianPhone,
    String? twinStudentId,
    bool? isSuspended,
    String? suspensionReason,
    DateTime? suspendedAt,
    String? suspendedBy,
    bool clearSuspensionMetadata = false,
    bool clearTwinStudentId = false,
    List<String>? groupIds,
    num? discountPercent,
    bool? discountNeedsReview,
    bool? centerOnly,
    bool? packageMember,
    bool? centerFeeEnabled,
    int? centerFeeAmount,
    String? notes,
    DateTime? createdAt,
    bool? createdAtKnown,
  }) => Student(
    id: id ?? this.id,
    code: code ?? this.code,
    barcode: barcode ?? this.barcode,
    name: name ?? this.name,
    phone: phone ?? this.phone,
    guardianPhone: guardianPhone ?? this.guardianPhone,
    twinStudentId: clearTwinStudentId
        ? null
        : twinStudentId ?? this.twinStudentId,
    isSuspended: isSuspended ?? this.isSuspended,
    suspensionReason: clearSuspensionMetadata
        ? ''
        : suspensionReason ?? this.suspensionReason,
    suspendedAt: clearSuspensionMetadata
        ? null
        : suspendedAt ?? this.suspendedAt,
    suspendedBy: clearSuspensionMetadata
        ? null
        : suspendedBy ?? this.suspendedBy,
    groupIds: groupIds ?? this.groupIds,
    discountPercent: discountPercent ?? this.discountPercent,
    discountNeedsReview: discountNeedsReview ?? this.discountNeedsReview,
    centerOnly: centerOnly ?? this.centerOnly,
    packageMember: packageMember ?? this.packageMember,
    centerFeeEnabled: centerFeeEnabled ?? this.centerFeeEnabled,
    centerFeeAmount: centerFeeAmount ?? this.centerFeeAmount,
    notes: notes ?? this.notes,
    createdAt: createdAt ?? this.createdAt,
    createdAtKnown: createdAtKnown ?? this.createdAtKnown,
  );
}

class LessonSession {
  LessonSession({
    this.id = '',
    this.preparedLessonId,
    this.name = '',
    required this.groupId,
    required this.number,
    this.monthNumber = 1,
    required this.startsAt,
    this.startsAtKnown = true,
    List<String>? importRoster,
    this.kind = SessionKind.counted,
    this.extraPrice = 0,
    this.status = SessionStatus.open,
    this.startedAt,
    this.startedBy,
    required this.createdAt,
  }) : importRoster = importRoster == null
           ? null
           : List<String>.unmodifiable(importRoster);
  final String id;
  final String? preparedLessonId;
  final String name;
  final String groupId;
  final int number;
  final int monthNumber;
  final DateTime startsAt;

  /// Historical imports can preserve a lesson number without claiming a date.
  final bool startsAtKnown;
  final List<String>? importRoster;
  final SessionKind kind;
  final int extraPrice;
  final SessionStatus status;
  final DateTime? startedAt;
  final String? startedBy;
  final DateTime createdAt;
  Map<String, dynamic> toJson() => {
    'id': id,
    if (preparedLessonId != null) 'preparedLessonId': preparedLessonId,
    if (name.isNotEmpty) 'name': name,
    'groupId': groupId,
    'number': number,
    if (monthNumber != 1) 'monthNumber': monthNumber,
    'startsAt': startsAt.toIso8601String(),
    if (!startsAtKnown) 'startsAtKnown': false,
    if (importRoster != null) 'importRoster': importRoster,
    'kind': kind.name,
    'extraPrice': extraPrice,
    'status': status.name,
    if (startedAt != null) 'startedAt': startedAt!.toIso8601String(),
    if (startedBy != null) 'startedBy': startedBy,
    'createdAt': createdAt.toIso8601String(),
  };
  factory LessonSession.fromJson(Map<String, dynamic> value) => LessonSession(
    id: value['id'] as String,
    preparedLessonId: value['preparedLessonId'] as String?,
    name: value['name'] as String? ?? '',
    groupId: value['groupId'] as String,
    number: value['number'] as int,
    monthNumber: value['monthNumber'] as int? ?? 1,
    startsAt: DateTime.parse(value['startsAt'] as String),
    startsAtKnown: value['startsAtKnown'] as bool? ?? true,
    importRoster: value['importRoster'] == null
        ? null
        : List<String>.unmodifiable(value['importRoster'] as List),
    kind: SessionKind.values.byName(value['kind'] as String),
    extraPrice: value['extraPrice'] as int,
    status: SessionStatus.values.byName(value['status'] as String),
    startedAt: value['startedAt'] == null
        ? null
        : DateTime.parse(value['startedAt'] as String),
    startedBy: value['startedBy'] as String?,
    createdAt: DateTime.parse(value['createdAt'] as String),
  );
  LessonSession copyWith({
    String? id,
    String? preparedLessonId,
    String? name,
    String? groupId,
    int? number,
    int? monthNumber,
    DateTime? startsAt,
    bool? startsAtKnown,
    List<String>? importRoster,
    SessionKind? kind,
    int? extraPrice,
    SessionStatus? status,
    DateTime? startedAt,
    String? startedBy,
    bool clearStarted = false,
    DateTime? createdAt,
  }) => LessonSession(
    id: id ?? this.id,
    preparedLessonId: preparedLessonId ?? this.preparedLessonId,
    name: name ?? this.name,
    groupId: groupId ?? this.groupId,
    number: number ?? this.number,
    monthNumber: monthNumber ?? this.monthNumber,
    startsAt: startsAt ?? this.startsAt,
    startsAtKnown: startsAtKnown ?? this.startsAtKnown,
    importRoster: importRoster ?? this.importRoster,
    kind: kind ?? this.kind,
    extraPrice: extraPrice ?? this.extraPrice,
    status: status ?? this.status,
    startedAt: clearStarted ? null : startedAt ?? this.startedAt,
    startedBy: clearStarted ? null : startedBy ?? this.startedBy,
    createdAt: createdAt ?? this.createdAt,
  );
}

class PrepaidPackage {
  const PrepaidPackage({
    this.id = '',
    required this.studentId,
    required this.groupId,
    required this.purchasedAt,
    int? remaining,
    this.totalSessions = 4,
    this.monthPlanId,
    this.monthPlanName,
    required this.paymentId,
  }) : remaining = remaining ?? totalSessions;
  final String id;
  final String studentId;
  final String groupId;
  final DateTime purchasedAt;
  final int remaining;
  final int totalSessions;
  final String? monthPlanId, monthPlanName;
  final String paymentId;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'groupId': groupId,
    'purchasedAt': purchasedAt.toIso8601String(),
    'remaining': remaining,
    'totalSessions': totalSessions,
    if (monthPlanId != null) 'monthPlanId': monthPlanId,
    if (monthPlanName != null) 'monthPlanName': monthPlanName,
    'paymentId': paymentId,
  };
  factory PrepaidPackage.fromJson(Map<String, dynamic> value) => PrepaidPackage(
    id: value['id'] as String,
    studentId: value['studentId'] as String,
    groupId: value['groupId'] as String,
    purchasedAt: DateTime.parse(value['purchasedAt'] as String),
    remaining: value['remaining'] as int,
    totalSessions: value['totalSessions'] as int? ?? 4,
    monthPlanId: value['monthPlanId'] as String?,
    monthPlanName: value['monthPlanName'] as String?,
    paymentId: value['paymentId'] as String,
  );
  PrepaidPackage copyWith({
    String? id,
    String? studentId,
    String? groupId,
    DateTime? purchasedAt,
    int? remaining,
    int? totalSessions,
    String? monthPlanId,
    String? monthPlanName,
    String? paymentId,
  }) => PrepaidPackage(
    id: id ?? this.id,
    studentId: studentId ?? this.studentId,
    groupId: groupId ?? this.groupId,
    purchasedAt: purchasedAt ?? this.purchasedAt,
    remaining: remaining ?? this.remaining,
    totalSessions: totalSessions ?? this.totalSessions,
    monthPlanId: monthPlanId ?? this.monthPlanId,
    monthPlanName: monthPlanName ?? this.monthPlanName,
    paymentId: paymentId ?? this.paymentId,
  );
}

class AttendanceRecord {
  const AttendanceRecord({
    this.id = '',
    required this.studentId,
    required this.sessionId,
    required this.status,
    required this.recordedAt,
    this.recordedAtKnown = true,
    this.packageId,
    this.originalAttendanceId,
    this.makeupSourceGroupId,
    this.paymentPending = false,
    this.fixedDiscountPercent,
    this.importSource = '',
    this.centerFeeOnly = false,
    this.packageMember = false,
    this.centerFeeAmount = 0,
  });
  final String id;
  final String studentId;
  final String sessionId;
  final AttendanceStatus status;
  final DateTime recordedAt;
  final bool recordedAtKnown;
  final String? packageId;
  final String? originalAttendanceId;

  /// Source group whose lesson price or prepaid balance covers cross-group makeup.
  final String? makeupSourceGroupId;

  /// Presence is saved independently; this entry still needs financial coverage.
  final bool paymentPending;

  /// Fixed student discount at recording time; null means unknown legacy history.
  final num? fixedDiscountPercent;

  /// Source file reference proves historical attendance, never a cash receipt.
  final String importSource;
  final bool centerFeeOnly;
  final bool packageMember;
  final int centerFeeAmount;
  Map<String, dynamic> toJson() => {
    'id': id,
    if (packageMember) 'packageMember': true,
    'studentId': studentId,
    'sessionId': sessionId,
    'status': status.name,
    'recordedAt': recordedAt.toIso8601String(),
    if (!recordedAtKnown) 'recordedAtKnown': false,
    'packageId': packageId,
    'originalAttendanceId': originalAttendanceId,
    if (makeupSourceGroupId != null) 'makeupSourceGroupId': makeupSourceGroupId,
    if (paymentPending) 'paymentPending': true,
    if (importSource.isNotEmpty) 'importSource': importSource,
    if (centerFeeOnly) 'centerFeeOnly': true,
    if (centerFeeAmount != 0) 'centerFeeAmount': centerFeeAmount,
    if (fixedDiscountPercent != null)
      'fixedDiscountPercent': fixedDiscountPercent,
  };
  factory AttendanceRecord.fromJson(Map<String, dynamic> value) =>
      AttendanceRecord(
        id: value['id'] as String,
        studentId: value['studentId'] as String,
        sessionId: value['sessionId'] as String,
        status: AttendanceStatus.values.byName(value['status'] as String),
        recordedAt: DateTime.parse(value['recordedAt'] as String),
        recordedAtKnown: value['recordedAtKnown'] as bool? ?? true,
        packageId: value['packageId'] as String?,
        originalAttendanceId: value['originalAttendanceId'] as String?,
        makeupSourceGroupId: value['makeupSourceGroupId'] as String?,
        paymentPending: value['paymentPending'] as bool? ?? false,
        fixedDiscountPercent: value['fixedDiscountPercent'] as num?,
        importSource: value['importSource'] as String? ?? '',
        centerFeeOnly: value['centerFeeOnly'] as bool? ?? false,
        packageMember: value['packageMember'] as bool? ?? false,
        centerFeeAmount: value['centerFeeAmount'] as int? ?? 0,
      );
  AttendanceRecord copyWith({
    String? id,
    String? studentId,
    String? sessionId,
    AttendanceStatus? status,
    DateTime? recordedAt,
    bool? recordedAtKnown,
    String? packageId,
    String? originalAttendanceId,
    String? makeupSourceGroupId,
    bool? paymentPending,
    num? fixedDiscountPercent,
    String? importSource,
    bool? centerFeeOnly,
    bool? packageMember,
    int? centerFeeAmount,
  }) => AttendanceRecord(
    id: id ?? this.id,
    studentId: studentId ?? this.studentId,
    sessionId: sessionId ?? this.sessionId,
    status: status ?? this.status,
    recordedAt: recordedAt ?? this.recordedAt,
    recordedAtKnown: recordedAtKnown ?? this.recordedAtKnown,
    packageId: packageId ?? this.packageId,
    originalAttendanceId: originalAttendanceId ?? this.originalAttendanceId,
    makeupSourceGroupId: makeupSourceGroupId ?? this.makeupSourceGroupId,
    paymentPending: paymentPending ?? this.paymentPending,
    fixedDiscountPercent: fixedDiscountPercent ?? this.fixedDiscountPercent,
    importSource: importSource ?? this.importSource,
    centerFeeOnly: centerFeeOnly ?? this.centerFeeOnly,
    packageMember: packageMember ?? this.packageMember,
    centerFeeAmount: centerFeeAmount ?? this.centerFeeAmount,
  );
}

/// Card fees are separate from lesson payments and never buy attendance credit.
class CenterCardSettings {
  const CenterCardSettings({
    this.price,
    this.requirePaymentBeforeReceipt = true,
  });
  final int? price;
  final bool requirePaymentBeforeReceipt;
  Map<String, dynamic> toJson() => {
    'price': price,
    'requirePaymentBeforeReceipt': requirePaymentBeforeReceipt,
  };
  factory CenterCardSettings.fromJson(Map<String, dynamic> value) =>
      CenterCardSettings(
        price: value['price'] as int?,
        requirePaymentBeforeReceipt:
            value['requirePaymentBeforeReceipt'] as bool? ?? true,
      );
}

class StudentCardPayment {
  const StudentCardPayment({
    this.id = '',
    required this.studentId,
    this.groupId,
    this.sessionId,
    required this.baseAmount,
    required this.discountPercent,
    required this.netAmount,
    this.paidAmount,
    this.method = 'نقدي',
    required this.createdAt,
    required this.staffId,
  });
  final String id, studentId, method, staffId;
  final String? groupId, sessionId;
  final int baseAmount, netAmount;
  final int? paidAmount;
  int get collectedAmount => paidAmount ?? netAmount;
  final num discountPercent;
  final DateTime createdAt;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'groupId': groupId,
    'sessionId': sessionId,
    'baseAmount': baseAmount,
    'discountPercent': discountPercent,
    'netAmount': netAmount,
    if (paidAmount != null) 'paidAmount': paidAmount,
    'method': method,
    'createdAt': createdAt.toIso8601String(),
    'staffId': staffId,
  };
  factory StudentCardPayment.fromJson(Map<String, dynamic> value) =>
      StudentCardPayment(
        id: value['id'] as String,
        studentId: value['studentId'] as String,
        groupId: value['groupId'] as String?,
        sessionId: value['sessionId'] as String?,
        baseAmount: value['baseAmount'] as int,
        discountPercent: value['discountPercent'] as num,
        netAmount: value['netAmount'] as int,
        paidAmount: value['paidAmount'] as int?,
        method: value['method'] as String,
        createdAt: DateTime.parse(value['createdAt'] as String),
        staffId: value['staffId'] as String,
      );
}

class StudentCardReceipt {
  const StudentCardReceipt({
    this.id = '',
    required this.studentId,
    this.paymentId,
    required this.receivedAt,
    required this.staffId,
    this.paymentBypassed = false,
  });
  final String id, studentId, staffId;
  final String? paymentId;
  final DateTime receivedAt;
  final bool paymentBypassed;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'paymentId': paymentId,
    'receivedAt': receivedAt.toIso8601String(),
    'staffId': staffId,
    'paymentBypassed': paymentBypassed,
  };
  factory StudentCardReceipt.fromJson(Map<String, dynamic> value) =>
      StudentCardReceipt(
        id: value['id'] as String,
        studentId: value['studentId'] as String,
        paymentId: value['paymentId'] as String?,
        receivedAt: DateTime.parse(value['receivedAt'] as String),
        staffId: value['staffId'] as String,
        paymentBypassed: value['paymentBypassed'] as bool,
      );
}

class PaymentRecord {
  const PaymentRecord({
    this.id = '',
    required this.studentId,
    required this.groupId,
    this.sessionId,
    this.packageId,
    required this.description,
    required this.baseAmount,
    required this.discountPercent,
    required this.netAmount,
    this.paidAmount,
    this.method = 'نقدي',
    required this.createdAt,
    this.createdAtKnown = true,
    required this.staffId,
  });
  final String id;
  final String studentId;
  final String groupId;
  final String? sessionId;
  final String? packageId;
  final String description;
  final int baseAmount;
  final num discountPercent;
  final int netAmount;
  final int? paidAmount;
  int get collectedAmount => paidAmount ?? netAmount;
  final String method;
  final DateTime createdAt;
  final bool createdAtKnown;
  final String staffId;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'groupId': groupId,
    'sessionId': sessionId,
    'packageId': packageId,
    'description': description,
    'baseAmount': baseAmount,
    'discountPercent': discountPercent,
    'netAmount': netAmount,
    if (paidAmount != null) 'paidAmount': paidAmount,
    'method': method,
    'createdAt': createdAt.toIso8601String(),
    if (!createdAtKnown) 'createdAtKnown': false,
    'staffId': staffId,
  };
  factory PaymentRecord.fromJson(Map<String, dynamic> value) => PaymentRecord(
    id: value['id'] as String,
    studentId: value['studentId'] as String,
    groupId: value['groupId'] as String,
    sessionId: value['sessionId'] as String?,
    packageId: value['packageId'] as String?,
    description: value['description'] as String,
    baseAmount: value['baseAmount'] as int,
    discountPercent: value['discountPercent'] as num,
    netAmount: value['netAmount'] as int,
    paidAmount: value['paidAmount'] as int?,
    method: value['method'] as String,
    createdAt: DateTime.parse(value['createdAt'] as String),
    createdAtKnown: value['createdAtKnown'] as bool? ?? true,
    staffId: value['staffId'] as String,
  );
  PaymentRecord copyWith({
    String? id,
    String? studentId,
    String? groupId,
    String? sessionId,
    String? packageId,
    String? description,
    int? baseAmount,
    num? discountPercent,
    int? netAmount,
    int? paidAmount,
    String? method,
    DateTime? createdAt,
    bool? createdAtKnown,
    String? staffId,
  }) => PaymentRecord(
    id: id ?? this.id,
    studentId: studentId ?? this.studentId,
    groupId: groupId ?? this.groupId,
    sessionId: sessionId ?? this.sessionId,
    packageId: packageId ?? this.packageId,
    description: description ?? this.description,
    baseAmount: baseAmount ?? this.baseAmount,
    discountPercent: discountPercent ?? this.discountPercent,
    netAmount: netAmount ?? this.netAmount,
    paidAmount: paidAmount ?? this.paidAmount,
    method: method ?? this.method,
    createdAt: createdAt ?? this.createdAt,
    createdAtKnown: createdAtKnown ?? this.createdAtKnown,
    staffId: staffId ?? this.staffId,
  );
}

/// Shared definitions belong to a prepared lesson; legacy ones keep a session.
class AcademicActivity {
  const AcademicActivity({
    this.id = '',
    this.sessionId = '',
    this.preparedLessonId,
    required this.kind,
    required this.name,
    this.maxScore = 10,
    this.maxScoreKnown = true,
    required this.createdAt,
  });
  final String id, sessionId, name;
  final String? preparedLessonId;
  final AcademicActivityKind kind;
  final int maxScore;
  final bool maxScoreKnown;
  final DateTime createdAt;
  Map<String, dynamic> toJson() => {
    'id': id,
    'sessionId': sessionId,
    if (preparedLessonId != null) 'preparedLessonId': preparedLessonId,
    'kind': kind.name,
    'name': name,
    'maxScore': maxScore,
    if (!maxScoreKnown) 'maxScoreKnown': false,
    'createdAt': createdAt.toIso8601String(),
  };
  factory AcademicActivity.fromJson(Map<String, dynamic> value) =>
      AcademicActivity(
        id: value['id'] as String,
        sessionId: value['sessionId'] as String? ?? '',
        preparedLessonId: value['preparedLessonId'] as String?,
        kind: AcademicActivityKind.values.byName(value['kind'] as String),
        name: value['name'] as String,
        maxScore: value['maxScore'] as int? ?? 10,
        maxScoreKnown: value['maxScoreKnown'] as bool? ?? true,
        createdAt: DateTime.parse(value['createdAt'] as String),
      );
  AcademicActivity copyWith({
    String? id,
    String? sessionId,
    String? preparedLessonId,
    AcademicActivityKind? kind,
    String? name,
    int? maxScore,
    bool? maxScoreKnown,
    DateTime? createdAt,
  }) => AcademicActivity(
    id: id ?? this.id,
    sessionId: sessionId ?? this.sessionId,
    preparedLessonId: preparedLessonId ?? this.preparedLessonId,
    kind: kind ?? this.kind,
    name: name ?? this.name,
    maxScore: maxScore ?? this.maxScore,
    maxScoreKnown: maxScoreKnown ?? this.maxScoreKnown,
    createdAt: createdAt ?? this.createdAt,
  );

  bool appliesToSession(LessonSession session) => preparedLessonId == null
      ? sessionId == session.id
      : preparedLessonId == session.preparedLessonId;
}

class AcademicRecord {
  const AcademicRecord({
    this.id = '',
    required this.studentId,
    required this.sessionId,
    this.activityId,
    this.homework = HomeworkStatus.notReviewed,
    this.score,
    this.maxScore = 10,
    this.maxScoreKnown = true,
    this.examAbsent = false,
    this.notes = '',
    required this.updatedAt,
  });
  final String id;
  final String studentId;
  final String sessionId;
  final String? activityId;
  final HomeworkStatus homework;
  final num? score;
  final int maxScore;
  final bool maxScoreKnown;
  final bool examAbsent;
  final String notes;
  final DateTime updatedAt;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'sessionId': sessionId,
    if (activityId != null) 'activityId': activityId,
    'homework': homework.name,
    'score': score,
    'maxScore': maxScore,
    if (!maxScoreKnown) 'maxScoreKnown': false,
    'examAbsent': examAbsent,
    'notes': notes,
    'updatedAt': updatedAt.toIso8601String(),
  };
  factory AcademicRecord.fromJson(Map<String, dynamic> value) => AcademicRecord(
    id: value['id'] as String,
    studentId: value['studentId'] as String,
    sessionId: value['sessionId'] as String,
    activityId: value['activityId'] as String?,
    homework: HomeworkStatus.values.byName(value['homework'] as String),
    score: value['score'] as num?,
    maxScore: value['maxScore'] as int,
    maxScoreKnown: value['maxScoreKnown'] as bool? ?? true,
    examAbsent: value['examAbsent'] as bool,
    notes: value['notes'] as String,
    updatedAt: DateTime.parse(value['updatedAt'] as String),
  );
  AcademicRecord copyWith({
    String? id,
    String? studentId,
    String? sessionId,
    String? activityId,
    HomeworkStatus? homework,
    num? score,
    int? maxScore,
    bool? maxScoreKnown,
    bool? examAbsent,
    String? notes,
    DateTime? updatedAt,
  }) => AcademicRecord(
    id: id ?? this.id,
    studentId: studentId ?? this.studentId,
    sessionId: sessionId ?? this.sessionId,
    activityId: activityId ?? this.activityId,
    homework: homework ?? this.homework,
    score: score ?? this.score,
    maxScore: maxScore ?? this.maxScore,
    maxScoreKnown: maxScoreKnown ?? this.maxScoreKnown,
    examAbsent: examAbsent ?? this.examAbsent,
    notes: notes ?? this.notes,
    updatedAt: updatedAt ?? this.updatedAt,
  );
}

class AuditRecord {
  const AuditRecord({
    this.id = '',
    required this.action,
    required this.description,
    required this.staffId,
    required this.createdAt,
  });
  final String id;
  final String action;
  final String description;
  final String staffId;
  final DateTime createdAt;
  Map<String, dynamic> toJson() => {
    'id': id,
    'action': action,
    'description': description,
    'staffId': staffId,
    'createdAt': createdAt.toIso8601String(),
  };
  factory AuditRecord.fromJson(Map<String, dynamic> value) => AuditRecord(
    id: value['id'] as String,
    action: value['action'] as String,
    description: value['description'] as String,
    staffId: value['staffId'] as String,
    createdAt: DateTime.parse(value['createdAt'] as String),
  );
  AuditRecord copyWith({
    String? id,
    String? action,
    String? description,
    String? staffId,
    DateTime? createdAt,
  }) => AuditRecord(
    id: id ?? this.id,
    action: action ?? this.action,
    description: description ?? this.description,
    staffId: staffId ?? this.staffId,
    createdAt: createdAt ?? this.createdAt,
  );
}

class StaffUser {
  const StaffUser({this.id = '', required this.name, required this.role});
  final String id;
  final String name;
  final StaffRole role;
  Map<String, dynamic> toJson() => {'id': id, 'name': name, 'role': role.name};
  factory StaffUser.fromJson(Map<String, dynamic> value) => StaffUser(
    id: value['id'] as String,
    name: value['name'] as String,
    role: StaffRole.values.byName(value['role'] as String),
  );
  StaffUser copyWith({String? id, String? name, StaffRole? role}) => StaffUser(
    id: id ?? this.id,
    name: name ?? this.name,
    role: role ?? this.role,
  );
}

/// Ephemeral financial preview; it is checked again inside the entry transaction.
class EntryConfirmation {
  const EntryConfirmation({
    required this.staffId,
    required this.groupId,
    required this.sessionKind,
    required this.baseAmount,
    required this.discountPercent,
    required this.netAmount,
    this.paidAmount,
    this.monthPlanId,
    this.monthPlanName,
    this.monthPlanSessions,
    this.centerFeeOnly = false,
    this.centerFeeAmount = 0,
    required this.eligibleRemaining,
  });
  final String staffId, groupId;
  final SessionKind sessionKind;
  final int baseAmount, netAmount, eligibleRemaining;
  final int? paidAmount;
  final String? monthPlanId, monthPlanName;
  final int? monthPlanSessions;
  final bool centerFeeOnly;
  final int centerFeeAmount;
  int get collectedAmount => paidAmount ?? netAmount;
  final num discountPercent;
  @override
  bool operator ==(Object other) =>
      other is EntryConfirmation &&
      staffId == other.staffId &&
      groupId == other.groupId &&
      sessionKind == other.sessionKind &&
      baseAmount == other.baseAmount &&
      discountPercent == other.discountPercent &&
      netAmount == other.netAmount &&
      paidAmount == other.paidAmount &&
      monthPlanId == other.monthPlanId &&
      monthPlanName == other.monthPlanName &&
      monthPlanSessions == other.monthPlanSessions &&
      centerFeeOnly == other.centerFeeOnly &&
      centerFeeAmount == other.centerFeeAmount &&
      eligibleRemaining == other.eligibleRemaining;
  @override
  int get hashCode => Object.hash(
    staffId,
    groupId,
    sessionKind,
    baseAmount,
    discountPercent,
    netAmount,
    paidAmount,
    monthPlanId,
    monthPlanName,
    monthPlanSessions,
    centerFeeOnly,
    centerFeeAmount,
    eligibleRemaining,
  );
}

class EntryRequest {
  const EntryRequest({
    required this.studentId,
    required this.sessionId,
    required this.mode,
    this.method = 'نقدي',
    this.notes = '',
    this.originalAttendanceId,
    this.makeupSourceGroupId,
    this.packageSessions = 4,
    this.monthPlanId,
    this.confirmation,
    this.paidAmount,
    this.acknowledgedAttendanceIds = const [],
  });
  final String studentId;
  final String sessionId;
  final EntryMode mode;
  final String method;
  final String notes;
  final String? originalAttendanceId;
  final String? makeupSourceGroupId;
  final int packageSessions;
  final String? monthPlanId;
  final EntryConfirmation? confirmation;
  final int? paidAmount;
  final List<String> acknowledgedAttendanceIds;
}

class PackageRequest {
  const PackageRequest({
    required this.studentId,
    required this.groupId,
    this.method = 'نقدي',
    this.notes = '',
    this.sessionId,
    this.sessions = 4,
    this.monthPlanId,
    this.expectedMonthPlan,
    this.paidAmount,
    this.expectedNetAmount,
  });
  final String studentId;
  final String groupId;
  final String method;
  final String notes;
  final String? sessionId;
  final int sessions;
  final String? monthPlanId;
  final GroupMonthPlan? expectedMonthPlan;
  final int? paidAmount;
  final int? expectedNetAmount;
}

class PaymentReview {
  const PaymentReview({
    this.id = '',
    required this.studentId,
    this.sessionId,
    this.paymentId,
    required this.paperAmount,
    required this.expectedAmount,
    this.notes = '',
    required this.staffId,
    required this.createdAt,
  });
  final String id, studentId, notes, staffId;
  final String? sessionId, paymentId;
  final int paperAmount, expectedAmount;
  final DateTime createdAt;
  int get difference => paperAmount - expectedAmount;
  bool get matched => paymentId != null && difference == 0;
  Map<String, dynamic> toJson() => {
    'id': id,
    'studentId': studentId,
    'sessionId': sessionId,
    'paymentId': paymentId,
    'paperAmount': paperAmount,
    'expectedAmount': expectedAmount,
    'notes': notes,
    'staffId': staffId,
    'createdAt': createdAt.toIso8601String(),
  };
  factory PaymentReview.fromJson(Map<String, dynamic> json) => PaymentReview(
    id: json['id'] as String,
    studentId: json['studentId'] as String,
    sessionId: json['sessionId'] as String?,
    paymentId: json['paymentId'] as String?,
    paperAmount: json['paperAmount'] as int,
    expectedAmount: json['expectedAmount'] as int,
    notes: json['notes'] as String,
    staffId: json['staffId'] as String,
    createdAt: DateTime.parse(json['createdAt'] as String),
  );
}

class ReviewRequest {
  const ReviewRequest({
    this.id = '',
    required this.studentId,
    required this.paperAmount,
    this.sessionId,
    this.paymentId,
    this.notes = '',
  });
  final String id, studentId, notes;
  final String? sessionId, paymentId;
  final int paperAmount;
}

class SessionPriceLine {
  const SessionPriceLine({
    required this.label,
    required this.unitAmount,
    required this.count,
  });
  final String label;
  final int unitAmount, count;
  int get total => unitAmount * count;
  Map<String, dynamic> toJson() => {
    'label': label,
    'unitAmount': unitAmount,
    'count': count,
  };
  factory SessionPriceLine.fromJson(Map<String, dynamic> json) =>
      SessionPriceLine(
        label: json['label'] as String,
        unitAmount: json['unitAmount'] as int,
        count: json['count'] as int,
      );
}

enum SessionStudentCategoryKind {
  unpaid,
  single,
  package,
  prepaid,
  free,
  makeup,
  absent,
  debtSettlement,
  packageMember,
}

class SessionStudentCategory {
  const SessionStudentCategory({
    required this.kind,
    required this.label,
    this.discountPercent,
    required this.unitAmount,
    required this.studentCount,
    required this.operationCount,
  });
  final SessionStudentCategoryKind kind;
  final String label;
  final num? discountPercent;
  final int unitAmount, studentCount, operationCount;
  Map<String, dynamic> toJson() => {
    'kind': kind.name,
    'label': label,
    'discountPercent': discountPercent,
    'unitAmount': unitAmount,
    'studentCount': studentCount,
    'operationCount': operationCount,
  };
  factory SessionStudentCategory.fromJson(Map<String, dynamic> j) =>
      SessionStudentCategory(
        kind: SessionStudentCategoryKind.values.byName(j['kind'] as String),
        label: j['label'] as String,
        discountPercent: j['discountPercent'] as num?,
        unitAmount: j['unitAmount'] as int,
        studentCount: j['studentCount'] as int,
        operationCount: j['operationCount'] as int,
      );
}

/// Actual attendee counts grouped by their fixed discount at registration.
/// This is separate from the price/discount on an earlier package purchase.
class AttendanceDiscountCategory {
  const AttendanceDiscountCategory({
    required this.discountPercent,
    required this.label,
    required this.studentCount,
  });
  final num? discountPercent;
  final String label;
  final int studentCount;
  Map<String, dynamic> toJson() => {
    'discountPercent': discountPercent,
    'label': label,
    'studentCount': studentCount,
  };
  factory AttendanceDiscountCategory.fromJson(Map<String, dynamic> json) =>
      AttendanceDiscountCategory(
        discountPercent: json['discountPercent'] as num?,
        label: json['label'] as String,
        studentCount: json['studentCount'] as int,
      );
}

/// Active receipts grouped by the amount actually collected and operation kind.
class SessionPaymentAmountCategory {
  const SessionPaymentAmountCategory({
    required this.kind,
    required this.unitAmount,
    required this.studentCount,
    required this.operationCount,
  });
  final SessionStudentCategoryKind kind;
  final int unitAmount, studentCount, operationCount;
  Map<String, dynamic> toJson() => {
    'kind': kind.name,
    'unitAmount': unitAmount,
    'studentCount': studentCount,
    'operationCount': operationCount,
  };
  factory SessionPaymentAmountCategory.fromJson(Map<String, dynamic> json) =>
      SessionPaymentAmountCategory(
        kind: SessionStudentCategoryKind.values.byName(json['kind'] as String),
        unitAmount: json['unitAmount'] as int,
        studentCount: json['studentCount'] as int,
        operationCount: json['operationCount'] as int,
      );
}

/// Center receipts stay separate from lesson/package income classifications.
class SessionCenterFeePaymentCategory {
  const SessionCenterFeePaymentCategory({
    required this.centerOnly,
    required this.unitAmount,
    required this.studentCount,
    required this.operationCount,
  });
  final bool centerOnly;
  final int unitAmount, studentCount, operationCount;
  Map<String, dynamic> toJson() => {
    'centerOnly': centerOnly,
    'unitAmount': unitAmount,
    'studentCount': studentCount,
    'operationCount': operationCount,
  };
  factory SessionCenterFeePaymentCategory.fromJson(Map<String, dynamic> json) =>
      SessionCenterFeePaymentCategory(
        centerOnly: json['centerOnly'] as bool,
        unitAmount: json['unitAmount'] as int,
        studentCount: json['studentCount'] as int,
        operationCount: json['operationCount'] as int,
      );
}

class SessionFinancialSummary {
  SessionFinancialSummary({
    required this.sessionId,
    required this.presentCount,
    required this.makeupCount,
    required this.absentCount,
    required this.prepaidCount,
    required this.singlePaymentCount,
    required this.packageSalesCount,
    required this.freeCount,
    required this.grossAmount,
    required this.discountAmount,
    required this.totalCollected,
    required this.expectedCash,
    this.refundAmount = 0,
    this.centerFeeCollected = 0,
    this.coverageClassificationVersion,
    required List<SessionPriceLine> lines,
    List<SessionStudentCategory>? studentCategories,
    List<AttendanceDiscountCategory>? attendanceDiscountCategories,
    this.allFreeCount,
    this.packageBuyerCount,
    this.cardPaymentCount,
    this.cardCollectedAmount,
    this.debtAmount,
    this.debtSettlementAmount,
    List<SessionPaymentAmountCategory>? paymentAmountCategories,
    List<SessionCenterFeePaymentCategory>? centerFeePaymentCategories,
  }) : lines = List.unmodifiable(lines),
       studentCategories = studentCategories == null
           ? null
           : List.unmodifiable(studentCategories),
       attendanceDiscountCategories = attendanceDiscountCategories == null
           ? null
           : List.unmodifiable(attendanceDiscountCategories),
       paymentAmountCategories = paymentAmountCategories == null
           ? null
           : List.unmodifiable(paymentAmountCategories),
       centerFeePaymentCategories = centerFeePaymentCategories == null
           ? null
           : List.unmodifiable(centerFeePaymentCategories);
  final String sessionId;
  final int presentCount,
      makeupCount,
      absentCount,
      prepaidCount,
      singlePaymentCount,
      packageSalesCount,
      freeCount,
      grossAmount,
      discountAmount,
      totalCollected,
      expectedCash;
  final int refundAmount;
  final int centerFeeCollected;
  final int? coverageClassificationVersion;
  final List<SessionPriceLine> lines;

  /// Absent in legacy saved closings; never fabricate historical category detail.
  final List<SessionStudentCategory>? studentCategories;

  /// Actual present/makeup attendees, by their fixed registration-time discount.
  /// Null means the historical closing predates this detail.
  final List<AttendanceDiscountCategory>? attendanceDiscountCategories;

  /// Distinct actual attendees in a free class or with a 100% exemption.
  /// Null means unavailable on a legacy closing.
  final int? allFreeCount;

  /// Distinct active package purchasers attributed to this session.
  final int? packageBuyerCount;

  /// Separate card operations attributed to the session; null on older closings.
  final int? cardPaymentCount, cardCollectedAmount;

  /// Debt created by original session sales, separate from collected cash.
  final int? debtAmount, debtSettlementAmount;
  final List<SessionPaymentAmountCategory>? paymentAmountCategories;

  /// Null preserves older closings without reconstructing unavailable detail.
  final List<SessionCenterFeePaymentCategory>? centerFeePaymentCategories;
  Map<String, dynamic> toJson() => {
    'sessionId': sessionId,
    'presentCount': presentCount,
    'makeupCount': makeupCount,
    'absentCount': absentCount,
    'prepaidCount': prepaidCount,
    'singlePaymentCount': singlePaymentCount,
    'packageSalesCount': packageSalesCount,
    'freeCount': freeCount,
    'grossAmount': grossAmount,
    'discountAmount': discountAmount,
    'totalCollected': totalCollected,
    'expectedCash': expectedCash,
    'refundAmount': refundAmount,
    if (centerFeeCollected != 0) 'centerFeeCollected': centerFeeCollected,
    if (coverageClassificationVersion != null)
      'coverageClassificationVersion': coverageClassificationVersion,
    'lines': lines.map((e) => e.toJson()).toList(),
    if (studentCategories != null)
      'studentCategories': studentCategories!.map((e) => e.toJson()).toList(),
    if (attendanceDiscountCategories != null)
      'attendanceDiscountCategories': attendanceDiscountCategories!
          .map((e) => e.toJson())
          .toList(),
    if (allFreeCount != null) 'allFreeCount': allFreeCount,
    if (packageBuyerCount != null) 'packageBuyerCount': packageBuyerCount,
    if (cardPaymentCount != null) 'cardPaymentCount': cardPaymentCount,
    if (cardCollectedAmount != null) 'cardCollectedAmount': cardCollectedAmount,
    if (debtAmount != null) 'debtAmount': debtAmount,
    if (debtSettlementAmount != null)
      'debtSettlementAmount': debtSettlementAmount,
    if (paymentAmountCategories != null)
      'paymentAmountCategories': paymentAmountCategories!
          .map((e) => e.toJson())
          .toList(),
    if (centerFeePaymentCategories != null)
      'centerFeePaymentCategories': centerFeePaymentCategories!
          .map((e) => e.toJson())
          .toList(),
  };
  factory SessionFinancialSummary.fromJson(
    Map<String, dynamic> json,
  ) => SessionFinancialSummary(
    sessionId: json['sessionId'] as String,
    presentCount: json['presentCount'] as int,
    makeupCount: json['makeupCount'] as int,
    absentCount: json['absentCount'] as int,
    prepaidCount: json['prepaidCount'] as int,
    singlePaymentCount: json['singlePaymentCount'] as int,
    packageSalesCount: json['packageSalesCount'] as int,
    freeCount: json['freeCount'] as int,
    grossAmount: json['grossAmount'] as int,
    discountAmount: json['discountAmount'] as int,
    totalCollected: json['totalCollected'] as int,
    expectedCash: json['expectedCash'] as int,
    refundAmount: json['refundAmount'] as int? ?? 0,
    centerFeeCollected: json['centerFeeCollected'] as int? ?? 0,
    coverageClassificationVersion:
        json['coverageClassificationVersion'] as int?,
    allFreeCount: json['allFreeCount'] as int?,
    packageBuyerCount: json['packageBuyerCount'] as int?,
    cardPaymentCount: json['cardPaymentCount'] as int?,
    cardCollectedAmount: json['cardCollectedAmount'] as int?,
    debtAmount: json['debtAmount'] as int?,
    debtSettlementAmount: json['debtSettlementAmount'] as int?,
    centerFeePaymentCategories: (json['centerFeePaymentCategories'] as List?)
        ?.map(
          (e) => SessionCenterFeePaymentCategory.fromJson(
            Map<String, dynamic>.from(e as Map),
          ),
        )
        .toList(),
    paymentAmountCategories: (json['paymentAmountCategories'] as List?)
        ?.map(
          (e) => SessionPaymentAmountCategory.fromJson(
            Map<String, dynamic>.from(e as Map),
          ),
        )
        .toList(),
    attendanceDiscountCategories:
        (json['attendanceDiscountCategories'] as List?)
            ?.map(
              (e) => AttendanceDiscountCategory.fromJson(
                Map<String, dynamic>.from(e as Map),
              ),
            )
            .toList(),
    studentCategories: (json['studentCategories'] as List?)
        ?.map(
          (e) => SessionStudentCategory.fromJson(
            Map<String, dynamic>.from(e as Map),
          ),
        )
        .toList(),
    lines: (json['lines'] as List)
        .map(
          (e) => SessionPriceLine.fromJson(Map<String, dynamic>.from(e as Map)),
        )
        .toList(),
  );
}

class SessionClosing {
  const SessionClosing({
    this.id = '',
    required this.sessionId,
    required this.summary,
    required this.actualCash,
    this.notes = '',
    required this.staffId,
    required this.createdAt,
  });
  final String id, sessionId, notes, staffId;
  final SessionFinancialSummary summary;
  final int actualCash;
  final DateTime createdAt;
  int get difference => actualCash - summary.expectedCash;
  Map<String, dynamic> toJson() => {
    'id': id,
    'sessionId': sessionId,
    'summary': summary.toJson(),
    'actualCash': actualCash,
    'notes': notes,
    'staffId': staffId,
    'createdAt': createdAt.toIso8601String(),
  };
  factory SessionClosing.fromJson(Map<String, dynamic> json) => SessionClosing(
    id: json['id'] as String,
    sessionId: json['sessionId'] as String,
    summary: SessionFinancialSummary.fromJson(
      Map<String, dynamic>.from(json['summary'] as Map),
    ),
    actualCash: json['actualCash'] as int,
    notes: json['notes'] as String,
    staffId: json['staffId'] as String,
    createdAt: DateTime.parse(json['createdAt'] as String),
  );
}
