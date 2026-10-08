part of 'center_store.dart';

/// The client holds a read snapshot in memory. It never opens or merges SQLite.
class _RemoteCenterStore extends CenterStore {
  _RemoteCenterStore(this.transport, String directory)
    : _pendingFile = File(p.join(directory, 'lan-pending-command.json')),
      super._network(p.join(directory, 'center.sqlite'));
  final LanTransport transport;
  final File _pendingFile;
  String? _staffSession;
  bool _connected = false;
  bool _supportsLiveRefresh = false;
  @override
  bool get supportsLiveRefresh => _supportsLiveRefresh;
  bool _canConfigureCards = false;
  String? _snapshotFingerprint;
  String? _snapshotVersion;
  Future<void>? _backgroundRefresh;
  int _refreshGeneration = 0, _foregroundRequests = 0;
  Map<String, dynamic> _remoteSupportStatus = const {'configured': false};
  @override
  Map<String, dynamic> get supportStatus => _remoteSupportStatus;
  @override
  Future<void> requestSupportUpload() async {
    final session = _staffSession;
    if (session == null || !_connected || currentUser == null) {
      throw const CenterException(
        'اتصل بالرئيسي وسجّل الدخول لرفع نسخة البيانات.',
      );
    }
    try {
      final response = await transport.post(
        '/api/support-upload',
        {},
        staffSession: session,
      );
      if (_staffSession != session) {
        throw const CenterException(
          'تغيّرت جلسة الموظف؛ راجع حالة الرفع على الرئيسي.',
        );
      }
      if (response['queued'] != true) {
        throw const CenterException('لم يؤكد الرئيسي تجهيز النسخة.');
      }
      _remoteSupportStatus = Map<String, dynamic>.from(
        response['supportStatus'] as Map? ?? {},
      );
      if (!_closed) notifyListeners();
    } on LanAuthorizationException {
      if (_staffSession == session) {
        signOut();
        _connected = false;
        if (!_closed) notifyListeners();
      }
      rethrow;
    } on LanConnectionException {
      if (_staffSession == session) {
        _connected = false;
        if (!_closed) notifyListeners();
      }
      rethrow;
    }
  }

  @override
  bool get isRemote => true;
  @override
  bool get remoteConnected => _connected;
  @override
  bool get hasStaff => true;
  @override
  bool get canConfigureCards => canManage && _canConfigureCards;

  void _apply(Map<String, dynamic> response) {
    final trace = PerformanceTrace('lan.refresh', budgetMs: 32);
    final version = response['stateVersion'] as String?;
    final unchanged = response['stateUnchanged'] == true;
    final delta = response['stateDelta'];
    final forms = [
      response.containsKey('state'),
      response.containsKey('stateDelta'),
      response.containsKey('stateUnchanged'),
    ].where((present) => present).length;
    if (forms != 1 ||
        response.containsKey('state') && response['state'] is! Map ||
        response.containsKey('stateDelta') && delta is! Map ||
        response.containsKey('stateUnchanged') && !unchanged ||
        version != null && (version.isEmpty || version.length > 128) ||
        unchanged && (version == null || version != _snapshotVersion) ||
        delta != null &&
            (version == null ||
                _snapshotVersion == null ||
                version == _snapshotVersion)) {
      throw const FormatException('Invalid unchanged state response');
    }
    final fingerprint = version == null ? jsonEncode(response['state']) : null;
    final stateChanged = version == null
        ? _snapshotVersion != null || _snapshotFingerprint != fingerprint
        : _snapshotVersion != version;
    final nextSupport = Map<String, dynamic>.from(
      response['supportStatus'] as Map? ?? {},
    );
    final supportChanged = !_sameSupportStatus(
      _remoteSupportStatus,
      nextSupport,
    );
    final nextUser = StaffUser.fromJson(
      response['currentUser'] as Map<String, dynamic>,
    );
    final changed =
        !_connected ||
        supportChanged ||
        stateChanged ||
        _currentUser?.id != nextUser.id ||
        _currentUser?.role != nextUser.role ||
        _canConfigureCards != (response['canConfigureCards'] == true);
    final nextState = !stateChanged
        ? _state
        : delta != null
        ? _state.applyPublicDelta(
            Map<String, dynamic>.from(delta as Map),
            baseVersion: _snapshotVersion!,
          )
        : CenterState.fromJson({
            ...response['state'] as Map<String, dynamic>,
            'credentials': <String, dynamic>{},
          });
    _state = nextState;
    if (response.containsKey('stateWatch')) {
      _supportsLiveRefresh = response['stateWatch'] == true;
    }
    _remoteSupportStatus = nextSupport;
    _currentUser = nextUser;
    _canConfigureCards = response['canConfigureCards'] == true;
    _connected = true;
    _snapshotFingerprint = fingerprint;
    _snapshotVersion = version;
    trace.stage('apply');
    trace.counts[delta != null ? 'delta' : 'full'] = stateChanged ? 1 : 0;
    trace.finish();
    if (changed && !_closed) notifyListeners();
  }

  Future<void> _applyResponse(
    Map<String, dynamic> response,
    String session, {
    required bool command,
  }) async {
    if (_staffSession != session) return;
    if (_tryApply(response)) return;
    try {
      final full = await transport.get('/api/state', staffSession: session);
      if (_staffSession != session) {
        if (!command) return;
        throw const LanConnectionException(
          'تغيّرت جلسة الموظف قبل مراجعة نتيجة العملية. سجّل الدخول بالحساب الأصلي لمراجعتها.',
          outcomeUnknown: true,
        );
      }
      if (!full.containsKey('state')) {
        throw const FormatException('Expected full state recovery');
      }
      _apply(full);
    } catch (error, stack) {
      if (_staffSession == session) {
        _connected = false;
        if (error is LanAuthorizationException) {
          signOut();
        } else if (!_closed) {
          notifyListeners();
        }
      }
      if (error is LanAuthorizationException && !command) rethrow;
      throw LanConnectionException(
        command
            ? 'لم يكتمل تحديث بيانات العملية المحفوظة. أعد الاتصال لمراجعة نتيجتها؛ لا تسجلها مرة ثانية.'
            : 'تعذر تحديث بيانات الجهاز من الرئيسي. أعد الاتصال للمحاولة مجددًا.',
        outcomeUnknown: command,
        cause: error,
        stackTrace: stack,
      );
    }
  }

  bool _tryApply(Map<String, dynamic> response) {
    try {
      _apply(response);
      return true;
    } on FormatException {
      // Recover malformed snapshots through an authoritative full read.
    } on TypeError {
      // A malformed row must not partially mutate the current snapshot.
    } on CenterException {
      // Unsupported schemas need full recovery, including after a commit.
    } on ArgumentError {
      // Invalid row enum values also require full recovery.
    }
    return false;
  }

  Future<void> _foreground(
    Future<void> Function() work, {
    required String operation,
  }) {
    _refreshGeneration++;
    transport.cancelStateWait();
    _foregroundRequests++;
    return _exclusive(
      work,
      operation: operation,
    ).whenComplete(() => _foregroundRequests--);
  }

  void _requireCommandReceipt(Map<String, dynamic> response) {
    if (response['status'] != 'committed' && response['status'] != 'aborted') {
      throw const LanConnectionException(
        'لم يؤكد الرئيسي نتيجة العملية. أعد الاتصال لمراجعتها قبل التسجيل مجددًا.',
        outcomeUnknown: true,
      );
    }
  }

  // HTTP decoding gives an unchanged receipt a new map identity on every poll.
  bool _sameSupportStatus(
    Map<String, dynamic> previous,
    Map<String, dynamic> next,
  ) =>
      mapEquals({...previous, 'receipt': null}, {...next, 'receipt': null}) &&
      mapEquals(previous['receipt'] as Map?, next['receipt'] as Map?);

  Future<void> _reconcile() async {
    if (!await _pendingFile.exists()) return;
    if (await _pendingFile.length() > 4096) {
      throw const CenterException(
        'سجل العملية المعلقة غير صالح. راجعه على الجهاز الرئيسي.',
      );
    }
    final pending =
        jsonDecode(await _pendingFile.readAsString()) as Map<String, dynamic>;
    if (pending['hostId'] != transport.endpoint.hostId ||
        pending['certificateSha256'] != transport.endpoint.certificateSha256) {
      throw const CenterException(
        'توجد عملية غير مؤكدة على جهاز رئيسي آخر. ارجع للربط الأصلي لمراجعتها.',
      );
    }
    if (pending['staffId'] != _currentUser?.id) {
      throw const CenterException(
        'توجد عملية غير مؤكدة لموظف آخر. سجّل الدخول بحسابه لمراجعتها.',
      );
    }
    final session = _staffSession;
    final response = await transport.post(
      '/api/cancel-command',
      pending,
      staffSession: session,
      stateVersion: _snapshotVersion,
      statePatches: true,
    );
    _requireCommandReceipt(response);
    if (_staffSession == session) {
      await _applyResponse(response, session!, command: true);
    }
    await _pendingFile.delete();
  }

  @override
  Future<void> prepareLanSwitch() => _foreground(() async {
    if (!await _pendingFile.exists()) return;
    if (_staffSession == null || !_connected) {
      throw const CenterException(
        'راجع العملية غير المؤكدة: اتصل بالرئيسي وسجّل دخول الموظف قبل تغيير الربط.',
      );
    }
    await _reconcile();
  }, operation: 'lan.switch');

  @override
  Future<void> signIn(String name, String password) {
    final revision = _logoutRevision;
    return _foreground(() async {
      final response = await transport.post('/api/login', {
        'name': name,
        'password': password,
      });
      if (_logoutRevision != revision) return;
      final session = response['staffSession'] as String;
      _staffSession = session;
      await _applyResponse(response, session, command: false);
      if (_staffSession != session) return;
      await _reconcile();
    }, operation: 'lan.sign_in');
  }

  @override
  void signOut() {
    _refreshGeneration++;
    final session = _staffSession;
    _staffSession = null;
    _canConfigureCards = false;
    _snapshotFingerprint = null;
    _snapshotVersion = null;
    _remoteSupportStatus = const {'configured': false};
    _state = CenterState();
    super.signOut();
    if (session != null) {
      unawaited(
        transport
            .post('/api/logout', {}, staffSession: session)
            .then<void>(
              (_) {},
              onError: (Object error, StackTrace stack) {
                reportProblem(error, stack, operation: 'lan.logout');
              },
            ),
      );
    }
  }

  @override
  Future<void> refreshRemote({bool waitForChanges = false}) {
    return _backgroundRefresh ??= _refreshInBackground(
      waitForChanges: waitForChanges,
    ).whenComplete(() => _backgroundRefresh = null);
  }

  Future<void> _refreshInBackground({required bool waitForChanges}) async {
    if (_closed || _foregroundRequests != 0) return;
    final generation = _refreshGeneration;
    final session = _staffSession;
    final version = _snapshotVersion;
    bool currentInteraction() =>
        !_closed &&
        _foregroundRequests == 0 &&
        generation == _refreshGeneration &&
        session == _staffSession;
    bool currentSnapshot() =>
        currentInteraction() && version == _snapshotVersion;

    try {
      // Waiting for a background GET must not hold the command queue. Apply
      // only against its captured session/version, inside that same queue.
      if (session == null) {
        final health = await transport.health();
        if (health['hostId'] != transport.endpoint.hostId ||
            health['protocol'] != 1) {
          throw const CenterException(
            'الجهاز المتصل لا يطابق جهاز السنتر المحفوظ.',
          );
        }
        await _exclusive(() async {
          if (currentSnapshot() && !_connected) {
            _connected = true;
            notifyListeners();
          }
        }, operation: 'lan.refresh');
      } else {
        final response = await transport.get(
          '/api/state',
          staffSession: session,
          stateVersion: version,
          statePatches: true,
          waitForChanges: waitForChanges,
        );
        var needsFullState = false;
        await _exclusive(() async {
          if (!currentSnapshot()) return;
          if (!_tryApply(response)) {
            needsFullState = true;
            return;
          }
          await _reconcile();
        }, operation: 'lan.refresh');
        if (!needsFullState || !currentSnapshot()) return;
        final full = await transport.get('/api/state', staffSession: session);
        await _exclusive(() async {
          if (!currentSnapshot()) return;
          if (!full.containsKey('state')) {
            throw const FormatException('Expected full state recovery');
          }
          _apply(full);
          await _reconcile();
        }, operation: 'lan.refresh');
      }
    } catch (error, stack) {
      // An obsolete read's failure cannot disconnect a newer command or login.
      if (!currentInteraction()) return;
      var relevant = false;
      await _exclusive(() async {
        if (!currentInteraction()) return;
        relevant = true;
        if (error is LanAuthorizationException) signOut();
        if (_connected) {
          _connected = false;
          notifyListeners();
        }
      }, operation: 'lan.refresh');
      if (!relevant) return;
      if (error is CenterException) rethrow;
      throw LanConnectionException(
        'تعذر تحديث بيانات الجهاز من الرئيسي. أعد الاتصال للمحاولة مجددًا.',
        outcomeUnknown: false,
        cause: error,
        stackTrace: stack,
      );
    }
  }

  Future<Object?> _command(
    String operation,
    Map<String, dynamic> arguments,
  ) async {
    Object? result;
    await _foreground(() async {
      if (_staffSession == null || !_connected || currentUser == null) {
        throw const CenterException(
          'الاتصال بالرئيسي غير متاح أو يلزم تسجيل الدخول؛ لم يُرسل الطلب.',
        );
      }
      await _reconcile();
      final session = _staffSession;
      if (session == null || currentUser == null) {
        throw const CenterException('سجّل الدخول من جديد قبل تنفيذ العملية.');
      }
      final request = {
        'requestId': CenterStore._uuid.v4(),
        'operation': operation,
        'arguments': arguments,
      };
      final pending = {
        'hostId': transport.endpoint.hostId,
        'certificateSha256': transport.endpoint.certificateSha256,
        'requestId': request['requestId'],
        'operation': operation,
        'requestHash': crypto.sha256
            .convert(
              utf8.encode(
                jsonEncode({'operation': operation, 'arguments': arguments}),
              ),
            )
            .toString(),
        'staffId': currentUser!.id,
      };
      // No names, payments, passwords or body are written in this journal.
      final staged = File('${_pendingFile.path}.tmp');
      await staged.writeAsString(jsonEncode(pending), flush: true);
      await staged.rename(_pendingFile.path);
      try {
        final response = await transport.post(
          '/api/command',
          request,
          staffSession: session,
          stateVersion: _snapshotVersion,
          statePatches: true,
        );
        _requireCommandReceipt(response);
        await _applyResponse(response, session, command: true);
        await _pendingFile.delete();
        if (response['status'] != 'committed') {
          throw const CenterException(
            'تم إلغاء الطلب قبل الحفظ. يمكنك التسجيل من جديد.',
          );
        }
        result = response['result'];
      } on LanConnectionException {
        _connected = false;
        notifyListeners();
        rethrow; // Keep pending receipt until the host settles its outcome.
      } on LanAuthorizationException {
        // Authorization is rejected before dispatch, so this request cannot commit.
        if (await _pendingFile.exists()) await _pendingFile.delete();
        signOut();
        rethrow;
      } on CenterException {
        try {
          if (await _pendingFile.exists()) await _pendingFile.delete();
        } catch (error, stack) {
          reportProblem(error, stack, operation: 'lan.$operation');
        }
        await _refreshRejectedCommand(session);
        rethrow; // Preserve the business rejection; never resend its mutation.
      }
    }, operation: 'lan.$operation');
    return result;
  }

  Future<void> _refreshRejectedCommand(String session) async {
    if (_staffSession != session) return;
    try {
      final response = await transport.get('/api/state', staffSession: session);
      await _applyResponse(response, session, command: false);
    } catch (error, stack) {
      if (_staffSession == session) {
        if (error is LanAuthorizationException) signOut();
        _connected = false;
        notifyListeners();
      }
      reportProblem(error, stack, operation: 'lan.refresh');
    }
  }

  Future<void> _send(String op, Map<String, dynamic> args) async {
    await _command(op, args);
  }

  @override
  Future<void> setupAdmin(String name, String password) async =>
      throw const CenterException('إعداد المدير يتم على الجهاز الرئيسي.');
  @override
  Future<String> createBackup({String? destination}) async =>
      throw const CenterException(
        'احفظ النسخة الاحتياطية من الجهاز الرئيسي الذي يحتوي البيانات.',
      );
  @override
  Future<void> restoreBackup(String path) async => throw const CenterException(
    'استعادة البيانات تتم على الجهاز الرئيسي فقط.',
  );
  @override
  Future<void> close() async {
    if (_closed) return;
    _refreshGeneration++;
    await _exclusive(() async {
      transport.close();
      _staffSession = null;
      _closed = true;
    });
  }

  @override
  Future<void> saveCatalog(CatalogEntry value) =>
      _send('saveCatalog', value.toJson());
  @override
  Future<void> saveGroup(StudyGroup value) =>
      _send('saveGroup', value.toJson());
  @override
  Future<void> saveMonthForGroups({
    required GroupMonthPlan plan,
    required List<String> groupIds,
  }) => _send('saveMonthForGroups', {
    'plan': plan.toJson(),
    'groupIds': List<String>.unmodifiable(groupIds),
  });
  @override
  Future<void> updateMonthForGroups({
    required GroupMonthPlan plan,
    required Map<String, GroupMonthPlan> expectedPlans,
  }) => _send('updateMonthForGroups', {
    'plan': plan.toJson(),
    'expectedPlans': {
      for (final entry in expectedPlans.entries)
        entry.key: entry.value.toJson(),
    },
  });
  @override
  Future<void> setStudentPackageMember({
    required String studentId,
    required bool enabled,
    String? sessionId,
  }) => _send('setStudentPackageMember', {
    'studentId': studentId,
    'enabled': enabled,
    'sessionId': sessionId,
  });

  @override
  Future<void> setStudentCenterFee({
    required String studentId,
    required bool enabled,
  }) => _send('setStudentCenterFee', {
    'studentId': studentId,
    'enabled': enabled,
  });
  @override
  Future<void> collectStudentCenterFee({
    required String studentId,
    required String sessionId,
  }) => _send('collectStudentCenterFee', {
    'studentId': studentId,
    'sessionId': sessionId,
  });
  @override
  Future<void> saveStudentCenterOnly({
    required String studentId,
    required bool enabled,
    int amount = 1500,
  }) => _send('saveStudentCenterOnly', {
    'studentId': studentId,
    'enabled': enabled,
    'amount': amount,
  });
  @override
  Future<Student> registerStudent(Student value) async => Student.fromJson(
    (await _command('registerStudent', value.toJson())) as Map<String, dynamic>,
  );
  @override
  Future<void> saveStudent(
    Student value, {
    bool preserveDiscount = false,
    bool preserveCenterOnly = true,
  }) => _send('saveStudent', {
    ...value.toJson(),
    'preserveDiscount': preserveDiscount,
    'preserveCenterOnly': preserveCenterOnly,
  });
  @override
  Future<void> suspendStudent({
    required String studentId,
    required String reason,
  }) => _send('suspendStudent', {'studentId': studentId, 'reason': reason});
  @override
  Future<void> reactivateStudent(String studentId) =>
      _send('reactivateStudent', {'studentId': studentId});
  @override
  Future<void> transferStudent(
    String studentId,
    String fromGroupId,
    String toGroupId,
  ) => _send('transferStudent', {
    'studentId': studentId,
    'fromGroupId': fromGroupId,
    'toGroupId': toGroupId,
  });
  @override
  Future<StudyMonth> saveStudyMonth(StudyMonth value) async =>
      StudyMonth.fromJson(
        Map<String, dynamic>.from(
          (await _command('saveStudyMonth', value.toJson())) as Map,
        ),
      );
  @override
  Future<LessonSession> startPreparedLesson({
    required String groupId,
    required String preparedLessonId,
  }) async => LessonSession.fromJson(
    Map<String, dynamic>.from(
      (await _command('startPreparedLesson', {
            'groupId': groupId,
            'preparedLessonId': preparedLessonId,
          }))
          as Map,
    ),
  );
  @override
  Future<void> saveSession(LessonSession value) =>
      _send('saveSession', value.toJson());
  @override
  Future<void> startSession(String sessionId) =>
      _send('startSession', {'sessionId': sessionId});
  @override
  Future<void> createGroupSessions({
    required List<String> groupIds,
    required SessionKind kind,
    int extraPrice = 0,
    int monthNumber = 1,
  }) => _send('createGroupSessions', {
    'groupIds': List<String>.of(groupIds),
    'kind': kind.name,
    'extraPrice': extraPrice,
    'monthNumber': monthNumber,
  });

  @override
  Future<AcademicActivity> saveAcademicActivity(AcademicActivity value) async =>
      AcademicActivity.fromJson(
        (await _command('saveAcademicActivity', value.toJson()))
            as Map<String, dynamic>,
      );
  @override
  Future<void> recordHomeworkExceptions({
    required String sessionId,
    required String activityId,
    String? missingStudentId,
  }) => _send('recordHomeworkExceptions', {
    'sessionId': sessionId,
    'activityId': activityId,
    'missingStudentId': ?missingStudentId,
  });

  @override
  Future<void> saveAcademic(AcademicRecord value) =>
      _send('saveAcademic', value.toJson());
  @override
  Future<void> importAcademicGrades(AcademicImportCommand command) =>
      _send('importAcademicGrades', command.toJson());
  @override
  Future<void> saveCardSettings(CenterCardSettings value) =>
      _send('saveCardSettings', value.toJson());
  @override
  Future<void> collectStudentCard({
    required String studentId,
    String method = 'نقدي',
    String? sessionId,
    int? paidAmount,
    int? expectedNetAmount,
  }) => _send('collectStudentCard', {
    'expected': _lanPaymentPreview('collectStudentCard', {
      'studentId': studentId,
    }),
    'studentId': studentId,
    'method': method,
    'sessionId': sessionId,
    'paidAmount': paidAmount,
    'expectedNetAmount': expectedNetAmount,
  });
  @override
  Future<void> receiveStudentCard(String studentId) =>
      _send('receiveStudentCard', {'studentId': studentId});
  @override
  Future<void> settleDebt({
    required String paymentId,
    required DebtKind kind,
    required int amount,
    String method = 'نقدي',
    String? sessionId,
    String notes = '',
  }) => _send('settleDebt', {
    'paymentId': paymentId,
    'kind': kind.name,
    'amount': amount,
    'method': method,
    'sessionId': sessionId,
    'notes': notes,
  });
  @override
  Future<void> saveStaff({
    required String name,
    required String password,
    required StaffRole role,
  }) => _send('saveStaff', {
    'name': name,
    'password': password,
    'role': role.name,
  });
  @override
  Future<void> saveStudentDiscount({
    required String studentId,
    required num percent,
    bool? centerOnly,
    int? centerFeeAmount,
  }) => _send('saveStudentDiscount', {
    'studentId': studentId,
    'percent': percent,
    'centerOnly': centerOnly,
    'centerFeeAmount': centerFeeAmount,
  });
  @override
  Future<void> saveStudentNote({
    required String studentId,
    required String notes,
  }) => _send('saveStudentNote', {'studentId': studentId, 'notes': notes});
  @override
  Future<void> renewPackage(PackageRequest r) => _send('renewPackage', {
    'expected': _lanPaymentPreview('renewPackage', {
      'studentId': r.studentId,
      'groupId': r.groupId,
      'sessions': r.sessions,
      'monthPlanId': r.monthPlanId,
    }),
    'studentId': r.studentId,
    'groupId': r.groupId,
    'method': r.method,
    'notes': r.notes,
    'sessionId': r.sessionId,
    'sessions': r.sessions,
    'monthPlanId': r.monthPlanId,
    'expectedMonthPlan': r.expectedMonthPlan?.toJson(),
    'paidAmount': r.paidAmount,
    'expectedNetAmount': r.expectedNetAmount,
  });
  @override
  Future<void> recordAttendance(EntryRequest r) => _send('record_attendance', {
    'studentId': r.studentId,
    'sessionId': r.sessionId,
    'mode': r.mode.name,
    'originalAttendanceId': r.originalAttendanceId,
    'makeupSourceGroupId': r.makeupSourceGroupId,
    'acknowledgedAttendanceIds': List<String>.unmodifiable(
      r.acknowledgedAttendanceIds,
    ),
  });
  @override
  Future<void> collectAndAttend(EntryRequest r) => _send('collectAndAttend', {
    'studentId': r.studentId,
    'sessionId': r.sessionId,
    'mode': r.mode.name,
    'method': r.method,
    'notes': r.notes,
    'originalAttendanceId': r.originalAttendanceId,
    'makeupSourceGroupId': r.makeupSourceGroupId,
    'packageSessions': r.packageSessions,
    'monthPlanId': r.monthPlanId,
    'paidAmount': r.paidAmount,
    'acknowledgedAttendanceIds': List<String>.unmodifiable(
      r.acknowledgedAttendanceIds,
    ),
    'confirmation': _encodeLanConfirmation(
      r.confirmation ?? entryConfirmationFor(r),
    ),
  });
  @override
  Future<void> closeSession(String sessionId) =>
      _send('closeSession', {'sessionId': sessionId});
  @override
  Future<void> reopenSession(String sessionId) =>
      _send('reopenSession', {'sessionId': sessionId});
  @override
  Future<void> cancelSession(String sessionId) =>
      _send('cancelSession', {'sessionId': sessionId});
  @override
  Future<void> reverseEntry({
    required String attendanceId,
    required String reason,
    String refundMethod = 'نقدي',
  }) => _send('reverseEntry', {
    'attendanceId': attendanceId,
    'reason': reason,
    'refundMethod': refundMethod,
  });
  @override
  Future<void> cancelPayment({
    required String paymentId,
    required String reason,
    RecordCancellationMode mode = RecordCancellationMode.recordOnly,
    String refundMethod = 'نقدي',
    bool reopenFinancialClosings = false,
  }) => _send('cancelPayment', {
    'paymentId': paymentId,
    'reason': reason,
    'mode': mode.name,
    'refundMethod': refundMethod,
    'reopenFinancialClosings': reopenFinancialClosings,
    'expected': _lanPaymentPreview('cancelPayment', {
      'paymentId': paymentId,
      'mode': mode.name,
      'reopenFinancialClosings': reopenFinancialClosings,
    }),
  });
  @override
  Future<void> cancelAttendance({
    required String attendanceId,
    required String reason,
    RecordCancellationMode mode = RecordCancellationMode.recordOnly,
    String refundMethod = 'نقدي',
    bool reopenFinancialClosings = false,
  }) => _send('cancelAttendance', {
    'attendanceId': attendanceId,
    'reason': reason,
    'mode': mode.name,
    'refundMethod': refundMethod,
    'reopenFinancialClosings': reopenFinancialClosings,
    'expected': _lanPaymentPreview('cancelAttendance', {
      'attendanceId': attendanceId,
      'mode': mode.name,
      'reopenFinancialClosings': reopenFinancialClosings,
    }),
  });
  @override
  Future<void> correctEntry({
    required String attendanceId,
    required EntryMode mode,
    required String reason,
    String method = 'نقدي',
    String? originalAttendanceId,
    int packageSessions = 4,
    String? monthPlanId,
    GroupMonthPlan? expectedMonthPlan,
  }) => _send('correctEntry', {
    'expected': _lanPaymentPreview('correctEntry', {
      'attendanceId': attendanceId,
    }),
    'attendanceId': attendanceId,
    'mode': mode.name,
    'reason': reason,
    'method': method,
    'originalAttendanceId': originalAttendanceId,
    'packageSessions': packageSessions,
    'monthPlanId': monthPlanId,
    'expectedMonthPlan': expectedMonthPlan?.toJson(),
  });
  @override
  Future<void> markAbsentPresent({
    required String attendanceId,
    required String reason,
    String method = 'نقدي',
  }) => _send('markAbsentPresent', {
    'expected': _lanPaymentPreview('markAbsentPresent', {
      'attendanceId': attendanceId,
    }),
    'attendanceId': attendanceId,
    'reason': reason,
    'method': method,
  });
  @override
  Future<void> correctPaymentMethod({
    required String paymentId,
    required String method,
    required String reason,
  }) => _send('correctPaymentMethod', {
    'paymentId': paymentId,
    'method': method,
    'reason': reason,
  });
  @override
  Future<void> refundPackage({
    required String packageId,
    required String reason,
    String refundMethod = 'نقدي',
  }) => _send('refundPackage', {
    'packageId': packageId,
    'reason': reason,
    'refundMethod': refundMethod,
  });
  @override
  Future<void> reopenFinancialClosing({
    required String closingId,
    required String reason,
  }) => _send('reopenFinancialClosing', {
    'closingId': closingId,
    'reason': reason,
  });
  @override
  Future<void> checkPayment({
    required String studentId,
    required String sessionId,
    int? expectedAmount,
  }) => _send('checkPayment', {
    'studentId': studentId,
    'sessionId': sessionId,
    'expectedAmount': expectedAmount,
  });
  @override
  Future<void> uncheckPayment({
    required String studentId,
    required String sessionId,
  }) =>
      _send('uncheckPayment', {'studentId': studentId, 'sessionId': sessionId});
  @override
  Future<void> clearPaymentChecks({required String sessionId}) =>
      _send('clearPaymentChecks', {'sessionId': sessionId});
  @override
  Future<void> savePaymentReview(ReviewRequest r) =>
      _send('savePaymentReview', {
        'id': r.id,
        'studentId': r.studentId,
        'paperAmount': r.paperAmount,
        'sessionId': r.sessionId,
        'paymentId': r.paymentId,
        'notes': r.notes,
      });
  @override
  Future<void> finalizeSession({
    required String sessionId,
    required int actualCash,
    String notes = '',
  }) => _send('finalizeSession', {
    'sessionId': sessionId,
    'actualCash': actualCash,
    'notes': notes,
  });
}
