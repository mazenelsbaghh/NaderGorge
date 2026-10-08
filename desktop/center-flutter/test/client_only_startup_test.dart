import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:cryptography/cryptography.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/admin_configuration.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/auth/auth_screen.dart';
import 'package:massar_center/features/management/lan_settings_page.dart';
import 'package:massar_center/lan/lan_controller.dart';
import 'package:massar_center/lan/lan_host_process.dart';
import 'package:massar_center/lan/lan_settings.dart';
import 'package:massar_center/lan/lan_transport.dart';
import 'package:massar_center/main.dart';
import 'helpers/synthetic_installer_assets.dart';

class _TestHostProcess extends LanHostProcess {
  _TestHostProcess(String executable) : super(executablePath: executable);
  @override
  Future<LanHostReady> start({
    required String dataDirectory,
    required String upstreamUrl,
    required String upstreamSecret,
    required String name,
    int port = 43873,
    int discoveryPort = 43874,
  }) => super.start(
    dataDirectory: dataDirectory,
    upstreamUrl: upstreamUrl,
    upstreamSecret: upstreamSecret,
    name: name,
    port: 0,
    discoveryPort: 0,
  );
}

class _RealHttpOverrides extends HttpOverrides {}

void main() {
  final binding = TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'temporary-client-lifecycle-password';
  late Directory buildDirectory;
  late String binary;
  late InstallationAdmin owner;
  late Directory directory;
  final stores = <CenterStore>[];
  final controllers = <LanController>[];

  setUpAll(() async {
    buildDirectory = await Directory.systemTemp.createTemp('massar-client-go-');
    binary =
        '${buildDirectory.path}/massar-lan-host${Platform.isWindows ? '.exe' : ''}';
    final built = await Process.run('go', [
      'build',
      '-o',
      binary,
      '.',
    ], workingDirectory: '../center-lan');
    expect(
      built.exitCode,
      0,
      reason: 'Actual gateway build must succeed: ${built.stderr}',
    );
    final salt = List<int>.generate(24, (i) => i + 1);
    final key = await Pbkdf2(
      macAlgorithm: Hmac.sha256(),
      iterations: 120000,
      bits: 256,
    ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
    owner = InstallationAdmin(
      id: 'test-installation-owner',
      name: 'temporary-owner',
      credential: {
        'algorithm': 'pbkdf2-sha256-120000',
        'salt': base64Encode(salt),
        'hash': base64Encode(await key.extractBytes()),
      },
    );
  });
  tearDownAll(() => buildDirectory.delete(recursive: true));
  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-client-only-');
    stores.clear();
    controllers.clear();
  });
  tearDown(() async {
    for (final controller in controllers.reversed) {
      await controller.close();
      controller.dispose();
    }
    for (final store in stores.reversed) {
      await store.close();
    }
    await directory.delete(recursive: true);
  });
  Future<T> real<T>(Future<T> Function() work) =>
      HttpOverrides.runWithHttpOverrides(work, _RealHttpOverrides());
  Future<CenterStore> client(String path) async {
    final store = await openInstalledCenter(directory: path, clientOnly: true);
    stores.add(store);
    return store;
  }

  LanController controller(
    CenterStore store, {
    bool clientOnly = true,
    Future<List<LanEndpoint>> Function()? discover,
  }) {
    final result = LanController(
      localStore: store,
      clientOnly: clientOnly,
      hostProcess: _TestHostProcess(binary),
      searchHosts: discover ?? (() async => []),
    );
    controllers.add(result);
    return result;
  }

  Future<void> expectNoDatabaseOrCredentials(String path) async {
    final folder = Directory(path);
    final files = await folder.exists()
        ? await folder.list(recursive: true).toList()
        : <FileSystemEntity>[];
    final names = files
        .map((e) => e.path.split(Platform.pathSeparator).last)
        .toList();
    expect(names.where((e) => e.startsWith('center.sqlite')), isEmpty);
    expect(
      names.where(
        (e) =>
            e.contains('credential') ||
            e == 'admin_account.json' ||
            e.contains('snapshot') ||
            e.endsWith('.db'),
      ),
      isEmpty,
    );
    for (final file in files.whereType<File>()) {
      final data = await file.readAsString();
      expect(data, isNot(contains(password)));
      expect(data, isNot(contains(owner.credential['hash'])));
      expect(data, isNot(contains('PRIVATE-STUDENT-NAME')));
    }
  }

  test(
    'unpaired client rejects local credentials, data and backups without creating a SQLite file',
    () async {
      final path = '${directory.path}/secondary';
      final store = await client(path);
      expect(store.isClientWorkspace, isTrue);
      expect(store.hasStaff, isTrue);
      expect(store.staff, isEmpty);
      expect(store.installationAdminName, isNull);
      await expectLater(
        store.setupAdmin('local-admin', password),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.signIn('local-admin', password),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.saveCatalog(
          const CatalogEntry(name: 'local-data', kind: CatalogKind.subject),
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(store.createBackup(), throwsA(isA<CenterException>()));
      await expectLater(
        store.exportReport('$path/export.csv'),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.restoreBackup('${directory.path}/missing.json'),
        throwsA(isA<CenterException>()),
      );
      expect(store.currentUser, isNull);
      expect(store.catalogs, isEmpty);
      expect(store.audit, isEmpty);
      await expectNoDatabaseOrCredentials(path);
    },
  );

  test(
    'client-only mode never opens or modifies legacy SQLite even when old settings requested host startup',
    () async {
      final path = '${directory.path}/legacy-secondary';
      await Directory(path).create();
      final originals = <String, List<int>>{};
      for (final suffix in ['', '-wal', '-shm', '.lock']) {
        final name = '$path/center.sqlite$suffix';
        final content = utf8.encode('legacy SQLite sentinel $suffix');
        await File(name).writeAsBytes(content, flush: true);
        originals[name] = content;
      }
      final settings = LanSettings(Directory(path));
      final old = await settings.read();
      await settings.save(old.copyWith(mode: LanMode.host));
      final store = await client(path);
      final lan = controller(store);
      await lan.initialize();
      expect(lan.isClientOnly, isTrue);
      expect(lan.hostReady, isNull);
      expect(lan.activeStore, same(store));
      expect(lan.configuration!.deviceId, old.deviceId);
      final metadata = await File(settings.filePath).readAsString();
      for (final command in [
        lan.startHost(),
        lan.stopHost(),
        lan.useStandalone(),
        lan.renewPairingCode(),
        lan.refreshDevices(),
        lan.revokeDevice('fake-id'),
      ]) {
        await expectLater(command, throwsA(isA<CenterException>()));
      }
      expect(await File(settings.filePath).readAsString(), metadata);
      for (final entry in originals.entries) {
        expect(await File(entry.key).readAsBytes(), entry.value);
      }
      expect(
        await Directory(path).list().map((e) => e.path).toList(),
        unorderedEquals([...originals.keys, settings.filePath]),
      );
      final pending = File('$path/lan-pending-command.json');
      const receipt = '{"requestId":"unresolved-temporary-receipt"}';
      await pending.writeAsString(receipt, flush: true);
      await expectLater(
        lan.pairHost(
          LanEndpoint(
            hostId: 'another-host',
            name: 'another-host',
            address: '127.0.0.1',
            port: 1,
            certificateSha256: 'a' * 64,
          ),
          '123456',
        ),
        throwsA(isA<CenterException>()),
      );
      expect(await pending.readAsString(), receipt);
      expect(await File(settings.filePath).readAsString(), metadata);
    },
  );

  test(
    'paired secondary holds host data only in memory, reopens offline and reconnects saved identity without pairing again',
    () => real(() async {
      // This LAN scenario needs an isolated empty host, not the private first-install seed.
      final hostStore = await CenterStore.open(
        directory: '${directory.path}/host',
      );
      await hostStore.ensureInstallationAdmin(owner);
      stores.add(hostStore);
      await hostStore.signIn(owner.name, password);
      for (final kind in CatalogKind.values) {
        await hostStore.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await hostStore.saveGroup(
        StudyGroup(
          name: 'host-group',
          subjectId: hostStore.catalogs[0].id,
          centerId: hostStore.catalogs[1].id,
          gradeId: hostStore.catalogs[2].id,
          sessionPrice: 10000,
          packagePrice: 40000,
        ),
      );
      final group = hostStore.groups.single;
      final student = await hostStore.registerStudent(
        Student(
          name: 'PRIVATE-STUDENT-NAME',
          groupIds: [group.id],
          createdAt: DateTime.now().subtract(const Duration(days: 1)),
        ),
      );
      await hostStore.saveSession(
        LessonSession(
          groupId: group.id,
          number: 1,
          startsAt: DateTime.now().add(const Duration(hours: 1)),
          createdAt: DateTime.now(),
        ),
      );
      final host = controller(hostStore, clientOnly: false);
      await host.initialize();
      await host.startHost();
      final endpoint = host.hostReady!.endpoint;
      final path = '${directory.path}/secondary';
      final placeholder = await client(path);
      final first = controller(placeholder);
      await first.initialize();
      final deviceId = first.configuration!.deviceId;
      await first.pairHost(endpoint, host.pairingCode!);
      final token = first.configuration!.deviceToken;
      expect(first.activeStore.isRemote, isTrue);
      expect(first.activeStore.currentUser, isNull);
      expect(first.activeStore.students, isEmpty);
      await first.activeStore.signIn(owner.name, password);
      expect(first.activeStore.students.single.id, student.id);
      await first.activeStore.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: hostStore.sessions.single.id,
          mode: EntryMode.single,
        ),
      );
      expect(hostStore.payments.single.netAmount, 10000);
      await expectNoDatabaseOrCredentials(path);
      await first.close();
      await host.stopHost();
      final restoredWorkspace = await client(path);
      final reopened = controller(
        restoredWorkspace,
        discover: () async =>
            host.hostReady == null ? [] : [host.hostReady!.endpoint],
      );
      await reopened.initialize();
      expect(reopened.activeStore.isRemote, isTrue);
      expect(reopened.activeStore.remoteConnected, isFalse);
      expect(reopened.activeStore.currentUser, isNull);
      expect(reopened.activeStore.students, isEmpty);
      expect(reopened.configuration!.deviceId, deviceId);
      expect(reopened.configuration!.deviceToken, token);
      await expectLater(
        () => reopened.activeStore.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: hostStore.sessions.single.id,
            mode: EntryMode.single,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(hostStore.payments, hasLength(1));
      await expectNoDatabaseOrCredentials(path);
      final automaticRecovery = Completer<void>();
      void observeConnection() {
        if (!automaticRecovery.isCompleted &&
            reopened.status == LanConnectionStatus.connected &&
            reopened.activeStore.remoteConnected) {
          automaticRecovery.complete();
        }
      }

      reopened.addListener(observeConnection);
      try {
        await host.startHost();
        await automaticRecovery.future.timeout(const Duration(seconds: 12));
      } finally {
        reopened.removeListener(observeConnection);
      }
      expect(reopened.activeStore.remoteConnected, isTrue);
      expect(reopened.configuration!.endpoint!.hostId, endpoint.hostId);
      expect(
        reopened.configuration!.endpoint!.certificateSha256,
        endpoint.certificateSha256,
      );
      expect(reopened.configuration!.deviceToken, token);
      await host.refreshDevices();
      expect(
        host.pairedDevices.where((e) => e['revoked'] != true),
        hasLength(1),
      );
      await reopened.activeStore.signIn(owner.name, password);
      expect(reopened.activeStore.students.single.id, student.id);
      expect(reopened.activeStore.payments.single.netAmount, 10000);
      await expectNoDatabaseOrCredentials(path);
    }),
  );

  test(
    'principal startup still installs and authenticates its permanent administrator in SQLite',
    () async {
      installSyntheticInstallerAssets(owner);
      final path = '${directory.path}/principal';
      final store = await openInstalledCenter(directory: path, admin: owner);
      stores.add(store);
      expect(store.isClientWorkspace, isFalse);
      expect(await File(store.databasePath).exists(), isTrue);
      expect(store.installationAdminName, owner.name);
      expect(store.staff.single.id, owner.id);
      expect(store.staff.single.role, StaffRole.admin);
      await store.signIn(owner.name, password);
      expect(store.canManage, isTrue);
    },
  );

  testWidgets(
    'dedicated client bootstrap opens pairing directly and never requests the missing installation credential asset',
    (tester) async {
      const provider = MethodChannel('plugins.flutter.io/path_provider');
      var credentialAssetReads = 0;
      final support = '${directory.path}/support';
      binding.defaultBinaryMessenger.setMockMethodCallHandler(provider, (
        call,
      ) async {
        expect(call.method, 'getApplicationSupportDirectory');
        return support;
      });
      binding.defaultBinaryMessenger.setMockMessageHandler('flutter/assets', (
        message,
      ) async {
        if (message != null &&
            utf8.decode(
                  message.buffer.asUint8List(
                    message.offsetInBytes,
                    message.lengthInBytes,
                  ),
                ) ==
                'assets/admin_account.json') {
          credentialAssetReads++;
        }
        return null; // The secondary bundle deliberately has no admin asset.
      });
      LanController? lan;
      CenterStore? workspace;
      try {
        await tester.binding.setSurfaceSize(const Size(1440, 900));
        await tester.pumpWidget(const CenterBootstrap(clientOnly: true));
        await tester.runAsync(() async {
          for (var attempt = 0; attempt < 150; attempt++) {
            await Future<void>.delayed(const Duration(milliseconds: 20));
            await tester.pump(const Duration(milliseconds: 20));
            if (find
                .byKey(const Key('client-first-pairing'))
                .evaluate()
                .isNotEmpty) {
              break;
            }
          }
        });
        expect(find.byKey(const Key('client-first-pairing')), findsOneWidget);
        expect(find.byType(AuthScreen), findsNothing);
        workspace = tester.widget<CenterApp>(find.byType(CenterApp)).store;
        lan = tester
            .widget<LanSettingsPage>(find.byType(LanSettingsPage))
            .controller;
        await tester.runAsync(() async {
          for (
            var attempt = 0;
            attempt < 100 && lan!.configuration == null;
            attempt++
          ) {
            await Future<void>.delayed(const Duration(milliseconds: 20));
            await tester.pump(const Duration(milliseconds: 20));
          }
        });
        expect(lan.configuration, isNotNull);
        expect(lan.isClientOnly, isTrue);
        expect(workspace.isClientWorkspace, isTrue);
        expect(workspace.staff, isEmpty);
        expect(workspace.installationAdminName, isNull);
        expect(credentialAssetReads, 0);
        await tester.runAsync(
          () => expectNoDatabaseOrCredentials('$support/massar-center'),
        );
        expect(tester.takeException(), isNull);
      } finally {
        await tester.pumpWidget(const SizedBox());
        await tester.runAsync(() async {
          await lan?.close();
          await workspace?.close();
        });
        binding.defaultBinaryMessenger.setMockMethodCallHandler(provider, null);
        binding.defaultBinaryMessenger.setMockMessageHandler(
          'flutter/assets',
          null,
        );
        await tester.binding.setSurfaceSize(null);
      }
    },
  );
}
