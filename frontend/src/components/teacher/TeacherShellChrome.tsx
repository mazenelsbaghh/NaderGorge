'use client';

import { ReactNode, useEffect, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  Home,
  Users,
  BookOpenText,
  KeyRound,
  Shield,
  GraduationCap,
  Coins,
  User,
  MessageSquareText,
  LogOut,
  Menu,
  X,
} from 'lucide-react';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useAuthStore } from '@/stores/auth-store';
import dynamic from 'next/dynamic';

const RippleGrid = dynamic(() => import('@/components/ui/ripple-grid').then(mod => ({ default: mod.RippleGrid })), { ssr: false });

type TeacherShellRoute =
  | '/teacher'
  | '/teacher/activity'
  | '/teacher/packages'
  | '/teacher/codes'
  | '/teacher/exams'
  | '/teacher/essays'
  | '/teacher/students'
  | '/teacher/finance'
  | '/teacher/profile'
  | '/teacher/chat';

type TeacherShellChromeProps = {
  activePath: TeacherShellRoute;
  sectionLabel: string;
  pageTitle: string;
  subtitle?: string;
  action?: ReactNode;
  headerAccessory?: ReactNode;
  children: ReactNode;
};

const navItems: Array<{
  href: TeacherShellRoute;
  label: string;
  icon: any;
}> = [
  {
    href: '/teacher',
    label: 'الرئيسية',
    icon: Home,
  },
  {
    href: '/teacher/activity',
    label: 'نشاط الطلاب',
    icon: Users,
  },
  {
    href: '/teacher/packages',
    label: 'المحتوى الدراسي',
    icon: BookOpenText,
  },
  {
    href: '/teacher/codes',
    label: 'أكواد الوصول',
    icon: KeyRound,
  },
  {
    href: '/teacher/exams',
    label: 'الاختبارات والأسئلة',
    icon: Shield,
  },
  {
    href: '/teacher/essays',
    label: 'تصحيح المقالي',
    icon: GraduationCap,
  },
  {
    href: '/teacher/students',
    label: 'قائمة الطلاب',
    icon: Users,
  },
  {
    href: '/teacher/finance',
    label: 'المالية والأرباح',
    icon: Coins,
  },
  {
    href: '/teacher/profile',
    label: 'الملف الشخصي',
    icon: User,
  },
  {
    href: '/teacher/chat',
    label: 'التواصل الداخلي',
    icon: MessageSquareText,
  },
];

export function TeacherShellChrome({
  activePath,
  sectionLabel,
  pageTitle,
  subtitle,
  action,
  headerAccessory,
  children,
}: TeacherShellChromeProps) {
  const router = useRouter();
  const clearAuth = useAuthStore((state) => state.clearAuth);
  const user = useAuthStore((state) => state.user);
  const { isDark, themeVars } = useAdminTheme();
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

  return (
    <div
      dir="rtl"
      className="h-dvh overflow-hidden bg-[var(--admin-bg)] text-[var(--admin-text)] relative"
      style={themeVars}
    >
      <div className="absolute inset-0 z-0 pointer-events-none bg-[radial-gradient(circle_at_top,rgba(148,163,184,0.16),transparent_48%),linear-gradient(180deg,transparent,rgba(100,116,139,0.06))]" />
      <div
        className={`absolute inset-0 z-0 pointer-events-none transition-opacity duration-300 ${showBackdrop ? 'opacity-100' : 'opacity-0'}`}
      >
        {showBackdrop ? (
          <RippleGrid
            gridColor={isDark ? '#64748b' : '#94a3b8'}
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

      {/* Desktop Sidebar */}
      <aside
        className="fixed right-0 top-0 z-50 hidden h-full w-20 flex-col justify-between bg-[var(--admin-sidebar)] py-6 shadow-[-12px_0_40px_var(--admin-shadow)] lg:flex group/sidebar transition-all duration-300 ease-in-out hover:w-64"
        role="navigation"
        aria-label="قائمة المعلم"
      >
        <div className="flex flex-col flex-1 min-h-0">
          <div className="flex justify-start pr-5 items-center transition-all duration-300 mb-7 flex-shrink-0">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-lg flex-shrink-0">
              <BookOpenText className="h-5 w-5" />
            </div>
            <span className="hidden group-hover/sidebar:block text-sm font-bold text-[var(--admin-text)] self-center mr-3 truncate whitespace-nowrap">
              لوحة المعلم
            </span>
          </div>

          <nav className="space-y-3 px-3 overflow-y-auto flex-1 min-h-0 [scrollbar-width:none] [-ms-overflow-style:none] [&::-webkit-scrollbar]:hidden">
            {navItems.map((item) => {
              const Icon = item.icon;
              const isActive = item.href === activePath;

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  prefetch={false}
                  className={`flex h-12 items-center justify-start pr-[18px] pl-4 rounded-full transition-all duration-300 gap-3 ${
                    isActive
                      ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                      : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                  }`}
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

        {/* User Info & Settings footer */}
        <div className="px-3 flex flex-col gap-4 flex-shrink-0">
          <div className="flex items-center gap-3 pr-[14px] py-2">
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-[var(--admin-primary-15)] border border-[var(--admin-border)] text-sm font-extrabold text-[var(--admin-primary)] shadow-sm shrink-0">
              {user?.fullName ? user.fullName[0].toUpperCase() : 'T'}
            </div>
            <div className="hidden group-hover/sidebar:block min-w-0">
              <p className="text-xs font-black text-[var(--admin-text)] truncate">{user?.fullName ?? 'معلم'}</p>
              <p className="text-[10px] text-[var(--admin-muted)] truncate">{user?.phone ?? ''}</p>
            </div>
          </div>

          <div className="flex group-hover/sidebar:flex-row flex-col items-center justify-between gap-2 border-t border-[var(--admin-border)] pt-4">
            <AnimatedThemeToggler />
            <button
              onClick={handleLogout}
              className="flex h-10 w-10 items-center justify-center rounded-full text-rose-500 hover:bg-rose-500/10 transition-colors"
              title="تسجيل الخروج"
            >
              <LogOut className="h-5 w-5" />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Workspace Frame */}
      <div className="flex h-full flex-col lg:mr-24">
        {/* Header */}
        <header className="flex h-16 items-center justify-between border-b border-[var(--admin-border)] bg-[var(--admin-bg-overlay)] px-6 backdrop-blur-md z-40">
          <div className="flex items-center gap-4">
            <button
              onClick={() => setIsMobileMenuOpen(true)}
              className="rounded-lg p-1.5 text-[var(--admin-text)] hover:bg-[var(--admin-hover)] lg:hidden"
              aria-label="فتح القائمة"
            >
              <Menu className="h-6 w-6" />
            </button>
            <div className="hidden sm:block">
              <span className="text-[10px] font-black tracking-wider text-[var(--admin-primary)] uppercase">
                {sectionLabel}
              </span>
              <h1 className="text-lg font-black tracking-tight text-[var(--admin-text)]">
                {pageTitle}
              </h1>
            </div>
          </div>

          <div className="flex items-center gap-4">
            {headerAccessory}
            {action}
            <div className="sm:hidden text-left">
              <h1 className="text-sm font-black tracking-tight text-[var(--admin-text)]">
                {pageTitle}
              </h1>
            </div>
          </div>
        </header>

        {/* Main Scrolling Body */}
        <main className="flex-1 overflow-y-auto px-6 py-8 relative z-10 [scrollbar-width:thin] scrollbar-color-[var(--admin-border)]-transparent">
          {subtitle && (
            <div className="mb-6 max-w-3xl">
              <p className="text-sm text-[var(--admin-muted)] leading-relaxed">
                {subtitle}
              </p>
            </div>
          )}
          {children}

          <footer className="mt-14 flex flex-col items-center opacity-35 select-none">
            <div className="mb-4 h-px w-full bg-gradient-to-r from-transparent via-[var(--admin-footer)] to-transparent" />
            <p className="text-[10px] font-black uppercase tracking-[0.34em] text-[var(--admin-primary)]">
              شيخ المتحف
            </p>
          </footer>
        </main>
      </div>

      {/* Mobile Drawer Menu */}
      {isMobileMenuOpen && (
        <div className="fixed inset-0 z-50 flex lg:hidden" role="dialog" aria-modal="true">
          <div
            onClick={() => setIsMobileMenuOpen(false)}
            className="fixed inset-0 bg-black/60 backdrop-blur-sm transition-opacity duration-300"
          />
          <aside className="relative flex w-64 max-w-xs flex-col bg-[var(--admin-sidebar)] py-6 px-4 shadow-[12px_0_40px_rgba(0,0,0,0.4)] animate-[slideInRight_0.22s_ease-out] mr-auto">
            <div className="flex items-center justify-between mb-8 pr-2">
              <div className="flex items-center gap-3">
                <div className="flex h-9 w-9 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-white shadow-md">
                  <BookOpenText className="h-5 w-5" />
                </div>
                <span className="text-sm font-bold text-[var(--admin-text)]">لوحة المعلم</span>
              </div>
              <button
                onClick={() => setIsMobileMenuOpen(false)}
                className="rounded-lg p-1 text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"
              >
                <X className="h-6 w-6" />
              </button>
            </div>

            <nav className="flex-1 space-y-2 overflow-y-auto">
              {navItems.map((item) => {
                const Icon = item.icon;
                const isActive = item.href === activePath;

                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={() => setIsMobileMenuOpen(false)}
                    className={`flex h-11 items-center rounded-full pr-4 pl-3 gap-3 transition-all ${
                      isActive
                        ? 'bg-[var(--admin-primary)] text-white shadow-md'
                        : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                  >
                    <Icon className="h-5 w-5" />
                    <span className="text-sm font-bold">{item.label}</span>
                  </Link>
                );
              })}
            </nav>

            <div className="border-t border-[var(--admin-border)] pt-4 mt-auto flex justify-between items-center pr-2">
              <AnimatedThemeToggler />
              <button
                onClick={handleLogout}
                className="flex h-10 w-10 items-center justify-center rounded-full text-rose-500 hover:bg-rose-500/10"
              >
                <LogOut className="h-5 w-5" />
              </button>
            </div>
          </aside>
        </div>
      )}
    </div>
  );
}
