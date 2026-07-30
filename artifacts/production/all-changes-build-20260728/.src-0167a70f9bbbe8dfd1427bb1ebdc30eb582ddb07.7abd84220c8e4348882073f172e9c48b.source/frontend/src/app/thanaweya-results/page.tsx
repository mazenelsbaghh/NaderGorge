import type { Metadata } from 'next';
import Link from 'next/link';
import { ArrowLeft, Clock3, GraduationCap, Sparkles } from 'lucide-react';

export const metadata: Metadata = {
  title: 'نتيجة الثانوية العامة | منصة مسار',
  description: 'صفحة نتيجة الثانوية العامة على منصة مسار.',
};

export default function ThanaweyaResultsPage() {
  return (
    <main className="relative isolate flex min-h-[calc(100svh-4rem)] items-center overflow-hidden bg-[var(--landing-bg)] px-4 py-16 text-[var(--landing-ink)] sm:px-6 lg:px-8">
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_15%_20%,color-mix(in_srgb,var(--landing-accent)_14%,transparent),transparent_32%),radial-gradient(circle_at_82%_84%,color-mix(in_srgb,var(--primary)_12%,transparent),transparent_30%)]"
      />

      <section className="mx-auto w-full max-w-3xl rounded-[2rem] border border-[var(--landing-line)] bg-[var(--landing-card)] p-6 text-center shadow-[0_24px_70px_rgba(10,29,61,0.12)] sm:p-10 lg:p-14">
        <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-[var(--landing-teal-soft)] text-[var(--landing-accent)]">
          <GraduationCap className="h-8 w-8" aria-hidden="true" />
        </div>

        <span className="mt-7 inline-flex items-center gap-2 rounded-full border border-[var(--landing-line)] bg-[var(--landing-bg-soft)] px-4 py-2 text-sm font-extrabold text-[var(--landing-accent)]">
          <Sparkles className="h-4 w-4" aria-hidden="true" />
          خدمة طلاب الثانوية العامة
        </span>

        <h1 className="mt-5 text-balance text-3xl font-black leading-tight sm:text-5xl">
          نتيجة الثانوية العامة
        </h1>
        <p className="mx-auto mt-4 max-w-xl text-pretty text-base font-semibold leading-8 text-[var(--landing-muted)] sm:text-lg">
          سيتم إتاحة النتيجة هنا خلال ساعة. نجهز صفحة الاستعلام لتظهر لك النتيجة بسهولة فور توفرها.
        </p>

        <div className="mx-auto mt-8 flex max-w-md items-center gap-3 rounded-2xl border border-[var(--landing-line)] bg-[var(--landing-bg-soft)] px-4 py-4 text-right">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[var(--landing-card)] text-[var(--landing-accent)] shadow-sm">
            <Clock3 className="h-5 w-5" aria-hidden="true" />
          </span>
          <p className="text-sm font-bold leading-6 text-[var(--landing-muted)]">
            ارجع للصفحة بعد قليل؛ سيتوفر رابط الاستعلام في هذا المكان.
          </p>
        </div>

        <Link
          href="/"
          className="mt-9 inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-[var(--landing-accent)] px-6 text-base font-black text-[var(--landing-accent-foreground)] transition-colors hover:bg-[var(--landing-accent-strong)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--landing-accent)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--landing-card)]"
        >
          العودة للرئيسية
          <ArrowLeft className="h-5 w-5" aria-hidden="true" />
        </Link>
      </section>
    </main>
  );
}
