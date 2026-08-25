import type { AdminAiMessage as Message } from '@/services/admin-ai-agent-contract';
import { formatCairoDateTime } from '@/lib/cairo-time';
import { AdminAiEvidenceDisclosure } from './AdminAiEvidenceDisclosure';

export function AdminAiMessage({ message }: { message: Message }) {
  const mine = message.role === 'Admin';
  return (
    <article
      aria-label={
        mine
          ? 'رسالتك'
          : message.role === 'Assistant'
            ? 'رد الوكيل'
            : 'تحديث حالة'
      }
      className={`max-w-[min(90%,48rem)] rounded-2xl px-4 py-3 text-sm leading-7 [overflow-wrap:anywhere] ${mine ? 'mr-auto bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'ml-auto border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)]'}`}
    >
      <p className="whitespace-pre-wrap [unicode-bidi:plaintext]" dir="auto">
        {message.content}
      </p>
      <AdminAiEvidenceDisclosure answer={message.answer} />
      <time
        className={`mt-2 block text-[11px] ${mine ? 'text-[var(--admin-primary-contrast)] opacity-70' : 'text-[var(--admin-muted)]'}`}
        dateTime={message.createdAt}
      >
        {formatCairoDateTime(message.createdAt, {
          hour: '2-digit',
          minute: '2-digit',
        })}
      </time>
    </article>
  );
}
