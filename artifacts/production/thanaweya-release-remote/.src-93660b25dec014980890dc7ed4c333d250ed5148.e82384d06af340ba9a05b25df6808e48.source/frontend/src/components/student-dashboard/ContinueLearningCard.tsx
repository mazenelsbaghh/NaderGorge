import { ArrowUpLeft, BookOpenText, KeyRound, PlayCircle } from "lucide-react";

import type { ResumePointDto } from "@/services/student-service";

type ContinueLearningCardProps = {
  resumePoint?: ResumePointDto;
  hasActivePackages?: boolean;
  onContinue: () => void;
};

export function ContinueLearningCard({
  resumePoint,
  hasActivePackages = false,
  onContinue,
}: ContinueLearningCardProps) {
  const Icon = resumePoint ? PlayCircle : hasActivePackages ? BookOpenText : KeyRound;
  const title = resumePoint
    ? resumePoint.lessonTitle
    : hasActivePackages
      ? "اختر الدرس الذي ستبدأ به"
      : "فعّل كودك لفتح أول باقة";
  const description = resumePoint
    ? `ارجع مباشرة إلى ${resumePoint.packageName}، الدرس ${resumePoint.lessonOrder}.`
    : hasActivePackages
      ? "باقاتك مفعلة وجاهزة. افتحها واختر أول درس متاح."
      : "أدخل كود التفعيل، وستظهر باقتك ودروسها هنا مباشرة.";
  const actionLabel = resumePoint
    ? "كمّل الدرس"
    : hasActivePackages
      ? "افتح الباقات"
      : "فعّل كودك";

  return (
    <section className="flex h-full flex-col justify-between rounded-2xl bg-[var(--admin-primary)] p-5 text-[var(--admin-primary-contrast)] sm:p-7">
      <div>
        <div className="flex items-start gap-4">
          <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary-contrast)]/10">
            <Icon className="h-6 w-6" />
          </div>
          <div className="min-w-0">
            <p className="text-xs font-bold">خطوتك الآن</p>
            <h2 className="mt-2 text-2xl font-black leading-tight sm:text-3xl">
              {title}
            </h2>
          </div>
        </div>
        <p className="mt-5 max-w-2xl text-sm leading-7 opacity-85 sm:text-base">
          {description}
        </p>
      </div>

      <button
        type="button"
        onClick={onContinue}
        className="mt-6 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-xl bg-[var(--admin-card)] px-6 text-sm font-black text-[var(--admin-primary)] transition-colors hover:bg-[var(--admin-card-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary-contrast)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-primary)] sm:w-fit"
      >
        {actionLabel}
        <ArrowUpLeft className="h-4 w-4" />
      </button>
    </section>
  );
}
