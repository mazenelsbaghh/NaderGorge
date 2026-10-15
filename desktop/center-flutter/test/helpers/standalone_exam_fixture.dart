import 'dart:io';

import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/domain/standalone_exam_results.dart';

class StandaloneExamFixture {
  StandaloneExamFixture._(
    this.directory,
    this.store,
    this.group,
    this.session,
    this.activity,
    this.students,
  );
  final Directory directory;
  final CenterStore store;
  final StudyGroup group;
  final LessonSession session;
  final AcademicActivity activity;
  final List<Student> students;
  static const roomId = 'fixture-secondary:fixture-room';

  static Future<StandaloneExamFixture> open() async {
    final directory = await Directory.systemTemp.createTemp(
      'standalone-exam-ui-',
    );
    await Directory('${directory.path}/host').create();
    final store = await CenterStore.open(directory: '${directory.path}/host');
    await store.setupAdmin('موظف التقرير', 'synthetic-report-password');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة التقرير',
        centerId: store.catalogs
            .firstWhere((row) => row.kind == CatalogKind.center)
            .id,
        subjectId: store.catalogs
            .firstWhere((row) => row.kind == CatalogKind.subject)
            .id,
        gradeId: store.catalogs
            .firstWhere((row) => row.kind == CatalogKind.grade)
            .id,
      ),
    );
    final group = store.groups.single;
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'شهر التقرير',
        lessons: [const PreparedLesson(number: 1)],
      ),
    );
    final session = await store.startPreparedLesson(
      groupId: group.id,
      preparedLessonId: month.lessons.single.id,
    );
    final activity = await store.saveAcademicActivity(
      AcademicActivity(
        kind: AcademicActivityKind.exam,
        name: 'امتحان من عشرين',
        maxScore: 20,
        preparedLessonId: session.preparedLessonId,
        createdAt: DateTime.now(),
      ),
    );
    for (var index = 0; index < 5; index++) {
      await store.saveStudent(
        Student(
          name: index == 3 ? '=2+2 & طالب <متابعة>' : 'طالب $index',
          code: '0000$index',
          phone: '0100000000$index',
          guardianPhone: '0120000000$index',
          groupIds: [group.id],
          createdAt: DateTime.now(),
        ),
      );
    }
    final students = [
      for (var index = 0; index < 5; index++)
        store.students.singleWhere((row) => row.code == '0000$index'),
    ];
    for (final student in students.take(4)) {
      await store.recordAttendance(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
    }
    await store.saveAcademic(
      AcademicRecord(
        studentId: students[1].id,
        sessionId: session.id,
        activityId: activity.id,
        score: 3,
        maxScore: 20,
        updatedAt: DateTime.now(),
      ),
    );
    final attempts = [
      for (final index in [0, 1, 2, 4])
        StandaloneExamAttempt(
          attemptId: 'attempt-$index',
          code: students[index].code,
          name: students[index].name,
          phone: students[index].phone,
          status: index == 2 ? 'pending_grading' : 'final',
          score: index == 2
              ? null
              : index == 0
              ? 0
              : 18.5,
          maxScore: 20,
          pending: index == 2 ? 1 : 0,
          resultVersion: 1,
        ),
    ];
    await store.publishStandaloneExamPage(
      StandaloneExamPage(
        sourceId: 'fixture-secondary',
        examId: 'fixture-room',
        title: 'امتحان القاعة المستقل',
        roomState: 'closed',
        maxScore: 20,
        totalAttempts: attempts.length,
        exportRevision: 'a' * 64,
        attempts: attempts,
      ),
    );
    return StandaloneExamFixture._(
      directory,
      store,
      group,
      session,
      activity,
      students,
    );
  }

  StandaloneExamImportPlan preview() => store.previewStandaloneExamImport(
    roomId: roomId,
    groupId: group.id,
    sessionId: session.id,
    activityId: activity.id,
  );

  Future<void> close() async {
    await store.close();
    await directory.delete(recursive: true);
  }
}
