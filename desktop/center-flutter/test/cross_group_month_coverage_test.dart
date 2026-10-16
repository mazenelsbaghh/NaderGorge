import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup originalGroup, visitedGroup, unrelatedGroup;
  late Student student;
  late DateTime now;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp(
      'massar-cross-group-month-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('manager', 'test-manager-123');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    for (final name in ['الأصلية', 'مجموعة التعويض', 'مجموعة مستقلة']) {
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
    originalGroup = store.groups.firstWhere((e) => e.name == 'الأصلية');
    visitedGroup = store.groups.firstWhere((e) => e.name == 'مجموعة التعويض');
    unrelatedGroup = store.groups.firstWhere((e) => e.name == 'مجموعة مستقلة');
    now = DateTime.now();
    await store.saveStudent(
      Student(
        name: 'طالبة التجربة',
        code: 'CROSS-1',
        groupIds: [originalGroup.id],
        createdAt: now.subtract(const Duration(days: 10)),
      ),
    );
    student = store.students.single;
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<LessonSession> lesson(
    String groupId,
    int number,
    int daysAgo, {
    bool dateKnown = true,
  }) async {
    await store.saveSession(
      LessonSession(
        groupId: groupId,
        number: number,
        monthNumber: 2,
        startsAt: now.subtract(Duration(days: daysAgo)),
        startsAtKnown: dateKnown,
        createdAt: now.subtract(const Duration(days: 7)),
      ),
    );
    return store.sessions.last;
  }

  EntryRequest entry(LessonSession session, {String? sourceGroupId}) =>
      EntryRequest(
        studentId: student.id,
        sessionId: session.id,
        mode: EntryMode.package,
        makeupSourceGroupId: sourceGroupId,
      );

  Future<void> buyDuringVisit(LessonSession visited) async {
    await store.recordAttendance(
      entry(visited, sourceGroupId: originalGroup.id),
    );
    // Reception may settle the already-saved guest attendance without selecting
    // its source again. That source must remain the owner of the new month.
    await store.collectAndAttend(entry(visited));
  }

  test(
    'a month bought during another group lesson covers the later original-group lesson without a second charge or unrelated-group credit',
    () async {
      // October 2026 regression: original lesson 3 was scheduled before the
      // actual collection time, after the visited purchase lesson 2.
      final earlierOriginal = await lesson(originalGroup.id, 1, 3);
      final visited = await lesson(visitedGroup.id, 2, 2);
      final laterOriginal = await lesson(originalGroup.id, 3, 1);
      final unrelated = await lesson(unrelatedGroup.id, 3, 1);
      await buyDuringVisit(visited);
      final package = store.packages.single;
      final payment = store.payments.single;
      expect(package.groupId, originalGroup.id);
      expect(payment.groupId, originalGroup.id);
      expect(payment.sessionId, visited.id);
      expect(store.attendances.single.status, AttendanceStatus.makeup);
      expect(store.attendances.single.makeupSourceGroupId, originalGroup.id);
      expect(package.remaining, 3);
      expect(package.purchasedAt.isAfter(laterOriginal.startsAt), isTrue);
      await store.closeSession(visited.id);
      await store.finalizeSession(sessionId: visited.id, actualCash: 21000);
      final closing = jsonEncode(store.closings.single.toJson());

      expect(store.eligibleRemainingFor(student.id, unrelated.id), 0);
      expect(store.eligibleRemainingFor(student.id, earlierOriginal.id), 0);
      expect(store.remainingFor(student.id, originalGroup.id), 3);
      expect(store.eligibleRemainingFor(student.id, laterOriginal.id), 3);
      final continuation = entry(laterOriginal);
      await store.recordAttendance(continuation);
      expect(
        store.attendanceNeedsPayment(student.id, laterOriginal.id),
        isTrue,
      );
      final quote = store.entryConfirmationFor(continuation);
      expect(quote.netAmount, 0);
      expect(quote.eligibleRemaining, 3);
      await store.collectAndAttend(continuation);
      expect(
        store.attendanceNeedsPayment(student.id, laterOriginal.id),
        isFalse,
      );
      expect(
        store.paymentStatusFor(student.id, laterOriginal.id).status,
        StudentPaymentStatus.paidPackage,
      );
      expect(store.packages.single.id, package.id);
      expect(store.packages.single.remaining, 2);
      expect(store.payments.single.toJson(), payment.toJson());
      expect(store.refunds, isEmpty);
      expect(jsonEncode(store.closings.single.toJson()), closing);

      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('manager', 'test-manager-123');
      expect(
        store.paymentStatusFor(student.id, laterOriginal.id).status,
        StudentPaymentStatus.paidPackage,
      );
      expect(store.packages.single.remaining, 2);
      expect(store.payments, hasLength(1));
      expect(store.eligibleRemainingFor(student.id, unrelated.id), 0);
      expect(jsonEncode(store.closings.single.toJson()), closing);
    },
  );

  for (final variant in [
    'unassigned purchase',
    'same-group purchase',
    'unknown visit date',
    'unknown target date',
    'same-time earlier lesson',
  ]) {
    test(
      '$variant cannot retroactively fund a class scheduled before the actual purchase',
      () async {
        final visited = await lesson(
          visitedGroup.id,
          2,
          2,
          dateKnown: variant != 'unknown visit date',
        );
        final later = await lesson(
          originalGroup.id,
          variant == 'same-time earlier lesson' ? 1 : 3,
          variant == 'same-time earlier lesson' ? 2 : 1,
          dateKnown: variant != 'unknown target date',
        );
        if (variant == 'unassigned purchase') {
          await store.renewPackage(
            PackageRequest(studentId: student.id, groupId: originalGroup.id),
          );
        } else if (variant == 'same-group purchase') {
          final sameGroup = await lesson(originalGroup.id, 2, 2);
          await store.collectAndAttend(entry(sameGroup));
        } else {
          await buyDuringVisit(visited);
        }
        final payment = store.payments.single.toJson();
        expect(
          store.remainingFor(student.id, originalGroup.id),
          greaterThan(0),
        );
        expect(store.eligibleRemainingFor(student.id, later.id), 0);
        expect(store.entryConfirmationFor(entry(later)).netAmount, 21000);
        expect(store.payments.single.toJson(), payment);
      },
    );
  }

  test(
    'another guest group can use the month only through its explicitly selected original source',
    () async {
      final visited = await lesson(visitedGroup.id, 2, 2);
      final nextVisit = await lesson(unrelatedGroup.id, 3, 1);
      await buyDuringVisit(visited);
      final payment = store.payments.single.toJson();
      expect(store.eligibleRemainingFor(student.id, nextVisit.id), 0);
      await expectLater(
        store.recordAttendance(entry(nextVisit)),
        throwsA(isA<CenterException>()),
      );
      expect(store.attendances, hasLength(1));
      expect(
        store.eligibleMakeupRemainingFor(
          student.id,
          nextVisit.id,
          originalGroup.id,
        ),
        3,
      );
      final explicit = entry(nextVisit, sourceGroupId: originalGroup.id);
      expect(store.entryConfirmationFor(explicit).netAmount, 0);
      await store.recordAttendance(explicit);
      await store.collectAndAttend(entry(nextVisit));
      expect(store.attendances.last.makeupSourceGroupId, originalGroup.id);
      expect(store.attendances.last.packageId, store.packages.single.id);
      expect(store.packages.single.remaining, 2);
      expect(store.payments.single.toJson(), payment);
    },
  );

  test(
    'attendance-only cancellation preserves the verified original-group month purchase anchor',
    () async {
      final visited = await lesson(visitedGroup.id, 2, 2);
      final later = await lesson(originalGroup.id, 3, 1);
      await buyDuringVisit(visited);
      final original = store.attendances.single;
      await store.cancelAttendance(
        attendanceId: original.id,
        reason: 'تصحيح حضور فقط',
      );
      expect(store.attendances, isEmpty);
      expect(store.allAttendances.any((e) => e.id == original.id), isTrue);
      expect(store.packages.single.remaining, 4);
      expect(store.eligibleRemainingFor(student.id, later.id), 4);
      expect(store.entryConfirmationFor(entry(later)).netAmount, 0);
      await store.recordAttendance(entry(later));
      await store.collectAndAttend(entry(later));
      expect(store.packages.single.remaining, 3);
      expect(store.payments, hasLength(1));
      expect(store.refunds, isEmpty);
    },
  );

  test(
    'a cross-group purchase snapshot with its historical makeup link removed is rejected without state loss',
    () async {
      final visited = await lesson(visitedGroup.id, 2, 2);
      await buyDuringVisit(visited);
      final payment = store.payments.single.toJson();
      final backup = await store.createBackup();
      final forged =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final data = forged['data'] as Map;
      data['attendances'] = [];
      data['corrections'] = [];
      (data['packages'] as List).single['remaining'] = 4;
      final file = File('${directory.path}/missing-makeup-evidence.json');
      await file.writeAsString(jsonEncode(forged));
      await expectLater(
        store.restoreBackup(file.path),
        throwsA(isA<CenterException>()),
      );
      expect(store.payments.single.toJson(), payment);
      expect(store.attendances.single.makeupSourceGroupId, originalGroup.id);
      expect(store.packages.single.remaining, 3);
    },
  );
}
