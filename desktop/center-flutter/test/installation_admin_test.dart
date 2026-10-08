import 'dart:convert';
import 'dart:async';
import 'dart:io';
import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter/material.dart';
import 'package:massar_center/application/admin_configuration.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/main.dart';
import 'helpers/synthetic_installer_assets.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late InstallationAdmin admin;
  const password = 'fixture-owner-pass';

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-install-admin-');
    final salt = List<int>.generate(24, (i) => i + 1);
    final key = await Pbkdf2(
      macAlgorithm: Hmac.sha256(),
      iterations: 120000,
      bits: 256,
    ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
    admin = InstallationAdmin(
      id: 'installation-owner',
      name: 'fixture-owner',
      credential: {
        'salt': base64Encode(salt),
        'hash': base64Encode(await key.extractBytes()),
        'algorithm': 'pbkdf2-sha256-120000',
      },
    );
    installSyntheticInstallerAssets(admin);
  });

  tearDown(() async => directory.delete(recursive: true));

  test(
    'each fresh installation provisions the same owner and always requires sign in',
    () async {
      for (final child in ['device-one', 'device-two']) {
        final path = '${directory.path}/$child';
        var store = await openInstalledCenter(directory: path, admin: admin);
        try {
          expect(store.hasStaff, isTrue);
          expect(store.currentUser, isNull);
          expect(store.canManage, isFalse);
          expect(store.staff.single.id, admin.id);
          expect(store.staff.single.role, StaffRole.admin);
          expect(store.students, isEmpty);
          await expectLater(
            store.signIn(admin.name, 'incorrect-pass'),
            throwsA(isA<CenterException>()),
          );
          await store.signIn(admin.name, password);
          expect(store.canManage, isTrue);
          final backup = await store.createBackup();
          expect(await File(backup).readAsString(), isNot(contains(password)));
          final audits = store.audit.length;
          await store.close();
          store = await openInstalledCenter(directory: path, admin: admin);
          expect(store.currentUser, isNull);
          expect(store.staff, hasLength(1));
          expect(store.audit, hasLength(audits));
          await store.signIn(admin.name.toUpperCase(), password);
          expect(store.canManage, isTrue);
        } finally {
          await store.close();
        }
      }
    },
  );

  test(
    'existing data and staff survive provisioning and old backup restore retains fixed owner',
    () async {
      final store = await CenterStore.open(directory: directory.path);
      try {
        await store.setupAdmin('legacy-manager', 'legacy-admin-pass');
        await store.saveCatalog(
          const CatalogEntry(name: 'فيزياء', kind: CatalogKind.subject),
        );
        final legacyId = store.staff.single.id;
        final oldBackup = await store.createBackup();
        store.signOut();
        await store.ensureInstallationAdmin(admin);
        expect(store.currentUser, isNull);
        expect(store.staff, hasLength(2));
        expect(store.staff.first.id, legacyId);
        expect(store.catalogs.single.name, 'فيزياء');
        await store.signIn(admin.name, password);
        await store.saveCatalog(
          const CatalogEntry(name: 'سنتر', kind: CatalogKind.center),
        );
        await store.restoreBackup(oldBackup);
        expect(store.currentUser, isNull);
        expect(store.staff, hasLength(2));
        expect(store.catalogs.single.name, 'فيزياء');
        await store.signIn(admin.name, password);
        expect(store.canManage, isTrue);
        store.signOut();
        await store.signIn('legacy-manager', 'legacy-admin-pass');
        expect(store.canManage, isTrue);
      } finally {
        await store.close();
      }
    },
  );

  test(
    'an existing matching login is reinstated without duplicate identity or data loss',
    () async {
      final store = await CenterStore.open(directory: directory.path);
      try {
        await store.setupAdmin('legacy-manager', 'legacy-admin-pass');
        await store.saveStaff(
          name: admin.name.toUpperCase(),
          password: 'old-cashier-pass',
          role: StaffRole.cashier,
        );
        final originalId = store.staff.last.id;
        store.signOut();
        await store.ensureInstallationAdmin(admin);
        expect(store.staff, hasLength(2));
        expect(store.staff.last.id, originalId);
        expect(store.staff.last.role, StaffRole.admin);
        await store.signIn(admin.name, password);
        expect(store.currentUser!.id, originalId);
        expect(store.canManage, isTrue);
        expect(store.audit.last.action, 'installation_admin');
      } finally {
        await store.close();
      }
    },
  );

  test(
    'installer asset is parsed into the configured owner and malformed credentials are rejected',
    () async {
      final bundled = await loadInstallationAdmin();
      expect(bundled.name, admin.name);
      expect(bundled.id, admin.id);
      expect(bundled.credential, admin.credential);
      expect(bundled.credential['algorithm'], 'pbkdf2-sha256-120000');
      expect(
        () => InstallationAdmin(
          id: 'owner',
          name: 'owner',
          credential: {
            'salt': 'bad',
            'hash': 'bad',
            'algorithm': 'pbkdf2-sha256-120000',
          },
        ),
        throwsA(isA<CenterException>()),
      );
    },
  );

  testWidgets(
    'installed owner starts with blank username and signs in to admin correction tools',
    (tester) async {
      await tester.runAsync(() async {
        final store = await openInstalledCenter(
          directory: directory.path,
          admin: admin,
        );
        try {
          await tester.binding.setSurfaceSize(const Size(1440, 900));
          await tester.pumpWidget(CenterApp(store: store));
          await tester.pumpAndSettle();
          expect(find.byKey(const Key('auth-confirm')), findsNothing);
          expect(
            tester
                .widget<TextFormField>(find.byKey(const Key('auth-name')))
                .controller!
                .text,
            isEmpty,
          );
          expect(store.currentUser, isNull);
          await tester.enterText(
            find.byKey(const Key('auth-name')),
            admin.name,
          );
          await tester.enterText(
            find.byKey(const Key('auth-password')),
            password,
          );
          final signedIn = Completer<void>();
          void listener() {
            if (store.canManage && !signedIn.isCompleted) signedIn.complete();
          }

          store.addListener(listener);
          await tester.tap(find.byKey(const Key('auth-submit')));
          await signedIn.future.timeout(const Duration(seconds: 15));
          store.removeListener(listener);
          await tester.pumpAndSettle();
          final corrections = find.widgetWithText(
            ListTile,
            'التصحيح والاسترداد',
          );
          await tester.scrollUntilVisible(
            corrections,
            120,
            scrollable: find.descendant(
              of: find.byKey(const Key('management-navigation')),
              matching: find.byType(Scrollable),
            ),
          );
          await tester.pumpAndSettle();
          await tester.tap(corrections);
          await tester.pumpAndSettle();
          expect(
            find.byKey(const Key('correction-student-code')),
            findsOneWidget,
          );
          expect(tester.takeException(), isNull);
          expect(store.canManage, isTrue);
          expect(store.students, isEmpty);
        } finally {
          await tester.pumpWidget(const SizedBox.shrink());
          await store.close();
          await tester.binding.setSurfaceSize(null);
        }
      });
    },
  );
}
