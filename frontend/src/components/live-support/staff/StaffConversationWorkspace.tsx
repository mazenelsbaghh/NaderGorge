import Image from 'next/image';
import { useEffect, useRef, useState } from 'react';
import { ChevronDown, Headphones, LoaderCircle, MessageSquareText, Paperclip, Send, XCircle } from 'lucide-react';
import type { LiveSupportPreferences } from '@/hooks/useLiveSupportPreferences';
import type { LiveSupportCannedReply, LiveSupportConversation, LiveSupportMessage } from '@/services/live-support-service';
import { LiveSupportMessageContent, LiveSupportMessageMeta } from '@/components/live-support/LiveSupportMessageContent';
import { LiveSupportMessageActions } from '@/components/live-support/LiveSupportMessageActions';
import { StaffVoiceRecorder } from '@/components/live-support/staff/StaffVoiceRecorder';
import { accessibleColorPair } from '@/lib/accessible-color';

interface StaffConversationWorkspaceProps {
  conversation?: LiveSupportConversation;
  messages: LiveSupportMessage[];
  draft: string;
  ownershipLost: boolean;
  pendingAction?: 'send' | 'close' | 'transfer' | null;
  messagesLoading?: boolean;
  messagesError?: string;
  onRetryMessages?: () => void;
  onDraftChange: (value: string) => void;
  onSend: () => void;
  uploading?: boolean;
  onUpload: (file?: File) => Promise<boolean>;
  onEditMessage: (messageId: string, content: string) => Promise<void>;
  onDeleteMessage: (messageId: string) => Promise<void>;
  onTransfer: () => void;
  onClose: () => void;
  cannedReplies: LiveSupportCannedReply[];
  onCannedReply: (reply: LiveSupportCannedReply) => void;
  preferences: LiveSupportPreferences;
}

export function StaffConversationWorkspace({ conversation, messages, draft, ownershipLost, pendingAction, messagesLoading, messagesError, onRetryMessages, onDraftChange, onSend, uploading, onUpload, onEditMessage, onDeleteMessage, onTransfer, onClose, cannedReplies, onCannedReply, preferences }: StaffConversationWorkspaceProps) {
  const [repliesOpen, setRepliesOpen] = useState(false);
  const [pendingImage, setPendingImage] = useState<{ conversationId: string; file: File }>();
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string>();
  const replyInputRef = useRef<HTMLInputElement>(null);
  const shouldRestoreReplyFocus = useRef(false);
  const pendingImageForConversation = pendingImage && pendingImage.conversationId === conversation?.id ? pendingImage.file : undefined;

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

  const sendAndRestoreFocus = () => {
    if (!draft.trim()) return;
    shouldRestoreReplyFocus.current = true;
    onSend();
  };

  const confirmImageUpload = () => {
    if (!pendingImageForConversation || ownershipLost || pendingAction || uploading) return;
    const file = pendingImageForConversation;
    void onUpload(file).then((sent) => {
      if (sent) setPendingImage(undefined);
    });
  };

  if (!conversation) return <main className="grid min-h-[420px] flex-1 place-items-center p-8 text-center text-[var(--admin-muted)]"><div><Headphones className="mx-auto mb-3" size={36}/><p>لا توجد محادثة مسندة إليك حاليًا.</p></div></main>;
  const participantName = conversation.participantName?.trim() || (conversation.participantType === 'Guest' ? 'زائر' : 'طالب مسجل');
  const participantDetail = conversation.participantType === 'Guest'
    ? 'زائر، يحتاج ربطًا يدويًا فقط'
    : conversation.subject
      ? `طالب مسجل · ${conversation.subject}`
      : 'طالب مسجل';
  return <main className="flex h-full min-h-0 min-w-0 flex-col">
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--admin-border)] p-4">
      <div><h2 className="font-bold text-[var(--admin-text)]">{participantName}</h2><p className="truncate text-xs text-[var(--admin-muted)]" title={conversation.subject}>{participantDetail}</p></div>
      <div className="flex gap-2"><button type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={onTransfer} className="min-h-11 rounded-xl border border-[var(--admin-warning-20)] px-3 text-sm font-semibold text-[var(--admin-warning)] hover:bg-[var(--admin-warning-10)] disabled:opacity-50">{pendingAction === 'transfer' ? 'جارٍ التحويل…' : 'تحويل'}</button><button type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={onClose} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-danger-20)] px-3 text-sm font-semibold text-[var(--admin-danger)] hover:bg-[var(--admin-danger-10)] disabled:opacity-50"><XCircle size={17}/>{pendingAction === 'close' ? 'جارٍ الإغلاق…' : 'إغلاق'}</button></div>
    </header>
    {ownershipLost && <p role="alert" className="border-b border-[var(--admin-warning-20)] bg-[var(--admin-warning-10)] px-4 py-3 text-sm font-medium text-[var(--admin-warning)]">تم نقل ملكية المحادثة. تم إيقاف الرد والإجراءات فورًا.</p>}
    <div role="log" aria-live="polite" className="min-h-0 flex-1 space-y-2 overflow-y-auto overscroll-contain p-4">{messagesLoading ? <p role="status" className="text-sm text-[var(--admin-muted)]">جارٍ تحميل الرسائل…</p> : messagesError ? <div role="alert" className="space-y-2 text-sm text-[var(--admin-danger)]"><p>{messagesError}</p><button type="button" onClick={onRetryMessages} className="font-semibold underline">إعادة المحاولة</button></div> : messages.map(message => { const isStaffMessage = ['Staff', 'Admin'].includes(message.senderType); const requestedBackground = isStaffMessage ? preferences.staffBubbleColor : preferences.studentBubbleColor; const colors = accessibleColorPair(requestedBackground); return <article dir="auto" key={message.id} style={{ backgroundColor: colors.backgroundColor, color: colors.color, fontSize: fontSize(preferences.fontScale) }} className={`max-w-[72%] break-words [overflow-wrap:anywhere] rounded-2xl px-3 py-2 ${isStaffMessage ? 'mr-auto' : 'ml-auto'}`}><LiveSupportMessageContent message={message} audience="staff"/>{isStaffMessage ? <LiveSupportMessageActions message={message} onEdit={onEditMessage} onDelete={onDeleteMessage}/> : null}<LiveSupportMessageMeta message={message} audience="staff"/></article>; })}</div>
    <div className="shrink-0 border-t border-[var(--admin-border)] p-4">
      {pendingImageForConversation && imagePreviewUrl && <div className="mb-3 flex flex-wrap items-center gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3" role="status" aria-label="معاينة الصورة قبل الإرسال">
        <Image src={imagePreviewUrl} alt="معاينة الصورة قبل الإرسال" width={160} height={112} unoptimized className="h-28 w-40 rounded-lg bg-[var(--admin-card)] object-contain" />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-bold text-[var(--admin-text)]">{pendingImageForConversation.name}</p>
          <p className="mt-1 text-xs text-[var(--admin-muted)]">راجع الصورة قبل إرسالها للطالب.</p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button type="button" disabled={Boolean(pendingAction) || uploading || ownershipLost} onClick={confirmImageUpload} className="min-h-10 rounded-lg bg-[var(--admin-primary)] px-3 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50">{uploading ? 'جارٍ الإرسال…' : 'إرسال الصورة'}</button>
            <button type="button" disabled={uploading} onClick={() => setPendingImage(undefined)} className="min-h-10 rounded-lg border border-[var(--admin-border)] px-3 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-50">إلغاء</button>
          </div>
        </div>
      </div>}
      <div className="grid grid-cols-[auto_minmax(0,1fr)_auto] gap-2 sm:grid-cols-[auto_auto_minmax(0,1fr)_auto]"><div className="relative col-span-3 sm:col-span-1"><button type="button" aria-expanded={repliesOpen} aria-controls="staff-canned-replies" disabled={ownershipLost || Boolean(pendingAction)} onClick={() => setRepliesOpen((open) => !open)} className="inline-flex h-11 w-full items-center justify-center gap-1.5 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 text-xs font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] disabled:opacity-50 sm:w-auto"><MessageSquareText size={17}/>ردود جاهزة<ChevronDown size={14} className={repliesOpen ? 'rotate-180 transition-transform' : 'transition-transform'}/></button>{repliesOpen && <div id="staff-canned-replies" role="menu" className="absolute bottom-[calc(100%+0.5rem)] right-0 z-20 max-h-60 w-full overflow-y-auto rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-2 shadow-lg sm:w-72">{cannedReplies.length === 0 ? <p className="px-3 py-4 text-center text-sm font-medium text-[var(--admin-muted)]">لا توجد ردود جاهزة لهذا الحساب بعد.</p> : cannedReplies.map(reply => <button key={reply.id} role="menuitem" type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={() => { if (reply.sendImmediately) shouldRestoreReplyFocus.current = true; onCannedReply(reply); setRepliesOpen(false); }} className="block w-full rounded-lg px-3 py-2.5 text-right text-sm font-semibold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-50"><span className="block truncate">{reply.title}</span><span className="mt-0.5 block truncate text-xs font-normal text-[var(--admin-muted)]">{reply.sendImmediately ? 'إرسال مباشر' : 'إضافة إلى مربع الكتابة'}</span></button>)}</div>}</div><label aria-label="اختيار صورة" className={`grid size-11 shrink-0 place-items-center rounded-xl border border-[var(--admin-border)] text-[var(--admin-muted)] focus-within:outline-2 ${ownershipLost || pendingAction || uploading || pendingImageForConversation ? 'pointer-events-none opacity-50' : 'cursor-pointer hover:bg-[var(--admin-hover)]'}`}>{uploading ? <LoaderCircle className="animate-spin" size={18}/> : <Paperclip size={18}/>}<input type="file" accept="image/jpeg,image/png,image/webp" disabled={ownershipLost || Boolean(pendingAction) || uploading || Boolean(pendingImageForConversation)} onChange={(event) => { const file = event.target.files?.[0]; if (file && conversation) setPendingImage({ conversationId: conversation.id, file }); event.currentTarget.value = ''; }} className="sr-only"/></label><input ref={replyInputRef} aria-label="رد موظف الدعم" disabled={ownershipLost || Boolean(pendingAction) || uploading} value={draft} onChange={event => onDraftChange(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); sendAndRestoreFocus(); } }} className="h-11 min-w-0 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus-visible:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-15)] disabled:bg-[var(--admin-card-soft)]" placeholder={ownershipLost ? 'المحادثة لم تعد مملوكة لك' : 'اكتب الرد'}/><button type="button" disabled={ownershipLost || Boolean(pendingAction) || uploading || !draft.trim()} onClick={sendAndRestoreFocus} aria-label="إرسال" className="grid size-11 place-items-center rounded-xl bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] disabled:opacity-50"><Send size={18}/></button></div>
      <StaffVoiceRecorder disabled={ownershipLost || Boolean(pendingAction)} uploading={uploading} onSend={onUpload} />
    </div>
  </main>;
}

function fontSize(scale: LiveSupportPreferences['fontScale']) {
  return scale === 'small' ? '0.8125rem' : scale === 'large' ? '1rem' : '0.875rem';
}
