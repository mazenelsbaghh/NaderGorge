'use client';

/**
 * StudentShellChrome — mirrors AdminShellChrome exactly.
 *
 * Structure:
 *  - Fixed vertical icon sidebar (desktop, right side for RTL)
 *  - Compact bottom nav bar (mobile) — 3 primary + menu button
 *  - Slide-out drawer for secondary items on mobile
 *  - Dot-grid background with ambient overlay
 *  - Theme toggle (light / dark) shared with admin via useAdminTheme
 *  - Breadcrumb + header section
 *  - Footer branding
 *
 * Token source: useAdminTheme() → same --admin-* CSS vars as admin pages.
 */

import { type CSSProperties, ReactNode, useEffect, useState, useCallback, useId, useRef } from 'react';
import Link from 'next/link';
import { useRouter, usePathname } from 'next/navigation';
import {
  Bell,
  Bug,
  BookOpen,
  BookMarked,
  ChartNoAxesColumn,
  ChevronLeft,
  ClipboardList,
  GraduationCap,
  Home,
  LogOut,
  Settings,
  User,
  Wallet,
  X,
} from 'lucide-react';
import { motion, AnimatePresence, useReducedMotion } from 'framer-motion';

// StudentThemeSettingsPanel removed as settings are now inside the profile tab system
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { SidebarBalance } from '@/components/layout/SidebarBalance';
import { SidebarGamification } from '@/components/layout/SidebarGamification';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useStudentTheme } from '@/hooks/useStudentTheme';
import { useAuthStore } from '@/stores/auth-store';
import { useLessonFocusStore } from '@/stores/lesson-focus-store';
import { UserAvatar } from '@/components/ui/UserAvatar';
import { useStudentShellStore } from '@/stores/student-shell-store';
import { ParentCodePopup } from '@/components/student/ParentCodePopup';
import { HeaderParentBadge } from '@/components/layout/HeaderParentBadge';
import { PlatformLogo } from '@/components/shared/PlatformLogo';
import { IntentLink } from '@/components/navigation/IntentLink';
import {
  NavigationFocusManager,
  SkipToContentLink,
} from '@/components/navigation/NavigationFocusManager';
import { useShellNavigationState } from '@/hooks/useShellNavigationState';
import { AccessibleOverlay } from '@/components/ui/AccessibleOverlay';
import { StudentBottomNav } from '@/components/layout/StudentBottomNav';

/* ── Route type safety ──────────────────────────────────────────────── */

type StudentShellRoute =
  | '/student'
  | '/student/lessons'
  | '/student/packages'
  | '/student/shared-packages'
  | '/student/public-exams'
  | '/student/balance'
  | '/student/mistakes'
  | '/student/notifications'
  | '/student/profile'
  | '/student/teachers';

type StudentShellChromeProps = {
  children: ReactNode;
};

const studentShellTokenAliases = {
  '--student-bg': 'var(--admin-bg)',
  '--student-sidebar': 'var(--admin-sidebar)',
  '--student-text': 'var(--admin-text)',
  '--student-muted': 'var(--admin-muted)',
  '--student-primary': 'var(--admin-primary)',
  '--student-primary-strong': 'var(--admin-primary-strong)',
  '--student-card': 'var(--admin-card)',
  '--student-card-soft': 'var(--admin-card-soft)',
  '--student-card-strong': 'var(--admin-card-strong)',
  '--student-border': 'var(--admin-border)',
  '--student-hover': 'var(--admin-hover)',
  '--student-shadow': 'var(--admin-shadow)',
} as CSSProperties;

/* ── Nav items ──────────────────────────────────────────────────────── */

/** Primary: always visible in bottom nav on mobile */
const primaryNavItems: Array<{
  href: StudentShellRoute;
  label: string;
  icon: typeof ChartNoAxesColumn;
}> = [
    { href: '/student/lessons', label: 'دروسي', icon: BookOpen },
    { href: '/student/packages', label: 'باقاتي', icon: BookMarked },
    { href: '/student/public-exams', label: 'امتحانات', icon: ClipboardList },
  ];

/** Secondary: visible only inside the drawer on mobile */
const secondaryNavItems: Array<{
  href: StudentShellRoute;
  label: string;
  icon: typeof ChartNoAxesColumn;
}> = [
    { href: '/student/teachers', label: 'المدرسين', icon: GraduationCap },
    { href: '/student/shared-packages', label: 'باكدجات عامة', icon: BookMarked },
    { href: '/student/mistakes', label: 'أخطائي', icon: Bug },
    { href: '/student/notifications', label: 'الإشعارات', icon: Bell },
    { href: '/student/balance', label: 'الرصيد', icon: Wallet },
  ];

/** All items combined — used by the desktop sidebar */
const allNavItems = [...primaryNavItems, ...secondaryNavItems.filter(i => i.href !== '/student/balance')];

/* ── Component ──────────────────────────────────────────────────────── */

export function StudentShellChrome({ children }: StudentShellChromeProps) {
  const router = useRouter();
  const pathname = usePathname();
  const logout = useAuthStore((state) => state.logout);
  const user = useAuthStore((state) => state.user);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const {
    isDark,
    toggleTheme,
  } = useStudentTheme();
  const isFocusMode = useLessonFocusStore((state) => state.isFocusMode);
  const shouldReduceMotion = useReducedMotion();
  // isThemeSettingsOpen state removed
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const drawerId = useId();
  const shellInstanceId = useId();
  const drawerTriggerRef = useRef<HTMLButtonElement>(null);
  const mainScrollRef = useRef<HTMLElement>(null);

  useShellNavigationState({
    surface: 'student',
    pathname,
    scrollRef: mainScrollRef,
  });

  useRootOverscrollBackground();

  const unreadCount = useStudentShellStore((state) => state.unreadNotificationsCount);
  const fetchBootstrap = useStudentShellStore((state) => state.fetchBootstrap);

  useEffect(() => {
    const isStudent = user?.roles?.some((role) => role.toLowerCase() === 'student');
    // The shell is also rendered briefly while the route guard redirects a
    // guest. Do not call the protected bootstrap endpoint in that state.
    if (!isAuthenticated || !isStudent) return;
    void fetchBootstrap();

    const handleNotificationsUpdated = () => {
      void fetchBootstrap(true);
    };

    if (typeof window !== "undefined") {
      window.addEventListener("notificationsUpdated", handleNotificationsUpdated);
      return () => {
        window.removeEventListener("notificationsUpdated", handleNotificationsUpdated);
      };
    }
  }, [fetchBootstrap, isAuthenticated, user?.roles]);

  // Close drawer on route change
  useEffect(() => {
    setIsDrawerOpen(false);
  }, [pathname]);

  const handleLogout = () => {
    void logout().finally(() => {
      router.replace('/login');
    });
  };

  const closeDrawer = useCallback(() => setIsDrawerOpen(false), []);

  /* Which top-level route is active? */
  const activePath: StudentShellRoute =
    pathname === '/student/lessons'
      ? '/student/lessons'
      : pathname.startsWith('/student/packages')
      ? '/student/packages'
      : pathname.startsWith('/student/shared-packages')
        ? '/student/shared-packages'
      : pathname.startsWith('/student/public-exams')
        ? '/student/public-exams'
      : pathname.startsWith('/student/teachers')
        ? '/student/teachers'
      : pathname.startsWith('/student/balance')
        ? '/student/balance'
      : pathname.startsWith('/student/mistakes')
        ? '/student/mistakes'
      : pathname.startsWith('/student/notifications')
        ? '/student/notifications'
      : pathname.startsWith('/student/profile')
        ? '/student/profile'
        : '/student';
  const drawerHasCurrentPage = secondaryNavItems.some((item) => item.href === activePath);
  const showAmbientBackground = !isFocusMode;

  return (
    <div
      data-testid="student-shell"
      data-shell-instance={shellInstanceId}
      style={studentShellTokenAliases}
      className="student-app-background relative h-screen h-dvh max-h-screen max-h-dvh overflow-x-clip text-[var(--student-text)]"
    >
      <SkipToContentLink />
      <NavigationFocusManager />
      {showAmbientBackground ? (
        <div className="pointer-events-none absolute inset-0 z-0 overflow-hidden">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_78%_12%,var(--admin-primary-10),transparent_34%)]" />
          {!shouldReduceMotion ? (
            <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-l from-transparent via-[var(--admin-primary)]/35 to-transparent" />
          ) : null}
        </div>
      ) : null}
      <AnimatePresence>
        {!isFocusMode && (
          <motion.aside
            initial={shouldReduceMotion ? false : { x: '100%' }}
            animate={{ x: 0 }}
            exit={shouldReduceMotion ? { opacity: 0 } : { x: '100%' }}
            transition={shouldReduceMotion ? { duration: 0 } : { duration: 0.24, ease: [0.16, 1, 0.3, 1] }}
            className="group/sidebar fixed start-0 top-0 z-50 hidden h-full w-20 flex-col justify-between border-e border-[var(--admin-border)] bg-[var(--admin-sidebar)] py-6 transition-[width] duration-200 ease-out hover:w-64 focus-within:w-64 lg:flex"
            role="navigation"
            aria-label="القائمة الرئيسية"
          >
            <div className="space-y-7">
              <Link
                href="/student/profile"
                className="flex w-full items-center justify-start gap-3 rounded-full px-5 py-1 text-right transition-colors duration-200 hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
                aria-label="الملف الشخصي"
              >
                <UserAvatar
                  avatarSlug={user?.avatarSlug}
                  fullName={user?.fullName}
                  size="sm"
                  className="ring-offset-2 ring-offset-[var(--admin-sidebar)] hover:scale-105 transition duration-300 flex-shrink-0"
                />
                <span className="hidden truncate whitespace-nowrap text-sm font-bold text-[var(--admin-text)] group-hover/sidebar:block group-focus-within/sidebar:block">
                  {user?.fullName || 'طالب'}
                </span>
              </Link>

              <nav className="space-y-3 px-3">
                <IntentLink
                  href="/student"
                  aria-label="لوحة التحكم"
                  aria-current={activePath === '/student' ? 'page' : undefined}
                  className={`flex h-12 items-center justify-start ps-[18px] pe-4 rounded-full transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300 gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)] ${activePath === '/student'
                    ? 'bg-[var(--admin-card-strong)] text-[var(--admin-primary)]'
                    : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                >
                  <Home className="h-5 w-5 flex-shrink-0" />
                  <span className="hidden truncate whitespace-nowrap text-sm font-bold group-hover/sidebar:block group-focus-within/sidebar:block">
                    لوحة التحكم
                  </span>
                </IntentLink>

                {allNavItems.map((item) => {
                  const Icon = item.icon;
                  const isActive = item.href === activePath;

                  return (
                    <IntentLink
                      key={item.href}
                      href={item.href}
                      className={`flex h-12 items-center justify-between ps-[18px] pe-4 rounded-full transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300 gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)] ${isActive
                        ? 'bg-[var(--admin-card-strong)] text-[var(--admin-primary)]'
                        : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                        }`}
                      title={item.label}
                      aria-label={item.label}
                      aria-current={isActive ? 'page' : undefined}
                    >
                      <div className="flex items-center gap-3">
                        <div className="relative">
                          <Icon className="h-5 w-5 flex-shrink-0" />
                          {item.href === '/student/notifications' && unreadCount > 0 && (
                            <span className="absolute -top-1 -end-1 h-2 w-2 rounded-full bg-[var(--admin-primary)]" />
                          )}
                        </div>
                        <span className="hidden truncate whitespace-nowrap text-sm font-bold group-hover/sidebar:block group-focus-within/sidebar:block">
                          {item.label}
                        </span>
                      </div>
                      {item.href === '/student/notifications' && unreadCount > 0 && (
                        <span className="ms-2 hidden h-5 items-center justify-center rounded-full bg-[var(--admin-primary)] px-1.5 text-xs font-black text-[var(--admin-primary-contrast)] group-hover/sidebar:flex group-focus-within/sidebar:flex">
                          {unreadCount}
                        </span>
                      )}
                    </IntentLink>
                  );
                })}
              </nav>
            </div>

            <div className="space-y-3 px-3">
              <div className="flex flex-col gap-2 justify-start px-4 transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300 w-full">
                <SidebarBalance />
                <SidebarGamification />
              </div>
              <div className="flex justify-start px-1 items-center transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300">
                <AnimatedThemeToggler
                  checked={isDark}
                  onToggle={toggleTheme}
                  aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                  title={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                  className="flex h-12 w-12 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)] flex-shrink-0"
                />
                <span className="ms-3 hidden self-center truncate whitespace-nowrap text-sm font-bold text-[var(--admin-muted)] group-hover/sidebar:block group-focus-within/sidebar:block">
                  {isDark ? 'الوضع الفاتح' : 'الوضع الداكن'}
                </span>
              </div>
              <IntentLink
                href="/student/profile"
                className={`flex h-12 w-full items-center justify-start ps-[18px] pe-4 rounded-full transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300 gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)] ${
                  pathname === '/student/profile'
                    ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-sm'
                    : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                }`}
                aria-label="الملف الشخصي"
                title="الملف الشخصي"
              >
                <Settings className="h-5 w-5 flex-shrink-0" />
                <span className="hidden truncate whitespace-nowrap text-sm font-bold group-hover/sidebar:block group-focus-within/sidebar:block">
                  الملف الشخصي
                </span>
              </IntentLink>
              <button
                ref={drawerTriggerRef}
                type="button"
                onClick={handleLogout}
                className="flex h-12 w-full items-center justify-start ps-[18px] pe-4 rounded-full text-[var(--admin-danger)] transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-300 gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
                title="تسجيل الخروج"
                aria-label="تسجيل الخروج"
              >
                <LogOut className="h-5 w-5 flex-shrink-0" />
                <span className="hidden truncate whitespace-nowrap text-sm font-bold group-hover/sidebar:block group-focus-within/sidebar:block">
                  تسجيل الخروج
                </span>
              </button>
            </div>
          </motion.aside>
        )}
      </AnimatePresence>

      <main
        ref={mainScrollRef}
        id="main-content"
        className={`app-shell-scroll relative z-10 h-screen h-dvh min-h-0 overflow-y-scroll overscroll-y-contain ${
          isFocusMode
            ? 'px-0 py-0 pb-0 lg:ms-0 lg:px-0 lg:py-0 lg:pb-0'
            : 'px-4 py-6 pb-[calc(7.5rem+env(safe-area-inset-bottom))] lg:ms-24 lg:px-8 lg:py-10 lg:pb-10'
        }`}
      >
        <AnimatePresence>
          {!isFocusMode && (
            <motion.header
              initial={{ opacity: 0, y: -20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              transition={{ duration: 0.3 }}
              className="mb-6 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-3 lg:mb-8 lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0"
            >
              <div className="mb-3 flex min-h-11 items-center justify-between gap-3 lg:hidden">
                <HeaderParentBadge />
                <PlatformLogo
                  variant="full"
                  size="sm"
                  tone={isDark ? 'light' : 'dark'}
                  priority
                  className="h-9 w-auto max-w-[128px]"
                />
                <Link
                  href="/student/profile"
                  className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                  aria-label="الملف الشخصي"
                >
                  <UserAvatar avatarSlug={user?.avatarSlug} fullName={user?.fullName} size="sm" />
                </Link>
              </div>
              <div className="flex items-center justify-between w-full">
                <nav className="flex min-w-0 items-center gap-1.5 text-sm font-medium text-[var(--admin-muted)] lg:gap-2 lg:text-xs">
                  <span className="truncate">المساحة الدراسية</span>
                  <ChevronLeft className="h-3 w-3 shrink-0" />
                  <span className="truncate text-[var(--admin-primary-strong)]">بوابة الطالب</span>
                </nav>
                <div className="flex items-center gap-2 lg:gap-3">
                  {/* Desktop-only header actions */}
                  <div className="hidden lg:flex items-center gap-3">
                    <HeaderParentBadge />
                    <SidebarBalance />
                    <Link
                      href="/student/notifications"
                      className="relative flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                      title="الإشعارات"
                      aria-label={
                        unreadCount > 0
                          ? `الإشعارات، ${unreadCount} غير مقروءة`
                          : 'الإشعارات، لا توجد إشعارات غير مقروءة'
                      }
                    >
                      <Bell className="h-5 w-5" />
                      {unreadCount > 0 && (
                        <span
                          aria-hidden="true"
                          className="absolute end-1 top-1 flex h-4.5 w-4.5 items-center justify-center rounded-full bg-[var(--admin-primary)] text-xs font-black text-[var(--admin-primary-contrast)]"
                        >
                          {unreadCount}
                        </span>
                      )}
                    </Link>
                    <AnimatedThemeToggler
                      checked={isDark}
                      onToggle={toggleTheme}
                      aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                      className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                    />
                    <Link
                      href="/student/profile"
                      className="hover:scale-105 transition duration-300"
                      title="الملف الشخصي"
                    >
                      <UserAvatar
                        avatarSlug={user?.avatarSlug}
                        fullName={user?.fullName}
                        size="sm"
                      />
                    </Link>
                  </div>
                </div>
              </div>
            </motion.header>
          )}
        </AnimatePresence>

        {children}

        <AnimatePresence>
          {!isFocusMode && (
            <motion.footer
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 0.6, y: 0 }}
              exit={{ opacity: 0, y: 16 }}
              className="mt-20 flex flex-col items-center select-none"
            >
              <div className="mb-4 h-px w-full bg-[var(--admin-border)]" />
              <p className="text-xs font-black tracking-[0.26em] text-[var(--admin-footer)]">
                منصة مسار
              </p>
            </motion.footer>
          )}
        </AnimatePresence>
      </main>

      {/* ── Mobile Bottom Nav (compact: 3 primary + menu) ─────────────── */}
      <AnimatePresence>
        {!isFocusMode && (
          <StudentBottomNav
            activePath={activePath}
            primaryItems={primaryNavItems}
            drawerHasCurrentPage={drawerHasCurrentPage}
            drawerId={drawerId}
            isDrawerOpen={isDrawerOpen}
            onOpenDrawer={() => setIsDrawerOpen(true)}
            unreadCount={unreadCount}
          />
        )}
      </AnimatePresence>

      {/* ── Mobile Drawer (slide from left for RTL) ────────────────────── */}
      <AccessibleOverlay
        open={isDrawerOpen}
        onClose={closeDrawer}
        label="القائمة الجانبية"
        triggerRef={drawerTriggerRef}
        backdropClassName="backdrop-blur-sm"
        layerClassName="lg:hidden"
        className="end-0 top-0 h-full w-72 max-w-[88vw] overflow-y-auto overscroll-contain bg-[var(--admin-sidebar)] shadow-sm"
        testId="student-mobile-drawer"
      >
            <div id={drawerId} className="min-h-full">
              <div className="flex min-h-full flex-col px-5 py-6">
                {/* Drawer header */}
                <div className="flex items-center justify-between mb-6">
                  <div className="flex items-center gap-3">
                    <UserAvatar
                      avatarSlug={user?.avatarSlug}
                      fullName={user?.fullName}
                      size="sm"
                    />
                    <span className="text-sm font-black text-[var(--admin-text)]">
                      أهلاً، {user?.fullName?.split(' ')[0] || 'طالب'}
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={closeDrawer}
                    className="flex h-11 w-11 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                    aria-label="إغلاق القائمة"
                  >
                    <X className="h-5 w-5" />
                  </button>
                </div>

                {/* Balance & Gamification cards */}
                <div className="mb-5 rounded-2xl bg-[var(--admin-card-soft)] p-4 flex flex-col gap-3">
                  <SidebarBalance />
                  <SidebarGamification />
                </div>

                {/* Secondary nav links */}
                <nav className="flex-1 space-y-1">
                  <p className="mb-2 text-xs font-black uppercase tracking-[0.2em] text-[var(--admin-muted)]">
                    وجهات إضافية
                  </p>
                  {secondaryNavItems.map((item) => {
                    const Icon = item.icon;
                    const isActive = item.href === activePath;

                    return (
                      <Link
                        key={item.href}
                        href={item.href}
                        onClick={closeDrawer}
                        className={`flex items-center justify-between rounded-2xl px-4 py-3 text-sm font-bold transition-[color,background-color,border-color,opacity,transform,box-shadow] ${isActive
                          ? 'bg-[var(--admin-card-strong)] text-[var(--admin-primary)]'
                          : 'text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'
                          }`}
                      >
                        <div className="flex items-center gap-3">
                          <Icon className="h-5 w-5 shrink-0" />
                          <span>{item.label}</span>
                        </div>
                        {item.href === '/student/notifications' && unreadCount > 0 && (
                          <span className="bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] text-xs font-black h-5 px-2 rounded-full flex items-center justify-center">
                            {unreadCount}
                          </span>
                        )}
                      </Link>
                    );
                  })}
                  {/* Profile */}
                  <Link
                    href="/student/profile"
                    onClick={closeDrawer}
                    className={`flex w-full items-center gap-3 rounded-2xl px-4 py-3 text-sm font-bold transition ${
                      activePath === '/student/profile'
                        ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-md border-transparent'
                        : 'text-[var(--admin-text)] hover:bg-[var(--admin-hover)] border-[var(--admin-border)]'
                    }`}
                  >
                    <User className="h-5 w-5 shrink-0" />
                    <span>الملف الشخصي</span>
                  </Link>
                </nav>

                {/* Drawer footer actions */}
                <div className="mt-auto space-y-1 border-t border-[var(--admin-border)] pt-4">
                  {/* Theme toggle */}
                  <div className="flex items-center justify-between rounded-2xl px-4 py-3">
                    <span className="text-sm font-bold text-[var(--admin-text)]">الوضع الليلي</span>
                    <AnimatedThemeToggler
                      checked={isDark}
                      onToggle={toggleTheme}
                      aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                      className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                    />
                  </div>

                  {/* Logout */}
                  <button
                    type="button"
                    onClick={() => {
                      closeDrawer();
                      handleLogout();
                    }}
                    className="flex w-full items-center gap-3 rounded-2xl px-4 py-3 text-sm font-bold text-[var(--admin-danger)] transition hover:bg-[var(--admin-hover)]"
                  >
                    <LogOut className="h-5 w-5 shrink-0" />
                    <span>تسجيل الخروج</span>
                  </button>
                </div>
              </div>
            </div>
      </AccessibleOverlay>
      <ParentCodePopup />
    </div>
  );
}
