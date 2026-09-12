'use client';

import { useCallback, useState } from 'react';
import { AssessmentStartConfirmation } from '@/components/assessments/AssessmentStartConfirmation';
import { useParams, useSearchParams, useRouter } from 'next/navigation';
import { examService, ActiveExamAttemptDto, ExamResultDto } from '@/services/exam-service';
import { ExamViewer, ExamResultPanel } from '@/components/exams/ExamViewer';

export default function ExamPageClient() {
  const params = useParams();
  return <ExamPageClientContent key={params.examId as string} />;
}

function ExamPageClientContent() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  
  const examId = params.examId as string;
  const packageId = searchParams.get('packageId') || undefined;
  const lessonId = searchParams.get('lessonId') || undefined;
  const fromPublicExams = searchParams.get('from') === 'public-exams';
  const resultReturnHref = fromPublicExams ? '/student/public-exams' : undefined;
  const resultReturnLabel = fromPublicExams ? 'العودة للامتحانات العامة' : undefined;
  
  const [exam, setExam] = useState<ActiveExamAttemptDto | null>(null);
  const [passedResult, setPassedResult] = useState<ExamResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [confirmation, setConfirmation] = useState<'entry' | 'restart' | null>('entry');

  const startExam = useCallback(async () => {
    if (!examId) return;
    setLoading(true);
    setError('');
    try {
      const response = await examService.startExam(examId);
      setExam(response.data.data);
      setPassedResult(null);
    } catch (err: unknown) {
      const apiError = err as { response?: { data?: { message?: string } } };
      setError(apiError.response?.data?.message || 'تعذر بدء محاولة جديدة. حاول مرة أخرى.');
      setExam(null);
      setPassedResult(null);
    } finally {
      setLoading(false);
    }
  }, [examId]);

  const loadExam = useCallback(async () => {
    if (!examId) return;
    setLoading(true);
    setError('');
    try {
      const response = await examService.getLatestResult(examId);
      setPassedResult(response.data.data);
      setExam(null);
    } catch (err: unknown) {
      const apiError = err as { response?: { status?: number; data?: { message?: string } } };
      if (apiError.response?.status === 404) {
        await startExam();
      } else {
        setError(apiError.response?.data?.message || 'تعذر تحميل نتيجة الامتحان الحالية. حاول مرة أخرى.');
        setExam(null);
        setPassedResult(null);
      }
    } finally {
      setLoading(false);
    }
  }, [examId, startExam]);

  const confirmationDialog = confirmation ? (
    <AssessmentStartConfirmation
      kind="exam"
      onConfirm={() => {
        const action = confirmation === 'restart' ? startExam : loadExam;
        setConfirmation(null);
        void action();
      }}
      onCancel={() => {
        if (confirmation === 'restart') {
          setConfirmation(null);
        } else {
          router.push(packageId && lessonId
            ? `/student/packages/${packageId}/lessons/${lessonId}`
            : fromPublicExams ? '/student/public-exams' : packageId ? `/student/packages/${packageId}` : '/student');
        }
      }}
    />
  ) : null;

  if (confirmation === 'entry') return confirmationDialog;

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
      {confirmationDialog}
          <ExamResultPanel
            result={passedResult}
            packageId={packageId}
            lessonId={passedResult.lessonId}
            onRestart={() => setConfirmation('restart')}
            onResultRefresh={setPassedResult}
            returnHref={resultReturnHref}
            returnLabel={resultReturnLabel}
          />
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
            } else if (fromPublicExams) {
              router.push('/student/public-exams');
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
      {confirmationDialog}
      {!passedResult && (
        <button 
          type="button"
          onClick={() => {
            if (packageId && lessonId) {
              router.push(`/student/packages/${packageId}/lessons/${lessonId}`);
            } else if (fromPublicExams) {
              router.push('/student/public-exams');
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

      <ExamViewer
        key={`${exam.attemptId}:${exam.revisionId ?? ""}`}
        examId={examId}
        examTitle={exam.title}
        examDescription={exam.description}
        attempt={exam}
        packageId={packageId}
        lessonId={lessonId}
        onRestart={() => setConfirmation('restart')}
        resultReturnHref={resultReturnHref}
        resultReturnLabel={resultReturnLabel}
      />
    </div>
  );
}
