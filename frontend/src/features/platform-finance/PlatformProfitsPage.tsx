'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowDownToLine, Plus } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import { cairoCurrentDate, cairoCurrentMonthPeriod, formatCairoTimestamp } from '@/lib/cairo-time';
import { getPlatformProfitReport, profitReportCsv, type PlatformProfitReport } from '@/services/platform-profits-service';
import FinancePeriodPicker from './FinancePeriodPicker';
import FinanceProfitSummary, { financeMoney as money } from './FinanceProfitSummary';
import TeacherProfitList from './TeacherProfitList';

export default function PlatformProfitsPage() {
  const [period, setPeriod] = useState(() => ({ from: cairoCurrentMonthPeriod().first, to: cairoCurrentDate() }));
  const [teacherId, setTeacherId] = useState('');
  const [report, setReport] = useState<PlatformProfitReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    setReport(null);
    void getPlatformProfitReport(period.from, period.to, controller.signal)
      .then(result => { if (!controller.signal.aborted) setReport(result); })
      .catch(() => { if (!controller.signal.aborted) setError('تعذر تحميل الحسابات. جرّب مرة تانية.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [period, attempt]);

  const rows = report?.teachers.filter(row => !teacherId || row.period.teacherId === teacherId) ?? [];
  const download = () => {
    if (!report) return;
    const url = URL.createObjectURL(new Blob([profitReportCsv(report, rows, period.from, period.to)], { type: 'text/csv;charset=utf-8;' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `platform-profits-${period.from}-${period.to}.csv`;
    anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };

  return <AdminPage activePath="/admin/platform-profits" sectionLabel="الحسابات" pageTitle="الحسابات" subtitle="كام طالب اشترى، نصيب كل مدرس، ونصيب المنصة."
    action={<Link className="admin-btn-primary inline-flex min-h-11 items-center gap-2 px-4" href="/admin/platform-finance/operations"><Plus size={18} aria-hidden="true" />تسجيل مصروف</Link>}>
    <div className="mx-auto max-w-6xl space-y-6" dir="rtl">
      <FinancePeriodPicker period={period} onChange={setPeriod} loading={loading} earliestDate={report?.earliestDate} />
      {loading ? <div role="status" className="rounded-xl bg-[var(--admin-card)] px-6 py-16 text-center text-[var(--admin-muted)]">جارٍ تحميل الحسابات...</div> : error ? <div role="alert" className="rounded-xl border border-[var(--admin-border)] p-6"><p>{error}</p><button className="admin-btn-primary mt-4 min-h-11 px-4" type="button" onClick={() => setAttempt(value => value + 1)}>إعادة المحاولة</button></div> : report && <>
        <section className="border-y border-[var(--admin-border)] py-6" aria-label="ملخص الحسابات">
          <dl className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
            <div><dt className="text-sm text-[var(--admin-muted)]">طلاب اشتروا</dt><dd className="mt-2 text-2xl font-bold">{report.purchasingStudents} طالب</dd></div>
            {[
              ['المبيعات بعد المرتجعات', report.teachers.reduce((total, row) => total + row.period.grossSales - row.period.refunds, 0)],
              ['نصيب المدرسين', report.teachers.reduce((total, row) => total + row.period.teacherShare, 0)],
              ['نصيب المنصة', report.teachers.reduce((total, row) => total + row.period.platformShare, 0)],
            ].map(([label, amount]) => <div key={label}><dt className="text-sm text-[var(--admin-muted)]">{label}</dt><dd className="mt-2 text-2xl font-bold tabular-nums">{money(Number(amount))}</dd></div>)}
          </dl>
          <p className="mt-4 text-sm text-[var(--admin-muted)]">المبيعات تشمل الأكواد المحتسبة. عدد الطلاب يخص شراء المحتوى؛ استخدام الأكواد وتسليمها ظاهر في حساب كل مدرس.</p>
        </section>
        <nav aria-label="إجراءات الحسابات" className="flex flex-wrap gap-x-6 gap-y-2 text-sm font-bold">
          <Link className="inline-flex min-h-11 items-center text-[var(--admin-primary)] underline underline-offset-4" href="/admin/teacher-finance">اتفاقات المدرسين</Link>
          <Link className="inline-flex min-h-11 items-center text-[var(--admin-primary)] underline underline-offset-4" href="/admin/platform-finance/expenses">مراجعة المصاريف</Link>
          <Link className="inline-flex min-h-11 items-center text-[var(--admin-primary)] underline underline-offset-4" href="/admin/platform-finance/refunds">إرجاع فلوس لطالب</Link>
        </nav>
        <section className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 sm:px-6">
          <div className="flex flex-wrap items-center justify-between gap-4 border-b border-[var(--admin-border)] pb-4">
            <div><h2 className="text-xl font-bold">حساب كل مدرس</h2><p className="mt-1 text-sm text-[var(--admin-muted)]">المبيعات والأنصبة بعد المرتجعات في الفترة المختارة. افتح المدرس لكشف PDF والأكواد والمدفوعات.</p></div>
            <label className="text-sm font-bold">اختار المدرّس<select className="admin-input mt-2 block min-h-11 max-w-full" value={teacherId} onChange={event => setTeacherId(event.target.value)}><option value="">كل المدرسين</option>{report.teachers.map(row => <option key={row.period.teacherId} value={row.period.teacherId}>{row.period.teacherName}</option>)}</select></label>
          </div>
          {rows.length ? <TeacherProfitList key={`${period.from}-${period.to}-${attempt}`} rows={rows} /> : <p className="py-8 text-center text-[var(--admin-muted)]">لما تضيف مدرس، حسابه هيظهر هنا.</p>}
          {teacherId && <p className="border-t border-[var(--admin-border)] pt-4 text-sm text-[var(--admin-muted)]">ملخص الأرباح فوق يخص المنصّة كلها.</p>}
        </section>
        <details><summary className="min-h-11 cursor-pointer font-bold">المصروفات وصافي ربح المنصة</summary><FinanceProfitSummary dashboard={report.platform} /></details>
        <footer className="flex flex-wrap items-center justify-between gap-3 text-sm text-[var(--admin-muted)]">
          <p>آخر تحديث: {formatCairoTimestamp(report.generatedAt)}</p>
          <button type="button" className="admin-btn-ghost inline-flex min-h-11 items-center gap-2 px-4" onClick={download}><ArrowDownToLine size={17} aria-hidden="true" />تنزيل الحسابات CSV</button>
        </footer>
      </>}
    </div>
  </AdminPage>;
}
