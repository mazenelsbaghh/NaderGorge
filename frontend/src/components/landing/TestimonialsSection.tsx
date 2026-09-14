"use client";

import { ChevronLeft, ChevronRight, Star } from "lucide-react";
import Image from "next/image";
import { motion, useReducedMotion } from "framer-motion";
import { useCallback, useMemo, useState } from "react";

import { testimonials } from "./data";

export function TestimonialsSection() {
  const prefersReducedMotion = useReducedMotion();
  const [currentIndex, setCurrentIndex] = useState(0);
  const paginate = useCallback((direction: number) => {
    setCurrentIndex(
      (index) =>
        (index + direction + testimonials.length) % testimonials.length
    );
  }, []);
  const orderedTestimonials = useMemo(
    () =>
      testimonials.map(
        (_, index) => testimonials[(currentIndex + index) % testimonials.length]
      ),
    [currentIndex]
  );

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

  const containerVariants = {
    hidden: {},
    visible: {
      transition: {
        staggerChildren: prefersReducedMotion ? 0 : 0.08,
      },
    },
  };

  const itemVariants = {
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

  return (
    <section id="testimonials" className="landing-section landing-section--stories mt-3 px-5 py-14 md:px-12 md:py-16 lg:px-16">
      <div className="relative z-10 mx-auto max-w-[1180px]">
        <motion.div
          initial="hidden"
          whileInView="visible"
          viewport={{ once: true, margin: "-80px" }}
          variants={titleVariants}
          className="text-center"
        >
          <h2 className="text-3xl font-black text-[var(--landing-ink)] md:text-5xl">آراء طلابنا</h2>
          <p className="mt-3 text-base font-bold text-[var(--landing-muted)] md:text-lg">
            تجارب حقيقية من طلاب حققوا أهدافهم
          </p>
        </motion.div>

        <div
          className="mt-9 grid items-center gap-4 md:grid-cols-[44px_1fr_44px]"
          role="region"
          aria-roledescription="carousel"
          aria-label="آراء طلاب منصة مسار"
          tabIndex={0}
          onKeyDown={(event) => {
            if (event.key === "ArrowRight") {
              event.preventDefault();
              paginate(-1);
            }
            if (event.key === "ArrowLeft") {
              event.preventDefault();
              paginate(1);
            }
            if (event.key === "Home") {
              event.preventDefault();
              setCurrentIndex(0);
            }
            if (event.key === "End") {
              event.preventDefault();
              setCurrentIndex(Math.max(testimonials.length - 1, 0));
            }
          }}
        >
          <p className="sr-only" aria-live="polite" aria-atomic="true">
            الرأي {currentIndex + 1} من {testimonials.length}،{" "}
            {testimonials[currentIndex]?.name}
          </p>
          <button
            type="button"
            onClick={() => paginate(-1)}
            className="hidden h-11 w-11 items-center justify-center rounded-full bg-[var(--landing-card)] text-[var(--landing-ink)] border border-[var(--landing-line)] hover:bg-[var(--landing-card-strong)] transition-colors duration-200 md:flex"
            aria-label="الرأي السابق"
          >
            <ChevronRight className="h-5 w-5" />
          </button>

          <motion.div
            className="grid gap-5 md:grid-cols-3"
            variants={containerVariants}
            initial="hidden"
            whileInView="visible"
            viewport={{ once: true, margin: "-50px" }}
          >
            {orderedTestimonials.map((review, index) => (
              <motion.figure
                key={review.name}
                variants={itemVariants}
                className={`landing-panel px-6 py-6 text-right md:block ${
                  index > 0 ? "hidden" : "block"
                }`}
              >
                <div className="flex items-center gap-4">
                  <Image
                    src={review.avatar}
                    alt={review.name}
                    width={64}
                    height={64}
                    unoptimized
                    className="h-16 w-16 rounded-full object-cover"
                  />
                  <div className="min-w-0">
                    <figcaption className="truncate text-base font-black text-[var(--landing-ink)]">
                      {review.name}
                    </figcaption>
                    <div className="mt-1 flex gap-0.5 text-[#D4A017]" aria-label="تقييم خمسة نجوم">
                      {Array.from({ length: 5 }).map((_, index) => (
                        <Star key={index} className="h-4 w-4 fill-current" />
                      ))}
                    </div>
                  </div>
                </div>
                <blockquote className="mt-4 text-sm font-semibold leading-7 text-[var(--landing-muted)]">
                  {review.quote}
                </blockquote>
              </motion.figure>
            ))}
          </motion.div>

          <button
            type="button"
            onClick={() => paginate(1)}
            className="hidden h-11 w-11 items-center justify-center rounded-full bg-[var(--landing-card)] text-[var(--landing-ink)] border border-[var(--landing-line)] hover:bg-[var(--landing-card-strong)] transition-colors duration-200 md:flex"
            aria-label="الرأي التالي"
          >
            <ChevronLeft className="h-5 w-5" />
          </button>
        </div>

        <div
          className="mt-5 flex justify-center gap-1"
          aria-label={`الرأي ${currentIndex + 1} من ${testimonials.length}`}
        >
          {testimonials.map((review, index) => (
            <button
              key={review.name}
              type="button"
              onClick={() => setCurrentIndex(index)}
              aria-label={`عرض رأي ${review.name}`}
              aria-current={index === currentIndex ? "true" : undefined}
              className={`min-h-11 min-w-11 rounded-full focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--landing-accent)] before:mx-auto before:block before:h-2 before:rounded-full before:content-[''] ${
                index === currentIndex
                  ? "before:w-6 before:bg-[var(--landing-accent)]"
                  : "before:w-2 before:bg-[var(--landing-line)]"
              }`}
            />
          ))}
        </div>
      </div>
    </section>
  );
}
