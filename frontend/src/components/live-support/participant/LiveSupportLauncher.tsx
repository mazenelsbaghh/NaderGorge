'use client';

import { useEffect, useRef, useState } from 'react';
import { Headphones, LoaderCircle, MessageCircle, Paperclip, Send, X } from 'lucide-react';
import { liveSupportService, type LiveSupportAvailability, type LiveSupportConversation, type LiveSupportMessage, type LiveSupportMessageType } from '@/services/live-support-service';
import { LiveSupportWidget } from './LiveSupportWidget';
import { GuestIntake } from './GuestIntake';
import { QueueStatus } from './QueueStatus';
import { ParticipantConversation } from './ParticipantConversation';
import { ConversationRating } from './ConversationRating';

function formatNext(value?: string | null) {
  if (!value) return null;
  return new Intl.DateTimeFormat('ar-EG', { dateStyle: 'full', timeStyle: 'short', timeZone: 'Africa/Cairo' }).format(new Date(value));
}

export function LiveSupportLauncher() {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [availability, setAvailability] = useState<LiveSupportAvailability>();
  const [conversation, setConversation] = useState<LiveSupportConversation>();
  const [messages, setMessages] = useState<LiveSupportMessage[]>([]);
  const [needsGuest, setNeedsGuest] = useState(false);
  const [error, setError] = useState('');
  const [draft, setDraft] = useState('');
  const [uploading, setUploading] = useState(false);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);
  const startingNew = useRef(false);

  const [activeAction, setActiveAction] = useState<any>(null);
  const [activeVerification, setActiveVerification] = useState<any>(null);

  async function refresh() {
    const availabilityResult = await liveSupportService.getAvailability();
    setAvailability(availabilityResult);
    if (!availabilityResult.isAvailable) return;
    try {
      const history = await liveSupportService.listParticipantConversations();
      const current = history.find((item) => !['Closed', 'Abandoned'].includes(item.status)) ?? (startingNew.current ? undefined : history[0]);
      setConversation(current);
      setNeedsGuest(false);
      if (current) {
        setMessages(await liveSupportService.getMessages(current.id));
        if (current.isAiActive) {
          try {
            const [act, ver] = await Promise.all([
              liveSupportService.getActivePendingAction(current.id),
              liveSupportService.getActiveVerificationSession(current.id)
            ]);
            setActiveAction(act);
            setActiveVerification(ver);
          } catch {
            setActiveAction(null);
            setActiveVerification(null);
          }
        } else {
          setActiveAction(null);
          setActiveVerification(null);
        }
      } else {
        setActiveAction(null);
        setActiveVerification(null);
      }
    } catch { setNeedsGuest(true); }
  }

  async function handleConfirmAction(proposalId: string) {
    if (!conversation) return;
    await liveSupportService.confirmAIAction(conversation.id, proposalId);
    await refresh();
  }

  async function handleCancelAction(proposalId: string) {
    if (!conversation) return;
    await liveSupportService.cancelAIAction(conversation.id, proposalId);
    await refresh();
  }

  async function handleConfirmHandoff() {
    if (!conversation) return;
    await liveSupportService.confirmAIHandoff(conversation.id);
    await refresh();
  }

  async function handleCancelHandoff() {
    if (!conversation) return;
    await liveSupportService.cancelAIHandoff(conversation.id);
    await refresh();
  }

  function handleVerificationSuccess() {
    void refresh();
  }

  function handleRegistrationSuccess() {
    void refresh();
  }

  useEffect(() => {
    if (!open) return;
    setLoading(true); setError('');
    void refresh().catch(() => setError('تعذر الاتصال بالدعم حاليًا.')).finally(() => setLoading(false));
    timer.current = setInterval(() => void refresh().catch(() => undefined), 5000);
    return () => { if (timer.current) clearInterval(timer.current); };
  }, [open]);

  async function createGuest(form: FormData) {
    setLoading(true); setError('');
    try {
      await liveSupportService.createGuestSession({ displayName: String(form.get('name')), phoneNumber: String(form.get('phone')) });
      setNeedsGuest(false); await refresh();
    } catch { setError('راجع الاسم ورقم الهاتف وحاول مرة أخرى.'); }
    finally { setLoading(false); }
  }

  async function start(form: FormData) {
    setLoading(true); setError('');
    try {
      const created = await liveSupportService.createConversation({ subject: String(form.get('subject') || '') });
      startingNew.current = false;
      setConversation(created); setMessages([]);
    } catch (cause) {
      const message = (cause as { response?: { data?: { message?: string } } }).response?.data?.message;
      setError(message ?? 'تعذر بدء المحادثة.'); await refresh();
    } finally { setLoading(false); }
  }

  async function send() {
    if (!conversation || !draft.trim()) return;
    const value = draft.trim(); setDraft('');
    try {
      const message = await liveSupportService.sendParticipantMessage(conversation.id, { clientMessageId: crypto.randomUUID(), type: 'Text', content: value });
      setMessages((items) => [...items, message]);
      if (conversation.isAiActive) {
        setConversation((current) => current ? { ...current, isAiTyping: true } : current);
        setTimeout(() => void refresh().catch(() => undefined), 500);
      }
    }
    catch { setDraft(value); setError('لم تُرسل الرسالة. حاول مرة أخرى.'); }
  }

  async function upload(file?: File) {
    if (!conversation || !file) return;
    setUploading(true); setError('');
    try {
      const attachment = await liveSupportService.uploadAttachment(conversation.id, file);
      const type: LiveSupportMessageType = file.type.startsWith('image/') ? 'Image' : file.type === 'application/pdf' ? 'Pdf' : 'Audio';
      const message = await liveSupportService.sendParticipantMessage(conversation.id, { clientMessageId: crypto.randomUUID(), type, content: file.name, attachmentId: attachment.id });
      setMessages((items) => [...items, message]);
      if (conversation.isAiActive) {
        setConversation((current) => current ? { ...current, isAiTyping: true } : current);
        setTimeout(() => void refresh().catch(() => undefined), 500);
      }
    } catch { setError('تعذر رفع الملف. الأنواع المتاحة: صور وPDF وصوت حتى 10 ميجابايت.'); }
    finally { setUploading(false); }
  }

  async function abandon() {
    if (!conversation) return;
    setLoading(true); setError('');
    try {
      const updated = await liveSupportService.abandonConversation(conversation.id);
      setConversation(updated);
      await refresh();
    } catch {
      setError('تعذر إنهاء المحادثة. حاول مرة أخرى.');
    } finally {
      setLoading(false);
    }
  }

  return <div dir="rtl" className="fixed bottom-[calc(1rem+env(safe-area-inset-bottom))] left-4 z-[90] sm:left-6">
    {open && <section role="dialog" aria-modal="true" aria-label="الدعم المباشر" className="mb-3 flex h-[min(680px,calc(100dvh-7rem))] w-[min(390px,calc(100vw-2rem))] flex-col overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-2xl">
      <header className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
        <div className="flex items-center gap-2"><span className="grid size-9 place-items-center rounded-xl bg-cyan-50 text-cyan-700"><Headphones size={19}/></span><div><h2 className="font-bold text-slate-900">الدعم المباشر</h2><p className="text-xs text-slate-500">فريق مسار</p></div></div>
        <div className="flex items-center gap-1.5">
          {conversation && !['Closed', 'Abandoned'].includes(conversation.status) && (
            <button
              type="button"
              onClick={() => {
                if (confirm('هل أنت متأكد من إنهاء المحادثة؟')) {
                  void abandon();
                }
              }}
              className="rounded-xl border border-red-200 px-2.5 py-1 text-xs font-semibold text-red-600 hover:bg-red-50 transition-colors"
            >
              إنهاء المحادثة
            </button>
          )}
          <button type="button" onClick={() => setOpen(false)} aria-label="إغلاق" className="grid size-10 place-items-center rounded-full text-slate-600 hover:bg-slate-100"><X size={20}/></button>
        </div>
      </header>
      <div className="flex min-h-0 flex-1 flex-col p-4"><LiveSupportWidget>
        {loading && !availability ? <div className="grid flex-1 place-items-center"><LoaderCircle className="animate-spin text-cyan-700"/></div> : null}
        {availability && !availability.isAvailable ? <div className="grid flex-1 place-items-center text-center"><div><span className="mx-auto mb-4 grid size-16 place-items-center rounded-2xl bg-slate-100 text-slate-500"><Headphones size={28}/></span><h3 className="text-lg font-bold text-slate-900">الدعم غير متاح الآن</h3><p className="mt-2 max-w-xs text-sm leading-6 text-slate-600">لا يمكن بدء محادثة جديدة حاليًا.</p>{formatNext(availability.nextAvailableAt) && <div className="mt-4 rounded-2xl bg-cyan-50 px-4 py-3 text-sm font-semibold text-cyan-900">موعد توفر الدعم القادم<br/>{formatNext(availability.nextAvailableAt)}</div>}</div></div> : null}
        {availability?.isAvailable && needsGuest ? <GuestIntake submit={createGuest} disabled={loading}/> : null}
        {availability?.isAvailable && !needsGuest && !conversation ? <form action={start} className="my-auto space-y-4"><h3 className="text-lg font-bold text-slate-900">كيف نساعدك؟</h3><label className="block text-sm font-medium text-slate-700">موضوع المحادثة<input name="subject" maxLength={200} placeholder="اكتب المشكلة باختصار" className="mt-1 h-11 w-full rounded-xl border border-slate-200 px-3 outline-none focus:border-cyan-600"/></label><button disabled={loading} className="h-11 w-full rounded-xl bg-cyan-700 font-semibold text-white disabled:opacity-50">ابدأ المحادثة</button></form> : null}
        {conversation && <>{conversation.status === 'Waiting' ? (
          conversation.isAiActive ? (
            <div aria-live="polite" className="mb-3 rounded-xl bg-cyan-50 px-3 py-2 text-xs text-cyan-900 font-medium">
              متصل بالمساعد الذكي للرد على استفسارك
            </div>
          ) : (
            <QueueStatus position={conversation.queuePosition}/>
          )
        ) : (
          <div aria-live="polite" className="mb-3 rounded-xl bg-slate-50 px-3 py-2 text-xs text-slate-600">
            {conversation.status === 'Closed' ? 'تم إغلاق المحادثة' : conversation.status === 'Abandoned' ? 'تم إنهاء المحادثة' : 'متصل بموظف الدعم'}
          </div>
        )}<ParticipantConversation
            conversationId={conversation.id}
            messages={messages}
            isAiTyping={conversation.isAiTyping}
            activeAction={activeAction}
            activeVerification={activeVerification}
            onConfirmAction={handleConfirmAction}
            onCancelAction={handleCancelAction}
            onConfirmHandoff={handleConfirmHandoff}
            onCancelHandoff={handleCancelHandoff}
            onVerificationSuccess={handleVerificationSuccess}
            onRegistrationSuccess={handleRegistrationSuccess}
          />{conversation.canSend ? <div className="flex gap-2 border-t border-slate-100 pt-3"><label aria-label="إرفاق ملف" className="grid size-11 shrink-0 cursor-pointer place-items-center rounded-xl border border-slate-200 text-slate-600 focus-within:outline-2"><Paperclip size={18}/><input type="file" accept="image/jpeg,image/png,image/webp,application/pdf,audio/mpeg,audio/mp4,audio/ogg" disabled={uploading} onChange={(event) => void upload(event.target.files?.[0])} className="sr-only"/></label><input value={draft} onChange={(event) => setDraft(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); void send(); } }} placeholder="اكتب رسالتك" className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 px-3 outline-none focus:border-cyan-600"/><button type="button" onClick={() => void send()} aria-label="إرسال" className="grid size-11 place-items-center rounded-xl bg-cyan-700 text-white"><Send size={18}/></button></div> : <ClosedActions conversation={conversation} onNew={() => { startingNew.current = true; setConversation(undefined); setMessages([]); }}/>}</>}
        {error && <p role="alert" className="mt-3 text-center text-sm text-red-600">{error}</p>}
      </LiveSupportWidget></div>
    </section>}
    <button type="button" onClick={() => setOpen((value) => !value)} aria-expanded={open} aria-label={open ? 'إغلاق الدعم المباشر' : 'فتح الدعم المباشر'} className="grid size-14 place-items-center rounded-2xl bg-slate-900 text-white shadow-xl transition-transform hover:scale-105 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-slate-900"><MessageCircle size={24}/></button>
  </div>;
}

function ClosedActions({ conversation, onNew }: { conversation: LiveSupportConversation; onNew: () => void }) {
  const [rated, setRated] = useState(!conversation.canRate);
  return <div className="space-y-3 border-t border-slate-100 pt-3">{!rated && conversation.status === 'Closed' && <ConversationRating conversationId={conversation.id} onRated={() => setRated(true)}/>}<button type="button" onClick={onNew} className="h-11 w-full rounded-xl bg-slate-900 font-semibold text-white">محادثة جديدة</button></div>;
}
