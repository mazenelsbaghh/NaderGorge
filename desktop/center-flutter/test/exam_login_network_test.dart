import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/omada_exam_network_guard.dart';
import 'package:massar_center/features/auth/auth_screen.dart';
import 'package:massar_center/features/exams/exam_network_page.dart';
import 'package:massar_center/shared/theme.dart';

import 'helpers/exam_network_fixture.dart';
import 'helpers/ui_wait_helpers.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('failed hotspot change still releases its maintenance lease', () async {
    final fixture = await DisconnectedExamNetworkFixture.open();
    addTearDown(fixture.close);
    fixture.engine.failHotspotChange = true;
    await expectLater(
      fixture.controller.network({'action': 'hotspot-start'}),
      throwsA(isA<CenterException>()),
    );
    expect(fixture.engine.leaseHeld, isFalse);
    expect(fixture.engine.routes.last, '/api/network/maintenance/release');
  });

  test(
    'uncertain hotspot lease release remains visible after a successful native change',
    () async {
      final fixture = await DisconnectedExamNetworkFixture.open();
      addTearDown(fixture.close);
      fixture.engine.failRelease = true;
      final response = await fixture.controller.network({
        'action': 'hotspot-start',
      });
      expect(fixture.engine.commands.single['action'], 'hotspot-start');
      expect(fixture.engine.leaseHeld, isTrue);
      expect(response['warning'], isA<String>());
      expect(fixture.controller.error, isNotNull);
    },
  );

  for (final action in [
    'hotspot-start',
    'hotspot-stop',
    'tethering-start',
    'tethering-stop',
  ]) {
    test(
      '$action respects active-room rejection and holds maintenance lease for the native change',
      () async {
        final fixture = await DisconnectedExamNetworkFixture.open();
        addTearDown(fixture.close);
        fixture.engine.nativeRoomActive = true;
        await expectLater(
          fixture.controller.network({'action': action}),
          throwsA(isA<CenterException>()),
        );
        expect(fixture.engine.commands, isEmpty);
        fixture.engine.nativeRoomActive = false;
        await fixture.controller.network({'action': action});
        expect(fixture.engine.commands.single['action'], action);
        expect(fixture.engine.leaseHeld, isFalse);
        expect(fixture.engine.adminAccess.every((enabled) => !enabled), isTrue);
      },
    );
  }

  test(
    'disconnected signed-out client can inspect and configure locally without enabling exam administration',
    () async {
      final fixture = await DisconnectedExamNetworkFixture.open();
      addTearDown(fixture.close);
      expect(fixture.store.currentUser, isNull);
      expect(fixture.store.remoteConnected, isFalse);
      expect(fixture.controller.canSync, isFalse);
      await fixture.controller.network({'action': 'status'});
      await fixture.controller.network({'action': 'list-adapters'});
      expect(fixture.engine.running, isFalse);
      await fixture.controller.network({
        'action': 'configure',
        'adapterId': 'ethernet',
      });
      expect(fixture.engine.running, isTrue);
      expect(fixture.engine.adminAccess, isNotEmpty);
      expect(fixture.engine.adminAccess.every((enabled) => !enabled), isTrue);
      await expectLater(
        fixture.controller.openAdmin(),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        fixture.controller.network({'action': 'open-exam'}),
        throwsA(isA<CenterException>()),
      );
      expect(fixture.engine.commands.map((row) => row['action']), [
        'status',
        'list-adapters',
        'configure',
      ]);
      expect(fixture.store.currentUser, isNull);
      expect(fixture.store.students, isEmpty);
    },
  );

  test(
    'native lease rejection blocks vendor writes even when login has no cached exam state',
    () async {
      final fixture = await DisconnectedExamNetworkFixture.open();
      addTearDown(fixture.close);
      fixture.engine.nativeRoomActive = true;
      expect(fixture.controller.hasLiveExam, isFalse);
      var wrote = false;
      await expectLater(
        OmadaExamNetworkGuard(fixture.controller).run((beforeWrite) async {
          await beforeWrite();
          wrote = true;
        }),
        throwsA(isA<CenterException>()),
      );
      expect(wrote, isFalse);
      expect(fixture.engine.leaseHeld, isFalse);
      expect(fixture.engine.adminAccess, [false]);
    },
  );

  test(
    'local maintenance lease is held for the vendor write and released on failure',
    () async {
      final fixture = await DisconnectedExamNetworkFixture.open();
      addTearDown(fixture.close);
      final guard = OmadaExamNetworkGuard(fixture.controller);
      await expectLater(
        guard.run((beforeWrite) async {
          expect(fixture.engine.leaseHeld, isTrue);
          await beforeWrite();
          throw const CenterException('فقد رد إدارة Omada.');
        }),
        throwsA(isA<CenterException>()),
      );
      expect(fixture.engine.leaseHeld, isFalse);
      expect(guard.releaseWarning, isNull);
      expect(
        fixture.engine.routes
            .where((route) => route.contains('maintenance'))
            .toList(),
        [
          '/api/network/maintenance/acquire',
          '/api/network/maintenance/release',
        ],
      );
    },
  );

  for (final confirmed in [false, true]) {
    testWidgets(
      'host login hotspot requires actual Windows active state: $confirmed',
      (tester) async {
        await tester.runAsync(() async {
          final fixture = await DisconnectedExamNetworkFixture.open();
          addTearDown(() => tester.runAsync(fixture.close));
          fixture.engine.confirmTetheringStart = confirmed;
          await tester.binding.setSurfaceSize(const Size(900, 1000));
          addTearDown(() => tester.binding.setSurfaceSize(null));
          await tester.pumpWidget(
            MaterialApp(
              theme: MassarTheme.light,
              home: Scaffold(
                body: ExamNetworkPage(
                  controller: fixture.controller,
                  isHost: true,
                ),
              ),
            ),
          );
          await waitForUiCondition(
            tester,
            () => find.byType(LinearProgressIndicator).evaluate().isEmpty,
            reason: 'Host network status finished.',
          );
          await tester.tap(find.text('تشغيل شبكة الهوست'));
          await waitForUiCondition(
            tester,
            () =>
                !fixture.engine.leaseHeld &&
                find.byType(LinearProgressIndicator).evaluate().isEmpty,
            reason:
                'The native hotspot change and maintenance release finished.',
          );
          expect(
            find.text('اسم الشبكة: Host trial'),
            confirmed ? findsOneWidget : findsNothing,
          );
          if (!confirmed) {
            expect(
              find.text('لم يؤكد ويندوز تشغيل شبكة الهوست.'),
              findsOneWidget,
            );
          }
          expect(fixture.store.currentUser, isNull);
          expect(fixture.engine.adminAccess, isNot(contains(true)));
          expect(
            fixture.engine.routes,
            contains('/api/network/maintenance/release'),
          );
          await tester.scrollUntilVisible(
            find.text('تجهيز وصلة الامتحان'),
            250,
            scrollable: find.byType(Scrollable).first,
          );
          final configure = tester.widget<FilledButton>(
            find.widgetWithText(FilledButton, 'تجهيز وصلة الامتحان'),
          );
          expect(configure.onPressed, isNull);
          expect(tester.takeException(), isNull);
          await tester.pumpWidget(const SizedBox.shrink());
        });
      },
    );
  }

  testWidgets(
    'login exposes working local network setup without employee credentials or host connection',
    (tester) async {
      await tester.runAsync(() async {
        final fixture = await DisconnectedExamNetworkFixture.open();
        addTearDown(
          () => TestWidgetsFlutterBinding.instance.runAsync(fixture.close),
        );
        const pathProvider = MethodChannel('plugins.flutter.io/path_provider');
        tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
          pathProvider,
          (call) async => fixture.directory.path,
        );
        addTearDown(
          () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
            pathProvider,
            null,
          ),
        );
        await tester.binding.setSurfaceSize(const Size(900, 780));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        await tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.light,
            locale: const Locale('ar', 'EG'),
            supportedLocales: const [Locale('ar', 'EG')],
            localizationsDelegates: GlobalMaterialLocalizations.delegates,
            home: Builder(
              builder: (context) => AuthScreen(
                store: fixture.store,
                onOpenNetwork: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => Scaffold(
                      body: ExamNetworkPage(
                        controller: fixture.controller,
                        isHost: false,
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const Key('auth-network-setup')));
        await tester.pump(const Duration(milliseconds: 400));
        await Future<void>.delayed(const Duration(milliseconds: 30));
        await tester.pump();
        await waitForUiCondition(
          tester,
          () =>
              fixture.engine.commands.length >= 2 &&
              find.byType(LinearProgressIndicator).evaluate().isEmpty,
          reason:
              'Local network reads and profile loading finish before inspecting the login route.',
        );
        expect(find.byType(ExamNetworkPage), findsOneWidget);
        expect(find.text('وصلة شبكة الامتحان'), findsOneWidget);
        expect(
          fixture.engine.commands.map((row) => row['action']),
          containsAll(['status', 'list-adapters']),
        );
        expect(fixture.store.currentUser, isNull);
        expect(fixture.store.remoteConnected, isFalse);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
      });
    },
  );
}
