"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { motion, useReducedMotion } from "framer-motion";
import { CakeSlice, PartyPopper, X } from "lucide-react";
import { studentService, type StudentNotificationDto } from "@/services/student-service";

const BIRTHDAY_TITLE = "عيد ميلاد سعيد! 🎉";
const CONFETTI_COLORS = ["#f59e0b", "#ec4899", "#14b8a6", "#2563eb", "#8b5cf6"];

function cairoDateKey(value: string | Date) {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "Africa/Cairo",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date(value));
}

function todayBirthdayNotification(notifications: StudentNotificationDto[]) {
  const today = cairoDateKey(new Date());
  return notifications.find(
    (notification) =>
      !notification.isRead &&
      notification.title === BIRTHDAY_TITLE &&
      cairoDateKey(notification.createdAt) === today,
  );
}

function millisecondsUntilNextCairoMidnight() {
  const now = new Date();
  const today = cairoDateKey(now);
  const [year, month, day] = today.split("-").map(Number);
  const approximateNextMidnight = new Date(Date.UTC(year!, month! - 1, day! + 1));
  const offsetName = new Intl.DateTimeFormat("en-US", {
    timeZone: "Africa/Cairo",
    timeZoneName: "longOffset",
  }).formatToParts(approximateNextMidnight)
    .find((part) => part.type === "timeZoneName")?.value ?? "GMT+00:00";
  const offset = /GMT([+-])(\d{2}):(\d{2})/.exec(offsetName);
  const offsetMilliseconds = offset
    ? (Number(offset[2]) * 60 + Number(offset[3])) * 60_000 * (offset[1] === "+" ? 1 : -1)
    : 0;
  return Math.max(1_000, approximateNextMidnight.getTime() - offsetMilliseconds - now.getTime());
}

export function StudentBirthdayCelebration() {
  const reduceMotion = useReducedMotion();
  const [notification, setNotification] = useState<StudentNotificationDto | null>(null);
  const [closing, setClosing] = useState(false);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const confetti = useMemo(
    () =>
      Array.from({ length: 26 }, (_, index) => ({
        id: index,
        left: `${4 + ((index * 37) % 92)}%`,
        color: CONFETTI_COLORS[index % CONFETTI_COLORS.length],
        delay: (index % 8) * 0.08,
        rotate: (index * 47) % 180,
      })),
    [],
  );

  const loadBirthday = useCallback(() => {
    let active = true;
    studentService
      .getNotifications()
      .then((notifications) => {
        if (active) setNotification(todayBirthdayNotification(notifications) ?? null);
      })
      .catch((error: unknown) => {
        console.error("[BirthdayCelebration] Unable to load student notifications.", error);
      });
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let cancelRequest = loadBirthday();
    let timer: ReturnType<typeof setTimeout>;
    const scheduleNextCheck = () => {
      timer = setTimeout(() => {
        cancelRequest();
        cancelRequest = loadBirthday();
        scheduleNextCheck();
      }, millisecondsUntilNextCairoMidnight() + 5_000);
    };
    scheduleNextCheck();
    const refreshWhenVisible = () => {
      if (document.visibilityState === "visible") {
        cancelRequest();
        cancelRequest = loadBirthday();
      }
    };
    document.addEventListener("visibilitychange", refreshWhenVisible);
    return () => {
      cancelRequest();
      clearTimeout(timer);
      document.removeEventListener("visibilitychange", refreshWhenVisible);
    };
  }, [loadBirthday]);

  const close = useCallback(async () => {
    if (!notification || closing) return;
    setClosing(true);
    try {
      await studentService.markNotificationAsRead(notification.id);
    } finally {
      setNotification(null);
      setClosing(false);
    }
  }, [closing, notification]);

  useEffect(() => {
    if (!notification) return;
    closeButtonRef.current?.focus();
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") void close();
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [close, notification]);

  if (!notification) return null;

  return (
    <div
      className="fixed inset-0 z-[var(--z-critical)] grid place-items-center bg-slate-950/65 px-4 py-8"
      dir="rtl"
      role="dialog"
      aria-modal="true"
      aria-labelledby="birthday-celebration-title"
    >
      {!reduceMotion &&
        confetti.map((piece) => (
          <motion.span
            key={piece.id}
            aria-hidden="true"
            className="pointer-events-none fixed top-[-24px] h-3 w-2 rounded-[2px]"
            style={{ left: piece.left, backgroundColor: piece.color }}
            initial={{ y: -20, rotate: 0, opacity: 0 }}
            animate={{ y: "105vh", rotate: piece.rotate + 540, opacity: [0, 1, 1, 0] }}
            transition={{ duration: 3.2, delay: piece.delay, ease: "linear", repeat: 1 }}
          />
        ))}

      <motion.section
        initial={reduceMotion ? { opacity: 0 } : { opacity: 0, scale: 0.9, y: 24 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        transition={{ duration: 0.45, ease: [0.16, 1, 0.3, 1] }}
        className="relative w-full max-w-md overflow-hidden rounded-2xl border border-amber-200 bg-[#fffaf0] px-6 pb-7 pt-9 text-center shadow-2xl shadow-slate-950/30 sm:px-9"
      >
        <button
          ref={closeButtonRef}
          type="button"
          onClick={close}
          disabled={closing}
          className="absolute start-4 top-4 grid size-10 place-items-center rounded-full text-amber-950 transition-colors hover:bg-amber-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-500 disabled:opacity-50"
          aria-label="إغلاق تهنئة عيد الميلاد"
        >
          <X className="size-5" />
        </button>

        <div className="mx-auto grid size-20 place-items-center rounded-full bg-amber-400 text-amber-950 shadow-lg shadow-amber-400/30">
          <CakeSlice className="size-10" aria-hidden="true" />
        </div>
        <p className="mt-5 text-sm font-black text-amber-700">النهارده يومك أنت 🎈</p>
        <h2
          id="birthday-celebration-title"
          className="mt-2 text-3xl font-black leading-tight text-[#0b2149]"
        >
          المنصة كلها بتحتفل بيك!
        </h2>
        <p className="mt-4 text-base font-semibold leading-8 text-slate-700">
          {notification.body}
        </p>
        <button
          type="button"
          onClick={close}
          disabled={closing}
          className="mt-7 inline-flex min-h-12 w-full items-center justify-center gap-2 rounded-2xl bg-[#0b2149] px-5 font-black text-white transition-transform hover:-translate-y-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0b2149] focus-visible:ring-offset-2 active:translate-y-0 disabled:cursor-wait disabled:opacity-70"
        >
          <PartyPopper className="size-5" aria-hidden="true" />
          {closing ? "لحظة..." : "شكرًا يا منصة مسار"}
        </button>
      </motion.section>
    </div>
  );
}
