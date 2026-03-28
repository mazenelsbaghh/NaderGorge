'use client';

import { motion } from "framer-motion";
import { Quote } from "lucide-react";

import { cn } from "@/lib/utils";
import { AnimatedList } from "@/components/ui/animated-list";
import { testimonials } from "./data";

export function TestimonialsSection() {
  const sectionVariants = {
    hidden: { opacity: 0, y: 30 },
    visible: {
      opacity: 1,
      y: 0,
      transition: { duration: 0.45, ease: [0.25, 1, 0.5, 1] as const, staggerChildren: 0.05 },
    },
  };

  const itemVariants = {
    hidden: { opacity: 0, y: 18 },
    visible: {
      opacity: 1,
      y: 0,
      transition: { duration: 0.32, ease: [0.25, 1, 0.5, 1] as const },
    },
  };

  return (
    <section id="testimonials" className="landing-content-visibility px-4 py-24 md:px-0">
      <motion.div
        initial="hidden"
        whileInView="visible"
        viewport={{ once: true, margin: "-80px" }}
        variants={sectionVariants}
        className="mx-auto flex w-[min(1080px,92vw)] flex-col items-center gap-12"
      >
        <motion.div
          variants={itemVariants}
          className="mx-auto flex max-w-[780px] flex-col items-center gap-5 text-center"
        >
          <span className="landing-chip">آراء الطلاب فينا</span>
          <div className="space-y-4">
            <h2 className="text-3xl font-black tracking-tight text-[var(--landing-ink)] md:text-5xl">
              كلام حقيقي من الطلبة عن التجربة داخل المنصة
            </h2>
            <p className="mx-auto max-w-[720px] text-base leading-8 text-[var(--landing-muted)] md:text-lg">
              انطباعات مباشرة من الطلاب عن الشرح، الاختبارات، وسهولة متابعة الرحلة التعليمية خطوة بخطوة.
            </p>
          </div>
        </motion.div>

        <motion.div
          variants={itemVariants}
          className="relative mx-auto h-[860px] w-full max-w-[920px] overflow-hidden rounded-[44px] bg-[linear-gradient(180deg,color-mix(in_srgb,var(--landing-card)_52%,transparent),color-mix(in_srgb,var(--landing-bg-soft)_94%,transparent))] px-5 py-6 shadow-[0_24px_80px_rgba(88,55,18,0.10)]"
        >
          <div className="pointer-events-none absolute inset-x-0 top-0 z-10 h-20 bg-gradient-to-b from-[var(--landing-bg-soft)] to-transparent" />
          <div className="pointer-events-none absolute inset-x-0 bottom-0 z-10 h-24 bg-gradient-to-t from-[var(--landing-bg-soft)] to-transparent" />
          <AnimatedList
            delay={900}
            className="mx-auto w-full max-w-[860px] items-stretch gap-5 px-3 py-3 md:px-6 md:py-5"
          >
            {testimonials.map((review) => (
              <ReviewCard key={review.name} {...review} />
            ))}
          </AnimatedList>
        </motion.div>
      </motion.div>
    </section>
  );
}

type ReviewCardProps = {
  avatar: string;
  name: string;
  role: string;
  quote: string;
};

function ReviewCard({ avatar, name, role, quote }: ReviewCardProps) {
  return (
    <figure
      className={cn(
        "landing-panel relative h-fit w-full overflow-hidden rounded-[34px] border border-[var(--landing-line-strong)] bg-[linear-gradient(180deg,color-mix(in_srgb,var(--landing-card-strong)_86%,transparent),color-mix(in_srgb,var(--landing-card)_98%,transparent))] p-7 text-right shadow-[0_18px_42px_rgba(88,55,18,0.08)]"
      )}
    >
      <div className="flex items-center gap-4">
        <img className="h-[56px] w-[56px] rounded-full object-cover ring-2 ring-[var(--landing-line)]" width="56" height="56" alt={name} src={avatar} loading="lazy" decoding="async" />
        <div className="min-w-0 flex-1">
          <figcaption className="truncate text-[1.15rem] font-black text-[var(--landing-ink)]">
            {name}
          </figcaption>
          <p className="truncate text-[0.98rem] font-semibold text-[var(--landing-muted)]">{role}</p>
        </div>
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--landing-card-strong)] text-[var(--landing-accent)]">
          <Quote className="h-5 w-5" />
        </div>
      </div>
      <blockquote className="mt-5 text-[1.02rem] leading-8 text-[var(--landing-muted)]">
        &ldquo;{quote}&rdquo;
      </blockquote>
    </figure>
  );
}
