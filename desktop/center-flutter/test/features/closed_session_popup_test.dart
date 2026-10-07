import '../helpers/notice_helpers.dart';
import 'dart:io';
import 'dart:async';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group, otherGroup;
  late LessonSession closed;
  late Student student;
  final captureKey = GlobalKey();

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      directory = await Directory.systemTemp.createTemp('massar-closed-popup-');
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('مدير السنتر', 'local-password-2026');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
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
      await store.saveStudent(
        Student(
          name: 'أحمد محمد',
          code: '123',
          groupIds: [group.id],
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
      student = store.students.single;
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 1,
          startsAt: DateTime.now().subtract(const Duration(days: 1)),
          createdAt: DateTime.now(),
        ),
      );
      closed = store.sessions.single;
      await store.closeSession(closed.id);
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<LessonSession> addSession(
    WidgetTester tester,
    int number, {
    bool other = false,
  }) async {
    late LessonSession added;
    await tester.runAsync(() async {
      await store.saveSession(
        LessonSession(
          groupId: other ? otherGroup.id : group.id,
          number: number,
          startsAt: DateTime.now().add(Duration(hours: number)),
          createdAt: DateTime.now(),
        ),
      );
      added = store.sessions.last;
    });
    return added;
  }

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
          home: AttendanceWorkspace(store: store, onExit: () {}),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> scan(WidgetTester tester) async {
    await tester.enterText(
      find.byKey(const Key('student-search')),
      student.code,
    );
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
  }

  Future<void> selectClosed(WidgetTester tester) async {
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
        find.text('حصة 1 · ${shortDate(closed.startsAt)} · مغلقة').last,
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> press(WidgetTester tester, LogicalKeyboardKey key) async {
    await tester.runAsync(() => tester.sendKeyEvent(key));
    await tester.pumpAndSettle();
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

  void expectAccountsUnchanged() {
    expect(store.payments, isEmpty);
    expect(store.packages, isEmpty);
    expect(store.cardPayments, isEmpty);
    expect(store.attendances, hasLength(1));
    expect(store.attendances.single.status, AttendanceStatus.absent);
  }

  void expectNoEntryWrites(int auditCount) {
    expectAccountsUnchanged();
    expect(store.audit, hasLength(auditCount));
  }

  Future<void> startReopening(WidgetTester tester) async {
    await tester.tap(find.byKey(const Key('closed-session-reopen')));
    await tester.pumpAndSettle();
    expect(
      find.byKey(const Key('reopen-session-confirmation')),
      findsOneWidget,
    );
    expect(find.text('إعادة فتح هذه الحصة؟'), findsOneWidget);
    expect(
      store.sessions.firstWhere((e) => e.id == closed.id).status,
      SessionStatus.closed,
    );
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
      final target = File('build/verification/$name.png');
      await target.parent.create(recursive: true);
      await target.writeAsBytes(bytes!.buffer.asUint8List());
      image.dispose();
    });
  }

  testWidgets(
    'closed selection warns once; Enter Esc L M and scanner repeats cannot write',
    (tester) async {
      await addSession(tester, 2);
      final auditCount = store.audit.length;
      await open(tester);
      await scan(tester);
      await selectClosed(tester);
      expect(find.byKey(const Key('closed-session-dialog')), findsOneWidget);
      expect(find.text('الحصة مغلقة'), findsOneWidget);
      expect(find.text(store.groupLabel(group.id)), findsWidgets);
      expect(
        find.text('الحصة مغلقة. اختار حصة مفتوحة لاستقبال الطالب.'),
        findsNothing,
      );
      await capture(tester, 'closed-session-popup-light-1280');
      expect(tester.getRect(find.text('إلغاء')).bottom, lessThanOrEqualTo(800));
      await tester.sendKeyDownEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('closed-session-dialog')), findsOneWidget);
      await press(tester, LogicalKeyboardKey.enter);
      expect(find.byKey(const Key('closed-session-dialog')), findsNothing);
      expectCodeFocus(tester);
      for (final key in [
        LogicalKeyboardKey.enter,
        LogicalKeyboardKey.keyL,
        LogicalKeyboardKey.keyM,
      ]) {
        await press(tester, key);
        expect(find.byKey(const Key('closed-session-dialog')), findsOneWidget);
        await press(tester, LogicalKeyboardKey.escape);
        expectCodeFocus(tester);
      }
      // Release a held shortcut only after dismissing the warning: its repeat
      // and key-up must not type a letter or reopen the modal on the code field.
      for (final key in [LogicalKeyboardKey.keyL, LogicalKeyboardKey.keyM]) {
        await tester.runAsync(() => tester.sendKeyDownEvent(key));
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('closed-session-dialog')), findsOneWidget);
        await press(tester, LogicalKeyboardKey.escape);
        await tester.sendKeyRepeatEvent(key);
        await tester.sendKeyUpEvent(key);
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('closed-session-dialog')), findsNothing);
        expect(
          tester
              .widget<TextField>(find.byKey(const Key('student-search')))
              .controller!
              .text,
          isEmpty,
        );
        expectCodeFocus(tester);
      }
      // A scanner's numeric payload resolves the student, never registers entry.
      await scan(tester);
      expect(find.byKey(const Key('closed-session-dialog')), findsNothing);
      await press(tester, LogicalKeyboardKey.enter);
      expect(find.byKey(const Key('closed-session-dialog')), findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.digit1);
      await press(tester, LogicalKeyboardKey.numpadEnter);
      expectCodeFocus(tester);
      expectNoEntryWrites(auditCount);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'lookup remains quiet on closed selection and re-entry can choose view only',
    (tester) async {
      await addSession(tester, 2);
      final auditCount = store.audit.length;
      await open(tester, dark: true);
      await tester.tap(find.text('بحث عن طالب'));
      await tester.pumpAndSettle();
      await selectClosed(tester);
      await scan(tester);
      await press(tester, LogicalKeyboardKey.enter);
      expect(find.byKey(const Key('closed-session-dialog')), findsNothing);
      await tester.runAsync(() => tester.tap(find.text('التحضير')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('closed-session-dialog')), findsOneWidget);
      await tester.runAsync(
        () => tester.tap(find.byKey(const Key('closed-session-view-only'))),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('closed-session-dialog')), findsNothing);
      expect(
        find.text('عرض البيانات والسجل فقط؛ لا يُسجل حضور أو دفع.'),
        findsOneWidget,
      );
      expectCodeFocus(tester);
      await press(tester, LogicalKeyboardKey.enter);
      expect(find.byKey(const Key('closed-session-dialog')), findsNothing);
      expectNoEntryWrites(auditCount);
    },
  );

  testWidgets(
    'open session requires explicit same-group choice with no attendance or payment',
    (tester) async {
      final second = await addSession(tester, 2);
      final third = await addSession(tester, 3);
      final foreign = await addSession(tester, 99, other: true);
      final auditCount = store.audit.length;
      await open(tester, dark: true);
      await scan(tester);
      await selectClosed(tester);
      await tester.tap(find.byKey(const Key('closed-session-choose-open')));
      await tester.pumpAndSettle();
      final choice = tester.widget<DropdownButtonFormField<String>>(
        find.byKey(const Key('closed-session-open-choice')),
      );
      expect(choice.initialValue, isNull);
      final choices = tester
          .widget<DropdownButton<String>>(
            find.descendant(
              of: find.byKey(const Key('closed-session-open-choice')),
              matching: find.byType(DropdownButton<String>),
            ),
          )
          .items!;
      expect(choices.map((item) => item.value), [second.id, third.id]);
      expect(choices.map((item) => item.value), isNot(contains(foreign.id)));
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('closed-session-choose-open')),
            )
            .onPressed,
        isNull,
      );
      await tester.tap(find.byKey(const Key('closed-session-open-choice')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('حصة 3 · ${shortDate(third.startsAt)}').last);
      await tester.pumpAndSettle();
      await tester.runAsync(
        () => tester.tap(find.byKey(const Key('closed-session-choose-open'))),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(ValueKey('session-${third.id}')), findsOneWidget);
      expect(find.byKey(const Key('closed-session-dialog')), findsNothing);
      expectCodeFocus(tester);
      expectNoEntryWrites(auditCount);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'no open session disables selection even when another group has an open class',
    (tester) async {
      await addSession(tester, 99, other: true);
      final auditCount = store.audit.length;
      await open(tester, dark: true);
      await scan(tester);
      await selectClosed(tester);
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('closed-session-choose-open')),
            )
            .onPressed,
        isNull,
      );
      expect(
        find.text(
          'لا توجد حصة مفتوحة لهذه المجموعة. يمكنك إعادة فتح هذه الحصة أو إضافة حصة جديدة.',
        ),
        findsOneWidget,
      );
      await capture(tester, 'closed-session-popup-dark-1280');
      expect(tester.getRect(find.text('إلغاء')).bottom, lessThanOrEqualTo(800));
      await tester.runAsync(() => tester.tap(find.text('إلغاء')));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expectNoEntryWrites(auditCount);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'reopen cancellation and scanner text leave the same class closed with code focus',
    (tester) async {
      final auditCount = store.audit.length;
      await open(tester);
      await scan(tester);
      await selectClosed(tester);
      await startReopening(tester);
      expect(
        find.text('التقفيلة السابقة ستبقى في السجل وتحتاج تقفيلة جديدة.'),
        findsNothing,
      );
      await capture(tester, 'reopen-session-confirmation-light-1280');
      await press(tester, LogicalKeyboardKey.escape);
      expectCodeFocus(tester);
      expectNoEntryWrites(auditCount);
      for (final key in [
        LogicalKeyboardKey.digit1,
        LogicalKeyboardKey.keyM,
        LogicalKeyboardKey.keyL,
      ]) {
        await press(tester, LogicalKeyboardKey.enter);
        await startReopening(tester);
        await tester.sendKeyEvent(key);
        await press(tester, LogicalKeyboardKey.enter);
        expect(
          find.byKey(const Key('reopen-session-confirmation')),
          findsOneWidget,
        );
        expect(
          tester
              .widget<FilledButton>(
                find.byKey(const Key('confirm-reopen-session')),
              )
              .onPressed,
          isNull,
        );
        expect(
          store.sessions.firstWhere((e) => e.id == closed.id).status,
          SessionStatus.closed,
        );
        await press(tester, LogicalKeyboardKey.escape);
        expectCodeFocus(tester);
        expectNoEntryWrites(auditCount);
      }
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'cashier Enter reopens the exact class once, archives closing and preserves durable evidence',
    (tester) async {
      await tester.runAsync(() async {
        await store.finalizeSession(
          sessionId: closed.id,
          actualCash: 0,
          notes: 'التقفيلة الأصلية',
        );
        await store.saveStaff(
          name: 'الاستقبال',
          password: 'cashier-password',
          role: StaffRole.cashier,
        );
        store.signOut();
        await store.signIn('الاستقبال', 'cashier-password');
      });
      final oldClosing = store.allClosings.single;
      final oldAttendance = store.allAttendances.single;
      await open(tester, dark: true);
      await scan(tester);
      await selectClosed(tester);
      await startReopening(tester);
      expect(
        find.text('التقفيلة السابقة ستبقى في السجل وتحتاج تقفيلة جديدة.'),
        findsOneWidget,
      );
      await capture(tester, 'reopen-session-confirmation-dark-1280');
      await mutateThroughUi(
        tester,
        () => tester.sendKeyDownEvent(LogicalKeyboardKey.enter),
      );
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(find.byKey(ValueKey('session-${closed.id}')), findsOneWidget);
      expect(
        store.sessions.firstWhere((e) => e.id == closed.id).status,
        SessionStatus.open,
      );
      expect(store.closings, isEmpty);
      expect(store.allClosings.single.toJson(), oldClosing.toJson());
      expect(store.allAttendances.single.toJson(), oldAttendance.toJson());
      expectAccountsUnchanged();
      expect(find.textContaining('الحصة اتفتحت.'), findsNothing);
      expectCodeFocus(tester);
      await tester.runAsync(() async {
        await store.close();
        store = await CenterStore.open(directory: directory.path);
      });
      expect(
        store.sessions.firstWhere((e) => e.id == closed.id).status,
        SessionStatus.open,
      );
      expect(store.closings, isEmpty);
      expect(store.allClosings.single.toJson(), oldClosing.toJson());
      expect(store.allAttendances.single.toJson(), oldAttendance.toJson());
      expectAccountsUnchanged();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'admin double mouse confirmation opens once without creating attendance or payment',
    (tester) async {
      final auditCount = store.audit.length;
      await open(tester);
      await scan(tester);
      await selectClosed(tester);
      await startReopening(tester);
      final confirm = tester
          .widget<FilledButton>(find.byKey(const Key('confirm-reopen-session')))
          .onPressed!;
      await mutateThroughUi(tester, () async {
        confirm();
        confirm();
      });
      expect(
        store.sessions.firstWhere((e) => e.id == closed.id).status,
        SessionStatus.open,
      );
      expect(store.audit.length, auditCount + 1);
      expect(store.allClosings, isEmpty);
      expectAccountsUnchanged();
      expectCodeFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'assistant has no reopen action and a stale privileged confirmation cannot bypass role checks',
    (tester) async {
      await tester.runAsync(
        () => store.saveStaff(
          name: 'مساعد',
          password: 'assistant-password',
          role: StaffRole.assistant,
        ),
      );
      await open(tester);
      await scan(tester);
      await selectClosed(tester);
      await startReopening(tester);
      await tester.runAsync(() async {
        store.signOut();
        await store.signIn('مساعد', 'assistant-password');
      });
      final auditCount = store.audit.length;
      await tester.runAsync(() async {
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await acknowledgeNotice(
          tester,
          message: 'ليس لديك صلاحية لهذا الإجراء.',
        );
      });
      expect(find.text('ليس لديك صلاحية لهذا الإجراء.'), findsNothing);
      expect(find.textContaining('الحصة اتفتحت.'), findsNothing);
      expect(
        store.sessions.firstWhere((e) => e.id == closed.id).status,
        SessionStatus.closed,
      );
      expectNoEntryWrites(auditCount);
      expectCodeFocus(tester);
      await press(tester, LogicalKeyboardKey.enter);
      expect(find.byKey(const Key('closed-session-dialog')), findsOneWidget);
      expect(find.byKey(const Key('closed-session-reopen')), findsNothing);
      await press(tester, LogicalKeyboardKey.escape);
      expectCodeFocus(tester);
      expectNoEntryWrites(auditCount);
    },
  );
}
