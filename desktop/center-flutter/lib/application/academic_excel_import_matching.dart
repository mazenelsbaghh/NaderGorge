part of 'academic_excel_import.dart';

class AcademicExcelSuggestion {
  const AcademicExcelSuggestion({
    required this.student,
    required this.reason,
    required this.similarity,
  });
  final Student student;
  final String reason;
  final double similarity;
}

class AcademicExcelMatch {
  AcademicExcelMatch({
    required this.row,
    required this.student,
    required Iterable<String> issues,
    required Iterable<AcademicExcelSuggestion> suggestions,
    this.matchReason = '',
  }) : issues = List.unmodifiable(issues),
       suggestions = List.unmodifiable(suggestions);
  final AcademicExcelRow row;
  final Student? student;
  final List<String> issues;
  final List<AcademicExcelSuggestion> suggestions;
  final String matchReason;
  bool get canAutoSelect =>
      student != null && row.eligible && !row.requiresReview && issues.isEmpty;
}

/// Only members of the explicitly selected group can appear in a result.
/// Names rank suggestions; they never establish student identity.
List<AcademicExcelMatch> matchAcademicExcelRows(
  List<AcademicExcelRow> rows, {
  required Iterable<Student> students,
  required String groupId,
}) {
  final roster = students
      .where((student) => student.groupIds.contains(groupId))
      .toList();
  final byCode = <String, Set<Student>>{};
  final byPhone = <String, Set<Student>>{};
  final normalized = <String, _NormalizedStudentMatch>{};
  for (final student in roster) {
    normalized[student.id] = _NormalizedStudentMatch(student);
    for (final code in studentIdentifiers(
      student,
    ).map(normalizeStudentIdentifier)) {
      if (code.isNotEmpty) byCode.putIfAbsent(code, () => {}).add(student);
    }
    for (final phone in [
      student.phone,
      student.guardianPhone,
    ].map(_normalizedPhone)) {
      if (phone.isNotEmpty) byPhone.putIfAbsent(phone, () => {}).add(student);
    }
  }
  final preliminary = <AcademicExcelMatch>[];
  final selectedCounts = <String, int>{};
  final attemptCounts = <String, int>{};
  for (final row in rows) {
    final key = _attemptKey(row);
    if (key != null) attemptCounts.update(key, (n) => n + 1, ifAbsent: () => 1);
  }
  for (final row in rows) {
    final code = normalizeStudentIdentifier(row.code);
    final phone = _normalizedPhone(row.phone);
    final codeMatches = byCode[code] ?? const <Student>{};
    final phoneMatches = byPhone[phone] ?? const <Student>{};
    final issues = <String>[];
    var matchReason = '';
    Student? selected;
    if (codeMatches.length > 1) {
      issues.add('الكود يطابق أكثر من طالب في المجموعة.');
    }
    if (phoneMatches.length > 1) {
      issues.add('الهاتف مشترك بين أكثر من طالب في المجموعة.');
    }
    if (issues.isEmpty) {
      if (codeMatches.length == 1 && phoneMatches.length == 1) {
        if (codeMatches.single.id == phoneMatches.single.id) {
          selected = codeMatches.single;
          matchReason = 'مطابق بالكود والهاتف';
        } else {
          issues.add(
            'مطابقة الكود والهاتف غير متفقة؛ اختر الطالب بعد المراجعة.',
          );
        }
      } else if (codeMatches.length == 1) {
        selected = codeMatches.single;
        matchReason = 'مطابق بالكود';
      } else if (phoneMatches.length == 1) {
        selected = phoneMatches.single;
        matchReason = _normalizedPhone(selected.phone) == phone
            ? 'مطابق بهاتف الطالب'
            : 'مطابق بهاتف ولي الأمر';
      }
    }
    if (selected == null && issues.isEmpty) {
      issues.add('لا توجد مطابقة مؤكدة داخل المجموعة.');
    }
    if (selected != null) {
      selectedCounts.update(
        selected.id,
        (value) => value + 1,
        ifAbsent: () => 1,
      );
    }
    if ((attemptCounts[_attemptKey(row)] ?? 0) > 1) {
      issues.add(
        'معرف المحاولة مكرر في الملف؛ راجع الصفوف واختر محاولة واحدة.',
      );
    }
    preliminary.add(
      AcademicExcelMatch(
        row: row,
        student: selected,
        matchReason: matchReason,
        issues: issues,
        suggestions: selected == null
            ? _suggestions(row, roster, normalized, codeMatches, phoneMatches)
            : const [],
      ),
    );
  }
  return List.unmodifiable(
    preliminary.map((match) {
      final student = match.student;
      if (student == null || selectedCounts[student.id] == 1) return match;
      return AcademicExcelMatch(
        row: match.row,
        student: null,
        issues: [
          ...match.issues,
          'الطالب مكرر في أكثر من صف؛ اختر نتيجة واحدة فقط.',
        ],
        suggestions: [
          AcademicExcelSuggestion(
            student: student,
            reason: 'مطابقة مؤكدة لكن الطالب مكرر في الملف',
            similarity: 1,
          ),
        ],
      );
    }),
  );
}

String? _attemptKey(AcademicExcelRow row) {
  final session = row.metadata['externalSessionId']!;
  final attempt = row.metadata['externalAttemptId']!;
  if (_blankPlaceholder(attempt)) return null;
  final version = row.metadata['sheetVersion']!;
  return jsonEncode([
    _blankPlaceholder(session) ? null : session,
    attempt,
    _blankPlaceholder(version) ? null : version,
  ]);
}

String _normalizedPhone(String value) {
  var phone = normalizeStudentIdentifier(
    value,
  ).replaceAll(RegExp(r'[\s()\-\u200e\u200f]'), '');
  if (phone.startsWith('+')) phone = phone.substring(1);
  if (!RegExp(r'^\d+$').hasMatch(phone)) return '';
  if (RegExp(r'^00201[0125]\d{8}$').hasMatch(phone)) phone = phone.substring(4);
  if (RegExp(r'^201[0125]\d{8}$').hasMatch(phone)) phone = phone.substring(2);
  // Excel often stores an Egyptian mobile as a number and drops its leading 0.
  // This is restricted to the four Egyptian mobile prefixes, never arbitrary IDs.
  if (RegExp(r'^1[0125]\d{8}$').hasMatch(phone)) phone = '0$phone';
  return phone;
}

Set<String> _nameTokens(String value) => _normalizedWords(
  value,
).replaceAll('ة', 'ه').split(' ').where((s) => s.isNotEmpty).toSet();

List<AcademicExcelSuggestion> _suggestions(
  AcademicExcelRow row,
  List<Student> roster,
  Map<String, _NormalizedStudentMatch> normalized,
  Set<Student> codeMatches,
  Set<Student> phoneMatches,
) {
  final rowTokens = _nameTokens(row.name);
  final result = <AcademicExcelSuggestion>[];
  final code = normalizeStudentIdentifier(row.code);
  final phone = _normalizedPhone(row.phone);
  for (final student in roster) {
    final fields = normalized[student.id]!;
    final tokens = fields.tokens;
    final overlap = rowTokens.intersection(tokens).length;
    final nameSimilarity = rowTokens.isEmpty || tokens.isEmpty
        ? 0.0
        : (2 * overlap) / (rowTokens.length + tokens.length);
    var similarity = nameSimilarity;
    var reason = 'تشابه الاسم؛ اقتراح يحتاج مراجعة';
    if (codeMatches.contains(student)) {
      similarity = 1;
      reason = 'الكود مطابق؛ راجع تعارض البيانات';
    } else if (phoneMatches.contains(student)) {
      similarity = .98;
      final ownPhone = fields.phone == phone;
      reason = ownPhone
          ? 'هاتف الطالب مطابق؛ يحتاج مراجعة'
          : 'هاتف ولي الأمر مطابق؛ يحتاج مراجعة';
    } else {
      final nearCode =
          code.length >= 4 &&
          fields.codes.any(
            (identifier) => _oneCharacterDifferent(code, identifier),
          );
      final nearPhone =
          phone.length >= 10 &&
          [
            fields.phone,
            fields.guardianPhone,
          ].any((number) => _oneCharacterDifferent(phone, number));
      if (nearCode || nearPhone) {
        similarity = math.max(similarity, .6 + nameSimilarity * .2);
        reason = nearCode
            ? 'اختلاف حرف واحد في الكود؛ راجع الاسم'
            : 'اختلاف رقم واحد في الهاتف؛ راجع الاسم';
      } else if (overlap < 2 || nameSimilarity < .5) {
        continue;
      }
    }
    final suggestion = AcademicExcelSuggestion(
      student: student,
      reason: reason,
      similarity: similarity,
    );
    final insertion = result.indexWhere(
      (existing) => _compareSuggestion(suggestion, existing) < 0,
    );
    if (insertion >= 0) {
      result.insert(insertion, suggestion);
    } else if (result.length < 3) {
      result.add(suggestion);
    }
    if (result.length > 3) result.removeLast();
  }
  return List.unmodifiable(result);
}

bool _oneCharacterDifferent(String first, String second) {
  if (first.length != second.length) return false;
  var differences = 0;
  for (var i = 0; i < first.length; i++) {
    if (first[i] != second[i] && ++differences > 1) return false;
  }
  return differences == 1;
}

int _compareSuggestion(
  AcademicExcelSuggestion first,
  AcademicExcelSuggestion second,
) {
  final score = second.similarity.compareTo(first.similarity);
  return score != 0 ? score : first.student.id.compareTo(second.student.id);
}

class _NormalizedStudentMatch {
  _NormalizedStudentMatch(Student student)
    : tokens = _nameTokens(student.name),
      codes = studentIdentifiers(
        student,
      ).map(normalizeStudentIdentifier).toList(),
      phone = _normalizedPhone(student.phone),
      guardianPhone = _normalizedPhone(student.guardianPhone);
  final Set<String> tokens;
  final List<String> codes;
  final String phone, guardianPhone;
}
