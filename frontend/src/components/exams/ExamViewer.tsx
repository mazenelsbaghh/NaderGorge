'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { ExamDto, ExamResultDto, examService, AnswerSubmissionDto } from '@/services/exam-service';

export function ExamViewer({ exam, packageId }: { exam: ExamDto; packageId?: string }) {
  const router = useRouter();
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ExamResultDto | null>(null);
  const [error, setError] = useState('');

  const allAnswered = Object.keys(answers).length === exam.questions.length;

  const handleSubmit = async () => {
    if (!allAnswered) return;
    setLoading(true);
    setError('');

    const submissions: AnswerSubmissionDto[] = Object.keys(answers).map(qId => ({
      examQuestionId: qId,
      selectedOptionId: answers[qId]
    }));

    try {
      const res = await examService.submitExam(exam.id, submissions);
      setResult(res.data.data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to submit exam');
    } finally {
      setLoading(false);
    }
  };

  if (result) {
    return (
      <div className={`mx-auto max-w-2xl rounded-3xl p-8 text-center shadow-xl ${result.isPassed ? 'bg-green-50 text-green-900 border border-green-200 dark:bg-green-900/20 dark:border-green-800 dark:text-green-100' : 'bg-red-50 text-red-900 border border-red-200 dark:bg-red-900/20 dark:border-red-800 dark:text-red-100'}`}>
        <div className="mb-4 flex justify-center">
          {result.isPassed ? (
             <svg className="h-16 w-16 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
               <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
             </svg>
          ) : (
             <svg className="h-16 w-16 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor">
               <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" />
             </svg>
          )}
        </div>
        <h2 className="text-3xl font-bold mb-2">{result.isPassed ? 'Passed!' : 'Failed'}</h2>
        <p className="text-xl mb-6">Score: {result.scoreAchieved} / {result.totalScore}</p>
        
        {result.blocksNextLesson && (
          <p className="mb-6 rounded-lg bg-red-100 p-3 text-sm text-red-700 dark:bg-red-900/40 dark:text-red-300">
             You must pass this exam to unlock the next lesson. You can retake the exam or request a teacher to unlock it.
          </p>
        )}

        <div className="flex justify-center gap-4">
          <button 
             onClick={() => window.location.reload()}
             className="rounded-xl bg-white px-6 py-2 font-semibold shadow hover:bg-gray-50 dark:bg-gray-800 dark:hover:bg-gray-700"
          >
             Retake Exam
          </button>
          <button 
             onClick={() => router.push(packageId ? `/student/packages/${packageId}` : '/student')}
             className="rounded-xl bg-indigo-600 px-6 py-2 font-semibold text-white shadow hover:bg-indigo-700"
          >
             Return to Course
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl space-y-8">
      <div className="rounded-2xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
        <h1 className="text-2xl font-bold text-gray-900 dark:text-white">{exam.title}</h1>
        <p className="mt-2 text-gray-600 dark:text-gray-400">{exam.description}</p>
        <p className="mt-4 font-semibold text-indigo-600 dark:text-indigo-400">Total Score: {exam.totalScore} Points</p>
      </div>

      {error && (
        <div className="rounded-xl bg-red-50 p-4 text-red-600 border border-red-200 dark:bg-red-900/20 dark:border-red-800 dark:text-red-400">
          {error}
        </div>
      )}

      <div className="space-y-6">
        {exam.questions.map((q, idx) => (
          <div key={q.id} className="rounded-2xl border border-gray-200 bg-white p-6 shadow-sm dark:border-gray-800 dark:bg-gray-900">
            <h3 className="text-lg font-medium text-gray-900 dark:text-gray-100 mb-4">
              <span className="text-gray-500 mr-2">{idx + 1}.</span>
              {q.text} <span className="text-sm font-normal text-gray-500">({q.points} pt)</span>
            </h3>
            <div className="space-y-3">
              {q.options.map(opt => (
                <label 
                  key={opt.id} 
                  className={`flex cursor-pointer border rounded-xl p-4 transition-all ${
                    answers[q.id] === opt.id 
                      ? 'border-indigo-500 bg-indigo-50 dark:border-indigo-400 dark:bg-indigo-900/20' 
                      : 'border-gray-200 bg-gray-50 hover:bg-gray-100 dark:border-gray-700 dark:bg-gray-800 dark:hover:bg-gray-700'
                  }`}
                >
                  <input
                    type="radio"
                    name={`q-${q.id}`}
                    value={opt.id}
                    checked={answers[q.id] === opt.id}
                    onChange={() => setAnswers({ ...answers, [q.id]: opt.id })}
                    className="mr-4 mt-1"
                  />
                  <span className="text-gray-800 dark:text-gray-200">{opt.text}</span>
                </label>
              ))}
            </div>
          </div>
        ))}
      </div>

      <div className="flex justify-end pt-4">
        <button
          onClick={handleSubmit}
          disabled={!allAnswered || loading}
          className="rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 px-8 py-3 font-semibold text-white shadow-lg disabled:opacity-50 transition-opacity"
        >
          {loading ? 'Submitting...' : 'Submit Answers'}
        </button>
      </div>
    </div>
  );
}
