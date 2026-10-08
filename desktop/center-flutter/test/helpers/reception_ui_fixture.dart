import 'dart:convert';
import 'dart:io';

import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

/// Synthetic records restored only into the caller's temporary SQLite store.
Future<void> seedReceptionUi(
  CenterStore store,
  Directory directory, {
  required int studentCount,
  required int sessionCount,
}) async {
  await store.setupAdmin('ui-admin', 'test-password-2026');
  final backup =
      jsonDecode(await File(await store.createBackup()).readAsString())
          as Map<String, dynamic>;
  final snapshot = backup['data'] as Map<String, dynamic>;
  final at = DateTime(2026, 9, 1, 10);
  final staffId = store.currentUser!.id;
  snapshot['catalogs'] = [
    for (final kind in CatalogKind.values)
      CatalogEntry(id: kind.name, name: kind.name, kind: kind).toJson(),
  ];
  snapshot['groups'] = [
    const StudyGroup(
      id: 'group',
      name: 'مجموعة الاختبار',
      subjectId: 'subject',
      centerId: 'center',
      gradeId: 'grade',
      sessionPrice: 10000,
      packagePrice: 40000,
    ).toJson(),
  ];
  snapshot['students'] = [
    for (var student = 0; student < studentCount; student++)
      Student(
        id: 'student-$student',
        name: 'طالب اختبار ${student.toString().padLeft(4, '0')}',
        code: '${10000 + student}',
        groupIds: const ['group'],
        createdAt: at.subtract(const Duration(days: 1)),
      ).toJson(),
  ];
  snapshot['sessions'] = [
    for (var session = 0; session < sessionCount; session++)
      LessonSession(
        id: 'session-$session',
        groupId: 'group',
        number: session + 1,
        startsAt: at.add(Duration(days: session)),
        createdAt: at,
      ).toJson(),
  ];
  snapshot['attendances'] = [
    for (var student = 0; student < studentCount; student++)
      for (var session = 0; session < sessionCount; session++)
        AttendanceRecord(
          id: 'attendance-$student-$session',
          studentId: 'student-$student',
          sessionId: 'session-$session',
          status: AttendanceStatus.present,
          fixedDiscountPercent: 0,
          recordedAt: at.add(Duration(days: session)),
        ).toJson(),
  ];
  snapshot['payments'] = [
    for (var student = 0; student < studentCount; student++)
      for (var session = 0; session < sessionCount; session++)
        PaymentRecord(
          id: 'payment-$student-$session',
          studentId: 'student-$student',
          groupId: 'group',
          sessionId: 'session-$session',
          description: 'حصة اختبار ${session + 1}',
          baseAmount: 10000,
          discountPercent: 0,
          netAmount: 10000,
          staffId: staffId,
          createdAt: at.add(Duration(days: session)),
        ).toJson(),
  ];
  snapshot['paymentChecks'] = [
    for (var student = 0; student < studentCount; student++)
      PaymentCheck(
        id: 'check-$student',
        studentId: 'student-$student',
        sessionId: 'session-0',
        status: StudentPaymentStatus.paidSingle,
        paymentId: 'payment-$student-0',
        staffId: staffId,
        checkedAt: at.add(Duration(minutes: student)),
        amount: 10000,
      ).toJson(),
  ];
  snapshot['enrollments'] = {
    for (var student = 0; student < studentCount; student++)
      'student-$student:group': at
          .subtract(const Duration(days: 1))
          .toIso8601String(),
  };
  final source = File('${directory.path}/synthetic-ui.json');
  await source.writeAsString(jsonEncode(backup));
  await store.restoreBackup(source.path);
  await store.signIn('ui-admin', 'test-password-2026');
}
