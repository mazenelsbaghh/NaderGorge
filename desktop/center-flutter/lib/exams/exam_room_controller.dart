import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../application/center_store.dart';
import '../domain/center_exam_integration.dart';
import '../shared/performance_trace.dart';
import '../shared/problem_reporting.dart';
import 'exam_engine.dart';

class ExamRoomController extends ChangeNotifier {
  ExamRoomController({
    required this.engine,
    required CenterStore store,
    this.pollInterval = const Duration(seconds: 3),
  }) : _store = store {
    _store.addListener(_storeChanged);
    _actor = store.currentUser?.id;
    _authorized = canSync;
  }

  final ExamEngine engine;
  final Duration pollInterval;
  CenterStore _store;
  CenterStore get store => _store;
  Timer? _timer;
  String? _actor;
  int _authGeneration = 0;
  int get authorizationGeneration => _authGeneration;
  Future<void> _adminAccessTail = Future<void>.value();
  bool _authorized = false;
  bool _disposed = false, _busy = false, starting = false;
  bool get running => engine.running;
  String? error;
  Map<String, dynamic> state = const {};
  final Map<String, String> resultProblems = {};
  final Map<String, String> _rosterFingerprints = {};
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
      if (await engine.reconnect()) {
        await _setAdminAccess(false);
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
    _rejectedRevision.clear();
    _storeChanged(force: true);
  }

  void _storeChanged({bool force = false}) {
    final actor = _store.currentUser?.id;
    if (!force && _actor == actor && _authorized == canSync) return;
    _authorized = canSync;
    _authGeneration++;
    _actor = actor;
    _rosterFingerprints.clear();
    _rejectedRevision.clear();
    resultProblems.clear();
    if (engine.running) {
      // Revoke web credentials on every employee change; student service stays up.
      unawaited(
        _setAdminAccess(false).catchError((Object failure, StackTrace stack) {
          reportProblem(failure, stack, operation: 'exam.lock');
          return <String, dynamic>{};
        }),
      );
    }
    if (actor == null) state = const {};
    if (!_disposed) notifyListeners();
  }

  void _beginPolling() {
    _timer ??= Timer.periodic(pollInterval, (_) => unawaited(refresh()));
  }

  Future<Map<String, dynamic>> _setAdminAccess(
    bool enabled, {
    int? generation,
  }) {
    final request = _adminAccessTail.then((_) {
      if (enabled && (_disposed || !canSync || generation != _authGeneration)) {
        throw const CenterException('تغيّرت جلسة الموظف؛ افتح الإدارة مجددًا.');
      }
      return engine.request('/api/center/admin-access', {'enabled': enabled});
    });
    // Keep later revocations runnable even if an earlier local request failed.
    _adminAccessTail = request.then<void>(
      (_) {},
      onError: (Object _, StackTrace _) {},
    );
    return request;
  }

  Future<void> start() async {
    if (starting) return;
    if (!canSync) {
      throw const CenterException('اتصل بالهوست وسجّل دخول الموظف أولًا.');
    }
    starting = true;
    error = null;
    notifyListeners();
    try {
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
    if (!canSync) {
      throw const CenterException('سجّل دخول الموظف قبل فتح إدارة الامتحان.');
    }
    final result = await _setAdminAccess(true, generation: generation);
    if (_disposed ||
        _store.currentUser?.id != actor ||
        generation != _authGeneration ||
        !canSync) {
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

  Future<void> refresh() async {
    if (_disposed || _busy || !engine.running || !canSync) return;
    _busy = true;
    final activeStore = _store;
    final actor = activeStore.currentUser?.id;
    final generation = _authGeneration;
    bool stillAuthorized() =>
        !_disposed &&
        identical(activeStore, _store) &&
        actor == _store.currentUser?.id &&
        generation == _authGeneration &&
        canSync;
    final trace = PerformanceTrace('exam.synchronize', budgetMs: 250);
    try {
      final next = await engine.request('/api/center/state');
      if (!stillAuthorized()) return;
      if (next['protocol'] != 1) {
        throw const CenterException('إصدار ربط الامتحان غير متوافق.');
      }
      state = next;
      error = null;
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
      // Yield between receipts: reception commands can join the host's normal queue.
      var sent = 0;
      for (final row in currentResults) {
        if (!stillAuthorized()) return;
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

  Future<Map<String, dynamic>> network(Map<String, dynamic> command) async {
    if (!canSync) {
      throw const CenterException('سجّل الدخول قبل تغيير إعداد الشبكة.');
    }
    final generation = _authGeneration;
    final action = command['action'];
    if (action == 'portal-configure' ||
        action == 'configure' ||
        action == 'restore' ||
        action == 'hotspot-stop') {
      if (running) {
        await refresh();
        if (error != null) {
          throw const CenterException(
            'تعذر التأكد من حالة القاعات؛ أعد الفحص قبل تغيير الشبكة.',
          );
        }
      }
      if (hasLiveExam) {
        throw const CenterException(
          'أغلق قاعات الامتحان قبل تغيير إعداد الشبكة.',
        );
      }
    }
    if (!canSync || generation != _authGeneration) {
      throw const CenterException('تغيّرت جلسة الموظف؛ لم يُرسل طلب التهيئة.');
    }
    return engine.network(command);
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
