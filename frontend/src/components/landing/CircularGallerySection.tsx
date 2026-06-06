"use client";

import { ArrowLeft, Star } from "lucide-react";
import Image from "next/image";
import Link from "next/link";
import { useState, useEffect } from "react";
import { motion, useReducedMotion, useMotionValue, useTransform, AnimatePresence, animate } from "framer-motion";

import { platformStats, teachers, topStudents } from "./data";

const medalStyles: Record<number, string> = {
  1: "bg-[#D4A017] text-white",
  2: "bg-[#9AA6B2] text-white",
  3: "bg-[#B87333] text-white",
};

export function CircularGallerySection() {
  const prefersReducedMotion = useReducedMotion();
  const [currentIndex, setCurrentIndex] = useState(0);
  const [swipeDirection, setSwipeDirection] = useState<"left" | "right">("left");
  const [isHovered, setIsHovered] = useState(false);

  // Motion values for swiping active card
  const dragX = useMotionValue(0);
  const dragRotate = useTransform(dragX, [-180, 180], [-18, 18]);
  const dragOpacity = useTransform(dragX, [-150, -100, 0, 100, 150], [0.6, 1, 1, 1, 0.6]);

  const handleDragEnd = (event: any, info: any) => {
    const swipeThreshold = 80;
    if (info.offset.x < -swipeThreshold) {
      setSwipeDirection("left");
      setCurrentIndex((prev) => (prev + 1) % topStudents.length);
    } else if (info.offset.x > swipeThreshold) {
      setSwipeDirection("right");
      setCurrentIndex((prev) => (prev - 1 + topStudents.length) % topStudents.length);
    }
    dragX.set(0);
  };

  // Auto-swipe cycle timer when not hovered
  useEffect(() => {
    if (prefersReducedMotion || isHovered) return;
    
    let timeoutId: NodeJS.Timeout;
    let animControls: { stop: () => void } | null = null;

    const runAutoSwipe = () => {
      setSwipeDirection("left");
      
      // Animate dragX to simulate dragging with the mouse
      animControls = animate(dragX, -100, {
        type: "spring",
        stiffness: 100,
        damping: 15,
      });

      // After the simulated drag animation is complete, trigger the swipe
      timeoutId = setTimeout(() => {
        setCurrentIndex((prev) => (prev + 1) % topStudents.length);
        dragX.set(0);
        // Schedule the next cycle
        timeoutId = setTimeout(runAutoSwipe, 3000);
      }, 500);
    };

    // Initial delay before first swipe
    timeoutId = setTimeout(runAutoSwipe, 3000);

    return () => {
      clearTimeout(timeoutId);
      if (animControls) {
        animControls.stop();
      }
      // Ensure dragX is reset if we interrupt the animation
      dragX.set(0);
    };
  }, [prefersReducedMotion, isHovered, dragX]);

  const containerVariants = {
    hidden: {},
    visible: {
      transition: {
        staggerChildren: prefersReducedMotion ? 0 : 0.08,
      },
    },
  };

  const cardVariants = {
    hidden: {
      opacity: prefersReducedMotion ? 1 : 0,
      y: prefersReducedMotion ? 0 : 20,
    },
    visible: {
      opacity: 1,
      y: 0,
      transition: {
        duration: 0.6,
        ease: [0.25, 1, 0.5, 1] as const,
      },
    },
  };

  const titleVariants = {
    hidden: {
      opacity: prefersReducedMotion ? 1 : 0,
      y: prefersReducedMotion ? 0 : 15,
    },
    visible: {
      opacity: 1,
      y: 0,
      transition: {
        duration: 0.55,
        ease: [0.25, 1, 0.5, 1] as const,
      },
    },
  };

  const nextIndex1 = (currentIndex + 1) % topStudents.length;
  const nextIndex2 = (currentIndex + 2) % topStudents.length;

  const studentTop = topStudents[currentIndex];
  const student1 = topStudents[nextIndex1];
  const student2 = topStudents[nextIndex2];

  return (
    <>
      <section id="about-platform" className="landing-section mt-3 px-5 py-14 md:px-12 md:py-18 lg:px-16">
        <div className="relative z-10 mx-auto max-w-[1180px] text-center">
          <motion.div
            initial="hidden"
            whileInView="visible"
            viewport={{ once: true, margin: "-80px" }}
            variants={titleVariants}
          >
            <h2 className="text-balance text-3xl font-black leading-tight text-[var(--landing-ink)] md:text-5xl">
              الأوائل مع مسار
            </h2>
            <p className="mt-3 text-base font-bold text-[var(--landing-muted)] md:text-lg">
              نفتخر بنجاح طلابنا المتفوقين
            </p>
          </motion.div>

          {/* Draggable 3D Card Stack Swiper */}
          {!prefersReducedMotion ? (
            <div 
              onMouseEnter={() => setIsHovered(true)}
              onMouseLeave={() => setIsHovered(false)}
              className="mt-12 flex flex-col items-center"
            >
              <div className="relative w-full max-w-[320px] h-[340px] flex items-center justify-center">
                {/* Preview Card 2 */}
                <motion.article
                  key={`prev2-${nextIndex2}`}
                  initial={{ scale: 0.85, y: 28, rotate: 3, opacity: 0 }}
                  animate={{ scale: 0.9, y: 24, rotate: -3, opacity: 0.6 }}
                  transition={{ type: "spring", stiffness: 300, damping: 24 }}
                  className="landing-panel absolute inset-0 flex flex-col items-center px-4 py-6 text-center select-none pointer-events-none z-10 border border-[var(--landing-line-strong)]"
                >
                  <span className="flex h-9 w-9 items-center justify-center rounded-full text-sm font-black bg-[#0E8F8F]/40 text-white">
                    {student2.rank}
                  </span>
                  <Image
                    src={student2.avatar}
                    alt={student2.name}
                    width={76}
                    height={76}
                    unoptimized
                    className="mt-3 h-[76px] w-[76px] rounded-full object-cover opacity-60"
                  />
                  <h3 className="mt-4 text-lg font-black text-[var(--landing-ink)] opacity-60">{student2.name}</h3>
                  <p className="mt-1 text-sm font-bold text-[var(--landing-muted)] opacity-60">{student2.stage}</p>
                  <strong className="mt-3 text-2xl font-black text-[var(--landing-ink)] opacity-60">{student2.score}</strong>
                  <span className="mt-1 text-xs font-extrabold text-[var(--landing-muted)] opacity-60">النسبة النهائية</span>
                </motion.article>

                {/* Preview Card 1 */}
                <motion.article
                  key={`prev1-${nextIndex1}`}
                  initial={{ scale: 0.9, y: 24, rotate: -3, opacity: 0.6 }}
                  animate={{ scale: 0.95, y: 12, rotate: 3, opacity: 0.95 }}
                  transition={{ type: "spring", stiffness: 300, damping: 24 }}
                  className="landing-panel absolute inset-0 flex flex-col items-center px-4 py-6 text-center select-none pointer-events-none z-20 border border-[var(--landing-line-strong)]"
                >
                  <span
                    className={`flex h-9 w-9 items-center justify-center rounded-full text-sm font-black ${
                      medalStyles[student1.rank] ?? "bg-[#0E8F8F] text-white"
                    }`}
                  >
                    {student1.rank}
                  </span>
                  <Image
                    src={student1.avatar}
                    alt={student1.name}
                    width={76}
                    height={76}
                    unoptimized
                    className="mt-3 h-[76px] w-[76px] rounded-full object-cover"
                  />
                  <h3 className="mt-4 text-lg font-black text-[var(--landing-ink)]">{student1.name}</h3>
                  <p className="mt-1 text-sm font-bold text-[var(--landing-muted)]">{student1.stage}</p>
                  <strong className="mt-3 text-2xl font-black text-[var(--landing-ink)]">{student1.score}</strong>
                  <span className="mt-1 text-xs font-extrabold text-[var(--landing-muted)]">النسبة النهائية</span>
                </motion.article>

                {/* Active Card with AnimatePresence */}
                <AnimatePresence initial={false} mode="popLayout">
                  <motion.article
                    key={`active-${currentIndex}`}
                    drag="x"
                    dragConstraints={{ left: 0, right: 0 }}
                    dragElastic={0.6}
                    onDragEnd={handleDragEnd}
                    style={{ x: dragX, rotate: dragRotate, opacity: dragOpacity }}
                    initial={{ scale: 0.95, y: 12, opacity: 0 }}
                    animate={{ scale: 1, y: 0, opacity: 1 }}
                    exit={{
                      x: swipeDirection === "left" ? -280 : 280,
                      rotate: swipeDirection === "left" ? -15 : 15,
                      opacity: 0,
                      scale: 0.95,
                    }}
                    transition={{
                      type: "spring",
                      stiffness: 280,
                      damping: 22,
                    }}
                    className="landing-panel absolute inset-0 flex flex-col items-center px-4 py-6 text-center select-none touch-none origin-bottom cursor-grab active:cursor-grabbing border border-[var(--landing-line-strong)] z-30"
                  >
                    <span
                      className={`flex h-9 w-9 items-center justify-center rounded-full text-sm font-black ${
                        medalStyles[studentTop.rank] ?? "bg-[#0E8F8F] text-white"
                      }`}
                    >
                      {studentTop.rank}
                    </span>
                    <Image
                      src={studentTop.avatar}
                      alt={studentTop.name}
                      width={76}
                      height={76}
                      unoptimized
                      className="mt-3 h-[76px] w-[76px] rounded-full object-cover"
                    />
                    <h3 className="mt-4 text-lg font-black text-[var(--landing-ink)]">{studentTop.name}</h3>
                    <p className="mt-1 text-sm font-bold text-[var(--landing-muted)]">{studentTop.stage}</p>
                    <strong className="mt-3 text-2xl font-black text-[var(--landing-ink)]">{studentTop.score}</strong>
                    <span className="mt-1 text-xs font-extrabold text-[var(--landing-muted)]">النسبة النهائية</span>
                  </motion.article>
                </AnimatePresence>
              </div>

              {/* Dots indicator */}
              <div className="mt-6 flex justify-center gap-2">
                {topStudents.map((_, idx) => (
                  <button
                    key={idx}
                    type="button"
                    onClick={() => {
                      setSwipeDirection(idx > currentIndex ? "left" : "right");
                      setCurrentIndex(idx);
                    }}
                    className={`h-2.5 w-2.5 rounded-full transition-colors duration-200 cursor-pointer ${
                      idx === currentIndex ? "bg-[#0E8F8F]" : "bg-[var(--landing-line)]"
                    }`}
                    aria-label={`الذهاب إلى الطالب ${idx + 1}`}
                  />
                ))}
              </div>
            </div>
          ) : (
            /* Accessibility fallback: Static grid */
            <motion.div
              className="mt-9 grid gap-4 sm:grid-cols-2 lg:grid-cols-5"
              variants={containerVariants}
              initial="hidden"
              whileInView="visible"
              viewport={{ once: true, margin: "-50px" }}
            >
              {topStudents.map((student) => (
                <motion.article
                  key={student.name}
                  variants={cardVariants}
                  className="landing-panel relative flex min-h-[230px] flex-col items-center px-4 py-6 text-center border border-[var(--landing-line-strong)]"
                >
                  <span
                    className={`absolute -top-3 flex h-9 w-9 items-center justify-center rounded-full text-sm font-black ${
                      medalStyles[student.rank] ?? "bg-[#0E8F8F] text-white"
                    }`}
                  >
                    {student.rank}
                  </span>
                  <Image
                    src={student.avatar}
                    alt={student.name}
                    width={76}
                    height={76}
                    unoptimized
                    className="mt-3 h-[76px] w-[76px] rounded-full object-cover"
                  />
                  <h3 className="mt-4 text-lg font-black text-[var(--landing-ink)]">{student.name}</h3>
                  <p className="mt-1 text-sm font-bold text-[var(--landing-muted)]">{student.stage}</p>
                  <strong className="mt-3 text-2xl font-black text-[var(--landing-ink)]">{student.score}</strong>
                  <span className="mt-1 text-xs font-extrabold text-[var(--landing-muted)]">النسبة النهائية</span>
                </motion.article>
              ))}
            </motion.div>
          )}

          <Link href="/register" className="landing-primary-button mx-auto mt-8">
            عرض المزيد من الأوائل
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </div>
      </section>

      <section id="teachers" className="landing-section mt-3 px-5 py-14 md:px-12 md:py-16 lg:px-16">
        <div className="relative z-10 flex flex-col gap-10">
          <motion.div
            initial="hidden"
            whileInView="visible"
            viewport={{ once: true, margin: "-80px" }}
            variants={titleVariants}
            className="text-center max-w-2xl mx-auto"
          >
            <h2 className="text-balance text-3xl font-black leading-tight text-[var(--landing-ink)] md:text-5xl">
              تعلم على يد نخبة من أفضل المعلمين
            </h2>
            <p className="mt-4 text-base font-semibold leading-8 text-[var(--landing-muted)] md:text-lg">
              خبرة عالية، شغف بالتعليم، دعم مستمر، وخطة واضحة تناسب مستوى كل طالب.
            </p>
            <Link href="/register" className="landing-primary-button mt-5 mx-auto">
              استكشف جميع المعلمين
            </Link>
          </motion.div>

          {/* Infinite auto-scrolling marquee for teachers */}
          <div className="relative w-full overflow-hidden py-4">
            <div className="flex gap-6 w-max animate-marquee-horizontal hover:[animation-play-state:paused] [direction:ltr]">
              {[...teachers, ...teachers, ...teachers].map((teacher, index) => (
                <article
                  key={index}
                  className="landing-panel w-[240px] shrink-0 overflow-hidden text-center hover:scale-[1.03] transition-transform duration-300 border border-[var(--landing-line-strong)]"
                >
                  <Image
                    src={teacher.avatar}
                    alt={teacher.name}
                    width={240}
                    height={280}
                    unoptimized
                    className="h-44 w-full object-cover"
                  />
                  <div className="px-4 py-4 [direction:rtl]">
                    <h3 className="text-base font-black text-[var(--landing-ink)] truncate">{teacher.name}</h3>
                    <p className="mt-1 text-xs font-bold text-[var(--landing-muted)]">{teacher.subject}</p>
                    <div className="mt-3 flex items-center justify-center gap-1 text-sm font-black text-[var(--landing-ink)]">
                      {teacher.rating}
                      <Star className="h-4 w-4 fill-[#D4A017] text-[#D4A017]" />
                    </div>
                  </div>
                </article>
              ))}
            </div>
          </div>

          <div className="mt-6 mx-auto grid grid-cols-2 gap-6 sm:grid-cols-4 w-full max-w-4xl">
            {platformStats.map(({ value, label, icon: Icon }) => (
              <div key={label} className="text-center p-4 bg-[var(--landing-card)] border border-[var(--landing-line-strong)] rounded-xl backdrop-blur-sm">
                <Icon className="mx-auto h-6 w-6 text-[#0E8F8F]" />
                <strong className="mt-2 block text-xl font-black text-[var(--landing-ink)]">{value}</strong>
                <span className="text-xs font-extrabold text-[var(--landing-muted)]">{label}</span>
              </div>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}



