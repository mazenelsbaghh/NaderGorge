import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'package:cryptography/cryptography.dart';
import 'package:crypto/crypto.dart' as crypto;
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:uuid/uuid.dart';
import '../data/center_state.dart';
import '../data/center_state_encoder.dart';
import '../domain/models.dart';
import '../domain/discount_calculation.dart';
import '../domain/student_lookup.dart';
import 'session_finance.dart';
import 'historical_academic_import.dart';
import 'academic_import_command.dart';
import '../shared/problem_reporting.dart';
import '../lan/lan_transport.dart';
export '../domain/models.dart' show CenterException;

part 'installation_admin.dart';
part 'center_store_lan.dart';
part 'center_store_remote.dart';
part 'center_store_client_workspace.dart';
part 'center_store_backups.dart';
part 'center_store_support.dart';
part 'center_store_debts.dart';
part 'center_store_read_index.dart';
part 'center_store_history.dart';
part 'center_store_students.dart';
part 'center_store_months.dart';
part 'center_store_data_repair.dart';
part 'center_store_center_fees.dart';
part 'center_store_study_months.dart';
part 'center_store_academic_import.dart';

class _EntryReversalOptions {
  const _EntryReversalOptions({
    this.action = CorrectionAction.entryReversed,
    this.refundNewPackage = false,
    this.mode = RecordCancellationMode.recordAndRelated,
  });
  final CorrectionAction action;
  final bool refundNewPackage;
  final RecordCancellationMode mode;
}

class CenterStore extends ChangeNotifier {
  CenterStore._(this._database, this.databasePath, this._state, this._fileLock);
  CenterStore._network(this.databasePath)
    : _database = null,
      _fileLock = null,
      _state = CenterState();
  factory CenterStore.remote(
    LanTransport transport, {
    required String localDirectory,
  }) => _RemoteCenterStore(transport, localDirectory);

  /// A secondary device keeps connection metadata only; no database is opened.
  factory CenterStore.clientWorkspace({required String directory}) =>
      _ClientWorkspace(directory);
  final RandomAccessFile? _fileLock;
  static final Set<String> _openFiles = {};
  final Database? _database;
  final String databasePath;
  CenterState _state;
  CenterState? _lanVersionState;
  String? _lanStateVersion;
  final _stateEncoder = CenterStateEncoder();
  _CenterReadIndex? _cachedReadIndex;
  CenterState? _lookupState;
  StudentLookupIndex? _receptionLookup;
  bool _changingState = false;

  // Mutations read live lists; committed and remote snapshots get their own index.
  _CenterReadIndex? get _readIndex {
    if (_changingState) return null;
    if (!identical(_cachedReadIndex?.state, _state)) {
      _cachedReadIndex = _CenterReadIndex(_state);
    }
    return _cachedReadIndex;
  }

  Iterable<AttendanceRecord> _attendancesForStudent(String studentId) {
    final indexed = _readIndex;
    return indexed == null
        ? _activeAttendances.where((entry) => entry.studentId == studentId)
        : indexed.attendancesByStudent[studentId] ?? const [];
  }

  Iterable<PaymentRecord> _paymentsForStudent(String studentId) {
    final indexed = _readIndex;
    return indexed == null
        ? _activePayments.where((payment) => payment.studentId == studentId)
        : indexed.paymentsByStudent[studentId] ?? const [];
  }

  StaffUser? _currentUser;
  InstallationAdmin? _installationAdmin;
  int _authenticationRevision = 0;
  int _logoutRevision = 0;
  Future<void> _queue = Future.value();
  static const automaticBackupInterval = Duration(minutes: 10);
  static const automaticBackupLimit = 50;
  Timer? _automaticBackupTimer;
  bool _automaticBackupRunning = false;
  DateTime? _lastAutomaticBackupAt;
  String? _automaticBackupError;

  DateTime? get lastAutomaticBackupAt => _lastAutomaticBackupAt;
  String? get automaticBackupError => _automaticBackupError;
  String? get automaticBackupDirectory => _database == null
      ? null
      : p.join(p.dirname(databasePath), 'backups', 'automatic');
  Future<void> Function()? supportUploadQueue;
  Map<String, dynamic> Function()? supportStatusReader;
  Map<String, dynamic> get supportStatus =>
      supportStatusReader?.call() ?? const {'configured': false};
  void notifySupportChanged() {
    if (!_closed) notifyListeners();
  }

  Future<void> requestSupportUpload() async {
    _require(currentUser != null);
    final queue = supportUploadQueue;
    if (queue == null || isRemote || isClientWorkspace) {
      throw const CenterException('المزامنة غير مهيأة على الجهاز الرئيسي بعد.');
    }
    await queue();
  }

  bool _closed = false;
  static const _uuid = Uuid();
  static final _passwordAlgorithm = Pbkdf2(
    macAlgorithm: Hmac.sha256(),
    iterations: 120000,
    bits: 256,
  );

  static Future<CenterStore> open({
    String? directory,
    Future<CenterState> Function()? initialState,
  }) async {
    try {
      return await _open(directory: directory, initialState: initialState);
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'database.open');
      if (error is CenterException) rethrow;
      throw CenterException(
        'تعذر فتح بيانات الجهاز. راجع صلاحية مجلد الحفظ والمساحة المتاحة.',
        cause: error,
        stackTrace: stackTrace,
      );
    }
  }

  static Future<CenterStore> _open({
    String? directory,
    Future<CenterState> Function()? initialState,
  }) async {
    sqfliteFfiInit();
    final base =
        directory ??
        p.join((await getApplicationSupportDirectory()).path, 'massar-center');
    await Directory(base).create(recursive: true);
    final file = p.normalize(p.absolute(p.join(base, 'center.sqlite')));
    if (!_openFiles.add(file)) {
      throw const CenterException(
        'بيانات السنتر مفتوحة بالفعل. استخدم نافذة التطبيق الموجودة.',
      );
    }
    RandomAccessFile? lock;
    Database? db;
    try {
      lock = await File('$file.lock').open(mode: FileMode.append);
      try {
        await lock.lock(FileLock.exclusive);
      } catch (error, stackTrace) {
        throw CenterException(
          'التطبيق يعمل بالفعل على هذا الجهاز. أغلق النسخة الأخرى أولًا.',
          cause: error,
          stackTrace: stackTrace,
        );
      }
      final databaseAlreadyExists = await File(file).exists();
      db = await databaseFactoryFfi.openDatabase(
        file,
        options: OpenDatabaseOptions(
          version: 1,
          onCreate: (db, version) async {
            if (databaseAlreadyExists) {
              throw const CenterException(
                'ملف بيانات موجود يحتاج مراجعة؛ لن يتم استبداله ببيانات التسطيب الأول.',
              );
            }
            final installedState = initialState == null
                ? CenterState()
                : await initialState();
            validateState(installedState);
            await db.execute(
              'CREATE TABLE state (id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL)',
            );
            await db.insert('state', {
              'id': 1,
              'payload': jsonEncode(installedState.toJson()),
            });
          },
          onUpgrade: (db, oldVersion, newVersion) async {
            throw const CenterException(
              'قاعدة البيانات تحتاج ترقية آمنة لم يدعمها هذا الإصدار. البيانات لم تُستبدل.',
            );
          },
          onDowngrade: (db, oldVersion, newVersion) async {
            throw const CenterException(
              'قاعدة البيانات تخص إصدارًا أحدث. افتح البرنامج الأحدث؛ لن تتم إعادة تهيئتها.',
            );
          },
          onConfigure: (db) async {
            await db.execute('PRAGMA journal_mode=WAL');
          },
        ),
      );
      await db.execute(_lanCommandsSchema);
      final row = await db.query('state', where: 'id = 1');
      if (row.length != 1) {
        throw const CenterException(
          'ملف البيانات غير مكتمل. استخدم نسخة احتياطية.',
        );
      }
      final state = CenterState.fromJson(
        jsonDecode(row.single['payload'] as String) as Map<String, dynamic>,
      );
      validateState(state);
      final store = CenterStore._(db, file, state, lock);
      await store._exclusive(
        () => store._preserveDataBeforeUpdate(
          existingDatabase: databaseAlreadyExists,
        ),
        operation: 'backup.before_update',
      );
      await store._migrateMonthPlans();
      await store._applyBundledDataRepair();
      await store._migrateStudyMonths();
      await store._applyHistoricalAcademicImport();
      store._startAutomaticBackups();
      return store;
    } catch (error, stackTrace) {
      await db?.close();
      await lock?.close();
      _openFiles.remove(file);
      if (error is CenterException) rethrow;
      throw CenterException(
        'تعذر قراءة بيانات الجهاز. لم تُحذف البيانات؛ راجع النسخ الاحتياطية.',
        cause: error,
        stackTrace: stackTrace,
      );
    }
  }

  List<CatalogEntry> get catalogs => List.unmodifiable(_state.catalogs);
  bool isCairoGroup(String groupId) {
    final group = groups.where((g) => g.id == groupId).firstOrNull;
    return group != null &&
        catalogs.any((c) => c.id == group.centerId && c.isCairo);
  }

  List<StudyGroup> groupsForRegion({bool cairo = false}) {
    final cairoCenters = catalogs
        .where((c) => c.isCairo)
        .map((c) => c.id)
        .toSet();
    return groups
        .where((g) => cairoCenters.contains(g.centerId) == cairo)
        .toList();
  }

  List<Student> studentsForRegion({bool cairo = false}) {
    final regionGroups = groupsForRegion(cairo: cairo).map((g) => g.id).toSet();
    return students
        .where(
          (s) => s.groupIds.isEmpty
              ? !cairo
              : s.groupIds.any(regionGroups.contains),
        )
        .toList();
  }

  List<StudyGroup> get groups => List.unmodifiable(_state.groups);
  List<Student> get students => List.unmodifiable(_state.students);
  List<LessonSession> get sessions => List.unmodifiable(_state.sessions);

  Student? studentById(String? id) {
    final index = _readIndex;
    return index != null
        ? index.studentsById[id]
        : _state.students.where((student) => student.id == id).firstOrNull;
  }

  LessonSession? sessionById(String? id) {
    final index = _readIndex;
    return index != null
        ? index.sessionsById[id]
        : _state.sessions.where((session) => session.id == id).firstOrNull;
  }

  StudyGroup? groupById(String? id) {
    final index = _readIndex;
    return index != null
        ? index.groupsById[id]
        : _state.groups.where((group) => group.id == id).firstOrNull;
  }

  List<Student> lookupReceptionStudents(String input) {
    if (_changingState) {
      return studentLookupCandidates(studentsForRegion(), input);
    }
    if (!identical(_lookupState, _state)) {
      if (_lookupState == null ||
          !listEquals(_lookupState!.students, _state.students) ||
          !listEquals(_lookupState!.groups, _state.groups) ||
          !listEquals(_lookupState!.catalogs, _state.catalogs)) {
        _receptionLookup = StudentLookupIndex(studentsForRegion());
      }
      _lookupState = _state;
    }
    return _receptionLookup!.candidates(input);
  }

  StudentHistoryRecords studentHistoryFor(String studentId) =>
      StudentHistoryRecords._(
        _readIndex ?? _CenterReadIndex(_state),
        studentId,
      );
  Set<String> get _voidAttendanceIds =>
      _state.corrections.map((e) => e.attendanceId).whereType<String>().toSet();
  Set<String> get _voidPaymentIds => _state.corrections
      .where((e) => e.voidsPayment)
      .map((e) => e.paymentId!)
      .toSet();
  Set<String> get _voidPackageIds => _state.corrections
      .where((e) => e.voidsPackage)
      .map((e) => e.packageId!)
      .toSet();
  Iterable<AttendanceRecord> get _activeAttendances {
    final indexed = _readIndex;
    if (indexed != null) return indexed.attendances;
    final voids = _voidAttendanceIds;
    return _state.attendances.where((e) => !voids.contains(e.id));
  }

  Iterable<PrepaidPackage> get _activePackages {
    final indexed = _readIndex;
    if (indexed != null) return indexed.packages;
    final voids = _voidPackageIds;
    return _state.packages.where((e) => !voids.contains(e.id));
  }

  String effectivePaymentMethod(String paymentId) {
    final edits = _state.corrections.where(
      (e) =>
          e.action == CorrectionAction.paymentMethod &&
          e.paymentId == paymentId,
    );
    return edits.lastOrNull?.newMethod ??
        _state.payments.firstWhere((e) => e.id == paymentId).method;
  }

  Iterable<PaymentRecord> get _financialPayments {
    final methods = <String, String>{};
    for (final edit in _state.corrections.where(
      (e) => e.action == CorrectionAction.paymentMethod,
    )) {
      methods[edit.paymentId!] = edit.newMethod!;
    }
    return _state.payments.map(
      (e) => methods.containsKey(e.id) ? e.copyWith(method: methods[e.id]!) : e,
    );
  }

  Iterable<PaymentRecord> get _activePayments {
    final indexed = _readIndex;
    if (indexed != null) return indexed.payments;
    final voids = _voidPaymentIds;
    return _financialPayments.where((e) => !voids.contains(e.id));
  }

  List<CorrectionRecord> get corrections =>
      List.unmodifiable(_state.corrections);
  List<RefundRecord> get refunds => List.unmodifiable(_state.refunds);
  List<AttendanceRecord> get allAttendances =>
      List.unmodifiable(_state.attendances);
  List<PaymentRecord> get allPayments => List.unmodifiable(_state.payments);
  List<CenterFeeRecord> get centerFees => List.unmodifiable(_state.centerFees);
  int centerFeeDueFor(String studentId, String sessionId) =>
      _centerFeeTotalDue(studentId, sessionId);
  int centerFeeCollectedFor(String studentId, String sessionId) =>
      _centerFeeCollected(studentId, sessionId);
  int centerFeeRemainingFor(String studentId, String sessionId) =>
      centerFeeDueFor(studentId, sessionId) -
      centerFeeCollectedFor(studentId, sessionId);
  List<PrepaidPackage> get allPackages => List.unmodifiable(_state.packages);
  List<SessionClosing> get allClosings => List.unmodifiable(_state.closings);
  List<PrepaidPackage> get packages =>
      _readIndex?.packages ?? List.unmodifiable(_activePackages);
  List<AttendanceRecord> get attendances =>
      _readIndex?.attendances ?? List.unmodifiable(_activeAttendances);
  List<PaymentRecord> get payments =>
      _readIndex?.payments ?? List.unmodifiable(_activePayments);
  List<AcademicRecord> get academics => List.unmodifiable(_state.academics);
  List<AcademicActivity> get academicActivities =>
      List.unmodifiable(_state.academicActivities);
  List<AuditRecord> get audit => List.unmodifiable(_state.audit);
  List<StaffUser> get staff => List.unmodifiable(_state.staff);
  List<PaymentReview> get reviews => List.unmodifiable(_state.reviews);
  List<SessionClosing> get closings {
    final reopened = _state.corrections
        .where((e) => e.action == CorrectionAction.closingReopened)
        .map((e) => e.closingId)
        .toSet();
    return List.unmodifiable(
      _state.closings.where((e) => !reopened.contains(e.id)),
    );
  }

  List<PaymentCheck> get paymentChecks =>
      List.unmodifiable(_state.paymentChecks);
  StaffUser? get currentUser =>
      Zone.current[_lanActorKey] as StaffUser? ?? _currentUser;
  bool get isRemote => false;
  bool get isClientWorkspace => false;
  bool get remoteConnected => true;
  Future<void> refreshRemote() async {}
  Future<void> prepareLanSwitch() async {}
  void _notifyLanCommit() => notifyListeners();
  void _notifyBackupStatus() => notifyListeners();
  bool get isReady => !_closed;
  bool get hasStaff => _state.staff.isNotEmpty;
  bool get canManage => currentUser?.role == StaffRole.admin;
  bool get canEditDiscount => currentUser != null;
  bool get canCollect => canManage || currentUser?.role == StaffRole.cashier;
  bool get canAssess => currentUser != null;
  CenterCardSettings get cardSettings => _state.cardSettings;
  List<StudentCardPayment> get cardPayments =>
      List.unmodifiable(_state.cardPayments);
  List<StudentCardReceipt> get cardReceipts =>
      List.unmodifiable(_state.cardReceipts);
  StudentCardPayment? cardPaymentFor(String studentId) {
    for (final payment in _state.cardPayments) {
      if (payment.studentId == studentId) return payment;
    }
    return null;
  }

  StudentCardReceipt? cardReceiptFor(String studentId) {
    for (final receipt in _state.cardReceipts) {
      if (receipt.studentId == studentId) return receipt;
    }
    return null;
  }

  bool get canConfigureCards {
    final owner = _installationAdmin;
    final actor = currentUser;
    return canManage &&
        owner != null &&
        actor != null &&
        (actor.id == owner.id ||
            actor.name.toLowerCase() == owner.name.toLowerCase());
  }

  bool canReceiveStudentCard(String studentId) =>
      canCollect &&
      _state.students.any((e) => e.id == studentId) &&
      !_student(studentId).isSuspended &&
      cardReceiptFor(studentId) == null &&
      (cardPaymentFor(studentId) != null ||
          !cardSettings.requirePaymentBeforeReceipt);

  Future<void> saveCardSettings(CenterCardSettings settings) => _change(
    'card_settings',
    'تعديل سعر الكارت وشرط الدفع قبل الاستلام',
    () async {
      if (!canConfigureCards) {
        throw const CenterException(
          'حساب المالك فقط يمكنه تعديل إعدادات الكارت.',
        );
      }
      if (settings.price != null && settings.price! < 0) {
        throw const CenterException('سعر الكارت لا يمكن أن يكون سالبًا.');
      }
      _state.cardSettings = settings;
    },
    auditDescription: () =>
        'إعدادات الكارت — السعر بالقرش: ${settings.price ?? 'غير محدد'}، اشتراط الدفع قبل الاستلام: ${settings.requirePaymentBeforeReceipt ? 'نعم' : 'لا'}',
  );

  Future<void> collectStudentCard({
    required String studentId,
    String method = 'نقدي',
    String? sessionId,
    int? paidAmount,
    int? expectedNetAmount,
  }) => _change(
    'card_payment',
    'تحصيل رسوم كارت الطالب',
    () async {
      _require(canCollect);
      final student = _student(studentId);
      _requireActiveStudent(student);
      if (cardPaymentFor(studentId) != null) {
        throw const CenterException('تم تسجيل دفع الكارت لهذا الطالب بالفعل.');
      }
      if (cardReceiptFor(studentId) != null) {
        throw const CenterException(
          'الكارت مستلم بالفعل؛ لا يمكن تحصيل رسوم جديدة له.',
        );
      }
      final price = cardSettings.price;
      if (price == null) {
        throw const CenterException('يجب أن يحدد المالك سعر الكارت أولًا.');
      }
      if (method.trim().isEmpty) {
        throw const CenterException('حدد وسيلة الدفع.');
      }
      final due = _studentDiscounted(student, price);
      _validateExpectedNet(due, expectedNetAmount);
      _validatedPaidAmount(due, paidAmount);
      String? groupId;
      if (sessionId != null) {
        final session = _session(sessionId);
        if (session.status != SessionStatus.open) {
          throw const CenterException('تحصيل الكارت مرتبط بحصة مفتوحة فقط.');
        }
        groupId = session.groupId;
      }
      _state.cardPayments.add(
        StudentCardPayment(
          id: _uuid.v4(),
          studentId: student.id,
          groupId: groupId,
          sessionId: sessionId,
          baseAmount: price,
          discountPercent: student.discountPercent,
          netAmount: _studentDiscounted(student, price),
          paidAmount: paidAmount,
          method: method.trim(),
          createdAt: DateTime.now(),
          staffId: currentUser!.id,
        ),
      );
    },
    auditDescription: () =>
        'تحصيل رسوم كارت الطالب — كود ${_student(studentId).code} — ${_collectionAudit(_state.cardPayments.last.netAmount, _state.cardPayments.last.collectedAmount, _state.cardPayments.last.id)}',
  );

  Future<void> receiveStudentCard(String studentId) => _change(
    'card_receipt',
    'تسجيل استلام كارت الطالب',
    () async {
      _require(canCollect);
      final student = _student(studentId);
      _requireActiveStudent(student);
      if (cardReceiptFor(studentId) != null) {
        throw const CenterException(
          'تم تسجيل استلام الكارت لهذا الطالب بالفعل.',
        );
      }
      final payment = cardPaymentFor(studentId);
      if (payment == null && cardSettings.requirePaymentBeforeReceipt) {
        throw const CenterException('سجل دفع الكارت أولًا قبل الاستلام.');
      }
      _state.cardReceipts.add(
        StudentCardReceipt(
          id: _uuid.v4(),
          studentId: student.id,
          paymentId: payment?.id,
          receivedAt: DateTime.now(),
          staffId: currentUser!.id,
          paymentBypassed: payment == null,
        ),
      );
    },
    auditDescription: () =>
        'تسجيل استلام كارت الطالب — كود ${_student(studentId).code}',
  );

  Future<void> settleDebt({
    required String paymentId,
    required DebtKind kind,
    required int amount,
    String method = 'نقدي',
    String? sessionId,
    String notes = '',
  }) => _change(
    'debt_settle',
    'تسديد مديونية طالب',
    () async {
      _require(canCollect);
      if (amount <= 0 || method.trim().isEmpty || notes.length > 2000) {
        throw const CenterException(
          'أدخل مبلغ تسديد موجبًا ووسيلة دفع وملاحظة لا تتجاوز ٢٠٠٠ حرف.',
        );
      }
      final String studentId;
      final int remaining;
      if (kind == DebtKind.lesson) {
        final payment = _activePayment(paymentId);
        studentId = payment.studentId;
        remaining = paymentDebtFor(paymentId);
      } else {
        final payment = _find<StudentCardPayment>(
          _state.cardPayments,
          (e) => e.id == paymentId,
          'عملية دفع الكارت غير موجودة.',
        );
        studentId = payment.studentId;
        remaining = cardDebtFor(paymentId);
      }
      if (amount > remaining) {
        throw const CenterException(
          'مبلغ التسديد أكبر من المديونية المتبقية؛ راجع الرصيد.',
        );
      }
      if (sessionId != null) {
        if (_session(sessionId).status != SessionStatus.open) {
          throw const CenterException('اربط التسديد بحصة تحصيل مفتوحة فقط.');
        }
        _requireFinancialOpen(sessionId);
      }
      _state.debtSettlements.add(
        DebtSettlement(
          id: _uuid.v4(),
          studentId: studentId,
          paymentId: paymentId,
          kind: kind,
          amount: amount,
          method: method.trim(),
          sessionId: sessionId,
          notes: notes.trim(),
          createdAt: DateTime.now(),
          staffId: currentUser!.id,
        ),
      );
    },
    auditDescription: () {
      final settlement = _state.debtSettlements.last;
      return 'تسديد مديونية — كود ${_student(settlement.studentId).code} — ${_auditAmount(amount)} — عملية $paymentId — سجل ${settlement.id}';
    },
  );

  void _require(bool allowed) {
    if (!allowed) {
      throw const CenterException(
        'ليس لديك صلاحية لهذا الإجراء.',
        diagnosticCode: 1101,
      );
    }
  }

  T _find<T>(Iterable<T> values, bool Function(T) match, String message) {
    for (final value in values) {
      if (match(value)) return value;
    }
    throw CenterException(message);
  }

  Student _student(String id) =>
      _readIndex?.studentsById[id] ??
      _find(_state.students, (e) => e.id == id, 'الطالب غير موجود.');
  StudyGroup _group(String id) =>
      _find(_state.groups, (e) => e.id == id, 'المجموعة غير موجودة.');
  LessonSession _session(String id) =>
      _readIndex?.sessionsById[id] ??
      _find(_state.sessions, (e) => e.id == id, 'الحصة غير موجودة.');
  void _log(String action, String description, String actorId) =>
      _state.audit.add(
        AuditRecord(
          id: _uuid.v4(),
          action: action,
          description: description,
          staffId: actorId,
          createdAt: DateTime.now(),
        ),
      );

  Future<void> _exclusive(
    Future<void> Function() work, {
    String operation = 'database.operation',
  }) {
    if (Zone.current[_lanExecutionKey] == this) return work();
    final future = _queue.then((_) async {
      try {
        if (_closed) throw const CenterException('تم إغلاق ملف البيانات.');
        await work();
      } catch (error, stackTrace) {
        reportProblem(error, stackTrace, operation: operation);
        if (error is CenterException) rethrow;
        throw CenterException(
          'تعذر الوصول لملف البيانات أو النسخة الاحتياطية. لم تكتمل العملية.',
          cause: error,
          stackTrace: stackTrace,
        );
      }
    });
    _queue = future.catchError((Object _) {});
    return future;
  }

  Future<void> _change(
    String action,
    String description,
    Future<void> Function() work, {
    String Function()? auditDescription,
    String? actorId,
    bool Function()? commitWhen,
  }) => _exclusive(() async {
    if (isRemote) {
      throw const CenterException(
        'هذه العملية يجب تنفيذها على الجهاز الرئيسي.',
      );
    }
    final previous = _state;
    final previousUser = _currentUser;
    final actor = currentUser;
    final authenticationRevision = _authenticationRevision;
    final authorize = Zone.current[_lanAuthorizationKey] as bool Function()?;
    if (authorize != null && !authorize()) {
      throw const LanAuthorizationException(
        'جلسة الموظف انتهت. سجّل الدخول من جديد.',
      );
    }
    _state = previous.copyForMutation();
    _changingState = true;
    _cachedReadIndex = null;
    try {
      await work();
      if (authorize != null && !authorize()) {
        throw const LanAuthorizationException(
          'جلسة الموظف انتهت. سجّل الدخول من جديد.',
        );
      }
      if (commitWhen != null && !commitWhen()) {
        _state = previous;
        return;
      }
      _log(
        action,
        auditDescription?.call() ?? description,
        actorId ?? actor?.id ?? _currentUser?.id ?? '',
      );
      validateState(_state);
      Future<void> persist(DatabaseExecutor tx) async {
        final updated = await tx.update('state', {
          'payload': _stateEncoder.encode(_state),
        }, where: 'id = 1');
        if (updated != 1) {
          throw const CenterException(
            'سجل قاعدة البيانات مفقود؛ لم تُحفظ العملية.',
          );
        }
      }

      final lanTransaction = Zone.current[_lanTransactionKey] as Transaction?;
      if (lanTransaction == null) {
        await _database!.transaction(persist);
        notifyListeners();
      } else {
        await persist(lanTransaction);
      }
    } catch (error, stackTrace) {
      _state = previous;
      if (_authenticationRevision == authenticationRevision) {
        _currentUser = previousUser;
      }
      if (error is CenterException) rethrow;
      throw CenterException(
        'تعذر حفظ العملية. لم يتغير الحساب أو الحضور.',
        cause: error,
        stackTrace: stackTrace,
      );
    } finally {
      _changingState = false;
      _cachedReadIndex = null;
    }
  }, operation: action);

  Future<Map<String, String>> _hashPassword(String password) async {
    if (password.length < 8) {
      throw const CenterException('كلمة المرور يجب أن تكون ٨ أحرف على الأقل.');
    }
    final random = Random.secure();
    final salt = List<int>.generate(24, (_) => random.nextInt(256));
    final key = await _passwordAlgorithm.deriveKey(
      secretKey: SecretKey(utf8.encode(password)),
      nonce: salt,
    );
    return {
      'salt': base64Encode(salt),
      'hash': base64Encode(await key.extractBytes()),
      'algorithm': 'pbkdf2-sha256-120000',
    };
  }

  Future<void> setupAdmin(String name, String password) => _change(
    'setup_admin',
    'إعداد مدير الجهاز',
    () async {
      if (hasStaff) throw const CenterException('تم إعداد الجهاز بالفعل.');
      if (name.trim().isEmpty) throw const CenterException('أدخل اسم المدير.');
      final credential = await _hashPassword(password);
      final user = StaffUser(
        id: _uuid.v4(),
        name: name.trim(),
        role: StaffRole.admin,
      );
      _state.staff.add(user);
      _state.credentials[user.id] = credential;
      _currentUser = user;
    },
  );
  Future<StaffUser> _authenticate(String name, String password) async {
    final candidates = _state.staff.where(
      (e) => e.name.toLowerCase() == name.trim().toLowerCase(),
    );
    if (candidates.isEmpty) {
      throw const CenterException(
        'اسم المستخدم أو كلمة المرور غير صحيحة.',
        diagnosticCode: 1102,
      );
    }
    final user = candidates.single;
    final secret = _state.credentials[user.id]!;
    final key = await _passwordAlgorithm.deriveKey(
      secretKey: SecretKey(utf8.encode(password)),
      nonce: base64Decode(secret['salt']!),
    );
    final actual = await key.extractBytes();
    final expected = base64Decode(secret['hash']!);
    var difference = actual.length ^ expected.length;
    for (var i = 0; i < min(actual.length, expected.length); i++) {
      difference |= actual[i] ^ expected[i];
    }
    if (difference != 0) {
      throw const CenterException(
        'اسم المستخدم أو كلمة المرور غير صحيحة.',
        diagnosticCode: 1102,
      );
    }
    return user;
  }

  Future<void> signIn(String name, String password) {
    final revision = _logoutRevision;
    return _exclusive(() async {
      final user = await _authenticate(name, password);
      if (_logoutRevision != revision) return;
      _currentUser = user;
      _authenticationRevision++;
      notifyListeners();
    }, operation: 'auth.sign_in');
  }

  void signOut() {
    _logoutRevision++;
    _authenticationRevision++;
    _currentUser = null;
    notifyListeners();
  }

  Future<void> saveStaff({
    required String name,
    required String password,
    required StaffRole role,
  }) => _change('staff_create', 'إضافة موظف', () async {
    _require(canManage);
    if (name.trim().isEmpty ||
        _state.staff.any(
          (e) => e.name.toLowerCase() == name.trim().toLowerCase(),
        )) {
      throw const CenterException('اسم الموظف مطلوب ويجب ألا يتكرر.');
    }
    final user = StaffUser(id: _uuid.v4(), name: name.trim(), role: role);
    _state.credentials[user.id] = await _hashPassword(password);
    _state.staff.add(user);
  });

  void _replace<T>(
    List<T> list,
    String id,
    T value,
    String Function(T) identify,
  ) {
    final index = list.indexWhere((e) => identify(e) == id);
    if (index < 0) {
      list.add(value);
    } else {
      list[index] = value;
    }
  }

  Future<void> saveCatalog(
    CatalogEntry entry,
  ) => _change('catalog_save', 'حفظ ${entry.name}', () async {
    _require(canManage);
    if (entry.name.trim().isEmpty) throw const CenterException('أدخل الاسم.');
    if (entry.id.isNotEmpty && !_state.catalogs.any((e) => e.id == entry.id)) {
      throw const CenterException('العنصر غير موجود.');
    }
    if (_state.catalogs.any(
      (e) =>
          e.id != entry.id &&
          e.kind == entry.kind &&
          e.name == entry.name.trim(),
    )) {
      throw const CenterException('الاسم مسجل بالفعل.');
    }
    if (entry.id.isNotEmpty &&
        _state.catalogs.firstWhere((e) => e.id == entry.id).kind !=
            entry.kind) {
      throw const CenterException('لا يمكن تغيير نوع العنصر.');
    }
    final saved = entry.copyWith(
      id: entry.id.isEmpty ? _uuid.v4() : entry.id,
      name: entry.name.trim(),
    );
    _replace(_state.catalogs, saved.id, saved, (e) => e.id);
  });
  Future<void> saveGroup(
    StudyGroup group,
  ) => _change('group_save', 'حفظ مجموعة ${group.name}', () async {
    _require(canManage);
    if (group.id.isNotEmpty) {
      final previous = _group(group.id);
      final identityChanged =
          previous.subjectId != group.subjectId ||
          previous.centerId != group.centerId ||
          previous.gradeId != group.gradeId;
      final hasHistory =
          _state.sessions.any((session) => session.groupId == group.id) ||
          _activePayments.any((payment) => payment.groupId == group.id) ||
          _state.attendances.any(
            (record) => record.makeupSourceGroupId == group.id,
          );
      if (identityChanged && hasHistory) {
        throw const CenterException(
          'لا يمكن تغيير مادة أو سنتر أو صف مجموعة لها حصص أو مدفوعات أو سجل تعويض. أنشئ مجموعة جديدة للحفاظ على السجل السابق.',
        );
      }
    }
    final saved = group.copyWith(
      id: group.id.isEmpty ? _uuid.v4() : group.id,
      name: group.name.trim(),
      monthPlans: _savedMonthPlans(group),
    );
    _replace(_state.groups, saved.id, saved, (e) => e.id);
  });

  Future<void> updateMonthForGroups({
    required GroupMonthPlan plan,
    required Map<String, GroupMonthPlan> expectedPlans,
  }) => _updateMonthForGroups(plan: plan, expectedPlans: expectedPlans);

  Future<void> setStudentPackageMember({
    required String studentId,
    required bool enabled,
    String? sessionId,
  }) => _setStudentPackageMember(
    studentId: studentId,
    enabled: enabled,
    sessionId: sessionId,
  );

  Future<void> setStudentCenterFee({
    required String studentId,
    required bool enabled,
  }) => _setStudentCenterFee(studentId: studentId, enabled: enabled);

  Future<void> collectStudentCenterFee({
    required String studentId,
    required String sessionId,
  }) => _collectStudentCenterFee(studentId: studentId, sessionId: sessionId);

  Future<void> saveStudentCenterOnly({
    required String studentId,
    required bool enabled,
    int amount = 1500,
  }) => _saveStudentCenterOnly(
    studentId: studentId,
    enabled: enabled,
    amount: amount,
  );

  Future<void> saveMonthForGroups({
    required GroupMonthPlan plan,
    required List<String> groupIds,
  }) {
    final selectedIds = List<String>.unmodifiable(groupIds);
    return _change(
      'month_save_groups',
      'إضافة شهر إلى المجموعات',
      () async {
        _require(canManage);
        _addMonthForGroups(plan, selectedIds);
      },
      auditDescription: () =>
          'إضافة شهر ${plan.name.trim()} — ${plan.sessions} حصص — السعر ${_auditAmount(plan.price)} — المجموعات ${selectedIds.join(', ')}',
    );
  }

  /// Proposed code for display; registration allocates again inside the transaction.
  String get nextStudentCode {
    var next = BigInt.from(1001);
    const arabic = '٠١٢٣٤٥٦٧٨٩';
    const persian = '۰۱۲۳۴۵۶۷۸۹';
    for (final student in _state.students) {
      var code = student.code.trim();
      for (var digit = 0; digit < 10; digit++) {
        code = code
            .replaceAll(arabic[digit], '$digit')
            .replaceAll(persian[digit], '$digit');
      }
      if (!RegExp(r'^[0-9]+$').hasMatch(code)) continue;
      final number = BigInt.parse(code);
      if (number >= next) next = number + BigInt.one;
    }
    return next.toString();
  }

  /// Enrolls a new student with an automatically allocated, permanent code.
  Future<Student> registerStudent(Student draft) async {
    late Student saved;
    await _change(
      'student_save',
      'تسجيل الطالب ${draft.name}',
      () async {
        if (draft.id.isNotEmpty) {
          throw const CenterException(
            'الطالب مسجل بالفعل؛ عدّل بياناته بدل تسجيله من جديد.',
          );
        }
        saved = _saveStudentRecord(draft, generateCode: true);
      },
      auditDescription: () => _studentProfileAudit(saved),
    );
    return saved;
  }

  /// Updates a profile or retains explicit codes in trusted legacy records.
  /// User-facing enrollment always uses registerStudent.
  Future<void> saveStudent(
    Student student, {
    bool preserveDiscount = false,
    bool preserveCenterOnly = true,
  }) {
    late Student saved;
    return _change(
      'student_save',
      'حفظ الطالب ${student.name}',
      () async {
        final current = student.id.isEmpty ? null : _student(student.id);
        saved = _saveStudentRecord(
          preserveDiscount && current != null
              ? student.copyWith(
                  discountPercent: current.discountPercent,
                  discountNeedsReview: current.discountNeedsReview,
                )
              : student,
          allowCenterOnlyChanges: !preserveCenterOnly,
        );
      },
      auditDescription: () => _studentProfileAudit(saved),
    );
  }

  Future<void> suspendStudent({
    required String studentId,
    required String reason,
  }) => _change(
    'student_suspend',
    'إيقاف الطالب',
    () async {
      _require(canCollect);
      final student = _student(studentId);
      if (reason.trim().isEmpty || reason.trim().length > 2000) {
        throw const CenterException(
          'سبب إيقاف الطالب مطلوب ولا يتجاوز ٢٠٠٠ حرف.',
        );
      }
      if (student.isSuspended) {
        throw const CenterException(
          'الطالب موقوف بالفعل؛ أعد تفعيله قبل إيقافه مجددًا.',
        );
      }
      _replace(
        _state.students,
        student.id,
        student.copyWith(
          isSuspended: true,
          suspensionReason: reason.trim(),
          suspendedAt: DateTime.now(),
          suspendedBy: currentUser!.id,
        ),
        (e) => e.id,
      );
    },
    auditDescription: () =>
        'إيقاف الطالب — كود ${_student(studentId).code} — السبب ${reason.trim()}',
  );

  Future<void> reactivateStudent(String studentId) {
    var changed = false;
    return _change(
      'student_reactivate',
      'إعادة تفعيل الطالب',
      () async {
        _require(canCollect);
        final student = _student(studentId);
        if (!student.isSuspended) return;
        _replace(
          _state.students,
          student.id,
          student.copyWith(isSuspended: false),
          (e) => e.id,
        );
        changed = true;
      },
      commitWhen: () => changed,
      auditDescription: () =>
          'إعادة تفعيل الطالب — كود ${_student(studentId).code} — آخر سبب إيقاف ${_student(studentId).suspensionReason}',
    );
  }

  Future<void> transferStudent(
    String studentId,
    String fromGroupId,
    String toGroupId,
  ) {
    var transferred = false;
    return _change(
      'student_transfer',
      'نقل الطالب بين المجموعات',
      () async {
        _require(canCollect);
        final student = _student(studentId);
        _group(fromGroupId);
        _group(toGroupId);
        if (fromGroupId == toGroupId) {
          throw const CenterException('اختر مجموعة مختلفة للنقل.');
        }
        if (!student.groupIds.contains(fromGroupId)) {
          if (student.groupIds.contains(toGroupId)) return;
          throw const CenterException(
            'تغيّرت مجموعة الطالب؛ راجع عضويته قبل النقل.',
          );
        }
        final alreadyEnrolled = student.groupIds.contains(toGroupId);
        final memberships = student.groupIds
            .where((id) => id != fromGroupId)
            .toList();
        if (!alreadyEnrolled) memberships.add(toGroupId);
        _replace(
          _state.students,
          studentId,
          student.copyWith(groupIds: memberships),
          (e) => e.id,
        );
        if (!alreadyEnrolled) {
          _state.enrollments['$studentId:$toGroupId'] = DateTime.now()
              .toIso8601String();
        }
        transferred = true;
      },
      commitWhen: () => transferred,
      auditDescription: () =>
          'نقل الطالب — كود ${_student(studentId).code} — من $fromGroupId إلى $toGroupId — العضوية فقط دون نقل الأرصدة أو تعديل السجل',
    );
  }

  Student _saveStudentRecord(
    Student student, {
    bool generateCode = false,
    bool allowCenterOnlyChanges = false,
  }) {
    _require(canCollect);
    final old = student.id.isEmpty ? null : _student(student.id);
    if (old != null && student.code.trim() != old.code.trim()) {
      throw const CenterException(
        'كود الطالب ثابت ولا يمكن تغييره بعد التسجيل.',
      );
    }
    final linksTwin =
        student.twinStudentId != null &&
        student.twinStudentId != old?.twinStudentId;
    if (!canEditDiscount &&
        !linksTwin &&
        student.discountPercent != (old?.discountPercent ?? 0)) {
      throw const CenterException('سجل دخولك لتعديل نسبة الخصم.');
    }
    final id = old?.id ?? _uuid.v4();
    final code =
        old?.code ??
        (generateCode || student.code.trim().isEmpty
            ? nextStudentCode
            : student.code.trim());
    final saved = student.copyWith(
      centerFeeEnabled: old?.centerFeeEnabled ?? student.centerFeeEnabled,
      packageMember: old?.packageMember ?? student.packageMember,
      centerOnly: allowCenterOnlyChanges
          ? student.centerOnly
          : old?.centerOnly ?? student.centerOnly,
      centerFeeAmount: allowCenterOnlyChanges
          ? student.centerFeeAmount
          : old?.centerFeeAmount ?? student.centerFeeAmount,
      id: id,
      code: code,
      barcode: old?.barcode ?? student.barcode.trim(),
      isSuspended: old?.isSuspended ?? false,
      suspensionReason: old?.suspensionReason ?? '',
      suspendedAt: old?.suspendedAt,
      suspendedBy: old?.suspendedBy,
      clearSuspensionMetadata: old?.suspendedAt == null,
      discountPercent:
          linksTwin ||
              (allowCenterOnlyChanges
                  ? student.centerOnly
                  : old?.centerOnly ?? student.centerOnly)
          ? 100
          : student.discountPercent,
      discountNeedsReview:
          linksTwin ||
              old != null &&
                  canEditDiscount &&
                  (old.discountPercent != student.discountPercent ||
                      !student.discountNeedsReview)
          ? false
          : old?.discountNeedsReview ?? student.discountNeedsReview,
      name: student.name.trim(),
      groupIds: List.unmodifiable(student.groupIds),
      createdAt: old?.createdAt ?? student.createdAt,
      createdAtKnown: old?.createdAtKnown ?? student.createdAtKnown,
    );
    _syncTwinRelationship(old, saved);
    _replace(_state.students, id, saved, (e) => e.id);
    for (final group in saved.groupIds) {
      if (old == null || !old.groupIds.contains(group)) {
        _state.enrollments['$id:$group'] =
            (old == null ? student.createdAt : DateTime.now())
                .toIso8601String();
      }
    }
    return saved;
  }

  Future<void> saveStudentDiscount({
    required String studentId,
    required num percent,
    bool? centerOnly,
    int? centerFeeAmount,
  }) {
    num? previousPercent;
    num? savedPercent;
    return _change(
      'student_discount',
      'تعديل الخصم الثابت للطالب',
      () async {
        _require(canEditDiscount);
        if (!percent.isFinite || percent < 0 || percent > 100) {
          throw const CenterException('نسبة الخصم من صفر إلى ١٠٠٪.');
        }
        final student = _student(studentId);
        final fee = centerFeeAmount ?? student.centerFeeAmount;
        final onlyCenter = centerOnly ?? (student.centerOnly && percent == 100);
        if (fee <= 0 || onlyCenter && percent != 100) {
          throw const CenterException(
            'نظام السنتر فقط يتطلب إعفاء ١٠٠٪ ورسوم سنتر موجبة.',
          );
        }
        previousPercent = student.discountPercent;
        savedPercent = percent == percent.round() ? percent.round() : percent;
        _replace(
          _state.students,
          student.id,
          student.copyWith(
            discountPercent: savedPercent,
            discountNeedsReview: false,
            centerOnly: onlyCenter,
            centerFeeAmount: fee,
          ),
          (e) => e.id,
        );
      },
      auditDescription: () =>
          'تعديل الخصم الثابت — كود ${_student(studentId).code} — من $previousPercent٪ إلى $savedPercent٪',
    );
  }

  Future<void> saveStudentNote({
    required String studentId,
    required String notes,
  }) => _change(
    'student_note',
    'حفظ ملاحظات الطالب',
    () async {
      _require(canCollect);
      if (notes.length > 4000) {
        throw const CenterException('ملاحظات الطالب لا تتجاوز ٤٠٠٠ حرف.');
      }
      // Resolve inside the serialized transaction, never overwrite a stale form.
      final student = _student(studentId);
      _replace(
        _state.students,
        student.id,
        student.copyWith(notes: notes.trim()),
        (e) => e.id,
      );
    },
    auditDescription: () =>
        'حفظ ملاحظات الطالب — كود ${_student(studentId).code}',
  );

  int nextSessionNumber(String? groupId, {int monthNumber = 1}) =>
      sessions
          .where(
            (session) =>
                session.groupId == groupId &&
                session.monthNumber == monthNumber,
          )
          .fold(
            0,
            (largest, session) =>
                session.number > largest ? session.number : largest,
          ) +
      1;

  List<StudyMonth> get studyMonths => List.unmodifiable(_state.studyMonths);

  StudyMonth? studyMonthForLesson(String lessonId) => _state.studyMonths
      .where((m) => m.lessons.any((l) => l.id == lessonId))
      .firstOrNull;

  LessonSession? sessionForPreparedLesson(
    String groupId,
    String preparedLessonId,
  ) => _state.sessions
      .where(
        (s) => s.groupId == groupId && s.preparedLessonId == preparedLessonId,
      )
      .firstOrNull;

  Future<StudyMonth> saveStudyMonth(StudyMonth month) => _saveStudyMonth(month);

  Future<LessonSession> startPreparedLesson({
    required String groupId,
    required String preparedLessonId,
  }) => _startPreparedLesson(
    groupId: groupId,
    preparedLessonId: preparedLessonId,
  );

  Future<void> saveSession(LessonSession session) => _change(
    'session_save',
    'حفظ الشهر ${session.monthNumber} — الحصة ${session.number}',
    () async {
      _saveSessionRecord(session);
    },
  );

  bool sessionHasStarted(String sessionId) {
    final session = _session(sessionId);
    return session.startedAt != null ||
        _state.attendances.any(
          (entry) =>
              entry.sessionId == sessionId &&
              entry.status != AttendanceStatus.absent,
        ) ||
        _activePayments.any((payment) => payment.sessionId == sessionId);
  }

  Future<void> startSession(String sessionId) {
    var started = false;
    return _change(
      'session_start',
      'بدء الحصة للتحضير',
      () async {
        _require(canCollect);
        final session = _session(sessionId);
        if (session.status != SessionStatus.open) {
          throw const CenterException(
            'يمكن بدء حصة مفتوحة فقط؛ الحصة مغلقة أو ملغاة.',
          );
        }
        if (sessionHasStarted(sessionId)) return;
        _replace(
          _state.sessions,
          session.id,
          session.copyWith(
            startedAt: DateTime.now(),
            startedBy: currentUser!.id,
          ),
          (e) => e.id,
        );
        started = true;
      },
      commitWhen: () => started,
      auditDescription: () =>
          'بدء الحصة ${_session(sessionId).number} — ${groupLabel(_session(sessionId).groupId)}',
    );
  }

  Future<void> createGroupSessions({
    required List<String> groupIds,
    required SessionKind kind,
    int extraPrice = 0,
    int monthNumber = 1,
  }) {
    final selectedIds = List<String>.unmodifiable(groupIds);
    final created = <LessonSession>[];
    return _change(
      'sessions_create',
      'إنشاء حصص للمجموعات',
      () async {
        _require(canManage);
        if (monthNumber <= 0) {
          throw const CenterException('رقم الشهر يجب أن يكون موجبًا.');
        }
        if (selectedIds.isEmpty ||
            selectedIds.toSet().length != selectedIds.length) {
          throw const CenterException('اختر مجموعات مختلفة لإنشاء الحصص.');
        }
        final savedAt = DateTime.now();
        for (final groupId in selectedIds) {
          _group(groupId);
          final session = LessonSession(
            groupId: groupId,
            number: nextSessionNumber(groupId, monthNumber: monthNumber),
            monthNumber: monthNumber,
            startsAt: savedAt,
            createdAt: savedAt,
            kind: kind,
            extraPrice: kind == SessionKind.extra ? extraPrice : 0,
          );
          _saveSessionRecord(session);
          created.add(session);
        }
      },
      auditDescription: () =>
          'إنشاء ${created.length} حصص — ${created.map((session) => '${groupLabel(session.groupId)}: الشهر ${session.monthNumber} — حصة ${session.number}').join('؛ ')}',
    );
  }

  void _saveSessionRecord(LessonSession session) {
    _require(canManage);
    final old = session.id.isEmpty ? null : _session(session.id);
    if (old != null) {
      if (_state.paymentChecks.any((e) => e.sessionId == old.id)) {
        throw const CenterException(
          'لا يمكن تعديل حصة تمت مراجعة الدفع بالكود فيها؛ أنشئ حصة جديدة للحفاظ على سجل المراجعة.',
        );
      }
      if (old.status != SessionStatus.open ||
          _state.attendances.any((e) => e.sessionId == old.id) ||
          _state.payments.any((e) => e.sessionId == old.id) ||
          _state.cardPayments.any((e) => e.sessionId == old.id) ||
          _state.debtSettlements.any((e) => e.sessionId == old.id) ||
          _state.academics.any((e) => e.sessionId == old.id) ||
          _state.academicActivities.any((e) => e.sessionId == old.id) ||
          _state.closings.any((e) => e.sessionId == old.id)) {
        throw const CenterException(
          'لا يمكن تعديل حصة بها تسجيلات أو حصة مغلقة.',
        );
      }
    }
    if (session.status != SessionStatus.open) {
      throw const CenterException('استخدم إغلاق أو إلغاء الحصة لتغيير حالتها.');
    }
    if (session.kind == SessionKind.counted &&
        _state.sessions.any(
          (e) =>
              e.id != session.id &&
              e.groupId == session.groupId &&
              e.kind == SessionKind.counted &&
              e.status != SessionStatus.canceled &&
              _precedes(session, e) &&
              (e.status == SessionStatus.closed ||
                  _activeAttendances.any((a) => a.sessionId == e.id)),
        )) {
      throw const CenterException(
        'لا يمكن إضافة حصة محسوبة قبل حصة تم تسجيلها؛ سيؤثر ذلك على ترتيب الباقات.',
      );
    }
    final saved = session.copyWith(
      id: session.id.isEmpty ? _uuid.v4() : session.id,
      startedAt: old?.startedAt,
      startedBy: old?.startedBy,
      clearStarted: old?.startedAt == null,
    );
    _replace(_state.sessions, saved.id, saved, (e) => e.id);
  }

  bool _precedes(LessonSession first, LessonSession second) =>
      first.startsAt.isBefore(second.startsAt) ||
      (first.startsAt.isAtSameMomentAs(second.startsAt) &&
          (first.monthNumber < second.monthNumber ||
              (first.monthNumber == second.monthNumber &&
                  first.number < second.number)));

  void _requirePreviousClosed(LessonSession session) {
    if (session.kind == SessionKind.counted &&
        _state.sessions.any(
          (e) =>
              e.id != session.id &&
              e.groupId == session.groupId &&
              e.kind == SessionKind.counted &&
              e.status == SessionStatus.open &&
              sessionHasStarted(e.id) &&
              _precedes(e, session),
        )) {
      throw const CenterException(
        'أغلق الحصة السابقة أولًا حتى تُحسب الباقات بالترتيب.',
      );
    }
  }

  int _studentDiscounted(Student student, int amount) {
    return discountedAmount(amount, student.discountPercent);
  }

  List<PrepaidPackage> _available(
    String studentId,
    String groupId, {
    LessonSession? forClosure,
  }) {
    final result = _activePackages.where((package) {
      if (package.studentId != studentId ||
          package.groupId != groupId ||
          package.remaining < 1) {
        return false;
      }
      if (forClosure == null ||
          !package.purchasedAt.isAfter(forClosure.startsAt)) {
        return true;
      }
      return _activePayments.any(
        (e) => e.id == package.paymentId && e.sessionId == forClosure.id,
      );
    }).toList()..sort((a, b) => a.purchasedAt.compareTo(b.purchasedAt));
    return result;
  }

  void _consume(PrepaidPackage package) {
    final index = _state.packages.indexWhere((e) => e.id == package.id);
    _state.packages[index] = package.copyWith(remaining: package.remaining - 1);
  }

  void _requirePackageSessions(int sessions) {
    if (sessions != 2 && sessions != 3 && sessions != 4) {
      throw const CenterException('اختر باقة حصتين أو ٣ أو ٤ حصص.');
    }
  }

  String _packageLabel(int sessions) => switch (sessions) {
    2 => 'باقة حصتين',
    3 => 'باقة ٣ حصص',
    _ => 'باقة ٤ حصص',
  };

  int _configuredPackageAmount(StudyGroup group, int sessions) {
    _requireGroupPrices(group);
    _requirePackageSessions(sessions);
    final amount = group.packageAmountFor(sessions);
    if (amount == null) {
      throw CenterException(
        'حدد سعر ${_packageLabel(sessions)} في المجموعة أولًا.',
      );
    }
    return amount;
  }

  void _requireGroupPrices(StudyGroup group) {
    if (!group.priceConfigured) {
      throw const CenterException(
        'حدد أسعار المجموعة المستوردة قبل تسجيل دفع جديد.',
      );
    }
  }

  int _configuredSessionAmount(StudyGroup group) {
    _requireGroupPrices(group);
    return group.sessionPrice;
  }

  PrepaidPackage _buyPackage(
    Student student,
    StudyGroup group,
    String method,
    String notes, {
    String? sessionId,
    int sessions = 4,
    int? paidAmount,
    int? expectedNetAmount,
    String? monthPlanId,
    GroupMonthPlan? expectedMonthPlan,
  }) {
    if (student.centerOnly) {
      throw const CenterException(
        'الطالب معفى من دفع المدرس؛ التحضير يخص رسوم السنتر فقط.',
      );
    }
    final purchase = _monthPurchaseFor(group, sessions, monthPlanId);
    if (expectedMonthPlan != null && expectedMonthPlan != purchase.plan) {
      throw const CenterException(
        'تغيّر اسم الشهر أو عدد حصصه أو سعره؛ راجع الاختيار قبل الدفع.',
      );
    }
    final baseAmount = purchase.baseAmount;
    final due = _studentDiscounted(student, baseAmount);
    _validateExpectedNet(due, expectedNetAmount);
    _validatedPaidAmount(due, paidAmount);
    final packageId = _uuid.v4();
    final paymentId = _uuid.v4();
    final now = DateTime.now();
    final package = PrepaidPackage(
      id: packageId,
      studentId: student.id,
      groupId: group.id,
      purchasedAt: now,
      remaining: purchase.sessions,
      totalSessions: purchase.sessions,
      monthPlanId: purchase.plan?.id,
      monthPlanName: purchase.plan?.name,
      paymentId: paymentId,
    );
    _state.packages.add(package);
    _state.payments.add(
      PaymentRecord(
        id: paymentId,
        studentId: student.id,
        groupId: group.id,
        sessionId: sessionId,
        packageId: packageId,
        description: notes.trim().isEmpty
            ? purchase.description
            : '${purchase.description} — ${notes.trim()}',
        baseAmount: baseAmount,
        discountPercent: student.discountPercent,
        netAmount: _studentDiscounted(student, baseAmount),
        paidAmount: paidAmount,
        method: method,
        createdAt: now,
        staffId: currentUser!.id,
      ),
    );
    return package;
  }

  Future<void> renewPackage(PackageRequest request) => _change(
    'package_renew',
    'شراء ${_packageLabel(request.sessions)}',
    () async {
      _require(canCollect);
      _requirePackageChoice(request.sessions, request.monthPlanId);
      final student = _student(request.studentId);
      _requireActiveStudent(student);
      final group = _group(request.groupId);
      if (!student.groupIds.contains(group.id)) {
        throw const CenterException('سجل الطالب في المجموعة أولًا.');
      }
      if (request.method.trim().isEmpty) {
        throw const CenterException('حدد وسيلة الدفع.');
      }
      if (request.sessionId != null) {
        final current = _session(request.sessionId!);
        if (current.groupId != group.id ||
            current.status != SessionStatus.open) {
          throw const CenterException(
            'الحصة المختارة لا تنتمي للمجموعة أو تم إغلاقها.',
          );
        }
      }
      _buyPackage(
        student,
        group,
        request.method,
        request.notes,
        sessionId: request.sessionId,
        sessions: request.sessions,
        paidAmount: request.paidAmount,
        expectedNetAmount: request.expectedNetAmount,
        monthPlanId: request.monthPlanId,
        expectedMonthPlan: request.expectedMonthPlan,
      );
    },
    auditDescription: () => _collectionAudit(
      _state.payments.last.netAmount,
      _state.payments.last.collectedAmount,
      _state.payments.last.id,
    ),
  );
  List<AttendanceRecord> attendanceConflictsFor(
    String studentId,
    String sessionId,
  ) {
    _student(studentId);
    final current = _session(sessionId);
    if (current.status == SessionStatus.canceled) return const [];
    final matchingSessions = _state.sessions
        .where(
          (e) =>
              e.status != SessionStatus.canceled &&
              (e.id == current.id ||
                  e.monthNumber == current.monthNumber &&
                      e.number == current.number),
        )
        .map((e) => e.id)
        .toSet();
    final matches =
        _activeAttendances
            .where(
              (e) =>
                  e.studentId == studentId &&
                  e.status != AttendanceStatus.absent &&
                  matchingSessions.contains(e.sessionId),
            )
            .toList()
          ..sort((a, b) => b.recordedAt.compareTo(a.recordedAt));
    return List.unmodifiable(matches);
  }

  void _requireAttendanceReview(
    EntryRequest request,
    Iterable<String> reviewedIds,
  ) {
    // Re-collecting a canceled payment preserves an existing physical attendance.
    if (attendanceNeedsPayment(request.studentId, request.sessionId)) return;
    final conflicts = attendanceConflictsFor(
      request.studentId,
      request.sessionId,
    ).where((e) => e.sessionId != request.sessionId).map((e) => e.id).toSet();
    final reviewed = reviewedIds.toSet();
    if (reviewed.length != reviewedIds.length ||
        !setEquals(conflicts, reviewed)) {
      throw const CenterException(
        'الطالب حضر نفس رقم الحصة في مجموعة أخرى، أو تغيّر الحضور السابق. راجع التنبيه قبل تسجيله من جديد.',
      );
    }
  }

  PaymentRecord? _sessionPayment(String studentId, String sessionId) =>
      _activePayments
          .where(
            (e) =>
                e.studentId == studentId &&
                e.sessionId == sessionId &&
                e.packageId == null,
          )
          .firstOrNull;

  bool attendanceNeedsPayment(String studentId, String sessionId) =>
      (_centerOnlyAttendance(studentId, sessionId)?.status !=
              AttendanceStatus.absent &&
          _centerOnlyAttendance(studentId, sessionId) != null &&
          centerFeeRemainingFor(studentId, sessionId) > 0) ||
      _sessionPayment(studentId, sessionId) == null &&
          _activeAttendances.any(
            (e) =>
                e.studentId == studentId &&
                e.sessionId == sessionId &&
                (e.status == AttendanceStatus.present ||
                    (e.status == AttendanceStatus.makeup &&
                        e.makeupSourceGroupId != null)) &&
                (e.centerFeeOnly ||
                    _session(sessionId).kind != SessionKind.free ||
                    e.makeupSourceGroupId != null) &&
                e.packageId == null,
          ) &&
          (_activeAttendances.any(
                (e) =>
                    e.studentId == studentId &&
                    e.sessionId == sessionId &&
                    e.paymentPending,
              ) ||
              _state.corrections.any(
                (e) =>
                    e.action == CorrectionAction.paymentCanceled &&
                    e.studentId == studentId &&
                    e.sessionId == sessionId &&
                    e.voidsPayment,
              ));

  bool hasRetainedSessionPayment(String studentId, String sessionId) =>
      _sessionPayment(studentId, sessionId) != null &&
      !_activeAttendances.any(
        (e) =>
            e.studentId == studentId &&
            e.sessionId == sessionId &&
            e.status != AttendanceStatus.absent,
      );

  String? _entryMakeupSourceGroupId(EntryRequest request) {
    final needsSettlement = attendanceNeedsPayment(
      request.studentId,
      request.sessionId,
    );
    final recordedSource = _activeAttendances
        .where(
          (entry) =>
              entry.studentId == request.studentId &&
              entry.sessionId == request.sessionId &&
              (entry.paymentPending || needsSettlement) &&
              entry.status == AttendanceStatus.makeup,
        )
        .firstOrNull
        ?.makeupSourceGroupId;
    if (recordedSource != null &&
        request.makeupSourceGroupId != null &&
        recordedSource != request.makeupSourceGroupId) {
      throw const CenterException(
        'مجموعة مصدر التعويض محفوظة؛ لا يمكن تغييرها عند تسديد الحضور.',
      );
    }
    return recordedSource ?? request.makeupSourceGroupId;
  }

  EntryConfirmation entryConfirmationFor(EntryRequest request) {
    _require(canCollect);
    _requireReceptionEntry(request);
    _requirePackageChoice(request.packageSessions, request.monthPlanId);
    final student = _student(request.studentId);
    _requireActiveStudent(student);
    if (student.packageMember) {
      throw const CenterException('الطالب باكدج؛ سجّل الحضور دون تحصيل.');
    }
    final session = _session(request.sessionId);
    final group = _group(session.groupId);
    if (_entryIsCenterOnly(student, session.id)) {
      final due = _centerFeeDue(student, session.id);
      _validatedPaidAmount(due, request.paidAmount);
      return EntryConfirmation(
        staffId: currentUser!.id,
        groupId: group.id,
        sessionKind: session.kind,
        baseAmount: 0,
        discountPercent: 100,
        netAmount: due,
        paidAmount: request.paidAmount,
        eligibleRemaining: 0,
        centerFeeOnly: true,
        centerFeeAmount: due,
      );
    }
    final sourceGroupId = _entryMakeupSourceGroupId(request);
    final selectedMonth =
        request.mode == EntryMode.package && request.monthPlanId != null
        ? _monthPlanFor(_group(sourceGroupId ?? group.id), request.monthPlanId!)
        : null;
    final balance = sourceGroupId == null
        ? eligibleRemainingFor(student.id, session.id)
        : eligibleMakeupRemainingFor(student.id, session.id, sourceGroupId);
    final int baseAmount;
    final reserved = _activeAttendances.any(
      (e) =>
          e.studentId == student.id &&
          e.sessionId == session.id &&
          e.status == AttendanceStatus.absent &&
          e.packageId != null &&
          session.status == SessionStatus.open,
    );
    if (sourceGroupId != null) {
      final source = _group(sourceGroupId);
      baseAmount =
          hasRetainedSessionPayment(student.id, session.id) || balance > 0
          ? 0
          : request.mode == EntryMode.package
          ? _monthPurchaseFor(
              source,
              request.packageSessions,
              request.monthPlanId,
            ).baseAmount
          : _configuredSessionAmount(source);
    } else if (reserved ||
        hasRetainedSessionPayment(student.id, session.id) ||
        request.mode == EntryMode.makeup ||
        session.kind == SessionKind.free) {
      baseAmount = 0;
    } else if (session.kind == SessionKind.extra) {
      baseAmount = session.extraPrice;
    } else if (request.mode == EntryMode.single) {
      baseAmount = _configuredSessionAmount(group);
    } else {
      baseAmount = balance > 0
          ? 0
          : _monthPurchaseFor(
              group,
              request.packageSessions,
              request.monthPlanId,
            ).baseAmount;
    }
    final net = _studentDiscounted(student, baseAmount);
    _validatedPaidAmount(net, request.paidAmount);
    return EntryConfirmation(
      staffId: currentUser!.id,
      groupId: group.id,
      sessionKind: session.kind,
      baseAmount: baseAmount,
      discountPercent: student.discountPercent,
      netAmount: net,
      paidAmount: request.paidAmount,
      monthPlanId: selectedMonth?.id,
      monthPlanName: selectedMonth?.name,
      monthPlanSessions: selectedMonth?.sessions,
      eligibleRemaining: balance,
    );
  }

  void _requireReceptionEntry(EntryRequest request) {
    final session = _session(request.sessionId);
    final student = _student(request.studentId);
    if (isCairoGroup(session.groupId) ||
        (student.groupIds.isNotEmpty && student.groupIds.every(isCairoGroup))) {
      throw const CenterException(
        'طلاب القاهرة يتم تحضيرهم برصد الامتحان من «حضور مجموعات القاهرة».',
      );
    }
  }

  Future<void> recordAttendance(EntryRequest request) {
    final reviewed = List<String>.unmodifiable(
      request.acknowledgedAttendanceIds,
    );
    var recorded = false;
    return _change(
      'attendance_record',
      'تسجيل حضور الطالب دون تحصيل',
      () async {
        _require(canCollect);
        _requireReceptionEntry(request);
        final student = _student(request.studentId);
        _requireActiveStudent(student);
        final session = _session(request.sessionId);
        if (session.status != SessionStatus.open) {
          throw const CenterException(
            'الحصة مغلقة أو ملغاة.',
            diagnosticCode: 1201,
          );
        }
        final previous = _activeAttendances
            .where(
              (e) => e.studentId == student.id && e.sessionId == session.id,
            )
            .firstOrNull;
        if (previous != null && previous.status != AttendanceStatus.absent) {
          return;
        }
        _requireAttendanceReview(request, reviewed);
        String? originalId;
        final sourceId = request.makeupSourceGroupId;
        if (sourceId != null) {
          if (request.originalAttendanceId != null ||
              !eligibleMakeupSourceGroups(
                student.id,
                session.id,
              ).any((e) => e.id == sourceId)) {
            throw const CenterException(
              'اختر مجموعة الطالب الأصلية بنفس المادة والصف.',
            );
          }
        } else if (request.mode == EntryMode.makeup) {
          if (!eligibleMakeups(
            student.id,
            session.id,
          ).any((e) => e.id == request.originalAttendanceId)) {
            throw const CenterException('اختر غيابًا محسوبًا صالحًا للتعويض.');
          }
          originalId = request.originalAttendanceId;
        } else if (!student.groupIds.contains(session.groupId)) {
          throw const CenterException(
            'الطالب من مجموعة أخرى؛ حدد مجموعته الأصلية للتعويض.',
          );
        }
        if (previous != null && (sourceId != null || originalId != null)) {
          throw const CenterException(
            'صحح الغياب المحفوظ بدل إنشاء تعويض جديد.',
          );
        }
        if (previous != null &&
            _activeAttendances.any(
              (e) => e.originalAttendanceId == previous.id,
            )) {
          throw const CenterException(
            'تم التعويض عن الغياب؛ ألغِ التعويض أولًا.',
          );
        }
        final retained = _sessionPayment(student.id, session.id);
        if (retained != null &&
            retained.groupId != (sourceId ?? session.groupId)) {
          throw const CenterException('الدفع المحفوظ يخص مجموعة مختلفة.');
        }
        if (student.centerOnly &&
            (previous?.packageId != null ||
                retained != null ||
                originalId != null)) {
          throw const CenterException(
            'للحضور دفع أو رصيد محفوظ؛ صحح السجل السابق قبل تغيير نظامه.',
          );
        }
        if (student.packageMember &&
            (previous?.packageId != null ||
                originalId != null ||
                retained != null)) {
          throw const CenterException(
            'للحضور رصيد أو دفع سابق؛ صحح السجل قبل استخدام علامة باكدج.',
          );
        }
        final entry = AttendanceRecord(
          id: _uuid.v4(),
          studentId: student.id,
          sessionId: session.id,
          status: sourceId != null || originalId != null
              ? AttendanceStatus.makeup
              : AttendanceStatus.present,
          recordedAt: DateTime.now(),
          packageId: previous?.packageId,
          originalAttendanceId: originalId,
          makeupSourceGroupId: sourceId,
          fixedDiscountPercent: student.discountPercent,
          packageMember: student.packageMember,
          centerFeeOnly: student.centerOnly,
          centerFeeAmount: student.centerOnly ? student.centerFeeAmount : 0,
          paymentPending:
              !student.packageMember &&
              retained == null &&
              previous?.packageId == null &&
              originalId == null &&
              (student.centerOnly ||
                  sourceId != null ||
                  session.kind != SessionKind.free),
        );
        _state.attendances.add(entry);
        if (previous != null) {
          _state.corrections.add(
            CorrectionRecord(
              id: _uuid.v4(),
              action: CorrectionAction.absencePresent,
              studentId: student.id,
              sessionId: session.id,
              attendanceId: previous.id,
              replacementAttendanceId: entry.id,
              packageId: entry.packageId,
              reason: 'تسجيل حضور مستقل بعد إعادة فتح الحصة',
              staffId: currentUser!.id,
              createdAt: DateTime.now(),
            ),
          );
        }
        recorded = true;
      },
      commitWhen: () => recorded,
      auditDescription: () =>
          'تسجيل حضور — كود ${_student(request.studentId).code} — حصة ${_session(request.sessionId).number}${reviewed.isEmpty ? '' : ' — مراجعة التسجيلات ${reviewed.join(', ')}'}',
    );
  }

  Future<void> collectAndAttend(EntryRequest request) {
    final reviewedIds = List<String>.unmodifiable(
      request.acknowledgedAttendanceIds,
    );
    var settlesPresence = false;
    String? settledSourceGroupId;
    PaymentRecord? newPayment;
    return _change(
      'entry',
      'تسجيل دخول طالب',
      () async {
        settlesPresence = attendanceNeedsPayment(
          request.studentId,
          request.sessionId,
        );
        settledSourceGroupId = _entryMakeupSourceGroupId(request);
        final before = _state.payments.length;
        _collectEntry(request, frozenAttendanceReview: reviewedIds);
        if (_state.payments.length > before) newPayment = _state.payments.last;
      },
      auditDescription: () {
        final description = settlesPresence
            ? 'تسديد حضور محفوظ دون تسجيل حضور جديد'
            : reviewedIds.isEmpty
            ? 'تسجيل دخول طالب'
            : 'تسجيل دخول طالب بعد مراجعة حضور سابق — حصة ${_session(request.sessionId).number} — التسجيلات ${reviewedIds.join(', ')}';
        final sourceDescription = settledSourceGroupId == null
            ? description
            : '$description — تعويض من مجموعة $settledSourceGroupId';
        final payment = newPayment;
        return payment == null
            ? sourceDescription
            : '$sourceDescription — ${_collectionAudit(payment.netAmount, payment.collectedAmount, payment.id)}';
      },
    );
  }

  void _collectEntry(
    EntryRequest request, {
    Iterable<String>? frozenAttendanceReview,
  }) {
    _require(canCollect);
    _requireReceptionEntry(request);
    _requirePackageChoice(request.packageSessions, request.monthPlanId);
    final student = _student(request.studentId);
    _requireActiveStudent(student);
    final session = _session(request.sessionId);
    final group = _group(session.groupId);
    final sourceGroupId = _entryMakeupSourceGroupId(request);
    // Validate custom collection even when the caller did not open a preview.
    if (request.paidAmount != null) entryConfirmationFor(request);
    if (sourceGroupId != null && request.originalAttendanceId != null) {
      throw const CenterException('حدد غياب التعويض أو مجموعته الأصلية فقط.');
    }
    if (request.confirmation != null &&
        request.confirmation != entryConfirmationFor(request)) {
      throw const CenterException(
        'تغيّر السعر أو الخصم أو رصيد الطالب؛ راجع الدفع وأكّد مرة أخرى.',
      );
    }
    if (session.status != SessionStatus.open) {
      throw const CenterException(
        'الحصة مغلقة أو ملغاة.',
        diagnosticCode: 1201,
      );
    }
    final previousAttendance = _activeAttendances
        .where((e) => e.studentId == student.id && e.sessionId == session.id)
        .firstOrNull;
    final unpaid = attendanceNeedsPayment(student.id, session.id);
    if (previousAttendance != null &&
        previousAttendance.status != AttendanceStatus.absent &&
        !unpaid) {
      throw const CenterException('تم تسجيل الطالب في هذه الحصة بالفعل.');
    }
    _requireAttendanceReview(
      request,
      frozenAttendanceReview ?? request.acknowledgedAttendanceIds,
    );
    if (previousAttendance != null &&
        request.mode == EntryMode.makeup &&
        !unpaid) {
      throw const CenterException(
        'للتحضير بعد إعادة فتح الحصة اختر الحصة أو الباقة؛ لا تحول الغياب المسجل إلى تعويض.',
      );
    }
    if (previousAttendance?.status == AttendanceStatus.absent &&
        sourceGroupId != null) {
      throw const CenterException(
        'للحصّة غياب محفوظ لهذا الطالب؛ صحح الغياب الأصلي بدل إنشاء تعويض جديد.',
      );
    }
    if (previousAttendance != null &&
        _activeAttendances.any(
          (e) => e.originalAttendanceId == previousAttendance.id,
        )) {
      throw const CenterException(
        'تم التعويض عن هذا الغياب؛ ألغِ دخول التعويض أولًا قبل تحويل الأصل إلى حضور.',
      );
    }
    if (request.method.trim().isEmpty) {
      throw const CenterException('حدد وسيلة الدفع.');
    }
    _requirePreviousClosed(session);
    if (_entryIsCenterOnly(student, session.id)) {
      _collectCenterOnlyEntry(request, student, session, previousAttendance);
      return;
    }
    String? packageId;
    String? originalId;
    String? makeupSourceGroupId;
    if (sourceGroupId != null) {
      final sources = eligibleMakeupSourceGroups(student.id, session.id);
      if (!sources.any((e) => e.id == sourceGroupId)) {
        throw const CenterException(
          'التعويض يتطلب مجموعة أخرى مسجلًا فيها الطالب بنفس المادة والصف.',
        );
      }
      makeupSourceGroupId = sourceGroupId;
      final source = _group(makeupSourceGroupId);
      final retained = _sessionPayment(student.id, session.id);
      if (retained != null) {
        if (retained.groupId != source.id) {
          throw const CenterException('الدفع المحفوظ يخص مجموعة أصلية مختلفة.');
        }
      } else {
        final choices = _available(student.id, source.id, forClosure: session);
        if (choices.isNotEmpty || request.mode == EntryMode.package) {
          final package = choices.isNotEmpty
              ? choices.first
              : _buyPackage(
                  student,
                  source,
                  request.method,
                  request.notes,
                  sessionId: session.id,
                  sessions: request.packageSessions,
                  paidAmount: request.paidAmount,
                  monthPlanId: request.monthPlanId,
                );
          _consume(package);
          packageId = package.id;
        } else {
          final amount = _configuredSessionAmount(source);
          _state.payments.add(
            PaymentRecord(
              id: _uuid.v4(),
              studentId: student.id,
              groupId: source.id,
              sessionId: session.id,
              description: 'دفع حصة تعويض من المجموعة الأصلية',
              baseAmount: amount,
              discountPercent: student.discountPercent,
              netAmount: _studentDiscounted(student, amount),
              paidAmount: request.paidAmount,
              method: request.method,
              createdAt: DateTime.now(),
              staffId: currentUser!.id,
            ),
          );
        }
      }
    } else if (hasRetainedSessionPayment(student.id, session.id)) {
      if (request.mode == EntryMode.makeup ||
          (!student.groupIds.contains(group.id) &&
              previousAttendance == null)) {
        throw const CenterException(
          'الدفع المحفوظ يخص حضور الطالب في هذه المجموعة.',
        );
      }
      // Retain the original receipt: no second charge or package consumption.
    } else if (previousAttendance?.packageId != null) {
      // Reopening keeps the original debit; attendance reuses that same session.
      packageId = previousAttendance!.packageId;
    } else if (request.mode == EntryMode.makeup) {
      final options = eligibleMakeups(student.id, session.id);
      if (request.originalAttendanceId == null ||
          !options.any((e) => e.id == request.originalAttendanceId)) {
        throw const CenterException(
          'اختر غيابًا مدفوعًا صالحًا للتعويض أو حدد مجموعة الطالب الأصلية.',
        );
      }
      originalId = request.originalAttendanceId;
    } else {
      if (!student.groupIds.contains(group.id) && previousAttendance == null) {
        throw const CenterException(
          'الطالب من مجموعة أخرى؛ اختر التعويض أو سجله في المجموعة.',
        );
      }
      if (session.kind == SessionKind.counted &&
          request.mode == EntryMode.single &&
          _available(student.id, group.id, forClosure: session).isNotEmpty) {
        throw const CenterException(
          'للطالب باقة سارية تغطي هذه الحصة؛ اختر الدخول بالباقة.',
        );
      }
      if (session.kind == SessionKind.counted &&
          request.mode == EntryMode.package) {
        final choices = _available(student.id, group.id, forClosure: session);
        final package = choices.isEmpty
            ? _buyPackage(
                student,
                group,
                request.method,
                request.notes,
                sessionId: session.id,
                sessions: request.packageSessions,
                paidAmount: request.paidAmount,
                monthPlanId: request.monthPlanId,
              )
            : choices.first;
        _consume(package);
        packageId = package.id;
      } else if (session.kind == SessionKind.extra ||
          (session.kind == SessionKind.counted &&
              request.mode == EntryMode.single)) {
        final amount = session.kind == SessionKind.extra
            ? session.extraPrice
            : _configuredSessionAmount(group);
        _state.payments.add(
          PaymentRecord(
            id: _uuid.v4(),
            studentId: student.id,
            groupId: group.id,
            sessionId: session.id,
            description: session.kind == SessionKind.extra
                ? 'حصة بسعر منفصل'
                : 'دفع حصة',
            baseAmount: amount,
            discountPercent: student.discountPercent,
            netAmount: _studentDiscounted(student, amount),
            paidAmount: request.paidAmount,
            method: request.method,
            createdAt: DateTime.now(),
            staffId: currentUser!.id,
          ),
        );
      }
    }
    _state.attendances.add(
      AttendanceRecord(
        id: _uuid.v4(),
        studentId: student.id,
        sessionId: session.id,
        status: originalId == null && makeupSourceGroupId == null
            ? AttendanceStatus.present
            : AttendanceStatus.makeup,
        recordedAt: previousAttendance?.paymentPending == true
            ? previousAttendance!.recordedAt
            : DateTime.now(),
        packageId: packageId,
        originalAttendanceId: originalId,
        makeupSourceGroupId: makeupSourceGroupId,
        fixedDiscountPercent: previousAttendance?.paymentPending == true
            ? previousAttendance!.fixedDiscountPercent
            : student.discountPercent,
      ),
    );
    if (previousAttendance != null) {
      _state.corrections.add(
        CorrectionRecord(
          id: _uuid.v4(),
          action: unpaid
              ? CorrectionAction.entryCorrected
              : CorrectionAction.absencePresent,
          studentId: student.id,
          sessionId: session.id,
          attendanceId: previousAttendance.id,
          packageId: packageId,
          replacementAttendanceId: _state.attendances.last.id,
          reason: unpaid
              ? 'تحصيل حضور غير مدفوع'
              : 'تسجيل حضور بعد إعادة فتح الحصة',
          staffId: currentUser!.id,
          createdAt: DateTime.now(),
        ),
      );
    }
  }

  Future<void> closeSession(String sessionId) =>
      _change('session_close', 'إغلاق الحصة', () async {
        _require(canCollect);
        final session = _session(sessionId);
        if (session.status != SessionStatus.open) {
          throw const CenterException('الحصة مغلقة أو ملغاة بالفعل.');
        }
        _requirePreviousClosed(session);
        for (final student in _state.students.where(
          (e) => !e.isSuspended && e.groupIds.contains(session.groupId),
        )) {
          final enrolledAt = DateTime.parse(
            _state.enrollments['${student.id}:${session.groupId}']!,
          );
          if (student.createdAt.isAfter(session.startsAt) ||
              enrolledAt.isAfter(session.startsAt) ||
              _activeAttendances.any(
                (e) => e.studentId == student.id && e.sessionId == session.id,
              )) {
            continue;
          }
          String? packageId;
          if (session.kind == SessionKind.counted &&
              !student.centerOnly &&
              !student.packageMember) {
            final available = _available(
              student.id,
              session.groupId,
              forClosure: session,
            );
            if (available.isNotEmpty) {
              packageId = available.first.id;
              _consume(available.first);
            }
          }
          _state.attendances.add(
            AttendanceRecord(
              id: _uuid.v4(),
              studentId: student.id,
              sessionId: session.id,
              status: AttendanceStatus.absent,
              recordedAt: DateTime.now(),
              fixedDiscountPercent: student.discountPercent,
              packageMember: student.packageMember,
              centerFeeOnly: student.centerOnly,
              centerFeeAmount: student.centerOnly ? student.centerFeeAmount : 0,
              packageId: packageId,
            ),
          );
        }
        _replace(
          _state.sessions,
          session.id,
          session.copyWith(status: SessionStatus.closed),
          (e) => e.id,
        );
      });
  Future<void> reopenSession(String sessionId) => _change(
    'session_reopen',
    'إعادة فتح الحصة للتحضير',
    () async {
      _require(canCollect);
      final session = _session(sessionId);
      if (session.status != SessionStatus.closed) {
        throw const CenterException('يمكن إعادة فتح الحصة المغلقة فقط.');
      }
      if (session.kind == SessionKind.counted &&
          _state.sessions.any(
            (later) =>
                later.id != session.id &&
                later.groupId == session.groupId &&
                later.kind == SessionKind.counted &&
                later.status != SessionStatus.canceled &&
                _precedes(session, later) &&
                (later.status == SessionStatus.closed ||
                    _state.attendances.any((e) => e.sessionId == later.id) ||
                    _state.payments.any((e) => e.sessionId == later.id)),
          )) {
        throw const CenterException(
          'لا يمكن إعادة فتح هذه الحصة؛ توجد حصة محسوبة تالية تم استخدامها أو إغلاقها في المجموعة، حفاظًا على ترتيب الباقات.',
        );
      }
      for (final closing in closings.where((e) => e.sessionId == sessionId)) {
        _state.corrections.add(
          CorrectionRecord(
            id: _uuid.v4(),
            action: CorrectionAction.closingReopened,
            closingId: closing.id,
            sessionId: sessionId,
            reason: 'إعادة فتح الحصة للتحضير',
            staffId: currentUser!.id,
            createdAt: DateTime.now(),
          ),
        );
      }
      _replace(
        _state.sessions,
        session.id,
        session.copyWith(status: SessionStatus.open),
        (e) => e.id,
      );
    },
    auditDescription: () =>
        'إعادة فتح الحصة للتحضير — حصة ${_session(sessionId).number} — ${groupLabel(_session(sessionId).groupId)}',
  );

  Future<void> cancelSession(
    String sessionId,
  ) => _change('session_cancel', 'إلغاء الحصة', () async {
    _require(canManage);
    final session = _session(sessionId);
    if (_state.paymentChecks.any((e) => e.sessionId == session.id)) {
      throw const CenterException(
        'لا يمكن إلغاء حصة تمت مراجعة الدفع بالكود فيها؛ سجل المراجعة محفوظ لهذه الحصة.',
      );
    }
    if (session.status != SessionStatus.open ||
        _state.attendances.any((e) => e.sessionId == session.id) ||
        _state.payments.any((e) => e.sessionId == session.id) ||
        _state.cardPayments.any((e) => e.sessionId == session.id) ||
        _state.debtSettlements.any((e) => e.sessionId == session.id) ||
        _state.academics.any((e) => e.sessionId == session.id) ||
        _state.academicActivities.any((e) => e.sessionId == session.id) ||
        _state.closings.any((e) => e.sessionId == session.id)) {
      throw const CenterException(
        'لا يمكن إلغاء حصة مسجل بها حضور أو دفع أو رصد؛ سياسة الاسترداد لم تُحدد.',
      );
    }
    _replace(
      _state.sessions,
      session.id,
      session.copyWith(status: SessionStatus.canceled),
      (e) => e.id,
    );
  });
  Future<AcademicActivity> saveAcademicActivity(
    AcademicActivity activity,
  ) async {
    late AcademicActivity saved;
    await _change(
      'academic_activity_save',
      'حفظ نشاط ${activity.name}',
      () async {
        _require(canAssess);
        if (activity.preparedLessonId != null) {
          if (activity.sessionId.isNotEmpty ||
              studyMonthForLesson(activity.preparedLessonId!) == null) {
            throw const CenterException('اختر حصة مجهزة موجودة لتعريف النشاط.');
          }
        } else if (_session(activity.sessionId).status ==
            SessionStatus.canceled) {
          throw const CenterException('الحصة ملغاة.', diagnosticCode: 1202);
        }
        final old = activity.id.isEmpty
            ? null
            : _find<AcademicActivity>(
                _state.academicActivities,
                (e) => e.id == activity.id,
                'النشاط غير موجود.',
              );
        if (old == null && activity.preparedLessonId == null) {
          throw const CenterException(
            'جهّز النشاط على الحصة المشتركة أولًا، ثم اختر مجموعة للرصد.',
          );
        }
        if (old != null &&
            (old.sessionId != activity.sessionId ||
                old.preparedLessonId != activity.preparedLessonId ||
                old.kind != activity.kind)) {
          throw const CenterException(
            'لا يمكن نقل النشاط إلى حصة أخرى أو تغيير نوعه؛ أنشئ نشاطًا جديدًا.',
          );
        }
        final name = activity.name.trim();
        if (name.isEmpty || name.length > 120) {
          throw const CenterException('اسم النشاط من حرف إلى ١٢٠ حرفًا.');
        }
        final maxScore = activity.kind == AcademicActivityKind.exam
            ? activity.maxScore
            : 10;
        if (!activity.maxScoreKnown) {
          throw const CenterException('حدد الدرجة النهائية للامتحان قبل حفظه.');
        }
        if (maxScore <= 0) {
          throw const CenterException(
            'الدرجة النهائية للامتحان يجب أن تكون أكبر من صفر.',
          );
        }
        if (old != null &&
            old.maxScoreKnown &&
            old.maxScore != maxScore &&
            _state.academics.any((e) => e.activityId == old.id)) {
          throw const CenterException(
            'لا يمكن تغيير الدرجة النهائية بعد تسجيل نتائج للامتحان.',
          );
        }
        if (_state.academicActivities.any(
          (e) =>
              e.id != activity.id &&
              e.sessionId == activity.sessionId &&
              e.preparedLessonId == activity.preparedLessonId &&
              e.kind == activity.kind &&
              e.name.toLowerCase() == name.toLowerCase(),
        )) {
          throw const CenterException(
            'يوجد نشاط من نفس النوع بهذا الاسم في الحصة؛ اختر اسمًا آخر.',
          );
        }
        saved = activity.copyWith(
          id: old?.id ?? _uuid.v4(),
          name: name,
          maxScore: maxScore,
          maxScoreKnown: true,
          createdAt: old?.createdAt ?? activity.createdAt,
        );
        if (old != null && !old.maxScoreKnown) {
          _resolveImportedMaximum(old.id, maxScore);
        }
        _replace(_state.academicActivities, saved.id, saved, (e) => e.id);
      },
    );
    return saved;
  }

  List<AcademicActivity> academicActivitiesFor(String sessionId) {
    final session = _session(sessionId);
    return List.unmodifiable(
      _state.academicActivities.where(
        (activity) => activity.appliesToSession(session),
      ),
    );
  }

  void _resolveImportedMaximum(String activityId, int maxScore) {
    for (var index = 0; index < _state.academics.length; index++) {
      final record = _state.academics[index];
      if (record.activityId != activityId) continue;
      if (record.score != null && record.score! > maxScore) {
        throw const CenterException(
          'الدرجة النهائية أقل من درجة مستوردة؛ راجع الدرجة النهائية دون تغيير نتائج الطلاب.',
        );
      }
      _state.academics[index] = record.copyWith(
        maxScore: maxScore,
        maxScoreKnown: true,
      );
    }
  }

  /// Marks only unreviewed actual attendees complete, preserving saved exceptions.
  /// An entered code is committed as missing in the same host transaction.
  Future<void> recordHomeworkExceptions({
    required String sessionId,
    required String activityId,
    String? missingStudentId,
  }) => _change(
    'homework_exceptions',
    'رصد الواجب للحاضرين والاستثناءات',
    () async {
      _require(canAssess);
      final session = _session(sessionId);
      final activity = _find<AcademicActivity>(
        _state.academicActivities,
        (a) => a.id == activityId,
        'الواجب غير موجود.',
      );
      if (session.status == SessionStatus.canceled ||
          !sessionHasStarted(sessionId) ||
          isCairoGroup(session.groupId) ||
          activity.kind != AcademicActivityKind.homework ||
          !activity.appliesToSession(session)) {
        throw const CenterException('اختر واجبًا وحصة بدأت لرصد الحاضرين.');
      }
      final present = _activeAttendances
          .where(
            (a) =>
                a.sessionId == sessionId && a.status != AttendanceStatus.absent,
          )
          .map((a) => a.studentId)
          .toSet();
      if (missingStudentId != null && !present.contains(missingStudentId)) {
        throw const CenterException('الطالب غير حاضر في هذه الحصة.');
      }
      final existing = {
        for (final r in _state.academics)
          if (r.sessionId == sessionId && r.activityId == activityId)
            r.studentId: r,
      };
      final now = DateTime.now();
      for (final studentId in present) {
        final old = existing[studentId];
        if (studentId != missingStudentId &&
            old != null &&
            old.homework != HomeworkStatus.notReviewed) {
          continue;
        }
        final saved = AcademicRecord(
          id: old?.id ?? _uuid.v4(),
          studentId: studentId,
          sessionId: sessionId,
          activityId: activityId,
          maxScore: activity.maxScore,
          homework: studentId == missingStudentId
              ? HomeworkStatus.missing
              : HomeworkStatus.complete,
          notes: old?.notes ?? '',
          updatedAt: now,
        );
        _replace(_state.academics, saved.id, saved, (r) => r.id);
      }
    },
  );

  Future<void> importAcademicGrades(AcademicImportCommand command) =>
      _importAcademicGrades(command);

  Future<void> saveAcademic(AcademicRecord record) => _change(
    'academic_save',
    'رصد امتحان وواجب',
    () async => _saveAcademicRecord(record),
  );

  void _saveAcademicRecord(AcademicRecord record) {
    _require(canAssess);
    if (!record.maxScoreKnown) {
      throw const CenterException('حدد الدرجة النهائية للامتحان قبل الرصد.');
    }
    final student = _student(record.studentId);
    final session = _session(record.sessionId);
    if (session.status == SessionStatus.canceled) {
      throw const CenterException('الحصة ملغاة.', diagnosticCode: 1202);
    }
    final cairo = isCairoGroup(session.groupId);
    if (cairo && !student.groupIds.contains(session.groupId)) {
      throw const CenterException(
        'الطالب غير مسجل في مجموعة القاهرة المختارة.',
      );
    }
    if (!cairo &&
        !_activeAttendances.any(
          (entry) =>
              entry.studentId == student.id &&
              entry.sessionId == session.id &&
              (entry.status == AttendanceStatus.present ||
                  entry.status == AttendanceStatus.makeup),
        )) {
      throw const CenterException(
        'الرصد متاح للطلاب الحاضرين والمعوّضين فعليًا في هذه الحصة فقط.',
      );
    }
    AcademicActivity? activity;
    if (record.activityId != null) {
      activity = _find<AcademicActivity>(
        _state.academicActivities,
        (e) => e.id == record.activityId,
        'النشاط غير موجود.',
      );
      if (!activity.appliesToSession(session)) {
        throw const CenterException('النشاط لا ينتمي للحصة المختارة.');
      }
      if (activity.kind == AcademicActivityKind.exam) {
        if (!activity.maxScoreKnown) {
          throw const CenterException(
            'حدد الدرجة النهائية للامتحان قبل الرصد.',
          );
        }
        if (record.homework != HomeworkStatus.notReviewed) {
          throw const CenterException(
            'هذا النشاط امتحان؛ لا تسجل فيه حالة واجب.',
          );
        }
        if (record.maxScore != activity.maxScore) {
          throw const CenterException(
            'الدرجة النهائية لا تطابق إعداد الامتحان.',
          );
        }
      } else if (record.score != null || record.examAbsent) {
        throw const CenterException(
          'هذا النشاط واجب؛ لا تسجل فيه درجة امتحان أو غياب امتحان.',
        );
      }
    }
    final existing = _state.academics.where(
      (e) =>
          e.studentId == record.studentId &&
          e.sessionId == record.sessionId &&
          e.activityId == record.activityId,
    );
    if (record.id.isNotEmpty && !existing.any((e) => e.id == record.id)) {
      throw const CenterException('سجل الرصد غير موجود.');
    }
    final id = existing.isEmpty ? _uuid.v4() : existing.first.id;
    final saved = AcademicRecord(
      id: id,
      studentId: record.studentId,
      sessionId: record.sessionId,
      activityId: record.activityId,
      homework: record.homework,
      score: record.score,
      maxScore: activity?.maxScore ?? record.maxScore,
      maxScoreKnown: true,
      examAbsent: record.examAbsent,
      notes: record.notes,
      updatedAt: DateTime.now(),
    );
    if (cairo) {
      if (activity?.kind != AcademicActivityKind.exam) {
        throw const CenterException('تحضير القاهرة مخصص لرصد الامتحانات.');
      }
      if (record.score != null && !record.examAbsent) {
        final previous = _activeAttendances
            .where(
              (a) => a.studentId == student.id && a.sessionId == session.id,
            )
            .firstOrNull;
        if (previous == null || previous.status == AttendanceStatus.absent) {
          final attendance =
              previous?.copyWith(status: AttendanceStatus.present) ??
              AttendanceRecord(
                id: _uuid.v4(),
                studentId: student.id,
                sessionId: session.id,
                status: AttendanceStatus.present,
                recordedAt: DateTime.now(),
              );
          _replace(_state.attendances, attendance.id, attendance, (a) => a.id);
        }
      }
    }
    _replace(_state.academics, id, saved, (e) => e.id);
  }

  void _correctionReason(String reason) {
    _require(canCollect);
    if (reason.trim().isEmpty) {
      throw const CenterException(
        'اكتب سبب التصحيح أو الاسترداد لحفظه في السجل.',
      );
    }
  }

  void _requireFinancialOpen(String? sessionId) {
    if (sessionId != null && closings.any((e) => e.sessionId == sessionId)) {
      throw const CenterException(
        'الحصة مقفلة ماليًا؛ أعد فتح تقفيلتها بسبب مسجل أولًا ثم صحح وأعد التقفيل.',
      );
    }
  }

  AttendanceRecord _activeAttendance(String id) => _find(
    _activeAttendances,
    (e) => e.id == id,
    'تسجيل الحضور غير موجود أو سبق تصحيحه.',
  );
  PaymentRecord _activePayment(String id) => _find(
    _activePayments,
    (e) => e.id == id,
    'عملية الدفع غير موجودة أو سبق استردادها.',
  );
  void _refund(
    PaymentRecord payment,
    String correctionId,
    String reason,
    String method,
  ) {
    if (method.trim().isEmpty) {
      throw const CenterException('حدد وسيلة رد المبلغ فعليًا.');
    }
    _requireFinancialOpen(payment.sessionId);
    _requireDebtRefundOpen(payment.id);
    _state.refunds.add(
      RefundRecord(
        id: _uuid.v4(),
        correctionId: correctionId,
        paymentId: payment.id,
        studentId: payment.studentId,
        groupId: payment.groupId,
        sessionId: payment.sessionId,
        packageId: payment.packageId,
        amount: paymentCollectedFor(payment.id),
        method: method.trim(),
        reason: reason.trim(),
        staffId: currentUser!.id,
        createdAt: DateTime.now(),
      ),
    );
  }

  CorrectionRecord _reverseEntry(
    String attendanceId,
    String reason,
    String refundMethod, {
    _EntryReversalOptions options = const _EntryReversalOptions(),
  }) {
    final original = _activeAttendance(attendanceId);
    final session = _session(original.sessionId);
    _requireFinancialOpen(session.id);
    if (original.status == AttendanceStatus.absent) {
      throw const CenterException(
        'هذا غياب؛ استخدم تحويل الغياب إلى حضور بدل إلغاء الدخول.',
      );
    }
    final correctionId = _uuid.v4();
    final single = _activePayments
        .where(
          (e) =>
              e.studentId == original.studentId &&
              e.sessionId == session.id &&
              e.packageId == null,
        )
        .firstOrNull;
    PaymentRecord? refunded = options.mode == RecordCancellationMode.recordOnly
        ? null
        : single;
    bool voidPackage = false;
    if (original.packageId != null &&
        (session.status == SessionStatus.open ||
            original.makeupSourceGroupId != null)) {
      final package = _activePackages.firstWhere(
        (e) => e.id == original.packageId,
      );
      _replace(
        _state.packages,
        package.id,
        package.copyWith(remaining: package.remaining + 1),
        (e) => e.id,
      );
      if (options.refundNewPackage) {
        final purchase = _activePayment(package.paymentId);
        if (purchase.sessionId != session.id ||
            package.remaining + 1 != package.totalSessions) {
          throw const CenterException(
            'لا يمكن تجاوز باقة سابقة أو مستخدمة بالدفع بالحصة؛ صحح وسيلة الدفع أو استخدم الباقة.',
          );
        }
        refunded = purchase;
        voidPackage = true;
      }
    }
    if (refunded != null) _refund(refunded, correctionId, reason, refundMethod);
    AttendanceRecord? replacement;
    if (session.status == SessionStatus.closed &&
        original.status != AttendanceStatus.makeup) {
      replacement = AttendanceRecord(
        id: _uuid.v4(),
        studentId: original.studentId,
        sessionId: session.id,
        status: AttendanceStatus.absent,
        recordedAt: DateTime.now(),
        fixedDiscountPercent: _student(original.studentId).discountPercent,
        packageId: original.packageId,
      );
      _state.attendances.add(replacement);
    }
    final event = CorrectionRecord(
      id: correctionId,
      action: options.action,
      studentId: original.studentId,
      sessionId: session.id,
      attendanceId: original.id,
      paymentId: refunded?.id,
      packageId: original.packageId,
      replacementAttendanceId: replacement?.id,
      voidsPayment: refunded != null,
      voidsPackage: voidPackage,
      reason: reason.trim(),
      staffId: currentUser!.id,
      createdAt: DateTime.now(),
    );
    _state.corrections.add(event);
    return event;
  }

  String _correctionAudit() {
    final e = _state.corrections.last;
    final refunded = _state.refunds
        .where((r) => r.correctionId == e.id)
        .fold(0, (s, r) => s + r.amount);
    return '${e.action.name} — ${e.studentId == null ? '' : 'كود ${_student(e.studentId!).code}'} — ${e.sessionId == null ? '' : 'حصة ${_session(e.sessionId!).number}'} — رد فعلي ${_auditAmount(refunded)} — السبب ${e.reason} — سجل ${e.id}';
  }

  Future<void> reverseEntry({
    required String attendanceId,
    required String reason,
    String refundMethod = 'نقدي',
  }) => _change('entry_reverse', 'إلغاء دخول خاطئ', () async {
    _correctionReason(reason);
    _reverseEntry(attendanceId, reason, refundMethod);
  }, auditDescription: _correctionAudit);
  Future<void> correctEntry({
    required String attendanceId,
    required EntryMode mode,
    required String reason,
    String method = 'نقدي',
    String? originalAttendanceId,
    int packageSessions = 4,
    String? monthPlanId,
    GroupMonthPlan? expectedMonthPlan,
  }) => _change('entry_correct', 'تصحيح دفع ودخول', () async {
    _correctionReason(reason);
    final original = _activeAttendance(attendanceId);
    if (expectedMonthPlan != null) {
      final groupId =
          original.makeupSourceGroupId ?? _session(original.sessionId).groupId;
      if (monthPlanId == null ||
          _monthPlanFor(_group(groupId), monthPlanId) != expectedMonthPlan) {
        throw const CenterException(
          'تغيّر اسم الشهر أو عدد حصصه أو سعره؛ راجع الاختيار قبل التصحيح.',
        );
      }
    }
    if (_session(original.sessionId).status != SessionStatus.open) {
      throw const CenterException(
        'تغيير الحصة إلى باقة أو العكس متاح والحضور مفتوح؛ للحصة المغلقة صحح الحضور أو وسيلة الدفع.',
      );
    }
    final event = _reverseEntry(
      attendanceId,
      reason,
      method,
      options: _EntryReversalOptions(
        action: CorrectionAction.entryCorrected,
        refundNewPackage: mode == EntryMode.single,
      ),
    );
    _collectEntry(
      EntryRequest(
        studentId: original.studentId,
        sessionId: original.sessionId,
        mode: mode,
        method: method,
        originalAttendanceId: originalAttendanceId,
        makeupSourceGroupId: originalAttendanceId == null
            ? original.makeupSourceGroupId
            : null,
        packageSessions: packageSessions,
        monthPlanId: monthPlanId,
        notes: reason,
        acknowledgedAttendanceIds:
            attendanceConflictsFor(original.studentId, original.sessionId)
                .where((e) => e.sessionId != original.sessionId)
                .map((e) => e.id)
                .toList(),
      ),
    );
    final json = event.toJson();
    json['replacementAttendanceId'] = _state.attendances.last.id;
    _replace(
      _state.corrections,
      event.id,
      CorrectionRecord.fromJson(json),
      (e) => e.id,
    );
  }, auditDescription: _correctionAudit);
  Future<void> markAbsentPresent({
    required String attendanceId,
    required String reason,
    String method = 'نقدي',
  }) => _change('absence_present', 'تصحيح غياب إلى حضور', () async {
    _correctionReason(reason);
    final old = _activeAttendance(attendanceId);
    final session = _session(old.sessionId);
    _requireFinancialOpen(session.id);
    if (old.status != AttendanceStatus.absent ||
        session.status != SessionStatus.closed) {
      throw const CenterException('اختر غيابًا مسجلًا في حصة مغلقة.');
    }
    if (_activeAttendances.any((e) => e.originalAttendanceId == old.id)) {
      throw const CenterException(
        'تم التعويض عن هذا الغياب؛ ألغِ دخول التعويض أولًا قبل تحويل الأصل إلى حضور.',
      );
    }
    final group = _group(session.groupId);
    if (old.packageId == null &&
        session.kind != SessionKind.free &&
        _sessionPayment(old.studentId, session.id) == null) {
      if (method.trim().isEmpty) {
        throw const CenterException('حدد وسيلة الدفع.');
      }
      final student = _student(old.studentId);
      final base = session.kind == SessionKind.extra
          ? session.extraPrice
          : _configuredSessionAmount(group);
      _state.payments.add(
        PaymentRecord(
          id: _uuid.v4(),
          studentId: old.studentId,
          groupId: group.id,
          sessionId: session.id,
          description: 'دفع حصة — تصحيح حضور',
          baseAmount: base,
          discountPercent: student.discountPercent,
          netAmount: _studentDiscounted(student, base),
          method: method.trim(),
          createdAt: DateTime.now(),
          staffId: currentUser!.id,
        ),
      );
    }
    final replacement = old.copyWith(
      id: _uuid.v4(),
      status: AttendanceStatus.present,
      recordedAt: DateTime.now(),
      fixedDiscountPercent: _student(old.studentId).discountPercent,
      importSource: '',
    );
    _state.attendances.add(replacement);
    _state.corrections.add(
      CorrectionRecord(
        id: _uuid.v4(),
        action: CorrectionAction.absencePresent,
        studentId: old.studentId,
        sessionId: session.id,
        attendanceId: old.id,
        packageId: old.packageId,
        replacementAttendanceId: replacement.id,
        reason: reason.trim(),
        staffId: currentUser!.id,
        createdAt: DateTime.now(),
      ),
    );
  }, auditDescription: _correctionAudit);
  Future<void> correctPaymentMethod({
    required String paymentId,
    required String method,
    required String reason,
  }) => _change('payment_method_correct', 'تصحيح وسيلة دفع', () async {
    _correctionReason(reason);
    final payment = _activePayment(paymentId);
    _requireFinancialOpen(payment.sessionId);
    if (method.trim().isEmpty || method.trim() == payment.method) {
      throw const CenterException('اختر وسيلة دفع مختلفة وصحيحة.');
    }
    _state.corrections.add(
      CorrectionRecord(
        id: _uuid.v4(),
        action: CorrectionAction.paymentMethod,
        studentId: payment.studentId,
        sessionId: payment.sessionId,
        paymentId: payment.id,
        oldMethod: payment.method,
        newMethod: method.trim(),
        reason: reason.trim(),
        staffId: currentUser!.id,
        createdAt: DateTime.now(),
      ),
    );
  }, auditDescription: _correctionAudit);
  Future<void> refundPackage({
    required String packageId,
    required String reason,
    String refundMethod = 'نقدي',
  }) => _change('package_refund', 'استرداد باقة غير مستخدمة', () async {
    _correctionReason(reason);
    _refundPackageRecord(packageId, reason, refundMethod);
  }, auditDescription: _correctionAudit);

  void _refundPackageRecord(
    String packageId,
    String reason,
    String refundMethod,
  ) {
    final package = _find(
      _activePackages,
      (e) => e.id == packageId,
      'الباقة غير موجودة أو سبق استردادها.',
    );
    if (package.remaining != package.totalSessions ||
        _activeAttendances.any((e) => e.packageId == package.id)) {
      throw const CenterException(
        'الشهر مستخدم في تسجيلات أخرى أو غياب محسوب. يمكنك إلغاء الحضور فقط؛ ولرد دفعة الشهر راجع خيار الدفع والحضور المرتبط من سجل الدفع. الغياب المحسوب لا يُلغى تلقائيًا.',
      );
    }
    final payment = _activePayment(package.paymentId);
    _requireFinancialOpen(payment.sessionId);
    final id = _uuid.v4();
    _refund(payment, id, reason, refundMethod);
    _state.corrections.add(
      CorrectionRecord(
        id: id,
        action: CorrectionAction.packageRefund,
        studentId: package.studentId,
        sessionId: payment.sessionId,
        paymentId: payment.id,
        packageId: package.id,
        voidsPayment: true,
        voidsPackage: true,
        reason: reason.trim(),
        staffId: currentUser!.id,
        createdAt: DateTime.now(),
      ),
    );
  }

  Future<void> cancelAttendance({
    required String attendanceId,
    required String reason,
    RecordCancellationMode mode = RecordCancellationMode.recordOnly,
    String refundMethod = 'نقدي',
    bool reopenFinancialClosings = false,
  }) => _change('attendance_cancel', 'إلغاء الحضور', () async {
    _correctionReason(reason);
    final original = _activeAttendance(attendanceId);
    _reopenCancellationClosings(
      cancellationFinancialClosings(attendanceId: attendanceId, mode: mode),
      reason,
      reopenFinancialClosings,
    );
    final packageId = original.packageId;
    _reverseEntry(
      attendanceId,
      reason,
      refundMethod,
      options: _EntryReversalOptions(
        mode: packageId == null ? mode : RecordCancellationMode.recordOnly,
      ),
    );
    if (mode == RecordCancellationMode.recordAndRelated && packageId != null) {
      _refundPackageRecord(packageId, reason, refundMethod);
    }
  }, auditDescription: _correctionAudit);

  Future<void> cancelPayment({
    required String paymentId,
    required String reason,
    RecordCancellationMode mode = RecordCancellationMode.recordOnly,
    String refundMethod = 'نقدي',
    bool reopenFinancialClosings = false,
  }) => _change('payment_cancel', 'إلغاء الدفع', () async {
    _correctionReason(reason);
    final payment = _activePayment(paymentId);
    _reopenCancellationClosings(
      cancellationFinancialClosings(paymentId: paymentId, mode: mode),
      reason,
      reopenFinancialClosings,
    );
    if (payment.packageId != null) {
      _cancelPackagePayment(payment, mode, reason, refundMethod);
    } else {
      _cancelSinglePayment(payment, mode, reason, refundMethod);
    }
  }, auditDescription: _correctionAudit);

  List<SessionClosing> cancellationFinancialClosings({
    String? paymentId,
    String? attendanceId,
    RecordCancellationMode mode = RecordCancellationMode.recordOnly,
  }) {
    _require(canCollect);
    if ((paymentId == null) == (attendanceId == null)) {
      throw const CenterException('اختر سجل حضور أو دفع واحدًا للإلغاء.');
    }
    final attendance = attendanceId == null
        ? null
        : _activeAttendance(attendanceId);
    final payments = paymentId != null
        ? [_activePayment(paymentId)]
        : mode == RecordCancellationMode.recordOnly
        ? <PaymentRecord>[]
        : _attendanceCancellationPayments(attendance!);
    final sessionIds = <String>{
      if (attendance != null) attendance.sessionId,
      ...payments.map((payment) => payment.sessionId).whereType<String>(),
    };
    if (paymentId != null &&
        (mode == RecordCancellationMode.recordAndRelated ||
            payments.single.packageId != null)) {
      final payment = payments.single;
      sessionIds.addAll(
        _activeAttendances
            .where(
              (entry) => payment.packageId != null
                  ? entry.packageId == payment.packageId
                  : entry.studentId == payment.studentId &&
                        entry.sessionId == payment.sessionId &&
                        entry.status != AttendanceStatus.absent,
            )
            .map((entry) => entry.sessionId),
      );
    }
    sessionIds.addAll(
      _state.debtSettlements
          .where(
            (settlement) =>
                settlement.kind == DebtKind.lesson &&
                payments.any((payment) => payment.id == settlement.paymentId),
          )
          .map((settlement) => settlement.sessionId)
          .whereType<String>(),
    );
    return List.unmodifiable(
      closings.where((closing) => sessionIds.contains(closing.sessionId)),
    );
  }

  List<PaymentRecord> _attendanceCancellationPayments(
    AttendanceRecord attendance,
  ) => _activePayments
      .where(
        (payment) => attendance.packageId != null
            ? payment.packageId == attendance.packageId
            : payment.studentId == attendance.studentId &&
                  payment.sessionId == attendance.sessionId &&
                  payment.packageId == null,
      )
      .toList();

  void _reopenCancellationClosings(
    List<SessionClosing> affected,
    String reason,
    bool accepted,
  ) {
    if (affected.isEmpty) return;
    if (!accepted) {
      throw const CenterException(
        'توجد تقفيلة مالية مرتبطة بالسجل. اختر إعادة فتح التقفيلات في نافذة الإلغاء؛ تبقى النسخ الأصلية محفوظة.',
      );
    }
    for (final closing in affected) {
      final reopeningReason = 'إعادة فتح التقفيلة لإلغاء سجل: ${reason.trim()}';
      _state.corrections.add(
        CorrectionRecord(
          id: _uuid.v4(),
          action: CorrectionAction.closingReopened,
          closingId: closing.id,
          sessionId: closing.sessionId,
          reason: reopeningReason,
          staffId: currentUser!.id,
          createdAt: DateTime.now(),
        ),
      );
      _log(
        'closing_reopen',
        '$reopeningReason — تقفيلة ${closing.id}',
        currentUser!.id,
      );
    }
  }

  void _cancelPackagePayment(
    PaymentRecord payment,
    RecordCancellationMode mode,
    String reason,
    String refundMethod,
  ) {
    if (mode == RecordCancellationMode.recordOnly) {
      final linked = _activeAttendances
          .where((e) => e.packageId == payment.packageId)
          .toList();
      if (linked.any(
        (e) =>
            e.status == AttendanceStatus.absent ||
            e.originalAttendanceId != null,
      )) {
        throw const CenterException(
          'الشهر مرتبط بغياب محسوب أو تعويض عن غياب؛ راجع هذه التسجيلات قبل إلغاء دفع الشهر.',
        );
      }
      for (final entry in linked) {
        final replacement = AttendanceRecord.fromJson({
          ...entry.toJson(),
          'id': _uuid.v4(),
          'packageId': null,
          'paymentPending': true,
          'importSource': '',
        });
        _state.attendances.add(replacement);
        _state.corrections.add(
          CorrectionRecord(
            id: _uuid.v4(),
            action: CorrectionAction.entryCorrected,
            studentId: entry.studentId,
            sessionId: entry.sessionId,
            attendanceId: entry.id,
            replacementAttendanceId: replacement.id,
            reason: reason.trim(),
            staffId: currentUser!.id,
            createdAt: DateTime.now(),
          ),
        );
      }
      final package = _activePackages.firstWhere(
        (e) => e.id == payment.packageId,
      );
      _replace(
        _state.packages,
        package.id,
        package.copyWith(remaining: package.totalSessions),
        (e) => e.id,
      );
    }
    if (mode == RecordCancellationMode.recordAndRelated) {
      final linked = _activeAttendances
          .where((e) => e.packageId == payment.packageId)
          .toList();
      if (linked.any(
        (e) =>
            e.status == AttendanceStatus.absent ||
            (_session(e.sessionId).status != SessionStatus.open &&
                !(e.status == AttendanceStatus.makeup &&
                    e.makeupSourceGroupId != null)),
      )) {
        throw const CenterException(
          'هذا الشهر استُخدم في حصة مغلقة أو غياب محسوب؛ لا يمكن رد دفعة الشهر كاملة. يمكنك إلغاء الحضور وحده مع الاحتفاظ بالاستهلاك، أو إعادة فتح الحصة المرتبطة أولًا لمراجعة تسجيلها.',
        );
      }
      for (final e in linked) {
        _reverseEntry(
          e.id,
          reason,
          refundMethod,
          options: const _EntryReversalOptions(
            mode: RecordCancellationMode.recordOnly,
          ),
        );
      }
    }
    _refundPackageRecord(payment.packageId!, reason, refundMethod);
  }

  void _cancelSinglePayment(
    PaymentRecord payment,
    RecordCancellationMode mode,
    String reason,
    String refundMethod,
  ) {
    if (mode == RecordCancellationMode.recordAndRelated) {
      final linked = _activeAttendances
          .where(
            (e) =>
                e.studentId == payment.studentId &&
                e.sessionId == payment.sessionId &&
                e.status != AttendanceStatus.absent,
          )
          .firstOrNull;
      if (linked != null) {
        _reverseEntry(
          linked.id,
          reason,
          refundMethod,
          options: const _EntryReversalOptions(
            mode: RecordCancellationMode.recordOnly,
          ),
        );
      }
    }
    _recordPaymentCancellation(payment, reason, refundMethod);
  }

  void _recordPaymentCancellation(
    PaymentRecord payment,
    String reason,
    String refundMethod,
  ) {
    final id = _uuid.v4();
    _refund(payment, id, reason, refundMethod);
    _state.corrections.add(
      CorrectionRecord(
        id: id,
        action: CorrectionAction.paymentCanceled,
        studentId: payment.studentId,
        sessionId: payment.sessionId,
        paymentId: payment.id,
        voidsPayment: true,
        reason: reason.trim(),
        staffId: currentUser!.id,
        createdAt: DateTime.now(),
      ),
    );
  }

  Future<void> reopenFinancialClosing({
    required String closingId,
    required String reason,
  }) => _change('closing_reopen', 'إعادة فتح تقفيلة مالية', () async {
    _correctionReason(reason);
    final closing = _find(
      closings,
      (e) => e.id == closingId,
      'التقفيلة غير موجودة أو سبق إعادة فتحها.',
    );
    _state.corrections.add(
      CorrectionRecord(
        id: _uuid.v4(),
        action: CorrectionAction.closingReopened,
        closingId: closing.id,
        sessionId: closing.sessionId,
        reason: reason.trim(),
        staffId: currentUser!.id,
        createdAt: DateTime.now(),
      ),
    );
  }, auditDescription: _correctionAudit);

  String _auditAmount(int value) {
    final absolute = value.abs();
    return '${value < 0 ? '-' : ''}${absolute ~/ 100}.${(absolute % 100).toString().padLeft(2, '0')} ج.م';
  }

  DateTime? enrollmentDateFor(String studentId, String groupId) {
    final date = _state.enrollments['$studentId:$groupId'];
    return date == null ? null : DateTime.tryParse(date);
  }

  SessionFinancialSummary sessionFinancialSummary(String sessionId) {
    final session = _session(sessionId);
    return buildSessionFinancialSummary(
      session: session,
      packages: _state.packages,
      cardPayments: _state.cardPayments,
      debtSettlements: _state.debtSettlements,
      centerFees: _state.centerFees,
      attendances: _activeAttendances,
      payments: _financialPayments,
      refunds: _state.refunds,
    );
  }

  /// Coverage for the selected class, never an assertion of paper-amount equality.
  PaymentStatusResult paymentStatusFor(String studentId, String sessionId) {
    _require(canCollect);
    _student(studentId);
    final session = _session(sessionId);
    if (session.status == SessionStatus.canceled) {
      throw const CenterException('الحصة ملغاة؛ اختر حصة أخرى للمراجعة.');
    }
    final attendance = _attendancesForStudent(studentId)
        .where((e) => e.studentId == studentId && e.sessionId == sessionId)
        .firstOrNull;
    if (attendance?.packageMember == true) {
      return const PaymentStatusResult(
        status: StudentPaymentStatus.free,
        detail: 'باكدج — دون تحصيل حصة أو سنتر',
      );
    }
    if (attendance?.centerFeeOnly == true) {
      final due = centerFeeDueFor(studentId, sessionId);
      final collected = centerFeeCollectedFor(studentId, sessionId);
      final remaining = centerFeeRemainingFor(studentId, sessionId);
      return PaymentStatusResult(
        status: StudentPaymentStatus.free,
        debtAmount: remaining,
        detail:
            'معفى من رسوم المدرس؛ رسوم السنتر ${_auditAmount(due)}، '
            'المحصّل ${_auditAmount(collected)}، المتبقي ${_auditAmount(remaining)}',
      );
    }
    if (attendance?.paymentPending == true) {
      return const PaymentStatusResult(
        status: StudentPaymentStatus.notPaid,
        detail: 'حاضر غير مسدد؛ الحضور محفوظ والدفع لم يُسجل بعد',
      );
    }
    if (attendance?.status == AttendanceStatus.makeup &&
        attendance?.makeupSourceGroupId == null) {
      final original = _state.attendances.firstWhere(
        (e) => e.id == attendance!.originalAttendanceId,
      );
      final package = _state.packages.firstWhere(
        (e) => e.id == original.packageId,
      );
      return PaymentStatusResult(
        status: StudentPaymentStatus.makeup,
        debtAmount: paymentDebtFor(package.paymentId),
        detail: _debtCoverageLabel(
          package.paymentId,
          'تعويض عن غياب محسوب سابقًا من الباقة',
        ),
      );
    }
    final single = _paymentsForStudent(studentId)
        .where(
          (e) =>
              e.studentId == studentId &&
              e.sessionId == sessionId &&
              e.packageId == null,
        )
        .firstOrNull;
    if (single != null) {
      return PaymentStatusResult(
        status: StudentPaymentStatus.paidSingle,
        paymentId: single.id,
        debtAmount: paymentDebtFor(single.id),
        detail: _debtCoverageLabel(single.id, 'دفع الحصة مسجل'),
      );
    }
    if (attendance?.makeupSourceGroupId != null) {
      final package = _activePackages
          .where((e) => e.id == attendance?.packageId)
          .firstOrNull;
      return package == null
          ? const PaymentStatusResult(
              status: StudentPaymentStatus.notPaid,
              detail: 'تعويض غير مسدد؛ أُلغيت دفعة الحصة',
            )
          : PaymentStatusResult(
              status: StudentPaymentStatus.paidPackage,
              packageId: package.id,
              paymentId: package.paymentId,
              debtAmount: paymentDebtFor(package.paymentId),
              detail: _debtCoverageLabel(
                package.paymentId,
                'التعويض محسوب من باقة المجموعة الأصلية',
              ),
            );
    }
    if (isCairoGroup(session.groupId)) {
      return const PaymentStatusResult(
        status: StudentPaymentStatus.free,
        detail: 'حضور القاهرة برصد الامتحان؛ لا يلزم دفع',
      );
    }
    if (session.kind == SessionKind.free) {
      return const PaymentStatusResult(
        status: StudentPaymentStatus.free,
        detail: 'حصة مجانية؛ لا يلزم دفع',
      );
    }
    if (session.kind == SessionKind.counted) {
      if (attendance != null &&
          attendance.status != AttendanceStatus.absent &&
          attendance.packageId == null) {
        return const PaymentStatusResult(
          status: StudentPaymentStatus.notPaid,
          detail: 'الحضور محفوظ؛ لا يوجد دفع فعلي أو خصم من رصيد لهذه الحصة',
        );
      }
      final linked = _paymentsForStudent(studentId)
          .where(
            (e) =>
                e.studentId == studentId &&
                e.sessionId == sessionId &&
                e.packageId != null,
          )
          .firstOrNull;
      final packageId =
          attendance?.packageId ??
          linked?.packageId ??
          _available(
            studentId,
            session.groupId,
            forClosure: session,
          ).firstOrNull?.id;
      if (packageId != null) {
        final package = _state.packages.firstWhere((e) => e.id == packageId);
        return PaymentStatusResult(
          status: StudentPaymentStatus.paidPackage,
          paymentId: package.paymentId,
          packageId: package.id,
          debtAmount: paymentDebtFor(package.paymentId),
          detail: _debtCoverageLabel(
            package.paymentId,
            attendance?.packageId != null
                ? 'الحصة محسوبة من باقة مدفوعة${attendance?.status == AttendanceStatus.absent ? ' رغم الغياب' : ''}'
                : 'باقة مدفوعة تغطي هذه الحصة؛ لم يُسجل الدخول منها بعد',
          ),
        );
      }
    }
    return PaymentStatusResult(
      status: StudentPaymentStatus.notPaid,
      detail: attendance?.importSource.isNotEmpty == true
          ? 'حضور مستورد؛ حالة الدفع غير موثقة في الملفات'
          : 'لا يوجد دفع مسجل أو باقة مؤهلة لهذه الحصة',
    );
  }

  int? paymentReviewAmountFor(String studentId, String sessionId) {
    _require(canCollect);
    final centerFeeOnly = _attendancesForStudent(studentId).any(
      (entry) =>
          entry.studentId == studentId &&
          entry.sessionId == sessionId &&
          entry.centerFeeOnly,
    );
    final amount = centerFeeOnly
        ? centerFeeCollectedFor(studentId, sessionId)
        : _sessionReviewCollections(
            studentId,
            sessionId,
            _paymentsForStudent(studentId),
            _state.debtSettlements,
          );
    // Coverage can come from an earlier month purchase without a receipt here.
    return amount <= 0 ? null : amount;
  }

  bool isPaymentCheckCurrent(String studentId, String sessionId) {
    _require(canCollect);
    if (_session(sessionId).status == SessionStatus.canceled) return false;
    final previous = _state.paymentChecks
        .where(
          (check) =>
              check.studentId == studentId && check.sessionId == sessionId,
        )
        .firstOrNull;
    if (previous == null ||
        !_attendancesForStudent(studentId).any(
          (entry) =>
              entry.studentId == studentId &&
              entry.sessionId == sessionId &&
              entry.status != AttendanceStatus.absent,
        )) {
      return false;
    }
    final amount = paymentReviewAmountFor(studentId, sessionId);
    final status = paymentStatusFor(studentId, sessionId);
    return amount != null &&
        previous.amount == amount &&
        previous.status == status.status &&
        previous.paymentId == status.paymentId &&
        previous.packageId == status.packageId;
  }

  Future<void> checkPayment({
    required String studentId,
    required String sessionId,
    int? expectedAmount,
  }) => _change(
    'payment_check',
    'مراجعة الدفع بالكود',
    () async {
      _require(canCollect);
      if (expectedAmount == null || expectedAmount <= 0) {
        throw const CenterException('اختر مبلغ الإيصال قبل حفظ المراجعة.');
      }
      final status = paymentStatusFor(studentId, sessionId);
      if (!_activeAttendances.any(
        (entry) =>
            entry.studentId == studentId &&
            entry.sessionId == sessionId &&
            entry.status != AttendanceStatus.absent,
      )) {
        throw const CenterException('راجع طالبًا مسجلًا ضمن حضور هذه الحصة.');
      }
      final amount = paymentReviewAmountFor(studentId, sessionId);
      if (amount == null) {
        throw const CenterException(
          'لا يوجد تحصيل فعلي لهذا الطالب في الحصة؛ لم تُحفظ علامة مراجعة إيصال. الحضور محفوظ.',
        );
      }
      if (expectedAmount != amount) {
        throw const CenterException(
          'مبلغ الإيصال لا يطابق المبلغ المحصّل في هذه الحصة؛ لم تُحفظ المراجعة.',
        );
      }
      final previous = _state.paymentChecks
          .where((e) => e.studentId == studentId && e.sessionId == sessionId)
          .firstOrNull;
      if (previous != null) {
        throw const CenterException(
          'الطالب مُعلّم كمُراجع بالفعل؛ أزل العلامة أولًا إذا أردت مراجعتها من جديد.',
        );
      }
      final check = PaymentCheck(
        id: _uuid.v4(),
        studentId: studentId,
        sessionId: sessionId,
        status: status.status,
        paymentId: status.paymentId,
        packageId: status.packageId,
        staffId: currentUser!.id,
        checkedAt: DateTime.now(),
        amount: amount,
      );
      _replace(_state.paymentChecks, check.id, check, (e) => e.id);
    },
    auditDescription: () =>
        'مراجعة كود ${_student(studentId).code} — حصة ${_session(sessionId).number} — مبلغ محصّل ${_auditAmount(paymentReviewAmountFor(studentId, sessionId)!)}',
  );

  Future<void> uncheckPayment({
    required String studentId,
    required String sessionId,
  }) {
    var removed = false;
    return _change(
      'payment_uncheck',
      'إزالة علامة مراجعة دفع الطالب',
      () async {
        _require(canCollect);
        _student(studentId);
        _session(sessionId);
        final before = _state.paymentChecks.length;
        _state.paymentChecks.removeWhere(
          (check) =>
              check.studentId == studentId && check.sessionId == sessionId,
        );
        removed = _state.paymentChecks.length != before;
      },
      commitWhen: () => removed,
      auditDescription: () =>
          'إزالة مراجعة الطالب $studentId — الحصة $sessionId؛ دون تغيير الدفع أو الحضور',
    );
  }

  Future<void> clearPaymentChecks({required String sessionId}) {
    var removed = 0;
    return _change(
      'payment_checks_clear',
      'إزالة علامات مراجعة الحصة',
      () async {
        _require(canCollect);
        _session(sessionId);
        final before = _state.paymentChecks.length;
        _state.paymentChecks.removeWhere(
          (check) => check.sessionId == sessionId,
        );
        removed = before - _state.paymentChecks.length;
      },
      commitWhen: () => removed > 0,
      auditDescription: () =>
          'إزالة $removed علامات مراجعة — الحصة $sessionId؛ دون تغيير الدفع أو الحضور',
    );
  }

  Future<void> savePaymentReview(ReviewRequest request) => _change(
    'payment_review_save',
    'مراجعة ورقية',
    () async {
      _require(canCollect);
      _student(request.studentId);
      if (request.paperAmount < 0) {
        throw const CenterException('المبلغ الورقي لا يمكن أن يكون سالبًا.');
      }
      final previous = request.id.isEmpty
          ? null
          : _find(
              _state.reviews,
              (e) => e.id == request.id,
              'صف المراجعة غير موجود.',
            );
      if (previous?.sessionId != null &&
          closings.any((e) => e.sessionId == previous!.sessionId)) {
        throw const CenterException(
          'الحصة مقفلة ماليًا؛ لا يمكن تغيير مراجعتها الورقية.',
        );
      }
      PaymentRecord? payment;
      if (request.paymentId != null) {
        final linkedPayment = _find<PaymentRecord>(
          _activePayments,
          (e) => e.id == request.paymentId,
          'عملية الدفع غير موجودة.',
        );
        if (linkedPayment.studentId != request.studentId) {
          throw const CenterException('عملية الدفع لا تخص كود هذا الطالب.');
        }
        if (request.sessionId != null &&
            linkedPayment.sessionId != request.sessionId) {
          throw const CenterException(
            'عملية الدفع لا تخص الحصة المختارة؛ لا يمكن إسناد دفعة غير مرتبطة إلى حصة.',
          );
        }
        if (_state.reviews.any(
          (e) => e.id != request.id && e.paymentId == linkedPayment.id,
        )) {
          throw const CenterException(
            'عملية الدفع لها مراجعة بالفعل؛ عدل صف المراجعة الموجود بدل تكراره.',
          );
        }
        payment = linkedPayment;
      }
      final sessionId = request.sessionId ?? payment?.sessionId;
      if (sessionId != null) {
        final session = _session(sessionId);
        if (session.status == SessionStatus.canceled) {
          throw const CenterException('الحصة ملغاة.', diagnosticCode: 1202);
        }
        if (closings.any((e) => e.sessionId == sessionId)) {
          throw const CenterException(
            'الحصة مقفلة ماليًا؛ لا تقبل مراجعات جديدة أو تعديلًا.',
          );
        }
      }
      final record = PaymentReview(
        id: previous?.id ?? _uuid.v4(),
        studentId: request.studentId,
        sessionId: sessionId,
        paymentId: payment?.id,
        paperAmount: request.paperAmount,
        expectedAmount: payment?.collectedAmount ?? 0,
        notes: request.notes.trim(),
        staffId: currentUser!.id,
        createdAt: DateTime.now(),
      );
      _replace(_state.reviews, record.id, record, (e) => e.id);
    },
    auditDescription: () {
      final review = request.id.isEmpty
          ? _state.reviews.last
          : _state.reviews.firstWhere((e) => e.id == request.id);
      final code = _student(review.studentId).code;
      final context = review.sessionId == null
          ? 'بدون حصة'
          : 'حصة ${_session(review.sessionId!).number}';
      return 'مراجعة ${review.id} — كود $code — $context — الورقي ${_auditAmount(review.paperAmount)}، المسجل ${_auditAmount(review.expectedAmount)}، الفرق ${_auditAmount(review.difference)} — ${review.paymentId == null ? 'بدون دفعة مسجلة' : 'عملية ${review.paymentId}'}';
    },
  );

  Future<void> finalizeSession({
    required String sessionId,
    required int actualCash,
    String notes = '',
  }) => _change(
    'session_finalize',
    'تقفيل مالي للحصة',
    () async {
      _require(canCollect);
      final session = _session(sessionId);
      if (session.status != SessionStatus.closed) {
        throw const CenterException('أغلق الحضور أولًا قبل التقفيل المالي.');
      }
      if (actualCash < 0) {
        throw const CenterException('النقدية الفعلية لا يمكن أن تكون سالبة.');
      }
      if (closings.any((e) => e.sessionId == sessionId)) {
        throw const CenterException(
          'الحصة مقفلة ماليًا بالفعل؛ التقفيلة محفوظة ولا يمكن تعديلها.',
        );
      }
      _state.closings.add(
        SessionClosing(
          id: _uuid.v4(),
          sessionId: sessionId,
          summary: sessionFinancialSummary(sessionId),
          actualCash: actualCash,
          notes: notes.trim(),
          staffId: currentUser!.id,
          createdAt: DateTime.now(),
        ),
      );
    },
    auditDescription: () {
      final closing = closings.firstWhere((e) => e.sessionId == sessionId);
      final session = _session(sessionId);
      return 'تقفيل حصة ${session.number} — ${groupLabel(session.groupId)} — النقدية المتوقعة ${_auditAmount(closing.summary.expectedCash)}، الفعلية ${_auditAmount(actualCash)}، الفرق ${_auditAmount(closing.difference)}';
    },
  );

  int eligibleRemainingFor(String studentId, String sessionId) {
    final session = _session(sessionId);
    final reserved =
        session.status == SessionStatus.open &&
            _activeAttendances.any(
              (e) =>
                  e.studentId == studentId &&
                  e.sessionId == sessionId &&
                  e.status == AttendanceStatus.absent &&
                  e.packageId != null,
            )
        ? 1
        : 0;
    return _available(
      studentId,
      session.groupId,
      forClosure: session,
    ).fold(reserved, (total, package) => total + package.remaining);
  }

  int remainingFor(String studentId, String groupId) => _activePackages
      .where((e) => e.studentId == studentId && e.groupId == groupId)
      .fold(0, (sum, e) => sum + e.remaining);
  int attendanceCount(String sessionId) => _activeAttendances
      .where(
        (e) => e.sessionId == sessionId && e.status != AttendanceStatus.absent,
      )
      .map((e) => e.studentId)
      .toSet()
      .length;
  List<StudyGroup> eligibleMakeupSourceGroups(
    String studentId,
    String targetSessionId,
  ) {
    final student = _student(studentId);
    final target = _session(targetSessionId);
    final targetGroup = _group(target.groupId);
    if (target.status != SessionStatus.open) {
      return const [];
    }
    final recordedSource = _entryMakeupSourceGroupId(
      EntryRequest(
        studentId: studentId,
        sessionId: targetSessionId,
        mode: EntryMode.makeup,
      ),
    );
    if (recordedSource == null && student.groupIds.contains(targetGroup.id)) {
      return const [];
    }
    return _state.groups.where((source) {
      return source.id != targetGroup.id &&
          (recordedSource != null
              ? source.id == recordedSource
              : student.groupIds.contains(source.id)) &&
          source.subjectId == targetGroup.subjectId &&
          source.gradeId == targetGroup.gradeId;
    }).toList();
  }

  int eligibleMakeupRemainingFor(
    String studentId,
    String targetSessionId,
    String sourceGroupId,
  ) {
    if (!eligibleMakeupSourceGroups(
      studentId,
      targetSessionId,
    ).any((source) => source.id == sourceGroupId)) {
      throw const CenterException(
        'اختر مجموعة أصلية مسجلًا فيها الطالب بنفس المادة والصف.',
      );
    }
    return _available(
      studentId,
      sourceGroupId,
      forClosure: _session(targetSessionId),
    ).fold(0, (total, package) => total + package.remaining);
  }

  List<AttendanceRecord> eligibleMakeups(
    String studentId,
    String targetSessionId,
  ) {
    final targets = _state.sessions.where(
      (e) => e.id == targetSessionId && e.status == SessionStatus.open,
    );
    if (targets.isEmpty) return [];
    final target = targets.first;
    final targetGroup = _group(target.groupId);
    final usedOriginals = _activeAttendances
        .where((e) => e.originalAttendanceId != null)
        .map((e) => e.originalAttendanceId)
        .toSet();
    return _activeAttendances.where((record) {
      if (record.studentId != studentId ||
          record.status != AttendanceStatus.absent ||
          record.packageId == null ||
          record.sessionId == target.id) {
        return false;
      }
      if (usedOriginals.contains(record.id)) {
        return false;
      }
      final original = _session(record.sessionId);
      final originalGroup = _group(original.groupId);
      return original.status != SessionStatus.canceled &&
          !target.startsAt.isBefore(original.startsAt) &&
          originalGroup.subjectId == targetGroup.subjectId &&
          originalGroup.gradeId == targetGroup.gradeId;
    }).toList();
  }

  String catalogName(String id) =>
      _state.catalogs.where((e) => e.id == id).map((e) => e.name).firstOrNull ??
      'غير محدد';
  String groupLabel(String id) {
    final matches = _state.groups.where((e) => e.id == id);
    if (matches.isEmpty) return 'مجموعة غير موجودة';
    final group = matches.first;
    return '${group.name} • ${catalogName(group.centerId)} • ${catalogName(group.gradeId)}';
  }

  void _validateExportPath(String destination, String extension) {
    final target = p.normalize(p.absolute(destination)).toLowerCase();
    final database = p.normalize(p.absolute(databasePath)).toLowerCase();
    final automaticFolder = automaticBackupDirectory;
    if (automaticFolder != null &&
        p.isWithin(
          p.normalize(p.absolute(automaticFolder)).toLowerCase(),
          target,
        )) {
      throw const CenterException(
        'هذا المجلد مخصص للنسخ التلقائية التي يتم تنظيفها؛ احفظ النسخة اليدوية في مجلد آخر.',
      );
    }
    if (target == database ||
        target == '$database-wal' ||
        target == '$database-shm' ||
        target == '$database-journal' ||
        p.extension(target) != extension) {
      throw CenterException(
        'اختر ملفًا جديدًا بامتداد $extension بعيدًا عن ملفات قاعدة البيانات.',
      );
    }
    if (File(destination).existsSync()) {
      throw const CenterException(
        'الملف موجود بالفعل؛ اختر اسمًا جديدًا للحفاظ عليه.',
      );
    }
  }

  Future<void> _writeBackup(String destination, CenterState state) async {
    final file = File(destination);
    await file.parent.create(recursive: true);
    final temporary = File('$destination.${_uuid.v4()}.tmp');
    try {
      await temporary.writeAsString(
        jsonEncode({
          'format': 'massar-center-backup',
          'exportedAt': DateTime.now().toIso8601String(),
          'data': state.toJson(),
        }),
        flush: true,
      );
      await temporary.rename(destination);
    } catch (_) {
      if (await temporary.exists()) await temporary.delete();
      rethrow;
    }
  }

  Future<String> createBackup({String? destination}) async {
    String result = '';
    await _exclusive(() async {
      _require(canManage);
      result =
          destination ??
          p.join(
            p.dirname(databasePath),
            'backups',
            'backup-${DateTime.now().millisecondsSinceEpoch}.json',
          );
      if (p.equals(p.absolute(result), p.absolute(databasePath))) {
        throw const CenterException('اختر ملفًا مختلفًا عن قاعدة البيانات.');
      }
      _validateExportPath(result, '.json');
      try {
        await _writeBackup(result, _state);
      } catch (error, stackTrace) {
        throw CenterException(
          'تعذر حفظ النسخة الاحتياطية في المكان المحدد.',
          cause: error,
          stackTrace: stackTrace,
        );
      }
    }, operation: 'backup.create');
    return result;
  }

  Future<void> restoreBackup(String path) => _exclusive(() async {
    _require(canManage);
    CenterState restored;
    try {
      final source = File(path);
      if (await source.length() > 100 * 1024 * 1024) {
        throw const CenterException(
          'ملف النسخة الاحتياطية أكبر من الحد المسموح.',
        );
      }
      final content =
          jsonDecode(await source.readAsString()) as Map<String, dynamic>;
      if (content['format'] != 'massar-center-backup') {
        throw const CenterException('هذا الملف ليس نسخة احتياطية لمسار.');
      }
      restored = CenterState.fromJson(
        Map<String, dynamic>.from(content['data'] as Map),
      );
      validateState(restored);
      if (!canConfigureCards &&
          !mapEquals(restored.cardSettings.toJson(), cardSettings.toJson())) {
        throw const CenterException(
          'النسخة تغير إعدادات الكارت؛ حساب المالك فقط يمكنه استعادتها.',
        );
      }
      if (_applyInstallationAdmin(restored)) validateState(restored);
      if (_applyDefaultMonthPlans(restored)) validateState(restored);
      if (_ensureStudyMonths(restored)) validateState(restored);
      if (restored.staff.isEmpty) {
        throw const CenterException('النسخة لا تحتوي حساب مدير صالح.');
      }
    } catch (error, stackTrace) {
      if (error is CenterException) rethrow;
      throw CenterException(
        'النسخة غير صالحة؛ لم تتغير البيانات.',
        cause: error,
        stackTrace: stackTrace,
      );
    }
    final preservation = p.join(
      p.dirname(databasePath),
      'backups',
      'before-restore-${DateTime.now().millisecondsSinceEpoch}.json',
    );
    await _writeBackup(preservation, _state);
    restored.audit.add(
      AuditRecord(
        id: _uuid.v4(),
        action: 'backup_restore',
        description:
            'استعادة نسخة احتياطية مع حفظ نسخة ما قبل الاستعادة وحساب مدير التثبيت',
        staffId: restored.staff.firstWhere((e) => e.role == StaffRole.admin).id,
        createdAt: DateTime.now(),
      ),
    );
    await _database!.transaction((tx) async {
      final updated = await tx.update('state', {
        'payload': jsonEncode(restored.toJson()),
      }, where: 'id = 1');
      if (updated != 1) {
        throw const CenterException(
          'سجل قاعدة البيانات مفقود؛ لم تُستعد النسخة.',
        );
      }
    });
    _state = restored;
    _stateEncoder.clearPublicHistory();
    _authenticationRevision++;
    _currentUser = null;
    notifyListeners();
  }, operation: 'backup.restore');
  Future<String> exportReport(String destination) async {
    await _exclusive(() async {
      _require(canManage);
      String cell(Object? value) {
        var text = (value ?? '').toString();
        if (value is String && RegExp(r'^\s*[=+@\-]').hasMatch(text)) {
          text = "'$text";
        }
        return '"${text.replaceAll('"', '""')}"';
      }

      _validateExportPath(destination, '.csv');
      final rows = <List<Object?>>[
        [
          'رقم العملية',
          'الطالب',
          'الكود',
          'المجموعة',
          'التاريخ',
          'البيان',
          'السعر بالقرش',
          'الخصم ٪',
          'المستحق بالقرش',
          'المتحصل بالقرش',
          'المديونية الحالية بالقرش',
          'وسيلة الدفع',
          'الموظف',
        ],
        ..._financialPayments.map(
          (e) => [
            e.id,
            _student(e.studentId).name,
            _student(e.studentId).code,
            groupLabel(e.groupId),
            e.createdAt.toIso8601String(),
            e.description,
            e.baseAmount,
            e.discountPercent,
            e.netAmount,
            e.collectedAmount,
            paymentDebtFor(e.id),
            e.method,
            _state.staff.firstWhere((u) => u.id == e.staffId).name,
          ],
        ),
        ..._state.cardPayments.map(
          (e) => [
            e.id,
            _student(e.studentId).name,
            _student(e.studentId).code,
            e.groupId == null ? '' : groupLabel(e.groupId!),
            e.createdAt.toIso8601String(),
            'رسوم كارت',
            e.baseAmount,
            e.discountPercent,
            e.netAmount,
            e.collectedAmount,
            cardDebtFor(e.id),
            e.method,
            _state.staff.firstWhere((u) => u.id == e.staffId).name,
          ],
        ),
        ..._state.debtSettlements.map((e) {
          final groupId = e.kind == DebtKind.lesson
              ? _state.payments.firstWhere((p) => p.id == e.paymentId).groupId
              : _state.cardPayments
                    .firstWhere((p) => p.id == e.paymentId)
                    .groupId;
          return [
            e.id,
            _student(e.studentId).name,
            _student(e.studentId).code,
            groupId == null ? '' : groupLabel(groupId),
            e.createdAt.toIso8601String(),
            'تسديد مديونية — عملية ${e.paymentId}',
            0,
            0,
            0,
            e.amount,
            0,
            e.method,
            _state.staff.firstWhere((u) => u.id == e.staffId).name,
          ];
        }),
        ..._state.refunds.map(
          (e) => [
            e.id,
            _student(e.studentId).name,
            _student(e.studentId).code,
            groupLabel(e.groupId),
            e.createdAt.toIso8601String(),
            'استرداد فعلي — ${e.reason}',
            0,
            0,
            0,
            -e.amount,
            0,
            e.method,
            _state.staff.firstWhere((u) => u.id == e.staffId).name,
          ],
        ),
      ];
      final file = File(destination);
      await file.parent.create(recursive: true);
      final temporary = File('$destination.${_uuid.v4()}.tmp');
      try {
        await temporary.writeAsString(
          '\uFEFF${rows.map((r) => r.map(cell).join(',')).join('\r\n')}',
          flush: true,
        );
        await temporary.rename(destination);
      } catch (error, stackTrace) {
        if (await temporary.exists()) await temporary.delete();
        throw CenterException(
          'تعذر حفظ التقرير؛ لم يتغير ملف البيانات.',
          cause: error,
          stackTrace: stackTrace,
        );
      }
    }, operation: 'reports.export');
    return destination;
  }

  Future<void> close() async {
    _automaticBackupTimer?.cancel();
    _automaticBackupTimer = null;
    if (_closed) return;
    await _exclusive(() async {
      await _database!.close();
      await _fileLock!.unlock();
      await _fileLock.close();
      _openFiles.remove(databasePath);
      _closed = true;
    }, operation: 'database.close');
  }
}

int _sessionReviewCollections(
  String studentId,
  String sessionId,
  Iterable<PaymentRecord> payments,
  Iterable<DebtSettlement> settlements,
) {
  final studentPayments = payments
      .where((p) => p.studentId == studentId)
      .toList();
  final paymentIds = studentPayments.map((p) => p.id).toSet();
  return studentPayments
          .where((p) => p.sessionId == sessionId)
          .fold<int>(0, (sum, p) => sum + p.collectedAmount) +
      settlements
          .where(
            (s) =>
                s.studentId == studentId &&
                s.sessionId == sessionId &&
                s.kind == DebtKind.lesson &&
                paymentIds.contains(s.paymentId),
          )
          .fold<int>(0, (sum, s) => sum + s.amount);
}

/// Validates both foreign keys and financial invariants before every disk commit
/// and before accepting an imported backup. No partially valid snapshot is used.
void validateState(CenterState state) {
  void check(bool condition, String message) {
    if (!condition) throw CenterException(message);
  }

  check(
    state.appliedDataRepairs.every((id) => id.trim().isNotEmpty) &&
        state.appliedDataRepairs.toSet().length ==
            state.appliedDataRepairs.length,
    'سجل تصحيح البيانات يحتوي معرّفًا فارغًا أو مكررًا.',
  );

  check(
    state.defaultMonthPriceVersion >= 0 && state.defaultMonthPriceVersion <= 1,
    'إصدار إعداد سعر الشهر غير مدعوم.',
  );
  final ids = <String>{};
  void identify(Iterable<String> values) {
    for (final id in values) {
      check(
        id.isNotEmpty && ids.add(id),
        'البيانات تحتوي معرفًا فارغًا أو مكررًا.',
      );
    }
  }

  identify(state.catalogs.map((e) => e.id));
  identify(state.groups.map((e) => e.id));
  identify(state.students.map((e) => e.id));
  identify(state.sessions.map((e) => e.id));
  identify(state.studyMonths.map((e) => e.id));
  identify(state.studyMonths.expand((m) => m.lessons).map((e) => e.id));
  identify(state.packages.map((e) => e.id));
  identify(state.attendances.map((e) => e.id));
  identify(state.payments.map((e) => e.id));
  identify(state.centerFees.map((e) => e.id));
  identify(state.debtSettlements.map((e) => e.id));
  identify(state.academics.map((e) => e.id));
  identify(state.academicActivities.map((e) => e.id));
  identify(state.audit.map((e) => e.id));
  identify(state.staff.map((e) => e.id));
  identify(state.reviews.map((e) => e.id));
  identify(state.closings.map((e) => e.id));
  identify(state.paymentChecks.map((e) => e.id));
  identify(state.corrections.map((e) => e.id));
  identify(state.refunds.map((e) => e.id));
  identify(state.cardPayments.map((e) => e.id));
  identify(state.cardReceipts.map((e) => e.id));
  final catalogs = {for (final e in state.catalogs) e.id: e};
  final groups = {for (final e in state.groups) e.id: e};
  final students = {for (final e in state.students) e.id: e};
  final sessions = {for (final e in state.sessions) e.id: e};
  final centerFeesById = {for (final fee in state.centerFees) fee.id: fee};
  for (final fee in state.centerFees) {
    check(
      students.containsKey(fee.studentId) &&
          sessions.containsKey(fee.sessionId) &&
          fee.amount > 0 &&
          fee.paidAmount >= 0 &&
          fee.paidAmount <= fee.amount &&
          fee.method.trim().isNotEmpty &&
          (fee.staffId == null ||
              state.staff.any(
                (user) =>
                    user.id == fee.staffId && user.role != StaffRole.assistant,
              )),
      'رسوم السنتر مرتبطة بطالب أو حصة غير موجودة، أو مبلغ غير صالح.',
    );
    if (fee.originalFeeId != null) {
      final original = centerFeesById[fee.originalFeeId];
      check(
        original != null &&
            original.originalFeeId == null &&
            original.id != fee.id &&
            original.studentId == fee.studentId &&
            original.sessionId == fee.sessionId &&
            fee.paidAmount == fee.amount &&
            !fee.recordedAt.isBefore(original.recordedAt),
        'إيصال سداد رسوم السنتر غير مرتبط بأصل الرسوم الصحيح.',
      );
    } else {
      final paid = state.centerFees
          .where((receipt) => receipt.originalFeeId == fee.id)
          .fold<int>(
            fee.paidAmount,
            (sum, receipt) => sum + receipt.paidAmount,
          );
      check(paid <= fee.amount, 'تحصيل رسوم السنتر أكبر من المبلغ المستحق.');
    }
  }
  final packages = {for (final e in state.packages) e.id: e};
  final payments = {for (final e in state.payments) e.id: e};
  final attendance = {for (final e in state.attendances) e.id: e};
  final staffIds = state.staff.map((e) => e.id).toSet();
  final correctionById = {for (final e in state.corrections) e.id: e};
  final closingById = {for (final e in state.closings) e.id: e};
  final voidAttendances = <String>{};
  final voidPayments = <String>{};
  final voidPackages = <String>{};
  final reopenedClosings = <String>{};
  final effectiveMethods = <String, String>{};
  final collectors = state.staff
      .where((e) => e.role != StaffRole.assistant)
      .map((e) => e.id)
      .toSet();
  check(
    state.cardSettings.price == null || state.cardSettings.price! >= 0,
    'سعر الكارت المحفوظ غير صالح.',
  );
  final cardPaymentsById = {for (final e in state.cardPayments) e.id: e};
  final cardPayers = <String>{};
  for (final payment in state.cardPayments) {
    final session = sessions[payment.sessionId];
    check(
      students.containsKey(payment.studentId) &&
          collectors.contains(payment.staffId) &&
          cardPayers.add(payment.studentId) &&
          payment.baseAmount >= 0 &&
          payment.discountPercent.isFinite &&
          payment.discountPercent >= 0 &&
          payment.discountPercent <= 100 &&
          payment.netAmount ==
              discountedAmount(payment.baseAmount, payment.discountPercent) &&
          payment.collectedAmount >= 0 &&
          payment.collectedAmount <= payment.netAmount &&
          payment.method.trim().isNotEmpty &&
          (payment.sessionId == null
              ? payment.groupId == null
              : session != null &&
                    session.status != SessionStatus.canceled &&
                    session.groupId == payment.groupId),
      'دفع الكارت مكرر أو مرتبط بهوية أو مبلغ غير صالح.',
    );
  }
  final cardRecipients = <String>{};
  for (final receipt in state.cardReceipts) {
    final payment = cardPaymentsById[receipt.paymentId];
    check(
      students.containsKey(receipt.studentId) &&
          collectors.contains(receipt.staffId) &&
          cardRecipients.add(receipt.studentId) &&
          (receipt.paymentBypassed
              ? receipt.paymentId == null &&
                    !cardPayers.contains(receipt.studentId)
              : payment != null &&
                    payment.studentId == receipt.studentId &&
                    !receipt.receivedAt.isBefore(payment.createdAt)),
      'استلام الكارت مكرر أو لا يطابق دفع الطالب وتاريخه.',
    );
  }
  for (final e in state.corrections) {
    check(
      e.reason.trim().isNotEmpty && collectors.contains(e.staffId),
      'سبب التصحيح أو الموظف غير صالح.',
    );
    if (e.studentId != null) {
      check(
        students.containsKey(e.studentId),
        'التصحيح مرتبط بطالب غير موجود.',
      );
    }
    if (e.sessionId != null) {
      check(
        sessions.containsKey(e.sessionId),
        'التصحيح مرتبط بحصة غير موجودة.',
      );
    }
    if (e.attendanceId != null) {
      final original = attendance[e.attendanceId];
      check(
        original != null &&
            original.studentId == e.studentId &&
            original.sessionId == e.sessionId &&
            voidAttendances.add(original.id),
        'التصحيح لا يطابق الحضور أو تم إلغاء التسجيل مرتين.',
      );
      if (e.replacementAttendanceId != null) {
        final replacement = attendance[e.replacementAttendanceId];
        check(
          replacement != null &&
              replacement.id != original!.id &&
              replacement.studentId == e.studentId &&
              replacement.sessionId == e.sessionId,
          'الحضور البديل لا يطابق التصحيح.',
        );
      }
    }
    if (e.voidsPayment) {
      final payment = payments[e.paymentId];
      check(
        payment != null &&
            payment.studentId == e.studentId &&
            payment.sessionId == e.sessionId &&
            voidPayments.add(payment.id),
        'عملية الدفع الملغاة لا تطابق التصحيح أو ألغيت مرتين.',
      );
      check(
        payment!.packageId == null ||
            (e.voidsPackage && e.packageId == payment.packageId),
        'استرداد شراء الباقة يجب أن يلغي الباقة نفسها.',
      );
    }
    if (e.voidsPackage) {
      final package = packages[e.packageId];
      check(
        package != null &&
            package.studentId == e.studentId &&
            package.paymentId == e.paymentId &&
            e.voidsPayment &&
            voidPackages.add(package.id),
        'الباقة الملغاة لا تطابق التصحيح أو ألغيت مرتين.',
      );
    }
    if (e.action == CorrectionAction.paymentMethod) {
      final payment = payments[e.paymentId];
      check(
        payment != null &&
            payment.studentId == e.studentId &&
            payment.sessionId == e.sessionId &&
            !e.voidsPayment &&
            !e.voidsPackage &&
            e.attendanceId == null &&
            e.oldMethod == (effectiveMethods[e.paymentId] ?? payment.method) &&
            (e.newMethod?.trim().isNotEmpty ?? false) &&
            e.oldMethod != e.newMethod,
        'سجل تغيير وسيلة الدفع غير متسلسل أو لا يطابق العملية.',
      );
      effectiveMethods[e.paymentId!] = e.newMethod!;
    } else if (e.action == CorrectionAction.closingReopened) {
      final closing = closingById[e.closingId];
      check(
        closing != null &&
            closing.sessionId == e.sessionId &&
            e.attendanceId == null &&
            !e.voidsPayment &&
            !e.voidsPackage &&
            reopenedClosings.add(closing.id),
        'إعادة فتح التقفيلة لا تطابق السجل أو مكررة.',
      );
    } else if (e.action == CorrectionAction.paymentCanceled) {
      check(
        e.voidsPayment &&
            !e.voidsPackage &&
            e.attendanceId == null &&
            e.replacementAttendanceId == null &&
            e.packageId == null &&
            payments[e.paymentId]?.packageId == null &&
            e.closingId == null &&
            e.oldMethod == null &&
            e.newMethod == null,
        'إلغاء الدفع يجب أن يطابق دفعة حصة دون حذف حضور أو باقة.',
      );
    } else if (e.action == CorrectionAction.packageRefund) {
      check(
        e.voidsPackage && e.voidsPayment && e.attendanceId == null,
        'استرداد الباقة لا يلغي الحضور أو الدفع بصورة صحيحة.',
      );
    } else {
      check(
        e.attendanceId != null,
        'تصحيح الدخول يجب أن يرتبط بالتسجيل الأصلي.',
      );
      final original = attendance[e.attendanceId]!;
      if (e.action == CorrectionAction.absencePresent) {
        final replacement = attendance[e.replacementAttendanceId];
        check(
          original.status == AttendanceStatus.absent &&
              replacement?.status == AttendanceStatus.present &&
              (original.packageId != null
                  ? replacement?.packageId == original.packageId
                  : replacement?.packageId == null ||
                        (sessions[original.sessionId]?.kind ==
                                SessionKind.counted &&
                            packages[replacement?.packageId]?.studentId ==
                                original.studentId &&
                            packages[replacement?.packageId]?.groupId ==
                                sessions[original.sessionId]?.groupId)) &&
              e.packageId == replacement?.packageId &&
              !e.voidsPayment &&
              !e.voidsPackage,
          'تصحيح الغياب إلى حضور غير صالح.',
        );
      } else {
        check(
          original.status != AttendanceStatus.absent,
          'إلغاء الدخول لا يحذف الغياب المحسوب.',
        );
        if (e.action == CorrectionAction.entryCorrected) {
          check(
            e.replacementAttendanceId != null,
            'تصحيح الدخول لا يحتوي البديل.',
          );
        }
        if (e.action == CorrectionAction.entryReversed &&
            e.replacementAttendanceId != null) {
          final replacement = attendance[e.replacementAttendanceId]!;
          check(
            replacement.status == AttendanceStatus.absent &&
                replacement.packageId == original.packageId &&
                original.status != AttendanceStatus.makeup,
            'إلغاء دخول حصة مغلقة يحفظ الغياب بنفس استهلاك الباقة.',
          );
        }
      }
    }
  }
  final settlementsByPayment = <(DebtKind, String), int>{};
  for (final settlement in state.debtSettlements) {
    final lesson = settlement.kind == DebtKind.lesson
        ? payments[settlement.paymentId]
        : null;
    final card = settlement.kind == DebtKind.card
        ? cardPaymentsById[settlement.paymentId]
        : null;
    final studentId = lesson?.studentId ?? card?.studentId;
    final createdAt = lesson?.createdAt ?? card?.createdAt;
    final due = lesson?.netAmount ?? card?.netAmount;
    final collected = lesson?.collectedAmount ?? card?.collectedAmount;
    check(
      studentId != null &&
          studentId == settlement.studentId &&
          createdAt != null &&
          !settlement.createdAt.isBefore(createdAt) &&
          collectors.contains(settlement.staffId) &&
          settlement.amount > 0 &&
          settlement.method.trim().isNotEmpty &&
          settlement.notes.length <= 2000 &&
          (settlement.sessionId == null ||
              sessions[settlement.sessionId] != null &&
                  sessions[settlement.sessionId]!.status !=
                      SessionStatus.canceled),
      'تسديد المديونية مرتبط بهوية أو مبلغ أو تاريخ غير صالح.',
    );
    if (lesson != null) {
      check(
        !state.corrections.any(
          (e) =>
              e.voidsPayment &&
              e.paymentId == lesson.id &&
              settlement.createdAt.isAfter(e.createdAt),
        ),
        'لا يمكن تسجيل تسديد بعد إلغاء أصل المديونية.',
      );
    }
    final key = (settlement.kind, settlement.paymentId);
    final total = (settlementsByPayment[key] ?? 0) + settlement.amount;
    check(
      due != null && collected != null && collected + total <= due,
      'تسديدات المديونية تتجاوز المبلغ المستحق.',
    );
    settlementsByPayment[key] = total;
  }
  final refundedPayments = <String>{};
  for (final r in state.refunds) {
    final payment = payments[r.paymentId];
    final correction = correctionById[r.correctionId];
    check(
      payment != null &&
          correction != null &&
          correction.voidsPayment &&
          correction.paymentId == payment.id &&
          r.studentId == payment.studentId &&
          r.groupId == payment.groupId &&
          r.sessionId == payment.sessionId &&
          r.packageId == payment.packageId &&
          r.amount ==
              payment.collectedAmount +
                  (settlementsByPayment[(DebtKind.lesson, payment.id)] ?? 0) &&
          r.amount >= 0 &&
          r.method.trim().isNotEmpty &&
          r.reason == correction.reason &&
          r.staffId == correction.staffId &&
          collectors.contains(r.staffId) &&
          refundedPayments.add(payment.id),
      'الاسترداد لا يطابق مبلغ الدفع الأصلي أو مكرر.',
    );
  }
  check(
    voidPayments.length == refundedPayments.length &&
        voidPayments.containsAll(refundedPayments),
    'كل دفع ملغى يجب أن يحتفظ باسترداده الفعلي.',
  );
  for (final record in state.attendances) {
    check(
      record.fixedDiscountPercent == null ||
          (record.fixedDiscountPercent!.isFinite &&
              record.fixedDiscountPercent! >= 0 &&
              record.fixedDiscountPercent! <= 100),
      'نسبة خصم الطالب وقت تسجيل الحضور غير صالحة.',
    );
  }
  final activeAttendance = state.attendances
      .where((e) => !voidAttendances.contains(e.id))
      .toList();
  final activeSinglePairs = state.payments
      .where((e) => !voidPayments.contains(e.id) && e.packageId == null)
      .map((e) => '${e.studentId}:${e.sessionId}')
      .toSet();
  final canceledSinglePairs = state.corrections
      .where(
        (e) => e.action == CorrectionAction.paymentCanceled && e.voidsPayment,
      )
      .map((e) => '${e.studentId}:${e.sessionId}')
      .toSet();
  check(
    state.staff.isEmpty || state.staff.any((e) => e.role == StaffRole.admin),
    'يجب وجود حساب مدير.',
  );
  final staffNames = <String>{};
  for (final user in state.staff) {
    check(
      user.name.trim().isNotEmpty && staffNames.add(user.name.toLowerCase()),
      'اسم الموظف مطلوب ويجب ألا يتكرر.',
    );
    final credential = state.credentials[user.id];
    check(
      credential != null && credential['algorithm'] == 'pbkdf2-sha256-120000',
      'بيانات الدخول غير مكتملة.',
    );
    try {
      check(
        base64Decode(credential!['hash']!).length == 32 &&
            base64Decode(credential['salt']!).length >= 16,
        'بيانات الدخول غير صالحة.',
      );
    } catch (_) {
      throw const CenterException('بيانات الدخول غير صالحة.');
    }
  }
  check(
    state.credentials.keys.every(staffIds.contains),
    'بيانات الدخول مرتبطة بموظف غير موجود.',
  );
  final catalogNames = <String>{};
  for (final entry in state.catalogs) {
    check(
      entry.name.trim().isNotEmpty &&
          catalogNames.add('${entry.kind.name}:${entry.name}'),
      'اسم المادة أو السنتر أو الصف فارغ أو مكرر.',
    );
  }
  final sharedMonthIds = state.studyMonths.map((month) => month.id).toSet();
  for (final group in state.groups) {
    final monthIds = <String>{};
    final monthNames = <String>{};
    for (final plan in group.monthPlans) {
      check(
        plan.id.trim().isNotEmpty &&
            monthIds.add(plan.id) &&
            plan.name.trim().isNotEmpty &&
            plan.name.trim().length <= 120 &&
            monthNames.add(
              '${sharedMonthIds.contains(plan.id) ? "shared" : "legacy"}:${plan.name.trim().toLowerCase()}',
            ) &&
            plan.sessions > 0 &&
            plan.price >= 0,
        'أشهر المجموعة تحتاج أسماء وهويات مختلفة وعدد حصص موجبًا وسعرًا صحيحًا.',
      );
    }
    check(
      group.name.trim().isNotEmpty &&
          group.sessionPrice >= 0 &&
          group.packagePrice >= 0 &&
          (group.twoSessionPrice == null || group.twoSessionPrice! >= 0) &&
          (group.threeSessionPrice == null || group.threeSessionPrice! >= 0),
      'أدخل اسم مجموعة وأسعارًا صحيحة.',
    );
    check(
      catalogs[group.subjectId]?.kind == CatalogKind.subject &&
          catalogs[group.centerId]?.kind == CatalogKind.center &&
          catalogs[group.gradeId]?.kind == CatalogKind.grade,
      'اربط المجموعة بمادة وسنتر وصف صالحين.',
    );
  }
  final codes = <String>{};
  final barcodes = <String>{};
  for (final student in state.students) {
    final hasSuspension =
        student.isSuspended ||
        student.suspensionReason.isNotEmpty ||
        student.suspendedAt != null ||
        student.suspendedBy != null;
    check(
      !hasSuspension ||
          (student.suspensionReason.trim().isNotEmpty &&
              student.suspensionReason.trim().length <= 2000 &&
              student.suspendedAt != null &&
              collectors.contains(student.suspendedBy)),
      'إيقاف الطالب يتطلب سببًا وتاريخًا وموظفًا مسؤولًا صالحًا.',
    );
    check(
      student.twinStudentId == null ||
          (student.twinStudentId != student.id &&
              students[student.twinStudentId]?.twinStudentId == student.id),
      'علاقة التوأم يجب أن تربط طالبين موجودين بصورة متبادلة، دون ربط الطالب بنفسه.',
    );
    check(
      student.name.trim().isNotEmpty &&
          student.code.trim().isNotEmpty &&
          codes.add(student.code.toLowerCase()),
      'اسم الطالب وكوده مطلوبان والكود يجب ألا يتكرر.',
    );
    check(
      student.barcode.isEmpty || barcodes.add(student.barcode),
      'باركود الكارت مكرر بين طالبين.',
    );
    check(
      student.discountPercent.isFinite &&
          student.discountPercent >= 0 &&
          student.discountPercent <= 100,
      'نسبة الخصم من صفر إلى ١٠٠٪.',
    );
    check(
      (!student.packageMember ||
              (!student.centerOnly && !student.centerFeeEnabled)) &&
          student.centerFeeAmount > 0 &&
          (!student.centerOnly || student.discountPercent == 100),
      'رسوم السنتر موجبة، ونظام السنتر فقط يتطلب إعفاء المدرس كاملًا.',
    );
    check(
      student.groupIds.isNotEmpty &&
          student.groupIds.toSet().length == student.groupIds.length &&
          student.groupIds.every(groups.containsKey),
      'اختر مجموعة صالحة للطالب دون تكرار.',
    );
    for (final groupId in student.groupIds) {
      final enrolled = state.enrollments['${student.id}:$groupId'];
      check(
        enrolled != null && DateTime.tryParse(enrolled) != null,
        'تاريخ تسجيل الطالب في المجموعة غير صالح.',
      );
    }
  }
  final monthNumbers = <int>{};
  final lessonDefinitions = <String, (StudyMonth, PreparedLesson)>{};
  for (final month in state.studyMonths) {
    check(
      month.number > 0 &&
          monthNumbers.add(month.number) &&
          month.name.trim().isNotEmpty &&
          month.name.length <= 120 &&
          month.price >= 0 &&
          month.lessons.isNotEmpty &&
          month.lessons.length <= 500,
      'اسم الشهر أو سعره أو عدد حصصه غير صالح.',
    );
    final numbers = <int>{};
    for (final lesson in month.lessons) {
      check(
        lesson.number > 0 &&
            numbers.add(lesson.number) &&
            lesson.name.length <= 120 &&
            lesson.extraPrice >= 0,
        'رقم الحصة داخل الشهر مكرر أو بياناتها غير صالحة.',
      );
      lessonDefinitions[lesson.id] = (month, lesson);
    }
  }
  final sessionKeys = <String>{};
  for (final session in state.sessions) {
    if (session.preparedLessonId != null) {
      final definition = lessonDefinitions[session.preparedLessonId];
      check(
        definition != null &&
            definition.$1.number == session.monthNumber &&
            definition.$2.number == session.number,
        'الحصة مرتبطة بتعريف غير صالح داخل الشهر.',
      );
    }
    check(
      (session.startedAt == null && session.startedBy == null) ||
          (session.startedAt != null &&
              (collectors.contains(session.startedBy) ||
                  (catalogs[groups[session.groupId]?.centerId]?.isCairo ==
                          true &&
                      state.staff.any(
                        (s) =>
                            s.id == session.startedBy &&
                            s.role == StaffRole.assistant,
                      )))),
      'بدء الحصة يحتاج تاريخًا وموظفًا مسؤولًا صالحًا.',
    );
    check(
      groups.containsKey(session.groupId) &&
          session.monthNumber > 0 &&
          session.number > 0 &&
          session.extraPrice >= 0,
      'بيانات الحصة غير صالحة.',
    );
    check(
      sessionKeys.add(
        '${session.groupId}:${session.monthNumber}:${session.number}',
      ),
      'رقم الحصة مسجل بالفعل في الشهر نفسه لهذه المجموعة.',
    );
    final importRoster = session.importRoster;
    check(
      importRoster == null ||
          (state.installationSeedId != null &&
              importRoster.toSet().length == importRoster.length &&
              importRoster.every(students.containsKey)),
      'قائمة طلاب الحصة المستوردة تحتوي هوية غير صالحة أو مكررة.',
    );
  }
  for (final payment in state.payments) {
    check(
      students.containsKey(payment.studentId) &&
          groups.containsKey(payment.groupId) &&
          staffIds.contains(payment.staffId),
      'عملية الدفع مرتبطة ببيانات غير موجودة.',
    );
    check(
      payment.baseAmount >= 0 &&
          payment.discountPercent.isFinite &&
          payment.discountPercent >= 0 &&
          payment.discountPercent <= 100 &&
          payment.netAmount ==
              discountedAmount(payment.baseAmount, payment.discountPercent) &&
          payment.collectedAmount >= 0 &&
          payment.collectedAmount <= payment.netAmount &&
          payment.method.trim().isNotEmpty,
      'حساب الدفع أو الخصم غير صالح.',
    );
    if (payment.sessionId != null) {
      check(
        sessions[payment.sessionId]?.groupId == payment.groupId ||
            state.attendances.any(
              (record) =>
                  record.studentId == payment.studentId &&
                  record.sessionId == payment.sessionId &&
                  record.status == AttendanceStatus.makeup &&
                  record.makeupSourceGroupId == payment.groupId &&
                  record.packageId == payment.packageId,
            ),
        'الدفع مرتبط بحصة مختلفة.',
      );
    }
    if (payment.packageId != null) {
      check(
        packages[payment.packageId]?.paymentId == payment.id,
        'الدفع غير مرتبط بباقته الصحيحة.',
      );
    }
  }
  final consumedByPackage = <String, int>{};
  for (final record in activeAttendance) {
    if (record.packageId != null) {
      consumedByPackage.update(
        record.packageId!,
        (count) => count + 1,
        ifAbsent: () => 1,
      );
    }
  }
  for (final package in state.packages) {
    final payment = payments[package.paymentId];
    check(
      payment != null &&
          payment.packageId == package.id &&
          payment.studentId == package.studentId &&
          payment.groupId == package.groupId &&
          package.remaining >= 0 &&
          package.remaining <= package.totalSessions &&
          (package.monthPlanId == null
              ? package.monthPlanName == null &&
                    (package.totalSessions == 2 ||
                        package.totalSessions == 3 ||
                        package.totalSessions == 4)
              : package.monthPlanId!.trim().isNotEmpty &&
                    package.monthPlanName != null &&
                    package.monthPlanName!.trim().isNotEmpty &&
                    package.monthPlanName!.trim().length <= 120 &&
                    package.totalSessions > 0),
      'بيانات الباقة أو الدفع غير صحيحة.',
    );
    final consumed = consumedByPackage[package.id] ?? 0;
    check(
      !voidPackages.contains(package.id) ||
          (package.remaining == package.totalSessions && consumed == 0),
      'الباقة المستردة لا يجوز أن تغطي حضورًا أو غيابًا.',
    );
    check(
      package.remaining == package.totalSessions - consumed,
      'رصيد الباقة لا يطابق سجل الحصص.',
    );
  }
  final attendanceKeys = <String>{};
  final makeupKeys = <String>{};
  for (final record in state.attendances) {
    final session = sessions[record.sessionId];
    check(
      students.containsKey(record.studentId) &&
          session != null &&
          session.status != SessionStatus.canceled,
      'الحضور مرتبط بطالب أو حصة غير صالحة.',
    );
    check(
      record.centerFeeAmount >= 0 &&
          (!record.centerFeeOnly ||
              record.centerFeeAmount > 0 &&
                  record.fixedDiscountPercent == 100 &&
                  record.packageId == null &&
                  record.originalAttendanceId == null &&
                  (record.paymentPending ||
                      record.status == AttendanceStatus.absent ||
                      state.centerFees.any(
                        (fee) =>
                            fee.studentId == record.studentId &&
                            fee.sessionId == record.sessionId,
                      ))),
      'حضور السنتر فقط يجب أن يحتفظ بإعفاء المدرس ورسوم منفصلة دون استهلاك باقة.',
    );
    check(
      record.importSource.isEmpty || state.installationSeedId != null,
      'الحضور المستورد لا يحمل مرجع التثبيت الأول.',
    );
    check(
      !record.paymentPending ||
          (record.status != AttendanceStatus.absent &&
              record.packageId == null &&
              record.originalAttendanceId == null &&
              record.importSource.isEmpty &&
              (record.centerFeeOnly ||
                  session!.kind != SessionKind.free ||
                  record.makeupSourceGroupId != null) &&
              (voidAttendances.contains(record.id) ||
                  !activeSinglePairs.contains(
                    '${record.studentId}:${record.sessionId}',
                  ))),
      'الحضور غير المسدد لا يجوز أن يدعي دفعًا أو استهلاك باقة.',
    );
    check(
      voidAttendances.contains(record.id) ||
          attendanceKeys.add('${record.studentId}:${record.sessionId}'),
      'حضور الطالب مكرر في نفس الحصة.',
    );
    if (record.packageId != null) {
      final package = packages[record.packageId];
      check(
        package?.studentId == record.studentId &&
            (record.status == AttendanceStatus.makeup &&
                    record.makeupSourceGroupId != null
                ? package?.groupId == record.makeupSourceGroupId
                : package?.groupId == session!.groupId &&
                      session.kind == SessionKind.counted &&
                      record.status != AttendanceStatus.makeup),
        'استهلاك الباقة مرتبط بحضور غير صالح.',
      );
    }
    check(
      !record.packageMember ||
          (!record.paymentPending &&
              !record.centerFeeOnly &&
              record.packageId == null &&
              record.originalAttendanceId == null),
      'حضور الباكدج لا يستهلك رصيدًا ولا ينشئ تحصيلًا.',
    );
    // Legacy Cairo lessons may retain a paid kind; exam presence has no fee.
    if (!voidAttendances.contains(record.id) &&
        record.status == AttendanceStatus.present &&
        !record.paymentPending &&
        record.importSource.isEmpty &&
        !record.centerFeeOnly &&
        !record.packageMember &&
        record.packageId == null &&
        session!.kind != SessionKind.free &&
        catalogs[groups[session.groupId]?.centerId]?.isCairo != true) {
      check(
        activeSinglePairs.contains('${record.studentId}:${record.sessionId}') ||
            canceledSinglePairs.contains(
              '${record.studentId}:${record.sessionId}',
            ),
        'حضور الحصة المدفوعة يجب أن يحتفظ بدفع فعال؛ الاسترداد لا يحوله إلى حضور مجاني.',
      );
    }
    if (record.status == AttendanceStatus.makeup &&
        record.makeupSourceGroupId != null) {
      final sourceGroup = groups[record.makeupSourceGroupId];
      final targetGroup = groups[session!.groupId]!;
      check(
        sourceGroup != null &&
            sourceGroup.id != targetGroup.id &&
            sourceGroup.subjectId == targetGroup.subjectId &&
            sourceGroup.gradeId == targetGroup.gradeId &&
            record.originalAttendanceId == null &&
            (record.paymentPending ||
                record.packageMember ||
                record.centerFeeOnly ||
                (record.packageId != null
                    ? packages[record.packageId]?.studentId ==
                              record.studentId &&
                          packages[record.packageId]?.groupId ==
                              sourceGroup.id &&
                          (!packages[record.packageId]!.purchasedAt.isAfter(
                                session.startsAt,
                              ) ||
                              payments[packages[record.packageId]!.paymentId]
                                      ?.sessionId ==
                                  session.id)
                    : state.payments.any(
                            (payment) =>
                                payment.studentId == record.studentId &&
                                payment.sessionId == record.sessionId &&
                                payment.groupId == sourceGroup.id &&
                                payment.packageId == null,
                          ) &&
                          (voidAttendances.contains(record.id) ||
                              activeSinglePairs.contains(
                                '${record.studentId}:${record.sessionId}',
                              ) ||
                              canceledSinglePairs.contains(
                                '${record.studentId}:${record.sessionId}',
                              )))) &&
            record.importSource.isEmpty,
        'التعويض لا يحمل دفع حصة أو باقة صالحة من المجموعة الأصلية.',
      );
    } else if (record.status == AttendanceStatus.makeup) {
      final original = attendance[record.originalAttendanceId];
      check(
        original != null &&
            original.studentId == record.studentId &&
            original.status == AttendanceStatus.absent &&
            original.packageId != null &&
            original.sessionId != record.sessionId &&
            (voidAttendances.contains(record.id) ||
                (!voidAttendances.contains(original.id) &&
                    makeupKeys.add(original.id))),
        'سجل التعويض غير صالح أو مكرر.',
      );
      final sourceSession = sessions[original!.sessionId]!;
      final sourceGroup = groups[sourceSession.groupId]!;
      final targetGroup = groups[session!.groupId]!;
      check(
        sourceGroup.subjectId == targetGroup.subjectId &&
            sourceGroup.gradeId == targetGroup.gradeId &&
            !session.startsAt.isBefore(sourceSession.startsAt),
        'التعويض في مادة أو صف مختلف أو قبل الحصة الأصلية.',
      );
      check(record.packageId == null, 'التعويض لا يستهلك باقة.');
    } else {
      check(
        record.originalAttendanceId == null &&
            record.makeupSourceGroupId == null,
        'حضور عادي مرتبط بتعويض.',
      );
    }
  }
  final activities = {for (final e in state.academicActivities) e.id: e};
  final activityNames = <(String, AcademicActivityKind, String)>{};
  final preparedLessonIds = state.studyMonths
      .expand((month) => month.lessons)
      .map((lesson) => lesson.id)
      .toSet();
  for (final activity in state.academicActivities) {
    check(
      activity.maxScoreKnown ||
          (state.installationSeedId != null &&
              activity.kind == AcademicActivityKind.exam),
      'الدرجة النهائية غير المعلومة تتطلب مصدر استيراد موثق.',
    );
    check(
      (activity.preparedLessonId == null
              ? sessions.containsKey(activity.sessionId) &&
                    sessions[activity.sessionId]!.status !=
                        SessionStatus.canceled
              : activity.sessionId.isEmpty &&
                    preparedLessonIds.contains(activity.preparedLessonId)) &&
          activity.name == activity.name.trim() &&
          activity.name.isNotEmpty &&
          activity.name.length <= 120 &&
          activityNames.add((
            activity.preparedLessonId == null
                ? 'session:${activity.sessionId}'
                : 'prepared:${activity.preparedLessonId}',
            activity.kind,
            activity.name.toLowerCase(),
          )) &&
          (activity.kind == AcademicActivityKind.exam
              ? activity.maxScore > 0
              : activity.maxScore == 10),
      'تعريف النشاط غير صالح أو اسمه مكرر في الحصة.',
    );
  }
  final academicKeys = <(String, String, String?)>{};
  final historicalAcademicAttendees = state.attendances
      .where(
        (entry) =>
            entry.status == AttendanceStatus.present ||
            entry.status == AttendanceStatus.makeup,
      )
      .map((entry) => (entry.studentId, entry.sessionId))
      .toSet();
  for (final academic in state.academics) {
    check(
      students.containsKey(academic.studentId) &&
          sessions.containsKey(academic.sessionId) &&
          academicKeys.add((
            academic.studentId,
            academic.sessionId,
            academic.activityId,
          )),
      'سجل الرصد غير صالح أو مكرر.',
    );
    if (academic.activityId != null) {
      final activity = activities[academic.activityId];
      check(
        activity != null &&
            activity.appliesToSession(sessions[academic.sessionId]!) &&
            academic.maxScore == activity.maxScore &&
            academic.maxScoreKnown == activity.maxScoreKnown &&
            (activity.kind == AcademicActivityKind.exam
                ? academic.homework == HomeworkStatus.notReviewed
                : academic.score == null && !academic.examAbsent),
        'سجل الرصد لا يطابق حصة النشاط ونوعه والدرجة النهائية.',
      );
      if (activity?.preparedLessonId != null) {
        check(
          (catalogs[groups[sessions[academic.sessionId]?.groupId]?.centerId]
                          ?.isCairo ==
                      true &&
                  academic.score == null) ||
              historicalAcademicAttendees.contains((
                academic.studentId,
                academic.sessionId,
              )),
          'رصد الحصة المشتركة يحتاج دليل حضور فعلي للطالب.',
        );
      }
    }
    check(
      academic.maxScore > 0 &&
          (academic.score == null ||
              (academic.score!.isFinite &&
                  academic.score! >= 0 &&
                  (!academic.maxScoreKnown ||
                      academic.score! <= academic.maxScore))) &&
          !(academic.examAbsent && academic.score != null),
      'الدرجة يجب أن تكون بين صفر والدرجة النهائية؛ الغائب لا تكون له درجة.',
    );
  }

  // Historical checks include canceled records and filter them at checkedAt.
  final reviewAttendances = _recordsBy(
    state.attendances,
    (entry) => (entry.studentId, entry.sessionId),
  );
  final reviewPayments = _recordsBy(
    state.payments,
    (payment) => payment.studentId,
  );
  final reviewSettlements = _recordsBy(
    state.debtSettlements,
    (settlement) => settlement.studentId,
  );
  final reviewFees = _recordsBy(
    state.centerFees,
    (fee) => (fee.studentId, fee.sessionId),
  );
  final financialStaffIds = state.staff
      .where((e) => e.role != StaffRole.assistant)
      .map((e) => e.id)
      .toSet();
  final paymentVoidedAt = {
    for (final correction in state.corrections)
      if (correction.voidsPayment) correction.paymentId!: correction.createdAt,
  };
  final attendanceVoidedAt = {
    for (final correction in state.corrections)
      if (correction.attendanceId != null)
        correction.attendanceId!: correction.createdAt,
  };
  final reviewedPayments = <String>{};
  final checkedPairs = <String>{};
  for (final row in state.paymentChecks) {
    final session = sessions[row.sessionId];
    final studentAttendances =
        reviewAttendances[(row.studentId, row.sessionId)] ??
        const <AttendanceRecord>[];
    check(
      students.containsKey(row.studentId) &&
          session != null &&
          financialStaffIds.contains(row.staffId) &&
          checkedPairs.add('${row.studentId}:${row.sessionId}'),
      'سجل مراجعة الكود غير صالح أو مكرر.',
    );
    if (row.amount != null) {
      final centerFeeOnly = studentAttendances.any(
        (entry) =>
            entry.studentId == row.studentId &&
            entry.sessionId == row.sessionId &&
            entry.centerFeeOnly &&
            (attendanceVoidedAt[entry.id] == null ||
                attendanceVoidedAt[entry.id]!.isAfter(row.checkedAt)) &&
            !entry.recordedAt.isAfter(row.checkedAt),
      );
      final collected = centerFeeOnly
          ? (reviewFees[(row.studentId, row.sessionId)] ??
                    const <CenterFeeRecord>[])
                .where(
                  (fee) =>
                      fee.studentId == row.studentId &&
                      fee.sessionId == row.sessionId &&
                      !fee.recordedAt.isAfter(row.checkedAt),
                )
                .fold<int>(0, (sum, fee) => sum + fee.paidAmount)
          : _sessionReviewCollections(
              row.studentId,
              row.sessionId,
              (reviewPayments[row.studentId] ?? const <PaymentRecord>[]).where(
                (p) =>
                    (paymentVoidedAt[p.id] == null ||
                        paymentVoidedAt[p.id]!.isAfter(row.checkedAt)) &&
                    !p.createdAt.isAfter(row.checkedAt),
              ),
              (reviewSettlements[row.studentId] ?? const <DebtSettlement>[])
                  .where((s) => !s.createdAt.isAfter(row.checkedAt)),
            );
      check(
        row.amount! > 0 && row.amount == collected,
        'مبلغ مراجعة الإيصال لا يطابق التحصيل التاريخي للحصة.',
      );
    }
    final payment = payments[row.paymentId];
    final package = packages[row.packageId];
    if (row.status == StudentPaymentStatus.paidSingle) {
      check(
        payment != null &&
            payment.studentId == row.studentId &&
            payment.sessionId == row.sessionId &&
            payment.packageId == null &&
            row.packageId == null,
        'مراجعة دفع الحصة لا تطابق عملية الطالب.',
      );
    } else if (row.status == StudentPaymentStatus.paidPackage) {
      check(
        package != null &&
            package.studentId == row.studentId &&
            ((package.groupId == session!.groupId &&
                    session.kind == SessionKind.counted) ||
                studentAttendances.any(
                  (record) =>
                      record.studentId == row.studentId &&
                      record.sessionId == row.sessionId &&
                      record.packageId == package.id &&
                      record.status == AttendanceStatus.makeup &&
                      record.makeupSourceGroupId == package.groupId,
                )) &&
            row.paymentId == package.paymentId,
        'مراجعة الباقة لا تطابق الطالب أو المجموعة.',
      );
    } else {
      check(
        row.paymentId == null && row.packageId == null,
        'مراجعة غير مدفوعة لا تقبل ربط مبلغ أو باقة.',
      );
      if (row.status == StudentPaymentStatus.free) {
        check(
          session!.kind == SessionKind.free ||
              studentAttendances.any(
                (entry) =>
                    entry.studentId == row.studentId &&
                    entry.sessionId == row.sessionId &&
                    entry.centerFeeOnly,
              ),
          'المراجعة المجانية لا تطابق حصة مجانية أو إعفاء سنتر محفوظ.',
        );
      }
      if (row.status == StudentPaymentStatus.makeup) {
        check(
          studentAttendances.any(
            (e) =>
                e.studentId == row.studentId &&
                e.sessionId == row.sessionId &&
                e.status == AttendanceStatus.makeup,
          ),
          'مراجعة التعويض لا تطابق حضورًا تعويضيًا.',
        );
      }
    }
  }
  for (final review in state.reviews) {
    check(
      students.containsKey(review.studentId) &&
          financialStaffIds.contains(review.staffId) &&
          review.paperAmount >= 0 &&
          review.expectedAmount >= 0,
      'هوية المراجعة الورقية أو مبلغها غير صالح.',
    );
    if (review.sessionId != null) {
      check(
        sessions.containsKey(review.sessionId) &&
            sessions[review.sessionId]!.status != SessionStatus.canceled,
        'المراجعة مرتبطة بحصة غير صالحة.',
      );
    }
    if (review.paymentId == null) {
      check(
        review.expectedAmount == 0,
        'المراجعة غير المرتبطة لا تمثل إيرادًا أو مبلغًا مسجلًا.',
      );
    } else {
      final payment = payments[review.paymentId];
      check(
        payment != null &&
            payment.studentId == review.studentId &&
            payment.sessionId == review.sessionId &&
            payment.collectedAmount == review.expectedAmount &&
            reviewedPayments.add(payment.id),
        'المراجعة مكررة أو لا تطابق هوية الدفع والمبلغ المحفوظ.',
      );
    }
  }
  final attendanceBySession = <String, List<AttendanceRecord>>{};
  final paymentsBySession = <String, List<PaymentRecord>>{};
  for (final record in activeAttendance) {
    attendanceBySession.putIfAbsent(record.sessionId, () => []).add(record);
  }
  for (final record in state.payments) {
    if (record.sessionId != null) {
      paymentsBySession
          .putIfAbsent(record.sessionId!, () => [])
          .add(
            record.copyWith(
              method: effectiveMethods[record.id] ?? record.method,
            ),
          );
    }
  }
  final closingSessions = <String>{};
  for (final closing in state.closings) {
    final session = sessions[closing.sessionId];
    check(
      session != null &&
          (session.status == SessionStatus.closed ||
              (session.status == SessionStatus.open &&
                  reopenedClosings.contains(closing.id))) &&
          closing.summary.sessionId == closing.sessionId &&
          financialStaffIds.contains(closing.staffId) &&
          closing.actualCash >= 0 &&
          (reopenedClosings.contains(closing.id) ||
              closingSessions.add(closing.sessionId)),
      'التقفيلة المالية غير صالحة أو مكررة.',
    );
    check(
      reopenedClosings.contains(closing.id) ||
          state.cardPayments
              .where((e) => e.sessionId == closing.sessionId)
              .every((e) => !e.createdAt.isAfter(closing.createdAt)),
      'دفع الكارت تم بعد التقفيلة المالية المحفوظة.',
    );
    check(
      reopenedClosings.contains(closing.id) ||
          state.debtSettlements
              .where((e) => e.sessionId == closing.sessionId)
              .every((e) => !e.createdAt.isAfter(closing.createdAt)),
      'تسديد مديونية تم بعد تقفيلة حصة التحصيل المحفوظة.',
    );
    final computed = buildSessionFinancialSummary(
      session: session!,
      coverageClassificationVersion:
          closing.summary.coverageClassificationVersion ?? 0,
      attendances: attendanceBySession[closing.sessionId] ?? [],
      payments: paymentsBySession[closing.sessionId] ?? [],
      packagePurchasePayments: state.payments,
      packages: state.packages,
      cardPayments: state.cardPayments,
      centerFees: state.centerFees.where(
        (fee) => !fee.recordedAt.isAfter(closing.createdAt),
      ),
      debtSettlements: state.debtSettlements.where(
        (e) => !e.createdAt.isAfter(closing.createdAt),
      ),
      refunds: state.refunds,
    );
    SessionFinancialSummary? historical;
    if (reopenedClosings.contains(closing.id)) {
      final priorCorrections = state.corrections
          .where((e) => !e.createdAt.isAfter(closing.createdAt))
          .toList();
      final priorVoids = priorCorrections
          .map((e) => e.attendanceId)
          .whereType<String>()
          .toSet();
      final laterReplacements = state.corrections
          .where((e) => e.createdAt.isAfter(closing.createdAt))
          .map((e) => e.replacementAttendanceId)
          .whereType<String>()
          .toSet();
      final priorMethods = <String, String>{};
      for (final e in priorCorrections.where(
        (e) => e.action == CorrectionAction.paymentMethod,
      )) {
        priorMethods[e.paymentId!] = e.newMethod!;
      }
      historical = buildSessionFinancialSummary(
        session: session,
        coverageClassificationVersion:
            closing.summary.coverageClassificationVersion ?? 0,
        packages: state.packages,
        centerFees: state.centerFees.where(
          (fee) => !fee.recordedAt.isAfter(closing.createdAt),
        ),
        cardPayments: state.cardPayments.where(
          (e) => !e.createdAt.isAfter(closing.createdAt),
        ),
        debtSettlements: state.debtSettlements.where(
          (e) => !e.createdAt.isAfter(closing.createdAt),
        ),
        attendances: state.attendances.where(
          (e) =>
              !e.recordedAt.isAfter(closing.createdAt) &&
              !laterReplacements.contains(e.id) &&
              !priorVoids.contains(e.id),
        ),
        payments: state.payments
            .where((e) => !e.createdAt.isAfter(closing.createdAt))
            .map((e) => e.copyWith(method: priorMethods[e.id] ?? e.method)),
        refunds: state.refunds.where(
          (e) => !e.createdAt.isAfter(closing.createdAt),
        ),
      );
    }
    final snapshot = closing.summary;
    final categoryKeys = <(SessionStudentCategoryKind, num?, int)>{};
    for (final category
        in snapshot.studentCategories ?? <SessionStudentCategory>[]) {
      final paid =
          category.kind == SessionStudentCategoryKind.single ||
          category.kind == SessionStudentCategoryKind.package;
      check(
        category.label.trim().isNotEmpty &&
            category.studentCount > 0 &&
            category.unitAmount >= 0 &&
            category.operationCount >= 0 &&
            categoryKeys.add((
              category.kind,
              category.discountPercent,
              category.unitAmount,
            )) &&
            (paid
                ? category.discountPercent != null &&
                      category.discountPercent!.isFinite &&
                      category.discountPercent! >= 0 &&
                      category.discountPercent! <= 100 &&
                      category.operationCount >= category.studentCount
                : (category.kind == SessionStudentCategoryKind.prepaid
                          ? category.discountPercent == null ||
                                (category.discountPercent!.isFinite &&
                                    category.discountPercent! >= 0 &&
                                    category.discountPercent! <= 100)
                          : category.discountPercent == null) &&
                      category.unitAmount == 0 &&
                      category.operationCount == 0),
        'تفاصيل فئات الطلبة في التقفيلة غير صالحة أو مكررة.',
      );
    }
    check(
      snapshot.grossAmount >= 0 &&
          snapshot.discountAmount >= 0 &&
          snapshot.discountAmount <= snapshot.grossAmount &&
          snapshot.refundAmount >= 0 &&
          snapshot.centerFeeCollected >= 0 &&
          snapshot.totalCollected ==
              snapshot.grossAmount -
                  snapshot.discountAmount -
                  (snapshot.debtAmount ?? 0) +
                  (snapshot.debtSettlementAmount ?? 0) -
                  snapshot.refundAmount &&
          (snapshot.debtAmount == null || snapshot.debtAmount! >= 0) &&
          (snapshot.debtSettlementAmount == null ||
              snapshot.debtSettlementAmount! >= 0) &&
          [
            snapshot.presentCount,
            snapshot.makeupCount,
            snapshot.absentCount,
            snapshot.prepaidCount,
            snapshot.singlePaymentCount,
            snapshot.packageSalesCount,
            snapshot.freeCount,
          ].every((e) => e >= 0) &&
          snapshot.lines.every((e) => e.count > 0) &&
          snapshot.lines.fold(0, (sum, e) => sum + e.total) ==
              snapshot.totalCollected,
      'لقطة التقفيلة التاريخية تحتوي قيمًا غير متسقة.',
    );
    final discountKeys = <num?>{};
    for (final category
        in snapshot.attendanceDiscountCategories ??
            <AttendanceDiscountCategory>[]) {
      check(
        category.studentCount > 0 &&
            category.label.trim().isNotEmpty &&
            discountKeys.add(category.discountPercent) &&
            (category.discountPercent == null ||
                (category.discountPercent!.isFinite &&
                    category.discountPercent! >= 0 &&
                    category.discountPercent! <= 100)),
        'تفاصيل الخصم الثابت للحاضرين غير صالحة أو مكررة.',
      );
    }
    check(
      snapshot.allFreeCount == null ||
          (snapshot.allFreeCount! >= 0 &&
              snapshot.allFreeCount! <=
                  snapshot.presentCount + snapshot.makeupCount),
      'إجمالي الحضور المجاني أو المعفى غير صالح.',
    );
    check(
      snapshot.packageBuyerCount == null ||
          (snapshot.packageBuyerCount! >= 0 &&
              snapshot.packageBuyerCount! <= snapshot.packageSalesCount),
      'عدد الطلاب المشترين للباقات غير صالح.',
    );
    final amountKeys = <String>{};
    for (final category
        in snapshot.paymentAmountCategories ??
            <SessionPaymentAmountCategory>[]) {
      check(
        (category.kind == SessionStudentCategoryKind.single ||
                category.kind == SessionStudentCategoryKind.package ||
                category.kind == SessionStudentCategoryKind.debtSettlement) &&
            category.unitAmount >= 0 &&
            category.studentCount > 0 &&
            category.operationCount >= category.studentCount &&
            amountKeys.add('${category.kind.name}:${category.unitAmount}'),
        'تفاصيل عدد الدافعين حسب المبلغ غير صالحة أو مكررة.',
      );
    }
    final centerFeeAmountKeys = <(bool, int)>{};
    for (final category
        in snapshot.centerFeePaymentCategories ??
            <SessionCenterFeePaymentCategory>[]) {
      check(
        category.unitAmount > 0 &&
            category.studentCount > 0 &&
            category.operationCount >= category.studentCount &&
            centerFeeAmountKeys.add((category.centerOnly, category.unitAmount)),
        'تفاصيل دافعي رسوم السنتر غير صالحة أو مكررة.',
      );
    }
    final expectedSnapshot = (historical ?? computed).toJson();
    if (snapshot.centerFeePaymentCategories == null) {
      expectedSnapshot.remove('centerFeePaymentCategories');
    }
    if (snapshot.coverageClassificationVersion == null) {
      expectedSnapshot.remove('coverageClassificationVersion');
    }
    check(
      snapshot.coverageClassificationVersion == null ||
          snapshot.coverageClassificationVersion == 1 ||
          snapshot.coverageClassificationVersion == 2,
      'إصدار تصنيف الحضور في التقفيلة غير مدعوم.',
    );
    if (snapshot.attendanceDiscountCategories == null) {
      expectedSnapshot.remove('attendanceDiscountCategories');
    }
    if (snapshot.allFreeCount == null) expectedSnapshot.remove('allFreeCount');
    if (snapshot.cardPaymentCount == null) {
      expectedSnapshot.remove('cardPaymentCount');
    }
    if (snapshot.cardCollectedAmount == null) {
      expectedSnapshot.remove('cardCollectedAmount');
    }
    if (snapshot.debtAmount == null) expectedSnapshot.remove('debtAmount');
    if (snapshot.debtSettlementAmount == null) {
      expectedSnapshot.remove('debtSettlementAmount');
    }
    check(
      (snapshot.cardPaymentCount == null || snapshot.cardPaymentCount! >= 0) &&
          (snapshot.cardCollectedAmount == null ||
              snapshot.cardCollectedAmount! >= 0),
      'إجمالي الكروت في التقفيلة غير صالح.',
    );
    if (snapshot.packageBuyerCount == null) {
      expectedSnapshot.remove('packageBuyerCount');
    }
    if (snapshot.paymentAmountCategories == null) {
      expectedSnapshot.remove('paymentAmountCategories');
    }
    if (snapshot.studentCategories == null) {
      expectedSnapshot.remove('studentCategories');
    } else {
      final oldPrepaid = snapshot.studentCategories!
          .where((e) => e.kind == SessionStudentCategoryKind.prepaid)
          .toList();
      if (oldPrepaid.length == 1 && oldPrepaid.single.discountPercent == null) {
        // Earlier category snapshots saved only aggregate prior-package attendance.
        final categories = (historical ?? computed).studentCategories!;
        final priorCount = categories
            .where((e) => e.kind == SessionStudentCategoryKind.prepaid)
            .fold(0, (sum, e) => sum + e.studentCount);
        final compatible = categories
            .where((e) => e.kind != SessionStudentCategoryKind.prepaid)
            .toList();
        if (priorCount > 0) {
          compatible.add(
            SessionStudentCategory(
              kind: SessionStudentCategoryKind.prepaid,
              label: 'حضور من باقة مدفوعة سابقًا',
              unitAmount: 0,
              studentCount: priorCount,
              operationCount: 0,
            ),
          );
        }
        compatible.sort((a, b) {
          final kind = a.kind.index.compareTo(b.kind.index);
          if (kind != 0) return kind;
          final discount = (a.discountPercent ?? 0).compareTo(
            b.discountPercent ?? 0,
          );
          return discount != 0
              ? discount
              : a.unitAmount.compareTo(b.unitAmount);
        });
        expectedSnapshot['studentCategories'] = compatible
            .map((e) => e.toJson())
            .toList();
      }
    }
    check(
      jsonEncode(expectedSnapshot) == jsonEncode(closing.summary.toJson()),
      'ملخص التقفيلة لا يطابق الحضور وأسعار المدفوعات المحفوظة.',
    );
  }
  for (final record in state.audit) {
    check(
      staffIds.contains(record.staffId),
      'سجل التدقيق مرتبط بموظف غير موجود.',
    );
  }
}
