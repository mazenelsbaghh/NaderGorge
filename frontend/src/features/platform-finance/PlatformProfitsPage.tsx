'use client';

import { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { ArrowDownToLine, CircleDollarSign, RefreshCw, Receipt, TrendingUp } from 'lucide-react';
import { AdminPage, AdminStatCard } from '@/components/admin';
import { cairoCurrentDate, cairoCurrentMonthPeriod, formatCairoTimestamp } from '@/lib/cairo-time';
import { getPlatformProfitReport, profitReportCsv, type PlatformProfitReport, type ProfitTeacherRow } from '@/services/platform-profits-service';

const money = (amount: number) => `${new Intl.NumberFormat('ar-EG-u-nu-latn', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(amount)} ج.م`;
const dateLabel = (date: string) => date.split('-').reverse().join('/');

function TeacherProfitTable({ rows }: { rows: ProfitTeacherRow[] }) {
  const [expanded, setExpanded] = useState<string | null>(null);
  const detailRef = useRef<HTMLElement>(null);
  useEffect(() => {
    if (!expanded || !detailRef.current) return;
    detailRef.current.focus({ preventScroll: true });
    detailRef.current.scrollIntoView({ block: 'start', behavior: 'instant' });
  }, [expanded]);
  const totals = rows.reduce((sum, { period }) => ({ sales: sum.sales + period.grossSales,
    teacher: sum.teacher + period.teacherShare, platform: sum.platform + period.platformShare,
    refunds: sum.refunds + period.refunds, paid: sum.paid + period.paid }), { sales: 0, teacher: 0, platform: 0, refunds: 0, paid: 0 });
  return <>
    <div className="overflow-x-auto rounded-xl border border-[var(--admin-border)]" tabIndex={0} role="region" aria-label="جدول أرباح المدرسين">
      <table className="w-full min-w-[1000px] text-start text-sm tabular-nums">
        <caption className="sr-only">توزيع أرباح المبيعات والمرتجعات والمدفوعات حسب المدرّس خلال الفترة المختارة</caption>
        <thead className="bg-[var(--admin-card-soft)] text-[var(--admin-text)]"><tr>
          {['المدرّس', 'المبيعات المدفوعة قبل المرتجعات', 'حصة المدرّس', 'حصة المنصّة', 'المرتجعات', 'المصروف للمدرّس', 'تفاصيل الحساب'].map(label => <th key={label} scope="col" className="whitespace-nowrap px-4 py-4 text-start font-bold">{label}</th>)}
        </tr></thead>
        <tbody>{rows.map(({ period: row, reconciliationDifference }) => <tr key={row.teacherId} className="border-t border-[var(--admin-border)] hover:bg-[var(--admin-hover)]">
          <th scope="row" className="px-4 py-4 text-start font-bold">{row.teacherName}{Math.abs(reconciliationDifference) >= 0.01 && <span className="mt-1 block text-xs font-medium text-[var(--admin-muted)]">رصيد الحساب المسجّل يحتاج مطابقة</span>}</th>
          <td className="whitespace-nowrap px-4 py-4">{money(row.grossSales)}</td>
          <td className="whitespace-nowrap px-4 py-4">{money(row.teacherShare)}</td>
          <td className="whitespace-nowrap px-4 py-4 font-bold text-[var(--admin-primary)]">{money(row.platformShare)}</td>
          <td className="whitespace-nowrap px-4 py-4">{money(row.refunds)}</td>
          <td className="whitespace-nowrap px-4 py-4">{money(row.paid)}</td>
          <td className="px-4 py-2"><button type="button" className="admin-btn-ghost min-h-11 whitespace-nowrap px-3" aria-expanded={expanded === row.teacherId} aria-controls={expanded === row.teacherId ? `profit-details-${row.teacherId}` : undefined} onClick={() => setExpanded(expanded === row.teacherId ? null : row.teacherId)}>تفاصيل {row.teacherName}</button></td>
        </tr>)}</tbody>
        <tfoot className="border-t-2 border-[var(--admin-border)] bg-[var(--admin-card-soft)] font-bold"><tr>
          <th scope="row" className="px-4 py-4 text-start">إجمالي المعروض</th>
          {[totals.sales, totals.teacher, totals.platform, totals.refunds, totals.paid].map((amount, i) => <td key={i} className="whitespace-nowrap px-4 py-4">{money(amount)}</td>)}<td />
        </tr></tfoot>
      </table>
    </div>
    {rows.filter(row => row.period.teacherId === expanded).map(row => <section key={row.period.teacherId} id={`profit-details-${row.period.teacherId}`} ref={detailRef} tabIndex={-1} className="scroll-mt-6 rounded-xl bg-[var(--admin-card-soft)] p-5 focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]" aria-label={`حساب ${row.period.teacherName}`}>
      <div className="flex flex-wrap items-center justify-between gap-3"><h3 className="text-lg font-bold">حساب {row.period.teacherName}</h3><Link className="admin-btn-ghost inline-flex min-h-11 items-center px-4" href={`/admin/teachers/${row.period.teacherId}/account`}>فتح كشف الحساب التفصيلي</Link></div>
      <p className="mt-4 font-bold">المستحق المحسوب الآن: {money(row.currentCalculatedBalance)}</p>
      <dl className="mt-4 flex flex-wrap gap-x-10 gap-y-4">
        <div><dt className="text-sm text-[var(--admin-muted)]">المستحق المحسوب في نهاية الفترة</dt><dd className="mt-1 font-bold tabular-nums">{money(row.period.outstanding)}</dd></div>
        <div><dt className="text-sm text-[var(--admin-muted)]">رصيد حساب المدرّس الآن</dt><dd className="mt-1 font-bold tabular-nums">{money(row.currentAccountBalance)}</dd></div>
        <div><dt className="text-sm text-[var(--admin-muted)]">رصيد الدفتر الآن</dt><dd className="mt-1 font-bold tabular-nums">{money(row.currentLedgerBalance)}</dd></div>
        <div><dt className="text-sm text-[var(--admin-muted)]">فرق المحسوب عن الحساب المسجّل</dt><dd className="mt-1 font-bold tabular-nums">{money(row.reconciliationDifference)}</dd></div>
      </dl>
      <p className="mt-4 max-w-prose text-sm leading-6 text-[var(--admin-muted)]">رصيد نهاية الفترة يشمل رصيد بدايتها. أرصدة «الآن» تشمل الحركات اللاحقة أيضًا. الأرباح تخص المبيعات، والمصروف للمدرّس يخص ما تم دفعه فعليًا.</p>
    </section>)}
  </>;
}

export default function PlatformProfitsPage() {
  const today = cairoCurrentDate();
  const [from, setFrom] = useState(cairoCurrentMonthPeriod().first);
  const [to, setTo] = useState(today);
  const [period, setPeriod] = useState({ from, to });
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
      .catch(() => { if (!controller.signal.aborted) setError('تعذر تحميل تقرير الأرباح. أعد المحاولة.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [period, attempt]);

  const rows = report?.teachers.filter(row => !teacherId || row.period.teacherId === teacherId) ?? [];
  const differences = report?.teachers.filter(row => Math.abs(row.reconciliationDifference) >= 0.01) ?? [];
  const invalidDates = !from || !to || from > to;
  const applyPeriod = (event: React.FormEvent) => {
    event.preventDefault();
    if (invalidDates) return;
    setPeriod({ from, to });
    setAttempt(value => value + 1);
  };
  const download = () => {
    if (!report) return;
    const url = URL.createObjectURL(new Blob([profitReportCsv(report, rows, period.from, period.to)], { type: 'text/csv;charset=utf-8;' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `platform-profits-${period.from}-${period.to}.csv`;
    anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };

  return <AdminPage activePath="/admin/platform-profits" sectionLabel="الحسابات" pageTitle="أرباح المنصّة" subtitle="حصة كل مدرّس وحصة المنصّة من المبيعات، مع المصروفات وصافي الربح خلال الفترة المختارة.">
    <div className="space-y-6" dir="rtl">
      <form onSubmit={applyPeriod} className="flex flex-wrap items-end gap-3 rounded-xl bg-[var(--admin-card)] p-4">
        <label className="text-sm font-bold">من<input className="admin-input mt-2 block min-h-11" type="date" value={from} onChange={event => setFrom(event.target.value)} required /></label>
        <label className="text-sm font-bold">إلى<input className="admin-input mt-2 block min-h-11" type="date" value={to} onChange={event => setTo(event.target.value)} required /></label>
        <button className="admin-btn-primary inline-flex min-h-11 items-center gap-2 px-4" type="submit" disabled={loading || invalidDates}><RefreshCw size={17} aria-hidden="true" />عرض التقرير</button>
        <button className="admin-btn-ghost min-h-11 px-4" type="button" disabled={!report || loading} onClick={() => { if (report) { setFrom(report.earliestDate); setTo(today); setPeriod({ from: report.earliestDate, to: today }); } }}>من بداية التسجيل</button>
        {invalidDates && <p role="alert" className="w-full text-sm text-red-700">اختر فترة صحيحة؛ تاريخ البداية لا يتجاوز تاريخ النهاية.</p>}
      </form>
      {loading ? <div role="status" className="rounded-xl bg-[var(--admin-card)] px-6 py-16 text-center text-[var(--admin-muted)]">جارٍ تحميل تقرير الأرباح...</div> : error ? <div role="alert" className="rounded-xl border border-[var(--admin-border)] p-6"><p>{error}</p><button className="admin-btn-primary mt-4 min-h-11 px-4" type="button" onClick={() => setAttempt(value => value + 1)}>إعادة المحاولة</button></div> : report && <>
        <div className="flex flex-wrap items-center justify-between gap-3"><p className="font-bold">الفترة: {dateLabel(period.from)} إلى {dateLabel(period.to)}</p><p className="text-sm text-[var(--admin-muted)]">آخر تحديث: {formatCairoTimestamp(report.generatedAt)}</p></div>
        {differences.length > 0 && <section role="status" className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 text-sm leading-7"><strong>أرصدة الحسابات المسجّلة تحتاج مطابقة لعدد {differences.length} من المدرسين.</strong><p>أرباح التقرير محسوبة من المدفوعات الفعلية والاتفاقات المعتمدة بأثر رجعي. تصحيح التقرير لا يغيّر رصيد حساب المدرّس المسجّل؛ تظهر المقارنة في التفاصيل.</p></section>}
        <section aria-label="نتيجة المنصة لكل المدرسين" className="grid gap-4 md:grid-cols-3 [&_.text-4xl]:text-2xl">
          <AdminStatCard label="إيراد المنصّة بعد مرتجعاتها" value={money(report.platform.revenue - report.platform.refunds)} icon={CircleDollarSign} variant="light" subtitle="حصة المنصّة من مبيعات المدرسين بعد الإلغاءات" />
          <AdminStatCard label="مصروفات المنصّة" value={money(report.platform.expenses)} icon={Receipt} variant="muted" subtitle="المصروفات المسجّلة خلال الفترة" />
          <AdminStatCard label="صافي ربح المبيعات" value={money(report.platform.netProfit)} icon={TrendingUp} variant="accent" subtitle="الإيرادات ناقص المرتجعات والمصروفات" />
        </section>
        <section className="space-y-4 rounded-xl bg-[var(--admin-card)] p-4 sm:p-6">
          <div className="flex flex-wrap items-end justify-between gap-4"><div><h2 className="text-xl font-bold">ربح كل مدرّس وحصة المنصّة</h2><p className="mt-2 max-w-prose text-sm leading-6 text-[var(--admin-muted)]">طُبّقت اتفاقات المدرسين المعتمدة بتاريخ ١٩ سبتمبر ٢٠٢٦ على المبيعات الأقدم، مع الحفاظ على الاتفاقات الخاصة. تُحسب المبالغ المدفوعة فقط، وتُستبعد الهدايا وتُخصم الإلغاءات والمرتجعات. شحن المحافظ لا يُحسب ربحًا عند الشحن.</p></div>
            <div className="flex flex-wrap items-end gap-3"><label className="text-sm font-bold">المدرّس<select className="admin-input mt-2 block min-h-11 max-w-full" value={teacherId} onChange={event => setTeacherId(event.target.value)}><option value="">كل المدرسين</option>{report.teachers.map(row => <option key={row.period.teacherId} value={row.period.teacherId}>{row.period.teacherName}</option>)}</select></label>
              <button type="button" className="admin-btn-ghost inline-flex min-h-11 items-center gap-2 px-4" onClick={download}><ArrowDownToLine size={17} aria-hidden="true" />تصدير التقرير CSV</button></div>
          </div>
          {rows.length ? <TeacherProfitTable rows={rows} /> : <p className="py-8 text-center text-[var(--admin-muted)]">لا يوجد مدرسون لعرضهم في التقرير.</p>}
          <p className="text-sm leading-6 text-[var(--admin-muted)]">الملخص العلوي يخص المنصّة كلها؛ اختيار المدرّس يفلتر الجدول فقط. تُنسب الحركات للفترة حسب تاريخ تسجيلها، ولا تُوزّع المصروفات العامة على المدرسين بافتراض نسبة غير مسجّلة.</p>
        </section>
      </>}
    </div>
  </AdminPage>;
}
