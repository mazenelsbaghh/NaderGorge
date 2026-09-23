import 'package:flutter/material.dart';
import '../data/models.dart';
import 'design_system.dart';
import 'formatters.dart';

void openDetail(BuildContext context, String title, List<Widget> children) =>
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => Scaffold(
          appBar: AppBar(title: Text(title)),
          body: ScreenBody(children: children),
        ),
      ),
    );

class AcademicList extends StatefulWidget {
  final StudentDetails details;
  final bool lessons;
  const AcademicList({super.key, required this.details, required this.lessons});
  @override
  State<AcademicList> createState() => _AcademicListState();
}

class _AcademicListState extends State<AcademicList> {
  String? teacher, course, term;
  String watchFilter = 'الكل';
  bool homework = false;
  List<AcademicRow> get source => widget.lessons
      ? widget.details.lessons
      : homework
      ? widget.details.homeworks
      : widget.details.exams;
  List<AcademicRow> get filtered => source
      .where(
        (row) =>
            (teacher == null || row.text('teacherId') == teacher) &&
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
      decoration: InputDecoration(labelText: label),
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
        .where((r) => teacher == null || r.text('teacherId') == teacher)
        .toList();
    final courseRows = teacherRows
        .where((r) => course == null || r.text('packageId') == course)
        .toList();
    return ScreenBody(
      children: [
        PageHeading(widget.lessons ? 'متابعة الحصص' : 'النتائج'),
        if (!widget.lessons)
          SegmentedButton<bool>(
            segments: const [
              ButtonSegment(value: false, label: Text('الاختبارات')),
              ButtonSegment(value: true, label: Text('الواجبات')),
            ],
            selected: {homework},
            onSelectionChanged: (selection) => setState(() {
              homework = selection.first;
              teacher = course = term = null;
            }),
          ),
        picker(
          'كل المدرسين',
          'teacherId',
          'teacherName',
          teacher,
          (id) => setState(() {
            teacher = id;
            course = term = null;
          }),
          source,
        ),
        picker(
          'كل الكورسات',
          'packageId',
          'packageName',
          course,
          (id) => setState(() {
            course = id;
            term = null;
          }),
          teacherRows,
        ),
        picker(
          'كل الترمات',
          'termId',
          'termTitle',
          term,
          (id) => setState(() => term = id),
          courseRows,
        ),
        if (widget.lessons)
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: ['الكل', 'مكتملة', 'قيد المشاهدة', 'لم يبدأ']
                .map(
                  (label) => ChoiceChip(
                    label: Text(label),
                    selected: watchFilter == label,
                    onSelected: (_) => setState(() => watchFilter = label),
                  ),
                )
                .toList(),
          ),
        if (filtered.isEmpty)
          const EmptyPanel('لا توجد بيانات لهذا الاختيار. جرّب تغيير الفلاتر.'),
        ...filtered.map(
          (row) => widget.lessons
              ? LessonTile(row: row)
              : AssessmentTile(row: row, homework: homework),
        ),
      ],
    );
  }
}

class LessonTile extends StatelessWidget {
  final AcademicRow row;
  const LessonTile({super.key, required this.row});
  @override
  Widget build(BuildContext context) => SoftPanel(
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        MassarDataRow(
          row.text('lessonTitle'),
          subtitle: row.scope,
          onTap: () => showLesson(context, row),
        ),
        StatusPill(
          row.watchStatus,
          color: row.flag('isCompleted')
              ? MassarTokens.teal
              : MassarTokens.warning,
        ),
        const SizedBox(height: 12),
        if (row.startedVideos != null)
          Text('بدأ ${row.startedVideos} من ${row.count('totalVideos')} فيديو'),
        Text('أكمل ${row.completedVideos} من ${row.count('totalVideos')}'),
        const SizedBox(height: 12),
        LinearProgressIndicator(
          value: row.videoProgress,
          minHeight: 8,
          borderRadius: BorderRadius.circular(20),
          color: MassarTokens.teal,
        ),
      ],
    ),
  );
}

void showLesson(BuildContext context, AcademicRow row) =>
    openDetail(context, 'تفاصيل الحصة', [
      PageHeading(
        row.text('lessonTitle'),
        subtitle: '${row.scope}\n${row.text('teacherName')}',
      ),
      SoftPanel(
        child: Column(
          children: [
            ProgressArc(
              progress: row.videoProgress,
              caption: 'اكتمال الفيديوهات',
              detail: row.watchStatus,
            ),
            const Divider(),
            if (row.startedVideos != null)
              MassarDataRow(
                'بدأ المشاهدة',
                trailing: '${row.startedVideos} من ${row.count('totalVideos')}',
              ),
            MassarDataRow(
              'أكمل المشاهدة',
              trailing: '${row.completedVideos} من ${row.count('totalVideos')}',
            ),
          ],
        ),
      ),
      SoftPanel(
        child: Column(
          children: [
            MassarDataRow(
              'وقت المشاهدة',
              subtitle: duration(row.count('watchedSeconds')),
              icon: Icons.access_time,
            ),
            MassarDataRow(
              'عدد المشاهدات',
              trailing: number(row.count('watchCount')),
            ),
            MassarDataRow(
              'آخر مشاهدة',
              subtitle: displayDate(row.text('lastWatchedAt')),
              icon: Icons.calendar_today_outlined,
            ),
          ],
        ),
      ),
      const Text(
        'بدء الفيديو لا يعني اكتمال مشاهدته',
        textAlign: TextAlign.center,
      ),
    ]);

class AssessmentTile extends StatelessWidget {
  final AcademicRow row;
  final bool homework;
  const AssessmentTile({super.key, required this.row, this.homework = false});
  @override
  Widget build(BuildContext context) => SoftPanel(
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        MassarDataRow(
          row.text(homework ? 'title' : 'examTitle'),
          subtitle: row.scope,
          onTap: () => showAssessment(context, row, homework),
        ),
        Wrap(
          spacing: 12,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            StatusPill(
              homework ? row.homeworkStatus : row.examStatus,
              color: homework
                  ? row.hasHomeworkGrade
                        ? MassarTokens.teal
                        : MassarTokens.warning
                  : row.text('status') == 'Failed'
                  ? MassarTokens.danger
                  : row.hasExamGrade
                  ? MassarTokens.teal
                  : MassarTokens.warning,
            ),
            Text(
              assessmentGrade(row, homework),
              style: Theme.of(context).textTheme.titleLarge,
            ),
          ],
        ),
        if (row.text('submittedAt').isNotEmpty)
          Text(displayDate(row.text('submittedAt'))),
      ],
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

void showAssessment(
  BuildContext context,
  AcademicRow row,
  bool homework,
) => openDetail(context, homework ? 'تفاصيل الواجب' : 'تفاصيل الاختبار', [
  PageHeading(row.text(homework ? 'title' : 'examTitle'), subtitle: row.scope),
  SoftPanel(
    child: Column(
      children: [
        StatusPill(homework ? row.homeworkStatus : row.examStatus),
        const SizedBox(height: 20),
        Text(
          assessmentGrade(row, homework),
          style: Theme.of(context).textTheme.headlineMedium,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 16),
        Text(displayDate(row.text('submittedAt'))),
      ],
    ),
  ),
  if (homework ? row.hasHomeworkGrade : row.hasExamGrade) ...[
    const PageHeading('مراجعة الأخطاء'),
    if (row.rows('mistakes').isEmpty)
      const EmptyPanel('لا توجد أخطاء متاحة للمراجعة.'),
    ...row
        .rows('mistakes')
        .map(
          (question) => SoftPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  question.text('questionText'),
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const Divider(),
                MassarDataRow(
                  'إجابة الطالب',
                  subtitle: question.text('studentAnswer').isEmpty
                      ? 'لا توجد إجابة'
                      : question.text('studentAnswer'),
                ),
                if (question.text('correctAnswer').isNotEmpty)
                  MassarDataRow(
                    'الإجابة الصحيحة',
                    subtitle: question.text('correctAnswer'),
                    icon: Icons.check_circle_outline,
                  ),
                if (question.text('writtenCorrection').isNotEmpty)
                  MassarDataRow(
                    'التصحيح',
                    subtitle: question.text('writtenCorrection'),
                    icon: Icons.lightbulb_outline,
                  ),
                if (homework && question.json['scoreReceived'] == null)
                  const StatusPill('بانتظار التصحيح')
                else
                  Text(
                    '${number(question.number(homework ? 'scoreReceived' : 'pointsAwarded'))} من ${number(question.number('points'))}',
                  ),
              ],
            ),
          ),
        ),
  ],
]);
