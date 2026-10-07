import '../../shared/workspace_draft_guard.dart';
import 'package:massar_center/shared/student_lookup_dialog.dart';
import 'package:massar_center/domain/student_lookup.dart';
import 'package:massar_center/domain/discount_calculation.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'management_widgets.dart';
import '../attendance/record_cancellation_dialog.dart';

enum _CorrectionAction {
  reverseEntry,
  changeEntry,
  absentPresent,
  paymentMethod,
  refundPackage,
  reopenClosing,
}

/// Collector corrections preserve the original records and require a logged reason.
class CorrectionsPage extends StatefulWidget {
  const CorrectionsPage({
    super.key,
    required this.store,
    this.initialStudentId,
    this.initialSessionId,
  });
  final CenterStore store;
  final String? initialStudentId, initialSessionId;
  @override
  State<CorrectionsPage> createState() => _CorrectionsPageState();
}

class _CorrectionsPageState extends State<CorrectionsPage> {
  final _code = TextEditingController(), _reason = TextEditingController();
  final _codeFocus = FocusNode();
  final _form = GlobalKey<FormState>();
  String? _studentId, _sessionId, _paymentId, _originalId, _error, _notice;
  _CorrectionAction? _action;
  EntryMode _mode = EntryMode.single;
  String? _monthPlanId;
  String _method = 'نقدي';
  bool _busy = false, _confirming = false, _queryChanged = false;
  int _contextRevision = 0;
  bool get _dirty => _action != null || _reason.text.trim().isNotEmpty;
  CenterStore get store => widget.store;
  bool get _locked => _busy || _confirming;
  Student? get _student =>
      store.students.where((s) => s.id == _studentId).firstOrNull;
  LessonSession? get _session =>
      store.sessions.where((s) => s.id == _sessionId).firstOrNull;
  AttendanceRecord? get _attendance => store.attendances
      .where((a) => a.studentId == _studentId && a.sessionId == _sessionId)
      .firstOrNull;
  PaymentRecord? get _payment =>
      store.payments.where((p) => p.id == _paymentId).firstOrNull;
  StudyGroup? get _billingGroup => store.groups
      .where(
        (group) =>
            group.id == (_attendance?.makeupSourceGroupId ?? _session?.groupId),
      )
      .firstOrNull;
  List<GroupMonthPlan> get _monthPlans =>
      _billingGroup?.effectiveMonthPlans ?? const [];
  GroupMonthPlan? get _selectedMonthPlan => _monthPlanId == null
      ? _monthPlans.firstOrNull
      : _monthPlans.where((plan) => plan.id == _monthPlanId).firstOrNull;

  String? get _initialMonthPlanId {
    final original = store.allPackages
        .where((package) => package.id == _attendance?.packageId)
        .firstOrNull;
    return _monthPlans.any((plan) => plan.id == original?.monthPlanId)
        ? original!.monthPlanId
        : _monthPlans.firstOrNull?.id;
  }

  SessionClosing? get _closing =>
      store.closings.where((c) => c.sessionId == _sessionId).firstOrNull;
  List<PaymentRecord> get _payments =>
      store.payments
          .where(
            (p) =>
                p.studentId == _studentId &&
                (_sessionId == null || p.sessionId == _sessionId),
          )
          .toList()
        ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  List<LessonSession> get _sessions =>
      store.sessions
          .where(
            (s) =>
                _student?.groupIds.contains(s.groupId) == true ||
                store.allAttendances.any(
                  (a) => a.studentId == _studentId && a.sessionId == s.id,
                ) ||
                store.allPayments.any(
                  (p) => p.studentId == _studentId && p.sessionId == s.id,
                ),
          )
          .toList()
        ..sort(compareSessionsNewestFirst);

  @override
  void initState() {
    super.initState();
    _reason.addListener(_draftChanged);
    final initial = store.students
        .where((s) => s.id == widget.initialStudentId)
        .firstOrNull;
    if (initial != null) {
      _studentId = initial.id;
      _code.text = initial.code;
      if (_sessions.any((s) => s.id == widget.initialSessionId)) {
        _sessionId = widget.initialSessionId;
      }
    }
  }

  void _draftChanged() {
    if (mounted) setState(() {});
  }

  Future<bool> _mayChangeContext() async {
    if (_locked) return false;
    if (!_dirty) return true;
    setState(() => _confirming = true);
    try {
      return await confirmDiscardDraft(context, dirty: true);
    } finally {
      if (mounted) setState(() => _confirming = false);
    }
  }

  Future<void> _changeContext(VoidCallback change) async {
    final accepted = await _mayChangeContext();
    if (!mounted) return;
    setState(() {
      if (accepted) change();
      _contextRevision++;
    });
  }

  @override
  void dispose() {
    _code.dispose();
    _reason.removeListener(_draftChanged);
    _reason.dispose();
    _codeFocus.dispose();
    super.dispose();
  }

  void _reset() {
    _monthPlanId = null;
    _paymentId = null;
    _action = null;
    _originalId = null;
    _reason.clear();
    _error = null;
    _notice = null;
  }

  Future<void> _lookup(String value) async {
    final accepted = await _mayChangeContext();
    if (!mounted) return;
    if (!accepted) {
      final student = _student;
      if (student != null) {
        setState(() {
          _code.text = student.code;
          _queryChanged = false;
        });
      }
      return;
    }
    final matches = studentsWithIdentifier(store.students, value);
    setState(() => _busy = true);
    try {
      final student = matches.length == 1
          ? matches.single
          : matches.isEmpty
          ? null
          : await chooseMatchingStudent(context, matches);
      if (!mounted) return;
      if (student != null) {
        setState(() {
          _reset();
          _sessionId = null;
          _studentId = student.id;
          _code.text = student.code;
          _queryChanged = false;
        });
      } else if (matches.isEmpty) {
        setState(() {
          _error = 'لم نجد هذا الكود أو الباركود. راجع الرقم أو امسح الكارت.';
        });
        await showManagementMessage(context, _error!, kind: NoticeKind.warning);
      } else if (_student != null) {
        setState(() {
          _code.text = _student!.code;
          _queryChanged = false;
        });
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted &&
              !_locked &&
              ModalRoute.of(context)?.isCurrent == true) {
            _codeFocus.requestFocus();
            _code.selection = TextSelection(
              baseOffset: 0,
              extentOffset: _code.text.length,
            );
          }
        });
      }
    }
  }

  String _label(_CorrectionAction action) => switch (action) {
    _CorrectionAction.reverseEntry => 'إلغاء حضور مسجل بالخطأ',
    _CorrectionAction.changeEntry => 'تغيير طريقة دخول الحصة',
    _CorrectionAction.absentPresent => 'تصحيح غياب إلى حضور',
    _CorrectionAction.paymentMethod => 'تصحيح وسيلة الدفع',
    _CorrectionAction.refundPackage => 'استرداد باقة لم تُستخدم',
    _CorrectionAction.reopenClosing => 'إعادة فتح التقفيلة المالية',
  };
  List<_CorrectionAction> get _actions {
    final values = <_CorrectionAction>[];
    if (_closing != null) return [_CorrectionAction.reopenClosing];
    final attendance = _attendance;
    if (attendance != null) {
      if (attendance.status == AttendanceStatus.absent &&
          _session?.status == SessionStatus.closed) {
        values.add(_CorrectionAction.absentPresent);
      } else if (attendance.status != AttendanceStatus.absent) {
        values.add(_CorrectionAction.reverseEntry);
        if (_session?.status == SessionStatus.open) {
          values.add(_CorrectionAction.changeEntry);
        }
      }
    }
    if (_payment != null) {
      values.add(_CorrectionAction.paymentMethod);
      if (_payment!.packageId != null &&
          store.packages.any(
            (p) =>
                p.id == _payment!.packageId && p.remaining == p.totalSessions,
          ) &&
          !store.attendances.any((a) => a.packageId == _payment!.packageId)) {
        values.add(_CorrectionAction.refundPackage);
      }
    }
    return values;
  }

  int get _entryRefund => store.payments
      .where(
        (p) =>
            p.studentId == _studentId &&
            p.sessionId == _sessionId &&
            p.packageId == null,
      )
      .fold(0, (sum, p) => sum + store.paymentCollectedFor(p.id));
  PaymentRecord? get _refundableEntryPackage {
    if (_session?.status != SessionStatus.open ||
        _attendance?.packageId == null) {
      return null;
    }
    final package = store.packages
        .where((p) => p.id == _attendance!.packageId)
        .firstOrNull;
    if (package == null || package.remaining != package.totalSessions - 1) {
      return null;
    }
    return store.payments
        .where((p) => p.id == package.paymentId && p.sessionId == _sessionId)
        .firstOrNull;
  }

  bool get _canSwitchToSingle {
    if (_session?.kind != SessionKind.counted) return true;
    return _attendance?.packageId != null
        ? _refundableEntryPackage != null
        : _studentId != null &&
              _sessionId != null &&
              store.eligibleRemainingFor(_studentId!, _sessionId!) == 0;
  }

  int _discount(int amount) =>
      discountedAmount(amount, _student?.discountPercent ?? 0);

  String _packageCorrectionQuote(StudyGroup? group) {
    final plan = _selectedMonthPlan;
    if (plan == null || group?.priceConfigured != true) {
      return 'يُستخدم الرصيد المؤهل بعد التصحيح. عند عدم وجود رصيد، اختر شهرًا صالحًا واضبط أسعار المجموعة أولًا.';
    }
    return 'يُستخدم الرصيد المؤهل بعد التصحيح من ${group!.name}، أو يُشترى ${plan.name} — ${plan.sessions} حصص بقيمة ${money(_discount(plan.price))} عند عدم وجود رصيد.';
  }

  String get _preview {
    final student = _student,
        session = _session,
        attendance = _attendance,
        payment = _payment;
    if (_action == null || student == null) {
      return 'اختار العملية والإجراء الذي تريد تنفيذه.';
    }
    final group = _billingGroup;
    final base = attendance?.makeupSourceGroupId != null
        ? group?.sessionPrice ?? 0
        : session?.kind == SessionKind.free
        ? 0
        : session?.kind == SessionKind.extra
        ? session!.extraPrice
        : group?.sessionPrice ?? 0;
    return switch (_action!) {
      _CorrectionAction.reverseEntry =>
        'إلغاء تسجيل ${attendance == null ? '' : attendanceLabel(attendance.status)} لهذه الحصة. ${_entryRefund > 0
            ? 'رد ${money(_entryRefund)} بطريقة $_method.'
            : attendance?.packageId != null
            ? session?.status == SessionStatus.closed && attendance?.makeupSourceGroupId == null
                  ? 'تحويل الحضور إلى غياب مدفوع؛ يظل خصم الحصة من الباقة كما هو.'
                  : 'إرجاع الحصة إلى رصيد الباقة.'
            : 'بدون رد مبلغ.'}',
      _CorrectionAction.changeEntry =>
        'استبدال الدخول الحالي بـ${_mode == EntryMode.single
            ? 'دفع حصة'
            : _mode == EntryMode.package
            ? 'دخول بالشهر / رصيد سابق'
            : 'تعويض'}. ${_entryRefund > 0
            ? 'يُرد الدفع الأصلي ${money(_entryRefund)} ثم يُحسب الدخول الجديد.'
            : _mode == EntryMode.single && _refundableEntryPackage != null
            ? 'رد دفعة الباقة الجديدة ${money(store.paymentCollectedFor(_refundableEntryPackage!.id))} ثم تحصيل الحصة.'
            : 'يُعاد أثر الدخول القديم قبل حساب الجديد.'} ${_mode == EntryMode.single
            ? 'سعر الحصة بعد الخصم: ${money(_discount(base))}.'
            : _mode == EntryMode.package
            ? _packageCorrectionQuote(group)
            : 'يتطلب اختيار غياب مدفوع مؤهل.'}',
      _CorrectionAction.absentPresent =>
        'تصحيح الغياب إلى حضور. ${attendance?.packageId != null
            ? 'الحصة محسوبة سابقًا من الباقة؛ بدون تحصيل جديد.'
            : (attendance != null && store.hasRetainedSessionPayment(attendance.studentId, attendance.sessionId))
            ? 'دفع الحصة محفوظ؛ تسجيل حضور دون تحصيل جديد.'
            : 'تحصيل ${money(_discount(base))} بطريقة $_method عند تسجيل الحضور.'}',
      _CorrectionAction.paymentMethod =>
        'تغيير وسيلة دفع ${money(payment == null ? 0 : store.paymentCollectedFor(payment.id))} من ${payment?.method ?? '—'} إلى $_method، مع حفظ العملية الأصلية.',
      _CorrectionAction.refundPackage =>
        'رد مبلغ شراء الباقة ${money(payment == null ? 0 : store.paymentCollectedFor(payment.id))} بطريقة $_method. يتاح فقط إذا لم تُستخدم أي حصة من الباقة.',
      _CorrectionAction.reopenClosing =>
        'إعادة فتح تقفيلة الحصة للتصحيح. تبقى التقفيلة الأصلية في السجل؛ يجب تسجيل تقفيلة جديدة بعد التصحيح.',
    };
  }

  Future<void> _apply() async {
    if (_locked ||
        _queryChanged ||
        !store.canCollect ||
        _action == null ||
        !_form.currentState!.validate()) {
      return;
    }
    final action = _action!,
        attendance = _attendance,
        payment = _payment,
        closing = _closing;
    final selectedMonth = _mode == EntryMode.package
        ? _selectedMonthPlan
        : null;
    final reason = _reason.text.trim(),
        method = _method,
        mode = _mode,
        packageSessions = selectedMonth?.sessions ?? 4,
        monthPlanId = selectedMonth?.id,
        originalId = _originalId;
    setState(() {
      _confirming = true;
      _error = null;
      _notice = null;
    });
    try {
      final accepted = await confirmManagement(
        context,
        title: _label(action),
        description:
            '${_student!.name} · كود ${_student!.code}\n${_session == null ? 'الدفعة المحددة' : 'حصة ${_session!.number} · ${sessionDateLabel(_session!)}'}\n\n$_preview\n\nالسبب: $reason\nسيُحفظ التصحيح واسم الموظف وتوقيته في السجل.',
        confirmLabel: 'تأكيد التصحيح',
        destructive:
            action == _CorrectionAction.reverseEntry ||
            action == _CorrectionAction.refundPackage,
      );
      if (!accepted || !mounted) return;
      setState(() {
        _confirming = false;
        _busy = true;
      });
      switch (action) {
        case _CorrectionAction.reverseEntry:
          await store.reverseEntry(
            attendanceId: attendance!.id,
            reason: reason,
            refundMethod: method,
          );
        case _CorrectionAction.changeEntry:
          await store.correctEntry(
            attendanceId: attendance!.id,
            mode: mode,
            reason: reason,
            method: method,
            originalAttendanceId: originalId,
            packageSessions: packageSessions,
            monthPlanId: monthPlanId,
            expectedMonthPlan: selectedMonth,
          );
        case _CorrectionAction.absentPresent:
          await store.markAbsentPresent(
            attendanceId: attendance!.id,
            reason: reason,
            method: method,
          );
        case _CorrectionAction.paymentMethod:
          await store.correctPaymentMethod(
            paymentId: payment!.id,
            method: method,
            reason: reason,
          );
        case _CorrectionAction.refundPackage:
          await store.refundPackage(
            packageId: payment!.packageId!,
            reason: reason,
            refundMethod: method,
          );
        case _CorrectionAction.reopenClosing:
          await store.reopenFinancialClosing(
            closingId: closing!.id,
            reason: reason,
          );
      }
      if (!mounted) return;
      setState(() {
        _action = null;
        _paymentId = null;
        _originalId = null;
        _reason.clear();
        _notice =
            'حُفظ التصحيح محليًا مع السبب واسم الموظف. السجل الأصلي محفوظ.';
      });
      await showManagementMessage(context, _notice!, kind: NoticeKind.success);
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.corrections_page');
      if (mounted) {
        setState(() => _error = managementError(error));
        await showManagementMessage(context, _error!, kind: NoticeKind.error);
      }
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
          _confirming = false;
        });
      }
    }
  }

  Future<void> _cancelRecord({required bool payment}) async {
    if (_locked || !store.canCollect) return;
    final paymentId = payment ? _payment?.id : null;
    final attendanceId = payment ? null : _attendance?.id;
    if (paymentId == null && attendanceId == null) return;
    setState(() => _confirming = true);
    try {
      final changed = await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => RecordCancellationDialog(
          store: store,
          paymentId: paymentId,
          attendanceId: attendanceId,
        ),
      );
      if (mounted && changed == true) {
        setState(_reset);
      }
    } finally {
      if (mounted) setState(() => _confirming = false);
    }
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: store,
    builder: (context, _) {
      if (!store.canCollect) {
        return const EmptySection(
          message:
              'تصحيح الحضور والدفع والاسترداد متاح للإدارة والاستقبال فقط.',
        );
      }
      return WorkspaceDraftRegistration(
        dirty: _dirty,
        busy: _locked,
        child: ManagementPanel(
          title: 'تصحيح الحضور والدفع',
          subtitle: 'راجع العملية الأصلية، ثم اختار التصحيح وسجل سببه.',
          child: ManagementBody(
            header: [
              TextField(
                key: const Key('correction-student-code'),
                controller: _code,
                focusNode: _codeFocus,
                enabled: !_locked,
                textInputAction: TextInputAction.search,
                decoration: const InputDecoration(
                  labelText: 'امسح الكارت أو اكتب كود الطالب واضغط Enter',
                  prefixIcon: Icon(Icons.qr_code_scanner),
                ),
                onSubmitted: _lookup,
                onChanged: (_) => setState(() => _queryChanged = true),
              ),
              const SizedBox(height: 14),
              if (_queryChanged && _student != null)
                const Padding(
                  padding: EdgeInsets.only(bottom: 8),
                  child: Text(
                    'اضغط Enter لعرض الطالب المطلوب؛ بيانات الطالب السابق متوقفة مؤقتًا.',
                  ),
                ),
              if (_busy) const LinearProgressIndicator(minHeight: 3),
            ],
            child: ExcludeFocus(
              excluding: _queryChanged,
              child: IgnorePointer(
                ignoring: _queryChanged,
                child: LayoutBuilder(
                  builder: (context, constraints) {
                    if (constraints.maxWidth < 950) {
                      return MassarScrollView(
                        child: Column(
                          children: [
                            SizedBox(height: 680, child: _editor()),
                            const SizedBox(height: 20),
                            SizedBox(height: 620, child: _history()),
                          ],
                        ),
                      );
                    }
                    return Row(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Expanded(flex: 5, child: _editor()),
                        const SizedBox(width: 20),
                        Expanded(flex: 6, child: _history()),
                      ],
                    );
                  },
                ),
              ),
            ),
          ),
        ),
      );
    },
  );

  Widget _editor() => Container(
    padding: const EdgeInsets.all(20),
    decoration: BoxDecoration(
      color: MassarPalette.of(context).surface,
      border: Border.all(color: MassarPalette.of(context).line),
      borderRadius: BorderRadius.circular(10),
    ),
    child: Form(
      key: _form,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Expanded(
            child: MassarScrollView(
              padding: const EdgeInsets.only(top: 8),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (_student == null)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 24),
                      child: Text(
                        'ابدأ بكود الطالب لعرض الحضور والمدفوعات الأصلية.',
                      ),
                    )
                  else ...[
                    Text(
                      _student!.name,
                      style: const TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    Text('كود ${_student!.code}'),
                    const SizedBox(height: 18),
                    DropdownButtonFormField<String>(
                      key: ValueKey(
                        'correction-session-$_sessionId-$_contextRevision',
                      ),
                      initialValue: _sessionId,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'الحصة المقصودة',
                      ),
                      items: [
                        const DropdownMenuItem(
                          value: null,
                          child: Text('اختار الحصة، أو حدد عملية دفع'),
                        ),
                        ..._sessions.map(
                          (s) => DropdownMenuItem(
                            value: s.id,
                            child: Text(
                              'حصة ${s.number} · ${sessionDateLabel(s)} · ${store.groupLabel(s.groupId)}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ),
                      ],
                      onChanged: _locked
                          ? null
                          : (id) => _changeContext(() {
                              _sessionId = id;
                              _reset();
                            }),
                    ),
                    const SizedBox(height: 14),
                    DropdownButtonFormField<String>(
                      key: ValueKey(
                        'correction-payment-$_paymentId-$_sessionId-$_contextRevision',
                      ),
                      initialValue: _paymentId,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'عملية الدفع المحددة',
                      ),
                      items: [
                        const DropdownMenuItem(
                          value: null,
                          child: Text('تصحيح حضور بدون اختيار دفعة'),
                        ),
                        ..._payments.map(
                          (p) => DropdownMenuItem(
                            value: p.id,
                            child: Text(
                              '${p.description} · ${money(store.paymentCollectedFor(p.id))} · ${p.method} · ${shortDate(p.createdAt)}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        ),
                      ],
                      onChanged: _locked
                          ? null
                          : (id) => _changeContext(() {
                              _paymentId = id;
                              final selected = store.payments
                                  .where((p) => p.id == id)
                                  .firstOrNull;
                              if (selected?.sessionId != null) {
                                _sessionId = selected!.sessionId;
                              }
                              _action = null;
                              _reason.clear();
                              _error = null;
                              _notice = null;
                            }),
                    ),
                    const SizedBox(height: 16),
                    _original(),
                    if (_attendance?.status != AttendanceStatus.absent &&
                            _attendance != null ||
                        _payment != null) ...[
                      const SizedBox(height: 12),
                      Wrap(
                        spacing: 12,
                        runSpacing: 8,
                        children: [
                          if (_attendance != null &&
                              _attendance!.status != AttendanceStatus.absent)
                            OutlinedButton(
                              onPressed: _locked
                                  ? null
                                  : () => _cancelRecord(payment: false),
                              child: const Text('إلغاء الحضور أو دفعه المرتبط'),
                            ),
                          if (_payment != null)
                            OutlinedButton(
                              onPressed: _locked
                                  ? null
                                  : () => _cancelRecord(payment: true),
                              child: const Text('إلغاء الدفع أو حضوره المرتبط'),
                            ),
                        ],
                      ),
                    ],
                    if (_closing != null)
                      Padding(
                        padding: const EdgeInsets.only(top: 14),
                        child: Text(
                          'الحصة مقفلة ماليًا. للإلغاء اختر إعادة فتح التقفيلات المتأثرة داخل نافذة الإلغاء؛ وللتصحيحات الأخرى أعد فتح التقفيلة أولًا.',
                          style: TextStyle(
                            color: Theme.of(context).colorScheme.error,
                          ),
                        ),
                      ),
                    const SizedBox(height: 18),
                    DropdownButtonFormField<_CorrectionAction>(
                      key: ValueKey(
                        'correction-action-$_studentId-$_sessionId-$_paymentId-$_action',
                      ),
                      initialValue: _actions.contains(_action) ? _action : null,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'التصحيح المطلوب',
                      ),
                      items: _actions
                          .map(
                            (a) => DropdownMenuItem(
                              value: a,
                              child: Text(_label(a)),
                            ),
                          )
                          .toList(),
                      onChanged: _locked
                          ? null
                          : (a) => setState(() {
                              _action = a;
                              _mode =
                                  _attendance?.status ==
                                          AttendanceStatus.makeup &&
                                      _attendance?.makeupSourceGroupId == null
                                  ? EntryMode.makeup
                                  : _attendance?.packageId != null
                                  ? EntryMode.package
                                  : EntryMode.single;
                              _monthPlanId = _initialMonthPlanId;
                              _originalId = null;
                              _error = null;
                              _notice = null;
                            }),
                    ),
                    if (_actions.isEmpty)
                      const Padding(
                        padding: EdgeInsets.only(top: 10),
                        child: Text(
                          'اختار حصة لها تسجيل، أو عملية دفع حالية، لعرض التصحيحات المتاحة.',
                        ),
                      ),
                    if (_action != null) ...[
                      const SizedBox(height: 14),
                      if (_action == _CorrectionAction.changeEntry) ...[
                        DropdownButtonFormField<EntryMode>(
                          initialValue: _mode,
                          isExpanded: true,
                          decoration: const InputDecoration(
                            labelText: 'طريقة الدخول الصحيحة',
                          ),
                          items: [
                            DropdownMenuItem(
                              value: EntryMode.single,
                              enabled: _canSwitchToSingle,
                              child: const Text('دفع الحصة'),
                            ),
                            if (_session?.kind == SessionKind.counted ||
                                _attendance?.makeupSourceGroupId != null)
                              const DropdownMenuItem(
                                value: EntryMode.package,
                                child: Text('دخول بالشهر / رصيد سابق'),
                              ),
                            const DropdownMenuItem(
                              value: EntryMode.makeup,
                              child: Text('تعويض'),
                            ),
                          ],
                          onChanged: _locked
                              ? null
                              : (mode) => setState(() {
                                  _mode = mode!;
                                  _originalId = null;
                                }),
                        ),
                        if (_mode == EntryMode.package) ...[
                          const SizedBox(height: 14),
                          DropdownButtonFormField<String>(
                            key: ValueKey(
                              'correction-month-$_sessionId-${_selectedMonthPlan?.id}-${_monthPlans.map((plan) => '${plan.id}:${plan.name}:${plan.sessions}:${plan.price}').join('|')}',
                            ),
                            initialValue: _selectedMonthPlan?.id,
                            isExpanded: true,
                            decoration: const InputDecoration(
                              labelText: 'الشهر عند شراء رصيد جديد',
                            ),
                            items: _monthPlans
                                .map(
                                  (plan) => DropdownMenuItem(
                                    value: plan.id,
                                    child: Text(
                                      '${plan.name} — ${plan.sessions} حصص — ${money(_discount(plan.price))}',
                                      maxLines: 1,
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                )
                                .toList(),
                            validator: (_) => _selectedMonthPlan == null
                                ? 'الشهر المختار لم يعد متاحًا؛ اختر شهرًا من المجموعة.'
                                : null,
                            onChanged: _locked
                                ? null
                                : (id) => setState(() => _monthPlanId = id),
                          ),
                        ],
                        if (!_canSwitchToSingle)
                          const Padding(
                            padding: EdgeInsets.only(top: 8),
                            child: Text(
                              'الباقة السابقة أو المستخدمة لا يمكن تجاوزها بالدفع بالحصة. يمكنك تصحيح الحضور أو وسيلة الدفع.',
                            ),
                          ),
                        const SizedBox(height: 14),
                        if (_mode == EntryMode.makeup)
                          DropdownButtonFormField<String>(
                            key: ValueKey('correction-original-$_originalId'),
                            initialValue: _originalId,
                            isExpanded: true,
                            decoration: const InputDecoration(
                              labelText: 'الغياب المدفوع الذي يعوضه',
                            ),
                            items: store
                                .eligibleMakeups(_studentId!, _sessionId!)
                                .map((a) {
                                  final s = store.sessions.firstWhere(
                                    (s) => s.id == a.sessionId,
                                  );
                                  return DropdownMenuItem(
                                    value: a.id,
                                    child: Text(
                                      'حصة ${s.number} · ${sessionDateLabel(s)}',
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  );
                                })
                                .toList(),
                            validator: (v) => v == null
                                ? 'اختار الغياب الأصلي للتعويض'
                                : null,
                            onChanged: _locked
                                ? null
                                : (id) => setState(() => _originalId = id),
                          ),
                      ],
                      if (_action != _CorrectionAction.reopenClosing)
                        DropdownButtonFormField<String>(
                          key: ValueKey('correction-method-$_action'),
                          initialValue: _method,
                          decoration: InputDecoration(
                            labelText:
                                _action == _CorrectionAction.reverseEntry ||
                                    _action == _CorrectionAction.refundPackage
                                ? 'طريقة رد المبلغ'
                                : 'طريقة الدفع الصحيحة',
                          ),
                          items: ['نقدي', 'إنستاباي', 'تحويل بنكي', 'بطاقة']
                              .map(
                                (m) =>
                                    DropdownMenuItem(value: m, child: Text(m)),
                              )
                              .toList(),
                          onChanged: _locked
                              ? null
                              : (m) => setState(() => _method = m!),
                        ),
                      const SizedBox(height: 14),
                      Container(
                        padding: const EdgeInsets.all(14),
                        color: MassarPalette.of(context).subtle,
                        child: Text(_preview),
                      ),
                      const SizedBox(height: 14),
                      TextFormField(
                        key: const Key('correction-reason'),
                        controller: _reason,
                        enabled: !_locked,
                        minLines: 2,
                        maxLines: 3,
                        decoration: const InputDecoration(
                          labelText: 'سبب التصحيح (إلزامي)',
                        ),
                        validator: (v) => v?.trim().isEmpty ?? true
                            ? 'اكتب سبب التصحيح قبل المتابعة'
                            : null,
                      ),
                    ],
                  ],
                ],
              ),
            ),
          ),
          if (_student != null) ...[
            const Divider(height: 24),
            FilledButton.icon(
              key: const Key('apply-correction'),
              onPressed: _locked || _action == null ? null : _apply,
              icon: const Icon(Icons.fact_check_outlined),
              label: Text(
                _busy ? 'جارٍ حفظ التصحيح…' : 'مراجعة وتأكيد التصحيح',
              ),
            ),
          ],
        ],
      ),
    ),
  );

  Widget _original() {
    final attendance = _attendance;
    final payment =
        _payment ??
        store.payments
            .where(
              (p) => p.studentId == _studentId && p.sessionId == _sessionId,
            )
            .firstOrNull;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'البيانات الأصلية',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
        ),
        const SizedBox(height: 8),
        Text(
          'الحضور: ${attendance == null ? 'لا يوجد تسجيل حالي لهذه الحصة' : attendanceLabel(attendance.status)}',
        ),
        if (attendance?.packageId != null)
          const Text('محسوب من الباقة المدفوعة'),
        if (payment != null) ...[
          Text('الدفع المرتبط: ${payment.description}'),
          Text(
            'السعر الأصلي ${money(payment.baseAmount)} · خصم ${percentText(payment.discountPercent)}%',
          ),
          Text(
            'المبلغ المدفوع: ${money(store.paymentCollectedFor(payment.id))} · ${payment.method}',
          ),
          Text(
            'وقت الدفع: ${shortDate(payment.createdAt)} · ${TimeOfDay.fromDateTime(payment.createdAt).format(context)}',
          ),
          if (payment.packageId != null &&
              !_actions.contains(_CorrectionAction.refundPackage) &&
              _closing == null)
            const Text(
              'الباقة مستخدمة؛ الاسترداد متاح بعد تصحيح التسجيلات المرتبطة، عند إرجاع كامل الرصيد.',
            ),
        ],
      ],
    );
  }

  String _recordLabel(CorrectionAction action) => switch (action) {
    CorrectionAction.entryReversed => 'إلغاء تسجيل حضور',
    CorrectionAction.entryCorrected => 'تغيير طريقة دخول الحصة',
    CorrectionAction.absencePresent => 'تصحيح غياب إلى حضور',
    CorrectionAction.paymentMethod => 'تصحيح وسيلة الدفع',
    CorrectionAction.paymentCanceled => 'إلغاء الدفع',
    CorrectionAction.packageRefund => 'استرداد باقة',
    CorrectionAction.closingReopened => 'إعادة فتح تقفيلة مالية',
  };

  String _correctionDescription(CorrectionRecord record) {
    final student = store.students
        .where((s) => s.id == record.studentId)
        .firstOrNull;
    final session = store.sessions
        .where((s) => s.id == record.sessionId)
        .firstOrNull;
    final payment = store.allPayments
        .where((p) => p.id == record.paymentId)
        .firstOrNull;
    final original = store.allAttendances
        .where((a) => a.id == record.attendanceId)
        .firstOrNull;
    final refunds = store.refunds.where((r) => r.correctionId == record.id);
    return [
      _recordLabel(record.action),
      if (student != null) '${student.name} · كود ${student.code}',
      if (session != null)
        'حصة ${session.number} · ${sessionDateLabel(session)}',
      if (original != null)
        'التسجيل الأصلي: ${attendanceLabel(original.status)}',
      if (payment != null)
        'المبلغ الأصلي: ${money(store.paymentCollectedFor(payment.id))} · خصم ${percentText(payment.discountPercent)}%',
      if (record.oldMethod != null)
        'وسيلة الدفع: ${record.oldMethod} ← ${record.newMethod ?? '—'}',
      ...refunds.map((r) => 'مبلغ مسترد: ${money(r.amount)} · ${r.method}'),
      'السبب: ${record.reason}',
    ].join('\n');
  }

  Widget _history() {
    final original =
        <({DateTime at, String label, String amount, String state})>[
          ...store.allPayments
              .where((p) => p.studentId == _studentId)
              .map(
                (p) => (
                  at: p.createdAt,
                  label: '${p.description}\n${p.method}',
                  amount: money(store.paymentCollectedFor(p.id)),
                  state: store.payments.any((active) => active.id == p.id)
                      ? 'حالية'
                      : store.refunds.any((r) => r.paymentId == p.id)
                      ? 'مستردة'
                      : 'مصَحّحة',
                ),
              ),
          ...store.allAttendances.where((a) => a.studentId == _studentId).map((
            a,
          ) {
            final session = store.sessions
                .where((s) => s.id == a.sessionId)
                .firstOrNull;
            return (
              at: a.recordedAt,
              label:
                  'حصة ${session?.number ?? '—'} · ${attendanceLabel(a.status)}',
              amount: a.packageId != null ? 'من الباقة' : '—',
              state: store.attendances.any((current) => current.id == a.id)
                  ? 'حالي'
                  : 'مصَحّح',
            );
          }),
        ]..sort((a, b) => b.at.compareTo(a.at));
    final audit = store.corrections.toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          _student == null
              ? 'السجل الأصلي والتصحيحات'
              : 'سجل ${_student!.name}',
          style: const TextStyle(fontSize: 21, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 12),
        Expanded(
          flex: 5,
          child: original.isEmpty
              ? const EmptySection(
                  message:
                      'ستظهر هنا سجلات الحضور والدفع الأصلية، حتى بعد التصحيح أو الاسترداد.',
                )
              : ManagementTable(
                  columns: const [
                    'السجل الأصلي',
                    'المبلغ',
                    'الحالة',
                    'التاريخ',
                  ],
                  rows: original
                      .map(
                        (item) => DataRow(
                          cells: [
                            DataCell(
                              SizedBox(
                                width: 135,
                                child: Text(
                                  item.label,
                                  maxLines: 2,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                            ),
                            DataCell(Text(item.amount)),
                            DataCell(Text(item.state)),
                            DataCell(Text(shortDate(item.at))),
                          ],
                        ),
                      )
                      .toList(),
                ),
        ),
        const SizedBox(height: 20),
        const Text(
          'سجل التصحيحات · جميع الطلبة',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 12),
        Expanded(
          flex: 4,
          child: audit.isEmpty
              ? const EmptySection(
                  message: 'كل تصحيح يُحفظ هنا مع السبب واسم الموظف.',
                )
              : ManagementTable(
                  columns: const [
                    'التصحيح والسبب',
                    'الموظف',
                    'التاريخ',
                    'التفاصيل',
                  ],
                  rows: audit
                      .map(
                        (a) => DataRow(
                          cells: [
                            DataCell(
                              SizedBox(
                                width: 180,
                                child: Text(
                                  '${_recordLabel(a.action)}\n${a.reason}',
                                  maxLines: 3,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                            ),
                            DataCell(
                              Text(
                                store.staff
                                        .where((s) => s.id == a.staffId)
                                        .firstOrNull
                                        ?.name ??
                                    '—',
                              ),
                            ),
                            DataCell(Text(shortDate(a.createdAt))),
                            DataCell(
                              TextButton(
                                onPressed: () => showDialog<void>(
                                  context: context,
                                  builder: (context) => ScrollableMassarDialog(
                                    title: const Text('تفاصيل التصحيح'),
                                    content: SelectableText(
                                      '${_correctionDescription(a)}\n\n${shortDate(a.createdAt)} · ${TimeOfDay.fromDateTime(a.createdAt).format(context)}\n${store.staff.where((s) => s.id == a.staffId).firstOrNull?.name ?? '—'}',
                                    ),
                                    actions: [
                                      TextButton(
                                        onPressed: () => Navigator.pop(context),
                                        child: const Text('إغلاق'),
                                      ),
                                    ],
                                  ),
                                ),
                                child: const Text('عرض'),
                              ),
                            ),
                          ],
                        ),
                      )
                      .toList(),
                ),
        ),
      ],
    );
  }
}
