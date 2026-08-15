'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { ChevronDown } from 'lucide-react';

import {
  StudentDestinationsPanel,
  StudentGettingStartedPanel,
  StudentMomentumRail,
  PackageGrid,
  StatsStrip,
  StudentHero,
  UpcomingExamsPanel,
  UpcomingHomeworkPanel,
  QuickAccessPanel,
} from '@/packages/student';
import {
  studentService,
  type DashboardDto,
  type QuickAccessItemDto,
} from '@/services/student-service';
import { useAuthStore } from '@/stores/auth-store';
import { RegistrationInstructionsModal } from '@/components/registration/RegistrationInstructionsModal';
import { usePlatformQuery } from '@/components/providers/QueryProvider';
import { queryKeys } from '@/lib/query-keys';

export default function StudentDashboardClient() {
  const userId = useAuthStore((state) => state.user?.id);
  const [showInstructionsOnboard, setShowInstructionsOnboard] = useState(false);
  const router = useRouter();
  const userBoundary = userId ?? 'pending';
  const dashboardQueryFn = useCallback(
    ({ signal }: { signal: AbortSignal }) =>
      studentService.getDashboard(signal),
    []
  );
  const quickAccessQueryFn = useCallback(
    ({ signal }: { signal: AbortSignal }) =>
      studentService.getQuickAccess(signal),
    []
  );
  const dashboardQuery = usePlatformQuery<DashboardDto>({
    queryKey: queryKeys.student.dashboard(userBoundary),
    queryFn: dashboardQueryFn,
    staleTime: 30_000,
    enabled: Boolean(userId),
  });
  const quickAccessQuery = usePlatformQuery<QuickAccessItemDto[]>({
    queryKey: queryKeys.student.quickAccess(userBoundary),
    queryFn: quickAccessQueryFn,
    staleTime: 30_000,
    enabled: Boolean(userId),
  });
  const data = dashboardQuery.data;
  const quickAccessItems = quickAccessQuery.data ?? [];
  const loading =
    !userId ||
    (dashboardQuery.data === undefined && dashboardQuery.error === null) ||
    (quickAccessQuery.data === undefined && quickAccessQuery.error === null);
  const loadError =
    dashboardQuery.error || quickAccessQuery.error
      ? 'تعذر تحميل لوحة الطالب. تحقق من الاتصال ثم أعد المحاولة.'
      : null;

  const refetchDashboard = useCallback(() => {
    void Promise.all([
      dashboardQuery.refetch(),
      quickAccessQuery.refetch(),
    ]).catch(() => undefined);
  }, [dashboardQuery, quickAccessQuery]);

  // ─── Cookie helpers (cross-subdomain, persists 1 year) ─────────────────
  const COOKIE_KEY = `onboarding_ack_${userId ?? 'anon'}`;

  const getOnboardingCookie = () => {
    if (typeof window === 'undefined') return false;
    try {
      if (localStorage.getItem(COOKIE_KEY) === '1') {
        return true;
      }
    } catch {}
    if (typeof document === 'undefined') return false;
    return document.cookie
      .split('; ')
      .some((c) => c.startsWith(`${COOKIE_KEY}=1`));
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
    try {
      localStorage.setItem(COOKIE_KEY, '1');
    } catch {}
  };

  useEffect(() => {
    if (userId) {
      if (!getOnboardingCookie()) {
        setShowInstructionsOnboard(true);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  const handleCloseOnboard = () => {
    setOnboardingCookie();
    setShowInstructionsOnboard(false);
  };

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
          onClick={refetchDashboard}
          className="inline-flex min-h-11 items-center justify-center rounded-xl bg-[var(--admin-primary)] px-5 text-sm font-black text-[var(--admin-primary-contrast)]"
        >
          إعادة المحاولة
        </button>
      </div>
    );
  }

  const d: DashboardDto = data ?? {
    studentName: 'طالب',
    activePackages: [],
    resumePoint: undefined,
    upcomingExams: [],
    upcomingHomeworks: [],
    overallProgressPercent: 0,
    totalLessonsCompleted: 0,
    totalLessons: 0,
    codesRedeemed: 0,
  };

  return (
    <div className="space-y-6 pb-4">
      {loadError && (
        <div
          role="alert"
          className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm font-bold text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100"
        >
          <span>{loadError} يتم عرض آخر بيانات متاحة.</span>
          <button
            type="button"
            onClick={refetchDashboard}
            className="min-h-11 rounded-xl border border-current px-4"
          >
            إعادة المحاولة
          </button>
        </div>
      )}

      <StudentHero data={d} />

      <StudentMomentumRail data={d} />

      {(d.activePackages.length === 0 ||
        (!d.resumePoint && d.totalLessonsCompleted === 0)) && (
        <StudentGettingStartedPanel
          data={d}
          hasDirectContentAccess={quickAccessItems.length > 0}
        />
      )}

      {(d.upcomingExams.length > 0 || d.upcomingHomeworks.length > 0) && (
        <details
          className="group rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]"
          open
        >
          <summary className="flex min-h-14 cursor-pointer list-none items-center gap-3 px-5 py-3 font-black text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)]">
            <span className="flex-1">المواعيد القريبة</span>
            <span className="text-xs font-bold text-[var(--admin-muted)]">
              {d.upcomingExams.length + d.upcomingHomeworks.length} عناصر
            </span>
            <ChevronDown
              className="h-4 w-4 transition-transform group-open:rotate-180"
              aria-hidden="true"
            />
          </summary>
          <div className="grid gap-4 border-t border-[var(--admin-border)] p-4 lg:grid-cols-2">
            <UpcomingExamsPanel
              exams={d.upcomingExams}
              onStartExam={(examId) => router.push(`/student/exams/${examId}`)}
            />
            <UpcomingHomeworkPanel
              homeworks={d.upcomingHomeworks}
              onStartHomework={(homeworkId) =>
                router.push(`/student/homework/${homeworkId}`)
              }
            />
          </div>
        </details>
      )}

      <details className="group rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
        <summary className="flex min-h-14 cursor-pointer list-none items-center gap-3 px-5 py-3 font-black text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)]">
          <span className="flex-1">باقاتي الكاملة</span>
          <span className="text-xs font-bold text-[var(--admin-muted)]">
            {d.activePackages.length} باقات
          </span>
          <ChevronDown
            className="h-4 w-4 transition-transform group-open:rotate-180"
            aria-hidden="true"
          />
        </summary>
        <div className="border-t border-[var(--admin-border)] p-4">
          <PackageGrid
            packages={d.activePackages}
            onOpenPackage={(packageId) =>
              router.push(`/student/packages/${packageId}`)
            }
            onBrowsePackages={() => router.push('/student/packages')}
          />
        </div>
      </details>

      <QuickAccessPanel accessItems={quickAccessItems} />

      <details className="group rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]">
        <summary className="flex min-h-14 cursor-pointer list-none items-center gap-3 px-5 py-3 font-black text-[var(--admin-text)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[var(--admin-primary)]">
          <span className="flex-1">المزيد من أدواتي</span>
          <span className="text-xs font-bold text-[var(--admin-muted)]">
            الوصول السريع والإحصاءات
          </span>
          <ChevronDown
            className="h-4 w-4 transition-transform group-open:rotate-180"
            aria-hidden="true"
          />
        </summary>
        <div className="space-y-4 border-t border-[var(--admin-border)] p-4">
          <div className="grid gap-4 lg:grid-cols-2 lg:items-start">
            <StudentDestinationsPanel />
            <StatsStrip data={d} />
          </div>
        </div>
      </details>

      <RegistrationInstructionsModal
        open={showInstructionsOnboard && !d.resumePoint}
        onClose={handleCloseOnboard}
        confirmLabel="قرأت التعليمات، ابدأ رحلتي"
        title="قبل أول خطوة في مسارك"
        subtitle="راجع تعليمات الاستخدام مرة واحدة، ثم ابدأ دراستك مباشرة."
      />
    </div>
  );
}
