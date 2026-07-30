"use client";

import { motion, AnimatePresence } from "framer-motion";
import { InlineLoader } from "@/components/ui/loading-indicator";
import type { WatchStatus } from "@/components/video/SecureVideoPlayer";

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
      className="w-full flex flex-col sm:flex-row sm:items-center justify-between gap-3 sm:gap-4 px-4 py-3 rounded-2xl bg-[var(--admin-card)]/90 backdrop-blur-xl border border-[var(--admin-border)] shadow-sm"
      dir="rtl"
      initial={{ opacity: 0, y: -8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: [0.23, 1, 0.32, 1] }}
    >
      {/* Left — label + counter */}
      <div className="flex items-center gap-3 min-w-0 w-full sm:w-auto">
        {/* Eye icon */}
        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-[var(--admin-primary)]/10 text-[var(--admin-primary)]">
          <svg className="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round">
            <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z" />
            <circle cx="12" cy="12" r="3" />
          </svg>
        </div>

        <div className="flex flex-col leading-tight min-w-0">
          <span className="text-sm font-bold text-[var(--admin-text)] tracking-tight truncate">
            {title ? title : "المشاهدات"}
          </span>
          <span className="text-xs text-[var(--admin-muted)] font-medium mt-px whitespace-nowrap">
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
            className="inline-flex items-center gap-2 rounded-full border border-[var(--admin-primary)]/20 bg-[var(--admin-primary)]/8 px-3 py-1.5 text-xs font-bold text-[var(--admin-primary)] self-start sm:self-auto shrink-0 max-w-full"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
          >
            <InlineLoader className="text-[var(--admin-primary)] !w-3 !h-3" />
            <span className="text-[10px] xs:text-xs leading-normal whitespace-normal break-words sm:whitespace-nowrap">
              {status
                ? `${watchedInThreshold}ث من ${status.thresholdSeconds}ث · تُحتسب المشاهدة عند اكتمال المدة`
                : "جاري التجهيز..."}
            </span>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}
