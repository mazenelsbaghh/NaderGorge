import 'dart:io';
import 'dart:ui' as ui;

import 'package:cryptography/cryptography.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/exams/omada_models.dart';
import 'package:massar_center/exams/omada_profile_store.dart';
import 'package:massar_center/features/exams/omada_setup_panel.dart';
import 'package:massar_center/shared/theme.dart';

import 'helpers/exam_network_fixture.dart';
import 'helpers/ui_wait_helpers.dart';

const _studentSsid = OmadaSsid(
  id: 'students',
  name: 'شبكة الطلاب الأصلية',
  band: 3,
  security: 3,
  broadcast: true,
  guestNetwork: false,
  vlanEnabled: false,
);
const _apMac = 'AA-BB-CC-DD-EE-01';
const _createLabel = 'إضافة شبكة الموظفين وحفظ شبكة الطلاب';

void main() {
  setUpAll(() async {
    await (FontLoader(
      'Tajawal',
    )..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))).load();
    await (FontLoader(
      'MaterialIcons',
    )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
  });

  testWidgets(
    'signed-out staff Wi-Fi flow preserves read-only student name and writes only under durable lease',
    (tester) async {
      await tester.runAsync(() async {
        final fixture = await _PanelFixture.open();
        addTearDown(
          () => TestWidgetsFlutterBinding.instance.runAsync(fixture.close),
        );
        await fixture.show(tester, size: const Size(440, 860));
        await _prepare(tester);
        expect(
          find.textContaining(
            'ستبقى الشبكات الحالية كما هي: شبكة الطلاب الأصلية',
          ),
          findsOneWidget,
        );
        expect(
          find.byWidgetPredicate(
            (widget) =>
                widget is TextField &&
                widget.controller?.text == _studentSsid.name,
          ),
          findsNothing,
        );
        expect(fixture.vendor.createdNames, isEmpty);
        expect(fixture.network.store.currentUser, isNull);
        expect(fixture.network.store.remoteConnected, isFalse);
        fixture.vendor.beforeCreate = () async {
          expect(fixture.network.engine.leaseHeld, isTrue);
          final saved = await fixture.profiles.load();
          expect(saved!['pending'], isA<Map>());
          expect((saved['pending'] as Map)['staffName'], 'MASSAR-STAFF');
          expect(
            await fixture.profiles.file.readAsString(),
            isNot(contains('fixture-api-secret')),
          );
        };
        if (const bool.fromEnvironment('CAPTURE_UI')) await _capture(tester);
        await _tap(tester, _createLabel);
        expect(fixture.vendor.createdNames, ['MASSAR-STAFF']);
        expect(fixture.vendor.ssids.first.name, _studentSsid.name);
        expect(fixture.vendor.lastAp, _apMac);
        expect(fixture.vendor.lastStudent, 'students');
        expect(fixture.network.engine.leaseHeld, isFalse);
        expect(
          fixture.network.engine.adminAccess.every((enabled) => !enabled),
          isTrue,
        );
        final saved = await fixture.profiles.load();
        expect(saved!['pending'], isNull);
        expect((saved['confirmed'] as Map)['staffName'], 'MASSAR-STAFF');
        expect(find.textContaining('حُفظت شبكة MASSAR-STAFF'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
      });
    },
  );

  testWidgets(
    'lost vendor reply retains encrypted pending across reopening and only offers readback',
    (tester) async {
      await tester.runAsync(() async {
        final fixture = await _PanelFixture.open();
        addTearDown(
          () => TestWidgetsFlutterBinding.instance.runAsync(fixture.close),
        );
        fixture.vendor.loseCreateReply = true;
        await fixture.show(tester);
        await _prepare(tester);
        await _tap(tester, _createLabel);
        expect(fixture.vendor.createdNames, ['MASSAR-STAFF']);
        expect(find.textContaining('طلب سابق يحتاج مراجعة'), findsOneWidget);
        final createButton = tester.widget<FilledButton>(
          find.widgetWithText(FilledButton, _createLabel),
        );
        expect(createButton.onPressed, isNull);
        await tester.pumpWidget(const SizedBox.shrink());
        await fixture.show(tester);
        expect(find.textContaining('طلب سابق يحتاج مراجعة'), findsOneWidget);
        expect(find.widgetWithText(FilledButton, _createLabel), findsNothing);
        await _tap(tester, 'فحص نتيجة الطلب السابق');
        expect(find.textContaining('موجودة الآن'), findsOneWidget);
        expect(fixture.vendor.createdNames, ['MASSAR-STAFF']);
        expect((await fixture.profiles.load())!['pending'], isA<Map>());
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
      });
    },
  );

  testWidgets(
    'failed protected pending save prevents network write and releases native lease',
    (tester) async {
      await tester.runAsync(() async {
        final fixture = await _PanelFixture.open();
        addTearDown(
          () => TestWidgetsFlutterBinding.instance.runAsync(fixture.close),
        );
        await fixture.show(tester);
        await _prepare(tester);
        fixture.protector.failProtect = true;
        await _tap(tester, _createLabel);
        expect(fixture.vendor.createdNames, isEmpty);
        expect(fixture.network.engine.leaseHeld, isFalse);
        expect((await fixture.profiles.load())!['pending'], isNull);
        expect(find.textContaining('طلب سابق يحتاج مراجعة'), findsNothing);
        fixture.protector.failProtect = false;
        await _tap(tester, _createLabel);
        expect(fixture.vendor.createdNames, ['MASSAR-STAFF']);
        await tester.pumpWidget(const SizedBox.shrink());
      });
    },
  );

  testWidgets(
    'actual native exam rejection prevents provisioning even when local UI snapshot is empty',
    (tester) async {
      await tester.runAsync(() async {
        final fixture = await _PanelFixture.open();
        addTearDown(
          () => TestWidgetsFlutterBinding.instance.runAsync(fixture.close),
        );
        await fixture.show(tester);
        await _prepare(tester);
        fixture.network.engine.nativeRoomActive = true;
        await _tap(tester, _createLabel);
        expect(fixture.vendor.createdNames, isEmpty);
        expect((await fixture.profiles.load())!['pending'], isNull);
        expect(
          find.text('هناك امتحان جارٍ أو تجهيز شبكة قائم.'),
          findsOneWidget,
        );
        await tester.pumpWidget(const SizedBox.shrink());
      });
    },
  );

  testWidgets(
    'confirmation-save failure retains the persisted pending barrier after a successful vendor write',
    (tester) async {
      await tester.runAsync(() async {
        final fixture = await _PanelFixture.open();
        addTearDown(
          () => TestWidgetsFlutterBinding.instance.runAsync(fixture.close),
        );
        await fixture.show(tester);
        await _prepare(tester);
        fixture.vendor.beforeCreate = () async =>
            fixture.protector.failProtect = true;
        await _tap(tester, _createLabel);
        expect(fixture.vendor.createdNames, ['MASSAR-STAFF']);
        expect(find.textContaining('طلب سابق يحتاج مراجعة'), findsOneWidget);
        expect((await fixture.profiles.load())!['pending'], isA<Map>());
        fixture.protector.failProtect = false;
        await _tap(tester, 'فحص نتيجة الطلب السابق');
        expect(find.textContaining('موجودة الآن'), findsOneWidget);
        expect(fixture.vendor.createdNames, ['MASSAR-STAFF']);
        await tester.pumpWidget(const SizedBox.shrink());
      });
    },
  );
}

Finder _field(String label) => find.byWidgetPredicate(
  (widget) => widget is TextField && widget.decoration?.labelText == label,
);
Future<void> _idle(WidgetTester tester) => waitForUiCondition(
  tester,
  () => find.byType(LinearProgressIndicator).evaluate().isEmpty,
  reason: 'The local profile/controller operation finished.',
);
Future<void> _tap(WidgetTester tester, String text) async {
  final target = find.text(text).last;
  await tester.ensureVisible(target);
  await tester.pumpAndSettle();
  await tester.tap(target);
  await tester.pump();
  await _idle(tester);
}

Future<void> _choose(WidgetTester tester, String label, String option) async {
  final field = find.widgetWithText(DropdownButtonFormField<String>, label);
  await tester.ensureVisible(field);
  await tester.pumpAndSettle();
  await tester.tap(field);
  await tester.pumpAndSettle();
  await tester.tap(find.text(option).last);
  await tester.pump();
  await _idle(tester);
}

Future<void> _prepare(WidgetTester tester) async {
  await _tap(tester, 'قراءة الشبكات');
  await _choose(tester, 'الموقع الموجود فيه Omada', 'قاعة التجربة');
  await _choose(
    tester,
    'جهاز Omada المتصل بالكابل',
    'أكسس القاعة · EAP-Fixture · $_apMac',
  );
  await _choose(tester, 'مجموعة الشبكات الحالية في Omada', 'مجموعة القاعة');
  await _choose(
    tester,
    'شبكة الطلاب الحالية، سيبقى اسمها كما هو',
    _studentSsid.name,
  );
  final password = _field('كلمة سر شبكة الموظفين');
  await tester.ensureVisible(password);
  await tester.enterText(password, 'Staff-Test-Password');
  tester.testTextInput.hide();
  await _tap(tester, 'مراجعة التجهيز قبل الإضافة');
  expect(find.text('ستُضاف: MASSAR-STAFF'), findsOneWidget);
}

Future<void> _capture(WidgetTester tester) async {
  await tester.ensureVisible(find.text(_createLabel));
  await tester.pumpAndSettle();
  final boundary = tester.renderObject<RenderRepaintBoundary>(
    find.byKey(const Key('omada-panel-capture')),
  );
  final image = await boundary.toImage(pixelRatio: 1);
  try {
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    final file = File(
      '../../artifacts/private/omada-integration-20261014/omada-login-rtl-preview.png',
    );
    await file.parent.create(recursive: true);
    await file.writeAsBytes(bytes!.buffer.asUint8List());
  } finally {
    image.dispose();
  }
}

class _PanelFixture {
  _PanelFixture(this.network, this.protector, this.profiles);
  final DisconnectedExamNetworkFixture network;
  final _TestProfileProtector protector;
  final OmadaProfileStore profiles;
  final vendor = _VendorState();
  static Future<_PanelFixture> open() async {
    final network = await DisconnectedExamNetworkFixture.open();
    final protector = _TestProfileProtector(
      await AesGcm.with256bits().newSecretKey(),
    );
    final profiles = OmadaProfileStore(
      file: File('${network.directory.path}/controller.enc'),
      protector: protector,
    );
    await profiles.save({
      'address': 'https://127.0.0.1:8043',
      'omadaId': 'fixture-controller',
      'clientId': 'fixture-client',
      'clientSecret': 'fixture-api-secret',
    });
    return _PanelFixture(network, protector, profiles);
  }

  Future<void> show(
    WidgetTester tester, {
    Size size = const Size(860, 900),
  }) async {
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        locale: const Locale('ar', 'EG'),
        supportedLocales: const [Locale('ar', 'EG')],
        localizationsDelegates: GlobalMaterialLocalizations.delegates,
        home: Scaffold(
          body: RepaintBoundary(
            key: const Key('omada-panel-capture'),
            child: ColoredBox(
              color: MassarTheme.light.scaffoldBackgroundColor,
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(16),
                child: OmadaSetupPanel(
                  controller: network.controller,
                  networkReady: true,
                  profileStore: profiles,
                  serviceFactory: (config) => _VendorConnection(vendor),
                ),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.pump();
    await _idle(tester);
  }

  Future<void> close() => network.close();
}

/// DPAPI is the platform boundary. Real authenticated encryption and a real
/// profile file exercise ordering/persistence without invoking Windows on macOS.
class _TestProfileProtector implements OmadaProfileProtector {
  _TestProfileProtector(this.key);
  final SecretKey key;
  final algorithm = AesGcm.with256bits();
  bool failProtect = false;
  @override
  Future<Uint8List> protect(Uint8List plaintext) async {
    if (failProtect) throw const CenterException('تعذر حفظ الربط المحمي.');
    final box = await algorithm.encrypt(plaintext, secretKey: key);
    return Uint8List.fromList([
      ...box.nonce,
      ...box.mac.bytes,
      ...box.cipherText,
    ]);
  }

  @override
  Future<Uint8List> unprotect(Uint8List ciphertext) async => Uint8List.fromList(
    await algorithm.decrypt(
      SecretBox(
        ciphertext.sublist(28),
        nonce: ciphertext.sublist(0, 12),
        mac: Mac(ciphertext.sublist(12, 28)),
      ),
      secretKey: key,
    ),
  );
}

class _VendorState {
  final ssids = <OmadaSsid>[_studentSsid];
  final createdNames = <String>[];
  bool loseCreateReply = false;
  String? lastAp, lastStudent;
  Future<void> Function()? beforeCreate;
}

class _VendorConnection implements OmadaControllerService {
  _VendorConnection(this.vendor);
  final _VendorState vendor;
  @override
  Future<List<OmadaSite>> listSites() async => [
    const OmadaSite(id: 'site', name: 'قاعة التجربة'),
  ];
  @override
  Future<List<OmadaAccessPoint>> listAccessPoints(String siteId) async => [
    const OmadaAccessPoint(
      mac: _apMac,
      name: 'أكسس القاعة',
      model: 'EAP-Fixture',
      status: 1,
      detailStatus: 14,
    ),
  ];
  @override
  Future<List<OmadaWlanGroup>> listWlanGroups(String siteId) async => [
    const OmadaWlanGroup(id: 'wlan', name: 'مجموعة القاعة'),
  ];
  @override
  Future<List<OmadaSsid>> listSsids(String siteId, String wlanId) async =>
      List.of(vendor.ssids);
  @override
  Future<OmadaStaffWifiPreview> previewStaffWifi({
    required String siteId,
    required String wlanId,
    required String accessPointMac,
    required String studentSsidId,
    required String staffName,
    required String staffPassword,
  }) async {
    vendor.lastAp = accessPointMac;
    vendor.lastStudent = studentSsidId;
    return OmadaStaffWifiPreview(
      siteId: siteId,
      wlanId: wlanId,
      accessPointMac: accessPointMac,
      staffName: staffName,
      preservedSsids: List.of(vendor.ssids),
    );
  }

  @override
  Future<OmadaStaffWifiResult> applyStaffWifi(
    OmadaStaffWifiPreview preview, {
    Future<void> Function()? beforeWrite,
  }) async {
    await beforeWrite!();
    await vendor.beforeCreate?.call();
    vendor.createdNames.add(preview.staffName);
    final ssid = OmadaSsid(
      id: 'created',
      name: preview.staffName,
      band: 3,
      security: 3,
      broadcast: true,
      guestNetwork: false,
      vlanEnabled: false,
    );
    vendor.ssids.add(ssid);
    if (vendor.loseCreateReply) {
      throw const OmadaException(
        'لم يتأكد رد إدارة Omada.',
        code: 'create_outcome_unknown',
      );
    }
    return OmadaStaffWifiResult(
      ssid: ssid,
      recoveredAfterUncertainCreate: false,
    );
  }

  @override
  Future<OmadaCertificateInfo> probeCertificate(Uri baseUri) async =>
      throw StateError(
        'The profile fixture already trusts its local controller.',
      );
  @override
  void close() {}
}
