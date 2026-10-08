import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/center_store_host_bridge.dart';
import 'package:massar_center/lan/lan_controller.dart';
import 'package:massar_center/lan/lan_host_process.dart';
import 'package:massar_center/lan/lan_settings.dart';
import 'package:massar_center/lan/lan_transport.dart';

class _HeldState {
  _HeldState({this.fullOnly = false, this.status});
  final bool fullOnly;
  final int? status;
  final arrived = Completer<void>(), release = Completer<void>();
  void finish() {
    if (!release.isCompleted) release.complete();
  }
}

// Real host SQLite, bridge, Go gateway and pinned TLS. Only the timing/status
// of a captured HTTP read response is changed at the loopback network boundary.
class _LanFixture {
  _LanFixture(this.executable);
  final String executable;
  late Directory directory, clientDirectory;
  late CenterStore host, remote;
  late CenterStoreHostBridge bridge;
  late HttpServer relay;
  late StreamSubscription<HttpRequest> relaySubscription;
  late LanHostProcess gateway;
  late LanHostReady ready;
  final forwarding = HttpClient();
  late String deviceToken;
  _HeldState? holdNextState, holdNextCommand;
  final held = <_HeldState>[];
  bool corruptNextState = false, loseNextCommand = false;
  int stateRequests = 0, commandRequests = 0;
  LanController? controller;
  CenterStore? workspace;
  Student get student => host.students.single;
  LessonSession get session => host.sessions.single;
  File get pending => File('${clientDirectory.path}/lan-pending-command.json');

  Future<void> start() async {
    directory = await Directory.systemTemp.createTemp('massar-read-priority-');
    clientDirectory = await Directory('${directory.path}/client').create();
    host = await CenterStore.open(directory: '${directory.path}/host');
    await host.setupAdmin('owner', 'synthetic-owner-password');
    await host.saveStaff(
      name: 'cashier',
      password: 'synthetic-cashier-password',
      role: StaffRole.cashier,
    );
    for (final kind in CatalogKind.values) {
      await host.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    String catalog(CatalogKind kind) =>
        host.catalogs.firstWhere((entry) => entry.kind == kind).id;
    await host.saveGroup(
      StudyGroup(
        name: 'Synthetic group',
        subjectId: catalog(CatalogKind.subject),
        centerId: catalog(CatalogKind.center),
        gradeId: catalog(CatalogKind.grade),
        sessionPrice: 6000,
        packagePrice: 24000,
      ),
    );
    await host.saveStudent(
      Student(
        name: 'Synthetic student',
        code: '1001',
        groupIds: [host.groups.single.id],
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    await host.saveSession(
      LessonSession(
        groupId: host.groups.single.id,
        number: 1,
        startsAt: DateTime.now(),
        createdAt: DateTime.now(),
      ),
    );
    bridge = await CenterStoreHostBridge.start(host);
    relay = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    relaySubscription = relay.listen(_forward);
    gateway = LanHostProcess(executablePath: executable);
    ready = await gateway.start(
      dataDirectory: '${directory.path}/gateway',
      upstreamUrl: 'http://127.0.0.1:${relay.port}',
      upstreamSecret: bridge.secret,
      name: 'Synthetic center',
      port: 0,
      discoveryPort: 0,
    );
    final pairing = LanTransport(ready.endpoint, '');
    try {
      final paired = await pairing.pair(
        ready.pairingCode,
        'test-client',
        'Secondary',
      );
      deviceToken = paired['token'] as String;
    } finally {
      pairing.close();
    }
    remote = CenterStore.remote(
      LanTransport(ready.endpoint, deviceToken),
      localDirectory: clientDirectory.path,
    );
    await login();
  }

  Future<void> _forward(HttpRequest request) async {
    final upstream = await forwarding.openUrl(
      request.method,
      bridge.uri.resolve(request.uri.path),
    );
    request.headers.forEach((name, values) {
      if (![
        HttpHeaders.hostHeader,
        HttpHeaders.contentLengthHeader,
        HttpHeaders.transferEncodingHeader,
      ].contains(name)) {
        upstream.headers.set(name, values);
      }
    });
    await upstream.addStream(request);
    final response = await upstream.close();
    var bytes = await response.fold<List<int>>(
      [],
      (all, chunk) => all..addAll(chunk),
    );
    var status = response.statusCode;
    if (request.uri.path == '/api/state') {
      stateRequests++;
      if (corruptNextState) {
        corruptNextState = false;
        bytes = utf8.encode('{"stateDelta":{},"stateVersion":"invalid-delta"}');
      }
      final delayed = holdNextState;
      final full = request.headers.value('X-Massar-State-Version') == null;
      if (delayed != null && (!delayed.fullOnly || full)) {
        holdNextState = null;
        delayed.arrived.complete();
        await delayed.release.future;
        status = delayed.status ?? status;
      }
    }
    if (request.uri.path == '/api/command') {
      commandRequests++;
      final delayed = holdNextCommand;
      if (delayed != null) {
        holdNextCommand = null;
        delayed.arrived.complete();
        await delayed.release.future;
      }
      if (loseNextCommand) {
        loseNextCommand = false;
        status = 502;
      }
    }
    request.response.statusCode = status;
    request.response.headers.contentType = ContentType.json;
    request.response.add(bytes);
    await request.response.close();
  }

  _HeldState delayState({bool fullOnly = false, int? status}) {
    final delayed = _HeldState(fullOnly: fullOnly, status: status);
    held.add(delayed);
    holdNextState = delayed;
    return delayed;
  }

  Future<void> login() =>
      remote.signIn('cashier', 'synthetic-cashier-password');
  Future<void> attend() => remote.recordAttendance(
    EntryRequest(
      studentId: student.id,
      sessionId: session.id,
      mode: EntryMode.single,
    ),
  );

  Future<void> startController() async {
    await remote.close();
    workspace = CenterStore.clientWorkspace(directory: clientDirectory.path);
    final settings = LanSettings(clientDirectory);
    await settings.save(
      LanConfiguration(
        mode: LanMode.client,
        deviceId: 'test-client',
        deviceName: 'Secondary',
        endpoint: ready.endpoint,
        deviceToken: deviceToken,
      ),
    );
    controller = LanController(
      localStore: workspace!,
      settings: settings,
      clientOnly: true,
      searchHosts: () async => [],
    );
    await controller!.initialize();
    remote = controller!.activeStore;
    await login();
  }

  Future<void> close() async {
    for (final delayed in held) {
      delayed.finish();
    }
    if (controller != null) {
      await controller!.close();
      controller!.dispose();
      await workspace!.close();
    } else {
      await remote.close();
    }
    await gateway.dispose();
    forwarding.close(force: true);
    await relay.close(force: true);
    await relaySubscription.cancel();
    await bridge.close();
    await host.close();
    await directory.delete(recursive: true);
  }
}

void main() {
  late Directory buildDirectory;
  late String executable;
  late _LanFixture fixture;
  setUpAll(() async {
    buildDirectory = await Directory.systemTemp.createTemp(
      'massar-read-priority-build-',
    );
    executable =
        Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'] ??
        '${buildDirectory.path}/massar-lan-host${Platform.isWindows ? '.exe' : ''}';
    if (Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'] == null) {
      final compiled = await Process.run('go', [
        'build',
        '-o',
        executable,
        '.',
      ], workingDirectory: '../center-lan');
      expect(compiled.exitCode, 0, reason: '${compiled.stderr}');
    }
  });
  tearDownAll(() => buildDirectory.delete(recursive: true));
  setUp(() async {
    fixture = _LanFixture(executable);
    await fixture.start();
  });
  tearDown(() => fixture.close());

  test(
    'waiting for a host change releases reception and delivers its committed state',
    () async {
      final waiting = fixture.remote.refreshRemote(waitForChanges: true);
      await Future<void>.delayed(const Duration(milliseconds: 100));
      await fixture.host.saveStudentNote(
        studentId: fixture.student.id,
        notes: 'Live host change',
      );
      await waiting.timeout(const Duration(seconds: 2));
      expect(fixture.remote.students.single.notes, 'Live host change');
      final nextWait = fixture.remote.refreshRemote(waitForChanges: true);
      await Future<void>.delayed(const Duration(milliseconds: 100));
      await fixture.remote
          .recordAttendance(
            EntryRequest(
              studentId: fixture.student.id,
              sessionId: fixture.session.id,
              mode: EntryMode.single,
            ),
          )
          .timeout(const Duration(seconds: 2));
      await nextWait;
      expect(fixture.host.attendances, hasLength(1));
      expect(fixture.remote.attendances, hasLength(1));
      expect(
        await File('${fixture.clientDirectory.path}/center.sqlite').exists(),
        false,
      );
    },
  );

  for (final fullRecovery in [false, true]) {
    test(
      'attendance commits before held ${fullRecovery ? 'full recovery' : 'background'} read and older state cannot overwrite it',
      () async {
        fixture.corruptNextState = fullRecovery;
        final delayed = fixture.delayState(fullOnly: fullRecovery);
        final refresh = fixture.remote.refreshRemote();
        await delayed.arrived.future.timeout(const Duration(seconds: 3));
        final coalesced = fixture.remote.refreshRemote();
        await fixture.attend().timeout(const Duration(seconds: 3));
        expect(delayed.release.isCompleted, isFalse);
        expect(fixture.host.attendances, hasLength(1));
        expect(fixture.remote.attendances, hasLength(1));
        expect(fixture.host.payments, isEmpty);
        expect(await fixture.pending.exists(), isFalse);
        delayed.finish();
        await Future.wait([refresh, coalesced]);
        expect(fixture.remote.attendances, hasLength(1));
        expect(fixture.stateRequests, fullRecovery ? 2 : 1);
        expect(fixture.commandRequests, 1);
        expect(await File(fixture.remote.databasePath).exists(), isFalse);
      },
    );
  }

  test('obsolete authorization error cannot sign out a newer login', () async {
    final delayed = fixture.delayState(status: 401);
    final refresh = fixture.remote.refreshRemote();
    await delayed.arrived.future;
    fixture.remote.signOut();
    await fixture.remote
        .signIn('owner', 'synthetic-owner-password')
        .timeout(const Duration(seconds: 3));
    delayed.finish();
    await refresh;
    expect(fixture.remote.currentUser!.name, 'owner');
    expect(fixture.remote.remoteConnected, isTrue);
    await fixture.attend();
    expect(fixture.host.attendances, hasLength(1));
  });

  test(
    'refresh during an unconfirmed command leaves reconciliation and its snapshot to the command',
    () async {
      final delayed = _HeldState();
      fixture.held.add(delayed);
      fixture.holdNextCommand = delayed;
      final attendance = fixture.attend();
      await delayed.arrived.future;
      await fixture.remote.refreshRemote().timeout(const Duration(seconds: 1));
      expect(fixture.stateRequests, 0);
      expect(fixture.remote.remoteConnected, isTrue);
      expect(fixture.remote.attendances, isEmpty);
      expect(fixture.host.attendances, hasLength(1));
      expect(await fixture.pending.exists(), isTrue);
      delayed.finish();
      await attendance;
      expect(fixture.remote.attendances, hasLength(1));
      expect(await fixture.pending.exists(), isFalse);
    },
  );

  test(
    'lost command reply remains pending despite an older successful read and reconciles once',
    () async {
      final delayed = fixture.delayState();
      final refresh = fixture.remote.refreshRemote();
      await delayed.arrived.future;
      fixture.loseNextCommand = true;
      await expectLater(
        fixture.attend(),
        throwsA(isA<LanConnectionException>()),
      );
      expect(await fixture.pending.exists(), isTrue);
      expect(fixture.host.attendances, hasLength(1));
      delayed.finish();
      await refresh;
      expect(fixture.remote.remoteConnected, isFalse);
      expect(await fixture.pending.exists(), isTrue);
      await fixture.remote.refreshRemote();
      expect(fixture.remote.remoteConnected, isTrue);
      expect(fixture.remote.attendances, hasLength(1));
      expect(fixture.host.attendances, hasLength(1));
      expect(fixture.commandRequests, 1);
      expect(await fixture.pending.exists(), isFalse);
    },
  );

  test(
    'signed-in polling delivers continuing host edits without unchanged controller rebuilds',
    () async {
      await fixture.startController();
      var controllerNotifications = 0;
      fixture.controller!.addListener(() => controllerNotifications++);
      for (var edit = 1; edit <= 2; edit++) {
        final changed = Completer<void>();
        void observe() {
          if (fixture.remote.students.single.notes == 'Host edit $edit' &&
              !changed.isCompleted) {
            changed.complete();
          }
        }

        fixture.remote.addListener(observe);
        try {
          await fixture.host.saveStudentNote(
            studentId: fixture.student.id,
            notes: 'Host edit $edit',
          );
          await changed.future.timeout(const Duration(milliseconds: 2400));
        } finally {
          fixture.remote.removeListener(observe);
        }
      }
      expect(controllerNotifications, 0);
      expect(fixture.stateRequests, inInclusiveRange(2, 3));
      expect(await File(fixture.remote.databasePath).exists(), isFalse);
    },
  );

  test('benchmark delayed background read command latency', () async {
    final delayed = fixture.delayState();
    final refresh = fixture.remote.refreshRemote();
    await delayed.arrived.future;
    final release = Timer(const Duration(milliseconds: 700), delayed.finish);
    final watch = Stopwatch()..start();
    try {
      await fixture.attend();
      watch.stop();
      // Private paired benchmark compares the exact same real TLS/SQLite case.
      // ignore: avoid_print
      print('LAN_BACKGROUND_COMMAND_US=${watch.elapsedMicroseconds}');
      await refresh;
      expect(fixture.host.attendances, hasLength(1));
      expect(fixture.remote.attendances, hasLength(1));
      expect(fixture.commandRequests, 1);
    } finally {
      release.cancel();
      delayed.finish();
    }
  });
}
