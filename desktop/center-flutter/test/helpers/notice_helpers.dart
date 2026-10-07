import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

/// Call inside runAsync so real SQLite completion can create the event notice.
Future<void> acknowledgeNotice(WidgetTester tester, {String? message}) async {
  final dialog = find.byKey(const Key('massar-notice-dialog'));
  for (var attempt = 0; attempt < 100; attempt++) {
    await Future<void>.delayed(const Duration(milliseconds: 20));
    await tester.pump(const Duration(milliseconds: 50));
    if (dialog.evaluate().isNotEmpty) break;
  }
  await tester.pump(const Duration(milliseconds: 200));
  expect(dialog, findsOneWidget);
  final body = find.descendant(
    of: dialog,
    matching: find.byKey(const Key('massar-notice-message')),
  );
  expect(body, findsOneWidget);
  if (message != null) {
    expect(
      find.descendant(of: body, matching: find.text(message)),
      findsOneWidget,
    );
  }
  await tester.sendKeyDownEvent(LogicalKeyboardKey.escape);
  await tester.pump();
  expect(dialog, findsOneWidget, reason: 'Notice closes only on fresh key-up.');
  await tester.sendKeyUpEvent(LogicalKeyboardKey.escape);
  await tester.pumpAndSettle();
  await Future<void>.delayed(const Duration(milliseconds: 20));
  await tester.pumpAndSettle();
  expect(dialog, findsNothing);
}
