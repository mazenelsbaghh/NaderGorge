'use client';

import { FormEvent, useEffect, useState } from 'react';
import { ArrowLeftRight, Calculator, RefreshCw } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import platformFinanceService, { FinanceBootstrap } from '@/services/platform-finance-service';

const idempotency = () => `${Date.now()}-${Math.random().toString(36).slice(2)}`;
const money = (value: number) => `${new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2 }).format(value)} ج.م`;

export default function PlatformFinancePlanning() {
  const today = new Date().toISOString().slice(0, 10);
  const [bootstrap, setBootstrap] = useState<FinanceBootstrap | null>(null);
  const [name, setName] = useState('ميزانية شهرية');
  const [from, setFrom] = useState(today.slice(0, 8) + '01');
  const [to, setTo] = useState(today);
  const [accountId, setAccountId] = useState('');
  const [plannedAmount, setPlannedAmount] = useState('');
  const [source, setSource] = useState('');
  const [destination, setDestination] = useState('');
  const [transferAmount, setTransferAmount] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [actuals, setActuals] = useState<Array<{ code: string; name: string; actual: number }>>([]);

  useEffect(() => { void platformFinanceService.bootstrap().then((data) => { setBootstrap(data); setAccountId(data.accounts.find((item) => item.code === '5000')?.id ?? data.accounts[0]?.id ?? ''); }).catch(() => setError('تعذر تحميل إعدادات التخطيط المالي')); }, []);

  async function submitBudget(event: FormEvent) {
    event.preventDefault(); setMessage(''); setError('');
    try { await platformFinanceService.createBudget({ name, periodKind: 2, startDate: from, endDate: to, lines: [{ financialAccountId: accountId, plannedAmount: Number(plannedAmount) }] }); setMessage('تم حفظ الميزانية'); setPlannedAmount(''); }
    catch { setError('تعذر حفظ الميزانية. راجع الحساب والمبلغ.'); }
  }

  async function submitTransfer(event: FormEvent) {
    event.preventDefault(); setMessage(''); setError('');
    try { await platformFinanceService.transfer({ sourceTreasuryAccountId: source, destinationTreasuryAccountId: destination, amount: Number(transferAmount), reference: 'تحويل داخلي', idempotencyKey: idempotency() }); setMessage('تم تسجيل التحويل بين الخزائن'); setTransferAmount(''); }
    catch { setError('تعذر تسجيل التحويل. تأكد من اختلاف الخزانتين.'); }
  }

  async function loadActuals() { setError(''); try { setActuals(await platformFinanceService.getBudgetActuals(from, to)); } catch { setError('تعذر تحميل المصروف الفعلي'); } }

  return <AdminPage activePath="/admin/platform-finance" sectionLabel="المالية" pageTitle="الميزانيات والخزائن" subtitle="خطط أسبوعية أو شهرية ومطابقة حركة الخزائن بالجنيه المصري.">
    <div className="space-y-6" dir="rtl">
      {message ? <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-4 font-bold text-emerald-700">{message}</div> : null}
      {error ? <div role="alert" className="rounded-2xl border border-rose-200 bg-rose-50 p-4 font-bold text-rose-700">{error}</div> : null}
      {!bootstrap ? <div className="admin-panel rounded-2xl p-8 text-center"><RefreshCw className="mx-auto animate-spin" /></div> : <>
        <div className="grid gap-6 lg:grid-cols-2">
          <form onSubmit={submitBudget} className="admin-panel space-y-4 rounded-2xl p-6"><div className="flex items-center gap-2 text-lg font-black"><Calculator size={20} /> خطة ميزانية</div>
            <input className="admin-input w-full" required value={name} onChange={(event) => setName(event.target.value)} placeholder="اسم الخطة" />
            <div className="grid grid-cols-2 gap-3"><input className="admin-input" type="date" value={from} onChange={(event) => setFrom(event.target.value)} /><input className="admin-input" type="date" value={to} onChange={(event) => setTo(event.target.value)} /></div>
            <select className="admin-input w-full" required value={accountId} onChange={(event) => setAccountId(event.target.value)}>{bootstrap.accounts.map((account) => <option key={account.id} value={account.id}>{account.code} - {account.name}</option>)}</select>
            <input className="admin-input w-full" required type="number" min="0" step="0.01" value={plannedAmount} onChange={(event) => setPlannedAmount(event.target.value)} placeholder="المبلغ المخطط" />
            <button className="admin-btn-primary w-full" type="submit">حفظ الميزانية</button>
          </form>
          <form onSubmit={submitTransfer} className="admin-panel space-y-4 rounded-2xl p-6"><div className="flex items-center gap-2 text-lg font-black"><ArrowLeftRight size={20} /> تحويل بين الخزائن</div>
            <select className="admin-input w-full" required value={source} onChange={(event) => setSource(event.target.value)}><option value="">من خزينة</option>{bootstrap.treasuryAccounts.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select>
            <select className="admin-input w-full" required value={destination} onChange={(event) => setDestination(event.target.value)}><option value="">إلى خزينة</option>{bootstrap.treasuryAccounts.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select>
            <input className="admin-input w-full" required type="number" min="0.01" step="0.01" value={transferAmount} onChange={(event) => setTransferAmount(event.target.value)} placeholder="المبلغ" />
            <button className="admin-btn-primary w-full" type="submit">تسجيل التحويل</button>
          </form>
        </div>
        <section className="admin-panel rounded-2xl p-6"><div className="mb-4 flex items-center justify-between"><h2 className="text-lg font-black">الفعلي من القيود</h2><button className="admin-btn-ghost" type="button" onClick={() => void loadActuals()}>تحديث</button></div><div className="space-y-3">{actuals.length === 0 ? <p className="text-[var(--admin-muted)]">اضغط تحديث لعرض الفعلي.</p> : actuals.map((item) => <div key={item.code} className="flex justify-between border-b border-[var(--admin-border)] pb-2"><span>{item.code} - {item.name}</span><b>{money(item.actual)}</b></div>)}</div></section>
      </>}
    </div>
  </AdminPage>;
}
