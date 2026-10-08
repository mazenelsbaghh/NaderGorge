part of 'center_store.dart';

class _DataRepairConflict extends CenterException {
  _DataRepairConflict(this.recordIds)
    : super(
        'لم يُطبّق إصلاح البيانات لأن السجلات تغيرت؛ بياناتك الحالية محفوظة.',
      );
  final List<String> recordIds;
}

extension _BundledCenterDataRepair on CenterStore {
  Future<void> _applyHistoricalAcademicImport() async {
    for (final asset in [
      'assets/academic_import_20261004.json',
      'assets/cairo_academic_import_20261004.json',
    ]) {
      await _applyAcademicImportAsset(asset);
    }
  }

  Future<void> _applyAcademicImportAsset(String asset) async {
    String? importId;
    try {
      await _exclusive(() async {
        final payload = Map<String, dynamic>.from(
          jsonDecode(await rootBundle.loadString(asset)) as Map,
        );
        importId = payload['id'] as String;
        if (_state.installationSeedId != payload['installationSeedId'] ||
            !_state.sessions.any((s) => s.id == payload['requiredSessionId']) ||
            _state.appliedDataRepairs.contains(importId)) {
          return;
        }
        final candidate = CenterState.fromJson(_state.toJson());
        final merge = HistoricalAcademicImport(candidate)..apply(payload);
        candidate.appliedDataRepairs.add(importId!);
        if (candidate.staff.isNotEmpty) {
          candidate.audit.add(
            AuditRecord(
              id: '$importId:audit',
              action: 'historical_academic_import',
              description:
                  '${payload['label'] ?? 'استيراد الشهر الأول المجاني وأولى الشهر الثاني'}: '
                  '${merge.attendanceAdded} حضور، ${merge.gradesAdded} رصد امتحان، '
                  '${merge.preserved} سجل محفوظ، '
                  '${merge.conflicts + (payload['unresolvedGradeCount'] as int? ?? 0)} حالة للمراجعة.',
              staffId: candidate.staff.first.id,
              createdAt: DateTime.now(),
            ),
          );
        }
        validateState(candidate);
        await _saveAutomaticBackup(_state.copyForBackup());
        await _database!.transaction((transaction) async {
          final count = await replaceRecordState(
            transaction,
            _stateEncoder.encodeStorageFields(candidate),
          );
          if (count != 1) throw const FormatException('Historical import save');
        });
        _state = candidate;
      }, operation: 'database.historical_import');
    } catch (error, stackTrace) {
      reportProblem(error, stackTrace, operation: 'database.historical_import');
      await _writeRepairFailure(importId, error);
    }
  }

  static const _repairAssets = [
    'assets/data_repair_20261002.json',
    'assets/data_repair_20261004.json',
    'assets/cairo_codes_20261004.json',
    'assets/gec_codes_20261004.json',
    'assets/gec_duplicate_codes_20261004.json',
  ];
  static const _repairDefaults = <String, Object>{
    'discountNeedsReview': false,
    'centerOnly': false,
    'centerFeeOnly': false,
    'monthNumber': 1,
    'paymentPending': false,
    'recordedAtKnown': true,
    'createdAtKnown': true,
    'startsAtKnown': true,
    'maxScoreKnown': true,
    'priceConfigured': true,
    'isSuspended': false,
    'suspensionReason': '',
  };
  static const _repairCollections = {
    'students',
    'groups',
    'sessions',
    'packages',
    'attendances',
    'payments',
    'academics',
    'academicActivities',
    'reviews',
    'closings',
    'paymentChecks',
    'corrections',
    'refunds',
    'cardPayments',
    'cardReceipts',
    'debtSettlements',
    'centerFees',
  };

  Future<void> _applyBundledDataRepair() async {
    for (final asset in _repairAssets) {
      await _applyBundledRepairAsset(asset);
    }
  }

  Future<void> _applyBundledRepairAsset(String asset) async {
    String? repairId;
    try {
      await _exclusive(() async {
        final patch = Map<String, dynamic>.from(
          jsonDecode(await rootBundle.loadString(asset)) as Map,
        );
        repairId = patch['id'] as String;
        final seedId = patch['installationSeedId'] as String;
        final sessionId = patch['requiredSessionId'] as String;
        if (repairId!.trim().isEmpty || seedId.isEmpty || sessionId.isEmpty) {
          throw const FormatException('Repair identity');
        }
        if (_state.installationSeedId != seedId ||
            !_state.sessions.any((e) => e.id == sessionId) ||
            _state.appliedDataRepairs.contains(repairId)) {
          return;
        }
        final document = _state.toJson();
        final guardedIds = (patch['guardedStudentIds'] as List? ?? const [])
            .cast<String>()
            .toSet();
        final expectedHistory = patch['expectedHistory'] as Map?;
        if (expectedHistory != null) {
          for (final item in expectedHistory.entries) {
            final currentRows = _repairRows(
              document,
              item.key as String,
            ).where((row) => guardedIds.contains(row['studentId']));
            final current = {
              for (final row in currentRows) row['id'] as String: row,
            };
            if (!_repairValuesMatch(current, item.value)) {
              throw _DataRepairConflict(guardedIds.toList());
            }
          }
        }

        final updatedRows = <String>{};
        for (final raw in patch['updates'] as List) {
          final update = Map<String, dynamic>.from(raw as Map);
          final collection = update['collection'] as String;
          final id = update['id'] as String;
          if (!updatedRows.add('$collection:$id')) {
            throw _DataRepairConflict([id]);
          }
          final rows = _repairRows(document, collection);
          final row = rows.where((e) => e['id'] == id).singleOrNull;
          if (row == null) throw _DataRepairConflict([id]);
          final before = Map<String, dynamic>.from(update['before'] as Map);
          final after = Map<String, dynamic>.from(update['after'] as Map);
          if (before.isEmpty ||
              before.containsKey('id') ||
              after.containsKey('id') ||
              !setEquals(before.keys.toSet(), after.keys.toSet())) {
            throw _DataRepairConflict([id]);
          }
          if (!_repairFieldsMatch(row, before) &&
              !_repairFieldsMatch(row, after)) {
            throw _DataRepairConflict([id]);
          }
          row.addAll(after);
        }
        for (final raw in patch['inserts'] as List) {
          final insert = Map<String, dynamic>.from(raw as Map);
          final collection = insert['collection'] as String;
          final value = Map<String, dynamic>.from(insert['value'] as Map);
          _insertRepairRow(_repairRows(document, collection), value);
        }
        for (final raw in patch['inserts'] as List) {
          final insert = Map<String, dynamic>.from(raw as Map);
          if (insert['collection'] == 'attendances') {
            _checkRepairAttendance(
              Map<String, dynamic>.from(insert['value'] as Map),
              document,
            );
          }
        }
        for (final raw in patch['audit'] as List) {
          _insertRepairRow(
            _repairRows(document, 'audit', allowAudit: true),
            Map<String, dynamic>.from(raw as Map),
          );
        }
        final candidate = CenterState.fromJson(document);
        _verifyRepairFields(candidate.toJson(), patch);
        candidate.appliedDataRepairs.add(repairId!);
        validateState(candidate);
        if (_state.staff.isNotEmpty) {
          await _saveAutomaticBackup(_state.copyForBackup());
        }
        await _database!.transaction((transaction) async {
          final updated = await replaceRecordState(
            transaction,
            _stateEncoder.encodeStorageFields(candidate),
          );
          if (updated != 1) {
            throw const CenterException(
              'لم يُحفظ الإصلاح؛ البيانات الأصلية محفوظة.',
            );
          }
        });
        _state = candidate;
      }, operation: 'database.repair');
    } catch (error, stackTrace) {
      // A bundled repair must never prevent access to the unchanged database.
      reportProblem(error, stackTrace, operation: 'database.repair');
      await _writeRepairFailure(repairId, error);
    }
  }

  List<Map<String, dynamic>> _repairRows(
    Map<String, dynamic> document,
    String collection, {
    bool allowAudit = false,
  }) {
    if (!_repairCollections.contains(collection) &&
        !(allowAudit && collection == 'audit')) {
      throw const FormatException('Repair collection');
    }
    final value = document[collection];
    if (value == null) document[collection] = <Map<String, dynamic>>[];
    final rows = (document[collection] as List)
        .map((e) => Map<String, dynamic>.from(e as Map))
        .toList();
    document[collection] = rows;
    return rows;
  }

  bool _repairFieldsMatch(
    Map<String, dynamic> row,
    Map<String, dynamic> fields,
  ) => fields.entries.every(
    (e) => _repairValuesMatch(_repairFieldValue(row, e.key), e.value),
  );

  Object? _repairFieldValue(Map<String, dynamic> row, String field) =>
      row.containsKey(field) ? row[field] : _repairDefaults[field];

  void _verifyRepairFields(
    Map<String, dynamic> document,
    Map<String, dynamic> patch,
  ) {
    for (final raw in patch['updates'] as List) {
      final update = Map<String, dynamic>.from(raw as Map);
      final id = update['id'] as String;
      final row = _repairRows(
        document,
        update['collection'] as String,
      ).where((e) => e['id'] == id).singleOrNull;
      final after = Map<String, dynamic>.from(update['after'] as Map);
      if (row == null || !_repairFieldsMatch(row, after)) {
        throw _DataRepairConflict([id]);
      }
    }
    for (final raw in patch['inserts'] as List) {
      final insert = Map<String, dynamic>.from(raw as Map);
      final value = Map<String, dynamic>.from(insert['value'] as Map);
      _verifyRepairRow(document, insert['collection'] as String, value);
    }
    for (final raw in patch['audit'] as List) {
      _verifyRepairRow(
        document,
        'audit',
        Map<String, dynamic>.from(raw as Map),
        allowAudit: true,
      );
    }
  }

  void _verifyRepairRow(
    Map<String, dynamic> document,
    String collection,
    Map<String, dynamic> value, {
    bool allowAudit = false,
  }) {
    final id = value['id'] as String;
    final row = _repairRows(
      document,
      collection,
      allowAudit: allowAudit,
    ).where((e) => e['id'] == id).singleOrNull;
    if (row == null || !_repairFieldsMatch(row, value)) {
      throw _DataRepairConflict([id]);
    }
  }

  bool _repairValuesMatch(Object? first, Object? second) {
    if (first is Map && second is Map) {
      return setEquals(first.keys.toSet(), second.keys.toSet()) &&
          first.keys.every(
            (key) => _repairValuesMatch(first[key], second[key]),
          );
    }
    if (first is List && second is List) {
      if (first.length != second.length) return false;
      for (var i = 0; i < first.length; i++) {
        if (!_repairValuesMatch(first[i], second[i])) return false;
      }
      return true;
    }
    return first == second;
  }

  void _insertRepairRow(
    List<Map<String, dynamic>> rows,
    Map<String, dynamic> value,
  ) {
    final id = value['id'] as String;
    if (id.trim().isEmpty) throw const FormatException('Repair record id');
    final existing = rows.where((e) => e['id'] == id).singleOrNull;
    if (existing != null) {
      final fields = {...existing.keys, ...value.keys};
      if (!fields.every(
        (field) => _repairValuesMatch(
          _repairFieldValue(existing, field),
          _repairFieldValue(value, field),
        ),
      )) {
        throw _DataRepairConflict([id]);
      }
      return;
    }
    rows.add(value);
  }

  void _checkRepairAttendance(
    Map<String, dynamic> value,
    Map<String, dynamic> document,
  ) {
    final voidIds = _repairRows(
      document,
      'corrections',
    ).map((e) => e['attendanceId']).toSet();
    final conflicts = _repairRows(document, 'attendances').where(
      (e) =>
          !voidIds.contains(e['id']) &&
          e['studentId'] == value['studentId'] &&
          e['sessionId'] == value['sessionId'] &&
          e['id'] != value['id'],
    );
    if (!voidIds.contains(value['id']) && conflicts.isNotEmpty) {
      throw _DataRepairConflict([value['id'] as String]);
    }
  }

  Future<void> _writeRepairFailure(String? repairId, Object error) async {
    final file = File(
      p.join(p.dirname(databasePath), 'data-repair-status.json'),
    );
    final temporary = File('${file.path}.${CenterStore._uuid.v4()}.tmp');
    try {
      await temporary.writeAsString(
        jsonEncode({
          'version': 1,
          'repairId': repairId,
          'status': error is _DataRepairConflict ? 'conflict' : 'failed',
          'recordIds': error is _DataRepairConflict
              ? error.recordIds
              : <String>[],
          'reportedAt': DateTime.now().toUtc().toIso8601String(),
        }),
        flush: true,
      );
      await temporary.rename(file.path);
    } on FileSystemException catch (failure, stackTrace) {
      reportProblem(failure, stackTrace, operation: 'database.repair');
    } finally {
      try {
        if (await temporary.exists()) await temporary.delete();
      } on FileSystemException catch (failure, stackTrace) {
        reportProblem(failure, stackTrace, operation: 'database.repair');
      }
    }
  }
}
