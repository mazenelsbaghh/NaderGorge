'use client';

import { useState, useCallback, useId, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  CheckCircle2,
  ChevronRight,
  ChevronLeft,
  Send,
  AlertTriangle,
} from 'lucide-react';
import {
  homeworkService,
  type StartHomeworkAttemptDto,
  type StartHomeworkQuestionDto,
  type AnswerSubmissionDto,
  type HomeworkResultDto,
} from '@/services/homework-service';
import { HomeworkResultPanel } from '@/components/homework/HomeworkResultPanel';
import { FindTheMistakeInteract } from '@/components/exams/FindTheMistakeInteract';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { sanitizeRichHtml } from '@/lib/sanitize-html';
import { useLessonFocusStore } from '@/stores/lesson-focus-store';
import { CountdownTimer } from '@/components/exams/CountdownTimer';

// ─── Question Card ──────────────────────────────────────────────────────────────

function HomeworkQuestionCard({
  q,
  qIndex,
  totalCount,
  answers,
  onAnswer,
}: {
  q: StartHomeworkQuestionDto;
  qIndex: number;
  totalCount: number;
  answers: Record<string, string>;
  onAnswer: (qId: string, value: string) => void;
}) {
  const essayAnswerId = useId();
  const hasAnswer = !!answers[q.id];
  const qType = q.questionType; // 0=MCQ, 1=Essay, 2=FindTheMistake

  return (
    <div className="space-y-5" dir="rtl">
      {/* Question header */}
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1">
          <div className="mb-3 flex flex-wrap items-center gap-2">
            <span className="rounded-full bg-primary/10 px-3 py-1 text-xs font-black text-primary">
              سؤال {qIndex + 1} من {totalCount}
            </span>
            <span className="rounded-full bg-muted px-3 py-1 text-xs font-black text-muted-foreground">
              {q.maxPoints} {q.maxPoints === 1 ? 'نقطة' : 'نقاط'}
            </span>
            {hasAnswer && (
              <span className="rounded-full bg-emerald-100 dark:bg-emerald-900/40 px-3 py-1 text-xs font-black text-emerald-700 dark:text-emerald-400">
                تمت الإجابة ✓
              </span>
            )}
          </div>
          <div
            className="text-xl font-black leading-8 text-foreground sm:text-2xl"
            dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.text) }}
          />
        </div>
      </div>

      {/* ─── Answer area ─── */}
      <div className="border-t border-border/50 pt-5">
        {qType === 2 && q.baseText ? (
          <FindTheMistakeInteract
            baseText={q.baseText}
            selectedText={answers[q.id] || null}
            onSelect={(selectedText) => {
              onAnswer(q.id, selectedText || '');
            }}
            disabled={false}
          />
        ) : qType === 1 ? (
          <div>
            <label htmlFor={essayAnswerId} className="mb-2 block text-sm font-black text-foreground">
              إجابتك المقالية
            </label>
            <textarea
              id={essayAnswerId}
              className="w-full rounded-2xl border border-border bg-background/70 px-5 py-4 text-base font-bold text-foreground outline-none placeholder:text-muted-foreground/50 focus:border-primary focus:ring-2 focus:ring-primary/20 min-h-[160px] resize-none"
              placeholder="اكتب إجابتك هنا..."
              value={answers[q.id] || ''}
              onChange={(e) => onAnswer(q.id, e.target.value)}
            />
          </div>
        ) : (
          /* MCQ - qType === 0 */
          <div className="space-y-3">
            {q.possibleAnswers?.map((opt, optIdx) => {
              const isSelected = answers[q.id] === opt;
              return (
                <label
                  key={optIdx}
                  className={`group flex cursor-pointer items-start gap-4 rounded-2xl border p-5 transition-all duration-200 hover:border-primary/40 hover:bg-primary/5 ${
                    isSelected
                      ? 'border-primary bg-primary/8 shadow-[0_2px_16px_color-mix(in_srgb,var(--primary)_12%,transparent)]'
                      : 'border-border bg-background/50'
                  }`}
                >
                  <input
                    type="radio"
                    name={`q-${q.id}`}
                    value={opt}
                    checked={isSelected}
                    onChange={() => onAnswer(q.id, opt)}
                    className="sr-only"
                    aria-label={opt}
                  />
                  <span
                    className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-xl text-xs font-black transition-colors duration-200 mt-[-2px] ${
                      isSelected
                        ? 'bg-primary text-primary-foreground'
                        : 'bg-muted text-muted-foreground group-hover:bg-primary/15 group-hover:text-primary'
                    }`}
                  >
                    {String.fromCharCode(0x0627 + optIdx)}
                  </span>
                  <span
                    className="flex-1 text-base font-bold leading-7 text-foreground"
                    dir="auto"
                    dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(opt) }}
                  />
                  {isSelected && (
                    <CheckCircle2 className="h-5 w-5 shrink-0 text-primary mt-1.5" />
                  )}
                </label>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Main Homework Viewer ───────────────────────────────────────────────────────

export function HomeworkViewer({
  homeworkId,
  attempt,
  packageId,
  lessonId,
  onRestart,
}: {
  homeworkId: string;
  attempt: StartHomeworkAttemptDto;
  packageId?: string;
  lessonId?: string;
  onRestart?: () => Promise<void> | void;
}) {
  const { setFocusMode } = useLessonFocusStore();

  useEffect(() => {
    setFocusMode(true);
    return () => setFocusMode(false);
  }, [setFocusMode]);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<HomeworkResultDto | null>(null);
  const [error, setError] = useState('');
  const [currentIdx, setCurrentIdx] = useState(0);
  const [direction, setDirection] = useState(1);

  // Confirm submit dialog
  const [showConfirm, setShowConfirm] = useState(false);
  const [pendingMissing, setPendingMissing] = useState(0);

  const questions = attempt.questions;
  const totalQ = questions.length;
  const answeredCount = Object.keys(answers).length;
  const progress = totalQ > 0 ? Math.round((answeredCount / totalQ) * 100) : 0;
  const currentQ = questions[currentIdx];

  // Restore answers from localStorage on mount
  useEffect(() => {
    const key = `homework_answers_${attempt.submissionId}`;
    const saved = localStorage.getItem(key);
    if (saved) {
      try {
        setAnswers(JSON.parse(saved));
      } catch {
        // ignore
      }
    }
  }, [attempt.submissionId]);

  const handleAnswer = useCallback((qId: string, value: string) => {
    setAnswers((prev) => {
      const next = { ...prev };
      if (!value) {
        delete next[qId];
      } else {
        next[qId] = value;
      }
      const key = `homework_answers_${attempt.submissionId}`;
      localStorage.setItem(key, JSON.stringify(next));
      return next;
    });
  }, [attempt.submissionId]);

  const navigateTo = (idx: number) => {
    setDirection(idx > currentIdx ? 1 : -1);
    setCurrentIdx(idx);
  };

  const handleSubmit = async (force = false) => {
    if (!force) {
      const missing = totalQ - Object.keys(answers).length;
      if (missing > 0) {
        setPendingMissing(missing);
        setShowConfirm(true);
        return;
      }
    }
    setLoading(true);
    setError('');
    setShowConfirm(false);

    const submissions: AnswerSubmissionDto[] = Object.entries(answers).map(
      ([questionId, providedAnswer]) => ({ questionId, providedAnswer })
    );

    try {
      await homeworkService.submitHomework(homeworkId, submissions);
      
      // Clear localStorage answers and timer on success
      const key = `homework_answers_${attempt.submissionId}`;
      localStorage.removeItem(key);
      localStorage.removeItem(`homework_attempt_${homeworkId}_start_time`);

      const res = await homeworkService.getHomeworkResult(homeworkId);
      setResult(res.data.data);
    } catch (err: unknown) {
      const apiError = err as { response?: { data?: { message?: string } } };
      setError(apiError.response?.data?.message || 'فشل في إرسال الواجب. حاول مرة أخرى.');
    } finally {
      setLoading(false);
    }
  };

  if (result) {
    return (
      <HomeworkResultPanel
        result={result}
        packageId={packageId}
        lessonId={lessonId}
        onRestart={onRestart}
      />
    );
  }

  const slideVariants = {
    initial: (d: number) => ({ x: d > 0 ? '40%' : '-40%', opacity: 0 }),
    active: { x: 0, opacity: 1, transition: { duration: 0.38, ease: 'easeOut' as const } },
    exit: (d: number) => ({ x: d < 0 ? '40%' : '-40%', opacity: 0, transition: { duration: 0.28, ease: 'easeIn' as const } }),
  };

  return (
    <div className="relative mx-auto max-w-3xl pb-16" role="form" aria-label={`واجب: ${attempt.title}`} dir="rtl">

      {/* ─── Countdown ─── */}
      {attempt.durationMinutes && (
        <div className="mb-4 flex justify-start">
          <CountdownTimer
            startedAt={attempt.startedAt}
            durationMinutes={attempt.durationMinutes}
            remainingSeconds={attempt.remainingSeconds}
            onTimeExpired={() => handleSubmit(true)}
          />
        </div>
      )}

      {/* ─── Homework header ─── */}
      <div className="mb-6 rounded-3xl border border-border bg-card px-6 py-5">
        <h1 className="text-2xl font-black text-foreground">{attempt.title}</h1>
        {attempt.instructions && (
          <p className="mt-1.5 text-sm leading-7 text-muted-foreground">{attempt.instructions}</p>
        )}
        <div className="mt-3 flex flex-wrap items-center gap-4 text-sm font-black text-primary">
          <span>الدرجة الكلية: {attempt.totalScore} نقطة</span>
          {attempt.passingScore && <span>درجة النجاح: {attempt.passingScore} نقطة</span>}
          {attempt.durationMinutes && <span>الزمن: {attempt.durationMinutes} دقيقة</span>}
        </div>
      </div>

      {/* ─── Error ─── */}
      {error && (
        <div
          role="alert"
          className="mb-4 flex items-center gap-2 rounded-2xl border border-destructive/30 bg-destructive/10 px-5 py-4 text-sm font-bold text-destructive"
        >
          <AlertTriangle className="h-4 w-4 shrink-0" />
          {error}
        </div>
      )}

      {/* ─── Progress ─── */}
      <div className="mb-4 space-y-2">
        <div className="flex items-center justify-between text-xs font-black text-muted-foreground">
          <span>تقدمك في الواجب</span>
          <span>{answeredCount} من {totalQ} أسئلة</span>
        </div>
        <div
          className="h-2 rounded-full bg-muted"
          role="progressbar"
          aria-label="نسبة الأسئلة التي تمت الإجابة عنها"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={progress}
          aria-valuetext={`${answeredCount} من ${totalQ} أسئلة تمت الإجابة عنها`}
        >
          <div
            className="h-full rounded-full bg-primary transition-all duration-500"
            style={{ width: `${progress}%` }}
          />
        </div>
      </div>

      {/* ─── Question nav bubbles ─── */}
      {totalQ > 0 && (
        <div className="mb-5">
          <div className="mb-2 flex flex-wrap gap-x-4 gap-y-1 text-xs font-bold text-muted-foreground" aria-label="دليل حالات الأسئلة">
            <span>● الحالي</span>
            <span>✓ تمت الإجابة</span>
            <span>○ بدون إجابة</span>
          </div>
          <div className="flex flex-wrap gap-2">
            {questions.map((q, idx) => {
              const isCurrent = idx === currentIdx;
              const hasAns = !!answers[q.id];
              const stateLabel = isCurrent
                ? 'السؤال الحالي'
                : hasAns
                  ? 'تمت الإجابة'
                  : 'بدون إجابة';
              const stateMark = isCurrent ? '●' : hasAns ? '✓' : '○';

              return (
                <button
                  key={q.id}
                  type="button"
                  onClick={() => navigateTo(idx)}
                  disabled={loading}
                  title={`سؤال ${idx + 1}: ${stateLabel}`}
                  aria-label={`سؤال ${idx + 1}: ${stateLabel}`}
                  aria-current={isCurrent ? 'step' : undefined}
                  className={`relative flex h-11 w-11 items-center justify-center rounded-xl text-xs font-black transition-all duration-200 focus-visible:ring-2 focus-visible:ring-primary ${
                    isCurrent
                      ? 'scale-110 bg-primary text-primary-foreground shadow-[0_4px_12px_color-mix(in_srgb,var(--primary)_30%,transparent)]'
                      : hasAns
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-400'
                        : 'bg-muted text-muted-foreground hover:bg-muted/80'
                  }`}
                >
                  <span aria-hidden="true" className="absolute left-1 top-0.5 text-xs leading-none">{stateMark}</span>
                  {idx + 1}
                </button>
              );
            })}
          </div>
        </div>
      )}

      {/* ─── Question content ─── */}
      <div className="overflow-hidden rounded-3xl border border-border bg-card p-6 sm:p-8">
        <AnimatePresence mode="wait" custom={direction}>
          {currentQ && (
            <motion.div
              key={currentIdx}
              custom={direction}
              variants={slideVariants}
              initial="initial"
              animate="active"
              exit="exit"
            >
              <HomeworkQuestionCard
                q={currentQ}
                qIndex={currentIdx}
                totalCount={totalQ}
                answers={answers}
                onAnswer={handleAnswer}
              />
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* ─── Navigation footer ─── */}
      <div className="mt-4 flex items-center justify-between gap-3">
        <button
          type="button"
          onClick={() => navigateTo(currentIdx - 1)}
          disabled={currentIdx === 0 || loading}
          className="flex min-h-11 items-center gap-2 rounded-2xl border border-border bg-card px-5 py-3 text-sm font-black text-muted-foreground transition hover:bg-muted disabled:cursor-not-allowed disabled:opacity-40"
        >
          <ChevronRight className="h-4 w-4" />
          السابق
        </button>

        {currentIdx < totalQ - 1 ? (
          <button
            type="button"
            onClick={() => navigateTo(currentIdx + 1)}
            disabled={loading}
            className="flex min-h-11 items-center gap-2 rounded-2xl bg-primary px-6 py-3 text-sm font-black text-primary-foreground shadow-[0_4px_16px_color-mix(in_srgb,var(--primary)_25%,transparent)] transition hover:opacity-90 hover:-translate-y-0.5 disabled:opacity-50"
          >
            التالي
            <ChevronLeft className="h-4 w-4" />
          </button>
        ) : (
          <button
            type="button"
            onClick={() => handleSubmit(false)}
            disabled={loading}
            className="flex min-h-11 items-center gap-2 rounded-2xl bg-foreground px-7 py-3 text-sm font-black text-background shadow-[0_4px_20px_color-mix(in_srgb,var(--foreground)_25%,transparent)] transition hover:opacity-90 hover:-translate-y-0.5 disabled:opacity-60"
          >
            {loading ? (
              'جاري التسليم...'
            ) : (
              <>
                تسليم الواجب
                <Send className="h-4 w-4" />
              </>
            )}
          </button>
        )}
      </div>

      {/* ─── Confirm submit dialog ─── */}
      <ConfirmDialog
        open={showConfirm}
        variant="warning"
        title="أسئلة بدون إجابة"
        description={`هناك ${pendingMissing} ${pendingMissing === 1 ? 'سؤال لم تُجب عليه' : 'أسئلة لم تُجب عليها'} بعد. هل تريد التسليم على أي حال؟`}
        confirmLabel="نعم، سلّم الآن"
        cancelLabel="إكمال الأسئلة أولاً"
        onConfirm={() => { void handleSubmit(true); }}
        onCancel={() => setShowConfirm(false)}
      />
    </div>
  );
}
