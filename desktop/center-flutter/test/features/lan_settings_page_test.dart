import 'dart:async';
import 'dart:convert';
import 'dart:ui' as ui;
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/lan_settings_page.dart';
import 'package:massar_center/lan/lan_controller.dart';
import 'package:massar_center/lan/lan_host_process.dart';
import 'package:massar_center/lan/lan_settings.dart';
import 'package:massar_center/lan/lan_transport.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/problem_log.dart';

// Only the OS-assigned test ports differ; the actual Go process, TLS, bridge,
// pairing, store authorization and SQLite persistence run unchanged.
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

// Flutter's default widget-test HTTP override returns 400. This override uses
// dart:io's real client, so the tests still exercise actual loopback TLS.
class _RealHttpOverrides extends HttpOverrides {}

void main() {
  final binding = TestWidgetsFlutterBinding.ensureInitialized();
  late Directory buildDirectory, directory;
  late String binary;
  late CenterStore hostStore, localStore;
  late LanController host, client;
  List<LanEndpoint> discovered = [];
  Object? discoveryFailure;
  Completer<List<LanEndpoint>>? discoveryGate;
  var searches = 0;
  LanController? secondHost;
  CenterStore? secondHostStore;
  final boundary = GlobalKey();

  setUpAll(
    () => binding.runAsync(() async {
      buildDirectory = await Directory.systemTemp.createTemp(
        'massar-lan-test-build-',
      );
      binary =
          '${buildDirectory.path}/massar-lan-host${Platform.isWindows ? '.exe' : ''}';
      final compiled = await Process.run('go', [
        'build',
        '-o',
        binary,
        '.',
      ], workingDirectory: '../center-lan');
      expect(
        compiled.exitCode,
        0,
        reason: 'The real local gateway must build: ${compiled.stderr}',
      );
    }),
  );

  tearDownAll(
    () => binding.runAsync(() => buildDirectory.delete(recursive: true)),
  );

  Future<void> setUpFixture({bool clientOnly = false}) async {
    directory = await Directory.systemTemp.createTemp('massar-lan-controller-');
    hostStore = await CenterStore.open(directory: '${directory.path}/host');
    localStore = clientOnly
        ? CenterStore.clientWorkspace(directory: '${directory.path}/secondary')
        : await CenterStore.open(directory: '${directory.path}/secondary');
    await hostStore.setupAdmin('host-owner', 'host-password-123');
    if (!clientOnly) {
      await localStore.setupAdmin('local-owner', 'local-password-123');
    }
    for (final store in clientOnly ? [hostStore] : [hostStore, localStore]) {
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة الاختبار',
          subjectId: store.catalogs[0].id,
          centerId: store.catalogs[1].id,
          gradeId: store.catalogs[2].id,
          sessionPrice: 10000,
          packagePrice: 35000,
        ),
      );
    }
    await hostStore.saveStudent(
      Student(
        name: 'بيانات الجهاز الرئيسي',
        code: 'HOST',
        groupIds: [hostStore.groups.single.id],
        createdAt: DateTime.now(),
      ),
    );
    if (!clientOnly) {
      await localStore.saveStudent(
        Student(
          name: 'بيانات الجهاز الثانوي',
          code: 'LOCAL',
          groupIds: [localStore.groups.single.id],
          createdAt: DateTime.now(),
        ),
      );
    }
    discovered = [];
    discoveryFailure = null;
    discoveryGate = null;
    searches = 0;
    secondHost = null;
    secondHostStore = null;
    host = LanController(
      localStore: hostStore,
      hostProcess: _TestHostProcess(binary),
      searchHosts: () async => [],
    );
    client = LanController(
      clientOnly: clientOnly,
      localStore: localStore,
      hostProcess: _TestHostProcess(binary),
      searchHosts: () async {
        searches++;
        if (discoveryGate != null) return discoveryGate!.future;
        if (discoveryFailure != null) throw discoveryFailure!;
        return discovered;
      },
    );
    await host.initialize();
    await client.initialize();
  }

  Future<void> disposeFixture(WidgetTester tester) async {
    if (discoveryGate != null && !discoveryGate!.isCompleted) {
      discoveryGate!.complete(const []);
    }
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 400));
    // Drain fake-zone continuations while real process/filesystem I/O closes.
    var finished = false;
    Object? failure;
    StackTrace? failureStack;
    await binding.runAsync(() async {
      unawaited(() async {
        try {
          await ProblemLog.current?.flush();
          ProblemLog.current = null;
          await secondHost?.close();
          secondHost?.dispose();
          await secondHostStore?.close();
          await client.close();
          client.dispose();
          await host.close();
          host.dispose();
          await localStore.close();
          await hostStore.close();
          await directory.delete(recursive: true);
        } catch (error, stack) {
          failure = error;
          failureStack = stack;
        } finally {
          finished = true;
        }
      }());
    });
    for (var attempt = 0; attempt < 750 && !finished; attempt++) {
      await binding.runAsync(
        () => Future<void>.delayed(const Duration(milliseconds: 20)),
      );
      await tester.pump(const Duration(milliseconds: 50));
    }
    expect(
      finished,
      isTrue,
      reason: 'LAN resources must close within 15 seconds.',
    );
    if (failure != null) Error.throwWithStackTrace(failure!, failureStack!);
  }

  void testLanWidgets(
    String description,
    Future<void> Function(WidgetTester) body,
  ) {
    testWidgets(description, (tester) async {
      try {
        await body(tester);
      } finally {
        await disposeFixture(tester);
      }
    });
  }

  Future<void> waitFor(bool Function() condition) async {
    final limit = DateTime.now().add(const Duration(seconds: 8));
    while (!condition()) {
      if (DateTime.now().isAfter(limit)) {
        throw TimeoutException('LAN fixture condition was not reached');
      }
      await Future<void>.delayed(const Duration(milliseconds: 20));
    }
  }

  Future<T?> realAsync<T>(WidgetTester tester, Future<T> Function() action) {
    return tester.runAsync(
      () => HttpOverrides.runWithHttpOverrides(action, _RealHttpOverrides()),
    );
  }

  Future<void> waitUi(WidgetTester tester, bool Function() condition) async {
    for (var attempt = 0; attempt < 400; attempt++) {
      await realAsync(
        tester,
        () => Future<void>.delayed(const Duration(milliseconds: 20)),
      );
      await tester.pump(const Duration(milliseconds: 50));
      if (condition()) return;
    }
    expect(
      condition(),
      isTrue,
      reason: 'Real gateway UI operation must finish.',
    );
  }

  Future<void> settleRoutes(WidgetTester tester) async {
    // User-input modals intentionally keep the page busy; their progress
    // indicator cannot settle. Pump the finite route transition instead.
    for (var frame = 0; frame < 4; frame++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  Future<void> open(
    WidgetTester tester,
    LanController controller, {
    Size size = const Size(1100, 900),
    double textScale = 1,
    bool dark = false,
    bool waitForDiscovery = true,
  }) async {
    await binding.setSurfaceSize(size);
    addTearDown(() => binding.setSurfaceSize(null));
    await tester.pumpWidget(
      RepaintBoundary(
        key: boundary,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          builder: (context, child) => MediaQuery(
            data: MediaQuery.of(
              context,
            ).copyWith(textScaler: TextScaler.linear(textScale)),
            child: Directionality(
              textDirection: TextDirection.rtl,
              child: child!,
            ),
          ),
          home: Scaffold(body: LanSettingsPage(controller: controller)),
        ),
      ),
    );
    if (waitForDiscovery) {
      await waitUi(
        tester,
        () =>
            !controller.isBusy &&
            find.byType(LinearProgressIndicator).evaluate().isEmpty,
      );
      await settleRoutes(tester);
    } else {
      await tester.pump(const Duration(milliseconds: 50));
    }
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    await tester.runAsync(() async {
      await (FontLoader('Tajawal')
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
          .load();
      await (FontLoader(
        'MaterialIcons',
      )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    });
    await tester.pump();
    await tester.runAsync(() async {
      final image =
          await (boundary.currentContext!.findRenderObject()
                  as RenderRepaintBoundary)
              .toImage();
      final png = await image.toByteData(format: ui.ImageByteFormat.png);
      final file = File('build/verification/$name.png');
      await file.parent.create(recursive: true);
      await file.writeAsBytes(png!.buffer.asUint8List());
      image.dispose();
    });
  }

  Future<void> tap(WidgetTester tester, Finder finder) async {
    await tester.ensureVisible(finder);
    await settleRoutes(tester);
    await realAsync(tester, () => tester.tap(finder));
    await tester.pump();
    await realAsync(tester, () async {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      await tester.pump(const Duration(milliseconds: 50));
    });
  }

  Future<void> expectInlineError(WidgetTester tester, String message) async {
    await waitUi(
      tester,
      () => find.textContaining(message).evaluate().isNotEmpty,
    );
    expect(find.textContaining(message), findsOneWidget);
    expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
  }

  Future<void> startFixture() async {
    await host.startHost(name: 'الجهاز الرئيسي للاختبار');
    discovered = [host.hostReady!.endpoint];
  }

  testLanWidgets(
    'host UI starts actual gateway, refreshes pairing and enforces cashier control denial',
    (tester) async {
      await realAsync(tester, setUpFixture);
      expect(tester.takeException(), isNull);
      final audits = hostStore.audit.length;
      await open(tester, host, size: const Size(960, 400), textScale: 2);
      await tap(tester, find.byKey(const ValueKey('lan-mode-host')));
      await tap(tester, find.byKey(const Key('lan-start-host')));
      await realAsync(tester, () => waitFor(() => host.isHost && !host.isBusy));
      await settleRoutes(tester);
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      expect(host.status, LanConnectionStatus.hosting);
      expect(host.activeStore, same(hostStore));
      expect(hostStore.students.single.code, 'HOST');
      expect(hostStore.audit, hasLength(audits));
      expect(find.byKey(const Key('lan-pairing-code')), findsOneWidget);
      final running = host.hostReady;
      await realAsync(
        tester,
        () => expectLater(host.startHost(), throwsA(isA<CenterException>())),
      );
      expect(host.hostReady, same(running));
      await realAsync(tester, host.renewPairingCode);
      expect(host.pairingCode, matches(RegExp(r'^\d{6}$')));
      expect(host.pairingExpiresAt!.isAfter(DateTime.now()), isTrue);
      await realAsync(tester, () async {
        await hostStore.saveStaff(
          name: 'host-cashier',
          role: StaffRole.cashier,
          password: 'cashier-password',
        );
        await hostStore.signIn('host-cashier', 'cashier-password');
        await expectLater(host.stopHost(), throwsA(isA<CenterException>()));
        await expectLater(
          host.renewPairingCode(),
          throwsA(isA<CenterException>()),
        );
        await expectLater(
          host.revokeDevice('not-authorized'),
          throwsA(isA<CenterException>()),
        );
      });
      await settleRoutes(tester);
      expect(host.isHost, isTrue);
      expect(
        tester
            .widget<OutlinedButton>(find.byKey(const Key('lan-stop-host')))
            .onPressed,
        isNull,
      );
      expect(find.byKey(const Key('lan-pairing-code')), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'pairing confirmation preserves local data, requires host staff login and saves device identity',
    (tester) async {
      await realAsync(tester, setUpFixture);
      expect(tester.takeException(), isNull);
      await realAsync(tester, startFixture);
      final originalAudit = localStore.audit.length;
      await open(tester, client);
      await tap(tester, find.byKey(const ValueKey('lan-mode-client')));
      await waitUi(tester, () => client.endpoints.isNotEmpty && !client.isBusy);
      await settleRoutes(tester);
      expect(find.byKey(const Key('lan-direct-host-identity')), findsOneWidget);
      expect(find.byKey(const Key('lan-pair-dialog')), findsNothing);
      expect(find.textContaining('لن تُدمج'), findsOneWidget);
      expect(find.textContaining('الربط لا يسجل دخول الموظف'), findsOneWidget);
      expect(localStore.currentUser!.name, 'local-owner');
      await tester.enterText(
        find.byKey(const Key('lan-direct-code')),
        host.pairingCode!,
      );
      await tap(tester, find.byKey(const Key('lan-direct-connect')));
      await waitUi(tester, () => client.activeStore.isRemote && !client.isBusy);
      await settleRoutes(tester);
      expect(client.activeStore.currentUser, isNull);
      expect(client.activeStore.hasStaff, isTrue);
      expect(client.activeStore.students, isEmpty);
      expect(localStore.currentUser, isNull);
      expect(localStore.students.single.code, 'LOCAL');
      expect(localStore.audit, hasLength(originalAudit));
      await realAsync(
        tester,
        () => client.activeStore.signIn('host-owner', 'host-password-123'),
      );
      expect(client.activeStore.students.single.code, 'HOST');
      expect(hostStore.currentUser!.name, 'host-owner');
      final persisted = await realAsync(
        tester,
        () => LanSettings(File(localStore.databasePath).parent).read(),
      );
      expect(persisted!.mode, LanMode.client);
      expect(persisted.endpoint!.hostId, host.hostReady!.hostId);
      expect(persisted.deviceId, client.configuration!.deviceId);
      expect(persisted.deviceToken, isNotEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'wrong code inline error cannot switch store or consume local data and retry can pair',
    (tester) async {
      await realAsync(tester, setUpFixture);
      expect(tester.takeException(), isNull);
      await realAsync(tester, startFixture);
      await open(tester, client);
      await tap(tester, find.byKey(const ValueKey('lan-mode-client')));
      await waitUi(tester, () => client.endpoints.isNotEmpty && !client.isBusy);
      await settleRoutes(tester);
      expect(find.byKey(const Key('lan-direct-host-identity')), findsOneWidget);
      expect(find.byKey(const Key('lan-pair-dialog')), findsNothing);
      final wrong = host.pairingCode == '000000' ? '111111' : '000000';
      await tester.enterText(find.byKey(const Key('lan-direct-code')), wrong);
      await tap(tester, find.byKey(const Key('lan-direct-connect')));
      await realAsync(tester, () => waitFor(() => !client.isBusy));
      await tester.pump(const Duration(milliseconds: 200));
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      expect(client.activeStore, same(localStore));
      expect(client.configuration!.mode, LanMode.standalone);
      expect(client.configuration!.deviceToken, isNull);
      expect(localStore.students.single.code, 'LOCAL');
      await expectInlineError(tester, 'كود الربط غير صحيح أو انتهت صلاحيته.');
      await realAsync(
        tester,
        () => client.pairHost(host.hostReady!.endpoint, host.pairingCode!),
      );
      expect(client.activeStore.isRemote, isTrue);
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'saved offline client and discovery error stay remote; local owner data is never writable through client',
    (tester) async {
      await realAsync(tester, () async {
        await setUpFixture();
        await startFixture();
        await client.pairHost(host.hostReady!.endpoint, host.pairingCode!);
        final identity = client.configuration!.deviceId;
        await client.close();
        client.dispose();
        await host.stopHost();
        client = LanController(
          localStore: localStore,
          hostProcess: _TestHostProcess(binary),
          searchHosts: () async =>
              throw const SocketException('fixture discovery unavailable'),
        );
        await client.initialize();
        expect(client.activeStore.isRemote, isTrue);
        expect(client.activeStore.remoteConnected, isFalse);
        expect(client.status, LanConnectionStatus.disconnected);
        expect(client.configuration!.deviceId, identity);
        expect(client.configuration!.mode, LanMode.client);
        await expectLater(
          client.activeStore.saveStudent(
            localStore.students.single.copyWith(name: 'must not overwrite'),
          ),
          throwsA(isA<CenterException>()),
        );
        expect(localStore.students.single.name, 'بيانات الجهاز الثانوي');
      });
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'address recovery pins the saved identity and revoked devices cannot read host data',
    (tester) async {
      await realAsync(tester, () async {
        await setUpFixture();
        await startFixture();
        await client.pairHost(host.hostReady!.endpoint, host.pairingCode!);
        final oldEndpoint = client.configuration!.endpoint!;
        await client.activeStore.signIn('host-owner', 'host-password-123');
        await host.stopHost();
        await host.startHost(name: 'الجهاز الرئيسي للاختبار');
        final current = host.hostReady!.endpoint;
        expect(current.hostId, oldEndpoint.hostId);
        expect(current.certificateSha256, oldEndpoint.certificateSha256);
        discovered = [
          LanEndpoint(
            hostId: current.hostId,
            name: 'wrong pin',
            address: current.address,
            port: current.port,
            certificateSha256: '0' * 64,
          ),
          current,
        ];
        await client.reconnect();
        expect(client.status, LanConnectionStatus.connected);
        expect(client.configuration!.endpoint!.port, current.port);
        expect(
          client.configuration!.endpoint!.certificateSha256,
          oldEndpoint.certificateSha256,
        );
        expect(client.activeStore.currentUser, isNull);
        await client.activeStore.signIn('host-owner', 'host-password-123');
        await host.refreshDevices();
        expect(
          host.pairedDevices.single['deviceId'],
          client.configuration!.deviceId,
        );
        await host.revokeDevice(client.configuration!.deviceId);
        expect(host.pairedDevices.single['revoked'], isTrue);
        await expectLater(
          client.activeStore.refreshRemote(),
          throwsA(isA<CenterException>()),
        );
        expect(localStore.students.single.code, 'LOCAL');
      });
      expect(tester.takeException(), isNull);
    },
  );
  testLanWidgets(
    'manual linking separates copied identity from six digits and pairs only after step two',
    (tester) async {
      await realAsync(tester, setUpFixture);
      await realAsync(tester, startFixture);
      final auditCount = localStore.audit.length;
      await open(tester, client);
      await tap(tester, find.byKey(const ValueKey('lan-mode-client')));
      await settleRoutes(tester);
      await tap(tester, find.byKey(const Key('lan-advanced-link')));
      await settleRoutes(tester);
      await tap(tester, find.byKey(const Key('lan-manual-link')));
      await settleRoutes(tester);
      expect(find.text('الخطوة ١: بيانات الجهاز الرئيسي'), findsOneWidget);
      await tester.enterText(
        find.byKey(const Key('lan-manual-data')),
        '١٢٣٤٥٦',
      );
      await tap(tester, find.byKey(const Key('lan-manual-next')));
      await settleRoutes(tester);
      expect(
        find.textContaining('هذه الستة أرقام هي كود الخطوة ٢'),
        findsOneWidget,
      );
      expect(find.byKey(const Key('lan-pair-dialog')), findsNothing);
      expect(client.configuration!.deviceToken, isNull);
      expect(client.activeStore, same(localStore));
      expect(localStore.audit, hasLength(auditCount));
      await capture(tester, 'lan-manual-step-one-light');
      final identity = host.hostReady!.endpoint.toJson();
      await tester.enterText(
        find.byKey(const Key('lan-manual-data')),
        jsonEncode({...identity}..remove('certificateSha256')),
      );
      await tap(tester, find.byKey(const Key('lan-manual-next')));
      await settleRoutes(tester);
      expect(
        find.text('الصق بيانات الربط كاملة، بما فيها بصمة الجهاز.'),
        findsOneWidget,
      );
      await tester.enterText(
        find.byKey(const Key('lan-manual-data')),
        jsonEncode(identity),
      );
      await tap(tester, find.byKey(const Key('lan-manual-next')));
      await settleRoutes(tester);
      await waitUi(
        tester,
        () => find.byKey(const Key('lan-pair-dialog')).evaluate().isNotEmpty,
      );
      await settleRoutes(tester);
      expect(find.byKey(const Key('lan-manual-dialog')), findsNothing);
      expect(find.text('الخطوة ٢: كود الربط'), findsOneWidget);
      expect(
        find.descendant(
          of: find.byKey(const Key('lan-pair-dialog')),
          matching: find.textContaining(
            host.hostReady!.certificateSha256.substring(0, 12),
          ),
        ),
        findsOneWidget,
      );
      expect(client.configuration!.deviceToken, isNull);
      await tester.enterText(find.byKey(const Key('lan-pair-code')), '١٢٣');
      await tap(tester, find.byKey(const Key('confirm-lan-pair')));
      await settleRoutes(tester);
      expect(
        find.text('اكتب الستة أرقام الظاهرة على الجهاز الرئيسي.'),
        findsOneWidget,
      );
      final arabicCode = host.pairingCode!
          .split('')
          .map((digit) => '٠١٢٣٤٥٦٧٨٩'[int.parse(digit)])
          .join();
      await tester.enterText(
        find.byKey(const Key('lan-pair-code')),
        arabicCode,
      );
      await tap(tester, find.byKey(const Key('confirm-lan-pair')));
      await waitUi(tester, () => client.activeStore.isRemote && !client.isBusy);
      await settleRoutes(tester);
      expect(client.configuration!.endpoint!.hostId, host.hostReady!.hostId);
      expect(
        client.configuration!.endpoint!.certificateSha256,
        host.hostReady!.certificateSha256,
      );
      expect(client.activeStore.currentUser, isNull);
      expect(localStore.students.single.code, 'LOCAL');
      expect(localStore.audit, hasLength(auditCount));
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'socket discovery recovery gives manual identity path and clears guidance after successful retry',
    (tester) async {
      await realAsync(tester, setUpFixture);
      discoveryFailure = const CenterException(
        'تعذر البحث عن أجهزة السنتر على الشبكة.',
        cause: SocketException('sensitive-network-address'),
      );
      await open(tester, client, size: const Size(960, 600), dark: true);
      await tap(tester, find.byKey(const ValueKey('lan-mode-client')));
      await expectInlineError(tester, 'تعذر البحث عن أجهزة السنتر على الشبكة.');
      await settleRoutes(tester);
      expect(find.byKey(const Key('lan-discovery-help')), findsOneWidget);
      expect(find.textContaining('sensitive-network-address'), findsNothing);
      if (Platform.isMacOS) {
        expect(
          find.textContaining(
            'إعدادات النظام ← الخصوصية والأمان ← الشبكة المحلية',
          ),
          findsOneWidget,
        );
        expect(
          find.textContaining('قد يكون إذن الشبكة المحلية'),
          findsOneWidget,
        );
      }
      await tester.ensureVisible(find.byKey(const Key('lan-discovery-manual')));
      await settleRoutes(tester);
      expect(
        tester.getRect(find.byKey(const Key('lan-discovery-manual'))).bottom,
        lessThanOrEqualTo(600),
      );
      await capture(tester, 'lan-discovery-recovery-dark960');
      await tap(tester, find.byKey(const Key('lan-discovery-manual')));
      await settleRoutes(tester);
      expect(find.byKey(const Key('lan-manual-dialog')), findsOneWidget);
      await tester.tap(find.text('رجوع').last);
      await settleRoutes(tester);
      discoveryFailure = null;
      await tap(tester, find.byKey(const Key('lan-discovery-retry')));
      await realAsync(tester, () => waitFor(() => !client.isBusy));
      await settleRoutes(tester);
      expect(find.byKey(const Key('lan-discovery-help')), findsNothing);
      discoveryFailure = const CenterException('تعذر البحث مؤقتًا.');
      await tap(tester, find.byKey(const Key('lan-discover')));
      await expectInlineError(tester, 'تعذر البحث مؤقتًا.');
      await settleRoutes(tester);
      expect(find.byKey(const Key('lan-discovery-help')), findsOneWidget);
      expect(find.textContaining('قد يكون إذن الشبكة المحلية'), findsNothing);
      expect(client.activeStore, same(localStore));
      expect(client.configuration!.deviceToken, isNull);
      expect(localStore.students.single.code, 'LOCAL');
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'LAN diagnostic export works before staff login and cannot overwrite an existing file',
    (tester) async {
      await realAsync(tester, setUpFixture);
      late ProblemLog log;
      final existing = File('${directory.path}/existing.txt');
      final output = File('${directory.path}/support.txt');
      await realAsync(tester, () async {
        log = ProblemLog(Directory('${directory.path}/logs'));
        ProblemLog.current = log;
        await log.startSession();
        await log.record(
          const SocketException('secret-token private-name 123456'),
          StackTrace.current,
          operation: 'lan.discovery',
        );
        await log.flush();
        localStore.signOut();
        await existing.writeAsString('must survive unchanged');
      });
      String? destination;
      const selector = MethodChannel('plugins.flutter.io/file_selector');
      TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
          .setMockMethodCallHandler(selector, (call) async {
            expect(call.method, 'getSavePath');
            expect((call.arguments as Map)['suggestedName'], endsWith('.txt'));
            return destination;
          });
      addTearDown(
        () => TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
            .setMockMethodCallHandler(selector, null),
      );
      await open(tester, client);
      expect(localStore.currentUser, isNull);
      final export = find.byKey(const Key('lan-export-problem-log'));
      await tap(tester, export);
      await settleRoutes(tester);
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      destination = existing.path;
      await tap(tester, export);
      await expectInlineError(
        tester,
        'تعذر تصدير سجل المشاكل. اختر ملفًا جديدًا بامتداد .txt خارج مجلد السجل، وراجع صلاحية الكتابة أو مساحة الجهاز. لا يمكن استبدال ملف موجود.',
      );
      await settleRoutes(tester);
      expect(
        await realAsync(tester, existing.readAsString),
        'must survive unchanged',
      );
      destination = output.path;
      await tap(tester, export);
      await waitUi(tester, () => output.existsSync());
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      await settleRoutes(tester);
      final text = (await realAsync(tester, output.readAsString))!;
      expect(text, contains('SocketException'));
      for (final secret in [
        'secret-token',
        'private-name',
        '123456',
        'host-password-123',
        'local-password-123',
        'بيانات الجهاز الثانوي',
      ]) {
        expect(text, isNot(contains(secret)));
      }
      expect(client.activeStore, same(localStore));
      expect(localStore.currentUser, isNull);
      expect(tester.takeException(), isNull);
    },
  );
  testLanWidgets(
    'dedicated client opens directly with a code, pairs once with held Enter and reconnects saved identity without SQLite',
    (tester) async {
      await realAsync(tester, () => setUpFixture(clientOnly: true));
      await realAsync(tester, startFixture);
      await open(tester, client, size: const Size(1280, 800), dark: true);
      expect(client.isClientOnly, isTrue);
      expect(searches, 1);
      expect(find.text('جهاز متصل — البيانات على الرئيسي'), findsOneWidget);
      expect(find.byKey(const ValueKey('lan-mode-host')), findsNothing);
      expect(find.byKey(const ValueKey('lan-mode-standalone')), findsNothing);
      expect(find.byKey(const Key('lan-start-host')), findsNothing);
      expect(find.byKey(const Key('lan-direct-host-identity')), findsOneWidget);
      expect(find.byKey(const Key('lan-pair-dialog')), findsNothing);
      expect(
        find.textContaining(host.hostReady!.certificateSha256.substring(0, 12)),
        findsOneWidget,
      );
      await tester.enterText(
        find.byKey(const Key('lan-direct-code')),
        host.pairingCode!,
      );
      await capture(tester, 'lan-direct-code-client-dark1280');
      await tester.enterText(
        find.byKey(const Key('lan-direct-code')),
        host.pairingCode!
            .split('')
            .map((digit) => '۰۱۲۳۴۵۶۷۸۹'[int.parse(digit)])
            .join(),
      );
      await realAsync(tester, () async {
        await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      });
      await waitUi(
        tester,
        () =>
            client.activeStore.isRemote &&
            client.activeStore.remoteConnected &&
            !client.isBusy,
      );
      await settleRoutes(tester);
      final token = client.configuration!.deviceToken;
      expect(token, isNotEmpty);
      expect(client.activeStore.currentUser, isNull);
      expect(find.byKey(const Key('lan-direct-code')), findsNothing);
      await realAsync(tester, host.refreshDevices);
      expect(host.pairedDevices, hasLength(1));
      expect(
        await realAsync(tester, () => File(localStore.databasePath).exists()),
        isFalse,
      );
      await tester.pumpWidget(const SizedBox());
      await realAsync(tester, () async {
        await client.close();
        client.dispose();
        await localStore.close();
        localStore = CenterStore.clientWorkspace(
          directory: '${directory.path}/secondary',
        );
        client = LanController(
          localStore: localStore,
          clientOnly: true,
          searchHosts: () async {
            searches++;
            return discovered;
          },
        );
        await client.initialize();
      });
      await open(tester, client);
      expect(client.activeStore.remoteConnected, isTrue);
      expect(client.configuration!.deviceToken, token);
      expect(client.configuration!.endpoint!.hostId, host.hostReady!.hostId);
      expect(
        searches,
        1,
        reason:
            'Saved connection needs no pairing/discovery when its address still works.',
      );
      expect(find.byKey(const Key('lan-direct-code')), findsNothing);
      expect(client.activeStore.currentUser, isNull);
      expect(
        await realAsync(tester, () => File(localStore.databasePath).exists()),
        isFalse,
      );
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'code typed while auto discovery is pending survives and is never submitted until identity is visible and Enter is pressed again',
    (tester) async {
      await realAsync(tester, () => setUpFixture(clientOnly: true));
      await realAsync(tester, startFixture);
      discoveryGate = Completer<List<LanEndpoint>>();
      await open(tester, client, waitForDiscovery: false);
      await waitUi(tester, () => client.isBusy);
      final code = find.byKey(const Key('lan-direct-code'));
      await tester.enterText(code, host.pairingCode!);
      await tester.testTextInput.receiveAction(TextInputAction.done);
      expect(client.configuration!.deviceToken, isNull);
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('lan-direct-connect')))
            .onPressed,
        isNull,
      );
      await realAsync(tester, () async {
        discoveryGate!.complete([host.hostReady!.endpoint]);
      });
      await waitUi(
        tester,
        () =>
            !client.isBusy &&
            find
                .byKey(const Key('lan-direct-host-identity'))
                .evaluate()
                .isNotEmpty,
      );
      await settleRoutes(tester);
      expect(
        tester.widget<TextFormField>(code).controller!.text,
        host.pairingCode,
      );
      expect(
        client.configuration!.deviceToken,
        isNull,
        reason: 'The earlier Enter is not an automatic queued pair request.',
      );
      await realAsync(tester, host.refreshDevices);
      expect(host.pairedDevices, isEmpty);
      await realAsync(
        tester,
        () => tester.testTextInput.receiveAction(TextInputAction.done),
      );
      await waitUi(
        tester,
        () =>
            client.activeStore.isRemote &&
            client.activeStore.remoteConnected &&
            !client.isBusy,
      );
      expect(client.configuration!.endpoint!.hostId, host.hostReady!.hostId);
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'multiple real hosts require an explicit choice and send the code only to the visible chosen host',
    (tester) async {
      await realAsync(tester, () => setUpFixture(clientOnly: true));
      await realAsync(tester, () async {
        await startFixture();
        secondHostStore = await CenterStore.open(
          directory: '${directory.path}/second-host',
        );
        await secondHostStore!.setupAdmin(
          'second-owner',
          'second-password-123',
        );
        secondHost = LanController(
          localStore: secondHostStore!,
          hostProcess: _TestHostProcess(binary),
        );
        await secondHost!.initialize();
        await secondHost!.startHost(name: 'رئيسي آخر');
        discovered = [
          host.hostReady!.endpoint,
          secondHost!.hostReady!.endpoint,
        ];
      });
      await open(tester, client);
      final code = find.byKey(const Key('lan-direct-code'));
      await tester.enterText(code, secondHost!.pairingCode!);
      expect(find.byKey(const Key('lan-direct-host-choice')), findsOneWidget);
      expect(find.byKey(const Key('lan-direct-host-identity')), findsNothing);
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('lan-direct-connect')))
            .onPressed,
        isNull,
      );
      await tester.testTextInput.receiveAction(TextInputAction.done);
      expect(client.configuration!.deviceToken, isNull);
      await tap(tester, find.byKey(const Key('lan-direct-host-choice')));
      await settleRoutes(tester);
      await tap(tester, find.textContaining('رئيسي آخر').last);
      await settleRoutes(tester);
      expect(find.text('تعديلات لم تُحفظ'), findsOneWidget);
      expect(client.configuration!.deviceToken, isNull);
      await tap(tester, find.text('خروج بدون حفظ'));
      await settleRoutes(tester);
      expect(tester.widget<TextFormField>(code).controller!.text, isEmpty);
      expect(
        find.textContaining(
          secondHost!.hostReady!.certificateSha256.substring(0, 12),
        ),
        findsWidgets,
      );
      await tester.enterText(code, secondHost!.pairingCode!);
      await tester.pump();
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('lan-direct-connect')))
            .onPressed,
        isNotNull,
      );
      await tap(tester, find.byKey(const Key('lan-direct-connect')));
      await waitUi(
        tester,
        () =>
            client.activeStore.isRemote &&
            client.activeStore.remoteConnected &&
            !client.isBusy,
      );
      expect(
        client.configuration!.endpoint!.hostId,
        secondHost!.hostReady!.hostId,
      );
      await realAsync(tester, () async {
        await host.refreshDevices();
        await secondHost!.refreshDevices();
      });
      expect(
        host.pairedDevices,
        isEmpty,
        reason: 'The code must never be tried against every discovered host.',
      );
      expect(
        secondHost!.pairedDevices.single['deviceId'],
        client.configuration!.deviceId,
      );
      expect(
        await realAsync(tester, () => File(localStore.databasePath).exists()),
        isFalse,
      );
      expect(tester.takeException(), isNull);
    },
  );

  testLanWidgets(
    'direct code validation, wrong-code and real rate-limit failures preserve code and focus until a fresh code succeeds',
    (tester) async {
      await realAsync(tester, () => setUpFixture(clientOnly: true));
      await realAsync(tester, startFixture);
      await open(tester, client);
      final code = find.byKey(const Key('lan-direct-code'));
      final connect = find.byKey(const Key('lan-direct-connect'));
      await tester.enterText(code, '١٢٣');
      await tap(tester, connect);
      expect(
        find.text('اكتب الستة أرقام الظاهرة على الجهاز الرئيسي.'),
        findsOneWidget,
      );
      final wrong = host.pairingCode == '000000' ? '111111' : '000000';
      await tester.enterText(code, wrong);
      for (var attempt = 0; attempt < 6; attempt++) {
        await tap(tester, connect);
        await expectInlineError(
          tester,
          attempt < 5
              ? 'كود الربط غير صحيح أو انتهت صلاحيته.'
              : 'محاولات الربط كثيرة. انتظر قليلًا قبل المحاولة مجددًا.',
        );
        await settleRoutes(tester);
        expect(client.configuration!.deviceToken, isNull);
        expect(tester.widget<TextFormField>(code).controller!.text, wrong);
        expect(
          tester
              .widget<TextField>(
                find.descendant(of: code, matching: find.byType(TextField)),
              )
              .focusNode!
              .hasFocus,
          isTrue,
        );
      }
      await realAsync(tester, host.refreshDevices);
      expect(host.pairedDevices, isEmpty);
      await realAsync(tester, host.renewPairingCode);
      await tester.enterText(code, host.pairingCode!);
      await tap(tester, connect);
      await waitUi(
        tester,
        () =>
            client.activeStore.isRemote &&
            client.activeStore.remoteConnected &&
            !client.isBusy,
      );
      expect(client.configuration!.deviceToken, isNotEmpty);
      expect(
        await realAsync(tester, () => File(localStore.databasePath).exists()),
        isFalse,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
