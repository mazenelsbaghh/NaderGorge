import 'dart:convert';

import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'center_state_encoder.dart';

/// Sends changed JSON sections across the SQLite isolate boundary. The stored
/// row remains a complete snapshot for existing readers, backups and rollback.
Future<int> updateStoredStateFields(
  DatabaseExecutor transaction,
  Map<String, String> previous,
  Map<String, String> current,
  Map<String, List<String>> appends,
) {
  var expression = 'payload';
  final arguments = <Object?>[];
  final removed = previous.keys.where((key) => !current.containsKey(key));
  if (removed.isNotEmpty) {
    expression =
        'json_remove($expression, ${removed.map((_) => '?').join(',')})';
    arguments.addAll(removed.map(_fieldPath));
  }
  final changed =
      (previous is EncodedStorageFields && current is EncodedStorageFields
              ? current.changedKeysFrom(previous)
              : current.keys.where((key) => previous[key] != current[key]))
          .toList();
  if (changed.isNotEmpty) {
    final replacements = <(String, String)>[];
    var extraPaths = 48 - changed.length;
    for (final key in changed) {
      final records = appends[key];
      // Bound JSON function arguments even when several sections grow at once.
      if (previous.containsKey(key) &&
          records != null &&
          records.isNotEmpty &&
          records.length - 1 <= extraPaths) {
        extraPaths -= records.length - 1;
        replacements.addAll([
          for (final record in records) ('${_fieldPath(key)}[#]', record),
        ]);
      } else {
        replacements.add((_fieldPath(key), current[key]!));
      }
    }
    expression =
        'json_set($expression, ${replacements.map((_) => '?, json(?)').join(',')})';
    for (final (path, json) in replacements) {
      arguments.addAll([path, json]);
    }
  }
  // Even an unchanged payload must detect a missing authoritative row.
  return transaction.rawUpdate(
    'UPDATE state SET payload = $expression WHERE id = 1',
    arguments,
  );
}

String _fieldPath(String key) => '\$.${jsonEncode(key)}';
