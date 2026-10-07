import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/shared/problem_log.dart';
import 'package:massar_center/shared/problem_log_health.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  late Directory directory;
  ProblemLog? previousLog;
  setUp(() async {
    previousLog = ProblemLog.current;
    directory = await Directory.systemTemp.createTemp('massar-log-health-');
  });
  tearDown(() async {
    await ProblemLog.current?.flush();
    ProblemLog.current = previousLog;
    await directory.delete(recursive: true);
  });

  Future<void> open(WidgetTester tester) => tester.pumpWidget(
    MaterialApp(
      theme: MassarTheme.dark,
      home: const ProblemLogHealth(child: Scaffold(body: Text('التحضير شغال'))),
    ),
  );

  testWidgets('healthy diagnostics stay quiet and never interrupt work', (
    tester,
  ) async {
    ProblemLog.current = ProblemLog(Directory('${directory.path}/logs'));
    await tester.runAsync(() => ProblemLog.current!.startSession());
    await open(tester);
    await tester.pumpAndSettle();
    expect(find.text('التحضير شغال'), findsOneWidget);
    expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets(
    'runtime log storage failure warns once without breaking the page',
    (tester) async {
      await tester.runAsync(() async {
        final log = ProblemLog(Directory('${directory.path}/logs'));
        ProblemLog.current = log;
        await open(tester);
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
        await File('${directory.path}/logs').writeAsString('blocked');
        await log.record(
          StateError('private fixture'),
          StackTrace.current,
          operation: 'entry',
        );
        await tester.pumpAndSettle();
        expect(log.writeFailure, isNotNull);
        expect(find.textContaining('الشغل مستمر'), findsOneWidget);
        expect(find.byKey(const Key('massar-notice-dialog')), findsOneWidget);
        await tester.sendKeyEvent(LogicalKeyboardKey.escape);
        await tester.pumpAndSettle();
        await log.record(
          StateError('another failure'),
          StackTrace.current,
          operation: 'entry',
        );
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
        expect(find.text('التحضير شغال'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
        await log.flush();
      });
    },
  );
}
