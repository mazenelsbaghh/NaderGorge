'use client';

import Image from 'next/image';
import { ArrowUpLeft, GraduationCap } from 'lucide-react';
import Link from 'next/link';
import { useEffect, useState } from 'react';

import { heroHighlights } from './data';

type HeroSectionProps = {
  registeredStudentsCount: number;
  baselineStudentsCount: number;
};

function formatArabicNumber(value: number) {
  return new Intl.NumberFormat('ar-EG').format(value);
}

export function HeroSection({
  registeredStudentsCount,
  baselineStudentsCount,
}: HeroSectionProps) {
  const [isDark, setIsDark] = useState(false);
  const totalRegisteredStudents =
    baselineStudentsCount + registeredStudentsCount;

  useEffect(() => {
    const updateThemeState = () => {
      setIsDark(document.documentElement.classList.contains('dark'));
    };

    updateThemeState();
    window.addEventListener('admin-theme-mode-change', updateThemeState);
    window.addEventListener('storage', updateThemeState);

    return () => {
      window.removeEventListener('admin-theme-mode-change', updateThemeState);
      window.removeEventListener('storage', updateThemeState);
    };
  }, []);
  return (
    <section
      className="landing-hero relative min-h-screen overflow-hidden px-5 pb-9 pt-28 text-[var(--landing-ink)] md:px-12 md:pb-12 md:pt-32 lg:px-16"
    >
      {/* Background Image */}
      <Image
        src={isDark ? '/images/landing-hero-dark.webp' : '/images/landing-hero.webp'}
        alt="خلفية الصفحة الرئيسية"
        fill
        priority
        sizes="100vw"
        className="object-cover -z-10"
      />
      {/* Gradient Overlay */}
      <div
        className="absolute inset-0 -z-10"
        style={{
          backgroundImage: 'linear-gradient(90deg, var(--hero-overlay-start) 0%, var(--hero-overlay-mid) 32%, var(--hero-overlay-subtle) 58%, var(--hero-overlay-end) 100%)'
        }}
      />
      <div className="relative z-10 mx-auto grid min-h-[calc(100vh-9rem)] w-full max-w-[1440px] items-center lg:grid-cols-[0.72fr_1fr] lg:[direction:ltr]">
        <div className="max-w-xl text-right lg:[direction:rtl]">
          <h1 className="text-balance text-[clamp(2.3rem,4vw,4.2rem)] font-black leading-[1.22] tracking-normal text-[var(--landing-ink)]">
            <span className="block py-1">ابدأ رحلتك التعليمية</span>
            <span className="block py-1 text-[#0E8F8F]">
              خطوتك الأولى نحو التفوق
            </span>
          </h1>

          <p className="mt-5 max-w-[38rem] text-pretty text-base font-semibold leading-8 text-[var(--landing-muted)] md:text-lg">
            منصة تعليمية متكاملة تساعدك على تعلم كل مهارة، في أي وقت ومن أي
            مكان.
          </p>

          <div className="mt-8 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-end">
            <Link href="/register" className="landing-primary-button">
              <GraduationCap className="h-5 w-5" />
              ابدأ التعلم الآن
            </Link>
            <span className="text-sm font-bold text-[var(--landing-muted)]">
              +{formatArabicNumber(totalRegisteredStudents)} طالب داخل الرحلة
            </span>
          </div>

          <div className="mt-10 grid grid-cols-2 gap-4 sm:grid-cols-4">
            {heroHighlights.map(({ label, icon: Icon }) => (
              <div
                key={label}
                className="flex flex-col items-center gap-2 text-center"
              >
                <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-[#E7F6F6] dark:bg-teal-950/40 text-[#0E8F8F] hover:scale-105 transition-transform duration-200">
                  <Icon className="h-5 w-5" />
                </span>
                <span className="text-xs font-extrabold text-[var(--landing-ink)] md:text-sm">
                  {label}
                </span>
              </div>
            ))}
          </div>
        </div>

        <Link
          href="#courses"
          className="absolute bottom-8 left-1/2 hidden h-12 w-12 -translate-x-1/2 items-center justify-center rounded-full bg-[var(--landing-accent)] text-white transition hover:bg-[var(--landing-accent-strong)] lg:flex"
          aria-label="استعرض الدورات"
        >
          <ArrowUpLeft className="h-5 w-5" />
        </Link>
      </div>
    </section>
  );
}
