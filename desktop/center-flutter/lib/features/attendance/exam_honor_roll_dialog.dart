import 'dart:io';

import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:path/path.dart' as p;

import '../../application/center_reports.dart' show parseAcademicScore;
import '../../application/center_store.dart';
import '../../application/exam_honor_roll.dart';
import '../../domain/models.dart';
import '../../exams/exam_attendance_xlsx.dart';
import '../../exams/exam_export_saver.dart';
import '../../shared/problem_reporting.dart';
import '../management/management_widgets.dart';

class ExamHonorRollDialog extends StatefulWidget {
  const ExamHonorRollDialog({
    super.key,
    required this.store,
    required this.session,
  });

  final CenterStore store;
  final LessonSession session;

  @override
  State<ExamHonorRollDialog> createState() => _ExamHonorRollDialogState();
}

class _ExamHonorRollDialogState extends State<ExamHonorRollDialog> {
  static const _columns = ['اسم الطالب', 'الكود', 'الدرجة'];
  num? _minimum = 55;
  String? _examId;
  bool _saving = false;
  late List<HonorRollExam> _exams;

  @override
  void initState() {
    super.initState();
    _exams = examsForHonorRoll(widget.store, widget.session.id);
    widget.store.addListener(_refreshExams);
  }

  void _refreshExams() => setState(() {
    _exams = examsForHonorRoll(widget.store, widget.session.id);
  });

  @override
  void dispose() {
    widget.store.removeListener(_refreshExams);
    super.dispose();
  }

  Future<void> _export(HonorRollExam exam, List<HonorRollRow> rows) async {
    final store = widget.store;
    final actor = store.currentUser;
    if (actor == null) return;
    var sessionChanged = false;
    void observeSession() {
      if (store.currentUser?.id != actor.id ||
          store.currentUser?.role != actor.role) {
        sessionChanged = true;
      }
    }

    bool authorized() => mounted && !sessionChanged && store.canAssess;
    store.addListener(observeSession);
    setState(() => _saving = true);
    try {
      final group = store.groupLabel(widget.session.groupId);
      final title = 'كشف التكريم - $group - ${exam.name}'
          .replaceAll(RegExp(r'[<>:"/\\|?*\x00-\x1f]'), ' ')
          .trim();
      final destination = await getSaveLocation(
        suggestedName:
            '${String.fromCharCodes(title.runes.take(72)).trimRight()}.xlsx',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'شيت Excel', extensions: ['xlsx']),
        ],
      );
      if (destination == null || !authorized()) return;
      await _saveWorkbook(rows, destination.path, authorized);
      if (mounted && authorized()) {
        await showManagementMessage(context, 'تم حفظ كشف التكريم في Excel.');
      }
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'ui.exam_honor_roll');
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      store.removeListener(observeSession);
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _saveWorkbook(
    List<HonorRollRow> rows,
    String destination,
    bool Function() authorized,
  ) async {
    final store = widget.store;
    final workbook = buildExamWorkbook(
      {
        'كشف التكريم': [
          _columns,
          for (final row in rows) [row.name, row.code, row.score],
        ],
      },
      columnWidths: const [38, 18, 14],
    );
    final temporary = await Directory.systemTemp.createTemp(
      'massar-honor-roll-',
    );
    try {
      final source = await File(
        p.join(temporary.path, 'honor-roll.xlsx'),
      ).writeAsBytes(workbook, flush: true);
      await ExamExportSaver(
        protectedDirectories: [
          Directory(p.dirname(store.databasePath)),
          Directory(p.dirname(Platform.resolvedExecutable)),
          if (store.automaticBackupDirectory case final String folder)
            Directory(folder),
        ],
        isAuthorized: authorized,
      ).save(source, destination);
    } finally {
      await temporary.delete(recursive: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final exams = _exams;
    final exam =
        exams.where((exam) => exam.id == _examId).firstOrNull ??
        exams.firstOrNull;
    final minimum = _minimum;
    final rows = minimum == null || exam == null
        ? const <HonorRollRow>[]
        : exam.qualifying(minimum);
    return Dialog.fullscreen(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('كشف التكريم'),
          leading: IconButton(
            tooltip: 'العودة للتحضير',
            onPressed: _saving ? null : () => Navigator.pop(context),
            icon: const Icon(Icons.close),
          ),
        ),
        body: SafeArea(
          child: ManagementBody(
            header: [
              _filters(exams, exam),
              if (exam != null && minimum != null)
                Text('عدد الطلبة المؤهلين: ${rows.length}'),
              if (rows.isNotEmpty)
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: FilledButton.icon(
                    onPressed: _saving ? null : () => _export(exam!, rows),
                    icon: const Icon(Icons.download_outlined),
                    label: Text(_saving ? 'جارٍ الحفظ…' : 'تنزيل شيت Excel'),
                  ),
                ),
            ],
            child: rows.isEmpty
                ? Center(
                    child: Text(
                      exam == null
                          ? 'لا توجد امتحانات أو درجات مرصودة لهذه الحصة.'
                          : minimum == null
                          ? 'اكتب الحد الأدنى لعرض النتائج.'
                          : 'لا يوجد طلبة بالدرجة المطلوبة.',
                    ),
                  )
                : ManagementTable.builder(
                    columns: _columns,
                    rowCount: rows.length,
                    rowBuilder: (index) {
                      final row = rows[index];
                      return DataRow(
                        cells: [
                          DataCell(Text(row.name)),
                          DataCell(Text(row.code)),
                          DataCell(Text('${row.score}')),
                        ],
                      );
                    },
                  ),
          ),
        ),
      ),
    );
  }

  Widget _filters(List<HonorRollExam> exams, HonorRollExam? exam) => Padding(
    padding: const EdgeInsets.all(16),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'المجموعة الأصلية · ${widget.store.groupLabel(widget.session.groupId)}',
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: [
            SizedBox(
              width: 320,
              child: DropdownButtonFormField<String>(
                key: ValueKey(exam?.id),
                initialValue: exam?.id,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'الامتحان'),
                items: [
                  for (final exam in exams)
                    DropdownMenuItem(
                      value: exam.id,
                      child: Text(exam.name, overflow: TextOverflow.ellipsis),
                    ),
                ],
                onChanged: _saving || exams.isEmpty
                    ? null
                    : (id) => setState(() => _examId = id),
              ),
            ),
            SizedBox(
              width: 160,
              child: TextFormField(
                initialValue: '٥٥',
                enabled: !_saving && exam != null,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                inputFormatters: [LengthLimitingTextInputFormatter(16)],
                decoration: InputDecoration(
                  labelText: 'من درجة',
                  errorText: _minimum == null ? 'أدخل درجة من صفر فأعلى' : null,
                ),
                onChanged: (text) {
                  final score = parseAcademicScore(text);
                  setState(
                    () => _minimum = score != null && score >= 0 ? score : null,
                  );
                },
              ),
            ),
          ],
        ),
      ],
    ),
  );
}
