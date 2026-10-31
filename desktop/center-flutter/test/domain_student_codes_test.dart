import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'student-code-pass';
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('massar-student-codes-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('مدير', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
      ),
    );
    group = store.groups.single;
  });
  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });
  Student draft(String name, {String code = ''}) => Student(
    name: name,
    code: code,
    groupIds: [group.id],
    createdAt: DateTime.now(),
  );
  Future<void> reopen() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('مدير', password);
  }

  test(
    'concurrent registrations allocate unique persisted numeric codes rather than draft codes or preview reservations',
    () async {
      expect(store.nextStudentCode, '1000');
      expect(store.nextStudentCode, '1000');
      final created = await Future.wait(
        List.generate(
          6,
          (index) => store.registerStudent(
            draft('طالب $index', code: 'ignored-$index'),
          ),
        ),
      );
      expect(created.map((student) => student.code).toSet(), {
        '1000',
        '1001',
        '1002',
        '1003',
        '1004',
        '1005',
      });
      expect(created.map((student) => student.id).toSet(), hasLength(6));
      for (final student in created) {
        final persisted = store.students.singleWhere(
          (saved) => saved.id == student.id,
        );
        expect(persisted.toJson(), student.toJson());
        expect(persisted.name, student.name);
      }
      expect(store.nextStudentCode, '1006');
      await reopen();
      expect(store.nextStudentCode, '1006');
      final next = await store.registerStudent(draft('بعد الفتح'));
      expect(next.code, '1006');
      expect(
        store.students.map((student) => student.code).toSet(),
        hasLength(7),
      );
    },
  );

  test(
    'mixed legacy codes remain unchanged while registration fills gaps through backup and restart',
    () async {
      const codes = [
        'LEGACY-9',
        '1000',
        '٠٠٢٠٠٠',
        '۲۰۰۱',
        '0002002',
        'MS-90000',
      ];
      for (final code in codes) {
        await store.saveStudent(draft('قديم $code', code: code));
      }
      final preserved = store.students
          .map((student) => student.toJson())
          .toList();
      expect(store.nextStudentCode, '1001');
      final backup = await store.createBackup(
        destination: '${directory.path}/mixed-legacy.json',
      );
      await store.restoreBackup(backup);
      await store.signIn('مدير', password);
      expect(
        store.students.map((student) => student.toJson()).toList(),
        preserved,
      );
      await reopen();
      expect(store.students.map((student) => student.code), codes);
      final registered = await store.registerStudent(
        draft('جديد', code: 'manual-invalid'),
      );
      expect(registered.code, '1001');
      await store.saveStudent(draft('كود فارغ', code: '   '));
      expect(store.students.last.code, '1002');
      expect(
        store.students.take(codes.length).map((student) => student.code),
        codes,
      );
    },
  );

  test(
    'four digit gaps skip legacy aliases, barcodes and suspended students before extending a high sequence',
    () async {
      for (final code in ['1000', '01001', '١٠٠٣', '99999999999999999999999']) {
        await store.saveStudent(draft('قديم $code', code: code));
      }
      await store.saveStudent(
        draft('باركود محجوز', code: 'LEGACY-A').copyWith(barcode: ' ۱۰۰۲ '),
      );
      await store.suspendStudent(
        studentId: store.students.first.id,
        reason: 'إيقاف تجريبي',
      );
      final original = store.students
          .map((student) => student.toJson())
          .toList();
      expect(store.nextStudentCode, '1004');
      final registered = await store.registerStudent(draft('ملء الفراغ'));
      expect(registered.code, '1004');
      expect(
        store.students.take(original.length).map((s) => s.toJson()),
        original,
      );
      await reopen();
      expect(store.nextStudentCode, '1005');
    },
  );

  test(
    'last four digit gap is filled before extending the sequence past a reserved barcode',
    () async {
      final backupPath = await store.createBackup(
        destination: '${directory.path}/four-digit-capacity.json',
      );
      final backup = jsonDecode(await File(backupPath).readAsString()) as Map;
      final backupState = backup['data'] as Map;
      backupState['students'] = [
        for (var code = 1000; code < 9999; code++)
          draft(
            'قديم $code',
            code: '$code',
          ).copyWith(id: 'legacy-$code').toJson(),
        draft(
          'تسلسل سابق',
          code: '10001',
        ).copyWith(id: 'extended', barcode: '١٠٠٠٢').toJson(),
      ];
      backupState['enrollments'] = {
        for (final student in backupState['students'] as List)
          '${student['id']}:${group.id}': student['createdAt'],
      };
      await File(backupPath).writeAsString(jsonEncode(backup));
      await store.restoreBackup(backupPath);
      await store.signIn('مدير', password);
      expect(store.nextStudentCode, '9999');
      expect((await store.registerStudent(draft('آخر فراغ'))).code, '9999');
      expect(store.nextStudentCode, '10003');
      expect((await store.registerStudent(draft('رقم جديد'))).code, '10003');
      await reopen();
      expect(store.nextStudentCode, '10004');
    },
  );

  test(
    'restored legacy code with surrounding spaces remains literal while normal profile updates accept editor trimming',
    () async {
      await store.saveStudent(draft('طالب قديم', code: 'OLD-CODE'));
      final backupPath = await store.createBackup(
        destination: '${directory.path}/legacy-whitespace.json',
      );
      final backup =
          jsonDecode(await File(backupPath).readAsString())
              as Map<String, dynamic>;
      final data = backup['data'] as Map<String, dynamic>;
      ((data['students'] as List).single as Map)['code'] = ' OLD-CODE ';
      final restoredPath = '${directory.path}/imported-whitespace.json';
      await File(restoredPath).writeAsString(jsonEncode(backup));
      await store.restoreBackup(restoredPath);
      await store.signIn('مدير', password);
      final legacy = store.students.single;
      expect(legacy.code, ' OLD-CODE ');
      await store.saveStudent(
        legacy.copyWith(
          code: legacy.code.trim(),
          name: 'اسم محدث',
          phone: '01011111111',
        ),
      );
      expect(store.students.single.code, ' OLD-CODE ');
      expect(store.students.single.name, 'اسم محدث');
      final auditCount = store.audit.length;
      await expectLater(
        store.saveStudent(
          store.students.single.copyWith(code: 'DIFFERENT', name: 'لا يحفظ'),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.students.single.name, 'اسم محدث');
      expect(store.audit.length, auditCount);
      await reopen();
      expect(store.students.single.code, ' OLD-CODE ');
      expect(store.students.single.phone, '01011111111');
    },
  );

  for (final role in [StaffRole.admin, StaffRole.cashier]) {
    test(
      '${role.name} can update student details but code replacement atomically rejects every accompanying edit and audit',
      () async {
        await store.saveStudent(draft('الاسم الأصلي', code: 'OLD-CODE'));
        final original = store.students.single;
        if (role == StaffRole.cashier) {
          await store.saveStaff(
            name: 'cashier',
            password: password,
            role: role,
          );
          await store.signIn('cashier', password);
        }
        final before = original.toJson();
        final audits = store.audit.length;
        await expectLater(
          store.saveStudent(
            original.copyWith(
              code: '1000',
              name: 'غير محفوظ',
              phone: '01099999999',
              notes: 'غير محفوظة',
            ),
          ),
          throwsA(isA<CenterException>()),
        );
        expect(store.students.single.toJson(), before);
        expect(store.audit.length, audits);
        expect(store.nextStudentCode, '1000');
        await store.saveStudent(
          original.copyWith(name: 'اسم محدث', phone: '01011111111'),
        );
        expect(store.students.single.name, 'اسم محدث');
        expect(store.students.single.code, 'OLD-CODE');
        final created = await store.registerStudent(draft('طالب جديد'));
        expect(created.code, '1000');
        await reopen();
        expect(store.students.first.code, 'OLD-CODE');
        expect(store.students.first.name, 'اسم محدث');
      },
    );
  }

  test(
    'registration denies assistants/signedout, invalid enrollment, and existing identities without consuming a code',
    () async {
      final original = await store.registerStudent(draft('الأصل'));
      final audits = store.audit.length;
      await expectLater(
        store.registerStudent(original.copyWith(name: 'محاولة إعادة تسجيل')),
        throwsA(isA<CenterException>()),
      );
      await expectLater(
        store.registerStudent(
          Student(
            name: 'مجموعة غير موجودة',
            groupIds: ['missing-group'],
            createdAt: DateTime.now(),
          ),
        ),
        throwsA(isA<CenterException>()),
      );
      expect(store.students.single.toJson(), original.toJson());
      expect(store.audit.length, audits);
      expect(store.nextStudentCode, '1001');
      await store.saveStaff(
        name: 'assistant',
        password: password,
        role: StaffRole.assistant,
      );
      await store.signIn('assistant', password);
      final assistantAudits = store.audit.length;
      await expectLater(
        store.registerStudent(draft('غير مسموح')),
        throwsA(isA<CenterException>()),
      );
      expect(store.audit.length, assistantAudits);
      expect(store.nextStudentCode, '1001');
      store.signOut();
      await expectLater(
        store.registerStudent(draft('بعد الخروج')),
        throwsA(isA<CenterException>()),
      );
      expect(store.students, hasLength(1));
      expect(store.nextStudentCode, '1001');
    },
  );

  test(
    'SQLite commit failure rolls back generated identity and code so retry persists the same next code',
    () async {
      final db = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      final audits = store.audit.length;
      try {
        await db.execute(
          "CREATE TRIGGER reject_registration BEFORE INSERT ON state_records BEGIN SELECT RAISE(ABORT, 'blocked'); END",
        );
        await expectLater(
          store.registerStudent(draft('لم يحفظ')),
          throwsA(
            isA<CenterException>().having(
              (error) => error.cause,
              'SQLite diagnostic cause',
              isNotNull,
            ),
          ),
        );
        expect(store.students, isEmpty);
        expect(store.audit.length, audits);
        expect(store.nextStudentCode, '1000');
        final persisted =
            jsonDecode((await db.query('state')).single['payload'] as String)
                as Map;
        expect(persisted['students'], isEmpty);
        await db.execute('DROP TRIGGER reject_registration');
      } finally {
        await db.close();
      }
      await reopen();
      expect(store.nextStudentCode, '1000');
      final saved = await store.registerStudent(draft('المحاولة الناجحة'));
      expect(saved.code, '1000');
      await reopen();
      expect(store.students.single.id, saved.id);
      expect(store.students.single.code, '1000');
      expect(store.nextStudentCode, '1001');
    },
  );
}
