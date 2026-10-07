import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/students_page.dart';
import 'package:massar_center/features/management/student_profile_page.dart';

void main() {
  testWidgets(
    'open student, reflect changes, inspect history and retain search on return',
    (tester) async {
      await initializeDateFormatting('ar_EG');
      final directory = (await tester.runAsync(
        () => Directory.systemTemp.createTemp('massar-profile-'),
      ))!;
      final store = (await tester.runAsync(
        () => CenterStore.open(directory: directory.path),
      ))!;
      addTearDown(() async {
        await store.close();
        await directory.delete(recursive: true);
      });
      await tester.runAsync(() async {
        await store.setupAdmin('مدير', 'test-profile-password');
        for (final kind in CatalogKind.values) {
          await store.saveCatalog(CatalogEntry(kind: kind, name: kind.name));
        }
        await store.saveGroup(
          StudyGroup(
            name: 'المجموعة',
            subjectId: store.catalogs
                .firstWhere((c) => c.kind == CatalogKind.subject)
                .id,
            centerId: store.catalogs
                .firstWhere((c) => c.kind == CatalogKind.center)
                .id,
            gradeId: store.catalogs
                .firstWhere((c) => c.kind == CatalogKind.grade)
                .id,
            sessionPrice: 6000,
            packagePrice: 21000,
          ),
        );
        await store.saveStudent(
          Student(
            code: '12345',
            name: 'طالب البروفايل',
            notes: 'ملاحظة أولى',
            groupIds: [store.groups.single.id],
            createdAt: DateTime(2026),
          ),
        );
      });
      final student = store.students.single;
      tester.view.physicalSize = const Size(1600, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(body: StudentsPage(store: store)),
        ),
      );
      await tester.enterText(find.byType(TextField).first, student.code);
      await tester.pump();
      await tester.tap(find.widgetWithText(TextButton, student.name));
      await tester.pumpAndSettle();
      expect(find.byType(StudentProfilePage), findsOneWidget);
      expect(find.text('ملاحظة أولى'), findsOneWidget);
      await tester.runAsync(
        () => store.saveStudent(student.copyWith(notes: 'ملاحظة محدثة')),
      );
      await tester.pumpAndSettle();
      expect(find.text('ملاحظة محدثة'), findsOneWidget);
      await tester.tap(find.text('السجل الكامل').first);
      await tester.pumpAndSettle();
      expect(find.text('الحصص والحضور'), findsOneWidget);
      expect(find.text('الامتحانات السابقة'), findsOneWidget);
      expect(tester.takeException(), isNull);
      await tester.tap(find.byType(BackButton));
      await tester.pumpAndSettle();
      expect(find.byType(StudentProfilePage), findsNothing);
      expect(
        tester.widget<TextField>(find.byType(TextField).first).controller!.text,
        student.code,
      );
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );
}
