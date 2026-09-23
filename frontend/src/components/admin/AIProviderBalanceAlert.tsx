'use client';

import { useEffect, useRef, useState } from 'react';
import { AlertTriangle, ExternalLink } from 'lucide-react';
import apiClient from '@/services/api-client';
import { useAuthStore } from '@/stores/auth-store';
import { AdminModal } from './AdminModal';

interface ProviderStatus {
  state: 'unknown' | 'healthy' | 'balance-exhausted' | 'quota-exhausted';
  incidentId: string | null;
  updatedAt: string | null;
}

export function AIProviderBalanceAlert() {
  const adminId = useAuthStore(state => state.user?.roles.includes('Admin') ? state.user.id : null);
  const [status, setStatus] = useState<ProviderStatus | null>(null);
  const [open, setOpen] = useState(false);
  const dismissed = useRef<Record<string, string>>({});

  useEffect(() => {
    if (!adminId) return;
    const controller = new AbortController();
    let timer: ReturnType<typeof setTimeout>;
    const poll = async () => {
      try {
        if (document.visibilityState !== 'hidden') {
          const response = await apiClient.get<{ data: ProviderStatus }>('/admin/ai-provider-status', { signal: controller.signal });
          if (controller.signal.aborted) return;
          const next = response.data.data;
          setStatus(next);
          if (next.state === 'healthy' || next.state === 'unknown') setOpen(false);
          else if (next.incidentId) {
            let dismissedId = dismissed.current[adminId];
            try { dismissedId = sessionStorage.getItem(`ai-provider-dismissed:${adminId}`) ?? dismissedId; } catch { /* Storage may be disabled. */ }
            if (dismissedId !== next.incidentId) setOpen(true);
          }
        }
      } catch {
        // Keep the last known alert during connection failures; a failed read is not recovery.
      }
      if (!controller.signal.aborted) timer = setTimeout(poll, 30_000);
    };
    void poll();
    return () => { controller.abort(); clearTimeout(timer); };
  }, [adminId]);

  if (!adminId || !status || !['balance-exhausted', 'quota-exhausted'].includes(status.state)) return null;
  const exhausted = status.state === 'balance-exhausted';
  const title = exhausted ? 'رصيد الذكاء الاصطناعي خلص' : 'وصل الذكاء الاصطناعي لحد الاستخدام';
  const close = () => {
    if (status.incidentId) {
      dismissed.current[adminId] = status.incidentId;
      try { sessionStorage.setItem(`ai-provider-dismissed:${adminId}`, status.incidentId); } catch { /* Keep the in-memory dismissal. */ }
    }
    setOpen(false);
  };

  return (
    <>
      {!open && (
        <button type="button" onClick={() => setOpen(true)}
          className="fixed bottom-24 left-4 z-[var(--z-floating)] flex min-h-11 max-w-[calc(100vw-2rem)] items-center gap-2 rounded-xl border border-amber-700 bg-amber-50 px-4 py-3 text-sm font-bold text-amber-950"
          aria-label={`عرض التنبيه: ${title}`}>
          <AlertTriangle size={18} aria-hidden="true" />{title}
        </button>
      )}
      <AdminModal open={open} onClose={close} title={title} maxWidth="max-w-lg">
        <div dir="rtl" className="space-y-5">
          <div className="flex items-start gap-3 rounded-xl bg-amber-50 p-4 text-amber-950">
            <AlertTriangle size={24} className="shrink-0" aria-hidden="true" />
            <p className="text-sm leading-7">{exhausted
              ? 'مزوّد الخدمة أبلغ بنفاد رصيد Gemini. التصحيح الآلي والمهام التي تحتاج الذكاء الاصطناعي متعطّلة حاليًا. اشحن الرصيد من صفحة الفوترة.'
              : 'مزوّد الخدمة أوقف الطلبات بسبب حد الاستخدام. ده مش تأكيد إن الرصيد خلص؛ راجع حدود الاستخدام، وسيعيد النظام محاولة التصحيح تلقائيًا.'}</p>
          </div>
          <p className="text-sm leading-7 text-[var(--admin-text)]">إجابات الطلاب محفوظة. سيعاود النظام تصحيح الواجبات المعلّقة تلقائيًا عند عودة الخدمة، دون إعادة تسليمها.</p>
          <div className="flex flex-wrap gap-3">
            <a href={exhausted ? 'https://aistudio.google.com/billing' : 'https://aistudio.google.com/usage'}
              target="_blank" rel="noopener noreferrer" className="admin-btn-primary inline-flex min-h-11 items-center gap-2">
              {exhausted ? 'فتح صفحة الفوترة' : 'مراجعة حدود الاستخدام'}<ExternalLink size={16} aria-hidden="true" />
            </a>
            <button type="button" onClick={close} className="admin-btn-secondary min-h-11">فهمت</button>
          </div>
        </div>
      </AdminModal>
    </>
  );
}
