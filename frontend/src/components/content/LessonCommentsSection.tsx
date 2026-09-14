'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Send } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  contentService,
  type LessonCommentDto,
} from '@/services/content-service';
import { useAuthStore } from '@/stores/auth-store';
import { UserAvatar } from '@/components/ui/UserAvatar';
import { registerCacheStore } from '@/lib/cache-invalidation';
import { LessonCommentReplies } from './LessonCommentReplies';
import { LessonCommentBubble } from './LessonCommentBubble';

export function LessonCommentsSection({ lessonId }: { lessonId: string }) {
  const [approvedComments, setApprovedComments] = useState<LessonCommentDto[]>(
    []
  );
  const [myComments, setMyComments] = useState<LessonCommentDto[]>([]);
  const [mineOnly, setMineOnly] = useState(false);
  const [body, setBody] = useState('');
  const user = useAuthStore((state) => state.user);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [submitError, setSubmitError] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const inFlight = useRef(false);

  const loadComments = useCallback(async () => {
    setLoading(true);
    setLoadError(false);
    try {
      const [approved, mine] = await Promise.all([
        contentService.getLessonComments(lessonId),
        contentService.getMyLessonComments(lessonId),
      ]);
      setApprovedComments(approved.data.data ?? []);
      setMyComments(mine.data.data ?? []);
    } catch {
      setLoadError(true);
    } finally {
      setLoading(false);
    }
  }, [lessonId]);

  useEffect(() => {
    void loadComments();
  }, [loadComments]);
  useEffect(
    () =>
      registerCacheStore(
        `content:lesson:${lessonId}:comments`,
        () => {},
        loadComments
      ),
    [lessonId, loadComments]
  );

  const submitComment = async (event: React.FormEvent) => {
    event.preventDefault();
    const trimmed = body.trim();
    if (!trimmed || inFlight.current) return;
    inFlight.current = true;
    setSubmitting(true);
    setSubmitError(false);
    try {
      const response = await contentService.createLessonComment(
        lessonId,
        trimmed
      );
      const created = response.data.data;
      if (!created) throw new Error('Missing comment receipt');
      setMyComments((current) => [
        {
          id: created.id,
          lessonId,
          authorName: user?.fullName || 'أنت',
          body: trimmed,
          status: created.status,
          createdAt: created.createdAt,
          isOwnComment: true,
          authorAvatarSlug: user?.avatarSlug,
        },
        ...current,
      ]);
      setBody('');
      toast.success(created.message || 'تم إرسال التعليق للمراجعة.');
    } catch {
      setSubmitError(true);
    } finally {
      inFlight.current = false;
      setSubmitting(false);
    }
  };

  // Published own comments appear in both endpoints; keep one stable thread.
  const comments = [
    ...new Map(
      (mineOnly ? myComments : [...myComments, ...approvedComments])
        .filter((comment) => !comment.parentCommentId)
        .map((comment) => [comment.id, comment])
    ).values(),
  ].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt));

  return (
    <section
      data-testid="lesson-discussion"
      dir="rtl"
      className="min-w-0 w-full max-w-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-5 sm:p-6"
    >
      <h2 className="text-xl font-bold text-[var(--admin-text)]">
        التعليقات تحت الفيديو
      </h2>
      <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
        اسأل وشارك زملاءك. التعليقات والردود تظهر للجميع بعد مراجعة المدرس.
      </p>
      <form
        onSubmit={submitComment}
        className="mt-5 flex min-w-0 items-start gap-2.5"
      >
        <UserAvatar
          avatarSlug={user?.avatarSlug}
          fullName={user?.fullName || 'أنت'}
          size="sm"
        />
        <div className="min-w-0 flex-1">
          <label htmlFor="lesson-comment-body" className="sr-only">
            أضف تعليقًا جديدًا
          </label>
          <textarea
            id="lesson-comment-body"
            required
            rows={2}
            maxLength={2000}
            disabled={submitting}
            value={body}
            onChange={(event) => setBody(event.target.value)}
            placeholder="اكتب تعليقك أو سؤالك…"
            className="block w-full min-w-0 resize-y rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 py-2 text-base leading-7 text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] focus-visible:outline-2 focus-visible:outline-[var(--admin-primary)]"
          />
          <div className="mt-1 flex flex-wrap items-center justify-between gap-2">
            <span className="text-xs text-[var(--admin-muted)]">
              {body.length}/2000
            </span>
            <button
              type="submit"
              disabled={submitting || !body.trim()}
              className="inline-flex min-h-11 items-center gap-2 rounded-xl px-3 text-sm font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-card-soft)] disabled:opacity-50"
            >
              <Send className="size-4" aria-hidden />
              {submitting ? 'جارٍ الإرسال…' : 'إرسال التعليق'}
            </button>
          </div>
          {submitError && (
            <p
              role="alert"
              className="text-sm leading-6 text-[var(--admin-danger)]"
            >
              تعذر تأكيد الإرسال. تعليقك محفوظ هنا؛ تحقق من التعليقات قبل إعادة
              المحاولة.
            </p>
          )}
        </div>
      </form>
      <div
        role="group"
        aria-label="تصفية التعليقات"
        className="my-4 flex flex-wrap gap-2 border-b border-[var(--admin-border)] pb-3"
      >
        {[false, true].map((onlyMine) => (
          <button
            key={String(onlyMine)}
            type="button"
            aria-pressed={mineOnly === onlyMine}
            onClick={() => setMineOnly(onlyMine)}
            className={`min-h-11 rounded-xl px-4 text-sm font-bold ${mineOnly === onlyMine ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-muted)] hover:bg-[var(--admin-card-soft)]'}`}
          >
            {onlyMine ? 'تعليقاتي' : 'كل التعليقات'}
          </button>
        ))}
      </div>
      {loadError && (
        <div role="alert" className="mb-4 text-sm text-[var(--admin-danger)]">
          تعذر تحديث التعليقات.{' '}
          <button
            type="button"
            onClick={() => void loadComments()}
            className="min-h-11 px-2 underline"
          >
            إعادة المحاولة
          </button>
        </div>
      )}
      {loading && comments.length === 0 ? (
        <p role="status" className="py-4 text-sm text-[var(--admin-muted)]">
          جارٍ تحميل التعليقات…
        </p>
      ) : comments.length === 0 && !loadError ? (
        <p className="py-4 text-sm text-[var(--admin-muted)]">
          {mineOnly ? 'لم ترسل تعليقات بعد.' : 'ابدأ النقاش بسؤال عن الدرس.'}
        </p>
      ) : null}
      <div className="min-w-0 space-y-5">
        {comments.map((comment) => (
          <article
            key={comment.id}
            data-comment-id={comment.id}
            className="min-w-0 max-w-full"
          >
            <LessonCommentBubble comment={comment} />
            {comment.status === 'Approved' && (
              <div className="min-w-0 ps-10 sm:ps-11">
                <LessonCommentReplies comment={comment} />
              </div>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}
