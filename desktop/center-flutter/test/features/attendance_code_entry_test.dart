import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';

void main() {
  testWidgets(
    'Arabic short code and Enter persist attendance once without inventing payment',
    (tester) async {
      late Directory directory;
      late CenterStore store;
      late LessonSession session;
      await tester.runAsync(() async {
        await initializeDateFormatting('ar_EG');
        directory = await Directory.systemTemp.createTemp('massar-code-entry-');
        store = await CenterStore.open(directory: directory.path);
        await store.setupAdmin('manager', 'code-entry-test-password');
        for (final kind in CatalogKind.values) {
          await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
        }
        await store.saveGroup(
          StudyGroup(
            name: 'مجموعة الاختبار',
            subjectId: store.catalogs[0].id,
            centerId: store.catalogs[1].id,
            gradeId: store.catalogs[2].id,
            sessionPrice: 6000,
          ),
        );
        await store.saveStudent(
          Student(
            name: 'طالب الاختبار',
            code: '01234',
            groupIds: [store.groups.single.id],
            createdAt: DateTime(2026),
          ),
        );
        final month = await store.saveStudyMonth(
          StudyMonth(
            name: 'شهر الاختبار',
            lessons: [const PreparedLesson(number: 1)],
          ),
        );
        session = await store.startPreparedLesson(
          groupId: store.groups.single.id,
          preparedLessonId: month.lessons.single.id,
        );
      });
      addTearDown(() async {
        await tester.binding.setSurfaceSize(null);
        await tester.runAsync(() async {
          await store.close();
          await directory.delete(recursive: true);
        });
      });
      await tester.binding.setSurfaceSize(const Size(1440, 960));
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: AttendanceWorkspace(
              store: store,
              initialSessionId: session.id,
              onExit: () {},
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      for (final code in ['١٢٣٤', '01234']) {
        await tester.enterText(find.byKey(const Key('student-search')), code);
        // Submit before the suggestion debounce expires, just like a scanner.
        await tester.runAsync(() async {
          if (store.attendances.isEmpty) {
            final committed = Completer<void>();
            void changed() {
              if (store.attendances.isNotEmpty && !committed.isCompleted) {
                committed.complete();
              }
            }

            store.addListener(changed);
            try {
              await tester.testTextInput.receiveAction(TextInputAction.done);
              await committed.future.timeout(const Duration(seconds: 5));
            } finally {
              store.removeListener(changed);
            }
          } else {
            await tester.testTextInput.receiveAction(TextInputAction.done);
            await acknowledgeNotice(tester);
          }
        });
        await tester.pumpAndSettle();
        expect(store.attendances, hasLength(1));
        expect(store.attendances.single.studentId, store.students.single.id);
        expect(store.attendances.single.status, AttendanceStatus.present);
        expect(store.attendances.single.paymentPending, isTrue);
        expect(store.payments, isEmpty);
        expect(tester.takeException(), isNull);
      }
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );
}
