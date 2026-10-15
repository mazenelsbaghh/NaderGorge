import 'exam_network_maintenance.dart';
import 'exam_room_controller.dart';

/// The exam engine's durable lease prevents a room opening between checking
/// its state and provisioning the access point. It does not lock reception.
class OmadaExamNetworkGuard {
  OmadaExamNetworkGuard(this.controller);
  final ExamRoomController controller;
  String? releaseWarning;

  Future<T> run<T>(
    Future<T> Function(Future<void> Function() beforeWrite) change,
  ) async {
    releaseWarning = null;
    await controller.prepareNetworkEngine();
    final maintenance = ExamNetworkMaintenance(controller.engine);
    try {
      return await maintenance.run(change);
    } finally {
      releaseWarning = maintenance.releaseWarning;
    }
  }
}
