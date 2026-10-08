import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/features/auth/auth_screen.dart';
import 'package:massar_center/shared/theme.dart';
import '../helpers/ui_wait_helpers.dart';

void main() {
  testWidgets(
    'failed real sign-in shows inline error and preserves login draft',
    (tester) async {
      late Directory directory;
      late CenterStore store;
      await tester.runAsync(() async {
        directory = await Directory.systemTemp.createTemp(
          'massar-auth-notice-',
        );
        store = await CenterStore.open(directory: directory.path);
        await store.setupAdmin('الاستقبال', 'actual-password-2026');
        store.signOut();
      });
      addTearDown(() async {
        await tester.runAsync(() async {
          await store.close();
          await directory.delete(recursive: true);
        });
      });
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.dark,
          home: AuthScreen(store: store),
        ),
      );
      await tester.enterText(find.byKey(const Key('auth-name')), 'الاستقبال');
      await tester.enterText(
        find.byKey(const Key('auth-password')),
        'wrong-password-2026',
      );
      await tester.runAsync(() async {
        await tester.tap(find.byKey(const Key('auth-submit')));
        await waitForUiCondition(
          tester,
          () => find
              .text('اسم المستخدم أو كلمة المرور غير صحيحة.')
              .evaluate()
              .isNotEmpty,
          reason: 'The rejected sign-in displays its inline error.',
        );
      });
      expect(store.currentUser, isNull);
      expect(
        tester
            .widget<TextFormField>(find.byKey(const Key('auth-name')))
            .controller!
            .text,
        'الاستقبال',
      );
      expect(
        tester
            .widget<TextFormField>(find.byKey(const Key('auth-password')))
            .controller!
            .text,
        'wrong-password-2026',
      );
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('auth-submit')))
            .onPressed,
        isNotNull,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
