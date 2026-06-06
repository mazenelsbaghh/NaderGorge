'use client';

import { motion } from 'framer-motion';
import Link from 'next/link';
import Image from 'next/image';
import { BadgeCheck, GraduationCap, Users, Award, ArrowUpLeft } from 'lucide-react';

const stats = [
  { num: '+١٠', label: 'سنوات خبرة', icon: Award },
  { num: '+٥٠٠٠', label: 'طالب درّسهم', icon: Users },
  { num: '٣', label: 'مراحل دراسية', icon: GraduationCap },
  { num: '٩٥٪', label: 'نسبة النجاح', icon: BadgeCheck },
];

export default function AboutPage() {
  return (
    <div className="landing-page">
      <div className="landing-page__backdrop" />
      <div className="landing-page__texture" />

      <div className="relative z-10 mx-auto max-w-4xl px-6 pt-28 pb-16">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
          {/* Header */}
          <div className="mb-16 text-center">
            <div className="landing-chip mx-auto mb-5">
              <GraduationCap className="h-4 w-4" />
              <span>عن المنصة</span>
            </div>
            <h1 className="text-4xl font-black text-[var(--landing-ink)] md:text-5xl leading-tight">
              منصة مسار
            </h1>
            <p className="mx-auto mt-4 max-w-2xl text-lg text-[var(--landing-muted)]">
              تجربة تعليمية منظمة تساعد طلاب المرحلة الثانوية على التعلم والمتابعة والتفوق بخطوات واضحة.
            </p>
          </div>

          {/* Bio Card */}
          <div className="landing-panel mb-12 overflow-hidden rounded-[28px] p-8 md:p-12">
            <div className="flex flex-col gap-8 md:flex-row md:items-start">
              <div className="shrink-0">
                <div className="relative h-32 w-32 overflow-hidden rounded-[24px] shadow-[0_20px_60px_rgba(88,55,18,0.2)]">
                  <Image
                    src="/images/hero-pharaoh.png"
                    alt="منصة مسار"
                    fill
                    className="object-cover"
                  />
                </div>
              </div>
              <div className="space-y-4">
                <h2 className="text-2xl font-black text-[var(--landing-ink)]">فلسفة التعليم</h2>
                <p className="leading-8 text-[var(--landing-muted)]">
                  منصة مسار بتجمع بين المحتوى المنظم والتكنولوجيا الحديثة عشان توفر تجربة تعلم سلسة ومميزة لكل طالب. الهدف إن الطالب يلاقي الدرس، الواجب، الامتحان، والمتابعة في مكان واحد واضح.
                </p>
                <p className="leading-8 text-[var(--landing-muted)]">
                  المنصة مصممة عشان تقدم دروس فيديو عالية الجودة يقدر الطالب يرجعلها في أي وقت، مع امتحانات تصحيح تلقائي بتضمن الفهم قبل الانتقال. النظام ده أثبت فعاليته في تحسين نتائج الطلاب بشكل ملموس.
                </p>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="mb-12 grid grid-cols-2 gap-5 md:grid-cols-4">
            {stats.map((s, i) => {
              const Icon = s.icon;
              return (
                <motion.div
                  key={i}
                  initial={{ opacity: 0, y: 16 }}
                  whileInView={{ opacity: 1, y: 0 }}
                  viewport={{ once: true }}
                  transition={{ delay: i * 0.1 }}
                  className="landing-panel flex flex-col items-center rounded-[24px] p-6 text-center"
                >
                  <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--landing-card-strong)] text-[var(--landing-accent)]">
                    <Icon className="h-6 w-6" />
                  </div>
                  <div className="text-3xl font-black text-[var(--landing-accent)]">{s.num}</div>
                  <div className="mt-1 text-sm font-bold text-[var(--landing-muted)]">{s.label}</div>
                </motion.div>
              );
            })}
          </div>

          {/* CTA */}
          <div className="text-center">
            <Link
              href="/register"
              className="inline-flex items-center gap-2 rounded-full bg-[var(--landing-accent)] px-10 py-4 text-lg font-extrabold text-[var(--landing-accent-foreground)] shadow-[0_16px_40px_rgba(145,95,42,0.28)] transition hover:-translate-y-0.5 hover:bg-[var(--landing-accent-strong)]"
            >
              ابدأ مع منصة مسار
              <ArrowUpLeft className="h-5 w-5" />
            </Link>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
