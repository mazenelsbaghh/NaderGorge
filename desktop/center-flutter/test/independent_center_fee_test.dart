import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/application/session_finance.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  for (final discount in [0, 50, 100]) {
    test(
      'independent center fee preserves $discount percent discount and attendance after reopen',
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
        await store.setStudentCenterFee(studentId: student.id, enabled: true);
        expect(store.students.single.discountPercent, discount);
        expect(store.centerFees, isEmpty);
        await expectLater(
          store.collectStudentCenterFee(
            studentId: student.id,
            sessionId: session.id,
          ),
          throwsA(isA<CenterException>()),
        );
        await store.recordAttendance(
          EntryRequest(
            studentId: student.id,
            sessionId: session.id,
            mode: EntryMode.single,
          ),
        );
        await store.collectStudentCenterFee(
          studentId: student.id,
          sessionId: session.id,
        );
        expect(store.centerFeeCollectedFor(student.id, session.id), 1500);
        expect(store.payments, isEmpty);
        expect(store.attendances, hasLength(1));
        await expectLater(
          store.collectStudentCenterFee(
            studentId: student.id,
            sessionId: session.id,
          ),
          throwsA(isA<CenterException>()),
        );
        final summary = buildSessionFinancialSummary(
          session: session,
          attendances: store.attendances,
          payments: store.payments,
          centerFees: store.centerFees,
        );
        expect(summary.centerFeeCollected, 1500);
        await store.close();
        store = await CenterStore.open(directory: directory.path);
        await store.signIn('fee-test', 'fee-test-password');
        expect(store.students.single.centerFeeEnabled, isTrue);
        expect(store.students.single.discountPercent, discount);
        await store.setStudentCenterFee(studentId: student.id, enabled: false);
        expect(store.centerFeeCollectedFor(student.id, session.id), 1500);
        expect(store.students.single.discountPercent, discount);
      },
    );
  }
}
