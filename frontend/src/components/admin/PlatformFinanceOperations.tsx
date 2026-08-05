'use client';

import { FormEvent, useEffect, useState } from 'react';
import { Link2, Receipt, RefreshCw } from 'lucide-react';
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
  const [sourceId, setSourceId] = useState('');
  const [studentId, setStudentId] = useState('');
  const [platformAmount, setPlatformAmount] = useState('');
  const [teacherAmount, setTeacherAmount] = useState('');
  const [refundMethod, setRefundMethod] = useState('1');
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  useEffect(() => { void platformFinanceService.bootstrap().then(setBootstrap).catch(() => setError('تعذر تحميل إعدادات المركز المالي')); }, []);

  async function submitExpense(event: FormEvent) {
    event.preventDefault(); setError(''); setMessage('');
    try {
      const created = await platformFinanceService.createExpense({ amount: Number(amount), occurredAt: new Date().toISOString(), categoryId, description });
      if (paidNow) await platformFinanceService.postExpense(created.expense.id, { treasuryAccountId: treasuryId, idempotencyKey: idempotency() });
      setMessage('تم تسجيل مصروف المنصة بنجاح'); setAmount(''); setDescription('');
    } catch { setError('تعذر تسجيل المصروف. راجع البيانات والصلاحيات.'); }
  }

  async function submitRefund(event: FormEvent) {
    event.preventDefault(); setError(''); setMessage('');
    try {
      const created = await platformFinanceService.createRefund({ originalSourceId: sourceId, originalSourceType: 'Purchase', studentId, platformAmount: Number(platformAmount), teacherAmount: Number(teacherAmount || 0), method: Number(refundMethod), treasuryAccountId: refundMethod === '2' ? treasuryId : undefined, reason });
      await platformFinanceService.postRefund(created.refund.id, idempotency());
      setMessage('تم تسجيل الاسترداد وقيده ماليًا'); setSourceId(''); setStudentId(''); setPlatformAmount(''); setTeacherAmount(''); setReason('');
    } catch { setError('تعذر تسجيل الاسترداد. تأكد من المصدر وطريقة الاسترداد.'); }
  }

  return <AdminPage activePath="/admin/platform-finance" sectionLabel="المالية" pageTitle="عمليات المركز المالي" subtitle="إضافة مصروفات المنصة وتسجيل الاستردادات كرصيد أو كاش.">
    <div className="space-y-6" dir="rtl">
      {message ? <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 font-bold text-emerald-700">{message}</div> : null}
      {error ? <div role="alert" className="rounded-2xl border border-rose-200 bg-rose-50 p-4 font-bold text-rose-700">{error}</div> : null}
      {!bootstrap ? <div className="admin-panel rounded-2xl p-8 text-center"><RefreshCw className="mx-auto animate-spin" /></div> : <div className="grid gap-6 lg:grid-cols-2">
        <form onSubmit={submitExpense} className="admin-panel space-y-4 rounded-2xl p-6"><div className="flex items-center gap-2 text-lg font-black"><Receipt size={20} /> مصروف منصة</div>
          <input className="admin-input w-full" required type="number" min="0.01" step="0.01" placeholder="المبلغ بالجنيه" value={amount} onChange={(e) => setAmount(e.target.value)} />
          <select className="admin-input w-full" required value={categoryId} onChange={(e) => setCategoryId(e.target.value)}><option value="">اختر التصنيف</option>{bootstrap.categories.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select>
          <input className="admin-input w-full" required placeholder="وصف المصروف" value={description} onChange={(e) => setDescription(e.target.value)} />
          <select className="admin-input w-full" value={treasuryId} onChange={(e) => setTreasuryId(e.target.value)}><option value="">مصروف آجل</option>{bootstrap.treasuryAccounts.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select>
          <label className="flex items-center gap-2 text-sm font-bold"><input type="checkbox" checked={paidNow} onChange={(e) => setPaidNow(e.target.checked)} /> دفع المصروف الآن من الخزينة</label>
          <button className="admin-btn-primary w-full" type="submit">حفظ وقيد المصروف</button>
        </form>
        <form onSubmit={submitRefund} className="admin-panel space-y-4 rounded-2xl p-6"><div className="flex items-center gap-2 text-lg font-black"><Link2 size={20} /> استرداد طالب</div>
          <input className="admin-input w-full" required placeholder="رقم عملية الشراء" value={sourceId} onChange={(e) => setSourceId(e.target.value)} />
          <input className="admin-input w-full" required placeholder="رقم الطالب" value={studentId} onChange={(e) => setStudentId(e.target.value)} />
          <div className="grid grid-cols-2 gap-3"><input className="admin-input w-full" required type="number" min="0" step="0.01" placeholder="حصة المنصة" value={platformAmount} onChange={(e) => setPlatformAmount(e.target.value)} /><input className="admin-input w-full" type="number" min="0" step="0.01" placeholder="حصة المدرس" value={teacherAmount} onChange={(e) => setTeacherAmount(e.target.value)} /></div>
          <select className="admin-input w-full" value={refundMethod} onChange={(e) => setRefundMethod(e.target.value)}><option value="1">إرجاع لرصيد الطالب</option><option value="2">استرداد كاش</option></select>
          {refundMethod === '2' ? <select className="admin-input w-full" required value={treasuryId} onChange={(e) => setTreasuryId(e.target.value)}><option value="">اختر الخزينة</option>{bootstrap.treasuryAccounts.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select> : null}
          <textarea className="admin-input min-h-24 w-full" required placeholder="سبب الاسترداد" value={reason} onChange={(e) => setReason(e.target.value)} />
          <button className="admin-btn-primary w-full" type="submit">تسجيل الاسترداد</button>
        </form>
      </div>}
    </div>
  </AdminPage>;
}
