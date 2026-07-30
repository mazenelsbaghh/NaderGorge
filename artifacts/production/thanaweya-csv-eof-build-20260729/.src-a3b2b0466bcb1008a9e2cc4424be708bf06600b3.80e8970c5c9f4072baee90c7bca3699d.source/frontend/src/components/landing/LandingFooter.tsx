'use client';

import {
  ArrowLeft,
  AtSign,
  GraduationCap,
  Globe,
  MessageCircle,
  Play,
  Send,
  Video,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { motion, useReducedMotion } from 'framer-motion';

import { educationTracks, finalCtaFeatures } from './data';

const quickLinks = [
  { label: 'الدورات', href: '#courses' },
  { label: 'المعلمون', href: '#teachers' },
  { label: 'الأسئلة الشائعة', href: '/faq' },
] as const;

const supportLinks = [
  { label: 'تواصل معنا', href: '#contact' },
  { label: 'سياسة الخصوصية', href: '/privacy' },
  { label: 'الشروط والأحكام', href: null },
] as const;

export function LandingFooter() {
  const prefersReducedMotion = useReducedMotion();

  return (
    <>
      <section
        id="courses"
        className="landing-section landing-section--courses mt-3 px-5 py-14 md:px-12 md:py-16 lg:px-16"
      >
        <div className="relative z-10 mx-auto max-w-[1180px]">
          <div className="text-center">
            <h2 className="text-3xl font-black text-[var(--landing-ink)] md:text-5xl">
              مساراتنا التعليمية
            </h2>
            <p className="mt-3 text-base font-bold text-[var(--landing-muted)] md:text-lg">
              اختر المسار المناسب لك وابدأ رحلتك نحو النجاح
            </p>
          </div>

          <div className="mt-9 grid gap-5 lg:grid-cols-2">
            {educationTracks.map(
              ({ title, description, icon: Icon, cta, href, tone }) => (
                <article
                  key={title}
                  className="landing-panel grid gap-6 p-6 text-right sm:grid-cols-[150px_1fr] sm:items-center"
                >
                  <div className={`flex aspect-square items-center justify-center rounded-xl text-[var(--landing-accent)] ${tone === 'teal' ? 'bg-[var(--landing-teal-soft)]' : 'bg-[var(--landing-navy-soft)] text-[var(--landing-ink)]'}`}>
                    <Icon className="h-20 w-20" />
                  </div>
                  <div>
                    <h3 className="text-2xl font-black text-[var(--landing-ink)]">
                      {title}
                    </h3>
                    <p className="mt-3 text-sm font-semibold leading-7 text-[var(--landing-muted)]">
                      {description}
                    </p>
                    <Link
                      href={href}
                      className={`mt-5 inline-flex min-h-12 items-center justify-center gap-2 rounded-lg px-5 text-sm font-black text-white transition hover:-translate-y-0.5 ${
                        tone === 'teal'
                          ? 'bg-[var(--landing-accent)] text-[var(--landing-accent-foreground)] hover:bg-[var(--landing-accent-strong)]'
                          : 'bg-[var(--primary)] text-[var(--primary-foreground)] hover:bg-[var(--landing-accent-strong)]'
                      }`}
                    >
                      {cta}
                      <ArrowLeft className="h-4 w-4" />
                    </Link>
                  </div>
                </article>
              )
            )}
          </div>
        </div>
      </section>

      <footer
        id="contact"
        className="mt-3 overflow-hidden rounded-[clamp(1rem,1.4vw,1.25rem)] bg-[#071832] text-white"
      >
        <section className="relative px-5 py-14 md:px-12 md:py-16 lg:px-16">
          <div className="footer-route-grid" aria-hidden="true" />
          <motion.div
            initial={prefersReducedMotion ? false : { opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true, margin: '-80px' }}
            transition={{ duration: 0.55, ease: [0.22, 1, 0.36, 1] }}
            className="relative z-10 mx-auto grid max-w-[1180px] overflow-hidden border border-white/15 bg-[#0A1D3D] lg:grid-cols-[1.1fr_0.9fr]"
          >
            <div className="p-7 text-center sm:p-10 lg:p-12 lg:text-right">
              <span className="inline-flex items-center gap-2 text-sm font-black text-[#9BE4E4]">
                <span className="h-2 w-2 rounded-full bg-[#D4A017]" />
                خطوتك التالية واضحة
              </span>
              <h2 className="mt-4 text-balance text-3xl font-black leading-tight md:text-5xl">
                ابدأ دراستك بثقة
              </h2>
              <p className="mt-4 max-w-xl text-base font-bold leading-8 text-white/82 md:text-lg lg:mr-0">
                سجّل الآن، اختر المادة والمعلم، وابدأ أول درس في وقتك.
              </p>
              <Link href="/register" className="landing-primary-button mt-7">
                <Play className="h-5 w-5 fill-current" />
                أنشئ حسابك مجانًا
              </Link>
            </div>

            <div className="relative flex min-h-80 flex-col justify-center overflow-hidden border-t border-white/15 bg-[#0C2850] p-7 sm:p-10 lg:border-t-0 lg:border-r lg:p-12">
              <div className="footer-step-line" aria-hidden="true" />
              <p className="relative text-sm font-black text-[#9BE4E4]">
                كيف تبدأ؟
              </p>
              <ol className="relative mt-7 space-y-6">
                {[
                  ['01', 'أنشئ حسابك'],
                  ['02', 'اختر مادّتك'],
                  ['03', 'ابدأ أول درس'],
                ].map(([number, label]) => (
                  <li key={number} className="flex items-center gap-4 text-right">
                    <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-[#38BDBD] text-xs font-black text-[#9BE4E4]">
                      {number}
                    </span>
                    <strong className="text-base font-black text-white">{label}</strong>
                  </li>
                ))}
              </ol>
            </div>
          </motion.div>

          <div className="relative z-10 mx-auto grid max-w-[1180px] grid-cols-2 border-x border-b border-white/15 sm:grid-cols-4">
            {finalCtaFeatures.map(({ label, detail, icon: Icon }) => (
              <div key={label} className="flex min-h-28 flex-col justify-center px-4 text-center not-last:border-l not-last:border-white/15">
                <Icon className="mx-auto h-5 w-5 text-[#9BE4E4]" aria-hidden="true" />
                <strong className="mt-2 text-sm font-black">{label}</strong>
                <span className="mt-1 text-xs font-bold text-white/70">{detail}</span>
              </div>
            ))}
          </div>
        </section>

        <div className="border-t border-white/10 px-5 py-8 md:px-12 lg:px-16">
          <div className="mx-auto grid max-w-[1180px] gap-8 md:grid-cols-4">
            <div className="text-right">
              <div className="relative flex h-16 w-16 items-center justify-center" aria-label="منصة مسار">
                <GraduationCap className="absolute h-8 w-8 text-[#9BE4E4]" aria-hidden="true" />
                <Image
                  src="/images/logo-mark-light.svg"
                  alt=""
                  width={64}
                  height={64}
                  className="relative h-16 w-16 object-contain"
                  unoptimized
                  onError={(event) => { event.currentTarget.style.display = 'none'; }}
                />
              </div>
              <p className="mt-3 text-sm font-semibold leading-7 text-white/72">
                خطواتك الأولى نحو التفوق
              </p>
            </div>

            <FooterLinks title="روابط سريعة" links={quickLinks} />
            <FooterLinks title="الدعم والمساعدة" links={supportLinks} />

            <div className="text-right">
              <h3 className="text-sm font-black">تابعنا</h3>
              <div
                className="mt-4 flex gap-3 md:justify-start"
                aria-hidden="true"
              >
                {[MessageCircle, AtSign, Send, Video, Globe].map(
                  (Icon, index) => (
                    <span
                      key={index}
                      className="flex h-10 w-10 items-center justify-center rounded-lg bg-white/10 text-white/70"
                    >
                      <Icon className="h-4 w-4" />
                    </span>
                  )
                )}
              </div>
              <p className="mt-3 text-xs font-semibold text-white/68">
                قنوات التواصل قريبًا
              </p>
            </div>
          </div>
        </div>
      </footer>
    </>
  );
}

type FooterLink = {
  label: string;
  href: string | null;
};

function FooterLinks({
  title,
  links,
}: {
  title: string;
  links: readonly FooterLink[];
}) {
  return (
    <div className="text-right">
      <h3 className="text-sm font-black">{title}</h3>
      <ul className="mt-4 space-y-2">
        {links.map(({ label, href }) => (
          <li key={label}>
            {href ? (
              <Link
                href={href}
                className="text-sm font-semibold text-white/68 transition hover:text-[#9BE4E4]"
              >
                {label}
              </Link>
            ) : (
              <span className="text-sm font-semibold text-white/48">
                {label}
              </span>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
