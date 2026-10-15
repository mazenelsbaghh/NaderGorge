import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/exam_engine.dart';
import 'package:massar_center/exams/exam_room_controller.dart';

// Only the private native process boundary is replaced. The client store is real
// and must never open a center database, even after local exam access is enabled.
class _LocalExamProcess implements ExamEngine {
  @override
  bool running = false;
  final adminAccess = <bool>[];
  final routes = <String>[];

  @override
  Future<void> start() async => running = true;
  @override
  Future<bool> reconnect() async => running;
  @override
  Future<void> close() async => running = false;
  @override
  Future<Map<String, dynamic>> network(Map<String, dynamic> command) =>
      throw StateError('Local exam access does not change the network.');
  @override
  Future<Map<String, dynamic>> request(
    String route, [
    Map<String, dynamic>? body,
  ]) async {
    routes.add(route);
    return switch (route) {
      '/api/center/state' => {
        'protocol': 1,
        'sourceId': 'local-exam-fixture',
        'exams': <Map<String, dynamic>>[],
      },
      '/api/center/admin-access' => () {
        adminAccess.add(body!['enabled'] as bool);
        return {'adminUrl': 'http://127.0.0.1:8766/'};
      }(),
      _ => throw StateError('Unexpected private native route: $route'),
    };
  }
}

void main() {
  test(
    'fresh secondary explicitly opens local exams while host data and reception stay unavailable',
    () async {
      final directory = await Directory.systemTemp.createTemp('local-exam-');
      final store = CenterStore.clientWorkspace(directory: directory.path);
      final engine = _LocalExamProcess();
      final controller = ExamRoomController(
        engine: engine,
        store: store,
        clientOnly: true,
        pollInterval: const Duration(days: 1),
      );
      addTearDown(() async {
        controller.dispose();
        await store.close();
        await directory.delete(recursive: true);
      });
      await controller.initialize();
      expect(controller.canAdmin, isFalse);
      await expectLater(
        controller.openAdmin(),
        throwsA(isA<CenterException>()),
      );
      expect(engine.adminAccess, isNot(contains(true)));

      await controller.enterLocalExamMode();
      expect(await controller.openAdmin(), 'http://127.0.0.1:8766/');
      expect(controller.canAdmin, isTrue);
      expect(controller.canSync, isFalse);
      expect(controller.offlinePrepared, isFalse);
      expect(store.currentUser, isNull);
      expect(store.canAssess, isFalse);
      await expectLater(
        controller.bind(
          examId: 'local-room',
          groupId: 'host-group',
          sessionId: 'host-session',
          activityId: 'host-exam',
        ),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.recordAttendance(
          const EntryRequest(
            studentId: 'host-student',
            sessionId: 'host-session',
            mode: EntryMode.single,
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(engine.routes, isNot(contains('/api/center/bind')));
      expect(await File('${directory.path}/center.sqlite').exists(), isFalse);
    },
  );

  test(
    'host cannot replace employee authentication with local exam mode',
    () async {
      final directory = await Directory.systemTemp.createTemp(
        'host-exam-auth-',
      );
      final store = await CenterStore.open(directory: directory.path);
      final controller = ExamRoomController(
        engine: _LocalExamProcess(),
        store: store,
      );
      addTearDown(() async {
        controller.dispose();
        await store.close();
        await directory.delete(recursive: true);
      });
      await expectLater(
        controller.enterLocalExamMode(),
        throwsA(isA<CenterException>()),
      );
      expect(controller.canAdmin, isFalse);
      expect(controller.localExamMode, isFalse);
      expect(controller.canSync, isFalse);
    },
  );
}
