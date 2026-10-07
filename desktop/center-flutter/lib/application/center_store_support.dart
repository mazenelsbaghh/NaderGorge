part of 'center_store.dart';

extension CenterStoreSupport on CenterStore {
  /// Captures one committed logical database under the same queue as reception.
  /// Network transfer happens afterwards; the live SQLite file is never copied.
  Future<Map<String, dynamic>> captureSupportSnapshot({
    bool automatic = false,
  }) async {
    if (isRemote || isClientWorkspace || _database == null) {
      throw const CenterException('نسخة البيانات تُجهّز على الرئيسي فقط.');
    }
    late String stateJson, exportedAt;
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
      // Every writer uses this queue, including atomic LAN command receipts.
      // Copy the committed JSON, then release reception before decoding it.
      final rows = await _database.query(
        'state',
        columns: ['payload'],
        where: 'id = 1',
      );
      stateJson = rows.single['payload'] as String;
      receipts = await _database.query('lan_commands');
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

Map<String, dynamic> _decodeSupportState(String payload) =>
    jsonDecode(payload) as Map<String, dynamic>;
