"use client";

import { useState } from "react";
import Link from "next/link";
import { ArrowUpLeft, BookOpenText, ChevronDown, KeyRound, X } from "lucide-react";

import type { DashboardDto } from "@/services/student-service";

const ONBOARDING_DISMISS_KEY = "student-dashboard-onboarding-dismissed-v1";

type StudentGettingStartedPanelProps = {
  data: DashboardDto;
};

type OnboardingVariant = {
  title: string;
  description: string;
  primaryCta: {
    href: string;
    label: string;
    icon: typeof KeyRound;
  };
  secondaryCta: {
    href: string;
    label: string;
    icon: typeof KeyRound;
  };
  steps: Array<{
    title: string;
    detail: string;
  }>;
};

function getVariant(data: DashboardDto): OnboardingVariant | null {
  if (data.activePackages.length === 0) {
    return {
      title: "تحتاج مساعدة في البداية؟",
      description: "ثلاث خطوات قصيرة من التفعيل حتى أول درس.",
      primaryCta: {
        href: "/student/code-redemption",
        label: "فعّل كودك",
        icon: KeyRound,
      },
      secondaryCta: {
        href: "/student/packages",
        label: "تصفح الباقات",
        icon: BookOpenText,
      },
      steps: [
        {
          title: "افتح الوصول",
          detail: "فعّل كودًا جديدًا أو راجع الباقات المتاحة لك.",
        },
        {
          title: "ابدأ أول درس",
          detail: "بعد التفعيل ستجد الباقة والدرس المتاح في لوحة الطالب.",
        },
        {
          title: "تابع تقدمك",
          detail: "إكمال الدروس والامتحانات يحدّث تقدمك تلقائيًا.",
        },
      ],
    };
  }

  if (!data.resumePoint && data.totalLessonsCompleted === 0) {
    return {
      title: "باقاتك جاهزة، ابدأ أول درس",
      description: "افتح إحدى باقاتك واختر أول درس متاح.",
      primaryCta: {
        href: "/student/packages",
        label: "افتح الباقات",
        icon: BookOpenText,
      },
      secondaryCta: {
        href: "/student/mistakes",
        label: "راجع ملف الأخطاء",
        icon: BookOpenText,
      },
      steps: [
        {
          title: "اختر الباقة",
          detail: "افتح الباقة المناسبة لرؤية الدروس المتاحة.",
        },
        {
          title: "ابدأ الدرس",
          detail: "ستجد الواجب أو الامتحان المرتبط داخل المسار نفسه.",
        },
        {
          title: "ارجع إلى اللوحة",
          detail: "بعد الإكمال ستظهر لك نقطة الاستكمال التالية مباشرة.",
        },
      ],
    };
  }

  return null;
}

export function StudentGettingStartedPanel({ data }: StudentGettingStartedPanelProps) {
  const [dismissed, setDismissed] = useState(() => {
    if (typeof window === "undefined") return true;
    return window.localStorage.getItem(ONBOARDING_DISMISS_KEY) === "true";
  });

  const variant = getVariant(data);

  if (!variant || dismissed) return null;

  const PrimaryIcon = variant.primaryCta.icon;
  const SecondaryIcon = variant.secondaryCta.icon;

  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
      <div className="flex items-start justify-between gap-3 px-5 py-4 sm:px-6">
        <div>
          <h2 className="text-base font-black text-[var(--admin-text)]">{variant.title}</h2>
          <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">{variant.description}</p>
        </div>
        <button
          type="button"
          aria-label="إخفاء خطوات البداية"
          onClick={() => {
            window.localStorage.setItem(ONBOARDING_DISMISS_KEY, "true");
            setDismissed(true);
          }}
          className="flex min-h-11 min-w-11 shrink-0 items-center justify-center rounded-xl text-[var(--admin-muted)] transition-colors hover:bg-[var(--admin-card-soft)] hover:text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
        >
          <X className="h-5 w-5" />
        </button>
      </div>

      <details className="group border-t border-[var(--admin-border)]">
        <summary className="flex min-h-12 cursor-pointer list-none items-center justify-between gap-3 px-5 text-sm font-bold text-[var(--admin-primary)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)] sm:px-6">
          <span>اعرض خطوات البداية</span>
          <ChevronDown className="h-4 w-4 transition-transform group-open:rotate-180" />
        </summary>

        <div className="border-t border-[var(--admin-border)] p-5 sm:p-6">
          <ol className="space-y-4">
            {variant.steps.map((step, index) => (
              <li key={step.title} className="flex gap-3">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[var(--admin-card-soft)] text-xs font-black text-[var(--admin-primary)]">
                  {index + 1}
                </span>
                <div>
                  <h3 className="text-sm font-black text-[var(--admin-text)]">{step.title}</h3>
                  <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">{step.detail}</p>
                </div>
              </li>
            ))}
          </ol>

          <div className="mt-5 flex flex-col gap-2 sm:flex-row">
            <Link
              href={variant.primaryCta.href}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
            >
              <PrimaryIcon className="h-4 w-4" />
              {variant.primaryCta.label}
            </Link>
            <Link
              href={variant.secondaryCta.href}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-[var(--admin-card-soft)] px-5 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
            >
              <SecondaryIcon className="h-4 w-4" />
              {variant.secondaryCta.label}
              <ArrowUpLeft className="h-4 w-4" />
            </Link>
          </div>
        </div>
      </details>
    </section>
  );
}
