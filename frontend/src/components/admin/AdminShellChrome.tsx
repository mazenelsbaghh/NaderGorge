'use client';

import { ReactNode, useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  BookOpenText,
  ClipboardList,
  Home,
  KeyRound,
  LogOut,
  Menu,
  MessageSquareText,
  Settings,
  Shield,
  Sparkles,
  Star,
  UserCog,
  Wrench,
  X,
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
  | '/admin/ai-monitor'
  | '/admin/codes'
  | '/admin/community'
  | '/admin/questions'
  | '/admin/overrides'
  | '/admin/watch-requests'
  | '/admin/forms';

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
    { href: '/admin/community', label: 'المجتمع', icon: MessageSquareText },
    { href: '/admin/ai-monitor', label: 'تحليل AI', icon: Sparkles },
    { href: '/admin/codes', label: 'الأكواد', icon: KeyRound },
    { href: '/admin/questions', label: 'الأسئلة', icon: Shield },
    { href: '/admin/overrides', label: 'التعديلات', icon: Wrench },
    { href: '/admin/watch-requests', label: 'طلبات المشاهدة', icon: Star },
    { href: '/admin/forms', label: 'النماذج', icon: ClipboardList },
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
  const { isDark, toggleTheme } = useAdminTheme();
  const [showBackdrop, setShowBackdrop] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  useRootOverscrollBackground();

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

  const mobilePrimaryItems = navItems.slice(0, 3);
  const mobileMoreItems = navItems.slice(3);
  const isMoreActive = mobileMoreItems.some((item) => item.href === activePath);

  return (
    <div
      dir="rtl"
      className="h-dvh overflow-hidden bg-[var(--admin-bg)] text-[var(--admin-text)] relative"
    >
      <div className="absolute inset-0 z-0 pointer-events-none bg-[radial-gradient(circle_at_top,rgba(212,167,98,0.09),transparent_48%),linear-gradient(180deg,transparent,rgba(212,167,98,0.03))]" />
      <div className={`absolute inset-0 z-0 pointer-events-none transition-opacity duration-300 ${showBackdrop ? 'opacity-100' : 'opacity-0'}`}>
        {showBackdrop ? (
          <RippleGrid
            gridColor={isDark ? '#c5a059' : '#d4a762'}
            rippleIntensity={0.035}
            gridSize={10}
            gridThickness={isDark ? 14 : 11}
            mouseInteraction={false}
            mouseInteractionRadius={1.2}
            opacity={isDark ? 0.32 : 0.14}
            animationSpeed={0.22}
          />
        ) : null}
      </div>
      <aside className="fixed right-0 top-0 z-50 hidden h-full w-20 flex-col justify-between bg-[var(--admin-sidebar)] py-6 shadow-[-12px_0_40px_var(--admin-shadow)] lg:flex group/sidebar transition-all duration-300 ease-in-out hover:w-64" role="navigation" aria-label="القائمة الرئيسية">
        <div className="space-y-7">
          <div className="flex justify-start pr-5 items-center transition-all duration-300">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-lg flex-shrink-0">
              <BookOpenText className="h-5 w-5" />
            </div>
            <span className="hidden group-hover/sidebar:block text-sm font-bold text-[var(--admin-text)] self-center mr-3 truncate whitespace-nowrap">
              نادر جورج
            </span>
          </div>

          <nav className="space-y-3 px-3">
            <Link
              href="/admin"
              prefetch={false}
              aria-label="الرئيسية"
              aria-current={activePath === '/admin' ? 'page' : undefined}
              className={`flex h-12 items-center justify-start pr-[18px] pl-4 rounded-full transition-all duration-300 gap-3 ${activePath === '/admin'
                ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                }`}
            >
              <Home className="h-5 w-5 flex-shrink-0" />
              <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
                الرئيسية
              </span>
            </Link>

            {navItems.map((item) => {
              const Icon = item.icon;
              const isActive = item.href === activePath;

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  prefetch={false}
                  className={`flex h-12 items-center justify-start pr-[18px] pl-4 rounded-full transition-all duration-300 gap-3 ${isActive
                    ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
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
          <div className="flex justify-start px-1 items-center transition-all duration-300">
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
          <Link href="/admin/settings" className="flex h-12 w-full items-center justify-start pr-[18px] pl-4 rounded-full text-[var(--admin-muted)] transition-all duration-300 gap-3 hover:bg-[var(--admin-hover)]" aria-label="الإعدادات" title="الإعدادات">
            <Settings className="h-5 w-5 flex-shrink-0" />
            <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
              الإعدادات
            </span>
          </Link>
          <button
            onClick={handleLogout}
            className="flex h-12 w-full items-center justify-start pr-[18px] pl-4 rounded-full text-[var(--admin-danger)] transition-all duration-300 gap-3 hover:bg-[var(--admin-hover)]"
            title="تسجيل الخروج"
            aria-label="تسجيل الخروج"
          >
            <LogOut className="h-5 w-5 flex-shrink-0" />
            <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
              تسجيل الخروج
            </span>
          </button>
        </div>
      </aside>

      <main className="relative z-10 h-dvh overflow-y-auto overscroll-none px-4 py-6 pb-28 lg:mr-24 lg:px-7 lg:py-8 lg:pb-8">
        <header className="mb-8 flex w-full flex-col gap-4 md:flex-row md:items-end md:justify-between lg:mb-9">
          <div className="w-full">
            <div className="flex items-center justify-end gap-2 mb-4 lg:hidden w-full">
              <AnimatedThemeToggler
                checked={isDark}
                onToggle={toggleTheme}
                aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
              />
              <Link href="/admin/settings" className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]" aria-label="الإعدادات" title="الإعدادات">
                <Settings className="h-4 w-4" />
              </Link>
              <button
                onClick={handleLogout}
                className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-danger)] transition hover:bg-[var(--admin-hover)]"
                title="تسجيل الخروج"
                aria-label="تسجيل الخروج"
              >
                <LogOut className="h-4 w-4" />
              </button>
            </div>
            <AdminBreadcrumbs />

            <div className="flex flex-wrap items-center gap-3">
              <div>
                <p className="mb-1 text-[11px] font-black tracking-[0.22em] text-[var(--admin-primary)]">
                  {sectionLabel}
                </p>
                <h1 className="mb-1 text-3xl font-extrabold tracking-tight text-[var(--admin-text)] lg:text-4xl">
                  {pageTitle}
                </h1>
                {subtitle ? <p className="max-w-3xl text-sm font-medium leading-6 text-[var(--admin-muted)]">{subtitle}</p> : null}
              </div>
              {headerAccessory}
            </div>
          </div>

          {action}
        </header>

        {subNav ? <div className="mb-8">{subNav}</div> : null}

        {children}

        <footer className="mt-14 flex flex-col items-center opacity-35 select-none">
          <div className="mb-4 h-px w-full bg-gradient-to-r from-transparent via-[var(--admin-footer)] to-transparent" />
          <p className="text-[10px] font-black uppercase tracking-[0.34em] text-[var(--admin-primary)]">
            شيخ المتحف
          </p>
        </footer>
      </main>

      <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-[var(--admin-border)] bg-[var(--admin-sidebar)]/90 px-3 py-3 backdrop-blur-xl lg:hidden" role="navigation" aria-label="القائمة السفلية">
        <div className="mx-auto grid w-full max-w-md grid-cols-5 gap-2">
          <Link
            href="/admin"
            aria-current={activePath === '/admin' ? 'page' : undefined}
            className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] p-2 text-center text-[10px] font-black transition-all ${activePath === '/admin'
              ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md border border-transparent'
              : 'bg-[var(--admin-card)] text-[var(--admin-muted)] border border-[var(--admin-border)]'
              }`}
          >
            <Home className="h-5 w-5" />
            <span className="truncate w-full" style={{ lineHeight: 1 }}>الرئيسية</span>
          </Link>

          {mobilePrimaryItems.map((item) => {
            const Icon = item.icon;
            const isActive = item.href === activePath;

            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={isActive ? 'page' : undefined}
                className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] p-2 text-center text-[10px] font-black transition-all ${isActive
                  ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md border border-transparent'
                  : 'bg-[var(--admin-card)] text-[var(--admin-muted)] border border-[var(--admin-border)]'
                  }`}
              >
                <Icon className="h-5 w-5" />
                <span className="truncate w-full" style={{ lineHeight: 1 }}>{item.label}</span>
              </Link>
            );
          })}
          <button
            type="button"
            onClick={() => setIsMobileMenuOpen(true)}
            className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] border p-2 text-center text-[10px] font-black transition-all ${isMoreActive || isMobileMenuOpen
              ? 'border-transparent bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md'
              : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'
              }`}
            aria-label="المزيد من صفحات الإدارة"
          >
            <Menu className="h-5 w-5" />
            <span className="truncate w-full" style={{ lineHeight: 1 }}>المزيد</span>
          </button>
        </div>
      </nav>

      {isMobileMenuOpen ? (
        <>
          <button
            type="button"
            className="fixed inset-0 z-50 bg-black/35 backdrop-blur-sm lg:hidden"
            aria-label="إغلاق قائمة الإدارة"
            onClick={() => setIsMobileMenuOpen(false)}
          />
          <aside
            className="fixed bottom-0 right-0 z-[60] w-full rounded-t-[24px] border border-[var(--admin-border)] bg-[var(--admin-sidebar)] px-4 pb-[max(1rem,env(safe-area-inset-bottom))] pt-4 shadow-[0_-24px_70px_var(--admin-shadow)] lg:hidden"
            aria-label="قائمة الإدارة الإضافية"
          >
            <div className="mb-3 flex items-center justify-between">
              <p className="text-sm font-black text-[var(--admin-text)]">صفحات الإدارة</p>
              <button
                type="button"
                onClick={() => setIsMobileMenuOpen(false)}
                className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                aria-label="إغلاق القائمة"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="grid grid-cols-2 gap-2">
              {mobileMoreItems.map((item) => {
                const Icon = item.icon;
                const isActive = item.href === activePath;

                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={() => setIsMobileMenuOpen(false)}
                    aria-current={isActive ? 'page' : undefined}
                    className={`flex min-h-12 items-center gap-3 rounded-[16px] border px-3 py-2 text-sm font-bold transition ${isActive
                      ? 'border-transparent bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                      : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'
                      }`}
                  >
                    <Icon className="h-5 w-5 shrink-0" />
                    <span className="min-w-0 truncate">{item.label}</span>
                  </Link>
                );
              })}
              <Link
                href="/admin/settings"
                onClick={() => setIsMobileMenuOpen(false)}
                className="flex min-h-12 items-center gap-3 rounded-[16px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
              >
                <Settings className="h-5 w-5 shrink-0" />
                <span>الإعدادات</span>
              </Link>
              <button
                type="button"
                onClick={handleLogout}
                className="flex min-h-12 items-center gap-3 rounded-[16px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-danger)] transition hover:bg-[var(--admin-hover)]"
              >
                <LogOut className="h-5 w-5 shrink-0" />
                <span>تسجيل الخروج</span>
              </button>
            </div>
          </aside>
        </>
      ) : null}

      {floatingAction}
    </div>
  );
}
