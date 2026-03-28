"use client";

import { useState, useEffect } from 'react';
import { Clock, ClockAlert } from 'lucide-react';

interface ExamTimerProps {
  startedAt: string;
  durationMinutes: number;
  onTimeExpired: () => void;
}

export function ExamTimer({ startedAt, durationMinutes, onTimeExpired }: ExamTimerProps) {
  const [timeLeft, setTimeLeft] = useState<number>(durationMinutes * 60);
  const [isWarning, setIsWarning] = useState(false);

  useEffect(() => {
    if (!startedAt || !durationMinutes) return;

    const startTime = new Date(startedAt).getTime();
    const durationMs = durationMinutes * 60 * 1000;
    const endTime = startTime + durationMs;

    const interval = setInterval(() => {
      const now = new Date().getTime();
      const remainingMs = endTime - now;

      if (remainingMs <= 0) {
        clearInterval(interval);
        setTimeLeft(0);
        onTimeExpired();
      } else {
        const remainingSeconds = Math.floor(remainingMs / 1000);
        setTimeLeft(remainingSeconds);
        // Warn warning if < 5 minutes (300 seconds)
        setIsWarning(remainingSeconds <= 300);
      }
    }, 1000);

    return () => clearInterval(interval);
  }, [startedAt, durationMinutes, onTimeExpired]);

  if (!durationMinutes) {
    return null;
  }

  const m = Math.floor(timeLeft / 60);
  const s = timeLeft % 60;
  const timeString = `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;

  return (
    <div 
      className={`sticky top-6 z-40 mr-auto mb-[-2rem] flex w-max items-center gap-2 px-5 py-2.5 rounded-full shadow-[0_12px_40px_rgba(0,0,0,0.12)] border backdrop-blur-xl transition-colors ${
        isWarning 
          ? 'bg-[#fee2e2]/90 border-[#f87171]/50 text-[#ef4444] animate-pulse dark:bg-[#7f1d1d]/90 dark:border-[#dc2626]/50' 
          : 'bg-[var(--admin-card)]/90 border-[var(--admin-border)] text-[var(--admin-text)]'
      }`}
      dir="ltr"
    >
      <span className="font-mono text-xl font-bold">{timeString}</span>
      {isWarning ? <ClockAlert className="w-5 h-5" /> : <Clock className="w-5 h-5" />}
    </div>
  );
}
