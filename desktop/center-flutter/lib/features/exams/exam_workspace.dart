import 'package:flutter/material.dart';

import '../../domain/models.dart';
import '../../domain/center_exam_integration.dart';
import '../../exams/exam_room_controller.dart';
import '../../shared/notice_dialog.dart';
import '../../shared/theme.dart';
import '../../shared/formatters.dart';
import '../../shared/workspace_navigation.dart';
import '../management/management_widgets.dart';
import 'exam_admin_view.dart';
import 'exam_admin_navigation.dart';
import 'exam_network_page.dart';
import 'host_exam_import_page.dart';

class ExamWorkspace extends StatefulWidget {
  const ExamWorkspace({super.key, required this.controller});
  final ExamRoomController controller;
  @override
  State<ExamWorkspace> createState() => _ExamWorkspaceState();
}

class _ExamWorkspaceState extends State<ExamWorkspace> {
  int _tab = 0;
  bool _openingLocal = false, _leaving = false, _allowPop = false;
  final Set<int> _visited = {};
  final _navigation = ExamAdminNavigation();

  @override
  void initState() {
    super.initState();
    _tab = widget.controller.clientOnly ? 0 : 3;
    _visited.add(_tab);
  }

  @override
  void dispose() {
    _navigation.dispose();
    super.dispose();
  }

  Future<void> _enterLocalExamMode() async {
    if (_openingLocal) return;
    setState(() => _openingLocal = true);
    try {
      await widget.controller.enterLocalExamMode();
    } catch (error) {
      if (mounted) {
        await showMassarNotice(
          context,
          managementError(error),
          kind: NoticeKind.error,
        );
      }
    } finally {
      if (mounted) setState(() => _openingLocal = false);
    }
  }

  Future<void> _leaveFor(VoidCallback action) async {
    if (_leaving || _navigation.leaving) return;
    _leaving = true;
    try {
      final allowed = _tab != 0 || await _navigation.confirmLeave();
      if (allowed && mounted) action();
    } finally {
      _leaving = false;
    }
  }

  void _returnToRegistration() => _leaveFor(() {
    setState(() => _allowPop = true);
    Navigator.of(context).pop();
  });

  void _selectPage(String page) {
    if (_leaving || _navigation.leaving) return;
    if (page.startsWith('web:')) {
      _navigation.request(page.substring(4));
      setState(() {
        _tab = 0;
        _visited.add(0);
      });
      return;
    }
    final next = switch (page) {
      'prepared' => 1,
      'network' => 2,
      'import' => 3,
      _ => _tab,
    };
    if (_tab == next) return;
    _leaveFor(
      () => setState(() {
        _tab = next;
        _visited.add(next);
      }),
    );
  }

  String get _selectedPage => switch (_tab) {
    0 => 'web:${_navigation.destination}',
    1 => 'prepared',
    2 => 'network',
    _ => 'import',
  };

  Widget _sidebar(ExamRoomController controller) => MassarWorkspaceNavigation(
    listKey: const Key('exam-navigation'),
    selectedId: _selectedPage,
    onSelected: _selectPage,
    sections: [
      if (controller.canAdmin && !_openingLocal) ...[
        WorkspaceNavigationSection(
          id: 'web:room',
          label: controller.clientOnly ? 'القاعة والتصحيح' : 'إدارة الامتحان',
          icon: Icons.quiz_outlined,
        ),
        const WorkspaceNavigationSection(
          id: 'web:exams',
          label: 'مكتبة الامتحانات',
          icon: Icons.library_books_outlined,
        ),
        if (!controller.clientOnly)
          const WorkspaceNavigationSection(
            id: 'import',
            label: 'تحميل درجات الساكند',
            icon: Icons.download_outlined,
          ),
        WorkspaceNavigationSection(
          id: 'network',
          label: controller.clientOnly
              ? 'الشبكة والـOmada'
              : 'التهيئة والبورتال',
          icon: Icons.router_outlined,
        ),
        const WorkspaceNavigationSection(
          id: 'web:sheets',
          label: 'أوراق الإجابة',
          icon: Icons.description_outlined,
        ),
        const WorkspaceNavigationSection(
          id: 'web:whatsapp',
          label: 'واتساب الامتحان',
          icon: Icons.chat_outlined,
        ),
        const WorkspaceNavigationSection(
          id: 'web:storage',
          label: 'النسخ والملفات',
          icon: Icons.folder_outlined,
        ),
        if (!controller.clientOnly)
          const WorkspaceNavigationSection(
            id: 'prepared',
            label: 'تجهيز الحصة وكشف الطلاب',
            icon: Icons.fact_check_outlined,
          ),
        const WorkspaceNavigationSection(
          id: 'web:guide',
          label: 'دليل التشغيل',
          icon: Icons.help_outline,
        ),
      ],
    ],
    actions: [
      Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16),
        child: FilledButton.icon(
          key: const Key('exam-return-registration'),
          onPressed: _returnToRegistration,
          icon: const Icon(Icons.qr_code_scanner),
          label: const Text('العودة للتسجيل'),
        ),
      ),
    ],
    userName:
        controller.store.currentUser?.name ??
        (controller.clientOnly ? 'الساكند' : 'الهوست'),
    userSubtitle: controller.store.currentUser == null
        ? 'الامتحان داخل القاعة'
        : staffRoleLabel(controller.store.currentUser!.role),
    footerAction: const Padding(
      padding: EdgeInsets.symmetric(horizontal: 20, vertical: 8),
      child: Text(
        'مسار · إدارة الامتحانات',
        textAlign: TextAlign.center,
        style: TextStyle(color: Color(0xFFCDDBE5), fontSize: 12),
      ),
    ),
  );

  Widget _entry(ExamRoomController controller) => Center(
    child: SingleChildScrollView(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 620),
        child: Padding(
          padding: const EdgeInsets.all(28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Icon(Icons.meeting_room_outlined, size: 40),
              const SizedBox(height: 20),
              Text(
                controller.clientOnly
                    ? 'افتح الامتحان على الساكند'
                    : 'افتح الامتحانات من حساب الموظف',
                style: Theme.of(context).textTheme.headlineSmall,
              ),
              const SizedBox(height: 12),
              Text(
                controller.clientOnly
                    ? 'اختَر نموذج الأسئلة وافتح القاعة. الطالب يدخل باسمه ورقم الموبايل والكود، والإجابات والتصحيح يفضلوا على الساكند. بعد الامتحان ارجع وصّل الهوست وحمّل الدرجات منه.'
                    : 'سجّل دخول الموظف لفتح الإدارة. خدمة الطلاب تستمر في الخلفية.',
              ),
              const SizedBox(height: 24),
              FilledButton.icon(
                onPressed: _openingLocal
                    ? null
                    : controller.clientOnly
                    ? _enterLocalExamMode
                    : _returnToRegistration,
                icon: Icon(
                  controller.clientOnly
                      ? Icons.quiz_outlined
                      : Icons.arrow_back,
                ),
                label: Text(
                  _openingLocal
                      ? 'جارٍ فتح الامتحان…'
                      : controller.clientOnly
                      ? 'فتح امتحان الساكند'
                      : 'العودة للدخول',
                ),
              ),
            ],
          ),
        ),
      ),
    ),
  );

  Widget _status(ExamRoomController controller) => Material(
    color: MassarPalette.of(context).subtle,
    child: Padding(
      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
      child: Row(
        children: [
          Icon(
            controller.error != null
                ? Icons.info_outline
                : Icons.check_circle_outline,
            size: 18,
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              controller.error ??
                  (controller.clientOnly
                      ? controller.canSync
                            ? 'الساكند متصل بالهوست · التسجيل متاح على الجهازين'
                            : 'الامتحان والإجابات محفوظان على الساكند · أعد الربط بعد التصحيح'
                      : 'التسجيل مستمر · حمّل درجات الساكند بعد إعادة توصيل الجهازين'),
            ),
          ),
        ],
      ),
    ),
  );

  Widget _content(ExamRoomController controller) {
    if (!controller.canAdmin || _openingLocal) return _entry(controller);
    final isHost = !controller.clientOnly && !controller.store.isRemote;
    return Column(
      children: [
        _status(controller),
        Expanded(
          child: IndexedStack(
            index: _tab,
            children: [
              if (_visited.contains(0))
                ExamAdminView(controller: controller, navigation: _navigation)
              else
                const SizedBox.shrink(),
              if (!controller.clientOnly && _visited.contains(1))
                ExamResultsPage(
                  controller: controller,
                  onPrepared: () => _selectPage('web:room'),
                )
              else
                const SizedBox.shrink(),
              // Network inspection starts only on an explicit visit to its page.
              if (_tab == 2)
                ExamNetworkPage(controller: controller, isHost: isHost)
              else
                const SizedBox.shrink(),
              if (!controller.clientOnly && _visited.contains(3))
                HostExamImportPage(store: controller.store)
              else
                const SizedBox.shrink(),
            ],
          ),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) => AnimatedBuilder(
    animation: Listenable.merge([widget.controller, _navigation]),
    builder: (context, _) => PopScope(
      canPop: _allowPop,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _returnToRegistration();
      },
      child: Scaffold(
        body: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _sidebar(widget.controller),
            Expanded(
              child: FocusTraversalGroup(child: _content(widget.controller)),
            ),
          ],
        ),
      ),
    ),
  );
}

class ExamResultsPage extends StatefulWidget {
  const ExamResultsPage({super.key, required this.controller, this.onPrepared});
  final ExamRoomController controller;
  final VoidCallback? onPrepared;
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
              ? 'تم فتح القاعة وربطها بكشف الطلاب. جهّز الساكند للقاعة قبل فصل الكابل.'
              : 'تم تحديث كشف الطلاب. تحميل الدرجات يتم من زر الهوست بعد التصحيح وإعادة توصيل الكابل.',
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

  Future<void> _prepareHall() async {
    setState(() {
      _saving = true;
      _error = null;
    });
    final controller = widget.controller;
    try {
      await controller.prepareHallContext(
        groupId: _group!,
        sessionId: _session!,
        activityId: _activity!,
      );
      if (mounted) widget.onPrepared?.call();
    } catch (error) {
      if (mounted) {
        setState(
          () => _error = controller.canSync
              ? managementError(error)
              : 'انقطع اتصال الهوست قبل اكتمال التجهيز. أعد الاتصال، ثم اضغط تجهيز مرة أخرى وانتظر رسالة «جاهز للقاعة».',
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
      title: controller.clientOnly
          ? 'قبل دخول القاعة'
          : 'تجهيز الحصة وكشف الطلاب',
      subtitle: controller.clientOnly
          ? 'وأنت متصل بالهوست، اختر الحصة والامتحان واحفظ كشف الطلاب. بعد «جاهز للقاعة» افتح الأسئلة من «القاعة والتصحيح».'
          : 'اربط الامتحان بالحصة وكشف الطلاب. بعد التصحيح حمّل الدرجات من الهوست.',
      actions: [
        if (!controller.clientOnly)
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
          label: Text(controller.clientOnly ? 'تحديث القائمة' : 'تحديث الرصد'),
        ),
      ],
      child: ManagementBody(
        header: [
          if (!controller.clientOnly && (_creating || controller.exams.isEmpty))
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
              if (!controller.clientOnly && _creating)
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
              else if (!controller.clientOnly)
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
                controller.clientOnly
                    ? 'الحصة التي حضرها الطلاب'
                    : 'الحصة الفعلية',
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
                controller.clientOnly
                    ? 'اسم الامتحان'
                    : 'اسم الامتحان في الرصد',
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
          if (controller.clientOnly && _group != null && sessions.isEmpty)
            const Text(
              'لا توجد حصة مفتوحة لهذه المجموعة. افتح الحصة على الهوست، ثم حدّث البيانات وأعد اختيارها هنا.',
            ),
          if (controller.clientOnly && selected != null && activities.isEmpty)
            const Text(
              'لا يوجد امتحان محدد لهذه الحصة. أضف الامتحان ودرجته النهائية على الهوست، ثم حدّث البيانات.',
            ),
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
              if (!controller.clientOnly)
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
              if (selected != null && activity != null && controller.clientOnly)
                FilledButton.icon(
                  key: const Key('exam-prepare-hall'),
                  onPressed: _saving || !controller.canSync
                      ? null
                      : _prepareHall,
                  icon: const Icon(Icons.meeting_room_outlined),
                  label: Text(
                    _saving
                        ? 'جارٍ حفظ كشف القاعة، لا تفصل الهوست…'
                        : 'تجهيز الساكند للقاعة',
                  ),
                ),
              if (controller.offlinePrepared)
                const Text('جاهز للقاعة • تم حفظ الكشف، ويمكن فصل الهوست الآن'),
              if (linked && !controller.clientOnly)
                const Text('مرتبط بالحصة وكشف الطلاب'),
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
                controller.clientOnly
                    ? 'الحاضرون المسجلون لهذه الحصة: ${progress.attending}'
                    : 'الحاضرون: ${progress.attending}   •   لهم درجة: ${progress.recorded}   •   بدون درجة نهائية: ${progress.missing.length}',
                style: Theme.of(context).textTheme.titleMedium,
              ),
            ),
          if (!controller.clientOnly && conflicts.isNotEmpty)
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
        child: controller.clientOnly
            ? const SizedBox.shrink()
            : progress == null
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
