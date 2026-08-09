'use client';

import { useState } from 'react';
import { AlertTriangle, Database, RefreshCw } from 'lucide-react';
import { AdminPage } from '@/components/admin';
import platformFinanceService from '@/services/platform-finance-service';

const money = (value: number) => `${new Intl.NumberFormat('ar-EG', { minimumFractionDigits: 2 }).format(value)} ج.م`;

export default function PlatformFinanceMigration() {
  const today = new Date().toISOString().slice(0, 10);
  const [from, setFrom] = useState('2000-01-01');
  const [to, setTo] = useState(today);
  const [preview, setPreview] = useState<Awaited<ReturnType<typeof platformFinanceService.migrationPreview>> | null>(null);
  const [result, setResult] = useState<Awaited<ReturnType<typeof platformFinanceService.postMigration>> | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  async function runPreview() { setLoading(true); setError(''); setResult(null); try { setPreview(await platformFinanceService.migrationPreview(from, to)); } catch { setError('تعذر تشغيل المعاينة. تأكد من صلاحية إعادة البناء التاريخية.'); } finally { setLoading(false); } }
  async function post() { setLoading(true); setError(''); try { setResult(await platformFinanceService.postMigration(from, to)); } catch { setError('تعذر نشر الحركات التاريخية. لم يتم تخمين السجلات الغامضة.'); } finally { setLoading(false); } }

  return <AdminPage activePath="/admin/platform-finance/migration" sectionLabel="المالية" pageTitle="إعادة البناء التاريخية" subtitle="إعادة بناء كل المصادر المالية الموثقة منذ بداية المنصة؛ إعادة التشغيل لا تنشئ قيودًا مكررة.">
    <div className="space-y-6" dir="rtl">
      <div className="admin-panel flex flex-wrap items-end gap-3 rounded-2xl p-5"><label className="text-sm font-bold">من<input className="admin-input mt-2 block" type="date" value={from} onChange={(event) => setFrom(event.target.value)} /></label><label className="text-sm font-bold">إلى<input className="admin-input mt-2 block" type="date" value={to} onChange={(event) => setTo(event.target.value)} /></label><button className="admin-btn-primary inline-flex items-center gap-2" type="button" onClick={() => void runPreview()} disabled={loading}><RefreshCw size={16} className={loading ? 'animate-spin' : ''} /> معاينة</button></div>
      {error ? <div role="alert" className="rounded-2xl border border-rose-200 bg-rose-50 p-4 font-bold text-rose-700">{error}</div> : null}
      <section className="admin-panel rounded-2xl p-6"><div className="mb-5 flex items-center gap-2 text-lg font-black"><Database size={20} /> الحركات القابلة لإعادة البناء</div>{preview ? <><div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3"><div className="rounded-xl bg-[var(--admin-surface-muted)] p-4"><p className="text-sm text-[var(--admin-muted)]">الشحنات المطابقة والمقبولة</p><b>{preview.rechargeCandidates} — {money(preview.rechargeAmount)}</b></div><div className="rounded-xl bg-[var(--admin-surface-muted)] p-4"><p className="text-sm text-[var(--admin-muted)]">المبيعات الموثقة</p><b>{preview.saleCandidates} — {money(preview.saleAmount)}</b></div><div className="rounded-xl bg-[var(--admin-surface-muted)] p-4"><p className="text-sm text-[var(--admin-muted)]">تسويات الرصيد القديمة</p><b>{preview.balanceAdjustmentCandidates} — {money(preview.balanceAdjustmentAmount)}</b></div><div className="rounded-xl bg-[var(--admin-surface-muted)] p-4"><p className="text-sm text-[var(--admin-muted)]">مدفوعات المدرسين</p><b>{preview.teacherPayoutCandidates} — {money(preview.teacherPayoutAmount)}</b></div><div className="rounded-xl bg-[var(--admin-surface-muted)] p-4"><p className="text-sm text-[var(--admin-muted)]">الرواتب المعتمدة</p><b>{preview.payrollCandidates} — {money(preview.payrollAmount)}</b></div><div className="rounded-xl bg-[var(--admin-surface-muted)] p-4"><p className="text-sm text-[var(--admin-muted)]">أدلة للمطابقة فقط</p><b>{preview.ambiguousCandidates}</b></div></div><div className="mt-5 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm font-semibold text-amber-800"><AlertTriangle size={18} className="mt-0.5 shrink-0" /> أحداث المدرسين وحركات الشراء والشحن التابعة لها تُطابق مع المصدر الأصلي ولا تُقيد مرتين. كل قيد جديد له مفتاح منع تكرار ثابت.</div><button className="admin-btn-primary mt-5" type="button" onClick={() => void post()} disabled={loading}>قيد كل الحركات الموثقة</button></> : <p className="text-[var(--admin-muted)]">شغّل المعاينة أولًا.</p>}</section>
      {result ? <section className="admin-panel rounded-2xl p-6"><h2 className="mb-3 text-lg font-black">نتيجة الدفعة {result.batchId}</h2><p>تم قيد {result.posted}، موجود مسبقًا {result.alreadyPosted}، فشل {result.failed}.</p>{result.errors.length ? <ul className="mt-3 list-disc pe-5 text-rose-700">{result.errors.map((item) => <li key={item}>{item}</li>)}</ul> : null}</section> : null}
    </div>
  </AdminPage>;
}
