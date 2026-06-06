"use client";

import type { DashboardDto } from "@/services/student-service";
import { ProgressRing } from "./ProgressRing";

type StudentHeroProps = {
  data: DashboardDto;
};

export function StudentHero({ data }: StudentHeroProps) {
  const completionLabel =
    data.overallProgressPercent >= 80
      ? "أنت قريب من إنهاء الجزء الأكبر من خطتك"
      : data.overallProgressPercent >= 45
        ? "أنت تسير بشكل ثابت، كمّل على نفس النسق"
        : "ابدأ بالدرس التالي، وسترى التقدّم يتحرك بسرعة";

  return (
    <section className="rounded-[28px] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] p-5 shadow-[0_28px_70px_var(--admin-shadow)] sm:rounded-[32px] sm:p-7 md:p-9">
      <div className="relative grid gap-6 lg:grid-cols-[1.15fr_0.85fr] lg:items-center">
        <div className="space-y-4">
          <div className="space-y-3">
            <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
              لوحة الطالب
            </p>
            <h1 className="text-2xl font-black leading-tight text-[var(--admin-text)] sm:text-3xl md:text-5xl">
              أهلاً بيك، {data.studentName}
            </h1>
            <p className="max-w-2xl text-sm leading-7 text-[var(--admin-muted)] sm:text-base md:text-lg">
              من هنا تعرف ما الذي عليك الآن: كمّل من آخر درس، تابع تقدّمك، وراجع
              ما ينتظرك بعد ذلك بدون تشتيت.
            </p>
          </div>

          <div className="inline-flex w-fit rounded-full bg-[var(--admin-card)] px-4 py-2 text-sm font-bold text-[var(--admin-primary)] ring-1 ring-[var(--admin-border)] sm:text-base">
            {completionLabel}
          </div>
        </div>

        <div className="flex justify-center lg:justify-end">
          <ProgressRing percent={data.overallProgressPercent} />
        </div>
      </div>
    </section>
  );
}
