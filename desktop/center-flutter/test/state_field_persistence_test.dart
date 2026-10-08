import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late String studentId, ownerId;
  const password = 'synthetic-field-persistence';
  var exportNumber = 0;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-state-fields-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('owner', password);
    ownerId = store.currentUser!.id;
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'Synthetic group',
        subjectId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.subject)
            .id,
        centerId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.center)
            .id,
        gradeId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.grade)
            .id,
      ),
    );
    // Finish the existing month-template migration before exercising the
    // schema1 fixture, so its first later write is the operation under test.
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('owner', password);
    await store.saveStudent(
      Student(
        code: '00123',
        name: 'Synthetic student',
        groupIds: [store.groups.single.id],
        createdAt: DateTime.utc(2026, 10, 1),
      ),
    );
    studentId = store.students.single.id;
    await store.saveStudentNote(studentId: studentId, notes: 'Warm saved note');
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<Database> connect({bool readOnly = false}) =>
      databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false, readOnly: readOnly),
      );

  Future<Map<String, dynamic>> durableState() async {
    final database = await connect(readOnly: true);
    try {
      return jsonDecode(
            (await database.query('state')).single['payload'] as String,
          )
          as Map<String, dynamic>;
    } finally {
      await database.close();
    }
  }

  Future<Map<String, dynamic>> exportedState() async {
    final filename = await store.createBackup(
      destination: '${directory.path}/state-${exportNumber++}.json',
    );
    return Map<String, dynamic>.from(
      (jsonDecode(await File(filename).readAsString()) as Map)['data'] as Map,
    );
  }

  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('owner', password);
  }

  for (final restore in [false, true]) {
    test(
      'first edit after ${restore ? 'restore' : 'reopen'} leaves unrelated durable records untouched',
      () async {
        if (restore) {
          final backup = await store.createBackup();
          await store.restoreBackup(backup);
          await store.signIn('owner', password);
        } else {
          await reopen();
        }
        final database = await connect();
        try {
          await database.execute(
            "CREATE TRIGGER reject_catalog_rewrite BEFORE DELETE ON state_records WHEN OLD.section='catalogs' BEGIN SELECT RAISE(ABORT, 'unrelated catalog replacement'); END",
          );
          await store.saveStudentNote(
            studentId: studentId,
            notes: 'First edit',
          );
          expect((await durableState())['students'][0]['notes'], 'First edit');
          await database.execute('DROP TRIGGER reject_catalog_rewrite');
        } finally {
          await database.close();
        }
      },
    );
  }

  test(
    'receipt insert failure rolls back the partial state write and an identical retry survives restart',
    () async {
      const requestId = '10000000-0000-4000-8000-000000000001';
      const note = 'Same retry "note"\n\\ عربي 📘';
      final request = <String, dynamic>{
        'requestId': requestId,
        'operation': 'saveStudentNote',
        'arguments': {'studentId': studentId, 'notes': note},
      };
      Future<Map<String, dynamic>> send() => store.commandLan(
        deviceId: 'synthetic-storage-client',
        staffId: ownerId,
        request: request,
        authorize: () => true,
      );
      final before = await durableState();
      final previousAudits = store.audit.length;
      final database = await connect();
      try {
        // The trigger runs after _change's successful UPDATE, but before the
        // enclosing LAN transaction can commit either state or its receipt.
        await database.execute('''
        CREATE TRIGGER reject_receipt BEFORE INSERT ON lan_commands
        BEGIN
          SELECT CASE
            WHEN json_extract((SELECT payload FROM state WHERE id = 1), '\$.students[0].notes') != 'Warm saved note'
            THEN RAISE(ABORT, 'receipt blocked after state update')
            ELSE RAISE(ABORT, 'state update was not reached')
          END;
        END
      ''');
        await expectLater(
          send(),
          throwsA(
            isA<CenterException>().having(
              (error) => error.cause.toString(),
              'failure after state UPDATE',
              contains('receipt blocked after state update'),
            ),
          ),
        );
        expect(store.students.single.notes, 'Warm saved note');
        expect(store.audit.length, previousAudits);
        expect(await durableState(), before);
        expect(await database.query('lan_commands'), isEmpty);
        await database.execute('DROP TRIGGER reject_receipt');

        // Retrying the identical value catches a stale field baseline: that
        // baseline would incorrectly omit the student section from this write.
        expect((await send())['status'], 'committed');
        expect(store.students.single.notes, note);
        expect((await durableState())['students'], [
          store.students.single.toJson(),
        ]);
        await store.saveCatalog(
          const CatalogEntry(name: 'Other section', kind: CatalogKind.subject),
        );
        final committed = await exportedState();
        expect(await durableState(), committed);
        expect(store.audit.length, previousAudits + 2);
        final receipts = await database.query('lan_commands');
        expect(receipts, hasLength(1));
        expect(receipts.single['request_id'], requestId);
      } finally {
        await database.execute('DROP TRIGGER IF EXISTS reject_receipt');
        await database.close();
      }
      final committed = await durableState();
      await reopen();
      expect(store.students.single.notes, note);
      expect(await exportedState(), committed);
      expect((await send())['status'], 'committed');
      expect(store.audit.length, previousAudits + 2);
      expect(await durableState(), committed);
    },
  );

  test(
    'the first write after opening schema1 fills omitted finance fields before later partial writes',
    () async {
      final legacy = await durableState();
      legacy['schemaVersion'] = 1;
      for (final field in [
        'reviews',
        'closings',
        'paymentChecks',
        'corrections',
        'refunds',
        'cardPayments',
        'cardReceipts',
        'academicActivities',
      ]) {
        legacy.remove(field);
      }
      await store.close();
      final database = await connect();
      try {
        await database.update('state', {
          'payload': jsonEncode(legacy),
        }, where: 'id = 1');
      } finally {
        await database.close();
      }
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('owner', password);
      expect(
        (await durableState())['schemaVersion'],
        1,
        reason:
            'The fixture must reach the first user write without another migration rewriting it.',
      );

      await store.saveStudentNote(
        studentId: studentId,
        notes: 'Canonical first write',
      );
      final canonical = await durableState();
      expect(canonical['schemaVersion'], isNot(1));
      expect(canonical['reviews'], isEmpty);
      expect(canonical['closings'], isEmpty);
      expect(canonical, await exportedState());
      await store.saveStudentNote(
        studentId: studentId,
        notes: 'Later partial write',
      );
      final committed = await durableState();
      await reopen();
      expect(store.students.single.notes, 'Later partial write');
      expect(await exportedState(), committed);
    },
  );

  test(
    'restore invalidates cached fields and fresh credential patches remain durable and LAN-redacted',
    () async {
      final backup = await store.createBackup(
        destination: '${directory.path}/restore.json',
      );
      const targetNote = 'Repeated after restore';
      await store.saveStudentNote(studentId: studentId, notes: targetNote);
      await store.saveStaff(
        name: 'removed-worker',
        password: password,
        role: StaffRole.cashier,
      );
      final removedWorker = store.staff.firstWhere(
        (staff) => staff.name == 'removed-worker',
      );
      await store.snapshotLan(ownerId, stateEncoding: LanStateEncoding.json);
      await store.restoreBackup(backup);
      expect(store.currentUser, isNull);
      await store.signIn('owner', password);
      await store.saveStudentNote(studentId: studentId, notes: targetNote);
      await store.saveStaff(
        name: 'retained-worker',
        password: password,
        role: StaffRole.cashier,
      );
      final worker = store.staff.firstWhere(
        (staff) => staff.name == 'retained-worker',
      );
      final committed = await durableState();
      expect(committed, await exportedState());
      final credentials = committed['credentials'] as Map;
      expect(credentials.keys, unorderedEquals([ownerId, worker.id]));
      expect(credentials, isNot(contains(removedWorker.id)));
      expect((committed['students'] as List).single['notes'], targetNote);

      final support = await store.captureSupportSnapshot();
      expect(support['data'], committed);
      expect(support['lanCommandReceipts'], isEmpty);
      final public = (await store.snapshotLan(worker.id))['state'] as Map;
      expect(public, Map.of(committed)..remove('credentials'));
      final encodedPublic = await store.snapshotLan(
        worker.id,
        stateEncoding: LanStateEncoding.json,
      );
      final publicText = encodedPublic['state'].json as String;
      for (final credential in credentials.values) {
        expect(publicText, isNot(contains((credential as Map)['hash'])));
        expect(publicText, isNot(contains(credential['salt'])));
      }
      expect(jsonDecode(publicText), isNot(contains('credentials')));
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('retained-worker', password);
      expect(store.currentUser?.id, worker.id);
      expect(store.students.single.notes, targetNote);
      expect(await durableState(), committed);
    },
  );
}
