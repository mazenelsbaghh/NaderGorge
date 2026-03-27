import Link from "next/link";

import { navigationLinks } from "./data";

export function LandingNav() {
  return (
    <header className="sticky top-0 z-30">
      <div className="mx-auto flex w-[min(1180px,92vw)] items-center justify-between rounded-full border border-[var(--landing-line)] bg-[color:rgba(250,242,226,0.84)] px-4 py-3 shadow-[0_18px_40px_rgba(88,55,18,0.08)] backdrop-blur md:px-6">
        <div className="flex items-center gap-3">
          <div className="flex h-11 w-11 items-center justify-center rounded-full border border-[var(--landing-line)] bg-[var(--landing-card)] text-lg text-[var(--landing-accent)] shadow-inner">
            ☥
          </div>
          <div>
            <p className="text-xs font-semibold tracking-[0.3em] text-[var(--landing-muted)]">
              NADER GORGE
            </p>
            <p className="text-base font-black text-[var(--landing-ink)] md:text-lg">
              الأستاذ نادر جورج
            </p>
          </div>
        </div>

        <nav className="hidden items-center gap-6 md:flex">
          {navigationLinks.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="text-sm font-bold text-[var(--landing-muted)] transition hover:text-[var(--landing-accent)]"
            >
              {link.label}
            </a>
          ))}
        </nav>

        <div className="flex items-center gap-3">
          <Link
            href="/login"
            className="hidden rounded-full border border-[var(--landing-line)] px-5 py-2.5 text-sm font-bold text-[var(--landing-ink)] transition hover:bg-[var(--landing-card)] md:inline-flex"
          >
            تسجيل الدخول
          </Link>
          <Link
            href="/register"
            className="rounded-full bg-[var(--landing-accent)] px-5 py-2.5 text-sm font-extrabold text-[var(--landing-accent-foreground)] shadow-[0_10px_24px_rgba(145,95,42,0.28)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
          >
            احجز مكانك
          </Link>
        </div>
      </div>
    </header>
  );
}

