"use client";

import Link from "next/link";
import { useMemo } from "react";
import {
  ArrowUpLeft,
  BookOpenText,
  CirclePlay,
  FilePenLine,
  KeyRound,
} from "lucide-react";
import { motion, useMotionTemplate, useMotionValue, useReducedMotion } from "framer-motion";
import type { DashboardDto } from "@/services/student-service";
import { easeQuart } from "@/lib/motion";

type StudentMomentumRailProps = {
  data: DashboardDto;
};

type RailStage = {
  id: string;
  eyebrow: string;
  title: string;
  description: string;
  href: string;
  cta: string;
  icon: typeof CirclePlay;
  state: "active" | "next";
};

function buildStages(data: DashboardDto): RailStage[] {
  const currentStage: RailStage =
    data.activePackages.length === 0
      ? {
          id: "unlock",
          eyebrow: "الآن",
          title: "افتح وصولك أولًا",
          description: "فعّل كودًا جديدًا أو راجع الباقات المتاحة حتى يبدأ مسارك الدراسي.",
          href: "/student/code-redemption",
          cta: "فعّل كودك",
          icon: KeyRound,
          state: "active",
        }
      : data.resumePoint
        ? {
            id: "resume",
            eyebrow: "الآن",
            title: data.resumePoint.lessonTitle,
            description: `هذا هو أقصر طريق للرجوع إلى ${data.resumePoint.packageName}.`,
            href: `/student/packages/${data.resumePoint.packageId}/lessons/${data.resumePoint.lessonId}`,
            cta: "كمّل من هنا",
            icon: CirclePlay,
            state: "active",
          }
        : {
            id: "start",
            eyebrow: "الآن",
            title: "ابدأ أول درس من باقاتك",
            description: "باقاتك جاهزة، والضغط هنا يأخذك مباشرة إلى أول نقطة بدء واضحة.",
            href: "/student/packages",
            cta: "افتح الباقات",
            icon: BookOpenText,
            state: "active",
          };

  const nextStage: RailStage =
    data.upcomingExams.length > 0
      ? {
          id: "exam",
          eyebrow: "بعده",
          title: data.upcomingExams[0].examTitle,
          description: `بعد إنهاء الدرس، هذا أقرب امتحان ينتظرك في ${data.upcomingExams[0].lessonTitle}.`,
          href: `/student/exams/${data.upcomingExams[0].examId}`,
          cta: "ابدأ الحل",
          icon: FilePenLine,
          state: "next",
        }
      : {
          id: "packages",
          eyebrow: "بعده",
          title: "راجع خطتك داخل الباقات",
          description: "إذا لم يكن هناك امتحان الآن، فهذه أسرع طريقة لمعرفة الدروس المفتوحة التالية.",
          href: "/student/packages",
          cta: "راجع الباقات",
          icon: BookOpenText,
          state: "next",
        };

  return [currentStage, nextStage];
}

export function StudentMomentumRail({ data }: StudentMomentumRailProps) {
  const shouldReduceMotion = useReducedMotion();
  const pointerX = useMotionValue(50);
  const pointerY = useMotionValue(50);
  const pointerGlow = useMotionTemplate`radial-gradient(26rem circle at ${pointerX}% ${pointerY}%, color-mix(in srgb, var(--admin-primary) 9%, transparent), transparent 72%)`;

  const stages = useMemo(() => buildStages(data), [data]);
  const activeIndex = stages.findIndex((stage) => stage.state === "active");

  return (
    <motion.section
      className="relative overflow-hidden rounded-2xl bg-[var(--admin-card)] p-6 shadow-[0_20px_56px_var(--admin-shadow)] ring-1 ring-[var(--admin-border)] sm:p-8"
      onPointerMove={(event) => {
        if (shouldReduceMotion) {
          return;
        }

        const rect = event.currentTarget.getBoundingClientRect();
        pointerX.set(((event.clientX - rect.left) / rect.width) * 100);
        pointerY.set(((event.clientY - rect.top) / rect.height) * 100);
      }}
      initial={shouldReduceMotion ? false : { opacity: 0, y: 18, scale: 0.985 }}
      animate={shouldReduceMotion ? { opacity: 1 } : { opacity: 1, y: 0, scale: 1 }}
      transition={{ duration: 0.65, ease: easeQuart }}
    >
      <motion.div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0"
        style={shouldReduceMotion ? undefined : { backgroundImage: pointerGlow }}
      />

      <div className="relative">
        <div className="max-w-2xl space-y-2">
          <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
            مسارك الآن
          </p>
          <h2 className="text-2xl font-black leading-tight text-[var(--admin-text)] sm:text-3xl">
            ابدأ من الجاهز الآن، ثم انتقل لما بعده
          </h2>
        </div>

        <div className="relative mt-6">
          <motion.div
            aria-hidden="true"
            className="absolute right-[25%] top-8 hidden h-[2px] w-[50%] origin-right bg-[linear-gradient(90deg,var(--admin-primary)_0%,color-mix(in_srgb,var(--admin-primary)_35%,transparent)_55%,transparent_100%)] lg:block"
            initial={shouldReduceMotion ? false : { scaleX: 0.2, opacity: 0 }}
            animate={{ scaleX: 1, opacity: 0.45 }}
            transition={{ duration: 0.9, delay: 0.15, ease: easeQuart }}
          />

          <div className="grid gap-4 lg:grid-cols-2">
            {stages.map((stage, index) => {
              const Icon = stage.icon;
              const isActive = index === activeIndex;

              return (
                <motion.article
                  key={stage.id}
                  initial={shouldReduceMotion ? false : { opacity: 0, y: 24, scale: 0.98 }}
                  animate={shouldReduceMotion ? { opacity: 1 } : { opacity: 1, y: 0, scale: 1 }}
                  transition={{
                    duration: 0.55,
                    delay: 0.12 + index * 0.08,
                    ease: easeQuart,
                  }}
                  className={`relative overflow-hidden rounded-[28px] border p-5 sm:p-6 ${
                    isActive
                      ? "border-[var(--admin-primary)] bg-[linear-gradient(135deg,var(--admin-primary-12),var(--admin-card))] shadow-[0_14px_32px_var(--admin-shadow)]"
                      : "border-[var(--admin-border)] bg-[var(--admin-card-soft)]"
                  }`}
                  whileHover={shouldReduceMotion ? undefined : { y: -3 }}
                >
                  {isActive && (
                    <motion.div
                      aria-hidden="true"
                      className="pointer-events-none absolute inset-x-0 top-0 h-1 bg-[linear-gradient(90deg,transparent,var(--admin-primary),transparent)]"
                      animate={shouldReduceMotion ? { opacity: 0.75 } : { opacity: [0.35, 0.75, 0.35] }}
                      transition={shouldReduceMotion ? { duration: 0 } : { duration: 2.8, repeat: Infinity, ease: "easeInOut" }}
                    />
                  )}

                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-3">
                      <span className="inline-flex rounded-full bg-[var(--admin-card)] px-3 py-1 text-[11px] font-black text-[var(--admin-primary)] ring-1 ring-[var(--admin-border)]">
                        {stage.eyebrow}
                      </span>
                      <h3 className="text-xl font-black leading-tight text-[var(--admin-text)]">
                        {stage.title}
                      </h3>
                    </div>

                    <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-card)] text-[var(--admin-primary)] ring-1 ring-[var(--admin-border)]">
                      <Icon className="h-5 w-5" />
                    </div>
                  </div>

                  <p className="mt-3 min-h-14 text-sm leading-7 text-[var(--admin-muted)]">
                    {stage.description}
                  </p>

                  <Link
                    href={stage.href}
                    className={`mt-4 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-2xl px-4 text-sm font-black transition focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 ${
                      isActive
                        ? "bg-[var(--admin-primary)] text-[var(--admin-primary-foreground)] hover:-translate-y-0.5 focus-visible:ring-offset-[var(--admin-card)]"
                        : "bg-[var(--admin-card)] text-[var(--admin-text)] ring-1 ring-[var(--admin-border)] hover:bg-[var(--admin-card-strong)] focus-visible:ring-offset-[var(--admin-card-soft)]"
                    }`}
                  >
                    <span>{stage.cta}</span>
                    <ArrowUpLeft className="h-4 w-4" />
                  </Link>
                </motion.article>
              );
            })}
          </div>
        </div>
      </div>
    </motion.section>
  );
}
