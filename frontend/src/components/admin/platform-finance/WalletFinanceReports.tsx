'use client';

import { useState } from 'react';
import platformFinanceService, { WalletFinanceReport } from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG-u-nu-latn', { minimumFractionDigits: 2 }).format(value)} ج.م`;

export default function WalletFinanceReports() {
  const today = new Date().toISOString().slice(0, 10);
  const [from, setFrom] = useState(`${today.slice(0, 8)}01`);
  const [to, setTo] = useState(today);
  const [report, setReport] = useState<WalletFinanceReport | null>(null);
  const [error, setError] = useState('');

  async function load() {
    try { setError(''); setReport(await platformFinanceService.getWalletReport(from, to)); }
    catch { setError('تعذر تحميل تقارير المحافظ'); }
  }

  return <section className="space-y-5" dir="rtl"><div className="admin-panel flex flex-wrap items-end gap-3 rounded-2xl p-5"><label>من<input className="admin-input mt-2 block" type="date" value={from} onChange={event => setFrom(event.target.value)} /></label><label>إلى<input className="admin-input mt-2 block" type="date" value={to} onChange={event => setTo(event.target.value)} /></label><button className="admin-btn-primary" type="button" onClick={() => void load()}>عرض تقارير المحافظ</button></div>{error ? <p role="alert" className="text-rose-600">{error}</p> : null}{report ? <><div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{report.wallets.map(wallet => <article key={wallet.id} className="admin-panel rounded-2xl p-5"><b>{wallet.label}</b><p className="mt-2 text-xl font-black">{money(wallet.currentBalance)}</p><p className="text-sm text-[var(--admin-muted)]">الرصيد الحالي · {wallet.phoneNumber}</p><dl className="mt-4 grid grid-cols-2 gap-3 text-sm"><div><dt>الوارد</dt><dd className="font-black text-emerald-600">{money(wallet.incoming)}</dd></div><div><dt>الصادر</dt><dd className="font-black text-rose-600">{money(wallet.outgoing)}</dd></div><div><dt>المصروفات</dt><dd className="font-black">{money(wallet.expenses)}</dd></div><div><dt>التحويلات الداخلية</dt><dd className="font-black">{money(wallet.internalTransfers)}</dd></div><div><dt>إجمالي المعاملات</dt><dd className="font-black">{wallet.transactions}</dd></div></dl></article>)}</div><div className="admin-panel rounded-2xl p-5"><h2 className="mb-4 text-lg font-black">شحن رصيد المدرسين من المحافظ</h2><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{report.teacherRechargeCards.map(card => <div key={`${card.walletId}-${card.teacherName}`} className="rounded-xl border border-[var(--admin-border)] p-4"><b>{card.teacherName}</b><p className="mt-2 text-lg font-black text-emerald-600">{money(card.amount)}</p><p className="text-sm text-[var(--admin-muted)]">{card.count} عملية شحن</p></div>)}</div></div></> : null}</section>;
}
