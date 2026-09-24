'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useAuthStore } from '@/stores/auth-store';
import { isFullAdmin } from '@/packages/admin/route-permissions';
import { TeacherAccountOverview } from '@/features/teacher-finance-center/TeacherAccountOverview';
import { useEffect, useState } from 'react';
import platformFinanceService, { FinanceTeacherSummary } from '@/services/platform-finance-service';

export default function TeacherFinancialSummary({ teacherId }: { teacherId?: string }) {
  const router = useRouter();
  const fullAdmin = useAuthStore(state => isFullAdmin(state.user));
  const [rows, setRows] = useState<FinanceTeacherSummary[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    if (fullAdmin && teacherId) { router.replace(`/admin/teachers/${teacherId}/account`); return; }
    let active = true;
    setLoading(true);
    setRows([]);
    setError('');
    void (teacherId ? platformFinanceService.getTeacherDetail(teacherId) : platformFinanceService.getTeacherSummary())
      .then(result => { if (active) setRows(Array.isArray(result) ? result : [result]); })
      .catch(() => { if (active) setError('تعذر تحميل ملخص المدرسين'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [teacherId, fullAdmin, router]);
  return <section className="admin-panel space-y-5 rounded-2xl p-6" dir="rtl">
    <h2 className="text-lg font-black">حساب المدرّس</h2>
    {loading && <p role="status">جارٍ تحميل الحساب...</p>}
    {error && <p role="alert">{error}</p>}
    {rows.map(row => <section key={row.teacherId} className="space-y-4">
      <h3 className="font-bold">{row.teacherName}</h3>
      {row.account ? <TeacherAccountOverview account={row.account} showSources /> : <p role="alert">تفاصيل الحساب غير متاحة.</p>}
      {fullAdmin && <Link className="inline-flex min-h-11 items-center underline" href={`/admin/teachers/${row.teacherId}/account`}>فتح حساب المدرّس</Link>}
    </section>)}
  </section>;
}
