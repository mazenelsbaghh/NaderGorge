import 'dart:convert';
import 'dart:io';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:archive/archive.dart';
import 'package:path/path.dart' as path;
import 'package:xml/xml.dart';
import 'package:xml/xml_events.dart';

import '../domain/models.dart';
import '../domain/student_lookup.dart';

part 'academic_excel_import_reader.dart';
part 'academic_excel_import_matching.dart';

abstract final class AcademicExcelLimits {
  static const maxFileBytes = 8 * 1024 * 1024;
  static const maxUncompressedBytes = 32 * 1024 * 1024;
  static const maxRows = 10000;
  static const maxCells = 190000;
  static const maxCellCharacters = 4096;
  static const maxSheets = 16;
  static const maxArchiveEntries = 512;
  static const maxXmlNodes = 1000000;
}

enum AcademicExcelScoreColumns { scoreThenMaximum, maximumThenScore }

const academicExcelColumns = [
  'studentName',
  'phone',
  'code',
  'gradeLevel',
  'center',
  'group',
  'lesson',
  'exam',
  'cairoDate',
  'finalScore',
  'totalMax',
  'percentage',
  'remainingToCorrect',
  'status',
  'scoreLabel',
  'cancellationReason',
  'externalSessionId',
  'externalAttemptId',
  'sheetVersion',
];

class AcademicExcelWorkbook {
  AcademicExcelWorkbook({
    required Iterable<AcademicExcelSheet> sheets,
    Iterable<String> warnings = const [],
  }) : sheets = List.unmodifiable(sheets),
       warnings = List.unmodifiable(warnings);
  final List<AcademicExcelSheet> sheets;
  final List<String> warnings;
}

class AcademicExcelSheet {
  AcademicExcelSheet({
    required this.name,
    required Iterable<AcademicExcelRow> rows,
  }) : rows = List.unmodifiable(rows);
  final String name;
  final List<AcademicExcelRow> rows;
}

class AcademicExcelRow {
  AcademicExcelRow._({
    required this.rowNumber,
    required List<String> cells,
    required this.score,
    required this.maxScore,
    required Iterable<String> errors,
    required Iterable<String> warnings,
    required this.requiresReview,
    required List<String> cellErrors,
    required List<String> cellWarnings,
  }) : cells = List.unmodifiable(cells),
       errors = List.unmodifiable(errors),
       warnings = List.unmodifiable(warnings),
       _cellErrors = List.unmodifiable(cellErrors),
       _cellWarnings = List.unmodifiable(cellWarnings),
       metadata = Map.unmodifiable({
         for (var i = 0; i < academicExcelColumns.length; i++)
           academicExcelColumns[i]: cells[i],
       });

  final int rowNumber;
  final List<String> cells, errors, warnings, _cellErrors, _cellWarnings;
  final Map<String, String> metadata;
  final num? score;
  final int? maxScore;
  final bool requiresReview;
  String get name => cells[0];
  String get phone => cells[1];
  String get code => cells[2];
  bool get eligible => errors.isEmpty;

  AcademicExcelRow withScoreColumns(AcademicExcelScoreColumns columns) =>
      _academicRow(rowNumber, cells, columns, _cellErrors, _cellWarnings);
}

/// Reads the fixed A:S export. Formulas are never evaluated and grade formulas
/// are rejected: a cached result is not evidence of a current mark.
AcademicExcelWorkbook readAcademicExcel(
  List<int> bytes, {
  AcademicExcelScoreColumns scoreColumns =
      AcademicExcelScoreColumns.scoreThenMaximum,
}) {
  if (bytes.isEmpty || bytes.length > AcademicExcelLimits.maxFileBytes) {
    throw const FormatException('اختر ملف Excel بحجم لا يزيد عن ٨ ميجابايت.');
  }
  try {
    return _AcademicWorkbookReader(bytes).read(scoreColumns);
  } on FormatException {
    rethrow;
  } catch (_) {
    throw const FormatException(
      'تعذر قراءة ملف Excel. تأكد أنه ملف ‎.xlsx سليم.',
    );
  }
}

AcademicExcelRow _academicRow(
  int rowNumber,
  List<String> cells,
  AcademicExcelScoreColumns columns,
  List<String> cellErrors,
  List<String> cellWarnings,
) {
  final errors = [...cellErrors];
  final warnings = [...cellWarnings];
  final scoreIndex = columns == AcademicExcelScoreColumns.scoreThenMaximum
      ? 9
      : 10;
  final maximumIndex = scoreIndex == 9 ? 10 : 9;
  final score = _finiteNumber(cells[scoreIndex]);
  final maximum = _finiteNumber(cells[maximumIndex]);
  final maximumValid =
      maximum != null &&
      maximum > 0 &&
      maximum <= 9007199254740991 &&
      maximum == maximum.truncateToDouble();
  if (score == null || score < 0) {
    errors.add('درجة الطالب مفقودة أو غير صحيحة.');
  }
  if (!maximumValid) {
    errors.add('الدرجة الكلية يجب أن تكون عددًا صحيحًا موجبًا.');
  }
  if (score != null && maximumValid && score > maximum) {
    errors.add('درجة الطالب أكبر من الدرجة الكلية.');
  }
  final remaining = _finiteNumber(cells[12]);
  if (remaining == null || remaining != 0) {
    errors.add('لا يمكن استيراد نتيجة لم يكتمل تصحيحها.');
  }
  final status = _normalizedWords(cells[13]);
  final blocked = RegExp(
    r'cancel|invalid|absent|pending|incomplete|in progress|not completed|not reviewed|not graded|ملغ|الغاء|غائب|غياب|غير صالح|قيد|انتظار|لم يكتمل|غير مكتمل|لم يصحح|غير مصحح',
  ).hasMatch(status);
  if (blocked || !_blankPlaceholder(cells[15])) {
    errors.add('حالة المحاولة لا تسمح باستيراد الدرجة أو أن المحاولة ملغاة.');
  }
  const completed = {
    'completed',
    'complete',
    'reviewed',
    'graded',
    'corrected',
    'submitted',
    'finished',
    'final',
    'مكتمل',
    'مكتملة',
    'مصحح',
    'مصححة',
    'تم التصحيح',
    'تم تصحيحه',
    'مكتمل التصحيح',
    'تم التسليم',
    'منتهي',
    'منتهية',
    'ناجح',
    'راسب',
  };
  final requiresReview = !blocked && !completed.contains(status);
  if (requiresReview) {
    warnings.add('حالة المحاولة غير معروفة؛ راجعها قبل اختيار الطالب.');
  }
  if (cells[0].trim().isEmpty) warnings.add('اسم الطالب غير موجود في الملف.');
  if (cells[1].trim().isEmpty && cells[2].trim().isEmpty) {
    warnings.add('لا يوجد كود أو هاتف للمطابقة؛ يلزم اختيار الطالب يدويًا.');
  }
  return AcademicExcelRow._(
    rowNumber: rowNumber,
    cells: cells,
    score: score,
    maxScore: maximumValid ? maximum.toInt() : null,
    errors: errors.toSet(),
    warnings: warnings.toSet(),
    requiresReview: requiresReview,
    cellErrors: cellErrors,
    cellWarnings: cellWarnings,
  );
}

double? _finiteNumber(String text) {
  final value = double.tryParse(
    normalizeStudentIdentifier(text).replaceAll('٫', '.'),
  );
  return value != null && value.isFinite ? value : null;
}

String _normalizedWords(String value) => normalizeStudentIdentifier(value)
    .replaceAll(RegExp(r'[\u064B-\u065F\u0670ـ]'), '')
    .replaceAll(RegExp('[أإآ]'), 'ا')
    .replaceAll('ى', 'ي')
    .replaceAll(RegExp(r'[^a-z0-9\u0621-\u064A]+'), ' ')
    .trim()
    .replaceAll(RegExp(r'\s+'), ' ');

bool _blankPlaceholder(String value) =>
    value.trim().isEmpty || RegExp(r'^[-–—]+$').hasMatch(value.trim());
