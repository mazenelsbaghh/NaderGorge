'use client';

import { FormEvent, useEffect, useRef, useState } from 'react';
import { LoaderCircle, Send, X } from 'lucide-react';
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
  const endRef = useRef<HTMLDivElement>(null);
  const canSend = timeline.conversation.status !== 'Closed' && timeline.conversation.status !== 'Abandoned';

  useEffect(() => {
    let active = true;
    setLoading(true);
    liveSupportService.getStaffMessages(timeline.conversation.id)
      .then((result) => { if (active) setMessages(result); })
      .catch(() => { if (active) setError('تعذر تحميل رسائل المحادثة.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [timeline.conversation.id]);

  useEffect(() => { endRef.current?.scrollIntoView({ block: 'end' }); }, [messages]);

  async function sendMessage(event: FormEvent) {
    event.preventDefault();
    const content = draft.trim();
    if (!content || sending) return;
    setSending(true);
    setError('');
    try {
      const message = await liveSupportService.sendStaffMessage(timeline.conversation.id, {
        clientMessageId: crypto.randomUUID(),
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

  return (
    <div className="fixed inset-0 z-[100] grid place-items-center bg-slate-950/60 p-4" onClick={close}>
      <section role="dialog" aria-modal="true" aria-label="متابعة المحادثة" onClick={(event) => event.stopPropagation()} className="flex max-h-[92dvh] w-full max-w-4xl flex-col overflow-hidden rounded-2xl bg-white" dir="rtl">
        <header className="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <div>
            <h2 className="font-bold text-slate-950">محادثة {timeline.conversation.participantName}</h2>
            <p className="mt-1 text-xs text-slate-600">{timeline.conversation.ownerName ? `المسؤول الآن: ${timeline.conversation.ownerName}` : 'في انتظار الاستلام'} · {timeline.conversation.status}</p>
          </div>
          <button type="button" onClick={close} aria-label="إغلاق" className="grid size-11 place-items-center rounded-xl text-slate-700 hover:bg-slate-100"><X /></button>
        </header>

        <div className="grid min-h-0 flex-1 lg:grid-cols-[1.35fr_.65fr]">
          <div className="flex min-h-[420px] flex-col border-l border-slate-200">
            <div className="min-h-0 flex-1 space-y-3 overflow-y-auto bg-slate-50 p-4" aria-live="polite">
              {loading ? <div className="grid h-full place-items-center"><LoaderCircle className="animate-spin text-cyan-700" /></div> : messages.length === 0 ? <p className="grid h-full place-items-center text-sm text-slate-600">لا توجد رسائل بعد.</p> : messages.map((message) => {
                const fromTeam = message.senderType === 'Staff' || message.senderType === 'Admin' || message.senderType === 'System' || message.senderType === 'AI';
                return <article key={message.id} className={`max-w-[82%] rounded-2xl px-4 py-3 ${fromTeam ? 'mr-auto bg-[#0A1D3D] text-white' : 'ml-auto bg-white text-slate-900 shadow-sm'}`}>
                  <p className="whitespace-pre-wrap break-words text-sm">{message.content}</p>
                  <div className={`mt-2 flex items-center justify-between gap-4 text-[11px] ${fromTeam ? 'text-slate-300' : 'text-slate-500'}`}>
                    <span>{senderLabel(message.senderType)}</span>
                    <time>{new Date(message.sentAt).toLocaleString('ar-EG')}</time>
                  </div>
                </article>;
              })}
              <div ref={endRef} />
            </div>
            <form onSubmit={sendMessage} className="border-t border-slate-200 bg-white p-4">
              {error && <p role="alert" className="mb-2 text-sm text-red-700">{error}</p>}
              <div className="flex gap-2">
                <textarea value={draft} onChange={(event) => setDraft(event.target.value)} disabled={!canSend || sending} rows={2} maxLength={4000} placeholder={canSend ? 'اكتب رسالة باسم الإدارة، وستظل ظاهرة لأي موظف يستلم المحادثة لاحقًا' : 'المحادثة مغلقة'} className="min-h-12 flex-1 resize-none rounded-xl border border-slate-300 px-3 py-2 text-sm outline-none focus:border-cyan-700 focus:ring-2 focus:ring-cyan-700/20 disabled:bg-slate-100" />
                <button type="submit" disabled={!canSend || !draft.trim() || sending} className="inline-flex min-h-12 min-w-12 items-center justify-center rounded-xl bg-[#0A1D3D] px-4 text-white disabled:cursor-not-allowed disabled:opacity-50"><Send size={18} /><span className="sr-only">إرسال الرسالة</span></button>
              </div>
            </form>
          </div>

          <aside className="min-h-0 overflow-y-auto p-4">
            <h3 className="mb-3 font-bold text-slate-900">السجل التشغيلي</h3>
            <ol className="space-y-3">{timeline.items.map((item, index) => <li key={`${item.at}-${index}`} className="rounded-xl bg-slate-50 p-3 text-sm"><strong className="text-slate-900">{item.summary}</strong><time className="mt-1 block text-xs text-slate-500">{new Date(item.at).toLocaleString('ar-EG')}</time><p className="mt-1 text-xs text-slate-600">بواسطة: {item.actorName || 'النظام'}</p>{item.safeDetails && <pre className="mt-2 whitespace-pre-wrap break-words rounded-lg bg-white p-2 text-xs">{item.safeDetails}</pre>}</li>)}</ol>
          </aside>
        </div>
      </section>
    </div>
  );
}

function senderLabel(senderType: LiveSupportMessage['senderType']) {
  return ({ Student: 'الطالب', Guest: 'الزائر', Staff: 'موظف الدعم', Admin: 'الإدارة', System: 'النظام', AI: 'المساعد الذكي' } as const)[senderType];
}
