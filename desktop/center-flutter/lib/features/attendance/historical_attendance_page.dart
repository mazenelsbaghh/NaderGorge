import 'package:flutter/material.dart';

import '../../application/center_store.dart';
import '../../application/whatsapp_attendance.dart';
import '../../domain/models.dart';
import '../../domain/student_lookup.dart';
import '../../shared/formatters.dart';
import '../../shared/scrollable_dialog.dart';
import '../../shared/student_lookup_dialog.dart';
import '../../shared/workspace_draft_guard.dart';

class HistoricalAttendancePage extends StatefulWidget {
  const HistoricalAttendancePage({
    super.key,
    required this.store,
    this.initialSessionId,
  });

  final CenterStore store;
  final String? initialSessionId;

  @override
  State<HistoricalAttendancePage> createState() =>
      _HistoricalAttendancePageState();
}

class _HistoricalAttendancePageState extends State<HistoricalAttendancePage> {
  final _form = GlobalKey<FormState>();
  final _code = TextEditingController();
  final _reason = TextEditingController();
  final _codeFocus = FocusNode();
  String? _sessionId, _studentId, _error, _saved;
  bool _busy = false;
  int _sessionPickerRevision = 0;

  CenterStore get _store => widget.store;
  LessonSession? get _session {
    final session = _store.sessionById(_sessionId);
    return session?.status == SessionStatus.closed ? session : null;
  }

  bool get _enabled => !_busy && _store.canRecordHistoricalAttendance;

  @override
  void initState() {
    super.initState();
    _sessionId = widget.initialSessionId;
  }

  @override
  void dispose() {
    _code.dispose();
    _reason.dispose();
    _codeFocus.dispose();
    super.dispose();
  }

  String _lessonLabel(LessonSession session) =>
      '${sessionLabel(session)} · ${sessionDateLabel(session)} · ${_store.groupLabel(session.groupId)}';

  Future<void> _selectSession(String? id) async {
    if (!_enabled || id == _session?.id) return;
    setState(() => _busy = true);
    try {
      final discard = await confirmDiscardDraft(
        context,
        dirty: _reason.text.trim().isNotEmpty,
      );
      if (!mounted || !discard) return;
      setState(() {
        _sessionId = id;
        _studentId = null;
        _code.clear();
        _reason.clear();
        _error = _saved = null;
      });
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
          _sessionPickerRevision++;
        });
        if (_sessionId == id && _session != null) _codeFocus.requestFocus();
      }
    }
  }

  Future<void> _lookup() async {
    if (!_enabled || _session == null || _studentId != null) return;
    setState(() {
      _busy = true;
      _studentId = _error = _saved = null;
    });
    try {
      final matches = studentsWithIdentifier(_store.students, _code.text);
      final student = switch (matches.length) {
        0 => null,
        1 => matches.single,
        _ => await chooseMatchingStudent(context, matches),
      };
      if (!mounted) return;
      setState(() {
        _studentId = student?.id;
        if (matches.isEmpty) {
          _error = 'راجع كود الطالب أو امسح الكارت مرة تانية.';
        }
        if (student != null) _code.text = student.code;
      });
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _save(AttendanceRecord absence) async {
    if (!_enabled || !_form.currentState!.validate()) return;
    final student = _store.studentById(absence.studentId)!;
    setState(() {
      _busy = true;
      _error = _saved = null;
    });
    try {
      await _store.recordHistoricalAttendance(
        attendanceId: absence.id,
        reason: _reason.text,
      );
      if (!mounted) return;
      setState(() {
        _saved = 'اتحفظ حضور ${student.name} · كود ${student.code}';
        _studentId = null;
        _code.clear();
        _reason.clear();
      });
    } on CenterException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) {
        setState(() => _busy = false);
        if (_saved != null) _codeFocus.requestFocus();
      }
    }
  }

  Widget _sessionField(List<LessonSession> sessions) =>
      DropdownButtonFormField<String>(
        key: ValueKey((_session?.id, _sessionPickerRevision)),
        initialValue: _session?.id,
        isExpanded: true,
        decoration: const InputDecoration(labelText: 'الحصة القديمة'),
        items: [
          for (final session in sessions)
            DropdownMenuItem(
              value: session.id,
              child: Text(
                _lessonLabel(session),
                overflow: TextOverflow.ellipsis,
              ),
            ),
        ],
        onChanged: _enabled ? _selectSession : null,
      );

  Widget _codeField() => TextField(
    key: const Key('historical-attendance-code'),
    controller: _code,
    focusNode: _codeFocus,
    autofocus: _session != null,
    enabled: _enabled && _session != null,
    textInputAction: TextInputAction.search,
    decoration: InputDecoration(
      labelText: 'كود الطالب أو الباركود',
      suffixIcon: IconButton(
        tooltip: 'بحث عن الطالب',
        onPressed: _enabled && _session != null ? _lookup : null,
        icon: const Icon(Icons.search),
      ),
    ),
    onChanged: (_) => setState(() {
      _studentId = _error = _saved = null;
      _reason.clear();
    }),
    onSubmitted: (_) => _lookup(),
  );

  Widget _studentForm(
    Student student,
    AttendanceRecord? absence,
    String status,
  ) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Text(
        '${student.name} · كود ${student.code}',
        style: Theme.of(context).textTheme.titleMedium,
      ),
      Text(status),
      if (absence != null) ...[
        const SizedBox(height: 12),
        TextFormField(
          controller: _reason,
          enabled: _enabled,
          maxLength: 2000,
          decoration: const InputDecoration(labelText: 'سبب التصحيح'),
          validator: (reason) =>
              reason!.trim().isEmpty ? 'اكتب سبب التصحيح.' : null,
        ),
        Align(
          alignment: AlignmentDirectional.centerStart,
          child: FilledButton.icon(
            onPressed: _enabled ? () => _save(absence) : null,
            icon: const Icon(Icons.check),
            label: const Text('حفظ الحضور'),
          ),
        ),
      ],
    ],
  );

  Widget _studentPanel(Student student, LessonSession session) {
    final attendance = _store.attendancesForStudent(student.id);
    final entry = attendance
        .where((record) => record.sessionId == session.id)
        .firstOrNull;
    final makeups = AttendanceMakeups(_store.sessionById, attendance);
    final status = makeups.statusFor(student.id, session, entry);
    if (student.isSuspended) {
      return _studentForm(
        student,
        null,
        'الطالب موقوف. فعّله قبل تسجيل الحضور.',
      );
    }
    return _studentForm(
      student,
      status == AttendanceStatus.absent ? entry : null,
      switch (status) {
        AttendanceStatus.present => 'حضور الطالب مسجل بالفعل.',
        AttendanceStatus.makeup => 'الحصة معوّضة بالفعل.',
        AttendanceStatus.absent =>
          'غياب مسجل. يمكنك تصحيحه إلى حضور بدون تحصيل.',
        null => 'لا يوجد سجل غياب لهذا الطالب في الحصة المختارة.',
      },
    );
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: _store,
    builder: (context, _) {
      if (!_store.canRecordHistoricalAttendance) {
        return const Center(child: Text('سجّل الدخول لتحضير حصة قديمة.'));
      }
      final sessions =
          _store.sessions
              .where((session) => session.status == SessionStatus.closed)
              .toList()
            ..sort(compareSessionsNewestFirst);
      final student = _store.studentById(_studentId);
      final session = _session;
      return WorkspaceDraftRegistration(
        dirty: _reason.text.trim().isNotEmpty,
        busy: _busy,
        child: MassarScrollView(
          padding: const EdgeInsets.all(24),
          child: Align(
            alignment: AlignmentDirectional.topStart,
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 750),
              child: Form(
                key: _form,
                onChanged: () => setState(() {}),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'تحضير حصة قديمة',
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 6),
                    const Text(
                      'اختار الحصة، واكتب كود الطالب أو امسح الكارت واضغط Enter.',
                    ),
                    const SizedBox(height: 20),
                    if (sessions.isEmpty)
                      const Text('مفيش حصص مغلقة حاليًا.')
                    else ...[
                      _sessionField(sessions),
                      const SizedBox(height: 12),
                      _codeField(),
                      if (_busy) const LinearProgressIndicator(),
                      if (student != null && session != null) ...[
                        const SizedBox(height: 16),
                        _studentPanel(student, session),
                      ],
                      if (_saved != null) ...[
                        const SizedBox(height: 12),
                        Text(
                          _saved!,
                          key: const Key('historical-attendance-saved'),
                        ),
                      ],
                      if (_error != null) ...[
                        const SizedBox(height: 12),
                        Text(
                          _error!,
                          style: TextStyle(
                            color: Theme.of(context).colorScheme.error,
                          ),
                        ),
                      ],
                    ],
                  ],
                ),
              ),
            ),
          ),
        ),
      );
    },
  );
}
