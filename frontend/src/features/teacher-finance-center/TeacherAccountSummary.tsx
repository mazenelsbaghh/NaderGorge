'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { financeService } from '@/services/finance-service';
import type { TeacherFinanceSummary } from './types';
import { useAuthStore } from '@/stores/auth-store';
import { isFullAdmin } from '@/packages/admin/route-permissions';

import { TeacherAccountOverview } from './TeacherAccountOverview';
export { teacherMoney } from './TeacherAccountOverview';

export function TeacherAccountSummary({ teacherId }: { teacherId: string }) {
  const canReadAccount = useAuthStore((state) => isFullAdmin(state.user));
  const [summary, setSummary] = useState<TeacherFinanceSummary | null>(null);
  const [error, setError] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!canReadAccount) return;
    let active = true;
    setSummary(null);
    setError(false);
    void financeService.getTeacherFinanceSummary(teacherId).then((account) => {
      if (!active) return;
      if (account) setSummary(account);
      else setError(true);
    }).catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [teacherId, attempt, canReadAccount]);

  if (!canReadAccount) return null;

  return <section className="admin-panel space-y-4 rounded-2xl p-6" aria-label="ملخص حساب المدرس">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h2 className="text-lg font-black">حساب المدرس</h2>
      <Link href={`/admin/teachers/${teacherId}/account`} className="inline-flex min-h-11 items-center rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold">فتح حساب المدرّس</Link>
    </div>
    {error ? <div role="alert">تعذر تحميل الحساب. <button type="button" className="min-h-11 px-3 underline" onClick={() => setAttempt((value) => value + 1)}>إعادة المحاولة</button></div>
      : !summary || summary.teacherId !== teacherId ? <p role="status">جارٍ تحميل الحساب...</p>
        : <TeacherAccountOverview account={summary} />}
  </section>;
}
