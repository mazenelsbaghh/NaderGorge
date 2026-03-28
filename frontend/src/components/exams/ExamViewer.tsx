'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { CheckCircle2, XCircle, RotateCcw, ArrowUpLeft, AlertCircle } from 'lucide-react';
import { ActiveExamAttemptDto, ExamResultDto, examService, AnswerSubmissionDto } from '@/services/exam-service';
import { ExamTimer } from '@/components/student/ExamTimer';

export function ExamViewer({ examId, examTitle, examDescription, attempt, packageId }: { examId: string; examTitle: string; examDescription: string; attempt: ActiveExamAttemptDto; packageId?: string }) {
  const router = useRouter();
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ExamResultDto | null>(null);
  const [error, setError] = useState('');
  
  // Wizard State
  const [currentIdx, setCurrentIdx] = useState(0);
  const [timeSpent, setTimeSpent] = useState<Record<string, number>>({});

  const allAnswered = Object.keys(answers).length === attempt.questions.length;

  const handleSubmit = async (isTimeout = false) => {
    // If it's a timeout, submit whatever we have. Otherwise require all answers.
    if (!isTimeout && !allAnswered) return;
    setLoading(true);
    setError('');

    const submissions: AnswerSubmissionDto[] = Object.keys(answers).map(qId => ({
      examQuestionId: qId,
      selectedOptionId: answers[qId]
    }));

    try {
      const res = await examService.submitExam(examId, attempt.attemptId, submissions);
      setResult(res.data.data);
    } catch (err: any) {
      setError(err.response?.data?.message || 'فشل في إرسال الامتحان');
    } finally {
      setLoading(false);
    }
  };

  /* ── Question Timer Logic ── */
  useEffect(() => {
    if (result) return;
    const q = attempt.questions[currentIdx];
    if (!q || !q.durationSeconds) return;

    const interval = setInterval(() => {
      setTimeSpent(prev => {
        const current = prev[q.id] || 0;
        if (current >= q.durationSeconds!) {
          return prev; // stop
        }
        return { ...prev, [q.id]: current + 1 };
      });
    }, 1000);

    return () => clearInterval(interval);
  }, [currentIdx, result, attempt.questions]);

  useEffect(() => {
    if (result) return;
    const q = attempt.questions[currentIdx];
    if (q && q.durationSeconds) {
      const spent = timeSpent[q.id] || 0;
      if (spent >= q.durationSeconds) {
        // Auto-forward if time is up and not at the end
        if (currentIdx < attempt.questions.length - 1) {
          setCurrentIdx(idx => idx + 1);
        }
      }
    }
  }, [timeSpent, currentIdx, attempt.questions, result]);

  /* ── Result Screen ── */
  if (result) {
    return (
      <div className={`mx-auto max-w-2xl rounded-[28px] border p-8 text-center shadow-[0_28px_70px_var(--admin-shadow)] sm:p-10 ${
        result.isPassed
          ? 'border-[var(--admin-success-20)] bg-[var(--admin-success-10)]'
          : 'border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)]'
      }`}>
        <div className="mb-5 flex justify-center">
          {result.isPassed ? (
            <div className="flex h-20 w-20 items-center justify-center rounded-full bg-[var(--admin-success-10)]">
              <CheckCircle2 className="h-10 w-10 text-[var(--admin-success)]" />
            </div>
          ) : (
            <div className="flex h-20 w-20 items-center justify-center rounded-full bg-[var(--admin-danger-10)]">
              <XCircle className="h-10 w-10 text-[var(--admin-danger)]" />
            </div>
          )}
        </div>

        <h2 className="text-3xl font-black text-[var(--admin-text)]">
          {result.isPassed ? 'ناجح! 🎉' : 'لم تجتز'}
        </h2>
        
        {result.isTimeExpired && (
          <div className="mt-4 flex items-center justify-center gap-2 text-[var(--admin-warning)] bg-[var(--admin-warning-10)] px-4 py-2 rounded-xl text-sm font-bold">
            <AlertCircle className="w-5 h-5" />
            انتهى الوقت المحدد للامتحان وتم تسليمه تلقائياً
          </div>
        )}

        <p className="mt-4 text-xl font-bold text-[var(--admin-muted)]">
          الدرجة: {result.scoreAchieved} / {result.totalScore}
        </p>

        {result.blocksNextLesson && (
          <p className="mx-auto mt-6 max-w-md rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-sm font-bold leading-7 text-[var(--admin-danger)]">
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
    <div className="mx-auto max-w-3xl space-y-6 relative" role="form" aria-label={`امتحان: ${examTitle}`}>
      {attempt.durationMinutes && !result && (
        <ExamTimer 
          startedAt={attempt.startedAt} 
          durationMinutes={attempt.durationMinutes} 
          onTimeExpired={() => handleSubmit(true)} 
        />
      )}
      
      {/* Header */}
      <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 backdrop-blur-xl sm:rounded-[28px]">
        <h1 className="text-2xl font-black text-[var(--admin-text)]">{examTitle}</h1>
        <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">{examDescription}</p>
        <p className="mt-4 text-sm font-extrabold text-[var(--admin-primary)] inline-flex gap-4">
          <span>الدرجة الكلية: {attempt.totalScore} نقطة</span>
          {attempt.durationMinutes && (
            <span>الزمن: {attempt.durationMinutes} دقيقة</span>
          )}
        </p>
      </div>

      {/* Error */}
      {error && (
        <div role="alert" className="rounded-2xl border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] p-4 text-sm font-bold text-[var(--admin-danger)]">
          {error}
        </div>
      )}

      {/* Progress Wizard Header */}
      <div className="flex items-center justify-between px-2 mb-2">
        <span className="text-sm font-bold text-[var(--admin-muted)]">
          السؤال {currentIdx + 1} من {attempt.questions.length}
        </span>
        <div className="flex gap-1">
          {attempt.questions.map((_, i) => (
            <div 
              key={i} 
              className={`h-2 w-8 rounded-full transition-colors ${
                i === currentIdx ? 'bg-[var(--admin-primary)]' : 
                i < currentIdx ? 'bg-[var(--admin-primary)]/40' : 'bg-[var(--admin-border)]'
              }`}
            />
          ))}
        </div>
      </div>

      {/* Active Question */}
      <div className="space-y-5">
        {attempt.questions.map((q, idx) => {
          if (idx !== currentIdx) return null; // Only render active question
          
          const isTimeUp = q.durationSeconds ? (timeSpent[q.id] || 0) >= q.durationSeconds : false;
          const remainingSeconds = q.durationSeconds ? Math.max(0, q.durationSeconds - (timeSpent[q.id] || 0)) : null;

          return (
            <div key={q.id} className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-5 backdrop-blur-xl sm:p-6 shadow-sm animate-in fade-in slide-in-from-right-4 duration-300" role="group" aria-label={`السؤال ${idx + 1}: ${q.text}`}>
              <div className="flex justify-between items-start mb-4">
                <h3 className="text-lg font-black text-[var(--admin-text)] max-w-[80%]">
                  <span className="inline-flex h-8 w-8 items-center justify-center rounded-xl bg-[var(--admin-card-strong)] text-sm font-black text-[var(--admin-primary)] ml-3">
                    {idx + 1}
                  </span>
                  <div 
                    className="inline-block" 
                    dangerouslySetInnerHTML={{ __html: q.text }} 
                  />
                  <span className="mr-2 text-sm font-bold text-[var(--admin-muted)]">({q.points} نقطة)</span>
                </h3>
                
                {remainingSeconds !== null && (
                  <div className={`flex items-center gap-2 px-3 py-1.5 rounded-full text-sm font-bold transition-colors ${
                    remainingSeconds <= 10 ? 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)] animate-pulse' : 'bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]'
                  }`}>
                    <AlertCircle className="w-4 h-4" />
                    {Math.floor(remainingSeconds / 60)}:{(remainingSeconds % 60).toString().padStart(2, '0')}
                  </div>
                )}
              </div>

              {isTimeUp && (
                <div className="mb-4 text-xs font-bold text-[var(--admin-danger)] bg-[var(--admin-danger-10)] p-2 rounded-lg text-center">
                  نفذ الوقت المخصص لهذا السؤال وتم قفله.
                </div>
              )}

              <div className="space-y-3">
                {q.options.map(opt => (
                  <label
                    key={opt.id}
                    className={`flex items-center gap-4 rounded-2xl p-4 transition ${
                      isTimeUp ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer hover:bg-[var(--admin-card-strong)]'
                    } ${
                      answers[q.id] === opt.id
                        ? 'border border-[var(--admin-primary)] bg-[var(--admin-primary-15)] shadow-[0_0_0_3px_var(--admin-primary-15)]'
                        : 'border border-[var(--admin-border)] bg-[var(--admin-card-soft)]'
                    }`}
                  >
                    <input
                      type="radio"
                      name={`q-${q.id}`}
                      value={opt.id}
                      checked={answers[q.id] === opt.id}
                      onChange={() => !isTimeUp && setAnswers({ ...answers, [q.id]: opt.id })}
                      disabled={isTimeUp}
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
          );
        })}
      </div>

      {/* Navigation & Submit */}
      <div className="flex justify-between items-center pt-4">
        <button
          onClick={() => setCurrentIdx(prev => Math.max(0, prev - 1))}
          disabled={currentIdx === 0}
          className="inline-flex items-center justify-center gap-2 rounded-2xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)] px-6 py-3.5 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] disabled:opacity-30 disabled:hover:bg-[var(--admin-card-soft)]"
        >
          السابق
        </button>

        {currentIdx === attempt.questions.length - 1 ? (
          <button
            onClick={() => handleSubmit(false)}
            disabled={!allAnswered || loading}
            className="inline-flex items-center justify-center gap-2 rounded-2xl bg-gradient-to-l from-[var(--admin-primary)] to-[var(--admin-primary-strong)] px-8 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_10px_24px_rgba(119,90,25,0.2)] transition hover:-translate-y-0.5 disabled:opacity-50 disabled:hover:translate-y-0"
          >
            {loading ? 'جاري الإرسال...' : 'إنهاء وتسليم الامتحان'}
          </button>
        ) : (
          <button
            onClick={() => setCurrentIdx(prev => Math.min(attempt.questions.length - 1, prev + 1))}
            className="inline-flex items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] font-bold px-8 py-3.5 text-sm transition hover:bg-[var(--admin-primary)]/20"
          >
            التالي
          </button>
        )}
      </div>
    </div>
  );
}
