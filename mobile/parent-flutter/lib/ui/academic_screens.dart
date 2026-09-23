import 'package:flutter/material.dart';
import '../data/models.dart';
import 'design_system.dart';
import 'formatters.dart';

void openDetail(BuildContext context, String title, List<Widget> children) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => MassarPage(
          title: title,
          body: ScreenBody(children: children),
        ),
      ),
    );

class AcademicList extends StatefulWidget {
  final StudentDetails details;
  final bool lessons;
  final bool initialHomework;
  const AcademicList({
    super.key,
    required this.details,
    required this.lessons,
    this.initialHomework = false,
  });
  @override
  State<AcademicList> createState() => _AcademicListState();
}

class _AcademicListState extends State<AcademicList> {
  String? teacher, course, term;
  String watchFilter = 'الكل';
  late bool homework = widget.initialHomework;
  List<AcademicRow> get source => widget.lessons
      ? widget.details.lessons
      : homework
      ? widget.details.homeworks
      : widget.details.exams;
  Map<String, String> get enrolledTeachers => {
    for (final c in widget.details.courses)
      if (c.text('teacherId').isNotEmpty)
        c.text('teacherId'): c.text('teacherName'),
  };
  bool get hasTeacher =>
      teacher != null && enrolledTeachers.containsKey(teacher);
  List<AcademicRow> get ownedCourses => widget.details.courses
      .where((c) => c.text('teacherId') == teacher)
      .toList();
  String rowTeacher(AcademicRow row) =>
      widget.details.courses
          .where((c) => c.text('packageId') == row.text('packageId'))
          .firstOrNull
          ?.text('teacherId') ??
      '';
  List<AcademicRow> get filtered => source
      .where(
        (row) =>
            hasTeacher &&
            rowTeacher(row) == teacher &&
            (course == null || row.text('packageId') == course) &&
            (term == null || row.text('termId') == term) &&
            (!widget.lessons ||
                watchFilter == 'الكل' ||
                row.watchStatus == watchFilter),
      )
      .toList();

  Widget picker(
    String label,
    String idKey,
    String nameKey,
    String? selected,
    ValueChanged<String?> changed,
    List<AcademicRow> rows,
  ) {
    final options = <String, String>{
      for (final row in rows)
        if (row.text(idKey).isNotEmpty) row.text(idKey): row.text(nameKey),
    };
    return DropdownButtonFormField<String>(
      initialValue: options.containsKey(selected) ? selected : null,
      key: ValueKey('$label:$selected:${options.keys.join()}'),
      isExpanded: true,
      decoration: InputDecoration(
        hintText: label,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 16,
          vertical: 12,
        ),
      ),
      items: [
        DropdownMenuItem(value: null, child: Text(label)),
        ...options.entries.map(
          (entry) => DropdownMenuItem(
            value: entry.key,
            child: Text(entry.value, overflow: TextOverflow.ellipsis),
          ),
        ),
      ],
      onChanged: changed,
    );
  }

  @override
  Widget build(BuildContext context) {
    final teacherRows = source
        .where((r) => hasTeacher && rowTeacher(r) == teacher)
        .toList();
    final courseRows = teacherRows
        .where((r) => course == null || r.text('packageId') == course)
        .toList();
    return ScreenBody(
      children: [
        PageHeading(
          widget.lessons ? 'متابعة الحصص' : 'النتائج',
          subtitle: widget.lessons
              ? 'تابع تقدم ابنك في الحصص المسجلة'
              : 'تابع أداء ابنك في الاختبارات والواجبات',
        ),
        if (!widget.lessons)
          PillTabs<bool>(
            options: const {false: 'الاختبارات', true: 'الواجبات'},
            selected: homework,
            onChanged: (value) => setState(() {
              homework = value;
              course = term = null;
            }),
          ),
        picker(
          'اختر المدرس',
          'teacherId',
          'teacherName',
          teacher,
          (id) => setState(() {
            teacher = id;
            course = term = null;
          }),
          widget.details.courses,
        ),
        if (!hasTeacher)
          EmptyPanel(
            enrolledTeachers.isEmpty
                ? 'لا توجد كورسات مشتراة متاحة لهذا الطالب.'
                : 'اختر المدرس لعرض الحصص والنتائج الخاصة بكورساته.',
            icon: Icons.school_outlined,
          ),
        if (hasTeacher) ...[
          Row(
            children: [
              Expanded(
                child: picker(
                  'كل الكورسات',
                  'packageId',
                  'packageName',
                  course,
                  (id) => setState(() {
                    course = id;
                    term = null;
                  }),
                  ownedCourses,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: picker(
                  'كل الترمات',
                  'termId',
                  'termTitle',
                  term,
                  (id) => setState(() => term = id),
                  courseRows,
                ),
              ),
            ],
          ),
          if (widget.lessons)
            PillTabs<String>(
              options: const {
                'الكل': 'الكل',
                'مكتملة': 'مكتملة',
                'قيد المشاهدة': 'قيد المشاهدة',
                'لم يبدأ': 'لم يبدأ',
              },
              selected: watchFilter,
              onChanged: (value) => setState(() => watchFilter = value),
            ),
          if (!widget.lessons && homework && filtered.isNotEmpty)
            SoftPanel(
              child: Row(
                children: [
                  ProgressRing(
                    progress:
                        filtered.where((r) => r.flag('isSubmitted')).length /
                        filtered.length,
                    label:
                        '${filtered.where((r) => r.flag('isSubmitted')).length} من ${filtered.length}',
                    size: 88,
                  ),
                  const SizedBox(width: 20),
                  Expanded(
                    child: Text(
                      'تم تسليم ${filtered.where((r) => r.flag('isSubmitted')).length} من ${filtered.length} واجبات',
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                  ),
                ],
              ),
            ),
          if (filtered.isEmpty)
            const EmptyPanel(
              'لا توجد بيانات لهذا الاختيار. جرّب تغيير الفلاتر.',
            ),
          ...filtered.map(
            (row) => widget.lessons
                ? LessonTile(row: row)
                : AssessmentTile(row: row, homework: homework),
          ),
        ],
      ],
    );
  }
}

Color academicColor(
  AcademicRow row, {
  bool homework = false,
  bool lesson = false,
}) {
  if (lesson) {
    return row.flag('isCompleted')
        ? MassarTokens.teal
        : row.watchStatus == 'لم يبدأ'
        ? MassarTokens.ink
        : MassarTokens.warning;
  }
  if (homework) {
    return row.hasHomeworkGrade
        ? MassarTokens.teal
        : row.flag('isSubmitted')
        ? MassarTokens.warning
        : MassarTokens.danger;
  }
  return row.text('status') == 'Failed'
      ? MassarTokens.danger
      : row.hasExamGrade
      ? MassarTokens.teal
      : row.text('status') == 'NotStarted'
      ? MassarTokens.ink
      : MassarTokens.warning;
}

class LessonTile extends StatelessWidget {
  final AcademicRow row;
  const LessonTile({super.key, required this.row});
  @override
  Widget build(BuildContext context) => SoftPanel(
    child: InkWell(
      onTap: () => showLesson(context, row),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  row.text('lessonTitle'),
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
              const SizedBox(width: 8),
              Flexible(
                child: StatusPill(
                  row.watchStatus,
                  color: academicColor(row, lesson: true),
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            '${row.startedVideos == null ? '' : 'بدأ ${row.startedVideos} من ${row.count('totalVideos')} • '}أكمل ${row.completedVideos} من ${row.count('totalVideos')}',
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Text(
                '${(row.videoProgress * 100).round()}%',
                style: const TextStyle(
                  color: MassarTokens.teal,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Directionality(
                  textDirection: TextDirection.ltr,
                  child: LinearProgressIndicator(
                    value: row.videoProgress,
                    minHeight: 9,
                    borderRadius: BorderRadius.circular(20),
                    color: MassarTokens.teal,
                    backgroundColor: MassarTokens.teal.withValues(alpha: .1),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    ),
  );
}

void showLesson(BuildContext context, AcademicRow row) =>
    openDetail(context, 'تفاصيل الحصة', lessonDetailChildren(row));
List<Widget> lessonDetailChildren(AcademicRow row) => [
  ContextBanner(
    title: row.text('lessonTitle'),
    subtitle: '${row.scope}\n${row.text('teacherName')}',
    icon: Icons.science_outlined,
  ),
  SoftPanel(
    child: Column(
      children: [
        ProgressArc(
          progress: row.videoProgress,
          caption: 'اكتمال الفيديوهات',
          detail: '',
        ),
        const Divider(),
        StatPair(
          firstLabel: 'بدأ المشاهدة',
          firstValue: row.startedVideos == null
              ? 'غير متاح'
              : '${row.startedVideos} من ${row.count('totalVideos')}',
          secondLabel: 'أكمل المشاهدة',
          secondValue: '${row.completedVideos} من ${row.count('totalVideos')}',
        ),
      ],
    ),
  ),
  SoftPanel(
    child: Column(
      children: [
        MetricRow(
          'وقت المشاهدة',
          subtitle: duration(row.count('watchedSeconds')),
          icon: Icons.access_time,
        ),
        const Divider(),
        MetricRow(
          'آخر مشاهدة',
          subtitle: displayDate(row.text('lastWatchedAt')),
          icon: Icons.calendar_today_outlined,
        ),
        if (row.count('watchCount') > 0)
          MassarDataRow(
            'عدد المشاهدات',
            trailing: number(row.count('watchCount')),
          ),
      ],
    ),
  ),
  const SoftPanel(
    tint: MassarTokens.mint,
    child: Row(
      children: [
        Icon(Icons.info_outline, color: MassarTokens.teal),
        SizedBox(width: 12),
        Expanded(child: Text('بدء الفيديو لا يعني اكتمال مشاهدته')),
      ],
    ),
  ),
];

class AssessmentTile extends StatelessWidget {
  final AcademicRow row;
  final bool homework;
  const AssessmentTile({super.key, required this.row, this.homework = false});
  @override
  Widget build(BuildContext context) => SoftPanel(
    child: InkWell(
      onTap: () => showAssessment(context, row, homework),
      child: LayoutBuilder(
        builder: (context, constraints) {
          final color = academicColor(row, homework: homework);
          final title = Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                row.text(homework ? 'title' : 'examTitle'),
                style: Theme.of(context).textTheme.titleLarge,
              ),
              if (row.scope.isNotEmpty) Text(row.scope),
              if (row.text('submittedAt').isNotEmpty &&
                  (homework ? row.flag('isSubmitted') : row.hasExamGrade))
                Text(displayDate(row.text('submittedAt'), compact: true)),
              if (homework)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: StatusPill(row.homeworkStatus, color: color),
                ),
            ],
          );
          final result = Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (!homework)
                ProgressRing(
                  progress: row.hasExamGrade
                      ? row.number('percentage') / 100
                      : null,
                  label: row.hasExamGrade
                      ? '${number(row.number('percentage'))}%'
                      : '—',
                ),
              const SizedBox(height: 8),
              Text(
                homework
                    ? assessmentGrade(row, true)
                    : row.hasExamGrade
                    ? '${number(row.number('score'))} من ${number(row.number('totalScore'))}'
                    : '—',
                style: const TextStyle(fontWeight: FontWeight.w900),
              ),
              if (!homework) StatusPill(row.examStatus, color: color),
            ],
          );
          if (MediaQuery.textScalerOf(context).scale(14) > 20) {
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [title, const SizedBox(height: 12), result],
            );
          }
          return Row(
            children: [
              if (homework) ...[
                IconBadge(Icons.description_outlined, color: color),
                const SizedBox(width: 10),
              ],
              Expanded(child: title),
              const SizedBox(width: 12),
              Flexible(child: result),
              const SizedBox(width: 6),
              const Icon(Icons.chevron_left, size: 20),
            ],
          );
        },
      ),
    ),
  );
}

String assessmentGrade(AcademicRow row, bool homework) => homework
    ? row.hasHomeworkGrade && row.text('grade').isNotEmpty
          ? row.text('grade')
          : '—'
    : row.hasExamGrade
    ? '${number(row.number('score'))} من ${number(row.number('totalScore'))} • ${number(row.number('percentage'))}%'
    : '—';

void showAssessment(BuildContext context, AcademicRow row, bool homework) =>
    openDetail(
      context,
      homework ? 'تفاصيل الواجب' : 'تفاصيل الاختبار',
      assessmentDetailChildren(row, homework),
    );
List<Widget> assessmentDetailChildren(AcademicRow row, bool homework) => [
  ContextBanner(
    title: row.text(homework ? 'title' : 'examTitle'),
    subtitle: row.scope,
    icon: Icons.description_outlined,
  ),
  SoftPanel(
    child: Column(
      children: [
        StatusPill(
          homework ? row.homeworkStatus : row.examStatus,
          color: academicColor(row, homework: homework),
        ),
        const SizedBox(height: 18),
        if (!homework && row.hasExamGrade)
          ProgressArc(
            progress: row.number('percentage') / 100,
            caption: '${number(row.number('percentage'))}%',
            detail: '',
            valueLabel:
                '${number(row.number('score'))} / ${number(row.number('totalScore'))}',
          )
        else
          Padding(
            padding: const EdgeInsets.all(20),
            child: Text(
              assessmentGrade(row, homework),
              style: const TextStyle(fontSize: 32, fontWeight: FontWeight.w900),
            ),
          ),
        if (row.text('submittedAt').isNotEmpty)
          MassarDataRow(
            homework ? 'تاريخ التسليم' : 'تاريخ الاختبار',
            subtitle: displayDate(row.text('submittedAt')),
            icon: Icons.calendar_today_outlined,
          ),
      ],
    ),
  ),
  if (homework ? row.hasHomeworkGrade : row.hasExamGrade) ...[
    const PageHeading(
      'مراجعة الأخطاء',
      subtitle: 'تعرف على إجابات الطالب والتصحيح',
    ),
    if (row.rows('mistakes').isEmpty)
      const EmptyPanel('لا توجد أخطاء متاحة للمراجعة.'),
    ...row.rows('mistakes').indexed.map((entry) {
      final question = entry.$2;
      return SoftPanel(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('السؤال ${entry.$1 + 1}'),
            const SizedBox(height: 10),
            Text(
              question.text('questionText'),
              style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
            ),
            AnswerPanel(
              label: 'إجابة الطالب',
              answer: question.text('studentAnswer').isEmpty
                  ? 'لا توجد إجابة'
                  : question.text('studentAnswer'),
              icon: Icons.close_rounded,
              color: MassarTokens.danger,
            ),
            if (question.text('correctAnswer').isNotEmpty)
              AnswerPanel(
                label: 'الإجابة الصحيحة',
                answer: question.text('correctAnswer'),
                icon: Icons.check_rounded,
                color: MassarTokens.teal,
              ),
            if (question.text('writtenCorrection').isNotEmpty)
              AnswerPanel(
                label: 'التصحيح',
                answer: question.text('writtenCorrection'),
                icon: Icons.lightbulb_outline,
                color: const Color(0xFF2766AC),
              ),
            const SizedBox(height: 12),
            if (homework && question.json['scoreReceived'] == null)
              const StatusPill('بانتظار التصحيح')
            else
              Text(
                '${number(question.number(homework ? 'scoreReceived' : 'pointsAwarded'))} من ${number(question.number('points'))}',
              ),
          ],
        ),
      );
    }),
  ],
];
