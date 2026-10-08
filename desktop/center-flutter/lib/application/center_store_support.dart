part of 'center_store.dart';

final _supportStoreNonces = Expando<String>();

extension CenterStoreSupport on CenterStore {
  /// Includes receipt-only writes and other connections' commits. Rollbacks
  /// may invalidate this token too, which only causes a conservative capture.
  Future<String> supportSnapshotRevision() async {
    late String revision;
    await _exclusive(() async {
      final database = _database;
      if (_closed || isRemote || isClientWorkspace || database == null) {
        throw const CenterException('نسخة البيانات تُجهّز على الرئيسي فقط.');
      }
      final nonce = _supportStoreNonces[this] ??= CenterStore._uuid.v4();
      final changes = await database.rawQuery(
        'SELECT total_changes() AS changes',
      );
      final external = await database.rawQuery('PRAGMA data_version');
      revision =
          '$nonce:${changes.single['changes']}:${external.single['data_version']}';
    }, operation: 'cloud.revision');
    return revision;
  }

  /// Captures one committed logical database under the same queue as reception.
  /// Network transfer happens afterwards; the live SQLite file is never copied.
  Future<Map<String, dynamic>> captureSupportSnapshot({
    bool automatic = false,
  }) async {
    if (isRemote || isClientWorkspace || _database == null) {
      throw const CenterException('نسخة البيانات تُجهّز على الرئيسي فقط.');
    }
    late String stateJson;
    late String exportedAt;
    late List<Map<String, Object?>> receipts;
    String? actorId;
    final authorize = Zone.current[_lanAuthorizationKey] as bool Function()?;
    void authorizeCapture() {
      if (_closed) throw const CenterException('تم إغلاق ملف البيانات.');
      if (authorize != null && !authorize()) {
        throw const LanAuthorizationException('انتهت جلسة الموظف.');
      }
      if (!automatic) {
        _require(actorId != null && currentUser?.id == actorId);
        _lanUser(actorId!);
      }
    }

    await _exclusive(() async {
      actorId = currentUser?.id;
      authorizeCapture();
      // A read transaction also protects evidence from other connections' commits.
      await _database.transaction((tx) async {
        final rows = await tx.query(
          'state',
          columns: ['payload'],
          where: 'id=1',
        );
        stateJson = rows.single['payload'] as String;
        receipts = await tx.query('lan_commands');
      });
      exportedAt = DateTime.now().toUtc().toIso8601String();
      authorizeCapture();
    }, operation: 'cloud.snapshot');
    final capturedState = await compute(_decodeSupportState, stateJson);
    authorizeCapture();
    return {
      'format': 'massar-center-backup',
      'exportedAt': exportedAt,
      'data': capturedState,
      'lanCommandReceipts': receipts
          .map((receipt) => Map<String, Object?>.of(receipt))
          .toList(),
    };
  }

  Future<Map<String, dynamic>> queueSupportUploadLan(
    String staffId, {
    required bool Function() authorize,
  }) async {
    if (!authorize()) {
      throw const LanAuthorizationException('انتهت جلسة الموظف.');
    }
    final actor = _lanUser(staffId);
    await runZoned(
      requestSupportUpload,
      zoneValues: {_lanActorKey: actor, _lanAuthorizationKey: authorize},
    );
    if (!authorize()) {
      throw const LanAuthorizationException('انتهت جلسة الموظف.');
    }
    return {'queued': true, 'supportStatus': supportStatus};
  }
}

Map<String, dynamic> _decodeSupportState(String stateJson) =>
    jsonDecode(stateJson) as Map<String, dynamic>;
