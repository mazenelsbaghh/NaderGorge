import 'dart:async';

import 'package:flutter/scheduler.dart';

import 'problem_log.dart';

/// Timings contain fixed stage names and counts, never operational identifiers.
class PerformanceTrace {
  PerformanceTrace(this.operation, {this.budgetMs = 150});

  final String operation;
  final int budgetMs;
  final _watch = Stopwatch()..start();
  final _phases = <String, int>{};
  final counts = <String, int>{};
  int _previousUs = 0;
  bool _finished = false;

  void stage(String name) {
    final elapsed = _watch.elapsedMicroseconds;
    _phases[name] = (_phases[name] ?? 0) + elapsed - _previousUs;
    _previousUs = elapsed;
  }

  static Future<T> measureAsync<T>(
    String operation,
    Future<T> Function() work, {
    int budgetMs = 150,
  }) async {
    final trace = PerformanceTrace(operation, budgetMs: budgetMs);
    var failed = false;
    try {
      return await work();
    } catch (_) {
      failed = true;
      rethrow;
    } finally {
      trace.stage('work');
      trace.finish(failed: failed);
    }
  }

  void finish({bool failed = false}) {
    if (_finished) return;
    _finished = true;
    _watch.stop();
    final elapsed = _watch.elapsedMicroseconds;
    if (elapsed < budgetMs * 1000) return;
    final log = ProblemLog.current;
    if (log == null) return;
    unawaited(
      log.recordPerformance(
        operation: operation,
        durationUs: elapsed,
        budgetMs: budgetMs,
        phasesUs: _phases,
        counts: counts,
        failed: failed,
      ),
    );
  }
}

class PerformanceMonitor {
  PerformanceMonitor() {
    SchedulerBinding.instance.addTimingsCallback(_frames);
    _timer = Timer.periodic(const Duration(seconds: 1), (_) => _tick());
  }

  final _clock = Stopwatch()..start();
  Timer? _timer;
  int _lastUs = 0;

  void _tick() {
    final now = _clock.elapsedMicroseconds;
    final delay = now - _lastUs - 1000000;
    _lastUs = now;
    if (delay < 150000) return;
    unawaited(
      ProblemLog.current?.recordPerformance(
            operation: 'ui.event_loop',
            durationUs: delay,
            budgetMs: 150,
          ) ??
          Future<void>.value(),
    );
  }

  void _frames(List<FrameTiming> frames) {
    for (final frame in frames) {
      if (frame.totalSpan.inMilliseconds < 50) continue;
      unawaited(
        ProblemLog.current?.recordPerformance(
              operation: 'ui.frame',
              durationUs: frame.totalSpan.inMicroseconds,
              budgetMs: 50,
              phasesUs: {
                'build': frame.buildDuration.inMicroseconds,
                'raster': frame.rasterDuration.inMicroseconds,
              },
            ) ??
            Future<void>.value(),
      );
    }
  }

  void close() {
    _timer?.cancel();
    SchedulerBinding.instance.removeTimingsCallback(_frames);
    _clock.stop();
  }
}
