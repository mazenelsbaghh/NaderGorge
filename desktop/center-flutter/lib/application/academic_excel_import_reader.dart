part of 'academic_excel_import.dart';

const _headerAliases = [
  ['اسم الطالب', 'الطالب'],
  ['الموبايل', 'رقم الموبايل', 'الهاتف', 'رقم الهاتف'],
  ['كود الطالب', 'الكود'],
  ['الصف', 'الصف الدراسي'],
  ['السنتر'],
  ['المجموعة'],
  ['الحصة'],
  ['الامتحان'],
  ['تاريخ الجلسة', 'تاريخ الجلسة بتوقيت القاهرة'],
  ['الدرجة النهائية'],
  ['المجموع'],
  ['النسبة', 'النسبة المئوية'],
  ['المتبقي للتصحيح'],
  ['الحالة'],
  ['جاب كام من كام'],
  ['سبب الإلغاء'],
  ['معرف الجلسة'],
  ['معرف محاولة الطالب'],
  ['إصدار الشيت'],
];

class _AcademicWorkbookReader {
  _AcademicWorkbookReader(List<int> bytes) {
    final checkedBytes = _checkZipEnvelope(bytes);
    final directory = ZipDirectory()..read(InputMemoryStream(checkedBytes));
    if (directory.fileHeaders.isEmpty ||
        directory.fileHeaders.length > AcademicExcelLimits.maxArchiveEntries) {
      throw const FormatException(
        'ملف Excel يحتوي على أجزاء أكثر من الحد المسموح.',
      );
    }
    var total = 0;
    for (final header in directory.fileHeaders) {
      final name = header.filename;
      final file = header.file;
      if (name.contains('\\') ||
          name.startsWith('/') ||
          name.contains('\u0000') ||
          name.split('/').contains('..') ||
          _files.containsKey(name) ||
          (header.externalFileAttributes >> 16) & 0xf000 == 0xa000 ||
          header.generalPurposeBitFlag & 1 != 0 ||
          file == null ||
          file.flags & 1 != 0 ||
          file.filename != name ||
          !const [0, 8].contains(header.compressionMethod) ||
          file.compressionMethod !=
              (header.compressionMethod == 0
                  ? CompressionType.none
                  : CompressionType.deflate)) {
        throw const FormatException('صيغة ملف Excel غير مدعومة أو غير سليمة.');
      }
      total += header.uncompressedSize;
      if (total > AcademicExcelLimits.maxUncompressedBytes ||
          header.compressedSize > bytes.length ||
          header.uncompressedSize < 0) {
        throw const FormatException('محتوى ملف Excel أكبر من الحد المسموح.');
      }
      if (name.startsWith('xl/externalLinks/') ||
          name.toLowerCase().endsWith('vbaproject.bin')) {
        throw const FormatException(
          'اختر ملف نتائج بدون روابط بيانات خارجية أو وحدات ماكرو.',
        );
      }
      _files[name] = header;
    }
  }

  final _files = <String, ZipFileHeader>{};
  var _readBytes = 0;
  var _cellCount = 0;
  var _rowCount = 0;
  var _xmlNodes = 0;
  var _date1904 = false;
  final _sharedStrings = <String>[];
  final _numberFormats = <String?>[];

  AcademicExcelWorkbook read(AcademicExcelScoreColumns columns) {
    final workbook = _xml('xl/workbook.xml');
    final date1904 = _elements(
      workbook,
      'workbookPr',
    ).firstOrNull?.getAttribute('date1904');
    _date1904 = date1904 == '1' || date1904 == 'true';
    final relationships = _xml('xl/_rels/workbook.xml.rels');
    final sheetPaths = <String, String>{};
    for (final relationship in _elements(relationships, 'Relationship')) {
      if (relationship.getAttribute('TargetMode') == 'External') {
        throw const FormatException(
          'ملف النتائج يستخدم رابطًا خارجيًا غير مدعوم.',
        );
      }
      final type = relationship.getAttribute('Type') ?? '';
      if (!type.endsWith('/worksheet')) continue;
      final target = relationship.getAttribute('Target') ?? '';
      final id = relationship.getAttribute('Id') ?? '';
      if (id.isEmpty ||
          sheetPaths.containsKey(id) ||
          target.contains('\\') ||
          target.contains('#') ||
          target.contains('?') ||
          Uri.tryParse(target)?.hasScheme != false) {
        throw const FormatException('رابط ورقة Excel غير سليم.');
      }
      final resolved = path.posix.normalize(
        target.startsWith('/')
            ? target.substring(1)
            : path.posix.join('xl', target),
      );
      if (!resolved.startsWith('xl/worksheets/') ||
          !resolved.endsWith('.xml')) {
        throw const FormatException('رابط ورقة Excel خارج الملف.');
      }
      sheetPaths[id] = resolved;
    }
    if (_files.containsKey('xl/sharedStrings.xml')) {
      final shared = _xml('xl/sharedStrings.xml');
      for (final item in _elements(shared, 'si')) {
        if (_sharedStrings.length >= AcademicExcelLimits.maxCells) {
          throw const FormatException(
            'عدد النصوص في Excel أكبر من الحد المسموح.',
          );
        }
        _sharedStrings.add(_cellText(item));
      }
    }
    _readStyles();
    final sheets = <AcademicExcelSheet>[];
    final warnings = <String>[];
    final sourceSheets = _elements(workbook, 'sheet').toList();
    if (sourceSheets.isEmpty ||
        sourceSheets.length > AcademicExcelLimits.maxSheets) {
      throw const FormatException(
        'عدد أوراق Excel غير مدعوم. الحد الأقصى ١٦ ورقة.',
      );
    }
    final usedPaths = <String>{};
    for (final sheet in sourceSheets) {
      final name = sheet.getAttribute('name') ?? 'ورقة';
      final id = sheet.attributes
          .where((a) => a.name.local == 'id')
          .firstOrNull
          ?.value;
      final sheetPath = sheetPaths[id];
      if (sheetPath == null || !usedPaths.add(sheetPath)) {
        throw const FormatException('تعذر تحديد ورقة Excel بشكل فريد.');
      }
      final rows = _readSheet(sheetPath, columns);
      if (rows == null) {
        warnings.add(
          'لم تُقرأ الورقة «$name»: عناوين الأعمدة لا تطابق نموذج النتائج A:S.',
        );
      } else {
        sheets.add(AcademicExcelSheet(name: name, rows: rows));
      }
    }
    if (sheets.isEmpty) {
      throw const FormatException(
        'لم توجد ورقة نتائج بالعناوين المطلوبة بالترتيب من A إلى S.',
      );
    }
    return AcademicExcelWorkbook(sheets: sheets, warnings: warnings);
  }

  XmlDocument _xml(String name) {
    final header = _files[name];
    if (header == null) {
      throw const FormatException('ملف Excel ناقص أو غير سليم.');
    }
    final maximum = math.min(
      header.uncompressedSize,
      AcademicExcelLimits.maxUncompressedBytes - _readBytes,
    );
    final output = _BoundedExcelSink(maximum);
    final compressed = header.file!.getRawContent();
    if (header.compressionMethod == 0) {
      output.add(compressed);
    } else {
      // archive's native decodeStream gathers every inflated chunk before
      // writing. A direct sink enforces the real size during decompression.
      final decoder = ZLibCodec(
        raw: true,
      ).decoder.startChunkedConversion(output);
      for (var offset = 0; offset < compressed.length; offset += 1024) {
        decoder.add(
          compressed.sublist(
            offset,
            math.min(offset + 1024, compressed.length),
          ),
        );
      }
      decoder.close();
    }
    final bytes = output.bytes.takeBytes();
    if (bytes.length != header.uncompressedSize ||
        getCrc32(bytes) != header.crc32) {
      throw const FormatException('محتوى ملف Excel تالف أو غير مكتمل.');
    }
    _readBytes += bytes.length;
    final text = utf8.decode(bytes);
    if (RegExp(r'<!\s*(DOCTYPE|ENTITY)', caseSensitive: false).hasMatch(text)) {
      throw const FormatException(
        'ملف Excel يحتوي على تعريفات نصية غير مدعومة.',
      );
    }
    _checkXmlBounds(text);
    return XmlDocument.parse(text);
  }

  void _checkXmlBounds(String text) {
    var depth = 0;
    var rows = 0;
    var cells = 0;
    var strings = 0;
    for (final event in parseEvents(
      text,
      validateNesting: true,
      validateDocument: true,
    )) {
      if (event is! XmlEndElementEvent &&
          ++_xmlNodes > AcademicExcelLimits.maxXmlNodes) {
        throw const FormatException(
          'تركيب ملف Excel أكبر أو أعقد من الحد المسموح.',
        );
      }
      if (event is XmlStartElementEvent) {
        if (depth >= 32 ||
            event.attributes.length > 64 ||
            event.attributes.any(
              (attribute) =>
                  attribute.value.length >
                  AcademicExcelLimits.maxCellCharacters,
            )) {
          throw const FormatException(
            'تركيب ملف Excel أكبر أو أعقد من الحد المسموح.',
          );
        }
        if (!event.isSelfClosing) depth++;
        if (event.localName == 'row' &&
                ++rows + _rowCount >
                    AcademicExcelLimits.maxRows +
                        AcademicExcelLimits.maxSheets ||
            event.localName == 'c' &&
                ++cells + _cellCount >
                    AcademicExcelLimits.maxCells +
                        19 * AcademicExcelLimits.maxSheets ||
            event.localName == 'si' &&
                ++strings > AcademicExcelLimits.maxCells) {
          throw const FormatException(
            'عدد صفوف أو خلايا Excel أكبر من الحد المسموح.',
          );
        }
      } else if (event is XmlEndElementEvent) {
        depth--;
      } else if (event is XmlTextEvent) {
        if (event.value.length > AcademicExcelLimits.maxCellCharacters) {
          throw const FormatException(
            'يوجد نص أطول من الحد المسموح داخل Excel.',
          );
        }
      } else if (event is XmlCDATAEvent &&
          event.value.length > AcademicExcelLimits.maxCellCharacters) {
        throw const FormatException('يوجد نص أطول من الحد المسموح داخل Excel.');
      }
    }
  }

  void _readStyles() {
    if (!_files.containsKey('xl/styles.xml')) return;
    final styles = _xml('xl/styles.xml');
    final formats = <String, String>{};
    for (final format in _elements(styles, 'numFmt')) {
      formats[format.getAttribute('numFmtId') ?? ''] =
          format.getAttribute('formatCode') ?? '';
    }
    final cellFormats = _elements(styles, 'cellXfs').firstOrNull;
    if (cellFormats == null) return;
    for (final format in cellFormats.childElements.where(
      (e) => e.name.local == 'xf',
    )) {
      _numberFormats.add(formats[format.getAttribute('numFmtId')]);
    }
  }

  List<AcademicExcelRow>? _readSheet(
    String sheetPath,
    AcademicExcelScoreColumns columns,
  ) {
    final document = _xml(sheetPath);
    final rows = <AcademicExcelRow>[];
    var hasHeader = false;
    var previousRow = 0;
    for (final row in _elements(document, 'row')) {
      if (++_rowCount >
          AcademicExcelLimits.maxRows + AcademicExcelLimits.maxSheets) {
        throw const FormatException('عدد صفوف Excel أكبر من ١٠ آلاف صف.');
      }
      final rowNumber = int.tryParse(row.getAttribute('r') ?? '');
      if (rowNumber == null ||
          rowNumber <= previousRow ||
          rowNumber > AcademicExcelLimits.maxRows + 1) {
        throw const FormatException(
          'ترتيب صفوف Excel غير سليم أو يتجاوز الحد المسموح.',
        );
      }
      previousRow = rowNumber;
      final values = List.filled(academicExcelColumns.length, '');
      final errors = <String>[];
      final warnings = <String>[];
      final usedColumns = <int>{};
      var extraValues = false;
      for (final cell in row.childElements.where((e) => e.name.local == 'c')) {
        if (++_cellCount >
            AcademicExcelLimits.maxCells + 19 * AcademicExcelLimits.maxSheets) {
          throw const FormatException('عدد خلايا Excel أكبر من الحد المسموح.');
        }
        final reference = cell.getAttribute('r') ?? '';
        final match = RegExp(
          r'^([A-Z]{1,3})([1-9][0-9]{0,6})$',
        ).firstMatch(reference);
        if (match == null || int.parse(match.group(2)!) != rowNumber) {
          throw const FormatException('عنوان خلية Excel غير سليم.');
        }
        var column = 0;
        for (final letter in match.group(1)!.codeUnits) {
          column = column * 26 + letter - 64;
        }
        if (!usedColumns.add(column) || column > 16384) {
          throw const FormatException(
            'ملف Excel يحتوي على خلية مكررة أو عنوان غير سليم.',
          );
        }
        final value = _readCell(cell, column, errors, warnings);
        if (column <= 19) {
          values[column - 1] = value;
        } else if (value.isNotEmpty) {
          extraValues = true;
        }
      }
      if (values.every((value) => value.isEmpty) && !extraValues) continue;
      if (!hasHeader) {
        if (extraValues || errors.isNotEmpty || !_validHeaders(values)) {
          return null;
        }
        hasHeader = true;
      } else {
        if (extraValues) {
          errors.add('الصف يحتوي على بيانات خارج أعمدة النموذج A:S.');
        }
        rows.add(_academicRow(rowNumber, values, columns, errors, warnings));
      }
    }
    return hasHeader ? rows : null;
  }

  String _readCell(
    XmlElement cell,
    int column,
    List<String> errors,
    List<String> warnings,
  ) {
    final type = cell.getAttribute('t');
    final formula = cell.childElements.any((e) => e.name.local == 'f');
    if (formula && const [2, 3, 10, 11, 13, 14, 16].contains(column)) {
      errors.add(
        'الصف يحتوي على معادلة في الدرجة أو بيانات المطابقة؛ صدّر القيم النهائية أولًا.',
      );
    }
    final raw =
        cell.childElements
            .where((e) => e.name.local == 'v')
            .firstOrNull
            ?.innerText ??
        '';
    String value;
    switch (type) {
      case 's':
        final index = int.tryParse(raw);
        if (index == null || index < 0 || index >= _sharedStrings.length) {
          throw const FormatException('مرجع النص داخل Excel غير سليم.');
        }
        value = _sharedStrings[index];
      case 'inlineStr':
        value = _cellText(cell);
      case 'e':
        errors.add('الصف يحتوي على خلية بها خطأ من Excel.');
        value = raw;
      case null:
      case 'n':
        value = raw;
        if (column == 2 || column == 3 || column == 17 || column == 18) {
          final integer = _exactInteger(raw);
          if (raw.isNotEmpty && integer == null) {
            errors.add(
              'رقم الهاتف أو الكود أو معرف المحاولة غير دقيق؛ احفظه كنص في Excel.',
            );
          } else if (integer != null) {
            value = integer;
            final style = int.tryParse(cell.getAttribute('s') ?? '');
            final format =
                style != null && style >= 0 && style < _numberFormats.length
                ? _numberFormats[style]
                : null;
            if (format != null && RegExp(r'^0{1,32}$').hasMatch(format)) {
              value = value.padLeft(format.length, '0');
            }
          }
        }
        if (column == 9 && raw.isNotEmpty) {
          final date = _excelWallTime(raw, date1904: _date1904);
          if (date == null) {
            warnings.add('تاريخ المصدر قيمة Excel رقمية غير معروفة: $raw.');
          } else {
            value = date;
          }
        }
      case 'str':
      case 'd':
        value = raw;
      default:
        errors.add('نوع خلية غير مدعوم في الصف.');
        value = raw;
    }
    if (value.length > AcademicExcelLimits.maxCellCharacters) {
      throw const FormatException('يوجد نص أطول من الحد المسموح داخل Excel.');
    }
    return value.trim();
  }
}

Iterable<XmlElement> _elements(XmlNode node, String localName) => node
    .descendants
    .whereType<XmlElement>()
    .where((element) => element.name.local == localName);

String _cellText(XmlNode node) {
  final buffer = StringBuffer();
  for (final text in _elements(node, 't')) {
    buffer.write(text.innerText);
    if (buffer.length > AcademicExcelLimits.maxCellCharacters) {
      throw const FormatException('يوجد نص أطول من الحد المسموح داخل Excel.');
    }
  }
  return buffer.toString();
}

bool _validHeaders(List<String> cells) {
  for (var i = 0; i < 19; i++) {
    final expected = [
      ..._headerAliases[i],
      academicExcelColumns[i],
    ].map(_normalizedWords);
    if (!expected.contains(_normalizedWords(cells[i]))) return false;
  }
  return true;
}

/// Expand numeric identifiers without floating point rounding. Excel numbers
/// exceeding 15 significant digits cannot reliably identify a student.
String? _exactInteger(String raw) {
  final match = RegExp(
    r'^\+?([0-9]+)(?:\.([0-9]*))?(?:[eE]([+-]?[0-9]+))?$',
  ).firstMatch(raw);
  if (match == null) return null;
  final whole = match.group(1)!;
  final fraction = match.group(2) ?? '';
  final exponent = int.tryParse(match.group(3) ?? '0');
  if (exponent == null || exponent.abs() > 32) return null;
  var digits = '$whole$fraction';
  if (digits.replaceFirst(RegExp(r'^0+'), '').length > 15) return null;
  final decimalPosition = whole.length + exponent;
  if (decimalPosition <= 0) {
    return RegExp(r'^0+$').hasMatch(digits) ? '0' : null;
  }
  if (decimalPosition < digits.length) {
    if (!RegExp(r'^0+$').hasMatch(digits.substring(decimalPosition))) {
      return null;
    }
    digits = digits.substring(0, decimalPosition);
  } else if (decimalPosition > digits.length) {
    digits = digits.padRight(decimalPosition, '0');
  }
  if (digits.length > 15) return null;
  return digits;
}

String? _excelWallTime(String value, {required bool date1904}) {
  final serial = _finiteNumber(value);
  if (serial == null ||
      serial < 0 ||
      serial > 2957000 ||
      (!date1904 && serial.floor() == 60)) {
    return null;
  }
  // UTC is used only for calendar arithmetic. The string retains the sheet's
  // Cairo wall time and makes no claim about the computer's timezone.
  final epoch = date1904
      ? DateTime.utc(1904, 1, 1)
      : DateTime.utc(1899, 12, 31);
  final corrected = !date1904 && serial >= 61 ? serial - 1 : serial;
  final date = epoch.add(
    Duration(milliseconds: (corrected * Duration.millisecondsPerDay).round()),
  );
  return date
      .toIso8601String()
      .replaceFirst('T', ' ')
      .replaceFirst(RegExp(r'\.000Z$'), '')
      .replaceFirst(RegExp(r'Z$'), '');
}

class _BoundedExcelSink implements Sink<List<int>> {
  _BoundedExcelSink(this.maximum);
  final int maximum;
  final bytes = BytesBuilder(copy: false);
  @override
  void add(List<int> data) {
    if (bytes.length + data.length > maximum) {
      throw const FormatException(
        'محتوى ملف Excel المضغوط يتجاوز الحجم المعلن.',
      );
    }
    bytes.add(data);
  }

  @override
  void close() {}
}

Uint8List _checkZipEnvelope(List<int> bytes) {
  final data = ByteData.sublistView(Uint8List.fromList(bytes));
  for (
    var offset = bytes.length - 22;
    offset >= math.max(0, bytes.length - 65557);
    offset--
  ) {
    if (data.getUint32(offset, Endian.little) != 0x06054b50) continue;
    if (offset + 22 + data.getUint16(offset + 20, Endian.little) !=
        bytes.length) {
      continue;
    }
    final count = data.getUint16(offset + 10, Endian.little);
    final size = data.getUint32(offset + 12, Endian.little);
    final start = data.getUint32(offset + 16, Endian.little);
    if (data.getUint16(offset + 4, Endian.little) != 0 ||
        data.getUint16(offset + 6, Endian.little) != 0 ||
        data.getUint16(offset + 8, Endian.little) != count ||
        count == 0 ||
        count > AcademicExcelLimits.maxArchiveEntries ||
        start + size > offset) {
      throw const FormatException('تركيب ملف Excel المضغوط غير مدعوم.');
    }
    if (offset >= 20 &&
        data.getUint32(offset - 20, Endian.little) == 0x07064b50) {
      throw const FormatException(
        'صيغة ZIP64 غير مطلوبة لهذا الملف وغير مدعومة.',
      );
    }
    var entryOffset = start;
    var actualCount = 0;
    while (entryOffset < start + size) {
      if (++actualCount > AcademicExcelLimits.maxArchiveEntries ||
          entryOffset + 46 > start + size ||
          data.getUint32(entryOffset, Endian.little) != 0x02014b50) {
        throw const FormatException('فهرس ملف Excel المضغوط غير سليم.');
      }
      entryOffset +=
          46 +
          data.getUint16(entryOffset + 28, Endian.little) +
          data.getUint16(entryOffset + 30, Endian.little) +
          data.getUint16(entryOffset + 32, Endian.little);
    }
    if (entryOffset != start + size || actualCount != count) {
      throw const FormatException('عدد أجزاء ملف Excel لا يطابق فهرسه.');
    }
    // Archive locates EOCD by its last byte signature, including signatures in
    // comments. Remove only the ZIP comment after verifying the real directory.
    final checked = Uint8List.fromList(bytes.sublist(0, offset + 22));
    ByteData.sublistView(checked).setUint16(offset + 20, 0, Endian.little);
    return checked;
  }
  throw const FormatException('اختر ملف Excel بصيغة ‎.xlsx سليمة.');
}
