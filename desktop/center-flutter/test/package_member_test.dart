import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/application/session_finance.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  for (final discount in [0, 50, 100]) {
    test(
      'package marker isolates collection at $discount percent and preserves history',
      () async {
        final directory = await Directory.systemTemp.createTemp(
          'center-fee-independent-',
        );
        var store = await CenterStore.open(directory: directory.path);
        addTearDown(() async {
          await store.close();
          await directory.delete(recursive: true);
        });
        await store.setupAdmin('fee-test', 'fee-test-password');
        for (final kind in CatalogKind.values) {
          await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
        }
        await store.saveGroup(
          StudyGroup(
            name: 'Group',
            subjectId: store.catalogs[0].id,
            centerId: store.catalogs[1].id,
            gradeId: store.catalogs[2].id,
            sessionPrice: 6000,
            packagePrice: 21000,
          ),
        );
        final group = store.groups.single;
        await store.saveStudent(
          Student(
            name: 'Student',
            code: 'F1',
            discountPercent: discount,
            groupIds: [group.id],
            createdAt: DateTime.now().subtract(const Duration(days: 1)),
          ),
        );
        final student = store.students.single;
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: 1,
            startsAt: DateTime.now(),
            createdAt: DateTime.now(),
          ),
        );
        final session = store.sessions.single;
        final request = EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        );
        await store.recordAttendance(request);
        expect(store.attendances.single.paymentPending, isTrue);
        await store.setStudentCenterFee(studentId: student.id, enabled: true);
        await store.setStudentPackageMember(
          studentId: student.id,
          enabled: true,
          sessionId: session.id,
        );
        expect(store.students.single.discountPercent, discount);
        expect(store.students.single.centerFeeEnabled, isFalse);
        expect(store.attendances.single.packageMember, isTrue);
        expect(store.attendances.single.paymentPending, isFalse);
        expect(store.attendanceNeedsPayment(student.id, session.id), isFalse);
        await store.recordAttendance(request);
        expect(store.attendances, hasLength(1));
        await expectLater(
          store.collectAndAttend(request),
          throwsA(isA<CenterException>()),
        );
        await expectLater(
          store.collectStudentCenterFee(
            studentId: student.id,
            sessionId: session.id,
          ),
          throwsA(isA<CenterException>()),
        );
        await expectLater(
          store.setStudentCenterFee(studentId: student.id, enabled: true),
          throwsA(isA<CenterException>()),
        );
        expect(store.payments, isEmpty);
        expect(store.centerFees, isEmpty);
        final summary = buildSessionFinancialSummary(
          session: session,
          attendances: store.attendances,
          payments: store.payments,
        );
        expect(
          summary.studentCategories!
              .singleWhere(
                (c) => c.kind == SessionStudentCategoryKind.packageMember,
              )
              .studentCount,
          1,
        );
        expect(
          summary.studentCategories!.where(
            (c) => c.kind == SessionStudentCategoryKind.unpaid,
          ),
          isEmpty,
        );
        expect(summary.freeCount, 0);
        expect(summary.totalCollected, 0);
        expect(
          SessionFinancialSummary.fromJson(summary.toJson()).studentCategories!
              .singleWhere(
                (c) => c.kind == SessionStudentCategoryKind.packageMember,
              )
              .studentCount,
          1,
        );
        await store.setStudentPackageMember(
          studentId: student.id,
          enabled: false,
          sessionId: session.id,
        );
        expect(store.attendances.single.paymentPending, isTrue);
        await store.setStudentPackageMember(
          studentId: student.id,
          enabled: true,
          sessionId: session.id,
        );
        await store.closeSession(session.id);
        await store.finalizeSession(sessionId: session.id, actualCash: 0);
        await store.setStudentPackageMember(
          studentId: student.id,
          enabled: false,
        );
        expect(store.attendances.single.packageMember, isTrue);
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.signIn('fee-test', 'fee-test-password');
        expect(store.attendances.single.packageMember, isTrue);
        expect(store.students.single.discountPercent, discount);
        await store.saveStaff(
          name: 'cashier',
          password: 'cashier-test-password',
          role: StaffRole.cashier,
        );
        final cashier = store.staff.firstWhere(
          (s) => s.role == StaffRole.cashier,
        );
        await store.commandLan(
          deviceId: 'test-secondary',
          staffId: cashier.id,
          request: {
            'requestId': '4c296a35-f45e-4d27-bab7-ae9f3e14c5be',
            'operation': 'setStudentPackageMember',
            'arguments': {'studentId': student.id, 'enabled': true},
          },
        );
        await store.saveSession(
          LessonSession(
            groupId: group.id,
            number: 2,
            startsAt: DateTime.now(),
            createdAt: DateTime.now(),
          ),
        );
        final nextSession = store.sessions.firstWhere((s) => s.number == 2);
        await store.signIn('cashier', 'cashier-test-password');
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: nextSession.id,
            mode: EntryMode.single,
          ),
        );
        expect(
          store.attendances
              .where((a) => a.sessionId == nextSession.id)
              .single
              .packageMember,
          isTrue,
        );
        expect(store.payments, isEmpty);
        expect(store.centerFees, isEmpty);
      },
    );
  }
}
