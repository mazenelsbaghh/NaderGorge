"use client";

import Link from "next/link";
import { ArrowUpLeft, BookOpenText, CirclePlay, FilePenLine, KeyRound } from "lucide-react";

import type { DashboardDto } from "@/services/student-service";

type StudentMomentumRailProps = {
  data: DashboardDto;
};

type RailStage = {
  id: string;
  label: string;
  title: string;
  href: string;
  cta: string;
  icon: typeof CirclePlay;
  active: boolean;
};

function buildStages(data: DashboardDto): RailStage[] {
  const currentStage: RailStage =
    data.activePackages.length === 0
      ? {
          id: "unlock",
          label: "الآن",
          title: "فعّل كودك لفتح أول باقة",
          href: "/student/code-redemption",
          cta: "فعّل كودك",
          icon: KeyRound,
          active: true,
        }
      : data.resumePoint
        ? {
            id: "resume",
            label: "الآن",
            title: data.resumePoint.lessonTitle,
            href: `/student/packages/${data.resumePoint.packageId}/lessons/${data.resumePoint.lessonId}`,
            cta: "كمّل الدرس",
            icon: CirclePlay,
            active: true,
          }
        : {
            id: "start",
            label: "الآن",
            title: "اختر أول درس من باقاتك",
            href: "/student/packages",
            cta: "افتح الباقات",
            icon: BookOpenText,
            active: true,
          };

  const nextStage: RailStage = data.upcomingExams[0]
    ? {
        id: "exam",
        label: "بعده",
        title: data.upcomingExams[0].examTitle,
        href: `/student/exams/${data.upcomingExams[0].examId}`,
        cta: "ابدأ الامتحان",
        icon: FilePenLine,
        active: false,
      }
    : {
        id: "packages",
        label: "بعده",
        title: "راجع الدروس المفتوحة التالية",
        href: "/student/packages",
        cta: "راجع الباقات",
        icon: BookOpenText,
        active: false,
      };

  return [currentStage, nextStage];
}

export function StudentMomentumRail({ data }: StudentMomentumRailProps) {
  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5 sm:p-6">
      <h2 className="text-lg font-black text-[var(--admin-text)]">مسارك الحالي</h2>
      <div className="mt-4 divide-y divide-[var(--admin-border)]">
        {buildStages(data).map((stage) => {
          const Icon = stage.icon;

          return (
            <Link
              key={stage.id}
              href={stage.href}
              className="flex min-h-16 items-center gap-3 py-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
            >
              <Icon className="h-5 w-5 shrink-0 text-[var(--admin-primary)]" />
              <span className="min-w-0 flex-1">
                <span className="block text-xs font-bold text-[var(--admin-primary)]">{stage.label}</span>
                <span className="mt-0.5 block truncate text-sm font-black text-[var(--admin-text)]">
                  {stage.title}
                </span>
              </span>
              <span className="flex min-h-11 shrink-0 items-center gap-1 text-xs font-black text-[var(--admin-primary)]">
                {stage.cta}
                <ArrowUpLeft className="h-4 w-4" />
              </span>
            </Link>
          );
        })}
      </div>
    </section>
  );
}
