import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/application/center_reports.dart'
    show parseAcademicScore;
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/workspace_draft_guard.dart';

class AcademicQuickEntry extends StatefulWidget {
  const AcademicQuickEntry({
    super.key,
    required this.store,
    required this.student,
    required this.session,
    required this.activity,
    required this.onSaved,
    required this.onCancel,
    required this.onDetails,
  });
  final CenterStore store;
  final Student student;
  final LessonSession session;
  final AcademicActivity activity;
  final VoidCallback onSaved, onCancel, onDetails;

  @override
  State<AcademicQuickEntry> createState() => _AcademicQuickEntryState();
}

class _ArmAcademicSave extends Intent {
  const _ArmAcademicSave(this.key, [this.homework]);
  final LogicalKeyboardKey key;
  final HomeworkStatus? homework;
}

class _AcademicQuickEntryState extends State<AcademicQuickEntry> {
  final _score = TextEditingController();
  final _scoreFocus = FocusNode();
  final _keyboardFocus = FocusNode();
  final _cancelFocus = FocusNode();
  final _detailsFocus = FocusNode();
  final _homeworkFocus = {
    for (final status in [
      HomeworkStatus.complete,
      HomeworkStatus.missing,
      HomeworkStatus.incomplete,
    ])
      status: FocusNode(),
  };
  bool _busy = false, _completed = false;
  String? _scoreError;
  _ArmAcademicSave? _armed;
  bool _enterCancels = false, _enterDetails = false;
  bool _leaving = false;
  String _initialScore = '', _saveError = '';
  bool get _dirty => !_completed && _exam && _score.text != _initialScore;

  bool get _exam => widget.activity.kind == AcademicActivityKind.exam;
  (String, String, String) get _context =>
      (widget.student.id, widget.session.id, widget.activity.id);
  bool get _available =>
      !_busy &&
      !_leaving &&
      !_completed &&
      widget.store.canAssess &&
      widget.activity.appliesToSession(widget.session) &&
      widget.session.status != SessionStatus.canceled &&
      ((widget.store.isCairoGroup(widget.session.groupId) &&
              widget.student.groupIds.contains(widget.session.groupId)) ||
          widget.store.attendances.any(
            (entry) =>
                entry.studentId == widget.student.id &&
                entry.sessionId == widget.session.id &&
                (entry.status == AttendanceStatus.present ||
                    entry.status == AttendanceStatus.makeup),
          ));

  AcademicRecord? _existing(
    String studentId,
    String sessionId,
    String activityId,
  ) => widget.store.academics
      .where(
        (record) =>
            record.studentId == studentId &&
            record.sessionId == sessionId &&
            record.activityId == activityId,
      )
      .firstOrNull;

  @override
  void initState() {
    super.initState();
    widget.store.addListener(_refreshPermission);
    _loadStudent();
  }

  @override
  void didUpdateWidget(AcademicQuickEntry oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.store != widget.store) {
      oldWidget.store.removeListener(_refreshPermission);
      widget.store.addListener(_refreshPermission);
    }
    if ((oldWidget.student.id, oldWidget.session.id, oldWidget.activity.id) !=
        _context) {
      _loadStudent();
    }
  }

  void _loadStudent() {
    final record = _existing(
      widget.student.id,
      widget.session.id,
      widget.activity.id,
    );
    _score.text = record?.score?.toString() ?? '';
    _initialScore = _score.text;
    _scoreError = null;
    _saveError = '';
    _completed = false;
    _armed = null;
    _focusEntry();
  }

  void _focusEntry() => WidgetsBinding.instance.addPostFrameCallback((_) {
    if (!mounted || !_available || ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    if (_exam && widget.activity.maxScoreKnown) {
      _scoreFocus.requestFocus();
      _score.selection = TextSelection(
        baseOffset: 0,
        extentOffset: _score.text.length,
      );
    } else {
      _keyboardFocus.requestFocus();
    }
  });

  void _refreshPermission() {
    if (mounted && !_busy) setState(() {});
  }

  @override
  void dispose() {
    widget.store.removeListener(_refreshPermission);
    _score.dispose();
    _scoreFocus.dispose();
    _keyboardFocus.dispose();
    _cancelFocus.dispose();
    _detailsFocus.dispose();
    for (final focus in _homeworkFocus.values) {
      focus.dispose();
    }
    super.dispose();
  }

  Future<void> _cancel() async {
    if (_busy || _leaving || _completed || hasPendingMassarNotice(context)) {
      return;
    }
    setState(() => _leaving = true);
    try {
      final accepted = await confirmDiscardDraft(context, dirty: _dirty);
      if (!mounted || !accepted) return;
      _completed = true;
      widget.onCancel();
    } finally {
      if (mounted) setState(() => _leaving = false);
    }
  }

  Future<void> _details() async {
    if (!_available || hasPendingMassarNotice(context)) return;
    setState(() => _leaving = true);
    try {
      final accepted = await confirmDiscardDraft(context, dirty: _dirty);
      if (!mounted || !accepted) return;
      setState(() => _busy = true);
      widget.onDetails();
    } finally {
      if (mounted) setState(() => _leaving = false);
    }
  }

  void _arm(_ArmAcademicSave intent) {
    if (_busy || _leaving || _completed || hasPendingMassarNotice(context)) {
      return;
    }
    final focusedHomework = _homeworkFocus.entries
        .where((entry) => entry.value.hasFocus)
        .firstOrNull
        ?.key;
    _armed = intent.homework == null && !_exam && focusedHomework != null
        ? _ArmAcademicSave(intent.key, focusedHomework)
        : intent;
    _enterDetails = intent.homework == null && _detailsFocus.hasFocus;
    _enterCancels = intent.homework == null && _cancelFocus.hasFocus;
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (event.logicalKey == LogicalKeyboardKey.escape) {
      if (event is KeyDownEvent) _cancel();
      return KeyEventResult.handled;
    }
    if (event is KeyUpEvent && _armed?.key == event.logicalKey) {
      final intent = _armed!;
      _armed = null;
      final controlRequired =
          intent.homework != null &&
          intent.key != LogicalKeyboardKey.enter &&
          intent.key != LogicalKeyboardKey.numpadEnter;
      if (HardwareKeyboard.instance.isControlPressed != controlRequired ||
          HardwareKeyboard.instance.isAltPressed ||
          HardwareKeyboard.instance.isMetaPressed ||
          HardwareKeyboard.instance.isShiftPressed) {
        return KeyEventResult.handled;
      }
      if (_enterCancels) {
        _cancel();
      } else if (_enterDetails) {
        _details();
      } else if (intent.homework != null) {
        _save(homework: intent.homework!);
      } else if (_exam) {
        _saveExam();
      }
      return KeyEventResult.handled;
    }
    if (event.logicalKey == LogicalKeyboardKey.enter ||
        event.logicalKey == LogicalKeyboardKey.numpadEnter) {
      return KeyEventResult.handled;
    }
    return KeyEventResult.ignored;
  }

  void _saveExam() {
    if (_busy || _completed) return;
    if (!widget.activity.maxScoreKnown) {
      setState(() => _scoreError = 'حدد الدرجة النهائية للامتحان أولًا.');
      return;
    }
    final score = parseAcademicScore(_score.text);
    if (score == null || score < 0 || score > widget.activity.maxScore) {
      setState(
        () => _scoreError = 'أدخل درجة من صفر إلى ${widget.activity.maxScore}',
      );
      _scoreFocus.requestFocus();
      return;
    }
    _save(score: score);
  }

  Future<void> _save({
    num? score,
    HomeworkStatus homework = HomeworkStatus.notReviewed,
  }) async {
    if (!_available || hasPendingMassarNotice(context)) return;
    final target = _context;
    final activity = widget.activity;
    final savedCallback = widget.onSaved;
    final existing = _existing(target.$1, target.$2, target.$3);
    setState(() {
      _busy = true;
      _saveError = '';
    });
    try {
      await widget.store.saveAcademic(
        AcademicRecord(
          id: existing?.id ?? '',
          studentId: target.$1,
          sessionId: target.$2,
          activityId: target.$3,
          score: score,
          maxScore: activity.kind == AcademicActivityKind.exam
              ? activity.maxScore
              : 10,
          maxScoreKnown: activity.maxScoreKnown,
          homework: homework,
          examAbsent: false,
          notes: existing?.notes ?? '',
          updatedAt: DateTime.now(),
        ),
      );
      if (mounted && _context == target) {
        _completed = true;
        savedCallback();
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.academics_page');
      if (mounted && _context == target) {
        setState(
          () => _saveError = error is CenterException
              ? error.message
              : 'تعذر حفظ الرصد. حاول مرة أخرى.',
        );
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        if (!_completed) _focusEntry();
      }
    }
  }

  Map<ShortcutActivator, Intent> get _shortcuts => {
    const SingleActivator(LogicalKeyboardKey.enter, includeRepeats: false):
        const _ArmAcademicSave(LogicalKeyboardKey.enter),
    const SingleActivator(
      LogicalKeyboardKey.numpadEnter,
      includeRepeats: false,
    ): const _ArmAcademicSave(
      LogicalKeyboardKey.numpadEnter,
    ),
    if (!_exam) ...{
      const SingleActivator(
        LogicalKeyboardKey.digit1,
        control: true,
        includeRepeats: false,
      ): const _ArmAcademicSave(
        LogicalKeyboardKey.digit1,
        HomeworkStatus.complete,
      ),
      const SingleActivator(
        LogicalKeyboardKey.digit2,
        control: true,
        includeRepeats: false,
      ): const _ArmAcademicSave(
        LogicalKeyboardKey.digit2,
        HomeworkStatus.missing,
      ),
      const SingleActivator(
        LogicalKeyboardKey.digit3,
        control: true,
        includeRepeats: false,
      ): const _ArmAcademicSave(
        LogicalKeyboardKey.digit3,
        HomeworkStatus.incomplete,
      ),
      const SingleActivator(
        LogicalKeyboardKey.numpad1,
        control: true,
        includeRepeats: false,
      ): const _ArmAcademicSave(
        LogicalKeyboardKey.numpad1,
        HomeworkStatus.complete,
      ),
      const SingleActivator(
        LogicalKeyboardKey.numpad2,
        control: true,
        includeRepeats: false,
      ): const _ArmAcademicSave(
        LogicalKeyboardKey.numpad2,
        HomeworkStatus.missing,
      ),
      const SingleActivator(
        LogicalKeyboardKey.numpad3,
        control: true,
        includeRepeats: false,
      ): const _ArmAcademicSave(
        LogicalKeyboardKey.numpad3,
        HomeworkStatus.incomplete,
      ),
    },
  };

  Widget _scoreField() => TextField(
    key: const Key('academic-quick-score'),
    controller: _score,
    focusNode: _scoreFocus,
    autofocus: true,
    enabled: _available && widget.activity.maxScoreKnown,
    textDirection: TextDirection.ltr,
    keyboardType: const TextInputType.numberWithOptions(decimal: true),
    decoration: InputDecoration(
      labelText: widget.activity.maxScoreKnown
          ? 'درجة الطالب (من ${widget.activity.maxScore})'
          : 'درجة الطالب · الدرجة النهائية غير معروفة',
      errorText: _scoreError,
      helperText:
          'اكتب الدرجة ثم Enter للحفظ والعودة إلى الكود. الصفر درجة فعلية.',
      helperMaxLines: 3,
    ),
    onChanged: (_) {
      setState(() {
        _scoreError = null;
        _saveError = '';
      });
    },
  );

  Widget _homeworkChoices() => Wrap(
    spacing: 12,
    runSpacing: 12,
    children: [
      for (final (status, label) in [
        (HomeworkStatus.complete, 'اتعمل'),
        (HomeworkStatus.missing, 'ما اتعملش'),
        (HomeworkStatus.incomplete, 'ناقص'),
      ])
        FilledButton.tonal(
          key: Key('academic-quick-homework-${status.name}'),
          focusNode: _homeworkFocus[status],
          onPressed: _available ? () => _save(homework: status) : null,
          child: Text(label),
        ),
    ],
  );

  Widget _header() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Tooltip(
        message: widget.student.name,
        child: Text(
          widget.student.name,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
        ),
      ),
      Text(
        'كود ${widget.student.code} · ${sessionLabel(widget.session)}',
        style: TextStyle(color: MassarPalette.of(context).muted),
      ),
      const SizedBox(height: 8),
      Tooltip(
        message: widget.activity.name,
        child: Text(
          '${_exam ? 'امتحان' : 'واجب'}: ${widget.activity.name}',
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
        ),
      ),
    ],
  );

  @override
  Widget build(BuildContext context) {
    final colors = MassarPalette.of(context);
    final previous = _existing(
      widget.student.id,
      widget.session.id,
      widget.activity.id,
    );
    return WorkspaceDraftRegistration(
      dirty: _dirty,
      busy: _busy || _leaving,
      child: Focus(
        onKeyEvent: _key,
        child: Shortcuts(
          shortcuts: _shortcuts,
          child: Actions(
            actions: {
              _ArmAcademicSave: CallbackAction<_ArmAcademicSave>(
                onInvoke: (intent) {
                  _arm(intent);
                  return null;
                },
              ),
            },
            child: Focus(
              focusNode: _keyboardFocus,
              child: Container(
                key: const Key('academic-quick-entry'),
                padding: const EdgeInsets.all(20),
                decoration: BoxDecoration(
                  color: colors.subtle,
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(color: colors.line),
                ),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _header(),
                    const SizedBox(height: 16),
                    if (_saveError.isNotEmpty)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: Semantics(
                          liveRegion: true,
                          child: Text(
                            'لم يُحفظ الرصد. $_saveError\n${_exam ? 'الدرجة المكتوبة موجودة' : 'لم تتغير الحالة السابقة'}؛ راجع السبب وحاول مجددًا.',
                            style: TextStyle(color: colors.error),
                          ),
                        ),
                      ),
                    if (_exam) ...[
                      _scoreField(),
                      if (!widget.activity.maxScoreKnown)
                        const Padding(
                          padding: EdgeInsets.only(top: 8),
                          child: Text(
                            'الدرجة المستوردة محفوظة. ألغِ الرصد وحدد الدرجة النهائية للامتحان قبل تسجيل نتيجة جديدة.',
                          ),
                        ),
                    ] else ...[
                      Text(
                        'الحالة السابقة: ${previous == null ? 'لم يُراجع' : homeworkLabel(previous.homework)}',
                      ),
                      const SizedBox(height: 12),
                      _homeworkChoices(),
                      const SizedBox(height: 12),
                      const Text(
                        'Ctrl + ١ اتعمل · Ctrl + ٢ ما اتعملش · Ctrl + ٣ ناقص',
                      ),
                    ],
                    if (_busy)
                      const Padding(
                        padding: EdgeInsets.only(top: 12),
                        child: Text('جارٍ حفظ الرصد…'),
                      ),
                    if (!widget.store.canAssess)
                      Padding(
                        padding: const EdgeInsets.only(top: 12),
                        child: Text(
                          'ليس لديك صلاحية للرصد.',
                          style: TextStyle(color: colors.error),
                        ),
                      ),
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 12,
                      runSpacing: 8,
                      children: [
                        if (_exam)
                          FilledButton(
                            key: const Key('academic-quick-save'),
                            onPressed:
                                _available && widget.activity.maxScoreKnown
                                ? _saveExam
                                : null,
                            child: const Text('حفظ الدرجة · Enter'),
                          ),
                        TextButton(
                          key: const Key('academic-quick-cancel'),
                          focusNode: _cancelFocus,
                          onPressed: !_busy && !_leaving && !_completed
                              ? _cancel
                              : null,
                          child: const Text('رجوع للكود · Esc'),
                        ),
                        TextButton(
                          key: const Key('academic-quick-details'),
                          focusNode: _detailsFocus,
                          onPressed: _available ? _details : null,
                          child: const Text('تفاصيل الرصد'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
