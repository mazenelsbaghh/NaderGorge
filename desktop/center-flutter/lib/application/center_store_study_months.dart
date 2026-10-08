part of 'center_store.dart';

extension _StudyMonthsStore on CenterStore {
  Future<StudyMonth> _saveStudyMonth(StudyMonth draft) async {
    late StudyMonth saved;
    await _change('study_month_save', 'حفظ الشهر وحصصه المشتركة', () async {
      _require(canManage);
      final old = draft.id.isEmpty
          ? null
          : _state.studyMonths.where((m) => m.id == draft.id).firstOrNull;
      if (draft.id.isNotEmpty && old == null) {
        throw const CenterException(
          'الشهر لم يعد موجودًا؛ افتح قائمة الشهور من جديد.',
        );
      }
      final nextNumber =
          _state.studyMonths.fold<int>(0, (n, m) => max(n, m.number)) + 1;
      saved = draft.copyWith(
        id: old?.id ?? CenterStore._uuid.v4(),
        name: draft.name.trim(),
        number: old?.number ?? nextNumber,
        lessons: draft.lessons
            .map(
              (lesson) => lesson.copyWith(
                id: lesson.id.isEmpty ? CenterStore._uuid.v4() : lesson.id,
                name: lesson.name.trim(),
              ),
            )
            .toList(),
      );
      if (saved.lessons.isEmpty || saved.lessons.length > 500) {
        throw const CenterException('اختر عدد حصص من ١ إلى ٥٠٠ للشهر.');
      }
      if (_state.studyMonths.any(
        (m) =>
            m.id != saved.id &&
            m.name.toLowerCase() == saved.name.toLowerCase(),
      )) {
        throw const CenterException(
          'اسم الشهر موجود بالفعل؛ اختر اسمًا مختلفًا.',
        );
      }
      for (final lesson in old?.lessons ?? <PreparedLesson>[]) {
        final referenced =
            _state.sessions.any((s) => s.preparedLessonId == lesson.id) ||
            _state.academicActivities.any(
              (a) => a.preparedLessonId == lesson.id,
            );
        if (!referenced) continue;
        final replacement = saved.lessons
            .where((l) => l.id == lesson.id)
            .firstOrNull;
        if (replacement == null ||
            replacement.number != lesson.number ||
            replacement.kind != lesson.kind ||
            replacement.extraPrice != lesson.extraPrice) {
          throw const CenterException(
            'حصة مستخدمة في مجموعة أو رصد لا يمكن حذفها أو تغيير رقمها وحسابها؛ يمكنك تعديل اسمها.',
          );
        }
      }
      _replace(_state.studyMonths, saved.id, saved, (m) => m.id);
      _syncStudyMonthPlans(
        _state,
        saved,
        overwrite: true,
        preservePrices: old != null && old.price == saved.price,
      );
    });
    return saved;
  }

  Future<LessonSession> _startPreparedLesson({
    required String groupId,
    required String preparedLessonId,
  }) async {
    late LessonSession started;
    var changed = false;
    await _change('prepared_lesson_start', 'بدء حصة الشهر للمجموعة', () async {
      final cairo = isCairoGroup(groupId);
      _require(cairo ? canAssess : canCollect);
      _group(groupId);
      final month = studyMonthForLesson(preparedLessonId);
      if (month == null) {
        throw const CenterException(
          'الحصة غير موجودة في الشهر؛ حدّث الاختيار.',
        );
      }
      final lesson = month.lessons.firstWhere((l) => l.id == preparedLessonId);
      final existing =
          sessionForPreparedLesson(groupId, preparedLessonId) ??
          _state.sessions
              .where(
                (s) =>
                    s.groupId == groupId &&
                    s.monthNumber == month.number &&
                    s.number == lesson.number,
              )
              .firstOrNull;
      if (existing != null && existing.status != SessionStatus.open) {
        throw const CenterException(
          'هذه الحصة مغلقة أو ملغاة؛ افتح سجلها للمراجعة أو إعادة الفتح.',
        );
      }
      if (existing != null &&
          existing.preparedLessonId == preparedLessonId &&
          sessionHasStarted(existing.id)) {
        started = existing;
        return;
      }
      final now = DateTime.now();
      started =
          existing?.copyWith(
            preparedLessonId: preparedLessonId,
            startedAt: existing.startedAt ?? now,
            startedBy: existing.startedBy ?? currentUser!.id,
          ) ??
          LessonSession(
            id: CenterStore._uuid.v4(),
            preparedLessonId: preparedLessonId,
            name: lesson.name,
            groupId: groupId,
            number: lesson.number,
            monthNumber: month.number,
            startsAt: now,
            createdAt: now,
            kind: cairo ? SessionKind.free : lesson.kind,
            extraPrice: cairo ? 0 : lesson.extraPrice,
            startedAt: now,
            startedBy: currentUser!.id,
          );
      _replace(_state.sessions, started.id, started, (s) => s.id);
      changed = true;
    }, commitWhen: () => changed);
    return started;
  }

  bool _syncStudyMonthPlans(
    CenterState state,
    StudyMonth month, {
    bool overwrite = false,
    bool preservePrices = false,
    bool inheritLegacyPrices = false,
  }) {
    var changed = false;
    state.groups = state.groups.map((group) {
      final plans = group.effectiveMonthPlans;
      final existing = plans.where((p) => p.id == month.id).firstOrNull;
      if (existing != null && !overwrite) return group;
      final legacyPrice = inheritLegacyPrices
          ? plans
                .where(
                  (plan) =>
                      plan.name.trim().toLowerCase() ==
                          month.name.trim().toLowerCase() ||
                      _legacyStudyMonthNumber(plan.name) == month.number,
                )
                .firstOrNull
                ?.price
          : null;
      final plan = GroupMonthPlan(
        id: month.id,
        name: month.name,
        sessions: month.lessons.length,
        price: preservePrices && existing != null
            ? existing.price
            : (legacyPrice ?? month.price),
      );
      if (existing == plan) return group;
      changed = true;
      return group.copyWith(
        monthPlans: [
          for (final old in plans)
            if (old.id != month.id) old,
          plan,
        ],
      );
    }).toList();
    return changed;
  }

  /// Links historical instances without changing attendance, payment or dates.
  bool _ensureStudyMonths(CenterState state) {
    var changed = false;
    final initialMigration = state.studyMonths.isEmpty;
    final numbers = state.sessions.map((s) => s.monthNumber).toSet();
    final legacyPlans = initialMigration
        ? state.groups
              .expand((group) => group.effectiveMonthPlans)
              .where((plan) => plan.name.trim() != 'شهر')
              .toList()
        : <GroupMonthPlan>[];
    for (final plan in legacyPlans) {
      final number = _legacyStudyMonthNumber(plan.name);
      if (number != null) numbers.add(number);
    }
    if (numbers.isEmpty && state.studyMonths.isEmpty) numbers.add(1);
    for (final number in numbers.toList()..sort()) {
      var month = state.studyMonths
          .where((m) => m.number == number)
          .firstOrNull;
      final existingSessions = state.sessions
          .where((s) => s.monthNumber == number)
          .toList();
      if (month == null) {
        final matchingPlans = legacyPlans
            .where((plan) => _legacyStudyMonthNumber(plan.name) == number)
            .toList();
        final count = matchingPlans.fold<int>(
          matchingPlans.isEmpty ? 4 : 0,
          (n, plan) => max(n, min(plan.sessions, 500)),
        );
        final lessonNumbers = {
          for (var n = 1; n <= count; n++) n,
          ...existingSessions.map((s) => s.number),
        }.toList()..sort();
        month = StudyMonth(
          id: CenterStore._uuid.v4(),
          name:
              legacyPlans
                  .where((plan) => _legacyStudyMonthNumber(plan.name) == number)
                  .firstOrNull
                  ?.name ??
              'الشهر $number',
          number: number,
          price: matchingPlans.firstOrNull?.price ?? 21000,
          lessons: lessonNumbers.map((n) {
            final source = existingSessions
                .where((session) => session.number == n)
                .firstOrNull;
            return PreparedLesson(
              id: CenterStore._uuid.v4(),
              number: n,
              name: source?.name ?? '',
              // Historical group-specific pricing stays on the actual session.
              kind: SessionKind.counted,
              extraPrice: 0,
            );
          }).toList(),
        );
        state.studyMonths.add(month);
        changed = true;
      }
      final missing = existingSessions
          .map((s) => s.number)
          .toSet()
          .difference(month.lessons.map((l) => l.number).toSet());
      if (missing.isNotEmpty) {
        month = month.copyWith(
          lessons: [
            ...month.lessons,
            for (final n in missing)
              PreparedLesson(id: CenterStore._uuid.v4(), number: n),
          ],
        );
        _replace(state.studyMonths, month.id, month, (m) => m.id);
        changed = true;
      }
      final lessons = {for (final l in month.lessons) l.number: l.id};
      state.sessions = state.sessions.map((s) {
        if (s.monthNumber != number || s.preparedLessonId != null) return s;
        changed = true;
        return s.copyWith(preparedLessonId: lessons[s.number]);
      }).toList();
    }
    // Preserve explicitly named payment months as templates without inventing attendance.
    for (final plan in legacyPlans) {
      if (state.studyMonths.any(
        (month) =>
            month.name.trim().toLowerCase() == plan.name.trim().toLowerCase() ||
            _legacyStudyMonthNumber(plan.name) == month.number,
      )) {
        continue;
      }
      if (plan.sessions > 500) {
        continue; // Historical pricing remains stored, never truncate it.
      }
      final number =
          state.studyMonths.fold<int>(0, (n, month) => max(n, month.number)) +
          1;
      state.studyMonths.add(
        StudyMonth(
          id: CenterStore._uuid.v4(),
          name: plan.name.trim(),
          number: number,
          price: plan.price,
          lessons: List.generate(
            plan.sessions,
            (index) =>
                PreparedLesson(id: CenterStore._uuid.v4(), number: index + 1),
          ),
        ),
      );
      changed = true;
    }
    for (final month in state.studyMonths) {
      if (_syncStudyMonthPlans(
        state,
        month,
        inheritLegacyPrices: initialMigration,
      )) {
        changed = true;
      }
    }
    return changed;
  }

  int? _legacyStudyMonthNumber(String name) {
    var normalized = name.trim().toLowerCase();
    const arabicDigits = '٠١٢٣٤٥٦٧٨٩';
    for (var digit = 0; digit < 10; digit++) {
      normalized = normalized.replaceAll(arabicDigits[digit], '$digit');
    }
    if (normalized == 'الشهر الأول' ||
        normalized == 'الشهر الاول' ||
        normalized == 'شهر الأول' ||
        normalized == 'شهر الاول') {
      return 1;
    }
    if (normalized == 'الشهر الثاني' ||
        normalized == 'شهر الثاني' ||
        normalized == 'الشهر التاني' ||
        normalized == 'شهر التاني') {
      return 2;
    }
    final numeric = RegExp(r'^(?:الشهر|شهر)\s+(\d+)$').firstMatch(normalized);
    final number = int.tryParse(numeric?.group(1) ?? '');
    return number != null && number > 0 ? number : null;
  }

  Future<void> _migrateStudyMonths() => _exclusive(() async {
    final migrated = _state.copyForMutation();
    if (!_ensureStudyMonths(migrated)) return;
    validateState(migrated);
    await _saveAutomaticBackup(_state.copyForBackup());
    await _database!.transaction((transaction) async {
      final updated = await replaceRecordState(
        transaction,
        _stateEncoder.encodeStorageFields(migrated),
      );
      if (updated != 1) {
        throw const CenterException(
          'تعذر حفظ تنظيم الشهور؛ البيانات السابقة محفوظة.',
        );
      }
    });
    _state = migrated;
  }, operation: 'database.open');
}
