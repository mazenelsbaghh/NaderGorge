export const MOTION_DURATION = {
  fast: 0.16,
  standard: 0.24,
  deliberate: 0.32,
} as const;

export const MOTION_EASE_OUT = [0.16, 1, 0.3, 1] as const;
export const easeQuart = [0.25, 1, 0.5, 1] as const;
export const feedbackTransition = {
  duration: MOTION_DURATION.fast,
  ease: MOTION_EASE_OUT,
} as const;
export const exitScale = {
  opacity: 0,
  scale: 0.98,
  transition: feedbackTransition,
} as const;
export const fadeSlideUp = {
  hidden: { opacity: 0, y: 12 },
  visible: {
    opacity: 1,
    y: 0,
    transition: {
      duration: MOTION_DURATION.standard,
      ease: MOTION_EASE_OUT,
    },
  },
} as const;

export function enterTransition(reduced: boolean | null) {
  return reduced
    ? { duration: 0 }
    : {
        duration: MOTION_DURATION.standard,
        ease: MOTION_EASE_OUT,
      };
}

export function enterFromY(reduced: boolean | null, distance = 12) {
  return {
    initial: reduced ? false : { opacity: 0, y: distance },
    animate: { opacity: 1, y: 0 },
    exit: reduced ? { opacity: 0 } : { opacity: 0, y: distance },
    transition: enterTransition(reduced),
  } as const;
}
