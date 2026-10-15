part of 'center_store.dart';

extension _CenterExamImport on CenterStore {
  ExamMonitorRecord _examImportRoom(String roomId) => _find<ExamMonitorRecord>(
    _state.examMonitors,
    (record) => record.id == roomId,
    'لم يصل كشف هذا الامتحان من الساكند بعد. وصّل الكابل وسجّل الدخول عليه.',
  );

  Future<void> _requestExamResultImport(String roomId) => _change(
    'exam_import_request',
    'تحميل درجات الامتحان من الساكند',
    () async {
      _require(canAssess && !isRemote && Zone.current[_lanDeviceKey] == null);
      final room = _examImportRoom(roomId);
      if (room.deviceId == 'local') {
        throw const CenterException('هذا الامتحان يعمل على الهوست نفسه.');
      }
      if (!_state.examMonitors.any(
        (record) => record.importRequestId != null,
      )) {
        await _writeBackup(
          p.join(
            p.dirname(databasePath),
            'backups',
            'before-exam-import-v13-${CenterStore._uuid.v4()}.json',
          ),
          _state.copyForBackup(),
        );
      }
      _state.examMonitors[_state.examMonitors.indexOf(room)] = room
          .withImportRequest(CenterStore._uuid.v4());
    },
  );

  Future<void> _completeExamResultImport(
    String roomId,
    String requestId,
    Map<String, String> problems,
  ) => _change('exam_import_complete', 'إتمام تحميل درجات الساكند', () async {
    _require(canAssess);
    final room = _examImportRoom(roomId);
    _require(room.deviceId == 'lan:${Zone.current[_lanDeviceKey]}');
    if (room.importRequestId != requestId) {
      throw const CenterException('طلب تحميل الدرجات تغيّر. حدّث الربط.');
    }
    if (problems.length > 1000 ||
        problems.entries.any(
          (entry) =>
              !room.identities.values.contains(entry.key) ||
              entry.value.isEmpty ||
              entry.value.length > 400,
        )) {
      throw const CenterException('تقرير تحميل الدرجات غير صالح.');
    }
    _state.examMonitors[_state.examMonitors.indexOf(room)] = room
        .completeImport(problems);
  });

  void _requireRequestedExamResult(IntegratedExamResult result) {
    final deviceId = Zone.current[_lanDeviceKey];
    if (deviceId == null) return;
    final room = _examImportRoom('${result.sourceId}:${result.examId}');
    _require(room.deviceId == 'lan:$deviceId' && room.importPending);
  }
}
