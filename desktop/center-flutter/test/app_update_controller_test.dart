import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/cloud/app_update_controller.dart';
import 'package:massar_center/cloud/cloud_support_settings.dart';
import 'package:path/path.dart' as path;

import 'helpers/cloud_http_fake.dart';

const _installedBuild = '1111111111111111';
const _releaseBuild = '2222222222222222';
final _archiveBytes = <int>[0x50, 0x4b, 0x05, 0x06, ...List.filled(18, 0)];
final _configuration = CloudSupportConfiguration(
  origin: Uri.parse('https://updates.example.invalid'),
  centerId: 'synthetic-center',
  deviceToken: 'synthetic-token-1234567890',
);

/// Compile/environment metadata is the other external boundary. Ordinary app
/// instances retain the actual AppBuildMetadata/ABI defaults.
class _ReleaseUpdateController extends AppUpdateController {
  _ReleaseUpdateController({
    required super.directory,
    required super.configuration,
    required super.httpClientFactory,
    this.targetRole = 'host',
    this.currentVersion = '1.0.0+1',
  });
  final String targetRole, currentVersion;
  @override
  String get installedVersion => currentVersion;
  @override
  String get installedBuild => _installedBuild;
  @override
  String get platform => 'macos-arm64';
  @override
  String get role => targetRole;
}

Map<String, dynamic> _manifest({String role = 'host'}) => {
  'releaseId': 'synthetic-release-2',
  'version': '1.1.0+2',
  'build': _releaseBuild,
  'platform': 'macos-arm64',
  'role': role,
  'size': _archiveBytes.length,
  'sha256': sha256.convert(_archiveBytes).toString(),
  'downloadPath': '/v1/releases/synthetic-update.zip',
  'notes': 'Synthetic release',
};

void main() {
  late Directory sandbox;
  final controllers = <AppUpdateController>[];
  setUp(() async {
    sandbox = await Directory.systemTemp.createTemp('massar-updates-test-');
  });
  tearDown(() async {
    for (final controller in controllers) {
      await controller.close();
      controller.dispose();
    }
    controllers.clear();
    await sandbox.delete(recursive: true);
  });

  AppUpdateController create(
    ScriptedCloudHttp network, {
    String role = 'host',
    String version = '1.0.0+1',
    Future<CloudSupportConfiguration?> Function()? configuration,
  }) {
    final controller = _ReleaseUpdateController(
      directory: sandbox,
      configuration: configuration ?? () async => _configuration,
      httpClientFactory: network.createClient,
      targetRole: role,
      currentVersion: version,
    );
    controllers.add(controller);
    return controller;
  }

  Future<List<FileSystemEntity>> files() =>
      sandbox.list(recursive: true).toList();

  test(
    'host and client packages download at the installed version and remain separately transferable offline',
    () async {
      final network = ScriptedCloudHttp((request) {
        if (request.uri.path.startsWith('/v1/updates/')) {
          return CloudHttpReply.json(
            200,
            _manifest(role: request.uri.pathSegments.last),
          );
        }
        return CloudHttpReply(200, _archiveBytes);
      });
      final parent = create(network, version: '1.1.0+2');
      final downloads = [
        for (final role in AppPackageRole.values)
          parent.createPackageDownloader(role),
      ];
      controllers.addAll(downloads);
      await Future.wait(downloads.map((download) => download.checkNow()));
      expect(
        downloads.map((download) => download.downloadedPath).toSet(),
        hasLength(2),
      );
      expect(parent.role, 'host');
      expect(parent.downloadedPath, isNull);
      for (final download in downloads) {
        expect(download.status, AppUpdateStatus.downloaded);
        expect(
          await File(download.downloadedPath!).readAsBytes(),
          _archiveBytes,
        );
      }
      await Future.wait(downloads.map((download) => download.checkNow()));
      expect(
        network.requests.where((r) => r.uri.path.startsWith('/v1/releases/')),
        hasLength(2),
      );
      final offline = create(
        ScriptedCloudHttp((_) => throw const SocketException('offline')),
        version: '1.1.0+2',
      );
      for (final role in AppPackageRole.values) {
        final restored = offline.createPackageDownloader(role);
        controllers.add(restored);
        await restored.checkNow();
        expect(restored.status, AppUpdateStatus.downloaded);
        expect(
          restored.suggestedFilename,
          'massar-${role.name}-macos-arm64-1.1.0+2.zip',
        );
        final destination = path.join(
          sandbox.path,
          restored.suggestedFilename!,
        );
        await restored.saveDownloadedTo(destination);
        expect(await File(destination).readAsBytes(), _archiveBytes);
      }
    },
  );

  for (final failure in ['wrong role', 'wrong digest', 'unpublished']) {
    test(
      'client $failure does not hide a successfully downloaded host package',
      () async {
        final network = ScriptedCloudHttp((request) {
          if (request.uri.path.endsWith('/client')) {
            if (failure == 'unpublished') return CloudHttpReply(204, []);
            return CloudHttpReply.json(200, {
              ..._manifest(role: failure == 'wrong role' ? 'host' : 'client'),
              if (failure == 'wrong digest')
                'sha256': List.filled(64, 'a').join(),
            });
          }
          if (request.uri.path.startsWith('/v1/updates/')) {
            return CloudHttpReply.json(200, _manifest());
          }
          return CloudHttpReply(200, _archiveBytes);
        });
        final parent = create(network);
        final host = parent.createPackageDownloader(AppPackageRole.host);
        final client = parent.createPackageDownloader(AppPackageRole.client);
        controllers.addAll([host, client]);
        await Future.wait([host.checkNow(), client.checkNow()]);
        expect(host.status, AppUpdateStatus.downloaded);
        expect(
          client.status,
          failure == 'unpublished'
              ? AppUpdateStatus.upToDate
              : AppUpdateStatus.error,
        );
        expect(client.downloadedPath, isNull);
        expect(await File(host.downloadedPath!).readAsBytes(), _archiveBytes);
      },
    );
  }

  test(
    'export rechecks the archive and preserves an existing destination when cache is corrupted',
    () async {
      final parent = create(
        ScriptedCloudHttp(
          (request) => request.uri.path.startsWith('/v1/updates/')
              ? CloudHttpReply.json(200, _manifest(role: 'client'))
              : CloudHttpReply(200, _archiveBytes),
        ),
      );
      final download = parent.createPackageDownloader(AppPackageRole.client);
      controllers.add(download);
      await download.checkNow();
      await File(
        download.downloadedPath!,
      ).writeAsBytes([..._archiveBytes.take(21), 1]);
      final destination = File(path.join(sandbox.path, 'existing.zip'));
      await destination.writeAsBytes([1, 2, 3]);
      await expectLater(
        download.saveDownloadedTo(destination.path),
        throwsFormatException,
      );
      expect(await destination.readAsBytes(), [1, 2, 3]);
    },
  );

  test(
    'proxy decompressed archive is checked by actual bytes and SHA, not compressed length',
    () async {
      final network = ScriptedCloudHttp((request) {
        expect(
          request.headers.value(HttpHeaders.acceptEncodingHeader),
          'identity',
        );
        return request.uri.path.startsWith('/v1/updates/')
            ? CloudHttpReply.json(200, _manifest())
            : CloudHttpReply(
                200,
                _archiveBytes,
                contentLength: 12,
                compressionState:
                    HttpClientResponseCompressionState.decompressed,
              );
      });
      final controller = create(network);
      await controller.initialize();
      expect(controller.status, AppUpdateStatus.downloaded);
      expect(
        await File(controller.downloadedPath!).readAsBytes(),
        _archiveBytes,
      );
    },
  );

  for (final role in ['host', 'client']) {
    test(
      'verified $role archive downloads only its role and is preserved across restart',
      () async {
        final manifest = _manifest(role: role);
        final network = ScriptedCloudHttp(
          (request) => request.uri.path.startsWith('/v1/updates/')
              ? CloudHttpReply.json(200, manifest)
              : CloudHttpReply(200, _archiveBytes),
        );
        final controller = create(network, role: role);
        await controller.checkNow();
        expect(controller.status, AppUpdateStatus.downloaded);
        expect(controller.statusLabel, contains('لم يتم تثبيته'));
        expect(controller.availableVersion, '1.1.0+2');
        expect(controller.progress, 1);
        expect(
          await File(controller.downloadedPath!).readAsBytes(),
          _archiveBytes,
        );
        expect(
          network.requests.first.uri.path,
          '/v1/updates/macos-arm64/$role',
        );
        expect(
          network.requests.every((request) => !request.followRedirects),
          isTrue,
        );
        expect(
          network.requests.every((request) => request.uri.scheme == 'https'),
          isTrue,
        );
        expect(
          network.requests.every(
            (request) =>
                request.headers.value(HttpHeaders.authorizationHeader) ==
                'Bearer ${_configuration.deviceToken}',
          ),
          isTrue,
        );
        final receipt =
            jsonDecode(
                  await File(
                    path.join(sandbox.path, 'downloaded-update.json'),
                  ).readAsString(),
                )
                as Map;
        expect(receipt['role'], role);
        expect(receipt['sha256'], sha256.convert(_archiveBytes).toString());
        expect(receipt.values, isNot(contains(_configuration.deviceToken)));
        final archivePath = controller.downloadedPath;
        await controller.close();
        final offline = ScriptedCloudHttp(
          (_) => throw const SocketException('offline'),
        );
        final restored = create(offline, role: role);
        await restored.checkNow();
        expect(restored.status, AppUpdateStatus.downloaded);
        expect(restored.downloadedPath, archivePath);
        expect(await File(archivePath!).readAsBytes(), _archiveBytes);
        expect(
          (await files()).where(
            (entry) => path.basename(entry.path).startsWith('.massar-update-'),
          ),
          isEmpty,
        );
      },
    );
  }

  final rejectedManifests = <String, Map<String, dynamic>>{
    'wrong role': {..._manifest(), 'role': 'client'},
    'wrong platform': {..._manifest(), 'platform': 'windows-x64'},
    'same version different build': {..._manifest(), 'version': '1.0.0+1'},
    'external download origin': {
      ..._manifest(),
      'downloadPath': 'https://other.example.invalid/archive.zip',
    },
    'path traversal': {
      ..._manifest(),
      'downloadPath': '/v1/releases/../archive.zip',
    },
    'malformed digest': {..._manifest(), 'sha256': 'not-a-digest'},
    'invalid release version': {..._manifest(), 'version': 'tomorrow'},
    'oversized archive': {..._manifest(), 'size': 2147483649},
  };
  for (final scenario in rejectedManifests.entries) {
    test('${scenario.key} cannot produce a downloadable update', () async {
      final network = ScriptedCloudHttp(
        (_) => CloudHttpReply.json(200, scenario.value),
      );
      final controller = create(network);
      await controller.checkNow();
      expect(controller.status, AppUpdateStatus.error);
      expect(controller.downloadedPath, isNull);
      expect(network.requests, hasLength(1));
      expect(await files(), isEmpty);
    });
  }

  final noNewVersions = <String, Map<String, dynamic>>{
    'old release': {..._manifest(), 'version': '0.9.0+999'},
    'same installed release': {
      ..._manifest(),
      'version': '1.0.0+1',
      'build': _installedBuild,
    },
    'prerelease of installed release': {
      ..._manifest(),
      'version': '1.0.0-beta.2+9',
    },
  };
  for (final scenario in noNewVersions.entries) {
    test('${scenario.key} is not downloaded or installed', () async {
      final network = ScriptedCloudHttp(
        (_) => CloudHttpReply.json(200, scenario.value),
      );
      final controller = create(network);
      await controller.checkNow();
      expect(controller.status, AppUpdateStatus.upToDate);
      expect(controller.downloadedPath, isNull);
      expect(network.requests, hasLength(1));
      expect(await files(), isEmpty);
    });
  }

  final badArchives = <String, CloudHttpReply>{
    'hash mismatch': CloudHttpReply(200, [..._archiveBytes.take(21), 1]),
    'truncated content': CloudHttpReply(200, _archiveBytes.take(10).toList()),
    'unknown length oversized body': CloudHttpReply(200, [
      ..._archiveBytes,
      1,
    ], contentLength: -1),
    'redirect download': CloudHttpReply(302, []),
  };
  for (final scenario in badArchives.entries) {
    test(
      '${scenario.key} removes partial staging without publishing an archive',
      () async {
        final network = ScriptedCloudHttp(
          (request) => request.uri.path.startsWith('/v1/updates/')
              ? CloudHttpReply.json(200, _manifest())
              : scenario.value,
        );
        final controller = create(network);
        await controller.checkNow();
        expect(controller.status, AppUpdateStatus.error);
        expect(controller.downloadedPath, isNull);
        expect(await files(), isEmpty);
      },
    );
  }

  test(
    'credentials changed during download prevent publication of the old response',
    () async {
      var configuration = _configuration;
      final network = ScriptedCloudHttp((request) {
        if (request.uri.path.startsWith('/v1/updates/')) {
          return CloudHttpReply.json(200, _manifest());
        }
        configuration = CloudSupportConfiguration(
          origin: _configuration.origin,
          centerId: _configuration.centerId,
          deviceToken: 'rotated-synthetic-token-123456',
        );
        return CloudHttpReply(200, _archiveBytes);
      });
      final controller = create(
        network,
        configuration: () async => configuration,
      );
      await controller.checkNow();
      expect(controller.status, AppUpdateStatus.error);
      expect(controller.downloadedPath, isNull);
      expect(await files(), isEmpty);
    },
  );

  test(
    'tampered persisted archive is not offered after an offline restart',
    () async {
      final network = ScriptedCloudHttp(
        (request) => request.uri.path.startsWith('/v1/updates/')
            ? CloudHttpReply.json(200, _manifest())
            : CloudHttpReply(200, _archiveBytes),
      );
      final controller = create(network);
      await controller.checkNow();
      final archive = File(controller.downloadedPath!);
      await controller.close();
      await archive.writeAsBytes([..._archiveBytes.take(21), 1]);
      final restored = create(
        ScriptedCloudHttp((_) => throw const SocketException('offline')),
      );
      await restored.checkNow();
      expect(restored.status, AppUpdateStatus.offline);
      expect(restored.downloadedPath, isNull);
      expect(await archive.readAsBytes(), isNot(_archiveBytes));
    },
  );

  test(
    'concurrent checks publish one verified archive and one receipt',
    () async {
      final started = Completer<void>();
      final manifestReply = Completer<CloudHttpReply>();
      final network = ScriptedCloudHttp((request) {
        if (request.uri.path.startsWith('/v1/updates/')) {
          started.complete();
          return manifestReply.future;
        }
        return CloudHttpReply(200, _archiveBytes);
      });
      final controller = create(network);
      final first = controller.checkNow(), second = controller.checkNow();
      await started.future;
      manifestReply.complete(CloudHttpReply.json(200, _manifest()));
      await Future.wait([first, second]);
      expect(network.requests, hasLength(2));
      expect(controller.status, AppUpdateStatus.downloaded);
      expect(
        (await files()).where((entry) => entry.path.endsWith('.zip')),
        hasLength(1),
      );
      expect(
        await File(controller.downloadedPath!).readAsBytes(),
        _archiveBytes,
      );
    },
  );

  for (final status in [204, 401, 302]) {
    test(
      'manifest HTTP $status never follows a redirect or downloads an archive',
      () async {
        final network = ScriptedCloudHttp((_) => CloudHttpReply(status, []));
        final controller = create(network);
        await controller.checkNow();
        expect(controller.status, switch (status) {
          204 => AppUpdateStatus.upToDate,
          401 => AppUpdateStatus.unauthorized,
          _ => AppUpdateStatus.error,
        });
        expect(network.requests, hasLength(1));
        expect(network.requests.single.followRedirects, isFalse);
        expect(await files(), isEmpty);
      },
    );
  }

  test('development build cannot claim a newer comparable release', () async {
    final network = ScriptedCloudHttp(
      (_) => CloudHttpReply.json(200, _manifest()),
    );
    final controller = create(network, version: 'development');
    await controller.checkNow();
    expect(controller.status, AppUpdateStatus.error);
    expect(controller.downloadedPath, isNull);
    expect(network.requests, hasLength(1));
  });
}
