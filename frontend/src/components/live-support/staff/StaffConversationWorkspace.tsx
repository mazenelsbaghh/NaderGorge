import { Headphones, Send, XCircle } from 'lucide-react';
import type { LiveSupportPreferences } from '@/hooks/useLiveSupportPreferences';
import type { LiveSupportCannedReply, LiveSupportConversation, LiveSupportMessage } from '@/services/live-support-service';

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
  onTransfer: () => void;
  onClose: () => void;
  cannedReplies: LiveSupportCannedReply[];
  onCannedReply: (reply: LiveSupportCannedReply) => void;
  preferences: LiveSupportPreferences;
}

export function StaffConversationWorkspace({ conversation, messages, draft, ownershipLost, pendingAction, messagesLoading, messagesError, onRetryMessages, onDraftChange, onSend, onTransfer, onClose, cannedReplies, onCannedReply, preferences }: StaffConversationWorkspaceProps) {
  if (!conversation) return <main className="grid min-h-[420px] flex-1 place-items-center p-8 text-center text-slate-500"><div><Headphones className="mx-auto mb-3" size={36}/><p>لا توجد محادثة مسندة إليك حاليًا.</p></div></main>;
  return <main className="flex min-h-[420px] min-w-0 flex-col">
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 p-4">
      <div><h2 className="font-bold text-slate-900">{conversation.subject || 'محادثة دعم'}</h2><p className="text-xs text-slate-500">{conversation.participantType === 'Guest' ? 'زائر — يحتاج ربطًا يدويًا فقط' : 'طالب مسجل'}</p></div>
      <div className="flex gap-2"><button type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={onTransfer} className="min-h-11 rounded-xl border border-amber-200 px-3 text-sm font-semibold text-amber-800 hover:bg-amber-50 disabled:opacity-50">{pendingAction === 'transfer' ? 'جارٍ التحويل…' : 'تحويل'}</button><button type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={onClose} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-red-200 px-3 text-sm font-semibold text-red-700 hover:bg-red-50 disabled:opacity-50"><XCircle size={17}/>{pendingAction === 'close' ? 'جارٍ الإغلاق…' : 'إغلاق'}</button></div>
    </header>
    {ownershipLost && <p role="alert" className="border-b border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-900">تم نقل ملكية المحادثة. تم إيقاف الرد والإجراءات فورًا.</p>}
    <div role="log" aria-live="polite" className="min-h-0 flex-1 space-y-2 overflow-y-auto p-4">{messagesLoading ? <p role="status" className="text-sm text-slate-500">جارٍ تحميل الرسائل…</p> : messagesError ? <div role="alert" className="space-y-2 text-sm text-red-700"><p>{messagesError}</p><button type="button" onClick={onRetryMessages} className="font-semibold underline">إعادة المحاولة</button></div> : messages.map(message => { const isStaffMessage = ['Staff', 'Admin'].includes(message.senderType); const backgroundColor = isStaffMessage ? preferences.staffBubbleColor : preferences.studentBubbleColor; return <article dir="auto" key={message.id} style={{ backgroundColor, color: contrastColor(backgroundColor), fontSize: fontSize(preferences.fontScale) }} className={`max-w-[85%] [overflow-wrap:anywhere] rounded-2xl px-3 py-2 ${isStaffMessage ? 'mr-auto' : 'ml-auto'}`}>{message.content}</article>; })}</div>
    <div className="border-t border-slate-100 p-4">{cannedReplies.length > 0 && <div className="mb-3 flex flex-wrap gap-2" aria-label="ردود ثابتة">{cannedReplies.map(reply => <button key={reply.id} type="button" disabled={ownershipLost || Boolean(pendingAction)} onClick={() => onCannedReply(reply)} className={`min-h-9 rounded-lg border px-3 text-xs font-semibold disabled:opacity-50 ${reply.sendImmediately ? 'border-amber-300 bg-amber-50 text-amber-900' : 'border-cyan-200 bg-cyan-50 text-cyan-900'}`}>{reply.sendImmediately ? 'إرسال: ' : ''}{reply.title}</button>)}</div>}<div className="flex gap-2"><input aria-label="رد موظف الدعم" disabled={ownershipLost || Boolean(pendingAction)} value={draft} onChange={event => onDraftChange(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); onSend(); } }} className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 px-3 outline-none focus-visible:border-cyan-700 focus-visible:ring-2 focus-visible:ring-cyan-700/20 disabled:bg-slate-100" placeholder={ownershipLost ? 'المحادثة لم تعد مملوكة لك' : 'اكتب الرد'}/><button type="button" disabled={ownershipLost || Boolean(pendingAction) || !draft.trim()} onClick={onSend} aria-label="إرسال" className="grid size-11 place-items-center rounded-xl bg-cyan-700 text-white disabled:opacity-50"><Send size={18}/></button></div></div>
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
