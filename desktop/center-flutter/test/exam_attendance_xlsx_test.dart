import 'dart:convert';

import 'package:archive/archive.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/exams/exam_attendance_xlsx.dart';
import 'package:xml/xml.dart';

import 'helpers/standalone_exam_fixture.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  test(
    'real XLSX separates actual attendees who never entered from essay follow-up and preserves text cells',
    () async {
      final fixture = await StandaloneExamFixture.open();
      addTearDown(fixture.close);
      final bytes = buildExamAttendanceWorkbook(
        store: fixture.store,
        plan: fixture.preview(),
      );
      final archive = ZipDecoder().decodeBytes(bytes);
      final documents = <String, XmlDocument>{
        for (final file in archive.files)
          file.name: XmlDocument.parse(utf8.decode(file.content as List<int>)),
      };
      final workbook = documents['xl/workbook.xml']!;
      expect(
        workbook
            .findAllElements('sheet')
            .map((sheet) => sheet.getAttribute('name')),
        ['لم يدخلوا الامتحان', 'حالات تحتاج متابعة'],
      );
      final missing = documents['xl/worksheets/sheet1.xml']!;
      final issues = documents['xl/worksheets/sheet2.xml']!;
      expect(
        missing.findAllElements('sheetView').single.getAttribute('rightToLeft'),
        '1',
      );
      expect(missing.findAllElements('row'), hasLength(2));
      final cells = missing
          .findAllElements('row')
          .last
          .findElements('c')
          .toList();
      expect(
        cells.map((cell) => cell.getAttribute('t')),
        everyElement('inlineStr'),
      );
      expect(cells.first.innerText, '00003');
      expect(cells[1].innerText, '=2+2 & طالب <متابعة>');
      expect(cells[2].innerText, '01000000003');
      expect(cells[3].innerText, '01200000003');
      expect(cells[4].innerText, contains('مجموعة التقرير'));
      expect(cells[6].innerText, 'امتحان من عشرين');
      expect(cells[7].innerText, 'حاضر ولم يدخل الامتحان');
      expect(
        documents.values.expand((document) => document.findAllElements('f')),
        isEmpty,
      );
      expect(issues.findAllElements('row'), hasLength(2));
      expect(
        issues.findAllElements('row').last.innerText,
        contains('بانتظار تصحيح المقالي'),
      );
      expect(
        issues.findAllElements('row').last.innerText,
        isNot(contains('طالب 4')),
      );
    },
  );
}
