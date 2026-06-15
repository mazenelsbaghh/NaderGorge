'use client';

import { useCallback, useEffect, useState } from 'react';
import { useParams, useSearchParams, useRouter } from 'next/navigation';
import {
  homeworkService,
  type StartHomeworkAttemptDto,
  type HomeworkResultDto,
} from '@/services/homework-service';
import { HomeworkViewer } from '@/components/homework/HomeworkViewer';
import { HomeworkResultPanel } from '@/components/homework/HomeworkResultPanel';

export default function HomeworkPageClient() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();

  const homeworkId = params.homeworkId as string;
  const packageId = searchParams.get('packageId') || undefined;
  const lessonId = searchParams.get('lessonId') || undefined;

  const [attempt, setAttempt] = useState<StartHomeworkAttemptDto | null>(null);
  const [completedResult, setCompletedResult] = useState<HomeworkResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const loadHomework = useCallback(async () => {
    if (!homeworkId) return;

    setLoading(true);
    setError('');
    setCompletedResult(null);

    try {
      const res = await homeworkService.startHomework(homeworkId);
      const data = res.data.data;

      if (data.alreadyCompleted) {
        // Homework was already completed → fetch the result
        try {
          const resultRes = await homeworkService.getHomeworkResult(homeworkId);
          setCompletedResult(resultRes.data.data);
          setAttempt(null);
        } catch {
          // If fetching result fails, show the data we have
          setError('تم حل هذا الواجب مسبقاً ولكن تعذر تحميل النتيجة.');
          setAttempt(null);
        }
      } else {
        setAttempt(data);
      }
    } catch (err: unknown) {
      const apiError = err as { response?: { status?: number; data?: { message?: string; errors?: string[] } } };

      if (apiError.response?.status === 403) {
        setError('لا يمكنك الوصول لهذا الواجب أو أن الحصة الخاصة به ما زالت مغلقة.');
      } else if (apiError.response?.status === 404) {
        setError('الواجب غير موجود.');
      } else {
        setError(apiError.response?.data?.message || 'حدث خطأ أثناء تحميل الواجب.');
      }
      setAttempt(null);
    } finally {
      setLoading(false);
    }
  }, [homeworkId]);

  useEffect(() => {
    void loadHomework();
  }, [loadHomework]);

  if (loading) {
    return (
      <div className="mx-auto max-w-4xl space-y-6 animate-pulse">
        <div className="h-32 w-full rounded-2xl bg-[var(--admin-card-soft)]"></div>
        <div className="space-y-4">
          {[1, 2, 3].map(i => <div key={i} className="h-40 w-full rounded-2xl bg-[var(--admin-card-soft)]"></div>)}
        </div>
      </div>
    );
  }

  if (error || !attempt) {
    if (completedResult) {
      return (
        <div className="mx-auto max-w-5xl pb-16">
          <HomeworkResultPanel result={completedResult} packageId={packageId} lessonId={lessonId} onRestart={loadHomework} />
        </div>
      );
    }

    return (
      <div className="mx-auto max-w-2xl rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-8 text-center">
        <h2 className="mb-4 text-xl font-bold text-[var(--admin-danger)]">الواجب غير متاح</h2>
        <p className="mb-6 text-[var(--admin-text)]">{error}</p>
        <button
          type="button"
          onClick={() => {
          if (packageId && lessonId) {
            router.push(`/student/packages/${packageId}/lessons/${lessonId}`);
          } else {
            router.push(packageId ? `/student/packages/${packageId}` : '/student');
          }
        }}
          className="inline-flex min-h-12 w-full items-center justify-center rounded-xl bg-[var(--admin-danger)] px-6 py-3 font-semibold text-[var(--admin-primary-contrast)] transition hover:brightness-110 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-danger-10)] sm:w-auto"
        >
          العودة
        </button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl pb-16">
      <button
        type="button"
        onClick={() => {
          if (packageId && lessonId) {
            router.push(`/student/packages/${packageId}/lessons/${lessonId}`);
          } else {
            router.push(packageId ? `/student/packages/${packageId}` : '/student');
          }
        }}
        className="mb-6 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:text-[var(--admin-text)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)] sm:mb-8 sm:w-auto sm:justify-start sm:rounded-full sm:border-transparent sm:bg-transparent sm:px-3"
      >
        <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
        </svg>
        إلغاء حل الواجب
      </button>

      <HomeworkViewer
        homeworkId={homeworkId}
        attempt={attempt}
        packageId={packageId}
        lessonId={lessonId}
        onRestart={loadHomework}
      />
    </div>
  );
}
