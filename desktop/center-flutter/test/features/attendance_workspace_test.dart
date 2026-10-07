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
import 'package:massar_center/features/management/closings_page.dart';
import 'package:massar_center/shared/theme.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/notice_dialog.dart';
import '../helpers/notice_helpers.dart';

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
    Future<void> Function() gesture, {
    String? noticeMessage,
  }) async {
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
        expect(
          find.descendant(
            of: find.byKey(const Key('massar-notice-dialog')),
            matching: find.text('تم بنجاح'),
          ),
          findsOneWidget,
        );
        await acknowledgeNotice(tester, message: noticeMessage);
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

  for (final confirm in [false, true]) {
    testWidgets(
      'L payment ${confirm ? 'with Shift requests confirmation' : 'without Shift collects directly'}',
      (tester) async {
        await tester.binding.setSurfaceSize(const Size(1440, 900));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final lesson = store.sessions.single;
        await tester.runAsync(() => store.startSession(lesson.id));
        final draft = AttendanceWorkspaceContext()
          ..groupId = lesson.groupId
          ..sessionId = lesson.id
          ..studentId = student.id
          ..studentResolved = true;
        await tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.light,
            home: Directionality(
              textDirection: TextDirection.rtl,
              child: AttendanceWorkspace(
                store: store,
                onExit: () {},
                initialSessionId: lesson.id,
                workspaceContext: draft,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        if (confirm) {
          await tester.sendKeyDownEvent(LogicalKeyboardKey.shiftLeft);
        }
        await tester.runAsync(() async {
          await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
          for (var attempt = 0; attempt < 100; attempt++) {
            await tester.pump(const Duration(milliseconds: 20));
            if (confirm
                ? find.byType(Dialog).evaluate().isNotEmpty
                : store.payments.isNotEmpty) {
              break;
            }
            await Future<void>.delayed(const Duration(milliseconds: 20));
          }
        });
        if (confirm) {
          expect(find.byType(Dialog), findsOneWidget);
          expect(store.payments, isEmpty);
          await tester.sendKeyUpEvent(LogicalKeyboardKey.shiftLeft);
        } else {
          expect(find.byType(Dialog), findsNothing);
          expect(store.payments, hasLength(1));
          expect(store.attendances, hasLength(1));
          expect(
            store.payments.single.collectedAmount,
            store.payments.single.netAmount,
          );
        }
        await tester.pumpWidget(const SizedBox());
        await tester.pumpAndSettle();
      },
    );
  }

  for (final paid in [200, 190]) {
    testWidgets(
      'month payment $paid keeps confirmation without Shift and records remaining debt',
      (tester) async {
        await tester.binding.setSurfaceSize(const Size(1440, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        late LessonSession lesson;
        await tester.runAsync(() async {
          await store.saveStudent(student.copyWith(discountPercent: 0));
          final month = await store.saveStudyMonth(
            StudyMonth(
              name: 'شهر الدفع',
              price: 21000,
              lessons: List.generate(
                4,
                (index) => PreparedLesson(number: index + 1),
              ),
            ),
          );
          lesson = await store.startPreparedLesson(
            groupId: group.id,
            preparedLessonId: month.lessons.first.id,
          );
        });
        final draft = AttendanceWorkspaceContext()
          ..groupId = lesson.groupId
          ..sessionId = lesson.id
          ..studentId = student.id
          ..studentResolved = true;
        await tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.light,
            home: Directionality(
              textDirection: TextDirection.rtl,
              child: AttendanceWorkspace(
                store: store,
                onExit: () {},
                initialSessionId: lesson.id,
                workspaceContext: draft,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.sendKeyEvent(LogicalKeyboardKey.keyN);
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('entry-confirmation-dialog')),
          findsOneWidget,
        );
        expect(store.payments, isEmpty);
        await tester.tap(find.byKey(const Key('custom-paid-amount')));
        await tester.pumpAndSettle();
        await tester.enterText(find.byKey(const Key('paid-amount')), '$paid');
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('payment-debt-preview')), findsOneWidget);
        await tester.sendKeyEvent(LogicalKeyboardKey.escape);
        await tester.pumpAndSettle();
        expect(store.payments, isEmpty);
        await tester.pumpWidget(const SizedBox());
        await tester.pumpAndSettle();
        await tester.runAsync(() async {
          await store.collectAndAttend(
            EntryRequest(
              studentId: student.id,
              sessionId: lesson.id,
              mode: EntryMode.package,
              monthPlanId: store.groups.single.monthPlans
                  .firstWhere((p) => p.name == 'شهر الدفع')
                  .id,
              packageSessions: 4,
              paidAmount: paid * 100,
            ),
          );
        });
        expect(store.payments, hasLength(1));
        expect(store.payments.single.netAmount, 21000);
        expect(store.payments.single.collectedAmount, paid * 100);
        expect(
          store.paymentDebtFor(store.payments.single.id),
          (210 - paid) * 100,
        );
        expect(store.attendances, hasLength(1));
        expect(tester.takeException(), isNull);
      },
    );
  }

  for (final useButton in [false, true]) {
    testWidgets(
      'free search protects payment keys after ${useButton ? 'search button' : 'double tap'}',
      (tester) async {
        await tester.binding.setSurfaceSize(const Size(1440, 900));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final lesson = store.sessions.single;
        await tester.runAsync(() => store.startSession(lesson.id));
        final draft = AttendanceWorkspaceContext()
          ..groupId = lesson.groupId
          ..sessionId = lesson.id
          ..studentId = student.id
          ..studentResolved = true;
        await tester.pumpWidget(
          MaterialApp(
            theme: MassarTheme.light,
            home: Directionality(
              textDirection: TextDirection.rtl,
              child: AttendanceWorkspace(
                store: store,
                onExit: () {},
                initialSessionId: lesson.id,
                workspaceContext: draft,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        final field = find.byKey(const Key('student-search'));
        if (useButton) {
          await tester.tap(find.byKey(const Key('enable-free-search')));
        } else {
          await tester.tap(field);
          await tester.pump(const Duration(milliseconds: 80));
          await tester.tap(field);
        }
        await tester.pumpAndSettle();
        expect(
          find.text('بحث حر · اكتب الاسم أو الكود أو الهاتف ثم Enter'),
          findsOneWidget,
        );
        for (final key in [
          LogicalKeyboardKey.keyL,
          LogicalKeyboardKey.keyN,
          LogicalKeyboardKey.keyC,
          LogicalKeyboardKey.keyS,
        ]) {
          await tester.sendKeyEvent(key);
          await tester.pumpAndSettle();
          expect(find.byType(Dialog), findsNothing);
        }
        await tester.enterText(field, 'laila');
        await tester.pump(const Duration(milliseconds: 150));
        expect(tester.widget<TextField>(field).controller!.text, 'laila');
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.centerFees, isEmpty);
        await tester.pumpWidget(const SizedBox());
      },
    );
  }

  testWidgets(
    'attendance search Enter resolves immediately before delayed refresh',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1440, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final lesson = store.sessions.single;
      await tester.runAsync(() => store.startSession(lesson.id));
      final draft = AttendanceWorkspaceContext()
        ..groupId = lesson.groupId
        ..sessionId = lesson.id
        ..lookupOnly = true;
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: AttendanceWorkspace(
              store: store,
              onExit: () {},
              initialSessionId: lesson.id,
              workspaceContext: draft,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      final field = find.byKey(const Key('student-search'));
      final student = store.students.first;
      await tester.enterText(field, student.code);
      await tester.pump(const Duration(milliseconds: 20));
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pumpAndSettle();
      expect(tester.widget<TextField>(field).controller!.text, isEmpty);
      expect(find.textContaining(student.name), findsWidgets);
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      await tester.enterText(field, 'pending');
      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 150));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'spotlight chooses a shared-phone student then Enter records presence without payment',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveStudent(
          Student(
            name: 'طالب آخر',
            code: '999',
            phone: student.phone,
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
        await store.startSession(store.sessions.single.id);
      });
      await tester.binding.setSurfaceSize(const Size(1440, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(
        MaterialApp(
          theme: MassarTheme.light,
          home: AttendanceWorkspace(
            store: store,
            onExit: () {},
            initialSessionId: store.sessions.single.id,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('open-student-spotlight')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('student-spotlight-query')),
        student.phone,
      );
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('student-spotlight-query')), findsOneWidget);
      expect(store.attendances, isEmpty);
      await tester.tap(find.byKey(ValueKey('spotlight-result-${student.id}')));
      await tester.pumpAndSettle();
      expect(find.text('كود الطالب: ${student.code}'), findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 400));
      for (var i = 0; i < 100; i++) {
        await tester.runAsync(
          () => Future<void>.delayed(const Duration(milliseconds: 20)),
        );
        await tester.pump(const Duration(milliseconds: 20));
        if (store.attendances.isNotEmpty &&
            find.byType(LinearProgressIndicator).evaluate().isEmpty) {
          break;
        }
      }
      expect(store.attendances.single.studentId, student.id);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
      // UI writes originate in the widget clock's zone. Keep pumping that
      // clock while the real SQLite connection drains and closes.
      var closed = false;
      final closing = store.close().then((_) => closed = true);
      for (var i = 0; i < 100 && !closed; i++) {
        await tester.runAsync(
          () => Future<void>.delayed(const Duration(milliseconds: 20)),
        );
        await tester.pump(const Duration(milliseconds: 20));
      }
      expect(closed, isTrue);
      await closing;
    },
  );

  for (final enterKey in [
    LogicalKeyboardKey.enter,
    LogicalKeyboardKey.numpadEnter,
  ]) {
    testWidgets(
      'two physical ${enterKey.keyLabel} presses show the student then collect once; held Enter only shows',
      (tester) async {
        await openWorkspace(tester);
        await tester.enterText(
          find.byKey(const Key('student-search')),
          student.code,
        );
        await tester.sendKeyDownEvent(enterKey);
        await tester.sendKeyRepeatEvent(enterKey);
        await tester.sendKeyRepeatEvent(enterKey);
        await tester.sendKeyUpEvent(enterKey);
        await tester.pumpAndSettle();
        expect(find.text('سجل ${student.name}'), findsOneWidget);
        expect(store.attendances, isEmpty);
        expect(store.payments, isEmpty);
        expectCodeFocus(tester);
        expect(find.textContaining('دفع ٧٥'), findsOneWidget);
        await mutateThroughUi(tester, () async {
          await tester.sendKeyDownEvent(enterKey);
          await tester.sendKeyRepeatEvent(enterKey);
          await tester.sendKeyUpEvent(enterKey);
        });
        expect(store.attendances, hasLength(1));
        expect(store.payments.single.netAmount, 7500);
        await tester.sendKeyEvent(enterKey);
        await tester.testTextInput.receiveAction(TextInputAction.done);
        await tester.pumpAndSettle();
        expect(store.payments, hasLength(1));
        expect(store.attendances, hasLength(1));
        expectCodeFocus(tester);
      },
    );
  }

  for (final entryCase in ['prepaid', 'free', 'extra']) {
    testWidgets(
      'second submitted Enter registers $entryCase using its actual existing account rules',
      (tester) async {
        await tester.runAsync(() async {
          if (entryCase == 'prepaid') {
            await store.renewPackage(
              PackageRequest(
                studentId: student.id,
                groupId: group.id,
                sessionId: store.sessions.single.id,
              ),
            );
          } else {
            await store.saveSession(
              store.sessions.single.copyWith(
                kind: entryCase == 'free'
                    ? SessionKind.free
                    : SessionKind.extra,
                extraPrice: entryCase == 'extra' ? 8000 : 0,
              ),
            );
          }
        });
        final initialPayments = store.payments.length;
        await openWorkspace(tester);
        await scan(tester, student.name);
        expect(store.attendances, isEmpty);
        expect(store.payments, hasLength(initialPayments));
        await mutateThroughUi(
          tester,
          () => tester.testTextInput.receiveAction(TextInputAction.done),
        );
        expect(store.attendances, hasLength(1));
        expect(
          store.payments,
          hasLength(initialPayments + (entryCase == 'extra' ? 1 : 0)),
        );
        if (entryCase == 'prepaid') {
          expect(store.remainingFor(student.id, group.id), 3);
          expect(store.attendances.single.packageId, isNotNull);
        } else if (entryCase == 'extra') {
          expect(store.payments.single.netAmount, 6000);
          expect(store.attendances.single.packageId, isNull);
        } else {
          expect(store.payments, isEmpty);
        }
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await tester.pumpAndSettle();
        expect(store.attendances, hasLength(1));
        expectCodeFocus(tester);
      },
    );
  }

  testWidgets(
    'lookup choice disambiguates names and shows notes and grades without attendance, renewal or closing',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveStudent(
          Student(
            name: 'أحمد عادل',
            code: 'MS-SECOND',
            groupIds: [group.id],
            createdAt: student.createdAt,
          ),
        );
        await store.saveStudentNote(
          studentId: student.id,
          notes: 'ملاحظة الطالب للبحث',
        );
        await store.saveAcademic(
          AcademicRecord(
            studentId: student.id,
            sessionId: store.sessions.single.id,
            homework: HomeworkStatus.complete,
            score: 7,
            maxScore: 10,
            examAbsent: false,
            updatedAt: DateTime.now(),
          ),
        );
        await store.renewPackage(
          PackageRequest(
            studentId: student.id,
            groupId: group.id,
            sessionId: store.sessions.single.id,
          ),
        );
      });
      final originalPayments = store.payments.length;
      await openWorkspace(tester, dark: true);
      await tester.tap(find.text('بحث عن طالب'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      await scan(tester, 'أحمد');
      expect(store.attendances, isEmpty);
      expect(store.payments, hasLength(originalPayments));
      await tester.runAsync(
        () =>
            acknowledgeNotice(tester, message: 'اختار الطالب من نتائج البحث.'),
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(find.text('اختار الطالب من نتائج البحث.'), findsOneWidget);
      expect(store.attendances, isEmpty);
      await tester.runAsync(
        () =>
            acknowledgeNotice(tester, message: 'اختار الطالب من نتائج البحث.'),
      );
      await tester.tap(find.widgetWithText(ListTile, student.name));
      await tester.pumpAndSettle();
      expect(find.text('ملاحظة الطالب للبحث'), findsOneWidget);
      expect(find.text('7 / 10'), findsWidgets);
      expect(find.text('كامل'), findsWidgets);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyEvent(LogicalKeyboardKey.numpadEnter);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-package')))
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<OutlinedButton>(
              find.widgetWithText(OutlinedButton, 'إنهاء الحصة'),
            )
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<OutlinedButton>(
              find.ancestor(
                of: find.textContaining('تحصيل باقة ٤ حصص جديدة'),
                matching: find.byType(OutlinedButton),
              ),
            )
            .onPressed,
        isNull,
      );
      expect(store.payments, hasLength(originalPayments));
      expect(store.attendances, isEmpty);
      expect(store.sessions.single.status, SessionStatus.open);
      expect(store.remainingFor(student.id, group.id), 4);
      await capture(tester, 'focus-student-lookup-dark');
      await tester.tap(find.text('التحضير'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      await mutateThroughUi(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.attendances, hasLength(1));
      expect(store.remainingFor(student.id, group.id), 3);
      expect(store.payments, hasLength(originalPayments));
    },
  );

  testWidgets(
    'a cleared or failed new query cannot prepare the previously selected student',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await tester.enterText(
        find.byKey(const Key('student-search')),
        'unknown',
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      await tester.runAsync(
        () => acknowledgeNotice(
          tester,
          message: 'لم نجد الطالب. راجع الكود أو أضفه من الاختصار.',
        ),
      );
      await tester.enterText(find.byKey(const Key('student-search')), '');
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.pumpAndSettle();
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      await scan(tester, student.code);
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('student-search')));
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      await tester.tap(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
      await tester.pumpAndSettle();
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      await mutateThroughUi(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.attendances, hasLength(1));
      expect(store.payments, hasLength(1));
    },
  );

  testWidgets(
    'L collects the selected student once and retains code focus across repeated keys',
    (tester) async {
      await openWorkspace(tester);
      expectCodeFocus(tester);
      await scan(tester, student.code);
      expectCodeFocus(tester);
      await tester.runAsync(() async {
        await tester.sendKeyDownEvent(LogicalKeyboardKey.keyL);
        await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyL);
        await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyL);
        await tester.sendKeyUpEvent(LogicalKeyboardKey.keyL);
      });
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      expect(
        find.byKey(const Key('entry-confirmation-dialog')),
        findsOneWidget,
      );
      await mutateThroughUi(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.payments, hasLength(1));
      expect(store.payments.single.netAmount, 7500);
      expect(store.attendances, hasLength(1));
      expectCodeFocus(tester);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.pumpAndSettle();
      expect(store.payments, hasLength(1));
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('student-search')))
            .controller!
            .text,
        isEmpty,
      );
      await tester.tap(find.text('الحصص والحضور'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
    },
  );

  testWidgets(
    'M buys four sessions, attends once and is safe against repeated activation',
    (tester) async {
      await openWorkspace(tester);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      expect(store.payments, isEmpty);
      await scan(tester, student.code);
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await mutateThroughUi(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.payments.single.netAmount, 30000);
      expect(store.remainingFor(student.id, group.id), 3);
      expect(store.attendances, hasLength(1));
      expectCodeFocus(tester);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.pumpAndSettle();
      expect(store.payments, hasLength(1));
    },
  );

  testWidgets(
    'pending search and student editor prevent payment against the previous student',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      await tester.enterText(
        find.byKey(const Key('student-search')),
        'another code',
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-attend')))
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-package')))
            .onPressed,
        isNull,
      );
      await scan(tester, student.code);
      await tester.sendKeyEvent(LogicalKeyboardKey.f4);
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      expect(store.payments, isEmpty);
      await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(store.payments, isEmpty);
    },
  );

  testWidgets('changing session and canceling close returns to the code', (
    tester,
  ) async {
    final first = store.sessions.single;
    await tester.runAsync(
      () => store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: first.startsAt.add(const Duration(days: 7)),
          createdAt: DateTime.now(),
        ),
      ),
    );
    await openWorkspace(tester);
    await tester.tap(find.byKey(ValueKey('session-${first.id}')));
    await tester.pumpAndSettle();
    await tester.runAsync(() => tester.sendKeyEvent(LogicalKeyboardKey.escape));
    await tester.pumpAndSettle();
    expectCodeFocus(tester);
    await tester.tap(find.byKey(ValueKey('session-${first.id}')));
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('حصة 2 ·').last);
    await tester.pumpAndSettle();
    expectCodeFocus(tester);
    await tester.tap(find.widgetWithText(OutlinedButton, 'إنهاء الحصة'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(TextButton, 'استكمال التحضير'));
    await tester.pumpAndSettle();
    expectCodeFocus(tester);
    expect(store.sessions.every((s) => s.status == SessionStatus.open), isTrue);
  });

  testWidgets('ending opens that session closing without finalizing cash', (
    tester,
  ) async {
    final current = store.sessions.single;
    await tester.runAsync(() async {
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: current.id,
          mode: EntryMode.single,
        ),
      );
      // A newer pending closing must not take the ended session's place.
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: current.startsAt.add(const Duration(days: 7)),
          kind: SessionKind.free,
          createdAt: DateTime.now(),
        ),
      );
      await store.closeSession(store.sessions.last.id);
    });
    await openWorkspace(tester);
    await scan(tester, student.code);
    await tester.runAsync(() async {
      await tester.tap(find.widgetWithText(OutlinedButton, 'إنهاء الحصة'));
      await tester.pumpAndSettle();
      final persisted = Completer<void>();
      void changed() {
        if (!persisted.isCompleted) persisted.complete();
      }

      store.addListener(changed);
      try {
        await tester.tap(
          find.widgetWithText(FilledButton, 'إنهاء وعرض التقفيلة'),
        );
        await persisted.future.timeout(const Duration(seconds: 5));
        await Future<void>(() {});
        expect(store.sessions.first.status, SessionStatus.closed);
        expect(store.closings, isEmpty);
        await acknowledgeNotice(
          tester,
          message: 'تم إنهاء الحصة وحفظ سجل الغياب.',
        );
      } finally {
        store.removeListener(changed);
      }
      await tester.pumpAndSettle();
      expect(store.sessions.first.status, SessionStatus.closed);
      expect(store.closings, isEmpty);
      expect(find.byType(ClosingsPage), findsOneWidget);
      expect(
        tester.widget<ClosingsPage>(find.byType(ClosingsPage)).initialSessionId,
        current.id,
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      expect(store.payments, hasLength(1));
      expect(store.closings, isEmpty);
      await tester.tap(find.byTooltip('العودة للتحضير'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      await tester.tap(find.widgetWithText(OutlinedButton, 'عرض التقفيلة'));
      await tester.pumpAndSettle();
      expect(find.byType(ClosingsPage), findsOneWidget);
      expect(store.closings, isEmpty);
      expect(tester.takeException(), isNull);
    });
    await capture(tester, 'closing-after-ending');
  });

  testWidgets('failed ending keeps preparation open and shows no closing', (
    tester,
  ) async {
    final first = store.sessions.single;
    await tester.runAsync(
      () => store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 2,
          startsAt: first.startsAt.add(const Duration(days: 7)),
          createdAt: DateTime.now(),
        ),
      ),
    );
    await openWorkspace(tester);
    await tester.tap(find.byKey(ValueKey('session-${first.id}')));
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('حصة 2 ·').last);
    await tester.pumpAndSettle();
    await tester.runAsync(() async {
      await tester.tap(find.widgetWithText(OutlinedButton, 'إنهاء الحصة'));
      await tester.pumpAndSettle();
      await tester.tap(
        find.widgetWithText(FilledButton, 'إنهاء وعرض التقفيلة'),
      );
      await tester.pump();
      // Domain rejection happens before persistence; drain its async callback.
      await Future<void>(() {});
      await tester.pump(const Duration(milliseconds: 200));
      expect(find.byType(ClosingsPage), findsNothing);
      expect(find.textContaining('أغلق الحصة السابقة أولًا'), findsOneWidget);
      expect(
        store.sessions.every((s) => s.status == SessionStatus.open),
        isTrue,
      );
      expect(store.closings, isEmpty);
      await acknowledgeNotice(
        tester,
        message: 'أغلق الحصة السابقة أولًا حتى تُحسب الباقات بالترتيب.',
      );
      expectCodeFocus(tester);
    });
  });

  testWidgets(
    'single entry charges discounted price once and counts current session only',
    (tester) async {
      final currentId = store.sessions.single.id;
      await tester.runAsync(() async {
        await store.saveSession(store.sessions.single.copyWith(number: 4));
        for (var number = 1; number <= 3; number++) {
          await store.saveSession(
            LessonSession(
              groupId: group.id,
              number: number,
              startsAt: DateTime.now().subtract(
                Duration(days: 25 - number * 7),
              ),
              kind: number == 3 ? SessionKind.extra : SessionKind.free,
              extraPrice: number == 3 ? 10000 : 0,
              createdAt: DateTime.now(),
            ),
          );
          final past = store.sessions.last;
          if (number != 2) {
            await store.collectAndAttend(
              EntryRequest(
                studentId: student.id,
                sessionId: past.id,
                mode: EntryMode.single,
              ),
            );
          }
          await store.saveAcademic(
            AcademicRecord(
              studentId: student.id,
              sessionId: past.id,
              score: number == 2
                  ? null
                  : number == 1
                  ? 8
                  : 6,
              examAbsent: number == 2,
              homework: number == 1
                  ? HomeworkStatus.complete
                  : number == 2
                  ? HomeworkStatus.missing
                  : HomeworkStatus.incomplete,
              updatedAt: DateTime.now(),
            ),
          );
          await store.closeSession(past.id);
        }
      });
      await openWorkspace(tester);
      expect(find.byType(NavigationRail), findsNothing);
      expect(find.text(student.name), findsNothing);
      await scan(tester, student.code);
      expect(find.text('2 من 3'), findsOneWidget);
      expect(find.text('6 / 10'), findsWidgets);
      expect(find.text('ناقص'), findsWidgets);
      expect(find.byType(TabBar), findsNothing);
      expect(find.byType(ChoiceChip), findsNothing);
      expect(find.text('الامتحانات السابقة'), findsOneWidget);
      expect(find.text('الواجبات السابقة'), findsOneWidget);
      expect(
        find.text('لا يوجد رصيد حصص. حصّل الحصة أو اختار باقة قبل الدخول.'),
        findsOneWidget,
      );
      final action = find.byKey(const Key('collect-attend'));
      expect(tester.getRect(action).bottom, lessThanOrEqualTo(900));
      expect(
        tester.getRect(find.byKey(const Key('collect-package'))).bottom,
        lessThanOrEqualTo(900),
      );
      expect(
        find.descendant(
          of: find.byKey(const Key('collect-package')),
          matching: find.textContaining('دفع الشهر (٤ حصص)'),
        ),
        findsOneWidget,
      );
      await capture(tester, 'focus-workspace');
      await tester.binding.setSurfaceSize(const Size(1280, 800));
      await tester.pumpAndSettle();
      expect(tester.getRect(action).bottom, lessThanOrEqualTo(800));
      expect(
        tester.getRect(find.text('8 / 10')).bottom,
        lessThanOrEqualTo(800),
      );
      final paymentTable = find.ancestor(
        of: find.text('المدفوع'),
        matching: find.byType(DataTable),
      );
      expect(find.text('المدفوعات الأصلية'), findsOneWidget);
      expect(paymentTable, findsOneWidget);
      final lastPayment = store.payments.last;
      final paymentAmount = find.descendant(
        of: paymentTable,
        matching: find.text(money(lastPayment.netAmount)),
      );
      expect(paymentAmount, findsWidgets);
      final paymentAction = find.byKey(
        ValueKey('cancel-payment-${lastPayment.id}'),
      );
      await tester.ensureVisible(paymentAction);
      await tester.pumpAndSettle();
      expect(tester.getRect(paymentAction).bottom, lessThanOrEqualTo(800));
      expect(
        tester.getRect(paymentAmount.first).bottom,
        lessThanOrEqualTo(800),
      );
      expect(tester.getRect(action).bottom, lessThanOrEqualTo(800));
      expect(tester.takeException(), isNull);
      await capture(tester, 'focus-workspace-1280');
      await tester.binding.setSurfaceSize(const Size(960, 900));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.binding.setSurfaceSize(const Size(1440, 900));
      await tester.pumpAndSettle();
      await tester.ensureVisible(action);
      await mutateThroughUi(tester, () => tester.tap(action));
      expect(store.payments.last.netAmount, 7500);
      await capture(tester, 'focus-workspace-registered');
      expect(store.attendanceCount(currentId), 1);
      expect(
        (tester.widget<Text>(
          find.byKey(const Key('session-attendance-counter')),
        )).data,
        '1 طالب',
      );
      await scan(tester, student.code);
      expect(find.byKey(const Key('collect-attend')), findsNothing);
      expect(store.payments, hasLength(2));
      expect(store.attendances, hasLength(4));
      await tester.sendKeyEvent(LogicalKeyboardKey.f6);
      await tester.pumpAndSettle();
      expect(find.text('جاهز لاستقبال الطالب'), findsOneWidget);
      expect(store.attendanceCount(currentId), 1);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'paid absence makeup is selected from entry without a second charge',
    (tester) async {
      await tester.runAsync(() async {
        await store.renewPackage(
          PackageRequest(studentId: student.id, groupId: group.id),
        );
        await store.closeSession(store.sessions.single.id);
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: 2,
            startsAt: DateTime.now().add(const Duration(minutes: 2)),
            createdAt: DateTime.now(),
          ),
        );
      });
      final target = store.sessions.last;
      await openWorkspace(tester);
      await scan(tester, student.code);
      final makeupSwitch = find.widgetWithText(TextButton, 'تعويض حصة غابها');
      await tester.ensureVisible(makeupSwitch);
      await tester.pumpAndSettle();
      await tester.tap(makeupSwitch);
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
      final action = find.byKey(const Key('collect-attend'));
      await tester.ensureVisible(action);
      await mutateThroughUi(tester, () => tester.tap(action));
      expect(store.payments, hasLength(1));
      expect(store.remainingFor(student.id, group.id), 3);
      expect(store.attendances.last.status, AttendanceStatus.makeup);
      expect(
        store.attendances.last.originalAttendanceId,
        store.attendances.first.id,
      );
      expect(store.attendanceCount(target.id), 1);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'package quote respects selected session eligibility before charging',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveSession(
          store.sessions.single.copyWith(
            startsAt: DateTime.now().subtract(const Duration(hours: 2)),
          ),
        );
        await store.renewPackage(
          PackageRequest(studentId: student.id, groupId: group.id),
        );
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      expect(find.byType(ChoiceChip), findsNothing);
      final action = find.byKey(const Key('collect-package'));
      expect(
        find.descendant(of: action, matching: find.textContaining('٣٠٠')),
        findsOneWidget,
      );
      await mutateThroughUi(tester, () => tester.tap(action));
      expect(store.payments, hasLength(2));
      expect(store.payments.last.netAmount, 30000);
      expect(store.remainingFor(student.id, group.id), 7);
      expect(store.attendanceCount(store.sessions.single.id), 1);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'eligible prepaid balance shows direct package entry and disables a single charge',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveSession(
          store.sessions.single.copyWith(
            startsAt: DateTime.now().subtract(const Duration(hours: 2)),
          ),
        );
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      expect(find.byType(ChoiceChip), findsNothing);
      await tester.runAsync(
        () => store.renewPackage(
          PackageRequest(
            studentId: student.id,
            groupId: group.id,
            sessionId: store.sessions.single.id,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-attend')))
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-package')))
            .onPressed,
        isNotNull,
      );
      expect(
        tester.widget<Text>(find.byKey(const Key('amount-due'))).data,
        money(0),
      );
      expect(
        find.text(
          'الباقة السارية تغطي هذه الحصة. الدخول العادي يُحسب من رصيدها.',
        ),
        findsOneWidget,
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.pumpAndSettle();
      expect(store.payments, hasLength(1));
      expect(store.attendances, isEmpty);
      await tester.runAsync(
        () => acknowledgeNotice(
          tester,
          message: 'الباقة تغطي الحصة. استخدم M ثم Enter للتسجيل من الرصيد.',
        ),
      );
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await mutateThroughUi(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.payments, hasLength(1));
      expect(store.payments.single.netAmount, 30000);
      expect(store.remainingFor(student.id, group.id), 3);
      expect(store.attendanceCount(store.sessions.single.id), 1);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'F4 enrolls a new student in selected group without leaving focus screen',
    (tester) async {
      await openWorkspace(tester);
      await tester.sendKeyEvent(LogicalKeyboardKey.f4);
      await tester.pumpAndSettle();
      expect(find.text('إضافة طالب وتسجيله'), findsOneWidget);
      final name = find.widgetWithText(TextFormField, 'اسم الطالب');
      await tester.enterText(name, 'مينا سامح');
      await mutateThroughUi(
        tester,
        () => tester.tap(find.widgetWithText(FilledButton, 'حفظ الطالب')),
      );
      expect(store.students, hasLength(2));
      expect(store.students.last.groupIds, [group.id]);
      expect(find.text('مينا سامح'), findsOneWidget);
      expect(find.byType(AlertDialog), findsNothing);
      expect(find.byType(NavigationRail), findsNothing);
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'cashier opens corrections for the focused student then returns to code without collecting',
    (tester) async {
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'cashier',
          password: 'cashier-test-pass',
          role: StaffRole.cashier,
        );
        store.signOut();
        await store.signIn('cashier', 'cashier-test-pass');
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      await tester.ensureVisible(
        find.widgetWithText(TextButton, 'تصحيح واسترداد'),
      );
      await tester.tap(find.widgetWithText(TextButton, 'تصحيح واسترداد'));
      await tester.pumpAndSettle();
      final field = tester.widget<TextField>(
        find.byKey(const Key('correction-student-code')),
      );
      expect(field.controller!.text, student.code);
      expect(find.text('تصحيح الحضور والدفع'), findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      await tester.tap(find.byTooltip('العودة للتحضير'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'inline note editing stays bound to scanned student, blocks charges and persists only notes',
    (tester) async {
      late Student other;
      await tester.runAsync(() async {
        await store.saveStudent(
          student.copyWith(notes: 'اتصل بولي الأمر قبل الدخول'),
        );
        await store.saveStudent(
          Student(
            code: '124',
            name: 'مينا عادل',
            groupIds: [group.id],
            notes: 'ملاحظة مينا',
            createdAt: DateTime.now(),
          ),
        );
        other = store.students.last;
        await store.saveStaff(
          name: 'cashier',
          password: 'cashier-test-pass',
          role: StaffRole.cashier,
        );
        store.signOut();
        await store.signIn('cashier', 'cashier-test-pass');
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      expect(find.text('اتصل بولي الأمر قبل الدخول'), findsOneWidget);
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('student-note-editor')),
        'LM ملاحظة معدلة',
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyEvent(LogicalKeyboardKey.f6);
      await tester.pumpAndSettle();
      // Even a queued scanner submit cannot replace the editing student's identity.
      final codeField = tester.widget<TextField>(
        find.byKey(const Key('student-search')),
      );
      expect(codeField.readOnly, isTrue);
      codeField.controller!.text = other.code;
      codeField.onSubmitted!(other.code);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('student-note-editor')), findsOneWidget);
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      // A concurrent non-note update must survive saving the note.
      await tester.runAsync(() async {
        store.signOut();
        await store.signIn('مدير الاختبار', 'test-password-2026');
        await store.saveStudent(
          store.students
              .firstWhere((s) => s.id == student.id)
              .copyWith(phone: '01099999999'),
        );
      });
      await mutateThroughUi(tester, () async {
        await tester.tap(find.byKey(const Key('save-student-note')));
        await tester.tap(find.byKey(const Key('save-student-note')));
      });
      final saved = store.students.firstWhere((s) => s.id == student.id);
      expect(saved.notes, 'LM ملاحظة معدلة');
      expect(saved.phone, '01099999999');
      expect(saved.discountPercent, 25);
      expect(
        store.audit.where((row) => row.action == 'student_note'),
        hasLength(1),
      );
      expect(
        store.students.firstWhere((s) => s.id == other.id).notes,
        'ملاحظة مينا',
      );
      expectCodeFocus(tester);
      await scan(tester, other.code);
      expect(find.text('ملاحظة مينا'), findsOneWidget);
      await scan(tester, student.code);
      expect(find.text('LM ملاحظة معدلة'), findsOneWidget);
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('student-note-editor')),
        'لن تحفظ',
      );
      await tester.tap(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(
        store.students.firstWhere((s) => s.id == student.id).notes,
        'LM ملاحظة معدلة',
      );
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      await mutateThroughUi(
        tester,
        () => tester.tap(find.byKey(const Key('clear-student-note'))),
      );
      expectCodeFocus(tester);
      expect(
        store.students.firstWhere((s) => s.id == student.id).notes,
        isEmpty,
      );
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(
        store.audit.where((row) => row.action == 'student_note'),
        hasLength(2),
      );
      await tester.runAsync(() async {
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.signIn('مدير الاختبار', 'test-password-2026');
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      expect(find.text('لا توجد ملاحظة مسجلة.'), findsOneWidget);
      expect(
        store.students.firstWhere((s) => s.id == other.id).notes,
        'ملاحظة مينا',
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'student note panel and direct payment actions fit light and dark desktop viewports',
    (tester) async {
      await tester.runAsync(
        () => store.saveStudentNote(
          studentId: student.id,
          notes: 'الملاحظة المهمة: تواصل مع ولي الأمر بعد الحصة',
        ),
      );
      for (final dark in [false, true]) {
        for (final width in [1280.0, 1440.0]) {
          await openWorkspace(tester, dark: dark, width: width);
          await scan(tester, student.code);
          expect(
            find.text('الملاحظة المهمة: تواصل مع ولي الأمر بعد الحصة'),
            findsOneWidget,
          );
          final single = find.byKey(const Key('collect-attend'));
          final month = find.byKey(const Key('collect-package'));
          expect(tester.getRect(single).bottom, lessThanOrEqualTo(900));
          expect(tester.getRect(month).bottom, lessThanOrEqualTo(900));
          expect(tester.takeException(), isNull);
          await capture(
            tester,
            'focus-notes-${dark ? 'dark' : 'light'}-${width.toInt()}',
          );
        }
      }
    },
  );
  Future<void> chooseQuantity(WidgetTester tester, int count) async {
    final dropdown = find.descendant(
      of: find.byKey(const Key('package-quantity')),
      matching: find.byType(DropdownButtonFormField<int>),
    );
    await tester.tap(dropdown);
    await tester.pumpAndSettle();
    await tester.tap(
      find
          .text(
            count == 2
                ? 'حصتين'
                : count == 3
                ? '٣ حصص'
                : '٤ حصص',
          )
          .last,
    );
    await tester.pumpAndSettle();
  }

  Future<void> quantityShortcut(
    WidgetTester tester,
    LogicalKeyboardKey key,
  ) async {
    await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
    await tester.sendKeyEvent(key);
    await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
    await tester.pumpAndSettle();
  }

  for (final count in [2, 3]) {
    testWidgets(
      'selected $count session package quotes independent price, buys once and resets for next student',
      (tester) async {
        await tester.runAsync(
          () => store.saveGroup(
            group.copyWith(twoSessionPrice: 18000, threeSessionPrice: 27000),
          ),
        );
        await openWorkspace(
          tester,
          dark: true,
          width: count == 2 ? 1280 : 1440,
        );
        await scan(tester, student.code);
        await chooseQuantity(tester, count);
        expectCodeFocus(tester);
        await capture(
          tester,
          'focus-package-$count-dark-${count == 2 ? 1280 : 1440}',
        );
        expect(tester.takeException(), isNull);
        expect(store.payments, isEmpty);
        expect(store.packages, isEmpty);
        final price = count == 2 ? 13500 : 20250;
        expect(
          find.textContaining(
            'تحصيل باقة ${count == 2 ? 'حصتين' : '٣ حصص'} · ${money(price)}',
          ),
          findsOneWidget,
        );
        if (count == 2) {
          await mutateThroughUi(
            tester,
            () => tester.tap(find.byKey(const Key('collect-package'))),
          );
        } else {
          await tester.runAsync(() async {
            await tester.sendKeyDownEvent(LogicalKeyboardKey.keyM);
            await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyM);
            await tester.sendKeyRepeatEvent(LogicalKeyboardKey.keyM);
            await tester.sendKeyUpEvent(LogicalKeyboardKey.keyM);
          });
          await tester.pumpAndSettle();
          expect(store.payments, isEmpty);
          expect(
            find.byKey(const Key('entry-confirmation-dialog')),
            findsOneWidget,
          );
          await mutateThroughUi(
            tester,
            () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
          );
        }
        expect(store.payments, hasLength(1));
        expect(store.payments.single.netAmount, price);
        expect(store.packages.single.totalSessions, count);
        expect(store.packages.single.remaining, count - 1);
        expect(store.attendances, hasLength(1));
        await tester.runAsync(() async {
          await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
          await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        });
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('massar-notice-dialog')), findsOneWidget);
        expect(find.text('الحضور مسجل بالفعل'), findsOneWidget);
        expect(store.payments, hasLength(1));
        expect(store.attendances, hasLength(1));
        expect(store.packages.single.remaining, count - 1);
        await tester.runAsync(() => acknowledgeNotice(tester));
        await tester.pumpAndSettle();
        expectCodeFocus(tester);
        await tester.sendKeyEvent(LogicalKeyboardKey.f6);
        await tester.pumpAndSettle();
        await scan(tester, student.code);
        // Existing balance still dominates; quantity resets even for a repeated scan.
        final renewal = find.widgetWithText(
          OutlinedButton,
          'تحصيل باقة ٤ حصص جديدة · ${money(30000)}',
        );
        await tester.ensureVisible(renewal);
        await tester.tap(renewal);
        await tester.pumpAndSettle();
        expect(
          tester
              .widget<DropdownButtonFormField<int>>(
                find.byKey(const Key('renew-package-quantity')),
              )
              .initialValue,
          4,
        );
        await tester.tap(find.text('رجوع'));
        await tester.pumpAndSettle();
        expectCodeFocus(tester);
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'quantity shortcuts only choose, numeric scanner codes stay intact and lookup or note editing cannot buy',
    (tester) async {
      late Student numeric;
      await tester.runAsync(() async {
        await store.saveGroup(
          group.copyWith(twoSessionPrice: 18000, threeSessionPrice: 27000),
        );
        await store.saveStudent(
          Student(
            code: '234',
            name: 'مينا الرقمي',
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
        numeric = store.students.last;
      });
      await openWorkspace(tester);
      await quantityShortcut(
        tester,
        LogicalKeyboardKey.digit2,
      ); // no student yet
      await scan(
        tester,
        student.code,
      ); // selecting student safely restores four
      expect(find.byKey(const ValueKey('package-quantity-4')), findsOneWidget);
      await quantityShortcut(tester, LogicalKeyboardKey.digit3);
      expect(find.byKey(const ValueKey('package-quantity-3')), findsOneWidget);
      expectCodeFocus(tester);
      expect(store.payments, isEmpty);
      await tester.sendKeyEvent(LogicalKeyboardKey.digit2);
      await tester.sendKeyEvent(LogicalKeyboardKey.numpad4);
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('package-quantity-3')), findsOneWidget);
      await scan(tester, numeric.code);
      expect(find.text('مينا الرقمي'), findsOneWidget);
      expect(find.byKey(const ValueKey('package-quantity-4')), findsOneWidget);
      await quantityShortcut(tester, LogicalKeyboardKey.numpad2);
      expect(find.byKey(const ValueKey('package-quantity-2')), findsOneWidget);
      await tester.tap(find.text('بحث عن طالب'));
      await tester.pumpAndSettle();
      await quantityShortcut(tester, LogicalKeyboardKey.digit4);
      expect(find.byKey(const ValueKey('package-quantity-2')), findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      await tester.tap(find.text('التحضير'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      await quantityShortcut(tester, LogicalKeyboardKey.digit3);
      expect(find.byKey(const ValueKey('package-quantity-2')), findsOneWidget);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      await tester.tap(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      await confirmShortcut(tester, LogicalKeyboardKey.keyL);
      await mutateThroughUi(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.payments.single.netAmount, 10000);
      expect(store.packages, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'missing independent price blocks a new package but existing prepaid entry and configured renewal work',
    (tester) async {
      await openWorkspace(tester);
      await scan(tester, student.code);
      await chooseQuantity(tester, 2);
      expect(
        find.byKey(const Key('package-price-unconfigured')),
        findsOneWidget,
      );
      expect(
        tester.widget<Text>(find.byKey(const Key('amount-due'))).data,
        'السعر غير محدد',
      );
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-package')))
            .onPressed,
        isNull,
      );
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(store.payments, isEmpty);
      expect(
        find.byKey(const Key('entry-confirmation-dialog')),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byKey(const Key('entry-confirmation-dialog')),
          matching: find.text('حدد سعر باقة حصتين في المجموعة أولًا.'),
        ),
        findsOneWidget,
      );
      expect(store.packages, isEmpty);
      expect(store.attendances, isEmpty);
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.escape),
      );
      await tester.pumpAndSettle();
      await tester.runAsync(
        () => store.renewPackage(
          PackageRequest(
            studentId: student.id,
            groupId: group.id,
            sessionId: store.sessions.single.id,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('collect-package')))
            .onPressed,
        isNotNull,
      );
      await confirmShortcut(tester, LogicalKeyboardKey.keyM);
      await mutateThroughUi(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
      );
      expect(store.payments, hasLength(1));
      expect(store.packages.single.totalSessions, 4);
      expect(store.packages.single.remaining, 3);
      final renew = find.widgetWithText(
        OutlinedButton,
        'اختيار وتحصيل باقة جديدة',
      );
      await tester.ensureVisible(renew);
      await tester.tap(renew);
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<FilledButton>(
              find.byKey(const Key('confirm-package-renewal')),
            )
            .onPressed,
        isNull,
      );
      // Shortcuts beneath the dialog cannot change quantity or collect anything.
      await quantityShortcut(tester, LogicalKeyboardKey.digit4);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
      expect(store.payments, hasLength(1));
      await tester.tap(find.text('رجوع'));
      await tester.pumpAndSettle();
      await tester.runAsync(
        () => store.saveGroup(
          group.copyWith(twoSessionPrice: 18000, threeSessionPrice: 27000),
        ),
      );
      await tester.pumpAndSettle();
      final pricedRenew = find.widgetWithText(
        OutlinedButton,
        'تحصيل باقة حصتين جديدة · ${money(13500)}',
      );
      await tester.ensureVisible(pricedRenew);
      await tester.runAsync(() => tester.tap(pricedRenew));
      await tester.pumpAndSettle();
      final dialogQuantity = find.byKey(const Key('renew-package-quantity'));
      await tester.tap(dialogQuantity);
      await tester.pumpAndSettle();
      await tester.tap(find.text('٣ حصص').last);
      await tester.pumpAndSettle();
      expect(find.textContaining('تضاف ٣ حصص للرصيد الحالي'), findsOneWidget);
      await mutateThroughUi(
        tester,
        () => tester.tap(find.byKey(const Key('confirm-package-renewal'))),
      );
      expect(store.payments, hasLength(2));
      expect(store.payments.last.netAmount, 20250);
      expect(store.packages.last.totalSessions, 3);
      expect(store.packages.last.remaining, 3);
      expect(store.attendances, hasLength(1));
      expectCodeFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Ctrl+2 chooses a quoted package and held Enter buys its two sessions once',
    (tester) async {
      await tester.runAsync(
        () => store.saveGroup(group.copyWith(twoSessionPrice: 18000)),
      );
      await openWorkspace(tester);
      await scan(tester, student.code);
      await quantityShortcut(tester, LogicalKeyboardKey.digit2);
      expect(store.payments, isEmpty);
      await mutateThroughUi(tester, () async {
        await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
        await tester.sendKeyEvent(
          LogicalKeyboardKey.digit3,
        ); // busy: cannot change this purchase
        await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
        await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      });
      expect(store.payments, hasLength(1));
      expect(store.payments.single.netAmount, 13500);
      expect(store.packages.single.totalSessions, 2);
      expect(store.packages.single.remaining, 1);
      expect(store.attendances, hasLength(1));
      expectCodeFocus(tester);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'a general notice guards preparation before its first frame and consumes mounted scanner shortcuts',
    (tester) async {
      late Student other;
      await tester.runAsync(() async {
        await store.saveStudent(
          Student(
            code: '124',
            name: 'مينا الحماية',
            groupIds: [group.id],
            createdAt: DateTime.now(),
          ),
        );
        other = store.students.last;
      });
      await openWorkspace(tester);
      await scan(tester, student.code);
      expectCodeFocus(tester);
      final auditCount = store.audit.length;
      final field = tester.widget<TextField>(
        find.byKey(const Key('student-search')),
      );
      final context = tester.element(find.byType(AttendanceWorkspace));
      const message = 'تنبيه عام لا يغيّر حساب الطالب.';
      await tester.runAsync(() async {
        final acknowledged = showMassarNotice(context, message);
        expect(hasPendingMassarNotice(context), isTrue);
        // No frame has mounted the notice yet; the queue guard must act now.
        expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
        await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
        await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
        await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
        await tester.sendKeyEvent(LogicalKeyboardKey.f4);
        await tester.sendKeyEvent(LogicalKeyboardKey.f6);
        field.onSubmitted!(other.code);
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.audit, hasLength(auditCount));
        expect(field.controller!.text, isEmpty);
        await tester.pump(const Duration(milliseconds: 200));
        expect(find.byKey(const Key('massar-notice-dialog')), findsOneWidget);
        await tester.sendKeyEvent(LogicalKeyboardKey.keyL);
        await tester.sendKeyEvent(LogicalKeyboardKey.keyM);
        await tester.sendKeyEvent(LogicalKeyboardKey.f4);
        await tester.sendKeyEvent(LogicalKeyboardKey.f6);
        field.onSubmitted!(other.code);
        expect(
          find.byKey(const Key('entry-confirmation-dialog')),
          findsNothing,
        );
        expect(find.text('إضافة طالب وتسجيله'), findsNothing);
        expect(find.text('سجل ${student.name}'), findsOneWidget);
        expect(find.text(other.name), findsNothing);
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        expect(store.audit, hasLength(auditCount));
        await acknowledgeNotice(tester, message: message);
        await acknowledged;
        expect(hasPendingMassarNotice(context), isFalse);
      });
      await tester.pumpAndSettle();
      expectCodeFocus(tester);
      expect(find.text('سجل ${student.name}'), findsOneWidget);
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.audit, hasLength(auditCount));
      expect(tester.takeException(), isNull);
    },
  );
}
