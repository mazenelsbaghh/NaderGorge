import { ArrowUpLeft, PlayCircle } from "lucide-react";

import type { ResumePointDto } from "@/services/student-service";

type ContinueLearningCardProps = {
  resumePoint?: ResumePointDto;
  onContinue: () => void;
};

export function ContinueLearningCard({
  resumePoint,
  onContinue,
}: ContinueLearningCardProps) {
  if (!resumePoint) {
    return (
      <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-7 shadow-sm">
        <div className="flex flex-col gap-5 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
              الخطوة التالية
            </p>
            <h2 className="mt-3 text-2xl font-black text-[var(--admin-text)]">
              افتح أول باقة ليظهر لك مسارك الدراسي
            </h2>
            <p className="mt-3 max-w-2xl text-base leading-8 text-[var(--admin-muted)]">
              بمجرد تفعيل كود أو فتح باقة، ستظهر هنا نقطة الاستكمال المباشرة بدل البحث داخل المحتوى.
            </p>
          </div>
          <button
            type="button"
            onClick={onContinue}
            className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] md:w-auto"
          >
            فعّل كود الآن
            <ArrowUpLeft className="h-4 w-4" />
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-7 shadow-sm">
      <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-start gap-4">
          <div className="mt-1 flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
            <PlayCircle className="h-7 w-7" />
          </div>
          <div>
            <p className="text-xs font-black uppercase tracking-[0.24em] text-[var(--admin-primary)]">
              استكمال الآن
            </p>
            <h2 className="mt-3 text-2xl font-black text-[var(--admin-text)]">
              {resumePoint.lessonTitle}
            </h2>
            <p className="mt-2 text-base font-semibold text-[var(--admin-primary)]">
              {resumePoint.packageName}
            </p>
            <p className="mt-2 max-w-2xl text-base leading-8 text-[var(--admin-muted)]">
              آخر نقطة وصلت إليها في الدرس {resumePoint.lessonOrder}. اضغط على الاستكمال لتعود مباشرة بدون خطوات إضافية.
            </p>
          </div>
        </div>

        <button
          type="button"
          onClick={onContinue}
          className="inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-7 py-4 text-base font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_8px_18px_var(--admin-shadow)] transition-colors hover:bg-[var(--admin-primary-strong)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-card)] lg:w-auto"
        >
          كمّل من هنا
          <ArrowUpLeft className="h-4 w-4" />
        </button>
      </div>
    </section>
  );
}
