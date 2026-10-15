import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/features/exams/host_exam_import_page.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/standalone_exam_fixture.dart';
import '../helpers/ui_wait_helpers.dart';

void main() {
  const chooser = MethodChannel('plugins.flutter.io/file_selector');
  setUpAll(() async {
    await (FontLoader('Tajawal')
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Medium.ttf'))
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
        .load();
    await (FontLoader(
      'MaterialIcons',
    )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
  });
  for (final changeEmployeeSession in [false, true]) {
    testWidgets(
      'host bulk import and renamed XLSX export ${changeEmployeeSession ? 'rejects a logout during the chooser' : 'records all exact matches with one confirmation'}',
      (tester) async {
        late StandaloneExamFixture fixture;
        await tester.runAsync(() async {
          fixture = await StandaloneExamFixture.open();
        });
        addTearDown(() => tester.runAsync(fixture.close));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        await tester.binding.setSurfaceSize(const Size(720, 1000));
        final output = '${fixture.directory.path}/downloads/متابعة مخصصة.csv';
        await tester.runAsync(
          () => Directory('${fixture.directory.path}/downloads').create(),
        );
        var choices = 0;
        tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
          chooser,
          (call) async {
            if (call.method != 'getSavePath') {
              throw StateError('Unexpected chooser method: ${call.method}');
            }
            choices++;
            if (changeEmployeeSession) {
              fixture.store.signOut();
              await fixture.store.signIn(
                'موظف التقرير',
                'synthetic-report-password',
              );
            }
            return output;
          },
        );
        addTearDown(
          () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
            chooser,
            null,
          ),
        );
        final captureKey = GlobalKey();
        await tester.pumpWidget(
          RepaintBoundary(
            key: captureKey,
            child: MaterialApp(
              theme: MassarTheme.dark,
              locale: const Locale('ar', 'EG'),
              supportedLocales: const [Locale('ar', 'EG')],
              localizationsDelegates: GlobalMaterialLocalizations.delegates,
              home: Scaffold(body: HostExamImportPage(store: fixture.store)),
            ),
          ),
        );
        await tester.pumpAndSettle();
        for (final (field, label) in [
          (1, 'مجموعة التقرير'),
          (2, 'حصة 1'),
          (3, 'امتحان من عشرين'),
        ]) {
          final dropdown = find
              .byType(DropdownButtonFormField<String>)
              .at(field);
          await tester.ensureVisible(dropdown);
          await tester.tap(dropdown);
          await tester.pumpAndSettle();
          await tester.tap(find.textContaining(label).last);
          await tester.pumpAndSettle();
        }
        expect(find.text('الحاضرون: 4'), findsOneWidget);
        expect(find.text('حاضر ولم يدخل: 1'), findsOneWidget);
        expect(find.text('جاهز للرصد: 2'), findsOneWidget);
        expect(find.textContaining('سيستبدل 1 درجة'), findsOneWidget);
        expect(find.byType(Checkbox), findsNothing);
        if (!changeEmployeeSession) {
          final boundary = tester.renderObject<RenderRepaintBoundary>(
            find.byKey(captureKey),
          );
          await tester.runAsync(() async {
            final image = await boundary.toImage(pixelRatio: 1);
            try {
              final bytes = await image.toByteData(
                format: ui.ImageByteFormat.png,
              );
              final directory = Directory(
                '../../artifacts/private/windows-exam-integration',
              );
              await directory.create(recursive: true);
              await File(
                '${directory.path}/host-standalone-import.png',
              ).writeAsBytes(bytes!.buffer.asUint8List(), flush: true);
            } finally {
              image.dispose();
            }
          });
          final confirm = find.byKey(
            const Key('host-confirm-standalone-grades'),
          );
          await tester.ensureVisible(confirm);
          await tester.runAsync(() async {
            await tester.tap(confirm);
            await waitForUiCondition(
              tester,
              () =>
                  fixture.store.academics.length == 2 &&
                  find.textContaining('تم رصد 2 نتيجة').evaluate().isNotEmpty,
              reason: 'One host confirmation commits both matched grades.',
            );
          });
          expect(
            fixture.store.academics.map((row) => row.score),
            containsAll([0, 18.5]),
          );
          expect(
            fixture.store.academics.map((row) => row.maxScore),
            everyElement(20),
          );
          expect(find.text('مرصود: 2'), findsOneWidget);
          expect(tester.widget<FilledButton>(confirm).onPressed, isNull);
        }
        final export = find.byKey(const Key('host-export-missing-exam'));
        await tester.ensureVisible(export);
        await tester.runAsync(() async {
          await tester.tap(export);
          await waitForUiCondition(
            tester,
            () =>
                choices == 1 &&
                export.evaluate().isNotEmpty &&
                tester.widget<OutlinedButton>(export).onPressed != null,
            reason: 'The file chooser and protected save have finished.',
          );
        });
        final expected = File(
          '${fixture.directory.path}/downloads/متابعة مخصصة.xlsx',
        );
        expect(await tester.runAsync(expected.exists), !changeEmployeeSession);
        if (!changeEmployeeSession) {
          expect(find.textContaining('تم حفظ شيت 1 حاضر'), findsOneWidget);
          final bytes = await tester.runAsync(expected.readAsBytes);
          expect(bytes!.take(2), [0x50, 0x4b]);
        }
        expect(await tester.runAsync(() => File(output).exists()), isFalse);
        expect(tester.takeException(), isNull);
      },
    );
  }
}
