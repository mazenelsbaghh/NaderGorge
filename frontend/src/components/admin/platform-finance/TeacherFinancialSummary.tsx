'use client';

import { useEffect, useState } from 'react';
import platformFinanceService, { FinanceTeacherSummary } from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG-u-nu-latn', { minimumFractionDigits: 2 }).format(value)} ج.م`;
export default function TeacherFinancialSummary({ teacherId }: { teacherId?: string }) {
  const [rows, setRows] = useState<FinanceTeacherSummary[]>([]);
  const [error, setError] = useState('');
  useEffect(() => { void (teacherId ? platformFinanceService.getTeacherDetail(teacherId) : platformFinanceService.getTeacherSummary()).then(result => setRows(Array.isArray(result) ? result : [result])).catch(() => setError('تعذر تحميل ملخص المدرسين')); }, [teacherId]);
  return <section className="admin-panel rounded-2xl p-6" dir="rtl"><h2 className="mb-4 text-lg font-black">أرصدة المدرسين</h2>{error ? <p className="text-rose-600">{error}</p> : null}<div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-right"><th>المدرس</th><th>إجمالي المبيعات</th><th>حصة المدرس</th><th>المدفوع</th><th>المتبقي</th></tr></thead><tbody>{rows.map(row => <tr key={row.teacherId} className="border-t border-[var(--admin-border)]"><td>{row.teacherName}</td><td>{money(row.grossSales)}</td><td>{money(row.teacherShare)}</td><td>{money(row.paid)}</td><td className="font-bold">{money(row.outstanding)}</td></tr>)}</tbody></table></div></section>;
}
