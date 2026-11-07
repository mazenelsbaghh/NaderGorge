part of 'academic_excel_import.dart';

_AttendanceSheets _legacyAttendanceSheets(Uint8List bytes) sync* {
  _checkLegacyWorkbookAllocation(bytes);
  final workbook = XlsReader.fromBytes(bytes);
  if (workbook.sheetCount > AcademicExcelLimits.maxSheets) {
    throw const FormatException('الحد الأقصى ١٦ ورقة في ملف Excel.');
  }
  var cellCount = 0;
  for (var index = 0; index < workbook.sheetCount; index++) {
    final sheet = workbook.sheet(index);
    cellCount += sheet.rowCount * sheet.colCount;
    if (sheet.lastRow > AcademicExcelLimits.maxRows + 1 ||
        cellCount > AcademicExcelLimits.maxCells) {
      throw const FormatException(
        'عدد خلايا ملف Excel القديم أكبر من المسموح.',
      );
    }
    yield (
      sheet.name,
      Iterable.generate(sheet.rowCount, (offset) {
        final row = sheet.firstRow + offset;
        return (
          row + 1,
          {
            for (var column = sheet.firstCol; column < sheet.lastCol; column++)
              if (sheet.cell(row, column) case final Object value)
                column + 1: _legacyCell(value),
          },
        );
      }),
    );
  }
}

_AttendanceCell _legacyCell(Object value) {
  final text = value.toString().trim();
  final exact = value is num ? _exactInteger(text) : text;
  return (
    value: exact ?? text,
    readable:
        exact != null && text.length <= AcademicExcelLimits.maxCellCharacters,
  );
}

// Bound compound-document sector chains before the legacy reader follows them.
void _checkLegacyWorkbookAllocation(Uint8List bytes) {
  const sectorSize = 512;
  const freeSector = 0xffffffff;
  const endOfChain = 0xfffffffe;
  const invalid = FormatException(
    'بنية ملف ‎.xls غير سليمة أو غير مدعومة؛ جرّب حفظه بصيغة ‎.xlsx.',
  );
  if (bytes.length < sectorSize * 2) {
    throw invalid;
  }
  final data = ByteData.sublistView(bytes);
  int word(int offset) => data.getUint32(offset, Endian.little);
  if (data.getUint16(30, Endian.little) != 9 ||
      data.getUint16(32, Endian.little) != 6 ||
      word(72) != 0) {
    throw invalid;
  }
  final completeSectors = bytes.length ~/ sectorSize - 1;
  final sectors = (bytes.length - 1) ~/ sectorSize;
  final fatSectors = <int>[
    for (var offset = 76; offset < sectorSize; offset += 4)
      if (word(offset) != freeSector && word(offset) != endOfChain)
        word(offset),
  ];
  if (fatSectors.length != word(44) ||
      fatSectors.toSet().length != fatSectors.length ||
      fatSectors.length * 128 < sectors ||
      fatSectors.any((sector) => sector >= completeSectors)) {
    throw invalid;
  }
  final fat = [
    for (final sector in fatSectors)
      for (
        var offset = (sector + 1) * sectorSize;
        offset < (sector + 2) * sectorSize;
        offset += 4
      )
        word(offset),
  ];
  final seen = Uint8List(sectors);
  for (var start = 0; start < sectors; start++) {
    var sector = start;
    while (sector < sectors && seen[sector] == 0) {
      seen[sector] = 1;
      sector = fat[sector];
    }
    if ((sector < sectors && seen[sector] == 1) ||
        (sector >= sectors && sector < 0xfffffffc)) {
      throw invalid;
    }
    sector = start;
    while (sector < sectors && seen[sector] == 1) {
      seen[sector] = 2;
      sector = fat[sector];
    }
  }
  final miniFatStart = word(60);
  if (miniFatStart >= sectors &&
      miniFatStart != endOfChain &&
      miniFatStart != freeSector) {
    throw invalid;
  }

  // Mini-stream reads are bounded by the directory entry's declared size.
  for (var sector = word(48); sector != endOfChain && sector != freeSector;) {
    if (sector >= completeSectors) throw invalid;
    final start = (sector + 1) * sectorSize;
    for (var offset = start; offset < start + sectorSize; offset += 128) {
      final size = word(offset + 120);
      final type = bytes[offset + 66];
      if (word(offset + 124) != 0 ||
          size > bytes.length ||
          ((type == 5 || type == 2 && size >= word(56)) &&
              size > 0 &&
              word(offset + 116) >= sectors)) {
        throw invalid;
      }
    }
    sector = fat[sector];
  }
}
