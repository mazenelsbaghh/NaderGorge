'use client';

import { ArrowUpLeft, BadgeCheck } from "lucide-react";
import Image from "next/image";
import { motion } from "framer-motion";
import { useAdminTheme } from "@/components/admin/useAdminTheme";
import { FloatingLines } from "@/components/ui/floating-lines";
import { MorphingText } from "@/components/ui/morphing-text";
import { InteractiveHoverButton } from "@/components/ui/interactive-hover-button";

export function HeroSection() {
  const { isDark } = useAdminTheme();

  const containerVariants = {
    hidden: { opacity: 0 },
    visible: {
      opacity: 1,
      transition: { staggerChildren: 0.1, delayChildren: 0.1 }
    }
  };

  const itemVariants = {
    hidden: { opacity: 0, y: 20 },
    visible: { opacity: 1, y: 0, transition: { duration: 0.6, ease: [0.25, 1, 0.5, 1] as const } }
  };

  const imageVariants = {
    hidden: { opacity: 0, scale: 0.95, y: 20 },
    visible: { opacity: 1, scale: 1, y: 0, transition: { duration: 0.8, ease: [0.25, 1, 0.5, 1] as const } }
  };

  return (
    <section className="relative -mt-28 min-h-screen overflow-hidden px-4 pt-36 pb-14 md:-mt-32 md:px-0 md:pt-40 md:pb-18 lg:pb-8">


      {/* Floating lines background */}
      <div className="absolute inset-0 z-0 pointer-events-none opacity-60">
        <FloatingLines
          enabledWaves={["top", "middle", "bottom"]}
          lineCount={5}
          lineDistance={5}
          bendRadius={5}
          bendStrength={-0.5}
          interactive={true}
          parallax={true}
          animationSpeed={0.3}
          linesGradient={
            isDark
              ? ["#c5a059", "#8f6b2f", "#f4f1e7"] // Dark mode: bright gold, strong gold, light text color
              : ["#5d4300", "#775a19", "#e8c176"] // Light mode: dark gold, strong gold, footer gold
          }
        />
      </div>

      <div className="relative z-10 mx-auto grid w-[min(1320px,95vw)] items-center gap-8 lg:min-h-[calc(100vh-2rem)] lg:items-center lg:[direction:ltr] lg:grid-cols-[1.14fr_0.86fr]">
        <div className="order-2 flex justify-center lg:order-1 lg:justify-start lg:self-end">
          <motion.div 
            initial="hidden" animate="visible" variants={imageVariants} 
            className="relative aspect-[4/5] w-full max-w-[740px] overflow-visible sm:max-w-[780px] lg:max-w-[860px] lg:translate-y-8 xl:max-w-[900px]"
          >
            <Image
              src="/images/hero-pharaoh.png"
              alt="الأستاذ نادر جورج بالزي الفرعوني"
              fill
              sizes="(max-width: 768px) 100vw, 50vw"
              priority
              className="scale-[1.1] object-contain object-bottom drop-shadow-[0_42px_100px_rgba(88,55,18,0.30)] lg:scale-[1.16] xl:scale-[1.18]"
            />
          </motion.div>
        </div>

        <motion.div 
          initial="hidden" animate="visible" variants={containerVariants} 
          className="order-1 space-y-8 text-right lg:order-2 lg:space-y-7 lg:[direction:rtl]"
        >
          <motion.div variants={itemVariants} className="landing-chip w-fit">
            <BadgeCheck className="h-4 w-4" />
            <span>منصة تعليمية بروح فرعونية معاصرة</span>
          </motion.div>

          <motion.div variants={itemVariants} className="space-y-4 lg:space-y-6">
            <h1 className="max-w-3xl flex flex-col text-right font-black tracking-tight" dir="rtl">
              <MorphingText texts={["شـــــيـــخ", "نـــــــــادر"]} className="text-[var(--landing-ink)] !w-full !max-w-none text-right lg:!h-[1.1em]" />
              <MorphingText texts={["المـــتحف", "جـــــــورج"]} className="text-[var(--landing-accent)] !w-full !max-w-none text-right lg:!h-[1.1em]" />
            </h1>
            <p className="max-w-2xl text-base leading-8 text-[var(--landing-muted)] md:text-lg lg:text-[1.32rem] lg:leading-9">
              بيئة رقمية متكاملة لضمان التفوق وتتبع الأداء خطوة بخطوة عبر محتوى مشروح بعناية، تقييمات ذكية، ومتابعة مستمرة.
            </p>
          </motion.div>

          <motion.div variants={itemVariants} className="flex flex-col items-stretch sm:items-start pt-6">
            <InteractiveHoverButton
              href="/register"
              tabIndex={-1}
              className="h-[58px] px-8 text-base shadow-[0_16px_40px_rgba(145,95,42,0.15)]"
              icon={<ArrowUpLeft className="h-5 w-5" />}
            >
              ابدأ الجلسة التجريبية
            </InteractiveHoverButton>
          </motion.div>
        </motion.div>
      </div>
    </section>
  );
}
