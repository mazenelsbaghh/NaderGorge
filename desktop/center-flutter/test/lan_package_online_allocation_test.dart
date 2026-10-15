import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/lan/center_store_host_bridge.dart';
import 'package:massar_center/lan/lan_host_process.dart';
import 'package:massar_center/lan/lan_transport.dart';

/// Records an actual network request so its exact durable command can be replayed.
/// Pairing, TLS, authentication, host dispatch and persistence remain real.
class _ReplayTransport extends LanTransport {
  _ReplayTransport(super.endpoint, super.deviceToken);

  Map<String, dynamic>? command, receipt;
  String? session, version;
  bool patches = false;

  @override
  Future<Map<String, dynamic>> post(
    String path,
    Map<String, dynamic> body, {
    String? staffSession,
    String? stateVersion,
    bool statePatches = false,
  }) async {
    final response = await super.post(
      path,
      body,
      staffSession: staffSession,
      stateVersion: stateVersion,
      statePatches: statePatches,
    );
    if (path == '/api/command') {
      command = jsonDecode(jsonEncode(body)) as Map<String, dynamic>;
      receipt = response;
      session = staffSession;
      version = stateVersion;
      patches = statePatches;
    }
    return response;
  }

  Future<Map<String, dynamic>> replay() => super.post(
    '/api/command',
    command!,
    staffSession: session,
    stateVersion: version,
    statePatches: patches,
  );
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test(
    'paired secondary allocates a closed monthly receipt once, replays its durable result and rejects an assistant without opening client SQLite',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'massar-lan-online-allocation-',
      );
      CenterStore? host, remote;
      CenterStoreHostBridge? bridge;
      LanHostProcess? gateway;
      _ReplayTransport? wire;
      try {
        final executable =
            Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'] ??
            '${directory.path}/massar-lan-host${Platform.isWindows ? '.exe' : ''}';
        if (Platform.environment['MASSAR_LAN_TEST_EXECUTABLE'] == null) {
          final built = await Process.run('go', [
            'build',
            '-o',
            executable,
            '.',
          ], workingDirectory: '../center-lan');
          expect(built.exitCode, 0, reason: '${built.stderr}');
        }
        host = await CenterStore.open(directory: '${directory.path}/host');
        await host.setupAdmin('manager', 'test-manager-123');
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
            name: 'مجموعة',
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
            packagePrice: 21000,
          ),
        );
        await host.saveStudent(
          Student(
            name: 'طالبة',
            code: 'P1',
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
        final student = host.students.single;
        final lesson = host.sessions.single;
        await host.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: lesson.id,
            mode: EntryMode.package,
          ),
        );
        final payment = host.payments.single;
        await host.closeSession(lesson.id);
        await host.finalizeSession(sessionId: lesson.id, actualCash: 21000);
        final originalClosing = jsonEncode(host.closings.single.toJson());
        bridge = await CenterStoreHostBridge.start(host);
        gateway = LanHostProcess(executablePath: executable);
        final ready = await gateway.start(
          dataDirectory: '${directory.path}/gateway',
          upstreamUrl: bridge.uri.toString(),
          upstreamSecret: bridge.secret,
          name: 'السنتر',
          port: 0,
          discoveryPort: 0,
        );
        final pairing = LanTransport(ready.endpoint, '');
        late Map<String, dynamic> paired;
        try {
          paired = await pairing.pair(
            ready.pairingCode,
            'allocation-client',
            'الساكند',
          );
        } finally {
          pairing.close();
        }
        wire = _ReplayTransport(ready.endpoint, paired['token'] as String);
        final clientDirectory = await Directory(
          '${directory.path}/client',
        ).create();
        remote = CenterStore.remote(wire, localDirectory: clientDirectory.path);
        await remote.signIn('cashier', 'test-cashier-123');

        await remote.allocatePackageToOnline(
          paymentId: payment.id,
          amount: 6000,
          expectedOnlineAmount: 0,
          reason: 'تعويض أونلاين من الساكند',
        );
        expect(remote.onlineAllocatedAmount(payment.id), 6000);
        expect(host.onlineAllocatedAmount(payment.id), 6000);
        expect(remote.centerAllocatedAmount(payment.id), 15000);
        expect(host.corrections.single.staffId, remote.currentUser!.id);
        expect(jsonEncode(host.closings.single.toJson()), originalClosing);
        expect(host.sessionFinancialSummary(lesson.id).totalCollected, 21000);
        expect(host.payments.single.collectedAmount, 21000);
        expect(host.refunds, isEmpty);

        final receipt = wire.receipt;
        final beforeReplay = jsonEncode(host.corrections.single.toJson());
        final auditCount = host.audit.length;
        expect(await wire.replay(), receipt);
        expect(await wire.replay(), receipt);
        expect(host.onlineAllocatedAmount(payment.id), 6000);
        expect(jsonEncode(host.corrections.single.toJson()), beforeReplay);
        expect(host.audit, hasLength(auditCount));
        await remote.refreshRemote();
        expect(remote.onlineAllocatedAmount(payment.id), 6000);

        remote.signOut();
        await remote.signIn('assistant', 'test-assistant-123');
        await expectLater(
          remote.allocatePackageToOnline(
            paymentId: payment.id,
            amount: 1000,
            expectedOnlineAmount: 6000,
            reason: 'ليس له صلاحية',
          ),
          throwsA(isA<CenterException>()),
        );
        expect(host.onlineAllocatedAmount(payment.id), 6000);
        expect(host.corrections, hasLength(1));
        expect(host.audit, hasLength(auditCount));
        expect(File(remote.databasePath).existsSync(), isFalse);
        expect(
          clientDirectory
              .listSync(recursive: true)
              .whereType<File>()
              .where(
                (file) => RegExp(r'\.(sqlite|db)(-|$)').hasMatch(file.path),
              ),
          isEmpty,
        );
      } finally {
        await remote?.close();
        wire?.close();
        await gateway?.dispose();
        await bridge?.close();
        await host?.close();
        await directory.delete(recursive: true);
      }
    },
    timeout: const Timeout(Duration(minutes: 2)),
  );
}
