import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/omada_profile_store.dart';

void main() {
  late Directory directory;
  late File file;
  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-omada-profile-');
    file = File('${directory.path}/profile.enc');
  });
  tearDown(() => directory.delete(recursive: true));

  test(
    'failed OS encryption preserves the previously saved connection',
    () async {
      final protector = _OperatingSystemProtectionBoundary();
      final store = OmadaProfileStore(file: file, protector: protector);
      await store.save({'clientSecret': 'old synthetic secret'});
      final previous = await file.readAsBytes();
      protector.fail = true;
      await expectLater(
        store.save({'clientSecret': 'new synthetic secret'}),
        throwsA(isA<CenterException>()),
      );
      expect(await file.readAsBytes(), previous);
      protector.fail = false;
      expect((await store.load())?['clientSecret'], 'old synthetic secret');
    },
  );

  test(
    'corrupt saved connection is rejected without returning its payload',
    () async {
      final store = OmadaProfileStore(
        file: file,
        protector: _OperatingSystemProtectionBoundary(),
      );
      for (final envelope in [
        'synthetic-secret',
        jsonEncode({'version': 2, 'protected': 'synthetic-secret'}),
        jsonEncode({'version': 1, 'protected': 'not base64'}),
        'x' * 65537,
      ]) {
        await file.writeAsString(envelope);
        await expectLater(
          store.load(),
          throwsA(
            isA<CenterException>().having(
              (exception) => exception.message,
              'safe explanation',
              isNot(contains('synthetic-secret')),
            ),
          ),
        );
      }
    },
  );

  test('forget removes saved credentials and allows fresh setup', () async {
    final store = OmadaProfileStore(
      file: file,
      protector: _OperatingSystemProtectionBoundary(),
    );
    expect(await store.load(), isNull);
    await store.save({'clientSecret': 'synthetic secret'});
    await store.forget();
    expect(await store.load(), isNull);
    expect(await File('${file.path}.tmp').exists(), isFalse);
  });

  test(
    'Windows DPAPI round trip never stores controller credentials in plaintext',
    () async {
      final arabicDirectory = Directory('${directory.path}/تجهيز الشبكات');
      await arabicDirectory.create();
      final encryptedFile = File('${arabicDirectory.path}/profile.enc');
      final store = OmadaProfileStore(
        file: encryptedFile,
        protector: WindowsOmadaProfileProtector(
          environment: {
            ...Platform.environment,
            'PSModulePath': r'C:\missing-pwsh-7\Modules',
          },
        ),
      );
      const profile = {
        'baseUri': 'https://127.0.0.1:8043',
        'clientSecret': 'synthetic private controller credential',
      };
      await store.save(profile);
      expect(await store.load(), profile);
      final updated = {...profile, 'staffName': 'MASSAR-STAFF'};
      await store.save(updated);
      expect(await store.load(), updated);
      expect(await File('${encryptedFile.path}.tmp').exists(), isFalse);
      final envelope = jsonDecode(await encryptedFile.readAsString()) as Map;
      final encrypted = base64Decode(envelope['protected'] as String);
      expect(
        utf8.decode(encrypted, allowMalformed: true),
        isNot(contains(profile['clientSecret']!)),
      );
    },
    skip: !Platform.isWindows,
  );
}

// DPAPI is an OS boundary; the persistence under test uses actual temp files.
class _OperatingSystemProtectionBoundary implements OmadaProfileProtector {
  bool fail = false;
  Uint8List? _plaintext;
  @override
  Future<Uint8List> protect(Uint8List plaintext) async {
    if (fail) throw const CenterException('OS protection unavailable');
    _plaintext = plaintext;
    return Uint8List.fromList([9, 8, 7]);
  }

  @override
  Future<Uint8List> unprotect(Uint8List ciphertext) async {
    if (_plaintext == null) throw const FormatException();
    return _plaintext!;
  }
}
