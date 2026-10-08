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
import 'package:massar_center/features/attendance/attendance_conflict_dialog.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';
import '../helpers/attendance_ui_helpers.dart';
import '../helpers/ui_wait_helpers.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late Student student;
  late LessonSession original, target;
  late AttendanceRecord originalAttendance;
  final boundary = GlobalKey();
  final code = find.byKey(const Key('student-search'));
  final crossDialog = find.byKey(const Key('attendance-conflict-dialog'));
  final proceed = find.byKey(const Key('attendance-conflict-continue'));

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      directory = await Directory.systemTemp.createTemp('massar-duplicate-ui-');
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('الإدارة', 'duplicate-ui-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(kind: kind, name: kind.name));
      }
      for (final name in ['المجموعة السابقة', 'المجموعة الحالية']) {
        await store.saveGroup(
          StudyGroup(
            name: name,
            subjectId: store.catalogs
                .firstWhere((row) => row.kind == CatalogKind.subject)
                .id,
            centerId: store.catalogs
                .firstWhere((row) => row.kind == CatalogKind.center)
                .id,
            gradeId: store.catalogs
                .firstWhere((row) => row.kind == CatalogKind.grade)
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
      }
      await seedAttendanceMonths(store);
      await store.saveStudent(
        Student(
          code: '701',
          name: 'مينا صاحب الحضور',
          groupIds: store.groups.map((group) => group.id).toList(),
          discountPercent: 25,
          createdAt: DateTime.now().subtract(const Duration(days: 20)),
        ),
      );
      student = store.students.single;
      for (var index = 0; index < 2; index++) {
        await store.saveSession(
          LessonSession(
            groupId: store.groups[index].id,
            number: 7,
            startsAt: DateTime.now().add(Duration(days: index == 0 ? -5 : 2)),
            createdAt: DateTime.now(),
          ),
        );
      }
      for (final session in store.sessions) {
        await store.startSession(session.id);
      }
      original = store.sessions.first;
      target = store.sessions.last;
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: original.id,
          mode: EntryMode.single,
        ),
      );
      originalAttendance = store.attendances.single;
      await store.closeSession(original.id);
    }),
  );
  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> settleRoutes(WidgetTester tester) async {
    Future<void> frame() async {
      await tester.pump();
      if (find.byType(LinearProgressIndicator).evaluate().isEmpty) {
        await tester.pumpAndSettle();
      } else {
        // A submission remains busy while its warning waits for acknowledgement.
        await tester.pump(const Duration(milliseconds: 400));
      }
    }

    await frame();
    await tester.runAsync(
      () => Future<void>.delayed(const Duration(milliseconds: 20)),
    );
    await frame();
  }

  Future<void> open(
    WidgetTester tester, {
    bool dark = false,
    double width = 1280,
    double height = 800,
    double scale = 1,
  }) async {
    await tester.binding.setSurfaceSize(
      Size(width, height < 600 ? 900 : height),
    );
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
        key: boundary,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          builder: (context, child) => MediaQuery(
            data: MediaQuery.of(
              context,
            ).copyWith(textScaler: TextScaler.linear(scale)),
            child: child!,
          ),
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: AttendanceWorkspace(
              store: store,
              onExit: () {},
              initialSessionId: target.id,
            ),
          ),
        ),
      ),
    );
    await settleRoutes(tester);
    await previewAttendanceStudent(tester, student.code);
    if (height < 600) {
      await tester.binding.setSurfaceSize(Size(width, height));
    }
    await settleRoutes(tester);
    expect(
      crossDialog,
      findsNothing,
      reason: 'Opening the student alone is not a registration attempt.',
    );
    expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    await tester.runAsync(() async {
      final image =
          await (boundary.currentContext!.findRenderObject()
                  as RenderRepaintBoundary)
              .toImage();
      final png = await image.toByteData(format: ui.ImageByteFormat.png);
      final file = File('build/verification/$name.png');
      await file.parent.create(recursive: true);
      await file.writeAsBytes(png!.buffer.asUint8List());
      image.dispose();
    });
  }

  Future<void> mutate(
    WidgetTester tester,
    Future<void> Function() gesture,
  ) async {
    final saved = Completer<void>();
    void changed() {
      if (!saved.isCompleted) saved.complete();
    }

    store.addListener(changed);
    try {
      await tester.runAsync(gesture);
      await tester.pump(const Duration(milliseconds: 400));
      await tester.runAsync(() async {
        for (var attempt = 0; attempt < 100 && !saved.isCompleted; attempt++) {
          await Future<void>.delayed(const Duration(milliseconds: 20));
          await tester.pump(const Duration(milliseconds: 50));
        }
        expect(
          saved.isCompleted,
          isTrue,
          reason: 'The real SQLite mutation must complete.',
        );
        await waitForUiCondition(
          tester,
          () =>
              crossDialog.evaluate().isEmpty &&
              find
                  .byKey(const Key('entry-confirmation-dialog'))
                  .evaluate()
                  .isEmpty,
          reason: 'Accepted attendance/payment completes',
        );
      });
      await settleRoutes(tester);
    } finally {
      store.removeListener(changed);
    }
  }

  void expectCodeFocus(WidgetTester tester) {
    expect(tester.widget<TextField>(code).focusNode!.hasFocus, isTrue);
    expect(tester.takeException(), isNull);
  }

  Future<void> seedTarget() => store.collectAndAttend(
    EntryRequest(
      studentId: student.id,
      sessionId: target.id,
      mode: EntryMode.single,
      acknowledgedAttendanceIds: [originalAttendance.id],
    ),
  );

  testWidgets(
    'exact session warning contains previous time, swallows held attempts, and lookup remains passive',
    (tester) async {
      await tester.runAsync(seedTarget);
      final previous = store.attendances.singleWhere(
        (record) => record.sessionId == target.id,
      );
      await open(tester);
      await tester.runAsync(() async {
        await tester.sendKeyDownEvent(LogicalKeyboardKey.keyL);
        await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyL);
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyEvent(LogicalKeyboardKey.keyN);
        await tester.sendKeyEvent(LogicalKeyboardKey.f4);
        await tester.sendKeyEvent(LogicalKeyboardKey.f6);
      });
      await settleRoutes(tester);
      final notice = find.byKey(const Key('massar-notice-dialog'));
      expect(notice, findsOneWidget);
      expect(find.text('الدفع مسجل بالفعل'), findsOneWidget);
      final message = tester
          .widget<Text>(
            find
                .descendant(
                  of: find.byKey(const Key('massar-notice-message')),
                  matching: find.byType(Text),
                )
                .first,
          )
          .data!;
      expect(message, contains('مينا صاحب الحضور · كود 701'));
      expect(message, contains('المجموعة الحالية'));
      expect(message, contains('حصة 7'));
      expect(message, contains(attendanceRecordedAtLabel(previous.recordedAt)));
      await capture(tester, 'duplicate-exact-light1280');
      await tester.runAsync(() => acknowledgeNotice(tester));
      await settleRoutes(tester);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.keyL);
      await settleRoutes(tester);
      expect(notice, findsNothing);
      expect(tester.widget<TextField>(code).controller!.text, isEmpty);
      expect(store.payments, hasLength(2));
      expect(store.attendances, hasLength(2));
      expect(store.packages, isEmpty);
      expectCodeFocus(tester);
      await tester.tap(find.text('بحث عن طالب'));
      await settleRoutes(tester);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyN);
      await settleRoutes(tester);
      expect(notice, findsNothing);
      expect(crossDialog, findsNothing);
      expect(store.payments, hasLength(2));
    },
  );

  testWidgets(
    'registered warning button and second Enter never repeat an exact-session charge',
    (tester) async {
      await tester.runAsync(seedTarget);
      await open(tester, dark: true);
      final button = find.byKey(const Key('duplicate-attendance-alert'));
      await tester.ensureVisible(button);
      await tester.runAsync(() => tester.tap(button));
      await settleRoutes(tester);
      await capture(tester, 'duplicate-exact-dark1280');
      await tester.runAsync(() => acknowledgeNotice(tester));
      await settleRoutes(tester);
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      await settleRoutes(tester);
      expect(find.byKey(const Key('massar-notice-dialog')), findsOneWidget);
      await tester.runAsync(() => acknowledgeNotice(tester));
      await settleRoutes(tester);
      expect(store.payments, hasLength(2));
      expect(store.attendances, hasLength(2));
      expectCodeFocus(tester);
    },
  );

  testWidgets(
    'cross-group direct attempt defaults to cancellation and explicit continuation registers once',
    (tester) async {
      await open(tester, dark: true);
      final button = find.byKey(const Key('collect-attend'));
      await tester.runAsync(() => tester.tap(button));
      await settleRoutes(tester);
      expect(crossDialog, findsOneWidget);
      expect(find.text('سبق حضور نفس الحصة'), findsOneWidget);
      expect(find.textContaining('المجموعة السابقة'), findsWidgets);
      expect(find.textContaining('المجموعة الحالية'), findsWidgets);
      await capture(tester, 'duplicate-cross-group-dark1280');
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      await settleRoutes(tester);
      expect(crossDialog, findsNothing);
      expect(store.payments, hasLength(1));
      expect(store.attendances, hasLength(1));
      expectCodeFocus(tester);
      await tester.runAsync(() => tester.tap(button));
      await settleRoutes(tester);
      await mutate(tester, () => tester.tap(proceed));
      expect(store.payments, hasLength(2));
      expect(store.payments.last.netAmount, 7500);
      expect(store.attendances, hasLength(2));
      expect(
        store.audit.where(
          (event) => event.description.contains(originalAttendance.id),
        ),
        isNotEmpty,
      );
      expectCodeFocus(tester);
    },
  );

  for (final key in [LogicalKeyboardKey.keyL, LogicalKeyboardKey.keyN]) {
    testWidgets(
      '${key.keyLabel} review threads frozen acknowledged IDs through shortcut confirmation and package repricing',
      (tester) async {
        await open(tester);
        if (key == LogicalKeyboardKey.keyN) {
          await selectAttendanceMonth(tester, 'شهر حصتين · 2 حصص');
        }
        await tester.runAsync(() => requestAttendanceConfirmation(tester, key));
        await settleRoutes(tester);
        expect(crossDialog, findsOneWidget);
        await tester.runAsync(() => tester.tap(proceed));
        await settleRoutes(tester);
        expect(
          find.byKey(const Key('entry-confirmation-dialog')),
          findsOneWidget,
        );
        if (key == LogicalKeyboardKey.keyN) {
          await selectAttendanceMonth(
            tester,
            'شهر ثلاث حصص · 3 حصص',
            confirmation: true,
          );
        }
        expect(store.payments, hasLength(1));
        await mutate(
          tester,
          () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
        );
        expect(store.payments, hasLength(2));
        expect(
          store.payments.last.netAmount,
          key == LogicalKeyboardKey.keyN ? 20250 : 7500,
        );
        expect(store.attendances, hasLength(2));
        if (key == LogicalKeyboardKey.keyN) {
          expect(store.packages.single.totalSessions, 3);
          expect(store.packages.single.remaining, 2);
        }
        expect(
          store.audit.where(
            (event) => event.description.contains(originalAttendance.id),
          ),
          isNotEmpty,
        );
        expectCodeFocus(tester);
      },
    );
  }

  testWidgets(
    'numeric scanner input cannot approve compact cross-class warning or leak into payment shortcuts',
    (tester) async {
      await open(tester, width: 960, height: 400, scale: 2);
      await tester.runAsync(() async {
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyEvent(LogicalKeyboardKey.digit7);
        await tester.sendKeyEvent(LogicalKeyboardKey.digit0);
        await tester.sendKeyEvent(LogicalKeyboardKey.keyN);
        await tester.sendKeyEvent(LogicalKeyboardKey.f4);
        await tester.sendKeyEvent(LogicalKeyboardKey.f6);
      });
      await settleRoutes(tester);
      expect(crossDialog, findsOneWidget);
      expect(tester.widget<FilledButton>(proceed).onPressed, isNull);
      await tester.ensureVisible(proceed);
      await settleRoutes(tester);
      expect(tester.getRect(proceed).bottom, lessThanOrEqualTo(400));
      await capture(tester, 'duplicate-cross-group-light960x400');
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      await settleRoutes(tester);
      expect(crossDialog, findsNothing);
      expect(store.payments, hasLength(1));
      expect(store.attendances, hasLength(1));
      expect(find.byKey(const Key('student-editor-dialog')), findsNothing);
      expectCodeFocus(tester);
    },
  );

  testWidgets('explicit reviewed quote refuses changed price without charging', (
    tester,
  ) async {
    await open(tester);
    await tester.runAsync(
      () => requestAttendanceConfirmation(tester, LogicalKeyboardKey.keyL),
    );
    await settleRoutes(tester);
    expect(crossDialog, findsOneWidget);
    await tester.runAsync(() => tester.tap(proceed));
    await settleRoutes(tester);
    expect(find.byKey(const Key('entry-confirmation-dialog')), findsOneWidget);
    await tester.runAsync(
      () => store.saveGroup(
        store.groups
            .singleWhere((group) => group.id == target.groupId)
            .copyWith(sessionPrice: 15000),
      ),
    );
    await tester.runAsync(() => tester.sendKeyEvent(LogicalKeyboardKey.enter));
    await settleRoutes(tester);
    await tester.runAsync(
      () => acknowledgeNotice(
        tester,
        message:
            'الحضور محفوظ، لكن تسديد الحصة لم يتم. تغيّر السعر أو الخصم أو رصيد الطالب؛ راجع الدفع وأكّد مرة أخرى.',
      ),
    );
    await settleRoutes(tester);
    expect(store.payments, hasLength(1));
    expect(store.attendances, hasLength(2));
    expect(store.attendanceNeedsPayment(student.id, target.id), isTrue);
    expect(store.packages, isEmpty);
    expectCodeFocus(tester);
  });

  testWidgets(
    'changed conflict set is rejected after frozen review and unpaid attendance repayment remains available',
    (tester) async {
      await open(tester);
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      await settleRoutes(tester);
      await tester.runAsync(() async {
        final firstGroup = store.groups.first;
        await store.saveGroup(
          firstGroup.copyWith(id: '', name: 'المجموعة الثالثة'),
        );
        final thirdGroup = store.groups.last;
        await store.saveStudent(
          student.copyWith(groupIds: [...student.groupIds, thirdGroup.id]),
        );
        await store.saveSession(
          LessonSession(
            groupId: thirdGroup.id,
            number: 7,
            startsAt: DateTime.now(),
            createdAt: DateTime.now(),
          ),
        );
        final thirdSession = store.sessions.last;
        await store.startSession(thirdSession.id);
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: thirdSession.id,
            mode: EntryMode.single,
            acknowledgedAttendanceIds: [originalAttendance.id],
          ),
        );
      });
      await settleRoutes(tester);
      await tester.runAsync(() => tester.tap(proceed));
      await tester.pump(const Duration(milliseconds: 400));
      await tester.runAsync(() => acknowledgeNotice(tester));
      await settleRoutes(tester);
      expect(
        store.attendances.any((record) => record.sessionId == target.id),
        isFalse,
      );
      expect(store.payments, hasLength(2));
      expectCodeFocus(tester);
      await tester.runAsync(() async {
        final reviewed = store
            .attendanceConflictsFor(student.id, target.id)
            .map((record) => record.id)
            .toList();
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: target.id,
            mode: EntryMode.single,
            acknowledgedAttendanceIds: reviewed,
          ),
        );
        final targetPayment = store.payments.singleWhere(
          (record) => record.sessionId == target.id,
        );
        await store.cancelPayment(
          paymentId: targetPayment.id,
          reason: 'إلغاء الدفع فقط',
        );
      });
      await settleRoutes(tester);
      expect(store.attendanceNeedsPayment(student.id, target.id), isTrue);
      final button = find.byKey(const Key('collect-attend'));
      await tester.ensureVisible(button);
      await mutate(tester, () => tester.tap(button));
      expect(crossDialog, findsNothing);
      expect(
        store.attendances.where((record) => record.sessionId == target.id),
        hasLength(1),
      );
      expect(
        store.payments.where((record) => record.sessionId == target.id),
        hasLength(1),
      );
      expectCodeFocus(tester);
    },
  );

  testWidgets(
    'already-recorded makeup is identified without a second debit or charge',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveSession(target.copyWith(number: 8));
        target = store.sessions.singleWhere(
          (session) => session.id == target.id,
        );
        await store.saveSession(
          LessonSession(
            groupId: original.groupId,
            number: 8,
            startsAt: DateTime.now(),
            createdAt: DateTime.now(),
          ),
        );
        final missedSession = store.sessions.last;
        await store.startSession(missedSession.id);
        await store.renewPackage(
          PackageRequest(
            studentId: student.id,
            groupId: original.groupId,
            sessionId: missedSession.id,
          ),
        );
        await store.closeSession(missedSession.id);
        final absence = store.attendances.singleWhere(
          (record) => record.sessionId == missedSession.id,
        );
        await store.collectAndAttend(
          EntryRequest(
            studentId: student.id,
            sessionId: target.id,
            mode: EntryMode.makeup,
            originalAttendanceId: absence.id,
          ),
        );
      });
      final paymentIds = store.payments.map((record) => record.id).toSet();
      final remaining = store.packages.single.remaining;
      await open(tester);
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      await settleRoutes(tester);
      expect(find.textContaining('الحضور مسجل كتعويض.'), findsOneWidget);
      await tester.runAsync(() => acknowledgeNotice(tester));
      await settleRoutes(tester);
      expect(store.payments.map((record) => record.id).toSet(), paymentIds);
      expect(store.packages.single.remaining, remaining);
      expect(
        store.attendances.where((record) => record.sessionId == target.id),
        hasLength(1),
      );
      expectCodeFocus(tester);
    },
  );
}
