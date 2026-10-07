import 'dart:async';
import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/main.dart';

void main() {
  testWidgets(
    'first run creates local admin and navigates management to focus without internet',
    (tester) async {
      await tester.runAsync(() async {
        final directory = await Directory.systemTemp.createTemp(
          'massar-first-run-',
        );
        final store = await CenterStore.open(directory: directory.path);
        addTearDown(
          () => TestWidgetsFlutterBinding.instance.runAsync(() async {
            await store.close();
            await directory.delete(recursive: true);
          }),
        );
        await tester.binding.setSurfaceSize(const Size(1440, 900));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        await tester.pumpWidget(CenterApp(store: store));
        await tester.pumpAndSettle();
        expect(find.text('إنشاء حساب الإدارة على هذا الجهاز'), findsOneWidget);
        expect(store.students, isEmpty);
        await tester.enterText(find.byKey(const Key('auth-name')), 'نادر');
        await tester.enterText(
          find.byKey(const Key('auth-password')),
          'first-admin-pass',
        );
        await tester.enterText(
          find.byKey(const Key('auth-confirm')),
          'first-admin-pass',
        );
        final created = Completer<void>();
        void observeAdmin() {
          if (store.currentUser != null && !created.isCompleted) {
            created.complete();
          }
        }

        store.addListener(observeAdmin);
        await tester.tap(find.byKey(const Key('auth-submit')));
        await created.future.timeout(const Duration(seconds: 15));
        store.removeListener(observeAdmin);
        await tester.pumpAndSettle();
        expect(find.text('إنشاء حساب الإدارة على هذا الجهاز'), findsNothing);
        expect(find.text('أساس النظام'), findsWidgets);
        expect(tester.takeException(), isNull);
        for (final section in ['التقارير', 'مراجعة', 'تقفيلة الحسابات']) {
          final sectionTile = find.widgetWithText(ListTile, section);
          await tester.scrollUntilVisible(
            sectionTile,
            100,
            scrollable: find.descendant(
              of: find.byKey(const Key('management-navigation')),
              matching: find.byType(Scrollable),
            ),
          );
          await tester.ensureVisible(sectionTile);
          await tester.pumpAndSettle();
          await tester.tap(sectionTile);
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
          expect(store.payments, isEmpty);
        }
        await tester.tap(find.text('التحضير والتحصيل'));
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('student-search')), findsOneWidget);
        expect(
          find.byKey(const Key('session-attendance-counter')),
          findsOneWidget,
        );
        expect(find.textContaining('وضع التركيز'), findsOneWidget);
        expect(find.text('الموظفون'), findsNothing);
        expect(tester.takeException(), isNull);
      });
    },
  );
}
