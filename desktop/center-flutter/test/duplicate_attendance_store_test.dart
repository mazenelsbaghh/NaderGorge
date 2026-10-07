import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'duplicate-attendance-test-password';
  late Directory directory;
  late CenterStore store;
  late List<StudyGroup> groups;
  late Student student;
  var day = 0;

  setUp(() async {
    day = 0;
    directory = await Directory.systemTemp.createTemp(
      'massar-duplicate-attendance-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير الاختبار', password);
    for (final kind in CatalogKind.values) {
      for (var index = 0; index < 2; index++) {
        await store.saveCatalog(
          CatalogEntry(name: '${kind.name} $index', kind: kind),
        );
      }
    }
    String catalog(CatalogKind kind, int index) =>
        store.catalogs.where((e) => e.kind == kind).elementAt(index).id;
    for (var index = 0; index < 4; index++) {
      final link = index == 0 || index == 3 ? 0 : 1;
      await store.saveGroup(
        StudyGroup(
          name: 'مجموعة $index',
          subjectId: catalog(CatalogKind.subject, link),
          centerId: catalog(CatalogKind.center, link),
          gradeId: catalog(CatalogKind.grade, link),
          sessionPrice: 10000,
          packagePrice: 39000,
          twoSessionPrice: 17777,
        ),
      );
    }
    groups = store.groups;
    student = await store.registerStudent(
      Student(
        name: 'طالب الاختبار',
        groupIds: groups.map((e) => e.id).toList(),
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<LessonSession> lesson(
    int groupIndex, {
    int number = 7,
    SessionKind kind = SessionKind.counted,
  }) async {
    day++;
    await store.saveSession(
      LessonSession(
        groupId: groups[groupIndex].id,
        number: number,
        kind: kind,
        extraPrice: kind == SessionKind.extra ? 6000 : 0,
        startsAt: DateTime.now().add(Duration(days: day)),
        createdAt: DateTime.now(),
      ),
    );
    return store.sessions.last;
  }

  Future<void> enter(
    LessonSession target, {
    EntryMode mode = EntryMode.single,
    List<String> reviewed = const [],
    String? originalId,
    String? studentId,
  }) => store.collectAndAttend(
    EntryRequest(
      studentId: studentId ?? student.id,
      sessionId: target.id,
      mode: mode,
      acknowledgedAttendanceIds: reviewed,
      originalAttendanceId: originalId,
      packageSessions: 2,
    ),
  );

  String evidence() => jsonEncode({
    'attendance': store.allAttendances.map((e) => e.toJson()).toList(),
    'payments': store.allPayments.map((e) => e.toJson()).toList(),
    'packages': store.allPackages.map((e) => e.toJson()).toList(),
    'refunds': store.refunds.map((e) => e.toJson()).toList(),
    'corrections': store.corrections.map((e) => e.toJson()).toList(),
    'audit': store.audit.map((e) => e.toJson()).toList(),
  });

  Set<String> conflicts(LessonSession target) => store
      .attendanceConflictsFor(student.id, target.id)
      .map((e) => e.id)
      .toSet();

  test(
    'same number means any date, subject or grade; query includes selected session and excludes other students, different numbers and canceled entries',
    () async {
      final first = await lesson(0);
      await enter(first);
      final firstId = store.attendances.single.id;
      await store.closeSession(first.id);
      final free = await lesson(1, kind: SessionKind.free);
      await enter(free, reviewed: [firstId]);
      final freeId = store.attendances.last.id;
      final otherNumber = await lesson(2, number: 8, kind: SessionKind.extra);
      await enter(otherNumber);
      final target = await lesson(2, kind: SessionKind.extra);
      final otherStudent = await store.registerStudent(
        Student(
          name: 'طالب آخر',
          groupIds: [groups[2].id],
          createdAt: DateTime.now(),
        ),
      );
      await enter(target, studentId: otherStudent.id);
      final canceled = await lesson(3);
      await store.cancelSession(canceled.id);
      expect(conflicts(target), {firstId, freeId});
      expect(conflicts(first), {firstId, freeId});
      final beforeRead = evidence();
      conflicts(target);
      expect(evidence(), beforeRead);
      await store.cancelAttendance(
        attendanceId: freeId,
        reason: 'حضور مجاني خاطئ',
      );
      expect(conflicts(target), {firstId});
      expect(store.allAttendances.any((e) => e.id == freeId), isTrue);
    },
  );

  test(
    'paid absence is not a conflict but makeup is, and canceling makeup makes that original usable again',
    () async {
      final absentSession = await lesson(0);
      await store.renewPackage(
        PackageRequest(
          studentId: student.id,
          groupId: groups[0].id,
          sessions: 2,
        ),
      );
      await store.closeSession(absentSession.id);
      final absent = store.attendances.single;
      expect(absent.status, AttendanceStatus.absent);
      final makeup = await lesson(3, kind: SessionKind.free);
      expect(conflicts(makeup), isEmpty);
      await enter(makeup, mode: EntryMode.makeup, originalId: absent.id);
      final makeupId = store.attendances.last.id;
      final target = await lesson(1);
      expect(conflicts(target), {makeupId});
      expect(conflicts(makeup), {makeupId});
      final before = evidence();
      await expectLater(enter(target), throwsA(isA<CenterException>()));
      expect(evidence(), before);
      await enter(target, reviewed: [makeupId]);
      await store.cancelAttendance(
        attendanceId: makeupId,
        reason: 'التعويض لم يحضر',
      );
      expect(conflicts(makeup), {store.attendances.last.id});
      expect(
        store.eligibleMakeups(student.id, makeup.id).map((e) => e.id),
        contains(absent.id),
      );
      expect(store.remainingFor(student.id, groups[0].id), 1);
    },
  );

  for (final scenario in ['single', 'newPackage', 'existingPackage']) {
    test(
      '$scenario needs the exact reviewed conflicts before collecting or consuming credit and audit records the acknowledgment',
      () async {
        final first = await lesson(0);
        await enter(first);
        final firstId = store.attendances.single.id;
        final target = await lesson(1);
        if (scenario == 'existingPackage') {
          await store.renewPackage(
            PackageRequest(
              studentId: student.id,
              groupId: groups[1].id,
              sessions: 2,
            ),
          );
        }
        final mode = scenario == 'single'
            ? EntryMode.single
            : EntryMode.package;
        final before = evidence();
        await expectLater(
          enter(target, mode: mode),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), before);
        final previousPayments = store.allPayments.length;
        await enter(target, mode: mode, reviewed: [firstId]);
        expect(store.attendanceCount(target.id), 1);
        expect(
          store.allPayments.length,
          previousPayments + (scenario == 'existingPackage' ? 0 : 1),
        );
        if (mode == EntryMode.package) {
          expect(store.remainingFor(student.id, groups[1].id), 1);
        }
        expect(store.audit.last.action, 'entry');
        expect(store.audit.last.staffId, store.currentUser!.id);
        expect(store.audit.last.description, contains(firstId));
        final after = evidence();
        await expectLater(
          enter(target, mode: mode, reviewed: conflicts(target).toList()),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), after);
      },
    );
  }

  test(
    'changed, extra, forged and duplicate acknowledgment IDs reject atomically; refreshed exact set succeeds regardless of order',
    () async {
      final first = await lesson(0);
      final noPriorAttendance = evidence();
      await expectLater(
        enter(first, reviewed: ['forged-id']),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), noPriorAttendance);
      await enter(first);
      final firstId = store.attendances.single.id;
      final target = await lesson(1);
      final second = await lesson(2);
      await enter(second, reviewed: [firstId]);
      final secondId = store.attendances.last.id;
      final before = evidence();
      for (final invalid in <List<String>>[
        [firstId],
        [firstId, secondId, 'forged-id'],
        [firstId, firstId, secondId],
      ]) {
        await expectLater(
          enter(target, reviewed: invalid),
          throwsA(isA<CenterException>()),
        );
        expect(evidence(), before);
      }
      await store.cancelAttendance(attendanceId: secondId, reason: 'لم يحضر');
      final afterCancel = evidence();
      await expectLater(
        enter(target, reviewed: [firstId, secondId]),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), afterCancel);
      await enter(second, reviewed: [firstId]);
      final replacementId = store.attendances.last.id;
      final beforeStaleId = evidence();
      await expectLater(
        enter(target, reviewed: [firstId, secondId]),
        throwsA(isA<CenterException>()),
      );
      expect(evidence(), beforeStaleId);
      await enter(target, reviewed: [replacementId, firstId]);
      expect(store.attendanceCount(target.id), 1);
      expect(store.audit.last.description, contains(firstId));
      expect(store.audit.last.description, contains(replacementId));
    },
  );

  test(
    'serialized concurrent attendance cannot reuse a conflict review after another group records a new entry',
    () async {
      final first = await lesson(0);
      await enter(first);
      final reviewed = [store.attendances.single.id];
      final second = await lesson(1);
      final third = await lesson(2);
      Future<bool> attempt(LessonSession target) async {
        try {
          await enter(target, reviewed: reviewed);
          return true;
        } on CenterException {
          return false;
        }
      }

      final pending = Future.wait([attempt(second), attempt(third)]);
      // Caller-owned lists may change while queued; each command must retain
      // the reviewed IDs it received, not the caller's later edits.
      reviewed.clear();
      final results = await pending;
      expect(results.where((e) => e), hasLength(1));
      expect(store.attendances, hasLength(2));
      expect(store.payments, hasLength(2));
      expect(store.refunds, isEmpty);
      expect(
        store.attendanceCount(second.id) + store.attendanceCount(third.id),
        1,
      );
    },
  );

  test(
    'retained payment re-attendance needs review; repayment and money correction preserve an already recorded physical entry without another warning',
    () async {
      final first = await lesson(0);
      await enter(first);
      final reviewedId = store.attendances.single.id;
      final target = await lesson(1);
      await enter(target, reviewed: [reviewedId]);
      final targetPayment = store.payments.last.toJson();
      await store.cancelAttendance(
        attendanceId: store.attendances.last.id,
        reason: 'إلغاء حضور فقط',
      );
      final before = evidence();
      await expectLater(enter(target), throwsA(isA<CenterException>()));
      expect(evidence(), before);
      await enter(target, reviewed: [reviewedId]);
      expect(store.payments.last.toJson(), targetPayment);
      expect(store.allPayments, hasLength(2));
      await store.cancelPayment(
        paymentId: store.payments.last.id,
        reason: 'إلغاء الإيصال فقط',
      );
      expect(store.attendanceNeedsPayment(student.id, target.id), isTrue);
      await enter(target);
      expect(store.attendanceCount(target.id), 1);
      expect(store.attendances, hasLength(2));
      await store.correctEntry(
        attendanceId: store.attendances.last.id,
        mode: EntryMode.package,
        packageSessions: 2,
        reason: 'استبدال الحصة بباقة',
      );
      expect(store.attendanceCount(target.id), 1);
      expect(store.attendances, hasLength(2));
      expect(store.packages.single.remaining, 1);
      expect(store.refunds, hasLength(2));
    },
  );

  test(
    'review is transaction-local: restart and backup preserve records but do not pre-authorize future attendance',
    () async {
      final first = await lesson(0);
      await enter(first);
      final firstId = store.attendances.single.id;
      final second = await lesson(1);
      await enter(second, reviewed: [firstId]);
      final ids = store.attendances.map((e) => e.id).toSet();
      final target = await lesson(2);
      final backup = await store.createBackup();
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير الاختبار', password);
      expect(conflicts(target), ids);
      final before = evidence();
      await expectLater(enter(target), throwsA(isA<CenterException>()));
      expect(evidence(), before);
      await store.restoreBackup(backup);
      await store.signIn('مدير الاختبار', password);
      expect(conflicts(target), ids);
      await expectLater(
        enter(target, reviewed: [firstId]),
        throwsA(isA<CenterException>()),
      );
      await enter(target, reviewed: ids.toList());
      expect(store.attendanceCount(target.id), 1);
    },
  );
}
