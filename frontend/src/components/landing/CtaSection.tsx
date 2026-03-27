import Link from "next/link";

export function CtaSection() {
  return (
    <section className="px-4 py-24 md:px-0">
      <div className="mx-auto w-[min(1180px,92vw)]">
        <div className="relative overflow-hidden rounded-[40px] border border-[var(--landing-line-strong)] bg-[linear-gradient(135deg,rgba(118,82,38,0.98),rgba(152,106,50,0.94))] px-8 py-10 text-[var(--landing-accent-foreground)] shadow-[0_26px_80px_rgba(88,55,18,0.22)] md:px-12 md:py-14">
          <div className="absolute inset-y-0 left-0 w-1/3 bg-[radial-gradient(circle_at_center,rgba(255,255,255,0.16),transparent_68%)]" />
          <div className="relative flex flex-col items-start justify-between gap-8 lg:flex-row lg:items-center">
            <div className="max-w-2xl">
              <p className="text-sm font-bold tracking-[0.28em] text-white/70">READY TO LAUNCH</p>
              <h2 className="mt-4 text-3xl font-black leading-tight md:text-5xl">
                جاهز نكمّل نفس الاتجاه
                <span className="block">على بقية الصفحة والمحتوى؟</span>
              </h2>
              <p className="mt-4 text-base leading-8 text-white/80 md:text-lg">
                الهيكل بقى متقسم بشكل يسمح إننا نعدّل أي جزء لوحده: صور، نصوص، مواد،
                شهادات، أو حتى الـ color system بالكامل.
              </p>
            </div>

            <div className="flex flex-col gap-4 sm:flex-row">
              <Link
                href="/register"
                className="inline-flex items-center justify-center rounded-full bg-[var(--landing-card)] px-7 py-4 text-base font-extrabold text-[var(--landing-accent)] transition hover:-translate-y-0.5"
              >
                سجّل الآن
              </Link>
              <Link
                href="/about"
                className="inline-flex items-center justify-center rounded-full border border-white/20 px-7 py-4 text-base font-bold text-white transition hover:bg-white/10"
              >
                اعرف المزيد
              </Link>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

