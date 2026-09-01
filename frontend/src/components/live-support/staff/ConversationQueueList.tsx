import { useMemo, useRef, useState } from 'react';
import type { KeyboardEvent } from 'react';

import { LiveSupportChannelBadge } from '@/components/live-support/shared/LiveSupportChannelBadge';
import { getLiveSupportChannelPresentation } from '@/lib/live-support-channel';
import type { LiveSupportConversation } from '@/services/live-support-service';

const statusLabels: Record<LiveSupportConversation['status'], string> = {
  Waiting: 'بانتظار الدعم',
  Assigned: 'مسندة',
  Active: 'نشطة',
  Closed: 'مغلقة',
  Abandoned: 'منتهية',
};

interface ConversationQueueListProps {
  conversations?: LiveSupportConversation[];
  selectedId?: string;
  onSelect: (conversation: LiveSupportConversation) => void;
  waitingCount?: number;
}

interface MessengerPageOption {
  key: string;
  label: string;
  count: number;
}

export function ConversationQueueList({
  conversations = [],
  selectedId,
  onSelect,
  waitingCount = 0,
}: ConversationQueueListProps) {
  const [pageFilter, setPageFilter] = useState('all');
  const optionRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const messengerPages = useMemo(
    () => getMessengerPageOptions(conversations),
    [conversations]
  );
  const effectivePageFilter =
    pageFilter === 'all' ||
    messengerPages.some((page) => page.key === pageFilter)
      ? pageFilter
      : 'all';
  const visibleConversations = useMemo(
    () =>
      effectivePageFilter === 'all'
        ? conversations
        : conversations.filter(
            (conversation) =>
              conversation.channel === 'Messenger' &&
              getMessengerPageKey(conversation) === effectivePageFilter
          ),
    [conversations, effectivePageFilter]
  );

  function moveFocus(
    event: KeyboardEvent<HTMLButtonElement>,
    index: number
  ) {
    if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const nextIndex =
      event.key === 'Home'
        ? 0
        : event.key === 'End'
          ? visibleConversations.length - 1
          : (index +
              (event.key === 'ArrowDown' ? 1 : -1) +
              visibleConversations.length) %
            visibleConversations.length;
    optionRefs.current[nextIndex]?.focus();
  }

  return (
    <aside
      aria-label="المحادثات المسندة والطابور"
      className="flex h-full min-h-0 flex-col border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] lg:border-b-0 lg:border-l"
    >
      <div className="shrink-0 border-b border-[var(--admin-border)] px-4 py-4">
        <h2 className="font-bold text-[var(--admin-text)]">
          محادثاتي{' '}
          <span className="text-[var(--admin-muted)]">
            ({visibleConversations.length})
          </span>
        </h2>
        <p className="mt-1 text-sm text-[var(--admin-muted)]">
          {waitingCount
            ? `${waitingCount} بانتظار التوزيع`
            : 'لا توجد محادثات بانتظار التوزيع'}
        </p>
        {messengerPages.length > 1 ? (
          <label className="mt-3 block">
            <span className="mb-1 block text-xs font-bold text-[var(--admin-muted)]">
              صفحة فيسبوك
            </span>
            <select
              value={effectivePageFilter}
              onChange={(event) => setPageFilter(event.target.value)}
              className="h-10 w-full rounded-lg border border-[var(--admin-border)] bg-[var(--admin-card)] px-2.5 text-sm font-semibold text-[var(--admin-text)] outline-none focus-visible:border-[var(--admin-primary)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-15)]"
            >
              <option value="all">كل الصفحات</option>
              {messengerPages.map((page) => (
                <option key={page.key} value={page.key}>
                  {page.label} ({page.count})
                </option>
              ))}
            </select>
          </label>
        ) : null}
      </div>
      <div
        className="min-h-0 flex-1 divide-y divide-[var(--admin-border)] overflow-y-auto overscroll-contain"
        role="listbox"
        aria-label="المحادثات المسندة"
      >
        {visibleConversations.map((conversation, index) => {
          const selected = selectedId === conversation.id;
          const unreadCount = selected
            ? 0
            : (conversation.unreadParticipantMessageCount ?? 0);
          const participantName =
            conversation.participantName?.trim() ||
            (conversation.participantType === 'Guest' ? 'زائر' : 'طالب مسجل');
          const presentation = getLiveSupportChannelPresentation(conversation);
          const participantDetail =
            presentation.channel === 'Web'
              ? conversation.subject ||
                (conversation.participantType === 'Guest'
                  ? 'زائر غير مسجل'
                  : 'طالب مسجل')
              : presentation.channel === 'Messenger'
                ? conversation.subject || 'يرد عليها الموظفون فقط'
              : presentation.detail;

          return (
            <button
              key={conversation.id}
              ref={(element) => {
                optionRefs.current[index] = element;
              }}
              role="option"
              aria-selected={selected}
              type="button"
              onKeyDown={(event) => moveFocus(event, index)}
              onClick={() => onSelect(conversation)}
              className={`w-full px-4 py-4 text-right transition-colors focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-[var(--admin-primary)] ${selected ? 'bg-[var(--admin-primary-15)]' : 'bg-[var(--admin-card)] hover:bg-[var(--admin-hover)]'}`}
            >
              <span className="flex items-start justify-between gap-2">
                <span className="flex min-w-0 flex-col items-start gap-1.5">
                  <strong className="max-w-full truncate text-[var(--admin-text)]">
                    {participantName}
                  </strong>
                  <LiveSupportChannelBadge
                    channel={conversation.channel}
                    externalPageName={conversation.externalPageName}
                  />
                </span>
                {unreadCount > 0 ? (
                  <span className="shrink-0 rounded-full bg-[var(--admin-danger)] px-2 py-0.5 text-xs font-bold text-white">
                    {unreadCount} جديد
                  </span>
                ) : (
                  <small className="shrink-0 text-[var(--admin-muted)]">
                    {statusLabels[conversation.status]}
                  </small>
                )}
              </span>
              <span
                className="mt-1.5 block truncate text-sm text-[var(--admin-muted)]"
                title={participantDetail}
                dir="auto"
              >
                {participantDetail}
              </span>
            </button>
          );
        })}
        {visibleConversations.length === 0 ? (
          <p className="px-5 py-10 text-center text-sm leading-6 text-[var(--admin-muted)]">
            {effectivePageFilter === 'all' ? (
              <>
                لا توجد محادثات مسندة إليك الآن.
                <br />
                ستظهر المحادثة التالية تلقائيًا.
              </>
            ) : (
              'لا توجد محادثات مسندة من هذه الصفحة الآن.'
            )}
          </p>
        ) : null}
      </div>
    </aside>
  );
}

function getMessengerPageOptions(
  conversations: LiveSupportConversation[]
): MessengerPageOption[] {
  const pages = new Map<string, MessengerPageOption>();

  for (const conversation of conversations) {
    if (conversation.channel !== 'Messenger') continue;
    const key = getMessengerPageKey(conversation);
    if (!key) continue;
    const label = conversation.externalPageName?.trim() || 'صفحة فيسبوك';
    const current = pages.get(key);
    pages.set(key, {
      key,
      label,
      count: (current?.count ?? 0) + 1,
    });
  }

  return [...pages.values()].sort((left, right) =>
    left.label.localeCompare(right.label, 'ar')
  );
}

function getMessengerPageKey(conversation: LiveSupportConversation) {
  return (
    conversation.externalPageId?.trim() ||
    conversation.externalPageName?.trim() ||
    ''
  );
}
