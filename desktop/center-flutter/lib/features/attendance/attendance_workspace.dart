import 'dart:async';
import '../../lan/lan_controller.dart';
import '../management/mobile_homework_dialog.dart';
import 'package:massar_center/shared/student_lookup_dialog.dart';
import 'package:massar_center/domain/student_lookup.dart'
    show normalizeStudentIdentifier, studentIdentifiers;
import 'student_spotlight_dialog.dart';
import '../management/management_widgets.dart'
    show ManagementBody, managementError;
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/document_service.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/massar_logo.dart';
import 'package:massar_center/shared/appearance.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import 'package:massar_center/shared/workspace_draft_guard.dart'
    show confirmDiscardDraft;
import 'student_editor_dialog.dart';
import 'student_transfer_dialog.dart';
import 'student_discount_dialog.dart';
import 'student_suspension_dialog.dart';
import 'session_start_dialog.dart';
import 'package:massar_center/domain/discount_calculation.dart';
import 'entry_confirmation_dialog.dart';
import 'paid_amount_dialog.dart';
import 'student_debt_dialog.dart';
import 'closed_session_dialog.dart';
import 'attendance_conflict_dialog.dart';
import 'student_history_panel.dart';
import '../management/closings_page.dart';
import '../management/review_page.dart';

enum _AttendancePurpose { registration, studentLookup }

/// Window-local navigation state. No financial request or authorization is kept.
class AttendanceWorkspaceContext {
  String? groupId, sessionId, studentId, monthPlanId;
  String? studyMonthId, preparedLessonId;
  String? originalAttendanceId, makeupSourceGroupId, closedViewSessionId;
  String? noteStudentId;
  EntryMode mode = EntryMode.single;
  bool lookupOnly = false, studentResolved = false;
  String paymentMethod = 'نقدي', cardMethod = 'نقدي';
  String searchText = '', noteDraft = '';
}

/// A single-student cashier workspace. Session stays selected between students.
class AttendanceWorkspace extends StatefulWidget {
  const AttendanceWorkspace({
    super.key,
    required this.store,
    required this.onExit,
    this.initialSessionId,
    this.workspaceContext,
    this.lanController,
  });
  final CenterStore store;
  final LanController? lanController;
  final VoidCallback onExit;
  final String? initialSessionId;
  final AttendanceWorkspaceContext? workspaceContext;

  @override
  State<AttendanceWorkspace> createState() => _AttendanceWorkspaceState();
}

class _AttendanceWorkspaceState extends State<AttendanceWorkspace> {
  Timer? _searchRefresh;
  bool _freeSearchTyping = false;
  DateTime? _lastSearchTap;
  final _search = TextEditingController();
  final _searchFocus = FocusNode();
  final _startFocus = FocusNode();
  final _notes = TextEditingController();
  final _notesFocus = FocusNode();
  String? _noteStudentId;
  String? _groupId, _sessionId, _studentId, _originalAttendanceId;
  String? _makeupSourceGroupId;
  String? _startedSessionId;
  String? _studyMonthId, _preparedLessonId;
  bool _startingSession = false;
  EntryMode _mode = EntryMode.single;
  String? _monthPlanId;
  List<GroupMonthPlan> _visibleMonthPlans(StudyGroup? selectedGroup) {
    if (selectedGroup == null) return const [];
    final plans = selectedGroup.effectiveMonthPlans;
    if (store.studyMonths.isEmpty) return plans;
    final monthIds = store.studyMonths.map((month) => month.id).toSet();
    return plans.where((plan) => monthIds.contains(plan.id)).toList();
  }

  GroupMonthPlan? _selectedMonth(StudyGroup? selectedGroup) {
    if (selectedGroup == null) return null;
    final plans = _visibleMonthPlans(selectedGroup);
    if (plans.isEmpty) return null;
    return plans.firstWhere(
      (plan) => plan.id == (_monthPlanId ?? _studyMonthId),
      orElse: () => plans.first,
    );
  }

  GroupMonthPlan? get _monthPlan =>
      _selectedMonth(_manualMakeupSource ?? group);
  int get _packageSessions => _monthPlan?.sessions ?? 4;
  int? _monthPrice(StudyGroup? selectedGroup) =>
      _selectedMonth(selectedGroup)?.price;
  String get _monthLabel => _monthPlan?.name ?? 'الشهر';
  _AttendancePurpose _purpose = _AttendancePurpose.registration;
  LogicalKeyboardKey? _heldPaymentShortcut;
  String _method = 'نقدي';
  String _cardMethod = 'نقدي';
  bool _busy = false;
  bool _shortcutConfirming = false;
  bool _discountEditing = false;
  bool _closedSessionWarning = false;
  bool _reviewing = false;
  bool _historyModal = false;
  bool _attendanceWarning = false;
  ValueNotifier<bool>? _attendanceScannerBlocked;
  ValueNotifier<bool>? _paidAmountScannerBlocked;
  bool _shortcutRetryRequired = false;
  ValueNotifier<bool>? _confirmationScannerBlocked;
  PackageConfirmationController? _packageConfirmation;
  bool _studentResolved = false;
  bool _submittingStudent = false;
  String? _inlineNotice;
  NoticeKind _inlineNoticeKind = NoticeKind.success;

  CenterStore get store => widget.store;
  MassarPalette get _palette => MassarPalette.of(context);
  bool get _lookupOnly => _purpose == _AttendancePurpose.studentLookup;
  bool get _workspaceEnabled {
    final current = session;
    if (_startingSession || current == null || current.groupId != _groupId) {
      return false;
    }
    return switch (current.status) {
      SessionStatus.open => store.sessionHasStarted(current.id),
      SessionStatus.closed => _startedSessionId == current.id,
      SessionStatus.canceled => false,
    };
  }

  bool get _canChangeContext =>
      !_startingSession &&
      !_busy &&
      !_discountEditing &&
      !_shortcutConfirming &&
      !_attendanceWarning &&
      !_closedSessionWarning &&
      !_reviewing &&
      !_historyModal &&
      _noteStudentId == null &&
      !hasPendingMassarNotice(context);
  bool get _canUseToolbar => _canChangeContext && !_submittingStudent;
  bool get _canExitWorkspace =>
      !_busy &&
      !_submittingStudent &&
      !_startingSession &&
      !_discountEditing &&
      !_shortcutConfirming &&
      !_attendanceWarning &&
      !_closedSessionWarning &&
      !_reviewing &&
      !_historyModal &&
      !hasPendingMassarNotice(context) &&
      ModalRoute.of(context)?.isCurrent == true;

  bool get _packageContextAvailable =>
      _workspaceEnabled &&
      !hasPendingMassarNotice(context) &&
      !_attendanceWarning &&
      !_lookupOnly &&
      !_reviewing &&
      !_historyModal &&
      !_busy &&
      !_discountEditing &&
      _noteStudentId == null &&
      store.canCollect &&
      student?.isSuspended != true &&
      (session?.kind == SessionKind.counted || _manualMakeupSource != null) &&
      session?.status == SessionStatus.open;
  String _countLabel(int count) => '$count حصص';

  void _chooseMonth(String id, {bool fromMenu = false}) {
    if (!_packageContextAvailable ||
        (!fromMenu && ModalRoute.of(context)?.isCurrent != true) ||
        !_visibleMonthPlans(
          _manualMakeupSource ?? group,
        ).any((plan) => plan.id == id)) {
      return;
    }
    setState(() {
      _monthPlanId = id;
      if (_manualMakeupSource != null ||
          student?.groupIds.contains(_groupId) == true &&
              _mode != EntryMode.makeup) {
        _mode = EntryMode.package;
      }
    });
    _focusCode();
  }

  StudyMonth? get _studyMonth =>
      _byId(store.studyMonths, _studyMonthId, (item) => item.id);
  PreparedLesson? get _preparedLesson => _byId(
    _studyMonth?.lessons ?? const <PreparedLesson>[],
    _preparedLessonId,
    (item) => item.id,
  );

  String _preparedLessonLabel(PreparedLesson lesson) =>
      lesson.name.trim().isEmpty
      ? 'حصة ${lesson.number}'
      : 'حصة ${lesson.number} · ${lesson.name}';

  void _selectBoundSession(LessonSession selected) {
    _sessionId = selected.id;
    _groupId = selected.groupId;
    _preparedLessonId = selected.preparedLessonId;
    _studyMonthId = selected.preparedLessonId == null
        ? null
        : store.studyMonthForLesson(selected.preparedLessonId!)?.id;
  }

  void _resolvePreparedSession() {
    _sessionId = _groupId == null || _preparedLesson == null
        ? null
        : store.sessionForPreparedLesson(_groupId!, _preparedLessonId!)?.id;
    if (_visibleMonthPlans(group).any((plan) => plan.id == _studyMonthId)) {
      _monthPlanId = _studyMonthId;
    }
  }

  LessonSession? get session =>
      store.sessionById(_sessionId) ??
      (_groupId == null || _preparedLesson == null
          ? null
          : store.sessionForPreparedLesson(_groupId!, _preparedLessonId!));
  StudyGroup? get group => store.groupById(_groupId);
  Student? get student => store.studentById(_studentId);
  T? _byId<T>(List<T> items, String? id, String Function(T) idOf) {
    for (final item in items) {
      if (idOf(item) == id) return item;
    }
    return null;
  }

  @override
  void initState() {
    super.initState();
    _searchFocus.onKeyEvent = _paymentKey;
    _restoreWorkspaceContext();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) {
        return;
      }
      if (_noteStudentId != null && _workspaceEnabled) {
        _notesFocus.requestFocus();
      } else {
        _focusCode();
      }
    });
  }

  void _restoreWorkspaceContext() {
    final saved = widget.workspaceContext;
    final explicitSession = _byId(
      store.sessions,
      widget.initialSessionId,
      (session) => session.id,
    );
    final explicitGroup = _byId(
      store.groupsForRegion(),
      explicitSession?.groupId,
      (group) => group.id,
    );
    final previousGroup = _byId(
      store.groupsForRegion(),
      saved?.groupId,
      (group) => group.id,
    );
    _groupId = explicitGroup?.id ?? previousGroup?.id;
    final previousSession = _byId(
      store.sessions,
      saved?.sessionId,
      (session) => session.id,
    );
    final selectedSession = explicitGroup != null
        ? explicitSession
        : previousSession;
    if (selectedSession != null &&
        selectedSession.groupId == _groupId &&
        selectedSession.status != SessionStatus.canceled) {
      _selectBoundSession(selectedSession);
    } else {
      final months = [...store.studyMonths]
        ..sort((a, b) => b.number.compareTo(a.number));
      final selectedMonth =
          _byId(months, saved?.studyMonthId, (item) => item.id) ??
          months.firstOrNull;
      _studyMonthId = selectedMonth?.id;
      final lessons = [...?selectedMonth?.lessons]
        ..sort((a, b) => a.number.compareTo(b.number));
      _preparedLessonId =
          _byId(lessons, saved?.preparedLessonId, (item) => item.id)?.id ??
          lessons.firstOrNull?.id;
      _resolvePreparedSession();
    }
    if (saved == null ||
        saved.groupId != _groupId ||
        saved.sessionId != session?.id) {
      return;
    }
    if (session?.status == SessionStatus.closed &&
        saved.closedViewSessionId == session?.id) {
      _startedSessionId = saved.closedViewSessionId;
    }
    _purpose = saved.lookupOnly
        ? _AttendancePurpose.studentLookup
        : _AttendancePurpose.registration;
    _method = saved.paymentMethod;
    _cardMethod = saved.cardMethod;
    _search.text = saved.searchText;
    final selected = _byId(
      store.students,
      saved.studentId,
      (student) => student.id,
    );
    final current = session;
    if (selected != null) {
      _studentId = selected.id;
      _studentResolved = saved.studentResolved && _search.text.trim().isEmpty;
      _mode = saved.mode;
    }
    if (selected != null && current != null) {
      final eligibleAbsences = store.eligibleMakeups(selected.id, current.id);
      if (eligibleAbsences.any(
        (record) => record.id == saved.originalAttendanceId,
      )) {
        _originalAttendanceId = saved.originalAttendanceId;
      }
      if (store
          .eligibleMakeupSourceGroups(selected.id, current.id)
          .any((source) => source.id == saved.makeupSourceGroupId)) {
        _makeupSourceGroupId = saved.makeupSourceGroupId;
      }
    }
    final billingGroup = _manualMakeupSource ?? group;
    if (selected != null &&
        _mode == EntryMode.makeup &&
        _originalAttendanceId == null &&
        _manualMakeupSource == null) {
      _mode = _preferredMode(selected);
    }
    if (_visibleMonthPlans(
      billingGroup,
    ).any((plan) => plan.id == saved.monthPlanId)) {
      _monthPlanId = saved.monthPlanId;
    }
    if (selected != null &&
        saved.noteStudentId == selected.id &&
        store.canCollect &&
        _workspaceEnabled) {
      _noteStudentId = selected.id;
      _notes.text = saved.noteDraft;
    }
  }

  void _captureWorkspaceContext() {
    final saved = widget.workspaceContext;
    if (saved == null) {
      return;
    }
    saved
      ..groupId = _groupId
      ..sessionId = session?.id
      ..studentId = _studentId
      ..monthPlanId = _monthPlanId
      ..studyMonthId = _studyMonthId
      ..preparedLessonId = _preparedLessonId
      ..originalAttendanceId = _originalAttendanceId
      ..makeupSourceGroupId = _makeupSourceGroupId
      ..closedViewSessionId = session?.status == SessionStatus.closed
          ? _startedSessionId
          : null
      ..mode = _mode
      ..lookupOnly = _lookupOnly
      ..paymentMethod = _method
      ..cardMethod = _cardMethod
      ..searchText = _search.text
      ..noteDraft = _notes.text
      ..noteStudentId = _noteStudentId
      ..studentResolved = _studentResolved;
  }

  void _resetStudentContext() {
    _startedSessionId = null;
    _studentId = null;
    _studentResolved = false;
    _originalAttendanceId = null;
    _shortcutRetryRequired = false;
    _heldPaymentShortcut = null;
    _monthPlanId = null;
    _mode = EntryMode.single;
    _purpose = _AttendancePurpose.registration;
    _search.clear();
    _inlineNotice = null;
    _searchFocus.unfocus();
  }

  void _changeGroup(String? id) {
    if (!_canChangeContext || id == _groupId) {
      return;
    }
    setState(() {
      _resetStudentContext();
      _groupId = id;
      _resolvePreparedSession();
    });
    _focusCode();
  }

  void _changeStudyMonth(String? id) {
    if (!_canChangeContext || id == _studyMonthId) {
      return;
    }
    setState(() {
      _resetStudentContext();
      _studyMonthId = id;
      final lessons = [...?_studyMonth?.lessons]
        ..sort((a, b) => a.number.compareTo(b.number));
      _preparedLessonId = lessons.firstOrNull?.id;
      _resolvePreparedSession();
    });
    _focusCode();
  }

  void _changePreparedLesson(String? id) {
    if (!_canChangeContext || id == _preparedLessonId) {
      return;
    }
    setState(() {
      _resetStudentContext();
      _preparedLessonId = id;
      _resolvePreparedSession();
    });
    _focusCode();
  }

  Future<void> _confirmSessionStart({bool changedContext = false}) async {
    final prepared = _preparedLesson, selectedMonth = _studyMonth;
    final selectedGroup = group, current = session;
    if (!_canChangeContext ||
        selectedGroup == null ||
        (prepared == null && current == null) ||
        current?.status == SessionStatus.canceled ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    if (current?.status == SessionStatus.closed) {
      await _warnClosedSession();
      return;
    }
    final preparedId = prepared?.id;
    final groupId = selectedGroup.id;
    final preview =
        current ??
        LessonSession(
          groupId: groupId,
          preparedLessonId: preparedId,
          name: prepared?.name ?? '',
          monthNumber: selectedMonth?.number ?? 1,
          number: prepared!.number,
          kind: prepared.kind,
          extraPrice: prepared.extraPrice,
          startsAt: DateTime.now(),
          startsAtKnown: false,
          createdAt: DateTime.now(),
        );
    setState(() => _startingSession = true);
    try {
      var confirmed = current != null && store.sessionHasStarted(current.id);
      if (!confirmed) {
        final route = DialogRoute<bool>(
          context: context,
          barrierDismissible: false,
          builder: (_) => SessionStartDialog(
            session: preview,
            groupLabel: store.groupLabel(groupId),
            changedContext: changedContext,
          ),
        );
        confirmed = await Navigator.of(context).push(route) == true;
        await route.completed;
      }
      if (!mounted ||
          !confirmed ||
          _groupId != groupId ||
          _preparedLessonId != preparedId) {
        return;
      }
      final LessonSession started;
      if (preparedId != null) {
        started = await store.startPreparedLesson(
          groupId: groupId,
          preparedLessonId: preparedId,
        );
      } else {
        final existingSession = current!;
        await store.startSession(existingSession.id);
        started = existingSession;
      }
      if (mounted) {
        setState(() {
          _selectBoundSession(started);
          if (_visibleMonthPlans(
            group,
          ).any((plan) => plan.id == _studyMonthId)) {
            _monthPlanId = _studyMonthId;
          }
        });
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.attendance_workspace');
      if (mounted) {
        await _feedback(
          error is CenterException
              ? error.message
              : 'تعذر بدء الحصة. حاول مرة أخرى.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) {
        setState(() => _startingSession = false);
        if (_workspaceEnabled) {
          _focusCode();
        } else {
          _startFocus.requestFocus();
        }
      }
    }
  }

  void _focusCode() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted &&
          _workspaceEnabled &&
          !hasPendingMassarNotice(context) &&
          !_attendanceWarning &&
          !_closedSessionWarning &&
          !_reviewing &&
          !_historyModal &&
          !_shortcutConfirming &&
          !_discountEditing &&
          _noteStudentId == null &&
          ModalRoute.of(context)?.isCurrent == true) {
        _searchFocus.requestFocus();
      }
    });
  }

  bool get _canEnter {
    final selected = student, current = session;
    if (!_workspaceEnabled ||
        hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _busy ||
        _closedSessionWarning ||
        _reviewing ||
        _historyModal ||
        _shortcutConfirming ||
        _lookupOnly ||
        !_studentResolved ||
        _noteStudentId != null ||
        !store.canCollect ||
        selected == null ||
        selected.isSuspended ||
        current == null ||
        current.status != SessionStatus.open ||
        _search.text.trim().isNotEmpty ||
        store
                .attendancesForStudent(selected.id)
                .any(
                  (a) =>
                      a.sessionId == current.id &&
                      a.status != AttendanceStatus.absent,
                ) &&
            !store.attendanceNeedsPayment(selected.id, current.id)) {
      return false;
    }
    final mode = _effectiveMode(selected, current);
    return mode == EntryMode.makeup
        ? _manualMakeupSource != null ||
              store
                  .eligibleMakeups(selected.id, current.id)
                  .any((a) => a.id == _originalAttendanceId)
        : selected.groupIds.contains(current.groupId);
  }

  KeyEventResult _paymentKey(FocusNode node, KeyEvent event) {
    if (!_workspaceEnabled) return KeyEventResult.handled;
    final key = event.logicalKey;
    if (_attendanceWarning && EntryConfirmationDialog.isScannerText(event)) {
      _attendanceScannerBlocked?.value = true;
    }
    if (_historyModal && EntryConfirmationDialog.isScannerText(event)) {
      _paidAmountScannerBlocked?.value = true;
    }
    if (hasPendingMassarNotice(context) ||
        _submittingStudent ||
        _attendanceWarning ||
        _closedSessionWarning ||
        _reviewing ||
        _historyModal ||
        _discountEditing) {
      return KeyEventResult.handled;
    }
    if (_shortcutConfirming) {
      final packageConfirmation = _packageConfirmation;
      if (packageConfirmation != null &&
          (key == LogicalKeyboardKey.enter ||
              key == LogicalKeyboardKey.numpadEnter)) {
        final keyboard = HardwareKeyboard.instance;
        if (event is KeyDownEvent &&
            !keyboard.isControlPressed &&
            !keyboard.isAltPressed &&
            !keyboard.isMetaPressed &&
            !keyboard.isShiftPressed) {
          packageConfirmation.requestPendingConfirmation();
        }
      } else if (packageConfirmation?.handleSequenceKey(event) == true) {
        // The same sequence state handles keys before the dialog gains focus.
      } else if (EntryConfirmationDialog.isScannerText(event)) {
        _confirmationScannerBlocked?.value = true;
      }
      return KeyEventResult.handled;
    }
    if (_freeSearchTyping &&
        key != LogicalKeyboardKey.enter &&
        key != LogicalKeyboardKey.numpadEnter) {
      return KeyEventResult.ignored;
    }
    final keyboard = HardwareKeyboard.instance;
    if ((key == LogicalKeyboardKey.keyL || key == LogicalKeyboardKey.keyN) &&
        !_lookupOnly &&
        student?.isSuspended == true &&
        _studentResolved &&
        _search.text.isEmpty &&
        !_busy &&
        _noteStudentId == null &&
        store.canCollect &&
        !keyboard.isControlPressed &&
        !keyboard.isShiftPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        ModalRoute.of(context)?.isCurrent == true) {
      if (event is KeyDownEvent) {
        _heldPaymentShortcut = key;
        _reactivateSelectedStudent();
      }
      return KeyEventResult.handled;
    }
    if (key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter) {
      if (event is KeyDownEvent &&
          !event.synthesized &&
          !keyboard.isControlPressed &&
          !keyboard.isAltPressed &&
          !keyboard.isMetaPressed &&
          !keyboard.isShiftPressed) {
        _submitStudent(_search.text);
      }
      return KeyEventResult.handled;
    }
    if (key == _heldPaymentShortcut && event is! KeyDownEvent) {
      if (event is KeyUpEvent) _heldPaymentShortcut = null;
      return KeyEventResult.handled;
    }
    if (key == LogicalKeyboardKey.keyC &&
        !keyboard.isControlPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        !keyboard.isShiftPressed &&
        _canChangeCard) {
      if (event is KeyDownEvent) {
        _heldPaymentShortcut = key;
        _collectCenterFee();
      }
      return KeyEventResult.handled;
    }
    if (key == LogicalKeyboardKey.keyS &&
        !keyboard.isControlPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        !keyboard.isShiftPressed &&
        _canEditDiscount &&
        !store.students.any(
          (student) => studentIdentifiers(student).any(
            (identifier) =>
                normalizeStudentIdentifier(identifier).startsWith('s'),
          ),
        )) {
      if (event is KeyDownEvent) {
        _heldPaymentShortcut = key;
        _editDiscount();
      }
      return KeyEventResult.handled;
    }
    if ((key == LogicalKeyboardKey.keyL || key == LogicalKeyboardKey.keyN) &&
        !keyboard.isControlPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        !keyboard.isShiftPressed &&
        _readyForAttendanceWarning &&
        _sameSessionAttendance != null) {
      if (event is KeyDownEvent) {
        _heldPaymentShortcut = key;
        _warnRecordedPayment();
      }
      return KeyEventResult.handled;
    }
    if ((key == LogicalKeyboardKey.keyL || key == LogicalKeyboardKey.keyN) &&
        !_lookupOnly &&
        !keyboard.isControlPressed &&
        !keyboard.isAltPressed &&
        !keyboard.isMetaPressed &&
        !keyboard.isShiftPressed &&
        _studentResolved &&
        student != null &&
        !_busy &&
        !_discountEditing &&
        _noteStudentId == null &&
        store.canCollect &&
        _search.text.isEmpty &&
        session?.status == SessionStatus.closed &&
        ModalRoute.of(context)?.isCurrent == true) {
      if (event is KeyDownEvent) {
        _heldPaymentShortcut = key;
        _warnClosedSession();
      }
      return KeyEventResult.handled;
    }
    if ((key != LogicalKeyboardKey.keyL && key != LogicalKeyboardKey.keyN) ||
        _lookupOnly ||
        keyboard.isControlPressed ||
        keyboard.isAltPressed ||
        keyboard.isMetaPressed ||
        student == null ||
        _noteStudentId != null ||
        _search.text.isNotEmpty ||
        ModalRoute.of(context)?.isCurrent != true ||
        !_canEnter) {
      return KeyEventResult.ignored;
    }
    // Consume held keys as well; one physical press can collect at most once.
    if (event is KeyDownEvent && _canEnter) {
      _heldPaymentShortcut = key;
      final current = session!;
      final mode = _effectiveMode(student!, current);
      if (key == LogicalKeyboardKey.keyL) {
        if (mode == EntryMode.makeup) {
          _confirmShortcut(
            EntryMode.makeup,
            showConfirmation: keyboard.isShiftPressed,
          );
        } else if (_isCenterOnly(student!, current) ||
            store.hasRetainedSessionPayment(student!.id, current.id) ||
            current.kind != SessionKind.counted ||
            store.eligibleRemainingFor(student!.id, current.id) == 0) {
          _confirmShortcut(
            EntryMode.single,
            showConfirmation: keyboard.isShiftPressed,
          );
        } else {
          _feedback('الباقة تغطي الحصة. استخدم N للتسجيل من الرصيد.');
        }
      } else if (_isCenterOnly(student!, current)) {
        _feedback(
          'الطالب معفى من دفع المدرس. استخدم C لتحصيل رسوم السنتر فقط.',
        );
      } else if (store.hasRetainedSessionPayment(student!.id, current.id)) {
        _feedback('الحصة مسددة مسبقًا. استخدم L لتسجيل الحضور دون دفع جديد.');
      } else if (mode == EntryMode.makeup && _manualMakeupSource != null) {
        final source = _manualMakeupSource!;
        final remaining = store.eligibleMakeupRemainingFor(
          student!.id,
          current.id,
          source.id,
        );
        _confirmShortcut(
          remaining > 0 ? EntryMode.makeup : EntryMode.package,
          showConfirmation: keyboard.isShiftPressed,
        );
      } else if (current.kind == SessionKind.counted &&
          mode != EntryMode.makeup) {
        _confirmShortcut(
          EntryMode.package,
          showConfirmation: keyboard.isShiftPressed,
        );
      } else {
        _feedback('استخدم L لتأكيد هذا النوع من الدخول.');
      }
    }
    return KeyEventResult.handled;
  }

  @override
  void dispose() {
    _searchRefresh?.cancel();
    _captureWorkspaceContext();
    _search.dispose();
    _searchFocus.dispose();
    _startFocus.dispose();
    _notes.dispose();
    _notesFocus.dispose();
    super.dispose();
  }

  Future<void> _selectStudent(Student value) async {
    if (!_workspaceEnabled ||
        hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _historyModal ||
        _shortcutConfirming ||
        _closedSessionWarning ||
        _reviewing ||
        _busy ||
        _noteStudentId != null) {
      return;
    }
    _applyStudentSelection(value);
    await _prepareSelectedStudent(value);
  }

  void _applyStudentSelection(Student value) {
    setState(() {
      _freeSearchTyping = false;
      _lastSearchTap = null;
      _studentId = value.id;
      _cardMethod = 'نقدي';
      _monthPlanId = null;
      _studentResolved = true;
      _shortcutRetryRequired = false;
      _originalAttendanceId = null;
      _mode = _preferredMode(value);
      _makeupSourceGroupId = null;
      _search.clear();
      _inlineNotice = null;
    });
  }

  StudyGroup? get _manualMakeupSource {
    final selected = student, current = session;
    if (selected == null || current == null || _originalAttendanceId != null) {
      return null;
    }
    final sources = store.eligibleMakeupSourceGroups(selected.id, current.id);
    return sources
            .where((group) => group.id == _makeupSourceGroupId)
            .firstOrNull ??
        sources.firstOrNull;
  }

  Future<void> _prepareSelectedStudent(Student selected) async {
    final targetSessionId = session?.id;
    if (selected.isSuspended && !_lookupOnly && store.canCollect) {
      final reactivated = await _reviewStudentStatus(
        selected,
        StudentSuspensionAction.reactivate,
      );
      if (!mounted) return;
      if (!reactivated) {
        _nextStudent();
        return;
      }
    }
    if (!mounted ||
        student?.id != selected.id ||
        !_studentResolved ||
        session?.id != targetSessionId ||
        !_workspaceEnabled) {
      return;
    }
    _focusCode();
  }

  EntryMode _preferredMode(Student value) {
    if (_hasPendingSourceMakeup(value, session)) return EntryMode.makeup;
    if (!value.groupIds.contains(_groupId)) return EntryMode.makeup;
    if (session != null &&
        store.hasRetainedSessionPayment(value.id, session!.id)) {
      return EntryMode.single;
    }
    if (_isCenterOnly(value, session)) return EntryMode.single;
    return session?.kind == SessionKind.counted &&
            store.eligibleRemainingFor(value.id, session!.id) > 0
        ? EntryMode.package
        : EntryMode.single;
  }

  EntryMode _effectiveMode(Student value, LessonSession current) {
    if (_hasPendingSourceMakeup(value, current)) return EntryMode.makeup;
    if (!value.groupIds.contains(current.groupId)) return EntryMode.makeup;
    if (store.hasRetainedSessionPayment(value.id, current.id)) {
      return EntryMode.single;
    }
    if (_isCenterOnly(value, current) && _mode != EntryMode.makeup) {
      return EntryMode.single;
    }
    if (_mode == EntryMode.makeup) return EntryMode.makeup;
    if (current.kind != SessionKind.counted) return EntryMode.single;
    return store.eligibleRemainingFor(value.id, current.id) > 0
        ? EntryMode.package
        : _mode;
  }

  bool _hasPendingSourceMakeup(Student value, LessonSession? current) =>
      current != null &&
      store.attendanceNeedsPayment(value.id, current.id) &&
      store
          .attendancesForStudent(value.id)
          .any(
            (record) =>
                record.sessionId == current.id &&
                record.status == AttendanceStatus.makeup &&
                record.makeupSourceGroupId != null,
          );

  bool _isCenterOnly(Student value, LessonSession? current) =>
      !value.centerFeeEnabled &&
      (value.centerOnly ||
          current != null &&
              store
                  .attendancesForStudent(value.id)
                  .any(
                    (record) =>
                        record.sessionId == current.id && record.centerFeeOnly,
                  ));

  int _centerFeeAmount(Student value, LessonSession? current) => current == null
      ? value.centerFeeAmount
      : store.centerFeeRemainingFor(value.id, current.id);

  void _nextStudent() {
    if (!_workspaceEnabled ||
        hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _busy ||
        _closedSessionWarning ||
        _reviewing ||
        _historyModal ||
        _shortcutConfirming ||
        _noteStudentId != null) {
      return;
    }
    setState(() {
      _studentId = null;
      _cardMethod = 'نقدي';
      _monthPlanId = null;
      _studentResolved = false;
      _shortcutRetryRequired = false;
      _originalAttendanceId = null;
      _search.clear();
      _inlineNotice = null;
    });
    _focusCode();
  }

  List<Student> _lookupCandidates(String input) =>
      store.lookupReceptionStudents(input);

  List<Student> get _matches =>
      _lookupCandidates(_search.text).take(6).toList();

  Future<bool> _scan(String raw) async {
    if (!_workspaceEnabled ||
        hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _closedSessionWarning ||
        _reviewing ||
        _historyModal ||
        _shortcutConfirming ||
        _busy ||
        _noteStudentId != null) {
      return false;
    }
    final value = raw.trim();
    if (value.isEmpty) {
      return false;
    }
    final matches = _lookupCandidates(value);
    if (matches.length == 1) {
      await _selectStudent(matches.single);
      return mounted &&
          _studentId == matches.single.id &&
          _studentResolved &&
          _search.text.trim().isEmpty;
    }
    setState(() => _studentResolved = false);
    if (matches.length > 1) {
      setState(() => _busy = true);
      Student? selected;
      try {
        selected = await chooseMatchingStudent(context, matches);
      } finally {
        if (mounted) setState(() => _busy = false);
      }
      if (!mounted) {
        return false;
      }
      if (selected != null && _search.text.trim() == value) {
        await _selectStudent(selected);
        return mounted &&
            _studentId == selected.id &&
            _studentResolved &&
            _search.text.trim().isEmpty;
      } else {
        _focusCode();
      }
      return false;
    }
    await _feedback(
      'لم نجد الطالب. راجع الكود أو الباركود أو أضفه من الاختصار.',
      kind: NoticeKind.warning,
    );
    _focusCode();
    return false;
  }

  Future<void> _submitStudent(String raw, {Student? chosenStudent}) async {
    _searchRefresh?.cancel();
    if (!_workspaceEnabled ||
        hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _busy ||
        _closedSessionWarning ||
        _reviewing ||
        _historyModal ||
        _shortcutConfirming ||
        _submittingStudent ||
        _noteStudentId != null ||
        (!_searchFocus.hasFocus && chosenStudent == null) ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    final targetSessionId = session?.id, targetGroupId = _groupId;
    final purpose = _purpose;
    setState(() => _submittingStudent = true);
    try {
      if (chosenStudent != null) {
        final selected = store.studentById(chosenStudent.id);
        if (selected == null ||
            !_lookupCandidates(raw).any((item) => item.id == selected.id)) {
          return;
        }
        await _selectStudent(selected);
        if (!mounted || !_studentResolved || _studentId != selected.id) {
          return;
        }
      } else if (raw.trim().isNotEmpty && !(await _scan(raw))) {
        return;
      }
      if (!mounted ||
          session?.id != targetSessionId ||
          _groupId != targetGroupId ||
          _purpose != purpose ||
          _lookupOnly) {
        return;
      }
      if (student?.isSuspended == true) {
        await _reactivateSelectedStudent();
        return;
      }
      if (_readyForAttendanceWarning &&
          store.attendanceNeedsPayment(student!.id, session!.id)) {
        await _feedback(
          'حضور الطالب مسجل بالفعل — غير مدفوع. استخدم L للحصة أو N للشهر لتسديده؛ لن نكرر الحضور.',
          kind: NoticeKind.warning,
        );
        _focusCode();
        return;
      }
      if (_warnSameSessionAttendance()) {
        return;
      }
      if (session?.status == SessionStatus.closed) {
        await _warnClosedSession();
        return;
      }
      if (_shortcutRetryRequired) {
        await _feedback(
          'لم يتم التسجيل. راجع الحساب واضغط L أو N للتأكيد من جديد.',
          kind: NoticeKind.warning,
        );
        return;
      }
      if (_canEnter) {
        await _collect();
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.attendance_workspace');
      if (mounted) {
        await _feedback(managementError(error), kind: NoticeKind.error);
      }
    } finally {
      if (mounted) {
        setState(() => _submittingStudent = false);
        _focusCode();
      }
    }
  }

  Future<void> _openSpotlight() async {
    if (!_canUseToolbar ||
        !_workspaceEnabled ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    final targetSession = session?.id;
    final targetGroup = _groupId;
    setState(() => _historyModal = true);
    StudentSpotlightSelection? selection;
    try {
      final route = DialogRoute<StudentSpotlightSelection>(
        context: context,
        builder: (_) => StudentSpotlightDialog(
          store: store,
          canRegister:
              store.canCollect && session?.status == SessionStatus.open,
        ),
      );
      selection = await Navigator.of(context).push(route);
      await route.completed;
    } finally {
      if (mounted) setState(() => _historyModal = false);
    }
    if (!mounted) return;
    if (selection == null ||
        targetSession != session?.id ||
        targetGroup != _groupId) {
      _focusCode();
      return;
    }
    if (selection.register) {
      setState(() => _purpose = _AttendancePurpose.registration);
      await _submitStudent(
        selection.student.code,
        chosenStudent: selection.student,
      );
    } else {
      await _selectStudent(selection.student);
      _focusCode();
    }
  }

  void _setPurpose(_AttendancePurpose purpose) {
    if (hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _busy ||
        _closedSessionWarning ||
        _reviewing ||
        _historyModal ||
        _noteStudentId != null) {
      return;
    }
    setState(() {
      _purpose = purpose;
      _search.clear();
    });
    _focusCode();
    if (!_lookupOnly) _warnClosedSession();
  }

  Future<void> _warnClosedSession() async {
    final current = session;
    if (hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _lookupOnly ||
        _discountEditing ||
        _busy ||
        _discountEditing ||
        _shortcutConfirming ||
        _closedSessionWarning ||
        _reviewing ||
        _historyModal ||
        _noteStudentId != null ||
        current == null ||
        current.status != SessionStatus.closed ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    _closedSessionWarning = true;
    var startChosenSession = false;
    final openSessions =
        store.sessions
            .where(
              (item) =>
                  item.groupId == current.groupId &&
                  item.status == SessionStatus.open,
            )
            .toList()
          ..sort((a, b) => a.startsAt.compareTo(b.startsAt));
    try {
      final choice = await showDialog<ClosedSessionChoice>(
        context: context,
        builder: (_) => ClosedSessionDialog(
          session: current,
          groupLabel: store.groupLabel(current.groupId),
          openSessions: openSessions,
          canReopen: store.canCollect,
          hasFinancialClosing: store.closings.any(
            (item) => item.sessionId == current.id,
          ),
        ),
      );
      if (!mounted || choice == null) return;
      if (choice.reopenCurrent) {
        final reopened = await _run(
          () async {
            await store.reopenSession(current.id);
            await store.startSession(current.id);
          },
          'الحصة اتفتحت. يمكنك متابعة التحضير دون تسجيل حضور أو دفع تلقائيًا.',
        );
        if (reopened && mounted) {
          setState(() {
            _selectBoundSession(current);
            _startedSessionId = null;
            _originalAttendanceId = null;
            if (student != null) _mode = _preferredMode(student!);
          });
        }
      } else if (choice.lookupOnly) {
        setState(() {
          _purpose = _AttendancePurpose.studentLookup;
          _startedSessionId = current.id;
          _search.clear();
        });
      } else {
        final chosen = _byId(
          store.sessions,
          choice.sessionId,
          (item) => item.id,
        );
        if (chosen?.groupId == current.groupId &&
            chosen?.status == SessionStatus.open) {
          setState(() {
            _resetStudentContext();
            _selectBoundSession(chosen!);
          });
          startChosenSession = true;
        }
      }
    } finally {
      if (mounted) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) return;
          setState(() => _closedSessionWarning = false);
          if (startChosenSession) {
            _confirmSessionStart(changedContext: true);
          } else {
            _focusCode();
          }
        });
        WidgetsBinding.instance.scheduleFrame();
      }
    }
  }

  String get _enterHint {
    if (!_workspaceEnabled) {
      return 'اختار الحصة واضغط «ابدأ الحصة» لتفعيل التسجيل.';
    }
    if (_lookupOnly) return 'عرض البيانات والسجل فقط؛ لا يُسجل حضور أو دفع.';
    if (_busy) return 'جارٍ حفظ العملية…';
    if (_submittingStudent) return 'جارٍ فتح الطالب وتسجيل الحضور…';
    if (_noteStudentId != null) return 'احفظ الملاحظة أو ألغِ التعديل أولًا.';
    final current = session, selected = student;
    if (current == null || current.status != SessionStatus.open) {
      return 'يمكنك البحث وعرض البيانات. اختار حصة مفتوحة للتحضير.';
    }
    if (selected == null || !_studentResolved || _search.text.isNotEmpty) {
      return 'Enter لفتح الطالب وتسجيل حضوره مباشرة؛ الدفع مستقل عبر L أو N.';
    }
    if (store.attendanceNeedsPayment(selected.id, current.id)) {
      final status = _effectiveMode(selected, current) == EntryMode.makeup
          ? 'معوّض'
          : 'حاضر';
      if (_isCenterOnly(selected, current)) {
        return '$status — باقي رسوم السنتر ${money(_centerFeeAmount(selected, current))}. C للسداد؛ الحضور محفوظ.';
      }
      return '$status — غير مدفوع. L للحصة أو N للشهر؛ Enter لن يكرر الحضور.';
    }
    if (_shortcutRetryRequired) {
      return 'لم يتم التسجيل. راجع الحساب واضغط L أو N للتأكيد من جديد.';
    }
    if (!_canEnter) return 'راجع حالة الطالب؛ لن يتكرر الحضور أو التحصيل.';
    return '${_enterActionLabel(selected, current)} L / N · Esc للإلغاء.';
  }

  String _enterActionLabel(Student selected, LessonSession current) {
    if (_isCenterOnly(selected, current)) {
      return 'Enter للحضور فقط؛ C لتحصيل رسوم السنتر ${money(_centerFeeAmount(selected, current))}. المدرس معفى بالكامل.';
    }
    if (store.hasRetainedSessionPayment(selected.id, current.id)) {
      return 'مسددة مسبقًا — تسجيل حضور دون دفع جديد.';
    }
    if (_effectiveMode(selected, current) == EntryMode.makeup) {
      return 'Enter للتحضير؛ رصيد المجموعة الأصلية يُستخدم إن توفر. L لدفع الحصة، N للباقة.';
    }
    if (current.kind == SessionKind.free) {
      return 'Enter: تسجيل الحضور المجاني.';
    }
    if (current.kind == SessionKind.counted &&
        store.eligibleRemainingFor(selected.id, current.id) > 0) {
      return 'Enter: تسجيل من الباقة بدون دفع جديد.';
    }
    if (_effectiveMode(selected, current) == EntryMode.package) {
      final price = _monthPrice(group);
      if (price == null) {
        return 'حدد سعر $_monthLabel (${_countLabel(_packageSessions)}) في المجموعة أولًا.';
      }
      return 'Enter للتحضير؛ N لشراء $_monthLabel (${_countLabel(_packageSessions)}) · ${money(_discounted(price, selected.discountPercent))}.';
    }
    final price = current.kind == SessionKind.extra
        ? current.extraPrice
        : group!.sessionPrice;
    final amount = money(_discounted(price, selected.discountPercent));
    return 'Enter للتحضير بدون دفع؛ L لدفع $amount.';
  }

  Future<void> _editStudent({
    bool create = false,
    bool focusTwin = false,
  }) async {
    if (!_workspaceEnabled ||
        hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _busy ||
        _closedSessionWarning ||
        _reviewing ||
        _historyModal ||
        _shortcutConfirming ||
        _noteStudentId != null ||
        !store.canCollect ||
        (!create && student == null) ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    setState(() => _historyModal = true);
    String? id;
    try {
      id = await showDialog<String>(
        context: context,
        barrierDismissible: false,
        builder: (_) => StudentEditorDialog(
          store: store,
          student: create ? null : student,
          initialGroupId: _groupId,
          focusTwin: focusTwin,
          cairo: false,
        ),
      );
    } finally {
      if (mounted) setState(() => _historyModal = false);
    }
    if (!mounted) return;
    _focusCode();
    if (id == null) return;
    final saved = _byId(store.students, id, (item) => item.id);
    if (saved != null) {
      if (create) {
        _selectStudent(saved);
      } else {
        _applyStudentSelection(saved);
        _focusCode();
      }
    }
  }

  Future<void> _transferStudent() async {
    final selected = student;
    if (!_workspaceEnabled ||
        !_canChangeContext ||
        !store.canCollect ||
        !_studentResolved ||
        selected == null ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    final sourceId =
        _manualMakeupSource?.id ??
        (selected.groupIds.contains(_groupId)
            ? _groupId
            : selected.groupIds.firstOrNull);
    setState(() => _historyModal = true);
    bool transferred = false;
    try {
      final route = DialogRoute<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => StudentTransferDialog(
          store: store,
          studentId: selected.id,
          initialSourceGroupId: sourceId,
          initialTargetGroupId: _groupId,
        ),
      );
      transferred = await Navigator.of(context).push(route) ?? false;
      await route.completed;
    } finally {
      if (mounted) setState(() => _historyModal = false);
    }
    if (!mounted) return;
    if (transferred) {
      final saved = _byId(store.students, selected.id, (value) => value.id);
      if (saved != null) _applyStudentSelection(saved);
      await _feedback(
        'تم نقل الطالب. لم يتغير حضوره أو دفعه المسجل.',
        kind: NoticeKind.success,
      );
    }
    _focusCode();
  }

  Future<void> _feedback(
    String message, {
    NoticeKind kind = NoticeKind.info,
    String? title,
  }) async {
    if (!mounted) return;
    if (kind == NoticeKind.success || kind == NoticeKind.info) {
      setState(() {
        _inlineNotice = message;
        _inlineNoticeKind = kind;
      });
      _focusCode();
      return;
    }
    await showMassarNotice(context, message, kind: kind, title: title);
    if (mounted) _focusCode();
  }

  Future<bool> _run(Future<void> Function() action, String? success) async {
    if (_discountEditing || _busy || hasPendingMassarNotice(context)) {
      return false;
    }
    setState(() => _busy = true);
    try {
      await action();
      if (success != null) {
        await _feedback(success, kind: NoticeKind.success);
      }
      return true;
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.attendance_workspace');
      await _feedback(
        error is CenterException
            ? error.message
            : 'تعذر إتمام العملية. حاول مرة أخرى.',
        kind: NoticeKind.error,
      );
      return false;
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        _focusCode();
      }
    }
  }

  bool get _readyForAttendanceWarning =>
      mounted &&
      !_lookupOnly &&
      !_busy &&
      !_attendanceWarning &&
      !_discountEditing &&
      !_closedSessionWarning &&
      !_reviewing &&
      !_historyModal &&
      !_shortcutConfirming &&
      !hasPendingMassarNotice(context) &&
      _noteStudentId == null &&
      _studentResolved &&
      _search.text.trim().isEmpty &&
      student != null &&
      session != null &&
      session!.status != SessionStatus.canceled &&
      store.canCollect &&
      ModalRoute.of(context)?.isCurrent == true;

  AttendanceRecord? get _sameSessionAttendance {
    final selected = student, current = session;
    if (selected == null ||
        current == null ||
        store.attendanceNeedsPayment(selected.id, current.id)) {
      return null;
    }
    return store
        .attendanceConflictsFor(selected.id, current.id)
        .where((record) => record.sessionId == current.id)
        .firstOrNull;
  }

  bool _warnSameSessionAttendance() {
    if (!_readyForAttendanceWarning) return false;
    final previous = _sameSessionAttendance;
    if (previous == null) return false;
    final selected = student!, current = session!;
    setState(() => _attendanceWarning = true);
    _showSameSessionWarning(selected, current, previous);
    return true;
  }

  bool _warnRecordedPayment() {
    if (!_readyForAttendanceWarning) return false;
    if (_isCenterOnly(student!, session!) &&
        store.centerFeeRemainingFor(student!.id, session!.id) > 0) {
      return false;
    }
    final previous = _sameSessionAttendance;
    if (previous == null) return false;
    final selected = student!, current = session!;
    final status = store.paymentStatusFor(selected.id, current.id);
    final debt = status.paymentId == null
        ? status.debtAmount
        : store.paymentDebtFor(status.paymentId!);
    setState(() => _attendanceWarning = true);
    _showSameSessionWarning(
      selected,
      current,
      previous,
      paymentTitle: previous.centerFeeOnly
          ? 'رسوم السنتر مسجلة بالفعل'
          : status.status == StudentPaymentStatus.paidSingle ||
                status.status == StudentPaymentStatus.paidPackage
          ? 'الدفع مسجل بالفعل'
          : 'الحصة مغطاة بالفعل',
      paymentDescription:
          '${status.detail}${debt > 0 ? '\nالمديونية المتبقية: ${money(debt)} محفوظة في سجل الطالب.' : ''}',
    );
    return true;
  }

  Future<void> _showSameSessionWarning(
    Student selected,
    LessonSession current,
    AttendanceRecord previous, {
    String? paymentDescription,
    String? paymentTitle,
  }) async {
    try {
      await _feedback(
        '${selected.name} · كود ${selected.code}\n${store.groupLabel(current.groupId)}\n${sessionLabel(current)} · ${sessionDateLabel(current)}\n${paymentDescription ?? (previous.status == AttendanceStatus.makeup ? 'الحضور مسجل كتعويض.' : 'الحضور مسجل بالفعل.')}\nوقت التسجيل السابق: ${attendanceRecordedAtLabel(previous.recordedAt)}\nلم يُسجل حضور جديد أو دفع أو خصم من الباقة.',
        title: paymentTitle ?? 'الحضور مسجل بالفعل',
        kind: NoticeKind.warning,
      );
    } finally {
      if (mounted) {
        setState(() => _attendanceWarning = false);
        _focusCode();
      }
    }
  }

  List<AttendanceRecord> _otherAttendanceConflicts(
    Student selected,
    LessonSession current,
  ) => store.attendanceNeedsPayment(selected.id, current.id)
      ? const []
      : store
            .attendanceConflictsFor(selected.id, current.id)
            .where((record) => record.sessionId != current.id)
            .toList();

  Future<List<String>?> _reviewAttendanceConflicts(
    Student selected,
    LessonSession current,
    List<AttendanceRecord> conflicts,
  ) async {
    if (!_readyForAttendanceWarning) return null;
    final items = conflicts.map((attendance) {
      final previousSession = _byId(
        store.sessions,
        attendance.sessionId,
        (session) => session.id,
      )!;
      return AttendanceConflictItem(
        attendance: attendance,
        session: previousSession,
        groupLabel: store.groupLabel(previousSession.groupId),
      );
    }).toList();
    final scannerBlocked = ValueNotifier<bool>(false);
    _attendanceScannerBlocked = scannerBlocked;
    setState(() => _attendanceWarning = true);
    try {
      final route = DialogRoute<List<String>>(
        context: context,
        barrierDismissible: false,
        builder: (_) => AttendanceConflictDialog(
          student: selected,
          session: current,
          groupLabel: store.groupLabel(current.groupId),
          conflicts: items,
          scannerBlocked: scannerBlocked,
        ),
      );
      final ids = await Navigator.of(context).push(route);
      await route.completed;
      return ids;
    } finally {
      _attendanceScannerBlocked = null;
      scannerBlocked.dispose();
      if (mounted) {
        setState(() => _attendanceWarning = false);
        _focusCode();
      }
    }
  }

  Future<void> _collect({EntryMode? mode}) async {
    if (mode != null && _warnRecordedPayment()) return;
    if (_warnSameSessionAttendance()) return;
    if (!_canEnter) return;
    final currentStudent = student, currentSession = session;
    if (currentStudent == null || currentSession == null) return;
    final chosenMode =
        store.hasRetainedSessionPayment(currentStudent.id, currentSession.id)
        ? EntryMode.single
        : mode ?? _effectiveMode(currentStudent, currentSession);
    if (mode != null) {
      await _confirmShortcut(chosenMode);
      return;
    }
    final packageSessions = _packageSessions,
        monthPlanId = _monthPlan?.id,
        method = _method;
    var acknowledgedAttendanceIds = <String>[];
    final conflicts = _otherAttendanceConflicts(currentStudent, currentSession);
    if (conflicts.isNotEmpty) {
      final reviewed = await _reviewAttendanceConflicts(
        currentStudent,
        currentSession,
        conflicts,
      );
      if (reviewed == null ||
          !mounted ||
          !_canEnter ||
          _studentId != currentStudent.id ||
          session?.id != currentSession.id) {
        return;
      }
      acknowledgedAttendanceIds = reviewed;
    }
    final request = EntryRequest(
      studentId: currentStudent.id,
      sessionId: currentSession.id,
      mode: chosenMode,
      packageSessions: packageSessions,
      monthPlanId: monthPlanId,
      method: method,
      originalAttendanceId: chosenMode == EntryMode.makeup
          ? _originalAttendanceId
          : null,
      makeupSourceGroupId:
          chosenMode == EntryMode.makeup && _originalAttendanceId == null
          ? _manualMakeupSource?.id
          : null,
      acknowledgedAttendanceIds: acknowledgedAttendanceIds,
    );
    await _run(() async {
      await store.recordAttendance(request);
      if (currentStudent.packageMember ||
          _isCenterOnly(currentStudent, currentSession)) {
        return;
      }
      final sourceId = request.makeupSourceGroupId;
      final remaining = sourceId == null
          ? store.eligibleRemainingFor(currentStudent.id, currentSession.id)
          : store.eligibleMakeupRemainingFor(
              currentStudent.id,
              currentSession.id,
              sourceId,
            );
      if ((currentSession.kind == SessionKind.counted || sourceId != null) &&
          store.attendanceNeedsPayment(currentStudent.id, currentSession.id) &&
          remaining > 0) {
        final coverageRequest = EntryRequest(
          studentId: request.studentId,
          sessionId: request.sessionId,
          mode: sourceId == null ? EntryMode.package : EntryMode.makeup,
          packageSessions: request.packageSessions,
          monthPlanId: request.monthPlanId,
          method: request.method,
          makeupSourceGroupId: sourceId,
          acknowledgedAttendanceIds: request.acknowledgedAttendanceIds,
        );
        final quote = store.entryConfirmationFor(coverageRequest);
        await _settleRecordedAttendance(
          EntryRequest(
            studentId: request.studentId,
            sessionId: request.sessionId,
            mode: coverageRequest.mode,
            packageSessions: request.packageSessions,
            monthPlanId: request.monthPlanId,
            method: request.method,
            makeupSourceGroupId: sourceId,
            acknowledgedAttendanceIds: request.acknowledgedAttendanceIds,
            confirmation: quote,
          ),
        );
      }
    }, 'تم حفظ الحضور. حالة الدفع ظاهرة في سجل الطالب.');
  }

  Future<bool> _recordAndSettleAttendance(EntryRequest request) async {
    await store.recordAttendance(request);
    return _settleRecordedAttendance(request);
  }

  Future<bool> _settleRecordedAttendance(EntryRequest request) async {
    if (!store.attendanceNeedsPayment(request.studentId, request.sessionId)) {
      return false;
    }
    try {
      await store.collectAndAttend(request);
      return true;
    } on CenterException catch (error) {
      throw CenterException(
        'الحضور محفوظ، لكن تسديد الحصة لم يتم. ${error.message}',
      );
    }
  }

  Future<void> _confirmShortcut(
    EntryMode chosenMode, {
    bool showConfirmation = false,
  }) async {
    if (student?.packageMember == true) {
      await _collect();
      return;
    }
    if (_warnRecordedPayment()) return;
    if (_warnSameSessionAttendance()) return;
    if (hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _shortcutConfirming ||
        !_canEnter) {
      return;
    }
    final selected = student!, current = session!;
    final retainedSessionPayment = store.hasRetainedSessionPayment(
      selected.id,
      current.id,
    );
    final packageSessions = _packageSessions,
        monthPlanId = _monthPlan?.id,
        method = _method;
    if (chosenMode == EntryMode.package &&
        store.studyMonths.isNotEmpty &&
        monthPlanId == null) {
      final source = _manualMakeupSource;
      final remaining = source == null
          ? store.eligibleRemainingFor(selected.id, current.id)
          : store.eligibleMakeupRemainingFor(
              selected.id,
              current.id,
              source.id,
            );
      if (remaining <= 0) {
        await _feedback(
          'لا يوجد شهر متاح لهذه المجموعة. أضف الشهر من صفحة الشهور والحصص.',
          kind: NoticeKind.warning,
        );
        return;
      }
    }
    final originalAttendanceId = chosenMode == EntryMode.makeup
        ? _originalAttendanceId
        : null;
    var acknowledgedAttendanceIds = <String>[];
    final conflicts = _otherAttendanceConflicts(selected, current);
    if (conflicts.isNotEmpty) {
      final reviewed = await _reviewAttendanceConflicts(
        selected,
        current,
        conflicts,
      );
      if (reviewed == null ||
          !mounted ||
          !_canEnter ||
          _studentId != selected.id ||
          session?.id != current.id) {
        return;
      }
      acknowledgedAttendanceIds = reviewed;
    }
    final draft = EntryRequest(
      studentId: selected.id,
      sessionId: current.id,
      mode: chosenMode,
      packageSessions: packageSessions,
      monthPlanId: monthPlanId,
      method: method,
      originalAttendanceId: originalAttendanceId,
      makeupSourceGroupId:
          _effectiveMode(selected, current) == EntryMode.makeup &&
              originalAttendanceId == null
          ? _manualMakeupSource?.id
          : null,
      acknowledgedAttendanceIds: acknowledgedAttendanceIds,
    );
    EntryConfirmation? quote;
    String? quoteError;
    try {
      quote = store.entryConfirmationFor(draft);
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.attendance_workspace');
      quoteError = error is CenterException
          ? error.message
          : 'تعذر مراجعة الحساب. حاول مرة أخرى.';
      setState(() {
        _shortcutRetryRequired = true;
      });
      if (chosenMode != EntryMode.package) {
        await _feedback(quoteError, kind: NoticeKind.error);
        _focusCode();
        return;
      }
    }
    final request = EntryRequest(
      studentId: draft.studentId,
      sessionId: draft.sessionId,
      mode: draft.mode,
      packageSessions: draft.packageSessions,
      monthPlanId: draft.monthPlanId,
      method: draft.method,
      originalAttendanceId: draft.originalAttendanceId,
      makeupSourceGroupId: draft.makeupSourceGroupId,
      acknowledgedAttendanceIds: draft.acknowledgedAttendanceIds,
      confirmation: quote,
    );
    final needsMonthConfirmation =
        chosenMode == EntryMode.package &&
        !retainedSessionPayment &&
        (quote == null || quote.eligibleRemaining == 0);
    if (!showConfirmation && !needsMonthConfirmation) {
      if (quote == null) {
        await _feedback(
          quoteError ?? 'تعذر حساب الدفع.',
          kind: NoticeKind.error,
        );
        return;
      }
      final saved = await _run(() async {
        await _recordAndSettleAttendance(request);
      }, null);
      if (mounted) {
        setState(() => _shortcutRetryRequired = !saved);
        _focusCode();
      }
      return;
    }
    final groupLabel = store.groupLabel(current.groupId);
    final original = _byId(
      store.allAttendances,
      request.originalAttendanceId,
      (item) => item.id,
    );
    final originalSession = _byId(
      store.sessions,
      original?.sessionId,
      (item) => item.id,
    );
    final originalLabel = originalSession == null
        ? draft.makeupSourceGroupId == null
              ? null
              : 'تعويض من ${store.groupLabel(draft.makeupSourceGroupId!)} · لا يوجد سجل غياب سابق مرتبط'
        : 'تعويض عن ${sessionLabel(originalSession)} · ${store.groupLabel(originalSession.groupId)} · ${sessionDateLabel(originalSession)}';
    final blocked = ValueNotifier(false);
    final actorId = store.currentUser?.id;
    final packageConfirmation = chosenMode != EntryMode.package
        ? null
        : PackageConfirmationController(
            preview: PackageConfirmationPreview(
              request: request,
              quote: quote,
              error: quoteError,
            ),
            scannerBlocked: blocked,
            studentCodes: () => store.students.expand(studentIdentifiers),
            plans: List.unmodifiable(
              _visibleMonthPlans(_manualMakeupSource ?? group),
            ),
            previewForPlan: (plan) {
              final changed = EntryRequest(
                studentId: draft.studentId,
                sessionId: draft.sessionId,
                mode: draft.mode,
                packageSessions: plan.sessions,
                monthPlanId: plan.id,
                method: draft.method,
                originalAttendanceId: draft.originalAttendanceId,
                makeupSourceGroupId: draft.makeupSourceGroupId,
                acknowledgedAttendanceIds: draft.acknowledgedAttendanceIds,
              );
              try {
                final updated = store.entryConfirmationFor(changed);
                if (updated.staffId != actorId ||
                    updated.groupId != current.groupId ||
                    updated.sessionKind != current.kind) {
                  throw const CenterException(
                    'تغيّر الموظف أو سياق الحصة. أغلق التأكيد وراجع الدخول من جديد.',
                  );
                }
                return PackageConfirmationPreview(
                  request: EntryRequest(
                    studentId: changed.studentId,
                    sessionId: changed.sessionId,
                    mode: changed.mode,
                    packageSessions: plan.sessions,
                    monthPlanId: plan.id,
                    method: changed.method,
                    originalAttendanceId: changed.originalAttendanceId,
                    makeupSourceGroupId: changed.makeupSourceGroupId,
                    acknowledgedAttendanceIds:
                        changed.acknowledgedAttendanceIds,
                    confirmation: updated,
                  ),
                  quote: updated,
                );
              } catch (error, stackTrace) {
                reportProblem(
                  error,
                  stackTrace,
                  operation: 'ui.attendance_workspace',
                );
                return PackageConfirmationPreview(
                  request: changed,
                  error: error is CenterException
                      ? error.message
                      : 'تعذر مراجعة السعر. اختر الشهر وراجع الحساب من جديد.',
                );
              }
            },
          );
    _packageConfirmation = packageConfirmation;
    _confirmationScannerBlocked = blocked;
    setState(() => _shortcutConfirming = true);
    int? approvedPaidAmount;
    try {
      final confirmed = await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => EntryConfirmationDialog(
          student: selected,
          session: current,
          groupLabel: groupLabel,
          originalAbsenceLabel:
              request.mode == EntryMode.makeup ||
                  request.makeupSourceGroupId != null
              ? originalLabel
              : null,
          request: request,
          quote: quote,
          scannerBlocked: blocked,
          packageConfirmation: packageConfirmation,
          retainedSessionPayment: retainedSessionPayment,
          attendanceRecorded: store
              .attendancesForStudent(selected.id)
              .any(
                (record) =>
                    record.sessionId == current.id &&
                    record.status != AttendanceStatus.absent,
              ),
          onPaidAmountConfirmed: (amount) => approvedPaidAmount = amount,
        ),
      );
      if (!mounted) return;
      final previewQuote = packageConfirmation == null
          ? quote
          : packageConfirmation.value.quote;
      final previewRequest = packageConfirmation?.value.request ?? request;
      final approvedQuote = previewQuote == null
          ? null
          : _entryQuoteWithPayment(previewQuote, approvedPaidAmount);
      final approvedRequest = _entryWithPayment(
        previewRequest,
        approvedQuote,
        approvedPaidAmount,
      );
      if (confirmed == true &&
          approvedQuote != null &&
          packageConfirmation?.validateCodePrefix() != false &&
          !blocked.value &&
          _studentResolved &&
          _search.text.trim().isEmpty &&
          _studentId == selected.id) {
        var settledNow = false;
        final saved = await _run(() async {
          settledNow = await _recordAndSettleAttendance(approvedRequest);
        }, null);
        if (saved) {
          await _feedback(
            settledNow
                ? 'تم تسجيل ${selected.name} · ${selected.code}. ${retainedSessionPayment
                      ? 'مسددة مسبقًا؛ تم تسجيل الحضور دون دفع جديد ودون خصم من الباقة.'
                      : approvedQuote.centerFeeOnly
                      ? 'رسوم السنتر: المطلوب ${money(approvedQuote.netAmount)} · المدفوع ${money(approvedQuote.collectedAmount)} · المتبقي ${money(approvedQuote.netAmount - approvedQuote.collectedAmount)}. المدرس معفى؛ لم يُخصم رصيد.'
                      : approvedRequest.makeupSourceGroupId != null && approvedQuote.eligibleRemaining > 0
                      ? 'تعويض محسوب من باقة المجموعة الأصلية؛ المتبقي ${approvedQuote.eligibleRemaining - 1} حصص.'
                      : approvedQuote.eligibleRemaining > 0 && current.kind == SessionKind.counted && chosenMode != EntryMode.makeup
                      ? 'من رصيد الباقة دون دفع جديد؛ المتبقي ${approvedQuote.eligibleRemaining - 1} حصص.'
                      : 'المطلوب ${money(approvedQuote.netAmount)} · المدفوع ${money(approvedQuote.collectedAmount)} · المديونية ${money(approvedQuote.netAmount - approvedQuote.collectedAmount)} · ${approvedRequest.method}.'}'
                : 'حضور ${selected.name} محفوظ ومغطى بالفعل. لم يحدث تحصيل أو خصم جديد في هذه العملية.',
          );
        }
        if (mounted) setState(() => _shortcutRetryRequired = !saved);
      } else if (blocked.value) {
        setState(() {
          _studentResolved = false;
          _search.clear();
        });
        await _feedback(
          'تم إلغاء التأكيد لحماية حساب الطالب. امسح كود الطالب من جديد.',
          kind: NoticeKind.warning,
        );
      }
    } finally {
      _packageConfirmation = null;
      packageConfirmation?.dispose();
      _confirmationScannerBlocked = null;
      _heldPaymentShortcut = null;
      blocked.dispose();
      if (mounted) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) return;
          setState(() => _shortcutConfirming = false);
          _focusCode();
        });
        WidgetsBinding.instance.scheduleFrame();
      }
    }
  }

  EntryConfirmation _entryQuoteWithPayment(
    EntryConfirmation quote,
    int? paidAmount,
  ) => EntryConfirmation(
    staffId: quote.staffId,
    groupId: quote.groupId,
    sessionKind: quote.sessionKind,
    baseAmount: quote.baseAmount,
    discountPercent: quote.discountPercent,
    netAmount: quote.netAmount,
    eligibleRemaining: quote.eligibleRemaining,
    paidAmount: paidAmount,
    monthPlanId: quote.monthPlanId,
    monthPlanName: quote.monthPlanName,
    monthPlanSessions: quote.monthPlanSessions,
    centerFeeOnly: quote.centerFeeOnly,
    centerFeeAmount: quote.centerFeeAmount,
  );

  EntryRequest _entryWithPayment(
    EntryRequest request,
    EntryConfirmation? quote,
    int? paidAmount,
  ) => EntryRequest(
    studentId: request.studentId,
    sessionId: request.sessionId,
    mode: request.mode,
    method: request.method,
    notes: request.notes,
    originalAttendanceId: request.originalAttendanceId,
    makeupSourceGroupId: request.makeupSourceGroupId,
    packageSessions: request.packageSessions,
    monthPlanId: request.monthPlanId,
    acknowledgedAttendanceIds: request.acknowledgedAttendanceIds,
    confirmation: quote,
    paidAmount: paidAmount,
  );

  bool get _canChangeCard =>
      _cardContextReady && ModalRoute.of(context)?.isCurrent == true;

  bool get _cardContextReady =>
      _workspaceEnabled &&
      !hasPendingMassarNotice(context) &&
      !_attendanceWarning &&
      !_historyModal &&
      !_shortcutConfirming &&
      !_closedSessionWarning &&
      !_reviewing &&
      !_lookupOnly &&
      !_busy &&
      !_discountEditing &&
      _noteStudentId == null &&
      _studentResolved &&
      student != null &&
      _search.text.trim().isEmpty &&
      store.canCollect;

  void _enableFreeSearch() {
    if (!_canUseToolbar) return;
    setState(() {
      _freeSearchTyping = true;
      _heldPaymentShortcut = null;
    });
    _searchFocus.requestFocus();
  }

  Future<void> _collectCenterFee() async {
    if (!_canChangeCard) return;
    final selected = student!, current = session;
    if (current == null ||
        !selected.centerFeeEnabled ||
        selected.packageMember ||
        store.centerFeeRemainingFor(selected.id, current.id) <= 0) {
      return;
    }
    await _run(
      () => store.collectStudentCenterFee(
        studentId: selected.id,
        sessionId: current.id,
      ),
      null,
    );
  }

  Future<void> _payStudentCard() async {
    if (!_canChangeCard) return;
    final selected = student!;
    if (selected.isSuspended) return;
    if (store.cardPaymentFor(selected.id) != null ||
        store.cardReceiptFor(selected.id) != null ||
        store.cardSettings.price == null) {
      return;
    }
    final current = session;
    final method = _cardMethod;
    final due = _discounted(
      store.cardSettings.price!,
      selected.discountPercent,
    );
    final payment = await _reviewPaidAmount(
      (blocked) => PaidAmountDialog(
        title: 'دفع كارت الطالب',
        studentLabel: '${selected.name} · ${selected.code}',
        options: [
          PaidAmountPriceOption(id: 0, label: 'كارت الطالب', dueAmount: due),
        ],
        initialOptionId: 0,
        method: method,
        description: 'هذه رسوم الكارت فقط؛ لا تسجل حضورًا ولا تخصم من الباقة.',
        scannerBlocked: blocked,
      ),
    );
    if (payment == null ||
        !mounted ||
        !_canChangeCard ||
        student?.id != selected.id) {
      return;
    }
    await _run(
      () => store.collectStudentCard(
        studentId: selected.id,
        method: method,
        sessionId: current?.status == SessionStatus.open ? current?.id : null,
        paidAmount: payment.paidAmount,
        expectedNetAmount: payment.dueAmount,
      ),
      'تم تسجيل دفع الكارت. ${_paymentSummary(payment)} استلام الكارت والحضور يُسجلان كلٌ على حدة.',
    );
  }

  String _paymentSummary(PaidAmountSelection payment) =>
      'المطلوب ${money(payment.dueAmount)} · المدفوع ${money(payment.collectedAmount)} · المديونية ${money(payment.dueAmount - payment.collectedAmount)}.';

  Future<PaidAmountSelection?> _reviewPaidAmount(
    PaidAmountDialog Function(ValueNotifier<bool>) dialog,
  ) async {
    if (!_canChangeContext) return null;
    final blocked = ValueNotifier(false);
    _paidAmountScannerBlocked = blocked;
    setState(() => _historyModal = true);
    try {
      final route = DialogRoute<PaidAmountSelection>(
        context: context,
        barrierDismissible: false,
        builder: (_) => dialog(blocked),
      );
      final payment = await Navigator.of(context).push(route);
      await route.completed;
      return blocked.value ? null : payment;
    } finally {
      _paidAmountScannerBlocked = null;
      blocked.dispose();
      if (mounted) {
        setState(() => _historyModal = false);
        _focusCode();
      }
    }
  }

  bool get _canChangeStudentStatus => _canChangeCard && !_historyModal;

  Future<bool> _reviewStudentStatus(
    Student selected,
    StudentSuspensionAction action,
  ) async {
    if (!_canChangeContext || !store.canCollect) return false;
    final blocked = ValueNotifier(false);
    if (action == StudentSuspensionAction.reactivate) {
      _paidAmountScannerBlocked = blocked;
    }
    setState(() => _historyModal = true);
    try {
      final route = DialogRoute<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => StudentSuspensionDialog(
          store: store,
          student: selected,
          action: action,
          scannerBlocked: action == StudentSuspensionAction.reactivate
              ? blocked
              : null,
        ),
      );
      final changed = await Navigator.of(context).push(route);
      await route.completed;
      return changed == true && !blocked.value;
    } finally {
      _paidAmountScannerBlocked = null;
      blocked.dispose();
      if (mounted) {
        setState(() => _historyModal = false);
        _focusCode();
      }
    }
  }

  Future<void> _reactivateSelectedStudent() async {
    final selected = student;
    if (selected == null || !_canChangeStudentStatus || !selected.isSuspended) {
      return;
    }
    final changed = await _reviewStudentStatus(
      selected,
      StudentSuspensionAction.reactivate,
    );
    if (!mounted || student?.id != selected.id) return;
    if (!changed) {
      _nextStudent();
    } else {
      setState(() => _mode = _preferredMode(student!));
      _focusCode();
    }
  }

  Future<void> _suspendSelectedStudent() async {
    final selected = student;
    if (selected == null || !_canChangeStudentStatus || selected.isSuspended) {
      return;
    }
    await _reviewStudentStatus(selected, StudentSuspensionAction.suspend);
    if (mounted) _focusCode();
  }

  Future<void> _settleStudentDebts() async {
    final selected = student, current = session;
    if (selected == null || !_canChangeCard) return;
    await _withHistoryModal(
      () => showStudentDebtDialog(
        context,
        store,
        selected,
        sessionId: current?.status == SessionStatus.open ? current?.id : null,
      ),
    );
    if (mounted) _focusCode();
  }

  void _editNote(Student selected) {
    if (hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _busy ||
        !store.canCollect) {
      return;
    }
    setState(() {
      _noteStudentId = selected.id;
      _notes.text = selected.notes;
    });
    _notesFocus.requestFocus();
  }

  void _cancelNote() {
    if (_busy) return;
    setState(() => _noteStudentId = null);
    _notes.clear();
    _search.clear();
    _focusCode();
  }

  Future<void> _saveNote({bool clear = false}) async {
    final id = _noteStudentId;
    if (hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _discountEditing ||
        _busy ||
        !store.canCollect ||
        id == null ||
        id != _studentId) {
      return;
    }
    final text = clear ? '' : _notes.text;
    final saved = await _run(
      () => store.saveStudentNote(studentId: id, notes: text),
      clear ? 'تم مسح ملاحظة الطالب.' : 'تم حفظ ملاحظة الطالب.',
    );
    if (!mounted || !saved) return;
    setState(() => _noteStudentId = null);
    _notes.clear();
    _search.clear();
    _focusCode();
  }

  Widget _studentNote(Student selected) {
    final palette = MassarPalette.of(context);
    final editing = _noteStudentId == selected.id;
    return Container(
      key: const Key('student-note-panel'),
      margin: const EdgeInsets.only(top: 10, bottom: 6),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: selected.notes.isEmpty ? palette.subtle : palette.warningSurface,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: palette.line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Icon(
                Icons.sticky_note_2_outlined,
                size: 18,
                color: palette.warning,
              ),
              const SizedBox(width: 8),
              const Expanded(
                child: Text(
                  'ملاحظة الطالب',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
              if (!editing && store.canCollect)
                TextButton(
                  key: const Key('edit-student-note'),
                  onPressed: _busy ? null : () => _editNote(selected),
                  child: Text(selected.notes.isEmpty ? 'إضافة' : 'تعديل'),
                ),
            ],
          ),
          if (editing) ...[
            TextField(
              key: const Key('student-note-editor'),
              controller: _notes,
              focusNode: _notesFocus,
              readOnly: _busy,
              maxLength: 4000,
              minLines: 2,
              maxLines: 4,
              decoration: const InputDecoration(
                hintText: 'اكتب ملاحظة تخص هذا الطالب',
              ),
            ),
            const Text('احفظ الملاحظة أو ألغِ التعديل قبل استقبال كود آخر.'),
            Wrap(
              spacing: 8,
              children: [
                FilledButton(
                  key: const Key('save-student-note'),
                  onPressed: _busy ? null : () => _saveNote(),
                  child: const Text('حفظ الملاحظة'),
                ),
                TextButton(
                  key: const Key('cancel-student-note'),
                  onPressed: _busy ? null : _cancelNote,
                  child: const Text('إلغاء'),
                ),
                if (selected.notes.isNotEmpty)
                  TextButton(
                    key: const Key('clear-student-note'),
                    onPressed: _busy ? null : () => _saveNote(clear: true),
                    child: const Text('مسح الملاحظة'),
                  ),
              ],
            ),
          ] else
            Text(
              selected.notes.isEmpty ? 'لا توجد ملاحظة مسجلة.' : selected.notes,
              key: const Key('student-note-text'),
              style: TextStyle(
                color: palette.ink,
                fontWeight: selected.notes.isEmpty
                    ? FontWeight.normal
                    : FontWeight.w600,
              ),
            ),
        ],
      ),
    );
  }

  Future<void> _renew() async {
    if (!_workspaceEnabled ||
        !_canChangeContext ||
        _lookupOnly ||
        !_studentResolved ||
        _search.text.trim().isNotEmpty ||
        !store.canCollect) {
      return;
    }
    final currentStudent = student, currentGroup = group;
    if (currentStudent == null ||
        currentGroup == null ||
        currentStudent.isSuspended) {
      return;
    }
    final current = session, method = _method;
    final plans = List<GroupMonthPlan>.unmodifiable(
      _visibleMonthPlans(currentGroup),
    );
    if (plans.isEmpty) {
      await _feedback(
        'لا يوجد شهر متاح لهذه المجموعة. أضف الشهر من صفحة الشهور والحصص.',
        kind: NoticeKind.warning,
      );
      return;
    }
    final selectedIndex = plans.indexWhere(
      (plan) => plan.id == _selectedMonth(currentGroup)?.id,
    );
    final payment = await _reviewPaidAmount(
      (blocked) => PaidAmountDialog(
        title: 'تجديد الشهر',
        studentLabel: '${currentStudent.name} · ${currentStudent.code}',
        options: [
          for (var index = 0; index < plans.length; index++)
            PaidAmountPriceOption(
              id: index,
              label: '${plans[index].name} · ${plans[index].sessions} حصص',
              dueAmount: _discounted(
                plans[index].price,
                currentStudent.discountPercent,
              ),
            ),
        ],
        initialOptionId: selectedIndex < 0 ? 0 : selectedIndex,
        method: method,
        description:
            'تضاف حصص الشهر كاملة للرصيد. المتبقي من سعره مديونية؛ لا يُسجل حضور بهذه العملية.',
        scannerBlocked: blocked,
      ),
    );
    if (payment != null &&
        mounted &&
        _canChangeContext &&
        !_lookupOnly &&
        student?.id == currentStudent.id) {
      final plan = plans[payment.optionId];
      setState(() => _monthPlanId = plan.id);
      await _run(
        () => store.renewPackage(
          PackageRequest(
            studentId: currentStudent.id,
            groupId: currentGroup.id,
            sessions: plan.sessions,
            monthPlanId: plan.id,
            expectedMonthPlan: plan,
            sessionId: current?.status == SessionStatus.open
                ? current?.id
                : null,
            method: method,
            paidAmount: payment.paidAmount,
            expectedNetAmount: payment.dueAmount,
          ),
        ),
        'تم تسجيل ${plan.name} وإضافة ${plan.sessions} حصص للرصيد. ${_paymentSummary(payment)}',
      );
    }
    _focusCode();
  }

  Future<void> _closeSession() async {
    if (!_workspaceEnabled) return;
    final current = session;
    if (hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _lookupOnly ||
        current == null ||
        _discountEditing ||
        _busy ||
        _noteStudentId != null) {
      return;
    }
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => ScrollableMassarDialog(
        title: Text('إنهاء ${sessionLabel(current)}؟'),
        content: const Text(
          'سيُسجل الطلبة غير المحضرين غائبين. الحصة المحسوبة تُخصم من الباقات المؤهلة؛ طالب الدفع بالحصة لا تنشأ عليه مديونية. بعدها تظهر تفاصيل التقفيلة لمراجعة النقدية الفعلية والفرق قبل حفظها.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('استكمال التحضير'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('إنهاء وعرض التقفيلة'),
          ),
        ],
      ),
    );
    if (confirmed == true && mounted) {
      final ended = await _run(
        () => store.closeSession(current.id),
        'تم إنهاء الحصة وحفظ سجل الغياب.',
      );
      if (ended && mounted) {
        setState(() => _startedSessionId = current.id);
        await _showClosing(current.id);
      }
    }
    _focusCode();
  }

  Future<void> _showClosing(String sessionId) async {
    if (hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        _lookupOnly ||
        _discountEditing ||
        _busy ||
        _noteStudentId != null ||
        !store.canCollect) {
      return;
    }
    await showDialog<void>(
      context: context,
      builder: (context) => Dialog.fullscreen(
        child: Scaffold(
          appBar: AppBar(
            title: const Text('تقرير الحصة والتقفيلة'),
            leading: IconButton(
              tooltip: 'العودة للتحضير',
              onPressed: () => Navigator.maybePop(context),
              icon: Icon(Icons.close),
            ),
          ),
          body: Directionality(
            textDirection: TextDirection.rtl,
            child: ClosingsPage(store: store, initialSessionId: sessionId),
          ),
        ),
      ),
    );
    _focusCode();
  }

  bool get _canReviewSession =>
      _workspaceEnabled &&
      !hasPendingMassarNotice(context) &&
      !_attendanceWarning &&
      !_busy &&
      !_discountEditing &&
      !_reviewing &&
      !_historyModal &&
      !_lookupOnly &&
      !_shortcutConfirming &&
      !_closedSessionWarning &&
      _noteStudentId == null &&
      store.canCollect &&
      session != null &&
      session!.status != SessionStatus.canceled &&
      ModalRoute.of(context)?.isCurrent == true;

  Future<void> _showSessionReview() async {
    if (!_canReviewSession) return;
    final selectedSession = session!;
    setState(() => _reviewing = true);
    try {
      await showDialog<void>(
        context: context,
        builder: (context) => Dialog(
          key: const Key('attendance-session-review-dialog'),
          insetPadding: const EdgeInsets.all(20),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: SizedBox(
              width: 1100,
              height: 740,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Align(
                    alignment: AlignmentDirectional.centerEnd,
                    child: IconButton(
                      key: const Key('close-attendance-session-review'),
                      tooltip: 'العودة للتحضير',
                      onPressed: () => Navigator.maybePop(context),
                      icon: const Icon(Icons.close),
                    ),
                  ),
                  Expanded(
                    child: Padding(
                      padding: const EdgeInsets.fromLTRB(20, 0, 20, 20),
                      child: ReviewPage(
                        store: store,
                        sessionId: selectedSession.id,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) return;
          setState(() => _reviewing = false);
          _focusCode();
        });
        WidgetsBinding.instance.scheduleFrame();
      }
    }
  }

  bool get _canEditDiscount =>
      _workspaceEnabled &&
      !hasPendingMassarNotice(context) &&
      !_attendanceWarning &&
      !_busy &&
      !_discountEditing &&
      !_shortcutConfirming &&
      !_closedSessionWarning &&
      !_reviewing &&
      !_historyModal &&
      !_lookupOnly &&
      _noteStudentId == null &&
      _studentResolved &&
      student != null &&
      group != null &&
      _search.text.trim().isEmpty &&
      store.canEditDiscount &&
      ModalRoute.of(context)?.isCurrent == true;

  List<DiscountPriceOption> _discountPrices(StudyGroup selectedGroup) {
    return <DiscountPriceOption>[
      DiscountPriceOption(
        id: 'single',
        label: 'الحصة',
        baseAmount: selectedGroup.sessionPrice,
      ),
      for (final plan in _visibleMonthPlans(selectedGroup))
        DiscountPriceOption(
          id: 'month-${plan.id}',
          label: '${plan.name} · ${plan.sessions} حصص',
          baseAmount: plan.price,
        ),
      if (session?.kind == SessionKind.extra)
        DiscountPriceOption(
          id: 'extra',
          label: 'الحصة بسعر منفصل',
          baseAmount: session!.extraPrice,
        ),
    ];
  }

  Future<void> _editDiscount() async {
    if (!_canEditDiscount) return;
    final selected = student!, selectedGroup = _manualMakeupSource ?? group!;
    final options = _discountPrices(selectedGroup);
    final initialId = session?.kind == SessionKind.extra
        ? 'extra'
        : _mode == EntryMode.package
        ? 'month-${_selectedMonth(selectedGroup)?.id}'
        : 'single';
    final initial = options.firstWhere(
      (option) => option.id == initialId,
      orElse: () => options.first,
    );
    setState(() => _discountEditing = true);
    try {
      await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => StudentDiscountDialog(
          store: store,
          studentId: selected.id,
          baseAmount: initial.baseAmount,
          priceLabel: initial.label,
          priceOptions: options,
          initialPriceId: initial.id,
        ),
      );
    } finally {
      if (mounted) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) return;
          setState(() {
            _discountEditing = false;
            _heldPaymentShortcut = null;
          });
          _focusCode();
        });
        WidgetsBinding.instance.scheduleFrame();
      }
    }
  }

  int _discounted(int amount, num percent) => discountedAmount(amount, percent);

  Widget _message(String text, {bool error = false, bool warning = false}) =>
      Container(
        width: double.infinity,
        padding: const EdgeInsets.all(14),
        margin: const EdgeInsets.only(bottom: 12),
        decoration: BoxDecoration(
          color: error
              ? _palette.errorSurface
              : warning
              ? _palette.warningSurface
              : _palette.successSurface,
          borderRadius: BorderRadius.circular(8),
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              error
                  ? Icons.error_outline
                  : warning
                  ? Icons.warning_amber_rounded
                  : Icons.check_circle_outline,
              size: 21,
              color: error
                  ? _palette.error
                  : warning
                  ? _palette.warning
                  : _palette.success,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                text,
                style: TextStyle(
                  color: error
                      ? _palette.error
                      : warning
                      ? _palette.warning
                      : _palette.success,
                ),
              ),
            ),
          ],
        ),
      );

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: store,
    builder: (context, _) => Shortcuts(
      shortcuts: const {
        SingleActivator(LogicalKeyboardKey.f2): _SearchIntent(),
        SingleActivator(LogicalKeyboardKey.f3): _SpotlightIntent(),
        SingleActivator(LogicalKeyboardKey.f4): _NewStudentIntent(),
        SingleActivator(LogicalKeyboardKey.f6): _NextIntent(),
      },
      child: Actions(
        actions: {
          _SpotlightIntent: CallbackAction<_SpotlightIntent>(
            onInvoke: (_) {
              _openSpotlight();
              return null;
            },
          ),
          _SearchIntent: CallbackAction<_SearchIntent>(
            onInvoke: (_) {
              if (!_workspaceEnabled ||
                  hasPendingMassarNotice(context) ||
                  _attendanceWarning ||
                  _reviewing ||
                  _historyModal ||
                  _submittingStudent ||
                  _busy) {
                return null;
              }
              if (_noteStudentId != null) {
                _notesFocus.requestFocus();
              } else {
                _searchFocus.requestFocus();
              }
              return null;
            },
          ),
          _NewStudentIntent: CallbackAction<_NewStudentIntent>(
            onInvoke: (_) {
              if (!_submittingStudent && !_startingSession) {
                _editStudent(create: true);
              }
              return null;
            },
          ),
          _NextIntent: CallbackAction<_NextIntent>(
            onInvoke: (_) {
              if (!_busy &&
                  !_discountEditing &&
                  !_submittingStudent &&
                  !_startingSession) {
                _nextStudent();
              }
              return null;
            },
          ),
        },
        child: Focus(
          onFocusChange: (focused) {
            if (focused) _focusCode();
          },
          child: Scaffold(
            backgroundColor: _palette.canvas,
            body: SafeArea(
              child: ManagementBody(
                header: [
                  _header(),
                  _contextBar(),
                  IgnorePointer(
                    ignoring: !_workspaceEnabled,
                    child: ExcludeFocus(
                      excluding: !_workspaceEnabled,
                      child: Opacity(
                        opacity: _workspaceEnabled ? 1 : .4,
                        child: _searchBar(),
                      ),
                    ),
                  ),
                  if (_busy || _submittingStudent || _startingSession)
                    Semantics(
                      liveRegion: true,
                      label: _startingSession ? 'جارٍ بدء الحصة' : _enterHint,
                      child: const LinearProgressIndicator(minHeight: 3),
                    ),
                ],
                child: !_workspaceEnabled
                    ? _sessionStartState()
                    : student == null
                    ? _emptyState()
                    : _studentWorkspace(),
              ),
            ),
          ),
        ),
      ),
    ),
  );

  Future<void> _exitWorkspace() async {
    if (!_canExitWorkspace) return;
    final noteStudent = _byId(
      store.students,
      _noteStudentId,
      (value) => value.id,
    );
    final dirty =
        _noteStudentId != null && _notes.text != (noteStudent?.notes ?? '');
    if (dirty) {
      setState(() => _historyModal = true);
      var leave = false;
      try {
        leave = await confirmDiscardDraft(context, dirty: true);
      } finally {
        if (mounted) setState(() => _historyModal = false);
      }
      if (!mounted) return;
      if (!leave) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted && ModalRoute.of(context)?.isCurrent == true) {
            _notesFocus.requestFocus();
          }
        });
        return;
      }
    }
    if (_noteStudentId != null) {
      setState(() => _noteStudentId = null);
      _notes.clear();
    }
    if (mounted) widget.onExit();
  }

  Widget _header() => Container(
    color: _palette.surface,
    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
    child: LayoutBuilder(
      builder: (context, constraints) {
        final compact =
            constraints.maxWidth / MediaQuery.textScalerOf(context).scale(1) <
            1000;
        const brand = MassarLogo(height: 30);
        const title = Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'دخول الطلبة والتحصيل',
              style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
            ),
            Text('نادر جورج · وضع التركيز'),
          ],
        );
        final counter = Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          decoration: BoxDecoration(
            color: _palette.successSurface,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Row(
            children: [
              Icon(Icons.people_outline, color: _palette.accent, size: 23),
              const SizedBox(width: 10),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('الحضور المسجل حتى الآن'),
                  Text(
                    '${session == null ? 0 : store.attendanceCount(session!.id)} طالب',
                    key: const Key('session-attendance-counter'),
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: _palette.ink,
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
        final actions = [
          if (store.canAssess)
            TextButton.icon(
              key: const Key('attendance-mobile-homework'),
              onPressed: () => showMobileHomeworkDialog(
                context,
                store: store,
                lan: widget.lanController,
                session: session,
              ),
              icon: const Icon(Icons.phone_android, size: 18),
              label: const Text('واجب الموبايل'),
            ),
          Tooltip(
            message: store.isRemote
                ? store.remoteConnected
                      ? 'متصل بالجهاز الرئيسي؛ البيانات محفوظة عليه'
                      : 'الاتصال بالرئيسي متوقف؛ راجع الشبكة قبل أي عملية'
                : 'البيانات محفوظة على هذا الجهاز',
            child: Icon(
              store.isRemote ? Icons.lan_outlined : Icons.offline_pin_outlined,
              color: store.isRemote && !store.remoteConnected
                  ? _palette.warning
                  : _palette.accent,
            ),
          ),
          const AppearanceToggle(),
          TextButton.icon(
            onPressed: _canExitWorkspace ? _exitWorkspace : null,
            icon: Icon(Icons.close_fullscreen, size: 18),
            label: const Text('الخروج من التركيز'),
          ),
        ];
        if (compact) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  brand,
                  const SizedBox(width: 12),
                  const Expanded(child: title),
                ],
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [counter, ...actions],
              ),
            ],
          );
        }
        return Row(
          children: [
            brand,
            const SizedBox(width: 12),
            const Expanded(child: title),
            counter,
            const SizedBox(width: 16),
            ...actions,
          ],
        );
      },
    ),
  );

  Widget _contextBar() {
    final months = [...store.studyMonths]
      ..sort((a, b) => a.number.compareTo(b.number));
    final lessons = [...?_studyMonth?.lessons]
      ..sort((a, b) => a.number.compareTo(b.number));
    final monthField = DropdownButtonFormField<String>(
      key: ValueKey('study-month-$_studyMonthId'),
      initialValue: _studyMonth?.id,
      isExpanded: true,
      decoration: const InputDecoration(labelText: 'الشهر', isDense: true),
      items: months
          .map(
            (month) => DropdownMenuItem(
              value: month.id,
              child: Text(
                '${month.name} · شهر ${month.number}',
                overflow: TextOverflow.ellipsis,
              ),
            ),
          )
          .toList(),
      onChanged: _canUseToolbar ? _changeStudyMonth : null,
    );
    final sessionField = DropdownButtonFormField<String>(
      key: ValueKey('prepared-lesson-$_preparedLessonId'),
      initialValue: _preparedLesson?.id,
      isExpanded: true,
      decoration: const InputDecoration(
        labelText: 'الحصة المعدّة',
        isDense: true,
      ),
      items: lessons
          .map(
            (lesson) => DropdownMenuItem(
              value: lesson.id,
              child: Text(
                _preparedLessonLabel(lesson),
                overflow: TextOverflow.ellipsis,
              ),
            ),
          )
          .toList(),
      onChanged: _canUseToolbar ? _changePreparedLesson : null,
    );
    final groupField = DropdownButtonFormField<String>(
      key: ValueKey('group-$_groupId'),
      initialValue: _groupId,
      isExpanded: true,
      decoration: const InputDecoration(labelText: 'المجموعة', isDense: true),
      items: store
          .groupsForRegion()
          .map(
            (item) => DropdownMenuItem(
              value: item.id,
              child: Text(
                store.groupLabel(item.id),
                overflow: TextOverflow.ellipsis,
              ),
            ),
          )
          .toList(),
      onChanged: _canUseToolbar ? _changeGroup : null,
    );
    final actions = <Widget>[
      if (session?.status != SessionStatus.closed &&
          session?.status != SessionStatus.canceled)
        FilledButton.icon(
          key: const Key('start-attendance-session'),
          focusNode: _startFocus,
          onPressed:
              !_workspaceEnabled &&
                  _canChangeContext &&
                  group != null &&
                  (_preparedLesson != null || session != null)
              ? () => _confirmSessionStart()
              : null,
          icon: Icon(
            _workspaceEnabled ? Icons.check_circle_outline : Icons.play_arrow,
          ),
          label: Text(_workspaceEnabled ? 'الحصة بدأت' : 'ابدأ الحصة'),
        ),
      if (session?.status == SessionStatus.closed && !_lookupOnly)
        OutlinedButton.icon(
          onPressed: _canChangeContext ? _warnClosedSession : null,
          icon: const Icon(Icons.lock_open_outlined),
          label: const Text('فتح أو عرض الحصة'),
        ),
      if (session != null || _preparedLesson != null)
        Text(
          sessionKindLabel(session?.kind ?? _preparedLesson!.kind),
          style: TextStyle(fontWeight: FontWeight.bold),
        ),
      OutlinedButton(
        onPressed:
            _discountEditing ||
                _busy ||
                _startingSession ||
                _lookupOnly ||
                _noteStudentId != null ||
                session == null ||
                session?.status == SessionStatus.canceled ||
                (session?.status == SessionStatus.open && !_workspaceEnabled) ||
                !store.canCollect
            ? null
            : session!.status == SessionStatus.closed
            ? () => _showClosing(session!.id)
            : _closeSession,
        child: Text(
          session?.status == SessionStatus.closed
              ? 'عرض التقفيلة'
              : 'إنهاء الحصة',
        ),
      ),
      if (session?.status == SessionStatus.open)
        OutlinedButton.icon(
          key: const Key('session-live-report'),
          onPressed: _canReviewSession ? () => _showClosing(session!.id) : null,
          icon: const Icon(Icons.assessment_outlined, size: 18),
          label: const Text('تقرير الحصة'),
        ),
      OutlinedButton.icon(
        key: const Key('session-payment-review'),
        onPressed: _canReviewSession ? _showSessionReview : null,
        icon: const Icon(Icons.fact_check_outlined, size: 18),
        label: const Text('مراجعة الدفع'),
      ),
    ];
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 10, 16, 8),
      child: LayoutBuilder(
        builder: (context, constraints) {
          final effectiveWidth =
              constraints.maxWidth / MediaQuery.textScalerOf(context).scale(1);
          final stacked = effectiveWidth < 560;
          if (effectiveWidth >= 1250) {
            return Row(
              children: [
                Expanded(flex: 3, child: monthField),
                const SizedBox(width: 8),
                Expanded(flex: 3, child: sessionField),
                const SizedBox(width: 8),
                Expanded(flex: 4, child: groupField),
                const SizedBox(width: 12),
                Wrap(
                  spacing: 6,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: actions,
                ),
              ],
            );
          }
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (stacked)
                Column(
                  children: [
                    monthField,
                    const SizedBox(height: 8),
                    sessionField,
                    const SizedBox(height: 8),
                    groupField,
                  ],
                )
              else
                Row(
                  children: [
                    Expanded(flex: 3, child: monthField),
                    const SizedBox(width: 10),
                    Expanded(flex: 4, child: sessionField),
                    const SizedBox(width: 10),
                    Expanded(flex: 4, child: groupField),
                  ],
                ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: actions,
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _searchBar() => Padding(
    padding: const EdgeInsets.fromLTRB(16, 0, 16, 10),
    child: LayoutBuilder(
      builder: (context, constraints) {
        final compact =
            constraints.maxWidth / MediaQuery.textScalerOf(context).scale(1) <
            1000;
        final matches = _search.text.trim().isEmpty
            ? const <Student>[]
            : _matches;
        final purpose = SegmentedButton<_AttendancePurpose>(
          key: const Key('attendance-purpose'),
          segments: const [
            ButtonSegment(
              value: _AttendancePurpose.registration,
              label: Text('التحضير'),
              icon: Icon(Icons.how_to_reg_outlined),
            ),
            ButtonSegment(
              value: _AttendancePurpose.studentLookup,
              label: Text('بحث عن طالب'),
              icon: Icon(Icons.search),
            ),
          ],
          selected: {_purpose},
          onSelectionChanged: !_canUseToolbar
              ? null
              : (selection) => _setPurpose(selection.single),
        );
        final hint = Text(
          _enterHint,
          key: const Key('attendance-enter-hint'),
          style: TextStyle(color: _palette.muted, fontSize: 13),
        );
        final search = TextField(
          key: const Key('student-search'),
          controller: _search,
          focusNode: _searchFocus,
          autofocus: true,
          readOnly:
              _attendanceWarning ||
              _shortcutConfirming ||
              _discountEditing ||
              _busy ||
              _submittingStudent ||
              _noteStudentId != null,
          onTapAlwaysCalled: true,
          onTap: () {
            final now = DateTime.now();
            final previous = _lastSearchTap;
            _lastSearchTap = now;
            if (previous != null &&
                now.difference(previous).inMilliseconds < 400) {
              _enableFreeSearch();
            }
          },
          decoration: InputDecoration(
            labelText: _freeSearchTyping
                ? 'بحث حر · اكتب الاسم أو الكود أو الهاتف ثم Enter'
                : 'امسح الكارت أو ابحث بالكود أو الباركود أو الاسم أو الموبايل',
            prefixIcon: const Icon(Icons.qr_code_scanner),
            suffixIcon: IconButton(
              key: const Key('enable-free-search'),
              tooltip: 'كتابة حرة بدون اختصارات الدفع · دبل كليك',
              onPressed: _canUseToolbar ? _enableFreeSearch : null,
              icon: const Icon(Icons.search),
            ),
          ),
          onChanged: (text) {
            if (_shortcutConfirming) {
              if (text.trim().isNotEmpty) {
                _confirmationScannerBlocked?.value = true;
              }
              return;
            }
            // Invalidate the selected student immediately for keyboard safety;
            // searching/rebuilding waits until the scanner or typist pauses.
            _studentResolved = false;
            _searchRefresh?.cancel();
            _searchRefresh = Timer(const Duration(milliseconds: 120), () {
              if (mounted) setState(() {});
            });
          },
          onEditingComplete: () {},
          onTapOutside: (_) => _focusCode(),
          onSubmitted: _submitStudent,
        );
        final actions = [
          FilledButton.tonalIcon(
            onPressed: !_canUseToolbar || !store.canCollect
                ? null
                : () => _editStudent(create: true),
            icon: Icon(Icons.person_add_alt_1),
            label: const Text('إضافة وتسجيل طالب · F4'),
          ),
          OutlinedButton.icon(
            onPressed: !_canUseToolbar ? null : _nextStudent,
            icon: Icon(Icons.skip_next),
            label: const Text('الطالب التالي · F6'),
          ),
          OutlinedButton.icon(
            key: const Key('open-student-spotlight'),
            onPressed: _canUseToolbar ? _openSpotlight : null,
            icon: const Icon(Icons.manage_search),
            label: const Text('بحث سريع · F3'),
          ),
        ];
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (compact) ...[
              search,
              const SizedBox(height: 8),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [purpose, ...actions],
              ),
              const SizedBox(height: 6),
              hint,
            ] else ...[
              Row(
                children: [
                  purpose,
                  const SizedBox(width: 8),
                  actions[2],
                  const SizedBox(width: 16),
                  Expanded(child: hint),
                ],
              ),
              const SizedBox(height: 6),
              Row(
                children: [
                  Expanded(child: search),
                  const SizedBox(width: 12),
                  actions[0],
                  const SizedBox(width: 12),
                  actions[1],
                ],
              ),
            ],
            if (_inlineNotice != null)
              Semantics(
                liveRegion: true,
                child: Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Tooltip(
                    message: _inlineNotice!,
                    child: Text(
                      _inlineNotice!,
                      key: const Key('attendance-operation-status'),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontSize: 12,
                        color: _inlineNoticeKind == NoticeKind.success
                            ? _palette.success
                            : _palette.accent,
                      ),
                    ),
                  ),
                ),
              ),
            if (_search.text.trim().isNotEmpty)
              Container(
                margin: const EdgeInsets.only(top: 8),
                decoration: BoxDecoration(
                  color: _palette.surface,
                  border: Border.all(color: _palette.line),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Column(
                  children: [
                    if (matches.isEmpty)
                      const Padding(
                        padding: EdgeInsets.all(14),
                        child: Text(
                          'لا توجد نتائج. يمكنك إضافة الطالب من الاختصار.',
                        ),
                      ),
                    ...matches.map(
                      (item) => ListTile(
                        dense: true,
                        leading: Icon(Icons.person_outline),
                        title: Text(item.name),
                        subtitle: Text(
                          'كود ${item.code} · ${item.phone.isEmpty ? 'بدون رقم موبايل' : item.phone}'
                          '${item.barcode.trim().isEmpty ? '' : '\nالباركود: ${item.barcode}'}',
                        ),
                        onTap: _busy || _submittingStudent
                            ? null
                            : () => _submitStudent(
                                _search.text,
                                chosenStudent: item,
                              ),
                      ),
                    ),
                  ],
                ),
              ),
          ],
        );
      },
    ),
  );

  Widget _sessionStartState() => Center(
    child: Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.lock_outline, size: 64, color: _palette.muted),
          const SizedBox(height: 20),
          Text(
            session == null
                ? 'اختار الشهر والحصة والمجموعة ثم ابدأ الحصة'
                : session?.status == SessionStatus.closed
                ? 'الحصة مغلقة؛ افتحها أو اعرض سجلها'
                : session?.status == SessionStatus.canceled
                ? 'الحصة ملغاة؛ اختار حصة مفتوحة'
                : 'اضغط «ابدأ الحصة» قبل التسجيل',
            style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 12),
          const Text(
            'الحصة المعدّة لا ترتبط بالمجموعة إلا عند البدء. البدء وحده لا يسجل حضورًا أو دفعًا.',
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 20),
          TextButton.icon(
            onPressed: _canExitWorkspace ? _exitWorkspace : null,
            icon: const Icon(Icons.settings_outlined),
            label: const Text('فتح الإدارة'),
          ),
        ],
      ),
    ),
  );

  Widget _emptyState() => Center(
    child: Padding(
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.qr_code_2, size: 76, color: _palette.accent),
          const SizedBox(height: 20),
          Text(
            store.groups.isEmpty
                ? 'ابدأ بإنشاء مجموعة من الإدارة'
                : session == null
                ? 'اختار حصة أو أنشئ حصة من الإدارة'
                : 'جاهز لاستقبال الطالب',
            style: TextStyle(fontSize: 26, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 10),
          const Text(
            'امسح الكارت أو ابحث عن الطالب. بياناته وحسابه وسجله يظهروا هنا.',
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 22),
          TextButton.icon(
            onPressed: _canExitWorkspace ? _exitWorkspace : null,
            icon: Icon(Icons.settings_outlined),
            label: const Text('فتح الإدارة'),
          ),
        ],
      ),
    ),
  );

  Future<void> _withHistoryModal(Future<void> Function() work) async {
    if (_historyModal ||
        _busy ||
        _discountEditing ||
        _shortcutConfirming ||
        _closedSessionWarning ||
        _reviewing ||
        _noteStudentId != null ||
        hasPendingMassarNotice(context) ||
        _attendanceWarning ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    setState(() => _historyModal = true);
    try {
      await work();
    } finally {
      if (mounted) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (!mounted) return;
          setState(() => _historyModal = false);
          _focusCode();
        });
        WidgetsBinding.instance.scheduleFrame();
      }
    }
  }

  Widget _studentWorkspace() => LayoutBuilder(
    builder: (context, constraints) {
      final cashier = _cashierPanel();
      final history = Container(
        decoration: BoxDecoration(
          color: _palette.surface,
          border: Border.all(color: _palette.line),
          borderRadius: BorderRadius.circular(10),
        ),
        child: StudentHistoryPanel(
          store: store,
          student: student!,
          collectionSessionId:
              _workspaceEnabled && session?.status == SessionStatus.open
              ? session!.id
              : null,
          actionsEnabled:
              !_busy &&
              !_historyModal &&
              !_shortcutConfirming &&
              !_closedSessionWarning &&
              !_discountEditing &&
              !_reviewing &&
              _noteStudentId == null &&
              !hasPendingMassarNotice(context),
          onOpenModal: _withHistoryModal,
          currentSessionId: session?.id,
          notesPanel: _studentNote(student!),
          onNote: _canChangeCard ? () => _editNote(student!) : null,
          onTwin: _canChangeCard ? () => _editStudent(focusTwin: true) : null,
          onCard: _canChangeCard
              ? () => _run(
                  () =>
                      DocumentService.showStudentCard(context, store, student!),
                  null,
                )
              : null,
          onSuspend: _canChangeStudentStatus && !student!.isSuspended
              ? _suspendSelectedStudent
              : null,
        ),
      );
      if (constraints.maxWidth / MediaQuery.textScalerOf(context).scale(1) <
          1000) {
        return MassarScrollView(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
          child: Column(
            children: [
              cashier.panel,
              const SizedBox(height: 12),
              cashier.entryAction,
              const SizedBox(height: 16),
              SizedBox(height: 460, child: history),
            ],
          ),
        );
      }
      return Padding(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Expanded(
              flex: 39,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(child: MassarScrollView(child: cashier.panel)),
                  const SizedBox(height: 12),
                  cashier.entryAction,
                ],
              ),
            ),
            const SizedBox(width: 20),
            Expanded(flex: 61, child: history),
          ],
        ),
      );
    },
  );

  Widget _cardPaymentControl(Student selected) {
    final payment = store.cardPaymentFor(selected.id);
    final base = store.cardSettings.price;
    final quote = base == null
        ? null
        : _discounted(base, selected.discountPercent);
    final unpaid =
        !selected.isSuspended &&
        payment == null &&
        store.cardReceiptFor(selected.id) == null &&
        quote != null;
    final mayPay = _canChangeCard && unpaid;
    return Row(
      children: [
        Expanded(
          child: OutlinedButton.icon(
            key: const Key('pay-student-card'),
            onPressed: mayPay ? _payStudentCard : null,
            icon: const Icon(Icons.badge_outlined, size: 17),
            label: Text(
              payment != null
                  ? 'الكارت مدفوع · ${money(store.cardCollectedFor(payment.id))}'
                  : quote == null
                  ? 'الكارت · السعر غير محدد'
                  : 'دفع الكارت · ${money(quote)}',
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ),
        if (payment == null && quote != null) ...[
          const SizedBox(width: 8),
          SizedBox(
            width: 130,
            child: DropdownButtonFormField<String>(
              key: ValueKey('card-payment-method-$_cardMethod'),
              initialValue: _cardMethod,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'الكارت',
                isDense: true,
              ),
              items: ['نقدي', 'إنستاباي', 'تحويل بنكي', 'بطاقة']
                  .map(
                    (method) => DropdownMenuItem(
                      value: method,
                      child: Text(method, overflow: TextOverflow.ellipsis),
                    ),
                  )
                  .toList(),
              // Opening this selector makes the page's route non-current.
              // Keep its callback available, then recheck after it closes.
              onChanged: _cardContextReady && unpaid
                  ? (value) {
                      if (!_canChangeCard ||
                          student?.id != selected.id ||
                          value == null) {
                        return;
                      }
                      setState(() => _cardMethod = value);
                      _focusCode();
                    }
                  : null,
            ),
          ),
        ],
      ],
    );
  }

  ({Widget panel, Widget entryAction}) _cashierPanel() {
    final currentStudent = student!;
    final currentGroup = group;
    final currentSession = session;
    final attendance = store.attendancesForStudent(currentStudent.id);
    final recordedAttendance = attendance
        .where(
          (record) =>
              record.sessionId == currentSession?.id &&
              record.status != AttendanceStatus.absent,
        )
        .firstOrNull;
    final twin = store.twinFor(currentStudent.id);
    final sourceGroup = _manualMakeupSource;
    final totalRemaining = sourceGroup != null
        ? store.remainingFor(currentStudent.id, sourceGroup.id)
        : _groupId == null
        ? 0
        : store.remainingFor(currentStudent.id, _groupId!);
    final remaining = sourceGroup != null && currentSession != null
        ? store.eligibleMakeupRemainingFor(
            currentStudent.id,
            currentSession.id,
            sourceGroup.id,
          )
        : currentSession == null || currentSession.kind != SessionKind.counted
        ? totalRemaining
        : store.eligibleRemainingFor(currentStudent.id, currentSession.id);
    final enrolled = currentStudent.groupIds.contains(_groupId);
    final hasAttendance = recordedAttendance != null;
    final needsPayment =
        currentSession != null &&
        store.attendanceNeedsPayment(currentStudent.id, currentSession.id);
    final retainedPayment =
        currentSession != null &&
        store.hasRetainedSessionPayment(currentStudent.id, currentSession.id);
    final registered = hasAttendance && !needsPayment;
    final centerOnly = _isCenterOnly(currentStudent, currentSession);
    final hasCenterFee =
        currentSession != null &&
        store.centerFeesFor(currentStudent.id, currentSession.id).isNotEmpty;
    final centerFeeDue = currentSession == null
        ? currentStudent.centerFeeAmount
        : store.centerFeeDueFor(currentStudent.id, currentSession.id);
    final centerFeeCollected = currentSession == null
        ? 0
        : store.centerFeeCollectedFor(currentStudent.id, currentSession.id);
    final centerFeeRemaining = _centerFeeAmount(currentStudent, currentSession);
    final eligible = currentSession == null
        ? <AttendanceRecord>[]
        : store.eligibleMakeups(currentStudent.id, currentSession.id);
    final makeupId = eligible.any((item) => item.id == _originalAttendanceId)
        ? _originalAttendanceId
        : null;
    final effectiveMode = currentSession == null
        ? _mode
        : _effectiveMode(currentStudent, currentSession);
    final isMakeup =
        recordedAttendance?.status == AttendanceStatus.makeup ||
        effectiveMode == EntryMode.makeup;
    final originalRecord = attendance
        .where(
          (record) =>
              record.id ==
              (recordedAttendance?.originalAttendanceId ??
                  _originalAttendanceId),
        )
        .firstOrNull;
    final originalSession = store.sessionById(originalRecord?.sessionId);
    final makeupGroupId =
        recordedAttendance?.makeupSourceGroupId ??
        sourceGroup?.id ??
        originalSession?.groupId;
    final makeupOrigin = makeupGroupId == null
        ? currentStudent.groupIds.map(store.groupLabel).join('، ')
        : store.groupLabel(makeupGroupId);
    final balanceGroupId = makeupGroupId ?? sourceGroup?.id ?? currentGroup?.id;
    final activePackages =
        store
            .packagesForStudent(currentStudent.id, groupId: balanceGroupId)
            .where(
              (package) =>
                  package.groupId == balanceGroupId && package.remaining > 0,
            )
            .toList()
          ..sort(
            (first, second) => second.purchasedAt.compareTo(first.purchasedAt),
          );
    final base = centerOnly
        ? 0
        : effectiveMode == EntryMode.makeup && sourceGroup != null
        ? remaining > 0
              ? 0
              : _mode == EntryMode.package
              ? _monthPrice(sourceGroup)
              : sourceGroup.sessionPrice
        : currentSession == null ||
              currentGroup == null ||
              effectiveMode == EntryMode.makeup ||
              retainedPayment ||
              currentSession.kind == SessionKind.free
        ? 0
        : currentSession.kind == SessionKind.extra
        ? currentSession.extraPrice
        : effectiveMode == EntryMode.package
        ? remaining > 0
              ? 0
              : _monthPrice(currentGroup)
        : currentGroup.sessionPrice;
    final net = currentStudent.packageMember
        ? 0
        : centerOnly
        ? _centerFeeAmount(currentStudent, currentSession)
        : base == null
        ? null
        : _discounted(base, currentStudent.discountPercent);
    final canEnter = _canEnter && !_submittingStudent && !_startingSession;
    final payments = store.paymentsForStudent(currentStudent.id).toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
    final panel = Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: _palette.surface,
        border: Border.all(color: _palette.line),
        borderRadius: BorderRadius.circular(10),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              CircleAvatar(
                radius: 20,
                backgroundColor: _palette.successSurface,
                child: Text(
                  currentStudent.name.isEmpty
                      ? '?'
                      : currentStudent.name.characters.first,
                  style: TextStyle(fontSize: 22, color: _palette.accent),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      currentStudent.name,
                      style: TextStyle(
                        fontSize: 21,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text('كود الطالب: ${currentStudent.code}'),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 6,
            runSpacing: 6,
            children: [
              _statusBadge(
                key: const Key('attendance-current-status'),
                label: currentStudent.isSuspended
                    ? 'مُصفّى'
                    : hasAttendance
                    ? isMakeup
                          ? 'معوّض'
                          : 'حاضر'
                    : 'لم يُسجل الحضور',
                icon: hasAttendance ? Icons.how_to_reg : Icons.person_outline,
                foreground: currentStudent.isSuspended || isMakeup
                    ? _palette.error
                    : hasAttendance
                    ? _palette.success
                    : _palette.muted,
                background: currentStudent.isSuspended || isMakeup
                    ? _palette.errorSurface
                    : hasAttendance
                    ? _palette.successSurface
                    : _palette.subtle,
              ),
              if (_lookupOnly)
                _statusBadge(
                  label: 'عرض السجل فقط',
                  icon: Icons.visibility_outlined,
                  foreground: _palette.muted,
                  background: _palette.subtle,
                )
              else if (store.canCollect && currentSession != null)
                _statusBadge(
                  key: const Key('attendance-payment-status'),
                  label: needsPayment
                      ? centerOnly
                            ? 'رسوم سنتر متبقية'
                            : 'الحضور محفوظ — الدفع مطلوب'
                      : registered
                      ? 'حساب الحصة مسجل'
                      : 'الدفع مستقل عن الحضور',
                  icon: needsPayment
                      ? Icons.payments_outlined
                      : Icons.receipt_long_outlined,
                  foreground: needsPayment ? _palette.warning : _palette.accent,
                  background: needsPayment
                      ? _palette.warningSurface
                      : _palette.subtle,
                ),
            ],
          ),
          if (currentStudent.isSuspended) ...[
            const SizedBox(height: 12),
            Text(
              'الطالب مُصفّى من السنتر — ${currentStudent.suspensionReason}',
              style: TextStyle(
                color: _palette.error,
                fontWeight: FontWeight.bold,
              ),
            ),
            if (store.canCollect)
              OutlinedButton.icon(
                onPressed: _canChangeStudentStatus
                    ? _reactivateSelectedStudent
                    : null,
                icon: const Icon(Icons.person_add_alt_1),
                label: const Text('إعادة تفعيل الطالب'),
              ),
          ],
          if (isMakeup) ...[
            const SizedBox(height: 14),
            Semantics(
              liveRegion: true,
              child: Container(
                key: const Key('attendance-makeup-status'),
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: _palette.errorSurface,
                  border: Border.all(color: _palette.error),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(Icons.swap_horiz_rounded, color: _palette.error),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'معوّض',
                            style: TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.bold,
                              color: _palette.error,
                            ),
                          ),
                          Text(
                            makeupOrigin.isEmpty
                                ? 'اختَر مجموعته الأصلية'
                                : 'من: $makeupOrigin',
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 6,
            children: [
              OutlinedButton.icon(
                key: const Key('attendance-transfer-student'),
                onPressed:
                    _canChangeContext &&
                        store.canCollect &&
                        currentStudent.groupIds.isNotEmpty &&
                        store.groups.length > 1
                    ? _transferStudent
                    : null,
                icon: const Icon(Icons.drive_file_move_outline),
                label: Text(
                  !enrolled
                      ? 'نقل الطالب للمجموعة الحالية'
                      : 'نقل الطالب لمجموعة أخرى',
                ),
              ),
              OutlinedButton.icon(
                key: const Key('attendance-edit-student-details'),
                onPressed: _canChangeContext && store.canCollect
                    ? () => _editStudent()
                    : null,
                icon: const Icon(Icons.manage_accounts_outlined),
                label: const Text('تعديل كل بيانات الطالب'),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 18,
            runSpacing: 8,
            children: [
              Text(
                'الطالب: ${currentStudent.phone.isEmpty ? '—' : currentStudent.phone}',
              ),
              Text(
                'ولي الأمر: ${currentStudent.guardianPhone.isEmpty ? '—' : currentStudent.guardianPhone}',
              ),
              Text(
                'الخصم الثابت: ${percentText(currentStudent.discountPercent)}٪',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
            ],
          ),
          const SizedBox(height: 10),
          SwitchListTile(
            key: const Key('attendance-package-member'),
            contentPadding: EdgeInsets.zero,
            title: const Text('باكدج'),
            subtitle: const Text(
              'علامة محفوظة — حضور بدون تحصيل حصة أو سنتر، ويظهر في التقفيلة.',
            ),
            value: currentStudent.packageMember,
            onChanged: _canChangeCard
                ? (enabled) => _run(() async {
                    await store.setStudentPackageMember(
                      studentId: currentStudent.id,
                      enabled: enabled,
                      sessionId: currentSession?.id,
                    );
                    if (mounted) {
                      _applyStudentSelection(
                        store.students.firstWhere(
                          (s) => s.id == currentStudent.id,
                        ),
                      );
                    }
                  }, null)
                : null,
          ),
          if (!currentStudent.packageMember)
            SwitchListTile(
              key: const Key('attendance-center-fee-enabled'),
              contentPadding: EdgeInsets.zero,
              title: const Text('رسوم السنتر'),
              subtitle: const Text(
                'اختيار محفوظ للطالب، مستقل عن الخصم. التفعيل لا يسجل دفعًا.',
              ),
              value: currentStudent.centerFeeEnabled,
              onChanged: _canChangeCard
                  ? (enabled) => _run(() async {
                      await store.setStudentCenterFee(
                        studentId: currentStudent.id,
                        enabled: enabled,
                      );
                      if (mounted) {
                        _applyStudentSelection(
                          store.students.firstWhere(
                            (s) => s.id == currentStudent.id,
                          ),
                        );
                      }
                    }, null)
                  : null,
            ),
          if (currentStudent.centerFeeEnabled) ...[
            Text('رسوم السنتر المدفوعة: ${money(centerFeeCollected)}'),
            OutlinedButton.icon(
              key: const Key('attendance-collect-center-fee'),
              onPressed:
                  _canChangeCard &&
                      currentSession != null &&
                      centerFeeRemaining > 0
                  ? _collectCenterFee
                  : null,
              icon: const Icon(Icons.payments_outlined),
              label: Text(
                centerFeeRemaining > 0
                    ? 'تحصيل رسوم السنتر · ${money(centerFeeRemaining)} · C'
                    : 'رسوم السنتر مدفوعة',
              ),
            ),
          ],
          if (centerOnly)
            _message(
              'المدرس معفى ١٠٠٪ · رسوم السنتر: المطلوب ${money(centerFeeDue)} · إجمالي المدفوع ${money(centerFeeCollected)} · المتبقي ${money(centerFeeRemaining)}. الحضور مستقل عن التحصيل.',
              warning: centerFeeRemaining > 0,
            ),
          Text(currentStudent.groupIds.map(store.groupLabel).join('، ')),
          const SizedBox(height: 6),
          Text(
            twin == null
                ? 'التوأم: لا يوجد'
                : 'التوأم: ${twin.name} · ${twin.code}${twin.discountPercent == 100 ? ' — معفى ١٠٠٪' : ''}',
            key: const Key('attendance-student-twin'),
          ),
          if (currentStudent.twinStudentId != null &&
              currentStudent.discountPercent == 100)
            const Text(
              'هذا الطالب معفى بنسبة ١٠٠٪.',
              style: TextStyle(fontWeight: FontWeight.bold),
            ),

          if (store.canCollect &&
              store.studentDebtFor(currentStudent.id) > 0) ...[
            const SizedBox(height: 8),
            Text(
              'إجمالي المديونية: ${money(store.studentDebtFor(currentStudent.id))}',
              key: const Key('student-total-debt'),
              style: TextStyle(
                color: _palette.error,
                fontWeight: FontWeight.bold,
              ),
            ),
            OutlinedButton.icon(
              key: const Key('attendance-settle-debt'),
              onPressed: _canChangeCard ? _settleStudentDebts : null,
              icon: const Icon(Icons.account_balance_wallet_outlined),
              label: const Text('سداد المديونية'),
            ),
          ],
          if (store.canCollect) ...[
            const SizedBox(height: 8),
            _cardPaymentControl(currentStudent),
          ],
          const SizedBox(height: 6),
          Wrap(
            spacing: 4,
            runSpacing: 4,
            children: [
              if (store.canEditDiscount)
                TextButton.icon(
                  key: const Key('attendance-discount'),
                  onPressed: _canEditDiscount ? _editDiscount : null,
                  icon: const Icon(Icons.percent, size: 18),
                  label: const Text('خصم سريع · S'),
                ),
              TextButton.icon(
                onPressed: _discountEditing || _busy || payments.isEmpty
                    ? null
                    : () => _run(
                        () => DocumentService.showReceipt(
                          context,
                          store,
                          payments.first,
                        ),
                        null,
                      ),
                icon: Icon(Icons.receipt_long_outlined, size: 18),
                label: const Text('آخر إيصال'),
              ),
            ],
          ),
          const Divider(height: 20),
          Row(
            children: [
              Expanded(
                child: Text(
                  totalRemaining == remaining
                      ? sourceGroup != null
                            ? 'رصيد باقة المجموعة الأصلية'
                            : 'رصيد الباقة لهذه المجموعة'
                      : 'المتاح للحصة الحالية',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
              Text(
                '$remaining حصص',
                key: const Key('package-remaining'),
                style: TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.bold,
                  color: _palette.accent,
                ),
              ),
            ],
          ),
          if (activePackages.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                activePackages
                    .map(
                      (package) =>
                          '${package.monthPlanName ?? 'شهر'} · ${package.remaining} من ${package.totalSessions} حصص',
                    )
                    .join(' · '),
                key: const Key('active-month-packages'),
                style: TextStyle(
                  color: _palette.accent,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          const SizedBox(height: 10),
          if (needsPayment)
            _message(
              centerOnly
                  ? '${isMakeup ? 'معوّض' : 'حاضر'} — باقي رسوم السنتر ${money(centerFeeRemaining)}. C للسداد دون تكرار الحضور.'
                  : '${isMakeup ? 'معوّض' : 'حاضر'} — غير مدفوع. الحضور محفوظ؛ L للحصة أو N للشهر بدون تكرار الحضور.',
              warning: true,
            ),
          if (retainedPayment)
            _message('مسددة مسبقًا — تسجيل حضور دون دفع جديد.'),
          if (registered) ...[
            _message(
              '${isMakeup ? 'الطالب معوّض في هذه الحصة' : 'حضور الطالب مسجل في هذه الحصة'}. لن يتكرر التحصيل أو الخصم.',
            ),
            TextButton.icon(
              key: const Key('duplicate-attendance-alert'),
              onPressed: _readyForAttendanceWarning
                  ? _warnSameSessionAttendance
                  : null,
              icon: const Icon(Icons.info_outline),
              label: const Text('الحضور مسجل — عرض التنبيه'),
            ),
          ],
          if (currentSession?.status == SessionStatus.canceled)
            _message('الحصة ملغاة ولا تقبل تسجيل حضور.', warning: true),
          if (!currentStudent.packageMember &&
              !centerOnly &&
              !registered &&
              !retainedPayment &&
              enrolled &&
              remaining == 0 &&
              currentSession?.kind == SessionKind.counted)
            _message(
              totalRemaining == 0
                  ? 'لا يوجد رصيد حصص. حصّل الحصة أو اختار شهرًا للتحصيل.'
                  : 'رصيدك $totalRemaining حصص من مشتريات أحدث، ولا يغطي هذه الحصة القديمة. حصّل الحصة أو باقة تبدأ بها.',
              warning: true,
            ),
          if (!currentStudent.packageMember &&
              !centerOnly &&
              !registered &&
              !retainedPayment &&
              enrolled &&
              currentSession?.kind == SessionKind.counted &&
              remaining > 0)
            _message(
              'الباقة السارية تغطي هذه الحصة. الدخول العادي يُحسب من رصيدها.',
            ),
          if (!currentStudent.packageMember &&
              !centerOnly &&
              currentSession?.kind == SessionKind.free &&
              !isMakeup)
            _message('هذه الحصة مجانية ولا تُخصم من الباقة.'),
          if (!currentStudent.packageMember &&
              !centerOnly &&
              currentSession?.kind == SessionKind.extra &&
              !isMakeup)
            _message('هذه الحصة بسعر منفصل ولا تُخصم من الباقة.'),
          if (currentSession != null && !registered) ...[
            if (enrolled &&
                !_hasPendingSourceMakeup(currentStudent, currentSession))
              TextButton.icon(
                onPressed: _busy
                    ? null
                    : () => setState(() {
                        _mode = effectiveMode == EntryMode.makeup
                            ? _preferredMode(currentStudent)
                            : EntryMode.makeup;
                        // An enrolled student can return from makeup to normal entry.
                        if (enrolled && effectiveMode == EntryMode.makeup) {
                          _mode = remaining > 0
                              ? EntryMode.package
                              : EntryMode.single;
                        }
                        _originalAttendanceId = null;
                      }),
                icon: Icon(Icons.swap_horiz),
                label: Text(
                  effectiveMode == EntryMode.makeup
                      ? 'الرجوع للدفع العادي'
                      : 'تعويض حصة غابها',
                ),
              ),
            const SizedBox(height: 12),
            if (effectiveMode == EntryMode.makeup)
              ExpansionTile(
                tilePadding: EdgeInsets.zero,
                title: const Text('تفاصيل التعويض'),
                children: [
                  if (eligible.isEmpty && _manualMakeupSource == null)
                    const Padding(
                      padding: EdgeInsets.only(bottom: 12),
                      child: Text(
                        'لا يوجد غياب مدفوع مؤهل للتعويض لهذه الحصة.',
                      ),
                    ),
                  if (_manualMakeupSource != null) ...[
                    Text(
                      'يُستخدم رصيد ${store.groupLabel(_manualMakeupSource!.id)} إن توفر، وإلا يمكنك دفع الحصة أو الباقة.',
                    ),
                    if (store
                            .eligibleMakeupSourceGroups(
                              currentStudent.id,
                              currentSession.id,
                            )
                            .length >
                        1)
                      DropdownButtonFormField<String>(
                        key: ValueKey(
                          'makeup-source-${_manualMakeupSource!.id}',
                        ),
                        initialValue: _manualMakeupSource!.id,
                        isExpanded: true,
                        decoration: const InputDecoration(
                          labelText: 'المجموعة الأصلية للتعويض',
                        ),
                        items: store
                            .eligibleMakeupSourceGroups(
                              currentStudent.id,
                              currentSession.id,
                            )
                            .map(
                              (source) => DropdownMenuItem(
                                value: source.id,
                                child: Text(store.groupLabel(source.id)),
                              ),
                            )
                            .toList(),
                        onChanged: _shortcutConfirming || _busy
                            ? null
                            : (source) {
                                setState(() => _makeupSourceGroupId = source);
                                _focusCode();
                              },
                      ),
                  ],
                  if (enrolled && eligible.isNotEmpty)
                    DropdownButtonFormField<String>(
                      key: ValueKey('makeup-$makeupId'),
                      initialValue: makeupId,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'الحصة الأصلية التي يعوضها',
                      ),
                      items: eligible.map((item) {
                        final original = _byId(
                          store.sessions,
                          item.sessionId,
                          (value) => value.id,
                        );
                        return DropdownMenuItem(
                          value: item.id,
                          child: Text(
                            original == null
                                ? 'الحصة الأصلية غير متاحة'
                                : '${sessionLabel(original)} · ${sessionDateLabel(original)} · ${store.groupLabel(original.groupId)}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        );
                      }).toList(),
                      onChanged:
                          _discountEditing || _busy || _noteStudentId != null
                          ? null
                          : (value) {
                              setState(() => _originalAttendanceId = value);
                              _focusCode();
                            },
                    ),
                  const SizedBox(height: 16),
                ],
              ),
            if (!currentStudent.packageMember &&
                (centerOnly || base != null && base > 0))
              ExpansionTile(
                key: const Key('payment-details'),
                tilePadding: EdgeInsets.zero,
                title: Text('تفاصيل الدفع · $_method'),
                children: [
                  if (!currentStudent.packageMember &&
                      !centerOnly &&
                      base != null) ...[
                    _detail('السعر الأصلي', money(base)),
                    _detail('قيمة الخصم', money(base - net!)),
                  ],
                  DropdownButtonFormField<String>(
                    initialValue: _method,
                    decoration: const InputDecoration(labelText: 'طريقة الدفع'),
                    items: ['نقدي', 'إنستاباي', 'تحويل بنكي', 'بطاقة']
                        .map(
                          (value) => DropdownMenuItem(
                            value: value,
                            child: Text(value),
                          ),
                        )
                        .toList(),
                    onChanged:
                        _discountEditing || _busy || _noteStudentId != null
                        ? null
                        : (value) {
                            setState(() => _method = value!);
                            _focusCode();
                          },
                  ),
                  const SizedBox(height: 16),
                ],
              ),
            Wrap(
              alignment: WrapAlignment.spaceBetween,
              crossAxisAlignment: WrapCrossAlignment.center,
              spacing: 12,
              runSpacing: 4,
              children: [
                const Text(
                  'المطلوب الآن',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
                Text(
                  net == null ? 'السعر غير محدد' : money(net),
                  key: const Key('amount-due'),
                  style: TextStyle(
                    fontSize: 28,
                    fontWeight: FontWeight.w800,
                    color: _palette.ink,
                  ),
                ),
              ],
            ),
            if (!currentStudent.packageMember &&
                !centerOnly &&
                effectiveMode == EntryMode.package)
              Padding(
                padding: const EdgeInsets.only(top: 8),
                child: Text(
                  remaining > 0
                      ? 'سيُستخدم رصيد الباقة. المتبقي بعد الدخول: ${remaining - 1}.'
                      : net == null
                      ? 'حدد سعر الباقة في المجموعة قبل التحصيل.'
                      : 'تحصيل $_monthLabel (${_countLabel(_packageSessions)}) وتسجيل الحضور. المتبقي بعد الدخول: ${_packageSessions - 1}.',
                ),
              ),
            const SizedBox(height: 18),
          ],
          if (!centerOnly &&
              !registered &&
              enrolled &&
              store.canCollect &&
              remaining > 0)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: OutlinedButton.icon(
                onPressed: _discountEditing || _busy || _lookupOnly
                    ? null
                    : _renew,
                icon: Icon(Icons.add_card_outlined),
                label: Text(
                  _monthPrice(currentGroup) == null
                      ? 'اختيار وتحصيل شهر جديد'
                      : 'تحصيل $_monthLabel (${_countLabel(_packageSessions)}) · ${money(_discounted(_monthPrice(currentGroup)!, currentStudent.discountPercent))}',
                ),
              ),
            ),
          const SizedBox(height: 12),
          TextButton.icon(
            onPressed: _discountEditing || _busy || _noteStudentId != null
                ? null
                : _nextStudent,
            icon: Icon(Icons.arrow_back),
            label: const Text('استقبال الطالب التالي · F6'),
          ),
        ],
      ),
    );
    return (
      panel: panel,
      entryAction: currentSession == null
          ? const SizedBox.shrink()
          : registered
          ? Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                OutlinedButton.icon(
                  key: const Key('collect-attend'),
                  onPressed: _readyForAttendanceWarning
                      ? _warnRecordedPayment
                      : null,
                  icon: Icon(
                    Icons.check_circle_outline,
                    color: _palette.success,
                  ),
                  label: Text(
                    currentStudent.packageMember
                        ? 'باكدج — الحضور مسجل دون تحصيل'
                        : centerOnly
                        ? 'رسوم السنتر مسجلة · عرض الحساب · L'
                        : 'الحصة مغطاة بالفعل · عرض الحساب · L',
                  ),
                ),
                if (!currentStudent.packageMember &&
                    !centerOnly &&
                    currentSession.kind == SessionKind.counted &&
                    enrolled) ...[
                  const SizedBox(height: 6),
                  _packageQuantity(),
                  const SizedBox(height: 6),
                  FilledButton.icon(
                    key: const Key('collect-package'),
                    onPressed: _canChangeCard && !currentStudent.isSuspended
                        ? _renew
                        : null,
                    icon: const Icon(Icons.add_card_outlined),
                    label: Text(
                      'تحصيل شهر جديد · $_monthLabel (${_countLabel(_packageSessions)})',
                    ),
                  ),
                ],
              ],
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              mainAxisSize: MainAxisSize.min,
              children: [
                if (currentStudent.packageMember)
                  FilledButton.icon(
                    key: const Key('package-member-attend'),
                    onPressed: canEnter ? () => _collect() : null,
                    icon: const Icon(Icons.verified_outlined),
                    label: const Text('باكدج — تسجيل حضور دون تحصيل'),
                  )
                else if (centerOnly)
                  FilledButton.icon(
                    key: const Key('collect-attend'),
                    onPressed: canEnter
                        ? () => _collect(
                            mode: effectiveMode == EntryMode.makeup
                                ? EntryMode.makeup
                                : EntryMode.single,
                          )
                        : null,
                    icon: const Icon(Icons.storefront_outlined),
                    label: Text(
                      '${hasCenterFee ? 'باقي رسوم السنتر' : 'تحصيل رسوم السنتر'} · ${money(net!)} · L',
                    ),
                  )
                else if (retainedPayment)
                  FilledButton.icon(
                    key: const Key('collect-attend'),
                    onPressed: canEnter
                        ? () => _collect(mode: EntryMode.single)
                        : null,
                    icon: const Icon(Icons.check_circle_outline),
                    label: const Text('مسددة مسبقًا — تسجيل حضور · L'),
                  )
                else if (effectiveMode == EntryMode.makeup) ...[
                  FilledButton.icon(
                    key: const Key('collect-attend'),
                    onPressed: canEnter
                        ? () => _collect(mode: EntryMode.makeup)
                        : null,
                    icon: Icon(Icons.payments_outlined),
                    label: Text(
                      sourceGroup != null && remaining == 0
                          ? 'دفع الحصة · ${money(_discounted(sourceGroup.sessionPrice, currentStudent.discountPercent))} · L'
                          : 'تسجيل من الباقة · بدون دفع جديد · L',
                    ),
                  ),
                  if (sourceGroup != null && remaining == 0) ...[
                    const SizedBox(height: 8),
                    _packageQuantity(),
                    const SizedBox(height: 8),
                    FilledButton.icon(
                      key: const Key('collect-makeup-package'),
                      onPressed: canEnter && _monthPrice(sourceGroup) != null
                          ? () => _confirmShortcut(EntryMode.package)
                          : null,
                      icon: const Icon(Icons.calendar_month),
                      label: Text(
                        _monthPrice(sourceGroup) == null
                            ? 'دفع $_monthLabel (${_countLabel(_packageSessions)}) · السعر غير محدد'
                            : 'دفع $_monthLabel (${_countLabel(_packageSessions)}) · ${money(_discounted(_monthPrice(sourceGroup)!, currentStudent.discountPercent))} · N',
                      ),
                    ),
                  ],
                ] else if (currentSession.kind == SessionKind.counted) ...[
                  _packageQuantity(),
                  const SizedBox(height: 8),
                  if (_monthPrice(currentGroup) == null && remaining == 0)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: Text(
                        'حدد سعر $_monthLabel (${_countLabel(_packageSessions)}) في المجموعة أولًا.',
                        key: const Key('package-price-unconfigured'),
                      ),
                    ),
                  FilledButton.icon(
                    key: const Key('collect-attend'),
                    onPressed: canEnter && remaining == 0
                        ? () => _collect(mode: EntryMode.single)
                        : null,
                    icon: Icon(Icons.payments_outlined),
                    label: Text(
                      'دفع الحصة · ${money(_discounted(currentGroup?.sessionPrice ?? 0, currentStudent.discountPercent))} · L',
                    ),
                  ),
                  const SizedBox(height: 8),
                  FilledButton.icon(
                    key: const Key('collect-package'),
                    onPressed:
                        canEnter &&
                            (remaining > 0 || _monthPrice(currentGroup) != null)
                        ? () => _collect(mode: EntryMode.package)
                        : null,
                    icon: Icon(Icons.calendar_month_outlined),
                    label: Text(
                      remaining > 0
                          ? 'تسجيل من الباقة · بدون دفع جديد · N'
                          : _monthPrice(currentGroup) == null
                          ? 'دفع $_monthLabel (${_countLabel(_packageSessions)}) · السعر غير محدد'
                          : 'دفع $_monthLabel (${_countLabel(_packageSessions)}) · ${money(_discounted(_monthPrice(currentGroup)!, currentStudent.discountPercent))} · N',
                    ),
                  ),
                ] else
                  FilledButton.icon(
                    key: const Key('collect-attend'),
                    onPressed: canEnter
                        ? () => _collect(mode: EntryMode.single)
                        : null,
                    icon: Icon(Icons.check_circle_outline),
                    label: Text(
                      currentSession.kind == SessionKind.free
                          ? 'تسجيل الحضور المجاني · L'
                          : 'دفع الحصة الإضافية · ${money(net!)} · L',
                    ),
                  ),
              ],
            ),
    );
  }

  Widget _packageQuantity() {
    final plans = _visibleMonthPlans(_manualMakeupSource ?? group);
    return DropdownButtonFormField<String>(
      key: ValueKey('month-plan-${_monthPlan?.id}'),
      initialValue: _monthPlan?.id,
      isExpanded: true,
      decoration: const InputDecoration(labelText: 'الشهر', isDense: true),
      items: plans
          .map(
            (plan) => DropdownMenuItem(
              value: plan.id,
              child: Tooltip(
                message:
                    '${plan.name} · ${plan.sessions} حصص · ${money(plan.price)}',
                child: Text(
                  '${plan.name} · ${plan.sessions} حصص',
                  overflow: TextOverflow.ellipsis,
                ),
              ),
            ),
          )
          .toList(),
      onChanged: _packageContextAvailable
          ? (id) {
              if (id != null) _chooseMonth(id, fromMenu: true);
            }
          : null,
    );
  }

  Widget _statusBadge({
    Key? key,
    required String label,
    required IconData icon,
    required Color foreground,
    required Color background,
  }) => Container(
    key: key,
    padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
    decoration: BoxDecoration(
      color: background,
      borderRadius: BorderRadius.circular(6),
    ),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: foreground),
        const SizedBox(width: 6),
        Flexible(
          child: Text(
            label,
            style: TextStyle(color: foreground, fontWeight: FontWeight.w600),
          ),
        ),
      ],
    ),
  );

  Widget _detail(String label, String value) => Padding(
    padding: const EdgeInsets.only(bottom: 6),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(width: 100, child: Text(label)),
        Expanded(
          child: Text(
            value.isEmpty ? '—' : value,
            style: TextStyle(fontWeight: FontWeight.w600),
          ),
        ),
      ],
    ),
  );
}

class _SearchIntent extends Intent {
  const _SearchIntent();
}

class _SpotlightIntent extends Intent {
  const _SpotlightIntent();
}

class _NewStudentIntent extends Intent {
  const _NewStudentIntent();
}

class _NextIntent extends Intent {
  const _NextIntent();
}
