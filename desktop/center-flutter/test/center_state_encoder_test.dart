import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/data/center_state_encoder.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  final deltaTime = DateTime.utc(2026, 10, 8);
  CenterState deltaFixture() => CenterState()
    ..students = List.generate(
      4,
      (index) => Student(
        id: 'student-$index',
        name: 'طالب $index',
        code: '$index',
        groupIds: const [],
        notes: 'ملاحظات "محفوظة" 📘 ' * 30,
        createdAt: deltaTime,
      ),
    )
    ..audit = [
      AuditRecord(
        id: 'audit-original',
        action: 'test',
        description: 'سجل محفوظ ' * 30,
        staffId: 'staff',
        createdAt: deltaTime,
      ),
    ]
    ..credentials = {
      'staff': {'hash': 'private-credential-marker'},
    };

  Map<String, dynamic> decodePublic(EncodedPublicCenterState encoded) =>
      jsonDecode(encoded.json) as Map<String, dynamic>;

  CenterState readFull(Map<String, dynamic> snapshot) =>
      CenterState.fromJson({...snapshot, 'credentials': <String, dynamic>{}});

  test(
    'deferred snapshots stay frozen after mutation and history eviction',
    () {
      final encoder = CenterStateEncoder();
      final original = deltaFixture()
        ..appliedDataRepairs = ['first-repair']
        ..groups = [
          StudyGroup(
            id: 'group',
            name: 'مجموعة',
            subjectId: 'subject',
            centerId: 'center',
            gradeId: 'grade',
            monthPlans: [],
          ),
        ];
      final expected =
          jsonDecode(jsonEncode(original.toJson())) as Map<String, dynamic>;
      final storage = encoder.encodeStorageFields(original);
      final public = encoder.encodePublicChanges(
        original,
        'original',
      )['state']!;
      original.students.clear();
      original.appliedDataRepairs.add('later-repair');
      original.credentials['staff']!['hash'] = 'later-private-marker';
      original.groups.single.monthPlans.add(
        const GroupMonthPlan(
          id: 'month',
          name: 'شهر',
          sessions: 4,
          price: 21000,
        ),
      );
      for (var version = 0; version < 6; version++) {
        encoder.encodePublicChanges(original, 'later-$version');
      }
      expect(
        storage.map((key, json) => MapEntry(key, jsonDecode(json))),
        expected,
      );
      expect(() => storage['students'] = '[]', throwsUnsupportedError);
      expect(decodePublic(public), {...expected}..remove('credentials'));
      expect(public.jsonLength, public.json.length);
      expect(public.json, isNot(contains('private-marker')));
    },
  );

  for (final change in ['append', 'replace', 'remove', 'reorder', 'clear']) {
    test('versioned $change delta reconstructs the exact public snapshot', () {
      final encoder = CenterStateEncoder();
      final original = deltaFixture();
      final full = encoder.encodePublicChanges(original, 'base')['state']!;
      final client = readFull(decodePublic(full));
      final changed = original.copyForMutation();
      switch (change) {
        case 'append':
          changed.students.add(
            original.students.first.copyWith(id: 'appended', code: '9'),
          );
        case 'replace':
          changed.students[1] = changed.students[1].copyWith(
            notes: 'تغيير "واحد"\n\\',
          );
        case 'remove':
          changed.students.removeAt(1);
        case 'reorder':
          changed.students = [
            original.students[1],
            original.students[0],
            ...original.students.skip(2),
          ];
        case 'clear':
          changed.students.clear();
      }
      final response = encoder.encodePublicChanges(
        changed,
        'next',
        baseVersion: 'base',
      );
      expect(response.keys, ['stateDelta']);
      final delta = decodePublic(response['stateDelta']!);
      final applied = client.applyPublicDelta(delta, baseVersion: 'base');
      expect(
        applied.toJson()..remove('credentials'),
        changed.toJson()..remove('credentials'),
      );
      expect(client.toJson()..remove('credentials'), decodePublic(full));
      expect(
        response['stateDelta']!.json,
        isNot(contains('private-credential-marker')),
      );
      if (change == 'clear') {
        expect(delta['set']['students'], isEmpty);
      } else {
        final splice = delta['splices']['students'] as Map;
        expect(splice['previousLength'], 4);
        expect(
          (splice['items'] as List).length,
          change == 'reorder'
              ? 2
              : change == 'remove'
              ? 0
              : 1,
        );
      }
    });
  }

  test(
    'mutable groups and new or removed optional fields freeze without credentials',
    () {
      final encoder = CenterStateEncoder();
      final original = deltaFixture()
        ..installationSeedId = 'seed'
        ..appliedDataRepairs = ['old-repair']
        ..groups = [
          StudyGroup(
            id: 'group',
            name: 'مجموعة',
            subjectId: 'subject',
            centerId: 'center',
            gradeId: 'grade',
            monthPlans: [],
          ),
        ];
      final full = encoder.encodePublicChanges(original, 'base')['state']!;
      final frozenJson = full.json;
      final client = readFull(decodePublic(full));
      final changed = original.copyForMutation()
        ..installationSeedId = null
        ..appliedDataRepairs.clear()
        ..centerFees.add(
          CenterFeeRecord(
            id: 'fee',
            studentId: 'student-0',
            sessionId: 'lesson',
            amount: 1500,
            paidAmount: 500,
            recordedAt: deltaTime,
          ),
        );
      changed.groups.single.monthPlans.add(
        const GroupMonthPlan(
          id: 'month',
          name: 'شهر جديد',
          sessions: 4,
          price: 21000,
        ),
      );
      changed.credentials['staff']!['hash'] = 'second-private-marker';
      final response = encoder.encodePublicChanges(
        changed,
        'next',
        baseVersion: 'base',
      );
      final delta = decodePublic(response['stateDelta']!);
      expect(delta['set'], contains('groups'));
      expect(delta['set'], contains('centerFees'));
      expect(delta['set']['schemaVersion'], changed.toJson()['schemaVersion']);
      expect(
        delta['remove'],
        containsAll(['installationSeedId', 'appliedDataRepairs']),
      );
      expect(delta['set'], isNot(contains('credentials')));
      expect(
        response['stateDelta']!.json,
        isNot(contains('second-private-marker')),
      );
      expect(full.json, frozenJson);
      expect(
        client.applyPublicDelta(delta, baseVersion: 'base').toJson()
          ..remove('credentials'),
        changed.toJson()..remove('credentials'),
      );
    },
  );

  test('unknown, evicted and reset versions receive complete snapshots', () {
    final encoder = CenterStateEncoder();
    final original = deltaFixture();
    encoder.encodePublicChanges(original, 'old');
    var current = original;
    for (var version = 1; version <= 16; version++) {
      current = current.copyForMutation();
      current.students[0] = current.students[0].copyWith(
        notes: 'revision $version',
      );
      encoder.encodePublicChanges(current, 'v$version');
    }
    for (final base in ['old', 'unknown']) {
      final response = encoder.encodePublicChanges(
        current,
        'v16',
        baseVersion: base,
      );
      expect(response.keys, ['state']);
      expect(
        decodePublic(response['state']!),
        current.toJson()..remove('credentials'),
      );
    }
    encoder.clearPublicHistory();
    expect(
      encoder
          .encodePublicChanges(original, 'restored', baseVersion: 'v16')
          .keys,
      ['state'],
    );
    expect(encoder.encodePublicChanges(original, 'restored').keys, ['state']);
  });

  test(
    'oversized history evicts old text after resolving the current request base',
    () {
      final encoder = CenterStateEncoder();
      final original = deltaFixture();
      original.students[0] = original.students[0].copyWith(
        notes: 'x' * (9 * 1024 * 1024),
      );
      encoder.encodePublicChanges(original, 'large-base');
      final changed = original.copyForMutation();
      changed.students[0] = changed.students[0].copyWith(
        notes: '${original.students[0].notes}y',
      );
      final response = encoder.encodePublicChanges(
        changed,
        'large-next',
        baseVersion: 'large-base',
      );
      expect(response.keys, ['stateDelta']);
      final decoded = decodePublic(response.values.single);
      expect(decoded, isNot(contains('credentials')));
      expect(
        encoder
            .encodePublicChanges(
              changed,
              'large-next',
              baseVersion: 'large-base',
            )
            .keys,
        ['state'],
      );
    },
  );

  test(
    'snapshot encoding preserves replacements, order, rollback and JSON escaping',
    () {
      final encoder = CenterStateEncoder();
      final now = DateTime.utc(2026, 10, 8);
      final original = CenterState()
        ..audit = [
          AuditRecord(
            id: 'one',
            action: 'test',
            description: 'طالب "أحمد"\n\\ 📘',
            staffId: 'staff',
            createdAt: now,
          ),
        ];
      void check(CenterState snapshot) {
        expect(encoder.encode(snapshot), jsonEncode(snapshot.toJson()));
        expect(
          encoder.encodePublicSnapshot(snapshot).json,
          jsonEncode(snapshot.toJson()..remove('credentials')),
        );
      }

      check(original);
      final next = original.copyForMutation()
        ..audit.add(
          AuditRecord(
            id: 'two',
            action: 'test',
            description: 'ثانٍ',
            staffId: 'staff',
            createdAt: now,
          ),
        );
      check(next);
      next.audit[0] = AuditRecord(
        id: 'one',
        action: 'changed',
        description: 'بديل',
        staffId: 'staff',
        createdAt: now,
      );
      check(next);
      next.audit = next.audit.reversed.toList();
      check(next);
      next.audit.removeLast();
      check(next);
      check(original);
      check(CenterState.fromJson(jsonDecode(encoder.encode(original))));
      next.audit.clear();
      check(next);
    },
  );

  test(
    'student replacements and mutable group plans and credentials reach the snapshot',
    () {
      final encoder = CenterStateEncoder();
      final snapshot = CenterState()
        ..students = [
          Student(
            id: 'student',
            name: 'طالب',
            code: '00001',
            groupIds: ['first'],
            createdAt: DateTime.utc(2026, 10, 8),
          ),
        ]
        ..groups = [
          StudyGroup(
            id: 'first',
            name: 'مجموعة',
            subjectId: 'subject',
            centerId: 'center',
            gradeId: 'grade',
            monthPlans: [],
          ),
        ]
        ..credentials = {
          'staff': {'hash': 'first'},
        };
      encoder.encode(snapshot);
      final capturedPublic = encoder.encodePublicSnapshot(snapshot);
      final originalPublic = jsonEncode(
        snapshot.toJson()..remove('credentials'),
      );
      snapshot.students[0] = snapshot.students.single.copyWith(
        groupIds: ['first', 'second'],
      );
      snapshot.groups.single.monthPlans.add(
        const GroupMonthPlan(
          id: 'month',
          name: 'شهر',
          sessions: 4,
          price: 21000,
        ),
      );
      snapshot.credentials['staff']!['hash'] = 'replacement';
      snapshot.appliedDataRepairs.add('repair-one');
      final encoded = encoder.encode(snapshot);
      expect(encoded, jsonEncode(snapshot.toJson()));
      final restored = CenterState.fromJson(jsonDecode(encoded));
      expect(restored.students.single.groupIds, ['first', 'second']);
      expect(restored.credentials['staff']!['hash'], 'replacement');
      expect(restored.appliedDataRepairs, ['repair-one']);
      expect(restored.groups.single.monthPlans.single.name, 'شهر');
      expect(capturedPublic.json, originalPublic);
      final nextPublic = encoder.encodePublicSnapshot(snapshot).json;
      expect(nextPublic, jsonEncode(snapshot.toJson()..remove('credentials')));
      expect(jsonDecode(nextPublic), isNot(contains('credentials')));
      expect(encoder.encode(snapshot), contains('replacement'));
    },
  );
}
