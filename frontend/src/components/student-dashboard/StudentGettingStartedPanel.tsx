"use client";

import { useState } from "react";
import Link from "next/link";
import { ArrowUpLeft, BookOpenText, KeyRound, X } from "lucide-react";
import type { DashboardDto } from "@/services/student-service";

const ONBOARDING_DISMISS_KEY = "student-dashboard-onboarding-dismissed-v1";

type StudentGettingStartedPanelProps = {
  data: DashboardDto;
};

type OnboardingVariant = {
  eyebrow: string;
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
      eyebrow: "ابدأ من هنا",
      title: "ابدأ بخطوتين بس",
      description:
        "فعّل كودك الأول أو شوف الباقات المتاحة ليك، وبعدها هتظهرلك الدروس اللي تقدر تبدأ فيها على طول.",
      primaryCta: {
        href: "/student/code-redemption",
        label: "فعّل كودك دلوقتي",
        icon: KeyRound,
      },
      secondaryCta: {
        href: "/student/packages",
        label: "شوف كل الباقات",
        icon: BookOpenText,
      },
      steps: [
        {
          title: "1. افتح الوصول",
          detail: "فعّل كود جديد أو شوف الباقات اللي متاحة ليك.",
        },
        {
          title: "2. ابدأ أول درس",
          detail: "بعد التفعيل هتلاقي الباقة المفتوحة والدرس اللي بعده في نفس الصفحة.",
        },
        {
          title: "3. تابع تقدمك",
          detail: "كل درس أو امتحان تخلصه هيزوّد نسبة التقدم ويوريك إيه اللي فاضل.",
        },
      ],
    };
  }

  if (!data.resumePoint && data.totalLessonsCompleted === 0) {
    return {
      eyebrow: "خطوة البداية",
      title: "الباقات جاهزة، ابدأ أول درس",
      description:
        "مش محتاج شرح كتير. افتح باقتك وابدأ أول درس، والخطوة اللي بعدها هتظهرلك لوحدها وإنت بتذاكر.",
      primaryCta: {
        href: "/student/packages",
        label: "افتح الباقات",
        icon: BookOpenText,
      },
      secondaryCta: {
        href: "/student/mistakes",
        label: "ملف الأخطاء — بعدين",
        icon: BookOpenText,
      },
      steps: [
        {
          title: "1. اختار الباقة",
          detail: "افتح الباقة المناسبة ليك عشان تشوف الدروس المتاحة دلوقتي.",
        },
        {
          title: "2. ابدأ أول درس",
          detail: "بعد ما تفتح الدرس هتلاقي الواجب أو الاختبار المرتبط بيه في نفس المسار.",
        },
        {
          title: "3. ارجع هنا بعد ما تخلص",
          detail: "بعد ما تخلص هتتحول لوحة الطالب لملخص واضح لإنجازاتك واللي فاضل.",
        },
      ],
    };
  }

  return null;
}

export function StudentGettingStartedPanel({ data }: StudentGettingStartedPanelProps) {
  const [dismissed, setDismissed] = useState(() => {
    if (typeof window === "undefined") {
      return true;
    }

    return window.localStorage.getItem(ONBOARDING_DISMISS_KEY) === "true";
  });

  const variant = getVariant(data);

  if (!variant || dismissed) {
    return null;
  }

  const PrimaryIcon = variant.primaryCta.icon;
  const SecondaryIcon = variant.secondaryCta.icon;

  return (
    <section className="rounded-2xl bg-[var(--admin-card)] p-6 shadow-sm ring-1 ring-[var(--admin-border)] sm:p-8">
      <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
        <div className="max-w-2xl space-y-3">
          <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
            {variant.eyebrow}
          </p>
          <h2 className="text-2xl font-black leading-tight text-[var(--admin-text)]">
            {variant.title}
          </h2>
          <p className="text-sm leading-7 text-[var(--admin-muted)] sm:text-base">
            {variant.description}
          </p>
        </div>

        <button
          type="button"
          onClick={() => {
            window.localStorage.setItem(ONBOARDING_DISMISS_KEY, "true");
            setDismissed(true);
          }}
          className="inline-flex min-h-11 items-center justify-center gap-2 self-start rounded-2xl px-4 text-sm font-bold text-[var(--admin-muted)] transition-colors hover:bg-[var(--admin-card-soft)] hover:text-[var(--admin-text)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
        >
          <X className="h-4 w-4" />
          <span>هستكشف بنفسي</span>
        </button>
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-[1.15fr_0.85fr]">
        <div className="grid gap-3">
          {variant.steps.map((step) => (
            <div
              key={step.title}
              className="rounded-[24px] bg-[var(--admin-card-soft)] px-5 py-4"
            >
              <h3 className="text-sm font-black text-[var(--admin-text)]">{step.title}</h3>
              <p className="mt-1 text-sm leading-7 text-[var(--admin-muted)]">{step.detail}</p>
            </div>
          ))}
        </div>

        <div className="flex flex-col gap-3 rounded-[24px] bg-[var(--admin-primary-12)] p-5">
          <p className="text-sm font-bold leading-7 text-[var(--admin-text)]">
            الفكرة بسيطة: تعرف إيه اللي عليك دلوقتي، وتوصله في أقل عدد من الضغطات.
          </p>

          <div className="mt-auto flex flex-col gap-3">
            <Link
              href={variant.primaryCta.href}
              className="inline-flex min-h-12 items-center justify-center gap-2 rounded-2xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-foreground)] transition-transform hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
            >
              <PrimaryIcon className="h-4 w-4" />
              <span>{variant.primaryCta.label}</span>
            </Link>

            <Link
              href={variant.secondaryCta.href}
              className="inline-flex min-h-12 items-center justify-center gap-2 rounded-2xl bg-[var(--admin-card-soft)] px-5 text-sm font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)]"
            >
              <SecondaryIcon className="h-4 w-4" />
              <span>{variant.secondaryCta.label}</span>
              <ArrowUpLeft className="h-4 w-4" />
            </Link>
          </div>
        </div>
      </div>
    </section>
  );
}
