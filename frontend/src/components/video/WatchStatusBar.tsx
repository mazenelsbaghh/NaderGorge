"use client";

import { motion, AnimatePresence } from "framer-motion";
import { InlineLoader } from "@/components/ui/loading-indicator";
import type { WatchStatus } from "@/components/video/SecureVideoPlayer";
import { formatWatchDuration } from "@/lib/watch-duration";

interface WatchStatusBarProps {
  status: WatchStatus | null;
  /** Video title shown in the left section */
  title?: string;
}

/**
 * WatchStatusBar — standalone premium watch-tracking bar.
 * Renders outside the video player, below or alongside it.
 */
export function WatchStatusBar({ status, title }: WatchStatusBarProps) {
  const cappedCurrent = status
    ? status.max > 0
      ? Math.min(status.current, status.max)
      : status.current
    : 0;
  const watchedInThreshold = status
    ? Math.min(status.displayedWatched, Math.max(0, status.thresholdSeconds))
    : 0;

  return (
    <motion.div
      className="flex w-full flex-col justify-between gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 px-3 py-2.5 shadow-sm backdrop-blur-md sm:flex-row sm:items-center sm:gap-4 sm:rounded-2xl sm:px-4 sm:py-3"
      dir="rtl"
      initial={{ opacity: 0, y: -8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: [0.23, 1, 0.32, 1] }}
    >
      {/* Left — label + counter */}
      <div className="flex min-w-0 w-full items-start gap-2.5 sm:w-auto sm:items-center sm:gap-3">
        {/* Eye icon */}
        <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-[var(--admin-primary)]/10 text-[var(--admin-primary)] sm:h-9 sm:w-9 sm:rounded-xl">
          <svg className="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
            <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z" />
            <circle cx="12" cy="12" r="3" />
          </svg>
        </div>

        <div className="flex flex-col leading-tight min-w-0">
          <span className="break-words text-[13px] font-bold leading-5 tracking-tight text-[var(--admin-text)] sm:text-sm">
            {title ? title : "المشاهدات"}
          </span>
          <span className="mt-px text-[11px] font-medium leading-4 text-[var(--admin-muted)] sm:whitespace-nowrap sm:text-xs">
            {status
              ? `${cappedCurrent} مشاهدة من أصل ${status.max}`
              : "جاري التجهيز..."}
          </span>
        </div>

        {/* Dot-progress pills */}
        {status && (
          <div className="hidden sm:flex items-center gap-1 mr-2">
            {Array.from({ length: status.max }).map((_, i) => (
              <motion.div
                key={i}
                className="h-1.5 w-5 rounded-full"
                style={{
                  backgroundColor:
                    i < cappedCurrent
                      ? "var(--admin-primary)"
                      : "var(--admin-border)",
                }}
                initial={{ scaleX: 0 }}
                animate={{ scaleX: 1 }}
                transition={{ delay: i * 0.06, duration: 0.3, ease: "easeOut" }}
              />
            ))}
          </div>
        )}
      </div>

      {/* Right — status badge */}
      <AnimatePresence mode="wait">
        {status?.isLocked ? (
          <motion.span
            key="locked"
            className="inline-flex items-center gap-1.5 rounded-full border border-red-400/20 bg-red-400/10 px-3 py-1.5 text-xs font-bold text-red-700 dark:text-red-300 self-start sm:self-auto shrink-0"
            initial={{ opacity: 0, scale: 0.85 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.85 }}
            transition={{ type: "spring", stiffness: 400, damping: 25 }}
          >
            تم الوصول للحد الأقصى
          </motion.span>
        ) : status?.viewTracked ? (
          <motion.span
            key="tracked"
            className="inline-flex items-center gap-1.5 rounded-full border border-emerald-400/20 bg-emerald-400/10 px-3 py-1.5 text-xs font-bold text-emerald-700 dark:text-emerald-300 self-start sm:self-auto shrink-0"
            initial={{ opacity: 0, scale: 0.85 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.85 }}
            transition={{ type: "spring", stiffness: 400, damping: 25 }}
          >
            <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
            </svg>
            تم احتساب المشاهدة
          </motion.span>
        ) : (
          <motion.div
            key="counting"
            className="flex w-full max-w-full items-start gap-2 rounded-lg border border-[var(--admin-primary)]/20 bg-[var(--admin-primary)]/8 px-2.5 py-2 text-xs font-bold leading-5 text-[var(--admin-primary)] sm:w-auto sm:shrink-0 sm:items-center sm:self-auto sm:rounded-full sm:px-3 sm:py-1.5"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <InlineLoader className="mt-1 !h-3 !w-3 shrink-0 text-[var(--admin-primary)] sm:mt-0" />
            <span className="min-w-0 break-words text-xs leading-5 sm:whitespace-nowrap">
              {status
                ? `${formatWatchDuration(watchedInThreshold)} من ${formatWatchDuration(status.thresholdSeconds)} · تُحتسب المشاهدة عند اكتمال المدة`
                : "جاري التجهيز..."}
            </span>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}
