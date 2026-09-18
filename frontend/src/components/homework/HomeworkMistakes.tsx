"use client";

import Link from "next/link";
import { QuestionImage } from "@/components/assessment/QuestionImage";
import { sanitizeRichHtml } from "@/lib/sanitize-html";
import type { HomeworkMistakeGroupDto } from "@/services/student-service";

export function HomeworkMistakes({ groups, total }: { groups: HomeworkMistakeGroupDto[]; total: number }) {
  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 sm:p-8">
      <h2 className="text-2xl font-black text-[var(--admin-text)]">أخطاء الواجبات <span className="text-[var(--admin-muted)]">({total})</span></h2>
      <p className="mt-2 text-sm text-[var(--admin-muted)]">الأسئلة اللي نقصت فيها درجات بعد التصحيح، سواء نجحت في الواجب أو لأ.</p>
      {groups.length === 0 ? <p className="mt-6 text-[var(--admin-muted)]">مفيش أخطاء في الواجبات المصححة لسه.</p> : (
        <div className="mt-6 space-y-5">
          {groups.map(group => (
            <article key={group.submissionId} className="rounded-2xl bg-[var(--admin-card-soft)] p-5">
              <h3 className="text-xl font-black text-[var(--admin-text)]">{group.homeworkTitle}</h3>
              <p className="mt-2 text-sm text-[var(--admin-muted)]">الدرجة: {group.score} / {group.totalScore}</p>
              {group.packageId && <Link className="mt-3 inline-flex min-h-11 items-center font-bold text-[var(--admin-primary)]" href={`/student/packages/${group.packageId}/lessons/${group.lessonId}`}>راجع الواجب في الدرس</Link>}
              <div className="mt-4 space-y-3">
                {group.items.map(question => (
                  <div key={question.questionId} className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-4">
                    <p className="text-sm font-bold text-[var(--admin-danger)]">السؤال {question.order} · {question.scoreReceived} / {question.maxPoints}</p>
                    <div className="mt-3 text-sm leading-7 text-[var(--admin-text)]" dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(question.questionText) }} />
                    <QuestionImage imageUrl={question.imageUrl} alt={`صورة السؤال ${question.order}`} />
                    <p className="mt-3 text-sm text-[var(--admin-muted)]">إجابتك: {question.yourAnswer || "بدون إجابة"}</p>
                    {question.correctAnswer && <div className="mt-2 text-sm text-[var(--admin-success)]">الإجابة الصحيحة: <span dangerouslySetInnerHTML={{ __html: sanitizeRichHtml(question.correctAnswer) }} /></div>}
                  </div>
                ))}
              </div>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
