import 'dart:async';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/discount_calculation.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/student_discount_dialog.dart';
import 'package:massar_center/shared/theme.dart';


void main() {
  late Directory directory;
  late CenterStore store;
  late Student student;
  late LessonSession session;
  bool? saved;
  final boundary = GlobalKey();
  final callerFocus = FocusNode();
  final amount = find.byKey(const Key('discount-amount'));
  final percent = find.byKey(const Key('discount-percent'));
  final save = find.byKey(const Key('save-student-discount'));

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      saved = null;
      directory = await Directory.systemTemp.createTemp(
        'massar-discount-dialog-',
      );
      store = await CenterStore.open(directory: directory.path);
      await store.setupAdmin('الإدارة', 'discount-dialog-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة الخصم',
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
        ),
      );
      await store.saveStudent(
        Student(
          code: '501',
          name: 'يوسف صاحب الخصم',
          discountPercent: 25,
          notes: 'ملاحظة أصلية',
          groupIds: [store.groups.single.id],
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
      student = store.students.single;
      await store.saveSession(
        LessonSession(
          groupId: store.groups.single.id,
          number: 1,
          startsAt: DateTime.now().add(const Duration(minutes: 5)),
          createdAt: DateTime.now(),
        ),
      );
      session = store.sessions.single;
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
    }),
  );

  tearDownAll(callerFocus.dispose);

  tearDown(
    () => TestWidgetsFlutterBinding.instance.runAsync(() async {
      await store.close();
      await directory.delete(recursive: true);
    }),
  );

  Future<void> open(
    WidgetTester tester, {
    int base = 12345,
    bool dark = true,
    double width = 1280,
    double scale = 1,
    List<DiscountPriceOption> options = const [],
    String? initial,
  }) async {
    await tester.binding.setSurfaceSize(Size(width, 800));
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
      MaterialApp(
        theme: dark ? MassarTheme.dark : MassarTheme.light,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(
            context,
          ).copyWith(textScaler: TextScaler.linear(scale)),
          child: Directionality(
            textDirection: TextDirection.rtl,
            child: RepaintBoundary(key: boundary, child: child!),
          ),
        ),
        home: Scaffold(
          body: Builder(
            builder: (context) => Column(
              children: [
                TextField(
                  key: const Key('caller-code'),
                  focusNode: callerFocus,
                ),
                FilledButton(
                  key: const Key('open-discount'),
                  onPressed: () async {
                    saved = await showDialog<bool>(
                      context: context,
                      builder: (_) => StudentDiscountDialog(
                        store: store,
                        studentId: student.id,
                        baseAmount: base,
                        priceLabel: 'مرجع السعر المحدد',
                        priceOptions: options,
                        initialPriceId: initial,
                      ),
                    );
                    callerFocus.requestFocus();
                  },
                  child: const Text('فتح الخصم'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.runAsync(
      () => tester.tap(find.byKey(const Key('open-discount'))),
    );
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('student-discount-dialog')), findsOneWidget);
  }

  Future<void> enter(WidgetTester tester, Finder field, String text) async {
    await tester.enterText(field, text);
    await tester.pump();
  }

  Future<void> changePrice(WidgetTester tester, String label) async {
    await tester.runAsync(
      () => tester.tap(
        find.widgetWithText(
          DropdownButtonFormField<String>,
          'مرجع حساب الخصم — النسبة ثابتة لكل الدفع',
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.runAsync(() => tester.tap(find.text(label).last));
    await tester.pumpAndSettle();
  }

  Future<void> saveMutation(
    WidgetTester tester,
    Future<void> Function() gesture,
    num expected,
  ) async {
    await tester.runAsync(() async {
      final completed = Completer<void>();
      void changed() {
        if (store.students.single.discountPercent == expected &&
            !completed.isCompleted) {
          completed.complete();
        }
      }

      store.addListener(changed);
      try {
        await gesture();
        await completed.future.timeout(const Duration(seconds: 5));
        await Future<void>(() {});
        await tester.pump();
      } finally {
        store.removeListener(changed);
      }
    });
    await tester.pumpAndSettle();
    expect(saved, isTrue);
    expect(find.byKey(const Key('student-discount-dialog')), findsNothing);
    expect(callerFocus.hasFocus, isTrue);
    expect(store.payments, hasLength(1));
    expect(store.payments.single.netAmount, 7500);
    expect(store.payments.single.discountPercent, 25);
    expect(store.attendances, hasLength(1));
    expect(store.packages, isEmpty);
    expect(store.cardPayments, isEmpty);
  }

  Future<void> capture(WidgetTester tester, String name) async {
    if (!const bool.fromEnvironment('CAPTURE_UI')) return;
    await tester.runAsync(() async {
      final render =
          boundary.currentContext!.findRenderObject() as RenderRepaintBoundary;
      final image = await render.toImage();
      final png = await image.toByteData(format: ui.ImageByteFormat.png);
      await Directory('build/verification').create(recursive: true);
      await File(
        'build/verification/$name.png',
      ).writeAsBytes(png!.buffer.asUint8List());
      image.dispose();
    });
  }

  testWidgets(
    'fractional Arabic percent saves only after fresh Enter key-up and preserves old payment snapshot',
    (tester) async {
      await open(tester);
      expect(tester.widget<TextField>(amount).focusNode!.hasFocus, isTrue);
      expect(
        tester.widget<TextField>(amount).controller!.selection,
        const TextSelection(baseOffset: 0, extentOffset: 5),
      );
      await enter(tester, percent, '٢٥٫٥');
      expect(tester.widget<TextField>(amount).controller!.text, '91.97');
      await tester.sendKeyDownEvent(LogicalKeyboardKey.enter);
      await tester.sendKeyRepeatEvent(LogicalKeyboardKey.enter);
      await tester.pump();
      expect(store.students.single.discountPercent, 25);
      expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
      await tester.sendKeyUpEvent(LogicalKeyboardKey.enter);
      await tester.pump();
      expect(store.students.single.discountPercent, 25);
      await saveMutation(
        tester,
        () => tester.sendKeyEvent(LogicalKeyboardKey.enter),
        25.5,
      );
      await tester.runAsync(() => store.close());
      store = (await tester.runAsync(
        () => CenterStore.open(directory: directory.path),
      ))!;
      expect(store.students.single.discountPercent, 25.5);
      expect(store.payments.single.discountPercent, 25);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'amount-derived percent retains full precision across price choices and saves latest student notes',
    (tester) async {
      await open(
        tester,
        base: 15000,
        initial: 'single',
        options: const [
          DiscountPriceOption(id: 'single', label: 'الحصة', baseAmount: 15000),
          DiscountPriceOption(
            id: 'three',
            label: 'باقة ٣ حصص',
            baseAmount: 27000,
          ),
        ],
      );
      await enter(tester, amount, '۱۰۰٫۰۰');
      final exact = discountPercentForAmount(15000, 10000);
      expect(
        exact,
        isNot(num.parse(tester.widget<TextField>(percent).controller!.text)),
      );
      expect(tester.widget<TextField>(percent).decoration!.prefixText, '≈ ');
      await changePrice(tester, 'باقة ٣ حصص');
      expect(tester.widget<TextField>(amount).controller!.text, '180.00');
      await tester.runAsync(
        () => store.saveStudentNote(
          studentId: student.id,
          notes: 'أحدث ملاحظة أثناء الحوار',
        ),
      );
      await saveMutation(tester, () async {
        await tester.tap(save);
        await tester.tap(save);
      }, exact);
      expect(store.students.single.notes, 'أحدث ملاحظة أثناء الحوار');
      expect(
        discountedAmount(15000, store.students.single.discountPercent),
        10000,
      );
      expect(
        discountedAmount(27000, store.students.single.discountPercent),
        18000,
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'invalid amounts and percentages never save and Escape cancels draft without payment changes',
    (tester) async {
      await open(tester, base: 10000);
      for (final invalid in ['١٠٠٫٠١', '-1', 'NaN', '50.001', '']) {
        await enter(tester, amount, invalid);
        expect(tester.widget<FilledButton>(save).onPressed, isNull);
        expect(store.students.single.discountPercent, 25);
      }
      for (final invalid in ['١٠٠٫١', '-1', 'NaN', 'Infinity', '']) {
        await enter(tester, percent, invalid);
        expect(tester.widget<FilledButton>(save).onPressed, isNull);
        expect(store.students.single.discountPercent, 25);
      }
      await enter(tester, percent, '٣٣٫٣');
      expect(tester.widget<FilledButton>(save).onPressed, isNotNull);
      await tester.runAsync(
        () => tester.sendKeyEvent(LogicalKeyboardKey.escape),
      );
      await tester.pumpAndSettle();
      expect(saved, isFalse);
      expect(callerFocus.hasFocus, isTrue);
      expect(store.students.single.discountPercent, 25);
      expect(store.payments.single.netAmount, 7500);
      expect(store.attendances, hasLength(1));
      expect(tester.takeException(), isNull);
    },
  );

  for (final target in [0, 100]) {
    testWidgets(
      'zero-priced reference permits $target percent without amount-derived division',
      (tester) async {
        await open(tester, base: 0);
        expect(tester.widget<TextField>(amount).enabled, isFalse);
        expect(tester.widget<TextField>(amount).controller!.text, '0.00');
        expect(
          find.text(
            'السعر صفر؛ لا يمكن حساب نسبة من مبلغ. يمكنك تعديل النسبة مباشرة.',
          ),
          findsOneWidget,
        );
        await enter(tester, percent, '$target');
        await saveMutation(tester, () => tester.tap(save), target);
        expect(store.students.single.discountPercent, target);
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets(
    'signout disables discount submission and retains the draft for authorized retry',
    (tester) async {
      await open(tester);
      await enter(tester, percent, '30.5');
      await tester.runAsync(() async {
        store.signOut();
      });
      await tester.pump();
      expect(tester.widget<FilledButton>(save).onPressed, isNull);
      await tester.pumpAndSettle();
      expect(tester.widget<TextField>(percent).controller!.text, '30.5');
      expect(store.students.single.discountPercent, 25);
      expect(store.payments, hasLength(1));
      await tester.runAsync(() async {
        store.signOut();
        await store.signIn('الإدارة', 'discount-dialog-password');
      });
      await tester.pump();
      await enter(tester, percent, '30.5');
      await saveMutation(tester, () => tester.tap(save), 30.5);
      expect(tester.takeException(), isNull);
    },
  );

  for (final (dark, width, scale, name) in [
    (true, 1280.0, 1.0, 'student-discount-dark1280'),
    (false, 960.0, 2.0, 'student-discount-light960-text200'),
  ]) {
    testWidgets('discount editing remains readable in $name', (tester) async {
      await tester.runAsync(
        () => store.saveStudent(
          student.copyWith(
            name: List.filled(8, 'يوسف صاحب الاسم الطويل').join(' '),
          ),
        ),
      );
      student = store.students.single;
      await open(
        tester,
        dark: dark,
        width: width,
        scale: scale,
        options: const [
          DiscountPriceOption(
            id: 'single',
            label: 'سعر الحصة داخل المجموعة',
            baseAmount: 12345,
          ),
          DiscountPriceOption(
            id: 'three',
            label: 'سعر باقة ٣ حصص داخل المجموعة',
            baseAmount: 27000,
          ),
        ],
      );
      await tester.ensureVisible(amount);
      await enter(tester, amount, '٩١٫٩٧');
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.ensureVisible(find.text('كود الطالب: 501'));
      final codeBounds = tester.getRect(find.text('كود الطالب: 501'));
      expect(codeBounds.top, greaterThanOrEqualTo(0));
      expect(codeBounds.bottom, lessThanOrEqualTo(800));
      await tester.ensureVisible(
        find.text('مرجع حساب النسبة: سعر الحصة داخل المجموعة'),
      );
      final priceBounds = tester.getRect(
        find.text('مرجع حساب النسبة: سعر الحصة داخل المجموعة'),
      );
      expect(priceBounds.bottom, lessThanOrEqualTo(800));
      await tester.ensureVisible(save);
      final bounds = tester.getRect(save);
      expect(bounds.top, greaterThanOrEqualTo(0));
      expect(bounds.bottom, lessThanOrEqualTo(800));
      expect(bounds.right, lessThanOrEqualTo(width));
      expect(tester.widget<FilledButton>(save).onPressed, isNotNull);
      await capture(tester, name);
      await saveMutation(
        tester,
        () => tester.tap(save),
        discountPercentForAmount(12345, 9197),
      );
      expect(tester.takeException(), isNull);
    });
  }
}
