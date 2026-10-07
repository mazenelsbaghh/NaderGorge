import '../../shared/workspace_draft_guard.dart';
import 'package:massar_center/shared/student_lookup_dialog.dart';
import 'package:massar_center/domain/student_lookup.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';

import '../../application/center_reports.dart';
import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../shared/formatters.dart';
import 'management_widgets.dart';

class ReportsPage extends StatefulWidget {
  const ReportsPage({super.key, required this.store});
  final CenterStore store;

  @override
  State<ReportsPage> createState() => _ReportsPageState();
}

class _ReportsPageState extends State<ReportsPage> {
  final _studentSearch = TextEditingController();
  final _studentFocus = FocusNode();
  final _exactScore = TextEditingController(),
      _minScore = TextEditingController(),
      _maxScore = TextEditingController();
  ExamReportStatus _examStatus = ExamReportStatus.all;
  HomeworkStatus? _homeworkStatus;
  String? _activityId;
  String? _homeworkMonthId, _homeworkLessonId;
  final Set<String> _homeworkGroups = {};
  CenterReportKind _kind = CenterReportKind.students;
  String? _studentId, _groupId, _sessionId, _subjectId, _centerId, _gradeId;
  DateTimeRange? _range;
  ReviewReportMode _reviewMode = ReviewReportMode.codes;
  PaymentReportMode _paymentMode = PaymentReportMode.all;
  DebtReportStatus _debtStatus = DebtReportStatus.outstanding;
  DebtKind? _debtKind;
  bool _attendanceCorrections = false;
  ClosingReportMode _closingMode = ClosingReportMode.live;
  bool _unassignedOnly = false;
  bool _busy = false;

  @override
  void dispose() {
    _studentSearch.dispose();
    _studentFocus.dispose();
    _exactScore.dispose();
    _minScore.dispose();
    _maxScore.dispose();
    super.dispose();
  }

  CenterReportFilter get _filter => CenterReportFilter(
    activityId: _activityId,
    groupIds: _kind == CenterReportKind.homework
        ? Set.of(_homeworkGroups)
        : const {},
    studyMonthId: _kind == CenterReportKind.homework ? _homeworkMonthId : null,
    preparedLessonId: _kind == CenterReportKind.homework
        ? _homeworkLessonId
        : null,
    studentId: _studentId,
    groupId: _groupId,
    sessionId: _sessionId,
    subjectId: _subjectId,
    centerId: _centerId,
    gradeId: _gradeId,
    from: _range?.start,
    until: _range?.end,
    unassignedPaymentsOnly: _unassignedOnly,
    reviewMode: _reviewMode,
    paymentMode: _paymentMode,
    debtStatus: _kind == CenterReportKind.debts
        ? _debtStatus
        : DebtReportStatus.outstanding,
    debtKind: _kind == CenterReportKind.debts ? _debtKind : null,
    attendanceCorrections: _attendanceCorrections,
    closingMode: _closingMode,
    examStatus: _kind == CenterReportKind.exams
        ? _examStatus
        : ExamReportStatus.all,
    exactScore: _kind == CenterReportKind.exams
        ? _scoreValue(_exactScore, 'الدرجة المحددة')
        : null,
    minScore: _kind == CenterReportKind.exams
        ? _scoreValue(_minScore, 'أقل درجة')
        : null,
    maxScore: _kind == CenterReportKind.exams
        ? _scoreValue(_maxScore, 'أعلى درجة')
        : null,
    homeworkStatus: _kind == CenterReportKind.homework ? _homeworkStatus : null,
  );

  num? _scoreValue(TextEditingController controller, String label) {
    if (controller.text.trim().isEmpty) return null;
    final score = parseAcademicScore(controller.text);
    if (score == null) {
      throw CenterException(
        'اكتب درجة رقمية في حقل $label، مثل 8.5. الصفر درجة فعلية.',
      );
    }
    return score;
  }

  Widget _scoreInput(
    TextEditingController controller,
    String label,
    String key,
  ) => SizedBox(
    width: 145,
    child: TextField(
      key: Key(key),
      controller: controller,
      keyboardType: const TextInputType.numberWithOptions(decimal: true),
      decoration: InputDecoration(labelText: label),
      onChanged: (_) => setState(() {}),
    ),
  );

  List<StudyGroup> get _groups => widget.store.groups
      .where(
        (group) =>
            (_subjectId == null || group.subjectId == _subjectId) &&
            (_centerId == null || group.centerId == _centerId) &&
            (_gradeId == null || group.gradeId == _gradeId),
      )
      .toList();

  void _clear() => setState(() {
    _studentId = _groupId = _sessionId = _subjectId = _centerId = _gradeId =
        null;
    _range = null;
    _unassignedOnly = false;
    _reviewMode = ReviewReportMode.codes;
    _paymentMode = PaymentReportMode.all;
    _debtStatus = DebtReportStatus.outstanding;
    _debtKind = null;
    _attendanceCorrections = false;
    _closingMode = ClosingReportMode.live;
    _studentSearch.clear();
    _exactScore.clear();
    _minScore.clear();
    _maxScore.clear();
    _examStatus = ExamReportStatus.all;
    _homeworkStatus = null;
    _activityId = null;
    _homeworkMonthId = _homeworkLessonId = null;
    _homeworkGroups.clear();
  });

  Future<void> _pickRange() async {
    final selected = await showDateRangePicker(
      context: context,
      initialDateRange: _range,
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
    );
    if (selected != null && mounted) {
      setState(() {
        _range = selected;
        _activityId = null;
      });
    }
  }

  Future<void> _export() async {
    final kind = _kind;
    final filter = _filter;
    setState(() => _busy = true);
    try {
      final timestamp = DateTime.now()
          .toIso8601String()
          .substring(0, 19)
          .replaceAll(':', '-');
      final location = await getSaveLocation(
        suggestedName: 'massar-${kind.name}-$timestamp.csv',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'تقرير CSV', extensions: ['csv']),
        ],
      );
      if (location == null) return;
      await CenterReports.exportCsv(
        store: widget.store,
        kind: kind,
        filter: filter,
        destination: location.path,
      );
      if (mounted) {
        await showManagementMessage(
          context,
          'حُفظ التقرير بالفلاتر المختارة. يمكنك فتحه في Excel.',
        );
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.reports_page');
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _applyCode() async {
    if (_busy) return;
    final code = _studentSearch.text.trim();
    final selected = widget.store.students
        .where((student) => student.id == _studentId)
        .firstOrNull;
    if (selected != null && code == '${selected.code} — ${selected.name}') {
      return;
    }
    final matches = studentsWithIdentifier(widget.store.students, code);
    setState(() => _busy = true);
    try {
      final student = matches.length == 1
          ? matches.single
          : matches.isEmpty
          ? null
          : await chooseMatchingStudent(context, matches);
      if (!mounted) return;
      if (student != null) {
        _selectStudent(student);
      } else if (matches.isEmpty) {
        await showManagementMessage(
          context,
          'لم يوجد هذا الكود أو الباركود. للبحث بالاسم اختر الطالب من النتائج.',
          kind: NoticeKind.warning,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _selectStudent(Student student) => setState(() {
    _studentId = student.id;
    _studentSearch.text = '${student.code} — ${student.name}';
    _studentFocus.unfocus();
  });

  Widget _studentPicker() => SizedBox(
    key: const Key('report-student-picker'),
    width: 340,
    child: RawAutocomplete<Student>(
      textEditingController: _studentSearch,
      focusNode: _studentFocus,
      displayStringForOption: (student) => '${student.code} — ${student.name}',
      optionsBuilder: (query) {
        final text = query.text.trim().toLowerCase();
        if (text.isEmpty) return const Iterable<Student>.empty();
        return studentLookupCandidates(widget.store.students, text).take(30);
      },
      onSelected: _selectStudent,
      fieldViewBuilder: (context, controller, focusNode, submit) => TextField(
        key: const Key('report-student-search'),
        enabled: !_busy,
        controller: controller,
        focusNode: focusNode,
        decoration: InputDecoration(
          labelText: 'كل الطلبة / الكود أو الباركود أو الاسم',
          prefixIcon: const Icon(Icons.person_search_outlined),
          suffixIcon: _studentId == null && controller.text.isEmpty
              ? null
              : IconButton(
                  tooltip: 'كل الطلبة',
                  onPressed: () => setState(() {
                    _studentId = null;
                    _studentSearch.clear();
                  }),
                  icon: const Icon(Icons.clear),
                ),
        ),
        onChanged: (_) => setState(() => _studentId = null),
        onSubmitted: (_) => _applyCode(),
      ),
      optionsViewBuilder: (context, select, options) {
        final students = options.toList();
        return Align(
          alignment: Alignment.topRight,
          child: Material(
            elevation: 6,
            borderRadius: BorderRadius.circular(8),
            child: SizedBox(
              width: 340,
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxHeight: 240),
                child: ListView.builder(
                  padding: EdgeInsets.zero,
                  shrinkWrap: true,
                  itemCount: students.length,
                  itemBuilder: (context, index) => ListTile(
                    title: Text(students[index].name),
                    subtitle: Text(
                      'الكود: ${students[index].code}'
                      '${students[index].barcode.trim().isEmpty ? '' : '\nالباركود: ${students[index].barcode}'}',
                    ),
                    onTap: () => select(students[index]),
                  ),
                ),
              ),
            ),
          ),
        );
      },
    ),
  );

  Widget _catalogPicker(
    CatalogKind kind,
    String label,
    String? selected,
    ValueChanged<String?> select,
  ) => SizedBox(
    width: 190,
    child: DropdownButtonFormField<String>(
      key: ValueKey('report-${kind.name}-$selected'),
      initialValue: selected,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: [
        DropdownMenuItem(value: null, child: Text('كل $label')),
        ...widget.store.catalogs
            .where((entry) => entry.kind == kind)
            .map(
              (entry) =>
                  DropdownMenuItem(value: entry.id, child: Text(entry.name)),
            ),
      ],
      onChanged: (id) => setState(() {
        select(id);
        _homeworkGroups.clear();
        _groupId = _sessionId = _activityId = null;
      }),
    ),
  );

  bool _activitySessionMatches(LessonSession session) {
    if ((_kind == CenterReportKind.homework &&
            ((_homeworkGroups.isNotEmpty &&
                    !_homeworkGroups.contains(session.groupId)) ||
                (_homeworkLessonId != null &&
                    session.preparedLessonId != _homeworkLessonId) ||
                (_homeworkMonthId != null &&
                    (session.preparedLessonId == null ||
                        widget.store
                                .studyMonthForLesson(session.preparedLessonId!)
                                ?.id !=
                            _homeworkMonthId)))) ||
        session.status == SessionStatus.canceled ||
        (_sessionId != null && session.id != _sessionId)) {
      return false;
    }
    if (_range == null) return true;
    if (!session.startsAtKnown) return false;
    final starts = session.startsAt.toLocal();
    final day = DateTime(starts.year, starts.month, starts.day);
    return !day.isBefore(_range!.start) && !day.isAfter(_range!.end);
  }

  List<AcademicActivity> _reportActivities(List<LessonSession> sessions) {
    final kind = _kind == CenterReportKind.exams
        ? AcademicActivityKind.exam
        : AcademicActivityKind.homework;
    return widget.store.academicActivities.where((activity) {
      if (activity.kind != kind) return false;
      if (sessions.any(
        (session) =>
            activity.appliesToSession(session) &&
            _activitySessionMatches(session),
      )) {
        return true;
      }
      return activity.preparedLessonId != null &&
          (_kind != CenterReportKind.homework ||
              (_homeworkGroups.isEmpty &&
                  (_homeworkLessonId == null ||
                      activity.preparedLessonId == _homeworkLessonId) &&
                  (_homeworkMonthId == null ||
                      widget.store
                              .studyMonthForLesson(activity.preparedLessonId!)
                              ?.id ==
                          _homeworkMonthId))) &&
          _groupId == null &&
          _sessionId == null &&
          _subjectId == null &&
          _centerId == null &&
          _gradeId == null &&
          !widget.store.sessions.any(
            (session) =>
                activity.appliesToSession(session) &&
                session.status != SessionStatus.canceled,
          );
    }).toList();
  }

  String _activityLabel(AcademicActivity activity) {
    if (activity.preparedLessonId != null) {
      final month = widget.store.studyMonthForLesson(
        activity.preparedLessonId!,
      );
      final lesson = month?.lessons
          .where((lesson) => lesson.id == activity.preparedLessonId)
          .firstOrNull;
      return '${activity.name} · ${month?.name ?? 'حصة مشتركة'} · ${lesson?.name ?? ''} · كل المجموعات';
    }
    final session = widget.store.sessions
        .where((session) => session.id == activity.sessionId)
        .firstOrNull;
    return session == null
        ? activity.name
        : '${activity.name} · ${sessionLabel(session)} · ${widget.store.groupLabel(session.groupId)} · ${sessionDateLabel(session)}';
  }

  Future<void> _pickHomeworkGroups() async {
    final selected = Set<String>.of(_homeworkGroups);
    final result = await showDialog<Set<String>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, update) => AlertDialog(
          title: const Text('مجموعات تقرير الواجب'),
          content: SizedBox(
            width: 520,
            height: 360,
            child: ListView(
              children: [
                CheckboxListTile(
                  title: const Text('كل المجموعات'),
                  value: selected.isEmpty,
                  onChanged: (_) => update(selected.clear),
                ),
                for (final group in _groups)
                  CheckboxListTile(
                    title: Text(widget.store.groupLabel(group.id)),
                    value: selected.contains(group.id),
                    onChanged: (checked) => update(() {
                      if (checked!) {
                        selected.add(group.id);
                      } else {
                        selected.remove(group.id);
                      }
                    }),
                  ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('إلغاء'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, selected),
              child: const Text('تطبيق'),
            ),
          ],
        ),
      ),
    );
    if (result != null && mounted) {
      setState(() {
        _homeworkGroups
          ..clear()
          ..addAll(result);
        _activityId = null;
      });
    }
  }

  List<Widget> _homeworkScopeFilters() {
    final month = widget.store.studyMonths
        .where((m) => m.id == _homeworkMonthId)
        .firstOrNull;
    return [
      OutlinedButton.icon(
        key: const Key('homework-report-groups'),
        onPressed: _pickHomeworkGroups,
        icon: const Icon(Icons.groups_outlined),
        label: Text(
          _homeworkGroups.isEmpty
              ? 'كل المجموعات'
              : '${_homeworkGroups.length} مجموعات مختارة',
        ),
      ),
      SizedBox(
        width: 230,
        child: DropdownButtonFormField<String>(
          key: ValueKey('homework-report-month-$_homeworkMonthId'),
          initialValue: _homeworkMonthId,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'الشهر'),
          items: [
            const DropdownMenuItem(value: null, child: Text('كل الشهور')),
            for (final m in widget.store.studyMonths)
              DropdownMenuItem(value: m.id, child: Text(m.name)),
          ],
          onChanged: (id) => setState(() {
            _homeworkMonthId = id;
            _homeworkLessonId = _activityId = null;
          }),
        ),
      ),
      SizedBox(
        width: 230,
        child: DropdownButtonFormField<String>(
          key: ValueKey(
            'homework-report-lesson-$_homeworkMonthId-$_homeworkLessonId',
          ),
          initialValue: _homeworkLessonId,
          isExpanded: true,
          decoration: const InputDecoration(labelText: 'الحصة من الشهر'),
          items: [
            const DropdownMenuItem(value: null, child: Text('كل حصص الشهر')),
            for (final lesson in month?.lessons ?? <PreparedLesson>[])
              DropdownMenuItem(
                value: lesson.id,
                child: Text('حصة ${lesson.number} · ${lesson.name}'),
              ),
          ],
          onChanged: month == null
              ? null
              : (id) => setState(() {
                  _homeworkLessonId = id;
                  _activityId = null;
                }),
        ),
      ),
    ];
  }

  Widget _filters() {
    final groupIds = _groups.map((group) => group.id).toSet();
    final sessions =
        widget.store.sessions
            .where(
              (session) =>
                  groupIds.contains(session.groupId) &&
                  (_groupId == null || session.groupId == _groupId),
            )
            .toList()
          ..sort(compareSessionsNewestFirst);
    final activityOptions = _reportActivities(sessions);
    return AbsorbPointer(
      absorbing: _busy,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Wrap(
            spacing: 12,
            runSpacing: 12,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              SizedBox(
                width: 250,
                child: DropdownButtonFormField<CenterReportKind>(
                  key: const Key('report-kind'),
                  initialValue: _kind,
                  isExpanded: true,
                  decoration: const InputDecoration(labelText: 'نوع التقرير'),
                  items: CenterReportKind.values
                      .where(
                        (kind) =>
                            !CenterReports.isFinancial(kind) ||
                            widget.store.canCollect,
                      )
                      .map(
                        (kind) => DropdownMenuItem(
                          value: kind,
                          child: Text(CenterReports.label(kind)),
                        ),
                      )
                      .toList(),
                  onChanged: (kind) => setState(() {
                    _kind = kind!;
                    if (_kind == CenterReportKind.homework) {
                      _homeworkStatus = HomeworkStatus.missing;
                      _groupId = _sessionId = null;
                    }
                    _activityId = null;
                    _unassignedOnly = false;
                  }),
                ),
              ),
              if (_kind == CenterReportKind.reviews)
                SizedBox(
                  width: 190,
                  child: DropdownButtonFormField<ReviewReportMode>(
                    key: ValueKey('report-review-mode-$_reviewMode'),
                    initialValue: _reviewMode,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'نوع المراجعة',
                    ),
                    items: const [
                      DropdownMenuItem(
                        value: ReviewReportMode.codes,
                        child: Text('أكواد'),
                      ),
                      DropdownMenuItem(
                        value: ReviewReportMode.amounts,
                        child: Text('مبالغ'),
                      ),
                      DropdownMenuItem(
                        value: ReviewReportMode.all,
                        child: Text('الكل'),
                      ),
                    ],
                    onChanged: (mode) => setState(() => _reviewMode = mode!),
                  ),
                ),
              if (_kind == CenterReportKind.exams)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Wrap(
                    spacing: 12,
                    runSpacing: 12,
                    children: [
                      SizedBox(
                        width: 230,
                        child: DropdownButtonFormField<ExamReportStatus>(
                          key: ValueKey('report-exam-status-$_examStatus'),
                          initialValue: _examStatus,
                          isExpanded: true,
                          decoration: const InputDecoration(
                            labelText: 'حالة الامتحان',
                          ),
                          items: const [
                            DropdownMenuItem(
                              value: ExamReportStatus.all,
                              child: Text('كل الحالات'),
                            ),
                            DropdownMenuItem(
                              value: ExamReportStatus.recorded,
                              child: Text('درجات مرصودة'),
                            ),
                            DropdownMenuItem(
                              value: ExamReportStatus.absent,
                              child: Text('غائب عن الامتحان'),
                            ),
                            DropdownMenuItem(
                              value: ExamReportStatus.unrecorded,
                              child: Text('لم تُرصد'),
                            ),
                            DropdownMenuItem(
                              value: ExamReportStatus.notTaken,
                              child: Text('غائب أو لم تُرصد'),
                            ),
                          ],
                          onChanged: (status) =>
                              setState(() => _examStatus = status!),
                        ),
                      ),
                      _scoreInput(
                        _exactScore,
                        'درجة محددة',
                        'report-exact-score',
                      ),
                      _scoreInput(_minScore, 'من درجة', 'report-min-score'),
                      _scoreInput(_maxScore, 'إلى درجة', 'report-max-score'),
                    ],
                  ),
                ),
              if (_kind == CenterReportKind.homework)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: SizedBox(
                    width: 230,
                    child: DropdownButtonFormField<HomeworkStatus>(
                      key: ValueKey('report-homework-status-$_homeworkStatus'),
                      initialValue: _homeworkStatus,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'حالة الواجب',
                      ),
                      items: [
                        const DropdownMenuItem(
                          value: null,
                          child: Text('كل الحالات'),
                        ),
                        ...HomeworkStatus.values.map(
                          (status) => DropdownMenuItem(
                            value: status,
                            child: Text(homeworkLabel(status)),
                          ),
                        ),
                      ],
                      onChanged: (status) =>
                          setState(() => _homeworkStatus = status),
                    ),
                  ),
                ),
              if (_kind == CenterReportKind.attendance)
                CheckboxListTile(
                  key: const Key('report-attendance-corrections'),
                  contentPadding: EdgeInsets.zero,
                  controlAffinity: ListTileControlAffinity.leading,
                  title: const Text('عرض سجل تصحيح الحضور'),
                  value: _attendanceCorrections,
                  onChanged: (selected) =>
                      setState(() => _attendanceCorrections = selected!),
                ),
              if (_kind == CenterReportKind.payments)
                SizedBox(
                  width: 190,
                  child: DropdownButtonFormField<PaymentReportMode>(
                    key: ValueKey('report-payment-mode-$_paymentMode'),
                    initialValue: _paymentMode,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'عرض الحركات'),
                    items: const [
                      DropdownMenuItem(
                        value: PaymentReportMode.all,
                        child: Text('الكل'),
                      ),
                      DropdownMenuItem(
                        value: PaymentReportMode.collections,
                        child: Text('التحصيل'),
                      ),
                      DropdownMenuItem(
                        value: PaymentReportMode.refunds,
                        child: Text('الاستردادات'),
                      ),
                    ],
                    onChanged: (mode) => setState(() => _paymentMode = mode!),
                  ),
                ),
              if (_kind == CenterReportKind.debts) ...[
                SizedBox(
                  width: 210,
                  child: DropdownButtonFormField<DebtReportStatus>(
                    key: ValueKey('report-debt-status-$_debtStatus'),
                    initialValue: _debtStatus,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'حالة المديونية الحالية',
                    ),
                    items: const [
                      DropdownMenuItem(
                        value: DebtReportStatus.outstanding,
                        child: Text('متبقي مديونية'),
                      ),
                      DropdownMenuItem(
                        value: DebtReportStatus.settled,
                        child: Text('مسددة بالكامل'),
                      ),
                      DropdownMenuItem(
                        value: DebtReportStatus.all,
                        child: Text('كل الالتزامات السارية'),
                      ),
                    ],
                    onChanged: (status) =>
                        setState(() => _debtStatus = status!),
                  ),
                ),
                SizedBox(
                  width: 210,
                  child: DropdownButtonFormField<DebtKind>(
                    key: ValueKey('report-debt-kind-$_debtKind'),
                    initialValue: _debtKind,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'نوع الالتزام',
                    ),
                    items: const [
                      DropdownMenuItem(value: null, child: Text('كل الأنواع')),
                      DropdownMenuItem(
                        value: DebtKind.lesson,
                        child: Text('الحصص والباقات'),
                      ),
                      DropdownMenuItem(
                        value: DebtKind.card,
                        child: Text('الكروت'),
                      ),
                    ],
                    onChanged: (kind) => setState(() => _debtKind = kind),
                  ),
                ),
              ],
              if (_kind == CenterReportKind.closings)
                SizedBox(
                  width: 260,
                  child: DropdownButtonFormField<ClosingReportMode>(
                    key: ValueKey('report-closing-mode-$_closingMode'),
                    initialValue: _closingMode,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'تقرير الحصة / التقفيلات',
                    ),
                    items: const [
                      DropdownMenuItem(
                        value: ClosingReportMode.live,
                        child: Text('لحظي — الحصص الحالية'),
                      ),
                      DropdownMenuItem(
                        value: ClosingReportMode.summary,
                        child: Text('تقفيلات محفوظة — ملخص'),
                      ),
                      DropdownMenuItem(
                        value: ClosingReportMode.categories,
                        child: Text('تقفيلات محفوظة — الفئات'),
                      ),
                    ],
                    onChanged: (mode) => setState(() => _closingMode = mode!),
                  ),
                ),
              if (_kind == CenterReportKind.homework)
                ..._homeworkScopeFilters(),
              _studentPicker(),
              _catalogPicker(
                CatalogKind.subject,
                'المواد',
                _subjectId,
                (id) => _subjectId = id,
              ),
              _catalogPicker(
                CatalogKind.center,
                'السناتر',
                _centerId,
                (id) => _centerId = id,
              ),
              _catalogPicker(
                CatalogKind.grade,
                'الصفوف',
                _gradeId,
                (id) => _gradeId = id,
              ),
              if (_kind != CenterReportKind.homework)
                SizedBox(
                  width: 290,
                  child: DropdownButtonFormField<String>(
                    key: ValueKey(
                      'report-group-$_groupId-$_subjectId-$_centerId-$_gradeId',
                    ),
                    initialValue: _groupId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'المجموعة'),
                    items: [
                      const DropdownMenuItem(
                        value: null,
                        child: Text('كل المجموعات'),
                      ),
                      ..._groups.map(
                        (group) => DropdownMenuItem(
                          value: group.id,
                          child: Text(widget.store.groupLabel(group.id)),
                        ),
                      ),
                    ],
                    onChanged: (id) => setState(() {
                      _groupId = id;
                      _sessionId = _activityId = null;
                    }),
                  ),
                ),
              if (_kind != CenterReportKind.homework)
                SizedBox(
                  width: 290,
                  child: DropdownButtonFormField<String>(
                    key: ValueKey(
                      'report-session-$_sessionId-$_groupId-$_subjectId-$_centerId-$_gradeId',
                    ),
                    initialValue: _sessionId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'الحصة'),
                    items: [
                      const DropdownMenuItem(
                        value: null,
                        child: Text('كل الحصص'),
                      ),
                      ...sessions.map(
                        (session) => DropdownMenuItem(
                          value: session.id,
                          child: Text(
                            '${sessionLabel(session)} — ${widget.store.groupLabel(session.groupId)} — ${sessionDateLabel(session)}',
                          ),
                        ),
                      ),
                    ],
                    onChanged: _unassignedOnly
                        ? null
                        : (id) => setState(() {
                            _sessionId = id;
                            _activityId = null;
                          }),
                  ),
                ),
              if (_kind == CenterReportKind.exams ||
                  _kind == CenterReportKind.homework)
                SizedBox(
                  width: 340,
                  child: DropdownButtonFormField<String>(
                    key: ValueKey(
                      'report-activity-$_kind-$_sessionId-$_groupId-$_activityId',
                    ),
                    initialValue:
                        activityOptions.any(
                          (activity) => activity.id == _activityId,
                        )
                        ? _activityId
                        : null,
                    isExpanded: true,
                    decoration: InputDecoration(
                      labelText: _kind == CenterReportKind.exams
                          ? 'اسم الامتحان'
                          : 'اسم الواجب',
                    ),
                    items: [
                      const DropdownMenuItem(
                        value: null,
                        child: Text('كل الأنشطة والسجلات السابقة'),
                      ),
                      ...activityOptions.map(
                        (activity) => DropdownMenuItem(
                          value: activity.id,
                          child: Tooltip(
                            message: _activityLabel(activity),
                            child: Text(
                              _activityLabel(activity),
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ),
                      ),
                    ],
                    onChanged: (id) => setState(() => _activityId = id),
                  ),
                ),
              OutlinedButton.icon(
                onPressed: _pickRange,
                icon: const Icon(Icons.date_range_outlined),
                label: Text(
                  _range == null
                      ? 'كل التواريخ'
                      : '${shortDate(_range!.start)} — ${shortDate(_range!.end)}',
                ),
              ),
              TextButton.icon(
                onPressed: _clear,
                icon: const Icon(Icons.filter_alt_off_outlined),
                label: const Text('مسح الفلاتر'),
              ),
            ],
          ),
          if (_kind == CenterReportKind.payments)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: CheckboxListTile(
                key: const Key('report-unassigned'),
                contentPadding: EdgeInsets.zero,
                controlAffinity: ListTileControlAffinity.leading,
                title: const Text('مدفوعات غير مرتبطة بحصة فقط'),
                value: _unassignedOnly,
                onChanged: (selected) => setState(() {
                  _unassignedOnly = selected!;
                  if (_unassignedOnly) _sessionId = null;
                }),
              ),
            ),
          if (_studentId == null && _studentSearch.text.isNotEmpty)
            const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Text(
                'اختر الطالب من النتائج، أو اضغط Enter بعد كتابة الكود لتطبيق الفلتر.',
              ),
            ),
        ],
      ),
    );
  }

  Widget _report(CenterReportData report) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const SizedBox(height: 16),
      Text(report.title, style: Theme.of(context).textTheme.titleLarge),
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 16),
        child: Wrap(
          spacing: 24,
          runSpacing: 8,
          children: report.summary.entries
              .map(
                (entry) => Text(
                  '${entry.key}: ${entry.value}',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              )
              .toList(),
        ),
      ),
      if (report.caption != null)
        Padding(
          padding: const EdgeInsets.only(bottom: 16),
          child: Text(report.caption!),
        ),
      Expanded(
        child: report.rows.isEmpty
            ? const EmptySection(
                message:
                    'لا توجد سجلات تطابق الفلاتر المختارة. يمكنك توسيع الفترة أو مسح الفلاتر.',
              )
            : ManagementTable.builder(
                key: ValueKey(
                  '$_kind-$_activityId-$_studentId-$_groupId-$_sessionId-$_subjectId-$_centerId-$_gradeId-$_range-$_unassignedOnly-$_reviewMode-$_paymentMode-$_debtStatus-$_debtKind-$_attendanceCorrections-$_closingMode-$_examStatus-${_exactScore.text}-${_minScore.text}-${_maxScore.text}-$_homeworkStatus',
                ),
                columns: report.columns,
                rowCount: report.rows.length,
                rowBuilder: (index) => DataRow(
                  cells: report.rows[index]
                      .map(
                        (cell) => DataCell(
                          ConstrainedBox(
                            constraints: const BoxConstraints(maxWidth: 240),
                            child: Tooltip(
                              message: cell?.toString() ?? '',
                              child: Text(
                                cell?.toString() ?? '',
                                textDirection:
                                    RegExp(
                                      r'^-?\d+(?:\.\d+)?$',
                                    ).hasMatch(cell?.toString() ?? '')
                                    ? TextDirection.ltr
                                    : null,
                                maxLines: 3,
                                overflow: TextOverflow.ellipsis,
                                style:
                                    cell == 'معوّض' ||
                                        (cell is String &&
                                            cell.startsWith('معوّض —'))
                                    ? TextStyle(
                                        color: Theme.of(
                                          context,
                                        ).colorScheme.error,
                                      )
                                    : null,
                              ),
                            ),
                          ),
                        ),
                      )
                      .toList(),
                ),
              ),
      ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    CenterReportData? report;
    String? error;
    try {
      report = CenterReports.build(widget.store, _kind, _filter);
    } on CenterException catch (exception) {
      error = exception.message;
    }
    return WorkspaceDraftRegistration(
      dirty: false,
      busy: _busy,
      child: ManagementPanel(
        title: 'التقارير',
        subtitle: 'اختر التقرير والطالب والفترة، وصدّر نفس النتائج المعروضة.',
        actions: [
          OutlinedButton.icon(
            onPressed: _busy || report == null ? null : _export,
            icon: const Icon(Icons.download_outlined),
            label: Text(
              _busy
                  ? 'جارٍ التصدير…'
                  : _kind == CenterReportKind.homework
                  ? 'تنزيل شيت الواجب · Excel / CSV'
                  : 'تصدير النتائج CSV',
            ),
          ),
        ],
        child: ManagementBody(
          header: [
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Wrap(
                spacing: 8,
                children: [
                  ChoiceChip(
                    label: const Text('كل التقارير'),
                    selected: _kind != CenterReportKind.homework,
                    onSelected: (_) => setState(() {
                      _kind = CenterReportKind.students;
                      _activityId = null;
                    }),
                  ),
                  ChoiceChip(
                    key: const Key('homework-report-tab'),
                    label: const Text('الواجب'),
                    selected: _kind == CenterReportKind.homework,
                    onSelected: (_) => setState(() {
                      _kind = CenterReportKind.homework;
                      _homeworkStatus = HomeworkStatus.missing;
                      _groupId = _sessionId = _activityId = null;
                    }),
                  ),
                ],
              ),
            ),
            _filters(),
          ],
          child: report == null
              ? EmptySection(message: error!)
              : _report(report),
        ),
      ),
    );
  }
}
