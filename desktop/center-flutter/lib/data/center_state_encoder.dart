import 'dart:collection';
import 'dart:convert';

import 'center_state.dart';
import 'copy_on_write_list.dart';

/// Reuses JSON for immutable records while still writing a complete snapshot.
/// Group month plans remain mutable, so groups are always encoded afresh.
/// Only records with immutable fields and nested collections belong in this cache.
class CenterStateEncoder {
  final _records = Expando<String>();
  final _sections = <String, _EncodedSection>{};
  final _sectionIdentities = <String, Object>{};
  final _publicHistory = <String, EncodedPublicCenterState>{};
  static const _historyLimit = 16;
  static const _historyTextBytes = 32 * 1024 * 1024;

  String encode(CenterState state) {
    final fields = state.mapJsonFields(_encodeRecords);
    return _encodeFields(fields);
  }

  /// Frozen local fields include credentials and must never enter LAN responses.
  EncodedStorageFields encodeStorageFields(CenterState state) =>
      EncodedStorageFields._({
        for (final entry in state.mapJsonFields(_encodeRecords).entries)
          entry.key: _freezeField(entry.value, null),
      });

  /// New attendance and audit rows usually extend immutable record lists.
  /// Large batches and edits keep using complete section replacement.
  Map<String, List<String>> encodeStorageAppends(
    CenterState previous,
    CenterState current,
  ) {
    final before = <String, List<Object>>{};
    previous.mapJsonFields(<T extends Object>(section, records, toJson) {
      if (centerStateImmutableRecordSections.contains(section)) {
        before[section] = records;
      }
      return const [];
    });
    final appends = <String, List<String>>{};
    current.mapJsonFields(<T extends Object>(section, records, toJson) {
      final original = before[section];
      if (original == null ||
          records.length <= original.length ||
          records.length - original.length > 16) {
        return const [];
      }
      for (var index = 0; index < original.length; index++) {
        if (!identical(original[index], records[index])) return const [];
      }
      appends[section] = [
        for (final record in records.skip(original.length))
          _records[record] ??= jsonEncode(toJson(record)),
      ];
      return const [];
    });
    return appends;
  }

  EncodedPublicCenterState encodePublicSnapshot(CenterState state) {
    final fields = state.mapJsonFields(_encodeRecords)..remove('credentials');
    final previous = _publicHistory.isEmpty ? null : _publicHistory.values.last;
    return EncodedPublicCenterState._({
      for (final entry in fields.entries)
        entry.key: _freezeField(entry.value, previous?._fields[entry.key]),
    });
  }

  Map<String, EncodedPublicCenterState> encodePublicChanges(
    CenterState state,
    String version, {
    String? baseVersion,
  }) {
    // Resolve the requested base before insertion can evict it from history.
    final previous = _publicHistory[baseVersion];
    final retained = _publicHistory[version];
    final current = retained ?? encodePublicSnapshot(state);
    final delta = previous == null
        ? null
        : _encodeDelta(baseVersion!, previous, current);
    if (retained == null) {
      _publicHistory[version] = current;
      _prunePublicHistory();
    }
    return delta != null && delta.jsonLength < current.jsonLength
        ? {'stateDelta': delta}
        : {'state': current};
  }

  void clearPublicHistory() => _publicHistory.clear();

  Object _freezeField(Object? field, Object? previous) {
    if (field is _EncodedSection) return field;
    final encoded = jsonEncode(field);
    return previous is String && previous == encoded ? previous : encoded;
  }

  EncodedPublicCenterState _encodeDelta(
    String baseVersion,
    EncodedPublicCenterState previous,
    EncodedPublicCenterState current,
  ) {
    final replacements = <String, Object>{};
    final splices = <String, Object>{};
    for (final entry in current._fields.entries) {
      final before = previous._fields[entry.key];
      final after = entry.value;
      if (entry.key == 'schemaVersion') {
        replacements[entry.key] = after;
      } else if (before is _EncodedSection && after is _EncodedSection) {
        if (identical(before, after)) continue;
        final splice = _encodeSplice(before, after);
        if (splice.length < after.jsonLength) {
          splices[entry.key] = splice;
        } else {
          replacements[entry.key] = after;
        }
      } else if (before != after) {
        replacements[entry.key] = after;
      }
    }
    return EncodedPublicCenterState._({
      'baseVersion': jsonEncode(baseVersion),
      'set': _encodeFrozenFields(replacements),
      'splices': _encodeFrozenFields(splices),
      'remove': jsonEncode([
        for (final key in previous._fields.keys)
          if (!current._fields.containsKey(key)) key,
      ]),
    });
  }

  String _encodeSplice(_EncodedSection previous, _EncodedSection current) {
    final before = previous.records;
    final after = current.records;
    var start = 0;
    while (start < before.length &&
        start < after.length &&
        identical(before[start], after[start])) {
      start++;
    }
    var beforeEnd = before.length;
    var afterEnd = after.length;
    while (beforeEnd > start &&
        afterEnd > start &&
        identical(before[beforeEnd - 1], after[afterEnd - 1])) {
      beforeEnd--;
      afterEnd--;
    }
    final inserted = after
        .getRange(start, afterEnd)
        .map((record) => _records[record]!)
        .join(',');
    return '{"start":$start,"deleteCount":${beforeEnd - start},'
        '"previousLength":${before.length},"items":[$inserted]}';
  }

  void _prunePublicHistory() {
    while (_publicHistory.length > 1 &&
        (_publicHistory.length > _historyLimit ||
            _retainedPublicTextBytes() > _historyTextBytes)) {
      _publicHistory.remove(_publicHistory.keys.first);
    }
  }

  int _retainedPublicTextBytes() {
    final fragments = Set<Object>.identity();
    for (final snapshot in _publicHistory.values) {
      for (final field in snapshot._fields.values) {
        if (field is _EncodedSection) {
          fragments.addAll(field.fragments);
        } else {
          fragments.add(field);
        }
      }
    }
    // Budget complete section text without joining it just for accounting.
    // Shared sections count once; distinct sections are counted conservatively.
    return fragments.fold(
      0,
      (bytes, field) => bytes + _frozenLength(field) * 2,
    );
  }

  String _encodeFields(Map<String, dynamic> fields) {
    return '{${fields.entries.map((entry) {
      final field = entry.value;
      final encoded = field is _EncodedSection ? field.json : jsonEncode(field);
      return '${jsonEncode(entry.key)}:$encoded';
    }).join(',')}}';
  }

  Object _encodeRecords<T extends Object>(
    String section,
    List<T> records,
    Map<String, dynamic> Function(T) toJson,
  ) {
    if (!centerStateImmutableRecordSections.contains(section)) {
      return records.map(toJson).toList();
    }
    final previous = _sections[section];
    final identity = rowsIdentity(records);
    if (previous != null &&
        ((records is CopyOnWriteList &&
                identical(_sectionIdentities[section], identity)) ||
            previous.matches(records))) {
      _sectionIdentities[section] = identity;
      return previous;
    }
    _sectionIdentities[section] = identity;
    return _sections[section] = _EncodedSection(
      List<Object>.unmodifiable(records),
      List<String>.unmodifiable(
        records.map(
          (record) => _records[record] ??= jsonEncode(toJson(record)),
        ),
      ),
    );
  }
}

/// Freezes mutable fields immediately; immutable sections join only if needed.
/// This map contains credentials and belongs exclusively to local persistence.
final class EncodedStorageFields extends UnmodifiableMapBase<String, String> {
  EncodedStorageFields._(Map<String, Object> fields)
    : _fields = Map.unmodifiable(fields);
  final Map<String, Object> _fields;

  @override
  Iterable<String> get keys => _fields.keys;

  @override
  bool containsKey(Object? key) => _fields.containsKey(key);

  @override
  String? operator [](Object? key) {
    final field = _fields[key];
    return field == null ? null : _frozenJson(field);
  }

  List<String>? recordFragments(String section) {
    final field = _fields[section];
    if (field == null) return null;
    return field is _EncodedSection
        ? field.fragments
        : _jsonArrayFragments(field as String);
  }

  Iterable<String> changedKeysFrom(EncodedStorageFields previous) =>
      keys.where((key) => _fields[key] != previous._fields[key]);
}

/// The constructor is private so only structurally redacted state can be sent.
/// Its immutable JSON captures the committed snapshot before the queue advances.
final class EncodedPublicCenterState {
  EncodedPublicCenterState._(Map<String, Object> fields)
    : _fields = Map.unmodifiable(fields);
  final Map<String, Object> _fields;
  String get json => _encodeFrozenFields(_fields);
  int get jsonLength =>
      2 +
      (_fields.isEmpty ? 0 : _fields.length - 1) +
      _fields.entries.fold(
        0,
        (length, entry) =>
            length +
            jsonEncode(entry.key).length +
            1 +
            _frozenLength(entry.value),
      );
}

String _frozenJson(Object field) =>
    field is _EncodedSection ? field.json : field as String;

int _frozenLength(Object field) =>
    field is _EncodedSection ? field.jsonLength : (field as String).length;

String _encodeFrozenFields(Map<String, Object> fields) =>
    '{${fields.entries.map((entry) => '${jsonEncode(entry.key)}:${_frozenJson(entry.value)}').join(',')}}';

class _EncodedSection {
  _EncodedSection(this.records, this.fragments);
  final List<Object> records;
  final List<String> fragments;
  late final String json = '[${fragments.join(',')}]';
  late final int jsonLength =
      2 +
      (fragments.isEmpty ? 0 : fragments.length - 1) +
      fragments.fold(0, (length, fragment) => length + fragment.length);

  bool matches(List<Object> next) {
    if (records.length != next.length) return false;
    for (var index = 0; index < records.length; index++) {
      if (!identical(records[index], next[index])) return false;
    }
    return true;
  }
}

List<String>? _jsonArrayFragments(String encoded) {
  if (!encoded.startsWith('[')) return null;
  return (jsonDecode(encoded) as List).map(jsonEncode).toList(growable: false);
}
