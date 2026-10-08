import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/rendering.dart';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/student_editor_dialog.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late Completer<String?> dialogResult;
  final captureKey = GlobalKey();
  const adminName = 'مدير الأكواد';
  const adminPassword = 'student-code-admin-password';
  const cashierName = 'استقبال الأكواد';
  const cashierPassword = 'student-code-cashier-password';

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      directory = await Directory.systemTemp.createTemp(
        'massar-student-codes-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin(adminName, adminPassword);
      await store.saveStaff(
        name: cashierName,
        password: cashierPassword,
        role: StaffRole.cashier,
      );
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(
          CatalogEntry(kind: kind, name: 'اختبار ${kind.name}'),
        );
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة الاختبار',
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
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Student draft(String name, {String code = '999999'}) => Student(
    name: name,
    code: code,
    groupIds: [group.id],
    createdAt: DateTime.now(),
  );

  Future<void> signInAs(WidgetTester tester, StaffRole role) async {
    await tester.runAsync(() async {
      store.signOut();
      await store.signIn(
        role == StaffRole.admin ? adminName : cashierName,
        role == StaffRole.admin ? adminPassword : cashierPassword,
      );
    });
  }

  Future<void> openEditor(
    WidgetTester tester, {
    Student? student,
    bool dark = false,
  }) async {
    await tester.binding.setSurfaceSize(const Size(1280, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    if (const bool.fromEnvironment('CAPTURE_UI')) {
      await tester.runAsync(() async {
        await (FontLoader('Tajawal')
              ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
              ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
            .load();
        await (FontLoader(
          'MaterialIcons',
        )..addFont(rootBundle.load('fonts/MaterialIcons-Regular.otf'))).load();
      });
    }
    dialogResult = Completer<String?>();
    await tester.pumpWidget(
      RepaintBoundary(
        key: captureKey,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          builder: (context, child) =>
              Directionality(textDirection: TextDirection.rtl, child: child!),
          theme: dark ? MassarTheme.dark : MassarTheme.light,
          home: Directionality(
            textDirection: TextDirection.rtl,
            child: Scaffold(
              body: Builder(
                builder: (context) => Center(
                  child: FilledButton(
                    onPressed: () async {
                      final result = await showDialog<String>(
                        context: context,
                        builder: (_) => StudentEditorDialog(
                          store: store,
                          student: student,
                          initialGroupId: group.id,
                        ),
                      );
                      dialogResult.complete(result);
                    },
                    child: const Text('فتح الطالب'),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.widgetWithText(FilledButton, 'فتح الطالب'));
    await tester.pumpAndSettle();
  }

  Finder field(String label) => find.widgetWithText(TextFormField, label);
  Finder getCode() => find.byKey(const Key('student-editor-code'));
  Finder getSave() =>
      find.widgetWithText(FilledButton, 'حفظ الطالب').evaluate().isNotEmpty
      ? find.widgetWithText(FilledButton, 'حفظ الطالب')
      : find.widgetWithText(FilledButton, 'حفظ التعديلات');

  String shownCode(WidgetTester tester) =>
      tester.widget<TextFormField>(getCode()).controller!.text;

  Future<String> save(WidgetTester tester, {bool doubleClick = false}) async {
    final saveButton = getSave();
    await tester.ensureVisible(saveButton);
    final staleCallback = tester.widget<FilledButton>(saveButton).onPressed!;
    await tester.runAsync(() async {
      final persisted = Completer<void>();
      void changed() {
        if (!persisted.isCompleted) persisted.complete();
      }

      store.addListener(changed);
      try {
        await tester.tap(saveButton);
        if (doubleClick) staleCallback();
        await persisted.future.timeout(const Duration(seconds: 5));
        await Future<void>(() {});
        await tester.pump();
      } finally {
        store.removeListener(changed);
      }
    });
    await tester.pumpAndSettle();
    expect(find.byType(StudentEditorDialog), findsNothing);
    return (await dialogResult.future)!;
  }

  Future<void> reload(WidgetTester tester, StaffRole role) async {
    await tester.runAsync(() async {
      await store.close();
      store = await CenterStore.open(directory: directory.path);
    });
    await signInAs(tester, role);
  }

  for (final role in [StaffRole.admin, StaffRole.cashier]) {
    testWidgets(
      '${role.name} creates a student with a readonly generated code and returns its persisted ID',
      (tester) async {
        await signInAs(tester, role);
        final expectedCode = store.nextStudentCode;
        await openEditor(tester, dark: role == StaffRole.cashier);
        expect(shownCode(tester), expectedCode);
        if (role == StaffRole.cashier &&
            const bool.fromEnvironment('CAPTURE_UI')) {
          await tester.runAsync(() async {
            final boundary =
                captureKey.currentContext!.findRenderObject()!
                    as RenderRepaintBoundary;
            final image = await boundary.toImage(pixelRatio: 1);
            final bytes = await image.toByteData(
              format: ui.ImageByteFormat.png,
            );
            final target = File(
              'build/verification/student-code-editor-dark-1280.png',
            );
            await target.parent.create(recursive: true);
            await target.writeAsBytes(bytes!.buffer.asUint8List());
            image.dispose();
          });
        }
        final editable = find.descendant(
          of: getCode(),
          matching: find.byType(EditableText),
        );
        expect(tester.widget<EditableText>(editable).readOnly, isTrue);
        await tester.tap(getCode());
        await tester.sendKeyEvent(LogicalKeyboardKey.digit9);
        await tester.pump();
        expect(shownCode(tester), expectedCode);
        await tester.enterText(field('اسم الطالب'), 'طالب ${role.name}');
        await tester.enterText(field('رقم الطالب'), '01012345678');
        final savedId = await save(tester, doubleClick: true);
        expect(store.students, hasLength(1));
        expect(savedId, store.students.single.id);
        expect(store.students.single.code, expectedCode);
        expect(store.students.single.groupIds, [group.id]);
        expect(store.students.single.phone, '01012345678');
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        await reload(tester, role);
        expect(store.students.single.id, savedId);
        expect(store.students.single.code, expectedCode);
        expect(store.students.single.name, 'طالب ${role.name}');
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'cashier edits other fields while the existing student code and ID stay immutable',
    (tester) async {
      late Student original;
      await tester.runAsync(() async {
        original = await store.registerStudent(draft('الاسم القديم'));
      });
      await signInAs(tester, StaffRole.cashier);
      await openEditor(tester, student: original);
      expect(shownCode(tester), original.code);
      expect(
        tester
            .widget<EditableText>(
              find.descendant(
                of: getCode(),
                matching: find.byType(EditableText),
              ),
            )
            .readOnly,
        isTrue,
      );
      await tester.tap(getCode());
      await tester.sendKeyEvent(LogicalKeyboardKey.backspace);
      await tester.sendKeyEvent(LogicalKeyboardKey.digit8);
      await tester.pump();
      expect(shownCode(tester), original.code);
      await tester.enterText(field('اسم الطالب'), 'الاسم الصحيح');
      await tester.enterText(field('رقم ولي الأمر'), '01112345678');
      await tester.ensureVisible(field('ملاحظات الطالب'));
      await tester.enterText(field('ملاحظات الطالب'), 'تم تحديث البيانات');
      final savedId = await save(tester);
      expect(savedId, original.id);
      expect(store.students, hasLength(1));
      expect(store.students.single.code, original.code);
      expect(store.students.single.name, 'الاسم الصحيح');
      expect(store.students.single.guardianPhone, '01112345678');
      expect(store.students.single.notes, 'تم تحديث البيانات');
      await tester.runAsync(() async {
        await expectLater(
          store.saveStudent(store.students.single.copyWith(code: '888888')),
          throwsA(isA<CenterException>()),
        );
      });
      await reload(tester, StaffRole.cashier);
      expect(store.students.single.code, original.code);
      expect(store.students.single.name, 'الاسم الصحيح');
      expect(store.students.single.notes, 'تم تحديث البيانات');
      expect(tester.takeException(), isNull);
    },
  );

  for (final role in [StaffRole.admin, StaffRole.cashier]) {
    testWidgets(
      '${role.name} can edit a student with an exact fractional discount without changing it',
      (tester) async {
        late Student original;
        await tester.runAsync(() async {
          original = await store.registerStudent(
            draft('طالب الخصم الكسري').copyWith(discountPercent: 100 / 7),
          );
        });
        await signInAs(tester, role);
        await openEditor(tester, student: original);
        await tester.enterText(field('اسم الطالب'), 'اسم محدث');
        final savedId = await save(tester);
        expect(savedId, original.id);
        expect(store.students.single.name, 'اسم محدث');
        expect(store.students.single.discountPercent, original.discountPercent);
        expect(store.payments, isEmpty);
        expect(store.attendances, isEmpty);
        await reload(tester, role);
        expect(store.students.single.discountPercent, original.discountPercent);
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets('admin edits the fixed discount using Arabic decimal input', (
    tester,
  ) async {
    late Student original;
    await tester.runAsync(() async {
      original = await store.registerStudent(draft('طالب النسبة'));
    });
    await openEditor(tester, student: original);
    await tester.enterText(field('نسبة الخصم الثابتة'), '٢٥٫٥');
    await save(tester);
    expect(store.students.single.discountPercent, 25.5);
    expect(store.payments, isEmpty);
    expect(store.attendances, isEmpty);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'an open dialog with a stale code preview returns the new atomic code rather than another student ID',
    (tester) async {
      await signInAs(tester, StaffRole.cashier);
      final preview = store.nextStudentCode;
      await openEditor(tester);
      await tester.enterText(field('اسم الطالب'), 'طالب النافذة');
      late Student concurrent;
      await tester.runAsync(() async {
        concurrent = await store.registerStudent(
          draft('طالب الإضافة المتزامنة', code: preview),
        );
      });
      expect(concurrent.code, preview);
      final committedCode = store.nextStudentCode;
      expect(committedCode, isNot(preview));
      await tester.pumpAndSettle();
      expect(shownCode(tester), preview);
      final returnedId = await save(tester, doubleClick: true);
      final saved = store.students.singleWhere(
        (student) => student.id == returnedId,
      );
      expect(store.students, hasLength(2));
      expect(returnedId, isNot(concurrent.id));
      expect(saved.name, 'طالب النافذة');
      expect(saved.code, committedCode);
      expect(
        store.students.map((student) => student.code).toSet(),
        hasLength(2),
      );
      await reload(tester, StaffRole.cashier);
      expect(
        store.students.singleWhere((student) => student.id == returnedId).code,
        committedCode,
      );
      expect(
        store.students
            .singleWhere((student) => student.id == concurrent.id)
            .code,
        preview,
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'canceling a student draft consumes no code and the next creation uses that same preview',
    (tester) async {
      final preview = store.nextStudentCode;
      await openEditor(tester);
      await tester.enterText(field('اسم الطالب'), 'مسودة ملغاة');
      await tester.ensureVisible(find.widgetWithText(TextButton, 'إلغاء'));
      await tester.tap(find.widgetWithText(TextButton, 'إلغاء'));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('student-editor-discard')), findsOneWidget);
      await tester.tap(find.byKey(const Key('student-editor-discard-changes')));
      await tester.pumpAndSettle();
      expect(await dialogResult.future, isNull);
      expect(store.students, isEmpty);
      expect(store.nextStudentCode, preview);
      await openEditor(tester);
      expect(shownCode(tester), preview);
      await tester.enterText(field('اسم الطالب'), 'طالب مسجل');
      final savedId = await save(tester);
      expect(store.students.single.id, savedId);
      expect(store.students.single.code, preview);
      await reload(tester, StaffRole.admin);
      expect(store.students.single.code, preview);
      expect(store.students, hasLength(1));
      expect(tester.takeException(), isNull);
    },
  );
}
