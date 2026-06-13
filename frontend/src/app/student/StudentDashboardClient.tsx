"use client";

import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";

import {
  ContinueLearningCard,
  StudentDestinationsPanel,
  StudentGettingStartedPanel,
  PackageGrid,
  StatsStrip,
  StudentHero,
  UpcomingExamsPanel,
  QuickAccessPanel,
} from "@/packages/student";
import { studentService, type DashboardDto, type QuickAccessItemDto } from "@/services/student-service";
import { useAuthStore } from "@/stores/auth-store";
import { RegistrationInstructionsModal } from "@/components/registration/RegistrationInstructionsModal";
import { registerCacheStore, unregisterCacheStore } from "@/lib/cache-invalidation";

export default function StudentDashboardClient() {
  const [data, setData] = useState<DashboardDto | null>(null);
  const [quickAccessItems, setQuickAccessItems] = useState<QuickAccessItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const { user } = useAuthStore();
  const [showInstructionsOnboard, setShowInstructionsOnboard] = useState(false);
  const router = useRouter();

  // ─── Cookie helpers (cross-subdomain, persists 1 year) ─────────────────
  const COOKIE_KEY = `onboarding_ack_${user?.id ?? 'anon'}`;

  const getOnboardingCookie = () => {
    if (typeof window === 'undefined') return false;
    try {
      if (localStorage.getItem(COOKIE_KEY) === '1') {
        return true;
      }
    } catch {}
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

  const fetchDashboard = useCallback(() => {
    setLoadError(null);
    Promise.all([
      studentService.getDashboard(),
      studentService.getQuickAccess(),
    ])
      .then(([dashboardData, dQuickAccess]) => {
        setData(dashboardData);
        setQuickAccessItems(dQuickAccess || []);
      })
      .catch(() => {
        setLoadError("تعذر تحميل لوحة الطالب. تحقق من الاتصال ثم أعد المحاولة.");
      })
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  useEffect(() => {
    registerCacheStore('student:exams', () => {}, fetchDashboard);
    return () => {
      unregisterCacheStore('student:exams');
    };
  }, [fetchDashboard]);

  if (loading) {
    return (
      <div className="space-y-6" aria-label="جارٍ تحميل لوحة الطالب">
        <div className="h-20 animate-pulse rounded-xl bg-[var(--admin-card-strong)]" />
        <div className="grid gap-4 xl:grid-cols-[minmax(0,1.35fr)_minmax(18rem,0.65fr)]">
          <div className="h-64 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />
          <div className="h-64 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />
        </div>
        <div className="h-72 animate-pulse rounded-2xl bg-[var(--admin-card-strong)]" />
      </div>
    );
  }

  if (!data && loadError) {
    return (
      <div
        role="alert"
        className="mx-auto flex max-w-xl flex-col items-center gap-4 rounded-2xl border border-red-200 bg-red-50 p-6 text-center dark:border-red-500/30 dark:bg-red-500/10"
      >
        <p className="font-bold text-red-700 dark:text-red-200">{loadError}</p>
        <button
          type="button"
          onClick={() => {
            setLoading(true);
            fetchDashboard();
          }}
          className="inline-flex min-h-11 items-center justify-center rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)]"
        >
          إعادة المحاولة
        </button>
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
    <div className="space-y-8 pb-4">
      {loadError && (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm font-bold text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100"
        >
          <span>{loadError} يتم عرض آخر بيانات متاحة.</span>
          <button
            type="button"
            onClick={fetchDashboard}
            className="min-h-11 rounded-xl border border-current px-4"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      <StudentHero data={d} />

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.35fr)_minmax(18rem,0.65fr)] xl:items-stretch">
        <ContinueLearningCard
          resumePoint={d.resumePoint ?? undefined}
          hasActivePackages={d.activePackages.length > 0}
          onContinue={() => {
            if (d.resumePoint) {
              router.push(
                `/student/packages/${d.resumePoint.packageId}/lessons/${d.resumePoint.lessonId}`,
              );
              return;
            }
            router.push(d.activePackages.length > 0 ? "/student/packages" : "/student/code-redemption");
          }}
        />

        <UpcomingExamsPanel
          exams={d.upcomingExams}
          onStartExam={(examId) => router.push(`/student/exams/${examId}`)}
        />
      </div>

      {(d.activePackages.length === 0 || (!d.resumePoint && d.totalLessonsCompleted === 0)) && (
        <StudentGettingStartedPanel data={d} />
      )}

      <PackageGrid
        packages={d.activePackages}
        onOpenPackage={(packageId) => router.push(`/student/packages/${packageId}`)}
        onActivateCode={() => router.push("/student/code-redemption")}
      />

      {quickAccessItems.length > 0 && <QuickAccessPanel items={quickAccessItems} />}

      <div className="grid gap-4 lg:grid-cols-2 lg:items-start">
        <StudentDestinationsPanel />
        <StatsStrip data={d} />
      </div>

      <RegistrationInstructionsModal
        open={showInstructionsOnboard}
        onClose={handleCloseOnboard}
        confirmLabel="أوافق وأرغب في استكمال استخدام المنصة"
        title="تعليمات وشروط هامة قبل الدخول"
        subtitle="يرجى قراءتها بدقة قبل تسجيل الدخول للجنة التعليمية واستخدام المنصة."
      />
    </div>
  );
}
