import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/cloud/app_update_controller.dart';
import 'package:massar_center/cloud/cloud_support_controller.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/cloud_settings_page.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/cloud_http_fake.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory temporary;
  late CenterStore store;
  late CloudSupportController cloud;
  late AppUpdateController updates;
  late ScriptedCloudHttp network;

  setUp(() async {
    temporary = await Directory.systemTemp.createTemp('massar-cloud-page-');
    store = await CenterStore.open(directory: temporary.path);
    await store.setupAdmin('test-manager', 'synthetic-password');
    await store.saveStaff(
      name: 'test-cashier',
      password: 'synthetic-cashier',
      role: StaffRole.cashier,
    );
    network = ScriptedCloudHttp((request) {
      final archive = <int>[0x50, 0x4b, 0x05, 0x06, ...List.filled(18, 0)];
      if (request.uri.path.startsWith('/v1/updates/')) {
        final role = request.uri.pathSegments.last;
        return CloudHttpReply.json(200, {
          'releaseId': 'synthetic-$role-release',
          'version': '1.1.0+2',
          'build': '2222222222222222',
          'platform': updates.platform,
          'role': role,
          'size': archive.length,
          'sha256': sha256.convert(archive).toString(),
          'downloadPath': '/v1/releases/synthetic-$role.zip',
        });
      }
      if (request.uri.path.startsWith('/v1/releases/')) {
        return CloudHttpReply(200, archive);
      }
      return CloudHttpReply.json(201, {
        'uploadId': (jsonDecode(request.body) as Map)['uploadId'],
        'receiptId': '12345678-1234-4234-8234-123456789abc',
        'sha256': List.filled(64, 'a').join(),
        'receivedAt': '2026-10-03T12:34:56Z',
      });
    });
    cloud = CloudSupportController(
      directory: Directory('${temporary.path}/support'),
      clientOnly: false,
      snapshot: store.captureSupportSnapshot,
      diagnostics: () async => '',
      httpClientFactory: network.createClient,
    );
    store.supportUploadQueue = cloud.queueUpload;
    store.supportStatusReader = () => cloud.publicStatus;
    await cloud.configure(
      Uri.parse('https://support.example.invalid'),
      'test-center',
      'synthetic-support-token-1234',
    );
    updates = AppUpdateController(
      directory: Directory('${temporary.path}/updates'),
      configuration: () async => cloud.configuration,
      httpClientFactory: network.createClient,
    );
  });

  tearDown(() async {
    await TestWidgetsFlutterBinding.instance.runAsync(() async {
      await updates.close();
      updates.dispose();
      await cloud.close();
      cloud.dispose();
      await store.close();
      await temporary.delete(recursive: true);
    });
  });

  Future<void> open(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(1100, 1200));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: Scaffold(
            body: CloudSettingsPage(
              store: store,
              cloud: cloud,
              updates: updates,
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets(
    'download both packages offers separate host and client save actions without uploading student data',
    (tester) async {
      final auditCount = store.audit.length;
      await open(tester);
      final both = find.widgetWithText(FilledButton, 'تنزيل الهوست والساكند');
      await tester.scrollUntilVisible(
        both,
        400,
        scrollable: find.byType(Scrollable).first,
      );
      final button = tester.widget<FilledButton>(both);
      await tester.runAsync(() async {
        await Function.apply(button.onPressed!, []);
      });
      await tester.pumpAndSettle();
      expect(find.text('حفظ ملف الهوست'), findsOneWidget);
      expect(find.text('حفظ ملف الساكند لإرساله'), findsOneWidget);
      expect(
        network.requests
            .where((r) => r.uri.path.startsWith('/v1/updates/'))
            .map((r) => r.uri.pathSegments.last),
        unorderedEquals(['host', 'client']),
      );
      expect(network.requests.every((r) => r.method == 'GET'), isTrue);
      expect(store.audit, hasLength(auditCount));
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets(
    'cashier can upload committed support data but cannot retarget the support destination',
    (tester) async {
      await tester.runAsync(
        () => store.signIn('test-cashier', 'synthetic-cashier'),
      );
      final auditCount = store.audit.length;
      await open(tester);
      expect(find.text('إعداد الربط'), findsNothing);
      final upload = find.widgetWithText(FilledButton, 'مزامنة للدعم');
      await tester.ensureVisible(upload);
      await tester.runAsync(() async {
        await tester.tap(upload);
        for (var i = 0; i < 100 && cloud.lastSuccess == null; i++) {
          await Future<void>.delayed(const Duration(milliseconds: 20));
        }
      });
      await tester.pumpAndSettle();
      expect(network.requests, hasLength(1));
      final envelope = jsonDecode(network.requests.single.body) as Map;
      expect(envelope['kind'], 'database');
      expect((envelope['data'] as Map)['format'], 'massar-center-backup');
      expect(cloud.lastSuccess, isNotNull);
      expect(store.audit, hasLength(auditCount));
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );

  testWidgets(
    'manager configuration cancels without altering the destination or starting an upload',
    (tester) async {
      await open(tester);
      await tester.tap(find.text('إعداد الربط'));
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('ربط هذا الجهاز بمسار'), findsOneWidget);
      expect(find.byType(TextFormField), findsNWidgets(3));
      expect(network.requests, isEmpty);
      // No draft edits: back closes only the configuration route.
      await tester.tap(find.text('رجوع'));
      await tester.pumpAndSettle();
      expect(find.text('ربط هذا الجهاز بمسار'), findsNothing);
      expect(cloud.origin.toString(), 'https://support.example.invalid');
      expect(network.requests, isEmpty);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );
}
