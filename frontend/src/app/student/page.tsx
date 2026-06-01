"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import { stagger, fadeSlideUp } from "@/lib/motion";

import { ContinueLearningCard } from "@/components/student-dashboard/ContinueLearningCard";
import { StudentDestinationsPanel } from "@/components/student-dashboard/StudentDestinationsPanel";
import { StudentGettingStartedPanel } from "@/components/student-dashboard/StudentGettingStartedPanel";
import { StudentMomentumRail } from "@/components/student-dashboard/StudentMomentumRail";
import { PackageGrid } from "@/components/student-dashboard/PackageGrid";
import { StatsStrip } from "@/components/student-dashboard/StatsStrip";
import { StudentHero } from "@/components/student-dashboard/StudentHero";
import { UpcomingExamsPanel } from "@/components/student-dashboard/UpcomingExamsPanel";
import { QuickAccessPanel } from "@/components/student-dashboard/QuickAccessPanel";
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

  useEffect(() => {
    if (user?.id) {
      const storageKey = `has_viewed_onboarding_instructions_${user.id}`;
      const hasViewed = localStorage.getItem(storageKey);
      if (!hasViewed) {
        setShowInstructionsOnboard(true);
      }
    }
  }, [user]);

  const handleCloseOnboard = () => {
    if (user?.id) {
      const storageKey = `has_viewed_onboarding_instructions_${user.id}`;
      localStorage.setItem(storageKey, "true");
    }
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
        <div className="h-72 rounded-[36px] bg-[var(--admin-card-strong)] animate-pulse" />
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
          <div className="h-[28rem] rounded-[32px] bg-[var(--admin-card-strong)] animate-pulse" />
          <div className="h-[28rem] rounded-[32px] bg-[var(--admin-card-strong)] animate-pulse" />
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
