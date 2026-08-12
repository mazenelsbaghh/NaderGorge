'use client';
import { useCallback, useEffect, useState } from 'react';
import { ClipboardCheck, ChevronDown } from 'lucide-react';
import { adminAiAgentService } from '@/services/admin-ai-agent-service';
import type { AdminAiAuditEvidence as Evidence } from '@/services/admin-ai-agent-contract';

export function AdminAiAuditEvidence() {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<Evidence[]>([]);
  const [cursor, setCursor] = useState<string>();
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);

  const load = useCallback(async (signal: AbortSignal, nextCursor?: string) => {
    setLoading(true);
    setFailed(false);
    try {
      const page = await adminAiAgentService.actionEvidence(signal, nextCursor);
      setItems((current) =>
        nextCursor ? [...current, ...page.items] : page.items
      );
      setCursor(page.nextCursor ?? undefined);
    } catch {
      setFailed(true);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!open || items.length > 0) return;
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [items.length, load, open]);

  return (
    <section className="border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)]">
      <button
        type="button"
        aria-expanded={open}
        onClick={() => setOpen((current) => !current)}
        className="flex min-h-11 w-full items-center gap-2 px-4 text-sm font-bold"
      >
        <ClipboardCheck className="h-4 w-4 text-[var(--admin-primary)]" />
        سجل أدلة الإجراءات المنقّح
        <ChevronDown className="mr-auto h-4 w-4" />
      </button>
      {open && (
        <div
          aria-label="أدلة الإجراءات دون نصوص المحادثات"
          className="max-h-64 overflow-y-auto border-t border-[var(--admin-border)] p-3"
          tabIndex={0}
        >
          {failed && (
            <p role="alert" className="text-sm text-[var(--admin-danger)]">
              تعذر تحميل الأدلة وفق صلاحية سجل التدقيق.
            </p>
          )}
          {!failed && !loading && items.length === 0 && (
            <p className="text-sm text-[var(--admin-muted)]">
              لا توجد أدلة إجراءات متاحة.
            </p>
          )}
          <ol className="space-y-2">
            {items.map((evidence) => (
              <li
                key={evidence.eventId}
                className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 text-sm"
              >
                <p className="font-bold">{evidence.safeSummaryAr}</p>
                <dl className="mt-2 grid grid-cols-2 gap-1 text-xs">
                  <dt className="text-[var(--admin-muted)]">الإجراء</dt>
                  <dd dir="auto">{evidence.capabilityKey ?? '—'}</dd>
                  <dt className="text-[var(--admin-muted)]">النتيجة</dt>
                  <dd>{evidence.resultStatus ?? '—'}</dd>
                  <dt className="text-[var(--admin-muted)]">الوقت</dt>
                  <dd dir="ltr">
                    {new Date(evidence.occurredAt).toLocaleString('ar-EG')}
                  </dd>
                </dl>
                <p
                  className="mt-2 break-all text-[11px] text-[var(--admin-muted)]"
                  dir="ltr"
                >
                  Trace: {evidence.traceId}
                </p>
              </li>
            ))}
          </ol>
          {cursor && (
            <button
              type="button"
              disabled={loading}
              onClick={() => void load(new AbortController().signal, cursor)}
              className="mt-3 min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold"
            >
              {loading ? 'جارٍ التحميل…' : 'تحميل أدلة أقدم'}
            </button>
          )}
        </div>
      )}
    </section>
  );
}
