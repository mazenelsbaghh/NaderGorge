'use client';

import { FormEvent, useCallback, useEffect, useRef, useState } from 'react';
import { Send, X } from 'lucide-react';

import { AccessibleOverlay } from '@/components/ui/AccessibleOverlay';
import { useLiveSupportHub } from '@/hooks/useLiveSupportHub';
import { formatCairoTimestamp } from '@/lib/cairo-time';
import { createClientId } from '@/lib/client-id';
import {
  liveSupportService,
  type LiveSupportConversationTimeline,
  type LiveSupportMessage,
} from '@/services/live-support-service';

export function ConversationInvestigation({ timeline, close }: { timeline: LiveSupportConversationTimeline; close: () => void }) {
  const [messages, setMessages] = useState<LiveSupportMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');
  const [eventFilter, setEventFilter] = useState('all');
  const [intervening, setIntervening] = useState(false);
  const [participantDraft, setParticipantDraft] = useState<string | null>(null);
  const endRef = useRef<HTMLDivElement>(null);
  const typingClearTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const messagesAbort = useRef<AbortController | null>(null);
  const canSend = timeline.conversation.status !== 'Closed' && timeline.conversation.status !== 'Abandoned';

  const refreshMessages = useCallback(async () => {
    messagesAbort.current?.abort();
    const controller = new AbortController();
    messagesAbort.current = controller;
    try {
      const result = await liveSupportService.getStaffMessages(timeline.conversation.id, controller.signal);
      setMessages(result);
      setError('');
    } catch (cause) {
      if (!isAbortError(cause)) setError('تعذر تحميل رسائل المحادثة.');
    } finally {
      if (messagesAbort.current === controller) setLoading(false);
    }
  }, [timeline.conversation.id]);

  const showParticipantDraft = useCallback((preview: string | null) => {
    setParticipantDraft(preview);
    if (typingClearTimer.current) clearTimeout(typingClearTimer.current);
    typingClearTimer.current = setTimeout(() => setParticipantDraft(null), 2_000);
  }, []);

  useLiveSupportHub(timeline.conversation.id, () => void refreshMessages(), showParticipantDraft);

  useEffect(() => {
    setLoading(true);
    void refreshMessages();
    return () => { messagesAbort.current?.abort(); if (typingClearTimer.current) clearTimeout(typingClearTimer.current); };
  }, [refreshMessages]);

  useEffect(() => { endRef.current?.scrollIntoView({ block: 'end' }); }, [messages]);

  async function sendMessage(event: FormEvent) {
    event.preventDefault();
    const content = draft.trim();
    if (!content || sending) return;
    setSending(true);
    setError('');
    try {
      const message = await liveSupportService.sendStaffMessage(timeline.conversation.id, {
        clientMessageId: createClientId(),
        content,
      });
      setMessages((current) => [...current, message]);
      setDraft('');
    } catch {
      setError('تعذر إرسال الرسالة. تحقق أن المحادثة ما زالت مفتوحة.');
    } finally {
      setSending(false);
    }
  }

  async function intervene(operation: 'close' | 'queue') {
    const reason = operation === 'close'
      ? 'إغلاق إداري مباشر'
      : window.prompt('اكتب سبب إعادتها للطابور');
    if (!reason?.trim()) return;
    setIntervening(true); setError('');
    try { await liveSupportService.intervene(timeline.conversation.id, operation, reason.trim()); close(); }
    catch { setError('تعذر تنفيذ تدخل الإدارة. حدّث المحادثة ثم حاول مرة أخرى.'); }
    finally { setIntervening(false); }
  }

  return (
    <AccessibleOverlay
      open
      onClose={close}
      label="متابعة المحادثة"
      backdropClassName="bg-[color-mix(in_srgb,var(--admin-primary)_72%,transparent)]"
      className="inset-x-4 top-1/2 mx-auto flex h-[calc(100dvh-2rem)] max-w-4xl -translate-y-1/2 flex-col overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-[var(--admin-shadow)] sm:h-[min(46rem,calc(100dvh-2rem))]"
    >
      <div className="flex min-h-0 flex-1 flex-col" dir="rtl">
        <header className="flex items-center justify-between border-b border-[var(--admin-border)] px-5 py-4">
          <div>
            <h2 className="font-bold text-[var(--admin-text)]">محادثة {timeline.conversation.participantName}</h2>
            <p className="mt-1 text-xs text-[var(--admin-muted)]">{timeline.conversation.ownerName ? `المسؤول الآن: ${timeline.conversation.ownerName}` : 'في انتظار الاستلام'} · {timeline.conversation.status}</p>
          </div>
          <button type="button" onClick={close} aria-label="إغلاق" className="grid size-11 place-items-center rounded-xl text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]"><X /></button>
        </header>
        {canSend && <div className="flex flex-wrap gap-2 border-b border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] px-5 py-3"><span className="ml-auto text-sm font-semibold text-[var(--admin-text)]">تدخل إداري مسجل بالكامل</span><button type="button" disabled={intervening} onClick={() => void intervene('queue')} className="min-h-10 rounded-lg border border-[var(--admin-warning-20)] px-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-card)] disabled:opacity-50">إعادة للطابور</button><button type="button" disabled={intervening} onClick={() => void intervene('close')} className="min-h-10 rounded-lg bg-[var(--admin-danger)] px-3 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50">إغلاق إداري</button></div>}

        <div className="grid min-h-0 flex-1 lg:grid-cols-[1.35fr_.65fr]">
          <div className="flex min-h-0 flex-col border-l border-[var(--admin-border)] lg:min-h-[420px]">
            <div className="min-h-0 flex-1 space-y-3 overflow-y-auto bg-[var(--admin-card-soft)] p-4" aria-live="polite">
              {loading ? <div className="grid h-full place-items-center gap-3" aria-label="جارٍ تحميل الرسائل"><div className="h-12 w-2/3 animate-pulse rounded-xl bg-[var(--admin-card-strong)]" /><div className="mr-auto h-16 w-1/2 animate-pulse rounded-xl bg-[var(--admin-card-strong)]" /></div> : messages.length === 0 ? <p className="grid h-full place-items-center text-sm text-[var(--admin-muted)]">لا توجد رسائل بعد.</p> : messages.map((message) => {
                const fromTeam = message.senderType === 'Staff' || message.senderType === 'Admin' || message.senderType === 'System' || message.senderType === 'AI';
                return <article key={message.id} className={`max-w-[82%] rounded-2xl px-4 py-3 ${fromTeam ? 'mr-auto bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'ml-auto bg-[var(--admin-card)] text-[var(--admin-text)] shadow-sm'}`}>
                  <p className="whitespace-pre-wrap break-words text-sm">{message.content}</p>
                  <div className={`mt-2 flex items-center justify-between gap-4 text-sm ${fromTeam ? 'text-[color-mix(in_srgb,var(--admin-primary-contrast)_76%,transparent)]' : 'text-[var(--admin-muted)]'}`}>
                    <span>{senderLabel(message.senderType)}</span>
                    <time dateTime={message.sentAt}>{formatCairoTimestamp(message.sentAt)}</time>
                  </div>
                </article>;
              })}
              {participantDraft !== null ? <article className="ml-auto max-w-[82%] rounded-2xl border border-cyan-200 bg-cyan-50 px-4 py-3 text-sm text-cyan-950"><p className="mb-1 text-xs font-bold text-cyan-700">الطالب يكتب الآن…</p><p className="whitespace-pre-wrap break-words">{participantDraft || '…'}</p></article> : null}
              <div ref={endRef} />
            </div>
            <form onSubmit={sendMessage} className="border-t border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
              {error && <p role="alert" className="mb-2 text-sm text-[var(--admin-danger)]">{error}</p>}
              <div className="flex gap-2">
                <textarea value={draft} onChange={(event) => setDraft(event.target.value)} disabled={!canSend || sending} rows={2} maxLength={4000} placeholder={canSend ? 'اكتب رسالة باسم الإدارة، وستظل ظاهرة لأي موظف يستلم المحادثة لاحقًا' : 'المحادثة مغلقة'} className="min-h-12 flex-1 resize-none rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)] disabled:bg-[var(--admin-card-soft)]" />
                <button type="submit" disabled={!canSend || !draft.trim() || sending} className="inline-flex min-h-12 min-w-12 items-center justify-center rounded-xl bg-[var(--admin-primary)] px-4 text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] disabled:cursor-not-allowed disabled:opacity-50"><Send size={18} /><span className="sr-only">إرسال الرسالة</span></button>
              </div>
            </form>
          </div>

          <aside className="min-h-0 overflow-y-auto p-4">
            <div className="mb-3 flex items-center justify-between gap-2"><h3 className="font-bold text-[var(--admin-text)]">السجل التشغيلي</h3><label className="text-xs text-[var(--admin-muted)]">النوع<select value={eventFilter} onChange={event => setEventFilter(event.target.value)} className="mr-2 h-9 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2 text-[var(--admin-text)]"><option value="all">الكل</option><option value="AI">AI / Worker</option><option value="Assignment">الإسناد</option><option value="StudentAction">الإجراءات</option><option value="Message">الرسائل</option></select></label></div>
            <ol className="space-y-3">{timeline.items.filter(item => eventFilter === 'all' || (eventFilter === 'AI' ? item.type.startsWith('AI') : item.type === eventFilter)).map((item, index) => <li key={`${item.at}-${index}`} className="rounded-xl bg-[var(--admin-card-soft)] p-3 text-sm"><strong className="text-[var(--admin-text)]">{item.summary}</strong><time dateTime={item.at} className="mt-1 block text-xs text-[var(--admin-muted)]">{formatCairoTimestamp(item.at)}</time><p className="mt-1 text-xs text-[var(--admin-muted)]">الفاعل: {item.actorName || 'النظام الآلي'}</p>{item.safeDetails && <pre className="mt-2 whitespace-pre-wrap break-all rounded-lg bg-[var(--admin-card)] p-2 text-xs text-[var(--admin-text)]" dir="auto">{item.safeDetails}</pre>}</li>)}</ol>
          </aside>
        </div>
      </div>
    </AccessibleOverlay>
  );
}

function senderLabel(senderType: LiveSupportMessage['senderType']) {
  return ({ Student: 'الطالب', Guest: 'الزائر', Staff: 'موظف الدعم', Admin: 'الإدارة', System: 'النظام', AI: 'المساعد الذكي' } as const)[senderType];
}

function isAbortError(cause: unknown) {
  return (typeof DOMException !== 'undefined' && cause instanceof DOMException && cause.name === 'AbortError')
    || (typeof cause === 'object' && cause !== null && 'code' in cause && (cause as { code?: string }).code === 'ERR_CANCELED');
}
