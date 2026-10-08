import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'package:crypto/crypto.dart' as crypto;
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/academic_import_command.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/center_store_host_bridge.dart';
import 'package:massar_center/lan/lan_host_process.dart';
import 'package:massar_center/lan/lan_transport.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';
import 'package:uuid/uuid.dart';

// Real SQLite, real Go executable, real TLS and HTTP. Faults affect only the
// transport response after the authoritative bridge runs the actual command.
class _Harness {
  _Harness(this.executable);
  final String executable;
  late Directory directory, clientDirectory;
  late CenterStore host, remote;
  late CenterStoreHostBridge bridge;
  late LanHostProcess gateway;
  late LanHostReady ready;
  late LanTransport wire;
  late HttpServer relay;
  late StreamSubscription<HttpRequest> subscription;
  final http = HttpClient();
  bool loseNextCommandReply = false;
  bool legacySnapshots = false;
  bool corruptNextCommandDelta = false;
  bool failNextFullState = false;
  Completer<void>? fullStateArrived, releaseFullState;
  int commandRequests = 0, fullStateRequests = 0;
  int lastCommandBytes = 0;
  Map<String, dynamic>? lastCommandResponse;
  Map<String, dynamic>? lastStateResponse;
  File get pendingFile =>
      File('${clientDirectory.path}/lan-pending-command.json');
  String? staffSession;
  StudyGroup get group => host.groups.single;
  Student get student => host.students.first;
  LessonSession get lesson => host.sessions.first;
  Future<void> start() async {
    directory = await Directory.systemTemp.createTemp('massar-lan-store-');
    clientDirectory = await Directory('${directory.path}/client').create();
    host = await CenterStore.open(directory: '${directory.path}/host');
    await host.setupAdmin('manager', 'test-manager-123');
    final fixtureBackup = await host.createBackup(
      destination: '${directory.path}/owner-fixture.json',
    );
    final fixture =
        jsonDecode(await File(fixtureBackup).readAsString())
            as Map<String, dynamic>;
    final credentials =
        (fixture['data'] as Map<String, dynamic>)['credentials']
            as Map<String, dynamic>;
    await host.ensureInstallationAdmin(
      InstallationAdmin(
        id: host.currentUser!.id,
        name: 'manager',
        credential: Map<String, String>.from(
          credentials[host.currentUser!.id] as Map,
        ),
      ),
    );
    await host.saveStaff(
      name: 'cashier',
      password: 'test-cashier-123',
      role: StaffRole.cashier,
    );
    await host.saveStaff(
      name: 'assistant',
      password: 'test-assistant-123',
      role: StaffRole.assistant,
    );
    for (final kind in CatalogKind.values) {
      await host.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await host.saveGroup(
      StudyGroup(
        name: 'الأحد',
        subjectId: host.catalogs
            .firstWhere((e) => e.kind == CatalogKind.subject)
            .id,
        centerId: host.catalogs
            .firstWhere((e) => e.kind == CatalogKind.center)
            .id,
        gradeId: host.catalogs
            .firstWhere((e) => e.kind == CatalogKind.grade)
            .id,
        sessionPrice: 6000,
        packagePrice: 24000,
      ),
    );
    await host.saveStudent(
      Student(
        name: 'أحمد',
        code: '1001',
        groupIds: [group.id],
        createdAt: DateTime.now().subtract(const Duration(days: 3)),
      ),
    );
    await host.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: DateTime.now(),
        createdAt: DateTime.now(),
      ),
    );
    bridge = await CenterStoreHostBridge.start(host);
    relay = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    subscription = relay.listen((request) async {
      final upstream = await http.openUrl(
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
      if (legacySnapshots) {
        upstream.headers.removeAll('X-Massar-State-Version');
        upstream.headers.removeAll('X-Massar-State-Patch');
      }
      final isCommand = request.uri.path == '/api/command';
      final isFullState =
          request.uri.path == '/api/state' &&
          request.headers.value('X-Massar-State-Version') == null &&
          request.headers.value('X-Massar-State-Patch') == null;
      if (isCommand) commandRequests++;
      if (isFullState) fullStateRequests++;
      await upstream.addStream(request);
      final response = await upstream.close();
      var bytes = await response.fold<List<int>>([], (a, b) => a..addAll(b));
      if (legacySnapshots) {
        final legacy = jsonDecode(utf8.decode(bytes)) as Map<String, dynamic>;
        legacy.remove('stateVersion');
        bytes = utf8.encode(jsonEncode(legacy));
      }
      if (isCommand) {
        lastCommandBytes = bytes.length;
        lastCommandResponse =
            jsonDecode(utf8.decode(bytes)) as Map<String, dynamic>;
        if (corruptNextCommandDelta && response.statusCode == 200) {
          corruptNextCommandDelta = false;
          final damaged = lastCommandResponse!;
          // An invalid base must recover through a full read, never a retry.
          (damaged['stateDelta'] as Map)['baseVersion'] = 'wrong-base';
          bytes = utf8.encode(jsonEncode(damaged));
        }
      }
      if (isFullState && fullStateArrived != null) {
        fullStateArrived!.complete();
        await releaseFullState!.future;
        fullStateArrived = null;
        releaseFullState = null;
      }
      if (request.uri.path == '/api/state') {
        lastStateResponse =
            jsonDecode(utf8.decode(bytes)) as Map<String, dynamic>;
      }
      if (failNextFullState && isFullState) {
        failNextFullState = false;
        request.response.statusCode = 502;
        request.response.write('{"error":"full_state_unavailable"}');
      } else if (loseNextCommandReply && isCommand) {
        loseNextCommandReply = false;
        request.response.statusCode = 502;
        request.response.write('{"error":"reply_lost"}');
      } else {
        request.response.statusCode = response.statusCode;
        request.response.headers.contentType = ContentType.json;
        request.response.add(bytes);
      }
      await request.response.close();
    });
    gateway = LanHostProcess(executablePath: executable);
    ready = await gateway.start(
      dataDirectory: '${directory.path}/gateway',
      upstreamUrl: 'http://127.0.0.1:${relay.port}',
      upstreamSecret: bridge.secret,
      name: 'السنتر',
      port: 0,
      discoveryPort: 0,
    );
    final pairing = LanTransport(ready.endpoint, '');
    final response = await pairing.pair(
      ready.pairingCode,
      'client-one',
      'الجهاز الثاني',
    );
    pairing.close();
    wire = LanTransport(ready.endpoint, response['token'] as String);
    remote = CenterStore.remote(wire, localDirectory: clientDirectory.path);
    await remote.signIn('cashier', 'test-cashier-123');
  }

  Future<LessonSession> addParallelGroup() async {
    final original = group;
    await host.saveGroup(original.copyWith(id: '', name: 'مجموعة أخرى'));
    final other = host.groups.firstWhere((e) => e.id != original.id);
    await host.saveStudent(student.copyWith(groupIds: [original.id, other.id]));
    await host.saveSession(
      LessonSession(
        groupId: other.id,
        number: lesson.number,
        startsAt: DateTime.now().add(const Duration(hours: 1)),
        createdAt: DateTime.now(),
      ),
    );
    return host.sessions.firstWhere((e) => e.groupId == other.id);
  }

  Future<void> rawLogin({
    String name = 'manager',
    String password = 'test-manager-123',
  }) async {
    final response = await wire.post('/api/login', {
      'name': name,
      'password': password,
    });
    staffSession = response['staffSession'] as String;
  }

  Map<String, dynamic> request(
    String operation,
    Map<String, dynamic> arguments,
  ) => {
    'requestId': const Uuid().v4(),
    'operation': operation,
    'arguments': {
      ...arguments,
      if (operation == 'renewPackage')
        'expected': {'baseAmount': 24000, 'discountPercent': 0, 'remaining': 0},
    },
  };
  Map<String, dynamic> pending(Map<String, dynamic> request, String staffId) =>
      {
        'requestId': request['requestId'],
        'operation': request['operation'],
        'staffId': staffId,
        'requestHash': crypto.sha256
            .convert(
              utf8.encode(
                jsonEncode({
                  'operation': request['operation'],
                  'arguments': request['arguments'],
                }),
              ),
            )
            .toString(),
      };
  Future<void> expectMatchingPublicState() async {
    final staffId = remote.currentUser!.id;
    final authoritative = await host.snapshotLan(staffId);
    final received = await remote.snapshotLan(staffId);
    expect(received['state'], authoritative['state']);
    expect(received['currentUser'], authoritative['currentUser']);
    expect(received['state'], isNot(contains('credentials')));
  }

  Future<void> close() async {
    await remote.close();
    wire.close();
    await gateway.dispose();
    http.close(force: true);
    await relay.close(force: true);
    await subscription.cancel();
    await bridge.close();
    await host.close();
    await directory.delete(recursive: true);
  }
}

void main() {
  late Directory buildDirectory;
  late String executable;
  setUpAll(() async {
    buildDirectory = await Directory.systemTemp.createTemp(
      'massar-lan-store-build-',
    );
    executable =
        Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'] ??
        '${buildDirectory.path}/massar-lan-host${Platform.isWindows ? '.exe' : ''}';
    if (Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'] == null) {
      final built = await Process.run('go', [
        'build',
        '-o',
        executable,
        '.',
      ], workingDirectory: '../center-lan');
      expect(
        built.exitCode,
        0,
        reason: 'Real native Go gateway required: ${built.stderr}',
      );
    }
  });
  tearDownAll(() => buildDirectory.delete(recursive: true));
  late _Harness h;
  setUp(() async {
    h = _Harness(executable);
    await h.start();
  });
  tearDown(() async => h.close());

  test(
    'unchanged LAN polls skip records but refresh support and changed host data',
    () async {
      await h.remote.refreshRemote();
      expect(h.lastStateResponse!['stateUnchanged'], isTrue);
      expect(h.lastStateResponse!.containsKey('state'), isFalse);
      expect(h.remote.students.single.name, h.student.name);
      h.host.supportStatusReader = () => {'configured': true, 'queued': true};
      await h.remote.refreshRemote();
      expect(h.lastStateResponse!['stateUnchanged'], isTrue);
      expect(h.remote.supportStatus['queued'], isTrue);
      await h.host.saveStudentNote(
        studentId: h.student.id,
        notes: 'تحديث من الرئيسي',
      );
      await h.remote.refreshRemote();
      expect(h.lastStateResponse!.containsKey('state'), isFalse);
      expect(h.lastStateResponse!.containsKey('stateDelta'), isTrue);
      expect(h.lastStateResponse!.containsKey('stateUnchanged'), isFalse);
      expect(h.remote.students.single.notes, 'تحديث من الرئيسي');
      await h.expectMatchingPublicState();
      await h.remote.refreshRemote();
      expect(h.lastStateResponse!.containsKey('state'), isFalse);
    },
  );

  test(
    'unchanged support receipts do not rebuild the secondary, but status changes do',
    () async {
      var receiptId = 'receipt-one';
      var pending = false;
      h.host.supportStatusReader = () => {
        'configured': true,
        'pending': pending,
        'receipt': {
          'uploadId': 'upload',
          'receiptId': receiptId,
          'sha256': 'hash',
          'receivedAt': '2026-10-08T00:00:00Z',
        },
      };
      await h.remote.refreshRemote();
      var notifications = 0;
      h.remote.addListener(() => notifications++);
      await h.remote.refreshRemote();
      await h.remote.refreshRemote();
      expect(h.lastStateResponse!['stateUnchanged'], isTrue);
      expect(notifications, 0);
      receiptId = 'receipt-two';
      await h.remote.refreshRemote();
      expect(notifications, 1);
      expect(h.remote.supportStatus['receipt']['receiptId'], 'receipt-two');
      pending = true;
      await h.remote.refreshRemote();
      expect(notifications, 2);
      h.host.supportStatusReader = () => {'configured': false};
      await h.remote.refreshRemote();
      expect(notifications, 3);
      expect(h.remote.supportStatus.containsKey('receipt'), isFalse);
      await h.remote.refreshRemote();
      expect(notifications, 3);
    },
  );

  test(
    'secondary still refreshes against a host without snapshot versions',
    () async {
      h.legacySnapshots = true;
      await h.remote.refreshRemote();
      expect(h.lastStateResponse!.containsKey('stateVersion'), isFalse);
      expect(h.lastStateResponse!.containsKey('state'), isTrue);
      expect(h.lastStateResponse!.containsKey('stateDelta'), isFalse);
      await h.host.saveStudentNote(
        studentId: h.student.id,
        notes: 'تحديث بالتوافق القديم',
      );
      await h.remote.refreshRemote();
      expect(h.remote.students.single.notes, 'تحديث بالتوافق القديم');
      expect(h.lastStateResponse!.containsKey('state'), isTrue);
      await h.expectMatchingPublicState();
      h.legacySnapshots = false;
      await h.remote.refreshRemote();
      await h.remote.refreshRemote();
      expect(h.lastStateResponse!['stateUnchanged'], isTrue);
    },
  );

  test(
    'versioned LAN splices preserve commands, deletions, settings and saved closing categories',
    () async {
      // Retained neighbours make a one-row splice smaller than a full list.
      for (final code in ['1002', '1003']) {
        await h.host.saveStudent(
          Student(
            name: 'طالب $code',
            code: code,
            groupIds: [h.group.id],
            createdAt: DateTime(2026),
          ),
        );
      }
      await h.remote.refreshRemote();
      await h.remote.saveStudentNote(
        studentId: h.student.id,
        notes: 'تحديث صغير من الجهاز الثاني',
      );
      final command = h.lastCommandResponse!;
      expect(command['status'], 'committed');
      expect(command.containsKey('state'), isFalse);
      expect(command.containsKey('stateUnchanged'), isFalse);
      expect(
        command['stateDelta']['splices']['students']['items'],
        hasLength(1),
      );
      expect(command['currentUser']['id'], h.remote.currentUser!.id);
      await h.expectMatchingPublicState();
      final full = await h.host.snapshotLan(h.remote.currentUser!.id);
      expect(
        h.lastCommandBytes,
        lessThan(utf8.encode(jsonEncode(full)).length),
      );

      final month = await h.host.saveStudyMonth(
        StudyMonth(
          name: 'شهر المزامنة',
          lessons: [const PreparedLesson(number: 1)],
        ),
      );
      final lesson = await h.host.startPreparedLesson(
        groupId: h.group.id,
        preparedLessonId: month.lessons.single.id,
      );
      await h.remote.refreshRemote();
      await h.remote.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: lesson.id,
          mode: EntryMode.single,
        ),
      );
      expect(h.lastCommandResponse!.containsKey('stateDelta'), isTrue);
      expect(h.remote.attendances, hasLength(1));
      expect(h.remote.payments, hasLength(1));
      await h.remote.checkPayment(
        studentId: h.student.id,
        sessionId: lesson.id,
        expectedAmount: 6000,
      );
      await h.host.uncheckPayment(
        studentId: h.student.id,
        sessionId: lesson.id,
      );
      await h.host.saveGroup(h.group.copyWith(name: 'اسم المجموعة المحدّث'));
      await h.host.saveCardSettings(const CenterCardSettings(price: 2000));
      await h.remote.refreshRemote();
      final delta = h.lastStateResponse!['stateDelta'] as Map;
      expect(delta['set']['paymentChecks'], isEmpty);
      expect(delta['set']['groups'], isNotEmpty);
      expect(delta['set']['cardSettings']['price'], 2000);
      expect(h.remote.paymentChecks, isEmpty);
      expect(h.remote.currentUser!.name, 'cashier');
      expect(h.host.currentUser!.name, 'manager');
      await h.expectMatchingPublicState();

      await h.host.setStudentCenterFee(studentId: h.student.id, enabled: true);
      await h.host.collectStudentCenterFee(
        studentId: h.student.id,
        sessionId: lesson.id,
      );
      await h.host.closeSession(lesson.id);
      await h.remote.refreshRemote();
      await h.host.finalizeSession(sessionId: lesson.id, actualCash: 7500);
      await h.remote.refreshRemote();
      expect(
        h.lastStateResponse!['stateDelta']['set']['closings'],
        hasLength(1),
      );
      expect(
        h.remote.closings.single.summary.centerFeePaymentCategories!.single
            .toJson(),
        {
          'centerOnly': false,
          'unitAmount': 1500,
          'studentCount': 1,
          'operationCount': 1,
        },
      );
      await h.expectMatchingPublicState();
    },
  );

  test(
    'malformed committed LAN delta recovers with one full read and one mutation',
    () async {
      final beforeAudit = h.host.audit.length;
      final fullReads = h.fullStateRequests;
      h.corruptNextCommandDelta = true;
      await h.remote.saveStudentNote(
        studentId: h.student.id,
        notes: 'حُفظت رغم تلف الرد',
      );
      expect(h.commandRequests, 1);
      expect(h.fullStateRequests, fullReads + 1);
      expect(h.lastStateResponse!.containsKey('state'), isTrue);
      expect(h.host.audit.length, beforeAudit + 1);
      expect(h.remote.students.single.notes, 'حُفظت رغم تلف الرد');
      expect(await h.pendingFile.exists(), isFalse);
      expect(h.remote.remoteConnected, isTrue);
      await h.expectMatchingPublicState();
    },
  );

  test(
    'failed LAN delta recovery retains the previous view and pending receipt until reconciliation',
    () async {
      final before = await h.remote.snapshotLan(h.remote.currentUser!.id);
      final beforeAudit = h.host.audit.length;
      h.corruptNextCommandDelta = true;
      h.failNextFullState = true;
      await expectLater(
        h.remote.saveStudentNote(
          studentId: h.student.id,
          notes: 'محفوظة على الرئيسي فقط',
        ),
        throwsA(
          isA<LanConnectionException>().having(
            (error) => error.outcomeUnknown,
            'outcomeUnknown',
            isTrue,
          ),
        ),
      );
      final after = await h.remote.snapshotLan(h.remote.currentUser!.id);
      expect(after['state'], before['state']);
      expect(h.student.notes, 'محفوظة على الرئيسي فقط');
      expect(h.host.audit.length, beforeAudit + 1);
      expect(h.remote.remoteConnected, isFalse);
      expect(await h.pendingFile.exists(), isTrue);
      expect(h.commandRequests, 1);

      await h.remote.refreshRemote();
      expect(h.remote.remoteConnected, isTrue);
      expect(await h.pendingFile.exists(), isFalse);
      expect(h.commandRequests, 1);
      expect(h.host.audit.length, beforeAudit + 1);
      await h.expectMatchingPublicState();
    },
  );

  test(
    'logout during LAN delta recovery cannot restore the old employee view or clear pending',
    () async {
      h.corruptNextCommandDelta = true;
      final arrived = h.fullStateArrived = Completer<void>();
      final release = h.releaseFullState = Completer<void>();
      final saving = expectLater(
        h.remote.saveStudentNote(
          studentId: h.student.id,
          notes: 'خرج الموظف أثناء وصول الرد',
        ),
        throwsA(
          isA<LanConnectionException>().having(
            (error) => error.outcomeUnknown,
            'outcomeUnknown',
            isTrue,
          ),
        ),
      );
      try {
        await arrived.future.timeout(const Duration(seconds: 10));
        h.remote.signOut();
      } finally {
        release.complete();
      }
      await saving;
      expect(h.remote.currentUser, isNull);
      expect(h.remote.students, isEmpty);
      expect(h.remote.remoteConnected, isFalse);
      expect(await h.pendingFile.exists(), isTrue);
      expect(h.commandRequests, 1);
      expect(h.student.notes, 'خرج الموظف أثناء وصول الرد');

      await h.remote.signIn('cashier', 'test-cashier-123');
      expect(await h.pendingFile.exists(), isFalse);
      expect(h.commandRequests, 1);
      await h.expectMatchingPublicState();
    },
  );

  test(
    'postcommit LAN snapshot failure preserves durable state and reconciles once',
    () async {
      const note = 'حفظ مؤكد قبل فشل تجهيز الرد';
      final beforeAudit = h.host.audit.length;
      final previousNote = h.remote.students.single.notes;
      h.host.supportStatusReader = () {
        if (h.student.notes == note) throw StateError('snapshot unavailable');
        return {'configured': false};
      };
      await expectLater(
        h.remote.saveStudentNote(studentId: h.student.id, notes: note),
        throwsA(
          isA<LanConnectionException>().having(
            (error) => error.outcomeUnknown,
            'outcomeUnknown',
            isTrue,
          ),
        ),
      );
      expect(h.student.notes, note);
      expect(h.host.audit.length, beforeAudit + 1);
      expect(h.remote.students.single.notes, previousNote);
      expect(h.remote.remoteConnected, isFalse);
      expect(await h.pendingFile.exists(), isTrue);
      final pending = jsonDecode(await h.pendingFile.readAsString()) as Map;
      final database = await databaseFactoryFfi.openDatabase(
        h.host.databasePath,
        options: OpenDatabaseOptions(readOnly: true, singleInstance: false),
      );
      try {
        final rows = await database.query('state');
        final saved = jsonDecode(rows.single['payload'] as String) as Map;
        expect((saved['students'] as List).single['notes'], note);
        expect((saved['audit'] as List).length, beforeAudit + 1);
        final receipts = await database.query(
          'lan_commands',
          where: 'request_id = ?',
          whereArgs: [pending['requestId']],
        );
        expect(receipts, hasLength(1));
        expect(
          jsonDecode(receipts.single['result_json'] as String)['status'],
          'committed',
        );
      } finally {
        await database.close();
        h.host.supportStatusReader = null;
      }
      await h.remote.refreshRemote();
      expect(await h.pendingFile.exists(), isFalse);
      expect(h.commandRequests, 1);
      expect(h.host.audit.length, beforeAudit + 1);
      expect(h.remote.students.single.notes, note);
      await h.expectMatchingPublicState();
    },
  );

  test(
    'failed state write preserves the transmitted snapshot and receipt until retry',
    () async {
      await h.rawLogin();
      final before = await h.wire.get(
        '/api/state',
        staffSession: h.staffSession,
      );
      final request = h.request('saveStudentNote', {
        'studentId': h.student.id,
        'notes': 'ملاحظة "جديدة"\n\\ 📘',
      });
      final database = await databaseFactoryFfi.openDatabase(
        h.host.databasePath,
      );
      await database.execute(
        "CREATE TRIGGER deny_lan_write BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'test write failure'); END",
      );
      try {
        await expectLater(
          h.wire.post('/api/command', request, staffSession: h.staffSession),
          throwsA(isA<CenterException>()),
        );
        final rejected = await h.wire.get(
          '/api/state',
          staffSession: h.staffSession,
        );
        expect(rejected['state'], before['state']);
        expect(
          await database.query(
            'lan_commands',
            where: 'request_id = ?',
            whereArgs: [request['requestId']],
          ),
          isEmpty,
        );
      } finally {
        await database.execute('DROP TRIGGER deny_lan_write');
      }
      final committed = await h.wire.post(
        '/api/command',
        request,
        staffSession: h.staffSession,
      );
      final replay = await h.wire.post(
        '/api/command',
        request,
        staffSession: h.staffSession,
      );
      expect(committed['status'], 'committed');
      expect(replay, committed);
      final expected = await h.host.snapshotLan(h.host.currentUser!.id);
      expect(committed['state'], expected['state']);
      expect(committed['state'], isNot(contains('credentials')));
      expect(h.student.notes, request['arguments']['notes']);
      expect(
        h.host.audit.length,
        (before['state']['audit'] as List).length + 1,
      );
    },
  );

  test(
    'host employee identity stays local; snapshot never includes credentials',
    () async {
      expect(h.remote.currentUser!.name, 'cashier');
      expect(h.host.currentUser!.name, 'manager');
      await h.remote.saveStudentNote(
        studentId: h.student.id,
        notes: 'ملاحظة من الثاني',
      );
      expect(h.host.students.single.notes, 'ملاحظة من الثاني');
      expect(h.host.audit.last.staffId, h.remote.currentUser!.id);
      expect(h.host.currentUser!.name, 'manager');
      final login = await h.wire.post('/api/login', {
        'name': 'cashier',
        'password': 'test-cashier-123',
      });
      expect(login['state'], isNot(contains('credentials')));
      expect(jsonEncode(login), isNot(contains('pbkdf2')));
      expect(
        await File('${h.clientDirectory.path}/center.sqlite').exists(),
        isFalse,
      );
    },
  );

  test(
    'Excel grade batch on secondary commits once with Cairo attendance after a lost reply',
    () async {
      final center = h.host.catalogs.firstWhere(
        (entry) => entry.kind == CatalogKind.center,
      );
      await h.host.saveCatalog(center.copyWith(region: 'cairo'));
      await h.host.saveStudent(
        Student(
          name: 'طالب ثان للاختبار',
          code: '1002',
          groupIds: [h.group.id],
          createdAt: DateTime.now(),
        ),
      );
      final month = await h.host.saveStudyMonth(
        StudyMonth(
          name: 'شهر استيراد الدرجات',
          lessons: [const PreparedLesson(number: 1)],
        ),
      );
      final session = await h.host.startPreparedLesson(
        groupId: h.group.id,
        preparedLessonId: month.lessons.single.id,
      );
      final exam = await h.host.saveAcademicActivity(
        AcademicActivity(
          name: 'امتحان الاستيراد',
          kind: AcademicActivityKind.exam,
          preparedLessonId: month.lessons.single.id,
          maxScore: 20,
          createdAt: DateTime.now(),
        ),
      );
      await h.remote.refreshRemote();
      final staffId = h.remote.currentUser!.id;
      final auditCount = h.host.audit.length;
      final students = h.remote.students;
      final command = AcademicImportCommand(
        groupId: h.group.id,
        sessionId: session.id,
        activityId: exam.id,
        maxScore: 20,
        rows: [
          for (var index = 0; index < students.length; index++)
            AcademicImportRow(
              studentId: students[index].id,
              score: index == 0 ? 0 : 18.5,
              maxScore: 20,
              expected: null,
              source: AcademicImportSource(
                sessionId: 'source-session',
                attemptId: 'source-attempt-$index',
                version: '1',
              ),
            ),
        ],
      );
      h.loseNextCommandReply = true;
      await expectLater(
        h.remote.importAcademicGrades(command),
        throwsA(
          isA<LanConnectionException>().having(
            (error) => error.outcomeUnknown,
            'outcomeUnknown',
            isTrue,
          ),
        ),
      );
      expect(h.commandRequests, 1);
      expect(h.host.academics.map((record) => record.score), [0, 18.5]);
      expect(h.host.attendances, hasLength(2));
      expect(
        h.host.attendances.every((a) => a.sessionId == session.id),
        isTrue,
      );
      expect(h.host.payments, isEmpty);
      expect(h.host.audit, hasLength(auditCount + 1));
      expect(h.host.audit.last.staffId, staffId);
      expect(h.remote.academics, isEmpty);
      expect(await h.pendingFile.exists(), isTrue);

      await h.remote.refreshRemote();
      expect(await h.pendingFile.exists(), isFalse);
      expect(h.commandRequests, 1);
      expect(h.host.audit, hasLength(auditCount + 1));
      expect(h.host.attendances, hasLength(2));
      expect(h.remote.academics, hasLength(2));
      expect(
        await File('${h.clientDirectory.path}/center.sqlite').exists(),
        isFalse,
      );
      await h.expectMatchingPublicState();
    },
  );

  test(
    'simultaneous host and client cannot charge the same attendance twice',
    () async {
      final request = EntryRequest(
        studentId: h.student.id,
        sessionId: h.lesson.id,
        mode: EntryMode.single,
      );
      final results = await Future.wait(
        [
          h.host.collectAndAttend(request),
          h.remote.collectAndAttend(request),
        ].map((f) => f.then<Object?>((_) => null, onError: (Object e) => e)),
      );
      expect(results.whereType<CenterException>(), hasLength(1));
      expect(h.host.payments.single.netAmount, 6000);
      expect(h.host.attendanceCount(h.lesson.id), 1);
      await h.remote.refreshRemote();
      expect(h.remote.payments.single.netAmount, 6000);
      expect(h.remote.attendanceCount(h.lesson.id), 1);
    },
  );

  test(
    'host assigns unique immutable codes for concurrent registrations',
    () async {
      Student draft(String name) => Student(
        name: name,
        code: '',
        groupIds: [h.group.id],
        createdAt: DateTime.now(),
      );
      final result = await Future.wait([
        h.host.registerStudent(draft('الأول')),
        h.remote.registerStudent(draft('الثاني')),
      ]);
      expect(result.map((e) => e.code).toSet(), hasLength(2));
      expect(h.host.students.map((e) => e.code).toSet(), hasLength(3));
      await h.remote.refreshRemote();
      expect(h.remote.students, hasLength(3));
    },
  );

  test(
    'cashier can edit discounts but cannot inherit the host admin staff permission',
    () async {
      final auditCount = h.host.audit.length;
      await h.remote.saveStudentDiscount(studentId: h.student.id, percent: 100);
      expect(h.host.audit.last.staffId, h.remote.currentUser!.id);
      await expectLater(
        h.remote.saveStaff(
          name: 'intruder',
          password: 'test-intruder-123',
          role: StaffRole.admin,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.students.single.discountPercent, 100);
      expect(h.host.audit, hasLength(auditCount + 1));
      expect(h.host.staff, hasLength(3));
      expect(
        await File(
          '${h.clientDirectory.path}/lan-pending-command.json',
        ).exists(),
        false,
      );
    },
  );

  test(
    'stale quoted discount is rejected after another device edits it',
    () async {
      final preview = h.remote.entryConfirmationFor(
        EntryRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          mode: EntryMode.single,
        ),
      );
      await h.host.saveStudentDiscount(studentId: h.student.id, percent: 25);
      await expectLater(
        h.remote.collectAndAttend(
          EntryRequest(
            studentId: h.student.id,
            sessionId: h.lesson.id,
            mode: EntryMode.single,
            confirmation: preview,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.payments, isEmpty);
      await h.remote.refreshRemote();
      expect(h.remote.students.single.discountPercent, 25);
    },
  );

  test(
    'card actions preserve attendance independence and named academics require actual attendance',
    () async {
      await h.host.saveCardSettings(
        const CenterCardSettings(
          price: 2000,
          requirePaymentBeforeReceipt: false,
        ),
      );
      await h.remote.refreshRemote();
      await h.remote.collectStudentCard(studentId: h.student.id);
      await h.remote.receiveStudentCard(h.student.id);
      expect(h.host.attendances, isEmpty);
      final month = await h.host.saveStudyMonth(
        StudyMonth(
          name: 'شهر الرصد',
          lessons: [const PreparedLesson(number: 1)],
        ),
      );
      final lesson = await h.host.startPreparedLesson(
        groupId: h.group.id,
        preparedLessonId: month.lessons.single.id,
      );
      h.remote.signOut();
      await h.remote.signIn('assistant', 'test-assistant-123');
      final exam = await h.remote.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: lesson.preparedLessonId,
          kind: AcademicActivityKind.exam,
          name: 'الحركة',
          maxScore: 20,
          createdAt: DateTime.now(),
        ),
      );
      await expectLater(
        h.remote.saveAcademic(
          AcademicRecord(
            studentId: h.student.id,
            sessionId: lesson.id,
            activityId: exam.id,
            score: 0,
            maxScore: 20,
            updatedAt: DateTime.now(),
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      await h.host.recordAttendance(
        EntryRequest(
          studentId: h.student.id,
          sessionId: lesson.id,
          mode: EntryMode.single,
        ),
      );
      await h.remote.refreshRemote();
      await h.remote.saveAcademic(
        AcademicRecord(
          studentId: h.student.id,
          sessionId: lesson.id,
          activityId: exam.id,
          score: 0,
          maxScore: 20,
          updatedAt: DateTime.now(),
        ),
      );
      final homework = await h.remote.saveAcademicActivity(
        AcademicActivity(
          preparedLessonId: lesson.preparedLessonId,
          kind: AcademicActivityKind.homework,
          name: 'الواجب الأول',
          createdAt: DateTime.now(),
        ),
      );
      await h.remote.saveAcademic(
        AcademicRecord(
          studentId: h.student.id,
          sessionId: lesson.id,
          activityId: homework.id,
          homework: HomeworkStatus.incomplete,
          updatedAt: DateTime.now(),
        ),
      );
      expect(h.host.academics.map((e) => e.score), contains(0));
      expect(
        h.host.academics.map((e) => e.homework),
        contains(HomeworkStatus.incomplete),
      );
      expect(h.host.cardReceipts, hasLength(1));
      expect(h.host.attendances, hasLength(1));
      expect(h.host.payments, isEmpty);
    },
  );

  test(
    'lost group batch response reconciles without creating another set of sessions',
    () async {
      final first = h.group;
      await h.host.saveGroup(first.copyWith(id: '', name: 'المجموعة الثانية'));
      final ids = h.host.groups.map((group) => group.id).toList();
      await h.remote.signIn('manager', 'test-manager-123');
      h.loseNextCommandReply = true;
      await expectLater(
        h.remote.createGroupSessions(groupIds: ids, kind: SessionKind.free),
        throwsA(isA<LanConnectionException>()),
      );
      expect(h.host.sessions, hasLength(3));
      await h.remote.signIn('manager', 'test-manager-123');
      expect(h.remote.sessions, hasLength(3));
      expect(h.host.sessions, hasLength(3));
      expect(
        h.remote.sessions
            .where((session) => session.groupId == first.id)
            .map((session) => session.number),
        [1, 2],
      );
      expect(
        h.remote.sessions
            .where((session) => session.groupId != first.id)
            .single
            .number,
        1,
      );
      expect(
        h.host.audit.where((entry) => entry.action == 'sessions_create'),
        hasLength(1),
      );
      expect(
        await File(
          '${h.clientDirectory.path}/lan-pending-command.json',
        ).exists(),
        false,
      );
      expect(
        await File('${h.clientDirectory.path}/center.sqlite').exists(),
        false,
      );
      expect(h.host.payments, isEmpty);
    },
  );

  test(
    'local and connected managers create simultaneous batches with unique per-group numbers',
    () async {
      final first = h.group;
      await h.host.saveGroup(first.copyWith(id: '', name: 'المجموعة الثانية'));
      final ids = h.host.groups.map((group) => group.id).toList();
      await h.remote.signIn('manager', 'test-manager-123');
      await Future.wait([
        h.remote.createGroupSessions(groupIds: ids, kind: SessionKind.counted),
        h.host.createGroupSessions(groupIds: ids, kind: SessionKind.counted),
      ]);
      expect(
        h.host.sessions
            .where((session) => session.groupId == first.id)
            .map((session) => session.number),
        [1, 2, 3],
      );
      expect(
        h.host.sessions
            .where((session) => session.groupId != first.id)
            .map((session) => session.number),
        [1, 2],
      );
      expect(
        h.host.audit.where((entry) => entry.action == 'sessions_create'),
        hasLength(2),
      );
      expect(h.host.payments, isEmpty);
    },
  );

  test(
    'lost committed payment response is reconciled once, including restart',
    () async {
      h.loseNextCommandReply = true;
      await expectLater(
        h.remote.renewPackage(
          PackageRequest(studentId: h.student.id, groupId: h.group.id),
        ),
        throwsA(isA<LanConnectionException>()),
      );
      expect(h.host.payments, hasLength(1));
      expect(h.remote.remoteConnected, false);
      final pending = File(
        '${h.clientDirectory.path}/lan-pending-command.json',
      );
      expect(await pending.exists(), true);
      expect(await pending.readAsString(), isNot(contains('test-cashier-123')));
      await h.remote.close();
      // New transport/store simulates restarting the second application.
      h.wire = LanTransport(h.ready.endpoint, h.wire.deviceToken);
      h.remote = CenterStore.remote(
        h.wire,
        localDirectory: h.clientDirectory.path,
      );
      await h.remote.signIn('cashier', 'test-cashier-123');
      expect(h.remote.payments, hasLength(1));
      expect(h.remote.remainingFor(h.student.id, h.group.id), 4);
      expect(await pending.exists(), false);
      expect(h.host.payments, hasLength(1));
    },
  );

  test(
    'durable cancellation tombstone rejects delayed requests and changed reuse',
    () async {
      await h.rawLogin();
      final request = h.request('renewPackage', {
        'studentId': h.student.id,
        'groupId': h.group.id,
        'method': 'نقدي',
        'notes': '',
        'sessionId': null,
        'sessions': 4,
      });
      final user = h.host.staff.first;
      final aborted = await h.wire.post(
        '/api/cancel-command',
        h.pending(request, user.id),
        staffSession: h.staffSession,
      );
      expect(aborted['status'], 'aborted');
      final late = await h.wire.post(
        '/api/command',
        request,
        staffSession: h.staffSession,
      );
      expect(late['status'], 'aborted');
      expect(h.host.payments, isEmpty);
      await expectLater(
        h.wire.post('/api/command', {
          ...request,
          'operation': 'saveStudentNote',
        }, staffSession: h.staffSession),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.payments, isEmpty);
    },
  );

  test(
    'same durable request cannot renew twice and binds employee/device',
    () async {
      await h.rawLogin();
      final request = h.request('renewPackage', {
        'studentId': h.student.id,
        'groupId': h.group.id,
        'method': 'نقدي',
        'notes': '',
        'sessionId': null,
        'sessions': 4,
      });
      await h.wire.post('/api/command', request, staffSession: h.staffSession);
      await h.wire.post('/api/command', request, staffSession: h.staffSession);
      expect(h.host.payments, hasLength(1));
      expect(h.host.packages, hasLength(1));
      await h.rawLogin(name: 'cashier', password: 'test-cashier-123');
      await expectLater(
        h.wire.post('/api/command', request, staffSession: h.staffSession),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.payments, hasLength(1));
    },
  );

  test(
    'remote close then reconciliation review and cash closing use same records',
    () async {
      await h.remote.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          mode: EntryMode.single,
        ),
      );
      await h.remote.checkPayment(
        studentId: h.student.id,
        sessionId: h.lesson.id,
        expectedAmount: 6000,
      );
      await h.remote.closeSession(h.lesson.id);
      await h.remote.savePaymentReview(
        ReviewRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          paymentId: h.remote.payments.single.id,
          paperAmount: 6000,
        ),
      );
      await h.remote.finalizeSession(sessionId: h.lesson.id, actualCash: 5900);
      expect(h.host.closings.single.actualCash, 5900);
      expect(h.remote.closings.single.actualCash, 5900);
      expect(h.host.reviews.single.expectedAmount, 6000);
      expect(h.host.paymentChecks, hasLength(1));
    },
  );

  test(
    'loopback bridge refuses missing secret and Go rejects unpaired requests',
    () async {
      final client = HttpClient();
      try {
        final request = await client.getUrl(h.bridge.uri.resolve('/api/state'));
        final response = await request.close();
        expect(response.statusCode, 403);
        await response.drain<void>();
      } finally {
        client.close(force: true);
      }
      final unpaired = LanTransport(h.ready.endpoint, '');
      await expectLater(
        unpaired.get('/api/state'),
        throwsA(isA<CenterException>()),
      );
      unpaired.close();
      await expectLater(
        h.remote.createBackup(),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        h.remote.restoreBackup('anything.json'),
        throwsA(isA<CenterException>()),
      );
    },
  );

  test(
    'revocation requires login and offline refresh never switches database',
    () async {
      await h.gateway.revokeDevice('client-one');
      await expectLater(
        h.remote.refreshRemote(),
        throwsA(isA<LanAuthorizationException>()),
      );
      expect(h.remote.currentUser, isNull);
      expect(h.remote.isRemote, true);
      expect(h.remote.students, isEmpty);
      expect(h.host.students, hasLength(1));
      expect(h.host.currentUser!.name, 'manager');
      await expectLater(
        h.remote.saveStudentNote(studentId: h.student.id, notes: 'offline'),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.students.single.notes, '');
    },
  );

  test(
    'signout wins a queued sign-in on both host and secondary device',
    () async {
      final remoteLogin = h.remote.signIn('cashier', 'test-cashier-123');
      h.remote.signOut();
      await remoteLogin;
      expect(h.remote.currentUser, isNull);
      expect(h.remote.students, isEmpty);
      final localLogin = h.host.signIn('manager', 'test-manager-123');
      h.host.signOut();
      await localLogin;
      expect(h.host.currentUser, isNull);
    },
  );

  test(
    'durable receipt survives SQLite reopening and new employee login',
    () async {
      await h.rawLogin();
      final request = h.request('renewPackage', {
        'studentId': h.student.id,
        'groupId': h.group.id,
        'method': 'نقدي',
        'notes': '',
        'sessionId': null,
        'sessions': 4,
      });
      await h.wire.post('/api/command', request, staffSession: h.staffSession);
      final oldToken = h.wire.deviceToken;
      final oldPort = h.ready.port;
      await h.remote.close();
      await h.gateway.stop();
      await h.bridge.close();
      await h.host.close();
      h.host = await CenterStore.open(directory: '${h.directory.path}/host');
      await h.host.signIn('manager', 'test-manager-123');
      h.bridge = await CenterStoreHostBridge.start(h.host);
      h.ready = await h.gateway.start(
        dataDirectory: '${h.directory.path}/gateway',
        upstreamUrl: 'http://127.0.0.1:${h.relay.port}',
        upstreamSecret: h.bridge.secret,
        name: 'السنتر',
        port: oldPort,
        discoveryPort: 0,
      );
      h.wire = LanTransport(h.ready.endpoint, oldToken);
      await h.rawLogin();
      final replay = await h.wire.post(
        '/api/command',
        request,
        staffSession: h.staffSession,
      );
      expect(replay['status'], 'committed');
      expect(h.host.payments, hasLength(1));
      expect(h.host.packages, hasLength(1));
      expect(h.host.packages.single.remaining, 4);
    },
  );

  test(
    'employee sessions cannot be borrowed by another paired device',
    () async {
      await h.rawLogin();
      final pairing = LanTransport(h.ready.endpoint, '');
      final result = await pairing.pair(
        h.ready.pairingCode,
        'client-two',
        'جهاز آخر',
      );
      pairing.close();
      final other = LanTransport(h.ready.endpoint, result['token'] as String);
      try {
        await expectLater(
          other.get('/api/state', staffSession: h.staffSession),
          throwsA(isA<LanAuthorizationException>()),
        );
      } finally {
        other.close();
      }
      expect(h.host.payments, isEmpty);
    },
  );

  test(
    'logout rejects a command whose headers preceded its delayed body',
    () async {
      await h.rawLogin();
      final command = h.request('renewPackage', {
        'studentId': h.student.id,
        'groupId': h.group.id,
        'method': 'نقدي',
        'notes': '',
        'sessionId': null,
        'sessions': 4,
      });
      final bytes = utf8.encode(jsonEncode(command));
      final client = HttpClient();
      try {
        final delayed = await client.postUrl(
          h.bridge.uri.resolve('/api/command'),
        );
        delayed.headers.contentType = ContentType.json;
        delayed.headers.set('X-Massar-Bridge-Secret', h.bridge.secret);
        delayed.headers.set('X-Massar-Device-ID', 'client-one');
        delayed.headers.set('X-Massar-Session', h.staffSession!);
        delayed.contentLength = bytes.length;
        delayed.add(bytes.sublist(0, 1));
        await delayed.flush();
        await h.wire.post('/api/logout', {}, staffSession: h.staffSession);
        delayed.add(bytes.sublist(1));
        final response = await delayed.close();
        expect(response.statusCode, 401);
        await response.drain<void>();
      } finally {
        client.close(force: true);
      }
      expect(h.host.payments, isEmpty);
      expect(h.host.packages, isEmpty);
    },
  );

  test(
    'unquoted direct entry cannot silently charge a newly changed price',
    () async {
      await h.host.saveGroup(h.group.copyWith(sessionPrice: 8000));
      await expectLater(
        h.remote.collectAndAttend(
          EntryRequest(
            studentId: h.student.id,
            sessionId: h.lesson.id,
            mode: EntryMode.single,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.payments, isEmpty);
      await h.remote.refreshRemote();
      await h.remote.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          mode: EntryMode.single,
        ),
      );
      expect(h.host.payments.single.netAmount, 8000);
    },
  );

  test(
    'package and card price previews reject stale cashier amounts',
    () async {
      await h.host.saveGroup(h.group.copyWith(packagePrice: 30000));
      await expectLater(
        h.remote.renewPackage(
          PackageRequest(studentId: h.student.id, groupId: h.group.id),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.payments, isEmpty);
      await h.host.saveCardSettings(const CenterCardSettings(price: 2000));
      await h.remote.refreshRemote();
      await h.host.saveCardSettings(const CenterCardSettings(price: 3000));
      await expectLater(
        h.remote.collectStudentCard(studentId: h.student.id),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.cardPayments, isEmpty);
      await h.remote.refreshRemote();
      await h.remote.collectStudentCard(studentId: h.student.id);
      expect(h.host.cardPayments.single.netAmount, 3000);
    },
  );

  test(
    'correction cannot reverse and recharge using a stale displayed price',
    () async {
      await h.remote.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          mode: EntryMode.single,
        ),
      );
      h.remote.signOut();
      await h.remote.signIn('manager', 'test-manager-123');
      final original = h.remote.attendances.single.id;
      await h.host.saveGroup(h.group.copyWith(sessionPrice: 8000));
      await expectLater(
        h.remote.correctEntry(
          attendanceId: original,
          mode: EntryMode.single,
          reason: 'تصحيح طريقة التسجيل',
        ),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.payments.single.netAmount, 6000);
      expect(h.host.refunds, isEmpty);
      expect(h.host.corrections, isEmpty);
      await h.remote.refreshRemote();
      await h.remote.correctEntry(
        attendanceId: original,
        mode: EntryMode.single,
        reason: 'تصحيح طريقة التسجيل',
      );
      expect(h.host.payments.single.netAmount, 8000);
      expect(h.host.refunds.single.amount, 6000);
    },
  );

  test(
    'changing connection is blocked until uncertain payment is settled',
    () async {
      h.loseNextCommandReply = true;
      await expectLater(
        h.remote.renewPackage(
          PackageRequest(studentId: h.student.id, groupId: h.group.id),
        ),
        throwsA(isA<LanConnectionException>()),
      );
      await expectLater(
        h.remote.prepareLanSwitch(),
        throwsA(isA<CenterException>()),
      );
      await h.remote.refreshRemote();
      await h.remote.prepareLanSwitch();
      expect(h.host.payments, hasLength(1));
      expect(
        await File(
          '${h.clientDirectory.path}/lan-pending-command.json',
        ).exists(),
        false,
      );
    },
  );
  test(
    'lost cancellation reply settles once and unpaid attendance can be collected remotely',
    () async {
      final request = EntryRequest(
        studentId: h.student.id,
        sessionId: h.lesson.id,
        mode: EntryMode.single,
      );
      await h.remote.collectAndAttend(request);
      final original = h.host.attendances.single;
      final payment = h.host.payments.single;
      h.loseNextCommandReply = true;
      await expectLater(
        h.remote.cancelPayment(paymentId: payment.id, reason: 'خطأ تحصيل'),
        throwsA(isA<LanConnectionException>()),
      );
      expect(h.host.refunds.single.amount, 6000);
      expect(h.host.attendances.single.id, original.id);
      await h.remote.refreshRemote();
      expect(h.remote.attendanceNeedsPayment(h.student.id, h.lesson.id), true);
      expect(h.host.refunds, hasLength(1));
      await h.remote.collectAndAttend(request);
      expect(h.host.attendances, hasLength(1));
      expect(h.host.payments, hasLength(1));
      expect(h.host.allPayments, hasLength(2));
      expect(h.host.refunds, hasLength(1));
      expect(h.host.currentUser!.name, 'manager');
    },
  );

  test(
    'stale cancellation effect cannot erase attendance paid after preview',
    () async {
      await h.remote.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          mode: EntryMode.single,
        ),
      );
      final attendance = h.remote.attendances.single;
      await h.host.cancelAttendance(
        attendanceId: attendance.id,
        reason: 'تصحيح محلي',
      );
      final before = h.host.corrections.length;
      await expectLater(
        h.remote.cancelAttendance(
          attendanceId: attendance.id,
          reason: 'تصحيح ثاني',
          mode: RecordCancellationMode.recordAndRelated,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.corrections, hasLength(before));
      expect(h.host.payments, hasLength(1));
      expect(h.host.refunds, isEmpty);
      await h.remote.refreshRemote();
      expect(
        h.remote.hasRetainedSessionPayment(h.student.id, h.lesson.id),
        true,
      );
      await h.remote.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          mode: EntryMode.single,
        ),
      );
      expect(h.host.payments, hasLength(1));
      expect(h.host.attendances, hasLength(1));
    },
  );

  test(
    'remote package payment with related attendance refunds atomically',
    () async {
      await h.remote.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          mode: EntryMode.package,
        ),
      );
      await h.remote.cancelPayment(
        paymentId: h.remote.payments.single.id,
        reason: 'إلغاء الباقة والحضور',
        mode: RecordCancellationMode.recordAndRelated,
      );
      expect(h.host.payments, isEmpty);
      expect(h.host.attendances, isEmpty);
      expect(h.host.packages, isEmpty);
      expect(h.host.refunds.single.amount, 24000);
      expect(h.remote.allPayments, hasLength(1));
      expect(h.remote.refunds, hasLength(1));
    },
  );
  test(
    'package cancellation preview cannot cancel a newly linked unreviewed attendance',
    () async {
      await h.remote.renewPackage(
        PackageRequest(
          studentId: h.student.id,
          groupId: h.group.id,
          sessionId: h.lesson.id,
        ),
      );
      final paymentId = h.remote.payments.single.id;
      await h.host.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: h.lesson.id,
          mode: EntryMode.package,
        ),
      );
      await expectLater(
        h.remote.cancelPayment(
          paymentId: paymentId,
          reason: 'إلغاء بعد مراجعة',
          mode: RecordCancellationMode.recordAndRelated,
        ),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.attendances, hasLength(1));
      expect(h.host.payments, hasLength(1));
      expect(h.host.packages.single.remaining, 3);
      expect(h.host.refunds, isEmpty);
      await h.remote.refreshRemote();
      await h.remote.cancelPayment(
        paymentId: paymentId,
        reason: 'راجعنا الحضور المرتبط',
        mode: RecordCancellationMode.recordAndRelated,
      );
      expect(h.host.attendances, isEmpty);
      expect(h.host.payments, isEmpty);
      expect(h.host.refunds, hasLength(1));
    },
  );
  test(
    'two devices cannot register same student in parallel same-number groups without review',
    () async {
      final original = h.lesson;
      final other = await h.addParallelGroup();
      await h.remote.refreshRemote();
      final outcomes = await Future.wait(
        [
          h.host.collectAndAttend(
            EntryRequest(
              studentId: h.student.id,
              sessionId: original.id,
              mode: EntryMode.single,
            ),
          ),
          h.remote.collectAndAttend(
            EntryRequest(
              studentId: h.student.id,
              sessionId: other.id,
              mode: EntryMode.single,
            ),
          ),
        ].map((f) => f.then<Object?>((_) => null, onError: (Object e) => e)),
      );
      expect(outcomes.whereType<CenterException>(), hasLength(1));
      expect(h.host.attendances, hasLength(1));
      expect(h.host.payments, hasLength(1));
      final first = h.host.attendances.single;
      final target = first.sessionId == original.id ? other : original;
      await h.remote.refreshRemote();
      expect(
        h.remote.attendanceConflictsFor(h.student.id, target.id).single.id,
        first.id,
      );
      h.loseNextCommandReply = true;
      await expectLater(
        h.remote.collectAndAttend(
          EntryRequest(
            studentId: h.student.id,
            sessionId: target.id,
            mode: EntryMode.single,
            acknowledgedAttendanceIds: [first.id],
          ),
        ),
        throwsA(isA<LanConnectionException>()),
      );
      expect(h.host.attendances, hasLength(2));
      expect(h.host.payments, hasLength(2));
      await h.remote.refreshRemote();
      expect(h.remote.attendances, hasLength(2));
      expect(h.host.payments, hasLength(2));
      expect(
        h.host.audit.where((e) => e.action == 'entry').last.description,
        contains(first.id),
      );
    },
  );

  test(
    'a canceled cross-group attendance invalidates an older LAN review without charging',
    () async {
      final original = h.lesson;
      final other = await h.addParallelGroup();
      await h.host.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: original.id,
          mode: EntryMode.single,
        ),
      );
      await h.remote.refreshRemote();
      final reviewed = h.remote
          .attendanceConflictsFor(h.student.id, other.id)
          .map((e) => e.id)
          .toList();
      await h.host.cancelAttendance(
        attendanceId: reviewed.single,
        reason: 'حضور خاطئ',
      );
      await expectLater(
        h.remote.collectAndAttend(
          EntryRequest(
            studentId: h.student.id,
            sessionId: other.id,
            mode: EntryMode.single,
            acknowledgedAttendanceIds: reviewed,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(h.host.attendances, isEmpty);
      expect(h.host.payments, hasLength(1));
      await h.remote.refreshRemote();
      await h.remote.collectAndAttend(
        EntryRequest(
          studentId: h.student.id,
          sessionId: other.id,
          mode: EntryMode.single,
        ),
      );
      expect(h.host.attendances, hasLength(1));
      expect(h.host.payments, hasLength(2));
    },
  );
}
