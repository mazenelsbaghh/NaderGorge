'use client';

import { useEffect, useState } from 'react';
import platformFinanceService, { PlatformRefundRow } from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2 }).format(value)} ج.م`;

export default function RefundManager() {
  const [rows, setRows] = useState<PlatformRefundRow[]>([]);
  const [error, setError] = useState('');
  const load = async () => { try { setRows(await platformFinanceService.getRefunds()); } catch { setError('تعذر تحميل الاستردادات'); } };
  useEffect(() => { void load(); }, []);
  async function reverse(id: string) { const reason = window.prompt('سبب عكس الاسترداد؟'); if (!reason) return; try { await platformFinanceService.reverseRefund(id, reason); await load(); } catch { setError('تعذر عكس الاسترداد'); } }
  return <section className="admin-panel rounded-2xl p-6" dir="rtl"><div className="mb-4 flex items-center justify-between"><h2 className="text-lg font-black">سجل الاستردادات</h2><button className="admin-btn-ghost" type="button" onClick={() => void load()}>تحديث</button></div>{error ? <p role="alert" className="mb-3 text-rose-600">{error}</p> : null}<div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-right"><th>عملية الشراء</th><th>الطريقة</th><th>حصة المنصة</th><th>حصة المدرس</th><th>الإجمالي</th><th>الحالة</th><th /></tr></thead><tbody>{rows.map(row => <tr key={row.id} className="border-t border-[var(--admin-border)]"><td>{row.originalSourceId}</td><td>{row.method === 2 ? 'كاش' : 'رصيد طالب'}</td><td>{money(row.platformAmount)}</td><td>{money(row.teacherAmount)}</td><td>{money(row.totalAmount)}</td><td>{row.status === 3 ? 'معكوس' : row.status === 2 ? 'مقيد' : 'مسودة'}</td><td>{row.status === 2 ? <button className="text-rose-600" type="button" onClick={() => void reverse(row.id)}>عكس</button> : null}</td></tr>)}</tbody></table>{rows.length === 0 ? <p className="py-8 text-center text-[var(--admin-muted)]">لا توجد استردادات.</p> : null}</div></section>;
}
