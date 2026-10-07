part of 'center_store.dart';

enum LanStateEncoding { map, json }

final _lanActorKey = Object();
final _lanExecutionKey = Object();
final _lanTransactionKey = Object();
final _lanAuthorizationKey = Object();
const _lanCommandsSchema = '''CREATE TABLE IF NOT EXISTS lan_commands (
 device_id TEXT NOT NULL, request_id TEXT NOT NULL, user_id TEXT NOT NULL,
 operation TEXT NOT NULL, request_hash TEXT NOT NULL, result_json TEXT NOT NULL,
 PRIMARY KEY(device_id, request_id))''';

/// These methods are called by the authenticated, loopback-only host bridge.
/// A paired device is never itself an employee authorization.
extension CenterStoreLanHost on CenterStore {
  Future<StaffUser> authenticateLan(String name, String password) async {
    late StaffUser user;
    await _exclusive(() async {
      user = await _authenticate(name, password);
    }, operation: 'lan.login');
    return user;
  }

  StaffUser _lanUser(String staffId) {
    final user = _state.staff.where((e) => e.id == staffId).firstOrNull;
    if (user == null) {
      throw const CenterException('حساب الموظف غير متاح. سجّل الدخول من جديد.');
    }
    return user;
  }

  Map<String, dynamic> _lanSnapshot(
    String staffId, {
    String? knownStateVersion,
    LanStateEncoding stateEncoding = LanStateEncoding.map,
    String? patchVersion,
  }) {
    final user = _lanUser(staffId);
    return runZoned(() {
      if (!identical(_lanVersionState, _state)) {
        _lanVersionState = _state;
        _lanStateVersion = CenterStore._uuid.v4();
      }
      final unchanged = knownStateVersion == _lanStateVersion;
      final stateFields = unchanged
          ? {'stateUnchanged': true}
          : switch (stateEncoding) {
              LanStateEncoding.map => {
                'state': _state.toJson()..remove('credentials'),
              },
              LanStateEncoding.json => _stateEncoder.encodePublicChanges(
                _state,
                _lanStateVersion!,
                baseVersion: patchVersion == '1' ? knownStateVersion : null,
              ),
            };
      return {
        'stateVersion': _lanStateVersion,
        ...stateFields,
        'currentUser': user.toJson(),
        'canConfigureCards': canConfigureCards,
        'supportStatus': supportStatus,
      };
    }, zoneValues: {_lanActorKey: user});
  }

  Future<Map<String, dynamic>> snapshotLan(
    String staffId, {
    bool Function()? authorize,
    String? knownStateVersion,
    LanStateEncoding stateEncoding = LanStateEncoding.map,
    String? patchVersion,
  }) async {
    late Map<String, dynamic> result;
    await _exclusive(() async {
      if (authorize != null && !authorize()) {
        throw const LanAuthorizationException(
          'جلسة الموظف انتهت. سجّل الدخول من جديد.',
        );
      }
      result = _lanSnapshot(
        staffId,
        knownStateVersion: knownStateVersion,
        stateEncoding: stateEncoding,
        patchVersion: patchVersion,
      );
    });
    return result;
  }

  /// The receipt and the business change commit in the SAME SQLite transaction.
  /// Cancelling an unseen request writes a tombstone; a delayed request cannot
  /// arrive after reconnect and charge again.
  Future<Map<String, dynamic>> commandLan({
    required String deviceId,
    required String staffId,
    required Map<String, dynamic> request,
    bool cancelIfUnseen = false,
    bool Function()? authorize,
    LanStateEncoding stateEncoding = LanStateEncoding.map,
    String? knownStateVersion,
    String? patchVersion,
  }) async {
    late Map<String, dynamic> response;
    Object? responseFailure;
    StackTrace? responseStack;
    void captureCommittedResponse(Map<String, dynamic> receipt) {
      try {
        response = {
          ...receipt,
          ..._lanSnapshot(
            staffId,
            stateEncoding: stateEncoding,
            knownStateVersion: knownStateVersion,
            patchVersion: patchVersion,
          ),
        };
      } catch (error, stack) {
        reportProblem(error, stack, operation: 'lan.committed_snapshot');
        responseFailure = error;
        responseStack = stack;
      }
    }

    await _exclusive(() async {
      if (authorize != null && !authorize()) {
        throw const LanAuthorizationException(
          'جلسة الموظف انتهت. سجّل الدخول من جديد.',
        );
      }
      final id = request['requestId'] as String?;
      final operation = request['operation'] as String?;
      final arguments = request['arguments'] as Map<String, dynamic>?;
      final hash = cancelIfUnseen
          ? request['requestHash'] as String?
          : crypto.sha256
                .convert(
                  utf8.encode(
                    jsonEncode({
                      'operation': operation,
                      'arguments': arguments,
                    }),
                  ),
                )
                .toString();
      if (id == null ||
          !RegExp(r'^[0-9a-f-]{36}$').hasMatch(id) ||
          operation == null ||
          operation.length > 80 ||
          hash == null ||
          !RegExp(r'^[a-f0-9]{64}$').hasMatch(hash) ||
          (!cancelIfUnseen && arguments == null)) {
        throw const CenterException('طلب الربط غير صالح.');
      }
      final actor = _lanUser(staffId);
      final rows = await _database!.query(
        'lan_commands',
        where: 'device_id = ? AND request_id = ?',
        whereArgs: [deviceId, id],
      );
      if (rows.isNotEmpty) {
        final row = rows.single;
        if (row['user_id'] != staffId ||
            row['operation'] != operation ||
            row['request_hash'] != hash) {
          throw const CenterException('معرّف العملية مستخدم لطلب مختلف.');
        }
        captureCommittedResponse(
          jsonDecode(row['result_json'] as String) as Map<String, dynamic>,
        );
        return;
      }
      final previous = _state;
      late Map<String, dynamic> receipt;
      try {
        await _database.transaction((tx) async {
          Object? result;
          if (!cancelIfUnseen) {
            result = await runZoned(
              () => _dispatchLan(operation, arguments!),
              zoneValues: {
                _lanActorKey: actor,
                _lanExecutionKey: this,
                _lanTransactionKey: tx,
                _lanAuthorizationKey: authorize,
              },
            );
          }
          receipt = {
            'status': cancelIfUnseen ? 'aborted' : 'committed',
            'result': result,
          };
          await tx.insert('lan_commands', {
            'device_id': deviceId,
            'request_id': id,
            'user_id': staffId,
            'operation': operation,
            'request_hash': hash,
            'result_json': jsonEncode(receipt),
          });
        });
      } catch (_) {
        _state = previous;
        rethrow;
      }
      if (!cancelIfUnseen) _notifyLanCommit();
      captureCommittedResponse(receipt);
    }, operation: 'lan.command');
    // A committed command cannot be reported as a business rejection (HTTP 400).
    // Throw outside the queue wrapper so the bridge returns outcome-unknown 500.
    if (responseFailure != null) {
      Error.throwWithStackTrace(
        StateError('Committed LAN response could not be prepared'),
        responseStack!,
      );
    }
    return response;
  }

  Future<Object?> _dispatchLan(String operation, Map<String, dynamic> a) async {
    if (operation == 'renewPackage' ||
        operation == 'collectStudentCard' ||
        operation == 'correctEntry' ||
        operation == 'markAbsentPresent' ||
        operation == 'cancelPayment' ||
        operation == 'cancelAttendance') {
      final expected = a['expected'] as Map<String, dynamic>?;
      if (expected == null ||
          !mapEquals(expected, _lanPaymentPreview(operation, a))) {
        if (operation == 'cancelPayment' || operation == 'cancelAttendance') {
          throw const CenterException(
            'تغيّر السجل أو رصيد الشهر أو التقفيلات على الجهاز الرئيسي. راجع الحالة المحدثة وافتح الإلغاء من جديد.',
          );
        }
        throw const CenterException(
          'تغيّر السعر أو الخصم أو الرصيد على الرئيسي. حدّث بيانات الطالب وراجع المبلغ قبل الدفع.',
        );
      }
    }
    if (operation == 'collectAndAttend' && a['confirmation'] == null) {
      throw const CenterException(
        'راجع مبلغ الدخول قبل تأكيده من الجهاز المتصل.',
      );
    }
    switch (operation) {
      case 'saveCatalog':
        await saveCatalog(CatalogEntry.fromJson(a));
      case 'saveGroup':
        await saveGroup(StudyGroup.fromJson(a));
      case 'saveMonthForGroups':
        await saveMonthForGroups(
          plan: GroupMonthPlan.fromJson(
            Map<String, dynamic>.from(a['plan'] as Map),
          ),
          groupIds: List<String>.from(a['groupIds'] as List),
        );
      case 'updateMonthForGroups':
        await updateMonthForGroups(
          plan: GroupMonthPlan.fromJson(
            Map<String, dynamic>.from(a['plan'] as Map),
          ),
          expectedPlans: (a['expectedPlans'] as Map).map(
            (id, month) => MapEntry(
              id as String,
              GroupMonthPlan.fromJson(Map<String, dynamic>.from(month as Map)),
            ),
          ),
        );
      case 'setStudentPackageMember':
        await setStudentPackageMember(
          studentId: a['studentId'] as String,
          enabled: a['enabled'] as bool,
          sessionId: a['sessionId'] as String?,
        );
      case 'setStudentCenterFee':
        await setStudentCenterFee(
          studentId: a['studentId'] as String,
          enabled: a['enabled'] as bool,
        );
      case 'collectStudentCenterFee':
        await collectStudentCenterFee(
          studentId: a['studentId'] as String,
          sessionId: a['sessionId'] as String,
        );
      case 'saveStudentCenterOnly':
        await saveStudentCenterOnly(
          studentId: a['studentId'] as String,
          enabled: a['enabled'] as bool,
          amount: a['amount'] as int? ?? 1500,
        );
      case 'registerStudent':
        return (await registerStudent(Student.fromJson(a))).toJson();
      case 'saveStudent':
        await saveStudent(
          Student.fromJson(a),
          preserveDiscount: a['preserveDiscount'] == true,
        );
      case 'suspendStudent':
        await suspendStudent(
          studentId: a['studentId'] as String,
          reason: a['reason'] as String,
        );
      case 'reactivateStudent':
        await reactivateStudent(a['studentId'] as String);
      case 'transferStudent':
        await transferStudent(
          a['studentId'] as String,
          a['fromGroupId'] as String,
          a['toGroupId'] as String,
        );
      case 'saveStudyMonth':
        return (await saveStudyMonth(StudyMonth.fromJson(a))).toJson();
      case 'startPreparedLesson':
        return (await startPreparedLesson(
          groupId: a['groupId'] as String,
          preparedLessonId: a['preparedLessonId'] as String,
        )).toJson();
      case 'saveSession':
        await saveSession(LessonSession.fromJson(a));
      case 'startSession':
        await startSession(a['sessionId'] as String);
      case 'createGroupSessions':
        await createGroupSessions(
          groupIds: List<String>.from(a['groupIds'] as List),
          kind: SessionKind.values.byName(a['kind'] as String),
          extraPrice: a['extraPrice'] as int,
          monthNumber: a['monthNumber'] as int? ?? 1,
        );
      case 'saveAcademicActivity':
        return (await saveAcademicActivity(
          AcademicActivity.fromJson(a),
        )).toJson();
      case 'recordHomeworkExceptions':
        await recordHomeworkExceptions(
          sessionId: a['sessionId'] as String,
          activityId: a['activityId'] as String,
          missingStudentId: a['missingStudentId'] as String?,
        );
        return null;
      case 'saveAcademic':
        await saveAcademic(AcademicRecord.fromJson(a));
      case 'importAcademicGrades':
        await importAcademicGrades(AcademicImportCommand.fromJson(a));
      case 'saveCardSettings':
        await saveCardSettings(CenterCardSettings.fromJson(a));
      case 'collectStudentCard':
        await collectStudentCard(
          studentId: a['studentId'] as String,
          method: a['method'] as String,
          sessionId: a['sessionId'] as String?,
          paidAmount: a['paidAmount'] as int?,
          expectedNetAmount: a['expectedNetAmount'] as int?,
        );
      case 'receiveStudentCard':
        await receiveStudentCard(a['studentId'] as String);
      case 'settleDebt':
        await settleDebt(
          paymentId: a['paymentId'] as String,
          kind: DebtKind.values.byName(a['kind'] as String),
          amount: a['amount'] as int,
          method: a['method'] as String,
          sessionId: a['sessionId'] as String?,
          notes: a['notes'] as String? ?? '',
        );
      case 'saveStaff':
        await saveStaff(
          name: a['name'] as String,
          password: a['password'] as String,
          role: StaffRole.values.byName(a['role'] as String),
        );
      case 'saveStudentDiscount':
        await saveStudentDiscount(
          studentId: a['studentId'] as String,
          percent: a['percent'] as num,
          centerOnly: a['centerOnly'] as bool?,
          centerFeeAmount: a['centerFeeAmount'] as int?,
        );
      case 'saveStudentNote':
        await saveStudentNote(
          studentId: a['studentId'] as String,
          notes: a['notes'] as String,
        );
      case 'renewPackage':
        await renewPackage(
          PackageRequest(
            studentId: a['studentId'] as String,
            groupId: a['groupId'] as String,
            method: a['method'] as String,
            notes: a['notes'] as String,
            sessionId: a['sessionId'] as String?,
            sessions: a['sessions'] as int,
            monthPlanId: a['monthPlanId'] as String?,
            expectedMonthPlan: a['expectedMonthPlan'] == null
                ? null
                : GroupMonthPlan.fromJson(
                    Map<String, dynamic>.from(a['expectedMonthPlan'] as Map),
                  ),
            paidAmount: a['paidAmount'] as int?,
            expectedNetAmount: a['expectedNetAmount'] as int?,
          ),
        );
      case 'record_attendance':
        await recordAttendance(
          EntryRequest(
            studentId: a['studentId'] as String,
            sessionId: a['sessionId'] as String,
            mode: EntryMode.values.byName(a['mode'] as String),
            originalAttendanceId: a['originalAttendanceId'] as String?,
            makeupSourceGroupId: a['makeupSourceGroupId'] as String?,
            acknowledgedAttendanceIds: List<String>.from(
              a['acknowledgedAttendanceIds'] as List? ?? const [],
            ),
          ),
        );
      case 'collectAndAttend':
        await collectAndAttend(
          EntryRequest(
            studentId: a['studentId'] as String,
            sessionId: a['sessionId'] as String,
            mode: EntryMode.values.byName(a['mode'] as String),
            method: a['method'] as String,
            notes: a['notes'] as String,
            originalAttendanceId: a['originalAttendanceId'] as String?,
            makeupSourceGroupId: a['makeupSourceGroupId'] as String?,
            packageSessions: a['packageSessions'] as int,
            monthPlanId: a['monthPlanId'] as String?,
            paidAmount: a['paidAmount'] as int?,
            acknowledgedAttendanceIds: List<String>.from(
              a['acknowledgedAttendanceIds'] as List? ?? const [],
            ),
            confirmation: a['confirmation'] == null
                ? null
                : _decodeLanConfirmation(
                    a['confirmation'] as Map<String, dynamic>,
                  ),
          ),
        );
      case 'closeSession':
        await closeSession(a['sessionId'] as String);
      case 'reopenSession':
        await reopenSession(a['sessionId'] as String);
      case 'cancelSession':
        await cancelSession(a['sessionId'] as String);
      case 'reverseEntry':
        await reverseEntry(
          attendanceId: a['attendanceId'] as String,
          reason: a['reason'] as String,
          refundMethod: a['refundMethod'] as String,
        );
      case 'correctEntry':
        await correctEntry(
          attendanceId: a['attendanceId'] as String,
          reason: a['reason'] as String,
          mode: EntryMode.values.byName(a['mode'] as String),
          method: a['method'] as String,
          originalAttendanceId: a['originalAttendanceId'] as String?,
          packageSessions: a['packageSessions'] as int,
          monthPlanId: a['monthPlanId'] as String?,
          expectedMonthPlan: a['expectedMonthPlan'] == null
              ? null
              : GroupMonthPlan.fromJson(
                  Map<String, dynamic>.from(a['expectedMonthPlan'] as Map),
                ),
        );
      case 'markAbsentPresent':
        await markAbsentPresent(
          attendanceId: a['attendanceId'] as String,
          reason: a['reason'] as String,
          method: a['method'] as String,
        );
      case 'correctPaymentMethod':
        await correctPaymentMethod(
          paymentId: a['paymentId'] as String,
          method: a['method'] as String,
          reason: a['reason'] as String,
        );
      case 'cancelPayment':
        await cancelPayment(
          paymentId: a['paymentId'] as String,
          reason: a['reason'] as String,
          mode: RecordCancellationMode.values.byName(a['mode'] as String),
          refundMethod: a['refundMethod'] as String,
          reopenFinancialClosings:
              a['reopenFinancialClosings'] as bool? ?? false,
        );
      case 'cancelAttendance':
        await cancelAttendance(
          attendanceId: a['attendanceId'] as String,
          reason: a['reason'] as String,
          mode: RecordCancellationMode.values.byName(a['mode'] as String),
          refundMethod: a['refundMethod'] as String,
          reopenFinancialClosings:
              a['reopenFinancialClosings'] as bool? ?? false,
        );
      case 'refundPackage':
        await refundPackage(
          packageId: a['packageId'] as String,
          reason: a['reason'] as String,
          refundMethod: a['refundMethod'] as String,
        );
      case 'reopenFinancialClosing':
        await reopenFinancialClosing(
          closingId: a['closingId'] as String,
          reason: a['reason'] as String,
        );
      case 'checkPayment':
        await checkPayment(
          studentId: a['studentId'] as String,
          sessionId: a['sessionId'] as String,
          expectedAmount: a['expectedAmount'] as int?,
        );
      case 'uncheckPayment':
        await uncheckPayment(
          studentId: a['studentId'] as String,
          sessionId: a['sessionId'] as String,
        );
      case 'clearPaymentChecks':
        await clearPaymentChecks(sessionId: a['sessionId'] as String);
      case 'savePaymentReview':
        await savePaymentReview(
          ReviewRequest(
            id: a['id'] as String,
            studentId: a['studentId'] as String,
            paperAmount: a['paperAmount'] as int,
            sessionId: a['sessionId'] as String?,
            paymentId: a['paymentId'] as String?,
            notes: a['notes'] as String,
          ),
        );
      case 'finalizeSession':
        await finalizeSession(
          sessionId: a['sessionId'] as String,
          actualCash: a['actualCash'] as int,
          notes: a['notes'] as String,
        );
      default:
        throw const CenterException('هذه العملية غير متاحة عبر الربط المحلي.');
    }
    return null;
  }

  Map<String, dynamic> _lanPaymentPreview(
    String operation,
    Map<String, dynamic> a,
  ) {
    if (operation == 'cancelPayment' || operation == 'cancelAttendance') {
      final payment = operation == 'cancelPayment'
          ? _activePayment(a['paymentId'] as String)
          : null;
      final attendance = operation == 'cancelAttendance'
          ? _activeAttendance(a['attendanceId'] as String)
          : null;
      final studentId = payment?.studentId ?? attendance!.studentId;
      final sessionId = payment?.sessionId ?? attendance?.sessionId;
      final packageId = payment?.packageId ?? attendance?.packageId;
      final linkedAttendance = _activeAttendances
          .where(
            (e) => packageId != null
                ? e.packageId == packageId
                : e.studentId == studentId && e.sessionId == sessionId,
          )
          .toList();
      final linkedPayments = _activePayments
          .where(
            (e) => packageId != null
                ? e.packageId == packageId
                : e.studentId == studentId && e.sessionId == sessionId,
          )
          .toList();
      final sessionIds = {
        ?sessionId,
        ...linkedAttendance.map((e) => e.sessionId),
        ...linkedPayments.map((e) => e.sessionId).whereType<String>(),
        ..._state.debtSettlements
            .where(
              (settlement) =>
                  settlement.kind == DebtKind.lesson &&
                  linkedPayments.any((p) => p.id == settlement.paymentId),
            )
            .map((settlement) => settlement.sessionId)
            .whereType<String>(),
      };
      // Bind the reviewed effect without sending history in each LAN command.
      final reviewed = jsonEncode({
        'record': payment?.id ?? attendance!.id,
        'mode': a['mode'] ?? RecordCancellationMode.recordOnly.name,
        'reopenFinancialClosings': a['reopenFinancialClosings'] ?? false,
        'payments': linkedPayments.map((e) => e.toJson()).toList(),
        'settlements': _state.debtSettlements
            .where(
              (e) =>
                  e.kind == DebtKind.lesson &&
                  linkedPayments.any((p) => p.id == e.paymentId),
            )
            .map((e) => e.toJson())
            .toList(),
        'attendance': linkedAttendance.map((e) => e.toJson()).toList(),
        'packages': _activePackages
            .where((e) => e.id == packageId)
            .map((e) => e.toJson())
            .toList(),
        'sessions': _state.sessions
            .where((e) => sessionIds.contains(e.id))
            .map(
              (e) => {
                'id': e.id,
                'groupId': e.groupId,
                'status': e.status.name,
                'kind': e.kind.name,
                'number': e.number,
                'monthNumber': e.monthNumber,
                'startsAt': e.startsAt.toIso8601String(),
              },
            )
            .toList(),
        'closings': closings
            .where((e) => sessionIds.contains(e.sessionId))
            .map((e) => {'id': e.id, 'sessionId': e.sessionId})
            .toList(),
      });
      return {
        'fingerprint': crypto.sha256.convert(utf8.encode(reviewed)).toString(),
      };
    }
    if (operation == 'correctEntry' || operation == 'markAbsentPresent') {
      final attendance = _activeAttendance(a['attendanceId'] as String);
      final session = _session(attendance.sessionId);
      final group = _group(attendance.makeupSourceGroupId ?? session.groupId);
      return {
        'discountPercent': _student(attendance.studentId).discountPercent,
        'sessionPrice': group.sessionPrice,
        'packagePrice': group.packagePrice,
        'twoSessionPrice': group.twoSessionPrice,
        'threeSessionPrice': group.threeSessionPrice,
        'monthPlansFingerprint': crypto.sha256
            .convert(
              utf8.encode(
                jsonEncode(
                  group.effectiveMonthPlans.map((e) => e.toJson()).toList(),
                ),
              ),
            )
            .toString(),
        'retainedSessionPayment': hasRetainedSessionPayment(
          attendance.studentId,
          session.id,
        ),
        'sessionKind': session.kind.name,
        'extraPrice': session.extraPrice,
        'eligibleRemaining': attendance.makeupSourceGroupId == null
            ? eligibleRemainingFor(attendance.studentId, session.id)
            : _available(
                attendance.studentId,
                group.id,
                forClosure: session,
              ).fold(0, (total, package) => total + package.remaining),
      };
    }
    final student = _student(a['studentId'] as String);
    if (operation == 'collectStudentCard') {
      return {
        'baseAmount': cardSettings.price,
        'discountPercent': student.discountPercent,
      };
    }
    final group = _group(a['groupId'] as String);
    return {
      'baseAmount': _monthPurchaseFor(
        group,
        a['sessions'] as int,
        a['monthPlanId'] as String?,
      ).baseAmount,
      if (a['monthPlanId'] != null)
        'monthPlanFingerprint': crypto.sha256
            .convert(
              utf8.encode(
                jsonEncode(
                  _monthPlanFor(group, a['monthPlanId'] as String).toJson(),
                ),
              ),
            )
            .toString(),
      'discountPercent': student.discountPercent,
      'remaining': remainingFor(student.id, group.id),
    };
  }
}

Map<String, dynamic> _encodeLanConfirmation(EntryConfirmation c) => {
  'staffId': c.staffId,
  'groupId': c.groupId,
  'sessionKind': c.sessionKind.name,
  'baseAmount': c.baseAmount,
  'discountPercent': c.discountPercent,
  'netAmount': c.netAmount,
  'paidAmount': c.paidAmount,
  'monthPlanId': c.monthPlanId,
  'monthPlanName': c.monthPlanName,
  'monthPlanSessions': c.monthPlanSessions,
  'centerFeeOnly': c.centerFeeOnly,
  'centerFeeAmount': c.centerFeeAmount,
  'eligibleRemaining': c.eligibleRemaining,
};
EntryConfirmation _decodeLanConfirmation(Map<String, dynamic> a) =>
    EntryConfirmation(
      staffId: a['staffId'] as String,
      groupId: a['groupId'] as String,
      sessionKind: SessionKind.values.byName(a['sessionKind'] as String),
      baseAmount: a['baseAmount'] as int,
      discountPercent: a['discountPercent'] as num,
      netAmount: a['netAmount'] as int,
      paidAmount: a['paidAmount'] as int?,
      monthPlanId: a['monthPlanId'] as String?,
      monthPlanName: a['monthPlanName'] as String?,
      monthPlanSessions: a['monthPlanSessions'] as int?,
      centerFeeOnly: a['centerFeeOnly'] as bool? ?? false,
      centerFeeAmount: a['centerFeeAmount'] as int? ?? 0,
      eligibleRemaining: a['eligibleRemaining'] as int,
    );
