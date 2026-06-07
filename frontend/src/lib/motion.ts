/**
 * Shared Motion Utilities — Massar Platform
 *
 * Centralized Framer Motion variants, easings, and transition presets.
 * Import these instead of re-defining animation configs in every component.
 *
 * Usage:
 *   import { fadeSlideUp, stagger, easeQuart } from '@/lib/motion';
 */

/* ─── Easing Curves ─── */

/** Smooth, confident deceleration — the project's signature easing */
export const easeQuart = [0.25, 1, 0.5, 1] as const;

/** Slightly snappier variant for small/fast animations */
export const easeQuint = [0.22, 1, 0.36, 1] as const;

/* ─── Transition Presets ─── */

/** Standard entrance transition (500ms) */
export const enterTransition = { duration: 0.5, ease: easeQuart };

/** Fast feedback transition (300ms) — for forms, toggles */
export const feedbackTransition = { duration: 0.3, ease: easeQuart };

/** Quick micro-interaction (200ms) */
export const microTransition = { duration: 0.2, ease: easeQuart };

/** Exit transition — 75% of entrance duration */
export const exitTransition = { duration: 0.25, ease: easeQuart };

/* ─── Variant Presets ─── */

/** Fade + slide up — the most common entrance pattern */
export const fadeSlideUp = {
  hidden: { opacity: 0, y: 18 },
  show: {
    opacity: 1,
    y: 0,
    transition: enterTransition,
  },
};

/** Fade + slide up + subtle scale — for cards */
export const fadeScaleUp = {
  hidden: { opacity: 0, y: 14, scale: 0.97 },
  show: {
    opacity: 1,
    y: 0,
    scale: 1,
    transition: enterTransition,
  },
};

/** Parent container stagger — wraps children with fadeSlideUp/fadeScaleUp */
export const stagger = (delayMs = 100) => ({
  hidden: {},
  show: { transition: { staggerChildren: delayMs / 1000 } },
});

/** Creates a staggered card variant with custom per-item delay */
export const staggeredCard = (delayPerItem = 0.08) => ({
  hidden: { opacity: 0, y: 14, scale: 0.97 },
  show: (i: number) => ({
    opacity: 1,
    y: 0,
    scale: 1,
    transition: { delay: i * delayPerItem, duration: 0.45, ease: easeQuart },
  }),
});

/** Exit animation — scale down + fade */
export const exitScale = {
  opacity: 0,
  scale: 0.95,
  transition: exitTransition,
};

/** Expand/collapse for forms and drawers */
export const expandCollapse = {
  initial: { opacity: 0, height: 0 },
  animate: { opacity: 1, height: 'auto' as const },
  exit: { opacity: 0, height: 0 },
  transition: feedbackTransition,
};
