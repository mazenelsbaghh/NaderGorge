'use client';

import { useEffect, useRef, useState } from 'react';
import { Download, Loader2 } from 'lucide-react';
import { AdminModal } from '@/components/admin';
import { cairoCurrentDate } from '@/lib/cairo-time';
import { isFullAdmin } from '@/packages/admin/route-permissions';
import { financeService } from '@/services/finance-service';
import { useAuthStore } from '@/stores/auth-store';

export function TeacherReportDownload({ teacherId, teacherName }: { teacherId: string; teacherName: string }) {
  const allowed = useAuthStore(state => isFullAdmin(state.user));
  const [open, setOpen] = useState(false);
  const [period, setPeriod] = useState('beginning');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState(cairoCurrentDate);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const request = useRef<AbortController | null>(null);
  useEffect(() => () => request.current?.abort(), []);

  const close = () => {
    request.current?.abort();
    setOpen(false);
    setError('');
  };

  const download = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (busy || !allowed) return;
    const beginning = period === 'beginning' ? undefined : from;
    if (!to || (period === 'custom' && !from) || (beginning && beginning > to) || to > cairoCurrentDate()) {
      setError('اختار فترة صحيحة. البداية لازم تكون قبل النهاية أو نفس اليوم.');
      return;
    }
    setError('');
    setBusy(true);
    const controller = new AbortController();
    request.current = controller;
    try {
      const pdf = await financeService.exportTeacherDetailedReport(teacherId, { from: beginning, to }, controller.signal);
      if (controller.signal.aborted) return;
      const url = URL.createObjectURL(pdf);
      const link = document.createElement('a');
      link.href = url;
      link.download = `حساب-${teacherName.replace(/[\\/:*?"<>|]/g, '-')}-${beginning ?? 'من-البداية'}-${to}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      setOpen(false);
    } catch {
      if (!controller.signal.aborted) setError('تعذر تنزيل الكشف. جرّب تاني.');
    } finally {
      setBusy(false);
      request.current = null;
    }
  };

  if (!allowed) return null;
  return <>
    <button type="button" onClick={() => { setError(''); setOpen(true); }} disabled={busy}
      className="admin-btn-primary inline-flex min-h-11 items-center gap-2 px-4 disabled:opacity-50">
      <Download size={16} aria-hidden="true" />تنزيل كشف الحساب
    </button>
    <AdminModal open={open} onClose={close} title={`كشف حساب ${teacherName}`} subtitle="اختار المدة ونزّل التفاصيل كاملة في PDF بهوية مسار.">
      <form onSubmit={download} className="space-y-5" aria-label="مدة كشف الحساب">
        <label className="block space-y-2 text-sm font-bold">المدة
          <select aria-label="المدة" value={period} disabled={busy} onChange={event => { setPeriod(event.target.value); setError(''); }}
            className="block min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3">
            <option value="beginning">من بداية الحساب</option><option value="custom">مدة محددة</option>
          </select>
        </label>
        <div className="grid gap-4 sm:grid-cols-2">
          {period === 'custom' && <label className="block space-y-2 text-sm font-bold">من يوم
            <input type="date" required value={from} max={to || cairoCurrentDate()} disabled={busy}
              onChange={event => { setFrom(event.target.value); setError(''); }}
              className="block min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" />
          </label>}
          <label className="block space-y-2 text-sm font-bold">لحد يوم
            <input type="date" required value={to} min={period === 'custom' ? from : undefined} max={cairoCurrentDate()} disabled={busy}
              onChange={event => { setTo(event.target.value); setError(''); }}
              className="block min-h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3" />
          </label>
        </div>
        <p className="text-sm leading-7 text-[var(--admin-muted)]">الكشف فيه الكورسات والحصص والشهور والترم والسنة، ملخص الحساب، وكل تفاصيل الطلاب والشراء والاستردادات والشحن والهدايا. اليوم الأخير محسوب كامل بتوقيت القاهرة.</p>
        {error && <p role="alert" className="text-sm font-bold text-red-700 dark:text-red-300">{error}</p>}
        {busy && <p role="status" className="text-sm text-[var(--admin-muted)]">بنجهّز الكشف بكل التفاصيل، استنى شوية...</p>}
        <div className="flex flex-wrap gap-3 border-t border-[var(--admin-border)] pt-4">
          <button type="submit" disabled={busy} className="admin-btn-primary inline-flex min-h-11 items-center gap-2 px-5 disabled:opacity-50">
            {busy ? <Loader2 size={16} className="animate-spin" aria-hidden="true" /> : <Download size={16} aria-hidden="true" />}
            {busy ? 'جارٍ تجهيز الملف...' : 'تنزيل PDF'}
          </button>
          <button type="button" onClick={close} className="admin-btn-ghost min-h-11 px-4">{busy ? 'إلغاء التنزيل' : 'إغلاق'}</button>
        </div>
      </form>
    </AdminModal>
  </>;
}
