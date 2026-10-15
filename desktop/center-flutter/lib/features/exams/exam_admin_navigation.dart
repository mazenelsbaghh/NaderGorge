import 'dart:convert';

import 'package:flutter/foundation.dart';

import '../../exams/exam_room_controller.dart';
import '../../shared/problem_reporting.dart';

bool examAdminNavigationSessionValid(
  ExamRoomController controller, {
  required int? generation,
  required String? actor,
}) =>
    generation != null &&
    controller.canAdmin &&
    (generation == controller.authorizationGeneration ||
        controller.clientOnly &&
            controller.localExamMode &&
            !controller.canSync &&
            actor == controller.store.currentUser?.id);

/// Only visual destinations cross this bridge; it grants no engine access.
class ExamAdminNavigation extends ChangeNotifier {
  static const destinations = {
    'room',
    'exams',
    'sheets',
    'catalog',
    'whatsapp',
    'storage',
    'guide',
  };

  String _destination = 'room', _requestedDestination = 'room';
  int _requestSerial = 0;
  bool _leaving = false, _disposed = false;
  Object? _guardOwner;
  Future<bool> Function()? _leaveGuard;

  String get destination => _destination;
  String get requestedDestination => _requestedDestination;
  int get requestSerial => _requestSerial;
  bool get leaving => _leaving;

  void request(String destination) {
    if (_disposed || _leaving || !destinations.contains(destination)) return;
    _requestedDestination = destination;
    _requestSerial++;
    notifyListeners();
  }

  void accept(String destination) {
    if (_disposed ||
        !destinations.contains(destination) ||
        _destination == destination) {
      return;
    }
    _destination = destination;
    notifyListeners();
  }

  void bindLeaveGuard(Object owner, Future<bool> Function() guard) {
    _guardOwner = owner;
    _leaveGuard = guard;
  }

  void unbindLeaveGuard(Object owner) {
    if (!identical(owner, _guardOwner)) return;
    _guardOwner = null;
    _leaveGuard = null;
  }

  Future<bool> confirmLeave() async {
    if (_disposed || _leaving) return false;
    _leaving = true;
    notifyListeners();
    try {
      final result = await (_leaveGuard?.call() ?? Future.value(true));
      return !_disposed && result;
    } catch (error, stack) {
      reportProblem(error, stack, operation: 'exam.navigation_leave');
      return false;
    } finally {
      _leaving = false;
      if (!_disposed) notifyListeners();
    }
  }

  @override
  void dispose() {
    _disposed = true;
    _leaveGuard = null;
    super.dispose();
  }
}

Map<String, dynamic>? examAdminNavigationMessage(
  dynamic message, {
  required String nonce,
  String? documentId,
}) {
  if (message is! Map ||
      message['kind'] != 'massar-center-navigation' ||
      message['nonce'] != nonce ||
      message['documentId'] is! String ||
      !RegExp(r'^[a-f0-9]{32}$').hasMatch(message['documentId'] as String)) {
    return null;
  }
  final action = message['action'];
  if (action == 'ready' || action == 'state') {
    if (!ExamAdminNavigation.destinations.contains(message['destination'])) {
      return null;
    }
  } else if (action == 'leave-result') {
    if (message['allowed'] is! bool ||
        message['requestId'] is! int ||
        (message['requestId'] as int) < 1) {
      return null;
    }
  } else {
    return null;
  }
  if (action != 'ready' && message['documentId'] != documentId) return null;
  return Map<String, dynamic>.from(message);
}

String examAdminNavigationScript(String nonce) =>
    '''(() => {
  if (location.origin !== 'http://127.0.0.1:8766' || window.top !== window ||
      new URLSearchParams(location.search).get('embedded') !== 'center' ||
      new URLSearchParams(location.search).get('native-shell') !== '1') return;
  window.__massarCenterNavigationNonce = ${jsonEncode(nonce)};
})();''';
