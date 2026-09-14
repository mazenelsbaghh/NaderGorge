'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { financeService } from '@/services/finance-service';
import { useAuthStore } from '@/stores/auth-store';
import { isFullAdmin } from '@/packages/admin/route-permissions';
import { formatCairoDateTime } from '@/lib/cairo-time';
import { teacherMoney } from './TeacherAccountSummary';
import type { TeacherCollection, TeacherCollections } from './types';

export function TeacherCollectionsPanel({ teacherId, details = false }: { teacherId: string; details?: boolean }) {
  const canRead = useAuthStore((state) => isFullAdmin(state.user));
  const [collections, setCollections] = useState<TeacherCollections | null>(null);
  const [error, setError] = useState(false);
  const [page, setPage] = useState(1);
  const [vodafoneOnly, setVodafoneOnly] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!canRead) return;
    let active = true;
    setCollections(null);
    setError(false);
    void financeService.getTeacherCollections(teacherId, { page, pageSize: details ? 25 : 1, vodafoneOnly })
      .then((response) => { if (active) setCollections(response); })
      .catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [teacherId, details, page, vodafoneOnly, attempt, canRead]);
  if (!canRead) return null;

  return <section id="collections" className="admin-panel space-y-4 rounded-2xl p-6" aria-label="تحويلات المحافظ الخاصة بالمدرس">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <h2 className="text-lg font-black">تحويلات المحافظ الخاصة بالمدرس</h2>
      {!details && <Link href={`/admin/teachers/${teacherId}/account#collections`} className="inline-flex min-h-11 items-center rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold">تفاصيل التحويلات</Link>}
    </div>
    <p className="text-sm text-[var(--admin-muted)]">كل الفترات · التحويلات المقبولة لشحن رصيد هذا المدرس فقط. هذه تحصيلات من الطلاب قبل احتساب عمولة المنصة أو شراء المحتوى.</p>
    {error ? <div role="alert">تعذر تحميل التحويلات. <button type="button" onClick={() => setAttempt((value) => value + 1)} className="min-h-11 px-3 underline">إعادة المحاولة</button></div>
      : !collections || collections.teacherId !== teacherId ? <p role="status">جارٍ تحميل التحويلات...</p>
        : <>
          <dl className="grid gap-4 md:grid-cols-3">
            <CollectionMetric label="إجمالي تحويلات المدرس" amount={collections.totalAmount} count={`${collections.totalCount} تحويل مؤكد`} />
            <CollectionMetric label="منها فودافون كاش الموثّق" amount={collections.vodafoneCashAmount} count={`${collections.vodafoneCashCount} تحويل موثّق برسالة`} />
            <CollectionMetric label="محافظ أخرى أو نوع غير موثّق" amount={collections.otherOrUnverifiedAmount} count={`${collections.totalCount - collections.vodafoneCashCount} تحويل`} />
          </dl>
          <p className="text-xs leading-6 text-[var(--admin-muted)]">تصنيف فودافون كاش يعتمد على جهة رسالة التحويل المحفوظة. التحويل المقبول يدويًا دون رسالة يظهر ضمن النوع غير الموثّق حتى لا يُنسب لفودافون كاش بالخطأ.</p>
          {details && <>
            <label className="flex min-h-11 items-center gap-3 text-sm font-bold"><input type="checkbox" checked={vodafoneOnly} onChange={(event) => { setVodafoneOnly(event.target.checked); setPage(1); }} />عرض تحويلات فودافون كاش الموثّقة فقط</label>
            <CollectionsTable rows={collections.items} />
            <div className="flex flex-wrap items-center justify-between gap-3 text-sm"><p>{collections.filteredCount} تحويل · صفحة {page} من {Math.max(1, Math.ceil(collections.filteredCount / collections.pageSize))}</p><div className="flex gap-3">
              <button type="button" className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 disabled:opacity-40" disabled={page === 1} onClick={() => setPage((current) => current - 1)}>السابق</button>
              <button type="button" className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 disabled:opacity-40" disabled={page * collections.pageSize >= collections.filteredCount} onClick={() => setPage((current) => current + 1)}>التالي</button>
            </div></div>
          </>}
        </>}
  </section>;
}

function CollectionMetric({ label, amount, count }: { label: string; amount: number; count: string }) {
  return <div className="rounded-xl border border-[var(--admin-border)] p-4"><dt className="text-sm text-[var(--admin-muted)]">{label}</dt><dd className="mt-2 break-words font-mono text-xl font-bold">{teacherMoney(amount)}</dd><dd className="mt-2 text-xs text-[var(--admin-muted)]">{count}</dd></div>;
}

function CollectionsTable({ rows }: { rows: TeacherCollection[] }) {
  return <div className="overflow-x-auto"><table className="w-full text-right text-sm">
    <caption className="sr-only">تفاصيل التحويلات المقبولة المرتبطة بالمدرس</caption>
    <thead><tr>{['تاريخ القبول', 'الطالب', 'المبلغ', 'المحفظة المستقبلة', 'رقم المحوّل', 'مرجع التحويل', 'التصنيف', 'القبول'].map((label) => <th scope="col" key={label} className="whitespace-nowrap p-3">{label}</th>)}</tr></thead>
    <tbody>{rows.map((row) => <tr key={row.id} className="border-t border-[var(--admin-border)]">
      <td className="whitespace-nowrap p-3">{row.resolvedAt ? formatCairoDateTime(row.resolvedAt, { dateStyle: 'medium', timeStyle: 'short' }) : 'غير مسجّل'}</td>
      <td className="p-3">{row.studentName}</td><td className="whitespace-nowrap p-3 font-mono">{teacherMoney(row.amount)}</td>
      <td className="p-3">{row.walletLabel}<span dir="ltr" className="block whitespace-nowrap font-mono text-xs text-[var(--admin-muted)]">{row.walletPhoneNumber}</span></td>
      <td className="whitespace-nowrap p-3 font-mono"><bdi>{row.senderPhoneNumber || '—'}</bdi></td>
      <td className="p-3 font-mono"><bdi>{row.transferReference || '—'}</bdi></td>
      <td className="p-3">{row.isVodafoneCash ? 'فودافون كاش' : 'أخرى / غير موثّق'}</td>
      <td className="p-3">{row.status === 'Matched' ? 'مطابقة تلقائية' : 'موافقة يدوية'}</td>
    </tr>)}{rows.length === 0 && <tr><td colSpan={8} className="p-8 text-center text-[var(--admin-muted)]">لا توجد تحويلات مطابقة.</td></tr>}</tbody>
  </table></div>;
}
