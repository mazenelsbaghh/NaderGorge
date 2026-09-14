import { LoaderCircle, RotateCcw, Square } from 'lucide-react';
import type { AdminAiTurn } from '@/services/admin-ai-agent-contract';

const labels: Record<AdminAiTurn['status'], string> = {
  Queued: 'الوكيل يستعد للرد عليك…',
  Planning: 'الوكيل يجهّز ردك الآن…',
  Retrieving: 'الوكيل يجمع البيانات المطلوبة…',
  Answering: 'الوكيل يكتب الرد الآن…',
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
  submitting = false,
  onStop,
  onRetry,
}: {
  turn?: AdminAiTurn;
  submitting?: boolean;
  onStop?: () => void;
  onRetry?: () => void;
}) {
  if (!turn && !submitting) return null;
  if (turn && ['Completed', 'Cancelled'].includes(turn.status)) return null;
  const label = submitting
    ? 'تم إرسال سؤالك، الوكيل يرد عليك الآن…'
    : turn?.safeProgressLabelAr || (turn ? labels[turn.status] : '');
  const active =
    submitting ||
    (turn ? !['Failed', 'AccessRevoked'].includes(turn.status) : false);
  return (
    <div
      role="status"
      aria-live="polite"
      className="flex items-center gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-3 text-sm"
    >
      <LoaderCircle
        className={`h-4 w-4 text-[var(--admin-primary)] ${active ? 'motion-safe:animate-spin' : ''}`}
      />
      <span className="font-bold text-[var(--admin-text)]">{label}</span>
      <div className="mr-auto">
        {turn?.canCancel && onStop && (
          <button onClick={onStop} className="min-h-11 px-3 font-bold">
            <Square className="inline h-4 w-4" /> إيقاف
          </button>
        )}
        {(turn?.canRetry || turn?.status === 'Failed') && onRetry && (
          <button onClick={onRetry} className="min-h-11 px-3 font-bold">
            <RotateCcw className="inline h-4 w-4" /> إعادة المحاولة
          </button>
        )}
      </div>
    </div>
  );
}
