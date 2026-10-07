import 'dart:async';
import 'package:flutter/foundation.dart';
import 'problem_log.dart';

void reportProblem(
  Object error,
  StackTrace stackTrace, {
  required String operation,
}) {
  final log = ProblemLog.current;
  if (log != null) {
    unawaited(log.record(error, stackTrace, operation: operation));
  }
}

/// Keep Flutter's existing presentation and the engine's error policy intact.
VoidCallback installProblemHandlers() {
  final previousFlutter = FlutterError.onError;
  final previousPlatform = PlatformDispatcher.instance.onError;
  FlutterError.onError = (details) {
    reportProblem(
      details.exception,
      details.stack ?? StackTrace.current,
      operation: 'flutter.framework',
    );
    (previousFlutter ?? FlutterError.presentError)(details);
  };
  PlatformDispatcher.instance.onError = (error, stackTrace) {
    reportProblem(error, stackTrace, operation: 'flutter.platform');
    return previousPlatform?.call(error, stackTrace) ?? false;
  };
  return () {
    FlutterError.onError = previousFlutter;
    PlatformDispatcher.instance.onError = previousPlatform;
  };
}
