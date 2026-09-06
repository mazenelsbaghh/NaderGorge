'use client';

import { useCallback, useEffect, useId, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import {
  contentService,
  type LessonCommentDto,
} from '@/services/content-service';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { UserAvatar } from '@/components/ui/UserAvatar';
import { formatCairoDateTime } from '@/lib/cairo-time';
import { LessonCommentReplyForm } from './LessonCommentReplyForm';

export function LessonCommentReplies({
  comment,
}: {
  comment: LessonCommentDto;
}) {
  const listId = useId();
  const [expanded, setExpanded] = useState(false);
  const [composing, setComposing] = useState(false);
  const [replies, setReplies] = useState<LessonCommentDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const generation = useRef({ value: 0 });

  const loadReplies = useCallback(
    async (offset = 0) => {
      const request = ++generation.current.value;
      setLoading(true);
      setError(false);
      try {
        const response = await contentService.getLessonReplies(
          comment.lessonId,
          comment.id,
          offset
        );
        if (request !== generation.current.value) return;
        const page = response.data.data ?? [];
        setReplies((previous) =>
          offset === 0
            ? page
            : [
                ...previous,
                ...page.filter(
                  (reply) => !previous.some((saved) => saved.id === reply.id)
                ),
              ]
        );
        setHasMore(page.length === 20);
      } catch {
        if (request === generation.current.value) setError(true);
      } finally {
        if (request === generation.current.value) setLoading(false);
      }
    },
    [comment.id, comment.lessonId]
  );

  useEffect(() => {
    if (!expanded) return;
    const requests = generation.current;
    void loadReplies();
    const unregister = registerCacheStore(
      `content:lesson:${comment.lessonId}:comments`,
      () => {},
      () => void loadReplies()
    );
    return () => {
      requests.value++;
      unregister();
    };
  }, [expanded, comment.lessonId, loadReplies]);

  async function sendReply(body: string) {
    const response = await contentService.createLessonComment(
      comment.lessonId,
      body,
      comment.id
    );
    if (!response.data.data) throw new Error('Missing reply receipt');
    setComposing(false);
    toast.success(response.data.data.message);
    await loadReplies();
  }

  return (
    <div className="mt-3">
      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={() => {
            setExpanded(true);
            setComposing(true);
          }}
          className="admin-btn-ghost min-h-11"
        >
          رد
        </button>
        <button
          type="button"
          aria-expanded={expanded}
          aria-controls={listId}
          onClick={() => setExpanded(!expanded)}
          className="admin-btn-ghost min-h-11"
        >
          {expanded
            ? 'إخفاء الردود'
            : `عرض الردود (${comment.replyCount ?? 0})`}
        </button>
      </div>
      {expanded && (
        <div
          id={listId}
          className="ms-3 mt-2 space-y-3 border-s border-[var(--admin-border)] ps-3 sm:ms-6 sm:ps-4"
        >
          <div
            className="space-y-4"
            aria-label={`الردود على ${comment.authorName}`}
          >
            {replies.map((reply) => (
              <article key={reply.id} className="min-w-0 py-2">
                <div className="flex items-center gap-2">
                  <UserAvatar
                    avatarSlug={reply.authorAvatarSlug}
                    fullName={reply.authorName}
                    size="xs"
                  />
                  <p className="text-sm font-bold text-[var(--admin-text)]">
                    {reply.authorName}
                  </p>
                  {reply.status === 'Pending' && (
                    <span className="text-xs text-[var(--admin-muted)]">
                      قيد المراجعة، ظاهر لك فقط
                    </span>
                  )}
                </div>
                <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-7 text-[var(--admin-text)]">
                  {reply.body}
                </p>
                <p className="mt-1 text-xs text-[var(--admin-muted)]">
                  {formatCairoDateTime(reply.createdAt, {
                    dateStyle: 'medium',
                    timeStyle: 'short',
                  })}
                </p>
              </article>
            ))}
          </div>
          {loading && (
            <p role="status" className="text-sm text-[var(--admin-muted)]">
              جارٍ تحميل الردود...
            </p>
          )}
          {error && (
            <p role="alert" className="text-sm text-[var(--admin-danger)]">
              تعذر تحميل الردود.{' '}
              <button
                type="button"
                onClick={() => void loadReplies()}
                className="min-h-11 underline"
              >
                إعادة المحاولة
              </button>
            </p>
          )}
          {!loading && !error && replies.length === 0 && (
            <p className="text-sm text-[var(--admin-muted)]">
              لا توجد ردود بعد.
            </p>
          )}
          {hasMore && (
            <button
              type="button"
              disabled={loading}
              onClick={() => void loadReplies(replies.length)}
              className="admin-btn-ghost min-h-11"
            >
              عرض المزيد من الردود
            </button>
          )}
          {composing && (
            <>
              <p className="text-xs text-[var(--admin-muted)]">
                يظهر ردك للجميع بعد مراجعته.
              </p>
              <LessonCommentReplyForm
                recipient={comment.authorName}
                onSubmit={sendReply}
                onCancel={() => setComposing(false)}
              />
            </>
          )}
        </div>
      )}
    </div>
  );
}
