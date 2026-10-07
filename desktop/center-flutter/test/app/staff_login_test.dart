import 'dart:async';
import '../helpers/notice_helpers.dart';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/auth/auth_screen.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  testWidgets(
    'staff login rejects wrong password then authenticates cashier role',
    (tester) async {
      await tester.runAsync(() async {
        final directory = await Directory.systemTemp.createTemp(
          'massar-login-',
        );
        final store = await CenterStore.open(directory: directory.path);
        addTearDown(
          () => TestWidgetsFlutterBinding.instance.runAsync(() async {
            await store.close();
            await directory.delete(recursive: true);
          }),
        );
        await store.setupAdmin('مدير', 'admin-login-pass');
        await store.saveStaff(
          name: 'الاستقبال',
          password: 'cashier-login-pass',
          role: StaffRole.cashier,
        );
        store.signOut();
        await tester.binding.setSurfaceSize(const Size(1440, 900));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final fonts = FontLoader('Tajawal')
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf'));
        await fonts.load();
        await (FontLoader(
          'MaterialIcons',
        )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
        final captureKey = GlobalKey();
        await tester.pumpWidget(
          MaterialApp(
            debugShowCheckedModeBanner: false,
            locale: const Locale('ar', 'EG'),
            supportedLocales: const [Locale('ar', 'EG')],
            localizationsDelegates: GlobalMaterialLocalizations.delegates,
            theme: MassarTheme.light,
            home: RepaintBoundary(
              key: captureKey,
              child: AuthScreen(store: store),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('auth-confirm')), findsNothing);
        expect(tester.takeException(), isNull);
        if (const bool.fromEnvironment('CAPTURE_UI')) {
          final boundary =
              captureKey.currentContext!.findRenderObject()
                  as RenderRepaintBoundary;
          final rendered = await boundary.toImage(pixelRatio: 1);
          final bytes = await rendered.toByteData(
            format: ui.ImageByteFormat.png,
          );
          await Directory('build/verification').create(recursive: true);
          await File(
            'build/verification/staff-login.png',
          ).writeAsBytes(bytes!.buffer.asUint8List());
          rendered.dispose();
        }
        await tester.enterText(find.byKey(const Key('auth-name')), 'الاستقبال');
        await tester.enterText(
          find.byKey(const Key('auth-password')),
          'wrong-password',
        );
        await tester.tap(find.byKey(const Key('auth-password-visibility')));
        await tester.pump();
        final passwordField = tester.widget<TextField>(
          find.descendant(
            of: find.byKey(const Key('auth-password')),
            matching: find.byType(TextField),
          ),
        );
        expect(passwordField.obscureText, isFalse);
        await tester.tap(find.byKey(const Key('auth-submit')));
        await tester.pump();
        expect(store.currentUser, isNull);
        await acknowledgeNotice(
          tester,
          message: 'اسم المستخدم أو كلمة المرور غير صحيحة.',
        );
        expect(store.currentUser, isNull);
        await tester.enterText(
          find.byKey(const Key('auth-password')),
          'cashier-login-pass',
        );
        final signedIn = Completer<void>();
        void observeLogin() {
          if (store.currentUser != null && !signedIn.isCompleted) {
            signedIn.complete();
          }
        }

        store.addListener(observeLogin);
        await tester.tap(find.byKey(const Key('auth-submit')));
        await signedIn.future.timeout(const Duration(seconds: 10));
        store.removeListener(observeLogin);
        await tester.pumpAndSettle();
        expect(store.currentUser?.name, 'الاستقبال');
        expect(store.canCollect, isTrue);
        expect(store.canManage, isFalse);
        expect(store.canAssess, isFalse);
        await tester.binding.setSurfaceSize(const Size(800, 600));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
      });
    },
  );
}
