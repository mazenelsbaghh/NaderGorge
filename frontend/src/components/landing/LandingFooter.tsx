import Link from "next/link";

export function LandingFooter() {
  return (
    <footer className="border-t border-[var(--landing-line)] px-4 py-8 md:px-0">
      <div className="mx-auto flex w-[min(1180px,92vw)] flex-col items-center justify-between gap-4 md:flex-row">
        <p className="text-sm font-semibold text-[var(--landing-muted)]">
          © 2026 الأستاذ نادر جورج. جميع الحقوق محفوظة.
        </p>
        <div className="flex items-center gap-5 text-sm font-bold text-[var(--landing-muted)]">
          <Link href="/about" className="transition hover:text-[var(--landing-accent)]">
            عن المنصة
          </Link>
          <Link href="/faq" className="transition hover:text-[var(--landing-accent)]">
            الأسئلة الشائعة
          </Link>
          <Link href="/login" className="transition hover:text-[var(--landing-accent)]">
            تسجيل الدخول
          </Link>
        </div>
      </div>
    </footer>
  );
}

