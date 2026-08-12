import { LoaderCircle, RotateCcw, Square } from 'lucide-react';
import type { AdminAiTurn } from '@/services/admin-ai-agent-contract';

const labels: Record<AdminAiTurn['status'], string> = {
  Queued: 'في قائمة الانتظار',
  Planning: 'يخطط للإجابة',
  Retrieving: 'يجمع البيانات',
  Answering: 'يصيغ الإجابة',
  WaitingClarification: 'ينتظر توضيحك',
  ProposalReady: 'تم تجهيز اقتراح للمراجعة',
  Completed: 'اكتملت الإجابة',
  CancelRequested: 'جارٍ إيقاف الطلب',
  Cancelled: 'تم إيقاف الطلب',
  Failed: 'تعذر إكمال الطلب',
  AccessRevoked: 'تم إلغاء صلاحية الوصول',
};
export function AdminAiTurnStatus({
  turn,
  onStop,
  onRetry,
}: {
  turn?: AdminAiTurn;
  onStop?: () => void;
  onRetry?: () => void;
}) {
  if (!turn || ['Completed', 'Cancelled'].includes(turn.status)) return null;
  const active = !['Failed', 'AccessRevoked'].includes(turn.status);
  return (
    <div
      role="status"
      aria-live="polite"
      className="flex items-center gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 text-sm"
    >
      <LoaderCircle
        className={`h-4 w-4 text-[var(--admin-primary)] ${active ? 'motion-safe:animate-spin' : ''}`}
      />
      <span>{turn.safeProgressLabelAr || labels[turn.status]}</span>
      <div className="mr-auto">
        {turn.canCancel && onStop && (
          <button onClick={onStop} className="min-h-11 px-3 font-bold">
            <Square className="inline h-4 w-4" /> إيقاف
          </button>
        )}
        {turn.canRetry && onRetry && (
          <button onClick={onRetry} className="min-h-11 px-3 font-bold">
            <RotateCcw className="inline h-4 w-4" /> إعادة المحاولة
          </button>
        )}
      </div>
    </div>
  );
}
