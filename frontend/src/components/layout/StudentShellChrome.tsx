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

import { ReactNode, useEffect, useState, useCallback } from 'react';
import Link from 'next/link';
import { useRouter, usePathname } from 'next/navigation';
import {
  Bug,
  BookMarked,
  ChartNoAxesColumn,
  ChevronLeft,
  Home,
  KeyRound,
  LogOut,
  Menu,
  MessageSquareText,
  Settings,
  Wallet,
  X,
} from 'lucide-react';
import { motion, AnimatePresence, useReducedMotion } from 'framer-motion';

import { StudentThemeSettingsPanel } from '@/components/student/StudentThemeSettingsPanel';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { SidebarBalance } from '@/components/layout/SidebarBalance';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useStudentTheme } from '@/hooks/useStudentTheme';
import { useAuthStore } from '@/stores/auth-store';
import { useLessonFocusStore } from '@/stores/lesson-focus-store';
import { UserAvatar } from '@/components/ui/UserAvatar';

/* ── Route type safety ──────────────────────────────────────────────── */

type StudentShellRoute =
  | '/student'
  | '/student/packages'
  | '/student/community'
  | '/student/balance'
  | '/student/code-redemption'
  | '/student/mistakes';

type StudentShellChromeProps = {
  children: ReactNode;
};

/* ── Nav items ──────────────────────────────────────────────────────── */

/** Primary: always visible in bottom nav on mobile */
const primaryNavItems: Array<{
  href: StudentShellRoute;
  label: string;
  icon: typeof ChartNoAxesColumn;
}> = [
    { href: '/student/packages', label: 'باقاتي', icon: BookMarked },
    { href: '/student/community', label: 'المجتمع', icon: MessageSquareText },
  ];

/** Secondary: visible only inside the drawer on mobile */
const secondaryNavItems: Array<{
  href: StudentShellRoute;
  label: string;
  icon: typeof ChartNoAxesColumn;
}> = [
    { href: '/student/mistakes', label: 'أخطائي', icon: Bug },
    { href: '/student/code-redemption', label: 'تفعيل كود', icon: KeyRound },
    { href: '/student/balance', label: 'الرصيد', icon: Wallet },
  ];

/** All items combined — used by the desktop sidebar */
const allNavItems = [...primaryNavItems, ...secondaryNavItems.filter(i => i.href !== '/student/balance')];

/* ── Component ──────────────────────────────────────────────────────── */

export function StudentShellChrome({ children }: StudentShellChromeProps) {
  const router = useRouter();
  const pathname = usePathname();
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const user = useAuthStore((state) => state.user);
  const {
    mode,
    isDark,
    toggleTheme,
    isSavingPreferences,
    selectedLightPaletteId,
    selectedDarkPaletteId,
    updatePalette,
    updateAvatar,
  } = useStudentTheme();
  const isFocusMode = useLessonFocusStore((state) => state.isFocusMode);
  const shouldReduceMotion = useReducedMotion();
  const [isThemeSettingsOpen, setIsThemeSettingsOpen] = useState(false);
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);

  useRootOverscrollBackground();

  // Close drawer on route change
  useEffect(() => {
    setIsDrawerOpen(false);
  }, [pathname]);

  const handleLogout = () => {
    clearAuth();
    router.replace('/login');
  };

  const closeDrawer = useCallback(() => setIsDrawerOpen(false), []);

  /* Which top-level route is active? */
  const activePath: StudentShellRoute =
    pathname.startsWith('/student/packages')
      ? '/student/packages'
      : pathname.startsWith('/student/community')
        ? '/student/community'
      : pathname.startsWith('/student/balance')
        ? '/student/balance'
      : pathname.startsWith('/student/mistakes')
        ? '/student/mistakes'
      : pathname.startsWith('/student/code-redemption')
        ? '/student/code-redemption'
        : '/student';
  const showAmbientBackground = !isFocusMode;

  return (
    <div
      dir="rtl"
      className="h-dvh overflow-hidden bg-[var(--admin-bg)] text-[var(--admin-text)] relative"
    >
      {showAmbientBackground ? (
        <div className="pointer-events-none absolute inset-0 z-0 overflow-hidden">
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_78%_12%,var(--admin-primary-15),transparent_38%),radial-gradient(circle_at_16%_86%,var(--admin-primary-10),transparent_34%),linear-gradient(135deg,transparent_0_44%,var(--admin-primary-10)_44%_45%,transparent_45%_100%)]" />
          <div className="absolute inset-0 opacity-[0.18] [background-image:linear-gradient(var(--admin-border)_1px,transparent_1px),linear-gradient(90deg,var(--admin-border)_1px,transparent_1px)] [background-size:28px_28px]" />
          <div className="absolute left-[8%] top-16 h-28 w-28 rounded-full border border-[var(--admin-primary-20)]" />
          <div className="absolute bottom-28 right-[14%] h-36 w-36 rounded-[32px] border border-[var(--admin-primary-15)] rotate-12" />
          {!shouldReduceMotion ? (
            <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-l from-transparent via-[var(--admin-primary)]/35 to-transparent" />
          ) : null}
        </div>
      ) : null}
      <AnimatePresence>
        {!isFocusMode && (
          <motion.aside
            initial={{ x: '100%' }}
            animate={{ x: 0 }}
            exit={{ x: '100%' }}
            transition={{ type: 'spring', stiffness: 300, damping: 30 }}
            className="fixed right-0 top-0 z-50 hidden h-full w-20 flex-col justify-between bg-[var(--admin-sidebar)] py-6 shadow-[-12px_0_40px_var(--admin-shadow)] lg:flex group/sidebar transition-all duration-300 ease-in-out hover:w-64"
            role="navigation"
            aria-label="القائمة الرئيسية"
          >
            <div className="space-y-7">
              <div className="flex justify-center group-hover/sidebar:justify-start group-hover/sidebar:px-5 transition-all duration-300 cursor-pointer items-center gap-3" onClick={() => setIsThemeSettingsOpen(true)}>
                <UserAvatar
                  avatarSlug={user?.avatarSlug}
                  fullName={user?.fullName}
                  size="sm"
                  className="ring-offset-2 ring-offset-[var(--admin-sidebar)] hover:scale-105 transition duration-300 flex-shrink-0"
                />
                <span className="hidden group-hover/sidebar:block text-sm font-bold text-[var(--admin-text)] truncate whitespace-nowrap">
                  {user?.fullName || 'طالب'}
                </span>
              </div>

              <nav className="space-y-3 px-3">
                <Link
                  href="/student"
                  aria-label="لوحة التحكم"
                  aria-current={activePath === '/student' ? 'page' : undefined}
                  className={`flex h-12 items-center justify-center group-hover/sidebar:justify-start group-hover/sidebar:px-4 rounded-full transition-all duration-300 gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)] ${activePath === '/student'
                    ? 'bg-[var(--admin-card-strong)] text-[var(--admin-primary)]'
                    : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                >
                  <Home className="h-5 w-5 flex-shrink-0" />
                  <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
                    لوحة التحكم
                  </span>
                </Link>

                {allNavItems.map((item) => {
                  const Icon = item.icon;
                  const isActive = item.href === activePath;

                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      className={`flex h-12 items-center justify-center group-hover/sidebar:justify-start group-hover/sidebar:px-4 rounded-full transition-all duration-300 gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)] ${isActive
                        ? 'bg-[var(--admin-card-strong)] text-[var(--admin-primary)]'
                        : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                        }`}
                      title={item.label}
                      aria-label={item.label}
                      aria-current={isActive ? 'page' : undefined}
                    >
                      <Icon className="h-5 w-5 flex-shrink-0" />
                      <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
                        {item.label}
                      </span>
                    </Link>
                  );
                })}
              </nav>
            </div>

            <div className="space-y-3 px-3">
              <div className="flex justify-center group-hover/sidebar:justify-start transition-all duration-300 w-full">
                <SidebarBalance />
              </div>
              <div className="flex justify-center group-hover/sidebar:justify-start group-hover/sidebar:px-3 transition-all duration-300">
                <AnimatedThemeToggler
                  checked={isDark}
                  onToggle={toggleTheme}
                  aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                  title={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                  className="flex h-12 w-12 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)] flex-shrink-0"
                />
                <span className="hidden group-hover/sidebar:block text-sm font-bold text-[var(--admin-muted)] self-center mr-3 truncate whitespace-nowrap">
                  {isDark ? 'الوضع الفاتح' : 'الوضع الداكن'}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setIsThemeSettingsOpen(true)}
                className="flex h-12 w-full items-center justify-center group-hover/sidebar:justify-start group-hover/sidebar:px-4 rounded-full text-[var(--admin-muted)] transition-all duration-300 gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
                aria-label="إعدادات الثيم"
                title="إعدادات الثيم"
              >
                <Settings className="h-5 w-5 flex-shrink-0" />
                <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
                  تخصيص المظهر
                </span>
              </button>
              <button
                type="button"
                onClick={handleLogout}
                className="flex h-12 w-full items-center justify-center group-hover/sidebar:justify-start group-hover/sidebar:px-4 rounded-full text-[var(--admin-danger)] transition-all duration-300 gap-3 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
                title="تسجيل الخروج"
                aria-label="تسجيل الخروج"
              >
                <LogOut className="h-5 w-5 flex-shrink-0" />
                <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
                  تسجيل الخروج
                </span>
              </button>
            </div>
          </motion.aside>
        )}
      </AnimatePresence>

      <main
        className={`relative z-10 h-dvh overflow-y-auto overscroll-none ${
          isFocusMode
            ? 'px-0 py-0 pb-0 lg:mr-0 lg:px-0 lg:py-0 lg:pb-0'
            : 'px-4 py-6 pb-20 lg:mr-24 lg:px-8 lg:py-10 lg:pb-10'
        }`}
      >
        <AnimatePresence>
          {!isFocusMode && (
            <motion.header
              initial={{ opacity: 0, y: -20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              transition={{ duration: 0.3 }}
              className="mb-8 lg:mb-10"
            >
              <div className="flex items-center justify-between w-full">
                <nav className="flex items-center gap-2 text-xs font-medium uppercase tracking-[0.3em] text-[var(--admin-muted)]">
                  <span>المساحة الدراسية</span>
                  <ChevronLeft className="h-3 w-3" />
                  <span className="text-[var(--admin-primary-strong)]">لوحة الطالب</span>
                </nav>
                {/* Desktop-only header actions */}
                <div className="hidden lg:flex items-center gap-3">
                  <SidebarBalance />
                  <AnimatedThemeToggler
                    checked={isDark}
                    onToggle={toggleTheme}
                    aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                    className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                  />
                  <button
                    type="button"
                    onClick={() => setIsThemeSettingsOpen(true)}
                    className="hover:scale-105 transition duration-300"
                    title="تخصيص الحساب"
                  >
                    <UserAvatar
                      avatarSlug={user?.avatarSlug}
                      fullName={user?.fullName}
                      size="sm"
                    />
                  </button>
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
              <p className="text-[11px] font-black tracking-[0.26em] text-[var(--admin-footer)]">
                أكاديمية نادر جورج
              </p>
            </motion.footer>
          )}
        </AnimatePresence>
      </main>

      {/* ── Mobile Bottom Nav (compact: 3 primary + menu) ─────────────── */}
      <AnimatePresence>
        {!isFocusMode && (
          <motion.nav
            initial={shouldReduceMotion ? false : { y: '100%' }}
            animate={{ y: 0 }}
            exit={shouldReduceMotion ? { opacity: 0 } : { y: '100%' }}
            transition={shouldReduceMotion ? { duration: 0 } : { type: 'spring', stiffness: 300, damping: 30 }}
            className="fixed inset-x-0 bottom-0 z-40 bg-[var(--admin-sidebar)]/95 backdrop-blur-xl px-3 pb-[max(0.5rem,env(safe-area-inset-bottom))] pt-1.5 lg:hidden"
            role="navigation"
            aria-label="القائمة السفلية"
          >
            <div className="mx-auto flex w-full max-w-md items-center justify-around gap-1">
              {/* Home */}
              <Link
                href="/student"
                aria-current={activePath === '/student' ? 'page' : undefined}
                className={`flex flex-col items-center justify-center gap-0.5 rounded-2xl px-3 py-1.5 text-center transition-all focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${activePath === '/student'
                  ? 'text-[var(--admin-primary)]'
                  : 'text-[var(--admin-muted)]'
                  }`}
              >
                <Home className="h-[22px] w-[22px]" />
                <span className="text-[10px] font-bold leading-none">الرئيسية</span>
              </Link>

              {/* Primary nav items */}
              {primaryNavItems.map((item) => {
                const Icon = item.icon;
                const isActive = item.href === activePath;

                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    aria-current={isActive ? 'page' : undefined}
                    className={`flex flex-col items-center justify-center gap-0.5 rounded-2xl px-3 py-1.5 text-center transition-all focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${isActive
                      ? 'text-[var(--admin-primary)]'
                      : 'text-[var(--admin-muted)]'
                      }`}
                  >
                    <Icon className="h-[22px] w-[22px]" />
                    <span className="text-[10px] font-bold leading-none">{item.label}</span>
                  </Link>
                );
              })}

              {/* Menu button — opens drawer */}
              <button
                type="button"
                onClick={() => setIsDrawerOpen(true)}
                className={`flex flex-col items-center justify-center gap-0.5 rounded-2xl px-3 py-1.5 text-center transition-all focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${isDrawerOpen
                  ? 'text-[var(--admin-primary)]'
                  : 'text-[var(--admin-muted)]'
                  }`}
                aria-label="القائمة"
              >
                <Menu className="h-[22px] w-[22px]" />
                <span className="text-[10px] font-bold leading-none">القائمة</span>
              </button>
            </div>
          </motion.nav>
        )}
      </AnimatePresence>

      {/* ── Mobile Drawer (slide from left for RTL) ────────────────────── */}
      <AnimatePresence>
        {isDrawerOpen && (
          <>
            {/* Backdrop */}
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.2 }}
              className="fixed inset-0 z-[60] bg-black/40 backdrop-blur-sm lg:hidden"
              onClick={closeDrawer}
              aria-hidden
            />

            {/* Drawer panel — slides from the right (RTL) */}
            <motion.div
              initial={{ x: '100%' }}
              animate={{ x: 0 }}
              exit={{ x: '100%' }}
              transition={{ type: 'spring', stiffness: 400, damping: 35 }}
              className="fixed top-0 right-0 z-[70] h-full w-72 bg-[var(--admin-sidebar)] shadow-[-20px_0_60px_var(--admin-shadow)] lg:hidden"
              role="dialog"
              aria-modal="true"
              aria-label="القائمة الجانبية"
            >
              <div className="flex h-full flex-col py-6 px-5">
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
                    className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                    aria-label="إغلاق القائمة"
                  >
                    <X className="h-5 w-5" />
                  </button>
                </div>

                {/* Balance card */}
                <div className="mb-5 rounded-2xl bg-[var(--admin-card-soft)] p-4">
                  <SidebarBalance />
                </div>

                {/* Secondary nav links */}
                <nav className="flex-1 space-y-1">
                  <p className="mb-2 text-[10px] font-black uppercase tracking-[0.2em] text-[var(--admin-muted)]">
                    صفحات تانية
                  </p>
                  {secondaryNavItems.map((item) => {
                    const Icon = item.icon;
                    const isActive = item.href === activePath;

                    return (
                      <Link
                        key={item.href}
                        href={item.href}
                        onClick={closeDrawer}
                        className={`flex items-center gap-3 rounded-2xl px-4 py-3 text-sm font-bold transition-all ${isActive
                          ? 'bg-[var(--admin-card-strong)] text-[var(--admin-primary)]'
                          : 'text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'
                          }`}
                      >
                        <Icon className="h-5 w-5 shrink-0" />
                        <span>{item.label}</span>
                      </Link>
                    );
                  })}
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

                  {/* Theme settings */}
                  <button
                    type="button"
                    onClick={() => {
                      closeDrawer();
                      setIsThemeSettingsOpen(true);
                    }}
                    className="flex w-full items-center gap-3 rounded-2xl px-4 py-3 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
                  >
                    <Settings className="h-5 w-5 shrink-0" />
                    <span>إعدادات الألوان</span>
                  </button>

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
            </motion.div>
          </>
        )}
      </AnimatePresence>

      <StudentThemeSettingsPanel
        open={isThemeSettingsOpen}
        onOpenChange={setIsThemeSettingsOpen}
        selectedLightPaletteId={selectedLightPaletteId}
        selectedDarkPaletteId={selectedDarkPaletteId}
        currentMode={mode === 'dark' ? 'dark' : 'light'}
        isSaving={isSavingPreferences}
        onSelectPalette={(paletteMode, paletteId) => {
          void updatePalette(paletteMode, paletteId);
        }}
        selectedAvatarSlug={user?.avatarSlug}
        onSelectAvatar={(slug) => {
          void updateAvatar(slug);
        }}
      />
    </div>
  );
}
