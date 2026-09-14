import type { DashboardDto } from "@/services/student-service";

type StudentHeroProps = {
  data: DashboardDto;
};

export function StudentHero({ data }: StudentHeroProps) {
  return (
    <header className="flex items-center justify-between gap-4">
      <div className="min-w-0">
        <h1 className="text-2xl font-black leading-tight text-[var(--admin-text)] sm:text-3xl">
          أهلاً يا {data.studentName.split(' ')[0]}
        </h1>
        <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
          جاهز للمذاكرة؟
        </p>
      </div>

    </header>
  );
}
