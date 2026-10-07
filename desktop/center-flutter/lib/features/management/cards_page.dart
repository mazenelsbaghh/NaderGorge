import 'package:massar_center/shared/student_lookup_dialog.dart';
import 'package:massar_center/domain/student_lookup.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';

import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../features/cards/student_card_actions.dart';
import '../../shared/document_service.dart';
import '../../shared/scrollable_dialog.dart';
import '../../shared/formatters.dart';
import '../../shared/workspace_draft_guard.dart';
import '../../shared/notice_dialog.dart' show hasPendingMassarNotice;
import '../../domain/discount_calculation.dart';
import '../attendance/paid_amount_dialog.dart';
import '../attendance/entry_confirmation_dialog.dart'
    show EntryConfirmationDialog;
import 'management_widgets.dart';

enum CardReceiptFilter { pending, received, all }

class CardsPage extends StatefulWidget {
  const CardsPage({super.key, required this.store});
  final CenterStore store;

  @override
  State<CardsPage> createState() => _CardsPageState();
}

class _CardsPageState extends State<CardsPage> {
  final _search = TextEditingController();
  final _codeFocus = FocusNode();
  String? _groupId, _studentId;
  var _receiptFilter = CardReceiptFilter.pending;
  var _method = 'نقدي';
  bool _paidOnly = false, _busy = false;
  ValueNotifier<bool>? _pendingPayment;

  @override
  void dispose() {
    _search.dispose();
    _codeFocus.dispose();
    super.dispose();
  }

  List<Student> get _scopedStudents => widget.store.students
      .where(
        (student) => _groupId == null || student.groupIds.contains(_groupId),
      )
      .toList();

  List<Student> get _matches {
    final query = _search.text.trim().toLowerCase();
    final receivedIds = widget.store.cardReceipts
        .map((receipt) => receipt.studentId)
        .toSet();
    final paidIds = widget.store.cardPayments
        .map((payment) => payment.studentId)
        .toSet();
    return _scopedStudents.where((student) {
      final received = receivedIds.contains(student.id);
      return switch (_receiptFilter) {
            CardReceiptFilter.pending => !received,
            CardReceiptFilter.received => received,
            CardReceiptFilter.all => true,
          } &&
          (!_paidOnly || paidIds.contains(student.id)) &&
          studentMatchesSearch(student, query);
    }).toList();
  }

  void _focusCode() => WidgetsBinding.instance.addPostFrameCallback((_) {
    if (!mounted || ModalRoute.of(context)?.isCurrent != true) return;
    _codeFocus.requestFocus();
    _search.selection = TextSelection(
      baseOffset: 0,
      extentOffset: _search.text.length,
    );
  });

  Future<void> _scan() async {
    if (_busy) return;
    final query = _search.text.trim();
    if (query.isEmpty) return;
    final exact = studentsWithIdentifier(_scopedStudents, query);
    final matches = exact.isNotEmpty ? exact : _matches;
    Student? selected;
    if (matches.length == 1) {
      selected = matches.single;
    } else if (matches.length > 1) {
      setState(() => _busy = true);
      selected = await chooseMatchingStudent(context, matches);
      if (!mounted) return;
      setState(() => _busy = false);
      if (_search.text.trim() != query) return;
    } else {
      await showManagementMessage(
        context,
        'لا يوجد طالب يطابق الكود أو الباركود أو الاسم في المجموعة المختارة.',
        kind: NoticeKind.warning,
      );
    }
    if (!mounted) return;
    if (selected == null) {
      _focusCode();
      return;
    }
    setState(() => _studentId = selected!.id);
    _codeFocus.unfocus();
  }

  Future<void> _run(Future<void> Function() action) async {
    if (_busy ||
        !widget.store.canCollect ||
        hasPendingMassarNotice(context) ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    setState(() => _busy = true);
    try {
      await action();
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.cards_page');
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
        _focusCode();
      }
    }
  }

  Future<void> _pay(Student student) => _run(() async {
    final price = widget.store.cardSettings.price;
    if (price == null) throw const CenterException('حدد سعر الكارت أولًا.');
    final method = _method;
    final blocked = ValueNotifier(false);
    _pendingPayment = blocked;
    try {
      final route = DialogRoute<PaidAmountSelection>(
        context: context,
        barrierDismissible: false,
        builder: (_) => PaidAmountDialog(
          title: 'دفع كارت الطالب',
          studentLabel: '${student.name} · ${student.code}',
          options: [
            PaidAmountPriceOption(
              id: 0,
              label: 'كارت الطالب',
              dueAmount: discountedAmount(price, student.discountPercent),
            ),
          ],
          initialOptionId: 0,
          method: method,
          description:
              'هذه رسوم الكارت فقط؛ باقي المطلوب يُحفظ كمديونية. لا يُسجل حضور أو استلام بهذه العملية.',
          scannerBlocked: blocked,
        ),
      );
      final payment = await Navigator.of(context).push(route);
      await route.completed;
      if (payment == null || !mounted || blocked.value) return;
      await widget.store.collectStudentCard(
        studentId: student.id,
        method: method,
        paidAmount: payment.paidAmount,
        expectedNetAmount: payment.dueAmount,
      );
      if (mounted) {
        await showManagementMessage(
          context,
          'سُجل دفع كارت ${student.name}. المطلوب ${money(payment.dueAmount)} · المدفوع ${money(payment.collectedAmount)} · المديونية ${money(payment.dueAmount - payment.collectedAmount)}.',
        );
      }
    } finally {
      _pendingPayment = null;
      blocked.dispose();
    }
  });

  KeyEventResult _paymentKey(FocusNode node, KeyEvent event) {
    final blocked = _pendingPayment;
    if (blocked == null) return KeyEventResult.ignored;
    if (EntryConfirmationDialog.isScannerText(event)) blocked.value = true;
    return KeyEventResult.handled;
  }

  Future<void> _receive(Student student) => _run(() async {
    final confirmed = await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (context) => ScrollableMassarDialog(
        title: const Text('تأكيد تسليم الكارت'),
        content: Text(
          '${student.name} · كود ${student.code}\nهل استلم كارت الدخول فعليًا؟ الطباعة وحدها لا تُعتبر استلامًا.',
        ),
        actions: [
          TextButton(
            autofocus: true,
            onPressed: () => Navigator.pop(context, false),
            child: const Text('رجوع'),
          ),
          FilledButton(
            key: const Key('confirm-card-receipt'),
            onPressed: () {
              if (ModalRoute.of(context)?.isCurrent == true) {
                Navigator.pop(context, true);
              }
            },
            child: const Text('تأكيد الاستلام'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    await widget.store.receiveStudentCard(student.id);
    if (mounted) {
      await showManagementMessage(context, 'سُجل استلام كارت ${student.name}.');
    }
  });

  Future<void> _print(List<Student> students) => _run(() async {
    await DocumentService.showStudentCards(context, widget.store, students);
  });

  String _paymentStatus(Student student) {
    final payment = widget.store.cardPaymentFor(student.id);
    if (payment == null) return 'لم يُسجل دفع';
    final debt = widget.store.cardDebtFor(payment.id);
    final collected = widget.store.cardCollectedFor(payment.id);
    return 'المدفوع ${money(collected)}${debt > 0 ? ' · مديونية ${money(debt)}' : ' · مسدد بالكامل'}';
  }

  @override
  Widget build(BuildContext context) {
    final scoped = _scopedStudents;
    final receivedIds = widget.store.cardReceipts
        .map((receipt) => receipt.studentId)
        .toSet();
    final paidIds = widget.store.cardPayments
        .map((payment) => payment.studentId)
        .toSet();
    final matches = _matches;
    final printable = matches
        .where((student) => !receivedIds.contains(student.id))
        .toList();
    final selected = widget.store.students
        .where((student) => student.id == _studentId)
        .firstOrNull;
    final pending = scoped
        .where((student) => !receivedIds.contains(student.id))
        .length;
    final paidPending = scoped
        .where(
          (student) =>
              !receivedIds.contains(student.id) && paidIds.contains(student.id),
        )
        .length;
    return WorkspaceDraftRegistration(
      dirty: false,
      busy: _busy,
      child: Focus(
        onKeyEvent: _paymentKey,
        child: ManagementPanel(
          title: 'كروت الطلبة',
          subtitle:
              'دفع الكارت وتسليمه مستقلان عن الحصص. الطباعة لا تسجل استلامًا.',
          actions: [
            if (widget.store.canCollect)
              OutlinedButton.icon(
                key: const Key('print-pending-student-cards'),
                onPressed: _busy || printable.isEmpty
                    ? null
                    : () => _print(printable),
                icon: const Icon(Icons.print_outlined),
                label: Text('طباعة / حفظ غير المستلمين (${printable.length})'),
              ),
          ],
          child: !widget.store.canCollect
              ? const EmptySection(
                  message: 'إدارة الكروت متاحة للاستقبال ومازن.',
                )
              : ManagementBody(
                  header: [
                    Wrap(
                      spacing: 12,
                      runSpacing: 12,
                      children: [
                        SizedBox(
                          width: 360,
                          child: TextField(
                            key: const Key('cards-student-search'),
                            controller: _search,
                            focusNode: _codeFocus,
                            enabled: !_busy,
                            textInputAction: TextInputAction.search,
                            decoration: InputDecoration(
                              labelText: 'كود الطالب أو الباركود أو الاسم',
                              prefixIcon: const Icon(
                                Icons.person_search_outlined,
                              ),
                              suffixIcon: _search.text.isEmpty
                                  ? null
                                  : IconButton(
                                      tooltip: 'مسح بحث الكروت',
                                      onPressed: _busy
                                          ? null
                                          : () {
                                              setState(() {
                                                _search.clear();
                                                _studentId = null;
                                              });
                                              _focusCode();
                                            },
                                      icon: const Icon(Icons.clear),
                                    ),
                            ),
                            onChanged: (_) => setState(() => _studentId = null),
                            onSubmitted: (_) => _scan(),
                          ),
                        ),
                        SizedBox(
                          width: 300,
                          child: DropdownButtonFormField<String>(
                            initialValue: _groupId,
                            isExpanded: true,
                            decoration: const InputDecoration(
                              labelText: 'المجموعة',
                            ),
                            items: [
                              const DropdownMenuItem(
                                value: null,
                                child: Text('كل المجموعات'),
                              ),
                              ...widget.store.groups.map(
                                (group) => DropdownMenuItem(
                                  value: group.id,
                                  child: Text(
                                    widget.store.groupLabel(group.id),
                                  ),
                                ),
                              ),
                            ],
                            onChanged: _busy
                                ? null
                                : (id) => setState(() {
                                    _groupId = id;
                                    _studentId = null;
                                  }),
                          ),
                        ),
                        SizedBox(
                          width: 220,
                          child: DropdownButtonFormField<CardReceiptFilter>(
                            key: const Key('cards-receipt-filter'),
                            initialValue: _receiptFilter,
                            isExpanded: true,
                            decoration: const InputDecoration(
                              labelText: 'حالة الاستلام',
                            ),
                            items: const [
                              DropdownMenuItem(
                                value: CardReceiptFilter.pending,
                                child: Text('لم يستلموا'),
                              ),
                              DropdownMenuItem(
                                value: CardReceiptFilter.received,
                                child: Text('استلموا'),
                              ),
                              DropdownMenuItem(
                                value: CardReceiptFilter.all,
                                child: Text('كل الطلبة'),
                              ),
                            ],
                            onChanged: _busy
                                ? null
                                : (value) => setState(() {
                                    _receiptFilter = value!;
                                    _studentId = null;
                                  }),
                          ),
                        ),
                      ],
                    ),
                    CheckboxListTile(
                      key: const Key('cards-paid-only'),
                      contentPadding: EdgeInsets.zero,
                      controlAffinity: ListTileControlAffinity.leading,
                      title: const Text('المدفوع فقط'),
                      subtitle: const Text(
                        'الطباعة تشمل نتائج البحث الذين لم يستلموا فقط، سواء دُفع الكارت أم لم يُدفع ما لم تختَر هذا الفلتر.',
                      ),
                      value: _paidOnly,
                      onChanged: _busy
                          ? null
                          : (value) => setState(() {
                              _paidOnly = value!;
                              _studentId = null;
                            }),
                    ),
                    Wrap(
                      spacing: 16,
                      runSpacing: 8,
                      children: [
                        Text(
                          '${_groupId == null ? 'كل الطلبة' : 'طلبة المجموعة'}: ${scoped.length}',
                        ),
                        Text('لم يستلموا: $pending'),
                        Text('استلموا: ${scoped.length - pending}'),
                        Text('دفعوا ولم يستلموا: $paidPending'),
                      ],
                    ),
                    const SizedBox(height: 12),
                    if (selected != null) ...[
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              '${selected.code} — ${selected.name}',
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                          ),
                          SizedBox(
                            width: 180,
                            child: DropdownButtonFormField<String>(
                              decoration: const InputDecoration(
                                labelText: 'طريقة الدفع',
                              ),
                              initialValue: _method,
                              items: const ['نقدي', 'تحويل', 'محفظة']
                                  .map(
                                    (method) => DropdownMenuItem(
                                      value: method,
                                      child: Text(method),
                                    ),
                                  )
                                  .toList(),
                              onChanged: _busy
                                  ? null
                                  : (method) =>
                                        setState(() => _method = method!),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 8),
                      StudentCardActions(
                        store: widget.store,
                        student: selected,
                        onPay: _busy ? null : () => _pay(selected),
                        onReceive: _busy ? null : () => _receive(selected),
                      ),
                      const SizedBox(height: 12),
                    ],
                  ],
                  child: matches.isEmpty
                      ? const EmptySection(
                          message:
                              'لا يوجد طلبة يطابقون بحث الكروت والفلاتر المختارة.',
                        )
                      : ManagementTable(
                          key: ValueKey(
                            'cards-$_groupId-$_receiptFilter-$_paidOnly-${_search.text}',
                          ),
                          columns: const [
                            'الكود',
                            'الطالب',
                            'المجموعات',
                            'دفع الكارت',
                            'استلام الكارت',
                            'الإجراء',
                          ],
                          rows: matches
                              .map(
                                (student) => DataRow(
                                  selected: student.id == _studentId,
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
                                      SizedBox(
                                        width: 220,
                                        child: Text(
                                          student.groupIds
                                              .map(widget.store.groupLabel)
                                              .join('\n'),
                                          maxLines: 2,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ),
                                    ),
                                    DataCell(Text(_paymentStatus(student))),
                                    DataCell(
                                      Text(
                                        !receivedIds.contains(student.id)
                                            ? 'لم يستلم'
                                            : 'تم الاستلام',
                                      ),
                                    ),
                                    DataCell(
                                      TextButton(
                                        key: ValueKey(
                                          'card-select-${student.id}',
                                        ),
                                        onPressed: _busy
                                            ? null
                                            : () => setState(
                                                () => _studentId = student.id,
                                              ),
                                        child: const Text('اختيار'),
                                      ),
                                    ),
                                  ],
                                ),
                              )
                              .toList(),
                        ),
                ),
        ),
      ),
    );
  }
}
