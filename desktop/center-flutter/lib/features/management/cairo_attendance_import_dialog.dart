import 'package:file_selector/file_selector.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../../application/academic_excel_import.dart';
import '../../application/center_store.dart';
import '../../domain/models.dart';
import '../../lan/lan_transport.dart';
import '../../shared/scrollable_dialog.dart';

class CairoAttendanceImportDialog extends StatefulWidget {
  const CairoAttendanceImportDialog({
    super.key,
    required this.store,
    required this.groupId,
    required this.month,
    required this.lesson,
  });

  final CenterStore store;
  final String groupId;
  final StudyMonth month;
  final PreparedLesson lesson;

  @override
  State<CairoAttendanceImportDialog> createState() =>
      _CairoAttendanceImportDialogState();
}

typedef _PreviewRow = ({
  String file,
  CairoAttendanceRow source,
  Student? student,
  String? issue,
});

class _CairoAttendanceImportDialogState
    extends State<CairoAttendanceImportDialog> {
  late final _actorId = widget.store.currentUser?.id;
  List<_PreviewRow> _rows = [];
  final _selected = <String>{};
  int _skippedSheets = 0;
  bool _busy = false, _outcomeUnknown = false;
  String? _error;

  bool get _authorized =>
      widget.store.canAssess && widget.store.currentUser?.id == _actorId;

  Future<void> _load() async {
    if (_busy || !_authorized) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final files = await openFiles(
        acceptedTypeGroups: [
          const XTypeGroup(label: 'Excel', extensions: ['xlsx', 'xls']),
        ],
      );
      if (files.isEmpty || !mounted || !_authorized) return;
      setState(() {
        _rows = [];
        _selected.clear();
        _skippedSheets = 0;
      });
      final preview = await _readFiles(files);
      if (!mounted || !_authorized) return;
      setState(() {
        _rows = preview.$1;
        _skippedSheets = preview.$2;
        _selected
          ..clear()
          ..addAll(
            _rows
                .where((row) => row.issue == null)
                .map((row) => row.student!.id),
          );
      });
    } on FormatException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } on Exception {
      if (mounted) {
        setState(
          () =>
              _error = 'تعذر فتح الملفات؛ تأكد أن الملفات متاحة وحاول مجددًا.',
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<(List<_PreviewRow>, int)> _readFiles(List<XFile> files) async {
    if (files.length > 10) {
      throw const FormatException('اختر حتى ١٠ ملفات لنفس المجموعة والحصة.');
    }
    final books = <(String, CairoAttendanceWorkbook)>[];
    var skipped = 0, totalRows = 0;
    for (final file in files) {
      if (await file.length() > AcademicExcelLimits.maxFileBytes) {
        throw FormatException('${file.name} أكبر من ٨ ميجابايت.');
      }
      try {
        final workbook = await compute(
          readCairoAttendanceExcel,
          await file.readAsBytes(),
        );
        skipped += workbook.skippedSheets;
        totalRows += workbook.rows.length;
        books.add((file.name, workbook));
      } on FormatException catch (error) {
        throw FormatException('${file.name} · ${error.message}');
      }
      if (totalRows > AcademicExcelLimits.maxRows) {
        throw const FormatException(
          'الملفات تتجاوز ١٠ آلاف صف في الدفعة الواحدة.',
        );
      }
    }
    return (_preview(books), skipped);
  }

  List<_PreviewRow> _preview(List<(String, CairoAttendanceWorkbook)> books) {
    final store = widget.store;
    final matcher = CairoAttendanceMatcher(
      store.academicRosterForGroup(widget.groupId),
    );
    final session = store.sessionForPreparedLesson(
      widget.groupId,
      widget.lesson.id,
    );
    final current = {
      for (final row in store.attendances)
        if (row.sessionId == session?.id) row.studentId: row.status,
    };
    final seen = <String>{};
    return [
      for (final (file, workbook) in books)
        for (final row in workbook.rows)
          _matchRow(file, row, matcher, current, seen),
    ];
  }

  _PreviewRow _matchRow(
    String file,
    CairoAttendanceRow row,
    CairoAttendanceMatcher matcher,
    Map<String, AttendanceStatus> current,
    Set<String> seen,
  ) {
    final student = row.present && row.readable ? matcher.match(row) : null;
    final issue = switch ((row.readable, row.present, student)) {
      (false, _, _) => 'بيانات الصف غير قابلة للقراءة',
      (_, false, _) => 'الصف بلا حالة حضور',
      (_, _, null) => 'لا توجد مطابقة مؤكدة داخل المجموعة',
      (_, _, final Student match) when match.isSuspended => 'طالب موقوف',
      (_, _, final Student match)
          when current[match.id] == AttendanceStatus.absent =>
        'غياب محفوظ يحتاج مراجعة',
      (_, _, final Student match) when current.containsKey(match.id) =>
        'الحضور مسجّل بالفعل',
      (_, _, final Student match) when !seen.add(match.id) =>
        'صف مكرر في الملفات',
      _ => null,
    };
    return (file: file, source: row, student: student, issue: issue);
  }

  Future<void> _save() async {
    if (_busy || !_authorized || _selected.isEmpty) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final count = await widget.store.importCairoAttendance(
        groupId: widget.groupId,
        preparedLessonId: widget.lesson.id,
        studentIds: _selected.toList(),
      );
      if (mounted) Navigator.pop(context, count);
    } on LanConnectionException catch (error) {
      if (mounted) {
        setState(() {
          _outcomeUnknown = error.outcomeUnknown;
          _error = error.outcomeUnknown
              ? 'نتيجة الحفظ غير مؤكدة. أعد الاتصال ثم اضغط «إعادة التحقق من الحفظ».'
              : error.message;
        });
      }
    } on CenterException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _row(_PreviewRow row) {
    final source = row.source;
    final student = row.student;
    return CheckboxListTile(
      dense: true,
      value: row.issue == null && _selected.contains(student?.id),
      onChanged: _busy || _outcomeUnknown || row.issue != null
          ? null
          : (selected) {
              setState(() {
                if (selected == true) {
                  _selected.add(student!.id);
                } else {
                  _selected.remove(student!.id);
                }
              });
            },
      title: Text(
        [
          source.name.isEmpty ? source.code : source.name,
          if (student != null) '${student.name} · ${student.code}',
        ].join(' ← '),
      ),
      subtitle: Text(
        '${row.file} · ${source.sheet} · صف ${source.number} · ${row.issue ?? 'جاهز للتسجيل'}',
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final excluded = _rows.where((row) => row.issue != null).length;
    return PopScope(
      canPop: !_busy,
      child: Directionality(
        textDirection: TextDirection.rtl,
        child: ScrollableMassarDialog(
          title: const Text('استيراد حضور القاهرة من Excel'),
          content: SizedBox(
            width: 670,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  '${widget.store.groupLabel(widget.groupId)} · ${widget.month.name} · الحصة ${widget.lesson.number} ${widget.lesson.name}',
                ),
                const SizedBox(height: 8),
                const Text(
                  'اختر ملفات ‎.xlsx أو ‎.xls لنفس المجموعة والحصة. لو الملف من غير عمود حالة، راجع إن المدرجين فيه هم الحاضرون.',
                ),
                const SizedBox(height: 8),
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: OutlinedButton.icon(
                    onPressed: _busy || _outcomeUnknown || !_authorized
                        ? null
                        : _load,
                    icon: const Icon(Icons.folder_open),
                    label: Text(
                      _rows.isEmpty
                          ? 'اختيار ملفات الحضور'
                          : 'اختيار ملفات أخرى',
                    ),
                  ),
                ),
                if (_busy) const LinearProgressIndicator(),
                if (_error != null)
                  Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                if (_rows.isNotEmpty) ...[
                  Text(
                    'المحدد للتسجيل: ${_selected.length} · صفوف مستبعدة: $excluded',
                  ),
                  if (_skippedSheets > 0)
                    Text('أوراق من غير جدول حضور: $_skippedSheets'),
                  SizedBox(
                    height: 270,
                    child: ListView.builder(
                      primary: false,
                      itemCount: _rows.length,
                      itemBuilder: (_, index) => _row(_rows[index]),
                    ),
                  ),
                ],
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: _busy ? null : () => Navigator.pop(context),
              child: const Text('رجوع'),
            ),
            FilledButton(
              onPressed: _busy || !_authorized || _selected.isEmpty
                  ? null
                  : _save,
              child: Text(
                _outcomeUnknown
                    ? 'إعادة التحقق من الحفظ'
                    : 'تسجيل الحضور المحدد',
              ),
            ),
          ],
        ),
      ),
    );
  }
}
