import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  testWidgets(
    'compact attendance shows named score colors and adjacent homework without overflow',
    (tester) async {
      late Directory directory;
      late CenterStore store;
      late LessonSession current;
      late StudyMonth month;
      final key = GlobalKey();
      final records = <AcademicRecord>[];
      await tester.runAsync(() async {
        await initializeDateFormatting('ar_EG');
        directory = await Directory.systemTemp.createTemp('massar-compact-');
        store = await CenterStore.open(directory: directory.path);
        await store.setupAdmin('مدير تجريبي', 'preview-password');
        for (final kind in CatalogKind.values) {
          await store.saveCatalog(
            CatalogEntry(
              kind: kind,
              name: switch (kind) {
                CatalogKind.subject => 'التاريخ',
                CatalogKind.center => 'سنتر النور',
                CatalogKind.grade => 'ثانية بكالوريا',
              },
            ),
          );
        }
        await store.saveGroup(
          StudyGroup(
            name: 'الجمعة',
            subjectId: store.catalogs[0].id,
            centerId: store.catalogs[1].id,
            gradeId: store.catalogs[2].id,
            sessionPrice: 6000,
            packagePrice: 21000,
          ),
        );
        await store.saveStudent(
          Student(
            name: 'أحمد محمد علي',
            code: '00123',
            phone: '01012345678',
            guardianPhone: '01198765432',
            centerFeeEnabled: true,
            notes: 'مراجعة الواجب السابق قبل الحصة',
            groupIds: [store.groups.single.id],
            createdAt: DateTime.now().subtract(const Duration(days: 30)),
          ),
        );
        month = await store.saveStudyMonth(
          StudyMonth(
            name: 'الشهر الثاني',
            lessons: [for (var n = 1; n <= 4; n++) PreparedLesson(number: n)],
          ),
        );
        for (var n = 0; n < 4; n++) {
          current = await store.startPreparedLesson(
            groupId: store.groups.single.id,
            preparedLessonId: month.lessons[n].id,
          );
          if (n == 3) break;
          await store.collectAndAttend(
            EntryRequest(
              studentId: store.students.single.id,
              sessionId: current.id,
              mode: EntryMode.single,
            ),
          );
          final exam = await store.saveAcademicActivity(
            AcademicActivity(
              preparedLessonId: month.lessons[n].id,
              name: 'اختبار الفصل ${n + 1}',
              kind: AcademicActivityKind.exam,
              maxScore: 10,
              createdAt: DateTime.now(),
            ),
          );
          final homework = await store.saveAcademicActivity(
            AcademicActivity(
              preparedLessonId: month.lessons[n].id,
              name: 'واجب الفصل ${n + 1}',
              kind: AcademicActivityKind.homework,
              createdAt: DateTime.now(),
            ),
          );
          await store.saveAcademic(
            AcademicRecord(
              studentId: store.students.single.id,
              sessionId: current.id,
              activityId: exam.id,
              score: [4, 5, 9][n],
              maxScore: 10,
              updatedAt: DateTime.now(),
            ),
          );
          records.add(store.academics.last);
          await store.saveAcademic(
            AcademicRecord(
              studentId: store.students.single.id,
              sessionId: current.id,
              activityId: homework.id,
              homework: n == 0
                  ? HomeworkStatus.missing
                  : HomeworkStatus.complete,
              updatedAt: DateTime.now(),
            ),
          );
          await store.closeSession(current.id);
        }
        await (FontLoader('Tajawal')
              ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
              ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
            .load();
        await (FontLoader(
          'MaterialIcons',
        )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
      });
      addTearDown(() async {
        await tester.binding.setSurfaceSize(null);
        await store.close();
        await directory.delete(recursive: true);
      });
      for (final dark in [false, true]) {
        await tester.binding.setSurfaceSize(const Size(1440, 960));
        await tester.pumpWidget(
          RepaintBoundary(
            key: key,
            child: MaterialApp(
              debugShowCheckedModeBanner: false,
              theme: dark ? MassarTheme.dark : MassarTheme.light,
              home: Directionality(
                textDirection: TextDirection.rtl,
                child: AttendanceWorkspace(
                  store: store,
                  onExit: () {},
                  initialSessionId: current.id,
                  workspaceContext: AttendanceWorkspaceContext()
                    ..groupId = current.groupId
                    ..sessionId = current.id
                    ..studyMonthId = month.id
                    ..preparedLessonId = current.preparedLessonId
                    ..studentId = store.students.single.id
                    ..studentResolved = true,
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        final palette = MassarPalette.of(
          tester.element(find.byType(AttendanceWorkspace)),
        );
        for (var n = 0; n < records.length; n++) {
          final score = tester.widget<Text>(
            find.byKey(ValueKey('history-exam-score-${records[n].id}')),
          );
          expect(score.style!.color, n == 0 ? palette.error : palette.success);
          expect(find.text('اختبار الفصل ${n + 1}'), findsOneWidget);
        }
        expect(
          tester.getTopLeft(find.text('الامتحانات السابقة')).dy,
          tester.getTopLeft(find.text('الواجبات السابقة')).dy,
        );
        if (const bool.fromEnvironment('CAPTURE_UI')) {
          await tester.runAsync(() async {
            final boundary =
                key.currentContext!.findRenderObject()!
                    as RenderRepaintBoundary;
            final image = await boundary.toImage(pixelRatio: 1.5);
            final bytes = await image.toByteData(
              format: ui.ImageByteFormat.png,
            );
            final target = File(
              'build/verification/attendance-compact-${dark ? 'dark' : 'light'}.png',
            );
            await target.parent.create(recursive: true);
            await target.writeAsBytes(bytes!.buffer.asUint8List());
            image.dispose();
          });
        }
        await tester.binding.setSurfaceSize(const Size(1280, 800));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        expect(
          tester.getRect(find.byKey(const Key('collect-package'))).bottom,
          lessThanOrEqualTo(800),
        );
        await tester.pumpWidget(const SizedBox.shrink());
      }
    },
  );
}
