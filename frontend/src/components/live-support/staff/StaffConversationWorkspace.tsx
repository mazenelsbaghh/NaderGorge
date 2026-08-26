import Image from 'next/image';
import { Fragment, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { ChevronDown, Headphones, LoaderCircle, MessageSquareReply, MessageSquareText, Paperclip, Send, X, XCircle } from 'lucide-react';
import type { LiveSupportPreferences } from '@/hooks/useLiveSupportPreferences';
import type { LiveSupportCannedReply, LiveSupportConversation, LiveSupportMessage, LiveSupportWhatsAppTemplate } from '@/services/live-support-service';
import { LiveSupportMessageContent, LiveSupportMessageMeta } from '@/components/live-support/LiveSupportMessageContent';
import { LiveSupportMessageActions } from '@/components/live-support/LiveSupportMessageActions';
import { StaffVoiceRecorder } from '@/components/live-support/staff/StaffVoiceRecorder';
import { EmojiPicker, insertEmojiAtCursor } from '@/components/live-support/shared/EmojiPicker';
import { accessibleColorPair } from '@/lib/accessible-color';
import { WhatsAppTemplatePicker } from '@/components/live-support/staff/WhatsAppTemplatePicker';

interface StaffConversationWorkspaceProps {
  conversation?: LiveSupportConversation;
  messages: LiveSupportMessage[];
  draft: string;
  participantDraft?: string | null;
  ownershipLost: boolean;
  pendingAction?: 'send' | 'close' | 'transfer' | null;
  messagesLoading?: boolean;
  messagesError?: string;
  hasOlderMessages?: boolean;
  hasPendingMessageGap?: boolean;
  olderMessagesLoading?: boolean;
  olderMessagesError?: string;
  onRetryMessages?: () => void;
  onLoadOlderMessages: () => Promise<void>;
  onDraftChange: (value: string) => void;
  onSend: (replyToMessageId?: string) => void;
  onSendWhatsAppTemplate: (template: LiveSupportWhatsAppTemplate, parameters: string[], previewText: string) => Promise<void>;
  uploading?: boolean;
  onUpload: (file?: File) => Promise<boolean>;
  onEditMessage: (messageId: string, content: string) => Promise<void>;
  onDeleteMessage: (messageId: string) => Promise<void>;
  onTransfer: () => void;
  onClose: () => void;
  cannedReplies: LiveSupportCannedReply[];
  onCannedReply: (reply: LiveSupportCannedReply) => void;
  preferences: LiveSupportPreferences;
  replyFocusRequest: number;
}

export function StaffConversationWorkspace({ conversation, messages, draft, participantDraft, ownershipLost, pendingAction, messagesLoading, messagesError, hasOlderMessages, hasPendingMessageGap, olderMessagesLoading, olderMessagesError, onRetryMessages, onLoadOlderMessages, onDraftChange, onSend, onSendWhatsAppTemplate, uploading, onUpload, onEditMessage, onDeleteMessage, onTransfer, onClose, cannedReplies, onCannedReply, preferences, replyFocusRequest }: StaffConversationWorkspaceProps) {
  const [repliesOpen, setRepliesOpen] = useState(false);
  const [pendingImage, setPendingImage] = useState<{ conversationId: string; file: File }>();
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string>();
  const [replyTarget, setReplyTarget] = useState<LiveSupportMessage>();
  const [currentTime, setCurrentTime] = useState(() => Date.now());
  const replyInputRef = useRef<HTMLInputElement>(null);
  const messagesViewportRef = useRef<HTMLDivElement>(null);
  const shouldStickToBottom = useRef(true);
  const shouldRestoreReplyFocus = useRef(false);
  const prependScrollAnchor = useRef<{ messageId: string; viewportOffset: number } | null>(null);
  const pendingImageForConversation = pendingImage && pendingImage.conversationId === conversation?.id ? pendingImage.file : undefined;
  const activeReplyTarget = conversation?.channel !== 'WhatsApp' && replyTarget?.conversationId === conversation?.id ? replyTarget : undefined;

  useEffect(() => {
    if (!pendingImageForConversation) {
      setImagePreviewUrl(undefined);
      return;
    }
    const url = URL.createObjectURL(pendingImageForConversation);
    setImagePreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [pendingImageForConversation]);

  useEffect(() => {
    if (!shouldRestoreReplyFocus.current || pendingAction || ownershipLost || uploading) return;
    const frame = requestAnimationFrame(() => {
      replyInputRef.current?.focus();
      shouldRestoreReplyFocus.current = false;
    });
    return () => cancelAnimationFrame(frame);
  }, [ownershipLost, pendingAction, uploading]);

  useEffect(() => {
    if (!replyFocusRequest || pendingAction || ownershipLost || uploading) return;
    const frame = requestAnimationFrame(() => replyInputRef.current?.focus());
    return () => cancelAnimationFrame(frame);
  }, [ownershipLost, pendingAction, replyFocusRequest, uploading]);

  useEffect(() => {
    shouldStickToBottom.current = true;
    prependScrollAnchor.current = null;
    const frame = requestAnimationFrame(() => {
      const viewport = messagesViewportRef.current;
      if (viewport) viewport.scrollTop = viewport.scrollHeight;
    });
    return () => cancelAnimationFrame(frame);
  }, [conversation?.id]);

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
    const anchorMessage = viewport.querySelector<HTMLElement>(`[data-live-support-message-id="${anchor.messageId}"]`);
    if (anchorMessage) {
      const nextOffset = anchorMessage.getBoundingClientRect().top - viewport.getBoundingClientRect().top;
      viewport.scrollTop += nextOffset - anchor.viewportOffset;
    }
    prependScrollAnchor.current = null;
  }, [messages.length, olderMessagesLoading]);

  useEffect(() => {
    setCurrentTime(Date.now());
    const interval = window.setInterval(() => setCurrentTime(Date.now()), 60_000);
    return () => window.clearInterval(interval);
  }, []);

  const sendAndRestoreFocus = () => {
    if (!draft.trim()) return;
    shouldRestoreReplyFocus.current = true;
    onSend(activeReplyTarget?.id);
    setReplyTarget(undefined);
  };

  const insertEmoji = (emoji: string) => {
    const input = replyInputRef.current;
    const draftInsertion = insertEmojiAtCursor(input, draft, emoji);
    onDraftChange(draftInsertion.draftText);
    requestAnimationFrame(() => {
      input?.focus();
      input?.setSelectionRange(draftInsertion.cursorPosition, draftInsertion.cursorPosition);
    });
  };

  const confirmImageUpload = () => {
    if (!pendingImageForConversation || ownershipLost || pendingAction || uploading) return;
    const file = pendingImageForConversation;
    void onUpload(file).then((sent) => {
      if (sent) setPendingImage(undefined);
    });
  };

  const loadOlderMessages = () => {
    const viewport = messagesViewportRef.current;
    if (!viewport || olderMessagesLoading || !hasOlderMessages) return;
    shouldStickToBottom.current = false;
    const viewportTop = viewport.getBoundingClientRect().top;
    const anchorMessage = [...viewport.querySelectorAll<HTMLElement>('[data-live-support-message-id]')]
      .find((element) => element.getBoundingClientRect().bottom > viewportTop);
    prependScrollAnchor.current = anchorMessage ? {
      messageId: anchorMessage.dataset.liveSupportMessageId ?? '',
      viewportOffset: anchorMessage.getBoundingClientRect().top - viewportTop,
    } : null;
    void onLoadOlderMessages();
  };

  if (!conversation) return <main className="grid min-h-[420px] flex-1 place-items-center p-8 text-center text-[var(--admin-muted)]"><div><Headphones className="mx-auto mb-3" size={36}/><p>لا توجد محادثة مسندة إليك حاليًا.</p></div></main>;
  const isWhatsApp = conversation.channel === 'WhatsApp';
  const participantName = conversation.participantName?.trim() || (conversation.participantType === 'Guest' ? 'زائر' : 'طالب مسجل');
  const participantDetail = isWhatsApp
    ? 'محادثة واتساب'
    : conversation.participantType === 'Guest'
    ? 'زائر، يحتاج ربطًا يدويًا فقط'
    : conversation.subject
      ? `طالب مسجل · ${conversation.subject}`
      : 'طالب مسجل';
  const whatsAppWindowExpiration = conversation.customerServiceWindowExpiresAt
    ? new Date(conversation.customerServiceWindowExpiresAt).getTime()
    : Number.NaN;
  const whatsAppWindowClosed = isWhatsApp && (!Number.isFinite(whatsAppWindowExpiration) || whatsAppWindowExpiration <= currentTime);
  const replyDisabled = ownershipLost || Boolean(pendingAction) || uploading || whatsAppWindowClosed;
  return <main className="flex h-full min-h-0 min-w-0 flex-col">
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--admin-border)] p-4">
      <div><div className="flex items-center gap-2"><h2 className="font-bold text-[var(--admin-text)]">{participantName}</h2>{isWhatsApp ? <span className="rounded-full bg-[var(--admin-success-10)] px-2 py-0.5 text-xs font-bold text-[var(--admin-success)]">واتساب</span> : null}</div><p className="truncate text-xs text-[var(--admin-muted)]" title={conversation.subject}>{participantDetail}{isWhatsApp && conversation.externalPhoneNumber ? <> · <bdi dir="ltr">{conversation.externalPhoneNumber}</bdi></> : null}</p></div>
      <div className="flex gap-2"><button type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={onTransfer} className="min-h-11 rounded-xl border border-[var(--admin-border)] px-3 text-sm font-semibold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-50">{pendingAction === 'transfer' ? 'جارٍ التحويل…' : 'تحويل المحادثة'}</button><button type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={onClose} className="inline-flex min-h-11 items-center gap-2 rounded-xl px-3 text-sm font-semibold text-[var(--admin-danger)] hover:bg-[var(--admin-danger-10)] disabled:opacity-50"><XCircle size={17}/>{pendingAction === 'close' ? 'جارٍ الإنهاء…' : 'إنهاء المحادثة'}</button></div>
    </header>
    {ownershipLost && <p role="alert" className="border-b border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] px-4 py-3 text-sm font-medium text-[var(--admin-warning)]">تم نقل ملكية المحادثة. تم إيقاف الرد والإجراءات فورًا.</p>}
    <div ref={messagesViewportRef} aria-label="سجل رسائل المحادثة" onScroll={(event) => { const viewport = event.currentTarget; shouldStickToBottom.current = viewport.scrollHeight - viewport.scrollTop - viewport.clientHeight < 80; }} className="min-h-0 flex-1 touch-pan-y overflow-y-auto overscroll-contain [-webkit-overflow-scrolling:touch] [scrollbar-color:var(--admin-border)_transparent] [scrollbar-gutter:stable] [scrollbar-width:thin]">
    <div role="log" aria-live="polite" className="min-h-full space-y-2 p-4">
      {messagesLoading ? <p role="status" className="text-sm text-[var(--admin-muted)]">جارٍ تحميل الرسائل…</p> : messagesError ? <div role="alert" className="space-y-2 text-sm text-[var(--admin-danger)]"><p>{messagesError}</p><button type="button" onClick={onRetryMessages} className="font-semibold underline">إعادة المحاولة</button></div> : <>
        {isWhatsApp && (hasOlderMessages || olderMessagesLoading || olderMessagesError) ? <div className={`flex flex-col items-center gap-2 pb-2 ${hasPendingMessageGap ? 'sticky top-2 z-10 mx-auto rounded-2xl bg-[var(--admin-warning-10)] px-2 pt-2 shadow-sm' : ''}`}>
          {hasOlderMessages ? <button type="button" disabled={olderMessagesLoading} onClick={loadOlderMessages} className="min-h-10 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 text-xs font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] disabled:opacity-60">{olderMessagesLoading ? 'جارٍ تحميل الرسائل الأقدم…' : hasPendingMessageGap ? 'استكمال الرسائل الناقصة' : 'تحميل الرسائل الأقدم'}</button> : null}
          {olderMessagesError ? <div role="alert" className="text-center text-xs text-[var(--admin-danger)]"><p>{olderMessagesError}</p><button type="button" onClick={loadOlderMessages} className="mt-1 font-bold underline">إعادة المحاولة</button></div> : null}
        </div> : null}
        {messages.map((message, index) => {
          const isStaffMessage = ['Staff', 'Admin'].includes(message.senderType);
          const requestedBackground = isStaffMessage ? preferences.staffBubbleColor : preferences.studentBubbleColor;
          const colors = accessibleColorPair(requestedBackground);
          const senderLabel = isStaffMessage ? 'أنت' : participantName;
          const previousConversationId = messages[index - 1]?.conversationId;
          const showEpisodeBoundary = isWhatsApp && message.conversationId !== previousConversationId
            && (index > 0 || message.conversationId !== conversation.id);
          return <Fragment key={message.id}>
            {showEpisodeBoundary ? <div role="separator" aria-label={message.conversationId === conversation.id ? 'بداية المحادثة الحالية' : 'بداية محادثة سابقة'} className="flex items-center gap-2 py-2 text-xs font-bold text-[var(--admin-muted)]"><span className="h-px flex-1 bg-[var(--admin-border)]"/><span>{message.conversationId === conversation.id ? 'المحادثة الحالية' : 'محادثة سابقة'}</span><span className="h-px flex-1 bg-[var(--admin-border)]"/></div> : null}
            <article dir="auto" data-live-support-message-id={message.id} aria-label={`رسالة من ${senderLabel}`} style={{ backgroundColor: colors.backgroundColor, color: colors.color, fontSize: fontSize(preferences.fontScale) }} className={`max-w-[76%] break-words [overflow-wrap:anywhere] rounded-2xl px-3 py-2 ${isStaffMessage ? 'mr-auto' : 'ml-auto'}`}><p className="mb-1 text-xs font-bold opacity-75">{senderLabel}</p>{message.replyTo ? <ReplyPreview message={message.replyTo}/>: null}<LiveSupportMessageContent message={message} audience="staff" staffWhatsAppThreadConversationId={isWhatsApp ? conversation.id : undefined}/>{!isWhatsApp ? <button type="button" onClick={() => { setReplyTarget(message); replyInputRef.current?.focus(); }} className="mt-1 inline-flex min-h-8 items-center gap-1 rounded-md px-1.5 text-xs font-bold opacity-75 hover:bg-black/10"><MessageSquareReply size={14}/>رد</button> : null}{isStaffMessage && !isWhatsApp ? <LiveSupportMessageActions message={message} onEdit={onEditMessage} onDelete={onDeleteMessage}/> : null}<LiveSupportMessageMeta message={message} audience="staff"/></article>
          </Fragment>;
        })}
        {!isWhatsApp && participantDraft !== null && participantDraft !== undefined ? <article className="ml-auto max-w-[76%] rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-primary-15)] px-3 py-2 text-sm text-[var(--admin-text)]"><p className="mb-1 text-xs font-bold text-[var(--admin-primary)]">{participantName} يكتب الآن…</p><p dir="auto" className="whitespace-pre-wrap break-words [overflow-wrap:anywhere]">{participantDraft || '…'}</p></article> : null}
      </>}
    </div>
    </div>
    <div className="shrink-0 border-t border-[var(--admin-border)] p-4">
      {isWhatsApp ? <WhatsAppTemplatePicker disabled={ownershipLost || Boolean(pendingAction) || Boolean(uploading)} onSend={onSendWhatsAppTemplate}/> : null}
      {whatsAppWindowClosed ? <p role="status" className="mb-3 rounded-lg bg-[var(--admin-warning-10)] px-3 py-2 text-sm font-medium text-[var(--admin-warning)]">انتهت نافذة الرد خلال 24 ساعة. أرسل قالب واتساب معتمدًا لبدء المحادثة من جديد.</p> : null}
      {!whatsAppWindowClosed ? <>
      {activeReplyTarget ? <div className="mb-2 flex items-center gap-2 rounded-lg bg-[var(--admin-card-soft)] px-3 py-2 text-xs text-[var(--admin-text)]"><MessageSquareReply size={15} className="text-[var(--admin-primary)]"/><p className="min-w-0 flex-1 truncate">رد على: {activeReplyTarget.content || 'مرفق'}</p><button type="button" onClick={() => setReplyTarget(undefined)} aria-label="إلغاء الرد"><X size={16}/></button></div> : null}
      {pendingImageForConversation && imagePreviewUrl && <div className="mb-3 flex flex-wrap items-center gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3" role="status" aria-label="معاينة الصورة قبل الإرسال">
        <Image src={imagePreviewUrl} alt="معاينة الصورة قبل الإرسال" width={160} height={112} unoptimized className="h-28 w-40 rounded-lg bg-[var(--admin-card)] object-contain" />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-bold text-[var(--admin-text)]">{pendingImageForConversation.name}</p>
          <p className="mt-1 text-xs text-[var(--admin-muted)]">راجع الصورة قبل إرسالها للطالب.</p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button type="button" disabled={replyDisabled} onClick={confirmImageUpload} className="min-h-10 rounded-lg bg-[var(--admin-primary)] px-3 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50">{uploading ? 'جارٍ الإرسال…' : 'إرسال الصورة'}</button>
            <button type="button" disabled={uploading} onClick={() => setPendingImage(undefined)} className="min-h-10 rounded-lg border border-[var(--admin-border)] px-3 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-50">إلغاء</button>
          </div>
        </div>
      </div>}
      {repliesOpen && <div id="staff-canned-replies" className="mb-2 max-h-48 overflow-y-auto rounded-xl bg-[var(--admin-card-soft)] p-2" aria-label="الردود الجاهزة">{cannedReplies.length === 0 ? <p className="px-3 py-4 text-center text-sm font-medium text-[var(--admin-muted)]">لا توجد ردود جاهزة لهذا الحساب بعد.</p> : cannedReplies.map(reply => <button key={reply.id} type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={() => { if (reply.sendImmediately) shouldRestoreReplyFocus.current = true; onCannedReply(reply); setRepliesOpen(false); }} className="block w-full rounded-lg px-3 py-2.5 text-right text-sm font-semibold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-50"><span className="block truncate">{reply.title}</span><span className="mt-0.5 block truncate text-xs font-normal text-[var(--admin-muted)]">{reply.sendImmediately ? 'إرسال مباشر' : 'إضافة إلى مربع الكتابة'}</span></button>)}</div>}
      <div className="grid grid-cols-[auto_minmax(0,1fr)_auto] gap-2 sm:grid-cols-[auto_auto_minmax(0,1fr)_auto]">
        <div className="col-span-3 sm:col-span-1">
          <button type="button" aria-expanded={repliesOpen} aria-controls="staff-canned-replies" disabled={ownershipLost || Boolean(pendingAction)} onClick={() => setRepliesOpen((open) => !open)} className="inline-flex h-11 w-full items-center justify-center gap-1.5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 text-xs font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] disabled:opacity-50 sm:w-auto"><MessageSquareText size={17}/>ردود جاهزة<ChevronDown size={14} className={repliesOpen ? 'rotate-180 transition-transform' : 'transition-transform'}/></button>
        </div>
        <div className="flex shrink-0 gap-2">
          <label aria-label="اختيار صورة" className={`grid size-11 shrink-0 place-items-center rounded-xl border border-[var(--admin-border)] text-[var(--admin-muted)] focus-within:outline-2 ${replyDisabled || pendingImageForConversation ? 'pointer-events-none opacity-50' : 'cursor-pointer hover:bg-[var(--admin-hover)]'}`}>{uploading ? <LoaderCircle className="animate-spin" size={18}/> : <Paperclip size={18}/>}<input type="file" accept="image/jpeg,image/png,image/webp" disabled={replyDisabled || Boolean(pendingImageForConversation)} onChange={(event) => { const file = event.target.files?.[0]; if (file && conversation) setPendingImage({ conversationId: conversation.id, file }); event.currentTarget.value = ''; }} className="sr-only"/></label>
          <EmojiPicker tone="staff" disabled={ownershipLost || Boolean(pendingAction) || uploading} onSelect={insertEmoji}/>
        </div>
        <input ref={replyInputRef} aria-label="رد موظف الدعم" disabled={replyDisabled} value={draft} onChange={event => onDraftChange(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); sendAndRestoreFocus(); } }} className="h-11 min-w-0 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus-visible:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-15)] disabled:bg-[var(--admin-card-soft)]" placeholder={ownershipLost ? 'المحادثة لم تعد مملوكة لك' : isWhatsApp ? 'اكتب ردك على واتساب' : 'اكتب ردك للطالب'}/>
        <button type="button" disabled={replyDisabled || !draft.trim()} onClick={sendAndRestoreFocus} aria-label="إرسال الرد" className="grid size-11 place-items-center rounded-xl bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] disabled:opacity-50"><Send size={18}/></button>
      </div>
      <StaffVoiceRecorder disabled={ownershipLost || Boolean(pendingAction) || whatsAppWindowClosed} uploading={uploading} onSend={onUpload} />
      </> : null}
    </div>
  </main>;
}

function fontSize(scale: LiveSupportPreferences['fontScale']) {
  return scale === 'small' ? '0.8125rem' : scale === 'large' ? '1rem' : '0.875rem';
}

function ReplyPreview({ message }: { message: NonNullable<LiveSupportMessage['replyTo']> }) {
  const preview = message.isDeleted ? 'تم حذف هذه الرسالة' : message.content || 'مرفق';
  return <div className="mb-2 rounded-lg bg-black/10 px-2 py-1.5 text-xs opacity-90"><p className="font-bold">رد على رسالة</p><p className="truncate">{preview}</p></div>;
}
