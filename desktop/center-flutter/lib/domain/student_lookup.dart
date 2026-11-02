import 'models.dart';

/// Preserve leading zeros; scanner identifiers are strings, never numbers.
String normalizeStudentIdentifier(String value) {
  const arabic = '٠١٢٣٤٥٦٧٨٩';
  const persian = '۰۱۲۳۴۵۶۷۸۹';
  return value.trim().toLowerCase().split('').map((character) {
    final arabicIndex = arabic.indexOf(character);
    if (arabicIndex >= 0) {
      return '$arabicIndex';
    }
    final persianIndex = persian.indexOf(character);
    return persianIndex >= 0 ? '$persianIndex' : character;
  }).join();
}

/// Egyptian mobile aliases are shared by Excel and local exam matching.
String normalizeStudentPhone(String value) {
  var phone = normalizeStudentIdentifier(
    value,
  ).replaceAll(RegExp(r'[\s()\-\u200e\u200f]'), '');
  if (phone.startsWith('+')) phone = phone.substring(1);
  if (!RegExp(r'^\d+$').hasMatch(phone)) return '';
  if (RegExp(r'^00201[0125]\d{8}$').hasMatch(phone)) phone = phone.substring(4);
  if (RegExp(r'^201[0125]\d{8}$').hasMatch(phone)) phone = phone.substring(2);
  if (RegExp(r'^1[0125]\d{8}$').hasMatch(phone)) phone = '0$phone';
  return phone;
}

Iterable<String> studentIdentifiers(Student student) {
  final code = normalizeStudentIdentifier(student.code);
  return [
    student.code,
    // Short forms omit only leading zeros from the five-digit printed code.
    // Other lengths, nonnumeric codes and the full barcode retain exact identity.
    if (RegExp(r'^0[0-9]{4}$').hasMatch(code))
      for (
        var start = 1;
        start < code.length && code[start - 1] == '0';
        start++
      )
        code.substring(start),
    if (student.barcode.trim().isNotEmpty) student.barcode,
  ];
}

class StudentCodeAllocator {
  StudentCodeAllocator(Iterable<Student> students) {
    for (final student in students) {
      reserve(student);
    }
  }

  final _occupied = <String>{};
  var _next = BigInt.from(10000);

  void reserve(Student student) {
    _occupied.addAll(
      studentIdentifiers(student).map(normalizeStudentIdentifier),
    );
    final code = normalizeStudentIdentifier(student.code);
    if (!RegExp(r'^[0-9]+$').hasMatch(code)) return;
    final number = BigInt.parse(code);
    _occupied.add(number.toString());
    if (number >= _next) _next = number + BigInt.one;
  }

  String get nextCode {
    // Fill gaps without reusing legacy aliases or full barcodes.
    for (var candidate = 1000; candidate <= 9999; candidate++) {
      if (!_occupied.contains('$candidate')) return '$candidate';
    }
    var candidate = _next;
    while (_occupied.contains(candidate.toString())) {
      candidate += BigInt.one;
    }
    return candidate.toString();
  }
}

List<Student> studentsWithIdentifier(Iterable<Student> students, String input) {
  final query = normalizeStudentIdentifier(input);
  if (query.isEmpty) {
    return [];
  }
  return students
      .where(
        (student) => studentIdentifiers(
          student,
        ).any((identifier) => normalizeStudentIdentifier(identifier) == query),
      )
      .toList();
}

bool studentMatchesSearch(Student student, String input) {
  final query = normalizeStudentIdentifier(input);
  return query.isEmpty ||
      [
        ...studentIdentifiers(student),
        student.name,
        student.phone,
        student.guardianPhone,
      ].any((value) => normalizeStudentIdentifier(value).contains(query));
}

/// Exact identifiers take precedence over partial identifiers and names.
/// Callers must explicitly choose when this returns more than one student.
List<Student> studentLookupCandidates(
  Iterable<Student> students,
  String input,
) {
  final exact = studentsWithIdentifier(students, input);
  if (exact.isNotEmpty) {
    return exact;
  }
  if (input.trim().isEmpty) {
    return [];
  }
  return students
      .where((student) => studentMatchesSearch(student, input))
      .toList();
}

/// Normalized identifiers and search fields for one immutable student roster.
class StudentLookupIndex {
  StudentLookupIndex(Iterable<Student> students)
    : _students = List.unmodifiable(students) {
    for (final student in _students) {
      final identifiers = studentIdentifiers(
        student,
      ).map(normalizeStudentIdentifier).toSet();
      for (final identifier in identifiers) {
        _exact.putIfAbsent(identifier, () => []).add(student);
      }
      _searchFields.add([
        ...identifiers,
        normalizeStudentIdentifier(student.name),
        normalizeStudentIdentifier(student.phone),
        normalizeStudentIdentifier(student.guardianPhone),
      ]);
    }
  }

  final List<Student> _students;
  final _exact = <String, List<Student>>{};
  final _searchFields = <List<String>>[];

  List<Student> candidates(String input) {
    final query = normalizeStudentIdentifier(input);
    if (query.isEmpty) return const [];
    final exact = _exact[query];
    if (exact != null) return List.unmodifiable(exact);
    return [
      for (var index = 0; index < _students.length; index++)
        if (_searchFields[index].any((field) => field.contains(query)))
          _students[index],
    ];
  }
}
