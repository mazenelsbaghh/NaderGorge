import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/exam_export_saver.dart';
import 'package:path/path.dart' as p;

void main() {
  late Directory directory, runtime, documents;
  late File verified;
  late ExamExportSaver saver;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('exam-save-test-');
    runtime = await Directory(p.join(directory.path, 'runtime')).create();
    documents = await Directory(p.join(directory.path, 'documents')).create();
    verified = await File(
      p.join(runtime.path, 'export.xlsx'),
    ).writeAsBytes(List.generate(200000, (index) => index % 256));
    saver = ExamExportSaver(
      protectedDirectories: [runtime],
      isAuthorized: () => true,
    );
  });

  tearDown(() => directory.delete(recursive: true));

  test(
    'renamed export preserves bytes and gets its verified extension',
    () async {
      for (final (name, expected) in [
        ('نتائج الامتحان', 'نتائج الامتحان.xlsx'),
        ('results.csv', 'results.xlsx'),
        ('results.XLSX', 'results.xlsx'),
      ]) {
        final destination = await saver.save(
          verified,
          p.join(documents.path, name),
        );
        expect(p.basename(destination.path), expected);
        expect(await destination.readAsBytes(), await verified.readAsBytes());
        expect(await verified.exists(), isTrue);
        await destination.delete();
      }
    },
  );

  test(
    'existing file is preserved including collision after extension correction',
    () async {
      final existing = await File(
        p.join(documents.path, 'results.xlsx'),
      ).writeAsString('previous export');
      await expectLater(
        saver.save(verified, p.join(documents.path, 'results.csv')),
        throwsA(isA<CenterException>()),
      );
      expect(await existing.readAsString(), 'previous export');
      expect(await documents.list().length, 1);
    },
  );

  test(
    'simultaneous saves to one filename cannot replace each other',
    () async {
      final target = p.join(documents.path, 'simultaneous.xlsx');
      Future<bool> trySave() async {
        try {
          await saver.save(verified, target);
          return true;
        } on CenterException {
          return false;
        }
      }

      final saved = await Future.wait([trySave(), trySave()]);
      expect(saved.where((success) => success).length, 1);
      expect(await File(target).readAsBytes(), await verified.readAsBytes());
    },
  );

  test(
    'protected application data and nested automatic backups are rejected',
    () async {
      final nested = await Directory(p.join(runtime.path, 'backups')).create();
      for (final parent in [runtime, nested]) {
        await expectLater(
          saver.save(verified, p.join(parent.path, 'new.xlsx')),
          throwsA(isA<CenterException>()),
        );
        expect(await File(p.join(parent.path, 'new.xlsx')).exists(), isFalse);
      }
      expect(await verified.length(), 200000);
    },
  );

  test(
    'protected path policy is case insensitive for Windows destinations',
    () async {
      saver = ExamExportSaver(
        protectedDirectories: [Directory(runtime.path.toUpperCase())],
        isAuthorized: () => true,
      );
      await expectLater(
        saver.save(verified, p.join(runtime.path, 'new.xlsx')),
        throwsA(isA<CenterException>()),
      );
      expect(await File(p.join(runtime.path, 'new.xlsx')).exists(), isFalse);
    },
  );

  test(
    'a similarly named sibling directory is a valid export destination',
    () async {
      final sibling = await Directory('${runtime.path}-exports').create();
      final destination = await saver.save(
        verified,
        p.join(sibling.path, 'new'),
      );
      expect(await destination.readAsBytes(), await verified.readAsBytes());
    },
  );

  test('logout before creation leaves no destination', () async {
    saver = ExamExportSaver(
      protectedDirectories: [runtime],
      isAuthorized: () => false,
    );
    await expectLater(
      saver.save(verified, p.join(documents.path, 'new.xlsx')),
      throwsA(isA<CenterException>()),
    );
    expect(await documents.list().toList(), isEmpty);
  });

  test('logout while copying deletes the incomplete export', () async {
    final target = File(p.join(documents.path, 'new.xlsx'));
    saver = ExamExportSaver(
      protectedDirectories: [runtime],
      isAuthorized: () => !target.existsSync() || target.lengthSync() == 0,
    );
    await expectLater(
      saver.save(verified, target.path),
      throwsA(isA<CenterException>()),
    );
    expect(await target.exists(), isFalse);
    expect(await verified.length(), 200000);
  });

  test(
    'authorization is rechecked after the complete copy before returning',
    () async {
      final target = File(p.join(documents.path, 'new.xlsx'));
      final expectedBytes = await verified.length();
      var deniedCompletedCopy = false;
      saver = ExamExportSaver(
        protectedDirectories: [runtime],
        isAuthorized: () {
          if (target.existsSync() && target.lengthSync() == expectedBytes) {
            deniedCompletedCopy = true;
            return false;
          }
          return true;
        },
      );
      await expectLater(
        saver.save(verified, target.path),
        throwsA(isA<CenterException>()),
      );
      expect(deniedCompletedCopy, isTrue);
      expect(await target.exists(), isFalse);
    },
  );

  test(
    'changing verified source during copy deletes partial destination',
    () async {
      await verified.writeAsBytes(List.filled(4 * 1024 * 1024, 27));
      final target = File(p.join(documents.path, 'new.xlsx'));
      var truncated = false;
      saver = ExamExportSaver(
        protectedDirectories: [runtime],
        isAuthorized: () {
          if (!truncated && target.existsSync() && target.lengthSync() > 0) {
            truncated = true;
            verified.writeAsBytesSync([]);
          }
          return true;
        },
      );
      await expectLater(
        saver.save(verified, target.path),
        throwsA(isA<CenterException>()),
      );
      expect(truncated, isTrue);
      expect(await target.exists(), isFalse);
    },
  );

  test(
    'Windows device names and invalid paths are rejected before creating files',
    () async {
      for (final name in [
        'CON.xlsx',
        'COM1.xlsx',
        'x:y.xlsx',
        'new. ',
        'new.',
      ]) {
        await expectLater(
          saver.save(verified, p.join(documents.path, name)),
          throwsA(isA<CenterException>()),
          reason: name,
        );
      }
      expect(await documents.list().toList(), isEmpty);
    },
  );

  test(
    'directory symlinks cannot redirect exports into application data',
    () async {
      final link = await Link(
        p.join(documents.path, 'alias'),
      ).create(runtime.path);
      await expectLater(
        saver.save(verified, p.join(link.path, 'new.xlsx')),
        throwsA(isA<CenterException>()),
      );
      expect(await File(p.join(runtime.path, 'new.xlsx')).exists(), isFalse);
    },
    skip: Platform.isWindows
        ? 'Windows symlink creation needs privilege'
        : false,
  );

  test(
    'an existing destination symlink cannot overwrite its target',
    () async {
      final outside = await File(
        p.join(documents.path, 'preserved'),
      ).writeAsString('preserve this');
      final link = await Link(
        p.join(documents.path, 'new.xlsx'),
      ).create(outside.path);
      await expectLater(
        saver.save(verified, link.path),
        throwsA(isA<CenterException>()),
      );
      expect(await outside.readAsString(), 'preserve this');
      expect(await link.exists(), isTrue);
    },
    skip: Platform.isWindows
        ? 'Windows symlink creation needs privilege'
        : false,
  );
}
