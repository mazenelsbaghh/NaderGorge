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
import '../helpers/attendance_ui_helpers.dart';
import '../helpers/ui_wait_helpers.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Map<int, String> months;
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
          monthPlans: const [
            GroupMonthPlan(
              id: 'four',
              name: 'الشهر الكامل',
              sessions: 4,
              price: 40000,
            ),
            GroupMonthPlan(
              id: 'two',
              name: 'شهر حصتين',
              sessions: 2,
              price: 18000,
            ),
            GroupMonthPlan(
              id: 'three',
              name: 'شهر ثلاث حصص',
              sessions: 3,
              price: 27000,
            ),
          ],
          twoSessionPrice: 18000,
          threeSessionPrice: 27000,
        ),
      );
      months = await seedAttendanceMonths(store);
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
      await store.startSession(store.sessions.single.id);
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
            child: AttendanceWorkspace(
              store: store,
              initialSessionId: store.sessions.last.id,
              onExit: () {},
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> scan(WidgetTester tester, String code) =>
      previewAttendanceStudent(tester, code);

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
        await waitForUiCondition(
          tester,
          () =>
              find.byType(LinearProgressIndicator).evaluate().isEmpty &&
              store.payments.isNotEmpty &&
              find
                  .byKey(const Key('entry-confirmation-dialog'))
                  .evaluate()
                  .isEmpty,
          reason: 'Confirmed package persists once',
        );
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
    final attendanceCount = store.attendances.length;
    await tester.runAsync(() => requestAttendanceConfirmation(tester, key));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('entry-confirmation-dialog')), findsOneWidget);
    expect(store.attendances, hasLength(attendanceCount));
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
        if (key == LogicalKeyboardKey.keyN) {
          await requestAttendanceConfirmation(tester, key);
        } else {
          await tester.sendKeyEvent(key);
        }
      }
    });
    await tester.pumpAndSettle();
  }

  for (final count in [2, 3, 4]) {
    testWidgets(
      'Month selection selects $count and Enter persists the quoted package once',
      (tester) async {
        await openWorkspace(
          tester,
          dark: count == 2,
          width: count == 3 ? 960 : 1280,
        );
        await scan(tester, student.code);
        await confirmShortcut(tester, LogicalKeyboardKey.keyN);
        await selectAttendanceMonth(
          tester,
          count == 4
              ? 'الشهر الكامل · 4 حصص'
              : count == 2
              ? 'شهر حصتين · 2 حصص'
              : 'شهر ثلاث حصص · 3 حصص',
          confirmation: true,
        );
        expect(
          find.byKey(ValueKey('confirmation-month-${months[count]}')),
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

  testWidgets('Enter after a month selection commits only its rendered quote', (
    tester,
  ) async {
    await openWorkspace(tester);
    await scan(tester, student.code);
    await confirmShortcut(tester, LogicalKeyboardKey.keyN);
    await selectAttendanceMonth(
      tester,
      'شهر ثلاث حصص · 3 حصص',
      confirmation: true,
    );
    expectEmptyAccounts();
    await accept(tester);
    expect(store.packages.single.totalSessions, 3);
    expect(store.payments.single.netAmount, 20250);
    expectCodeFocus(tester);
  });

  testWidgets(
    'dropdown and Esc leave the workspace package selection unchanged',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);
      await selectAttendanceMonth(
        tester,
        'شهر حصتين · 2 حصص',
        confirmation: true,
      );
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(13500),
      );
      await cancel(tester);
      expectEmptyAccounts();
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);
      expect(
        find.byKey(ValueKey('confirmation-month-${months[4]}')),
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
    'held N repeats never change the month and cancellation restores the code',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await selectAttendanceMonth(tester, 'شهر ثلاث حصص · 3 حصص');
      await tester.runAsync(
        () => tester.sendKeyDownEvent(LogicalKeyboardKey.keyN),
      );
      await tester.pumpAndSettle();
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyN);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.keyN);
      await tester.pumpAndSettle();
      expect(
        find.byKey(ValueKey('confirmation-month-${months[3]}')),
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

  for (final code in ['n', 'NN-old', 'N2', 'N3-card']) {
    testWidgets(
      'scanner code $code cannot approve an ambiguous month shortcut',
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
          LogicalKeyboardKey.keyN,
          if (code != 'n')
            code.startsWith('NN')
                ? LogicalKeyboardKey.keyN
                : code.startsWith('N2')
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
        LogicalKeyboardKey.keyN,
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
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);
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
    'changed session accounting blocks the frozen quote until a valid context is reviewed',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);
      await tester.runAsync(
        () => store.saveSession(
          store.sessions.single.copyWith(
            kind: SessionKind.extra,
            extraPrice: 10000,
          ),
        ),
      );
      await selectAttendanceMonth(
        tester,
        'شهر حصتين · 2 حصص',
        confirmation: true,
      );
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
        () => store.saveSession(
          store.sessions.single.copyWith(
            kind: SessionKind.counted,
            extraPrice: 0,
          ),
        ),
      );
      await selectAttendanceMonth(
        tester,
        'شهر ثلاث حصص · 3 حصص',
        confirmation: true,
      );
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
    await confirmShortcut(tester, LogicalKeyboardKey.keyN);
    await selectAttendanceMonth(
      tester,
      'شهر حصتين · 2 حصص',
      confirmation: true,
    );
    await tester.runAsync(
      () => store.saveStudent(
        Student(
          name: 'كود أضيف متأخرًا',
          code: 'N',
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
    'previously paid balance stays covered after its old group price is changed',
    (tester) async {
      await tester.runAsync(() async {
        await store.renewPackage(
          PackageRequest(studentId: student.id, groupId: group.id, sessions: 2),
        );
        await store.saveGroup(
          group.copyWith(
            twoSessionPrice: 23000,
            monthPlans: [
              for (final plan in group.monthPlans)
                plan.id == months[2] ? plan.copyWith(price: 23000) : plan,
            ],
          ),
        );
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);
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
    'workspace month price refresh is shown before approving any purchase',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await selectAttendanceMonth(tester, 'شهر حصتين · 2 حصص');
      await tester.runAsync(
        () => store.saveGroup(
          group.copyWith(
            monthPlans: [
              for (final plan in group.monthPlans)
                plan.id == months[2] ? plan.copyWith(price: 22000) : plan,
            ],
          ),
        ),
      );
      await tester.pumpAndSettle();
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);

      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(16500),
      );
      expectEmptyAccounts();
      await accept(tester);
      expect(store.packages.single.totalSessions, 2);
      expect(store.payments.single.netAmount, 16500);
    },
  );

  testWidgets(
    'changing staff cannot be legitimized by selecting another quantity',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);
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
      await selectAttendanceMonth(
        tester,
        'شهر حصتين · 2 حصص',
        confirmation: true,
      );
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
    'price change cannot refresh an approved month quote silently on Enter',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);
      await selectAttendanceMonth(
        tester,
        'شهر حصتين · 2 حصص',
        confirmation: true,
      );
      await tester.runAsync(
        () => store.saveGroup(
          group.copyWith(
            twoSessionPrice: 20000,
            monthPlans: [
              for (final plan in group.monthPlans)
                plan.id == months[2] ? plan.copyWith(price: 20000) : plan,
            ],
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(13500),
      );
      await rejectQuote(tester, 'تغيّر السعر');
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.attendances.single.paymentPending, isTrue);
      expectCodeFocus(tester);
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(store.attendances.single.paymentPending, isTrue);
      await acknowledgeNotice(
        tester,
        message:
            'حضور الطالب مسجل بالفعل — غير مدفوع. استخدم L للحصة أو N للشهر لتسديده؛ لن نكرر الحضور.',
      );
      await confirmShortcut(tester, LogicalKeyboardKey.keyN);
      await selectAttendanceMonth(
        tester,
        'شهر حصتين · 2 حصص',
        confirmation: true,
      );
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
