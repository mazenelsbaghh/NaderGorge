'use client';

import { ReactNode } from 'react';
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

  useRootOverscrollBackground(themeVars);

  const handleLogout = () => {
    clearAuth();
    router.replace('/login');
  };

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
              className={`flex h-12 items-center justify-center rounded-full transition ${
                activePath === '/admin'
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

      <main className="h-dvh overflow-y-auto overscroll-none px-5 py-8 lg:mr-24 lg:px-8 lg:py-10">
        <header className="mb-12 flex flex-col gap-6 md:flex-row md:items-end md:justify-between">
          <div>
            <nav className="mb-3 flex items-center gap-2 text-xs font-medium uppercase tracking-[0.3em] text-[var(--admin-muted)]">
              <span>الباحث التحريري</span>
              <ChevronLeft className="h-3 w-3" />
              <span className="text-[var(--admin-primary-strong)]">{sectionLabel}</span>
            </nav>

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
            الأرشيف التحريري الملكي
          </p>
        </footer>
      </main>

      {floatingAction}
    </div>
  );
}
