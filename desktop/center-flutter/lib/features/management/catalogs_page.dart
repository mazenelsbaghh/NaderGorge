import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

import 'management_widgets.dart';

class CatalogsPage extends StatefulWidget {
  const CatalogsPage({super.key, required this.store, this.cairo = false});
  final CenterStore store;
  final bool cairo;

  @override
  State<CatalogsPage> createState() => _CatalogsPageState();
}

class _CatalogsPageState extends State<CatalogsPage> {
  CatalogKind _kind = CatalogKind.subject;
  @override
  void initState() {
    super.initState();
    if (widget.cairo) _kind = CatalogKind.center;
  }

  String _label(CatalogKind kind) => switch (kind) {
    CatalogKind.subject => 'المواد',
    CatalogKind.center => 'السناتر',
    CatalogKind.grade => 'الصفوف الدراسية',
  };

  String _itemLabel(CatalogKind kind) => switch (kind) {
    CatalogKind.subject => 'مادة',
    CatalogKind.center => 'سنتر',
    CatalogKind.grade => 'صف دراسي',
  };

  Future<void> _edit([CatalogEntry? entry]) async {
    final name = TextEditingController(text: entry?.name);
    await showDialog<bool>(
      context: context,
      builder: (context) => ManagementEditor(
        controllers: [name],
        title: '${entry == null ? 'إضافة' : 'تعديل'} ${_itemLabel(_kind)}',
        saveLabel: entry == null
            ? 'إضافة ${_itemLabel(_kind)}'
            : 'حفظ التعديلات',
        fields: [
          TextFormField(
            controller: name,
            autofocus: true,
            decoration: const InputDecoration(labelText: 'الاسم'),
            validator: requiredText,
          ),
        ],
        onSave: () => widget.store.saveCatalog(
          CatalogEntry(
            id: entry?.id ?? '',
            name: name.text.trim(),
            kind: _kind,
            region: widget.cairo ? 'cairo' : 'alexandria',
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final entries = widget.store.catalogs
        .where(
          (entry) =>
              entry.kind == _kind &&
              (_kind != CatalogKind.center || entry.isCairo == widget.cairo),
        )
        .toList();
    return ManagementPanel(
      title: widget.cairo ? 'سناتر القاهرة' : 'أساس النظام — إسكندرية',
      subtitle: 'أضف المواد والسناتر والصفوف، ثم اربطها بالمجموعات.',
      actions: [
        if (widget.store.canManage)
          FilledButton.icon(
            onPressed: _edit,
            icon: const Icon(Icons.add),
            label: const Text('إضافة'),
          ),
      ],
      child: ManagementBody(
        header: [
          Wrap(
            spacing: 8,
            children: CatalogKind.values
                .map(
                  (kind) => ChoiceChip(
                    label: Text(_label(kind)),
                    selected: _kind == kind,
                    onSelected: (_) => setState(() => _kind = kind),
                  ),
                )
                .toList(),
          ),
          const SizedBox(height: 20),
        ],
        child: entries.isEmpty
            ? EmptySection(
                message:
                    'لم تُضف ${_label(_kind)} بعد. ابدأ بإضافة الأسماء المستخدمة في السنتر.',
              )
            : ManagementTable(
                columns: const ['الاسم', 'الإجراء'],
                rows: entries
                    .map(
                      (entry) => DataRow(
                        cells: [
                          DataCell(Text(entry.name)),
                          DataCell(
                            TextButton.icon(
                              onPressed: widget.store.canManage
                                  ? () => _edit(entry)
                                  : null,
                              icon: const Icon(Icons.edit_outlined, size: 18),
                              label: const Text('تعديل'),
                            ),
                          ),
                        ],
                      ),
                    )
                    .toList(),
              ),
      ),
    );
  }
}
