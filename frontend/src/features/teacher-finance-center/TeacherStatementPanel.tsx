'use client';

import { useEffect, useState } from 'react';
import { Download, RefreshCw } from 'lucide-react';
import { formatCairoDateTime } from '@/lib/cairo-time';
import { financeService } from '@/services/finance-service';
import type { TeacherStatement, TeacherStatementRow } from './types';
import { teacherMoney } from './TeacherAccountOverview';

const kindLabels: Record<TeacherStatementRow['kind'], string> = {
  Earning: 'ربح أو مرتجع', Payout: 'طلب سحب', Settlement: 'تسوية',
  SettlementPayment: 'صرف تسوية', Adjustment: 'تعديل أو مديونية',
  CodeDelivery: 'تسليم أكواد', CodePayment: 'سداد أكواد', StudentCollection: 'شحن طالب',
  CodeActivation: 'كود مستخدم', StudentRefund: 'استرداد طالب',
};
const statusLabels: Record<string, string> = {
  AutoApproved: 'معتمد', Approved: 'معتمد', PendingReview: 'تحت المراجعة', Pending: 'قيد المراجعة',
  Paid: 'تم الصرف', Rejected: 'مرفوض', Reserved: 'محجوز', Retained: 'محتفظ به',
  Open: 'مفتوح', Applied: 'مطبق', Voided: 'ملغي', Cancelled: 'ملغي', Received: 'تم السداد',
  Matched: 'مطابق', Confirmed: 'مؤكد', Draft: 'مسودة', Reviewed: 'تمت المراجعة',
  Unpaid: 'غير مصروف', Debt: 'مديونية', Reversed: 'مرتجع', ReversedDebt: 'مرتجع / مديونية',
  Used: 'مستخدم', Refunded: 'تم الاسترداد',
};

export function TeacherStatementPanel({ teacherId, refreshVersion = 0 }: { teacherId?: string; refreshVersion?: number }) {
  const [draftFrom, setDraftFrom] = useState('');
  const [draftTo, setDraftTo] = useState('');
  const [period, setPeriod] = useState({ from: '', to: '' });
  const [page, setPage] = useState(1);
  const [version, setVersion] = useState(0);
  const [statement, setStatement] = useState<TeacherStatement | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [downloading, setDownloading] = useState(false);
  const [filterError, setFilterError] = useState('');

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError('');
    void financeService.getTeacherStatement(teacherId, { from: period.from || undefined, to: period.to || undefined, page, pageSize: 25 })
      .then(result => { if (active) setStatement(result); })
      .catch(() => { if (active) setError('تعذر تحميل كشف الحساب. حاول مرة أخرى.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [teacherId, period, page, version, refreshVersion]);

  const applyPeriod = (event: React.FormEvent) => {
    event.preventDefault();
    if (draftFrom && draftTo && draftFrom > draftTo) {
      setFilterError('تاريخ البداية لازم يكون قبل تاريخ النهاية.');
      return;
    }
    setFilterError('');
    setPage(1);
    setPeriod({ from: draftFrom, to: draftTo });
  };

  const downloadPdf = async () => {
    if (downloading) return;
    setDownloading(true);
    try {
      const blob = await financeService.exportTeacherStatementPdf(teacherId, { from: period.from || undefined, to: period.to || undefined });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `كشف-حساب-المدرس${period.from ? `-من-${period.from}` : ''}${period.to ? `-إلى-${period.to}` : ''}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch {
      setError('تعذر تنزيل PDF. حاول مرة أخرى.');
    } finally {
      setDownloading(false);
    }
  };

  const totalPages = Math.max(1, Math.ceil((statement?.total ?? 0) / 25));
  return <section className="admin-panel space-y-5 rounded-2xl p-5 sm:p-6" aria-label="كشف حساب المدرس">
    <div className="flex flex-wrap items-start justify-between gap-4">
      <div><h2 className="text-xl font-black text-[var(--admin-text)]">ملخص حساب المدرس</h2>
        <p className="mt-1 max-w-3xl text-sm leading-6 text-[var(--admin-muted)]">الطلاب دفعوا كام، نصيب المدرس والمنصة، واللي اتدفع والمتبقي.</p></div>
      <button type="button" onClick={() => void downloadPdf()} disabled={downloading || loading || !!error}
        className="admin-btn-primary inline-flex min-h-11 items-center gap-2 px-4 disabled:opacity-50">
        <Download className="h-4 w-4" aria-hidden="true" />{downloading ? 'جارٍ تجهيز PDF...' : 'كشف حساب بسيط PDF'}
      </button>
    </div>

    <form onSubmit={applyPeriod} className="flex flex-wrap items-end gap-3 border-y border-[var(--admin-border)] py-4">
      <label className="space-y-1 text-sm font-bold">من تاريخ<input type="date" value={draftFrom} onChange={event => setDraftFrom(event.target.value)}
        className="block min-h-11 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" /></label>
      <label className="space-y-1 text-sm font-bold">إلى تاريخ<input type="date" value={draftTo} onChange={event => setDraftTo(event.target.value)}
        className="block min-h-11 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" /></label>
      <button type="submit" className="admin-btn-ghost min-h-11 px-5">عرض الفترة</button>
      {(period.from || period.to) && <button type="button" className="min-h-11 px-2 underline" onClick={() => { setDraftFrom(''); setDraftTo(''); setPeriod({ from: '', to: '' }); setPage(1); setFilterError(''); }}>كل الفترات</button>}
      {filterError && <p role="alert" className="w-full text-sm text-red-700">{filterError}</p>}
    </form>

    {error && <div role="alert" className="flex flex-wrap items-center gap-2 text-sm">{error}
      <button type="button" onClick={() => setVersion(value => value + 1)} className="inline-flex min-h-11 items-center gap-1 underline"><RefreshCw className="h-4 w-4" />إعادة المحاولة</button></div>}
    {loading && <p role="status" className="py-5 text-sm text-[var(--admin-muted)]">جارٍ تحميل كشف الحساب...</p>}
    {!loading && statement && !error && <>
      <dl className="grid gap-x-6 gap-y-5 sm:grid-cols-2 lg:grid-cols-4">
        <div><dt className="text-sm text-[var(--admin-muted)]">طلاب اشتروا</dt><dd className="mt-2 text-2xl font-bold">{statement.activity.purchasingStudents} طالب</dd><p className="text-sm">{statement.activity.purchaseOperations} عملية شراء</p></div>
        {[
          ['قيمة الشراء', statement.activity.purchaseValue],
          ['نصيب المدرس بعد المرتجعات', statement.totals.earned],
          ['نصيب المنصة بعد المرتجعات', statement.totals.platformEarned],
        ].map(([label, amount]) => <div key={label}><dt className="text-sm text-[var(--admin-muted)]">{label}</dt><dd className="mt-2 text-xl font-bold tabular-nums">{teacherMoney(Number(amount))}</dd></div>)}
      </dl>
      <div className="overflow-x-auto border-y border-[var(--admin-border)] py-3">
        <table className="w-full min-w-[620px] text-right text-sm">
          <caption className="pb-3 text-start font-bold">كام عملية × كام جنيه</caption>
          <thead><tr>{['العمليات × السعر', 'الإجمالي', 'نصيب المدرس', 'نصيب المنصة', 'نسبة المنصة الفعلية'].map(label => <th key={label} scope="col" className="p-3">{label}</th>)}</tr></thead>
          <tbody>{statement.sales.map((sale, index) => <tr key={index} className="border-t border-[var(--admin-border)]">
            <td className="p-3">{sale.operations} × {teacherMoney(sale.unitPrice)}<span className="block text-xs text-[var(--admin-muted)]">{sale.students} طالب</span></td>
            <td className="p-3 tabular-nums">{teacherMoney(sale.total)}</td><td className="p-3 tabular-nums">{teacherMoney(sale.teacherShare)}</td><td className="p-3 tabular-nums">{teacherMoney(sale.platformShare)}</td><td className="p-3">{sale.platformPercent == null ? '—' : `${sale.platformPercent}٪`}</td>
          </tr>)}{!statement.sales.length && <tr><td colSpan={5} className="p-4 text-[var(--admin-muted)]">لا توجد مشتريات في الفترة دي.</td></tr>}</tbody>
        </table>
        <p className="mt-2 text-xs leading-6 text-[var(--admin-muted)]">الطالب قد يشتري أكثر من مرة. النسبة من المبلغ الموزّع وقت البيع؛ الباقة المشتركة قد تشمل نصيب مدرس آخر. الأكواد معروضة تحت.</p>
      </div>
      <section className="space-y-4" aria-label="المدفوع والمتبقي">
        <h3 className="font-bold">دفعت له كام وباقي له كام؟</h3>
        <dl className="grid gap-5 sm:grid-cols-3">{[
          ['دفعت له في الفترة', statement.totals.teacherPayments],
          ['نصيبه اللي احتفظ به من الأكواد', statement.totals.retainedEarnings],
          ['باقي له الآن ومتاح للصرف', statement.account.netPayable],
        ].map(([label, amount]) => <div key={label}><dt className="text-sm text-[var(--admin-muted)]">{label}</dt><dd className="mt-1 text-xl font-bold tabular-nums">{teacherMoney(Number(amount))}</dd></div>)}</dl>
        <p className="text-sm text-[var(--admin-muted)]">التحويلات المسجلة للمدرس، ومنها فودافون كاش، متخصّمة من المتبقي. المتبقي يشمل كل الفترات.</p>
        {statement.account.reserved > 0 && <p className="text-sm">محجوز لصرف لم يكتمل: {teacherMoney(statement.account.reserved)}</p>}
        {statement.account.debt > 0 && <p className="text-sm">مديونية حالية: {teacherMoney(statement.account.debt)}</p>}
      </section>
      <section className="space-y-3 border-t border-[var(--admin-border)] pt-4" aria-label="ملخص الأكواد">
        <h3 className="font-bold">الأكواد اللي سلّمتها للمدرس</h3>
        {statement.codeBatches.map((batch, index) => <div key={index} className="flex flex-wrap justify-between gap-3 border-b border-[var(--admin-border)] py-3 text-sm">
          <span><b>{batch.name}</b> · {batch.codes} كود · قيمة {batch.value == null ? 'غير مسجلة' : teacherMoney(batch.value)}</span>
          <span>استلمت منه {teacherMoney(batch.collected)} · باقي عليه {batch.remaining == null ? 'غير مسجل' : teacherMoney(batch.remaining)}</span>
        </div>)}
        {!statement.codeBatches.length && <p className="text-sm text-[var(--admin-muted)]">لا توجد دفعات تسليم في الفترة دي.</p>}
        <p className="text-sm">اتستخدم {statement.activity.activatedCodes} كود بقيمة {teacherMoney(statement.activity.activatedCodeValue)}. إجمالي الباقي عليه من الأكواد الآن: <b>{teacherMoney(statement.account.codeAmountDue ?? 0)}</b>.</p>
        <p className="text-xs text-[var(--admin-muted)]">سداد كل دفعة حتى نهاية الفترة. تسليم الكود واستخدامه لا يُحسبان مرتين؛ الباقي عليه من الأكواد منفصل عن المتاح لصرف أرباحه.</p>
      </section>
      <details className="border-t border-[var(--admin-border)] pt-3"><summary className="min-h-11 cursor-pointer font-bold">تحويلات الطلاب المقبولة والمرتجعات</summary>
        <p className="py-2 text-sm">{statement.activity.rechargeOperations} تحويل مقبول = {teacherMoney(statement.activity.rechargeAmount)}. المقبول يدويًا والمطابق تلقائيًا محسوبين مع بعض.</p>
        <p className="py-2 text-sm">استرد {statement.activity.refundedStudents} طالب {teacherMoney(statement.activity.refundAmount)}.</p>
        <p className="text-xs text-[var(--admin-muted)]">شحن الرصيد لا يُضاف للمبيعات قبل شراء المحتوى.</p>
      </details>
      <details className="border-t border-[var(--admin-border)] pt-3"><summary className="min-h-11 cursor-pointer font-bold">عرض تفاصيل الحركات ({statement.total})</summary>
      <div className="mt-3 overflow-x-auto rounded-xl border border-[var(--admin-border)]">
        <table className="w-full min-w-[900px] text-right text-sm"><caption className="sr-only">حركات كشف حساب {statement.teacherName}</caption>
          <thead className="bg-[var(--admin-card-soft)]"><tr>{['التاريخ والنوع', 'البيان والتفاصيل', 'الحالة', 'ربح المدرس', 'صرف للمدرس', 'مستحق للمنصة', 'سداد للمنصة'].map(label => <th scope="col" key={label} className="p-3 font-bold">{label}</th>)}</tr></thead>
          <tbody>{statement.items.map(row => <tr key={`${row.kind}-${row.id}`} className="border-t border-[var(--admin-border)] align-top">
            <td className="whitespace-nowrap p-3"><span dir="ltr">{formatCairoDateTime(row.occurredAt, { dateStyle: 'short', timeStyle: 'short' })}</span><span className="mt-1 block text-xs font-bold">{kindLabels[row.kind]}</span></td>
            <td className="min-w-64 p-3"><strong className="font-bold">{row.title}</strong><span className="mt-1 block text-xs leading-5 text-[var(--admin-muted)]">{row.detail}</span>{row.reference && <span className="mt-1 block break-all text-xs">مرجع: <bdi>{row.reference}</bdi></span>}
              {row.studentCollectionAmount != null && <span className="mt-1 block text-xs font-bold">شحن الطالب: {teacherMoney(row.studentCollectionAmount)}</span>}
              {row.studentRefundAmount != null && <span className="mt-1 block text-xs font-bold">استرداد الطالب: {teacherMoney(row.studentRefundAmount)}</span>}
              {row.adjustmentAmount != null && <span className="mt-1 block text-xs font-bold">قيمة التعديل: {teacherMoney(row.adjustmentAmount)}</span>}</td>
            <td className="whitespace-nowrap p-3">{statusLabels[row.status] ?? row.status}</td>
            <td className="whitespace-nowrap p-3 tabular-nums">{row.kind === 'Earning' ? teacherMoney(row.teacherShareAmount ?? 0) : '—'}{row.kind === 'Earning' && !row.recognized && <span className="block text-xs text-[var(--admin-muted)]">خارج الإجمالي</span>}</td>
            <td className="whitespace-nowrap p-3 tabular-nums">{row.teacherPaymentAmount == null ? '—' : teacherMoney(row.teacherPaymentAmount)}</td>
            <td className="whitespace-nowrap p-3 tabular-nums">{row.platformDueAmount == null ? '—' : teacherMoney(row.platformDueAmount)}</td>
            <td className="whitespace-nowrap p-3 tabular-nums">{row.platformPaymentAmount == null ? '—' : teacherMoney(row.platformPaymentAmount)}</td>
          </tr>)}{statement.items.length === 0 && <tr><td colSpan={7} className="p-8 text-center text-[var(--admin-muted)]">مافيش حركات في الفترة دي. جرّب فترة تانية أو اعرض كل الفترات.</td></tr>}</tbody>
        </table>
      </div>
      <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm"><p>صفحة {page} من {totalPages}. ملف PDF يعرض ملخصًا بسيطًا للمبيعات والأكواد والمدفوع.</p>
        <div className="flex gap-2"><button type="button" disabled={page <= 1} onClick={() => setPage(value => value - 1)} className="admin-btn-ghost min-h-11 px-4 disabled:opacity-40">السابق</button>
          <button type="button" disabled={page >= totalPages} onClick={() => setPage(value => value + 1)} className="admin-btn-ghost min-h-11 px-4 disabled:opacity-40">التالي</button></div></div></details>
    </>}
  </section>;
}
