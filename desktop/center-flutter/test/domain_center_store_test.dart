import 'dart:io';
import 'dart:async';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late String groupId;
  late String studentId;
  Future<LessonSession> session(
    int number, {
    SessionKind kind = SessionKind.counted,
    DateTime? date,
  }) async {
    await store.saveSession(
      LessonSession(
        groupId: groupId,
        number: number,
        startsAt: date ?? DateTime.now().add(Duration(hours: number)),
        kind: kind,
        extraPrice: 20000,
        createdAt: DateTime.now(),
      ),
    );
    return store.sessions.last;
  }

  Future<void> enter(
    LessonSession lesson, {
    EntryMode mode = EntryMode.package,
    String? original,
  }) => store.collectAndAttend(
    EntryRequest(
      studentId: studentId,
      sessionId: lesson.id,
      mode: mode,
      originalAttendanceId: original,
    ),
  );
  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-center-domain-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', 'test-pass-123');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'أ',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 10000,
        packagePrice: 40000,
      ),
    );
    groupId = store.groups.single.id;
    await store.saveStudent(
      Student(
        name: 'أحمد',
        code: 'S1',
        groupIds: [groupId],
        discountPercent: 25,
        createdAt: DateTime.now().subtract(const Duration(days: 2)),
      ),
    );
    studentId = store.students.single.id;
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  test(
    'package entry atomically buys discounted four and duplicate cannot charge',
    () async {
      final lesson = await session(1);
      await enter(lesson);
      expect(store.remainingFor(studentId, groupId), 3);
      expect(store.payments.single.baseAmount, 40000);
      expect(store.payments.single.netAmount, 30000);
      expect(store.attendanceCount(lesson.id), 1);
      await expectLater(enter(lesson), throwsA(isA<CenterException>()));
      expect(store.payments, hasLength(1));
      expect(store.attendances, hasLength(1));
      expect(store.packages.single.remaining, 3);
    },
  );
  test(
    'counted absence consumes once but single absence never creates debt',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: studentId, groupId: groupId),
      );
      await store.saveStudent(
        Student(
          name: 'مينا',
          code: 'S2',
          groupIds: [groupId],
          createdAt: DateTime.now().subtract(const Duration(days: 1)),
        ),
      );
      final lesson = await session(1);
      await store.closeSession(lesson.id);
      expect(store.attendances, hasLength(2));
      expect(store.packages.single.remaining, 3);
      expect(store.payments, hasLength(1));
      expect(store.attendances.first.packageId, store.packages.single.id);
      expect(store.attendances.last.packageId, isNull);
      await expectLater(
        store.closeSession(lesson.id),
        throwsA(isA<CenterException>()),
      );
      expect(store.packages.single.remaining, 3);
      await expectLater(enter(lesson), throwsA(isA<CenterException>()));
    },
  );
  test(
    'four consecutive counted sessions consume package and uncounted do not',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: studentId, groupId: groupId),
      );
      await store.closeSession((await session(1)).id);
      await enter(await session(2, kind: SessionKind.free));
      expect(store.payments, hasLength(1));
      expect(store.packages.single.remaining, 3);
      await enter(await session(3, kind: SessionKind.extra));
      expect(store.payments.last.netAmount, 15000);
      expect(store.packages.single.remaining, 3);
      for (var i = 4; i <= 6; i++) {
        await store.closeSession((await session(i)).id);
      }
      expect(store.packages.single.remaining, 0);
      await enter(await session(7));
      expect(store.packages, hasLength(2));
      expect(store.packages.last.remaining, 3);
    },
  );
  test(
    'single entry and 100 percent discount still records zero payment',
    () async {
      final student = store.students.single;
      await store.saveStudent(student.copyWith(discountPercent: 100));
      await enter(await session(1), mode: EntryMode.single);
      expect(store.payments.single.netAmount, 0);
      expect(store.packages, isEmpty);
      await store.saveStudent(student.copyWith(discountPercent: 0));
      expect(store.payments.single.discountPercent, 100);
      expect(store.payments.single.netAmount, 0);
      await store.closeSession(store.sessions.first.id);
      await enter(await session(2), mode: EntryMode.single);
      expect(store.payments.last.netAmount, 10000);
    },
  );
  test('prepaid renew keeps balance and consumption is FIFO', () async {
    await store.renewPackage(
      PackageRequest(studentId: studentId, groupId: groupId),
    );
    await enter(await session(1));
    final first = store.packages.first.id;
    await store.renewPackage(
      PackageRequest(studentId: studentId, groupId: groupId),
    );
    expect(store.remainingFor(studentId, groupId), 7);
    await store.closeSession(store.sessions.first.id);
    await enter(await session(2));
    expect(store.packages.first.remaining, 2);
    expect(store.packages.last.remaining, 4);
    expect(store.attendances.last.packageId, first);
  });
  test(
    'makeup links paid absence without consumption or charge and cannot repeat',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: studentId, groupId: groupId),
      );
      final original = await session(1);
      await store.closeSession(original.id);
      final absence = store.attendances.single;
      final target = await session(2);
      expect(store.eligibleMakeups(studentId, target.id).single.id, absence.id);
      await enter(target, mode: EntryMode.makeup, original: absence.id);
      expect(store.packages.single.remaining, 3);
      expect(store.payments, hasLength(1));
      expect(store.attendances.last.originalAttendanceId, absence.id);
      expect(store.attendanceCount(target.id), 1);
      final repeat = await session(3);
      await expectLater(
        enter(repeat, mode: EntryMode.makeup, original: absence.id),
        throwsA(isA<CenterException>()),
      );
      expect(store.attendances, hasLength(2));
    },
  );
  test(
    'past closure cannot consume newly purchased package or include new student',
    () async {
      final past = await session(
        1,
        date: DateTime.now().subtract(const Duration(days: 1)),
      );
      await store.renewPackage(
        PackageRequest(studentId: studentId, groupId: groupId),
      );
      await store.saveStudent(
        Student(
          name: 'جديد',
          code: 'S2',
          groupIds: [groupId],
          createdAt: DateTime.now(),
        ),
      );
      await store.closeSession(past.id);
      expect(store.packages.single.remaining, 4);
      expect(store.attendances, hasLength(1));
      expect(store.attendances.single.packageId, isNull);
    },
  );
  test(
    'package bought in current session permits other close accounting and paid original',
    () async {
      final lesson = await session(
        1,
        date: DateTime.now().subtract(const Duration(minutes: 10)),
      );
      await enter(lesson);
      expect(store.packages.single.remaining, 3);
      await store.closeSession(lesson.id);
      expect(store.attendances, hasLength(1));
      expect(store.packages.single.remaining, 3);
    },
  );
  test(
    'cancel with data is rejected and academically absent is distinct from zero',
    () async {
      final lesson = await session(1);
      await enter(lesson);
      await expectLater(
        store.cancelSession(lesson.id),
        throwsA(isA<CenterException>()),
      );
      await store.saveAcademic(
        AcademicRecord(
          studentId: studentId,
          sessionId: lesson.id,
          score: 0,
          maxScore: 10,
          updatedAt: DateTime.now(),
        ),
      );
      expect(store.academics.single.score, 0);
      expect(store.academics.single.examAbsent, false);
      await expectLater(
        store.saveAcademic(
          AcademicRecord(
            studentId: studentId,
            sessionId: lesson.id,
            score: 0,
            examAbsent: true,
            updatedAt: DateTime.now(),
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.academics.single.score, 0);
      await store.saveAcademic(
        AcademicRecord(
          studentId: studentId,
          sessionId: lesson.id,
          examAbsent: true,
          updatedAt: DateTime.now(),
        ),
      );
      expect(store.academics.single.score, isNull);
      expect(store.academics.single.examAbsent, true);
    },
  );
  test(
    'concurrent duplicate requests serialize and unauthorized actions cannot write',
    () async {
      final lesson = await session(1);
      final results = await Future.wait([
        enter(lesson).then((_) => true).catchError((_) => false),
        enter(lesson).then((_) => true).catchError((_) => false),
      ]);
      expect(results.where((e) => e), hasLength(1));
      expect(store.payments, hasLength(1));
      await store.saveStaff(
        name: 'مساعد',
        password: 'assistant-123',
        role: StaffRole.assistant,
      );
      store.signOut();
      await store.signIn('مساعد', 'assistant-123');
      await expectLater(
        store.renewPackage(
          PackageRequest(studentId: studentId, groupId: groupId),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.payments, hasLength(1));
      await store.saveAcademic(
        AcademicRecord(
          studentId: studentId,
          sessionId: lesson.id,
          score: 5,
          updatedAt: DateTime.now(),
        ),
      );
      expect(store.academics.single.score, 5);
    },
  );
  test(
    'counted sessions cannot skip earlier open session or backdate after processing',
    () async {
      final first = await session(1);
      final second = await session(2);
      await expectLater(enter(second), throwsA(isA<CenterException>()));
      await expectLater(
        store.closeSession(second.id),
        throwsA(isA<CenterException>()),
      );
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      await store.closeSession(first.id);
      await enter(second);
      await expectLater(
        session(3, date: first.startsAt.subtract(const Duration(hours: 1))),
        throwsA(isA<CenterException>()),
      );
      expect(store.sessions, hasLength(2));
    },
  );
  test(
    'newer package cannot retrospectively cover old entry, current linked renewal can',
    () async {
      final past = await session(
        1,
        date: DateTime.now().subtract(const Duration(hours: 2)),
      );
      await store.renewPackage(
        PackageRequest(studentId: studentId, groupId: groupId),
      );
      final standalone = store.packages.single;
      await enter(past);
      expect(store.packages.first.remaining, 4);
      expect(store.attendances.single.packageId, isNot(standalone.id));
      await store.closeSession(past.id);
      final current = await session(
        2,
        date: DateTime.now().subtract(const Duration(minutes: 5)),
      );
      await store.renewPackage(
        PackageRequest(
          studentId: studentId,
          groupId: groupId,
          sessionId: current.id,
        ),
      );
      final linked = store.packages.last;
      await enter(current);
      expect(store.attendances.last.packageId, linked.id);
      expect(store.packages.first.remaining, 4);
    },
  );
  test(
    'removing and rejoining group cannot create backdated absence',
    () async {
      final group = store.groups.single;
      await store.saveGroup(group.copyWith(id: '', name: 'ب'));
      final otherId = store.groups.last.id;
      final old = store.students.single;
      await store.saveStudent(old.copyWith(groupIds: [otherId]));
      final past = await session(
        1,
        date: DateTime.now().subtract(const Duration(hours: 1)),
      );
      await store.saveStudent(old.copyWith(groupIds: [groupId, otherId]));
      await store.closeSession(past.id);
      expect(store.attendances, isEmpty);
    },
  );
  test('cashier can register but cannot assign or remove discounts', () async {
    await store.saveStaff(
      name: 'تحصيل',
      password: 'cashier-123',
      role: StaffRole.cashier,
    );
    store.signOut();
    await store.signIn('تحصيل', 'cashier-123');
    await expectLater(
      store.saveStudent(store.students.single.copyWith(discountPercent: 0)),
      throwsA(isA<CenterException>()),
    );
    await expectLater(
      store.saveStudent(
        Student(
          name: 'جديد',
          discountPercent: 10,
          groupIds: [groupId],
          createdAt: DateTime.now(),
        ),
      ),
      throwsA(isA<CenterException>()),
    );
    await store.saveStudent(
      Student(name: 'جديد', groupIds: [groupId], createdAt: DateTime.now()),
    );
    expect(store.students, hasLength(2));
    expect(store.students.first.discountPercent, 25);
  });
  test(
    'paid absence can be made up after package expires and across matching groups',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: studentId, groupId: groupId),
      );
      final original = await session(1);
      await store.closeSession(original.id);
      final absence = store.attendances.single;
      for (var i = 2; i <= 4; i++) {
        await store.closeSession((await session(i)).id);
      }
      expect(store.remainingFor(studentId, groupId), 0);
      final group = store.groups.single;
      await store.saveGroup(group.copyWith(id: '', name: 'مجموعة أخرى'));
      final matchingGroup = store.groups.last;
      await store.saveSession(
        LessonSession(
          groupId: matchingGroup.id,
          number: 1,
          startsAt: DateTime.now().add(const Duration(hours: 5)),
          createdAt: DateTime.now(),
        ),
      );
      final target = store.sessions.last;
      expect(
        store
            .eligibleMakeups(studentId, target.id)
            .any((e) => e.id == absence.id),
        true,
      );
      await enter(target, mode: EntryMode.makeup, original: absence.id);
      expect(store.payments, hasLength(1));
      expect(store.remainingFor(studentId, groupId), 0);
      await store.saveCatalog(
        const CatalogEntry(name: 'صف آخر', kind: CatalogKind.grade),
      );
      await store.saveGroup(
        group.copyWith(
          id: '',
          name: 'صف مختلف',
          gradeId: store.catalogs.last.id,
        ),
      );
      await store.saveSession(
        LessonSession(
          groupId: store.groups.last.id,
          number: 1,
          startsAt: DateTime.now().add(const Duration(hours: 6)),
          createdAt: DateTime.now(),
        ),
      );
      expect(store.eligibleMakeups(studentId, store.sessions.last.id), isEmpty);
    },
  );
  test(
    'explicit signout during pending command is never undone by rollback',
    () async {
      final pending = store
          .saveStaff(
            name: 'مساعد جديد',
            password: 'assistant-123',
            role: StaffRole.assistant,
          )
          .then((_) => true)
          .catchError((_) => false);
      scheduleMicrotask(store.signOut);
      await pending;
      expect(store.currentUser, isNull);
      expect(store.canManage, false);
      await expectLater(
        store.saveCatalog(
          const CatalogEntry(name: 'ممنوع', kind: CatalogKind.subject),
        ),
        throwsA(isA<CenterException>()),
      );
    },
  );
  test(
    'equal-time counted sessions use session number to preserve package order',
    () async {
      final date = DateTime.now().add(const Duration(hours: 1));
      final first = await session(1, date: date);
      final second = await session(2, date: date);
      await expectLater(enter(second), throwsA(isA<CenterException>()));
      expect(store.payments, isEmpty);
      await store.closeSession(first.id);
      await enter(second);
      expect(store.attendances.last.sessionId, second.id);
      expect(store.payments, hasLength(1));
    },
  );
  test(
    'eligible package cannot skip counted session through an extra single charge',
    () async {
      await store.renewPackage(
        PackageRequest(studentId: studentId, groupId: groupId),
      );
      final current = await session(1);
      await expectLater(
        enter(current, mode: EntryMode.single),
        throwsA(isA<CenterException>()),
      );
      expect(store.payments, hasLength(1));
      expect(store.attendances, isEmpty);
      expect(store.remainingFor(studentId, groupId), 4);
      await enter(current, mode: EntryMode.package);
      expect(store.payments, hasLength(1));
      expect(store.attendances.single.packageId, store.packages.single.id);
      expect(store.remainingFor(studentId, groupId), 3);
    },
  );
  test(
    'used group keeps historical subject center and grade while price edits remain allowed',
    () async {
      final group = store.groups.single;
      await session(1);
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(
          CatalogEntry(name: 'جديد ${kind.name}', kind: kind),
        );
      }
      final newSubject = store.catalogs
          .lastWhere((e) => e.kind == CatalogKind.subject)
          .id;
      final newCenter = store.catalogs
          .lastWhere((e) => e.kind == CatalogKind.center)
          .id;
      final newGrade = store.catalogs
          .lastWhere((e) => e.kind == CatalogKind.grade)
          .id;
      for (final changed in [
        group.copyWith(subjectId: newSubject),
        group.copyWith(centerId: newCenter),
        group.copyWith(gradeId: newGrade),
      ]) {
        await expectLater(
          store.saveGroup(changed),
          throwsA(isA<CenterException>()),
        );
      }
      expect(store.groups.single.subjectId, group.subjectId);
      expect(store.groups.single.centerId, group.centerId);
      expect(store.groups.single.gradeId, group.gradeId);
      await store.saveGroup(
        group.copyWith(
          name: 'اسم جديد',
          sessionPrice: 15000,
          packagePrice: 60000,
          schedule: 'الأحد',
        ),
      );
      expect(store.groups.single.name, 'اسم جديد');
      expect(store.groups.single.sessionPrice, 15000);
      expect(store.groups.single.schedule, 'الأحد');
    },
  );
}
