import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/domain/discount_calculation.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'helpers/synthetic_installer_assets.dart';

const asset = 'assets/attendance_payment_repair_20261011.json';
const repairId = 'synthetic-attendance-payment-correction';

Future<CenterState> persistedState(Directory directory) async {
  final database = await databaseFactoryFfi.openDatabase(
    '${directory.path}/center.sqlite',
    options: OpenDatabaseOptions(readOnly: true),
  );
  try {
    final rows = await database.query('state');
    return CenterState.fromJson(
      Map<String, dynamic>.from(jsonDecode(rows.single['payload'] as String)),
    );
  } finally {
    await database.close();
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Map<String, dynamic> baseline;
  late Map<String, dynamic> patch;

  setUpAll(() async {
    final directory = await Directory.systemTemp.createTemp('repair-fixture-');
    installSyntheticRepairAssets();
    final store = await CenterStore.open(directory: directory.path);
    try {
      await store.setupAdmin('fixture-owner', 'fixture-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'fixture group',
          subjectId: store.catalogs
              .singleWhere((e) => e.kind == CatalogKind.subject)
              .id,
          gradeId: store.catalogs
              .singleWhere((e) => e.kind == CatalogKind.grade)
              .id,
          centerId: store.catalogs
              .singleWhere((e) => e.kind == CatalogKind.center)
              .id,
          sessionPrice: 6000,
        ),
      );
      final group = store.groups.single;
      await store.saveStudent(
        Student(
          code: '00100',
          name: 'fixture student',
          groupIds: [group.id],
          notes: 'previous note',
          createdAt: DateTime(2026, 9, 1),
        ),
      );
      final student = store.students.single;
      final month = await store.saveStudyMonth(
        StudyMonth(
          name: 'fixture month',
          lessons: [const PreparedLesson(number: 1)],
        ),
      );
      final session = await store.startPreparedLesson(
        groupId: group.id,
        preparedLessonId: month.lessons.single.id,
      );
      await store.collectAndAttend(
        EntryRequest(
          studentId: student.id,
          sessionId: session.id,
          mode: EntryMode.single,
        ),
      );
      baseline = Map<String, dynamic>.from(
        jsonDecode(
          await File(await store.createBackup()).readAsString(),
        )['data'],
      );
      baseline['installationSeedId'] = 'synthetic-repair-installation';
      final originalPayment = (baseline['payments'] as List).single as Map;
      final originalAttendance =
          (baseline['attendances'] as List).single as Map;
      final discount = discountPercentForAmount(6000, 2000);
      final replacementPayment = {
        ...originalPayment,
        'id': 'replacement-receipt',
        'discountPercent': discount,
        'netAmount': 2000,
        'paidAmount': 2000,
      };
      final replacementAttendance = {
        ...originalAttendance,
        'fixedDiscountPercent': discount,
      };
      patch = {
        'id': repairId,
        'installationSeedId': baseline['installationSeedId'],
        'requiredSessionId': session.id,
        'updates': [
          {
            'collection': 'attendances',
            'id': originalAttendance['id'],
            'before': {'fixedDiscountPercent': 0},
            'after': {'fixedDiscountPercent': discount},
          },
        ],
        'deletes': [
          {'collection': 'payments', 'before': originalPayment},
        ],
        'inserts': [
          {'collection': 'payments', 'value': replacementPayment},
        ],
        'noteAppends': [
          {
            'studentId': student.id,
            'lines': ['source: 20 EGP discounted lesson'],
          },
        ],
        'historyGuards': [
          for (final (collection, original, replacement) in [
            ('payments', originalPayment, replacementPayment),
            ('attendances', originalAttendance, replacementAttendance),
          ])
            {
              'collection': collection,
              'sessionIds': [session.id],
              'before': {original['id']: original},
              'after': {replacement['id']: replacement},
            },
        ],
        'audit': [],
      };
    } finally {
      await store.close();
      await directory.delete(recursive: true);
    }
  });

  for (final scenario in [
    'apply',
    'changed-target',
    'other-installation',
    'backup-write-fails',
  ]) {
    test(
      'bundled correction $scenario preserves the current state or commits once with a recovery backup',
      () async {
        final initial = CenterState.fromJson(
          Map<String, dynamic>.from(jsonDecode(jsonEncode(baseline))),
        );
        if (scenario == 'changed-target') {
          initial.payments[0] = initial.payments[0].copyWith(paidAmount: 1000);
        } else if (scenario == 'other-installation') {
          initial.installationSeedId = 'another-installation';
        }
        final unchanged = initial.toJson();
        final originalAsset = syntheticRepairAssets[asset];
        syntheticRepairAssets[asset] = patch;
        installSyntheticRepairAssets();
        addTearDown(() => syntheticRepairAssets[asset] = originalAsset!);
        final directory = await Directory.systemTemp.createTemp(
          'repair-acceptance-',
        );
        CenterStore? store;
        try {
          if (scenario == 'backup-write-fails') {
            await File(
              '${directory.path}/backups',
            ).writeAsString('blocked backup directory');
          }
          store = await CenterStore.open(
            directory: directory.path,
            initialState: () async => initial,
          );
          await store.close();
          store = null;
          final saved = await persistedState(directory);
          if (scenario == 'apply') {
            expect(saved.payments.single.id, 'replacement-receipt');
            expect(saved.payments.single.collectedAmount, 2000);
            expect(
              saved.attendances.single.fixedDiscountPercent,
              discountPercentForAmount(6000, 2000),
            );
            expect(
              saved.students.single.notes,
              'previous note\nsource: 20 EGP discounted lesson',
            );
            expect(saved.appliedDataRepairs, contains(repairId));
            final recoveryFile = await Directory(
              '${directory.path}/backups',
            ).list().firstWhere((f) => f.path.contains('before-data-repair'));
            final recovery = CenterState.fromJson(
              Map<String, dynamic>.from(
                jsonDecode(
                  await File(recoveryFile.path).readAsString(),
                )['data'],
              ),
            );
            expect(recovery.payments.single.collectedAmount, 6000);
            expect(recovery.students.single.notes, 'previous note');
            store = await CenterStore.open(directory: directory.path);
            await store.close();
            store = null;
            expect((await persistedState(directory)).toJson(), saved.toJson());
          } else {
            expect(saved.toJson(), unchanged);
            if (scenario != 'other-installation') {
              final status = jsonDecode(
                await File(
                  '${directory.path}/data-repair-status.json',
                ).readAsString(),
              );
              expect(
                status['status'],
                scenario == 'changed-target' ? 'conflict' : 'failed',
              );
            }
          }
        } finally {
          await store?.close();
          await directory.delete(recursive: true);
        }
      },
    );
  }
}
