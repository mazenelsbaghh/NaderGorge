'use client';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import type { AdminAiConversationSnapshot } from '@/services/admin-ai-agent-contract';
import { AdminAiMessage } from './AdminAiMessage';
import { AdminAiActionProposalCard } from './AdminAiActionProposalCard';
import { AdminAiTurnStatus } from './AdminAiTurnStatus';
import { AdminAiEmptyState } from './AdminAiEmptyState';
export function AdminAiTranscript({
  snapshot,
  busyProposalId,
  onConfirm,
  onCancelProposal,
  onSecureInput,
  onStop,
  onRetry,
  onLoadOlder,
  loadingOlder,
}: {
  snapshot?: AdminAiConversationSnapshot;
  busyProposalId?: string;
  onConfirm: (id: string, phrase?: string) => void;
  onCancelProposal: (id: string) => void;
  onSecureInput: (id: string) => void;
  onStop: () => void;
  onRetry: () => void;
  onLoadOlder: () => void;
  loadingOlder: boolean;
}) {
  const scroller = useRef<HTMLDivElement>(null);
  const historyAnchor = useRef<{ height: number; top: number } | undefined>(
    undefined
  );
  const [unseen, setUnseen] = useState(false);
  const count = snapshot?.messages.length ?? 0;
  useLayoutEffect(() => {
    const element = scroller.current;
    const anchor = historyAnchor.current;
    if (!element || !anchor) return;
    element.scrollTop = anchor.top + element.scrollHeight - anchor.height;
    historyAnchor.current = undefined;
  }, [count]);
  useEffect(() => {
    const el = scroller.current;
    if (!el) return;
    const near = el.scrollHeight - el.scrollTop - el.clientHeight < 100;
    if (near) {
      el.scrollTop = el.scrollHeight;
      setUnseen(false);
    } else setUnseen(true);
  }, [count]);
  if (!snapshot || (count === 0 && !snapshot.proposals?.length))
    return <AdminAiEmptyState />;
  return (
    <div className="relative min-h-0 flex-1">
      <div
        ref={scroller}
        role="log"
        aria-live="polite"
        aria-relevant="additions"
        className="absolute inset-0 space-y-4 overflow-y-auto p-4 sm:p-6"
      >
        {snapshot.nextBeforeSequence && (
          <button
            type="button"
            disabled={loadingOlder}
            onClick={() => {
              const element = scroller.current;
              if (element) {
                historyAnchor.current = {
                  height: element.scrollHeight,
                  top: element.scrollTop,
                };
              }
              onLoadOlder();
            }}
            className="mx-auto block min-h-11 rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold disabled:opacity-50"
          >
            {loadingOlder ? 'جارٍ تحميل الأقدم…' : 'تحميل رسائل أقدم'}
          </button>
        )}
        {snapshot.messages.map((m) => (
          <AdminAiMessage key={m.id} message={m} />
        ))}
        <AdminAiTurnStatus
          turn={(snapshot.activeTurns ?? snapshot.turns)?.[0]}
          onStop={onStop}
          onRetry={onRetry}
        />
        {snapshot.proposals?.map((p) => (
          <AdminAiActionProposalCard
            key={p.id}
            proposal={p}
            busy={busyProposalId === p.id}
            onConfirm={(phrase) => onConfirm(p.id, phrase)}
            onCancel={() => onCancelProposal(p.id)}
            onSecureInput={() => onSecureInput(p.id)}
          />
        ))}
      </div>
      {unseen && (
        <button
          onClick={() => {
            const el = scroller.current;
            if (el) el.scrollTop = el.scrollHeight;
            setUnseen(false);
          }}
          className="absolute bottom-3 left-1/2 z-10 min-h-11 -translate-x-1/2 rounded-full bg-[var(--admin-primary)] px-4 text-sm font-bold text-[var(--admin-primary-contrast)]"
        >
          رسائل جديدة
        </button>
      )}
    </div>
  );
}
