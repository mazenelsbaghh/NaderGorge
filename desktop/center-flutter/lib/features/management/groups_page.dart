import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';

import 'management_widgets.dart';
import 'study_month_editor_dialog.dart';

class GroupsPage extends StatefulWidget {
  const GroupsPage({super.key, required this.store, this.cairo = false});
  final CenterStore store;
  final bool cairo;

  @override
  State<GroupsPage> createState() => _GroupsPageState();
}

class _GroupsPageState extends State<GroupsPage> {
  CenterStore get store => widget.store;
  List<StudyGroup> get _groups => store.groupsForRegion(cairo: widget.cairo);
  bool _editing = false;

  bool get _catalogsReady => CatalogKind.values.every(
    (kind) => store.catalogs.any((entry) => entry.kind == kind),
  );

  List<GroupMonthPlan> _sharedPlans(StudyGroup? group) => [
    for (final month in store.studyMonths)
      GroupMonthPlan(
        id: month.id,
        name: month.name,
        sessions: month.lessons.length,
        price:
            group?.effectiveMonthPlans
                .where((plan) => plan.id == month.id)
                .firstOrNull
                ?.price ??
            month.price,
      ),
  ];

  List<GroupMonthPlan> _savedPlans(
    StudyGroup? latest,
    List<_MonthPriceDraft> drafts,
  ) {
    final sharedIds = store.studyMonths.map((month) => month.id).toSet();
    return [
      ...?latest?.effectiveMonthPlans.where(
        (plan) => !sharedIds.contains(plan.id),
      ),
      for (final plan in _sharedPlans(latest))
        plan.copyWith(
          price:
              drafts
                  .where((draft) => draft.plan.id == plan.id && draft.edited)
                  .firstOrNull
                  ?.savedPrice ??
              plan.price,
        ),
    ];
  }

  List<Widget> _monthPrices(List<_MonthPriceDraft> drafts) => [
    const Text(
      'الشهور وحصصها مشتركة بين المجموعات. يمكنك تخصيص السعر لهذه المجموعة فقط؛ اسم الشهر وعدد الحصص يُعدّلان من إدارة الشهور المشتركة. المدفوعات القديمة تظل محفوظة.',
    ),
    if (drafts.isEmpty)
      const Text(
        'لا توجد شهور مشتركة. أضف شهرًا وحصصه من أعلى صفحة المجموعات.',
      ),
    for (final draft in drafts) ...[
      const Divider(),
      Text(
        '${draft.plan.name} · ${draft.plan.sessions} حصص',
        style: const TextStyle(fontWeight: FontWeight.bold),
      ),
      TextFormField(
        key: ValueKey('group-month-price-${draft.plan.id}'),
        controller: draft.price,
        keyboardType: const TextInputType.numberWithOptions(decimal: true),
        decoration: const InputDecoration(
          labelText: 'سعر الشهر لهذه المجموعة بالجنيه',
        ),
        validator: validPrice,
        onChanged: (_) => draft.edited = true,
      ),
    ],
  ];

  StudyGroup? _latestGroup(StudyGroup? group) {
    if (group == null) return null;
    final latest = store.groups
        .where((entry) => entry.id == group.id)
        .firstOrNull;
    if (latest == null) {
      throw const CenterException(
        'المجموعة لم تعد موجودة؛ افتح القائمة من جديد.',
      );
    }
    return latest;
  }

  Future<void> _edit(BuildContext context, [StudyGroup? group]) async {
    if (_editing || !store.canManage) return;
    setState(() => _editing = true);
    final name = TextEditingController(text: group?.name);
    final schedule = TextEditingController(text: group?.schedule);
    final sessionPrice = TextEditingController(
      text: priceText(group?.sessionPrice ?? 0),
    );
    final prices = _sharedPlans(group).map(_MonthPriceDraft.new).toList();
    final associationsLocked =
        group != null &&
        (store.sessions.any((session) => session.groupId == group.id) ||
            store.allPayments.any((payment) => payment.groupId == group.id) ||
            store.allAttendances.any(
              (entry) => entry.makeupSourceGroupId == group.id,
            ));
    String? subjectId = group?.subjectId,
        centerId = group?.centerId,
        gradeId = group?.gradeId;
    Widget catalogField(
      CatalogKind kind,
      String label,
      String? selected,
      ValueChanged<String?> select,
    ) => DropdownButtonFormField<String>(
      initialValue: selected,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: store.catalogs
          .where(
            (entry) =>
                entry.kind == kind &&
                (kind != CatalogKind.center || entry.isCairo == widget.cairo),
          )
          .map(
            (entry) =>
                DropdownMenuItem(value: entry.id, child: Text(entry.name)),
          )
          .toList(),
      onChanged: associationsLocked ? null : select,
      validator: (id) => id == null ? 'اختر $label' : null,
    );
    try {
      await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => ManagementEditor(
          controllers: [
            name,
            schedule,
            sessionPrice,
            ...prices.map((draft) => draft.price),
          ],
          title: group == null ? 'مجموعة جديدة' : 'تعديل المجموعة',
          saveLabel: group == null ? 'إضافة مجموعة' : 'حفظ التعديلات',
          fields: [
            TextFormField(
              controller: name,
              autofocus: true,
              decoration: const InputDecoration(
                labelText: 'اسم أو رقم المجموعة',
              ),
              validator: requiredText,
            ),
            catalogField(
              CatalogKind.subject,
              'المادة',
              subjectId,
              (id) => subjectId = id,
            ),
            catalogField(
              CatalogKind.center,
              'السنتر',
              centerId,
              (id) => centerId = id,
            ),
            catalogField(
              CatalogKind.grade,
              'الصف الدراسي',
              gradeId,
              (id) => gradeId = id,
            ),
            if (associationsLocked)
              const Text(
                'المادة والسنتر والصف ثابتة بعد إنشاء حصص أو مدفوعات أو تعويض للمجموعة. أنشئ مجموعة جديدة لتغييرها.',
              ),
            TextFormField(
              controller: schedule,
              decoration: const InputDecoration(
                labelText: 'المواعيد',
                hintText: 'الأحد والأربعاء — ٥ مساءً',
              ),
              validator: requiredText,
            ),
            if (!widget.cairo)
              TextFormField(
                controller: sessionPrice,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: const InputDecoration(
                  labelText: 'سعر الحصة بالجنيه',
                ),
                validator: validPrice,
              ),
            if (!widget.cairo) ..._monthPrices(prices),
          ],
          onSave: () {
            final latest = _latestGroup(group);
            return store.saveGroup(
              StudyGroup(
                id: latest?.id ?? '',
                name: name.text.trim(),
                subjectId: subjectId!,
                centerId: centerId!,
                gradeId: gradeId!,
                schedule: schedule.text.trim(),
                sessionPrice: piastresFromText(sessionPrice.text)!,
                twoSessionPrice: latest?.twoSessionPrice,
                threeSessionPrice: latest?.threeSessionPrice,
                packagePrice: latest?.packagePrice ?? 21000,
                monthPlans: _savedPlans(latest, prices),
              ),
            );
          },
        ),
      );
    } finally {
      if (mounted) setState(() => _editing = false);
    }
  }

  Future<void> _editPrices(StudyGroup group) async {
    if (_editing || !store.canManage) return;
    setState(() => _editing = true);
    final drafts = _sharedPlans(group).map(_MonthPriceDraft.new).toList();
    try {
      await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) => ManagementEditor(
          title: 'أسعار الشهور — ${group.name}',
          saveLabel: 'حفظ الأسعار',
          controllers: drafts.map((draft) => draft.price).toList(),
          fields: _monthPrices(drafts),
          onSave: () {
            final latest = _latestGroup(group)!;
            return store.saveGroup(
              latest.copyWith(monthPlans: _savedPlans(latest, drafts)),
            );
          },
        ),
      );
    } finally {
      if (mounted) setState(() => _editing = false);
    }
  }

  Future<void> _sharedMonthEditor([StudyMonth? month]) => showDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder: (_) => StudyMonthEditorDialog(store: store, month: month),
  );

  Future<void> _addSharedMonth() async {
    if (_editing || !store.canManage) return;
    setState(() => _editing = true);
    try {
      await _sharedMonthEditor();
    } finally {
      if (mounted) setState(() => _editing = false);
    }
  }

  Future<void> _manageSharedMonths() async {
    if (_editing || !store.canManage) return;
    setState(() => _editing = true);
    try {
      await showDialog<void>(
        context: context,
        builder: (dialogContext) => StatefulBuilder(
          builder: (_, refresh) => ScrollableMassarDialog(
            title: const Text('الشهور والحصص المشتركة'),
            content: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'جهّز الشهر وحصصه مرة واحدة لكل المجموعات. المجموعة ترتبط بالحصة عند بدئها؛ سعر المجموعة يمكن تخصيصه من صف المجموعة.',
                ),
                const SizedBox(height: 12),
                if (store.studyMonths.isEmpty) const Text('أضف أول شهر وحصصه.'),
                for (final month in store.studyMonths)
                  ListTile(
                    title: Text(month.name),
                    subtitle: Text(
                      '${month.lessons.length} حصص · السعر العام ${money(month.price)}',
                    ),
                    trailing: const Icon(Icons.edit_outlined),
                    onTap: () async {
                      await _sharedMonthEditor(month);
                      if (dialogContext.mounted) refresh(() {});
                    },
                  ),
              ],
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(dialogContext),
                child: const Text('إغلاق'),
              ),
              FilledButton.icon(
                onPressed: () async {
                  await _sharedMonthEditor();
                  if (dialogContext.mounted) refresh(() {});
                },
                icon: const Icon(Icons.add),
                label: const Text('إضافة شهر وحصصه'),
              ),
            ],
          ),
        ),
      );
    } finally {
      if (mounted) setState(() => _editing = false);
    }
  }

  String _monthLabels(StudyGroup group) => _sharedPlans(group)
      .map(
        (plan) => '${plan.name} · ${plan.sessions} حصص · ${money(plan.price)}',
      )
      .join('\n');

  @override
  Widget build(BuildContext context) => ManagementPanel(
    title: widget.cairo ? 'مجموعات القاهرة' : 'مجموعات إسكندرية',
    subtitle: widget.cairo
        ? 'مجموعات القاهرة للرصد والتحضير بالدرجة، بدون تحصيل.'
        : 'كل مجموعة مرتبطة بمادة وسنتر وصف دراسي. الشهور وحصصها مشتركة، ويمكن تخصيص سعر المجموعة.',
    actions: [
      if (store.canManage)
        FilledButton.icon(
          onPressed: _catalogsReady && !_editing ? () => _edit(context) : null,
          icon: const Icon(Icons.add),
          label: const Text('مجموعة جديدة'),
        ),
      if (store.canManage)
        OutlinedButton.icon(
          key: const Key('manage-billing-months'),
          onPressed: _editing ? null : _manageSharedMonths,
          icon: const Icon(Icons.edit_calendar_outlined),
          label: const Text('الشهور والحصص المشتركة'),
        ),
      if (store.canManage)
        OutlinedButton.icon(
          key: const Key('add-month-all-groups'),
          onPressed: _editing ? null : _addSharedMonth,
          icon: const Icon(Icons.calendar_month_outlined),
          label: const Text('إضافة شهر وحصصه'),
        ),
    ],
    child: _groups.isEmpty
        ? EmptySection(
            message: _catalogsReady
                ? 'أنشئ أول مجموعة، ثم سجل الطلبة وابدأ الحصة المشتركة.'
                : 'أضف مادة وسنترًا وصفًا دراسيًا من «أساس النظام» لإنشاء مجموعة.',
          )
        : ManagementTable(
            columns: [
              'المجموعة',
              'المادة / السنتر / الصف',
              'المواعيد',
              'عدد الطلاب',
              if (!widget.cairo) 'الحصة',
              if (!widget.cairo) 'الشهور المشتركة',
              'الإجراء',
            ],
            rows: _groups
                .map(
                  (group) => DataRow(
                    cells: [
                      DataCell(Text(group.name)),
                      DataCell(
                        Text(
                          '${store.catalogName(group.subjectId)}\n${store.catalogName(group.centerId)} · ${store.catalogName(group.gradeId)}',
                        ),
                      ),
                      DataCell(Text(group.schedule)),
                      DataCell(
                        Text(
                          '${store.students.where((student) => student.groupIds.contains(group.id)).length}',
                        ),
                      ),
                      if (!widget.cairo)
                        DataCell(Text(money(group.sessionPrice))),
                      if (!widget.cairo)
                        DataCell(
                          Tooltip(
                            message: _monthLabels(group),
                            child: InkWell(
                              onTap: store.canManage && !_editing
                                  ? () => _editPrices(group)
                                  : null,
                              child: Text(
                                store.studyMonths.isEmpty
                                    ? 'لم يُجهز شهر بعد'
                                    : _monthLabels(group),
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                              ),
                            ),
                          ),
                        ),
                      DataCell(
                        IconButton(
                          tooltip: 'تعديل المجموعة',
                          onPressed: store.canManage && !_editing
                              ? () => _edit(context, group)
                              : null,
                          icon: const Icon(Icons.edit_outlined),
                        ),
                      ),
                    ],
                  ),
                )
                .toList(),
          ),
  );
}

class _MonthPriceDraft {
  _MonthPriceDraft(this.plan)
    : price = TextEditingController(text: priceText(plan.price));
  final GroupMonthPlan plan;
  final TextEditingController price;
  bool edited = false;
  int get savedPrice => piastresFromText(price.text)!;
}
