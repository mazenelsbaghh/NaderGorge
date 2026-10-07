import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/management/closings_page.dart';
import 'package:massar_center/shared/formatters.dart';
import 'package:massar_center/shared/theme.dart';

void main() {
  testWidgets(
    'closing tables show zero and fractional discounts, actual collections and distinct students through restart',
    (tester) async {
      late Directory directory;
      late CenterStore store;
      late StudyGroup group;
      late LessonSession session;
      late Student fullPrice;
      late Student centerOnly;
      final captureKey = GlobalKey();
      const password = 'closing-table-test-password';

      await tester.runAsync(() async {
        await initializeDateFormatting('ar_EG');
        directory = await Directory.systemTemp.createTemp('closing-table-');
        store = await CenterStore.open(directory: directory.path);
        await store.setupAdmin('manager', password);
        for (final kind in CatalogKind.values) {
          await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
        }
        await store.saveGroup(
          StudyGroup(
            name: 'مجموعة التقفيلة',
            subjectId: store.catalogs[0].id,
            centerId: store.catalogs[1].id,
            gradeId: store.catalogs[2].id,
            sessionPrice: 10000,
            packagePrice: 40000,
          ),
        );
        group = store.groups.single;
        final month = await store.saveStudyMonth(
          StudyMonth(
            name: 'شهر الاختبار',
            lessons: [const PreparedLesson(number: 1)],
          ),
        );
        session = await store.startPreparedLesson(
          groupId: group.id,
          preparedLessonId: month.lessons.single.id,
        );

        Future<Student> add(num discount) async {
          final index = store.students.length;
          await store.saveStudent(
            Student(
              name: 'طالب $index',
              code: 'D$index',
              discountPercent: discount,
              groupIds: [group.id],
              createdAt: DateTime(2026),
            ),
          );
          return store.students.last;
        }

        Future<void> collect(Student student, {int? paid}) =>
            store.collectAndAttend(
              EntryRequest(
                studentId: student.id,
                sessionId: session.id,
                mode: EntryMode.single,
                paidAmount: paid,
              ),
            );

        fullPrice = await add(0);
        await collect(fullPrice);
        await collect(await add(0));
        await collect(await add(25), paid: 3000);
        await collect(await add(50), paid: 3000);
        await collect(await add(25.5));
        await collect(await add(0), paid: 0);
        await collect(await add(100));
        final refunded = await add(75);
        await collect(refunded);
        await store.cancelPayment(
          paymentId: store.payments.last.id,
          reason: 'إيصال خاطئ',
        );
        final buyer = await add(0);
        for (var count = 0; count < 2; count++) {
          await store.renewPackage(
            PackageRequest(
              studentId: buyer.id,
              groupId: group.id,
              sessionId: session.id,
            ),
          );
        }
        Future<Student> addCenterOnly(int amount, {int? paid}) async {
          final student = await add(0);
          await store.saveStudentCenterOnly(
            studentId: student.id,
            enabled: true,
            amount: amount,
          );
          await collect(student, paid: paid);
          return student;
        }

        centerOnly = await addCenterOnly(1500);
        await addCenterOnly(1500);
        final partialFee = await addCenterOnly(2000, paid: 1000);
        await collect(partialFee, paid: 1000);
        await addCenterOnly(2500, paid: 0);
        await store.setStudentCenterFee(studentId: fullPrice.id, enabled: true);
        await store.collectStudentCenterFee(
          studentId: fullPrice.id,
          sessionId: session.id,
        );
        await store.closeSession(session.id);
      });

      addTearDown(() async {
        await tester.binding.setSurfaceSize(null);
        await tester.runAsync(() async {
          await store.close();
          await directory.delete(recursive: true);
        });
      });

      String? cell(String key) =>
          tester.widget<Text>(find.byKey(Key(key))).data;

      await (FontLoader('Tajawal')
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Regular.ttf'))
            ..addFont(rootBundle.load('assets/fonts/Tajawal-Bold.ttf')))
          .load();

      Future<void> open({
        required double width,
        String captureName = 'mixed',
      }) async {
        await tester.binding.setSurfaceSize(Size(width, 960));
        await tester.pumpWidget(
          RepaintBoundary(
            key: captureKey,
            child: MaterialApp(
              theme: MassarTheme.light,
              home: Directionality(
                textDirection: TextDirection.rtl,
                child: Scaffold(
                  body: ClosingsPage(
                    store: store,
                    initialSessionId: session.id,
                  ),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('الخصم والمبلغ المدفوع'));
        await tester.pumpAndSettle();
        if (const bool.fromEnvironment('CAPTURE_UI')) {
          await tester.runAsync(() async {
            final boundary =
                captureKey.currentContext!.findRenderObject()
                    as RenderRepaintBoundary;
            final image = await boundary.toImage();
            final bytes = await image.toByteData(
              format: ui.ImageByteFormat.png,
            );
            final file = File(
              '../../artifacts/private/closing-discount-table/table-$captureName-${width.toInt()}.png',
            );
            await file.parent.create(recursive: true);
            await file.writeAsBytes(bytes!.buffer.asUint8List());
            image.dispose();
          });
        }
      }

      void verifyTable() {
        expect(find.text('الخصم والمبلغ المدفوع'), findsOneWidget);
        expect(cell('category-discount-single-0-10000'), '0٪ · بدون خصم');
        expect(cell('category-students-single-0-10000'), '2');
        expect(cell('category-total-single-0-10000'), money(20000));
        expect(cell('category-discount-single-25.5-7450'), '25.5٪');
        expect(cell('category-amount-single-25-3000'), money(3000));
        expect(cell('category-amount-single-50-3000'), money(3000));
        expect(cell('payment-amount-students-single-3000'), '2');
        expect(cell('category-amount-single-0-0'), 'بدون تحصيل (${money(0)})');
        expect(cell('category-students-single-0-0'), '1');
        expect(cell('category-total-single-0-0'), money(0));
        expect(cell('category-students-package-0-40000'), '1');
        expect(cell('category-operations-package-0-40000'), '2');
        expect(cell('category-total-package-0-40000'), money(80000));
        expect(
          find.byKey(const Key('attendance-discount-100')),
          findsOneWidget,
        );
        expect(cell('attendance-discount-students-100'), '5');
        expect(cell('attendance-discount-students-0'), '3');
        expect(cell('center-fee-amount-only-1500'), money(1500));
        expect(cell('center-fee-students-only-1500'), '2');
        expect(cell('center-fee-total-only-1500'), money(3000));
        expect(cell('center-fee-students-only-1000'), '1');
        expect(cell('center-fee-operations-only-1000'), '2');
        expect(cell('center-fee-total-only-1000'), money(2000));
        expect(cell('center-fee-students-independent-1500'), '1');
        expect(cell('payment-amount-center-fee-students-only-1500'), '2');
        expect(find.byKey(const Key('center-fee-amount-only-0')), findsNothing);
        expect(
          find.byKey(const Key('category-amount-single-75-2500')),
          findsNothing,
        );
        expect(
          find.byKey(const Key('category-amount-single-100-0')),
          findsNothing,
        );
        expect(tester.takeException(), isNull);
      }

      await open(width: 1440);
      verifyTable();
      final live = store.sessionFinancialSummary(session.id);
      expect(live.totalCollected, 119950);
      expect(live.centerFeeCollected, 6500);
      await tester.pumpWidget(const SizedBox.shrink());

      // A saved closing must keep its original prices, rates and counts even
      // after profiles change, the app restarts, and a narrower screen opens it.
      await tester.runAsync(() async {
        await store.finalizeSession(sessionId: session.id, actualCash: 119950);
        final snapshot = jsonEncode(store.closings.single.summary.toJson());
        await store.saveStudentDiscount(studentId: fullPrice.id, percent: 60);
        await store.saveGroup(group.copyWith(sessionPrice: 20000));
        await store.saveStudentCenterOnly(
          studentId: centerOnly.id,
          enabled: false,
        );
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.signIn('manager', password);
        expect(jsonEncode(store.closings.single.summary.toJson()), snapshot);
      });
      await open(width: 960);
      verifyTable();
      await tester.pumpWidget(const SizedBox.shrink());

      // Center fee receipts must appear even when there are no teacher sales.
      await tester.runAsync(() async {
        await store.saveStudentCenterOnly(
          studentId: centerOnly.id,
          enabled: true,
          amount: 1500,
        );
        final month = await store.saveStudyMonth(
          StudyMonth(
            name: 'شهر السنتر فقط',
            lessons: [const PreparedLesson(number: 1)],
          ),
        );
        session = await store.startPreparedLesson(
          groupId: group.id,
          preparedLessonId: month.lessons.single.id,
        );
        await store.collectAndAttend(
          EntryRequest(
            studentId: centerOnly.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.closeSession(session.id);
        await store.finalizeSession(sessionId: session.id, actualCash: 1500);
      });
      await open(width: 960, captureName: 'center-only');
      expect(find.text('الخصم والمبلغ المدفوع'), findsOneWidget);
      expect(cell('center-fee-students-only-1500'), '1');
      expect(cell('center-fee-total-only-1500'), money(1500));
      expect(cell('payment-amount-center-fee-students-only-1500'), '1');
      expect(
        find.byKey(const Key('category-amount-single-0-10000')),
        findsNothing,
      );
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox.shrink());
    },
  );
}
