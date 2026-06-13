import type { DashboardDto } from "@/services/student-service";
import { ProgressRing } from "./ProgressRing";

type StudentHeroProps = {
  data: DashboardDto;
};

export function StudentHero({ data }: StudentHeroProps) {
  return (
    <header className="flex items-center justify-between gap-4 border-b border-[var(--admin-border)] pb-5">
      <div className="min-w-0">
        <h1 className="text-2xl font-black leading-tight text-[var(--admin-text)] sm:text-3xl">
          أهلاً بيك، {data.studentName}
        </h1>
        <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
          ابدأ بالخطوة الجاهزة لك الآن.
        </p>
      </div>

      <div className="flex shrink-0 items-center gap-3">
        <div className="hidden text-left sm:block">
          <p className="text-xs font-bold text-[var(--admin-muted)]">التقدم الكلي</p>
          <p className="mt-1 text-sm font-black text-[var(--admin-text)]">
            {data.totalLessonsCompleted} من {data.totalLessons} درس
          </p>
        </div>
        <ProgressRing percent={data.overallProgressPercent} />
      </div>
    </header>
  );
}
