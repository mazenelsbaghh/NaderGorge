import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:cryptography/cryptography.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import '../helpers/ui_wait_helpers.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/card_settings_page.dart';
import 'package:massar_center/features/management/cards_page.dart';
import 'package:massar_center/features/management/management_workspace.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  late Directory dir;
  late CenterStore store;
  const ownerName = 'fixture-card-owner';
  const password = 'fixture-card-password';
  final captureKey = GlobalKey();

  setUp(() async {
    await initializeDateFormatting('ar_EG');
    dir = await Directory.systemTemp.createTemp('massar-cards-widget-');
    store = await CenterStore.open(directory: dir.path);
    final salt = List<int>.generate(24, (i) => i + 1);
    final key = await Pbkdf2(
      macAlgorithm: Hmac.sha256(),
      iterations: 120000,
      bits: 256,
    ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
    await store.ensureInstallationAdmin(
      InstallationAdmin(
        id: 'fixture-card-owner',
        name: ownerName,
        credential: {
          'salt': base64Encode(salt),
          'hash': base64Encode(await key.extractBytes()),
          'algorithm': 'pbkdf2-sha256-120000',
        },
      ),
    );
    await store.signIn(ownerName, password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(
        CatalogEntry(kind: kind, name: 'اختبار ${kind.name}'),
      );
    }
    await store.saveGroup(
      StudyGroup(
        name: 'الأحد',
        subjectId: store.catalogs
            .firstWhere((c) => c.kind == CatalogKind.subject)
            .id,
        centerId: store.catalogs
            .firstWhere((c) => c.kind == CatalogKind.center)
            .id,
        gradeId: store.catalogs
            .firstWhere((c) => c.kind == CatalogKind.grade)
            .id,
        sessionPrice: 6000,
        packagePrice: 24000,
      ),
    );
    for (final (code, name, discount, notes) in [
      ('101', 'أحمد محمد', 25, ''),
      ('202', 'مينا سامح', 0, ''),
      ('303', 'يوسف قديم', 0, 'دفع الكارت واستلمه قديمًا'),
    ]) {
      await store.saveStudent(
        Student(
          code: code,
          name: name,
          discountPercent: discount,
          notes: notes,
          groupIds: [store.groups.single.id],
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
    }
    await store.saveStaff(
      name: 'استقبال',
      password: password,
      role: StaffRole.cashier,
    );
    await store.saveStaff(
      name: 'مدير آخر',
      password: password,
      role: StaffRole.admin,
    );
  });

  tearDown(() async {
    await TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await dir.delete(recursive: true);
    });
  });

  Future<void> open(
    WidgetTester tester, {
    bool dark = true,
    Size size = const Size(1280, 900),
  }) async {
    await tester.binding.setSurfaceSize(size);
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await (FontLoader('Tajawal')
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
          ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
        .load();
    await (FontLoader(
      'MaterialIcons',
    )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    await tester.pumpWidget(
      MaterialApp(
        debugShowCheckedModeBanner: false,
        theme: dark ? MassarTheme.dark : MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: RepaintBoundary(
            key: captureKey,
            child: Scaffold(
              body: ManagementWorkspace(store: store, onOpenAttendance: () {}),
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> page(WidgetTester tester, String label) async {
    await navigateManagementPage(tester, label);
  }

  Future<void> waitOperation(WidgetTester tester, {bool notice = true}) async {
    for (var attempt = 0; attempt < 80; attempt++) {
      await Future<void>.delayed(const Duration(milliseconds: 20));
      await tester.pumpAndSettle();
      final search = find.byKey(const Key('cards-student-search'));
      if (search.evaluate().isNotEmpty &&
          tester.widget<TextField>(search).enabled == true) {
        return;
      }
      final save = find.byKey(const Key('save-card-settings'));
      if (save.evaluate().isNotEmpty &&
          tester.widget<FilledButton>(save).onPressed != null) {
        return;
      }
    }
    fail('Card operation did not complete.');
  }

  Future<void> scan(WidgetTester tester, String code) async {
    await tester.enterText(find.byKey(const Key('cards-student-search')), code);
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
  }

  Future<void> receiptFilter(WidgetTester tester, String label) async {
    await tester.tap(find.byKey(const Key('cards-receipt-filter')));
    await tester.pumpAndSettle();
    await tester.tap(find.text(label).last);
    await tester.pumpAndSettle();
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    final boundary =
        captureKey.currentContext!.findRenderObject() as RenderRepaintBoundary;
    final image = await boundary.toImage(pixelRatio: 1);
    final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
    final file = File('build/verification/$name.png');
    await file.parent.create(recursive: true);
    await file.writeAsBytes(bytes!.buffer.asUint8List());
    image.dispose();
  }

  for (final dark in [false, true]) {
    testWidgets(
      'owner config and cashier card collection/confirmed handover remain separate from lessons (${dark ? 'dark' : 'light'})',
      (tester) async {
        await tester.runAsync(() async {
          await open(tester, dark: dark);
          await page(tester, 'إعدادات مازن');
          expect(store.cardSettings.price, isNull);
          expect(
            tester
                .widget<SwitchListTile>(
                  find.byKey(const Key('card-setting-require-payment')),
                )
                .value,
            isTrue,
          );
          await tester.enterText(
            find.byKey(const Key('card-setting-price')),
            '٥٠٫٢٥',
          );
          await tester.tap(find.byKey(const Key('save-card-settings')));
          await waitOperation(tester);
          expect(store.cardSettings.price, 5025);
          await capture(
            tester,
            'card-settings-${dark ? 'dark' : 'light'}-1280',
          );
          store.signOut();
          await store.signIn('استقبال', password);
          await tester.pumpAndSettle();
          expect(find.widgetWithText(ListTile, 'إعدادات مازن'), findsNothing);
          expect(store.canConfigureCards, isFalse);
          await page(tester, 'كروت الطلبة');
          expect(find.text('لم يستلموا: 3'), findsOneWidget);
          await scan(tester, '101');
          expect(
            tester
                .widget<FilledButton>(
                  find.byKey(const Key('receive-student-card')),
                )
                .onPressed,
            isNull,
          );
          await tester.tap(find.byKey(const Key('pay-student-card')));
          await tester.pumpAndSettle();
          expect(store.cardPayments, isEmpty);
          final confirm = tester
              .widget<FilledButton>(
                find.byKey(const Key('confirm-paid-amount')),
              )
              .onPressed!;
          confirm();
          confirm();
          await waitOperation(tester);
          expect(store.cardPayments, hasLength(1));
          expect(store.cardPayments.single.netAmount, 3769);
          expect(store.cardPayments.single.discountPercent, 25);
          expect(store.cardReceipts, isEmpty);
          expect(find.text('دفعوا ولم يستلموا: 1'), findsOneWidget);
          expect(
            tester
                .widget<OutlinedButton>(
                  find.byKey(const Key('pay-student-card')),
                )
                .onPressed,
            isNull,
          );
          await capture(
            tester,
            'cards-paid-pending-${dark ? 'dark' : 'light'}-1280',
          );
          await tester.tap(find.byKey(const Key('receive-student-card')));
          await tester.pumpAndSettle();
          expect(find.text('تأكيد تسليم الكارت'), findsOneWidget);
          expect(
            tester
                .widget<TextField>(
                  find.byKey(const Key('cards-student-search')),
                )
                .enabled,
            isFalse,
          );
          expect(
            tester
                .widget<DropdownButtonFormField<CardReceiptFilter>>(
                  find.byKey(const Key('cards-receipt-filter')),
                )
                .onChanged,
            isNull,
          );
          await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
          await waitOperation(tester, notice: false);
          expect(store.cardReceipts, isEmpty);
          await tester.tap(find.byKey(const Key('receive-student-card')));
          await tester.pumpAndSettle();
          await tester.tap(find.byKey(const Key('confirm-card-receipt')));
          await waitOperation(tester);
          expect(store.cardReceipts, hasLength(1));
          expect(store.cardReceipts.single.paymentBypassed, isFalse);
          expect(
            store.cardReceipts.single.paymentId,
            store.cardPayments.single.id,
          );
          expect(find.text('لم يستلموا: 2'), findsOneWidget);
          expect(find.text('استلموا: 1'), findsOneWidget);
          expect(find.text('دفعوا ولم يستلموا: 0'), findsOneWidget);
          expect(store.payments, isEmpty);
          expect(store.attendances, isEmpty);
          expect(store.packages, isEmpty);
          expect(
            tester
                .widget<TextField>(
                  find.byKey(const Key('cards-student-search')),
                )
                .focusNode!
                .hasFocus,
            isTrue,
          );
          await tester.binding.setSurfaceSize(const Size(1440, 1000));
          await tester.pumpAndSettle();
          await capture(
            tester,
            'cards-received-${dark ? 'dark' : 'light'}-1440',
          );
          expect(tester.takeException(), isNull);
        });
      },
    );
  }

  testWidgets(
    'legacy bypass without configured price permits cashier handover and never infers payment from notes',
    (tester) async {
      await tester.runAsync(() async {
        await open(tester);
        await page(tester, 'إعدادات مازن');
        await tester.tap(find.byKey(const Key('card-setting-require-payment')));
        await tester.tap(find.byKey(const Key('save-card-settings')));
        await waitOperation(tester);
        expect(store.cardSettings.price, isNull);
        expect(store.cardSettings.requirePaymentBeforeReceipt, isFalse);
        store.signOut();
        await store.signIn('استقبال', password);
        await tester.pumpAndSettle();
        await page(tester, 'كروت الطلبة');
        await scan(tester, '303');
        expect(
          tester
              .widget<OutlinedButton>(find.byKey(const Key('pay-student-card')))
              .onPressed,
          isNull,
        );
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('receive-student-card')),
              )
              .onPressed,
          isNotNull,
        );
        expect(store.cardPaymentFor(store.students.last.id), isNull);
        await tester.tap(find.byKey(const Key('receive-student-card')));
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const Key('confirm-card-receipt')));
        await waitOperation(tester);
        expect(store.cardPayments, isEmpty);
        expect(store.cardReceipts.single.paymentBypassed, isTrue);
        expect(store.cardReceipts.single.paymentId, isNull);
        expect(find.textContaining('استلم بدون تحصيل مسجل'), findsOneWidget);
        store.signOut();
        await store.signIn('مدير آخر', password);
        await tester.pumpAndSettle();
        expect(store.canManage, isTrue);
        expect(store.canConfigureCards, isFalse);
        expect(find.widgetWithText(ListTile, 'إعدادات مازن'), findsNothing);
        await tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.dark,
            home: Scaffold(body: CardSettingsPage(store: store)),
          ),
        );
        await tester.pumpAndSettle();
        expect(
          find.text('هذه الإعدادات متاحة لحساب مازن المثبت فقط.'),
          findsOneWidget,
        );
        expect(find.byKey(const Key('save-card-settings')), findsNothing);
        expect(tester.takeException(), isNull);
      });
    },
  );

  testWidgets(
    'receipt and paid filters list actual statuses and bulk print only matching pending cards',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveCardSettings(const CenterCardSettings(price: 5000));
        await store.collectStudentCard(studentId: store.students.first.id);
        await store.receiveStudentCard(store.students.first.id);
        await store.collectStudentCard(studentId: store.students[1].id);
        await open(tester);
        await page(tester, 'كروت الطلبة');
        expect(find.text('كل الطلبة: 3'), findsOneWidget);
        expect(find.text('لم يستلموا: 2'), findsOneWidget);
        expect(find.text('دفعوا ولم يستلموا: 1'), findsOneWidget);
        expect(find.text('أحمد محمد'), findsNothing);
        expect(find.text('مينا سامح'), findsOneWidget);
        expect(find.text('يوسف قديم'), findsOneWidget);
        expect(find.text('طباعة / حفظ غير المستلمين (2)'), findsOneWidget);
        await tester.tap(find.byKey(const Key('cards-paid-only')));
        await tester.pumpAndSettle();
        expect(find.text('مينا سامح'), findsOneWidget);
        expect(find.text('يوسف قديم'), findsNothing);
        expect(find.text('طباعة / حفظ غير المستلمين (1)'), findsOneWidget);
        await receiptFilter(tester, 'استلموا');
        expect(find.text('أحمد محمد'), findsOneWidget);
        expect(find.text('مينا سامح'), findsNothing);
        expect(
          tester
              .widget<OutlinedButton>(
                find.byKey(const Key('print-pending-student-cards')),
              )
              .onPressed,
          isNull,
        );
        await receiptFilter(tester, 'كل الطلبة');
        expect(find.text('أحمد محمد'), findsOneWidget);
        expect(find.text('مينا سامح'), findsOneWidget);
        expect(find.text('طباعة / حفظ غير المستلمين (1)'), findsOneWidget);
        expect(store.cardReceipts, hasLength(1));
        expect(store.cardPayments, hasLength(2));
        expect(tester.takeException(), isNull);
      });
    },
  );
}
