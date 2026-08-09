import { useRef } from 'react';
import type { KeyboardEvent } from 'react';
import type { LiveSupportConversation } from '@/services/live-support-service';

const statusLabels: Record<LiveSupportConversation['status'], string> = {
  Waiting: 'بانتظار الدعم',
  Assigned: 'مسندة',
  Active: 'نشطة',
  Closed: 'مغلقة',
  Abandoned: 'منتهية',
};

export function ConversationQueueList({ conversations = [], selectedId, onSelect, waitingCount = 0 }: { conversations?: LiveSupportConversation[]; selectedId?: string; onSelect: (conversation: LiveSupportConversation) => void; waitingCount?: number }) {
  const optionRefs = useRef<Array<HTMLButtonElement | null>>([]);

  function moveFocus(event: KeyboardEvent<HTMLButtonElement>, index: number) {
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const nextIndex = event.key === 'Home'
      ? 0
      : event.key === 'End'
        ? conversations.length - 1
        : (index + (event.key === 'ArrowDown' ? 1 : -1) + conversations.length) % conversations.length;
    optionRefs.current[nextIndex]?.focus();
  }

  return (
    <aside aria-label="المحادثات المسندة والطابور" className="flex h-full min-h-0 flex-col border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] lg:border-b-0 lg:border-l">
      <div className="shrink-0 border-b border-[var(--admin-border)] px-4 py-4">
        <h2 className="font-bold text-[var(--admin-text)]">محادثاتي <span className="text-[var(--admin-muted)]">({conversations.length})</span></h2>
        <p className="mt-1 text-sm text-[var(--admin-muted)]">{waitingCount ? `${waitingCount} بانتظار التوزيع` : 'لا توجد محادثات بانتظار التوزيع'}</p>
      </div>
      <div className="min-h-0 flex-1 divide-y divide-[var(--admin-border)] overflow-y-auto overscroll-contain" role="listbox" aria-label="المحادثات المسندة">
        {conversations.map((conversation, index) => {
          const selected = selectedId === conversation.id;
          const unreadCount = selected ? 0 : conversation.unreadParticipantMessageCount ?? 0;
          const participantName = conversation.participantName?.trim() || (conversation.participantType === 'Guest' ? 'زائر' : 'طالب مسجل');
          const participantDetail = conversation.subject || (conversation.participantType === 'Guest' ? 'زائر غير مسجل' : 'طالب مسجل');
          return (
            <button
              key={conversation.id}
              ref={(element) => { optionRefs.current[index] = element; }}
              role="option"
              aria-selected={selected}
              type="button"
              onKeyDown={(event) => moveFocus(event, index)}
              onClick={() => onSelect(conversation)}
              className={`w-full px-4 py-4 text-right transition-colors focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-[var(--admin-primary)] ${selected ? 'bg-[var(--admin-primary-15)]' : 'bg-[var(--admin-card)] hover:bg-[var(--admin-hover)]'}`}
            >
              <span className="flex items-center justify-between gap-2">
                <strong className="truncate text-[var(--admin-text)]">{participantName}</strong>
                {unreadCount > 0 ? <span className="rounded-full bg-[var(--admin-danger)] px-2 py-0.5 text-xs font-bold text-white">{unreadCount} جديد</span> : <small className="shrink-0 text-[var(--admin-muted)]">{statusLabels[conversation.status]}</small>}
              </span>
              <span className="mt-1 block truncate text-sm text-[var(--admin-muted)]" title={conversation.subject}>{participantDetail}</span>
            </button>
          );
        })}
        {conversations.length === 0 ? <p className="px-5 py-10 text-center text-sm leading-6 text-[var(--admin-muted)]">لا توجد محادثات مسندة إليك الآن.<br />ستظهر المحادثة التالية تلقائيًا.</p> : null}
      </div>
    </aside>
  );
}
