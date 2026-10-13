import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:massar_center/application/center_store.dart';
import 'package:massar_center/data/center_state.dart';
import 'package:massar_center/domain/models.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'helpers/synthetic_installer_assets.dart';

const _asset = 'assets/gec_month1_repair_20261013.json';
const _repairId = 'synthetic-grade-and-group-attendance-import';
const _grade = CatalogEntry(
  id: 'imported-grade',
  name: 'imported grade',
  kind: CatalogKind.grade,
);
const _enrolledAt = '2026-09-05T00:00:00.000';

enum _Scenario {
  newGradeAndGroup,
  alreadyInsertedWithoutMarker,
  changedCatalog,
  sameGradeNameAtAnotherId,
  changedGroup,
  missingCatalogReference,
  nonCanonicalCatalogField,
}

Future<CenterState> _persistedState(Directory directory) async {
  final database = await databaseFactoryFfi.openDatabase(
    '${directory.path}/center.sqlite',
    options: OpenDatabaseOptions(readOnly: true),
  );
  try {
    return CenterState.fromJson(
      Map<String, dynamic>.from(
        jsonDecode((await database.query('state')).single['payload'] as String),
      ),
    );
  } finally {
    await database.close();
  }
}

Future<CenterState> _backupState(File backup) async => CenterState.fromJson(
  Map<String, dynamic>.from(jsonDecode(await backup.readAsString())['data']),
);

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late Map<String, dynamic> baseline;
  late StudyGroup importedGroup;
  late Student importedStudent;
  late LessonSession importedSession;
  late AttendanceRecord importedAttendance;

  setUpAll(() async {
    installSyntheticRepairAssets();
    final directory = await Directory.systemTemp.createTemp('catalog-fixture-');
    final store = await CenterStore.open(directory: directory.path);
    try {
      await store.setupAdmin('fixture-owner', 'fixture-password');
      for (final kind in CatalogKind.values) {
        await store.saveCatalog(CatalogEntry(name: kind.name, kind: kind));
      }
      await store.saveGroup(
        StudyGroup(
          name: 'existing group',
          subjectId: store.catalogs
              .singleWhere((c) => c.kind == CatalogKind.subject)
              .id,
          centerId: store.catalogs
              .singleWhere((c) => c.kind == CatalogKind.center)
              .id,
          gradeId: store.catalogs
              .singleWhere((c) => c.kind == CatalogKind.grade)
              .id,
          sessionPrice: 6000,
        ),
      );
      await store.saveStudent(
        Student(
          code: '00100',
          name: 'existing student',
          groupIds: [store.groups.single.id],
          notes: 'existing note must remain',
          createdAt: DateTime(2026, 9, 1),
        ),
      );
      final month = await store.saveStudyMonth(
        StudyMonth(
          name: 'existing month',
          lessons: [const PreparedLesson(number: 1)],
        ),
      );
      final anchor = await store.startPreparedLesson(
        groupId: store.groups.single.id,
        preparedLessonId: month.lessons.single.id,
      );
      await store.collectAndAttend(
        EntryRequest(
          studentId: store.students.single.id,
          sessionId: anchor.id,
          mode: EntryMode.single,
        ),
      );
      baseline = (await _backupState(
        File(await store.createBackup()),
      )).toJson();
      baseline['installationSeedId'] = 'synthetic-catalog-installation';
      importedGroup = StudyGroup(
        id: 'imported-group',
        name: 'imported group',
        subjectId: store.groups.single.subjectId,
        centerId: store.groups.single.centerId,
        gradeId: _grade.id,
        sessionPrice: 0,
        packagePrice: 0,
        monthPlans: store.groups.single.monthPlans
            .map((plan) => plan.copyWith(price: 0))
            .toList(),
      );
      importedStudent = Student(
        id: 'imported-student',
        code: '00200',
        name: 'imported student',
        groupIds: [importedGroup.id],
        createdAt: DateTime(2026, 9, 5),
      );
      importedSession = LessonSession(
        id: 'imported-session',
        preparedLessonId: store.studyMonths
            .singleWhere((month) => month.number == 1)
            .lessons
            .singleWhere((lesson) => lesson.number == 1)
            .id,
        groupId: importedGroup.id,
        number: 1,
        startsAt: DateTime(2026, 9, 5),
        kind: SessionKind.free,
        status: SessionStatus.closed,
        importRoster: [importedStudent.id],
        createdAt: DateTime(2026, 9, 5),
      );
      importedAttendance = AttendanceRecord(
        id: 'imported-attendance',
        studentId: importedStudent.id,
        sessionId: importedSession.id,
        status: AttendanceStatus.present,
        recordedAt: DateTime(2026, 9, 5),
      );
    } finally {
      await store.close();
      await directory.delete(recursive: true);
    }
  });

  for (final scenario in _Scenario.values) {
    test(
      'catalog attendance import ${scenario.name} is atomic and durable',
      () async {
        final initial = CenterState.fromJson(
          Map<String, dynamic>.from(jsonDecode(jsonEncode(baseline))),
        );
        final gradeRow = _grade.toJson();
        final groupRow = importedGroup.toJson();
        final enrollmentKey = '${importedStudent.id}:${importedGroup.id}';
        switch (scenario) {
          case _Scenario.alreadyInsertedWithoutMarker:
            initial.catalogs.add(_grade);
            initial.groups.add(importedGroup);
            initial.students.add(importedStudent);
            initial.enrollments[enrollmentKey] = _enrolledAt;
            initial.sessions.add(importedSession);
            initial.attendances.add(importedAttendance);
          case _Scenario.changedCatalog:
            initial.catalogs.add(_grade.copyWith(name: 'changed on the host'));
          case _Scenario.sameGradeNameAtAnotherId:
            initial.catalogs.add(
              _grade.copyWith(id: 'grade-created-on-the-host'),
            );
          case _Scenario.changedGroup:
            initial.catalogs.add(_grade);
            initial.groups.add(
              importedGroup.copyWith(name: 'changed on the host'),
            );
          case _Scenario.missingCatalogReference:
            groupRow['subjectId'] = 'missing-subject';
          case _Scenario.nonCanonicalCatalogField:
            gradeRow['region'] = 'cairo';
          case _Scenario.newGradeAndGroup:
            break;
        }
        validateState(initial);
        final unchanged = initial.toJson();
        final patch = {
          'id': _repairId,
          'installationSeedId': initial.installationSeedId,
          'requiredSessionId': initial.sessions.first.id,
          'updates': [],
          'inserts': [
            {'collection': 'catalogs', 'value': gradeRow},
            {'collection': 'groups', 'value': groupRow},
            {'collection': 'students', 'value': importedStudent.toJson()},
            {'collection': 'sessions', 'value': importedSession.toJson()},
            {'collection': 'attendances', 'value': importedAttendance.toJson()},
          ],
          'enrollmentInserts': [
            {
              'studentId': importedStudent.id,
              'groupId': importedGroup.id,
              'enrolledAt': _enrolledAt,
            },
          ],
          'audit': [],
        };
        final applies =
            scenario == _Scenario.newGradeAndGroup ||
            scenario == _Scenario.alreadyInsertedWithoutMarker;
        final expected = CenterState.fromJson(
          Map<String, dynamic>.from(jsonDecode(jsonEncode(unchanged))),
        );
        if (scenario == _Scenario.newGradeAndGroup) {
          expected.catalogs.add(_grade);
          expected.groups.add(importedGroup);
          expected.students.add(importedStudent);
          expected.enrollments[enrollmentKey] = _enrolledAt;
          expected.sessions.add(importedSession);
          expected.attendances.add(importedAttendance);
        }
        if (applies) expected.appliedDataRepairs.add(_repairId);
        validateState(expected);
        final originalAsset = syntheticRepairAssets[_asset];
        syntheticRepairAssets[_asset] = patch;
        installSyntheticRepairAssets();
        addTearDown(() => syntheticRepairAssets[_asset] = originalAsset!);

        final directory = await Directory.systemTemp.createTemp(
          'catalog-repair-',
        );
        CenterStore? store;
        try {
          store = await CenterStore.open(
            directory: directory.path,
            initialState: () async => initial,
          );
          expect(
            store.catalogs.map((c) => c.toJson()),
            expected.catalogs.map((c) => c.toJson()),
          );
          expect(
            store.groups.map((g) => g.toJson()),
            expected.groups.map((g) => g.toJson()),
          );
          await store.close();
          store = null;
          final first = await _persistedState(directory);
          expect(first.toJson(), expected.toJson());
          if (applies) {
            final recoveryFile = await Directory(
              '${directory.path}/backups',
            ).list().firstWhere((f) => f.path.contains('before-data-repair'));
            expect(
              (await _backupState(File(recoveryFile.path))).toJson(),
              unchanged,
            );
          } else {
            expect(first.appliedDataRepairs, isNot(contains(_repairId)));
            final status = jsonDecode(
              await File(
                '${directory.path}/data-repair-status.json',
              ).readAsString(),
            );
            expect(status['repairId'], _repairId);
            expect(status['status'], anyOf('conflict', 'failed'));
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
}
