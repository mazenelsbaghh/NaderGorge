import 'dart:convert';
import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  setUp(() async {
    directory = await Directory.systemTemp.createTemp(
      'massar-center-persistence-',
    );
    store = await CenterStore.open(directory: directory.path);
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });
  test(
    'first startup is empty, credentials are hashed, reopening persists and requires login',
    () async {
      expect(store.hasStaff, false);
      expect(store.students, isEmpty);
      expect(store.currentUser, isNull);
      await store.setupAdmin('نادر', 'private-pass-123');
      await store.saveCatalog(
        const CatalogEntry(name: 'فيزياء', kind: CatalogKind.subject),
      );
      final backup = await store.createBackup();
      final content = await File(backup).readAsString();
      expect(content, isNot(contains('private-pass-123')));
      expect(content, contains('pbkdf2-sha256-120000'));
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      expect(store.currentUser, isNull);
      expect(store.catalogs.single.name, 'فيزياء');
      await expectLater(
        store.signIn('نادر', 'wrong-password'),
        throwsA(isA<CenterException>()),
      );
      await store.signIn('نادر', 'private-pass-123');
      expect(store.canManage, true);
    },
  );
  test(
    'restore validates before changing state, keeps previous backup and signs out',
    () async {
      await store.setupAdmin('نادر', 'private-pass-123');
      await store.saveCatalog(
        const CatalogEntry(name: 'فيزياء', kind: CatalogKind.subject),
      );
      final backup = await store.createBackup();
      await store.saveCatalog(
        const CatalogEntry(name: 'سنتر', kind: CatalogKind.center),
      );
      final invalid = File('${directory.path}/invalid.json');
      final json =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      (json['data'] as Map)['schemaVersion'] = 999;
      await invalid.writeAsString(jsonEncode(json));
      await expectLater(
        store.restoreBackup(invalid.path),
        throwsA(isA<CenterException>()),
      );
      expect(store.catalogs, hasLength(2));
      expect(store.currentUser, isNotNull);
      await store.restoreBackup(backup);
      expect(store.catalogs, hasLength(1));
      expect(store.currentUser, isNull);
      final files = await Directory(
        '${directory.path}/backups',
      ).list().toList();
      expect(files.any((e) => e.path.contains('before-restore')), true);
      await store.signIn('نادر', 'private-pass-123');
      expect(store.audit.last.action, 'backup_restore');
    },
  );
  test(
    'automatic reopen backup is retained and invalid relations cannot commit',
    () async {
      await store.setupAdmin('نادر', 'private-pass-123');
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('نادر', 'private-pass-123');
      await expectLater(
        store.saveGroup(
          const StudyGroup(
            name: 'غير صالح',
            subjectId: 'missing',
            centerId: 'missing',
            gradeId: 'missing',
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.groups, isEmpty);
      final files = await Directory(
        store.automaticBackupDirectory!,
      ).list().toList();
      expect(files.any((e) => e.path.contains('auto-')), true);
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      expect(store.groups, isEmpty);
    },
  );
  test(
    'exports cannot overwrite database or an existing backup and CSV is atomic',
    () async {
      await store.setupAdmin('نادر', 'private-pass-123');
      await expectLater(
        store.exportReport(store.databasePath),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.createBackup(destination: '${store.databasePath}-wal'),
        throwsA(isA<CenterException>()),
      );
      final backup = await store.createBackup();
      await expectLater(
        store.exportReport(backup),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.createBackup(destination: backup),
        throwsA(isA<CenterException>()),
      );
      final report = await store.exportReport('${directory.path}/payments.csv');
      expect((await File(report).readAsBytes()).take(3).toList(), [
        0xEF,
        0xBB,
        0xBF,
      ]);
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('نادر', 'private-pass-123');
      expect(store.canManage, true);
    },
  );
  test(
    'second instance cannot overwrite stale snapshot of same database',
    () async {
      await store.setupAdmin('نادر', 'private-pass-123');
      await expectLater(
        CenterStore.open(directory: directory.path),
        throwsA(isA<CenterException>()),
      );
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('نادر', 'private-pass-123');
      expect(store.canManage, true);
    },
  );
  test(
    'forged backup references and password format cannot replace valid state',
    () async {
      await store.setupAdmin('نادر', 'private-pass-123');
      final backup = await store.createBackup();
      final original =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final data = original['data'] as Map;
      data['credentials'] = {};
      final forged = File('${directory.path}/forged.json');
      await forged.writeAsString(jsonEncode(original));
      await expectLater(
        store.restoreBackup(forged.path),
        throwsA(isA<CenterException>()),
      );
      expect(store.currentUser, isNotNull);
      expect(store.staff.single.name, 'نادر');
      store.signOut();
      await store.signIn('نادر', 'private-pass-123');
      expect(store.canManage, true);
    },
  );
  test(
    'failed pre-update backup blocks opening without losing saved data',
    () async {
      await store.setupAdmin('نادر', 'private-pass-123');
      await store.saveCatalog(
        const CatalogEntry(name: 'فيزياء', kind: CatalogKind.subject),
      );
      final audited = store.audit.map((entry) => entry.toJson()).toList();
      final backupFolder = Directory(store.automaticBackupDirectory!);
      await store.close();
      await File('${directory.path}/last-opened-build.json').delete();
      if (await backupFolder.exists()) {
        await backupFolder.delete(recursive: true);
      }
      final obstruction = File(backupFolder.path);
      await obstruction.writeAsString('not a directory');
      await expectLater(
        CenterStore.open(directory: directory.path),
        throwsA(
          isA<CenterException>().having(
            (error) => error.cause,
            'filesystem cause',
            isA<FileSystemException>(),
          ),
        ),
      );
      await obstruction.delete();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('نادر', 'private-pass-123');
      expect(store.catalogs.single.name, 'فيزياء');
      expect(store.audit.map((entry) => entry.toJson()), audited);
      expect(
        await backupFolder
            .list()
            .where((entry) => entry.path.contains('auto-'))
            .length,
        1,
      );
    },
  );
  test(
    'failed pre-restore preservation leaves database and logged-in state intact',
    () async {
      await store.setupAdmin('نادر', 'private-pass-123');
      final backup = await store.createBackup(
        destination: '${directory.path}/saved.json',
      );
      final folder = Directory('${directory.path}/backups');
      if (await folder.exists()) await folder.delete(recursive: true);
      final obstruction = File(folder.path);
      await obstruction.writeAsString('not a directory');
      await expectLater(
        store.restoreBackup(backup),
        throwsA(
          isA<CenterException>().having(
            (error) => error.cause,
            'filesystem cause',
            isA<FileSystemException>(),
          ),
        ),
      );
      expect(store.currentUser?.name, 'نادر');
      expect(store.audit, hasLength(1));
      await obstruction.delete();
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('نادر', 'private-pass-123');
      expect(store.canManage, true);
    },
  );
  test(
    'SQLite write failure rolls back change and restore without reporting success',
    () async {
      await store.setupAdmin('نادر', 'private-pass-123');
      final backup = await store.createBackup(
        destination: '${directory.path}/saved.json',
      );
      final database = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      await database.execute(
        "CREATE TRIGGER deny_write BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'simulated storage failure'); END",
      );
      await expectLater(
        store.saveCatalog(
          const CatalogEntry(name: 'فيزياء', kind: CatalogKind.subject),
        ),
        throwsA(
          isA<CenterException>().having(
            (error) => error.cause,
            'sqlite cause',
            isA<DatabaseException>(),
          ),
        ),
      );
      expect(store.catalogs, isEmpty);
      expect(store.audit, hasLength(1));
      await expectLater(
        store.restoreBackup(backup),
        throwsA(
          isA<CenterException>().having(
            (error) => error.cause,
            'sqlite cause',
            isA<DatabaseException>(),
          ),
        ),
      );
      expect(store.currentUser?.name, 'نادر');
      expect(store.audit, hasLength(1));
      await database.execute('DROP TRIGGER deny_write');
      await database.close();
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('نادر', 'private-pass-123');
      expect(store.catalogs, isEmpty);
    },
  );
}
