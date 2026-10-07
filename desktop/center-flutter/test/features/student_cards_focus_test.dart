import 'dart:io';
import 'dart:async';
import '../helpers/notice_helpers.dart';
import 'dart:convert';
import 'package:cryptography/cryptography.dart';
import 'dart:ui' as ui;
import 'package:flutter/rendering.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/formatters.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  final captureKey = GlobalKey();
  late InstallationAdmin owner;
  _CardDialogObserver? observer;

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp('massar-entry-test-');
      store = await CenterStore.open(directory: directory.path);
      final salt = List<int>.generate(24, (i) => i + 1);
      final key =
          await Pbkdf2(
            macAlgorithm: Hmac.sha256(),
            iterations: 120000,
            bits: 256,
          ).deriveKey(
            secretKey: SecretKey(utf8.encode('card-owner-password')),
            nonce: salt,
          );
      owner = InstallationAdmin(
        id: 'card-owner',
        name: 'card-owner',
        credential: {
          'salt': base64Encode(salt),
          'hash': base64Encode(await key.extractBytes()),
          'algorithm': 'pbkdf2-sha256-120000',
        },
      );
      await store.ensureInstallationAdmin(owner);
      await store.signIn(owner.name, 'card-owner-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(
          CatalogEntry(
            name: switch (kind) {
              CatalogKind.subject => 'الفيزياء',
              CatalogKind.center => 'سنتر النور',
              CatalogKind.grade => 'الثالث الثانوي',
            },
            kind: kind,
          ),
        );
      }
      await store.saveGroup(
        StudyGroup(
          name: 'الأحد',
          subjectId: store.catalogs
              .firstWhere((item) => item.kind == CatalogKind.subject)
              .id,
          centerId: store.catalogs
              .firstWhere((item) => item.kind == CatalogKind.center)
              .id,
          gradeId: store.catalogs
              .firstWhere((item) => item.kind == CatalogKind.grade)
              .id,
          sessionPrice: 10000,
          packagePrice: 40000,
        ),
      );
      group = store.groups.single;
      await store.saveStudent(
        Student(
          name: 'أحمد محمد',
          code: '123',
          phone: '01012345678',
          guardianPhone: '01198765432',
          groupIds: [group.id],
          discountPercent: 25,
          createdAt: DateTime.now().subtract(const Duration(days: 20)),
        ),
      );
      student = store.students.single;
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 1,
          startsAt: DateTime.now().add(const Duration(minutes: 1)),
          createdAt: DateTime.now(),
        ),
      );
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> openWorkspace(
    WidgetTester tester, {
    bool dark = false,
    double width = 1440,
  }) async {
    await tester.binding.setSurfaceSize(Size(width, 900));
    await tester.runAsync(() async {
      final fonts = FontLoader('Tajawal')
        ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
        ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf'));
      await fonts.load();
      await (FontLoader(
        'MaterialIcons',
      )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    });
    addTearDown(() => tester.binding.setSurfaceSize(null));
    observer = _CardDialogObserver();
    await tester.pumpWidget(
      RepaintBoundary(
        key: captureKey,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          navigatorObservers: [observer!],
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: AttendanceWorkspace(store: store, onExit: () {}),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> scan(WidgetTester tester, String code) async {
    await tester.enterText(find.byKey(const Key('student-search')), code);
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
  }

  Future<void> mutateThroughUi(
    WidgetTester tester,
    Future<void> Function() gesture,
  ) async {
    await tester.runAsync(() async {
      final persisted = Completer<void>();
      void changed() {
        if (!persisted.isCompleted) persisted.complete();
      }

      store.addListener(changed);
      try {
        await gesture();
        await persisted.future.timeout(const Duration(seconds: 5));
        // Await the result popup only after the store publishes durable state.
        await Future<void>(() {});
        await acknowledgeNotice(tester);
      } finally {
        store.removeListener(changed);
      }
    });
    await tester.pumpAndSettle();
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    await tester.pump();
    await tester.runAsync(() async {
      final boundary =
          captureKey.currentContext!.findRenderObject()!
              as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 1);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      final target = File('build/verification/$name.png');
      await target.parent.create(recursive: true);
      await target.writeAsBytes(bytes!.buffer.asUint8List());
      image.dispose();
    });
  }

  void expectCodeFocus(WidgetTester tester) {
    expect(
      tester
          .widget<TextField>(find.byKey(const Key('student-search')))
          .focusNode!
          .hasFocus,
      isTrue,
    );
  }

  Future<void> asOwner(Future<void> Function() action) async {
    store.signOut();
    await store.signIn(owner.name, 'card-owner-password');
    await action();
    store.signOut();
    await store.signIn('cashier', 'card-cashier-password');
  }

  Future<void> setupCashier() async {
    await store.saveStaff(
      name: 'cashier',
      password: 'card-cashier-password',
      role: StaffRole.cashier,
    );
    store.signOut();
    await store.signIn('cashier', 'card-cashier-password');
  }

  testWidgets(
    'cashier pays and hands over card once independently of lesson payment, retaining historical amount after reload',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveCardSettings(const CenterCardSettings(price: 5000));
        await setupCashier();
      });
      await openWorkspace(tester, dark: true, width: 1280);
      await scan(tester, student.code);
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('receive-student-card')))
            .onPressed,
        isNull,
      );
      final lessonMethod = find.widgetWithText(
        DropdownButtonFormField<String>,
        'طريقة الدفع',
      );
      await tester.ensureVisible(lessonMethod);
      await tester.tap(lessonMethod);
      await tester.pumpAndSettle();
      await tester.tap(find.text('إنستاباي').last);
      await tester.pumpAndSettle();
      final cardMethod = find.widgetWithText(
        DropdownButtonFormField<String>,
        'طريقة دفع الكارت',
      );
      expect(
        tester.widget<DropdownButtonFormField<String>>(cardMethod).initialValue,
        'نقدي',
      );
      await tester.ensureVisible(cardMethod);
      await tester.tap(cardMethod);
      await tester.pumpAndSettle();
      await tester.tap(find.text('بطاقة').last);
      await tester.pumpAndSettle();
      final pay = find.byKey(const Key('pay-student-card'));
      await tester.ensureVisible(pay);
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.byKey(const Key('student-card-actions')));
      await tester.pumpAndSettle();
      await capture(tester, 'focus-card-unpaid-dark-1280');
      await tester.ensureVisible(pay);
      final stalePay = tester.widget<OutlinedButton>(pay).onPressed!;
      await mutateThroughUi(tester, () async {
        await tester.tap(pay);
        stalePay();
      });
      final payment = store.cardPayments.single;
      expect(payment.baseAmount, 5000);
      expect(payment.discountPercent, 25);
      expect(payment.netAmount, 3750);
      expect(payment.method, 'بطاقة');
      expect(payment.sessionId, store.sessions.single.id);
      expect(payment.groupId, group.id);
      expect(payment.studentId, student.id);
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.cardReceipts, isEmpty);
      expectCodeFocus(tester);
      final receive = find.byKey(const Key('receive-student-card'));
      await tester.ensureVisible(receive);
      final staleReceive = tester.widget<FilledButton>(receive).onPressed!;
      await mutateThroughUi(tester, () async {
        await tester.tap(receive);
        staleReceive();
      });
      expect(store.cardReceipts, hasLength(1));
      expect(store.cardReceiptFor(student.id)!.paymentId, payment.id);
      expect(store.cardReceiptFor(student.id)!.paymentBypassed, isFalse);
      expectCodeFocus(tester);
      await tester.runAsync(
        () => asOwner(() async {
          await store.saveCardSettings(const CenterCardSettings(price: 9000));
          await store.saveStudent(student.copyWith(discountPercent: 50));
        }),
      );
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<Text>(find.byKey(const Key('student-card-payment-status')))
            .data,
        contains(money(3750)),
      );
      expect(store.cardPayments.single.discountPercent, 25);
      expect(store.cardPayments.single.netAmount, 3750);
      expect(tester.widget<OutlinedButton>(pay).onPressed, isNull);
      expect(tester.widget<FilledButton>(receive).onPressed, isNull);
      await tester.runAsync(() async {
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.ensureInstallationAdmin(owner);
        await store.signIn('cashier', 'card-cashier-password');
      });
      await openWorkspace(tester, dark: true, width: 1280);
      await scan(tester, student.code);
      await tester.ensureVisible(find.byKey(const Key('student-card-actions')));
      await tester.pumpAndSettle();
      await capture(tester, 'focus-card-paid-received-dark-1280');
      expect(store.cardPayments, hasLength(1));
      expect(store.cardReceipts, hasLength(1));
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.packages, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'card actions guard unresolved codes, lookup, notes and dialogs; bypass receipt stays explicit after policy changes',
    (tester) async {
      late Student other;
      await tester.runAsync(() async {
        await store.saveStudent(
          Student(
            code: '124',
            name: 'مينا سامح',
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
        other = store.students.last;
        await store.saveCardSettings(
          const CenterCardSettings(
            price: 5000,
            requirePaymentBeforeReceipt: false,
          ),
        );
        await setupCashier();
      });
      await openWorkspace(tester, width: 1280);
      await scan(tester, student.code);
      final pay = find.byKey(const Key('pay-student-card'));
      final receive = find.byKey(const Key('receive-student-card'));
      final stalePay = tester.widget<OutlinedButton>(pay).onPressed!;
      final staleReceive = tester.widget<FilledButton>(receive).onPressed!;
      await tester.enterText(find.byKey(const Key('student-search')), '999');
      stalePay();
      staleReceive();
      await tester.pumpAndSettle();
      expect(store.cardPayments, isEmpty);
      expect(store.cardReceipts, isEmpty);
      expect(tester.widget<OutlinedButton>(pay).onPressed, isNull);
      await scan(tester, student.code);
      await tester.tap(find.text('بحث عن طالب'));
      await tester.pumpAndSettle();
      stalePay();
      staleReceive();
      expect(tester.widget<OutlinedButton>(pay).onPressed, isNull);
      expect(tester.widget<FilledButton>(receive).onPressed, isNull);
      await tester.tap(find.text('التحضير'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.byKey(const Key('edit-student-note')));
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      stalePay();
      staleReceive();
      await tester.pumpAndSettle();
      expect(store.cardPayments, isEmpty);
      expect(store.cardReceipts, isEmpty);
      await tester.ensureVisible(find.byKey(const Key('cancel-student-note')));
      await tester.tap(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      await tester.sendKeyEvent(LogicalKeyboardKey.f4);
      await tester.pumpAndSettle();
      stalePay();
      staleReceive();
      expect(store.cardPayments, isEmpty);
      expect(store.cardReceipts, isEmpty);
      await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      final cardMethod = find.widgetWithText(
        DropdownButtonFormField<String>,
        'طريقة دفع الكارت',
      );
      await tester.ensureVisible(cardMethod);
      await tester.tap(cardMethod);
      await tester.pumpAndSettle();
      await tester.tap(find.text('إنستاباي').last);
      await tester.pumpAndSettle();
      await tester.ensureVisible(receive);
      await mutateThroughUi(tester, () => tester.tap(receive));
      final handed = store.cardReceiptFor(student.id)!;
      expect(handed.paymentBypassed, isTrue);
      expect(handed.paymentId, isNull);
      expect(store.cardPayments, isEmpty);
      expectCodeFocus(tester);
      await tester.runAsync(
        () => asOwner(
          () => store.saveCardSettings(const CenterCardSettings(price: 7500)),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<Text>(find.byKey(const Key('student-card-received-detail')))
            .data,
        contains('استلم بدون تحصيل مسجل'),
      );
      expect(
        tester
            .widget<Text>(find.byKey(const Key('student-card-payment-status')))
            .data,
        'لا يوجد تحصيل مسجل للكارت.',
      );
      expect(tester.widget<OutlinedButton>(pay).onPressed, isNull);
      stalePay();
      staleReceive();
      await tester.pumpAndSettle();
      expect(store.cardPayments, isEmpty);
      expect(store.cardReceipts, hasLength(1));
      await scan(tester, other.code);
      expect(
        tester
            .widget<DropdownButtonFormField<String>>(
              find.widgetWithText(
                DropdownButtonFormField<String>,
                'طريقة دفع الكارت',
              ),
            )
            .initialValue,
        'نقدي',
      );
      expect(tester.widget<FilledButton>(receive).onPressed, isNull);
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.packages, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'unconfigured card fee is never quoted as zero and opening card PDF does not record receipt',
    (tester) async {
      await openWorkspace(tester, dark: true);
      await scan(tester, student.code);
      expect(
        tester
            .widget<Text>(find.byKey(const Key('student-card-payment-status')))
            .data,
        'رسوم الكارت غير محددة في إعدادات السنتر.',
      );
      expect(
        tester
            .widget<OutlinedButton>(find.byKey(const Key('pay-student-card')))
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('receive-student-card')))
            .onPressed,
        isNull,
      );
      final print = find.widgetWithText(TextButton, 'كارت الطالب');
      await tester.ensureVisible(print);
      await tester.runAsync(() async {
        await tester.tap(print);
        await tester.pump();
        await observer!.shown.future.timeout(const Duration(seconds: 5));
      });
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));
      expect(find.text('الكارت أو الإيصال'), findsOneWidget);
      expect(store.cardPayments, isEmpty);
      expect(store.cardReceipts, isEmpty);
      await tester.runAsync(
        () => tester.tap(find.widgetWithText(TextButton, 'إلغاء')),
      );
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(store.cardPayments, isEmpty);
      expect(store.cardReceipts, isEmpty);
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.packages, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );
}

class _CardDialogObserver extends NavigatorObserver {
  final shown = Completer<void>();
  @override
  void didPush(Route<dynamic> route, Route<dynamic>? previousRoute) {
    if (route is DialogRoute && !shown.isCompleted) shown.complete();
    super.didPush(route, previousRoute);
  }
}
