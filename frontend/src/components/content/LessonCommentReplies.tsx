'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import toast from 'react-hot-toast';
import {
  contentService,
  type LessonCommentDto,
} from '@/services/content-service';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { LessonCommentBubble } from './LessonCommentBubble';
import { LessonCommentReplyForm } from './LessonCommentReplyForm';

export function LessonCommentReplies({
  comment,
}: {
  comment: LessonCommentDto;
}) {
  const threadRef = useRef<HTMLDivElement>(null);
  const [nearViewport, setNearViewport] = useState(false);
  const [requested, setRequested] = useState(false);
  const [composing, setComposing] = useState(false);
  const [replies, setReplies] = useState<LessonCommentDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const generation = useRef({ value: 0 });
  const shouldLoad =
    requested || (nearViewport && (comment.replyCount ?? 0) > 0);

  useEffect(() => {
    const thread = threadRef.current;
    if (!thread) return;
    // Reveal replies automatically, without fetching every off-screen thread.
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          setNearViewport(true);
          observer.disconnect();
        }
      },
      { rootMargin: '200px' }
    );
    observer.observe(thread);
    return () => observer.disconnect();
  }, []);

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
    if (!shouldLoad) return;
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
  }, [shouldLoad, comment.lessonId, loadReplies]);

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
    <div ref={threadRef} className="min-w-0">
      <div className="flex flex-wrap gap-2">
        <button
          type="button"
          onClick={() => {
            setRequested(true);
            setComposing(true);
          }}
          className="min-h-11 rounded-lg px-2 text-sm font-bold text-[var(--admin-muted)] hover:bg-[var(--admin-card-soft)]"
        >
          رد
        </button>
      </div>
      {(shouldLoad || replies.length > 0) && (
        <div className="min-w-0 space-y-3 border-s border-[var(--admin-border)] ps-2 sm:ps-3">
          <div
            className="space-y-4"
            aria-label={`الردود على ${comment.authorName}`}
          >
            {replies.map((reply) => (
              <article key={reply.id} className="min-w-0">
                <LessonCommentBubble
                  comment={reply}
                  replyTo={comment.authorName}
                />
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
