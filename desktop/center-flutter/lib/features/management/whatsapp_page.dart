import 'package:flutter/material.dart';

import '../../application/center_reports.dart';
import '../../application/center_store.dart';
import '../../application/center_whatsapp_reports.dart';
import '../../domain/models.dart';
import '../../shared/formatters.dart';
import '../../shared/problem_reporting.dart';
import '../../shared/theme.dart';
import 'management_widgets.dart';

class WhatsAppPage extends StatefulWidget {
  const WhatsAppPage({super.key, required this.store, this.onWhatsApp});
  final CenterStore store;
  final Future<void> Function(CenterReportData report)? onWhatsApp;

  @override
  State<WhatsAppPage> createState() => _WhatsAppPageState();
}

class _WhatsAppPageState extends State<WhatsAppPage> {
  String? _centerId, _groupId, _monthId, _sessionId, _examId, _homeworkId;
  String _messageKind = 'followup', _status = 'all';
  final _excluded = <String>{};
  bool _busy = false;

  String _monthKey(LessonSession session) => session.preparedLessonId == null
      ? 'number:${session.monthNumber}'
      : widget.store.studyMonthForLesson(session.preparedLessonId!)?.id ??
            'number:${session.monthNumber}';

  String _monthLabel(LessonSession session) => session.preparedLessonId == null
      ? 'الشهر ${session.monthNumber}'
      : widget.store.studyMonthForLesson(session.preparedLessonId!)?.name ??
            'الشهر ${session.monthNumber}';

  List<LessonSession> get _sessions =>
      widget.store.sessions
          .where(
            (session) =>
                session.groupId == _groupId &&
                session.status != SessionStatus.canceled,
          )
          .toList()
        ..sort(compareSessionsNewestFirst);

  CenterReportData? _report() {
    if (_groupId == null || _sessionId == null) return null;
    return CenterWhatsAppReports.build(
      widget.store,
      CenterWhatsAppSelection(
        groupId: _groupId!,
        sessionId: _sessionId!,
        examId: _examId,
        homeworkId: _homeworkId,
      ),
    );
  }

  bool _matches(Map<String, dynamic> row) {
    if (_messageKind == 'grade' && row['score'] == null) return false;
    return switch (_status) {
      'present' =>
        row['attendance'] == attendanceLabel(AttendanceStatus.present) ||
            row['attendance'] == attendanceLabel(AttendanceStatus.makeup),
      'absent' => row['attendance'] == attendanceLabel(AttendanceStatus.absent),
      'unrecorded' => row['attendance'] == 'لم يُسجل حضور',
      'complete' => row['homework'] == homeworkLabel(HomeworkStatus.complete),
      'missing' => row['homework'] == homeworkLabel(HomeworkStatus.missing),
      'incomplete' =>
        row['homework'] == homeworkLabel(HomeworkStatus.incomplete),
      'notReviewed' =>
        row['homework'] == homeworkLabel(HomeworkStatus.notReviewed),
      _ => true,
    };
  }

  void _resetClass() {
    _monthId = _sessionId = _examId = _homeworkId = null;
    _excluded.clear();
  }

  Future<void> _preview() async {
    if (_busy || widget.onWhatsApp == null) return;
    setState(() => _busy = true);
    try {
      final fresh = _report();
      if (fresh == null) return;
      final recipients = fresh.whatsAppRecords
          .where(_matches)
          .where((row) => !_excluded.contains(row['studentId']))
          .toList();
      if (recipients.isEmpty) {
        throw const CenterException('حدد طالبًا واحدًا على الأقل من المعاينة.');
      }
      if (recipients.length > 1000) {
        throw const CenterException('اختر مجموعة من ١٠٠٠ طالب أو أقل للإرسال.');
      }
      await widget.onWhatsApp!(
        CenterReportData(
          title: fresh.title,
          columns: fresh.columns,
          rows: recipients.map(CenterWhatsAppReports.displayRow).toList(),
          summary: {'الطلاب المحددون': '${recipients.length}'},
          caption: fresh.caption,
          whatsAppRecords: recipients,
          whatsAppMessageKind: _messageKind,
        ),
      );
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.whatsapp_preview');
      if (mounted) {
        await showManagementMessage(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Widget _select(
    String label,
    String? selected,
    Map<String, String> options,
    ValueChanged<String?> onChanged,
  ) => SizedBox(
    width: 244,
    child: DropdownButtonFormField<String>(
      key: ValueKey('$label:$selected:${options.keys.join(',')}'),
      initialValue: options.containsKey(selected) ? selected : null,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: options.entries
          .map(
            (option) => DropdownMenuItem(
              value: option.key,
              child: Text(option.value, overflow: TextOverflow.ellipsis),
            ),
          )
          .toList(),
      onChanged: _busy ? null : onChanged,
    ),
  );

  Widget _filters(List<LessonSession> sessions) {
    final store = widget.store;
    final selectedSession = store.sessionById(_sessionId);
    final activities = store.academicActivities.where(
      (activity) =>
          selectedSession != null && activity.appliesToSession(selectedSession),
    );
    final statusOptions = _messageKind == 'homework'
        ? const {
            'all': 'كل حالات الواجب',
            'complete': 'عمل الواجب',
            'missing': 'لم يعمل الواجب',
            'incomplete': 'واجب ناقص',
            'notReviewed': 'لم يُراجع',
          }
        : const {
            'all': 'كل حالات الحضور',
            'present': 'حاضر ومعوّض',
            'absent': 'غائب مسجل',
            'unrecorded': 'لم يُسجل حضور',
          };
    return Wrap(
      spacing: 12,
      runSpacing: 14,
      children: [
        _select(
          'السنتر',
          _centerId,
          {
            for (final center in store.catalogs.where(
              (entry) => entry.kind == CatalogKind.center,
            ))
              center.id: center.name,
          },
          (id) => setState(() {
            _centerId = id;
            _groupId = null;
            _resetClass();
          }),
        ),
        _select(
          'المجموعة',
          _groupId,
          {
            for (final group in store.groups.where(
              (group) => _centerId == null || group.centerId == _centerId,
            ))
              group.id: store.groupLabel(group.id),
          },
          (id) => setState(() {
            _groupId = id;
            _resetClass();
          }),
        ),
        _select(
          'الشهر',
          _monthId,
          {
            for (final session in sessions)
              _monthKey(session): _monthLabel(session),
          },
          (id) => setState(() {
            _monthId = id;
            _sessionId = _examId = _homeworkId = null;
            _excluded.clear();
          }),
        ),
        _select(
          'الحصة',
          _sessionId,
          {
            for (final session in sessions.where(
              (session) => _monthId == null || _monthKey(session) == _monthId,
            ))
              session.id:
                  '${sessionLabel(session)} · ${sessionDateLabel(session)}',
          },
          (id) => setState(() {
            _sessionId = id;
            final session = store.sessionById(id);
            final candidates = store.academicActivities.where(
              (activity) =>
                  session != null && activity.appliesToSession(session),
            );
            final exams = candidates
                .where((activity) => activity.kind == AcademicActivityKind.exam)
                .toList();
            final homework = candidates
                .where(
                  (activity) => activity.kind == AcademicActivityKind.homework,
                )
                .toList();
            _examId = exams.length == 1 ? exams.single.id : null;
            _homeworkId = homework.length == 1 ? homework.single.id : null;
            _excluded.clear();
          }),
        ),
        _select(
          'الامتحان',
          _examId,
          {
            for (final activity in activities.where(
              (activity) => activity.kind == AcademicActivityKind.exam,
            ))
              activity.id: activity.name,
          },
          (id) => setState(() {
            _examId = id;
            _excluded.clear();
          }),
        ),
        _select(
          'الواجب',
          _homeworkId,
          {
            for (final activity in activities.where(
              (activity) => activity.kind == AcademicActivityKind.homework,
            ))
              activity.id: activity.name,
          },
          (id) => setState(() {
            _homeworkId = id;
            _excluded.clear();
          }),
        ),
        _select(
          'نوع الرسالة',
          _messageKind,
          const {
            'followup': 'حضور وواجب ودرجة',
            'attendance': 'حضور وغياب',
            'homework': 'متابعة الواجب',
            'grade': 'درجة الامتحان',
          },
          (id) => setState(() {
            _messageKind = id!;
            _status = 'all';
            _excluded.clear();
          }),
        ),
        if (_messageKind != 'grade')
          _select(
            'حالة الطلاب',
            _status,
            statusOptions,
            (id) => setState(() {
              _status = id!;
              _excluded.clear();
            }),
          ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: widget.store,
    builder: (context, _) {
      CenterReportData? report;
      String? selectionError;
      try {
        report = _report();
      } on CenterException catch (error) {
        selectionError = error.message;
      }
      final rows =
          report?.whatsAppRecords.where(_matches).toList() ??
          <Map<String, dynamic>>[];
      final selectedCount = rows
          .where((row) => !_excluded.contains(row['studentId']))
          .length;
      final requiresActivity =
          (_messageKind == 'grade' && _examId == null) ||
          (_messageKind == 'homework' && _homeworkId == null);
      final canPreview =
          widget.onWhatsApp != null &&
          report != null &&
          !requiresActivity &&
          selectedCount > 0 &&
          !_busy;
      final palette = MassarPalette.of(context);
      return ManagementPanel(
        title: 'رسائل واتساب',
        subtitle:
            'اختر الحصة، راجع الطلاب، ثم اختر القالب المحفوظ ومعاينة الرسائل.',
        actions: [
          FilledButton.icon(
            key: const Key('whatsapp-open-preview'),
            onPressed: canPreview ? _preview : null,
            icon: _busy
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.preview_outlined),
            label: Text('اختيار القالب ومعاينة $selectedCount طالب'),
          ),
        ],
        child: ManagementBody(
          header: [
            _filters(_sessions),
            const SizedBox(height: 16),
            if (widget.onWhatsApp == null)
              const Text(
                'الإرسال غير متاح حاليًا. افتح هذه الصفحة من الجهاز المتصل بالهوست.',
              ),
            if (selectionError != null)
              Text(selectionError, style: TextStyle(color: palette.error)),
            if (requiresActivity && report != null)
              const Text(
                'اختر الامتحان أو الواجب المقصود حتى تظهر بياناته وحده في الرسالة.',
              ),
            if (report != null) ...[
              Wrap(
                spacing: 16,
                runSpacing: 8,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    '${rows.length} طالب في المعاينة · $selectedCount محدد',
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  TextButton(
                    onPressed: _busy
                        ? null
                        : () => setState(
                            () => _excluded.removeAll(
                              rows.map((row) => row['studentId'] as String),
                            ),
                          ),
                    child: const Text('تحديد الكل'),
                  ),
                  TextButton(
                    onPressed: _busy
                        ? null
                        : () => setState(
                            () => _excluded.addAll(
                              rows.map((row) => row['studentId'] as String),
                            ),
                          ),
                    child: const Text('إلغاء التحديد'),
                  ),
                ],
              ),
              Text(
                report.caption!,
                style: Theme.of(context).textTheme.bodySmall,
              ),
              const SizedBox(height: 10),
            ],
          ],
          child: report == null || rows.isEmpty
              ? EmptySection(
                  message:
                      selectionError ??
                      (report == null
                          ? 'اختر المجموعة والحصة لعرض الطلاب ورسائلهم.'
                          : 'لا يوجد طلاب مطابقون للاختيارات الحالية.'),
                )
              : ManagementTable.builder(
                  columns: ['إرسال', ...report.columns],
                  rowCount: rows.length,
                  rowBuilder: (index) {
                    final row = rows[index];
                    final id = row['studentId'] as String;
                    return DataRow(
                      cells: [
                        DataCell(
                          Checkbox(
                            value: !_excluded.contains(id),
                            onChanged: _busy
                                ? null
                                : (checked) => setState(() {
                                    checked == true
                                        ? _excluded.remove(id)
                                        : _excluded.add(id);
                                  }),
                          ),
                        ),
                        ...CenterWhatsAppReports.displayRow(
                          row,
                        ).map((cell) => DataCell(Text('$cell'))),
                      ],
                    );
                  },
                ),
        ),
      );
    },
  );
}
