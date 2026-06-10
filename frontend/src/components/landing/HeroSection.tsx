"use client";

import { ArrowUpLeft, GraduationCap } from "lucide-react";
import Link from "next/link";
import { motion, useScroll, useTransform, useReducedMotion, useMotionValue, useSpring, useMotionTemplate } from "framer-motion";
import { useEffect, useRef, useState } from "react";

import { heroHighlights } from "./data";

type HeroSectionProps = {
  registeredStudentsCount: number;
  baselineStudentsCount: number;
};

function formatArabicNumber(value: number) {
  return new Intl.NumberFormat("ar-EG").format(value);
}

export function HeroSection({
  registeredStudentsCount,
  baselineStudentsCount,
}: HeroSectionProps) {
  const [isDark, setIsDark] = useState(false);
  const totalRegisteredStudents = baselineStudentsCount + registeredStudentsCount;
  const sectionRef = useRef<HTMLDivElement>(null);

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
  
  const prefersReducedMotion = useReducedMotion();
  const { scrollYProgress } = useScroll({
    target: sectionRef,
    offset: ["start start", "end start"],
  });

  const transformBgY = useTransform(scrollYProgress, [0, 1], ["50%", "70%"]);
  const transformContentY = useTransform(scrollYProgress, [0, 1], [0, 70]);
  const transformContentOpacity = useTransform(scrollYProgress, [0, 0.75], [1, 0]);

  const bgY = prefersReducedMotion ? "50%" : transformBgY;
  const contentY = prefersReducedMotion ? 0 : transformContentY;
  const contentOpacity = prefersReducedMotion ? 1 : transformContentOpacity;

  // Spotlight mouse tracking with smooth spring physics
  const mouseX = useMotionValue(0);
  const mouseY = useMotionValue(0);
  const spotlightX = useSpring(mouseX, { stiffness: 120, damping: 22 });
  const spotlightY = useSpring(mouseY, { stiffness: 120, damping: 22 });

  const handleMouseMove = (e: React.MouseEvent) => {
    if (prefersReducedMotion) return;
    const rect = sectionRef.current?.getBoundingClientRect();
    if (!rect) return;
    mouseX.set(e.clientX - rect.left);
    mouseY.set(e.clientY - rect.top);
  };

  // Kinetic Typography definitions
  const titleText = "ابدأ رحلتك التعليمية";
  const subText = "خطوتك الأولى نحو التفوق";
  
  const titleWords = titleText.split(" ");
  const subWords = subText.split(" ");

  const titleContainerVariants = {
    hidden: {},
    visible: {
      transition: {
        staggerChildren: prefersReducedMotion ? 0 : 0.08,
      },
    },
  };

  const wordVariants = {
    hidden: { 
      opacity: prefersReducedMotion ? 1 : 0,
      y: prefersReducedMotion ? 0 : "110%" 
    },
    visible: {
      opacity: 1,
      y: 0,
      transition: {
        type: "spring" as const,
        stiffness: 110,
        damping: 14,
      },
    },
  };

  return (
    <motion.section
      ref={sectionRef}
      onMouseMove={handleMouseMove}
      className="landing-hero relative min-h-screen overflow-hidden px-5 pb-9 pt-28 text-[var(--landing-ink)] md:px-12 md:pb-12 md:pt-32 lg:px-16"
      style={{
        backgroundImage: `linear-gradient(90deg, var(--hero-overlay-start) 0%, var(--hero-overlay-mid) 32%, var(--hero-overlay-subtle) 58%, var(--hero-overlay-end) 100%), url('${
          isDark ? "/images/landing-hero-dark.webp" : "/images/landing-hero.webp"
        }')`,
        backgroundSize: "cover",
        backgroundPosition: "center center",
        backgroundPositionY: bgY,
        backgroundRepeat: "no-repeat",
      }}
    >
      {/* Interactive mouse-glow spotlight */}
      {!prefersReducedMotion && (
        <motion.div
          className="pointer-events-none absolute inset-0 select-none"
          style={{
            background: useMotionTemplate`radial-gradient(350px circle at ${spotlightX}px ${spotlightY}px, rgba(14, 143, 143, 0.09) 0%, rgba(212, 160, 23, 0.04) 50%, transparent 100%)`,
          }}
        />
      )}

      <div className="relative z-10 mx-auto grid min-h-[calc(100vh-9rem)] w-full max-w-[1440px] items-center lg:grid-cols-[0.72fr_1fr] lg:[direction:ltr]">
        <motion.div
          style={{ y: contentY, opacity: contentOpacity }}
          className="max-w-xl text-right lg:[direction:rtl]"
        >
          {/* Kinetic Typography Heading */}
          <motion.h1
            variants={titleContainerVariants}
            initial="hidden"
            animate="visible"
            className="text-balance text-[clamp(2.3rem,4vw,4.2rem)] font-black leading-[1.22] tracking-normal text-[var(--landing-ink)]"
          >
            <span className="block overflow-hidden py-1">
              {titleWords.map((word, index) => (
                <motion.span
                  key={index}
                  variants={wordVariants}
                  className="inline-block me-3 origin-bottom"
                >
                  {word}
                </motion.span>
              ))}
            </span>
            <span className="block overflow-hidden py-1 text-[#0E8F8F]">
              {subWords.map((word, index) => (
                <motion.span
                  key={index}
                  variants={wordVariants}
                  className="inline-block me-3 origin-bottom"
                >
                  {word}
                </motion.span>
              ))}
            </span>
          </motion.h1>

          <motion.p
            initial={prefersReducedMotion ? { opacity: 1 } : { opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.45, duration: 0.6 }}
            className="mt-5 max-w-[38rem] text-pretty text-base font-semibold leading-8 text-[var(--landing-muted)] md:text-lg"
          >
            منصة تعليمية متكاملة تساعدك على تعلم كل مهارة، في أي وقت ومن أي مكان.
          </motion.p>

          <motion.div
            initial={prefersReducedMotion ? { opacity: 1 } : { opacity: 0, y: 15 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.55, duration: 0.6 }}
            className="mt-8 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-end"
          >
            <Link href="/register" className="landing-primary-button">
              <GraduationCap className="h-5 w-5" />
              ابدأ التعلم الآن
            </Link>
            <span className="text-sm font-bold text-[var(--landing-muted)]">
              +{formatArabicNumber(totalRegisteredStudents)} طالب داخل الرحلة
            </span>
          </motion.div>

          <motion.div
            initial={prefersReducedMotion ? { opacity: 1 } : { opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.7, duration: 0.6 }}
            className="mt-10 grid grid-cols-2 gap-4 sm:grid-cols-4"
          >
            {heroHighlights.map(({ label, icon: Icon }) => (
              <div key={label} className="flex flex-col items-center gap-2 text-center">
                <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-[#E7F6F6] dark:bg-teal-950/40 text-[#0E8F8F] hover:scale-105 transition-transform duration-200">
                  <Icon className="h-5 w-5" />
                </span>
                <span className="text-xs font-extrabold text-[var(--landing-ink)] md:text-sm">{label}</span>
              </div>
            ))}
          </motion.div>
        </motion.div>

        <Link
          href="#courses"
          className="absolute bottom-8 left-1/2 hidden h-12 w-12 -translate-x-1/2 items-center justify-center rounded-full bg-[var(--landing-accent)] text-white transition hover:bg-[var(--landing-accent-strong)] lg:flex"
          aria-label="استعرض الدورات"
        >
          <ArrowUpLeft className="h-5 w-5" />
        </Link>
      </div>
    </motion.section>
  );
}

