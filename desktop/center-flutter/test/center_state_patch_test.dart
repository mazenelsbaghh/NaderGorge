import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/domain/models.dart';

const _baseVersion = '12345678-1234-4234-8234-123456789abc';

Map<String, dynamic> _delta() => {
  'baseVersion': _baseVersion,
  'set': <String, dynamic>{'schemaVersion': 11},
  'splices': <String, dynamic>{},
  'remove': <String>[],
};

Student _student(String code) => Student(
  id: 'student-$code',
  code: code,
  name: 'طالب $code',
  createdAt: DateTime.utc(2026),
);

void main() {
  test(
    'a changed student and appended audit preserve the other typed records',
    () {
      final original = CenterState()
        ..students = [_student('0001'), _student('0002')];
      final before = jsonEncode(original.toJson());
      final edited = original.students.last.copyWith(notes: 'سطر "جديد"\n📘');
      final audit = AuditRecord(
        id: 'audit-1',
        action: 'student_note',
        description: 'Synthetic note change',
        staffId: 'staff-1',
        createdAt: DateTime.utc(2026),
      );
      final patch = _delta();
      patch['splices'] = {
        'students': {
          'start': 1,
          'deleteCount': 1,
          'previousLength': 2,
          'items': [edited.toJson()],
        },
        'audit': {
          'start': 0,
          'deleteCount': 0,
          'previousLength': 0,
          'items': [audit.toJson()],
        },
      };
      final next = original.applyPublicDelta(patch, baseVersion: _baseVersion);
      expect(next.students.last.toJson(), edited.toJson());
      expect(next.audit.single.toJson(), audit.toJson());
      expect(identical(next.students.first, original.students.first), isTrue);
      expect(identical(next.payments, original.payments), isTrue);
      expect(jsonEncode(original.toJson()), before);
      expect(next.credentials, isEmpty);
    },
  );

  for (final variant
      in <
        ({
          String name,
          int start,
          int deleted,
          List<String> inserted,
          List<String> expected,
        })
      >[
        (
          name: 'prepend',
          start: 0,
          deleted: 0,
          inserted: ['4'],
          expected: ['4', '1', '2', '3'],
        ),
        (
          name: 'append',
          start: 3,
          deleted: 0,
          inserted: ['4'],
          expected: ['1', '2', '3', '4'],
        ),
        (
          name: 'middle deletion',
          start: 1,
          deleted: 1,
          inserted: [],
          expected: ['1', '3'],
        ),
        (
          name: 'replacement',
          start: 1,
          deleted: 2,
          inserted: ['4'],
          expected: ['1', '4'],
        ),
        (name: 'clear', start: 0, deleted: 3, inserted: [], expected: []),
      ]) {
    test('list splice ${variant.name} preserves exact row order', () {
      final original = CenterState()
        ..students = ['1', '2', '3'].map(_student).toList();
      final patch = _delta();
      patch['splices'] = {
        'students': {
          'start': variant.start,
          'deleteCount': variant.deleted,
          'previousLength': 3,
          'items': variant.inserted
              .map((code) => _student(code).toJson())
              .toList(),
        },
      };
      final next = original.applyPublicDelta(patch, baseVersion: _baseVersion);
      expect(next.students.map((student) => student.code), variant.expected);
      expect(original.students.map((student) => student.code), ['1', '2', '3']);
    });
  }

  test(
    'whole optional list replacements and removal reset the prior values',
    () {
      final original = CenterState()
        ..installationSeedId = 'seed'
        ..appliedDataRepairs = ['repair']
        ..defaultMonthPriceVersion = 1;
      final fee = CenterFeeRecord(
        id: 'fee',
        studentId: 'student',
        sessionId: 'session',
        amount: 1500,
        paidAmount: 1500,
        recordedAt: DateTime.utc(2026),
      );
      final introduced = _delta();
      (introduced['set'] as Map)['centerFees'] = [fee.toJson()];
      final withFee = original.applyPublicDelta(
        introduced,
        baseVersion: _baseVersion,
      );
      expect(withFee.centerFees.single.toJson(), fee.toJson());
      final removed = _delta();
      removed['remove'] = [
        'installationSeedId',
        'appliedDataRepairs',
        'defaultMonthPriceVersion',
        'studyMonths',
        'centerFees',
        'debtSettlements',
      ];
      final cleared = withFee.applyPublicDelta(
        removed,
        baseVersion: _baseVersion,
      );
      expect(cleared.installationSeedId, isNull);
      expect(cleared.appliedDataRepairs, isEmpty);
      expect(cleared.defaultMonthPriceVersion, 0);
      expect(cleared.centerFees, isEmpty);
      expect(withFee.centerFees.single.id, 'fee');
    },
  );

  final invalidPatches = <String, void Function(Map<String, dynamic>)>{
    'wrong base': (patch) => patch['baseVersion'] = 'different-generation',
    'unknown section': (patch) => (patch['set'] as Map)['unrecognized'] = [],
    'credentials': (patch) => (patch['set'] as Map)['credentials'] = {},
    'required field removed': (patch) => patch['remove'] = ['students'],
    'duplicate removal': (patch) =>
        patch['remove'] = ['centerFees', 'centerFees'],
    'overlapping fields': (patch) => (patch['set'] as Map)['students'] = [],
    'missing schema': (patch) => (patch['set'] as Map).remove('schemaVersion'),
    'unsupported schema': (patch) =>
        (patch['set'] as Map)['schemaVersion'] = 999,
    'wrong length': (patch) =>
        patch['splices']['students']['previousLength'] = 2,
    'noninteger length': (patch) =>
        patch['splices']['students']['previousLength'] = 1.0,
    'negative start': (patch) => patch['splices']['students']['start'] = -1,
    'past end': (patch) => patch['splices']['students']['start'] = 2,
    'noninteger start': (patch) => patch['splices']['students']['start'] = 0.0,
    'negative removal': (patch) =>
        patch['splices']['students']['deleteCount'] = -1,
    'too much removal': (patch) =>
        patch['splices']['students']['deleteCount'] = 2,
    'unknown splice property': (patch) =>
        patch['splices']['students']['extra'] = true,
  };
  for (final invalid in invalidPatches.entries) {
    test(
      'invalid patch ${invalid.key} leaves the prior snapshot unchanged',
      () {
        final original = CenterState()..students = [_student('1')];
        final before = jsonEncode(original.toJson());
        final patch = _delta();
        patch['splices'] = {
          'students': {
            'start': 0,
            'deleteCount': 1,
            'previousLength': 1,
            'items': [_student('2').toJson()],
          },
        };
        invalid.value(patch);
        expect(
          () => original.applyPublicDelta(patch, baseVersion: _baseVersion),
          throwsA(isA<Exception>()),
        );
        expect(jsonEncode(original.toJson()), before);
      },
    );
  }

  test(
    'a later malformed row cannot partially apply an earlier valid section',
    () {
      final original = CenterState()..students = [_student('1')];
      final before = jsonEncode(original.toJson());
      final patch = _delta();
      patch['set'] = {
        'schemaVersion': 11,
        'students': [_student('2').toJson()],
        'audit': [
          {'id': 'missing-required-fields'},
        ],
      };
      expect(
        () => original.applyPublicDelta(patch, baseVersion: _baseVersion),
        throwsA(isA<TypeError>()),
      );
      expect(jsonEncode(original.toJson()), before);
    },
  );

  test('malformed legacy optional sections still reject full parsing', () {
    final legacy = CenterState().toJson()
      ..['schemaVersion'] = 1
      ..['reviews'] = 'invalid';
    expect(() => CenterState.fromJson(legacy), throwsFormatException);
  });
}
