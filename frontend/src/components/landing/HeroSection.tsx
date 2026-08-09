'use client';

import { ArrowDown, GraduationCap } from 'lucide-react';
import Link from 'next/link';

import { heroHighlights } from './data';

type HeroSectionProps = {
  registeredStudentsCount: number;
  baselineStudentsCount: number;
};

function formatStudentCount(value: number) {
  return new Intl.NumberFormat('en-US').format(value);
}

export function HeroSection({
  registeredStudentsCount,
  baselineStudentsCount,
}: HeroSectionProps) {
  const safeRegisteredStudentsCount = Math.max(0, registeredStudentsCount);
  const totalStudentsCount = baselineStudentsCount + safeRegisteredStudentsCount;
  const studentProofLabel = `${formatStudentCount(totalStudentsCount)} طالب مسجل`;

  return (
    <section
      className="landing-hero relative overflow-hidden px-4 pb-10 pt-24 text-[var(--landing-ink)] sm:px-5 md:min-h-[78svh] md:px-12 md:pb-12 md:pt-28 lg:px-16"
    >
      <div
        aria-hidden="true"
        className="landing-hero-art absolute inset-0 -z-10 hidden bg-cover bg-[center_68%] md:block lg:bg-center"
      />
      {/* Gradient Overlay */}
      <div
        className="absolute inset-0 -z-10"
        style={{
          backgroundImage: 'linear-gradient(90deg, var(--hero-overlay-start) 0%, var(--hero-overlay-mid) 32%, var(--hero-overlay-subtle) 58%, var(--hero-overlay-end) 100%)'
        }}
      />
      <div className="relative z-10 mx-auto grid min-h-0 w-full max-w-[1440px] items-start py-6 md:min-h-[calc(78svh-8rem)] lg:items-center lg:py-0 lg:grid-cols-[0.72fr_1fr] lg:[direction:ltr]">
        <div className="mx-auto w-full max-w-xl text-center lg:mx-0 lg:text-right lg:[direction:rtl]">
          <span className="landing-journey-marker">
            مسارك واضح من أول درس إلى هدفك
          </span>
          <h1 className="text-balance text-[clamp(2.05rem,9vw,4.2rem)] font-black leading-[1.18] tracking-normal text-[var(--landing-ink)]">
            <span className="mt-3 block py-1">ابدأ رحلتك التعليمية</span>
            <span className="block py-1 text-[var(--landing-accent)]">
              خطوتك الأولى نحو التفوق
            </span>
          </h1>

          <p className="mx-auto mt-5 max-w-[22rem] text-pretty text-base font-semibold leading-7 text-[var(--landing-muted)] sm:max-w-[38rem] md:text-lg md:leading-8 lg:mx-0">
            منصة تعليمية متكاملة تساعدك على إتقان موادك، في أي وقت ومن أي مكان.
          </p>

          <div className="mx-auto mt-7 flex w-full max-w-sm flex-col gap-3 sm:mt-8 sm:max-w-none sm:flex-row sm:items-center sm:justify-center lg:justify-end">
            <Link href="/register" className="landing-primary-button w-full sm:w-auto">
              <GraduationCap className="h-5 w-5" />
              ابدأ التعلم الآن
            </Link>
            <span
              dir="rtl"
              aria-label={studentProofLabel}
              className="landing-student-proof"
            >
              <span className="landing-student-proof__baseline">
                {formatStudentCount(totalStudentsCount)}
              </span>
              <span className="landing-student-proof__label">طالب</span>
            </span>
          </div>

          <div
            data-hero-highlights
            className="mx-auto mt-7 grid max-w-sm grid-cols-2 gap-x-4 gap-y-4 sm:mt-8 sm:max-w-none sm:grid-cols-4 sm:gap-4"
          >
            {heroHighlights.map(({ label, icon: Icon }) => (
              <div
                key={label}
                className="flex flex-col items-center gap-2 text-center"
              >
                <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-[var(--landing-teal-soft)] text-[var(--landing-accent)]">
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
          <ArrowDown className="h-5 w-5" />
        </Link>
      </div>
    </section>
  );
}
