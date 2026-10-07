part of 'center_state.dart';

const _removableStateFields = {
  'installationSeedId',
  'appliedDataRepairs',
  'defaultMonthPriceVersion',
  'studyMonths',
  'centerFees',
  'debtSettlements',
};

extension CenterStatePublicPatch on CenterState {
  /// Builds a new read snapshot; a malformed patch cannot change the old one.
  CenterState applyPublicDelta(
    Map<String, dynamic> delta, {
    required String baseVersion,
  }) {
    if (delta.length != 4 ||
        !delta.keys.toSet().containsAll({
          'baseVersion',
          'set',
          'splices',
          'remove',
        }) ||
        baseVersion.isEmpty ||
        baseVersion.length > 128 ||
        delta['baseVersion'] != baseVersion) {
      throw const FormatException('Invalid state delta base');
    }
    final replacements = Map<String, dynamic>.from(delta['set'] as Map);
    final splices = Map<String, dynamic>.from(delta['splices'] as Map);
    final removed = List<String>.from(delta['remove'] as List);
    final fields = mapJsonFields(_retainStateRows, includeEmptyFields: true)
      ..['credentials'] = <String, dynamic>{};
    final changed = [...replacements.keys, ...splices.keys, ...removed];
    if (!replacements.containsKey('schemaVersion') ||
        replacements['schemaVersion'] is! int ||
        changed.toSet().length != changed.length ||
        changed.any(
          (key) => key == 'credentials' || !fields.containsKey(key),
        ) ||
        removed.any((key) => !_removableStateFields.contains(key))) {
      throw const FormatException('Invalid state delta fields');
    }
    fields.addAll(replacements);
    for (final section in splices.entries) {
      if (!centerStateImmutableRecordSections.contains(section.key)) {
        throw const FormatException('Invalid state splice section');
      }
      final retained = fields[section.key] as _RetainedStateRows;
      fields[section.key] = retained.withSplice(
        Map<String, dynamic>.from(section.value as Map),
      );
    }
    for (final key in removed) {
      fields.remove(key);
    }
    return CenterState._fromFields(fields);
  }
}

Object _retainStateRows<T extends Object>(
  String section,
  List<T> records,
  Map<String, dynamic> Function(T) toJson,
) => _RetainedStateRows(records);

class _RetainedStateRows {
  const _RetainedStateRows(this.records, [this.splice]);
  final List<Object> records;
  final Map<String, dynamic>? splice;
  int get length => splice == null
      ? records.length
      : records.length -
            (splice!['deleteCount'] as int) +
            (splice!['items'] as List).length;

  _RetainedStateRows withSplice(Map<String, dynamic> supplied) {
    final start = supplied['start'];
    final deleteCount = supplied['deleteCount'];
    if (supplied.length != 4 ||
        !supplied.keys.toSet().containsAll({
          'start',
          'deleteCount',
          'previousLength',
          'items',
        }) ||
        supplied['previousLength'] is! int ||
        supplied['previousLength'] != records.length ||
        start is! int ||
        deleteCount is! int ||
        supplied['items'] is! List ||
        start < 0 ||
        start > records.length ||
        deleteCount < 0 ||
        deleteCount > records.length - start) {
      throw const FormatException('Invalid state splice range');
    }
    return _RetainedStateRows(records, supplied);
  }

  List<T> decode<T>(T Function(Map<String, dynamic>) parse) {
    final previous = records as List<T>;
    final change = splice;
    if (change == null) return previous;
    final start = change['start'] as int;
    final deleteCount = change['deleteCount'] as int;
    final inserted = (change['items'] as List)
        .map((row) => parse(Map<String, dynamic>.from(row as Map)))
        .toList();
    return [
      ...previous.take(start),
      ...inserted,
      ...previous.skip(start + deleteCount),
    ];
  }
}
