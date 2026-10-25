import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/domain/student_lookup.dart';

void main() {
  final students = [
    Student(
      id: 'zero',
      code: '01234',
      name: 'طالب أول',
      barcode: 'S-01234',
      phone: '٠١٠١٢٣٤٥٦٧٨',
      createdAt: DateTime(2026),
    ),
    Student(
      id: 'collision',
      code: '1234',
      name: 'طالب ثان',
      barcode: '1234',
      guardianPhone: '۰۱۱۹۸۷۶۵۴۳۲',
      createdAt: DateTime(2026),
    ),
    Student(
      id: 'name',
      code: '9000',
      name: 'اسم 1234',
      barcode: 'LAST',
      createdAt: DateTime(2026),
    ),
  ];

  test(
    'indexed search preserves exact precedence, aliases, collisions, Arabic digits and roster order',
    () {
      final index = StudentLookupIndex(students);
      for (final query in [
        '',
        ' ',
        '01234',
        '1234',
        '١٢٣٤',
        '۱۲۳۴',
        ' S-01234 ',
        'last',
        'طالب',
        '010123',
        '011987',
        'absent',
      ]) {
        expect(
          index.candidates(query).map((s) => s.id),
          studentLookupCandidates(students, query).map((s) => s.id),
          reason: query,
        );
      }
      expect(index.candidates('1234').map((s) => s.id), ['zero', 'collision']);
      expect(index.candidates('01234').map((s) => s.id), ['zero']);
    },
  );

  test(
    'short forms of a five-digit code with repeated leading zeros take precedence over partial matches',
    () {
      final original = Student(
        id: 'original',
        code: '00037',
        name: 'الطالب المقصود',
        barcode: 'CARD-00037',
        createdAt: DateTime(2026),
      );
      final roster = [
        Student(
          id: 'partial',
          code: '1037',
          name: 'تشابه جزئي',
          createdAt: DateTime(2026),
        ),
        original,
      ];
      final index = StudentLookupIndex(roster);
      for (final query in ['00037', '0037', '037', '37', '٣٧', '۰۳۷']) {
        expect(index.candidates(query).map((student) => student.id), [
          'original',
        ], reason: query);
        expect(
          studentLookupCandidates(roster, query).map((student) => student.id),
          ['original'],
          reason: query,
        );
        expect(studentMatchesSearch(original, query), isTrue, reason: query);
      }
      expect(original.code, '00037');
      expect(original.barcode, 'CARD-00037');
    },
  );

  test(
    'short zero aliases retain every exact collision and do not shorten full barcodes or other code lengths',
    () {
      final roster = [
        Student(
          id: 'short',
          code: '37',
          name: 'كود مستقل',
          createdAt: DateTime(2026),
        ),
        Student(
          id: 'padded',
          code: '00037',
          name: 'كود مطبوع',
          createdAt: DateTime(2026),
        ),
        Student(
          id: 'barcode',
          code: '8001',
          barcode: '00000037',
          name: 'باركود مستقل',
          createdAt: DateTime(2026),
        ),
        Student(
          id: 'six',
          code: '000037',
          name: 'كود بستة أرقام',
          createdAt: DateTime(2026),
        ),
        Student(
          id: 'zero',
          code: '00000',
          name: 'كود صفري',
          createdAt: DateTime(2026),
        ),
      ];
      final index = StudentLookupIndex(roster);
      expect(index.candidates('37').map((student) => student.id), [
        'short',
        'padded',
      ]);
      expect(index.candidates('00000037').map((student) => student.id), [
        'barcode',
      ]);
      expect(index.candidates('000037').map((student) => student.id), ['six']);
      expect(index.candidates('0').map((student) => student.id), ['zero']);
      expect(index.candidates(' '), isEmpty);
    },
  );
}
