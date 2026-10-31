import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'helpers/synthetic_installer_assets.dart';

const _asset = 'assets/gec_month1_repair_20261013.json';
const _repairId = 'synthetic-new-student-enrollment-repair';
const _studentId = 'synthetic-imported-student';
const _enrolledAt = '2026-09-01T00:00:00.000';

Future<CenterState> _persistedState(Directory directory) async {
  final db = await databaseFactoryFfi.openDatabase(
    '${directory.path}/center.sqlite',
    options: OpenDatabaseOptions(readOnly: true),
  );
  try {
    return CenterState.fromJson(
      Map<String, dynamic>.from(
        jsonDecode((await db.query('state')).single['payload'] as String),
      ),
    );
  } finally {
    await db.close();
  }
}

enum _Scenario {
  newStudent,
  alreadyInsertedWithoutMarker,
  changedEnrollment,
  studentNotInserted,
  groupNotOnStudent,
  duplicateEnrollment,
  invalidDate,
  laterValidationFailure,
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Map<String, dynamic> baseline;

  setUpAll(() async {
    installSyntheticRepairAssets();
    final directory = await Directory.systemTemp.createTemp(
      'enrollment-fixture-',
    );
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
              .singleWhere((c) => c.kind == CatalogKind.subject)
              .id,
          gradeId: store.catalogs
              .singleWhere((c) => c.kind == CatalogKind.grade)
              .id,
          centerId: store.catalogs
              .singleWhere((c) => c.kind == CatalogKind.center)
              .id,
          sessionPrice: 0,
        ),
      );
      await store.saveStudent(
        Student(
          code: '00100',
          name: 'existing student',
          groupIds: [store.groups.single.id],
          createdAt: DateTime(2026, 9, 1),
        ),
      );
      final month = await store.saveStudyMonth(
        StudyMonth(
          name: 'fixture month',
          price: 0,
          lessons: [const PreparedLesson(number: 1, kind: SessionKind.free)],
        ),
      );
      await store.startPreparedLesson(
        groupId: store.groups.single.id,
        preparedLessonId: month.lessons.single.id,
      );
      baseline = Map<String, dynamic>.from(
        jsonDecode(
          await File(await store.createBackup()).readAsString(),
        )['data'],
      );
      baseline['installationSeedId'] = 'synthetic-enrollment-installation';
    } finally {
      await store.close();
      await directory.delete(recursive: true);
    }
  });

  for (final scenario in _Scenario.values) {
    test(
      'bundled student enrollment ${scenario.name} commits once or preserves live state',
      () async {
        final initial = CenterState.fromJson(
          Map<String, dynamic>.from(jsonDecode(jsonEncode(baseline))),
        );
        final groupId = initial.groups.single.id;
        final enrollmentKey = '$_studentId:$groupId';
        final importedStudent = Student(
          id: _studentId,
          code: scenario == _Scenario.laterValidationFailure
              ? '00100'
              : '00200',
          name: 'imported student',
          groupIds: [groupId],
          createdAt: DateTime(2026, 9, 1),
        );
        final enrollment = <String, dynamic>{
          'studentId': _studentId,
          'groupId': groupId,
          'enrolledAt': _enrolledAt,
        };
        switch (scenario) {
          case _Scenario.alreadyInsertedWithoutMarker:
          case _Scenario.changedEnrollment:
            initial.students.add(importedStudent);
            initial.enrollments[enrollmentKey] =
                scenario == _Scenario.changedEnrollment
                ? '2026-09-02T00:00:00.000'
                : _enrolledAt;
          case _Scenario.studentNotInserted:
            enrollment['studentId'] = initial.students.single.id;
          case _Scenario.groupNotOnStudent:
            enrollment['groupId'] = 'group-outside-imported-membership';
          case _Scenario.invalidDate:
            enrollment['enrolledAt'] = 'invalid-date';
          case _Scenario.newStudent:
          case _Scenario.duplicateEnrollment:
          case _Scenario.laterValidationFailure:
            break;
        }
        validateState(initial);
        final unchanged = initial.toJson();
        final patch = <String, dynamic>{
          'id': _repairId,
          'installationSeedId': initial.installationSeedId,
          'requiredSessionId': initial.sessions.single.id,
          'updates': [],
          'inserts': [
            {'collection': 'students', 'value': importedStudent.toJson()},
          ],
          'enrollmentInserts': [
            enrollment,
            if (scenario == _Scenario.duplicateEnrollment) {...enrollment},
          ],
          'audit': [],
        };
        final applies =
            scenario == _Scenario.newStudent ||
            scenario == _Scenario.alreadyInsertedWithoutMarker;
        final expected = CenterState.fromJson(
          Map<String, dynamic>.from(jsonDecode(jsonEncode(unchanged))),
        );
        if (applies) {
          if (scenario == _Scenario.newStudent) {
            expected.students.add(importedStudent);
          }
          expected.enrollments[enrollmentKey] = _enrolledAt;
          expected.appliedDataRepairs.add(_repairId);
          validateState(expected);
        }
        final originalAsset = syntheticRepairAssets[_asset];
        syntheticRepairAssets[_asset] = patch;
        installSyntheticRepairAssets();
        addTearDown(() => syntheticRepairAssets[_asset] = originalAsset!);

        final directory = await Directory.systemTemp.createTemp(
          'enrollment-repair-',
        );
        CenterStore? store;
        try {
          store = await CenterStore.open(
            directory: directory.path,
            initialState: () async => initial,
          );
          expect(
            store.enrollmentDateFor(_studentId, groupId),
            expected.enrollments[enrollmentKey] == null
                ? null
                : DateTime.parse(expected.enrollments[enrollmentKey]!),
          );
          expect(
            store.students.map((student) => student.toJson()),
            expected.students.map((student) => student.toJson()),
          );
          await store.close();
          store = null;
          final first = await _persistedState(directory);
          expect(first.toJson(), expected.toJson());
          if (!applies) {
            final status = jsonDecode(
              await File(
                '${directory.path}/data-repair-status.json',
              ).readAsString(),
            );
            expect(status['repairId'], _repairId);
            expect(
              status['status'],
              scenario == _Scenario.changedEnrollment ? 'conflict' : 'failed',
            );
          }
          store = await CenterStore.open(directory: directory.path);
          await store.close();
          store = null;
          expect((await _persistedState(directory)).toJson(), first.toJson());
        } finally {
          await store?.close();
          await directory.delete(recursive: true);
        }
      },
    );
  }

  for (final scenario in [
    'appendGroup',
    'removeOldGroup',
    'alreadyAddedWithoutMarker',
  ]) {
    test(
      'existing student enrollment $scenario preserves prior membership',
      () async {
        final initial = CenterState.fromJson(
          Map<String, dynamic>.from(jsonDecode(jsonEncode(baseline))),
        );
        final originalStudent = initial.students.single;
        final oldGroups = originalStudent.groupIds;
        final additionalGroup = initial.groups.single.copyWith(
          id: 'additional-imported-group',
          name: 'additional imported group',
        );
        initial.groups.add(additionalGroup);
        final newGroups = [...oldGroups, additionalGroup.id];
        final enrollmentKey = '${originalStudent.id}:${additionalGroup.id}';
        if (scenario == 'alreadyAddedWithoutMarker') {
          initial.students[0] = originalStudent.copyWith(groupIds: newGroups);
          initial.enrollments[enrollmentKey] = _enrolledAt;
        }
        validateState(initial);
        final before = initial.toJson();
        final patch = {
          'id': _repairId,
          'installationSeedId': initial.installationSeedId,
          'requiredSessionId': initial.sessions.single.id,
          'updates': [
            {
              'collection': 'students',
              'id': originalStudent.id,
              'before': {'groupIds': oldGroups},
              'after': {
                'groupIds': scenario == 'removeOldGroup'
                    ? [additionalGroup.id]
                    : newGroups,
              },
            },
          ],
          'inserts': [],
          'enrollmentInserts': [
            {
              'studentId': originalStudent.id,
              'groupId': additionalGroup.id,
              'enrolledAt': _enrolledAt,
            },
          ],
          'audit': [],
        };
        // A newer membership that already equals the reviewed after-state is safe.
        final applies = scenario != 'removeOldGroup';
        final expected = CenterState.fromJson(
          Map<String, dynamic>.from(jsonDecode(jsonEncode(before))),
        );
        if (applies) {
          expected.students[0] = originalStudent.copyWith(groupIds: newGroups);
          expected.enrollments[enrollmentKey] = _enrolledAt;
          expected.appliedDataRepairs.add(_repairId);
        }
        final originalAsset = syntheticRepairAssets[_asset];
        syntheticRepairAssets[_asset] = patch;
        installSyntheticRepairAssets();
        addTearDown(() => syntheticRepairAssets[_asset] = originalAsset!);
        final directory = await Directory.systemTemp.createTemp(
          'existing-enrollment-',
        );
        addTearDown(() => directory.delete(recursive: true));
        final store = await CenterStore.open(
          directory: directory.path,
          initialState: () async => initial,
        );
        await store.close();
        expect((await _persistedState(directory)).toJson(), expected.toJson());
        final reopened = await CenterStore.open(directory: directory.path);
        await reopened.close();
        expect((await _persistedState(directory)).toJson(), expected.toJson());
      },
    );
  }
}
