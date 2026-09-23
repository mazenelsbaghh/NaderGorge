import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../state/parent_controller.dart';
import '../data/parent_api.dart';
import '../data/models.dart';
import 'academic_screens.dart';
import 'design_system.dart';
import 'formatters.dart';
import 'link_flow.dart';

class Dashboard extends StatefulWidget {
  final ParentController controller;
  const Dashboard({super.key, required this.controller});
  @override
  State<Dashboard> createState() => _DashboardState();
}

class _DashboardState extends State<Dashboard> {
  int tab = 0;
  ParentController get controller => widget.controller;
  @override
  Widget build(BuildContext context) {
    final details = controller.details;
    return Scaffold(
      appBar: AppBar(
        centerTitle: true,
        title: const BrandLogo(width: 114),
        leading: IconButton(
          tooltip: 'التنبيهات',
          onPressed: () => openNotifications(context, controller),
          icon: Badge(
            isLabelVisible: controller.notifications.any(
              (n) => !n.flag('isRead'),
            ),
            child: const Icon(Icons.notifications_none_rounded),
          ),
        ),
        actions: [
          IconButton(
            tooltip: 'اختيار الطالب',
            onPressed: () => openStudents(context, controller),
            icon: const Icon(Icons.people_outline),
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 20),
            child: StudentBanner(
              name: controller.active!.name,
              grade: details?.text('grade') ?? '',
              onTap: () => openStudents(context, controller),
            ),
          ),
          if (controller.refreshing)
            const LinearProgressIndicator(minHeight: 2),
          if (controller.failure != null)
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(
                controller.failure!.message,
                style: const TextStyle(color: MassarTokens.danger),
              ),
            ),
          Expanded(
            child: details == null
                ? ScreenBody(
                    children: [
                      if (!controller.refreshing)
                        ErrorPanel(
                          controller.failure?.message ??
                              'تعذر تحميل بيانات الطالب.',
                          retry: controller.refresh,
                        ),
                      if (controller.failure?.unauthorized == true)
                        PrimaryAction(
                          label: 'إعادة ربط الطالب',
                          onPressed: () => linkStudent(context, controller),
                        ),
                      if (controller.refreshing)
                        const EmptyPanel(
                          'جاري تحميل بيانات الطالب…',
                          icon: Icons.hourglass_empty,
                        ),
                    ],
                  )
                : RefreshIndicator(
                    onRefresh: controller.refresh,
                    child: AnimatedSwitcher(
                      duration: MassarMotion.duration(
                        context,
                        MassarMotion.change,
                      ),
                      child: switch (tab) {
                        1 => AcademicList(
                          key: ValueKey(
                            'lessons:${controller.active!.studentId}',
                          ),
                          details: details,
                          lessons: true,
                        ),
                        2 => AcademicList(
                          key: ValueKey(
                            'results:${controller.active!.studentId}',
                          ),
                          details: details,
                          lessons: false,
                        ),
                        3 => MoreScreen(
                          key: const ValueKey("more"),
                          controller: controller,
                        ),
                        _ => HomeOverview(
                          key: const ValueKey("home"),
                          controller: controller,
                          onLessons: () => setState(() => tab = 1),
                          onResults: () => setState(() => tab = 2),
                        ),
                      },
                    ),
                  ),
          ),
        ],
      ),
      bottomNavigationBar: MassarBottomNavigation(
        selectedIndex: tab,
        onChanged: (index) => setState(() => tab = index),
      ),
    );
  }
}

class HomeOverview extends StatelessWidget {
  final ParentController controller;
  final VoidCallback onLessons, onResults;
  const HomeOverview({
    super.key,
    required this.controller,
    required this.onLessons,
    required this.onResults,
  });
  @override
  Widget build(BuildContext context) {
    final details = controller.details!;
    final recent = details.exams.where((row) => row.hasExamGrade).toList()
      ..sort((a, b) => b.text('submittedAt').compareTo(a.text('submittedAt')));
    final lessons = details.lessons.toList()
      ..sort(
        (a, b) => b.text('lastWatchedAt').compareTo(a.text('lastWatchedAt')),
      );
    return ScreenBody(
      children: [
        PageHeading('متابعة ${details.text('studentName')}'),
        if (controller.failure != null)
          SoftPanel(
            child: Column(
              children: [
                const StatusPill('بيانات محفوظة', color: MassarTokens.warning),
                Text(
                  'آخر تحديث: ${displayDate(controller.lastUpdated!.toUtc().toIso8601String())}',
                ),
                TextButton(
                  onPressed: controller.refresh,
                  child: const Text('إعادة المحاولة'),
                ),
              ],
            ),
          ),
        SoftPanel(
          child: Column(
            children: [
              ProgressArc(
                progress: details.progress,
                caption: 'الحصص المكتملة',
                detail:
                    '${details.attendance.count('watchedLessons')} من ${details.attendance.count('totalLessons')} حصة',
              ),
              const Divider(),
              MassarDataRow(
                'الواجبات المسلّمة',
                subtitle:
                    '${details.homeworks.where((h) => h.flag('isSubmitted')).length} من ${details.homeworks.length}',
                icon: Icons.assignment_outlined,
                onTap: onResults,
              ),
            ],
          ),
        ),
        if (details.warnings.isNotEmpty)
          SoftPanel(
            child: MassarDataRow(
              '${details.warnings.length} تنبيه يحتاج مراجعة',
              icon: Icons.warning_amber_rounded,
              onTap: () => openNotifications(context, controller),
            ),
          ),
        SectionHeading('آخر النتائج', onTap: onResults),
        if (recent.isEmpty) const EmptyPanel('لا توجد نتائج مكتملة بعد.'),
        ...recent.take(2).map((row) => AssessmentTile(row: row)),
        SectionHeading('متابعة الحصص', onTap: onLessons),
        ...lessons.take(2).map((row) => LessonTile(row: row)),
        if (lessons.isEmpty)
          const EmptyPanel('ستظهر الحصص هنا بعد تفعيل محتوى للطالب.'),
      ],
    );
  }
}

void linkStudent(BuildContext context, ParentController controller) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => LinkFlow(controller: controller, welcome: false),
      ),
    );

class MoreScreen extends StatelessWidget {
  final ParentController controller;
  const MoreScreen({super.key, required this.controller});
  @override
  Widget build(BuildContext context) => ScreenBody(
    children: [
      const PageHeading('المزيد'),
      SoftPanel(
        child: Column(
          children: [
            MassarDataRow(
              'الطلاب المرتبطون',
              icon: Icons.people_outline,
              onTap: () => openStudents(context, controller),
            ),
            const Divider(height: 1),
            MassarDataRow(
              'بيانات الطالب',
              icon: Icons.person_outline,
              onTap: () => openDetail(context, 'بيانات الطالب', [
                SoftPanel(
                  child: Column(
                    children: [
                      MassarDataRow(
                        'اسم الطالب',
                        subtitle: controller.details!.text('studentName'),
                      ),
                      MassarDataRow(
                        'الصف الدراسي',
                        subtitle: controller.details!.text('grade'),
                      ),
                      MassarDataRow(
                        'المدرسة',
                        subtitle: controller.details!.text('school').isEmpty
                            ? 'غير مسجلة'
                            : controller.details!.text('school'),
                      ),
                    ],
                  ),
                ),
              ]),
            ),
            const Divider(height: 1),
            MassarDataRow(
              'الكورسات المسجلة',
              icon: Icons.menu_book_outlined,
              onTap: () => openCourses(context, controller.details!),
            ),
            const Divider(height: 1),
            MassarDataRow(
              'الرصيد',
              icon: Icons.account_balance_wallet_outlined,
              onTap: () => openBalance(context, controller.details!),
            ),
            const Divider(height: 1),
            MassarDataRow(
              'التنبيهات',
              icon: Icons.notifications_none,
              onTap: () => openNotifications(context, controller),
            ),
          ],
        ),
      ),
      SoftPanel(
        child: Column(
          children: [
            DropdownButtonFormField<ThemeMode>(
              initialValue: controller.themeMode,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'المظهر'),
              items: const [
                DropdownMenuItem(
                  value: ThemeMode.system,
                  child: Text('حسب الجهاز'),
                ),
                DropdownMenuItem(value: ThemeMode.light, child: Text('فاتح')),
                DropdownMenuItem(value: ThemeMode.dark, child: Text('داكن')),
              ],
              onChanged: (mode) {
                if (mode != null) {
                  runAction(context, () => controller.setTheme(mode));
                }
              },
            ),
            MassarDataRow(
              'تفعيل الإشعارات',
              icon: Icons.notifications_active_outlined,
              onTap: () => runAction(context, () async {
                final granted = await controller.bridge.requestNotifications();
                if (granted) {
                  await controller.registerPush();
                } else {
                  await controller.bridge.openSettings();
                }
              }),
            ),
            MassarDataRow(
              'إعدادات إشعارات الجهاز',
              onTap: () => runAction(context, controller.bridge.openSettings),
            ),
            if (controller.notificationError != null)
              Text(controller.notificationError!),
          ],
        ),
      ),
    ],
  );
}

Future<void> runAction(
  BuildContext context,
  Future<void> Function() action,
) async {
  try {
    await action();
  } on ParentFailure catch (failure) {
    if (context.mounted) {
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text(failure.message)));
    }
  } on PlatformException {
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('تعذر إتمام العملية على الجهاز. أعد المحاولة.'),
        ),
      );
    }
  }
}

void openStudents(
  BuildContext context,
  ParentController controller,
) => Navigator.of(context).push(
  MaterialPageRoute<void>(
    builder: (routeContext) => ListenableBuilder(
      listenable: controller,
      builder: (context, _) => Scaffold(
        appBar: AppBar(title: const BrandLogo(width: 114), centerTitle: true),
        body: ScreenBody(
          children: [
            const PageHeading(
              'الطلاب المرتبطون',
              subtitle: 'اختر الطالب لعرض بياناته',
            ),
            ...controller.profiles.map(
              (student) => SoftPanel(
                child: Column(
                  children: [
                    MassarDataRow(
                      student.name,
                      icon: controller.active?.studentId == student.studentId
                          ? Icons.check_circle_outline
                          : Icons.person_outline,
                      subtitle:
                          controller.active?.studentId == student.studentId
                          ? 'الطالب الحالي'
                          : null,
                      onTap: () => runAction(context, () async {
                        await controller.select(student);
                        if (routeContext.mounted) Navigator.pop(routeContext);
                      }),
                    ),
                    TextButton(
                      onPressed: () async {
                        final confirmed = await showDialog<bool>(
                          context: context,
                          builder: (dialogContext) => AlertDialog(
                            title: const Text('إزالة الربط؟'),
                            content: Text(
                              'سيُزال ${student.name} من هذا الجهاز. يمكنك ربطه مجددًا باستخدام الرمز.',
                            ),
                            actions: [
                              TextButton(
                                onPressed: () =>
                                    Navigator.pop(dialogContext, false),
                                child: const Text('إلغاء'),
                              ),
                              TextButton(
                                onPressed: () =>
                                    Navigator.pop(dialogContext, true),
                                child: const Text('إزالة الربط'),
                              ),
                            ],
                          ),
                        );
                        if (confirmed == true && context.mounted) {
                          await runAction(
                            context,
                            () => controller.remove(student),
                          );
                        }
                      },
                      child: const Text('إزالة الربط'),
                    ),
                  ],
                ),
              ),
            ),
            PrimaryAction(
              label: 'ربط طالب جديد',
              onPressed: () => linkStudent(context, controller),
            ),
          ],
        ),
      ),
    ),
  ),
);

void openCourses(BuildContext context, StudentDetails details) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => MassarPage(
          title: 'الكورسات المسجلة',
          body: CourseCatalog(details: details),
        ),
      ),
    );

class CourseCatalog extends StatefulWidget {
  final StudentDetails details;
  const CourseCatalog({super.key, required this.details});
  @override
  State<CourseCatalog> createState() => _CourseCatalogState();
}

class _CourseCatalogState extends State<CourseCatalog> {
  String? teacher;
  @override
  Widget build(BuildContext context) {
    final teachers = {
      for (final c in widget.details.courses)
        if (c.text('teacherId').isNotEmpty)
          c.text('teacherId'): c.text('teacherName'),
    };
    final courses = widget.details.courses
        .where((c) => teacher == null || c.text('teacherId') == teacher)
        .toList();
    return ScreenBody(
      children: [
        const PageHeading(
          'الكورسات المسجلة',
          subtitle: 'تابع كورسات ابنك المسجلة ومدرسي كل مادة',
        ),
        DropdownButtonFormField<String>(
          initialValue: teacher,
          isExpanded: true,
          decoration: const InputDecoration(
            prefixIcon: Icon(Icons.people_outline),
          ),
          items: [
            const DropdownMenuItem(value: null, child: Text('كل المدرسين')),
            ...teachers.entries.map(
              (e) => DropdownMenuItem(value: e.key, child: Text(e.value)),
            ),
          ],
          onChanged: (value) => setState(() => teacher = value),
        ),
        if (courses.isEmpty) const EmptyPanel('لا توجد كورسات مفعلة حاليًا.'),
        ...courses.indexed.map((entry) {
          final course = entry.$2;
          return SoftPanel(
            tint: MassarTokens.mint,
            child: ExpansionTile(
              key: ValueKey('${course.text('packageId')}:$teacher'),
              initiallyExpanded: entry.$1 == 0,
              tilePadding: EdgeInsets.zero,
              shape: const Border(),
              collapsedShape: const Border(),
              leading: const IconBadge(Icons.school_outlined, size: 52),
              title: Text(
                course.text('packageName'),
                style: Theme.of(context).textTheme.titleLarge,
              ),
              subtitle: Text(course.text('teacherName')),
              children: course.rows('terms').map((term) {
                final lessons = widget.details.lessons
                    .where(
                      (l) =>
                          l.text('packageId') == course.text('packageId') &&
                          l.text('termId') == term.text('termId'),
                    )
                    .toList();
                final exams = widget.details.exams
                    .where(
                      (l) =>
                          l.text('packageId') == course.text('packageId') &&
                          l.text('termId') == term.text('termId'),
                    )
                    .toList();
                return Column(
                  children: [
                    Text(term.text('termTitle')),
                    StatPair(
                      firstLabel: 'الحصص',
                      firstValue: '${term.count('lessonCount')}',
                      secondLabel: 'الاختبارات',
                      secondValue: '${term.count('examCount')}',
                    ),
                    ...lessons
                        .take(3)
                        .map(
                          (l) => Padding(
                            padding: const EdgeInsets.only(bottom: 8),
                            child: SoftPanel(
                              child: MassarDataRow(
                                l.text('lessonTitle'),
                                icon: Icons.play_circle_outline,
                                onTap: () => showLesson(context, l),
                              ),
                            ),
                          ),
                        ),
                    Row(
                      children: [
                        Expanded(
                          child: PrimaryAction(
                            label: 'عرض الحصص',
                            onPressed: () =>
                                openDetail(context, term.text('termTitle'), [
                                  if (lessons.isEmpty)
                                    const EmptyPanel('لا توجد حصص متاحة.'),
                                  ...lessons.map((l) => LessonTile(row: l)),
                                ]),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: TextButton(
                            onPressed: () => openDetail(context, 'النتائج', [
                              if (exams.isEmpty)
                                const EmptyPanel('لا توجد نتائج متاحة.'),
                              ...exams.map((e) => AssessmentTile(row: e)),
                            ]),
                            child: const Text('عرض النتائج'),
                          ),
                        ),
                      ],
                    ),
                  ],
                );
              }).toList(),
            ),
          );
        }),
      ],
    );
  }
}

void openBalance(BuildContext context, StudentDetails details) {
  final transactions = details.balance.rows('transactions')
    ..sort((a, b) => b.text('createdAt').compareTo(a.text('createdAt')));
  openDetail(context, 'الرصيد', [
    StudentBanner(
      name: details.text('studentName'),
      grade: details.text('grade'),
    ),
    SoftPanel(
      tint: MassarTokens.mint,
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'الرصيد المتاح',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 10),
                Text(
                  '${number(details.balance.number('currentBalance'))} ج.م',
                  style: Theme.of(
                    context,
                  ).textTheme.headlineLarge?.copyWith(fontSize: 38),
                ),
                const SizedBox(height: 10),
                const StatusPill('للمتابعة فقط'),
              ],
            ),
          ),
          const SizedBox(width: 10),
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              border: Border.all(
                color: Colors.white.withValues(alpha: .8),
                width: 3,
              ),
            ),
            child: const IconBadge(
              Icons.account_balance_wallet_outlined,
              size: 70,
            ),
          ),
        ],
      ),
    ),
    const PageHeading('آخر المعاملات'),
    if (transactions.isEmpty) const EmptyPanel('لا توجد معاملات مسجلة.'),
    ...transactions.map(
      (t) => SoftPanel(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            MassarDataRow(
              t.text('description'),
              icon: t.number('amount') >= 0
                  ? Icons.add_rounded
                  : Icons.description_outlined,
              subtitle: displayDate(t.text('createdAt')),
            ),
            Text(
              '${t.number('amount') >= 0 ? '+' : ''}${number(t.number('amount'))} ج.م',
              textDirection: TextDirection.ltr,
              style: Theme.of(context).textTheme.titleLarge?.copyWith(
                color: t.number('amount') >= 0 ? MassarTokens.teal : null,
              ),
            ),
            Text('الرصيد بعدها ${number(t.number('balanceAfter'))} ج.م'),
          ],
        ),
      ),
    ),
  ]);
}

void openNotifications(BuildContext context, ParentController controller) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => ListenableBuilder(
          listenable: controller,
          builder: (context, _) => Scaffold(
            appBar: AppBar(
              title: const BrandLogo(width: 114),
              centerTitle: true,
            ),
            body: RefreshIndicator(
              onRefresh: controller.refresh,
              child: ScreenBody(
                children: [
                  const PageHeading(
                    'التنبيهات',
                    subtitle: 'كل ما يخص متابعة مستوى ابنك الأكاديمي',
                  ),
                  if (controller.active != null)
                    StudentBanner(
                      name: controller.active!.name,
                      grade: controller.details?.text('grade') ?? '',
                    ),
                  if (controller.notificationError != null)
                    ErrorPanel(
                      controller.notificationError!,
                      retry: controller.refresh,
                    ),
                  if (controller.details?.warnings.isNotEmpty ?? false)
                    const PageHeading('تنبيهات المتابعة'),
                  ...?controller.details?.warnings.map(
                    (warning) => SoftPanel(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          StatusPill(switch (warning.text('severity')) {
                            'High' || 'Critical' => 'مهم',
                            'Medium' => 'متوسط',
                            _ => 'تنبيه',
                          }, color: MassarTokens.warning),
                          MassarDataRow(
                            warning.text('reason'),
                            icon: Icons.warning_amber_rounded,
                            subtitle: displayDate(warning.text('createdAt')),
                          ),
                        ],
                      ),
                    ),
                  ),
                  if (controller.notifications.isNotEmpty)
                    const PageHeading('آخر الإشعارات'),
                  ...controller.notifications.map(
                    (notification) => SoftPanel(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (!notification.flag('isRead'))
                            const StatusPill('جديد'),
                          MassarDataRow(
                            notification.text('title'),
                            icon: Icons.notifications_none_rounded,
                            subtitle: notification.text('body'),
                          ),
                          Text(displayDate(notification.text('createdAt'))),
                          if (!notification.flag('isRead'))
                            TextButton(
                              onPressed: () => runAction(
                                context,
                                () => controller.markRead(notification),
                              ),
                              child: const Text('تحديد كمقروء'),
                            ),
                        ],
                      ),
                    ),
                  ),
                  SoftPanel(
                    child: MassarDataRow(
                      'إعدادات إشعارات الجهاز',
                      icon: Icons.settings_outlined,
                      onTap: () =>
                          runAction(context, controller.bridge.openSettings),
                    ),
                  ),
                  if (controller.notifications.isEmpty &&
                      (controller.details?.warnings.isEmpty ?? true) &&
                      controller.notificationError == null)
                    const EmptyPanel(
                      'لا توجد تنبيهات حالية.',
                      icon: Icons.check_circle_outline,
                    ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
