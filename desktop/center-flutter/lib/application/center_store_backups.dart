part of 'center_store.dart';

extension _AutomaticCenterBackups on CenterStore {
  void _startAutomaticBackups() {
    _automaticBackupTimer = Timer.periodic(
      CenterStore.automaticBackupInterval,
      (_) => unawaited(_runAutomaticBackup()),
    );
    if (_lastAutomaticBackupAt == null) unawaited(_runAutomaticBackup());
  }

  Future<void> _runAutomaticBackup() async {
    if (_closed || _automaticBackupRunning || _automaticBackupTimer == null) {
      return;
    }
    _automaticBackupRunning = true;
    try {
      await _exclusive(() async {
        if (_automaticBackupTimer == null || _state.staff.isEmpty) return;
        await _saveAutomaticBackup();
      }, operation: 'backup.automatic');
    } on CenterException catch (error) {
      // _exclusive records the underlying failure before wrapping it.
      _automaticBackupError = error.message;
    } finally {
      _automaticBackupRunning = false;
      if (!_closed) _notifyBackupStatus();
    }
  }

  Future<void> _saveAutomaticBackup() async {
    final now = DateTime.now().toUtc();
    final stamp = now.toIso8601String().replaceAll(':', '-');
    final destination = p.join(
      automaticBackupDirectory!,
      'auto-$stamp-${CenterStore._uuid.v4()}.json',
    );
    await _writeBackup(destination, _state);
    _lastAutomaticBackupAt = now;
    await _pruneAutomaticBackups(destination);
    _automaticBackupError = null;
  }

  Future<void> _preserveDataBeforeUpdate({
    required bool existingDatabase,
  }) async {
    const build = String.fromEnvironment(
      'MASSAR_BUILD_ID',
      defaultValue: 'development',
    );
    final marker = File(
      p.join(p.dirname(databasePath), 'last-opened-build.json'),
    );
    String? previousBuild;
    if (await marker.exists()) {
      final data =
          jsonDecode(await marker.readAsString()) as Map<String, dynamic>;
      if (data['version'] != 1 || data['buildIdentifier'] is! String) {
        throw const CenterException(
          'تعذر مراجعة تعريف التحديث. الداتا محفوظة؛ راجع سجل المشاكل.',
        );
      }
      previousBuild = data['buildIdentifier'] as String;
    }
    if (previousBuild == build) return;
    // Missing metadata on an existing installation is treated as an update.
    if (existingDatabase) await _saveAutomaticBackup();
    final temporary = File('${marker.path}.${CenterStore._uuid.v4()}.tmp');
    try {
      await temporary.writeAsString(
        jsonEncode({
          'version': 1,
          'buildIdentifier': build,
          'openedAt': DateTime.now().toUtc().toIso8601String(),
        }),
        flush: true,
      );
      await temporary.rename(marker.path);
    } catch (error, stackTrace) {
      try {
        if (await temporary.exists()) await temporary.delete();
      } on FileSystemException catch (cleanupError, cleanupStack) {
        reportProblem(
          cleanupError,
          cleanupStack,
          operation: 'backup.before_update',
        );
      }
      Error.throwWithStackTrace(error, stackTrace);
    }
  }

  Future<void> _pruneAutomaticBackups(String newestPath) async {
    final names = RegExp(
      r'^auto-\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}\.\d{3,6}Z-[0-9a-f-]{36}\.json$',
    );
    final files = await Directory(automaticBackupDirectory!)
        .list(followLinks: false)
        .where(
          (entry) => entry is File && names.hasMatch(p.basename(entry.path)),
        )
        .cast<File>()
        .toList();
    files.sort((a, b) => p.basename(b.path).compareTo(p.basename(a.path)));
    // Retain the new complete file even if the computer clock moved backwards.
    files.removeWhere((file) => p.equals(file.path, newestPath));
    final oldFilesToKeep = CenterStore.automaticBackupLimit - 1;
    // 0..49 older files: keep all; 50+: remove only the surplus after success.
    for (final file in files.skip(oldFilesToKeep)) {
      await file.delete();
    }
  }
}
