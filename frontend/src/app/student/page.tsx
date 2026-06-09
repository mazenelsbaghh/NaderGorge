"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import { stagger, fadeSlideUp } from "@/lib/motion";

import {
  ContinueLearningCard,
  StudentDestinationsPanel,
  StudentGettingStartedPanel,
  StudentMomentumRail,
  PackageGrid,
  StatsStrip,
  StudentHero,
  UpcomingExamsPanel,
  QuickAccessPanel,
} from "@/packages/student";
import { useStudentTheme } from "@/hooks/useStudentTheme";
import { studentService, type DashboardDto, type QuickAccessItemDto } from "@/services/student-service";
import { useAuthStore } from "@/stores/auth-store";
import { RegistrationInstructionsModal } from "@/components/registration/RegistrationInstructionsModal";

const pageStagger = stagger(100);

export default function StudentDashboard() {
  const { isReady } = useStudentTheme();
  const [data, setData] = useState<DashboardDto | null>(null);
  const [quickAccessItems, setQuickAccessItems] = useState<QuickAccessItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const { user } = useAuthStore();
  const [showInstructionsOnboard, setShowInstructionsOnboard] = useState(false);
  const router = useRouter();

  // ─── Cookie helpers (cross-subdomain, persists 1 year) ─────────────────
  const COOKIE_KEY = `onboarding_ack_${user?.id ?? 'anon'}`;

  const getOnboardingCookie = () => {
    if (typeof document === 'undefined') return false;
    return document.cookie.split('; ').some((c) => c.startsWith(`${COOKIE_KEY}=1`));
  };

  const setOnboardingCookie = () => {
    if (typeof document === 'undefined') return;
    const domain = window.location.hostname.includes('massar-academy.net')
      ? '.massar-academy.net'
      : window.location.hostname;
    const expires = new Date();
    expires.setFullYear(expires.getFullYear() + 1);
    document.cookie = `${COOKIE_KEY}=1; path=/; domain=${domain}; expires=${expires.toUTCString()}; SameSite=Lax`;
    // Also set in localStorage as fallback for local dev
    try { localStorage.setItem(COOKIE_KEY, '1'); } catch {}
  };

  useEffect(() => {
    if (user?.id) {
      if (!getOnboardingCookie()) {
        setShowInstructionsOnboard(true);
      }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.id]);

  const handleCloseOnboard = () => {
    setOnboardingCookie();
    setShowInstructionsOnboard(false);
  };

  useEffect(() => {
    Promise.all([
      studentService.getDashboard(),
      studentService.getQuickAccess(),
    ])
      .then(([dashboardData, dQuickAccess]) => {
        setData(dashboardData);
        setQuickAccessItems(dQuickAccess || []);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="space-y-6">
        <div className="h-72 rounded-2xl bg-[var(--admin-card-strong)] animate-pulse" />
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          {[1, 2, 3, 4].map((i) => (
            <div
              key={i}
              className="h-32 rounded-[28px] bg-[var(--admin-card-strong)] animate-pulse"
            />
          ))}
        </div>
        <div className="h-44 rounded-[28px] bg-[var(--admin-card-strong)] animate-pulse" />
        <div className="grid gap-6 xl:grid-cols-[1.45fr_0.85fr]">
          <div className="h-[28rem] rounded-2xl bg-[var(--admin-card-strong)] animate-pulse" />
          <div className="h-[28rem] rounded-2xl bg-[var(--admin-card-strong)] animate-pulse" />
        </div>
      </div>
    );
  }

  const d: DashboardDto = data ?? {
    studentName: "طالب",
    activePackages: [],
    resumePoint: undefined,
    upcomingExams: [],
    overallProgressPercent: 0,
    totalLessonsCompleted: 0,
    totalLessons: 0,
    codesRedeemed: 0,
  };

  return (
    <motion.div
      className="space-y-10"
      variants={pageStagger}
      initial="hidden"
      animate={isReady ? "show" : undefined}
    >
      <motion.div variants={fadeSlideUp}>
        <StudentHero data={d} />
      </motion.div>

      <motion.div variants={fadeSlideUp} className="grid gap-6 xl:grid-cols-[1.05fr_0.95fr] xl:items-start">
        <ContinueLearningCard
          resumePoint={d.resumePoint ?? undefined}
          onContinue={() => {
            if (d.resumePoint) {
              router.push(
                `/student/packages/${d.resumePoint.packageId}/lessons/${d.resumePoint.lessonId}`,
              );
              return;
            }
            router.push("/student/code-redemption");
          }}
        />

        <StudentMomentumRail data={d} />
      </motion.div>

      {(d.activePackages.length === 0 || (!d.resumePoint && d.totalLessonsCompleted === 0)) && (
        <motion.div variants={fadeSlideUp}>
          <StudentGettingStartedPanel data={d} />
        </motion.div>
      )}

      <motion.div variants={fadeSlideUp} className="grid gap-6 xl:grid-cols-[1.38fr_0.92fr] xl:items-start">
        <div className="space-y-6">
          <PackageGrid
            packages={d.activePackages}
            onOpenPackage={(packageId) => router.push(`/student/packages/${packageId}`)}
            onActivateCode={() => router.push("/student/code-redemption")}
          />
        </div>

        <div className="space-y-6">
          {quickAccessItems.length > 0 && (
            <QuickAccessPanel items={quickAccessItems} />
          )}

          <UpcomingExamsPanel
            exams={d.upcomingExams}
            onStartExam={(examId) => router.push(`/student/exams/${examId}`)}
          />
        </div>
      </motion.div>

      <motion.div variants={fadeSlideUp} className="grid gap-5 xl:grid-cols-[1.15fr_0.85fr] xl:items-start">
        <StudentDestinationsPanel />
        <StatsStrip data={d} />
      </motion.div>

      <RegistrationInstructionsModal
        open={showInstructionsOnboard}
        onClose={handleCloseOnboard}
        confirmLabel="أوافق وأرغب في استكمال استخدام المنصة"
      />
    </motion.div>
  );
}
