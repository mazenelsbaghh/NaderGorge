'use client';

import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import {
  CheckCircle2,
  XCircle,
  ArrowRight,
  ShieldCheck,
  ShieldX,
  Award,
  BookOpen,
  RotateCcw,
} from 'lucide-react';
import type { HomeworkResultDto } from '@/services/homework-service';
import { sanitizeRichHtml } from '@/lib/sanitize-html';

export function HomeworkResultPanel({
  result,
  packageId,
  lessonId,
  onRestart,
}: {
  result: HomeworkResultDto;
  packageId?: string;
  lessonId?: string;
  onRestart?: () => Promise<void> | void;
}) {
  const router = useRouter();
  const reviewedQuestions = result.questionReviews ?? [];
  const wrongQuestions = reviewedQuestions.filter((q) => q.isCorrect === false);
  const accuracy =
    result.totalScore > 0 ? Math.round((result.score / result.totalScore) * 100) : 0;
  const hasReviewData = reviewedQuestions.length > 0;

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
              {result.isPassed ? 'اجتزت الواجب' : 'لم تجتز الواجب'}
            </div>

            <h2 className="text-4xl font-black text-foreground sm:text-5xl">
              {result.isPassed ? 'أحسنت!' : 'حاول مرة أخرى'}
            </h2>

            <p className="max-w-lg text-sm leading-relaxed text-muted-foreground">
              {result.isPassed
                ? 'أجدت في هذا الواجب. راجع إجاباتك بالتفصيل أدناه.'
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
            { label: 'الدرجة', value: `${result.score} / ${result.totalScore}` },
            { label: 'إجابات صحيحة', value: `${result.correctAnswers}` },
            { label: 'إجابات خاطئة', value: `${result.wrongAnswers}` },
            { label: 'التقييم', value: result.evaluation || '—' },
          ].map((stat) => (
            <div
              key={stat.label}
              className="rounded-xl bg-background/60 px-4 py-3 backdrop-blur-sm border border-border/50"
            >
              <p className="text-xs font-black uppercase tracking-widest text-muted-foreground">{stat.label}</p>
              <p className="mt-0.5 text-lg font-black text-foreground">{stat.value}</p>
            </div>
          ))}
        </div>

        {/* CTA button */}
        <div className="relative z-10 mt-6 flex flex-wrap gap-3">
          {!result.isPassed && onRestart && (
            <button
              type="button"
              onClick={() => { void onRestart(); }}
              className="inline-flex min-h-12 items-center gap-2 rounded-2xl bg-foreground px-6 py-3 text-sm font-black text-background transition hover:opacity-90 focus-visible:ring-2 focus-visible:ring-primary"
            >
              <RotateCcw className="h-4 w-4" />
              إعادة حل الواجب
            </button>
          )}
          <button
            type="button"
            onClick={() =>
              router.push(
                lessonId && packageId
                  ? `/student/packages/${packageId}/lessons/${lessonId}`
                  : packageId
                    ? `/student/packages/${packageId}`
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
          <h3 className="text-xl font-black text-foreground">نقاط الضعف</h3>
          <p className="mt-1 text-sm text-muted-foreground">هذه الأسئلة كانت مواضع الخطأ — ركز عليها في المذاكرة.</p>

          <div className="mt-5 space-y-3">
            {wrongQuestions.map((q) => (
              <article
                key={`wrong-${q.questionId}`}
                className="rounded-2xl border border-destructive/20 bg-destructive/5 p-5"
              >
                <div className="mb-3 flex items-center justify-between gap-4">
                  <span className="rounded-full bg-destructive/10 px-3 py-1 text-xs font-black text-destructive">
                    سؤال {q.order}
                  </span>
                  <span className="text-xs font-black text-muted-foreground">{q.scoreReceived ?? 0} / {q.maxPoints} نقطة</span>
                </div>
                <div
                  className="text-base font-bold leading-8 text-foreground"
                  dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.text) }}
                />
                <p className="mt-3 text-sm font-bold text-muted-foreground">
                  إجابتك:{' '}
                  <span className="font-black text-foreground" dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.providedAnswer || 'لم تُجب') }} />
                </p>
                {q.correctAnswer && (
                  <p className="mt-1 text-sm font-bold text-emerald-600 dark:text-emerald-400">
                    الصحيح: <span dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.correctAnswer) }} />
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
        <h3 className="text-xl font-black text-foreground">مراجعة الإجابات كاملة</h3>
        <p className="mt-1 text-sm text-muted-foreground">كل سؤال بإجابتك وحالته النهائية.</p>

        {hasReviewData ? (
          <div className="mt-5 space-y-3">
            {reviewedQuestions.map((q) => (
              <article
                key={q.questionId}
                className={`rounded-2xl border p-5 transition-colors ${
                  q.isCorrect
                    ? 'border-emerald-200/60 bg-emerald-50/40 dark:border-emerald-800/40 dark:bg-emerald-950/20'
                    : q.isCorrect === false
                      ? 'border-destructive/20 bg-destructive/5'
                      : 'border-amber-200/60 bg-amber-50/40 dark:border-amber-800/40 dark:bg-amber-950/20'
                }`}
              >
                <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
                  <span className="rounded-full bg-background/70 px-3 py-1 text-xs font-black text-muted-foreground border border-border/50">
                    سؤال {q.order}
                  </span>
                  <span
                    className={`rounded-full px-3 py-1 text-xs font-black ${
                      q.isCorrect
                        ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-400'
                        : q.isCorrect === false
                          ? 'bg-destructive/10 text-destructive'
                          : 'bg-amber-100 text-amber-700 dark:bg-amber-900/50 dark:text-amber-400'
                    }`}
                  >
                    {q.isCorrect ? 'صحيحة ✓' : q.isCorrect === false ? 'خاطئة ✗' : 'قيد التصحيح'}
                  </span>
                </div>

                <div
                  className="text-base font-bold leading-8 text-foreground"
                  dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.text) }}
                />

                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                  <div className="rounded-xl bg-background/60 border border-border/40 p-4">
                    <p className="text-xs font-black uppercase tracking-widest text-muted-foreground">إجابتك</p>
                    <p className="mt-1.5 text-sm font-bold leading-6 text-foreground" dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.providedAnswer || 'لم تُجب') }} />
                  </div>

                  {q.correctAnswer ? (
                    <div className="rounded-xl bg-background/60 border border-border/40 p-4">
                      <p className="text-xs font-black uppercase tracking-widest text-muted-foreground">
                        الإجابة الصحيحة
                      </p>
                      <p className="mt-1.5 text-sm font-bold leading-6 text-emerald-600 dark:text-emerald-400" dir="auto" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(q.correctAnswer) }} />
                      {q.writtenCorrection && (
                        <div className="mt-3 border-t border-border/30 pt-3">
                          <p className="text-xs font-black uppercase tracking-widest text-muted-foreground">
                            التصحيح
                          </p>
                          <p className="mt-1.5 whitespace-pre-wrap text-sm font-bold leading-6 text-foreground">
                            {q.writtenCorrection}
                          </p>
                        </div>
                      )}
                      {q.audioUrl && (
                        <div className="mt-3 border-t border-border/30 pt-3">
                          <p className="mb-2 text-xs font-black uppercase tracking-widest text-muted-foreground">
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
                      <p className="text-xs font-black uppercase tracking-widest text-muted-foreground">
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
