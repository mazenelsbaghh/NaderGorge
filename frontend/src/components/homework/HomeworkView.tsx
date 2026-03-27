'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { CheckCircle2, BookOpen, PenTool, ArrowUpLeft } from 'lucide-react';
import { HomeworkDto, homeworkService, AnswerSubmissionDto } from '@/services/homework-service';

export function HomeworkView({ homework, packageId }: { homework: HomeworkDto; packageId?: string }) {
  const router = useRouter();
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [error, setError] = useState('');

  const hasQuestions = homework.questions && homework.questions.length > 0;
  const allAnswered = hasQuestions ? Object.keys(answers).length === homework.questions!.length : false;

  const handleSubmit = async () => {
    setLoading(true);
    setError('');

    const submissions: AnswerSubmissionDto[] = Object.keys(answers).map(qId => ({
      questionId: qId,
      providedAnswer: answers[qId]
    }));

    try {
      await homeworkService.submitHomework(homework.id, submissions);
      setSubmitted(true);
    } catch (err: any) {
      setError(err.response?.data?.message || 'فشل في إرسال الواجب');
    } finally {
      setLoading(false);
    }
  };

  /* ── Success Screen ── */
  if (submitted) {
    return (
      <div className="mx-auto max-w-2xl rounded-[28px] border border-[color:rgba(34,197,94,0.3)] bg-[color:rgba(34,197,94,0.06)] p-8 text-center shadow-[0_28px_70px_var(--admin-shadow)] sm:p-10">
        <div className="mb-5 flex justify-center">
          <div className="flex h-20 w-20 items-center justify-center rounded-full bg-[color:rgba(34,197,94,0.12)]">
            <CheckCircle2 className="h-10 w-10 text-[#22c55e]" />
          </div>
        </div>
        <h2 className="text-3xl font-black text-[var(--admin-text)]">تم تسليم الواجب! 🎉</h2>
        <p className="mt-3 text-lg font-bold text-[var(--admin-muted)]">
          إجاباتك تحت المراجعة.
        </p>

        <div className="mt-8">
          <button
            onClick={() => router.push(packageId ? `/student/packages/${packageId}` : '/student')}
            className="inline-flex items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_10px_24px_rgba(119,90,25,0.2)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)]"
          >
            العودة للوحة التحكم
            <ArrowUpLeft className="h-4 w-4" />
          </button>
        </div>
      </div>
    );
  }

  /* ── Homework Form ── */
  return (
    <div className="mx-auto max-w-4xl space-y-6 pb-12">
      {/* Header */}
      <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 backdrop-blur-xl sm:rounded-[28px] sm:p-8">
        <div className="flex items-center gap-3 mb-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
            <BookOpen className="h-5 w-5" />
          </div>
          <span className="text-xs font-black tracking-[0.2em] text-[var(--admin-primary)]">
            واجب دراسي
          </span>
        </div>
        <h1 className="text-2xl font-black text-[var(--admin-text)] sm:text-3xl">{homework.title}</h1>
        {homework.description && (
          <p className="mt-3 text-sm leading-7 text-[var(--admin-muted)] whitespace-pre-wrap">
            {homework.description}
          </p>
        )}
      </div>

      {/* Error */}
      {error && (
        <div role="alert" className="rounded-2xl border border-[color:rgba(239,68,68,0.2)] bg-[color:rgba(239,68,68,0.06)] p-4 text-sm font-bold text-[#ef4444]">
          {error}
        </div>
      )}

      {/* Questions */}
      <div className="space-y-5">
        {homework.questions?.map((q, idx) => (
          <div key={q.id} className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] overflow-hidden backdrop-blur-xl" role="group" aria-label={`السؤال ${idx + 1}: ${q.text}`}>
            <div className="bg-[var(--admin-card-strong)] p-5">
              <h3 className="text-lg font-black text-[var(--admin-text)]">
                <span className="inline-flex h-8 w-8 items-center justify-center rounded-xl bg-[var(--admin-primary)] text-sm font-black text-[var(--admin-primary-contrast)] ml-3">
                  {idx + 1}
                </span>
                {q.text}
              </h3>
            </div>

            <div className="p-5 sm:p-6">
              {q.questionType === 0 && q.options ? (
                /* MCQ */
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
              ) : (
                /* Essay */
                <textarea
                  className="w-full min-h-[140px] rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 text-sm font-medium text-[var(--admin-text)] placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] focus:outline-none focus:ring-4 focus:ring-[color:rgba(154,105,51,0.12)] transition"
                  placeholder="اكتب إجابتك هنا..."
                  value={answers[q.id] || ''}
                  onChange={(e) => setAnswers({ ...answers, [q.id]: e.target.value })}
                />
              )}
            </div>
          </div>
        ))}

        {!hasQuestions && (
          <div className="rounded-[28px] border border-dashed border-[var(--admin-border)] bg-[var(--admin-card)] p-10 text-center backdrop-blur-xl">
            <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-3xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
              <PenTool className="h-7 w-7" />
            </div>
            <h3 className="mt-5 text-xl font-black text-[var(--admin-text)]">
              لا توجد أسئلة حالياً
            </h3>
            <p className="mx-auto mt-3 max-w-xl text-sm leading-7 text-[var(--admin-muted)]">
              لم يتم تعيين أسئلة لهذا الواجب بعد.
            </p>
          </div>
        )}
      </div>

      {/* Submit */}
      <div className="flex justify-end pt-4">
        <button
          onClick={handleSubmit}
          disabled={loading || (hasQuestions && !allAnswered)}
          className="inline-flex items-center justify-center gap-2 rounded-2xl bg-gradient-to-l from-[var(--admin-primary)] to-[var(--admin-primary-strong)] px-8 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_10px_24px_rgba(119,90,25,0.2)] transition hover:-translate-y-0.5 disabled:opacity-50 disabled:hover:translate-y-0"
        >
          {loading ? 'جاري التسليم...' : 'تسليم الواجب'}
        </button>
      </div>
    </div>
  );
}
