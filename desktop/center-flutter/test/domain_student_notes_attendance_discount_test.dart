import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  var sessionNumber = 0;

  setUp(() async {
    sessionNumber = 0;
    directory = await Directory.systemTemp.createTemp('massar-fixed-discount-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', 'test-pass-123');
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 10000,
        packagePrice: 40000,
      ),
    );
    group = store.groups.single;
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<Student> addStudent(String code, int discount) async {
    await store.saveStudent(
      Student(
        name: 'طالب $code',
        code: code,
        discountPercent: discount,
        groupIds: [group.id],
        phone: '01011111111',
        guardianPhone: '01022222222',
        createdAt: DateTime.now().subtract(const Duration(days: 1)),
      ),
    );
    return store.students.last;
  }

  Future<LessonSession> addSession({
    SessionKind kind = SessionKind.counted,
  }) async {
    sessionNumber++;
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: sessionNumber,
        kind: kind,
        startsAt: DateTime.now().add(Duration(hours: sessionNumber)),
        createdAt: DateTime.now(),
      ),
    );
    return store.sessions.last;
  }

  Future<void> enter(
    Student student,
    LessonSession session,
    EntryMode mode, {
    String? original,
  }) => store.collectAndAttend(
    EntryRequest(
      studentId: student.id,
      sessionId: session.id,
      mode: mode,
      originalAttendanceId: original,
    ),
  );

  Future<void> reopenStore() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير', 'test-pass-123');
  }

  test(
    'cashier notes save resolves latest student without overwriting identity or discount; persists and clears',
    () async {
      final staleSelectedStudent = await addStudent('S1', 30);
      // These queued commands imitate a student update while the note editor is open.
      final renamed = staleSelectedStudent.copyWith(
        name: 'الاسم الجديد',
        discountPercent: 70,
        phone: '01033333333',
      );
      await Future.wait([
        store.saveStudent(renamed),
        store.saveStudentNote(
          studentId: staleSelectedStudent.id,
          notes: '  ملاحظة جديدة  ',
        ),
      ]);
      final latest = store.students.single;
      expect(latest.name, 'الاسم الجديد');
      expect(latest.discountPercent, 70);
      expect(latest.phone, '01033333333');
      expect(latest.code, staleSelectedStudent.code);
      expect(latest.groupIds, staleSelectedStudent.groupIds);
      expect(latest.notes, 'ملاحظة جديدة');
      await store.saveStaff(
        name: 'خزينة',
        password: 'cashier-pass-123',
        role: StaffRole.cashier,
      );
      await store.signIn('خزينة', 'cashier-pass-123');
      final otherFields = Map<String, dynamic>.from(
        store.students.single.toJson(),
      )..remove('notes');
      await store.saveStudentNote(studentId: latest.id, notes: 'ملاحظة الموظف');
      expect(
        Map<String, dynamic>.from(store.students.single.toJson())
          ..remove('notes'),
        otherFields,
      );
      expect(store.audit.last.action, 'student_note');
      expect(store.audit.last.staffId, store.currentUser!.id);
      expect(store.audit.last.description, contains(latest.code));
      await reopenStore();
      expect(store.students.single.notes, 'ملاحظة الموظف');
      await store.saveStudentNote(studentId: latest.id, notes: '   ');
      await reopenStore();
      expect(store.students.single.notes, isEmpty);
    },
  );

  test(
    'notes reject assistant, signed-out, missing student and excessive length atomically',
    () async {
      final student = await addStudent('S1', 25);
      await store.saveStudentNote(studentId: student.id, notes: 'محفوظة');
      final auditCount = store.audit.length;
      await expectLater(
        store.saveStudentNote(studentId: student.id, notes: 'x' * 4001),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.saveStudentNote(studentId: 'missing', notes: 'wrong'),
        throwsA(isA<CenterException>()),
      );
      expect(store.audit.length, auditCount);
      expect(store.students.single.notes, 'محفوظة');
      await store.saveStaff(
        name: 'مساعد',
        password: 'assistant-pass-123',
        role: StaffRole.assistant,
      );
      await store.signIn('مساعد', 'assistant-pass-123');
      await expectLater(
        store.saveStudentNote(studentId: student.id, notes: 'wrong'),
        throwsA(isA<CenterException>()),
      );
      store.signOut();
      await expectLater(
        store.saveStudentNote(studentId: student.id, notes: ''),
        throwsA(isA<CenterException>()),
      );
      await reopenStore();
      expect(store.students.single.notes, 'محفوظة');
    },
  );

  test(
    'prior package 50% and fixed attendance 25% stay separate through closing and backup after later edits',
    () async {
      final student = await addStudent('S1', 50);
      final first = await addSession();
      await enter(student, first, EntryMode.package);
      await store.closeSession(first.id);
      await store.saveStudent(
        store.students.single.copyWith(discountPercent: 25),
      );
      final second = await addSession();
      await enter(student, second, EntryMode.package);
      final secondEntry = store.attendances.firstWhere(
        (e) => e.sessionId == second.id,
      );
      expect(secondEntry.fixedDiscountPercent, 25);
      await store.saveStudent(
        store.students.single.copyWith(discountPercent: 70),
      );
      await store.closeSession(second.id);
      final summary = store.sessionFinancialSummary(second.id);
      expect(summary.totalCollected, 0);
      expect(summary.studentCategories!.single.discountPercent, 50);
      expect(
        summary.studentCategories!.single.kind,
        SessionStudentCategoryKind.prepaid,
      );
      expect(summary.attendanceDiscountCategories!.single.discountPercent, 25);
      expect(summary.attendanceDiscountCategories!.single.studentCount, 1);
      expect(summary.packageBuyerCount, 0);
      expect(summary.paymentAmountCategories, isEmpty);
      expect(summary.allFreeCount, 0);
      await store.finalizeSession(sessionId: second.id, actualCash: 0);
      final saved = store.closings.single.summary.toJson();
      final backup = await store.createBackup(
        destination: '${directory.path}/fixed-backup.json',
      );
      await reopenStore();
      expect(store.closings.single.summary.toJson(), saved);
      await store.restoreBackup(backup);
      expect(store.closings.single.summary.toJson(), saved);
      expect(
        store.allAttendances
            .firstWhere((e) => e.sessionId == first.id)
            .fixedDiscountPercent,
        50,
      );
    },
  );

  test(
    'free 70% attendee has no payment; fixed attendance and absence snapshots are immutable',
    () async {
      final attended = await addStudent('S1', 70);
      final absent = await addStudent('S2', 25);
      final session = await addSession(kind: SessionKind.free);
      await enter(attended, session, EntryMode.single);
      await store.saveStudent(
        store.students.first.copyWith(discountPercent: 0),
      );
      await store.closeSession(session.id);
      final summary = store.sessionFinancialSummary(session.id);
      expect(store.payments, isEmpty);
      expect(summary.totalCollected, 0);
      expect(summary.attendanceDiscountCategories!.single.discountPercent, 70);
      expect(summary.allFreeCount, 1);
      expect(summary.absentCount, 1);
      expect(
        store.attendances
            .firstWhere((e) => e.studentId == absent.id)
            .fixedDiscountPercent,
        25,
      );
      await store.saveStudent(
        store.students.last.copyWith(discountPercent: 100),
      );
      await store.markAbsentPresent(
        attendanceId: store.attendances
            .firstWhere((e) => e.studentId == absent.id)
            .id,
        reason: 'حضر ولم يسجل',
      );
      final corrected = store.sessionFinancialSummary(session.id);
      expect(corrected.allFreeCount, 2);
      expect(
        corrected.attendanceDiscountCategories!.map((e) => e.discountPercent),
        [70, 100],
      );
      await reopenStore();
      expect(store.sessionFinancialSummary(session.id).allFreeCount, 2);
    },
  );

  test(
    'unique free aggregate does not double count exempt prior package, fixed exemption, single or makeup',
    () async {
      final previousExempt = await addStudent('P', 100);
      final fixedExempt = await addStudent('F', 50);
      final source = await addStudent('M', 25);
      final singleExempt = await addStudent('S', 100);
      final first = await addSession();
      await enter(previousExempt, first, EntryMode.package);
      await enter(fixedExempt, first, EntryMode.package);
      await store.renewPackage(
        PackageRequest(studentId: source.id, groupId: group.id),
      );
      await store.closeSession(first.id);
      final original = store.attendances.firstWhere(
        (e) => e.studentId == source.id,
      );
      await store.saveStudent(
        store.students
            .firstWhere((e) => e.id == fixedExempt.id)
            .copyWith(discountPercent: 100),
      );
      await store.saveStudent(
        store.students
            .firstWhere((e) => e.id == source.id)
            .copyWith(discountPercent: 100),
      );
      final second = await addSession();
      await enter(previousExempt, second, EntryMode.package);
      await enter(fixedExempt, second, EntryMode.package);
      await enter(source, second, EntryMode.makeup, original: original.id);
      await enter(singleExempt, second, EntryMode.single);
      await store.closeSession(second.id);
      final summary = store.sessionFinancialSummary(second.id);
      expect(summary.presentCount, 3);
      expect(summary.makeupCount, 1);
      expect(summary.allFreeCount, 4);
      expect(summary.attendanceDiscountCategories!.single.discountPercent, 100);
      expect(summary.attendanceDiscountCategories!.single.studentCount, 4);
      expect(summary.totalCollected, 0);
      expect(summary.packageBuyerCount, 0);
      await store.finalizeSession(sessionId: second.id, actualCash: 0);
      final originalSnapshot = store.closings.single.summary.toJson();
      await store.reopenFinancialClosing(
        closingId: store.closings.single.id,
        reason: 'تصحيح الكود',
      );
      final singleAttendance = store.attendances.firstWhere(
        (e) => e.studentId == singleExempt.id && e.sessionId == second.id,
      );
      await store.reverseEntry(
        attendanceId: singleAttendance.id,
        reason: 'الطالب لم يحضر',
      );
      expect(store.sessionFinancialSummary(second.id).allFreeCount, 3);
      await store.finalizeSession(sessionId: second.id, actualCash: 0);
      expect(store.allClosings.first.summary.toJson(), originalSnapshot);
      await reopenStore();
      expect(store.allClosings.first.summary.toJson(), originalSnapshot);
      expect(store.closings.single.summary.allFreeCount, 3);
    },
  );

  test(
    'net price rows combine different discounts while unique package buyers are never summed across rows',
    () async {
      final first = await addStudent('S1', 40);
      final second = await addStudent('S2', 50);
      final buyer = await addStudent('P', 25);
      final session = await addSession();
      await enter(first, session, EntryMode.single);
      await store.saveGroup(store.groups.single.copyWith(sessionPrice: 12000));
      await enter(second, session, EntryMode.single);
      await store.renewPackage(
        PackageRequest(
          studentId: buyer.id,
          groupId: group.id,
          sessionId: session.id,
        ),
      );
      await store.saveGroup(store.groups.single.copyWith(packagePrice: 60000));
      await store.saveStudent(
        store.students.last.copyWith(discountPercent: 50),
      );
      await store.renewPackage(
        PackageRequest(
          studentId: buyer.id,
          groupId: group.id,
          sessionId: session.id,
        ),
      );
      await store.saveGroup(store.groups.single.copyWith(packagePrice: 70000));
      await store.renewPackage(
        PackageRequest(
          studentId: buyer.id,
          groupId: group.id,
          sessionId: session.id,
        ),
      );
      final summary = store.sessionFinancialSummary(session.id);
      final sameSingles = summary.paymentAmountCategories!.singleWhere(
        (e) => e.kind == SessionStudentCategoryKind.single,
      );
      expect(
        [
          sameSingles.unitAmount,
          sameSingles.studentCount,
          sameSingles.operationCount,
        ],
        [6000, 2, 2],
      );
      final samePackages = summary.paymentAmountCategories!.singleWhere(
        (e) =>
            e.kind == SessionStudentCategoryKind.package &&
            e.unitAmount == 30000,
      );
      expect([samePackages.studentCount, samePackages.operationCount], [1, 2]);
      expect(
        summary.paymentAmountCategories!.where(
          (e) => e.kind == SessionStudentCategoryKind.package,
        ),
        hasLength(2),
      );
      expect(summary.packageBuyerCount, 1);
      expect(summary.packageSalesCount, 3);
      expect(
        summary.studentCategories!.where(
          (e) => e.kind == SessionStudentCategoryKind.single,
        ),
        hasLength(2),
      );
      expect(summary.allFreeCount, 0);
      // A package sold to a student who has not entered must not count as an attendee.
      expect(
        summary.attendanceDiscountCategories!.fold(
          0,
          (sum, e) => sum + e.studentCount,
        ),
        2,
      );
      await store.closeSession(session.id);
      await store.finalizeSession(
        sessionId: session.id,
        actualCash: summary.expectedCash,
      );
      await reopenStore();
      expect(store.closings.single.summary.packageBuyerCount, 1);
    },
  );

  test(
    'legacy attendance and closing fields remain explicitly unknown and new closing groups unknown rows',
    () async {
      final student = await addStudent('S1', 70);
      final session = await addSession(kind: SessionKind.free);
      await enter(student, session, EntryMode.single);
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final databasePath = store.databasePath;
      await store.close();
      final db = await databaseFactoryFfi.openDatabase(databasePath);
      final rows = await db.query('state');
      final state =
          jsonDecode(rows.single['payload'] as String) as Map<String, dynamic>;
      for (final row in state['attendances'] as List) {
        (row as Map).remove('fixedDiscountPercent');
      }
      final closing = (state['closings'] as List).single as Map;
      final summary = closing['summary'] as Map;
      for (final key in [
        'attendanceDiscountCategories',
        'allFreeCount',
        'paymentAmountCategories',
        'packageBuyerCount',
      ]) {
        summary.remove(key);
      }
      await db.update(
        'state',
        {'payload': jsonEncode(state)},
        where: 'id = ?',
        whereArgs: [1],
      );
      await db.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.attendances.single.fixedDiscountPercent, isNull);
      expect(
        store.closings.single.summary.attendanceDiscountCategories,
        isNull,
      );
      expect(store.closings.single.summary.allFreeCount, isNull);
      expect(store.closings.single.summary.packageBuyerCount, isNull);
      expect(store.closings.single.summary.paymentAmountCategories, isNull);
      expect(
        store
            .sessionFinancialSummary(session.id)
            .attendanceDiscountCategories!
            .single
            .discountPercent,
        isNull,
      );
      final backup = await store.createBackup(
        destination: '${directory.path}/legacy.json',
      );
      await store.restoreBackup(backup);
      await store.signIn('مدير', 'test-pass-123');
      await store.reopenFinancialClosing(
        closingId: store.closings.single.id,
        reason: 'مراجعة جديدة',
      );
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      expect(
        store
            .closings
            .single
            .summary
            .attendanceDiscountCategories!
            .single
            .discountPercent,
        isNull,
      );
      expect(store.closings.single.summary.allFreeCount, 1);
      await reopenStore();
      expect(store.allClosings.first.summary.allFreeCount, isNull);
      expect(store.closings.single.summary.allFreeCount, 1);
    },
  );
  test(
    'forged fixed-discount and closing counts reject restore; SQLite note failure rolls back without false audit',
    () async {
      final student = await addStudent('S1', 70);
      await store.saveStudentNote(studentId: student.id, notes: 'الأصل');
      final session = await addSession(kind: SessionKind.free);
      await enter(student, session, EntryMode.single);
      await store.closeSession(session.id);
      await store.finalizeSession(sessionId: session.id, actualCash: 0);
      final backup = await store.createBackup(
        destination: '${directory.path}/valid.json',
      );
      final original =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      for (final kind in ['attendanceDiscount', 'closingCount']) {
        final forged = jsonDecode(jsonEncode(original)) as Map<String, dynamic>;
        final state = forged['data'] as Map;
        if (kind == 'attendanceDiscount') {
          (state['attendances'] as List).single['fixedDiscountPercent'] = 101;
        } else {
          (state['closings'] as List)
                  .single['summary']['attendanceDiscountCategories'][0]['studentCount'] =
              2;
        }
        final file = File('${directory.path}/forged-$kind.json');
        await file.writeAsString(jsonEncode(forged));
        await expectLater(
          store.restoreBackup(file.path),
          throwsA(isA<CenterException>()),
        );
        expect(store.students.single.notes, 'الأصل');
        expect(store.attendances.single.fixedDiscountPercent, 70);
        expect(
          store
              .closings
              .single
              .summary
              .attendanceDiscountCategories!
              .single
              .studentCount,
          1,
        );
      }
      final auditCount = store.audit.length;
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      await db.execute(
        "CREATE TRIGGER reject_note BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'blocked'); END",
      );
      await expectLater(
        store.saveStudentNote(studentId: student.id, notes: 'لم تحفظ'),
        throwsA(isA<CenterException>()),
      );
      expect(store.students.single.notes, 'الأصل');
      expect(store.audit.length, auditCount);
      await db.execute('DROP TRIGGER reject_note');
      await db.close();
      await reopenStore();
      expect(store.students.single.notes, 'الأصل');
      expect(store.audit.length, auditCount);
    },
  );
}
