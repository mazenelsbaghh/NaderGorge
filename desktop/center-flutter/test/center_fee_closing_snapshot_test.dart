import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/data/center_state_encoder.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Directory directory;
  late CenterStore store;
  late LessonSession session;
  late Student centerOnlyStudent, independentFeeStudent;
  const password = 'center-fee-closing-password';

  setUp(() async {
    directory = await Directory.systemTemp.createTemp('center-fee-closing-');
    store = await CenterStore.open(directory: directory.path);
    await store.setupAdmin('closing-manager', password);
    for (final kind in CatalogKind.values) {
      await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
    }
    await store.saveGroup(
      StudyGroup(
        name: 'مجموعة رسوم السنتر',
        subjectId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.subject)
            .id,
        centerId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.center)
            .id,
        gradeId: store.catalogs
            .firstWhere((entry) => entry.kind == CatalogKind.grade)
            .id,
        sessionPrice: 6000,
      ),
    );
    final group = store.groups.single;
    final month = await store.saveStudyMonth(
      StudyMonth(
        name: 'شهر رسوم السنتر',
        lessons: [const PreparedLesson(number: 1)],
      ),
    );
    session = await store.startPreparedLesson(
      groupId: group.id,
      preparedLessonId: month.lessons.single.id,
    );
    for (final code in ['CF1', 'CF2']) {
      await store.saveStudent(
        Student(
          name: 'طالب $code',
          code: code,
          groupIds: [group.id],
          createdAt: DateTime(2026),
        ),
      );
    }
    centerOnlyStudent = store.students.first;
    independentFeeStudent = store.students.last;
    await store.saveStudentCenterOnly(
      studentId: centerOnlyStudent.id,
      enabled: true,
    );
    for (var payment = 0; payment < 2; payment++) {
      await store.collectAndAttend(
        EntryRequest(
          studentId: centerOnlyStudent.id,
          sessionId: session.id,
          mode: EntryMode.single,
          paidAmount: 750,
        ),
      );
    }
    await store.setStudentCenterFee(
      studentId: independentFeeStudent.id,
      enabled: true,
    );
    await store.recordAttendance(
      EntryRequest(
        studentId: independentFeeStudent.id,
        sessionId: session.id,
        mode: EntryMode.single,
      ),
    );
    await store.collectStudentCenterFee(
      studentId: independentFeeStudent.id,
      sessionId: session.id,
    );
    await store.closeSession(session.id);
    await store.finalizeSession(sessionId: session.id, actualCash: 3000);
  });

  tearDown(() async {
    await store.close();
    await directory.delete(recursive: true);
  });

  Future<void> reopenStore() async {
    await store.close();
    store = await CenterStore.open(directory: directory.path);
    await store.signIn('closing-manager', password);
  }

  List<Map<String, Object>> expectedCategories() => [
    {
      'centerOnly': true,
      'unitAmount': 750,
      'studentCount': 1,
      'operationCount': 2,
    },
    {
      'centerOnly': false,
      'unitAmount': 1500,
      'studentCount': 1,
      'operationCount': 1,
    },
  ];

  test(
    'saved fee classification survives profile changes, restart and public snapshot serialization',
    () async {
      final saved = store.closings.single.summary.toJson();
      expect(saved['centerFeePaymentCategories'], expectedCategories());
      await store.saveStudentCenterOnly(
        studentId: centerOnlyStudent.id,
        enabled: false,
      );
      await store.setStudentCenterFee(
        studentId: independentFeeStudent.id,
        enabled: false,
      );
      await store.saveStudentCenterOnly(
        studentId: independentFeeStudent.id,
        enabled: true,
      );
      await reopenStore();
      expect(store.closings.single.summary.toJson(), saved);
      expect(
        store
            .sessionFinancialSummary(session.id)
            .toJson()['centerFeePaymentCategories'],
        expectedCategories(),
      );
      final response = await store.snapshotLan(
        store.currentUser!.id,
        stateEncoding: LanStateEncoding.json,
      );
      final publicJson =
          jsonDecode((response['state'] as EncodedPublicCenterState).json)
              as Map<String, dynamic>;
      expect(publicJson, isNot(contains('credentials')));
      final remoteState = CenterState.fromJson({
        ...publicJson,
        'credentials': <String, dynamic>{},
      });
      expect(remoteState.closings.single.summary.toJson(), saved);
    },
  );

  test(
    'legacy closing without fee categories stays unknown through restore and a new closing gains the detail',
    () async {
      final originalId = store.closings.single.id;
      final databasePath = store.databasePath;
      await store.close();
      final database = await databaseFactoryFfi.openDatabase(databasePath);
      try {
        final rows = await database.query('state');
        final persisted = jsonDecode(rows.single['payload'] as String) as Map;
        (persisted['closings'].single['summary'] as Map).remove(
          'centerFeePaymentCategories',
        );
        await database.update('state', {
          'payload': jsonEncode(persisted),
        }, where: 'id = 1');
      } finally {
        await database.close();
      }
      store = await CenterStore.open(directory: directory.path);
      await store.signIn('closing-manager', password);
      expect(store.closings.single.summary.centerFeePaymentCategories, isNull);
      final backup = await store.createBackup(
        destination: '${directory.path}/legacy-closing.json',
      );
      await store.restoreBackup(backup);
      await store.signIn('closing-manager', password);
      expect(store.closings.single.summary.centerFeePaymentCategories, isNull);
      expect(store.closings.single.summary.centerFeeCollected, 3000);
      await store.reopenFinancialClosing(
        closingId: originalId,
        reason: 'إعادة مراجعة التقفيلة القديمة',
      );
      await store.finalizeSession(sessionId: session.id, actualCash: 3000);
      await reopenStore();
      expect(store.allClosings, hasLength(2));
      expect(
        store.allClosings
            .firstWhere((closing) => closing.id == originalId)
            .summary
            .centerFeePaymentCategories,
        isNull,
      );
      expect(store.closings.single.id, isNot(originalId));
      expect(
        store.closings.single.summary.toJson()['centerFeePaymentCategories'],
        expectedCategories(),
      );
      expect(store.closings.single.summary.centerFeeCollected, 3000);
    },
  );

  for (final forgedCount in {'studentCount': 2, 'operationCount': 3}.entries) {
    test(
      'restore rejects positive but incorrect fee ${forgedCount.key} without replacing the current closing',
      () async {
        final original = store.closings.single.summary.toJson();
        final auditCount = store.audit.length;
        final backup = await store.createBackup(
          destination: '${directory.path}/valid-closing.json',
        );
        final envelope = jsonDecode(await File(backup).readAsString()) as Map;
        final categories =
            envelope['data']['closings']
                    .single['summary']['centerFeePaymentCategories']
                as List;
        final centerOnlyCategory =
            categories.singleWhere((row) => row['centerOnly'] == true) as Map;
        centerOnlyCategory[forgedCount.key] = forgedCount.value;
        final forged = File('${directory.path}/forged-closing.json');
        await forged.writeAsString(jsonEncode(envelope));
        await expectLater(
          store.restoreBackup(forged.path),
          throwsA(
            isA<CenterException>().having(
              (error) => error.message,
              'historical summary mismatch',
              'ملخص التقفيلة لا يطابق الحضور وأسعار المدفوعات المحفوظة.',
            ),
          ),
        );
        expect(store.closings.single.summary.toJson(), original);
        expect(store.audit.length, auditCount);
        await reopenStore();
        expect(store.closings.single.summary.toJson(), original);
      },
    );
  }
}
