import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/backup_page.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/shared/problem_log.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';
import '../helpers/ui_wait_helpers.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late ProblemLog log;
  String? destination;
  String? copiedPath;
  var chooserCalls = 0;
  final captureKey = GlobalKey();
  const selector = MethodChannel('plugins.flutter.io/file_selector');

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-problem-export-',
      );
      store = await CenterStore.open(directory: '${directory.path}/data');
      await store.setupAdmin('الإدارة', 'export-log-password');
      log = ProblemLog(Directory('${directory.path}/logs'));
      ProblemLog.current = log;
      await log.startSession();
      log.record(
        const FormatException('fixture failure'),
        StackTrace.current,
        operation: 'ui.backup_page',
      );
      await log.flush();
      destination = '${directory.path}/support.txt';
      copiedPath = null;
      chooserCalls = 0;
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(selector, (call) async {
            expect(call.method, 'getSavePath');
            chooserCalls++;
            expect((call.arguments as Map)['suggestedName'], endsWith('.txt'));
            return destination;
          });
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform, (call) async {
            if (call.method == 'Clipboard.setData') {
              copiedPath = (call.arguments as Map)['text'] as String;
            }
            return null;
          });
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      ProblemLog.current = null;
      await log.flush();
      await store.close();
      await directory.delete(recursive: true);
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(selector, null);
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(SystemChannels.platform, null);
    }),
  );

  Future<void> open(
    WidgetTester tester, {
    bool dark = false,
    bool shell = false,
    double width = 1280,
  }) async {
    await tester.binding.setSurfaceSize(Size(width, 800));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.runAsync(() async {
      await (FontLoader('Tajawal')
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
          .load();
      await (FontLoader(
        'MaterialIcons',
      )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    });
    await tester.pumpWidget(
      RepaintBoundary(
        key: captureKey,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: Scaffold(
              body: shell
                  ? ManagementWorkspace(store: store, onOpenAttendance: () {})
                  : BackupPage(store: store),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    if (shell) {
      await tester.scrollUntilVisible(
        find.widgetWithText(ListTile, 'النسخ والسجل'),
        150,
        scrollable: find.descendant(
          of: find.byKey(const Key('management-navigation')),
          matching: find.byType(Scrollable),
        ),
      );
      await tester.tap(find.widgetWithText(ListTile, 'النسخ والسجل'));
      await tester.pumpAndSettle();
    }
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (Platform.environment['CAPTURE_UI'] != 'true') return;
    await tester.runAsync(() async {
      final boundary =
          captureKey.currentContext!.findRenderObject()
              as RenderRepaintBoundary;
      final screenshot = await boundary.toImage(pixelRatio: 1);
      final bytes = await screenshot.toByteData(format: ui.ImageByteFormat.png);
      final output = File('build/verification/$name.png');
      await output.parent.create(recursive: true);
      await output.writeAsBytes(bytes!.buffer.asUint8List());
      screenshot.dispose();
    });
  }

  Future<void> export(WidgetTester tester, String message) async {
    await tester.runAsync(() async {
      await tester.tap(find.byKey(const Key('export-problem-log')));
      await acknowledgeNotice(tester, message: message);
    });
  }

  testWidgets(
    'exports real log once without modifying SQLite, backup, audit or source log',
    (tester) async {
      late List<int> databaseBefore, backupBefore;
      late String backupPath;
      late Map<String, String> originalLogs;
      final auditBefore = store.audit.map((record) => record.toJson()).toList();
      await tester.runAsync(() async {
        databaseBefore = await File(store.databasePath).readAsBytes();
        backupPath = await store.createBackup(
          destination: '${directory.path}/backup.json',
        );
        backupBefore = await File(backupPath).readAsBytes();
        originalLogs = {
          for (final file in Directory(
            log.directoryPath,
          ).listSync().whereType<File>())
            file.path: await file.readAsString(),
        };
      });
      await open(tester);
      expect(
        tester
            .widget<SelectableText>(find.byKey(const Key('problem-log-path')))
            .data,
        log.directoryPath,
      );
      final action = tester
          .widget<OutlinedButton>(find.byKey(const Key('export-problem-log')))
          .onPressed!;
      await tester.runAsync(() async {
        action();
        action();
        await waitForUiCondition(
          tester,
          () =>
              File(destination!).existsSync() &&
              tester
                      .widget<OutlinedButton>(
                        find.byKey(const Key('export-problem-log')),
                      )
                      .onPressed !=
                  null,
          reason: 'Diagnostics export finishes and re-enables its button.',
        );
      });
      expect(chooserCalls, 1);
      await tester.runAsync(() async {
        final exported = await File(destination!).readAsString();
        expect(exported, contains('ui.backup_page'));
        expect(await File(store.databasePath).readAsBytes(), databaseBefore);
        expect(await File(backupPath).readAsBytes(), backupBefore);
        for (final entry in originalLogs.entries) {
          expect(await File(entry.key).readAsString(), entry.value);
        }
      });
      expect(
        store.audit.map((record) => record.toJson()).toList(),
        auditBefore,
      );
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  for (final unsafe in [
    'existing',
    'database',
    'wrong-extension',
    'source-folder',
  ]) {
    testWidgets('rejects unsafe destination $unsafe without replacing any file', (
      tester,
    ) async {
      late List<int> protectedBefore;
      await tester.runAsync(() async {
        switch (unsafe) {
          case 'existing':
            await File(destination!).writeAsString('existing support file');
            break;
          case 'database':
            destination = store.databasePath;
            break;
          case 'wrong-extension':
            destination = '${directory.path}/new-backup.json';
            break;
          case 'source-folder':
            destination = '${log.directoryPath}/new-export.txt';
            break;
        }
        if (await File(destination!).exists()) {
          protectedBefore = await File(destination!).readAsBytes();
        }
      });
      await open(tester);
      await export(
        tester,
        'اختار ملفًا جديدًا بامتداد .txt خارج مجلد السجل. لا يمكن استبدال ملف موجود.',
      );
      await tester.runAsync(() async {
        if (unsafe == 'existing' || unsafe == 'database') {
          expect(await File(destination!).readAsBytes(), protectedBefore);
        } else {
          expect(await File(destination!).exists(), isFalse);
        }
      });
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets(
    'canceled chooser writes nothing and copy copies the actual local path',
    (tester) async {
      destination = null;
      await open(tester);
      await tester.runAsync(
        () => tester.tap(find.byKey(const Key('export-problem-log'))),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      expect(chooserCalls, 1);
      await tester.runAsync(() async {
        await tester.tap(find.byKey(const Key('copy-problem-log-path')));
        await waitForUiCondition(
          tester,
          () => copiedPath == log.directoryPath,
          reason: 'The clipboard receives the actual log directory.',
        );
      });
      expect(copiedPath, log.directoryPath);
      expect(
        tester
            .widget<OutlinedButton>(find.byKey(const Key('export-problem-log')))
            .onPressed,
        isNotNull,
      );
    },
  );

  testWidgets('export filesystem failure is logged and shows an error popup', (
    tester,
  ) async {
    await tester.runAsync(() async {
      await File('${directory.path}/blocked').writeAsString('not a directory');
      destination = '${directory.path}/blocked/support.txt';
    });
    await open(tester);
    await export(
      tester,
      'تعذر تصدير سجل المشاكل. اختار مكانًا آخر وراجع صلاحية الكتابة أو مساحة الجهاز.',
    );
    await tester.runAsync(() async {
      await log.flush();
      final contents = Directory(log.directoryPath)
          .listSync()
          .whereType<File>()
          .map((file) => file.readAsStringSync())
          .join();
      expect(contents, contains('diagnostics.export'));
      expect(
        await File('${directory.path}/blocked').readAsString(),
        'not a directory',
      );
    });
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'write failure warns without opening chooser and missing log disables controls',
    (tester) async {
      await tester.runAsync(() async {
        final blockedPath = '${directory.path}/blocked-log';
        await File(blockedPath).writeAsString('keep');
        log = ProblemLog(Directory(blockedPath));
        ProblemLog.current = log;
        await log.startSession();
      });
      expect(log.writeFailure, isNotNull);
      await open(tester);
      await export(
        tester,
        'تعذر حفظ سجل المشاكل محليًا. راجع صلاحية الكتابة أو مساحة الجهاز قبل التصدير.',
      );
      expect(chooserCalls, 0);
      ProblemLog.current = null;
      await open(tester);
      expect(
        tester
            .widget<OutlinedButton>(find.byKey(const Key('export-problem-log')))
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<OutlinedButton>(
              find.byKey(const Key('copy-problem-log-path')),
            )
            .onPressed,
        isNull,
      );
    },
  );

  testWidgets('cashier cannot access support exports even via direct page', (
    tester,
  ) async {
    await tester.runAsync(() async {
      await store.saveStaff(
        name: 'استقبال',
        password: 'cashier-export-password',
        role: StaffRole.cashier,
      );
      await store.signIn('استقبال', 'cashier-export-password');
    });
    await open(tester);
    expect(find.text('النسخ والسجل متاحان للإدارة فقط.'), findsOneWidget);
    expect(find.byKey(const Key('problem-log-section')), findsNothing);
    expect(chooserCalls, 0);
  });

  for (final dark in [false, true]) {
    for (final width in [1280.0, 960.0]) {
      testWidgets(
        'diagnostics stays readable within management shell dark=$dark width=$width',
        (tester) async {
          await open(tester, dark: dark, shell: true, width: width);
          final copy = find.byKey(const Key('copy-problem-log-path'));
          final exportButton = find.byKey(const Key('export-problem-log'));
          expect(tester.getRect(copy).bottom, lessThanOrEqualTo(800));
          expect(tester.getRect(exportButton).bottom, lessThanOrEqualTo(800));
          expect(find.text('سجل التغييرات'), findsOneWidget);
          expect(tester.takeException(), isNull);
          await capture(
            tester,
            'problem-log-${dark ? 'dark' : 'light'}-${width.toInt()}',
          );
        },
      );
    }
  }
}
