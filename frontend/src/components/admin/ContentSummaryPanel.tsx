'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { CalendarDays, Gift, PackageCheck, RefreshCw, ShoppingBag, UsersRound } from 'lucide-react';
import { contentService, type ContentPackageSummaryDto, type ContentSummaryDto } from '@/services/content-service';
import { cairoCurrentDate, cairoCurrentMonthPeriod, cairoDateAfterDays, cairoDateTimeLocalToUtcISOString } from '@/lib/cairo-time';

type Period = 'all' | 'today' | 'month' | 'custom';

const number = new Intl.NumberFormat('ar-EG');

function buildRange(period: Period, from: string, to: string) {
  if (period === 'all') return {};
  if (period === 'today') return { fromUtc: cairoDateTimeLocalToUtcISOString(`${cairoCurrentDate()}T00:00`) };
  if (period === 'month') return { fromUtc: cairoDateTimeLocalToUtcISOString(`${cairoCurrentMonthPeriod().first}T00:00`) };
  if (!from && !to) return {};

  const endDate = to ? cairoDateAfterDays(1, new Date(`${to}T00:00:00Z`)) : undefined;
  return {
    fromUtc: from ? cairoDateTimeLocalToUtcISOString(`${from}T00:00`) : undefined,
    toUtc: endDate ? cairoDateTimeLocalToUtcISOString(`${endDate}T00:00`) : undefined,
  };
}

function AcquisitionRow({ label, values }: { label: string; values: ContentPackageSummaryDto['package'] }) {
  return (
    <div className="grid grid-cols-[1fr_auto_auto] items-center gap-3 border-b border-[var(--admin-border)]/60 py-3 last:border-b-0">
      <span className="text-sm font-bold text-[var(--admin-text)]">{label}</span>
      <span className="min-w-20 text-center text-sm text-[var(--admin-muted)]">
        <strong className="font-black text-[var(--admin-text)]">{number.format(values.purchased)}</strong> مشتري
      </span>
      <span className="min-w-20 text-center text-sm text-[var(--admin-muted)]">
        <strong className="font-black text-[var(--admin-primary)]">{number.format(values.gifts)}</strong> هدية
      </span>
    </div>
  );
}

function PackageSummary({ packageSummary }: { packageSummary: ContentPackageSummaryDto }) {
  return (
    <article className="overflow-hidden rounded-2xl bg-[var(--admin-card)] shadow-[0_2px_8px_rgba(10,29,61,0.08)]">
      <header className="flex flex-wrap items-start justify-between gap-4 bg-[var(--admin-primary-15)] px-5 py-4">
        <div>
          <h3 className="text-lg font-black text-[var(--admin-text)]">{packageSummary.packageName}</h3>
          {packageSummary.teacherName && <p className="mt-1 text-xs font-medium text-[var(--admin-muted)]">{packageSummary.teacherName}</p>}
        </div>
        <div className="flex items-center gap-2 rounded-full bg-[var(--admin-card)] px-3 py-2 text-sm font-black text-[var(--admin-primary)]">
          <UsersRound className="h-4 w-4" aria-hidden="true" />
          {number.format(packageSummary.totalStudents)} طالب
        </div>
      </header>

      <div className="px-5">
        <AcquisitionRow label="الباقة كاملة" values={packageSummary.package} />
        <AcquisitionRow label="الترم" values={packageSummary.term} />
        <AcquisitionRow label="الكورس / القسم" values={packageSummary.section} />
        <AcquisitionRow label="الحصة" values={packageSummary.lesson} />
      </div>

      <footer className="grid grid-cols-3 divide-x divide-x-reverse divide-[var(--admin-border)] border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-center">
        <div className="px-2 py-3"><ShoppingBag className="mx-auto mb-1 h-4 w-4 text-[var(--admin-secondary)]" /><b>{number.format(packageSummary.purchasedStudents)}</b><span className="block text-xs text-[var(--admin-muted)]">مشتري</span></div>
        <div className="px-2 py-3"><Gift className="mx-auto mb-1 h-4 w-4 text-[var(--admin-primary)]" /><b>{number.format(packageSummary.giftStudents)}</b><span className="block text-xs text-[var(--admin-muted)]">هدية فقط</span></div>
        <div className="px-2 py-3"><UsersRound className="mx-auto mb-1 h-4 w-4 text-[var(--admin-text)]" /><b>{number.format(packageSummary.totalStudents)}</b><span className="block text-xs text-[var(--admin-muted)]">الإجمالي</span></div>
      </footer>
    </article>
  );
}

export function ContentSummaryPanel({ scope }: { scope: 'admin' | 'teacher' }) {
  const [period, setPeriod] = useState<Period>('all');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [summary, setSummary] = useState<ContentSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const range = useMemo(() => buildRange(period, from, to), [period, from, to]);

  const load = useCallback(async () => {
    if (period === 'custom' && from && to && from > to) {
      setError('تاريخ البداية يجب أن يسبق تاريخ النهاية.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      const response = await contentService.getContentSummary(scope, range.fromUtc, range.toUtc);
      setSummary(response.data.data ?? null);
    } catch {
      setError('تعذر تحميل ملخص المحتوى. حاول مرة أخرى.');
    } finally {
      setLoading(false);
    }
  }, [from, period, range.fromUtc, range.toUtc, scope, to]);

  useEffect(() => { void load(); }, [load]);

  return (
    <section className="space-y-6" aria-label="ملخص اقتناء المحتوى">
      <div className="flex flex-col gap-4 rounded-2xl bg-[var(--admin-card-soft)] p-4 md:flex-row md:items-center md:justify-between">
        <div className="flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
          <CalendarDays className="h-5 w-5 text-[var(--admin-secondary)]" aria-hidden="true" />
          فترة الملخص
        </div>
        <div className="flex flex-wrap gap-2" role="group" aria-label="اختيار فترة الملخص">
          {([['all', 'كل الوقت'], ['today', 'اليوم'], ['month', 'هذا الشهر'], ['custom', 'فترة مخصصة']] as const).map(([value, label]) => (
            <button key={value} type="button" onClick={() => setPeriod(value)} aria-pressed={period === value} className={`min-h-11 rounded-full px-4 text-sm font-bold transition-colors ${period === value ? 'bg-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}>
              {label}
            </button>
          ))}
        </div>
      </div>

      {period === 'custom' && (
        <div className="flex flex-col gap-3 rounded-xl bg-[var(--admin-card)] p-4 sm:flex-row sm:items-end">
          <label className="flex-1 text-sm font-bold text-[var(--admin-text)]">من<input className="admin-input mt-2" type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></label>
          <label className="flex-1 text-sm font-bold text-[var(--admin-text)]">إلى<input className="admin-input mt-2" type="date" value={to} onChange={(event) => setTo(event.target.value)} /></label>
        </div>
      )}

      {loading ? (
        <div className="grid gap-5 lg:grid-cols-2" aria-label="جاري تحميل الملخص">
          {[0, 1].map((skeletonIndex) => <div key={skeletonIndex} className="h-80 animate-pulse rounded-2xl bg-[var(--admin-card-soft)]" />)}
        </div>
      ) : error ? (
        <div className="flex flex-col items-center gap-3 rounded-2xl bg-[var(--admin-card)] px-6 py-10 text-center">
          <p className="font-bold text-red-700">{error}</p>
          <button type="button" onClick={() => void load()} className="admin-btn-ghost inline-flex min-h-11 items-center gap-2"><RefreshCw className="h-4 w-4" />إعادة المحاولة</button>
        </div>
      ) : !summary?.packages.length ? (
        <div className="rounded-2xl bg-[var(--admin-card)] px-6 py-12 text-center">
          <PackageCheck className="mx-auto mb-3 h-8 w-8 text-[var(--admin-secondary)]" />
          <p className="font-black text-[var(--admin-text)]">لا توجد باقات في هذا النطاق.</p>
        </div>
      ) : (
        <>
          <div className="grid gap-5 lg:grid-cols-2">
            {summary.packages.map((packageSummary) => <PackageSummary key={packageSummary.packageId} packageSummary={packageSummary} />)}
          </div>

          <section className="rounded-2xl bg-[var(--admin-card)] p-5 shadow-[0_2px_8px_rgba(10,29,61,0.08)]">
            <h2 className="text-lg font-black text-[var(--admin-text)]">الباقات التي اشتراها الطلاب معًا</h2>
            {summary.packageCombinations.length ? (
              <div className="mt-4 divide-y divide-[var(--admin-border)]">
                {summary.packageCombinations.map((combination) => (
                  <div key={combination.packageIds.join('-')} className="flex flex-col gap-2 py-4 sm:flex-row sm:items-center sm:justify-between">
                    <p className="font-bold text-[var(--admin-text)]">{combination.packageNames.join(' + ')}</p>
                    <span className="w-fit rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-sm font-black text-[var(--admin-primary)]">{number.format(combination.studentsCount)} طالب</span>
                  </div>
                ))}
              </div>
            ) : <p className="mt-3 text-sm text-[var(--admin-muted)]">لا يوجد طلاب اشتروا أكثر من باقة خلال الفترة المختارة.</p>}
          </section>
        </>
      )}
    </section>
  );
}
