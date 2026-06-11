'use client';

import { useCallback, useEffect, useState } from 'react';
import { MessageSquareText, Send, ShieldCheck, Clock3 } from 'lucide-react';
import toast from 'react-hot-toast';

import {
  contentService,
  type CreateLessonCommentResponse,
  type LessonCommentDto,
} from '@/services/content-service';
import { useAuthStore } from '@/stores/auth-store';
import { UserAvatar } from '@/components/ui/UserAvatar';
import { registerCacheStore, unregisterCacheStore } from '@/lib/cache-invalidation';

type LessonCommentsSectionProps = {
  lessonId: string;
};

const formatCommentDate = (value: string) =>
  new Intl.DateTimeFormat('ar-EG', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));

const getStatusLabel = (status: string) => {
  switch (status) {
    case 'Approved':
      return 'مقبول';
    case 'Rejected':
      return 'مرفوض';
    default:
      return 'قيد المراجعة';
  }
};

const getStatusClasses = (status: string) => {
  switch (status) {
    case 'Approved':
      return 'border-[var(--admin-success-20)] bg-[var(--admin-success-10)] text-[var(--admin-success)]';
    case 'Rejected':
      return 'border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] text-[var(--admin-danger)]';
    default:
      return 'border-[var(--admin-primary-15)] bg-[var(--admin-primary-10)] text-[var(--admin-primary)]';
  }
};

export function LessonCommentsSection({ lessonId }: LessonCommentsSectionProps) {
  const [approvedComments, setApprovedComments] = useState<LessonCommentDto[]>([]);
  const [myComments, setMyComments] = useState<LessonCommentDto[]>([]);
  const [body, setBody] = useState('');
  const user = useAuthStore((state) => state.user);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  const loadComments = useCallback(async () => {
    setLoading(true);
    try {
      const [approvedRes, mineRes] = await Promise.all([
        contentService.getLessonComments(lessonId),
        contentService.getMyLessonComments(lessonId),
      ]);

      setApprovedComments(approvedRes.data?.data ?? []);
      setMyComments(mineRes.data?.data ?? []);
    } catch {
      setApprovedComments([]);
      setMyComments([]);
    } finally {
      setLoading(false);
    }
  }, [lessonId]);

  useEffect(() => {
    loadComments();
  }, [loadComments]);

  useEffect(() => {
    registerCacheStore(`content:lesson:${lessonId}:comments`, () => {}, loadComments);
    return () => {
      unregisterCacheStore(`content:lesson:${lessonId}:comments`);
    };
  }, [lessonId, loadComments]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = body.trim();
    if (!trimmed) {
      toast.error('اكتب تعليقًا صالحًا قبل الإرسال.');
      return;
    }

    setSubmitting(true);
    try {
      const response = await contentService.createLessonComment(lessonId, trimmed);
      const created = response.data?.data as CreateLessonCommentResponse | undefined;

      if (created) {
        setMyComments((current) => [
          {
            id: created.id,
            lessonId,
            authorName: 'أنت',
            body: trimmed,
            status: created.status,
            createdAt: created.createdAt,
            isOwnComment: true,
            authorAvatarSlug: user?.avatarSlug,
          },
          ...current,
        ]);
      }

      setBody('');
      toast.success(created?.message || 'تم إرسال التعليق بنجاح.');
    } catch {
      // Toast is handled by axios interceptor.
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 shadow-sm sm:p-8">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <div className="mb-4 inline-flex items-center gap-3 rounded-full border border-[var(--admin-primary-15)] bg-[var(--admin-primary-10)] px-4 py-2 text-xs font-black tracking-[0.18em] text-[var(--admin-primary)]">
            <MessageSquareText className="h-4 w-4" />
            نقاش الدرس
          </div>
          <h2 className="text-2xl font-black tracking-tight text-[var(--admin-text)] sm:text-3xl">
            التعليقات تحت الفيديو
          </h2>
          <p className="mt-3 max-w-3xl text-sm font-medium leading-relaxed text-[var(--admin-muted)] sm:text-base">
            اكتب سؤالك أو استفسارك عن هذا الدرس هنا. التعليقات الجديدة لا تظهر للجميع إلا بعد مراجعة المدرس.
          </p>
        </div>
        <div className="inline-flex items-center gap-2 self-start rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-xs font-bold text-[var(--admin-muted)]">
          <ShieldCheck className="h-4 w-4 text-[var(--admin-primary)]" />
          مراجعة المدرس مفعّلة
        </div>
      </div>

      <form onSubmit={handleSubmit} className="mt-8 rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 sm:p-6">
        <label htmlFor="lesson-comment-body" className="mb-3 block text-sm font-bold text-[var(--admin-text)]">
          أضف تعليقًا جديدًا
        </label>
        <textarea
          id="lesson-comment-body"
          className="admin-input min-h-[140px] w-full resize-y rounded-[24px] bg-[var(--admin-card)] px-5 py-4 text-base"
          placeholder="اكتب تعليقك أو سؤالك بوضوح..."
          value={body}
          onChange={(e) => setBody(e.target.value)}
          maxLength={2000}
        />
        <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs font-medium text-[var(--admin-muted)]">
            {body.trim().length}/2000 حرف. سيتم إرسال التعليق بحالة قيد المراجعة.
          </p>
          <button
            type="submit"
            disabled={submitting}
            className="inline-flex min-h-12 items-center justify-center gap-2 rounded-[20px] border border-[var(--admin-primary)] bg-[var(--admin-primary)] px-6 py-3 text-sm font-black text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary)]/20 transition-all hover:-translate-y-0.5 hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-60"
          >
            <Send className="h-4 w-4" />
            {submitting ? 'جارٍ الإرسال...' : 'إرسال التعليق'}
          </button>
        </div>
      </form>

      <div className="mt-8 grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
        <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-5 sm:p-6">
          <div className="mb-5 flex items-center justify-between gap-4">
            <div>
              <h3 className="text-lg font-black text-[var(--admin-text)]">التعليقات الظاهرة للجميع</h3>
              <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">
                تظهر هنا التعليقات التي تمت الموافقة عليها فقط.
              </p>
            </div>
            <span className="rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
              {approvedComments.length}
            </span>
          </div>

          {loading ? (
            <div className="space-y-3">
              <div className="h-24 animate-pulse rounded-[24px] bg-[var(--admin-card-soft)]" />
              <div className="h-24 animate-pulse rounded-[24px] bg-[var(--admin-card-soft)]" />
            </div>
          ) : approvedComments.length === 0 ? (
            <div className="rounded-[24px] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-10 text-center">
              <p className="text-base font-bold text-[var(--admin-text)]">لا توجد تعليقات معتمدة حتى الآن.</p>
              <p className="mt-2 text-sm font-medium text-[var(--admin-muted)]">
                يمكنك أن تكون أول من يبدأ النقاش حول هذا الدرس.
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {approvedComments.map((comment) => (
                <article
                  key={comment.id}
                  className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-4"
                >
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <UserAvatar
                        avatarSlug={comment.authorAvatarSlug}
                        fullName={comment.authorName}
                        size="sm"
                      />
                      <div>
                        <p className="text-sm font-black text-[var(--admin-text)]">{comment.authorName}</p>
                        <p className="mt-1 text-xs font-medium text-[var(--admin-muted)]">
                          {formatCommentDate(comment.createdAt)}
                        </p>
                      </div>
                    </div>
                    <span className="rounded-full border border-[var(--admin-success-20)] bg-[var(--admin-success-10)] px-3 py-1 text-xs font-black text-[var(--admin-success)]">
                      معتمد
                    </span>
                  </div>
                  <p className="mt-4 whitespace-pre-wrap text-sm font-medium leading-7 text-[var(--admin-text)]">
                    {comment.body}
                  </p>
                </article>
              ))}
            </div>
          )}
        </div>

        <div className="rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-bg)] p-5 sm:p-6">
          <div className="mb-5 flex items-center gap-3">
            <Clock3 className="h-5 w-5 text-[var(--admin-primary)]" />
            <div>
              <h3 className="text-lg font-black text-[var(--admin-text)]">تعليقاتي</h3>
              <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">
                تتابع من هنا حالة التعليقات التي أرسلتها.
              </p>
            </div>
          </div>

          {loading ? (
            <div className="space-y-3">
              <div className="h-20 animate-pulse rounded-[24px] bg-[var(--admin-card-soft)]" />
              <div className="h-20 animate-pulse rounded-[24px] bg-[var(--admin-card-soft)]" />
            </div>
          ) : myComments.length === 0 ? (
            <div className="rounded-[24px] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-10 text-center">
              <p className="text-base font-bold text-[var(--admin-text)]">لم ترسل أي تعليقات بعد.</p>
              <p className="mt-2 text-sm font-medium text-[var(--admin-muted)]">
                بعد الإرسال ستظهر هنا حالة كل تعليق.
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {myComments.map((comment) => (
                <article
                  key={comment.id}
                  className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-4"
                >
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <UserAvatar
                        avatarSlug={comment.authorAvatarSlug}
                        fullName="أنت"
                        size="xs"
                      />
                      <p className="text-xs font-medium text-[var(--admin-muted)]">
                        {formatCommentDate(comment.createdAt)}
                      </p>
                    </div>
                    <span className={`rounded-full border px-3 py-1 text-xs font-black ${getStatusClasses(comment.status)}`}>
                      {getStatusLabel(comment.status)}
                    </span>
                  </div>
                  <p className="mt-3 whitespace-pre-wrap text-sm font-medium leading-7 text-[var(--admin-text)]">
                    {comment.body}
                  </p>
                </article>
              ))}
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
