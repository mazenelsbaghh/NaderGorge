import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/copy_on_write_list.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  test(
    'both copy-on-write branches preserve rollback after edits and appends',
    () {
      final original = CopyOnWriteList<int>([1, 2]);
      final newer = original.fork();
      newer.add(3);
      original[0] = 4;
      expect(newer, [1, 2, 3]);
      expect(original, [4, 2]);
      final third = newer.fork();
      newer.removeWhere((number) => number.isEven);
      third.sort((a, b) => b.compareTo(a));
      expect(newer, [1, 3]);
      expect(third, [3, 2, 1]);
    },
  );

  for (final valid in [true, false]) {
    test(
      'legacy migration ${valid ? 'backs up and preserves records on reopen' : 'rolls back an invalid snapshot'}',
      () async {
        final directory = await Directory.systemTemp.createTemp(
          'massar-storage-migration-',
        );
        CenterStore? store;
        try {
          store = await CenterStore.open(directory: '${directory.path}/seed');
          await store.setupAdmin('synthetic-owner', 'synthetic-password');
          for (final kind in CatalogKind.values) {
            await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
          }
          String catalog(CatalogKind kind) =>
              store!.catalogs.firstWhere((entry) => entry.kind == kind).id;
          await store.saveGroup(
            StudyGroup(
              name: 'Synthetic group',
              subjectId: catalog(CatalogKind.subject),
              centerId: catalog(CatalogKind.center),
              gradeId: catalog(CatalogKind.grade),
            ),
          );

          await store.registerStudent(
            Student(
              name: 'Synthetic student',
              notes: 'Saved note',
              groupIds: [store.groups.single.id],
              createdAt: DateTime.utc(2026, 10, 8),
            ),
          );
          final backup =
              jsonDecode(await File(await store.createBackup()).readAsString())
                  as Map<String, dynamic>;
          final expected = backup['data'] as Map<String, dynamic>;
          await store.close();
          store = null;
          final legacy = await Directory('${directory.path}/legacy').create();
          if (!valid) expected['schemaVersion'] = 999;
          final database = await databaseFactoryFfi.openDatabase(
            '${legacy.path}/center.sqlite',
            options: OpenDatabaseOptions(
              version: 1,
              onCreate: (db, version) async {
                await db.execute(
                  'CREATE TABLE state(id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL)',
                );
                await db.insert('state', {
                  'id': 1,
                  'payload': jsonEncode(expected),
                });
              },
            ),
          );
          await database.close();
          if (!valid) {
            await expectLater(
              CenterStore.open(directory: legacy.path),
              throwsA(isA<CenterException>()),
            );
            final unchanged = await databaseFactoryFfi.openDatabase(
              '${legacy.path}/center.sqlite',
            );
            try {
              expect(await unchanged.getVersion(), 1);
              expect(
                jsonDecode(
                  (await unchanged.query('state')).single['payload'] as String,
                ),
                expected,
              );
            } finally {
              await unchanged.close();
            }
            return;
          }
          store = await CenterStore.open(directory: legacy.path);
          await store.signIn('synthetic-owner', 'synthetic-password');
          expect(store.students.single.notes, 'Saved note');
          final copies = await Directory('${legacy.path}/backups')
              .list()
              .where((entry) => entry.path.contains('pre-storage-v2'))
              .toList();
          expect(copies, hasLength(1));
          expect(
            jsonDecode(await File(copies.single.path).readAsString())['data'],
            expected,
          );
          await store.saveStudentNote(
            studentId: store.students.single.id,
            notes: 'New note',
          );
          await store.close();
          store = await CenterStore.open(directory: legacy.path);
          await store.signIn('synthetic-owner', 'synthetic-password');
          expect(store.students.single.notes, 'New note');
        } finally {
          await store?.close();
          await directory.delete(recursive: true);
        }
      },
    );
  }
}
