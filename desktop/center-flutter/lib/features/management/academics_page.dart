import 'package:massar_center/shared/student_lookup_dialog.dart';
import 'package:massar_center/domain/student_lookup.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/shared/notice_dialog.dart'
    show hasPendingMassarNotice, showMassarNotice, NoticeKind;
import 'package:massar_center/shared/theme.dart';

import 'academic_quick_entry.dart';
import 'academic_excel_import_dialog.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';

import 'management_widgets.dart';

class AcademicsPage extends StatefulWidget {
  const AcademicsPage({super.key, required this.store, this.cairo = false});
  final CenterStore store;
  final bool cairo;
  @override
  State<AcademicsPage> createState() => _AcademicsPageState();
}

class _AcademicsPageState extends State<AcademicsPage> {
  String? _groupId, _sessionId, _activityId, _monthId, _preparedLessonId;
  final _search = TextEditingController();
  final _codeFocus = FocusNode();
  bool _editing = false;
  String? _quickStudentId, _lastSaved;
  bool _homeworkDefaultsQueued = false;
  (String, String)? _failedHomeworkScope;
  _AcademicScopeSnapshot? _cachedScope;

  @override
  void initState() {
    super.initState();
    _codeFocus.onKeyEvent = _codeKey;
    widget.store.addListener(_storeChanged);
    _monthId = widget.store.studyMonths.firstOrNull?.id;
    _preparedLessonId = _month?.lessons.firstOrNull?.id;
    _activityId = _activitiesFor(null).firstOrNull?.id;
  }

  @override
  void didUpdateWidget(AcademicsPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.store != widget.store) {
      oldWidget.store.removeListener(_storeChanged);
      widget.store.addListener(_storeChanged);
      _cachedScope = null;
    }
    if (oldWidget.cairo != widget.cairo) _cachedScope = null;
  }

  StudyMonth? get _month => widget.store.studyMonths
      .where((month) => month.id == _monthId)
      .firstOrNull;
  PreparedLesson? get _preparedLesson => _month?.lessons
      .where((lesson) => lesson.id == _preparedLessonId)
      .firstOrNull;
  LessonSession? get _selectedSession {
    final session = _monthId == null
        ? widget.store.sessions
              .where((session) => session.id == _sessionId)
              .firstOrNull
        : _groupId == null || _preparedLessonId == null
        ? null
        : widget.store.sessionForPreparedLesson(_groupId!, _preparedLessonId!);
    return session != null &&
            session.status != SessionStatus.canceled &&
            widget.store.sessionHasStarted(session.id)
        ? session
        : null;
  }

  KeyEventResult _codeKey(FocusNode node, KeyEvent event) {
    if (event.logicalKey != LogicalKeyboardKey.enter &&
        event.logicalKey != LogicalKeyboardKey.numpadEnter) {
      return KeyEventResult.ignored;
    }
    final keyboard = HardwareKeyboard.instance;
    if (event is KeyDownEvent &&
        !keyboard.isControlPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        !keyboard.isShiftPressed &&
        _selectedSession != null) {
      _submit(_selectedSession!);
    }
    return KeyEventResult.handled;
  }

  @override
  void dispose() {
    widget.store.removeListener(_storeChanged);
    _search.dispose();
    _codeFocus.dispose();
    super.dispose();
  }

  _AcademicScopeSnapshot _scopeFor(LessonSession? session) {
    final scope = (
      cairo: widget.cairo,
      groupId: _groupId,
      sessionId: session?.id,
      activityId: _activityId,
    );
    if (_cachedScope?.scope != scope) {
      _cachedScope = _AcademicScopeSnapshot(widget.store, scope, session);
    }
    return _cachedScope!;
  }

  List<Student> _roster(LessonSession session) => _scopeFor(session).roster;

  List<Student> _matches(List<Student> roster) => _search.text.trim().isEmpty
      ? roster
      : studentLookupCandidates(roster, _search.text);

  void _focusCode() => WidgetsBinding.instance.addPostFrameCallback((_) {
    if (!mounted ||
        _editing ||
        hasPendingMassarNotice(context) ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    _codeFocus.requestFocus();
    _search.selection = TextSelection(
      baseOffset: 0,
      extentOffset: _search.text.length,
    );
  });

  Future<void> _submit(LessonSession session) async {
    if (_editing ||
        hasPendingMassarNotice(context) ||
        ModalRoute.of(context)?.isCurrent != true ||
        !widget.store.canAssess ||
        session.id != _selectedSession?.id ||
        !_canRecordFor(session.id)) {
      return;
    }
    final roster = _roster(session);
    final query = _search.text.trim();
    if (query.isEmpty) return;
    final matches = studentLookupCandidates(roster, query);
    if (matches.length == 1) return _recordCode(matches.single, session);
    if (matches.length > 1) {
      final activityId = _activityId;
      setState(() => _editing = true);
      final selected = await chooseMatchingStudent(context, matches);
      if (!mounted) return;
      setState(() => _editing = false);
      if (selected != null &&
          session.id == _selectedSession?.id &&
          activityId == _activityId &&
          _canRecordFor(session.id) &&
          widget.store.canAssess) {
        return _recordCode(selected, session);
      }
      _focusCode();
      return;
    }
    showManagementMessage(
      context,
      'لا يوجد طالب بهذا الكود أو الباركود أو الاسم حاضر أو معوّض فعليًا في هذه الحصة.',
      kind: NoticeKind.warning,
    );
    _focusCode();
  }

  void _storeChanged() {
    _cachedScope = null;
    if (mounted) setState(() {});
  }

  void _queueHomeworkDefaults(
    LessonSession? session,
    AcademicActivity? activity,
  ) {
    if (widget.cairo ||
        session == null ||
        activity?.kind != AcademicActivityKind.homework ||
        _editing ||
        _homeworkDefaultsQueued ||
        !widget.store.canAssess ||
        ModalRoute.of(context)?.isCurrent != true ||
        hasPendingMassarNotice(context)) {
      return;
    }
    final scope = (session.id, activity!.id);
    if (_failedHomeworkScope == scope) return;
    final reviewed = _scopeFor(session).records.values
        .where((r) => r.homework != HomeworkStatus.notReviewed)
        .map((r) => r.studentId)
        .toSet();
    if (!_roster(session).any((s) => !reviewed.contains(s.id))) return;
    _homeworkDefaultsQueued = true;
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      _homeworkDefaultsQueued = false;
      if (!mounted ||
          _editing ||
          _selectedSession?.id != scope.$1 ||
          _activity?.id != scope.$2 ||
          ModalRoute.of(context)?.isCurrent != true ||
          hasPendingMassarNotice(context)) {
        return;
      }
      await _recordHomework(session, automatic: true);
    });
  }

  Future<void> _recordCode(Student student, LessonSession session) async {
    if (_activity?.kind == AcademicActivityKind.homework) {
      await _recordHomework(session, missingStudent: student);
    } else {
      await _edit(student, session);
    }
  }

  Future<void> _importExcel(
    LessonSession session,
    AcademicActivity activity,
  ) async {
    if (_editing || !widget.store.canAssess) return;
    setState(() => _editing = true);
    final saved = await showDialog<int>(
      context: context,
      barrierDismissible: false,
      builder: (context) => AcademicExcelImportDialog(
        store: widget.store,
        groupId: session.groupId,
        sessionId: session.id,
        activityId: activity.id,
      ),
    );
    if (!mounted) return;
    setState(() {
      _editing = false;
      if (saved != null) _lastSaved = 'تم استيراد $saved درجة من Excel.';
    });
    _focusCode();
  }

  Future<void> _recordHomework(
    LessonSession session, {
    Student? missingStudent,
    bool automatic = false,
  }) async {
    final activity = _activity;
    if (_editing ||
        !widget.store.canAssess ||
        activity?.kind != AcademicActivityKind.homework ||
        session.id != _selectedSession?.id) {
      return;
    }
    setState(() => _editing = true);
    try {
      await widget.store.recordHomeworkExceptions(
        sessionId: session.id,
        activityId: activity!.id,
        missingStudentId: missingStudent?.id,
      );
      if (!mounted) return;
      setState(() {
        if (!automatic) _search.clear();
        _failedHomeworkScope = null;
        _lastSaved = missingStudent == null
            ? 'تم رصد الحاضرين: اتعمل. أدخل كود اللي ماعملش؛ الاستثناءات السابقة محفوظة.'
            : '${missingStudent.name} · ${missingStudent.code}: ما اتعملش';
      });
    } catch (error) {
      if (automatic) _failedHomeworkScope = (session.id, activity!.id);
      if (mounted) {
        showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) {
        setState(() => _editing = false);
        if (!automatic) _focusCode();
      }
    }
  }

  List<AcademicActivity> _activitiesFor(String? sessionId) {
    if (_preparedLessonId != null && _monthId != null) {
      final activities = widget.store.academicActivities
          .where(
            (activity) =>
                activity.preparedLessonId == _preparedLessonId &&
                (!widget.cairo || activity.kind == AcademicActivityKind.exam),
          )
          .toList();
      if (sessionId != null) {
        activities.addAll(
          widget.store.academicActivities.where(
            (activity) =>
                activity.preparedLessonId == null &&
                activity.sessionId == sessionId &&
                (!widget.cairo || activity.kind == AcademicActivityKind.exam),
          ),
        );
      }
      return activities;
    }
    return sessionId == null
        ? []
        : widget.store
              .academicActivitiesFor(sessionId)
              .where(
                (a) => !widget.cairo || a.kind == AcademicActivityKind.exam,
              )
              .toList();
  }

  bool _canRecordFor(String sessionId) => _activityId == null
      ? widget.store.academics.any(
          (record) =>
              record.sessionId == sessionId && record.activityId == null,
        )
      : _activitiesFor(sessionId).any((activity) => activity.id == _activityId);

  AcademicActivity? get _activity => _activitiesFor(
    _selectedSession?.id,
  ).where((activity) => activity.id == _activityId).firstOrNull;

  Future<void> _createActivity(
    LessonSession? session,
    AcademicActivityKind kind, {
    AcademicActivity? existing,
  }) async {
    if (_editing ||
        hasPendingMassarNotice(context) ||
        ModalRoute.of(context)?.isCurrent != true ||
        !widget.store.canAssess ||
        (session == null && _preparedLesson == null) ||
        session?.status == SessionStatus.canceled) {
      return;
    }
    setState(() => _editing = true);
    final name = TextEditingController(text: existing?.name);
    final maximum = TextEditingController(
      text: existing == null
          ? '10'
          : existing.maxScoreKnown
          ? '${existing.maxScore}'
          : '',
    );
    AcademicActivity? created;
    final exam = kind == AcademicActivityKind.exam;
    await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => ManagementEditor(
        saveLabel: existing != null
            ? 'حفظ الدرجة النهائية'
            : exam
            ? 'إضافة امتحان'
            : 'إضافة واجب',
        title: existing != null
            ? 'تحديد الدرجة النهائية — ${existing.name}'
            : exam
            ? 'إضافة امتحان — ${_preparedLesson?.name ?? 'حصة ${session!.number}'}'
            : 'إضافة واجب — ${_preparedLesson?.name ?? 'حصة ${session!.number}'}',
        controllers: [name, maximum],
        fields: [
          TextFormField(
            key: const Key('academic-activity-name'),
            controller: name,
            autofocus: true,
            maxLength: 120,
            decoration: InputDecoration(
              labelText: exam ? 'اسم الامتحان' : 'اسم الواجب',
            ),
            validator: requiredText,
          ),
          if (exam)
            TextFormField(
              key: const Key('academic-activity-max'),
              controller: maximum,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'الدرجة النهائية'),
              validator: (text) => (parseAcademicInteger(text ?? '') ?? 0) > 0
                  ? null
                  : 'الدرجة النهائية عدد صحيح أكبر من صفر',
            ),
          Text(
            _preparedLesson != null
                ? '${_month!.name} · ${_preparedLesson!.name} · النشاط متاح لكل مجموعة تبدأ هذه الحصة.'
                : 'الحصة ${session!.number} · ${widget.store.groupLabel(session.groupId)}',
          ),
        ],
        onSave: () async {
          created = await widget.store.saveAcademicActivity(
            AcademicActivity(
              id: existing?.id ?? '',
              sessionId:
                  existing?.sessionId ??
                  (_preparedLesson == null ? session!.id : ''),
              preparedLessonId: existing != null
                  ? existing.preparedLessonId
                  : _preparedLesson?.id,
              kind: kind,
              name: name.text.trim(),
              maxScore: exam ? parseAcademicInteger(maximum.text)! : 10,
              createdAt: existing?.createdAt ?? DateTime.now(),
            ),
          );
        },
      ),
    );
    if (!mounted) return;
    setState(() {
      _editing = false;
      if (created != null) {
        _activityId = created!.id;
        _search.clear();
        _lastSaved = null;
      }
    });
    _focusCode();
  }

  Future<void> _edit(Student student, LessonSession session) async {
    if (_editing ||
        hasPendingMassarNotice(context) ||
        ModalRoute.of(context)?.isCurrent != true ||
        !widget.store.canAssess ||
        session.id != _selectedSession?.id ||
        !_canRecordFor(session.id)) {
      return;
    }
    if (_activity == null) return _editDetailed(student, session);
    setState(() {
      _editing = true;
      _quickStudentId = student.id;
      _lastSaved = null;
    });
  }

  void _finishQuick({required bool saved}) {
    if (!mounted) return;
    final record = widget.store.academics
        .where(
          (record) =>
              record.studentId == _quickStudentId &&
              record.sessionId == _selectedSession?.id &&
              record.activityId == _activityId,
        )
        .firstOrNull;
    final student = widget.store.students
        .where((student) => student.id == _quickStudentId)
        .firstOrNull;
    setState(() {
      if (saved && record != null && student != null) {
        final result = _activity?.kind == AcademicActivityKind.exam
            ? record.maxScoreKnown
                  ? '${record.score} / ${record.maxScore}'
                  : '${record.score} · الدرجة النهائية غير معروفة'
            : _quickHomeworkLabel(record.homework);
        _lastSaved = 'تم رصد ${student.name} · ${student.code}: $result';
        _search.clear();
      }
      _quickStudentId = null;
      _editing = false;
    });
    _focusCode();
  }

  Future<void> _quickDetails(Student student, LessonSession session) async {
    setState(() {
      _quickStudentId = null;
      _editing = false;
    });
    await _editDetailed(student, session);
  }

  String _quickHomeworkLabel(HomeworkStatus status) => switch (status) {
    HomeworkStatus.complete => 'اتعمل',
    HomeworkStatus.missing => 'ما اتعملش',
    HomeworkStatus.incomplete => 'ناقص',
    _ => homeworkLabel(status),
  };

  Future<void> _editDetailed(Student student, LessonSession session) async {
    if (_editing ||
        hasPendingMassarNotice(context) ||
        ModalRoute.of(context)?.isCurrent != true ||
        !widget.store.canAssess ||
        session.id != _selectedSession?.id ||
        !_canRecordFor(session.id)) {
      return;
    }
    final activity = _activity;
    setState(() => _editing = true);
    final record = widget.store.academics
        .where(
          (record) =>
              record.studentId == student.id &&
              record.sessionId == session.id &&
              record.activityId == activity?.id,
        )
        .firstOrNull;
    final legacy = activity == null;
    final exam = activity?.kind == AcademicActivityKind.exam;
    if (exam && !activity!.maxScoreKnown) {
      setState(() => _editing = false);
      await showMassarNotice(
        context,
        'حدد الدرجة النهائية للامتحان أولًا من زر «حدد الدرجة النهائية». الدرجة المستوردة محفوظة.',
        kind: NoticeKind.warning,
      );
      _focusCode();
      return;
    }
    var homework = record?.homework ?? HomeworkStatus.notReviewed;
    var examAbsent = record?.examAbsent ?? false;
    final score = TextEditingController(text: record?.score?.toString() ?? '');
    final maxScore = TextEditingController(
      text: record?.maxScoreKnown == false && legacy
          ? ''
          : '${activity?.maxScore ?? record?.maxScore ?? 10}',
    );
    final notes = TextEditingController(text: record?.notes);
    final saved = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (context) => StatefulBuilder(
        builder: (context, update) => ManagementEditor(
          controllers: [score, maxScore, notes],
          saveLabel: 'حفظ الرصد',
          hasUnsavedChanges: examAbsent != (record?.examAbsent ?? false),
          title: legacy
              ? 'رصد ${student.name} — حصة ${session.number}'
              : 'رصد ${exam ? 'امتحان' : 'واجب'}: ${activity.name} — ${student.name}',
          fields: [
            if (legacy || !exam)
              DropdownButtonFormField<HomeworkStatus>(
                initialValue: homework,
                autofocus: !legacy,
                decoration: const InputDecoration(labelText: 'الواجب'),
                items: HomeworkStatus.values
                    .map(
                      (status) => DropdownMenuItem(
                        value: status,
                        child: Text(homeworkLabel(status)),
                      ),
                    )
                    .toList(),
                onChanged: (status) => homework = status!,
              ),
            if (legacy || exam) ...[
              TextFormField(
                key: const Key('academic-result-max'),
                controller: maxScore,
                readOnly: !legacy,
                keyboardType: TextInputType.number,
                decoration: InputDecoration(
                  labelText: 'الدرجة النهائية',
                  helperText: legacy ? null : 'محددة عند إنشاء الامتحان.',
                ),
                validator: (text) => (parseAcademicInteger(text ?? '') ?? 0) > 0
                    ? null
                    : 'الدرجة النهائية عدد صحيح أكبر من صفر',
              ),
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('غائب عن الامتحان'),
                value: examAbsent,
                onChanged: (absent) => update(() {
                  examAbsent = absent!;
                  if (examAbsent) score.clear();
                }),
              ),
              TextFormField(
                controller: score,
                autofocus: exam,
                enabled: !examAbsent,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: const InputDecoration(
                  labelText: 'درجة الطالب',
                  helperText: 'اتركها فارغة لو لم تُرصد بعد. الصفر درجة فعلية.',
                ),
                validator: (text) {
                  if (examAbsent || text == null || text.trim().isEmpty) {
                    return null;
                  }
                  final grade = parseAcademicScore(text),
                      maximum = parseAcademicInteger(maxScore.text);
                  return grade == null ||
                          grade < 0 ||
                          maximum == null ||
                          grade > maximum
                      ? 'الدرجة من صفر إلى الدرجة النهائية'
                      : null;
                },
              ),
            ],
            TextFormField(
              controller: notes,
              maxLines: 3,
              decoration: InputDecoration(
                labelText: legacy
                    ? 'ملاحظات / اسم الامتحان أو الواجب'
                    : 'ملاحظات الرصد',
              ),
            ),
          ],
          onSave: () => widget.store.saveAcademic(
            AcademicRecord(
              id: record?.id ?? '',
              studentId: student.id,
              sessionId: session.id,
              activityId: activity?.id,
              homework: legacy || !exam ? homework : HomeworkStatus.notReviewed,
              score:
                  (legacy || exam) &&
                      !examAbsent &&
                      score.text.trim().isNotEmpty
                  ? parseAcademicScore(score.text)!
                  : null,
              maxScore: legacy || exam
                  ? parseAcademicInteger(maxScore.text)!
                  : 10,
              examAbsent: (legacy || exam) && examAbsent,
              notes: notes.text.trim(),
              updatedAt: DateTime.now(),
            ),
          ),
        ),
      ),
    );
    if (!mounted) return;
    setState(() {
      _editing = false;
      if (saved == true) _search.clear();
    });
    _focusCode();
  }

  Future<void> _prepareCairoSession() async {
    if (_editing || _groupId == null || _preparedLessonId == null) return;
    setState(() => _editing = true);
    try {
      await widget.store.startPreparedLesson(
        groupId: _groupId!,
        preparedLessonId: _preparedLessonId!,
      );
    } catch (error) {
      if (mounted) {
        await showMassarNotice(
          context,
          error.toString(),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _editing = false);
    }
    _focusCode();
  }

  void _resetEntry() {
    _search.clear();
    _lastSaved = null;
    _quickStudentId = null;
  }

  void _changeScope(VoidCallback change, {bool focusCode = false}) {
    if (_editing ||
        hasPendingMassarNotice(context) ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    setState(() {
      _failedHomeworkScope = null;
      change();
    });
    if (focusCode) _focusCode();
  }

  Widget _scopeSelectors(List<LessonSession> sessions) => Wrap(
    spacing: 12,
    runSpacing: 12,
    children: [
      SizedBox(
        width: 280,
        child: DropdownButtonFormField<String>(
          key: ValueKey('academic-month-$_monthId'),
          initialValue: _month?.id,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'الشهر المشترك'),
          items: [
            const DropdownMenuItem(
              value: null,
              child: Text('سجل الحصص السابق'),
            ),
            ...widget.store.studyMonths.map(
              (month) =>
                  DropdownMenuItem(value: month.id, child: Text(month.name)),
            ),
          ],
          onChanged: _editing
              ? null
              : (id) => _changeScope(() {
                  _monthId = id;
                  _preparedLessonId = _month?.lessons.firstOrNull?.id;
                  _sessionId = null;
                  _activityId = _activitiesFor(
                    _selectedSession?.id,
                  ).firstOrNull?.id;
                  _resetEntry();
                }),
        ),
      ),
      if (_month != null)
        SizedBox(
          width: 340,
          child: DropdownButtonFormField<String>(
            key: ValueKey('academic-prepared-$_monthId-$_preparedLessonId'),
            initialValue: _preparedLesson?.id,
            isExpanded: true,
            decoration: const InputDecoration(
              labelText: 'الحصة المجهزة لكل المجموعات',
            ),
            items: _month!.lessons
                .map(
                  (lesson) => DropdownMenuItem(
                    value: lesson.id,
                    child: Text('${lesson.number} · ${lesson.name}'),
                  ),
                )
                .toList(),
            onChanged: _editing
                ? null
                : (id) => _changeScope(() {
                    _preparedLessonId = id;
                    _activityId = _activitiesFor(
                      _selectedSession?.id,
                    ).firstOrNull?.id;
                    _resetEntry();
                  }),
          ),
        ),
      SizedBox(
        width: 300,
        child: DropdownButtonFormField<String>(
          key: ValueKey('academic-group-$_groupId'),
          initialValue:
              widget.store
                  .groupsForRegion(cairo: widget.cairo)
                  .any((group) => group.id == _groupId)
              ? _groupId
              : null,
          isExpanded: true,
          decoration: const InputDecoration(
            labelText: 'المجموعة التي بدأت الحصة',
          ),
          items: [
            const DropdownMenuItem(
              value: null,
              child: Text('اختر المجموعة للرصد'),
            ),
            ...widget.store
                .groupsForRegion(cairo: widget.cairo)
                .map(
                  (group) => DropdownMenuItem(
                    value: group.id,
                    child: Text(widget.store.groupLabel(group.id)),
                  ),
                ),
          ],
          onChanged: _editing
              ? null
              : (id) => _changeScope(() {
                  _groupId = id;
                  if (_monthId == null) {
                    _sessionId = null;
                    _activityId = null;
                  } else if (!_activitiesFor(
                    _selectedSession?.id,
                  ).any((activity) => activity.id == _activityId)) {
                    _activityId = _activitiesFor(
                      _selectedSession?.id,
                    ).firstOrNull?.id;
                  }
                  _resetEntry();
                }),
        ),
      ),
      if (_monthId == null)
        SizedBox(
          width: 550,
          child: DropdownButtonFormField<String>(
            key: ValueKey('academic-session-$_groupId-$_sessionId'),
            initialValue: sessions.any((session) => session.id == _sessionId)
                ? _sessionId
                : null,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'الحصة القديمة للرصد'),
            items: sessions
                .map(
                  (session) => DropdownMenuItem(
                    value: session.id,
                    child: Text(
                      '${sessionLabel(session)} — ${widget.store.groupLabel(session.groupId)} — ${sessionDateLabel(session)}',
                    ),
                  ),
                )
                .toList(),
            onChanged: _editing
                ? null
                : (id) {
                    _changeScope(() {
                      _sessionId = id;
                      _activityId = _activitiesFor(id).firstOrNull?.id;
                      _resetEntry();
                    }, focusCode: true);
                  },
          ),
        ),
      if (_preparedLesson != null &&
          _groupId != null &&
          _selectedSession == null)
        if (widget.cairo)
          FilledButton.icon(
            onPressed: _editing ? null : _prepareCairoSession,
            icon: const Icon(Icons.play_arrow),
            label: const Text('فتح الحصة للرصد'),
          )
        else
          const Text(
            'المجموعة لم تبدأ هذه الحصة بعد. يمكنك تجهيز النشاط الآن، والرصد متاح بعد الحضور الفعلي.',
          ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    final selected = _selectedSession;
    final snapshot = _scopeFor(selected);
    final sessions = snapshot.sessions;
    final activities = _activitiesFor(selected?.id);
    final activity = _activity;
    _queueHomeworkDefaults(selected, activity);
    final legacy = activity == null;
    final exam = activity?.kind == AcademicActivityKind.exam;
    final legacyAvailable = snapshot.legacyAvailable;
    final roster = snapshot.roster;
    final students = _matches(roster);
    final quickStudent = widget.store.students
        .where((student) => student.id == _quickStudentId)
        .firstOrNull;
    final records = snapshot.records;
    final attendance = snapshot.attendance;
    return ManagementPanel(
      title: widget.cairo
          ? 'حضور مجموعات القاهرة'
          : 'الامتحانات والواجبات — إسكندرية',
      subtitle: widget.cairo
          ? 'اختر المجموعة والحصة والامتحان. رصد الدرجة يثبت الحضور تلقائيًا، بدون تحصيل أو خصم من الباقة.'
          : 'جهّز الامتحان أو الواجب للحصة المشتركة، ثم اختر المجموعة لرصد الحاضرين: الكود ثم Enter، الدرجة ثم Enter.',
      child: ManagementBody(
        header: [
          _scopeSelectors(sessions),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              FilledButton.icon(
                key: const Key('add-academic-exam'),
                onPressed:
                    _preparedLesson != null &&
                        !_editing &&
                        widget.store.canAssess
                    ? () => _createActivity(selected, AcademicActivityKind.exam)
                    : null,
                icon: const Icon(Icons.quiz_outlined),
                label: const Text('إضافة امتحان'),
              ),
              if (!widget.cairo)
                OutlinedButton.icon(
                  key: const Key('add-academic-homework'),
                  onPressed:
                      _preparedLesson != null &&
                          !_editing &&
                          widget.store.canAssess
                      ? () => _createActivity(
                          selected,
                          AcademicActivityKind.homework,
                        )
                      : null,
                  icon: const Icon(Icons.assignment_outlined),
                  label: const Text('إضافة واجب'),
                ),
              Tooltip(
                message:
                    'اختر المجموعة وحصة بدأت وامتحانًا باسم ودرجة نهائية أولًا.',
                child: OutlinedButton.icon(
                  key: const Key('academic-import-excel'),
                  onPressed:
                      selected != null &&
                          activity?.kind == AcademicActivityKind.exam &&
                          activity?.maxScoreKnown == true &&
                          !_editing &&
                          widget.store.canAssess
                      ? () => _importExcel(selected, activity!)
                      : null,
                  icon: const Icon(Icons.upload_file),
                  label: const Text('استيراد Excel'),
                ),
              ),
              if ((selected != null || _preparedLesson != null) &&
                  activities.isEmpty)
                const Text('أضف امتحانًا أو واجبًا باسم لهذه الحصة.'),
              if ((selected != null || _preparedLesson != null) &&
                  activity?.kind == AcademicActivityKind.exam &&
                  activity?.maxScoreKnown == false)
                OutlinedButton.icon(
                  key: const Key('resolve-academic-max'),
                  onPressed: !_editing && widget.store.canAssess
                      ? () => _createActivity(
                          selected,
                          AcademicActivityKind.exam,
                          existing: activity,
                        )
                      : null,
                  icon: const Icon(Icons.edit_outlined),
                  label: const Text('حدد الدرجة النهائية'),
                ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              if (selected != null || _preparedLesson != null)
                SizedBox(
                  width: 550,
                  child: DropdownButtonFormField<String>(
                    key: ValueKey(
                      'academic-activity-$_preparedLessonId-${selected?.id}-$_activityId',
                    ),
                    initialValue:
                        activities.any((activity) => activity.id == _activityId)
                        ? _activityId
                        : null,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'اختر الامتحان أو الواجب',
                    ),
                    items: [
                      if (legacyAvailable)
                        const DropdownMenuItem(
                          value: null,
                          child: Text('الرصد السابق للحصة'),
                        ),
                      ...activities.map(
                        (item) => DropdownMenuItem(
                          value: item.id,
                          child: Tooltip(
                            message: item.name,
                            child: Text(
                              '${item.kind == AcademicActivityKind.exam ? 'امتحان' : 'واجب'}: ${item.name}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ),
                      ),
                    ],
                    onChanged:
                        _editing || (!legacyAvailable && activities.isEmpty)
                        ? null
                        : (id) {
                            _changeScope(() {
                              _activityId = id;
                              _search.clear();
                              _lastSaved = null;
                            }, focusCode: true);
                          },
                  ),
                ),
              SizedBox(
                width: 380,
                child: TextField(
                  key: const Key('academic-code-search'),
                  controller: _search,
                  focusNode: _codeFocus,
                  enabled:
                      selected != null &&
                      !_editing &&
                      _canRecordFor(selected.id),
                  textInputAction: TextInputAction.search,
                  decoration: InputDecoration(
                    labelText: activity?.kind == AcademicActivityKind.homework
                        ? 'كود اللي ماعملش الواجب ثم Enter'
                        : 'كود الطالب أو الباركود أو الاسم',
                    prefixIcon: const Icon(Icons.person_search_outlined),
                    suffixIcon: _search.text.isEmpty
                        ? null
                        : IconButton(
                            tooltip: 'كل طلبة الحصة',
                            icon: const Icon(Icons.clear),
                            onPressed: () {
                              _changeScope(_search.clear, focusCode: true);
                            },
                          ),
                  ),
                  onChanged: (_) => setState(() => _lastSaved = null),
                  onSubmitted: (_) {
                    if (selected != null) _submit(selected);
                  },
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (activity != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                '${exam ? 'الامتحان' : 'الواجب'}: ${activity.name}'
                '${!exam
                    ? ''
                    : activity.maxScoreKnown
                    ? ' · الدرجة النهائية ${activity.maxScore}'
                    : ' · الدرجة النهائية غير معروفة'}',
                key: const Key('selected-academic-activity'),
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
          if (!exam && activity != null && selected != null) ...[
            Wrap(
              spacing: 12,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                const Text(
                  'الحاضرين: اتعمل تلقائيًا. اكتب فقط كود اللي ماعملش ثم Enter. لتصحيح حالة طالب استخدم رصد.',
                ),
              ],
            ),
            const SizedBox(height: 12),
          ],
          if (quickStudent != null && selected != null && activity != null) ...[
            AcademicQuickEntry(
              key: ValueKey(
                'quick-${selected.id}-${activity.id}-${quickStudent.id}',
              ),
              store: widget.store,
              student: quickStudent,
              session: selected,
              activity: activity,
              onSaved: () => _finishQuick(saved: true),
              onCancel: () => _finishQuick(saved: false),
              onDetails: () => _quickDetails(quickStudent, selected),
            ),
            const SizedBox(height: 12),
          ],
          if (_lastSaved != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Text(
                _lastSaved!,
                key: const Key('academic-last-saved'),
                style: TextStyle(
                  color: MassarPalette.of(context).accent,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          if (selected != null)
            Text(
              widget.cairo
                  ? 'النتائج: ${students.length} من ${roster.length} طالب — حفظ درجة الامتحان يثبت الحضور.'
                  : 'النتائج: ${students.length} من ${roster.length} طالب — الحاضرون والمعوّضون فعليًا فقط.',
            ),
          const SizedBox(height: 12),
        ],
        child: selected == null
            ? const EmptySection(
                message:
                    'اختر الشهر والحصة لتجهيز الامتحان أو الواجب، ثم المجموعة التي بدأت الحصة لرصد الحاضرين فقط.',
              )
            : !_canRecordFor(selected.id)
            ? const EmptySection(
                message: 'أضف امتحانًا أو واجبًا أولًا، ثم اختر اسمه للرصد.',
              )
            : students.isEmpty
            ? const EmptySection(
                message:
                    'لا يوجد طلبة يطابقون البحث في هذه الحصة. راجع الكود أو امسح البحث.',
              )
            : ManagementTable.builder(
                key: ValueKey(
                  'academic-table-$_sessionId-$_activityId-${_search.text}',
                ),
                columns: [
                  'الكود',
                  'الطالب',
                  'الحضور',
                  if (legacy || !exam) 'الواجب',
                  if (legacy || exam) 'الامتحان',
                  'ملاحظات',
                  'الرصد',
                ],
                rowCount: students.length,
                rowBuilder: (index) {
                  final student = students[index];
                  final record = records[student.id];
                  return DataRow(
                    cells: [
                      DataCell(
                        Tooltip(
                          message: student.barcode.trim().isEmpty
                              ? 'كود ${student.code}'
                              : 'الباركود: ${student.barcode}',
                          child: Text(student.code),
                        ),
                      ),
                      DataCell(Text(student.name)),
                      DataCell(
                        Text(
                          attendance[student.id] == null
                              ? (widget.cairo ? 'لم يُرصد' : 'لم يُحضّر')
                              : attendanceLabel(attendance[student.id]!.status),
                        ),
                      ),
                      if (legacy || !exam)
                        DataCell(
                          Text(
                            _quickHomeworkLabel(
                              record?.homework ?? HomeworkStatus.notReviewed,
                            ),
                          ),
                        ),
                      if (legacy || exam)
                        DataCell(
                          Text(
                            record?.examAbsent == true
                                ? 'غائب عن الامتحان'
                                : record?.score == null
                                ? 'لم تُرصد'
                                : record!.maxScoreKnown
                                ? '${record.score} / ${record.maxScore}'
                                : '${record.score} · الدرجة النهائية غير معروفة',
                            textDirection:
                                record?.score != null &&
                                    record?.maxScoreKnown == true
                                ? TextDirection.ltr
                                : null,
                          ),
                        ),
                      DataCell(
                        SizedBox(
                          width: 180,
                          child: Text(
                            record?.notes ?? '—',
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ),
                      DataCell(
                        TextButton.icon(
                          onPressed:
                              widget.store.canAssess &&
                                  !_editing &&
                                  _canRecordFor(selected.id)
                              ? () => _edit(student, selected)
                              : null,
                          icon: const Icon(Icons.edit_note),
                          label: const Text('رصد'),
                        ),
                      ),
                    ],
                  );
                },
              ),
      ),
    );
  }
}

typedef _AcademicScopeKey = ({
  bool cairo,
  String? groupId,
  String? sessionId,
  String? activityId,
});

class _AcademicScopeSnapshot {
  _AcademicScopeSnapshot(
    CenterStore store,
    this.scope,
    LessonSession? session,
  ) {
    sessions =
        store.sessions
            .where(
              (entry) =>
                  entry.status != SessionStatus.canceled &&
                  store.isCairoGroup(entry.groupId) == scope.cairo &&
                  (scope.groupId == null || entry.groupId == scope.groupId),
            )
            .toList()
          ..sort(compareSessionsNewestFirst);
    attendance = {
      for (final record in store.attendances)
        if (record.sessionId == scope.sessionId) record.studentId: record,
    };
    final academics = store.academics.where(
      (record) => record.sessionId == scope.sessionId,
    );
    records = {
      for (final record in academics)
        if (record.activityId == scope.activityId) record.studentId: record,
    };
    legacyAvailable = academics.any((record) => record.activityId == null);
    roster = _studentsForSession(store, session);
  }

  final _AcademicScopeKey scope;
  late final List<LessonSession> sessions;
  late final List<Student> roster;
  late final Map<String, AcademicRecord> records;
  late final Map<String, AttendanceRecord> attendance;
  late final bool legacyAvailable;

  List<Student> _studentsForSession(CenterStore store, LessonSession? session) {
    if (session == null) return [];
    if (scope.cairo) {
      return store
          .studentsForRegion(cairo: true)
          .where((student) => student.groupIds.contains(session.groupId))
          .toList();
    }
    final present = attendance.values
        .where(
          (record) =>
              record.status == AttendanceStatus.present ||
              record.status == AttendanceStatus.makeup,
        )
        .map((record) => record.studentId)
        .toSet();
    return store.students
        .where((student) => present.contains(student.id))
        .toList();
  }
}
