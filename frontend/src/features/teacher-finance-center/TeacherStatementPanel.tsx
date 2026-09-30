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

export function TeacherStatementPanel({ teacherId }: { teacherId?: string }) {
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
  }, [teacherId, period, page, version]);

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
        <p className="mt-1 max-w-3xl text-sm leading-6 text-[var(--admin-muted)]">عدد الطلاب والمبالغ اللي وصلت، فودافون كاش، الاستردادات والأكواد المستخدمة، ثم تفاصيل كل عملية بالاسم والتاريخ.</p></div>
      <button type="button" onClick={() => void downloadPdf()} disabled={downloading || loading || !!error}
        className="admin-btn-primary inline-flex min-h-11 items-center gap-2 px-4 disabled:opacity-50">
        <Download className="h-4 w-4" aria-hidden="true" />{downloading ? 'جارٍ تجهيز PDF...' : 'تنزيل كشف الحساب PDF'}
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
      <div className="space-y-3"><h3 className="text-base font-black">الطلاب والتحصيل خلال الفترة</h3>
        <dl className="grid gap-x-6 gap-y-5 sm:grid-cols-2 xl:grid-cols-3">
          <div className="border-b border-[var(--admin-border)] pb-3"><dt className="text-sm font-bold text-[var(--admin-muted)]">طلاب اشتروا محتوى</dt><dd className="mt-1 text-2xl font-black tabular-nums">{statement.activity.purchasingStudents} طالب</dd><p className="mt-1 text-xs text-[var(--admin-muted)]">{statement.activity.purchaseOperations} عملية · قيمة مسجلة {teacherMoney(statement.activity.purchaseValue)}</p></div>
          <div className="border-b border-[var(--admin-border)] pb-3"><dt className="text-sm font-bold text-[var(--admin-muted)]">شحن رصيد الطلاب</dt><dd className="mt-1 text-2xl font-black tabular-nums">{teacherMoney(statement.activity.rechargeAmount)}</dd><p className="mt-1 text-xs text-[var(--admin-muted)]">{statement.activity.rechargeStudents} طالب · {statement.activity.rechargeOperations} عملية شحن</p></div>
          <div className="border-b border-[var(--admin-border)] pb-3"><dt className="text-sm font-bold text-[var(--admin-muted)]">من فودافون كاش المؤكد</dt><dd className="mt-1 text-2xl font-black tabular-nums">{teacherMoney(statement.activity.vodafoneCashAmount)}</dd><p className="mt-1 text-xs text-[var(--admin-muted)]">{statement.activity.vodafoneCashStudents} طالب · {statement.activity.vodafoneCashOperations} تحويل · مصادر أخرى/غير مؤكدة {teacherMoney(statement.activity.otherRechargeAmount)}</p></div>
          <div className="border-b border-[var(--admin-border)] pb-3"><dt className="text-sm font-bold text-[var(--admin-muted)]">طلاب استردوا</dt><dd className="mt-1 text-2xl font-black tabular-nums">{statement.activity.refundedStudents} طالب</dd><p className="mt-1 text-xs text-[var(--admin-muted)]">{statement.activity.refundOperations} عملية استرداد معتمدة · {teacherMoney(statement.activity.refundAmount)}</p></div>
          <div className="border-b border-[var(--admin-border)] pb-3"><dt className="text-sm font-bold text-[var(--admin-muted)]">أكواد اتستخدمت</dt><dd className="mt-1 text-2xl font-black tabular-nums">{statement.activity.activatedCodes} كود</dd><p className="mt-1 text-xs text-[var(--admin-muted)]">بواسطة {statement.activity.codeStudents} طالب · قيمة الأكواد {teacherMoney(statement.activity.activatedCodeValue)}</p></div>
        </dl></div>
      <p className="text-xs leading-5 text-[var(--admin-muted)]">الطالب بيتحسب مرة واحدة في كل فئة. فودافون كاش محسوب من رسائل التحويل المطابقة فقط. شحن الرصيد وقيمة الأكواد مش أرباح إضافية للمدرس.</p>
      <div className="space-y-3 border-t border-[var(--admin-border)] pt-4"><h3 className="text-base font-black">أرباح المدرس والصرف</h3>
        <dl className="grid gap-x-6 gap-y-4 sm:grid-cols-2 lg:grid-cols-4">
        {[
          ['أرباح الفترة بعد المرتجعات', statement.totals.earned],
          ['اتصرف للمدرس في الفترة', statement.totals.teacherPayments],
          ['محتفظ به من الأكواد', statement.totals.retainedEarnings],
          ['متاح للسحب الآن', statement.account.netPayable],
        ].map(([label, amount]) => <div key={label} className="border-b border-[var(--admin-border)] pb-3">
          <dt className="text-xs font-bold text-[var(--admin-muted)]">{label}</dt><dd className="mt-1 text-lg font-black tabular-nums">{teacherMoney(Number(amount))}</dd>
        </div>)}
        </dl></div>
      <details className="text-sm"><summary className="min-h-9 cursor-pointer font-bold">تفاصيل المبالغ الأخرى</summary>
        <dl className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">{[
          ['ربح تحت المراجعة', statement.totals.pendingEarnings],
          ['مستحق للمنصة من الأكواد', statement.totals.platformCodeDue],
          ['سداد الأكواد للمنصة', statement.totals.platformCodePayments],
          ['شحن الطلاب، خارج الأرباح', statement.totals.studentCollections],
          ['مديونيات مفتوحة سُجلت بالفترة', statement.totals.openDebtAdjustments],
          ['مديونية الحساب الحالية', statement.account.debt],
        ].map(([label, amount]) => <div key={label}><dt className="text-[var(--admin-muted)]">{label}</dt><dd className="font-bold tabular-nums">{teacherMoney(Number(amount))}</dd></div>)}</dl>
      </details>
      <p className="text-xs leading-5 text-[var(--admin-muted)]">أرقام الفترة حسب تاريخ كل حركة. «متاح للسحب الآن» و«مديونية الحساب الحالية» من بداية الحساب حتى الآن. طلبات السحب والتسويات غير المصروفة لا تدخل في مبلغ الصرف، والمرتجع يظهر كربح سالب مرة واحدة.</p>
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
      <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm"><p>صفحة {page} من {totalPages}. ملف PDF يشمل الملخص وكل الحركات في الفترة المحددة.</p>
        <div className="flex gap-2"><button type="button" disabled={page <= 1} onClick={() => setPage(value => value - 1)} className="admin-btn-ghost min-h-11 px-4 disabled:opacity-40">السابق</button>
          <button type="button" disabled={page >= totalPages} onClick={() => setPage(value => value + 1)} className="admin-btn-ghost min-h-11 px-4 disabled:opacity-40">التالي</button></div></div></details>
    </>}
  </section>;
}
