import { Menu, Pencil } from 'lucide-react';
import type { AdminAiConversationSummary } from '@/services/admin-ai-agent-contract';
export function AdminAiConversationHeader({
  conversation,
  connection,
  onHistory,
  onRename,
}: {
  conversation?: AdminAiConversationSummary;
  connection: string;
  onHistory: () => void;
  onRename: () => void;
}) {
  return (
    <header className="flex min-h-16 items-center gap-3 border-b border-[var(--admin-border)] px-4">
      <button
        onClick={onHistory}
        className="min-h-11 min-w-11 lg:hidden"
        aria-label="عرض المحادثات"
      >
        <Menu className="mx-auto h-5 w-5" />
      </button>
      <div className="min-w-0">
        <h2 className="truncate font-black">
          {conversation?.title || 'وكيل الإدارة AI'}
        </h2>
        <p className="text-xs text-[var(--admin-muted)]">
          {connection === 'connected'
            ? 'متصل'
            : connection === 'reconnecting'
              ? 'يعيد الاتصال…'
              : 'غير متصل'}
        </p>
      </div>
      {conversation && (
        <button
          onClick={onRename}
          className="mr-auto min-h-11 min-w-11"
          aria-label="تعديل اسم المحادثة"
        >
          <Pencil className="mx-auto h-4 w-4" />
        </button>
      )}
    </header>
  );
}
