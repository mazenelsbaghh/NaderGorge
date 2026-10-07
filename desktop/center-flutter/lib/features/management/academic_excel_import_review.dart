part of 'academic_excel_import_dialog.dart';

class _RowReviewDialog extends StatefulWidget {
  const _RowReviewDialog({
    required this.row,
    required this.students,
    required this.existing,
    required this.warnings,
    required this.destination,
  });
  final _ReviewedRow row;
  final List<Student> students;
  final Map<String, AcademicRecord> existing;
  final List<String> warnings;
  final String destination;
  @override
  State<_RowReviewDialog> createState() => _RowReviewDialogState();
}

class _RowReviewDialogState extends State<_RowReviewDialog> {
  late String? _studentId = widget.row.studentId;
  late bool _ignored = widget.row.ignored,
      _replace = widget.row.replace,
      _reviewed = widget.row.sourceReviewed;
  Future<void> _chooseStudent() async {
    final student = await showDialog<Student>(
      context: context,
      builder: (context) => _GroupStudentPicker(students: widget.students),
    );
    if (mounted && student != null) {
      setState(() {
        _studentId = student.id;
        _replace = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final row = widget.row.match.row;
    final student = widget.students
        .where((student) => student.id == _studentId)
        .firstOrNull;
    final previous = widget.existing[_studentId];
    return ScrollableMassarDialog(
      width: 800,
      title: Text('مراجعة صف ${row.rowNumber}'),
      content: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('الوجهة: ${widget.destination}'),
          Text('المصدر: ${row.name} · كود ${row.code} · ${row.phone}'),
          Text('الدرجة: ${row.score ?? '—'} / ${row.maxScore ?? '—'}'),
          for (final issue in [
            ...row.errors,
            ...widget.row.match.issues,
            ...widget.warnings,
          ])
            Padding(padding: const EdgeInsets.only(top: 6), child: Text(issue)),
          const SizedBox(height: 12),
          Text(
            student == null
                ? 'لم يتم اختيار طالب.'
                : 'طالب السنتر: ${student.name} · كود ${student.code} · ${student.phone}',
            key: const Key('import-selected-student'),
          ),
          OutlinedButton.icon(
            key: const Key('import-manual-student'),
            onPressed: _chooseStudent,
            icon: const Icon(Icons.person_search_outlined),
            label: const Text('اختيار طالب من المجموعة'),
          ),
          if (widget.row.match.suggestions.isNotEmpty) ...[
            const Text('أقرب اقتراحات — لا تُعتمد إلا باختيارك:'),
            for (final suggestion in widget.row.match.suggestions)
              ListTile(
                key: ValueKey('import-suggestion-${suggestion.student.id}'),
                title: Text(
                  '${suggestion.student.name} · ${suggestion.student.code}',
                ),
                subtitle: Text(
                  '${suggestion.reason} · ${suggestion.student.phone}',
                ),
                onTap: () => setState(() {
                  _studentId = suggestion.student.id;
                  _replace = false;
                }),
              ),
          ],
          if (previous != null) ...[
            Text(
              'المحفوظ سابقًا: ${previous.examAbsent ? 'غائب' : '${previous.score ?? '—'} / ${previous.maxScore}'}',
            ),
            CheckboxListTile(
              key: const Key('import-replace-existing'),
              contentPadding: EdgeInsets.zero,
              title: const Text(
                'أوافق على استبدال الرصد السابق لهذا الطالب بهذه الدرجة',
              ),
              value: _replace,
              onChanged: (value) => setState(() => _replace = value ?? false),
            ),
          ],
          if (widget.warnings.isNotEmpty)
            CheckboxListTile(
              key: const Key('import-source-reviewed'),
              contentPadding: EdgeInsets.zero,
              title: const Text(
                'راجعت تحذيرات المصدر وأؤكد الرصد في المجموعة والامتحان المختارين',
              ),
              value: _reviewed,
              onChanged: (value) => setState(() => _reviewed = value ?? false),
            ),
          CheckboxListTile(
            key: const Key('import-ignore-row'),
            contentPadding: EdgeInsets.zero,
            title: const Text('تجاهل هذا الصف من الاستيراد'),
            value: _ignored,
            onChanged: (value) => setState(() => _ignored = value ?? false),
          ),
          const Divider(),
          Text(
            'بيانات المصدر كاملة A:S',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          for (var index = 0; index < row.cells.length; index++)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 4),
              child: SelectableText(
                '${String.fromCharCode(65 + index)} · ${_columnLabels[index]}: ${row.cells[index].isEmpty ? '—' : row.cells[index]}',
              ),
            ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('رجوع'),
        ),
        FilledButton(
          key: const Key('import-apply-review'),
          onPressed: () => Navigator.pop(context, (
            studentId: _studentId,
            ignored: _ignored,
            replace: _replace,
            sourceReviewed: _reviewed,
          )),
          child: const Text('اعتماد المراجعة'),
        ),
      ],
    );
  }
}

const _columnLabels = [
  'اسم الطالب',
  'الهاتف',
  'الكود',
  'الصف الدراسي',
  'السنتر',
  'المجموعة',
  'الحصة',
  'الامتحان',
  'تاريخ القاهرة',
  'درجة الطالب',
  'الدرجة النهائية',
  'النسبة',
  'المتبقي للتصحيح',
  'الحالة',
  'وصف الدرجة',
  'سبب الإلغاء',
  'معرّف الحصة بالمصدر',
  'معرّف المحاولة بالمصدر',
  'نسخة الورقة',
];

class _GroupStudentPicker extends StatefulWidget {
  const _GroupStudentPicker({required this.students});
  final List<Student> students;
  @override
  State<_GroupStudentPicker> createState() => _GroupStudentPickerState();
}

class _GroupStudentPickerState extends State<_GroupStudentPicker> {
  String _query = '';
  @override
  Widget build(BuildContext context) {
    final students = widget.students
        .where((student) => studentMatchesSearch(student, _query))
        .toList();
    return Dialog(
      child: SizedBox(
        width: 700,
        height: MediaQuery.sizeOf(context).height * .75,
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            children: [
              Text(
                'اختر طالبًا من المجموعة',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              TextField(
                key: const Key('import-student-search'),
                decoration: const InputDecoration(
                  labelText: 'الاسم أو الكود أو الهاتف',
                ),
                onChanged: (value) => setState(() => _query = value),
              ),
              Expanded(
                child: ListView.builder(
                  itemCount: students.length,
                  itemBuilder: (context, index) {
                    final student = students[index];
                    return ListTile(
                      key: ValueKey('import-student-${student.id}'),
                      title: Text(student.name),
                      subtitle: Text('كود ${student.code} · ${student.phone}'),
                      onTap: () => Navigator.pop(context, student),
                    );
                  },
                ),
              ),
              TextButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('رجوع'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
