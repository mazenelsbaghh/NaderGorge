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

Iterable<String> studentIdentifiers(Student student) {
  final code = normalizeStudentIdentifier(student.code);
  return [
    student.code,
    // Five-digit codes keep their printed zero, but accept the other four digits.
    // Other lengths, nonnumeric codes and the full barcode retain exact identity.
    if (RegExp(r'^0[0-9]{4}$').hasMatch(code)) code.substring(1),
    if (student.barcode.trim().isNotEmpty) student.barcode,
  ];
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
