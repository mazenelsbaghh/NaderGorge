import 'dart:convert';
import 'dart:typed_data';

import 'package:archive/archive.dart';

import '../application/center_store.dart';
import '../domain/models.dart';
import '../domain/standalone_exam_results.dart';

/// Text cells keep student codes and phone prefixes intact and cannot become
/// spreadsheet formulas, including when a name starts with =, +, - or @.
Uint8List buildExamAttendanceWorkbook({
  required CenterStore store,
  required StandaloneExamImportPlan plan,
}) {
  if (!store.canAssess || store.isRemote) {
    throw const CenterException(
      'افتح تقرير الامتحان من حساب الموظف على الهوست.',
    );
  }
  final session = store.sessionById(plan.sessionId)!;
  final activity = store.academicActivities.firstWhere(
    (row) => row.id == plan.activityId,
  );
  final group = store.groupLabel(plan.groupId);
  final lesson =
      'الشهر ${session.monthNumber} • حصة ${session.number} • ${session.name}';
  const headers = [
    'كود الطالب',
    'اسم الطالب',
    'رقم الموبايل',
    'موبايل ولي الأمر',
    'المجموعة',
    'الحصة',
    'الامتحان',
    'الحالة',
  ];
  List<String> cells(Student student, String reason) => [
    student.code,
    student.name,
    student.phone,
    student.guardianPhone,
    group,
    lesson,
    activity.name,
    reason,
  ];
  final sheets = [
    [
      headers,
      for (final student in plan.didNotEnter)
        cells(student, 'حاضر ولم يدخل الامتحان'),
    ],
    [
      headers,
      for (final issue in plan.attendanceIssues)
        cells(issue.student, issue.reason),
    ],
  ];
  final files = <String, String>{
    '[Content_Types].xml':
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>''',
    '_rels/.rels':
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>''',
    'xl/workbook.xml':
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="لم يدخلوا الامتحان" sheetId="1" r:id="rId1"/><sheet name="حالات تحتاج متابعة" sheetId="2" r:id="rId2"/></sheets></workbook>''',
    'xl/_rels/workbook.xml.rels':
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>''',
    'xl/styles.xml':
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="2"><xf numFmtId="49" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyAlignment="1"><alignment readingOrder="2" vertical="center"/></xf><xf numFmtId="49" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyAlignment="1"><alignment readingOrder="2" wrapText="1"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>''',
    for (var index = 0; index < sheets.length; index++)
      'xl/worksheets/sheet${index + 1}.xml': _worksheet(sheets[index]),
  };
  final archive = Archive();
  for (final entry in files.entries) {
    final bytes = utf8.encode(entry.value);
    archive.addFile(ArchiveFile(entry.key, bytes.length, bytes));
  }
  return Uint8List.fromList(ZipEncoder().encode(archive));
}

String _xml(String text) => text
    .replaceAll(RegExp(r'[\x00-\x08\x0b\x0c\x0e-\x1f\uFFFE\uFFFF]'), '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&apos;');

String _worksheet(List<List<String>> rows) {
  final xml = StringBuffer(
    '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><dimension ref="A1:H${rows.length}"/><sheetViews><sheetView workbookViewId="0" rightToLeft="1"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight="20"/><cols><col min="1" max="1" width="16" customWidth="1"/><col min="2" max="2" width="32" customWidth="1"/><col min="3" max="4" width="20" customWidth="1"/><col min="5" max="8" width="38" customWidth="1"/></cols><sheetData>''',
  );
  for (var row = 0; row < rows.length; row++) {
    xml.write('<row r="${row + 1}">');
    for (var column = 0; column < rows[row].length; column++) {
      final address = '${String.fromCharCode(65 + column)}${row + 1}';
      xml.write(
        '<c r="$address" t="inlineStr" s="${row == 0 ? 1 : 0}"><is><t xml:space="preserve">${_xml(rows[row][column])}</t></is></c>',
      );
    }
    xml.write('</row>');
  }
  xml.write('</sheetData><autoFilter ref="A1:H${rows.length}"/></worksheet>');
  return xml.toString();
}
