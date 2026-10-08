part of 'center_store.dart';

class _MonthPurchase {
  const _MonthPurchase(
    this.sessions,
    this.baseAmount,
    this.description,
    this.plan,
  );
  final int sessions, baseAmount;
  final String description;
  final GroupMonthPlan? plan;
}

extension CenterStoreMonths on CenterStore {
  Future<void> _updateMonthForGroups({
    required GroupMonthPlan plan,
    required Map<String, GroupMonthPlan> expectedPlans,
  }) {
    final reviewed = Map<String, GroupMonthPlan>.unmodifiable(expectedPlans);
    return _change(
      'month_update_groups',
      'تعديل شهر التحصيل للمجموعات',
      () async {
        _require(canManage);
        final name = plan.name.trim();
        if (reviewed.isEmpty ||
            name.isEmpty ||
            name.length > 120 ||
            plan.sessions <= 0 ||
            plan.price < 0) {
          throw const CenterException(
            'اختر مجموعات واكتب اسم الشهر وعدد حصصه وسعره الصحيح.',
          );
        }
        for (final entry in reviewed.entries) {
          final group = _group(entry.key);
          final current = group.effectiveMonthPlans
              .where((month) => month.id == entry.value.id)
              .firstOrNull;
          if (current != entry.value) {
            throw const CenterException(
              'تغيّرت بيانات الشهر؛ افتح إدارة الشهور من جديد قبل الحفظ.',
            );
          }
          if (group.effectiveMonthPlans.any(
            (month) =>
                month.id != current!.id &&
                month.name.trim().toLowerCase() == name.toLowerCase(),
          )) {
            throw CenterException(
              'الشهر "$name" موجود بالفعل في مجموعة ${group.name}.',
            );
          }
          _replace(
            _state.groups,
            group.id,
            group.copyWith(
              monthPlans: [
                for (final month in group.effectiveMonthPlans)
                  month.id == current!.id
                      ? plan.copyWith(id: month.id, name: name)
                      : month,
              ],
            ),
            (group) => group.id,
          );
        }
      },
    );
  }

  void _addMonthForGroups(GroupMonthPlan plan, List<String> groupIds) {
    final name = plan.name.trim();
    if (name.isEmpty ||
        name.length > 120 ||
        plan.sessions <= 0 ||
        plan.price < 0) {
      throw const CenterException(
        'أدخل اسم شهر حتى ١٢٠ حرفًا وعدد حصص موجبًا وسعرًا صحيحًا.',
      );
    }
    if (groupIds.isEmpty || groupIds.toSet().length != groupIds.length) {
      throw const CenterException('اختر مجموعات مختلفة لإضافة الشهر إليها.');
    }
    final selected = groupIds.map(_group).toList();
    for (final group in selected) {
      if (group.effectiveMonthPlans.any(
        (e) => e.name.trim().toLowerCase() == name.toLowerCase(),
      )) {
        throw CenterException(
          'الشهر "$name" موجود بالفعل في مجموعة ${group.name}؛ لم يُحفظ أي تغيير.',
        );
      }
    }
    for (final group in selected) {
      final added = plan.copyWith(id: CenterStore._uuid.v4(), name: name);
      _replace(
        _state.groups,
        group.id,
        group.copyWith(monthPlans: [...group.effectiveMonthPlans, added]),
        (e) => e.id,
      );
    }
  }

  void _requirePackageChoice(int sessions, String? monthPlanId) {
    if (monthPlanId == null) _requirePackageSessions(sessions);
    if (monthPlanId != null && monthPlanId.trim().isEmpty) {
      throw const CenterException('اختر شهرًا صالحًا من المجموعة.');
    }
  }

  GroupMonthPlan _monthPlanFor(StudyGroup group, String monthPlanId) =>
      _find<GroupMonthPlan>(
        group.effectiveMonthPlans,
        (e) => e.id == monthPlanId,
        'الشهر المختار لم يعد موجودًا في المجموعة؛ اختر شهرًا آخر.',
      );

  _MonthPurchase _monthPurchaseFor(
    StudyGroup group,
    int sessions,
    String? monthPlanId,
  ) {
    if (monthPlanId == null) {
      return _MonthPurchase(
        sessions,
        _configuredPackageAmount(group, sessions),
        _packageLabel(sessions),
        null,
      );
    }
    final plan = _monthPlanFor(group, monthPlanId);
    return _MonthPurchase(
      plan.sessions,
      plan.price,
      '${plan.name} — ${plan.sessions} حصص',
      plan,
    );
  }

  List<GroupMonthPlan> _savedMonthPlans(StudyGroup group) {
    final existing = group.id.isEmpty ? null : _group(group.id);
    final plans = group.monthPlans.isNotEmpty
        ? group.monthPlans
        : existing?.effectiveMonthPlans ?? group.effectiveMonthPlans;
    final saved = plans
        .map(
          (plan) => plan.copyWith(
            id: plan.id.isEmpty ? CenterStore._uuid.v4() : plan.id,
            name: plan.name.trim(),
          ),
        )
        .toList();
    for (final month in _state.studyMonths) {
      final index = saved.indexWhere((plan) => plan.id == month.id);
      final plan = GroupMonthPlan(
        id: month.id,
        name: month.name,
        sessions: month.lessons.length,
        price: index < 0 ? month.price : saved[index].price,
      );
      if (index < 0) {
        saved.add(plan);
      } else {
        saved[index] = plan;
      }
    }
    return List.unmodifiable(saved);
  }

  bool _applyDefaultMonthPlans(CenterState state) {
    final migratePrice = state.defaultMonthPriceVersion < 1;
    var changed = migratePrice;
    state.groups = state.groups.map((group) {
      final plans = group.effectiveMonthPlans
          .map(
            (plan) =>
                migratePrice &&
                    plan.id == 'legacy-month4' &&
                    plan.name.trim() == 'شهر'
                ? plan.copyWith(sessions: 4, price: 21000)
                : plan,
          )
          .toList();
      final packagePrice = migratePrice ? 21000 : group.packagePrice;
      if (group.packagePrice == packagePrice &&
          listEquals(group.monthPlans, plans)) {
        return group;
      }
      changed = true;
      return group.copyWith(monthPlans: plans, packagePrice: packagePrice);
    }).toList();
    state.defaultMonthPriceVersion = 1;
    return changed;
  }

  Future<void> _migrateMonthPlans() => _exclusive(() async {
    final migrated = CenterState.fromJson(_state.toJson());
    if (!_applyDefaultMonthPlans(migrated)) return;
    validateState(migrated);
    // Preserve the old logical database before applying forward-only defaults.
    if (_state.staff.isNotEmpty && _lastAutomaticBackupAt == null) {
      await _saveAutomaticBackup(_state.copyForBackup());
    }
    await _database!.transaction((transaction) async {
      final updated = await replaceRecordState(
        transaction,
        _stateEncoder.encodeStorageFields(migrated),
      );
      if (updated != 1) {
        throw const CenterException(
          'لم تُحفظ إعدادات الأشهر؛ بياناتك القديمة محفوظة.',
        );
      }
    });
    _state = migrated;
  }, operation: 'database.open');
}
