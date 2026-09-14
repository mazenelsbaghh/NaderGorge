'use client';

import { FormEvent, useEffect, useState } from 'react';
import platformFinanceService, { FinanceBootstrap } from '@/services/platform-finance-service';

export default function TreasuryManager() {
  const [bootstrap, setBootstrap] = useState<FinanceBootstrap | null>(null);
  const [treasuryId, setTreasuryId] = useState('');
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  useEffect(() => { void platformFinanceService.bootstrap().then(data => { setBootstrap(data); setTreasuryId(data.treasuryAccounts[0]?.id ?? ''); }).catch(() => setError('تعذر تحميل الخزائن')); }, []);
  async function submit(event: FormEvent) { event.preventDefault(); try { setError(''); const result = await platformFinanceService.reconcile({ treasuryAccountId: treasuryId, asOfDate: new Date().toISOString(), countedOrStatementBalance: Number(amount), evidenceNote: note }); setMessage(`تمت المطابقة. الفرق ${Number(result.variance ?? 0).toFixed(2)} ج.م`); } catch { setError('تعذر تسجيل المطابقة'); } }
  return <section className="admin-panel rounded-2xl p-6" dir="rtl"><h2 className="mb-4 text-lg font-black">مطابقة الخزائن</h2>{message ? <p className="mb-3 text-emerald-600">{message}</p> : null}{error ? <p className="mb-3 text-rose-600">{error}</p> : null}<form className="grid gap-3 md:grid-cols-3" onSubmit={submit}><select className="admin-input" required value={treasuryId} onChange={e => setTreasuryId(e.target.value)}><option value="">اختر الخزينة</option>{bootstrap?.treasuryAccounts.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select><input className="admin-input" required type="number" step="0.01" placeholder="الرصيد الفعلي" value={amount} onChange={e => setAmount(e.target.value)} /><input className="admin-input" required placeholder="ملاحظة الإثبات" value={note} onChange={e => setNote(e.target.value)} /><button className="admin-btn-primary md:col-span-3" type="submit">حفظ المطابقة</button></form></section>;
}
