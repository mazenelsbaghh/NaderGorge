'use client';

import {
  Fragment,
  type FormEvent,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { MessageCircle, Send, UserRoundCog, X } from 'lucide-react';

import {
  LiveSupportMessageContent,
  LiveSupportMessageMeta,
} from '@/components/live-support/LiveSupportMessageContent';
import { WhatsAppTemplatePicker } from '@/components/live-support/staff/WhatsAppTemplatePicker';
import { LiveSupportChannelBadge } from '@/components/live-support/shared/LiveSupportChannelBadge';
import { AccessibleOverlay } from '@/components/ui/AccessibleOverlay';
import { useLiveSupportHub } from '@/hooks/useLiveSupportHub';
import { formatCairoTimestamp } from '@/lib/cairo-time';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { createClientId } from '@/lib/client-id';
import { resolveLiveSupportChannelCapabilities } from '@/lib/live-support-channel';
import {
  advanceLiveSupportThreadHistory,
  createLiveSupportThreadPagination,
  mergeOrderedLiveSupportMessages,
  reconcileLiveSupportThreadHead,
  type LiveSupportThreadPagination,
} from '@/lib/live-support-message-pages';
import {
  getLiveSupportApiError,
  liveSupportService,
  type LiveSupportConversationTimeline,
  type LiveSupportMessage,
  type LiveSupportStaffConfig,
  type LiveSupportWhatsAppTemplate,
} from '@/services/live-support-service';

interface ConversationInvestigationProps {
  timeline: LiveSupportConversationTimeline;
  staff?: LiveSupportStaffConfig[];
  close: () => void;
}

export function ConversationInvestigation({
  timeline,
  staff = [],
  close,
}: ConversationInvestigationProps) {
  const [messages, setMessages] = useState<LiveSupportMessage[]>([]);
  const [draft, setDraft] = useState('');
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');
  const [olderMessagesCursor, setOlderMessagesCursor] = useState<string>();
  const [olderMessagesLoading, setOlderMessagesLoading] = useState(false);
  const [olderMessagesError, setOlderMessagesError] = useState('');
  const [threadHistoryGap, setThreadHistoryGap] = useState(false);
  const [eventFilter, setEventFilter] = useState('all');
  const [intervening, setIntervening] = useState(false);
  const [participantDraft, setParticipantDraft] = useState<string | null>(null);
  const [reassignOpen, setReassignOpen] = useState(false);
  const [targetStaffUserId, setTargetStaffUserId] = useState('');
  const [reassignReason, setReassignReason] = useState('');
  const [currentTime, setCurrentTime] = useState(() => Date.now());
  const messagesViewportRef = useRef<HTMLDivElement>(null);
  const typingClearTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const messagesAbort = useRef<AbortController | null>(null);
  const olderMessagesAbort = useRef<AbortController | null>(null);
  const messageViewGeneration = useRef(0);
  const headRequestGeneration = useRef(0);
  const threadPagination = useRef(createLiveSupportThreadPagination());
  const shouldStickToBottom = useRef(true);
  const prependScrollAnchor = useRef<{
    messageId: string;
    viewportOffset: number;
  } | null>(null);
  const conversation = timeline.conversation;
  const isWhatsApp = conversation.channel === 'WhatsApp';
  const isMessenger = conversation.channel === 'Messenger';
  const canSend =
    conversation.status !== 'Closed' && conversation.status !== 'Abandoned';
  const channelCapabilities = resolveLiveSupportChannelCapabilities(
    { ...conversation, canSend },
    currentTime
  );
  const useWhatsAppThread = isWhatsApp;
  const externalWindowOpen =
    channelCapabilities.customerServiceWindowOpen === true;
  const canSendText = channelCapabilities.canSendFreeform;
  const eligibleStaff = useMemo(
    () => staff.filter((item) => item.isEnabled),
    [staff]
  );

  const updateThreadPagination = useCallback((pagination: LiveSupportThreadPagination) => {
    threadPagination.current = pagination;
    setOlderMessagesCursor(pagination.cursor);
    setThreadHistoryGap(pagination.resumePoints.length > 0);
  }, []);

  const refreshMessages = useCallback(async () => {
    const viewGeneration = messageViewGeneration.current;
    const requestGeneration = ++headRequestGeneration.current;
    messagesAbort.current?.abort();
    const controller = new AbortController();
    messagesAbort.current = controller;
    try {
      if (useWhatsAppThread) {
        const page = await liveSupportService.getStaffWhatsAppThreadMessages(
          conversation.id,
          undefined,
          controller.signal
        );
        if (
          viewGeneration !== messageViewGeneration.current ||
          requestGeneration !== headRequestGeneration.current ||
          controller.signal.aborted ||
          messagesAbort.current !== controller
        ) return;
        updateThreadPagination(reconcileLiveSupportThreadHead(
          threadPagination.current,
          page,
        ));
        setOlderMessagesError('');
        setMessages((current) =>
          mergeOrderedLiveSupportMessages(current, page.items)
        );
      } else {
        const result = await liveSupportService.getStaffMessages(
          conversation.id,
          controller.signal
        );
        if (
          viewGeneration !== messageViewGeneration.current ||
          requestGeneration !== headRequestGeneration.current ||
          controller.signal.aborted ||
          messagesAbort.current !== controller
        ) return;
        setMessages(result);
      }
      setError('');
    } catch (cause) {
      if (
        !isAbortError(cause) &&
        viewGeneration === messageViewGeneration.current &&
        requestGeneration === headRequestGeneration.current
      ) {
        setError('تعذر تحميل رسائل المحادثة.');
      }
    } finally {
      if (
        messagesAbort.current === controller &&
        viewGeneration === messageViewGeneration.current &&
        requestGeneration === headRequestGeneration.current
      ) {
        messagesAbort.current = null;
        setLoading(false);
      }
    }
  }, [conversation.id, updateThreadPagination, useWhatsAppThread]);

  const loadOlderMessages = useCallback(async () => {
    const cursor = threadPagination.current.cursor;
    if (!useWhatsAppThread || !cursor || olderMessagesAbort.current) return;
    const viewGeneration = messageViewGeneration.current;
    const controller = new AbortController();
    olderMessagesAbort.current = controller;
    setOlderMessagesLoading(true);
    setOlderMessagesError('');
    try {
      const page = await liveSupportService.getStaffWhatsAppThreadMessages(
        conversation.id,
        cursor,
        controller.signal
      );
      if (viewGeneration !== messageViewGeneration.current) return;
      const advancement = advanceLiveSupportThreadHistory(
        threadPagination.current,
        cursor,
        page,
      );
      if (!advancement.stale) {
        updateThreadPagination(advancement.pagination);
        setOlderMessagesError(advancement.historyGapUnresolved
          ? 'لم يكتمل ربط أجزاء السجل بعد. أعد المحاولة لاستكمال الرسائل الناقصة.'
          : '');
      }
      setMessages((current) =>
        mergeOrderedLiveSupportMessages(current, page.items)
      );
    } catch (cause) {
      if (
        !isAbortError(cause) &&
        viewGeneration === messageViewGeneration.current
      ) {
        setOlderMessagesError(
          'تعذر تحميل الرسائل الأقدم. أعد المحاولة.'
        );
      }
    } finally {
      if (olderMessagesAbort.current === controller) {
        olderMessagesAbort.current = null;
        if (viewGeneration === messageViewGeneration.current) {
          setOlderMessagesLoading(false);
        }
      }
    }
  }, [conversation.id, updateThreadPagination, useWhatsAppThread]);

  const showParticipantDraft = useCallback((preview: string | null) => {
    setParticipantDraft(preview);
    if (typingClearTimer.current) clearTimeout(typingClearTimer.current);
    typingClearTimer.current = setTimeout(
      () => setParticipantDraft(null),
      2_000
    );
  }, []);

  useLiveSupportHub(
    conversation.id,
    () => void refreshMessages(),
    showParticipantDraft
  );

  useEffect(() => {
    messageViewGeneration.current += 1;
    headRequestGeneration.current += 1;
    messagesAbort.current?.abort();
    olderMessagesAbort.current?.abort();
    messagesAbort.current = null;
    olderMessagesAbort.current = null;
    threadPagination.current = createLiveSupportThreadPagination();
    shouldStickToBottom.current = true;
    prependScrollAnchor.current = null;
    setMessages([]);
    setOlderMessagesCursor(undefined);
    setOlderMessagesLoading(false);
    setOlderMessagesError('');
    setThreadHistoryGap(false);
    setError('');
    setLoading(true);
    void refreshMessages();
    return () => {
      messagesAbort.current?.abort();
      olderMessagesAbort.current?.abort();
      if (typingClearTimer.current) clearTimeout(typingClearTimer.current);
    };
  }, [refreshMessages]);

  useEffect(
    () =>
      registerCacheStore(
        'support:staff',
        () => {},
        () => void refreshMessages()
      ),
    [refreshMessages]
  );

  useEffect(() => {
    const timer = window.setInterval(() => setCurrentTime(Date.now()), 60_000);
    return () => window.clearInterval(timer);
  }, []);

  useEffect(() => {
    if (!shouldStickToBottom.current) return;
    const frame = requestAnimationFrame(() => {
      const viewport = messagesViewportRef.current;
      if (viewport) viewport.scrollTop = viewport.scrollHeight;
    });
    return () => cancelAnimationFrame(frame);
  }, [messages.length, participantDraft]);

  useLayoutEffect(() => {
    const anchor = prependScrollAnchor.current;
    const viewport = messagesViewportRef.current;
    if (!anchor || olderMessagesLoading || !viewport) return;
    const anchorMessage = viewport.querySelector<HTMLElement>(
      `[data-live-support-message-id="${anchor.messageId}"]`
    );
    if (anchorMessage) {
      const nextOffset =
        anchorMessage.getBoundingClientRect().top -
        viewport.getBoundingClientRect().top;
      viewport.scrollTop += nextOffset - anchor.viewportOffset;
    }
    prependScrollAnchor.current = null;
  }, [messages.length, olderMessagesLoading]);

  function requestOlderMessages() {
    const viewport = messagesViewportRef.current;
    if (!viewport || olderMessagesLoading || !olderMessagesCursor) return;
    shouldStickToBottom.current = false;
    const viewportTop = viewport.getBoundingClientRect().top;
    const anchorMessage = [
      ...viewport.querySelectorAll<HTMLElement>(
        '[data-live-support-message-id]'
      ),
    ].find(
      (element) => element.getBoundingClientRect().bottom > viewportTop
    );
    prependScrollAnchor.current = anchorMessage
      ? {
          messageId: anchorMessage.dataset.liveSupportMessageId ?? '',
          viewportOffset:
            anchorMessage.getBoundingClientRect().top - viewportTop,
        }
      : null;
    void loadOlderMessages();
  }

  async function sendMessage(event: FormEvent) {
    event.preventDefault();
    const content = draft.trim();
    if (!content || sending || !canSendText) return;
    setSending(true);
    setError('');
    try {
      const message = await liveSupportService.sendStaffMessage(
        conversation.id,
        {
          clientMessageId: createClientId(),
          content,
        }
      );
      appendMessage(message);
      setDraft('');
    } catch (cause) {
      setError(
        getLiveSupportApiError(
          cause,
          'تعذر إرسال الرسالة. تحقق أن المحادثة ما زالت مفتوحة.'
        )
      );
    } finally {
      setSending(false);
    }
  }

  async function sendTemplate(
    template: LiveSupportWhatsAppTemplate,
    parameters: string[],
    previewText: string
  ) {
    if (!isWhatsApp || !canSend || sending) return;
    setSending(true);
    setError('');
    try {
      const message = await liveSupportService.sendWhatsAppTemplate(
        conversation.id,
        {
          clientMessageId: createClientId(),
          templateId: template.id,
          parameters,
          previewText,
        }
      );
      appendMessage(message);
    } catch (cause) {
      setError(getLiveSupportApiError(cause, 'تعذر إرسال قالب واتساب.'));
      throw cause;
    } finally {
      setSending(false);
    }
  }

  function appendMessage(message: LiveSupportMessage) {
    setMessages((current) =>
      mergeOrderedLiveSupportMessages(current, [message])
    );
  }

  async function intervene(operation: 'close' | 'queue') {
    const reason =
      operation === 'close'
        ? 'إغلاق إداري مباشر'
        : window.prompt('اكتب سبب إعادتها للطابور');
    if (!reason?.trim()) return;
    setIntervening(true);
    setError('');
    try {
      await liveSupportService.intervene(
        conversation.id,
        operation,
        reason.trim()
      );
      close();
    } catch (cause) {
      setError(
        getLiveSupportApiError(
          cause,
          'تعذر تنفيذ تدخل الإدارة. حدّث المحادثة ثم حاول مرة أخرى.'
        )
      );
    } finally {
      setIntervening(false);
    }
  }

  function openReassign() {
    const firstAvailable = eligibleStaff.find(isStaffAvailable);
    setTargetStaffUserId(firstAvailable?.userId ?? '');
    setReassignReason('');
    setError('');
    setReassignOpen(true);
  }

  async function reassignConversation() {
    const reason = reassignReason.trim();
    if (!targetStaffUserId) {
      setError('اختر موظفًا متاحًا لإعادة التعيين.');
      return;
    }
    if (reason.length < 3) {
      setError('اكتب سببًا واضحًا لإعادة التعيين من 3 أحرف على الأقل.');
      return;
    }
    setIntervening(true);
    setError('');
    try {
      await liveSupportService.intervene(
        conversation.id,
        'reassign',
        reason,
        targetStaffUserId
      );
      close();
    } catch (cause) {
      setError(
        getLiveSupportApiError(
          cause,
          'تعذر إعادة تعيين المحادثة. قد يكون الموظف غير متاح أو وصل إلى سعته.'
        )
      );
    } finally {
      setIntervening(false);
    }
  }

  return (
    <AccessibleOverlay
      open
      onClose={close}
      label="متابعة المحادثة"
      backdropClassName="bg-[color-mix(in_srgb,var(--admin-primary)_72%,transparent)]"
      className="inset-x-3 top-1/2 mx-auto flex h-[calc(100dvh-1.5rem)] max-w-6xl -translate-y-1/2 flex-col overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-[var(--admin-shadow)] sm:inset-x-4 sm:h-[min(52rem,calc(100dvh-2rem))]"
    >
      <div className="flex min-h-0 flex-1 flex-col" dir="rtl">
        <header className="flex items-start justify-between gap-3 border-b border-[var(--admin-border)] px-4 py-3 sm:px-5 sm:py-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="font-bold text-[var(--admin-text)]">
                محادثة {conversation.participantName}
              </h2>
              <LiveSupportChannelBadge
                channel={conversation.channel}
                externalPageName={conversation.externalPageName}
              />
              {channelCapabilities.isHumanOnly ? (
                <span className="rounded-full border border-[var(--admin-border)] px-2 py-0.5 text-xs font-bold text-[var(--admin-muted)]">
                  موظفون فقط
                </span>
              ) : null}
            </div>
            <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">
              {conversation.ownerName
                ? `المسؤول الآن: ${conversation.ownerName}`
                : 'في انتظار الاستلام'}{' '}
              · {conversation.status}
              {isWhatsApp && conversation.externalPhoneNumber ? (
                <>
                  {' '}
                  · <bdi dir="ltr">{conversation.externalPhoneNumber}</bdi>
                </>
              ) : null}
            </p>
            {channelCapabilities.usesExternalThread ? (
              <p
                className={`mt-1 text-xs font-bold ${externalWindowOpen ? 'text-[var(--admin-success)]' : 'text-[var(--admin-warning)]'}`}
              >
                {externalWindowOpen ? (
                  <>
                    نافذة الرد مفتوحة حتى{' '}
                    <time
                      dateTime={
                        conversation.customerServiceWindowExpiresAt ?? undefined
                      }
                    >
                      {formatCairoTimestamp(
                        conversation.customerServiceWindowExpiresAt!
                      )}
                    </time>
                  </>
                ) : (
                  isWhatsApp
                    ? 'نافذة الرد النصي منتهية — الإرسال متاح بالقالب فقط'
                    : 'نافذة الرد منتهية — انتظر رسالة جديدة من العميل'
                )}
              </p>
            ) : null}
          </div>
          <button
            type="button"
            onClick={close}
            aria-label="إغلاق"
            className="grid size-11 shrink-0 place-items-center rounded-xl text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-accent)]"
          >
            <X />
          </button>
        </header>

        {canSend ? (
          <div className="flex flex-wrap items-center gap-2 border-b border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] px-4 py-3 sm:px-5">
            <span className="ml-auto text-sm font-semibold text-[var(--admin-text)]">
              تدخل إداري مسجل بالكامل
            </span>
            <button
              type="button"
              disabled={intervening}
              onClick={() => void intervene('queue')}
              className="min-h-10 rounded-lg border border-[var(--admin-warning-20)] px-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-card)] disabled:opacity-50"
            >
              إعادة للطابور
            </button>
            <button
              type="button"
              disabled={intervening || !eligibleStaff.some(isStaffAvailable)}
              title={
                eligibleStaff.some(isStaffAvailable)
                  ? undefined
                  : 'لا يوجد موظف دعم متاح حاليًا'
              }
              onClick={openReassign}
              className="inline-flex min-h-10 items-center gap-2 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-hover)] disabled:opacity-50"
            >
              <UserRoundCog aria-hidden="true" size={16} />
              إعادة تعيين
            </button>
            <button
              type="button"
              disabled={intervening}
              onClick={() => void intervene('close')}
              className="min-h-10 rounded-lg bg-[var(--admin-danger)] px-3 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50"
            >
              إغلاق إداري
            </button>
          </div>
        ) : null}

        {reassignOpen ? (
          <section
            aria-label="إعادة تعيين المحادثة"
            className="grid gap-3 border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-4 sm:grid-cols-[minmax(13rem,.7fr)_minmax(16rem,1.3fr)_auto] sm:items-end sm:px-5"
          >
            <label className="text-xs font-bold text-[var(--admin-text)]">
              الموظف الجديد
              <select
                value={targetStaffUserId}
                onChange={(event) => setTargetStaffUserId(event.target.value)}
                disabled={intervening}
                className="mt-1 h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"
              >
                <option value="">اختر موظفًا متاحًا</option>
                {eligibleStaff.map((item) => (
                  <option
                    key={item.userId}
                    value={item.userId}
                    disabled={!isStaffAvailable(item)}
                  >
                    {item.staffName} · {item.activeLoad}/
                    {item.maxActiveConversations}
                    {!item.isCheckedIn
                      ? ' · غير حاضر'
                      : item.activeLoad >= item.maxActiveConversations
                        ? ' · مكتمل السعة'
                        : ''}
                  </option>
                ))}
              </select>
            </label>
            <label className="text-xs font-bold text-[var(--admin-text)]">
              سبب إعادة التعيين
              <input
                value={reassignReason}
                onChange={(event) => setReassignReason(event.target.value)}
                disabled={intervening}
                maxLength={500}
                placeholder="مثال: متابعة مسؤول المدفوعات"
                className="mt-1 h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-sm font-normal text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)]"
              />
            </label>
            <div className="flex gap-2">
              <button
                type="button"
                disabled={intervening}
                onClick={() => setReassignOpen(false)}
                className="min-h-11 rounded-xl border border-[var(--admin-border)] px-3 text-sm font-bold text-[var(--admin-text)]"
              >
                إلغاء
              </button>
              <button
                type="button"
                disabled={
                  intervening ||
                  !targetStaffUserId ||
                  reassignReason.trim().length < 3
                }
                onClick={() => void reassignConversation()}
                className="min-h-11 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50"
              >
                {intervening ? 'جارٍ التعيين…' : 'تأكيد التعيين'}
              </button>
            </div>
          </section>
        ) : null}

        <div className="grid min-h-0 flex-1 grid-rows-[minmax(0,1fr)_auto] lg:grid-cols-[1.45fr_.55fr] lg:grid-rows-1">
          <div className="flex min-h-0 flex-col border-[var(--admin-border)] lg:border-l">
            <div
              ref={messagesViewportRef}
              className="min-h-0 flex-1 space-y-3 overflow-y-auto bg-[var(--admin-card-soft)] p-3 sm:p-4"
              aria-live="polite"
              aria-label="سجل رسائل المحادثة"
              onScroll={(event) => {
                const viewport = event.currentTarget;
                shouldStickToBottom.current =
                  viewport.scrollHeight -
                    viewport.scrollTop -
                    viewport.clientHeight <
                  80;
              }}
            >
              {loading ? (
                <div
                  className="grid h-full place-items-center gap-3"
                  aria-label="جارٍ تحميل الرسائل"
                >
                  <div className="h-12 w-2/3 animate-pulse rounded-xl bg-[var(--admin-card-strong)]" />
                  <div className="mr-auto h-16 w-1/2 animate-pulse rounded-xl bg-[var(--admin-card-strong)]" />
                </div>
              ) : messages.length === 0 ? (
                <p className="grid h-full place-items-center text-sm text-[var(--admin-muted)]">
                  لا توجد رسائل بعد.
                </p>
              ) : (
                <>
                  {useWhatsAppThread &&
                  (olderMessagesCursor ||
                    olderMessagesLoading ||
                    olderMessagesError) ? (
                    <div className={`flex flex-col items-center gap-2 pb-2 ${threadHistoryGap ? 'sticky top-2 z-10 mx-auto rounded-2xl bg-[var(--admin-warning-10)] px-2 pt-2 shadow-sm' : ''}`}>
                      {olderMessagesCursor ? (
                        <button
                          type="button"
                          disabled={olderMessagesLoading}
                          onClick={requestOlderMessages}
                          className="min-h-10 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 text-xs font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] disabled:opacity-60"
                        >
                          {olderMessagesLoading
                            ? 'جارٍ تحميل الرسائل الأقدم…'
                            : threadHistoryGap
                              ? 'استكمال الرسائل الناقصة'
                              : 'تحميل الرسائل الأقدم'}
                        </button>
                      ) : null}
                      {olderMessagesError ? (
                        <div
                          role="alert"
                          className="text-center text-xs text-[var(--admin-danger)]"
                        >
                          <p>{olderMessagesError}</p>
                          <button
                            type="button"
                            onClick={requestOlderMessages}
                            className="mt-1 font-bold underline"
                          >
                            إعادة المحاولة
                          </button>
                        </div>
                      ) : null}
                    </div>
                  ) : null}
                  {messages.map((message, index) => {
                    const fromTeam = [
                      'Staff',
                      'Admin',
                      'System',
                      'AI',
                    ].includes(message.senderType);
                    const previousConversationId =
                      messages[index - 1]?.conversationId;
                    const showEpisodeBoundary =
                      useWhatsAppThread &&
                      message.conversationId !== previousConversationId &&
                      (index > 0 || message.conversationId !== conversation.id);
                    return (
                      <Fragment key={message.id}>
                        {showEpisodeBoundary ? (
                          <div
                            role="separator"
                            aria-label={
                              message.conversationId === conversation.id
                                ? 'بداية المحادثة المحددة'
                                : 'بداية جلسة واتساب أخرى'
                            }
                            className="flex items-center gap-2 py-2 text-xs font-bold text-[var(--admin-muted)]"
                          >
                            <span className="h-px flex-1 bg-[var(--admin-border)]" />
                            <span>
                              {message.conversationId === conversation.id
                                ? 'المحادثة المحددة'
                                : 'جلسة واتساب أخرى'}
                            </span>
                            <span className="h-px flex-1 bg-[var(--admin-border)]" />
                          </div>
                        ) : null}
                        <article
                          dir="auto"
                          data-live-support-message-id={message.id}
                          className={`max-w-[86%] break-words [overflow-wrap:anywhere] rounded-2xl px-4 py-3 sm:max-w-[78%] ${fromTeam ? 'mr-auto bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'ml-auto border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)]'}`}
                        >
                          <p className="mb-1 text-xs font-bold opacity-80">
                            {message.senderDisplayName ||
                              senderLabel(message.senderType)}
                          </p>
                          <LiveSupportMessageContent
                            message={message}
                            audience="staff"
                            staffWhatsAppThreadConversationId={
                              useWhatsAppThread
                                ? conversation.id
                                : undefined
                            }
                          />
                          <LiveSupportMessageMeta
                            message={message}
                            audience="staff"
                          />
                        </article>
                      </Fragment>
                    );
                  })}
                </>
              )}
              {channelCapabilities.supportsParticipantTypingPreview && participantDraft !== null ? (
                <article className="ml-auto max-w-[82%] rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-primary-15)] px-4 py-3 text-sm text-[var(--admin-text)]">
                  <p className="mb-1 text-xs font-bold text-[var(--admin-primary)]">
                    الطالب يكتب الآن…
                  </p>
                  <p className="whitespace-pre-wrap break-words">
                    {participantDraft || '…'}
                  </p>
                </article>
              ) : null}
            </div>

            <div className="border-t border-[var(--admin-border)] bg-[var(--admin-card)] p-3 sm:p-4">
              {error ? (
                <p
                  role="alert"
                  className="mb-3 rounded-lg bg-[var(--admin-danger-10)] px-3 py-2 text-sm font-medium text-[var(--admin-danger)]"
                >
                  {error}
                </p>
              ) : null}
              {isWhatsApp && canSend ? (
                <WhatsAppTemplatePicker
                  disabled={sending || intervening}
                  onSend={sendTemplate}
                />
              ) : null}
              {channelCapabilities.usesExternalThread && !externalWindowOpen && canSend ? (
                <p
                  role="status"
                  className="mb-3 rounded-lg bg-[var(--admin-warning-10)] px-3 py-2 text-sm font-medium text-[var(--admin-warning)]"
                >
                  <MessageCircle
                    aria-hidden="true"
                    className="ml-1 inline"
                    size={16}
                  />
                  {isWhatsApp
                    ? 'انتهت نافذة الرد النصي. اختر قالب واتساب معتمدًا لإعادة فتحها.'
                    : 'انتهت نافذة الرد المتاحة لهذه الصفحة. انتظر رسالة جديدة من العميل قبل الرد.'}
                </p>
              ) : null}
              <form onSubmit={sendMessage}>
                <div className="flex gap-2">
                  <textarea
                    aria-label="رد الإدارة على المحادثة"
                    value={draft}
                    onChange={(event) => setDraft(event.target.value)}
                    disabled={!canSendText || sending}
                    rows={2}
                    maxLength={4000}
                    placeholder={
                      !canSend
                        ? 'المحادثة مغلقة'
                        : channelCapabilities.usesExternalThread && !externalWindowOpen
                          ? isWhatsApp
                            ? 'الإرسال النصي متوقف — استخدم قالب واتساب'
                            : 'الإرسال متوقف حتى تصل رسالة جديدة من العميل'
                          : isWhatsApp
                            ? 'اكتب ردًا على واتساب'
                            : isMessenger
                              ? 'اكتب ردًا على ماسنجر'
                            : 'اكتب رسالة باسم الإدارة'
                    }
                    className="min-h-12 min-w-0 flex-1 resize-none rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-accent)] focus:ring-2 focus:ring-[var(--admin-accent-soft)] disabled:bg-[var(--admin-card-soft)]"
                  />
                  <button
                    type="submit"
                    disabled={!canSendText || !draft.trim() || sending}
                    className="inline-flex min-h-12 min-w-12 items-center justify-center rounded-xl bg-[var(--admin-primary)] px-4 text-[var(--admin-primary-contrast)] transition hover:bg-[var(--admin-primary-strong)] disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <Send aria-hidden="true" size={18} />
                    <span className="sr-only">إرسال الرسالة</span>
                  </button>
                </div>
              </form>
            </div>
          </div>

          <aside className="max-h-52 min-h-0 overflow-y-auto border-t border-[var(--admin-border)] p-4 lg:max-h-none lg:border-t-0">
            <div className="mb-3 flex items-center justify-between gap-2">
              <h3 className="font-bold text-[var(--admin-text)]">
                السجل التشغيلي
              </h3>
              <label className="text-xs text-[var(--admin-muted)]">
                النوع
                <select
                  value={eventFilter}
                  onChange={(event) => setEventFilter(event.target.value)}
                  className="mr-2 h-9 rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2 text-[var(--admin-text)]"
                >
                  <option value="all">الكل</option>
                  <option value="AI">AI / Worker</option>
                  <option value="Assignment">الإسناد</option>
                  <option value="StudentAction">الإجراءات</option>
                  <option value="Message">الرسائل</option>
                </select>
              </label>
            </div>
            <ol className="space-y-3">
              {timeline.items
                .filter(
                  (item) =>
                    eventFilter === 'all' ||
                    (eventFilter === 'AI'
                      ? item.type.startsWith('AI')
                      : item.type === eventFilter)
                )
                .map((item, index) => (
                  <li
                    key={`${item.at}-${index}`}
                    className="rounded-xl bg-[var(--admin-card-soft)] p-3 text-sm"
                  >
                    <strong className="text-[var(--admin-text)]">
                      {item.summary}
                    </strong>
                    <time
                      dateTime={item.at}
                      className="mt-1 block text-xs text-[var(--admin-muted)]"
                    >
                      {formatCairoTimestamp(item.at)}
                    </time>
                    <p className="mt-1 text-xs text-[var(--admin-muted)]">
                      الفاعل: {item.actorName || 'النظام الآلي'}
                    </p>
                    {item.safeDetails && (
                      <pre
                        className="mt-2 whitespace-pre-wrap break-all rounded-lg bg-[var(--admin-card)] p-2 text-xs text-[var(--admin-text)]"
                        dir="auto"
                      >
                        {item.safeDetails}
                      </pre>
                    )}
                  </li>
                ))}
            </ol>
          </aside>
        </div>
      </div>
    </AccessibleOverlay>
  );
}

function senderLabel(senderType: LiveSupportMessage['senderType']) {
  return (
    {
      Student: 'الطالب',
      Guest: 'الزائر',
      Staff: 'موظف الدعم',
      Admin: 'الإدارة',
      System: 'النظام',
      AI: 'المساعد الذكي',
    } as const
  )[senderType];
}

function isStaffAvailable(staff: LiveSupportStaffConfig) {
  return (
    staff.isEnabled &&
    staff.isCheckedIn &&
    staff.activeLoad < staff.maxActiveConversations
  );
}

function isAbortError(cause: unknown) {
  return (
    (typeof DOMException !== 'undefined' &&
      cause instanceof DOMException &&
      cause.name === 'AbortError') ||
    (typeof cause === 'object' &&
      cause !== null &&
      'code' in cause &&
      (cause as { code?: string }).code === 'ERR_CANCELED')
  );
}
