'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { CheckCircle2, XCircle, RotateCcw, ArrowUpLeft } from 'lucide-react';
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
      setError(err.response?.data?.message || 'فشل في إرسال الامتحان');
    } finally {
      setLoading(false);
    }
  };

  /* ── Result Screen ── */
  if (result) {
    return (
      <div className={`mx-auto max-w-2xl rounded-[28px] border p-8 text-center shadow-[0_28px_70px_var(--admin-shadow)] sm:p-10 ${
        result.isPassed
          ? 'border-[color:rgba(34,197,94,0.3)] bg-[color:rgba(34,197,94,0.06)]'
          : 'border-[color:rgba(239,68,68,0.3)] bg-[color:rgba(239,68,68,0.06)]'
      }`}>
        <div className="mb-5 flex justify-center">
          {result.isPassed ? (
            <div className="flex h-20 w-20 items-center justify-center rounded-full bg-[color:rgba(34,197,94,0.12)]">
              <CheckCircle2 className="h-10 w-10 text-[#22c55e]" />
            </div>
          ) : (
            <div className="flex h-20 w-20 items-center justify-center rounded-full bg-[color:rgba(239,68,68,0.12)]">
              <XCircle className="h-10 w-10 text-[#ef4444]" />
            </div>
          )}
        </div>

        <h2 className="text-3xl font-black text-[var(--admin-text)]">
          {result.isPassed ? 'ناجح! 🎉' : 'لم تجتز'}
        </h2>
        <p className="mt-3 text-xl font-bold text-[var(--admin-muted)]">
          الدرجة: {result.scoreAchieved} / {result.totalScore}
        </p>

        {result.blocksNextLesson && (
          <p className="mx-auto mt-6 max-w-md rounded-2xl border border-[color:rgba(239,68,68,0.2)] bg-[color:rgba(239,68,68,0.06)] p-4 text-sm font-bold leading-7 text-[#ef4444]">
            يجب اجتياز هذا الامتحان لفتح الدرس التالي. يمكنك إعادة الامتحان أو طلب فتحه من المُعلم.
          </p>
        )}

        <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
          <button
            onClick={() => {
              setResult(null);
              setAnswers({});
              setError('');
            }}
            className="inline-flex items-center justify-center gap-2 rounded-2xl bg-[var(--admin-card-strong)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
          >
            <RotateCcw className="h-4 w-4" />
            إعادة الامتحان
          </button>
          <button
            onClick={() => router.push(packageId ? `/student/packages/${packageId}` : '/student')}
            className="inline-flex items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_10px_24px_rgba(119,90,25,0.2)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)]"
          >
            العودة للمحتوى
            <ArrowUpLeft className="h-4 w-4" />
          </button>
        </div>
      </div>
    );
  }

  /* ── Exam Questions ── */
  return (
    <div className="mx-auto max-w-3xl space-y-6" role="form" aria-label={`امتحان: ${exam.title}`}>
      {/* Header */}
      <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 backdrop-blur-xl sm:rounded-[28px]">
        <h1 className="text-2xl font-black text-[var(--admin-text)]">{exam.title}</h1>
        <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">{exam.description}</p>
        <p className="mt-4 text-sm font-extrabold text-[var(--admin-primary)]">
          الدرجة الكلية: {exam.totalScore} نقطة
        </p>
      </div>

      {/* Error */}
      {error && (
        <div role="alert" className="rounded-2xl border border-[color:rgba(239,68,68,0.2)] bg-[color:rgba(239,68,68,0.06)] p-4 text-sm font-bold text-[#ef4444]">
          {error}
        </div>
      )}

      {/* Questions */}
      <div className="space-y-5">
        {exam.questions.map((q, idx) => (
          <div key={q.id} className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 backdrop-blur-xl sm:p-6" role="group" aria-label={`السؤال ${idx + 1}: ${q.text}`}>
            <h3 className="mb-4 text-lg font-black text-[var(--admin-text)]">
              <span className="inline-flex h-8 w-8 items-center justify-center rounded-xl bg-[var(--admin-card-strong)] text-sm font-black text-[var(--admin-primary)] ml-3">
                {idx + 1}
              </span>
              {q.text}
              <span className="mr-2 text-sm font-bold text-[var(--admin-muted)]">({q.points} نقطة)</span>
            </h3>
            <div className="space-y-3">
              {q.options.map(opt => (
                <label
                  key={opt.id}
                  className={`flex cursor-pointer items-center gap-4 rounded-2xl p-4 transition ${
                    answers[q.id] === opt.id
                      ? 'border border-[var(--admin-primary)] bg-[color:rgba(154,105,51,0.08)] shadow-[0_0_0_2px_color-mix(in_srgb,var(--admin-primary)_20%,transparent)]'
                      : 'border border-[var(--admin-border)] bg-[var(--admin-card-soft)] hover:bg-[var(--admin-card-strong)]'
                  }`}
                >
                  <input
                    type="radio"
                    name={`q-${q.id}`}
                    value={opt.id}
                    checked={answers[q.id] === opt.id}
                    onChange={() => setAnswers({ ...answers, [q.id]: opt.id })}
                    className="sr-only"
                  />
                  <span className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full border-2 transition ${
                    answers[q.id] === opt.id
                      ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]'
                      : 'border-[var(--admin-border)]'
                  }`}>
                    {answers[q.id] === opt.id && (
                      <span className="h-2 w-2 rounded-full bg-[var(--admin-primary-contrast)]" />
                    )}
                  </span>
                  <span className="text-sm font-bold text-[var(--admin-text)]">{opt.text}</span>
                </label>
              ))}
            </div>
          </div>
        ))}
      </div>

      {/* Submit */}
      <div className="flex justify-end pt-4">
        <button
          onClick={handleSubmit}
          disabled={!allAnswered || loading}
          className="inline-flex items-center justify-center gap-2 rounded-2xl bg-gradient-to-l from-[var(--admin-primary)] to-[var(--admin-primary-strong)] px-8 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_10px_24px_rgba(119,90,25,0.2)] transition hover:-translate-y-0.5 disabled:opacity-50 disabled:hover:translate-y-0"
        >
          {loading ? 'جاري الإرسال...' : 'تسليم الإجابات'}
        </button>
      </div>
    </div>
  );
}
