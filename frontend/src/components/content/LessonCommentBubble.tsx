'use client';

import type { LessonCommentDto } from '@/services/content-service';
import { UserAvatar } from '@/components/ui/UserAvatar';
import { formatCairoDateTime } from '@/lib/cairo-time';

export function LessonCommentBubble({
  comment,
  replyTo,
}: {
  comment: LessonCommentDto;
  replyTo?: string;
}) {
  return (
    <div className="flex min-w-0 items-start gap-2.5">
      <UserAvatar
        avatarSlug={comment.authorAvatarSlug}
        fullName={comment.authorName}
        size="sm"
      />
      <div className="min-w-0 flex-1">
        <div className="w-fit max-w-full rounded-2xl bg-[var(--admin-card-soft)] px-3.5 py-2.5 [overflow-wrap:anywhere]">
          <p className="text-sm font-bold text-[var(--admin-text)]">
            {comment.authorName}
          </p>
          {replyTo && (
            <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">
              ردًا على{' '}
              <span className="font-bold text-[var(--admin-primary)]">
                {replyTo}
              </span>
            </p>
          )}
          <p
            dir="auto"
            className="mt-1 whitespace-pre-wrap text-base leading-7 text-[var(--admin-text)]"
          >
            {comment.body}
          </p>
        </div>
        <div className="mt-1 flex min-w-0 flex-wrap items-center gap-x-3 gap-y-1 px-1 text-xs leading-5 text-[var(--admin-muted)]">
          <time dateTime={comment.createdAt}>
            {formatCairoDateTime(comment.createdAt, {
              dateStyle: 'medium',
              timeStyle: 'short',
            })}
          </time>
          {comment.status !== 'Approved' && (
            <span>
              {comment.status === 'Rejected'
                ? 'مرفوض، ظاهر لك فقط'
                : 'قيد المراجعة، ظاهر لك فقط'}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
