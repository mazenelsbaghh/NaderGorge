"use client";

/**
 * ProgressRing — SVG ring with animated glow trail + sparkle tip
 *
 * Replaces the old conic-gradient approach with a GPU-friendly SVG ring
 * that has:
 *  • Animated stroke-dasharray fill (0% → target)
 *  • Glowing trail at the leading edge via SVG filter
 *  • Counting number animation
 *  • Ambient pulsing glow behind the ring
 *
 * prefers-reduced-motion: skips the fill animation, shows final state instantly.
 */

import { useEffect, useState, useId } from "react";
import { motion, useMotionValue, useTransform, animate, useReducedMotion } from "framer-motion";
import { easeQuart } from "@/lib/motion";

type ProgressRingProps = {
  percent: number;
  /** Outer diameter class, e.g. "h-52 w-52 sm:h-64 sm:w-64 md:h-72 md:w-72" */
  sizeClass?: string;
};

/*
 * SVG geometry: a 200×200 viewBox with stroke on a circle of r=82.
 * Circumference = 2π × 82 ≈ 515.22
 */
const RADIUS = 82;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

export function ProgressRing({ percent, sizeClass = "h-52 w-52 sm:h-64 sm:w-64 md:h-72 md:w-72" }: ProgressRingProps) {
  const id = useId();
  const filterId = `glow-${id}`;
  const gradientId = `ring-grad-${id}`;
  const shouldReduceMotion = useReducedMotion();

  /* ── Animated progress ── */
  const progressVal = useMotionValue(0);
  const dashOffset = useTransform(progressVal, (v) => CIRCUMFERENCE - (CIRCUMFERENCE * v) / 100);
  const [displayPercent, setDisplayPercent] = useState(0);

  /* ── Sparkle tip angle (tracks the arc's leading edge) ── */
  const tipAngle = useTransform(progressVal, (v) => (v / 100) * 360 - 90); // -90 b/c SVG starts at top

  useEffect(() => {
    if (shouldReduceMotion) {
      progressVal.set(percent);
      return;
    }

    const ctrl = animate(progressVal, percent, {
      duration: 1.4,
      ease: easeQuart,
      onUpdate: (v) => setDisplayPercent(Math.round(v)),
    });
    return () => ctrl.stop();
  }, [percent, progressVal, shouldReduceMotion]);

  const renderedPercent = shouldReduceMotion ? Math.round(percent) : displayPercent;

  return (
    <div className={`relative ${sizeClass} max-w-full`}>
      {/* ── Ambient pulsing glow ── */}
      <motion.div
        className="absolute inset-0 rounded-full"
        style={{
          background: "radial-gradient(circle, var(--admin-primary) 0%, transparent 70%)",
          filter: "blur(30px)",
        }}
        animate={shouldReduceMotion ? { opacity: 0.1 } : { opacity: [0.08, 0.18, 0.08] }}
        transition={shouldReduceMotion ? { duration: 0 } : { duration: 3.5, repeat: Infinity, ease: "easeInOut" }}
      />

      {/* ── Outer ring container ── */}
      <div className="absolute inset-0 flex items-center justify-center rounded-full border border-[var(--admin-border)] bg-gradient-to-br from-[var(--admin-card)] via-[var(--admin-card-strong)] to-[var(--admin-card)] shadow-[0_20px_60px_var(--admin-shadow)]">

        {/* ── SVG Ring ── */}
        <svg viewBox="0 0 200 200" className="absolute inset-3 sm:inset-4 h-[calc(100%-24px)] w-[calc(100%-24px)] sm:h-[calc(100%-32px)] sm:w-[calc(100%-32px)] -rotate-90">
          <defs>
            {/* Glow filter for the leading edge */}
            <filter id={filterId} x="-50%" y="-50%" width="200%" height="200%">
              <feGaussianBlur in="SourceGraphic" stdDeviation="4" result="blur" />
              <feMerge>
                <feMergeNode in="blur" />
                <feMergeNode in="blur" />
                <feMergeNode in="SourceGraphic" />
              </feMerge>
            </filter>

            {/* Gradient along the arc: from transparent → gold → bright gold */}
            <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stopColor="var(--admin-primary)" stopOpacity="0.3" />
              <stop offset="60%" stopColor="var(--admin-primary)" stopOpacity="0.9" />
              <stop offset="100%" stopColor="var(--admin-primary)" stopOpacity="1" />
            </linearGradient>
          </defs>

          {/* Track ring */}
          <circle
            cx="100"
            cy="100"
            r={RADIUS}
            fill="none"
            stroke="var(--admin-card-strong)"
            strokeWidth="14"
            strokeLinecap="round"
          />

          {/* Glow layer (blurred duplicate for the trail) */}
          <motion.circle
            cx="100"
            cy="100"
            r={RADIUS}
            fill="none"
            stroke="var(--admin-primary)"
            strokeWidth="16"
            strokeLinecap="round"
            strokeDasharray={CIRCUMFERENCE}
            style={{ strokeDashoffset: dashOffset }}
            filter={`url(#${filterId})`}
            opacity={0.5}
          />

          {/* Main progress arc */}
          <motion.circle
            cx="100"
            cy="100"
            r={RADIUS}
            fill="none"
            stroke={`url(#${gradientId})`}
            strokeWidth="12"
            strokeLinecap="round"
            strokeDasharray={CIRCUMFERENCE}
            style={{ strokeDashoffset: dashOffset }}
          />
        </svg>

        {/* ── Sparkle dot at the tip ── */}
        <motion.div
          className="absolute"
          style={{
            width: 10,
            height: 10,
            borderRadius: "50%",
            background: "var(--admin-primary)",
            boxShadow: "0 0 12px 4px var(--admin-primary), 0 0 24px 8px rgba(197,160,89,0.3)",
            // Position on the ring's circumference
            top: "50%",
            left: "50%",
            x: useTransform(tipAngle, (a) => Math.cos((a * Math.PI) / 180) * (RADIUS / 100) * 50 - 5 + "%"),
            y: useTransform(tipAngle, (a) => Math.sin((a * Math.PI) / 180) * (RADIUS / 100) * 50 - 5 + "%"),
          }}
          animate={shouldReduceMotion ? { scale: 1, opacity: 0.95 } : {
            scale: [1, 1.4, 1],
            opacity: [0.9, 1, 0.9],
          }}
          transition={shouldReduceMotion ? { duration: 0 } : { duration: 2, repeat: Infinity, ease: "easeInOut" }}
        />

        {/* ── Center content ── */}
        <div className="relative flex h-32 w-32 flex-col items-center justify-center rounded-full bg-[var(--admin-card-soft)] px-3 text-center shadow-inner sm:h-40 sm:w-40 md:h-44 md:w-44">
          <p className="text-[10px] font-bold tracking-[0.16em] text-[var(--admin-muted)] sm:text-sm sm:tracking-[0.22em]">
            التقدّم
          </p>
          <p className="mt-2 text-3xl font-black text-[var(--admin-text)] sm:mt-3 sm:text-4xl md:text-5xl">
            {renderedPercent}%
          </p>
          <p className="mt-1 text-center text-xs font-semibold leading-5 text-[var(--admin-muted)] sm:mt-2 sm:text-sm sm:leading-6">
            تقدمك الكلي في المحتوى
          </p>
        </div>
      </div>
    </div>
  );
}
