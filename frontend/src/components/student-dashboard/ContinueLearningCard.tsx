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
      <section className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl rounded-[32px] p-7">
        <div className="flex flex-col gap-5 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-sm font-black tracking-[0.24em] text-[var(--admin-muted)]">
              NEXT STEP
            </p>
            <h2 className="mt-3 text-2xl font-black text-[var(--admin-text)]">
              ابدأ أول باقة وخلّي الرحلة أوضح
            </h2>
            <p className="mt-3 max-w-2xl text-base leading-8 text-[var(--admin-muted)]">
              بمجرد تفعيل كود أو دخول باقة، هتظهر هنا نقطة الاستكمال المباشرة بدل ما
              تدور جوه المحتوى.
            </p>
          </div>
          <button
            onClick={onContinue}
            className="inline-flex items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-6 py-3.5 text-sm font-extrabold text-[var(--admin-primary-contrast)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)]"
          >
            فعّل كود الآن
            <ArrowUpLeft className="h-4 w-4" />
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl rounded-[32px] p-7">
      <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-start gap-4">
          <div className="mt-1 flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--admin-card-strong)] text-[var(--admin-primary)]">
            <PlayCircle className="h-7 w-7" />
          </div>
          <div>
            <p className="text-sm font-black tracking-[0.24em] text-[var(--admin-muted)]">
              CONTINUE LEARNING
            </p>
            <h2 className="mt-3 text-2xl font-black text-[var(--admin-text)]">
              {resumePoint.lessonTitle}
            </h2>
            <p className="mt-2 text-base font-semibold text-[var(--admin-primary)]">
              {resumePoint.packageName}
            </p>
            <p className="mt-2 max-w-2xl text-base leading-8 text-[var(--admin-muted)]">
              آخر نقطة وقفت عندها في الدرس {resumePoint.lessonOrder}. اضغط استكمال
              وارجع مباشرة من غير أي خطوات إضافية.
            </p>
          </div>
        </div>

        <button
          onClick={onContinue}
          className="inline-flex items-center justify-center gap-2 rounded-full bg-[var(--admin-primary)] px-7 py-4 text-base font-extrabold text-[var(--admin-primary-contrast)] shadow-[0_14px_30px_rgba(145,95,42,0.25)] transition hover:-translate-y-0.5 hover:bg-[var(--admin-primary-strong)]"
        >
          استكمل الآن
          <ArrowUpLeft className="h-4 w-4" />
        </button>
      </div>
    </section>
  );
}

