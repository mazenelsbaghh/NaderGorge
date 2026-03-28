'use client';

import { ReactNode, useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  BookOpenText,
  ChevronLeft,
  Home,
  KeyRound,
  LogOut,
  Settings,
  Shield,
  Star,
  UserCog,
  Wrench,
} from 'lucide-react';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useAuthStore } from '@/stores/auth-store';
import { RippleGrid } from '@/components/ui/ripple-grid';
import { AdminBreadcrumbs } from './AdminBreadcrumbs';

type AdminShellRoute =
  | '/admin'
  | '/admin/users'
  | '/admin/content'
  | '/admin/codes'
  | '/admin/questions'
  | '/admin/overrides';

type AdminShellChromeProps = {
  activePath: AdminShellRoute;
  sectionLabel: string;
  pageTitle: string;
  subtitle?: string;
  action?: ReactNode;
  headerAccessory?: ReactNode;
  subNav?: ReactNode;
  children: ReactNode;
  floatingAction?: ReactNode;
};

const navItems: Array<{
  href: AdminShellRoute;
  label: string;
  icon: typeof UserCog;
}> = [
    { href: '/admin/users', label: 'المستخدمين', icon: UserCog },
    { href: '/admin/content', label: 'المحتوى', icon: BookOpenText },
    { href: '/admin/codes', label: 'الأكواد', icon: KeyRound },
    { href: '/admin/questions', label: 'الأسئلة', icon: Shield },
    { href: '/admin/overrides', label: 'التعديلات', icon: Wrench },
  ];

export function AdminShellChrome({
  activePath,
  sectionLabel,
  pageTitle,
  subtitle,
  action,
  headerAccessory,
  subNav,
  children,
  floatingAction,
}: AdminShellChromeProps) {
  const router = useRouter();
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const { isDark, themeVars, toggleTheme } = useAdminTheme();
  const [showBackdrop, setShowBackdrop] = useState(false);

  useRootOverscrollBackground(themeVars);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setShowBackdrop(true);
    }, 180);

    return () => {
      window.clearTimeout(timer);
    };
  }, []);

  const handleLogout = () => {
    clearAuth();
    router.replace('/login');
  };

  return (
    <div
      dir="rtl"
      className="h-dvh overflow-hidden text-[var(--admin-text)] relative"
      style={{
        ...themeVars,
        backgroundColor: 'var(--admin-bg)',
      }}
    >
      <div className="absolute inset-0 z-0 pointer-events-none bg-[radial-gradient(circle_at_top,rgba(212,167,98,0.12),transparent_48%),linear-gradient(180deg,transparent,rgba(212,167,98,0.04))]" />
      <div className={`absolute inset-0 z-0 pointer-events-none transition-opacity duration-300 ${showBackdrop ? 'opacity-100' : 'opacity-0'}`}>
        {showBackdrop ? (
          <RippleGrid
            gridColor={isDark ? '#c5a059' : '#d4a762'}
            rippleIntensity={0.035}
            gridSize={10}
            gridThickness={isDark ? 14 : 11}
            mouseInteraction={false}
            mouseInteractionRadius={1.2}
            opacity={isDark ? 0.52 : 0.24}
            animationSpeed={0.22}
          />
        ) : null}
      </div>
      <aside className="fixed right-0 top-0 z-50 hidden h-full w-20 flex-col justify-between bg-[var(--admin-sidebar)] py-6 shadow-[-12px_0_40px_var(--admin-shadow)] lg:flex" role="navigation" aria-label="القائمة الرئيسية">
        <div className="space-y-7">
          <div className="flex justify-center">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-lg">
              <BookOpenText className="h-5 w-5" />
            </div>
          </div>

          <nav className="space-y-3 px-3">
            <Link
              href="/admin"
              aria-label="الرئيسية"
              aria-current={activePath === '/admin' ? 'page' : undefined}
              className={`flex h-12 items-center justify-center rounded-full transition ${activePath === '/admin'
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
                  className={`flex h-12 items-center justify-center rounded-full transition ${isActive
                    ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                    : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                  title={item.label}
                  aria-label={item.label}
                  aria-current={isActive ? 'page' : undefined}
                >
                  <Icon className="h-5 w-5" />
                </Link>
              );
            })}

          </nav>
        </div>

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
            className="flex h-12 w-full items-center justify-center rounded-full text-[var(--admin-danger)] transition hover:bg-[var(--admin-hover)]"
            title="تسجيل الخروج"
            aria-label="تسجيل الخروج"
          >
            <LogOut className="h-5 w-5" />
          </button>
        </div>
      </aside>

      <main className="relative z-10 h-dvh overflow-y-auto overscroll-none px-5 py-8 pb-32 lg:mr-24 lg:px-8 lg:py-10 lg:pb-10">
        <header className="mb-12 flex flex-col gap-6 md:flex-row md:items-end md:justify-between">
          <div>
          <AdminBreadcrumbs />

            <div className="flex flex-wrap items-center gap-4">
              <div>
                <h1 className="mb-2 text-4xl font-extrabold tracking-tight text-[var(--admin-text)] lg:text-5xl">
                  {pageTitle}
                </h1>
                {subtitle ? <p className="font-medium text-[var(--admin-muted)]">{subtitle}</p> : null}
              </div>
              {headerAccessory}
            </div>
          </div>

          {action}
        </header>

        {subNav ? <div className="mb-8">{subNav}</div> : null}

        {children}

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
            شيخ المتحف
          </p>
        </footer>
      </main>

      <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-[var(--admin-border)] bg-[var(--admin-sidebar)]/90 px-3 py-3 backdrop-blur-xl lg:hidden" role="navigation" aria-label="القائمة السفلية">
        <div className="flex w-full gap-2 overflow-x-auto overscroll-x-contain pb-1 [&::-webkit-scrollbar]:hidden [-ms-overflow-style:none] [scrollbar-width:none]">
          <Link
            href="/admin"
            aria-current={activePath === '/admin' ? 'page' : undefined}
            className={`flex shrink-0 w-[4.5rem] flex-col items-center justify-center gap-1.5 rounded-[20px] p-2 text-center text-[10px] font-black transition-all ${activePath === '/admin'
              ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md border border-transparent'
              : 'bg-[var(--admin-card)] text-[var(--admin-muted)] border border-[var(--admin-border)]'
              }`}
          >
            <Home className="h-5 w-5" />
            <span className="truncate w-full" style={{ lineHeight: 1 }}>الرئيسية</span>
          </Link>

          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = item.href === activePath;

            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={isActive ? 'page' : undefined}
                className={`flex shrink-0 w-[4.5rem] flex-col items-center justify-center gap-1.5 rounded-[20px] p-2 text-center text-[10px] font-black transition-all ${isActive
                  ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md border border-transparent'
                  : 'bg-[var(--admin-card)] text-[var(--admin-muted)] border border-[var(--admin-border)]'
                  }`}
              >
                <Icon className="h-5 w-5" />
                <span className="truncate w-full" style={{ lineHeight: 1 }}>{item.label}</span>
              </Link>
            );
          })}
        </div>
      </nav>

      {floatingAction}
    </div>
  );
}
