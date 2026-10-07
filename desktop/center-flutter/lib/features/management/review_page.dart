import 'dart:async';
import 'package:massar_center/shared/student_lookup_dialog.dart';
import 'package:massar_center/domain/student_lookup.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/workspace_draft_guard.dart';
import 'package:massar_center/shared/notice_dialog.dart'
    show hasPendingMassarNotice;
import 'management_widgets.dart';
import '../attendance/record_cancellation_dialog.dart';

enum PaymentReviewCategory {
  all,
  single,
  package,
  free,
  makeup,
  unpaid,
  centerOnly,
}

PaymentRecord? paymentForAttendanceReview(
  CenterStore store,
  AttendanceRecord? entry,
  PaymentStatusResult status,
) {
  var packageId = status.packageId ?? entry?.packageId;
  if (packageId == null && entry?.originalAttendanceId != null) {
    packageId = store.allAttendances
        .where((old) => old.id == entry!.originalAttendanceId)
        .firstOrNull
        ?.packageId;
  }
  final package = store.packages.where((p) => p.id == packageId).firstOrNull;
  final paymentId = status.paymentId ?? package?.paymentId;
  return store.payments.where((p) => p.id == paymentId).firstOrNull;
}

bool paymentReviewIsExempt(
  CenterStore store,
  AttendanceRecord entry,
  PaymentStatusResult status,
) =>
    entry.centerFeeOnly ||
    status.status == StudentPaymentStatus.free ||
    entry.fixedDiscountPercent == 100 &&
        status.status != StudentPaymentStatus.notPaid ||
    paymentForAttendanceReview(store, entry, status)?.netAmount == 0;

bool paymentReviewIsCurrent(
  CenterStore store,
  AttendanceRecord entry,
  PaymentStatusResult status,
) =>
    entry.status != AttendanceStatus.absent &&
    store.isPaymentCheckCurrent(entry.studentId, entry.sessionId);

class ReviewPage extends StatefulWidget {
  const ReviewPage({
    super.key,
    required this.store,
    this.sessionId,
    this.initialCategory = PaymentReviewCategory.all,
  });
  final CenterStore store;
  final String? sessionId;
  final PaymentReviewCategory initialCategory;
  @override
  State<ReviewPage> createState() => _ReviewPageState();
}

class _ReviewPageState extends State<ReviewPage> {
  final _code = TextEditingController();
  final _customAmount = TextEditingController();
  final _focus = FocusNode();
  String? _groupId, _sessionId, _studentId, _error;
  bool _busy = false, _advanced = false;
  PaymentReviewCategory _category = PaymentReviewCategory.all;
  int? _expectedAmount;
  String? _customAmountError;
  bool _unreviewedOnly = false;
  Timer? _searchRefresh;
  // Display-only memoization. Store commands still validate live financial data.
  // Invalidate on every store notification, including remote snapshot updates.
  final _displayValues = <(String, String, String), Object?>{};

  T _display<T>(
    String kind,
    String studentId,
    String sessionId,
    T Function() read,
  ) {
    final key = (kind, studentId, sessionId);
    if (!_displayValues.containsKey(key)) _displayValues[key] = read();
    return _displayValues[key] as T;
  }

  void _invalidateDisplay() => _displayValues.clear();

  PaymentStatusResult _displayStatus(String studentId, String sessionId) =>
      _display(
        'status',
        studentId,
        sessionId,
        () => store.paymentStatusFor(studentId, sessionId),
      );

  int? _displayAmount(String studentId, String sessionId) => _display(
    'amount',
    studentId,
    sessionId,
    () => store.paymentReviewAmountFor(studentId, sessionId),
  );

  void _searchChanged(String value) {
    _studentId = null;
    _error = null;
    _searchRefresh?.cancel();
    _searchRefresh = Timer(const Duration(milliseconds: 120), () {
      if (mounted) setState(() {});
    });
  }

  CenterStore get store => widget.store;
  bool get _locked => widget.sessionId != null;
  LessonSession? get _session => store.sessions
      .where(
        (session) =>
            session.id == _sessionId &&
            session.status != SessionStatus.canceled,
      )
      .firstOrNull;
  Student? get _student =>
      store.students.where((s) => s.id == _studentId).firstOrNull;
  List<AttendanceRecord> get _attendees => store.attendances
      .where(
        (entry) =>
            entry.sessionId == _sessionId &&
            entry.status != AttendanceStatus.absent,
      )
      .toList();
  List<LessonSession> get _sessions =>
      store.sessions
          .where(
            (s) => s.groupId == _groupId && s.status != SessionStatus.canceled,
          )
          .toList()
        ..sort((a, b) => a.startsAt.compareTo(b.startsAt));

  @override
  void initState() {
    super.initState();
    store.addListener(_invalidateDisplay);
    _initializeContext();
  }

  void _initializeContext() {
    _advanced = false;
    _category = widget.initialCategory;
    _expectedAmount = null;
    _unreviewedOnly = false;
    _customAmount.clear();
    _customAmountError = null;
    if (_locked) {
      _sessionId = widget.sessionId;
      _groupId = _session?.groupId;
      _studentId = null;
      _error = null;
      _code.clear();
    } else {
      _groupId = store.groups.firstOrNull?.id;
      _selectSession();
    }
  }

  @override
  void didUpdateWidget(covariant ReviewPage oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.store != widget.store) {
      oldWidget.store.removeListener(_invalidateDisplay);
      store.addListener(_invalidateDisplay);
    }
    _invalidateDisplay();
    if (oldWidget.sessionId != widget.sessionId ||
        oldWidget.store != widget.store) {
      _initializeContext();
      _focusCode();
    }
  }

  void _selectSession() {
    final options = _sessions;
    final started = options
        .where((s) => !s.startsAt.isAfter(DateTime.now()))
        .toList();
    _sessionId = started.lastOrNull?.id ?? options.firstOrNull?.id;
    _expectedAmount = null;
    _unreviewedOnly = false;
    _customAmount.clear();
    _customAmountError = null;
    _studentId = null;
    _error = null;
    _code.clear();
  }

  void _focusCode({bool selectAll = false}) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted ||
          _advanced ||
          _session == null ||
          ModalRoute.of(context)?.isCurrent != true) {
        return;
      }
      if (selectAll) {
        _code.selection = TextSelection(
          baseOffset: 0,
          extentOffset: _code.text.length,
        );
      }
      _focus.requestFocus();
    });
  }

  @override
  void dispose() {
    _searchRefresh?.cancel();
    store.removeListener(_invalidateDisplay);
    _code.dispose();
    _customAmount.dispose();
    _focus.dispose();
    super.dispose();
  }

  Future<void> _scan(String raw) async {
    _searchRefresh?.cancel();
    if (_busy || !store.canCollect) return;
    final current = _session;
    if (current == null) {
      setState(() {
        _studentId = null;
        _error = _locked
            ? 'الحصة غير متاحة أو ملغاة؛ لا يمكن مراجعة أكوادها.'
            : 'اختار الحصة أولًا.';
      });
      await showManagementMessage(context, _error!, kind: NoticeKind.warning);
      return;
    }
    final id = current.id;
    final expectedAmount = _expectedAmount;
    final identifiers = studentsWithIdentifier(store.students, raw);
    final matches = identifiers.isNotEmpty
        ? identifiers
        : raw.trim().isEmpty
        ? <Student>[]
        : store.students
              .where((student) => _matchesSearch(student, raw))
              .toList();
    setState(() {
      _busy = true;
      _error = null;
      _studentId = null;
    });
    try {
      if (matches.isEmpty) {
        setState(() {
          _error = 'لم نجد طالبًا بهذا الكود أو الاسم أو الهاتف.';
        });
        await showManagementMessage(context, _error!, kind: NoticeKind.warning);
        return;
      }
      final student = matches.length == 1
          ? matches.single
          : await chooseMatchingStudent(context, matches);
      if (!mounted ||
          student == null ||
          _sessionId != id ||
          _session?.status == SessionStatus.canceled ||
          !store.canCollect) {
        return;
      }
      final status = store.paymentStatusFor(student.id, id);
      final attendance = _attendees
          .where((entry) => entry.studentId == student.id)
          .firstOrNull;
      final inCategory =
          attendance != null && _matchesCategory(attendance, status);
      final amount = store.paymentReviewAmountFor(student.id, id);
      if (!mounted || _sessionId != id) return;
      setState(() => _studentId = student.id);
      final existing = store.paymentChecks.any(
        (check) => check.studentId == student.id && check.sessionId == id,
      );
      if (existing) {
        final message = store.isPaymentCheckCurrent(student.id, id)
            ? 'الطالب تمت مراجعة إيصال هذه الحصة بالفعل؛ لم تتكرر المراجعة. مراجعة الإيصال لا تعني سداد أي مديونية متبقية.'
            : 'توجد علامة مراجعة سابقة لهذا الطالب وتغيّر حسابه. أزل العلامة ثم راجع الإيصال من جديد؛ لم تُستبدل العلامة تلقائيًا.';
        setState(() => _error = message);
        await showManagementMessage(
          context,
          '${student.name} · ${student.code}\n$message',
          kind: NoticeKind.warning,
        );
        return;
      }
      if (expectedAmount == null) {
        const message =
            'اختر مبلغ الإيصال أو اكتبه بالجنيه قبل المراجعة. لم تُحفظ علامة مراجعة.';
        setState(() => _error = message);
        await showManagementMessage(context, message, kind: NoticeKind.warning);
        return;
      }
      if (amount != expectedAmount) {
        final message = amount == null
            ? 'المبلغ المختار ${money(expectedAmount)}، لكن لا يوجد تحصيل إيصال لهذا الطالب في هذه الحصة. لم تُحفظ علامة مراجعة.'
            : 'المبلغ مختلف: اخترت ${money(expectedAmount)} وإيصال الطالب بهذه الحصة ${money(amount)}. لم تُحفظ علامة مراجعة.';
        setState(() => _error = message);
        await showManagementMessage(
          context,
          '${student.name} · ${student.code}\n$message',
          kind: NoticeKind.warning,
        );
        return;
      }
      final reviewable = inCategory && _canReview(attendance, status);
      if (reviewable) {
        await store.checkPayment(
          studentId: student.id,
          sessionId: id,
          expectedAmount: expectedAmount,
        );
      }
      if (!mounted || _sessionId != id) return;
      final message = attendance == null
          ? 'الطالب ليس ضمن الحضور الفعلي لهذه الحصة؛ لم تُحفظ مراجعة.'
          : !inCategory
          ? 'الطالب خارج الفئة المحددة؛ لم تُحفظ مراجعة.'
          : !reviewable
          ? 'لا يوجد مبلغ محصّل بإيصال لهذه الحصة؛ لم تُحفظ علامة مراجعة. التغطية والحضور ظاهران في السجل.'
          : null;
      if (message != null) {
        setState(() => _error = message);
        await showManagementMessage(
          context,
          '${student.name} · كود ${student.code}\n${_coverageLabel(attendance, status)}\n${status.detail}\n$message',
          kind: NoticeKind.warning,
        );
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.review_page');
      if (mounted) {
        setState(() => _error = managementError(error));
        await showManagementMessage(context, _error!, kind: NoticeKind.error);
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        _focusCode(selectAll: true);
      }
    }
  }

  Widget _reviewedTable(
    List<AttendanceRecord> attendees,
    Map<String, Student> students,
    Map<String, PaymentStatusResult> statuses,
  ) {
    final entries = {for (final entry in attendees) entry.studentId: entry};
    final checks = store.paymentChecks.where((check) {
      final entry = entries[check.studentId];
      return check.sessionId == _sessionId &&
          entry != null &&
          _reviewed(entry, statuses[entry.id]!);
    }).toList()..sort((a, b) => b.checkedAt.compareTo(a.checkedAt));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'اللي راجعتهم · ${checks.length}',
          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
        ),
        const Text(
          'آخر مراجعة فوق؛ القائمة تفضل ظاهرة أثناء البحث عن الطالب التالي.',
        ),
        const SizedBox(height: 8),
        if (checks.isEmpty)
          const Padding(
            padding: EdgeInsets.all(12),
            child: Text('لسه مفيش إيصالات تمت مراجعتها في الحصة دي.'),
          )
        else
          SizedBox(
            height: 250,
            child: ManagementTable(
              key: const Key('reviewed-students-table'),
              columns: const [
                'الطالب والكود',
                'نوع الدفع',
                'المبلغ المراجع',
                'وقت المراجعة',
                'الإجراء',
              ],
              rows: checks.map((check) {
                final entry = entries[check.studentId]!;
                final student = students[check.studentId];
                final time = check.checkedAt.toLocal();
                return DataRow(
                  key: ValueKey('reviewed-student-${check.studentId}'),
                  cells: [
                    DataCell(
                      Text(
                        '${student?.name ?? '—'}\nكود ${student?.code ?? '—'}',
                      ),
                    ),
                    DataCell(Text(_coverageLabel(entry, statuses[entry.id]!))),
                    DataCell(
                      Text(
                        money(
                          _displayAmount(check.studentId, check.sessionId)!,
                        ),
                      ),
                    ),
                    DataCell(
                      Text(
                        '${time.hour.toString().padLeft(2, '0')}:${time.minute.toString().padLeft(2, '0')}:${time.second.toString().padLeft(2, '0')}',
                      ),
                    ),
                    DataCell(
                      TextButton.icon(
                        key: ValueKey('undo-reviewed-${check.studentId}'),
                        onPressed: _busy ? null : () => _uncheck(entry),
                        icon: const Icon(Icons.undo),
                        label: const Text('إزالة المراجعة'),
                      ),
                    ),
                  ],
                );
              }).toList(),
            ),
          ),
        const SizedBox(height: 16),
      ],
    );
  }

  Widget _panel({
    required String title,
    required String subtitle,
    required List<Widget> actions,
    required Widget child,
  }) {
    if (!_locked) {
      return ManagementPanel(
        title: title,
        subtitle: subtitle,
        actions: actions,
        child: child,
      );
    }
    final body = child is ManagementBody ? child : null;
    return Padding(
      padding: const EdgeInsets.all(16),
      child: ManagementBody(
        header: [
          Text(
            title,
            style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          if (actions.isNotEmpty) ...[
            const SizedBox(height: 8),
            Wrap(spacing: 8, runSpacing: 8, children: actions),
          ],
          const SizedBox(height: 12),
          if (body != null) ...body.header,
        ],
        child: body?.child ?? child,
      ),
    );
  }

  String _label(StudentPaymentStatus status) => switch (status) {
    StudentPaymentStatus.paidSingle => 'دفع بالحصة',
    StudentPaymentStatus.paidPackage => 'الحصة مغطاة بالباقة',
    StudentPaymentStatus.free => 'حصة مجانية',
    StudentPaymentStatus.makeup => 'تعويض',
    StudentPaymentStatus.notPaid => 'غير دافع',
  };

  String _categoryLabel(PaymentReviewCategory category) => switch (category) {
    PaymentReviewCategory.all => 'كل الحاضرين',
    PaymentReviewCategory.single => 'دفع بالحصة',
    PaymentReviewCategory.package => 'شهر / رصيد باقة',
    PaymentReviewCategory.free => 'مجاني / إعفاء من رسوم المدرس',
    PaymentReviewCategory.makeup => 'تعويض',
    PaymentReviewCategory.unpaid => 'مبالغ غير مسددة',
    PaymentReviewCategory.centerOnly => 'سنتر فقط',
  };

  bool _matchesSearch(Student student, String raw) {
    if (studentMatchesSearch(student, raw)) return true;
    final normalized = normalizeStudentIdentifier(raw);
    if (!RegExp(r'^[0-9+()\s-]+$').hasMatch(normalized)) return false;
    final digits = normalized.replaceAll(RegExp(r'[^0-9]'), '');
    return digits.length >= 3 &&
        [student.phone, student.guardianPhone].any(
          (phone) => normalizeStudentIdentifier(
            phone,
          ).replaceAll(RegExp(r'[^0-9]'), '').contains(digits),
        );
  }

  PaymentRecord? _paymentFor(
    AttendanceRecord? entry,
    PaymentStatusResult status,
  ) {
    return entry == null
        ? paymentForAttendanceReview(store, entry, status)
        : _display(
            'payment',
            entry.studentId,
            entry.sessionId,
            () => paymentForAttendanceReview(store, entry, status),
          );
  }

  int _centerRemaining(AttendanceRecord entry) {
    if (!entry.centerFeeOnly) return 0;
    return _display(
      'centerRemaining',
      entry.studentId,
      entry.sessionId,
      () => store.centerFeeRemainingFor(entry.studentId, entry.sessionId),
    );
  }

  bool _canReview(AttendanceRecord entry, PaymentStatusResult status) =>
      (_displayAmount(entry.studentId, entry.sessionId) ?? 0) > 0;

  bool _hasUnpaidBalance(AttendanceRecord entry, PaymentStatusResult status) =>
      status.status == StudentPaymentStatus.notPaid ||
      status.debtAmount > 0 ||
      _centerRemaining(entry) > 0;

  bool _hasReviewMark(AttendanceRecord entry) => store.paymentChecks.any(
    (check) =>
        check.studentId == entry.studentId &&
        check.sessionId == entry.sessionId,
  );

  bool _isFree(AttendanceRecord entry, PaymentStatusResult status) => _display(
    'exempt',
    entry.studentId,
    entry.sessionId,
    () => paymentReviewIsExempt(store, entry, status),
  );

  bool _matchesCategory(AttendanceRecord entry, PaymentStatusResult status) =>
      switch (_category) {
        PaymentReviewCategory.all => true,
        PaymentReviewCategory.single =>
          status.status == StudentPaymentStatus.paidSingle &&
              !_isFree(entry, status),
        PaymentReviewCategory.package =>
          status.status == StudentPaymentStatus.paidPackage,
        PaymentReviewCategory.free => _isFree(entry, status),
        PaymentReviewCategory.makeup => entry.status == AttendanceStatus.makeup,
        PaymentReviewCategory.unpaid => _hasUnpaidBalance(entry, status),
        PaymentReviewCategory.centerOnly => entry.centerFeeOnly,
      };

  bool _reviewed(AttendanceRecord entry, PaymentStatusResult status) =>
      _canReview(entry, status) &&
      _display(
        'current',
        entry.studentId,
        entry.sessionId,
        () => paymentReviewIsCurrent(store, entry, status),
      );

  String _coverageLabel(AttendanceRecord? entry, PaymentStatusResult status) =>
      entry == null
      ? _readCoverageLabel(entry, status)
      : _display(
          'coverage',
          entry.studentId,
          entry.sessionId,
          () => _readCoverageLabel(entry, status),
        );

  String _readCoverageLabel(
    AttendanceRecord? entry,
    PaymentStatusResult status,
  ) {
    final payment = _paymentFor(entry, status);
    if (entry?.centerFeeOnly == true) {
      return 'سنتر فقط — رسوم المدرس ${money(0)}';
    }
    if (entry != null && _isFree(entry, status)) {
      return 'مجاني / إعفاء — ${money(0)}';
    }
    if (status.status == StudentPaymentStatus.paidPackage ||
        status.status == StudentPaymentStatus.makeup) {
      final package = store.packages
          .where((package) => package.id == payment?.packageId)
          .firstOrNull;
      final name = package?.monthPlanName ?? 'شهر / باقة';
      return '${entry?.status == AttendanceStatus.makeup ? 'تعويض — ' : ''}'
          '$name${payment == null ? '' : ' — محصّل ${money(store.paymentCollectedFor(payment.id))}'}'
          '${package == null ? '' : ' · رصيد ${package.remaining}/${package.totalSessions}'}';
    }
    return '${entry?.status == AttendanceStatus.makeup ? 'تعويض — ' : ''}'
        '${_label(status.status)}${payment == null ? '' : ' — محصّل ${money(store.paymentCollectedFor(payment.id))}'}';
  }

  String _amountDetail(AttendanceRecord entry, PaymentStatusResult status) =>
      _display(
        'amountDetail',
        entry.studentId,
        entry.sessionId,
        () => _readAmountDetail(entry, status),
      );

  String _readAmountDetail(AttendanceRecord entry, PaymentStatusResult status) {
    final receiptAmount = _displayAmount(entry.studentId, entry.sessionId);
    final receiptLabel = receiptAmount == null
        ? 'لا تحصيل في هذه الحصة'
        : 'محصّل هذه الحصة ${money(receiptAmount)}';
    if (entry.centerFeeOnly) {
      final paid = store.centerFeeCollectedFor(
        entry.studentId,
        entry.sessionId,
      );
      return 'سنتر: محصّل هذه الحصة ${money(paid)} · باقي ${money(_centerRemaining(entry))}';
    }
    final payment = _paymentFor(entry, status);
    return payment == null
        ? '$receiptLabel · ${status.detail}'
        : '$receiptLabel · إجمالي تحصيل الدفعة ${money(store.paymentCollectedFor(payment.id))}'
              ' · مديونية ${money(store.paymentDebtFor(payment.id))}'
              '${payment.sessionId != entry.sessionId ? ' · دفعة شهر سابقة' : ''}';
  }

  Future<void> _cancel(
    AttendanceRecord entry,
    PaymentStatusResult status, {
    bool paymentOnly = false,
  }) async {
    if (_busy || !store.canCollect) return;
    final payment = paymentOnly ? _paymentFor(entry, status) : null;
    if (paymentOnly && payment == null) return;
    setState(() => _busy = true);
    try {
      final changed = await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => RecordCancellationDialog(
          store: store,
          paymentId: payment?.id,
          attendanceId: paymentOnly ? null : entry.id,
        ),
      );
      if (mounted && changed == true) {
        setState(() {
          _studentId = null;
          _error = null;
        });
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.review_page');
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        _focusCode(selectAll: true);
      }
    }
  }

  Future<void> _uncheck(AttendanceRecord entry) => _changeReviewMarks(
    title: 'إزالة علامة مراجعة الطالب؟',
    description:
        '${store.students.where((student) => student.id == entry.studentId).firstOrNull?.name ?? "الطالب"}\n${sessionLabel(store.sessions.firstWhere((session) => session.id == entry.sessionId))}\nستُزال علامة المراجعة فقط. الحضور والإيصالات والمدفوعات محفوظة.',
    action: () => store.uncheckPayment(
      studentId: entry.studentId,
      sessionId: entry.sessionId,
    ),
  );

  Future<void> _clearChecks() async {
    final current = _session;
    if (current == null) return;
    await _changeReviewMarks(
      title: 'مسح مراجعات هذه الحصة؟',
      description:
          '${sessionLabel(current)} · ${store.groupLabel(current.groupId)}\nستُمسح جميع علامات مراجعة الحصة، بما فيها غير الظاهرة بسبب الفلاتر. لن تُحذف أي دفعة أو حضور.',
      action: () => store.clearPaymentChecks(sessionId: current.id),
    );
  }

  Future<void> _changeReviewMarks({
    required String title,
    required String description,
    required Future<void> Function() action,
  }) async {
    if (_busy || !store.canCollect) return;
    setState(() => _busy = true);
    try {
      final confirmed = await confirmManagement(
        context,
        title: title,
        description: description,
        confirmLabel: 'إزالة علامات المراجعة',
        destructive: true,
      );
      if (!mounted || !confirmed) return;
      await action();
      if (mounted) {
        setState(() {
          _studentId = null;
          _error = null;
        });
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.review_page');
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        _focusCode(selectAll: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_advanced) {
      return _PaperReviewPage(
        store: store,
        onBack: () {
          setState(() => _advanced = false);
          _focusCode(selectAll: true);
        },
      );
    }
    return WorkspaceDraftRegistration(
      dirty: false,
      busy: _busy,
      child: AnimatedBuilder(
        animation: store,
        builder: (context, _) {
          if (!store.canCollect) {
            return const EmptySection(
              message: 'مراجعة الدفع متاحة للإدارة والاستقبال فقط.',
            );
          }
          final current = _session;
          if (_locked && current == null) {
            return const EmptySection(
              key: Key('session-review-unavailable'),
              message: 'الحصة غير متاحة أو ملغاة؛ لا يمكن مراجعة أكوادها.',
            );
          }
          final attendees = _attendees;
          final studentsById = {
            for (final student in store.students) student.id: student,
          };
          final statuses = {
            for (final entry in attendees)
              entry.id: _displayStatus(entry.studentId, entry.sessionId),
          };
          final visible = attendees.where((entry) {
            final student = studentsById[entry.studentId];
            return student != null &&
                _matchesCategory(entry, statuses[entry.id]!) &&
                (_expectedAmount == null ||
                    _displayAmount(entry.studentId, entry.sessionId) ==
                        _expectedAmount) &&
                (!_unreviewedOnly ||
                    _canReview(entry, statuses[entry.id]!) &&
                        !_reviewed(entry, statuses[entry.id]!)) &&
                _matchesSearch(student, _code.text);
          }).toList();
          final selected = _student;
          final selectedAttendance = attendees
              .where((entry) => entry.studentId == selected?.id)
              .firstOrNull;
          final result = selected == null || _sessionId == null
              ? null
              : _displayStatus(selected.id, _sessionId!);
          final selectedSettled =
              result != null &&
              selectedAttendance != null &&
              _canReview(selectedAttendance, result);
          final reviewed = attendees
              .where((entry) => _reviewed(entry, statuses[entry.id]!))
              .length;
          final missing = attendees
              .where((entry) => _hasUnpaidBalance(entry, statuses[entry.id]!))
              .length;
          final receiptAmounts = <int>{
            6000,
            21000,
            ...attendees
                .map(
                  (entry) => _displayAmount(entry.studentId, entry.sessionId),
                )
                .whereType<int>()
                .where((amount) => amount > 0),
          };
          if (_expectedAmount != null) receiptAmounts.add(_expectedAmount!);
          final sortedAmounts = receiptAmounts.toList()..sort();
          return Focus(
            onFocusChange: (focused) {
              if (focused) _focusCode();
            },
            child: _panel(
              title: 'مراجعة دفع الحاضرين',
              subtitle: _locked
                  ? 'راجع إيصال الحصة بالكود أو الاسم أو الهاتف، واختر المبلغ قبل المسح.'
                  : 'اختار الحصة ومبلغ الإيصال ثم ابحث بالكود أو الاسم أو الهاتف.',
              actions: [
                if (current != null)
                  OutlinedButton.icon(
                    key: const Key('clear-session-payment-checks'),
                    onPressed:
                        _busy ||
                            !store.paymentChecks.any(
                              (check) => check.sessionId == current.id,
                            )
                        ? null
                        : _clearChecks,
                    icon: const Icon(Icons.playlist_remove),
                    label: const Text('مسح مراجعات الحصة'),
                  ),
                if (!_locked)
                  OutlinedButton.icon(
                    onPressed: _busy
                        ? null
                        : () => setState(() => _advanced = true),
                    icon: const Icon(Icons.compare_arrows),
                    label: const Text('مقارنة مبلغ الورق'),
                  ),
              ],
              child: ManagementBody(
                header: [
                  if (_locked)
                    Container(
                      key: const Key('session-review-context'),
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: MassarPalette.of(context).subtle,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                          color: MassarPalette.of(context).line,
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(
                            '${sessionLabel(current!)} · ${sessionDateLabel(current)} · ${sessionKindLabel(current.kind)}',
                            style: const TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                          Text(store.groupLabel(current.groupId)),
                        ],
                      ),
                    )
                  else
                    Wrap(
                      spacing: 12,
                      runSpacing: 12,
                      children: [
                        SizedBox(
                          width: 420,
                          child: DropdownButtonFormField<String>(
                            key: ValueKey('code-check-group-$_groupId'),
                            initialValue: _groupId,
                            isExpanded: true,
                            decoration: const InputDecoration(
                              labelText: 'المجموعة',
                            ),
                            items: store.groups
                                .map(
                                  (g) => DropdownMenuItem(
                                    value: g.id,
                                    child: Text(
                                      store.groupLabel(g.id),
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                )
                                .toList(),
                            onChanged: _busy
                                ? null
                                : (id) {
                                    _focusCode();
                                    setState(() {
                                      _groupId = id;
                                      _selectSession();
                                    });
                                  },
                          ),
                        ),
                        SizedBox(
                          width: 350,
                          child: DropdownButtonFormField<String>(
                            key: ValueKey('code-check-session-$_sessionId'),
                            initialValue: _sessionId,
                            isExpanded: true,
                            decoration: const InputDecoration(
                              labelText: 'الحصة',
                            ),
                            items: _sessions
                                .map(
                                  (s) => DropdownMenuItem(
                                    value: s.id,
                                    child: Text(
                                      '${sessionLabel(s)} · ${sessionDateLabel(s)}',
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                  ),
                                )
                                .toList(),
                            onChanged: _busy
                                ? null
                                : (id) {
                                    _focusCode();
                                    setState(() {
                                      _sessionId = id;
                                      _expectedAmount = null;
                                      _unreviewedOnly = false;
                                      _customAmount.clear();
                                      _customAmountError = null;
                                      _studentId = null;
                                      _error = null;
                                      _code.clear();
                                    });
                                  },
                          ),
                        ),
                      ],
                    ),
                  SizedBox(height: _locked ? 12 : 20),
                  SizedBox(
                    width: 420,
                    child: DropdownButtonFormField<PaymentReviewCategory>(
                      key: ValueKey(
                        'payment-review-category-${_category.name}',
                      ),
                      initialValue: _category,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'فئة الحاضرين للمراجعة',
                      ),
                      items: PaymentReviewCategory.values
                          .map(
                            (category) => DropdownMenuItem(
                              value: category,
                              child: Text(_categoryLabel(category)),
                            ),
                          )
                          .toList(),
                      onChanged: _busy
                          ? null
                          : (category) => setState(() {
                              _category = category!;
                              _code.clear();
                              _studentId = null;
                            }),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 12,
                    runSpacing: 8,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      SizedBox(
                        width: 300,
                        child: DropdownButtonFormField<int>(
                          key: ValueKey(
                            'review-receipt-amount-$_expectedAmount',
                          ),
                          initialValue: _expectedAmount,
                          isExpanded: true,
                          decoration: const InputDecoration(
                            labelText: 'مبلغ الإيصال للمراجعة',
                            isDense: true,
                          ),
                          items: [
                            const DropdownMenuItem<int>(
                              value: null,
                              child: Text('اختر مبلغ الإيصال أولًا'),
                            ),
                            ...sortedAmounts.map(
                              (amount) => DropdownMenuItem(
                                value: amount,
                                child: Text(money(amount)),
                              ),
                            ),
                          ],
                          onChanged: _busy
                              ? null
                              : (amount) {
                                  setState(() {
                                    _expectedAmount = amount;
                                    _customAmount.clear();
                                    _customAmountError = null;
                                    _studentId = null;
                                    _error = null;
                                  });
                                  _focusCode(selectAll: true);
                                },
                        ),
                      ),
                      SizedBox(
                        width: 220,
                        child: TextField(
                          key: const Key('payment-review-custom-amount'),
                          controller: _customAmount,
                          readOnly: _busy,
                          enabled: current != null,
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          decoration: InputDecoration(
                            labelText: 'أو مبلغ آخر بالجنيه',
                            isDense: true,
                            errorText: _customAmountError,
                          ),
                          onChanged: (text) {
                            final amount = piastresFromText(text);
                            setState(() {
                              _expectedAmount = amount != null && amount > 0
                                  ? amount
                                  : null;
                              _customAmountError = text.trim().isEmpty
                                  ? null
                                  : _expectedAmount == null
                                  ? 'اكتب مبلغًا أكبر من صفر، حتى رقمين بعد الفاصلة'
                                  : null;
                              _studentId = null;
                              _error = null;
                            });
                          },
                          onSubmitted: (_) {
                            if (_expectedAmount != null) {
                              _focusCode(selectAll: true);
                            }
                          },
                        ),
                      ),
                      FilterChip(
                        key: const Key('paid-unreviewed-filter'),
                        label: const Text('مدفوعون لم تتم مراجعتهم'),
                        selected: _unreviewedOnly,
                        onSelected: _busy
                            ? null
                            : (selected) {
                                setState(() {
                                  _unreviewedOnly = selected;
                                  _code.clear();
                                  _studentId = null;
                                  _error = null;
                                });
                                _focusCode();
                              },
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Container(
                    padding: EdgeInsets.all(_locked ? 12 : 20),
                    decoration: BoxDecoration(
                      color: MassarPalette.of(context).surface,
                      border: Border.all(color: MassarPalette.of(context).line),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        TextField(
                          key: const Key('payment-check-code'),
                          controller: _code,
                          focusNode: _focus,
                          enabled: current != null,
                          readOnly: _busy,
                          autofocus: true,
                          textInputAction: TextInputAction.search,
                          style: const TextStyle(fontSize: 22),
                          decoration: const InputDecoration(
                            labelText:
                                'الكود أو الباركود أو الاسم أو الهاتف — Enter للمراجعة',
                            prefixIcon: Icon(Icons.qr_code_scanner),
                          ),
                          onSubmitted: _scan,
                          onEditingComplete: () {},
                          onTapOutside: (_) => _focusCode(),
                          onChanged: _searchChanged,
                        ),
                        if (_busy)
                          const Padding(
                            padding: EdgeInsets.only(top: 14),
                            child: LinearProgressIndicator(),
                          ),
                        if (result != null && selected != null) ...[
                          SizedBox(height: _locked ? 10 : 18),
                          Container(
                            padding: EdgeInsets.all(_locked ? 10 : 18),
                            decoration: BoxDecoration(
                              color: selectedSettled && _error == null
                                  ? MassarPalette.of(context).successSurface
                                  : MassarPalette.of(context).errorSurface,
                              borderRadius: BorderRadius.circular(8),
                            ),
                            child: Row(
                              children: [
                                Icon(
                                  selectedSettled
                                      ? Icons.check_circle_outline
                                      : Icons.warning_amber,
                                  size: _locked ? 26 : 34,
                                  color: selectedSettled && _error == null
                                      ? MassarPalette.of(context).accent
                                      : Theme.of(context).colorScheme.error,
                                ),
                                const SizedBox(width: 16),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        '${selected.name} · كود ${selected.code}',
                                        style: TextStyle(
                                          fontWeight: FontWeight.bold,
                                          fontSize: _locked ? 16 : 18,
                                        ),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        _coverageLabel(
                                          selectedAttendance,
                                          result,
                                        ),
                                        key: const Key('payment-check-result'),
                                        style: TextStyle(
                                          fontSize: _locked ? 21 : 25,
                                          fontWeight: FontWeight.w800,
                                        ),
                                      ),
                                      Text(result.detail),
                                      if (_error != null)
                                        Text(
                                          _error!,
                                          key: const Key(
                                            'payment-review-warning',
                                          ),
                                          style: TextStyle(
                                            color: MassarPalette.of(
                                              context,
                                            ).error,
                                          ),
                                        ),
                                      if (selectedAttendance != null)
                                        Text(
                                          _amountDetail(
                                            selectedAttendance,
                                            result,
                                          ),
                                          style: const TextStyle(
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      if (selectedAttendance != null &&
                                          _hasReviewMark(selectedAttendance))
                                        TextButton.icon(
                                          key: const Key(
                                            'uncheck-selected-student',
                                          ),
                                          onPressed: _busy
                                              ? null
                                              : () => _uncheck(
                                                  selectedAttendance,
                                                ),
                                          icon: const Icon(Icons.undo),
                                          label: const Text(
                                            'إزالة مراجعة هذا الطالب',
                                          ),
                                        ),
                                      if (_locked)
                                        const Text(
                                          'مراجعة الإيصال لا تعني سداد المديونية؛ المتبقي ظاهر بالجدول.',
                                          style: TextStyle(fontSize: 12),
                                        ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ),
                          if (!_locked) ...[
                            const SizedBox(height: 8),
                            const Text(
                              'علامة المراجعة تؤكد المبلغ المحصّل بهذه الحصة، ولا تعني سداد المديونية المتبقية. تغطية الباقة السابقة والإعفاء لا تنشئ إيصالًا جديدًا.',
                            ),
                          ],
                        ],
                      ],
                    ),
                  ),
                  SizedBox(height: _locked ? 12 : 22),
                  _reviewedTable(attendees, studentsById, statuses),
                  Wrap(
                    spacing: 24,
                    runSpacing: 8,
                    children: [
                      Text(
                        'تمت مراجعة $reviewed من ${attendees.length} حاضر',
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 19,
                        ),
                      ),
                      Text('غير مسدد بالكامل: $missing'),
                      Text('الفئة المعروضة: ${visible.length} طالب'),
                    ],
                  ),
                  SizedBox(height: _locked ? 8 : 14),
                ],
                child: visible.isEmpty
                    ? const EmptySection(
                        message: 'لا يوجد حاضر يطابق الفئة والبحث الحاليين.',
                      )
                    : ManagementTable.builder(
                        pageSize: 20,
                        columns: const [
                          'الطالب والكود',
                          'نوع الدفع والمبلغ',
                          'تحصيل الحصة والمديونية',
                          'المراجعة',
                          'الإجراءات',
                        ],
                        rowCount: visible.length,
                        rowBuilder: (index) {
                          final entry = visible[index];
                          final student = studentsById[entry.studentId];
                          final status = statuses[entry.id]!;
                          final checked = _reviewed(entry, status);
                          return DataRow(
                            cells: [
                              DataCell(
                                Text(
                                  '${student?.name ?? '—'}\nكود ${student?.code ?? '—'}',
                                ),
                              ),
                              DataCell(Text(_coverageLabel(entry, status))),
                              DataCell(Text(_amountDetail(entry, status))),
                              DataCell(
                                Text(
                                  checked
                                      ? _hasUnpaidBalance(entry, status)
                                            ? 'تمت مراجعة الإيصال — ما زال مبلغ متبقٍ'
                                            : 'تمت مراجعة الإيصال'
                                      : _hasReviewMark(entry)
                                      ? 'مراجعة قديمة — أزلها ثم راجع'
                                      : _canReview(entry, status)
                                      ? 'بانتظار المراجعة'
                                      : 'لا تحصيل هنا — دون علامة',
                                ),
                              ),
                              DataCell(
                                Wrap(
                                  spacing: 8,
                                  children: [
                                    TextButton(
                                      onPressed:
                                          _busy ||
                                              !_canReview(entry, status) ||
                                              student == null
                                          ? null
                                          : () => _scan(
                                              student.barcode.isEmpty
                                                  ? student.code
                                                  : student.barcode,
                                            ),
                                      child: const Text('مراجعة'),
                                    ),
                                    if (_hasReviewMark(entry))
                                      TextButton.icon(
                                        key: ValueKey(
                                          'uncheck-payment-${entry.studentId}',
                                        ),
                                        onPressed: _busy
                                            ? null
                                            : () => _uncheck(entry),
                                        icon: const Icon(Icons.undo),
                                        label: const Text('إزالة المراجعة'),
                                      ),
                                    IconButton(
                                      tooltip: 'إلغاء حضور الطالب مع حفظ الأصل',
                                      onPressed: _busy
                                          ? null
                                          : () => _cancel(entry, status),
                                      icon: const Icon(
                                        Icons.remove_circle_outline,
                                      ),
                                    ),
                                    if (_paymentFor(entry, status) != null)
                                      IconButton(
                                        tooltip: 'إلغاء الدفع مع حفظ الأصل',
                                        onPressed: _busy
                                            ? null
                                            : () => _cancel(
                                                entry,
                                                status,
                                                paymentOnly: true,
                                              ),
                                        icon: const Icon(Icons.money_off),
                                      ),
                                  ],
                                ),
                              ),
                            ],
                          );
                        },
                      ),
              ),
            ),
          );
        },
      ),
    );
  }
}

class _PaperReviewPage extends StatefulWidget {
  const _PaperReviewPage({required this.store, required this.onBack});
  final VoidCallback onBack;
  final CenterStore store;
  @override
  State<_PaperReviewPage> createState() => _PaperReviewPageState();
}

class _PaperReviewPageState extends State<_PaperReviewPage> {
  static const _missingPayment = '__missing__';
  final _code = TextEditingController(),
      _paper = TextEditingController(),
      _notes = TextEditingController();
  final _codeFocus = FocusNode();
  final _form = GlobalKey<FormState>();
  String? _groupId, _sessionId, _studentId, _paymentChoice, _reviewId;
  String _statusFilter = 'all';
  DateTimeRange? _range;
  bool _busy = false;
  bool _checkingLeave = false;
  int _contextRevision = 0;
  String _savedPaper = '', _savedNotes = '';
  String? _error, _notice;
  CenterStore get store => widget.store;
  bool get _blocked => _busy || _checkingLeave;
  bool get _dirty => _paper.text != _savedPaper || _notes.text != _savedNotes;

  void _markClean() {
    _savedPaper = _paper.text;
    _savedNotes = _notes.text;
  }

  Future<bool> _allowContextChange() async {
    if (_blocked || hasPendingMassarNotice(context)) return false;
    setState(() => _checkingLeave = true);
    try {
      final leave = await confirmDiscardDraft(context, dirty: _dirty);
      return mounted && leave && !_busy;
    } finally {
      if (mounted) {
        setState(() {
          _checkingLeave = false;
          _contextRevision++;
        });
      }
    }
  }

  Future<void> _back() async {
    if (await _allowContextChange()) widget.onBack();
  }

  Future<void> _changeGroup(String? id) async {
    if (id == _groupId || !await _allowContextChange()) return;
    setState(() {
      _groupId = id;
      _sessionId = null;
      _resetSelection();
    });
  }

  Future<void> _changeSession(String? id) async {
    if (id == _sessionId || !await _allowContextChange()) return;
    setState(() {
      _sessionId = id;
      _resetSelection();
    });
  }

  Future<void> _showAllReviews() async {
    if (!await _allowContextChange()) return;
    setState(() {
      _code.clear();
      _resetSelection(clearStudent: true);
    });
  }

  Student? get _student =>
      store.students.where((item) => item.id == _studentId).firstOrNull;
  PaymentRecord? get _payment =>
      store.payments.where((item) => item.id == _paymentChoice).firstOrNull;
  PaymentReview? get _selectedReview =>
      store.reviews.where((item) => item.id == _reviewId).firstOrNull;
  bool get _refundedReview =>
      _selectedReview?.paymentId != null && _payment == null;
  List<PaymentRecord> get _payments =>
      store.payments
          .where(
            (item) =>
                item.studentId == _studentId &&
                (_groupId == null || item.groupId == _groupId) &&
                (_sessionId == null || item.sessionId == _sessionId),
          )
          .toList()
        ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
  String? get _effectiveSessionId => _payment?.sessionId ?? _sessionId;
  bool get _finalized =>
      _effectiveSessionId != null &&
      store.closings.any((item) => item.sessionId == _effectiveSessionId);

  @override
  void dispose() {
    _code.dispose();
    _paper.dispose();
    _notes.dispose();
    _codeFocus.dispose();
    super.dispose();
  }

  void _resetSelection({bool clearStudent = false}) {
    _paymentChoice = null;
    _reviewId = null;
    _paper.clear();
    _notes.clear();
    _markClean();
    _error = null;
    _notice = null;
    if (clearStudent) _studentId = null;
  }

  Future<void> _lookup(String value) async {
    if (!await _allowContextChange() || !mounted) return;
    final matches = studentLookupCandidates(store.students, value);
    setState(() {
      _busy = true;
      _resetSelection(clearStudent: true);
    });
    try {
      final student = matches.length == 1
          ? matches.single
          : matches.isEmpty
          ? null
          : await chooseMatchingStudent(context, matches);
      if (!mounted) return;
      if (student != null) {
        setState(() => _studentId = student.id);
      } else if (matches.isEmpty) {
        setState(() {
          _error = 'لم نجد طالبًا بهذا الكود أو الاسم أو الهاتف.';
        });
        await showManagementMessage(context, _error!, kind: NoticeKind.warning);
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        _prepareNextCode();
      }
    }
  }

  void _prepareNextCode() {
    _code.selection = TextSelection(
      baseOffset: 0,
      extentOffset: _code.text.length,
    );
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && !_busy && ModalRoute.of(context)?.isCurrent == true) {
        _codeFocus.requestFocus();
      }
    });
  }

  Future<void> _choosePayment(String value) async {
    if (value == _paymentChoice || !await _allowContextChange()) return;
    setState(() {
      _resetSelection();
      _paymentChoice = value;
      final existing = store.reviews
          .where((review) => review.paymentId == value)
          .firstOrNull;
      if (existing != null) {
        _reviewId = existing.id;
        _paper.text = priceText(existing.paperAmount);
        _notes.text = existing.notes;
      }
      _markClean();
    });
  }

  Future<void> _editReview(PaymentReview review) async {
    if (!await _allowContextChange()) return;
    final payment = store.allPayments
        .where((item) => item.id == review.paymentId)
        .firstOrNull;
    final session = store.sessions
        .where((item) => item.id == review.sessionId)
        .firstOrNull;
    setState(() {
      _studentId = review.studentId;
      _code.text = _student?.code ?? '';
      _sessionId = review.sessionId;
      _groupId = session?.groupId ?? payment?.groupId;
      _paymentChoice = review.paymentId ?? _missingPayment;
      _reviewId = review.id;
      _paper.text = priceText(review.paperAmount);
      _notes.text = review.notes;
      _markClean();
      _error = null;
      _notice = null;
    });
  }

  Future<void> _save() async {
    if (_blocked || _refundedReview) return;
    if (!_form.currentState!.validate()) return;
    if (_studentId == null || _paymentChoice == null) {
      setState(
        () => _error = 'اختار عملية دفع محددة أو بند الورق بدون دفع مسجل.',
      );
      await showManagementMessage(context, _error!, kind: NoticeKind.warning);
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });
    var savedSuccessfully = false;
    try {
      final previousIds = store.reviews.map((review) => review.id).toSet();
      final editingId = _reviewId;
      await store.savePaymentReview(
        ReviewRequest(
          id: _reviewId ?? '',
          studentId: _studentId!,
          sessionId: _effectiveSessionId,
          paymentId: _payment?.id,
          paperAmount: piastresFromText(_paper.text)!,
          notes: _notes.text.trim(),
        ),
      );
      if (!mounted) return;
      final saved = editingId == null
          ? store.reviews.firstWhere(
              (review) => !previousIds.contains(review.id),
            )
          : store.reviews.firstWhere((review) => review.id == editingId);
      setState(() {
        _reviewId = saved.id;
        _markClean();
        _notice = 'حُفظت المراجعة محليًا. لم تتغير المدفوعات أو الحضور.';
      });
      await showManagementMessage(context, _notice!, kind: NoticeKind.success);
      savedSuccessfully = true;
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.review_page');
      if (mounted) {
        setState(() => _error = managementError(error));
        await showManagementMessage(context, _error!, kind: NoticeKind.error);
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        if (savedSuccessfully) _prepareNextCode();
      }
    }
  }

  String _result(PaymentReview review) => review.paymentId == null
      ? 'بدون دفع مسجل'
      : review.matched
      ? 'مطابق'
      : review.difference < 0
      ? 'نقص في الورق'
      : 'زيادة في الورق';
  String _studentLabel(String id) {
    final value = store.students.where((item) => item.id == id).firstOrNull;
    return value == null ? 'طالب غير متاح' : '${value.name}\nكود ${value.code}';
  }

  List<PaymentReview> get _visibleReviews => store.reviews.where((review) {
    if (_studentId != null && review.studentId != _studentId) return false;
    if (_sessionId != null && review.sessionId != _sessionId) return false;
    if (_groupId != null) {
      final payment = store.allPayments
          .where((item) => item.id == review.paymentId)
          .firstOrNull;
      final session = store.sessions
          .where((item) => item.id == review.sessionId)
          .firstOrNull;
      if ((session?.groupId ?? payment?.groupId) != _groupId) return false;
    }
    if (_range != null &&
        (review.createdAt.isBefore(_range!.start) ||
            !review.createdAt.isBefore(
              _range!.end.add(const Duration(days: 1)),
            ))) {
      return false;
    }
    return switch (_statusFilter) {
      'matched' => review.matched,
      'difference' => review.paymentId != null && !review.matched,
      'missing' => review.paymentId == null,
      _ => true,
    };
  }).toList()..sort((a, b) => b.createdAt.compareTo(a.createdAt));

  Future<void> _pickDates() async {
    final selected = await showDateRangePicker(
      context: context,
      initialDateRange: _range,
      firstDate: DateTime(2020),
      lastDate: DateTime(2100),
    );
    if (selected != null && mounted) setState(() => _range = selected);
  }

  @override
  Widget build(BuildContext context) => WorkspaceDraftRegistration(
    dirty: _dirty,
    busy: _blocked,
    child: AnimatedBuilder(
      animation: store,
      builder: (context, _) {
        if (!store.canCollect) {
          return const EmptySection(
            message: 'مراجعة التحصيل متاحة للإدارة والاستقبال فقط.',
          );
        }
        final reviews = _visibleReviews;
        return ManagementPanel(
          title: 'مقارنة مبلغ الورق',
          actions: [
            TextButton.icon(
              onPressed: _blocked ? null : _back,
              icon: const Icon(Icons.arrow_back),
              label: const Text('الرجوع لمراجعة الأكواد'),
            ),
          ],
          subtitle:
              'ابحث بكود الطالب وقارن كل عملية بالورق. المراجعة لا تضيف إيرادًا ولا تعدل المدفوعات.',
          child: ManagementBody(
            header: [
              _filters(),
              const SizedBox(height: 16),
              if (_busy) const LinearProgressIndicator(minHeight: 3),
            ],
            child: LayoutBuilder(
              builder: (context, constraints) {
                final editor = _editor(
                  height: constraints.maxHeight < 560
                      ? 560
                      : constraints.maxHeight,
                );
                final records = _records(reviews);
                if (constraints.maxWidth < 1000) {
                  return MassarScrollView(
                    child: Column(
                      children: [
                        editor,
                        const SizedBox(height: 20),
                        SizedBox(height: 460, child: records),
                      ],
                    ),
                  );
                }
                return Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Expanded(
                      flex: 4,
                      child: _editor(height: constraints.maxHeight),
                    ),
                    const SizedBox(width: 22),
                    Expanded(flex: 6, child: records),
                  ],
                );
              },
            ),
          ),
        );
      },
    ),
  );

  Widget _filters() {
    final sessions =
        store.sessions
            .where((session) => _groupId == null || session.groupId == _groupId)
            .toList()
          ..sort(compareSessionsNewestFirst);
    return Wrap(
      spacing: 12,
      runSpacing: 12,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        SizedBox(
          width: 350,
          child: DropdownButtonFormField<String>(
            key: ValueKey('review-group-$_groupId-$_contextRevision'),
            initialValue: _groupId,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'المجموعة'),
            items: [
              const DropdownMenuItem(value: null, child: Text('كل المجموعات')),
              ...store.groups.map(
                (item) => DropdownMenuItem(
                  value: item.id,
                  child: Text(
                    store.groupLabel(item.id),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ),
            ],
            onChanged: _blocked ? null : _changeGroup,
          ),
        ),
        SizedBox(
          width: 320,
          child: DropdownButtonFormField<String>(
            key: ValueKey('review-session-$_sessionId-$_contextRevision'),
            initialValue: _sessionId,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'الحصة'),
            items: [
              const DropdownMenuItem(
                value: null,
                child: Text('كل الحصص والدفعات غير المنسوبة'),
              ),
              ...sessions.map(
                (item) => DropdownMenuItem(
                  value: item.id,
                  child: Text(
                    'حصة ${item.number} · ${sessionDateLabel(item)}',
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ),
            ],
            onChanged: _blocked ? null : _changeSession,
          ),
        ),
        SizedBox(
          width: 210,
          child: DropdownButtonFormField<String>(
            initialValue: _statusFilter,
            isExpanded: true,
            decoration: const InputDecoration(labelText: 'حالة المراجعة'),
            items: const [
              DropdownMenuItem(value: 'all', child: Text('كل الحالات')),
              DropdownMenuItem(value: 'matched', child: Text('مطابق')),
              DropdownMenuItem(
                value: 'difference',
                child: Text('نقص أو زيادة'),
              ),
              DropdownMenuItem(value: 'missing', child: Text('بدون دفع مسجل')),
            ],
            onChanged: _blocked
                ? null
                : (value) => setState(() => _statusFilter = value!),
          ),
        ),
        OutlinedButton.icon(
          onPressed: _blocked ? null : _pickDates,
          icon: const Icon(Icons.date_range_outlined),
          label: Text(
            _range == null
                ? 'تاريخ المراجعة'
                : '${shortDate(_range!.start)} – ${shortDate(_range!.end)}',
          ),
        ),
        if (_range != null)
          IconButton(
            tooltip: 'إلغاء الفترة',
            onPressed: _blocked ? null : () => setState(() => _range = null),
            icon: const Icon(Icons.close),
          ),
      ],
    );
  }

  Widget _editor({required double height}) {
    final student = _student, payment = _payment;
    final paper = piastresFromText(_paper.text);
    final expected = _refundedReview
        ? _selectedReview!.expectedAmount
        : payment?.collectedAmount ?? 0;
    final delta = paper == null ? null : paper - expected;
    final missing = _paymentChoice == _missingPayment;
    final status = _paymentChoice == null
        ? 'اختار عملية للمقارنة'
        : missing
        ? 'ورق بدون دفع مسجل'
        : paper == null
        ? 'أدخل مبلغ الورق'
        : delta == 0
        ? 'مطابق'
        : delta! < 0
        ? 'نقص في الورق'
        : 'زيادة في الورق';
    return Container(
      height: height,
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
                    TextField(
                      key: const Key('review-student-code'),
                      controller: _code,
                      focusNode: _codeFocus,
                      enabled: !_blocked,
                      textInputAction: TextInputAction.search,
                      decoration: const InputDecoration(
                        labelText: 'الكود أو الباركود أو الاسم أو الهاتف',
                        prefixIcon: Icon(Icons.qr_code_scanner),
                      ),
                      onSubmitted: _lookup,
                      onChanged: (_) {
                        if (_studentId != null && !_dirty && !_blocked) {
                          setState(() => _resetSelection(clearStudent: true));
                        }
                      },
                    ),
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 8,
                      runSpacing: 4,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        FilledButton.tonal(
                          onPressed: _blocked
                              ? null
                              : () => _lookup(_code.text),
                          child: const Text('بحث عن الطالب'),
                        ),
                        TextButton(
                          onPressed: _blocked ? null : _showAllReviews,
                          child: const Text('عرض كل المراجعات'),
                        ),
                      ],
                    ),
                    if (student == null)
                      const Padding(
                        padding: EdgeInsets.symmetric(vertical: 22),
                        child: Text(
                          'اختار الطالب بالكود أو الاسم أو الهاتف؛ لو له أكثر من عملية دفع، اختار العملية المقصودة من القائمة.',
                        ),
                      )
                    else ...[
                      const Divider(height: 26),
                      Text(
                        student.name,
                        style: const TextStyle(
                          fontSize: 21,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      Text('كود ${student.code}'),
                      const SizedBox(height: 14),
                      const Text(
                        'عملية الدفع التي تقابل بند الورق',
                        style: TextStyle(fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 8),
                      if (_payments.isEmpty)
                        const Text(
                          'لا توجد عمليات دفع مسجلة في السياق المختار.',
                        ),
                      RadioGroup<String>(
                        groupValue: _paymentChoice,
                        onChanged: _blocked
                            ? (_) {}
                            : (value) => _choosePayment(value!),
                        child: Column(
                          children: [
                            ..._payments.map(
                              (item) => RadioListTile<String>(
                                contentPadding: EdgeInsets.zero,
                                value: item.id,
                                title: Text(
                                  '${item.description} · ${money(item.collectedAmount)}',
                                ),
                                subtitle: Text(
                                  '${shortDate(item.createdAt)} · ${item.method} · ${item.packageId != null ? 'شراء باقة' : 'دفع حصة'}${store.reviews.any((review) => review.paymentId == item.id) ? ' · سبق مراجعته' : ''}',
                                ),
                              ),
                            ),
                            const RadioListTile<String>(
                              contentPadding: EdgeInsets.zero,
                              value: _missingPayment,
                              title: Text('بند في الورق بدون دفع مسجل'),
                              subtitle: Text(
                                'يُحفظ كبند غير مطابق ولا يُضاف إلى التحصيل.',
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 14),
                      TextFormField(
                        key: const Key('review-paper-amount'),
                        controller: _paper,
                        enabled: !_blocked && !_finalized && !_refundedReview,
                        keyboardType: const TextInputType.numberWithOptions(
                          decimal: true,
                        ),
                        decoration: const InputDecoration(
                          labelText: 'المبلغ المكتوب في الورق',
                          suffixText: 'جنيه',
                        ),
                        validator: validPrice,
                        onChanged: (_) => setState(() {}),
                      ),
                      const SizedBox(height: 14),
                      Container(
                        padding: const EdgeInsets.all(14),
                        decoration: BoxDecoration(
                          color: MassarPalette.of(context).subtle,
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            Text(
                              status,
                              key: const Key('review-match-status'),
                              style: const TextStyle(
                                fontWeight: FontWeight.bold,
                                fontSize: 18,
                              ),
                            ),
                            const SizedBox(height: 10),
                            _amountRow(
                              'المسجل بالنظام',
                              _paymentChoice == null ? '—' : money(expected),
                            ),
                            _amountRow(
                              'المكتوب في الورق',
                              paper == null ? '—' : money(paper),
                            ),
                            _amountRow(
                              'الفرق: الورق − المسجل',
                              delta == null || _paymentChoice == null
                                  ? '—'
                                  : money(delta),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 14),
                      TextFormField(
                        controller: _notes,
                        enabled: !_blocked && !_finalized && !_refundedReview,
                        maxLines: 2,
                        decoration: const InputDecoration(
                          labelText: 'ملاحظات المراجعة',
                        ),
                        onChanged: (_) => setState(() {}),
                      ),
                    ],
                  ],
                ),
              ),
            ),
            if (student != null) ...[
              const Divider(height: 24),
              if (_refundedReview)
                const Text(
                  'هذه مراجعة قديمة لدفعة مستردة، محفوظة للعرض فقط.',
                  key: Key('review-refunded-history'),
                )
              else if (_finalized)
                const Text('هذه الحصة مقفلة ماليًا. مراجعتها محفوظة للعرض فقط.')
              else
                FilledButton.icon(
                  key: const Key('save-payment-review'),
                  onPressed: _blocked || _paymentChoice == null ? null : _save,
                  icon: const Icon(Icons.fact_check_outlined),
                  label: Text(
                    _busy
                        ? 'جارٍ الحفظ…'
                        : _reviewId == null
                        ? 'حفظ المراجعة'
                        : 'تحديث المراجعة',
                  ),
                ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _amountRow(String label, String value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 3),
    child: Row(
      children: [
        Expanded(child: Text(label)),
        Text(value, style: const TextStyle(fontWeight: FontWeight.w600)),
      ],
    ),
  );

  Widget _records(List<PaymentReview> reviews) {
    final matched = reviews.where((item) => item.matched).length;
    final missing = reviews.where((item) => item.paymentId == null).length;
    final differences = reviews.length - matched - missing;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          _student == null ? 'سجل المراجعات' : 'مراجعات ${_student!.name}',
          style: const TextStyle(fontSize: 21, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 22,
          runSpacing: 8,
          children: [
            Text('مطابق: $matched'),
            Text('نقص أو زيادة: $differences'),
            Text('بدون دفع مسجل: $missing'),
          ],
        ),
        const SizedBox(height: 16),
        Expanded(
          child: reviews.isEmpty
              ? const EmptySection(
                  message:
                      'لم تُسجل مراجعات بهذا الفلتر. ابدأ بمقارنة بند الورق بكود الطالب.',
                )
              : ManagementTable(
                  columns: const [
                    'الطالب والكود',
                    'العملية',
                    'الورق',
                    'المسجل',
                    'الفرق',
                    'الحالة',
                    'الموظف / التاريخ',
                    'إجراء',
                  ],
                  rows: reviews.map((item) {
                    final payment = store.allPayments
                        .where((payment) => payment.id == item.paymentId)
                        .firstOrNull;
                    final finalized = store.closings.any(
                      (closing) => closing.sessionId == item.sessionId,
                    );
                    final refunded =
                        payment != null &&
                        !store.payments.any(
                          (active) => active.id == payment.id,
                        );
                    return DataRow(
                      cells: [
                        DataCell(Text(_studentLabel(item.studentId))),
                        DataCell(
                          Text(
                            payment == null
                                ? 'بدون دفع مسجل'
                                : '${payment.description}${refunded ? ' · دفعة مستردة' : ''}',
                          ),
                        ),
                        DataCell(Text(money(item.paperAmount))),
                        DataCell(Text(money(item.expectedAmount))),
                        DataCell(Text(money(item.difference))),
                        DataCell(Text(_result(item))),
                        DataCell(
                          Text(
                            '${store.staff.where((staff) => staff.id == item.staffId).firstOrNull?.name ?? '—'}\n${shortDate(item.createdAt)}',
                          ),
                        ),
                        DataCell(
                          TextButton(
                            onPressed: _blocked
                                ? null
                                : () => _editReview(item),
                            child: Text(
                              finalized || refunded ? 'عرض' : 'تعديل',
                            ),
                          ),
                        ),
                      ],
                    );
                  }).toList(),
                ),
        ),
      ],
    );
  }
}
