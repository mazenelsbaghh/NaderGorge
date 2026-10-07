import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/auth/auth_screen.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/main.dart';
import 'package:massar_center/shared/problem_log.dart';

import '../helpers/notice_helpers.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const provider = MethodChannel('plugins.flutter.io/path_provider');
  const selector = MethodChannel('plugins.flutter.io/file_selector');
  const corruptContent = 'PRIVATE-BOOTSTRAP-SENTINEL: not a SQLite database';
  late Directory directory;
  late Directory support;
  late File databaseFile;
  late ProblemLog log;
  ProblemLog? previousLog;
  CenterStore? openedStore;
  String? destination;
  var supportCalls = 0;
  var chooserCalls = 0;

  setUp(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-bootstrap-scenarios-',
      );
      support = Directory('${directory.path}/support');
      databaseFile = File('${support.path}/massar-center/center.sqlite');
      await databaseFile.parent.create(recursive: true);
      await databaseFile.writeAsString(corruptContent, flush: true);
      previousLog = ProblemLog.current;
      log = ProblemLog(Directory('${directory.path}/logs'));
      ProblemLog.current = log;
      await log.startSession();
      openedStore = null;
      destination = null;
      supportCalls = 0;
      chooserCalls = 0;
      // The installed unit-test default is MethodChannelPathProvider. No
      // unhandled request is forwarded to a real native directory provider.
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(provider, (call) async {
            expect(call.method, 'getApplicationSupportDirectory');
            supportCalls++;
            return support.path;
          });
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(selector, (call) async {
            expect(call.method, 'getSavePath');
            expect(
              (call.arguments as Map)['suggestedName'],
              'massar-startup-problems.txt',
            );
            chooserCalls++;
            return destination;
          });
      await (FontLoader('Tajawal')
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
          .load();
      await (FontLoader(
        'MaterialIcons',
      )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await openedStore?.close();
      await log.flush();
      ProblemLog.current = previousLog;
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(provider, null);
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(selector, null);
      await directory.delete(recursive: true);
    }),
  );

  Future<void> waitFor(WidgetTester tester, Finder finder) async {
    for (var attempt = 0; attempt < 150; attempt++) {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      await tester.pump(const Duration(milliseconds: 20));
      if (finder.evaluate().isNotEmpty) break;
    }
    expect(finder, findsOneWidget);
  }

  Future<void> openFailedBootstrap(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(1440, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(const CenterBootstrap());
    await waitFor(
      tester,
      find.text('تعذر فتح البيانات المحلية. البيانات لم تُحذف.'),
    );
    expect(supportCalls, 1);
    expect(find.byType(CenterApp), findsNothing);
    expect(find.textContaining(corruptContent), findsNothing);
    expect(find.textContaining('DatabaseException'), findsNothing);
    expect(find.text('إعادة المحاولة'), findsOneWidget);
    expect(find.text('تصدير سجل المشاكل'), findsOneWidget);
    expect(await databaseFile.readAsString(), corruptContent);
    expect(tester.takeException(), isNull);
  }

  Finder exportButton() =>
      find.widgetWithText(OutlinedButton, 'تصدير سجل المشاكل');

  testWidgets(
    'startup failure stays generic and cancelling export leaves broken database and logs intact',
    (tester) async {
      await tester.runAsync(() async {
        await openFailedBootstrap(tester);
        await log.flush();
        final sourceLog = File('${log.directoryPath}/problems.jsonl');
        final before = await sourceLog.readAsString();
        expect(before, contains('database.open'));
        expect(before, isNot(contains(corruptContent)));
        await tester.tap(exportButton());
        for (var attempt = 0; attempt < 20; attempt++) {
          await Future<void>.delayed(const Duration(milliseconds: 20));
          await tester.pump();
          if (tester.widget<OutlinedButton>(exportButton()).onPressed != null) {
            break;
          }
        }
        expect(chooserCalls, 1);
        expect(
          tester.widget<OutlinedButton>(exportButton()).onPressed,
          isNotNull,
        );
        expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
        expect(await sourceLog.readAsString(), before);
        expect(await databaseFile.readAsString(), corruptContent);
        expect(
          await directory
              .list(recursive: true)
              .where((e) => e.path.endsWith('.txt'))
              .toList(),
          isEmpty,
        );
        expect(tester.takeException(), isNull);
      });
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets(
    'failed startup export preserves existing file then a fresh export saves sanitized real diagnostics once',
    (tester) async {
      await tester.runAsync(() async {
        await openFailedBootstrap(tester);
        final existing = File('${directory.path}/existing.txt');
        await existing.writeAsString(
          'preserve this existing file',
          flush: true,
        );
        destination = existing.path;
        await tester.tap(exportButton());
        await acknowledgeNotice(
          tester,
          message: 'تعذر تصدير سجل المشاكل. اختر مكانًا آخر قابلًا للحفظ.',
        );
        expect(await existing.readAsString(), 'preserve this existing file');
        expect(await databaseFile.readAsString(), corruptContent);
        expect(chooserCalls, 1);
        destination = '${directory.path}/exported-problems.txt';
        final action = tester.widget<OutlinedButton>(exportButton()).onPressed!;
        action();
        action();
        await acknowledgeNotice(tester, message: 'تم تصدير سجل المشاكل.');
        expect(chooserCalls, 2);
        final exported = await File(destination!).readAsString();
        final rows = const LineSplitter()
            .convert(exported)
            .map((line) => jsonDecode(line) as Map)
            .toList();
        expect(rows.first['kind'], 'export');
        expect(rows.first['eventCount'], rows.length - 1);
        expect(
          rows.skip(1).map((row) => row['operation']),
          contains('database.open'),
        );
        expect(
          rows.skip(1).map((row) => row['operation']),
          contains('startup.export'),
        );
        expect(exported, isNot(contains(corruptContent)));
        expect(exported, isNot(contains(directory.path)));
        expect(await existing.readAsString(), 'preserve this existing file');
        expect(await databaseFile.readAsString(), corruptContent);
        expect(find.byType(CenterApp), findsNothing);
        expect(tester.takeException(), isNull);
      });
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets(
    'retry opens repaired temporary SQLite and preserves existing administrator credentials and catalog',
    (tester) async {
      await tester.runAsync(() async {
        await openFailedBootstrap(tester);
        for (final suffix in ['', '-wal', '-shm']) {
          final file = File('${databaseFile.path}$suffix');
          if (await file.exists()) await file.delete();
        }
        final repaired = await CenterStore.open(
          directory: databaseFile.parent.path,
        );
        late String adminId;
        late CatalogEntry catalog;
        late List<String> auditIds;
        try {
          await repaired.setupAdmin('preserved-admin', 'preserved-admin-pass');
          adminId = repaired.staff.single.id;
          await repaired.saveCatalog(
            const CatalogEntry(name: 'مادة محفوظة', kind: CatalogKind.subject),
          );
          catalog = repaired.catalogs.single;
          auditIds = repaired.audit.map((e) => e.id).toList();
        } finally {
          await repaired.close();
        }
        // Regression 2026-10-02: returning the new Future from setState's
        // callback asserted and left retry stuck on the startup error screen.
        await tester.tap(find.widgetWithText(OutlinedButton, 'إعادة المحاولة'));
        await waitFor(tester, find.byType(CenterApp));
        openedStore = tester.widget<CenterApp>(find.byType(CenterApp)).store;
        final store = openedStore!;
        expect(supportCalls, 2);
        expect(store.databasePath, databaseFile.path);
        expect(store.currentUser, isNull);
        expect(store.canManage, isFalse);
        expect(
          store.staff.where((e) => e.name == 'preserved-admin').single.id,
          adminId,
        );
        expect(store.catalogs.single.toJson(), catalog.toJson());
        expect(store.audit.take(auditIds.length).map((e) => e.id), auditIds);
        expect(store.audit.last.action, 'installation_admin');
        expect(find.byType(AuthScreen), findsOneWidget);
        expect(
          find.text('تعذر فتح البيانات المحلية. البيانات لم تُحذف.'),
          findsNothing,
        );
        final signedIn = Completer<void>();
        void observeLogin() {
          if (store.currentUser != null && !signedIn.isCompleted) {
            signedIn.complete();
          }
        }

        store.addListener(observeLogin);
        try {
          await tester.enterText(
            find.byKey(const Key('auth-name')),
            'preserved-admin',
          );
          await tester.enterText(
            find.byKey(const Key('auth-password')),
            'preserved-admin-pass',
          );
          await tester.tap(find.byKey(const Key('auth-submit')));
          await signedIn.future.timeout(const Duration(seconds: 10));
        } finally {
          store.removeListener(observeLogin);
        }
        await tester.pumpAndSettle();
        expect(store.currentUser?.id, adminId);
        expect(store.canManage, isTrue);
        expect(find.byType(ManagementWorkspace), findsOneWidget);
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
        await store.close();
        final persisted = await CenterStore.open(
          directory: databaseFile.parent.path,
        );
        try {
          await persisted.signIn('preserved-admin', 'preserved-admin-pass');
          expect(persisted.currentUser?.id, adminId);
          expect(persisted.catalogs.single.toJson(), catalog.toJson());
        } finally {
          await persisted.close();
        }
      });
    },
  );
}
