"use client";

import { BookOpen, CalendarClock, Sparkles } from "lucide-react";
import { motion } from "framer-motion";

import type { DashboardDto } from "@/services/student-service";
import { ProgressRing } from "./ProgressRing";

type StudentHeroProps = {
  data: DashboardDto;
};

export function StudentHero({ data }: StudentHeroProps) {
  const completionLabel =
    data.overallProgressPercent >= 80
      ? "ممتاز، أنت قريب من الإتقان"
      : data.overallProgressPercent >= 45
        ? "مستواك ثابت، كمّل بنفس النسق"
        : "ابدأ بخطوة ثابتة والنتيجة هتظهر بسرعة";

  return (
    <section className="relative overflow-hidden rounded-[28px] border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-primary)]/10 via-[var(--admin-card)] to-[var(--admin-card-strong)] p-4 shadow-[0_28px_70px_var(--admin-shadow)] sm:rounded-[32px] sm:p-6 md:rounded-[36px] md:p-9">
      {/* Ambient glow — subtle pulse */}
      <motion.div
        className="absolute inset-y-0 left-0 w-1/2 bg-[radial-gradient(circle_at_center,var(--admin-primary)_0%,transparent_70%)] opacity-[0.06]"
        animate={{ opacity: [0.04, 0.08, 0.04] }}
        transition={{ duration: 4, repeat: Infinity, ease: "easeInOut" }}
      />

      <div className="relative grid gap-8 lg:grid-cols-[1.25fr_0.75fr] lg:items-center">
        <div className="space-y-6">
          <div className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-xs font-bold text-[var(--admin-primary)] max-w-full">
            <Sparkles className="h-4 w-4" />
            <span className="truncate sm:whitespace-normal">لوحة متابعة يومية بتصميم أهدى وأوضح</span>
          </div>

          <div className="space-y-3">
            <h1 className="text-2xl font-black leading-tight text-[var(--admin-text)] sm:text-3xl md:text-5xl">
              أهلاً بيك، {data.studentName}
            </h1>
            <p className="max-w-2xl text-sm leading-7 text-[var(--admin-muted)] sm:text-base md:text-lg">
              كل المهم موجود قدامك مباشرة: نقطة الاستكمال، مدى تقدمك، الباقات،
              والامتحانات القادمة بدون زحمة أو تكرار بصري.
            </p>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 backdrop-blur sm:rounded-[28px] sm:p-5">
              <div className="flex items-center gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
                  <BookOpen className="h-5 w-5" />
                </div>
                <div>
                  <p className="text-sm font-bold text-[var(--admin-muted)]">
                    الدروس المكتملة
                  </p>
                  <p className="text-xl font-black text-[var(--admin-text)]">
                    {data.totalLessonsCompleted}/{data.totalLessons}
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 backdrop-blur sm:rounded-[28px] sm:p-5">
              <div className="flex items-center gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
                  <CalendarClock className="h-5 w-5" />
                </div>
                <div>
                  <p className="text-sm font-bold text-[var(--admin-muted)]">
                    امتحانات منتظرة
                  </p>
                  <p className="text-xl font-black text-[var(--admin-text)]">
                    {data.upcomingExams.length}
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 backdrop-blur sm:rounded-[28px] sm:p-5">
              <p className="text-sm font-bold text-[var(--admin-muted)]">
                الباقات النشطة
              </p>
              <p className="mt-2 text-xl font-black text-[var(--admin-text)]">
                {data.activePackages.length}
              </p>
              <p className="mt-2 text-xs font-bold text-[var(--admin-primary)]">
                {completionLabel}
              </p>
            </div>
          </div>
        </div>

        {/* ── Progress Ring (SVG Glow) ── */}
        <div className="flex justify-center lg:justify-end">
          <ProgressRing percent={data.overallProgressPercent} />
        </div>
      </div>
    </section>
  );
}

