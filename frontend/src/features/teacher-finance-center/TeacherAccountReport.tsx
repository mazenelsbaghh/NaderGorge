'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { financeService } from '@/services/finance-service';
import { teacherService, type TeacherDto } from '@/services/teacher-service';
import { formatCairoDateTime } from '@/lib/cairo-time';
import { TeacherAccountOverview, teacherMoney, incomeSourceLabels } from './TeacherAccountOverview';
import { TeacherPayoutRequests } from './TeacherPayoutRequests';
import { TeacherFinanceCenterWorkspace } from './TeacherFinanceCenterWorkspace';
import { TeacherAllocationExplanation } from './TeacherAllocationExplanation';
import type { PagedTeacherLedger, TeacherFinanceSummary } from './types';
import { TeacherCollectionsPanel } from './TeacherCollectionsPanel';

const statuses: Record<string, string> = { Unpaid: 'غير مدفوع', Reserved: 'محجوز', Paid: 'مدفوع', Reversed: 'معكوس', Debt: 'مديونية' };

export function TeacherAccountReport({ teacherId }: { teacherId: string }) {
  const [teacher, setTeacher] = useState<TeacherDto | null>(null);
  const teacherName = teacher?.fullName ?? '';
  const [summary, setSummary] = useState<TeacherFinanceSummary | null>(null);
  const [ledger, setLedger] = useState<PagedTeacherLedger | null>(null);
  const [error, setError] = useState(false);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let active = true;
    setLedger(null);
    setSummary(null);
    setError(false);
    void Promise.all([
      teacherService.getTeacherById(teacherId),
      financeService.getTeacherFinanceSummary(teacherId),
      financeService.getTeacherLedger(teacherId, { page, pageSize: 25, status: status || undefined }),
    ]).then(([teacher, account, statement]) => {
      if (!active) return;
      if (!teacher.success || !teacher.data || !account) { setError(true); return; }
      setTeacher(teacher.data);
      setSummary(account);
      setLedger(statement);
    }).catch(() => { if (active) setError(true); });
    return () => { active = false; };
  }, [teacherId, page, status, attempt]);

  return <div className="space-y-6">
    <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-xl font-black">{teacherName || 'حساب المدرس'}</h2><Link className="inline-flex min-h-11 items-center underline" href={`/admin/teachers/${teacherId}`}>العودة إلى بروفايل المدرس</Link></div>
    <section className="admin-panel space-y-4 rounded-2xl p-6" aria-label="ملخص حساب المدرس">
      {summary ? <TeacherAccountOverview account={summary} showSources /> : <p role={error ? 'alert' : 'status'}>{error ? 'تعذر تحميل حساب المدرّس.' : 'جارٍ تحميل الحساب...'}{error && <button type="button" className="min-h-11 px-3 underline" onClick={() => setAttempt(value => value + 1)}>إعادة المحاولة</button>}</p>}
    </section>
    <details className="rounded-xl border border-[var(--admin-border)] p-4"><summary className="min-h-9 cursor-pointer font-bold">طلبات السحب والمدفوعات</summary><TeacherPayoutRequests teacherId={teacherId} onChanged={() => setAttempt(value => value + 1)} /></details>
    {teacher && <TeacherFinanceCenterWorkspace key={teacher.id} teacher={teacher} onChanged={() => setAttempt(value => value + 1)} />}
    <details className="rounded-xl border border-[var(--admin-border)] p-4"><summary className="min-h-9 cursor-pointer font-bold">تحصيل شحن الطلاب (مش أرباح)</summary><TeacherCollectionsPanel key={`collections-${teacherId}`} teacherId={teacherId} details /></details>
    <section className="admin-panel space-y-4 rounded-2xl p-6">
      <h2 className="text-lg font-black">كشف حركات الأرباح</h2>
      <p className="text-sm text-[var(--admin-muted)]">كل الفترات. شحن رصيد الطالب لا يُحتسب كربح للمدرس قبل شراء المحتوى. المرتجعات ظاهرة كحركات سالبة؛ مبلغ المرتجع بجانب العملية الأصلية للتوضيح فقط ومش خصم مرة تانية.</p>
      <label className="flex flex-wrap items-center gap-3 text-sm font-bold">حالة الدفع<select className="min-h-11 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" value={status} onChange={(event) => { setStatus(event.target.value); setPage(1); }}><option value="">كل الحالات</option>{Object.entries(statuses).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      {error ? <div role="alert">تعذر تحميل كشف الحساب. <button type="button" className="min-h-11 px-3 underline" onClick={() => setAttempt((value) => value + 1)}>إعادة المحاولة</button></div>
        : !ledger ? <p role="status">جارٍ تحميل الحركات...</p>
          : <><div className="overflow-x-auto"><table className="w-full text-right text-sm"><caption className="sr-only">حركات أرباح {teacherName} — كل الفترات</caption><thead><tr>{['التاريخ والمصدر', 'المحتوى', 'حصة المدرس وطريقة حسابها', 'المرتجع من الحركة', 'الحالة'].map((label) => <th scope="col" key={label} className="whitespace-nowrap p-3">{label}</th>)}</tr></thead><tbody>{ledger.items.map((line) => <tr key={line.id} className="border-t border-[var(--admin-border)]"><td className="whitespace-nowrap p-3">{formatCairoDateTime(line.occurredAt, { dateStyle: 'medium' })}<p className="mt-1 text-xs text-[var(--admin-muted)]">{incomeSourceLabels[line.sourceType] ?? 'مصدر آخر'}</p></td><td className="p-3">{line.contentNameSnapshot || '—'}</td><td className="whitespace-nowrap p-3 font-mono">{teacherMoney(line.teacherShareAmount)}<TeacherAllocationExplanation line={line} /></td><td className="whitespace-nowrap p-3 font-mono">{teacherMoney(line.reversedAmount)}</td><td className="p-3">{line.reviewStatus === 'PendingReview' ? 'تحت المراجعة · خارج الأرباح' : line.reviewStatus === 'Rejected' ? 'مرفوض · خارج الأرباح' : line.retainedByTeacher && line.payoutStatus === 'Paid' ? 'محتفظ به من الأكواد' : statuses[line.payoutStatus] ?? 'غير مستحق'}</td></tr>)}{ledger.items.length === 0 && <tr><td colSpan={5} className="p-8 text-center text-[var(--admin-muted)]">لا توجد حركات مطابقة.</td></tr>}</tbody></table></div>
            <div className="flex flex-wrap items-center justify-between gap-3 text-sm"><p>{ledger.total} حركة · صفحة {page} من {Math.max(1, Math.ceil(ledger.total / ledger.pageSize))}</p><div className="flex gap-3"><button type="button" className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 disabled:opacity-40" disabled={page === 1} onClick={() => setPage((current) => current - 1)}>السابق</button><button type="button" className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 disabled:opacity-40" disabled={page * ledger.pageSize >= ledger.total} onClick={() => setPage((current) => current + 1)}>التالي</button></div></div></>}
    </section>
  </div>;
}
