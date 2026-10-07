import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../domain/models.dart';
import 'scrollable_dialog.dart';

/// No default student: only an explicit selection resolves an ambiguous scan.
Future<Student?> chooseMatchingStudent(
  BuildContext context,
  List<Student> students,
) async {
  bool finished = false;
  void finish(BuildContext context, [Student? student]) {
    if (finished) {
      return;
    }
    finished = true;
    Navigator.pop(context, student);
  }

  final route = RawDialogRoute<Student>(
    barrierDismissible: false,
    barrierLabel: 'اختيار الطالب',
    pageBuilder: (context, animation, secondaryAnimation) => Directionality(
      textDirection: TextDirection.rtl,
      child: Focus(
        autofocus: true,
        descendantsAreFocusable: false,
        onKeyEvent: (node, event) {
          if (event.logicalKey == LogicalKeyboardKey.escape) {
            if (event is KeyDownEvent) {
              finish(context);
            }
            return KeyEventResult.handled;
          }
          if (event.logicalKey == LogicalKeyboardKey.enter ||
              event.logicalKey == LogicalKeyboardKey.numpadEnter ||
              (event.character?.isNotEmpty ?? false)) {
            return KeyEventResult.handled;
          }
          return KeyEventResult.ignored;
        },
        child: ScrollableMassarDialog(
          key: const Key('student-lookup-choice'),
          title: const Text('اختر الطالب'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'البحث يطابق أكثر من طالب. راجع الاسم والباركود الكامل ثم اختر الطالب المقصود.',
              ),
              const SizedBox(height: 12),
              for (final student in students)
                ListTile(
                  key: ValueKey('student-lookup-${student.id}'),
                  title: Text(student.name),
                  subtitle: Text(
                    'الكود: ${student.code}'
                    '${student.barcode.trim().isEmpty ? '' : '\nالباركود: ${student.barcode}'}'
                    '${student.phone.isEmpty ? '' : '\nالهاتف: ${student.phone}'}',
                  ),
                  onTap: () => finish(context, student),
                ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => finish(context),
              child: const Text('إلغاء'),
            ),
          ],
        ),
      ),
    ),
  );
  final student = await Navigator.of(context).push(route);
  await route.completed;
  return student;
}
