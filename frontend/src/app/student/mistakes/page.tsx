"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { AlertTriangle, ArrowUpLeft, BookX, Bug, ChevronLeft, ShieldCheck } from "lucide-react";

import { sanitizeRichHtml } from "@/lib/sanitize-html";
import { studentService, type StudentMistakesDto } from "@/services/student-service";

export default function StudentMistakesPage() {
  const router = useRouter();
  const [data, setData] = useState<StudentMistakesDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    studentService
      .getMistakes()
      .then(setData)
      .catch(() => setData(null))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="h-52 animate-pulse rounded-[36px] bg-[var(--admin-card-strong)]" />
        <div className="grid gap-4 lg:grid-cols-2">
          <div className="h-80 animate-pulse rounded-[32px] bg-[var(--admin-card-strong)]" />
          <div className="h-80 animate-pulse rounded-[32px] bg-[var(--admin-card-strong)]" />
        </div>
      </div>
    );
  }

  const mistakes = data ?? {
    totalExamMistakes: 0,
    examsWithMistakes: 0,
    weakHomeworkCount: 0,
    examMistakes: [],
    homeworkWeaknesses: [],
  };

  return (
    <div className="space-y-6">
      <section className="rounded-[36px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-[0_24px_60px_var(--admin-shadow)] sm:p-8">
        <div className="grid gap-6 lg:grid-cols-[1.15fr_0.85fr]">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-danger-10)] px-4 py-2 text-xs font-black tracking-[0.18em] text-[var(--admin-danger)]">
              <Bug className="h-4 w-4" />
              ملف الأخطاء
            </div>
            <h1 className="mt-5 text-3xl font-black tracking-tight text-[var(--admin-text)] sm:text-4xl">
              أخطائي ونقط ضعفي
            </h1>
            <p className="mt-3 max-w-2xl text-sm leading-7 text-[var(--admin-muted)] sm:text-base">
              هنا هتلاقي كل الأسئلة اللي غلطت فيها في الامتحانات، والواجبات اللي محتاجة شغل أكتر. الفكرة إنك تعرف إيه اللي محتاج تراجعه الأول.
            </p>
          </div>

          <dl className="grid gap-3 rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5 sm:grid-cols-3 lg:grid-cols-1">
            <div>
              <dt className="text-xs font-black tracking-[0.16em] text-[var(--admin-muted)]">أخطاء الامتحانات</dt>
              <dd className="mt-2 text-2xl font-black text-[var(--admin-text)]">{mistakes.totalExamMistakes}</dd>
            </div>
            <div>
              <dt className="text-xs font-black tracking-[0.16em] text-[var(--admin-muted)]">اختبارات بها أخطاء</dt>
              <dd className="mt-2 text-2xl font-black text-[var(--admin-text)]">{mistakes.examsWithMistakes}</dd>
            </div>
            <div>
              <dt className="text-xs font-black tracking-[0.16em] text-[var(--admin-muted)]">واجبات تحتاج متابعة</dt>
              <dd className="mt-2 text-2xl font-black text-[var(--admin-text)]">{mistakes.weakHomeworkCount}</dd>
            </div>
          </dl>
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[1.3fr_0.7fr]">
        <section className="rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 shadow-sm sm:p-8">
          <div className="flex items-center justify-between gap-4">
            <div>
              <h2 className="text-2xl font-black text-[var(--admin-text)]">أخطاء الامتحانات</h2>
              <p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">تجميعة لكل الأسئلة اللي غلطت فيها في محاولاتك المختلفة.</p>
            </div>
          </div>

          {mistakes.examMistakes.length === 0 ? (
            <EmptyState
              icon={<ShieldCheck className="h-8 w-8" />}
              title="مفيش أخطاء امتحانات لسه"
              description="لما تغلط في أسئلة الامتحانات، هتتجمع هنا عشان تراجعها بعدين."
            />
          ) : (
            <div className="mt-6 space-y-5">
              {mistakes.examMistakes.map((group) => (
                <article key={group.examId} className="rounded-[28px] bg-[var(--admin-card-soft)] p-5">
                  <div className="flex flex-col gap-4 sm:flex-row sm:flex-wrap sm:items-start sm:justify-between">
                    <div>
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="rounded-full bg-[var(--admin-card)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
                          {group.packageName}
                        </span>
                        <span className={`rounded-full px-3 py-1 text-xs font-black ${group.passedEventually ? 'bg-[var(--admin-success-10)] text-[var(--admin-success)]' : 'bg-[var(--admin-danger-10)] text-[var(--admin-danger)]'}`}>
                          {group.passedEventually ? 'عديته بعدين 👍' : 'لسه معديتوش'}
                        </span>
                      </div>
                      <h3 className="mt-3 text-xl font-black text-[var(--admin-text)]">{group.examTitle}</h3>
                      <p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">مرتبط بدرس: {group.lessonTitle}</p>
                    </div>

                    <button
                      type="button"
                      onClick={() => router.push(`/student/exams/${group.examId}${group.packageId ? `?packageId=${group.packageId}` : ""}`)}
                      className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-[18px] bg-[var(--admin-primary)] px-4 py-3 text-sm font-extrabold text-[var(--admin-primary-contrast)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] sm:w-auto sm:rounded-full sm:px-4 sm:py-2.5"
                    >
                      راجع الامتحان
                      <ArrowUpLeft className="h-4 w-4" />
                    </button>
                  </div>

                  <p className="mt-4 text-sm font-bold leading-7 text-[var(--admin-muted)]">
                    {group.mistakesCount} غلطة.
                    {" "}
                    {group.latestScore != null && group.latestTotalScore != null
                      ? `آخر نتيجة ${group.latestScore}/${group.latestTotalScore}.`
                      : "آخر نتيجة مش متاحة."}
                    {" "}
                    {group.latestEvaluation ? `آخر تقييم: ${group.latestEvaluation}.` : "آخر تقييم مش متاح."}
                  </p>

                  <div className="mt-5 space-y-3">
                    {group.items.map((item) => (
                      <div key={item.examQuestionId} className="rounded-[22px] border border-[var(--admin-border)] bg-[var(--admin-card)]/70 p-4">
                        <div className="flex flex-wrap items-center justify-between gap-3">
                          <span className="rounded-full bg-[var(--admin-danger-10)] px-3 py-1 text-xs font-black text-[var(--admin-danger)]">
                            السؤال {item.questionOrder}
                          </span>
                          <span className="text-xs font-black text-[var(--admin-muted)]">اتكرر الغلط {item.timesMissed} مرة</span>
                        </div>
                        <div
                          className="mt-3 text-sm font-black leading-7 text-[var(--admin-text)]"
                          dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(item.questionText) }}
                        />
                        <p className="mt-3 text-sm font-bold text-[var(--admin-muted)]">
                          إجابتك الأخيرة: <span className="text-[var(--admin-text)]">{item.yourAnswer || "ماختارتش إجابة"}</span>
                        </p>
                        <p className="mt-2 text-sm font-bold text-[var(--admin-muted)]">
                          {item.canRevealCorrectAnswer
                            ? <>الإجابة الصح: <span className="text-[var(--admin-success)]">{item.correctAnswer || "مش متاحة"}</span></>
                            : "الإجابة الصح هتظهر بعد ما تعدي الامتحان ده."}
                        </p>
                      </div>
                    ))}
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        <section className="rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 shadow-sm sm:p-8">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-warning-10)] text-[var(--admin-warning)]">
              <BookX className="h-6 w-6" />
            </div>
            <div>
              <h2 className="text-2xl font-black text-[var(--admin-text)]">واجبات محتاجة شغل</h2>
              <p className="text-sm font-bold text-[var(--admin-muted)]">الواجبات اللي مسقطتها أو معديتهاش.</p>
            </div>
          </div>

          {mistakes.homeworkWeaknesses.length === 0 ? (
            <EmptyState
              icon={<AlertTriangle className="h-8 w-8" />}
              title="مفيش واجبات حرجة دلوقتي"
              description="لو في واجب مسقطته أو معديتوش، هيظهر هنا ومعاه لينك تروح له على طول."
              compact
            />
          ) : (
            <div className="mt-6 space-y-4">
              {mistakes.homeworkWeaknesses.map((item) => (
                <article key={`${item.homeworkId}-${item.lessonId}`} className="rounded-[24px] bg-[var(--admin-card-soft)] p-4">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <span className="rounded-full bg-[var(--admin-warning-10)] px-3 py-1 text-xs font-black text-[var(--admin-warning)]">
                      {item.status === "Missed" ? "واجب فاتك" : "واجب معديتوش"}
                    </span>
                    <button
                      type="button"
                      onClick={() => router.push(item.packageId ? `/student/packages/${item.packageId}/lessons/${item.lessonId}` : `/student/packages`)}
                      className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm font-extrabold text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card-soft)] sm:w-auto sm:rounded-full sm:border-transparent sm:bg-transparent sm:px-4 sm:py-2.5"
                    >
                      افتح الدرس
                      <ChevronLeft className="h-4 w-4" />
                    </button>
                  </div>
                  <h3 className="mt-3 text-lg font-black text-[var(--admin-text)]">{item.homeworkTitle}</h3>
                  <p className="mt-1 text-sm font-bold text-[var(--admin-muted)]">{item.packageName} · {item.lessonTitle}</p>
                  <p className="mt-3 text-sm font-bold text-[var(--admin-text)]">
                    الدرجة: {item.score}
                    {item.passingScore != null ? <span className="text-[var(--admin-muted)]"> / حد النجاح {item.passingScore}</span> : null}
                  </p>
                  {item.assistantNotes && (
                    <p className="mt-3 rounded-[18px] bg-[var(--admin-card)]/75 p-3 text-sm font-bold leading-7 text-[var(--admin-muted)]">
                      ملاحظات المراجع: {item.assistantNotes}
                    </p>
                  )}
                </article>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

function EmptyState({
  icon,
  title,
  description,
  compact = false,
}: {
  icon: React.ReactNode;
  title: string;
  description: string;
  compact?: boolean;
}) {
  return (
    <div className={`flex flex-col items-center justify-center rounded-[28px] border-2 border-dashed border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-center ${compact ? "mt-6 px-4 py-10" : "mt-6 px-6 py-14"}`}>
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-[var(--admin-card)] text-[var(--admin-primary)]">
        {icon}
      </div>
      <h3 className="mt-4 text-xl font-black text-[var(--admin-text)]">{title}</h3>
      <p className="mt-2 max-w-lg text-sm font-bold leading-7 text-[var(--admin-muted)]">{description}</p>
    </div>
  );
}
