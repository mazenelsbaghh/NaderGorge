import { AlarmClock, ArrowUpLeft, ChevronDown, FilePenLine } from "lucide-react";

import type { UpcomingExamDto } from "@/services/student-service";

type UpcomingExamsPanelProps = {
  exams: UpcomingExamDto[];
  onStartExam: (examId: string) => void;
};

export function UpcomingExamsPanel({
  exams,
  onStartExam,
}: UpcomingExamsPanelProps) {
  const [nextExam, ...laterExams] = exams;

  return (
    <aside className="h-full rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-xs font-bold text-[var(--admin-primary)]">التالي بعد المذاكرة</p>
          <h2 className="mt-1 text-xl font-black text-[var(--admin-text)]">
            أقرب امتحان
          </h2>
        </div>
        <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-[var(--admin-card-soft)] text-[var(--admin-primary)]">
          <AlarmClock className="h-5 w-5" />
        </div>
      </div>

      {!nextExam ? (
        <div className="mt-5 flex items-start gap-3 border-t border-[var(--admin-border)] pt-5">
          <FilePenLine className="mt-0.5 h-5 w-5 shrink-0 text-[var(--admin-primary)]" />
          <div>
            <p className="font-black text-[var(--admin-text)]">لا يوجد امتحان متاح الآن</p>
            <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
              سيظهر أقرب امتحان هنا فور إتاحته.
            </p>
          </div>
        </div>
      ) : (
        <div className="mt-5 border-t border-[var(--admin-border)] pt-5">
          <article>
            <span className="inline-flex rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
              جاهز الآن
            </span>
            <h3 className="mt-3 text-lg font-black leading-tight text-[var(--admin-text)]">
              {nextExam.examTitle}
            </h3>
            <p className="mt-2 text-sm leading-6 text-[var(--admin-muted)]">
              مرتبط بدرس {nextExam.lessonTitle}
            </p>
            <button
              type="button"
              onClick={() => onStartExam(nextExam.examId)}
              className="mt-4 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[var(--admin-card-soft)] px-4 text-sm font-black text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
            >
              ابدأ الامتحان
              <ArrowUpLeft className="h-4 w-4" />
            </button>
          </article>

          {laterExams.length > 0 && (
            <details className="group mt-4 border-t border-[var(--admin-border)] pt-2">
              <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-3 rounded-lg px-2 text-sm font-bold text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]">
                <span>{laterExams.length} امتحانات أخرى</span>
                <ChevronDown className="h-4 w-4 transition-transform group-open:rotate-180" />
              </summary>
              <div className="space-y-2 pt-2">
                {laterExams.map((exam) => (
                  <button
                    key={exam.examId}
                    type="button"
                    onClick={() => onStartExam(exam.examId)}
                    className="flex min-h-11 w-full items-center justify-between gap-3 rounded-xl bg-[var(--admin-card-soft)] px-3 py-2 text-right transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-sm font-bold text-[var(--admin-text)]">
                        {exam.examTitle}
                      </span>
                      <span className="mt-0.5 block truncate text-xs text-[var(--admin-muted)]">
                        {exam.lessonTitle}
                      </span>
                    </span>
                    <ArrowUpLeft className="h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
                  </button>
                ))}
              </div>
            </details>
          )}
        </div>
      )}
    </aside>
  );
}
