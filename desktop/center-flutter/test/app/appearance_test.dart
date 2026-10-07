import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/main.dart';
import 'package:massar_center/shared/appearance.dart';

void main() {
  test(
    'device appearance survives restart and failed saving retains saved mode',
    () async {
      final dir = await Directory.systemTemp.createTemp('massar-appearance-');
      addTearDown(() => dir.delete(recursive: true));
      final file = File('${dir.path}/appearance.json');
      final first = AppearanceSettings(file);
      addTearDown(first.dispose);
      await first.load();
      expect(first.mode, ThemeMode.dark);
      await first.toggle();
      final restarted = AppearanceSettings(file);
      addTearDown(restarted.dispose);
      await restarted.load();
      expect(restarted.mode, ThemeMode.light);
      await Directory('${file.path}.tmp').create();
      await expectLater(
        restarted.toggle(),
        throwsA(isA<FileSystemException>()),
      );
      expect(restarted.mode, ThemeMode.light);
      expect(restarted.error, contains('تعذر حفظ'));
      final afterFailure = AppearanceSettings(file);
      addTearDown(afterFailure.dispose);
      await afterFailure.load();
      expect(afterFailure.mode, ThemeMode.light);
    },
  );

  test(
    'invalid appearance is visible and can be repaired by a new choice',
    () async {
      final dir = await Directory.systemTemp.createTemp(
        'massar-appearance-invalid-',
      );
      addTearDown(() => dir.delete(recursive: true));
      final file = File('${dir.path}/appearance.json');
      await file.writeAsString('{invalid');
      final settings = AppearanceSettings(file);
      addTearDown(settings.dispose);
      await settings.load();
      expect(settings.error, contains('تعذر قراءة'));
      expect(settings.mode, ThemeMode.dark);
      await settings.toggle();
      expect(settings.error, isNull);
      final reopened = AppearanceSettings(file);
      addTearDown(reopened.dispose);
      await reopened.load();
      expect(reopened.mode, ThemeMode.light);
    },
  );

  testWidgets(
    'theme toggle preserves login input and student note draft across modes',
    (tester) async {
      late Directory dir;
      late CenterStore store;
      await tester.runAsync(() async {
        dir = await Directory.systemTemp.createTemp('massar-theme-ui-');
        store = await CenterStore.open(directory: dir.path);
        await store.setupAdmin('مدير', 'appearance-test-password');
        for (final kind in CatalogKind.values) {
          await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
        }
        await store.saveGroup(
          StudyGroup(
            name: 'مجموعة التجربة',
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
            packagePrice: 24000,
          ),
        );
        await store.saveStudent(
          Student(
            name: 'مينا عادل',
            code: '321',
            groupIds: [store.groups.single.id],
            notes: 'ملاحظة الطالب المحفوظة',
            createdAt: DateTime.now().subtract(const Duration(days: 10)),
          ),
        );
        await store.saveSession(
          LessonSession(
            groupId: store.groups.single.id,
            number: 1,
            createdAt: DateTime.now(),
            startsAt: DateTime.now().add(const Duration(minutes: 1)),
          ),
        );
        store.signOut();
        final fonts = FontLoader('Tajawal')
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf'));
        await fonts.load();
        await (FontLoader(
          'MaterialIcons',
        )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
      });
      addTearDown(
        () => TestWidgetsFlutterBinding.instance.runAsync(() async {
          await store.close();
          await dir.delete(recursive: true);
        }),
      );
      await tester.binding.setSurfaceSize(const Size(1440, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final capture = GlobalKey();
      await tester.pumpWidget(
        RepaintBoundary(
          key: capture,
          child: CenterApp(store: store),
        ),
      );
      await tester.pumpAndSettle();
      final settings = AppearanceScope.maybeOf(
        tester.element(find.byKey(const Key('auth-name'))),
      )!;

      Future<void> waitForAppearance() async {
        final deadline = DateTime.now().add(const Duration(seconds: 5));
        while (settings.busy && DateTime.now().isBefore(deadline)) {
          await tester.runAsync(
            () => Future<void>.delayed(const Duration(milliseconds: 10)),
          );
          await tester.pump();
        }
        expect(
          settings.busy,
          isFalse,
          reason: 'File-backed appearance operation should complete',
        );
        await tester.pumpAndSettle();
      }

      Future<void> toggle() async {
        await tester.runAsync(
          () => tester.tap(find.byKey(const Key('appearance-toggle'))),
        );
        await waitForAppearance();
      }

      Future<void> screenshot(String name) async {
        if (!const bool.fromEnvironment('CAPTURE_UI')) return;
        await tester.runAsync(() async {
          final boundary =
              capture.currentContext!.findRenderObject()
                  as RenderRepaintBoundary;
          final image = await boundary.toImage(pixelRatio: 1);
          final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
          await Directory('build/verification').create(recursive: true);
          await File(
            'build/verification/$name.png',
          ).writeAsBytes(bytes!.buffer.asUint8List());
          image.dispose();
        });
      }

      await waitForAppearance();
      await tester.enterText(find.byKey(const Key('auth-name')), 'مدير');
      expect(
        Theme.of(tester.element(find.byKey(const Key('auth-name')))).brightness,
        Brightness.dark,
      );
      await screenshot('login-dark');
      await toggle();
      expect(settings.mode, ThemeMode.light);
      expect(
        Theme.of(tester.element(find.byKey(const Key('auth-name')))).brightness,
        Brightness.light,
      );
      expect(
        tester
            .widget<TextFormField>(find.byKey(const Key('auth-name')))
            .controller!
            .text,
        'مدير',
      );
      await tester.runAsync(
        () => store.signIn('مدير', 'appearance-test-password'),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('التحضير والتحصيل'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('student-search')), '321');
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();
      expect(find.text('ملاحظة الطالب المحفوظة'), findsOneWidget);
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('student-note-editor')),
        'مسودة لم تُحفظ بعد',
      );
      await toggle();
      expect(
        Theme.of(
          tester.element(find.byKey(const Key('student-search'))),
        ).brightness,
        Brightness.dark,
      );
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('student-note-editor')))
            .controller!
            .text,
        'مسودة لم تُحفظ بعد',
      );
      expect(store.payments, isEmpty);
      expect(store.students.single.notes, 'ملاحظة الطالب المحفوظة');
      await screenshot('focus-theme-draft-dark');
      await tester.tap(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('student-search')))
            .focusNode!
            .hasFocus,
        isTrue,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
