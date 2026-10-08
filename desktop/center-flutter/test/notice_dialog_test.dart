import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import 'package:massar_center/shared/theme.dart';

class _RouteObserver extends NavigatorObserver {
  Route<dynamic>? last;
  @override
  void didPush(Route<dynamic> route, Route<dynamic>? previousRoute) {
    last = route;
  }
}

void main() {
  late BuildContext caller;
  late FocusNode codeFocus;
  late TextEditingController code;
  late _RouteObserver observer;
  var underlyingEntries = 0;
  Future<void> open(
    WidgetTester tester, {
    bool dark = false,
    bool reduced = false,
  }) async {
    codeFocus = FocusNode();
    code = TextEditingController(text: 'MS-123');
    observer = _RouteObserver();
    underlyingEntries = 0;
    addTearDown(codeFocus.dispose);
    addTearDown(code.dispose);
    await tester.pumpWidget(
      MaterialApp(
        theme: dark ? MassarTheme.dark : MassarTheme.light,
        navigatorObservers: [observer],
        home: MediaQuery(
          data: MediaQueryData(disableAnimations: reduced),
          child: Builder(
            builder: (context) {
              caller = context;
              return Scaffold(
                body: Focus(
                  onKeyEvent: (node, event) {
                    if (event is KeyDownEvent &&
                        event.logicalKey == LogicalKeyboardKey.enter) {
                      underlyingEntries++;
                    }
                    return KeyEventResult.ignored;
                  },
                  child: TextField(
                    controller: code,
                    focusNode: codeFocus,
                    autofocus: true,
                  ),
                ),
              );
            },
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    code.selection = const TextSelection(baseOffset: 0, extentOffset: 6);
  }

  for (final dark in [false, true]) {
    testWidgets(
      'notice has RTL semantic error colors and respects reduced motion dark=$dark',
      (tester) async {
        await open(tester, dark: dark, reduced: true);
        unawaited(
          showMassarNotice(caller, 'لم يُحفظ الدفع.', kind: NoticeKind.error),
        );
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('massar-notice-dialog')), findsOneWidget);
        final context = tester.element(
          find.byKey(const Key('massar-notice-dialog')),
        );
        expect(Directionality.of(context), TextDirection.rtl);
        final box = tester.widget<Container>(
          find.byKey(const Key('massar-notice-message')),
        );
        expect(
          (box.decoration as BoxDecoration).color,
          dark
              ? MassarPalette.dark.errorSurface
              : MassarPalette.light.errorSurface,
        );
        expect(
          (observer.last as TransitionRoute).transitionDuration,
          Duration.zero,
        );
        await tester.sendKeyEvent(LogicalKeyboardKey.escape);
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
        expect(codeFocus.hasFocus, isTrue);
        expect(code.text, 'MS-123');
        expect(
          code.selection,
          const TextSelection(baseOffset: 0, extentOffset: 6),
        );
      },
    );
  }

  testWidgets('success leaves scanner focus and notice queue available', (
    tester,
  ) async {
    await open(tester);
    await showMassarNotice(caller, 'تم التحصيل.', kind: NoticeKind.success);
    await tester.pumpAndSettle();
    expect(hasPendingMassarNotice(caller), isFalse);
    expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
    expect(codeFocus.hasFocus, isTrue);
    expect(code.text, 'MS-123');
  });

  testWidgets(
    'queue shows one notice at a time and held Enter or scanner cannot reach entry underneath',
    (tester) async {
      await open(tester);
      unawaited(
        showMassarNotice(caller, 'راجع التحصيل.', kind: NoticeKind.info),
      );
      expect(hasPendingMassarNotice(caller), isTrue);
      unawaited(
        showMassarNotice(caller, 'راجع الكود.', kind: NoticeKind.warning),
      );
      await tester.pumpAndSettle();
      expect(find.text('راجع التحصيل.'), findsOneWidget);
      expect(find.text('راجع الكود.'), findsNothing);
      expect(
        (observer.last as TransitionRoute).transitionDuration,
        const Duration(milliseconds: 150),
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyEvent(LogicalKeyboardKey.digit2);
      await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(find.text('راجع التحصيل.'), findsOneWidget);
      expect(underlyingEntries, 0);
      expect(code.text, 'MS-123');
      await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('massar-notice-dialog')), findsOneWidget);
      expect(find.text('راجع الكود.'), findsOneWidget);
      expect(hasPendingMassarNotice(caller), isTrue);
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      expect(underlyingEntries, 0);
      expect(hasPendingMassarNotice(caller), isFalse);
      expect(codeFocus.hasFocus, isTrue);
    },
  );
  testWidgets('long notice scrolls by keyboard without releasing shortcuts', (
    tester,
  ) async {
    await open(tester);
    unawaited(
      showMassarNotice(
        caller,
        List.generate(90, (i) => 'تفاصيل المراجعة $i').join('\n'),
      ),
    );
    await tester.pumpAndSettle();
    final scroll = tester.widget<SingleChildScrollView>(
      find.descendant(
        of: find.byKey(const Key('massar-notice-dialog')),
        matching: find.byType(SingleChildScrollView),
      ),
    );
    expect(scroll.controller!.offset, 0);
    await tester.sendKeyEvent(LogicalKeyboardKey.pageDown);
    await tester.pump();
    expect(scroll.controller!.offset, greaterThan(0));
    expect(underlyingEntries, 0);
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pumpAndSettle();
    expect(hasPendingMassarNotice(caller), isFalse);
  });

  testWidgets('queued notice skips disposed caller and always clears pending', (
    tester,
  ) async {
    final visible = ValueNotifier(true);
    addTearDown(visible.dispose);
    late BuildContext outer;
    late BuildContext removable;
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) {
            outer = context;
            return ValueListenableBuilder<bool>(
              valueListenable: visible,
              builder: (_, value, _) {
                return value
                    ? Builder(
                        builder: (context) {
                          removable = context;
                          return const SizedBox();
                        },
                      )
                    : const SizedBox();
              },
            );
          },
        ),
      ),
    );
    unawaited(showMassarNotice(removable, 'الأول'));
    unawaited(showMassarNotice(removable, 'لن يظهر بعد إغلاق الصفحة'));
    expect(hasPendingMassarNotice(outer), isTrue);
    await tester.pumpAndSettle();
    visible.value = false;
    await tester.pump();
    expect(removable.mounted, isFalse);
    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
    expect(hasPendingMassarNotice(outer), isFalse);
    expect(tester.takeException(), isNull);
  });
}
