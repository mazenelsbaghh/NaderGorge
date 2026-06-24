import { Headphones, Send, XCircle } from 'lucide-react';
import type { LiveSupportConversation, LiveSupportMessage } from '@/services/live-support-service';

interface StaffConversationWorkspaceProps {
  conversation?: LiveSupportConversation;
  messages: LiveSupportMessage[];
  draft: string;
  ownershipLost: boolean;
  onDraftChange: (value: string) => void;
  onSend: () => void;
  onTransfer: () => void;
  onClose: () => void;
}

export function StaffConversationWorkspace({ conversation, messages, draft, ownershipLost, onDraftChange, onSend, onTransfer, onClose }: StaffConversationWorkspaceProps) {
  if (!conversation) return <main className="grid min-h-[420px] flex-1 place-items-center p-8 text-center text-slate-500"><div><Headphones className="mx-auto mb-3" size={36}/><p>لا توجد محادثة مسندة إليك حاليًا.</p></div></main>;
  return <main className="flex min-h-[420px] min-w-0 flex-col">
    <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 p-4">
      <div><h2 className="font-bold text-slate-900">{conversation.subject || 'محادثة دعم'}</h2><p className="text-xs text-slate-500">{conversation.participantType === 'Guest' ? 'زائر — يحتاج ربطًا يدويًا فقط' : 'طالب مسجل'}</p></div>
      <div className="flex gap-2"><button type="button" disabled={ownershipLost} onClick={onTransfer} className="min-h-11 rounded-xl border border-amber-200 px-3 text-sm font-semibold text-amber-800 hover:bg-amber-50 disabled:opacity-50">تحويل</button><button type="button" disabled={ownershipLost} onClick={onClose} className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-red-200 px-3 text-sm font-semibold text-red-700 hover:bg-red-50 disabled:opacity-50"><XCircle size={17}/>إغلاق</button></div>
    </header>
    {ownershipLost && <p role="alert" className="border-b border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-900">تم نقل ملكية المحادثة. تم إيقاف الرد والإجراءات فورًا.</p>}
    <div role="log" aria-live="polite" className="min-h-0 flex-1 space-y-2 overflow-y-auto p-4">{messages.map(message => <article dir="auto" key={message.id} className={`max-w-[85%] [overflow-wrap:anywhere] rounded-2xl px-3 py-2 text-sm ${['Staff','Admin'].includes(message.senderType) ? 'mr-auto bg-cyan-700 text-white' : 'ml-auto bg-slate-100 text-slate-800'}`}>{message.content}</article>)}</div>
    <div className="flex gap-2 border-t border-slate-100 p-4"><input aria-label="رد موظف الدعم" disabled={ownershipLost} value={draft} onChange={event => onDraftChange(event.target.value)} onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); onSend(); } }} className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 px-3 outline-none focus-visible:border-cyan-700 focus-visible:ring-2 focus-visible:ring-cyan-700/20 disabled:bg-slate-100" placeholder={ownershipLost ? 'المحادثة لم تعد مملوكة لك' : 'اكتب الرد'}/><button type="button" disabled={ownershipLost || !draft.trim()} onClick={onSend} aria-label="إرسال" className="grid size-11 place-items-center rounded-xl bg-cyan-700 text-white disabled:opacity-50"><Send size={18}/></button></div>
  </main>;
}
