import 'package:flutter/material.dart';

import '../../domain/models.dart';
import '../../domain/center_exam_integration.dart';
import '../../exams/exam_room_controller.dart';
import '../../shared/notice_dialog.dart';
import '../../shared/theme.dart';
import '../management/management_widgets.dart';
import 'exam_admin_view.dart';
import 'exam_network_page.dart';

class ExamWorkspace extends StatefulWidget {
  const ExamWorkspace({super.key, required this.controller});
  final ExamRoomController controller;
  @override
  State<ExamWorkspace> createState() => _ExamWorkspaceState();
}

class _ExamWorkspaceState extends State<ExamWorkspace> {
  int _tab = 0;
  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: widget.controller,
    builder: (context, _) {
      final controller = widget.controller;
      if (!controller.store.canAssess) {
        return Scaffold(
          appBar: AppBar(title: const Text('الامتحانات')),
          body: const Center(
            child: Text(
              'سجّل دخول الموظف لعرض إدارة الامتحان. خدمة الطلاب تستمر في الخلفية.',
            ),
          ),
        );
      }
      final isHost = !controller.store.isRemote;
      return Scaffold(
        appBar: AppBar(
          title: const Text('امتحانات السنتر'),
          actions: [
            TextButton.icon(
              onPressed: () => Navigator.pop(context),
              icon: const Icon(Icons.how_to_reg),
              label: const Text('العودة للتسجيل'),
            ),
          ],
          bottom: PreferredSize(
            preferredSize: const Size.fromHeight(54),
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: SegmentedButton<int>(
                  segments: const [
                    ButtonSegment(
                      value: 0,
                      label: Text('إدارة الامتحان'),
                      icon: Icon(Icons.quiz_outlined),
                    ),
                    ButtonSegment(
                      value: 1,
                      label: Text('الربط والرصد التلقائي'),
                      icon: Icon(Icons.fact_check_outlined),
                    ),
                    ButtonSegment(
                      value: 2,
                      label: Text('التهيئة والبورتال'),
                      icon: Icon(Icons.router_outlined),
                    ),
                  ],
                  selected: {_tab},
                  onSelectionChanged: (value) =>
                      setState(() => _tab = value.first),
                ),
              ),
            ),
          ),
        ),
        body: Column(
          children: [
            Material(
              color: MassarPalette.of(context).subtle,
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 20,
                  vertical: 8,
                ),
                child: Row(
                  children: [
                    Icon(
                      controller.running
                          ? Icons.check_circle_outline
                          : Icons.info_outline,
                      size: 18,
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        controller.error ??
                            (controller.running
                                ? 'خدمة الطلاب تعمل في الخلفية • التحضير والتحصيل متاحان على الجهازين أثناء الاتصال'
                                : 'افتح إدارة الامتحان لتشغيل الخدمة، ثم اربطه بالمجموعة والحصة.'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
            Expanded(
              child: IndexedStack(
                index: _tab,
                children: [
                  ExamAdminView(controller: controller),
                  ExamResultsPage(controller: controller),
                  // Network inspection is initiated only when the settings tab opens.
                  if (_tab == 2)
                    ExamNetworkPage(controller: controller, isHost: isHost)
                  else
                    const SizedBox.shrink(),
                ],
              ),
            ),
          ],
        ),
      );
    },
  );
}

class ExamResultsPage extends StatefulWidget {
  const ExamResultsPage({super.key, required this.controller});
  final ExamRoomController controller;
  @override
  State<ExamResultsPage> createState() => _ExamResultsPageState();
}

class _ExamResultsPageState extends State<ExamResultsPage> {
  String? _exam, _template, _group, _session, _activity;
  bool _saving = false, _creating = false;
  String? _error;

  void _selectExam(String? value) {
    final bindings = widget.controller.bindings.where(
      (binding) => binding['examId'] == value,
    );
    final binding = bindings.isEmpty ? null : bindings.first;
    setState(() {
      _exam = value;
      _creating = false;
      _group = binding?['groupId'] as String?;
      _session = binding?['sessionId'] as String?;
      _activity = binding?['activityId'] as String?;
      _error = null;
    });
  }

  Future<void> _bind() async {
    if ((_creating ? _template == null : _exam == null) ||
        _group == null ||
        _session == null ||
        _activity == null ||
        _saving) {
      return;
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final creating = _creating;
      if (creating) {
        final examId = await widget.controller.openRoom(
          templateId: _template!,
          groupId: _group!,
          sessionId: _session!,
          activityId: _activity!,
        );
        if (mounted) _selectExam(examId);
      } else {
        await widget.controller.bind(
          examId: _exam!,
          groupId: _group!,
          sessionId: _session!,
          activityId: _activity!,
        );
      }
      if (mounted) {
        await showMassarNotice(
          context,
          creating
              ? 'تم فتح القاعة وربطها بالسنتر. الطلاب مسجلون من كشف المجموعة، والدرجات النهائية تُرصد تلقائيًا.'
              : 'تم ربط الامتحان. بيانات الطلاب تتحدث والدرجات النهائية تُرصد تلقائيًا.',
          kind: NoticeKind.success,
        );
      }
    } catch (error) {
      if (mounted) {
        setState(
          () => _error = error is CenterException
              ? error.message
              : 'تعذر ربط الامتحان. راجع المجموعة والحصة والدرجة النهائية.',
        );
      }
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Widget _choice(
    String label,
    String? value,
    List<DropdownMenuItem<String>> items,
    ValueChanged<String?> onChanged,
  ) => SizedBox(
    width: 270,
    child: DropdownButtonFormField<String>(
      key: ValueKey('$label:$value'),
      initialValue: items.any((item) => item.value == value) ? value : null,
      isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: items,
      onChanged: _saving ? null : onChanged,
    ),
  );

  @override
  Widget build(BuildContext context) {
    final controller = widget.controller;
    final store = controller.store;
    final sessions =
        store.sessions
            .where(
              (session) =>
                  session.groupId == _group &&
                  session.status != SessionStatus.canceled &&
                  store.sessionHasStarted(session.id),
            )
            .toList()
          ..sort((a, b) => b.startsAt.compareTo(a.startsAt));
    final selected = sessions
        .where((session) => session.id == _session)
        .firstOrNull;
    final activities = store.academicActivities
        .where(
          (activity) =>
              activity.kind == AcademicActivityKind.exam &&
              activity.maxScoreKnown &&
              selected != null &&
              activity.appliesToSession(selected),
        )
        .toList();
    final linked = controller.bindings.any(
      (binding) =>
          binding['examId'] == _exam &&
          binding['sessionId'] == _session &&
          binding['activityId'] == _activity,
    );
    final template = controller.templates
        .where((row) => row['id'] == _template)
        .firstOrNull;
    final activity = activities.where((row) => row.id == _activity).firstOrNull;
    final mismatchedScore =
        _creating &&
        template != null &&
        activity != null &&
        template['maxScore'] != activity.maxScore;
    final conflicts = (controller.state['results'] as List? ?? const [])
        .whereType<Map>()
        .where(
          (row) =>
              (_exam == null || row['examId'] == _exam) &&
              controller.resultProblems.containsKey(
                '${row['attemptId']}:${row['version']}',
              ),
        )
        .toList();
    IntegratedExamProgress? progress;
    if (_group != null && _session != null && _activity != null) {
      try {
        progress = store.integratedExamProgress(
          groupId: _group!,
          sessionId: _session!,
          activityId: _activity!,
        );
      } on CenterException {
        /* Stale filters are editable below. */
      }
    }
    return ManagementPanel(
      title: 'الربط والرصد التلقائي',
      subtitle:
          'افتح قاعة من نموذج الأسئلة أو اربط قاعة موجودة ببيانات السنتر. تُرصد النتيجة بعد إغلاق القاعة واكتمال تصحيح المقالي.',
      actions: [
        OutlinedButton.icon(
          key: const Key('exam-create-mode'),
          onPressed: _saving
              ? null
              : () => setState(() {
                  _creating = !_creating;
                  _exam = null;
                  _template = null;
                  _error = null;
                }),
          icon: Icon(_creating ? Icons.link : Icons.add),
          label: Text(_creating ? 'اختيار قاعة موجودة' : 'إنشاء قاعة مرتبطة'),
        ),
        OutlinedButton.icon(
          onPressed: controller.running ? controller.refresh : null,
          icon: const Icon(Icons.sync),
          label: const Text('تحديث الرصد'),
        ),
      ],
      child: ManagementBody(
        header: [
          if (_creating || controller.exams.isEmpty)
            Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: Text(
                _creating
                    ? controller.templates.isEmpty
                          ? 'جهّز نموذج الأسئلة من مكتبة الامتحانات في «إدارة الامتحان»، ثم اضغط «تحديث الرصد».'
                          : 'اختر نموذج الأسئلة والمجموعة والحصة. بيانات الطلاب تأتي من السنتر مباشرة، ولا تحتاج لإضافة المجموعات مرة أخرى.'
                    : 'اختر «إنشاء قاعة مرتبطة» لفتح امتحان من المكتبة باستخدام مجموعات السنتر الحالية.',
              ),
            ),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              if (_creating)
                _choice(
                  'نموذج الأسئلة',
                  _template,
                  controller.templates
                      .map(
                        (template) => DropdownMenuItem(
                          value: template['id'] as String,
                          child: Text(
                            '${template['title'] ?? 'نموذج أسئلة'} • من ${template['maxScore']}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  (value) => setState(() => _template = value),
                )
              else
                _choice(
                  'جلسة الامتحان',
                  _exam,
                  controller.exams
                      .map(
                        (exam) => DropdownMenuItem(
                          value: exam['id'] as String,
                          child: Text(
                            '${exam['title'] ?? exam['name'] ?? 'امتحان'} • ${exam['state'] == 'closed'
                                ? 'انتهى'
                                : exam['state'] == 'running'
                                ? 'جارٍ'
                                : 'تجهيز'}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      )
                      .toList(),
                  _selectExam,
                ),
              _choice(
                'مجموعة السنتر',
                _group,
                store.groups
                    .map(
                      (group) => DropdownMenuItem(
                        value: group.id,
                        child: Text(
                          store.groupLabel(group.id),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    )
                    .toList(),
                (value) => setState(() {
                  _group = value;
                  _session = null;
                  _activity = null;
                }),
              ),
              _choice(
                'الحصة الفعلية',
                _session,
                sessions
                    .map(
                      (session) => DropdownMenuItem(
                        value: session.id,
                        child: Text(
                          'شهر ${session.monthNumber} • ${session.name.isEmpty ? 'حصة ${session.number}' : session.name}',
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    )
                    .toList(),
                (value) => setState(() {
                  _session = value;
                  _activity = null;
                }),
              ),
              _choice(
                'اسم الامتحان في الرصد',
                _activity,
                activities
                    .map(
                      (activity) => DropdownMenuItem(
                        value: activity.id,
                        child: Text(
                          '${activity.name} • من ${activity.maxScore}',
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    )
                    .toList(),
                (value) => setState(() => _activity = value),
              ),
            ],
          ),
          const SizedBox(height: 12),
          if (mismatchedScore)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Text(
                'نموذج الأسئلة من ${template['maxScore']} وامتحان الرصد من ${activity.maxScore}. اختر نموذجًا وامتحانًا بنفس الدرجة النهائية.',
                style: TextStyle(color: MassarPalette.of(context).error),
              ),
            ),
          Wrap(
            spacing: 16,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              FilledButton.icon(
                onPressed:
                    _saving ||
                        !controller.running ||
                        !controller.canSync ||
                        selected == null ||
                        activity == null ||
                        mismatchedScore ||
                        (_creating ? template == null : _exam == null)
                    ? null
                    : _bind,
                key: const Key('exam-link-submit'),
                icon: Icon(
                  _creating
                      ? Icons.meeting_room_outlined
                      : linked
                      ? Icons.check
                      : Icons.link,
                ),
                label: Text(
                  _saving
                      ? (_creating ? 'جارٍ فتح القاعة…' : 'جارٍ الربط…')
                      : _creating
                      ? 'فتح قاعة الامتحان'
                      : linked
                      ? 'تحديث كشف الطلاب'
                      : 'ربط الامتحان بالسنتر',
                ),
              ),
              if (linked)
                const Text('مرتبط • الرصد التلقائي يعمل عند اكتمال التصحيح'),
            ],
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 10),
              child: Text(
                _error!,
                style: TextStyle(color: MassarPalette.of(context).error),
              ),
            ),
          const Divider(height: 32),
          if (progress != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Text(
                'الحاضرون: ${progress.attending}   •   لهم درجة: ${progress.recorded}   •   بدون درجة نهائية: ${progress.missing.length}',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
          if (conflicts.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: OutlinedButton.icon(
                icon: Icon(
                  Icons.report_outlined,
                  color: MassarPalette.of(context).warning,
                ),
                label: Text('مراجعة ${conflicts.length} نتيجة تحتاج تدخلًا'),
                onPressed: () => showDialog<void>(
                  context: context,
                  builder: (context) => AlertDialog(
                    title: const Text('نتائج تحتاج مراجعة'),
                    content: SizedBox(
                      width: 600,
                      height: 360,
                      child: ListView.separated(
                        itemCount: conflicts.length,
                        separatorBuilder: (_, _) => const Divider(),
                        itemBuilder: (context, index) {
                          final row = conflicts[index];
                          final student = store.students
                              .where(
                                (student) => student.id == row['studentId'],
                              )
                              .firstOrNull;
                          return ListTile(
                            title: Text(
                              student == null
                                  ? 'طالب غير موجود في كشف السنتر'
                                  : '${student.code} • ${student.name}',
                            ),
                            subtitle: Text(
                              controller
                                      .resultProblems['${row['attemptId']}:${row['version']}'] ??
                                  'أُعيدت المزامنة؛ حدّث القائمة.',
                            ),
                            trailing: row['score'] == null
                                ? null
                                : Text('${row['score']} / ${row['maxScore']}'),
                          );
                        },
                      ),
                    ),
                    actions: [
                      TextButton(
                        onPressed: () => Navigator.pop(context),
                        child: const Text('إغلاق'),
                      ),
                    ],
                  ),
                ),
              ),
            ),
        ],
        child: progress == null
            ? const Center(
                child: Text(
                  'اختَر الحصة والامتحان لعرض الحاضرين الذين لم تُرصد درجاتهم.',
                ),
              )
            : progress.missing.isEmpty
            ? const Center(
                child: Text('لا يوجد حاضر بدون درجة نهائية لهذا الامتحان.'),
              )
            : ListView.separated(
                itemCount: progress.missing.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (context, index) {
                  final student = progress!.missing[index];
                  return ListTile(
                    leading: SizedBox(
                      width: 65,
                      child: Text(
                        student.code,
                        textDirection: TextDirection.ltr,
                      ),
                    ),
                    title: Text(student.name),
                    subtitle: Text(
                      _exam == null
                          ? 'لم يُرصد'
                          : controller.missingReason(_exam!, student.studentId),
                    ),
                    trailing: const Icon(Icons.pending_outlined),
                  );
                },
              ),
      ),
    );
  }
}
