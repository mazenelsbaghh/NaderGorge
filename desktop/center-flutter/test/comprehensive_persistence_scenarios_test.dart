import 'dart:convert';
import 'dart:io';

import 'package:cryptography/cryptography.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  const password = 'temporary-owner-password';
  late InstallationAdmin owner;
  late Directory directory;
  late CenterStore store;
  late StudyGroup group;
  late LessonSession session;
  late Student firstStudent;
  late Student secondStudent;

  setUpAll(() async {
    final salt = List<int>.generate(24, (i) => i + 1);
    final key = await Pbkdf2(
      macAlgorithm: Hmac.sha256(),
      iterations: 120000,
      bits: 256,
    ).deriveKey(secretKey: SecretKey(utf8.encode(password)), nonce: salt);
    owner = InstallationAdmin(
      id: 'temporary-installed-owner',
      name: 'temporary-owner',
      credential: {
        'salt': base64Encode(salt),
        'hash': base64Encode(await key.extractBytes()),
        'algorithm': 'pbkdf2-sha256-120000',
      },
    );
  });

  setUp(() async {
    directory = await Directory.systemTemp.createTemp(
      'massar-comprehensive-persistence-',
    );
    store = await CenterStore.open(directory: directory.path);
    await store.ensureInstallationAdmin(owner);
    await store.signIn(owner.name, password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة الاختبار',
        subjectId: store.catalogs[0].id,
        centerId: store.catalogs[1].id,
        gradeId: store.catalogs[2].id,
        sessionPrice: 12345,
        packagePrice: 41000,
      ),
    );
    group = store.groups.single;
    for (final name in ['الطالب الأول', 'الطالب الثاني']) {
      await store.registerStudent(
        Student(
          name: name,
          groupIds: [group.id],
          createdAt: DateTime.now().subtract(const Duration(days: 1)),
        ),
      );
    }
    firstStudent = store.students[0];
    secondStudent = store.students[1];
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'شهر الاختبار',
        lessons: [const PreparedLesson(number: 1)],
      ),
    );
    await store.saveSession(
      LessonSession(
        preparedLessonId: month.lessons.single.id,
        monthNumber: month.number,
        groupId: group.id,
        number: 1,
        startsAt: DateTime.now().add(const Duration(hours: 1)),
        createdAt: DateTime.now(),
      ),
    );
    session = store.sessions.single;
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  EntryRequest singleEntry(Student student) => EntryRequest(
    studentId: student.id,
    sessionId: session.id,
    mode: EntryMode.single,
  );

  Future<Object?> outcome(Future<void> command) async {
    try {
      await command;
      return null;
    } catch (error) {
      return error;
    }
  }

  Future<String> storedPayload() async {
    final database = await databaseFactoryFfi.openDatabase(
      store.databasePath,
      options: OpenDatabaseOptions(singleInstance: false),
    );
    try {
      final rows = await database.query('state', where: 'id = 1');
      expect(rows, hasLength(1));
      return rows.single['payload']! as String;
    } finally {
      await database.close();
    }
  }

  Future<void> reopenAndSignIn() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.ensureInstallationAdmin(owner);
    expect(store.currentUser, isNull);
    await store.signIn(owner.name, password);
  }

  for (final corruption in ['invalid-utf8', 'truncated-json', 'oversized']) {
    test(
      '$corruption restore preserves persisted money, identities, audit and login',
      () async {
        await store.collectAndAttend(singleEntry(firstStudent));
        final before = await storedPayload();
        final auditIds = store.audit.map((e) => e.id).toList();
        final source = File('${directory.path}/$corruption.json');
        if (corruption == 'invalid-utf8') {
          await source.writeAsBytes([0xc3, 0x28], flush: true);
        } else if (corruption == 'truncated-json') {
          await source.writeAsString(
            '{"format":"massar-center-backup","data":',
            flush: true,
          );
        } else {
          // Sparse length exercises the boundary without allocating 100 MiB.
          final file = await source.open(mode: FileMode.write);
          try {
            await file.truncate(100 * 1024 * 1024 + 1);
          } finally {
            await file.close();
          }
        }
        await expectLater(
          store.restoreBackup(source.path),
          throwsA(
            isA<CenterException>().having(
              (error) => error.message,
              'safe restoration error',
              corruption == 'oversized'
                  ? 'ملف النسخة الاحتياطية أكبر من الحد المسموح.'
                  : 'النسخة غير صالحة؛ لم تتغير البيانات.',
            ),
          ),
        );
        expect(await storedPayload(), before);
        expect(store.currentUser?.id, owner.id);
        expect(store.audit.map((e) => e.id), auditIds);
        expect(store.payments.single.netAmount, 12345);
        expect(store.attendances.single.studentId, firstStudent.id);
        await reopenAndSignIn();
        expect(await storedPayload(), before);
        await store.collectAndAttend(singleEntry(secondStudent));
        expect(store.payments.map((e) => e.netAmount), [12345, 12345]);
      },
    );
  }

  test(
    'restore rejects two accounts competing for installed owner identity without replacing either live identity',
    () async {
      await store.collectAndAttend(singleEntry(firstStudent));
      final before = await storedPayload();
      final backup = await store.createBackup(
        destination: '${directory.path}/original.json',
      );
      final value = jsonDecode(await File(backup).readAsString()) as Map;
      final data = value['data'] as Map;
      final staff = data['staff'] as List;
      (staff.single as Map)['name'] = 'different-backup-manager';
      staff.add({
        'id': 'second-competing-owner',
        'name': owner.name,
        'role': StaffRole.admin.name,
      });
      (data['credentials'] as Map)['second-competing-owner'] = Map.of(
        owner.credential,
      );
      final source = File('${directory.path}/identity-collision.json');
      await source.writeAsString(jsonEncode(value), flush: true);
      await expectLater(
        store.restoreBackup(source.path),
        throwsA(
          isA<CenterException>().having(
            (error) => error.message,
            'identity collision',
            'حساب مدير التثبيت يتعارض مع حسابين في النسخة. لم تتغير البيانات.',
          ),
        ),
      );
      expect(await storedPayload(), before);
      expect(store.currentUser?.id, owner.id);
      expect(store.staff.single.name, owner.name);
      expect(store.canConfigureCards, isTrue);
      await reopenAndSignIn();
      expect(await storedPayload(), before);
      expect(store.payments.single.netAmount, 12345);
    },
  );

  test(
    'queued collection rechecks authentication after restore and preservation contains the displaced cash entry',
    () async {
      final backup = await store.createBackup(
        destination: '${directory.path}/before-entry.json',
      );
      await store.collectAndAttend(singleEntry(firstStudent));
      final displacedPayment = store.payments.single;
      final results = await Future.wait([
        outcome(store.restoreBackup(backup)),
        outcome(store.collectAndAttend(singleEntry(secondStudent))),
      ]);
      expect(results[0], isNull);
      expect(results[1], isA<CenterException>());
      expect(store.currentUser, isNull);
      expect(store.payments, isEmpty);
      expect(store.attendances, isEmpty);
      expect(store.audit.last.action, 'backup_restore');
      expect(store.audit.where((e) => e.action == 'entry'), isEmpty);
      final preserved = await Directory(
        '${directory.path}/backups',
      ).list().where((file) => file.path.contains('before-restore-')).toList();
      expect(preserved, hasLength(1));
      final value =
          jsonDecode(await File(preserved.single.path).readAsString()) as Map;
      expect((value['data'] as Map)['payments'], [displacedPayment.toJson()]);
      await reopenAndSignIn();
      expect(store.payments, isEmpty);
      await store.collectAndAttend(singleEntry(secondStudent));
      expect(store.payments.single.studentId, secondStudent.id);
    },
  );

  test(
    'queued write before close commits and a write behind close cannot overwrite its persisted snapshot',
    () async {
      final results = await Future.wait([
        outcome(store.collectAndAttend(singleEntry(firstStudent))),
        outcome(store.close()),
        outcome(store.collectAndAttend(singleEntry(secondStudent))),
      ]);
      expect(results[0], isNull);
      expect(results[1], isNull);
      expect(
        results[2],
        isA<CenterException>().having(
          (error) => error.message,
          'closed store',
          'تم إغلاق ملف البيانات.',
        ),
      );
      await reopenAndSignIn();
      expect(store.payments.single.studentId, firstStudent.id);
      expect(store.payments.single.netAmount, 12345);
      expect(store.attendances.single.studentId, firstStudent.id);
      expect(store.audit.where((e) => e.action == 'entry'), hasLength(1));
      await store.collectAndAttend(singleEntry(secondStudent));
      expect(store.payments, hasLength(2));
    },
  );

  test(
    'missing snapshot during SQL update rolls back the deleted row and the next queued payment still commits',
    () async {
      final database = await databaseFactoryFfi.openDatabase(
        store.databasePath,
        options: OpenDatabaseOptions(singleInstance: false),
      );
      final previousAudits = store.audit.map((e) => e.id).toList();
      try {
        await database.execute(
          '''CREATE TRIGGER remove_snapshot BEFORE INSERT ON state_records
          WHEN NEW.section='payments' AND json_extract(NEW.payload, '\$.studentId') = '${firstStudent.id}'
          BEGIN DELETE FROM state WHERE id = 1; END''',
        );
        final results = await Future.wait([
          outcome(store.collectAndAttend(singleEntry(firstStudent))),
          outcome(store.collectAndAttend(singleEntry(secondStudent))),
        ]);
        expect(
          results[0],
          isA<CenterException>().having(
            (error) => error.message,
            'missing SQL snapshot',
            'سجل قاعدة البيانات مفقود؛ لم تُحفظ العملية.',
          ),
        );
        expect(results[1], isNull);
        expect(store.payments.single.studentId, secondStudent.id);
        expect(store.payments.single.netAmount, 12345);
        expect(store.attendances.single.studentId, secondStudent.id);
        expect(store.packages, isEmpty);
        expect(
          store.audit.take(previousAudits.length).map((e) => e.id),
          previousAudits,
        );
        expect(store.audit, hasLength(previousAudits.length + 1));
        expect(store.audit.last.staffId, owner.id);
        final payload = jsonDecode(await storedPayload()) as Map;
        expect(
          (payload['payments'] as List).single,
          store.payments.single.toJson(),
        );
      } finally {
        await database.execute('DROP TRIGGER IF EXISTS remove_snapshot');
        await database.close();
      }
      await reopenAndSignIn();
      expect(store.payments.single.studentId, secondStudent.id);
      expect(store.attendances.single.studentId, secondStudent.id);
      await store.collectAndAttend(singleEntry(firstStudent));
      expect(store.payments, hasLength(2));
    },
  );

  test(
    'queued role changes authorize each payment at execution and denied assistant entry leaves no financial audit',
    () async {
      await store.saveStaff(
        name: 'temporary-assistant',
        password: 'temporary-assistant-password',
        role: StaffRole.assistant,
      );
      final audits = store.audit.length;
      final results = await Future.wait([
        outcome(
          store.signIn('temporary-assistant', 'temporary-assistant-password'),
        ),
        outcome(store.collectAndAttend(singleEntry(firstStudent))),
        outcome(store.signIn(owner.name, password)),
        outcome(store.collectAndAttend(singleEntry(secondStudent))),
      ]);
      expect(results[0], isNull);
      expect(results[1], isA<CenterException>());
      expect(results[2], isNull);
      expect(results[3], isNull);
      expect(store.currentUser?.id, owner.id);
      expect(store.payments.single.studentId, secondStudent.id);
      expect(store.attendances.single.studentId, secondStudent.id);
      expect(store.audit, hasLength(audits + 1));
      expect(store.audit.last.action, 'entry');
      expect(store.audit.last.staffId, owner.id);
      await reopenAndSignIn();
      expect(store.payments.single.studentId, secondStudent.id);
      expect(store.payments.single.netAmount, 12345);
    },
  );
}
