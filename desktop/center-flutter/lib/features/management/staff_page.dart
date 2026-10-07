import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

import 'management_widgets.dart';

class StaffPage extends StatelessWidget {
  const StaffPage({super.key, required this.store});
  final CenterStore store;

  String _role(StaffRole role) => switch (role) {
    StaffRole.admin => 'إدارة',
    StaffRole.cashier => 'استقبال وتحصيل',
    StaffRole.assistant => 'مساعد أكاديمي',
  };

  Future<void> _add(BuildContext context) async {
    final name = TextEditingController();
    final password = TextEditingController();
    var role = StaffRole.cashier;
    await showDialog<bool>(
      context: context,
      builder: (context) => ManagementEditor(
        controllers: [name, password],
        title: 'إضافة موظف',
        saveLabel: 'إضافة موظف',
        fields: [
          TextFormField(
            controller: name,
            autofocus: true,
            decoration: const InputDecoration(labelText: 'اسم الدخول'),
            validator: requiredText,
          ),
          TextFormField(
            controller: password,
            obscureText: true,
            enableSuggestions: false,
            autocorrect: false,
            decoration: const InputDecoration(labelText: 'كلمة المرور'),
            validator: (text) => text == null || text.length < 8
                ? 'استخدم ٨ أحرف على الأقل'
                : null,
          ),
          DropdownButtonFormField<StaffRole>(
            initialValue: role,
            decoration: const InputDecoration(labelText: 'الصلاحية'),
            items: StaffRole.values
                .map(
                  (role) =>
                      DropdownMenuItem(value: role, child: Text(_role(role))),
                )
                .toList(),
            onChanged: (selected) => role = selected!,
          ),
          const Text(
            'الإدارة: كل العمليات. الاستقبال: تسجيل الطلبة والتحضير والتحصيل. المساعد: الرصد الأكاديمي.',
          ),
        ],
        onSave: () => store.saveStaff(
          name: name.text.trim(),
          password: password.text,
          role: role,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => ManagementPanel(
    title: 'الموظفون',
    subtitle: 'حسابات محلية على الجهاز، وكل عملية تُسجّل باسم الموظف.',
    actions: [
      FilledButton.icon(
        onPressed: () => _add(context),
        icon: const Icon(Icons.person_add_alt),
        label: const Text('إضافة موظف'),
      ),
    ],
    child: ManagementTable(
      columns: const ['اسم الدخول', 'الصلاحية'],
      rows: store.staff
          .map(
            (staff) => DataRow(
              cells: [
                DataCell(Text(staff.name)),
                DataCell(
                  Text(
                    staff.name == store.installationAdminName
                        ? '${_role(staff.role)} · حساب التثبيت'
                        : _role(staff.role),
                  ),
                ),
              ],
            ),
          )
          .toList(),
    ),
  );
}
