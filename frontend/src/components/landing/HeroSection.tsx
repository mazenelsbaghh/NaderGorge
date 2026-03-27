import Link from "next/link";
import { ArrowUpLeft, BadgeCheck, PlayCircle } from "lucide-react";
import Image from "next/image";

import { heroStats } from "./data";

export function HeroSection() {
  return (
    <section className="relative -mt-28 min-h-screen overflow-hidden px-4 pt-36 pb-16 md:-mt-32 md:px-0 md:pt-40 md:pb-24">
      {/* Full-section Egyptian wall background */}
      <div
        className="absolute inset-0 bg-[url('/images/egyptian-wall.png')] bg-cover bg-center"
        style={{ backgroundSize: "cover", backgroundPosition: "center" }}
      />
      {/* Soft overlay so text stays readable */}
      <div className="absolute inset-0 bg-[var(--landing-bg)]/60" />

      <div className="relative z-10 mx-auto grid w-[min(1280px,95vw)] items-center gap-8 lg:min-h-screen lg:[direction:ltr] lg:grid-cols-[1.08fr_0.92fr]">
        <div className="order-2 flex justify-center lg:order-1 lg:justify-start">
          <div className="relative aspect-[4/5] w-full max-w-[740px] overflow-visible">
            <Image
              src="/images/hero-pharaoh.png"
              alt="الأستاذ نادر جورج بالزي الفرعوني"
              fill
              priority
              className="scale-[1.06] object-contain object-bottom drop-shadow-[0_40px_90px_rgba(88,55,18,0.30)]"
            />
          </div>
        </div>

        <div className="order-1 space-y-8 text-right lg:order-2 lg:[direction:rtl]">
          <div className="landing-chip w-fit">
            <BadgeCheck className="h-4 w-4" />
            <span>منصة تعليمية بروح فرعونية معاصرة</span>
          </div>

          <div className="space-y-5">
            <h1 className="max-w-3xl text-4xl font-black leading-[1.04] tracking-tight text-[var(--landing-ink)] md:text-6xl lg:text-[6.2rem]">
              تعلّم التاريخ
              <span className="block text-[var(--landing-accent)]">بروح الحضارة</span>
            </h1>
            <p className="max-w-2xl text-base leading-8 text-[var(--landing-muted)] md:text-lg lg:text-[1.32rem] lg:leading-9">
              منصة تعليمية بهوية مصرية أصيلة، مبنية بأيدي معلمين خبراء.
              دروس فيديو، اختبارات تفاعلية، ومتابعة دقيقة لتقدمك الدراسي.
            </p>
          </div>

          <div className="flex flex-col items-stretch gap-4 sm:flex-row sm:items-center sm:justify-start">
            <Link
              href="/register"
              className="inline-flex items-center justify-center gap-2 rounded-full bg-[var(--landing-accent)] px-7 py-4 text-base font-extrabold text-[var(--landing-accent-foreground)] shadow-[0_16px_40px_rgba(145,95,42,0.28)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
            >
              ابدأ الجلسة التجريبية
              <ArrowUpLeft className="h-4 w-4" />
            </Link>
            <a
              href="#features"
              className="inline-flex items-center justify-center gap-2 rounded-full border border-[var(--landing-line)] bg-[color:rgba(255,250,240,0.82)] px-7 py-4 text-base font-bold text-[var(--landing-ink)] backdrop-blur-sm transition hover:bg-[var(--landing-card)]"
            >
              شاهد المميزات
              <PlayCircle className="h-5 w-5" />
            </a>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            {heroStats.map((stat) => (
              <div key={stat.label} className="landing-panel rounded-[28px] px-5 py-5">
                <p className="text-3xl font-black text-[var(--landing-accent)]">{stat.value}</p>
                <p className="mt-2 text-sm font-semibold text-[var(--landing-muted)]">
                  {stat.label}
                </p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
