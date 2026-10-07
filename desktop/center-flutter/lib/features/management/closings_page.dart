import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import 'package:file_selector/file_selector.dart';
import 'package:intl/intl.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/workspace_draft_guard.dart';
import 'management_widgets.dart';
import 'review_page.dart';

class ClosingsPage extends StatefulWidget {
  const ClosingsPage({super.key, required this.store, this.initialSessionId});
  final CenterStore store;
  final String? initialSessionId;
  @override
  State<ClosingsPage> createState() => _ClosingsPageState();
}

class _ClosingsPageState extends State<ClosingsPage> {
  final _cash = TextEditingController(), _notes = TextEditingController();
  final _form = GlobalKey<FormState>();
  String? _groupId, _sessionId, _error, _notice;
  bool _busy = false, _reopening = false;
  String _initialCash = '', _initialNotes = '';
  bool _confirming = false, _submitted = false;
  int _selectionRevision = 0;
  bool get _working => _busy || _reopening || _confirming;
  bool get _dirty => _cash.text != _initialCash || _notes.text != _initialNotes;
  CenterStore get store => widget.store;
  MassarPalette get _palette => MassarPalette.of(context);
  LessonSession? get _session =>
      store.sessions.where((item) => item.id == _sessionId).firstOrNull;
  SessionClosing? get _closing =>
      store.closings.where((item) => item.sessionId == _sessionId).firstOrNull;

  String _reportTime(DateTime date) {
    final local = date.toLocal();
    return '${shortDate(local)} · ${DateFormat('HH:mm:ss', 'ar_EG').format(local)}';
  }

  @override
  void initState() {
    super.initState();
    final initial = store.sessions
        .where(
          (session) =>
              session.id == widget.initialSessionId &&
              session.status != SessionStatus.canceled,
        )
        .firstOrNull;
    if (initial != null) {
      _groupId = initial.groupId;
      _select(initial.id);
    } else {
      _chooseInitial();
    }
  }

  void _chooseInitial() {
    final sessions =
        store.sessions
            .where(
              (session) =>
                  session.status != SessionStatus.canceled &&
                  (_groupId == null || session.groupId == _groupId),
            )
            .toList()
          ..sort(compareSessionsNewestFirst);
    final pending = sessions
        .where(
          (session) =>
              session.status == SessionStatus.closed &&
              !store.closings.any((closing) => closing.sessionId == session.id),
        )
        .firstOrNull;
    _select(pending?.id ?? sessions.firstOrNull?.id);
  }

  void _select(String? id) {
    _sessionId = id;
    _error = null;
    _notice = null;
    final saved = _closing;
    _cash.text = saved == null ? '' : priceText(saved.actualCash);
    _notes.text = saved?.notes ?? '';
    _initialCash = _cash.text;
    _initialNotes = _notes.text;
    _submitted = false;
  }

  Future<void> _changeSelection({
    String? groupId,
    String? sessionId,
    bool changeGroup = false,
  }) async {
    if (_working) return;
    setState(() => _confirming = true);
    try {
      final accepted = await confirmDiscardDraft(context, dirty: _dirty);
      if (!mounted || !accepted) return;
      setState(() {
        if (changeGroup) {
          _groupId = groupId;
          _chooseInitial();
        } else {
          _select(sessionId);
        }
      });
    } finally {
      if (mounted) {
        setState(() {
          _confirming = false;
          _selectionRevision++;
        });
      }
    }
  }

  @override
  void dispose() {
    _cash.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _closeAttendance() async {
    final session = _session;
    if (session == null || _working) return;
    setState(() {
      _error = null;
      _notice = null;
      _confirming = true;
    });
    try {
      final accepted = await confirmManagement(
        context,
        title: 'إغلاق حضور ${sessionLabel(session)}',
        description:
            'سيُسجل غير المحضرين غائبين، وتُحسب الحصة من الباقات المؤهلة. هذا الإجراء يغلق الحضور فقط؛ التقفيل المالي يحتاج مراجعة النقدية وتأكيدًا مستقلًا.',
        confirmLabel: 'إغلاق الحضور',
      );
      if (!accepted || !mounted) return;
      setState(() => _busy = true);
      await store.closeSession(session.id);
      if (mounted) {
        setState(
          () => _notice =
              'أُغلق الحضور. راجع النقدية الفعلية ثم أكّد التقفيل المالي.',
        );
        await showManagementMessage(
          context,
          _notice!,
          kind: NoticeKind.success,
        );
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.closings_page');
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

  Future<void> _reopen() async {
    final closing = _closing;
    if (closing == null || _working || !store.canCollect) return;
    setState(() => _reopening = true);
    try {
      final discard = await confirmDiscardDraft(context, dirty: _dirty);
      if (!mounted || !discard) return;
      final acceptedReason = await showDialog<String>(
        context: context,
        builder: (_) => _ReopenClosingDialog(closing: closing),
      );
      if (!mounted || acceptedReason == null) return;
      setState(() {
        _busy = true;
        _error = null;
        _notice = null;
      });
      await store.reopenFinancialClosing(
        closingId: closing.id,
        reason: acceptedReason,
      );
      if (mounted) {
        setState(() {
          _cash.text = priceText(closing.actualCash);
          _notes.clear();
          _initialCash = _cash.text;
          _initialNotes = _notes.text;
          _notice =
              'أُعيد فتح التقفيلة. راجع النقدية ثم سجّل تقفيلة جديدة؛ النسخة الأصلية محفوظة في السجل.';
        });
        await showManagementMessage(
          context,
          _notice!,
          kind: NoticeKind.success,
        );
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.closings_page');
      if (mounted) {
        setState(() => _error = managementError(error));
        await showManagementMessage(context, _error!, kind: NoticeKind.error);
      }
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
          _reopening = false;
        });
      }
    }
  }

  Future<void> _finalize() async {
    final session = _session;
    if (session == null ||
        _working ||
        session.status != SessionStatus.closed ||
        !store.canCollect) {
      return;
    }
    setState(() => _submitted = true);
    if (!_form.currentState!.validate()) return;
    final actual = piastresFromText(_cash.text)!;
    final notes = _notes.text.trim();
    final summary = store.sessionFinancialSummary(session.id);
    final reviews = store.reviews
        .where((item) => item.sessionId == session.id)
        .toList();
    final unresolved = reviews.where((item) => !item.matched).length;
    final difference = actual - summary.expectedCash;
    setState(() {
      _error = null;
      _notice = null;
      _confirming = true;
    });
    try {
      final accepted = await confirmManagement(
        context,
        title: 'تأكيد التقفيل المالي — ${sessionLabel(session)}',
        description:
            'النقدي المسجل: ${money(summary.expectedCash)}\nالنقدية الفعلية: ${money(actual)}\nالفرق: ${money(difference)}\n${_codeReviewSummary(session.id)}\nعلامة المراجعة تخص التحصيل الفعلي الحالي، وقد يكون جزئيًا مع مديونية متبقية.\n${reviews.isEmpty ? 'مقارنة مبالغ الورق اختيارية ولم تُستخدم لهذه الحصة.' : 'مقارنة المبالغ: بنود مراجعة غير مطابقة: $unresolved من ${reviews.length}.'}\n\nالتقفيل يحفظ هذه الأرقام كما هي، ولا يعدل المدفوعات. النسخة الأصلية تبقى محفوظة. يمكن إعادة فتح التقفيلة بسبب مسجل ثم إنشاء نسخة جديدة بعد التصحيح.',
        confirmLabel: 'حفظ التقفيلة النهائية',
      );
      if (!accepted || !mounted) return;
      setState(() => _busy = true);
      await store.finalizeSession(
        sessionId: session.id,
        actualCash: actual,
        notes: notes,
      );
      if (mounted) {
        setState(() {
          _initialCash = _cash.text;
          _initialNotes = _notes.text;
          _notice =
              'حُفظت التقفيلة النهائية والفرق كما هو، بدون تعديل المدفوعات.';
        });
        await showManagementMessage(
          context,
          _notice!,
          kind: NoticeKind.success,
        );
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.closings_page');
      if (mounted) {
        setState(() => _error = managementError(error));
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

  String _codeReviewSummary(String sessionId) {
    final attendees = store.attendances
        .where(
          (entry) =>
              entry.sessionId == sessionId &&
              entry.status != AttendanceStatus.absent,
        )
        .toList();
    var reviewed = 0, exempt = 0, unsettled = 0;
    for (final entry in attendees) {
      final status = store.paymentStatusFor(entry.studentId, sessionId);
      if (paymentReviewIsExempt(store, entry, status)) exempt++;
      if (status.status == StudentPaymentStatus.notPaid ||
          status.debtAmount > 0) {
        unsettled++;
      }
      if (paymentReviewIsCurrent(store, entry, status)) reviewed++;
    }
    return 'مراجعة الحضور الحالي: $reviewed من ${attendees.length} مُراجع '
        '· $exempt إعفاء من رسوم المدرس · $unsettled عليه مبلغ متبقٍ أو بلا دفع';
  }

  Future<void> _exportReport() async {
    final session = _session;
    if (session == null ||
        session.status == SessionStatus.canceled ||
        _working ||
        !store.canCollect) {
      return;
    }
    final mode = _closing == null
        ? ClosingReportMode.live
        : ClosingReportMode.summary;
    setState(() => _busy = true);
    try {
      final timestamp = DateTime.now()
          .toIso8601String()
          .substring(0, 19)
          .replaceAll(':', '-');
      final location = await getSaveLocation(
        suggestedName: 'massar-session-${session.number}-$timestamp.csv',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'تقرير CSV', extensions: ['csv']),
        ],
      );
      if (location == null) return;
      final currentSession = store.sessions
          .where((item) => item.id == session.id)
          .firstOrNull;
      if (currentSession == null ||
          currentSession.status == SessionStatus.canceled) {
        throw const CenterException('الحصة غير متاحة للتقرير الآن.');
      }
      await CenterReports.exportCsv(
        store: store,
        kind: CenterReportKind.closings,
        filter: CenterReportFilter(sessionId: session.id, closingMode: mode),
        destination: location.path,
      );
      if (mounted) {
        await showManagementMessage(
          context,
          mode == ClosingReportMode.live
              ? 'حُفظ التقرير اللحظي بوقت إعداده. التصدير لا يغلق الحضور أو يحفظ تقفيلة.'
              : 'حُفظ تقرير التقفيلات المسجلة لهذه الحصة، مع توضيح النسخ التاريخية.',
          kind: NoticeKind.success,
        );
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.closings_page');
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

  Future<void> _reviewAttendees(PaymentReviewCategory category) async {
    final session = _session;
    if (session == null ||
        session.status == SessionStatus.canceled ||
        _working ||
        !store.canCollect) {
      return;
    }
    setState(() => _busy = true);
    try {
      await showDialog<void>(
        context: context,
        builder: (dialogContext) => Dialog(
          insetPadding: const EdgeInsets.all(20),
          child: SizedBox(
            width: 1100,
            height: MediaQuery.sizeOf(dialogContext).height * 0.85,
            child: Column(
              children: [
                Align(
                  alignment: AlignmentDirectional.centerEnd,
                  child: IconButton(
                    tooltip: 'إغلاق المراجعة',
                    onPressed: () => Navigator.pop(dialogContext),
                    icon: const Icon(Icons.close),
                  ),
                ),
                Expanded(
                  child: ReviewPage(
                    store: store,
                    sessionId: session.id,
                    initialCategory: category,
                  ),
                ),
              ],
            ),
          ),
        ),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _reviewActions(LessonSession session) => Wrap(
    spacing: 8,
    runSpacing: 8,
    children: [
      for (final category in PaymentReviewCategory.values)
        OutlinedButton(
          onPressed: _working || session.status == SessionStatus.canceled
              ? null
              : () => _reviewAttendees(category),
          child: Text(switch (category) {
            PaymentReviewCategory.all => 'مراجعة كل الحاضرين',
            PaymentReviewCategory.single => 'دفع الحصة',
            PaymentReviewCategory.package => 'شهر / رصيد سابق',
            PaymentReviewCategory.free => 'مجاني / إعفاء',
            PaymentReviewCategory.makeup => 'معوّض',
            PaymentReviewCategory.unpaid => 'غير مسدد',
            PaymentReviewCategory.centerOnly => 'سنتر فقط',
          }),
        ),
    ],
  );

  Widget _amount(String label, int amount, {bool prominent = false}) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Row(
      children: [
        Expanded(child: Text(label)),
        Text(
          money(amount),
          style: TextStyle(
            fontSize: prominent ? 23 : 16,
            fontWeight: prominent ? FontWeight.w800 : FontWeight.w600,
            color: _palette.ink,
          ),
        ),
      ],
    ),
  );

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: store,
    builder: (context, _) {
      if (!store.canCollect) {
        return const EmptySection(
          message: 'تقفيل الحصص متاح للإدارة والاستقبال فقط.',
        );
      }
      final session = _session;
      final closing = _closing;
      final summary = session == null
          ? null
          : closing?.summary ?? store.sessionFinancialSummary(session.id);
      return WorkspaceDraftRegistration(
        dirty: _dirty,
        busy: _working,
        child: ManagementPanel(
          title: 'تقرير الحصة والتقفيل',
          subtitle:
              'اعرض وصدّر التحصيل أثناء الحصة. التقفيل النهائي يحفظ النقدية والفرق بعد إغلاق الحضور.',
          child: ManagementBody(
            header: [
              _selectors(),
              if (session != null &&
                  session.status != SessionStatus.canceled) ...[
                const SizedBox(height: 12),
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: OutlinedButton.icon(
                    key: const Key('export-session-financial-report'),
                    onPressed: _working ? null : _exportReport,
                    icon: const Icon(Icons.download_outlined),
                    label: Text(
                      closing == null
                          ? 'تصدير تقرير الحصة اللحظي'
                          : 'تصدير التقفيلات المحفوظة',
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 16),
              if (_busy) const LinearProgressIndicator(minHeight: 3),
            ],
            child: session == null || summary == null
                ? const EmptySection(
                    message:
                        'أنشئ حصة من الإدارة ثم اختارها لمراجعة التحصيل والتقفيل.',
                  )
                : LayoutBuilder(
                    builder: (context, constraints) {
                      final figures = _summaryPanel(session, summary, closing);
                      final reconcile = _reconciliation(
                        session,
                        summary,
                        closing,
                      );
                      if (constraints.maxWidth < 1000) {
                        return MassarScrollView(
                          child: Column(
                            children: [
                              figures,
                              const SizedBox(height: 20),
                              reconcile,
                            ],
                          ),
                        );
                      }
                      return Row(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Expanded(
                            flex: 6,
                            child: MassarScrollView(child: figures),
                          ),
                          const SizedBox(width: 22),
                          Expanded(
                            flex: 4,
                            child: MassarScrollView(child: reconcile),
                          ),
                        ],
                      );
                    },
                  ),
          ),
        ),
      );
    },
  );

  Widget _selectors() {
    final sessions =
        store.sessions
            .where((session) => _groupId == null || session.groupId == _groupId)
            .toList()
          ..sort(compareSessionsNewestFirst);
    return KeyedSubtree(
      key: ValueKey(_selectionRevision),
      child: Wrap(
        spacing: 14,
        runSpacing: 12,
        children: [
          SizedBox(
            width: 400,
            child: DropdownButtonFormField<String>(
              key: ValueKey('closing-group-$_groupId'),
              initialValue: _groupId,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'المجموعة'),
              items: [
                const DropdownMenuItem(
                  value: null,
                  child: Text('كل المجموعات'),
                ),
                ...store.groups.map(
                  (group) => DropdownMenuItem(
                    value: group.id,
                    child: Text(
                      store.groupLabel(group.id),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ),
              ],
              onChanged: _working
                  ? null
                  : (value) =>
                        _changeSelection(groupId: value, changeGroup: true),
            ),
          ),
          SizedBox(
            width: 450,
            child: DropdownButtonFormField<String>(
              key: ValueKey('closing-session-$_sessionId'),
              initialValue: _sessionId,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'الحصة'),
              items: sessions
                  .map(
                    (session) => DropdownMenuItem(
                      value: session.id,
                      child: Text(
                        '${sessionLabel(session)} · ${sessionDateLabel(session)} · ${store.closings.any((closing) => closing.sessionId == session.id)
                            ? 'مقفلة ماليًا'
                            : session.status == SessionStatus.closed
                            ? 'الحضور مغلق'
                            : session.status == SessionStatus.canceled
                            ? 'ملغاة'
                            : 'الحضور مفتوح'}',
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  )
                  .toList(),
              onChanged: _working
                  ? null
                  : (value) => _changeSelection(sessionId: value),
            ),
          ),
        ],
      ),
    );
  }

  Widget _surface(Widget child) => Container(
    padding: const EdgeInsets.all(22),
    decoration: BoxDecoration(
      color: _palette.surface,
      border: Border.all(color: _palette.line),
      borderRadius: BorderRadius.circular(10),
    ),
    child: child,
  );

  Widget _summaryPanel(
    LessonSession session,
    SessionFinancialSummary summary,
    SessionClosing? closing,
  ) {
    final unassigned = store.payments
        .where(
          (payment) =>
              payment.groupId == session.groupId && payment.sessionId == null,
        )
        .length;
    return _surface(
      Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            '${sessionLabel(session)} · ${store.groupLabel(session.groupId)}',
            style: TextStyle(fontSize: 21, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),
          Text(
            '${sessionDateLabel(session)} · ${sessionKindLabel(session.kind)}${closing == null ? '' : ' · ملخص محفوظ وقت التقفيل'}',
          ),
          const SizedBox(height: 8),
          Text(
            closing == null
                ? 'تقرير لحظي — وقت الإعداد: ${_reportTime(DateTime.now())}'
                : 'أرقام محفوظة وقت التقفيل: ${_reportTime(closing.createdAt)}',
            key: const Key('session-report-time'),
          ),
          if (session.status == SessionStatus.open)
            const Padding(
              padding: EdgeInsets.only(top: 6),
              child: Text(
                'الحضور مفتوح؛ غير المسجلين لم يُحتسبوا غائبين. التقرير متاح الآن دون إغلاق الحصة.',
              ),
            ),
          const Divider(height: 30),
          Wrap(
            spacing: 24,
            runSpacing: 10,
            children: [
              Text(
                'إجمالي الحضور: ${summary.presentCount + summary.makeupCount}',
              ),
              Text('منهم معوّض: ${summary.makeupCount}'),
              Text('غياب مسجل: ${summary.absentCount}'),
              Text('دخول بالباقة: ${summary.prepaidCount}'),
              Text('إعفاء رسوم المدرس: ${summary.freeCount}'),
              Text(
                'باكدج: ${summary.studentCategories?.where((e) => e.kind == SessionStudentCategoryKind.packageMember).fold<int>(0, (sum, e) => sum + e.studentCount) ?? 0}',
              ),
              Text(
                summary.cardPaymentCount == null ||
                        summary.cardCollectedAmount == null
                    ? 'تحصيل الكروت: غير محفوظ في التقفيلة القديمة'
                    : 'تحصيل الكروت: ${summary.cardPaymentCount} عملية · ${money(summary.cardCollectedAmount!)}',
                key: const Key('closing-card-collections'),
              ),
              Text(
                summary.allFreeCount == null
                    ? 'إجمالي الإعفاء: غير متاح في هذه النسخة القديمة'
                    : 'إجمالي إعفاء رسوم المدرس: ${summary.allFreeCount} طالب',
                key: const Key('closing-all-free-count'),
              ),
              Text(
                summary.packageBuyerCount == null
                    ? 'عدد مشتري الباقات: غير متاح في هذه النسخة القديمة'
                    : 'مشترو الباقات لهذه الحصة: ${summary.packageBuyerCount} طالب',
                key: const Key('closing-package-buyer-count'),
              ),
            ],
          ),
          const SizedBox(height: 18),
          Text(_codeReviewSummary(session.id)),
          const SizedBox(height: 8),
          _reviewActions(session),
          const SizedBox(height: 18),
          _attendanceDiscounts(summary),
          const SizedBox(height: 20),
          _paymentAmounts(summary),
          const SizedBox(height: 20),
          _studentCategories(summary),
          const SizedBox(height: 20),
          const Text(
            'التحصيل حسب العمليات والأسعار الفعلية',
            style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
          ),
          const SizedBox(height: 8),
          Text(
            'عمليات الدفع الأصلية: ${summary.singlePaymentCount} حصة · ${summary.packageSalesCount} شراء باقة',
          ),
          const SizedBox(height: 12),
          if (summary.lines.isEmpty)
            const Text(
              'لا توجد عمليات دفع منسوبة لهذه الحصة. الحضور بالباقة والتعويض لا ينشئان تحصيلًا جديدًا.',
            )
          else
            LayoutBuilder(
              builder: (context, constraints) => MassarScrollView(
                scrollDirection: Axis.horizontal,
                child: ConstrainedBox(
                  constraints: BoxConstraints(minWidth: constraints.maxWidth),
                  child: DataTable(
                    headingRowHeight: 40,
                    dataRowMinHeight: 44,
                    dataRowMaxHeight: 58,
                    horizontalMargin: 10,
                    columnSpacing: 20,
                    headingRowColor: WidgetStatePropertyAll(
                      _palette.tableHeader,
                    ),
                    columns: const [
                      DataColumn(label: Text('البند')),
                      DataColumn(label: Text('العدد × صافي الوحدة')),
                      DataColumn(label: Text('الإجمالي')),
                    ],
                    rows: summary.lines
                        .map(
                          (line) => DataRow(
                            cells: [
                              DataCell(
                                ConstrainedBox(
                                  constraints: const BoxConstraints(
                                    maxWidth: 220,
                                  ),
                                  child: Text(line.label),
                                ),
                              ),
                              DataCell(
                                Text(
                                  '${line.count} × ${money(line.unitAmount)}',
                                ),
                              ),
                              DataCell(Text(money(line.total))),
                            ],
                          ),
                        )
                        .toList(),
                  ),
                ),
              ),
            ),
          const Divider(height: 28),
          _amount('السعر الأصلي للعمليات', summary.grossAmount),
          if (summary.centerFeeCollected > 0)
            _amount(
              'رسوم السنتر المحصّلة — مستقلة عن إيراد المدرس',
              summary.centerFeeCollected,
            ),
          _amount('إجمالي الخصومات', summary.discountAmount),
          _amount('مديونيات نشأت من دفعات الحصة', summary.debtAmount ?? 0),
          _amount(
            'المحصّل من تسديد مديونيات سابقة',
            summary.debtSettlementAmount ?? 0,
          ),
          _amount(
            'التحصيل قبل الاسترداد',
            summary.totalCollected + summary.refundAmount,
          ),
          _amount('المبالغ المستردة', summary.refundAmount),
          _amount(
            'صافي التحصيل دون رسوم السنتر',
            summary.totalCollected - summary.centerFeeCollected,
          ),
          _amount(
            'إجمالي التحصيل الفعلي',
            summary.totalCollected,
            prominent: true,
          ),
          _amount('صافي النقدي المسجل', summary.expectedCash),
          _amount(
            'صافي وسائل الدفع الأخرى',
            summary.totalCollected - summary.expectedCash,
          ),
          const SizedBox(height: 14),
          const Text(
            'الأسعار مأخوذة من كل عملية وقت الدفع. دخول الباقة المدفوعة سابقًا والحضور المجاني والتعويض لا تُحتسب كإيراد جديد.',
          ),
          if (unassigned > 0)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                'يوجد $unassigned دفعة لهذه المجموعة غير منسوبة لحصة؛ تظهر في التقارير ولا تدخل هذه التقفيلة.',
              ),
            ),
        ],
      ),
    );
  }

  Widget _attendanceDiscounts(SessionFinancialSummary summary) {
    final categories = summary.attendanceDiscountCategories;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'الخصم الثابت للطلبة الحاضرين',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
        ),
        if (categories != null) ...[
          const SizedBox(height: 8),
          Text(
            'إجمالي أصحاب الخصم: ${categories.where((row) => (row.discountPercent ?? 0) > 0).fold<int>(0, (sum, row) => sum + row.studentCount)} طالب · منهم إعفاء ١٠٠٪: ${categories.where((row) => row.discountPercent == 100).fold<int>(0, (sum, row) => sum + row.studentCount)} طالب',
            style: const TextStyle(fontWeight: FontWeight.bold),
          ),
        ],
        const SizedBox(height: 6),
        const Text(
          'حسب الخصم الثابت وقت تسجيل الحضور، حتى لو لم يدفع الطالب اليوم. لا يمثل تحصيلًا جديدًا ولا يُجمع مع فئات الدفع.',
        ),
        const SizedBox(height: 8),
        if (categories == null)
          const Text(
            'تفاصيل الخصم وقت الحضور غير متاحة في التقفيلة القديمة؛ لم يتم افتراض بيانات سابقة.',
          )
        else if (categories.isEmpty)
          const Text('لا يوجد حضور فعلي مسجل لهذه الحصة.')
        else
          _attendanceDiscountTable(categories),
      ],
    );
  }

  String _discountLabel(num? percent) => percent == null
      ? 'غير معروف'
      : percent == 0
      ? '0٪ · بدون خصم'
      : percent == 100
      ? '100٪ · إعفاء'
      : '${percentText(percent)}٪';

  Widget _attendanceDiscountTable(
    List<AttendanceDiscountCategory> categories,
  ) => _categoryDetailsTable(
    columns: const [
      DataColumn(label: Text('نسبة الخصم')),
      DataColumn(label: Text('عدد الطلبة')),
    ],
    rows: categories.map((row) {
      final key = row.discountPercent ?? 'unknown';
      return DataRow(
        cells: [
          DataCell(
            Tooltip(
              message: row.label,
              child: Text(
                _discountLabel(row.discountPercent),
                key: Key('attendance-discount-$key'),
              ),
            ),
          ),
          DataCell(
            Text(
              '${row.studentCount}',
              key: Key('attendance-discount-students-$key'),
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
          ),
        ],
      );
    }).toList(),
  );

  Widget _paymentAmounts(SessionFinancialSummary summary) {
    final categories = summary.paymentAmountCategories;
    final centerFees = summary.centerFeePaymentCategories ?? const [];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'الدفع حسب المبلغ المحصّل',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 6),
        const Text(
          'التجميع حسب المبلغ المحصّل ونوع الدفع، حتى لو اختلفت نسبة الخصم. رسوم السنتر مستقلة عن الحصة والباقة؛ العمليات المستردة مستبعدة من فئات دفع المدرس.',
        ),
        const SizedBox(height: 8),
        if (categories == null)
          const Text(
            'تجميع دفع المدرس حسب المبلغ غير متاح في التقفيلة القديمة.',
          ),
        if (categories != null &&
            categories.isEmpty &&
            centerFees.isEmpty &&
            summary.centerFeeCollected == 0)
          const Text('لا يوجد دفع نشط منسوب لهذه الحصة.'),
        if ((categories?.isNotEmpty ?? false) || centerFees.isNotEmpty)
          LayoutBuilder(
            builder: (context, constraints) => MassarScrollView(
              scrollDirection: Axis.horizontal,
              child: ConstrainedBox(
                constraints: BoxConstraints(minWidth: constraints.maxWidth),
                child: DataTable(
                  headingRowHeight: 38,
                  dataRowMinHeight: 40,
                  dataRowMaxHeight: 54,
                  horizontalMargin: 8,
                  columnSpacing: 16,
                  headingRowColor: WidgetStatePropertyAll(_palette.tableHeader),
                  columns: const [
                    DataColumn(label: Text('الدفع')),
                    DataColumn(label: Text('المبلغ')),
                    DataColumn(label: Text('طلبة')),
                    DataColumn(label: Text('عمليات')),
                  ],
                  rows: [
                    ...?categories?.map((row) {
                      final key = '${row.kind.name}-${row.unitAmount}';
                      return DataRow(
                        cells: [
                          DataCell(
                            Text(
                              row.kind == SessionStudentCategoryKind.package
                                  ? 'شهر / باقة'
                                  : row.kind ==
                                        SessionStudentCategoryKind
                                            .debtSettlement
                                  ? 'تسديد مديونية'
                                  : 'حصة',
                            ),
                          ),
                          DataCell(Text(money(row.unitAmount))),
                          DataCell(
                            Text(
                              '${row.studentCount}',
                              key: Key('payment-amount-students-$key'),
                            ),
                          ),
                          DataCell(
                            Text(
                              '${row.operationCount}',
                              key: Key('payment-amount-operations-$key'),
                            ),
                          ),
                        ],
                      );
                    }),
                    for (final row in centerFees)
                      DataRow(
                        cells: _centerFeePaymentCells(
                          row,
                          keyPrefix: 'payment-amount-',
                        ),
                      ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }

  Widget _studentCategories(SessionFinancialSummary summary) {
    final categories = summary.studentCategories;
    final centerFees = summary.centerFeePaymentCategories ?? const [];
    final paid = (categories ?? const <SessionStudentCategory>[])
        .where(
          (row) =>
              row.kind == SessionStudentCategoryKind.single ||
              row.kind == SessionStudentCategoryKind.package,
        )
        .toList();
    final attendance = (categories ?? const <SessionStudentCategory>[])
        .where(
          (row) =>
              row.kind != SessionStudentCategoryKind.single &&
              row.kind != SessionStudentCategoryKind.package,
        )
        .toList();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'تصنيف الطلبة في هذه الحصة',
          style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 6),
        const Text(
          'الطلبة يُحسبون داخل كل فئة؛ قد يملك الطالب أكثر من عملية أو يظهر في فئة حضور أيضًا.',
        ),
        if (categories == null)
          const Text(
            'هذه التقفيلة القديمة محفوظة بدون تصنيف للطلبة. الأرقام المالية الأصلية محفوظة كما هي.',
          ),
        if (summary.centerFeePaymentCategories == null &&
            summary.centerFeeCollected > 0)
          const Padding(
            padding: EdgeInsets.only(top: 8),
            child: Text(
              'تفاصيل الدافعين لرسوم السنتر غير محفوظة في هذه التقفيلة القديمة؛ إجمالي رسوم السنتر محفوظ كما هو.',
            ),
          ),
        if (categories != null &&
            categories.isEmpty &&
            centerFees.isEmpty &&
            summary.centerFeeCollected == 0)
          const Padding(
            padding: EdgeInsets.only(top: 12),
            child: Text('لا يوجد حضور أو دفع مسجل لهذه الحصة.'),
          ),
        if (paid.isNotEmpty || centerFees.isNotEmpty) ...[
          const SizedBox(height: 12),
          const Text(
            'الخصم والمبلغ المدفوع',
            style: TextStyle(fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 6),
          const Text(
            'خصم المدرس محفوظ وقت الدفع، ورسوم السنتر مستقلة عنه. المبلغ هو المحصّل فعليًا لكل عملية حتى لو كان جزئيًا. عدد الطلبة بدون تكرار داخل الصف؛ قد يظهر الطالب في أكثر من صف. إجمالي المدفوع يعتمد على عدد العمليات.',
          ),
          const SizedBox(height: 8),
          _discountPaymentTable(paid, centerFees),
        ],
        if (attendance.isNotEmpty) ...[
          const SizedBox(height: 12),
          const Text(
            'الحضور وحالة رسوم المدرس',
            style: TextStyle(fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),
          _categoryTable(attendance),
        ],
      ],
    );
  }

  Widget _discountPaymentTable(
    List<SessionStudentCategory> categories,
    List<SessionCenterFeePaymentCategory> centerFees,
  ) => _categoryDetailsTable(
    columns: const [
      DataColumn(label: Text('نسبة الخصم')),
      DataColumn(label: Text('نوع الدفع')),
      DataColumn(label: Text('المدفوع لكل عملية')),
      DataColumn(label: Text('عدد الطلبة')),
      DataColumn(label: Text('عدد العمليات')),
      DataColumn(label: Text('إجمالي المدفوع')),
    ],
    rows: [
      ...categories.map((row) {
        final key =
            '${row.kind.name}-${row.discountPercent ?? 'none'}-${row.unitAmount}';
        return DataRow(
          cells: [
            DataCell(
              Text(
                _discountLabel(row.discountPercent),
                key: Key('category-discount-$key'),
              ),
            ),
            DataCell(
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 205),
                child: Text(row.label, key: Key('category-label-$key')),
              ),
            ),
            DataCell(
              Text(
                row.unitAmount == 0
                    ? 'بدون تحصيل (${money(0)})'
                    : money(row.unitAmount),
                key: Key('category-amount-$key'),
              ),
            ),
            DataCell(
              Text(
                '${row.studentCount}',
                key: Key('category-students-$key'),
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
            ),
            DataCell(
              Text(
                '${row.operationCount}',
                key: Key('category-operations-$key'),
              ),
            ),
            DataCell(
              Text(
                money(row.unitAmount * row.operationCount),
                key: Key('category-total-$key'),
              ),
            ),
          ],
        );
      }),
      for (final row in centerFees) _centerFeeDiscountRow(row),
    ],
  );

  String _centerFeeCategoryKey(SessionCenterFeePaymentCategory row) =>
      '${row.centerOnly ? 'only' : 'independent'}-${row.unitAmount}';

  DataRow _centerFeeDiscountRow(SessionCenterFeePaymentCategory row) => DataRow(
    cells: [
      DataCell(
        Tooltip(
          message: 'رسوم السنتر مستقلة عن خصم المدرس',
          child: Text(
            '—',
            key: Key('center-fee-discount-${_centerFeeCategoryKey(row)}'),
          ),
        ),
      ),
      ..._centerFeePaymentCells(row),
      DataCell(
        Text(
          money(row.unitAmount * row.operationCount),
          key: Key('center-fee-total-${_centerFeeCategoryKey(row)}'),
        ),
      ),
    ],
  );

  List<DataCell> _centerFeePaymentCells(
    SessionCenterFeePaymentCategory row, {
    String keyPrefix = '',
  }) {
    final key = _centerFeeCategoryKey(row);
    return [
      DataCell(
        Text(
          row.centerOnly ? 'سنتر فقط' : 'رسوم سنتر مستقلة',
          key: Key('${keyPrefix}center-fee-kind-$key'),
        ),
      ),
      DataCell(
        Text(
          money(row.unitAmount),
          key: Key('${keyPrefix}center-fee-amount-$key'),
        ),
      ),
      DataCell(
        Text(
          '${row.studentCount}',
          key: Key('${keyPrefix}center-fee-students-$key'),
        ),
      ),
      DataCell(
        Text(
          '${row.operationCount}',
          key: Key('${keyPrefix}center-fee-operations-$key'),
        ),
      ),
    ];
  }

  Widget _categoryTable(
    List<SessionStudentCategory> categories,
  ) => _categoryDetailsTable(
    columns: const [
      DataColumn(label: Text('الفئة')),
      DataColumn(label: Text('طلبة')),
      DataColumn(label: Text('عمليات')),
      DataColumn(label: Text('المبلغ / تحصيل جديد')),
    ],
    rows: categories.map((row) {
      final key =
          '${row.kind.name}-${row.discountPercent ?? 'none'}-${row.unitAmount}';
      return DataRow(
        cells: [
          DataCell(
            ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 205),
              child: Text(row.label, key: Key('category-label-$key')),
            ),
          ),
          DataCell(
            Text(
              '${row.studentCount}',
              key: Key('category-students-$key'),
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
          ),
          DataCell(
            Text('${row.operationCount}', key: Key('category-operations-$key')),
          ),
          DataCell(
            Text(money(row.unitAmount), key: Key('category-amount-$key')),
          ),
        ],
      );
    }).toList(),
  );

  Widget _categoryDetailsTable({
    required List<DataColumn> columns,
    required List<DataRow> rows,
  }) => LayoutBuilder(
    builder: (context, constraints) => MassarScrollView(
      scrollDirection: Axis.horizontal,
      child: ConstrainedBox(
        constraints: BoxConstraints(minWidth: constraints.maxWidth),
        child: DataTable(
          headingRowHeight: 38,
          dataRowMinHeight: 40,
          dataRowMaxHeight: 60,
          horizontalMargin: 8,
          columnSpacing: 16,
          headingRowColor: WidgetStatePropertyAll(_palette.tableHeader),
          columns: columns,
          rows: rows,
        ),
      ),
    ),
  );

  Widget _reconciliation(
    LessonSession session,
    SessionFinancialSummary summary,
    SessionClosing? closing,
  ) {
    final reviews = store.reviews
        .where((review) => review.sessionId == session.id)
        .toList();
    final unresolved = reviews.where((review) => !review.matched).length;
    final actual = closing?.actualCash ?? piastresFromText(_cash.text);
    final difference = actual == null ? null : actual - summary.expectedCash;
    return _surface(
      Form(
        key: _form,
        autovalidateMode: _submitted
            ? AutovalidateMode.onUserInteraction
            : AutovalidateMode.disabled,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Semantics(
                  liveRegion: true,
                  child: Text(
                    'تعذر إتمام العملية. $_error\nالمدخلات موجودة؛ راجع السبب وحاول مجددًا.',
                    style: TextStyle(color: _palette.error),
                  ),
                ),
              ),
            Text(
              closing == null
                  ? 'مراجعة النقدية الفعلية'
                  : 'تقفيلة نهائية محفوظة',
              style: TextStyle(fontSize: 21, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: _palette.subtle,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    _codeReviewSummary(session.id),
                    style: TextStyle(fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'المراجعة تخص الإيصال المحصّل للحاضرين الحاليين. التحصيل الجزئي يمكن مراجعته مع بقاء المديونية؛ دون تحصيل في الحصة لا توجد علامة مراجعة.',
                  ),
                  if (reviews.isNotEmpty) ...[
                    const SizedBox(height: 8),
                    Text(
                      'مقارنة مبالغ الورق: ${reviews.length - unresolved} مطابق · $unresolved غير مطابق',
                    ),
                  ],
                  if (unresolved > 0)
                    const Padding(
                      padding: EdgeInsets.only(top: 8),
                      child: Text(
                        'راجع الفروق والبنود بدون دفع مسجل. التقفيل لا يعالجها تلقائيًا.',
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(height: 18),
            if ((summary.debtAmount ?? 0) > 0)
              _amount('مديونيات نشأت من دفعات الحصة', summary.debtAmount!),
            if ((summary.debtSettlementAmount ?? 0) > 0)
              _amount('تسديد مديونيات سابقة', summary.debtSettlementAmount!),
            if (summary.refundAmount > 0)
              _amount('المبالغ المستردة', summary.refundAmount),
            if (summary.centerFeeCollected > 0)
              _amount(
                'رسوم السنتر المحصّلة — مستقلة عن إيراد المدرس',
                summary.centerFeeCollected,
              ),
            _amount(
              'صافي التحصيل دون رسوم السنتر',
              summary.totalCollected - summary.centerFeeCollected,
            ),
            _amount(
              'إجمالي التحصيل الفعلي',
              summary.totalCollected,
              prominent: true,
            ),
            _amount('النقدي المتوقع من العمليات', summary.expectedCash),
            if (closing != null)
              _amount('النقدية المحفوظة في التقفيلة', closing.actualCash)
            else
              TextFormField(
                key: const Key('closing-actual-cash'),
                controller: _cash,
                enabled:
                    !_working &&
                    closing == null &&
                    session.status != SessionStatus.canceled,
                decoration: const InputDecoration(
                  labelText: 'النقدية الموجودة فعليًا',
                  suffixText: 'جنيه',
                  helperText: 'قارن النقدية فقط؛ التحويلات والبطاقات مستبعدة.',
                ),
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                validator: validPrice,
                onChanged: (_) => setState(() => _error = null),
              ),
            const SizedBox(height: 16),
            Text(
              difference == null
                  ? 'أدخل النقدية الفعلية لعرض الفرق.'
                  : difference == 0
                  ? 'النقدية مطابقة'
                  : difference < 0
                  ? 'عجز في النقدية'
                  : 'زيادة في النقدية',
              key: const Key('closing-difference-status'),
              style: TextStyle(
                fontWeight: FontWeight.bold,
                fontSize: 20,
                color: difference == 0 ? _palette.accent : _palette.ink,
              ),
            ),
            if (difference != null)
              _amount('الفرق: الفعلي − المتوقع', difference, prominent: true),
            const SizedBox(height: 14),
            if (closing != null)
              Text(
                'ملاحظات التقفيلة المحفوظة: ${closing.notes.isEmpty ? 'لا توجد' : closing.notes}',
              )
            else
              TextFormField(
                controller: _notes,
                enabled: !_working && closing == null,
                onChanged: (_) => setState(() => _error = null),
                maxLines: 3,
                decoration: const InputDecoration(labelText: 'ملاحظات التقفيل'),
              ),
            const SizedBox(height: 18),
            if (closing != null) ...[
              if (_dirty)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: Text(
                    'توجد مسودة محلية لم تُحفظ؛ التقفيلة المعروضة محفوظة بالفعل.\nنقدية المسودة: ${_cash.text.isEmpty ? 'لم تُكتب' : _cash.text} جنيه\nملاحظات المسودة: ${_notes.text.isEmpty ? 'لا توجد' : _notes.text}',
                    style: TextStyle(color: _palette.warning),
                  ),
                ),
              Text(
                'حُفظت بواسطة ${store.staff.where((staff) => staff.id == closing.staffId).firstOrNull?.name ?? '—'} · ${shortDate(closing.createdAt)}',
              ),
              const SizedBox(height: 10),
              const Text(
                'نسخة التقفيلة محفوظة للعرض. إعادة الفتح بسبب مسجل تسمح بالتصحيح وإنشاء نسخة جديدة، مع بقاء هذه النسخة في السجل.',
              ),
              const SizedBox(height: 14),
              OutlinedButton.icon(
                key: const Key('reopen-financial-closing'),
                onPressed: _working ? null : _reopen,
                icon: Icon(Icons.lock_open_outlined),
                label: const Text('إعادة فتح التقفيلة للتصحيح'),
              ),
            ] else if (session.status == SessionStatus.open) ...[
              const Text(
                'يمكن عرض وتصدير التقرير الآن والحضور مفتوح. إغلاق الحضور مطلوب فقط لحفظ التقفيلة النهائية، وسيُسجّل غير المحضرين غائبين.',
              ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                key: const Key('close-attendance-before-finance'),
                onPressed: _working ? null : _closeAttendance,
                icon: Icon(Icons.event_available_outlined),
                label: const Text('إغلاق الحضور أولًا'),
              ),
            ] else if (session.status == SessionStatus.canceled)
              const Text('الحصة ملغاة ولا يمكن تقفيلها ماليًا.')
            else
              FilledButton.icon(
                key: const Key('finalize-session-finance'),
                onPressed: _working ? null : _finalize,
                icon: Icon(Icons.lock_outline),
                label: Text(_busy ? 'جارٍ الحفظ…' : 'مراجعة وتأكيد التقفيلة'),
              ),
          ],
        ),
      ),
    );
  }
}

class _ReopenClosingDialog extends StatefulWidget {
  const _ReopenClosingDialog({required this.closing});
  final SessionClosing closing;
  @override
  State<_ReopenClosingDialog> createState() => _ReopenClosingDialogState();
}

class _ReopenClosingDialogState extends State<_ReopenClosingDialog> {
  final _reason = TextEditingController();
  final _form = GlobalKey<FormState>();
  bool _submitted = false, _returning = false;

  Future<void> _submit() async {
    if (_returning) return;
    setState(() => _submitted = true);
    if (!_form.currentState!.validate()) return;
    final reason = _reason.text.trim();
    setState(() => _returning = true);
    await WidgetsBinding.instance.endOfFrame;
    if (mounted) Navigator.pop(context, reason);
  }

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => WorkspaceDraftRegistration(
    dirty: !_returning && _reason.text.isNotEmpty,
    busy: false,
    child: ScrollableMassarDialog(
      title: const Text('إعادة فتح التقفيلة المالية'),
      content: Form(
        key: _form,
        autovalidateMode: _submitted
            ? AutovalidateMode.onUserInteraction
            : AutovalidateMode.disabled,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'النقدية المحفوظة: ${money(widget.closing.actualCash)}\nالنقدي المتوقع: ${money(widget.closing.summary.expectedCash)}\nالفرق المحفوظ: ${money(widget.closing.difference)}',
            ),
            const SizedBox(height: 16),
            const Text(
              'تبقى التقفيلة الأصلية في السجل. إعادة الفتح تسمح بالتصحيح ثم تسجيل تقفيلة جديدة، ولا تغيّر المدفوعات أو الحضور تلقائيًا.',
            ),
            const SizedBox(height: 16),
            TextFormField(
              key: const Key('reopen-closing-reason'),
              controller: _reason,
              enabled: !_returning,
              onChanged: (_) => setState(() {}),
              autofocus: true,
              minLines: 2,
              maxLines: 3,
              decoration: const InputDecoration(
                labelText: 'سبب إعادة الفتح (إلزامي)',
              ),
              validator: (text) =>
                  text?.trim().isEmpty ?? true ? 'اكتب سبب إعادة الفتح' : null,
            ),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _returning ? null : () => Navigator.maybePop(context),
          child: const Text('رجوع'),
        ),
        FilledButton(
          onPressed: _returning ? null : _submit,
          child: const Text('تأكيد إعادة الفتح'),
        ),
      ],
    ),
  );
}
