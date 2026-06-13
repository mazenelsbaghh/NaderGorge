'use client';

import { ReactNode, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import {
  BookOpenText,
  Briefcase,
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
  PhoneCall,
  Video,
  BarChart3,
  Library,
  GraduationCap,
  Coins,
  Users,
  ChevronDown,
  ArrowRight,
} from 'lucide-react';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useAuthStore } from '@/stores/auth-store';
import { AdminBreadcrumbs } from './AdminBreadcrumbs';
import { useHasPermission } from '@/hooks/useHasPermission';

type AdminShellRoute =
  | '/admin'
  | '/admin/users'
  | '/admin/students'
  | '/admin/assistants'
  | '/admin/admins'
  | '/admin/teachers'
  | '/admin/content'
  | '/admin/subjects'
  | '/admin/ai-monitor'
  | '/admin/codes'
  | '/admin/community'
  | '/admin/questions'
  | '/admin/overrides'
  | '/admin/watch-requests'
  | '/admin/forms'
  | '/admin/hr'
  | '/admin/hr/my-attendance'
  | '/admin/operations'
  | '/admin/finance'
  | '/teacher'
  | '/teacher/packages'
  | '/teacher/codes'
  | '/teacher/exams'
  | '/teacher/finance'
  | '/admin/chat'
  | '/teacher/chat'
  | '/assistant/chat'
  | '/admin/crm'
  | '/assistant/crm'
  | '/admin/reports'
  | '/admin/media';

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
  permission?: string;
}> = [
  {
    href: '/admin/students',
    label: 'الطلاب',
    icon: Users,
    permission: 'users.manage',
  },
  {
    href: '/admin/assistants',
    label: 'المساعدين',
    icon: Briefcase,
    permission: 'users.manage',
  },
  {
    href: '/admin/admins',
    label: 'المديرين',
    icon: UserCog,
    permission: 'users.manage',
  },
  {
    href: '/admin/teachers',
    label: 'المعلمين',
    icon: GraduationCap,
    permission: 'users.manage',
  },
  {
    href: '/admin/content',
    label: 'المحتوى',
    icon: BookOpenText,
    permission: 'content.manage',
  },
  {
    href: '/admin/subjects',
    label: 'المواد الدراسية',
    icon: Library,
    permission: 'content.manage',
  },
  {
    href: '/admin/community',
    label: 'المجتمع',
    icon: MessageSquareText,
    permission: 'community.manage',
  },
  {
    href: '/admin/ai-monitor',
    label: 'تحليل AI',
    icon: Sparkles,
    permission: 'reports.manage',
  },
  {
    href: '/admin/codes',
    label: 'الأكواد',
    icon: KeyRound,
    permission: 'codes.manage',
  },
  {
    href: '/admin/questions',
    label: 'الأسئلة',
    icon: Shield,
    permission: 'exams.manage',
  },
  {
    href: '/admin/overrides',
    label: 'التعديلات',
    icon: Wrench,
    permission: 'users.manage',
  },
  {
    href: '/admin/watch-requests',
    label: 'طلبات المشاهدة',
    icon: Star,
    permission: 'watch_requests.manage',
  },
  {
    href: '/admin/forms',
    label: 'النماذج',
    icon: ClipboardList,
    permission: 'content.manage',
  },
  {
    href: '/admin/operations',
    label: 'العمليات',
    icon: Briefcase,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr',
    label: 'الموارد البشرية',
    icon: Users,
    permission: 'hr.manage',
  },
  {
    href: '/admin/finance',
    label: 'المالية والرواتب',
    icon: Coins,
    permission: 'users.manage',
  },
  {
    href: '/admin/chat',
    label: 'التواصل الداخلي',
    icon: MessageSquareText,
  },
  {
    href: '/admin/crm',
    label: 'الكول سنتر',
    icon: PhoneCall,
    permission: 'crm.manage',
  },
  {
    href: '/admin/media',
    label: 'الإنتاج والنشر',
    icon: Video,
    permission: 'media.manage',
  },
  {
    href: '/admin/reports',
    label: 'سجل الأمان والتقارير',
    icon: BarChart3,
    permission: 'reports.manage',
  },
];

const GROUP_CONFIG = [
  {
    id: 'users',
    label: 'شؤون الأعضاء',
    icon: Users,
    hrefs: ['/admin/students', '/admin/teachers', '/admin/assistants', '/admin/admins'],
  },
  {
    id: 'academic',
    label: 'التعليم والمحتوى',
    icon: Library,
    hrefs: ['/admin/subjects', '/admin/content', '/admin/questions', '/admin/forms'],
  },
  {
    id: 'operations',
    label: 'العمليات والتحكم',
    icon: Wrench,
    hrefs: ['/admin/watch-requests', '/admin/overrides', '/admin/codes', '/admin/community', '/admin/media'],
  },
  {
    id: 'admin_hr_finance',
    label: 'الإدارة والمالية',
    icon: Briefcase,
    hrefs: ['/admin/operations', '/admin/hr', '/admin/finance'],
  },
  {
    id: 'crm_chat',
    label: 'الاتصال والتواصل',
    icon: PhoneCall,
    hrefs: ['/admin/crm', '/assistant/crm', '/admin/chat'],
  },
  {
    id: 'reports',
    label: 'التقارير والمراقبة',
    icon: BarChart3,
    hrefs: ['/admin/ai-monitor', '/admin/reports'],
  },
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
  const user = useAuthStore((state) => state.user);
  const roles = user?.roles || [];
  const { hasPermission } = useHasPermission();
  const { isDark, themeVars, toggleTheme } = useAdminTheme();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>(() => {
    const initial: Record<string, boolean> = {};
    GROUP_CONFIG.forEach((group) => {
      if (group.hrefs.includes(activePath)) {
        initial[group.id] = true;
      }
    });
    return initial;
  });

  const toggleGroup = (groupId: string) => {
    setExpandedGroups((prev) => ({
      ...prev,
      [groupId]: !prev[groupId],
    }));
  };

  useRootOverscrollBackground();

  const resolvedNavItems = navItems.map((item) => {
    if (item.href === '/admin/crm') {
      const isCrmAgent =
        user?.roles?.includes('Assistant') &&
        !user?.roles?.includes('Admin') &&
        !user?.roles?.includes('Supervisor');
      if (isCrmAgent) {
        return { ...item, href: '/assistant/crm' as const };
      }
    }
    return item;
  });

  const filteredNavItems = resolvedNavItems.filter((item) =>
    hasPermission(item.permission)
  );

  const navGroups = GROUP_CONFIG.map((group) => {
    const items = filteredNavItems.filter((item) => group.hrefs.includes(item.href));
    return {
      ...group,
      items,
    };
  }).filter((group) => group.items.length > 0);

  const handleLogout = () => {
    clearAuth();
    router.replace('/login');
  };

  const mobilePrimaryItems = filteredNavItems.slice(0, 3);
  const mobileMoreItems = filteredNavItems.slice(3);
  const isMoreActive = mobileMoreItems.some((item) => item.href === activePath);

  return (
    <div
      dir="rtl"
      className="h-dvh overflow-hidden bg-[var(--admin-bg)] text-[var(--admin-text)] relative"
      style={themeVars}
    >
      <aside
        className="fixed right-0 top-0 z-50 hidden h-full w-20 flex-col justify-between bg-[var(--admin-sidebar)] py-6 shadow-[-12px_0_40px_var(--admin-shadow)] lg:flex group/sidebar transition-all duration-300 ease-in-out hover:w-64"
        role="navigation"
        aria-label="القائمة الرئيسية"
      >
        <div className="flex flex-col flex-1 min-h-0">
          <div className="flex justify-start pr-5 items-center transition-all duration-300 mb-7 flex-shrink-0">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-lg flex-shrink-0">
              <BookOpenText className="h-5 w-5" />
            </div>
            <span className="hidden group-hover/sidebar:block text-sm font-bold text-[var(--admin-text)] self-center mr-3 truncate whitespace-nowrap">
              منصة مسار
            </span>
          </div>

          <nav className="space-y-3 px-3 overflow-y-auto flex-1 min-h-0 [scrollbar-width:none] [-ms-overflow-style:none] [&::-webkit-scrollbar]:hidden">
            <Link
              href="/admin"
              prefetch={false}
              aria-label="الرئيسية"
              aria-current={activePath === '/admin' ? 'page' : undefined}
              className={`flex h-12 items-center justify-start pr-[18px] pl-4 rounded-full transition-all duration-300 gap-3 ${
                activePath === '/admin'
                  ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                  : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
              }`}
            >
              <Home className="h-5 w-5 flex-shrink-0" />
              <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
                الرئيسية
              </span>
            </Link>

            {(roles.includes('Assistant') || roles.includes('Staff')) && (
              <Link
                href="/assistant/dashboard"
                prefetch={false}
                className="flex h-12 items-center justify-start pr-[18px] pl-4 rounded-full transition-all duration-300 gap-3 text-emerald-500 hover:bg-emerald-500/10 font-bold border border-emerald-500/20"
              >
                <ArrowRight className="h-5 w-5 flex-shrink-0" />
                <span className="hidden group-hover/sidebar:block text-sm truncate whitespace-nowrap">
                  مساحة المساعدين
                </span>
              </Link>
            )}

            {navGroups.map((group) => {
              const GroupIcon = group.icon;
              const isExpanded = !!expandedGroups[group.id];
              const isGroupActive = group.hrefs.includes(activePath);

              return (
                <div key={group.id} className="space-y-1">
                  <button
                    type="button"
                    onClick={() => toggleGroup(group.id)}
                    className={`flex h-12 w-full items-center justify-between pr-[18px] pl-4 rounded-full transition-all duration-300 gap-3 outline-none ${
                      isGroupActive
                        ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-bold border border-[var(--admin-primary)]/10 shadow-[0_2px_8px_var(--admin-shadow)]'
                        : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                    title={group.label}
                  >
                    <div className="flex items-center gap-3">
                      <GroupIcon className="h-5 w-5 flex-shrink-0" />
                      <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
                        {group.label}
                      </span>
                    </div>
                    <ChevronDown
                      className={`hidden group-hover/sidebar:block h-4 w-4 transition-transform duration-200 flex-shrink-0 ${
                        isExpanded ? 'rotate-180' : ''
                      }`}
                    />
                  </button>

                  {isExpanded && (
                    <div className="space-y-1 mt-1 transition-all duration-300 pr-3 group-hover/sidebar:pr-5">
                      {group.items.map((item) => {
                        const Icon = item.icon;
                        const isActive = item.href === activePath;

                        return (
                          <Link
                            key={item.href}
                            href={item.href}
                            prefetch={false}
                            className={`flex h-10 items-center justify-start pr-[14px] pl-4 rounded-full transition-all duration-300 gap-3 ${
                              isActive
                                ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_4px_12px_var(--admin-shadow)]'
                                : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                            }`}
                            title={item.label}
                            aria-label={item.label}
                            aria-current={isActive ? 'page' : undefined}
                          >
                            <Icon className="h-4.5 w-4.5 flex-shrink-0" />
                            <span className="hidden group-hover/sidebar:block text-xs font-bold truncate whitespace-nowrap">
                              {item.label}
                            </span>
                          </Link>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            })}
          </nav>
        </div>

        <div className="space-y-3 px-3 flex-shrink-0 mt-4">
          <div className="flex justify-start px-1 items-center transition-all duration-300">
            <AnimatedThemeToggler
              checked={isDark}
              onToggle={toggleTheme}
              aria-label={
                isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'
              }
              title={
                isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'
              }
              className="flex h-12 w-12 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)] flex-shrink-0"
            />
            <span className="hidden group-hover/sidebar:block text-sm font-bold text-[var(--admin-muted)] self-center mr-3 truncate whitespace-nowrap">
              {isDark ? 'الوضع الفاتح' : 'الوضع الداكن'}
            </span>
          </div>
          {hasPermission('settings.manage') && (
            <Link
              href="/admin/settings"
              className="flex h-12 w-full items-center justify-start pr-[18px] pl-4 rounded-full text-[var(--admin-muted)] transition-all duration-300 gap-3 hover:bg-[var(--admin-hover)]"
              aria-label="الإعدادات"
              title="الإعدادات"
            >
              <Settings className="h-5 w-5 flex-shrink-0" />
              <span className="hidden group-hover/sidebar:block text-sm font-bold truncate whitespace-nowrap">
                الإعدادات
              </span>
            </Link>
          )}
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
                aria-label={
                  isDark
                    ? 'التحويل إلى الوضع الفاتح'
                    : 'التحويل إلى الوضع الداكن'
                }
                className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
              />
              {hasPermission('settings.manage') && (
                <Link
                  href="/admin/settings"
                  className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                  aria-label="الإعدادات"
                  title="الإعدادات"
                >
                  <Settings className="h-4 w-4" />
                </Link>
              )}
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
                <p className="mb-1 text-xs font-black tracking-[0.22em] text-[var(--admin-primary)]">
                  {sectionLabel}
                </p>
                <h1 className="mb-1 text-3xl font-extrabold tracking-tight text-[var(--admin-text)] lg:text-4xl">
                  {pageTitle}
                </h1>
                {subtitle ? (
                  <p className="max-w-3xl text-sm font-medium leading-6 text-[var(--admin-muted)]">
                    {subtitle}
                  </p>
                ) : null}
              </div>
              {headerAccessory}
            </div>
          </div>

          {action}
        </header>

        {subNav ? <div className="mb-8">{subNav}</div> : null}

        {children}

        <footer className="mt-14 flex flex-col items-center opacity-60 select-none">
          <div className="mb-4 h-px w-full bg-[var(--admin-border)]" />
          <p className="text-xs font-bold text-[var(--admin-muted)]">
            منصة مسار
          </p>
        </footer>
      </main>

      <nav
        className="fixed inset-x-0 bottom-0 z-40 border-t border-[var(--admin-border)] bg-[var(--admin-sidebar)]/90 px-3 py-3 backdrop-blur-xl lg:hidden"
        role="navigation"
        aria-label="القائمة السفلية"
      >
        <div className="mx-auto grid w-full max-w-md grid-cols-5 gap-2">
          <Link
            href="/admin"
            aria-current={activePath === '/admin' ? 'page' : undefined}
            className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] p-2 text-center text-xs font-black transition-all ${
              activePath === '/admin'
                ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md border border-transparent'
                : 'bg-[var(--admin-card)] text-[var(--admin-muted)] border border-[var(--admin-border)]'
            }`}
          >
            <Home className="h-5 w-5" />
            <span className="truncate w-full" style={{ lineHeight: 1 }}>
              الرئيسية
            </span>
          </Link>

          {mobilePrimaryItems.map((item) => {
            const Icon = item.icon;
            const isActive = item.href === activePath;

            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={isActive ? 'page' : undefined}
                className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] p-2 text-center text-xs font-black transition-all ${
                  isActive
                    ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md border border-transparent'
                    : 'bg-[var(--admin-card)] text-[var(--admin-muted)] border border-[var(--admin-border)]'
                }`}
              >
                <Icon className="h-5 w-5" />
                <span className="truncate w-full" style={{ lineHeight: 1 }}>
                  {item.label}
                </span>
              </Link>
            );
          })}
          <button
            type="button"
            onClick={() => setIsMobileMenuOpen(true)}
            className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] border p-2 text-center text-xs font-black transition-all ${
              isMoreActive || isMobileMenuOpen
                ? 'border-transparent bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md'
                : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'
            }`}
            aria-label="المزيد من صفحات الإدارة"
          >
            <Menu className="h-5 w-5" />
            <span className="truncate w-full" style={{ lineHeight: 1 }}>
              المزيد
            </span>
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
            className="fixed bottom-0 right-0 z-[60] w-full max-h-[80vh] overflow-y-auto rounded-t-[24px] border border-[var(--admin-border)] bg-[var(--admin-sidebar)] px-4 pb-[max(1rem,env(safe-area-inset-bottom))] pt-4 shadow-[0_-24px_70px_var(--admin-shadow)] lg:hidden"
            aria-label="قائمة الإدارة الإضافية"
          >
            <div className="mb-3 flex items-center justify-between">
              <p className="text-sm font-black text-[var(--admin-text)]">
                صفحات الإدارة
              </p>
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
                    className={`flex min-h-12 items-center gap-3 rounded-[16px] border px-3 py-2 text-sm font-bold transition ${
                      isActive
                        ? 'border-transparent bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                        : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'
                    }`}
                  >
                    <Icon className="h-5 w-5 shrink-0" />
                    <span className="min-w-0 truncate">{item.label}</span>
                  </Link>
                );
              })}
              {hasPermission('settings.manage') && (
                <Link
                  href="/admin/settings"
                  onClick={() => setIsMobileMenuOpen(false)}
                  className="flex min-h-12 items-center gap-3 rounded-[16px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)]"
                >
                  <Settings className="h-5 w-5 shrink-0" />
                  <span>الإعدادات</span>
                </Link>
              )}
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
