import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/data/center_state_encoder.dart';
import 'package:massar_center/data/center_state_storage.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  final timestamp = DateTime.utc(2026, 10, 8);
  late Database database;
  late CenterState original;
  late CenterStateEncoder encoder;
  late Map<String, String> previous;

  setUp(() async {
    sqfliteFfiInit();
    database = await databaseFactoryFfi.openDatabase(inMemoryDatabasePath);
    await database.execute(
      'CREATE TABLE state (id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL)',
    );
    original = CenterState()
      ..installationSeedId = 'original-seed'
      ..appliedDataRepairs = ['repair']
      ..students = [
        for (var i = 0; i < 3; i++)
          Student(
            id: 'student-$i',
            code: '00$i',
            name: 'طالب $i',
            notes: 'نص "قديم" \\ 📘',
            groupIds: const [],
            createdAt: timestamp,
          ),
      ]
      ..groups = [
        StudyGroup(
          id: 'group',
          name: 'مجموعة',
          subjectId: 'subject',
          centerId: 'center',
          gradeId: 'grade',
          monthPlans: [],
        ),
      ]
      ..credentials = {
        'staff': {'hash': 'private-old-hash'},
      };
    encoder = CenterStateEncoder();
    previous = encoder.encodeStorageFields(original);
    await database.insert('state', {
      'id': 1,
      'payload': jsonEncode(original.toJson()),
    });
  });
  tearDown(() async => database.close());

  for (final change in [
    'append',
    'append-few',
    'append-many',
    'append-and-edit',
    'optional-array',
    'replace',
    'reorder',
    'remove',
    'clear',
    'optional',
  ]) {
    test('section update preserves exact stored state after $change', () async {
      final next = original.copyForMutation();
      switch (change) {
        case 'append':
          next.students.add(
            next.students.first.copyWith(id: 'new-student', code: '009'),
          );
        case 'append-many':
          next.students.addAll([
            for (var index = 0; index < 20; index++)
              next.students.first.copyWith(
                id: 'added-$index',
                code: '10$index',
              ),
          ]);
        case 'append-few':
          next.students.addAll([
            for (var index = 0; index < 3; index++)
              next.students.first.copyWith(
                id: 'added-$index',
                code: '10$index',
              ),
          ]);
        case 'append-and-edit':
          next.students[0] = next.students[0].copyWith(notes: 'تعديل سابق');
          next.students.add(
            next.students.first.copyWith(id: 'new', code: '009'),
          );
        case 'optional-array':
          next.centerFees.add(
            CenterFeeRecord(
              id: 'fee',
              studentId: 'student-0',
              sessionId: 'session',
              amount: 1500,
              paidAmount: 500,
              recordedAt: timestamp,
            ),
          );
        case 'replace':
          next.students[1] = next.students[1].copyWith(
            notes: 'جديد\n"اقتباس" \\ 😀',
            discountPercent: 12.5,
          );
        case 'reorder':
          next.students = next.students.reversed.toList();
        case 'remove':
          next.students.removeAt(1);
        case 'clear':
          next.students.clear();
        case 'optional':
          next.installationSeedId = null;
          next.appliedDataRepairs.clear();
          next.defaultMonthPriceVersion = 1;
      }
      final count = await database.transaction(
        (tx) => updateStoredStateFields(
          tx,
          previous,
          encoder.encodeStorageFields(next),
          encoder.encodeStorageAppends(original, next),
        ),
      );
      expect(count, 1);
      final stored = jsonDecode(
        (await database.query('state')).single['payload'] as String,
      );
      expect(stored, next.toJson());
      expect(CenterState.fromJson(stored).toJson(), next.toJson());
    });
  }

  test(
    'frozen baseline keeps mutable groups and private credentials current',
    () async {
      final frozenGroup = previous['groups'];
      final next = original.copyForMutation();
      next.groups.single.monthPlans.add(
        const GroupMonthPlan(
          id: 'month',
          name: 'شهر',
          sessions: 4,
          price: 21000,
        ),
      );
      next.credentials['staff']!['hash'] = 'private-new-hash';
      next.enrollments['student-0:group'] = timestamp.toIso8601String();
      // An unrelated public encoding must never become the local write baseline.
      final publicSnapshot = encoder.encodePublicChanges(next, 'new')['state']!;
      expect(publicSnapshot.json, isNot(contains('private-new-hash')));
      expect(previous['groups'], frozenGroup);
      await database.transaction(
        (tx) => updateStoredStateFields(
          tx,
          previous,
          encoder.encodeStorageFields(next),
          encoder.encodeStorageAppends(original, next),
        ),
      );
      expect(
        jsonDecode((await database.query('state')).single['payload'] as String),
        next.toJson(),
      );
    },
  );

  test(
    'null remains JSON null while removed fields disappear and empty values remain',
    () async {
      final before = {
        'nullable': '1',
        'removed': 'true',
        'array': '[1]',
        'map': '{"a":1}',
      };
      final after = {
        'nullable': 'null',
        'array': '[]',
        'map': '{}',
        'flag': 'false',
      };
      await database.update('state', {
        'payload': jsonEncode(
          before.map((key, encoded) => MapEntry(key, jsonDecode(encoded))),
        ),
      });
      expect(
        await updateStoredStateFields(database, before, after, const {}),
        1,
      );
      expect(
        jsonDecode((await database.query('state')).single['payload'] as String),
        {'nullable': null, 'array': [], 'map': {}, 'flag': false},
      );
      expect(
        await updateStoredStateFields(database, after, after, const {}),
        1,
      );
      await database.delete('state');
      expect(
        await updateStoredStateFields(database, after, after, const {}),
        0,
      );
    },
  );

  test(
    'growing sections preserve every row when append paths reach the SQL budget',
    () async {
      const sections = ['students', 'attendances', 'payments', 'audit'];
      final before = {
        for (final section in sections)
          section: jsonEncode([
            {'original': section},
          ]),
      };
      final appends = {
        for (final section in sections)
          section: [
            for (var index = 0; index < 16; index++)
              jsonEncode({'section': section, 'position': index}),
          ],
      };
      final expected = {
        for (final section in sections)
          section: [
            {'original': section},
            ...appends[section]!.map(jsonDecode),
          ],
      };
      final after = expected.map(
        (key, rows) => MapEntry(key, jsonEncode(rows)),
      );
      await database.update('state', {
        'payload': jsonEncode(
          before.map((key, encoded) => MapEntry(key, jsonDecode(encoded))),
        ),
      });
      expect(
        await updateStoredStateFields(database, before, after, appends),
        1,
      );
      expect(
        jsonDecode((await database.query('state')).single['payload'] as String),
        expected,
      );
    },
  );
}
