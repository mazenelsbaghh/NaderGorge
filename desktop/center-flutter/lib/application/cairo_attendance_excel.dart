part of 'academic_excel_import.dart';

typedef CairoAttendanceRow = ({
  String sheet,
  int number,
  String name,
  String code,
  List<String> phones,
  bool present,
  bool readable,
});

class CairoAttendanceWorkbook {
  const CairoAttendanceWorkbook(this.rows, this.skippedSheets);
  final List<CairoAttendanceRow> rows;
  final int skippedSheets;
}

CairoAttendanceWorkbook readCairoAttendanceExcel(List<int> bytes) {
  if (bytes.isEmpty || bytes.length > AcademicExcelLimits.maxFileBytes) {
    throw const FormatException('اختر ملف Excel بحجم لا يزيد عن ٨ ميجابايت.');
  }
  try {
    final sheets = bytes.first == 0xd0
        ? _legacyAttendanceSheets(Uint8List.fromList(bytes))
        : _xlsxAttendanceSheets(bytes);
    final rows = <CairoAttendanceRow>[];
    var skipped = 0;
    for (final (name, source) in sheets) {
      final parsed = _attendanceRows(name, source);
      if (parsed == null) {
        skipped++;
      } else {
        rows.addAll(parsed);
      }
    }
    if (rows.isEmpty) {
      throw const FormatException(
        'لم أجد صفوف حضور. تأكد من وجود عمود كود الطالب أو رقم الهاتف وعناوين الأعمدة.',
      );
    }
    return CairoAttendanceWorkbook(List.unmodifiable(rows), skipped);
  } on FormatException {
    rethrow;
  } on ArgumentError {
    throw const FormatException('ملف الحضور ناقص أو غير سليم.');
  } on StateError {
    throw const FormatException('ملف الحضور ناقص أو غير سليم.');
  } on XmlException {
    throw const FormatException('بيانات ورقة Excel غير سليمة.');
  }
}

typedef _AttendanceCell = ({String value, bool readable});
typedef _AttendanceSheetRows = Iterable<(int, Map<int, _AttendanceCell>)>;
typedef _AttendanceSheets = Iterable<(String, _AttendanceSheetRows)>;

_AttendanceSheets _xlsxAttendanceSheets(List<int> bytes) sync* {
  final reader = _AcademicWorkbookReader(bytes);
  for (final (name, path) in reader._sheetSources()) {
    yield (name, _xlsxAttendanceRows(reader, path));
  }
}

_AttendanceSheetRows _xlsxAttendanceRows(
  _AcademicWorkbookReader reader,
  String path,
) sync* {
  for (final (number, cells) in reader._sheetRows(path)) {
    final row = <int, _AttendanceCell>{};
    for (final (column, cell) in cells) {
      final errors = <String>[];
      // Apply the existing student-code rules to every possible identity column.
      final value = reader._readCell(cell, 3, errors, []);
      if (value.trim().isNotEmpty || errors.isNotEmpty) {
        row[column] = (value: value.trim(), readable: errors.isEmpty);
      }
    }
    yield (number, row);
  }
}

const _attendanceCodeHeaders = ['الكود', 'كود الطالب', 'كود', 'student code'];
const _attendanceNameHeaders = ['اسم الطالب', 'الاسم', 'اسم', 'الطالب', 'name'];
const _attendancePhoneHeaders = [
  'الموبيل',
  'الموبايل',
  'موبيل',
  'موبايل',
  'الهاتف',
  'التليفون',
  'رقم الهاتف',
  'رقم التليفون',
  'ورقم التليفون',
  'رقم الموبايل',
  'رقم هاتف ولي الامر',
  'هاتف ولي الامر',
  'ولي الامر',
  'phone',
];

class _AttendanceHeaders {
  _AttendanceHeaders(Map<int, _AttendanceCell> row)
    : code = _headerColumn(row, _attendanceCodeHeaders),
      name = _headerColumn(row, _attendanceNameHeaders),
      phones = row.entries
          .where(
            (cell) => _attendancePhoneHeaders.contains(
              _normalizedWords(cell.value.value),
            ),
          )
          .map((cell) => cell.key)
          .toList(),
      status = _headerColumn(row, const [
        'الحاله',
        'الحالة',
        'حاله الحضور',
        'status',
      ]);
  final int? code, name, status;
  final List<int> phones;
  bool get valid => code != null || (name != null && phones.isNotEmpty);

  static int? _headerColumn(
    Map<int, _AttendanceCell> row,
    List<String> names,
  ) => row.entries
      .where((cell) => names.contains(_normalizedWords(cell.value.value)))
      .firstOrNull
      ?.key;

  CairoAttendanceRow? read(
    String sheet,
    int number,
    Map<int, _AttendanceCell> row,
  ) {
    final identity = [row[code], ...phones.map((column) => row[column])];
    final student = row[name]?.value ?? '';
    if (identity.every((cell) => cell == null) && student.isEmpty) return null;
    final marker = _normalizedWords(row[status]?.value ?? '');
    return (
      sheet: sheet,
      number: number,
      name: student,
      code: row[code]?.value ?? '',
      phones: [
        for (final column in phones)
          if (row[column]?.readable == true) row[column]!.value,
      ],
      present:
          status == null || const ['حاضر', 'حضور', 'present'].contains(marker),
      readable:
          identity.every((cell) => cell?.readable != false) &&
          row[name]?.readable != false &&
          row[status]?.readable != false,
    );
  }
}

List<CairoAttendanceRow>? _attendanceRows(
  String sheet,
  _AttendanceSheetRows source,
) {
  _AttendanceHeaders? headers;
  final rows = <CairoAttendanceRow>[];
  for (final (number, row) in source) {
    if (headers == null) {
      if (number > 30) break;
      final candidate = _AttendanceHeaders(row);
      if (candidate.valid) headers = candidate;
      continue;
    }
    final parsed = headers.read(sheet, number, row);
    if (parsed != null) rows.add(parsed);
  }
  return headers == null ? null : rows;
}

class CairoAttendanceMatcher {
  CairoAttendanceMatcher(Iterable<Student> roster) {
    for (final student in roster) {
      for (final code in studentIdentifiers(
        student,
      ).map(normalizeStudentIdentifier)) {
        _codes.putIfAbsent(code, () => {}).add(student);
      }
      for (final phone in [
        student.phone,
        student.guardianPhone,
      ].map(normalizeStudentPhone)) {
        if (phone.length >= 7) {
          _phones.putIfAbsent(phone, () => {}).add(student);
        }
      }
    }
  }

  final _codes = <String, Set<Student>>{};
  final _phones = <String, Set<Student>>{};

  Student? match(CairoAttendanceRow row) {
    final byCode =
        _codes[normalizeStudentIdentifier(row.code)] ?? const <Student>{};
    final byPhone = {
      for (final phone in row.phones.map(normalizeStudentPhone))
        ...?_phones[phone],
    };
    final confirmed = byCode.intersection(byPhone);
    if (confirmed.length == 1) return confirmed.single;

    final candidates = byCode.union(byPhone);
    final name = _attendanceName(row.name);
    final sameName = candidates
        .where(
          (student) => name.isNotEmpty && _attendanceName(student.name) == name,
        )
        .toList();
    if (sameName.length == 1) return sameName.single;
    if (candidates.length == 1 && (byPhone.isNotEmpty || name.isEmpty)) {
      return candidates.single;
    }
    return null;
  }
}

String _attendanceName(String name) =>
    _normalizedWords(name).replaceAll('ة', 'ه').replaceAll(' ', '');
