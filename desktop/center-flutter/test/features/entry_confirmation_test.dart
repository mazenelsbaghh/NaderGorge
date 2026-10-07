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

  for (final entryCase in [
    'single',
    'package2',
    'package3',
    'prepaid',
    'free',
    'extra',
    'makeup',
  ]) {
    testWidgets(
      'shortcut confirmation quotes and commits $entryCase without premature charge',
      (tester) async {
        await tester.runAsync(() async {
          await store.saveGroup(
            group.copyWith(twoSessionPrice: 18000, threeSessionPrice: 27000),
          );
          group = store.groups.single;
          if (entryCase == 'prepaid' || entryCase == 'makeup') {
            await store.renewPackage(
              PackageRequest(
                studentId: student.id,
                groupId: group.id,
                sessionId: store.sessions.single.id,
              ),
            );
          }
          if (entryCase == 'free' || entryCase == 'extra') {
            await store.saveSession(
              store.sessions.single.copyWith(
                kind: entryCase == 'free'
                    ? SessionKind.free
                    : SessionKind.extra,
                extraPrice: 8000,
              ),
            );
          } else if (entryCase == 'makeup') {
            await store.closeSession(store.sessions.single.id);
            await store.saveSession(
              LessonSession(
                groupId: group.id,
                number: 2,
                startsAt: DateTime.now().add(const Duration(minutes: 2)),
                createdAt: DateTime.now(),
              ),
            );
          }
        });
        final priorPayments = store.payments.length;
        final priorAttendance = store.attendances.length;
        await openWorkspace(
          tester,
          dark: entryCase == 'package2',
          width: entryCase == 'single' ? 960 : 1280,
        );
        await scan(tester, student.code);
        if (entryCase == 'package2' || entryCase == 'package3') {
          await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
          await tester.sendKeyEvent(
            entryCase == 'package2'
                ? LogicalKeyboardKey.digit2
                : LogicalKeyboardKey.digit3,
          );
          await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
          await tester.pumpAndSettle();
        } else if (entryCase == 'makeup') {
          final switcher = find.widgetWithText(TextButton, 'تعويض حصة غابها');
          await tester.ensureVisible(switcher);
          await tester.tap(switcher);
          await tester.pumpAndSettle();
          final chooser = find.widgetWithText(
            DropdownButtonFormField<String>,
            'الحصة الأصلية التي يعوضها',
          );
          await tester.ensureVisible(chooser);
          await tester.tap(chooser);
          await tester.pumpAndSettle();
          await tester.tap(find.textContaining('حصة 1 ·').last);
          await tester.pumpAndSettle();
        }
        final shortcut = ['package2', 'package3', 'prepaid'].contains(entryCase)
            ? LogicalKeyboardKey.keyM
            : LogicalKeyboardKey.keyL;
        await tester.runAsync(() => tester.sendKeyEvent(shortcut));
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('entry-confirmation-dialog')),
          findsOneWidget,
        );
        expect(store.payments, hasLength(priorPayments));
        expect(store.attendances, hasLength(priorAttendance));
        final net = switch (entryCase) {
          'single' => 7500,
          'package2' => 13500,
          'package3' => 20250,
          'extra' => 6000,
          _ => 0,
        };
        expect(
          tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
          money(net),
        );
        if (['prepaid', 'free', 'makeup'].contains(entryCase)) {
          expect(find.text('لا يوجد تحصيل جديد'), findsOneWidget);
        }
        if (entryCase == 'package2') {
          await capture(tester, 'entry-confirmation-package2-dark-1280');
        }
        await cancel(tester);
        expect(store.payments, hasLength(priorPayments));
        expect(store.attendances, hasLength(priorAttendance));
        await tester.runAsync(() => tester.sendKeyEvent(shortcut));
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('entry-confirmation-dialog')),
          findsOneWidget,
        );
        if (entryCase == 'package3') {
          final action = find.byKey(const Key('confirm-entry-confirmation'));
          final staleConfirm = tester.widget<FilledButton>(action).onPressed!;
          await mutateThroughUi(tester, () async {
            await tester.tap(action);
            staleConfirm();
          });
        } else {
          await accept(
            tester,
            key: entryCase == 'prepaid'
                ? LogicalKeyboardKey.numpadEnter
                : LogicalKeyboardKey.enter,
          );
        }
        expect(store.attendances, hasLength(priorAttendance + 1));
        expect(
          store.payments,
          hasLength(
            priorPayments +
                (['single', 'package2', 'package3', 'extra'].contains(entryCase)
                    ? 1
                    : 0),
          ),
        );
        if (net > 0) expect(store.payments.last.netAmount, net);
        if (entryCase.startsWith('package')) {
          expect(
            store.packages.single.remaining,
            entryCase == 'package2' ? 1 : 2,
          );
        }
        if (entryCase == 'prepaid') expect(store.packages.single.remaining, 3);
        if (entryCase == 'makeup') {
          expect(store.attendances.last.status, AttendanceStatus.makeup);
        }
        expectCodeFocus(tester);
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await tester.pumpAndSettle();
        expect(store.attendances, hasLength(priorAttendance + 1));
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'scanner text during pending or focused confirmation blocks Enter and requires a safe rescan',
    (tester) async {
      late Student other;
      await tester.runAsync(() async {
        await store.saveStudent(
          Student(
            name: 'الطالب التالي',
            code: 'MS-456',
            groupIds: [group.id],
            createdAt: student.createdAt,
          ),
        );
        other = store.students.last;
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      // No frame between opening shortcut and subsequent scanner characters.
      await tester.runAsync(() async {
        await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
        await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
        await tester.sendKeyEvent(LogicalKeyboardKey.minus);
        await tester.sendKeyEvent(LogicalKeyboardKey.digit4);
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      });
      await tester.pumpAndSettle();
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
      await tester.sendKeyEvent(LogicalKeyboardKey.numpadEnter);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      await cancel(tester);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(store.attendances, isEmpty);
      await scan(tester, other.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.digit9);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      await cancel(tester);
      await scan(tester, other.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyL);
      await accept(tester);
      expect(store.attendances.single.studentId, other.id);
      expect(store.payments.single.studentId, other.id);
      expectCodeFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'a stale M quote cannot fall through to single entry on Enter and a fresh package quote is required',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(30000),
      );
      await tester.runAsync(
        () => store.saveGroup(group.copyWith(packagePrice: 50000)),
      );
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(30000),
      );
      await rejectQuote(tester, 'تغيّر السعر');
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expectCodeFocus(tester);
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-package')))
            .onPressed,
        isNotNull,
      );
      await tester.runAsync(() async {
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await tester.testTextInput.receiveAction(TextInputAction.done);
      });
      expect(store.payments, isEmpty);
      await acknowledgeNotice(
        tester,
        message: 'لم يتم التسجيل. راجع الحساب واضغط L أو M للتأكيد من جديد.',
      );
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(37500),
      );
      await accept(tester);
      expect(store.payments.single.netAmount, 37500);
      expect(store.packages.single.totalSessions, 4);
      expect(store.packages.single.remaining, 3);
      expect(store.attendances.single.packageId, store.packages.single.id);
      expectCodeFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'a different cashier must review a fresh quote before a previously confirmed request can save',
    (tester) async {
      await tester.runAsync(
        () => store.saveStaff(
          name: 'موظف آخر',
          password: 'another-cashier-password',
          role: StaffRole.cashier,
        ),
      );
      await openWorkspace(tester);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyL);
      await tester.runAsync(() async {
        store.signOut();
        await store.signIn('موظف آخر', 'another-cashier-password');
      });
      await rejectQuote(tester, 'تغيّر السعر');
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expectCodeFocus(tester);
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.payments, isEmpty);
      await acknowledgeNotice(
        tester,
        message: 'لم يتم التسجيل. راجع الحساب واضغط L أو M للتأكيد من جديد.',
      );
      await confirmShortcut(tester, LogicalKeyboardKey.keyL);
      await accept(tester);
      expect(store.payments.single.staffId, store.currentUser!.id);
      expect(store.payments.single.netAmount, 7500);
      expect(store.attendances, hasLength(1));
      expectCodeFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'long student name fits light confirmation and other shortcuts cannot change its frozen target',
    (tester) async {
      await tester.runAsync(
        () => store.saveStudent(
          student.copyWith(
            name:
                'أحمد محمد عبد الرحمن مصطفى إبراهيم عبد الله محمود حسن عبد العزيز',
          ),
        ),
      );
      student = store.students.single;
      await openWorkspace(tester, width: 960);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.f4);
      await tester.sendKeyEvent(LogicalKeyboardKey.f6);
      await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
      await tester.sendKeyEvent(LogicalKeyboardKey.digit2);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
      await tester.pumpAndSettle();
      expect(
        find.byKey(const Key('entry-confirmation-dialog')),
        findsOneWidget,
      );
      expect(find.text('إضافة طالب وتسجيله'), findsNothing);
      expect(
        tester
            .widget<Text>(find.byKey(const Key('confirmation-student-name')))
            .data,
        student.name,
      );
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(7500),
      );
      expect(
        tester
            .getRect(find.byKey(const Key('confirm-entry-confirmation')))
            .bottom,
        lessThanOrEqualTo(900),
      );
      await capture(tester, 'entry-confirmation-long-name-light-960');
      await cancel(tester);
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-attend')))
            .onPressed,
        isNotNull,
      );
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );
}
