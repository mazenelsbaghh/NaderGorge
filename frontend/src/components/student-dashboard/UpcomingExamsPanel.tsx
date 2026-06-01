import { AlarmClock, ArrowUpLeft, FilePenLine } from "lucide-react";

import type { UpcomingExamDto } from "@/services/student-service";

type UpcomingExamsPanelProps = {
  exams: UpcomingExamDto[];
  onStartExam: (examId: string) => void;
};

export function UpcomingExamsPanel({
  exams,
  onStartExam,
}: UpcomingExamsPanelProps) {
  return (
    <aside className="rounded-[32px] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-6 backdrop-blur-xl">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
            الامتحانات القادمة
          </p>
          <h2 className="mt-2 text-2xl font-black text-[var(--admin-text)]">
            ما ينتظرك قريبًا
          </h2>
        </div>
        <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
          <AlarmClock className="h-6 w-6" />
        </div>
      </div>

      {exams.length === 0 ? (
        <div className="mt-8 rounded-[24px] bg-[var(--admin-card-soft)] p-6 text-center">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
            <FilePenLine className="h-6 w-6" />
          </div>
          <p className="mt-4 text-lg font-black text-[var(--admin-text)]">
            لا يوجد امتحان متاح الآن
          </p>
          <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">
            عندما يصبح هناك امتحان جاهز، سيظهر هنا لتبدأه مباشرة بدون بحث.
          </p>
        </div>
      ) : (
        <div className="mt-6 space-y-4">
          {exams.map((exam, index) => (
            <article
              key={exam.examId}
              className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-5"
            >
              <div className="flex items-start justify-between gap-4">
                <div>
                  <span className="inline-flex rounded-full bg-[var(--admin-card-strong)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
                    أولوية {index + 1}
                  </span>
                  <h3 className="mt-3 text-lg font-black text-[var(--admin-text)]">
                    {exam.examTitle}
                  </h3>
                  <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">
                    مرتبط بدرس {exam.lessonTitle}
                  </p>
                </div>
              </div>

              <button
                type="button"
                onClick={() => onStartExam(exam.examId)}
                className="mt-5 inline-flex items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-5 py-3 text-sm font-extrabold text-[var(--admin-primary-contrast)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
              >
                ابدأ الحل الآن
                <ArrowUpLeft className="h-4 w-4" />
              </button>
            </article>
          ))}
        </div>
      )}
    </aside>
  );
}
