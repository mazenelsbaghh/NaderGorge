import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:archive/archive.dart' as archive;
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/exam_downloads.dart';
import 'package:path/path.dart' as p;

const _xlsx = ExamDownloads.xlsxMime;
const _route = '/api/exams/exam-1/results.xlsx?groupId=group-2';
const _token = 'synthetic-native-export-admin-token-1';

Uint8List _zip(Map<String, String> contents, {bool store = false}) {
  final files = archive.Archive();
  for (final entry in contents.entries) {
    final bytes = utf8.encode(entry.value);
    files.add(
      store
          ? archive.ArchiveFile.noCompress(entry.key, bytes.length, bytes)
          : archive.ArchiveFile(entry.key, bytes.length, bytes),
    );
  }
  return Uint8List.fromList(archive.ZipEncoder().encode(files));
}

Uint8List _workbook({bool store = false}) => _zip({
  '[Content_Types].xml': '<Types/>',
  'xl/workbook.xml': '<workbook/>',
  'xl/worksheets/sheet1.xml': '<worksheet/>',
}, store: store);

class _Reply {
  const _Reply(this.body, {this.mime = _xlsx, this.status = 200, this.length});
  final List<int> body;
  final String mime;
  final int status;
  final int? length;
}

class _Request {
  _Request(HttpRequest request, this.body)
    : method = request.method,
      route = request.uri.toString(),
      token = request.headers.value('X-Admin-Token'),
      examRequest = request.headers.value('X-Exam-Request');
  final String method, route, body;
  final String? token, examRequest;
}

void main() {
  late Directory directory;
  late HttpServer server;
  late ExamDownloads downloader;
  late _Reply reply;
  late List<_Request> requests;
  var currentToken = _token;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('exam-exports-test-');
    server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    reply = _Reply(_workbook());
    requests = [];
    currentToken = _token;
    server.listen((request) async {
      final body = await utf8.decoder.bind(request).join();
      requests.add(_Request(request, body));
      final authenticated =
          request.headers.value('X-Admin-Token') == currentToken;
      final response = authenticated
          ? reply
          : _Reply(
              utf8.encode('{"error":"expired"}'),
              mime: 'application/json',
              status: 401,
            );
      // Write the wire response ourselves so truncated Content-Length is real.
      final socket = await request.response.detachSocket(writeHeaders: false);
      socket.write(
        'HTTP/1.1 ${response.status} Test\r\n'
        'Content-Type: ${response.mime}\r\n'
        'Content-Length: ${response.length ?? response.body.length}\r\n'
        'Connection: close\r\n',
      );
      if (response.status == 302) socket.write('Location: /unexpected\r\n');
      socket.write('\r\n');
      socket.add(response.body);
      await socket.flush();
      await socket.close();
    });
    downloader = ExamDownloads(
      adminToken: () => currentToken,
      runtimeDirectory: directory,
      adminPort: server.port,
    );
  });

  tearDown(() async {
    await server.close(force: true);
    await directory.delete(recursive: true);
  });

  Future<void> expectNoStaging() async {
    final temporary = Directory(p.join(directory.path, 'tmp'));
    if (await temporary.exists()) {
      expect(await temporary.list().toList(), isEmpty);
    }
  }

  test(
    'native export uses current authentication and preserves workbook bytes',
    () async {
      final client = HttpClient();
      try {
        final request = await client.getUrl(
          Uri.parse('http://127.0.0.1:${server.port}$_route'),
        );
        final unauthenticated = await request.close();
        expect(unauthenticated.statusCode, 401);
        await unauthenticated.drain<void>();
      } finally {
        client.close(force: true);
      }
      currentToken = 'synthetic-rotated-export-admin-token-2';
      final file = await downloader.receive(_route, _xlsx);
      expect(await file.readAsBytes(), reply.body);
      expect(p.extension(file.path), '.xlsx');
      expect(requests.last.token, currentToken);
      expect(requests.last.examRequest, '1');
      expect(requests.last.route, _route);
      expect(requests.last.method, 'GET');
      expect(
        p.isWithin(
          p.join(await directory.resolveSymbolicLinks(), 'tmp'),
          file.path,
        ),
        isTrue,
      );
      await file.parent.delete(recursive: true);
      await expectNoStaging();
    },
  );

  test(
    'repeated downloads get unique staging and never overwrite runtime data',
    () async {
      final database = File(p.join(directory.path, 'exams.sqlite3'));
      await database.writeAsString('existing runtime database');
      final first = await downloader.receive(_route, _xlsx);
      reply = _Reply(_workbook(store: true));
      final second = await downloader.receive(_route, _xlsx);
      expect(second.parent.path, isNot(first.parent.path));
      expect(await first.exists(), isTrue);
      expect(await second.readAsBytes(), reply.body);
      expect(await database.readAsString(), 'existing runtime database');
    },
  );

  for (final (label, damagedReply) in <(String, _Reply)>[
    (
      'HTTP error',
      _Reply(utf8.encode('{}'), mime: 'application/json', status: 401),
    ),
    ('wrong MIME', _Reply(_workbook(), mime: 'application/json')),
    ('JSON masquerading as XLSX', _Reply(utf8.encode('{"error":"expired"}'))),
    ('truncated ZIP', _Reply(_workbook().sublist(0, _workbook().length - 22))),
    ('non-workbook ZIP', _Reply(_zip({'error.json': '{}'}))),
    ('truncated transfer', _Reply(_workbook(), length: _workbook().length + 1)),
    ('redirect', _Reply(_workbook(), status: 302)),
    (
      'oversized transfer',
      _Reply([], length: ExamDownloads.maxExportBytes + 1),
    ),
  ]) {
    test('$label is rejected and partial files are removed', () async {
      reply = damagedReply;
      await expectLater(
        downloader.receive(_route, _xlsx),
        throwsA(isA<CenterException>()),
      );
      await expectNoStaging();
      expect(requests.map((r) => r.route), [_route]);
    });
  }

  test(
    'ZIP checksum corruption is rejected even with complete HTTP body',
    () async {
      final bytes = _workbook(store: true);
      final position = latin1.decode(bytes).indexOf('<workbook/>');
      expect(position, isNonNegative);
      bytes[position + 1] ^= 1;
      reply = _Reply(bytes);
      await expectLater(
        downloader.receive(_route, _xlsx),
        throwsA(isA<CenterException>()),
      );
      await expectNoStaging();
    },
  );

  test(
    'streaming ZIP data descriptors used by the Go engine are supported',
    () async {
      // Standard deflated ZIP written to a forward-only stream: the local header
      // has zero sizes/CRC and the descriptor follows the compressed report.
      final bytes = base64Decode(
        'UEsDBBQACAAIAA+4TF0AAAAAAAAAAAAAAAAKAAAAcmVwb3J0LnBkZlMNcHHTNdQz4SquzCvJSC3JTOZSVXX1d+MCAFBLBwhWog81GwAAABkAAABQSwECFAMUAAgACAAPuExdVqIPNRsAAAAZAAAACgAAAAAAAAAAAAAAgAEAAAAAcmVwb3J0LnBkZlBLBQYAAAAAAQABADgAAABTAAAAAAA=',
      );
      reply = _Reply(bytes, mime: 'application/zip');
      final file = await downloader.receive(
        '/api/exams/exam-1/reports.zip',
        'application/zip',
      );
      expect(await file.readAsBytes(), bytes);
    },
  );

  test('unapproved routes or MIME pairs never contact the engine', () async {
    for (final (route, mime) in <(String, String)>[
      ('http://127.0.0.1:${server.port}$_route', _xlsx),
      ('//example.com/export.xlsx', _xlsx),
      ('$_route#fragment', _xlsx),
      ('$_route&groupId=other', _xlsx),
      ('$_route\n', _xlsx),
      ('/api/exams/../results.xlsx', _xlsx),
      ('/api/exams/%2e%2e/results.xlsx', _xlsx),
      ('/api/settings/gemini', _xlsx),
      ('/api/backups/../exams.sqlite3', 'application/octet-stream'),
      (_route, 'application/pdf'),
    ]) {
      await expectLater(
        downloader.receive(route, mime),
        throwsA(isA<CenterException>()),
        reason: route,
      );
    }
    expect(requests, isEmpty);
    await expectNoStaging();
  });

  test(
    'PDF, ZIP, CSV and both backup routes preserve verified content',
    () async {
      final sqlite = [
        ...ascii.encode('SQLite format 3\x00'),
        ...List.filled(84, 0),
      ];
      for (final (route, mime, bytes, method)
          in <(String, String, List<int>, String)>[
            (
              '/api/attempts/attempt-1/report.pdf',
              'application/pdf',
              ascii.encode('%PDF-1.4\nbody\n%%EOF\n'),
              'GET',
            ),
            (
              '/api/exams/exam-1/reports.zip',
              'application/zip',
              _zip({'طالب.pdf': '%PDF-1.4\nbody\n%%EOF\n'}),
              'GET',
            ),
            (
              '/api/exams/exam-1/results.csv',
              'text/csv',
              utf8.encode('\ufeffاسم,درجة\nطالب,0\n'),
              'GET',
            ),
            (
              '/api/exams/exam-1/students.csv',
              'text/csv',
              utf8.encode('اسم,كود\nطالب,001\n'),
              'GET',
            ),
            (
              '/api/backups/exam-room-123.sqlite3',
              'application/octet-stream',
              sqlite,
              'GET',
            ),
            ('/api/backup', 'application/octet-stream', sqlite, 'POST'),
          ]) {
        reply = _Reply(bytes, mime: '$mime; charset=utf-8');
        final file = await downloader.receive(route, mime);
        expect(await file.readAsBytes(), bytes);
        expect(requests.last.method, method);
        expect(requests.last.route, route);
        expect(requests.last.body, method == 'POST' ? '{}' : '');
        await file.parent.delete(recursive: true);
      }
      await expectNoStaging();
    },
  );

  test('truncated PDF, SQLite header and JSON/HTML CSV are rejected', () async {
    for (final (route, mime, body) in <(String, String, String)>[
      (
        '/api/attempts/attempt-1/report.pdf',
        'application/pdf',
        '%PDF-1.4\ncut',
      ),
      ('/api/backup', 'application/octet-stream', 'SQLite format 3\x00'),
      ('/api/exams/exam-1/results.csv', 'text/csv', '{"error":"failed"}'),
      (
        '/api/exams/exam-1/results.csv',
        'text/csv',
        '\ufeff  <html>error</html>',
      ),
    ]) {
      reply = _Reply(utf8.encode(body), mime: mime);
      await expectLater(
        downloader.receive(route, mime),
        throwsA(isA<CenterException>()),
      );
      await expectNoStaging();
    }
  });

  test('unsafe archive names and declared expansion are rejected', () async {
    final oversized = _zip({'report.pdf': 'synthetic report'});
    final central = latin1.decode(oversized).indexOf('PK\x01\x02');
    ByteData.sublistView(
      oversized,
    ).setUint32(central + 24, ExamDownloads.maxExportBytes + 1, Endian.little);
    final incorrectCount = _workbook();
    final footer = ByteData.sublistView(incorrectCount);
    footer.setUint16(incorrectCount.length - 14, 1, Endian.little);
    footer.setUint16(incorrectCount.length - 12, 1, Endian.little);
    final encrypted = _zip({'report.pdf': 'synthetic report'});
    final encryptedFields = ByteData.sublistView(encrypted);
    encryptedFields.setUint16(6, 1, Endian.little);
    encryptedFields.setUint16(
      latin1.decode(encrypted).indexOf('PK\x01\x02') + 8,
      1,
      Endian.little,
    );
    for (final bytes in [
      _zip({'../exams.sqlite3': 'outside'}),
      _zip({'C:\\exams.sqlite3': 'outside'}),
      _zip({'/exams.sqlite3': 'outside'}),
      oversized,
      incorrectCount,
      encrypted,
    ]) {
      reply = _Reply(bytes, mime: 'application/zip');
      await expectLater(
        downloader.receive('/api/exams/exam-1/reports.zip', 'application/zip'),
        throwsA(isA<CenterException>()),
      );
      await expectNoStaging();
    }
  });

  test(
    'symlink temporary directory is rejected without contacting engine',
    () async {
      final outside = await Directory.systemTemp.createTemp(
        'exam-outside-test-',
      );
      addTearDown(() => outside.delete(recursive: true));
      final preserved = File(p.join(outside.path, 'exams.sqlite3'));
      await preserved.writeAsString('do not replace');
      await Link(p.join(directory.path, 'tmp')).create(outside.path);
      await expectLater(
        downloader.receive(_route, _xlsx),
        throwsA(isA<CenterException>()),
      );
      expect(requests, isEmpty);
      expect(await preserved.readAsString(), 'do not replace');
      expect(await outside.list().length, 1);
    },
    skip: Platform.isWindows
        ? 'Windows symlink creation needs privilege'
        : false,
  );
}
