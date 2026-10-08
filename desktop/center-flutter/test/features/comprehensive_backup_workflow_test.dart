import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/backup_page.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const selector = MethodChannel('plugins.flutter.io/file_selector');
  late Directory directory;
  late CenterStore store;
  String? destination;
  String? source;

  setUp(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-backup-workflow-',
      );
      store = await CenterStore.open(directory: '${directory.path}/data');
      await store.setupAdmin('مدير', 'backup-workflow-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة',
          subjectId: store.catalogs[0].id,
          centerId: store.catalogs[1].id,
          gradeId: store.catalogs[2].id,
        ),
      );
      await store.saveStudent(
        Student(
          name: 'طالب أصلي',
          code: 'B1',
          groupIds: [store.groups.single.id],
          createdAt: DateTime.now(),
        ),
      );
      destination = '${directory.path}/manual.json';
      source = null;
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(selector, (call) async {
            if (call.method == 'getSavePath') return destination;
            if (call.method == 'openFile') {
              return source == null ? null : [source!];
            }
            throw UnsupportedError('Unexpected chooser operation');
          });
    }),
  );
  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(selector, null);
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> open(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(1280, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: Scaffold(body: BackupPage(store: store)),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> waitFor(WidgetTester tester, bool Function() condition) async {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++) {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      await tester.pump(const Duration(milliseconds: 30));
    }
    expect(condition(), isTrue);
    await tester.pumpAndSettle();
  }

  testWidgets(
    'manual backup cancellation and existing-file error allow a subsequent real save without data changes',
    (tester) async {
      await tester.runAsync(() async {
        await open(tester);
        final before = store.students.single.toJson();
        final audit = store.audit.length;
        destination = null;
        await tester.tap(find.text('حفظ نسخة على الجهاز أو فلاشة'));
        await waitFor(
          tester,
          () => find.text('حفظ نسخة على الجهاز أو فلاشة').evaluate().isNotEmpty,
        );
        expect(find.byType(AlertDialog), findsNothing);
        destination = '${directory.path}/protected.json';
        await File(destination!).writeAsString('protected bytes');
        await tester.tap(find.text('حفظ نسخة على الجهاز أو فلاشة'));
        await acknowledgeNotice(
          tester,
          message: 'الملف موجود بالفعل؛ اختر اسمًا جديدًا للحفاظ عليه.',
        );
        expect(await File(destination!).readAsString(), 'protected bytes');
        destination = '${directory.path}/saved.json';
        await tester.tap(find.text('حفظ نسخة على الجهاز أو فلاشة'));
        await waitFor(
          tester,
          () => find
              .text('آخر نسخة يدوية: ${destination!}')
              .evaluate()
              .isNotEmpty,
        );
        final envelope =
            jsonDecode(await File(destination!).readAsString()) as Map;
        expect(envelope['format'], 'massar-center-backup');
        expect(((envelope['data'] as Map)['students'] as List).single, before);
        expect(find.text('آخر نسخة يدوية: ${destination!}'), findsOneWidget);
        expect(store.students.single.toJson(), before);
        expect(store.audit.length, audit);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'restore chooser and confirmation cancellations preserve live data; accepted restore preserves a recovery copy and requires login',
    (tester) async {
      await tester.runAsync(() async {
        source = await store.createBackup(
          destination: '${directory.path}/source.json',
        );
        await store.saveStudent(
          Student(
            name: 'طالب بعد النسخة',
            code: 'B2',
            groupIds: [store.groups.single.id],
            createdAt: DateTime.now(),
          ),
        );
        final liveStudents = store.students.map((s) => s.toJson()).toList();
        final audit = store.audit.length;
        final selected = source;
        await open(tester);
        source = null;
        await tester.tap(find.text('استعادة نسخة'));
        await waitFor(
          tester,
          () =>
              tester
                  .widget<OutlinedButton>(
                    find.widgetWithText(OutlinedButton, 'استعادة نسخة'),
                  )
                  .onPressed !=
              null,
        );
        expect(find.byType(AlertDialog), findsNothing);
        source = selected;
        await tester.tap(find.text('استعادة نسخة'));
        await waitFor(
          tester,
          () => find.text('استعادة البيانات').evaluate().isNotEmpty,
        );
        await tester.tap(find.text('رجوع'));
        await tester.pumpAndSettle();
        expect(store.students.map((s) => s.toJson()).toList(), liveStudents);
        expect(store.audit.length, audit);
        await tester.tap(find.text('استعادة نسخة'));
        await waitFor(
          tester,
          () => find.text('استعادة البيانات').evaluate().isNotEmpty,
        );
        await tester.tap(find.text('استعادة البيانات'));
        await waitFor(tester, () => store.currentUser == null);
        expect(store.students.single.code, 'B1');
        final recovery = Directory('${directory.path}/data/backups')
            .listSync()
            .whereType<File>()
            .where((f) => f.path.contains('before-restore-'))
            .single;
        final state =
            (jsonDecode(await recovery.readAsString()) as Map)['data'] as Map;
        expect(state['students'], liveStudents);
        await store.signIn('مدير', 'backup-workflow-password');
        expect(store.canManage, isTrue);
        expect(store.audit.last.action, 'backup_restore');
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'malformed restore displays an error and keeps the signed-in live state available',
    (tester) async {
      await tester.runAsync(() async {
        source = '${directory.path}/malformed.json';
        await File(source!).writeAsString('not a backup');
        final studentBefore = store.students.single.toJson();
        final userBefore = store.currentUser!.id;
        await open(tester);
        await tester.tap(find.text('استعادة نسخة'));
        await waitFor(
          tester,
          () => find.text('استعادة البيانات').evaluate().isNotEmpty,
        );
        await tester.tap(find.text('استعادة البيانات'));
        await acknowledgeNotice(
          tester,
          message: 'النسخة غير صالحة؛ لم تتغير البيانات.',
        );
        expect(store.students.single.toJson(), studentBefore);
        expect(store.currentUser!.id, userBefore);
        expect(store.audit.where((a) => a.action == 'backup_restore'), isEmpty);
        expect(
          tester
              .widget<OutlinedButton>(
                find.widgetWithText(OutlinedButton, 'استعادة نسخة'),
              )
              .onPressed,
          isNotNull,
        );
        expect(tester.takeException(), isNull);
      });
    },
  );
}
