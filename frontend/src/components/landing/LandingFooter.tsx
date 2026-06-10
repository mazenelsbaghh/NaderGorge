"use client";

import { ArrowLeft, AtSign, Globe, MessageCircle, Play, Send, Video } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useEffect, useState } from "react";

import { educationTracks, finalCtaFeatures } from "./data";

const quickLinks = ["الدورات", "المعلمون", "الأسئلة الشائعة"] as const;
const supportLinks = ["تواصل معنا", "سياسة الخصوصية", "الشروط والأحكام"] as const;

export function LandingFooter() {
  const [isDark, setIsDark] = useState(false);

  useEffect(() => {
    const updateThemeState = () => {
      setIsDark(document.documentElement.classList.contains("dark"));
    };

    updateThemeState();
    window.addEventListener("admin-theme-mode-change", updateThemeState);
    window.addEventListener("storage", updateThemeState);

    return () => {
      window.removeEventListener("admin-theme-mode-change", updateThemeState);
      window.removeEventListener("storage", updateThemeState);
    };
  }, []);

  return (
    <>
      <section id="courses" className="landing-section mt-3 px-5 py-14 md:px-12 md:py-16 lg:px-16">
        <div className="relative z-10 mx-auto max-w-[1180px]">
          <div className="text-center">
            <h2 className="text-3xl font-black text-[var(--landing-ink)] md:text-5xl">مساراتنا التعليمية</h2>
            <p className="mt-3 text-base font-bold text-[var(--landing-muted)] md:text-lg">
              اختر المسار المناسب لك وابدأ رحلتك نحو النجاح
            </p>
          </div>

          <div className="mt-9 grid gap-5 lg:grid-cols-2">
            {educationTracks.map(({ title, description, icon: Icon, cta, href, tone }) => (
              <article key={title} className="landing-panel grid gap-6 p-6 text-right sm:grid-cols-[150px_1fr] sm:items-center">
                <div className="flex aspect-square items-center justify-center rounded-xl bg-[#E7F6F6] dark:bg-teal-950/40 text-[#0E8F8F]">
                  <Icon className="h-20 w-20" />
                </div>
                <div>
                  <h3 className="text-2xl font-black text-[var(--landing-ink)]">{title}</h3>
                  <p className="mt-3 text-sm font-semibold leading-7 text-[var(--landing-muted)]">{description}</p>
                  <Link
                    href={href}
                    className={`mt-5 inline-flex min-h-12 items-center justify-center gap-2 rounded-lg px-5 text-sm font-black text-white transition hover:-translate-y-0.5 ${
                      tone === "teal"
                        ? "bg-[#0E8F8F] hover:bg-[#0a6d72]"
                        : "bg-[var(--landing-accent)] hover:bg-[var(--landing-accent-strong)]"
                    }`}
                  >
                    {cta}
                    <ArrowLeft className="h-4 w-4" />
                  </Link>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <footer id="contact" className="mt-3 overflow-hidden rounded-[clamp(1rem,1.4vw,1.25rem)] bg-[#071832] text-white">
        <section className="relative px-5 py-14 md:px-12 md:py-16 lg:px-16">
          <div className="absolute inset-0 opacity-50">
            <div className="absolute left-10 top-10 h-40 w-40 rounded-full bg-[#0E8F8F] blur-[90px]" />
            <div className="absolute right-20 bottom-10 h-52 w-52 rounded-full bg-[#123A73] blur-[110px]" />
          </div>

          <div className="relative z-10 mx-auto grid max-w-[1180px] gap-10 lg:grid-cols-[1fr_0.95fr] lg:items-center">
            <div className="text-center lg:text-right">
              <h2 className="text-balance text-3xl font-black leading-tight md:text-5xl">
                مستقبلك يبدأ من هنا
              </h2>
              <p className="mt-4 text-lg font-bold leading-8 text-white/82">
                انضم لآلاف الطلاب وابدأ رحلتك نحو التفوق
              </p>

              <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                {finalCtaFeatures.map(({ label, detail, icon: Icon }) => (
                  <div key={label} className="text-center">
                    <Icon className="mx-auto h-7 w-7 text-[#9BE4E4]" />
                    <strong className="mt-3 block text-sm font-black">{label}</strong>
                    <span className="mt-1 block text-xs font-bold text-white/70">{detail}</span>
                  </div>
                ))}
              </div>

              <Link href="/register" className="landing-primary-button mt-9">
                <Play className="h-5 w-5 fill-current" />
                ابدأ الآن مجانًا
              </Link>
            </div>

            <div className="relative mx-auto aspect-[1.25] w-full max-w-lg">
              <Image
                src={isDark ? "/images/landing-hero-dark.webp" : "/images/landing-hero.webp"}
                alt="منصة مسار على جهاز تعليمي"
                fill
                sizes="(max-width: 1024px) 92vw, 42vw"
                className="object-contain"
              />
            </div>
          </div>
        </section>

        <div className="border-t border-white/10 px-5 py-8 md:px-12 lg:px-16">
          <div className="mx-auto grid max-w-[1180px] gap-8 md:grid-cols-4">
            <div className="text-right">
              <Image
                src="/images/logo-mark-light.svg"
                alt="منصة مسار"
                width={64}
                height={64}
                className="h-16 w-auto object-contain"
                style={{ width: "auto", height: "auto" }}
              />
              <p className="mt-3 text-sm font-semibold leading-7 text-white/72">
                خطواتك الأولى نحو التفوق
              </p>
            </div>

            <FooterLinks title="روابط سريعة" links={quickLinks} />
            <FooterLinks title="الدعم والمساعدة" links={supportLinks} />

            <div className="text-right">
              <h3 className="text-sm font-black">تابعنا</h3>
              <div className="mt-4 flex gap-3 md:justify-start">
                {[MessageCircle, AtSign, Send, Video, Globe].map((Icon, index) => (
                  <a
                    key={index}
                    href="#"
                    aria-label="تابع منصة مسار"
                    className="flex h-10 w-10 items-center justify-center rounded-lg bg-white/10 text-white transition hover:bg-[#0E8F8F]"
                  >
                    <Icon className="h-4 w-4" />
                  </a>
                ))}
              </div>
            </div>
          </div>
        </div>
      </footer>
    </>
  );
}

function FooterLinks({ title, links }: { title: string; links: readonly string[] }) {
  return (
    <div className="text-right">
      <h3 className="text-sm font-black">{title}</h3>
      <ul className="mt-4 space-y-2">
        {links.map((link) => (
          <li key={link}>
            <a href="#" className="text-sm font-semibold text-white/68 transition hover:text-[#9BE4E4]">
              {link}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}
