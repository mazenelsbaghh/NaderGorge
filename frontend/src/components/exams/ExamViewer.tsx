'use client';

import { useState, useEffect, useCallback, useId } from 'react';
import { useRouter } from 'next/navigation';
import { motion, AnimatePresence } from 'framer-motion';
import {
  CheckCircle2,
  XCircle,
  RotateCcw,
  ArrowRight,
  AlertTriangle,
  ShieldCheck,
  ShieldX,
  ChevronRight,
  ChevronLeft,
  Lightbulb,
  SkipForward,
  Send,
  BookOpen,
  RefreshCw,
  Split,
} from 'lucide-react';
import {
  ActiveExamAttemptDto,
  ExamAttemptGradingStatusDto,
  ExamResultDto,
  examService,
  AnswerSubmissionDto,
} from '@/services/exam-service';
import { CountdownTimer } from '@/components/exams/CountdownTimer';
import { shuffleArray } from '@/lib/utils';
import { sanitizeRichHtml } from '@/lib/sanitize-html';
import { useLessonFocusStore } from '@/stores/lesson-focus-store';
import { FindTheMistakeInteract } from '@/components/exams/FindTheMistakeInteract';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';

// ─── Result Panel ───────────────────────────────────────────────────────────────

export function ExamResultPanel({
  result,
  packageId,
  lessonId,
  onRestart,
}: {
  result: ExamResultDto;
  packageId?: string;
  lessonId?: string;
  onRestart?: () => Promise<void> | void;
}) {
  const router = useRouter();
  const reviewedQuestions = result.questions ?? [];
  const resolvedPackageId = packageId ?? result.packageId;
  const resolvedLessonId = lessonId ?? result.lessonId;
  const wrongQuestions = reviewedQuestions.filter((q) => q.isAnswered && !q.isCorrect);
  const answeredCount = reviewedQuestions.filter((q) => q.isAnswered).length;
  const accuracy =
    result.totalScore > 0 ? Math.round((result.scoreAchieved / result.totalScore) * 100) : 0;
  const hasReviewData = reviewedQuestions.length > 0;
  const [gradingStatus, setGradingStatus] = useState<ExamAttemptGradingStatusDto | null>(null);
  const [gradingError, setGradingError] = useState('');

  useEffect(() => {
    if (result.resultState === 'Completed') {
      setGradingStatus(null);
      setGradingError('');
      return;
    }
    let isCancelled = false;
    let timeoutId: ReturnType<typeof setTimeout> | null = null;
    const loadGradingStatus = async () => {
      try {
        const response = await examService.getGradingStatus(result.attemptId);
        if (isCancelled) return;
        setGradingStatus(response.data.data);
        setGradingError('');
        if (response.data.data.resultState !== 'Completed') {
          timeoutId = setTimeout(() => { void loadGradingStatus(); }, 5000);
        }
      } catch {
        if (isCancelled) return;
        setGradingError('تعذر تحديث حالة تصحيح الأسئلة المقالية الآن.');
        timeoutId = setTimeout(() => { void loadGradingStatus(); }, 10000);
      }
    };
    void loadGradingStatus();
    return () => {
      isCancelled = true;
      if (timeoutId) clearTimeout(timeoutId);
    };
  }, [result.attemptId, result.resultState]);

  const effectiveResultState = gradingStatus?.resultState ?? result.resultState;
  const essayStatusLabels: Record<string, string> = {
    WaitAI: 'بانتظار الذكاء الاصطناعي',
    AIScored: 'تم التقييم المبدئي',
    WaitTeacher: 'بانتظار المعلم',
    TeacherGraded: 'اكتمل التصحيح',
  };

  return (
    <div className="space-y-6 pb-16" dir="rtl">
      {/* ─── Score Hero ─── */}
      <motion.div
        initial={{ opacity: 0, y: 24 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5, ease: [0.16, 1, 0.3, 1] }}
        className={`relative overflow-hidden rounded-3xl border p-8 sm:p-10 ${
          result.isPassed
            ? 'bg-emerald-50 border-emerald-200 dark:bg-emerald-950/30 dark:border-emerald-800/40'
            : 'bg-destructive/5 border-destructive/20'
        }`}
      >
        {/* Decorative circle */}
        <div
          className={`absolute -left-10 -top-10 h-48 w-48 rounded-full opacity-20 ${
            result.isPassed ? 'bg-emerald-400' : 'bg-destructive'
          }`}
          style={{ filter: 'blur(40px)' }}
        />

        <div className="relative z-10 flex flex-col gap-6 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-3">
            <div
              className={`inline-flex items-center gap-2 rounded-full px-4 py-1.5 text-xs font-black tracking-widest uppercase ${
                result.isPassed
                  ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-400'
                  : 'bg-destructive/10 text-destructive'
              }`}
            >
              {result.isPassed ? (
                <ShieldCheck className="h-3.5 w-3.5" />
              ) : (
                <ShieldX className="h-3.5 w-3.5" />
              )}
              {result.isPassed ? 'اجتزت الاختبار' : 'محاولة تحتاج مراجعة'}
            </div>

            <h2 className="text-4xl font-black text-foreground sm:text-5xl">
              {result.isPassed ? 'ممتاز!' : 'حاول مرة أخرى'}
            </h2>

            <p className="max-w-lg text-sm leading-relaxed text-muted-foreground">
              {result.isPassed
                ? 'أجدت في هذا الامتحان. راجع ورقتك بالتفاصيل واعرف نقاط قوتك.'
                : 'إجاباتك وأماكن الخطأ ظاهرة أدناه مع الإجابات الصحيحة.'}
            </p>
          </div>

          <div
            className={`flex h-24 w-24 shrink-0 flex-col items-center justify-center rounded-2xl ${
              result.isPassed
                ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-400'
                : 'bg-destructive/10 text-destructive'
            }`}
          >
            {result.isPassed ? (
              <CheckCircle2 className="h-10 w-10" />
            ) : (
              <XCircle className="h-10 w-10" />
            )}
            <span className="mt-1 text-xs font-black">{accuracy}%</span>
          </div>
        </div>

        {/* Stats row */}
        <div className="relative z-10 mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
          {[
            { label: 'الدرجة', value: `${result.scoreAchieved} / ${result.totalScore}` },
            { label: 'الدقة', value: `${accuracy}%` },
            { label: 'التقييم', value: result.evaluation },
            { label: 'الإجابات', value: hasReviewData ? `${answeredCount}/${reviewedQuestions.length}` : '—' },
          ].map((stat) => (
            <div
              key={stat.label}
              className="rounded-xl bg-background/60 px-4 py-3 backdrop-blur-sm border border-border/50"
            >
              <p className="text-[10px] font-black uppercase tracking-widest text-muted-foreground">{stat.label}</p>
              <p className="mt-0.5 text-lg font-black text-foreground">{stat.value}</p>
            </div>
          ))}
        </div>

        {/* Alerts */}
        {result.isTimeExpired && (
          <div className="relative z-10 mt-4 flex items-center gap-2 rounded-xl bg-amber-50 dark:bg-amber-950/30 px-4 py-3 text-sm font-bold text-amber-700 dark:text-amber-400">
            <AlertTriangle className="h-4 w-4 shrink-0" />
            انتهى الوقت وتم حفظ ورقتك تلقائيًا.
          </div>
        )}

        {effectiveResultState !== 'Completed' && (
          <div className="relative z-10 mt-4 flex items-center gap-2 rounded-xl bg-amber-50 dark:bg-amber-950/30 px-4 py-3 text-sm font-bold text-amber-700 dark:text-amber-400">
            <AlertTriangle className="h-4 w-4 shrink-0" />
            النتيجة غير نهائية — هناك أسئلة مقالية قيد التصحيح. يتم التحديث تلقائيًا.
          </div>
        )}

        {gradingError && effectiveResultState !== 'Completed' && (
          <div className="relative z-10 mt-4 rounded-xl bg-destructive/10 px-4 py-3 text-sm font-bold text-destructive">
            {gradingError}
          </div>
        )}

        {/* Essay status */}
        {gradingStatus && gradingStatus.essays.length > 0 && effectiveResultState !== 'Completed' && (
          <div className="relative z-10 mt-4 rounded-xl bg-background/60 p-4 backdrop-blur-sm border border-border/50">
            <p className="mb-3 text-xs font-black uppercase tracking-widest text-muted-foreground">
              حالة الأسئلة المقالية
            </p>
            <div className="space-y-2">
              {gradingStatus.essays.map((essay, idx) => (
                <div
                  key={essay.essaySubmissionId}
                  className="flex items-center justify-between rounded-lg bg-background/70 px-3 py-2 border border-border/30"
                >
                  <span className="text-sm font-bold text-foreground">سؤال مقالي {idx + 1}</span>
                  <span className="rounded-full bg-amber-100 dark:bg-amber-900/40 px-3 py-0.5 text-xs font-black text-amber-700 dark:text-amber-400">
                    {essayStatusLabels[essay.status] ?? essay.status}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* CTA buttons */}
        <div className="relative z-10 mt-6 flex flex-wrap gap-3">
          {!result.isPassed && (
            <button
              type="button"
              onClick={() => { void onRestart?.(); }}
              className="inline-flex min-h-12 items-center gap-2 rounded-2xl bg-foreground px-6 py-3 text-sm font-black text-background transition hover:opacity-90 focus-visible:ring-2 focus-visible:ring-primary"
            >
              <RotateCcw className="h-4 w-4" />
              إعادة الامتحان
            </button>
          )}
          <button
            type="button"
            onClick={() =>
              router.push(
                resolvedLessonId
                  ? `/student/lessons/${resolvedLessonId}${resolvedPackageId ? `?packageId=${resolvedPackageId}` : ''}`
                  : resolvedPackageId
                    ? `/student/packages/${resolvedPackageId}`
                    : '/student'
              )
            }
            className="inline-flex min-h-12 items-center gap-2 rounded-2xl bg-primary px-6 py-3 text-sm font-black text-primary-foreground shadow-[0_8px_24px_color-mix(in_srgb,var(--primary)_30%,transparent)] transition hover:opacity-90 hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-primary"
          >
            العودة للحصة
            <ArrowRight className="h-4 w-4" />
          </button>
        </div>
      </motion.div>

      {/* ─── Wrong answers summary ─── */}
      {wrongQuestions.length > 0 && (
        <motion.section
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.45, delay: 0.12, ease: [0.16, 1, 0.3, 1] }}
          className="rounded-3xl border border-border bg-card p-6 sm:p-8"
        >
          <h3 className="text-xl font-black text-foreground">نقاط الضعف في هذه المحاولة</h3>
          <p className="mt-1 text-sm text-muted-foreground">هذه الأسئلة كانت مواضع الخطأ — ركز عليها في المذاكرة.</p>

          <div className="mt-5 space-y-3">
            {wrongQuestions.map((q) => (
              <article
                key={`wrong-${q.examQuestionId}`}
                className="rounded-2xl border border-destructive/20 bg-destructive/5 p-5"
              >
                <div className="mb-3 flex items-center justify-between gap-4">
                  <span className="rounded-full bg-destructive/10 px-3 py-1 text-xs font-black text-destructive">
                    سؤال {q.order}
                  </span>
                  <span className="text-xs font-black text-muted-foreground">{q.pointsAwarded} نقطة</span>
                </div>
                <div
                  className="text-base font-bold leading-8 text-foreground"
                  dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.questionText) }}
                />
                <p className="mt-3 text-sm font-bold text-muted-foreground">
                  إجابتك:{' '}
                  <span className="font-black text-foreground" dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.selectedOptionText || 'لم تُجب') }} />
                </p>
                {q.correctOptionText && (
                  <p className="mt-1 text-sm font-bold text-emerald-600 dark:text-emerald-400">
                    الصحيح: <span dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.correctOptionText) }} />
                  </p>
                )}
              </article>
            ))}
          </div>
        </motion.section>
      )}

      {/* ─── Full review ─── */}
      <motion.section
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.45, delay: 0.22, ease: [0.16, 1, 0.3, 1] }}
        className="rounded-3xl border border-border bg-card p-6 sm:p-8"
      >
        <h3 className="text-xl font-black text-foreground">مراجعة الورقة كاملة</h3>
        <p className="mt-1 text-sm text-muted-foreground">كل سؤال بإجابتك وحالته النهائية.</p>

        {hasReviewData ? (
          <div className="mt-5 space-y-3">
            {reviewedQuestions.map((q) => (
              <article
                key={q.examQuestionId}
                className={`rounded-2xl border p-5 transition-colors ${
                  q.isAnswered
                    ? q.isCorrect
                      ? 'border-emerald-200/60 bg-emerald-50/40 dark:border-emerald-800/40 dark:bg-emerald-950/20'
                      : 'border-destructive/20 bg-destructive/5'
                    : 'border-border bg-muted/30'
                }`}
              >
                <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                  <span className="rounded-full bg-background/70 px-3 py-1 text-xs font-black text-muted-foreground border border-border/50">
                    سؤال {q.order}
                  </span>
                  <span
                    className={`rounded-full px-3 py-1 text-xs font-black ${
                      !q.isAnswered
                        ? 'bg-muted text-muted-foreground'
                        : q.isCorrect
                          ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-400'
                          : 'bg-destructive/10 text-destructive'
                    }`}
                  >
                    {!q.isAnswered ? 'بدون إجابة' : q.isCorrect ? 'صحيحة ✓' : 'خاطئة ✗'}
                  </span>
                </div>

                <div
                  className="text-base font-bold leading-8 text-foreground"
                  dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.questionText) }}
                />

                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                  <div className="rounded-xl bg-background/60 border border-border/40 p-4">
                    <p className="text-[10px] font-black uppercase tracking-widest text-muted-foreground">إجابتك</p>
                    <p className="mt-1.5 text-sm font-bold leading-6 text-foreground" dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.selectedOptionText || 'لم تختر إجابة.') }} />
                  </div>

                  {q.correctOptionText ? (
                    <div className="rounded-xl bg-background/60 border border-border/40 p-4">
                      <p className="text-[10px] font-black uppercase tracking-widest text-muted-foreground">
                        الإجابة الصحيحة
                      </p>
                      <p className="mt-1.5 text-sm font-bold leading-6 text-emerald-600 dark:text-emerald-400" dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.correctOptionText) }} />
                      {q.writtenCorrection && (
                        <div className="mt-3 border-t border-border/30 pt-3">
                          <p className="text-[10px] font-black uppercase tracking-widest text-muted-foreground">
                            التصحيح
                          </p>
                          <p className="mt-1.5 whitespace-pre-wrap text-sm font-bold leading-6 text-foreground">
                            {q.writtenCorrection}
                          </p>
                        </div>
                      )}
                      {q.audioUrl && (
                        <div className="mt-3 border-t border-border/30 pt-3">
                          <p className="mb-2 text-[10px] font-black uppercase tracking-widest text-muted-foreground">
                            تصحيح صوتي
                          </p>
                          <audio controls className="h-9 w-full" preload="none">
                            <source src={q.audioUrl} type="audio/mpeg" />
                          </audio>
                        </div>
                      )}
                    </div>
                  ) : (
                    <div className="rounded-xl bg-muted/30 border border-border/30 p-4">
                      <p className="text-[10px] font-black uppercase tracking-widest text-muted-foreground">
                        ملاحظات
                      </p>
                      {q.writtenCorrection ? (
                        <p className="mt-1.5 whitespace-pre-wrap text-sm font-bold leading-6 text-foreground">
                          {q.writtenCorrection}
                        </p>
                      ) : (
                        <p className="mt-1.5 text-sm font-bold leading-6 text-muted-foreground">
                          لا توجد ملاحظات.
                        </p>
                      )}
                    </div>
                  )}
                </div>
              </article>
            ))}
          </div>
        ) : (
          <div className="mt-5 rounded-2xl border border-dashed border-border bg-muted/30 px-5 py-10 text-center">
            <BookOpen className="mx-auto mb-3 h-8 w-8 text-primary/40" />
            <p className="text-sm font-bold text-muted-foreground">لا توجد تفاصيل أسئلة متاحة لهذه النتيجة بعد.</p>
          </div>
        )}
      </motion.section>
    </div>
  );
}

// ─── Question Card ──────────────────────────────────────────────────────────────

function QuestionCard({
  q,
  qIndex,
  totalCount,
  answers,
  skipped,
  hiddenOptions,
  hasUsedFiftyFifty,
  hasUsedHint,
  revealedHintId,
  examId,
  attemptId,
  loading,
  onAnswer,
  onSkip,
  onUseFiftyFifty,
  onUseHint,
  hasUsedSwap,
  onUseSwap,
}: {
  q: ActiveExamAttemptDto['questions'][number];
  qIndex: number;
  totalCount: number;
  answers: Record<string, string>;
  skipped: Set<string>;
  hiddenOptions: Set<string>;
  hasUsedFiftyFifty: boolean;
  hasUsedHint: boolean;
  revealedHintId: string | null;
  examId: string;
  attemptId: string;
  loading: boolean;
  onAnswer: (qId: string, value: string) => void;
  onSkip: () => void;
  onUseFiftyFifty: (hiddenIds: string[]) => void;
  onUseHint: (qId: string) => void;
  hasUsedSwap: boolean;
  onUseSwap: (qId: string) => void;
}) {
  const isSkipped = skipped.has(q.id);
  const hasAnswer = !!answers[q.id];
  const hintRevealed = revealedHintId === q.id;
  const essayAnswerId = useId();

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
              {q.points} {q.points === 1 ? 'نقطة' : 'نقاط'}
            </span>
            {hasAnswer && (
              <span className="rounded-full bg-emerald-100 dark:bg-emerald-900/40 px-3 py-1 text-xs font-black text-emerald-700 dark:text-emerald-400">
                تمت الإجابة ✓
              </span>
            )}
            {isSkipped && !hasAnswer && (
              <span className="rounded-full bg-amber-100 dark:bg-amber-900/40 px-3 py-1 text-xs font-black text-amber-700 dark:text-amber-400">
                متخطى
              </span>
            )}
          </div>
          <div
            className="text-xl font-black leading-8 text-foreground sm:text-2xl"
            dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.text) }}
          />
        </div>
      </div>

      {/* Lifeline toolbar */}
      <div className="flex flex-wrap items-center gap-2">
        {q.type !== 'Essay' && q.type !== 'FindTheMistake' && (q.options?.length ?? 0) > 2 && (
          <button
            type="button"
            disabled={hasUsedFiftyFifty || loading}
            onClick={async () => {
              if (hasUsedFiftyFifty) return;
              try {
                const res = await examService.useFiftyFifty(examId, attemptId, q.id);
                if (res.data.data) onUseFiftyFifty(res.data.data);
              } catch { /* ignore */ }
            }}
            className={`inline-flex min-h-11 items-center gap-1.5 rounded-xl border px-3.5 py-2 text-sm font-bold transition-all ${
              hasUsedFiftyFifty
                ? 'cursor-not-allowed border-border bg-muted/50 text-muted-foreground/50'
                : 'border-amber-300/50 bg-amber-50 dark:bg-amber-950/30 text-amber-700 dark:text-amber-400 hover:bg-amber-100 dark:hover:bg-amber-900/40'
            }`}
          >
            <Split className="h-3.5 w-3.5" />
            ٥٠/٥٠
          </button>
        )}

        {q.hintText && (
          <button
            type="button"
            disabled={(hasUsedHint && !hintRevealed) || loading}
            onClick={() => { if (!hasUsedHint) onUseHint(q.id); }}
            className={`inline-flex min-h-11 items-center gap-1.5 rounded-xl border px-3.5 py-2 text-sm font-bold transition-all ${
              hasUsedHint && !hintRevealed
                ? 'cursor-not-allowed border-border bg-muted/50 text-muted-foreground/50'
                : 'border-primary/30 bg-primary/5 text-primary hover:bg-primary/10'
            }`}
          >
            <Lightbulb className="h-3.5 w-3.5" />
            تلميح
          </button>
        )}

        <button
          type="button"
          disabled={hasUsedSwap || loading || hasAnswer}
          onClick={() => { if (!hasUsedSwap) onUseSwap(q.id); }}
          className={`inline-flex min-h-11 items-center gap-1.5 rounded-xl border px-3.5 py-2 text-sm font-bold transition-all ${
            hasUsedSwap || hasAnswer
              ? 'cursor-not-allowed border-border bg-muted/50 text-muted-foreground/50'
              : 'border-blue-300/50 bg-blue-50 dark:bg-blue-950/30 text-blue-700 dark:text-blue-400 hover:bg-blue-100 dark:hover:bg-blue-900/40'
          }`}
        >
          <RefreshCw className="h-3.5 w-3.5" />
          تغيير السؤال
        </button>

        <button
          type="button"
          disabled={loading}
          onClick={onSkip}
          className="inline-flex min-h-11 items-center gap-1.5 rounded-xl border border-border bg-transparent px-3.5 py-2 text-sm font-bold text-muted-foreground transition hover:bg-muted"
        >
          <SkipForward className="h-3.5 w-3.5" />
          تخطي
        </button>
      </div>

      {/* Hint block */}
      <AnimatePresence>
        {hintRevealed && q.hintText && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="overflow-hidden"
          >
            <div className="rounded-2xl border border-primary/20 bg-primary/5 px-5 py-4 text-sm leading-7 text-foreground">
              <span className="font-black text-primary">تلميح: </span>
              {q.hintText}
            </div>
          </motion.div>
        )}
      </AnimatePresence>


      {/* ─── Answer area ─── */}
      <div className="border-t border-border/50 pt-5">
        {q.type === 'FindTheMistake' && q.baseText ? (
          <FindTheMistakeInteract
            baseText={q.baseText}
            selectedText={answers[q.id] || null}
            onSelect={(selectedText) => {
              if (!selectedText) {
                onAnswer(q.id, '');
              } else {
                onAnswer(q.id, selectedText);
              }
            }}
            disabled={false}
          />
        ) : q.type === 'Essay' ? (
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
          <div className="space-y-3">
            {q.options.map((opt, optIdx) => {
              if (hiddenOptions.has(opt.id)) return null;
              const isSelected = answers[q.id] === opt.id;
              return (
                <label
                  key={opt.id}
                  className={`group flex cursor-pointer items-start gap-4 rounded-2xl border p-5 transition-all duration-200 hover:border-primary/40 hover:bg-primary/5 ${
                    isSelected
                      ? 'border-primary bg-primary/8 shadow-[0_2px_16px_color-mix(in_srgb,var(--primary)_12%,transparent)]'
                      : 'border-border bg-background/50'
                  }`}
                >
                  <input
                    type="radio"
                    name={`q-${q.id}`}
                    value={opt.id}
                    checked={isSelected}
                    onChange={() => onAnswer(q.id, opt.id)}
                    className="sr-only"
                    aria-label={opt.text}
                  />
                  {/* Option letter indicator */}
                  <span
                    className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-xl text-xs font-black transition-colors duration-200 mt-[-2px] ${
                      isSelected
                        ? 'bg-primary text-primary-foreground'
                        : 'bg-muted text-muted-foreground group-hover:bg-primary/15 group-hover:text-primary'
                    }`}
                  >
                    {String.fromCharCode(0x0627 + optIdx) /* أ ب ج د */}
                  </span>
                  <span className="flex-1 text-base font-bold leading-7 text-foreground" dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(opt.text) }} />
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

// ─── Main Exam Viewer ───────────────────────────────────────────────────────────

export function ExamViewer({
  examId,
  examTitle,
  examDescription,
  attempt,
  packageId,
  onRestart,
}: {
  examId: string;
  examTitle: string;
  examDescription: string;
  attempt: ActiveExamAttemptDto;
  packageId?: string;
  onRestart?: () => Promise<void> | void;
}) {
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [skipped, setSkipped] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<ExamResultDto | null>(null);
  const [error, setError] = useState('');
  const [currentIdx, setCurrentIdx] = useState(0);
  const [direction, setDirection] = useState(1);
  // Lifelines
  const [hasUsedFiftyFifty, setHasUsedFiftyFifty] = useState(false);
  const [hasUsedHint, setHasUsedHint] = useState(false);
  const [hiddenOptions, setHiddenOptions] = useState<Set<string>>(new Set());
  const [revealedHintId, setRevealedHintId] = useState<string | null>(null);
  const [hasUsedSwap, setHasUsedSwap] = useState(false);

  // Confirm submit dialog state
  const [showConfirm, setShowConfirm] = useState(false);
  const [pendingMissing, setPendingMissing] = useState(0);

  const { setFocusMode } = useLessonFocusStore();
  useEffect(() => {
    setFocusMode(true);
    return () => setFocusMode(false);
  }, [setFocusMode]);

  // Restore draft answers from localStorage
  useEffect(() => {
    try {
      const saved = localStorage.getItem('exam_answers_' + attempt.attemptId);
      if (saved) {
        setAnswers(JSON.parse(saved));
      }
    } catch {
      // ignore JSON parse or localStorage errors
    }
  }, [attempt.attemptId]);

  const [shuffledQuestions, setShuffledQuestions] = useState<ActiveExamAttemptDto['questions']>([]);
  useEffect(() => {
    if (attempt.questions?.length > 0) {
      const qs = shuffleArray(attempt.questions).map((q: ActiveExamAttemptDto['questions'][number]) => ({
        ...q,
        options: shuffleArray(q.options),
      }));
      setShuffledQuestions(qs);
    }
  }, [attempt.questions]);

  const handleAnswer = useCallback((qId: string, value: string) => {
    if (!value) {
      setAnswers((prev) => {
        const next = { ...prev };
        delete next[qId];
        try {
          localStorage.setItem('exam_answers_' + attempt.attemptId, JSON.stringify(next));
        } catch { /* ignore */ }
        return next;
      });
    } else {
      setAnswers((prev) => {
        const next = { ...prev, [qId]: value };
        try {
          localStorage.setItem('exam_answers_' + attempt.attemptId, JSON.stringify(next));
        } catch { /* ignore */ }
        return next;
      });
    }
  }, [attempt.attemptId]);

  const handleSwap = async (qId: string) => {
    if (hasUsedSwap) return;
    try {
      setLoading(true);
      setError('');
      const res = await examService.swapQuestion(examId, attempt.attemptId, qId);
      if (res.data.data) {
        setHasUsedSwap(true);
        const newQuestion = { ...res.data.data, options: shuffleArray(res.data.data.options) } as ActiveExamAttemptDto['questions'][number];
        
        setShuffledQuestions(prev => {
           const next = [...prev];
           const idx = next.findIndex(x => x.id === qId);
           if (idx !== -1) next[idx] = newQuestion;
           return next;
        });

        setAnswers(prev => {
          const next = {...prev};
          delete next[qId];
          try {
            localStorage.setItem('exam_answers_' + attempt.attemptId, JSON.stringify(next));
          } catch { /* ignore */ }
          return next;
        });
        setSkipped(prev => { const next = new Set(prev); next.delete(qId); return next; });
      }
    } catch (err: unknown) {
      const apiError = err as { response?: { data?: { message?: string } } };
      setError(apiError.response?.data?.message || 'تعذر تبديل السؤال. قد لا يوجد أسئلة بديلة.');
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (isTimeout = false) => {
    if (!isTimeout) {
      const missing = attempt.questions.length - Object.keys(answers).length;
      if (missing > 0) {
        setPendingMissing(missing);
        setShowConfirm(true);
        return;
      }
    }
    setLoading(true);
    setError('');
    setShowConfirm(false);

    const submissions: AnswerSubmissionDto[] = Object.keys(answers).map((qId) => {
      const q = attempt.questions.find((x) => x.id === qId);
      if (q?.type === 'Essay') return { examQuestionId: qId, answerText: answers[qId] };
      if (q?.type === 'FindTheMistake') return { examQuestionId: qId, selectedText: answers[qId] };
      return { examQuestionId: qId, selectedOptionId: answers[qId] };
    });

    try {
      const res = await examService.submitExam(examId, attempt.attemptId, submissions);
      try {
        localStorage.removeItem('exam_answers_' + attempt.attemptId);
      } catch { /* ignore */ }
      setResult(res.data.data);
    } catch (err: unknown) {
      const apiError = err as { response?: { data?: { message?: string } } };
      setError(apiError.response?.data?.message || 'فشل في إرسال الامتحان. حاول مرة أخرى.');
    } finally {
      setLoading(false);
    }
  };

  const navigateTo = (idx: number) => {
    setDirection(idx > currentIdx ? 1 : -1);
    setCurrentIdx(idx);
  };

  if (result) {
    return (
      <ExamResultPanel
        result={result}
        packageId={packageId ?? attempt.packageId}
        lessonId={attempt.lessonId}
        onRestart={onRestart}
      />
    );
  }

  const totalQ = shuffledQuestions.length;
  const answeredCount = Object.keys(answers).length;
  const progress = totalQ > 0 ? Math.round((answeredCount / totalQ) * 100) : 0;
  const currentQ = shuffledQuestions[currentIdx];

  const slideVariants = {
    initial: (d: number) => ({ x: d > 0 ? '40%' : '-40%', opacity: 0 }),
    active: { x: 0, opacity: 1, transition: { duration: 0.38, ease: 'easeOut' as const } },
    exit: (d: number) => ({ x: d < 0 ? '40%' : '-40%', opacity: 0, transition: { duration: 0.28, ease: 'easeIn' as const } }),
  };


  return (
    <div className="relative mx-auto max-w-3xl pb-16" role="form" aria-label={`امتحان: ${examTitle}`} dir="rtl">

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

      {/* ─── Exam header ─── */}
      <div className="mb-6 rounded-3xl border border-border bg-card px-6 py-5">
        <h1 className="text-2xl font-black text-foreground">{examTitle}</h1>
        {examDescription && (
          <p className="mt-1.5 text-sm leading-7 text-muted-foreground">{examDescription}</p>
        )}
        <div className="mt-3 flex flex-wrap items-center gap-4 text-sm font-black text-primary">
          <span>الدرجة الكلية: {attempt.totalScore} نقطة</span>
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
          <span>تقدمك في الامتحان</span>
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
            <span>! متخطى</span>
            <span>○ بدون إجابة</span>
          </div>
          <div className="flex flex-wrap gap-2">
            {shuffledQuestions.map((q, idx) => {
              const isCurrent = idx === currentIdx;
              const hasAns = !!answers[q.id];
              const isSkip = skipped.has(q.id) && !hasAns;
              const stateLabel = isCurrent
                ? 'السؤال الحالي'
                : hasAns
                  ? 'تمت الإجابة'
                  : isSkip
                    ? 'متخطى'
                    : 'بدون إجابة';
              const stateMark = isCurrent ? '●' : hasAns ? '✓' : isSkip ? '!' : '○';

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
                        : isSkip
                          ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400'
                          : 'bg-muted text-muted-foreground hover:bg-muted/80'
                  }`}
                >
                  <span aria-hidden="true" className="absolute left-1 top-0.5 text-[9px] leading-none">{stateMark}</span>
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
              <QuestionCard
                q={currentQ}
                qIndex={currentIdx}
                totalCount={totalQ}
                answers={answers}
                skipped={skipped}
                hiddenOptions={hiddenOptions}
                hasUsedFiftyFifty={hasUsedFiftyFifty}
                hasUsedHint={hasUsedHint}
                revealedHintId={revealedHintId}
                examId={examId}
                attemptId={attempt.attemptId}
                loading={loading}
                onAnswer={handleAnswer}
                onSkip={() => {
                  setSkipped((prev) => new Set([...prev, currentQ.id]));
                  if (currentIdx < totalQ - 1) navigateTo(currentIdx + 1);
                }}
                onUseFiftyFifty={(hiddenIds) => {
                  setHiddenOptions((prev) => new Set([...prev, ...hiddenIds]));
                  setHasUsedFiftyFifty(true);
                }}
                onUseHint={(qId) => {
                  setHasUsedHint(true);
                  setRevealedHintId(qId);
                }}
                hasUsedSwap={hasUsedSwap}
                onUseSwap={handleSwap}
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
                تسليم وإنهاء
                <Send className="h-4 w-4" />
              </>
            )}
          </button>
        )}
      </div>

      {/* ─── Confirm submit — uses shared ConfirmDialog ─── */}
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
