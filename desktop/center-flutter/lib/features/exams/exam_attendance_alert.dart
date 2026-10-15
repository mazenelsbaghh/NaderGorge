import 'dart:async';

import 'package:flutter/material.dart';

import '../../application/center_store.dart';
import '../../domain/center_exam_monitor.dart';
import '../../shared/formatters.dart';
import '../../shared/scrollable_dialog.dart';
import '../../shared/theme.dart';

class ExamAttendanceAlert extends StatelessWidget {
  const ExamAttendanceAlert({super.key, required this.store});

  final CenterStore store;

  @override
  Widget build(BuildContext context) => _LiveAttendanceMonitor(
    store: store,
    builder: (context) {
      if (!store.canAssess) return const SizedBox.shrink();
      final rooms = store.integratedExamMonitorStatuses;
      if (rooms.isEmpty) return const SizedBox.shrink();
      final alerts = store.integratedExamAttendanceAlerts;
      final staleCount = rooms.where((room) => room.stale).length;
      final palette = MassarPalette.of(context);
      final needsAttention = alerts.isNotEmpty || staleCount > 0;
      return Material(
        color: needsAttention ? palette.warningSurface : palette.subtle,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
          child: Row(
            children: [
              Icon(
                needsAttention
                    ? Icons.fact_check_outlined
                    : Icons.check_circle_outline,
                size: 20,
                color: needsAttention ? palette.warning : palette.muted,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  _summary(alerts.length, staleCount),
                  key: const Key('exam-attendance-summary'),
                  style: TextStyle(color: palette.ink),
                ),
              ),
              TextButton(
                key: const Key('open-exam-attendance-alerts'),
                onPressed: () => showDialog<void>(
                  context: context,
                  builder: (_) => _ExamAttendanceDialog(store: store),
                ),
                child: const Text('عرض التفاصيل'),
              ),
            ],
          ),
        ),
      );
    },
  );
}

String _summary(int count, int staleCount) {
  final summary = count > 0
      ? 'دخول امتحان بدون حضور: $count'
      : staleCount > 0
      ? 'لا توجد حالات في آخر تحديث'
      : 'لا يوجد دخول امتحان بدون حضور مسجّل';
  return staleCount == 0
      ? summary
      : '$summary • تحديث متأخر: $staleCount امتحان';
}

class _ExamAttendanceDialog extends StatelessWidget {
  _ExamAttendanceDialog({required this.store})
    : actorId = store.currentUser?.id;

  final CenterStore store;
  final String? actorId;

  @override
  Widget build(BuildContext context) => _LiveAttendanceMonitor(
    store: store,
    builder: (context) {
      final authorized = store.canAssess && store.currentUser?.id == actorId;
      final rooms = authorized
          ? store.integratedExamMonitorStatuses
          : const <IntegratedExamMonitorStatus>[];
      final alerts = authorized
          ? store.integratedExamAttendanceAlerts
          : const <IntegratedExamAttendanceAlert>[];
      return ScrollableMassarDialog(
        width: 760,
        title: const Text('دخول الامتحان ومراجعة الحضور'),
        content: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (!authorized)
              const Text('سجّل دخول الموظف لعرض متابعة الحضور.')
            else ...[
              Text(_summary(alerts.length, rooms.where((r) => r.stale).length)),
              const SizedBox(height: 8),
              const Text('الحالات تخص الحصة المرتبطة بكل امتحان.'),
              for (final room in rooms) ...[
                const Divider(height: 28),
                _RoomAttendanceSection(
                  room: room,
                  alerts: alerts
                      .where(
                        (alert) =>
                            alert.sourceId == room.sourceId &&
                            alert.examId == room.examId,
                      )
                      .toList(),
                ),
              ],
              if (rooms.isEmpty)
                const Padding(
                  padding: EdgeInsets.only(top: 12),
                  child: Text('لا توجد امتحانات متاحة للمتابعة.'),
                ),
            ],
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('إغلاق'),
          ),
        ],
      );
    },
  );
}

class _RoomAttendanceSection extends StatelessWidget {
  const _RoomAttendanceSection({required this.room, required this.alerts});

  final IntegratedExamMonitorStatus room;
  final List<IntegratedExamAttendanceAlert> alerts;

  @override
  Widget build(BuildContext context) {
    final palette = MassarPalette.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(room.activityName, style: Theme.of(context).textTheme.titleMedium),
        Text('${room.groupLabel} • ${room.sessionLabel}'),
        const SizedBox(height: 6),
        Text(
          _freshness(context, room),
          style: TextStyle(color: room.stale ? palette.warning : palette.muted),
        ),
        if (room.stale) const Text('التحديث متأخر؛ قد توجد حالات لم تصل بعد.'),
        if (alerts.isEmpty)
          const Padding(
            padding: EdgeInsets.only(top: 12),
            child: Text('لا توجد حالات بدون حضور في آخر كشف مستلم.'),
          ),
        for (final alert in alerts)
          ListTile(
            contentPadding: EdgeInsets.zero,
            title: Wrap(
              spacing: 12,
              children: [Text(alert.name), Text('الكود: ${alert.code}')],
            ),
            subtitle: Text(_attemptStatus(alert.status)),
          ),
      ],
    );
  }
}

String _freshness(BuildContext context, IntegratedExamMonitorStatus room) {
  final received = room.lastSeenAt?.toLocal();
  if (received == null) return 'لم يصل تحديث من جهاز الامتحان بعد';
  final time = MaterialLocalizations.of(context).formatTimeOfDay(
    TimeOfDay.fromDateTime(received),
    alwaysUse24HourFormat: true,
  );
  return 'آخر تحديث: ${shortDate(received)} $time'
      ' • ${room.stale ? 'متأخر' : 'محدّث'}';
}

String _attemptStatus(String status) => switch (status) {
  'not_submitted' => 'دخل الامتحان ولم يسلّم',
  'pending_grading' => 'سلّم الامتحان وينتظر التصحيح',
  'awaiting_close' => 'تم التصحيح وينتظر إغلاق الامتحان',
  'final' => 'النتيجة نهائية',
  'cancelled' => 'المحاولة ملغاة',
  _ => 'حالة المحاولة غير متاحة',
};

class _LiveAttendanceMonitor extends StatefulWidget {
  const _LiveAttendanceMonitor({required this.store, required this.builder});

  final CenterStore store;
  final WidgetBuilder builder;

  @override
  State<_LiveAttendanceMonitor> createState() => _LiveAttendanceMonitorState();
}

class _LiveAttendanceMonitorState extends State<_LiveAttendanceMonitor> {
  late final Timer _freshnessTimer;

  @override
  void initState() {
    super.initState();
    _freshnessTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      setState(() {});
    });
  }

  @override
  void dispose() {
    _freshnessTimer.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: widget.store,
    builder: (context, _) => widget.builder(context),
  );
}
