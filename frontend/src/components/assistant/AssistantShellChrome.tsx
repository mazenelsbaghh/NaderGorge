'use client';

import { ReactNode, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  Home,
  ClipboardList,
  PhoneCall,
  MessageSquareText,
  Calendar,
  Compass,
  Bell,
  LogOut,
  Menu,
  X,
  BookOpenText,
  Shield,
  Star,
  BookOpen,
  Headphones,
} from 'lucide-react';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useAuthStore } from '@/stores/auth-store';
import { useHasPermission } from '@/hooks/useHasPermission';

type AssistantShellRoute =
  | '/assistant/dashboard'
  | '/assistant/tasks'
  | '/assistant/crm'
  | '/assistant/chat'
  | '/assistant/live-support'
  | '/assistant/attendance'
  | '/assistant/vacations'
  | '/assistant/notifications'
  | '/admin/content'
  | '/admin/community'
  | '/admin/questions'
  | '/admin/watch-requests';

type AssistantShellChromeProps = {
  activePath: AssistantShellRoute;
  sectionLabel: string;
  pageTitle: string;
  subtitle?: string;
  action?: ReactNode;
  headerAccessory?: ReactNode;
  children: ReactNode;
};

const navItems: Array<{
  href: AssistantShellRoute;
  label: string;
  icon: any;
  permission?: string;
}> = [
  {
    href: '/assistant/dashboard',
    label: 'الرئيسية',
    icon: Home,
  },
  {
    href: '/assistant/tasks',
    label: 'المهام والعمليات',
    icon: ClipboardList,
    permission: 'tasks.manage',
  },
  {
    href: '/admin/content',
    label: 'إدارة تعليقات الدروس',
    icon: BookOpen,
    permission: 'comments.manage',
  },
  {
    href: '/admin/community',
    label: 'إدارة مجتمع الطلاب',
    icon: MessageSquareText,
    permission: 'community.manage',
  },
  {
    href: '/admin/questions',
    label: 'إدارة الامتحانات والأسئلة',
    icon: Shield,
    permission: 'exams.manage',
  },
  {
    href: '/admin/watch-requests',
    label: 'طلبات إعادة المشاهدة',
    icon: Star,
    permission: 'watch_requests.manage',
  },
  {
    href: '/assistant/crm',
    label: 'الكول سنتر (CRM)',
    icon: PhoneCall,
    permission: 'crm.manage',
  },
  {
    href: '/assistant/live-support',
    label: 'الدعم المباشر',
    icon: Headphones,
  },
  {
    href: '/assistant/chat',
    label: 'التواصل الداخلي',
    icon: MessageSquareText,
    permission: 'chat.manage',
  },
  {
    href: '/assistant/attendance',
    label: 'سجل الحضور',
    icon: Calendar,
  },
  {
    href: '/assistant/vacations',
    label: 'طلبات الإجازة',
    icon: Compass,
  },
  {
    href: '/assistant/notifications',
    label: 'الإشعارات',
    icon: Bell,
  },
];

export function AssistantShellChrome({
  activePath,
  sectionLabel,
  pageTitle,
  subtitle,
  action,
  headerAccessory,
  children,
}: AssistantShellChromeProps) {
  const router = useRouter();
  const logout = useAuthStore((state) => state.logout);
  const user = useAuthStore((state) => state.user);
  const { hasPermission } = useHasPermission();
  const { themeVars } = useAdminTheme();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  useRootOverscrollBackground();

  let filteredNavItems = navItems.filter((item) =>
    !item.permission || hasPermission(item.permission)
  );

  const allowedNavbarItems = user?.allowedNavbarItems;
  if (allowedNavbarItems && allowedNavbarItems.length > 0) {
    filteredNavItems = filteredNavItems.filter((item) =>
      allowedNavbarItems.includes(item.href)
    );
  }

  const getWorkspaceLabel = () => {
    const roles = user?.roles || [];
    if (roles.includes('Supervisor')) return 'مساحة المشرفين';
    if (roles.includes('Staff')) return 'مساحة الموظفين';
    return 'مساحة المساعدين';
  };

  const handleLogout = () => {
    void logout().finally(() => {
      router.replace('/login');
    });
  };

  return (
    <div
      dir="rtl"
      className="h-dvh overflow-hidden bg-[var(--admin-bg)] text-[var(--admin-text)] relative"
      style={themeVars}
    >

      {/* Desktop Sidebar */}
      <aside
        className="fixed right-0 top-0 z-50 hidden h-full w-20 flex-col justify-between bg-[var(--admin-sidebar)] py-6 shadow-[-12px_0_40px_var(--admin-shadow)] lg:flex group/sidebar transition-all duration-300 ease-in-out hover:w-64"
        role="navigation"
        aria-label="قائمة المساعد"
      >
        <div className="flex flex-col flex-1 min-h-0">
          <div className="flex justify-start pr-5 items-center transition-all duration-300 mb-7 flex-shrink-0">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-lg flex-shrink-0">
              <BookOpenText className="h-5 w-5" />
            </div>
            <span className="hidden group-hover/sidebar:block text-sm font-bold text-[var(--admin-text)] self-center mr-3 truncate whitespace-nowrap">
              {getWorkspaceLabel()}
            </span>
          </div>

          <nav className="space-y-3 px-3 overflow-y-auto flex-1 min-h-0 [scrollbar-width:none] [-ms-overflow-style:none] [&::-webkit-scrollbar]:hidden">
            {filteredNavItems.map((item) => {
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
              {user?.fullName ? user.fullName[0].toUpperCase() : 'A'}
            </div>
            <div className="hidden group-hover/sidebar:block min-w-0">
              <p className="text-xs font-black text-[var(--admin-text)] truncate">{user?.fullName ?? 'مساعد'}</p>
              <p className="text-xs text-[var(--admin-muted)] truncate">{user?.phone ?? ''}</p>
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
              <span className="text-xs font-bold text-[var(--admin-primary)]">
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

          <footer className="mt-14 flex flex-col items-center opacity-60 select-none">
            <div className="mb-4 h-px w-full bg-[var(--admin-border)]" />
            <p className="text-xs font-bold text-[var(--admin-muted)]">
              منصة مسار
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
                <span className="text-sm font-bold text-[var(--admin-text)]">{getWorkspaceLabel()}</span>
              </div>
              <button
                onClick={() => setIsMobileMenuOpen(false)}
                className="rounded-lg p-1 text-[var(--admin-text)] hover:bg-[var(--admin-hover)]"
              >
                <X className="h-6 w-6" />
              </button>
            </div>

            <nav className="flex-1 space-y-2 overflow-y-auto">
              {filteredNavItems.map((item) => {
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
