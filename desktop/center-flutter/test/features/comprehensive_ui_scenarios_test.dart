import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/features/management/academics_page.dart';
import 'package:massar_center/features/management/sessions_page.dart';
import 'package:massar_center/main.dart';
import 'package:massar_center/shared/appearance.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/notice_helpers.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late LessonSession session;
  late Student first, second;

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await initializeDateFormatting('ar_EG');
      directory = await Directory.systemTemp.createTemp(
        'massar-comprehensive-ui-',
      );
      store = await CenterStore.open(directory: directory.path);
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> seedClass(WidgetTester tester) => tester.runAsync(() async {
    await store.setupAdmin('مدير الاختبار', 'comprehensive-password');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(
        CatalogEntry(name: 'اختبار ${kind.name}', kind: kind),
      );
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة الشامل',
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
        twoSessionPrice: 18000,
        threeSessionPrice: 27000,
      ),
    );
    group = store.groups.single;
    for (final (code, name) in [
      ('201', 'أحمد الأول'),
      ('202', 'مينا التالي'),
    ]) {
      await store.saveStudent(
        Student(
          code: code,
          name: name,
          groupIds: [group.id],
          discountPercent: 25,
          notes: 'ملاحظة محفوظة',
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
    }
    first = store.students.first;
    second = store.students.last;
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 17,
        startsAt: DateTime.now().add(const Duration(minutes: 5)),
        createdAt: DateTime.now(),
      ),
    );
    session = store.sessions.single;
  });

  Future<void> open(
    WidgetTester tester,
    Widget page, {
    bool app = false,
  }) async {
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
      app
          ? page
          : MaterialApp(
              theme: MassarTheme.dark,
              builder: (context, child) => Directionality(
                textDirection: TextDirection.rtl,
                child: child!,
              ),
              home: Scaffold(body: page),
            ),
    );
    await tester.pumpAndSettle();
    await tester.runAsync(() => Future<void>(() {}));
    await tester.pumpAndSettle();
  }

  Future<void> mutate(
    WidgetTester tester,
    Future<void> Function() gesture,
    bool Function() persisted, {
    bool notice = true,
  }) async {
    await tester.runAsync(() async {
      final completed = Completer<void>();
      void observed() {
        if (persisted() && !completed.isCompleted) completed.complete();
      }

      store.addListener(observed);
      try {
        await gesture();
        await completed.future.timeout(const Duration(seconds: 5));
        if (notice) await acknowledgeNotice(tester);
      } finally {
        store.removeListener(observed);
      }
    });
    if (notice) await tester.pumpAndSettle();
  }

  Future<void> key(WidgetTester tester, LogicalKeyboardKey key) async {
    await tester.runAsync(() => tester.sendKeyEvent(key));
    await tester.pumpAndSettle();
  }

  Future<void> scan(WidgetTester tester, Student student) async {
    await tester.enterText(
      find.byKey(const Key('student-search')),
      student.code,
    );
    await tester.testTextInput.receiveAction(TextInputAction.done);
    await tester.pumpAndSettle();
  }

  void expectCodeFocus(WidgetTester tester, String codeKey) => expect(
    tester.widget<TextField>(find.byKey(Key(codeKey))).focusNode!.hasFocus,
    isTrue,
  );

  Future<void> choose(WidgetTester tester, String label, String option) async {
    final picker = find.widgetWithText(DropdownButtonFormField<String>, label);
    await tester.runAsync(() => tester.tap(picker));
    await tester.pumpAndSettle();
    await tester.runAsync(() => tester.tap(find.textContaining(option).last));
    await tester.pumpAndSettle();
  }

  Future<AcademicActivity> academicPage(WidgetTester tester) async {
    await seedClass(tester);
    final activity =
        await tester.runAsync(
              () => store.saveAcademicActivity(
                AcademicActivity(
                  sessionId: session.id,
                  kind: AcademicActivityKind.exam,
                  name: 'الامتحان المحدد',
                  maxScore: 20,
                  createdAt: DateTime.now(),
                ),
              ),
            )
            as AcademicActivity;
    await open(
      tester,
      AnimatedBuilder(
        animation: store,
        builder: (context, _) => AcademicsPage(store: store),
      ),
    );
    await choose(tester, 'اختر الحصة للرصد', 'حصة 17 —');
    expect(find.byKey(const Key('selected-academic-activity')), findsOneWidget);
    return activity;
  }

  Future<void> gradeStudent(WidgetTester tester, Student student) async {
    await tester.enterText(
      find.byKey(const Key('academic-code-search')),
      student.code,
    );
    await tester.testTextInput.receiveAction(TextInputAction.search);
    await tester.pumpAndSettle();
    final details = find.byKey(const Key('academic-quick-details'));
    expect(details, findsOneWidget);
    await tester.ensureVisible(details);
    await tester.tap(details);
    await tester.pumpAndSettle();
    expect(find.widgetWithText(TextFormField, 'درجة الطالب'), findsOneWidget);
  }

  testWidgets(
    'keyboard first-run confirmation rejects mismatch and duplicate submits create only one admin',
    (tester) async {
      await open(tester, CenterApp(store: store), app: true);
      await tester.enterText(find.byKey(const Key('auth-name')), 'حساب جديد');
      await tester.enterText(
        find.byKey(const Key('auth-password')),
        'keyboard-password',
      );
      await tester.enterText(
        find.byKey(const Key('auth-confirm')),
        'different-password',
      );
      await tester.testTextInput.receiveAction(TextInputAction.done);
      await tester.pump();
      expect(find.text('كلمتا المرور غير متطابقتين'), findsOneWidget);
      expect(store.hasStaff, isFalse);
      expect(store.currentUser, isNull);
      await tester.enterText(
        find.byKey(const Key('auth-confirm')),
        'keyboard-password',
      );
      await mutate(
        tester,
        () async {
          await tester.testTextInput.receiveAction(TextInputAction.done);
          await tester.testTextInput.receiveAction(TextInputAction.done);
        },
        () => store.currentUser != null,
        notice: false,
      );
      await tester.pumpAndSettle();
      expect(store.staff, hasLength(1));
      expect(store.currentUser!.name, 'حساب جديد');
      expect(store.canManage, isTrue);
      expect(find.byKey(const Key('auth-confirm')), findsNothing);
      expect(find.byKey(const Key('management-navigation')), findsOneWidget);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'held payment Enter and scanner text in the resulting success notice never bill the next student',
    (tester) async {
      await seedClass(tester);
      await open(tester, AttendanceWorkspace(store: store, onExit: () {}));
      await scan(tester, first);
      await key(tester, LogicalKeyboardKey.keyL);
      await mutate(
        tester,
        () => tester.sendKeyDownEvent(LogicalKeyboardKey.enter),
        () => store.attendances.length == 1,
        notice: false,
      );
      await tester.runAsync(() async {
        for (var attempt = 0; attempt < 30; attempt++) {
          await Future<void>.delayed(const Duration(milliseconds: 20));
          await tester.pump(const Duration(milliseconds: 100));
          if (find
              .byKey(const Key('massar-notice-dialog'))
              .evaluate()
              .isNotEmpty) {
            break;
          }
        }
        await tester.pump(const Duration(milliseconds: 300));
      });
      expect(find.byKey(const Key('massar-notice-dialog')), findsOneWidget);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      await tester.pump(const Duration(milliseconds: 100));
      expect(find.byKey(const Key('massar-notice-dialog')), findsOneWidget);
      await tester.runAsync(() async {
        for (final scanKey in [
          LogicalKeyboardKey.digit2,
          LogicalKeyboardKey.digit0,
          LogicalKeyboardKey.digit2,
        ]) {
          await tester.sendKeyEvent(scanKey);
        }
        await tester.sendKeyEvent(LogicalKeyboardKey.enter);
        await tester.pump(const Duration(milliseconds: 300));
        await Future<void>.delayed(const Duration(milliseconds: 20));
        await tester.pump(const Duration(milliseconds: 300));
      });
      await tester.pumpAndSettle();
      expect(store.payments, hasLength(1));
      expect(store.payments.single.studentId, first.id);
      expect(store.attendances.single.studentId, first.id);
      expect(store.packages, isEmpty);
      expect(store.cardPayments, isEmpty);
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('student-search')))
            .controller!
            .text,
        isEmpty,
      );
      expectCodeFocus(tester, 'student-search');
      await scan(tester, second);
      await key(tester, LogicalKeyboardKey.keyL);
      await mutate(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
        () => store.attendances.length == 2,
      );
      expect(store.payments.map((payment) => payment.studentId).toSet(), {
        first.id,
        second.id,
      });
      expect(
        store.payments.every((payment) => payment.netAmount == 7500),
        isTrue,
      );
      expectCodeFocus(tester, 'student-search');
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'failed appearance save preserves student note and package choice then recovers without collecting',
    (tester) async {
      await seedClass(tester);
      await open(tester, CenterApp(store: store), app: true);
      await tester.tap(find.text('التحضير والتحصيل'));
      await tester.pumpAndSettle();
      await scan(tester, first);
      await tester.sendKeyDownEvent(LogicalKeyboardKey.controlLeft);
      await key(tester, LogicalKeyboardKey.digit3);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.controlLeft);
      await tester.tap(find.byKey(const Key('edit-student-note')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('student-note-editor')),
        'مسودة تظل كما هي',
      );
      final settings = AppearanceScope.maybeOf(
        tester.element(find.byKey(const Key('student-search'))),
      )!;
      expect(settings.mode, ThemeMode.dark);
      await tester.runAsync(() async {
        await Directory('${settings.file.path}.tmp').create();
        await tester.tap(find.byKey(const Key('appearance-toggle')));
        await acknowledgeNotice(
          tester,
          message: 'تعذر حفظ المظهر على الجهاز. حاول مرة أخرى.',
        );
      });
      await tester.pumpAndSettle();
      expect(settings.mode, ThemeMode.dark);
      expect(
        tester
            .widget<TextField>(find.byKey(const Key('student-note-editor')))
            .controller!
            .text,
        'مسودة تظل كما هي',
      );
      expectCodeFocus(tester, 'student-note-editor');
      expect(store.students.first.notes, 'ملاحظة محفوظة');
      expect(store.payments, isEmpty);
      await tester.runAsync(() async {
        await Directory('${settings.file.path}.tmp').delete();
        await tester.tap(find.byKey(const Key('appearance-toggle')));
      });
      for (var attempt = 0; attempt < 50 && settings.busy; attempt++) {
        await tester.runAsync(
          () => Future<void>.delayed(const Duration(milliseconds: 10)),
        );
        await tester.pump();
      }
      await tester.pumpAndSettle();
      expect(settings.busy, isFalse);
      expect(settings.mode, ThemeMode.light);
      expect(
        Theme.of(
          tester.element(find.byKey(const Key('student-search'))),
        ).brightness,
        Brightness.light,
      );
      await tester.ensureVisible(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('cancel-student-note')));
      await tester.pumpAndSettle();
      expectCodeFocus(tester, 'student-search');
      await key(tester, LogicalKeyboardKey.keyM);
      expect(find.text('شراء باقة 3 حصص وتسجيل الحضور'), findsOneWidget);
      expect(
        tester.widget<Text>(find.byKey(const Key('confirmation-net'))).data,
        money(20250),
      );
      await key(tester, LogicalKeyboardKey.escape);
      expectCodeFocus(tester, 'student-search');
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.students.first.notes, 'ملاحظة محفوظة');
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'invalid Arabic grade leaves named result untouched and corrected zero does not leak to next student',
    (tester) async {
      final activity = await academicPage(tester);
      await gradeStudent(tester, first);
      final score = find.widgetWithText(TextFormField, 'درجة الطالب');
      for (final invalid in ['-١', '٢٠٫٥', '٢١']) {
        await tester.enterText(score, invalid);
        await tester.tap(find.widgetWithText(FilledButton, 'حفظ'));
        await tester.pump();
        expect(find.text('الدرجة من صفر إلى الدرجة النهائية'), findsOneWidget);
        expect(store.academics, isEmpty);
        expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      }
      await tester.enterText(score, '٠');
      await mutate(
        tester,
        () => tester.tap(find.widgetWithText(FilledButton, 'حفظ')),
        () => store.academics.length == 1,
      );
      final saved = store.academics.single;
      expect(saved.activityId, activity.id);
      expect(saved.studentId, first.id);
      expect(saved.score, 0);
      expect(saved.maxScore, 20);
      expectCodeFocus(tester, 'academic-code-search');
      await gradeStudent(tester, second);
      expect(tester.widget<TextFormField>(score).controller!.text, isEmpty);
      expect(
        tester
            .widget<TextFormField>(find.byKey(const Key('academic-result-max')))
            .controller!
            .text,
        '20',
      );
      await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
      await tester.pumpAndSettle();
      expect(store.academics, hasLength(1));
      expectCodeFocus(tester, 'academic-code-search');
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'permission change during academic editing rejects save but preserves the draft and safe cancel focus',
    (tester) async {
      await academicPage(tester);
      await gradeStudent(tester, first);
      await tester.enterText(
        find.widgetWithText(TextFormField, 'درجة الطالب'),
        '١٨',
      );
      await tester.enterText(
        find.widgetWithText(TextFormField, 'ملاحظات الرصد'),
        'مسودة قبل تبديل الموظف',
      );
      await tester.runAsync(() async {
        await store.saveStaff(
          name: 'الاستقبال',
          password: 'cashier-password',
          role: StaffRole.cashier,
        );
        store.signOut();
        await store.signIn('الاستقبال', 'cashier-password');
      });
      await tester.pumpAndSettle();
      await tester.runAsync(() async {
        await tester.tap(find.widgetWithText(FilledButton, 'حفظ'));
        await acknowledgeNotice(
          tester,
          message: 'ليس لديك صلاحية لهذا الإجراء.',
        );
      });
      await tester.pumpAndSettle();
      expect(store.academics, isEmpty);
      expect(
        tester
            .widget<TextFormField>(
              find.widgetWithText(TextFormField, 'درجة الطالب'),
            )
            .controller!
            .text,
        '١٨',
      );
      expect(
        tester
            .widget<TextFormField>(
              find.widgetWithText(TextFormField, 'ملاحظات الرصد'),
            )
            .controller!
            .text,
        'مسودة قبل تبديل الموظف',
      );
      await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
      await tester.pumpAndSettle();
      expectCodeFocus(tester, 'academic-code-search');
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('add-academic-exam')))
            .onPressed,
        isNull,
      );
      expect(store.academicActivities, hasLength(1));
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'staff creation preserves invalid credentials then changed role signs in with academic-only permissions',
    (tester) async {
      await seedClass(tester);
      await open(tester, CenterApp(store: store), app: true);
      final navigation = find.text('الموظفون');
      await tester.scrollUntilVisible(
        navigation,
        180,
        scrollable: find.descendant(
          of: find.byKey(const Key('management-navigation')),
          matching: find.byType(Scrollable),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(navigation);
      await tester.pumpAndSettle();
      await tester.runAsync(
        () => tester.tap(find.widgetWithText(FilledButton, 'إضافة موظف')),
      );
      await tester.pumpAndSettle();
      final name = find.widgetWithText(TextFormField, 'اسم الدخول');
      final password = find.widgetWithText(TextFormField, 'كلمة المرور');
      await tester.enterText(name, 'مساعد جديد');
      await tester.enterText(password, 'short');
      await tester.runAsync(
        () => tester.tap(
          find.widgetWithText(DropdownButtonFormField<StaffRole>, 'الصلاحية'),
        ),
      );
      await tester.pumpAndSettle();
      await tester.runAsync(() => tester.tap(find.text('مساعد أكاديمي').last));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'حفظ'));
      await tester.pump();
      expect(find.text('استخدم ٨ أحرف على الأقل'), findsOneWidget);
      expect(tester.widget<TextFormField>(name).controller!.text, 'مساعد جديد');
      expect(store.staff, hasLength(1));
      await tester.enterText(password, 'assistant-password');
      final save = find.widgetWithText(FilledButton, 'حفظ');
      await mutate(tester, () async {
        await tester.tap(save);
        await tester.tap(save);
      }, () => store.staff.length == 2);
      expect(
        store.staff.singleWhere((row) => row.name == 'مساعد جديد').role,
        StaffRole.assistant,
      );
      expect(find.widgetWithText(TextFormField, 'اسم الدخول'), findsNothing);
      expect(find.text('مساعد جديد'), findsOneWidget);
      await tester.tap(find.text('تبديل الموظف'));
      await tester.pumpAndSettle();
      expect(store.currentUser, isNull);
      await tester.enterText(find.byKey(const Key('auth-name')), 'مساعد جديد');
      await tester.enterText(
        find.byKey(const Key('auth-password')),
        'assistant-password',
      );
      await mutate(
        tester,
        () => tester.testTextInput.receiveAction(TextInputAction.done),
        () => store.currentUser != null,
        notice: false,
      );
      await tester.pumpAndSettle();
      expect(store.currentUser!.role, StaffRole.assistant);
      expect(store.canAssess, isTrue);
      expect(store.canManage, isFalse);
      expect(store.canCollect, isFalse);
      expect(find.text('الامتحانات والواجب'), findsOneWidget);
      expect(find.text('الموظفون'), findsNothing);
      expect(find.text('التحضير والتحصيل'), findsNothing);
      expect(find.text('إنشاء حصة'), findsNothing);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'all groups creation hides date and manual number while preserving the single group choice',
    (tester) async {
      await seedClass(tester);
      await tester.runAsync(
        () => store.saveGroup(group.copyWith(id: '', name: 'المجموعة الثانية')),
      );
      await open(
        tester,
        AnimatedBuilder(
          animation: store,
          builder: (context, _) =>
              SessionsPage(store: store, onOpenAttendance: () {}),
        ),
      );
      await tester.runAsync(
        () => tester.tap(find.widgetWithText(FilledButton, 'إنشاء حصة')),
      );
      await tester.pumpAndSettle();
      await choose(tester, 'المجموعة', group.name);
      final all = find.widgetWithText(CheckboxListTile, 'كل المجموعات');
      await tester.tap(all);
      await tester.pumpAndSettle();
      expect(find.widgetWithText(TextFormField, 'رقم الحصة'), findsNothing);
      expect(find.byIcon(Icons.calendar_today_outlined), findsNothing);
      await tester.tap(all);
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<TextFormField>(
              find.widgetWithText(TextFormField, 'رقم الحصة'),
            )
            .controller!
            .text,
        '18',
      );
      await tester.tap(all);
      await tester.pumpAndSettle();
      await mutate(
        tester,
        () => tester.tap(find.widgetWithText(FilledButton, 'حفظ')),
        () => store.sessions.length == 3,
      );
      expect(
        store.sessions
            .where((session) => session.groupId == group.id)
            .map((session) => session.number),
        [17, 18],
      );
      expect(
        store.sessions
            .where((session) => session.groupId != group.id)
            .single
            .number,
        1,
      );
      expect(store.payments, isEmpty);
      expect(store.audit.last.action, 'sessions_create');
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'session UI creates independently priced extra class edits it and requires explicit unused cancellation',
    (tester) async {
      await seedClass(tester);
      await open(
        tester,
        AnimatedBuilder(
          animation: store,
          builder: (context, _) =>
              SessionsPage(store: store, onOpenAttendance: () {}),
        ),
      );
      await tester.runAsync(
        () => tester.tap(find.widgetWithText(FilledButton, 'إنشاء حصة')),
      );
      await tester.pumpAndSettle();
      expect(find.byIcon(Icons.calendar_today_outlined), findsNothing);
      await tester.tap(find.widgetWithText(FilledButton, 'حفظ'));
      await tester.pump();
      expect(find.text('اختر المجموعة'), findsOneWidget);
      expect(store.sessions, hasLength(1));
      await choose(tester, 'المجموعة', group.name);
      final number = find.widgetWithText(TextFormField, 'رقم الحصة');
      expect(tester.widget<TextFormField>(number).controller!.text, '18');
      await tester.runAsync(
        () => tester.tap(
          find.widgetWithText(
            DropdownButtonFormField<SessionKind>,
            'حساب الحصة',
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.runAsync(
        () => tester.tap(find.text(sessionKindLabel(SessionKind.extra)).last),
      );
      await tester.pumpAndSettle();
      await tester.enterText(
        find.widgetWithText(TextFormField, 'السعر المنفصل بالجنيه'),
        '٤٥٫٥٠',
      );
      final beforeSave = DateTime.now();
      await mutate(
        tester,
        () => tester.tap(find.widgetWithText(FilledButton, 'حفظ')),
        () => store.sessions.length == 2,
      );
      final createdId = store.sessions
          .singleWhere((row) => row.id != session.id)
          .id;
      var created = store.sessions.singleWhere((row) => row.id == createdId);
      expect(created.number, 18);
      expect(created.groupId, group.id);
      expect(created.kind, SessionKind.extra);
      expect(created.extraPrice, 4550);
      expect(created.status, SessionStatus.open);
      expect(created.startsAt.isBefore(beforeSave), isFalse);
      expect(created.startsAt.isAfter(DateTime.now()), isFalse);
      expect(created.startsAt, created.createdAt);
      final originalDate = created.startsAt;
      final edit = find.byTooltip('تعديل الحصة').last;
      await tester.ensureVisible(edit);
      await tester.runAsync(() => tester.tap(edit));
      await tester.pumpAndSettle();
      expect(find.byIcon(Icons.calendar_today_outlined), findsOneWidget);
      expect(
        tester
            .widget<DropdownButtonFormField<String>>(
              find.widgetWithText(DropdownButtonFormField<String>, 'المجموعة'),
            )
            .onChanged,
        isNull,
      );
      await tester.enterText(number, '19');
      await tester.enterText(
        find.widgetWithText(TextFormField, 'السعر المنفصل بالجنيه'),
        '٦٠',
      );
      await mutate(
        tester,
        () => tester.tap(find.widgetWithText(FilledButton, 'حفظ')),
        () =>
            store.sessions.singleWhere((row) => row.id == createdId).number ==
            19,
      );
      created = store.sessions.singleWhere((row) => row.id == createdId);
      expect(created.extraPrice, 6000);
      expect(created.startsAt, originalDate);
      expect(created.createdAt, originalDate);
      expect(created.id, createdId);
      final cancel = find.byTooltip('إلغاء الحصة').last;
      await tester.ensureVisible(cancel);
      await tester.runAsync(() => tester.tap(cancel));
      await tester.pumpAndSettle();
      expect(find.text('إلغاء الحصة رقم 19'), findsOneWidget);
      await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
      await tester.pumpAndSettle();
      expect(
        store.sessions.singleWhere((row) => row.id == createdId).status,
        SessionStatus.open,
      );
      await tester.runAsync(() => tester.tap(cancel));
      await tester.pumpAndSettle();
      await mutate(
        tester,
        () => tester.tap(find.widgetWithText(FilledButton, 'إلغاء الحصة')),
        () =>
            store.sessions.singleWhere((row) => row.id == createdId).status ==
            SessionStatus.canceled,
      );
      expect(store.sessions, hasLength(2));
      expect(
        store.sessions.singleWhere((row) => row.id == session.id).status,
        SessionStatus.open,
      );
      expect(store.attendances, isEmpty);
      expect(store.payments, isEmpty);
      expect(store.packages, isEmpty);
      expect(
        tester
            .widget<IconButton>(
              find.ancestor(
                of: find.byTooltip('تعديل الحصة').last,
                matching: find.byType(IconButton),
              ),
            )
            .onPressed,
        isNull,
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'used session editing and cancellation errors preserve draft and transactions then explicit closure registers absence',
    (tester) async {
      await seedClass(tester);
      await tester.runAsync(
        () => store.collectAndAttend(
          EntryRequest(
            studentId: first.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        ),
      );
      final originalPayment = store.payments.single;
      final originalAttendance = store.attendances.single;
      await open(
        tester,
        AnimatedBuilder(
          animation: store,
          builder: (context, _) =>
              SessionsPage(store: store, onOpenAttendance: () {}),
        ),
      );
      final edit = find.byTooltip('تعديل الحصة');
      await tester.ensureVisible(edit);
      await tester.runAsync(() => tester.tap(edit));
      await tester.pumpAndSettle();
      final number = find.widgetWithText(TextFormField, 'رقم الحصة');
      await tester.enterText(number, '99');
      await tester.runAsync(() async {
        await tester.tap(find.widgetWithText(FilledButton, 'حفظ'));
        await acknowledgeNotice(
          tester,
          message: 'لا يمكن تعديل حصة بها تسجيلات أو حصة مغلقة.',
        );
      });
      await tester.pumpAndSettle();
      expect(tester.widget<TextFormField>(number).controller!.text, '99');
      expect(store.sessions.single.number, 17);
      expect(store.sessions.single.status, SessionStatus.open);
      expect(store.payments.single.id, originalPayment.id);
      expect(store.attendances.single.id, originalAttendance.id);
      await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
      await tester.pumpAndSettle();
      final cancel = find.byTooltip('إلغاء الحصة');
      await tester.ensureVisible(cancel);
      await tester.runAsync(() => tester.tap(cancel));
      await tester.pumpAndSettle();
      await tester.runAsync(() async {
        await tester.tap(find.widgetWithText(FilledButton, 'إلغاء الحصة'));
        await acknowledgeNotice(
          tester,
          message:
              'لا يمكن إلغاء حصة مسجل بها حضور أو دفع أو رصد؛ سياسة الاسترداد لم تُحدد.',
        );
      });
      await tester.pumpAndSettle();
      expect(store.sessions.single.status, SessionStatus.open);
      final close = find.byTooltip('إغلاق وتسجيل الغياب');
      await tester.ensureVisible(close);
      await tester.runAsync(() => tester.tap(close));
      await tester.pumpAndSettle();
      expect(find.text('إغلاق الحصة رقم 17'), findsOneWidget);
      await mutate(
        tester,
        () => tester.tap(
          find.widgetWithText(FilledButton, 'إغلاق وتسجيل الغياب'),
        ),
        () => store.sessions.single.status == SessionStatus.closed,
      );
      expect(store.sessions.single.id, session.id);
      expect(store.payments, hasLength(1));
      expect(store.payments.single.id, originalPayment.id);
      expect(store.payments.single.netAmount, 7500);
      expect(
        store.attendances.singleWhere((row) => row.studentId == first.id).id,
        originalAttendance.id,
      );
      expect(
        store.attendances
            .singleWhere((row) => row.studentId == second.id)
            .status,
        AttendanceStatus.absent,
      );
      expect(store.packages, isEmpty);
      expect(store.refunds, isEmpty);
      expect(
        tester
            .widget<IconButton>(
              find.ancestor(
                of: find.byTooltip('تعديل الحصة'),
                matching: find.byType(IconButton),
              ),
            )
            .onPressed,
        isNull,
      );
      expect(
        tester
            .widget<IconButton>(
              find.ancestor(
                of: find.byTooltip('إغلاق وتسجيل الغياب'),
                matching: find.byType(IconButton),
              ),
            )
            .onPressed,
        isNull,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
