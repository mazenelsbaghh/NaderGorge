import { Bot, CircleAlert, Clock3, Headphones } from 'lucide-react';
import type { LiveSupportAITurnState } from '@/services/live-support-service';

interface AIConversationStatusProps {
  turnState?: LiveSupportAITurnState | null;
  onRequestHuman: () => void;
}

export function AIConversationStatus({ turnState, onRequestHuman }: AIConversationStatusProps) {
  const failed = turnState === 'Failed';
  const working = turnState === 'Queued' || turnState === 'Processing' || turnState === 'ProviderCompleted';
  return (
    <section aria-live="polite" className="mb-3 rounded-xl border border-cyan-100 bg-cyan-50 px-3 py-2.5 text-xs text-slate-700">
      <div className="flex items-start gap-2">
        <span className="mt-0.5 text-cyan-700" aria-hidden="true">{failed ? <CircleAlert size={16}/> : working ? <Clock3 size={16}/> : <Bot size={16}/>}</span>
        <div className="min-w-0 flex-1">
          <p className="font-bold text-slate-900">أنت تتحدث الآن مع مساعد ذكي</p>
          <p className="mt-0.5 leading-5">{failed ? 'تعذر إكمال الرد، وجارٍ تحويلك للدعم البشري.' : working ? 'بنراجع رسالتك ونجهّز الرد…' : 'تقدر تطلب موظف دعم في أي وقت.'}</p>
        </div>
        <button type="button" onClick={onRequestHuman} className="inline-flex min-h-11 shrink-0 items-center gap-1 rounded-lg px-2 font-semibold text-cyan-800 hover:bg-cyan-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-cyan-700">
          <Headphones size={15} aria-hidden="true"/> موظف
        </button>
      </div>
    </section>
  );
}
