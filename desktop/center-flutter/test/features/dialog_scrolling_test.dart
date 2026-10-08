import 'dart:io';
import '../helpers/ui_wait_helpers.dart';
import 'dart:ui' as ui;
import 'package:flutter/rendering.dart';

import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/student_editor_dialog.dart';
import 'package:massar_center/shared/scrollable_dialog.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  Future<void> host(
    WidgetTester tester,
    Widget dialog, {
    double scale = 1,
    bool dark = false,
  }) async {
    await tester.binding.setSurfaceSize(const Size(960, 400));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    if (const bool.fromEnvironment('CAPTURE_UI')) {
      await tester.runAsync(() async {
        await (FontLoader('Tajawal')
              ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
              ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
            .load();
        await (FontLoader(
          'MaterialIcons',
        )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
      });
    }
    await tester.pumpWidget(
      MaterialApp(
        theme: dark ? MassarTheme.dark : MassarTheme.light,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: TextScaler.linear(scale)),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: child!,
          ),
        ),
        home: Scaffold(
          body: Builder(
            builder: (context) => Center(
              child: TextButton(
                onPressed: () => showDialog<void>(
                  context: context,
                  builder: (_) =>
                      RepaintBoundary(key: const Key('capture'), child: dialog),
                ),
                child: const Text('فتح النموذج'),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.text('فتح النموذج'));
    await tester.pumpAndSettle();
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    await tester.runAsync(() async {
      final boundary = tester.renderObject<RenderRepaintBoundary>(
        find.byKey(const Key('capture')),
      );
      final image = await boundary.toImage();
      final data = await image.toByteData(format: ui.ImageByteFormat.png);
      await File(
        'build/verification/$name.png',
      ).writeAsBytes(data!.buffer.asUint8List());
      image.dispose();
    });
  }

  void visible(WidgetTester tester, Finder target, Finder viewport) {
    final bounds = tester.getRect(viewport);
    final rect = tester.getRect(target);
    expect(rect.top, greaterThanOrEqualTo(bounds.top));
    expect(rect.bottom, lessThanOrEqualTo(bounds.bottom));
    expect(target.hitTestable(), findsOneWidget);
  }

  Future<void> wheel(WidgetTester tester, Finder viewport) async {
    final pointer = TestPointer(17, PointerDeviceKind.mouse);
    await tester.sendEventToBinding(pointer.hover(tester.getCenter(viewport)));
    await tester.sendEventToBinding(pointer.scroll(const Offset(0, 8000)));
    await tester.pumpAndSettle();
    await tester.sendEventToBinding(pointer.removePointer());
  }

  for (final scale in [1.0, 2.0]) {
    testWidgets('short dialog reaches last field and actions at scale $scale', (
      tester,
    ) async {
      final controller = ScrollController();
      addTearDown(controller.dispose);
      var accepted = false;
      await host(
        tester,
        ScrollableMassarDialog(
          title: const Text('نموذج طويل لكل البيانات'),
          scrollController: controller,
          content: Column(
            children: List.generate(
              12,
              (index) => Padding(
                padding: const EdgeInsets.only(bottom: 20),
                child: TextFormField(
                  key: Key('field-$index'),
                  decoration: InputDecoration(labelText: 'الحقل ${index + 1}'),
                ),
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: () {}, child: const Text('إلغاء')),
            FilledButton(
              onPressed: () => accepted = true,
              child: const Text('حفظ النموذج'),
            ),
          ],
        ),
        scale: scale,
        dark: scale == 2,
      );
      final viewport = find.byType(SingleChildScrollView);
      expect(find.text('حفظ النموذج').hitTestable(), findsNothing);
      await wheel(tester, viewport);
      visible(tester, find.byKey(const Key('field-11')), viewport);
      visible(
        tester,
        find.widgetWithText(FilledButton, 'حفظ النموذج'),
        viewport,
      );
      await tester.tap(find.text('حفظ النموذج'));
      expect(accepted, isTrue);

      // Use the actual desktop thumb, rather than programmatically revealing a
      // field: dragging it must move the same viewport in RTL as wheel input.
      controller.jumpTo(0);
      await tester.pumpAndSettle();
      final bounds = tester.getRect(viewport);
      final mouse = await tester.createGesture(kind: PointerDeviceKind.mouse);
      await mouse.addPointer(
        location: Offset(bounds.left + 2, bounds.top + 16),
      );
      await tester.pump();
      await mouse.down(Offset(bounds.left + 2, bounds.top + 16));
      await tester.pumpAndSettle();
      await mouse.moveTo(Offset(bounds.left + 2, bounds.bottom - 10));
      await mouse.up();
      await tester.pumpAndSettle();
      expect(controller.offset, greaterThan(0));
      visible(
        tester,
        find.widgetWithText(FilledButton, 'حفظ النموذج'),
        viewport,
      );
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets(
    'Tab reveals every field and the final action in a short dialog',
    (tester) async {
      final nodes = List.generate(8, (_) => FocusNode());
      final saveFocus = FocusNode();
      addTearDown(() {
        for (final node in nodes) {
          node.dispose();
        }
        saveFocus.dispose();
      });
      var saved = false;
      await host(
        tester,
        ScrollableMassarDialog(
          title: const Text('التنقل بلوحة المفاتيح'),
          content: Column(
            children: List.generate(
              nodes.length,
              (index) => Padding(
                padding: const EdgeInsets.only(bottom: 24),
                child: TextFormField(
                  key: Key('tab-$index'),
                  focusNode: nodes[index],
                  autofocus: index == 0,
                  decoration: InputDecoration(labelText: 'بيانات $index'),
                ),
              ),
            ),
          ),
          actions: [
            FilledButton(
              focusNode: saveFocus,
              onPressed: () => saved = true,
              child: const Text('حفظ بالتنقل'),
            ),
          ],
        ),
        scale: 2,
      );
      final viewport = find.byType(SingleChildScrollView);
      expect(nodes.first.hasFocus, isTrue);
      for (var index = 1; index < nodes.length; index++) {
        await tester.sendKeyEvent(LogicalKeyboardKey.tab);
        await tester.pumpAndSettle();
        expect(nodes[index].hasFocus, isTrue);
        visible(tester, find.byKey(Key('tab-$index')), viewport);
      }
      await tester.sendKeyEvent(LogicalKeyboardKey.tab);
      await tester.pumpAndSettle();
      expect(saveFocus.hasFocus, isTrue);
      visible(
        tester,
        find.widgetWithText(FilledButton, 'حفظ بالتنقل'),
        viewport,
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(saved, isTrue);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('adjacent and nested scroll panes keep independent positions', (
    tester,
  ) async {
    final controllers = List.generate(3, (_) => ScrollController());
    addTearDown(() {
      for (final controller in controllers) {
        controller.dispose();
      }
    });
    await tester.binding.setSurfaceSize(const Size(960, 400));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Row(
            children: [
              Expanded(
                child: MassarScrollView(
                  key: const Key('outer'),
                  controller: controllers[0],
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      SizedBox(
                        height: 180,
                        child: MassarScrollView(
                          key: const Key('inner'),
                          controller: controllers[1],
                          child: const SizedBox(height: 1600),
                        ),
                      ),
                      const SizedBox(height: 1800),
                    ],
                  ),
                ),
              ),
              Expanded(
                child: MassarScrollView(
                  key: const Key('adjacent'),
                  controller: controllers[2],
                  child: const SizedBox(height: 2400),
                ),
              ),
            ],
          ),
        ),
      ),
    );
    await wheel(tester, find.byKey(const Key('inner')));
    expect(controllers[1].offset, greaterThan(0));
    expect(controllers[0].offset, 0);
    expect(controllers[2].offset, 0);
    await wheel(tester, find.byKey(const Key('adjacent')));
    expect(controllers[2].offset, greaterThan(0));
    expect(controllers[0].offset, 0);
    await wheel(tester, find.byKey(const Key('outer')));
    expect(controllers[0].offset, greaterThan(0));
    // Disposal of the widgets must not dispose caller-owned controllers.
    await tester.pumpWidget(const SizedBox());
    controllers.first.addListener(() {});
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'student editor with many groups scrolls to notes and cancels safely',
    (tester) async {
      late Directory directory;
      late CenterStore store;
      await tester.runAsync(() async {
        directory = await Directory.systemTemp.createTemp(
          'massar-dialog-scroll-',
        );
        store = await CenterStore.open(directory: directory.path);
        await store.setupAdmin('إدارة النوافذ', 'dialog-scroll-password');
        for (final kind in CatalogKind.values) {
          await store.saveCatalog(CatalogEntry(kind: kind, name: kind.name));
        }
        for (var index = 0; index < 18; index++) {
          await store.saveGroup(
            StudyGroup(
              name: 'المجموعة ${index + 1}',
              subjectId: store.catalogs
                  .firstWhere((c) => c.kind == CatalogKind.subject)
                  .id,
              centerId: store.catalogs
                  .firstWhere((c) => c.kind == CatalogKind.center)
                  .id,
              gradeId: store.catalogs
                  .firstWhere((c) => c.kind == CatalogKind.grade)
                  .id,
              sessionPrice: 10000,
              packagePrice: 40000,
            ),
          );
        }
        await store.registerStudent(
          Student(
            name: 'طالب محفوظ',
            code: '',
            groupIds: [store.groups.first.id],
            notes: 'ملاحظة أصلية',
            createdAt: DateTime.now(),
          ),
        );
      });
      addTearDown(
        () => tester.runAsync(() async {
          await store.close();
          await directory.delete(recursive: true);
        }),
      );
      final original = store.students.single;
      final auditCount = store.audit.length;
      await host(
        tester,
        StudentEditorDialog(store: store, student: original),
        scale: 2,
        dark: true,
      );
      await capture(tester, 'student-editor-scroll-dark960x400-top');
      await tester.enterText(
        find.widgetWithText(TextFormField, 'اسم الطالب'),
        'اسم في المسودة فقط',
      );
      await tester.pumpAndSettle();
      final viewport = find.byType(SingleChildScrollView);
      await wheel(tester, viewport);
      final notes = find.widgetWithText(TextFormField, 'ملاحظات الطالب');
      final cancel = find.widgetWithText(TextButton, 'إلغاء');
      visible(tester, notes, viewport);
      visible(tester, cancel, viewport);
      visible(
        tester,
        find.widgetWithText(FilledButton, 'حفظ التعديلات'),
        viewport,
      );
      await capture(tester, 'student-editor-scroll-dark960x400-bottom');
      await tester.enterText(notes, 'ملاحظة في المسودة فقط');
      await tester.tap(cancel);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('student-editor-discard')), findsOneWidget);
      expect(store.students.single.toJson(), original.toJson());
      await tester.runAsync(() async {
        await tester.ensureVisible(
          find.byKey(const Key('student-editor-discard-changes')),
        );
        await tester.tap(
          find.byKey(const Key('student-editor-discard-changes')),
        );
        await waitForUiCondition(
          tester,
          () => find.byType(StudentEditorDialog).evaluate().isEmpty,
          reason:
              'Discarding closes the editor after the confirmation route exits.',
        );
      });
      expect(find.byType(StudentEditorDialog), findsNothing);
      await tester.runAsync(() async {
        await store.close();
        store = await CenterStore.open(directory: directory.path);
      });
      expect(store.students.single.toJson(), original.toJson());
      expect(store.groups, hasLength(18));
      expect(store.audit, hasLength(auditCount));
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );
}
