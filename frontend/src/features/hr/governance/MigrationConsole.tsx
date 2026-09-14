'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CheckCircle2, Loader2, RotateCcw, ShieldCheck } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  hrGovernanceService,
  MigrationBatchDto,
  MigrationRowInput,
  RolloutDto,
} from '@/services/hr-governance-service';
import { HrStatusBadge } from '@/features/hr/components/HrStatusBadge';

const modules = [
  { value: 'people', label: 'الموظفون والهيكل' },
  { value: 'attendance', label: 'الحضور' },
  { value: 'leave', label: 'الإجازات' },
  { value: 'payroll', label: 'الرواتب' },
  { value: 'remaining', label: 'الوحدات المتبقية' },
];

const rolloutLabels: Record<string, string> = {
  Legacy: 'النظام القديم',
  NewActive: 'النظام الجديد نشط',
  RolledBack: 'تم الرجوع',
};

const targetLabels: Record<string, string> = {
  legacy: 'القديم',
  new: 'الجديد',
};

function moduleLabel(moduleKey: string) {
  return modules.find((moduleOption) => moduleOption.value === moduleKey)?.label ?? moduleKey;
}

function rolloutLabel(rolloutState?: string | null) {
  if (!rolloutState) return rolloutLabels.Legacy;
  return rolloutLabels[rolloutState] ?? rolloutState;
}

export function MigrationConsole() {
  const [module, setModule] = useState('people');
  const [json, setJson] = useState(
    '[{"sourceType":"employee","sourceId":"1","targetId":"00000000-0000-0000-0000-000000000001","amount":1,"sourceHash":"sha256-a"}]'
  );
  const [batches, setBatches] = useState<MigrationBatchDto[]>([]);
  const [rollouts, setRollouts] = useState<RolloutDto[]>([]);
  const [conflicts, setConflicts] = useState<unknown[]>([]);
  const [loading, setLoading] = useState(true);

  const rows = useMemo(() => {
    try {
      return JSON.parse(json) as MigrationRowInput[];
    } catch {
      return [];
    }
  }, [json]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const migrationStatus = await hrGovernanceService.status();
      setBatches(migrationStatus.batches);
      setRollouts(migrationStatus.rollouts);
      setConflicts(migrationStatus.conflicts);
    } catch {
      toast.error('تعذر تحميل حالة الترحيل');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  async function dryRun() {
    if (!rows.length) {
      toast.error('بيانات JSON غير صالحة');
      return;
    }
    try {
      await hrGovernanceService.dryRun(module, rows);
      toast.success('اكتملت المحاكاة دون تغيير المصدر');
      await load();
    } catch {
      toast.error('فشلت محاكاة الترحيل');
    }
  }

  async function reconcile(batch: MigrationBatchDto) {
    try {
      await hrGovernanceService.reconcile(batch.id, batch.module, rows);
      toast.success('اكتملت مطابقة البيانات');
      await load();
    } catch {
      toast.error('يوجد فرق أو تعارض يحتاج إلى معالجة');
    }
  }

  async function activate(batch: MigrationBatchDto) {
    try {
      await hrGovernanceService.activate(batch.id, batch.module, 'اعتماد بعد التطابق');
      toast.success('تم تفعيل الوحدة على النظام الجديد');
      await load();
    } catch {
      toast.error('التفعيل متوقف بسبب اعتماد سابق أو تعارض مفتوح');
    }
  }

  async function rollback(rollout: RolloutDto) {
    try {
      await hrGovernanceService.rollback(rollout.module, 'رجوع تشغيلي مستقل');
      toast.success('عادت الوحدة إلى النظام القديم');
      await load();
    } catch {
      toast.error('تعذر الرجوع إلى النظام القديم');
    }
  }

  if (loading) {
    return (
      <div className="admin-panel py-16 text-center" role="status">
        <Loader2 className="mx-auto h-6 w-6 animate-spin text-[var(--admin-accent)]" />
        <p className="mt-3 text-sm font-bold text-[var(--admin-muted)]">جارٍ تحميل حالة الترحيل…</p>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <section className="admin-panel">
        <div className="grid gap-4 lg:grid-cols-[220px_1fr]">
          <label className="text-sm font-bold">
            الوحدة
            <select
              value={module}
              onChange={(event) => setModule(event.target.value)}
              className="admin-input mt-2"
            >
              {modules.map((moduleOption) => (
                <option value={moduleOption.value} key={moduleOption.value}>
                  {moduleOption.label}
                </option>
              ))}
            </select>
            <p className="mt-2 text-xs leading-5 text-[var(--admin-muted)]">
              الترتيب إلزامي: الهيكل ← الحضور ← الإجازات ← الرواتب ← الباقي.
            </p>
          </label>
          <label className="text-sm font-bold">
            صفوف المصدر بصيغة JSON
            <textarea
              dir="ltr"
              value={json}
              onChange={(event) => setJson(event.target.value)}
              className="admin-input mt-2 min-h-32 font-mono text-xs"
              spellCheck={false}
            />
          </label>
        </div>
        <button onClick={() => void dryRun()} className="admin-btn-primary mt-4 min-h-11">
          تشغيل محاكاة الترحيل
        </button>
      </section>

      {conflicts.length > 0 && (
        <div
          role="alert"
          className="rounded-xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 font-black text-[var(--admin-danger)]"
        >
          يوجد {conflicts.length} تعارض مفتوح؛ لا يمكن تفعيل الوحدة قبل معالجته.
        </div>
      )}

      <section aria-label="حالة الوحدات" className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        {modules.map((moduleOption) => {
          const rollout = rollouts.find((rolloutRow) => rolloutRow.module === moduleOption.value);
          return (
            <article key={moduleOption.value} className="admin-panel">
              <p className="text-xs font-black text-[var(--admin-muted)]">{moduleOption.label}</p>
              <p className="mt-2 font-black">{rolloutLabel(rollout?.state)}</p>
              <p className="mt-2 text-xs leading-5 text-[var(--admin-muted)]">
                قراءة: {targetLabels[rollout?.readTarget ?? 'legacy'] ?? rollout?.readTarget}
                {' · '}
                كتابة: {targetLabels[rollout?.writeTarget ?? 'legacy'] ?? rollout?.writeTarget}
              </p>
              {rollout?.state === 'NewActive' && (
                <button
                  onClick={() => void rollback(rollout)}
                  className="admin-btn-secondary mt-3 min-h-11"
                >
                  <RotateCcw className="h-4 w-4" aria-hidden="true" />
                  رجوع
                </button>
              )}
            </article>
          );
        })}
      </section>

      <section aria-label="دفعات الترحيل" className="space-y-3">
        {batches.length === 0 ? (
          <div className="hr-empty">لا توجد دفعات ترحيل بعد. ابدأ بمحاكاة الوحدة المحددة.</div>
        ) : batches.map((batch) => (
          <article key={batch.id} className="admin-panel">
            <div className="flex flex-wrap justify-between gap-3">
              <div>
                <div className="flex flex-wrap items-center gap-2">
                  <p className="font-black">{moduleLabel(batch.module)}</p>
                  <HrStatusBadge status={batch.state} />
                </div>
                <p className="mt-2 text-sm text-[var(--admin-muted)]">
                  المصدر {batch.sourceCount} / {batch.sourceTotal} — الهدف {batch.targetCount} / {batch.targetTotal}
                </p>
                <p className="mt-2 break-all font-mono text-xs text-[var(--admin-muted)]" dir="ltr">
                  {batch.sourceHash} → {batch.targetHash ?? 'لم يُحسب'}
                </p>
              </div>
              <CheckCircle2
                className={
                  batch.state === 'Reconciled' || batch.state === 'Activated'
                    ? 'text-[var(--admin-success)]'
                    : 'text-[var(--admin-muted)]'
                }
                aria-hidden="true"
              />
            </div>
            <div className="mt-4 flex flex-wrap gap-2">
              {(batch.state === 'DryRun' || batch.state === 'Failed') && (
                <button
                  onClick={() => void reconcile(batch)}
                  className="admin-btn-secondary min-h-11"
                >
                  مطابقة البيانات
                </button>
              )}
              {batch.state === 'Reconciled' && (
                <button
                  onClick={() => void activate(batch)}
                  className="admin-btn-primary min-h-11"
                >
                  <ShieldCheck className="h-4 w-4" aria-hidden="true" />
                  تفعيل الوحدة
                </button>
              )}
            </div>
          </article>
        ))}
      </section>
    </div>
  );
}
