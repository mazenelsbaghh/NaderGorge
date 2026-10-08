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
  late List<Student> students;
  late LessonSession session;
  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-category-');
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
    final discounts = [0, 50, 100, 0, 25, 0, 0];
    for (var i = 0; i < discounts.length; i++) {
      await store.saveStudent(
        Student(
          name: 'طالب $i',
          code: 'S$i',
          groupIds: [group.id],
          discountPercent: discounts[i],
          createdAt: DateTime.now().subtract(const Duration(days: 1)),
        ),
      );
    }
    students = store.students;
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 1,
        startsAt: DateTime.now().add(const Duration(hours: 1)),
        createdAt: DateTime.now(),
      ),
    );
    await store.renewPackage(
      PackageRequest(studentId: students[5].id, groupId: group.id),
    );
    await store.closeSession(store.sessions.single.id);
    final absence = store.attendances.firstWhere(
      (e) => e.studentId == students[5].id,
    );
    await store.renewPackage(
      PackageRequest(studentId: students[4].id, groupId: group.id),
    );
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 2,
        startsAt: DateTime.now().add(const Duration(hours: 2)),
        createdAt: DateTime.now(),
      ),
    );
    session = store.sessions.last;
    for (var i = 0; i < 5; i++) {
      await store.collectAndAttend(
        EntryRequest(
          studentId: students[i].id,
          sessionId: session.id,
          mode: i < 3 ? EntryMode.single : EntryMode.package,
        ),
      );
    }
    await store.renewPackage(
      PackageRequest(
        studentId: students[3].id,
        groupId: group.id,
        sessionId: session.id,
      ),
    );
    await store.renewPackage(
      PackageRequest(
        studentId: students[4].id,
        groupId: group.id,
        sessionId: session.id,
      ),
    );
    await store.collectAndAttend(
      EntryRequest(
        studentId: students[5].id,
        sessionId: session.id,
        mode: EntryMode.makeup,
        originalAttendanceId: absence.id,
      ),
    );
    await store.closeSession(session.id);
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });
  SessionStudentCategory category(
    SessionFinancialSummary summary,
    SessionStudentCategoryKind kind, {
    int? percent,
  }) => summary.studentCategories!.singleWhere(
    (e) => e.kind == kind && (percent == null || e.discountPercent == percent),
  );

  test(
    'categories use immutable discounts and distinct students, not package operation count',
    () async {
      final summary = store.sessionFinancialSummary(session.id);
      final full = category(
        summary,
        SessionStudentCategoryKind.single,
        percent: 0,
      );
      final discount = category(
        summary,
        SessionStudentCategoryKind.single,
        percent: 50,
      );
      final exempt = category(summary, SessionStudentCategoryKind.free);
      expect(
        [full.studentCount, full.operationCount, full.unitAmount],
        [1, 1, 10000],
      );
      expect([discount.studentCount, discount.unitAmount], [1, 5000]);
      expect(exempt.unitAmount, 0);
      expect(exempt.label, contains('إعفاء'));
      expect(
        summary.studentCategories!.where(
          (e) =>
              e.kind == SessionStudentCategoryKind.single &&
              e.discountPercent == 100,
        ),
        isEmpty,
      );
      final packs = category(
        summary,
        SessionStudentCategoryKind.package,
        percent: 0,
      );
      expect(
        [packs.studentCount, packs.operationCount, packs.unitAmount],
        [1, 2, 40000],
      );
      expect(
        category(
          summary,
          SessionStudentCategoryKind.package,
          percent: 25,
        ).unitAmount,
        30000,
      );
      expect(
        category(summary, SessionStudentCategoryKind.prepaid).studentCount,
        1,
      );
      expect(
        category(summary, SessionStudentCategoryKind.prepaid).operationCount,
        0,
      );
      expect(
        category(summary, SessionStudentCategoryKind.makeup).studentCount,
        1,
      );
      expect(
        category(summary, SessionStudentCategoryKind.absent).studentCount,
        1,
      );
      expect(summary.totalCollected, 125000);
      await store.finalizeSession(sessionId: session.id, actualCash: 125000);
      final saved = store.closings.single;
      final snapshot = jsonEncode(saved.summary.toJson());
      await store.saveStudent(students[1].copyWith(discountPercent: 20));
      await store.saveGroup(
        group.copyWith(sessionPrice: 20000, packagePrice: 80000),
      );
      expect(jsonEncode(store.closings.single.summary.toJson()), snapshot);
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(jsonEncode(store.closings.single.summary.toJson()), snapshot);
      expect(
        () => store.closings.single.summary.studentCategories!.add(full),
        throwsUnsupportedError,
      );
    },
  );
  test(
    'prior package holders retain original full discounted and exempt purchase categories in every class',
    () async {
      for (final i in [0, 1, 2]) {
        await store.renewPackage(
          PackageRequest(studentId: students[i].id, groupId: group.id),
        );
      }
      for (final i in [0, 1, 2]) {
        await store.saveStudent(
          students[i].copyWith(discountPercent: i == 0 ? 20 : 0),
        );
      }
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 3,
          startsAt: DateTime.now().add(const Duration(hours: 3)),
          createdAt: DateTime.now(),
        ),
      );
      final next = store.sessions.last;
      for (final i in [0, 1, 2]) {
        await store.collectAndAttend(
          EntryRequest(
            studentId: students[i].id,
            sessionId: next.id,
            mode: EntryMode.package,
          ),
        );
      }
      await store.closeSession(next.id);
      final summary = store.sessionFinancialSummary(next.id);
      final prepaid = summary.studentCategories!
          .where((e) => e.kind == SessionStudentCategoryKind.prepaid)
          .toList();
      expect(prepaid.map((e) => e.discountPercent), [0, 50, 100]);
      expect(prepaid.map((e) => e.studentCount), [1, 1, 1]);
      expect(prepaid.map((e) => e.operationCount), [0, 0, 0]);
      expect(prepaid.map((e) => e.unitAmount), [0, 0, 0]);
      expect(prepaid.first.label, contains('بالسعر الكامل'));
      expect(prepaid[1].label, contains('50٪'));
      expect(prepaid.last.label, contains('إعفاء 100٪'));
      expect(summary.totalCollected, 0);
      expect(summary.expectedCash, 0);
      expect(summary.packageSalesCount, 0);
      await store.finalizeSession(sessionId: next.id, actualCash: 0);
      final old = store.closings.single;
      await store.reopenFinancialClosing(
        closingId: old.id,
        reason: 'تدقيق الحضور',
      );
      await store.reverseEntry(
        attendanceId: store.attendances
            .firstWhere(
              (e) => e.studentId == students[0].id && e.sessionId == next.id,
            )
            .id,
        reason: 'كان غائب',
      );
      await store.finalizeSession(sessionId: next.id, actualCash: 0);
      expect(
        store.allClosings.first.summary.studentCategories!
            .where((e) => e.kind == SessionStudentCategoryKind.prepaid)
            .map((e) => e.discountPercent),
        [0, 50, 100],
      );
      expect(
        store.closings.single.summary.studentCategories!
            .where((e) => e.kind == SessionStudentCategoryKind.prepaid)
            .map((e) => e.discountPercent),
        [50, 100],
      );
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(
        category(
          store.allClosings.first.summary,
          SessionStudentCategoryKind.prepaid,
          percent: 100,
        ).studentCount,
        1,
      );
      expect(store.closings.single.summary.totalCollected, 0);
    },
  );
  test(
    'earlier aggregate prior-package category snapshot stays readable without invented discount detail',
    () async {
      await store.finalizeSession(sessionId: session.id, actualCash: 125000);
      final backup = await store.createBackup();
      final original =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final rows =
          (((original['data'] as Map)['closings'] as List)
                  .single['summary']['studentCategories']
              as List);
      final prepaid = rows.singleWhere((e) => e['kind'] == 'prepaid') as Map;
      prepaid['discountPercent'] = null;
      prepaid['label'] = 'حضور من باقة مدفوعة سابقًا';
      final legacy = File('${directory.path}/aggregate-prepaid-category.json');
      await legacy.writeAsString(jsonEncode(original));
      await store.restoreBackup(legacy.path);
      await store.signIn('مدير', 'test-pass-123');
      final old = store.closings.single;
      expect(
        category(
          old.summary,
          SessionStudentCategoryKind.prepaid,
        ).discountPercent,
        isNull,
      );
      await store.reopenFinancialClosing(
        closingId: old.id,
        reason: 'إعادة مراجعة',
      );
      await store.finalizeSession(sessionId: session.id, actualCash: 125000);
      expect(
        category(
          store.allClosings.first.summary,
          SessionStudentCategoryKind.prepaid,
        ).discountPercent,
        isNull,
      );
      expect(
        category(
          store.closings.single.summary,
          SessionStudentCategoryKind.prepaid,
        ).discountPercent,
        25,
      );
    },
  );
  test('same discount remains split by actual charged unit price', () async {
    await store.saveGroup(group.copyWith(packagePrice: 80000));
    await store.saveSession(
      LessonSession(
        groupId: group.id,
        number: 3,
        startsAt: DateTime.now().add(const Duration(hours: 3)),
        createdAt: DateTime.now(),
      ),
    );
    final next = store.sessions.last;
    await store.renewPackage(
      PackageRequest(
        studentId: students[3].id,
        groupId: group.id,
        sessionId: next.id,
      ),
    );
    await store.saveGroup(group.copyWith(packagePrice: 40000));
    await store.renewPackage(
      PackageRequest(
        studentId: students[3].id,
        groupId: group.id,
        sessionId: next.id,
      ),
    );
    final rows = store
        .sessionFinancialSummary(next.id)
        .studentCategories!
        .where((e) => e.kind == SessionStudentCategoryKind.package)
        .toList();
    expect(rows.map((e) => e.unitAmount), [40000, 80000]);
    expect(rows.map((e) => e.studentCount), [1, 1]);
    expect(rows.map((e) => e.operationCount), [1, 1]);
    expect(rows.map((e) => e.discountPercent), [0, 0]);
    expect(store.sessionFinancialSummary(next.id).totalCollected, 120000);
  });
  test(
    'free attendance remains separate from a recorded 100 percent payment exemption',
    () async {
      await store.saveSession(
        LessonSession(
          groupId: group.id,
          number: 3,
          kind: SessionKind.free,
          startsAt: DateTime.now().add(const Duration(hours: 3)),
          createdAt: DateTime.now(),
        ),
      );
      final free = store.sessions.last;
      for (final index in [0, 2]) {
        await store.collectAndAttend(
          EntryRequest(
            studentId: students[index].id,
            sessionId: free.id,
            mode: EntryMode.single,
          ),
        );
      }
      final summary = store.sessionFinancialSummary(free.id);
      expect(summary.totalCollected, 0);
      expect(
        category(summary, SessionStudentCategoryKind.free).studentCount,
        2,
      );
      expect(
        category(summary, SessionStudentCategoryKind.free).discountPercent,
        isNull,
      );
      expect(
        summary.studentCategories!.where(
          (e) => e.kind == SessionStudentCategoryKind.single,
        ),
        isEmpty,
      );
      expect(
        category(
          store.sessionFinancialSummary(session.id),
          SessionStudentCategoryKind.free,
        ).studentCount,
        1,
      );
    },
  );
  test(
    'reopen and refunds exclude active categories while original closed snapshot replays exactly',
    () async {
      await store.finalizeSession(sessionId: session.id, actualCash: 125000);
      final old = store.closings.single;
      final original = jsonEncode(old.summary.toJson());
      await store.reopenFinancialClosing(closingId: old.id, reason: 'تدقيق');
      final entry = store.attendances.firstWhere(
        (e) => e.studentId == students[0].id && e.sessionId == session.id,
      );
      await store.reverseEntry(attendanceId: entry.id, reason: 'دخول بالخطأ');
      final unused = store.packages.firstWhere(
        (e) => e.studentId == students[3].id && e.remaining == 4,
      );
      await store.refundPackage(packageId: unused.id, reason: 'تجديد مكرر');
      final active = store.sessionFinancialSummary(session.id);
      expect(
        active.studentCategories!.where(
          (e) =>
              e.kind == SessionStudentCategoryKind.single &&
              e.discountPercent == 0,
        ),
        isEmpty,
      );
      final packs = category(
        active,
        SessionStudentCategoryKind.package,
        percent: 0,
      );
      expect([packs.studentCount, packs.operationCount], [1, 1]);
      expect(
        category(active, SessionStudentCategoryKind.absent).studentCount,
        2,
      );
      expect(active.refundAmount, 50000);
      expect(active.totalCollected, 75000);
      await store.finalizeSession(sessionId: session.id, actualCash: 75000);
      expect(jsonEncode(store.allClosings.first.summary.toJson()), original);
      expect(
        category(
          store.closings.single.summary,
          SessionStudentCategoryKind.package,
          percent: 0,
        ).operationCount,
        1,
      );
      final backup = await store.createBackup();
      final forged =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final oldCategories =
          ((forged['data'] as Map)['closings'] as List)
                  .first['summary']['studentCategories']
              as List;
      oldCategories.first['studentCount'] = 9;
      final file = File('${directory.path}/forged-category.json');
      await file.writeAsString(jsonEncode(forged));
      await expectLater(
        store.restoreBackup(file.path),
        throwsA(isA<CenterException>()),
      );
      expect(jsonEncode(store.allClosings.first.summary.toJson()), original);
    },
  );
  test(
    'legacy SQLite and backup without categories preserve financial evidence and can be corrected',
    () async {
      await store.finalizeSession(sessionId: session.id, actualCash: 125000);
      final backup = await store.createBackup();
      final legacy =
          jsonDecode(await File(backup).readAsString()) as Map<String, dynamic>;
      final data = legacy['data'] as Map<String, dynamic>;
      ((data['closings'] as List).single['summary'] as Map).remove(
        'studentCategories',
      );
      final file = File('${directory.path}/legacy-category.json');
      await file.writeAsString(jsonEncode(legacy));
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      await db.update('state', {'payload': jsonEncode(data)}, where: 'id=1');
      await db.close();
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.closings.single.summary.studentCategories, isNull);
      expect(store.closings.single.summary.totalCollected, 125000);
      await store.restoreBackup(file.path);
      await store.signIn('مدير', 'test-pass-123');
      final old = store.closings.single;
      await store.reopenFinancialClosing(
        closingId: old.id,
        reason: 'تصحيح قديم',
      );
      await store.reverseEntry(
        attendanceId: store.attendances
            .firstWhere(
              (e) => e.studentId == students[0].id && e.sessionId == session.id,
            )
            .id,
        reason: 'دخول خاطئ',
      );
      await store.finalizeSession(sessionId: session.id, actualCash: 115000);
      expect(store.allClosings.first.summary.studentCategories, isNull);
      expect(store.closings.single.summary.studentCategories, isNotNull);
      expect(store.closings.single.summary.totalCollected, 115000);
      await store.close();
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('مدير', 'test-pass-123');
      expect(store.allClosings.first.summary.studentCategories, isNull);
      expect(store.closings.single.summary.studentCategories, isNotEmpty);
    },
  );
}
