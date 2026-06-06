import Link from "next/link";
import Image from "next/image";

import { navigationLinks } from "./data";

export function LandingNav() {
  return (
    <header className="absolute inset-x-0 top-0 z-30 px-4 pt-4 md:px-8">
      <div className="mx-auto flex w-full max-w-[1410px] items-center justify-between gap-4">
        <Link href="/" className="flex shrink-0 items-center gap-3" aria-label="منصة مسار">
          <Image
            src="/images/logo.svg"
            width={112}
            height={64}
            className="h-14 w-auto object-contain md:h-16 dark:hidden"
            style={{ width: "auto", height: "auto" }}
            alt="منصة مسار"
            priority
          />
          <Image
            src="/images/logo-mark-light.svg"
            width={64}
            height={64}
            className="h-14 w-auto object-contain md:h-16 hidden dark:block"
            style={{ width: "auto", height: "auto" }}
            alt="منصة مسار"
            priority
          />
        </Link>

        <nav className="hidden items-center gap-8 lg:flex">
          {navigationLinks.map((link) => (
            <a
              key={link.href}
              href={link.href}
              className="text-sm font-extrabold text-[var(--landing-ink)] transition hover:text-[#0E8F8F]"
            >
              {link.label}
            </a>
          ))}
        </nav>

        <div className="flex items-center gap-2 sm:gap-3">
          <Link href="/login" className="landing-secondary-button hidden sm:inline-flex">
            تسجيل الدخول
          </Link>
          <Link href="/register" className="landing-primary-button px-4 sm:px-6">
            ابدأ الآن
          </Link>
        </div>
      </div>
    </header>
  );
}
