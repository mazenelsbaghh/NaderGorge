'use client';

import { useCallback, useEffect, useState } from 'react';
import { useParams, useSearchParams, useRouter } from 'next/navigation';
import { examService, ActiveExamAttemptDto, ExamResultDto } from '@/services/exam-service';
import { ExamViewer, ExamResultPanel } from '@/components/exams/ExamViewer';

export default function ExamPageClient() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  
  const examId = params.examId as string;
  const packageId = searchParams.get('packageId') || undefined;
  const lessonId = searchParams.get('lessonId') || undefined;
  
  const [exam, setExam] = useState<ActiveExamAttemptDto | null>(null);
  const [passedResult, setPassedResult] = useState<ExamResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const loadExam = useCallback(async () => {
    if (!examId) return;

    setLoading(true);
    setError('');
    setPassedResult(null);

    try {
      try {
        const passedResultResponse = await examService.getLatestPassedResult(examId);
        setPassedResult(passedResultResponse.data.data);
        setExam(null);
        return;
      } catch (err: unknown) {
        const passedResultError = err as { response?: { status?: number; data?: { message?: string } } };
        if (passedResultError.response?.status && passedResultError.response.status !== 404) {
          if (passedResultError.response.data?.message) {
            setError(passedResultError.response.data.message);
          } else if (passedResultError.response.status === 403) {
            setError('لا يمكنك الوصول لهذا الامتحان أو أن الحصة الخاصة به ما زالت مغلقة.');
          } else {
            setError('تعذر تحميل نتيجة الامتحان الحالية.');
          }
          setExam(null);
          return;
        }
      }

      try {
        const res = await examService.startExam(examId);
        setExam(res.data.data);
      } catch (err: unknown) {
        const apiError = err as { response?: { status?: number; data?: { errors?: string[]; message?: string } } };

        if (apiError.response?.data?.message) {
          setError(apiError.response.data.message);
        } else if (apiError.response?.status === 403) {
          setError('لا يمكنك الوصول لهذا الامتحان أو أن الحصة الخاصة به ما زالت مغلقة.');
        } else if (apiError.response?.data?.errors?.includes('لقد اجتزت هذا الامتحان بالفعل.')) {
          try {
            const passedResultResponse = await examService.getLatestPassedResult(examId);
            setPassedResult(passedResultResponse.data.data);
            setExam(null);
            return;
          } catch {
            setError('لقد اجتزت هذا الامتحان بالفعل.');
          }
        } else {
          setError('الامتحان غير موجود أو حدث خطأ أثناء تحميله.');
        }
        setExam(null);
      }

    } finally {
      setLoading(false);
    }
  }, [examId]);

  useEffect(() => {
    void loadExam();
  }, [loadExam]);

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

  if (error || !exam) {
    if (passedResult) {
      return (
        <div className="mx-auto max-w-5xl pb-16">
          <ExamResultPanel result={passedResult} packageId={packageId} lessonId={passedResult.lessonId} onRestart={loadExam} />
        </div>
      );
    }

    return (
      <div className="mx-auto max-w-2xl rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-8 text-center">
        <h2 className="mb-4 text-xl font-bold text-[var(--admin-danger)]">الامتحان غير متاح</h2>
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
      {!passedResult && (
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
          إلغاء الامتحان
        </button>
      )}

      <ExamViewer examId={examId} examTitle={exam.title} examDescription={exam.description} attempt={exam} packageId={packageId} lessonId={lessonId} onRestart={loadExam} />
    </div>
  );
}
