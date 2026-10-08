import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late List<String> ids;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-group-sessions-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('manager', 'group-sessions-password');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    for (final name in ['أ', 'ب']) {
      await store.saveGroup(
        StudyGroup(
          name: name,
          subjectId: store.catalogs[0].id,
          centerId: store.catalogs[1].id,
          gradeId: store.catalogs[2].id,
        ),
      );
    }
    ids = store.groups.map((group) => group.id).toList();
    for (var index = 0; index < ids.length; index++) {
      await store.saveSession(
        LessonSession(
          groupId: ids[index],
          number: [10, 3][index],
          startsAt: DateTime.now().subtract(const Duration(days: 1)),
          createdAt: DateTime.now(),
        ),
      );
    }
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('manager', 'group-sessions-password');
  }

  Map<String, dynamic> legacySessionFields(LessonSession session) =>
      session.toJson()..remove('preparedLessonId');
  void expectPreparedLinks() {
    for (final session in store.sessions) {
      expect(session.preparedLessonId, isNotNull);
      final month = store.studyMonthForLesson(session.preparedLessonId!);
      expect(month, isNotNull);
      expect(month!.number, session.monthNumber);
      expect(
        month.lessons
            .singleWhere((lesson) => lesson.id == session.preparedLessonId)
            .number,
        session.number,
      );
    }
  }

  for (final kind in SessionKind.values) {
    test(
      '$kind batch uses per-group numbering and one durable timestamp without collecting money',
      () async {
        final previousIds = store.sessions.map((session) => session.id).toSet();
        final auditCount = store.audit.length;
        await store.createGroupSessions(
          groupIds: ids,
          kind: kind,
          extraPrice: 4550,
        );
        final created = store.sessions
            .where((session) => !previousIds.contains(session.id))
            .toList();
        expect(created.map((session) => session.number), [11, 4]);
        expect(created.map((session) => session.groupId), ids);
        expect(created.map((session) => session.kind), everyElement(kind));
        expect(
          created.map((session) => session.extraPrice),
          everyElement(kind == SessionKind.extra ? 4550 : 0),
        );
        expect(
          created.map((session) => session.startsAt).toSet(),
          hasLength(1),
        );
        expect(
          created.every((session) => session.createdAt == session.startsAt),
          isTrue,
        );
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.audit, hasLength(auditCount + 1));
        expect(store.audit.last.action, 'sessions_create');
        final saved = created.map((session) => session.toJson()).toList();
        await reopen();
        expectPreparedLinks();
        expect(
          store.sessions
              .where((session) => !previousIds.contains(session.id))
              .map(legacySessionFields),
          saved,
        );
      },
    );
  }

  for (final variant in ['empty', 'duplicate', 'missing']) {
    test('$variant selection leaves every group and audit unchanged', () async {
      final selected = switch (variant) {
        'empty' => <String>[],
        'duplicate' => [ids.first, ids.first],
        _ => [ids.first, 'missing-group'],
      };
      final before = store.sessions.map((session) => session.toJson()).toList();
      final auditCount = store.audit.length;
      await expectLater(
        store.createGroupSessions(
          groupIds: selected,
          kind: SessionKind.counted,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.sessions.map((session) => session.toJson()), before);
      expect(store.audit, hasLength(auditCount));
      await reopen();
      expectPreparedLinks();
      expect(store.sessions.map(legacySessionFields), before);
    });
  }

  for (final role in [StaffRole.cashier, StaffRole.assistant]) {
    test('$role cannot create a batch or leave partial sessions', () async {
      await store.saveStaff(
        name: role.name,
        password: 'staff-test-password',
        role: role,
      );
      await store.signIn(role.name, 'staff-test-password');
      final auditCount = store.audit.length;
      await expectLater(
        store.createGroupSessions(groupIds: ids, kind: SessionKind.free),
        throwsA(isA<CenterException>()),
      );
      expect(store.sessions, hasLength(2));
      expect(store.audit, hasLength(auditCount));
    });
  }

  test(
    'concurrent batches allocate distinct next numbers for each group',
    () async {
      await Future.wait([
        store.createGroupSessions(groupIds: ids, kind: SessionKind.counted),
        store.createGroupSessions(groupIds: ids, kind: SessionKind.counted),
      ]);
      expect(
        store.sessions
            .where((session) => session.groupId == ids.first)
            .map((session) => session.number),
        [10, 11, 12],
      );
      expect(
        store.sessions
            .where((session) => session.groupId == ids.last)
            .map((session) => session.number),
        [3, 4, 5],
      );
      await reopen();
      expect(store.sessions, hasLength(6));
    },
  );

  test(
    'later group with a closed future counted class rolls the whole batch back',
    () async {
      await store.closeSession(
        store.sessions.singleWhere((session) => session.groupId == ids.last).id,
      );
      await store.saveSession(
        LessonSession(
          groupId: ids.last,
          number: 4,
          startsAt: DateTime.now().add(const Duration(days: 1)),
          createdAt: DateTime.now(),
        ),
      );
      await store.closeSession(store.sessions.last.id);
      final auditCount = store.audit.length;
      await expectLater(
        store.createGroupSessions(groupIds: ids, kind: SessionKind.counted),
        throwsA(isA<CenterException>()),
      );
      expect(store.sessions, hasLength(3));
      expect(store.nextSessionNumber(ids.first), 11);
      expect(store.audit, hasLength(auditCount));
      await reopen();
      expect(store.sessions, hasLength(3));
    },
  );

  test(
    'SQLite write failure rolls back all sessions and numbers before a successful retry',
    () async {
      final database = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      final auditCount = store.audit.length;
      try {
        await database.execute(
          "CREATE TRIGGER reject_batch BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'test failure'); END",
        );
        await expectLater(
          store.createGroupSessions(groupIds: ids, kind: SessionKind.counted),
          throwsA(isA<CenterException>()),
        );
        expect(store.sessions, hasLength(2));
        expect(store.audit, hasLength(auditCount));
      } finally {
        await database.execute('DROP TRIGGER IF EXISTS reject_batch');
        await database.close();
      }
      await store.createGroupSessions(groupIds: ids, kind: SessionKind.counted);
      await reopen();
      expect(store.sessions.map((session) => session.number), [10, 3, 11, 4]);
    },
  );
}
