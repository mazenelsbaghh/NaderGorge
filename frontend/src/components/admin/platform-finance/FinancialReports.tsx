'use client';

import { useState } from 'react';
import platformFinanceService, { PlatformFinancialReport } from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2 }).format(value)} ج.م`;
export default function FinancialReports() {
  const today = new Date().toISOString().slice(0, 10);
  const [from, setFrom] = useState(`${today.slice(0, 8)}01`);
  const [to, setTo] = useState(today);
  const [kind, setKind] = useState('summary');
  const [report, setReport] = useState<PlatformFinancialReport | null>(null);
  const [error, setError] = useState('');
  async function load() { try { setError(''); setReport(await platformFinanceService.getReport(kind, from, to)); } catch { setError('تعذر تحميل التقرير'); } }
  return <section className="space-y-5" dir="rtl"><div className="admin-panel flex flex-wrap items-end gap-3 rounded-2xl p-5"><label>من<input className="admin-input mt-2 block" type="date" value={from} onChange={e => setFrom(e.target.value)} /></label><label>إلى<input className="admin-input mt-2 block" type="date" value={to} onChange={e => setTo(e.target.value)} /></label><select className="admin-input" value={kind} onChange={e => setKind(e.target.value)}><option value="summary">ملخص</option><option value="profit-loss">الأرباح والخسائر</option><option value="cash-flow">التدفقات النقدية</option><option value="financial-position">المركز المالي</option><option value="expenses">المصروفات</option><option value="refunds">الاستردادات</option></select><button className="admin-btn-primary" type="button" onClick={() => void load()}>عرض التقرير</button></div>{error ? <p role="alert" className="text-rose-600">{error}</p> : null}{report ? <div className="admin-panel rounded-2xl p-6"><div className="mb-5 grid gap-3 sm:grid-cols-3"><div><span className="text-sm text-[var(--admin-muted)]">إجمالي المدين</span><b className="block">{money(report.totalDebit)}</b></div><div><span className="text-sm text-[var(--admin-muted)]">إجمالي الدائن</span><b className="block">{money(report.totalCredit)}</b></div><div><span className="text-sm text-[var(--admin-muted)]">الفرق</span><b className="block">{money(report.totalDebit - report.totalCredit)}</b></div></div><div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-right"><th>الحساب</th><th>الاسم</th><th>مدين</th><th>دائن</th><th>الرصيد</th></tr></thead><tbody>{report.rows.map(row => <tr key={row.code} className="border-t border-[var(--admin-border)]"><td>{row.code}</td><td>{row.name}</td><td>{money(row.debit)}</td><td>{money(row.credit)}</td><td>{money(row.balance)}</td></tr>)}</tbody></table></div></div> : null}</section>;
}
