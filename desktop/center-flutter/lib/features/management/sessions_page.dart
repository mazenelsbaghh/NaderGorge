import 'package:massar_center/shared/problem_reporting.dart';
import 'package:flutter/material.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/workspace_draft_guard.dart';

import 'management_widgets.dart';
import 'study_month_editor_dialog.dart';

class SessionsPage extends StatefulWidget {
  const SessionsPage({
    super.key,
    required this.store,
    required this.onOpenAttendance,
    this.onOpenSession,
  });
  final CenterStore store;
  final VoidCallback onOpenAttendance;
  final ValueChanged<String>? onOpenSession;

  @override
  State<SessionsPage> createState() => _SessionsPageState();
}

class _SessionsPageState extends State<SessionsPage> {
  String? _groupId;
  bool _busy = false, _confirming = false;
  bool get _working => _busy || _confirming;

  String _status(SessionStatus status) => switch (status) {
    SessionStatus.open => 'مفتوحة',
    SessionStatus.closed => 'مغلقة',
    SessionStatus.canceled => 'ملغاة',
  };

  String? _studyMonthId;

  StudyMonth? get _selectedMonth {
    final months = [...widget.store.studyMonths]
      ..sort((a, b) => b.number.compareTo(a.number));
    return months.where((month) => month.id == _studyMonthId).firstOrNull ??
        months.firstOrNull;
  }

  Future<void> _editMonth([StudyMonth? month]) async {
    if (_working || !widget.store.canManage) {
      return;
    }
    setState(() => _busy = true);
    try {
      await showDialog<bool>(
        context: context,
        barrierDismissible: false,
        builder: (_) =>
            StudyMonthEditorDialog(store: widget.store, month: month),
      );
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _close(LessonSession session) async {
    if (_working || !widget.store.canCollect) return;
    setState(() => _confirming = true);
    try {
      final accepted = await confirmManagement(
        context,
        title: 'إغلاق ${sessionLabel(session)}',
        description:
            'سيُسجّل الطلبة غير المحضّرين غائبين. للحصة المحسوبة فقط، يُخصم رصيد الباقات المؤهلة للغائبين. بعد الإغلاق لن يمكن تسجيل دخول جديد.',
        confirmLabel: 'إغلاق وتسجيل الغياب',
      );
      if (!mounted || !accepted) return;
      await _run(
        () => widget.store.closeSession(session.id),
        'أُغلقت الحصة وسُجل الغياب.',
      );
    } finally {
      if (mounted) setState(() => _confirming = false);
    }
  }

  Future<void> _cancel(LessonSession session) async {
    if (_working || !widget.store.canManage) return;
    setState(() => _confirming = true);
    try {
      final accepted = await confirmManagement(
        context,
        title: 'إلغاء ${sessionLabel(session)}',
        description:
            'يمكن إلغاء الحصة إذا لم تُسجّل عليها مدفوعات أو حضور. لن يُخصم رصيد أي طالب.',
        confirmLabel: 'إلغاء الحصة',
        destructive: true,
      );
      if (!mounted || !accepted) return;
      await _run(() => widget.store.cancelSession(session.id), 'أُلغيت الحصة.');
    } finally {
      if (mounted) setState(() => _confirming = false);
    }
  }

  Future<void> _run(Future<void> Function() action, String message) async {
    if (_busy || !mounted) return;
    setState(() => _busy = true);
    try {
      await action();
      if (mounted) showManagementMessage(context, message);
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'ui.sessions_page');
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

  @override
  Widget build(BuildContext context) {
    final months = [...widget.store.studyMonths]
      ..sort((a, b) => a.number.compareTo(b.number));
    final month = _selectedMonth;
    final lessons = [...?month?.lessons]
      ..sort((a, b) => a.number.compareTo(b.number));
    final lessonIds = lessons.map((lesson) => lesson.id).toSet();
    final sessions =
        widget.store.sessions
            .where(
              (session) =>
                  !widget.store.isCairoGroup(session.groupId) &&
                  (month == null ||
                      lessonIds.contains(session.preparedLessonId)) &&
                  (_groupId == null || session.groupId == _groupId),
            )
            .toList()
          ..sort(compareSessionsNewestFirst);
    return WorkspaceDraftRegistration(
      dirty: false,
      busy: _working,
      child: ManagementPanel(
        title: 'الشهور والحصص',
        subtitle:
            'جهّز حصص الشهر مرة واحدة. ربط الحصة بالمجموعة يتم عند بدء التحضير فقط.',
        actions: [
          OutlinedButton.icon(
            onPressed: _working ? null : widget.onOpenAttendance,
            icon: const Icon(Icons.fullscreen),
            label: const Text('فتح التحضير'),
          ),
          if (widget.store.canManage) ...[
            FilledButton.icon(
              onPressed: _working ? null : () => _editMonth(),
              icon: const Icon(Icons.add),
              label: const Text('إضافة شهر وحصصه'),
            ),
            if (month != null)
              OutlinedButton.icon(
                onPressed: _working ? null : () => _editMonth(month),
                icon: const Icon(Icons.edit_outlined),
                label: const Text('تعديل الشهر وحصصه'),
              ),
          ],
        ],
        child: ManagementBody(
          header: [
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                SizedBox(
                  width: 320,
                  child: DropdownButtonFormField<String>(
                    key: ValueKey('shared-month-${month?.id}'),
                    initialValue: month?.id,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'الشهر'),
                    items: months
                        .map(
                          (item) => DropdownMenuItem(
                            value: item.id,
                            child: Text(
                              '${item.name} · شهر ${item.number}',
                              overflow: TextOverflow.ellipsis,
                            ),
                          ),
                        )
                        .toList(),
                    onChanged: _working
                        ? null
                        : (id) => setState(() => _studyMonthId = id),
                  ),
                ),
                if (month != null)
                  Text(
                    '${lessons.length} حصص معدّة${widget.store.canCollect ? ' · سعر الشهر ${money(month.price)}' : ''}',
                  ),
              ],
            ),
            const SizedBox(height: 12),
            const Text(
              'الحصص المعدّة — بدون مجموعة حتى البدء',
              style: TextStyle(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
          ],
          child: months.isEmpty
              ? const EmptySection(
                  message:
                      'أضف شهرًا باسمه وسعره وحصصه أولًا، ثم اختاره في التحضير.',
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Expanded(
                      flex: 4,
                      child: ManagementTable(
                        columns: const [
                          'الحصة',
                          'الحساب',
                          'المجموعات المرتبطة',
                        ],
                        rows: lessons
                            .map(
                              (lesson) => DataRow(
                                cells: [
                                  DataCell(
                                    SizedBox(
                                      width: 260,
                                      child: Text(
                                        lesson.name.trim().isEmpty
                                            ? 'حصة ${lesson.number}'
                                            : 'حصة ${lesson.number} · ${lesson.name}',
                                        maxLines: 2,
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                    ),
                                  ),
                                  DataCell(Text(sessionKindLabel(lesson.kind))),
                                  DataCell(
                                    Text(
                                      '${widget.store.sessions.where((session) => session.preparedLessonId == lesson.id).map((session) => session.groupId).toSet().length}',
                                    ),
                                  ),
                                ],
                              ),
                            )
                            .toList(),
                      ),
                    ),
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 16,
                      runSpacing: 8,
                      crossAxisAlignment: WrapCrossAlignment.center,
                      children: [
                        const Text(
                          'الحصص المرتبطة بالمجموعات',
                          style: TextStyle(fontWeight: FontWeight.bold),
                        ),
                        SizedBox(
                          width: 300,
                          child: DropdownButtonFormField<String>(
                            initialValue: _groupId,
                            isExpanded: true,
                            decoration: const InputDecoration(
                              labelText: 'عرض مجموعة',
                              isDense: true,
                            ),
                            items: [
                              const DropdownMenuItem<String>(
                                value: null,
                                child: Text('كل المجموعات'),
                              ),
                              ...widget.store.groupsForRegion().map(
                                (group) => DropdownMenuItem(
                                  value: group.id,
                                  child: Text(
                                    widget.store.groupLabel(group.id),
                                    overflow: TextOverflow.ellipsis,
                                  ),
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
                    const SizedBox(height: 8),
                    Expanded(
                      flex: 5,
                      child: sessions.isEmpty
                          ? const EmptySection(
                              message:
                                  'لم تبدأ حصة من هذا الشهر مع هذه المجموعة بعد.',
                            )
                          : ManagementTable(
                              columns: const [
                                'الحصة',
                                'المجموعة',
                                'الموعد',
                                'الحساب',
                                'الحالة',
                                'الحضور',
                                'الإجراءات',
                              ],
                              rows: sessions
                                  .map(
                                    (session) => DataRow(
                                      cells: [
                                        DataCell(Text(sessionLabel(session))),
                                        DataCell(
                                          SizedBox(
                                            width: 220,
                                            child: Text(
                                              widget.store.groupLabel(
                                                session.groupId,
                                              ),
                                              maxLines: 2,
                                              overflow: TextOverflow.ellipsis,
                                            ),
                                          ),
                                        ),
                                        DataCell(
                                          Text(
                                            session.startsAtKnown
                                                ? '${sessionDateLabel(session)}\n${TimeOfDay.fromDateTime(session.startsAt).format(context)}'
                                                : sessionDateLabel(session),
                                          ),
                                        ),
                                        DataCell(
                                          Text(sessionKindLabel(session.kind)),
                                        ),
                                        DataCell(Text(_status(session.status))),
                                        DataCell(
                                          Text(
                                            '${widget.store.attendanceCount(session.id)}',
                                          ),
                                        ),
                                        DataCell(
                                          Row(
                                            mainAxisSize: MainAxisSize.min,
                                            children: [
                                              IconButton(
                                                tooltip:
                                                    'تحضير أو عرض هذه الحصة',
                                                onPressed:
                                                    !_working &&
                                                        session.status !=
                                                            SessionStatus
                                                                .canceled
                                                    ? () {
                                                        if (widget
                                                                .onOpenSession !=
                                                            null) {
                                                          widget.onOpenSession!(
                                                            session.id,
                                                          );
                                                        } else {
                                                          widget
                                                              .onOpenAttendance();
                                                        }
                                                      }
                                                    : null,
                                                icon: const Icon(
                                                  Icons.qr_code_scanner,
                                                ),
                                              ),
                                              IconButton(
                                                tooltip: 'إغلاق وتسجيل الغياب',
                                                onPressed:
                                                    !_working &&
                                                        widget
                                                            .store
                                                            .canCollect &&
                                                        session.status ==
                                                            SessionStatus.open
                                                    ? () => _close(session)
                                                    : null,
                                                icon: const Icon(
                                                  Icons.task_alt,
                                                ),
                                              ),
                                              IconButton(
                                                tooltip: 'إلغاء الحصة',
                                                onPressed:
                                                    !_working &&
                                                        widget
                                                            .store
                                                            .canManage &&
                                                        session.status ==
                                                            SessionStatus.open
                                                    ? () => _cancel(session)
                                                    : null,
                                                icon: const Icon(
                                                  Icons.cancel_outlined,
                                                ),
                                              ),
                                            ],
                                          ),
                                        ),
                                      ],
                                    ),
                                  )
                                  .toList(),
                            ),
                    ),
                  ],
                ),
        ),
      ),
    );
  }
}
