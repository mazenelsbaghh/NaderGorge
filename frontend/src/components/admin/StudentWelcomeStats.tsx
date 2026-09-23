'use client';

import { useCallback, useEffect, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import apiClient from '@/services/api-client';

type WelcomeStats = {
  totalStudents: number;
  completed: number;
  pending: number;
  completedToday: number;
  completionPercent: number;
};
const number = new Intl.NumberFormat('ar-EG', { maximumFractionDigits: 1 });

export function StudentWelcomeStats() {
  const [stats, setStats] = useState<WelcomeStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const load = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setFailed(false);
    try {
      const { data } = await apiClient.get<{ success: boolean; data: WelcomeStats }>(
        '/admin/settings/student-welcome-stats', { signal, suppressErrorToast: true },
      );
      if (!signal?.aborted) {
        if (data.success) setStats(data.data);
        else setFailed(true);
      }
    } catch {
      if (!signal?.aborted) setFailed(true);
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  return (
    <section dir="rtl" aria-labelledby="welcome-stats-title" className="space-y-6 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-7">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 id="welcome-stats-title" className="text-xl font-bold text-[var(--admin-text)]">ترحيب البداية مع ميم</h2>
          <p className="mt-2 max-w-2xl text-sm leading-7 text-[var(--admin-muted)]">متابعة إكمال الترحيب الأول لكل الطلاب، الجدد والقدامى. الإكمال يُسجّل بعد انتهاء الترحيب، وليس بمجرد ظهوره.</p>
        </div>
        <button type="button" onClick={() => void load()} disabled={loading} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-medium text-[var(--admin-text)] disabled:opacity-50">
          <RefreshCw size={16} aria-hidden="true" />{loading ? 'جارٍ التحديث…' : 'تحديث الأرقام'}
        </button>
      </div>
      {failed && <p role="alert" className="text-sm text-[var(--admin-text)]">تعذّر تحديث الإحصائيات. جرّب التحديث مرة أخرى.{stats ? ' الأرقام الظاهرة من آخر تحميل ناجح.' : ''}</p>}
      {!stats && loading && <p role="status" className="text-sm text-[var(--admin-muted)]">جارٍ تحميل إحصائيات الترحيب…</p>}
      {stats && <>
        <dl className="grid grid-cols-2 gap-4 lg:grid-cols-4" aria-busy={loading}>
          {[
            ['إجمالي الطلاب', stats.totalStudents],
            ['أكملوا ترحيب البداية', stats.completed],
            ['لم يكملوا الترحيب بعد', stats.pending],
            ['أكملوه اليوم', stats.completedToday],
          ].map(([label, count]) => <div key={label} className="rounded-xl bg-[var(--admin-card-soft)] p-4">
            <dt className="text-sm leading-6 text-[var(--admin-muted)]">{label}</dt>
            <dd className="mt-3 text-3xl font-bold tabular-nums text-[var(--admin-text)]">{number.format(Number(count))}</dd>
          </div>)}
        </dl>
        <div>
          <div className="mb-3 flex items-center justify-between text-sm text-[var(--admin-text)]"><span>نسبة إكمال ترحيب البداية</span><strong>{number.format(stats.completionPercent)}٪</strong></div>
          <progress aria-label="نسبة إكمال ترحيب البداية" max={100} value={stats.completionPercent} className="h-2 w-full overflow-hidden rounded-full accent-[var(--admin-primary)]" />
        </div>
        <p className="text-xs leading-6 text-[var(--admin-muted)]">تشمل «لم يكملوا» من لم يظهر لهم الترحيب ومن أغلقوه قبل النهاية. الأرقام تشمل الحسابات الموقوفة وتستبعد المحذوفة. «اليوم» بتوقيت مصر؛ ترحيب الرجوع اليومي لا يزيد عدد إكمال ترحيب البداية.</p>
      </>}
    </section>
  );
}
