import { ArrowUpLeft, ClipboardCheck, FilePenLine } from "lucide-react";

import type { UpcomingHomeworkDto } from "@/services/student-service";

type UpcomingHomeworkPanelProps = {
  homeworks: UpcomingHomeworkDto[];
  onStartHomework: (homeworkId: string) => void;
};

export function UpcomingHomeworkPanel({ homeworks, onStartHomework }: UpcomingHomeworkPanelProps) {
  const [nextHomework, ...laterHomeworks] = homeworks;

  return (
    <aside className="h-full min-w-0 overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <p className="text-xs font-bold text-[var(--admin-primary)]">بعد مراجعة الدرس</p>
          <h2 className="mt-1 text-xl font-black text-[var(--admin-text)]">الواجب القادم</h2>
        </div>
        <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-[var(--admin-card-soft)] text-[var(--admin-primary)]">
          <ClipboardCheck className="h-5 w-5" />
        </div>
      </div>

      {!nextHomework ? (
        <div className="mt-5 flex items-start gap-3 border-t border-[var(--admin-border)] pt-5">
          <FilePenLine className="mt-0.5 h-5 w-5 shrink-0 text-[var(--admin-primary)]" />
          <div>
            <p className="font-black text-[var(--admin-text)]">لا يوجد واجب متاح الآن</p>
            <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">سيظهر الواجب هنا فور إتاحته.</p>
          </div>
        </div>
      ) : (
        <div className="mt-5 min-w-0 border-t border-[var(--admin-border)] pt-5">
          <article>
            <span className="inline-flex rounded-full bg-[var(--admin-card-soft)] px-3 py-1 text-xs font-black text-[var(--admin-primary)]">
              جاهز للحل
            </span>
            <h3 className="mt-3 break-words text-lg font-black leading-7 text-[var(--admin-text)]">{nextHomework.homeworkTitle}</h3>
            <p className="mt-2 break-words text-sm leading-7 text-[var(--admin-muted)]">مرتبط بدرس {nextHomework.lessonTitle}</p>
            <button
              type="button"
              onClick={() => onStartHomework(nextHomework.homeworkId)}
              className="mt-4 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-xl bg-[var(--admin-card-soft)] px-4 text-sm font-black text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
            >
              ابدأ حل الواجب
              <ArrowUpLeft className="h-4 w-4" />
            </button>
          </article>

          {laterHomeworks.length > 0 && (
            <details className="group mt-4 border-t border-[var(--admin-border)] pt-2">
              <summary className="flex min-h-11 cursor-pointer list-none items-center justify-between gap-3 rounded-lg px-2 text-sm font-bold text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]">
                <span>{laterHomeworks.length} واجبات أخرى</span>
              </summary>
              <div className="min-w-0 space-y-2 pt-2">
                {laterHomeworks.map((homework) => (
                  <button
                    key={homework.homeworkId}
                    type="button"
                    onClick={() => onStartHomework(homework.homeworkId)}
                    className="flex min-h-11 w-full items-start justify-between gap-3 rounded-xl bg-[var(--admin-card-soft)] px-3 py-2 text-right transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                  >
                    <span className="min-w-0 break-words">
                      <span className="block line-clamp-2 text-sm font-bold leading-6 text-[var(--admin-text)]">{homework.homeworkTitle}</span>
                      <span className="mt-0.5 block line-clamp-2 text-xs leading-5 text-[var(--admin-muted)]">{homework.lessonTitle}</span>
                    </span>
                    <ArrowUpLeft className="mt-1 h-4 w-4 shrink-0 text-[var(--admin-primary)]" />
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
