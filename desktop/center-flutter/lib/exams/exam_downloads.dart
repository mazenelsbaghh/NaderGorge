import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'package:archive/archive.dart' as archive;
import 'package:path/path.dart' as p;

import '../domain/models.dart';

abstract interface class ExamDownloadSource {
  Future<File> receiveExport(String route, String mimeType);
}

/// Returns a verified file in its own private temporary directory. The caller
/// removes that directory after the native save dialog completes or is canceled.
class ExamDownloads {
  ExamDownloads({
    required this.adminToken,
    required this.runtimeDirectory,
    this.adminPort = 8766,
  });

  final String Function() adminToken;
  final Directory runtimeDirectory;
  final int adminPort;
  static const maxExportBytes = 512 * 1024 * 1024;
  static const xlsxMime =
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
  static const _deadline = Duration(minutes: 5);
  static final _routes = <(RegExp, String, String, String)>[
    (
      RegExp(
        r'^/api/exams/[A-Za-z0-9_-]+/results\.xlsx(?:\?groupId=[A-Za-z0-9_-]*)?$',
      ),
      xlsxMime,
      '.xlsx',
      'GET',
    ),
    (
      RegExp(r'^/api/exams/[A-Za-z0-9_-]+/reports\.zip$'),
      'application/zip',
      '.zip',
      'GET',
    ),
    (
      RegExp(r'^/api/exams/[A-Za-z0-9_-]+/(?:students|results)\.csv$'),
      'text/csv',
      '.csv',
      'GET',
    ),
    (
      RegExp(r'^/api/attempts/[A-Za-z0-9_-]+/report\.pdf$'),
      'application/pdf',
      '.pdf',
      'GET',
    ),
    (
      RegExp(r'^/api/backups/exam-room-[0-9]+\.sqlite3$'),
      'application/octet-stream',
      '.sqlite3',
      'GET',
    ),
    (RegExp(r'^/api/backup$'), 'application/octet-stream', '.sqlite3', 'POST'),
  ];

  Future<File> receive(String route, String mimeType) async {
    final policy = _routePolicy(route, mimeType);
    final token = adminToken();
    if (token.length < 32 ||
        token.length > 256 ||
        RegExp(r'[\x00-\x20\x7f]').hasMatch(token)) {
      throw const CenterException(
        'افتح إدارة الامتحان مجددًا لتجديد جلسة التنزيل.',
      );
    }
    final client = HttpClient()
      ..connectionTimeout = const Duration(seconds: 10)
      ..autoUncompress = false
      ..findProxy = (_) => 'DIRECT';
    final elapsed = Stopwatch()..start();
    final deadline = Timer(_deadline, () => client.close(force: true));
    Directory? staging;
    try {
      staging = await _stagingDirectory();
      final file = File(p.join(staging.path, 'export${policy.$1}'));
      await _receiveFile(
        client,
        (route, mimeType, policy.$2, token),
        file,
        elapsed,
      );
      await _validateExport(file, mimeType, elapsed);
      _remaining(elapsed);
      return file;
    } catch (failure, stack) {
      if (staging != null) await staging.delete(recursive: true);
      if (failure is CenterException) rethrow;
      throw CenterException(
        'تعذر تنزيل ملف سليم؛ تأكد من تشغيل الامتحان والمساحة المتاحة ثم أعد المحاولة.',
        cause: failure,
        stackTrace: stack,
      );
    } finally {
      deadline.cancel();
      client.close(force: true);
    }
  }

  (String, String) _routePolicy(String route, String mimeType) {
    if (adminPort < 1 ||
        adminPort > 65535 ||
        route.length > 500 ||
        RegExp(r'[\x00-\x20\x7f]').hasMatch(route)) {
      throw const CenterException('طلب تنزيل الملف غير صالح.');
    }
    for (final policy in _routes) {
      if (policy.$1.hasMatch(route) && policy.$2 == mimeType) {
        return (policy.$3, policy.$4);
      }
    }
    throw const CenterException('نوع الملف أو رابط تنزيل الامتحان غير مسموح.');
  }

  Future<Directory> _stagingDirectory() async {
    await _requireDirectory(runtimeDirectory);
    final root = await runtimeDirectory.resolveSymbolicLinks();
    final temporary = Directory(p.join(root, 'tmp'));
    await _requireDirectory(temporary);
    if (!p.isWithin(root, await temporary.resolveSymbolicLinks())) {
      throw const CenterException('مجلد تنزيل الامتحانات غير صالح.');
    }
    return temporary.createTemp('export-');
  }

  Future<void> _requireDirectory(Directory directory) async {
    final type = await FileSystemEntity.type(
      directory.path,
      followLinks: false,
    );
    if (type == FileSystemEntityType.notFound) {
      await directory.create(recursive: true);
    }
    if (await FileSystemEntity.type(directory.path, followLinks: false) !=
        FileSystemEntityType.directory) {
      throw const CenterException('مجلد تنزيل الامتحانات غير صالح.');
    }
  }

  Duration _remaining(Stopwatch elapsed) {
    final remaining = _deadline - elapsed.elapsed;
    if (remaining <= Duration.zero) {
      throw const CenterException('استغرق التنزيل وقتًا طويلًا؛ أعد المحاولة.');
    }
    return remaining;
  }

  Future<void> _receiveFile(
    HttpClient client,
    (String, String, String, String) requestDetails,
    File destination,
    Stopwatch elapsed,
  ) async {
    final (route, mimeType, method, token) = requestDetails;
    final parsed = Uri.parse(route);
    final request = await client
        .openUrl(
          method,
          Uri.http(
            '127.0.0.1:$adminPort',
            parsed.path,
            parsed.hasQuery ? parsed.queryParameters : null,
          ),
        )
        .timeout(_remaining(elapsed));
    request.followRedirects = false;
    request.headers.set('X-Admin-Token', token);
    request.headers.set('X-Exam-Request', '1');
    request.headers.set(HttpHeaders.acceptHeader, mimeType);
    if (method == 'POST') {
      request.headers.contentType = ContentType.json;
      request.contentLength = 2;
      request.add(const [123, 125]);
    }
    final response = await request.close().timeout(_remaining(elapsed));
    if (response.statusCode != HttpStatus.ok ||
        response.headers.contentType?.mimeType != mimeType) {
      throw const CenterException(
        'الاستجابة ليست الملف المطلوب؛ لم يُحفظ ملف غير صالح.',
      );
    }
    if (response.contentLength > maxExportBytes) {
      throw const CenterException('ملف الامتحان أكبر من الحد المسموح.');
    }
    await _writeResponse(response, destination, elapsed);
  }

  Future<void> _writeResponse(
    HttpClientResponse response,
    File destination,
    Stopwatch elapsed,
  ) async {
    final output = await destination.open(mode: FileMode.writeOnly);
    var received = 0;
    try {
      await for (final chunk in response.timeout(const Duration(seconds: 30))) {
        _remaining(elapsed);
        received += chunk.length;
        if (received > maxExportBytes) {
          throw const CenterException('ملف الامتحان أكبر من الحد المسموح.');
        }
        await output.writeFrom(chunk);
      }
      if (received == 0 ||
          response.contentLength >= 0 && received != response.contentLength) {
        throw const CenterException('لم يصل الملف كاملًا؛ أعد التنزيل.');
      }
      await output.flush();
    } finally {
      await output.close();
    }
  }

  Future<void> _validateExport(
    File file,
    String mimeType,
    Stopwatch elapsed,
  ) async {
    if (mimeType == xlsxMime || mimeType == 'application/zip') {
      await _validateArchive(file, mimeType, elapsed);
      return;
    }
    final source = await file.open();
    try {
      final prefix = await source.read(100);
      final beginning = latin1.decode(prefix);
      if (mimeType == 'application/pdf') {
        await source.setPosition(max(0, await source.length() - 4096));
        final ending = latin1.decode(await source.read(4096)).trimRight();
        if (!beginning.startsWith('%PDF-') || !ending.endsWith('%%EOF')) {
          throw const CenterException('تقرير PDF غير مكتمل؛ أعد التنزيل.');
        }
      } else if (mimeType == 'application/octet-stream') {
        if (prefix.length < 100 ||
            !beginning.startsWith('SQLite format 3\x00')) {
          throw const CenterException('الملف المستلم ليس نسخة SQLite صالحة.');
        }
      } else {
        final csv = utf8
            .decode(prefix, allowMalformed: true)
            .replaceFirst('\ufeff', '')
            .trimLeft();
        if (csv.isEmpty ||
            const ['{', '[', '<'].contains(csv[0]) ||
            prefix.contains(0)) {
          throw const CenterException('الملف المستلم ليس كشف CSV صالحًا.');
        }
      }
    } finally {
      await source.close();
    }
  }

  Future<void> _validateArchive(
    File file,
    String mimeType,
    Stopwatch elapsed,
  ) async {
    await _validateZipEnd(file);
    final input = archive.InputFileStream(file.path);
    try {
      final directory = archive.ZipDirectory()..read(input);
      if (directory.fileHeaders.isEmpty ||
          directory.fileHeaders.length !=
              directory.totalCentralDirectoryEntries) {
        throw const CenterException('ملف التقارير فارغ أو غير مكتمل.');
      }
      final names = <String>{};
      var total = 0;
      for (final header in directory.fileHeaders) {
        total += header.uncompressedSize;
        _validateZipHeader(header, names, total);
      }
      if (mimeType == xlsxMime &&
          !names.containsAll({
            '[Content_Types].xml',
            'xl/workbook.xml',
            'xl/worksheets/sheet1.xml',
          })) {
        throw const CenterException('الملف المستلم ليس شيت Excel صالحًا.');
      }
      for (final header in directory.fileHeaders) {
        await _verifyZipContent(header, elapsed);
      }
    } finally {
      await input.close();
    }
  }

  Future<void> _validateZipEnd(File file) async {
    final source = await file.open();
    try {
      final size = await source.length();
      final offset = max(0, size - 65577);
      await source.setPosition(offset);
      final tail = await source.read(size - offset);
      final fields = ByteData.sublistView(tail);
      for (var at = tail.length - 22; at >= 0; at--) {
        if (fields.getUint32(at, Endian.little) != 0x06054b50) continue;
        final count = fields.getUint16(at + 10, Endian.little);
        final length = fields.getUint32(at + 12, Endian.little);
        final start = fields.getUint32(at + 16, Endian.little);
        final zip64 =
            at >= 20 && fields.getUint32(at - 20, Endian.little) == 0x07064b50;
        if (at + 22 + fields.getUint16(at + 20, Endian.little) == tail.length &&
            !zip64 &&
            fields.getUint16(at + 4, Endian.little) == 0 &&
            fields.getUint16(at + 6, Endian.little) == 0 &&
            fields.getUint16(at + 8, Endian.little) == count &&
            count > 0 &&
            count <= 10000 &&
            length <= 16 * 1024 * 1024 &&
            start + length <= offset + at) {
          await _validateDirectoryHeaders(source, start, length, count);
          return;
        }
        // ZipDirectory uses the last signature; never validate an earlier one
        // while it parses an unvalidated record hidden inside the comment.
        break;
      }
      throw const CenterException(
        'ملف التقارير غير مكتمل أو أكبر من الحد المسموح.',
      );
    } finally {
      await source.close();
    }
  }

  Future<void> _validateDirectoryHeaders(
    RandomAccessFile source,
    int start,
    int length,
    int expected,
  ) async {
    await source.setPosition(start);
    final bytes = await source.read(length);
    final fields = ByteData.sublistView(bytes);
    var cursor = 0, count = 0;
    while (cursor + 46 <= bytes.length && count < expected) {
      if (fields.getUint32(cursor, Endian.little) != 0x02014b50) break;
      cursor +=
          46 +
          fields.getUint16(cursor + 28, Endian.little) +
          fields.getUint16(cursor + 30, Endian.little) +
          fields.getUint16(cursor + 32, Endian.little);
      count++;
    }
    // Bound objects before ZipDirectory parses local headers; its own parser
    // otherwise trusts directory length rather than the declared entry count.
    if (bytes.length != length || cursor != length || count != expected) {
      throw const CenterException('فهرس ملف التقارير تالف أو غير مكتمل.');
    }
  }

  void _validateZipHeader(
    archive.ZipFileHeader header,
    Set<String> names,
    int total,
  ) {
    final local = header.file;
    final name = header.filename.replaceAll('\\', '/');
    if (local == null ||
        header.generalPurposeBitFlag & 1 != 0 ||
        local.flags & 1 != 0 ||
        !const [0, 8].contains(header.compressionMethod) ||
        header.filename != local.filename ||
        local.compressionMethod !=
            (header.compressionMethod == 8
                ? archive.CompressionType.deflate
                : archive.CompressionType.none) ||
        header.uncompressedSize < 0 ||
        total > maxExportBytes ||
        name.isEmpty ||
        name.startsWith('/') ||
        name.contains(':') ||
        name.contains('\x00') ||
        name.split('/').contains('..') ||
        (header.externalFileAttributes >> 16) & 0xf000 == 0xa000 ||
        !names.add(name)) {
      throw const CenterException(
        'ملف التقارير تالف أو يحتوي صيغة غير مسموحة.',
      );
    }
  }

  Future<void> _verifyZipContent(
    archive.ZipFileHeader header,
    Stopwatch elapsed,
  ) async {
    final compressed = header.file!.getStream(decompress: false);
    if (compressed.length != header.compressedSize) {
      throw const CenterException('ملف التقارير غير مكتمل؛ أعد التنزيل.');
    }
    Stream<List<int>> chunks() async* {
      while (!compressed.isEOS) {
        _remaining(elapsed);
        yield compressed.readBytes(min(65536, compressed.length)).toUint8List();
      }
    }

    final bytes = header.compressionMethod == 8
        ? chunks().transform(ZLibDecoder(raw: true))
        : chunks();
    var crc = 0, count = 0;
    // archive 4.0.9's ZipDecoder verify flag does not check CRC; verify explicitly.
    await for (final chunk in bytes) {
      _remaining(elapsed);
      count += chunk.length;
      if (count > header.uncompressedSize || count > maxExportBytes) {
        throw const CenterException(
          'حجم محتوى التقارير لا يطابق الملف المستلم.',
        );
      }
      crc = archive.getCrc32(chunk, crc);
    }
    if (count != header.uncompressedSize ||
        crc != header.crc32 ||
        crc != header.file!.crc32) {
      throw const CenterException(
        'ملف التقارير تالف أو غير مكتمل؛ أعد التنزيل.',
      );
    }
  }
}
