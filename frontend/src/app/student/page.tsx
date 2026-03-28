"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import { stagger, fadeSlideUp } from "@/lib/motion";

import { ContinueLearningCard } from "@/components/student-dashboard/ContinueLearningCard";
import { PackageGrid } from "@/components/student-dashboard/PackageGrid";
import { StatsStrip } from "@/components/student-dashboard/StatsStrip";
import { StudentHero } from "@/components/student-dashboard/StudentHero";
import { UpcomingExamsPanel } from "@/components/student-dashboard/UpcomingExamsPanel";
import { QuickAccessPanel } from "@/components/student-dashboard/QuickAccessPanel";
import { studentService, type DashboardDto, type QuickAccessItemDto } from "@/services/student-service";

const pageStagger = stagger(100);

export default function StudentDashboard() {
  const [data, setData] = useState<DashboardDto | null>(null);
  const [quickAccessItems, setQuickAccessItems] = useState<QuickAccessItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const router = useRouter();

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
      className="space-y-6"
      variants={pageStagger}
      initial="hidden"
      animate="show"
    >
      <motion.div variants={fadeSlideUp}>
        <StudentHero data={d} />
      </motion.div>

      <motion.div variants={fadeSlideUp}>
        <StatsStrip data={d} />
      </motion.div>

      {quickAccessItems.length > 0 && (
        <motion.div variants={fadeSlideUp}>
          <QuickAccessPanel items={quickAccessItems} />
        </motion.div>
      )}

      <motion.div variants={fadeSlideUp} className="grid gap-6 xl:grid-cols-[1.45fr_0.85fr]">
        <div className="space-y-6">
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

          <PackageGrid
            packages={d.activePackages}
            onOpenPackage={(packageId) => router.push(`/student/packages/${packageId}`)}
            onActivateCode={() => router.push("/student/code-redemption")}
          />
        </div>

        <UpcomingExamsPanel
          exams={d.upcomingExams}
          onStartExam={(examId) => router.push(`/student/exams/${examId}`)}
        />
      </motion.div>
    </motion.div>
  );
}

