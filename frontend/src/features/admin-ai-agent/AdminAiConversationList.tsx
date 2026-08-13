import { Archive, MessageSquarePlus, RotateCcw } from 'lucide-react';
import type { AdminAiConversationSummary } from '@/services/admin-ai-agent-contract';
export function AdminAiConversationList({
  items,
  selectedId,
  archived,
  onSelect,
  onCreate,
  onToggleArchived,
  onArchive,
  onLoadMore,
  hasMore,
  loadingMore,
}: {
  items: AdminAiConversationSummary[];
  selectedId?: string;
  archived: boolean;
  onSelect: (id: string) => void;
  onCreate: () => void;
  onToggleArchived: () => void;
  onArchive: (item: AdminAiConversationSummary) => void;
  onLoadMore: () => void;
  hasMore: boolean;
  loadingMore: boolean;
}) {
  return (
    <aside
      aria-label="سجل المحادثات"
      className="flex min-h-0 w-full flex-1 flex-col border-b border-[var(--admin-border)] bg-[var(--admin-card-soft)] lg:w-auto lg:flex-none lg:border-b-0 lg:border-l"
    >
      <div className="p-3">
        <button
          onClick={onCreate}
          className="flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 text-sm font-black text-[var(--admin-primary-contrast)]"
        >
          <MessageSquarePlus className="h-4 w-4" />
          محادثة جديدة
        </button>
        <button
          onClick={onToggleArchived}
          className="mt-2 min-h-11 w-full text-sm font-bold text-[var(--admin-muted)]"
        >
          {archived ? 'عرض النشطة' : 'عرض المؤرشفة'}
        </button>
      </div>
      <nav
        className="min-h-0 flex-1 overflow-y-auto p-2"
        aria-label={archived ? 'المحادثات المؤرشفة' : 'المحادثات النشطة'}
      >
        {items.length === 0 ? (
          <p className="p-4 text-center text-sm text-[var(--admin-muted)]">
            لا توجد محادثات هنا.
          </p>
        ) : (
          items.map((item) => (
            <div
              key={item.id}
              className={`mb-1 flex rounded-xl ${selectedId === item.id ? 'bg-[var(--admin-primary-15)]' : ''}`}
            >
              <button
                onClick={() => onSelect(item.id)}
                className="min-h-12 min-w-0 flex-1 truncate px-3 text-right text-sm font-bold"
              >
                {item.title}
              </button>
              <button
                onClick={() => onArchive(item)}
                className="min-h-11 min-w-11"
                aria-label={
                  archived ? `استعادة ${item.title}` : `أرشفة ${item.title}`
                }
              >
                {archived ? (
                  <RotateCcw className="mx-auto h-4 w-4" />
                ) : (
                  <Archive className="mx-auto h-4 w-4" />
                )}
              </button>
            </div>
          ))
        )}
        {hasMore && (
          <button
            type="button"
            onClick={onLoadMore}
            disabled={loadingMore}
            className="min-h-11 w-full rounded-xl border border-[var(--admin-border)] px-3 text-sm font-bold disabled:opacity-50"
          >
            {loadingMore ? 'جارٍ تحميل المزيد…' : 'تحميل محادثات أقدم'}
          </button>
        )}
      </nav>
    </aside>
  );
}
