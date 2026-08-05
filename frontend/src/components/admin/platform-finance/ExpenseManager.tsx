'use client';

import { useEffect, useState } from 'react';
import platformFinanceService, { PlatformExpenseRow } from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2 }).format(value)} ج.م`;

export default function ExpenseManager() {
  const [rows, setRows] = useState<PlatformExpenseRow[]>([]);
  const [error, setError] = useState('');
  const load = async () => { try { setRows(await platformFinanceService.getExpenses()); } catch { setError('تعذر تحميل المصروفات'); } };
  useEffect(() => { void load(); }, []);
  async function reverse(id: string) { const reason = window.prompt('سبب عكس المصروف؟'); if (!reason) return; try { await platformFinanceService.reverseExpense(id, reason); await load(); } catch { setError('تعذر عكس المصروف'); } }
  return <section className="admin-panel rounded-2xl p-6" dir="rtl"><div className="mb-4 flex items-center justify-between"><h2 className="text-lg font-black">مصروفات المنصة</h2><button className="admin-btn-ghost" type="button" onClick={() => void load()}>تحديث</button></div>{error ? <p role="alert" className="mb-3 text-rose-600">{error}</p> : null}<div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-right"><th>المستند</th><th>الوصف</th><th>التاريخ</th><th>المبلغ</th><th>المدفوع</th><th>الحالة</th><th /></tr></thead><tbody>{rows.map(row => <tr key={row.id} className="border-t border-[var(--admin-border)]"><td>{row.documentNumber}</td><td>{row.description}</td><td>{new Date(row.occurredAt).toLocaleDateString('ar-EG')}</td><td>{money(row.amount)}</td><td>{money(row.paid)}</td><td>{row.status === 5 ? 'معكوس' : row.status === 4 ? 'مدفوع' : row.status === 3 ? 'مدفوع جزئيًا' : row.status === 2 ? 'آجل' : 'مسودة'}</td><td>{row.status !== 5 && row.status !== 1 ? <button className="text-rose-600" type="button" onClick={() => void reverse(row.id)}>عكس</button> : null}</td></tr>)}</tbody></table>{rows.length === 0 ? <p className="py-8 text-center text-[var(--admin-muted)]">لا توجد مصروفات.</p> : null}</div></section>;
}
