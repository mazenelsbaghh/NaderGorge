"use client";

import React, { useEffect, useState } from "react";
import { Clock, ClockAlert } from "lucide-react";
import { useReducedMotion } from "framer-motion";

interface CountdownTimerProps {
  startedAt: string; // ISO string
  durationMinutes: number;
  remainingSeconds?: number;
  onTimeExpired: () => void;
  dangerThresholdMinutes?: number;
}

export function CountdownTimer({
  startedAt,
  durationMinutes,
  remainingSeconds,
  onTimeExpired,
  dangerThresholdMinutes = 5,
}: CountdownTimerProps) {
  const [timeLeft, setTimeLeft] = useState<number>(() => {
    if (remainingSeconds !== undefined && remainingSeconds !== null) {
      return remainingSeconds;
    }
    return durationMinutes * 60;
  });
  const shouldReduceMotion = useReducedMotion();

  useEffect(() => {
    if (!durationMinutes) return;

    // Use remainingSeconds if provided, otherwise compute target from client system clock relative to startedAt
    const initialTimeLeft = remainingSeconds !== undefined && remainingSeconds !== null
      ? remainingSeconds
      : (() => {
          const startTime = new Date(startedAt).getTime();
          const durationMs = durationMinutes * 60 * 1000;
          const endTime = startTime + durationMs;
          const remainingMs = endTime - Date.now();
          return Math.max(0, Math.floor(remainingMs / 1000));
        })();

    setTimeLeft(initialTimeLeft);

    if (initialTimeLeft <= 0) {
      onTimeExpired();
      return;
    }

    const targetEndTime = Date.now() + initialTimeLeft * 1000;

    const interval = setInterval(() => {
      const remainingMs = targetEndTime - Date.now();

      if (remainingMs <= 0) {
        clearInterval(interval);
        setTimeLeft(0);
        onTimeExpired();
      } else {
        setTimeLeft(Math.ceil(remainingMs / 1000));
      }
    }, 200); // 200ms poll speed ensures wake from tab suspend is processed near-instantly

    return () => clearInterval(interval);
  }, [startedAt, durationMinutes, remainingSeconds, onTimeExpired]);

  if (!durationMinutes) {
    return null;
  }

  const isWarning = timeLeft <= dangerThresholdMinutes * 60;
  
  const h = Math.floor(timeLeft / 3600);
  const m = Math.floor((timeLeft % 3600) / 60);
  const s = timeLeft % 60;
  const accessibleTimeLeft = h > 0
    ? `${h} ساعة و${m} دقيقة و${s} ثانية`
    : `${m} دقيقة و${s} ثانية`;

  return (
    <div
      className={`sticky top-6 z-40 mr-auto flex w-max items-center gap-3 rounded-full border px-6 py-3 shadow-[0_12px_40px_var(--admin-shadow)] backdrop-blur-xl transition-colors ${
        isWarning
          ? "bg-[var(--admin-danger-10)] border-[var(--admin-danger-20)] text-[var(--admin-danger)]"
          : "bg-[var(--admin-card)]/95 border-[var(--admin-border)] text-[var(--admin-text)]"
      }`}
      role="timer"
      aria-live="off"
      aria-atomic="true"
      aria-label={`الوقت المتبقي: ${accessibleTimeLeft}`}
      dir="ltr"
    >
      <div 
        className={`flex items-center gap-1 font-mono text-3xl font-black ${isWarning && !shouldReduceMotion ? 'animate-pulse' : ''}`}
      >
        {h > 0 && (
          <>
            <span className="w-8 text-center font-black">{h.toString().padStart(2, '0')}</span>
            <span className="text-[var(--admin-muted)]">:</span>
          </>
        )}
        <span className="w-8 text-center font-black">{m.toString().padStart(2, '0')}</span>
        <span className="text-[var(--admin-muted)]">:</span>
        <span className="w-8 text-center font-black">{s.toString().padStart(2, '0')}</span>
      </div>
      {isWarning ? <ClockAlert className={`w-6 h-6 ${shouldReduceMotion ? '' : 'animate-pulse'}`} /> : <Clock className="w-6 h-6 opacity-70" />}
    </div>
  );
}
