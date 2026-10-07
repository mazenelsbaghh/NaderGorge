import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/theme.dart';

/// Reviews an explicit cancellation. Keyboard scans never submit this dialog.
class RecordCancellationDialog extends StatefulWidget {
  const RecordCancellationDialog({
    super.key,
    required this.store,
    this.paymentId,
    this.attendanceId,
  }) : assert((paymentId == null) != (attendanceId == null));

  final CenterStore store;
  final String? paymentId, attendanceId;

  @override
  State<RecordCancellationDialog> createState() =>
      _RecordCancellationDialogState();
}

class _RecordCancellationDialogState extends State<RecordCancellationDialog> {
  final _form = GlobalKey<FormState>();
  final _reason = TextEditingController();
  final _reasonFocus = FocusNode();
  RecordCancellationMode _mode = RecordCancellationMode.recordOnly;
  String _method = 'نقدي';
  bool _busy = false, _finished = false;
  bool _reopenFinancialClosings = false;

  PaymentRecord? get _payment => widget.store.allPayments
      .where((record) => record.id == widget.paymentId)
      .firstOrNull;
  AttendanceRecord? get _attendance => widget.store.allAttendances
      .where((record) => record.id == widget.attendanceId)
      .firstOrNull;
  String? get _studentId => _payment?.studentId ?? _attendance?.studentId;
  String? get _sessionId => _payment?.sessionId ?? _attendance?.sessionId;
  LessonSession? get _session => widget.store.sessions
      .where((record) => record.id == _sessionId)
      .firstOrNull;
  bool get _isPayment => widget.paymentId != null;
  bool get _active => _isPayment
      ? widget.store.payments.any((record) => record.id == widget.paymentId)
      : widget.store.attendances.any(
          (record) =>
              record.id == widget.attendanceId &&
              record.status != AttendanceStatus.absent,
        );

  PaymentRecord? get _relatedPayment {
    final attendance = _attendance;
    if (attendance == null) return null;
    return widget.store.payments
        .where(
          (payment) => attendance.packageId != null
              ? payment.packageId == attendance.packageId
              : payment.packageId == null &&
                    payment.studentId == attendance.studentId &&
                    payment.sessionId == attendance.sessionId,
        )
        .firstOrNull;
  }

  List<AttendanceRecord> get _relatedAttendances {
    final payment = _payment;
    if (payment == null) return const [];
    return widget.store.attendances
        .where(
          (attendance) => payment.packageId != null
              ? attendance.packageId == payment.packageId
              : attendance.studentId == payment.studentId &&
                    attendance.sessionId == payment.sessionId,
        )
        .toList();
  }

  List<SessionClosing> get _financialClosings =>
      !_active || !widget.store.canCollect
      ? const []
      : widget.store.cancellationFinancialClosings(
          paymentId: widget.paymentId,
          attendanceId: widget.attendanceId,
          mode: _mode,
        );

  String? get _packageRestriction {
    final packageId = _payment?.packageId ?? _attendance?.packageId;
    if (packageId == null) return null;
    final package = widget.store.packages
        .where((entry) => entry.id == packageId)
        .firstOrNull;
    if (package == null) return 'دفعة الشهر لم تعد سارية؛ راجع السجل.';
    if (!_isPayment && _mode == RecordCancellationMode.recordOnly) return null;
    final linked = widget.store.attendances
        .where((entry) => entry.packageId == packageId)
        .toList();
    if (_isPayment && _mode == RecordCancellationMode.recordOnly) {
      return linked.any(
            (entry) =>
                entry.status == AttendanceStatus.absent ||
                entry.originalAttendanceId != null,
          )
          ? 'الشهر مرتبط بغياب محسوب أو تعويض عن غياب؛ راجع هذه التسجيلات أولًا.'
          : null;
    }
    if (!_isPayment && linked.any((entry) => entry.id != widget.attendanceId)) {
      return 'للشهر استخدامات أخرى؛ اختر الحضور فقط. إلغاء دفعة الشهر بالكامل متاح من سجل الدفع مع مراجعة كل التسجيلات المرتبطة.';
    }
    final entries = _isPayment ? linked : [_attendance!];
    if (entries.any(
      (entry) =>
          entry.status == AttendanceStatus.absent ||
          (widget.store.sessions
                      .where((s) => s.id == entry.sessionId)
                      .firstOrNull
                      ?.status !=
                  SessionStatus.open &&
              !(entry.status == AttendanceStatus.makeup &&
                  entry.makeupSourceGroupId != null)),
    )) {
      return 'الشهر به حصة مغلقة أو غياب محسوب لا يمكن رد حصته تلقائيًا. اختر الحضور فقط للاحتفاظ بالاستهلاك، أو أعد فتح الحصة المرتبطة لمراجعتها أولًا.';
    }
    return null;
  }

  @override
  void dispose() {
    _reason.dispose();
    _reasonFocus.dispose();
    super.dispose();
  }

  void _cancel() {
    if (_busy || _finished || hasPendingMassarNotice(context)) return;
    _finished = true;
    Navigator.of(context).pop(false);
  }

  String get _effect {
    final both = _mode == RecordCancellationMode.recordAndRelated;
    if (_isPayment) {
      if (_payment?.packageId != null) {
        return both
            ? 'إلغاء دفعة الشهر والتسجيلات المرتبطة القابلة للإلغاء، وإرجاع رصيدها ثم رد المبلغ المحصّل فعليًا. استخدام حصة مغلقة أو غياب محسوب يمنع رد الشهر؛ التعويض من المجموعة الأصلية يرجع رصيده عند الإلغاء.'
            : 'إلغاء دفعة الشهر ورد المبلغ المحصّل فعليًا وإلغاء مديونيتها. يظل الحضور والتعويض مسجلين دون تغطية الشهر الملغى؛ يمكن دفع حصة أو شهر جديد.';
      }
      return both
          ? _session?.status == SessionStatus.closed
                ? 'رد المبلغ المحصّل فعليًا وإلغاء الحضور المرتبط؛ الحضور العادي في الحصة المغلقة يصبح غيابًا، والتعويض يُلغى. تبقى العملية الأصلية وسبب الإلغاء في السجل.'
                : 'رد المبلغ المدفوع وإلغاء الحضور المرتبط بهذه الدفعة أيضًا. تبقى العملية الأصلية وسبب الإلغاء في السجل.'
          : 'رد المبلغ المدفوع وإلغاء الدفعة فقط. يبقى الحضور مسجلًا، ويظهر أنه بلا دفع ساري؛ لا يُسجل دفع بديل تلقائيًا.';
    }
    if (_attendance?.packageId != null) {
      return both
          ? 'إلغاء هذا الحضور ورد المبلغ المحصّل من الشهر إذا لم يبق أي استخدام آخر له، وإلغاء المديونية المتبقية من الدفعة. إذا تعذر رد الشهر، تبقى العملية كاملة كما كانت.'
          : _session?.status == SessionStatus.closed &&
                _attendance?.makeupSourceGroupId == null
          ? 'تحويل الحضور إلى غياب مدفوع؛ تظل الحصة مستهلكة من الباقة ولا يُرد مبلغ.'
          : 'إلغاء الحضور وإرجاع حصة واحدة إلى رصيد الباقة. تبقى دفعة شراء الباقة سارية ولا يُرد مبلغ.';
    }
    if (_attendance?.status == AttendanceStatus.makeup &&
        _attendance?.makeupSourceGroupId == null) {
      return 'إلغاء دخول التعويض وإتاحة الغياب الأصلي للتعويض من جديد. لا يوجد دفع لهذه الحصة يُرد.';
    }
    if (_attendance?.centerFeeOnly == true) {
      return 'إلغاء حضور الطالب المعفى من رسوم المدرس. رسوم السنتر وإيصالات سدادها مستقلة وتبقى محفوظة؛ لا يحدث استرداد لهذه الرسوم.';
    }
    if (_attendance?.status == AttendanceStatus.makeup) {
      return both
          ? 'إلغاء التعويض ورد دفعة الحصة المرتبطة إن وجدت، مع حفظ الأصل والسبب.'
          : 'إلغاء التعويض فقط؛ تبقى دفعة الحصة المرتبطة سارية ولا يُرد مبلغ.';
    }
    return both
        ? _session?.status == SessionStatus.closed
              ? 'تحويل الحضور في هذه الحصة المغلقة إلى غياب ورد دفع الحصة المرتبط به إن وجد. تبقى الدفعة الأصلية والاسترداد وسبب الإلغاء في السجل.'
              : 'إلغاء الحضور ورد دفع الحصة المرتبط به إن وجد. تبقى الدفعة الأصلية والاسترداد وسبب الإلغاء في السجل.'
        : _session?.status == SessionStatus.closed
        ? 'تحويل الحضور إلى غياب؛ يبقى دفع الحصة ساريًا ولا يُرد مبلغ.'
        : 'إلغاء الحضور فقط. يبقى دفع الحصة ساريًا ولا يُرد مبلغ.';
  }

  Future<void> _save() async {
    if (_busy ||
        _finished ||
        !_active ||
        !widget.store.canCollect ||
        _packageRestriction != null ||
        hasPendingMassarNotice(context) ||
        !_form.currentState!.validate()) {
      return;
    }
    final mode = _mode, reason = _reason.text.trim(), method = _method;
    final reopen = _reopenFinancialClosings;
    setState(() => _busy = true);
    try {
      if (_isPayment) {
        await widget.store.cancelPayment(
          paymentId: widget.paymentId!,
          reason: reason,
          mode: mode,
          refundMethod: method,
          reopenFinancialClosings: reopen,
        );
      } else {
        await widget.store.cancelAttendance(
          attendanceId: widget.attendanceId!,
          reason: reason,
          mode: mode,
          refundMethod: method,
          reopenFinancialClosings: reopen,
        );
      }
      if (!mounted) return;
      await showMassarNotice(
        context,
        _isPayment
            ? 'تم إلغاء الدفع وحفظ أثر العملية وسببها في السجل.'
            : 'تم إلغاء الحضور وحفظ أثر العملية وسببها في السجل.',
        kind: NoticeKind.success,
      );
      if (mounted) {
        _finished = true;
        Navigator.of(context).pop(true);
      }
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'ui.attendance_workspace');
      if (mounted) {
        await showMassarNotice(
          context,
          error is CenterException
              ? error.message
              : 'تعذر الإلغاء. لم تكتمل العملية؛ راجع السجل وحاول مرة أخرى.',
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted &&
              !_busy &&
              !hasPendingMassarNotice(context) &&
              ModalRoute.of(context)?.isCurrent == true) {
            _reasonFocus.requestFocus();
          }
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: widget.store,
    builder: (context, _) {
      final palette = MassarPalette.of(context);
      final student = widget.store.students
          .where((record) => record.id == _studentId)
          .firstOrNull;
      final group = widget.store.groups
          .where((record) => record.id == _session?.groupId)
          .firstOrNull;
      final financialClosings = _financialClosings;
      final restriction = _packageRestriction;
      return PopScope(
        canPop: !_busy,
        child: Focus(
          onKeyEvent: (_, event) {
            if (_busy || _finished || hasPendingMassarNotice(context)) {
              return KeyEventResult.handled;
            }
            if (event.logicalKey == LogicalKeyboardKey.escape) {
              if (event is KeyDownEvent) _cancel();
              return KeyEventResult.handled;
            }
            if (event.logicalKey == LogicalKeyboardKey.enter ||
                event.logicalKey == LogicalKeyboardKey.numpadEnter) {
              return KeyEventResult.handled;
            }
            return KeyEventResult.ignored;
          },
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: ScrollableMassarDialog(
              key: const Key('record-cancellation-dialog'),
              title: Text(
                _isPayment ? 'مراجعة إلغاء الدفع' : 'مراجعة إلغاء الحضور',
              ),
              content: Form(
                key: _form,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      '${student?.name ?? 'طالب غير متاح'} · كود ${student?.code ?? '—'}',
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      _session == null
                          ? 'دفع غير مرتبط بحصة'
                          : '${sessionLabel(_session!)} · ${sessionDateLabel(_session!)} · ${group?.name ?? 'المجموعة غير متاحة'}',
                    ),
                    if (_payment != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        '${_payment!.description} · المدفوع ${money(widget.store.paymentCollectedFor(_payment!.id))} · ${widget.store.effectivePaymentMethod(_payment!.id)}',
                      ),
                    ],
                    if (!_isPayment && _relatedPayment != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        'الدفع المرتبط: ${_relatedPayment!.description} · ${money(widget.store.paymentCollectedFor(_relatedPayment!.id))} · ${widget.store.effectivePaymentMethod(_relatedPayment!.id)}',
                      ),
                    ],
                    const SizedBox(height: 18),
                    DropdownButtonFormField<RecordCancellationMode>(
                      key: const Key('cancellation-mode'),
                      initialValue: _mode,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'ماذا تريد إلغاءه؟',
                      ),
                      items: [
                        DropdownMenuItem(
                          value: RecordCancellationMode.recordOnly,
                          child: Text(_isPayment ? 'الدفع فقط' : 'الحضور فقط'),
                        ),
                        DropdownMenuItem(
                          value: RecordCancellationMode.recordAndRelated,
                          child: Text(
                            _isPayment
                                ? 'الدفع والحضور المرتبط'
                                : 'الحضور والدفع المرتبط',
                          ),
                        ),
                      ],
                      onChanged: _busy
                          ? null
                          : (mode) => setState(() {
                              _mode = mode!;
                              _reopenFinancialClosings = false;
                            }),
                    ),
                    const SizedBox(height: 12),
                    Container(
                      padding: const EdgeInsets.all(12),
                      color: palette.warningSurface,
                      child: Text(
                        _effect,
                        style: TextStyle(color: palette.ink),
                      ),
                    ),
                    if (_isPayment &&
                        _mode == RecordCancellationMode.recordAndRelated &&
                        _relatedAttendances.isNotEmpty) ...[
                      const SizedBox(height: 12),
                      Text(
                        'الحضور المرتبط (${_relatedAttendances.length}):',
                        style: const TextStyle(fontWeight: FontWeight.bold),
                      ),
                      ..._relatedAttendances.map((attendance) {
                        final session = widget.store.sessions
                            .where(
                              (record) => record.id == attendance.sessionId,
                            )
                            .firstOrNull;
                        return Text(
                          session == null
                              ? 'حصة غير متاحة'
                              : '${sessionLabel(session)} · ${sessionDateLabel(session)} · ${widget.store.groupLabel(session.groupId)} · ${attendanceLabel(attendance.status)} · ${session.status == SessionStatus.open ? 'مفتوحة' : 'مغلقة'}',
                        );
                      }),
                    ],
                    const SizedBox(height: 12),
                    if (restriction != null) ...[
                      Text(restriction, style: TextStyle(color: palette.error)),
                      const SizedBox(height: 12),
                    ],
                    if (financialClosings.isNotEmpty) ...[
                      CheckboxListTile(
                        key: const Key('cancellation-reopen-closings'),
                        value: _reopenFinancialClosings,
                        controlAffinity: ListTileControlAffinity.leading,
                        contentPadding: EdgeInsets.zero,
                        title: const Text(
                          'إعادة فتح التقفيلات المالية المتأثرة مع الإلغاء',
                        ),
                        subtitle: Text(
                          'عددها ${financialClosings.length}. تبقى كل نسخة أصلية محفوظة، ويُسجل السبب والموظف. راجع النقدية وأعد التقفيل بعد الإلغاء.',
                        ),
                        onChanged: _busy
                            ? null
                            : (value) => setState(
                                () => _reopenFinancialClosings = value == true,
                              ),
                      ),
                      ...financialClosings.map((closing) {
                        final session = widget.store.sessions
                            .where((s) => s.id == closing.sessionId)
                            .first;
                        return Text(
                          '${sessionLabel(session)} · ${widget.store.groupLabel(session.groupId)}',
                        );
                      }),
                    ] else
                      const Text(
                        'السجلات الأصلية لا تُحذف؛ لا توجد تقفيلة مالية سارية متأثرة بهذا الاختيار.',
                      ),
                    const SizedBox(height: 16),
                    TextFormField(
                      key: const Key('cancellation-reason'),
                      controller: _reason,
                      focusNode: _reasonFocus,
                      enabled: !_busy,
                      autofocus: true,
                      minLines: 2,
                      maxLines: 4,
                      maxLength: 1000,
                      decoration: const InputDecoration(
                        labelText: 'سبب الإلغاء',
                        hintText: 'اكتب سببًا واضحًا يحفظ مع اسم الموظف',
                      ),
                      validator: (value) => value?.trim().isNotEmpty == true
                          ? null
                          : 'اكتب سبب الإلغاء أولًا',
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<String>(
                      key: const Key('cancellation-refund-method'),
                      initialValue: _method,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'طريقة رد المبلغ إن وجد',
                      ),
                      items: ['نقدي', 'إنستاباي', 'تحويل بنكي', 'بطاقة']
                          .map(
                            (method) => DropdownMenuItem(
                              value: method,
                              child: Text(method),
                            ),
                          )
                          .toList(),
                      onChanged: _busy
                          ? null
                          : (method) => setState(() => _method = method!),
                    ),
                    if (!_active || !widget.store.canCollect) ...[
                      const SizedBox(height: 12),
                      Text(
                        !_active
                            ? 'هذه العملية لم تعد سارية؛ راجع السجل.'
                            : 'يلزم صلاحية الإدارة أو الاستقبال للإلغاء.',
                        style: TextStyle(color: palette.error),
                      ),
                    ],
                  ],
                ),
              ),
              actions: [
                TextButton(
                  key: const Key('cancellation-cancel'),
                  onPressed: _busy ? null : _cancel,
                  child: const Text('رجوع بدون إلغاء'),
                ),
                Shortcuts(
                  shortcuts: const {
                    SingleActivator(LogicalKeyboardKey.enter):
                        DoNothingAndStopPropagationIntent(),
                    SingleActivator(LogicalKeyboardKey.numpadEnter):
                        DoNothingAndStopPropagationIntent(),
                  },
                  child: FilledButton(
                    key: const Key('cancellation-confirm'),
                    style: FilledButton.styleFrom(
                      backgroundColor: Theme.of(context).colorScheme.error,
                      foregroundColor: Theme.of(context).colorScheme.onError,
                    ),
                    onPressed:
                        _busy ||
                            !_active ||
                            !widget.store.canCollect ||
                            restriction != null ||
                            financialClosings.isNotEmpty &&
                                !_reopenFinancialClosings
                        ? null
                        : _save,
                    child: Text(
                      _busy
                          ? 'جارٍ حفظ الإلغاء…'
                          : _mode == RecordCancellationMode.recordAndRelated
                          ? 'إلغاء الدفع والحضور المرتبط'
                          : _isPayment
                          ? 'إلغاء الدفعة فقط'
                          : 'إلغاء الحضور فقط',
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
    },
  );
}
