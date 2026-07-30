'use client';

import { ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
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
  MessageSquarePlus,
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
  BadgePercent,
  Building2,
  CalendarCheck2,
  CalendarClock,
  ChartNoAxesCombined,
  CircleDollarSign,
  ClockArrowUp,
  BadgeDollarSign,
  DatabaseZap,
  FileCheck2,
  Headphones,
  IdCard,
  Inbox,
  Network,
  ShieldCheck,
  Scale,
  UserRoundCheck,
  UserRoundPlus,
  Wallet,
  Gift,
  Tags,
  PanelRightClose,
  PanelRightOpen,
  Search,
  Coffee,
} from 'lucide-react';

import { useAdminTheme } from '@/components/admin/useAdminTheme';
import { AnimatedThemeToggler } from '@/components/ui/animated-theme-toggler';
import { useRootOverscrollBackground } from '@/hooks/useRootOverscrollBackground';
import { useAuthStore } from '@/stores/auth-store';
import { AdminBreadcrumbs } from './AdminBreadcrumbs';
import { useHasPermission } from '@/hooks/useHasPermission';
import { walletService, type WalletDto } from '@/services/wallet-service';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';

export type AdminShellRoute =
  | '/admin'
  | '/admin/users'
  | '/admin/students'
  | '/admin/assistants'
  | '/admin/admins'
  | '/admin/teachers'
  | '/admin/content'
  | '/admin/content/video-types'
  | '/admin/gifts'
  | '/admin/subjects'
  | '/admin/ai-monitor'
  | '/admin/codes'
  | '/admin/codes/templates'
  | '/admin/sales'
  | '/admin/public-exams'
  | '/admin/community'
  | '/admin/comments'
  | '/admin/questions'
  | '/admin/overrides'
  | '/admin/watch-requests'
  | '/admin/forms'
  | '/admin/hr'
  | '/admin/hr/my-attendance'
  | '/admin/hr/organization'
  | '/admin/hr/employees'
  | '/admin/hr/shifts'
  | '/admin/hr/attendance-policies'
  | '/admin/hr/breaks'
  | '/admin/hr/attendance-corrections'
  | '/admin/hr/attendance-adjustments'
  | '/admin/hr/leave'
  | '/admin/hr/approvals'
  | '/admin/hr/payroll'
  | '/admin/hr/performance'
  | '/admin/hr/cases'
  | '/admin/hr/recruitment'
  | '/admin/hr/lifecycle'
  | '/admin/hr/migration'
  | '/admin/hr/reports'
  | '/admin/operations'
  | '/admin/finance'
  | '/admin/teacher-finance'
  | '/admin/shared-packages'
  | '/admin/wallets'
  | '/admin/recharge-verification'
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
  | '/admin/media'
  | '/admin/live-support'
  | '/admin/live-support/ai'
  | '/admin/settings'
  | '/admin/popup';

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

type AdminNavItem = {
  href: AdminShellRoute;
  label: string;
  icon: typeof UserCog;
  permission?: string;
  adminOnly?: boolean;
};

const HR_NAV_ITEMS = [
  {
    href: '/admin/hr',
    label: 'لوحة الموارد البشرية',
    icon: Users,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/my-attendance',
    label: 'حضوري',
    icon: CalendarCheck2,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/organization',
    label: 'الهيكل والموظفون',
    icon: Network,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/shifts',
    label: 'الشفتات',
    icon: CalendarClock,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/attendance-policies',
    label: 'سياسات الحضور',
    icon: ShieldCheck,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/breaks',
    label: 'متابعة البريك والإذن',
    icon: Coffee,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/attendance-corrections',
    label: 'تصحيحات الحضور',
    icon: ClockArrowUp,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/attendance-adjustments',
    label: 'بدلات وخصومات الحضور',
    icon: BadgeDollarSign,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/leave',
    label: 'الإجازات والأرصدة',
    icon: IdCard,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/approvals',
    label: 'الموافقات',
    icon: Inbox,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/payroll',
    label: 'رواتب الموظفين',
    icon: CircleDollarSign,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/performance',
    label: 'الأداء والتقييمات',
    icon: ChartNoAxesCombined,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/cases',
    label: 'قضايا الموظفين',
    icon: Scale,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/recruitment',
    label: 'التوظيف',
    icon: UserRoundPlus,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/lifecycle',
    label: 'دورة حياة الموظف',
    icon: UserRoundCheck,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/migration',
    label: 'الترحيل والتشغيل',
    icon: DatabaseZap,
    permission: 'hr.manage',
  },
  {
    href: '/admin/hr/reports',
    label: 'تقارير قوة العمل',
    icon: FileCheck2,
    permission: 'hr.manage',
  },
] satisfies AdminNavItem[];

const navItems: AdminNavItem[] = [
  {
    href: '/admin/comments',
    label: 'تعليقات الطلاب',
    icon: MessageSquareText,
    permission: 'comments.manage',
  },
  {
    href: '/admin/students',
    label: 'الطلاب',
    icon: Users,
    permission: 'users.manage',
  },
  {
    href: '/admin/assistants',
    label: 'الموظفون والمساعدون',
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
    href: '/admin/content/video-types',
    label: 'أنواع الفيديو',
    icon: Tags,
    adminOnly: true,
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
    href: '/admin/gifts',
    label: 'الهدايا',
    icon: Gift,
    permission: 'gifts.manage',
  },
  {
    href: '/admin/sales',
    label: 'الخصومات',
    icon: BadgePercent,
    permission: 'sales.manage',
  },
  {
    href: '/admin/questions',
    label: 'الأسئلة',
    icon: Shield,
    permission: 'exams.manage',
  },
  {
    href: '/admin/public-exams',
    label: 'الامتحانات العامة',
    icon: ClipboardList,
    permission: 'public_exams.manage',
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
  ...HR_NAV_ITEMS,
  {
    href: '/admin/finance',
    label: 'المالية والرواتب',
    icon: Coins,
    permission: 'finance.manage',
  },
  {
    href: '/admin/teacher-finance',
    label: 'مركز مالية المدرسين',
    icon: BadgeDollarSign,
    permission: 'finance.manage',
  },
  {
    href: '/admin/shared-packages',
    label: 'الباكدجات المشتركة',
    icon: BookOpenText,
    permission: 'content.manage',
  },
  {
    href: '/admin/wallets',
    label: 'محافظ الشحن',
    icon: Wallet,
    permission: 'payments.manage',
  },
  {
    href: '/admin/recharge-verification',
    label: 'مطابقة الشحن',
    icon: ClipboardList,
    permission: 'payments.manage',
  },
  {
    href: '/admin/live-support',
    label: 'الدعم المباشر',
    icon: Headphones,
    permission: 'live_support.manage',
  },
  {
    href: '/admin/live-support/ai',
    label: 'الدعم الذكي AI',
    icon: Sparkles,
    permission: 'live_support.manage',
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
    label: 'مركز التقارير',
    icon: BarChart3,
    permission: 'reports.manage',
  },
  {
    href: '/admin/settings',
    label: 'الإعدادات',
    icon: Settings,
    permission: 'settings.manage',
  },
  {
    href: '/admin/popup',
    label: 'Popup المنصة',
    icon: MessageSquarePlus,
    permission: 'settings.manage',
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
    hrefs: ['/admin/subjects', '/admin/content', '/admin/content/video-types', '/admin/questions', '/admin/public-exams', '/admin/forms'],
  },
  {
    id: 'hr',
    label: 'الموارد البشرية',
    icon: Building2,
    hrefs: HR_NAV_ITEMS.map(({ href }) => href),
  },
  {
    id: 'operations',
    label: 'العمليات والتحكم',
    icon: Wrench,
    hrefs: ['/admin/watch-requests', '/admin/overrides', '/admin/codes', '/admin/gifts', '/admin/sales', '/admin/community', '/admin/comments', '/admin/media'],
  },
  {
    id: 'admin_hr_finance',
    label: 'الإدارة والمالية',
    icon: Briefcase,
    hrefs: ['/admin/operations', '/admin/finance', '/admin/shared-packages', '/admin/wallets', '/admin/recharge-verification'],
  },
  {
    id: 'teacher_finance',
    label: 'مالية المدرسين',
    icon: BadgeDollarSign,
    hrefs: ['/admin/teacher-finance'],
  },
  {
    id: 'crm_chat',
    label: 'الاتصال والتواصل',
    icon: PhoneCall,
    hrefs: ['/admin/crm', '/assistant/crm', '/admin/chat', '/admin/live-support', '/admin/live-support/ai'],
  },
  {
    id: 'reports',
    label: 'التقارير والمراقبة',
    icon: BarChart3,
    hrefs: ['/admin/ai-monitor', '/admin/reports', '/admin/settings', '/admin/popup'],
  },
];

const MOBILE_QUICK_ROUTE_ORDER: AdminShellRoute[] = [
  '/admin/hr',
  '/admin/finance',
  '/admin/content',
  '/admin/students',
  '/admin/live-support',
];

const formatWalletAmount = (amount: number) => {
  const formatter = new Intl.NumberFormat('en-US', {
    maximumFractionDigits: 0,
    notation: Math.abs(amount) >= 100_000 ? 'compact' : 'standard',
  });

  return formatter.format(amount);
};

function AdminWalletBalanceBadge({ compact = false }: { compact?: boolean }) {
  const [wallets, setWallets] = useState<WalletDto[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);
  const [hasLoaded, setHasLoaded] = useState(false);

  const loadWallets = useCallback(async () => {
    if (hasLoaded || isLoading) return;

    setIsLoading(true);
    try {
      const data = await walletService.getWallets();
      setWallets(data);
      setHasLoaded(true);
      setHasError(false);
    } catch {
      setHasError(true);
    } finally {
      setIsLoading(false);
    }
  }, [hasLoaded, isLoading]);

  const activeWallets = wallets.filter((wallet) => wallet.isActive);
  const totalBalance = activeWallets.reduce(
    (sum, wallet) => sum + Number(wallet.currentBalance || 0),
    0
  );

  return (
    <Link
      href="/admin/wallets"
      prefetch={false}
      onMouseEnter={() => void loadWallets()}
      onFocus={() => void loadWallets()}
      className="flex min-h-14 w-full items-center justify-start gap-3 rounded-[18px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-2 py-2 text-[var(--admin-text)] transition-all duration-300 hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
      aria-label="رصيد محافظ الشحن"
      title="رصيد محافظ الشحن"
    >
      <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
        <Wallet className="h-4.5 w-4.5" />
      </span>
      {!compact && <span className="min-w-0 flex-1">
        <span className="block truncate text-xs font-black text-[var(--admin-text)]">
          {isLoading
            ? 'جار تحميل الرصيد'
            : hasError
              ? 'تعذر تحميل الرصيد'
              : hasLoaded
                ? `${formatWalletAmount(totalBalance)} ج.م`
                : 'محافظ الشحن'}
        </span>
        <span className="block truncate text-[11px] font-bold text-[var(--admin-muted)]">
          {hasError
            ? 'افتح المحافظ للمراجعة'
            : hasLoaded
              ? `${activeWallets.length} محفظة نشطة`
              : 'يتم تحديثها عند الحاجة'}
        </span>
      </span>}
    </Link>
  );
}

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
  const pathname = usePathname();
  const logout = useAuthStore((state) => state.logout);
  const user = useAuthStore((state) => state.user);
  const roles = user?.roles || [];
  const { hasPermission } = useHasPermission();
  const { isDark, themeVars, toggleTheme } = useAdminTheme();
  const isHrSurface = activePath === '/admin/hr' || activePath.startsWith('/admin/hr/');
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const [navQuery, setNavQuery] = useState('');
  const [mobileNavQuery, setMobileNavQuery] = useState('');
  const navSearchRef = useRef<HTMLInputElement>(null);
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
    if (isSidebarCollapsed) {
      setIsSidebarCollapsed(false);
    }
    setExpandedGroups((prev) => ({
      ...prev,
      [groupId]: isSidebarCollapsed ? true : !prev[groupId],
    }));
  };

  useRootOverscrollBackground();

  useEffect(() => {
    const focusNavigationSearch = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 'k') return;
      const target = event.target as HTMLElement | null;
      if (target?.matches('input, textarea, select, [contenteditable="true"]')) return;

      event.preventDefault();
      setIsSidebarCollapsed(false);
      requestAnimationFrame(() => navSearchRef.current?.focus());
    };

    window.addEventListener('keydown', focusNavigationSearch);
    return () => window.removeEventListener('keydown', focusNavigationSearch);
  }, []);

  const resolvedNavItems = navItems.map((item) => {
    if (item.href === '/admin/crm') {
      const isCrmAgent =
        user?.roles?.some(r => r.toLowerCase().includes('assistant') || r.toLowerCase().includes('staff')) &&
        !user?.roles?.some(r => r.toLowerCase().includes('admin') || r.toLowerCase().includes('supervisor'));
      if (isCrmAgent) {
        return { ...item, href: '/assistant/crm' as const };
      }
    }
    return item;
  });

  let filteredNavItems = resolvedNavItems.filter((item) =>
    (!roles.includes('Admin') || item.href !== '/admin/hr/my-attendance') &&
    (!item.adminOnly || roles.includes('Admin')) && hasPermission(item.permission)
  );

  const allowedNavbarItems = user?.allowedNavbarItems;
  if (!roles.includes('Admin') && allowedNavbarItems && allowedNavbarItems.length > 0) {
    filteredNavItems = filteredNavItems.filter((item) =>
      allowedNavbarItems.some(allowedPath =>
        allowedPath === item.href || allowedPath.startsWith(item.href + '/')
      )
    );
  }

  const mobilePrimaryItems = [...filteredNavItems]
    .sort((left, right) => {
      const leftPriority = MOBILE_QUICK_ROUTE_ORDER.indexOf(left.href);
      const rightPriority = MOBILE_QUICK_ROUTE_ORDER.indexOf(right.href);
      return (leftPriority === -1 ? Number.MAX_SAFE_INTEGER : leftPriority) -
        (rightPriority === -1 ? Number.MAX_SAFE_INTEGER : rightPriority);
    })
    .slice(0, 3);
  const mobileMoreItems = filteredNavItems.filter((item) => !mobilePrimaryItems.includes(item));
  const isMoreActive = mobileMoreItems.some((item) => item.href === activePath);
  const normalizedNavQuery = navQuery.trim().toLocaleLowerCase('ar');
  const normalizedMobileNavQuery = mobileNavQuery.trim().toLocaleLowerCase('ar');
  const navGroups = useMemo(() => GROUP_CONFIG.map((group) => {
    const queryMatchesGroup = group.label.toLocaleLowerCase('ar').includes(normalizedNavQuery);
    const items = filteredNavItems.filter((item) =>
      group.hrefs.includes(item.href) &&
      (!normalizedNavQuery || queryMatchesGroup || item.label.toLocaleLowerCase('ar').includes(normalizedNavQuery))
    );
    return {
      ...group,
      items,
    };
  }).filter((group) => group.items.length > 0), [filteredNavItems, normalizedNavQuery]);

  const mobileNavGroups = useMemo(() => GROUP_CONFIG.map((group) => ({
    ...group,
    items: mobileMoreItems.filter((item) =>
      group.hrefs.includes(item.href) &&
      (
        !normalizedMobileNavQuery ||
        group.label.toLocaleLowerCase('ar').includes(normalizedMobileNavQuery) ||
        item.label.toLocaleLowerCase('ar').includes(normalizedMobileNavQuery)
      )
    ),
  })).filter((group) => group.items.length > 0), [mobileMoreItems, normalizedMobileNavQuery]);

  const handleLogout = () => {
    void logout().finally(() => {
      router.replace('/login');
    });
  };

  // Content management is shared with staff, but it must keep the staff
  // workspace chrome when rendered through the assistant route aliases.
  if (pathname.startsWith('/assistant/content')) {
    return (
      <AssistantShellChrome
        activePath="/assistant/content"
        sectionLabel={sectionLabel}
        pageTitle={pageTitle}
        subtitle={subtitle}
        action={action}
        headerAccessory={headerAccessory}
      >
        {subNav}
        {children}
        {floatingAction}
      </AssistantShellChrome>
    );
  }

  return (
    <div
      dir="rtl"
      className={`h-dvh max-h-dvh overflow-x-hidden bg-[var(--admin-bg)] text-[var(--admin-text)] relative ${isHrSurface ? 'hr-theme' : ''}`}
      style={themeVars}
    >
      <aside
        className={`fixed right-0 top-0 z-50 hidden h-full flex-col justify-between border-l border-[var(--admin-border)] bg-[var(--admin-sidebar)] py-5 lg:flex transition-[width] duration-200 ease-out ${
          isSidebarCollapsed ? 'w-20' : 'w-72'
        }`}
        role="navigation"
        aria-label="القائمة الرئيسية"
      >
        <div className="flex flex-col flex-1 min-h-0">
          <div className={`mb-5 flex items-center ${isSidebarCollapsed ? 'justify-center px-3' : 'justify-between px-5'} flex-shrink-0`}>
            <div className="flex min-w-0 items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] flex-shrink-0">
              <BookOpenText className="h-5 w-5" />
              </div>
              {!isSidebarCollapsed && (
                <span className="text-sm font-bold text-[var(--admin-text)] truncate whitespace-nowrap">
                  منصة مسار
                </span>
              )}
            </div>
            {!isSidebarCollapsed && (
              <button
                type="button"
                onClick={() => setIsSidebarCollapsed(true)}
                className="flex h-10 w-10 items-center justify-center rounded-xl text-[var(--admin-muted)] transition-colors hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                aria-label="طي القائمة الجانبية"
                title="طي القائمة الجانبية"
              >
                <PanelRightClose className="h-5 w-5" />
              </button>
            )}
            {isSidebarCollapsed && (
              <button
                type="button"
                onClick={() => setIsSidebarCollapsed(false)}
                className="absolute top-5 left-[-1.25rem] flex h-10 w-10 items-center justify-center rounded-l-xl border border-[var(--admin-border)] bg-[var(--admin-sidebar)] text-[var(--admin-muted)] transition-colors hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                aria-label="توسيع القائمة الجانبية"
                title="توسيع القائمة الجانبية"
              >
                <PanelRightOpen className="h-5 w-5" />
              </button>
            )}
          </div>

          {!isSidebarCollapsed && (
            <label className="relative mx-4 mb-4 block flex-shrink-0">
              <Search className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
              <input
                ref={navSearchRef}
                value={navQuery}
                onChange={(event) => setNavQuery(event.target.value)}
                className="h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] py-2 pr-10 pl-3 text-sm font-medium text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary-15)]"
                placeholder="ابحث عن صفحة أو أداة (Ctrl K)"
                aria-label="ابحث في صفحات الإدارة"
              />
            </label>
          )}

          <nav className={`space-y-2 overflow-y-auto flex-1 min-h-0 [scrollbar-width:none] [-ms-overflow-style:none] [&::-webkit-scrollbar]:hidden ${isSidebarCollapsed ? 'px-3' : 'px-4'}`}>
            <Link
              href="/admin"
              prefetch={false}
              aria-label="الرئيسية"
              aria-current={activePath === '/admin' ? 'page' : undefined}
              className={`flex h-11 items-center rounded-xl transition-colors gap-3 ${isSidebarCollapsed ? 'justify-center px-3' : 'justify-start px-3'} ${
                activePath === '/admin'
                  ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                  : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
              }`}
            >
              <Home className="h-5 w-5 flex-shrink-0" />
              {!isSidebarCollapsed && <span className="text-sm font-bold truncate whitespace-nowrap">الرئيسية</span>}
            </Link>

            {(roles.includes('Assistant') || roles.includes('Staff') || user?.allowedDomains?.includes('assistant')) && (
              <Link
                href="/assistant/dashboard"
                prefetch={false}
                className={`flex h-11 items-center rounded-xl transition-colors gap-3 font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] ${isSidebarCollapsed ? 'justify-center px-3' : 'justify-start px-3'}`}
              >
                <ArrowRight className="h-5 w-5 flex-shrink-0" />
                {!isSidebarCollapsed && <span className="text-sm truncate whitespace-nowrap">مساحة المساعدين</span>}
              </Link>
            )}

            {navGroups.map((group) => {
              const GroupIcon = group.icon;
              const isExpanded = normalizedNavQuery.length > 0 || !!expandedGroups[group.id];
              const isGroupActive = group.hrefs.includes(activePath);

              return (
                <div key={group.id} className="space-y-1">
                  <button
                    type="button"
                    onClick={() => toggleGroup(group.id)}
                    className={`flex h-11 w-full items-center rounded-xl transition-colors gap-3 outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${isSidebarCollapsed ? 'justify-center px-3' : 'justify-between px-3'} ${
                      isGroupActive
                        ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)] font-bold'
                        : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                    }`}
                    title={group.label}
                  >
                    <div className="flex items-center gap-3">
                      <GroupIcon className="h-5 w-5 flex-shrink-0" />
                      {!isSidebarCollapsed && <span className="text-sm font-bold truncate whitespace-nowrap">{group.label}</span>}
                    </div>
                    <ChevronDown
                      className={`h-4 w-4 transition-transform duration-200 flex-shrink-0 ${isSidebarCollapsed ? 'hidden' : ''} ${
                        isExpanded ? 'rotate-180' : ''
                      }`}
                    />
                  </button>

                  {isExpanded && !isSidebarCollapsed && (
                    <div className="space-y-1 mt-1 pr-4">
                      {group.items.map((item) => {
                        const Icon = item.icon;
                        const isActive = item.href === activePath;

                        return (
                          <Link
                            key={item.href}
                            href={item.href}
                            prefetch={false}
                            className={`flex h-10 items-center justify-start px-3 rounded-lg transition-colors gap-3 ${
                              isActive
                                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                                : 'text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
                            }`}
                            title={item.label}
                            aria-label={item.label}
                            aria-current={isActive ? 'page' : undefined}
                          >
                            <Icon className="h-4.5 w-4.5 flex-shrink-0" />
                            <span className="text-xs font-bold truncate whitespace-nowrap">{item.label}</span>
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

        <div className={`space-y-2 flex-shrink-0 mt-4 ${isSidebarCollapsed ? 'px-3' : 'px-4'}`}>
          {hasPermission('payments.manage') && <AdminWalletBalanceBadge compact={isSidebarCollapsed} />}
          <div className={`flex items-center ${isSidebarCollapsed ? 'justify-center' : 'justify-start'} px-1`}>
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
            {!isSidebarCollapsed && <span className="mr-3 self-center truncate whitespace-nowrap text-sm font-bold text-[var(--admin-muted)]">
              {isDark ? 'الوضع الفاتح' : 'الوضع الداكن'}
            </span>}
          </div>
          {hasPermission('settings.manage') && (
            <Link
              href="/admin/settings"
              className={`flex h-11 w-full items-center rounded-xl text-[var(--admin-muted)] transition-colors gap-3 hover:bg-[var(--admin-hover)] ${isSidebarCollapsed ? 'justify-center px-3' : 'justify-start px-3'}`}
              aria-label="الإعدادات"
              title="الإعدادات"
            >
              <Settings className="h-5 w-5 flex-shrink-0" />
              {!isSidebarCollapsed && <span className="text-sm font-bold truncate whitespace-nowrap">الإعدادات</span>}
            </Link>
          )}
          <button
            onClick={handleLogout}
            className={`flex h-11 w-full items-center rounded-xl text-[var(--admin-danger)] transition-colors gap-3 hover:bg-[var(--admin-hover)] ${isSidebarCollapsed ? 'justify-center px-3' : 'justify-start px-3'}`}
            title="تسجيل الخروج"
            aria-label="تسجيل الخروج"
          >
            <LogOut className="h-5 w-5 flex-shrink-0" />
            {!isSidebarCollapsed && <span className="text-sm font-bold truncate whitespace-nowrap">تسجيل الخروج</span>}
          </button>
        </div>
      </aside>

      <main className={`app-shell-scroll relative z-10 h-dvh overflow-y-auto overscroll-y-auto px-4 py-6 pb-[calc(8rem+env(safe-area-inset-bottom))] lg:px-7 lg:py-8 lg:pb-10 transition-[margin] duration-200 ${isSidebarCollapsed ? 'lg:mr-20' : 'lg:mr-72'}`}>
        <header className="mb-8 flex w-full flex-col gap-4 md:flex-row md:items-end md:justify-between lg:mb-9">
          <div className="w-full">
            <div className="flex items-center justify-end gap-2 mb-4 lg:hidden w-full">
              {hasPermission('payments.manage') && (
                <Link
                  href="/admin/wallets"
                  className="flex h-10 min-w-10 items-center justify-center rounded-full px-3 text-[var(--admin-primary)] transition hover:bg-[var(--admin-hover)]"
                  aria-label="رصيد محافظ الشحن"
                  title="رصيد محافظ الشحن"
                >
                  <Wallet className="h-4 w-4" />
                </Link>
              )}
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
        className="fixed inset-x-0 bottom-0 z-40 border-t border-[var(--admin-border)] bg-[var(--admin-sidebar)] px-3 py-3 lg:hidden"
        role="navigation"
        aria-label="القائمة السفلية"
      >
        <div className="mx-auto grid w-full max-w-md grid-cols-5 gap-2">
          <Link
            href="/admin"
            aria-current={activePath === '/admin' ? 'page' : undefined}
            className={`flex min-h-14 flex-col items-center justify-center gap-1 rounded-[18px] p-2 text-center text-xs font-black transition-all ${
              activePath === '/admin'
                ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
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
                    ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
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
                ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
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
            className="fixed inset-0 z-50 bg-black/35 lg:hidden"
            aria-label="إغلاق قائمة الإدارة"
            onClick={() => setIsMobileMenuOpen(false)}
          />
          <aside
            className="fixed bottom-0 right-0 z-[60] w-full max-h-[80vh] overflow-y-auto rounded-t-2xl border border-[var(--admin-border)] bg-[var(--admin-sidebar)] px-4 pb-[max(1rem,env(safe-area-inset-bottom))] pt-4 lg:hidden"
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
            <label className="relative mb-4 block">
              <Search className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
              <input
                value={mobileNavQuery}
                onChange={(event) => setMobileNavQuery(event.target.value)}
                className="h-11 w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] py-2 pr-10 pl-3 text-sm font-medium text-[var(--admin-text)] outline-none placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] focus:ring-2 focus:ring-[var(--admin-primary-15)]"
                placeholder="ابحث عن صفحة أو أداة"
                aria-label="ابحث في صفحات الإدارة"
              />
            </label>
            <div className="space-y-5">
              {mobileNavGroups.map((group) => {
                const GroupIcon = group.icon;
                return (
                  <section key={group.id} aria-label={group.label}>
                    <div className="mb-2 flex items-center gap-2 text-sm font-black text-[var(--admin-text)]">
                      <GroupIcon className="h-4 w-4 text-[var(--admin-primary)]" />
                      <h2>{group.label}</h2>
                    </div>
                    <div className="grid grid-cols-2 gap-2">
                      {group.items.map((item) => {
                        const Icon = item.icon;
                        const isActive = item.href === activePath;
                        return (
                          <Link
                            key={item.href}
                            href={item.href}
                            onClick={() => setIsMobileMenuOpen(false)}
                            aria-current={isActive ? 'page' : undefined}
                            className={`flex min-h-12 items-center gap-3 rounded-xl border px-3 py-2 text-sm font-bold transition-colors ${
                              isActive
                                ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                                : 'border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'
                            }`}
                          >
                            <Icon className="h-5 w-5 shrink-0" />
                            <span className="min-w-0 truncate">{item.label}</span>
                          </Link>
                        );
                      })}
                    </div>
                  </section>
                );
              })}
              {mobileNavGroups.length === 0 && (
                <p className="rounded-xl bg-[var(--admin-hover)] px-3 py-4 text-sm font-medium text-[var(--admin-muted)]">
                  لا توجد صفحات مطابقة للبحث.
                </p>
              )}
            </div>
            <div className="mt-5 grid grid-cols-2 gap-2">
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
