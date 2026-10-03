'use client';

import { useEffect, useState } from 'react';
import { financeService } from '@/services/finance-service';
import { teacherMoney } from './TeacherAccountOverview';
import type { TeacherSettlementPaymentInput, TeacherTransferQuote } from './types';

export function TeacherSettlementPaymentForm({ settlementId, isBusy, onPay }: {
  settlementId: string;
  isBusy: boolean;
  onPay: (input: TeacherSettlementPaymentInput) => Promise<void>;
}) {
  const [paymentMethod, setPaymentMethod] = useState('فودافون كاش');
  const [preview, setPreview] = useState<{ method: string; quote: TeacherTransferQuote } | null>(null);
  const [previewError, setPreviewError] = useState(false);
  const [retryVersion, setRetryVersion] = useState(0);
  const quote = preview?.method === paymentMethod ? preview.quote : null;

  useEffect(() => {
    let active = true;
    setPreview(null); setPreviewError(false);
    void financeService.previewTeacherSettlementPayment(settlementId, paymentMethod)
      .then(quote => { if (active) setPreview({ method: paymentMethod, quote }); })
      .catch(() => { if (active) setPreviewError(true); });
    return () => { active = false; };
  }, [settlementId, paymentMethod, retryVersion]);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!quote || isBusy || quote.netTransferAmount < 0) return;
    const fields = new FormData(event.currentTarget);
    await onPay({ paymentMethod, amount: quote.netTransferAmount,
      transferReference: String(fields.get('transferReference') || '').trim(),
      attachmentUrl: String(fields.get('attachmentUrl') || '').trim() || undefined });
    setRetryVersion(version => version + 1);
  };

  return <form onSubmit={submit} className="space-y-3 border-t border-[var(--admin-border)] pt-4">
    <p className="font-black text-[var(--admin-text)]">تسجيل التحويل للمدرّس</p>
    <div className="grid gap-3 sm:grid-cols-2">
      <label className="text-sm font-bold">طريقة التحويل<select aria-label="طريقة التحويل" disabled={isBusy} value={paymentMethod} onChange={event => setPaymentMethod(event.target.value)} className="mt-1 min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] px-3 text-sm">
        <option value="فودافون كاش">فودافون كاش</option><option value="bank">تحويل بنكي</option><option value="cash">نقدي</option><option value="other">طريقة أخرى</option>
      </select></label>
      <label className="text-sm font-bold">مرجع التحويل<input required disabled={isBusy} name="transferReference" placeholder="رقم العملية أو الإيصال" className="mt-1 min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-3 text-sm" /></label>
    </div>
    <div aria-live="polite" className="rounded-xl bg-[var(--admin-card-soft)] p-3 text-sm">
      {previewError ? <p role="alert">تعذر حساب مبلغ التحويل. <button type="button" onClick={() => setRetryVersion(version => version + 1)} className="min-h-11 underline">جرّب تاني</button></p>
        : !quote ? <p>بنحسب مبلغ التحويل…</p> : quote.netTransferAmount < 0 ? <p role="alert">عمولة التحويل أكبر من مستحقاته. راجع بنود التسوية.</p> : <dl className="space-y-2">
          <div className="flex flex-wrap justify-between gap-2"><dt>مستحقاته قبل عمولة التحويل</dt><dd>{teacherMoney(quote.teacherAmount)}</dd></div>
          {quote.feeRate > 0 && <><div className="flex flex-wrap justify-between gap-2"><dt>نصيب المنصة اللي بنحسب عليه</dt><dd>{teacherMoney(quote.platformShareBasis)}</dd></div>
            <div className="flex flex-wrap justify-between gap-2"><dt>عمولة فودافون كاش — {quote.feeRate}٪ من نصيبنا</dt><dd>{teacherMoney(quote.transferFee)}</dd></div>
            <p className="text-[var(--admin-muted)]">العمولة بتتخصم من مستحقاته وبتتضاف لحساب المنصة.</p></>}
          <div className="flex flex-wrap justify-between gap-2 border-t border-[var(--admin-border)] pt-2 font-black"><dt>اللي يتحوّل للمدرّس</dt><dd>{teacherMoney(quote.netTransferAmount)}</dd></div>
        </dl>}
    </div>
    <label className="block text-sm font-bold">رابط الإيصال (اختياري)<input disabled={isBusy} name="attachmentUrl" type="url" className="mt-1 min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-3 text-sm" /></label>
    <div className="flex justify-end"><button disabled={isBusy || !quote || quote.netTransferAmount < 0} className="min-h-11 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-white disabled:opacity-50">تسجيل تحويل{quote ? ` ${teacherMoney(quote.netTransferAmount)}` : ''}</button></div>
  </form>;
}
