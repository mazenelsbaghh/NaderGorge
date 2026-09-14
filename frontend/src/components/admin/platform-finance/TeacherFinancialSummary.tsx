'use client';

import { useEffect, useState } from 'react';
import platformFinanceService, { FinanceTeacherSummary } from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG-u-nu-latn', { minimumFractionDigits: 2 }).format(value)} ج.م`;
export default function TeacherFinancialSummary({ teacherId }: { teacherId?: string }) {
  const [rows, setRows] = useState<FinanceTeacherSummary[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    let active = true;
    setLoading(true);
    setRows([]);
    setError('');
    void (teacherId ? platformFinanceService.getTeacherDetail(teacherId) : platformFinanceService.getTeacherSummary())
      .then(result => { if (active) setRows(Array.isArray(result) ? result : [result]); })
      .catch(() => { if (active) setError('تعذر تحميل ملخص المدرسين'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [teacherId]);
  return <section className="admin-panel rounded-2xl p-6" dir="rtl"><h2 className="mb-4 text-lg font-black">أرصدة المدرسين</h2><p className="mb-4 text-sm text-[var(--admin-muted)]">حركة آخر شهر حتى اليوم؛ المتبقي يشمل رصيد بداية الفترة. المبيعات بعد الخصومات وقبل المرتجعات، والحصص بعد المرتجعات.</p>{loading ? <p role="status">جارٍ تحميل الحسابات...</p> : null}{error ? <p className="text-rose-600">{error}</p> : null}<div className="overflow-x-auto"><table className="w-full text-sm"><thead><tr className="text-right"><th>المدرس</th><th>المبيعات قبل المرتجعات</th><th>حصة المنصة</th><th>حصة المدرس</th><th>المرتجعات</th><th>المدفوع</th><th>رصيد نهاية الفترة</th></tr></thead><tbody>{rows.map(row => <tr key={row.teacherId} className="border-t border-[var(--admin-border)]"><td>{row.teacherName}</td><td>{money(row.grossSales)}</td><td>{money(row.platformShare)}</td><td>{money(row.teacherShare)}</td><td>{money(row.refunds)}</td><td>{money(row.paid)}</td><td className="font-bold">{money(row.outstanding)}</td></tr>)}</tbody></table></div></section>;
}
