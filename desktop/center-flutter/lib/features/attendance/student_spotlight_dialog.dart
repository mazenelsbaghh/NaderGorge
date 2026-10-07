import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../domain/student_lookup.dart';
import '../management/student_profile_page.dart';

class StudentSpotlightSelection {
  const StudentSpotlightSelection(this.student, {required this.register});
  final Student student;
  final bool register;
}

class StudentSpotlightDialog extends StatefulWidget {
  const StudentSpotlightDialog({
    super.key,
    required this.store,
    required this.canRegister,
  });
  final CenterStore store;
  final bool canRegister;
  @override
  State<StudentSpotlightDialog> createState() => _StudentSpotlightDialogState();
}

class _StudentSpotlightDialogState extends State<StudentSpotlightDialog> {
  final _query = TextEditingController();
  final _focus = FocusNode();
  List<Student> _matches = [];
  String? _selectedId;
  bool _finished = false;
  Student? get _selected =>
      widget.store.students.where((s) => s.id == _selectedId).firstOrNull;

  @override
  void initState() {
    super.initState();
    _focus.onKeyEvent = (_, event) {
      if (event.logicalKey == LogicalKeyboardKey.arrowDown ||
          event.logicalKey == LogicalKeyboardKey.arrowUp) {
        if (event is KeyDownEvent && _matches.isNotEmpty) {
          final old = _matches.indexWhere((s) => s.id == _selectedId);
          final next = old < 0
              ? 0
              : (old +
                        (event.logicalKey == LogicalKeyboardKey.arrowDown
                            ? 1
                            : -1))
                    .clamp(0, _matches.length - 1);
          setState(() => _selectedId = _matches[next].id);
        }
        return KeyEventResult.handled;
      }
      if (event.logicalKey == LogicalKeyboardKey.enter ||
          event.logicalKey == LogicalKeyboardKey.numpadEnter) {
        if (event is KeyDownEvent) _finish(widget.canRegister);
        return KeyEventResult.handled;
      }
      return KeyEventResult.ignored;
    };
  }

  @override
  void dispose() {
    _query.dispose();
    _focus.dispose();
    super.dispose();
  }

  void _search(String query) {
    setState(() {
      _matches = query.trim().isEmpty
          ? []
          : studentLookupCandidates(widget.store.studentsForRegion(), query);
      // A shared phone or partial name never silently selects the first student.
      _selectedId = _matches.length == 1 ? _matches.single.id : null;
    });
  }

  void _finish(bool register) {
    final student = _selected;
    if (_finished ||
        student == null ||
        ModalRoute.of(context)?.isCurrent != true) {
      return;
    }
    _finished = true;
    Navigator.pop(
      context,
      StudentSpotlightSelection(student, register: register),
    );
  }

  Widget _results() => ListView.builder(
    itemCount: _matches.length,
    itemBuilder: (context, index) {
      final student = _matches[index];
      return ListTile(
        key: ValueKey('spotlight-result-${student.id}'),
        selected: student.id == _selectedId,
        leading: const Icon(Icons.person_outline),
        title: Text(student.name),
        subtitle: Text(
          '${student.code} · ${student.phone}\nولي الأمر: ${student.guardianPhone}',
        ),
        onTap: () {
          setState(() => _selectedId = student.id);
          _focus.requestFocus();
        },
      );
    },
  );

  @override
  Widget build(BuildContext context) => Directionality(
    textDirection: TextDirection.rtl,
    child: Dialog(
      insetPadding: const EdgeInsets.all(20),
      clipBehavior: Clip.antiAlias,
      child: SizedBox(
        width: 1180,
        height: MediaQuery.sizeOf(context).height * .86,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      key: const Key('student-spotlight-query'),
                      controller: _query,
                      focusNode: _focus,
                      autofocus: true,
                      decoration: const InputDecoration(
                        prefixIcon: Icon(Icons.search),
                        hintText:
                            'ابحث بالاسم أو رقم الطالب أو ولي الأمر أو الكود أو الباركود',
                      ),
                      onChanged: _search,
                    ),
                  ),
                  IconButton(
                    tooltip: 'إغلاق · Esc',
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
            ),
            Text(
              _matches.isEmpty
                  ? (_query.text.trim().isEmpty
                        ? 'اكتب بيانات الطالب للبحث'
                        : 'لا توجد نتائج مطابقة؛ جرّب الاسم أو رقمًا آخر')
                  : '${_matches.length} نتيجة · اختار الطالب بالماوس أو الأسهم',
            ),
            const Divider(),
            Expanded(
              child: _matches.isEmpty
                  ? const Center(child: Text('النتائج وملف الطالب هيظهروا هنا'))
                  : LayoutBuilder(
                      builder: (context, constraints) {
                        final preview = _selectedId == null
                            ? const Center(
                                child: Text(
                                  'اختر الطالب لعرض بياناته؛ Enter لا يحضّر اسمًا غير محدد.',
                                ),
                              )
                            : StudentProfilePage(
                                key: ValueKey(_selectedId),
                                store: widget.store,
                                studentId: _selectedId!,
                                embedded: true,
                                readOnly: true,
                              );
                        if (constraints.maxWidth < 760) {
                          return Column(
                            children: [
                              SizedBox(height: 150, child: _results()),
                              const Divider(),
                              Expanded(child: preview),
                            ],
                          );
                        }
                        return Row(
                          children: [
                            SizedBox(width: 330, child: _results()),
                            const VerticalDivider(width: 1),
                            Expanded(child: preview),
                          ],
                        );
                      },
                    ),
            ),
            const Divider(height: 1),
            Padding(
              padding: const EdgeInsets.all(12),
              child: Wrap(
                spacing: 12,
                runSpacing: 8,
                children: [
                  FilledButton.icon(
                    key: const Key('spotlight-register'),
                    onPressed: widget.canRegister && _selected != null
                        ? () => _finish(true)
                        : null,
                    icon: const Icon(Icons.how_to_reg),
                    label: const Text('تسجيل الحضور · Enter'),
                  ),
                  OutlinedButton(
                    onPressed: _selected == null ? null : () => _finish(false),
                    child: const Text('عرض الطالب فقط'),
                  ),
                  if (!widget.canRegister)
                    const Text('لبدء التحضير اختار حصة مفتوحة وابدأها.'),
                ],
              ),
            ),
          ],
        ),
      ),
    ),
  );
}
