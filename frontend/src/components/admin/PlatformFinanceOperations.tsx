'use client';

import { FormEvent, useEffect, useState } from 'react';
import Link from 'next/link';
import { Link2 } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import platformFinanceService, { FinanceBootstrap } from '@/services/platform-finance-service';

const idempotency = () => `${Date.now()}-${Math.random().toString(36).slice(2)}`;

export default function PlatformFinanceOperations() {
  const [bootstrap, setBootstrap] = useState<FinanceBootstrap | null>(null);
  const [amount, setAmount] = useState('');
  const [categoryId, setCategoryId] = useState('');
  const [description, setDescription] = useState('');
  const [treasuryId, setTreasuryId] = useState('');
  const [paidNow, setPaidNow] = useState(true);
  const [pendingExpense, setPendingExpense] = useState<{ id: string; key: string } | null>(null);
  const [savingExpense, setSavingExpense] = useState(false);
  const [savingRefund, setSavingRefund] = useState(false);
  const [bootstrapAttempt, setBootstrapAttempt] = useState(0);
  const [sourceId, setSourceId] = useState('');
  const [studentId, setStudentId] = useState('');
  const [platformAmount, setPlatformAmount] = useState('');
  const [teacherAmount, setTeacherAmount] = useState('');
  const [refundMethod, setRefundMethod] = useState('1');
  const [refundTreasuryId, setRefundTreasuryId] = useState('');
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;
    setError('');
    void platformFinanceService.bootstrap().then(result => { if (active) setBootstrap(result); })
      .catch(() => { if (active) setError('تعذر تحميل أنواع المصاريف والمحافظ.'); });
    return () => { active = false; };
  }, [bootstrapAttempt]);

  async function submitExpense(event: FormEvent) {
    event.preventDefault();
    if (savingExpense || (paidNow && !treasuryId)) return;
    setSavingExpense(true); setError(''); setMessage('');
    let record = pendingExpense;
    try {
      if (!record) {
        const created = await platformFinanceService.createExpense({ amount: Number(amount), occurredAt: new Date().toISOString(), categoryId, description });
        record = { id: created.id, key: idempotency() };
        setPendingExpense(record);
      }
      await platformFinanceService.postExpense(record.id, { treasuryAccountId: paidNow ? treasuryId : undefined, idempotencyKey: record.key });
      setMessage(paidNow ? 'اتسجّل المصروف واتخصم من المحفظة أو الخزنة المختارة.' : 'اتسجّل المصروف كمبلغ مطلوب دفعه. مفيش فلوس اتخصمت.');
      setPendingExpense(null); setAmount(''); setDescription('');
    } catch { setError(record ? 'المصروف اتحفظ، لكن تأكيد تسجيله ما اكتملش. أعد المحاولة أو راجع سجل المصاريف قبل تسجيله من جديد.' : 'تعذر تسجيل المصروف. راجع البيانات والصلاحيات.'); }
    finally { setSavingExpense(false); }
  }

  async function submitRefund(event: FormEvent) {
    event.preventDefault();
    if (savingRefund) return;
    setSavingRefund(true); setError(''); setMessage('');
    try {
      const created = await platformFinanceService.createRefund({ originalSourceId: sourceId, originalSourceType: 'Purchase', studentId, platformAmount: Number(platformAmount), teacherAmount: Number(teacherAmount || 0), method: Number(refundMethod), treasuryAccountId: refundMethod === '2' ? refundTreasuryId : undefined, reason });
      await platformFinanceService.postRefund(created.id, idempotency());
      setMessage('تم تسجيل الاسترداد وقيده ماليًا'); setSourceId(''); setStudentId(''); setPlatformAmount(''); setTeacherAmount(''); setReason('');
    } catch { setError('تعذر تسجيل الاسترداد. تأكد من المصدر وطريقة الاسترداد.'); }
    finally { setSavingRefund(false); }
  }

  return <AdminPage activePath="/admin/platform-finance/operations" sectionLabel="الحسابات" pageTitle="تسجيل مصروف" subtitle="اكتب المبلغ واتصرف في إيه، وحدد اتدفع ولا لسه.">
    <div className="mx-auto max-w-2xl space-y-6" dir="rtl">
      {message ? <div role="status" className="rounded-xl bg-[var(--admin-card-soft)] p-4 font-bold">{message}</div> : null}
      {error ? <div role="alert" className="rounded-xl border border-[var(--admin-border)] p-4 text-[var(--admin-danger)]">{error}{!bootstrap && <button type="button" className="admin-btn-ghost ms-3 min-h-11 px-3" onClick={() => setBootstrapAttempt(value => value + 1)}>إعادة المحاولة</button>}</div> : null}
      {!bootstrap ? !error && <div role="status" className="admin-panel rounded-xl p-8 text-center">جارٍ تحميل بيانات المصروف...</div> : <div className="space-y-6">
        <form onSubmit={submitExpense} className="admin-panel space-y-5 rounded-xl p-5 sm:p-6" aria-label="تسجيل مصروف">
          <fieldset disabled={savingExpense || !!pendingExpense} className="space-y-5 disabled:opacity-70">
            <label className="block text-sm font-bold">المبلغ بالجنيه<input className="admin-input mt-2 min-h-11 w-full" required type="number" min="0.01" step="0.01" placeholder="مثال: 250" value={amount} onChange={(e) => setAmount(e.target.value)} /></label>
            <label className="block text-sm font-bold">نوع المصروف<select className="admin-input mt-2 min-h-11 w-full" required value={categoryId} onChange={(e) => setCategoryId(e.target.value)}><option value="">اختار النوع</option>{bootstrap.categories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
            <label className="block text-sm font-bold">اتصرف في إيه؟<input className="admin-input mt-2 min-h-11 w-full" required placeholder="مثال: اشتراك الإنترنت لشهر سبتمبر" value={description} onChange={(e) => setDescription(e.target.value)} /></label>
            <fieldset><legend className="mb-2 text-sm font-bold">اتدفع؟</legend><div className="flex flex-wrap gap-5">
              <label className="flex min-h-11 items-center gap-2 text-sm"><input type="radio" name="expense-payment" checked={paidNow} onChange={() => setPaidNow(true)} />أيوه، اتدفع</label>
              <label className="flex min-h-11 items-center gap-2 text-sm"><input type="radio" name="expense-payment" checked={!paidNow} onChange={() => setPaidNow(false)} />لسه ما اتدفعش</label>
            </div></fieldset>
            {paidNow && <label className="block text-sm font-bold">اتدفع منين؟<select className="admin-input mt-2 min-h-11 w-full" required value={treasuryId} onChange={(e) => setTreasuryId(e.target.value)}><option value="">اختار المحفظة أو الخزنة</option>{bootstrap.treasuryAccounts.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
          </fieldset>
          {!paidNow && <p className="text-sm leading-6 text-[var(--admin-muted)]">هيدخل في مصاريف المنصّة، ويفضل مبلغ مطلوب دفعه لحد ما تسجّل الدفع.</p>}
          <button className="admin-btn-primary min-h-11 w-full" type="submit" disabled={savingExpense}>{savingExpense ? 'جارٍ الحفظ...' : pendingExpense ? 'إعادة محاولة التأكيد' : 'حفظ المصروف'}</button>
        </form>
        <nav aria-label="إجراءات أخرى" className="flex flex-wrap gap-5 text-sm font-bold"><Link className="inline-flex min-h-11 items-center underline underline-offset-4" href="/admin/platform-finance/expenses">مراجعة المصاريف</Link><Link className="inline-flex min-h-11 items-center underline underline-offset-4" href="/admin/platform-finance/refunds">إرجاع فلوس لطالب</Link></nav>
        <details className="border-t border-[var(--admin-border)] pt-4"><summary className="cursor-pointer py-3 text-sm font-bold">تسجيل مرتجع يدوي (متقدم)</summary>
        <form onSubmit={submitRefund} className="space-y-4 py-4"><div className="flex items-center gap-2 font-bold"><Link2 size={20} aria-hidden="true" /> استرداد طالب بأرقام العملية</div>
          <input className="admin-input w-full" required placeholder="رقم عملية الشراء" value={sourceId} onChange={(e) => setSourceId(e.target.value)} />
          <input className="admin-input w-full" required placeholder="رقم الطالب" value={studentId} onChange={(e) => setStudentId(e.target.value)} />
          <div className="grid grid-cols-2 gap-3"><input className="admin-input w-full" required type="number" min="0" step="0.01" placeholder="حصة المنصة" value={platformAmount} onChange={(e) => setPlatformAmount(e.target.value)} /><input className="admin-input w-full" type="number" min="0" step="0.01" placeholder="حصة المدرس" value={teacherAmount} onChange={(e) => setTeacherAmount(e.target.value)} /></div>
          <select className="admin-input w-full" value={refundMethod} onChange={(e) => setRefundMethod(e.target.value)}><option value="1">إرجاع لرصيد الطالب</option><option value="2">استرداد كاش</option></select>
          {refundMethod === '2' ? <select className="admin-input w-full" required value={refundTreasuryId} onChange={(e) => setRefundTreasuryId(e.target.value)}><option value="">اختر الخزينة</option>{bootstrap.treasuryAccounts.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select> : null}
          <textarea className="admin-input min-h-24 w-full" required placeholder="سبب الاسترداد" value={reason} onChange={(e) => setReason(e.target.value)} />
          <button className="admin-btn-primary min-h-11 w-full" type="submit" disabled={savingRefund}>{savingRefund ? 'جارٍ التسجيل...' : 'تسجيل الاسترداد'}</button>
        </form>
        </details>
      </div>}
    </div>
  </AdminPage>;
}
