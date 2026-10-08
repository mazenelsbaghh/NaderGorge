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
import '../helpers/ui_wait_helpers.dart';
import '../helpers/attendance_ui_helpers.dart';

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
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'شهر الاختبار',
        price: 27000,
        lessons: [
          for (var number = 1; number <= 3; number++)
            PreparedLesson(number: number),
        ],
      ),
    );
    session = await store.startPreparedLesson(
      groupId: group.id,
      preparedLessonId: month.lessons.first.id,
    );
    group = store.groups.single;
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
    bool Function() persisted,
  ) async {
    await tester.runAsync(() async {
      await gesture();
      await Future<void>(() {});
      await tester.pump(const Duration(milliseconds: 20));
      await waitForUiCondition(
        tester,
        () =>
            persisted() &&
            find.byType(LinearProgressIndicator).evaluate().isEmpty &&
            find.byType(CircularProgressIndicator).evaluate().isEmpty,
        reason: 'The intended UI mutation must finish and persist.',
      );
    });
    await tester.pumpAndSettle();
  }

  Future<void> key(WidgetTester tester, LogicalKeyboardKey key) async {
    await tester.runAsync(() => tester.sendKeyEvent(key));
    await tester.pumpAndSettle();
  }

  Future<void> scan(WidgetTester tester, Student student) =>
      previewAttendanceStudent(tester, student.code);

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
                  preparedLessonId: session.preparedLessonId,
                  kind: AcademicActivityKind.exam,
                  name: 'الامتحان المحدد',
                  maxScore: 20,
                  createdAt: DateTime.now(),
                ),
              ),
            )
            as AcademicActivity;
    await tester.runAsync(() async {
      for (final student in [first, second]) {
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
      }
    });
    await open(
      tester,
      AnimatedBuilder(
        animation: store,
        builder: (context, _) => AcademicsPage(store: store),
      ),
    );
    await choose(tester, 'الشهر المشترك', 'شهر الاختبار');
    await choose(
      tester,
      'المجموعة التي بدأت الحصة',
      store.groupLabel(group.id),
    );
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
      await mutate(tester, () async {
        await tester.testTextInput.receiveAction(TextInputAction.done);
        await tester.testTextInput.receiveAction(TextInputAction.done);
      }, () => store.currentUser != null);
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
    'held confirmation Enter cannot charge the next code before a fresh Enter',
    (tester) async {
      await seedClass(tester);
      await open(
        tester,
        AttendanceWorkspace(
          store: store,
          onExit: () {},
          initialSessionId: session.id,
        ),
      );
      await scan(tester, first);
      await requestAttendanceConfirmation(tester, LogicalKeyboardKey.keyL);
      await tester.pumpAndSettle();
      await mutate(
        tester,
        () => tester.sendKeyDownEvent(LogicalKeyboardKey.enter),
        () => store.attendances.length == 1,
      );
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      await tester.enterText(
        find.byKey(const Key('student-search')),
        second.code,
      );
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();
      expect(store.payments, hasLength(1));
      expect(store.payments.single.studentId, first.id);
      expect(store.attendances.single.studentId, first.id);
      expect(store.packages, isEmpty);
      expect(store.cardPayments, isEmpty);
      await mutate(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
        () => store.attendances.length == 2,
      );
      expect(store.payments.single.studentId, first.id);
      expect(store.attendances.map((entry) => entry.studentId).toSet(), {
        first.id,
        second.id,
      });
      await mutate(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.keyL),
        () => store.payments.length == 2,
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
      await choose(tester, 'الشهر', 'شهر الاختبار');
      await choose(tester, 'المجموعة', store.groupLabel(group.id));
      await scan(tester, first);
      await selectAttendanceMonth(tester, 'شهر الاختبار · 3 حصص');
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
      await requestAttendanceConfirmation(tester, LogicalKeyboardKey.keyN);
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('confirmation-net')), findsOneWidget);
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
        await tester.tap(find.widgetWithText(FilledButton, 'حفظ الرصد'));
        await tester.pump();
        expect(find.text('الدرجة من صفر إلى الدرجة النهائية'), findsOneWidget);
        expect(store.academics, isEmpty);
        expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      }
      await tester.enterText(score, '٠');
      await mutate(
        tester,
        () => tester.tap(find.widgetWithText(FilledButton, 'حفظ الرصد')),
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
      await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
      await tester.pumpAndSettle();
      expect(store.academics, hasLength(1));
      expectCodeFocus(tester, 'academic-code-search');
      expect(store.attendances.map((row) => row.studentId).toSet(), {
        first.id,
        second.id,
      });
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'revoked staff session rejects academic save while preserving the draft and safe cancel',
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
        store.signOut();
      });
      await tester.pumpAndSettle();
      await tester.runAsync(() async {
        await tester.tap(find.widgetWithText(FilledButton, 'حفظ الرصد'));
        await waitForUiCondition(
          tester,
          () =>
              find.textContaining('ليس لديك صلاحية لهذا الإجراء.').evaluate().isNotEmpty,
          reason: 'The editor must keep the rejected academic save inline.',
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
      await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
      await tester.pumpAndSettle();
      expect(find.text('الخروج بدون حفظ؟'), findsOneWidget);
      await tester.tap(
        find.widgetWithText(FilledButton, 'تجاهل التعديلات والخروج'),
      );
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
        () => tester.tap(find.widgetWithText(FilledButton, 'إضافة موظف').last),
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
      await tester.tap(find.widgetWithText(FilledButton, 'إضافة موظف').last);
      await tester.pump();
      expect(find.text('استخدم ٨ أحرف على الأقل'), findsOneWidget);
      expect(tester.widget<TextFormField>(name).controller!.text, 'مساعد جديد');
      expect(store.staff, hasLength(1));
      await tester.enterText(password, 'assistant-password');
      final save = find.widgetWithText(FilledButton, 'إضافة موظف').last;
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
      );
      await tester.pumpAndSettle();
      expect(store.currentUser!.role, StaffRole.assistant);
      expect(store.canAssess, isTrue);
      expect(store.canManage, isFalse);
      expect(store.canCollect, isFalse);
      expect(find.text('رصد الامتحانات والواجبات'), findsOneWidget);
      expect(find.text('الموظفون'), findsNothing);
      expect(find.text('التحضير والتحصيل'), findsNothing);
      expect(find.text('إنشاء حصة'), findsNothing);
      expect(store.payments, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  Future<void> sessionsPage(WidgetTester tester) => open(
    tester,
    AnimatedBuilder(
      animation: store,
      builder: (context, _) =>
          SessionsPage(store: store, onOpenAttendance: () {}),
    ),
  );

  Future<void> prepareMonth(
    WidgetTester tester,
    String name, {
    bool extra = false,
  }) async {
    await tester.tap(find.widgetWithText(FilledButton, 'إضافة شهر وحصصه'));
    await tester.pumpAndSettle();
    expect(find.byIcon(Icons.calendar_today_outlined), findsNothing);
    expect(
      find.widgetWithText(DropdownButtonFormField<String>, 'المجموعة'),
      findsNothing,
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'اسم الشهر'),
      name,
    );
    await tester.enterText(
      find.widgetWithText(TextFormField, 'عدد الحصص المعدّة'),
      extra ? '1' : '2',
    );
    await tester.tap(find.text('تجهيز الحصص'));
    await tester.pumpAndSettle();
    if (extra) {
      final kind = find.widgetWithText(
        DropdownButtonFormField<SessionKind>,
        'حساب الحصة',
      );
      await tester.ensureVisible(kind);
      await tester.tap(kind);
      await tester.pumpAndSettle();
      await tester.tap(find.text(sessionKindLabel(SessionKind.extra)).last);
      await tester.pumpAndSettle();
      await tester.enterText(
        find.widgetWithText(TextFormField, 'سعر الحصة الإضافية بالجنيه'),
        '٤٥٫٥٠',
      );
    }
    final save = find.widgetWithText(FilledButton, 'إضافة شهر وحصصه').last;
    await tester.ensureVisible(save);
    await mutate(
      tester,
      () => tester.tap(save),
      () => store.studyMonths.any((month) => month.name == name),
    );
  }

  testWidgets(
    'shared lesson preparation creates no group sessions until each selected group starts',
    (tester) async {
      await seedClass(tester);
      await tester.runAsync(
        () => store.saveGroup(group.copyWith(id: '', name: 'المجموعة الثانية')),
      );
      final original = session.toJson();
      await sessionsPage(tester);
      await prepareMonth(tester, 'شهر مشترك جديد');
      final month = store.studyMonths.last;
      expect(month.lessons.map((lesson) => lesson.number), [1, 2]);
      expect(store.sessions.single.toJson(), original);
      expect(store.payments, isEmpty);
      await open(tester, AttendanceWorkspace(store: store, onExit: () {}));
      for (final selected in store.groups) {
        await choose(tester, 'المجموعة', store.groupLabel(selected.id));
        final before = store.sessions.length;
        await mutate(tester, () async {
          await tester.tap(find.byKey(const Key('start-attendance-session')));
          await tester.pump(const Duration(milliseconds: 200));
          expect(find.byKey(const Key('session-start-dialog')), findsOneWidget);
          await tester.tap(find.byKey(const Key('session-start-confirm')));
        }, () => store.sessions.length == before + 1);
        final started = store.sessionForPreparedLesson(
          selected.id,
          month.lessons.first.id,
        )!;
        expect(started.number, 1);
        expect(started.startedAt, isNotNull);
        expect(started.groupId, selected.id);
      }
      expect(store.sessions, hasLength(3));
      expect(
        store.sessions.singleWhere((row) => row.id == session.id).toJson(),
        original,
      );
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'extra lesson price is edited before starting and unused cancellation requires confirmation',
    (tester) async {
      await seedClass(tester);
      await sessionsPage(tester);
      await prepareMonth(tester, 'شهر إضافي', extra: true);
      final month = store.studyMonths.last;
      expect(month.lessons.single.extraPrice, 4550);
      expect(store.sessions, hasLength(1));
      await tester.tap(find.text('تعديل الشهر وحصصه'));
      await tester.pumpAndSettle();
      final price = find.widgetWithText(
        TextFormField,
        'سعر الحصة الإضافية بالجنيه',
      );
      await tester.enterText(price, '٦٠');
      final save = find.widgetWithText(FilledButton, 'حفظ التعديلات');
      await tester.ensureVisible(save);
      await mutate(
        tester,
        () => tester.tap(save),
        () => store.studyMonths.last.lessons.single.extraPrice == 6000,
      );
      final beforeStart = DateTime.now();
      final started = await tester.runAsync(
        () => store.startPreparedLesson(
          groupId: group.id,
          preparedLessonId: month.lessons.single.id,
        ),
      );
      await tester.pumpAndSettle();
      expect(started!.kind, SessionKind.extra);
      expect(started.extraPrice, 6000);
      expect(started.startsAt.isBefore(beforeStart), isFalse);
      final cancel = find.byTooltip('إلغاء الحصة');
      await tester.ensureVisible(cancel);
      await tester.tap(cancel);
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(TextButton, 'رجوع'));
      await tester.pumpAndSettle();
      expect(
        store.sessions.singleWhere((row) => row.id == started.id).status,
        SessionStatus.open,
      );
      await tester.tap(cancel);
      await tester.pumpAndSettle();
      await mutate(
        tester,
        () => tester.tap(find.widgetWithText(FilledButton, 'إلغاء الحصة')),
        () =>
            store.sessions.singleWhere((row) => row.id == started.id).status ==
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
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'used lesson protects its number and kind, rejects cancellation, and explicit closure records absence',
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
      await sessionsPage(tester);
      await tester.tap(find.text('تعديل الشهر وحصصه'));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<TextField>(
              find.descendant(
                of: find
                    .widgetWithText(TextFormField, 'رقم الحصة داخل الشهر')
                    .first,
                matching: find.byType(TextField),
              ),
            )
            .readOnly,
        isTrue,
      );
      expect(
        tester
            .widget<DropdownButtonFormField<SessionKind>>(
              find
                  .widgetWithText(
                    DropdownButtonFormField<SessionKind>,
                    'حساب الحصة',
                  )
                  .first,
            )
            .onChanged,
        isNull,
      );
      final cancelEditor = find.widgetWithText(TextButton, 'رجوع');
      await tester.ensureVisible(cancelEditor);
      await tester.tap(cancelEditor);
      await tester.pumpAndSettle();
      final cancel = find.byTooltip('إلغاء الحصة');
      await tester.ensureVisible(cancel);
      await tester.tap(cancel);
      await tester.pumpAndSettle();
      await tester.runAsync(() async {
        await tester.tap(find.widgetWithText(FilledButton, 'إلغاء الحصة'));
        await acknowledgeNotice(
          tester,
          message:
              'لا يمكن إلغاء حصة مسجل بها حضور أو دفع أو رصد؛ سياسة الاسترداد لم تُحدد.',
        );
      });
      expect(store.sessions.single.status, SessionStatus.open);
      expect(store.payments.single.id, originalPayment.id);
      expect(store.attendances.single.id, originalAttendance.id);
      final close = find.byTooltip('إغلاق وتسجيل الغياب');
      await tester.ensureVisible(close);
      await tester.tap(close);
      await tester.pumpAndSettle();
      await mutate(
        tester,
        () => tester.tap(
          find.widgetWithText(FilledButton, 'إغلاق وتسجيل الغياب'),
        ),
        () => store.sessions.single.status == SessionStatus.closed,
      );
      expect(store.sessions.single.id, session.id);
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
              find.byWidgetPredicate(
                (widget) =>
                    widget is IconButton &&
                    widget.tooltip == 'إغلاق وتسجيل الغياب',
              ),
            )
            .onPressed,
        isNull,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
