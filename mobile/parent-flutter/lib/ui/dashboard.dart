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
        title: const BrandLogo(),
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
            child: MassarDataRow(
              controller.active!.name,
              subtitle: details?.text('grade'),
              icon: Icons.person_outline,
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
                      3 => MoreScreen(controller: controller),
                      _ => HomeOverview(
                        controller: controller,
                        onLessons: () => setState(() => tab = 1),
                        onResults: () => setState(() => tab = 2),
                      ),
                    },
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
            MassarDataRow(
              'الكورسات المسجلة',
              icon: Icons.menu_book_outlined,
              onTap: () => openCourses(context, controller.details!),
            ),
            MassarDataRow(
              'الرصيد',
              icon: Icons.account_balance_wallet_outlined,
              onTap: () => openBalance(context, controller.details!),
            ),
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
        appBar: AppBar(title: const Text('الطلاب المرتبطون')),
        body: ScreenBody(
          children: [
            const PageHeading(
              'اختر الطالب',
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

void openCourses(
  BuildContext context,
  StudentDetails details,
) => openDetail(context, 'الكورسات المسجلة', [
  if (details.courses.isEmpty) const EmptyPanel('لا توجد كورسات مفعلة حاليًا.'),
  ...details.courses.map(
    (course) => SoftPanel(
      child: ExpansionTile(
        title: Text(course.text('packageName')),
        subtitle: Text(course.text('teacherName')),
        children: course
            .rows('terms')
            .map(
              (term) => Column(
                children: [
                  MassarDataRow(
                    term.text('termTitle'),
                    subtitle:
                        '${term.count('lessonCount')} حصة • ${term.count('examCount')} اختبار',
                  ),
                  TextButton(
                    onPressed: () => openDetail(
                      context,
                      term.text('termTitle'),
                      details.lessons
                          .where(
                            (l) =>
                                l.text('packageId') ==
                                    course.text('packageId') &&
                                l.text('termId') == term.text('termId'),
                          )
                          .map((l) => LessonTile(row: l))
                          .toList(),
                    ),
                    child: const Text('عرض الحصص'),
                  ),
                  TextButton(
                    onPressed: () => openDetail(
                      context,
                      'النتائج',
                      details.exams
                          .where(
                            (e) =>
                                e.text('packageId') ==
                                    course.text('packageId') &&
                                e.text('termId') == term.text('termId'),
                          )
                          .map((e) => AssessmentTile(row: e))
                          .toList(),
                    ),
                    child: const Text('عرض النتائج'),
                  ),
                ],
              ),
            )
            .toList(),
      ),
    ),
  ),
]);

void openBalance(BuildContext context, StudentDetails details) {
  final transactions = details.balance.rows('transactions')
    ..sort((a, b) => b.text('createdAt').compareTo(a.text('createdAt')));
  openDetail(context, 'الرصيد', [
    SoftPanel(
      child: Column(
        children: [
          const Text('الرصيد المتاح'),
          Text(
            '${number(details.balance.number('currentBalance'))} ج.م',
            style: Theme.of(context).textTheme.headlineLarge,
          ),
          const StatusPill('للمتابعة فقط'),
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
              subtitle: displayDate(t.text('createdAt')),
            ),
            Text(
              '${t.number('amount') >= 0 ? '+' : ''}${number(t.number('amount'))} ج.م',
              textDirection: TextDirection.ltr,
              style: Theme.of(context).textTheme.titleLarge,
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
            appBar: AppBar(title: const Text('التنبيهات')),
            body: RefreshIndicator(
              onRefresh: controller.refresh,
              child: ScreenBody(
                children: [
                  if (controller.notificationError != null)
                    ErrorPanel(
                      controller.notificationError!,
                      retry: controller.refresh,
                    ),
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
                            subtitle: displayDate(warning.text('createdAt')),
                          ),
                        ],
                      ),
                    ),
                  ),
                  ...controller.notifications.map(
                    (notification) => SoftPanel(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (!notification.flag('isRead'))
                            const StatusPill('جديد'),
                          MassarDataRow(
                            notification.text('title'),
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
