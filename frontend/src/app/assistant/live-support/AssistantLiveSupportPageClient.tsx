'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { LoaderCircle, MessageSquareText, Settings2 } from 'lucide-react';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { liveSupportService, type LiveSupportCannedReply, type LiveSupportConversation, type LiveSupportMessage, type LiveSupportStaffBootstrap } from '@/services/live-support-service';
import { StudentContextPanel } from '@/components/live-support/student-context/StudentContextPanel';
import { useLiveSupportHub } from '@/hooks/useLiveSupportHub';
import { StaffStatusHeader } from '@/components/live-support/staff/StaffStatusHeader';
import { ConversationQueueList } from '@/components/live-support/staff/ConversationQueueList';
import { StaffConversationWorkspace } from '@/components/live-support/staff/StaffConversationWorkspace';
import { StaffConversationLayout } from '@/components/live-support/staff/StaffConversationLayout';
import { StaffCannedRepliesDialog } from '@/components/live-support/staff/StaffCannedRepliesDialog';
import { StaffChatSettings } from '@/components/live-support/staff/StaffChatSettings';
import { playLiveSupportSound, useLiveSupportPreferences } from '@/hooks/useLiveSupportPreferences';
import { useLiveSupportStore } from '@/stores/live-support-store';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { acquireMutationLock, releaseMutationLock } from '@/lib/conversation-mutation-lock';
import { createClientId } from '@/lib/client-id';
import { AdminModal } from '@/components/ui/admin-modal';

export default function AssistantLiveSupportPageClient() {
  const [bootstrap, setBootstrap] = useState<LiveSupportStaffBootstrap>();
  const [selected, setSelected] = useState<LiveSupportConversation>();
  const [messages, setMessages] = useState<LiveSupportMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [participantDraft, setParticipantDraft] = useState<string | null>(null);
  const [error, setError] = useState('');
  const [needsStaffActivation, setNeedsStaffActivation] = useState(false);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [messagesError, setMessagesError] = useState('');
  const [pendingAction, setPendingAction] = useState<'send' | 'close' | 'transfer' | null>(null);
  const [uploading, setUploading] = useState(false);
  const [personalReplies, setPersonalReplies] = useState<LiveSupportCannedReply[]>([]);
  const [isRepliesDialogOpen, setIsRepliesDialogOpen] = useState(false);
  const [repliesSaving, setRepliesSaving] = useState(false);
  const [repliesError, setRepliesError] = useState('');
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [replyFocusRequest, setReplyFocusRequest] = useState(0);
  const [conversationAction, setConversationAction] = useState<'close' | 'transfer' | null>(null);
  const [transferReason, setTransferReason] = useState('');
  const [actionValidationError, setActionValidationError] = useState('');
  const { preferences, updatePreferences } = useLiveSupportPreferences();
  const refreshGeneration = useRef(0);
  const selectionGeneration = useRef(0);
  const refreshAbort = useRef<AbortController | null>(null);
  const messagesAbort = useRef<AbortController | null>(null);
  const mutationInFlight = useRef(false);
  const typingClearTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const knownMessageIds = useRef<Record<string, Set<string>>>({});
  const knownConversationIds = useRef<Set<string> | undefined>(undefined);
  const selectedId = selected?.id;
  const selectedOwnerUserId = selected?.currentOwnerUserId;
  const ownershipLost = useLiveSupportStore(state => selectedId ? state.ownershipLost[selectedId] ?? false : false);
  const setOwnershipLost = useLiveSupportStore(state => state.setOwnershipLost);
  const drafts = useLiveSupportStore(state => state.drafts);
  const selectConversation = useLiveSupportStore(state => state.selectConversation);
  const setStoredDraft = useLiveSupportStore(state => state.setDraft);
  const clearStoredDraft = useLiveSupportStore(state => state.clearDraft);

  useEffect(() => {
    setDraft(selectedId ? drafts[selectedId] ?? '' : '');
  }, [drafts, selectedId]);

  const alertForIncomingMessages = useCallback((conversationId: string, nextMessages: LiveSupportMessage[]) => {
    const known = knownMessageIds.current[conversationId];
    if (!known) {
      knownMessageIds.current[conversationId] = new Set(nextMessages.map((message) => message.id));
      return;
    }
    const incoming = nextMessages.filter((message) => !known.has(message.id) && ['Student', 'Guest'].includes(message.senderType));
    nextMessages.forEach((message) => known.add(message.id));
    if (incoming.length === 0) return;
    if (preferences.soundEnabled) playLiveSupportSound(preferences.sound, preferences.soundVolume);
    if (preferences.notificationsEnabled && typeof Notification !== 'undefined' && Notification.permission === 'granted') {
      new Notification('رسالة جديدة في الدعم المباشر', { body: incoming.at(-1)?.content ?? 'وصلت رسالة جديدة من الطالب.' });
    }
  }, [preferences]);

  const alertForIncomingConversation = useCallback((conversations: LiveSupportConversation[]) => {
    const known = knownConversationIds.current;
    if (!known) {
      knownConversationIds.current = new Set(conversations.map((conversation) => conversation.id));
      return;
    }
    const incoming = conversations.filter((conversation) => !known.has(conversation.id));
    knownConversationIds.current = new Set(conversations.map((conversation) => conversation.id));
    if (incoming.length === 0) return;
    if (preferences.soundEnabled) playLiveSupportSound(preferences.sound, preferences.soundVolume);
    if (preferences.notificationsEnabled && typeof Notification !== 'undefined' && Notification.permission === 'granted') {
      const conversation = incoming.at(-1);
      new Notification('محادثة دعم جديدة', { body: conversation?.subject || 'وصلت محادثة جديدة من طالب.' });
    }
  }, [preferences]);

  const loadMessages = useCallback(async (conversationId: string, generation: number, showLoading = true) => {
    messagesAbort.current?.abort();
    const controller = new AbortController();
    messagesAbort.current = controller;
    if (showLoading) {
      setMessagesLoading(true);
      setMessagesError('');
    }
    try {
      const nextMessages = await liveSupportService.getStaffMessages(conversationId, controller.signal);
      if (generation === selectionGeneration.current) {
        alertForIncomingMessages(conversationId, nextMessages);
        setMessages(nextMessages);
        setBootstrap((current) => current ? {
          ...current,
          conversations: current.conversations.map((conversation) => conversation.id === conversationId
            ? { ...conversation, unreadParticipantMessageCount: 0 }
            : conversation),
        } : current);
      }
    } catch (cause) {
      if (isAbortError(cause)) return;
      if (showLoading && generation === selectionGeneration.current) setMessagesError('تعذر تحميل الرسائل. أعد المحاولة.');
    } finally {
      if (showLoading && generation === selectionGeneration.current) setMessagesLoading(false);
    }
  }, [alertForIncomingMessages]);

  const refresh = useCallback(async () => {
    const generation = ++refreshGeneration.current;
    refreshAbort.current?.abort();
    const controller = new AbortController();
    refreshAbort.current = controller;
    try {
      const next = await liveSupportService.getStaffBootstrap(controller.signal);
      if (generation !== refreshGeneration.current) return;
      alertForIncomingConversation(next.conversations);
      setBootstrap(next);
      setError('');
      setNeedsStaffActivation(false);
      const refreshedSelection = next.conversations.find((item) => item.id === selectedId);
      if (selectedId && (!refreshedSelection || refreshedSelection.currentOwnerUserId !== selectedOwnerUserId)) {
        setOwnershipLost(selectedId, true);
        selectionGeneration.current += 1;
        messagesAbort.current?.abort();
        selectConversation(undefined);
        setSelected(undefined);
        setMessages([]);
        setMessagesError('');
      }
      const current = refreshedSelection ?? next.conversations[0];
      setSelected(current);
      if (current) {
        setOwnershipLost(current.id, false);
        await loadMessages(current.id, selectionGeneration.current, current.id !== selectedId);
      }
    } catch (cause) {
      if (isAbortError(cause) || generation !== refreshGeneration.current) return;
      const message = (cause as { response?: { data?: { message?: string } } }).response?.data?.message ?? 'تعذر تحميل مركز الدعم.';
      const transient = !(cause as { response?: unknown }).response;
      if (transient) {
        window.setTimeout(() => void refresh(), 1_500);
        setError('جارٍ إعادة الاتصال بمركز الدعم…');
        return;
      }
      setError(message);
      setNeedsStaffActivation(message.includes('يستقبل محادثات') || message.includes('غير مفعّل للدعم'));
    }
  }, [alertForIncomingConversation, loadMessages, selectConversation, selectedId, selectedOwnerUserId, setOwnershipLost]);
  const showParticipantDraft = useCallback((preview: string | null) => {
    setParticipantDraft(preview);
    if (typingClearTimer.current) clearTimeout(typingClearTimer.current);
    typingClearTimer.current = setTimeout(() => setParticipantDraft(null), 2_000);
  }, []);
  const { connected } = useLiveSupportHub(selected?.id, () => void refresh(), showParticipantDraft);

  useEffect(() => {
    setParticipantDraft(null);
    return () => {
      if (typingClearTimer.current) clearTimeout(typingClearTimer.current);
    };
  }, [selectedId]);

  useEffect(() => {
    void refresh();
    const timer = setInterval(() => void refresh(), 15000);
    return () => clearInterval(timer);
  }, [refresh]);

  useEffect(() => {
    return registerCacheStore('support:staff', () => {}, () => void refresh());
  }, [refresh]);

  async function send(contentOverride?: string) {
    const conversationId = selected?.id;
    const generation = selectionGeneration.current;
    const value = (contentOverride ?? draft).trim();
    if (!conversationId || !value || ownershipLost || pendingAction) return;
    setPendingAction('send');
    setDraft('');
    setStoredDraft(conversationId, '');
    try {
      const message = await liveSupportService.sendStaffMessage(conversationId, { clientMessageId: createClientId(), content: value });
      if (generation === selectionGeneration.current && selected?.id === conversationId) {
        setMessages((items) => items.some((item) => item.id === message.id) ? items : [...items, message]);
        clearStoredDraft(conversationId);
      }
    } catch (cause) {
      if (generation === selectionGeneration.current && selected?.id === conversationId) {
        setDraft(value);
        setStoredDraft(conversationId, value);
        setError(getStaffMutationError(cause, 'تعذر إرسال الرسالة. أعد المحاولة.'));
      }
    } finally { setPendingAction(null); }
  }

  async function upload(file?: File): Promise<boolean> {
    const conversationId = selected?.id;
    const generation = selectionGeneration.current;
    if (!conversationId || !file || ownershipLost || pendingAction || uploading) return false;
    const isImage = file.type.startsWith('image/');
    const isAudio = file.type.startsWith('audio/');
    if (!isImage && !isAudio) {
      setError('اختر صورة بصيغة JPG أو PNG أو WebP، أو سجّل رسالة صوتية من زر التسجيل.');
      return false;
    }
    setUploading(true);
    setError('');
    try {
      const attachment = await liveSupportService.uploadStaffAttachment(conversationId, file);
      const message = await liveSupportService.sendStaffMessage(conversationId, {
        clientMessageId: createClientId(),
        type: isAudio ? 'Audio' : 'Image',
        content: file.name,
        attachmentId: attachment.id,
      });
      if (generation === selectionGeneration.current && selected?.id === conversationId) {
        setMessages((items) => items.some((item) => item.id === message.id) ? items : [...items, message]);
      }
      return true;
    } catch (cause) {
      setError(getStaffMutationError(cause, 'تعذر إرسال المرفق. استخدم صورة أو تسجيلًا صوتيًا بحجم لا يتجاوز 10 ميجابايت.'));
      return false;
    } finally {
      setUploading(false);
    }
  }

  async function editMessage(messageId: string, content: string) {
    if (!selected) return;
    try {
      const updated = await liveSupportService.updateStaffMessage(selected.id, messageId, content);
      setMessages((items) => items.map((message) => message.id === updated.id ? updated : message));
    } catch (cause) {
      setError(getStaffMutationError(cause, 'تعذر تعديل الرسالة. أعد المحاولة.'));
      throw cause;
    }
  }

  async function deleteMessage(messageId: string) {
    if (!selected) return;
    try {
      const deleted = await liveSupportService.deleteStaffMessage(selected.id, messageId);
      setMessages((items) => items.map((message) => message.id === deleted.id ? deleted : message));
    } catch (cause) {
      setError(getStaffMutationError(cause, 'تعذر حذف الرسالة. أعد المحاولة.'));
      throw cause;
    }
  }

  function useCannedReply(reply: LiveSupportCannedReply) {
    if (!selectedId || ownershipLost || pendingAction) return;
    if (reply.sendImmediately) { void send(reply.content); return; }
    setDraft(reply.content); setStoredDraft(selectedId, reply.content);
  }

  async function openRepliesDialog() {
    setRepliesError('');
    try {
      setPersonalReplies(await liveSupportService.getStaffCannedReplies());
      setIsRepliesDialogOpen(true);
    } catch (cause) {
      setError(getStaffMutationError(cause, 'تعذر تحميل ردودك الجاهزة. أعد المحاولة.'));
    }
  }

  async function savePersonalReplies() {
    if (personalReplies.some((reply) => !reply.title.trim() || !reply.content.trim())) {
      setRepliesError('أكمل عنوان ونص كل رد قبل الحفظ.');
      return;
    }
    setRepliesSaving(true);
    setRepliesError('');
    try {
      await liveSupportService.updateStaffCannedReplies(personalReplies);
      setIsRepliesDialogOpen(false);
      await refresh();
    } catch (cause) {
      setRepliesError(getStaffMutationError(cause, 'تعذر حفظ ردودك الجاهزة. أعد المحاولة.'));
    } finally {
      setRepliesSaving(false);
    }
  }

  async function close() {
    if (!selected || !acquireMutationLock(mutationInFlight)) return;
    setPendingAction('close');
    try { await liveSupportService.closeConversation(selected.id); setConversationAction(null); setSelected(undefined); setMessages([]); await refresh(); }
    catch (cause) { setError(getStaffMutationError(cause, 'تعذر إغلاق المحادثة. راجع الملكية وحاول مرة أخرى.')); }
    finally { releaseMutationLock(mutationInFlight); setPendingAction(null); }
  }

  async function transfer(reason: string) {
    if (!selected || !acquireMutationLock(mutationInFlight)) return;
    setPendingAction('transfer');
    try { await liveSupportService.transferConversation(selected.id, reason); setConversationAction(null); setTransferReason(''); setSelected(undefined); setMessages([]); await refresh(); }
    catch (cause) { setError(getStaffMutationError(cause, 'تعذر تحويل المحادثة. راجع الملكية وحاول مرة أخرى.')); }
    finally { releaseMutationLock(mutationInFlight); setPendingAction(null); }
  }

  function requestConversationAction(action: 'close' | 'transfer') {
    setActionValidationError('');
    setTransferReason('');
    setConversationAction(action);
  }

  function confirmTransfer() {
    const reason = transferReason.trim();
    if (reason.length < 5) {
      setActionValidationError('اكتب سببًا واضحًا من 5 أحرف على الأقل ليظهر للموظف التالي.');
      return;
    }
    void transfer(reason);
  }

  function selectStaffConversation(item: LiveSupportConversation) {
    selectionGeneration.current += 1;
    delete knownMessageIds.current[item.id];
    selectConversation(item.id);
    setSelected(item);
    setMessages([]);
    setMessagesError('');
    void loadMessages(item.id, selectionGeneration.current);
  }

  return <NavRouteGuard routePath="/assistant/live-support"><AssistantPage activePath="/assistant/live-support" sectionLabel="خدمة العملاء" pageTitle="مركز الدعم المباشر" subtitle="التوزيع يتم تلقائيًا حسب الحضور والحمل والحد الأقصى المحدد لكل موظف.">
    <StaffChatSettings open={settingsOpen} preferences={preferences} onClose={() => setSettingsOpen(false)} onChange={updatePreferences}/>
    {!bootstrap && !error ? <div className="grid min-h-80 place-items-center"><LoaderCircle className="animate-spin"/></div> : null}
    {error ? <div role="alert" className={`rounded-2xl p-5 ${needsStaffActivation ? 'border border-amber-200 bg-amber-50 text-amber-950' : 'border border-red-200 bg-red-50 text-red-800'}`}>
      <p className="font-bold">{needsStaffActivation ? 'الحساب لديه صلاحية، لكنه غير مضاف لتوزيع المحادثات' : error}</p>
      {needsStaffActivation && <ol className="mt-3 list-decimal space-y-1 pr-5 text-sm"><li>افتح لوحة الأدمن ثم «إدارة الدعم المباشر».</li><li>ابحث عن الموظف وفعّل «يستقبل محادثات» وحدد السعة والجدول، ثم اضغط حفظ.</li><li>ارجع هنا بعد تسجيل حضور الموظف.</li></ol>}
    </div> : null}
    {bootstrap && <div dir="rtl" className="space-y-3">
      <div className="flex flex-wrap items-center gap-2 rounded-xl bg-[var(--admin-card-soft)] pe-2">
        <div className="min-w-0 flex-1"><StaffStatusHeader state={bootstrap} connected={connected}/></div>
        <button type="button" onClick={() => setSettingsOpen((current) => !current)} className="inline-flex min-h-10 items-center gap-2 rounded-lg px-3 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"><Settings2 size={17}/>تفضيلات العرض</button>
        <button type="button" onClick={() => void openRepliesDialog()} className="inline-flex min-h-10 items-center gap-2 rounded-lg px-3 text-sm font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)]"><MessageSquareText size={17}/>الردود الجاهزة</button>
      </div>
      {!bootstrap.isCheckedIn && <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm font-medium text-amber-900">سجّل الحضور أولًا حتى تستقبل محادثات جديدة.</div>}
      <StaffConversationLayout
        workspaceFocusRequest={replyFocusRequest}
        queue={
          <ConversationQueueList
            conversations={bootstrap.conversations}
            selectedId={selected?.id}
            waitingCount={bootstrap.waitingCount}
            onSelect={selectStaffConversation}
          />
        }
        workspace={
          <StaffConversationWorkspace
            conversation={selected}
            messages={messages}
            draft={draft}
            participantDraft={participantDraft}
            ownershipLost={ownershipLost}
            pendingAction={pendingAction}
            messagesLoading={messagesLoading}
            messagesError={messagesError}
            onRetryMessages={() => selected && void loadMessages(selected.id, selectionGeneration.current)}
            onDraftChange={(value) => {
              if (selectedId) setStoredDraft(selectedId, value);
              setDraft(value);
            }}
            onSend={() => void send()}
            uploading={uploading}
            onUpload={(file) => upload(file)}
            onEditMessage={editMessage}
            onDeleteMessage={deleteMessage}
            onTransfer={() => requestConversationAction('transfer')}
            onClose={() => requestConversationAction('close')}
            cannedReplies={bootstrap.cannedReplies ?? []}
            onCannedReply={useCannedReply}
            preferences={preferences}
            replyFocusRequest={replyFocusRequest}
          />
        }
        context={selected ? <StudentContextPanel conversation={selected} onActionCompleted={() => setReplyFocusRequest((request) => request + 1)} onConversationChange={(updated) => { setSelected(updated); setBootstrap((current) => current ? { ...current, conversations: current.conversations.map((item) => item.id === updated.id ? updated : item) } : current); }}/> : undefined}
      />
    </div>}
    <StaffCannedRepliesDialog open={isRepliesDialogOpen} replies={personalReplies} saving={repliesSaving} error={repliesError} onClose={() => { if (!repliesSaving) setIsRepliesDialogOpen(false); }} onChange={setPersonalReplies} onSave={() => void savePersonalReplies()} />
    <AdminModal open={conversationAction === 'transfer'} onClose={() => !pendingAction && setConversationAction(null)} title="تحويل المحادثة" size="sm">
      <p className="mb-4 text-sm leading-6 text-[var(--admin-muted)]">ستعود المحادثة إلى التوزيع ليكملها موظف آخر. اكتب سببًا مختصرًا يساعده على المتابعة دون سؤال الطالب من البداية.</p>
      <label className="block text-sm font-bold text-[var(--admin-text)]" htmlFor="transfer-reason">سبب التحويل</label>
      <textarea id="transfer-reason" autoFocus value={transferReason} onChange={(event) => { setTransferReason(event.target.value); setActionValidationError(''); }} rows={4} className="mt-2 w-full resize-none rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 text-[var(--admin-text)] outline-none focus-visible:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-15)]" placeholder="مثال: يحتاج مراجعة مسؤول المدفوعات" />
      {actionValidationError ? <p role="alert" className="mt-2 text-sm font-medium text-[var(--admin-danger)]">{actionValidationError}</p> : null}
      <div className="mt-5 flex justify-end gap-2">
        <button type="button" disabled={Boolean(pendingAction)} onClick={() => setConversationAction(null)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 font-bold text-[var(--admin-text)]">إلغاء</button>
        <button type="button" disabled={Boolean(pendingAction)} onClick={confirmTransfer} className="min-h-11 rounded-xl bg-[var(--admin-primary)] px-4 font-bold text-white disabled:opacity-50">{pendingAction === 'transfer' ? 'جارٍ التحويل…' : 'تحويل المحادثة'}</button>
      </div>
    </AdminModal>
    <AdminModal open={conversationAction === 'close'} onClose={() => !pendingAction && setConversationAction(null)} title="إنهاء المحادثة" size="sm">
      <p className="text-sm leading-6 text-[var(--admin-muted)]">سيتم إنهاء المحادثة وإزالتها من قائمة المحادثات النشطة. سيظل سجلها محفوظًا في ملف الطالب.</p>
      <div className="mt-5 flex justify-end gap-2">
        <button type="button" disabled={Boolean(pendingAction)} onClick={() => setConversationAction(null)} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-4 font-bold text-[var(--admin-text)]">العودة للمحادثة</button>
        <button type="button" disabled={Boolean(pendingAction)} onClick={() => void close()} className="min-h-11 rounded-xl bg-[var(--admin-danger)] px-4 font-bold text-white disabled:opacity-50">{pendingAction === 'close' ? 'جارٍ الإنهاء…' : 'إنهاء المحادثة'}</button>
      </div>
    </AdminModal>
  </AssistantPage></NavRouteGuard>;
}

function isAbortError(cause: unknown) {
  return (typeof DOMException !== 'undefined' && cause instanceof DOMException && cause.name === 'AbortError')
    || (typeof cause === 'object' && cause !== null && 'code' in cause && (cause as { code?: string }).code === 'ERR_CANCELED');
}

function getStaffMutationError(cause: unknown, fallback: string) {
  const response = (cause as { response?: { status?: number; data?: { message?: unknown } } }).response;
  if (response?.status === 409) return 'تغيرت حالة المحادثة أو ملكيتها. حدّث القائمة ثم اختر المحادثة من جديد.';
  if (response?.status === 403) return 'لم تعد تملك صلاحية هذه المحادثة. اختر محادثة مسندة إليك.';
  return typeof response?.data?.message === 'string' ? response.data.message : fallback;
}
