import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Use inside runAsync when waiting for real SQLite, file or credential work.
Future<void> waitForUiCondition(
  WidgetTester tester,
  bool Function() condition, {
  required String reason,
}) async {
  final deadline = DateTime.now().add(const Duration(seconds: 5));
  while (!condition() && DateTime.now().isBefore(deadline)) {
    await Future<void>.delayed(const Duration(milliseconds: 20));
    await tester.pump(const Duration(milliseconds: 20));
  }
  expect(condition(), isTrue, reason: reason);
  await tester.pumpAndSettle();
}

Future<void> navigateManagementPage(WidgetTester tester, String label) async {
  final navigation = find.descendant(
    of: find.byKey(const Key('management-navigation')),
    matching: find.byType(Scrollable),
  );
  final state = tester.state<ScrollableState>(navigation);
  state.position.jumpTo(0);
  await tester.pump();
  final target = find.widgetWithText(ListTile, label);
  await tester.scrollUntilVisible(target, 150, scrollable: navigation);
  await tester.pumpAndSettle();
  await tester.tap(target);
  await tester.pumpAndSettle();
}
