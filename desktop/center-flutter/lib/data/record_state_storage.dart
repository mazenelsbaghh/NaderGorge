import 'package:sqflite_common_ffi/sqflite_ffi.dart';

import 'center_state_encoder.dart';

/// The compatibility view reconstructs a full snapshot only when read/exported.
Future<void> createRecordStateStorage(DatabaseExecutor database) async {
  await database.execute(
    'CREATE TABLE state_identity (id INTEGER PRIMARY KEY CHECK(id=1))',
  );
  await database.execute(
    'CREATE TABLE state_fields (name TEXT PRIMARY KEY, kind INTEGER NOT NULL CHECK(kind IN (0,1)), payload TEXT NOT NULL)',
  );
  await database.execute(
    'CREATE TABLE state_records (section TEXT NOT NULL, position INTEGER NOT NULL, payload TEXT NOT NULL, PRIMARY KEY(section,position)) WITHOUT ROWID',
  );
  await database.execute('''CREATE VIEW state AS SELECT id, (
    SELECT json_group_object(name, json(CASE WHEN kind=0 THEN payload ELSE
      COALESCE((SELECT '[' || group_concat(payload, ',') || ']' FROM
        (SELECT payload FROM state_records WHERE section=state_fields.name ORDER BY position)), '[]') END))
    FROM state_fields) AS payload FROM state_identity''');
  // Private recovery tools can keep using the complete logical snapshot boundary.
  for (final verb in ['INSERT', 'UPDATE']) {
    await database.execute(
      '''CREATE TRIGGER state_${verb.toLowerCase()} INSTEAD OF $verb ON state BEGIN
      INSERT OR IGNORE INTO state_identity(id) VALUES(1);
      DELETE FROM state_records;
      DELETE FROM state_fields;
      INSERT INTO state_fields(name,kind,payload)
        SELECT key, CASE WHEN type='array' THEN 1 ELSE 0 END,
          CASE WHEN type='array' THEN '[]' WHEN type='text' THEN json_quote(value)
               WHEN type='null' THEN 'null' WHEN type='true' THEN 'true'
               WHEN type='false' THEN 'false' ELSE CAST(value AS TEXT) END
        FROM json_each(NEW.payload);
      INSERT INTO state_records(section,position,payload)
        SELECT field.key, row.key,
          CASE WHEN row.type='text' THEN json_quote(row.value)
               WHEN row.type='null' THEN 'null' WHEN row.type='true' THEN 'true'
               WHEN row.type='false' THEN 'false' ELSE CAST(row.value AS TEXT) END
        FROM json_each(NEW.payload) AS field, json_each(field.value) AS row
        WHERE field.type='array';
    END''',
    );
  }
  await database.execute(
    '''CREATE TRIGGER state_delete INSTEAD OF DELETE ON state BEGIN
    DELETE FROM state_records; DELETE FROM state_fields;
    DELETE FROM state_identity WHERE id=OLD.id;
  END''',
  );
}

Future<int> replaceRecordState(
  DatabaseExecutor database,
  EncodedStorageFields fields,
) async {
  final present = await database.query(
    'state_identity',
    columns: ['id'],
    where: 'id=1',
  );
  if (present.isEmpty) return 0;
  final batch = database.batch()
    ..delete('state_records')
    ..delete('state_fields');
  for (final name in fields.keys) {
    _replaceField(batch, name, fields);
  }
  await batch.commit(noResult: true);
  return (await database.query(
    'state_identity',
    columns: ['id'],
    where: 'id=1',
  )).length;
}

Future<int> updateRecordState(
  DatabaseExecutor database,
  EncodedStorageFields previous,
  EncodedStorageFields current,
) async {
  final present = await database.query(
    'state_identity',
    columns: ['id'],
    where: 'id=1',
  );
  if (present.isEmpty) return 0;
  final batch = database.batch();
  for (final name in previous.keys.where(
    (name) => !current.containsKey(name),
  )) {
    batch.delete('state_records', where: 'section=?', whereArgs: [name]);
    batch.delete('state_fields', where: 'name=?', whereArgs: [name]);
  }
  for (final name in current.changedKeysFrom(previous)) {
    final before = previous.recordFragments(name);
    final after = current.recordFragments(name);
    if (before == null || after == null) {
      batch.delete('state_records', where: 'section=?', whereArgs: [name]);
      _replaceField(batch, name, current);
      continue;
    }
    var prefix = 0;
    while (prefix < before.length &&
        prefix < after.length &&
        before[prefix] == after[prefix]) {
      prefix++;
    }
    batch.delete(
      'state_records',
      where: 'section=? AND position>=?',
      whereArgs: [name, after.length],
    );
    for (var position = prefix; position < after.length; position++) {
      if (position < before.length && before[position] == after[position]) {
        continue;
      }
      batch.insert('state_records', {
        'section': name,
        'position': position,
        'payload': after[position],
      }, conflictAlgorithm: ConflictAlgorithm.replace);
    }
  }
  await batch.commit(noResult: true);
  return (await database.query(
    'state_identity',
    columns: ['id'],
    where: 'id=1',
  )).length;
}

void _replaceField(Batch batch, String name, EncodedStorageFields fields) {
  final records = fields.recordFragments(name);
  batch.insert('state_fields', {
    'name': name,
    'kind': records == null ? 0 : 1,
    'payload': records == null ? fields[name]! : '[]',
  }, conflictAlgorithm: ConflictAlgorithm.replace);
  if (records == null) return;
  for (var position = 0; position < records.length; position++) {
    batch.insert('state_records', {
      'section': name,
      'position': position,
      'payload': records[position],
    });
  }
}
