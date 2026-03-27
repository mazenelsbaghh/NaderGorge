'use client';

/**
 * StudentShellChrome — mirrors AdminShellChrome exactly.
 *
 * Structure:
 *  - Fixed vertical icon sidebar (desktop, right side for RTL)
 *  - Bottom nav bar (mobile)
 *  - Dot-grid background with ambient overlay
 *  - Theme toggle (light / dark) shared with admin via useAdminTheme
 *  - Breadcrumb + header section
 *  - Footer branding
 *
 * Token source: useAdminTheme() → same --admin-* CSS vars as admin pages.
 */

import { ReactNode } from 'react';
import Link from 'next/link';
import { useRouter, usePathname } from 'next/navigation';
import {
  BookMarked,
  BookOpenText,
  ChartNoAxesColumn,
  ChevronLeft,
  GraduationCap,
  Home,
  KeyRound,
  LogOut,
  Settings,
  Shield,
  Star,
} from 'lucide-react';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useAuthStore } from '@/stores/auth-store';

/* ── Route type safety ──────────────────────────────────────────────── */

type StudentShellRoute =
  | '/student'
  | '/student/packages'
  | '/student/code-redemption';

type StudentShellChromeProps = {
  children: ReactNode;
};

/* ── Nav items (icon sidebar) ───────────────────────────────────────── */

const navItems: Array<{
  href: StudentShellRoute;
  label: string;
  icon: typeof ChartNoAxesColumn;
}> = [
  { href: '/student/packages',        label: 'باقاتي',       icon: BookMarked },
  { href: '/student/code-redemption', label: 'تفعيل كود',    icon: KeyRound },
];

/* ── Component ──────────────────────────────────────────────────────── */

export function StudentShellChrome({ children }: StudentShellChromeProps) {
  const router = useRouter();
  const pathname = usePathname();
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const { isDark, themeVars, toggleTheme } = useAdminTheme();

  useRootOverscrollBackground(themeVars);

  const handleLogout = () => {
    clearAuth();
    router.replace('/login');
  };

  /* Which top-level route is active? */
  const activePath: StudentShellRoute =
    pathname.startsWith('/student/packages')
      ? '/student/packages'
      : pathname.startsWith('/student/code-redemption')
        ? '/student/code-redemption'
        : '/student';

  return (
    <div
      dir="rtl"
      className="h-dvh overflow-hidden text-[var(--admin-text)]"
      style={{
        ...themeVars,
        backgroundImage:
          'radial-gradient(circle at 2px 2px, var(--admin-dot) 1px, transparent 0), linear-gradient(var(--admin-bg-overlay), var(--admin-bg-overlay))',
        backgroundSize: '40px 40px, 100% 100%',
        backgroundColor: 'var(--admin-bg)',
      }}
    >
      {/* ═══════════════════════════════════════════════════════════════
          SIDEBAR — desktop only, fixed right (RTL), exact clone of Admin
          ═══════════════════════════════════════════════════════════════ */}
      <aside className="fixed right-0 top-0 z-50 hidden h-full w-20 flex-col justify-between bg-[var(--admin-sidebar)] py-6 shadow-[-12px_0_40px_var(--admin-shadow)] lg:flex" role="navigation" aria-label="القائمة الرئيسية">
        <div className="space-y-7">
          {/* Identity badge */}
          <div className="flex justify-center">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-lg">
              <GraduationCap className="h-5 w-5" />
            </div>
          </div>

          {/* Nav icons */}
          <nav className="space-y-3 px-3">
            {/* Home */}
            <Link
              href="/student"
              aria-label="لوحة التحكم"
              className={`flex h-12 items-center justify-center rounded-full transition ${
                activePath === '/student'
                  ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                  : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
              }`}
            >
              <Home className="h-5 w-5" />
            </Link>

            {navItems.map((item) => {
              const Icon = item.icon;
              const isActive = item.href === activePath;

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`flex h-12 items-center justify-center rounded-full transition ${
                    isActive
                      ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                      : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                  }`}
                  title={item.label}
                  aria-label={item.label}
                >
                  <Icon className="h-5 w-5" />
                </Link>
              );
            })}
          </nav>
        </div>

        {/* Bottom toolbar: theme toggle + logout */}
        <div className="space-y-3 px-3">
          <div className="flex justify-center">
            <AnimatedThemeToggler
              checked={isDark}
              onToggle={toggleTheme}
              aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
              title={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
              className="flex h-12 w-12 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
            />
          </div>
          <button className="flex h-12 w-full items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]" aria-label="الإعدادات" title="الإعدادات">
            <Settings className="h-5 w-5" />
          </button>
          <button
            onClick={handleLogout}
            className="flex h-12 w-full items-center justify-center rounded-full text-[#cf6d5b] transition hover:bg-[var(--admin-hover)]"
            title="تسجيل الخروج"
            aria-label="تسجيل الخروج"
          >
            <LogOut className="h-5 w-5" />
          </button>
        </div>
      </aside>

      {/* ═══════════════════════════════════════════════════════════════
          MAIN CONTENT — offset for sidebar on desktop
          ═══════════════════════════════════════════════════════════════ */}
      <main className="h-dvh overflow-y-auto overscroll-none px-5 py-8 pb-28 lg:mr-24 lg:px-8 lg:py-10 lg:pb-10">
        {/* Header breadcrumb */}
        <header className="mb-10">
          <nav className="mb-3 flex items-center gap-2 text-xs font-medium uppercase tracking-[0.3em] text-[var(--admin-muted)]">
            <span>الباحث التحريري</span>
            <ChevronLeft className="h-3 w-3" />
            <span className="text-[var(--admin-primary-strong)]">بوابة الطالب</span>
          </nav>
        </header>

        {children}

        {/* Footer — identical to Admin */}
        <footer className="mt-20 flex flex-col items-center opacity-40 select-none">
          <div className="mb-8 h-px w-full bg-gradient-to-r from-transparent via-[var(--admin-footer)] to-transparent" />
          <div className="flex gap-8 text-[var(--admin-footer)]">
            <Star className="h-9 w-9" />
            <BookOpenText className="h-9 w-9" />
            <Shield className="h-9 w-9" />
            <KeyRound className="h-9 w-9" />
            <Star className="h-9 w-9" />
          </div>
          <p className="mt-4 text-[10px] font-black uppercase tracking-[0.4em] text-[var(--admin-primary)]">
            الأرشيف التحريري الملكي
          </p>
        </footer>
      </main>

      {/* ═══════════════════════════════════════════════════════════════
          BOTTOM NAV — mobile only, matches Admin styling
          ═══════════════════════════════════════════════════════════════ */}
      <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-[var(--admin-border)] bg-[var(--admin-sidebar)] px-2 py-2 backdrop-blur lg:hidden" role="navigation" aria-label="القائمة السفلية">
        <div className="mx-auto grid w-full max-w-xl grid-cols-3 gap-2 rounded-[26px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-2 shadow-[0_-18px_40px_var(--admin-shadow)]">
          {/* Home */}
          <Link
            href="/student"
            className={`flex min-w-0 flex-col items-center justify-center gap-1 rounded-[20px] px-1.5 py-2.5 text-center text-[11px] font-black transition-all ${
              activePath === '/student'
                ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)]'
                : 'text-[var(--admin-muted)]'
            }`}
          >
            <Home className="h-5 w-5" />
            <span className="truncate">لوحة التحكم</span>
          </Link>

          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = item.href === activePath;

            return (
              <Link
                key={item.href}
                href={item.href}
                className={`flex min-w-0 flex-col items-center justify-center gap-1 rounded-[20px] px-1.5 py-2.5 text-center text-[11px] font-black transition-all ${
                  isActive
                    ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)]'
                    : 'text-[var(--admin-muted)]'
                }`}
              >
                <Icon className="h-5 w-5" />
                <span className="truncate">{item.label}</span>
              </Link>
            );
          })}
        </div>
      </nav>
    </div>
  );
}
