import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:flutter/material.dart';
import 'record_cancellation_dialog.dart';
import '../management/student_profile_page.dart';
import 'student_debt_dialog.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/application/center_reports.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';

/// All recent records remain visible together while the cashier receives a student.
class StudentHistoryPanel extends StatelessWidget {
  const StudentHistoryPanel({
    super.key,
    required this.store,
    required this.student,
    this.expanded = false,
    this.actionsEnabled = true,
    this.onOpenModal,
    this.collectionSessionId,
    this.currentSessionId,
    this.notesPanel,
    this.onTwin,
    this.onNote,
    this.onCard,
    this.onSuspend,
  });
  final String? currentSessionId;
  final Widget? notesPanel;
  final VoidCallback? onTwin, onCard, onSuspend, onNote;
  final CenterStore store;
  final Student student;
  final bool expanded;
  final String? collectionSessionId;
  final bool actionsEnabled;
  final Future<void> Function(Future<void> Function())? onOpenModal;

  Future<void> _openModal(Future<void> Function() work) =>
      onOpenModal == null ? work() : onOpenModal!(work);

  Future<void> _cancelRecord(
    BuildContext context, {
    String? paymentId,
    String? attendanceId,
  }) => _openModal(() async {
    if (!context.mounted || !actionsEnabled || !store.canCollect) return;
    await showDialog<bool>(
      context: context,
      barrierDismissible: false,
      builder: (_) => RecordCancellationDialog(
        store: store,
        paymentId: paymentId,
        attendanceId: attendanceId,
      ),
    );
  });

  LessonSession? _session(String id) => store.sessionById(id);

  String _sessionLabel(String id) {
    final session = _session(id);
    if (session == null) return 'حصة غير متاحة';
    final group = store.groupById(session.groupId);
    return '${sessionLabel(session)}${group == null ? '' : ' · ${group.name}'}';
  }

  String _scoreLabel(AcademicRecord? record) =>
      record == null || (!record.examAbsent && record.score == null)
      ? 'لم تُرصد'
      : record.examAbsent
      ? 'غائب عن الامتحان'
      : record.maxScoreKnown
      ? '${record.score} / ${record.maxScore}'
      : '${record.score} · الدرجة النهائية غير معروفة';

  String _attendanceLabel(AttendanceRecord record) {
    final label = switch (record.status) {
      AttendanceStatus.present => 'حاضر',
      AttendanceStatus.makeup => 'معوّض',
      AttendanceStatus.absent => 'غائب',
    };
    return record.paymentPending ? '$label — غير مدفوع' : label;
  }

  String _historicalEntryLabel(AttendanceRecord record) {
    if (record.importSource.isNotEmpty) {
      return '${_attendanceLabel(record)} · مستورد؛ الدفع غير موثق';
    }
    final account = record.packageId != null
        ? 'باقة'
        : record.status == AttendanceStatus.makeup
        ? 'تعويض'
        : record.status == AttendanceStatus.absent
        ? 'بدون مديونية'
        : _session(record.sessionId)?.kind == SessionKind.free
        ? 'حصة مجانية'
        : 'دفع بالحصة';
    return '${_attendanceLabel(record)} · $account';
  }

  String _makeupLabel(AttendanceRecord record) {
    if (record.makeupSourceGroupId != null) {
      return 'من ${store.groupLabel(record.makeupSourceGroupId!)}';
    }
    if (store.attendances.any(
      (item) => item.originalAttendanceId == record.id,
    )) {
      return 'تم التعويض';
    }
    if (record.originalAttendanceId != null) {
      final originals = store.attendances.where(
        (item) => item.id == record.originalAttendanceId,
      );
      return originals.isEmpty
          ? 'تعويض'
          : 'عن ${_sessionLabel(originals.first.sessionId)}';
    }
    return record.status == AttendanceStatus.absent && record.packageId != null
        ? 'غياب محسوب من الشهر'
        : '—';
  }

  PaymentRecord? _attendancePayment(AttendanceRecord record) {
    var packageId = record.packageId;
    if (packageId == null && record.originalAttendanceId != null) {
      packageId = store.allAttendances
          .where((original) => original.id == record.originalAttendanceId)
          .firstOrNull
          ?.packageId;
    }
    if (packageId != null) {
      final paymentId = store.allPackages
          .where((package) => package.id == packageId)
          .firstOrNull
          ?.paymentId;
      return store.payments
          .where((payment) => payment.id == paymentId)
          .firstOrNull;
    }
    return store.payments
        .where(
          (payment) =>
              payment.studentId == record.studentId &&
              payment.sessionId == record.sessionId &&
              payment.packageId == null,
        )
        .firstOrNull;
  }

  String _collectionStatus(int due, int collected, int remaining) {
    if (due == 0) return 'معفى — لا يوجد مبلغ مستحق';
    if (remaining == 0) return 'مسدد';
    return collected == 0 ? 'غير مدفوع' : 'دفع جزئي';
  }

  String _paymentStatus(PaymentRecord payment, Set<String> activeIds) =>
      activeIds.contains(payment.id)
      ? _collectionStatus(
          payment.netAmount,
          store.paymentCollectedFor(payment.id),
          store.paymentDebtFor(payment.id),
        )
      : 'ملغاة';

  String _accountLabel(AttendanceRecord record) {
    if (record.packageMember) return 'باكدج — دون تحصيل';
    if (record.paymentPending) return 'حضور محفوظ — غير مدفوع';
    final payment = _attendancePayment(record);
    if (payment != null) {
      final status = _collectionStatus(
        payment.netAmount,
        store.paymentCollectedFor(payment.id),
        store.paymentDebtFor(payment.id),
      );
      final account =
          record.packageId != null || record.originalAttendanceId != null
          ? 'محسوب من الشهر'
          : record.makeupSourceGroupId != null
          ? 'حصة معوّض'
          : 'دفع الحصة';
      return '$account · $status';
    }
    if (record.importSource.isNotEmpty) return 'مستورد؛ الدفع غير موثق';
    if (record.makeupSourceGroupId != null) {
      return 'غير مدفوع — لا يوجد دفع ساري';
    }
    if (record.status == AttendanceStatus.makeup) {
      return 'معوّض — دون تحصيل جديد';
    }
    if (record.status == AttendanceStatus.absent) {
      return 'غير مدفوع — بدون مديونية';
    }
    if (_session(record.sessionId)?.kind == SessionKind.free) {
      return 'حصة مجانية';
    }
    return 'غير مدفوع — لا يوجد دفع ساري';
  }

  String _paymentTime(DateTime date) {
    final local = date.toLocal();
    String twoDigits(int number) => number.toString().padLeft(2, '0');
    return '${shortDate(local)} · ${twoDigits(local.hour)}:${twoDigits(local.minute)}:${twoDigits(local.second)}';
  }

  bool _isTimestamp(String text) =>
      text.contains(' · ') && RegExp(r'\d{2}:\d{2}:\d{2}$').hasMatch(text);

  Widget _value(String value, {int maxLines = 1}) {
    final numericScore = RegExp(
      r'^\d+ ?/ ?\d+(?:، \d+ ?/ ?\d+)*$',
    ).hasMatch(value);
    final warning =
        value == 'ناقص' ||
        value == 'لم يعمل' ||
        value == 'لا يوجد دفع ساري' ||
        value.contains('غير مدفوع') ||
        value.contains('دفع جزئي') ||
        value == 'ملغاة' ||
        value.startsWith('غائب') ||
        value.startsWith('معوّض');
    return Builder(
      builder: (context) => Text(
        value,
        textDirection: numericScore || _isTimestamp(value)
            ? TextDirection.ltr
            : TextDirection.rtl,
        maxLines: maxLines,
        overflow: TextOverflow.ellipsis,
        style: warning
            ? TextStyle(
                color: MassarPalette.of(context).error,
                fontWeight: FontWeight.w600,
              )
            : null,
      ),
    );
  }

  Widget _table(
    List<String> headers,
    List<List<String>> rows,
    String empty, {
    Widget Function(int index)? action,
    Widget? Function(int row, int column)? cell,
  }) {
    if (rows.isEmpty) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 10),
        child: Text(empty),
      );
    }
    return LayoutBuilder(
      builder: (context, constraints) => MassarScrollView(
        scrollDirection: Axis.horizontal,
        child: ConstrainedBox(
          constraints: BoxConstraints(minWidth: constraints.maxWidth),
          child: DataTable(
            columnSpacing: 14,
            horizontalMargin: 10,
            headingRowHeight: 36,
            dataRowMinHeight: 32,
            dataRowMaxHeight: 40,
            headingRowColor: WidgetStatePropertyAll(
              MassarPalette.of(context).tableHeader,
            ),
            columns: [if (action != null) 'الإجراء', ...headers]
                .map(
                  (label) => DataColumn(
                    label: Text(
                      label,
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                  ),
                )
                .toList(),
            rows: rows
                .asMap()
                .entries
                .map(
                  (entry) => DataRow(
                    cells: [
                      if (action != null) DataCell(action(entry.key)),
                      ...entry.value.asMap().entries.map(
                        (column) => DataCell(
                          cell?.call(entry.key, column.key) ??
                              Tooltip(
                                message: column.value,
                                child: ConstrainedBox(
                                  constraints: BoxConstraints(
                                    maxWidth: _isTimestamp(column.value)
                                        ? 210
                                        : 160,
                                  ),
                                  child: _value(column.value),
                                ),
                              ),
                        ),
                      ),
                    ],
                  ),
                )
                .toList(),
          ),
        ),
      ),
    );
  }

  Widget _section(String title, IconData icon, Widget content) => Builder(
    builder: (context) => Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: MassarPalette.of(context).surface,
        border: Border.all(color: MassarPalette.of(context).line),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.all(10),
            child: Row(
              children: [
                Icon(icon, size: 18, color: MassarPalette.of(context).accent),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(
                      fontWeight: FontWeight.bold,
                      fontSize: 16,
                    ),
                  ),
                ),
              ],
            ),
          ),
          content,
        ],
      ),
    ),
  );

  Widget _beside(Widget first, Widget second) => LayoutBuilder(
    builder: (context, constraints) =>
        constraints.maxWidth / MediaQuery.textScalerOf(context).scale(1) < 560
        ? Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [first, const SizedBox(height: 12), second],
          )
        : Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(child: first),
              const SizedBox(width: 12),
              Expanded(child: second),
            ],
          ),
  );

  Widget _stateCell(
    BuildContext context,
    String text, {
    required bool good,
    bool warning = false,
  }) {
    final palette = MassarPalette.of(context);
    final color = good
        ? palette.success
        : warning
        ? palette.warning
        : palette.error;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          good
              ? Icons.check_circle_outline
              : warning
              ? Icons.remove_circle_outline
              : Icons.cancel_outlined,
          size: 17,
          color: color,
        ),
        const SizedBox(width: 4),
        Text(
          text,
          style: TextStyle(color: color, fontWeight: FontWeight.w600),
        ),
      ],
    );
  }

  Widget _examScore(BuildContext context, AcademicRecord record) {
    final palette = MassarPalette.of(context);
    final known =
        record.score != null && record.maxScoreKnown && !record.examAbsent;
    return Text(
      _scoreLabel(record),
      key: ValueKey('history-exam-score-${record.id}'),
      textDirection: record.score == null
          ? TextDirection.rtl
          : TextDirection.ltr,
      style: TextStyle(
        fontWeight: FontWeight.bold,
        color: record.examAbsent
            ? palette.error
            : !known
            ? null
            : record.score! * 2 < record.maxScore
            ? palette.error
            : palette.success,
      ),
    );
  }

  Widget _toolbar(BuildContext context, List<AttendanceRecord> attendance) {
    final current = attendance
        .where(
          (a) =>
              a.sessionId == currentSessionId &&
              a.status != AttendanceStatus.absent,
        )
        .firstOrNull;
    Widget button(
      String label,
      IconData icon,
      VoidCallback? action,
      Color color,
    ) => OutlinedButton.icon(
      onPressed: actionsEnabled ? action : null,
      style: OutlinedButton.styleFrom(
        foregroundColor: color,
        backgroundColor: color.withValues(alpha: .08),
      ),
      icon: Icon(icon, size: 17),
      label: Text(label),
    );
    final palette = MassarPalette.of(context);
    return Wrap(
      spacing: 8,
      runSpacing: 6,
      children: [
        button(
          'بروفايل الطالب',
          Icons.account_box_outlined,
          () => _openModal(() async {
            await Navigator.of(context).push(
              MaterialPageRoute<void>(
                builder: (_) =>
                    StudentProfilePage(store: store, studentId: student.id),
              ),
            );
          }),
          palette.accent,
        ),
        if (onTwin != null)
          button('التوأم', Icons.people_outline, onTwin, palette.accent),
        if (onNote != null)
          button('ملاحظة', Icons.note_add_outlined, onNote, palette.warning),
        if (onCard != null)
          button('الكارت', Icons.badge_outlined, onCard, palette.accent),
        if (store.canCollect)
          button(
            'إلغاء حضور',
            Icons.person_remove_outlined,
            current == null
                ? null
                : () => _cancelRecord(context, attendanceId: current.id),
            palette.error,
          ),
        if (onSuspend != null)
          button(
            'إيقاف الطالب',
            Icons.person_off_outlined,
            onSuspend,
            palette.error,
          ),
      ],
    );
  }

  Widget _summaryItem(
    IconData icon,
    String label,
    String value, {
    String? tooltip,
  }) => Builder(
    builder: (context) => Tooltip(
      message: tooltip ?? '$label: $value',
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 18, color: MassarPalette.of(context).accent),
          const SizedBox(width: 6),
          Text('$label: '),
          _value(value),
        ],
      ),
    ),
  );

  Future<void> _showFullHistory(BuildContext context) => _openModal(() async {
    if (!context.mounted) return;
    var cancellationOpen = false;
    await showDialog<void>(
      context: context,
      builder: (context) => Directionality(
        textDirection: TextDirection.rtl,
        child: ScrollableMassarDialog(
          width: 1100,
          contentPadding: const EdgeInsets.all(8),
          content: SizedBox(
            width: 1050,
            height: 650,
            child: StudentHistoryPanel(
              store: store,
              student: student,
              expanded: true,
              actionsEnabled: actionsEnabled,
              collectionSessionId: collectionSessionId,
              onOpenModal: (work) async {
                if (cancellationOpen) return;
                cancellationOpen = true;
                try {
                  await work();
                } finally {
                  cancellationOpen = false;
                }
              },
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('رجوع للتحضير'),
            ),
          ],
        ),
      ),
    );
  });

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: store,
    builder: (context, _) => _buildHistory(context),
  );

  Widget _buildHistory(BuildContext context) {
    final history = store.studentHistoryFor(student.id);
    final attendance = history.attendances.toList()
      ..sort((a, b) {
        final first = _session(a.sessionId);
        final second = _session(b.sessionId);
        return first != null && second != null
            ? compareSessionsNewestFirst(first, second)
            : b.recordedAt.compareTo(a.recordedAt);
      });
    final academics = history.academics.toList()
      ..sort((a, b) {
        final first = _session(a.sessionId);
        final second = _session(b.sessionId);
        final classOrder = first != null && second != null
            ? compareSessionsNewestFirst(first, second)
            : b.updatedAt.compareTo(a.updatedAt);
        if (classOrder != 0) return classOrder;
        final updateOrder = b.updatedAt.compareTo(a.updatedAt);
        return updateOrder != 0 ? updateOrder : b.id.compareTo(a.id);
      });
    final payments = history.payments.toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
    final cardPayments = history.cardPayments.toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
    final centerFees = history.centerFees.toList()
      ..sort((a, b) => b.recordedAt.compareTo(a.recordedAt));
    final settlements = history.settlements.toList()
      ..sort((a, b) => b.createdAt.compareTo(a.createdAt));
    final debt = store.canCollect ? store.studentDebtFor(student.id) : 0;
    final activePaymentIds = history.activePaymentIds;
    final refunds = history.refunds.toList()
      ..sort((first, second) => second.createdAt.compareTo(first.createdAt));
    final corrections =
        history.corrections
            .where(
              (correction) =>
                  store.canCollect || correction.attendanceId != null,
            )
            .toList()
          ..sort(
            (first, second) => second.createdAt.compareTo(first.createdAt),
          );
    final allAttendanceById = {
      for (final record in history.allAttendances) record.id: record,
    };
    final activitiesById = history.activitiesById;
    final examRecords = academics
        .where(
          (record) =>
              record.activityId == null ||
              activitiesById[record.activityId]?.kind ==
                  AcademicActivityKind.exam,
        )
        .toList();
    final homeworkRecords = academics
        .where(
          (record) =>
              record.activityId == null ||
              activitiesById[record.activityId]?.kind ==
                  AcademicActivityKind.homework,
        )
        .toList();
    String academicLabel(AcademicRecord record) =>
        '${record.activityId == null ? 'رصد سابق بدون اسم' : activitiesById[record.activityId]?.name ?? 'نشاط غير متاح'} · ${_sessionLabel(record.sessionId)} · ${_session(record.sessionId) == null ? shortDate(record.updatedAt) : sessionDateLabel(_session(record.sessionId)!)}';
    final scored = examRecords
        .where((record) => record.score != null || record.examAbsent)
        .toList();
    final reviewed = homeworkRecords
        .where((record) => record.homework != HomeworkStatus.notReviewed)
        .toList();
    final presence = attendance
        .where((record) => record.status != AttendanceStatus.absent)
        .length;
    final lastScores = scored
        .where((record) => record.score != null)
        .take(3)
        .toList()
        .reversed
        .map(_scoreLabel)
        .join('، ');
    final recentAttendance = (expanded ? attendance : attendance.take(4))
        .toList();
    final recentExams = (expanded ? examRecords : examRecords.take(4)).toList();
    final recentHomework =
        (expanded ? homeworkRecords : homeworkRecords.take(4)).toList();
    final attendanceTable = _table(
      [
        'الحصة',
        if (expanded) 'التاريخ',
        if (expanded) 'وقت الحضور',
        'الحضور',
        if (store.canCollect) 'الحساب',
        if (expanded) 'التعويض',
      ],
      recentAttendance
          .map(
            (record) => [
              expanded
                  ? _sessionLabel(record.sessionId)
                  : 'شهر ${_session(record.sessionId)?.monthNumber ?? '—'} · حصة ${_session(record.sessionId)?.number ?? '—'}',
              if (expanded)
                _session(record.sessionId) == null
                    ? shortDate(record.recordedAt)
                    : sessionDateLabel(_session(record.sessionId)!),
              if (expanded)
                record.recordedAtKnown
                    ? _paymentTime(record.recordedAt)
                    : 'وقت الحضور غير موثق',
              expanded
                  ? _attendanceLabel(record)
                  : switch (record.status) {
                      AttendanceStatus.present => 'حاضر',
                      AttendanceStatus.makeup => 'معوّض',
                      AttendanceStatus.absent => 'غائب',
                    },
              if (store.canCollect) _accountLabel(record),
              if (expanded) _makeupLabel(record),
            ],
          )
          .toList(),
      'سجل الحضور يظهر بعد تسجيل أول حصة.',
      cell: (row, column) => !expanded && column == 1
          ? _stateCell(
              context,
              _attendanceLabel(recentAttendance[row]),
              good: recentAttendance[row].status == AttendanceStatus.present,
            )
          : null,
      action: !store.canCollect
          ? null
          : (index) {
              final record = recentAttendance[index];
              if (record.status == AttendanceStatus.absent) {
                return const Text('—');
              }
              return IconButton(
                key: ValueKey('cancel-attendance-${record.id}'),
                onPressed: actionsEnabled
                    ? () => _cancelRecord(context, attendanceId: record.id)
                    : null,
                tooltip: 'إلغاء الحضور',
                icon: const Icon(Icons.person_remove_outlined, size: 18),
              );
            },
    );
    String nameOf(AcademicRecord record) => expanded
        ? academicLabel(record)
        : activitiesById[record.activityId]?.name ??
              'امتحان الحصة ${_session(record.sessionId)?.number ?? ''}';
    final examsTable = _table(
      ['الامتحان', 'الدرجة'],
      recentExams
          .map((record) => [nameOf(record), _scoreLabel(record)])
          .toList(),
      'لم تُرصد امتحانات بعد.',
      cell: (row, column) =>
          column == 1 ? _examScore(context, recentExams[row]) : null,
    );
    final homeworkTable = _table(
      ['الواجب والحصة', 'الحالة'],
      recentHomework
          .map(
            (record) => [
              expanded
                  ? academicLabel(record)
                  : activitiesById[record.activityId]?.name ??
                        'واجب الحصة ${_session(record.sessionId)?.number ?? ''}',
              switch (record.homework) {
                HomeworkStatus.complete => 'اتعمل',
                HomeworkStatus.missing => 'لم يعمل',
                HomeworkStatus.incomplete => 'ناقص',
                _ => 'لم يُرصد',
              },
            ],
          )
          .toList(),
      'لم تُرصد واجبات بعد.',
      cell: (row, column) =>
          column == 1 &&
              recentHomework[row].homework != HomeworkStatus.notReviewed
          ? _stateCell(
              context,
              homeworkLabel(recentHomework[row].homework),
              good: recentHomework[row].homework == HomeworkStatus.complete,
              warning:
                  recentHomework[row].homework == HomeworkStatus.incomplete,
            )
          : null,
    );
    return MassarScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (!expanded) ...[
            _toolbar(context, attendance),
            const SizedBox(height: 10),
          ],
          Row(
            children: [
              Expanded(
                child: Text(
                  'سجل ${student.name}',
                  style: const TextStyle(
                    fontSize: 19,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              if (!expanded)
                TextButton.icon(
                  onPressed: actionsEnabled
                      ? () => _showFullHistory(context)
                      : null,
                  icon: const Icon(Icons.description_outlined, size: 18),
                  label: const Text('عرض السجل الكامل'),
                ),
              if (expanded) const Text('السجل الكامل'),
            ],
          ),
          const SizedBox(height: 8),
          if (expanded)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
              decoration: BoxDecoration(
                color: MassarPalette.of(context).subtle,
                border: Border.all(color: MassarPalette.of(context).line),
                borderRadius: BorderRadius.circular(6),
              ),
              child: Wrap(
                spacing: 18,
                runSpacing: 8,
                children: [
                  _summaryItem(
                    Icons.people_outline,
                    'الحضور',
                    '$presence من ${attendance.length}',
                  ),
                  _summaryItem(
                    Icons.description_outlined,
                    'آخر امتحان',
                    scored.isEmpty ? 'لم يُرصد' : _scoreLabel(scored.first),
                    tooltip: scored.isEmpty
                        ? null
                        : '${activitiesById[scored.first.activityId]?.name ?? 'رصد سابق'}: ${_scoreLabel(scored.first)}',
                  ),
                  _summaryItem(
                    Icons.menu_book_outlined,
                    'آخر واجب',
                    reviewed.isEmpty
                        ? 'لم يُراجع'
                        : homeworkLabel(reviewed.first.homework),
                    tooltip: reviewed.isEmpty
                        ? null
                        : '${activitiesById[reviewed.first.activityId]?.name ?? 'رصد سابق'}: ${homeworkLabel(reviewed.first.homework)}',
                  ),
                  _summaryItem(
                    Icons.bar_chart_outlined,
                    'آخر ٣ درجات',
                    lastScores.isEmpty ? 'لم تُرصد' : lastScores,
                  ),
                ],
              ),
            ),
          const SizedBox(height: 12),
          if (store.canCollect && (expanded || debt > 0)) ...[
            Row(
              children: [
                Expanded(
                  child: Text(
                    'المديونية المتبقية: ${money(debt)}',
                    style: TextStyle(
                      color: debt > 0 ? MassarPalette.of(context).error : null,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                TextButton.icon(
                  onPressed: actionsEnabled && debt > 0
                      ? () => _openModal(
                          () => showStudentDebtDialog(
                            context,
                            store,
                            student,
                            sessionId: collectionSessionId,
                          ),
                        )
                      : null,
                  icon: const Icon(Icons.payments_outlined),
                  label: const Text('تسديد المديونية'),
                ),
              ],
            ),
            const SizedBox(height: 12),
          ],
          if (!expanded && notesPanel != null)
            _beside(
              _section(
                'الحصص والحضور',
                Icons.event_available_outlined,
                attendanceTable,
              ),
              _section('الملاحظات', Icons.sticky_note_2_outlined, notesPanel!),
            )
          else
            _section(
              'الحصص والحضور',
              Icons.event_available_outlined,
              attendanceTable,
            ),
          const SizedBox(height: 12),
          _beside(
            _section(
              'الامتحانات السابقة',
              Icons.description_outlined,
              examsTable,
            ),
            _section(
              'الواجبات السابقة',
              Icons.menu_book_outlined,
              homeworkTable,
            ),
          ),
          const SizedBox(height: 14),
          if (store.canCollect)
            _section(
              'المدفوعات الأصلية',
              Icons.receipt_long_outlined,
              _table(
                [
                  'البيان',
                  'حالة الدفع الحالية',
                  expanded ? 'المحصّل وقت العملية' : 'المدفوع',
                  'المتبقي',
                  'التاريخ والوقت',
                  if (expanded) 'المستحق',
                  if (expanded) 'المحصّل حتى الآن',
                  if (expanded) 'قبل الخصم',
                  if (expanded) 'الخصم',
                  if (expanded) 'الطريقة السارية',
                  if (expanded) 'الطريقة الأصلية',
                ],
                payments
                    .map(
                      (record) => [
                        record.description,
                        _paymentStatus(record, activePaymentIds),
                        money(
                          expanded
                              ? record.collectedAmount
                              : store.paymentCollectedFor(record.id),
                        ),
                        money(store.paymentDebtFor(record.id)),
                        record.createdAtKnown
                            ? _paymentTime(record.createdAt)
                            : 'وقت الدفع غير موثق — من السجل القديم',
                        if (expanded) money(record.netAmount),
                        if (expanded)
                          money(store.paymentCollectedFor(record.id)),
                        if (expanded) money(record.baseAmount),
                        if (expanded) '${percentText(record.discountPercent)}٪',
                        if (expanded) store.effectivePaymentMethod(record.id),
                        if (expanded) record.method,
                      ],
                    )
                    .toList(),
                'لم تُسجل مدفوعات بعد.',
                action: (index) {
                  final record = payments[index];
                  return TextButton(
                    key: ValueKey('cancel-payment-${record.id}'),
                    onPressed:
                        actionsEnabled && activePaymentIds.contains(record.id)
                        ? () => _cancelRecord(context, paymentId: record.id)
                        : null,
                    child: Text(
                      activePaymentIds.contains(record.id)
                          ? 'إلغاء الدفع'
                          : 'ملغاة',
                    ),
                  );
                },
              ),
            ),
          if (store.canCollect && centerFees.isNotEmpty) ...[
            const SizedBox(height: 14),
            _section(
              'رسوم السنتر — مستقلة عن دفع المدرس',
              Icons.storefront_outlined,
              _table(
                [
                  'الحصة',
                  'النوع',
                  'حالة الدفع',
                  'المستحق الأصلي',
                  'المحصّل وقت الحركة',
                  'المحصّل حتى الآن',
                  'المتبقي',
                  'تاريخ ووقت الدفع',
                  'ملاحظة المصدر',
                ],
                centerFees.map((fee) {
                  final isSettlement = fee.originalFeeId != null;
                  final remaining = store.centerFeeRemainingFor(
                    student.id,
                    fee.sessionId,
                  );
                  return [
                    _sessionLabel(fee.sessionId),
                    isSettlement ? 'سداد رسوم سنتر' : 'رسوم السنتر',
                    isSettlement
                        ? 'سداد مسجل'
                        : _collectionStatus(
                            fee.amount,
                            fee.amount - remaining,
                            remaining,
                          ),
                    isSettlement ? '—' : money(fee.amount),
                    money(fee.paidAmount),
                    isSettlement ? '—' : money(fee.amount - remaining),
                    isSettlement ? '—' : money(remaining),
                    fee.recordedAtKnown
                        ? _paymentTime(fee.recordedAt)
                        : 'غير معروف — من السجل القديم',
                    fee.sourceNote.isEmpty ? '—' : fee.sourceNote,
                  ];
                }).toList(),
                'لم تسجل رسوم سنتر.',
              ),
            ),
          ],
          if (store.canCollect && cardPayments.isNotEmpty) ...[
            const SizedBox(height: 14),
            _section(
              'رسوم الكارت',
              Icons.badge_outlined,
              _table(
                [
                  'حالة الدفع الحالية',
                  'المحصّل وقت العملية',
                  'المتبقي',
                  'التاريخ والوقت',
                  'المستحق',
                  'المحصّل حتى الآن',
                  'الطريقة',
                ],
                cardPayments
                    .map(
                      (p) => [
                        _collectionStatus(
                          p.netAmount,
                          store.cardCollectedFor(p.id),
                          store.cardDebtFor(p.id),
                        ),
                        money(p.collectedAmount),
                        money(store.cardDebtFor(p.id)),
                        _paymentTime(p.createdAt),
                        money(p.netAmount),
                        money(store.cardCollectedFor(p.id)),
                        p.method,
                      ],
                    )
                    .toList(),
                'لم تسجل رسوم كارت.',
              ),
            ),
          ],
          if (store.canCollect && settlements.isNotEmpty) ...[
            const SizedBox(height: 14),
            _section(
              'تسديدات المديونية',
              Icons.payments_outlined,
              _table(
                [
                  'تاريخ ووقت التسديد',
                  'البيان',
                  'حصة التحصيل',
                  'المبلغ',
                  'الطريقة',
                  'الموظف',
                ],
                settlements.map((p) {
                  final invoice = payments
                      .where((payment) => payment.id == p.paymentId)
                      .firstOrNull;
                  return [
                    _paymentTime(p.createdAt),
                    p.kind == DebtKind.card
                        ? 'رسوم الكارت'
                        : invoice?.description ?? 'دفعة سابقة',
                    p.sessionId == null
                        ? 'تحصيل مستقل'
                        : _sessionLabel(p.sessionId!),
                    money(p.amount),
                    p.method,
                    store.staff
                            .where((staff) => staff.id == p.staffId)
                            .firstOrNull
                            ?.name ??
                        '—',
                  ];
                }).toList(),
                'لا توجد تسديدات.',
              ),
            ),
          ],
          if (store.canCollect && refunds.isNotEmpty) ...[
            const SizedBox(height: 14),
            _section(
              'الاستردادات',
              Icons.currency_exchange_outlined,
              _table(
                [
                  'الحصة',
                  'تاريخ ووقت الرد',
                  'المبلغ المُعاد',
                  'الطريقة',
                  'السبب',
                  'الموظف',
                ],
                (expanded ? refunds : refunds.take(3))
                    .map(
                      (refund) => [
                        refund.sessionId == null
                            ? 'غير مرتبطة بحصة'
                            : _sessionLabel(refund.sessionId!),
                        _paymentTime(refund.createdAt),
                        money(refund.amount),
                        refund.method,
                        refund.reason,
                        store.staff
                            .firstWhere((staff) => staff.id == refund.staffId)
                            .name,
                      ],
                    )
                    .toList(),
                'لا توجد استردادات.',
              ),
            ),
          ],
          if (expanded && store.canCollect && corrections.isNotEmpty) ...[
            const SizedBox(height: 14),
            _section(
              'سجل التصحيحات',
              Icons.history_outlined,
              _table(
                [
                  'التاريخ والوقت',
                  'الحصة',
                  'التصحيح',
                  'قبل',
                  'بعد',
                  'السبب',
                  'الموظف',
                ],
                (expanded ? corrections : corrections.take(4)).map((
                  correction,
                ) {
                  final original = allAttendanceById[correction.attendanceId];
                  final replacement =
                      allAttendanceById[correction.replacementAttendanceId];
                  return [
                    _paymentTime(correction.createdAt),
                    correction.sessionId == null
                        ? '—'
                        : _sessionLabel(correction.sessionId!),
                    CenterReports.correctionLabel(correction.action),
                    correction.oldMethod ??
                        (correction.action == CorrectionAction.packageRefund
                            ? 'باقة سارية'
                            : original == null
                            ? '—'
                            : _historicalEntryLabel(original)),
                    correction.newMethod ??
                        (correction.action == CorrectionAction.packageRefund
                            ? 'باقة مستردة'
                            : replacement == null
                            ? (original == null ? '—' : 'أُلغي التسجيل')
                            : _historicalEntryLabel(replacement)),
                    correction.reason,
                    store.staff
                        .firstWhere((staff) => staff.id == correction.staffId)
                        .name,
                  ];
                }).toList(),
                'لا توجد تصحيحات.',
              ),
            ),
          ],
        ],
      ),
    );
  }
}
