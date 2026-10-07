import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/academic_excel_import.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  test(
    'reads an independently generated XLSX export with Cairo dates and numeric phones',
    () {
      // Synthetic fixture written by openpyxl, independently of the Dart reader.
      final workbook = readAcademicExcel(
        File('test/fixtures/academic-results-openpyxl.xlsx').readAsBytesSync(),
      );
      expect(workbook.sheets.single.name, 'نتائج الامتحان');
      expect(workbook.warnings, hasLength(1));
      final rows = workbook.sheets.single.rows;
      expect(rows.map((row) => row.score), [0, 18.5]);
      expect(rows.map((row) => row.maxScore), [20, 20]);
      expect(rows.every((row) => row.eligible && row.warnings.isEmpty), isTrue);
      expect(rows.first.metadata['cairoDate'], contains('2026-10-08'));
      expect(rows.first.metadata['cairoDate'], contains('18:30'));
      expect(rows.first.code, '08000');
      final students = [
        for (var index = 0; index < 2; index++)
          Student(
            id: 'student-$index',
            name: 'طالب اختبار ${index + 1}',
            code: '0800$index',
            phone: '0101234567${8 + index}',
            groupIds: const ['group'],
            createdAt: DateTime(2026),
          ),
      ];
      final matched = matchAcademicExcelRows(
        rows,
        students: students,
        groupId: 'group',
      );
      expect(matched.every((row) => row.canAutoSelect), isTrue);
      expect(matched.map((row) => row.student!.id), ['student-0', 'student-1']);
    },
  );
}
