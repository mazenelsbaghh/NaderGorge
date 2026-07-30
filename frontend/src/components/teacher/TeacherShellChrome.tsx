'use client';

import {
  createContext,
  Fragment,
  ReactNode,
  useContext,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
} from 'react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import {
  Activity,
  BarChart3,
  BookOpenText,
  Briefcase,
  ChevronDown,
  ChevronLeft,
  Coins,
  GraduationCap,
  Home,
  KeyRound,
  LogOut,
  Menu,
  MessageSquareText,
  Settings,
  Shield,
  User,
  Users,
  X,
  type LucideIcon,
} from 'lucide-react';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { teacherService } from '@/services/teacher-service';
import { useAuthStore } from '@/stores/auth-store';
import { IntentLink } from '@/components/navigation/IntentLink';
import {
  NavigationFocusManager,
  SkipToContentLink,
} from '@/components/navigation/NavigationFocusManager';
import { useShellNavigationState } from '@/hooks/useShellNavigationState';
import { AccessibleOverlay } from '@/components/ui/AccessibleOverlay';

export type TeacherShellRoute =
  | '/teacher'
  | '/teacher/activity'
  | '/teacher/packages'
  | '/teacher/codes'
  | '/teacher/public-exams'
  | '/teacher/community'
  | '/teacher/comments'
  | '/teacher/essays'
  | '/teacher/students'
  | '/teacher/finance'
  | '/teacher/reports'
  | '/teacher/profile'
  | '/teacher/chat';

type TeacherShellChromeProps = {
  activePath: TeacherShellRoute;
  sectionLabel: string;
  pageTitle: string;
  subtitle?: string;
  action?: ReactNode;
  headerAccessory?: ReactNode;
  subNav?: ReactNode;
  children: ReactNode;
  floatingAction?: ReactNode;
  persistentRoot?: boolean;
};

type TeacherPageDescriptor = Omit<
  TeacherShellChromeProps,
  'children' | 'persistentRoot'
>;

type RegisteredTeacherPage = {
  pathname: string;
  descriptor: TeacherPageDescriptor;
};

const TeacherShellContext = createContext<{
  registerPage: (page: RegisteredTeacherPage | null) => void;
} | null>(null);

type TeacherNavItem = {
  href: TeacherShellRoute;
  label: string;
  icon: LucideIcon;
  group: 'content' | 'followup' | 'account';
  permission?: string;
};

const navItems: TeacherNavItem[] = [
  {
    href: '/teacher/reports',
    label: 'مركز التقارير',
    icon: BarChart3,
    group: 'followup',
    permission: 'reports',
  },
  {
    href: '/teacher/comments',
    label: 'تعليقات الطلاب',
    icon: MessageSquareText,
    group: 'followup',
    permission: 'comments',
  },
  {
    href: '/teacher/activity',
    label: 'نشاط الطلاب',
    icon: Activity,
    group: 'followup',
    permission: 'activity',
  },
  {
    href: '/teacher/students',
    label: 'الطلاب',
    icon: Users,
    group: 'followup',
    permission: 'students',
  },
  {
    href: '/teacher/community',
    label: 'مجتمع المدرس',
    icon: MessageSquareText,
    group: 'followup',
    permission: 'community',
  },
  {
    href: '/teacher/essays',
    label: 'تصحيح المقالي',
    icon: GraduationCap,
    group: 'followup',
    permission: 'essays',
  },
  {
    href: '/teacher/packages',
    label: 'المحتوى الدراسي',
    icon: BookOpenText,
    group: 'content',
    permission: 'content',
  },
  {
    href: '/teacher/codes',
    label: 'أكواد الوصول',
    icon: KeyRound,
    group: 'content',
    permission: 'codes',
  },
  {
    href: '/teacher/public-exams',
    label: 'الامتحانات العامة',
    icon: Shield,
    group: 'content',
    permission: 'publicExams',
  },
  {
    href: '/teacher/finance',
    label: 'المالية والأرباح',
    icon: Coins,
    group: 'account',
    permission: 'finance',
  },
  {
    href: '/teacher/profile',
    label: 'الملف الشخصي',
    icon: User,
    group: 'account',
    permission: 'profile',
  },
  {
    href: '/teacher/chat',
    label: 'التواصل الداخلي',
    icon: MessageSquareText,
    group: 'account',
    permission: 'chat',
  },
];

const GROUP_CONFIG: Array<{
  id: TeacherNavItem['group'];
  label: string;
  icon: LucideIcon;
  hrefs: TeacherShellRoute[];
}> = [
  {
    id: 'followup',
    label: 'المتابعة والتفاعل',
    icon: Users,
    hrefs: ['/teacher/activity', '/teacher/students', '/teacher/community', '/teacher/comments', '/teacher/essays', '/teacher/reports'],
  },
  {
    id: 'content',
    label: 'التعليم والمحتوى',
    icon: BookOpenText,
    hrefs: ['/teacher/packages', '/teacher/codes', '/teacher/public-exams'],
  },
  {
    id: 'account',
    label: 'الحساب والمالية',
    icon: Briefcase,
    hrefs: ['/teacher/finance', '/teacher/profile', '/teacher/chat'],
  },
];

const teacherLabelMap: Record<string, string> = {
  teacher: 'الرئيسية',
  activity: 'نشاط الطلاب',
  reports: 'مركز التقارير',
  students: 'الطلاب',
  packages: 'المحتوى الدراسي',
  codes: 'أكواد الوصول',
  'public-exams': 'الامتحانات العامة',
  community: 'المجتمع',
  comments: 'تعليقات الطلاب',
  essays: 'تصحيح المقالي',
  finance: 'المالية والأرباح',
  profile: 'الملف الشخصي',
  chat: 'التواصل الداخلي',
};

function TeacherBreadcrumbs() {
  const pathname = usePathname();
  if (!pathname?.startsWith('/teacher')) return null;

  const segments = pathname.split('/').filter(Boolean);
  const parentSegments = segments.slice(0, -1);
  if (parentSegments.length <= 1) return null;

  return (
    <nav aria-label="مسار التنقل" className="mb-4 flex items-center">
      <ol className="flex items-center gap-2 rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)]/60 px-4 py-2 text-sm font-bold text-[var(--admin-muted)] shadow-sm backdrop-blur-sm">
        <li className="flex items-center">
          <Home className="h-4 w-4 shrink-0" />
        </li>
        {parentSegments.map((segment, index) => {
          const href = '/' + segments.slice(0, index + 1).join('/');
          const isLast = index === parentSegments.length - 1;
          const label = teacherLabelMap[segment] ?? segment.replace(/[-_]/g, ' ');

          return (
            <Fragment key={href}>
              <li className="shrink-0">
                <ChevronLeft className="h-3 w-3 opacity-40" />
              </li>
              <li className="shrink-0">
                {isLast ? (
                  <span className="text-[var(--admin-text)]">{label}</span>
                ) : (
                  <Link
                    href={href}
                    className="transition-colors hover:text-[var(--admin-text)] hover:underline"
                  >
                    {label}
                  </Link>
                )}
              </li>
            </Fragment>
          );
        })}
      </ol>
    </nav>
  );
}

export function resolveTeacherShellRoute(pathname: string): TeacherShellRoute {
  if (pathname === '/teacher') return '/teacher';
  return (
    [...navItems]
      .sort((left, right) => right.href.length - left.href.length)
      .find(
        (item) =>
          pathname === item.href || pathname.startsWith(`${item.href}/`)
      )?.href ?? '/teacher'
  );
}

export function getTeacherShellDefaults(pathname: string): TeacherPageDescriptor {
  const activePath = resolveTeacherShellRoute(pathname);
  const item = navItems.find((entry) => entry.href === activePath);
  return {
    activePath,
    sectionLabel:
      activePath === '/teacher' ? 'لوحة المعلم' : 'مساحة المعلم',
    pageTitle:
      activePath === '/teacher'
        ? 'الرئيسية'
        : (item?.label ?? 'مساحة المعلم'),
  };
}

function TeacherPageRegistration({
  activePath,
  sectionLabel,
  pageTitle,
  subtitle,
  action,
  headerAccessory,
  subNav,
  children,
  floatingAction,
}: TeacherShellChromeProps) {
  const pathname = usePathname();
  const context = useContext(TeacherShellContext);

  useEffect(() => {
    if (!context) return;
    context.registerPage({
      pathname,
      descriptor: {
        activePath,
        sectionLabel,
        pageTitle,
        subtitle,
        action,
        headerAccessory,
        subNav,
        floatingAction,
      },
    });
    return () => {
      context.registerPage(null);
    };
  }, [
    action,
    activePath,
    context,
    floatingAction,
    headerAccessory,
    pageTitle,
    pathname,
    sectionLabel,
    subNav,
    subtitle,
  ]);

  return <>{children}</>;
}

/**
 * Route-level content descriptor. The teacher layout owns the only shell;
 * this component updates its page chrome without replacing the frame.
 */
export function TeacherPage(props: TeacherShellChromeProps) {
  return <TeacherPageRegistration {...props} />;
}

export function TeacherShellChrome(props: TeacherShellChromeProps) {
  const context = useContext(TeacherShellContext);
  if (context && !props.persistentRoot) {
    return <TeacherPageRegistration {...props} />;
  }
  return <TeacherShellFrame {...props} />;
}

function TeacherShellFrame({
  activePath,
  sectionLabel,
  pageTitle,
  subtitle,
  action,
  headerAccessory,
  subNav,
  children,
  floatingAction,
}: TeacherShellChromeProps) {
  const router = useRouter();
  const pathname = usePathname();
  const logout = useAuthStore((state) => state.logout);
  const user = useAuthStore((state) => state.user);
  const { isDark, themeVars, toggleTheme } = useAdminTheme();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const mobileMenuTriggerRef = useRef<HTMLButtonElement>(null);
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>(() => {
    const initial: Record<string, boolean> = {};
    GROUP_CONFIG.forEach((group) => {
      if (group.hrefs.includes(activePath)) {
        initial[group.id] = true;
      }
    });
    return initial;
  });
  const [workspacePermissions, setWorkspacePermissions] = useState<Set<string> | null>(null);
  const [isOwner, setIsOwner] = useState(false);
  const [registeredPage, setRegisteredPage] =
    useState<RegisteredTeacherPage | null>(null);
  const shellInstanceId = useId();
  const mainScrollRef = useRef<HTMLElement>(null);

  useRootOverscrollBackground();
  useShellNavigationState({
    surface: 'teacher',
    pathname,
    scrollRef: mainScrollRef,
    expandedGroups,
    setExpandedGroups,
  });

  const descriptor =
    registeredPage?.pathname === pathname
      ? registeredPage.descriptor
      : {
          activePath,
          sectionLabel,
          pageTitle,
          subtitle,
          action,
          headerAccessory,
          subNav,
          floatingAction,
        };
  activePath = descriptor.activePath;
  sectionLabel = descriptor.sectionLabel;
  pageTitle = descriptor.pageTitle;
  subtitle = descriptor.subtitle;
  action = descriptor.action;
  headerAccessory = descriptor.headerAccessory;
  subNav = descriptor.subNav;
  floatingAction = descriptor.floatingAction;

  const shellContext = useMemo(
    () => ({ registerPage: setRegisteredPage }),
    []
  );

  useEffect(() => {
    let cancelled = false;

    teacherService.getWorkspaceContext()
      .then((response) => {
        if (cancelled || !response.success || !response.data) return;
        setIsOwner(response.data.isOwner);
        setWorkspacePermissions(new Set(response.data.permissionKeys));
      })
      .catch(() => {
        if (!cancelled) setWorkspacePermissions(new Set());
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const canSeeItem = (item: TeacherNavItem) =>
    isOwner || !item.permission || Boolean(workspacePermissions?.has(item.permission));
  const canSeeDashboard = isOwner || Boolean(workspacePermissions?.has('dashboard'));
  const canSeeProfile = isOwner || Boolean(workspacePermissions?.has('profile'));

  const visibleNavItems = navItems.filter(canSeeItem);
  const navGroups = GROUP_CONFIG.map((group) => ({
    ...group,
    items: visibleNavItems.filter((item) => item.group === group.id),
  })).filter((group) => group.items.length > 0);

  const toggleGroup = (groupId: string) => {
    setExpandedGroups((prev) => ({
      ...prev,
      [groupId]: !prev[groupId],
    }));
  };

  const handleLogout = () => {
    void logout().finally(() => {
      router.replace('/login');
    });
  };

  const mobilePrimaryItems = visibleNavItems.slice(0, 3);
  const mobileMoreItems = visibleNavItems.slice(3);
  const isMoreActive = mobileMoreItems.some((item) => item.href === activePath);
  const userInitial = user?.fullName ? user.fullName[0].toUpperCase() : 'T';

  return (
    <TeacherShellContext.Provider value={shellContext}>
    <div
      dir="rtl"
      className="relative h-dvh max-h-dvh overflow-x-hidden bg-[var(--admin-bg)] text-[var(--admin-text)]"
      style={themeVars}
      data-testid="teacher-shell"
      data-shell-instance={shellInstanceId}
    >
      <SkipToContentLink />
      <NavigationFocusManager />
      <aside
        className="fixed right-0 top-0 z-50 hidden h-full w-20 flex-col justify-between bg-[var(--admin-sidebar)] py-6 shadow-[-12px_0_40px_var(--admin-shadow)] transition-all duration-300 ease-in-out lg:flex group/sidebar hover:w-64"
        role="navigation"
        aria-label="قائمة المدرس الرئيسية"
      >
        <div className="flex min-h-0 flex-1 flex-col">
          <div className="mb-7 flex shrink-0 items-center justify-start pr-5 transition-all duration-300">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-lg">
              <BookOpenText className="h-5 w-5" />
            </div>
            <span className="mr-3 hidden truncate whitespace-nowrap self-center text-sm font-bold text-[var(--admin-text)] group-hover/sidebar:block">
              لوحة المدرس
            </span>
          </div>

          <nav className="min-h-0 flex-1 space-y-3 overflow-y-auto px-3 [scrollbar-width:none] [-ms-overflow-style:none] [&::-webkit-scrollbar]:hidden">
            {canSeeDashboard ? (
              <IntentLink
                href="/teacher"
                aria-label="الرئيسية"
                aria-current={activePath === '/teacher' ? 'page' : undefined}
                className={`flex h-12 items-center justify-start gap-3 rounded-full pr-[18px] pl-4 transition-all duration-300 ${
                  activePath === '/teacher'
                    ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                    : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                }`}
              >
                <Home className="h-5 w-5 shrink-0" />
                <span className="hidden truncate whitespace-nowrap text-sm font-bold group-hover/sidebar:block">
                  الرئيسية
                </span>
              </IntentLink>
            ) : null}

            {navGroups.map((group) => {
              const GroupIcon = group.icon;
              const isExpanded = !!expandedGroups[group.id];
              const isGroupActive = group.hrefs.includes(activePath);

              return (
                <div key={group.id} className="space-y-1">
                  <button
                    type="button"
                    onClick={() => toggleGroup(group.id)}
                    className={`flex h-12 w-full items-center justify-between gap-3 rounded-full pr-[18px] pl-4 transition-all duration-300 outline-none ${
                      isGroupActive
                        ? 'border border-[var(--admin-primary)]/10 bg-[var(--admin-primary-15)] font-bold text-[var(--admin-primary)] shadow-[0_2px_8px_var(--admin-shadow)]'
                        : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                    title={group.label}
                  >
                    <div className="flex items-center gap-3">
                      <GroupIcon className="h-5 w-5 shrink-0" />
                      <span className="hidden truncate whitespace-nowrap text-sm font-bold group-hover/sidebar:block">
                        {group.label}
                      </span>
                    </div>
                    <ChevronDown
                      className={`hidden h-4 w-4 shrink-0 transition-transform duration-200 group-hover/sidebar:block ${
                        isExpanded ? 'rotate-180' : ''
                      }`}
                    />
                  </button>

                  {isExpanded ? (
                    <div className="mt-1 space-y-1 pr-3 transition-all duration-300 group-hover/sidebar:pr-5">
                      {group.items.map((item) => {
                        const Icon = item.icon;
                        const isActive = item.href === activePath;

                        return (
                          <IntentLink
                            key={item.href}
                            href={item.href}
                            className={`flex h-10 items-center justify-start gap-3 rounded-full pr-[14px] pl-4 transition-all duration-300 ${
                              isActive
                                ? 'bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-[0_4px_12px_var(--admin-shadow)]'
                                : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                            }`}
                            title={item.label}
                            aria-label={item.label}
                            aria-current={isActive ? 'page' : undefined}
                          >
                            <Icon className="h-4.5 w-4.5 shrink-0" />
                            <span className="hidden truncate whitespace-nowrap text-xs font-bold group-hover/sidebar:block">
                              {item.label}
                            </span>
                          </IntentLink>
                        );
                      })}
                    </div>
                  ) : null}
                </div>
              );
            })}
          </nav>
        </div>

        <div className="mt-4 shrink-0 space-y-3 px-3">
          {canSeeProfile ? (
            <IntentLink
              href="/teacher/profile"
              aria-label="فتح الملف الشخصي"
              title="فتح الملف الشخصي"
              className="flex h-12 w-full items-center justify-start gap-3 rounded-full pr-[14px] pl-4 text-[var(--admin-muted)] transition-all duration-300 hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
            >
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full border border-[var(--admin-border)] bg-[var(--admin-primary-15)] text-sm font-extrabold text-[var(--admin-primary)] shadow-sm">
                {userInitial}
              </span>
              <span className="hidden min-w-0 group-hover/sidebar:block">
                <span className="block truncate text-xs font-black text-[var(--admin-text)]">
                  {user?.fullName ?? 'معلم'}
                </span>
                <span className="block truncate text-[11px] font-bold text-[var(--admin-muted)]">
                  {user?.phone ?? 'الملف الشخصي'}
                </span>
              </span>
            </IntentLink>
          ) : null}

          <div className="flex items-center justify-start px-1 transition-all duration-300">
            <AnimatedThemeToggler
              checked={isDark}
              onToggle={toggleTheme}
              aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
              title={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
              className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
            />
            <span className="mr-3 hidden truncate whitespace-nowrap self-center text-sm font-bold text-[var(--admin-muted)] group-hover/sidebar:block">
              {isDark ? 'الوضع الفاتح' : 'الوضع الداكن'}
            </span>
          </div>

          <button
            type="button"
            onClick={handleLogout}
            className="flex h-12 w-full items-center justify-start gap-3 rounded-full pr-[18px] pl-4 text-[var(--admin-danger)] transition-all duration-300 hover:bg-[var(--admin-hover)]"
            title="تسجيل الخروج"
            aria-label="تسجيل الخروج"
          >
            <LogOut className="h-5 w-5 shrink-0" />
            <span className="hidden truncate whitespace-nowrap text-sm font-bold group-hover/sidebar:block">
              تسجيل الخروج
            </span>
          </button>
        </div>
      </aside>

      <main ref={mainScrollRef} id="main-content" className="app-shell-scroll relative z-10 h-dvh overflow-y-auto overscroll-y-auto px-4 py-6 pb-[calc(8rem+env(safe-area-inset-bottom))] lg:mr-24 lg:px-7 lg:py-8 lg:pb-10">
        <header className="mb-8 flex w-full flex-col gap-4 md:flex-row md:items-end md:justify-between lg:mb-9">
          <div className="w-full">
            <div className="mb-4 flex w-full items-center justify-end gap-2 lg:hidden">
              <AnimatedThemeToggler
                checked={isDark}
                onToggle={toggleTheme}
                aria-label={isDark ? 'التحويل إلى الوضع الفاتح' : 'التحويل إلى الوضع الداكن'}
                className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
              />
              {canSeeProfile ? (
                <Link
                  href="/teacher/profile"
                  className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)]"
                  aria-label="الملف الشخصي"
                  title="الملف الشخصي"
                >
                  <Settings className="h-4 w-4" />
                </Link>
              ) : null}
              <button
                type="button"
                onClick={handleLogout}
                className="flex h-10 w-10 items-center justify-center rounded-full text-[var(--admin-danger)] transition hover:bg-[var(--admin-hover)]"
                title="تسجيل الخروج"
                aria-label="تسجيل الخروج"
              >
                <LogOut className="h-4 w-4" />
              </button>
            </div>

            <TeacherBreadcrumbs />

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

        <footer className="mt-14 flex select-none flex-col items-center opacity-60">
          <div className="mb-4 h-px w-full bg-[var(--admin-border)]" />
          <p className="text-xs font-bold text-[var(--admin-muted)]">منصة مسار</p>
        </footer>
      </main>

      <nav
        className="fixed inset-x-0 bottom-0 z-40 border-t border-[var(--admin-border)] bg-[var(--admin-sidebar)]/90 px-3 py-3 backdrop-blur-xl lg:hidden"
        role="navigation"
        aria-label="قائمة المدرس السفلية"
      >
        <div className="mx-auto grid w-full max-w-md grid-cols-5 gap-2">
          {canSeeDashboard ? (
            <Link
              href="/teacher"
              aria-current={activePath === '/teacher' ? 'page' : undefined}
              className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] p-2 text-center text-xs font-black transition-all ${
                activePath === '/teacher'
                  ? 'border border-transparent bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md'
                  : 'border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'
              }`}
            >
              <Home className="h-5 w-5" />
              <span className="w-full truncate" style={{ lineHeight: 1 }}>
                الرئيسية
              </span>
            </Link>
          ) : null}

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
                    ? 'border border-transparent bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md'
                    : 'border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'
                }`}
              >
                <Icon className="h-5 w-5" />
                <span className="w-full truncate" style={{ lineHeight: 1 }}>
                  {item.label}
                </span>
              </Link>
            );
          })}

          {mobileMoreItems.length > 0 ? (
            <button
              ref={mobileMenuTriggerRef}
              type="button"
              onClick={() => setIsMobileMenuOpen(true)}
              className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] border p-2 text-center text-xs font-black transition-all ${
                isMoreActive || isMobileMenuOpen
                  ? 'border-transparent bg-gradient-to-r from-[var(--admin-primary)] to-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md'
                  : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-muted)]'
              }`}
              aria-label="المزيد من صفحات المدرس"
              aria-current={isMoreActive ? 'page' : undefined}
              aria-expanded={isMobileMenuOpen}
            >
              <Menu className="h-5 w-5" />
              <span className="w-full truncate" style={{ lineHeight: 1 }}>
                المزيد
              </span>
            </button>
          ) : null}
        </div>
      </nav>

      <AccessibleOverlay
        open={isMobileMenuOpen}
        onClose={() => setIsMobileMenuOpen(false)}
        label="قائمة المدرس الإضافية"
        triggerRef={mobileMenuTriggerRef}
        backdropClassName="backdrop-blur-sm"
        layerClassName="lg:hidden"
        className="bottom-0 right-0 max-h-[80vh] w-full overflow-y-auto rounded-t-[24px] border border-[var(--admin-border)] bg-[var(--admin-sidebar)] px-4 pb-[max(1rem,env(safe-area-inset-bottom))] pt-4 shadow-[0_-24px_70px_var(--admin-shadow)]"
        testId="teacher-mobile-drawer"
      >
            <div className="mb-3 flex items-center justify-between">
              <p className="text-sm font-black text-[var(--admin-text)]">صفحات المدرس</p>
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
              <button
                type="button"
                onClick={handleLogout}
                className="flex min-h-12 items-center gap-3 rounded-[16px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-3 py-2 text-sm font-bold text-[var(--admin-danger)] transition hover:bg-[var(--admin-hover)]"
              >
                <LogOut className="h-5 w-5 shrink-0" />
                <span>تسجيل الخروج</span>
              </button>
            </div>
      </AccessibleOverlay>

      {floatingAction}
    </div>
    </TeacherShellContext.Provider>
  );
}
