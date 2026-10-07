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
}
