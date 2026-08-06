'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import { ArrowDownLeft, ArrowUpRight, RefreshCw } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import platformFinanceService, { FinanceJournal, PlatformFinanceDashboard, WalletFinanceReport } from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)} ج.م`;

function Metric({ label, value, tone = 'default' }: { label: string; value: number; tone?: 'default' | 'positive' | 'negative' }) {
  return <div className="admin-panel rounded-2xl p-5">
    <p className="text-sm font-semibold text-[var(--admin-muted)]">{label}</p>
    <p className={`mt-3 text-2xl font-black ${tone === 'positive' ? 'text-emerald-600' : tone === 'negative' ? 'text-rose-600' : 'text-[var(--admin-text)]'}`}>{money(value)}</p>
  </div>;
}

export default function PlatformFinanceCockpit() {
  const today = new Date();
  const monthStart = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10);
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today.toISOString().slice(0, 10));
  const [dashboard, setDashboard] = useState<PlatformFinanceDashboard | null>(null);
  const [ledger, setLedger] = useState<FinanceJournal[]>([]);
  const [walletReport, setWalletReport] = useState<WalletFinanceReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [nextDashboard, nextLedger, nextWalletReport] = await Promise.all([
        platformFinanceService.getDashboard(from, to),
        platformFinanceService.getLedger(from, to),
        platformFinanceService.getWalletReport(from, to),
      ]);
      setDashboard(nextDashboard);
      setLedger(nextLedger);
      setWalletReport(nextWalletReport);
    } catch {
      setError('تعذر تحميل بيانات المركز المالي. تأكد من الصلاحية ومن تطبيق migration المالية.');
    } finally {
      setLoading(false);
    }
  }, [from, to]);

  useEffect(() => { void load(); }, [load]);
  const accounts = useMemo(() => dashboard?.accounts ?? [], [dashboard]);

  return <AdminPage activePath="/admin/platform-finance" sectionLabel="المالية" pageTitle="المركز المالي العام" subtitle="الخزينة، أرصدة الطلبة، مستحقات المدرسين، الإيرادات والمصروفات في مكان واحد.">
    <div className="space-y-6" dir="rtl">
      <div className="admin-panel flex flex-wrap items-end gap-3 rounded-2xl p-4">
        <label className="text-sm font-bold">من<input className="admin-input mt-2 block" type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></label>
        <label className="text-sm font-bold">إلى<input className="admin-input mt-2 block" type="date" value={to} onChange={(event) => setTo(event.target.value)} /></label>
        <button type="button" className="admin-btn-primary inline-flex items-center gap-2" onClick={() => void load()} disabled={loading}><RefreshCw size={16} className={loading ? 'animate-spin' : ''} /> تحديث</button>
        <Link className="admin-btn-ghost" href="/admin/platform-finance/operations">المصروفات والاستردادات</Link>
        <Link className="admin-btn-ghost" href="/admin/platform-finance/planning">الميزانيات والخزائن</Link>
        <Link className="admin-btn-ghost" href="/admin/platform-finance/migration">إعادة بناء التاريخ</Link>
        <a className="admin-btn-ghost" href={`/api/admin/platform-finance/exports/xlsx?from=${from}&to=${to}`}>Excel</a>
        <a className="admin-btn-ghost" href={`/api/admin/platform-finance/exports/pdf?from=${from}&to=${to}`}>PDF</a>
      </div>
      {error ? <div role="alert" className="rounded-2xl border border-rose-200 bg-rose-50 p-4 font-bold text-rose-700">{error}</div> : null}
      {loading && !dashboard ? <div className="admin-panel rounded-2xl p-8 text-center font-bold">جاري تحميل المركز المالي…</div> : null}
      {dashboard ? <>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Metric label="النقدية والمحافظ" value={dashboard.cash} />
          <Metric label="أرصدة طلبة عامة" value={dashboard.generalStudentLiability} />
          <Metric label="مستحقات المدرسين" value={dashboard.teacherPayable} tone="negative" />
          <Metric label="صافي الربح" value={dashboard.netProfit} tone={dashboard.netProfit >= 0 ? 'positive' : 'negative'} />
        </div>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Metric label="أرصدة مرتبطة بمدرس" value={dashboard.teacherStudentLiability} />
          <Metric label="مستحقات الموردين" value={dashboard.supplierPayable} tone="negative" />
          <Metric label="إيرادات الشراء" value={dashboard.revenue} tone="positive" />
          <Metric label="الاستردادات + المصروفات" value={dashboard.refunds + dashboard.expenses} tone="negative" />
        </div>
        <section className="admin-panel rounded-2xl p-5"><h2 className="mb-4 text-lg font-black">تقارير المحافظ</h2><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{walletReport?.wallets.map(wallet => <div key={wallet.id} className="rounded-xl border border-[var(--admin-border)] p-4"><div className="flex items-center justify-between"><b>{wallet.label}</b><span className="text-xs text-[var(--admin-muted)]">{wallet.phoneNumber}</span></div><p className="mt-2 text-xl font-black">الرصيد: {money(wallet.currentBalance)}</p><div className="mt-3 grid grid-cols-2 gap-2 text-sm"><span>الوارد: <b className="text-emerald-600">{money(wallet.incoming)}</b></span><span>الصادر: <b className="text-rose-600">{money(wallet.outgoing)}</b></span><span>مصروفات: {money(wallet.expenses)}</span><span>تحويل داخلي: {money(wallet.internalTransfers)}</span><span>المعاملات: {wallet.transactions}</span></div></div>)}</div></section>
        <section className="admin-panel rounded-2xl p-5"><h2 className="mb-4 text-lg font-black">شحن رصيد المدرسين من المحافظ</h2><div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">{walletReport?.teacherRechargeCards.map(card => <div key={`${card.walletId}-${card.teacherName}`} className="rounded-xl border border-[var(--admin-border)] p-4"><p className="font-black">{card.teacherName}</p><p className="mt-2 text-lg font-black text-emerald-600">{money(card.amount)}</p><p className="text-sm text-[var(--admin-muted)]">{card.count} عملية شحن</p></div>) ?? <p className="text-[var(--admin-muted)]">لا توجد شحنات رصيد مدرس في الفترة.</p>}</div></section>
        <div className="grid gap-6 lg:grid-cols-[1fr_1.4fr]">
          <section className="admin-panel rounded-2xl p-5"><h2 className="mb-4 text-lg font-black">أرصدة الحسابات</h2><div className="space-y-3">{accounts.map((account) => <div key={account.accountId} className="flex items-center justify-between border-b border-[var(--admin-border)] pb-3"><span><b>{account.code}</b> <span className="text-sm text-[var(--admin-muted)]">{account.name}</span></span><b>{money(account.balance)}</b></div>)}</div></section>
          <section className="admin-panel rounded-2xl p-5"><h2 className="mb-4 text-lg font-black">آخر القيود</h2><div className="space-y-3">{ledger.length === 0 ? <p className="text-[var(--admin-muted)]">لا توجد قيود في الفترة.</p> : ledger.map((entry) => <details key={entry.id} className="rounded-xl border border-[var(--admin-border)] p-3"><summary className="cursor-pointer list-none"><div className="flex items-center justify-between gap-3"><span><b>#{entry.sequenceNumber}</b> {entry.description}</span><span className="text-xs text-[var(--admin-muted)]">{new Date(entry.occurredAt).toLocaleDateString('ar-EG')}</span></div></summary><div className="mt-3 space-y-2 text-sm">{entry.lines.map((line) => <div key={line.id} className="flex justify-between"><span>{line.accountCode} - {line.accountName}</span><span className="font-bold">{line.debit ? <><ArrowDownLeft size={14} className="inline text-rose-600" /> {money(line.debit)}</> : <><ArrowUpRight size={14} className="inline text-emerald-600" /> {money(line.credit)}</>}</span></div>)}</div></details>)}</div></section>
        </div>
      </> : null}
    </div>
  </AdminPage>;
}
