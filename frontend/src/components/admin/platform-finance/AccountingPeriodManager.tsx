'use client';

import { useEffect, useState } from 'react';
import apiClient from '@/services/api-client';

type Period = { id: string; startDate: string; endDate: string; status: number; closeReason?: string | null };
export default function AccountingPeriodManager() {
  const [periods, setPeriods] = useState<Period[]>([]);
  const [error, setError] = useState('');
  const load = async () => { try { setPeriods((await apiClient.get<Period[]>('/admin/platform-finance/periods')).data); } catch { setError('تعذر تحميل الفترات'); } };
  useEffect(() => { void load(); }, []);
  async function mutate(period: Period) { const reason = window.prompt('السبب؟'); if (!reason) return; try { await apiClient.post(`/admin/platform-finance/periods/${period.id}/${period.status === 2 ? 'reopen' : 'close'}`, { reason }); await load(); } catch { setError('تعذر تحديث الفترة'); } }
  return <section className="admin-panel rounded-2xl p-6" dir="rtl"><h2 className="mb-4 text-lg font-black">إغلاق الفترات المحاسبية</h2>{error ? <p className="mb-3 text-rose-600">{error}</p> : null}<div className="space-y-3">{periods.map(period => <div key={period.id} className="flex items-center justify-between border-b border-[var(--admin-border)] pb-3"><span>{new Date(period.startDate).toLocaleDateString('ar-EG')} — {new Date(period.endDate).toLocaleDateString('ar-EG')}</span><span className="flex items-center gap-3"><b>{period.status === 2 ? 'مغلقة' : period.status === 3 ? 'أعيد فتحها' : 'مفتوحة'}</b><button className="admin-btn-ghost" type="button" onClick={() => void mutate(period)}>{period.status === 2 ? 'إعادة فتح' : 'إغلاق'}</button></span></div>)}</div></section>;
}
