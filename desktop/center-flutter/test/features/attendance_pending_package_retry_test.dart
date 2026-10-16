import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:massar_center/features/attendance/attendance_workspace.dart';
import 'package:massar_center/shared/theme.dart';

import '../helpers/attendance_ui_helpers.dart';
import '../helpers/notice_helpers.dart';

void main() {
  late Directory fixture;
  late CenterStore store;
  late StudyGroup originalGroup, visitedGroup;
  late Student student;

  setUp(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await initializeDateFormatting('ar_EG');
      fixture = await Directory.systemTemp.createTemp(
        'massar-pending-coverage-',
      );
      store = await CenterStore.open(directory: fixture.path);
      await store.setupAdmin('manager', 'test-manager-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(kind: kind, name: kind.name));
      }
      for (final name in ['الأصلية', 'التعويض']) {
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
            sessionPrice: 6000,
            packagePrice: 21000,
          ),
        );
      }
      await seedAttendanceMonths(store, fourPrice: 21000);
      originalGroup = store.groups.firstWhere((e) => e.name == 'الأصلية');
      visitedGroup = store.groups.firstWhere((e) => e.name == 'التعويض');
      await store.saveStudent(
        Student(
          name: 'طالب تجربة',
          code: 'P123',
          groupIds: [originalGroup.id],
          createdAt: DateTime.now().subtract(const Duration(days: 10)),
        ),
      );
      student = store.students.single;
    }),
  );

  tearDown(
    () => TestWidgetsFlutterBinding.ensureInitialized().runAsync(() async {
      await store.close();
      await fixture.delete(recursive: true);
    }),
  );

  Future<LessonSession> targetSession(String groupId) async {
    await store.saveSession(
      LessonSession(
        groupId: groupId,
        number: 2,
        startsAt: DateTime.now().add(const Duration(hours: 1)),
        createdAt: DateTime.now(),
      ),
    );
    final session = store.sessions.last;
    await store.startSession(session.id);
    return session;
  }

  Future<void> mount(WidgetTester tester, LessonSession session) async {
    await tester.binding.setSurfaceSize(const Size(1440, 960));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        theme: MassarTheme.light,
        home: Directionality(
          textDirection: TextDirection.rtl,
          child: AttendanceWorkspace(
            store: store,
            initialSessionId: session.id,
            onExit: () {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> scan(WidgetTester tester, String code) async {
    final field = find.byKey(const Key('student-search'));
    await tester.enterText(field, code);
    final submit =
        tester.widget<TextField>(field).onSubmitted!
            as Future<void> Function(String);
    await submit(code);
    await tester.pumpAndSettle();
  }

  for (final inOriginalGroup in [true, false]) {
    testWidgets(
      'Enter retries prepaid pending attendance in ${inOriginalGroup ? 'original' : 'makeup source'} group without charging or duplicating presence',
      (tester) async {
        await tester.runAsync(() async {
          await store.renewPackage(
            PackageRequest(studentId: student.id, groupId: originalGroup.id),
          );
          final payment = store.payments.single.toJson();
          final package = store.packages.single;
          final session = await targetSession(
            inOriginalGroup ? originalGroup.id : visitedGroup.id,
          );
          await store.recordAttendance(
            EntryRequest(
              studentId: student.id,
              sessionId: session.id,
              mode: inOriginalGroup ? EntryMode.package : EntryMode.makeup,
              makeupSourceGroupId: inOriginalGroup ? null : originalGroup.id,
            ),
          );
          final pending = store.attendances.single;
          expect(pending.paymentPending, isTrue);
          expect(pending.packageId, isNull);
          await mount(tester, session);
          await scan(tester, student.code);
          expect(store.attendanceNeedsPayment(student.id, session.id), isFalse);
          expect(store.attendances, hasLength(1));
          expect(store.attendances.single.packageId, package.id);
          expect(store.attendances.single.recordedAt, pending.recordedAt);
          expect(
            store.attendances.single.makeupSourceGroupId,
            inOriginalGroup ? null : originalGroup.id,
          );
          expect(store.packages.single.remaining, 3);
          expect(store.payments.single.toJson(), payment);
          expect(store.allPayments, hasLength(1));
          expect(store.refunds, isEmpty);
          expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
          expect(tester.takeException(), isNull);
        });
      },
    );
  }

  for (final pending in [false, true]) {
    testWidgets(
      'Enter ${pending ? 'retries pending' : 'records fresh'} coverage from existing credits while a named five-session month is selected',
      (tester) async {
        await tester.runAsync(() async {
          await store.renewPackage(
            PackageRequest(studentId: student.id, groupId: originalGroup.id),
          );
          final payment = store.payments.single.toJson();
          final originalPackage = store.packages.single;
          final month = await store.saveStudyMonth(
            StudyMonth(
              name: 'شهر خمس حصص',
              price: 25000,
              lessons: List.generate(
                5,
                (index) => PreparedLesson(number: index + 1),
              ),
            ),
          );
          final session = await store.startPreparedLesson(
            groupId: originalGroup.id,
            preparedLessonId: month.lessons[1].id,
          );
          if (pending) {
            await store.recordAttendance(
              EntryRequest(
                studentId: student.id,
                sessionId: session.id,
                mode: EntryMode.package,
                monthPlanId: month.id,
                packageSessions: 5,
              ),
            );
            expect(store.attendances.single.paymentPending, isTrue);
          }
          await mount(tester, session);
          await scan(tester, student.code);
          expect(store.attendanceNeedsPayment(student.id, session.id), isFalse);
          expect(store.attendances, hasLength(1));
          expect(store.attendances.single.packageId, originalPackage.id);
          expect(store.packages, hasLength(1));
          expect(store.packages.single.totalSessions, 4);
          expect(store.packages.single.remaining, 3);
          expect(store.payments.single.toJson(), payment);
          expect(store.allPayments, hasLength(1));
          expect(store.refunds, isEmpty);
          expect(find.byKey(const Key('massar-notice-dialog')), findsNothing);
          expect(tester.takeException(), isNull);
        });
      },
    );
  }

  testWidgets(
    'Enter keeps genuinely unpaid pending attendance behind the payment warning without buying a month',
    (tester) async {
      await tester.runAsync(() async {
        final session = await targetSession(originalGroup.id);
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        final pending = store.attendances.single.toJson();
        await mount(tester, session);
        final field = find.byKey(const Key('student-search'));
        await tester.enterText(field, student.code);
        final submit =
            tester.widget<TextField>(field).onSubmitted!
                as Future<void> Function(String);
        final submitted = submit(student.code);
        await acknowledgeNotice(
          tester,
          message:
              'حضور الطالب مسجل بالفعل — غير مدفوع. استخدم L للحصة أو N للشهر لتسديده؛ لن نكرر الحضور.',
        );
        await submitted;
        await tester.pumpAndSettle();
        expect(store.attendances.single.toJson(), pending);
        expect(store.attendanceNeedsPayment(student.id, session.id), isTrue);
        expect(store.packages, isEmpty);
        expect(store.payments, isEmpty);
        expect(store.corrections, isEmpty);
        expect(tester.takeException(), isNull);
      });
    },
  );
}
