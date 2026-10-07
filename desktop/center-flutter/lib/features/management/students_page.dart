import 'package:massar_center/domain/student_lookup.dart';
import 'package:massar_center/domain/discount_calculation.dart';
import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/student_editor_dialog.dart';
import 'package:massar_center/shared/document_service.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/workspace_draft_guard.dart';

import 'management_widgets.dart';
import 'student_profile_page.dart';
import '../attendance/paid_amount_dialog.dart';
import '../attendance/student_discount_dialog.dart';
import '../attendance/student_suspension_dialog.dart';
import '../attendance/entry_confirmation_dialog.dart'
    show EntryConfirmationDialog;

class StudentsPage extends StatefulWidget {
  const StudentsPage({super.key, required this.store, this.cairo = false});
  final CenterStore store;
  final bool cairo;

  @override
  State<StudentsPage> createState() => _StudentsPageState();
}

class _StudentsPageState extends State<StudentsPage> {
  final _search = TextEditingController();
  String? _groupId;
  bool _renewing = false, _statusChanging = false, _discountEditing = false;
  bool _editing = false, _printing = false;
  bool get _working =>
      _renewing || _statusChanging || _discountEditing || _editing || _printing;
  ValueNotifier<bool>? _pendingRenewal;

  void _openProfile(Student student) {
    if (_working) return;
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => StudentProfilePage(
          store: widget.store,
          studentId: student.id,
          readOnly: widget.cairo,
        ),
      ),
    );
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  Future<void> _edit([Student? student]) async {
    if (_working || !(widget.store.canManage || widget.store.canCollect)) {
      return;
    }
    setState(() => _editing = true);
    try {
      final route = DialogRoute<String>(
        context: context,
        builder: (_) => StudentEditorDialog(
          store: widget.store,
          student: student,
          initialGroupId: _groupId,
          cairo: widget.cairo,
        ),
      );
      await Navigator.of(context).push(route);
      await route.completed;
    } finally {
      if (mounted) setState(() => _editing = false);
    }
  }

  Future<void> _discount(Student student) async {
    if (_working || !widget.store.canEditDiscount) {
      return;
    }
    final group = widget.store.groups
        .where((g) => student.groupIds.contains(g.id))
        .firstOrNull;
    setState(() => _discountEditing = true);
    try {
      final route = DialogRoute<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => StudentDiscountDialog(
          store: widget.store,
          studentId: student.id,
          baseAmount: group?.sessionPrice ?? 0,
          priceLabel: 'الحصة',
          initialPriceId: group == null ? null : 'single',
          priceOptions: group == null
              ? const []
              : [
                  DiscountPriceOption(
                    id: 'single',
                    label: 'الحصة',
                    baseAmount: group.sessionPrice,
                  ),
                  for (final plan in group.effectiveMonthPlans)
                    DiscountPriceOption(
                      id: 'month-${plan.id}',
                      label: '${plan.name} · ${plan.sessions} حصص',
                      baseAmount: plan.price,
                    ),
                ],
        ),
      );
      await Navigator.of(context).push(route);
      await route.completed;
    } finally {
      if (mounted) setState(() => _discountEditing = false);
    }
  }

  Future<void> _print(Student student) async {
    if (_working) return;
    setState(() => _printing = true);
    try {
      await DocumentService.showStudentCard(context, widget.store, student);
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.students_page');
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _printing = false);
    }
  }

  Future<void> _renew(Student student) async {
    if (_working || student.isSuspended || !widget.store.canCollect) {
      return;
    }
    if (student.groupIds.isEmpty) {
      await showManagementMessage(
        context,
        'سجل الطالب في مجموعة أولًا.',
        kind: NoticeKind.warning,
      );
      return;
    }
    final blocked = ValueNotifier(false);
    _pendingRenewal = blocked;
    setState(() => _renewing = true);
    try {
      final initialGroupId = student.groupIds.contains(_groupId)
          ? _groupId!
          : student.groupIds.first;
      final plansByGroup = <String, List<GroupMonthPlan>>{};
      final options = student.groupIds.map((id) {
        final group = widget.store.groups.firstWhere((group) => group.id == id);
        plansByGroup[id] = List.unmodifiable(group.effectiveMonthPlans);
        return PaidAmountGroupOption(
          id: id,
          label: widget.store.groupLabel(id),
          prices: [
            for (
              var index = 0;
              index < group.effectiveMonthPlans.length;
              index++
            )
              PaidAmountPriceOption(
                id: index,
                label:
                    '${group.effectiveMonthPlans[index].name} · ${group.effectiveMonthPlans[index].sessions} حصص',
                dueAmount: discountedAmount(
                  group.effectiveMonthPlans[index].price,
                  student.discountPercent,
                ),
              ),
          ],
        );
      }).toList();
      final route = DialogRoute<PaidAmountSelection>(
        context: context,
        barrierDismissible: false,
        builder: (_) => PaidAmountDialog(
          title: 'تجديد الشهر',
          studentLabel: '${student.name} · ${student.code}',
          options: const [],
          groups: options,
          initialGroupId: initialGroupId,
          initialOptionId: 0,
          method: 'نقدي',
          paymentMethods: const ['نقدي', 'تحويل', 'محفظة'],
          description:
              'تضاف حصص الشهر كاملة دون إلغاء الرصيد السابق. المتبقي من المطلوب مديونية. لا يسجل هذا التجديد حضورًا.',
          scannerBlocked: blocked,
        ),
      );
      final payment = await Navigator.of(context).push(route);
      await route.completed;
      if (payment == null || !mounted || blocked.value) return;
      final plan = plansByGroup[payment.groupId]![payment.optionId];
      await widget.store.renewPackage(
        PackageRequest(
          studentId: student.id,
          groupId: payment.groupId!,
          method: payment.method,
          sessions: plan.sessions,
          monthPlanId: plan.id,
          expectedMonthPlan: plan,
          paidAmount: payment.paidAmount,
          expectedNetAmount: payment.dueAmount,
        ),
      );
      if (mounted) {
        await showManagementMessage(
          context,
          'سُجل ${plan.name} (${plan.sessions} حصص). المطلوب ${money(payment.dueAmount)} · المدفوع ${money(payment.collectedAmount)} · المديونية ${money(payment.dueAmount - payment.collectedAmount)}.',
        );
      }
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.students_page');
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      _pendingRenewal = null;
      blocked.dispose();
      if (mounted) setState(() => _renewing = false);
    }
  }

  Future<void> _changeStudentStatus(Student student) async {
    if (_working || !widget.store.canCollect) return;
    final blocked = ValueNotifier(false);
    if (student.isSuspended) _pendingRenewal = blocked;
    setState(() => _statusChanging = true);
    try {
      final route = DialogRoute<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => StudentSuspensionDialog(
          store: widget.store,
          student: student,
          action: student.isSuspended
              ? StudentSuspensionAction.reactivate
              : StudentSuspensionAction.suspend,
          scannerBlocked: student.isSuspended ? blocked : null,
        ),
      );
      await Navigator.of(context).push(route);
      await route.completed;
    } finally {
      _pendingRenewal = null;
      blocked.dispose();
      if (mounted) setState(() => _statusChanging = false);
    }
  }

  KeyEventResult _paymentKey(FocusNode node, KeyEvent event) {
    final blocked = _pendingRenewal;
    if (blocked == null) {
      return _statusChanging ? KeyEventResult.handled : KeyEventResult.ignored;
    }
    if (EntryConfirmationDialog.isScannerText(event)) blocked.value = true;
    return KeyEventResult.handled;
  }

  @override
  Widget build(BuildContext context) {
    final query = _search.text.trim().toLowerCase();
    final students = widget.store
        .studentsForRegion(cairo: widget.cairo)
        .where(
          (student) =>
              (_groupId == null || student.groupIds.contains(_groupId)) &&
              studentMatchesSearch(student, query),
        )
        .toList();
    return WorkspaceDraftRegistration(
      dirty: false,
      busy: _working,
      child: Focus(
        onKeyEvent: _paymentKey,
        child: ManagementPanel(
          title: widget.cairo ? 'طلاب القاهرة' : 'طلاب إسكندرية',
          subtitle:
              'ملف واحد لكل طالب، مع تسجيل المجموعات والخصم الدائم وكارت الدخول.',
          actions: [
            if (widget.store.canManage || widget.store.canCollect)
              FilledButton.icon(
                onPressed: _working || widget.store.groups.isEmpty
                    ? null
                    : _edit,
                icon: const Icon(Icons.person_add_alt_1),
                label: const Text('إضافة طالب'),
              ),
          ],
          child: ManagementBody(
            header: [
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _search,
                      readOnly: _working,
                      decoration: const InputDecoration(
                        labelText: 'بحث بالاسم أو الكود أو الباركود أو الهاتف',
                        prefixIcon: Icon(Icons.search),
                      ),
                      onChanged: (_) => setState(() {}),
                    ),
                  ),
                  const SizedBox(width: 16),
                  Expanded(
                    child: DropdownButtonFormField<String>(
                      initialValue: _groupId,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'المجموعة'),
                      items: [
                        const DropdownMenuItem(
                          value: null,
                          child: Text('كل المجموعات'),
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
                      onChanged: _working
                          ? null
                          : (id) => setState(() => _groupId = id),
                    ),
                  ),
                ],
              ),
              if (_working) const LinearProgressIndicator(minHeight: 3),
              const SizedBox(height: 20),
            ],
            child: students.isEmpty
                ? EmptySection(
                    message: widget.store.students.isEmpty
                        ? 'أضف المجموعات أولًا، ثم سجل أول طالب. لن تُضاف بيانات تجريبية.'
                        : 'لا يوجد طالب يطابق البحث أو المجموعة.',
                  )
                : ManagementTable.builder(
                    columns: [
                      'الكود',
                      'الطالب',
                      'الهاتف / ولي الأمر',
                      widget.cairo ? 'المجموعات' : 'المجموعات والرصيد',
                      if (!widget.cairo) 'الخصم',
                      'التوأم',
                      'الحالة',
                      'الإجراءات',
                    ],
                    rowCount: students.length,
                    rowBuilder: (index) {
                      final student = students[index];
                      return DataRow(
                        cells: [
                          DataCell(
                            Tooltip(
                              message: student.barcode.trim().isEmpty
                                  ? 'كود ${student.code}'
                                  : 'الباركود: ${student.barcode}',
                              child: Text(student.code),
                            ),
                            onTap: _working
                                ? null
                                : () => _openProfile(student),
                          ),
                          DataCell(
                            TextButton(
                              onPressed: _working
                                  ? null
                                  : () => _openProfile(student),
                              child: Text(student.name),
                            ),
                          ),
                          DataCell(
                            Text(
                              '${student.phone.isEmpty ? '—' : student.phone}\n${student.guardianPhone.isEmpty ? '—' : student.guardianPhone}',
                            ),
                          ),
                          DataCell(
                            SizedBox(
                              width: 230,
                              child: Text(
                                student.groupIds
                                    .map(
                                      (id) => widget.cairo
                                          ? widget.store.groupLabel(id)
                                          : '${widget.store.groupLabel(id)}: ${widget.store.remainingFor(student.id, id)} حصص',
                                    )
                                    .join('\n'),
                                maxLines: 3,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          ),
                          if (!widget.cairo)
                            DataCell(
                              Text('${percentText(student.discountPercent)}٪'),
                            ),
                          DataCell(Text(_twinLabel(student))),
                          DataCell(
                            Tooltip(
                              message: student.isSuspended
                                  ? student.suspensionReason
                                  : 'الطالب فعال',
                              child: Text(
                                student.isSuspended ? 'مُصفّى' : 'فعال',
                                style: TextStyle(
                                  color: student.isSuspended
                                      ? Theme.of(context).colorScheme.error
                                      : null,
                                ),
                              ),
                            ),
                          ),
                          DataCell(
                            Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                IconButton(
                                  tooltip: 'فتح ملف الطالب',
                                  onPressed: _working
                                      ? null
                                      : () => _openProfile(student),
                                  icon: const Icon(Icons.account_box_outlined),
                                ),
                                IconButton(
                                  tooltip: 'تعديل الملف والمجموعات',
                                  onPressed:
                                      !_working &&
                                          (widget.store.canManage ||
                                              widget.store.canCollect)
                                      ? () => _edit(student)
                                      : null,
                                  icon: const Icon(Icons.edit_outlined),
                                ),
                                if (!widget.cairo &&
                                    widget.store.canEditDiscount)
                                  IconButton(
                                    tooltip: 'خصم بالمبلغ أو النسبة',
                                    onPressed: _working
                                        ? null
                                        : () => _discount(student),
                                    icon: const Icon(Icons.percent),
                                  ),
                                IconButton(
                                  tooltip: 'كارت الطالب',
                                  onPressed: _working
                                      ? null
                                      : () => _print(student),
                                  icon: const Icon(Icons.qr_code_2),
                                ),
                                if (!widget.cairo)
                                  IconButton(
                                    tooltip: 'تجديد الشهر',
                                    onPressed:
                                        widget.store.canCollect &&
                                            !_working &&
                                            !student.isSuspended
                                        ? () => _renew(student)
                                        : null,
                                    icon: const Icon(Icons.add_card),
                                  ),
                                if (widget.store.canCollect)
                                  IconButton(
                                    tooltip: student.isSuspended
                                        ? 'إعادة تفعيل الطالب'
                                        : 'تصفية الطالب',
                                    onPressed: _working
                                        ? null
                                        : () => _changeStudentStatus(student),
                                    icon: Icon(
                                      student.isSuspended
                                          ? Icons.person_add_alt_1
                                          : Icons.person_off_outlined,
                                    ),
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
      ),
    );
  }

  String _twinLabel(Student student) {
    final twin = widget.store.twinFor(student.id);
    return twin == null
        ? 'لا يوجد'
        : '${twin.name} · ${twin.code}${twin.discountPercent == 100 ? ' — معفى ١٠٠٪' : ''}';
  }
}
