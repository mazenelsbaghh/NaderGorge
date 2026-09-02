'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Headphones, LoaderCircle, MessageCircle, Paperclip, Send, X } from 'lucide-react';
import { liveSupportService, type LiveSupportAIPendingDecision, type LiveSupportAITurnState, type LiveSupportAIVerificationSession, type LiveSupportAvailability, type LiveSupportConversation, type LiveSupportMessage } from '@/services/live-support-service';
import { LiveSupportWidget } from './LiveSupportWidget';
import { QueueStatus } from './QueueStatus';
import { ParticipantConversation } from './ParticipantConversation';
import { ConversationRating } from './ConversationRating';
import { AIConversationStatus } from './AIConversationStatus';
import { EmojiPicker, insertEmojiAtCursor } from '@/components/live-support/shared/EmojiPicker';
import { useLiveSupportHub } from '@/hooks/useLiveSupportHub';
import { useLiveSupportStore } from '@/stores/live-support-store';
import { useAuthStore } from '@/stores/auth-store';
import apiClient from '@/services/api-client';
import { createClientId } from '@/lib/client-id';
import { formatCairoDateTime } from '@/lib/cairo-time';

function formatNext(value?: string | null) {
  if (!value) return null;
  return formatCairoDateTime(value, { dateStyle: 'full', timeStyle: 'short' });
}

function formatSupportTime(value: string) {
  const [hour = '0', minute = '0'] = value.split(':');
  return new Intl.DateTimeFormat('ar-EG-u-nu-latn', { hour: 'numeric', minute: '2-digit', hour12: true, timeZone: 'UTC' })
    .format(new Date(Date.UTC(2000, 0, 1, Number(hour), Number(minute))));
}

function formatBusinessHours(windows?: LiveSupportAvailability['businessHours']) {
  if (!windows?.length) return 'لم تُحدد مواعيد العمل لهذا اليوم بعد';
  return windows.map((window) => `من ${formatSupportTime(window.startLocalTime)} إلى ${formatSupportTime(window.endLocalTime)}`).join('، ');
}

const SUPPORT_SETTINGS_CACHE_KEY = 'massar:support-settings';

type LiveSupportLauncherProps = {
  avoidMobileBottomNav?: boolean;
};

type SupportVisibilitySettings = {
  liveSupportEnabled: boolean;
  showSupportOutsideAccount: boolean;
  guestSupportWhatsAppNumber: string;
  supportPhoneNumber: string;
};

export function LiveSupportLauncher({ avoidMobileBottomNav = false }: LiveSupportLauncherProps) {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [availability, setAvailability] = useState<LiveSupportAvailability>();
  const [conversation, setConversation] = useState<LiveSupportConversation>();
  const [messages, setMessages] = useState<LiveSupportMessage[]>([]);
  const [error, setError] = useState('');
  const [draft, setDraft] = useState('');
  const messageInputRef = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [pendingAction, setPendingAction] = useState<string | null>(null);
  const timer = useRef<ReturnType<typeof setInterval> | null>(null);
  const startingNew = useRef(false);
  const decisionIdempotencyKeys = useRef<Record<string, string>>({});
  const refreshGeneration = useRef(0);
  const refreshAbort = useRef<AbortController | null>(null);
  const mutationInFlight = useRef(false);
  const authIsLoading = useAuthStore((state) => state.isLoading);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const [supportVisibility, setSupportVisibility] = useState<SupportVisibilitySettings | null>(null);
  const [supportSettingsUnavailable, setSupportSettingsUnavailable] = useState(false);
  const drafts = useLiveSupportStore(state => state.drafts);
  const setStoredDraft = useLiveSupportStore(state => state.setDraft);
  const clearStoredDraft = useLiveSupportStore(state => state.clearDraft);

  useEffect(() => {
    setDraft(conversation?.id ? drafts[conversation.id] ?? '' : '');
  }, [conversation?.id, drafts]);

  useEffect(() => {
    const openSupport = () => setOpen(true);
    window.addEventListener('massar:open-support', openSupport);
    return () => window.removeEventListener('massar:open-support', openSupport);
  }, []);

  const loadSupportVisibility = useCallback(async () => {
    setSupportSettingsUnavailable(false);
    try {
      const response = await apiClient.get('/public/settings', { suppressErrorToast: true });
      const settings = mapSupportVisibilitySettings(response.data);
      setSupportVisibility(settings);
      window.localStorage.setItem(SUPPORT_SETTINGS_CACHE_KEY, JSON.stringify(settings));
    } catch {
      setSupportVisibility(readCachedSupportVisibility());
      setSupportSettingsUnavailable(true);
    }
  }, []);

  useEffect(() => {
    void loadSupportVisibility();
  }, [loadSupportVisibility]);

  const [activeAction, setActiveAction] = useState<LiveSupportAIPendingDecision | null>(null);
  const [activeVerification, setActiveVerification] = useState<LiveSupportAIVerificationSession | null>(null);
  const [aiTurnState, setAiTurnState] = useState<LiveSupportAITurnState | null>(null);
  const { sendTyping } = useLiveSupportHub(conversation?.id, () => void refresh());

  async function refresh() {
    const generation = ++refreshGeneration.current;
    refreshAbort.current?.abort();
    const controller = new AbortController();
    refreshAbort.current = controller;
    try {
      try {
        const nextAvailability = await liveSupportService.getAvailability(controller.signal);
        if (generation !== refreshGeneration.current) return;
        setAvailability(normalizeAvailability(nextAvailability));
        setError('');
      } catch (cause) {
        if (isAbortError(cause) || generation !== refreshGeneration.current) return;
        setError('تعذر معرفة حالة الدعم. أعد المحاولة.');
      }
      const history = await liveSupportService.listParticipantConversations(controller.signal);
      if (generation !== refreshGeneration.current) return;
      const current = history.find((item) => !['Closed', 'Abandoned'].includes(item.status)) ?? (startingNew.current ? undefined : history[0]);
      setConversation(current);
      if (current) {
        if (current.isAiActive) {
          try {
            const snapshot = await liveSupportService.getParticipantAISnapshot(current.id);
            setMessages(mergeMessages([], snapshot.messages, current.id));
            setActiveAction(snapshot.pendingDecision ?? null);
            setActiveVerification(snapshot.verification ?? null);
            setAiTurnState(snapshot.aiTurnState ?? null);
            setConversation((value) => value?.id === current.id ? { ...value, canSend: snapshot.canSend, queuePosition: snapshot.queuePosition ?? undefined, isAiTyping: ['Queued', 'Processing', 'ProviderCompleted'].includes(snapshot.aiTurnState ?? '') } : current);
          } catch (cause) {
            if (isAbortError(cause) || generation !== refreshGeneration.current) return;
            const fallbackMessages = await liveSupportService.getMessages(current.id, controller.signal);
            if (generation !== refreshGeneration.current) return;
            setMessages(mergeMessages([], fallbackMessages, current.id));
            setActiveAction(null);
            setActiveVerification(null);
            setAiTurnState(null);
          }
        } else {
          const nextMessages = await liveSupportService.getMessages(current.id, controller.signal);
          if (generation !== refreshGeneration.current) return;
          setMessages(mergeMessages([], nextMessages, current.id));
          setActiveAction(null);
          setActiveVerification(null);
          setAiTurnState(null);
        }
      } else {
        setMessages([]);
        setActiveAction(null);
        setActiveVerification(null);
        setAiTurnState(null);
      }
    } catch (cause) {
      if (isAbortError(cause) || generation !== refreshGeneration.current) return;
      if (isForbiddenOrConflict(cause)) setError(getParticipantMutationError(cause, 'لا يمكنك الوصول إلى هذه المحادثة. أعد فتحها من سجل المحادثات.'));
      else setError('تعذر تحميل سجل الدعم. أعد المحاولة.');
    }
  }

  async function handleConfirmAction(proposalId: string) {
    if (!conversation || pendingAction) return;
    setPendingAction(proposalId);
    const key = decisionIdempotencyKeys.current[proposalId] ??= createClientId();
    try { await liveSupportService.confirmAIAction(conversation.id, proposalId, key); await refresh(); }
    catch (cause) { setError(getParticipantMutationError(cause, 'تعذر تنفيذ الإجراء. راجع الحالة ثم أعد المحاولة.')); }
    finally { setPendingAction(null); }
  }

  async function handleCancelAction(proposalId: string) {
    if (!conversation || pendingAction) return;
    setPendingAction(proposalId);
    const key = decisionIdempotencyKeys.current[proposalId] ??= createClientId();
    try { await liveSupportService.cancelAIAction(conversation.id, proposalId, key); await refresh(); }
    catch (cause) { setError(getParticipantMutationError(cause, 'تعذر إلغاء الإجراء. أعد المحاولة.')); }
    finally { setPendingAction(null); }
  }

  async function handleConfirmHandoff() {
    if (!conversation || pendingAction) return;
    setPendingAction('handoff-confirm');
    try { await liveSupportService.confirmAIHandoff(conversation.id); await refresh(); }
    catch (cause) { setError(getParticipantMutationError(cause, 'تعذر تأكيد التحويل. أعد المحاولة.')); }
    finally { setPendingAction(null); }
  }

  async function handleCancelHandoff() {
    if (!conversation || pendingAction) return;
    setPendingAction('handoff-cancel');
    try { await liveSupportService.cancelAIHandoff(conversation.id); await refresh(); }
    catch (cause) { setError(getParticipantMutationError(cause, 'تعذر إلغاء التحويل. أعد المحاولة.')); }
    finally { setPendingAction(null); }
  }

  async function requestHumanSupport() {
    if (!conversation || pendingAction) return;
    setPendingAction('human-support');
    try { await liveSupportService.requestHumanHandoff(conversation.id); await refresh(); }
    catch (cause) { setError(getParticipantMutationError(cause, 'تعذر طلب موظف الدعم. أعد المحاولة.')); }
    finally { setPendingAction(null); }
  }

  function handleVerificationSuccess() {
    void refresh();
  }

  function handleRegistrationSuccess() {
    void refresh();
  }

  useEffect(() => {
    if (!open || authIsLoading) return;
    setLoading(true); setError('');
    void refresh().catch(() => setError('تعذر الاتصال بالدعم حاليًا.')).finally(() => setLoading(false));
    timer.current = setInterval(() => void refresh().catch(() => undefined), 5000);
    return () => { if (timer.current) clearInterval(timer.current); refreshAbort.current?.abort(); };
  }, [open, authIsLoading]);

  async function start(form: FormData) {
    setLoading(true); setError('');
    try {
      const created = await liveSupportService.createConversation({ subject: String(form.get('subject') || '') });
      startingNew.current = false;
      setConversation(created); setMessages([]); await refresh();
    } catch (cause) {
      setError(getParticipantMutationError(cause, 'تعذر بدء المحادثة. أعد المحاولة.')); await refresh();
    } finally { setLoading(false); }
  }

  async function send() {
    const conversationId = conversation?.id;
    const value = draft.trim();
    if (!conversationId || !value || pendingAction || !conversation?.canSend) return;
    setPendingAction('send'); setDraft(''); setStoredDraft(conversationId, '');
    try {
      const message = await liveSupportService.sendParticipantMessage(conversationId, { clientMessageId: createClientId(), type: 'Text', content: value });
      setMessages((items) => mergeMessages(items, [message], conversationId));
      clearStoredDraft(conversationId);
      if (conversation.isAiActive) {
        setConversation((current) => current ? { ...current, isAiTyping: true } : current);
        setTimeout(() => void refresh().catch(() => undefined), 500);
      }
    }
    catch (cause) { setDraft(value); setStoredDraft(conversationId, value); setError(getParticipantMutationError(cause, 'لم تُرسل الرسالة. أعد المحاولة.')); }
    finally { setPendingAction(null); }
  }

  function insertEmoji(emoji: string) {
    const input = messageInputRef.current;
    const draftInsertion = insertEmojiAtCursor(input, draft, emoji);
    setDraft(draftInsertion.draftText);
    if (conversation?.id) {
      setStoredDraft(conversation.id, draftInsertion.draftText);
      sendTyping(draftInsertion.draftText);
    }
    requestAnimationFrame(() => {
      input?.focus();
      input?.setSelectionRange(draftInsertion.cursorPosition, draftInsertion.cursorPosition);
    });
  }

  async function upload(file?: File) {
    if (!conversation || !file || pendingAction) return;
    const isImage = file.type.startsWith('image/');
    const isPdf = file.type === 'application/pdf';
    if (!isImage && !isPdf) {
      setError('يمكن للطلاب إرسال صور وملفات PDF فقط. التسجيل الصوتي متاح لفريق الدعم.');
      return;
    }
    setUploading(true); setError('');
    try {
      const attachment = await liveSupportService.uploadAttachment(conversation.id, file);
      const type = isImage ? 'Image' : 'Pdf';
      const message = await liveSupportService.sendParticipantMessage(conversation.id, { clientMessageId: createClientId(), type, content: file.name, attachmentId: attachment.id });
      setMessages((items) => mergeMessages(items, [message], conversation.id));
      if (conversation.isAiActive) {
        setConversation((current) => current ? { ...current, isAiTyping: true } : current);
        setTimeout(() => void refresh().catch(() => undefined), 500);
      }
    } catch (cause) { setError(getParticipantMutationError(cause, 'تعذر رفع الملف. الأنواع المتاحة: صور وPDF وصوت حتى 10 ميجابايت.')); }
    finally { setUploading(false); }
  }

  async function editMessage(messageId: string, content: string) {
    if (!conversation || pendingAction) return;
    setPendingAction(`edit:${messageId}`); setError('');
    try {
      const updated = await liveSupportService.updateParticipantMessage(conversation.id, messageId, content);
      setMessages((items) => mergeMessages(items, [updated], conversation.id));
    } catch (cause) {
      setError(getParticipantMutationError(cause, 'تعذر تعديل الرسالة. أعد المحاولة.'));
      throw cause;
    } finally { setPendingAction(null); }
  }

  async function deleteMessage(messageId: string) {
    if (!conversation || pendingAction) return;
    setPendingAction(`delete:${messageId}`); setError('');
    try {
      const deleted = await liveSupportService.deleteParticipantMessage(conversation.id, messageId);
      setMessages((items) => mergeMessages(items, [deleted], conversation.id));
    } catch (cause) {
      setError(getParticipantMutationError(cause, 'تعذر حذف الرسالة. أعد المحاولة.'));
      throw cause;
    } finally { setPendingAction(null); }
  }

  async function abandon() {
    if (!conversation || mutationInFlight.current) return;
    mutationInFlight.current = true;
    setLoading(true); setError('');
    try {
      const updated = await liveSupportService.abandonConversation(conversation.id);
      setConversation(updated);
      await refresh();
    } catch (cause) {
      setError(getParticipantMutationError(cause, 'تعذر إنهاء المحادثة. أعد المحاولة.'));
    } finally {
      mutationInFlight.current = false;
      setLoading(false);
    }
  }

  const launcherPositionClass = avoidMobileBottomNav
    ? 'bottom-[calc(5.75rem+env(safe-area-inset-bottom))] lg:bottom-[calc(1rem+env(safe-area-inset-bottom))]'
    : 'bottom-[calc(1rem+env(safe-area-inset-bottom))]';
  const afterHours = Boolean(availability && !availability.isAvailable && availability.isOutsideBusinessHours);
  const whatsappNumber = supportVisibility?.guestSupportWhatsAppNumber.replace(/\D/g, '') ?? '';
  const contactNumber = whatsappNumber || supportVisibility?.supportPhoneNumber.trim() || '';

  if (authIsLoading || (!supportVisibility && !supportSettingsUnavailable)) return null;

  if (!supportVisibility) {
    return <SupportSettingsRecovery launcherPositionClass={launcherPositionClass} retry={loadSupportVisibility}/>;
  }

  if (!isAuthenticated) {
    const whatsappNumber = supportVisibility.guestSupportWhatsAppNumber.replace(/\D/g, '');
    if (!supportVisibility.showSupportOutsideAccount || !whatsappNumber) return null;
    return <a href={`https://wa.me/${whatsappNumber}`} target="_blank" rel="noopener noreferrer" dir="rtl" aria-label="التواصل عبر واتساب" className={`fixed ${launcherPositionClass} left-2 z-[var(--z-floating)] inline-flex size-12 items-center justify-center gap-2 rounded-full bg-[#25D366] p-0 text-sm font-black text-white shadow-xl transition-colors hover:bg-[#1fb75a] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#25D366] sm:left-6 sm:h-auto sm:w-auto sm:rounded-2xl sm:px-4 sm:py-3`}>
      <MessageCircle size={22}/><span className="sr-only sm:not-sr-only">واتساب الدعم</span>
    </a>;
  }

  if (!supportVisibility.liveSupportEnabled) {
    if (!contactNumber) return null;
    return <SupportContactLink launcherPositionClass={launcherPositionClass} whatsappNumber={whatsappNumber} contactNumber={contactNumber}/>;
  }

  return <div dir="rtl" className={`fixed ${launcherPositionClass} left-2 z-[var(--z-floating)] sm:left-6`}>
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
        {availability && !availability.isAvailable && !conversation && !afterHours ? <div className="grid flex-1 place-items-center text-center"><div><span className="mx-auto mb-4 grid size-16 place-items-center rounded-2xl bg-slate-100 text-slate-500"><Headphones size={28}/></span><h3 className="text-lg font-bold text-slate-900">الدعم غير متاح الآن</h3><p className="mt-2 max-w-xs text-sm leading-6 text-slate-600">لا يمكن بدء محادثة جديدة حاليًا.</p>{formatNext(availability.nextAvailableAt) && <div className="mt-4 rounded-2xl bg-cyan-50 px-4 py-3 text-sm font-semibold text-cyan-900">موعد توفر الدعم القادم<br/>{formatNext(availability.nextAvailableAt)}</div>}</div></div> : null}
        {availability && !availability.isAvailable && conversation ? <div role="status" className="mb-3 rounded-xl bg-amber-50 px-3 py-2 text-xs text-amber-900">الدعم غير متاح لبدء محادثة جديدة، لكن يمكنك متابعة محادثتك وسجلها الحالي.</div> : null}
        {(availability?.isAvailable || afterHours) && !conversation ? <form action={start} className="my-auto space-y-4">{afterHours ? <div className="rounded-2xl bg-amber-50 px-4 py-3 text-sm leading-6 text-amber-950"><h3 className="font-black">نحن الآن خارج مواعيد العمل الرسمية</h3><p className="mt-1">مواعيد العمل اليوم: {formatBusinessHours(availability?.businessHours)}.</p>{contactNumber && <p className="mt-1">للتواصل العاجل: {whatsappNumber ? <a href={`https://wa.me/${whatsappNumber}`} target="_blank" rel="noreferrer" dir="ltr" className="font-black underline">{contactNumber}</a> : <a href={`tel:${contactNumber.replace(/\s/g, '')}`} dir="ltr" className="font-black underline">{contactNumber}</a>} — وسنرد عليك صباحًا.</p>}</div> : <h3 className="text-lg font-bold text-slate-900">كيف نساعدك؟</h3>}<label className="block text-sm font-medium text-slate-700">{afterHours ? 'اترك رسالتك وسنتابعها صباحًا' : 'موضوع المحادثة'}<input name="subject" maxLength={200} required placeholder="اكتب المشكلة باختصار" className="mt-1 h-11 w-full rounded-xl border border-slate-200 px-3 outline-none focus:border-cyan-600"/></label><button disabled={loading || Boolean(pendingAction)} className="h-11 w-full rounded-xl bg-cyan-700 font-semibold text-white disabled:opacity-50">{afterHours ? 'إرسال الرسالة' : 'ابدأ المحادثة'}</button></form> : null}
        {conversation && <>{conversation.status === 'Waiting' ? (
          conversation.isAiActive ? (
            <AIConversationStatus turnState={aiTurnState} onRequestHuman={() => void requestHumanSupport().catch(() => setError('تعذر طلب موظف الدعم. حاول مرة أخرى.'))}/>
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
            onEditMessage={editMessage}
            onDeleteMessage={deleteMessage}
          />{conversation.canSend && !activeAction && !activeVerification ? <div className="flex gap-2 border-t border-slate-100 pt-3"><div className="flex shrink-0 gap-2"><label aria-label="إرفاق صورة أو PDF" className={`grid size-11 shrink-0 place-items-center rounded-xl border border-slate-200 text-slate-600 focus-within:outline-2 ${pendingAction ? 'pointer-events-none opacity-50' : 'cursor-pointer'}`}><Paperclip size={18}/><input type="file" accept="image/jpeg,image/png,image/webp,application/pdf" disabled={uploading || Boolean(pendingAction)} onChange={(event) => { void upload(event.target.files?.[0]); event.currentTarget.value = ''; }} className="sr-only"/></label><EmojiPicker disabled={Boolean(pendingAction) || uploading} onSelect={insertEmoji}/></div><input ref={messageInputRef} aria-label="رسالة الدعم" disabled={Boolean(pendingAction)} value={draft} onChange={(event) => { const nextDraft = event.target.value; setDraft(nextDraft); setStoredDraft(conversation.id, nextDraft); sendTyping(nextDraft); }} onKeyDown={(event) => { if (event.key === 'Enter') { event.preventDefault(); void send(); } }} placeholder="اكتب رسالتك" className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 px-3 outline-none focus-visible:border-cyan-700 focus-visible:ring-2 focus-visible:ring-cyan-700/20 disabled:bg-slate-100"/><button type="button" disabled={!draft.trim() || Boolean(pendingAction)} onClick={() => void send()} aria-label="إرسال" className="grid size-11 shrink-0 place-items-center rounded-xl bg-cyan-700 text-white disabled:opacity-50"><Send size={18}/></button></div> : conversation.canSend ? <p role="status" className="border-t border-slate-100 pt-3 text-center text-xs font-medium text-slate-600">أكمل خطوة التأكيد الظاهرة قبل إرسال رسالة جديدة.</p> : <ClosedActions conversation={conversation} onNew={() => { startingNew.current = true; setConversation(undefined); setMessages([]); }}/>}</>}
        {error && <div role="alert" className="mt-3 text-center text-sm text-red-600"><p>{error}</p><button type="button" disabled={retrying} onClick={() => { setRetrying(true); void refresh().finally(() => setRetrying(false)); }} className="mt-1 font-semibold underline">{retrying ? 'جارٍ التحديث…' : 'إعادة المحاولة'}</button></div>}
      </LiveSupportWidget></div>
    </section>}
    <div className="flex items-center gap-2">
      <span className="hidden max-w-[8rem] items-center justify-center rounded-xl border border-[#DCE1E6] bg-white px-3 py-2 text-xs font-black text-[#0A1D3D] shadow-lg sm:inline-flex sm:max-w-none sm:text-sm">
        تواصل معنا
      </span>
      <button type="button" onClick={() => setOpen((value) => !value)} aria-expanded={open} aria-label={open ? 'إغلاق الدعم المباشر' : 'فتح الدعم المباشر'} className="grid size-12 place-items-center rounded-full bg-[#0A1D3D] text-white shadow-xl transition-colors hover:bg-[#0E8F8F] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#0A1D3D] sm:size-14 sm:rounded-2xl"><MessageCircle size={24}/></button>
    </div>
  </div>;
}

function mapSupportVisibilitySettings(settings: unknown): SupportVisibilitySettings {
  const settingsRecord = settings as Record<string, unknown> | null;
  return {
    liveSupportEnabled: settingsRecord?.liveSupportEnabled === true,
    showSupportOutsideAccount: settingsRecord?.showSupportOutsideAccount === true,
    guestSupportWhatsAppNumber: String(settingsRecord?.guestSupportWhatsAppNumber ?? ''),
    supportPhoneNumber: String(settingsRecord?.supportPhoneNumber ?? ''),
  };
}

function readCachedSupportVisibility(): SupportVisibilitySettings | null {
  try {
    const rawSettings = window.localStorage.getItem(SUPPORT_SETTINGS_CACHE_KEY);
    if (!rawSettings) return null;
    return mapSupportVisibilitySettings(JSON.parse(rawSettings));
  } catch {
    return null;
  }
}

function SupportSettingsRecovery({ launcherPositionClass, retry }: { launcherPositionClass: string; retry: () => Promise<void> }) {
  const [retrying, setRetrying] = useState(false);
  const [open, setOpen] = useState(false);

  async function retrySupportSettings() {
    setRetrying(true);
    await retry();
    setRetrying(false);
  }

  return <div dir="rtl" className={`fixed ${launcherPositionClass} left-2 z-[var(--z-floating)] sm:left-6`}>
    {open && <section role="dialog" aria-modal="true" aria-label="مساعدة الدعم" className="mb-3 w-[min(360px,calc(100vw-2rem))] rounded-3xl border border-amber-200 bg-white p-5 shadow-2xl">
      <h2 className="text-lg font-black text-[#0A1D3D]">تعذر الوصول إلى الدعم الآن</h2>
      <p className="mt-2 text-sm font-bold leading-6 text-slate-600">تحقق من اتصالك بالإنترنت، ثم أعد تحميل وسيلة التواصل. لا نريد أن نعرض لك قناة دعم غير مؤكدة.</p>
      <div className="mt-5 flex items-center gap-3">
        <button type="button" disabled={retrying} onClick={() => void retrySupportSettings()} className="min-h-11 rounded-xl bg-[#0A1D3D] px-4 text-sm font-black text-white disabled:opacity-60">{retrying ? 'جارٍ التحقق…' : 'إعادة المحاولة'}</button>
        <button type="button" onClick={() => setOpen(false)} className="min-h-11 px-2 text-sm font-black text-slate-600">إغلاق</button>
      </div>
    </section>}
    <button type="button" onClick={() => setOpen((value) => !value)} aria-expanded={open} aria-label="فتح مساعدة الدعم" className="grid size-14 place-items-center rounded-2xl bg-[#0A1D3D] text-white shadow-xl transition-colors hover:bg-[#0E8F8F] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#0A1D3D]"><Headphones size={24}/></button>
  </div>;
}

function SupportContactLink({ launcherPositionClass, whatsappNumber, contactNumber }: { launcherPositionClass: string; whatsappNumber: string; contactNumber: string }) {
  const href = whatsappNumber ? `https://wa.me/${whatsappNumber}` : `tel:${contactNumber.replace(/\s/g, '')}`;
  const label = whatsappNumber ? 'التواصل مع الدعم عبر واتساب' : 'الاتصال بالدعم';

  return <a href={href} target={whatsappNumber ? '_blank' : undefined} rel={whatsappNumber ? 'noopener noreferrer' : undefined} dir="rtl" aria-label={label} className={`fixed ${launcherPositionClass} left-2 z-[var(--z-floating)] inline-flex size-12 items-center justify-center gap-2 rounded-full bg-[#0A1D3D] p-0 text-sm font-black text-white shadow-xl transition-colors hover:bg-[#0E8F8F] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#0A1D3D] sm:left-6 sm:h-auto sm:w-auto sm:rounded-2xl sm:px-4 sm:py-3`}>
    <Headphones size={22}/><span className="sr-only sm:not-sr-only">{whatsappNumber ? 'واتساب الدعم' : 'اتصل بالدعم'}</span>
  </a>;
}

function isForbiddenOrConflict(cause: unknown) {
  const status = (cause as { response?: { status?: number } }).response?.status;
  return status === 403 || status === 409;
}

function normalizeAvailability(value: LiveSupportAvailability): LiveSupportAvailability {
  if (typeof value?.isAvailable !== 'boolean' || typeof value?.availableStaffCount !== 'number') {
    return {
      isAvailable: false,
      availableStaffCount: 0,
      nextAvailableAt: null,
      code: 'INVALID_AVAILABILITY',
      message: 'تعذر التحقق من حالة الدعم',
    };
  }
  return value;
}

function mergeMessages(current: LiveSupportMessage[], incoming: LiveSupportMessage[], conversationId: string) {
  const byId = new Map(current.filter((item) => item.conversationId === conversationId).map((item) => [item.id, item]));
  for (const item of incoming) {
    if (item.conversationId === conversationId) byId.set(item.id, item);
  }
  return [...byId.values()].sort((left, right) => Date.parse(left.sentAt) - Date.parse(right.sentAt));
}

function isAbortError(cause: unknown) {
  return (typeof DOMException !== 'undefined' && cause instanceof DOMException && cause.name === 'AbortError')
    || (typeof cause === 'object' && cause !== null && 'code' in cause && (cause as { code?: string }).code === 'ERR_CANCELED');
}

function getParticipantMutationError(cause: unknown, fallback: string) {
  const response = (cause as { response?: { status?: number; data?: { message?: unknown } } }).response;
  if (response?.status === 409) return 'تغيرت حالة المحادثة. احتفظ بالمسودة، حدّث السجل، ثم أعد المحاولة.';
  if (response?.status === 403) return 'لم تعد تملك صلاحية هذه المحادثة. افتح سجل المحادثات واختر محادثة متاحة.';
  return typeof response?.data?.message === 'string' ? response.data.message : fallback;
}

function ClosedActions({ conversation, onNew }: { conversation: LiveSupportConversation; onNew: () => void }) {
  const [rated, setRated] = useState(!conversation.canRate);
  const [submittedStars, setSubmittedStars] = useState<number>();

  useEffect(() => {
    setRated(!conversation.canRate);
    setSubmittedStars(undefined);
  }, [conversation.id, conversation.canRate]);

  return <div className="space-y-3 border-t border-slate-100 pt-3">
    {!rated && (
      <ConversationRating
        conversationId={conversation.id}
        onRated={(stars) => {
          setRated(true);
          setSubmittedStars(stars);
        }}
      />
    )}
    {submittedStars && <p role="status" className="rounded-xl bg-cyan-50 px-3 py-2 text-center text-sm font-semibold text-cyan-900">شكرًا لتقييمك {submittedStars} من 5 نجوم.</p>}
    <button type="button" onClick={onNew} className="h-11 w-full rounded-xl bg-slate-900 font-semibold text-white">محادثة جديدة</button>
  </div>;
}
