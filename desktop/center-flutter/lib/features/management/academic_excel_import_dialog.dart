import 'dart:convert';

import 'package:file_selector/file_selector.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../application/academic_excel_import.dart';
import '../../application/academic_import_command.dart';
import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../domain/student_lookup.dart';
import '../../lan/lan_transport.dart';
import '../../shared/scrollable_dialog.dart';
import '../../shared/performance_trace.dart';
import 'management_widgets.dart';

part 'academic_excel_import_review.dart';

class AcademicExcelFile {
  const AcademicExcelFile(this.name, this.bytes);
  final String name;
  final List<int> bytes;
}

typedef AcademicExcelFileLoader = Future<AcademicExcelFile?> Function();

Future<AcademicExcelFile?> _chooseExcelFile() async {
  final file = await openFile(
    acceptedTypeGroups: [
      const XTypeGroup(label: 'Excel', extensions: ['xlsx']),
    ],
  );
  if (file == null) return null;
  if (await file.length() > AcademicExcelLimits.maxFileBytes) {
    throw const FormatException('حجم الملف أكبر من ٨ ميجابايت.');
  }
  return AcademicExcelFile(file.name, await file.readAsBytes());
}

typedef _MatchInput = ({
  List<AcademicExcelRow> rows,
  List<Student> students,
  String groupId,
});

List<AcademicExcelMatch> _matchRows(_MatchInput input) =>
    matchAcademicExcelRows(
      input.rows,
      students: input.students,
      groupId: input.groupId,
    );

/// A preview owns its scope and expected records until explicitly refreshed.
class AcademicExcelImportDialog extends StatefulWidget {
  const AcademicExcelImportDialog({
    super.key,
    required this.store,
    required this.groupId,
    required this.sessionId,
    required this.activityId,
    this.fileLoader,
  });
  final CenterStore store;
  final String groupId, sessionId, activityId;
  final AcademicExcelFileLoader? fileLoader;

  @override
  State<AcademicExcelImportDialog> createState() =>
      _AcademicExcelImportDialogState();
}

class _AcademicExcelImportDialogState extends State<AcademicExcelImportDialog> {
  AcademicExcelWorkbook? _workbook;
  String? _fileName, _error;
  int _sheet = 0, _revision = 0, _actorRevision = 0;
  bool _busy = false, _saving = false, _stale = false, _unknown = false;
  bool _reviewOnly = false;
  bool _sheetContextReviewed = false;
  bool _uniformContext = false, _previewCairo = false;
  AcademicActivity? _previewActivity;
  String? _previewGroupName;
  late String _observedScope;
  String? _observedActor;
  List<_ReviewedRow> _rows = [];
  Map<String, Student> _students = {};
  Map<String, AcademicRecord> _existing = {};
  Set<String> _present = {};

  StudyGroup? get _group => widget.store.groupById(widget.groupId);
  LessonSession? get _session => widget.store.sessionById(widget.sessionId);
  AcademicActivity? get _activity => widget.store.academicActivities
      .where((activity) => activity.id == widget.activityId)
      .firstOrNull;
  bool get _locked => _busy || _saving;
  String? get _scopeProblem {
    final activity = _activity;
    final session = _session;
    if (!widget.store.canAssess) return 'سجّل الدخول بحساب متاح للرصد.';
    if (widget.store.isRemote && !widget.store.remoteConnected) {
      return 'الاتصال بالرئيسي غير متاح؛ أعد الاتصال وتحديث المعاينة.';
    }
    if (_group == null ||
        session == null ||
        session.groupId != widget.groupId ||
        session.status == SessionStatus.canceled ||
        !widget.store.sessionHasStarted(session.id)) {
      return 'المجموعة أو الحصة لم تعد متاحة للرصد. أغلق الاستيراد وراجع الاختيار.';
    }
    if (activity == null ||
        activity.kind != AcademicActivityKind.exam ||
        !activity.maxScoreKnown ||
        !activity.appliesToSession(session)) {
      return 'اختر امتحانًا باسم ودرجة نهائية لهذه الحصة.';
    }
    return null;
  }

  @override
  void initState() {
    super.initState();
    _observedActor = widget.store.currentUser?.id;
    _observedScope = _scopeFingerprint();
    widget.store.addListener(_storeChanged);
  }

  @override
  void didUpdateWidget(AcademicExcelImportDialog oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.store != widget.store ||
        oldWidget.groupId != widget.groupId ||
        oldWidget.sessionId != widget.sessionId ||
        oldWidget.activityId != widget.activityId) {
      oldWidget.store.removeListener(_storeChanged);
      widget.store.addListener(_storeChanged);
      _revision++;
      _stale = true;
      _rows = [];
      _students = {};
      _existing = {};
      _sheetContextReviewed = false;
      _scopeReadRevision = null;
      _observedReferences = null;
      _observedActor = widget.store.currentUser?.id;
      _observedScope = _scopeFingerprint();
    }
  }

  @override
  void dispose() {
    widget.store.removeListener(_storeChanged);
    super.dispose();
  }

  Object? _scopeReadRevision;
  List<Object?>? _observedReferences;

  void _storeChanged() {
    final readRevision = widget.store.academicImportReadRevision;
    if (_scopeReadRevision == readRevision) return;
    _scopeReadRevision = readRevision;
    final references = _scopeReferences();
    final previous = _observedReferences;
    _observedReferences = references;
    if (previous != null &&
        previous.length == references.length &&
        Iterable<int>.generate(references.length).every(
          (index) => previous[index] is List && references[index] is List
              ? listEquals(previous[index] as List, references[index] as List)
              : previous[index] == references[index],
        )) {
      return;
    }
    final actor = widget.store.currentUser?.id;
    final trace = PerformanceTrace('academic.import.scope', budgetMs: 32);
    final scope = _scopeFingerprint();
    trace.stage('scope');
    trace.finish();
    if (_observedScope == scope && _observedActor == actor) return;
    final changedActor = _observedActor != actor;
    _observedScope = scope;
    _observedActor = actor;
    _revision++;
    if (changedActor) {
      _actorRevision++;
      _rows = [];
      _students = {};
      _existing = {};
      _present = {};
      _sheetContextReviewed = false;
      if (mounted) {
        final importRoute = ModalRoute.of(context);
        if (importRoute?.isActive == true && !importRoute!.isCurrent) {
          Navigator.of(
            context,
          ).popUntil((route) => identical(route, importRoute));
        }
      }
    }
    if (mounted && (!_saving || changedActor)) {
      setState(() => _stale = _workbook != null);
    }
  }

  List<Object?> _scopeReferences() => [
    jsonEncode(widget.store.currentUser?.toJson()),
    widget.store.remoteConnected,
    _group?.name,
    widget.store.isCairoGroup(widget.groupId),
    _session,
    _activity,
    widget.store.academicRosterForGroup(widget.groupId),
    widget.store
        .academicRecordsForSession(widget.sessionId)
        .where((record) => record.activityId == widget.activityId)
        .toList(),
    widget.store.academicAttendanceForSession(widget.sessionId),
  ];

  String _scopeFingerprint() => jsonEncode({
    'actor': widget.store.currentUser?.toJson(),
    'connected': !widget.store.isRemote || widget.store.remoteConnected,
    'groupName': _group?.name,
    'cairo': widget.store.isCairoGroup(widget.groupId),
    'session': _session?.toJson(),
    'activity': _activity?.toJson(),
    'students': widget.store
        .academicRosterForGroup(widget.groupId)
        .map((student) => student.toJson())
        .toList(),
    'records': widget.store
        .academicRecordsForSession(widget.sessionId)
        .where(
          (record) =>
              record.sessionId == widget.sessionId &&
              record.activityId == widget.activityId,
        )
        .map((record) => record.toJson())
        .toList(),
    'attendance': widget.store
        .academicAttendanceForSession(widget.sessionId)
        .map((record) => record.toJson())
        .toList(),
  });

  Future<void> _chooseFile() async {
    if (_locked) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final file = await (widget.fileLoader ?? _chooseExcelFile)();
      if (file == null || !mounted) return;
      if (!file.name.toLowerCase().endsWith('.xlsx')) {
        throw const FormatException('اختر ملف Excel بصيغة .xlsx.');
      }
      if (file.bytes.length > AcademicExcelLimits.maxFileBytes) {
        throw const FormatException('حجم الملف أكبر من ٨ ميجابايت.');
      }
      final trace = PerformanceTrace('academic.import.read', budgetMs: 250);
      final AcademicExcelWorkbook workbook;
      try {
        workbook = await compute(readAcademicExcel, file.bytes);
      } finally {
        trace.stage('decode');
        trace.counts['bytes'] = file.bytes.length;
        trace.finish();
      }
      if (!mounted) return;
      _workbook = workbook;
      _fileName = file.name;
      _sheet = 0;
      _rows = [];
      _sheetContextReviewed = false;
      await _refreshRows();
    } catch (error) {
      if (mounted) setState(() => _error = _errorText(error));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _refreshRows({bool reconnect = false}) async {
    if (_workbook == null || _saving) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      if (reconnect && widget.store.isRemote) {
        await widget.store.refreshRemote();
      }
      final revision = _revision;
      final actorRevision = _actorRevision;
      final activity = _activity;
      final groupName = _group?.name;
      final cairo = widget.store.isCairoGroup(widget.groupId);
      final previous = {for (final row in _rows) row.match.row.rowNumber: row};
      final students = widget.store.students
          .where((student) => student.groupIds.contains(widget.groupId))
          .toList();
      final existing = {
        for (final record in widget.store.academics)
          if (record.sessionId == widget.sessionId &&
              record.activityId == widget.activityId)
            record.studentId: record,
      };
      final trace = PerformanceTrace('academic.import.match', budgetMs: 150);
      final matches = await compute(_matchRows, (
        rows: _workbook!.sheets[_sheet].rows,
        students: students,
        groupId: widget.groupId,
      ));
      trace.stage('match');
      trace.counts['rows'] = matches.length;
      trace.finish();
      if (!mounted) return;
      if (_actorRevision != actorRevision) {
        _stale = true;
        return;
      }
      _students = {for (final student in students) student.id: student};
      _existing = existing;
      _previewActivity = activity;
      _previewGroupName = groupName;
      _previewCairo = cairo;
      _present = {
        for (final attendance in widget.store.attendances)
          if (attendance.sessionId == widget.sessionId &&
              (attendance.status == AttendanceStatus.present ||
                  attendance.status == AttendanceStatus.makeup))
            attendance.studentId,
      };
      _rows = matches.map((match) {
        final before = previous[match.row.rowNumber];
        final selectedId = before?.studentId;
        return _ReviewedRow(
          match,
          studentId:
              before?.chosenManually == true &&
                  selectedId != null &&
                  _students.containsKey(selectedId)
              ? selectedId
              : match.canAutoSelect || match.issues.isEmpty
              ? match.student?.id
              : null,
          ignored: before?.ignored ?? false,
          chosenManually: before?.chosenManually ?? false,
        );
      }).toList();
      _uniformContext =
          _rows
              .map(
                (row) => (
                  row.match.row.metadata['group']?.trim(),
                  row.match.row.metadata['exam']?.trim(),
                ),
              )
              .toSet()
              .length ==
          1;
      _stale = revision != _revision;
      _sheetContextReviewed = false;
      _unknown = false;
    } catch (error) {
      if (mounted) _error = _errorText(error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  List<String> _warnings(_ReviewedRow reviewed) {
    return [
      ...reviewed.match.row.warnings,
      if (!_sheetContextReviewed || !_uniformContext)
        ..._contextWarnings(reviewed),
    ];
  }

  List<String> _contextWarnings(_ReviewedRow reviewed) {
    final row = reviewed.match.row;
    final source = row.metadata;
    final sourceGroup = source['group']?.trim() ?? '';
    final sourceExam = source['exam']?.trim() ?? '';
    return [
      if (sourceGroup.isNotEmpty && sourceGroup != _previewGroupName?.trim())
        'مجموعة الملف «$sourceGroup» تختلف عن المجموعة المختارة «$_previewGroupName».',
      if (sourceExam.isNotEmpty && sourceExam != _previewActivity?.name.trim())
        'امتحان الملف «$sourceExam» يختلف عن الامتحان المختار «${_previewActivity?.name}».',
    ];
  }

  Set<String> get _duplicates {
    final counts = <String, int>{};
    for (final row in _rows) {
      if (!row.ignored && row.studentId != null) {
        counts.update(row.studentId!, (value) => value + 1, ifAbsent: () => 1);
      }
    }
    return counts.keys.where((id) => counts[id]! > 1).toSet();
  }

  _RowState _stateOf(_ReviewedRow reviewed, Set<String> duplicates) {
    if (reviewed.ignored) {
      return const _RowState(_RowKind.ignored, 'تم تجاهل الصف');
    }
    final row = reviewed.match.row;
    if (!row.eligible) {
      return _RowState(_RowKind.invalid, row.errors.join(' · '));
    }
    if (row.maxScore != _previewActivity?.maxScore) {
      return _RowState(
        _RowKind.invalid,
        'النهاية ${row.maxScore ?? '—'} لا تطابق الامتحان ${_previewActivity?.maxScore ?? '—'}.',
      );
    }
    final student = _students[reviewed.studentId];
    if (student == null) {
      return _RowState(
        reviewed.match.suggestions.isEmpty
            ? _RowKind.unmatched
            : _RowKind.review,
        reviewed.match.issues.isEmpty
            ? 'اختر الطالب من المجموعة.'
            : reviewed.match.issues.join(' · '),
      );
    }
    if (duplicates.contains(student.id)) {
      return const _RowState(
        _RowKind.review,
        'أكثر من صف مرتبط بنفس الطالب؛ تجاهل التكرار أو صحح الاختيار.',
      );
    }
    if (!_previewCairo && !_present.contains(student.id)) {
      return const _RowState(
        _RowKind.invalid,
        'الطالب غير حاضر أو معوّض فعليًا في هذه الحصة.',
      );
    }
    if (_previewCairo &&
        student.isSuspended &&
        !_present.contains(student.id)) {
      return const _RowState(
        _RowKind.invalid,
        'الطالب موقوف ولا يوجد له حضور فعلي؛ لا يمكن إثبات حضوره بالاستيراد.',
      );
    }
    if (_warnings(reviewed).isNotEmpty && !reviewed.sourceReviewed) {
      return const _RowState(
        _RowKind.review,
        'راجع تحذيرات بيانات المصدر وأكدها صراحة.',
      );
    }
    final existing = _existing[student.id];
    if (existing != null) {
      if (existing.score == row.score &&
          existing.maxScore == row.maxScore &&
          existing.maxScoreKnown &&
          !existing.examAbsent) {
        return const _RowState(
          _RowKind.unchanged,
          'نفس الدرجة محفوظة؛ سيُتخطى الصف.',
        );
      }
      if (!reviewed.replace) {
        return const _RowState(
          _RowKind.review,
          'توجد درجة سابقة؛ الاستبدال يحتاج موافقة لهذا الصف.',
        );
      }
    }
    return const _RowState(_RowKind.ready, 'جاهز للحفظ');
  }

  Future<void> _review(_ReviewedRow row) async {
    final updated = await showDialog<_RowDecision>(
      context: context,
      builder: (context) => _RowReviewDialog(
        row: row,
        students: _students.values.toList(),
        existing: _existing,
        warnings: _warnings(row),
        destination: '${_group?.name} · ${_activity?.name}',
      ),
    );
    if (!mounted || updated == null || _locked) return;
    setState(() {
      row.studentId = updated.studentId;
      row.chosenManually = updated.studentId != null;
      row.replace = updated.replace;
      row.sourceReviewed = updated.sourceReviewed;
      row.ignored = updated.ignored;
    });
  }

  Future<void> _save() async {
    if (_locked || _stale || _unknown || _scopeProblem != null) return;
    final duplicates = _duplicates;
    final ready = _rows
        .where((row) => _stateOf(row, duplicates).kind == _RowKind.ready)
        .toList();
    if (ready.isEmpty || ready.length > 1000) return;
    final command = AcademicImportCommand(
      groupId: widget.groupId,
      sessionId: widget.sessionId,
      activityId: widget.activityId,
      maxScore: _activity!.maxScore,
      rows: ready.map((reviewed) {
        final row = reviewed.match.row;
        final metadata = row.metadata;
        return AcademicImportRow(
          studentId: reviewed.studentId!,
          score: row.score!,
          maxScore: row.maxScore!,
          expected: _existing[reviewed.studentId],
          source: AcademicImportSource(
            studentName: row.name,
            examName: metadata['exam'],
            sessionId: metadata['externalSessionId'],
            attemptId: metadata['externalAttemptId'],
            version: metadata['sheetVersion'],
            cairoDate: metadata['cairoDate'],
          ),
        );
      }).toList(),
    );
    // Keep one atomic command within the LAN gateway's existing body bound.
    if (utf8
            .encode(
              jsonEncode({
                'requestId': '00000000-0000-0000-0000-000000000000',
                'operation': 'importAcademicGrades',
                'arguments': command.toJson(),
              }),
            )
            .length >
        256 * 1024) {
      setState(
        () => _error =
            'بيانات الدفعة أكبر من الحد المتاح. اختر عددًا أقل من الصفوف؛ لم تُرسل أي درجة.',
      );
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    final actorRevision = _actorRevision;
    try {
      await widget.store.importAcademicGrades(command);
      if (mounted) {
        if (_actorRevision != actorRevision) {
          setState(() {
            _stale = true;
            _error = 'تغيّر الحساب أثناء الحفظ؛ راجع النتيجة بعد تسجيل الدخول.';
          });
        } else {
          Navigator.pop(context, ready.length);
        }
      }
    } catch (error) {
      if (mounted) {
        setState(() {
          _unknown = error is LanConnectionException && error.outcomeUnknown;
          _stale = true;
          _error = _unknown
              ? 'نتيجة الحفظ غير مؤكدة. لا تكرر الإرسال؛ أعد الاتصال وتحديث المعاينة لمراجعة العملية المحفوظة.'
              : _errorText(error);
        });
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  String _errorText(Object error) =>
      error is FormatException ? error.message : error.toString();

  @override
  Widget build(BuildContext context) {
    final duplicates = _duplicates;
    final states = {for (final row in _rows) row: _stateOf(row, duplicates)};
    int count(_RowKind kind) =>
        states.values.where((state) => state.kind == kind).length;
    final ready = count(_RowKind.ready);
    final visible = _rows
        .where(
          (row) =>
              !_reviewOnly ||
              [
                _RowKind.review,
                _RowKind.unmatched,
                _RowKind.invalid,
              ].contains(states[row]!.kind),
        )
        .toList();
    final scopeProblem = _scopeProblem;
    return PopScope(
      canPop: !_saving,
      child: Dialog(
        insetPadding: const EdgeInsets.all(16),
        child: SizedBox(
          width: 1380,
          height: MediaQuery.sizeOf(context).height * .9,
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'استيراد درجات Excel',
                  style: Theme.of(context).textTheme.headlineSmall,
                ),
                const SizedBox(height: 12),
                Expanded(
                  child: ManagementBody(
                    header: [
                      Text(
                        'الوجهة: ${_group?.name ?? '—'} · الحصة ${_session?.number ?? '—'} · ${_activity?.name ?? '—'} من ${_activity?.maxScore ?? '—'}',
                        key: const Key('import-destination'),
                      ),
                      const Text(
                        'J: درجة الطالب · K: المجموع / الامتحان من كام. راجع اسم امتحان المصدر والمجموعة قبل الحفظ.',
                      ),
                      Text(
                        widget.store.isCairoGroup(widget.groupId)
                            ? 'رصد القاهرة يثبت الحضور فقط، بدون دفع أو خصم من الباقة.'
                            : 'يُرصد للحاضرين والمعوّضين فعليًا فقط في المجموعة المختارة.',
                      ),
                      const SizedBox(height: 12),
                      Wrap(
                        spacing: 12,
                        runSpacing: 8,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          OutlinedButton.icon(
                            key: const Key('import-choose-file'),
                            onPressed: _locked || _unknown ? null : _chooseFile,
                            icon: const Icon(Icons.upload_file),
                            label: Text(
                              _fileName == null
                                  ? 'اختيار ملف Excel'
                                  : 'اختيار ملف آخر',
                            ),
                          ),
                          if (_fileName != null) Text(_fileName!),
                          if (_workbook != null)
                            SizedBox(
                              width: 270,
                              child: DropdownButtonFormField<int>(
                                key: ValueKey(
                                  'import-sheet-$_fileName-$_sheet',
                                ),
                                initialValue: _sheet,
                                decoration: const InputDecoration(
                                  labelText: 'ورقة العمل',
                                ),
                                isExpanded: true,
                                items: [
                                  for (
                                    var index = 0;
                                    index < _workbook!.sheets.length;
                                    index++
                                  )
                                    DropdownMenuItem(
                                      value: index,
                                      child: Text(
                                        _workbook!.sheets[index].name,
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                    ),
                                ],
                                onChanged: _locked || _unknown
                                    ? null
                                    : (index) {
                                        if (index == null) return;
                                        setState(() {
                                          _sheet = index;
                                          _rows = [];
                                        });
                                        _refreshRows();
                                      },
                              ),
                            ),
                          if (_workbook != null)
                            OutlinedButton.icon(
                              key: const Key('import-refresh'),
                              onPressed: _locked
                                  ? null
                                  : () => _refreshRows(reconnect: true),
                              icon: const Icon(Icons.refresh),
                              label: Text(
                                _unknown
                                    ? 'إعادة الاتصال ومراجعة النتيجة'
                                    : 'تحديث المعاينة',
                              ),
                            ),
                        ],
                      ),
                      if (_busy || _saving)
                        const Padding(
                          padding: EdgeInsets.symmetric(vertical: 8),
                          child: LinearProgressIndicator(),
                        ),
                      if (_error != null)
                        Padding(
                          padding: const EdgeInsets.symmetric(vertical: 8),
                          child: Text(
                            _error!,
                            key: const Key('import-error'),
                            style: TextStyle(
                              color: Theme.of(context).colorScheme.error,
                            ),
                          ),
                        ),
                      if (scopeProblem != null)
                        Text(
                          scopeProblem,
                          key: const Key('import-scope-error'),
                        ),
                      if (_stale)
                        const Text(
                          'تغيّرت البيانات؛ حدّث المعاينة وأعد مراجعة الاستبدالات قبل الحفظ.',
                          key: Key('import-stale'),
                        ),
                      if (_rows.isNotEmpty) ...[
                        const SizedBox(height: 12),
                        if (_uniformContext &&
                            _contextWarnings(_rows.first).isNotEmpty)
                          CheckboxListTile(
                            key: const Key('import-context-reviewed'),
                            contentPadding: EdgeInsets.zero,
                            title: Text(
                              '${_contextWarnings(_rows.first).join(' ')} أؤكد رصد هذه الورقة في الوجهة المختارة.',
                            ),
                            value: _sheetContextReviewed,
                            onChanged: _locked
                                ? null
                                : (value) => setState(
                                    () =>
                                        _sheetContextReviewed = value ?? false,
                                  ),
                          ),
                        for (final warning in _workbook?.warnings ?? <String>[])
                          Text(warning),
                        Text(
                          'جاهز: $ready · يحتاج مراجعة: ${count(_RowKind.review)} · غير مطابق: ${count(_RowKind.unmatched)} · غير صالح: ${count(_RowKind.invalid)} · محفوظ كما هو: ${count(_RowKind.unchanged)} · متجاهل: ${count(_RowKind.ignored)}',
                          key: const Key('import-counts'),
                        ),
                        const Text(
                          'سيُحفظ الجاهز فقط. الصفوف التي تحتاج مراجعة أو غير الصالحة لن تُرسل.',
                        ),
                        FilterChip(
                          label: const Text('تحتاج مراجعة فقط'),
                          selected: _reviewOnly,
                          onSelected: _locked
                              ? null
                              : (value) => setState(() => _reviewOnly = value),
                        ),
                        const SizedBox(height: 8),
                      ],
                    ],
                    child: _rows.isEmpty
                        ? const EmptySection(
                            message:
                                'اختر ملف .xlsx لعرض الصفوف ومطابقتها مع طلبة المجموعة، دون حفظ تلقائي.',
                          )
                        : ManagementTable.builder(
                            key: ValueKey('import-table-$_sheet-$_reviewOnly'),
                            columns: const [
                              'الصف',
                              'بيانات الملف',
                              'الدرجة J / K',
                              'طالب السنتر',
                              'المحفوظ سابقًا',
                              'الحالة والمراجعة',
                            ],
                            rowCount: visible.length,
                            rowBuilder: (index) {
                              final reviewed = visible[index];
                              final row = reviewed.match.row;
                              final student = _students[reviewed.studentId];
                              final previous = _existing[reviewed.studentId];
                              final state = states[reviewed]!;
                              return DataRow(
                                cells: [
                                  DataCell(
                                    Row(
                                      mainAxisSize: MainAxisSize.min,
                                      children: [
                                        Checkbox(
                                          key: ValueKey(
                                            'import-include-${row.rowNumber}',
                                          ),
                                          value: !reviewed.ignored,
                                          onChanged: _locked
                                              ? null
                                              : (value) => setState(
                                                  () => reviewed.ignored =
                                                      !(value ?? false),
                                                ),
                                        ),
                                        Text('${row.rowNumber}'),
                                      ],
                                    ),
                                  ),
                                  DataCell(
                                    SizedBox(
                                      width: 220,
                                      child: Text(
                                        '${row.name}\nكود ${row.code} · ${row.phone}',
                                        maxLines: 3,
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                    ),
                                  ),
                                  DataCell(
                                    Text(
                                      '${row.score ?? '—'} / ${row.maxScore ?? '—'}',
                                      textDirection: TextDirection.ltr,
                                    ),
                                  ),
                                  DataCell(
                                    SizedBox(
                                      width: 240,
                                      child: Tooltip(
                                        message: student == null
                                            ? reviewed.match.suggestions
                                                  .map(
                                                    (suggestion) =>
                                                        '${suggestion.student.name}: ${suggestion.reason}',
                                                  )
                                                  .join('\n')
                                            : '${student.name} · ${student.code} · ${student.phone}',
                                        child: Text(
                                          student == null
                                              ? reviewed
                                                        .match
                                                        .suggestions
                                                        .isEmpty
                                                    ? 'لم يُحدد طالب'
                                                    : 'اقتراحات: ${reviewed.match.suggestions.map((suggestion) => suggestion.student.name).join('، ')}'
                                              : '${student.name}\nكود ${student.code} · ${student.phone}',
                                          maxLines: 3,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ),
                                    ),
                                  ),
                                  DataCell(
                                    Text(
                                      previous == null
                                          ? 'لا توجد'
                                          : previous.examAbsent
                                          ? 'غائب'
                                          : '${previous.score ?? '—'} / ${previous.maxScore}',
                                    ),
                                  ),
                                  DataCell(
                                    SizedBox(
                                      width: 260,
                                      child: Row(
                                        children: [
                                          Expanded(
                                            child: Tooltip(
                                              message: state.message,
                                              child: Text(
                                                state.message,
                                                maxLines: 2,
                                                overflow: TextOverflow.ellipsis,
                                              ),
                                            ),
                                          ),
                                          TextButton(
                                            key: ValueKey(
                                              'import-review-${row.rowNumber}',
                                            ),
                                            onPressed: _locked
                                                ? null
                                                : () => _review(reviewed),
                                            child: const Text('مراجعة'),
                                          ),
                                        ],
                                      ),
                                    ),
                                  ),
                                ],
                              );
                            },
                          ),
                  ),
                ),
                const SizedBox(height: 12),
                Wrap(
                  alignment: WrapAlignment.end,
                  spacing: 12,
                  runSpacing: 8,
                  children: [
                    if (ready > 1000)
                      const Text(
                        'الحد الأقصى ١٠٠٠ درجة في الدفعة؛ تجاهل الصفوف الزائدة.',
                      ),
                    TextButton(
                      onPressed: _saving ? null : () => Navigator.pop(context),
                      child: const Text('إغلاق'),
                    ),
                    FilledButton.icon(
                      key: const Key('import-save'),
                      onPressed:
                          _locked ||
                              _stale ||
                              _unknown ||
                              scopeProblem != null ||
                              ready == 0 ||
                              ready > 1000
                          ? null
                          : _save,
                      icon: const Icon(Icons.save_outlined),
                      label: Text(
                        _saving ? 'جارٍ حفظ الدفعة…' : 'حفظ $ready درجة جاهزة',
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

enum _RowKind { ready, review, unmatched, invalid, unchanged, ignored }

class _RowState {
  const _RowState(this.kind, this.message);
  final _RowKind kind;
  final String message;
}

class _ReviewedRow {
  _ReviewedRow(
    this.match, {
    this.studentId,
    this.ignored = false,
    this.chosenManually = false,
  });
  final AcademicExcelMatch match;
  String? studentId;
  bool ignored, chosenManually, replace = false, sourceReviewed = false;
}

typedef _RowDecision = ({
  String? studentId,
  bool ignored,
  bool replace,
  bool sourceReviewed,
});
