import type { JSX, SVGProps } from "react";
import { ArrowUpLeft } from "lucide-react";
import Link from "next/link";

const footerSections = [
  {
    title: "المنصة",
    links: [
      { href: "/register", label: "ابدأ التجربة" },
      { href: "/login", label: "تسجيل الدخول" },
      { href: "/about", label: "عن الأستاذ" },
    ],
  },
  {
    title: "المساعدة",
    links: [
      { href: "/faq", label: "الأسئلة الشائعة" },
      { href: "#testimonials", label: "آراء الطلبة" },
      { href: "mailto:hello@nadergorge.com", label: "تواصل معنا" },
    ],
  },
  {
    title: "النظام",
    links: [
      { href: "/register", label: "احجز مكانك" },
      { href: "/about", label: "رؤيتنا التعليمية" },
      { href: "/login", label: "لوحة المتابعة" },
    ],
  },
] as const;

function GithubIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}>
      <path d="M15 22v-4a4.8 4.8 0 0 0-1-3.5c3 0 6-2 6-5.5.08-1.25-.27-2.48-1-3.5.28-1.15.28-2.35 0-3.5 0 0-1 0-3 1.5-2.64-.5-5.36-.5-8 0C6 2 5 2 5 2c-.3 1.15-.3 2.35 0 3.5A5.4 5.4 0 0 0 4 9c0 3.5 3 5.5 6 5.5-.39.49-.68 1.05-.85 1.65-.17.6-.22 1.23-.15 1.85v4" />
      <path d="M9 18c-4.51 2-5-2-7-2" />
    </svg>
  );
}

function LinkedinIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}>
      <path d="M16 8a6 6 0 0 1 6 6v7h-4v-7a2 2 0 0 0-2-2 2 2 0 0 0-2 2v7h-4v-7a6 6 0 0 1 6-6z" />
      <rect width="4" height="12" x="2" y="9" />
      <circle cx="4" cy="4" r="2" />
    </svg>
  );
}

function YoutubeIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}>
      <path d="M2.5 17a24.12 24.12 0 0 1 0-10 2 2 0 0 1 1.4-1.4 49.56 49.56 0 0 1 16.2 0A2 2 0 0 1 21.5 7a24.12 24.12 0 0 1 0 10 2 2 0 0 1-1.4 1.4 49.55 49.55 0 0 1-16.2 0A2 2 0 0 1 2.5 17" />
      <path d="m10 15 5-3-5-3z" />
    </svg>
  );
}

function InstagramIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}>
      <rect width="20" height="20" x="2" y="2" rx="5" ry="5" />
      <path d="M16 11.37A4 4 0 1 1 12.63 8 4 4 0 0 1 16 11.37z" />
      <line x1="17.5" x2="17.51" y1="6.5" y2="6.5" />
    </svg>
  );
}

const socialLinks: Array<{
  href: string;
  label: string;
  icon: (props: SVGProps<SVGSVGElement>) => JSX.Element;
}> = [
  { href: "#", label: "GitHub", icon: GithubIcon },
  { href: "#", label: "LinkedIn", icon: LinkedinIcon },
  { href: "#", label: "YouTube", icon: YoutubeIcon },
  { href: "#", label: "Instagram", icon: InstagramIcon },
];

export function LandingFooter() {
  return (
    <div className="landing-content-visibility bg-[linear-gradient(180deg,color-mix(in_srgb,var(--landing-bg)_0%,transparent),color-mix(in_srgb,var(--landing-ink)_18%,transparent)_14%,var(--landing-ink)_100%)] px-4 pt-20">
      <footer className="relative mx-auto w-full max-w-[1350px] overflow-hidden rounded-t-[2rem] border border-[color:color-mix(in_srgb,var(--landing-accent)_14%,var(--landing-line))] bg-[linear-gradient(180deg,color-mix(in_srgb,var(--landing-ink)_90%,black_10%),color-mix(in_srgb,var(--landing-ink)_96%,black_4%))] px-5 pt-8 text-[var(--landing-accent-foreground)] shadow-[0_-20px_80px_rgba(44,23,8,0.24)] sm:px-8 md:px-14 lg:px-24 lg:pt-12">
        <div className="grid grid-cols-1 gap-10 lg:grid-cols-6 lg:gap-12">
          <div className="space-y-6 lg:col-span-3">
            <Link href="/" className="inline-flex items-center gap-4">
              <span className="flex h-14 w-14 items-center justify-center rounded-full border border-[color:color-mix(in_srgb,var(--landing-accent)_34%,transparent)] bg-[color:color-mix(in_srgb,var(--landing-accent)_12%,transparent)] text-2xl text-[var(--landing-accent)] shadow-[inset_0_1px_0_color-mix(in_srgb,var(--landing-accent)_18%,transparent)]">
                ☥
              </span>
              <span>
                <span className="block text-xs font-semibold tracking-[0.38em] text-[color:color-mix(in_srgb,var(--landing-accent-foreground)_54%,var(--landing-accent)_46%)]">
                  NADER GORGE
                </span>
                <span className="mt-1 block text-xl font-black text-[var(--landing-accent-foreground)] md:text-2xl">
                  الأستاذ نادر جورج
                </span>
              </span>
            </Link>

            <p className="max-w-[34rem] text-sm leading-7 text-[color:color-mix(in_srgb,var(--landing-accent-foreground)_72%,transparent)] md:text-base">
              نظام تعليمي منظم لطلبة الثانوي يساعدهم يذاكروا بوضوح، يراجعوا أسرع، ويتابعوا
              تقدمهم خطوة بخطوة بدون تشتت.
            </p>

            <div className="flex flex-wrap gap-3">
              <Link
                href="/register"
                className="inline-flex min-h-11 items-center gap-2 rounded-full bg-[var(--landing-accent)] px-5 py-3 text-sm font-extrabold text-[var(--landing-accent-foreground)] shadow-[0_16px_36px_color-mix(in_srgb,var(--landing-accent)_34%,transparent)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
              >
                ابدأ التجربة
                <ArrowUpLeft className="h-4 w-4" />
              </Link>
              <Link
                href="/faq"
                className="inline-flex min-h-11 items-center rounded-full border border-[color:color-mix(in_srgb,var(--landing-accent)_18%,transparent)] bg-[color:color-mix(in_srgb,var(--landing-accent)_8%,transparent)] px-5 py-3 text-sm font-bold text-[var(--landing-accent-foreground)] transition hover:bg-[color:color-mix(in_srgb,var(--landing-accent)_14%,transparent)]"
              >
                اعرف النظام
              </Link>
            </div>

            <div className="flex flex-wrap gap-3 pt-1">
              {socialLinks.map(({ href, label, icon: Icon }) => (
                <a
                  key={label}
                  href={href}
                  aria-label={label}
                  className="inline-flex h-11 w-11 items-center justify-center rounded-full border border-[color:color-mix(in_srgb,var(--landing-accent)_18%,transparent)] bg-[color:color-mix(in_srgb,var(--landing-accent)_7%,transparent)] text-[color:color-mix(in_srgb,var(--landing-accent-foreground)_82%,transparent)] transition hover:-translate-y-0.5 hover:border-[color:color-mix(in_srgb,var(--landing-accent)_34%,transparent)] hover:text-[var(--landing-accent)]"
                >
                  <Icon className="h-4.5 w-4.5" />
                </a>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-2 gap-8 sm:grid-cols-3 lg:col-span-3 lg:gap-14">
            {footerSections.map((section) => (
              <div key={section.title} className="text-right">
                <h3 className="mb-4 text-sm font-black tracking-[0.14em] text-[var(--landing-accent-foreground)]">
                  {section.title}
                </h3>
                <ul className="space-y-3">
                  {section.links.map((link) => (
                    <li key={link.label}>
                      <Link
                        href={link.href}
                        className="text-sm font-semibold text-[color:color-mix(in_srgb,var(--landing-accent-foreground)_68%,transparent)] transition hover:text-[var(--landing-accent)]"
                      >
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </div>

        <div className="mt-12 flex flex-col gap-3 border-t border-[color:color-mix(in_srgb,var(--landing-accent)_12%,transparent)] pt-5 text-sm md:flex-row md:items-center md:justify-between">
          <p className="text-[color:color-mix(in_srgb,var(--landing-accent-foreground)_52%,transparent)]">
            © 2026 الأستاذ نادر جورج. جميع الحقوق محفوظة.
          </p>
          <p className="text-[color:color-mix(in_srgb,var(--landing-accent-foreground)_44%,transparent)]">
            تعلّم بخطة واضحة. ذاكر بتركيز. تابع تقدمك باستمرار.
          </p>
        </div>

        <div className="relative mt-6">
          <div className="pointer-events-none absolute inset-x-0 bottom-0 mx-auto h-44 w-full max-w-3xl rounded-full bg-[color:color-mix(in_srgb,var(--landing-accent)_52%,transparent)] blur-[150px]" />
          <p className="relative text-center text-[clamp(3.4rem,15vw,12rem)] font-black leading-[0.74] tracking-[0.08em] text-transparent [-webkit-text-stroke:1px_color-mix(in_srgb,var(--landing-accent)_34%,transparent)]">
            NADER
          </p>
        </div>
      </footer>
    </div>
  );
}
