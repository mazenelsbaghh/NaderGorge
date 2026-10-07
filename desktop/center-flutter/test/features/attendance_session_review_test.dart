import '../helpers/notice_helpers.dart';
import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/features/management/review_page.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group, otherGroup;
  late Student paid, unpaid;
  late LessonSession old, upcoming, foreign;
  final captureKey = GlobalKey();
  final reviewButton = find.byKey(const Key('session-payment-review'));
  final reviewCode = find.byKey(const Key('payment-check-code'));
  final attendanceCode = find.byKey(const Key('student-search'));

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      directory = await Directory.systemTemp.createTemp(
        'massar-session-review-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('مدير السنتر', 'local-password-2026');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(
          CatalogEntry(
            kind: kind,
            name: switch (kind) {
              CatalogKind.subject => 'الفيزياء',
              CatalogKind.center => 'سنتر النور',
              CatalogKind.grade => 'الثالث الثانوي',
            },
          ),
        );
      }
      for (final name in ['الأحد', 'مجموعة أخرى']) {
        await store.saveGroup(
          StudyGroup(
            name: name,
            subjectId: store.catalogs
                .firstWhere((e) => e.kind == CatalogKind.subject)
                .id,
            centerId: store.catalogs
                .firstWhere((e) => e.kind == CatalogKind.center)
                .id,
            gradeId: store.catalogs
                .firstWhere((e) => e.kind == CatalogKind.grade)
                .id,
            sessionPrice: 10000,
            packagePrice: 40000,
          ),
        );
      }
      group = store.groups.first;
      otherGroup = store.groups.last;
      for (final code in ['123', 'M']) {
        await store.saveStudent(
          Student(
            code: code,
            name: code == '123' ? 'أحمد محمد' : 'مريم أحمد',
            groupIds: [group.id],
            createdAt: DateTime.now().subtract(const Duration(days: 10)),
          ),
        );
      }
      paid = store.students.first;
      unpaid = store.students.last;
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 1,
          startsAt: DateTime.now().subtract(const Duration(hours: 4)),
          createdAt: DateTime.now(),
        ),
      );
      old = store.sessions.single;
      await store.collectAndAttend(
        EntryRequest(
          studentId: paid.id,
          sessionId: old.id,
          mode: EntryMode.single,
        ),
      );
      await store.closeSession(old.id);
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: DateTime.now().add(const Duration(hours: 1)),
          createdAt: DateTime.now(),
        ),
      );
      upcoming = store.sessions.last;
      await store.saveSession(
        LessonSession(
          groupId: otherGroup.id,
          number: 99,
          startsAt: DateTime.now().add(const Duration(hours: 2)),
          createdAt: DateTime.now(),
        ),
      );
      foreign = store.sessions.last;
    }),
  );
  tearDown(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> open(WidgetTester tester, {bool dark = false}) async {
    await tester.binding.setSurfaceSize(const Size(1280, 800));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.runAsync(() async {
      await (FontLoader('Tajawal')
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
          .load();
      await (FontLoader(
        'MaterialIcons',
      )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
    });
    await tester.pumpWidget(
      RepaintBoundary(
        key: captureKey,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          builder: (context, child) =>
              Directionality(textDirection: TextDirection.rtl, child: child!),
          home: AttendanceWorkspace(
            store: store,
            onExit: () {},
            initialSessionId: upcoming.id,
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> press(WidgetTester tester, LogicalKeyboardKey key) async {
    await tester.runAsync(() => tester.sendKeyEvent(key));
    await tester.pumpAndSettle();
  }

  Future<void> showStudent(WidgetTester tester) async {
    await tester.enterText(attendanceCode, paid.code);
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
  }

  Future<void> selectSession(WidgetTester tester, LessonSession session) async {
    final selector = find.byWidgetPredicate(
      (widget) =>
          widget is DropdownButtonFormField<String> &&
          widget.key is ValueKey<String> &&
          (widget.key! as ValueKey<String>).value.startsWith('session-'),
    );
    await tester.runAsync(() => tester.tap(selector));
    await tester.pumpAndSettle();
    await tester.runAsync(
      () => tester.tap(
        find
            .text(
              'حصة ${session.number} · ${shortDate(session.startsAt)}${session.id == old.id ? ' · مغلقة' : ''}',
            )
            .last,
      ),
    );
    await tester.pumpAndSettle();
    if (session.id == old.id) await press(tester, LogicalKeyboardKey.escape);
  }

  Future<void> openReview(WidgetTester tester) async {
    await tester.runAsync(() => tester.tap(reviewButton));
    await tester.pumpAndSettle();
    expect(
      find.byKey(const Key('attendance-session-review-dialog')),
      findsOneWidget,
    );
    expect(tester.widget<TextField>(reviewCode).focusNode!.hasFocus, isTrue);
  }

  Future<void> mutate(
    WidgetTester tester,
    Future<void> Function() gesture,
  ) async {
    await tester.runAsync(() async {
      final saved = Completer<void>();
      void changed() {
        if (!saved.isCompleted) saved.complete();
      }

      store.addListener(changed);
      try {
        await gesture();
        await saved.future.timeout(const Duration(seconds: 5));
        await Future<void>(() {});
        await tester.pump(const Duration(milliseconds: 200));
        if (find
            .byKey(const Key('massar-notice-dialog'))
            .evaluate()
            .isNotEmpty) {
          await acknowledgeNotice(tester);
        }
      } finally {
        store.removeListener(changed);
      }
    });
    await tester.pumpAndSettle();
  }

  Map<String, Object> accountSnapshot() => {
    'attendance': store.allAttendances.map((e) => e.toJson()).toList(),
    'payments': store.allPayments.map((e) => e.toJson()).toList(),
    'packages': store.allPackages.map((e) => e.toJson()).toList(),
    'cards': store.cardPayments.map((e) => e.toJson()).toList(),
    'receipts': store.cardReceipts.map((e) => e.toJson()).toList(),
    'sessions': store.sessions.map((e) => e.toJson()).toList(),
  };

  void expectAttendanceFocus(WidgetTester tester) => expect(
    tester.widget<TextField>(attendanceCode).focusNode!.hasFocus,
    isTrue,
  );
  bool enabled(WidgetTester tester) =>
      tester.widget<OutlinedButton>(reviewButton).onPressed != null;

  void expectReviewRowsVisible(WidgetTester tester, int count) {
    final tableFinder = find.descendant(
      of: find.byType(ReviewPage),
      matching: find.byType(Table),
    );
    final table = tester.renderObject<RenderTable>(tableFinder);
    final viewport = tester.getRect(
      find
          .ancestor(
            of: tableFinder,
            matching: find.byWidgetPredicate(
              (widget) =>
                  widget is SingleChildScrollView &&
                  widget.scrollDirection == Axis.vertical,
            ),
          )
          .first,
    );
    final popup = tester.getRect(
      find.byKey(const Key('attendance-session-review-dialog')),
    );
    for (var row = 1; row <= count; row++) {
      final bounds = table.getRowBox(row);
      final rect = Rect.fromPoints(
        table.localToGlobal(bounds.topLeft),
        table.localToGlobal(bounds.bottomRight),
      );
      expect(rect.top, greaterThanOrEqualTo(viewport.top));
      expect(
        rect.bottom,
        lessThanOrEqualTo(viewport.bottom),
        reason: 'Checked row $row must fit inside the actual table clip.',
      );
      expect(rect.bottom, lessThanOrEqualTo(popup.bottom));
    }
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI') &&
        Platform.environment['CAPTURE_UI'] != 'true') {
      return;
    }
    await tester.runAsync(() async {
      final boundary =
          captureKey.currentContext!.findRenderObject()!
              as RenderRepaintBoundary;
      final image = await boundary.toImage(pixelRatio: 1);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      final file = File('build/verification/$name.png');
      await file.parent.create(recursive: true);
      await file.writeAsBytes(bytes!.buffer.asUint8List());
      image.dispose();
    });
  }

  testWidgets(
    'review checks the explicitly chosen old closed session and restores student and search draft',
    (tester) async {
      await open(tester);
      await showStudent(tester);
      await selectSession(tester, old);
      await tester.enterText(attendanceCode, 'مسودة بحث');
      final snapshot = accountSnapshot();
      await openReview(tester);
      expect(
        tester.widget<ReviewPage>(find.byType(ReviewPage)).sessionId,
        old.id,
      );
      expect(
        find.descendant(
          of: find.byType(ReviewPage),
          matching: find.byType(DropdownButtonFormField<String>),
        ),
        findsNothing,
      );
      expect(find.text('مقارنة مبلغ الورق'), findsNothing);
      for (final student in [paid, unpaid]) {
        await tester.enterText(reviewCode, student.code);
        await mutate(
          tester,
          () => tester.testTextInput.receiveAction(TextInputAction.search),
        );
        final field = tester.widget<TextField>(reviewCode);
        expect(field.focusNode!.hasFocus, isTrue);
        expect(
          field.controller!.selection,
          TextSelection(baseOffset: 0, extentOffset: student.code.length),
        );
      }
      expect(store.paymentChecks.map((e) => e.sessionId).toSet(), {old.id});
      expect(
        store.paymentChecks.firstWhere((e) => e.studentId == paid.id).status,
        StudentPaymentStatus.paidSingle,
      );
      expect(
        store.paymentChecks.firstWhere((e) => e.studentId == unpaid.id).status,
        StudentPaymentStatus.notPaid,
      );
      expect(
        store.paymentChecks.any(
          (e) => e.sessionId == upcoming.id || e.sessionId == foreign.id,
        ),
        isFalse,
      );
      expect(accountSnapshot(), snapshot);
      expectReviewRowsVisible(tester, 2);
      await capture(tester, 'attendance-session-review-light-1280');
      await tester.binding.setSurfaceSize(const Size(960, 800));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expectReviewRowsVisible(tester, 2);
      await capture(tester, 'attendance-session-review-light-960');
      await press(tester, LogicalKeyboardKey.escape);
      expect(find.byType(ReviewPage), findsNothing);
      expect(find.byKey(ValueKey('session-${old.id}')), findsOneWidget);
      expect(find.text('سجل ${paid.name}'), findsOneWidget);
      expect(
        tester.widget<TextField>(attendanceCode).controller!.text,
        'مسودة بحث',
      );
      expectAttendanceFocus(tester);
      expect(accountSnapshot(), snapshot);
    },
  );

  testWidgets(
    'double opening and pending attendance shortcuts cannot stack dialogs or charge',
    (tester) async {
      await open(tester, dark: true);
      await showStudent(tester);
      final snapshot = accountSnapshot();
      final openCallback = tester
          .widget<OutlinedButton>(reviewButton)
          .onPressed!;
      await tester.runAsync(() async {
        openCallback();
        openCallback();
        for (final key in [
          LogicalKeyboardKey.enter,
          LogicalKeyboardKey.keyL,
          LogicalKeyboardKey.keyM,
          LogicalKeyboardKey.f4,
          LogicalKeyboardKey.f6,
        ]) {
          await tester.sendKeyEvent(key);
        }
      });
      await tester.pumpAndSettle();
      expect(find.byType(ReviewPage), findsOneWidget);
      expect(find.byKey(const Key('entry-confirmation-dialog')), findsNothing);
      expect(find.text('إضافة طالب وتسجيله'), findsNothing);
      expect(
        tester.widget<ReviewPage>(find.byType(ReviewPage)).sessionId,
        upcoming.id,
      );
      await tester.enterText(reviewCode, unpaid.code);
      await mutate(
        tester,
        () => tester.testTextInput.receiveAction(TextInputAction.search),
      );
      expect(store.paymentChecks.single.studentId, unpaid.id);
      expect(store.paymentChecks.single.sessionId, upcoming.id);
      expect(store.paymentChecks.single.status, StudentPaymentStatus.notPaid);
      expect(accountSnapshot(), snapshot);
      expectReviewRowsVisible(tester, 1);
      await capture(tester, 'attendance-session-review-dark-1280');
      await press(tester, LogicalKeyboardKey.escape);
      expectAttendanceFocus(tester);
      expect(find.text('سجل ${paid.name}'), findsOneWidget);
      expect(accountSnapshot(), snapshot);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'closing during a pending check keeps its frozen class and restores attendance safely',
    (tester) async {
      await open(tester);
      await showStudent(tester);
      final snapshot = accountSnapshot();
      await openReview(tester);
      final submit = tester.widget<TextField>(reviewCode).onSubmitted!;
      final close = tester
          .widget<IconButton>(
            find.byKey(const Key('close-attendance-session-review')),
          )
          .onPressed!;
      await mutate(tester, () async {
        submit(paid.code);
        close();
      });
      expect(find.byType(ReviewPage), findsNothing);
      expect(store.paymentChecks.single.sessionId, upcoming.id);
      expect(store.paymentChecks.single.studentId, paid.id);
      expect(accountSnapshot(), snapshot);
      expectAttendanceFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'review eligibility respects notes lookup canceled class roles and another confirmation',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'مساعد',
          password: 'assistant-password',
          role: StaffRole.assistant,
        );
        await store.saveStaff(
          name: 'استقبال',
          password: 'cashier-password',
          role: StaffRole.cashier,
        );
      });
      await open(tester);
      await showStudent(tester);
      expect(enabled(tester), isTrue);
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      expect(enabled(tester), isFalse);
      await tester.ensureVisible(find.byKey(const Key('cancel-student-note')));
      await tester.tap(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('بحث عن طالب'));
      await tester.pumpAndSettle();
      expect(enabled(tester), isFalse);
      await tester.tap(find.text('التحضير'));
      await tester.pumpAndSettle();
      final review = tester.widget<OutlinedButton>(reviewButton).onPressed!;
      await press(tester, LogicalKeyboardKey.keyL);
      expect(
        find.byKey(const Key('entry-confirmation-dialog')),
        findsOneWidget,
      );
      review();
      await tester.pumpAndSettle();
      expect(find.byType(ReviewPage), findsNothing);
      await press(tester, LogicalKeyboardKey.escape);
      await tester.runAsync(() async {
        store.signOut();
        await store.signIn('مساعد', 'assistant-password');
      });
      await tester.pumpAndSettle();
      expect(enabled(tester), isFalse);
      await tester.runAsync(() async {
        store.signOut();
        await store.signIn('استقبال', 'cashier-password');
      });
      await tester.pumpAndSettle();
      expect(enabled(tester), isTrue);
      await tester.runAsync(() async {
        store.signOut();
        await store.signIn('مدير السنتر', 'local-password-2026');
        await store.cancelSession(upcoming.id);
      });
      await tester.pumpAndSettle();
      expect(enabled(tester), isFalse);
      expect(store.paymentChecks, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'a stale review click while lesson collection is busy cannot open its popup',
    (tester) async {
      await open(tester);
      await showStudent(tester);
      final review = tester.widget<OutlinedButton>(reviewButton).onPressed!;
      final collect = tester
          .widget<FilledButton>(find.byKey(const Key('collect-attend')))
          .onPressed!;
      await mutate(tester, () async {
        collect();
        review();
      });
      expect(find.byType(ReviewPage), findsNothing);
      expect(store.paymentChecks, isEmpty);
      expect(store.attendanceCount(upcoming.id), 1);
      expect(store.payments, hasLength(2));
      expectAttendanceFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );
}
