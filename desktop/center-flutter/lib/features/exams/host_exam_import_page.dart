import 'dart:io';
import 'package:file_selector/file_selector.dart';
import 'package:path/path.dart' as p;
import 'package:flutter/material.dart';
import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../domain/standalone_exam_results.dart';
import '../../exams/exam_attendance_xlsx.dart';
import '../../exams/exam_export_saver.dart';
import '../../domain/center_exam_monitor.dart';
import '../../domain/center_exam_integration.dart';
import '../../shared/theme.dart';
import '../management/management_widgets.dart';

class HostExamImportPage extends StatefulWidget {
  const HostExamImportPage({super.key, required this.store});
  final CenterStore store;
  @override
  State<HostExamImportPage> createState() => _HostExamImportPageState();
}

class _HostExamImportPageState extends State<HostExamImportPage> {
  String? _roomId, _groupId, _sessionId, _activityId, _error, _completion;
  bool _busy = false;

  void _selectRoom(StandaloneExamRoom room) {
    final store = widget.store;
    final target = room.target;
    final targetHere = target?.hostId == store.academicCatalogHostId;
    setState(() {
      _roomId = room.id;
      _groupId = room.groupId ?? (targetHere ? target?.groupId : null);
      _sessionId = room.sessionId ?? (targetHere ? target?.sessionId : null);
      _activityId = room.activityId ?? (targetHere ? target?.activityId : null);
      _error = _completion = null;
    });
  }

  Future<void> _import(StandaloneExamImportPlan plan) async {
    final store = widget.store, actor = widget.store.currentUser?.id;
    setState(() {
      _busy = true;
      _error = _completion = null;
    });
    try {
      final result = await store.importStandaloneExamGrades(plan);
      if (mounted && store.canAssess && store.currentUser?.id == actor) {
        setState(
          () => _completion =
              'تم رصد ${result.recorded} نتيجة • مرصود من قبل: ${result.alreadyRecorded} • لم يُرصد: ${result.skipped}',
        );
      }
    } catch (failure) {
      if (mounted) setState(() => _error = managementError(failure));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _export(StandaloneExamImportPlan plan) async {
    final store = widget.store, actor = widget.store.currentUser?.id;
    var sessionChanged = actor == null;
    void observeSession() {
      if (!store.canAssess || store.currentUser?.id != actor) {
        sessionChanged = true;
      }
    }

    bool authorized() =>
        mounted &&
        !sessionChanged &&
        store.canAssess &&
        !store.isRemote &&
        store.currentUser?.id == actor;
    Directory? temporary;
    store.addListener(observeSession);
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final fresh = store.previewStandaloneExamImport(
        roomId: plan.roomId,
        groupId: plan.groupId,
        sessionId: plan.sessionId,
        activityId: plan.activityId,
      );
      final bytes = buildExamAttendanceWorkbook(store: store, plan: fresh);
      temporary = await Directory.systemTemp.createTemp(
        'massar-exam-attendance-',
      );
      final source = File(p.join(temporary.path, 'attendance.xlsx'));
      await source.writeAsBytes(bytes, flush: true);
      if (!authorized()) {
        throw const CenterException(
          'تغيّرت جلسة الموظف؛ أعد التصدير بعد الدخول.',
        );
      }
      final destination = await getSaveLocation(
        suggestedName: 'حاضرون-لم-يدخلوا-الامتحان.xlsx',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'شيت Excel', extensions: ['xlsx']),
        ],
      );
      if (destination == null) return;
      await ExamExportSaver(
        protectedDirectories: [
          Directory(p.dirname(store.databasePath)),
          Directory(p.dirname(Platform.resolvedExecutable)),
          if (store.automaticBackupDirectory case final String folder)
            Directory(folder),
        ],
        isAuthorized: authorized,
      ).save(source, destination.path);
      if (authorized()) {
        setState(
          () => _completion =
              'تم حفظ شيت ${fresh.didNotEnter.length} حاضر لم يدخلوا الامتحان، ومعه ورقة منفصلة للحالات التي تحتاج متابعة.',
        );
      }
    } catch (failure) {
      if (mounted && authorized()) {
        setState(() => _error = managementError(failure));
      }
    } finally {
      store.removeListener(observeSession);
      if (temporary != null) await temporary.delete(recursive: true);
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _choice(
    String label,
    String? selected,
    List<DropdownMenuItem<String>> options,
    ValueChanged<String?> onChanged,
  ) => SizedBox(
    width: 300,
    child: DropdownButtonFormField<String>(
      key: ValueKey('$label:$selected'),
      initialValue: selected,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: options,
      onChanged: _busy ? null : onChanged,
    ),
  );

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: widget.store,
    builder: (context, _) {
      final store = widget.store;
      if (!store.canAssess || store.isRemote) {
        return const Center(
          child: Text('افتح تحميل الدرجات من حساب الموظف على الهوست.'),
        );
      }
      final rooms = store.standaloneExamRooms.toList()
        ..sort((left, right) => right.receivedAt.compareTo(left.receivedAt));
      final room =
          rooms.where((row) => row.id == _roomId).firstOrNull ??
          rooms.firstOrNull;
      final target = room?.target;
      final targetHere = target?.hostId == store.academicCatalogHostId;
      final groupId =
          _groupId ?? room?.groupId ?? (targetHere ? target?.groupId : null);
      final selectedGroup = store.groups
          .where((row) => row.id == groupId)
          .firstOrNull;
      final sessions =
          store.sessions
              .where(
                (row) =>
                    row.groupId == selectedGroup?.id &&
                    row.status != SessionStatus.canceled &&
                    store.sessionHasStarted(row.id),
              )
              .toList()
            ..sort((left, right) => right.startsAt.compareTo(left.startsAt));
      final sessionId =
          _sessionId ??
          room?.sessionId ??
          (targetHere ? target?.sessionId : null);
      final session = sessions.where((row) => row.id == sessionId).firstOrNull;
      final activities = store.academicActivities
          .where(
            (row) =>
                row.kind == AcademicActivityKind.exam &&
                row.maxScoreKnown &&
                session != null &&
                row.appliesToSession(session),
          )
          .toList();
      final activityId =
          _activityId ??
          room?.activityId ??
          (targetHere ? target?.activityId : null);
      final activity = activities
          .where((row) => row.id == activityId)
          .firstOrNull;
      StandaloneExamImportPlan? plan;
      String? previewError;
      if (room != null &&
          room.complete &&
          selectedGroup != null &&
          session != null &&
          activity != null) {
        try {
          plan = store.previewStandaloneExamImport(
            roomId: room.id,
            groupId: selectedGroup.id,
            sessionId: session.id,
            activityId: activity.id,
          );
        } catch (failure) {
          previewError = managementError(failure);
        }
      }
      final legacy = store.integratedExamRooms
          .where((row) => row.deviceId != 'local')
          .isNotEmpty;
      final approved = plan;
      return ManagementPanel(
        title: 'تحميل درجات الساكند',
        subtitle:
            'بعد الامتحان والتصحيح، أعد ربط الجهازين وافتح برنامج الساكند بحساب الموظف. اختر هنا مكان الرصد، ثم أكّد كل النتائج المطابقة مرة واحدة. لا تحتاج شيت استيراد.',
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(vertical: 16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (room == null)
                const Text(
                  'لم تصل نتائج من الساكند بعد. شغّل الامتحان عليه مباشرةً، ثم أعد ربطه بالهوست بعد الانتهاء لتصل النتائج هنا تلقائيًا.',
                ),
              if (room != null) ...[
                Wrap(
                  spacing: 12,
                  runSpacing: 12,
                  children: [
                    _choice(
                      'امتحان الساكند',
                      room.id,
                      rooms
                          .map(
                            (row) => DropdownMenuItem(
                              value: row.id,
                              child: Text(
                                '${row.title} • من ${row.maxScore}',
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          )
                          .toList(),
                      (id) {
                        if (id != null) {
                          _selectRoom(rooms.firstWhere((row) => row.id == id));
                        }
                      },
                    ),
                    _choice(
                      'مجموعة السنتر',
                      selectedGroup?.id,
                      store.groups
                          .map(
                            (row) => DropdownMenuItem(
                              value: row.id,
                              child: Text(
                                store.groupLabel(row.id),
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          )
                          .toList(),
                      (id) => setState(() {
                        _roomId = room.id;
                        _groupId = id;
                        _sessionId = _activityId = null;
                        _error = _completion = null;
                      }),
                    ),
                    _choice(
                      'الحصة التي حضرها الطلاب',
                      session?.id,
                      sessions
                          .map(
                            (row) => DropdownMenuItem(
                              value: row.id,
                              child: Text(
                                'شهر ${row.monthNumber} • ${row.name.isEmpty ? 'حصة ${row.number}' : row.name}',
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          )
                          .toList(),
                      (id) => setState(() {
                        _roomId = room.id;
                        _groupId = selectedGroup?.id;
                        _sessionId = id;
                        _activityId = null;
                        _error = _completion = null;
                      }),
                    ),
                    _choice(
                      'اسم الامتحان في الرصد',
                      activity?.id,
                      activities
                          .map(
                            (row) => DropdownMenuItem(
                              value: row.id,
                              child: Text(
                                '${row.name} • من ${row.allowedMaxScores.join(' أو ')}',
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          )
                          .toList(),
                      (id) => setState(() {
                        _roomId = room.id;
                        _groupId = selectedGroup?.id;
                        _sessionId = session?.id;
                        _activityId = id;
                        _error = _completion = null;
                      }),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                Text(
                  room.complete
                      ? 'وصل كشف ${room.totalAttempts} محاولة • ${room.roomState == 'closed' ? 'الامتحان مغلق' : 'الامتحان لم يُغلق بعد'}'
                      : 'جارٍ وصول النتائج: ${room.attempts.length} من ${room.totalAttempts}. انتظر اكتمال الكشف قبل الرصد.',
                ),
                if (room.target != null && !targetHere)
                  const Text(
                    'هذا الامتحان موجّه لهوست آخر. لن تُرصد درجاته هنا.',
                  ),
                if (selectedGroup != null && sessions.isEmpty)
                  const Text(
                    'افتح الحصة لهذه المجموعة على الهوست لتختارها للرصد.',
                  ),
                if (session != null && activities.isEmpty)
                  const Text(
                    'أضف اسم امتحان ودرجته لهذه الحصة على الهوست أولًا.',
                  ),
                if (activity != null &&
                    !activity.acceptsMaxScore(room.maxScore))
                  Text(
                    'النهاية الفعلية للامتحان ${room.maxScore} غير مسموحة في الامتحان المختار. اختر الامتحان الصحيح أو اضبط نهاياته المسموحة.',
                  ),
                if (approved != null) ...[
                  const Divider(height: 28),
                  Wrap(
                    spacing: 24,
                    runSpacing: 10,
                    children: [
                      Text('الحاضرون: ${approved.attending}'),
                      Text('دخلوا الامتحان: ${approved.examined}'),
                      Text('مرصود: ${approved.recorded}'),
                      Text('حاضر ولم يدخل: ${approved.didNotEnter.length}'),
                      Text('جاهز للرصد: ${approved.readyCount}'),
                      Text('مرصود من قبل: ${approved.alreadyRecordedCount}'),
                    ],
                  ),
                  const SizedBox(height: 16),
                  if (approved.replacementCount > 0)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Text(
                        'التأكيد سيستبدل ${approved.replacementCount} درجة محفوظة بالنتيجة الجديدة.',
                        style: TextStyle(
                          color: MassarPalette.of(context).warning,
                        ),
                      ),
                    ),
                  Wrap(
                    spacing: 12,
                    runSpacing: 12,
                    children: [
                      FilledButton.icon(
                        key: const Key('host-confirm-standalone-grades'),
                        onPressed: _busy || approved.readyCount == 0
                            ? null
                            : () => _import(approved),
                        icon: const Icon(Icons.done_all),
                        label: Text(
                          _busy
                              ? 'جارٍ التنفيذ…'
                              : 'تأكيد ورصد ${approved.readyCount} نتيجة${approved.replacementCount == 0 ? '' : ' • تعديل ${approved.replacementCount}'}',
                        ),
                      ),
                      OutlinedButton.icon(
                        key: const Key('host-export-missing-exam'),
                        onPressed: _busy ? null : () => _export(approved),
                        icon: const Icon(Icons.table_view_outlined),
                        label: Text(
                          'شيت الحاضرين الذين لم يدخلوا (${approved.didNotEnter.length})',
                        ),
                      ),
                    ],
                  ),
                  if (approved.skippedReasons.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    const Text('الحالات التي لن تُرصد بهذا التأكيد:'),
                    for (final reason in approved.skippedReasons.entries)
                      Text('${reason.value} • ${reason.key}'),
                  ],
                  if (approved.attendanceIssues.isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.only(top: 12),
                      child: Text(
                        '${approved.attendanceIssues.length} حاضر يحتاج متابعة: مقالي أو محاولة غير مكتملة أو مطابقة غير مؤكدة. التفاصيل في ورقة المتابعة بالشيت.',
                      ),
                    ),
                ],
              ],
              if (_completion != null)
                Padding(
                  padding: const EdgeInsets.only(top: 16),
                  child: Text(
                    _completion!,
                    key: const Key('host-import-completion'),
                    style: TextStyle(color: MassarPalette.of(context).accent),
                  ),
                ),
              if ((_error ?? previewError) case final String error)
                Padding(
                  padding: const EdgeInsets.only(top: 16),
                  child: Text(
                    error,
                    style: TextStyle(color: MassarPalette.of(context).error),
                  ),
                ),
              if (legacy)
                ExpansionTile(
                  title: const Text('امتحانات مرتبطة من النسخ السابقة'),
                  children: [
                    SizedBox(
                      height: 650,
                      child: _LegacyHostExamImportPage(store: store),
                    ),
                  ],
                ),
            ],
          ),
        ),
      );
    },
  );
}

class _LegacyHostExamImportPage extends StatefulWidget {
  const _LegacyHostExamImportPage({required this.store});
  final CenterStore store;
  @override
  State<_LegacyHostExamImportPage> createState() =>
      _LegacyHostExamImportPageState();
}

class _LegacyHostExamImportPageState extends State<_LegacyHostExamImportPage> {
  String? _roomId, _error;
  bool _requesting = false;

  Future<void> _download(ExamMonitorRecord room) async {
    setState(() {
      _requesting = true;
      _error = null;
    });
    try {
      await widget.store.requestExamResultImport(room.id);
    } catch (failure) {
      if (mounted) setState(() => _error = managementError(failure));
    } finally {
      if (mounted) setState(() => _requesting = false);
    }
  }

  String _roomLabel(ExamMonitorRecord room) {
    final snapshot = room.snapshot;
    final session = widget.store.sessionById(snapshot.sessionId);
    final activity = widget.store.academicActivities
        .where((row) => row.id == snapshot.activityId)
        .firstOrNull;
    return '${widget.store.groupLabel(snapshot.groupId)} • حصة ${session?.number ?? ''} • ${activity?.name ?? ''}';
  }

  String _reason(ExamMonitorRecord room, String studentId) {
    if (room.importProblems[studentId] case final String problem) {
      return problem;
    }
    final attempt = room.snapshot.attempts
        .where((attempt) => attempt.studentId == studentId)
        .firstOrNull;
    return switch (attempt?.status) {
      'final' => 'التصحيح مكتمل • جاهز للتحميل',
      'pending_grading' => 'بانتظار تصحيح المقالي',
      'awaiting_close' => 'بانتظار إغلاق الامتحان',
      'cancelled' => 'المحاولة ملغاة • تحتاج مراجعة',
      'not_submitted' => 'دخل ولم يسلّم',
      _ => 'لم يدخل الامتحان',
    };
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: widget.store,
    builder: (context, _) {
      final rooms = widget.store.integratedExamRooms
          .where((room) => room.deviceId != 'local')
          .toList()
          .reversed
          .toList();
      final room =
          rooms.where((room) => room.id == _roomId).firstOrNull ??
          rooms.firstOrNull;
      IntegratedExamProgress? progress;
      String? projectionError;
      if (room != null) {
        try {
          progress = widget.store.integratedExamProgress(
            groupId: room.snapshot.groupId,
            sessionId: room.snapshot.sessionId,
            activityId: room.snapshot.activityId,
          );
        } catch (failure) {
          projectionError = managementError(failure);
        }
      }
      return ManagementPanel(
        title: 'تحميل درجات الساكند',
        subtitle:
            'بعد الامتحان والتصحيح، وصّل الجهازين بالكابل وسجّل الدخول على الساكند. اضغط تحميل؛ تُرصد النتائج النهائية المحفوظة فقط، وتظهر الحالات المتبقية هنا.',
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (room == null)
                const Text(
                  'لم يصل امتحان مرتبط قديم من الساكند بعد. الامتحانات الجديدة لا تحتاج تجهيز كشف قبل القاعة.',
                ),
              if (room != null) ...[
                DropdownButtonFormField<String>(
                  key: ValueKey('exam-import-room:${room.id}'),
                  initialValue: room.id,
                  isExpanded: true,
                  decoration: const InputDecoration(
                    labelText: 'امتحان الساكند',
                  ),
                  items: rooms
                      .map(
                        (row) => DropdownMenuItem(
                          value: row.id,
                          child: Text(
                            _roomLabel(row),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  onChanged: _requesting
                      ? null
                      : (id) => setState(() => _roomId = id),
                ),
                const SizedBox(height: 16),
                Wrap(
                  spacing: 16,
                  runSpacing: 12,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    FilledButton.icon(
                      key: const Key('host-download-exam-grades'),
                      onPressed: _requesting || room.importPending
                          ? null
                          : () => _download(room),
                      icon: const Icon(Icons.download_outlined),
                      label: Text(
                        room.importPending
                            ? 'بانتظار الساكند…'
                            : 'تحميل الدرجات ورصدها',
                      ),
                    ),
                    if (progress != null) ...[
                      Text('الحاضرون: ${progress.attending}'),
                      Text('مرصود: ${progress.recorded}'),
                      Text('بدون درجة: ${progress.missing.length}'),
                    ],
                  ],
                ),
                const SizedBox(height: 12),
                Text(
                  room.importPending
                      ? 'طلب التحميل محفوظ. وصّل الكابل وافتح البرنامج وسجّل الدخول على الساكند ليكتمل.'
                      : room.importCompletedAt != null
                      ? 'اكتمل طلب التحميل. الحالات بدون درجة موضّحة أسفل؛ يمكن إعادة التحميل بعد معالجتها.'
                      : 'آخر كشف مستلم: ${room.receivedAt.toLocal()} • أثناء فصل الكابل لا تصل تحديثات جديدة.',
                ),
                const SizedBox(height: 20),
                if (progress != null && progress.missing.isNotEmpty)
                  SingleChildScrollView(
                    scrollDirection: Axis.horizontal,
                    child: DataTable(
                      columns: const [
                        DataColumn(label: Text('الكود')),
                        DataColumn(label: Text('الطالب')),
                        DataColumn(label: Text('سبب عدم الرصد')),
                      ],
                      rows: progress.missing
                          .map(
                            (student) => DataRow(
                              cells: [
                                DataCell(Text(student.code)),
                                DataCell(Text(student.name)),
                                DataCell(
                                  Text(_reason(room, student.studentId)),
                                ),
                              ],
                            ),
                          )
                          .toList(),
                    ),
                  ),
                if (progress?.missing.isEmpty == true)
                  const Text('كل الحاضرين مرصود لهم درجة.'),
              ],
              if ((_error ?? projectionError) != null)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Text(
                    (_error ?? projectionError)!,
                    style: TextStyle(color: MassarPalette.of(context).error),
                  ),
                ),
            ],
          ),
        ),
      );
    },
  );
}
