import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../application/center_store.dart';
import '../domain/center_exam_integration.dart';
import '../domain/models.dart';
import '../domain/center_exam_monitor.dart';
import '../domain/standalone_exam_results.dart';
import '../shared/performance_trace.dart';
import '../shared/problem_reporting.dart';
import 'exam_engine.dart';
import 'exam_network_maintenance.dart';

class ExamRoomController extends ChangeNotifier {
  ExamRoomController({
    required this.engine,
    required CenterStore store,
    this.pollInterval = const Duration(seconds: 3),
    this.clientOnly = false,
  }) : _store = store {
    _store.addListener(_storeChanged);
    _actor = store.currentUser?.id;
    _authorized = canAdmin;
    _syncAuthorized = canSync;
  }

  final ExamEngine engine;
  final Duration pollInterval;
  final bool clientOnly;
  bool _localExamMode = false;
  bool get localExamMode => _localExamMode;
  DateTime? _offlineUntil;
  String? _offlineStaffId;
  bool get offlinePrepared => _offlineUntil?.isAfter(DateTime.now()) == true;
  bool get canAdmin =>
      canSync ||
      clientOnly && _localExamMode ||
      clientOnly &&
          offlinePrepared &&
          (_store.currentUser == null ||
              _store.currentUser!.id == _offlineStaffId) &&
          (_store.isClientWorkspace ||
              _store.isRemote && !_store.remoteConnected);
  CenterStore _store;
  CenterStore get store => _store;
  Timer? _timer;
  String? _actor;
  int _authGeneration = 0;
  int get authorizationGeneration => _authGeneration;
  Future<void> _adminAccessTail = Future<void>.value();
  bool _authorized = false;
  bool _syncAuthorized = false;
  bool _disposed = false, _busy = false, starting = false;
  bool get running => engine.running;
  String? error;
  Map<String, dynamic> state = const {};
  final Map<String, String> resultProblems = {};
  final Map<String, String> _rosterFingerprints = {};
  String? _academicCatalogFingerprint;
  CenterStore? _academicCatalogStore;
  Object? _academicCatalogRevision;
  String? _academicCatalogSourceId;
  final Map<String, Object> _rejectedRevision = {};
  final Map<String, DateTime> _retryAfter = {};
  List<Map<String, dynamic>> get exams => _rows(state['exams']);
  List<Map<String, dynamic>> get templates => _rows(state['templates']);
  List<Map<String, dynamic>> get bindings => _rows(state['bindings']);
  List<Map<String, dynamic>> get attempts => _rows(state['attempts']);
  bool get hasLiveExam => exams.any(
    (exam) => exam['state'] == 'waiting' || exam['state'] == 'running',
  );
  bool get canSync =>
      _store.canAssess && (!_store.isRemote || _store.remoteConnected);

  static List<Map<String, dynamic>> _rows(Object? value) => value is List
      ? value
            .whereType<Map>()
            .map((row) => Map<String, dynamic>.from(row))
            .toList(growable: false)
      : const [];

  Future<void> initialize() async {
    try {
      _invalidateAcademicCatalog();
      final reconnected = await engine.reconnect();
      if (!reconnected && clientOnly) await engine.start();
      if (reconnected || clientOnly) {
        await _setAdminAccess(false);
        final snapshot = await engine.request('/api/center/state');
        _readOfflineAccess(snapshot);
        _authorized = canAdmin;
        if (canAdmin) state = snapshot;
        _beginPolling();
        await refresh();
      }
    } catch (failure, stack) {
      reportProblem(failure, stack, operation: 'exam.reconnect');
    }
    if (!_disposed) notifyListeners();
  }

  void useStore(CenterStore value) {
    if (identical(value, _store)) return;
    _store.removeListener(_storeChanged);
    _authGeneration++;
    _store = value;
    _store.addListener(_storeChanged);
    _rosterFingerprints.clear();
    _invalidateAcademicCatalog();
    _rejectedRevision.clear();
    _storeChanged(force: true);
  }

  void _storeChanged({bool force = false}) {
    final actor = _store.currentUser?.id;
    if (!force &&
        _actor == actor &&
        _authorized == canAdmin &&
        _syncAuthorized == canSync) {
      return;
    }
    _invalidateAcademicCatalog();
    final changedEmployee = _actor != null && _actor != actor;
    if (changedEmployee) {
      _offlineUntil = null;
      _offlineStaffId = null;
    }
    final remainsOffline = !changedEmployee && !canSync && canAdmin;
    _authorized = canAdmin;
    _syncAuthorized = canSync;
    _authGeneration++;
    _actor = actor;
    _rosterFingerprints.clear();
    _rejectedRevision.clear();
    resultProblems.clear();
    if (engine.running && !remainsOffline) {
      // Revoke web credentials on every employee change; student service stays up.
      unawaited(
        _setAdminAccess(false).catchError((Object failure, StackTrace stack) {
          reportProblem(failure, stack, operation: 'exam.lock');
          return <String, dynamic>{};
        }),
      );
    }
    if (actor == null && !canAdmin) state = const {};
    if (engine.running && changedEmployee) unawaited(_revokeOfflineAccess());
    if (!_disposed) notifyListeners();
  }

  void _beginPolling() {
    _timer ??= Timer.periodic(pollInterval, (_) => unawaited(refresh()));
  }

  Future<Map<String, dynamic>> _setAdminAccess(
    bool enabled, {
    int? generation,
  }) => _accessWrite(() {
    if (enabled && (_disposed || !canAdmin || generation != _authGeneration)) {
      throw const CenterException('تغيّرت جلسة الموظف؛ افتح الإدارة مجددًا.');
    }
    return engine.request('/api/center/admin-access', {'enabled': enabled});
  });

  Future<Map<String, dynamic>> _accessWrite(
    Future<Map<String, dynamic>> Function() write,
  ) {
    final request = _adminAccessTail.then((_) => write());
    // Keep later revocations runnable even if an earlier local request failed.
    _adminAccessTail = request.then<void>(
      (_) {},
      onError: (Object _, StackTrace _) {},
    );
    return request;
  }

  Future<void> enterLocalExamMode() async {
    if (!clientOnly) {
      throw const CenterException('افتح امتحانات الهوست من حساب الموظف.');
    }
    if (!_localExamMode) {
      _localExamMode = true;
      _storeChanged(force: true);
    }
    await start();
  }

  Future<void> start() async {
    if (starting) return;
    if (!canAdmin) {
      throw const CenterException('اتصل بالهوست وسجّل دخول الموظف أولًا.');
    }
    starting = true;
    error = null;
    notifyListeners();
    try {
      _invalidateAcademicCatalog();
      await engine.start();
      _beginPolling();
      await refresh();
    } catch (failure, stack) {
      error = failure is CenterException
          ? failure.message
          : 'تعذر تشغيل الامتحانات. راجع سجل المشاكل.';
      reportProblem(failure, stack, operation: 'exam.start');
      rethrow;
    } finally {
      starting = false;
      if (!_disposed) notifyListeners();
    }
  }

  Future<String> openAdmin() async {
    await start();
    final actor = _store.currentUser?.id;
    final generation = _authGeneration;
    if (!canAdmin) {
      throw const CenterException('سجّل دخول الموظف قبل فتح إدارة الامتحان.');
    }
    final result = await _setAdminAccess(true, generation: generation);
    if (_disposed ||
        _store.currentUser?.id != actor ||
        generation != _authGeneration ||
        !canAdmin) {
      throw const CenterException('تغيّر الموظف. افتح إدارة الامتحان من جديد.');
    }
    final address = result['adminUrl'];
    final uri = address is String ? Uri.tryParse(address) : null;
    if (uri == null ||
        uri.scheme != 'http' ||
        uri.host != '127.0.0.1' ||
        uri.port != 8766) {
      throw const CenterException('تعذر التحقق من عنوان إدارة الامتحان.');
    }
    return address as String;
  }

  Future<void> bind({
    required String examId,
    required String groupId,
    required String sessionId,
    required String activityId,
  }) async {
    if (_busy) throw const CenterException('جارٍ تحديث الرصد. حاول خلال لحظة.');
    if (!canSync) {
      throw const CenterException('اتصل بالهوست وسجّل الدخول لربط الامتحان.');
    }
    _busy = true;
    final generation = _authGeneration;
    try {
      final context = _bindingContext(
        groupId: groupId,
        sessionId: sessionId,
        activityId: activityId,
      );
      await engine.request('/api/center/bind', {'examId': examId, ...context});
      if (generation != _authGeneration || !canSync) {
        throw const CenterException(
          'تغيّرت جلسة الموظف؛ راجع ربط الامتحان قبل المتابعة.',
        );
      }
      _rosterFingerprints.remove(examId);
    } finally {
      _busy = false;
    }
    await refresh();
  }

  Map<String, dynamic> _bindingContext({
    required String groupId,
    required String sessionId,
    required String activityId,
  }) {
    final roster = _store.integratedExamRoster(
      groupId: groupId,
      sessionId: sessionId,
      activityId: activityId,
    );
    final activity = _store.academicActivities.firstWhere(
      (entry) => entry.id == activityId,
    );
    return {
      'groupId': groupId,
      'sessionId': sessionId,
      'activityId': activityId,
      'maxScore': activity.maxScore,
      'roster': roster.map((student) => student.toJson()).toList(),
    };
  }

  Future<String> openRoom({
    required String templateId,
    required String groupId,
    required String sessionId,
    required String activityId,
  }) async {
    if (_busy) throw const CenterException('جارٍ تحديث الرصد. حاول خلال لحظة.');
    if (_disposed || !canSync || !engine.running) {
      throw const CenterException(
        'شغّل خدمة الامتحانات واتصل بالهوست لفتح القاعة.',
      );
    }
    final context = _bindingContext(
      groupId: groupId,
      sessionId: sessionId,
      activityId: activityId,
    );
    final template = templates
        .where((row) => row['id'] == templateId)
        .firstOrNull;
    if (template == null || template['maxScore'] != context['maxScore']) {
      throw const CenterException(
        'اختر نموذج أسئلة يطابق الدرجة النهائية للامتحان في الرصد.',
      );
    }
    final generation = _authGeneration;
    bool stillAuthorized() =>
        !_disposed && generation == _authGeneration && canSync;
    _busy = true;
    late String examId;
    try {
      final created = await engine.request('/api/center/open', {
        'templateId': templateId,
        ...context,
      });
      if (!stillAuthorized()) {
        throw const CenterException(
          'تغيّرت جلسة الموظف؛ حدّث القاعات قبل المتابعة.',
        );
      }
      final id = created['examId'];
      if (id is! String || id.isEmpty) {
        throw const CenterException(
          'لم يتأكد فتح القاعة؛ حدّث القائمة قبل إعادة المحاولة.',
        );
      }
      examId = id;
      _rosterFingerprints[examId] = jsonEncode(context['roster']);
    } finally {
      _busy = false;
    }
    await refresh();
    if (!stillAuthorized()) {
      throw const CenterException(
        'تغيّرت جلسة الموظف؛ حدّث القاعات قبل المتابعة.',
      );
    }
    return examId;
  }

  void _invalidateAcademicCatalog() {
    _academicCatalogFingerprint = null;
    _academicCatalogStore = null;
    _academicCatalogRevision = null;
    _academicCatalogSourceId = null;
  }

  Future<void> _publishAcademicCatalog(
    CenterStore source,
    bool Function() stillAuthorized,
  ) async {
    final revision = source.readRevision;
    if (identical(source, _academicCatalogStore) &&
        identical(revision, _academicCatalogRevision)) {
      return;
    }
    final contexts = <Map<String, dynamic>>[];
    for (final session in source.sessions) {
      if (session.status == SessionStatus.canceled) {
        continue;
      }
      for (final activity in source.academicActivitiesFor(session.id)) {
        if (activity.kind != AcademicActivityKind.exam ||
            !activity.maxScoreKnown) {
          continue;
        }
        contexts.add({
          'groupId': session.groupId,
          'sessionId': session.id,
          'activityId': activity.id,
          'groupLabel': source.groupLabel(session.groupId),
          'sessionLabel':
              'الشهر ${session.monthNumber} • الحصة ${session.number}',
          'activityName': activity.name,
          'maxScore': activity.maxScore,
          'allowedMaxScores': activity.allowedMaxScores,
        });
      }
    }
    final catalog = {
      'hostId': source.academicCatalogHostId,
      'contexts': contexts,
    };
    final encoded = jsonEncode(catalog);
    if (_academicCatalogFingerprint == encoded) {
      _academicCatalogStore = source;
      _academicCatalogRevision = revision;
      return;
    }
    if (contexts.length > 1000 || utf8.encode(encoded).length > 1024 * 1024) {
      throw const CenterException(
        'كشف الحصص والامتحانات أكبر من حد النقل إلى جهاز الامتحان.',
      );
    }
    final saved = await engine.request('/api/center/catalog', catalog);
    if (saved['saved'] != true || saved['contextCount'] != contexts.length) {
      throw const CenterException(
        'لم يؤكد جهاز الامتحان حفظ كشف الحصص والامتحانات.',
      );
    }
    if (stillAuthorized() && canSync) {
      _academicCatalogFingerprint = encoded;
      _academicCatalogStore = source;
      _academicCatalogRevision = revision;
    }
  }

  Future<void> _publishStandaloneRooms(
    CenterStore destination,
    bool Function() stillAuthorized,
  ) async {
    String? cursor;
    final seenRooms = <String>{};
    do {
      final catalog = await engine.request('/api/center/standalone-rooms', {
        'afterExamId': ?cursor,
      });
      if (!stillAuthorized() || !canSync) return;
      if (catalog['protocol'] != 1 ||
          catalog['sourceId'] is! String ||
          catalog['hasMore'] != (catalog['nextCursor'] != null)) {
        throw const CenterException('كشف امتحانات الجهاز غير صالح.');
      }
      final rooms = _rows(catalog['rooms']);
      if (rooms.length > 200 || seenRooms.length + rooms.length > 1000) {
        throw const CenterException('عدد امتحانات الجهاز أكبر من حد التنزيل.');
      }
      for (final room in rooms) {
        final examId = room['examId'] as String;
        if (!seenRooms.add(examId)) {
          throw const CenterException('كشف امتحانات الجهاز مكرر.');
        }
        final pages = <StandaloneExamPage>[];
        String? afterAttempt;
        do {
          final exported = await engine
              .request('/api/center/standalone-results', {
                'examId': examId,
                'afterAttemptId': ?afterAttempt,
                if (pages.isNotEmpty)
                  'exportRevision': pages.first.exportRevision,
              });
          if (!stillAuthorized() || !canSync) return;
          final page = StandaloneExamPage.fromJson({
            ...exported,
            'afterAttemptId': afterAttempt,
          });
          if (page.sourceId != catalog['sourceId'] ||
              page.examId != examId ||
              (pages.isNotEmpty &&
                  page.exportRevision != pages.first.exportRevision)) {
            throw const CenterException(
              'تغيّر مصدر أو إصدار الامتحان أثناء التنزيل.',
            );
          }
          if (pages.isEmpty &&
              destination.standaloneExamRooms.any(
                (saved) =>
                    saved.id == page.id &&
                    saved.complete &&
                    saved.exportRevision == page.exportRevision,
              )) {
            break;
          }
          pages.add(page);
          if (pages.length > 50 ||
              page.nextCursor == afterAttempt && page.nextCursor != null) {
            throw const CenterException('دفعات نتائج الامتحان غير متسلسلة.');
          }
          afterAttempt = page.nextCursor;
        } while (afterAttempt != null);
        if (pages.isEmpty) continue;
        final verified = await engine.request(
          '/api/center/standalone-results',
          {'examId': examId, 'exportRevision': pages.first.exportRevision},
        );
        if (!stillAuthorized() || !canSync) return;
        if (verified['exportRevision'] != pages.first.exportRevision) {
          throw const CenterException(
            'تغيّرت النتائج قبل اعتماد التنزيل. أعد المحاولة.',
          );
        }
        for (final page in pages) {
          if (!stillAuthorized() || !canSync) return;
          if (utf8.encode(jsonEncode(page.toJson())).length > 256 * 1024) {
            throw const CenterException(
              'دفعة نتائج الامتحان أكبر من حد النقل.',
            );
          }
          await destination.publishStandaloneExamPage(page);
          await Future<void>.delayed(Duration.zero);
        }
      }
      final nextCursor = catalog['nextCursor'] as String?;
      if (nextCursor != null &&
          (rooms.isEmpty ||
              nextCursor != rooms.last['examId'] ||
              nextCursor == cursor)) {
        throw const CenterException('صفحات كشف الامتحانات غير متسلسلة.');
      }
      cursor = nextCursor;
    } while (cursor != null);
  }

  Future<void> refresh() async {
    if (_authorized && !canAdmin) _storeChanged();
    if (_disposed || _busy || !engine.running || !canAdmin) return;
    _busy = true;
    final activeStore = _store;
    final actor = activeStore.currentUser?.id;
    final generation = _authGeneration;
    bool stillAuthorized() =>
        !_disposed &&
        identical(activeStore, _store) &&
        actor == _store.currentUser?.id &&
        generation == _authGeneration &&
        canAdmin;
    final trace = PerformanceTrace('exam.synchronize', budgetMs: 250);
    try {
      final next = await engine.request('/api/center/state');
      if (!stillAuthorized()) return;
      if (next['protocol'] != 1) {
        throw const CenterException('إصدار ربط الامتحان غير متوافق.');
      }
      state = next;
      _readOfflineAccess(next);
      if (!canAdmin) {
        await _setAdminAccess(false);
        _storeChanged();
        return;
      }
      error = null;
      if (!canSync) return;
      if (next['standaloneExportProtocol'] == 1) {
        await _publishStandaloneRooms(activeStore, stillAuthorized);
        if (!stillAuthorized() || !canSync) return;
      }
      if (next['academicCatalogProtocol'] == 1) {
        if (_academicCatalogSourceId != next['sourceId']) {
          _invalidateAcademicCatalog();
          _academicCatalogSourceId = next['sourceId'] as String?;
        }
        await _publishAcademicCatalog(activeStore, stillAuthorized);
        if (!stillAuthorized() || !canSync) return;
      } else {
        _invalidateAcademicCatalog();
      }
      final currentResults = _rows(next['results']);
      final keys = currentResults
          .map((row) => '${row['attemptId']}:${row['version']}')
          .toSet();
      resultProblems.removeWhere((key, _) => !keys.contains(key));
      _rejectedRevision.removeWhere((key, _) => !keys.contains(key));
      _retryAfter.removeWhere((key, _) => !keys.contains(key));
      for (final binding in bindings) {
        if (!stillAuthorized()) return;
        final examId = binding['examId'] as String;
        try {
          final roster = activeStore.integratedExamRoster(
            groupId: binding['groupId'] as String,
            sessionId: binding['sessionId'] as String,
            activityId: binding['activityId'] as String,
          );
          final rows = roster.map((student) => student.toJson()).toList();
          final fingerprint = jsonEncode(rows);
          if (_rosterFingerprints[examId] == fingerprint) continue;
          await engine.request('/api/center/bind', {
            ...binding,
            'roster': rows,
          });
          if (!stillAuthorized()) return;
          _rosterFingerprints[examId] = fingerprint;
        } on CenterException catch (failure) {
          error = failure.message;
        }
      }
      trace.stage('roster');
      for (final row in _rows(next['monitors'])) {
        if (!stillAuthorized()) return;
        try {
          final snapshot = ExamMonitorSnapshot.fromJson(row);
          if (snapshot.sourceId != next['sourceId']) {
            throw const CenterException(
              'مصدر متابعة الحضور لا يطابق جهاز الامتحان.',
            );
          }
          await activeStore.publishIntegratedExamSnapshot(snapshot);
          if (!stillAuthorized()) return;
        } on CenterException catch (failure) {
          error = failure.message;
        }
        await Future<void>.delayed(Duration.zero);
      }
      trace.stage('attendance_monitor');
      // Yield between receipts: reception commands can join the host's normal queue.
      var sent = 0;
      final requestedRooms = {
        for (final room in activeStore.integratedExamRooms)
          if (room.snapshot.sourceId == next['sourceId'] && room.importPending)
            room.snapshot.examId: room,
      };
      for (final row in currentResults) {
        if (!stillAuthorized()) return;
        if (activeStore.isRemote &&
            !requestedRooms.containsKey(row['examId'])) {
          continue;
        }
        final key = '${row['attemptId']}:${row['version']}';
        if (row['status'] == 'revoked') {
          resultProblems[key] =
              'المحاولة أُلغيت أو أُعيدت بعد الرصد؛ راجع درجتها في السنتر.';
          continue;
        }
        if (identical(_rejectedRevision[key], activeStore.readRevision) &&
            (_retryAfter[key]?.isAfter(DateTime.now()) ?? false)) {
          continue;
        }
        if (sent >= 20) break;
        sent++;
        var committed = false;
        try {
          final result = IntegratedExamResult.fromJson(row);
          if (result.sourceId != next['sourceId']) {
            throw const CenterException('مصدر الدرجة لا يطابق جهاز الامتحان.');
          }
          final receipt = await activeStore.recordIntegratedExamResult(result);
          committed = true;
          if (!stillAuthorized()) return;
          if (!mapEquals(receipt.result.toJson(), result.toJson()) ||
              receipt.recordId.isEmpty) {
            throw const CenterException(
              'لم يؤكد الهوست حفظ نفس الدرجة المطلوبة.',
            );
          }
          await engine.request('/api/center/ack', {
            'attemptId': result.attemptId,
            'version': result.version,
          });
          resultProblems.remove(key);
          _rejectedRevision.remove(key);
          _retryAfter.remove(key);
        } on CenterException catch (failure) {
          resultProblems[key] = failure.message;
          if (!committed && failure.cause == null) {
            _rejectedRevision[key] = activeStore.readRevision;
            _retryAfter[key] = DateTime.now().add(const Duration(seconds: 15));
          }
        }
        await Future<void>.delayed(Duration.zero);
      }
      if (activeStore.isRemote && sent < 20 && stillAuthorized()) {
        for (final room in requestedRooms.values) {
          if (!stillAuthorized()) return;
          final problems = <String, String>{};
          for (final row in currentResults.where(
            (row) => row['examId'] == room.snapshot.examId,
          )) {
            final message =
                resultProblems['${row['attemptId']}:${row['version']}'];
            if (message != null) problems[row['studentId'] as String] = message;
          }
          await activeStore.completeExamResultImport(
            roomId: room.id,
            requestId: room.importRequestId!,
            problems: problems,
          );
        }
      }
      trace.stage('results');
    } catch (failure, stack) {
      error = failure is CenterException
          ? failure.message
          : 'تعذر تحديث الربط. الامتحان يحتفظ بالإجابات، وستُعاد المزامنة.';
      reportProblem(failure, stack, operation: 'exam.synchronize');
    } finally {
      trace.finish(failed: error != null);
      _busy = false;
      if (!_disposed) notifyListeners();
    }
  }

  void _readOfflineAccess(Map<String, dynamic> snapshot) {
    final access = snapshot['offlineAccess'];
    if (access is! Map || access['allowed'] != true) {
      _offlineUntil = null;
      _offlineStaffId = null;
      return;
    }
    final seconds = access['remainingSeconds'];
    final staffId = access['staffId'];
    if (seconds is! int ||
        seconds <= 0 ||
        seconds > 43200 ||
        staffId is! String) {
      throw const CenterException(
        'تجهيز الساكند للقاعة غير صالح. جهّزه أثناء الاتصال.',
      );
    }
    _offlineUntil = DateTime.now().add(Duration(seconds: seconds));
    _offlineStaffId = staffId;
  }

  Future<void> _revokeOfflineAccess() async {
    try {
      await _accessWrite(
        () => engine.request('/api/center/offline-access', {'revoke': true}),
      );
    } catch (failure, stack) {
      reportProblem(failure, stack, operation: 'exam.offline_revoke');
    }
  }

  Future<void> prepareHallContext({
    required String groupId,
    required String sessionId,
    required String activityId,
  }) async {
    if (!canSync || !clientOnly) {
      throw const CenterException(
        'وصّل الساكند بالهوست وسجّل الدخول قبل تجهيز القاعة.',
      );
    }
    await start();
    final generation = _authGeneration;
    final staffId = _store.currentUser?.id;
    _requireHallPreparationAccess(generation, staffId);
    final context = _bindingContext(
      groupId: groupId,
      sessionId: sessionId,
      activityId: activityId,
    );
    final activity = _store.academicActivities.firstWhere(
      (row) => row.id == activityId,
    );
    final session = _store.sessionById(sessionId)!;
    final prepared = await engine.request('/api/center/context', {
      ...context,
      'groupLabel': _store.groupLabel(groupId),
      'sessionLabel': 'حصة ${session.number} • ${session.name}',
      'activityName': activity.name,
    });
    _requireHallPreparationAccess(generation, staffId);
    final access = await _accessWrite(() {
      _requireHallPreparationAccess(generation, staffId);
      return engine.request('/api/center/offline-access', {
        'staffId': staffId,
        'contextId': prepared['contextId'],
      });
    });
    _requireHallPreparationAccess(generation, staffId);
    _readOfflineAccess({'offlineAccess': access});
    if (!offlinePrepared || _offlineStaffId != staffId) {
      _offlineUntil = null;
      _offlineStaffId = null;
      throw const CenterException(
        'لم يتأكد حفظ تجهيز الساكند. أبقِ الجهازين متصلين وأعد التجهيز قبل الفصل.',
      );
    }
    _authorized = canAdmin;
    notifyListeners();
  }

  void _requireHallPreparationAccess(int generation, String? staffId) {
    if (_disposed || staffId == null || _store.currentUser?.id != staffId) {
      throw const CenterException('تغيّر حساب الموظف. أعد تجهيز القاعة.');
    }
    if (!canSync) {
      throw const CenterException(
        'انقطع الاتصال بالهوست أثناء التجهيز. أعد توصيل الجهازين وانتظر تأكيد جاهزية الساكند قبل الفصل.',
      );
    }
    if (generation != _authGeneration) {
      throw const CenterException('تغيّرت جلسة الموظف. أعد تجهيز القاعة.');
    }
  }

  String missingReason(String examId, String studentId) {
    final rows = attempts.where(
      (row) => row['examId'] == examId && row['studentId'] == studentId,
    );
    if (rows.isEmpty) return 'لم يدخل الامتحان';
    final attempt = rows.first;
    final problem =
        resultProblems['${attempt['attemptId'] ?? attempt['id']}:${attempt['resultVersion']}'];
    if (problem != null) return problem;
    return switch (attempt['status']) {
      'cancelled' || 'canceled' || 'revoked' => 'محاولة ملغاة — تحتاج مراجعة',
      'pending' || 'grading' || 'pending_grading' => 'بانتظار تصحيح المقالي',
      'awaiting_close' => 'اكتمل التصحيح — بانتظار إغلاق الامتحان',
      'submitted' =>
        (attempt['pending'] as num? ?? 0) > 0
            ? 'بانتظار تصحيح المقالي'
            : 'بانتظار إغلاق الامتحان والرصد',
      'final' || 'ready' || 'recorded' => 'اكتمل التصحيح — بانتظار تأكيد الرصد',
      _ => 'دخل ولم يسلّم',
    };
  }

  Future<void> prepareNetworkEngine() async {
    if (_disposed) throw const CenterException('أعد فتح شاشة تجهيز الشبكة.');
    if (!running) {
      _invalidateAcademicCatalog();
      await engine.start();
    }
    if (!canAdmin) await _setAdminAccess(false);
    _beginPolling();
  }

  Future<Map<String, dynamic>> network(Map<String, dynamic> command) async {
    final action = command['action'];
    const localActions = {
      'status',
      'list-adapters',
      'hotspot-status',
      'hotspot-start',
      'hotspot-stop',
      'tethering-status',
      'tethering-start',
      'tethering-stop',
      'configure',
      'restore',
      'portal-configure',
    };
    if (_disposed || !localActions.contains(action)) {
      throw const CenterException('طلب تجهيز الشبكة غير معروف.');
    }
    if (action == 'hotspot-start' ||
        action == 'hotspot-stop' ||
        action == 'tethering-start' ||
        action == 'tethering-stop') {
      return _changeHotspot(command);
    }
    if (action == 'portal-configure' ||
        action == 'configure' ||
        action == 'restore') {
      // The native helper acquires a durable lease that checks actual rooms.
      // Network setup before login never enables the exam administration UI.
      await prepareNetworkEngine();
    }
    return engine.network(command);
  }

  Future<Map<String, dynamic>> _changeHotspot(
    Map<String, dynamic> command,
  ) async {
    await prepareNetworkEngine();
    final maintenance = ExamNetworkMaintenance(engine);
    Map<String, dynamic> result;
    try {
      result = await maintenance.run((beforeWrite) async {
        if (_disposed) {
          throw const CenterException('أعد فتح شاشة تجهيز الشبكة.');
        }
        await beforeWrite();
        return engine.network(command);
      });
    } catch (failure, stack) {
      if (maintenance.releaseWarning == null) rethrow;
      throw CenterException(
        '${failure is CenterException ? failure.message : 'تعذر إكمال طلب الشبكة.'} '
        '${maintenance.releaseWarning}',
        cause: failure,
        stackTrace: stack,
      );
    } finally {
      if (maintenance.releaseWarning != null) {
        error = maintenance.releaseWarning;
        if (!_disposed) notifyListeners();
      }
    }
    final warning = maintenance.releaseWarning;
    if (warning == null) return result;
    return {
      ...result,
      'warning': warning,
      'message': '${result['message'] ?? 'راجع حالة الشبكة.'} $warning',
    };
  }

  @override
  void dispose() {
    _disposed = true;
    _timer?.cancel();
    _store.removeListener(_storeChanged);
    unawaited(engine.close());
    super.dispose();
  }
}
