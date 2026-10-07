import 'dart:io';
import 'dart:async';
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
import '../helpers/notice_helpers.dart' as notices;

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Student student;
  final captureKey = GlobalKey();

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp('massar-entry-test-');
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('مدير الاختبار', 'test-password-2026');
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
          twoSessionPrice: 18000,
          threeSessionPrice: 27000,
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

  Future<void> acknowledgeNotice(WidgetTester tester, {String? message}) async {
    await tester.runAsync(
      () => notices.acknowledgeNotice(tester, message: message),
    );
  }

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
    await tester.pumpWidget(
      RepaintBoundary(
        key: captureKey,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          builder: (context, child) =>
              Directionality(textDirection: TextDirection.rtl, child: child!),
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
        await Future<void>(() {});
        await notices.acknowledgeNotice(tester);
      } finally {
        store.removeListener(changed);
      }
    });
    await tester.pumpAndSettle();
  }

  Future<void> confirmShortcut(
    WidgetTester tester,
    LogicalKeyboardKey key,
  ) async {
    await tester.runAsync(() => tester.sendKeyEvent(key));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('entry-confirmation-dialog')), findsOneWidget);
    expect(
      store.attendances.where((a) => a.status == AttendanceStatus.present),
      isEmpty,
    );
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

  Future<void> accept(
    WidgetTester tester, {
    LogicalKeyboardKey key = LogicalKeyboardKey.enter,
  }) => mutateThroughUi(tester, () => tester.sendKeyEvent(key));

  Future<void> cancel(WidgetTester tester) async {
    await tester.runAsync(() => tester.sendKeyEvent(LogicalKeyboardKey.escape));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('entry-confirmation-dialog')), findsNothing);
    if (find.byKey(const Key('massar-notice-dialog')).evaluate().isNotEmpty) {
      await acknowledgeNotice(tester);
    }
    expectCodeFocus(tester);
  }

  Future<void> rejectQuote(WidgetTester tester, String message) async {
    await tester.runAsync(() => tester.sendKeyEvent(LogicalKeyboardKey.enter));
    for (var i = 0; i < 40; i++) {
      await tester.runAsync(
        () => Future<void>.delayed(const Duration(milliseconds: 10)),
      );
      await tester.pump(const Duration(milliseconds: 100));
      if (find.textContaining(message).evaluate().isNotEmpty) break;
    }
    expect(find.textContaining(message), findsOneWidget);
    await acknowledgeNotice(tester);
  }

  Future<void> key(WidgetTester tester, LogicalKeyboardKey key) async {
    await tester.runAsync(() => tester.sendKeyEvent(key));
    await tester.pumpAndSettle();
  }

  void expectEmptyAccounts() {
    expect(store.payments, isEmpty);
    expect(store.packages, isEmpty);
    expect(store.attendances, isEmpty);
  }

  Future<void> pendingSequence(
    WidgetTester tester,
    List<LogicalKeyboardKey> keys,
  ) async {
    await tester.runAsync(() async {
      for (final key in keys) {
        await tester.sendKeyEvent(key);
      }
    });
    await tester.pumpAndSettle();
  }

  Future<void> waitForEntry(WidgetTester tester) async {
    for (var i = 0; i < 50 && store.attendances.isEmpty; i++) {
      await tester.runAsync(
        () => Future<void>.delayed(const Duration(milliseconds: 10)),
      );
      await tester.pump(const Duration(milliseconds: 100));
    }
    expect(store.attendances, hasLength(1));
    await acknowledgeNotice(tester);
    await tester.pumpAndSettle();
  }

  for (final count in [2, 3, 4]) {
    testWidgets(
      'M sequence selects $count and Enter persists the quoted package once',
      (tester) async {
        await openWorkspace(
          tester,
          dark: count == 2,
          width: count == 3 ? 960 : 1280,
        );
        await scan(tester, student.code);
        await confirmShortcut(tester, LogicalKeyboardKey.keyM);
        await key(
          tester,
          count == 4
              ? LogicalKeyboardKey.keyM
              : count == 2
              ? LogicalKeyboardKey.numpad2
              : LogicalKeyboardKey.digit3,
        );
        expect(
          find.byKey(ValueKey('confirmation-package-count-$count')),
          findsOneWidget,
        );
        final amount = count == 2
            ? 13500
            : count == 3
            ? 20250
            : 30000;
        expect(
          tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
          money(amount),
        );
        expectEmptyAccounts();
        expect(
          tester
              .getRect(find.byKey(const Key('confirm-entry-confirmation')))
              .bottom,
          lessThanOrEqualTo(900),
        );
        if (count != 4) {
          await capture(
            tester,
            'package-sequence-$count-${count == 2 ? 'dark-1280' : 'light-960'}',
          );
        }
        await accept(tester);
        expect(store.payments.single.netAmount, amount);
        expect(store.packages.single.totalSessions, count);
        expect(store.packages.single.remaining, count - 1);
        expect(store.attendances.single.packageId, store.packages.single.id);
        expectCodeFocus(tester);
        await key(tester, LogicalKeyboardKey.enter);
        expect(store.payments, hasLength(1));
        expect(store.attendances, hasLength(1));
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'same-frame M3 Enter commits only after rendering its selected quote',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await tester.runAsync(() async {
        await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
        await tester.sendKeyEvent(LogicalKeyboardKey.digit3);
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        expectEmptyAccounts();
      });
      await tester.pumpAndSettle();
      await waitForEntry(tester);
      expect(store.packages.single.totalSessions, 3);
      expect(store.payments.single.netAmount, 20250);
      expectCodeFocus(tester);
    },
  );

  testWidgets(
    'dropdown and Esc leave the workspace package selection unchanged',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await tester.runAsync(
        () => tester.tap(
          find.byKey(const ValueKey('confirmation-package-count-4')),
        ),
      );
      await tester.pumpAndSettle();
      await tester.runAsync(() => tester.tap(find.text('2 حصص').last));
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(13500),
      );
      await cancel(tester);
      expectEmptyAccounts();
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      expect(
        find.byKey(const ValueKey('confirmation-package-count-4')),
        findsOneWidget,
      );
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(30000),
      );
      await cancel(tester);
    },
  );

  testWidgets(
    'held M repeats never mean MM and cancellation restores the code',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
      await key(tester, LogicalKeyboardKey.digit3);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
      await tester.runAsync(
        () => tester.sendKeyDownEvent(LogicalKeyboardKey.keyM),
      );
      await tester.pumpAndSettle();
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.keyM);
      await tester.pumpAndSettle();
      expect(
        find.byKey(const ValueKey('confirmation-package-count-3')),
        findsOneWidget,
      );
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(20250),
      );
      await cancel(tester);
      expectEmptyAccounts();
    },
  );

  for (final code in ['m', 'MM-old', 'M2', 'M3-card']) {
    testWidgets(
      'legacy code $code poisons ambiguous shortcut before scanner Enter',
      (tester) async {
        await tester.runAsync(
          () => store.saveStudent(
            Student(
              name: 'طالب آخر',
              code: code,
              groupIds: [group.id],
              createdAt: DateTime.now(),
            ),
          ),
        );
        await openWorkspace(tester);
        await scan(tester, student.code);
        await pendingSequence(tester, [
          LogicalKeyboardKey.keyM,
          if (code != 'm')
            code.startsWith('MM')
                ? LogicalKeyboardKey.keyM
                : code.startsWith('M2')
                ? LogicalKeyboardKey.digit2
                : LogicalKeyboardKey.digit3,
          LogicalKeyboardKey.enter,
        ]);
        expect(
          find.byKey(const Key('entry-confirmation-dialog')),
          findsOneWidget,
        );
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('confirm-entry-confirmation')),
              )
              .onPressed,
          isNull,
        );
        expectEmptyAccounts();
        await cancel(tester);
      },
    );
  }

  testWidgets(
    'extra scanner characters poison both pending and visible sequence',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await pendingSequence(tester, [
        LogicalKeyboardKey.keyM,
        LogicalKeyboardKey.digit2,
        LogicalKeyboardKey.digit3,
        LogicalKeyboardKey.enter,
      ]);
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('confirm-entry-confirmation')),
            )
            .onPressed,
        isNull,
      );
      expectEmptyAccounts();
      await cancel(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await key(tester, LogicalKeyboardKey.digit3);
      await key(tester, LogicalKeyboardKey.numpad1);
      await key(tester, LogicalKeyboardKey.enter);
      expectEmptyAccounts();
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('confirm-entry-confirmation')),
            )
            .onPressed,
        isNull,
      );
      await cancel(tester);
    },
  );

  testWidgets(
    'missing price disables old quote but a configured quantity can recover within preview',
    (tester) async {
      await tester.runAsync(
        () => store.saveGroup(
          StudyGroup.fromJson({...group.toJson(), 'twoSessionPrice': null}),
        ),
      );
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await key(tester, LogicalKeyboardKey.digit2);
      expect(find.byKey(const Key('confirmation-price-error')), findsOneWidget);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        'راجع السعر أولًا',
      );
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('confirm-entry-confirmation')),
            )
            .onPressed,
        isNull,
      );
      await key(tester, LogicalKeyboardKey.enter);
      expectEmptyAccounts();
      await tester.runAsync(
        () => tester.tap(
          find.byKey(const ValueKey('confirmation-package-count-2')),
        ),
      );
      await tester.pumpAndSettle();
      await tester.runAsync(() => tester.tap(find.text('3 حصص').last));
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(20250),
      );
      await accept(tester);
      expect(store.packages.single.totalSessions, 3);
      expect(store.payments.single.netAmount, 20250);
    },
  );

  testWidgets('newly imported command code blocks confirmation recheck', (
    tester,
  ) async {
    await openWorkspace(tester);
    await scan(tester, student.code);
    await confirmShortcut(tester, LogicalKeyboardKey.keyM);
    await key(tester, LogicalKeyboardKey.digit2);
    await tester.runAsync(
      () => store.saveStudent(
        Student(
          name: 'كود أضيف متأخرًا',
          code: 'M2-new',
          groupIds: [group.id],
          createdAt: DateTime.now(),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await key(tester, LogicalKeyboardKey.enter);
    expectEmptyAccounts();
    expect(
      tester
          .widget<FilledButton>(
            find.byKey(const Key('confirm-entry-confirmation')),
          )
          .onPressed,
      isNull,
    );
    await cancel(tester);
  });

  testWidgets(
    'previously paid balance remains free even for unavailable price',
    (tester) async {
      await tester.runAsync(() async {
        await store.renewPackage(
          PackageRequest(studentId: student.id, groupId: group.id, sessions: 2),
        );
        await store.saveGroup(
          StudyGroup.fromJson({...group.toJson(), 'threeSessionPrice': null}),
        );
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      await key(tester, LogicalKeyboardKey.keyM);
      await key(tester, LogicalKeyboardKey.digit3);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(0),
      );
      expect(find.text('دخول من الباقة السارية بدون دفع جديد'), findsOneWidget);
      final payments = store.payments.length;
      await accept(tester);
      expect(store.packages.single.totalSessions, 2);
      expect(store.packages.single.remaining, 1);
      expect(store.payments, hasLength(payments));
      expectCodeFocus(tester);
    },
  );
  testWidgets(
    'initial unavailable selection still opens M so M3 can select a configured price',
    (tester) async {
      await tester.runAsync(
        () => store.saveGroup(
          StudyGroup.fromJson({...group.toJson(), 'twoSessionPrice': null}),
        ),
      );
      await openWorkspace(tester);
      await scan(tester, student.code);
      await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
      await key(tester, LogicalKeyboardKey.digit2);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      expect(find.byKey(const Key('confirmation-price-error')), findsOneWidget);
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('confirm-entry-confirmation')),
            )
            .onPressed,
        isNull,
      );
      await key(tester, LogicalKeyboardKey.digit3);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(20250),
      );
      expectEmptyAccounts();
      await accept(tester);
      expect(store.packages.single.totalSessions, 3);
      expect(store.payments.single.netAmount, 20250);
    },
  );

  testWidgets(
    'changing staff cannot be legitimized by selecting another quantity',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'موظف آخر',
          role: StaffRole.cashier,
          password: 'another-cashier-password',
        );
        store.signOut();
        await store.signIn('موظف آخر', 'another-cashier-password');
      });
      await tester.pumpAndSettle();
      await key(tester, LogicalKeyboardKey.digit2);
      expect(find.textContaining('تغيّر الموظف أو سياق الحصة'), findsOneWidget);
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('confirm-entry-confirmation')),
            )
            .onPressed,
        isNull,
      );
      await key(tester, LogicalKeyboardKey.enter);
      expectEmptyAccounts();
      await cancel(tester);
    },
  );

  testWidgets(
    'price change cannot refresh an approved M2 quote silently on Enter',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await key(tester, LogicalKeyboardKey.digit2);
      await tester.runAsync(
        () => store.saveGroup(group.copyWith(twoSessionPrice: 20000)),
      );
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(13500),
      );
      await rejectQuote(tester, 'تغيّر السعر');
      expectEmptyAccounts();
      expectCodeFocus(tester);
      await key(tester, LogicalKeyboardKey.enter);
      expectEmptyAccounts();
      await acknowledgeNotice(
        tester,
        message: 'لم يتم التسجيل. راجع الحساب واضغط L أو M للتأكيد من جديد.',
      );
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await key(tester, LogicalKeyboardKey.digit2);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(15000),
      );
      await accept(tester);
      expect(store.payments.single.netAmount, 15000);
      expect(store.packages.single.totalSessions, 2);
    },
  );
}
