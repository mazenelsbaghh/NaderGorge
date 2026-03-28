'use client';

import { useEffect, useState } from 'react';
import { useParams, useSearchParams, useRouter } from 'next/navigation';
import { examService, ActiveExamAttemptDto } from '@/services/exam-service';
import { ExamViewer } from '@/components/exams/ExamViewer';

export default function ExamPage() {
  const params = useParams();
  const searchParams = useSearchParams();
  const router = useRouter();
  
  const examId = params.examId as string;
  const packageId = searchParams.get('packageId') || undefined;
  
  const [exam, setExam] = useState<ActiveExamAttemptDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!examId) return;

    examService.startExam(examId)
      .then(res => setExam(res.data.data))
      .catch(err => {
        if (err.response?.status === 403) {
           setError('You do not have access to this exam, or its lesson is locked.');
        } else {
           setError('Exam not found or an error occurred loading it.');
        }
      })
      .finally(() => setLoading(false));
  }, [examId]);

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
    return (
      <div className="mx-auto max-w-2xl rounded-2xl border border-red-200 bg-red-50 p-8 text-center dark:border-red-900/30 dark:bg-red-900/10">
        <h2 className="mb-4 text-xl font-bold text-red-600 dark:text-red-400">Cannot Access Exam</h2>
        <p className="text-gray-700 dark:text-gray-300 mb-6">{error}</p>
        <button 
          onClick={() => router.push(packageId ? `/student/packages/${packageId}` : '/student')}
          className="rounded-xl bg-red-600 px-6 py-2 font-semibold text-white hover:bg-red-700 transition"
        >
          Go Back
        </button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl pb-16">
      <button 
        onClick={() => router.push(packageId ? `/student/packages/${packageId}` : '/student')}
        className="mb-8 flex items-center text-sm font-medium text-gray-500 hover:text-gray-900 dark:text-gray-400 dark:hover:text-white transition-colors"
      >
        <svg className="mr-2 h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
        </svg>
        Cancel Exam
      </button>

      <ExamViewer examId={examId} examTitle={exam.title} examDescription={exam.description} attempt={exam} packageId={packageId} />
    </div>
  );
}
