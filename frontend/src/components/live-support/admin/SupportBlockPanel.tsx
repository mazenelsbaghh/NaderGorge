'use client';

import { useEffect, useState } from 'react';
import { liveSupportService, getLiveSupportApiError, type SupportBlockStatus } from '@/services/live-support-service';

const states: Record<string, string> = { Pending: 'في الانتظار', Processing: 'جارٍ التنفيذ', Succeeded: 'تم التنفيذ', Failed: 'تعذر التنفيذ', Superseded: 'استُبدل بطلب أحدث' };
const notices: Record<string, string> = { Pending: 'لم يُرسل بعد', Sent: 'تم إرسال السبب', Failed: 'تعذر إرسال السبب', Uncertain: 'لم يتأكد وصول السبب' };

export function SupportBlockPanel({ conversationId, onChanged }: { conversationId: string; onChanged?: () => void }) {
  const [status, setStatus] = useState<SupportBlockStatus>();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    let loading = false;
    async function refresh() {
      if (loading) return;
      loading = true;
      try { const current = await liveSupportService.getSupportBlock(conversationId, controller.signal); if (!controller.signal.aborted) setStatus(current); }
      catch (cause) { if (!controller.signal.aborted) setError(getLiveSupportApiError(cause, 'تعذر إتمام الطلب. حاول مجددًا.')); }
      finally { loading = false; }
    }
    void refresh();
    const interval = window.setInterval(() => void refresh(), 5000);
    return () => { controller.abort(); window.clearInterval(interval); };
  }, [conversationId]);
  const blocked = Boolean(status?.block && !status.block.unblockedAt);
  async function submit() {
    if (!status) return;
    setBusy(true); setError('');
    try {
      setStatus(await liveSupportService.setSupportBlock(conversationId, !blocked, reason, status.conversationVersion));
      setReason(''); onChanged?.();
    } catch (cause) { setError(getLiveSupportApiError(cause, 'تعذر إتمام الطلب. حاول مجددًا.')); }
    finally { setBusy(false); }
  }
  return <details className="border-b border-[var(--admin-border)] px-4 py-3 text-sm text-[var(--admin-text)]">
    <summary className="cursor-pointer font-bold">{blocked ? 'محظور من الدعم' : 'حظر التواصل مع الدعم'}</summary>
    <div className="mt-3 space-y-3">
      {blocked ? <p className="break-words">سبب الحظر: {status?.block?.reason}</p> : <label className="block">سبب الحظر الذي سيظهر للشخص
        <textarea maxLength={500} value={reason} onChange={event => setReason(event.target.value)} rows={2} className="mt-2 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] p-3" /></label>}
      <p className="text-xs leading-6 text-[var(--admin-muted)]">الحظر يمنع التواصل في الدعم. لحسابات واتساب المرتبطة، نحاول إرسال السبب أولًا ثم تنفيذ الحظر على واتساب.</p>
      <button type="button" disabled={!status || busy || (!blocked && !reason.trim())} onClick={() => void submit()} className="min-h-11 rounded-lg bg-[var(--admin-danger)] px-4 font-bold text-white disabled:opacity-50">{busy ? 'جارٍ الحفظ…' : blocked ? 'فك الحظر' : 'حظر الشخص وإبلاغه بالسبب'}</button>
      {status?.deliveries.map((delivery, index) => <p key={delivery.id} role="status" className="text-xs leading-6">{delivery.accountId ? `واتساب QR ${index + 1}` : 'واتساب Meta'}: {states[delivery.status] ?? 'حالة غير معروفة'} · {notices[delivery.noticeStatus] ?? delivery.noticeStatus}</p>)}
      {status?.deliveries.some(delivery => delivery.status === 'Failed') && <button type="button" disabled={busy} className="min-h-11 rounded-lg border border-[var(--admin-border)] px-3 font-bold" onClick={() => {
        setBusy(true); setError('');
        void liveSupportService.retrySupportBlock(conversationId).then(setStatus).catch(cause => setError(getLiveSupportApiError(cause, 'تعذر إتمام الطلب. حاول مجددًا.'))).finally(() => setBusy(false));
      }}>إعادة محاولة التنفيذ على واتساب</button>}
      {error && <p role="alert" className="text-[var(--admin-danger)]">{error}</p>}
    </div>
  </details>;
}
