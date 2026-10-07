import 'dart:convert';
import 'dart:typed_data';

import 'package:archive/archive.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/academic_excel_import.dart';
import 'package:massar_center/domain/models.dart';
import 'package:xml/xml.dart';

void main() {
  test(
    'reads Arabic headers and a genuine zero with source metadata intact',
    () {
      final row = _row(score: '٠', maximum: '۲۰', code: '٠٠١٢٣');
      row[16] = 'unknown-session';
      row[17] = 'attempt-42';
      row[18] = 'future-version';
      final result = readAcademicExcel(_xlsx([row], headers: _arabicHeaders));
      final parsed = result.sheets.single.rows.single;
      expect(parsed.score, 0);
      expect(parsed.maxScore, 20);
      expect(parsed.eligible, isTrue);
      expect(parsed.metadata['externalSessionId'], 'unknown-session');
      expect(parsed.metadata['externalAttemptId'], 'attempt-42');
      expect(parsed.metadata['sheetVersion'], 'future-version');
      expect(parsed.cells[2], '٠٠١٢٣');
      expect(parsed.rowNumber, 2);
      expect(() => parsed.cells[0] = 'changed', throwsUnsupportedError);
      expect(() => parsed.metadata['code'] = 'changed', throwsUnsupportedError);
    },
  );

  test(
    'reads shared rich text and numeric identifiers without floating rounding',
    () {
      final row = _row();
      row[0] = const _Cell('0', type: 's');
      row[1] = const _Cell('1.012345678E9', type: 'n');
      row[2] = const _Cell('123', type: 'n', style: 1);
      final parsed = readAcademicExcel(
        _xlsx(
          [row],
          extraParts: {
            'xl/sharedStrings.xml':
                '<sst><si><r><t>أحمد </t></r><r><t>محمد</t></r></si></sst>',
            'xl/styles.xml':
                '<styleSheet><numFmts><numFmt numFmtId="164" formatCode="00000"/></numFmts><cellXfs><xf numFmtId="0"/><xf numFmtId="164"/></cellXfs></styleSheet>',
          },
        ),
      ).sheets.single.rows.single;
      expect(parsed.name, 'أحمد محمد');
      expect(parsed.phone, '1012345678');
      expect(parsed.code, '00123');
      expect(parsed.warnings, isEmpty);
      final match = matchAcademicExcelRows(
        [parsed],
        students: [_student('one', phone: '01012345678', code: '00123')],
        groupId: 'selected',
      ).single;
      expect(match.student?.id, 'one');
      expect(match.canAutoSelect, isTrue);
    },
  );

  test(
    'keeps several compatible sheets and explicitly reports an incompatible one',
    () {
      final parsed = readAcademicExcel(
        _xlsx(
          [_row()],
          moreSheets: {
            'نتائج أخرى': _sheet([_row(code: 'other')]),
            'ملاحظات': _sheet(
              [
                ['ملاحظة'],
              ],
              headers: ['وصف'],
            ),
          },
        ),
      );
      expect(parsed.sheets.map((s) => s.name), ['نتائج', 'نتائج أخرى']);
      expect(parsed.warnings.single, contains('ملاحظات'));
    },
  );

  test(
    'rejects shifted or repeated headers instead of interpreting a wrong score',
    () {
      for (final headers in [
        [...academicExcelColumns.skip(1), 'extra'],
        [...academicExcelColumns]..[10] = 'finalScore',
      ]) {
        expect(
          () => readAcademicExcel(_xlsx([_row()], headers: headers)),
          throwsFormatException,
        );
      }
    },
  );

  test('does not invent absent marks, maxima or completion counts', () {
    final invalid = <List<Object?>>[
      _row(score: ''),
      _row(score: 'NaN'),
      _row(score: 'Infinity'),
      _row(score: '-1'),
      _row(score: '21'),
      _row(maximum: ''),
      _row(maximum: '0'),
      _row(maximum: '20.5'),
      _row()..[12] = '',
      _row()..[12] = '1',
    ];
    final parsed = readAcademicExcel(_xlsx(invalid)).sheets.single.rows;
    expect(parsed, hasLength(invalid.length));
    expect(
      parsed.every((row) => !row.eligible && row.errors.isNotEmpty),
      isTrue,
    );
    expect(parsed.first.score, isNull);
  });

  test(
    'blocks cancelled, pending, absent and invalid results; unknown status needs review',
    () {
      final statuses = [
        'cancelled',
        'pending',
        'absent',
        'invalid',
        'غير مُصحح',
        'قيد التصحيح',
        'غائب',
      ];
      final parsed = readAcademicExcel(
        _xlsx([
          for (final status in statuses) _row()..[13] = status,
          _row()..[15] = 'إلغاء يدوي',
          _row()..[13] = 'legacy state',
          _row()..[13] = 'مُصحَّح',
          _row()..[15] = '—',
        ]),
      ).sheets.single.rows;
      expect(
        parsed.take(statuses.length + 1).every((row) => !row.eligible),
        isTrue,
      );
      expect(parsed[statuses.length + 1].eligible, isTrue);
      expect(parsed[statuses.length + 1].requiresReview, isTrue);
      expect(parsed[statuses.length + 1].warnings, isNotEmpty);
      expect(parsed[statuses.length + 2].requiresReview, isFalse);
      expect(parsed.last.eligible, isTrue);
    },
  );

  test(
    'rejects grade formulas even with cached values; never evaluates a formula',
    () {
      final formula = _row()
        ..[9] = const _Cell('18', type: 'n', formula: '10+8');
      final error = _row()..[9] = const _Cell('#VALUE!', type: 'e');
      final parsed = readAcademicExcel(
        _xlsx([formula, error]),
      ).sheets.single.rows;
      expect(parsed.first.score, 18);
      expect(parsed.first.eligible, isFalse);
      expect(parsed.first.errors.single, contains('معادلة'));
      expect(parsed.last.eligible, isFalse);
    },
  );

  test(
    'preserves Cairo calendar fields with Excel 1900 and 1904 date systems',
    () {
      final row = _row()..[8] = const _Cell('61.5', type: 'n');
      final first = readAcademicExcel(_xlsx([row])).sheets.single.rows.single;
      expect(first.metadata['cairoDate'], '1900-03-01 12:00:00');
      expect(first.warnings, isEmpty);
      final other = readAcademicExcel(
        _xlsx([row], date1904: true),
      ).sheets.single.rows.single;
      expect(other.metadata['cairoDate'], '1904-03-02 12:00:00');
      final invalid = readAcademicExcel(
        _xlsx([_row()..[8] = const _Cell('60', type: 'n')]),
      ).sheets.single.rows.single;
      expect(invalid.metadata['cairoDate'], '60');
      expect(invalid.warnings.single, contains('غير معروفة'));
    },
  );

  test(
    'flags imprecise Excel numeric identifiers while preserving text IDs',
    () {
      final parsed = readAcademicExcel(
        _xlsx([
          _row()..[2] = const _Cell('1234567890123456', type: 'n'),
          _row()..[1] = const _Cell('1.123E-4', type: 'n'),
          _row(code: '1234567890123456'),
        ]),
      ).sheets.single.rows;
      expect(parsed[0].eligible, isFalse);
      expect(parsed[1].eligible, isFalse);
      expect(parsed[2].eligible, isTrue);
      expect(parsed[2].code, '1234567890123456');
    },
  );

  test('rejects duplicate cells and bad shared-string references', () {
    final sheet = _sheet([_row()]).replaceFirst(
      '</row>',
      '<c r="A1" t="inlineStr"><is><t>duplicate</t></is></c></row>',
    );
    expect(
      () => readAcademicExcel(_xlsx([], firstSheet: sheet)),
      throwsFormatException,
    );
    expect(
      () => readAcademicExcel(
        _xlsx([_row()..[0] = const _Cell('400', type: 's')]),
      ),
      throwsFormatException,
    );
  });

  test(
    'flags populated columns after S rather than shifting them into the row',
    () {
      final result = readAcademicExcel(
        _xlsx([
          [..._row(), 'extra'],
        ]),
      ).sheets.single.rows.single;
      expect(result.eligible, isFalse);
      expect(result.errors.single, contains('A:S'));
      expect(result.score, 18);
    },
  );

  test(
    'rejects external sheet targets, DTD, external links and excessive XML depth',
    () {
      final badBooks = [
        _xlsx([_row()], sheetTarget: 'https://example.invalid/sheet.xml'),
        _xlsx([_row()], sheetTarget: '../../other.xml'),
        _xlsx(
          [],
          firstSheet: '<!DOCTYPE worksheet [<!ENTITY x "value">]><worksheet/>',
        ),
        _xlsx(
          [_row()],
          extraParts: {'xl/externalLinks/externalLink1.xml': '<externalLink/>'},
        ),
        _xlsx(
          [],
          firstSheet:
              '${List.filled(34, '<x>').join()}${List.filled(34, '</x>').join()}',
        ),
      ];
      for (final book in badBooks) {
        expect(() => readAcademicExcel(book), throwsFormatException);
      }
    },
  );

  test('enforces compressed, declared, actual decompressed and row limits', () {
    expect(
      () => readAcademicExcel(Uint8List(AcademicExcelLimits.maxFileBytes + 1)),
      throwsFormatException,
    );
    final hugeDeclared = _patchEntrySize(
      _xlsx([_row()]),
      'xl/worksheets/sheet1.xml',
      AcademicExcelLimits.maxUncompressedBytes + 1,
    );
    expect(() => readAcademicExcel(hugeDeclared), throwsFormatException);
    final dishonest = _patchEntrySize(
      _xlsx([_row(name: List.filled(2000, 'أ').join())]),
      'xl/worksheets/sheet1.xml',
      80,
    );
    expect(() => readAcademicExcel(dishonest), throwsFormatException);
    final tooFar = _sheet([_row()])
        .replaceAll('r="2"', 'r="10002"')
        .replaceAllMapped(RegExp(r'r="([A-S])2"'), (m) => 'r="${m[1]}10002"');
    expect(
      () => readAcademicExcel(_xlsx([], firstSheet: tooFar)),
      throwsFormatException,
    );
  });

  test(
    'bounds actual central-directory entries before decoding archive objects',
    () {
      final entries = {
        for (var i = 0; i < AcademicExcelLimits.maxArchiveEntries; i++)
          'extra/$i': '',
      };
      final bytes = _xlsx([_row()], extraParts: entries);
      final dishonest = Uint8List.fromList(bytes);
      final data = ByteData.sublistView(dishonest);
      final eocd = bytes.length - 22;
      data.setUint16(eocd + 8, 3, Endian.little);
      data.setUint16(eocd + 10, 3, Endian.little);
      expect(() => readAcademicExcel(dishonest), throwsFormatException);
    },
  );

  test(
    'ZIP comments cannot replace the verified end-of-directory signature',
    () {
      final bytes = _xlsx([_row()]);
      final comment = [0x50, 0x4b, 0x05, 0x06, ...List.filled(30, 0)];
      final commented = Uint8List.fromList([...bytes, ...comment]);
      ByteData.sublistView(
        commented,
      ).setUint16(bytes.length - 2, comment.length, Endian.little);
      expect(readAcademicExcel(commented).sheets.single.rows.single.score, 18);
    },
  );

  group('group-scoped matching', () {
    final one = _student(
      'one',
      code: '01234',
      phone: '01012345678',
      name: 'أحمد محمد علي',
    );
    final two = _student(
      'two',
      code: '8888',
      guardian: '01112345678',
      name: 'يوسف عادل محمود',
    );
    final outside = _student(
      'outside',
      code: 'outside',
      phone: '01212345678',
      group: 'elsewhere',
    );

    AcademicExcelMatch match(List<Object?> row, {List<Student>? roster}) =>
        matchAcademicExcelRows(
          readAcademicExcel(_xlsx([row])).sheets.single.rows,
          students: roster ?? [one, two, outside],
          groupId: 'selected',
        ).single;

    test(
      'accepts Arabic aliases and either unique identifier when the other is unknown',
      () {
        expect(match(_row(code: '١٢٣٤', phone: '999999')).student?.id, 'one');
        final phoneOnly = match(
          _row(code: 'platform-code', phone: '+20 11 1234 5678'),
        );
        expect(phoneOnly.student?.id, 'two');
        expect(phoneOnly.matchReason, contains('ولي الأمر'));
        expect(phoneOnly.canAutoSelect, isTrue);
        expect(
          match(_row(code: '', phone: '01012345678')).canAutoSelect,
          isTrue,
        );
      },
    );

    test('does not use students outside the group or empty identifiers', () {
      final excluded = match(
        _row(code: 'outside', phone: '01212345678', name: 'اسم غريب'),
      );
      expect(excluded.student, isNull);
      expect(excluded.canAutoSelect, isFalse);
      expect(
        excluded.suggestions.every(
          (suggestion) => suggestion.student.groupIds.contains('selected'),
        ),
        isTrue,
      );
      expect(
        excluded.suggestions.map((suggestion) => suggestion.student.id),
        isNot(contains('outside')),
      );
      expect(
        match(_row(code: '', phone: '', name: 'اسم غريب')).student,
        isNull,
      );
    });

    test('conflicting identifiers and shared phones stay manual', () {
      final conflict = match(_row(code: '1234', phone: '01112345678'));
      expect(conflict.student, isNull);
      expect(conflict.issues.single, contains('غير متفقة'));
      expect(conflict.suggestions.map((s) => s.student.id), ['one', 'two']);
      final shared = match(
        _row(code: '1234', phone: '01012345678'),
        roster: [
          one,
          _student('sibling', guardian: '01012345678'),
        ],
      );
      expect(shared.student, isNull);
      expect(shared.canAutoSelect, isFalse);
      expect(shared.issues.single, contains('مشترك'));
    });

    test(
      'duplicate target students and repeated source attempts never auto-select',
      () {
        final duplicate = readAcademicExcel(
          _xlsx([
            _row(code: '1234', phone: ''),
            _row(code: '', phone: '01012345678'),
          ]),
        ).sheets.single.rows;
        final matched = matchAcademicExcelRows(
          duplicate,
          students: [one],
          groupId: 'selected',
        );
        expect(
          matched.every((m) => m.student == null && !m.canAutoSelect),
          isTrue,
        );
        final attempts = readAcademicExcel(
          _xlsx([
            _row(code: '1234', phone: '')
              ..[16] = 'session'
              ..[17] = 'attempt',
            _row(code: '8888', phone: '')
              ..[16] = 'session'
              ..[17] = 'attempt',
          ]),
        ).sheets.single.rows;
        final matchedAttempts = matchAcademicExcelRows(
          attempts,
          students: [one, two],
          groupId: 'selected',
        );
        expect(
          matchedAttempts.every(
            (m) =>
                !m.canAutoSelect && m.issues.single.contains('المحاولة مكرر'),
          ),
          isTrue,
        );
      },
    );

    test(
      'name suggestions remain bounded, relevant and never establish identity',
      () {
        final result = match(
          _row(name: 'احمد مُحمد على', code: '', phone: ''),
          roster: [
            one,
            for (var i = 0; i < 4; i++)
              _student('similar$i', name: 'أحمد محمد اسم$i'),
            two,
          ],
        );
        expect(result.student, isNull);
        expect(result.canAutoSelect, isFalse);
        expect(result.suggestions, hasLength(3));
        expect(result.suggestions.first.student.id, 'one');
        expect(result.suggestions.every((s) => s.similarity >= .5), isTrue);
        expect(
          match(_row(name: 'اسم مختلف تماما', code: '', phone: '')).suggestions,
          isEmpty,
        );
      },
    );

    test(
      'unknown status and invalid grades cannot be auto-selected by exact code',
      () {
        expect(
          match(_row(code: '1234', phone: '')..[13] = 'legacy').canAutoSelect,
          isFalse,
        );
        expect(
          match(_row(code: '1234', phone: '', score: '')).canAutoSelect,
          isFalse,
        );
      },
    );
  });
}

const _arabicHeaders = [
  'اسم الطالب',
  'الموبايل',
  'كود الطالب',
  'الصف',
  'السنتر',
  'المجموعة',
  'الحصة',
  'الامتحان',
  'تاريخ الجلسة بتوقيت القاهرة',
  'الدرجة النهائية',
  'المجموع',
  'النسبة',
  'المتبقي للتصحيح',
  'الحالة',
  'جاب كام من كام',
  'سبب الإلغاء',
  'معرّف الجلسة',
  'معرّف محاولة الطالب',
  'إصدار الشيت',
];

List<Object?> _row({
  String name = 'أحمد محمد',
  String code = '1234',
  String phone = '',
  String score = '18',
  String maximum = '20',
}) => [
  name,
  phone,
  code,
  'الثالث',
  'القاهرة',
  'مجموعة ١',
  'حصة ٢',
  'امتحان ٤',
  '2026-10-08 13:45:00',
  score,
  maximum,
  '90%',
  '0',
  'completed',
  '$score من $maximum',
  '',
  '',
  '',
  'v1',
];

Student _student(
  String id, {
  String name = 'طالب آخر',
  String code = '',
  String phone = '',
  String guardian = '',
  String group = 'selected',
}) => Student(
  id: id,
  name: name,
  code: code,
  phone: phone,
  guardianPhone: guardian,
  groupIds: [group],
  createdAt: DateTime(2026),
);

class _Cell {
  const _Cell(this.value, {this.type = 'inlineStr', this.style, this.formula});
  final String value, type;
  final int? style;
  final String? formula;
}

String _sheet(
  List<List<Object?>> rows, {
  List<String> headers = academicExcelColumns,
}) =>
    '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>${[headers, ...rows].indexed.map((entry) {
      final number = entry.$1 + 1;
      final cells = entry.$2.indexed.map((item) {
        if (item.$2 == null) return '';
        final cell = item.$2 is _Cell ? item.$2 as _Cell : _Cell('${item.$2}');
        final reference = '${String.fromCharCode(65 + item.$1)}$number';
        final value = XmlText(cell.value).toXmlString();
        final style = cell.style == null ? '' : ' s="${cell.style}"';
        final formula = cell.formula == null ? '' : '<f>${XmlText(cell.formula!).toXmlString()}</f>';
        final content = cell.type == 'inlineStr' ? '<is><t>$value</t></is>' : '<v>$value</v>';
        return '<c r="$reference" t="${cell.type}"$style>$formula$content</c>';
      }).join();
      return '<row r="$number">$cells</row>';
    }).join()}</sheetData></worksheet>';

Uint8List _xlsx(
  List<List<Object?>> rows, {
  List<String> headers = academicExcelColumns,
  Map<String, String> extraParts = const {},
  Map<String, String> moreSheets = const {},
  String? firstSheet,
  String sheetTarget = 'worksheets/sheet1.xml',
  bool date1904 = false,
}) {
  final sheets = {
    'نتائج': firstSheet ?? _sheet(rows, headers: headers),
    ...moreSheets,
  };
  final parts = <String, String>{
    'xl/workbook.xml':
        '<workbook xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><workbookPr date1904="${date1904 ? 1 : 0}"/><sheets>${sheets.keys.indexed.map((e) => '<sheet name="${e.$2}" sheetId="${e.$1 + 1}" r:id="rId${e.$1 + 1}"/>').join()}</sheets></workbook>',
    'xl/_rels/workbook.xml.rels':
        '<Relationships>${sheets.keys.indexed.map((e) => '<Relationship Id="rId${e.$1 + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="${e.$1 == 0 ? sheetTarget : 'worksheets/sheet${e.$1 + 1}.xml'}"/>').join()}</Relationships>',
    for (final entry in sheets.values.indexed)
      'xl/worksheets/sheet${entry.$1 + 1}.xml': entry.$2,
    ...extraParts,
  };
  final archive = Archive();
  for (final entry in parts.entries) {
    final bytes = utf8.encode(entry.value);
    archive.addFile(ArchiveFile(entry.key, bytes.length, bytes));
  }
  return Uint8List.fromList(ZipEncoder().encode(archive));
}

Uint8List _patchEntrySize(Uint8List original, String filename, int declared) {
  final bytes = Uint8List.fromList(original);
  final directory = ZipDirectory()..read(InputMemoryStream(bytes));
  final target = directory.fileHeaders.singleWhere(
    (file) => file.filename == filename,
  );
  final data = ByteData.sublistView(bytes);
  data.setUint32(target.localHeaderOffset + 22, declared, Endian.little);
  var offset = directory.centralDirectoryOffset;
  for (final header in directory.fileHeaders) {
    if (header.filename == filename) {
      data.setUint32(offset + 24, declared, Endian.little);
      break;
    }
    offset +=
        46 +
        data.getUint16(offset + 28, Endian.little) +
        data.getUint16(offset + 30, Endian.little) +
        data.getUint16(offset + 32, Endian.little);
  }
  return bytes;
}
