import { useState } from 'react';
import { ChevronDown, Headphones, LoaderCircle, MessageSquareText, Paperclip, Send, XCircle } from 'lucide-react';
import type { LiveSupportPreferences } from '@/hooks/useLiveSupportPreferences';
import type { LiveSupportCannedReply, LiveSupportConversation, LiveSupportMessage } from '@/services/live-support-service';
import { LiveSupportMessageContent } from '@/components/live-support/LiveSupportMessageContent';

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
  onUpload: (file?: File) => void;
  onTransfer: () => void;
  onClose: () => void;
  cannedReplies: LiveSupportCannedReply[];
  onCannedReply: (reply: LiveSupportCannedReply) => void;
  preferences: LiveSupportPreferences;
}

export function StaffConversationWorkspace({ conversation, messages, draft, ownershipLost, pendingAction, messagesLoading, messagesError, onRetryMessages, onDraftChange, onSend, uploading, onUpload, onTransfer, onClose, cannedReplies, onCannedReply, preferences }: StaffConversationWorkspaceProps) {
  const [repliesOpen, setRepliesOpen] = useState(false);
  if (!conversation) return <main className="grid min-h-[420px] flex-1 place-items-center p-8 text-center text-slate-500"><div><Headphones className="mx-auto mb-3" size={36}/><p>لا توجد محادثة مسندة إليك حاليًا.</p></div></main>;
  return <main className="flex h-full min-h-0 min-w-0 flex-col">
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 p-4">
      <div><h2 className="font-bold text-slate-900">{conversation.subject || 'محادثة دعم'}</h2><p className="text-xs text-slate-500">{conversation.participantType === 'Guest' ? 'زائر — يحتاج ربطًا يدويًا فقط' : 'طالب مسجل'}</p></div>
      <div className="flex gap-2"><button type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={onTransfer} className="min-h-11 rounded-xl border border-amber-200 px-3 text-sm font-semibold text-amber-800 hover:bg-amber-50 disabled:opacity-50">{pendingAction === 'transfer' ? 'جارٍ التحويل…' : 'تحويل'}</button><button type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={onClose} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-red-200 px-3 text-sm font-semibold text-red-700 hover:bg-red-50 disabled:opacity-50"><XCircle size={17}/>{pendingAction === 'close' ? 'جارٍ الإغلاق…' : 'إغلاق'}</button></div>
    </header>
    {ownershipLost && <p role="alert" className="border-b border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-900">تم نقل ملكية المحادثة. تم إيقاف الرد والإجراءات فورًا.</p>}
    <div role="log" aria-live="polite" className="min-h-0 flex-1 space-y-2 overflow-y-auto overscroll-contain p-4">{messagesLoading ? <p role="status" className="text-sm text-slate-500">جارٍ تحميل الرسائل…</p> : messagesError ? <div role="alert" className="space-y-2 text-sm text-red-700"><p>{messagesError}</p><button type="button" onClick={onRetryMessages} className="font-semibold underline">إعادة المحاولة</button></div> : messages.map(message => { const isStaffMessage = ['Staff', 'Admin'].includes(message.senderType); const backgroundColor = isStaffMessage ? preferences.staffBubbleColor : preferences.studentBubbleColor; return <article dir="auto" key={message.id} style={{ backgroundColor, color: contrastColor(backgroundColor), fontSize: fontSize(preferences.fontScale) }} className={`max-w-[72%] break-words [overflow-wrap:anywhere] rounded-2xl px-3 py-2 ${isStaffMessage ? 'mr-auto' : 'ml-auto'}`}><LiveSupportMessageContent message={message} audience="staff"/></article>; })}</div>
    <div className="shrink-0 border-t border-slate-100 p-4"><div className="flex gap-2"><div className="relative shrink-0"><button type="button" aria-expanded={repliesOpen} aria-controls="staff-canned-replies" disabled={ownershipLost || Boolean(pendingAction)} onClick={() => setRepliesOpen((open) => !open)} className="inline-flex h-11 items-center gap-1.5 rounded-xl border border-cyan-200 bg-cyan-50 px-3 text-xs font-bold text-cyan-900 hover:bg-cyan-100 disabled:opacity-50"><MessageSquareText size={17}/>ردود جاهزة<ChevronDown size={14} className={repliesOpen ? 'rotate-180 transition-transform' : 'transition-transform'}/></button>{repliesOpen && <div id="staff-canned-replies" role="menu" className="absolute bottom-[calc(100%+0.5rem)] right-0 z-20 max-h-60 w-72 overflow-y-auto rounded-xl border border-slate-200 bg-white p-2 shadow-lg">{cannedReplies.length === 0 ? <p className="px-3 py-4 text-center text-sm font-medium text-slate-500">لا توجد ردود جاهزة لهذا الحساب بعد.</p> : cannedReplies.map(reply => <button key={reply.id} role="menuitem" type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={() => { onCannedReply(reply); setRepliesOpen(false); }} className="block w-full rounded-lg px-3 py-2.5 text-right text-sm font-semibold text-slate-800 hover:bg-slate-50 disabled:opacity-50"><span className="block truncate">{reply.title}</span><span className="mt-0.5 block truncate text-xs font-normal text-slate-500">{reply.sendImmediately ? 'إرسال مباشر' : 'إضافة إلى مربع الكتابة'}</span></button>)}</div>}</div><label aria-label="إرسال صورة" className={`grid size-11 shrink-0 place-items-center rounded-xl border border-slate-200 text-slate-600 focus-within:outline-2 ${ownershipLost || pendingAction || uploading ? 'pointer-events-none opacity-50' : 'cursor-pointer hover:bg-slate-50'}`}>{uploading ? <LoaderCircle className="animate-spin" size={18}/> : <Paperclip size={18}/>}<input type="file" accept="image/jpeg,image/png,image/webp" disabled={ownershipLost || Boolean(pendingAction) || uploading} onChange={(event) => { onUpload(event.target.files?.[0]); event.currentTarget.value = ''; }} className="sr-only"/></label><input aria-label="رد موظف الدعم" disabled={ownershipLost || Boolean(pendingAction) || uploading} value={draft} onChange={event => onDraftChange(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); onSend(); } }} className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 px-3 outline-none focus-visible:border-cyan-700 focus-visible:ring-2 focus-visible:ring-cyan-700/20 disabled:bg-slate-100" placeholder={ownershipLost ? 'المحادثة لم تعد مملوكة لك' : 'اكتب الرد'}/><button type="button" disabled={ownershipLost || Boolean(pendingAction) || uploading || !draft.trim()} onClick={onSend} aria-label="إرسال" className="grid size-11 place-items-center rounded-xl bg-cyan-700 text-white disabled:opacity-50"><Send size={18}/></button></div></div>
  </main>;
}

function fontSize(scale: LiveSupportPreferences['fontScale']) {
  return scale === 'small' ? '0.8125rem' : scale === 'large' ? '1rem' : '0.875rem';
}

function contrastColor(hex: string) {
  const normalized = hex.replace('#', '');
  if (!/^[0-9a-f]{6}$/i.test(normalized)) return '#0f172a';
  const [red, green, blue] = [0, 2, 4].map((offset) => Number.parseInt(normalized.slice(offset, offset + 2), 16));
  return (red * 299 + green * 587 + blue * 114) / 1000 >= 155 ? '#0f172a' : '#ffffff';
}
